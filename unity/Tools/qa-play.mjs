// QA play harness (production/qa-play-plan.md 1; QA-3 docs/qa-attempts.md 10): play a stage or a tutorial board on the
// real rules (core.js + levels.js) from the shell, one command per call, for blind tester agents. Every attempt is one
// line of reports/qa/attempts.jsonl; the format is the contract in docs/qa-attempts.md (Unity BuildTools.QaReplay
// replays the same lines).
// Never prints a solution: no solver, no expected values, no minimum body count.
// Naive (QA-3, the only mode): show / status / help / errors explain no rule. The only rule words a tester reads are
// the game's own texts at that moment: the stage start line, the HUD message line (Data/Strings msg.*), the
// KEEP / SWITCH button tips, the first-slide guide, the tutorial cue plate, the clear screen's star reasons and
// the stage hint line on `hint` (the game's first H tier). docs/qa-attempts.md 10.3 lists every text.
//
// Usage (repo root):  node unity/Tools/qa-play.mjs <command> [--tester T] [args]
//   start --tester T --level N|T1|T2|T3 [--lang ko|en]   begin stage N (1-50) or a tutorial board
//   show --tester T [--json]          board, HUD, the line the game shows now
//   move --tester T UDLR...           one or more moves, e.g. move RRDD (stops at the first blocked move)
//   keep | switch --tester T          on the KEEP / SWITCH choice
//   undo | restart | hint | giveup | status --tester T
//   story prologue|epilogue [--lang ko|en]   the cutscene's pages, as the game shows them
//   verify                            re-check every line of attempts.jsonl (docs/qa-attempts.md 7)
// QA_DIR (env) overrides the output folder (default <repo>/reports/qa).
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseLevel, newState, dispatch, tileAt, doorOpen, isDoor, statueCount, stars, STEPS } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS, TUTORIALS } from '../../prototypes/seven-steps/src/levels.js';

export const FORMAT_VERSION = 1;
const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const data = join(here, '..', 'Assets', '_Project', 'Data');
const qaDir = () => resolve(process.env.QA_DIR || join(root, 'reports', 'qa'));
const attemptsPath = () => join(qaDir(), 'attempts.jsonl');
const sessionPath = (tester) => join(qaDir(), 'sessions', `${tester}.json`);

export class QaError extends Error {}
const fail = (msg) => { throw new QaError(msg); };

// ---------- game texts (Data/Strings, Data/cutscenes.json: what the game shows) ----------

const tables = {};
/// A string-table line in ko / en, {0} {1} filled in. Throws for a missing key (the harness must show game text only).
export function gameText(lang, key, ...args) {
  tables[lang] ??= Object.fromEntries(JSON.parse(readFileSync(join(data, 'Strings', `${lang}.json`), 'utf8')).entries.map((e) => [e.key, e.text]));
  const t = tables[lang][key];
  if (t === undefined) throw new Error(`string ${key} missing in ${lang}.json`);
  return t.replace(/\{(\d+)(?::[^}]*)?\}/g, (_, i) => String(args[Number(i)]));
}

/// Pages of a cutscene ("prologue", "epilogue") in a language: [[line, ...], ...], as U5CutsceneView shows them.
export function storyPages(scene, lang) {
  const file = JSON.parse(readFileSync(join(data, 'cutscenes.json'), 'utf8'));
  const s = file.scenes.find((x) => x.id === scene);
  if (!s) fail(`story must be one of ${file.scenes.map((x) => x.id).join(', ')}`);
  return s.pages.map((p) => p.lines.map((l) => (lang === 'ko' ? l.line : (l.text ?? []).find((t) => t.lang === lang)?.line ?? l.line)));
}

// ---------- levels and tutorial boards ----------

let exported = null;
const exportedFile = () => (exported ??= JSON.parse(readFileSync(join(data, 'levels.json'), 'utf8')));

export const mapHash = (map) => createHash('sha256').update(map.join('\n'), 'utf8').digest('hex').slice(0, 16);
const TUTORIAL_RE = /^T([1-9])$/;
export const isTutorial = (level) => typeof level === 'string';
const levelRange = () => `1-${LEVELS.length} or T1-T${TUTORIALS.length}`;

/// Stage n (1-based) or tutorial board "T1".."T3": definition, parsed level, levels.json minBodies (never printed) and,
/// for a board, its exported entry (cues as the game reads them). Throws when out of range or levels.json is stale.
export function stage(n) {
  if (isTutorial(n)) {
    const m = TUTORIAL_RE.exec(n);
    const k = m ? Number(m[1]) : 0;
    if (!m || k > TUTORIALS.length) fail(`level must be ${levelRange()}, got ${n}`);
    const def = TUTORIALS[k - 1].level;
    const ex = (exportedFile().tutorials ?? [])[k - 1];
    if (!ex || ex.id !== TUTORIALS[k - 1].id || ex.level.name !== def.name || mapHash(ex.level.map) !== mapHash(def.map))
      fail(`levels.json is stale for tutorial ${n} (run unity/Tools/unity.sh levels)`);
    return { def, level: parseLevel(def), minBodies: ex.level.expected.minBodies, tutorial: ex };
  }
  if (!Number.isInteger(n) || n < 1 || n > LEVELS.length) fail(`level must be ${levelRange()}, got ${n}`);
  const def = LEVELS[n - 1];
  const ex = exportedFile().levels[n - 1];
  if (!ex || ex.name !== def.name || mapHash(ex.map) !== mapHash(def.map) || !ex.expected?.minBodies)
    fail(`levels.json is stale for stage ${n} (run unity/Tools/unity.sh levels)`);
  return { def, level: parseLevel(def), minBodies: ex.expected.minBodies, tutorial: null };
}

/// "--level" text: "12" -> 12, "T2" / "t2" -> "T2".
export function parseLevelArg(raw) {
  if (/^\d+$/.test(raw)) return Number(raw);
  if (/^[Tt]\d+$/.test(raw)) return raw.toUpperCase();
  fail(`level must be ${levelRange()}, got ${raw}`);
}

function textOf(def, lang) {
  const t = lang === 'ko' ? null : (def.text ?? []).find((x) => x.lang === lang);
  return { name: t?.name ?? def.name, story: t?.story ?? def.story ?? '', hint: t?.hint ?? def.hint ?? '' };
}
/// The stage start line (LevelDto.StartLine + StoryDirector.StartText): story by default, hint, or none.
function startLineOf(def, lang) {
  const tx = textOf(def, lang);
  const which = def.startLine ?? 'story';
  return which === 'story' ? tx.story : which === 'hint' ? tx.hint : '';
}

// ---------- tutorial cues (same rule as C# Core TutorialCues, docs/tutorial.md 2.3) ----------

const lastCue = (t, on) => t.cues.reduce((f, c, i) => (c.on === on ? i : f), -1);
export const cueStart = (t) => lastCue(t, 'start');
export const cueRestart = (t) => { const i = lastCue(t, 'restart'); return i >= 0 ? i : cueStart(t); };
const SEVENTH = ['win', 'choose', 'stone', 'vanish', 'bridge', 'seal'];
const bodyNo = (s) => s.level.bodies - s.bodies + 1; // C# Stars.BodiesUsed
/// The cue one applied token shows (U D L R K X Z !), -1 = keep the current line. Last match in event order, then list order.
export function matchCue(t, token, before, after, events) {
  if (token === '!') return cueRestart(t);
  if (token === 'Z') return -1;
  const move = 'UDLR'.includes(token);
  let found = -1;
  for (const e of events) {
    t.cues.forEach((c, i) => {
      const who = e.type === 'respawn' ? bodyNo(after) : bodyNo(before);
      if (c.body && c.body !== who) return;
      const fires = {
        step: e.type === 'step' && e.stepIdx === c.n,
        seventh: move && SEVENTH.includes(e.type),
        choose: e.type === 'choose',
        keep: token === 'K' && e.type === 'stone',
        switch: token === 'X' && e.type === 'switch',
        doorOpen: e.type === 'doorOpen',
        respawn: e.type === 'respawn',
        win: e.type === 'win',
        lose: e.type === 'lose',
      }[c.on];
      if (fires) found = i;
    });
  }
  return found;
}
const cueLine = (c, lang) => (lang === 'ko' ? c.line : c.text.find((x) => x.lang === lang)?.line ?? c.line);

// ---------- HUD message line (Game MessagePresenter: the stage's line; a tutorial board mutes it) ----------

const MSG = { choose: 'msg.choose', stone: 'msg.stone', switch: 'msg.switch', bridge: 'msg.bridge', seal: 'msg.seal', fall: 'msg.fall',
  vanish: 'msg.vanish', crumble: 'msg.crumble', win: 'msg.win', lose: 'msg.lose', undo: 'msg.undo', restart: 'msg.restart', flip: 'msg.flip' };
/// The message key after one command's events ('' = empty line, null = unchanged). hinted: this attempt opened a hint.
function messageKey(events, current, hinted) {
  let key = current;
  for (const e of events) {
    if (MSG[e.type]) key = e.type === 'restart' && hinted ? 'msg.restart_hinted' : MSG[e.type];
    if (e.type === 'step') key = e.stepIdx === STEPS - 1 ? 'msg.lucky_next' : '';
  }
  return key;
}
const SLIDE_GUIDE = 'tutorial.slide'; // TutorialIds.Slide: once per player, on the first slide

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
  if (line.stageName !== st.def.name) return [`stageName ${line.stageName}, levels.js ${st.def.name}`];
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
  if (!ss.active) fail(`tester "${tester}" has no stage in progress (last: ${ss.level}, ${ss.ended}): run start --level N`);
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
// What each map letter looks like on the board. No rule: the game draws tiles without explaining them.
const LEGEND = '@ 지금 형제  * 선돌  S 출발점  E 출구  # 벽  . 바닥  o 구멍  ~ 얼음  x 금 간 바닥  m 진흙\n'
  + 'a-c 발판  A-C 문  1-7 숫자가 적힌 문  Q 숫자가 적힌 문(Q 숫자 참고)  L 레버  R 올라온 벽  r 내려간 벽';

export function view(ss, s) {
  const st = stage(ss.level);
  const { def } = st;
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
  const cue = st.tutorial && ss.cue >= 0 ? st.tutorial.cues[ss.cue] : null;
  return {
    level: ss.level, levels: LEVELS.length, tutorial: !!st.tutorial, name: tx.name, chapter: def.chapter ?? '',
    // A stage shows its start line and the HUD message line; a tutorial board shows its cue plate instead (docs 10.3).
    startLine: st.tutorial ? '' : startLineOf(def, ss.lang),
    message: st.tutorial || !ss.msg ? '' : gameText(ss.lang, ss.msg),
    cue: cue ? cueLine(cue, ss.lang) : '',
    cueCell: cue && cue.cell.length === 2 ? { x: cue.cell[0], y: cue.cell[1] } : null,
    choice: s.status === 'choose' ? { keep: gameText(ss.lang, 'ui5.choice.keep'), switch: gameText(ss.lang, 'ui5.choice.switch') } : null,
    status: s.status, rows, width: lv.w, height: lv.h,
    player: { x: s.pos % lv.w, y: Math.floor(s.pos / lv.w) }, start: { x: s.start % lv.w, y: Math.floor(s.start / lv.w) },
    stepsUsed: s.used, brothers: lv.bodies, brothersLeft: s.bodies,
    brother: s.status === 'lose' ? null : lv.bodies - s.bodies + 1,
    statues: s.statues.map((i) => ({ x: i % lv.w, y: Math.floor(i / lv.w) })), statuesStanding: statueCount(s),
    statueCountDoor: lv.tiles.includes('Q') ? lv.statueCount : null,
    doors, trail: s.trail.map((t) => ({ x: t.i % lv.w, y: Math.floor(t.i / lv.w), step: t.n })),
    counts: { undos: ss.levelUndos, hints: ss.levelHints, restarts: ss.levelRestarts },
  };
}

const STATUS_TEXT = { play: '진행 중', choose: 'KEEP 또는 SWITCH', win: '클리어', lose: '남은 형제 없음 (undo / restart / giveup)' };
function printView(v) {
  const head = v.tutorial ? `[튜토리얼 ${v.level}] ${v.name}` : `[${v.level}/${v.levels}] ${v.name} (${v.chapter})`;
  const out = [head];
  if (v.startLine) out.push(`시작 문구: ${v.startLine}`);
  if (v.tutorial) out.push(`안내: ${v.cue || '(없음)'}` + (v.cueCell ? `   [빛나는 칸 (${v.cueCell.x},${v.cueCell.y})]` : ''));
  else out.push(`화면 문구: ${v.message || '(없음)'}`);
  out.push('');
  const ruler = '   ' + Array.from({ length: v.width }, (_, x) => x % 10).join('');
  out.push(ruler, ...v.rows.map((r, y) => `${String(y).padStart(2)} ${r}`), '');
  out.push(LEGEND, '');
  out.push(`상태: ${STATUS_TEXT[v.status]}`);
  if (v.choice) out.push(`  keep: ${v.choice.keep}`, `  switch: ${v.choice.switch}`);
  out.push(`걸음: ${v.stepsUsed}/7   형제: ${v.brother ?? '-'}번째 / ${v.brothers} (남은 형제 ${v.brothersLeft})`);
  out.push(`지금 위치 (${v.player.x},${v.player.y})  출발점 (${v.start.x},${v.start.y})  서 있는 선돌 ${v.statuesStanding}`
    + (v.statueCountDoor != null ? `  (Q 문 위의 숫자: ${v.statueCountDoor})` : ''));
  if (v.doors.length) out.push('문: ' + v.doors.map((d) => `${d.door}${d.at} ${d.open ? '열림' : '닫힘'}`).join(', '));
  out.push('이번 형제의 발자국: ' + v.trail.map((t) => `${t.step}(${t.x},${t.y})`).join(' '));
  if (!v.tutorial) out.push(`이 스테이지에서: 되돌리기 ${v.counts.undos}, 힌트 ${v.counts.hints}, 재시작 ${v.counts.restarts}`);
  return out.join('\n');
}

// What the eye sees on the board for each event (no reason given). The game's own words are the message line / cue.
function describe(lv, events) {
  const w = (e) => ({
    step: `걸음 ${e.stepIdx} ${xy(lv, e.at)}`, slide: '얼음에서 미끄러짐', crumble: `금 간 바닥 ${xy(lv, e.at)} 무너짐`,
    fall: `구멍 ${xy(lv, e.at)}에 떨어짐`, bridge: `구멍 ${xy(lv, e.at)}에 다리가 생김`, seal: `금 간 바닥 ${xy(lv, e.at)}이 굳음`,
    vanish: `출발점 ${xy(lv, e.at)}에서 형제가 사라짐`, choose: `${xy(lv, e.at)}: KEEP 또는 SWITCH`, stone: `선돌 ${xy(lv, e.at)}`,
    switch: `${xy(lv, e.at)}가 새 출발점`, respawn: `다음 형제가 ${xy(lv, e.at)}에서 깨어남`, lose: '남은 형제 없음', win: `출구 ${xy(lv, e.at)}: 클리어`,
    flip: `레버 ${xy(lv, e.at)}: 벽이 뒤집힘`, doorOpen: `문 ${xy(lv, e.at)} 열림`, doorClose: `문 ${xy(lv, e.at)} 닫힘`, undo: '되돌림', restart: '재시작',
  }[e.type] ?? e.type);
  return events.filter((e) => e.type !== 'bump').map((e) => '  - ' + w(e)).join('\n');
}

/// The clear screen's three stars and their reasons (U5ClearView tips result.star1..3).
function starLines(lang, got, bodies) {
  const reasons = [gameText(lang, 'result.star1'), gameText(lang, 'result.star2'), gameText(lang, bodies === 1 ? 'result.star3_one' : 'result.star3_many', bodies)];
  return reasons.map((r, k) => `  ${k < got ? '★' : '☆'} ${r}`).join('\n');
}

// ---------- commands ----------

const TESTER_RE = /^[A-Za-z0-9_-]{1,32}$/;

/// Runs one command; returns the text to print. Throws QaError on a bad command (exit 2).
export function run(argv) {
  const [cmd, ...rest] = argv;
  const opt = (name) => { const i = rest.indexOf(name); if (i < 0) return undefined; const v = rest[i + 1]; if (v === undefined || v.startsWith('--')) fail(`${name} needs a value`); return v; };
  const flag = (name) => rest.includes(name);
  const langOpt = () => { const l = opt('--lang') ?? 'ko'; if (l !== 'ko' && l !== 'en') fail('--lang must be ko or en'); return l; };
  if (!cmd || cmd === 'help' || cmd === '--help') return HELP;
  if (cmd === 'verify') return verify();
  if (cmd === 'story') {
    const scene = rest.find((a, i) => !a.startsWith('--') && !(i > 0 && rest[i - 1].startsWith('--')));
    if (!scene) fail('story needs a scene: story prologue [--lang ko|en]');
    const pages = storyPages(scene, langOpt());
    return pages.map((lines, k) => `[${k + 1}/${pages.length}]\n${lines.join('\n')}`).join('\n\n');
  }
  const tester = opt('--tester');
  if (!tester) fail('--tester T is required');
  if (!TESTER_RE.test(tester)) fail('--tester must be 1-32 of A-Z a-z 0-9 _ -');

  if (cmd === 'start') {
    const raw = opt('--level');
    if (raw === undefined) fail('start needs --level N');
    const level = parseLevelArg(raw);
    const lang = langOpt();
    const st = stage(level);
    const p = sessionPath(tester);
    let guides = [];
    if (existsSync(p)) {
      const old = JSON.parse(readFileSync(p, 'utf8'));
      if (old.active) fail(`${old.level} is still in progress: clear it or run giveup first`);
      guides = old.guides ?? [];
    }
    const now = new Date().toISOString();
    const ss = { tester, level, lang, active: true, inputs: '', from: 0, levelUndos: 0, levelHints: 0, levelRestarts: 0, startT: now, attemptT: now,
      msg: '', cue: st.tutorial ? cueStart(st.tutorial) : -1, guides };
    saveSession(ss);
    const n = nextAttemptNo(tester, level);
    const what = st.tutorial ? `튜토리얼 ${level}` : `스테이지 ${level}`;
    return `${what} 시작 (시도 ${n}번째)\n\n` + printView(view(ss, newState(st.level)));
  }

  const ss = loadSession(tester);
  const { state: s0 } = stateOf(ss);
  const lv = s0.level;
  const st = stage(ss.level);

  switch (cmd) {
    case 'show': {
      const v = view(ss, s0);
      return flag('--json') ? JSON.stringify(v, null, 2) : printView(v);
    }
    case 'status': {
      const v = view(ss, s0);
      return `${v.tutorial ? '튜토리얼' : '스테이지'} ${v.level} ${v.name}: ${STATUS_TEXT[v.status]}, 걸음 ${v.stepsUsed}/7, 형제 ${v.brother ?? '-'}/${v.brothers}, 선돌 ${v.statuesStanding}, `
        + `되돌리기 ${v.counts.undos}, 힌트 ${v.counts.hints}, 재시작 ${v.counts.restarts}, 이 시도 입력 ${ss.inputs.length - ss.from}`;
    }
    case 'move': {
      const seq = rest.find((a, i) => !a.startsWith('--') && !(i > 0 && rest[i - 1].startsWith('--')));
      if (!seq || !/^[UDLRudlr]+$/.test(seq)) fail('move needs directions, e.g. move RRDD (U D L R)');
      if (s0.status === 'choose') fail(gameText(ss.lang, 'msg.choose_nudge'));
      if (s0.status !== 'play') fail(`cannot move: ${STATUS_TEXT[s0.status]}`);
      return applyTokens(ss, s0, seq.toUpperCase());
    }
    case 'keep': case 'switch': {
      if (s0.status !== 'choose') fail(`${cmd}: 지금은 고를 수 없다 (${STATUS_TEXT[s0.status]})`);
      return applyTokens(ss, s0, cmd === 'keep' ? 'K' : 'X');
    }
    case 'undo': {
      if (!s0.prev) fail('nothing to undo');
      if (s0.status === 'win') fail('already cleared');
      return applyTokens(ss, s0, 'Z');
    }
    case 'hint': {
      // The game's H on a stage opens its hint line first (BoardController HintTier Principle); a tutorial board has no H.
      if (st.tutorial) fail('no hint on a tutorial board');
      ss.inputs += 'H'; ss.levelHints++;
      saveSession(ss);
      const line = textOf(st.def, ss.lang).hint || gameText(ss.lang, 'hint.not_found');
      // The game asks first (hint.confirm: a hint caps this attempt at 1 star); the harness counts at once.
      return `${line}\n(힌트 사용 ${ss.levelHints}회 기록. ${gameText(ss.lang, 'hint.confirm').split(/(?<=[.!?])\s/)[0]})`;
    }
    case 'restart': {
      let msg = '';
      if (playedSince(ss)) { const line = closeAttempt(ss, 'restart', s0, []); msg = `시도 ${line.attempt} 기록: restart\n`; }
      // The game counts a restart only when something was done (AttemptCounts); only a counted restart is a token.
      if (s0.prev) { ss.inputs += '!'; ss.levelRestarts++; }
      ss.attemptT = new Date().toISOString();
      noteRestart(ss, st);
      saveSession(ss);
      return msg + '재시작\n\n' + printView(view(ss, newState(lv)));
    }
    case 'giveup': {
      // Nothing played since the last line: the line is only a stop marker (steps+keeps+switches+undos+hints = 0).
      const line = closeAttempt(ss, 'gave_up', s0, []);
      ss.active = false; ss.ended = 'gave_up';
      saveSession(ss);
      return `시도 ${line.attempt} 기록: gave_up. ${ss.level}을 끝냈다. 다음은 start --level N`;
    }
    default:
      fail(`unknown command "${cmd}" (start show move keep switch undo restart hint giveup status story verify)`);
  }
}

// Something other than restarts was entered since the last line.
const playedSince = (ss) => ss.inputs.slice(ss.from).replace(/!/g, '') !== '';

// The line after a restart: the board's restart cue, or the stage's message line.
function noteRestart(ss, st) {
  if (st.tutorial) ss.cue = cueRestart(st.tutorial);
  else ss.msg = messageKey([{ type: 'restart' }], ss.msg, ss.levelHints > 0);
}

/// One applied token: the message line or the cue, and the one-time slide guide (stages only: a board mutes the line).
function noteEvents(ss, st, token, before, after, events) {
  if (st.tutorial) {
    const c = matchCue(st.tutorial, token, before, after, events);
    if (c >= 0) ss.cue = c;
    return;
  }
  ss.msg = messageKey(events, ss.msg, ss.levelHints > 0);
  if (events.some((e) => e.type === 'slide') && !ss.guides.includes('slide@1')) { ss.guides.push('slide@1'); ss.msg = SLIDE_GUIDE; }
}

function applyTokens(ss, s0, tokens) {
  const st = stage(ss.level);
  let s = s0;
  const out = [];
  let applied = 0;
  for (const c of tokens) {
    if (s.status !== 'play' && 'UDLR'.includes(c)) { out.push(`(남은 입력 ${tokens.slice(applied)}는 무시: ${STATUS_TEXT[s.status]})`); break; }
    const r = dispatch(s, TOKEN_CMD[c]);
    if (r.state === s) { out.push(`막힘: ${c} (${applied}개 적용, 남은 입력 ${tokens.slice(applied)}는 무시)`); break; }
    if (c === 'Z') ss.levelUndos++;
    ss.inputs += c; applied++;
    noteEvents(ss, st, c, s, r.state, r.events);
    s = r.state;
    out.push(`${c}:\n${describe(s.level, r.events)}`);
    if (s.status === 'win' || s.status === 'lose') {
      const outcome = s.status === 'win' ? 'clear' : r.events.some((e) => e.type === 'fall') ? 'fell' : 'out_of_bodies';
      const line = closeAttempt(ss, outcome, s, r.events);
      if (outcome === 'clear') {
        ss.active = false; ss.ended = 'clear';
        // A tutorial board has no clear screen: its win cue, then the next board (TutorialRunController).
        if (st.tutorial) out.push(`\n튜토리얼 ${ss.level} 클리어 (시도 ${line.attempt})\n안내: ${cueLine(st.tutorial.cues[ss.cue] ?? { line: '', text: [] }, ss.lang)}`);
        else out.push(`\n클리어! ${'★'.repeat(line.stars)}${'☆'.repeat(3 - line.stars)} (시도 ${line.attempt})\n${starLines(ss.lang, line.stars, st.level.bodies)}`);
      } else if (st.tutorial) {
        // The game shows the lose cue, then restarts by itself (1.2 s).
        const lost = cueLine(st.tutorial.cues[ss.cue] ?? { line: '(없음)', text: [] }, ss.lang);
        ss.inputs += '!'; ss.levelRestarts++;
        noteRestart(ss, st);
        s = newState(s.level);
        out.push(`\n시도 ${line.attempt} 기록: ${outcome}\n안내: ${lost}\n(자동 재시작)`);
      } else out.push(`\n시도 ${line.attempt} 기록: ${outcome}. undo 로 이어 하거나 restart / giveup`);
      if (applied < tokens.length) out.push(`(남은 입력 ${tokens.slice(applied)}는 무시)`);
      break;
    }
  }
  saveSession(ss);
  if (applied === 0 && out.length) fail(out.join('\n'));
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
    for (const p of replayLine(j)) bad.push(`line ${i + 1} (${j.tester} ${j.level} #${j.attempt}): ${p}`);
  });
  if (bad.length) fail(`verify: ${bad.length} problem(s) in ${lines.length} lines\n` + bad.join('\n'));
  return `verify OK: ${lines.length} lines`;
}

const HELP = `qa-play: play Seven Steps from the shell (docs/qa-attempts.md)
  start --tester T --level N|T1|T2|T3 [--lang ko|en]
  show --tester T [--json]      status --tester T
  move --tester T RRDD          keep|switch|undo|restart|hint|giveup --tester T
  story prologue|epilogue [--lang ko|en]
  verify                        re-check reports/qa/attempts.jsonl`;

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  try { console.log(run(process.argv.slice(2))); }
  catch (e) {
    if (e instanceof QaError) { console.error('error: ' + e.message); process.exit(2); }
    throw e;
  }
}
