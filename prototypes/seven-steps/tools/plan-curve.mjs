// Planned interest curve for the 50-stage plan (before the stages exist).
// Input: design/levels/stage-plan-50.json (target D, planned tags, planned aha A per stage).
// Uses the interest-model.md formulas for N, C, F, I with the target D instead of the solver D,
// plus the chapter rules proposed in stage-plan-50.md: new-planet novelty bonus and fatigue reset across chapters.
// Usage: node prototypes/seven-steps/tools/plan-curve.mjs  → design/levels/stage-plan-50.html, table on stdout
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const plan = JSON.parse(readFileSync(join(ROOT, 'design/levels/stage-plan-50.json'), 'utf8'));
const M = { iW: { N: 0.3, A: 0.3, C: 0.4, F: 0.4 }, baseline: 0.35, near: 0.05, skillRate: 0.5, stretch: 0.1, intro: 0.15, sigma: 0.2, spikeFree: 0.2,
  planetBonus: 0.1,      // N bonus on the first stage of a chapter (new planet)
  chapterCarry: 0.5 };   // overlap with stages of an earlier chapter counts at this weight (fatigue partly resets)
const jac = (a, b) => { const A = new Set(a.filter((t) => t !== 'walk7')), B = new Set(b.filter((t) => t !== 'walk7')); if (!A.size && !B.size) return 1; let i = 0; for (const t of A) if (B.has(t)) i++; return i / (A.size + B.size - i); };
const r2 = (v) => Math.round(v * 100) / 100;

const seenTags = new Set(); const combos = new Map(); let skill = 0; const rows = [];
for (const [k, s] of plan.stages.entries()) {
  const tags = s.tags.filter((t) => t !== 'walk7');
  const fresh = tags.filter((t) => !seenTags.has(t));
  const combo = [...tags].sort().join('+');
  const rep = combos.get(combo) ?? 0;
  const first = k === 0 || plan.stages[k - 1].ch !== s.ch;
  const N = Math.min(1, 0.6 * Math.min(1, fresh.length / 2) + 0.4 * 0.4 ** rep + (first ? M.planetBonus : 0));
  const target = skill + M.stretch - (fresh.length ? M.intro : 0);
  const C = Math.exp(-(((s.D - target) / M.sigma) ** 2));
  let overlap = 0;
  [0.5, 0.3, 0.2].forEach((w, j) => { const p = plan.stages[k - 1 - j]; if (p) overlap += w * jac(s.tags, p.tags) * (p.ch === s.ch ? 1 : M.chapterCarry); });
  const prevD = k ? plan.stages[k - 1].D : s.D;
  const F = (fresh.length ? 0.4 : 1) * overlap + 2 * Math.max(0, s.D - prevD - M.spikeFree);
  const I = M.iW.N * N + M.iW.A * s.A + M.iW.C * C - M.iW.F * F;
  rows.push({ ...s, fresh, N: r2(N), C: r2(C), F: r2(F), I: r2(I) });
  fresh.forEach((t) => seenTags.add(t)); combos.set(combo, rep + 1);
  skill += M.skillRate * (s.D - skill);
}
rows.forEach((r, k) => { r.drop = r.I < M.baseline + M.near || (k > 0 && rows[k - 1].I - r.I >= 0.15); r.below = r.I < M.baseline; });

// ---- SVG ----
const W = 1100, H = 380, L = 44, Rr = 16, T = 40, B = 70;
const x = (k) => L + (k + 0.5) * ((W - L - Rr) / rows.length);
const y = (v) => T + (1 - v) * (H - T - B);
const COLORS = ['#e8f0e4', '#e6e9f2', '#efe8dc', '#e3eef3', '#f0e6dc', '#ede3e3', '#ece8f2'];
let g = '';
for (const c of plan.chapters) {
  const ks = rows.map((r, k) => (r.ch === c.id ? k : -1)).filter((k) => k >= 0);
  const x0 = x(ks[0]) - (x(1) - x(0)) / 2, x1 = x(ks.at(-1)) + (x(1) - x(0)) / 2;
  g += `<rect x="${x0}" y="${T}" width="${x1 - x0}" height="${H - T - B}" fill="${COLORS[(c.id - 1) % 7]}"/>`;
  g += `<text x="${(x0 + x1) / 2}" y="${T - 8}" text-anchor="middle" class="ch">${c.id}. ${c.planet}</text>`;
}
for (const v of [0, 0.2, 0.4, 0.6, 0.8, 1]) g += `<line x1="${L}" x2="${W - Rr}" y1="${y(v)}" y2="${y(v)}" class="grid"/><text x="${L - 6}" y="${y(v) + 4}" text-anchor="end" class="ax">${v}</text>`;
g += `<line x1="${L}" x2="${W - Rr}" y1="${y(M.baseline)}" y2="${y(M.baseline)}" class="base"/><text x="${W - Rr}" y="${y(M.baseline) - 4}" text-anchor="end" class="ax">기준선 ${M.baseline}</text>`;
const path = (key) => rows.map((r, k) => `${k ? 'L' : 'M'}${x(k).toFixed(1)},${y(r[key]).toFixed(1)}`).join('');
g += `<path d="${path('D')}" class="d"/><path d="${path('I')}" class="i"/>`;
rows.forEach((r, k) => {
  g += `<circle cx="${x(k)}" cy="${y(r.I)}" r="${r.role === '피날레' ? 5 : 3}" class="${r.below ? 'below' : r.drop ? 'warn' : 'ip'}"><title>${r.n}. ${r.title} (${r.role}) D ${r.D} I ${r.I} — ${r.aha}</title></circle>`;
  g += `<circle cx="${x(k)}" cy="${y(r.D)}" r="2.5" class="dp"><title>${r.n}. ${r.title} D ${r.D}</title></circle>`;
  if (r.role === '피날레') g += `<text x="${x(k)}" y="${H - B + 30}" text-anchor="middle" class="fin">★</text>`;
  if (r.fresh.length) g += `<path d="M${x(k) - 4},${H - B + 44} l4,-7 l4,7z" class="new"><title>새 태그: ${r.fresh.join(', ')}</title></path>`;
  g += `<text x="${x(k)}" y="${H - B + 16}" text-anchor="middle" class="ax">${r.n}</text>`;
});
const table = rows.map((r) => `<tr class="${r.below ? 'below' : r.drop ? 'warn' : ''}"><td>${r.n}</td><td>${r.ch}</td><td>${r.role}</td><td>${r.title}</td><td>${r.src}</td><td>${r.tags.join(', ')}</td><td>${r.D}</td><td>${r.A}</td><td>${r.N}</td><td>${r.C}</td><td>${r.F}</td><td><b>${r.I}</b></td><td>${r.aha}</td></tr>`).join('');
const html = `<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>50스테이지 계획 곡선</title>
<style>:root{--bg:#fbfaf7;--fg:#222;--mut:#777}body{background:var(--bg);color:var(--fg);font:14px/1.5 system-ui,sans-serif;margin:16px}svg{max-width:100%;height:auto;background:#fff}
.grid{stroke:#0001}.base{stroke:#c33;stroke-dasharray:5 4}.ax{font-size:10px;fill:var(--mut)}.ch{font-size:11px;fill:#444}.d{fill:none;stroke:#888;stroke-width:1.5;stroke-dasharray:3 2}.i{fill:none;stroke:#2a6f6b;stroke-width:2}
.ip{fill:#2a6f6b}.warn{fill:#d98a1c}.below{fill:#c33}.dp{fill:#888}.fin{font-size:13px;fill:#b98a2a}.new{fill:#c33}
table{border-collapse:collapse;font-size:12px;margin-top:12px}td,th{border-bottom:1px solid #0001;padding:3px 6px;text-align:left;vertical-align:top}tr.warn td{background:#fdf1df}tr.below td{background:#fbe1e1}
@media (prefers-color-scheme:dark){:root:not([data-theme=light]){--bg:#1b1c1e;--fg:#eee;--mut:#aaa}}</style></head><body>
<h1>50스테이지 계획 곡선 (계획값)</h1>
<p>목표 난이도 D(회색 점선)와 계획 태그·깨달음으로 계산한 예측 흥미도 I(청록). 스테이지가 만들어지면 <code>level-metrics.mjs</code>의 실제 값으로 바뀐다. ★ 챕터 피날레, ▲ 새 태그, 주황 = 하락 경고(기준선+0.05 아래 또는 직전보다 0.15 이상 하락), 빨강 = 기준선 아래. 챕터 규칙: 새 행성 첫 스테이지 N +${M.planetBonus}, 앞 챕터와의 겹침 피로 ×${M.chapterCarry}.</p>
<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="50스테이지 계획 난이도와 흥미도">${g}</svg>
<table><thead><tr><th>#</th><th>장</th><th>역할</th><th>제목</th><th>출처</th><th>태그</th><th>D</th><th>A</th><th>N</th><th>C</th><th>F</th><th>I</th><th>아하</th></tr></thead><tbody>${table}</tbody></table></body></html>`;
writeFileSync(join(ROOT, 'design/levels/stage-plan-50.html'), html);
for (const r of rows) console.log(`${String(r.n).padStart(2)} ${r.ch} ${r.role.padEnd(3)} D ${r.D.toFixed(2)} I ${r.I.toFixed(2)} ${r.below ? 'BELOW' : r.drop ? 'warn' : ''}  ${r.title}`);
const minI = rows.reduce((a, r) => (r.I < a.I ? r : a));
console.log(`min I ${minI.I} at ${minI.n} ${minI.title}; warnings ${rows.filter((r) => r.drop).length}, below ${rows.filter((r) => r.below).length}`);
