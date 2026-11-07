// EXPERIMENT copy of src/core.js (statue-count door). Not the rule source. Changes marked EXP.
// Pure rules for Seven Steps. No DOM, no time, no randomness: every state change goes through dispatch().
export const STEPS = 7;
export const DIRS = { up: [0, -1], down: [0, 1], left: [-1, 0], right: [1, 0] };

// Map legend: # wall, . floor, S start, E exit, o pit, ~ ice, x cracked floor,
// a-c plate, A-C door (open while a statue stands on the matching plate), 1-7 gate (pass only on that step).
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
  return { name: def.name, hint: def.hint ?? '', w, h, tiles, plates, start, bodies: def.bodies ?? STEPS, statueCount: def.statueCount ?? 0 }; // EXP statueCount
}

export function newState(level) {
  return {
    level, start: level.start, pos: level.start, used: 0, bodies: level.bodies,
    statues: [], bridges: [], broken: [], trail: [{ i: level.start, n: 0 }],
    status: 'play', prev: null,
  };
}

// Tile as currently seen: bridges (pit filled by a statue) read as '=', broken cracked floor as a pit.
export function tileAt(s, i) {
  if (s.bridges.includes(i)) return '=';
  if (s.broken.includes(i)) return 'o';
  return s.level.tiles[i];
}

export function doorOpen(s, door) {
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
  if (t === '#') return false;
  if (t >= 'A' && t <= 'C') return doorOpen(s, t);
  if (t === 'Q') return s.statues.length === s.level.statueCount; // EXP statue-count door
  if (t >= '1' && t <= '7') return Number(t) === stepIdx;
  return true;
}

const add = (arr, i) => (arr.includes(i) ? arr : [...arr, i].sort((a, b) => a - b));

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
  const stepIdx = s.used + 1;
  const first = neighbor(s, s.pos, dir);
  if (!passable(s, first, stepIdx)) return { state: s, events: [{ type: 'bump', at: first }] };

  let pos = first;
  const trail = [...s.trail, { i: pos, n: stepIdx }];
  // Ice: keep sliding while standing on ice and the next cell is open. The slide is still one step.
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
    return { state: nextBody({ ...s, statues: add(s.statues, s.pos) }, events), events };
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

const keyOf = (s) => `${s.start}|${s.pos}|${s.used}|${s.bodies}|${s.statues}|${s.bridges}|${s.broken}|${s.status}`;

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
