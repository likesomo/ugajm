// All tunable game data. Core logic reads only from here.

export const NUMBERS = {
  3: {
    name: '삼세판 전사', hp: 24, atk: 3,
    desc: '쓰러져도 한 번 부활합니다. 서양의 Three on a match 미신에 저주받습니다.',
    ult: '삼연격', ultDesc: '모든 적을 3번 공격',
    imprint: { maxHp: 10 }, imprintText: '최대 HP +10',
  },
  7: {
    name: '도박사', hp: 26, atk: 4,
    desc: '공격마다 1/6 확률로 잭팟(피해 ×3). 동양의 귀신의 달(七月)엔 저주받습니다.',
    ult: '777 잭팟', ultDesc: '모든 적에게 7 피해 ×3회',
    imprint: { jackpot: 0.12 }, imprintText: '잭팟 확률 +12%',
  },
  8: {
    name: '상인 發', hp: 28, atk: 4,
    desc: '처치 골드 2배. 수학 미궁의 짝수 숙청에 저주받습니다.',
    ult: '八方', ultDesc: '무작위 적에게 8연타',
    imprint: { gold: 0.5 }, imprintText: '골드 +50%',
  },
  13: {
    name: '체의 생존자', hp: 22, atk: 5,
    desc: '13번째 공격마다 즉사기. 서양의 13일의 금요일에 저주받습니다.',
    ult: 'Ulam의 체', ultDesc: '짝수 자리의 적 처형, 나머지 피해',
    imprint: { atk: 2 }, imprintText: '공격력 +2',
  },
};

// Ulam lucky numbers (sieve survivors).
export const ULAM = [1, 3, 7, 9, 13, 15, 21, 25, 31, 33, 37, 43, 49];

export const CULTURES = {
  west: {
    name: '서양 대성당', icon: '✝', blessed: [7], cursed: [3, 13], feared: [13], combo: 7,
    rule: '7은 축복. 13(13일의 금요일)과 3(Three on a match)은 저주. 합이 7이면 연쇄.',
  },
  east: {
    name: '동양 사원', icon: '☯', blessed: [3, 8], cursed: [7], feared: [4], combo: 8,
    rule: '8(發)과 3(삼세판)은 축복, 7은 귀신의 달. 적 4는 겁먹음. 합이 8이면 연쇄.',
  },
  math: {
    name: '수학 미궁', icon: '∑', blessed: [3, 7, 13], cursed: [8], feared: [], combo: null,
    rule: 'Ulam의 체 생존자(1·3·7·9·13…)는 축복, 짝수는 숙청. 적이 행운수면 연쇄.',
  },
};

// Base prices end in 3/7/8 so each lucky number gets a discount on one item.
export const ITEMS = {
  heal: { name: '회복 물약', price: 13, desc: 'HP +12' },
  atk: { name: '숫돌', price: 17, desc: '공격력 +1' },
  vit: { name: '부적', price: 18, desc: '최대 HP +6' },
};

export const TUNING = {
  floors: 7,
  ultFaith: 3,
  faithAtk: 1,
  blessMult: 1.5,
  curseMult: 1.5,
  curseAtkMult: 0.7,
  critDiv: 8, // crit multiplier = 1 + num / critDiv: big numbers divide rarely but hit hard
  jackpotMult: 3,
  jackpotChance7: 1 / 6,
  comboSplash: 0.5,
  executePct: 0.25,
  sieveCount: 13,
  dyePerFaith: 0.15,
  dyeMax: 0.6,
  sameChance: 0.1,
  fearedChance: 0.12,
  maxAllies: 3,
  ehpBase: 4, ehpFloor: 2,
  eatkBase: 1, eatkFloorDiv: 2,
  bossHp: 60, bossHpPerHeld: 6, godMult: 1.3, bossAtk: 5,
  betrayHp: 6, betrayHpFaith: 5, betrayAtk: 1, betrayAtkFaith: 0.5, betrayMinFaith: 2,
  floorHeal: 8,
  reviveFrac: 0.5,
  priceStep: 10,
};
