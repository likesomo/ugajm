// Golden telemetry for the C# port: runs command strings through core.js + telemetry.js Tracker and writes the
// time-independent events to server/fixtures/telemetry.golden.json. Unity's TelemetryTracker must produce the same.
//   node server/tools/telemetry-golden.mjs [traces.golden.json]
//   node server/tools/telemetry-golden.mjs --check [traces.golden.json]   (compare only, ignores CRLF; exit 1 if stale)
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

// Hand-written scenarios for flows the rule traces do not cover. A scenario names its stage (found by name in
// levels.js, so inserting or reordering stages does not break it) or carries its own map (def), so it does not depend
// on the stage list at all. expect: what the events must contain, "type" or "type:value" (value = result, effect,
// via, reason or blockedBy, or a true flag such as crumbled). Generation fails when a scenario stops showing what it
// is for (a stage renamed or its map changed), instead of silently testing something else.
const MANUAL = [
  // First entry of the app run picked on the map/list: via "select" (docs/telemetry.md via rule, 2026-11-10).
  // Other traces model a direct launch (boot). The first level_start's via is the trace's top-level "via".
  { id: 'manual-first-entry-select', stage: '일곱 걸음', via: 'select', commands: 'RRRRRRR', expect: ['level_start:select', 'level_clear'] },
  { id: 'manual-hint-then-clear', stage: '일곱 걸음', commands: 'RRRHRRRR', expect: ['hint', 'seventh_step:win', 'level_clear'] },
  { id: 'manual-select-leave', stage: '일곱 걸음', commands: 'RRZR>RRR', expect: ['undo', 'level_leave', 'level_start:select'] },
  { id: 'manual-clear-again', stage: '일곱 걸음', commands: 'RRRRRRR*RRL', expect: ['level_clear', 'level_start:again'] },
  { id: 'manual-blocked-wall-edge', stage: '늦게 도착하기', commands: 'UUUULLLL', expect: ['move:blocked', 'move:wall'] },
  { id: 'manual-fail-undo-restart', stage: '몸으로 놓는 다리', commands: 'RRRRRRRRRZZ*H', expect: ['move:fell', 'body_lost:fell', 'level_fail', 'undo', 'restart', 'hint'] },
  { id: 'manual-gate-solution', stage: '숫자 문', commands: 'UDRRRRR', expect: ['move:gate', 'seventh_step:win'] },
  {
    id: 'manual-plate-door-crack', commands: 'DLLRRRRRKRDDDDDD', expect: ['move:wall', 'move:crumbled', 'keep', 'body_lost:statue', 'move:door', 'seventh_step:win'],
    def: { name: '발판과 문 (골든 전용)', bodies: 3, map: ['##########', '#x.S..a..#', '####A#####', '####.#####', '####.#####', '####.#####', '####.#####', '####E#####', '##########'] },
  },
  {
    id: 'manual-keep-switch', commands: 'RRRRRRRXLLLLLLLK', expect: ['seventh_step:choose', 'switch', 'body_lost:switch', 'keep', 'body_lost:statue'],
    def: { name: 'KEEP과 SWITCH (골든 전용)', bodies: 3, map: ['##########', '#S.......#', '##########'] },
  },
];

const shows = (events, spec) => {
  const [type, value] = spec.split(':');
  return events.some((e) => e.type === type && (value == null || [e.result, e.effect, e.via, e.reason, e.blockedBy].includes(value) || e[value] === true));
};

function loadTraces(arg) {
  if (arg) return { source: arg, json: JSON.parse(readFileSync(arg, 'utf8')) };
  const local = join(root, TRACE_PATH);
  if (existsSync(local)) return { source: TRACE_PATH, json: JSON.parse(readFileSync(local, 'utf8')) };
  const text = execFileSync('git', ['show', `origin/feat/core:${TRACE_PATH}`], { cwd: root, encoding: 'utf8', maxBuffer: 64 << 20 });
  return { source: `origin/feat/core:${TRACE_PATH}`, json: JSON.parse(text) };
}

const keyToStage = new Map(LEVELS.map((d, i) => [levelKey(parseLevel(d)), i + 1]));

function run({ id, def, level, commands, via = 'boot' }) {
  const events = [];
  const tr = new Tracker({ emit: (e) => events.push(e), now: () => 0, attempts: new Map() });
  let n = level;
  let d = def;
  let state = newState(parseLevel(d));
  tr.sessionStart({ firstTime: true, cleared: 0 });
  tr.levelStart(state, n, d.name, via);
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
  const parsed = parseLevel(def);
  return { id, level, levelName: def.name, levelKey: levelKey(parsed), via, commands, map: def.map, bodies: def.bodies, statueCount: parsed.statueCount ?? 0, events: clean };
}

const CHECK = process.argv.includes('--check');
const { source, json } = loadTraces(process.argv.slice(2).find((a) => a !== '--check'));
const traces = [];
for (const t of json.traces) {
  // Tutorial boards send no level_* telemetry (docs/tutorial.md 2.6).
  if (t.id.startsWith('tutorial:')) continue;
  const key = levelKey(parseLevel(t.level));
  const stage = keyToStage.get(key) ?? (LEVELS.findIndex((d) => d.name === t.level.name) + 1 || 1);
  traces.push(run({ id: t.id, def: t.level, level: stage, commands: t.commands }));
}
for (const m of MANUAL) {
  let def = m.def;
  let level = 1; // own map: not a stage of the build
  if (m.stage) {
    level = LEVELS.findIndex((d) => d.name === m.stage) + 1;
    if (!level) throw new Error(`${m.id}: no stage named "${m.stage}" in levels.js. Pick the stage that now shows ${m.expect.join(', ')}.`);
    if (m.commands.includes('>') && !LEVELS[level]) throw new Error(`${m.id}: "${m.stage}" is the last stage, ">" needs a next one`);
    def = LEVELS[level - 1];
  }
  const t = run({ id: m.id, def, level, commands: m.commands, via: m.via });
  const missing = m.expect.filter((x) => !shows(t.events, x));
  if (missing.length) throw new Error(`${m.id} (${def.name}): no ${missing.join(', ')}. The stage changed; fix the commands or pick another stage.`);
  traces.push(t);
}

const out = {
  generator: 'server/tools/telemetry-golden.mjs',
  source: `${source} + manual scenarios`,
  reference: 'prototypes/seven-steps/src/telemetry.js',
  schema: SCHEMA_VERSION,
  notes: 'Clock fixed at 0: no idle events. Removed fields: t, durationMs, idleMs, sessionMs. Excluded types: idle, session_end. ' +
    'Commands: U D L R K X, Z undo, * restart, H hint, > next stage via selector. session_start uses firstTime:true, cleared:0; attempts start at 1. The first level_start uses the trace via (boot = direct launch, select = picked on the map/list).',
  traces,
};
const text = JSON.stringify(out, null, 1) + '\n';
if (CHECK) {
  // Windows checkouts may hold the file with CRLF (core.autocrlf): compare the content, never rewrite it.
  const current = existsSync(OUT) ? readFileSync(OUT, 'utf8').replace(/\r\n/g, '\n') : '';
  if (current !== text) {
    console.error('server/fixtures/telemetry.golden.json is stale: run node server/tools/telemetry-golden.mjs and commit the result');
    process.exit(1);
  }
  console.log('golden up to date');
  process.exit(0);
}
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, text);
console.log(`${traces.length} traces, ${traces.reduce((a, t) => a + t.events.length, 0)} events → server/fixtures/telemetry.golden.json`);
