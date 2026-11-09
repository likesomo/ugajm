import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseLevel, newState, dispatch } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';
import { Tracker, levelKey, fnv1a, tileKind } from '../../prototypes/seven-steps/src/telemetry.js';
import { checkEvent } from '../src/validate.mjs';

const here = dirname(fileURLToPath(import.meta.url));

function play(levelNo, commands, { step = 1000, pauses = {} } = {}) {
  let clock = 1_762_500_000_000;
  const events = [];
  const tr = new Tracker({ emit: (e) => events.push(e), now: () => clock });
  let s = newState(parseLevel(LEVELS[levelNo - 1]));
  tr.sessionStart({ firstTime: true, cleared: 0 });
  tr.levelStart(s, levelNo, LEVELS[levelNo - 1].name, 'boot');
  const M = { U: 'up', D: 'down', L: 'left', R: 'right' };
  [...commands].forEach((ch, i) => {
    clock += pauses[i] ?? step;
    if (ch === 'H') return tr.hint(s);
    const cmd = M[ch] ? { type: 'move', dir: M[ch] } : { K: { type: 'keep' }, X: { type: 'switch' }, Z: { type: 'undo' }, '*': { type: 'restart' } }[ch];
    const r = dispatch(s, cmd);
    tr.command(s, cmd, r);
    s = r.state;
  });
  return { events, state: s, tr };
}

test('stage 1 solution: moves, seventh step, clear', () => {
  const { events } = play(1, 'RRRRRRR');
  assert.deepEqual(events.map((e) => e.type), ['session_start', 'level_start', ...Array(7).fill('move'), 'seventh_step', 'level_clear']);
  assert.deepEqual(events.map((e) => e.seq), events.map((_, i) => i));
  const clear = events.at(-1);
  assert.equal(clear.moves, 7);
  assert.equal(clear.durationMs, 7000);
  assert.equal(clear.bodiesUsed, 1);
  assert.deepEqual(events[8].to, [8, 1]);
  assert.equal(events[9].effect, 'win');
  for (const e of events) assert.equal(checkEvent(e), null, JSON.stringify(e));
});

test('blocked move, idle after a long pause, undo and restart', () => {
  const { events } = play(1, 'LRZ*', { pauses: { 1: 45000 } });
  const types = events.map((e) => e.type);
  assert.deepEqual(types.slice(2), ['move', 'idle', 'move', 'undo', 'restart']);
  assert.equal(events[2].result, 'blocked');
  assert.equal(events[2].blockedBy, 'wall');
  assert.equal(events[3].idleMs, 45000);
  assert.deepEqual(events[3].at, [1, 1]);
  assert.equal(events[6].moves, 1);
});

test('pit without the 7th step: fell + body_lost wasted', () => {
  // Stage 5: S..o.E — the third step lands in the pit.
  const { events } = play(5, 'RRR');
  const fell = events.find((e) => e.type === 'move' && e.result === 'fell');
  assert.ok(fell);
  const lost = events.find((e) => e.type === 'body_lost');
  assert.equal(lost.reason, 'fell');
  assert.equal(lost.wasted, true);
});

test('levelKey is stable and matches the golden file', () => {
  assert.equal(fnv1a(''), '811c9dc5');
  assert.equal(fnv1a('a'), 'e40c292c');
  const keys = LEVELS.map((d) => levelKey(parseLevel(d)));
  assert.equal(new Set(keys).size, LEVELS.length);
  const golden = JSON.parse(readFileSync(join(here, '../fixtures/telemetry.golden.json'), 'utf8'));
  // Manual scenarios on a stage carry that stage's key; the ones with their own map (" (골든 전용)") are not stages.
  const onStages = golden.traces.filter((x) => x.id.startsWith('manual-') && !x.levelName.endsWith('(골든 전용)'));
  assert.ok(onStages.length >= 5);
  for (const t of onStages) assert.equal(t.levelKey, keys[t.level - 1], t.id);
  for (const t of golden.traces.filter((x) => x.levelName.endsWith('(골든 전용)'))) assert.equal(keys.includes(t.levelKey), false, t.id);
});

test('golden file is up to date with telemetry.js', () => {
  const path = join(here, '../fixtures/telemetry.golden.json');
  const beforeText = readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
  execFileSync(process.execPath, [join(here, '../tools/telemetry-golden.mjs')], { cwd: join(here, '../..') });
  const afterText = readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
  assert.equal(afterText, beforeText, 'run node server/tools/telemetry-golden.mjs and commit the result');
});

test('level_start carries the chapter id only when the stage has one', () => {
  const ev = [];
  const tr = new Tracker({ emit: (e) => ev.push(e), now: () => 1_762_500_000_000 });
  const s = newState(parseLevel(LEVELS[0]));
  tr.levelStart(s, 1, LEVELS[0].name, 'boot');
  tr.levelStart(s, 1, LEVELS[0].name, 'again', 'moss-ruins');
  assert.equal('chapter' in ev[0], false);
  assert.equal(ev[1].chapter, 'moss-ruins');
  for (const e of ev) assert.equal(checkEvent(e), null);
  assert.match(checkEvent({ ...ev[1], chapter: 'x'.repeat(41) }), /chapter is invalid/);
});

test('mud costs 2 steps: a 7th step reached through mud is recorded; tile names', () => {
  let clock = 1_762_500_000_000;
  const ev = [];
  const tr = new Tracker({ emit: (e) => ev.push(e), now: () => (clock += 1000) });
  let s = newState(parseLevel({ name: 'mud', bodies: 2, map: ['#########', '#S....m.#', '#########'] }));
  tr.levelStart(s, 1, 'mud', 'boot');
  for (let i = 0; i < 6; i++) { const r = dispatch(s, { type: 'move', dir: 'right' }); tr.command(s, { type: 'move', dir: 'right' }, r); s = r.state; }
  const moves = ev.filter((e) => e.type === 'move');
  assert.deepEqual(moves.map((m) => m.step), [1, 2, 3, 4, 6, 7]); // 5th move enters mud: steps 5+6
  const seventh = ev.find((e) => e.type === 'seventh_step');
  assert.ok(seventh, 'seventh_step after mud');
  assert.deepEqual(seventh.at, [7, 1]);
  for (const e of ev) assert.equal(checkEvent(e), null, JSON.stringify(e));
  const lv = parseLevel({ name: 't', bodies: 1, map: ['#mLRrQ#', '#S....E#'], statueCount: 0 });
  const st = newState(lv);
  assert.deepEqual([1, 2, 3, 4, 5].map((x) => tileKind(st, x)), ['mud', 'lever', 'flip_up', 'flip_down', 'door']);
});
