// DOM renderer. Reads state, never mutates it. Clicks are emitted as data-cmd JSON.
import { NUMBERS, CULTURES, ITEMS, TUNING as T } from './data.js';
import {
  NUMBER_IDS, alive, relation, forecast, cultureOf, numberStatus, heroAtk, maxHpOf, itemPrice, critMult,
} from './core.js';

const TAG = { crit: '치명타', jackpot: '잭팟', combo: '연쇄', sieve: '체', ult: '궁극기', ally: '동료' };
const STATUS = { blessed: '축복', cursed: '저주', neutral: '평범' };
const ENDINGS = {
  dead: ['운이 다했다', '당신의 숫자는 탑 어딘가에 묻혔습니다.'],
  devoted: ['일편단심', '한 번도 흔들리지 않은 믿음. 당신은 숫자의 신이 되었습니다.'],
  cursed: ['저주를 이긴 자', '모두가 피하는 13을 끝까지 지켰습니다. 수학은 처음부터 당신 편이었습니다.'],
  wanderer: ['방랑자', '문화마다 다른 행운을 좇아 숫자를 갈아탔습니다. 모든 축복을 조금씩 받았습니다.'],
};

const log = [];

const cmd = (o) => `data-cmd='${JSON.stringify(o)}'`;
const esc = (t) => String(t).replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]);
const bar = (v, max, cls = '') => `<div class="bar ${cls}"><i style="width:${Math.max(0, (100 * v) / max)}%"></i><span>${v}/${max}</span></div>`;
const badge = (num, key) => {
  const st = numberStatus(num, key);
  return `<span class="badge ${st}">${CULTURES[key].icon} ${STATUS[st]}</span>`;
};

export function render(root, s, ev) {
  recordLog(s, ev);
  const view = { pick: pickView, battle: battleView, altar: altarView, end: endView }[s.phase];
  root.innerHTML = view(s);
  playFx(root, ev);
}

// ---------- views ----------

function pickView() {
  log.length = 0;
  const cards = NUMBER_IDS.map((n, i) => {
    const d = NUMBERS[n];
    return `<button class="pick-card" ${cmd({ type: 'pick', num: n })}>
      <div class="key">${i + 1}</div>
      <div class="big">${n}</div>
      <div class="name">${d.name}</div>
      <div class="stats">HP ${d.hp} · 공격 ${d.atk} · 치명타 ×${(1 + n / T.critDiv).toFixed(1)}</div>
      <p>${d.desc}</p>
      <div class="ult">궁극기 <b>${d.ult}</b>: ${d.ultDesc}</div>
      <div class="badges">${Object.keys(CULTURES).map((k) => badge(n, k)).join('')}</div>
    </button>`;
  }).join('');
  return `<div class="screen pick">
    <h1>13 FLOORS <small>행운의 수</small></h1>
    <p class="lead">당신의 <b>행운의 수</b>를 고르세요. 그 숫자가 직업이자 세계의 규칙이 됩니다.<br>
    층마다 <b>KEEP</b>(믿음을 쌓음) 또는 <b>SWITCH</b>(숫자를 갈아탐)를 결정하세요. 버린 숫자는 돌아옵니다.</p>
    <div class="pick-row">${cards}</div>
    <div class="rules">${Object.values(CULTURES).map((c) => `<div><b>${c.icon} ${c.name}</b> ${c.rule}</div>`).join('')}</div>
  </div>`;
}

function hud(s) {
  const h = s.hero;
  // At the altar the banner previews the floor you are about to enter.
  const atAltar = s.phase === 'altar';
  const key = atAltar ? forecast(s)[0].culture : cultureOf(s);
  const fc = forecast(s).map((f) => `<div class="fc">${f.floor}층 ${badge(h.num, f.culture)}</div>`).join('');
  return `<div class="hud">
    <div class="floor">${s.floor}<small>/${T.floors}층</small></div>
    <div class="culture ${key}">${atAltar ? '다음 층: ' : ''}${CULTURES[key].icon} ${CULTURES[key].name} <span>${CULTURES[key].rule}</span></div>
    <div class="forecast"><small>예보</small>${fc || '<div class="fc">최종층</div>'}</div>
  </div>`;
}

function heroPanel(s) {
  const h = s.hero;
  const st = s.phase === 'battle' ? numberStatus(h.num, cultureOf(s)) : 'neutral';
  const extras = [];
  if (h.revives) extras.push(`부활 ${h.revives}`);
  if (h.num === 13) extras.push(`체 ${h.counter13}/${T.sieveCount}`);
  if (h.imprints.length) extras.push(`각인 ${h.imprints.map((n) => `<b title="${NUMBERS[n].imprintText}">${n}</b>`).join(' ')}`);
  const allies = s.allies.map((a) => `<span class="ally">${a.num}<small>공${a.atk}</small></span>`).join('');
  return `<div class="hero ${st}" id="hero">
    <div class="big">${h.num}</div>
    <div class="hero-info">
      <div class="name">${NUMBERS[h.num].name} <span class="badge ${st}">${STATUS[st]}</span></div>
      ${bar(h.hp, maxHpOf(h), 'hp')}
      <div class="stats">공격 ${Math.round(heroAtk(s))} · 신앙 <b class="faith">${'★'.repeat(h.faith)}${'☆'.repeat(Math.max(0, T.ultFaith - h.faith))}</b> · 💰${h.gold}</div>
      <div class="stats">${extras.join(' · ')}</div>
      ${allies ? `<div class="allies">동료 ${allies}</div>` : ''}
    </div>
  </div>`;
}

function enemyCard(s, e, idx) {
  if (e.dead) return `<div class="enemy dead" data-eid="${e.id}"><div class="big">${e.num}</div><div class="name">${e.recruited ? '동료가 됨' : '처치'}</div></div>`;
  const r = relation(s, e);
  const tags = [];
  if (r.recruit) tags.push('<span class="tag recruit">영입</span>');
  if (r.crit) tags.push(`<span class="tag crit">치명타 ×${critMult(s.hero).toFixed(1)}</span>`);
  if (r.combo) tags.push('<span class="tag combo">연쇄</span>');
  if (r.fear) tags.push('<span class="tag fear">겁먹음</span>');
  return `<button class="enemy ${e.kind}" data-eid="${e.id}" ${cmd({ type: 'attack', id: e.id })}>
    <div class="key">${idx + 1}</div>
    ${e.name ? `<div class="title">${esc(e.name)}</div>` : ''}
    <div class="big">${e.num}</div>
    ${bar(e.hp, e.maxHp)}
    <div class="stats">공격 ${e.atk}</div>
    <div class="tags">${tags.join('')}</div>
  </button>`;
}

function battleView(s) {
  const h = s.hero;
  let i = 0;
  const enemies = s.enemies.map((e) => enemyCard(s, e, e.dead ? -1 : i++)).join('');
  const ultReady = h.ultCharge > 0;
  const ultLabel = h.faith >= T.ultFaith
    ? `${NUMBERS[h.num].ult} ${ultReady ? '' : '(사용함)'}`
    : `${NUMBERS[h.num].ult} (신앙 ${T.ultFaith} 필요)`;
  return `<div class="screen battle">
    ${hud(s)}
    <div class="enemies">${enemies}</div>
    <div class="bottom">
      ${heroPanel(s)}
      <div class="actions">
        <button class="ult-btn" ${ultReady ? cmd({ type: 'ult' }) : 'disabled'}>[Q] ${ultLabel}</button>
        <div class="hint">적을 클릭(또는 숫자키)해 공격 · 같은 숫자는 영입</div>
      </div>
      <div class="log">${log.slice(-7).map((l) => `<div>${l}</div>`).join('')}</div>
    </div>
  </div>`;
}

function altarView(s) {
  const h = s.hero;
  const [next, after] = forecast(s);
  const nextKey = next.culture;
  const willUlt = h.faith + 1 >= T.ultFaith;
  const keepWarn = numberStatus(h.num, nextKey) === 'cursed'
    ? `<div class="warn">⚠ ${CULTURES[nextKey].name}은 ${h.num}을 저주합니다. 받는 피해 ×${T.curseMult}, 공격 ×${T.curseAtkMult}, 적 공격 +${Math.ceil((h.faith + 1) / 2)}</div>`
    : '';
  const doors = s.altar.options.map((n) => {
    const betray = h.faith >= T.betrayMinFaith
      ? `<div class="warn">⚠ 배신당한 ${h.num}(신앙 ${h.faith})이 다음 층에 나타남</div>` : '<div class="ok">신앙이 낮아 배신 없음</div>';
    return `<button class="door" ${cmd({ type: 'switch', num: n })}>
      <div class="big">${n}</div>
      <div class="name">${NUMBERS[n].name}</div>
      <div>다음 층 ${badge(n, nextKey)}${after ? ` 그다음 ${badge(n, after.culture)}` : ''}</div>
      <div class="ok">각인 획득: ${NUMBERS[h.num].imprintText}</div>
      ${betray}
    </button>`;
  }).join('');
  const shop = Object.entries(ITEMS).map(([k, it]) => {
    const price = itemPrice(s, k);
    const sale = price < it.price + T.priceStep * Math.floor((s.floor - 1) / 3);
    const bought = s.altar.bought.includes(k);
    const can = !bought && h.gold >= price;
    return `<button class="item" ${can ? cmd({ type: 'buy', item: k }) : 'disabled'}>
      <b>${it.name}</b> ${it.desc}<br><span class="${sale ? 'sale' : ''}">💰${price}${sale ? ` (끝자리 ${h.num % 10} 할인)` : ''}</span>${bought ? ' ✔' : ''}
    </button>`;
  }).join('');
  return `<div class="screen altar">
    ${hud(s)}
    <h2>⛩ 제단 <small>${s.floor}층 돌파</small></h2>
    ${heroPanel(s)}
    <div class="choice">
      <button class="keep" ${cmd({ type: 'keep' })}>
        <div class="label">[K] KEEP <b>${h.num}</b></div>
        <div>신앙 ${h.faith} → <b>${h.faith + 1}</b> (공격 +${T.faithAtk})</div>
        <div>세상에 ${h.num}의 배수가 더 많이 나타남</div>
        ${willUlt ? `<div class="ok">궁극기 「${NUMBERS[h.num].ult}」 사용 가능</div>` : ''}
        <div>다음 층 ${badge(h.num, nextKey)}</div>
        ${keepWarn}
      </button>
      <div class="switch">
        <div class="label">SWITCH: 신앙 초기화, 새 숫자로</div>
        <div class="doors">${doors}</div>
      </div>
    </div>
    <div class="shop"><small>상점 (가격 끝자리가 내 숫자면 반값)</small>${shop}</div>
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
    <div class="stats">도달 ${s.floor}층 · 숫자 변경 ${h.switches}회 · 골드 ${h.gold} · 시드 ${s.seed}</div>
    <button class="restart" ${cmd({ type: 'restart' })}>[R] 다시 고르기</button>
  </div>`;
}

// ---------- log & fx ----------

function recordLog(s, ev) {
  for (const e of ev) {
    if (e.t === 'floor') log.push(`<b>${e.floor}층</b> ${CULTURES[e.culture].icon} ${CULTURES[e.culture].name}`);
    if (e.t === 'hit' && !e.tags.includes('ally')) log.push(`${e.tags.map((t) => `<em>${TAG[t]}</em>`).join('')} ${e.dmg} 피해`);
    if (e.t === 'kill') log.push(`처치! 💰+${e.gold}`);
    if (e.t === 'recruit') log.push(`<em>영입</em> ${e.num}이(가) 동료가 됨`);
    if (e.t === 'revive') log.push('<em>삼세판</em> 부활!');
    if (e.t === 'ult') log.push(`<em>궁극기</em> ${NUMBERS[e.num].ult}!`);
    if (e.t === 'keep') log.push(`KEEP ${e.num} · 신앙 ${e.faith}`);
    if (e.t === 'switch') log.push(`SWITCH ${e.from} → ${e.to}`);
  }
  if (log.length > 40) log.splice(0, log.length - 40);
}

function playFx(root, ev) {
  let hurt = 0;
  for (const e of ev) {
    if (e.t === 'hit') floatText(root.querySelector(`[data-eid="${e.id}"]`), `-${e.dmg}`, e.tags[0]);
    if (e.t === 'hurt') hurt += e.dmg;
  }
  const hero = root.querySelector('#hero');
  if (hurt && hero) {
    floatText(hero, `-${hurt}`, 'hurt');
    hero.classList.add('shake');
  }
}

function floatText(el, text, cls = '') {
  if (!el) return;
  const f = document.createElement('div');
  f.className = `float ${cls || ''}`;
  f.textContent = text;
  f.style.left = `${30 + Math.random() * 40}%`;
  el.appendChild(f);
  setTimeout(() => f.remove(), 900);
}
