// Batch 4 (stages 31-40 of design/levels/stage-plan-50.md), current rules, body budget 3.
// Run: node prototypes/seven-steps/experiments/run.mjs batch4
import { LEVELS } from '../../src/levels.js';
const old = (n, extra = {}) => ({ ...LEVELS[n - 1], bodies: 3, ...extra });

export const STAGES = [
  old(10, { gimmickTiles: 'r' }),             // 31 올려서 멈추기 (keep 10)
  {
    name: '레버와 바통', // 32 new (stage-search lever, seed 102 #1): pull, hand over, walk through the lowered wall
    hint: '레버를 당긴 형제 다음은 어디서 출발할까?',
    bodies: 3,
    gimmickTiles: 'LRr',
    map: [
      '##########',
      '#ER.#.#.##',
      '##...S#..#',
      '#.....L..#',
      '#R...#r.##',
      '##########',
    ],
  },
  {
    name: '레버와 숫자', // 33 new (stage-search lever-gate, seed 101 #1): the wall comes down; count the new way to the numbered doors
    hint: '벽이 내려간 뒤의 길이로 숫자를 맞추자.',
    bodies: 3,
    gimmickTiles: 'LRr1234567',
    map: [
      '########',
      '#....3R#',
      '#.#..#E#',
      '#.#S#L##',
      '#...#..#',
      '#6.....#',
      '########',
    ],
  },
  {
    name: '레버와 발판', // 34 new (stage-search lever-plate, seed 103 #1): a lever and a plate, two brothers spent before the third walks
    hint: '레버는 한 번, 발판은 계속. 둘 다 필요하다.',
    bodies: 3,
    gimmickTiles: 'LRraA',
    map: [
      '##########',
      '#.#..a..##',
      '#E#...#R.#',
      '#...###R.#',
      '#.##.##..#',
      '#...A.LS##',
      '##########',
    ],
  },
  {
    name: '결구의 장치', // 35 new (stage-search lever-ice2, seed 109 #1): chapter 5 finale, lever + ice + statue + hand-over
    hint: '레버, 얼음, 선돌, 바통. 결구의 행성을 빠져나가자.',
    bodies: 3,
    gimmickTiles: 'LRr~',
    map: [
      '#########',
      '#..L...##',
      '#.S..##E#',
      '##..#...#',
      '#.~R....#',
      '#..#r##.#',
      '#########',
    ],
  },
  old(11, { gimmickTiles: 'xaA' }),          // 36 한 번만 버티는 바닥 (keep 11)
  {
    name: '무너진 길 위의 구멍', // 37 new (stage-search cracked-pit, seed 105 #0): cracked floor and a pit close the short ways
    hint: '지나간 바닥은 무너진다. 돌아올 길을 남겨 두자.',
    bodies: 3,
    gimmickTiles: 'xo',
    map: [
      '###########',
      '##E#.#xS..#',
      '##....###.#',
      '#...#o....#',
      '##.....#..#',
      '###########',
    ],
  },
  old(13, { gimmickTiles: 'Q' }),            // 38 하나만 남겨라 (keep 13)
  {
    name: '열쇠가 되는 형제', // 39 redo 14: the first brother stands as the key in a dead end so the count door opens for the second
    hint: '문을 열 선돌은 어디서 구하지?',
    bodies: 3,
    statueCount: 1,
    gimmickTiles: 'Q',
    map: [
      '########',
      '#.######',
      '#S.Q..a#',
      '#.######',
      '#.A...E#',
      '########',
    ],
  },
  old(15, { gimmickTiles: 'QaA' }),          // 40 남기기 전에 지나가라 (edit 15: budget 4 -> 3)
];
