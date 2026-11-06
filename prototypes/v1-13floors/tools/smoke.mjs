// Render smoke test without a browser: bots play runs, every frame is rendered
// into a fake root and scanned for undefined/NaN/exceptions.
import { newGame, dispatch, alive, NUMBER_IDS } from '../src/core.js';
import { render } from '../src/render.js';

const root = { innerHTML: '', querySelector: () => null };
const problems = [];
let frames = 0;
const phasesSeen = new Set();

function frame(s, ev, ctx) {
  render(root, s, ev);
  frames++;
  phasesSeen.add(s.phase);
  const bad = root.innerHTML.match(/undefined|NaN|\[object Object\]/);
  if (bad) problems.push(`${ctx} phase=${s.phase}: "${bad[0]}"`);
}

for (const num of NUMBER_IDS) {
  for (let seed = 1; seed <= 40; seed++) {
    const s = newGame(seed);
    const ctx = `num=${num} seed=${seed}`;
    try {
      frame(s, [], ctx);
      frame(s, dispatch(s, { type: 'pick', num }), ctx);
      let guard = 0;
      while (s.phase !== 'end' && guard++ < 2000) {
        let ev;
        if (s.phase === 'battle') {
          ev = s.hero.ultCharge ? dispatch(s, { type: 'ult' }) : dispatch(s, { type: 'attack', id: alive(s)[0].id });
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
