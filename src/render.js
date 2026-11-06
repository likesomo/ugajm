// DOM renderer. Reads state, never mutates it. Clicks are emitted as data-cmd JSON;
// data-ui marks UI-only clicks (operator selection) handled in main.js.
import { NUMBERS, CULTURES, OPS, ITEMS, START_NUMBERS, TUNING as T } from './data.js';
import {
  alive, preview, forecast, cultureOf, numberStatus, availableOps, actionsPerTurn, fit,
} from './core.js';

const STATUS = { blessed: '축복', cursed: '저주', neutral: '평범' };
const STATUS_EFFECT = { blessed: '매 턴 행동 +1', cursed: '적 공격 주기 −1', neutral: '' };
const RESULT = { death: '💀 소멸', lucky: '✨ 축복', none: '', invalid: '✗ 불가' };
const FAITH_UNLOCKS = ['', '% 나머지 해금', 'Σ 수비학 해금', '매 턴 행동 +1'];
const ENDINGS = {
  dead: ['운이 다했다', '당신의 숫자는 탑 어딘가에 묻혔습니다.'],
  devoted: ['일편단심', '한 번도 흔들리지 않은 믿음. 당신은 숫자의 신이 되었습니다.'],
  wanderer: ['방랑자', '문화마다 다른 행운을 좇아 숫자를 갈아탔습니다.'],
};

const log = [];

const cmd = (o) => `data-cmd='${JSON.stringify(o)}'`;
const bar = (v, max) => `<div class="bar"><i style="width:${Math.max(0, (100 * v) / max)}%"></i><span>${v}/${max}</span></div>`;
const badge = (num, key) => {
  const st = numberStatus(num, key);
  return `<span class="badge ${st}">${CULTURES[key].icon} ${STATUS[st]}</span>`;
};
const opLabel = (op, n) => (OPS[op].unary ? OPS[op].sym : `${OPS[op].sym}${n}`);
const EXPR = { inc: (v) => `${v} + 1`, dec: (v) => `${v} − 1`, digit: (v) => `Σ(${v})` };
const exprText = (op, v, n) => (EXPR[op] ? EXPR[op](v) : `${v} ${OPS[op].sym} ${n}`);
const targetsText = (key) => {
  const c = CULTURES[key];
  return `<span class="t-death">💀 ${c.death.join(' · ')}</span> <span class="t-lucky">✨ ${c.lucky.join(' · ')}</span>`;
};

export function render(root, s, ev, ui) {
  recordLog(s, ev);
  const view = { pick: pickView, battle: battleView, altar: altarView, end: endView }[s.phase];
  root.innerHTML = view(s, ui);
  playFx(root, ev);
}

// ---------- views ----------

function pickView() {
  log.length = 0;
  const cards = START_NUMBERS.map((n, i) => `<button class="pick-card" ${cmd({ type: 'pick', num: n })}>
      <div class="key">${i + 1}</div>
      <div class="big">${n}</div>
      <div class="name">${NUMBERS[n].name}</div>
      <p>${NUMBERS[n].desc}</p>
      <div class="badges">${Object.keys(CULTURES).map((k) => badge(n, k)).join('')}</div>
    </button>`).join('');
  return `<div class="screen pick">
    <h1>숫자 연금술사 <small>LUCKY NUMBER ALCHEMIST</small></h1>
    <div class="howto">
      <div><b>1.</b> 행운의 수를 고르세요. 그 숫자가 당신의 모든 계산에 들어갑니다.</div>
      <div><b>2.</b> 적은 숫자입니다. <b>+ − × ÷</b>로 적 숫자에 내 숫자를 계산해 <b>💀 죽음의 수</b>로 만들면 소멸, <b>✨ 행운의 수</b>로 만들면 축복(회복)입니다.</div>
      <div class="example">동양 사원 (💀 4) · 내 숫자 <b>8</b> · 적 <b>12</b> → <code>12 − 8 = 4</code> 💀</div>
      <div><b>3.</b> 적은 카운트다운이 끝나면 <b>현재 숫자만큼</b> 때립니다. 키우면 위험합니다.</div>
      <div><b>4.</b> 층 사이 제단에서 숫자를 <b>KEEP</b>(신앙 → 새 연산)하거나 <b>SWITCH</b>하세요. 버린 숫자는 돌아옵니다.</div>
    </div>
    <div class="pick-row">${cards}</div>
    <div class="rules">${Object.values(CULTURES).map((c) => `<div><b>${c.icon} ${c.name}</b> ${c.rule} 내 숫자 축복: ${c.blessed.join('·')} / 저주: ${c.cursed.join('·')}</div>`).join('')}</div>
  </div>`;
}

function hud(s) {
  const h = s.hero;
  const atAltar = s.phase === 'altar';
  const key = atAltar ? forecast(s)[0].culture : cultureOf(s);
  const st = numberStatus(h.num, key);
  const fc = forecast(s).map((f) => `<div class="fc">${f.floor}층 ${CULTURES[f.culture].icon} ${targetsText(f.culture)} ${badge(h.num, f.culture)}</div>`).join('');
  return `<div class="hud">
    <div class="floor">${s.floor}<small>/${T.floors}층</small></div>
    <div class="culture ${key}">
      <div>${atAltar ? '다음 층: ' : ''}${CULTURES[key].icon} ${CULTURES[key].name}</div>
      <div class="targets">${targetsText(key)}</div>
      <div class="judge">내 숫자 ${h.num}: <span class="badge ${st}">${STATUS[st]}</span> ${STATUS_EFFECT[st]}</div>
    </div>
    <div class="forecast"><small>예보</small>${fc || '<div class="fc">최종층</div>'}</div>
  </div>`;
}

function enemyCard(s, e, idx, op) {
  if (e.dead) {
    return `<div class="enemy dead ${e.blessed ? 'blessed' : ''}" data-eid="${e.id}">
      <div class="big">${e.num}</div><div class="name">${e.blessed ? '✨ 축복' : '💀 소멸'}</div></div>`;
  }
  const p = op ? preview(s, op, e) : null;
  const pv = p
    ? `<div class="preview ${p.result}">${p.to === null ? RESULT.invalid : `${exprText(op, e.num, s.hero.num)} = <b>${p.to}</b> ${RESULT[p.result]}`}</div>`
    : '<div class="preview"></div>';
  const danger = e.timer === 1 ? 'danger' : '';
  const phases = e.kind === 'boss' ? `<div class="phases">${'●'.repeat(e.phases)}</div>` : '';
  return `<button class="enemy ${e.kind} ${danger}" data-eid="${e.id}" ${op && p.to !== null ? cmd({ type: 'act', op, id: e.id }) : 'disabled'}>
    <div class="key">${idx + 1}</div>
    ${e.name ? `<div class="title">${e.name}</div>` : ''}
    ${phases}
    <div class="big">${e.num}</div>
    <div class="timer ${danger}">${e.timer === 1 ? '⚠ 다음 턴' : `⏳ ${e.timer}턴 후`} <b>${e.num}</b> 피해</div>
    ${pv}
  </button>`;
}

function battleView(s, ui) {
  const h = s.hero;
  const ops = availableOps(h);
  const op = ops.includes(ui.op) ? ui.op : null;
  let i = 0;
  const enemies = s.enemies.map((e) => enemyCard(s, e, e.dead ? -1 : i++, op)).join('');
  const opButtons = Object.keys(OPS).map((k) => {
    const unlocked = ops.includes(k);
    return `<button class="op ${k === op ? 'selected' : ''} ${OPS[k].unary ? 'unary' : ''}" ${unlocked ? `data-ui='${k}'` : 'disabled'}>
      <span class="sym">${opLabel(k, h.num)}</span>
      <span class="okey">${unlocked ? OPS[k].key.toUpperCase() : `신앙 ${OPS[k].faith}`}</span>
      ${OPS[k].name ? `<span class="oname">${OPS[k].name}</span>` : ''}
    </button>`;
  }).join('');
  const perTurn = actionsPerTurn(s);
  return `<div class="screen battle">
    ${hud(s)}
    <div class="enemies">${enemies}</div>
    <div class="bottom">
      <div class="hero ${numberStatus(h.num, cultureOf(s))}" id="hero">
        <div class="big">${h.num}</div>
        <div class="hero-info">
          ${bar(h.hp, h.maxHp)}
          <div class="stats">행동 <b class="pips">${'●'.repeat(h.actions)}${'○'.repeat(Math.max(0, perTurn - h.actions))}</b> · 신앙 <b class="faith">${'★'.repeat(h.faith) || '-'}</b> · 💰${h.gold}</div>
        </div>
      </div>
      <div class="ops">${opButtons}</div>
      <div class="hint">${op ? '적을 클릭(숫자키)해서 적용' : '연산자를 먼저 고르세요 (Q W E R · Z X)'}</div>
    </div>
    <div class="log">${log.slice(-6).map((l) => `<div>${l}</div>`).join('')}</div>
  </div>`;
}

function altarView(s) {
  const h = s.hero;
  const [next, after] = forecast(s);
  const fitLine = (n) => `다음 층 적합도 <b>${fit(n, next.culture)}</b>`;
  const doors = s.altar.options.map((n) => `<button class="door" ${cmd({ type: 'switch', num: n })}>
      <div class="big">${n}</div>
      <div class="name">${NUMBERS[n].name}</div>
      <div>다음 ${badge(n, next.culture)}${after ? ` 그다음 ${badge(n, after.culture)}` : ''}</div>
      <div>${fitLine(n)}</div>
    </button>`).join('');
  const betray = h.faith >= 1
    ? `<div class="warn">⚠ 바꾸면 신앙 ${h.faith} → ${Math.floor(h.faith / 2)}, 다음 층에 <b>배신당한 ${h.num}</b>(숫자 ${h.num * h.faith}) 등장</div>`
    : '<div class="ok">신앙이 없어 배신 없음</div>';
  const shop = Object.entries(ITEMS).map(([k, it]) => {
    const bought = s.altar.bought.includes(k);
    const can = !bought && h.gold >= it.price;
    return `<button class="item" ${can ? cmd({ type: 'buy', item: k }) : 'disabled'}><b>${it.name}</b> ${it.desc} · 💰${it.price}${bought ? ' ✔' : ''}</button>`;
  }).join('');
  return `<div class="screen altar">
    ${hud(s)}
    <h2>⛩ 제단 <small>${s.floor}층 돌파 · HP ${h.hp}/${h.maxHp} · 💰${h.gold}</small></h2>
    <div class="choice">
      <button class="keep" ${cmd({ type: 'keep' })}>
        <div class="label">[K] KEEP <b>${h.num}</b></div>
        <div>신앙 ${h.faith} → <b>${h.faith + 1}</b> ${FAITH_UNLOCKS[h.faith + 1] ? `<span class="ok">${FAITH_UNLOCKS[h.faith + 1]}</span>` : ''}</div>
        <div>다음 ${badge(h.num, next.culture)}${after ? ` 그다음 ${badge(h.num, after.culture)}` : ''}</div>
        <div>${fitLine(h.num)}</div>
      </button>
      <div class="switch">
        <div class="label">SWITCH <small>적합도: 낮을수록 그 층에서 유리</small></div>
        ${betray}
        <div class="doors">${doors}</div>
      </div>
    </div>
    <div class="shop"><small>상점</small>${shop}</div>
  </div>`;
}

function endView(s) {
  const [title, text] = ENDINGS[s.ending];
  const h = s.hero;
  const line = s.timeline.map((n, i) => (i && n !== s.timeline[i - 1] ? `<i>⇄</i><b>${n}</b>` : `<b>${n}</b>`)).join('━');
  return `<div class="screen end ${s.ending}">
    <h1>${title}</h1>
    <p>${text}</p>
    <div class="timeline">${line || '-'}${s.ending === 'dead' ? `━<b class="x">✝${h.num}</b>` : ''}</div>
    <div class="stats">도달 ${s.floor}층 · 숫자 변경 ${h.switches}회 · 시드 ${s.seed}</div>
    <button class="restart" ${cmd({ type: 'restart' })}>[R] 다시 고르기</button>
  </div>`;
}

// ---------- log & fx ----------

function recordLog(s, ev) {
  for (const e of ev) {
    if (e.t === 'floor') log.push(`<b>${e.floor}층</b> ${CULTURES[e.culture].icon} ${CULTURES[e.culture].name}`);
    if (e.t === 'op') log.push(`${exprText(e.op, e.from, e.n)} = <b>${e.to}</b>`);
    if (e.t === 'kill') log.push('<em>💀 소멸</em>');
    if (e.t === 'bless') log.push(`<em>✨ 축복</em> HP +${e.heal}`);
    if (e.t === 'bossPhase') log.push(`<em>운명이 다시 섞입니다</em> → ${e.num}`);
    if (e.t === 'attack') log.push(`<span class="hurt">적 공격 −${e.dmg}</span>`);
    if (e.t === 'invalid') log.push('계산할 수 없습니다');
  }
  if (log.length > 40) log.splice(0, log.length - 40);
}

function playFx(root, ev) {
  let hurt = 0;
  for (const e of ev) {
    const el = root.querySelector(`[data-eid="${e.id}"]`);
    if (e.t === 'op') floatText(el, `→ ${e.to}`, 'op');
    if (e.t === 'kill') floatText(el, '💀', 'kill');
    if (e.t === 'bless') floatText(el, `✨+${e.heal}`, 'bless');
    if (e.t === 'bossPhase') floatText(el, '🔀', 'kill');
    if (e.t === 'attack') { hurt += e.dmg; el?.classList.add('lunge'); }
  }
  const hero = root.querySelector('#hero');
  if (hurt && hero) {
    floatText(hero, `-${hurt}`, 'hurt');
    hero.classList.add('shake');
  }
}

function floatText(el, text, cls) {
  if (!el) return;
  const f = document.createElement('div');
  f.className = `float ${cls}`;
  f.textContent = text;
  el.appendChild(f);
  setTimeout(() => f.remove(), 900);
}
