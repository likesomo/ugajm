// Pure rules for Seven Steps. No DOM, no time, no randomness: every state change goes through dispatch().
export const STEPS = 7;
export const DIRS = { up: [0, -1], down: [0, 1], left: [-1, 0], right: [1, 0] };

// Map legend: # wall, . floor, S start, E exit, o pit, ~ ice, x cracked floor,
// a-c plate, A-C door (open while a statue stands on the matching plate), 1-7 gate (pass only on that step),
// Q statue-count door (open while exactly `statueCount` statues stand; bridges and seals are not statues),
// L lever (a 7th step ending on it flips every flip wall), R raised flip wall (blocks), r lowered flip wall (open),
// m mud (stepping into it costs 2 steps, so it blocks with one step left; an ice slide stops on it at no extra cost).
export function parseLevel(def) {
  const h = def.map.length;
  const w = Math.max(...def.map.map((r) => r.length));
  const tiles = [];
  const plates = {};
  let start = -1;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const t = def.map[y][x] ?? '#';
      const i = y * w + x;
      if (t === 'S') start = i;
      if (t >= 'a' && t <= 'c') (plates[t] ??= []).push(i);
      tiles.push(t === 'S' ? '.' : t);
    }
  }
  if (start < 0) throw new Error(`level "${def.name}" has no S`);
  return { name: def.name, hint: def.hint ?? '', w, h, tiles, plates, start, bodies: def.bodies ?? STEPS, statueCount: def.statueCount ?? 0 };
}

export function newState(level) {
  return {
    level, start: level.start, pos: level.start, used: 0, bodies: level.bodies,
    statues: [], bridges: [], broken: [], flipped: false, trail: [{ i: level.start, n: 0 }],
    status: 'play', prev: null,
  };
}

// Tile as currently seen: bridges (pit filled by a statue) read as '=', broken cracked floor as a pit,
// flip walls after an odd number of lever pulls swap: 'R' is always "raised now", 'r' "lowered now".
export function tileAt(s, i) {
  if (s.bridges.includes(i)) return '=';
  if (s.broken.includes(i)) return 'o';
  const t = s.level.tiles[i];
  if (s.flipped && isFlipWall(t)) return t === 'R' ? 'r' : 'R';
  return t;
}

export const isFlipWall = (t) => t === 'R' || t === 'r';

// Statues standing now. Bridges (filled pits, sealed cracked floor) are not statues.
export const statueCount = (s) => s.statues.length;

export const isDoor = (t) => (t >= 'A' && t <= 'C') || t === 'Q';

// Door letter A-C: a statue stands on a matching plate. Q: exactly level.statueCount statues stand.
export function doorOpen(s, door) {
  if (door === 'Q') return statueCount(s) === s.level.statueCount;
  return (s.level.plates[door.toLowerCase()] ?? []).some((p) => s.statues.includes(p));
}

function neighbor(s, i, dir) {
  const { w, h } = s.level;
  const x = (i % w) + DIRS[dir][0];
  const y = Math.floor(i / w) + DIRS[dir][1];
  return x < 0 || y < 0 || x >= w || y >= h ? -1 : y * w + x;
}

export function passable(s, i, stepIdx) {
  if (i < 0 || s.statues.includes(i)) return false;
  const t = tileAt(s, i);
  if (t === '#' || t === 'R') return false;
  if (isDoor(t)) return doorOpen(s, t);
  if (t >= '1' && t <= '7') return Number(t) === stepIdx;
  return true;
}

const add = (arr, i) => (arr.includes(i) ? arr : [...arr, i].sort((a, b) => a - b));

// A new statue may open doors (plates, or Q reaching its count) or close them (Q passing its count):
// one 'doorOpen'/'doorClose' event per door cell that changed, in cell order. Undo/restart swap the whole state instead.
function doorChanges(before, after, events) {
  after.level.tiles.forEach((t, i) => {
    if (!isDoor(t)) return;
    const was = doorOpen(before, t);
    const now = doorOpen(after, t);
    if (was !== now) events.push({ type: now ? 'doorOpen' : 'doorClose', at: i });
  });
}

function nextBody(s, events, start = s.start) {
  const bodies = s.bodies - 1;
  if (bodies <= 0) {
    events.push({ type: 'lose' });
    return { ...s, bodies: 0, status: 'lose' };
  }
  events.push({ type: 'respawn', at: start });
  return { ...s, bodies, start, pos: start, used: 0, status: 'play', trail: [{ i: start, n: 0 }] };
}

// One step. Returns { state, events }; an illegal move returns the same state with a 'bump' event.
export function step(s, dir) {
  const events = [];
  if (s.status !== 'play' || !DIRS[dir]) return { state: s, events };
  const first = neighbor(s, s.pos, dir);
  // Mud: stepping into it costs 2 steps; with only one step left it blocks like a wall.
  const stepIdx = s.used + (first >= 0 && tileAt(s, first) === 'm' ? 2 : 1);
  if (stepIdx > STEPS || !passable(s, first, stepIdx)) return { state: s, events: [{ type: 'bump', at: first }] };

  let pos = first;
  const trail = [...s.trail, { i: pos, n: stepIdx }];
  // Ice: keep sliding while standing on ice and the next cell is open. The slide is still one step.
  // Sliding into mud stops there (mud is not ice) and adds no step.
  while (tileAt(s, pos) === '~') {
    const n = neighbor(s, pos, dir);
    if (!passable(s, n, stepIdx)) break;
    pos = n;
    trail.push({ i: pos, n: stepIdx });
  }

  let next = { ...s, pos, used: stepIdx, trail };
  // Cracked floor survives one crossing: it breaks into a pit behind you.
  if (tileAt(s, s.pos) === 'x') {
    next.broken = add(next.broken, s.pos);
    events.push({ type: 'crumble', at: s.pos });
  }
  // Slid on from the first (ice) cell: 'slide' at that cell, before what happens where the slide ends.
  if (pos !== first) events.push({ type: 'slide', at: first });

  const t = tileAt(next, pos);
  const lucky = stepIdx === STEPS;
  if (t === 'o') {
    if (lucky) {
      next.bridges = add(next.bridges, pos);
      events.push({ type: 'bridge', at: pos });
    } else {
      events.push({ type: 'fall', at: pos });
    }
    return { state: nextBody(next, events), events };
  }
  if (!lucky) {
    events.push({ type: 'step', at: pos, stepIdx });
    return { state: next, events };
  }
  // Lever: the 7th step ending on it flips every flip wall, then the body is spent as usual (choose/stone).
  // On the start cell the 7th step only vanishes, so the lever there is not pulled.
  if (t === 'L' && pos !== s.start) {
    next.flipped = !s.flipped;
    events.push({ type: 'flip', at: pos });
  }
  if (t === 'E') {
    events.push({ type: 'win', at: pos });
    return { state: { ...next, status: 'win' }, events };
  }
  if (t === 'x') {
    // Ending on cracked floor seals it: the floor gives way and the statue becomes a bridge.
    next.broken = add(next.broken, pos);
    next.bridges = add(next.bridges, pos);
    events.push({ type: 'seal', at: pos });
  } else if (pos === s.start) {
    events.push({ type: 'vanish', at: pos });
  } else if (s.bodies > 1) {
    // KEEP or SWITCH: the player decides what this body leaves behind.
    events.push({ type: 'choose', at: pos });
    return { state: { ...next, status: 'choose' }, events };
  } else {
    next.statues = add(next.statues, pos);
    events.push({ type: 'stone', at: pos });
    doorChanges(s, next, events);
  }
  return { state: nextBody(next, events), events };
}

// After the 7th step. KEEP: become a statue here, next body wakes at the current start.
// SWITCH: leave nothing, next body wakes here (this spot becomes the new start). Both spend the body.
export function choose(s, kind) {
  const events = [];
  if (s.status !== 'choose') return { state: s, events };
  if (kind === 'keep') {
    events.push({ type: 'stone', at: s.pos });
    const kept = { ...s, statues: add(s.statues, s.pos) };
    doorChanges(s, kept, events);
    return { state: nextBody(kept, events), events };
  }
  if (kind === 'switch') {
    events.push({ type: 'switch', at: s.pos });
    return { state: nextBody(s, events, s.pos), events };
  }
  return { state: s, events };
}

// Every legal command kind in a state; the solver walks these.
export function actions(s, noSwitch = false) {
  if (s.status === 'choose') return noSwitch ? ['keep'] : ['keep', 'switch'];
  return s.status === 'play' ? Object.keys(DIRS) : [];
}

function apply(s, a) {
  return DIRS[a] ? step(s, a) : choose(s, a);
}

// Single entry point for the shell. Keeps an undo chain through `prev`.
export function dispatch(s, cmd) {
  if (cmd.type === 'move' || cmd.type === 'keep' || cmd.type === 'switch') {
    const r = cmd.type === 'move' ? step(s, cmd.dir) : choose(s, cmd.type);
    if (r.state === s) return r;
    return { state: { ...r.state, prev: s }, events: r.events };
  }
  if (cmd.type === 'undo') return s.prev ? { state: s.prev, events: [{ type: 'undo' }] } : { state: s, events: [] };
  if (cmd.type === 'restart') return { state: newState(s.level), events: [{ type: 'restart' }] };
  return { state: s, events: [] };
}

const keyOf = (s) => `${s.start}|${s.pos}|${s.used}|${s.bodies}|${s.statues}|${s.bridges}|${s.broken}|${s.status}|${s.flipped ? 1 : 0}`;

// BFS over actions (moves and KEEP/SWITCH). Returns the shortest winning move list, or null. Used by the solver tool and the hint button.
export function solve(level, maxBodies = level.bodies, { noSwitch = false, limit = 400000 } = {}) {
  const start = newState({ ...level, bodies: maxBodies });
  const seen = new Set([keyOf(start)]);
  let frontier = [{ s: start, path: [] }];
  while (frontier.length && seen.size < limit) {
    const nextFrontier = [];
    for (const { s, path } of frontier) {
      for (const a of actions(s, noSwitch)) {
        const { state } = apply(s, a);
        if (state === s || state.status === 'lose') continue;
        const p = [...path, a];
        if (state.status === 'win') return { path: p, bodiesUsed: maxBodies - state.bodies + 1 };
        const k = keyOf(state);
        if (seen.has(k)) continue;
        seen.add(k);
        nextFrontier.push({ s: state, path: p });
      }
    }
    frontier = nextFrontier;
  }
  return null;
}

// Fewest bodies that can still clear the level.
export function minBodies(level) {
  for (let b = 1; b <= level.bodies; b++) {
    const r = solve(level, b);
    if (r) return { bodies: b, moves: r.path.length, path: r.path };
  }
  return null;
}
