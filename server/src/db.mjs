// SQLite storage (node:sqlite). One raw events table; aggregation happens in stats.mjs.
import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

const SCHEMA = `
CREATE TABLE IF NOT EXISTS events (
  id          INTEGER PRIMARY KEY,
  received_at INTEGER NOT NULL,          -- server ms
  v           INTEGER NOT NULL,
  player_id   TEXT    NOT NULL,
  session_id  TEXT    NOT NULL,
  seq         INTEGER NOT NULL,
  t           INTEGER NOT NULL,          -- client ms
  type        TEXT    NOT NULL,
  client      TEXT    NOT NULL,
  build       TEXT    NOT NULL,
  level       INTEGER,
  x           INTEGER,                   -- cell of the event (at, or 'to' for move)
  y           INTEGER,
  data        TEXT    NOT NULL,          -- the event JSON minus seq/t/type
  lang        TEXT,                      -- UI language: the event's lang, else the session's last known one
  UNIQUE (session_id, seq)
);
CREATE INDEX IF NOT EXISTS events_type_level ON events (type, level);
CREATE INDEX IF NOT EXISTS events_player_t ON events (player_id, t);
CREATE INDEX IF NOT EXISTS events_received ON events (received_at);
CREATE INDEX IF NOT EXISTS events_build ON events (build, client);
`;

// Columns added after the first release; ALTER on older files.
const ADDED = [['lang', 'TEXT']];

export function openDb(path) {
  if (path !== ':memory:') mkdirSync(dirname(path), { recursive: true });
  const db = new DatabaseSync(path);
  db.exec('PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;');
  db.exec(SCHEMA);
  const have = new Set(db.prepare('PRAGMA table_info(events)').all().map((c) => c.name));
  for (const [col, type] of ADDED) if (!have.has(col)) db.exec(`ALTER TABLE events ADD COLUMN ${col} ${type}`);
  db.exec('CREATE INDEX IF NOT EXISTS events_lang ON events (lang)');
  const lastLang = db.prepare('SELECT lang FROM events WHERE session_id = ? AND seq < ? AND lang IS NOT NULL ORDER BY seq DESC LIMIT 1');
  const insert = db.prepare(`INSERT OR IGNORE INTO events
    (received_at, v, player_id, session_id, seq, t, type, client, build, level, x, y, data, lang)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`);

  // Inserts validated events of one envelope. Returns { accepted, duplicates }.
  function insertBatch(env, events, receivedAt = Date.now()) {
    let accepted = 0;
    db.exec('BEGIN');
    try {
      // Language carries forward within a session (session_start / level_start send it).
      let lang = events.length ? lastLang.get(env.sessionId, events[0].seq)?.lang ?? null : null;
      for (const e of events) {
        if (e.lang) lang = e.lang;
        const { seq, t, type, ...rest } = e;
        const cell = rest.at ?? rest.to ?? null;
        const r = insert.run(receivedAt, env.v, env.playerId, env.sessionId, seq, t, type, env.client, env.build,
          rest.level ?? null, cell ? cell[0] : null, cell ? cell[1] : null, JSON.stringify(rest), lang);
        accepted += Number(r.changes);
      }
      db.exec('COMMIT');
    } catch (err) {
      db.exec('ROLLBACK');
      throw err;
    }
    return { accepted, duplicates: events.length - accepted };
  }

  // Rows matching dashboard filters. f: { client, build, lang, from, to, types, level }
  function query(f = {}, extraWhere = '') {
    const where = [];
    const args = [];
    if (f.client) { where.push('client = ?'); args.push(f.client); }
    if (f.build) { where.push('build = ?'); args.push(f.build); }
    if (f.lang) { where.push('lang = ?'); args.push(f.lang); }
    if (f.from) { where.push('received_at >= ?'); args.push(Number(f.from)); }
    if (f.to) { where.push('received_at < ?'); args.push(Number(f.to)); }
    if (f.level) { where.push('level = ?'); args.push(Number(f.level)); }
    if (f.types) { where.push(`type IN (${f.types.map(() => '?').join(',')})`); args.push(...f.types); }
    if (extraWhere) where.push(extraWhere);
    const sql = `SELECT player_id, session_id, seq, t, received_at, type, client, build, level, x, y, data, lang FROM events
      ${where.length ? `WHERE ${where.join(' AND ')}` : ''} ORDER BY player_id, session_id, seq`;
    return db.prepare(sql).all(...args).map((r) => ({ ...r, data: JSON.parse(r.data) }));
  }

  function facets() {
    return {
      builds: db.prepare('SELECT build, client, COUNT(*) AS n, MAX(received_at) AS last FROM events GROUP BY build, client ORDER BY last DESC').all(),
      langs: db.prepare('SELECT lang, COUNT(DISTINCT session_id) AS sessions FROM events WHERE lang IS NOT NULL GROUP BY lang ORDER BY sessions DESC').all(),
      totals: db.prepare('SELECT COUNT(*) AS events, COUNT(DISTINCT player_id) AS players, COUNT(DISTINCT session_id) AS sessions FROM events').get(),
    };
  }

  return { db, insertBatch, query, facets, close: () => db.close() };
}
