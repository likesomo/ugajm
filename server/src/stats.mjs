import { starsFor } from '../../prototypes/seven-steps/src/telemetry.js';

// Aggregations for the dashboard. Works on rows from db.query(); events are replayed per session
// into "attempts" (level_start .. level_clear | level_leave | end of session) so every metric shares one definition.
export const QUIT_AFTER_MS = 30 * 60 * 1000; // an attempt with no event for this long counts as abandoned

export const STUCK_UNDO_STREAK = 5; // this many undos in a row counts as being stuck (like a 30s idle)

// Replays rows (ordered by player, session, seq) into attempts. Also tags each row with row.attemptId.
// minMovesFor(levelKey, level) optionally supplies the solver's shortest move count.
export function buildAttempts(rows, now = Date.now(), minMovesFor = () => null) {
  const attempts = [];
  const sessions = [];
  let sess = null;
  let cur = null;
  let closed = null; // the attempt that just ended (events after a clear, e.g. undo, still belong to it)

  const close = (outcome, t, durationMs) => {
    if (!cur) return;
    cur.outcome = outcome;
    cur.tEnd = t;
    cur.durationMs = durationMs ?? Math.max(0, t - cur.t0);
    if (outcome === 'clear' && cur.stuckEnd) {
      cur.lastStuckToClearMoves = cur.moves - cur.stuckEnd.moves;
      cur.lastStuckToClearMs = Math.max(0, t - cur.stuckEnd.t);
    }
    closed = cur;
    cur = null;
  };
  const endSession = () => {
    if (!sess) return;
    if (cur) close(now - sess.lastT > QUIT_AFTER_MS ? 'quit' : 'open', sess.lastT);
    sessions.push(sess);
    sess = null;
  };

  for (const r of rows) {
    if (!sess || sess.sessionId !== r.session_id) {
      endSession();
      sess = {
        sessionId: r.session_id, playerId: r.player_id, client: r.client, build: r.build,
        t0: r.t, lastT: r.t, endReason: null, attempts: [], firstTime: null,
      };
    }
    sess.lastT = Math.max(sess.lastT, r.t);
    const d = r.data;
    r.attemptId = null;
    switch (r.type) {
      case 'level_start': {
        if (cur) close('leave', r.t);
        const prev = sess.attempts.at(-1);
        if (prev && prev.level !== d.level) prev.nextLevelStarted = true;
        cur = {
          id: `${r.session_id}:${r.seq}`,
          playerId: r.player_id, sessionId: r.session_id, client: r.client, build: r.build, sessionFirstTime: sess.firstTime,
          level: d.level, levelName: d.levelName ?? '', levelKey: d.levelKey ?? '', chapter: d.chapter ?? '', attempt: d.attempt, via: d.via ?? '',
          bodies: d.bodies ?? null, t0: r.t, tEnd: null,
          outcome: 'open', durationMs: 0, moves: 0, blocked: 0, undos: 0, restarts: 0, hints: 0, fails: 0,
          idles: 0, idleMs: 0, idleMsMax: 0, firstHintMs: null, wasted: 0, lost: {}, keeps: 0, switches: 0,
          bodiesUsed: null, minMoves: minMovesFor(d.levelKey, d.level), nextLevelStarted: false,
          undoStreak: 0, stuckEnd: null, lastStuckToClearMoves: null, lastStuckToClearMs: null,
        };
        attempts.push(cur);
        sess.attempts.push(cur);
        r.attemptId = cur.id;
        break;
      }
      case 'session_end':
        sess.endReason = d.reason;
        if (cur) r.attemptId = cur.id;
        break;
      case 'session_start':
        sess.endReason = null;
        if (sess.firstTime == null) sess.firstTime = d.firstTime ?? null;
        break;
      default: {
        if (!cur) { if (closed && closed.sessionId === r.session_id && closed.level === d.level) r.attemptId = closed.id; break; }
        if (d.level !== cur.level) break;
        r.attemptId = cur.id;
        // A run of undos ends when any other input arrives.
        if (r.type !== 'undo') {
          if (cur.undoStreak >= STUCK_UNDO_STREAK) cur.stuckEnd = { t: r.t, moves: cur.moves };
          if (r.type !== 'idle') cur.undoStreak = 0;
        }
        if (r.type === 'move') { if (d.result === 'blocked') cur.blocked++; else cur.moves++; }
        else if (r.type === 'undo') { cur.undos++; cur.undoStreak++; }
        else if (r.type === 'restart') cur.restarts++;
        else if (r.type === 'hint') { cur.hints++; if (cur.firstHintMs == null) cur.firstHintMs = Math.max(0, r.t - cur.t0); }
        else if (r.type === 'keep') cur.keeps++;
        else if (r.type === 'switch') cur.switches++;
        else if (r.type === 'level_fail') cur.fails++;
        else if (r.type === 'idle') {
          cur.idles++; cur.idleMs += d.idleMs; cur.idleMsMax = Math.max(cur.idleMsMax, d.idleMs);
          cur.stuckEnd = { t: r.t, moves: cur.moves }; // idle is sent when the pause ends
        } else if (r.type === 'body_lost') {
          cur.lost[d.reason] = (cur.lost[d.reason] ?? 0) + 1;
          if (d.wasted) cur.wasted++;
        } else if (r.type === 'level_clear') {
          cur.bodiesUsed = d.bodiesUsed ?? null;
          cur.clientStars = d.stars ?? null;
          cur.clearCounts = { hints: d.hints ?? cur.hints, undos: d.undos ?? cur.undos, restarts: d.restarts ?? cur.restarts };
          if (d.minMoves != null) cur.minMoves = d.minMoves;
          close('clear', r.t, d.durationMs);
        } else if (r.type === 'level_leave') {
          if (d.toLevel != null && d.toLevel !== cur.level) cur.nextLevelStarted = true;
          close('leave', r.t, d.durationMs);
        }
      }
    }
  }
  endSession();
  return { attempts, sessions };
}

export function quantile(xs, q) {
  if (!xs.length) return null;
  const s = [...xs].sort((a, b) => a - b);
  const pos = (s.length - 1) * q;
  const lo = Math.floor(pos);
  const hi = Math.ceil(pos);
  return Math.round(s[lo] + (s[hi] - s[lo]) * (pos - lo));
}

// Which stage of the current build an attempt belongs to. Stage numbers shift when stages are inserted or reordered,
// so match by levelKey (map hash) first, then by name (then by number when the event has neither); no match = a map this build no longer has (an "old" stage).
// Without level data (levels = []) the event's stage number is used as is.
export function stageResolver(levels = []) {
  const byKey = new Map(levels.filter((l) => l.key).map((l) => [l.key, l]));
  const byName = new Map(levels.filter((l) => l.name).map((l) => [l.name, l]));
  const cache = new Map();
  return (a) => {
    const ck = `${a.level}|${a.levelKey}|${a.levelName}|${a.chapter}`;
    if (cache.has(ck)) return cache.get(ck);
    let r;
    const byNumber = !a.levelKey && !a.levelName ? levels.find((l) => l.n === a.level) : null; // nothing else to go on
    const hit = levels.length ? (a.levelKey && byKey.get(a.levelKey)) || (a.levelName && byName.get(a.levelName)) || byNumber || null : null;
    if (!levels.length) r = { slot: `n:${a.level}`, n: a.level, name: a.levelName, key: a.levelKey, chapter: a.chapter ?? '', match: 'number' };
    else if (hit) {
      const match = a.levelKey && a.levelKey === hit.key ? 'key' : a.levelName ? 'name' : 'number';
      r = { slot: `n:${hit.n}`, n: hit.n, name: hit.name, key: hit.key, chapter: hit.chapter || a.chapter || '', match, minBodies: match === 'name' ? null : hit.minBodies ?? null };
    }
    else {
      const id = a.levelKey || a.levelName || `#${a.level}`;
      r = { slot: `old:${id}`, n: null, name: a.levelName || `#${a.level}`, key: a.levelKey, chapter: a.chapter ?? '', match: 'old' };
    }
    cache.set(ck, r);
    return r;
  };
}

// Stars of a cleared attempt: what the client sent, else plan B from the level_clear counts and the fewest bodies of
// the stage's current map (null when that is unknown, e.g. the map changed since).
export function attemptStars(a, stage) {
  if (a.outcome !== 'clear') return { stars: null, source: '' };
  if (a.clientStars) return { stars: a.clientStars, source: 'client' };
  const stars = starsFor({ bodiesUsed: a.bodiesUsed, minBodies: stage.minBodies, ...a.clearCounts });
  return { stars, source: stars ? 'server' : '' };
}

function aggregate(rows, levels, now) {
  const { attempts, sessions } = buildAttempts(rows, now);
  const resolve = stageResolver(levels);
  const by = new Map();
  const get = (st) => {
    if (!by.has(st.slot)) {
      by.set(st.slot, {
        slot: st.slot, level: st.n, old: st.n == null, name: st.name ?? '', key: st.key ?? '', chapter: st.chapter ?? '', names: new Set(),
        attempts: 0, players: new Set(), clearPlayers: new Set(), clears: 0, leaves: 0, quits: 0, open: 0,
        clearMs: [], allMs: [], clearMoves: [], undos: 0, restarts: 0, hints: 0, fails: 0, blocked: 0,
        idles: 0, idleMs: 0, wasted: 0, lost: {}, sessions: new Set(), sessionEnds: 0, playerQuits: 0, hintPlayers: new Set(),
        bestStars: new Map(), minBodies: st.minBodies ?? null,
      });
    }
    return by.get(st.slot);
  };
  for (const l of levels) get({ slot: `n:${l.n}`, n: l.n, name: l.name, key: l.key, chapter: l.chapter, minBodies: l.minBodies });

  for (const a of attempts) {
    a.stage = resolve(a);
    const s = get(a.stage);
    if (a.levelName) s.names.add(a.levelName);
    s.attempts++;
    s.players.add(a.playerId);
    s.sessions.add(a.sessionId);
    if (a.outcome === 'clear') { s.clears++; s.clearPlayers.add(a.playerId); s.clearMs.push(a.durationMs); s.clearMoves.push(a.moves); }
    else if (a.outcome === 'leave') s.leaves++;
    else if (a.outcome === 'quit') s.quits++;
    else s.open++;
    if (a.outcome !== 'open') s.allMs.push(a.durationMs);
    const { stars } = attemptStars(a, a.stage);
    if (stars) s.bestStars.set(a.playerId, Math.max(stars, s.bestStars.get(a.playerId) ?? 0));
    s.undos += a.undos; s.restarts += a.restarts; s.hints += a.hints; s.fails += a.fails; s.blocked += a.blocked;
    s.idles += a.idles; s.idleMs += a.idleMs; s.wasted += a.wasted;
    if (a.hints) s.hintPlayers.add(a.playerId);
    for (const [k, v] of Object.entries(a.lost)) s.lost[k] = (s.lost[k] ?? 0) + v;
  }
  // Drop-off: the session's last attempt was this stage and it was not cleared.
  for (const sess of sessions) {
    const last = sess.attempts.at(-1);
    if (last && last.outcome !== 'clear' && last.outcome !== 'open') get(last.stage).sessionEnds++;
  }
  // Player quit: the player's latest attempt overall is this stage, uncleared, and stale.
  const lastByPlayer = new Map();
  for (const a of attempts) {
    const p = lastByPlayer.get(a.playerId);
    if (!p || a.t0 >= p.t0) lastByPlayer.set(a.playerId, a);
  }
  for (const a of lastByPlayer.values()) {
    if (a.outcome === 'quit' || a.outcome === 'leave') get(a.stage).playerQuits++;
  }
  // Current stages in play order, then maps from older builds (most played first).
  const list = [...by.values()];
  const cur = list.filter((s) => !s.old).sort((x, y) => x.level - y.level);
  const old = list.filter((s) => s.old).sort((x, y) => y.attempts - x.attempts);
  return { stages: [...cur, ...old], attempts, sessions };
}

const rate = (a, b) => (b ? Math.round((a / b) * 1000) / 1000 : null);
const per = (a, b) => (b ? Math.round((a / b) * 100) / 100 : null);

// Per-stage funnel and struggle metrics. levels: [{ n, name, key, chapter }] from the current build (optional).
// level = stage number in the current build; null for a map the build no longer has (old = true).
export function levelStats(rows, levels = [], now = Date.now()) {
  return aggregate(rows, levels, now).stages.map((s) => ({
    level: s.level,
    name: s.name || [...s.names][0] || '',
    chapter: s.chapter,
    levelKey: s.key,
    old: s.old,
    namesSeen: [...s.names],
    attempts: s.attempts,
    players: s.players.size,
    clearPlayers: s.clearPlayers.size,
    clears: s.clears, leaves: s.leaves, quits: s.quits, open: s.open,
    clearRate: rate(s.clearPlayers.size, s.players.size),
    sessionEndRate: rate(s.sessionEnds, s.sessions.size),
    playerQuitRate: rate(s.playerQuits, s.players.size),
    playerQuits: s.playerQuits,
    clearMsMedian: quantile(s.clearMs, 0.5),
    clearMsP75: quantile(s.clearMs, 0.75),
    attemptMsMedian: quantile(s.allMs, 0.5),
    clearMovesMedian: quantile(s.clearMoves, 0.5),
    undos: s.undos, restarts: s.restarts, hints: s.hints, fails: s.fails, blocked: s.blocked,
    undosPerAttempt: per(s.undos, s.attempts), restartsPerAttempt: per(s.restarts, s.attempts),
    hintPlayerRate: rate(s.hintPlayers.size, s.players.size),
    idles: s.idles, idleMs: s.idleMs, wastedBodies: s.wasted, bodyLost: s.lost,
    minBodies: s.minBodies,
    ...starColumns(s.bestStars, s.clearPlayers.size),
  }));
}

// Players by their best stars on a stage: stars1..3, starsKnown (clear players with a star value), star3Rate of those.
function starColumns(best, clearPlayers) {
  const n = [0, 0, 0, 0];
  for (const v of best.values()) n[v]++;
  return { stars1: n[1], stars2: n[2], stars3: n[3], starsKnown: best.size, starsUnknown: clearPlayers - best.size, star3Rate: rate(n[3], best.size), star2PlusRate: rate(n[2] + n[3], best.size) };
}

// Per-chapter summary over the current build's stages (old maps left out), in play order.
// players: started any stage of the chapter; playerShare: of all players in the data. reachedLast: started its last
// stage. clearedAll: cleared every stage of it. quitPlayers: latest attempt overall is an uncleared stage here.
export function chapterStats(rows, levels = [], now = Date.now()) {
  const { stages } = aggregate(rows, levels, now);
  const all = new Set();
  for (const s of stages) for (const p of s.players) all.add(p);
  const chapters = new Map();
  for (const s of stages) {
    if (s.old) continue;
    const c = chapters.get(s.chapter) ?? { chapter: s.chapter, stages: [], players: new Set(), quitPlayers: 0 };
    c.stages.push(s);
    for (const p of s.players) c.players.add(p);
    c.quitPlayers += s.playerQuits;
    chapters.set(s.chapter, c);
  }
  return [...chapters.values()].map((c) => {
    const last = c.stages.at(-1);
    const clearedAll = [...c.players].filter((p) => c.stages.every((s) => s.clearPlayers.has(p))).length;
    return {
      chapter: c.chapter,
      firstLevel: c.stages[0].level, lastLevel: last.level, stages: c.stages.length,
      players: c.players.size, playerShare: rate(c.players.size, all.size),
      reachedLast: last.players.size, clearedAll, clearedAllRate: rate(clearedAll, c.players.size),
      quitPlayers: c.quitPlayers, quitRate: rate(c.quitPlayers, c.players.size),
      attempts: c.stages.reduce((n, s) => n + s.attempts, 0),
    };
  });
}

// Heatmap for stage n of the current build: attempts on its current map only (same levelKey, or none sent),
// wherever the stage sat in older builds. rows: all events with the dashboard filters (store.query).
export function stageHeatmap(rows, levels, n, now = Date.now()) {
  const level = levels.find((l) => l.n === n) ?? null;
  const { attempts } = buildAttempts(rows, now);
  const resolve = stageResolver(levels);
  const keep = new Set();
  let otherMaps = 0;
  for (const a of attempts) {
    if (resolve(a).n !== n) continue;
    if (level?.key && a.levelKey && a.levelKey !== level.key) { otherMaps++; continue; }
    keep.add(a.id);
  }
  return { level, otherMapAttempts: otherMaps, cells: heatmap(rows.filter((r) => r.attemptId && keep.has(r.attemptId))) };
}

// Cell counts for one level. rows: events already filtered to that level.
// rows: one level, ordered by session and seq (store.query). Idle after level_clear (the clear screen) is not being stuck.
export function heatmap(rows) {
  const cells = {};
  const cleared = new Set(); // sessions whose current attempt on this level is cleared
  const bump = (x, y, k, n = 1) => {
    if (x == null || y == null) return;
    const c = (cells[`${x},${y}`] ??= { x, y });
    c[k] = (c[k] ?? 0) + n;
  };
  for (const r of rows) {
    const d = r.data;
    if (r.type === 'level_start') cleared.delete(r.session_id);
    else if (r.type === 'level_clear') cleared.add(r.session_id);
    if (r.type === 'idle') {
      if (cleared.has(r.session_id)) continue;
      bump(r.x, r.y, 'idle'); bump(r.x, r.y, 'idleMs', d.idleMs);
    }
    else if (r.type === 'undo') bump(r.x, r.y, 'undo');
    else if (r.type === 'restart') bump(r.x, r.y, 'restart');
    else if (r.type === 'hint') bump(r.x, r.y, 'hint');
    else if (r.type === 'body_lost') { bump(r.x, r.y, `lost_${d.reason}`); if (d.wasted) bump(r.x, r.y, 'wasted'); }
    else if (r.type === 'move' && d.result === 'blocked' && d.from) bump(d.from[0], d.from[1], 'blocked');
    else if (r.type === 'move' && d.result !== 'blocked') bump(r.x, r.y, 'visit');
    else if ((r.type === 'session_end' || r.type === 'level_leave') && d.cleared !== true) bump(r.x, r.y, 'left');
  }
  return Object.values(cells);
}

// Recent players with their sessions and per-attempt outcomes (session timeline view).
export function timelines(rows, { limit = 50, now = Date.now() } = {}) {
  const { sessions } = buildAttempts(rows, now);
  const players = new Map();
  for (const s of sessions) {
    const p = players.get(s.playerId) ?? { playerId: s.playerId, lastT: 0, sessions: [] };
    p.lastT = Math.max(p.lastT, s.lastT);
    p.sessions.push({
      sessionId: s.sessionId, client: s.client, build: s.build, t0: s.t0, lastT: s.lastT, endReason: s.endReason,
      attempts: s.attempts.map((a) => ({
        level: a.level, outcome: a.outcome, t0: a.t0, durationMs: a.durationMs, moves: a.moves,
        undos: a.undos, restarts: a.restarts, hints: a.hints, fails: a.fails, idleMs: a.idleMs, via: a.via,
      })),
    });
    players.set(s.playerId, p);
  }
  return [...players.values()]
    .sort((a, b) => b.lastT - a.lastT)
    .slice(0, limit)
    .map((p) => {
      p.sessions.sort((a, b) => a.t0 - b.t0);
      const all = p.sessions.flatMap((s) => s.attempts);
      const cleared = new Set(all.filter((a) => a.outcome === 'clear').map((a) => a.level));
      const last = all.at(-1);
      return { ...p, cleared: [...cleared].sort((a, b) => a - b), lastLevel: last?.level ?? null, lastOutcome: last?.outcome ?? null };
    });
}

const CSV_ESC = (v) => {
  const s = v == null ? '' : typeof v === 'object' ? JSON.stringify(v) : String(v);
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};

export function toCsv(objs, cols = objs.length ? Object.keys(objs[0]) : []) {
  return [cols.join(','), ...objs.map((o) => cols.map((c) => CSV_ESC(o[c])).join(','))].join('\n') + '\n';
}

// Columns from the attempt's current stage: chapter, stage number now, how it was matched, and its stars.
const stageColumns = (a, st) => {
  const { stars, source } = attemptStars(a, st);
  return { chapter: st.chapter, stageNow: st.n ?? '', stageMatch: st.match, stars: stars ?? '', starsSource: source };
};

// One row per attempt; columns follow design/levels/telemetry-mapping.md section 4.1 (feat/levels).
export const ATTEMPT_COLUMNS = [
  'attemptId', 'playerId', 'sessionId', 'build', 'client', 'level', 'levelName', 'levelKey', 'attempt', 'via', 'bodies',
  'sessionFirstTime', 'startT', 'endT', 'outcome', 'durationMs', 'activeMs', 'moves', 'blocked', 'undos', 'restarts',
  'hints', 'fails', 'bodiesUsed', 'minMoves', 'idleCount', 'idleMsTotal', 'idleMsMax', 'firstHintMs', 'bodiesWasted',
  'lostFell', 'lostVanish', 'lostStatue', 'lostSwitch', 'lostBridge', 'lostSeal', 'keeps', 'switches',
  'lastStuckToClearMoves', 'lastStuckToClearMs', 'nextLevelStarted', 'chapter', 'stageNow', 'stageMatch', 'stars', 'starsSource',
];

// levels (optional): adds chapter, stageNow (stage number in the current build, '' for an old map) and
// stageMatch (key | name | old | number), see stageResolver.
export function attemptRows(rows, now = Date.now(), minMovesFor, levels = []) {
  const resolve = stageResolver(levels);
  return buildAttempts(rows, now, minMovesFor).attempts.map((a) => ({
    attemptId: a.id, playerId: a.playerId, sessionId: a.sessionId, build: a.build, client: a.client,
    level: a.level, levelName: a.levelName, levelKey: a.levelKey, attempt: a.attempt, via: a.via, bodies: a.bodies,
    sessionFirstTime: a.sessionFirstTime, startT: a.t0, endT: a.tEnd, outcome: a.outcome,
    durationMs: a.durationMs, activeMs: Math.max(0, a.durationMs - a.idleMs), moves: a.moves, blocked: a.blocked,
    undos: a.undos, restarts: a.restarts, hints: a.hints, fails: a.fails, bodiesUsed: a.bodiesUsed, minMoves: a.minMoves,
    idleCount: a.idles, idleMsTotal: a.idleMs, idleMsMax: a.idleMsMax, firstHintMs: a.firstHintMs, bodiesWasted: a.wasted,
    lostFell: a.lost.fell ?? 0, lostVanish: a.lost.vanish ?? 0, lostStatue: a.lost.statue ?? 0, lostSwitch: a.lost.switch ?? 0,
    lostBridge: a.lost.bridge ?? 0, lostSeal: a.lost.seal ?? 0, keeps: a.keeps, switches: a.switches,
    lastStuckToClearMoves: a.lastStuckToClearMoves, lastStuckToClearMs: a.lastStuckToClearMs, nextLevelStarted: a.nextLevelStarted,
    ...stageColumns(a, resolve(a)),
  }));
}

// Raw events, one JSON object per line, with the envelope fields and attemptId (null outside a stage).
export function eventsJsonl(rows, now = Date.now()) {
  buildAttempts(rows, now);
  return rows.map((r) => JSON.stringify({
    playerId: r.player_id, sessionId: r.session_id, client: r.client, build: r.build, seq: r.seq, t: r.t,
    receivedAt: r.received_at, type: r.type, attemptId: r.attemptId, ...r.data,
  })).join('\n') + (rows.length ? '\n' : '');
}
