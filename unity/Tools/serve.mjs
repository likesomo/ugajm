// Static file server for the WebGL build (no Python needed).
// Usage: node unity/Tools/serve.mjs [port] [dir]   (defaults: 8090, Builds/WebGL)
// Precompressed files follow the same rule as server/src/app.mjs (fileHeaders): .unityweb that really is gzip and .gz
// get Content-Encoding: gzip when the browser accepts it, so it unpacks natively; Content-Type comes from the inner
// extension (.wasm -> application/wasm for streaming compile). Otherwise the bytes go out unchanged and the build's
// decompression fallback unpacks them in JS.
import { createServer } from 'node:http';
import { createReadStream, statSync, openSync, readSync, closeSync } from 'node:fs';
import { extname, join, normalize, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = fileURLToPath(new URL('.', import.meta.url));
const port = Number(process.argv[2] ?? 8090);
const root = resolve(process.argv[3] ?? join(here, '..', '..', 'Builds', 'WebGL'));
const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.css': 'text/css',
  '.json': 'application/json', '.wasm': 'application/wasm', '.png': 'image/png', '.ico': 'image/x-icon',
  '.data': 'application/octet-stream',
};
const COMPRESSED = { '.unityweb': null, '.gz': 'gzip', '.br': 'br' }; // null: sniff (only gzip has a magic number)

function isGzip(file) {
  const fd = openSync(file, 'r');
  try {
    const b = Buffer.alloc(2);
    return readSync(fd, b, 0, 2, 0) === 2 && b[0] === 0x1f && b[1] === 0x8b;
  } finally {
    closeSync(fd);
  }
}

const accepts = (req, enc) => (req.headers['accept-encoding'] ?? '').split(',').some((p) => {
  const [name, ...params] = p.trim().toLowerCase().split(';');
  return (name === enc || name === '*') && !params.some((q) => /^\s*q=0(\.0*)?\s*$/.test(q));
});

function headersFor(file, req) {
  const outer = extname(file).toLowerCase();
  if (!(outer in COMPRESSED)) return { 'Content-Type': types[outer] ?? 'application/octet-stream' };
  const inner = extname(file.slice(0, -outer.length)).toLowerCase();
  const enc = COMPRESSED[outer] ?? (isGzip(file) ? 'gzip' : null);
  return {
    'Content-Type': types[inner] ?? 'application/octet-stream',
    Vary: 'Accept-Encoding',
    ...(enc && accepts(req, enc) ? { 'Content-Encoding': enc } : {}),
  };
}

createServer((req, res) => {
  const path = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  let file = normalize(join(root, path));
  if (file !== root && !file.startsWith(root + sep)) { res.writeHead(403).end(); return; }
  let size;
  try {
    if (statSync(file).isDirectory()) file = join(file, 'index.html');
    size = statSync(file).size;
  } catch {
    res.writeHead(404).end('not found');
    return;
  }
  res.writeHead(200, { ...headersFor(file, req), 'Content-Length': size, 'Cache-Control': 'no-store' });
  createReadStream(file).pipe(res);
}).listen(port, '127.0.0.1', () => console.log(`[serve] ${root} -> http://localhost:${port}/`));
