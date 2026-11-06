// Seeded RNG (mulberry32). State lives in state.rng so snapshots stay deterministic.
export function nextRandom(s) {
  let t = (s.rng = (s.rng + 0x6d2b79f5) >>> 0);
  t = Math.imul(t ^ (t >>> 15), t | 1);
  t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}

export const randInt = (s, lo, hi) => lo + Math.floor(nextRandom(s) * (hi - lo + 1));
export const pick = (s, arr) => arr[Math.floor(nextRandom(s) * arr.length)];
