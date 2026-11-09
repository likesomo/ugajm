// Batch 3 (stages 21-30 of design/levels/stage-plan-50.md), current rules, body budget 3.
// Run: node prototypes/seven-steps/experiments/run.mjs batch3
import { LEVELS } from '../levels-v17.mjs';
const old = (n, extra = {}) => ({ ...LEVELS[n - 1], bodies: 3, ...extra });

export const STAGES = [
  {
    name: '새김돌의 계산',                     // 21 new (stage-search gate-plate, seed 95 #1): keep on the plate past the 5 door, hand over, walk through A
    hint: '선돌 하나, 바통 하나, 숫자 문 하나. 세 형제의 걸음을 모두 세자.',
    bodies: 3,
    gimmickTiles: '1234567aA',
    map: [
      '##########',
      '#E..#....#',
      '#.##....##',
      '#...A##5a#',
      '#..#S....#',
      '##########',
    ],
  },
  {
    name: '미끄러운 첫걸음',                    // 22 new: the slide is one step; on floor alone the exit is 12 away
    hint: '얼음은 몇 칸을 미끄러져도 한 걸음.',
    bodies: 3,
    gimmickTiles: '~',
    map: [
      '#########',
      '#S~~~~~.#',
      '#######.#',
      '###E....#',
      '#########',
    ],
  },
  old(7, { gimmickTiles: '~' }),               // 23 홀수의 저주 (keep 7)
  {
    name: '얼음 위의 숫자',                     // 24 new (stage-search gate-ice, seed 82 #1): slides and numbered doors, two brothers
    hint: '미끄럼 한 번도 한 걸음. 숫자 문은 몇 번째에?',
    bodies: 3,
    gimmickTiles: '~1234567',
    map: [
      '#########',
      '#E2...4##',
      '#...#.S.#',
      '#.....~~#',
      '#..#~~~~#',
      '#########',
    ],
  },
  old(9, { gimmickTiles: '~', name: '멈춰 세우는 선돌' }), // 25 멈춰 세우는 선돌 (keep 9, renamed from 석상 2026-11-10)
  {
    name: '미끄럼을 막는 형제', // 26 polish (stage-search ice-statue --strict on a skeleton, seed 133): the statue stops the slide; 2 bodies, D 0.56 -> 0.40
    hint: '석상은 미끄럼을 멈추지만 길도 막는다. 어디에 세울까?',
    bodies: 3,
    gimmickTiles: '~',
    map: [
      '##########',
      '#...E#...#',
      '#.##.#.#.#',
      '#~~.~....#',
      '#.#.##.#.#',
      '#...#S...#',
      '##########',
    ],
  },
  {
    name: '얼음 끝의 구멍',                     // 27 new (stage-search ice-pit, seed 96 #3): slide onto the pit on the 7th step, the next crosses
    hint: '미끄러져 구멍에 닿는 순간이 7번째라면?',
    bodies: 3,
    gimmickTiles: '~o',
    map: [
      '########',
      '#E#...##',
      '#.#.#.##',
      '#o.#~S##',
      '#...~..#',
      '#..~~.##',
      '########',
    ],
  },
  {
    name: '서리의 길목',                // 28 new (stage-search ice-plate, seed 91 #2): reach the plate by sliding, then the next goes through the door
    hint: '발판도 문도 얼음 너머에.',
    bodies: 3,
    gimmickTiles: '~aA',
    map: [
      '########',
      '##a...##',
      '#S~~#..#',
      '#......#',
      '##.###.#',
      '#..AE#.#',
      '########',
    ],
  },
  old(8, { gimmickTiles: 'LR' }),              // 29 내리는 벽 (keep 8)
  {
    name: '열면 닫힌다',                        // 30 exp lever sketch: the lever opens one way and closes another
    hint: '레버는 모든 벽을 뒤집는다. 열린 길도 닫힌다.',
    bodies: 3,
    gimmickTiles: 'LRr',
    map: [
      '##########',
      '#S..r..L.#',
      '#.##R#####',
      '#...R..E##',
      '##########',
    ],
  },
];
