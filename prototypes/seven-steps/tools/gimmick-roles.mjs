// Gimmick roles shared by gimmick-audit.mjs and stage-repair.mjs.
// For each gimmick group the solver compares three maps:
//   base     — the stage as it is
//   inert    — the gimmick loses its effect but its cells stay where they are (see INERT): harder than base → "used"
//              (the fewest-body solutions need what the gimmick does, not just the cell it stands on)
//   floor    — every cell of the group becomes plain floor: easier than base → "blocks"
//              (it closes a shorter way; not using it can be the point)
//   neither  → "dead": the stage plays the same without it
// "Harder" compares fewest bodies, then moves; with byBodies only bodies (stars only care about bodies).
import { parseLevel, minBodies } from '../src/core.js';

// Groups: a plate and its door are separate (a plate can matter while its door is a decoy, and back).
export const groupOf = (t) => (t >= 'a' && t <= 'c' ? `plate ${t}` : t >= 'A' && t <= 'C' ? `door ${t}` : t >= '1' && t <= '7' ? `gate ${t}`
  : t === 'L' ? 'lever' : t === 'R' || t === 'r' ? 'flip walls'
  : { o: 'pit', '~': 'ice', x: 'cracked', Q: 'Q door', m: 'mud' }[t] ?? null);

// What a cell becomes when its gimmick does nothing:
//   ice, mud, cracked, plate, lever → floor (walkable, no slide / no extra step / never breaks / opens nothing / flips nothing)
//   pit → wall (cannot be bridged; nothing falls in either)
//   gate, door, Q door → wall (never passable)
//   flip walls → their starting state, fixed (R wall, r floor)
export const INERT = (t) => ('~mxLabc'.includes(t) ? '.' : t === 'r' ? '.' : '#');

export const measure = (def) => {
  try { const b = minBodies(parseLevel(def)); return b ? { bodies: b.bodies, moves: b.moves } : null; } catch { return null; }
};
export const harder = (a, b, byBodies = false) =>
  (!a ? !!b : !b ? false : (byBodies ? a.bodies - b.bodies : a.bodies - b.bodies || a.moves - b.moves)) > 0;

const swap = (def, keep, to) => ({ ...def, map: def.map.map((r, y) => [...r].map((t, x) => (keep(t, x, y) ? to(t) : t)).join('')) });

// Roles for every group, or for every single cell with { cells: true }.
export function roles(def, { byBodies = false, cells = false } = {}) {
  const base = measure(def);
  const units = [];
  def.map.forEach((row, y) => [...row].forEach((t, x) => {
    const g = groupOf(t);
    if (!g) return;
    if (cells) units.push({ name: `${t}(${x},${y})`, hit: (tt, xx, yy) => xx === x && yy === y });
    else if (!units.some((u) => u.name === g)) units.push({ name: g, hit: (tt) => groupOf(tt) === g });
  }));
  return {
    base,
    units: units.map((u) => {
      const inert = measure(swap(def, u.hit, INERT));
      const floor = measure(swap(def, u.hit, () => '.'));
      const used = harder(inert, base, byBodies);
      const blocks = harder(base, floor, byBodies);
      return { name: u.name, inert, floor, role: used && blocks ? 'used+blocks' : used ? 'used' : blocks ? 'blocks' : 'dead' };
    }),
  };
}
