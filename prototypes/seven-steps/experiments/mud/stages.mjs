// Mud sketches (experiment rules: experiments/mud/core.js — entering mud costs 2 steps, not enterable on the 7th).
// Run: node prototypes/seven-steps/experiments/run.mjs mud
export const STAGES = [
  {
    name: '진흙 소개: 한 칸에 두 걸음',
    hint: '진흙은 한 칸에 두 걸음이 든다.',
    bodies: 3,
    gimmickTiles: 'm',
    // 6 cells away: floor alone is always even (6, 8…), one mud makes it 7.
    map: [
      '#########',
      '#S..m..E#',
      '#########',
    ],
  },
  {
    name: '진흙과 숫자 문 (탐색 후보 mud-gate #1)',
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
    name: '진흙과 얼음 (탐색 후보 mud-ice #2)',
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
];
