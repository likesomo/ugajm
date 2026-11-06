// Headless balance sim: bots play many seeded runs through dispatch() only.
// Usage: node tools/sim.mjs [runsPerCell]   -> reports/sim-latest.json + summary
import { writeFileSync, mkdirSync } from 'node:fs';
import {
  newGame, dispatch, alive, solve, availableOps, targetsOf, cultureOf, forecast, ease, fit,
} from '../src/core.js';
import { START_NUMBERS, ITEMS } from '../src/data.js';

const RUNS = Number(process.argv[2] || 300);

// Work on the most urgent enemy: least slack (timer - moves needed), then biggest hit.
function battleCmd(s) {
  const h = s.hero;
  const ops = availableOps(h);
  const targets = targetsOf(cultureOf(s));
  let best = null;
  for (const e of alive(s)) {
    const path = solve(e.num, h.num, ops, targets) ?? ['sub'];
    const slack = e.timer - path.length;
    const score = slack * 100 - e.num;
    if (!best || score < best.score) best = { score, cmd: { type: 'act', op: path[0], id: e.id } };
  }
  return best.cmd;
}

function shop(s) {
  const h = s.hero;
  if (h.hp < h.maxHp * 0.7 && h.gold >= ITEMS.heal.price) dispatch(s, { type: 'buy', item: 'heal' });
  if (h.gold >= ITEMS.vit.price) dispatch(s, { type: 'buy', item: 'vit' });
}

const bestOption = (s, culture) => s.altar.options.reduce((a, b) => (fit(b, culture) < fit(a, culture) ? b : a));

const STRATEGIES = {
  keeper: () => ({ type: 'keep' }),
  // Always jump to the number that is easiest for the next culture.
  hopper: (s) => {
    const next = forecast(s)[0].culture;
    const alt = bestOption(s, next);
    return fit(alt, next) < fit(s.hero.num, next) ? { type: 'switch', num: alt } : { type: 'keep' };
  },
  // Switch only when the gain is large, and hold once faith has built up.
  thoughtful: (s) => {
    const next = forecast(s)[0].culture;
    const alt = bestOption(s, next);
    const gain = fit(s.hero.num, next) - fit(alt, next);
    return gain > 0.3 + s.hero.faith * 0.3 ? { type: 'switch', num: alt } : { type: 'keep' };
  },
  random: (s) => (s.rng % 2 ? { type: 'keep' } : { type: 'switch', num: s.altar.options[s.rng % 3] }),
};

export function play(seed, startNum, strategy) {
  const s = newGame(seed);
  dispatch(s, { type: 'pick', num: startNum });
  let guard = 0;
  while (s.phase !== 'end' && guard++ < 3000) {
    if (s.phase === 'battle') dispatch(s, battleCmd(s));
    else { shop(s); dispatch(s, STRATEGIES[strategy](s)); }
  }
  return s;
}

const a = JSON.stringify(play(12345, 8, 'thoughtful'));
const deterministic = a === JSON.stringify(play(12345, 8, 'thoughtful'));

const cells = [];
for (const strategy of Object.keys(STRATEGIES)) {
  for (const num of START_NUMBERS) {
    let wins = 0, floorSum = 0, switchSum = 0, turnSum = 0;
    for (let i = 0; i < RUNS; i++) {
      const s = play(1000 + i, num, strategy);
      if (s.ending !== 'dead') wins++;
      floorSum += s.floor;
      switchSum += s.hero.switches;
    }
    cells.push({ strategy, start: num, winRate: +(wins / RUNS).toFixed(3), avgFloor: +(floorSum / RUNS).toFixed(2), avgSwitches: +(switchSum / RUNS).toFixed(2) });
  }
}

const easeTable = Object.fromEntries(START_NUMBERS.map((n) => [n, ['west', 'east', 'math'].map((c) => ease(n, c))]));
mkdirSync(new URL('../reports/', import.meta.url), { recursive: true });
writeFileSync(new URL('../reports/sim-latest.json', import.meta.url), JSON.stringify({ runsPerCell: RUNS, deterministic, easeTable, cells }, null, 2));

console.log(`deterministic: ${deterministic}  runs/cell: ${RUNS}`);
console.log('ease (avg moves, west/east/math):', JSON.stringify(easeTable));
console.log('strategy     ' + START_NUMBERS.map((n) => String(n).padStart(6)).join(''));
for (const strategy of Object.keys(STRATEGIES)) {
  const row = cells.filter((c) => c.strategy === strategy);
  console.log(strategy.padEnd(12) + row.map((c) => `${(c.winRate * 100).toFixed(0)}%`.padStart(6)).join('') + `   floor ${(row.reduce((a, c) => a + c.avgFloor, 0) / row.length).toFixed(1)}  sw ${(row.reduce((a, c) => a + c.avgSwitches, 0) / row.length).toFixed(1)}`);
}
