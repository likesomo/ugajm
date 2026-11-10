// Seven Steps telemetry server. node server/src/index.mjs   (settings: server/README.md)
import { createServer } from 'node:http';
import { randomBytes } from 'node:crypto';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { openDb } from './db.mjs';
import { loadLevels } from './levels.mjs';
import { createApp, WEBGL_DIR } from './app.mjs';
import { existsSync } from 'node:fs';
import { networkInterfaces } from 'node:os';

const here = dirname(fileURLToPath(import.meta.url));
const env = process.env;
const port = Number(env.PORT ?? 8787);
const host = env.HOST ?? '0.0.0.0';
const dbPath = env.DB_PATH ?? join(here, '../data/telemetry.sqlite');
let token = env.STATS_TOKEN;
if (!token) {
  token = randomBytes(12).toString('base64url');
  console.warn('[server] STATS_TOKEN not set; using a random token for this run.');
}

const store = openDb(dbPath);
const levels = await loadLevels();
const webglDir = env.WEBGL_DIR ?? WEBGL_DIR;
const app = createApp({
  store, token, levels,
  allowedOrigins: (env.ALLOWED_ORIGINS ?? '').split(',').map((s) => s.trim()),
  trustProxy: env.TRUST_PROXY === '1',
  webglDir,
});

const server = createServer(app);
server.listen(port, host, () => {
  const base = `http://localhost:${port}`;
  console.log(`[server] db ${dbPath}, ${levels.length} levels`);
  console.log(`[server] play       ${base}/play/`);
  console.log(`[server] webgl      ${base}/webgl/` + (existsSync(join(webglDir, 'index.html')) ? '' : `   (no build in ${webglDir}; set WEBGL_DIR)`));
  console.log(`[server] dashboard  ${base}/dashboard#token=${token}`);
  // Same Wi-Fi devices: http://<this PC's address>:<port>/webgl/ (listening on all addresses unless HOST is set).
  if (host === '0.0.0.0') {
    const lan = Object.values(networkInterfaces()).flat().filter((a) => a && a.family === 'IPv4' && !a.internal).map((a) => a.address);
    for (const a of lan) console.log(`[server] LAN        http://${a}:${port}/webgl/`);
  }
});

const stop = () => server.close(() => { store.close(); process.exit(0); });
process.on('SIGINT', stop);
process.on('SIGTERM', stop);
