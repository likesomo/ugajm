import { createServer } from 'node:http';
import { openDb } from '../src/db.mjs';
import { createApp } from '../src/app.mjs';
import { loadLevels } from '../src/levels.mjs';

export const TOKEN = 'test-token';
export const PID = '11111111-1111-4111-8111-111111111111';
export const SID = '22222222-2222-4222-8222-222222222222';
export const T0 = 1_762_500_000_000;

export function envelope(events, over = {}) {
  return { v: 1, playerId: PID, sessionId: SID, client: 'web-proto', build: 'test', events, ...over };
}

// Starts the app on a random port with an in-memory DB. Returns { url, store, close }.
export async function startServer(opts = {}) {
  const store = openDb(':memory:');
  const levels = await loadLevels();
  const app = createApp({ store, token: TOKEN, levels, ...opts });
  const server = createServer(app);
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  const url = `http://127.0.0.1:${server.address().port}`;
  return { url, store, levels, close: () => new Promise((r) => server.close(() => { store.close(); r(); })) };
}

export const post = (url, body, headers = {}) => fetch(`${url}/v1/events`, {
  method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: typeof body === 'string' ? body : JSON.stringify(body),
});
export const get = (url, path, token = TOKEN) => fetch(`${url}${path}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
