// For each stage: can it be cleared using ALL its bodies (bodiesUsed == level.bodies, the 3-star rule, design/levels/stars.md 0)?
// BFS over dispatch from newState(level); a win counts only when the last body is the one that wins (state.bodies == 1).
// Writes design/levels/all-brothers-check.json: [{ stage, bodies, solvable, minSteps|null }] - no solution paths.
// Usage: node unity/Tools/all-brothers-check.mjs [limit]   (state limit per stage, default 3000000)
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseLevel, newState, dispatch, actions, keyOf } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const limit = Number(process.argv[2] ?? 3000000);
const cmd = (a) => (a === 'keep' || a === 'switch' ? { type: a } : { type: 'move', dir: a });

// Shortest action list (moves + 'keep'/'switch') that wins on the last body; null if none; throws if the limit was hit
// first. The path stays in memory (tests); the JSON only gets its length.
export function solveAllBodies(level, max = limit) {
  const start = newState(level);
  const seen = new Set([keyOf(start)]);
  let frontier = [{ s: start, path: null }];
  for (let depth = 1; frontier.length; depth++) {
    const next = [];
    for (const { s, path } of frontier) {
      for (const a of actions(s)) {
        const { state } = dispatch(s, cmd(a));
        if (state === s || state.status === 'lose') continue;
        const p = { a, up: path };
        if (state.status === 'win') { if (state.bodies === 1) return unwind(p); continue; }
        const k = keyOf(state);
        if (seen.has(k)) continue;
        seen.add(k);
        if (seen.size > max) throw new Error(`state limit ${max} hit at depth ${depth}`);
        next.push({ s: { ...state, prev: null }, path: p });
      }
    }
    frontier = next;
  }
  return null;
}
const unwind = (p) => { const out = []; for (; p; p = p.up) out.push(p.a); return out.reverse(); };

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const rows = LEVELS.map((def, i) => {
    const level = parseLevel(def);
    const minSteps = solveAllBodies(level)?.length ?? null;
    return { stage: i + 1, bodies: level.bodies, solvable: minSteps !== null, minSteps };
  });
  const out = join(root, 'design', 'levels', 'all-brothers-check.json');
  writeFileSync(out, '[\n' + rows.map((r) => '  ' + JSON.stringify(r)).join(',\n') + '\n]\n');
  const bad = rows.filter((r) => !r.solvable).map((r) => r.stage);
  console.log(`all-brothers: ${rows.length - bad.length}/${rows.length} solvable; unsolvable: ${bad.join(',') || 'none'}`);
}
