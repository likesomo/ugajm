// Batch and event validation for schema v1 (docs/telemetry.md). Pure functions, no I/O.
export const SCHEMA_VERSION = 1;
export const MAX_EVENTS = 100;
export const MAX_BYTES = 64 * 1024;
export const CLIENTS = ['web-proto', 'unity-webgl', 'unity-desktop'];

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
// Tile names (docs/telemetry.md 2): floor exit pit ice cracked bridge plate door gate wall start edge statue mud lever
// flip_up flip_down. New mechanics add names, so any short lowercase name is accepted rather than a fixed list.
const tile = (v) => typeof v === 'string' && /^[a-z_]{1,20}$/.test(v);

const int = (min = 0, max = 1e12) => (v) => Number.isInteger(v) && v >= min && v <= max;
const bool = (v) => typeof v === 'boolean';
const str = (max = 80) => (v) => typeof v === 'string' && v.length <= max;
const oneOf = (...xs) => (v) => xs.includes(v);
const xy = (v) => v === null || (Array.isArray(v) && v.length === 2 && v.every((n) => Number.isInteger(n) && n >= -1 && n < 256));

const LEVEL = { level: int(1, 999) };
const AT = { ...LEVEL, body: int(1, 99), at: xy };
const WHERE = { ...AT, step: int(0, 7) };

// type -> [required, optional]
export const EVENTS = {
  session_start: [{}, { firstTime: bool, cleared: int(0, 999), resumed: bool }],
  level_start: [{ ...LEVEL, attempt: int(1, 1e6) }, { levelName: str(80), chapter: str(40), levelKey: (v) => typeof v === 'string' && /^[0-9a-f]{8}$/.test(v), via: oneOf('boot', 'url', 'next', 'select', 'again'), bodies: int(1, 99) }],
  move: [{
    ...LEVEL, body: int(1, 99), step: int(1, 7), dir: oneOf('up', 'down', 'left', 'right'), from: xy, to: xy,
    result: oneOf('moved', 'blocked', 'slid', 'fell', 'bridged', 'door', 'gate'),
  }, { blockedBy: tile, crumbled: bool }],
  seventh_step: [{ ...AT, tile, effect: oneOf('win', 'bridge', 'seal', 'vanish', 'choose', 'statue') }, { flipped: bool }],
  keep: [AT, {}],
  switch: [AT, {}],
  undo: [WHERE, { status: oneOf('play', 'choose', 'lose', 'win') }],
  restart: [WHERE, { moves: int() }],
  hint: [WHERE, {}],
  body_lost: [{ ...AT, reason: oneOf('fell', 'vanish', 'statue', 'switch', 'bridge', 'seal') }, { wasted: bool }],
  level_fail: [{ ...LEVEL, at: xy }, { moves: int(), bodiesUsed: int(0, 99) }],
  level_clear: [{ ...LEVEL, durationMs: int(), moves: int() }, {
    undos: int(), restarts: int(), hints: int(), fails: int(), bodiesUsed: int(0, 99), minMoves: int(),
  }],
  level_leave: [{ ...LEVEL, durationMs: int() }, {
    toLevel: int(1, 999), moves: int(), undos: int(), restarts: int(), hints: int(), fails: int(),
    body: int(1, 99), step: int(0, 7), at: xy,
  }],
  idle: [{ ...LEVEL, idleMs: int(1) }, {
    body: int(1, 99), step: int(0, 7), at: xy, status: oneOf('play', 'choose', 'lose', 'win'), bodiesLeft: int(0, 99), statues: int(0, 999),
  }],
  session_end: [{ reason: oneOf('hidden', 'unload') }, {
    level: int(1, 999), cleared: bool, body: int(1, 99), step: int(0, 7), at: xy, sessionMs: int(),
  }],
};

// Validates the envelope. Returns an error string or null.
export function checkEnvelope(b) {
  if (!b || typeof b !== 'object' || Array.isArray(b)) return 'body must be a JSON object';
  if (b.v !== SCHEMA_VERSION) return `unsupported schema version ${JSON.stringify(b.v)}`;
  if (!UUID.test(b.playerId ?? '')) return 'playerId must be a UUID';
  if (!UUID.test(b.sessionId ?? '')) return 'sessionId must be a UUID';
  if (!CLIENTS.includes(b.client)) return `client must be one of ${CLIENTS.join(', ')}`;
  if (typeof b.build !== 'string' || !b.build.length || b.build.length > 40) return 'build must be a 1-40 char string';
  if (!Array.isArray(b.events) || !b.events.length) return 'events must be a non-empty array';
  if (b.events.length > MAX_EVENTS) return `at most ${MAX_EVENTS} events per batch`;
  return null;
}

// Validates one event. Returns an error string or null.
export function checkEvent(e) {
  if (!e || typeof e !== 'object' || Array.isArray(e)) return 'event must be an object';
  if (!int(0, 1e9)(e.seq)) return 'seq must be an integer >= 0';
  if (!int(1e12, 1e14)(e.t)) return 't must be epoch milliseconds';
  const spec = EVENTS[e.type];
  if (!spec) return `unknown type ${JSON.stringify(String(e.type).slice(0, 40))}`;
  const [req, opt] = spec;
  for (const [k, ok] of Object.entries(req)) {
    if (!(k in e)) return `${e.type}.${k} is required`;
    if (!ok(e[k])) return `${e.type}.${k} is invalid`;
  }
  for (const [k, ok] of Object.entries(opt)) {
    if (k in e && e[k] !== undefined && !ok(e[k])) return `${e.type}.${k} is invalid`;
  }
  if (JSON.stringify(e).length > 2048) return 'event larger than 2KB';
  return null;
}
