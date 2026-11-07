// Browser shell: input, rendering, level select. Reads core state, changes it only through dispatch().
import { STEPS, DIRS, parseLevel, newState, dispatch, tileAt, doorOpen, solve } from './core.js';
import { LEVELS } from './levels.js';
import { initTelemetry, fnv1a } from './telemetry.js';

const root = document.getElementById('app');
const params = new URLSearchParams(location.search);
const SAVE_KEY = 'seven-steps-cleared';
const cleared = new Set(JSON.parse(localStorage.getItem(SAVE_KEY) ?? '[]'));

let levelIdx = Math.min(Math.max(Number(params.get('level') ?? 1) - 1, 0), LEVELS.length - 1);
let state = newState(parseLevel(LEVELS[levelIdx]));
let message = LEVELS[levelIdx].hint;
let flash = null; // { at, type } for one-shot cell animation

// Telemetry hook (docs/telemetry.md). Build = hash of all stages, so a level change shows up as a new build.
const tel = initTelemetry({ build: `proto-${fnv1a(JSON.stringify(LEVELS))}`, params, getState: () => state });

// Replay: ?level=8&replay=LRRRRDDKLLDDRDD reproduces an exact screen (U D L R moves, K keep, X switch).
const LETTER = { U: 'up', D: 'down', L: 'left', R: 'right' };
const CHOICE = { K: 'keep', X: 'switch' };
if (params.has('replay')) {
  for (const ch of params.get('replay').toUpperCase()) {
    if (LETTER[ch]) state = dispatch(state, { type: 'move', dir: LETTER[ch] }).state;
    if (CHOICE[ch]) state = dispatch(state, { type: CHOICE[ch] }).state;
  }
}

const MESSAGES = {
  choose: '행운의 7번째 걸음. 이 몸을 어떻게 할까?',
  stone: 'KEEP. 몸이 석상이 되었다. 다음 몸은 출발점에서 깨어난다.',
  switch: 'SWITCH. 이 자리가 새 출발점이 되었다.',
  bridge: '7번째 걸음으로 구덩이에 몸을 던졌다. 다리가 되었다.',
  seal: '금 간 바닥 위에서 석상이 되었다. 바닥이 영원히 굳었다.',
  fall: '구덩이에 떨어졌다. 7번째 걸음이 아니면 아무것도 남지 않는다.',
  vanish: '출발점에서는 석상이 될 수 없다. 몸 하나를 잃었다.',
  crumble: '금 간 바닥이 무너졌다.',
  win: '7번째 걸음으로 출구에 닿았다!',
  lose: '몸을 모두 잃었다. Z로 되돌리거나 R로 다시 시작.',
  undo: '한 걸음 되돌렸다.',
  restart: '처음부터.',
};

function loadLevel(idx, via = 'select') {
  tel.leave(state, idx + 1);
  levelIdx = idx;
  state = newState(parseLevel(LEVELS[idx]));
  message = LEVELS[idx].hint;
  flash = null;
  history.replaceState(null, '', `?level=${idx + 1}`);
  tel.levelStart(state, idx + 1, LEVELS[idx].name, via);
  render();
}

function send(cmd) {
  const before = state;
  const r = dispatch(state, cmd);
  tel.command(before, cmd, r);
  state = r.state;
  flash = null;
  for (const e of r.events) {
    if (MESSAGES[e.type]) message = MESSAGES[e.type];
    if (e.type === 'bump') flash = { at: e.at, type: 'bump' };
    if (['stone', 'bridge', 'seal', 'fall', 'win', 'choose', 'switch'].includes(e.type)) flash = { at: e.at, type: e.type };
    if (e.type === 'step') message = e.stepIdx === STEPS - 1 ? '다음 걸음이 행운의 7번째 걸음이다.' : '';
  }
  if (state.status === 'win') {
    cleared.add(levelIdx);
    localStorage.setItem(SAVE_KEY, JSON.stringify([...cleared]));
  }
  render();
}

function hint() {
  tel.hint(state);
  // The solver starts from a fresh stage, so the hint is the full route from the beginning.
  const r = solve(state.level);
  const glyph = { up: '↑', down: '↓', left: '←', right: '→', keep: ' KEEP ', switch: ' SWITCH ' };
  message = r ? `처음부터의 최단 풀이: ${r.path.map((d) => glyph[d]).join('')}` : '풀이를 찾지 못했다.';
  render();
}

const CELL = {
  '#': 'wall', '.': 'floor', 'E': 'exit', 'o': 'pit', '~': 'ice', 'x': 'cracked', '=': 'bridge',
};

function cellHtml(i) {
  const s = state;
  const t = tileAt(s, i);
  const nextStep = s.used + 1;
  let cls = CELL[t] ?? 'floor';
  let label = '';
  if (t >= 'a' && t <= 'c') { cls = `plate p-${t}`; label = t.toUpperCase(); }
  if (t >= 'A' && t <= 'C') { cls = `door p-${t.toLowerCase()} ${doorOpen(s, t) ? 'open' : 'closed'}`; label = t; }
  if (t >= '1' && t <= '7') { cls = `gate ${Number(t) === nextStep ? 'go' : ''}`; label = t; }
  if (t === 'E') { label = '出'; if (nextStep === STEPS) cls += ' ready'; }
  if (i === s.start) cls += ' start';
  else if (i === s.level.start) cls += ' old-start';

  const foot = s.trail.filter((f) => f.i === i && f.n > 0).map((f) => f.n);
  const parts = [];
  if (label) parts.push(`<span class="label">${label}</span>`);
  if (foot.length && i !== s.pos) parts.push(`<span class="foot">${foot.join('·')}</span>`);
  if (s.statues.includes(i)) parts.push('<span class="statue">7</span>');
  if (s.bridges.includes(i)) parts.push('<span class="bridge-mark">7</span>');
  if (i === s.pos && s.status !== 'lose') {
    const c = s.status === 'choose' ? 'choosing' : STEPS - s.used === 1 ? 'last' : '';
    parts.push(`<span class="player ${c}">${STEPS - s.used}</span>`);
  }
  const fx = flash && flash.at === i ? ` fx-${flash.type}` : '';
  return `<div class="cell ${cls}${fx}" data-i="${i}">${parts.join('')}</div>`;
}

function render() {
  const s = state;
  const { w, h } = s.level;
  const nextStep = s.used + 1;
  const pips = Array.from({ length: STEPS }, (_, k) => {
    const n = k + 1;
    const c = n <= s.used ? 'used' : n === nextStep && s.status === 'play' ? 'next' : '';
    return `<span class="pip ${c} ${n === STEPS ? 'lucky' : ''}">${n}</span>`;
  }).join('');
  const bodies = Array.from({ length: s.level.bodies }, (_, k) =>
    `<span class="body ${k < s.bodies ? '' : 'gone'}"></span>`).join('');
  const cells = [];
  for (let i = 0; i < w * h; i++) cells.push(cellHtml(i));
  const levelBtns = LEVELS.map((_, k) =>
    `<button class="lv ${k === levelIdx ? 'cur' : ''} ${cleared.has(k) ? 'done' : ''}" data-level="${k}">${k + 1}</button>`).join('');

  let overlay = '';
  if (s.status === 'win') {
    const last = levelIdx === LEVELS.length - 1;
    overlay = `<div class="overlay"><div class="panel"><h2>클리어</h2>
      <p>몸 ${s.level.bodies - s.bodies + 1}개를 썼다.</p>
      ${last ? '<p>모든 스테이지를 끝냈다. 고맙습니다!</p>' : '<button data-act="next">다음 스테이지 (Enter)</button>'}
      <button data-act="restart">다시 하기 (R)</button></div></div>`;
  } else if (s.status === 'lose') {
    overlay = `<div class="overlay"><div class="panel"><h2>몸을 모두 잃었다</h2>
      <button data-act="undo">한 걸음 되돌리기 (Z)</button>
      <button data-act="restart">다시 시작 (R)</button></div></div>`;
  }

  root.innerHTML = `
    <header>
      <h1>일곱 걸음 <small>Seven Steps · 프로토타입</small></h1>
      <nav>${levelBtns}</nav>
    </header>
    <section class="hud">
      <div class="title">${levelIdx + 1}. ${s.level.name}</div>
      <div class="pips">${pips}</div>
      <div class="next">${s.status === 'choose' ? '<b>KEEP</b> 또는 <b>SWITCH</b>' : s.status === 'play' ? (nextStep === STEPS ? '다음 걸음이 <b>행운의 7번째</b>' : `다음 걸음: <b>${nextStep}번째</b>`) : '&nbsp;'}</div>
      <div class="bodies">몸 ${bodies}</div>
    </section>
    <main>
      <div class="board" style="grid-template-columns: repeat(${w}, var(--cell)); grid-template-rows: repeat(${h}, var(--cell));">${cells.join('')}</div>
      ${overlay}
    </main>
    <p class="msg">${message || '&nbsp;'}</p>
    ${s.status === 'choose' ? `<div class="choice">
      <button class="keep" data-act="keep"><b>KEEP</b> (K)<small>여기에 석상을 남긴다<br>다음 몸은 지금 출발점에서</small></button>
      <button class="switch" data-act="switch"><b>SWITCH</b> (X)<small>석상을 남기지 않는다<br>여기가 새 출발점이 된다</small></button>
    </div>` : ''}
    <footer>
      <button data-act="undo">되돌리기 Z</button>
      <button data-act="restart">재시작 R</button>
      <button data-act="hint">풀이 보기 H</button>
      <button data-act="telemetry" title="익명 플레이 기록(어디서 막히는지)을 개발팀 서버로 보낸다">기록 전송 ${tel.enabled ? '켬' : '끔'}</button>
      <span class="keys">이동: 방향키 / WASD / 옆 칸 클릭 · 7걸음 후: K KEEP / X SWITCH · 스테이지: [ ]</span>
    </footer>
    <details class="legend"><summary>타일 설명</summary>
      <ul>
        <li><span class="cell mini plate p-a"><span class="label">A</span></span> 발판: 석상이 올라가 있으면 같은 글자의 문이 열린다</li>
        <li><span class="cell mini door p-a closed"><span class="label">A</span></span> 문</li>
        <li><span class="cell mini gate"><span class="label">3</span></span> 숫자 문: 3번째 걸음일 때만 통과</li>
        <li><span class="cell mini pit"></span> 구덩이: 7번째 걸음으로 빠지면 다리가 된다</li>
        <li><span class="cell mini ice"></span> 얼음: 막힐 때까지 미끄러진다. 몇 칸이든 1걸음</li>
        <li><span class="cell mini cracked"></span> 금 간 바닥: 지나가면 무너진다. 7번째 걸음으로 서면 굳는다</li>
        <li><span class="cell mini exit"><span class="label">出</span></span> 출구: 7번째 걸음으로 밟아야 한다</li>
        <li><span class="cell mini start"></span> 출발점: SWITCH를 고르면 옮겨진다 (점선은 원래 출발점)</li>
      </ul>
    </details>`;
}

const KEYS = {
  ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right',
  w: 'up', s: 'down', a: 'left', d: 'right', W: 'up', S: 'down', A: 'left', D: 'right',
};

window.addEventListener('keydown', (e) => {
  if (KEYS[e.key]) { e.preventDefault(); return send({ type: 'move', dir: KEYS[e.key] }); }
  if (e.key === 'k' || e.key === 'K') return send({ type: 'keep' });
  if (e.key === 'x' || e.key === 'X') return send({ type: 'switch' });
  if (e.key === 'z' || e.key === 'Z' || e.key === 'Backspace') return send({ type: 'undo' });
  if (e.key === 'r' || e.key === 'R') return send({ type: 'restart' });
  if (e.key === 'h' || e.key === 'H') return hint();
  if (e.key === '[' && levelIdx > 0) return loadLevel(levelIdx - 1);
  if (e.key === ']' && levelIdx < LEVELS.length - 1) return loadLevel(levelIdx + 1);
  if (e.key === 'Enter' && state.status === 'win' && levelIdx < LEVELS.length - 1) return loadLevel(levelIdx + 1, 'next');
});

root.addEventListener('click', (e) => {
  const lv = e.target.closest('[data-level]');
  if (lv) return loadLevel(Number(lv.dataset.level));
  const act = e.target.closest('[data-act]')?.dataset.act;
  if (act === 'next') return loadLevel(levelIdx + 1, 'next');
  if (act === 'hint') return hint();
  if (act === 'telemetry') return tel.setEnabled(!tel.enabled);
  if (act) return send({ type: act });
  const cell = e.target.closest('.board .cell');
  if (!cell) return;
  const i = Number(cell.dataset.i);
  const { w } = state.level;
  const dx = (i % w) - (state.pos % w);
  const dy = Math.floor(i / w) - Math.floor(state.pos / w);
  const dir = Object.keys(DIRS).find((d) => DIRS[d][0] === dx && DIRS[d][1] === dy);
  if (dir) send({ type: 'move', dir });
});

render();
tel.sessionStart({ cleared: cleared.size });
tel.levelStart(state, levelIdx + 1, LEVELS[levelIdx].name, params.has('level') ? 'url' : 'boot');
