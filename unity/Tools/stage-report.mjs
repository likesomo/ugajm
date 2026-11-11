// Stage report for playtests: per stage the solver's fewest bodies and shortest solutions, which body counts can win,
// and the star thresholds (design/levels/stars.md 0, core.js stars()). Read-only: it only reads levels.js and levels.json.
// Also checks levels.json "expected" still matches what the solver finds now (stale export = run `unity.sh levels`).
// Usage (repo root): node unity/Tools/stage-report.mjs [--out <dir>]   → <dir>/stage-report.md + .json (default reports/)
// Exit 1 when a stage is unsolvable, over the body cap, capped by the search limit, or levels.json is stale.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseLevel, newState, step, choose, actions, keyOf, minBodies, solve, stars, DIRS } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

const BODY_CAP = 3;
const STATE_LIMIT = 600000; // same budget as design/levels/stars.md
const LETTER = { up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' };
const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const argOut = process.argv.indexOf('--out');
const outDir = argOut > 0 ? resolve(process.argv[argOut + 1]) : join(root, 'reports');

const exported = JSON.parse(readFileSync(join(here, '..', 'Assets', '_Project', 'Data', 'levels.json'), 'utf8')).levels;
const replay = (path) => path.map((a) => LETTER[a]).join('');
const apply = (s, a) => (DIRS[a] ? step(s, a) : choose(s, a)).state;

// Full BFS from the start with the stage's own body budget: every body count that can end in a win, and the state count.
function explore(level) {
  const start = newState(level);
  const seen = new Set([keyOf(start)]);
  const winBodies = new Set();
  let frontier = [start];
  while (frontier.length && seen.size < STATE_LIMIT) {
    const next = [];
    for (const s of frontier) {
      for (const a of actions(s)) {
        const t = apply(s, a);
        if (t === s || t.status === 'lose') continue;
        if (t.status === 'win') { winBodies.add(level.bodies - t.bodies + 1); continue; }
        const k = keyOf(t);
        if (seen.has(k)) continue;
        seen.add(k);
        next.push(t);
      }
    }
    frontier = next;
  }
  return { winBodies: [...winBodies].sort(), states: seen.size, capped: frontier.length > 0 };
}

const problems = [];
const rows = LEVELS.map((def, i) => {
  const stage = i + 1;
  const level = parseLevel(def);
  const tag = `${stage}. ${def.name}`;
  if (level.bodies > BODY_CAP) problems.push(`${tag}: bodies ${level.bodies} > ${BODY_CAP}`);
  const best = minBodies(level);
  if (!best) { problems.push(`${tag}: unsolvable`); return { stage, name: def.name, chapter: def.chapter ?? '', bodies: level.bodies, ok: false }; }
  const shortest = solve(level, level.bodies); // fewest inputs, any body count within the budget
  const needsSwitch = !solve(level, level.bodies, { noSwitch: true });
  const ex = explore(level);
  if (ex.capped) problems.push(`${tag}: search capped at ${STATE_LIMIT} states (winBodies may be incomplete)`);
  const minPath = replay(best.path);
  const exp = exported[i]?.expected;
  const stale = !exp || exported[i].name !== def.name || exp.minBodies !== best.bodies || exp.moves !== best.moves
    || exp.path !== minPath || exp.needsSwitch !== needsSwitch;
  if (stale) problems.push(`${tag}: levels.json expected differs from the solver (run unity.sh levels)`);
  return {
    stage, name: def.name, chapter: def.chapter ?? '', bodies: level.bodies, ok: true,
    minBodies: best.bodies,
    minPath, minInputs: best.moves, minDirInputs: best.path.filter((a) => DIRS[a]).length,
    shortestPath: replay(shortest.path), shortestInputs: shortest.path.length, shortestBodies: shortest.bodiesUsed,
    winBodies: ex.winBodies, star2Splits: ex.winBodies.some((b) => b > best.bodies),
    needsSwitch, states: ex.states, capped: ex.capped, levelsJsonMatches: !stale,
  };
});

// Star thresholds per stage, straight from core.js stars() so the table cannot drift from the rule.
for (const r of rows.filter((r) => r.ok)) {
  const m = r.minBodies, b = r.bodies;
  r.stars = {
    star3AllBodies: stars({ bodiesUsed: b, bodies: b }) === 3,
    star3WithUndoRestart: stars({ bodiesUsed: b, bodies: b, undos: 1, restarts: 1 }) === 3,
    star2FewerBodies: m < b ? stars({ bodiesUsed: m, bodies: b }) === 2 : null,
    star1WithHint: stars({ bodiesUsed: b, bodies: b, hints: 1 }) === 1,
  };
  // ★3 needs a win on the last body (design/levels/all-brothers-check.json has the same answer).
  r.star3Possible = r.winBodies.includes(b);
  if (!Object.values(r.stars).every((v) => v !== false)) problems.push(`${r.stage}. ${r.name}: stars() thresholds unexpected`);
}

const gen = new Date().toISOString().slice(0, 10);
const ok = rows.filter((r) => r.ok);
const splits = ok.filter((r) => r.star2Splits).length;
const no3 = ok.filter((r) => !r.star3Possible);
const md = [
  `# 스테이지 솔버 리포트 (${rows.length}스테이지)`,
  '',
  `생성: \`node unity/Tools/stage-report.mjs\` (${gen}, 코어). 입력: \`prototypes/seven-steps/src/levels.js\` + \`core.js\` 솔버, \`levels.json\` expected 대조.`,
  '스테이지를 바꾸면 다시 실행한다. 리플레이 형식: U D L R 이동, K KEEP, X SWITCH (`-replay` / `?replay=`).',
  '',
  '## 별 기준 (design/levels/stars.md 0, 가정 2026-11-11 — `core.js` `stars()` / C# `Stars.Of`)',
  '',
  '| 별 | 조건 (한 시도 안에서) |',
  '|---|---|',
  '| ★1 | 클리어 |',
  '| ★2 | ★1 + 풀이 보기(H) 0회 |',
  '| ★3 | ★2 + 형제를 모두 씀 (쓴 몸 수 = 몸) |',
  '',
  `- ★3이 불가능한 스테이지(몸을 모두 써서 이길 수 없음): **${no3.length ? no3.map((r) => r.stage).join(', ') : '없음'}**`,
  `- 최소 몸보다 많은 몸으로도 이길 수 있는 스테이지: ${splits}/${ok.length}`,
  '- 최단 입력 = 몸 수와 상관없이 입력(이동+K/X)이 가장 적은 해. 최소 몸 해와 다르면 "최단"에 몸 수를 적는다',
  `- 상태 수 = 몸 예산 전체로 BFS 한 상태 수(한도 ${STATE_LIMIT}). 난이도 직접 지표는 아니고 탐색 공간 크기`,
  `- 문제: ${problems.length ? problems.length + '건 (아래)' : '없음 (모든 스테이지 풀이 가능, 몸 ≤ 3, levels.json 최신)'}`,
  '',
  '## 스테이지별',
  '',
  '| # | 챕터 | 이름 | 몸 | 최소 몸 | 이기는 몸 수 | ★3 | SWITCH 필수 | 최소 몸 해 입력(방향) | 최단 입력 | 상태 수 | 최소 몸 해 (리플레이) |',
  '|---|---|---|---|---|---|---|---|---|---|---|---|',
  ...rows.map((r) => r.ok
    ? `| ${r.stage} | ${r.chapter} | ${r.name} | ${r.bodies} | ${r.minBodies} | ${r.winBodies.join('/')} | ${r.star3Possible ? '가능' : '**불가**'} | ${r.needsSwitch ? '예' : ''} | ${r.minInputs} (${r.minDirInputs}) | ${r.shortestInputs}${r.shortestBodies !== r.minBodies ? ` (몸 ${r.shortestBodies})` : ''} | ${r.states}${r.capped ? '+' : ''} | \`${r.minPath}\` |`
    : `| ${r.stage} | ${r.chapter} | ${r.name} | ${r.bodies} | **풀이 없음** | | | | | | | |`),
  '',
  ...(problems.length ? ['## 문제', '', ...problems.map((p) => `- ${p}`), ''] : []),
].join('\n');

mkdirSync(outDir, { recursive: true });
writeFileSync(join(outDir, 'stage-report.md'), md);
writeFileSync(join(outDir, 'stage-report.json'), JSON.stringify({ generated: gen, stateLimit: STATE_LIMIT, problems, rows }, null, 2));
console.log(`[stage-report] ${rows.length} stages, ★2 splits ${splits}, problems ${problems.length} → ${outDir}`);
for (const p of problems) console.log('  ' + p);
process.exit(problems.length ? 1 : 0);
