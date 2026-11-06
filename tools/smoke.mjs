// Render smoke test without a browser: bots play runs, every frame is rendered
// (with every operator selected) into a fake root and scanned for undefined/NaN/exceptions.
import { newGame, dispatch, alive, availableOps, solve, targetsOf, cultureOf } from '../src/core.js';
import { render } from '../src/render.js';
import { START_NUMBERS } from '../src/data.js';

const root = { innerHTML: '', querySelector: () => null };
const problems = [];
let frames = 0;
const phasesSeen = new Set();

function frame(s, ev, ctx, op = 'sub') {
  render(root, s, ev, { op });
  frames++;
  phasesSeen.add(s.phase);
  const bad = root.innerHTML.match(/undefined|NaN|\[object Object\]|null/);
  if (bad) problems.push(`${ctx} phase=${s.phase} op=${op}: "${bad[0]}"`);
}

for (const num of START_NUMBERS) {
  for (let seed = 1; seed <= 25; seed++) {
    const s = newGame(seed);
    const ctx = `num=${num} seed=${seed}`;
    try {
      frame(s, [], ctx);
      frame(s, dispatch(s, { type: 'pick', num }), ctx);
      let guard = 0;
      while (s.phase !== 'end' && guard++ < 3000) {
        let ev;
        if (s.phase === 'battle') {
          for (const op of availableOps(s.hero)) frame(s, [], ctx, op);
          const e = alive(s)[0];
          const path = solve(e.num, s.hero.num, availableOps(s.hero), targetsOf(cultureOf(s))) ?? ['dec'];
          ev = dispatch(s, { type: 'act', op: path[0], id: e.id });
        } else {
          dispatch(s, { type: 'buy', item: 'heal' });
          frame(s, [], ctx);
          ev = dispatch(s, seed % 2 ? { type: 'keep' } : { type: 'switch', num: s.altar.options[seed % 3] });
        }
        frame(s, ev, ctx);
      }
    } catch (err) {
      problems.push(`${ctx}: ${err.stack.split('\n').slice(0, 2).join(' ')}`);
    }
  }
}

console.log(`frames: ${frames}  phases: ${[...phasesSeen].join(',')}  problems: ${problems.length}`);
for (const p of problems.slice(0, 10)) console.log('  ' + p);
process.exit(problems.length ? 1 : 0);
