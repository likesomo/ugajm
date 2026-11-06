// All tunable game data. Core logic reads only from here.

export const START_NUMBERS = [3, 4, 7, 8, 9, 13];

export const NUMBERS = {
  3: { name: '삼세판', desc: '작고 다루기 쉬운 수. 한국의 삼세판, 수학의 행운수.' },
  4: { name: '死', desc: '동양이 꺼리는 수. 하지만 적을 4로 만들기엔 이만한 게 없습니다.' },
  7: { name: '럭키 세븐', desc: '서양의 행운수. 적을 7로 만들어 축복을 끌어냅니다.' },
  8: { name: '發', desc: '동양의 길수. 적 12에서 8을 빼면 4(死).' },
  9: { name: '久', desc: '중국에선 장수, 일본에선 고통. 9의 배수는 수비학으로 9가 됩니다.' },
  13: { name: '13일의 금요일', desc: '서양의 저주수, 수학의 행운수. 크고 거칠게 움직입니다.' },
};

// death: turning an enemy into this number destroys it.
// lucky: turning an enemy into this number blesses it away and heals you.
// blessed/cursed: the culture's judgment of YOUR number (+1 action / enemies attack 1 turn sooner).
export const CULTURES = {
  west: {
    name: '서양 대성당', icon: '✝', death: [13], lucky: [7], blessed: [7], cursed: [13],
    rule: '13(13일의 금요일)으로 만들면 소멸, 7(럭키 세븐)이면 축복.',
  },
  east: {
    name: '동양 사원', icon: '☯', death: [4, 14], lucky: [8], blessed: [8, 9], cursed: [4, 7],
    rule: '4(死)·14(要死)로 만들면 소멸, 8(發)이면 축복.',
  },
  math: {
    name: '수학 미궁', icon: '∑', death: [0], lucky: [1], blessed: [3, 7, 9, 13], cursed: [4, 8],
    rule: '0으로 만들면 소멸, 1(단위원)이면 축복.',
  },
};

// faith: KEEP count required to unlock. unary ops ignore your number.
export const OPS = {
  inc: { sym: '+1', faith: 0, key: 'z', unary: true, name: '맨손' },
  dec: { sym: '−1', faith: 0, key: 'x', unary: true, name: '맨손' },
  add: { sym: '+', faith: 0, key: 'q' },
  sub: { sym: '−', faith: 0, key: 'w' },
  mul: { sym: '×', faith: 0, key: 'e' },
  div: { sym: '÷', faith: 0, key: 'r' },
  mod: { sym: '%', faith: 1, key: 't', name: '나머지' },
  digit: { sym: 'Σ', faith: 2, key: 'y', unary: true, name: '수비학' },
};

export const ITEMS = {
  heal: { name: '회복 물약', price: 12, desc: 'HP +15' },
  vit: { name: '부적', price: 18, desc: '최대 HP +8' },
};

export const TUNING = {
  floors: 7,
  heroHp: 40,
  maxValue: 999,
  extraActionFaith: 3,
  betrayTimer: 3,
  luckyHeal: 6,
  floorHeal: 6,
  killGold: 4,
  bossPhases: 3,
  bossTimer: 3,
  solverDepth: 4,
  // per floor (index = floor - 1)
  enemyCount: [2, 3, 3, 3, 4, 4, 1],
  range: [[5, 20], [5, 25], [8, 30], [8, 35], [10, 40], [10, 45], [15, 50]],
  timer: [[3, 4], [3, 4], [3, 4], [3, 4], [3, 4], [3, 4], [3, 4]],
};
