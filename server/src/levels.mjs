// Level maps for the heatmap and solver move counts, read from the web prototype (the rules' reference).
// LEVELS_JS / PROTO_DIR override the paths.
import { pathToFileURL, fileURLToPath } from 'node:url';
import { join, dirname } from 'node:path';
import { readFileSync } from 'node:fs';

const here = dirname(fileURLToPath(import.meta.url));
export const PROTO_DIR = process.env.PROTO_DIR ?? join(here, '../../prototypes/seven-steps');

// Planet names per chapter id (design/levels/chapters.json, level design). Missing file = no names.
export const CHAPTERS_JSON = process.env.CHAPTERS_JSON ?? join(here, '../../design/levels/chapters.json');
export function loadPlanets(path = CHAPTERS_JSON) {
  try {
    return new Map(JSON.parse(readFileSync(path, 'utf8')).chapters.map((c, i) => [c.id, { planet: c.planet ?? '', order: i + 1 }]));
  } catch {
    return new Map();
  }
}

export async function loadLevels(protoDir = PROTO_DIR, levelsPath = process.env.LEVELS_JS ?? join(protoDir, 'src/levels.js'), planets = loadPlanets()) {
  try {
    const { LEVELS } = await import(pathToFileURL(levelsPath).href);
    const core = await import(pathToFileURL(join(protoDir, 'src/core.js')).href);
    const { levelKey } = await import(pathToFileURL(join(protoDir, 'src/telemetry.js')).href);
    return LEVELS.map((d, i) => {
      const parsed = core.parseLevel(d);
      const sol = core.solve(parsed);
      const fewest = core.minBodies(parsed);
      return {
        n: i + 1, name: d.name, chapter: d.chapter ?? '', planet: planets.get(d.chapter)?.planet ?? '', bodies: parsed.bodies, w: parsed.w, h: parsed.h,
        map: d.map.map((r) => r.padEnd(parsed.w, '#')), key: levelKey(parsed), minMoves: sol ? sol.path.length : null, minBodies: fewest?.bodies ?? null,
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
