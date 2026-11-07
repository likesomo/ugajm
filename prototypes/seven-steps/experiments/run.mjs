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
  const plain = def.gimmickTiles
    ? core.minBodies(core.parseLevel({ ...def, map: def.map.map((r) => r.replace(new RegExp(`[${def.gimmickTiles}]`, 'g'), '.')) }))
    : undefined;
  if (!best) failed++;
  console.log(`${best ? 'OK  ' : 'FAIL'} ${i + 1}. ${def.name}  몸 ${best?.bodies ?? '-'}/${level.bodies}  ${needsSwitch ? 'SWITCH필수' : ''}`);
  if (best) console.log(`     ${best.path.map((d) => arrows[d]).join('')}  (${best.moves} 행동)`);
  if (plain !== undefined) console.log(`     기믹 칸을 바닥으로 바꾸면: ${plain ? `몸 ${plain.bodies}로 풀림 (${plain.moves} 행동)` : '못 풂'}`);
}
process.exit(failed ? 1 : 0);
