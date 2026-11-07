// Sketch stages for the statue-count door. Q opens only while exactly `statueCount` statues stand (bridges and seals do not count).
// gimmickTiles: run.mjs replaces them with floor to check the door is really needed.
export const STAGES = [
  {
    name: '하나만 남겨라', statueCount: 1, bodies: 3, gimmickTiles: 'Q',
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
    // The plate is behind the door, so the first statue can only be a key that opens it. The plate statue then makes two.
    name: '열쇠가 되는 몸', statueCount: 1, bodies: 4, gimmickTiles: 'Q',
    hint: '발판은 문 너머에 있다. 문을 열 석상은 어디서 구하지?',
    map: [
      '##########',
      '#S..Qa...#',
      '###A######',
      '###E######',
      '##########',
    ],
  },
  {
    // Reversal: a 0 door. Any statue closes it, so get past it (SWITCH) before leaving the plate statue.
    name: '남기기 전에 지나가라', statueCount: 0, bodies: 4, gimmickTiles: 'Q',
    hint: '0이 적힌 문은 석상이 하나라도 서면 닫힌다.',
    map: [
      '###########',
      '#S.a..Q...#',
      '#######A###',
      '#######E###',
      '###########',
    ],
  },
];
