import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildAttempts, attemptRows, levelStats, heatmap, quantile, toCsv, QUIT_AFTER_MS } from '../src/stats.mjs';
import { PID, SID, T0 } from './helpers.mjs';

// Builds db-like rows for one session.
function rows(list, { sid = SID, pid = PID } = {}) {
  return list.map(([dt, type, data = {}], seq) => {
    const cell = data.at ?? data.to ?? null;
    return { player_id: pid, session_id: sid, seq, t: T0 + dt, received_at: T0 + dt, type, client: 'web-proto', build: 'b',
      level: data.level ?? null, x: cell?.[0] ?? null, y: cell?.[1] ?? null, data };
  });
}
const mv = (level, to, result = 'moved') => ({ level, body: 1, step: 1, dir: 'right', from: [1, 1], to, result });
const NOW = T0 + QUIT_AFTER_MS * 10;

test('attempt outcomes: clear, leave, quit, open', () => {
  const r = rows([
    [0, 'session_start', { firstTime: true }],
    [1, 'level_start', { level: 1, attempt: 1, levelKey: 'a1070f9a' }],
    [2, 'move', mv(1, [2, 1])],
    [3, 'level_clear', { level: 1, durationMs: 5000, moves: 1 }],
    [4, 'level_start', { level: 2, attempt: 1 }],
    [5, 'level_leave', { level: 2, toLevel: 3, durationMs: 1000 }],
    [6, 'level_start', { level: 3, attempt: 1 }],
    [7, 'undo', { level: 3, body: 1, step: 1, at: [3, 3] }],
    [8, 'session_end', { reason: 'hidden', level: 3, at: [3, 3] }],
  ]);
  const { attempts, sessions } = buildAttempts(r, NOW);
  assert.deepEqual(attempts.map((a) => a.outcome), ['clear', 'leave', 'quit']);
  assert.equal(attempts[0].durationMs, 5000);
  assert.equal(attempts[0].nextLevelStarted, true);
  assert.equal(attempts[1].nextLevelStarted, true);
  assert.equal(attempts[2].nextLevelStarted, false);
  assert.equal(attempts[2].undos, 1);
  assert.equal(sessions[0].endReason, 'hidden');
  assert.equal(buildAttempts(r, T0 + 100).attempts[2].outcome, 'open', 'recent activity is still open');
  assert.equal(r[7].attemptId, `${SID}:6`);
  assert.equal(r[0].attemptId, null);
});

test('attempt rows: idle, hints, lost bodies, stuck-to-clear', () => {
  const r = rows([
    [0, 'session_start', { firstTime: false }],
    [1000, 'level_start', { level: 4, attempt: 2, bodies: 2 }],
    [2000, 'move', mv(4, [2, 1])],
    [50000, 'idle', { level: 4, idleMs: 45000 }],
    [50100, 'move', mv(4, [3, 1])],
    [60000, 'hint', { level: 4, body: 1, step: 2, at: [3, 1] }],
    [61000, 'body_lost', { level: 4, body: 1, at: [3, 1], reason: 'fell', wasted: true }],
    [61500, 'switch', { level: 4, body: 1, at: [3, 1] }],
    [62000, 'undo', { level: 4, body: 2, step: 0, at: [1, 1] }],
    [62100, 'undo', { level: 4, body: 2, step: 0, at: [1, 1] }],
    [62200, 'undo', { level: 4, body: 2, step: 0, at: [1, 1] }],
    [62300, 'undo', { level: 4, body: 2, step: 0, at: [1, 1] }],
    [62400, 'undo', { level: 4, body: 2, step: 0, at: [1, 1] }],
    [70000, 'move', mv(4, [2, 1])],
    [71000, 'move', mv(4, [3, 1])],
    [72000, 'level_clear', { level: 4, durationMs: 71000, moves: 4, bodiesUsed: 2 }],
  ]);
  const [a] = attemptRows(r, NOW, () => 12);
  assert.equal(a.outcome, 'clear');
  assert.equal(a.sessionFirstTime, false);
  assert.equal(a.idleCount, 1);
  assert.equal(a.idleMsTotal, 45000);
  assert.equal(a.activeMs, 71000 - 45000);
  assert.equal(a.firstHintMs, 59000);
  assert.equal(a.lostFell, 1);
  assert.equal(a.bodiesWasted, 1);
  assert.equal(a.switches, 1);
  assert.equal(a.undos, 5);
  assert.equal(a.minMoves, 12);
  // Last stuck point: the 5-undo streak ended at t=70000, then 2 moves to the clear at 72000.
  assert.equal(a.lastStuckToClearMoves, 2);
  assert.equal(a.lastStuckToClearMs, 2000);
});

test('level stats: drop-off counts the level a player stopped at', () => {
  const a = rows([
    [0, 'level_start', { level: 1, attempt: 1 }],
    [10, 'level_clear', { level: 1, durationMs: 10, moves: 7 }],
    [20, 'level_start', { level: 2, attempt: 1 }],
  ]);
  const b = rows([
    [0, 'level_start', { level: 1, attempt: 1 }],
    [30, 'level_clear', { level: 1, durationMs: 30, moves: 7 }],
    [40, 'level_start', { level: 2, attempt: 1 }],
    [50, 'level_clear', { level: 2, durationMs: 10, moves: 9 }],
  ], { sid: '44444444-4444-4444-8444-444444444444', pid: '55555555-5555-4555-8555-555555555555' });
  const s = levelStats([...a, ...b], [{ n: 1, name: 'one' }, { n: 2, name: 'two' }, { n: 3, name: 'three' }], NOW);
  assert.equal(s.length, 3);
  assert.equal(s[0].clearRate, 1);
  assert.equal(s[0].clearMsMedian, 20);
  assert.equal(s[1].players, 2);
  assert.equal(s[1].clearRate, 0.5);
  assert.equal(s[1].playerQuits, 1);
  assert.equal(s[1].playerQuitRate, 0.5);
  assert.equal(s[1].sessionEndRate, 0.5);
  assert.equal(s[2].players, 0);
});

test('heatmap groups by cell', () => {
  const r = rows([
    [0, 'idle', { level: 1, idleMs: 40000, at: [2, 1] }],
    [1, 'idle', { level: 1, idleMs: 30000, at: [2, 1] }],
    [2, 'undo', { level: 1, at: [3, 1] }],
    [3, 'body_lost', { level: 1, at: [4, 1], reason: 'fell', wasted: true }],
    [4, 'move', { ...mv(1, [5, 1], 'blocked'), from: [4, 1] }],
  ]);
  const cells = Object.fromEntries(heatmap(r).map((c) => [`${c.x},${c.y}`, c]));
  assert.equal(cells['2,1'].idle, 2);
  assert.equal(cells['2,1'].idleMs, 70000);
  assert.equal(cells['3,1'].undo, 1);
  assert.equal(cells['4,1'].lost_fell, 1);
  assert.equal(cells['4,1'].blocked, 1);
});

test('helpers: quantile and CSV escaping', () => {
  assert.equal(quantile([], 0.5), null);
  assert.equal(quantile([1, 2, 3, 4], 0.5), 3);
  assert.equal(quantile([10, 20, 30, 40, 50], 0.75), 40);
  assert.equal(toCsv([{ a: '한글, "따옴표"', b: { x: 1 }, c: null }]), 'a,b,c\n"한글, ""따옴표""","{""x"":1}",\n');
});
