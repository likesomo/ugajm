// Writes prototypes/seven-steps/src/levels.js from the batch candidates (design/levels/rollout-50.md).
// chapter, story and (optional) a Korean hint override come from design/levels/story-50.json, matched by stage number and checked by name.
// English text (name, hint, story) comes from design/levels/i18n/en.json the same way and goes into "text" (docs/architecture.md 13.5).
// Usage (repo root): node prototypes/seven-steps/tools/write-levels.mjs <count>     e.g. 20 for R1
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { LEVELS as CANDIDATES } from '../experiments/batch5/levels-1-50.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const count = Number(process.argv[2]);
if (!(count >= 1 && count <= CANDIDATES.length)) throw new Error(`usage: write-levels.mjs <1..${CANDIDATES.length}>`);
const story = JSON.parse(readFileSync(join(ROOT, 'design/levels/story-50.json'), 'utf8')).stages;
const en = JSON.parse(readFileSync(join(ROOT, 'design/levels/i18n/en.json'), 'utf8')).stages;

const q = (s) => `'${String(s).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;
let out = `// Stages in play order. Legend lives in core.js parseLevel(). tools/solve.mjs checks every stage is clearable.
// chapter = design/levels/chapters.json id, story = the triplets' one line, separate from the hint (docs/architecture.md 6.4).
// Written by tools/write-levels.mjs from the batch candidates (design/levels/stage-plan-50.md, rollout-50.md).
export const LEVELS = [
`;
CANDIDATES.slice(0, count).forEach((d, i) => {
  const s = story[i];
  if (!s || s.name !== d.name) throw new Error(`story-50.json stage ${i + 1} is "${s?.name}", candidate is "${d.name}"`);
  const e = en[i];
  if (!e || e.n !== i + 1 || e.ko !== d.name) throw new Error(`en.json stage ${i + 1} is "${e?.ko}", candidate is "${d.name}"`);
  out += '  {\n';
  out += `    name: ${q(d.name)},\n`;
  out += `    chapter: ${q(s.chapter)},\n`;
  out += `    story: ${q(s.story)},\n`;
  out += `    hint: ${q(s.hint ?? d.hint ?? '')},\n`;
  // startLine (design/story/story-integration.md 0.3): which line the HUD shows at stage start. Omitted = "story".
  if (s.startLine !== undefined) out += `    startLine: ${q(s.startLine)},\n`;
  out += `    bodies: ${d.bodies ?? 3},\n`;
  out += `    text: [{ lang: 'en', name: ${q(e.name)}, hint: ${q(e.hint)}, story: ${q(e.story)} }],
`;
  if (d.statueCount !== undefined) out += `    statueCount: ${d.statueCount},\n`;
  out += `    map: [\n${d.map.map((r) => `      ${q(r)},`).join('\n')}\n    ],\n`;
  out += '  },\n';
});
out += '];\n';
writeFileSync(join(ROOT, 'prototypes/seven-steps/src/levels.js'), out);
console.log(`[write-levels] ${count} stages -> prototypes/seven-steps/src/levels.js`);
