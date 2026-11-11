// Batch 5 (stages 41-50 of design/levels/stage-plan-50.md), current rules (mud is in core.js since 6ae58e2), body budget 3.
// Run: node prototypes/seven-steps/experiments/run.mjs batch5
export const STAGES = [
  {
    name: '선돌 아닌 흔적', // 41 new (stage-search qdoor-cracked on a skeleton, seed 121): a seal is not a statue, so the 2 door stays shut; go round
    hint: '굳은 바닥은 길이 되지만 선돌로 세지 않는다.',
    bodies: 3,
    statueCount: 2,
    gimmickTiles: 'Qx',
    map: [
      '##########',
      '#....#...#',
      '#.##x#.#.#',
      '#.SQ.....#',
      '#.#.##.#.#',
      '#...#...E#',
      '##########',
    ],
  },
  {
    name: '무너지는 계산', // 42 new (stage-search qdoor-cracked-pit on a skeleton, seed 122): chapter 6 finale; a bridge is not a statue either
    hint: '다리도 굳은 바닥도 선돌이 아니다. 문은 몇을 기다릴까?',
    bodies: 3,
    statueCount: 1,
    gimmickTiles: 'Qxo',
    map: [
      '#########',
      '#.......#',
      '#.##.##.#',
      '#..x..x.#',
      '#o##.##.#',
      '#.E.Q.S.#',
      '#########',
    ],
  },
  {
    name: '한 칸에 두 걸음', // 43 new (experiments/mud 1): six cells away, one mud makes it seven
    hint: '진흙은 한 칸에 두 걸음이 든다.',
    bodies: 3,
    gimmickTiles: 'm',
    map: [
      '#########',
      '#S..m..E#',
      '#########',
    ],
  },
  {
    name: '진흙과 숫자', // 44 new (experiments/mud 2): mud skips a number on the way to the doors
    hint: '진흙을 지나면 숫자가 하나 건너뛴다.',
    bodies: 3,
    gimmickTiles: 'm',
    map: [
      '########',
      '##..m..#',
      '####.#.#',
      '#...#.S#',
      '#E#3##5#',
      '##....m#',
      '#......#',
      '########',
    ],
  },
  {
    name: '얼음과 진흙', // 45 new (experiments/mud 3): many cells for one step, one cell for two
    hint: '얼음은 여러 칸에 한 걸음, 진흙은 한 칸에 두 걸음.',
    bodies: 3,
    gimmickTiles: 'm',
    map: [
      '#########',
      '#E.#..###',
      '#..#...##',
      '##~..#.##',
      '#.~..#m.#',
      '#.~#.m..#',
      '#.~#.S..#',
      '#########',
    ],
  },
  {
    name: '칠칠의 방', // 46 redo 16 (stage-search mix on a skeleton, seed 127): pit, plate, number door and lever with three brothers
    hint: '다리, 발판, 숫자 문, 레버. 몸은 셋.',
    bodies: 3,
    gimmickTiles: 'oaA1234567LRr',
    map: [
      '#########',
      '#.......#',
      '#a#S#.#.#',
      '#R.A.o..#',
      '#.#.#.#.#',
      '#..LE.6.#',
      '#########',
    ],
  },
  {
    name: '레버와 선돌의 수', // 47 new (stage-search lever-qdoor on a skeleton, seed 123): pull the lever, but a statue shuts the 0 door
    hint: '레버를 당긴 형제가 선돌로 남으면 0 문이 닫힌다.',
    bodies: 3,
    statueCount: 0,
    gimmickTiles: 'LRrQ',
    map: [
      '#########',
      '#S......#',
      '#.##.##.#',
      '#Q..R.E.#',
      '#.##.##.#',
      '#.L.....#',
      '#########',
    ],
  },
  {
    name: '길이 된 형제들', // 48 new (stage-search cracked-pit on a skeleton, seed 126): one brother seals the cracked floor, one bridges the pit
    hint: '금 간 바닥은 지나가면 무너지고, 7번째 걸음으로 서면 굳는다.',
    bodies: 3,
    gimmickTiles: 'xo',
    map: [
      '##########',
      '#..Sx#.x.#',
      '#.##.#.#.#',
      '#.....o..#',
      '#.#o##.#.#',
      '#...#.E..#',
      '##########',
    ],
  },
  {
    name: '일곱의 일곱', // 49 new (stage-search seven on a 7x7 skeleton, seed 125): mud and a number door in a 7x7 hall
    hint: '일곱 칸 곱하기 일곱 칸. 전부 세자.',
    bodies: 3,
    gimmickTiles: 'm1234567',
    map: [
      '#########',
      '#.S.....#',
      '#.#.#.#.#',
      '#.......#',
      '#.#.#.#.#',
      '#.m..2.E#',
      '#.#.#.#.#',
      '#.......#',
      '#########',
    ],
  },
  {
    name: '원형 제단', // 50 new (ring skeleton search): two brothers stand as menhirs (one on the plate), the third steps onto the altar on the 7th
    hint: '제단의 문은 서 있는 선돌 수를 센다.',
    bodies: 3,
    statueCount: 2,
    gimmickTiles: 'QaA~m',
    map: [
      '#########',
      '###a..###',
      '##~....##',
      '#..###..#',
      '#..#E#..#',
      '#.S#Q#m.#',
      '##..~..##',
      '###..A###',
      '#########',
    ],
  },
];
