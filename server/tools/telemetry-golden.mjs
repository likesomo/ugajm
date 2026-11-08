// Golden telemetry for the C# port: runs command strings through core.js + telemetry.js Tracker and writes the
// time-independent events to server/fixtures/telemetry.golden.json. Unity's TelemetryTracker must produce the same.
//   node server/tools/telemetry-golden.mjs [traces.golden.json]
// Input: the core agent's traces (unity/Assets/_Project/Tests/Golden/traces.golden.json, from the working tree or
// origin/feat/core) plus the manual scenarios below.
// Command letters: U D L R move, K keep, X switch, Z undo, * restart, H hint, > go to the next stage via the stage
// selector (level_leave + level_start via "select"). The clock never advances, so no idle events.
// Output per trace: events from session_start on, with t, durationMs, idleMs, sessionMs removed; idle/session_end excluded.
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseLevel, newState, dispatch } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';
import { Tracker, levelKey, SCHEMA_VERSION } from '../../prototypes/seven-steps/src/telemetry.js';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '../..');
const TRACE_PATH = 'unity/Assets/_Project/Tests/Golden/traces.golden.json';
const OUT = join(root, 'server/fixtures/telemetry.golden.json');
const DROP = new Set(['t', 'durationMs', 'idleMs', 'sessionMs']);
const SKIP = new Set(['idle', 'session_end']);
const MOVE = { U: 'up', D: 'down', L: 'left', R: 'right' };
const CMD = { K: { type: 'keep' }, X: { type: 'switch' }, Z: { type: 'undo' }, '*': { type: 'restart' } };

// Hand-written scenarios for flows the rule traces do not cover.
const MANUAL = [
  { id: 'manual-hint-then-clear', level: 1, commands: 'RRRHRRRR' },
  { id: 'manual-select-leave', level: 1, commands: 'RRZR>RRR' },
  { id: 'manual-clear-again', level: 1, commands: 'RRRRRRR*RRL' },
  { id: 'manual-blocked-wall-edge', level: 2, commands: 'UUUULLLL' },
  { id: 'manual-fail-undo-restart', level: 5, commands: 'RRRRRRRRRZZ*H' },
  { id: 'manual-gate-solution', level: 6, commands: 'UDRRRRR' },
  { id: 'manual-plate-door-crack', level: 9, commands: 'RLRLRRRRLRRRRRKRRRRDDR' },
  { id: 'manual-keep-switch', level: 4, commands: 'RRRRRRRXRRRR' },
];

function loadTraces(arg) {
  if (arg) return { source: arg, json: JSON.parse(readFileSync(arg, 'utf8')) };
  const local = join(root, TRACE_PATH);
  if (existsSync(local)) return { source: TRACE_PATH, json: JSON.parse(readFileSync(local, 'utf8')) };
  const text = execFileSync('git', ['show', `origin/feat/core:${TRACE_PATH}`], { cwd: root, encoding: 'utf8', maxBuffer: 64 << 20 });
  return { source: `origin/feat/core:${TRACE_PATH}`, json: JSON.parse(text) };
}

const keyToStage = new Map(LEVELS.map((d, i) => [levelKey(parseLevel(d)), i + 1]));

function run({ id, def, level, commands }) {
  const events = [];
  const tr = new Tracker({ emit: (e) => events.push(e), now: () => 0, attempts: new Map() });
  let n = level;
  let d = def;
  let state = newState(parseLevel(d));
  tr.sessionStart({ firstTime: true, cleared: 0 });
  tr.levelStart(state, n, d.name, 'boot');
  for (const ch of commands) {
    if (ch === 'H') { tr.hint(state); continue; }
    if (ch === '>') {
      tr.leave(state, n + 1);
      n += 1;
      d = LEVELS[n - 1];
      state = newState(parseLevel(d));
      tr.levelStart(state, n, d.name, 'select');
      continue;
    }
    const cmd = MOVE[ch] ? { type: 'move', dir: MOVE[ch] } : CMD[ch];
    if (!cmd) throw new Error(`${id}: unknown command letter ${ch}`);
    const before = state;
    const r = dispatch(state, cmd);
    tr.command(before, cmd, r);
    state = r.state;
  }
  const clean = events.filter((e) => !SKIP.has(e.type)).map((e) => Object.fromEntries(Object.entries(e).filter(([k]) => !DROP.has(k))));
  return { id, level, levelName: def.name, levelKey: levelKey(parseLevel(def)), via: 'boot', commands, map: def.map, bodies: def.bodies, events: clean };
}

const { source, json } = loadTraces(process.argv[2]);
const traces = [];
for (const t of json.traces) {
  const key = levelKey(parseLevel(t.level));
  const stage = keyToStage.get(key) ?? (LEVELS.findIndex((d) => d.name === t.level.name) + 1 || 1);
  traces.push(run({ id: t.id, def: t.level, level: stage, commands: t.commands }));
}
for (const m of MANUAL) traces.push(run({ ...m, def: LEVELS[m.level - 1] }));

const out = {
  generator: 'server/tools/telemetry-golden.mjs',
  source: `${source} + manual scenarios`,
  reference: 'prototypes/seven-steps/src/telemetry.js',
  schema: SCHEMA_VERSION,
  notes: 'Clock fixed at 0: no idle events. Removed fields: t, durationMs, idleMs, sessionMs. Excluded types: idle, session_end. ' +
    'Commands: U D L R K X, Z undo, * restart, H hint, > next stage via selector. session_start uses firstTime:true, cleared:0; attempts start at 1.',
  traces,
};
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, JSON.stringify(out, null, 1) + '\n');
console.log(`${traces.length} traces, ${traces.reduce((a, t) => a + t.events.length, 0)} events → server/fixtures/telemetry.golden.json`);
