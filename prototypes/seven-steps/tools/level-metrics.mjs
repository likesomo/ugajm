// Per-stage difficulty / interest metrics for Seven Steps (model: design/levels/interest-model.md).
// Usage: node prototypes/seven-steps/tools/level-metrics.mjs
//   → reports/level-metrics.json (not committed) and design/levels/interest-curve.html (committed)
// Auto metrics come from the rules in src/core.js; judgement metrics come from design/levels/judgement.json.
// Proposals (experimental rules / another stage order), paths relative to the repo root:
//   --core <rules.js> --levels <module exporting LEVELS> --html <out.html> --json <out.json>
//   --title <text> --compare <earlier level-metrics.json>   (draws that run's D/I faintly, matched by stage name)
//   --qa <reports/qa/qa-report.json>   (tools/qa-report.mjs: measured mean tries per stage on a right-hand axis, matched by stage name;
//                                       a stage renamed since the QA build matches by number)
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const argv = process.argv.slice(2);
const arg = (name, fallback) => { const i = argv.indexOf(`--${name}`); return i >= 0 ? argv[i + 1] : fallback; };
const fromRoot = (p) => resolve(ROOT, p);
const CORE = fromRoot(arg('core', 'prototypes/seven-steps/src/core.js'));
const LEVELS_FILE = fromRoot(arg('levels', 'prototypes/seven-steps/src/levels.js'));
const JUDGEMENT = fromRoot(arg('judgement', 'design/levels/judgement.json'));
const OUT_JSON = fromRoot(arg('json', 'reports/level-metrics.json'));
const OUT_HTML = fromRoot(arg('html', 'design/levels/interest-curve.html'));
const TITLE = arg('title', '');
const COMPARE = arg('compare') ? JSON.parse(readFileSync(fromRoot(arg('compare')), 'utf8')).rows : null;
const QA = arg('qa') ? JSON.parse(readFileSync(fromRoot(arg('qa')), 'utf8')) : null;
const { parseLevel, newState, step, choose, actions, dispatch, minBodies, solve } = await import(pathToFileURL(CORE));
const { LEVELS } = await import(pathToFileURL(LEVELS_FILE));

// ---- Model constants (documented in interest-model.md; tune after playtests) ----
const M = {
  // Difficulty: fixed normalisation scales so adding stages never re-scales old ones.
  dWeights: { bodies: 0.25, moves: 0.15, search: 0.25, rarity: 0.25, traps: 0.1 },
  scale: { bodiesMax: 7, movesMin: 7, movesMax: 50, searchMax: 1e5, rarityMax: 12 },
  // Interest.
  iWeights: { N: 0.3, A: 0.3, C: 0.4, F: 0.4 },
  baseline: 0.35,
  nearMargin: 0.05,    // I within this of the baseline also counts as a drop zone (warning)
  skillRate: 0.5,      // EMA of past difficulty = current skill
  stretch: 0.1,        // ideal D = skill + stretch
  introDiscount: 0.15, // a stage that introduces something new should sit lower
  sigma: 0.2,          // width of the "right challenge" bell
  spikeFree: 0.2,      // D rise above this vs previous stage adds fatigue
  streakLimit: 3,      // same gimmick combo more than this many stages in a row → insert a new gimmick
  exploreLimit: 300000,
};
const BASIC = new Set(['walk7']); // tags every stage has; excluded from overlap/novelty
const TAG_KO = {
  walk7: '7걸음', detour: '돌아가기', keep: 'KEEP 석상', plate: '발판·문', switch: 'SWITCH', pit: '구덩이', bridge: '몸 다리',
  gate: '숫자 문', ice: '얼음', parity: '홀짝', statueStopper: '석상 멈춤벽', cracked: '금 간 바닥', seal: '봉인 다리',
  multiStatue: '석상 여러 개', switchBack: '출발점 되돌리기', statueDoor: '석상 수 문', statueKey: '열쇠 석상',
  emptyDoor: '0개 문', lever: '레버', mud: '진흙',
};
const ko = (t) => TAG_KO[t] ?? t;

const clamp = (v, lo = 0, hi = 1) => Math.max(lo, Math.min(hi, v));
const r2 = (v) => Math.round(v * 100) / 100;
const log10Big = (b) => { const t = b.toString(); const k = Math.min(15, t.length); return t.length - k + Math.log10(Number(t.slice(0, k))); };
const keyOf = (s) => `${s.start}|${s.pos}|${s.used}|${s.bodies}|${s.statues}|${s.bridges}|${s.broken}|${s.status}`;
const apply = (s, a) => (a === 'keep' || a === 'switch' ? choose(s, a) : step(s, a));
const jaccard = (a, b) => {
  const A = [...a].filter((t) => !BASIC.has(t));
  const B = new Set([...b].filter((t) => !BASIC.has(t)));
  if (!A.length && !B.size) return 1;
  const inter = A.filter((t) => B.has(t)).length;
  return inter / (A.length + B.size - inter);
};

// Full state graph at the stage's real body limit (what the player can actually reach).
function explore(level) {
  const start = newState(level);
  const nodes = new Map([[keyOf(start), { depth: 0, ways: 1n, out: [], win: false }]]);
  let frontier = [start];
  let depth = 0;
  let winDepth = null;
  let winWays = 0n;
  let wins = 0;
  let capped = false;
  const loseWays = new Map(); // depth → number of action sequences that end in a lost stage at that depth
  while (frontier.length) {
    const next = [];
    for (const s of frontier) {
      const node = nodes.get(keyOf(s));
      for (const a of actions(s)) {
        const { state } = apply(s, a);
        if (state === s) continue;
        if (state.status === 'lose') { loseWays.set(depth + 1, (loseWays.get(depth + 1) ?? 0n) + node.ways); continue; }
        if (state.status === 'win') {
          node.win = true;
          wins++;
          if (winDepth === null) winDepth = depth + 1;
          if (winDepth === depth + 1) winWays += node.ways;
          continue;
        }
        const k = keyOf(state);
        let child = nodes.get(k);
        if (!child) {
          if (nodes.size >= M.exploreLimit) { capped = true; continue; }
          child = { depth: depth + 1, ways: 0n, out: [], win: false };
          nodes.set(k, child);
          next.push(state);
        }
        if (child.depth === depth + 1) child.ways += node.ways;
        node.out.push(k);
      }
    }
    frontier = next;
    depth++;
  }
  // States from which a win is still reachable (reverse propagation).
  const alive = new Set([...nodes].filter(([, n]) => n.win).map(([k]) => k));
  for (let changed = true; changed;) {
    changed = false;
    for (const [k, n] of nodes) {
      if (!alive.has(k) && n.out.some((o) => alive.has(o))) { alive.add(k); changed = true; }
    }
  }
  // All legal action sequences of the winning length (wins + losses + still-playing paths that long).
  let pathsAtWin = winWays + (loseWays.get(winDepth) ?? 0n);
  for (const n of nodes.values()) if (n.depth === winDepth) pathsAtWin += n.ways;
  // Commit traps: body boundaries (a fresh body, not the very first) from which the stage can no longer be won.
  const fresh = [...nodes].filter(([k, n]) => n.depth > 0 && k.split('|')[2] === '0');
  const deadFresh = fresh.filter(([k]) => !alive.has(k)).length;
  const beforeWin = winDepth === null ? nodes.size : [...nodes.values()].filter((n) => n.depth < winDepth).length;
  return {
    states: nodes.size, capped, beforeWin, winDepth, winWays, winEdges: wins,
    deadRatio: nodes.size ? 1 - alive.size / nodes.size : 0,
    rarity: log10Big(pathsAtWin) - log10Big(winWays), pathsAtWin, freshBodies: fresh.length, trapRatio: fresh.length ? deadFresh / fresh.length : 0,
  };
}

// Replays the fewest-body solution and turns what happened into gimmick tags.
function tagsOf(level, def, best, needsSwitch) {
  const tags = new Set(['walk7']);
  const has = (re) => def.map.some((row) => re.test(row));
  if (has(/[a-c]/)) tags.add('plate');
  if (has(/o/)) tags.add('pit');
  if (has(/~/)) tags.add('ice');
  if (has(/x/)) tags.add('cracked');
  if (has(/[1-7]/)) tags.add('gate');
  if (has(/Q/)) tags.add('statueDoor');
  if (has(/L/)) tags.add('lever');
  if (has(/m/)) tags.add('mud');
  if (needsSwitch) tags.add('switch');
  const ev = { keep: 0, switch: 0, bridge: 0, seal: 0, crumble: 0, reversals: 0, bodies: best.bodies };
  let s = newState(level);
  let prevDir = null;
  const opposite = { up: 'down', down: 'up', left: 'right', right: 'left' };
  for (const a of best.path) {
    const r = dispatch(s, a === 'keep' || a === 'switch' ? { type: a } : { type: 'move', dir: a });
    s = r.state;
    if (a === 'keep' || a === 'switch') { ev[a]++; prevDir = null; continue; }
    if (prevDir && opposite[prevDir] === a) ev.reversals++;
    prevDir = a;
    for (const e of r.events) {
      if (e.type === 'bridge') ev.bridge++;
      if (e.type === 'seal') ev.seal++;
      if (e.type === 'crumble') ev.crumble++;
      if (e.type === 'respawn') prevDir = null;
    }
  }
  if (ev.keep) tags.add('keep');
  if (ev.keep >= 2) tags.add('multiStatue');
  if (ev.bridge) tags.add('bridge');
  if (ev.seal) tags.add('seal');
  if (ev.reversals) tags.add('detour');
  return { tags, ev };
}

function difficulty(level, best, ex) {
  const { scale, dWeights: w } = M;
  const parts = {
    bodies: clamp((best.bodies - 1) / (scale.bodiesMax - 1)),
    moves: clamp(Math.log(best.moves / scale.movesMin) / Math.log(scale.movesMax / scale.movesMin)),
    search: clamp(Math.log10(Math.max(1, ex.beforeWin)) / Math.log10(scale.searchMax)),
    // How unlikely a random legal sequence of the winning length is to win: -log10(winning / all), capped.
    rarity: clamp(ex.rarity / scale.rarityMax),
    traps: clamp(ex.trapRatio),
  };
  const D = Object.entries(w).reduce((sum, [k, v]) => sum + v * parts[k], 0);
  return { D, parts };
}

// ---- Run ----
const judgement = JSON.parse(readFileSync(JUDGEMENT, 'utf8'));
const rows = [];
for (const [idx, def] of LEVELS.entries()) {
  const level = parseLevel(def);
  const best = minBodies(level);
  if (!best) throw new Error(`stage ${idx + 1} "${def.name}" is not solvable`);
  const needsSwitch = !solve(level, level.bodies, { noSwitch: true });
  const ex = explore(level);
  const { tags, ev } = tagsOf(level, def, best, needsSwitch);
  const j = judgement.stages.find((x) => x.name === def.name);
  if (!j) console.warn(`! judgement.json has no entry for "${def.name}" (A = 0)`);
  for (const t of j?.tagsAdd ?? []) tags.add(t);
  const { D, parts } = difficulty(level, best, ex);
  rows.push({
    idx: idx + 1, name: def.name, limit: level.bodies, size: `${level.w}x${level.h}`,
    minBodies: best.bodies, moves: best.moves, needsSwitch,
    states: ex.states, capped: ex.capped, beforeWin: ex.beforeWin, shortestPaths: ex.winWays.toString(),
    rarity: r2(ex.rarity), trapRatio: r2(ex.trapRatio), freshBodies: ex.freshBodies,
    deadRatio: r2(ex.deadRatio), events: ev, tags: [...tags].sort(),
    D: r2(D), dParts: Object.fromEntries(Object.entries(parts).map(([k, v]) => [k, r2(v)])),
    A: j?.A ?? 0, aReason: j?.reason ?? '', nAdj: j?.nAdj ?? 0, note: j?.note ?? '',
  });
}

// Novelty, fatigue, challenge, interest — these depend on stage order.
const seenTags = new Set();
const comboCount = new Map();
let skill = 0;
let streak = 0;
for (const [i, r] of rows.entries()) {
  const tags = new Set(r.tags);
  const fresh = [...tags].filter((t) => !seenTags.has(t));
  const combo = [...tags].filter((t) => !BASIC.has(t)).sort().join('+') || 'walk7';
  const repeats = comboCount.get(combo) ?? 0;
  const nAuto = 0.6 * clamp(fresh.length / 2) + 0.4 * Math.pow(0.4, repeats);
  r.newTags = fresh;
  r.combo = combo;
  r.N = r2(clamp(nAuto + r.nAdj));

  const prev = [1, 2, 3].map((k) => rows[i - k]).filter(Boolean);
  const overlap = prev.reduce((sum, p, k) => sum + [0.5, 0.3, 0.2][k] * jaccard(tags, p.tags), 0);
  const prevD = rows[i - 1]?.D ?? 0;
  const spike = Math.max(0, r.D - prevD - M.spikeFree) * 2;
  // Same main combo repeated: count consecutive stages whose tags add nothing new over the previous one.
  const samey = i > 0 && fresh.length === 0 && jaccard(tags, rows[i - 1].tags) >= 0.5;
  streak = samey ? streak + 1 : 1;
  r.streak = streak;
  r.F = r2(clamp((fresh.length ? 0.4 : 1) * overlap + spike));
  r.overlap = r2(overlap);
  r.spike = r2(spike);

  r.skill = r2(skill);
  const target = skill + M.stretch - (fresh.length ? M.introDiscount : 0);
  r.target = r2(target);
  r.C = r2(Math.exp(-(((r.D - target) / M.sigma) ** 2)));
  const w = M.iWeights;
  r.I = r2(w.N * r.N + w.A * r.A + w.C * r.C - w.F * r.F);
  skill = skill + M.skillRate * (r.D - skill);

  fresh.forEach((t) => seenTags.add(t));
  comboCount.set(combo, repeats + 1);
}
for (const [i, r] of rows.entries()) {
  const prevI = rows[i - 1]?.I;
  r.drop = r.I < M.baseline + M.nearMargin || (prevI !== undefined && prevI - r.I >= 0.15);
  r.insertGimmick = i > 0 && (r.I < M.baseline || r.streak > M.streakLimit);
}

mkdirSync(dirname(OUT_JSON), { recursive: true });
writeFileSync(OUT_JSON, JSON.stringify({ model: M, generated: new Date().toISOString(), rows }, null, 2));
writeFileSync(OUT_HTML, renderHtml(rows));

console.log(' #  이름            몸     걸음  상태수    희소   함정   D     N     A     C     F     I    태그');
for (const r of rows) {
  const flag = r.insertGimmick ? ' ◀ 기믹' : r.drop ? ' ◀ 하락' : '';
  console.log(
    `${String(r.idx).padStart(2)}  ${r.name.padEnd(12)} ${r.minBodies}/${r.limit}${r.needsSwitch ? 'X' : ' '}  ` +
    `${String(r.moves).padStart(4)} ${String(r.beforeWin).padStart(7)} ${r.rarity.toFixed(2).padStart(6)}  ${r.trapRatio.toFixed(2)}  ` +
    `${[r.D, r.N, r.A, r.C, r.F, r.I].map((v) => v.toFixed(2)).join('  ')}  ${r.combo}${flag}`,
  );
}
console.log(`\n→ ${OUT_HTML}\n→ ${OUT_JSON}`);

// ---- HTML (inline SVG, no libraries) ----
function renderHtml(rows) {
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const W = 960, H = 440, L = 56, R = QA ? 44 : 24, T = 64, B = 64;
  const n = rows.length;
  const x = (i) => L + (n === 1 ? 0 : (i * (W - L - R)) / (n - 1));
  const yMin = -0.2, yMax = 1;
  const y = (v) => T + ((yMax - clamp(v, yMin, yMax)) * (H - T - B)) / (yMax - yMin);
  const line = (key) => rows.map((r, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(r[key]).toFixed(1)}`).join(' ');
  const half = (W - L - R) / Math.max(1, n - 1) / 2;

  const grid = [-0.2, 0, 0.2, 0.4, 0.6, 0.8, 1].map((v) =>
    `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" class="grid${v === 0 ? ' zero' : ''}"/>` +
    `<text x="${L - 8}" y="${y(v) + 4}" class="tick" text-anchor="end">${v.toFixed(1)}</text>`).join('');
  const drops = rows.map((r, i) => (r.drop
    ? `<rect x="${x(i) - half}" y="${T}" width="${half * 2}" height="${H - T - B}" class="drop"><title>${esc(`${r.idx}. ${r.name}: 흥미도 하락 (I=${r.I})`)}</title></rect>` : '')).join('');
  const intros = rows.map((r, i) => (r.newTags.length
    ? `<line x1="${x(i)}" x2="${x(i)}" y1="${T - 6 - (i % 2) * 16}" y2="${H - B}" class="intro"/><text x="${x(i)}" y="${T - 12 - (i % 2) * 16}" class="introlabel" text-anchor="${i === n - 1 ? 'end' : i === 0 ? 'start' : 'middle'}">${esc(r.newTags.map(ko).join('·'))}</text>` : '')).join('');
  const inserts = rows.map((r, i) => (r.insertGimmick
    ? `<g class="insert"><path d="M${x(i)},${H - B + 30} l-7,10 h14 z"/><title>${esc(`${r.idx}. 새 기믹 투입 지점`)}</title></g>` : '')).join('');
  // Earlier run (--compare): same stage name → plotted at its new position, gaps where a stage is new.
  const ghost = (key) => {
    if (!COMPARE) return '';
    const byName = new Map(COMPARE.map((r) => [r.name, r]));
    let d = '', pen = 'M';
    rows.forEach((r, i) => { const o = byName.get(r.name); if (!o) { pen = 'M'; return; } d += `${pen}${x(i).toFixed(1)},${y(o[key]).toFixed(1)} `; pen = 'L'; });
    return `<path d="${d}" class="ghost${key}"><title>이전 ${key}</title></path>`;
  };
  // Play QA (--qa): mean tries per tester, 1 try = 0.0 and 15 tries = 1.0 on the left scale, own ticks on the right.
  const qaY = (t) => y((t - 1) / 14);
  const qaByName = QA ? new Map(QA.levels.map((l) => [l.name, l])) : null;
  const qaLayer = () => {
    if (!QA) return '';
    let d = '', pen = 'M', marks = '';
    rows.forEach((r, i) => {
      // A stage renamed after the QA build (23 and 37 after QA 1) falls back to the same stage number,
      // but only when that QA stage's name is not used by any current stage (so a reorder never matches the wrong map).
      const byNum = QA.levels.find((l) => l.level === r.idx);
      const q = qaByName.get(r.name) ?? (byNum && !rows.some((o) => o.name === byNum.name) ? byNum : undefined);
      if (!q || q.meanTries === null) { pen = 'M'; return; }
      d += `${pen}${x(i).toFixed(1)},${qaY(q.meanTries).toFixed(1)} `; pen = 'L';
      const warn = QA.warnings.filter((w) => w.level === q.level);
      marks += `<rect x="${x(i) - 4}" y="${qaY(q.meanTries) - 4}" width="8" height="8" class="dotQ${warn.length ? ' warnQ' : ''}"><title>${esc(`${r.idx}. ${r.name}
QA 평균 ${q.meanTries}회 (최대 ${q.maxTries}, 예측 ${q.predictedTries})
첫 시도 클리어 ${Math.round((q.firstTryClearRate ?? 0) * 100)}%  포기 ${q.gaveUp}${warn.map((w) => `
! ${w.text}`).join('')}`)}</title></rect>`;
    });
    const ticks = [1, 4, 8, 12, 15].map((t) => `<text x="${W - R + 4}" y="${qaY(t) + 4}" class="tick qtick">${t}</text>`).join('');
    return `<path d="${d}" class="lineQ"/>${marks}${ticks}<text x="${W - R + 4}" y="${T - 10}" class="tick qtick">시도</text>`;
  };
  const dots = (key, cls) => rows.map((r, i) =>
    `<circle cx="${x(i)}" cy="${y(r[key])}" r="5" class="${cls}"><title>${esc(`${r.idx}. ${r.name}\nD ${r.D}  I ${r.I}\nN ${r.N}  A ${r.A}  C ${r.C}  F ${r.F}\n${r.tags.map(ko).join(', ')}`)}</title></circle>`).join('');
  const xLabels = rows.map((r, i) =>
    `<text x="${x(i)}" y="${H - B + 18}" class="tick" text-anchor="middle">${r.idx}</text>`).join('');

  const svg = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="스테이지별 난이도와 흥미도 곡선">
  ${grid}${drops}${intros}
  <line x1="${L}" x2="${W - R}" y1="${y(M.baseline)}" y2="${y(M.baseline)}" class="baseline"/>
  <text x="${W - R}" y="${y(M.baseline) - 6}" class="baselabel" text-anchor="end">기준선 ${M.baseline}</text>
  ${ghost('D')}${ghost('I')}<path d="${line('D')}" class="lineD"/><path d="${line('I')}" class="lineI"/>
  ${dots('D', 'dotD')}${dots('I', 'dotI')}${qaLayer()}${xLabels}${inserts}
  <text x="${(L + W - R) / 2}" y="${H - 6}" class="tick" text-anchor="middle">스테이지</text>
</svg>`;

  const cell = (v) => `<td class="num">${v}</td>`;
  const table = rows.map((r) => `<tr class="${r.drop ? 'is-drop' : ''}">
    <td class="num">${r.idx}</td><td>${esc(r.name)}</td>${cell(`${r.minBodies}/${r.limit}${r.needsSwitch ? ' X' : ''}`)}${cell(r.moves)}${cell(r.beforeWin)}${cell(r.rarity)}${cell(r.trapRatio)}
    ${cell(r.D)}${cell(r.N)}${cell(r.A)}${cell(r.C)}${cell(r.F)}<td class="num strong">${r.I}</td>
    <td>${esc(r.tags.filter((t) => !BASIC.has(t)).map(ko).join(', ') || ko('walk7'))}${r.newTags.length ? ` <span class="new">새: ${esc(r.newTags.map(ko).join(', '))}</span>` : ''}</td>
    <td class="reason">${esc(r.aReason)}</td></tr>`).join('');

  return `<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Seven Steps Interest Curve</title>
<style>
:root{--bg:#fbfaf7;--fg:#22211f;--muted:#6b6862;--grid:#e3e0d9;--d:#c2571a;--i:#2b6cb0;--base:#8a8580;--drop:rgba(214,69,65,.12);--intro:#3f8f5a;--insert:#d64541;--card:#fff;--line:#e8e5de;--q:#6b4fa0}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#1a1917;--fg:#ece9e3;--muted:#a19d96;--grid:#34322e;--d:#f08a4b;--i:#6aa9ee;--base:#8f8a83;--drop:rgba(240,100,95,.16);--intro:#6cc28a;--insert:#f06c66;--card:#22211e;--line:#33312d;--q:#b59be6}}
:root[data-theme="dark"]{--bg:#1a1917;--fg:#ece9e3;--muted:#a19d96;--grid:#34322e;--d:#f08a4b;--i:#6aa9ee;--base:#8f8a83;--drop:rgba(240,100,95,.16);--intro:#6cc28a;--insert:#f06c66;--card:#22211e;--line:#33312d;--q:#b59be6}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.55 system-ui,-apple-system,"Segoe UI","Malgun Gothic",sans-serif}
main{max-width:1040px;margin:0 auto;padding:24px 16px 48px}h1{font-size:22px;margin:0 0 4px}p.sub{color:var(--muted);margin:0 0 16px}
.legend{display:flex;flex-wrap:wrap;gap:16px;font-size:13px;color:var(--muted);margin:0 0 8px}.legend i{display:inline-block;width:18px;height:3px;vertical-align:middle;margin-right:6px}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:8px}
svg{width:100%;height:auto;display:block}.grid{stroke:var(--grid)}.grid.zero{stroke:var(--muted);stroke-dasharray:2 3}.tick{fill:var(--muted);font-size:12px}
.baseline{stroke:var(--base);stroke-width:1.5;stroke-dasharray:6 4}.baselabel{fill:var(--base);font-size:12px}
.lineD{fill:none;stroke:var(--d);stroke-width:2.5}.lineI{fill:none;stroke:var(--i);stroke-width:3}.dotD{fill:var(--d)}.ghostD,.ghostI{fill:none;stroke-width:2;stroke-dasharray:5 4;opacity:.45}.ghostD{stroke:var(--d)}.ghostI{stroke:var(--i)}.dotI{fill:var(--i)}.lineQ{fill:none;stroke:var(--q);stroke-width:2.5;stroke-dasharray:1 0}.dotQ{fill:var(--q)}.warnQ{stroke:var(--insert);stroke-width:2.5}.qtick{fill:var(--q)}
.drop{fill:var(--drop)}.intro{stroke:var(--intro);stroke-dasharray:3 3}.introlabel{fill:var(--intro);font-size:11px}.insert path{fill:var(--insert)}
.tablewrap{overflow-x:auto;margin-top:20px}table{border-collapse:collapse;font-size:13px;min-width:900px;width:100%}
th,td{padding:6px 8px;border-bottom:1px solid var(--line);text-align:left;vertical-align:top}th{color:var(--muted);font-weight:600}
td:nth-child(2){white-space:nowrap}td.num{text-align:right;font-variant-numeric:tabular-nums}td.strong{font-weight:700}tr.is-drop td{background:var(--drop)}.new{color:var(--intro)}.reason{color:var(--muted);max-width:280px}
</style></head><body><main>
<h1>일곱 걸음 — 난이도·흥미도 곡선</h1>
<p class="sub">${TITLE ? `${esc(TITLE)} · ` : ''}${n}개 스테이지. 모델: design/levels/interest-model.md · 생성: tools/level-metrics.mjs</p>
<div class="legend"><span><i style="background:var(--d)"></i>난이도 D</span><span><i style="background:var(--i)"></i>흥미도 I</span>
<span><i style="background:var(--base)"></i>기준선</span>${QA ? `<span><i style="background:var(--q)"></i>QA 실측 평균 시도(오른쪽 눈금, 테스터 ${QA.source.testers.length}명 AI, 빨간 테두리 = 경고)</span>` : ''}${COMPARE ? '<span><i style="background:var(--muted);opacity:.5"></i>점선: 비교 대상(같은 이름 스테이지, 값이 같으면 겹쳐 안 보임)</span>' : ''}<span><i style="background:var(--drop);height:10px"></i>흥미도 하락 구간</span>
<span><i style="background:var(--intro)"></i>새 요소 등장</span><span><i style="background:var(--insert);height:8px;width:8px"></i>새 기믹 투입 지점</span></div>
<div class="card">${svg}</div>
<div class="tablewrap"><table><thead><tr><th>#</th><th>이름</th><th>몸(최소/제한)</th><th>걸음</th><th>탐색 상태</th><th>희소도</th><th>함정 비율</th>
<th>D</th><th>N</th><th>A</th><th>C</th><th>F</th><th>I</th><th>기믹 조합</th><th>깨달음 근거</th></tr></thead><tbody>${table}</tbody></table></div>
</main></body></html>
`;
}
