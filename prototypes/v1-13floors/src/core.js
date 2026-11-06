// Pure game core. Every state change goes through dispatch(state, cmd).
// No DOM, no Date, no Math.random: time and seed come from outside.
import { nextRandom, randInt, pick } from './rng.js';
import { NUMBERS, CULTURES, ULAM, ITEMS, TUNING as T } from './data.js';

export const NUMBER_IDS = [3, 7, 8, 13];

export function newGame(seed) {
  const s = {
    seed: seed >>> 0, rng: seed >>> 0, phase: 'pick', floor: 0,
    cultures: [], hero: null, enemies: [], allies: [], nextId: 1,
    pendingBetrayed: [], timeline: [], altar: null, ending: null, turn: 0,
  };
  const keys = Object.keys(CULTURES);
  for (let i = 0; i < T.floors; i++) s.cultures.push(pick(s, keys));
  return s;
}

// ---------- queries (safe for UI and bots) ----------

export const cultureOf = (s, floor = s.floor) => s.cultures[floor - 1];
export const alive = (s) => s.enemies.filter((e) => !e.dead);

export function numberStatus(num, cultureKey) {
  const c = CULTURES[cultureKey];
  if (c.cursed.includes(num)) return 'cursed';
  if (c.blessed.includes(num)) return 'blessed';
  return 'neutral';
}

export function forecast(s) {
  const out = [];
  for (let f = s.floor + 1; f <= Math.min(T.floors, s.floor + 2); f++) {
    out.push({ floor: f, culture: s.cultures[f - 1] });
  }
  return out;
}

const imprintSum = (h, key) => h.imprints.reduce((a, n) => a + (NUMBERS[n].imprint[key] || 0), 0);

export const maxHpOf = (h) => NUMBERS[h.num].hp + h.maxHpBonus + imprintSum(h, 'maxHp');
export const jackpotChance = (h) => (h.num === 7 ? T.jackpotChance7 : 0) + imprintSum(h, 'jackpot');
export const critMult = (h) => 1 + h.num / T.critDiv;
export const goldMult = (h) => (h.num === 8 ? 2 : 1) + imprintSum(h, 'gold');

export function heroAtk(s) {
  const h = s.hero;
  let a = NUMBERS[h.num].atk + h.atkBonus + imprintSum(h, 'atk') + h.faith * T.faithAtk;
  const status = s.phase === 'battle' ? numberStatus(h.num, cultureOf(s)) : 'neutral';
  if (status === 'blessed') a *= T.blessMult;
  if (status === 'cursed') a *= T.curseAtkMult;
  return a;
}

export function relation(s, e) {
  const h = s.hero;
  const cul = CULTURES[cultureOf(s)];
  const crit = e.num !== h.num && e.num % h.num === 0;
  if (e.kind !== 'normal') return { crit: e.num % h.num === 0, combo: false, recruit: false, fear: false };
  return {
    recruit: e.num === h.num && s.allies.length < T.maxAllies,
    crit,
    combo: cul.combo ? h.num + e.num === cul.combo : ULAM.includes(e.num),
    fear: cul.feared.includes(e.num),
  };
}

export function itemPrice(s, key) {
  const base = ITEMS[key].price + T.priceStep * Math.floor((s.floor - 1) / 3);
  return base % 10 === s.hero.num % 10 ? Math.ceil(base / 2) : base;
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
    if (s.phase !== 'pick' || !NUMBER_IDS.includes(num)) return;
    s.hero = {
      num, hp: 0, maxHpBonus: 0, atkBonus: 0, gold: 0, faith: 0,
      imprints: [], revives: num === 3 ? 1 : 0, counter13: 0, ultCharge: 0,
      held: {}, switches: 0,
    };
    s.hero.hp = maxHpOf(s.hero);
    ev.push({ t: 'pick', num });
    startFloor(s, ev);
  },

  attack(s, { id }, ev) {
    if (s.phase !== 'battle') return;
    const e = alive(s).find((x) => x.id === id);
    if (!e) return;
    const h = s.hero;
    const rel = relation(s, e);
    if (rel.recruit) {
      e.dead = true;
      e.recruited = true;
      s.allies.push({ num: e.num, atk: Math.max(2, e.atk) });
      ev.push({ t: 'recruit', id: e.id, num: e.num });
    } else {
      let dmg = heroAtk(s);
      const tags = [];
      if (rel.crit) { dmg *= critMult(h); tags.push('crit'); }
      const jc = jackpotChance(h);
      if (jc > 0 && nextRandom(s) < jc) { dmg *= T.jackpotMult; tags.push('jackpot'); }
      if (h.num === 13 && ++h.counter13 >= T.sieveCount) {
        h.counter13 = 0;
        tags.push('sieve');
        dmg = e.kind === 'normal' ? Math.max(dmg, e.hp) : dmg + e.maxHp * T.executePct;
      }
      dmg = Math.round(dmg);
      hit(s, e, dmg, tags, ev);
      if (rel.combo) {
        for (const o of alive(s)) if (o !== e) hit(s, o, Math.max(1, Math.round(dmg * T.comboSplash)), ['combo'], ev);
      }
    }
    endTurn(s, ev);
  },

  ult(s, _cmd, ev) {
    if (s.phase !== 'battle' || s.hero.ultCharge < 1) return;
    const h = s.hero;
    h.ultCharge--;
    ev.push({ t: 'ult', num: h.num });
    const base = Math.round(heroAtk(s));
    if (h.num === 3) {
      for (let i = 0; i < 3; i++) for (const e of alive(s)) hit(s, e, base, ['ult'], ev);
    } else if (h.num === 7) {
      for (let i = 0; i < 3; i++) for (const e of alive(s)) hit(s, e, 7, ['ult', 'jackpot'], ev);
    } else if (h.num === 8) {
      for (let i = 0; i < 8; i++) {
        const targets = alive(s);
        if (!targets.length) break;
        hit(s, pick(s, targets), Math.max(1, Math.round(base * 0.6)), ['ult'], ev);
      }
    } else if (h.num === 13) {
      alive(s).forEach((e, i) => {
        if (i % 2 === 1) hit(s, e, e.kind === 'normal' ? e.hp : Math.round(e.maxHp * T.executePct), ['ult', 'sieve'], ev);
        else hit(s, e, base, ['ult'], ev);
      });
    }
    endTurn(s, ev);
  },

  buy(s, { item }, ev) {
    if (s.phase !== 'altar' || !ITEMS[item] || s.altar.bought.includes(item)) return;
    const h = s.hero;
    const price = itemPrice(s, item);
    if (h.gold < price) return;
    h.gold -= price;
    s.altar.bought.push(item);
    if (item === 'heal') h.hp = Math.min(maxHpOf(h), h.hp + 12);
    if (item === 'atk') h.atkBonus += 1;
    if (item === 'vit') { h.maxHpBonus += 6; h.hp += 6; }
    ev.push({ t: 'buy', item, price });
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
    const old = h.num;
    if (h.faith >= T.betrayMinFaith) s.pendingBetrayed.push({ num: old, faith: h.faith });
    if (!h.imprints.includes(old)) h.imprints.push(old);
    h.num = num;
    h.faith = 0;
    h.switches++;
    h.revives = num === 3 ? 1 : 0;
    h.counter13 = 0;
    h.hp = Math.min(h.hp, maxHpOf(h));
    ev.push({ t: 'switch', from: old, to: num });
    startFloor(s, ev);
  },
};

// ---------- internals ----------

function startFloor(s, ev) {
  s.floor++;
  s.phase = 'battle';
  s.turn = 0;
  s.altar = null;
  const h = s.hero;
  h.ultCharge = h.faith >= T.ultFaith ? 1 : 0;
  s.enemies = [];
  if (s.floor === T.floors) {
    spawnBoss(s);
  } else {
    // A betrayed number takes the place of a regular enemy instead of stacking on top.
    const n = Math.max(1, 2 + Math.floor(s.floor / 2) - s.pendingBetrayed.length);
    for (let i = 0; i < n; i++) s.enemies.push(makeEnemy(s, rollEnemyNum(s)));
  }
  for (const b of s.pendingBetrayed) {
    s.enemies.push({
      id: s.nextId++, kind: 'betrayed', num: b.num, name: `배신당한 ${b.num}`,
      maxHp: T.betrayHp + b.faith * T.betrayHpFaith + s.floor * 2,
      atk: T.betrayAtk + Math.ceil(b.faith * T.betrayAtkFaith) + Math.floor(s.floor / 2),
    });
  }
  s.pendingBetrayed = [];
  for (const e of s.enemies) if (e.hp === undefined) e.hp = e.maxHp;
  ev.push({ t: 'floor', floor: s.floor, culture: cultureOf(s) });
}

function rollEnemyNum(s) {
  const h = s.hero;
  const cul = CULTURES[cultureOf(s)];
  const r = nextRandom(s);
  // KEEP spreads your number through the world: more multiples of it appear.
  const dye = Math.min(T.dyeMax, h.faith * T.dyePerFaith);
  if (r < dye) return h.num * randInt(s, 2, Math.max(2, Math.floor(40 / h.num)));
  if (r < dye + T.sameChance) return h.num;
  if (r < dye + T.sameChance + T.fearedChance && cul.feared.length) return pick(s, cul.feared);
  return randInt(s, 1, 20);
}

function makeEnemy(s, num) {
  const h = s.hero;
  let atk = T.eatkBase + Math.floor(s.floor / T.eatkFloorDiv);
  // Cultures that curse your number push back harder the more you believe in it.
  if (numberStatus(h.num, cultureOf(s)) === 'cursed') atk += Math.ceil(h.faith / 2);
  return { id: s.nextId++, kind: 'normal', num, maxHp: T.ehpBase + s.floor * T.ehpFloor + Math.floor(num / 4), atk };
}

function spawnBoss(s) {
  const h = s.hero;
  let bossNum = h.num;
  let best = -1;
  for (const [n, c] of Object.entries(h.held)) if (c > best) { best = c; bossNum = Number(n); }
  const devoted = h.switches === 0;
  const hp = Math.round((T.bossHp + T.bossHpPerHeld * Math.max(0, best)) * (devoted ? T.godMult : 1));
  s.enemies.push({
    id: s.nextId++, kind: 'boss', num: bossNum,
    name: devoted ? `${bossNum}의 신` : `${bossNum}의 화신`, maxHp: hp, atk: T.bossAtk,
  });
  for (let i = 0; i < 2; i++) s.enemies.push(makeEnemy(s, rollEnemyNum(s)));
}

function hit(s, e, dmg, tags, ev) {
  if (e.dead) return;
  e.hp = Math.max(0, e.hp - dmg);
  ev.push({ t: 'hit', id: e.id, dmg, tags });
  if (e.hp === 0) {
    e.dead = true;
    const gold = Math.round(e.num * goldMult(s.hero));
    s.hero.gold += gold;
    ev.push({ t: 'kill', id: e.id, gold });
  }
}

function endTurn(s, ev) {
  if (!alive(s).length) return clearFloor(s, ev);
  for (const a of s.allies) {
    const t = alive(s)[0];
    if (!t) break;
    hit(s, t, a.atk, ['ally'], ev);
  }
  if (!alive(s).length) return clearFloor(s, ev);

  const h = s.hero;
  const cul = CULTURES[cultureOf(s)];
  const cursed = numberStatus(h.num, cultureOf(s)) === 'cursed';
  for (const e of alive(s)) {
    let d = e.atk;
    if (e.kind === 'normal' && cul.feared.includes(e.num)) d = Math.ceil(d / 2);
    if (cursed) d = Math.ceil(d * T.curseMult);
    h.hp -= d;
    ev.push({ t: 'hurt', id: e.id, dmg: d });
    if (h.hp <= 0) {
      if (h.revives > 0) {
        h.revives--;
        h.hp = Math.ceil(maxHpOf(h) * T.reviveFrac);
        ev.push({ t: 'revive' });
      } else {
        h.hp = 0;
        s.phase = 'end';
        s.ending = 'dead';
        ev.push({ t: 'dead' });
        return;
      }
    }
  }
  s.turn++;
}

function clearFloor(s, ev) {
  const h = s.hero;
  h.held[h.num] = (h.held[h.num] || 0) + 1;
  s.timeline.push(h.num);
  ev.push({ t: 'clear', floor: s.floor });
  if (s.floor >= T.floors) {
    s.phase = 'end';
    s.ending = h.switches > 0 ? 'wanderer' : h.num === 13 ? 'cursed' : 'devoted';
    ev.push({ t: 'win', ending: s.ending });
    return;
  }
  h.hp = Math.min(maxHpOf(h), h.hp + T.floorHeal);
  s.phase = 'altar';
  s.altar = { options: NUMBER_IDS.filter((n) => n !== h.num), bought: [] };
}
