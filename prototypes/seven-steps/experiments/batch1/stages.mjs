// Batch 1 (stages 1-10 of design/levels/stage-plan-50.md) with the current rules (core.js re-exports src/core.js).
// Body budget 3 everywhere (triplets). Run: node prototypes/seven-steps/experiments/run.mjs batch1
import { LEVELS } from '../../src/levels.js';
const old = (n, extra = {}) => ({ ...LEVELS[n - 1], bodies: 3, ...extra });

export const STAGES = [
  old(1),                                     // 1 일곱 걸음 (keep 1)
  old(2),                                     // 2 늦게 도착하기 (keep 2)
  {
    name: '딱 맞는 길',                        // 3 new: exit exactly 7 away, one route, two dead ends; no slack to pad
    hint: '출구까지 꼭 7걸음인 길은 하나뿐이다.',
    bodies: 3,
    map: [
      '########',
      '#S..#..#',
      '#.#.#.##',
      '#.#...E#',
      '#...####',
      '########',
    ],
  },
  old(3, { gimmickTiles: 'aA' }),             // 4 남는 형제 (keep 3 돌이 된 몸)
  {
    name: '가까운 발판',                        // 5 new: two plates; the near one is an even distance away, so no 7th step ends there
    hint: '발판이 둘. 7번째 걸음으로 설 수 있는 쪽은 어디일까?',
    bodies: 3,
    gimmickTiles: 'aAbB',
    map: [
      '########',
      '#b.S..a#',
      '##B#A###',
      '##.#.###',
      '##.E.###',
      '########',
    ],
  },
  {
    name: '길을 막는 선돌',                     // 6 new: the statue on the plate blocks the short way; go round
    hint: '선돌은 문을 열지만, 그 자리를 막기도 한다.',
    bodies: 3,
    gimmickTiles: 'aA',
    map: [
      '######',
      '#SaAE#',
      '#.#.##',
      '#...##',
      '######',
    ],
  },
  old(12, {                                   // 7 두 개의 문 (edit 12: smaller board, D 0.50 -> 0.45 for the chapter 1 finale)
    gimmickTiles: 'aAbB',
    map: [
      '#######',
      '#aS..b#',
      '##.####',
      '##A####',
      '##B####',
      '##.E###',
      '#######',
    ],
  }),
  old(4),                                     // 8 새 출발점 (keep 4, budget 2 -> 3)
  {
    name: '바통 넘기기',                        // 9 new: 21 steps away, hand over twice
    hint: '혼자서는 닿지 않는다. 형제에게 7걸음째 자리를 넘겨 잇자.',
    bodies: 3,
    map: [
      '############',
      '#S.........#',
      '##########.#',
      '#.E........#',
      '############',
    ],
  },
  old(5, { gimmickTiles: 'o' }),              // 10 몸으로 놓는 다리 (keep 5)
];
