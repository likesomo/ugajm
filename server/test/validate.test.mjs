import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkEnvelope, checkEvent } from '../src/validate.mjs';
import { envelope, T0 } from './helpers.mjs';

const ev = (type, f = {}) => ({ seq: 0, t: T0, type, ...f });

test('envelope: accepts a valid batch, rejects bad headers', () => {
  assert.equal(checkEnvelope(envelope([ev('session_start')])), null);
  assert.match(checkEnvelope(envelope([ev('session_start')], { v: 2 })), /version/);
  assert.match(checkEnvelope(envelope([ev('session_start')], { playerId: 'bob@example.com' })), /playerId/);
  assert.match(checkEnvelope(envelope([ev('session_start')], { client: 'ios' })), /client/);
  assert.match(checkEnvelope(envelope([])), /non-empty/);
  assert.match(checkEnvelope(envelope(Array.from({ length: 101 }, (_, i) => ev('hint', { seq: i })))), /at most 100/);
  assert.match(checkEnvelope([]), /object/);
});

test('event: required and optional fields', () => {
  const move = { level: 3, body: 1, step: 2, dir: 'up', from: [1, 1], to: [1, 0], result: 'blocked', blockedBy: 'wall' };
  assert.equal(checkEvent(ev('move', move)), null);
  assert.equal(checkEvent(ev('move', { ...move, to: null, blockedBy: 'edge' })), null);
  assert.match(checkEvent(ev('move', { ...move, step: 8 })), /step/);
  assert.match(checkEvent(ev('move', { ...move, result: 'teleport' })), /result/);
  assert.match(checkEvent(ev('move', { ...move, from: undefined })), /from is required|from is invalid/);
  assert.match(checkEvent(ev('level_start', { level: 1 })), /attempt/);
  assert.equal(checkEvent(ev('level_start', { level: 1, attempt: 1, levelKey: 'a1070f9a', via: 'boot' })), null);
  assert.match(checkEvent(ev('level_start', { level: 1, attempt: 1, levelKey: 'XYZ' })), /levelKey/);
  assert.match(checkEvent(ev('nope')), /unknown type/);
  assert.match(checkEvent({ ...ev('hint', { level: 1, body: 1, step: 0, at: [1, 1] }), t: 123 }), /epoch/);
  assert.match(checkEvent(ev('session_end', { reason: 'hidden', extra: 'x'.repeat(3000) })), /2KB/);
});
