// Sketch stages for the statue-count door. Q opens only while exactly `count` statues stand (bridges and seals do not count).
export const STAGES = [
  {
    name: '하나만 남겨라', count: 1, bodies: 3,
    hint: '문의 숫자는 서 있는 석상 수. 정확히 그 수일 때만 열린다.',
    map: [
      '########',
      '#S.....#',
      '#.######',
      '#..Q..E#',
      '########',
    ],
  },
  {
    name: '다리는 세지 않는다', count: 1, bodies: 4,
    hint: '구덩이를 메운 몸은 석상으로 세지 않는다.',
    map: [
      '##########',
      '#S..o...a#',
      '#.########',
      '#..A.QE###',
      '##########',
    ],
  },
  {
    name: '남기지 말고 옮겨라', count: 1, bodies: 4,
    hint: '석상이 하나 더 서면 문이 닫힌다. 남기지 않고 가는 방법은?',
    map: [
      '##########',
      '#a......S#',
      '#######A##',
      '#.E..Q...#',
      '##########',
    ],
  },
];
