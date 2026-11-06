// Browser shell: owns the seed source and input, forwards commands to the core.
import { newGame, dispatch, alive } from './core.js';
import { render } from './render.js';

const root = document.getElementById('app');
const params = new URLSearchParams(location.search);
const seedFromUrl = params.has('seed') ? Number(params.get('seed')) : null;
let state = newGame(seedFromUrl ?? (Date.now() >>> 0));

function send(cmd) {
  let events = [];
  if (cmd.type === 'restart') state = newGame(Date.now() >>> 0);
  else events = dispatch(state, cmd);
  render(root, state, events);
}

root.addEventListener('click', (e) => {
  const el = e.target.closest('[data-cmd]');
  if (el && !el.disabled) send(JSON.parse(el.dataset.cmd));
});

const PICK_KEYS = { 1: 3, 2: 7, 3: 8, 4: 13 };
window.addEventListener('keydown', (e) => {
  const k = e.key.toLowerCase();
  if (state.phase === 'pick' && PICK_KEYS[k]) send({ type: 'pick', num: PICK_KEYS[k] });
  else if (state.phase === 'battle') {
    if (k === 'q') send({ type: 'ult' });
    const target = alive(state)[Number(k) - 1];
    if (target) send({ type: 'attack', id: target.id });
  } else if (state.phase === 'altar' && k === 'k') send({ type: 'keep' });
  else if (state.phase === 'end' && k === 'r') send({ type: 'restart' });
});

// Replay: ?seed=42&replay=[{"type":"pick","num":7},...] reproduces an exact state.
if (params.has('replay')) {
  for (const c of JSON.parse(params.get('replay'))) dispatch(state, c);
}

render(root, state, []);

// Debug hook for manual testing in the console.
window.__game = { get state() { return state; }, send };
