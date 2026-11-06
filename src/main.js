// Browser shell: owns the seed source, input and UI-only state, forwards commands to the core.
import { newGame, dispatch, alive, availableOps } from './core.js';
import { render } from './render.js';
import { OPS, START_NUMBERS } from './data.js';

const root = document.getElementById('app');
const params = new URLSearchParams(location.search);
let state = newGame(params.has('seed') ? Number(params.get('seed')) : Date.now() >>> 0);
const ui = { op: 'sub' };

// Replay: ?seed=42&replay=[{"type":"pick","num":8},...]&op=sub reproduces an exact screen.
if (params.has('replay')) for (const c of JSON.parse(params.get('replay'))) dispatch(state, c);
if (params.has('op')) ui.op = params.get('op');

function send(cmd) {
  let events = [];
  if (cmd.type === 'restart') state = newGame(Date.now() >>> 0);
  else events = dispatch(state, cmd);
  render(root, state, events, ui);
}

function selectOp(op) {
  if (state.phase !== 'battle' || !availableOps(state.hero).includes(op)) return;
  ui.op = op;
  render(root, state, [], ui);
}

root.addEventListener('click', (e) => {
  const opEl = e.target.closest('[data-ui]');
  if (opEl) return selectOp(opEl.dataset.ui);
  const el = e.target.closest('[data-cmd]');
  if (el && !el.disabled) send(JSON.parse(el.dataset.cmd));
});

const OP_KEYS = Object.fromEntries(Object.entries(OPS).map(([k, o]) => [o.key, k]));
window.addEventListener('keydown', (e) => {
  const k = e.key.toLowerCase();
  const n = Number(k);
  if (state.phase === 'pick' && START_NUMBERS[n - 1]) send({ type: 'pick', num: START_NUMBERS[n - 1] });
  else if (state.phase === 'battle') {
    if (OP_KEYS[k]) selectOp(OP_KEYS[k]);
    const target = alive(state)[n - 1];
    if (target) send({ type: 'act', op: ui.op, id: target.id });
  } else if (state.phase === 'altar' && k === 'k') send({ type: 'keep' });
  else if (state.phase === 'end' && k === 'r') send({ type: 'restart' });
});

render(root, state, [], ui);

// Debug hook for manual testing in the console.
window.__game = { get state() { return state; }, send, ui };
