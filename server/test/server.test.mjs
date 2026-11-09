import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { startServer, envelope, post, get, T0, TOKEN } from './helpers.mjs';
import { generate } from '../tools/fake-data.mjs';

let srv;
before(async () => { srv = await startServer(); });
after(() => srv.close());

const events = [
  { seq: 0, t: T0, type: 'session_start', firstTime: true, cleared: 0 },
  { seq: 1, t: T0 + 10, type: 'level_start', level: 1, levelName: '일곱 걸음', attempt: 1, via: 'boot', bodies: 3 },
  { seq: 2, t: T0 + 20, type: 'move', level: 1, body: 1, step: 1, dir: 'right', from: [1, 1], to: [2, 1], result: 'moved' },
];

test('intake: stores valid events, ignores duplicates, reports bad ones', async () => {
  let res = await post(srv.url, envelope(events));
  assert.equal(res.status, 200);
  assert.deepEqual(await res.json(), { accepted: 3, duplicates: 0, rejected: [] });
  res = await post(srv.url, envelope([...events, { seq: 3, t: T0, type: 'bogus' }]));
  assert.deepEqual(await res.json(), { accepted: 0, duplicates: 3, rejected: [{ seq: 3, error: 'unknown type "bogus"' }] });
  const row = srv.store.db.prepare('SELECT * FROM events WHERE seq = 2').get();
  assert.equal(row.x, 2);
  assert.equal(row.level, 1);
  assert.equal(JSON.parse(row.data).dir, 'right');
});

test('intake: text/plain (sendBeacon), CORS allow list, errors', async () => {
  const sid = '33333333-3333-4333-8333-333333333333';
  let res = await post(srv.url, envelope([events[0]], { sessionId: sid }), { 'Content-Type': 'text/plain', Origin: 'https://someone.itch.io' });
  assert.equal(res.status, 200);
  assert.equal(res.headers.get('access-control-allow-origin'), 'https://someone.itch.io');
  res = await post(srv.url, envelope([events[0]]), { Origin: 'https://evil.example' });
  assert.equal(res.status, 403);
  res = await fetch(`${srv.url}/v1/events`, { method: 'OPTIONS', headers: { Origin: 'http://localhost:8777' } });
  assert.equal(res.status, 204);
  assert.equal(res.headers.get('access-control-allow-origin'), 'http://localhost:8777');
  res = await post(srv.url, '{not json');
  assert.equal(res.status, 400);
  res = await post(srv.url, 'x'.repeat(70000));
  assert.equal(res.status, 413);
  res = await post(srv.url, envelope([events[0]]), { 'Content-Type': 'application/xml' });
  assert.equal(res.status, 415);
});

test('intake: no IP or user agent is stored', async () => {
  const cols = srv.store.db.prepare('PRAGMA table_info(events)').all().map((c) => c.name);
  assert.ok(!cols.some((c) => /ip|agent|addr/i.test(c)), cols.join(','));
});

test('rate limit answers 429 with Retry-After', async () => {
  const s = await startServer();
  try {
    let last;
    for (let i = 0; i < 70; i++) last = await post(s.url, '{}');
    assert.equal(last.status, 429);
    assert.ok(Number(last.headers.get('retry-after')) >= 1);
  } finally { await s.close(); }
});

test('stats API needs the token', async () => {
  assert.equal((await get(srv.url, '/api/stats/levels', '')).status, 401);
  assert.equal((await get(srv.url, '/api/stats/levels', 'wrong-token')).status, 401);
  assert.equal((await get(srv.url, '/api/stats/levels')).status, 200);
  assert.equal((await fetch(`${srv.url}/api/stats/levels?token=${TOKEN}`)).status, 200);
});

test('dashboard, prototype and health are served', async () => {
  const d = await fetch(`${srv.url}/dashboard`);
  assert.equal(d.status, 200);
  assert.match(await d.text(), /플레이 분석/);
  const p = await fetch(`${srv.url}/play/`);
  assert.match(await p.text(), /일곱 걸음/);
  assert.equal((await fetch(`${srv.url}/play/src/telemetry.js`)).headers.get('content-type'), 'text/javascript; charset=utf-8');
  assert.equal((await fetch(`${srv.url}/play/..%2F..%2Fserver%2Fsrc%2Fapp.mjs`)).status, 404);
  assert.equal((await fetch(`${srv.url}/healthz`)).status, 200);
});

test('fake playtest: posts through HTTP and aggregates', async () => {
  const big = { capacity: 1e6, perSec: 1e6 };
  const s = await startServer({ limits: { requests: big, events: big } });
  try {
    const batches = generate({ players: 30, seed: 3, startT: Date.now() - 86400000 * 2 });
    let accepted = 0;
    for (const b of batches) {
      const j = await (await post(s.url, b)).json();
      assert.deepEqual(j.rejected, []);
      accepted += j.accepted;
    }
    assert.equal(accepted, batches.reduce((a, b) => a + b.events.length, 0));

    const levels = await (await get(s.url, '/api/stats/levels')).json();
    assert.equal(levels.length, s.levels.length);
    const l1 = levels[0];
    assert.equal(l1.players, 30);
    assert.ok(l1.clearRate > 0 && l1.clearRate <= 1);
    for (const l of levels) {
      assert.ok(l.clearPlayers <= l.players);
      if (l.clearMsMedian != null) assert.ok(l.clearMsP75 >= l.clearMsMedian);
    }
    assert.ok(levels.some((l) => l.playerQuits > 0), 'some players quit somewhere');

    const hm = await (await get(s.url, '/api/stats/heatmap?level=8')).json();
    assert.equal(hm.level.n, 8);
    assert.ok(hm.cells.length > 0);
    for (const c of hm.cells) assert.ok(c.x < hm.level.w && c.y < hm.level.h);

    const tl = await (await get(s.url, '/api/stats/timeline')).json();
    assert.equal(tl.length, 30);
    assert.ok(tl[0].sessions[0].attempts.length > 0);

    const csv = await (await get(s.url, '/api/export/attempts.csv?client=web-proto')).text();
    const [head, ...lines] = csv.trim().split('\n');
    assert.match(head, /^attemptId,playerId,sessionId,build,client,level,levelName/);
    assert.ok(lines.length > 0);
    assert.ok(lines.every((l) => l.includes(',web-proto,')));
    const jsonl = (await (await get(s.url, '/api/export/events.jsonl')).text()).trim().split('\n').map((l) => JSON.parse(l));
    assert.equal(jsonl.length, accepted);
    assert.ok(jsonl.filter((e) => e.type === 'move').every((e) => e.attemptId));
    const future = await (await get(s.url, `/api/export/attempts.json?since=${new Date(Date.now() + 3600000).toISOString()}`)).json();
    assert.equal(future.length, 0);
  } finally { await s.close(); }
});

test('WebGL build at /webgl/: adds ?telemetry=<this server>, streams the files', async () => {
  const { mkdtempSync, mkdirSync, writeFileSync } = await import('node:fs');
  const { tmpdir } = await import('node:os');
  const { join } = await import('node:path');
  const dir = mkdtempSync(join(tmpdir(), 'webgl-'));
  mkdirSync(join(dir, 'Build'));
  writeFileSync(join(dir, 'index.html'), '<title>Seven Steps</title>');
  writeFileSync(join(dir, 'Build', 'WebGL.wasm.unityweb'), Buffer.alloc(1000, 7));
  const w = await startServer({ webglDir: dir });
  const proxied = await startServer({ webglDir: dir, trustProxy: true });
  try {
    const r = await fetch(`${w.url}/webgl/?level=8`, { redirect: 'manual' });
    assert.equal(r.status, 302);
    const loc = new URL(r.headers.get('location'), w.url);
    assert.equal(loc.searchParams.get('level'), '8');
    assert.equal(loc.searchParams.get('telemetry'), w.url);
    assert.equal(new URL((await fetch(`${w.url}/webgl`, { redirect: 'manual' })).headers.get('location'), w.url).pathname, '/webgl/');
    // Behind a tunnel the browser's origin comes from the proxy headers.
    const t = await fetch(`${proxied.url}/webgl/`, { redirect: 'manual', headers: { 'X-Forwarded-Proto': 'https', 'X-Forwarded-Host': 'x.trycloudflare.com' } });
    assert.equal(new URL(t.headers.get('location'), proxied.url).searchParams.get('telemetry'), 'https://x.trycloudflare.com');
    // An explicit telemetry=off (or another server) is kept.
    const off = await fetch(`${w.url}/webgl/?telemetry=off`);
    assert.equal(off.status, 200);
    assert.match(await off.text(), /Seven Steps/);
    const b = await fetch(`${w.url}/webgl/Build/WebGL.wasm.unityweb`);
    assert.equal(b.headers.get('content-length'), '1000');
    assert.equal((await b.arrayBuffer()).byteLength, 1000);
    assert.equal((await fetch(`${w.url}/webgl/..%2F..%2Fsecret`)).status, 404);
  } finally {
    await w.close();
    await proxied.close();
  }
});

test('language: carried forward within a session across batches, filterable, listed in the overview', async () => {
  const w = await startServer();
  try {
    const sid = '33333333-3333-4333-8333-3333333333a1';
    const first = [
      { seq: 0, t: T0, type: 'session_start', firstTime: true, cleared: 0, lang: 'en' },
      { seq: 1, t: T0 + 10, type: 'level_start', level: 1, levelName: '일곱 걸음', attempt: 1, via: 'boot', bodies: 3, lang: 'en' },
    ];
    const later = [{ seq: 2, t: T0 + 20, type: 'move', level: 1, body: 1, step: 1, dir: 'right', from: [1, 1], to: [2, 1], result: 'moved' }];
    assert.equal((await post(w.url, envelope(first, { sessionId: sid }))).status, 200);
    assert.equal((await post(w.url, envelope(later, { sessionId: sid }))).status, 200);
    assert.equal((await post(w.url, envelope([{ seq: 0, t: T0, type: 'session_start', lang: 'ko' }]))).status, 200);
    const bad = await (await post(w.url, envelope([{ seq: 0, t: T0, type: 'session_start', lang: 'English!' }], { sessionId: '33333333-3333-4333-8333-3333333333a2' }))).json();
    assert.equal(bad.rejected[0].error, 'session_start.lang is invalid');
    const ov = await (await get(w.url, '/api/stats/overview')).json();
    assert.deepEqual(ov.langs.map((l) => l.lang).sort(), ['en', 'ko']);
    const en = await (await get(w.url, '/api/export/events.json?lang=en')).json();
    assert.deepEqual(en.map((e) => e.type), ['session_start', 'level_start', 'move']);
  } finally {
    await w.close();
  }
});

test('an events table from before the lang column gets it on open', async () => {
  const { DatabaseSync } = await import('node:sqlite');
  const { mkdtempSync } = await import('node:fs');
  const { tmpdir } = await import('node:os');
  const { join } = await import('node:path');
  const { openDb } = await import('../src/db.mjs');
  const file = join(mkdtempSync(join(tmpdir(), 'olddb-')), 'old.sqlite');
  const old = new DatabaseSync(file);
  old.exec(`CREATE TABLE events (id INTEGER PRIMARY KEY, received_at INTEGER NOT NULL, v INTEGER NOT NULL, player_id TEXT NOT NULL,
    session_id TEXT NOT NULL, seq INTEGER NOT NULL, t INTEGER NOT NULL, type TEXT NOT NULL, client TEXT NOT NULL, build TEXT NOT NULL,
    level INTEGER, x INTEGER, y INTEGER, data TEXT NOT NULL, UNIQUE (session_id, seq))`);
  old.exec(`INSERT INTO events (received_at, v, player_id, session_id, seq, t, type, client, build, data) VALUES (1, 1, 'p', 's', 0, ${T0}, 'session_start', 'web-proto', 'b', '{}')`);
  old.close();
  const store = openDb(file);
  try {
    assert.equal(store.query().length, 1);
    assert.equal(store.insertBatch(envelope([{ seq: 0, t: T0, type: 'session_start', lang: 'en' }]), [{ seq: 0, t: T0, type: 'session_start', lang: 'en' }]).accepted, 1);
    assert.equal(store.query({ lang: 'en' }).length, 1);
  } finally {
    store.close();
  }
});
