// Checks design/story/cutscenes.json (design/story/cutscenes.md). Node only, no Unity.
// Usage: node design/story/tools/check-cutscenes.mjs [path] ; exit 1 on any error.
//   - every page: unique image key cutscene.<scene>.<page>, 1-3 lines, ko + en on every line, an art prompt
//   - ko: Hangul only from KS X 1001 (the 2,350 syllables Gaegu covers), no '·' (Gaegu has no U+00B7; scene-validate
//     catches it later, this catches it first), symbols from a small set; forbidden words as check-story.mjs
//   - length: ko <= KO_MAX, en <= EN_MAX characters per line (storybook text box, 1-2 rows at 1920x1080)
//   - rule (optional, 2026-11-13): one plain-language rule caption per page, ko + en, ko <= RULE_KO_MAX, en <= RULE_EN_MAX;
//     may also use digits, ':' and Latin capitals (KEEP, SWITCH)
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const path = resolve(process.argv[2] ?? resolve(root, 'design/story/cutscenes.json'));
const KO_MAX = 32, EN_MAX = 90, RULE_KO_MAX = 40, RULE_EN_MAX = 100;
const FORBIDDEN_KO = ['죽음', '희생', '저주', '영원히', '제물', '기도', '제사', '칠성', '운명'];
const FORBIDDEN_EN = /\b(death|dead|die[sd]?|dying|sacrific\w*|curse[sd]?|forever|offering|gods?|pray\w*|ritual\w*|fate|destiny)\b/i;
const KO_SYMBOLS = new Set([...' ,.?!…“”‘’']);
const RULE_SYMBOLS = new Set([...KO_SYMBOLS, ...'0123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZ']);

const dec = new TextDecoder('euc-kr');
const ksx = new Set();
for (let a = 0xB0; a <= 0xC8; a++) for (let b = 0xA1; b <= 0xFE; b++) ksx.add(dec.decode(Uint8Array.of(a, b)));

const errors = [];
const data = JSON.parse(readFileSync(path, 'utf8'));
if (!Array.isArray(data.scenes) || data.scenes.length === 0) errors.push('no scenes');
const keys = new Set();
let pages = 0, rules = 0;
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
    const check = (l, w, koMax, enMax, symbols) => {
      const ko = l.line ?? '';
      const en = (l.text ?? []).find(t => t.lang === 'en')?.line ?? '';
      if (!ko) errors.push(`${w}: no ko`);
      if (!en) errors.push(`${w}: no en`);
      if (ko.length > koMax) errors.push(`${w}: ko ${ko.length} > ${koMax}`);
      if (en.length > enMax) errors.push(`${w}: en ${en.length} > ${enMax}`);
      for (const c of ko) {
        if (c >= '가' && c <= '힣') { if (!ksx.has(c)) errors.push(`${w}: '${c}' outside KS X 1001 (Gaegu)`); }
        else if (!symbols.has(c)) errors.push(`${w}: symbol '${c}' (U+${c.codePointAt(0).toString(16).toUpperCase()})`);
      }
      for (const f of FORBIDDEN_KO) if (ko.includes(f)) errors.push(`${w}: forbidden "${f}"`);
      const m = en.match(FORBIDDEN_EN);
      if (m) errors.push(`${w}: forbidden "${m[0]}"`);
    };
    lines.forEach((l, i) => check(l, `${at} line ${i + 1}`, KO_MAX, EN_MAX, KO_SYMBOLS));
    if (p.rule !== undefined) { rules++; check(p.rule, `${at} rule`, RULE_KO_MAX, RULE_EN_MAX, RULE_SYMBOLS); }
  }
}
for (const e of errors) console.log(`ERROR ${e}`);
console.log(errors.length ? `FAIL ${errors.length} errors` : `OK ${data.scenes.length} scenes, ${pages} pages, ${rules} rules, ${keys.size} images`);
process.exit(errors.length ? 1 : 0);
