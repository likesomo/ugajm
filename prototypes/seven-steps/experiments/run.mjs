// Solves the sketch stages of one experiment with that experiment's rule copy.
// Usage: node prototypes/seven-steps/experiments/run.mjs <experiment-dir-name>
import { pathToFileURL } from 'node:url';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const name = process.argv[2];
const dir = join(dirname(fileURLToPath(import.meta.url)), name);
const core = await import(pathToFileURL(join(dir, 'core.js')));
const { STAGES } = await import(pathToFileURL(join(dir, 'stages.mjs')));
const arrows = { up: '↑', down: '↓', left: '←', right: '→', keep: ' K ', switch: ' X ' };

let failed = 0;
for (const [i, def] of STAGES.entries()) {
  const level = core.parseLevel(def);
  const best = core.minBodies(level);
  const needsSwitch = !!best && !core.solve(level, level.bodies, { noSwitch: true });
  // Without the gimmick tiles (replaced by floor): is the gimmick actually needed? null = unsolvable = needed.
  // Gimmick check: replace the gimmick tiles with floor, solve that, then replay its answer on the real stage.
  // The gimmick is needed when the plain stage needs fewer bodies, or its answer fails on the real stage.
  let plain, plainWorks, plainNoSwitch;
  if (def.gimmickTiles) {
    const plainLevel = core.parseLevel({ ...def, map: def.map.map((r) => r.replace(new RegExp(`[${def.gimmickTiles}]`, 'g'), '.')) });
    plain = core.minBodies(plainLevel);
    plainNoSwitch = !!core.solve(plainLevel, plainLevel.bodies, { noSwitch: true });
    if (plain) {
      let st = core.newState(level);
      for (const a of plain.path) st = core.dispatch(st, a === 'keep' || a === 'switch' ? { type: a } : { type: 'move', dir: a }).state;
      plainWorks = st.status === 'win';
    }
  }
  if (!best) failed++;
  console.log(`${best ? 'OK  ' : 'FAIL'} ${i + 1}. ${def.name}  몸 ${best?.bodies ?? '-'}/${level.bodies}  ${needsSwitch ? 'SWITCH필수' : ''}`);
  if (best) console.log(`     ${best.path.map((d) => arrows[d]).join('')}  (${best.moves} 행동)`);
  if (def.gimmickTiles) {
    console.log(`     기믹 칸을 바닥으로 바꾸면: ${plain ? `몸 ${plain.bodies}로 풀림 (${plain.moves} 행동), 그 해가 원래 판에서 ${plainWorks ? '통함' : '안 통함'}` : '못 풂'}${plainNoSwitch && needsSwitch ? ', SWITCH 없이도 풀림' : ''}`);
    // Needed when removing it changes the answer either way: unsolvable, other body count or length, or its answer fails here.
    const needed = !plain || plain.bodies !== best?.bodies || plain.moves !== best?.moves || !plainWorks || (needsSwitch && plainNoSwitch);
    console.log(`     → 기믹 ${needed ? '필요' : '불필요 (장식)'}`);
  }
}
process.exit(failed ? 1 : 0);
