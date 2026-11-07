// Exports the web prototype's stages (prototypes/seven-steps/src/levels.js) to unity/Assets/_Project/Data/levels.json,
// together with what the JS solver finds for each stage. Unity's LevelVerifier checks the C# port gives the same answers.
// Usage (repo root): node unity/Tools/export-levels.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { STEPS, parseLevel, minBodies, solve } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

const LETTER = { up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' };
const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, '..', 'Assets', '_Project', 'Data', 'levels.json');

const levels = LEVELS.map((def) => {
  const level = parseLevel(def);
  const best = minBodies(level);
  if (!best) throw new Error(`stage "${def.name}" has no solution`);
  return {
    name: def.name,
    hint: def.hint ?? '',
    bodies: level.bodies,
    statueCount: level.statueCount,
    map: def.map,
    expected: {
      minBodies: best.bodies,
      moves: best.moves,
      needsSwitch: !solve(level, level.bodies, { noSwitch: true }),
      path: best.path.map((a) => LETTER[a]).join(''),
    },
  };
});

writeFileSync(out, JSON.stringify({ steps: STEPS, levels }, null, 2) + '\n');
console.log(`[export-levels] ${levels.length} stages -> ${out}`);
