'use strict';
/* ===== Интерфейс Kingdom Crash ===== */
const $ = id => document.getElementById(id);
const UI = { S: null, bpage: 0, tab: 'map', sheet: null, sheetMin: false, speed: 1, paused: false, toasts: [], lastHtml: '', inboxTab: 'all', inboxNote: '', modal: null, shownOver: false, hud: {}, achT: 0, seenMsg: undefined, planWas: undefined, fxT: 0, mmN: 0,
  create: { type: 'continent', size: 'm', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, sandbox: 0, free: 1, events: 1 } };
const me = () => UI.S.players[0];
const fmt = n => n >= 1e6 ? (n / 1e6).toFixed(1) + 'M' : n >= 1e4 ? Math.round(n / 1e3) + 'K' : String(Math.floor(n));
const pad2 = n => (n < 10 ? '0' : '') + n;
const gameClock = t => { const hh = (8 + (t / DAY) * 24) % 24, h = Math.floor(hh); return 'Д' + (Math.floor(t / DAY) + 1) + ' ' + pad2(h) + ':' + pad2(Math.floor((hh - h) * 60)); };
// всё, что приходит из данных партии (названия, письма), в разметку попадает только через esc
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const btn = (act, label, cls) => `<button class="px ${cls || ''}" data-act="${act}">${label}</button>`;
const sw = col => `<span class="sw" style="background:${/^#[0-9a-fA-F]{6}$/.test(col) ? col : '#999999'}"></span>`;
const prodSpeed = p => p.mult * (perk(p, 'craft') ? 1.3 : 1), trainSpeed = p => p.mult * (perk(p, 'drill') ? 1.25 : 1);
const secs = v => Math.max(0, Math.ceil(v));
function uiError(e) { try { console.error(e); } catch (x) { /* консоли может не быть */ } if (UI.S) UI.S.toast.push({ title: 'Сбой', text: 'Действие не выполнено', t: UI.S.time, bad: true }); }
const STAR = () => IMG('star', 16), INF = () => `<img class="ic" src="${ICONURL.inf || ''}" style="width:22px;height:16px" alt="">`;

/* Обновление разметки «на месте»: меняются только отличающиеся узлы. Кнопка под пальцем не пересоздаётся,
   поэтому нажатия не теряются, а прокрутка списков не сбрасывается (раньше панели перерисовывались целиком). */
function morph(el, html) { const t = document.createElement('template'); t.innerHTML = html; patchKids(el, t.content); }
function patchKids(a, b) {
  let x = a.firstChild, y = b.firstChild;
  while (y) {
    const ny = y.nextSibling;
    if (!x) a.appendChild(y);
    else if (x.nodeType !== y.nodeType || x.nodeName !== y.nodeName) { const nx = x.nextSibling; a.replaceChild(y, x); x = nx; }
    else {
      if (x.nodeType === 1) {
        const xa = x.attributes, ya = y.attributes;
        for (let i = xa.length - 1; i >= 0; i--) if (!y.hasAttribute(xa[i].name)) x.removeAttribute(xa[i].name);
        for (let i = 0; i < ya.length; i++) if (x.getAttribute(ya[i].name) !== ya[i].value) x.setAttribute(ya[i].name, ya[i].value);
        patchKids(x, y);
      } else if (x.nodeValue !== y.nodeValue) x.nodeValue = y.nodeValue;
      x = x.nextSibling;
    }
    y = ny;
  }
  while (x) { const nx = x.nextSibling; a.removeChild(x); x = nx; }
}

/* ----- настройки, награды, рекорды (хранятся на устройстве) ----- */
const store = {
  get(k) { try { return JSON.parse(localStorage.getItem(k)); } catch (e) { return null; } },
  set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); return true; } catch (e) { return false; } }
};
const SET = { sfx: 1, music: 1, hints: 1, confirm: 1 };
{ const s = store.get('kc_set'); if (s && typeof s === 'object') for (const k in SET) if (s[k] === 0 || s[k] === 1) SET[k] = s[k]; }
function applySettings() { SND.set('sfx', SET.sfx); SND.set('music', SET.music); store.set('kc_set', SET); }
const cnt = (S, t) => { let n = 0; for (const b of S.buildings) if (b.owner === 0 && b.type === t) n++; return n; };
const myCities = S => S.cities.filter(c => c.owner === 0);
const ACH = [
  ['first', 'Первый камень', 'Построить первое здание', S => S.buildings.some(b => b.owner === 0)],
  ['farmer', 'Кормилец', 'Иметь 5 ферм', S => cnt(S, 'farm') >= 5],
  ['miller', 'Мельник', 'Поставить мельницу рядом с фермой', S => S.buildings.some(b => b.owner === 0 && b.type === 'farm' && millNear(S, b))],
  ['arms', 'К оружию!', 'Обучить первый отряд', (S, p) => !!p.flags.squad],
  ['smith', 'Оружейная палата', 'Накопить по 20 мечей и луков', (S, p) => p.stock.sword >= 20 && p.stock.bow >= 20],
  ['general', 'Полководец', 'Обучить полководца', S => S.armies.some(a => a.owner === 0 && a.sq.some(q => q.t === 'cmd'))],
  ['host', 'Большое войско', 'Собрать армию из 8 отрядов', S => S.armies.some(a => a.owner === 0 && a.sq.length >= MAX_SQ_ARMY)],
  ['city3', 'Градостроитель', 'Владеть тремя городами', S => myCities(S).length >= 3],
  ['city6', 'Империя', 'Владеть шестью городами', S => myCities(S).length >= 6],
  ['lvl5', 'Столичный блеск', 'Улучшить город до 5 уровня', S => S.cities.some(c => c.owner === 0 && c.level >= 5)],
  ['free', 'Под крыло', 'Захватить вольный город', (S, p) => !!p.flags.freecap],
  ['conq', 'Завоеватель', 'Захватить три города', (S, p) => p.captured >= 3],
  ['capital', 'Покоритель столиц', 'Захватить чужую столицу', (S, p) => !!p.flags.capcap],
  ['trade', 'Купец', 'Заключить торговое соглашение', (S, p) => !!p.flags.trade],
  ['ally', 'Верный союзник', 'Заключить союз', (S, p) => !!p.flags.ally],
  ['navy', 'Адмирал', 'Построить военный корабль', (S, p) => !!p.flags.warship],
  ['sea', 'Морской путь', 'Получить доход от торгового судна', (S, p) => !!p.flags.shipTrade],
  ['merc', 'Звонкая монета', 'Нанять наёмников в таверне', (S, p) => !!p.flags.merc],
  ['fort', 'Неприступная крепость', 'Построить 20 стен и башню', S => cnt(S, 'wall') >= 20 && cnt(S, 'tower') >= 1],
  ['rich', 'Полная казна', 'Накопить 5000 золота', (S, p) => p.stock.gold >= 5000],
  ['kills', 'Гроза врагов', 'Сразить 500 воинов', (S, p) => p.kills >= 500],
  ['day30', 'Долгое правление', 'Дожить до 30-го дня', S => S.time >= 29 * DAY],
  ['win', 'Корона', 'Победить в партии', S => !!S.over && S.over.winner === 0],
  ['winhard', 'Железный король', 'Победить сложных соперников', S => !!S.over && S.over.winner === 0 && S.opts.diff === 'hard'],
  ['wincaps', 'Собиратель земель', 'Победить, захватив все столицы', S => !!S.over && S.over.winner === 0 && S.opts.win === 'caps' && S.cities.filter(c => c.isCap).every(c => c.owner === 0)]
];
const GOT = {};
{ const a = store.get('kc_ach'); if (a && typeof a === 'object') for (const q of ACH) if (a[q[0]]) GOT[q[0]] = 1; }
function checkAch() { // награды в песочнице не выдаются
  const S = UI.S; if (!S || S.opts.sandbox) return; const p = me(); let any = false;
  for (const q of ACH) {
    if (GOT[q[0]]) continue; let ok = false; try { ok = !!q[3](S, p); } catch (e) { ok = false; }
    if (ok) { GOT[q[0]] = 1; any = true; UI.toasts.push({ t: { title: 'Награда: ' + q[1], text: q[2], good: true, icon: 'star' }, rt: performance.now(), dur: 5000 }); }
  }
  if (any) { store.set('kc_ach', GOT); SND.play('ach'); }
}
function loadRecords() {
  const r = store.get('kc_rec'), out = []; if (!Array.isArray(r)) return out;
  for (const q of r.slice(0, 10)) if (q && typeof q === 'object') out.push({ score: Math.max(0, Math.floor(+q.score) || 0), ci: Math.max(0, Math.min(PNAMES.length - 1, q.ci | 0)), days: Math.max(1, q.days | 0), win: !!q.win, diff: DIFF[q.diff] || q.diff === 'mixed' ? q.diff : 'normal', at: +q.at || 0 });
  return out;
}
function addRecord(S) {
  if (S.opts.sandbox) return false; const p = S.players[0], r = loadRecords(), rec = { score: p.score, ci: p.ci, days: Math.floor(S.time / DAY) + 1, win: S.over.winner === 0, diff: S.opts.diff, at: Date.now() };
  r.push(rec); r.sort((a, b) => b.score - a.score); const top = r.slice(0, 10); store.set('kc_rec', top); return top[0] === rec;
}

/* ----- нижняя панель ----- */
const TABS = [['map', 'КАРТА', 't_map'], ['city', 'ГОРОДА', 't_city'], ['build', 'СТРОЙКА', 't_build'], ['army', 'АРМИЯ', 't_army'], ['pol', 'ПОЛИТ.', 't_pol'], ['fleet', 'ФЛОТ', 't_fleet']];
const SHORT = { academy: 'АКАДЕМИЯ', siege: 'ОСАДНАЯ', wall: 'СТЕНА', tower: 'БАШНЯ', gate: 'ВОРОТА', moat: 'РОВ', hospital: 'ГОСПИТ.', farm: 'ФЕРМА', sawmill: 'ЛЕСОПИЛ.', mine: 'РУДНИК', barracks: 'КАЗАРМЫ', forge: 'КУЗНЯ', workshop: 'МАСТЕРСК.', stable: 'КОНЮШНЯ', market: 'РЫНОК', port: 'ПОРТ', mill: 'МЕЛЬНИЦА', tavern: 'ТАВЕРНА', prison: 'ТЮРЬМА' };
const BUILD_PAGES = [['farm', 'sawmill', 'mine', 'mill', 'barracks', 'hospital', 'forge', 'workshop', 'stable'], ['wall', 'tower', 'gate', 'moat', 'siege', 'market', 'port', 'academy', 'tavern'], ['prison']];
const BPAGE_LBL = ['ПРОИЗВ.', 'УКРЕПЛ.', 'ОБЩЕЕ'], BPAGE_ICO = ['B:farm', 'B:tower', 'B:prison'];
const BAR = {
  map: [['center', 'ЦЕНТР', 'home'], ['mm', 'КАРТА', 't_map'], ['folk', 'ЖИТЕЛИ', 't_folk'], ['borders', 'ГРАНИЦЫ', 'flag'], ['stock', 'СКЛАД', 'iron'], ['save', 'СОХРАН.', 'scroll'], ['plan', 'ПЛАН', 'check'], ['rate', 'РЕЙТИНГ', 'trophy'], ['ach', 'НАГРАДЫ', 'star'], ['menu', 'МЕНЮ', 'menu']],
  city: [['cities', 'ГОРОДА', 't_city'], ['place:city', 'НОВЫЙ', 'flag'], ['upg', 'УЛУЧШ.', 't_build'], ['center', 'СТОЛИЦА', 'home']],
  build: [],
  army: [['attack', 'НАПАСТЬ', 'skull'], ['barr', 'КАЗАРМЫ', 'B:barracks'], ['armies', 'АРМИИ', 't_army'], ['merge', 'ОБЪЕД.', 'merge'], ['split', 'РАЗДЕЛ.', 'split'], ['stop', 'СТОЯТЬ', 'stop'], ['retreat', 'ОТСТУП.', 'home'], ['taverns', 'ТАВЕРНЫ', 'mug'], ['stock', 'СКЛАД', 'armH']],
  pol: [['nations', 'ДЕРЖАВЫ', 'globe'], ['polsan', 'САНКЦИИ', 't_san'], ['dip', 'ДИПЛОМАТ.', 'flag'], ['trade', 'ТОРГОВЛЯ', 'cart'], ['eco', 'ЭКОНОМИКА', 'chart'], ['ult', 'УЛЬТИМАТ.', 'scroll'], ['attack', 'ВОЙНА', 'skull'], ['rate', 'РЕЙТИНГ', 'trophy']],
  fleet: [['ports', 'ПОРТЫ', 'B:port'], ['ships', 'КОРАБЛИ', 'ship'], ['stopship', 'СТОЯТЬ', 'stop'], ['unload', 'ВЫСАДКА', 'flag']]
};
const bIcon = {};
function bldIcon(type, size) {
  if (!BD[type]) return '';
  if (!bIcon[type]) { const c = document.createElement('canvas'); c.width = 32; c.height = 32; const x = c.getContext('2d'); if (type === 'moat') drawMoat(x, 0, 0, 5, true); else { DRAW[type](x, 0, 0, '#d93a3a', 1); if (type === 'mill') millBlades(x, 16, 9, 0.5, 1); } bIcon[type] = c.toDataURL(); }
  return `<img class="ic" src="${bIcon[type]}" style="width:${size}px;height:${size}px" alt="">`;
}
function icoHtml(name, s) { return name.indexOf('B:') === 0 ? bldIcon(name.slice(2), s) : IMG(name, s); }
function barOn(act) { // подсветка включённых режимов
  if (act === 'mm') return !$('mm').classList.contains('hidden'); if (act === 'borders') return RN.showTerr;
  return !!RN.place && act === 'place:' + RN.place.type;
}
function renderTabs() {
  morph($('tabs'), TABS.map(t => `<button class="${UI.tab === t[0] ? 'on' : ''}" data-act="tab:${t[0]}">${IMG(t[2], 24)}<span>${t[1]}</span></button>`).join(''));
  const bs = t => ['place:' + t, SHORT[t], 'B:' + t]; let sl;
  if (UI.tab === 'build') { const nx = (UI.bpage + 1) % 3; sl = BUILD_PAGES[UI.bpage % 3].map(bs).concat([['bpage:' + nx, BPAGE_LBL[nx], BPAGE_ICO[nx]]]); }
  else sl = (BAR[UI.tab] || []).slice();
  while (sl.length < 10) sl.push(null);
  const fixed = [`<button data-act="pause" class="${UI.paused ? 'on' : ''}"><div class="bx fix">${IMG('pause', 32)}</div>ПАУЗА</button>`,
    `<button data-act="speed" class=""><div class="bx fix">${IMG('hour', 32)}<b class="spd">x${UI.speed}</b></div>СКОРОСТЬ</button>`];
  const cells = []; // колонка 0: пауза и скорость, дальше 5 колонок по 2 ряда
  for (let r = 0; r < 2; r++) { cells.push(fixed[r]); for (let c = 0; c < 5; c++) { const s = sl[r * 5 + c]; cells.push(s ? `<button data-act="${s[0]}" class="${barOn(s[0]) ? 'on' : ''}"><div class="bx">${icoHtml(s[2], 32)}</div>${s[1]}</button>` : '<div></div>'); } }
  morph($('bar'), `<div id="barIn">${cells.join('')}</div>`);
}

/* ----- HUD: в разметку пишем только то, что изменилось ----- */
function setText(id, v) { if (UI.hud[id] !== v) { UI.hud[id] = v; $(id).textContent = v; } }
function setHtml(id, v) { if (UI.hud[id] !== v) { UI.hud[id] = v; morph($(id), v); } }
function setCls(id, cls, on) { const k = id + '.' + cls; if (UI.hud[k] !== on) { UI.hud[k] = on; $(id).classList.toggle(cls, on); } }
function updateHud() {
  const S = UI.S, p = me(), now = performance.now();
  if (UI.hud.col !== p.col) { UI.hud.col = p.col; $('flagc').style.background = p.col; }
  setText('nation', p.name); setText('scoreN', String(p.score)); setText('clock', gameClock(S.time));
  const sb = !!S.opts.sandbox; let h = '<div class="g4">';
  for (const k of ['gold', 'wood', 'food', 'iron']) { const v = p.rate[k]; h += `<div class="r">${IMG(k, 18)}<span class="${!sb && k === 'food' && p.stock[k] < 1 ? 'neg' : ''}">${sb ? INF() : fmt(p.stock[k])}</span>${sb ? '' : `<span class="rt ${v < 0 ? 'neg' : 'pos'}">${v >= 0 ? '+' : ''}${v.toFixed(1)}</span>`}</div>`; }
  h += '</div><div class="g6">'; for (const k of ITEM_KEYS) h += `<div class="r">${IMG(k, 18)}${sb ? INF() : Math.floor(p.stock[k])}</div>`;
  setHtml('resbar', h + '</div>');
  const un = S.msgs.filter(m => !m.read).length; setText('badge', String(un)); setCls('badge', 'hidden', !un); setCls('inboxBtn', 'alert', un > 0);
  // звук нового письма
  { const m = S.msgs[0], top = m ? m.id : 0;
    if (UI.seenMsg === undefined) UI.seenMsg = top;
    else if (top !== UI.seenMsg) { UI.seenMsg = top; if (m) SND.play(m.type === 'war' ? (/потерян/.test(m.title) ? 'lost' : 'horn') : m.title === 'Город захвачен!' ? 'capture' : /готов/.test(m.title) ? 'ready' : 'msg'); } }
  // всплывающие заметки
  for (const t of S.toast.splice(0)) UI.toasts.push({ t, rt: now, dur: t.ev ? 5000 : 3000 });
  UI.toasts = UI.toasts.filter(q => now - q.rt < q.dur).slice(-2);
  setHtml('toasts', UI.toasts.map(q => `<div class="toast ${q.t.bad ? 'bad' : q.t.good ? 'good' : ''}">${IMG(q.t.icon || (q.t.ev ? 'scroll' : 'flag'), 28)}<div><div class="tt">${esc(q.t.title)}</div><div class="ts">${esc(q.t.text)}</div></div><div class="bar"><i style="width:${Math.max(0, 100 - (now - q.rt) / q.dur * 100).toFixed(0)}%"></i></div></div>`).join(''));
  // армия несёт тяжёлые потери — спросить про отступление
  if (S.prompts && S.prompts.length && !UI.modal && !S.over) {
    const pr = S.prompts[0], a = S.armies.find(q => q.id === pr.id);
    if (!a) S.prompts.shift(); else {
      let w = 0; for (const q of a.sq) w += q.w || 0; const hosp = nearestHospital(S, a); centerOn(a.x, a.y); RN.sel = { k: 'army', id: a.id };
      showModal('Тяжёлые потери', `<div class="bad big-res">Армия несёт большие потери!</div><div>Осталось <b class="gl">${units(a)}</b> из ${a.pk || armyCap(a)} бойцов.${w ? ' Раненых, которых можно вернуть: <b class="gl">' + w + '</b>.' : ''}</div><div class="dim" style="margin-top:6px">${hosp ? 'Отступите к госпиталю: армию вылечат, а раненых вернут в строй. Отступающие не отвечают на удары.' : 'Госпиталей нет — отступить к ближайшему городу? Постройте госпиталь, чтобы восстанавливать армии.'}</div>`,
        btn('retreatyes:' + a.id, 'Отступить', 'gold') + btn('retreatno:' + a.id, 'Продолжить бой'), { kind: 'prompt' });
    }
  }
  // подсказка плана
  const pl = planState(), showPlan = !!SET.hints && !pl.done;
  setCls('plan', 'hidden', !showPlan); setCls('hud', 'noplan', !showPlan);
  if (showPlan) setHtml('plan', `<b>${pl.i + 1}/${PLAN.length}</b><span>${PLAN[pl.i][0]}</span>`);
  if (pl.done && UI.planWas === false) { UI.toasts.push({ t: { title: 'План развития выполнен', text: 'Королевство крепко стоит на ногах.', good: true, icon: 'star' }, rt: now, dur: 5000 }); SND.play('ach'); }
  UI.planWas = pl.done;
  if (!$('mm').classList.contains('hidden')) { UI.mmN = (UI.mmN || 0) + 1; if (UI.mmN % 4 === 0) drawMinimap(S); }
  if (now - UI.achT > 2000) { UI.achT = now; checkAch(); }
}
const PLAN = [
  ['Постройте ферму на лугу (вкладка СТРОЙКА)', p => p.flags.b_farm], ['Постройте лесопилку в лесу', p => p.flags.b_sawmill], ['Постройте рудник на залежи руды', p => p.flags.b_mine],
  ['Постройте казармы', p => p.flags.b_barracks], ['Обучите ополчение в казармах — оружие ему не нужно', p => p.flags.squad], ['Постройте мастерскую: луки и копья', p => p.flags.b_workshop],
  ['Постройте кузню: мечи и доспехи', p => p.flags.b_forge], ['Поставьте мельницу вплотную к ферме', p => p.flags.b_mill], ['Постройте конюшню — для конницы', p => p.flags.b_stable],
  ['Постройте рынок', p => p.flags.b_market], ['Улучшите город: нажмите на него', p => p.flags.upg], ['Основайте второй город (ГОРОДА, затем НОВЫЙ)', (p, S) => myCities(S).length >= 2],
  ['Объедините отряды в армию (АРМИЯ, затем ОБЪЕД.)', p => p.flags.army], ['Захватите город: подведите армию. Вольный город берут без войны', p => p.flags.capture], ['Постройте порт на берегу моря', p => p.flags.b_port],
  ['Постройте судно в порту', p => p.flags.ship], ['Заключите торговое соглашение (ПОЛИТ., затем ТОРГОВЛЯ)', p => p.flags.trade], ['Постройте таверну и наймите наёмников', p => p.flags.merc]
];
function planState() { const S = UI.S, p = me(); for (let i = 0; i < PLAN.length; i++) if (!PLAN[i][1](p, S)) return { i, done: false }; return { i: PLAN.length, done: true }; }

/* ----- окно выбранного объекта ----- */
function setSheet(s) { UI.sheet = s; UI.sheetMin = false; UI.lastHtml = ''; renderSheet(true); const bd = $('sheet').querySelector('.bd'); if (bd) bd.scrollTop = 0; } // новое окно всегда открывается с начала
function closeSheet() { const had = !!RN.place; UI.sheet = null; RN.place = null; RN.sel = null; renderSheet(true); if (had) renderTabs(); }
function renderSheet(force) {
  const sh = UI.sheet, el = $('sheet'); if (!sh) { el.classList.add('hidden'); return; }
  const r = sheetHtml(sh); if (!r) { UI.sheet = null; el.classList.add('hidden'); if (RN.sel && sh.k !== 'place' && sh.k !== 'list') RN.sel = null; return; }
  const html = `<div class="hd"><div class="tt">${r.title}</div>${btn('minsheet', UI.sheetMin ? '▴' : '▾', 'x')}${btn('closesheet', IMG('x', 14), 'x')}</div><div class="bd">${r.body}</div>`;
  if (html === UI.lastHtml && !force) return; UI.lastHtml = html;
  morph(el, html); el.classList.remove('hidden'); el.classList.toggle('min', !!UI.sheetMin); el.classList.toggle('place', sh.k === 'place');
}
function needTxt(c, p) { const ks = Object.keys(c); return ks.length ? ks.map(k => `<span class="${p.stock[k] >= c[k] ? '' : 'bad'}">${IMG(k, 16)}${c[k]}</span>`).join(' ') : 'бесплатно'; }
const pct = v => Math.max(0, Math.min(100, v)).toFixed(0);
function hpLine(b) { return `<div class="row">Прочность: ${Math.max(0, Math.floor(b.hp))} из ${Math.round(b.mhp)}</div><div class="pb"><i style="width:${pct(100 * b.hp / b.mhp)}%"></i></div>`; }
function sheetHtml(sh) {
  const S = UI.S, p = me();
  switch (sh.k) {
    case 'city': {
      const c = S.cities[sh.id]; if (!c) return null; const mine = c.owner === 0;
      let b = `<div class="row">${sw(ownerCol(S, c.owner))}<span>${esc(pname(S, c.owner))} · уровень ${c.level}</span></div><div class="dim">Гарнизон: ${garrison(c)} · граница: ${c.radius.toFixed(1)} из ${cityMaxR(c)} клеток</div>`;
      if (c.cap) b += `<div class="${c.cap.by === 0 ? 'gd' : 'bad'}">Захват: ${esc(pname(S, c.cap.by))} ${Math.floor(c.cap.p * 100)}%${c.cap.p >= 1 ? ' — удержание...' : ''}</div><div class="pb"><i style="width:${pct(c.cap.p * 100)}%"></i></div>`;
      if (mine) {
        b += `<div class="dim">Доход: +${((0.6 + 0.45 * c.level) * (perk(p, 'gold') ? 1.25 : 1)).toFixed(1)} золота/с</div>`;
        b += c.level < 5 ? `<div class="row">Улучшение: ${needTxt(upgradeCost(c), p)}</div><div class="btns">${btn('upgrade:' + c.id, 'Улучшить город', 'gold')}</div>` : '<div class="gl">Максимальный уровень</div>';
      } else b += `<div class="dim">${c.owner < 0 ? 'Вольный город: его можно захватить без войны. Стрелки на стенах бьют метко — малым отрядом не взять.' : atWar(S, 0, c.owner) ? 'Вы воюете. Подведите армию, чтобы начать захват.' : 'Объявите войну, чтобы напасть.'}</div>`;
      return { title: (c.isCap ? STAR() + ' ' : '') + esc(c.name), body: b };
    }
    case 'bld': {
      const b0 = S.buildings.find(q => q.id === sh.id); if (!b0) return null; const d = BD[b0.type], mine = b0.owner === 0; let b = `<div class="row">${bldIcon(b0.type, 32)} <div class="dim" style="flex:1">${d.d}</div></div>`;
      if (!mine) return { title: d.n, body: b + `<div class="dim">Владелец: ${b0.owner < 0 ? 'никто (заброшено)' : esc(pname(S, b0.owner))}</div>` + (STRUCT[b0.type] ? hpLine(b0) : '') };
      if (b0.type === 'farm') { const mn = millNear(S, b0); b += `<div class="gd">+${farmRate(S, b0, p).toFixed(2)} еды/с${mn ? ' — рядом мельница' : ''}</div>${mn ? '' : '<div class="dim">Мельница вплотную к ферме добавит 50%.</div>'}`; }
      if (b0.type === 'mill') { let n = 0; for (const q of S.buildings) if (q.type === 'farm' && q.owner === 0 && Math.abs(q.x - b0.x) <= 1 && Math.abs(q.y - b0.y) <= 1) n++; b += `<div class="${n ? 'gd' : 'bad'}">${n ? 'Ферм рядом: ' + n + ' — каждая даёт на 50% больше еды.' : 'Рядом нет ферм — мельница простаивает.'}</div>`; }
      if (b0.type === 'sawmill') { let f = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const X = b0.x + dx, Y = b0.y + dy; if (X >= 0 && Y >= 0 && X < S.W && Y < S.H && S.ter[Y * S.W + X] === TR.FOREST) f++; } b += `<div class="gd">+${((0.45 + 0.08 * f) * p.mult * (perk(p, 'wood') ? 1.3 : 1)).toFixed(2)} дерева/с. Чем гуще лес вокруг, тем выше добыча.</div>`; }
      if (b0.type === 'mine') b += `<div class="gd">${b0.res === RES.GOLD ? 'Золотой рудник' : 'Железный рудник'}: +${(0.45 * p.mult * (perk(p, 'mine') ? 1.3 : 1)).toFixed(2)} ${b0.res === RES.GOLD ? 'золота' : 'железа'}/с</div>`;
      if (b0.type === 'market') b += `<div class="gd">+${(0.7 * p.mult).toFixed(2)} золота/с. Каждый рынок (до четырёх) добавляет дохода торговым соглашениям.</div>`;
      const rc = RECIPES[b0.type];
      if (rc) {
        b += '<div class="dim">Производит предметы для отрядов. Выберите, что делать:</div><div class="btns">';
        for (const k in rc) { const r = rc[k]; b += btn(`recipe:${b0.id}:${k}`, `${IMG(r.out, 18)} ${RNAME[r.out]}`, b0.recipe === k ? 'on' : ''); }
        const r = rc[b0.recipe], ps = prodSpeed(p); b += `</div><div class="row">Цена: ${needTxt(r.in, p)} · ${secs(r.t / ps)}с · на складе: ${S.opts.sandbox ? INF() : Math.floor(p.stock[r.out])}</div>`;
        b += b0.busy ? `<div class="gl">Производится: ${RNAME[r.out]} — ${Math.floor(100 * b0.pt / r.t)}% (ещё ${secs((r.t - b0.pt) / ps)}с)</div><div class="pb"><i style="width:${pct(100 * b0.pt / r.t)}%"></i></div>` : `<div class="${b0.off ? 'dim' : 'bad'}">${esc(b0.status || 'Ожидание ресурсов')}</div>`;
        if (b0.off && b0.busy) b += '<div class="dim">Это изделие доделают, новое начинать не будут.</div>';
        b += `<div class="btns">${btn('prod:' + b0.id, b0.off ? 'Возобновить производство' : 'Остановить производство')}</div>`;
        const capN = Math.min(b0.stop || ITEM_CAP, ITEM_CAP);
        b += `<div class="dim" style="margin-top:6px">Сколько изделий держать на складе (сейчас ${capN}). Достигнув предела, мастерская не тратит ресурсы — можно копить железо для второй кузницы.</div><div class="btns">` +
          [5, 10, 20].map(n => btn(`stopset:${b0.id}:${n}`, String(n) + ' шт.', capN === n ? 'on' : '')).join('') + btn(`stopset:${b0.id}:0`, 'Без предела', !b0.stop ? 'on' : '') + '</div>';
      }
      if (STRUCT[b0.type]) b += hpLine(b0);
      if (b0.type === 'gate') b += '<div class="dim">Свои войска и союзники проходят, враги — нет, пока ворота не сломают.</div>';
      if (b0.type === 'tower') {
        if (b0.gar && b0.gar.s.length) { const hpS = b0.gar.s.reduce((x, y) => x + y.hp, 0) / (b0.gar.s.length * SQ.archer.hp); b += `<div class="gd">В башне лучники: ${b0.gar.s.length} из 10 · здоровье ${Math.round(hpS * 100)}%. Стреляют дальше и получают на 40% меньше урона.</div><div class="btns">${btn('ungar:' + b0.id, 'Вывести отряд')}</div>`; }
        else b += '<div class="dim">Башня пуста. Выберите армию с лучниками и нажмите на башню — отряд лучников займёт её.</div>';
        if (b0.links && b0.links.length) {
          b += '<div class="gl" style="margin-top:6px">Соединения башен</div>';
          for (const l of b0.links) {
            const o = S.bmap.get(l.id);
            if (!o || o.type !== 'tower') continue;
            b += `<div class="row sp"><span>Соединение с башней</span>${btn('togglepass:' + b0.id + ':' + l.id, l.open ? 'Проход открыт' : 'Проход закрыт', l.open ? 'on' : '')}</div>`;
          }
          b += '<div class="dim">Рядом поставленные башни соединяются стеной. Проход пропускает ваших и союзников.</div>';
        }
      }
      if (b0.type === 'moat') b += b0.wet ? '<div class="gd">Ров залит водой: вражеские бойцы в нём слабеют на 3–7% и медленнее идут.</div>' : '<div class="bad">Ров не соединён с водоёмом — воды в нём нет.</div>';
      if (b0.type === 'academy') {
        const hc = hasCommander(S, 0), q0 = b0.queue[0];
        b += '<div class="dim">Полководец усиливает войска своего вида рядом с собой: +25% урона и на 20% меньше получаемого урона в радиусе 4,5 клетки.</div>';
        if (q0) { const pr = Math.max(0, Math.min(1, 1 - q0.left / (q0.total || 60))); b += `<div class="gl">Обучение: ${CMD[q0.cls].n} — ${Math.floor(pr * 100)}% (ещё ${secs(q0.left / trainSpeed(p))}с)</div><div class="pb"><i style="width:${pct(pr * 100)}%"></i></div>`; }
        else if (hc && hc !== true) { const q = hc.sq.find(z => z.t === 'cmd'); b += `<div class="gd">Полководец: ${CMD[q.cls].n} (${CMD[q.cls].of})</div><div class="btns">${btn('selarmy:' + hc.id, 'Найти армию')}</div>`; }
        else { const cost = Object.assign({ gold: SQ.cmd.gold, food: SQ.cmd.food }, SQ.cmd.need); b += '<div class="row">Цена: ' + needTxt(cost, p) + ' · ' + secs(SQ.cmd.t / trainSpeed(p)) + 'с</div><div class="list">'; for (const k in CMD) b += `<div class="it">${IMG(CMD[k].ico, 26)}<div class="g">${CMD[k].n}<div class="dim">ведёт ${CMD[k].of}</div></div>${btn('cmd:' + b0.id + ':' + k, 'Обучить')}</div>`; b += '</div>'; }
      }
      if (b0.type === 'hospital') {
        let near = 0, wnd = 0; for (const a of S.armies) if (a.owner === 0 && !a.ship && dist(a.x, a.y, b0.x + 0.5, b0.y + 0.5) <= 3.5) { near++; for (const q of a.sq) wnd += q.w || 0; }
        b += `<div class="gd">Лечит армии рядом (радиус 3,5 клетки): +2 здоровья/с. Возвращает раненых бойцов в строй — по одному раз в 5 с (8 золота и 4 еды за бойца).</div><div class="row">Армий рядом: ${near} · раненых ждёт возвращения: ${wnd}</div>${b0.treated ? '<div class="gl">Идёт лечение...</div>' : ''}`;
      }
      if (b0.type === 'barracks' || b0.type === 'siege') {
        b += b0.type === 'siege' ? '<div class="dim">Осадные машины делаются по одной. Выберите машину:</div>' : '<div class="dim">Отряд — 10 воинов. Выберите отряд для обучения:</div>';
        b += '<div class="list">'; for (const t in SQ) { if (SQ[t].bld !== b0.type) continue; const q = SQ[t], cost = Object.assign({ gold: q.gold, food: q.food }, q.need); b += `<div class="it">${IMG(q.ico, 28)}<div class="g">${q.n}<div class="dim">${needTxt(cost, p)} · ${secs(q.t / trainSpeed(p))}с${q.d ? '<br>' + q.d : ''}</div></div>${btn(`train:${b0.id}:${t}`, 'Обучить')}</div>`; } b += '</div>';
        if (b0.queue.length) {
          const q0 = b0.queue[0], T0 = q0.total || SQ[q0.t].t, pr = Math.max(0, Math.min(1, 1 - q0.left / T0)), n0 = SQ[q0.t].cnt || 10, lf = secs(q0.left / trainSpeed(p));
          b += `<div class="gl">${n0 === 1 ? 'Сборка машины: ' + SQ[q0.t].n + ' — ' + Math.floor(pr * 100) + '% (ещё ' + lf + 'с)' : 'Обучение: ' + SQ[q0.t].n + ' — вышло ' + Math.min(n0, Math.floor(pr * n0)) + ' из ' + n0 + ' (ещё ' + lf + 'с)'}</div><div class="pb"><i style="width:${pct(pr * 100)}%"></i></div>`;
          if (b0.queue.length > 1) b += '<div class="dim">В очереди: ' + b0.queue.slice(1).map(q => SQ[q.t].n).join(', ') + '</div>';
        } else b += '<div class="dim">Очередь пуста</div>';
      }
      if (b0.type === 'tavern') {
        const cd = Math.max(0, Math.ceil((b0.cd || 0) - S.time));
        b += `<div class="dim">${cd ? 'Новые наёмники подойдут через ' + cd + ' с.' : 'Наёмники готовы выступить сразу. Набор меняется каждые сутки.'}</div><div class="list">`;
        for (const t of tavernOffer(S, b0)) b += `<div class="it">${IMG(SQ[t].ico, 28)}<div class="g">${SQ[t].n}<div class="dim">${needTxt({ gold: MERC[t] }, p)}</div></div>${btn(`hire:${b0.id}:${t}`, 'Нанять', cd ? 'dis' : '')}</div>`;
        b += '</div>';
      }
      if (b0.type === 'port') {
        b += `<div class="gd">+${(0.3 * p.mult * (perk(p, 'sea') ? 3 : 1)).toFixed(2)} золота/с. Порты усиливают торговые соглашения.</div>`;
        b += `<div class="btns">${btn('ship:' + b0.id + ':trade', `${IMG('ship', 18)} Торговое судно`)}${btn('ship:' + b0.id + ':war', `${IMG('ship', 18)} Военное судно`)}</div><div class="dim">Торговое: ${needTxt(shipCost('trade', p), p)}<br>Военное (перевозит войска): ${needTxt(shipCost('war', p), p)}</div>`;
        b += '<div class="dim">Чтобы погрузить армию, выберите её и нажмите на порт. Затем выберите корабль и укажите берег.</div>';
      }
      b += `<div class="btns">${btn('demo1:' + b0.id, 'Снести')}</div>`; return { title: d.n, body: b };
    }
    case 'army': {
      const a = S.armies.find(q => q.id === sh.id); if (!a || (a.owner !== 0 && !canSee(S, a))) return null; const mine = a.owner === 0 || allied(S, 0, a.owner);
      let b = `<div class="row chips">${sw(ownerCol(S, a.owner))}<span>${units(a)} воинов</span></div><div class="chips">` + a.sq.map(q => `<span class="chip">${IMG(SQ[q.t].ico, 18)}${q.u}<i style="width:${Math.round(100 * q.hp / (sqMax(q.t) * (q.t === 'cmd' ? SQ.cmd.hp : SQ[q.t].hp)))}%"></i></span>`).join('') + '</div>';
      { let w = 0; for (const q of a.sq) w += q.w || 0; if (w) b += `<div class="dim">Раненых можно вернуть в госпитале: ${w}</div>`; }
      { const cq = a.sq.find(z => z.t === 'cmd'); if (cq) b += `<div class="gl">${CMD[cq.cls].n} (${CMD[cq.cls].of}): +25% урона и на 20% меньше получаемого урона войскам этого вида в радиусе 4,5 клетки</div>`; }
      b += `<div class="dim">${a.ship ? 'На корабле' : a.retreat ? 'Отступает' : a.sg ? 'Осада укрепления' : a.fight ? 'В бою!' : a.st === 'move' ? 'В походе' : 'Ожидает'}${combo(a) > 1 ? ' · бонус рода войск +' + Math.round((combo(a) - 1) * 100) + '%' : ''}</div>`;
      if (a.owner === 0 && !a.ship) b += `<div class="btns tiny">${btn('merge', IMG('merge', 16) + ' Объед.')}${btn('split', IMG('split', 16) + ' Разд.')}${btn('stop', 'Стоять')}${btn('retreat', 'Отступ.')}</div>`;
      return { title: a.owner === 0 ? 'Армия' : mine ? 'Армия союзника' : 'Чужая армия: ' + esc(pname(S, a.owner)), body: b };
    }
    case 'ship': {
      const s = S.ships.find(q => q.id === sh.id); if (!s) return null; const mine = s.owner === 0;
      let b = `<div class="dim">${s.kind === 'war' ? 'Военное судно: перевозит войска, в боях не участвует.' : 'Торговое судно: возит товары между портами партнёров.'}</div>`;
      if (!mine) return { title: (s.kind === 'war' ? 'Военный корабль' : 'Торговый корабль') + ': ' + esc(pname(S, s.owner)), body: b };
      b += `<div class="row">${s.cargo ? 'Груз: армия на борту' : 'Трюм пуст'} · ${s.path.length ? 'в пути' : 'стоит'}</div>`;
      if (s.kind === 'war') b += `<div class="btns">${btn('unload', 'Высадить')}${btn('stopship', 'Стоять')}</div><div class="dim">Нажмите на воду или на берег, чтобы отправить корабль. Войска высадятся на указанный берег.</div>`; else b += '<div class="dim">Корабль плавает сам, если есть торговое соглашение с владельцем порта.</div>';
      return { title: s.kind === 'war' ? 'Военный корабль' : 'Торговый корабль', body: b };
    }
    case 'place': {
      const pl = RN.place; if (!pl) return null; const isC = pl.type === 'city', line = (pl.type === 'wall' || pl.type === 'moat'), name = isC ? 'Новый город' : BD[pl.type].n;
      const tiles = pl.x != null ? (line && pl.x2 != null ? lineTiles(pl.x, pl.y, pl.x2, pl.y2) : [{ x: pl.x, y: pl.y }]) : [], one = isC ? cityCost(S, 0) : BD[pl.type].cost, cost = {}; for (const k in one) cost[k] = one[k] * Math.max(1, tiles.length);
      let msg = line ? 'Нажмите на карту: первая точка, затем вторая — построится линия' : 'Нажмите на карту — выберите место (подходящие клетки подсвечены)', ok = false, cls = 'dim';
      if (pl.x != null) {
        let good = 0, e0 = null; for (const t of tiles) { const e = siteOk(S, 0, pl.type, t.x, t.y); if (!e) good++; else e0 = e0 || e; }
        if (!good) { msg = e0; cls = 'bad'; } else if (!canAfford(p, cost)) { msg = 'Не хватает ресурсов'; cls = 'bad'; } else { msg = line && tiles.length > 1 ? `Подходит клеток: ${good} из ${tiles.length}` : 'Место подходит'; cls = 'gd'; ok = true; }
        if (line && pl.x2 == null && ok) msg += ' · нажмите вторую точку для линии';
      }
      let extra = isC ? '' : `<div class="dim xtra">${BD[pl.type].d}</div>`;
      if (isC && pl.x != null) { const cl = cityClaim(S, 0, pl.x, pl.y); extra = `<div class="dim">Территория вокруг: суши ${cl.land} клеток, свободной ${cl.free}${cl.forest ? '; лес под городом вырубят' : ''}. Чем больше свободной земли, тем лучше место.</div>`; }
      return { title: name, body: `<div class="row">${needTxt(cost, p)}</div><div class="${cls}">${msg}</div>${extra}<div class="btns">${btn('confirmplace', 'Построить', ok ? 'gold' : 'dis')}${btn('cancelplace', 'Отмена')}</div>` };
    }
    case 'list': return listHtml(sh);
  }
  return null;
}
function listHtml(sh) {
  const S = UI.S, p = me(); let b = '', title = '';
  const blds = (type, sub, none) => { const l = S.buildings.filter(x => x.owner === 0 && x.type === type); return l.length ? '<div class="list">' + l.map(x => `<div class="it">${bldIcon(type, 24)}<div class="g">${BD[type].n}${sub ? '<div class="dim">' + sub(x) + '</div>' : ''}</div>${btn('selbld:' + x.id, 'Открыть')}</div>`).join('') + '</div>' : `<div class="dim">${none}</div>`; };
  switch (sh.what) {
    case 'cities': title = 'Города'; b = '<div class="list">' + myCities(S).map(c => `<div class="it">${IMG('t_city', 24)}<div class="g">${c.isCap ? STAR() + ' ' : ''}${esc(c.name)}<div class="dim">ур. ${c.level} · граница ${c.radius.toFixed(0)}</div></div>${btn('selcity:' + c.id, 'Выбрать')}</div>`).join('') + '</div>'; break;
    case 'armies': title = 'Армии'; { const l = S.armies.filter(a => a.owner === 0 && !a.ship); b = l.length ? '<div class="list">' + l.map(a => `<div class="it">${IMG(SQ[a.sq[0].t].ico, 24)}<div class="g">Отрядов: ${a.sq.length} · воинов ${units(a)}<div class="dim">${a.fight ? 'в бою' : a.retreat ? 'отступает' : a.st === 'move' ? 'в походе' : 'ожидает'}</div></div>${btn('selarmy:' + a.id, 'Выбрать')}</div>`).join('') + '</div>' : '<div class="dim">Армий нет. Обучите отряд в казармах.</div>'; } break;
    case 'barracks': title = 'Казармы'; b = blds('barracks', x => 'очередь: ' + x.queue.length, 'Постройте казармы во вкладке СТРОЙКА.'); break;
    case 'taverns': title = 'Таверны'; b = blds('tavern', x => (x.cd || 0) > S.time ? 'наёмники подойдут через ' + Math.ceil(x.cd - S.time) + ' с' : 'наёмники ждут', 'Таверн нет. Постройте таверну (СТРОЙКА, затем УКРЕПЛ.) — в ней нанимают отряды за золото.'); break;
    case 'ports': title = 'Порты'; b = blds('port', null, 'Портов нет. Порт строится на берегу моря.'); break;
    case 'ships': title = 'Корабли'; { const l = S.ships.filter(x => x.owner === 0); b = l.length ? '<div class="list">' + l.map(x => `<div class="it">${IMG('ship', 24)}<div class="g">${x.kind === 'war' ? 'Военный' : 'Торговый'}<div class="dim">${x.cargo ? 'с войсками' : x.path.length ? 'в пути' : 'стоит'}</div></div>${btn('selship:' + x.id, 'Выбрать')}</div>`).join('') + '</div>' : '<div class="dim">Кораблей нет. Постройте их в порту.</div>'; } break;
    case 'stock': title = 'Склад'; b = STOCK_KEYS.map(k => `<div class="row sp"><span>${IMG(k, 22)} ${RNAME[k]}</span><b class="gl">${S.opts.sandbox ? INF() : Math.floor(p.stock[k])}</b></div>`).join('') + `<div class="dim">Предметов для отрядов на складе помещается не больше ${ITEM_CAP} каждого вида.</div>`; break;
    case 'dip': title = 'Дипломатия'; b = '<div class="list">' + S.players.filter(q => q.id !== 0).map(q => {
      const r = rel(S, 0, q.id), ci = claimInfo(S, 0, q.id), theirs = claimInfo(S, q.id, 0);
      const st = r.st === 'war' ? '<span class="bad">ВОЙНА</span>' : r.ally ? '<span class="gd">союз</span>' : r.st === 'truce' ? '<span class="gl">перемирие</span>' : '<span class="gd">мир</span>';
      let btns = '';
      if (q.alive) {
        if (r.st === 'war') btns += btn('offer:truce:' + q.id, 'Мир');
        else {
          if (ci && ci.ready) btns += btn('war:' + q.id, 'Объявить войну', 'gold'); else if (!ci && !r.ally) btns += btn('claim:' + q.id, 'Претензия');
          if (r.ally) btns += btn('breakally:' + q.id, 'Разорвать союз'); else if (r.st === 'peace') btns += btn('offer:ally:' + q.id, 'Союз');
          if (!r.ally && !passLeft(S, 0, q.id)) btns += btn('offer:pass:' + q.id, 'Право прохода');
        }
      }
      return `<div class="it"><div class="g">${sw(q.col)} ${esc(q.name)}${q.alive ? '' : ' (пал)'}<div class="dim">${q.alive ? st + ' · отношение ' + (r.op > 20 ? 'тёплое' : r.op < -20 ? 'холодное' : 'нейтральное') + (r.trade ? ' · торговля' : '') + ' · ' + PERKS[q.ci].n.toLowerCase() : 'государства больше нет'}</div>${ci ? `<div class="gl">Претензия: ${ci.ready ? 'готова — можно объявить войну' : ci.day + ' из 3 дней'}</div>` : ''}${passLeft(S, 0, q.id) ? `<div class="gd">Ваши войска могут проходить ещё ${Math.ceil(passLeft(S, 0, q.id) / DAY)} дн.</div>` : ''}${passLeft(S, q.id, 0) ? `<div class="dim">Они могут проходить по вашей земле ещё ${Math.ceil(passLeft(S, q.id, 0) / DAY)} дн.</div>` : ''}${theirs ? '<div class="bad">Готовит претензии к вам: ' + (theirs.ready ? 'война возможна!' : theirs.day + ' из 3 дней') + '</div>' : ''}<div class="btns tiny">${btns}</div></div></div>`;
    }).join('') + '</div><div class="dim">Армии не могут входить на чужую территорию без войны, союза или права прохода. Войну можно объявить только через 3 дня после предъявления претензии.</div>'; break;
    case 'trade': title = 'Торговля'; b = '<div class="list">' + S.players.filter(q => q.id !== 0 && q.alive).map(q => { const r = rel(S, 0, q.id); return `<div class="it"><div class="g">${sw(q.col)} ${esc(q.name)}<div class="dim">${r.trade ? '<span class="gd">соглашение действует</span>' : r.st === 'war' ? 'идёт война' : r.st === 'truce' ? 'перемирие' : 'нет соглашения'}</div></div>${r.trade ? btn('canceltrade:' + q.id, 'Расторгнуть') : r.st === 'peace' ? btn('offer:trade:' + q.id, 'Предложить') : ''}</div>`; }).join('') + '</div><div class="dim">Соглашение даёт золото обеим сторонам; порты и рынки увеличивают доход. Торговые суда привозят ещё больше.</div>'; break;
    case 'eco': title = 'Экономика'; { const rr = p.rate, bc = {}; S.buildings.forEach(x => { if (x.owner === 0) bc[x.type] = (bc[x.type] || 0) + 1; }); b = `<div class="gl">${PERKS[p.ci].n}: ${PERKS[p.ci].d.toLowerCase()}</div>` + ['gold', 'wood', 'food', 'iron'].map(k => `<div class="row sp"><span>${IMG(k, 22)} ${RNAME[k]}</span><span>${S.opts.sandbox ? '' : fmt(p.stock[k])} <span class="${rr[k] < 0 ? 'bad' : 'gd'}">${rr[k] >= 0 ? '+' : ''}${rr[k].toFixed(2)}/с</span></span></div>`).join('') + '<div class="gl" style="margin-top:6px">Здания</div>' + (Object.keys(bc).map(k => `<div class="row sp"><span>${BD[k].n}</span><b>${bc[k]}</b></div>`).join('') || '<div class="dim">пока нет</div>') + '<div class="dim" style="margin-top:6px">Отряды едят еду и стоят золота. Без еды армия слабеет.</div>'; } break;
    case 'ult': title = 'Ультиматум'; b = '<div class="dim">Потребуйте 150 золота. Слабый соперник заплатит, сильный ответит войной. Повторить требование можно через два дня.</div><div class="list">' + S.players.filter(q => q.id !== 0 && q.alive).map(q => { const cd = cdLeft(S, 0, q.id, 'ult'), e = dealError(S, 0, q.id, 'ult'); return `<div class="it"><div class="g">${sw(q.col)} ${esc(q.name)}<div class="dim">мощь ${Math.round(milPower(S, q.id))} против вашей ${Math.round(milPower(S, 0))}${cd ? '<br>повтор через ' + Math.max(1, Math.ceil(cd / DAY * 24)) + ' ч.' : ''}</div></div>${e || cd ? '' : btn('offer:ult:' + q.id, 'Потребовать')}</div>`; }).join('') + '</div>'; break;
    case 'rate': title = 'Рейтинг'; { const tg = S.opts.sandbox ? 0 : S.opts.win === 'score' ? S.opts.target : 0; b = (S.opts.sandbox ? '<div class="dim">Песочница: без победы по очкам</div>' : tg ? `<div class="dim">Победа: первый, кто наберёт ${tg} очков</div>` : '<div class="dim">Победа: захват всех столиц</div>') + '<div class="list">' + S.players.slice().sort((x, y) => y.score - x.score).map((q, i) => `<div class="it"><b>${i + 1}</b>${sw(q.col)}<div class="g">${esc(q.name)}${q.alive ? '' : ' (пал)'}<div class="dim">хозяйство ${q.eco} · война ${q.mil}</div>${tg ? `<div class="pb"><i style="width:${pct(100 * q.score / tg)}%"></i></div>` : ''}</div><b class="gl">${q.score}</b></div>`).join('') + '</div>'; } break;
    case 'plan': title = 'План развития'; { const ps = planState(); b = PLAN.map((q, i) => `<div class="row ${i < ps.i || ps.done ? 'dim' : i === ps.i ? 'gl' : ''}">${i < ps.i || ps.done ? IMG('check', 14) : i === ps.i ? '▶' : '•'}<span style="flex:1">${q[0]}</span></div>`).join(''); } break;
    case 'folk': {
      title = 'Жители провинции';
      const list = (UI.S.folk || []).filter(f => f.st !== 'dead');
      b = `<div class="dim">Всего жителей: ${list.length} · они сами заселяются, строят дома, рынки и фермы</div><div class="list">` + (list.length ? list.slice(-300).reverse().map(f => `<div class="it"><div class="g">${IMG('person', 24)}<b>${esc(f.name)}</b> <span class="dim">${Math.round(f.age)} лет · ${esc(f.job)}</span><div class="dim">${f.st === 'prison' ? '<span class="bad">в тюрьме</span>' : 'население'}${f.city != null && UI.S.cities[f.city] ? ' · ' + esc(UI.S.cities[f.city].name) : ''}</div></div>${btn('folkone:' + f.id, 'Подробнее')}</div>`).join('') : '<div class="dim">Пока никого — жители скоро придут</div>') + '</div>';
      break;
    }
    case 'folkone': {
      const f = folkGet(UI.S, sh.id); if (!f || f.st === 'dead') return null;
      title = esc(f.name);
      const c = UI.S.cities[f.city];
      b = `<div class="row">${IMG('person', 32)}<div><b>${esc(f.name)}</b><div class="dim">${Math.round(f.age)} лет · ${esc(f.job)}</div></div></div>` +
        `<div class="row sp"><span>Место</span><b>${c ? esc(c.name) : 'провинция'}</b></div>` +
        `<div class="row sp"><span>Достаток</span><b>${f.wealth > 70 ? 'богат' : f.wealth > 35 ? 'средний' : 'беден'}</b></div>` +
        `<div class="row sp"><span>Настроение</span><b class="${f.mood > 60 ? 'gd' : f.mood < 35 ? 'bad' : ''}">${Math.round(f.mood)}%</b></div>` +
        `<div class="row sp"><span>Статус</span><b>${f.st === 'prison' ? 'в тюрьме' : 'на свободе'}</b></div>` +
        `<div class="dim" style="margin-top:6px">С ним можно: вручить подарок, угостить в таверне, призвать в ополчение, казнить, нанять наёмников анонимно${f.st === 'prison' ? ', выпустить из тюрьмы' : ' или посадить в тюрьму'}.</div>` +
        `<div class="btns">${btn('folk', '← К списку')}${btn('fgift:' + f.id, 'Подарок · 10 зол.')}${btn('ffeast:' + f.id, 'Пир · 8 зол.')}${btn('fdraft:' + f.id, 'Призвать')}${btn('fkill1:' + f.id, 'Казнить')}${btn('fmerc:' + f.id, 'Наёмники (анонимно)', 'gold')}${f.st === 'prison' ? btn('fprison:' + f.id + ':0', 'Освободить') : (UI.S.buildings.some(x => x.owner === 0 && x.type === 'prison') ? btn('fprison:' + f.id + ':1', 'В тюрьму') : '')}</div>` +
        (f.st !== 'prison' && !UI.S.buildings.some(x => x.owner === 0 && x.type === 'prison') ? '<div class="dim">Посадить в тюрьму можно только при своей тюрьме (СТРОЙКА → ОБЩЕЕ).</div>' : '') +
        `<div class="dim">Наёмники стоят 150 золота и делают дело без следов. Публичная казнь пугает людей.</div>`;
      break;
    }
    default: return null;
  }
  return { title, body: b };
}
function openList(what) { closeModal(); RN.sel = null; const had = !!RN.place; RN.place = null; setSheet({ k: 'list', what }); if (had) renderTabs(); }
setInterval(() => { if (UI.S && UI.sheet && !UI.modal) renderSheet(); }, 300);

/* ----- действия ----- */
function say(t, ok) { if (!UI.S) return; UI.S.toast.push({ title: ok ? 'Готово' : 'Нельзя', text: t, t: UI.S.time, bad: !ok }); SND.play(ok ? 'ok' : 'err'); }
function selEntity(k, id) { RN.sel = { k, id }; const had = !!RN.place; RN.place = null; setSheet({ k, id }); if (had) renderTabs(); }
function selArmy() { const s = RN.sel; if (s && s.k === 'army') { const a = UI.S.armies.find(q => q.id === s.id); if (a && a.owner === 0) return a; } return null; }
const myBld = id => { const b = UI.S.buildings.find(q => q.id === +id); return b && b.owner === 0 ? b : null; }; // чужими зданиями управлять нельзя
const myShip = () => { const s = RN.sel; if (!s || s.k !== 'ship') return null; const sh = UI.S.ships.find(q => q.id === s.id); return sh && sh.owner === 0 ? sh : null; };
const CREATE_OPTS = { type: MAPTYPES, size: ['s', 'm', 'l'], sandbox: [0, 1], bots: [0, 1, 2, 3, 4, 5, 6], diff: ['easy', 'normal', 'hard', 'mixed'], win: ['score', 'caps'], target: [10000, 20000, 30000, 50000], nation: [0, 1, 2, 3, 4, 5, 6, 7], free: [0, 1, 2], events: [0, 1] };
const ACT = {
  tab(t) { if (!TABS.some(x => x[0] === t)) return; UI.tab = t; renderTabs(); SND.play('tab'); if (t === 'pol') showPolMenu(); },
  pause() { UI.paused = !UI.paused; renderTabs(); },
  speed() { UI.speed = UI.speed === 1 ? 2 : UI.speed === 2 ? 4 : 1; renderTabs(); },
  closesheet() { closeSheet(); },
  bpage(n) { UI.bpage = (+n) % 3; renderTabs(); },
  ungar(id) { const b = myBld(id); if (b && b.gar) { ungarrison(UI.S, b); say('Отряд покинул башню', true); renderSheet(true); } },
  togglepass(a) { const [id, other] = a.split(':'), b = myBld(id); if (!b) return; const e = toggleTowerPass(UI.S, b, +other); say(e || 'Проход изменён', !e); recomputeAll(); renderSheet(true); },
  minsheet() { UI.sheetMin = !UI.sheetMin; renderSheet(true); },
  center() { const c = capitalOf(UI.S, 0); if (c) centerOn(c.x + 1, c.y + 1); },
  mm() { $('mm').classList.toggle('hidden'); drawMinimap(UI.S); renderTabs(); },
  borders() { RN.showTerr = !RN.showTerr; renderTabs(); },
  stock() { openList('stock'); }, plan() { openList('plan'); }, rate() { openList('rate'); },
  save() { UI.saveMode = 'save'; showSaves('save'); },
  menu() { showMenuModal(); }, cities() { openList('cities'); }, armies() { openList('armies'); }, barr() { openList('barracks'); }, taverns() { openList('taverns'); }, ports() { openList('ports'); }, ships() { openList('ships'); },
  dip() { openList('dip'); }, trade() { openList('trade'); }, eco() { openList('eco'); }, ult() { openList('ult'); },
  folk() { openList('folk'); }, folkone(id) { setSheet({ k: 'list', what: 'folkone', id: +id }); },
  fkill1(id) { const f = folkGet(UI.S, id); if (!f) return; confirmBox('Казнить жителя?', '<b>' + esc(f.name) + '</b> будет казнён публично. Люди встревожены.', 'Казнить', 'fkill2:' + id, 'saves'); },
  fkill2(id) { closeModal(); const e = folkKill(UI.S, id, false); say(e || 'Приказ выполнен', !e); const f = folkGet(UI.S, id); if (!f || f.st === 'dead') openList('folk'); else renderSheet(true); },
  fmerc(id) { const f = folkGet(UI.S, id); if (!f) return; const e = folkKill(UI.S, id, true); say(e || 'Наёмники вышли на дело — результат будет известен сразу', !e); const g = folkGet(UI.S, id); if (!g || g.st === 'dead') openList('folk'); else renderSheet(true); },
  fprison(a) { const [id, y] = a.split(':'); const e = folkPrison(UI.S, id, y === '1'); say(e || (y === '1' ? 'Посажен в тюрьму' : 'Освобождён'), !e); renderSheet(true); },
  fgift(id) { const e = folkGift(UI.S, id); say(e || 'Подарок вручён', !e); renderSheet(true); },
  ffeast(id) { const e = folkGift(UI.S, id, true); say(e || 'Пир устроен', !e); renderSheet(true); },
  fdraft(id) { const e = folkDraft(UI.S, id); say(e || 'Житель призван в ополчение', !e); renderSheet(true); },
  stopset(a) { const [id, n] = a.split(':'); const b = UI.S.bmap.get(+id); if (!b) return; b.stop = +n ? +n : null; renderSheet(true); },
  song() { SND.next(); showSettings(true); },
  track() { SND.trackToggle(); showSettings(true); },
  tracknext() { SND.trackNext(); showSettings(true); },
  nations() { closeModal(); showNatList(); }, polsan() { closeModal(); showSanctionPick(); },
  nat(id) { showNatCard(+id); }, sancpick(id) { showSanctions(+id); },
  sanc(a) { const [k, id] = a.split(':'); const e = applySanction(UI.S, 0, +id, k); say(e || 'Санкция введена', !e); recomputeAll(); showSanctions(+id); },
  unsanc(a) { const [k, id] = a.split(':'); const e = liftSanction(UI.S, 0, +id, k); say(e || 'Санкция снята', !e); recomputeAll(); showSanctions(+id); },
  attack() { closeModal(); showAttack(); },
  warask(id) { const q = UI.S.players[+id]; if (!q || +id === 0) return; const r = rel(UI.S, 0, +id); if (r.ally) return say('Это союзник — сначала разорвите союз в ПОЛИТ.', false); confirmBox('Объявить войну?', 'Вы объявляете войну: <b>' + esc(q.name) + '</b>. Армии сразу смогут вступать на их землю.', 'Напасть!', 'doattack:' + id); },
  doattack(id) { closeModal(); const S = UI.S, q = S.players[+id]; if (!q || +id === 0 || !q.alive) return; if (atWar(S, 0, +id)) return say('Война уже идёт', false); const e = startWar(S, 0, +id); if (e) return say(e, false); say('Война объявлена: ' + q.name, true); SND.play('horn'); recomputeAll(); },
  place(t) { if (t !== 'city' && !BD[t]) return; RN.sel = null; RN.place = { type: t, x: null, y: null }; setSheet({ k: 'place' }); renderTabs(); },
  confirmplace() {
    const pl = RN.place; if (!pl || pl.x == null) return; const S = UI.S, isLine = (pl.type === 'wall' || pl.type === 'moat') && pl.x2 != null;
    const tiles = isLine ? lineTiles(pl.x, pl.y, pl.x2, pl.y2) : [{ x: pl.x, y: pl.y }]; let built = 0, fail = '', todo = tiles.slice();
    // ров копают от воды: проходим линию несколько раз, пока хоть что-то строится
    for (let pass = 0; pass < tiles.length && todo.length; pass++) { let pr = false; for (const t of todo.slice()) { const e = doBuild(S, 0, pl.type, t.x, t.y); if (!e) { built++; pr = true; todo.splice(todo.indexOf(t), 1); } else fail = e; } if (!pr) break; }
    if (!built) say(fail || 'Нельзя построить', false);
    else { say(built > 1 ? 'Построено клеток: ' + built : 'Построено', true); SND.play('build'); recomputeAll(); if (pl.type === 'city') { closeSheet(); return; } }
    pl.x = null; pl.y = null; pl.x2 = null; pl.y2 = null; renderSheet(true);
  },
  cancelplace() { closeSheet(); },
  upg() { const s = RN.sel, c = s && s.k === 'city' ? UI.S.cities[s.id] : null; if (c && c.owner === 0) ACT.upgrade(c.id); else openList('cities'); },
  upgrade(id) { const c = UI.S.cities[+id]; if (!c || c.owner !== 0) return; const e = upgradeCity(UI.S, c); say(e || 'Город улучшен', !e); if (!e) SND.play('build'); renderSheet(true); },
  selcity(id) { const c = UI.S.cities[+id]; if (c) { centerOn(c.x + 1, c.y + 1); selEntity('city', c.id); } },
  selarmy(id) { const a = UI.S.armies.find(q => q.id === +id); if (a) { centerOn(a.x, a.y); selEntity('army', a.id); } },
  selbld(id) { const b = UI.S.buildings.find(q => q.id === +id); if (b) { centerOn(b.x, b.y); selEntity('bld', b.id); } },
  selship(id) { const s = UI.S.ships.find(q => q.id === +id); if (s) { centerOn(s.x, s.y); selEntity('ship', s.id); } },
  recipe(a) { const [id, k] = a.split(':'), b = myBld(id); if (b) { const e = setRecipe(UI.S, b, k); if (e) say(e, false); renderSheet(true); } },
  prod(id) { const b = myBld(id); if (b && RECIPES[b.type]) { b.off = !b.off; renderSheet(true); } },
  cmd(a) { const [id, k] = a.split(':'), b = myBld(id); if (!b) return; const e = enqueueCmd(UI.S, b, k); if (e) say(e, false); else SND.play('coin'); renderSheet(true); },
  train(a) { const [id, t] = a.split(':'), b = myBld(id); if (!b) return; const e = enqueue(UI.S, b, t); if (e) say(e, false); else SND.play('coin'); renderSheet(true); },
  hire(a) { const [id, t] = a.split(':'), b = myBld(id); if (!b) return; const e = hireMerc(UI.S, b, t); say(e || SQ[t].n + ': наёмники у таверны', !e); if (!e) recomputeAll(); renderSheet(true); },
  ship(a) { const [id, k] = a.split(':'), b = myBld(id); if (!b) return; const e = buildShip(UI.S, b, k); say(e || 'Судно построено', !e); },
  demo1(id) { const b = myBld(id); if (!b) return; if (SET.confirm) confirmBox('Снести здание?', `${BD[b.type].n} будет снесено. Ресурсы не возвращаются${b.queue && b.queue.length ? ', очередь обучения пропадёт' : ''}.`, 'Снести', 'demo2:' + b.id); else ACT.demo2(id); },
  demo2(id) { const b = myBld(id); closeModal(); if (b) { demolish(UI.S, b); recomputeAll(); closeSheet(); say('Здание снесено', true); } },
  merge() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); if (a.ship) return say('Армия на корабле', false); const n = mergeNear(UI.S, a); say(n ? 'Отряды объединены' : 'Рядом нет своих армий (нужно подойти вплотную) или отрядов больше ' + MAX_SQ_ARMY, !!n); renderSheet(true); },
  split() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); if (a.ship) return say('Армия на корабле', false); const n = splitArmy(UI.S, a); say(n ? 'Армия разделена' : 'В армии один отряд', !!n); },
  stop() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); stopArmy(a); },
  retreat() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); const e = retreat(UI.S, a); if (e) say(e, false); },
  stopship() { const sh = myShip(); if (sh) stopShip(sh); else if (RN.sel && RN.sel.k === 'ship') say('Это чужой корабль', false); else openList('ships'); },
  unload() { const sh = myShip(); if (!sh) return say('Выберите свой военный корабль', false); const e = unloadShip(UI.S, sh); say(e || 'Войска высажены', !e); },
  claim(id) { const e = startClaim(UI.S, 0, +id); say(e || 'Претензия предъявлена: 3 дня', !e); renderSheet(true); },
  breakally(id) { const q = UI.S.players[+id]; if (q && +id !== 0 && rel(UI.S, 0, +id).ally) { breakAlly(UI.S, 0, +id); renderSheet(true); } },
  retreatyes(id) { const S = UI.S; S.prompts.shift(); const a = S.armies.find(q => q.id === +id && q.owner === 0); closeModal(); if (a) { const e = retreat(S, a); say(e || 'Армия отступает', !e); } },
  retreatno() { UI.S.prompts.shift(); closeModal(); },
  war(id) { const e = declareWar(UI.S, 0, +id); say(e || 'Война объявлена', !e); renderSheet(true); },
  offer(a) { const [k, id] = a.split(':'), e = offer(UI.S, 0, +id, k); say(e || 'Предложение отправлено', !e); renderSheet(true); },
  canceltrade(id) { const q = UI.S.players[+id]; if (q && +id !== 0 && rel(UI.S, 0, +id).trade) { cancelTrade(UI.S, 0, +id); renderSheet(true); } },
  inbox() { UI.inboxNote = ''; showInbox(); },
  answer(a) { const [id, y] = a.split(':'), e = answerMsg(UI.S, +id, y === '1'); UI.inboxNote = e || ''; SND.play(e ? 'err' : 'ok'); recomputeAll(); showInbox(true); },
  inboxtab(t) { if (['all', 'war', 'trade', 'econ'].indexOf(t) < 0) return; UI.inboxTab = t; UI.inboxNote = ''; showInbox(true); },
  closemodal() { if (UI.modal && UI.modal.lock) return; const back = UI.modal && UI.modal.back; closeModal(); if (back === 'menu' && UI.S) showMenuModal(); },
  newgame() { if (hasSave()) confirmBox('Начать новую игру?', 'Сохранённая партия будет заменена новой после первого автосохранения.', 'Новая игра', 'newgame2'); else showCreate(); },
  newgame2() { closeModal(); showCreate(); },
  continue() { UI.saveMode = 'load'; showSaves('load'); },
  loads(slot) {
    closeModal();
    const S = loadGame(slot); if (S) { startGame(S); return; }
    if (loadGame.error) showModal('Сохранение не читается', `<div>${esc(loadGame.error)}.</div><div class="dim" style="margin-top:6px">Продолжить эту партию нельзя. Удалить повреждённый слот?</div>`, btn('delsave2:' + slot, 'Удалить', 'gold') + btn('closemodal', 'Оставить'));
    else say('Этот слот пуст', false);
  },
  saveinto(a) {
    const i = a.indexOf(':'), slot = i < 0 ? a : a.slice(0, i), yes = i >= 0 && a.slice(i + 1) === '1';
    if (yes) { closeModal(); const ok = saveGame(UI.S, slot); say(ok ? 'Сохранено: ' + slotName(slot) : 'Не удалось сохранить партию', ok); return; }
    const m = readSaveMeta(slot);
    if (m && slot !== 'auto') confirmBox('Перезаписать слот?', '<b>' + slotName(slot) + '</b>: ' + esc(m.name) + ', день ' + m.day + ', очки ' + m.score + '. Старое сохранение будет заменено.', 'Сохранить', 'saveinto:' + slot + ':1');
    else { const ok = saveGame(UI.S, slot); say(ok ? 'Сохранено: ' + slotName(slot) : 'Не удалось сохранить партию', ok); }
  },
  delsave(slot) { confirmBox('Удалить сохранение?', 'Слот <b>' + slotName(slot) + '</b> будет очищен.', 'Удалить', 'delsave2:' + slot); },
  delsave2(slot) { dropSave(slot); closeModal(); refreshMenu(); showSaves(UI.saveMode || 'load'); },
  dropsave() { dropSave(); closeModal(); refreshMenu(); },
  howto() { showModal('Как играть', HOWTO, null, { back: UI.S ? 'menu' : '' }); },
  ach() { showAch(); }, records() { showRecords(); }, settings() { showSettings(); },
  toggle(k) { if (!Object.prototype.hasOwnProperty.call(SET, k)) return; SET[k] = SET[k] ? 0 : 1; applySettings(); showSettings(true); },
  asktomenu() { confirmBox('Выйти в главное меню?', 'Партия будет сохранена — её можно продолжить позже.', 'Выйти', 'tomenu', UI.S && !UI.S.over ? 'menu' : ''); },
  tomenu() {
    const S = UI.S; if (S && !S.over) saveGame(S); // выход в меню не должен стоить игроку прогресса
    closeModal(); $('hud').classList.add('hidden'); $('sheet').classList.add('hidden'); $('mm').classList.add('hidden'); $('menu').classList.remove('hidden');
    UI.S = null; UI.sheet = null; RN.S = null; RN.sel = null; RN.place = null; refreshMenu(); initDemo();
  },
  save2() { closeModal(); UI.saveMode = 'save'; showSaves('save', 'menu'); },
  cset(a) {
    const i = a.indexOf('='), k = a.slice(0, i), raw = a.slice(i + 1), list = CREATE_OPTS[k]; if (!list) return;
    const v = list.find(x => String(x) === raw); if (v === undefined) return; UI.create[k] = v;
    if (!UI.create.sandbox && UI.create.bots < 1) UI.create.bots = 1; showCreate();
  },
  start() {
    const o = Object.assign({}, UI.create); o.sandbox = !!o.sandbox; $('loading').classList.remove('hidden');
    // даём экрану показать «Создаём мир...» и только потом занимаем процессор
    setTimeout(() => {
      let S = null; try { S = newGame(o); } catch (e) { S = null; }
      $('loading').classList.add('hidden');
      if (!S) { showModal('Не получилось', '<div>Не удалось создать карту с такими настройками.</div><div class="dim">Попробуйте другой тип или размер карты либо меньше соперников.</div>'); return; }
      $('create').classList.add('hidden'); startGame(S);
    }, 60);
  },
  back() { $('create').classList.add('hidden'); $('menu').classList.remove('hidden'); }
};
function recomputeAll() { calcRates(UI.S); calcScore(UI.S); }
function refreshMenu() { $('contBtn').classList.toggle('hidden', !hasSave()); }
document.addEventListener('click', e => {
  const t = e.target.closest ? e.target.closest('[data-act]') : null; if (!t) return;
  const s = t.getAttribute('data-act'), i = s.indexOf(':'), n = i < 0 ? s : s.slice(0, i), a = i < 0 ? '' : s.slice(i + 1);
  if (!Object.prototype.hasOwnProperty.call(ACT, n)) return;
  if (n !== 'tab') SND.play('click');
  try { ACT[n](a); } catch (err) { uiError(err); }
  if (UI.modal && UI.modal.kind === 'nat' && UI.natSel != null && NAT_ACTS[n]) showNatCard(UI.natSel);
});
$('mm').addEventListener('click', e => { const r = $('mm').getBoundingClientRect(), S = UI.S; if (S && r.width) centerOn((e.clientX - r.left) / r.width * S.W, (e.clientY - r.top) / r.height * S.H); });

/* ----- нажатия на карту ----- */
UI.mapTap = function (tx, ty) {
  const S = UI.S; if (!S || S.over || UI.modal) return;
  if (!(tx >= 0 && ty >= 0 && tx < S.W && ty < S.H)) { if (!RN.place) closeSheet(); return; } // нажатие за краем карты
  if (RN.place) {
    const pl = RN.place, off = pl.type === 'city' ? 0.5 : 0, px = Math.max(0, Math.floor(tx - off)), py = Math.max(0, Math.floor(ty - off));
    if (pl.type === 'wall' || pl.type === 'moat') { if (pl.x == null || pl.x2 != null) { pl.x = px; pl.y = py; pl.x2 = null; pl.y2 = null; } else { pl.x2 = px; pl.y2 = py; } } else { pl.x = px; pl.y = py; }
    renderSheet(true); return;
  }
  const pick = () => {
    const mg = markerHit(S, tx, ty);
    if (mg) { const cur = RN.sel, idx = cur && cur.k === 'army' ? mg.list.findIndex(q => q.id === cur.id) : -1, a = mg.list[(idx + 1) % mg.list.length]; return { k: 'army', id: a.id }; }
    let best = null, bd = 0.6;
    for (const a of S.armies) if (!a.ship && canSee(S, a)) { const d = dist(a.x, a.y, tx, ty); if (d < bd) { bd = d; best = { k: 'army', id: a.id }; } }
    for (const s of S.ships) if (canSee(S, s)) { const d = dist(s.x, s.y, tx, ty); if (d < bd) { bd = d; best = { k: 'ship', id: s.id }; } }
    if (best) return best;
    const o = S.occ[Math.floor(ty) * S.W + Math.floor(tx)]; if (o >= 1000000) return { k: 'city', id: o - 1000000 }; if (o >= 0) return { k: 'bld', id: o };
    return null;
  };
  const hit = pick(), cur = RN.sel;
  const ownA = cur && cur.k === 'army' ? S.armies.find(a => a.id === cur.id && a.owner === 0) : null, ownS = cur && cur.k === 'ship' ? S.ships.find(a => a.id === cur.id && a.owner === 0) : null;
  const hitOwnArmy = hit && hit.k === 'army' && S.armies.some(a => a.id === hit.id && a.owner === 0), hitOwnShip = hit && hit.k === 'ship' && S.ships.some(a => a.id === hit.id && a.owner === 0);
  if (ownA && !hitOwnArmy) { const e = orderMove(S, ownA, tx, ty); if (e) say(e, false); return; }
  if (ownS && ownS.kind === 'war' && !hitOwnShip) { const e = orderShip(S, ownS, tx, ty); if (e) say(e, false); return; }
  if (hit) selEntity(hit.k, hit.id); else closeSheet();
};

/* ----- окна ----- */
const HOWTO = `<b class="gl">Цель.</b> Развивайте королевство, захватывайте города и копите очки (или захватите все столицы).<br><br>
<b class="gl">Управление.</b> Тяните карту пальцем, щипком меняйте масштаб. Нажмите на здание, город или армию — откроется окно. Кнопка «Назад» закрывает окно или открывает меню.<br><br>
<b class="gl">Стройка.</b> Вкладка СТРОЙКА: выберите здание и нажмите на подсвеченную клетку. Ферма — на лугу, лесопилка — в лесу, рудник — на залежи, порт — на берегу моря. Мельница вплотную к фермам даёт им +50% еды. Строить можно только в своих границах; границы растут со временем.<br><br>
<b class="gl">Войска.</b> Мастерская делает луки и копья, кузня — мечи и доспехи, конюшня — коней. В казармах обучают отряды по 10 воинов. Ополчению оружие не нужно, но оно слабое. Копейщики бьют конницу, конница — лучников, тяжёлые рыцари — конницу, лучники — лёгких рыцарей. В таверне можно сразу нанять наёмников за золото.<br><br>
<b class="gl">Армии.</b> Выберите армию и нажмите на карту — она пойдёт. Отряды, стоящие вплотную, можно объединить (до ${MAX_SQ_ARMY} отрядов). Разные рода войск в одной армии дают бонус. Новый приказ отменяет отступление.<br><br>
<b class="gl">Захват.</b> Подведите армию к городу: поверх него появится сетка цвета захватчика. Когда сетка заполнится и армия удержит город, он ваш. Вольные (серые) города можно брать без объявления войны, но обороняются они крепко.<br><br>
<b class="gl">Укрепления.</b> СТРОЙКА, затем УКРЕПЛ.: стены и ров строятся линией (нажмите две точки). Ров должен начинаться у воды. Башня вмещает отряд лучников: выберите армию и нажмите на башню. Ворота своих пропускают, врагов — нет. Осадная мастерская делает катапульты (бьют издалека) и тараны (быстро ломают стены и ворота).<br><br>
<b class="gl">Полководцы.</b> Королевская академия — только одна. В ней обучают одного полководца: Воеводу (лучники), Капитана (лёгкие рыцари), Лорда-командора (тяжёлые рыцари) или Маршала (конница). Рядом с ним войска его вида бьют сильнее и получают меньше урона.<br><br>
<b class="gl">Море.</b> В порту стройте суда. Военное судно перевозит армию: выберите армию и нажмите на порт, затем выберите корабль и нажмите на берег.<br><br>
<b class="gl">Политика.</b> Во вкладке ПОЛИТ. открывается меню: выберите страну и посмотрите её характеристики (мощь, города, отношение). Предъявляйте претензии, объявляйте войну, торгуйте. Раздел «Санкции» — как в Victoria 3: закрыть транзит, торговое эмбарго, пошлины, запрет оружия, конфискация казны и другие. Кнопка НАПАСТЬ во вкладке АРМИЯ объявляет войну выбранной стране сразу.
<b class=<b class="gl">Жители и сохранение.</b> Кнопка ЖИТЕЛИ рядом с КАРТА показывает всех жителей провинции: они сами заселяются и строят дома, рынки и фермы. На жителя можно нажать и сделать с ним что-нибудь: казнить, отправить наёмников анонимно или посадить в тюрьму. Кнопка СОХРАН. открывает слоты сохранений — выбирайте, в какой слот сохранить и какой загрузить. Рядом поставленные башни соединяются стеной, в соединении можно сделать проход.
<b class=<b class="gl">Державы и события.</b> У каждой державы своя особенность — она указана при выборе. Соперники развиваются сами, но постепенно. Время от времени случаются события: урожай, караван, пожар, ярмарка.`;
function showModal(title, body, foot, o) {
  o = o || {}; const el = $('modal'), was = !el.classList.contains('hidden') && !!UI.modal;
  const html = `<div class="box"><div class="hd"><div class="tt">${title}</div></div><div class="bd${o.flat ? ' flat' : ''}">${body}</div><div class="ft">${foot || btn('closemodal', 'Закрыть')}</div></div>`;
  if (o.keep && was) morph(el, html); else el.innerHTML = html;
  UI.modal = { kind: o.kind || '', lock: !!o.lock, back: o.back || '' }; el.classList.remove('hidden');
}
function closeModal() { UI.modal = null; $('modal').classList.add('hidden'); }
function confirmBox(title, text, yes, act, back) { showModal(title, `<div>${text}</div>`, btn(act, yes, 'gold') + btn('closemodal', 'Отмена'), { back: back || '' }); }
function showMenuModal() {
  const S = UI.S; if (!S) return;
  showModal('Меню', `<div class="dim">Игра приостановлена · ${gameClock(S.time)}</div>`, btn('closemodal', 'Продолжить', 'gold') + btn('save2', 'Сохранить') + btn('settings', 'Настройки') + btn('howto', 'Как играть') + btn('ach', 'Награды') + btn('asktomenu', 'В главное меню'), { kind: 'menu' });
}
function showInbox(keep) {
  const S = UI.S, f = UI.inboxTab, types = { all: null, war: ['war'], trade: ['trade', 'truce'], econ: ['econ', 'info'] };
  const list = S.msgs.filter(m => !types[f] || types[f].indexOf(m.type) >= 0); list.forEach(m => { if (!m.kind || m.done) m.read = true; });
  const ic = { war: 'skull', trade: 'cart', truce: 'scroll', econ: 'chart', info: 'flag' };
  let body = `<div class="tabs2">${[['all', 'Все'], ['war', 'Война'], ['trade', 'Торговля'], ['econ', 'Страна']].map(t => btn('inboxtab:' + t[0], t[1], f === t[0] ? 'on' : '')).join('')}</div>`;
  if (UI.inboxNote) body += `<div class="note parch">${esc(UI.inboxNote)}</div>`;
  body += list.length ? list.map(m => {
    let act = '';
    if (m.kind && !m.done) act = `<div class="btns">${btn('answer:' + m.id + ':1', m.kind === 'event' ? 'Заплатить' : m.kind === 'ult' ? 'Заплатить 150' : 'Принять', 'gold')}${btn('answer:' + m.id + ':0', m.kind === 'ult' ? 'Отказать (война)' : 'Отказать')}</div>`;
    else if (m.kind) act = `<div class="dim">${m.late ? 'уже не действует' : 'ответ дан'}</div>`;
    return `<div class="msg ${m.read ? '' : 'unread'}">${IMG(m.kind === 'event' ? 'scroll' : ic[m.type] || 'mail', 28)}<div class="g"><span class="gl">${esc(m.title)}</span> <span class="dim">${gameClock(m.t)}</span><div class="dim">${esc(m.text)}</div>${act}</div></div>`;
  }).join('') : '<div class="dim" style="padding:10px">Писем нет.</div>';
  showModal(IMG('mail', 24) + 'Письма', body, null, { flat: 1, kind: 'inbox', keep });
}
function showAch(keep) {
  const n = ACH.filter(q => GOT[q[0]]).length;
  showModal(IMG('star', 22) + `Награды: ${n} из ${ACH.length}`, ACH.map(q => `<div class="ach ${GOT[q[0]] ? 'got' : ''}">${IMG(GOT[q[0]] ? 'star' : 'lock', 28)}<div class="g"><div class="nm">${q[1]}</div><div class="dim">${q[2]}</div></div></div>`).join('') + '<div class="dim">В песочнице награды не выдаются.</div>', null, { kind: 'ach', keep, back: UI.S ? 'menu' : '' });
}
function showRecords() {
  const r = loadRecords();
  showModal(IMG('trophy', 22) + 'Рекорды', r.length ? r.map((q, i) => `<div class="rec"><b class="n">${i + 1}</b><div>${sw(PCOL[q.ci])} ${PNAMES[q.ci]}<div class="dim">${q.win ? 'победа' : 'поражение'} · день ${q.days} · ${q.diff === 'mixed' ? 'разные' : DIFF[q.diff].n.toLowerCase()}</div></div><b class="gl">${q.score}</b></div>`).join('') : '<div class="dim">Пока пусто. Завершите партию — результат появится здесь.</div>');
}
function showSettings(keep) {
  const row = (k, name, hint) => `<div class="opt"><div>${name}<div class="dim">${hint}</div></div>${btn('toggle:' + k, SET[k] ? 'Вкл.' : 'Выкл.', SET[k] ? 'on' : '')}</div>`;
  let b = row('sfx', 'Звуки', 'Щелчки, бой, сигналы') + row('music', 'Музыка', 'Лютня на фоне') + `<div class="opt"><div>Мелодия<div class="dim">${esc(SND.songName() || 'сменяется автоматически')}</div></div>${btn('song', 'Следующая')}</div>` + `<div class="opt"><div>Саундтрек<div class="dim">${esc(SND.trackName())}${SND.trackPlaying() ? ' · играет' : ''}</div></div>${btn('track', SND.trackPlaying() ? 'Стоп' : 'Слушать')}${btn('tracknext', 'Дальше')}</div>` + row('hints', 'Подсказки плана', 'Свиток с текущим шагом развития') + row('confirm', 'Спрашивать перед сносом', 'Защита от случайного нажатия');
  b += `<div class="dim" style="margin-top:8px">Kingdom Crash ${GAME_VERSION}. Игра работает без интернета и ничего никуда не отправляет.</div>`;
  showModal(IMG('gear', 22) + 'Настройки', b, UI.S ? null : btn('closemodal', 'Закрыть', 'gold') + (hasSave() ? btn('askdrop', 'Удалить сохранение') : ''), { kind: 'settings', keep, back: UI.S ? 'menu' : '' });
}
ACT.askdrop = () => confirmBox('Удалить сохранение?', 'Все сохранённые партии исчезнут без возможности восстановить.', 'Удалить', 'dropsave');

/* ----- слоты сохранений ----- */
const slotName = s => s === 'auto' ? 'Автосохранение' : 'Слот ' + s;
function showSaves(mode, back) {
  UI.saveMode = mode = mode === 'save' ? 'save' : 'load';
  const rows = SAVE_SLOTS.map(slot => {
    const m = readSaveMeta(slot);
    const info = m ? `${sw(PCOL[m.ci] || '#999')} <b>${esc(m.name)}</b> · день ${m.day} · очки ${m.score}${m.at ? ' · ' + new Date(m.at).toLocaleDateString() : ''}` : '<span class="dim">пусто</span>';
    const acts = m
      ? (mode === 'save' ? btn('saveinto:' + slot, 'Сохранить сюда', 'gold') + btn('loads:' + slot, 'Загрузить') + btn('delsave:' + slot, 'Удалить') : btn('loads:' + slot, 'Загрузить', 'gold') + btn('delsave:' + slot, 'Удалить'))
      : (mode === 'save' ? btn('saveinto:' + slot, 'Сохранить сюда', 'gold') : '<span class="dim">—</span>');
    return `<div class="it"><div class="g"><b>${slotName(slot)}</b><div class="dim">${info}</div></div><div class="btns tiny">${acts}</div></div>`;
  }).join('');
  showModal(IMG('scroll', 22) + (mode === 'save' ? 'Сохранить партию' : 'Загрузить партию'),
    `<div class="dim">Выберите слот${mode === 'save' ? ' для сохранения' : ' для загрузки'} — сохранений может быть несколько.</div><div class="list">${rows}</div>`,
    back === 'menu' ? btn('closemodal', 'Назад', 'gold') : btn('closemodal', 'Закрыть'), { kind: 'saves', back: back || '' });
}

/* ----- меню политики и державы ----- */
function showPolMenu() {
  showModal(IMG('t_pol', 22) + 'Политика', `<div class="dim">Выберите раздел, затем страну</div><div class="list">
    ${btn('nations', 'Державы и характеристики', 'gold')}${btn('polsan', 'Санкции')}${btn('dip', 'Дипломатия')}${btn('trade', 'Торговля')}${btn('eco', 'Экономика')}${btn('ult', 'Ультиматум')}${btn('attack', 'Объявить войну')}${btn('rate', 'Рейтинг')}
  </div>`, btn('closemodal', 'Закрыть'), { kind: 'pol' });
}
function showNatList() {
  const S = UI.S; if (!S) return;
  showModal(IMG('globe', 22) + 'Державы', '<div class="list">' + S.players.filter(q => q.id !== 0).map(q => `<div class="it"><div class="g">${sw(q.col)} ${esc(q.name)}${q.alive ? '' : ' (пал)'}<div class="dim">${PERKS[q.ci].n.toLowerCase()} · мощь ${Math.round(milPower(S, q.id))}</div></div>${q.alive ? btn('nat:' + q.id, 'Открыть', 'gold') : ''}</div>`).join('') + '</div>', btn('closemodal', 'Закрыть'), { kind: 'natlist' });
}
function showNatCard(id) {
  const S = UI.S, q = S.players[id]; if (!S || !q || id === 0) return;
  UI.natSel = id;
  const r = rel(S, 0, id), ci = claimInfo(S, 0, id), cities = S.cities.filter(c => c.owner === id), caps = cities.filter(c => c.isCap).length;
  const blds = S.buildings.reduce((n, b) => n + (b.owner === id ? 1 : 0), 0), sans = sanList(S, 0, id).concat(sanList(S, id, 0));
  const st = r.st === 'war' ? '<span class="bad">идёт война</span>' : r.st === 'truce' ? 'перемирие' : r.ally ? '<span class="gd">союзник</span>' : 'мир';
  const power = milPower(S, id), mine = milPower(S, 0);
  let b = `<div class="row">${sw(q.col)}<div><b>${esc(q.name)}</b><div class="dim">${PERKS[q.ci].n}: ${PERKS[q.ci].d.toLowerCase()}</div></div></div>` +
    `<div class="row sp"><span>Статус</span><b>${st}</b></div>` +
    `<div class="row sp"><span>Отношение</span><b class="${r.op > 20 ? 'gd' : r.op < -20 ? 'bad' : ''}">${Math.round(r.op)}</b></div>` +
    `<div class="row sp"><span>Города</span><b>${cities.length} (столиц: ${caps})</b></div>` +
    `<div class="row sp"><span>Здания</span><b>${blds}</b></div>` +
    `<div class="row sp"><span>Мощь армии</span><b class="${power > mine * 1.3 ? 'bad' : power < mine * 0.7 ? 'gd' : ''}">${Math.round(power)} против вашей ${Math.round(mine)}</b></div>` +
    `<div class="row sp"><span>Очки</span><b>${q.score}</b></div>` +
    `<div class="row sp"><span>Торговля</span><b>${r.trade ? 'соглашение есть' : 'нет'}</b></div>`;
  if (passLeft(S, 0, id)) b += `<div class="gd">Ваш проход: ещё ${Math.ceil(passLeft(S, 0, id) / DAY)} дн.</div>`;
  if (passLeft(S, id, 0)) b += `<div class="dim">Их проход: ещё ${Math.ceil(passLeft(S, id, 0) / DAY)} дн.</div>`;
  if (ci) b += `<div class="${ci.ready ? 'gl' : 'dim'}">Претензия: ${ci.ready ? 'готова — можно объявлять войну' : ci.day + ' из 3 дней'}</div>`;
  if (sans.length) b += '<div class="gl" style="margin-top:6px">Санкции</div>' + sans.map(s => `<div class="row sp"><span>${SANCTIONS[s.k].n}</span><span class="dim">${s.a === 0 ? 'вами' : 'ими'} · ${s.until > S.time + 30 * DAY ? 'бессрочно' : 'осталось ' + Math.ceil((s.until - S.time) / DAY) + ' дн.'}</span></div>`).join('');
  const btns = [];
  if (r.st === 'war') btns.push(btn('offer:truce:' + id, 'Предложить мир', 'gold'));
  else {
    if (ci && ci.ready) btns.push(btn('war:' + id, 'Объявить войну', 'gold'));
    else if (!ci && !r.ally) btns.push(btn('claim:' + id, 'Претензия'));
    if (!r.ally) btns.push(btn('warask:' + id, 'Напасть сразу'));
    if (!r.ally && r.st === 'peace') btns.push(btn('offer:ally:' + id, 'Союз'));
    if (r.ally) btns.push(btn('breakally:' + id, 'Разорвать союз'));
    if (r.st === 'peace' && !r.trade) btns.push(btn('offer:trade:' + id, 'Торговля'));
    if (r.trade) btns.push(btn('canceltrade:' + id, 'Расторгнуть торговлю'));
    if (!r.ally && !passLeft(S, 0, id)) btns.push(btn('offer:pass:' + id, 'Право прохода'));
    btns.push(btn('offer:ult:' + id, 'Ультиматум'));
  }
  btns.push(btn('sancpick:' + id, 'Санкции', 'gold'));
  showModal(IMG('globe', 22) + esc(q.name), b + '<div class="btns" style="margin-top:8px">' + btns.join('') + '</div>',
    btn('nations', 'К списку') + btn('closemodal', 'Закрыть'), { kind: 'nat' });
}
function showSanctionPick() {
  const S = UI.S; if (!S) return;
  showModal(IMG('t_san', 22) + 'Санкции', `<div class="dim">Выберите страну, против которой действовать</div><div class="list">` +
    S.players.filter(q => q.id !== 0 && q.alive).map(q => `<div class="it"><div class="g">${sw(q.col)} ${esc(q.name)}<div class="dim">${sanList(S, 0, q.id).length ? 'действует санкций: ' + sanList(S, 0, q.id).length : 'санкций нет'}</div></div>${btn('sancpick:' + q.id, 'Выбрать', 'gold')}</div>`).join('') + '</div>',
    btn('closemodal', 'Закрыть'), { kind: 'sanpick' });
}
const SAN_ICO = { transit: 'lock', embargo: 'cart', tariff: 'gold', arms: 'armH', food: 'food', gold: 'gold', ports: 'ship', ore: 'iron', mercs: 'mug', seize: 'gold', envoys: 'scroll', protest: 'scroll' };
function showSanctions(id) {
  const S = UI.S, q = S.players[id]; if (!S || !q) return;
  UI.natSel = id;
  const rows = SAN_KEYS.map(k => {
    const d = SANCTIONS[k], on = sanActive(S, 0, id, k), cd = sanCdLeft(S, 0, id, k);
    return `<div class="it"><div class="g">${IMG(SAN_ICO[k] || 'scroll', 22)}<b>${d.n}</b> <span class="dim">${d.cost ? d.cost + ' золота' : 'бесплатно'}</span><div class="dim">${d.d}</div>${on ? '<div class="gl">действует</div>' : cd > 0 ? '<div class="dim">повтор через ' + Math.max(1, Math.ceil(cd / DAY)) + ' дн.</div>' : ''}</div><div class="btns tiny">${on ? btn('unsanc:' + k + ':' + id, 'Снять') : btn('sanc:' + k + ':' + id, d.once ? 'Применить' : 'Ввести', d.once ? '' : 'gold')}</div></div>`;
  }).join('');
  showModal(IMG('t_san', 22) + 'Санкции: ' + esc(q.name), `<div class="dim">Санкции бьют по экономике и правам страны. Пока санкция не снята, её эффект действует.</div><div class="list">${rows}</div>`,
    btn('nat:' + id, 'К характеристикам') + btn('closemodal', 'Закрыть'), { kind: 'san' });
}
function showAttack() {
  const S = UI.S; if (!S) return;
  showModal(IMG('skull', 22) + 'Объявить войну', `<div class="dim">Выберите страну. Война объявляется сразу — армии смогут вступать на их землю.</div><div class="list">` +
    S.players.filter(q => q.id !== 0 && q.alive).map(q => { const r = rel(S, 0, q.id); return `<div class="it"><div class="g">${sw(q.col)} ${esc(q.name)}<div class="dim">мощь ${Math.round(milPower(S, q.id))} против вашей ${Math.round(milPower(S, 0))} · ${r.st === 'war' ? 'уже воюем' : r.ally ? 'союзник' : r.st === 'truce' ? 'перемирие' : 'мир'}</div></div>${r.st === 'war' || r.ally ? '' : btn('warask:' + q.id, 'Напасть', 'gold')}</div>`; }).join('') + '</div>',
    btn('closemodal', 'Закрыть'), { kind: 'war' });
}
const NAT_ACTS = { claim: 1, breakally: 1, war: 1, offer: 1, canceltrade: 1, doattack: 1 };
function showGameOver() {
  const S = UI.S, w = S.over.winner, won = w === 0, p = me(); dropSave(); checkAch(); const best = addRecord(S);
  SND.play(won ? 'win' : 'lose');
  showModal(won ? STAR() + ' Победа!' : 'Поражение', `<div class="${won ? 'gd' : 'bad'} big-res">${won ? 'Вы победили!' : (w >= 0 ? 'Победил ' + esc(S.players[w].name) : 'Игра окончена')}</div><div class="dim">${esc(S.over.why)} · ${gameClock(S.time)}</div><div style="margin-top:8px">Ваши очки: <b class="gl">${p.score}</b> (хозяйство ${p.eco}, война ${p.mil})</div>${best ? '<div class="gl">Новый рекорд!</div>' : ''}`,
    btn('tomenu', 'В главное меню', 'gold'), { kind: 'over', lock: true });
}
function showCreate() {
  const c = UI.create, seg = (k, opts, cls) => `<div class="seg ${cls || ''}">${opts.map(o => btn(`cset:${k}=${o[0]}`, o[1], c[k] === o[0] ? 'on' : '')).join('')}</div>`;
  $('menu').classList.add('hidden'); const el = $('create'); el.classList.remove('hidden');
  morph(el, `<div class="in"><h2 class="hd">Новая игра</h2>
  <div class="grp"><div class="lb">Тип карты</div>${seg('type', [['continent', 'Материк'], ['continents', 'Континенты'], ['islands', 'Острова'], ['lakes', 'Озёра']])}</div>
  <div class="grp"><div class="lb">Размер карты</div>${seg('size', [['s', 'Малая'], ['m', 'Средняя'], ['l', 'Большая']])}</div>
  <div class="grp"><div class="lb">Режим игры</div>${seg('sandbox', [[0, 'Обычный'], [1, 'Песочница']])}${c.sandbox ? '<div class="dim" style="margin-top:4px">Бесконечные ресурсы, без победы по очкам и без наград. Соперников можно не добавлять.</div>' : ''}</div>
  <div class="grp"><div class="lb">Число соперников</div>${seg('bots', (c.sandbox ? [[0, '0']] : []).concat([[1, '1'], [2, '2'], [3, '3'], [4, '4'], [5, '5'], [6, '6']]), 'num')}</div>
  <div class="grp"><div class="lb">Сложность соперников</div>${seg('diff', [['easy', 'Лёгкая'], ['normal', 'Средняя'], ['hard', 'Сложная'], ['mixed', 'Разная']])}</div>
  ${c.sandbox ? '' : `<div class="grp"><div class="lb">Конец игры</div>${seg('win', [['score', 'По очкам'], ['caps', 'Захват столиц']])}</div>`}
  ${!c.sandbox && c.win === 'score' ? `<div class="grp"><div class="lb">Очков для победы</div>${seg('target', [[10000, '10 000'], [20000, '20 000'], [30000, '30 000'], [50000, '50 000']])}</div>` : ''}
  <div class="grp"><div class="lb">Вольные города</div>${seg('free', [[0, 'Нет'], [1, 'Немного'], [2, 'Много']])}</div>
  ${c.sandbox ? '' : `<div class="grp"><div class="lb">Случайные события</div>${seg('events', [[1, 'Есть'], [0, 'Нет']])}</div>`}
  <div class="grp"><div class="lb">Ваша держава: <b>${PNAMES[c.nation]}</b></div><div class="seg nat">${PNAMES.map((n, i) => btn('cset:nation=' + i, sw(PCOL[i]), c.nation === i ? 'on' : '')).join('')}</div>
  <div class="note parch">${PERKS[c.nation].n}: ${PERKS[c.nation].d.toLowerCase()}.</div></div>
  <div class="go"><button class="px big gold" data-act="start">НАЧАТЬ ИГРУ</button><button class="px" data-act="back">НАЗАД</button></div></div>`);
}

/* ----- кнопка «Назад» (её вызывает оболочка приложения) -----
   true — нажатие обработано игрой; false — делать нечего, приложение можно закрывать. */
window.__kcBack = function () {
  try {
    if (!$('loading').classList.contains('hidden')) return true;
    if (UI.modal) { if (UI.modal.lock) ACT.tomenu(); else ACT.closemodal(); return true; }
    if (!$('create').classList.contains('hidden')) { ACT.back(); return true; }
    if (!UI.S) return false;
    if (RN.place || UI.sheet) { closeSheet(); return true; }
    if (!$('mm').classList.contains('hidden')) { ACT.mm(); return true; }
    showMenuModal(); return true;
  } catch (e) { return false; }
};
