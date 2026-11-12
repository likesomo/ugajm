// Checks every stage and tutorial board (T1.., docs/tutorial.md; a board must need all its bodies): solvable within its body limit, fewest bodies, and that the solution replays to a win via dispatch.
// Usage: node tools/solve.mjs   → summary on stdout, full result in reports/seven-steps-solve.json
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseLevel, newState, dispatch, minBodies, solve } from '../src/core.js';
import { LEVELS, TUTORIALS } from '../src/levels.js';

const arrows = { up: '↑', down: '↓', left: '←', right: '→', keep: ' K ', switch: ' X ' };
const boards = [...LEVELS.map((def, k) => ({ def, idx: k + 1 })), ...TUTORIALS.map((t, k) => ({ def: t.level, idx: `T${k + 1}`, tutorial: true }))];
const rows = boards.map(({ def, idx, tutorial }) => {
  const level = parseLevel(def);
  const best = minBodies(level);
  let replayWin = false;
  if (best) {
    let s = newState(level);
    for (const a of best.path) s = dispatch(s, a === 'keep' || a === 'switch' ? { type: a } : { type: 'move', dir: a }).state;
    replayWin = s.status === 'win';
  }
  const needsSwitch = !!best && !solve(level, level.bodies, { noSwitch: true });
  return {
    idx, name: def.name, limit: level.bodies, needsSwitch,
    ok: !!best && replayWin && (!tutorial || best.bodies === level.bodies),
    minBodies: best?.bodies ?? null, moves: best?.moves ?? null,
    path: best ? best.path.map((d) => arrows[d]).join('') : null,
  };
});

for (const r of rows) {
  const mark = r.ok ? 'OK  ' : 'FAIL';
  const sw = r.needsSwitch ? 'SWITCH필수' : '          ';
  console.log(`${mark} ${String(r.idx).padStart(2)}. ${r.name.padEnd(12)} 몸 ${r.minBodies ?? '-'}/${r.limit}  ${sw}  ${r.path ?? ''}`);
}
const failed = rows.filter((r) => !r.ok).length;
console.log(failed ? `\n${failed}개 보드 실패` : `\n전체 ${LEVELS.length}개 스테이지 + 튜토리얼 ${TUTORIALS.length}개 풀이 가능`);

const out = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'reports', 'seven-steps-solve.json');
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify({ rows }, null, 2));
process.exit(failed ? 1 : 0);
