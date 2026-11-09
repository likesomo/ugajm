// Exports the web prototype's stages (prototypes/seven-steps/src/levels.js) to unity/Assets/_Project/Data/levels.json,
// together with what the JS solver finds for each stage. Unity's LevelVerifier checks the C# port gives the same answers.
// Also copies design/levels/chapters.json to unity/Assets/_Project/Data/chapters.json (docs/architecture.md 6.4).
// Before writing anything it asserts the stage set (6.4); on any failure it prints every problem and exits 1
// without touching either file.
// Usage (repo root): node unity/Tools/export-levels.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { STEPS, parseLevel, minBodies, solve } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

const BODY_CAP = 3; // the triplets (user decision 2026-11-08)
const LETTER = { up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' };
const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const data = join(here, '..', 'Assets', '_Project', 'Data');
const out = join(data, 'levels.json');
const chaptersSrc = join(root, 'design', 'levels', 'chapters.json');
const chaptersOut = join(data, 'chapters.json');

const problems = [];
const fail = (msg) => problems.push(msg);

// ---- chapters.json ----
const chapterFile = JSON.parse(readFileSync(chaptersSrc, 'utf8'));
const chapters = (chapterFile.chapters ?? []).map((c) => ({
  id: c.id, planet: c.planet ?? '', concept: c.concept ?? '', intro: c.intro ?? '', artPreset: c.artPreset ?? '',
}));
const order = new Map();
chapters.forEach((c, i) => {
  if (!/^[a-z][a-z0-9-]*$/.test(c.id ?? '')) fail(`chapters.json #${i + 1}: id "${c.id}" must be lowercase letters, digits, hyphens`);
  if (order.has(c.id)) fail(`chapters.json: id "${c.id}" appears twice`);
  order.set(c.id, i);
  if (!c.planet) fail(`chapters.json "${c.id}": planet is empty`);
});
if (!chapters.length) fail('chapters.json: no chapters');

// ---- stages ----
const names = new Set();
let lastChapter = -1;
const levels = LEVELS.map((def, i) => {
  const tag = `stage ${i + 1} "${def.name}"`;
  if (names.has(def.name)) fail(`${tag}: name used twice (telemetry and progress v2 key on the name)`);
  names.add(def.name);
  if (!def.chapter) fail(`${tag}: no chapter`);
  else if (!order.has(def.chapter)) fail(`${tag}: chapter "${def.chapter}" is not in chapters.json`);
  else {
    const k = order.get(def.chapter);
    if (k < lastChapter) fail(`${tag}: chapter "${def.chapter}" comes back after a later chapter`);
    else if (k > lastChapter + 1) fail(`${tag}: chapter "${def.chapter}" skips chapter "${chapters[lastChapter + 1].id}"`);
    lastChapter = Math.max(lastChapter, k);
  }
  const level = parseLevel(def);
  if (level.bodies > BODY_CAP) fail(`${tag}: bodies ${level.bodies} > ${BODY_CAP}`);
  const best = minBodies(level);
  if (!best) { fail(`${tag}: no solution`); return null; }
  if (best.bodies > BODY_CAP) fail(`${tag}: needs ${best.bodies} bodies > ${BODY_CAP}`);
  return {
    name: def.name,
    hint: def.hint ?? '',
    bodies: level.bodies,
    statueCount: level.statueCount,
    // docs/architecture.md 6.4: chapter id and one-line story.
    ...(def.chapter ? { chapter: def.chapter } : {}),
    ...(def.story ? { story: def.story } : {}),
    map: def.map,
    expected: {
      minBodies: best.bodies,
      moves: best.moves,
      needsSwitch: !solve(level, level.bodies, { noSwitch: true }),
      path: best.path.map((a) => LETTER[a]).join(''),
    },
  };
});

if (problems.length) {
  for (const p of problems) console.error(`[export-levels] FAIL ${p}`);
  console.error(`[export-levels] ${problems.length} problem(s); levels.json and chapters.json were not written`);
  process.exit(1);
}

writeFileSync(out, JSON.stringify({ steps: STEPS, levels }, null, 2) + '\n');
writeFileSync(chaptersOut, JSON.stringify({ chapters }, null, 2) + '\n');
console.log(`[export-levels] ${levels.length} stages -> ${out}`);
console.log(`[export-levels] ${chapters.length} chapters -> ${chaptersOut}`);
