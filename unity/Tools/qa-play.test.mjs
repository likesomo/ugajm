// Tests for qa-play.mjs and the docs/qa-attempts.md contract. Run: node --test unity/Tools/qa-play.test.mjs
// The tests use the solver to find inputs; the harness itself never does.
import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { run, replayLine, stage, finalKey, keyHash, QaError } from './qa-play.mjs';
import { newState, step, choose, actions, minBodies, keyOf, DIRS } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

const LETTER = { up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' };
const word = (path) => path.map((a) => LETTER[a]).join('');
const lines = () => { const p = join(process.env.QA_DIR, 'attempts.jsonl'); return existsSync(p) ? readFileSync(p, 'utf8').trim().split('\n').map((l) => JSON.parse(l)) : []; };
const last = () => lines().at(-1);
const T = ['--tester', 'A'];
const play = (word) => {
  // Moves go through `move`, K/X through keep/switch, Z through undo, H through hint.
  for (const c of word) {
    if ('UDLR'.includes(c)) run(['move', ...T, c]);
    else run([{ K: 'keep', X: 'switch', Z: 'undo', H: 'hint' }[c], ...T]);
  }
};
const errors = (argv, re) => assert.throws(() => run(argv), (e) => e instanceof QaError && re.test(e.message));

// BFS from the start for a command list that ends in a lose whose last command does (or does not) contain a fall.
function findLoss(level, wantFall) {
  const seen = new Set();
  let frontier = [{ s: newState(level), path: [] }];
  while (frontier.length) {
    const next = [];
    for (const { s, path } of frontier) for (const a of actions(s)) {
      const r = DIRS[a] ? step(s, a) : choose(s, a);
      if (r.state === s) continue;
      const p = [...path, a];
      if (r.state.status === 'lose') { if (r.events.some((e) => e.type === 'fall') === wantFall) return p; continue; }
      if (r.state.status === 'win') continue;
      const k = keyOf(r.state); if (seen.has(k)) continue; seen.add(k);
      next.push({ s: r.state, path: p });
    }
    frontier = next;
  }
  return null;
}
const pitStage = LEVELS.findIndex((d) => d.map.join('').includes('o')) + 1;

beforeEach(() => { process.env.QA_DIR = mkdtempSync(join(tmpdir(), 'qa-play-')); });

test('commands before start, bad commands and level range fail at once', () => {
  errors(['show', ...T], /no session/);
  errors(['start', ...T, '--level', '0'], /level must be 1-50/);
  errors(['start', ...T, '--level', '51'], /level must be 1-50/);
  errors(['start', ...T, '--level', 'x'], /level must be/);
  errors(['start', '--level', '1'], /--tester/);
  errors(['start', '--tester', 'a b', '--level', '1'], /--tester must be/);
  run(['start', ...T, '--level', '1']);
  errors(['start', ...T, '--level', '2'], /still in progress/);
  errors(['fly', ...T], /unknown command/);
  errors(['move', ...T, 'RQ'], /move needs directions/);
  errors(['keep', ...T], /only after a 7th step/);
  errors(['undo', ...T], /nothing to undo/);
  assert.equal(lines().length, 0);
});

test('every stage clears with its minimum-body solution: 3 stars, the line replays', () => {
  for (let n = 1; n <= LEVELS.length; n++) {
    run(['start', ...T, '--level', String(n)]);
    play(word(minBodies(stage(n).level).path));
    const l = last();
    assert.equal(l.level, n); assert.equal(l.outcome, 'clear'); assert.equal(l.stars, 3, `stage ${n}`);
    assert.equal(l.bodiesUsed, stage(n).minBodies);
    assert.deepEqual(replayLine(l), [], `stage ${n}`);
  }
});

test('undo gives 2 stars, a hint 1 star, a counted restart 2 stars', () => {
  const path = word(minBodies(stage(4).level).path);
  run(['start', ...T, '--level', '4']); play(path[0] + 'Z' + path);
  assert.equal(last().stars, 2); assert.equal(last().undos, 1); assert.equal(last().inputs, path[0] + 'Z' + path);
  run(['start', ...T, '--level', '4']); play('H' + path);
  assert.equal(last().stars, 1); assert.equal(last().hints, 1); assert.equal(last().attempt, 2);
  run(['start', ...T, '--level', '4']);
  run(['restart', ...T]); // fresh: not counted, no line
  assert.equal(lines().length, 2);
  play(path[0]); run(['restart', ...T]);
  assert.equal(last().outcome, 'restart'); assert.equal(last().levelRestarts, 1); assert.equal(last().attempt, 3);
  play(path);
  assert.equal(last().outcome, 'clear'); assert.equal(last().stars, 2); assert.equal(last().levelRestarts, 1); assert.equal(last().attempt, 4);
  for (const l of lines()) assert.deepEqual(replayLine(l), []);
});

test('fell and out_of_bodies end an attempt; undo continues the run with from > 0', () => {
  const fellPath = findLoss(stage(pitStage).level, true);
  assert.ok(fellPath, 'a stage with a pit can be lost by falling');
  run(['start', ...T, '--level', String(pitStage)]); play(word(fellPath));
  const f = last();
  assert.equal(f.outcome, 'fell'); assert.equal(f.failReason, 'fall'); assert.equal(f.bodiesUsed, stage(pitStage).level.bodies);
  errors(['move', ...T, 'R'], /cannot move: status lose/);
  run(['undo', ...T]); run(['giveup', ...T]);
  const g = last();
  assert.equal(g.outcome, 'gave_up'); assert.equal(g.from, f.inputs.length); assert.equal(g.inputs, f.inputs + 'Z');
  assert.equal(g.levelUndos, 1); assert.equal(g.undos, 1);

  const oobPath = findLoss(stage(1).level, false);
  run(['start', ...T, '--level', '1']); play(word(oobPath));
  assert.equal(last().outcome, 'out_of_bodies'); assert.ok(['vanish', 'stone', 'bridge', 'seal'].includes(last().failReason));
  run(['restart', ...T]); // after a loss: counted, but no new tokens so no line
  const n = lines().length;
  run(['giveup', ...T]);
  assert.equal(lines().length, n + 1);
  assert.equal(last().inputs, ''); assert.equal(last().from, 0); assert.equal(last().levelRestarts, 1);
  assert.equal(last().finalKey, finalKey(newState(stage(1).level)));
  for (const l of lines()) assert.deepEqual(replayLine(l), []);
});

test('no solution leaks: show --json and every output carry no solver or expected values', () => {
  run(['start', ...T, '--level', '50']);
  const v = JSON.parse(run(['show', ...T, '--json']));
  const text = JSON.stringify(v) + run(['show', ...T]) + run(['status', ...T]) + run(['hint', ...T]);
  for (const k of ['expected', 'minBodies', 'path', 'solution', 'moves']) assert.ok(!text.includes(k), k);
  const exp = JSON.parse(readFileSync(new URL('../Assets/_Project/Data/levels.json', import.meta.url), 'utf8')).levels[49].expected.path;
  assert.ok(!text.includes(exp));
});

test('replayLine catches tampering; finalHash is the key hash; CLI exits 2 on errors', () => {
  run(['start', ...T, '--level', '1']); play(word(minBodies(stage(1).level).path));
  const l = last();
  assert.equal(l.finalHash, keyHash(l.finalKey));
  assert.match(replayLine({ ...l, stars: 2 }).join(), /stars/);
  assert.match(replayLine({ ...l, inputs: l.inputs + 'R' }).join(), /no effect/);
  assert.match(replayLine({ ...l, outcome: 'restart' }).join(), /status win/);
  assert.match(replayLine({ ...l, mapHash: 'x' }).join(), /map changed/);
  const cli = spawnSync(process.execPath, [fileURLToPath(new URL('./qa-play.mjs', import.meta.url)), 'show', '--tester', 'nobody'], { env: process.env, encoding: 'utf8' });
  assert.equal(cli.status, 2); assert.match(cli.stderr, /^error: no session/);
  const ok = spawnSync(process.execPath, [fileURLToPath(new URL('./qa-play.mjs', import.meta.url)), 'verify'], { env: process.env, encoding: 'utf8' });
  assert.equal(ok.status, 0); assert.match(ok.stdout, /verify OK: 1 lines/);
});
