// Batch 2 (stages 11-20 of design/levels/stage-plan-50.md), current rules, body budget 3.
// Run: node prototypes/seven-steps/experiments/run.mjs batch2
import { LEVELS } from '../levels-v17.mjs';
const old = (n, extra = {}) => ({ ...LEVELS[n - 1], bodies: 3, ...extra });

export const STAGES = [
  {
    name: '다리냐 선돌이냐',                    // 11 new: one brother bridges, one keeps the plate, one walks through the door
    hint: '형제 하나는 다리, 하나는 발판. 누가 어디로?',
    bodies: 3,
    gimmickTiles: 'oaA',
    map: [
      '########',
      '#S..o.a#',
      '#####A##',
      '#####.##',
      '#####E##',
      '########',
    ],
  },
  {
    name: '건너편에서 깨어나기',                 // 12 new: bridge, cross and SWITCH on the far side, then the last one walks
    hint: '다리를 건넌 형제가 그 자리에서 바통을 넘긴다.',
    bodies: 3,
    gimmickTiles: 'o',
    map: [
      '#######',
      '#S..o.#',
      '#####.#',
      '###E..#',
      '#######',
    ],
  },
  {
    name: '구멍 둘, 형제 셋',                   // 13 new: two pits, two bridges, the third walks
    hint: '다리가 된 형제는 다시 걷지 못한다. 형제는 셋.',
    bodies: 3,
    gimmickTiles: 'o',
    map: [
      '######',
      '#.####',
      '#So.o#',
      '####.#',
      '####E#',
      '######',
    ],
  },
  {
    name: '떠나온 자리',                       // 14 edit 17: corridor shortened so 3 bodies are enough
    hint: '출발점을 옮기면 돌아오는 길도 멀어진다.',
    bodies: 3,
    gimmickTiles: 'aA',
    map: [
      '#################',
      '#E.A...S.......a#',
      '#################',
    ],
  },
  old(6, { gimmickTiles: '35' }),              // 15 숫자 문 (keep 6 for now; the playtest decides the polish, playtest-guide 7.2)





  {
    name: '문 너머의 발판',                     // 18 new: through the 3 door on the 3rd step, keep on the plate, the next goes down the door
    hint: '숫자 문을 지나 발판에 서자.',
    bodies: 3,
    gimmickTiles: '3aA',
    map: [
      '########',
      '#S..3.a#',
      '##A#####',
      '##E#####',
      '########',
    ],
  },  {
    name: '갈림길의 숫자',                      // 17 new (stage-search gate seed 77): numbered doors close the short cuts; the second brother goes round
    hint: '숫자 문은 지름길이 아니다. 몇 번째에 닿는지 세자.',
    bodies: 3,
    gimmickTiles: '1234567',
    map: [
      '#########',
      '#......##',
      '##..#...#',
      '####4.6.#',
      '#..S..5E#',
      '#########',
    ],
  },
  {
    name: '처음부터 다시 세기',                  // 19 new: the 2 door is 5 away, so the first brother never passes it; the next starts 2 away
    hint: '새 출발점에서는 1부터 다시 센다.',
    bodies: 3,
    gimmickTiles: '2',
    map: [
      '###########',
      '#S....2..E#',
      '###########',
    ],
  },  {
    name: '숫자와 구멍',                        // 20 new: bridge first, then the 3 door counts from the start across the bridge
    hint: '다리를 건너는 걸음도 센다.',
    bodies: 3,
    gimmickTiles: 'o3',
    map: [
      '########',
      '#.######',
      '#So.3.E#',
      '########',
    ],
  },  {
    name: '두 숫자',                           // 20 new (stage-search gate seed 77): a 3 door and a 6 door around a split board
    hint: '두 숫자 문. 둘째 형제의 몇 번째 걸음일까?',
    bodies: 3,
    gimmickTiles: '1234567',
    map: [
      '#########',
      '#..6....#',
      '#....#3.#',
      '#...#...#',
      '##..S#E.#',
      '#########',
    ],
  },
];
