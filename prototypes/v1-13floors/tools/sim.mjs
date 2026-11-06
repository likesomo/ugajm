// Headless balance sim: bots play many seeded runs through dispatch() only.
// Usage: node tools/sim.mjs [runsPerCell]
// Writes reports/sim-latest.json and prints a short summary.
import { writeFileSync, mkdirSync } from 'node:fs';
import {
  newGame, dispatch, alive, relation, forecast, numberStatus, itemPrice, maxHpOf, NUMBER_IDS,
} from '../src/core.js';

const RUNS = Number(process.argv[2] || 400);

function battleCmd(s) {
  const h = s.hero;
  const foes = alive(s);
  if (h.ultCharge > 0 && (foes.length >= 3 || foes.some((e) => e.kind !== 'normal'))) return { type: 'ult' };
  const score = (e) => {
    const r = relation(s, e);
    return (r.recruit ? 100 : 0) + (r.combo ? 30 : 0) + (r.crit ? 20 : 0) - e.hp;
  };
  const target = foes.reduce((a, b) => (score(b) > score(a) ? b : a));
  return { type: 'attack', id: target.id };
}

function shop(s) {
  const h = s.hero;
  const order = h.hp < maxHpOf(h) * 0.6 ? ['heal', 'atk', 'vit'] : ['atk', 'vit', 'heal'];
  for (const item of order) if (h.gold >= itemPrice(s, item)) dispatch(s, { type: 'buy', item });
}

const STRATEGIES = {
  keeper: () => ({ type: 'keep' }),
  adapter: (s) => {
    const next = forecast(s)[0].culture;
    if (numberStatus(s.hero.num, next) !== 'cursed') return { type: 'keep' };
    const better = s.altar.options.find((n) => numberStatus(n, next) === 'blessed')
      ?? s.altar.options.find((n) => numberStatus(n, next) === 'neutral');
    return { type: 'switch', num: better };
  },
  // Switch away from a cursed floor only while faith is still cheap to give up.
  thoughtful: (s) => {
    if (s.hero.faith >= 3) return { type: 'keep' };
    return STRATEGIES.adapter(s);
  },
  random: (s) => (s.rng % 2 ? { type: 'keep' } : { type: 'switch', num: s.altar.options[s.rng % 3] }),
};

function play(seed, startNum, strategy) {
  const s = newGame(seed);
  dispatch(s, { type: 'pick', num: startNum });
  let guard = 0;
  while (s.phase !== 'end' && guard++ < 2000) {
    if (s.phase === 'battle') dispatch(s, battleCmd(s));
    else if (s.phase === 'altar') { shop(s); dispatch(s, STRATEGIES[strategy](s)); }
  }
  return s;
}

// Determinism check: same seed + same commands -> identical state.
const a = JSON.stringify(play(12345, 7, 'adapter'));
const b = JSON.stringify(play(12345, 7, 'adapter'));
const deterministic = a === b;

const cells = [];
for (const strategy of Object.keys(STRATEGIES)) {
  for (const num of NUMBER_IDS) {
    let wins = 0, floorSum = 0, turnsSum = 0, switchSum = 0;
    const endings = {};
    for (let i = 0; i < RUNS; i++) {
      const s = play(1000 + i, num, strategy);
      if (s.ending !== 'dead') wins++;
      endings[s.ending] = (endings[s.ending] || 0) + 1;
      floorSum += s.floor;
      switchSum += s.hero.switches;
    }
    cells.push({
      strategy, start: num, winRate: +(wins / RUNS).toFixed(3),
      avgFloor: +(floorSum / RUNS).toFixed(2), avgSwitches: +(switchSum / RUNS).toFixed(2), endings,
    });
  }
}

const report = { runsPerCell: RUNS, deterministic, cells };
mkdirSync(new URL('../reports/', import.meta.url), { recursive: true });
writeFileSync(new URL('../reports/sim-latest.json', import.meta.url), JSON.stringify(report, null, 2));

console.log(`deterministic: ${deterministic}  runs/cell: ${RUNS}`);
console.log('strategy  start  win%   avgFloor  switches');
for (const c of cells) {
  console.log(`${c.strategy.padEnd(9)} ${String(c.start).padStart(5)}  ${(c.winRate * 100).toFixed(1).padStart(5)}  ${String(c.avgFloor).padStart(8)}  ${String(c.avgSwitches).padStart(8)}`);
}
