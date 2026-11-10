// HTTP handler: event intake, stats API, dashboard, the web prototype at /play/ and the Unity WebGL build at /webgl/
// (same-origin playtests).
import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { readFile, stat, open } from 'node:fs/promises';
import { createReadStream } from 'node:fs';
import { join, normalize, extname, dirname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkEnvelope, checkEvent, MAX_BYTES } from './validate.mjs';
import { levelStats, chapterStats, stageHeatmap, timelines, toCsv, attemptRows, eventsJsonl, ATTEMPT_COLUMNS } from './stats.mjs';
import { PROTO_DIR, minMovesLookup } from './levels.mjs';
import { parseTime, checkTz, SERVER_TZ } from './time.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png',
  '.ico': 'image/x-icon', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.unityweb': 'application/octet-stream',
};
export const WEBGL_DIR = join(here, '../../Builds/WebGL');

// Precompressed files: Unity WebGL builds (gzip + decompression fallback) name them *.unityweb, others use *.gz / *.br.
// Content-Type comes from the inner extension (WebGL.wasm.unityweb -> application/wasm, so the browser can compile
// while streaming), and the encoding is declared only when the file really is compressed that way and the client
// accepts it. Without the header the bytes go out as they are and Unity's JS fallback unpacks them, so both paths load.
const COMPRESSED = { '.unityweb': null, '.gz': 'gzip', '.br': 'br' };
const GZIP_MAGIC = [0x1f, 0x8b];

async function sniffGzip(file) {
  const fh = await open(file, 'r');
  try {
    const { buffer, bytesRead } = await fh.read(Buffer.alloc(2), 0, 2, 0);
    return bytesRead === 2 && buffer[0] === GZIP_MAGIC[0] && buffer[1] === GZIP_MAGIC[1];
  } finally {
    await fh.close();
  }
}

const accepts = (req, enc) => (req.headers['accept-encoding'] ?? '').split(',').some((p) => {
  const [name, ...params] = p.trim().toLowerCase().split(';');
  return (name === enc || name === '*') && !params.some((q) => /^\s*q=0(\.0*)?\s*$/.test(q));
});

// { type, encoding } for a file to serve. encoding is null when the bytes go out unchanged.
export async function fileHeaders(file, req) {
  const outer = extname(file).toLowerCase();
  if (!(outer in COMPRESSED)) return { type: MIME[outer] ?? 'application/octet-stream', encoding: null };
  const inner = extname(file.slice(0, -outer.length)).toLowerCase();
  const type = MIME[inner] ?? 'application/octet-stream';
  // .unityweb may be gzip, Brotli or uncompressed depending on the build setting: only gzip has a magic number.
  // Brotli .unityweb gets no header (the fallback handles it; br also needs HTTPS in browsers).
  const enc = COMPRESSED[outer] ?? ((await sniffGzip(file)) ? 'gzip' : null);
  return { type, encoding: enc && accepts(req, enc) ? enc : null };
}

const DEFAULT_ORIGINS = [
  /^https?:\/\/(localhost|127\.0\.0\.1|\[::1\])(:\d+)?$/,
  /^https:\/\/([a-z0-9-]+\.)*itch\.io$/,
  /^https:\/\/([a-z0-9-]+\.)*itch\.zone$/,
  /^https:\/\/([a-z0-9-]+\.)*hwcdn\.net$/, // itch.io HTML5 game CDN
];

export function originMatcher(extra = []) {
  const extraRx = extra.filter(Boolean).map((o) => {
    if (o.startsWith('*.')) {
      const dom = o.slice(2).replace(/\./g, '\\.');
      return new RegExp(`^https://([a-z0-9-]+\\.)*${dom}$`);
    }
    return new RegExp(`^${o.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\/$/, '')}$`);
  });
  const all = [...DEFAULT_ORIGINS, ...extraRx];
  return (origin) => all.some((rx) => rx.test(origin));
}

// Token bucket per hashed client address. Addresses are hashed with a per-process salt and never stored.
export function rateLimiter({ capacity, perSec }) {
  const salt = randomBytes(16);
  const buckets = new Map();
  let lastSweep = Date.now();
  return (addr, cost = 1, now = Date.now()) => {
    if (now - lastSweep > 60000) {
      for (const [k, b] of buckets) if (now - b.t > 600000) buckets.delete(k);
      lastSweep = now;
    }
    const key = createHash('sha256').update(salt).update(String(addr)).digest('base64url').slice(0, 16);
    const b = buckets.get(key) ?? { tokens: capacity, t: now };
    b.tokens = Math.min(capacity, b.tokens + ((now - b.t) / 1000) * perSec);
    b.t = now;
    buckets.set(key, b);
    if (b.tokens < cost) return Math.ceil((cost - b.tokens) / perSec);
    b.tokens -= cost;
    return 0;
  };
}

function readBody(req, limit) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    // Over the limit: keep draining (so the 413 reaches the client) but stop buffering.
    req.on('data', (c) => {
      size += c.length;
      if (size <= limit) chunks.push(c);
    });
    req.on('end', () => (size > limit ? reject(Object.assign(new Error('too large'), { status: 413 })) : resolve(Buffer.concat(chunks).toString('utf8'))));
    req.on('error', reject);
  });
}

// limits: per client address, requests ({capacity, perSec}) and events ({capacity, perSec}).
export const DEFAULT_LIMITS = { requests: { capacity: 60, perSec: 2 }, events: { capacity: 3000, perSec: 50 } };

export function createApp({ store, token, levels = [], allowedOrigins = [], trustProxy = false, protoDir = PROTO_DIR, webglDir = WEBGL_DIR, now = () => Date.now(), limits = DEFAULT_LIMITS }) {
  const allowOrigin = originMatcher(allowedOrigins);
  const reqLimit = rateLimiter(limits.requests);
  const eventLimit = rateLimiter(limits.events);
  const tokenBuf = Buffer.from(token);
  let dashboardHtml = null;

  const send = (res, status, body, headers = {}) => {
    const isStr = typeof body === 'string' || Buffer.isBuffer(body);
    res.writeHead(status, {
      'Content-Type': isStr ? 'text/plain; charset=utf-8' : 'application/json; charset=utf-8',
      'X-Content-Type-Options': 'nosniff',
      ...headers,
    });
    res.end(isStr ? body : JSON.stringify(body));
  };

  const authed = (req, url) => {
    const h = req.headers.authorization ?? '';
    const given = Buffer.from(h.startsWith('Bearer ') ? h.slice(7) : (url.searchParams.get('token') ?? ''));
    return given.length === tokenBuf.length && timingSafeEqual(given, tokenBuf);
  };

  const minMovesFor = minMovesLookup(levels);
  // client, build, lang, from/since and to filter on build/client/language and server receive time. Times: epoch ms,
  // ISO with offset, or a date/date-time without offset read in ?tz= (IANA or +09:00), default the server's zone
  // (time.mjs). Returns { error } for a bad time or zone instead of silently not filtering.
  const filters = (url) => {
    const f = {};
    for (const k of ['client', 'build', 'lang']) if (url.searchParams.get(k)) f[k] = url.searchParams.get(k);
    let tz = SERVER_TZ;
    if (url.searchParams.get('tz')) {
      try { tz = checkTz(url.searchParams.get('tz')); } catch { return { error: `unknown tz "${url.searchParams.get('tz')}"` }; }
    }
    for (const [k, v] of [['from', url.searchParams.get('from') ?? url.searchParams.get('since')], ['to', url.searchParams.get('to')]]) {
      if (!v) continue;
      f[k] = parseTime(v, tz);
      if (f[k] == null) return { error: `bad time "${v}" (epoch ms, YYYY-MM-DD, YYYY-MM-DDTHH:mm or ISO with offset)` };
    }
    return f;
  };

  async function intake(req, res, cors) {
    const addr = (trustProxy && req.headers['x-forwarded-for']?.split(',')[0].trim()) || req.socket.remoteAddress;
    const wait = reqLimit(addr);
    if (wait) return send(res, 429, { error: 'rate limited' }, { ...cors, 'Retry-After': String(wait) });
    const ct = (req.headers['content-type'] ?? '').split(';')[0].trim();
    if (ct && ct !== 'application/json' && ct !== 'text/plain') { req.resume(); return send(res, 415, { error: 'use application/json or text/plain' }, cors); }
    if (Number(req.headers['content-length']) > MAX_BYTES) { req.resume(); return send(res, 413, { error: `body over ${MAX_BYTES} bytes` }, cors); }
    let raw;
    try { raw = await readBody(req, MAX_BYTES); } catch (err) {
      return send(res, err.status ?? 400, { error: err.status === 413 ? `body over ${MAX_BYTES} bytes` : 'bad body' }, cors);
    }
    let body;
    try { body = JSON.parse(raw); } catch { return send(res, 400, { error: 'invalid JSON' }, cors); }
    const envErr = checkEnvelope(body);
    if (envErr) return send(res, 400, { error: envErr }, cors);
    const wait2 = eventLimit(addr, body.events.length);
    if (wait2) return send(res, 429, { error: 'rate limited' }, { ...cors, 'Retry-After': String(wait2) });
    const good = [];
    const rejected = [];
    for (const e of body.events) {
      const err = checkEvent(e);
      if (err) rejected.push({ seq: e?.seq ?? null, error: err });
      else good.push(e);
    }
    const r = good.length ? store.insertBatch(body, good, now()) : { accepted: 0, duplicates: 0 };
    return send(res, 200, { ...r, rejected }, cors);
  }

  // Streams a file under root. Precompressed files (WebGL build) get Content-Encoding when possible, see fileHeaders.
  async function serveStatic(req, res, rel, root) {
    const file = normalize(join(root, rel || 'index.html'));
    if (!file.startsWith(normalize(root) + sep) && file !== normalize(root)) return send(res, 404, 'not found');
    let st;
    try { st = await stat(file); } catch { return send(res, 404, 'not found'); }
    if (st.isDirectory()) return serveStatic(req, res, join(rel, 'index.html'), root);
    const { type, encoding } = await fileHeaders(file, req);
    const compressed = extname(file).toLowerCase() in COMPRESSED;
    res.writeHead(200, {
      'Content-Type': type, 'Content-Length': st.size, 'Cache-Control': 'no-cache', 'X-Content-Type-Options': 'nosniff',
      ...(encoding ? { 'Content-Encoding': encoding } : {}),
      ...(compressed ? { Vary: 'Accept-Encoding' } : {}),
    });
    if (req.method === 'HEAD') return res.end();
    await new Promise((ok) => createReadStream(file).on('error', () => { res.destroy(); ok(); }).on('end', ok).pipe(res));
  }

  // The origin the browser used to reach us (proxy headers only with trustProxy, e.g. behind a tunnel).
  const selfOrigin = (req) => {
    const proto = (trustProxy && req.headers['x-forwarded-proto']?.split(',')[0].trim()) || 'http';
    const host = (trustProxy && req.headers['x-forwarded-host']?.split(',')[0].trim()) || req.headers.host || 'localhost';
    return `${proto}://${host}`;
  };

  return async function handle(req, res) {
    const url = new URL(req.url, 'http://local');
    const path = url.pathname;
    const origin = req.headers.origin;
    try {
      if (path === '/v1/events') {
        // Same origin (the build served from /webgl/ on this server, e.g. a phone on the LAN via http://<PC IP>:8787)
        // is always allowed: browsers send Origin on every POST, and a LAN address is not on the allow list.
        if (origin && origin !== selfOrigin(req) && !allowOrigin(origin)) return send(res, 403, { error: 'origin not allowed' });
        const cors = origin ? { 'Access-Control-Allow-Origin': origin, Vary: 'Origin' } : {};
        if (req.method === 'OPTIONS') {
          return send(res, 204, '', { ...cors, 'Access-Control-Allow-Methods': 'POST', 'Access-Control-Allow-Headers': 'Content-Type', 'Access-Control-Max-Age': '86400' });
        }
        if (req.method !== 'POST') return send(res, 405, { error: 'POST only' }, cors);
        return await intake(req, res, cors);
      }
      if (req.method !== 'GET' && req.method !== 'HEAD') return send(res, 405, { error: 'method not allowed' });
      if (path === '/healthz') return send(res, 200, { ok: true });
      if (path === '/') return send(res, 302, '', { Location: '/play/' });
      if (path === '/play') return send(res, 302, '', { Location: '/play/' });
      if (path.startsWith('/play/')) return await serveStatic(req, res, decodeURIComponent(path.slice(6)), protoDir);
      if (path === '/webgl') return send(res, 302, '', { Location: '/webgl/' + url.search });
      if (path === '/webgl/' || path === '/webgl/index.html') {
        // The Unity build sends only when told where (AppConfig.telemetryEndpoint is empty): add ?telemetry=<this server>.
        if (!url.searchParams.has('telemetry')) {
          url.searchParams.set('telemetry', selfOrigin(req));
          return send(res, 302, '', { Location: '/webgl/' + url.search });
        }
        return await serveStatic(req, res, 'index.html', webglDir);
      }
      if (path.startsWith('/webgl/')) return await serveStatic(req, res, decodeURIComponent(path.slice(7)), webglDir);
      if (path === '/dashboard') {
        dashboardHtml ??= await readFile(join(here, 'dashboard.html'));
        return send(res, 200, dashboardHtml, { 'Content-Type': MIME['.html'], 'Cache-Control': 'no-cache' });
      }
      if (path.startsWith('/api/')) {
        if (!authed(req, url)) return send(res, 401, { error: 'token required' });
        const f = filters(url);
        if (f.error) return send(res, 400, { error: f.error });
        const t = now();
        switch (path) {
          case '/api/levels': return send(res, 200, levels);
          case '/api/stats/overview': return send(res, 200, store.facets());
          case '/api/stats/levels': return send(res, 200, levelStats(store.query(f), levels, t));
          case '/api/stats/chapters': return send(res, 200, chapterStats(store.query(f), levels, t));
          case '/api/stats/heatmap': {
            const n = Number(url.searchParams.get('level'));
            if (!n) return send(res, 400, { error: 'level required' });
            return send(res, 200, stageHeatmap(store.query(f), levels, n, t));
          }
          case '/api/stats/timeline':
            return send(res, 200, timelines(store.query(f), { limit: Number(url.searchParams.get('limit')) || 50, now: t }));
          case '/api/export/attempts.csv':
            return send(res, 200, toCsv(attemptRows(store.query(f), t, minMovesFor, levels), ATTEMPT_COLUMNS), { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': 'attachment; filename="attempts.csv"' });
          case '/api/export/levels.csv':
            return send(res, 200, toCsv(levelStats(store.query(f), levels, t).map(({ namesSeen, ...r }) => r)), { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': 'attachment; filename="levels.csv"' });
          case '/api/export/levels.json':
            return send(res, 200, levelStats(store.query(f), levels, t), { 'Content-Disposition': 'attachment; filename="levels.json"' });
          case '/api/export/attempts.json':
            return send(res, 200, attemptRows(store.query(f), t, minMovesFor, levels), { 'Content-Disposition': 'attachment; filename="attempts.json"' });
          case '/api/export/events.jsonl':
            return send(res, 200, eventsJsonl(store.query(f), t), { 'Content-Type': 'application/x-ndjson; charset=utf-8', 'Content-Disposition': 'attachment; filename="events.jsonl"' });
          case '/api/export/events.json':
            return send(res, 200, store.query(f), { 'Content-Disposition': 'attachment; filename="events.json"' });
          default: return send(res, 404, { error: 'unknown endpoint' });
        }
      }
      return send(res, 404, 'not found');
    } catch (err) {
      console.error('[server]', err);
      if (!res.headersSent) send(res, 500, { error: 'internal error' });
    }
  };
}
