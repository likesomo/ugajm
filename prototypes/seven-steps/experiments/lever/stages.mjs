// Sketch stages for the lever. 7th step on L flips every R (raised → lowered) and r (lowered → raised).
// A statue on the lever blocks it; SWITCH there makes it the start, and a 7th step on the start vanishes and flips nothing.
export const STAGES = [
  {
    name: '내리는 벽', bodies: 3, gimmickTiles: 'Rr',
    hint: '레버를 7번째 걸음으로 밟으면 벽이 내려간다.',
    map: [
      '#########',
      '#S....L.#',
      '#.#######',
      '#.R...E##',
      '#########',
    ],
  },
  {
    name: '열면 닫힌다', bodies: 3, gimmickTiles: 'Rr',
    hint: '레버는 모든 벽을 뒤집는다. 열린 길도 닫힌다.',
    map: [
      '##########',
      '#S..r..L.#',
      '#.##R#####',
      '#...R..E##',
      '##########',
    ],
  },
  {
    name: '올려서 멈추기', bodies: 3, gimmickTiles: 'r',
    hint: '내려가 있던 벽(r)은 레버를 당기면 올라온다. 올라온 벽은 얼음을 멈춘다.',
    map: [
      '##########',
      '#S....L..#',
      '#.########',
      '#.########',
      '#.~~~~r..#',
      '#####E####',
      '##########',
    ],
  },
];
