// Fake playtest data: simulated players play the real rules (core.js) and the real Tracker (telemetry.js)
// turns their inputs into schema v1 events. Used by tests and for dashboard screenshots.
//   node server/tools/fake-data.mjs --players 80 --db server/data/telemetry.sqlite   (write straight into a DB)
//   node server/tools/fake-data.mjs --players 80 --url http://localhost:8787         (POST to a running server)
//   node server/tools/fake-data.mjs --players 5 --out fake.json                       (dump batches)
import { writeFileSync } from 'node:fs';
import { parseLevel, newState, dispatch, solve, DIRS } from '../../prototypes/seven-steps/src/core.js';
import { pathToFileURL } from 'node:url';
// LEVELS_JS (same variable as the server) plays another levels file, e.g. a copy with chapter ids.
const { LEVELS } = await import(process.env.LEVELS_JS ? pathToFileURL(process.env.LEVELS_JS).href : '../../prototypes/seven-steps/src/levels.js');
import { Tracker, SCHEMA_VERSION } from '../../prototypes/seven-steps/src/telemetry.js';

function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const uuidFrom = (r) => 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
  const n = Math.floor(r() * 16);
  return (c === 'x' ? n : (n & 3) | 8).toString(16);
});

const DIR_KEYS = Object.keys(DIRS);
let PLANS = null;
function plans() {
  if (PLANS) return PLANS;
  PLANS = LEVELS.map((def, i) => {
    const level = parseLevel(def);
    const sol = solve(level);
    const path = sol ? sol.path : [];
    // Difficulty grows with solution length and late-game mechanics; 8 and 12 are made extra sticky on purpose.
    const diff = Math.min(1, path.length / 45 + i * 0.03 + (i === 7 || i === 11 ? 0.25 : 0));
    return { level, path, diff, name: def.name, chapter: def.chapter };
  });
  return PLANS;
}

const cmdOf = (a) => (DIRS[a] ? { type: 'move', dir: a } : { type: a });

// Simulates one player. Returns envelopes ({ v, playerId, sessionId, client, build, events }).
export function simulatePlayer(r, { startT, build = 'proto-fake', client = 'web-proto' }) {
  const P = plans();
  const playerId = uuidFrom(r);
  const skill = 0.35 + r() * 0.6;
  const patience = 0.4 + r() * 0.8;
  let clock = startT;
  const attempts = new Map();
  const envelopes = [];
  const cleared = new Set();
  let quit = false;
  const sessions = 1 + Math.floor(r() * r() * 3);

  for (let si = 0; si < sessions && !quit; si++) {
    const sessionId = uuidFrom(r);
    const events = [];
    const tr = new Tracker({ emit: (e) => events.push(e), now: () => clock, attempts });
    tr.sessionStart({ firstTime: si === 0, cleared: cleared.size, resumed: false });
    let idx = [...Array(P.length).keys()].find((k) => !cleared.has(k)) ?? 0;
    let via = si === 0 ? 'boot' : 'boot';
    let sessionBudget = 8 + r() * 30; // minutes this sitting
    const sessionT0 = clock;
    let state = null;

    while (idx < P.length) {
      const plan = P[idx];
      state = newState(plan.level);
      tr.levelStart(state, idx + 1, plan.name, via, plan.chapter);
      let pi = 0; // next index into the solution path while on track
      let off = 0; // moves made off the solution path
      let frustration = 0;
      let won = false;
      let hinted = false;
      for (let guard = 0; guard < 400 && !won; guard++) {
        clock += 300 + r() * 1500 * (1 + plan.diff);
        // Long pause, more likely on hard stages and for less skilled players.
        if (r() < 0.012 + plan.diff * 0.03 * (1.3 - skill)) clock += 30000 + r() * 90000;
        const cmdAct = (cmd) => {
          const before = state;
          const res = dispatch(state, cmd);
          tr.command(before, cmd, res);
          state = res.state;
          return res;
        };
        if (state.status === 'lose' || off > 6 + skill * 8) {
          if (state.status !== 'lose' && r() < 0.5) {
            while (off > 0 && state.prev) { cmdAct({ type: 'undo' }); off--; clock += 200; }
            if (off === 0) continue;
          }
          cmdAct({ type: 'restart' });
          pi = 0; off = 0; frustration += 1;
          continue;
        }
        if (!hinted && frustration > 2 + skill * 4 && r() < 0.4) { clock += 1000; tr.hint(state); hinted = true; }
        const err = (1 - skill) * (0.15 + plan.diff * 0.5) * (hinted ? 0.4 : 1);
        if (off === 0 && pi < plan.path.length && r() > err) {
          const res = cmdAct(cmdOf(plan.path[pi]));
          if (res.state.status === 'win') won = true;
          pi++;
        } else {
          const a = state.status === 'choose' ? (r() < 0.5 ? 'keep' : 'switch') : DIR_KEYS[Math.floor(r() * 4)];
          const before = state;
          cmdAct(cmdOf(a));
          if (state !== before) {
            off++;
            // Noticing a mistake right away: undo it.
            if (r() < 0.25 + skill * 0.4 && state.prev) { clock += 400; cmdAct({ type: 'undo' }); off--; }
          }
        }
        if (frustration > patience * (3 + skill * 6) * (1.2 - plan.diff * 0.6)) {
          if (r() < 0.55) { quit = true; break; }
          if (r() < 0.4 && idx + 1 < P.length) { idx++; via = 'select'; tr.leave(state, idx + 1); frustration = 0; break; }
          frustration = 0;
        }
        if ((clock - sessionT0) / 60000 > sessionBudget) break;
      }
      if (quit) break;
      if (won) {
        cleared.add(idx);
        clock += 1500 + r() * 3000;
        if (idx + 1 >= P.length) break;
        tr.leave(state, idx + 2);
        idx++;
        via = 'next';
        if ((clock - sessionT0) / 60000 > sessionBudget) break;
        continue;
      }
      if ((clock - sessionT0) / 60000 > sessionBudget) break;
      if (via !== 'select') break;
    }
    tr.sessionEnd(state, r() < 0.5 ? 'hidden' : 'unload');
    for (let k = 0; k < events.length; k += 100) {
      envelopes.push({ v: SCHEMA_VERSION, playerId, sessionId, client, build, events: events.slice(k, k + 100) });
    }
    clock += 3600000 * (2 + r() * 30); // next sitting hours later
  }
  return envelopes;
}

export function generate({ players = 40, seed = 7, startT = Date.now() - 3 * 86400000, builds = ['proto-fake'] } = {}) {
  const r = rng(seed);
  const out = [];
  for (let i = 0; i < players; i++) {
    const t = startT + Math.floor(r() * 2 * 86400000);
    out.push(...simulatePlayer(r, { startT: t, build: builds[i % builds.length], client: r() < 0.7 ? 'web-proto' : 'unity-webgl' }));
  }
  return out;
}

if ((process.argv[1] ?? '').replace(/\\/g, '/').endsWith('server/tools/fake-data.mjs')) {
  const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, arr) => (a.startsWith('--') ? [...acc, [a.slice(2), arr[i + 1]]] : acc), []));
  const batches = generate({ players: Number(args.players ?? 40), seed: Number(args.seed ?? 7), builds: (args.builds ?? 'proto-fake').split(',') });
  const nEvents = batches.reduce((a, b) => a + b.events.length, 0);
  if (args.db) {
    const { openDb } = await import('../src/db.mjs');
    const store = openDb(args.db);
    let acc = 0;
    for (const b of batches) acc += store.insertBatch(b, b.events).accepted;
    store.close();
    console.log(`${batches.length} batches, ${nEvents} events, ${acc} inserted into ${args.db}`);
  } else if (args.url) {
    let acc = 0;
    for (const b of batches) {
      const res = await fetch(`${args.url.replace(/\/$/, '')}/v1/events`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(b) });
      const j = await res.json();
      acc += j.accepted ?? 0;
      if (j.rejected?.length) console.warn('rejected', j.rejected.slice(0, 3));
    }
    console.log(`${batches.length} batches, ${nEvents} events, ${acc} accepted by ${args.url}`);
  } else {
    writeFileSync(args.out ?? 'fake-telemetry.json', JSON.stringify(batches));
    console.log(`${batches.length} batches, ${nEvents} events → ${args.out ?? 'fake-telemetry.json'}`);
  }
}
