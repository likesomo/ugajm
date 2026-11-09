import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseTime, checkTz } from '../src/time.mjs';
import { startServer, post, get, envelope } from './helpers.mjs';

const iso = (t) => new Date(t).toISOString();

test('parseTime: dates without an offset are wall-clock time in the zone', () => {
  assert.equal(iso(parseTime('2026-11-10', 'Asia/Seoul')), '2026-11-09T15:00:00.000Z');
  assert.equal(iso(parseTime('2026-11-10', '+09:00')), '2026-11-09T15:00:00.000Z');
  assert.equal(iso(parseTime('2026-11-10T09:30', 'Asia/Seoul')), '2026-11-10T00:30:00.000Z');
  assert.equal(iso(parseTime('2026-11-10', 'UTC')), '2026-11-10T00:00:00.000Z');
  assert.equal(iso(parseTime('2026-11-10', '-05:00')), '2026-11-10T05:00:00.000Z');
  // Daylight saving: New York is UTC-4 after 2026-03-08 02:00, UTC-5 before.
  assert.equal(iso(parseTime('2026-03-07', 'America/New_York')), '2026-03-07T05:00:00.000Z');
  assert.equal(iso(parseTime('2026-03-09', 'America/New_York')), '2026-03-09T04:00:00.000Z');
});

test('parseTime: epoch ms and ISO with an offset are taken as is; junk is null', () => {
  assert.equal(parseTime('1794112353226', 'Asia/Seoul'), 1794112353226);
  assert.equal(iso(parseTime('2026-11-10T00:00Z', 'Asia/Seoul')), '2026-11-10T00:00:00.000Z');
  assert.equal(iso(parseTime('2026-11-10T09:00+09:00', 'UTC')), '2026-11-10T00:00:00.000Z');
  for (const bad of ['abc', '2026-13-01', '2026-02-30', '2026-11-10T25:00', '']) assert.equal(parseTime(bad, 'UTC'), null, bad);
  assert.throws(() => checkTz('Mars/Olympus'));
  assert.equal(checkTz('+09:00'), '+09:00');
});

test('export since: local date (tz), epoch ms, bad values are 400', async () => {
  // Received at 2026-11-10 08:00 KST = 2026-11-09 23:00 UTC: "since=2026-11-10" in UTC would miss it.
  const received = Date.parse('2026-11-09T23:00:00Z');
  const s = await startServer({ now: () => received });
  try {
    const sessionStart = { seq: 0, t: received, type: 'session_start', firstTime: true, cleared: 0, resumed: false };
    assert.equal((await post(s.url, envelope([sessionStart]))).status, 200);
    const count = async (q) => (await (await get(s.url, `/api/export/events.json?${q}`)).json()).length;
    assert.equal(await count('since=2026-11-10&tz=Asia/Seoul'), 1);
    assert.equal(await count('since=2026-11-10&tz=%2B09:00'), 1);
    assert.equal(await count('since=2026-11-10&tz=UTC'), 0);
    assert.equal(await count('since=2026-11-10T00:00%2B09:00'), 1, 'ISO with offset');
    assert.equal(await count(`from=${received - 1000}`), 1, 'epoch ms (dashboard period filter)');
    assert.equal(await count(`from=${received + 1000}`), 0, 'epoch ms filters');
    assert.equal(await count('since=2026-11-10&to=2026-11-11&tz=Asia/Seoul'), 1);
    assert.equal((await get(s.url, '/api/export/events.json?since=yesterday')).status, 400);
    assert.equal((await get(s.url, '/api/export/events.json?since=2026-11-10&tz=Nowhere/City')).status, 400);
  } finally {
    await s.close();
  }
});
