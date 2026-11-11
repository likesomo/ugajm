// QA play harness (production/qa-play-plan.md 1): play a stage on the real rules (core.js + levels.js) from the shell,
// one command per call, for blind tester agents. Every attempt is one line of reports/qa/attempts.jsonl; the format is
// the contract in docs/qa-attempts.md (Unity BuildTools.QaReplay replays the same lines).
// Never prints a solution: no solver, no expected values, no minimum body count. Stars are shown on a clear, as in the game.
//
// Usage (repo root):  node unity/Tools/qa-play.mjs <command> --tester T [args]
//   start --level N [--lang ko|en]   begin stage N (1-50)
//   show [--json]                     board, legend, steps, brothers, statues, doors
//   move UDLR...                      one or more moves, e.g. move RRDD (stops at the first blocked move)
//   keep | switch                     after a 7th step on floor
//   undo | restart | hint | giveup | status
//   verify                            re-check every line of attempts.jsonl (docs/qa-attempts.md 7)
// QA_DIR (env) overrides the output folder (default <repo>/reports/qa).
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseLevel, newState, dispatch, tileAt, doorOpen, isDoor, statueCount, stars, STEPS } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS } from '../../prototypes/seven-steps/src/levels.js';

export const FORMAT_VERSION = 1;
const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const qaDir = () => resolve(process.env.QA_DIR || join(root, 'reports', 'qa'));
const attemptsPath = () => join(qaDir(), 'attempts.jsonl');
const sessionPath = (tester) => join(qaDir(), 'sessions', `${tester}.json`);

export class QaError extends Error {}
const fail = (msg) => { throw new QaError(msg); };

// ---------- levels ----------

let exported = null;
function exportedLevels() {
  exported ??= JSON.parse(readFileSync(join(here, '..', 'Assets', '_Project', 'Data', 'levels.json'), 'utf8')).levels;
  return exported;
}

export const mapHash = (map) => createHash('sha256').update(map.join('\n'), 'utf8').digest('hex').slice(0, 16);

/// Stage n (1-based): definition, parsed level and levels.json minBodies (never printed). Throws when out of range or stale.
export function stage(n) {
  if (!Number.isInteger(n) || n < 1 || n > LEVELS.length) fail(`level must be 1-${LEVELS.length}, got ${n}`);
  const def = LEVELS[n - 1];
  const ex = exportedLevels()[n - 1];
  if (!ex || ex.name !== def.name || mapHash(ex.map) !== mapHash(def.map) || !ex.expected?.minBodies)
    fail(`levels.json is stale for stage ${n} (run unity/Tools/unity.sh levels)`);
  return { def, level: parseLevel(def), minBodies: ex.expected.minBodies };
}

function textOf(def, lang) {
  const t = lang === 'ko' ? null : (def.text ?? []).find((x) => x.lang === lang);
  return { name: t?.name ?? def.name, story: t?.story ?? def.story ?? '', hint: t?.hint ?? def.hint ?? '' };
}

// ---------- state key, replay (docs/qa-attempts.md 6, 7) ----------

// Same text as C# Rules.Key: keyOf fields with the status as its enum number (Play 0, Choose 1, Win 2, Lose 3).
const STATUS_NO = { play: 0, choose: 1, win: 2, lose: 3 };
export const stateKey = (s) => `${s.start}|${s.pos}|${s.used}|${s.bodies}|${s.statues}|${s.bridges}|${s.broken}|${STATUS_NO[s.status]}|${s.flipped ? 1 : 0}`;
// FNV-1a 32 over the UTF-8 bytes, 8 lowercase hex digits (C# QaReplay.StateHash).
export function keyHash(k) {
  let h = 0x811c9dc5;
  for (const b of Buffer.from(k, 'utf8')) { h ^= b; h = Math.imul(h, 0x01000193) >>> 0; }
  return h.toString(16).padStart(8, '0');
}
const TOKEN_CMD = { U: { type: 'move', dir: 'up' }, D: { type: 'move', dir: 'down' }, L: { type: 'move', dir: 'left' }, R: { type: 'move', dir: 'right' },
  K: { type: 'keep' }, X: { type: 'switch' }, Z: { type: 'undo' }, '!': { type: 'restart' } };
const LOSS = ['fall', 'bridge', 'seal', 'vanish', 'stone'];

/// Runs tokens from newState(level). Returns { state, lastEvents, error } (error = index and token without effect).
export function runTokens(level, inputs) {
  let s = newState(level);
  let lastEvents = [];
  for (let i = 0; i < inputs.length; i++) {
    const c = inputs[i];
    if (c === 'H') continue;
    const cmd = TOKEN_CMD[c];
    if (!cmd) return { state: s, lastEvents, error: `token ${i} "${c}" is not U D L R K X Z H !` };
    if (c === '!' && !s.prev) return { state: s, lastEvents, error: `token ${i} "!" on a fresh state (such a restart is not counted and never recorded)` };
    const r = dispatch(s, cmd);
    if (r.state === s) return { state: s, lastEvents, error: `token ${i} "${c}" had no effect` };
    s = r.state; lastEvents = r.events;
  }
  return { state: s, lastEvents, error: null };
}

const bodiesUsedOf = (s) => s.level.bodies - s.bodies + (s.status === 'lose' ? 0 : 1);
const count = (str, chars) => [...str].filter((c) => chars.includes(c)).length;
const lossOf = (events) => [...events].reverse().find((e) => LOSS.includes(e.type))?.type ?? null;

/// Checks one attempts.jsonl line against a fresh replay (the reference for BuildTools.QaReplay). Returns a list of problems.
export function replayLine(line) {
  const problems = [];
  let st;
  try { st = stage(line.level); } catch (e) { return [`level: ${e.message}`]; }
  if (mapHash(st.def.map) !== line.mapHash) return ['skipped: map changed'];
  const r = runTokens(st.level, line.inputs);
  if (r.error) return [r.error];
  const s = r.state;
  const want = { clear: ['win'], fell: ['lose'], out_of_bodies: ['lose'], restart: ['play', 'choose'], gave_up: ['play', 'choose', 'lose'] }[line.outcome];
  if (!want) problems.push(`outcome "${line.outcome}" unknown`);
  else if (!want.includes(s.status)) problems.push(`outcome ${line.outcome} but status ${s.status}`);
  if (s.status === 'lose') {
    const fell = r.lastEvents.some((e) => e.type === 'fall');
    if (line.outcome === 'fell' && !fell) problems.push('outcome fell without a fall event');
    if (line.outcome === 'out_of_bodies' && fell) problems.push('outcome out_of_bodies with a fall event');
    if ((line.outcome === 'fell' || line.outcome === 'out_of_bodies') && lossOf(r.lastEvents) !== line.failReason)
      problems.push(`failReason ${line.failReason}, replay ${lossOf(r.lastEvents)}`);
  }
  const k = stateKey(s);
  if (k !== line.stateKey) problems.push(`stateKey ${line.stateKey}, replay ${k}`);
  if (keyHash(k) !== line.stateHash) problems.push(`stateHash ${line.stateHash}, replay ${keyHash(k)}`);
  for (const [f, ch] of [['levelUndos', 'Z'], ['levelHints', 'H'], ['levelRestarts', '!']])
    if (count(line.inputs, ch) !== line[f]) problems.push(`${f} ${line[f]}, replay ${count(line.inputs, ch)}`);
  if (bodiesUsedOf(s) !== line.bodiesUsed) problems.push(`bodiesUsed ${line.bodiesUsed}, replay ${bodiesUsedOf(s)}`);
  const own = line.inputs.slice(line.from);
  for (const [f, chars] of [['steps', 'UDLR'], ['keeps', 'K'], ['switches', 'X'], ['undos', 'Z'], ['hints', 'H']])
    if (count(own, chars) !== line[f]) problems.push(`${f} ${line[f]}, replay ${count(own, chars)}`);
  const wantStars = s.status === 'win'
    ? stars({ bodiesUsed: bodiesUsedOf(s), bodies: st.level.bodies, hints: line.levelHints })
    : null;
  if (wantStars !== line.stars) problems.push(`stars ${line.stars}, replay ${wantStars}`);
  return problems;
}

// ---------- session ----------

function loadSession(tester) {
  const p = sessionPath(tester);
  if (!existsSync(p)) fail(`no session for tester "${tester}": run start --tester ${tester} --level N`);
  const ss = JSON.parse(readFileSync(p, 'utf8'));
  if (!ss.active) fail(`tester "${tester}" has no stage in progress (last: stage ${ss.level}, ${ss.ended}): run start --level N`);
  return ss;
}
const saveSession = (ss) => { mkdirSync(dirname(sessionPath(ss.tester)), { recursive: true }); writeFileSync(sessionPath(ss.tester), JSON.stringify(ss, null, 2)); };
const stateOf = (ss) => {
  const r = runTokens(stage(ss.level).level, ss.inputs);
  if (r.error) fail(`session file is inconsistent (${r.error}); start the stage again`);
  return r;
};

function nextAttemptNo(tester, level) {
  if (!existsSync(attemptsPath())) return 1;
  let n = 0;
  for (const l of readFileSync(attemptsPath(), 'utf8').split('\n')) {
    if (!l.trim()) continue;
    const j = JSON.parse(l);
    if (j.tester === tester && j.level === level) n++;
  }
  return n + 1;
}

/// Writes the line for the attempt that just ended and opens the next one at the end of the run.
function closeAttempt(ss, outcome, s, lastEvents) {
  const st = stage(ss.level);
  const own = ss.inputs.slice(ss.from);
  const lose = outcome === 'fell' || outcome === 'out_of_bodies';
  const line = {
    v: FORMAT_VERSION, tester: ss.tester, level: ss.level, stageName: st.def.name, chapter: st.def.chapter ?? '',
    mapHash: mapHash(st.def.map), lang: ss.lang, attempt: nextAttemptNo(ss.tester, ss.level), outcome,
    failReason: lose ? lossOf(lastEvents) : null,
    inputs: ss.inputs, from: ss.from,
    steps: count(own, 'UDLR'), keeps: count(own, 'K'), switches: count(own, 'X'), undos: count(own, 'Z'), hints: count(own, 'H'),
    bodiesUsed: bodiesUsedOf(s), levelUndos: ss.levelUndos, levelHints: ss.levelHints, levelRestarts: ss.levelRestarts,
    stars: outcome === 'clear'
      ? stars({ bodiesUsed: bodiesUsedOf(s), bodies: st.level.bodies, hints: ss.levelHints })
      : null,
    stateKey: stateKey(s), stateHash: keyHash(stateKey(s)), startT: ss.attemptT, t: new Date().toISOString(),
  };
  mkdirSync(qaDir(), { recursive: true });
  appendFileSync(attemptsPath(), JSON.stringify(line) + '\n');
  ss.from = ss.inputs.length;
  ss.attemptT = line.t;
  return line;
}

// ---------- output ----------

const xy = (lv, i) => `(${i % lv.w},${Math.floor(i / lv.w)})`;
const LEGEND = '@ 지금 형제  * 선돌  S 출발점  E 출구  # 벽  . 바닥  o 구멍  = 다리  ~ 얼음  x 금 간 바닥  m 진흙\n'
  + 'a-c 발판  A-C 문(발판에 선돌이 서면 열림)  1-7 숫자 문(그 번째 걸음으로만)  Q 선돌 수 문  L 레버  R 올라온 벽  r 내려간 벽';

export function view(ss, s) {
  const { def } = stage(ss.level);
  const lv = s.level;
  const tx = textOf(def, ss.lang);
  const rows = [];
  for (let y = 0; y < lv.h; y++) {
    let row = '';
    for (let x = 0; x < lv.w; x++) {
      const i = y * lv.w + x;
      if (i === s.pos && s.status !== 'lose') row += '@';
      else if (s.statues.includes(i)) row += '*';
      else if (i === s.start) row += 'S';
      else row += tileAt(s, i);
    }
    rows.push(row);
  }
  const doors = [];
  lv.tiles.forEach((_, i) => { const t = tileAt(s, i); if (isDoor(t)) doors.push({ at: xy(lv, i), x: i % lv.w, y: Math.floor(i / lv.w), door: t, open: doorOpen(s, t) }); });
  return {
    level: ss.level, levels: LEVELS.length, name: tx.name, chapter: def.chapter ?? '', story: tx.story, hint: tx.hint,
    status: s.status, rows, width: lv.w, height: lv.h,
    player: { x: s.pos % lv.w, y: Math.floor(s.pos / lv.w) }, start: { x: s.start % lv.w, y: Math.floor(s.start / lv.w) },
    stepsUsed: s.used, stepsLeft: STEPS - s.used, brothers: lv.bodies, brothersLeft: s.bodies,
    brother: s.status === 'lose' ? null : lv.bodies - s.bodies + 1,
    statues: s.statues.map((i) => ({ x: i % lv.w, y: Math.floor(i / lv.w) })), statuesStanding: statueCount(s),
    statueCountDoor: lv.tiles.includes('Q') ? lv.statueCount : null,
    doors, trail: s.trail.map((t) => ({ x: t.i % lv.w, y: Math.floor(t.i / lv.w), step: t.n })),
    counts: { undos: ss.levelUndos, hints: ss.levelHints, restarts: ss.levelRestarts },
  };
}

function printView(v) {
  const out = [`[${v.level}/${v.levels}] ${v.name} (${v.chapter})`, `이야기: ${v.story}`, `힌트 문구: ${v.hint}`, ''];
  const ruler = '   ' + Array.from({ length: v.width }, (_, x) => x % 10).join('');
  out.push(ruler, ...v.rows.map((r, y) => `${String(y).padStart(2)} ${r}`), '');
  out.push(LEGEND, '');
  const status = { play: '진행 중', choose: '7번째 걸음: keep 또는 switch 를 고른다', win: '클리어', lose: '형제를 모두 썼다 (undo / restart / giveup)' }[v.status];
  out.push(`상태: ${status}`);
  out.push(`걸음: ${v.stepsUsed}/7 사용, 남은 걸음 ${v.stepsLeft}   형제: ${v.brother ?? '-'}번째 / ${v.brothers} (남은 형제 ${v.brothersLeft})`);
  out.push(`지금 위치 (${v.player.x},${v.player.y})  출발점 (${v.start.x},${v.start.y})  서 있는 선돌 ${v.statuesStanding}`
    + (v.statueCountDoor != null ? `  (Q 문은 선돌이 정확히 ${v.statueCountDoor}개일 때 열림)` : ''));
  if (v.doors.length) out.push('문: ' + v.doors.map((d) => `${d.door}${d.at} ${d.open ? '열림' : '닫힘'}`).join(', '));
  out.push('이번 형제의 발자국: ' + v.trail.map((t) => `${t.step}(${t.x},${t.y})`).join(' '));
  out.push(`이 스테이지에서: 되돌리기 ${v.counts.undos}, 힌트 ${v.counts.hints}, 재시작 ${v.counts.restarts}`);
  return out.join('\n');
}

function describe(lv, events) {
  const w = (e) => ({
    step: `${e.stepIdx}번째 걸음 ${xy(lv, e.at)}`, slide: `얼음에서 미끄러짐`, crumble: `금 간 바닥 ${xy(lv, e.at)} 무너짐`,
    fall: `구멍 ${xy(lv, e.at)}에 떨어짐: 형제 하나를 잃음`, bridge: `7번째 걸음에 구멍 ${xy(lv, e.at)}: 형제가 다리가 됨`,
    seal: `7번째 걸음에 금 간 바닥 ${xy(lv, e.at)}: 굳어서 다리가 됨`, vanish: `출발점에서 7번째 걸음: 형제가 사라짐`,
    choose: `7번째 걸음 ${xy(lv, e.at)}: keep 또는 switch`, stone: `선돌이 섬 ${xy(lv, e.at)}`, switch: `SWITCH: ${xy(lv, e.at)}가 새 출발점`,
    respawn: `다음 형제가 ${xy(lv, e.at)}에서 깨어남`, lose: `형제를 모두 썼다`, win: `출구에 7번째 걸음으로 도착: 클리어`,
    flip: `레버: 벽이 뒤집힘`, doorOpen: `문 ${xy(lv, e.at)} 열림`, doorClose: `문 ${xy(lv, e.at)} 닫힘`, undo: `되돌림`, restart: `재시작`,
  }[e.type] ?? e.type);
  return events.map((e) => '  - ' + w(e)).join('\n');
}

// ---------- commands ----------

const TESTER_RE = /^[A-Za-z0-9_-]{1,32}$/;

/// Runs one command; returns the text to print. Throws QaError on a bad command (exit 2).
export function run(argv) {
  const [cmd, ...rest] = argv;
  const opt = (name) => { const i = rest.indexOf(name); if (i < 0) return undefined; const v = rest[i + 1]; if (v === undefined || v.startsWith('--')) fail(`${name} needs a value`); return v; };
  const flag = (name) => rest.includes(name);
  if (!cmd || cmd === 'help' || cmd === '--help') return HELP;
  if (cmd === 'verify') return verify();
  const tester = opt('--tester');
  if (!tester) fail('--tester T is required');
  if (!TESTER_RE.test(tester)) fail('--tester must be 1-32 of A-Z a-z 0-9 _ -');

  if (cmd === 'start') {
    const raw = opt('--level');
    if (raw === undefined) fail('start needs --level N');
    if (!/^\d+$/.test(raw)) fail(`level must be 1-${LEVELS.length}, got ${raw}`);
    const level = Number(raw);
    const lang = opt('--lang') ?? 'ko';
    if (lang !== 'ko' && lang !== 'en') fail('--lang must be ko or en');
    stage(level);
    const p = sessionPath(tester);
    if (existsSync(p)) {
      const old = JSON.parse(readFileSync(p, 'utf8'));
      if (old.active) fail(`stage ${old.level} is still in progress: clear it or run giveup first`);
    }
    const now = new Date().toISOString();
    const ss = { tester, level, lang, active: true, inputs: '', from: 0, levelUndos: 0, levelHints: 0, levelRestarts: 0, startT: now, attemptT: now };
    saveSession(ss);
    const n = nextAttemptNo(tester, level);
    return `스테이지 ${level} 시작 (이 스테이지 시도 ${n}번째)\n\n` + printView(view(ss, newState(stage(level).level)));
  }

  const ss = loadSession(tester);
  const { state: s0 } = stateOf(ss);
  const lv = s0.level;

  switch (cmd) {
    case 'show': {
      const v = view(ss, s0);
      return flag('--json') ? JSON.stringify(v, null, 2) : printView(v);
    }
    case 'status': {
      const v = view(ss, s0);
      return `스테이지 ${v.level} ${v.name}: ${v.status}, 걸음 ${v.stepsUsed}/7, 형제 ${v.brother ?? '-'}/${v.brothers}, 선돌 ${v.statuesStanding}, `
        + `되돌리기 ${v.counts.undos}, 힌트 ${v.counts.hints}, 재시작 ${v.counts.restarts}, 이 시도 입력 ${ss.inputs.length - ss.from}`;
    }
    case 'move': {
      const seq = rest.find((a, i) => !a.startsWith('--') && !(i > 0 && rest[i - 1].startsWith('--')));
      if (!seq || !/^[UDLRudlr]+$/.test(seq)) fail('move needs directions, e.g. move RRDD (U D L R)');
      if (s0.status === 'choose') fail('choose first: keep or switch');
      if (s0.status !== 'play') fail(`cannot move: status ${s0.status}`);
      return applyTokens(ss, s0, seq.toUpperCase());
    }
    case 'keep': case 'switch': {
      if (s0.status !== 'choose') fail(`${cmd} only after a 7th step on floor (status ${s0.status})`);
      return applyTokens(ss, s0, cmd === 'keep' ? 'K' : 'X');
    }
    case 'undo': {
      if (!s0.prev) fail('nothing to undo');
      if (s0.status === 'win') fail('stage already cleared');
      return applyTokens(ss, s0, 'Z');
    }
    case 'hint': {
      ss.inputs += 'H'; ss.levelHints++;
      saveSession(ss);
      return `힌트 문구: ${textOf(stage(ss.level).def, ss.lang).hint}\n(힌트 사용 ${ss.levelHints}회 기록. 이 스테이지 세션에서는 ★1까지만)`;
    }
    case 'restart': {
      let msg = '';
      if (playedSince(ss)) { const line = closeAttempt(ss, 'restart', s0, []); msg = `시도 ${line.attempt} 기록: restart\n`; }
      // The game counts a restart only when something was done (AttemptCounts); only a counted restart is a token.
      if (s0.prev) { ss.inputs += '!'; ss.levelRestarts++; }
      ss.attemptT = new Date().toISOString();
      saveSession(ss);
      return msg + '재시작\n\n' + printView(view(ss, newState(lv)));
    }
    case 'giveup': {
      // Nothing played since the last line: the line is only a stop marker (steps+keeps+switches+undos+hints = 0).
      const line = closeAttempt(ss, 'gave_up', s0, []);
      ss.active = false; ss.ended = 'gave_up';
      saveSession(ss);
      return `시도 ${line.attempt} 기록: gave_up. 스테이지 ${ss.level}을 끝냈다. 다음은 start --level N`;
    }
    default:
      fail(`unknown command "${cmd}" (start show move keep switch undo restart hint giveup status verify)`);
  }
}

// Something other than restarts was entered since the last line.
const playedSince = (ss) => ss.inputs.slice(ss.from).replace(/!/g, '') !== '';

function applyTokens(ss, s0, tokens) {
  let s = s0;
  const out = [];
  let applied = 0;
  for (const c of tokens) {
    if (s.status !== 'play' && 'UDLR'.includes(c)) { out.push(`(남은 입력 ${tokens.slice(applied)}는 무시: 상태 ${s.status})`); break; }
    const r = dispatch(s, TOKEN_CMD[c]);
    if (r.state === s) { out.push(`막힘: ${c} (${applied}개 적용, 남은 입력 ${tokens.slice(applied)}는 무시)`); break; }
    if (c === 'Z') ss.levelUndos++;
    ss.inputs += c; applied++;
    s = r.state;
    out.push(`${c}:\n${describe(s.level, r.events)}`);
    if (s.status === 'win' || s.status === 'lose') {
      const outcome = s.status === 'win' ? 'clear' : r.events.some((e) => e.type === 'fall') ? 'fell' : 'out_of_bodies';
      const line = closeAttempt(ss, outcome, s, r.events);
      if (outcome === 'clear') { ss.active = false; ss.ended = 'clear'; out.push(`\n클리어! ${'★'.repeat(line.stars)}${'☆'.repeat(3 - line.stars)} (시도 ${line.attempt})`); }
      else out.push(`\n시도 ${line.attempt} 기록: ${outcome}. undo 로 이어 하거나 restart / giveup`);
      if (applied < tokens.length) out.push(`(남은 입력 ${tokens.slice(applied)}는 무시)`);
      break;
    }
  }
  saveSession(ss);
  if (applied === 0 && out.length) { saveSession(ss); fail(out.join('\n')); }
  return out.join('\n') + '\n\n' + printView(view(ss, s));
}

function verify() {
  if (!existsSync(attemptsPath())) return `${attemptsPath()}: no attempts yet`;
  const lines = readFileSync(attemptsPath(), 'utf8').split('\n').filter((l) => l.trim());
  const bad = [];
  lines.forEach((l, i) => {
    let j;
    try { j = JSON.parse(l); } catch { bad.push(`line ${i + 1}: not JSON`); return; }
    if (j.v !== FORMAT_VERSION) { bad.push(`line ${i + 1}: v ${j.v}`); return; }
    for (const p of replayLine(j)) bad.push(`line ${i + 1} (${j.tester} stage ${j.level} #${j.attempt}): ${p}`);
  });
  if (bad.length) fail(`verify: ${bad.length} problem(s) in ${lines.length} lines\n` + bad.join('\n'));
  return `verify OK: ${lines.length} lines`;
}

const HELP = `qa-play: play Seven Steps stages from the shell (docs/qa-attempts.md)
  start --tester T --level N [--lang ko|en]
  show --tester T [--json]      status --tester T
  move --tester T RRDD          keep|switch|undo|restart|hint|giveup --tester T
  verify                        re-check reports/qa/attempts.jsonl`;

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  try { console.log(run(process.argv.slice(2))); }
  catch (e) {
    if (e instanceof QaError) { console.error('error: ' + e.message); process.exit(2); }
    throw e;
  }
}
