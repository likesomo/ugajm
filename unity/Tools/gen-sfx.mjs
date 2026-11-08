// Synthesizes the placeholder sound effects into unity/Assets/Shared/Audio/*.wav (shared folder outside git) (16-bit mono, 44.1 kHz).
// Deterministic (seeded noise), so re-running gives identical files. Swap for recorded/CC0 sounds later by
// replacing the WAVs with the same names.
// Usage (repo root): node unity/Tools/gen-sfx.mjs
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const RATE = 44100;
const outDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'Assets', 'Shared', 'Audio');

let seed = 7;
const noise = () => {
  seed = (seed * 1103515245 + 12345) & 0x7fffffff;
  return (seed / 0x7fffffff) * 2 - 1;
};

function render(seconds, fn) {
  const n = Math.round(seconds * RATE);
  const buf = new Float32Array(n);
  for (let i = 0; i < n; i++) buf[i] = fn(i / RATE, i);
  return buf;
}

const tone = (freq, t) => Math.sin(2 * Math.PI * freq * t);
const env = (t, attack, decay) => (t < attack ? t / attack : Math.exp(-(t - attack) / decay));

function lowpass(buf, alpha) {
  let y = 0;
  return buf.map((x) => (y += alpha * (x - y)));
}

function mix(...bufs) {
  const n = Math.max(...bufs.map((b) => b.length));
  const out = new Float32Array(n);
  for (const b of bufs) for (let i = 0; i < b.length; i++) out[i] += b[i];
  return out;
}

function delay(buf, seconds) {
  const off = Math.round(seconds * RATE);
  const out = new Float32Array(buf.length + off);
  out.set(buf, off);
  return out;
}

function note(freq, seconds, decay, harmonics = [1, 0.4, 0.2]) {
  return render(seconds, (t) => env(t, 0.005, decay) * harmonics.reduce((s, a, k) => s + a * tone(freq * (k + 1), t), 0) * 0.35);
}

function writeWav(name, buf, gain = 0.8) {
  const peak = Math.max(1e-6, ...buf.map(Math.abs));
  const data = Buffer.alloc(buf.length * 2);
  for (let i = 0; i < buf.length; i++) {
    const fade = Math.min(1, (buf.length - i) / 200); // avoid end clicks
    data.writeInt16LE(Math.round((buf[i] / peak) * gain * fade * 32767), i * 2);
  }
  const h = Buffer.alloc(44);
  h.write('RIFF', 0); h.writeUInt32LE(36 + data.length, 4); h.write('WAVE', 8);
  h.write('fmt ', 12); h.writeUInt32LE(16, 16); h.writeUInt16LE(1, 20); h.writeUInt16LE(1, 22);
  h.writeUInt32LE(RATE, 24); h.writeUInt32LE(RATE * 2, 28); h.writeUInt16LE(2, 32); h.writeUInt16LE(16, 34);
  h.write('data', 36); h.writeUInt32LE(data.length, 40);
  writeFileSync(join(outDir, `${name}.wav`), Buffer.concat([h, data]));
  console.log(`[gen-sfx] ${name}.wav ${(buf.length / RATE).toFixed(2)}s`);
}

mkdirSync(outDir, { recursive: true });

// 1st-6th step: soft thud (pitch is raised per step in game).
writeWav('step', mix(
  lowpass(render(0.09, (t) => noise() * env(t, 0.002, 0.02)), 0.15),
  render(0.09, (t) => tone(140 - 400 * t, t) * env(t, 0.002, 0.03) * 0.6),
), 0.6);

// Lucky 7th step: bell chime.
writeWav('lucky', mix(
  note(1046.5, 1.4, 0.45, [1, 0.5, 0.25, 0.12]),
  delay(note(1568, 1.2, 0.4, [1, 0.3]), 0.04),
  delay(note(2093, 1.0, 0.3, [0.6]), 0.08),
));

// KEEP: body turns to stone.
writeWav('stone', mix(
  lowpass(render(0.45, (t) => noise() * env(t, 0.003, 0.12)), 0.08),
  render(0.45, (t) => tone(70, t) * env(t, 0.003, 0.12)),
), 0.85);

// SWITCH: rising shimmer.
writeWav('switch', render(0.5, (t) => {
  const f = 400 * Math.pow(3, t / 0.5);
  return (tone(f, t) + 0.3 * tone(f * 2, t)) * env(t, 0.02, 0.25);
}), 0.6);

// Fall into a pit: falling whistle.
writeWav('fall', render(0.55, (t) => tone(700 * Math.pow(0.25, t / 0.55), t) * env(t, 0.01, 0.3)), 0.55);

// Blocked move.
writeWav('bump', lowpass(render(0.1, (t) => Math.sign(tone(90, t)) * env(t, 0.002, 0.03)), 0.2), 0.5);

// Statue fills a pit / seals cracked floor.
writeWav('bridge', mix(
  lowpass(render(0.6, (t) => noise() * env(t, 0.005, 0.2)), 0.05),
  render(0.6, (t) => tone(55, t) * env(t, 0.005, 0.25)),
), 0.85);

// Cracked floor breaks behind you.
writeWav('crumble', lowpass(render(0.35, (t) => noise() * env(t, 0.002, 0.08) * (1 + 0.5 * Math.sign(tone(30, t)))), 0.3), 0.6);

// Undo / restart blip.
writeWav('undo', render(0.08, (t) => tone(660 - 1500 * t, t) * env(t, 0.003, 0.03)), 0.4);

// Clear jingle: C E G C.
writeWav('win', mix(
  note(523.25, 0.5, 0.2), delay(note(659.25, 0.5, 0.2), 0.12),
  delay(note(783.99, 0.5, 0.2), 0.24), delay(note(1046.5, 1.2, 0.5), 0.36),
));

// Out of bodies: falling minor notes.
writeWav('lose', mix(
  note(392, 0.5, 0.25), delay(note(311.13, 0.5, 0.25), 0.18), delay(note(261.63, 1.0, 0.45), 0.36),
), 0.6);
