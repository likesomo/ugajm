// Play-QA aggregate report (production/qa-play-plan.md section 5, level design).
// Input : reports/qa/attempts.jsonl (one line per attempt, written by unity/Tools/qa-play.mjs; format docs/qa-attempts.md v1, feat/core d7402f2)
//         reports/qa/notes-<tester>.md (one note line per stage; lines starting with the stage number are picked up)
//         reports/level-metrics.json (model prediction; run tools/level-metrics.mjs first)
//         design/levels/stage-report.json (min-body solution: minBodies, minInputs; chapter per stage)
// Output: reports/qa/qa-report.json (not committed)
//         design/levels/qa-1.md: only the block between <!-- qa:begin --> and <!-- qa:end --> is rewritten
// Usage : node prototypes/seven-steps/tools/qa-report.mjs [--attempts <jsonl>] [--notes <dir>] [--out <json>] [--md <md>|--no-md]
// Then  : node prototypes/seven-steps/tools/level-metrics.mjs --html design/levels/interest-curve-50.html --json reports/level-metrics-50.json \
//           --title "50스테이지 + QA 실측" --qa reports/qa/qa-report.json      (measured attempts line on the curve)
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const argv = process.argv.slice(2);
const arg = (name, fallback) => { const i = argv.indexOf(`--${name}`); return i >= 0 ? argv[i + 1] : fallback; };
const fromRoot = (p) => resolve(ROOT, p);
const ATTEMPTS = fromRoot(arg('attempts', 'reports/qa/attempts.jsonl'));
const NOTES_DIR = fromRoot(arg('notes', dirname(ATTEMPTS)));
const METRICS = fromRoot(arg('metrics', 'reports/level-metrics.json'));
const STAGES = fromRoot(arg('stages', 'design/levels/stage-report.json'));
const CHAPTERS = fromRoot(arg('chapters', 'design/levels/chapters.json'));
const OUT = fromRoot(arg('out', 'reports/qa/qa-report.json'));
const MD = argv.includes('--no-md') ? null : fromRoot(arg('md', 'design/levels/qa-1.md'));

// ---- Assumptions (written into the report; tune after the first run) ----
const Q = {
  maxAttempts: Number(arg('max-attempts', 15)), // tester try cap (qa-play-plan 2: 15; QA 1 ran with 10). gave_up also comes from a tester giving up early
  predicted: (D) => 1 + 5 * D, // expected tries per tester from model D (0..1 → 1..6). No data behind it yet: first QA run calibrates it
  tooHardRatio: 2,            // warning when mean tries ≥ 2 × predicted
  outcomes: ['clear', 'restart', 'fell', 'out_of_bodies', 'gave_up'],
};

const fail = (msg) => { console.error(`qa-report: ${msg}`); process.exit(1); };
if (!existsSync(ATTEMPTS)) fail(`no attempts file ${ATTEMPTS}`);
if (!existsSync(METRICS)) fail(`no model metrics ${METRICS} — run prototypes/seven-steps/tools/level-metrics.mjs first`);
const metrics = JSON.parse(readFileSync(METRICS, 'utf8')).rows;
const stageRows = JSON.parse(readFileSync(STAGES, 'utf8')).rows;
const chapters = JSON.parse(readFileSync(CHAPTERS, 'utf8'));
const planetOf = Object.fromEntries((chapters.chapters ?? chapters).map((c) => [c.id, c.planet]));

// ---- Read attempts (strict: a bad line stops the report) ----
const attempts = [];
readFileSync(ATTEMPTS, 'utf8').split(/\r?\n/).forEach((line, i) => {
  if (!line.trim()) return;
  let a;
  try { a = JSON.parse(line); } catch (e) { fail(`${basename(ATTEMPTS)}:${i + 1} is not JSON (${e.message})`); }
  for (const k of ['tester', 'level', 'outcome']) if (a[k] === undefined) fail(`${basename(ATTEMPTS)}:${i + 1} has no "${k}"`);
  if (!Q.outcomes.includes(a.outcome)) fail(`${basename(ATTEMPTS)}:${i + 1} unknown outcome "${a.outcome}"`);
  if (!stageRows[a.level - 1]) fail(`${basename(ATTEMPTS)}:${i + 1} level ${a.level} out of range`);
  const want = stageRows[a.level - 1].name;
  if (a.stageName && a.stageName !== want) fail(`${basename(ATTEMPTS)}:${i + 1} level ${a.level} is "${a.stageName}", this build has "${want}" (wrong levels.js?)`);
  attempts.push({ ...a, line: i + 1 });
});
if (!attempts.length) fail('attempts file is empty');
const testers = [...new Set(attempts.map((a) => a.tester))].sort();

// ---- Notes: "- 12: …", "12. …", "## 12 …", "| 12 | … |" ----
const notes = {}; // level → [{tester, text}]
if (existsSync(NOTES_DIR)) {
  for (const f of readdirSync(NOTES_DIR).filter((f) => /^notes-.+\.md$/.test(f))) {
    const tester = f.slice(6, -3);
    for (const line of readFileSync(join(NOTES_DIR, f), 'utf8').split(/\r?\n/)) {
      const m = line.match(/^\s*(?:[-*#|]+\s*)?(?:stage\s*|스테이지\s*)?(\d{1,2})\s*(?:[.:)|\]-]|번)\s*(.+)$/i);
      if (!m || +m[1] < 1 || +m[1] > stageRows.length) continue;
      (notes[+m[1]] ??= []).push({ tester, text: m[2].replace(/\|\s*$/, '').trim() });
    }
  }
}

// ---- Per stage ----
const r2 = (v) => (v === null || v === undefined || Number.isNaN(v) ? null : Math.round(v * 100) / 100);
const mean = (xs) => (xs.length ? xs.reduce((s, v) => s + v, 0) / xs.length : null);
// inputs run from the stage session start, earlier tries and restarts (!) included (docs/qa-attempts.md 3).
// The cleared run itself is what follows the last "!"; only U D L R K X count (Z undo, H hint do not).
const inputCount = (s) => { s = String(s ?? ''); return s.slice(s.lastIndexOf('!') + 1).replace(/[^UDLRKX]/g, '').length; };

const levels = stageRows.map((st, k) => {
  const level = k + 1;
  const m = metrics.find((r) => r.name === st.name) ?? metrics[k];
  const mine = attempts.filter((a) => a.level === level);
  const perTester = testers.map((t) => {
    const xs = mine.filter((a) => a.tester === t).sort((a, b) => (a.attempt ?? a.line) - (b.attempt ?? b.line));
    if (!xs.length) return null;
    // docs/qa-attempts.md 4: a gave_up line where nothing was done since the previous line only marks the stop; not a try.
    const isMarker = (a) => a.outcome === 'gave_up' && ['steps', 'keeps', 'switches', 'undos', 'hints'].every((k) => !(a[k] ?? 0));
    const played = xs.filter((a) => !isMarker(a));
    const tries = Math.max(played.length, ...played.map((a) => a.attempt ?? 0));
    const clear = xs.find((a) => a.outcome === 'clear');
    return {
      tester: t, tries, cleared: !!clear, gaveUp: xs.some((a) => a.outcome === 'gave_up'),
      firstTryClear: xs[0].outcome === 'clear',
      stars: clear?.stars ?? null, bodiesUsed: clear?.bodiesUsed ?? null, clearInputs: clear ? inputCount(clear.inputs) : null,
      hinted: xs.some((a) => (a.hints ?? 0) > 0),
      undos: xs.reduce((s, a) => s + (a.undos ?? 0), 0),
    };
  }).filter(Boolean);

  const fails = {};
  for (const a of mine) if (a.outcome !== 'clear') {
    const key = a.failReason ? `${a.outcome}:${a.failReason}` : a.outcome;
    fails[key] = (fails[key] ?? 0) + 1;
  }
  const triesList = perTester.map((p) => p.tries);
  const clears = perTester.filter((p) => p.cleared);
  const stars = { 1: 0, 2: 0, 3: 0 };
  clears.forEach((p) => { if (stars[p.stars] !== undefined) stars[p.stars]++; });
  const predicted = m ? Q.predicted(m.D) : null;
  const meanTries = mean(triesList);
  return {
    level, name: st.name, chapter: st.chapter, planet: planetOf[st.chapter] ?? st.chapter,
    model: m ? { D: m.D, I: m.I, A: m.A } : null,
    predictedTries: r2(predicted),
    testers: perTester.length,
    tries: Object.fromEntries(perTester.map((p) => [p.tester, p.tries])),
    meanTries: r2(meanTries), maxTries: triesList.length ? Math.max(...triesList) : null,
    ratioToPredicted: predicted && meanTries !== null ? r2(meanTries / predicted) : null,
    firstTryClearRate: perTester.length ? r2(perTester.filter((p) => p.firstTryClear).length / perTester.length) : null,
    clearRate: perTester.length ? r2(clears.length / perTester.length) : null,
    gaveUp: perTester.filter((p) => p.gaveUp).length,
    failReasons: fails,
    minBodies: st.minBodies, minInputs: st.minInputs,
    meanClearInputs: r2(mean(clears.map((p) => p.clearInputs))),
    inputRatio: clears.length && st.minInputs ? r2(mean(clears.map((p) => p.clearInputs)) / st.minInputs) : null,
    meanBodiesUsed: r2(mean(clears.map((p) => p.bodiesUsed).filter((v) => v !== null))),
    stars,
    hintRate: perTester.length ? r2(perTester.filter((p) => p.hinted).length / perTester.length) : null,
    notes: notes[level] ?? [],
  };
});

// ---- Warnings ----
const warnings = [];
const played = levels.filter((l) => l.testers > 0);
for (const l of played) {
  if (l.gaveUp) warnings.push({ level: l.level, kind: 'gave_up', text: `${l.gaveUp}/${l.testers}명 포기 줄 있음 (상한 ${Q.maxAttempts}회 도달 또는 스스로 포기)` });
  if (l.ratioToPredicted !== null && l.ratioToPredicted >= Q.tooHardRatio)
    warnings.push({ level: l.level, kind: 'too_hard', text: `평균 ${l.meanTries}회, 예측 ${l.predictedTries}회의 ${l.ratioToPredicted}배` });
  if (l.testers === testers.length && l.maxTries === 1 && l.clearRate === 1)
    warnings.push({ level: l.level, kind: 'too_easy', text: '모든 테스터가 1회에 클리어' });
}
const firsts = played.filter((l) => l.level > 1 && stageRows[l.level - 2].chapter !== l.chapter);
for (const l of firsts) {
  const fin = levels[l.level - 2];
  if (fin.testers && l.meanTries > fin.meanTries)
    warnings.push({ level: l.level, kind: 'planet_start_harder', text: `행성 첫 스테이지 평균 ${l.meanTries}회 > 직전 피날레 ${fin.level}의 ${fin.meanTries}회 (톱니 곡선 어긋남)` });
}
const ORDER = { gave_up: 0, too_hard: 1, planet_start_harder: 2, too_easy: 3 };
warnings.sort((a, b) => ORDER[a.kind] - ORDER[b.kind] || a.level - b.level);

// Spearman rank correlation between model D and mean tries (ties get average rank).
const rank = (xs) => { const idx = xs.map((v, i) => [v, i]).sort((a, b) => a[0] - b[0]); const r = []; for (let i = 0; i < idx.length;) { let j = i; while (j + 1 < idx.length && idx[j + 1][0] === idx[i][0]) j++; for (let k = i; k <= j; k++) r[idx[k][1]] = (i + j) / 2 + 1; i = j + 1; } return r; };
const pearson = (a, b) => { const ma = mean(a), mb = mean(b); let n = 0, da = 0, db = 0; a.forEach((v, i) => { n += (v - ma) * (b[i] - mb); da += (v - ma) ** 2; db += (b[i] - mb) ** 2; }); return da && db ? n / Math.sqrt(da * db) : null; };
const both = played.filter((l) => l.model && l.meanTries !== null);
const spearmanD = both.length >= 3 ? r2(pearson(rank(both.map((l) => l.model.D)), rank(both.map((l) => l.meanTries)))) : null;

const report = {
  generated: new Date().toISOString(),
  source: { attempts: ATTEMPTS.replace(ROOT, '').replace(/^[\\/]/, ''), lines: attempts.length, testers },
  assumptions: { maxAttempts: Q.maxAttempts, predictedTries: '1 + 5 × D (model D from level-metrics)', tooHardRatio: Q.tooHardRatio },
  summary: {
    stagesPlayed: played.length, cleared: played.filter((l) => l.clearRate === 1).length,
    gaveUpStages: played.filter((l) => l.gaveUp).length,
    meanTries: r2(mean(played.map((l) => l.meanTries))), spearmanDvsTries: spearmanD,
    byPlanet: [...new Set(levels.map((l) => l.chapter))].map((c) => {
      const ls = played.filter((l) => l.chapter === c);
      return { chapter: c, planet: planetOf[c] ?? c, stages: ls.length, meanTries: r2(mean(ls.map((l) => l.meanTries))), gaveUp: ls.reduce((s, l) => s + l.gaveUp, 0), star3: ls.reduce((s, l) => s + l.stars[3], 0), clears: ls.reduce((s, l) => s + l.stars[1] + l.stars[2] + l.stars[3], 0) };
    }),
  },
  warnings, levels,
};
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, JSON.stringify(report, null, 2) + '\n');

// ---- qa-1.md generated block ----
if (MD) {
  const pct = (v) => (v === null ? '–' : `${Math.round(v * 100)}%`);
  const s = report.summary;
  const KIND = { gave_up: '포기', too_hard: '예측의 2배 이상', planet_start_harder: '행성 첫 스테이지 > 직전 피날레', too_easy: '너무 쉬움(전원 1회)' };
  const failText = (f) => Object.entries(f).sort((a, b) => b[1] - a[1]).map(([k, v]) => `${k} ${v}`).join(', ') || '–';
  let out = `<!-- qa:begin (tools/qa-report.mjs가 다시 씀. 이 블록 밖에만 손으로 적는다) -->\n`;
  out += `생성 ${report.generated.slice(0, 16).replace('T', ' ')} UTC · 시도 ${attempts.length}줄 · 테스터 ${testers.join(', ')} · 예측 시도 = 1 + 5×D (가정)\n\n`;
  out += `### 요약\n\n- 플레이한 스테이지 ${s.stagesPlayed} / ${stageRows.length}, 전원 클리어 ${s.cleared}, 포기 나온 스테이지 ${s.gaveUpStages}\n`;
  out += `- 테스터당 평균 시도 ${s.meanTries ?? '–'}회. 모델 D와 평균 시도의 순위 상관(스피어만) ${s.spearmanDvsTries ?? '–'}\n\n`;
  out += `| 행성 | 스테이지 | 평균 시도 | 포기 | ★3 / 클리어 |\n|---|---|---|---|---|\n`;
  out += s.byPlanet.map((p) => `| ${p.planet} | ${p.stages} | ${p.meanTries ?? '–'} | ${p.gaveUp} | ${p.star3} / ${p.clears} |`).join('\n') + '\n\n';
  out += `### 경고 (${warnings.length})\n\n`;
  out += warnings.length ? warnings.map((w) => `- **${w.level} ${levels[w.level - 1].name}** — ${KIND[w.kind]}: ${w.text}`).join('\n') + '\n\n' : '없음\n\n';
  out += `### 스테이지별\n\n| # | 이름 | D | 예측 | 시도 (${testers.join('/')}) | 평균 | 최대 | 첫 시도 클리어 | 포기 | 실패 원인 | 입력/최소 | ★1/★2/★3 | 풀이 보기 |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|\n`;
  out += levels.map((l) => `| ${l.level} | ${l.name} | ${l.model?.D ?? '–'} | ${l.predictedTries ?? '–'} | ${testers.map((t) => l.tries[t] ?? '–').join(' / ')} | ${l.meanTries ?? '–'} | ${l.maxTries ?? '–'} | ${pct(l.firstTryClearRate)} | ${l.gaveUp || ''} | ${failText(l.failReasons)} | ${l.inputRatio ?? '–'} | ${l.stars[1]}/${l.stars[2]}/${l.stars[3]} | ${pct(l.hintRate)} |`).join('\n') + '\n\n';
  const noted = levels.filter((l) => l.notes.length);
  out += `### 테스터 메모 (스테이지 번호로 시작하는 줄)\n\n`;
  out += noted.length ? noted.map((l) => `- **${l.level} ${l.name}**: ${l.notes.map((n) => `${n.tester} "${n.text}"`).join(' · ')}`).join('\n') + '\n' : '없음\n';
  out += `<!-- qa:end -->`;
  if (!existsSync(MD)) fail(`${MD} not found (template with the qa:begin/qa:end markers)`);
  const md = readFileSync(MD, 'utf8');
  const re = /<!-- qa:begin[\s\S]*?<!-- qa:end -->/;
  if (!re.test(md)) fail(`${MD} has no <!-- qa:begin --> … <!-- qa:end --> block`);
  writeFileSync(MD, md.replace(re, out));
}

console.log(`qa-report: ${attempts.length} attempts, ${testers.length} testers, ${played.length} stages, ${warnings.length} warnings, spearman(D, tries) ${spearmanD}`);
for (const w of warnings) console.log(`  ! ${String(w.level).padStart(2)} ${w.kind}: ${w.text}`);
console.log(`→ ${OUT}${MD ? `\n→ ${MD}` : ''}`);
