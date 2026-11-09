// Level maps for the heatmap and solver move counts, read from the web prototype (the rules' reference).
// LEVELS_JS / PROTO_DIR override the paths.
import { pathToFileURL, fileURLToPath } from 'node:url';
import { join, dirname } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
export const PROTO_DIR = process.env.PROTO_DIR ?? join(here, '../../prototypes/seven-steps');

export async function loadLevels(protoDir = PROTO_DIR, levelsPath = process.env.LEVELS_JS ?? join(protoDir, 'src/levels.js')) {
  try {
    const { LEVELS } = await import(pathToFileURL(levelsPath).href);
    const core = await import(pathToFileURL(join(protoDir, 'src/core.js')).href);
    const { levelKey } = await import(pathToFileURL(join(protoDir, 'src/telemetry.js')).href);
    return LEVELS.map((d, i) => {
      const parsed = core.parseLevel(d);
      const sol = core.solve(parsed);
      return {
        n: i + 1, name: d.name, chapter: d.chapter ?? '', bodies: parsed.bodies, w: parsed.w, h: parsed.h,
        map: d.map.map((r) => r.padEnd(parsed.w, '#')), key: levelKey(parsed), minMoves: sol ? sol.path.length : null,
      };
    });
  } catch (err) {
    console.warn(`[levels] could not load levels from ${levelsPath}: ${err.message}`);
    return [];
  }
}

// levelKey first (survives reordering), then stage number.
export const minMovesLookup = (levels) => (key, n) => {
  const hit = key ? levels.find((l) => l.key === key) : levels.find((l) => l.n === n);
  return hit?.minMoves ?? null;
};
