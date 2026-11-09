// Anonymous play telemetry (schema: docs/telemetry.md, v1). Owned by backend/analytics.
// Tracker turns dispatch() results into events and is pure (clock and output injected), so the fake-data
// generator in server/tools reuses it. initTelemetry() wires it to the browser: local queue, batching, sendBeacon.
import { STEPS, tileAt, minBodies, stars as coreStars } from './core.js';

export const SCHEMA_VERSION = 1;
export const IDLE_MS = 30000;

// R/r are read through tileAt, so a flipped wall reports its current state (flip_up blocks, flip_down is open).
const TILE_KIND = {
  '#': 'wall', '.': 'floor', E: 'exit', o: 'pit', '~': 'ice', x: 'cracked', '=': 'bridge', m: 'mud',
  Q: 'door', L: 'lever', R: 'flip_up', r: 'flip_down',
};

export function tileKind(s, i) {
  if (i < 0) return 'edge';
  if (i === s.start) return 'start';
  const t = tileAt(s, i);
  if (t >= 'a' && t <= 'c') return 'plate';
  if (t >= 'A' && t <= 'C') return 'door';
  if (t >= '1' && t <= '7') return 'gate';
  return TILE_KIND[t] ?? 'floor';
}

// FNV-1a 32-bit over the string's UTF-16 code units (maps are ASCII, so this equals hashing the bytes). 8 hex chars.
export function fnv1a(str) {
  let h = 0x811c9dc5;
  for (let i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return h.toString(16).padStart(8, '0');
}

// Stable id of a stage's content (docs/telemetry.md "levelKey"): body count + map rows padded to the level width
// with '#', start written as 'S', joined by '\n'. Survives reordering; changes when the map or body count changes.
export function levelKey(level) {
  const rows = [];
  for (let y = 0; y < level.h; y++) {
    let row = '';
    for (let x = 0; x < level.w; x++) {
      const i = y * level.w + x;
      row += i === level.start ? 'S' : level.tiles[i];
    }
    rows.push(row);
  }
  return fnv1a(`${level.bodies}\n${rows.join('\n')}`);
}

// Stars (design/levels/stars.md, plan B): core.js stars(), under the name the server and tests use.
export const starsFor = coreStars;

// Fewest bodies for a level (solver), cached by map. levels.json carries it as expected.minBodies.
const MIN_BODIES = new Map();
export function minBodiesOf(level) {
  const key = levelKey(level);
  if (!MIN_BODIES.has(key)) MIN_BODIES.set(key, minBodies(level)?.bodies ?? null);
  return MIN_BODIES.get(key);
}

const xy = (s, i) => (i < 0 || i == null ? null : [i % s.level.w, Math.floor(i / s.level.w)]);
const bodyNo = (s) => s.level.bodies - s.bodies + 1;
const LOST = { fall: 'fell', vanish: 'vanish', stone: 'statue', switch: 'switch', bridge: 'bridge', seal: 'seal' };

export class Tracker {
  // emit(event) receives { seq, t, type, ...fields }. now() returns ms. lang: UI language ("ko", "en"), sent on
  // session_start and level_start when set (docs/telemetry.md); change it with setLang when the player switches.
  constructor({ emit, now = () => Date.now(), idleMs = IDLE_MS, attempts = new Map(), lang = null }) {
    this.emit = emit;
    this.lang = lang;
    this.now = now;
    this.idleMs = idleMs;
    this.attempts = attempts; // level number -> times started on this device
    this.seq = 0;
    this.sessionT = null;
    this.lastInput = null;
    this.run = null; // current attempt
  }

  send(type, fields = {}) {
    this.emit({ seq: this.seq++, t: Math.round(this.now()), type, ...fields });
  }

  setLang(lang) { this.lang = lang || null; }
  langField() { return this.lang ? { lang: this.lang } : {}; }

  sessionStart(fields = {}) {
    if (this.sessionT == null) this.sessionT = this.now();
    this.lastInput = this.now();
    this.send('session_start', { ...fields, ...this.langField() });
  }

  // chapter: the stage's chapter id from levels.js (optional, sent only when the stage has one).
  levelStart(state, level, levelName, via, chapter) {
    const attempt = (this.attempts.get(level) ?? 0) + 1;
    this.attempts.set(level, attempt);
    this.run = { level, t0: this.now(), moves: 0, undos: 0, restarts: 0, hints: 0, fails: 0, done: false };
    this.lastInput = this.now();
    this.send('level_start', { level, levelName, levelKey: levelKey(state.level), ...(chapter ? { chapter } : {}), attempt, via, bodies: state.level.bodies, ...this.langField() });
  }

  where(s) {
    return { level: this.run.level, body: bodyNo(s), step: s.used, at: xy(s, s.pos) };
  }

  // Call on every player input before acting on it. Emits idle if the pause before this input was long.
  input(state) {
    const t = this.now();
    if (this.run && this.lastInput != null && t - this.lastInput >= this.idleMs) {
      this.send('idle', {
        ...this.where(state), idleMs: Math.round(t - this.lastInput), status: state.status,
        bodiesLeft: state.bodies, statues: state.statues.length,
      });
    }
    this.lastInput = t;
  }

  // before: state passed to dispatch, cmd: the command, r: { state, events } from dispatch.
  command(before, cmd, r) {
    if (!this.run) return;
    this.input(before);
    const run = this.run;
    const after = r.state;
    const L = run.level;
    if (cmd.type === 'restart') {
      if (run.done) return this.levelStart(after, L, before.level.name, 'again');
      // A restart at the stage start (no previous state: fresh, or undone back to it) changes nothing: not recorded or
      // counted, like an undo with nothing to undo (docs/architecture.md 6.5).
      if (!before.prev) return;
      this.send('restart', { ...this.where(before), moves: run.moves });
      run.restarts++;
      return;
    }
    if (cmd.type === 'undo') {
      if (after === before) return;
      this.send('undo', { ...this.where(before), status: before.status });
      run.undos++;
      return;
    }
    const types = r.events.map((e) => e.type);
    // Ignored input (moving while choosing KEEP/SWITCH or after the stage ended): nothing to record.
    if (after === before && !types.includes('bump')) return;
    const evAt = (type) => r.events.find((e) => e.type === type)?.at;
    let land = before.pos;

    if (cmd.type === 'move') {
      const first = neighborOf(before, cmd.dir);
      const blocked = types.includes('bump');
      // step = the step count after this move: entering mud costs 2 (core.js step). A blocked move reports used + 1.
      const step = before.used + (!blocked && first >= 0 && tileAt(before, first) === 'm' ? 2 : 1);
      const base = { level: L, body: bodyNo(before), step, dir: cmd.dir, from: xy(before, before.pos) };
      if (blocked) {
        const at = evAt('bump');
        const blockedBy = at >= 0 && before.statues.includes(at) ? 'statue' : tileKind(before, at);
        this.send('move', { ...base, to: xy(before, at), result: 'blocked', blockedBy });
        return;
      }
      run.moves++;
      // Landing cell (after any ice slide), taken from the rule events.
      land = evAt('fall') ?? evAt('bridge') ?? evAt('win') ?? evAt('seal') ?? evAt('vanish') ??
        evAt('choose') ?? evAt('stone') ?? evAt('step');
      const firstTile = tileKind(before, first);
      let result = 'moved';
      if (types.includes('fall')) result = 'fell';
      else if (types.includes('bridge')) result = 'bridged';
      else if (land !== first) result = 'slid';
      else if (firstTile === 'door') result = 'door';
      else if (firstTile === 'gate') result = 'gate';
      const ev = { ...base, to: xy(before, land), result };
      if (types.includes('crumble')) ev.crumbled = true;
      this.send('move', ev);
      if (step === STEPS && !types.includes('fall')) {
        const effect = types.includes('win') ? 'win' : types.includes('bridge') ? 'bridge' : types.includes('seal') ? 'seal'
          : types.includes('vanish') ? 'vanish' : types.includes('choose') ? 'choose' : 'statue';
        // tile is the landing cell before the step (a lever, not the walls it flips); flipped: the lever flipped the walls.
        this.send('seventh_step', { level: L, body: bodyNo(before), at: xy(before, land), tile: tileKind(before, land), effect, ...(types.includes('flip') ? { flipped: true } : {}) });
      }
    } else if (cmd.type === 'keep' || cmd.type === 'switch') {
      this.send(cmd.type, { level: L, body: bodyNo(before), at: xy(before, before.pos) });
    }

    for (const e of r.events) {
      if (LOST[e.type]) {
        const reason = LOST[e.type];
        this.send('body_lost', { level: L, body: bodyNo(before), at: xy(before, e.at), reason, wasted: reason === 'fell' || reason === 'vanish' });
      }
    }
    if (after.status === 'lose') {
      run.fails++;
      this.send('level_fail', { level: L, at: xy(before, land), moves: run.moves, bodiesUsed: before.level.bodies });
    }
    if (after.status === 'win' && !run.done) {
      run.done = true;
      const bodiesUsed = bodyNo(after);
      const stars = starsFor({ bodiesUsed, minBodies: minBodiesOf(after.level), hints: run.hints, undos: run.undos, restarts: run.restarts });
      this.send('level_clear', {
        level: L, durationMs: Math.round(this.now() - run.t0), moves: run.moves, undos: run.undos,
        restarts: run.restarts, hints: run.hints, fails: run.fails, bodiesUsed, ...(stars ? { stars } : {}),
      });
    }
  }

  hint(state) {
    if (!this.run) return;
    this.input(state);
    this.run.hints++;
    this.send('hint', this.where(state));
  }

  // Leaving the current stage for another one (call before levelStart of the new one).
  leave(state, toLevel) {
    if (!this.run) return;
    this.flushIdle(state);
    const run = this.run;
    if (!run.done) {
      this.send('level_leave', {
        ...this.where(state), toLevel, durationMs: Math.round(this.now() - run.t0),
        moves: run.moves, undos: run.undos, restarts: run.restarts, hints: run.hints, fails: run.fails,
      });
    }
    this.run = null;
  }

  // An idle pause still in progress when the stage or tab is left.
  flushIdle(state) {
    if (state && this.run) this.input(state);
  }

  sessionEnd(state, reason) {
    this.flushIdle(state);
    const f = { reason, sessionMs: Math.round(this.now() - (this.sessionT ?? this.now())) };
    if (this.run && state) Object.assign(f, this.where(state), { cleared: this.run.done });
    this.send('session_end', f);
  }
}

function neighborOf(s, dir) {
  const d = { up: [0, -1], down: [0, 1], left: [-1, 0], right: [1, 0] }[dir];
  const { w, h } = s.level;
  const x = (s.pos % w) + d[0];
  const y = Math.floor(s.pos / w) + d[1];
  return x < 0 || y < 0 || x >= w || y >= h ? -1 : y * w + x;
}

// ---------- Browser side ----------

const KEY_OFF = 'seven-steps-telemetry';
const KEY_URL = 'seven-steps-telemetry-url';
const KEY_QUEUE = 'seven-steps-telemetry-queue';
const KEY_PLAYER = 'seven-steps-player';
const KEY_ATTEMPTS = 'seven-steps-attempts';
const QUEUE_MAX = 1000;
const BATCH_MAX = 100;
const BYTES_MAX = 60000;

const store = {
  get(k) { try { return localStorage.getItem(k); } catch { return null; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch { /* storage blocked: keep going */ } },
  del(k) { try { localStorage.removeItem(k); } catch { /* ignore */ } },
};

const uuid = () => (globalThis.crypto?.randomUUID?.() ??
  'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 3) | 8).toString(16);
  }));

function resolveEndpoint(params) {
  const p = params.get('telemetry');
  if (p === 'off' || p === '0') return null;
  if (p && /^https?:\/\//.test(p)) { store.set(KEY_URL, p); return p.replace(/\/$/, ''); }
  const saved = store.get(KEY_URL);
  if (saved) return saved.replace(/\/$/, '');
  if (location.pathname.startsWith('/play/')) return location.origin; // served by the collection server
  if (['localhost', '127.0.0.1'].includes(location.hostname)) return 'http://localhost:8787';
  return null;
}

// Returns a Tracker-like object; all methods are no-ops when telemetry is off. Never throws into the game.
export function initTelemetry({ build = 'proto-dev', client = 'web-proto', params = new URLSearchParams(location.search), getState = () => null, lang = 'ko' } = {}) {
  if (['off', '0'].includes(params.get('telemetry'))) store.set(KEY_OFF, 'off');
  else if (params.get('telemetry')) store.del(KEY_OFF);
  const enabled = store.get(KEY_OFF) !== 'off';
  const endpoint = resolveEndpoint(params);

  const isNew = !store.get(KEY_PLAYER);
  let playerId = store.get(KEY_PLAYER);
  if (!playerId) { playerId = uuid(); store.set(KEY_PLAYER, playerId); }
  const sessionId = uuid();
  let attempts;
  try { attempts = new Map(JSON.parse(store.get(KEY_ATTEMPTS) ?? '[]')); } catch { attempts = new Map(); }

  // Queue items keep their own envelope so a reload can still send an old session's events.
  let queue = [];
  try { queue = JSON.parse(store.get(KEY_QUEUE) ?? '[]'); } catch { queue = []; }
  const save = () => store.set(KEY_QUEUE, JSON.stringify(queue));
  let backoff = 0;
  let retryAt = 0;
  let sending = false;

  const tracker = new Tracker({
    attempts,
    lang,
    emit: (e) => {
      if (!enabled) return;
      queue.push({ s: sessionId, e });
      if (queue.length > QUEUE_MAX) queue.splice(0, queue.length - QUEUE_MAX);
      save();
      if (e.type === 'level_start') store.set(KEY_ATTEMPTS, JSON.stringify([...attempts]));
      if (queue.length >= 20) flush();
    },
  });

  function nextBatch() {
    const sid = queue[0].s;
    const events = [];
    let bytes = 300;
    for (const item of queue) {
      if (item.s !== sid || events.length >= BATCH_MAX) break;
      const n = JSON.stringify(item.e).length + 1;
      if (bytes + n > BYTES_MAX && events.length) break;
      bytes += n;
      events.push(item.e);
    }
    return { body: JSON.stringify({ v: SCHEMA_VERSION, playerId, sessionId: sid, client, build, events }), count: events.length };
  }

  async function flush() {
    if (!enabled || !endpoint || sending || !queue.length || Date.now() < retryAt) return;
    sending = true;
    try {
      while (queue.length) {
        const { body, count } = nextBatch();
        let res;
        try {
          res = await fetch(`${endpoint}/v1/events`, {
            method: 'POST', headers: { 'Content-Type': 'text/plain' }, body, keepalive: body.length < 60000,
            signal: AbortSignal.timeout?.(10000),
          });
        } catch { res = null; }
        if (!res || res.status === 429 || res.status >= 500) {
          backoff = Math.min(backoff ? backoff * 2 : 5000, 300000);
          const ra = Number(res?.headers.get('Retry-After'));
          retryAt = Date.now() + (ra > 0 ? ra * 1000 : backoff);
          break;
        }
        backoff = 0;
        queue.splice(0, count);
        save();
      }
    } finally { sending = false; }
  }

  function beacon() {
    if (!enabled || !endpoint || !navigator.sendBeacon) return;
    while (queue.length) {
      const { body, count } = nextBatch();
      if (!navigator.sendBeacon(`${endpoint}/v1/events`, new Blob([body], { type: 'text/plain' }))) break;
      queue.splice(0, count);
    }
    save();
  }

  let hidden = false;
  const onHide = (reason) => {
    if (hidden) return;
    hidden = true;
    tracker.sessionEnd(getState(), reason);
    beacon();
  };
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') onHide('hidden');
    else if (hidden) {
      hidden = false;
      tracker.sessionStart({ firstTime: false, cleared: 0, resumed: true });
    }
  });
  window.addEventListener('pagehide', () => onHide('unload'));
  setInterval(flush, 5000);

  const safe = (fn) => (...a) => { try { return fn(...a); } catch (err) { console.warn('[telemetry]', err); } };
  return {
    enabled, endpoint, playerId, sessionId,
    sessionStart: safe((f) => tracker.sessionStart({ firstTime: isNew, ...f })),
    levelStart: safe((...a) => tracker.levelStart(...a)),
    command: safe((...a) => tracker.command(...a)),
    hint: safe((s) => tracker.hint(s)),
    leave: safe((...a) => tracker.leave(...a)),
    setLang: safe((l) => tracker.setLang(l)),
    flush: safe(flush),
    setEnabled(on) {
      if (on) store.del(KEY_OFF);
      else { store.set(KEY_OFF, 'off'); store.del(KEY_QUEUE); }
      location.reload();
    },
  };
}
