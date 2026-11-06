// Pure game core. Every state change goes through dispatch(state, cmd).
// No DOM, no Date, no Math.random: time and seed come from outside.
import { nextRandom, randInt, pick } from './rng.js';
import { START_NUMBERS, CULTURES, OPS, ITEMS, TUNING as T } from './data.js';

export function newGame(seed) {
  const s = {
    seed: seed >>> 0, rng: seed >>> 0, phase: 'pick', floor: 0,
    cultures: [], hero: null, enemies: [], nextId: 1,
    pendingBetrayed: [], timeline: [], altar: null, ending: null, turn: 0,
  };
  const keys = Object.keys(CULTURES);
  for (let i = 0; i < T.floors; i++) s.cultures.push(pick(s, keys));
  return s;
}

// ---------- pure math (shared by core, UI preview, bots) ----------

export function applyOp(op, v, n) {
  switch (op) {
    case 'inc': return v + 1 <= T.maxValue ? v + 1 : null;
    case 'dec': return v - 1 >= 0 ? v - 1 : null;
    case 'add': return v + n <= T.maxValue ? v + n : null;
    case 'sub': return v - n >= 0 ? v - n : null;
    case 'mul': return v * n <= T.maxValue ? v * n : null;
    case 'div': return v % n === 0 ? v / n : null;
    case 'mod': return v >= n ? v % n : null;
    case 'digit': return v >= 10 ? String(v).split('').reduce((a, d) => a + Number(d), 0) : null;
    default: return null;
  }
}

export const BASE_OPS = ['inc', 'dec', 'add', 'sub', 'mul', 'div'];

// Shortest op sequence from v to any target (BFS). Returns null if none within depth.
export function solve(v, n, ops, targets, depth = T.solverDepth) {
  if (targets.includes(v)) return [];
  const seen = new Set([v]);
  let frontier = [[v, []]];
  for (let d = 0; d < depth; d++) {
    const next = [];
    for (const [val, path] of frontier) {
      for (const op of ops) {
        const r = applyOp(op, val, n);
        if (r === null || seen.has(r)) continue;
        if (targets.includes(r)) return [...path, op];
        seen.add(r);
        next.push([r, [...path, op]]);
      }
    }
    frontier = next;
  }
  return null;
}

// ---------- queries ----------

export const cultureOf = (s, floor = s.floor) => s.cultures[floor - 1];
export const alive = (s) => s.enemies.filter((e) => !e.dead);
export const targetsOf = (key) => [...CULTURES[key].death, ...CULTURES[key].lucky];
export const availableOps = (h) => Object.keys(OPS).filter((k) => h.faith >= OPS[k].faith);
export function numberStatus(num, cultureKey) {
  const c = CULTURES[cultureKey];
  if (c.cursed.includes(num)) return 'cursed';
  if (c.blessed.includes(num)) return 'blessed';
  return 'neutral';
}

export function actionsPerTurn(s) {
  const h = s.hero;
  return 1 + (h.faith >= T.extraActionFaith ? 1 : 0) + (numberStatus(h.num, cultureOf(s)) === 'blessed' ? 1 : 0);
}

// What would happen if `op` is applied to enemy `e` right now (for UI preview and bots).
export function preview(s, op, e) {
  const to = applyOp(op, e.num, s.hero.num);
  if (to === null) return { to: null, result: 'invalid' };
  const c = CULTURES[cultureOf(s)];
  const result = c.death.includes(to) ? 'death' : c.lucky.includes(to) ? 'lucky' : 'none';
  return { to, result };
}

// Average moves needed to finish an enemy (values 5..40) with number n in a culture. Lower = easier.
const easeCache = new Map();
export function ease(n, cultureKey) {
  const key = `${n}:${cultureKey}`;
  if (!easeCache.has(key)) {
    const targets = targetsOf(cultureKey);
    let sum = 0;
    for (let v = 5; v <= 40; v++) sum += solve(v, n, BASE_OPS, targets, 8)?.length ?? 9;
    easeCache.set(key, +(sum / 36).toFixed(2));
  }
  return easeCache.get(key);
}

// Expected moves per turn-of-pressure for number n in a culture, including its judgment. Lower = better fit.
export function fit(n, cultureKey) {
  const st = numberStatus(n, cultureKey);
  return +(ease(n, cultureKey) * (st === 'cursed' ? 1.4 : 1) / (st === 'blessed' ? 2 : 1)).toFixed(2);
}

export function forecast(s) {
  const out = [];
  for (let f = s.floor + 1; f <= Math.min(T.floors, s.floor + 2); f++) out.push({ floor: f, culture: s.cultures[f - 1] });
  return out;
}

// ---------- command entry point ----------

export function dispatch(s, cmd) {
  const ev = [];
  const handler = HANDLERS[cmd.type];
  if (!handler) return [{ t: 'invalid', cmd }];
  handler(s, cmd, ev);
  return ev;
}

const HANDLERS = {
  pick(s, { num }, ev) {
    if (s.phase !== 'pick' || !START_NUMBERS.includes(num)) return;
    s.hero = { num, hp: T.heroHp, maxHp: T.heroHp, faith: 0, gold: 0, actions: 1, switches: 0, held: {} };
    ev.push({ t: 'pick', num });
    startFloor(s, ev);
  },

  act(s, { op, id }, ev) {
    if (s.phase !== 'battle') return;
    const h = s.hero;
    const e = alive(s).find((x) => x.id === id);
    if (!e || !availableOps(h).includes(op)) return;
    const to = applyOp(op, e.num, h.num);
    if (to === null) { ev.push({ t: 'invalid', id, op }); return; }
    ev.push({ t: 'op', id, op, from: e.num, to, n: h.num });
    e.num = to;
    resolve(s, e, ev);
    h.actions--;
    if (!alive(s).length) return clearFloor(s, ev);
    if (h.actions <= 0) enemyPhase(s, ev);
  },

  buy(s, { item }, ev) {
    if (s.phase !== 'altar' || !ITEMS[item] || s.altar.bought.includes(item)) return;
    const h = s.hero;
    if (h.gold < ITEMS[item].price) return;
    h.gold -= ITEMS[item].price;
    s.altar.bought.push(item);
    if (item === 'heal') h.hp = Math.min(h.maxHp, h.hp + 15);
    if (item === 'vit') { h.maxHp += 8; h.hp += 8; }
    ev.push({ t: 'buy', item });
  },

  keep(s, _cmd, ev) {
    if (s.phase !== 'altar') return;
    s.hero.faith++;
    ev.push({ t: 'keep', num: s.hero.num, faith: s.hero.faith });
    startFloor(s, ev);
  },

  switch(s, { num }, ev) {
    if (s.phase !== 'altar' || !s.altar.options.includes(num)) return;
    const h = s.hero;
    if (h.faith >= 1) s.pendingBetrayed.push({ num: h.num, faith: h.faith });
    ev.push({ t: 'switch', from: h.num, to: num });
    h.num = num;
    h.faith = Math.floor(h.faith / 2); // switching costs half your faith, and the old number comes back
    h.switches++;
    startFloor(s, ev);
  },
};

// ---------- internals ----------

function resolve(s, e, ev) {
  const c = CULTURES[cultureOf(s)];
  const h = s.hero;
  const isDeath = c.death.includes(e.num);
  const isLucky = c.lucky.includes(e.num);
  if (!isDeath && !isLucky) return;
  if (e.kind === 'boss' && e.phases > 1) {
    e.phases--;
    e.num = rollEnemyNum(s, targetsOf(cultureOf(s)));
    e.timer = e.baseTimer;
    ev.push({ t: 'bossPhase', id: e.id, num: e.num, phases: e.phases });
    return;
  }
  e.dead = true;
  if (isDeath) {
    const gold = T.killGold + s.floor;
    h.gold += gold;
    ev.push({ t: 'kill', id: e.id, gold });
  } else {
    e.blessed = true;
    const heal = Math.min(T.luckyHeal, h.maxHp - h.hp);
    h.hp += heal;
    ev.push({ t: 'bless', id: e.id, heal });
  }
}

function enemyPhase(s, ev) {
  const h = s.hero;
  for (const e of alive(s)) {
    e.timer--;
    if (e.timer > 0) continue;
    e.timer = e.baseTimer;
    h.hp -= e.num;
    ev.push({ t: 'attack', id: e.id, dmg: e.num });
    if (h.hp <= 0) {
      h.hp = 0;
      s.phase = 'end';
      s.ending = 'dead';
      ev.push({ t: 'dead' });
      return;
    }
  }
  h.actions = actionsPerTurn(s);
  s.turn++;
}

// Enemy numbers ignore your number: how well your number fits the floor is the challenge.
function rollEnemyNum(s, targets) {
  const [lo, hi] = T.range[s.floor - 1];
  let v;
  do v = randInt(s, lo, hi); while (targets.includes(v));
  return v;
}

function startFloor(s, ev) {
  s.floor++;
  s.phase = 'battle';
  s.turn = 0;
  s.altar = null;
  const h = s.hero;
  h.actions = actionsPerTurn(s);
  const cursed = numberStatus(h.num, cultureOf(s)) === 'cursed';
  const targets = targetsOf(cultureOf(s));
  const idx = s.floor - 1;
  s.enemies = [];
  const spawn = (props) => {
    // A culture that curses your number makes its enemies strike one turn sooner.
    const baseTimer = Math.max(2, (props.baseTimer ?? randInt(s, ...T.timer[idx])) - (cursed ? 1 : 0));
    s.enemies.push({ id: s.nextId++, kind: 'normal', timer: baseTimer, baseTimer, ...props });
  };

  if (s.floor === T.floors) {
    let bossNum = h.num;
    let best = -1;
    for (const [n, c] of Object.entries(h.held)) if (c > best) { best = c; bossNum = Number(n); }
    spawn({
      kind: 'boss', name: h.switches ? `${bossNum}의 화신` : `${bossNum}의 신`, phases: T.bossPhases,
      num: rollEnemyNum(s, targets), baseTimer: T.bossTimer,
    });
  }
  // A betrayed number takes the place of a regular enemy.
  for (const b of s.pendingBetrayed) {
    // The abandoned number returns as its own multiple, scaled by the faith you gave up.
    let num = b.num * b.faith;
    while (targets.includes(num)) num += b.num;
    spawn({ kind: 'betrayed', name: `배신당한 ${b.num}`, num, baseTimer: T.betrayTimer });
  }
  s.pendingBetrayed = [];
  const count = Math.max(0, T.enemyCount[idx] - (s.enemies.length - (s.floor === T.floors ? 1 : 0)));
  for (let i = 0; i < count; i++) spawn({ num: rollEnemyNum(s, targets) });
  ev.push({ t: 'floor', floor: s.floor, culture: cultureOf(s) });
}

function clearFloor(s, ev) {
  const h = s.hero;
  h.held[h.num] = (h.held[h.num] || 0) + 1;
  s.timeline.push(h.num);
  ev.push({ t: 'clear', floor: s.floor });
  if (s.floor >= T.floors) {
    s.phase = 'end';
    s.ending = h.switches > 0 ? 'wanderer' : 'devoted';
    ev.push({ t: 'win', ending: s.ending });
    return;
  }
  h.hp = Math.min(h.maxHp, h.hp + T.floorHeal);
  s.phase = 'altar';
  const pool = START_NUMBERS.filter((n) => n !== h.num);
  const options = [];
  while (options.length < 3) {
    const n = pick(s, pool);
    if (!options.includes(n)) options.push(n);
  }
  s.altar = { options, bought: [] };
}
