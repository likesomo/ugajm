// Golden data for the C# rules (SevenSteps.Core). Runs the web prototype's core.js and writes what it produces
// to unity/Assets/_Project/Tests/Golden/*.json. EditMode tests replay the same inputs in C# and compare.
// Usage (repo root): node unity/Tools/gen-golden.mjs        (deterministic: same core.js + levels.js -> same files)
//
// Files (all JsonUtility-friendly: no dictionaries, no nested arrays):
//   levels.golden.json   every stage of levels.js: parsed grid, plates, solver answers per body count
//   traces.golden.json   command sequences with the full state and events after every command
//                        (stage solutions, seeded random walks with undo/restart, hand-made edge cases)
//
// Command letters in traces: U D L R move, K keep, X switch, Z undo, * restart.
// Events: `stepIdx` is set only on 'step' events (core.js puts it nowhere else); other events carry 0.
// `at` is -1 for events without a cell (lose, undo, restart).
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { STEPS, parseLevel, newState, dispatch, solve, minBodies } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, '..', 'Assets', '_Project', 'Tests', 'Golden');
const LETTER = { up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' };
const CMD = {
  U: { type: 'move', dir: 'up' }, D: { type: 'move', dir: 'down' },
  L: { type: 'move', dir: 'left' }, R: { type: 'move', dir: 'right' },
  K: { type: 'keep' }, X: { type: 'switch' }, Z: { type: 'undo' }, '*': { type: 'restart' },
};
const letters = (path) => (path ? path.map((a) => LETTER[a]).join('') : '');

// ---- levels ---------------------------------------------------------------------------------------------

function levelRecord(def) {
  const level = parseLevel(def);
  const solves = [];
  for (let b = 1; b <= level.bodies; b++) {
    for (const noSwitch of [false, true]) {
      const r = solve(level, b, { noSwitch });
      solves.push({ bodies: b, noSwitch, solvable: !!r, path: letters(r?.path), bodiesUsed: r?.bodiesUsed ?? 0 });
    }
  }
  const best = minBodies(level);
  return {
    name: level.name,
    hint: level.hint,
    bodies: level.bodies,
    statueCount: level.statueCount,
    map: def.map,
    w: level.w,
    h: level.h,
    start: level.start,
    tiles: level.tiles.join(''),
    plates: Object.keys(level.plates).sort().map((k) => ({ plate: k, cells: level.plates[k] })),
    minBodies: best?.bodies ?? 0,
    minMoves: best?.moves ?? 0,
    minPath: letters(best?.path),
    needsSwitch: !!best && !solve(level, level.bodies, { noSwitch: true }),
    solves,
  };
}

// ---- traces ---------------------------------------------------------------------------------------------

function snapshot(s) {
  let undoDepth = 0;
  for (let p = s.prev; p; p = p.prev) undoDepth++;
  return {
    start: s.start, pos: s.pos, used: s.used, bodies: s.bodies, status: s.status,
    statues: s.statues, bridges: s.bridges, broken: s.broken, flipped: s.flipped,
    trail: s.trail.map((f) => ({ i: f.i, n: f.n })),
    undoDepth,
  };
}

function eventRecord(e) {
  return { type: e.type, at: e.at ?? -1, stepIdx: e.type === 'step' ? e.stepIdx : 0 };
}

// Replays `commands` from a fresh state. `bodies` overrides the stage's body count (0 = stage default).
function trace(id, kind, def, commands, bodies = 0) {
  const level = parseLevel(bodies ? { ...def, bodies } : def);
  let s = newState(level);
  const initial = snapshot(s);
  const steps = [];
  for (const c of commands) {
    if (!CMD[c]) throw new Error(`trace ${id}: bad command '${c}'`);
    const r = dispatch(s, CMD[c]);
    steps.push({ cmd: c, changed: r.state !== s, events: r.events.map(eventRecord), state: snapshot(r.state) });
    s = r.state;
  }
  return { id, kind, level: inlineLevel(def, bodies), commands, initial, steps };
}

function inlineLevel(def, bodies) {
  return { name: def.name, hint: def.hint ?? '', bodies: bodies || def.bodies || STEPS, statueCount: def.statueCount ?? 0, map: def.map };
}

// Small seeded PRNG (mulberry32) so random walks are the same on every run.
function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Mostly moves; sometimes keep/switch (also when not choosing, to check they are ignored), undo, restart.
function randomCommands(seed, length) {
  const r = rng(seed);
  const out = [];
  for (let k = 0; k < length; k++) {
    const x = r();
    if (x < 0.72) out.push('UDLR'[Math.floor(r() * 4)]);
    else if (x < 0.82) out.push('K');
    else if (x < 0.90) out.push('X');
    else if (x < 0.98) out.push('Z');
    else out.push('*');
  }
  return out.join('');
}

// Hand-made stages for rules the 12 stages may not reach. Legend: core.js parseLevel().
// `expect`: event types (in order, other events may sit between) the trace must produce, so a broken case fails here.
// A body walks exactly 7 steps, so on a plain grid it can only end on cells at odd Manhattan distance from its start.
const EDGE = [
  { id: 'edge-bump-wall-and-border', map: ['S.E'], bodies: 1, cmds: 'ULUDRRLLLL', expect: 'bump step bump' },
  { id: 'edge-early-exit-is-floor', map: ['#####', '#SE.#', '#####'], bodies: 2, cmds: 'RRLRLRLL', expect: 'step win' },
  { id: 'edge-lose-last-body', map: ['#####', '#So.#', '#####'], bodies: 2, cmds: 'RRZZR', expect: 'fall respawn fall lose undo undo fall' },
  { id: 'edge-last-body-stone', map: ['#########', '#S......#', '#########'], bodies: 1, cmds: 'RRRRRRRL', expect: 'bump stone lose' },
  // 7 plain steps can never come back to the start; the ice slide covers 2 cells in one step.
  { id: 'edge-vanish-at-start', map: ['#####', '#S~.#', '#...#', '#####'], bodies: 2, cmds: 'DURDLLU' + 'DURDLLU', expect: 'vanish respawn vanish lose' },
  { id: 'edge-keep-then-switch', map: ['##########', '#S.......#', '##########'], bodies: 3,
    cmds: 'RRRRRRRK' + 'RRRRRRL' + 'X' + 'RRRRRRR', expect: 'choose stone respawn choose switch respawn bump' },
  { id: 'edge-keep-switch-outside-choose', map: ['#####', '#S..#', '#####'], bodies: 2, cmds: 'KXRKXZZZ', expect: 'step undo' },
  { id: 'edge-choose-blocks-moves', map: ['##########', '#S.......#', '##########'], bodies: 2, cmds: 'RRRRRRRUDLRZRK', expect: 'choose undo choose stone' },
  { id: 'edge-pit-fall-and-bridge', map: ['#########', '#S..o...E', '#########'], bodies: 3,
    cmds: 'RRR' + 'RRLRLRR' + 'RRRRRRR', expect: 'fall respawn bridge respawn win' },
  { id: 'edge-ice-slide', map: ['#######', '#S~~~.#', '#.~~~##', '#######'], bodies: 3, cmds: 'RLDRULRDUL', expect: 'slide step choose' },
  { id: 'edge-slide-into-pit', map: ['#######', '#S~~o.#', '#######'], bodies: 2, cmds: 'RR', expect: 'slide fall respawn' },
  { id: 'edge-crumble-then-slide', map: ['#######', '#Sx~~.#', '#######'], bodies: 2, cmds: 'RRL', expect: 'step crumble slide step' },
  { id: 'edge-ice-stops-at-gate', map: ['#########', '#S~~3...#', '#########'], bodies: 2, cmds: 'RRRLRRRR', expect: 'step bump' },
  { id: 'edge-ice-through-gate', map: ['#########', '#S~~1...#', '#########'], bodies: 2, cmds: 'RRRRRR', expect: 'step' },
  { id: 'edge-gates', map: ['###########', '#S1.3...7.#', '###########'], bodies: 3,
    cmds: 'RLR' + 'ZZ' + 'RRRRRRR' + 'K' + 'R', expect: 'step step bump undo undo step choose stone respawn' },
  { id: 'edge-cracked-crumble', map: ['#######', '#Sx...#', '#######'], bodies: 3, cmds: 'RRLLRLR' + 'RR', expect: 'crumble fall respawn' },
  { id: 'edge-cracked-seal', map: ['##########', '#S....x.E', '##########'], bodies: 3,
    cmds: 'RRRRLRR' + 'RRRRRRR' + 'Z' + 'R', expect: 'seal respawn win undo win' },
  { id: 'edge-door-plate', map: ['#########', '#S..a...#', '#.....AE#', '#########'], bodies: 3,
    cmds: 'DRRRRRR' + '*' + 'RRRLRLR' + 'K' + 'DRRRRRR', expect: 'bump restart stone doorOpen respawn win' },
  { id: 'edge-two-doors-one-plate', map: ['#######', '#S..a.#', '#A##A##', '#######'], bodies: 2, cmds: 'RRRLRLR' + 'K', expect: 'stone doorOpen doorOpen respawn' },
  { id: 'edge-last-body-opens-door', map: ['######', '#S..a#', '####A#', '######'], bodies: 1, cmds: 'RRRLRLR', expect: 'stone doorOpen lose' },
  // The second statue on another 'a' plate opens nothing new: no doorOpen event.
  { id: 'edge-second-plate-no-door-event', map: ['#########', '#a..S..a#', '####A####', '####E####'], bodies: 3,
    cmds: 'LLLRLRL' + 'K' + 'RRRLRLR' + 'K', expect: 'stone doorOpen respawn choose stone respawn' },
  { id: 'edge-two-plates-one-door', map: ['#########', '#a..S..a#', '####A####', '####E####'], bodies: 3,
    cmds: 'D' + 'LLLRLRL' + 'K' + 'D' + 'ZZZ' + '*' + 'RRRLRLR' + 'K' + 'DD',
    expect: 'bump stone doorOpen respawn step undo restart stone doorOpen respawn step step' },
  { id: 'edge-statue-blocks', map: ['##########', '#S.......#', '##########'], bodies: 3, cmds: 'RRRRRRRK' + 'RRRRRRR', expect: 'stone respawn bump' },
  { id: 'edge-undo-chain-and-restart', map: ['##########', '#S.......#', '##########'], bodies: 3,
    cmds: 'RRZZZ' + 'RRRRRRRK' + 'ZZ' + 'RX' + '*' + 'ZZR', expect: 'undo undo stone undo undo choose switch restart step' },
  { id: 'edge-ragged-map', map: ['#####', '#S.', '#..E#', '##'], bodies: 3, cmds: 'RRRDDLURDR', expect: 'bump win' },
  // Statue-count door Q: open while exactly statueCount statues stand. Bridges and seals do not count.
  { id: 'edge-count-door-zero-closes', map: ['##########', '#S.......#', '#Q########', '##########'], bodies: 3, statueCount: 0,
    cmds: 'RRRRRRRK' + 'D', expect: 'stone doorClose respawn bump' },
  { id: 'edge-count-door-one', map: ['##########', '#S.......#', '#Q########', '##########'], bodies: 4, statueCount: 1,
    cmds: 'D' + 'RRRRRRRK' + 'DZ' + 'RRRRRRLK' + 'D', expect: 'bump stone doorOpen respawn step undo stone doorClose respawn bump' },
  { id: 'edge-count-door-bridge-not-counted', map: ['#########', '#S..o...#', '#Q#######', '#########'], bodies: 3, statueCount: 1,
    cmds: 'RRLRLRR' + 'D', expect: 'bridge respawn bump' },
  { id: 'edge-count-door-seal-not-counted', map: ['##########', '#S....x..#', '#Q########', '##########'], bodies: 3, statueCount: 1,
    cmds: 'RRRRLRR' + 'D', expect: 'seal respawn bump' },
  { id: 'edge-count-door-and-plate-door', map: ['#########', '#S..a...#', '#A#####Q#', '#########'], bodies: 3, statueCount: 1,
    cmds: 'RRRLRLR' + 'K', expect: 'stone doorOpen doorOpen respawn' },
  { id: 'edge-count-door-last-body', map: ['##########', '#S.......#', '#Q########', '##########'], bodies: 1, statueCount: 0,
    cmds: 'RRRRRRR', expect: 'stone doorClose lose' },
  // SWITCH onto the door, then a statue elsewhere closes it under the next body's feet; stepping off still works.
  { id: 'edge-count-door-closes-under-start', map: ['##########', '#S......Q#', '#........#', '##########'], bodies: 4, statueCount: 0,
    cmds: 'RRRRRRR' + 'X' + 'DLLLLLL' + 'K' + 'L' + 'R', expect: 'choose switch respawn choose stone doorClose respawn step bump' },
  // KEEP while standing on an open Q: the statue stands on the door cell and that door closes.
  { id: 'edge-count-door-keep-on-door', map: ['##########', '#S......Q#', '##########'], bodies: 3, statueCount: 0,
    cmds: 'RRRRRRRK' + 'RRRRRR' + 'R', expect: 'choose stone doorClose respawn bump' },
  // Lever L: a 7th step ending on it flips every flip wall (R raised/blocks, r lowered/open).
  { id: 'edge-lever-pull-then-keep', map: ['##########', '#S......L#', '#R########', '##########'], bodies: 3,
    cmds: 'D' + 'RRRRRRR' + 'K' + 'RRRRRRR' + 'Z' + 'Z' + 'Z' + 'Z' + 'Z' + 'Z' + 'D', expect: 'bump flip choose stone respawn bump undo step' },
  { id: 'edge-lever-steps-1-to-6-pass', map: ['##########', '#S..L....#', '#R########', '##########'], bodies: 3,
    cmds: 'RRRRRRL' + 'K' + 'D' + 'RRRLRLR' + 'K' + 'D', expect: 'choose stone respawn bump flip choose stone respawn step' },
  // SWITCH onto the lever, then a 7th step back on it (the start) vanishes and does not pull it.
  { id: 'edge-lever-on-start-vanishes', map: ['############', '#S......L~.#', '#R.........#', '############'], bodies: 3,
    cmds: 'RRRRRRR' + 'X' + 'DURDLLU', expect: 'flip choose switch respawn vanish respawn' },
  { id: 'edge-lever-two-levers', map: ['##########', '#S......L#', '#R.....L.#', '##########'], bodies: 3,
    cmds: 'RRRRRRR' + 'K' + 'DRRRRRR' + 'K' + 'D', expect: 'flip choose stone respawn flip choose stone respawn bump' },
  // A wall rises under a statue: both block, nothing else changes.
  { id: 'edge-lever-raises-under-statue', map: ['##########', '#S......L#', '#..r.....#', '##########'], bodies: 3,
    cmds: 'DRRLRLR' + 'K' + 'RRRRRRR' + 'K', expect: 'choose stone respawn flip choose stone' },
  { id: 'edge-lever-last-body', map: ['##########', '#S......L#', '##########'], bodies: 1, cmds: 'RRRRRRR', expect: 'flip stone lose' },
  { id: 'edge-lever-undo', map: ['##########', '#S......L#', '#R########', '##########'], bodies: 3,
    cmds: 'RRRRRRR' + 'Z' + 'R' + 'K' + 'ZZ' + 'R' + 'X' + '*' + 'D', expect: 'flip choose undo flip choose stone undo undo flip choose switch respawn restart bump' },
  { id: 'edge-win-ends-input', map: ['#########', '#S......E', '#########'], bodies: 3, cmds: 'RRRRRRR' + 'RLKXZR', expect: 'win undo win' },
];

function checkExpect(t, expect) {
  const got = t.steps.flatMap((s) => s.events.map((e) => e.type));
  let k = 0;
  for (const want of expect.split(' ')) {
    while (k < got.length && got[k] !== want) k++;
    if (k++ >= got.length) throw new Error(`${t.id}: expected events "${expect}" in order, got "${got.join(' ')}"`);
  }
}

// Statue-count door sketch stages (design/levels/gimmick-backlog.md 1, experiments/statue-door/stages.mjs @ feat/levels b72e999).
// Kept here until they land in levels.js, so the rule has whole-stage coverage now.
const SKETCH = [
  { name: '하나만 남겨라', statueCount: 1, bodies: 3, map: ['########', '#S.....#', '#.######', '#..Q..E#', '########'] },
  { name: '다리는 세지 않는다', statueCount: 1, bodies: 4, map: ['##########', '#S..o...a#', '#.########', '#..A.QE###', '##########'] },
  { name: '남기지 말고 옮겨라', statueCount: 1, bodies: 4, map: ['##########', '#a......S#', '#######A##', '#.E..Q...#', '##########'] },
];

// Lever sketch stages (design/levels/gimmick-backlog.md 2, experiments/lever/stages.mjs @ feat/levels 9b45886).
const LEVER_SKETCH = [
  { name: '내리는 벽', bodies: 3, map: ['#########', '#S....L.#', '#.#######', '#.R...E##', '#########'] },
  { name: '열면 닫힌다', bodies: 3, map: ['##########', '#S..r..L.#', '#.##R#####', '#...R..E##', '##########'] },
  { name: '올려서 멈추기', bodies: 3, map: ['##########', '#S....L..#', '#.########', '#.########', '#.~~~~r..#', '#####E####', '##########'] },
];

const traces = [];
LEVELS.forEach((def, idx) => {
  const best = minBodies(parseLevel(def));
  traces.push(trace(`stage-${idx + 1}-solution`, 'solution', def, letters(best.path)));
  for (const seed of [1, 2, 3]) {
    traces.push(trace(`stage-${idx + 1}-random-${seed}`, 'random', def, randomCommands(1000 * (idx + 1) + seed, 120)));
  }
});
SKETCH.forEach((def, idx) => {
  const best = minBodies(parseLevel(def));
  if (!best) throw new Error(`sketch "${def.name}" has no solution`);
  traces.push(trace(`sketch-count-door-${idx + 1}-solution`, 'solution', def, letters(best.path)));
  for (const seed of [1, 2]) {
    traces.push(trace(`sketch-count-door-${idx + 1}-random-${seed}`, 'random', def, randomCommands(9000 + 10 * idx + seed, 120)));
  }
});
LEVER_SKETCH.forEach((def, idx) => {
  const best = minBodies(parseLevel(def));
  if (!best) throw new Error(`sketch "${def.name}" has no solution`);
  traces.push(trace(`sketch-lever-${idx + 1}-solution`, 'solution', def, letters(best.path)));
  for (const seed of [1, 2]) {
    traces.push(trace(`sketch-lever-${idx + 1}-random-${seed}`, 'random', def, randomCommands(9500 + 10 * idx + seed, 120)));
  }
});
for (const e of EDGE) {
  const t = trace(e.id, 'edge', { name: e.id, map: e.map, bodies: e.bodies, statueCount: e.statueCount }, e.cmds);
  checkExpect(t, e.expect);
  traces.push(t);
}

// Together the traces must produce every event type.
const seenEvents = new Set(traces.flatMap((t) => t.steps.flatMap((s) => s.events.map((e) => e.type))));
const allEvents = ['bump', 'step', 'crumble', 'bridge', 'fall', 'win', 'seal', 'vanish', 'choose', 'stone', 'switch', 'respawn', 'lose', 'undo', 'restart', 'slide', 'doorOpen', 'doorClose', 'flip'];
const missing = allEvents.filter((t) => !seenEvents.has(t));
if (missing.length) throw new Error(`golden traces never produce: ${missing.join(', ')}`);

// ---- write ----------------------------------------------------------------------------------------------

mkdirSync(outDir, { recursive: true });
const meta = { generator: 'unity/Tools/gen-golden.mjs', source: 'prototypes/seven-steps/src/core.js', steps: STEPS };
// Readable diffs: one line per level, per trace header, per command.
const json = (v) => JSON.stringify(v);
const header = Object.entries(meta).map(([k, v]) => `  ${json(k)}: ${json(v)},`).join('\n');
const write = (file, key, lines) => {
  writeFileSync(join(outDir, file), `{\n${header}\n  ${json(key)}: [\n${lines.join(',\n')}\n  ]\n}\n`);
  console.log(`[gen-golden] ${file}`);
};
write('levels.golden.json', 'levels', LEVELS.map((def) => `    ${json(levelRecord(def))}`));
write('traces.golden.json', 'traces', traces.map(({ steps, ...head }) =>
  `    ${json(head).slice(0, -1)},\n     "steps": [\n${steps.map((st) => `      ${json(st)}`).join(',\n')}\n    ]}`));
const stepCount = traces.reduce((n, t) => n + t.steps.length, 0);
console.log(`[gen-golden] ${LEVELS.length} stages, ${traces.length} traces, ${stepCount} commands -> ${outDir}`);
