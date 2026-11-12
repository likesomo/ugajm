// Checks design/story/cutscenes.json (design/story/cutscenes.md). Node only, no Unity.
// Usage: node design/story/tools/check-cutscenes.mjs [path] ; exit 1 on any error.
//   - every page: unique image key cutscene.<scene>.<page>, 1-3 lines, ko + en on every line, an art prompt
//   - ko: Hangul only from KS X 1001 (the 2,350 syllables Gaegu covers), no '·' (Gaegu has no U+00B7; scene-validate
//     catches it later, this catches it first), symbols from a small set; forbidden words as check-story.mjs
//   - length: ko <= KO_MAX, en <= EN_MAX characters per line (storybook text box, 1-2 rows at 1920x1080)
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const path = resolve(process.argv[2] ?? resolve(root, 'design/story/cutscenes.json'));
const KO_MAX = 32, EN_MAX = 90;
const FORBIDDEN_KO = ['죽음', '희생', '저주', '영원히', '제물', '기도', '제사', '칠성', '운명'];
const FORBIDDEN_EN = /\b(death|dead|die[sd]?|dying|sacrific\w*|curse[sd]?|forever|offering|gods?|pray\w*|ritual\w*|fate|destiny)\b/i;
const KO_SYMBOLS = new Set([...' ,.?!…“”‘’']);

const dec = new TextDecoder('euc-kr');
const ksx = new Set();
for (let a = 0xB0; a <= 0xC8; a++) for (let b = 0xA1; b <= 0xFE; b++) ksx.add(dec.decode(Uint8Array.of(a, b)));

const errors = [];
const data = JSON.parse(readFileSync(path, 'utf8'));
if (!Array.isArray(data.scenes) || data.scenes.length === 0) errors.push('no scenes');
const keys = new Set();
let pages = 0;
for (const s of data.scenes ?? []) {
  if (!s.id) errors.push('scene without id');
  if (!Array.isArray(s.pages) || s.pages.length === 0) errors.push(`${s.id}: no pages`);
  for (const p of s.pages ?? []) {
    pages++;
    const at = `${s.id}.${p.id}`;
    const want = `cutscene.${s.id}.${p.id}`;
    if (p.image !== want) errors.push(`${at}: image "${p.image}", expected "${want}"`);
    if (keys.has(p.image)) errors.push(`${at}: duplicate image key`);
    keys.add(p.image);
    if (!p.art || p.art.length < 20) errors.push(`${at}: missing art prompt`);
    const lines = p.lines ?? [];
    if (lines.length < 1 || lines.length > 3) errors.push(`${at}: ${lines.length} lines (1-3)`);
    lines.forEach((l, i) => {
      const w = `${at} line ${i + 1}`;
      const ko = l.line ?? '';
      const en = (l.text ?? []).find(t => t.lang === 'en')?.line ?? '';
      if (!ko) errors.push(`${w}: no ko`);
      if (!en) errors.push(`${w}: no en`);
      if (ko.length > KO_MAX) errors.push(`${w}: ko ${ko.length} > ${KO_MAX}`);
      if (en.length > EN_MAX) errors.push(`${w}: en ${en.length} > ${EN_MAX}`);
      for (const c of ko) {
        if (c >= '가' && c <= '힣') { if (!ksx.has(c)) errors.push(`${w}: '${c}' outside KS X 1001 (Gaegu)`); }
        else if (!KO_SYMBOLS.has(c)) errors.push(`${w}: symbol '${c}' (U+${c.codePointAt(0).toString(16).toUpperCase()})`);
      }
      for (const f of FORBIDDEN_KO) if (ko.includes(f)) errors.push(`${w}: forbidden "${f}"`);
      const m = en.match(FORBIDDEN_EN);
      if (m) errors.push(`${w}: forbidden "${m[0]}"`);
    });
  }
}
for (const e of errors) console.log(`ERROR ${e}`);
console.log(errors.length ? `FAIL ${errors.length} errors` : `OK ${data.scenes.length} scenes, ${pages} pages, ${keys.size} images`);
process.exit(errors.length ? 1 : 0);
