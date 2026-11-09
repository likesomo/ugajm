// Exports the web prototype's stages (prototypes/seven-steps/src/levels.js) to unity/Assets/_Project/Data/levels.json,
// together with what the JS solver finds for each stage. Unity's LevelVerifier checks the C# port gives the same answers.
// Also copies design/levels/chapters.json (with its "text" translations) to unity/Assets/_Project/Data/chapters.json (docs/architecture.md 6.4).
// Before writing anything it asserts the stage set (6.4); on any failure it prints every problem and exits 1
// without touching either file. Missing or empty translations for a language in chapters.json "languages" fail too (13.6),
// and so does a chapter without a tagline once any chapter has one (6.4).
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
// Translations (docs/architecture.md 13.5, 13.6). design/levels/chapters.json "languages" lists the languages that are on;
// ko is the base fields, every other one must be a "text" entry with no empty field on every chapter and stage.
const BASE_LANG = 'ko';
const LANGS = (chapterFile.languages ?? [BASE_LANG]).filter((l) => l !== BASE_LANG);
const LANG_CODE = /^[a-z]{2}$/;
if (!LANGS.every((l) => LANG_CODE.test(l))) fail(`chapters.json languages ${JSON.stringify(chapterFile.languages)}: codes are two lowercase letters`);
// Checks one "text" array and returns it trimmed to the known fields (unknown languages are an error, not dropped).
function checkText(text, fields, tag) {
  const out = [];
  const seen = new Set();
  for (const t of text ?? []) {
    if (!LANGS.includes(t?.lang)) { fail(`${tag}: text lang "${t?.lang}" is not in chapters.json languages`); continue; }
    if (seen.has(t.lang)) fail(`${tag}: text lang "${t.lang}" appears twice`);
    seen.add(t.lang);
    const e = { lang: t.lang };
    for (const f of fields) {
      if (typeof t[f] !== 'string' || !t[f].trim()) fail(`${tag}: ${t.lang} ${f} is empty`);
      e[f] = t[f] ?? '';
    }
    out.push(e);
  }
  for (const l of LANGS) if (!seen.has(l)) fail(`${tag}: no ${l} text`);
  return out;
}

// tagline (6.4, 2026-11-10): one line for the planet select screen. All or none while level design fills it in:
// once any chapter has a ko tagline, every chapter needs one and every language's text needs a tagline.
const HAS_TAGLINE = (chapterFile.chapters ?? []).some((c) => typeof c.tagline === 'string' && c.tagline.trim());
const TEXT_FIELDS = HAS_TAGLINE ? ['planet', 'concept', 'intro', 'tagline'] : ['planet', 'concept', 'intro'];
if (!HAS_TAGLINE) console.warn('[export-levels] WARN chapters.json: no tagline yet (planet select shows none; docs 6.4)');
const chapters = (chapterFile.chapters ?? []).map((c, i) => ({
  id: c.id, planet: c.planet ?? '', concept: c.concept ?? '', intro: c.intro ?? '', tagline: c.tagline ?? '', artPreset: c.artPreset ?? '',
  text: checkText(c.text, TEXT_FIELDS, `chapters.json "${c.id ?? `#${i + 1}`}"`),
}));
const order = new Map();
chapters.forEach((c, i) => {
  if (!/^[a-z][a-z0-9-]*$/.test(c.id ?? '')) fail(`chapters.json #${i + 1}: id "${c.id}" must be lowercase letters, digits, hyphens`);
  if (order.has(c.id)) fail(`chapters.json: id "${c.id}" appears twice`);
  order.set(c.id, i);
  if (!c.planet) fail(`chapters.json "${c.id}": planet is empty`);
  if (HAS_TAGLINE && !(typeof c.tagline === 'string' && c.tagline.trim())) fail(`chapters.json "${c.id}": tagline is empty (other chapters have one)`);
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
    // docs/architecture.md 13.5: translations; name stays the ko save/telemetry key.
    text: checkText(def.text, ['name', 'hint', 'story'], tag),
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
