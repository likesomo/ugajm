// Stage candidate search: random small layouts → solver filter → gimmick-necessity check → ranked list.
// The designer picks and polishes; this only finds raw candidates that obey the hard rules.
//
// Usage (repo root):
//   export PATH="/c/Program Files/nodejs:$PATH"
//   node prototypes/seven-steps/tools/stage-search.mjs --recipe gate-ice --tries 20000 --min 2 --max 3 --top 8 [--seed 7]
//   node prototypes/seven-steps/tools/stage-search.mjs --list            (recipes)
//   options: --core <rules.js> (experiment rules), --switch yes|no|any (SWITCH required?), --moves 10-30,
//            --json reports/stage-search/<recipe>.json (default), --budget 3 (body cap, triplets)
//
// Hard filters for every candidate:
//   - solvable with the budget (default 3 bodies = the triplets), minimum bodies within --min..--max
//   - every gimmick class on the map is necessary: replacing that class with floor makes the stage
//     unsolvable, or changes its minimum bodies / shortest length, or the plain answer fails on the real map
//   - no unreachable floor islands (every floor cell is reachable by walking, ignoring doors and rules)
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const argv = process.argv.slice(2);
const arg = (n, d) => { const i = argv.indexOf(`--${n}`); return i >= 0 ? argv[i + 1] : d; };
const core = await import(pathToFileURL(resolve(ROOT, arg('core', 'prototypes/seven-steps/src/core.js'))));
const { parseLevel, newState, step, choose, actions, solve, minBodies, dispatch } = core;

// Gimmick classes: the tiles that make up one gimmick (replaced together by floor in the necessity check).
const CLASSES = {
  pit: 'o', ice: '~', gate: '1234567', plate: 'aAbB', lever: 'LRr', cracked: 'x', qdoor: 'Q', mud: 'm',
};

// Recipes: room size and what to scatter. Counts are [min,max]. `walls` = extra wall density inside the room.
const RECIPES = {
  'walk':        { w: [5, 8], h: [3, 5], walls: 0.25, place: {} },
  'plate':       { w: [6, 9], h: [3, 5], walls: 0.3, place: { a: [1, 1], A: [1, 1] } },
  'plate2':      { w: [6, 9], h: [4, 6], walls: 0.3, place: { a: [1, 1], A: [1, 1], b: [1, 1], B: [1, 1] } },
  'switch':      { w: [6, 10], h: [3, 6], walls: 0.35, place: {} },
  'pit':         { w: [5, 9], h: [3, 5], walls: 0.3, place: { o: [1, 3] } },
  'pit-plate':   { w: [6, 9], h: [4, 6], walls: 0.3, place: { o: [1, 2], a: [1, 1], A: [1, 1] } },
  'gate':        { w: [5, 9], h: [3, 5], walls: 0.3, place: { gate: [1, 3] } },
  'gate-plate':  { w: [6, 9], h: [3, 6], walls: 0.3, place: { gate: [1, 2], a: [1, 1], A: [1, 1] } },
  'gate-pit':    { w: [6, 9], h: [3, 6], walls: 0.3, place: { gate: [1, 2], o: [1, 2] } },
  'ice':         { w: [6, 9], h: [4, 6], walls: 0.2, place: { iceRun: [1, 2] } },
  'gate-ice':    { w: [6, 9], h: [4, 6], walls: 0.25, place: { iceRun: [1, 2], gate: [1, 2] } },
  'ice-statue':  { w: [7, 10], h: [4, 7], walls: 0.25, place: { iceRun: [1, 2] } },
  'lever':       { w: [6, 10], h: [4, 6], walls: 0.3, place: { L: [1, 1], R: [1, 2], r: [0, 1] } },
  'lever-ice':   { w: [7, 10], h: [4, 7], walls: 0.25, place: { L: [1, 1], R: [0, 1], r: [1, 2], iceRun: [1, 1] } },
  'lever-gate':  { w: [6, 10], h: [4, 6], walls: 0.3, place: { L: [1, 1], R: [1, 2], gate: [1, 2] } },
  'cracked':     { w: [6, 9], h: [3, 6], walls: 0.3, place: { x: [1, 3] } },
  'cracked-pit': { w: [6, 9], h: [4, 6], walls: 0.3, place: { x: [1, 2], o: [1, 2] } },
  'cracked-plate': { w: [6, 9], h: [4, 6], walls: 0.3, place: { x: [1, 2], a: [1, 1], A: [1, 1] } },
  'qdoor':       { w: [6, 10], h: [3, 6], walls: 0.3, place: { Q: [1, 2] }, statueCount: [0, 2] },
  'qdoor-plate': { w: [6, 10], h: [4, 6], walls: 0.3, place: { Q: [1, 1], a: [1, 1], A: [1, 1] }, statueCount: [0, 2] },
  'qdoor-pit':   { w: [6, 10], h: [4, 6], walls: 0.3, place: { Q: [1, 1], o: [1, 2] }, statueCount: [0, 2] },
  'qdoor-cracked': { w: [6, 10], h: [4, 6], walls: 0.3, place: { Q: [1, 1], x: [1, 2] }, statueCount: [0, 2] },
  'mud':         { w: [5, 9], h: [3, 6], walls: 0.3, place: { m: [1, 3] } },
  'mud-gate':    { w: [6, 9], h: [3, 6], walls: 0.3, place: { m: [1, 2], gate: [1, 2] } },
  'mud-ice':     { w: [6, 9], h: [4, 6], walls: 0.25, place: { m: [1, 2], iceRun: [1, 1] } },
  'mix':         { w: [7, 10], h: [5, 7], walls: 0.25, place: { o: [0, 1], a: [0, 1], A: [0, 1], gate: [0, 1], iceRun: [0, 1], L: [0, 1], R: [0, 1], x: [0, 1] } },
};

if (argv.includes('--list')) { for (const [k, r] of Object.entries(RECIPES)) console.log(k.padEnd(14), JSON.stringify(r.place)); process.exit(0); }

const recipeName = arg('recipe', 'gate-ice');
const R = RECIPES[recipeName];
if (!R) throw new Error(`unknown recipe ${recipeName} (--list)`);
const TRIES = Number(arg('tries', 5000));
const BUDGET = Number(arg('budget', 3));
const MIN = Number(arg('min', 1));
const MAX = Number(arg('max', BUDGET));
const TOP = Number(arg('top', 8));
const SW = arg('switch', 'any');
const [MOVES_LO, MOVES_HI] = (arg('moves', '7-60')).split('-').map(Number);
let seed = Number(arg('seed', Date.now() % 1e9));
const SEED0 = seed;
const rnd = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648; };
const ri = (lo, hi) => lo + Math.floor(rnd() * (hi - lo + 1));
const pick = (a) => a[Math.floor(rnd() * a.length)];

// ---- layout ----
function layout() {
  const w = ri(R.w[0], R.w[1]), h = ri(R.h[0], R.h[1]);
  const g = Array.from({ length: h + 2 }, (_, y) => Array.from({ length: w + 2 }, (_, x) => (x === 0 || y === 0 || x === w + 1 || y === h + 1 ? '#' : '.')));
  for (let y = 1; y <= h; y++) for (let x = 1; x <= w; x++) if (rnd() < R.walls) g[y][x] = '#';
  const floor = () => { const c = []; for (let y = 1; y <= h; y++) for (let x = 1; x <= w; x++) if (g[y][x] === '.') c.push([x, y]); return c; };
  const put = (ch) => { const f = floor(); if (!f.length) return false; const [x, y] = pick(f); g[y][x] = ch; return true; };
  for (const [k, [lo, hi]] of Object.entries(R.place)) {
    const n = ri(lo, hi);
    for (let i = 0; i < n; i++) {
      if (k === 'gate') put(String(ri(2, 6)));
      else if (k === 'iceRun') {
        const f = floor(); if (!f.length) continue;
        let [x, y] = pick(f); const [dx, dy] = pick([[1, 0], [0, 1]]); const len = ri(2, 5);
        for (let j = 0; j < len && g[y]?.[x] === '.'; j++, x += dx, y += dy) g[y][x] = '~';
      } else put(k);
    }
  }
  if (!put('S') || !put('E')) return null;
  return { map: g.map((r) => r.join('')), statueCount: R.statueCount ? ri(...R.statueCount) : undefined };
}

// Every non-wall cell reachable from S ignoring rules (no dead decoration rooms).
function connected(map) {
  const h = map.length, w = map[0].length; const cells = [];
  let s = -1;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { if (map[y][x] !== '#') cells.push(y * w + x); if (map[y][x] === 'S') s = y * w + x; }
  const seen = new Set([s]); const q = [s];
  while (q.length) { const i = q.pop(); for (const d of [1, -1, w, -w]) { const j = i + d; const ch = map[Math.floor(j / w)]?.[j % w]; if (ch && ch !== '#' && !seen.has(j)) { seen.add(j); q.push(j); } } }
  return seen.size === cells.length;
}

// Canonical form: trim all-wall border rows/cols beyond one.
function trim(map) {
  let rows = map.slice();
  const wallRow = (r) => /^#+$/.test(r);
  while (rows.length > 3 && wallRow(rows[0]) && wallRow(rows[1])) rows.shift();
  while (rows.length > 3 && wallRow(rows.at(-1)) && wallRow(rows.at(-2))) rows.pop();
  const colWall = (c) => rows.every((r) => r[c] === '#');
  while (rows[0].length > 3 && colWall(0) && colWall(1)) rows = rows.map((r) => r.slice(1));
  while (rows[0].length > 3 && colWall(rows[0].length - 1) && colWall(rows[0].length - 2)) rows = rows.map((r) => r.slice(0, -1));
  return rows;
}

const keyOf = (s) => `${s.start}|${s.pos}|${s.used}|${s.bodies}|${s.statues}|${s.bridges}|${s.broken}|${s.status}|${s.flipped ? 1 : 0}`;
const apply = (s, a) => (a === 'keep' || a === 'switch' ? choose(s, a) : step(s, a));
// States seen before the shortest solution depth (difficulty proxy, same idea as level-metrics "search").
function searchSize(level, bodies) {
  let frontier = [newState({ ...level, bodies })]; const seen = new Set([keyOf(frontier[0])]);
  for (let d = 0; d < 80 && frontier.length; d++) {
    const nf = [];
    for (const s of frontier) for (const a of actions(s)) {
      const { state } = apply(s, a);
      if (state === s || state.status === 'lose') continue;
      if (state.status === 'win') return seen.size;
      const k = keyOf(state); if (seen.has(k)) continue; seen.add(k); nf.push(state);
      if (seen.size > 200000) return seen.size;
    }
    frontier = nf;
  }
  return seen.size;
}

function replay(level, path) {
  let st = newState(level);
  for (const a of path) st = dispatch(st, a === 'keep' || a === 'switch' ? { type: a } : { type: 'move', dir: a }).state;
  return st.status === 'win';
}

// Necessity of one gimmick class (see header).
export function classNeeded(def, chars, best) {
  const plainDef = { ...def, map: def.map.map((r) => r.replace(new RegExp(`[${chars}]`, 'g'), '.')) };
  const plain = minBodies(parseLevel(plainDef));
  if (!plain) return true;
  if (plain.bodies !== best.bodies || plain.moves !== best.moves) return true;
  return !replay(parseLevel(def), plain.path);
}

function evaluate(def) {
  const level = parseLevel(def);
  const best = minBodies(level);
  if (!best || best.bodies < MIN || best.bodies > MAX) return null;
  if (best.moves < MOVES_LO || best.moves > MOVES_HI) return null;
  const switchNeeded = !solve(level, level.bodies, { noSwitch: true });
  if (SW === 'yes' && !switchNeeded) return null;
  if (SW === 'no' && switchNeeded) return null;
  const present = Object.entries(CLASSES).filter(([, chars]) => def.map.some((r) => new RegExp(`[${chars}]`).test(r)));
  for (const [, chars] of present) if (!classNeeded(def, chars, best)) return null;
  const search = searchSize(level, best.bodies);
  const keeps = best.path.filter((a) => a === 'keep').length, switches = best.path.filter((a) => a === 'switch').length;
  // Rank: prefer more search (thinking), then fewer wall cells (readable), then compact boards.
  const size = def.map.length * def.map[0].length;
  const score = Math.log10(search + 1) * 2 + best.bodies * 0.5 - size / 60;
  return { map: def.map, statueCount: def.statueCount, bodies: best.bodies, moves: best.moves, search, switchNeeded, keeps, switches,
    gimmicks: present.map(([k]) => k), path: best.path.map((a) => ({ up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' })[a]).join(''), score: Math.round(score * 100) / 100 };
}

const found = new Map();
const t0 = Date.now();
for (let t = 0; t < TRIES; t++) {
  const raw = layout(); if (!raw) continue;
  if (!connected(raw.map)) continue;
  const map = trim(raw.map);
  const key = map.join('/') + (raw.statueCount ?? '');
  if (found.has(key)) continue;
  const def = { name: 'cand', bodies: BUDGET, map, ...(raw.statueCount !== undefined ? { statueCount: raw.statueCount } : {}) };
  let r = null;
  try { r = evaluate(def); } catch { r = null; }
  if (r) found.set(key, r);
}
const list = [...found.values()].sort((a, b) => b.score - a.score);
const out = resolve(ROOT, arg('json', `reports/stage-search/${recipeName}.json`));
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify({ recipe: recipeName, seed: SEED0, tries: TRIES, budget: BUDGET, found: list.length, list }, null, 1));
console.log(`recipe ${recipeName}  seed ${SEED0}  tries ${TRIES}  →  ${list.length} candidates (${((Date.now() - t0) / 1000).toFixed(1)}s)  ${out}`);
const byBodies = [1, 2, 3].map((b) => list.filter((c) => c.bodies === b).length);
console.log(`  by min bodies 1/2/3: ${byBodies.join(' / ')}   SWITCH required: ${list.filter((c) => c.switchNeeded).length}`);
for (const c of list.slice(0, TOP)) {
  console.log(`\n  score ${c.score}  bodies ${c.bodies}  moves ${c.moves}  search ${c.search}${c.switchNeeded ? '  SWITCH필수' : ''}${c.statueCount !== undefined ? `  statueCount ${c.statueCount}` : ''}  [${c.gimmicks.join(',')}]`);
  for (const row of c.map) console.log('    ' + row);
  console.log('    ' + c.path);
}
