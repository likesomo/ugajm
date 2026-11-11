// Gimmick audit (QA-1, design/levels/qa-1.md 3): the role of each gimmick on a stage (gimmick-roles.mjs:
// used / blocks / used+blocks / dead), by bodies (what stars count) and by bodies then moves, for each group and each cell.
// With --attempts it replays each tester's clearing run (inputs after the last "!", undos applied) and lists the
// gimmick kinds that run touched (stood on, slid over, passed, or left a statue on).
// Usage (repo root): node prototypes/seven-steps/tools/gimmick-audit.mjs [stage ...] [--attempts reports/qa/attempts.jsonl] [--json out.json]
import { readFileSync, writeFileSync } from 'node:fs';
import { parseLevel, newState, step, choose } from '../src/core.js';
import { LEVELS } from '../src/levels.js';
import { roles, groupOf } from './gimmick-roles.mjs';

const args = process.argv.slice(2);
const opt = (k) => { const i = args.indexOf(k); return i >= 0 ? args.splice(i, 2)[1] : null; };
const attemptsPath = opt('--attempts');
const jsonOut = opt('--json');
const stages = args.length ? args.map(Number) : LEVELS.map((_, i) => i + 1);
const fmt = (m) => (m ? `${m.bodies}몸 ${m.moves}수` : '해 없음');

function touched(def, moves) {
  const level = parseLevel(def);
  let s = newState(level);
  const kinds = new Set();
  for (const c of moves) {
    const dir = { U: 'up', D: 'down', L: 'left', R: 'right' }[c];
    if (dir) {
      const r = step(s, dir);
      if (r.state !== s) for (const t of r.state.trail.slice(s.trail.length)) { const g = groupOf(level.tiles[t.i]); if (g) kinds.add(g); }
      s = r.state;
    } else if (c === 'K' || c === 'X') s = choose(s, c === 'K' ? 'keep' : 'switch').state;
  }
  return { kinds: [...kinds].sort(), win: s.status === 'win' };
}

const clears = {};
if (attemptsPath) {
  for (const line of readFileSync(attemptsPath, 'utf8').split('\n')) {
    if (!line.trim()) continue;
    const a = JSON.parse(line);
    if (a.outcome !== 'clear') continue;
    const inputs = String(a.inputs ?? '');
    const moves = [];
    for (const c of inputs.slice(inputs.lastIndexOf('!') + 1)) { if (c === 'Z') moves.pop(); else if ('UDLRKX'.includes(c)) moves.push(c); }
    (clears[a.level] ??= []).push({ tester: a.tester, moves: moves.join(''), stars: a.stars, bodiesUsed: a.bodiesUsed });
  }
}

const out = [];
for (const n of stages) {
  const def = LEVELS[n - 1];
  const byB = roles(def, { byBodies: true });
  const byM = roles(def);
  const cells = roles(def, { byBodies: true, cells: true });
  const testers = (clears[n] ?? []).map((c) => ({ ...c, ...touched(def, c.moves) }));
  const groups = byB.units.map((u, i) => ({ name: u.name, inert: u.inert, floor: u.floor, byBodies: u.role, byMoves: byM.units[i].role }));
  out.push({ stage: n, name: def.name, base: byB.base, groups, cells: cells.units.map((u) => ({ name: u.name, role: u.role })), testers });
  console.log(`\n${n}. ${def.name}  기준 ${fmt(byB.base)}`);
  for (const g of groups) console.log(`  ${g.name.padEnd(11)} 효과 없음→ ${fmt(g.inert).padEnd(10)} 바닥→ ${fmt(g.floor).padEnd(10)} 몸 기준 ${g.byBodies.padEnd(12)} 수까지 ${g.byMoves}`);
  console.log(`  칸별(몸 기준): ${cells.units.map((u) => `${u.name} ${u.role}`).join(' · ')}`);
  for (const t of testers) console.log(`  [${t.tester}] ${t.bodiesUsed}몸 ★${t.stars} ${t.moves.length}수 ${t.win ? '' : '(재생 실패) '}밟음: ${t.kinds.join(', ') || '-'}`);
}
if (jsonOut) writeFileSync(jsonOut, JSON.stringify(out, null, 2) + '\n');
