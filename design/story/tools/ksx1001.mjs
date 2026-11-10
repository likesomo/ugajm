// Lists Hangul syllables in the given files that are outside KS X 1001 (the 2,350 syllables Gaegu covers).
// Usage: node design/story/tools/ksx1001.mjs <file>... ; exit 1 if any are found.
import { readFileSync } from 'node:fs';
const dec = new TextDecoder('euc-kr');
const ok = new Set();
for (let a = 0xB0; a <= 0xC8; a++) for (let b = 0xA1; b <= 0xFE; b++) ok.add(dec.decode(Uint8Array.of(a, b)));
if (ok.size < 2350) throw new Error(`KS X 1001 table has ${ok.size} entries, expected 2350`);
let bad = 0;
for (const f of process.argv.slice(2)) {
  const miss = new Set([...readFileSync(f, 'utf8')].filter(c => c >= '가' && c <= '힣' && !ok.has(c)));
  if (miss.size) { bad += miss.size; console.log(`${f}: ${[...miss].join(' ')}`); }
}
console.log(bad ? `FAIL ${bad} syllables outside KS X 1001` : 'OK all Hangul in KS X 1001');
process.exit(bad ? 1 : 0);
