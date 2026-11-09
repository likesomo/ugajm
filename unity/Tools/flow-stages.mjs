// Stages for `unity.sh smoke` flow step (docs/scene-flow.md S8): prints "N:path" for every planet's last stage
// (levels.json "chapter" changes after it, or the last stage) plus stage 1 and the second to last stage as
// mid-planet controls. path = the JS solver's answer (expected.path), which wins the stage.
// Usage: node flow-stages.mjs <levels.json> [--planet2-first]   (the second form prints the first stage of the second planet)
import fs from 'node:fs';

const levels = JSON.parse(fs.readFileSync(process.argv[2], 'utf8')).levels;
if (process.argv[3] === '--planet2-first') {
  const i = levels.findIndex(l => (l.chapter || '') !== (levels[0].chapter || ''));
  if (i < 0) throw new Error('levels.json has one planet');
  console.log(i + 1);
  process.exit(0);
}
const out = [];
levels.forEach((l, i) => {
  const last = i === levels.length - 1;
  const planetEnd = last || (l.chapter || '') !== (levels[i + 1].chapter || '');
  if (planetEnd || i === 0 || i === levels.length - 2) {
    if (!l.expected || !l.expected.path) throw new Error(`stage ${i + 1} has no expected.path (unity.sh levels)`);
    out.push(`${i + 1}:${l.expected.path}`);
  }
});
console.log(out.join(' '));
