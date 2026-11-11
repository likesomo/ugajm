// Export assertions for the story data (design/story/story-integration.md 3.1). Node only, no Unity.
// Usage: node design/story/tools/check-story.mjs [--story <story.json>] [--strict-levels]
//   Checks design/story/story.json (#1-#12, #14), design/story/copy-levels.json (stage 4 proposal, errors),
//   and the current level data levels.js story + chapters.json intro/tagline (#13, warnings unless --strict-levels).
// Exit 1 on any error. Limits = narrative-structure.md 4.1 (minimal UI table); UiLayoutCheck is the final judge (3.3).
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const args = process.argv.slice(2);
const opt = n => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : null; };
const storyPath = resolve(opt('--story') ?? resolve(root, 'design/story/story.json'));
const copyPath = resolve(root, 'design/story/copy-levels.json');
const chaptersPath = resolve(root, 'design/levels/chapters.json');
const levelsPath = resolve(root, 'prototypes/seven-steps/src/levels.js');
const strictLevels = args.includes('--strict-levels');

const errors = [], warnings = [];
const err = (n, where, msg) => errors.push(`#${n} ${where}: ${msg}`);
const warn = (n, where, msg) => warnings.push(`#${n} ${where}: ${msg}`);

// ---- tables (narrative-structure.md 4.1, story-integration.md 2.3) ----
// [ko max, en max]; per line.
const LIMIT = {
  title: [20, 44], map: [20, 40], stillStar: [20, 44], loading: [16, 36], seventhFirst: [14, 30],
  keep: [14, 30], switch: [14, 30], bridge: [14, 30], seal: [14, 30], fall: [14, 30], vanish: [14, 30],
  lose: [10, 24], finale: [16, 36], constellation: [20, 40], ending: [18, 40],
  stage: [20, 42], intro: [18, 40], tagline: [12, 26],
};
const STAGE_MAX_WORDS = 7;
const ON = Object.keys(LIMIT).filter(k => !['stage', 'intro', 'tagline'].includes(k));
const AFTER = ['', 'ending', 'allStars', 'allTruths'];
const FORBIDDEN_KO = ['죽음', '희생', '저주', '영원히', '제물', '기도', '제사', '칠성', '운명'];
const FORBIDDEN_KO_WORD = /(^|[\s“‘])신(이|은|을|의|께|에게|도)?(?=$|[\s.,!?…”’])/; // the word 신, not 신발
const FORBIDDEN_EN = /\b(death|dead|die[sd]?|dying|sacrific\w*|curse[sd]?|forever|offering|gods?|pray\w*|ritual\w*|fate|destiny)\b/i;
const SYMBOLS_KO = new Set([...'…·,.?!“”‘’ ']);
const SYMBOLS_EN = new Set([...'…·,.?!“”‘’ \'']); // en contractions use ASCII '
const isHangul = c => c >= '가' && c <= '힣';
const isJamo = c => (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏');

// KS X 1001 (Gaegu) table, same as ksx1001.mjs
const dec = new TextDecoder('euc-kr');
const ksx = new Set();
for (let a = 0xB0; a <= 0xC8; a++) for (let b = 0xA1; b <= 0xFE; b++) ksx.add(dec.decode(Uint8Array.of(a, b)));
if (ksx.size !== 2350) throw new Error(`KS X 1001 table has ${ksx.size} entries, expected 2350`);

const len = s => [...s].length;
const words = s => s.trim().split(/\s+/).filter(Boolean).length;

/** #4-#7 (+#11 placeholder) on one string. report = err or warn. */
function checkText(report, where, lang, s, [koMax, enMax], { maxWords = 0, placeholders = false } = {}) {
  if (typeof s !== 'string' || !s.trim()) { report(3, where, `${lang} line is empty`); return; }
  const body = placeholders ? s.replaceAll('{brother}', '') : s;
  const max = lang === 'ko' ? koMax : enMax;
  if (len(s) > max) report(4, where, `${lang} ${len(s)} chars > ${max}: "${s}"`);
  if (maxWords && words(s) > maxWords) report(4, where, `${lang} ${words(s)} ${lang === 'ko' ? '어절' : 'words'} > ${maxWords}: "${s}"`);
  if (/[{}]/.test(body)) report(11, where, `${lang} only {brother} may be substituted: "${s}"`);
  if (/[0-9０-９]/.test(s)) report(6, where, `${lang} Arabic digits: "${s}"`);
  const allowed = lang === 'ko' ? SYMBOLS_KO : SYMBOLS_EN;
  const bad = new Set([...body].filter(c => !isHangul(c) && !/[A-Za-z0-9]/.test(c) && !allowed.has(c)));
  if (bad.size) report(6, where, `${lang} symbols not allowed: ${[...bad].map(c => JSON.stringify(c)).join(' ')}`);
  if (lang === 'ko') {
    const outside = new Set([...s].filter(c => (isHangul(c) && !ksx.has(c)) || isJamo(c)));
    if (outside.size) report(5, where, `ko outside KS X 1001: ${[...outside].join(' ')}`);
    if (/[A-Za-z]/.test(body)) report(6, where, `ko line has Latin letters: "${s}"`);
    const f = FORBIDDEN_KO.find(w => s.includes(w)) ?? (FORBIDDEN_KO_WORD.test(s) ? '신' : null);
    if (f) report(7, where, `ko forbidden word "${f}": "${s}"`);
  } else {
    const m = s.match(FORBIDDEN_EN);
    if (m) report(7, where, `en forbidden word "${m[0]}": "${s}"`);
  }
}

const readJson = (p, n) => { try { return JSON.parse(readFileSync(p, 'utf8')); } catch (e) { err(n, p, `cannot read JSON: ${e.message}`); return null; } };
const transOf = (text, lang, field) => (Array.isArray(text) ? text.find(t => t && t.lang === lang) : null)?.[field];

// ---- chapters.json (ids, languages) ----
const chapters = readJson(chaptersPath, 1);
const chapterIds = (chapters?.chapters ?? []).map(c => c.id);
const languages = chapters?.languages ?? [];
const others = languages.filter(l => l !== 'ko');

// ---- story.json ----
const story = readJson(storyPath, 1);
if (story) {
  // #1 shape
  if (story.version !== 1) err(1, 'story', `version must be 1 (got ${story.version})`);
  if (JSON.stringify(story.languages) !== JSON.stringify(languages)) err(1, 'story', `languages ${JSON.stringify(story.languages)} != chapters.json ${JSON.stringify(languages)}`);
  for (const k of ['brothers', 'beats', 'images', 'dipper', 'entries']) if (!Array.isArray(story[k])) err(1, 'story', `missing array "${k}"`);

  // #11 brother names: no final consonant (조사 고정)
  for (const b of story.brothers ?? []) {
    if (![1, 2, 3].includes(b.n)) err(1, `brothers`, `n must be 1..3 (got ${b.n})`);
    const last = [...(b.name ?? '')].at(-1) ?? '';
    if (!isHangul(last) || (last.charCodeAt(0) - 0xAC00) % 28 !== 0) err(11, `brothers.${b.n}`, `ko name must end without 받침: "${b.name}"`);
    for (const l of others) if (!transOf(b.text, l, 'name')) err(3, `brothers.${b.n}`, `missing ${l} name`);
  }
  if ((story.brothers ?? []).length !== 3) err(1, 'brothers', `need 3 brothers`);

  // beats (metadata only): chapter ids
  for (const b of story.beats ?? []) if (!chapterIds.includes(b.chapter)) err(8, 'beats', `unknown chapter "${b.chapter}"`);

  // images
  const images = new Set(story.images ?? []);
  for (const k of story.images ?? []) if (!/^[a-z0-9]+(\.[a-z0-9]+)*$/.test(k)) err(12, 'images', `bad key "${k}"`);
  for (const id of chapterIds) for (const p of ['card', 'carving']) if (!images.has(`${p}.${id}`)) err(12, 'images', `missing ${p}.${id}`);

  // #14 dipper
  const seen = new Set();
  for (const d of story.dipper ?? []) {
    const k = `${d.from}>${d.to}`;
    if (!chapterIds.includes(d.from)) err(14, 'dipper', `from "${d.from}" is not a chapter id`);
    if (!(chapterIds.includes(d.to) || d.to === 'circle')) err(14, 'dipper', `to "${d.to}" is not a chapter id or "circle"`);
    if (d.from === d.to) err(14, 'dipper', `self line ${k}`);
    if (seen.has(k)) err(14, 'dipper', `duplicate line ${k}`);
    seen.add(k);
  }
  for (const id of chapterIds) if (!(story.dipper ?? []).some(d => d.from === id)) err(14, 'dipper', `chapter "${id}" has no line from it`);

  // entries
  const ids = new Set(), combos = new Map();
  for (const e of story.entries ?? []) {
    const where = `entry ${e.id}`;
    // #2 ids
    if (typeof e.id !== 'string' || !/^[a-z0-9]+(\.[a-z0-9]+)*$/.test(e.id)) err(2, where, `id must match [a-z0-9.]+`);
    if (ids.has(e.id)) err(2, where, `duplicate entry id`);
    ids.add(e.id);
    // #8 on + qualifiers
    if (!ON.includes(e.on)) { err(8, where, `unknown on "${e.on}"`); continue; }
    for (const [k, t] of [['chapter', 'string'], ['after', 'string'], ['image', 'string'], ['brother', 'number'], ['stones', 'number'], ['card', 'number']])
      if (typeof e[k] !== t) err(1, where, `"${k}" must be a ${t} (no null)`);
    if (e.chapter !== '' && !chapterIds.includes(e.chapter)) err(8, where, `chapter "${e.chapter}" not in chapters.json`);
    if (!(Number.isInteger(e.brother) && e.brother >= 0 && e.brother <= 3)) err(8, where, `brother ${e.brother} not in 0..3`);
    if (!(Number.isInteger(e.stones) && e.stones >= -1 && e.stones <= 2)) err(8, where, `stones ${e.stones} not in -1..2`);
    if (!(Number.isInteger(e.card) && e.card >= 0 && e.card <= 4)) err(8, where, `card ${e.card} not in 0..4`);
    if (!AFTER.includes(e.after)) err(8, where, `after "${e.after}" not in ${JSON.stringify(AFTER)}`);
    if (e.on !== 'ending' && e.card !== 0) err(8, where, `card is only for on "ending"`);
    if (e.on === 'ending' && e.card === 0) err(8, where, `ending needs card 1..4`);
    if (e.after === 'allTruths' && !(e.on === 'ending' && e.card === 3)) err(8, where, `after "allTruths" is only for ending card 3`);
    // #12 image
    if (e.image !== '' && !images.has(e.image)) err(12, where, `image "${e.image}" not in images`);
    // #10 ambiguity
    const combo = [e.on, e.chapter, e.brother, e.stones, e.card, e.after].join('|');
    if (combos.has(combo)) err(10, where, `same trigger as ${combos.get(combo)}`);
    combos.set(combo, e.id);
    // lines: #2 ids, #3 translations, #4-#7, #11
    if (!Array.isArray(e.lines) || !e.lines.length) { err(3, where, `needs at least one line`); continue; }
    const lineIds = new Set();
    for (const l of e.lines) {
      const lw = `${where}.${l.id}`;
      if (typeof l.id !== 'string' || !l.id) err(2, lw, `line id missing`);
      if (lineIds.has(l.id)) err(2, lw, `duplicate line id`);
      lineIds.add(l.id);
      checkText(err, lw, 'ko', l.line, LIMIT[e.on], { placeholders: true });
      for (const lang of others) {
        const t = transOf(l.text, lang, 'line');
        if (t === undefined) err(3, lw, `missing ${lang}`);
        else checkText(err, lw, lang, t, LIMIT[e.on], { placeholders: true });
      }
    }
    // pool sizes (2.3)
    if (e.on === 'loading' && e.lines.length < 7) err(9, where, `loading needs 7+ lines`);
    if (['keep', 'switch', 'bridge'].includes(e.on) && e.lines.length < 2) err(9, where, `${e.on} needs 2+ lines`);
  }

  // #9 required entries (story-integration 0.3 / 0.5 / 2.3)
  const has = q => (story.entries ?? []).some(e => Object.entries(q).every(([k, v]) => e[k] === v));
  const need = (q, what) => { if (!has(q)) err(9, 'entries', `missing ${what}`); };
  need({ on: 'title', after: '' }, 'title (after "")');
  need({ on: 'title', after: 'ending' }, 'title (after "ending")');
  for (const id of chapterIds) need({ on: 'finale', chapter: id }, `finale for ${id}`);
  for (const b of [1, 2, 3]) need({ on: 'ending', card: 1, brother: b }, `ending card 1 brother ${b}`);
  for (const s of [0, 1, 2]) need({ on: 'ending', card: 2, stones: s }, `ending card 2 stones ${s}`);
  need({ on: 'ending', card: 3, after: '' }, 'ending card 3 (after "")');
  need({ on: 'ending', card: 3, after: 'allTruths' }, 'ending card 3 (after "allTruths")');
  need({ on: 'ending', card: 4 }, 'ending card 4');
  need({ on: 'lose' }, 'lose');
  // optional groups: if one brother is written, all three must be
  for (const on of ['keep', 'switch', 'bridge']) if (has({ on })) for (const b of [1, 2, 3]) need({ on, brother: b }, `${on} brother ${b}`);
  if (has({ on: 'map' })) for (const a of ['', 'ending', 'allStars']) need({ on: 'map', after: a }, `map (after "${a}")`);
  if (has({ on: 'constellation' })) for (const id of chapterIds) need({ on: 'constellation', chapter: id }, `constellation for ${id}`);
}

// ---- #13 level data: stage-4 proposal (errors) and current data (warnings) ----
let LEVELS = [];
try { ({ LEVELS } = await import(pathToFileURL(levelsPath).href)); } catch (e) { err(13, 'levels.js', `cannot import: ${e.message}`); }

const copy = readJson(copyPath, 13);
if (copy) {
  if (copy.stages?.length !== LEVELS.length) err(13, 'copy-levels', `${copy.stages?.length} stages, levels.js has ${LEVELS.length}`);
  for (const s of copy.stages ?? []) {
    const where = `copy-levels stage ${s.n}`;
    const lv = LEVELS[s.n - 1];
    if (lv && lv.chapter !== s.chapter) err(13, where, `chapter "${s.chapter}" but levels.js says "${lv.chapter}"`);
    if (s.startLine !== undefined && !['story', 'hint', ''].includes(s.startLine)) err(13, where, `startLine "${s.startLine}"`);
    checkText(err, where, 'ko', s.story, LIMIT.stage, { maxWords: STAGE_MAX_WORDS });
    checkText(err, where, 'en', s.en, LIMIT.stage, { maxWords: STAGE_MAX_WORDS });
    if (s.alt !== undefined) {
      checkText(err, `${where} alt`, 'ko', s.alt.story, LIMIT.stage, { maxWords: STAGE_MAX_WORDS });
      checkText(err, `${where} alt`, 'en', s.alt.en, LIMIT.stage, { maxWords: STAGE_MAX_WORDS });
      if (s.alt.story === s.story) err(13, where, `alt repeats story`);
    }
  }
  const copyIds = (copy.chapters ?? []).map(c => c.id);
  if (JSON.stringify(copyIds) !== JSON.stringify(chapterIds)) err(13, 'copy-levels', `chapters ${JSON.stringify(copyIds)} != chapters.json`);
  for (const c of copy.chapters ?? []) {
    for (const f of ['intro', 'tagline']) {
      checkText(err, `copy-levels ${c.id}.${f}`, 'ko', c[f], LIMIT[f]);
      checkText(err, `copy-levels ${c.id}.${f}`, 'en', c.en?.[f], LIMIT[f]);
    }
  }
}

const levelReport = strictLevels ? err : warn;
for (const [i, lv] of LEVELS.entries()) {
  const where = `levels.js stage ${i + 1}`;
  if (!lv.story) continue;
  checkText(levelReport, where, 'ko', lv.story, LIMIT.stage, { maxWords: STAGE_MAX_WORDS });
  const en = transOf(lv.text, 'en', 'story');
  if (en !== undefined) checkText(levelReport, where, 'en', en, LIMIT.stage, { maxWords: STAGE_MAX_WORDS });
}
for (const c of chapters?.chapters ?? []) for (const f of ['intro', 'tagline']) {
  checkText(levelReport, `chapters.json ${c.id}.${f}`, 'ko', c[f], LIMIT[f]);
  for (const lang of others) checkText(levelReport, `chapters.json ${c.id}.${f}`, lang, transOf(c.text, lang, f) ?? '', LIMIT[f]);
}

// ---- report ----
const n = (story?.entries ?? []).length, lines = (story?.entries ?? []).reduce((a, e) => a + (e.lines?.length ?? 0), 0);
if (warnings.length) {
  console.log(`WARN ${warnings.length} (current level data, #13; becomes an error once the stage-4 copy is merged)`);
  for (const w of warnings) console.log(`  ${w}`);
}
for (const e of errors) console.log(`ERROR ${e}`);
console.log(errors.length
  ? `FAIL ${errors.length} errors`
  : `OK story.json ${n} entries / ${lines} lines, dipper ${(story?.dipper ?? []).length}, copy-levels ${copy?.stages?.length ?? 0} stages + ${copy?.chapters?.length ?? 0} chapters`);
process.exit(errors.length ? 1 : 0);
