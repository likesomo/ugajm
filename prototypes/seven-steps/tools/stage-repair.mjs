// Stage repair search (QA-1): small edits to an existing stage until its gimmicks have the roles the stage is meant
// to teach (gimmick-roles.mjs), judged by bodies (what ★3 counts). Edits: swap a wall and a floor cell, open a wall,
// or move one gimmick cell onto a floor cell; S and E stay put. Candidates are ranked by fewest changed cells.
// A candidate passes when every group in --need is "used", every group in --block "blocks", no group is "dead"
// (unless --allow-dead), and the fewest bodies are within --bodies.
// Usage: node prototypes/seven-steps/tools/stage-repair.mjs <stage> --need "ice,mud" [--block "Q door"] --bodies 2[-3]
//        [--edits 3] [--tries 4000] [--seed 1] [--top 3] [--allow-dead] [--json out.json]
import { writeFileSync } from 'node:fs';
import { LEVELS } from '../src/levels.js';
import { roles, groupOf } from './gimmick-roles.mjs';

const argv = process.argv.slice(2);
const arg = (k, d) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : d; };
const n = Number(argv[0]);
const def0 = LEVELS[n - 1];
const list = (s) => (s ? s.split(',').map((x) => x.trim()).filter(Boolean) : []);
const NEED = list(arg('need', '')), BLOCK = list(arg('block', ''));
const [BLO, BHI = BLO] = arg('bodies', '1-3').split('-').map(Number);
const EDITS = Number(arg('edits', 3)), TRIES = Number(arg('tries', 4000)), TOP = Number(arg('top', 3));
const ALLOW_DEAD = argv.includes('--allow-dead');
let seed = Number(arg('seed', 1));
const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
const pick = (a) => a[Math.floor(rnd() * a.length)];

function judge(def) {
  const r = roles(def, { byBodies: true });
  if (!r.base || r.base.bodies < BLO || r.base.bodies > BHI) return null;
  const role = Object.fromEntries(r.units.map((u) => [u.name, u.role]));
  for (const g of NEED) if (!String(role[g]).startsWith('used')) return null;
  for (const g of BLOCK) if (!String(role[g]).endsWith('blocks')) return null;
  if (!ALLOW_DEAD && Object.values(role).includes('dead')) return null;
  return { base: r.base, role };
}

function mutate(map) {
  const g = map.map((r) => [...r]);
  const cells = [];
  for (let y = 1; y < g.length - 1; y++) for (let x = 1; x < g[0].length - 1; x++) cells.push([x, y]);
  const of = (f) => cells.filter(([x, y]) => f(g[y][x]));
  const k = 1 + Math.floor(rnd() * EDITS);
  for (let e = 0; e < k; e++) {
    const r = rnd();
    if (r < 0.4) { const a = pick(of((t) => t === '#')), b = pick(of((t) => t === '.')); if (a && b) { g[a[1]][a[0]] = '.'; g[b[1]][b[0]] = '#'; } }
    else if (r < 0.6) { const a = pick(of((t) => t === '#')); if (a) g[a[1]][a[0]] = '.'; }
    else { const a = pick(of((t) => groupOf(t))), b = pick(of((t) => t === '.')); if (a && b) { g[b[1]][b[0]] = g[a[1]][a[0]]; g[a[1]][a[0]] = '.'; } }
  }
  return g.map((r) => r.join(''));
}

const now = judge(def0);
console.log(`${n}. ${def0.name}  지금: ${now ? 'pass' : 'fail'}  need=[${NEED}] block=[${BLOCK}] bodies ${BLO}-${BHI}`);
const found = new Map();
for (let t = 0; t < TRIES; t++) {
  const map = mutate(def0.map);
  const key = map.join('/');
  if (found.has(key)) continue;
  const r = judge({ ...def0, map });
  if (r) found.set(key, { map, ...r, diff: map.reduce((s, row, y) => s + [...row].filter((c, x) => c !== def0.map[y][x]).length, 0) });
}
const best = [...found.values()].sort((a, b) => a.diff - b.diff || b.base.moves - a.base.moves).slice(0, TOP);
console.log(`${found.size} candidates`);
for (const c of best) {
  console.log(`\n  바뀐 칸 ${c.diff}  ${c.base.bodies}몸 ${c.base.moves}수  ${Object.entries(c.role).map(([k, v]) => `${k}:${v}`).join(' ')}`);
  c.map.forEach((row, y) => console.log('    ' + row + '   ' + def0.map[y]));
}
const out = arg('json', null);
if (out) writeFileSync(out, JSON.stringify({ stage: n, name: def0.name, need: NEED, block: BLOCK, candidates: best }, null, 2) + '\n');
