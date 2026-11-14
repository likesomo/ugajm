// Tests for qa-play.mjs and the docs/qa-attempts.md contract. Run: node --test unity/Tools/qa-play.test.mjs
// The tests use the solver to find inputs; the harness itself never does.
import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, existsSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { run, replayLine, stage, stateKey, keyHash, runTokens, QaError, matchCue, cueStart, storyPages, gameText } from './qa-play.mjs';
import { newState, step, choose, actions, minBodies, keyOf, DIRS, dispatch } from '../../prototypes/seven-steps/src/core.js';
import { LEVELS, TUTORIALS } from '../../prototypes/seven-steps/src/levels.js';
import { solveAllBodies } from './all-brothers-check.mjs';

const LETTER = { up: 'U', down: 'D', left: 'L', right: 'R', keep: 'K', switch: 'X' };
const word = (path) => path.map((a) => LETTER[a]).join('');
const lines = () => { const p = join(process.env.QA_DIR, 'attempts.jsonl'); return existsSync(p) ? readFileSync(p, 'utf8').trim().split('\n').map((l) => JSON.parse(l)) : []; };
const last = () => lines().at(-1);
const T = ['--tester', 'A'];
const play = (word) => {
  // Moves go through `move`, K/X through keep/switch, Z through undo, H through hint.
  for (const c of word) {
    if ('UDLR'.includes(c)) run(['move', ...T, c]);
    else if (c === 'H') run(['hint', ...T, '--confirm']);
    else run([{ K: 'keep', X: 'switch', Z: 'undo' }[c], ...T]);
  }
};
const count = (str) => [...str].filter((c) => 'UDLR'.includes(c)).length;
// Hints unlock by tries (HintGate, 2026-11-14): set the tester's try count for a stage in its session file.
const seedTries = (tester, level, n) => { const p = join(process.env.QA_DIR, 'sessions', `${tester}.json`); const ss = JSON.parse(readFileSync(p, 'utf8')); ss.tries = { ...(ss.tries ?? {}), [level]: n }; writeFileSync(p, JSON.stringify(ss)); };
const errors = (argv, re) => assert.throws(() => run(argv), (e) => e instanceof QaError && (typeof re === 'function' ? re(e.message) : re.test(e.message)));
// A board's ko cue line by on (levels.js TUTORIALS), so the tests follow wording changes.
const cueOf = (k, on) => TUTORIALS[k].cues.find((c) => c.on === on).line;

// BFS from the start for a command list that ends in a lose whose last command does (or does not) contain a fall.
function findLoss(level, wantFall) {
  const seen = new Set();
  let frontier = [{ s: newState(level), path: [] }];
  while (frontier.length) {
    const next = [];
    for (const { s, path } of frontier) for (const a of actions(s)) {
      const r = DIRS[a] ? step(s, a) : choose(s, a);
      if (r.state === s) continue;
      const p = [...path, a];
      if (r.state.status === 'lose') { if (r.events.some((e) => e.type === 'fall') === wantFall) return p; continue; }
      if (r.state.status === 'win') continue;
      const k = keyOf(r.state); if (seen.has(k)) continue; seen.add(k);
      next.push({ s: r.state, path: p });
    }
    frontier = next;
  }
  return null;
}
const pitStage = LEVELS.findIndex((d) => d.map.join('').includes('o')) + 1;

beforeEach(() => { process.env.QA_DIR = mkdtempSync(join(tmpdir(), 'qa-play-')); });

test('commands before start, bad commands and level range fail at once', () => {
  errors(['show', ...T], /no session/);
  errors(['start', ...T, '--level', '0'], /level must be 1-50/);
  errors(['start', ...T, '--level', 'T4'], /T1-T3/);
  errors(['start', ...T, '--level', 'T0'], /T1-T3/);
  errors(['start', ...T, '--level', '51'], /level must be 1-50/);
  errors(['start', ...T, '--level', 'x'], /level must be/);
  errors(['start', '--level', '1'], /--tester/);
  errors(['start', '--tester', 'a b', '--level', '1'], /--tester must be/);
  run(['start', ...T, '--level', '1']);
  errors(['start', ...T, '--level', '2'], /still in progress/);
  errors(['fly', ...T], /unknown command/);
  errors(['move', ...T, 'RQ'], /move needs directions/);
  errors(['keep', ...T], /지금은 고를 수 없다/);
  errors(['undo', ...T], /nothing to undo/);
  assert.equal(lines().length, 0);
});

// Stars (design/levels/stars.md 0): 1 clear, 2 no hint, 3 also every brother used (bodiesUsed == level bodies).
test('every stage clears with its minimum-body solution: 3 stars only when that uses every brother, the line replays', () => {
  for (let n = 1; n <= LEVELS.length; n++) {
    run(['start', ...T, '--level', String(n)]);
    play(word(minBodies(stage(n).level).path));
    const l = last();
    assert.equal(l.level, n); assert.equal(l.outcome, 'clear');
    assert.equal(l.bodiesUsed, stage(n).minBodies);
    assert.equal(l.stars, l.bodiesUsed === stage(n).level.bodies ? 3 : 2, `stage ${n}`);
    assert.deepEqual(replayLine(l), [], `stage ${n}`);
  }
});

test('stage 4 with every brother: 3 stars even after undo or restart; a hint 1 star; fewer brothers 2 stars', () => {
  const path = word(solveAllBodies(stage(4).level));
  assert.ok(stage(4).minBodies < stage(4).level.bodies, 'stage 4 can be cleared with fewer brothers');
  run(['start', ...T, '--level', '4']); play(path[0] + 'Z' + path);
  assert.equal(last().stars, 3); assert.equal(last().undos, 1); assert.equal(last().inputs, path[0] + 'Z' + path);
  assert.equal(last().bodiesUsed, stage(4).level.bodies);
  run(['start', ...T, '--level', '4']); seedTries('A', 4, 3); play('H' + path);
  assert.equal(last().stars, 1); assert.equal(last().hints, 1); assert.equal(last().attempt, 2);
  run(['start', ...T, '--level', '4']);
  run(['restart', ...T]); // fresh: not counted, no line
  assert.equal(lines().length, 2);
  play(path[0]); run(['restart', ...T]);
  // The restart line ends before the restart; the counted restart is the '!' that starts the next attempt.
  assert.equal(last().outcome, 'restart'); assert.equal(last().inputs, path[0]); assert.equal(last().levelRestarts, 0); assert.equal(last().attempt, 3);
  play(path);
  assert.equal(last().outcome, 'clear'); assert.equal(last().stars, 3); assert.equal(last().levelRestarts, 1); assert.equal(last().attempt, 4);
  assert.equal(last().inputs, path[0] + '!' + path); assert.equal(last().from, 1); assert.equal(last().steps, count(path));
  run(['start', ...T, '--level', '4']); play(word(minBodies(stage(4).level).path));
  assert.equal(last().stars, 2); assert.equal(last().bodiesUsed, stage(4).minBodies);
  for (const l of lines()) assert.deepEqual(replayLine(l), []);
});

test('fell and out_of_bodies end an attempt; undo continues the run with from > 0', () => {
  const fellPath = findLoss(stage(pitStage).level, true);
  assert.ok(fellPath, 'a stage with a pit can be lost by falling');
  run(['start', ...T, '--level', String(pitStage)]); play(word(fellPath));
  const f = last();
  assert.equal(f.outcome, 'fell'); assert.equal(f.failReason, 'fall'); assert.equal(f.bodiesUsed, stage(pitStage).level.bodies);
  errors(['move', ...T, 'R'], /cannot move/);
  run(['undo', ...T]); run(['giveup', ...T]);
  const g = last();
  assert.equal(g.outcome, 'gave_up'); assert.equal(g.from, f.inputs.length); assert.equal(g.inputs, f.inputs + 'Z');
  assert.equal(g.levelUndos, 1); assert.equal(g.undos, 1);

  const oobPath = findLoss(stage(1).level, false);
  run(['start', ...T, '--level', '1']); play(word(oobPath));
  assert.equal(last().outcome, 'out_of_bodies'); assert.ok(['vanish', 'stone', 'bridge', 'seal'].includes(last().failReason));
  run(['restart', ...T]); // after a loss: counted, but no new tokens so no line
  const n = lines().length;
  run(['giveup', ...T]);
  assert.equal(lines().length, n + 1);
  const m = last(); // a stop marker: nothing but the restart since the loss line
  assert.equal(m.inputs, word(oobPath) + '!'); assert.equal(m.levelRestarts, 1);
  assert.equal(m.steps + m.keeps + m.switches + m.undos + m.hints, 0);
  assert.equal(m.stateKey, stateKey(newState(stage(1).level)));
  for (const l of lines()) assert.deepEqual(replayLine(l), []);
});

test('no solution leaks: show --json and every output carry no solver or expected values', () => {
  run(['start', ...T, '--level', '50']);
  const v = JSON.parse(run(['show', ...T, '--json']));
  const text = JSON.stringify(v) + run(['show', ...T]) + run(['status', ...T]) + run(['hint', ...T]);
  for (const k of ['expected', 'minBodies', 'path', 'solution', 'moves']) assert.ok(!text.includes(k), k);
  const exp = JSON.parse(readFileSync(new URL('../Assets/_Project/Data/levels.json', import.meta.url), 'utf8')).levels[49].expected.path;
  assert.ok(!text.includes(exp));
});

test('replayLine catches tampering; finalHash is the key hash; CLI exits 2 on errors', () => {
  run(['start', ...T, '--level', '1']); play(word(minBodies(stage(1).level).path));
  const l = last();
  assert.equal(l.stateHash, keyHash(l.stateKey)); assert.match(l.stateHash, /^[0-9a-f]{8}$/);
  assert.equal(keyHash('a'), 'e40c292c'); // FNV-1a 32 reference value
  assert.match(l.stateKey, /|2|0$/); // status as the C# enum number (Win = 2)
  assert.match(replayLine({ ...l, stars: 3 }).join(), /stars/); // the line has 2 (one brother of 3)
  assert.match(replayLine({ ...l, inputs: l.inputs + 'R' }).join(), /no effect/);
  assert.match(replayLine({ ...l, outcome: 'restart' }).join(), /status win/);
  assert.match(replayLine({ ...l, mapHash: 'x' }).join(), /map changed/);
  const cli = spawnSync(process.execPath, [fileURLToPath(new URL('./qa-play.mjs', import.meta.url)), 'show', '--tester', 'nobody'], { env: process.env, encoding: 'utf8' });
  assert.equal(cli.status, 2); assert.match(cli.stderr, /^error: no session/);
  const ok = spawnSync(process.execPath, [fileURLToPath(new URL('./qa-play.mjs', import.meta.url)), 'verify'], { env: process.env, encoding: 'utf8' });
  assert.equal(ok.status, 0); assert.match(ok.stdout, /verify OK: 1 lines/);
});

// Golden shared with C# (docs/qa-attempts.md 6): scene-flow checks the same stage, inputs, key and hash in EditMode.
// Stage 29 with the smoke replay DURRRRRKDDR plus R ! H and Z K (CLAUDE.md lists that replay; no new solution data).
test('golden: stage 29 "R!HDURRRRRKZKDDR" -> stateKey and stateHash', () => {
  const r = runTokens(stage(29).level, 'R!HDURRRRRKZKDDR');
  assert.equal(r.error, null);
  assert.equal(stateKey(r.state), '10|29|3|2|15|||0|1');
  assert.equal(keyHash(stateKey(r.state)), 'e57f027f');
});

// ---------- QA-3 (docs/qa-attempts.md 10) ----------

const exported = () => JSON.parse(readFileSync(new URL('../Assets/_Project/Data/levels.json', import.meta.url), 'utf8'));
const onOf = (t, i) => (i < 0 ? '-' : t.cues[i].on + (t.cues[i].body ? '/' + t.cues[i].body : ''));
const CMD = { U: { type: 'move', dir: 'up' }, D: { type: 'move', dir: 'down' }, L: { type: 'move', dir: 'left' }, R: { type: 'move', dir: 'right' },
  K: { type: 'keep' }, X: { type: 'switch' }, Z: { type: 'undo' }, '!': { type: 'restart' } };
// Same sequences as the C# TutorialTests (Core TutorialCues): the JS matcher must agree with the game's.
function cues(k, letters) {
  const t = exported().tutorials[k - 1];
  let s = newState(stage('T' + k).level);
  const shown = [];
  for (const c of letters) { const r = dispatch(s, CMD[c]); shown.push(onOf(t, matchCue(t, c, s, r.state, r.events))); s = r.state; }
  return shown;
}

test('tutorial cues match the C# TutorialCues sequences', () => {
  const t1 = exported().tutorials[0];
  assert.equal(onOf(t1, cueStart(t1)), 'start');
  assert.deepEqual(cues(1, 'RRRDDLL'), ['step', '-', '-', '-', '-', 'step', 'win']);
  assert.deepEqual(cues(1, 'RRRDDLR!'), ['step', '-', '-', '-', '-', 'step', 'lose', 'restart']);
  const t2 = cues(2, 'DUDRRRRKDDRRRDD');
  assert.equal(t2[4], 'step/1'); assert.equal(t2[6], 'choose'); assert.equal(t2[7], 'doorOpen'); assert.equal(t2[12], '-'); assert.equal(t2[14], 'win');
  assert.equal(cues(2, 'DUDRRRRX')[7], 'switch');
  const t3 = cues(3, exported().tutorials[2].level.expected.path);
  assert.deepEqual([t3[6], t3[7], t3[14], t3[15], t3.at(-1)], ['choose/1', 'doorOpen', 'choose/2', 'switch', 'win']);
  assert.equal(cues(3, 'RRRRDDRKRRRRDDLK')[15], 'keep/2');
  // doorShut: only a move refused by a closed door (C# DoorShutShowsOnlyOnAClosedDoor).
  const shut = cues(3, 'URRRRDDLXLLLDDR');
  assert.equal(shut[0], '-'); assert.equal(shut[14], 'doorShut');
  assert.equal(cues(2, 'DDRRRD')[5], 'doorShut');
});

test('T1-T3: start, cue plate, lose -> lose cue + automatic restart, clear; lines replay with level "T1"..', () => {
  const out = run(['start', ...T, '--level', 't1']);
  assert.match(out, /튜토리얼 T1/); assert.ok(out.includes('안내: ' + TUTORIALS[0].cues[0].line));
  const lost = run(['move', ...T, 'RRRDDLR']);
  assert.match(lost, /out_of_bodies/); assert.ok(lost.includes(cueOf(0, 'lose'))); assert.match(lost, /자동 재시작/);
  assert.ok(run(['show', ...T]).includes('안내: ' + cueOf(0, 'restart')));
  assert.equal(last().level, 'T1'); assert.equal(last().attempt, 1);
  errors(['hint', ...T], /no hint/);
  const won = run(['move', ...T, 'RRRDDLL']);
  assert.match(won, /튜토리얼 T1 클리어/); assert.ok(won.includes(cueOf(0, 'win')));
  assert.ok(!won.includes('★'), 'a tutorial board has no clear screen');
  assert.equal(last().outcome, 'clear'); assert.equal(last().attempt, 2); assert.equal(last().inputs, 'RRRDDLR!RRRDDLL'); assert.equal(last().from, 7);
  for (const k of [2, 3]) {
    const en = run(['start', ...T, '--level', 'T' + k, '--lang', 'en']);
    assert.ok(en.includes(exported().tutorials[k - 1].cues[0].text[0].line), 'en cue');
    play(exported().tutorials[k - 1].level.expected.path);
    assert.equal(last().outcome, 'clear'); assert.equal(last().level, 'T' + k); assert.equal(last().bodiesUsed, k);
  }
  for (const l of lines()) assert.deepEqual(replayLine(l), [], JSON.stringify(l));
  assert.match(run(['verify']), /verify OK: 4 lines/);
});

test('story prints the cutscene pages in order, ko and en', () => {
  const ko = run(['story', 'prologue']);
  const en = run(['story', 'prologue', '--lang', 'en']);
  const file = JSON.parse(readFileSync(new URL('../Assets/_Project/Data/cutscenes.json', import.meta.url), 'utf8'));
  const pro = file.scenes.find((x) => x.id === 'prologue');
  assert.ok(ko.startsWith(`[1/${pro.pages.length}]`));
  assert.ok(ko.includes(pro.pages[0].lines[0].line)); assert.ok(en.includes(pro.pages[0].lines[0].text[0].line));
  assert.ok(ko.indexOf(pro.pages[0].lines[0].line) < ko.indexOf(pro.pages.at(-1).lines[0].line));
  assert.equal(storyPages('epilogue', 'ko').length, file.scenes.find((x) => x.id === 'epilogue').pages.length);
  // Rule captions (cutscenes v2) sit under their page's lines; the SWITCH page says where the next brother wakes.
  const ruled = pro.pages.filter((p) => p.rule);
  assert.ok(ruled.length >= 2);
  for (const p of ruled) {
    assert.ok(ko.includes('[' + p.rule.line + ']')); assert.ok(en.includes('[' + p.rule.text[0].line + ']'));
    assert.ok(ko.indexOf(p.lines.at(-1).line) < ko.indexOf(p.rule.line));
  }
  errors(['story', 'middle'], /story must be one of/);
});

test('T3 doorShut: a move into the closed door shows the cue, even when nothing else applied', () => {
  run(['start', ...T, '--level', 'T3']);
  play('RRRRDDLX');
  const out = run(['move', ...T, 'LLLDDR']);
  const shutLine = TUTORIALS[2].cues.find((c) => c.on === 'doorShut').line;
  assert.ok(out.indexOf('막힘: R') >= 0 && out.indexOf('막힘: R') < out.indexOf('안내: ' + shutLine), out);
  errors(['move', ...T, 'R'], (m) => m.includes('안내: ' + shutLine));
});

// Naive (docs 10.3): the harness's own words explain no rule. Only the game's texts may (start line, message line,
// button tips, clear screen, hint line), and those come from levels.js / Data/Strings unchanged: they are cut out first.
const RULE_WORDS = [/7번째 걸음으로/, /7th step/i, /그 번째 걸음으로만/, /선돌이 서면/, /정확히/, /모두 써/, /all (three|brothers)/i, /★1까지/, /다리가 됨/, /선돌 수 문/];
const GAME_KEYS = ['msg.win', 'msg.fall', 'msg.bridge', 'msg.seal', 'msg.vanish', 'msg.stone', 'msg.switch', 'msg.choose', 'msg.choose_nudge', 'msg.lucky_next', 'msg.lose',
  'result.star1', 'result.star2', 'result.star3_many', 'hint.confirm', 'ui5.choice.keep', 'ui5.choice.switch', 'tutorial.slide'];
function harnessOnly(text, n) {
  let t = text;
  const def = LEVELS[n - 1];
  const game = [...GAME_KEYS.flatMap((k) => [gameText('ko', k, 3), gameText('en', k, 3)]), def.story, def.hint, ...(def.text ?? []).flatMap((x) => [x.story, x.hint])];
  for (const g of game.filter(Boolean).sort((a, b) => b.length - a.length)) t = t.split(g).join('');
  return t;
}
test('naive: show, status, help and errors say no rule beyond the game texts', () => {
  const said = [run(['help'])];
  for (const n of [1, 2, 4, 12, 22, 29]) {
    said.push(harnessOnly(run(['start', ...T, '--level', String(n)]), n));
    for (const c of word(minBodies(stage(n).level).path)) {
      // Wrong commands first: keep while walking, a move on the choice (both refused, nothing changes).
      try { run(['UDLR'.includes(c) ? 'keep' : 'move', ...T, 'U']); assert.fail('should be refused'); } catch (e) { said.push(harnessOnly(e.message, n)); }
      said.push(harnessOnly('UDLR'.includes(c) ? run(['move', ...T, c]) : run([{ K: 'keep', X: 'switch' }[c], ...T]), n));
      if (last()?.outcome === 'clear' && last().level === n) break;
      said.push(harnessOnly(run(['status', ...T]), n));
    }
    if (!(last()?.outcome === 'clear' && last().level === n)) run(['giveup', ...T]);
  }
  const text = said.join('\n');
  for (const re of RULE_WORDS) assert.ok(!re.test(text), `harness text matches ${re}`);
});

// 2026-11-14 (QA-4, c82d833 content): story scenes once per tester, hints by tries with the popup, clear-screen lines.
test('arrival before a planet\'s first stage and the interlude after its finale, once per tester', () => {
  const first = run(['start', '--tester', 'S', '--level', '1']);
  assert.match(first, /\[이야기 arrival\.moss\]/);
  assert.ok(first.includes(storyPages('arrival.moss', 'ko')[0][0]), 'the arrival page as the game shows it');
  run(['giveup', '--tester', 'S']);
  assert.doesNotMatch(run(['start', '--tester', 'S', '--level', '1']), /arrival\.moss/, 'once');
  run(['giveup', '--tester', 'S']);
  assert.doesNotMatch(run(['start', '--tester', 'S', '--level', '2']), /arrival/, 'not on a later stage');
  run(['giveup', '--tester', 'S']);
  run(['start', '--tester', 'S', '--level', '7']); // moss finale
  const path = exportedPath(7);
  let out = '';
  for (const c of path) out = 'UDLR'.includes(c) ? run(['move', '--tester', 'S', c]) : run([c === 'K' ? 'keep' : 'switch', '--tester', 'S']);
  assert.match(out, /\[이야기 interlude\.moss\]/);
  assert.doesNotMatch(out, /strands: /, 'the strand ribbon was removed from the clear (2026-11-14)');
});

const exportedPath = (n) => JSON.parse(readFileSync(new URL('../Assets/_Project/Data/levels.json', import.meta.url), 'utf8')).levels[n - 1].expected.path;

test('hints: locked until 3 tries, the popup before the first, tiers 1-3 at 3/6/9 tries, the 1-star line', () => {
  const H = ['--tester', 'H'];
  run(['start', ...H, '--level', '4']);
  assert.equal(run(['hint', ...H]), gameText('ko', 'msg.hint_locked', 3));
  run(['move', ...H, 'D']); const r = run(['restart', ...H]);
  assert.doesNotMatch(r, /힌트가 나타났다/);
  assert.equal(run(['hint', ...H]), gameText('ko', 'msg.hint_locked', 2), 'a restart from a played state counts a try');
  run(['move', ...H, 'D']); run(['restart', ...H]);
  run(['move', ...H, 'D']); assert.match(run(['restart', ...H]), new RegExp(gameText('ko', 'msg.hint_ready')));
  const popup = run(['hint', ...H]);
  assert.match(popup, /\[팝업\] 힌트 1\/3/); assert.ok(popup.includes(gameText('ko', 'hint.popup_body')));
  const t1 = run(['hint', ...H, '--confirm']);
  assert.match(t1, /힌트 1\/3/); assert.ok(t1.includes(gameText('ko', 'hint.used_cap')));
  assert.match(run(['hint', ...H]), /힌트 1\/3/, 'only tier 1 is open: the highest again, no popup');
  seedTries('H', 4, 9);
  assert.match(run(['hint', ...H]), /힌트 2\/3[\s\S]*빛나는 칸/);
  assert.match(run(['hint', ...H]), /힌트 3\/3/);
  let out = '';
  for (const c of exportedPath(4)) out = 'UDLR'.includes(c) ? run(['move', ...H, c]) : run([c === 'K' ? 'keep' : 'switch', ...H]);
  assert.match(out, /★☆☆/); assert.ok(out.includes(gameText('ko', 'result.hint_again')));
});
