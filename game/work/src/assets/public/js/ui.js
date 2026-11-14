'use strict';
/* ===== Интерфейс Kingdom Crash ===== */
const $ = id => document.getElementById(id);
const UI = { S: null, bpage: 0, tab: 'map', sheet: null, speed: 1, paused: false, toasts: [], lastHtml: '', inboxTab: 'all', create: { type: 'continent', size: 'm', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, sandbox: 0 }, last: 0, shownOver: false };
const me = () => UI.S.players[0];
const fmt = n => n >= 1e6 ? (n / 1e6).toFixed(1) + 'M' : n >= 1e4 ? Math.round(n / 1e3) + 'K' : String(Math.floor(n));
const clock = t => { const m = Math.floor(t / 60), s = Math.floor(t % 60); return String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0'); };
const esc = s => String(s).replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));
const btn = (act, label, cls) => `<button class="px ${cls || ''}" data-act="${act}">${label}</button>`;
const sw = col => `<span class="sw" style="background:${col}"></span>`;

/* ----- нижняя панель ----- */
const TABS = [['map', 'КАРТА', 't_map'], ['city', 'ГОРОДА', 't_city'], ['build', 'СТРОЙКА', 't_build'], ['army', 'АРМИЯ', 't_army'], ['pol', 'ПОЛИТ.', 't_pol'], ['fleet', 'ФЛОТ', 't_fleet']];
const SHORT = { academy: 'АКАДЕМИЯ', siege: 'ОСАДНАЯ', wall: 'СТЕНА', tower: 'БАШНЯ', gate: 'ВОРОТА', moat: 'РОВ', hospital: 'ГОСПИТ.', farm: 'ФЕРМА', sawmill: 'ЛЕСОПИЛ.', mine: 'РУДНИК', barracks: 'КАЗАРМЫ', forge: 'КУЗНЯ', workshop: 'МАСТЕРСК.', stable: 'КОНЮШНЯ', market: 'РЫНОК', port: 'ПОРТ' };
const BAR = {
  map: [['center', 'ЦЕНТР', 'home'], ['mm', 'КАРТА', 't_map'], ['borders', 'ГРАНИЦЫ', 'flag'], ['stock', 'СКЛАД', 'iron'], ['save', 'СОХРАН.', 'scroll'], ['plan', 'ПЛАН', 'scroll'], ['rate', 'РЕЙТИНГ', 'trophy'], ['menu', 'МЕНЮ', 'menu']],
  city: [['cities', 'ГОРОДА', 't_city'], ['place:city', 'НОВЫЙ', 'flag'], ['upg', 'УЛУЧШ.', 't_build'], ['center', 'СТОЛИЦА', 'home']],
  build: [],
  army: [['barr', 'КАЗАРМЫ', 'B:barracks'], ['armies', 'АРМИИ', 't_army'], ['merge', 'ОБЪЕД.', 'merge'], ['split', 'РАЗДЕЛ.', 'split'], ['stop', 'СТОЯТЬ', 'stop'], ['retreat', 'ОТСТУП.', 'home'], ['stock', 'СКЛАД', 'armH']],
  pol: [['dip', 'ДИПЛОМАТ.', 'globe'], ['trade', 'ТОРГОВЛЯ', 'cart'], ['eco', 'ЭКОНОМИКА', 'chart'], ['ult', 'УЛЬТИМАТ.', 'scroll'], ['rate', 'РЕЙТИНГ', 'trophy']],
  fleet: [['ports', 'ПОРТЫ', 'B:port'], ['ships', 'КОРАБЛИ', 'ship'], ['stopship', 'СТОЯТЬ', 'stop'], ['unload', 'ВЫСАДКА', 'flag']]
};
const bIcon = {};
function bldIcon(type, size) { const k = type + size; if (!bIcon[k]) { const c = document.createElement('canvas'); c.width = 32; c.height = 32; if (type === 'moat') drawMoat(c.getContext('2d'), 0, 0, 5, true); else DRAW[type](c.getContext('2d'), 0, 0, '#d93a3a', 1); bIcon[k] = c.toDataURL(); } return `<img class="ic" src="${bIcon[k]}" style="width:${size}px;height:${size}px">`; }
function icoHtml(name, s) { return name.startsWith('B:') ? bldIcon(name.slice(2), s) : IMG(name, s); }
function renderTabs() {
  $('tabs').innerHTML = TABS.map(t => `<button class="px ${UI.tab === t[0] ? 'on' : ''}" data-act="tab:${t[0]}">${IMG(t[2], 24)}<span>${t[1]}</span></button>`).join('');
  const bs = t => ['place:' + t, SHORT[t], 'B:' + t], sl = (UI.tab === 'build' ? (UI.bpage === 1 ? ['wall', 'tower', 'gate', 'moat', 'market', 'port', 'academy'].map(bs).concat([['bpage:0', 'ПРОИЗВ.', 'B:farm']]) : ['farm', 'sawmill', 'mine', 'barracks', 'hospital', 'forge', 'workshop', 'stable', 'siege'].map(bs).concat([['bpage:1', 'УКРЕПЛ.', 'B:tower']])) : (BAR[UI.tab] || [])).slice(); while (sl.length < 10) sl.push(null);
  const fixed = [`<button data-act="pause" class="${UI.paused ? 'on' : ''}"><div class="bx">${IMG('pause', 32)}</div>ПАУЗА</button>`, `<button data-act="speed"><div class="bx">${IMG('hour', 32)}<b style="position:absolute;margin:20px 0 0 22px;color:#ffe27a">x${UI.speed}</b></div>СКОРОСТЬ</button>`];
  const cells = []; // колонка 0: пауза/скорость, дальше 5 колонок по 2 ряда
  for (let r = 0; r < 2; r++) { cells.push(fixed[r]); for (let c = 0; c < 5; c++) { const s = sl[r * 5 + c]; cells.push(s ? `<button data-act="${s[0]}" id="bb_${s[0].replace(':', '_')}"><div class="bx">${icoHtml(s[2], 32)}</div>${s[1]}</button>` : '<div></div>'); } }
  $('bar').innerHTML = cells.join('');
}

/* ----- HUD ----- */
function updateHud() {
  const S = UI.S, p = me(); $('flag').style.background = p.col; $('nation').textContent = p.name;
  $('score').innerHTML = IMG('medal', 22) + p.score; { const hh = todHours(S), h = Math.floor(hh), m = Math.floor((hh - h) * 60); $('clock').textContent = 'Д' + (Math.floor(S.time / DAY) + 1) + ' ' + String(h).padStart(2, '0') + ':' + String(m).padStart(2, '0'); }
  const rt = k => { const v = p.rate[k]; return `<span class="${v < 0 ? 'neg' : 'pos'}" style="font-size:11px">${v >= 0 ? '+' : ''}${v.toFixed(1)}</span>`; };
  const sb = !!S.opts.sandbox; let h = ''; for (const k of ['gold', 'wood', 'food', 'iron']) h += `<div class="r">${IMG(k, 18)}<span class="${!sb && p.stock[k] < 1 && k === 'food' ? 'neg' : ''}">${sb ? '∞' : fmt(p.stock[k])}</span>${sb ? '' : rt(k)}</div>`;
  for (const k of ['bow', 'sword', 'armL', 'armH', 'horse']) h += `<div class="r">${IMG(k, 18)}${sb ? '∞' : Math.floor(p.stock[k])}</div>`;
  if ($('resbar').dataset.h !== h) { $('resbar').innerHTML = h; $('resbar').dataset.h = h; }
  const un = S.msgs.filter(m => !m.read).length; $('badge').textContent = un; $('badge').classList.toggle('hidden', !un);
  $('inboxIc').innerHTML = IMG('mail', 28); $('inboxBtn').classList.toggle('alert', un > 0);
  // тосты
  for (const t of S.toast.splice(0)) UI.toasts.push({ t, rt: performance.now() });
  const now = performance.now(); UI.toasts = UI.toasts.filter(q => now - q.rt < 3000).slice(-2);
  const th = UI.toasts.map(q => `<div class="toast">${IMG('flag', 28)}<div><div class="tt">${esc(q.t.title)}</div><div class="ts">${esc(q.t.text)}</div></div><div class="bar"><i style="width:${100 - (now - q.rt) / 30}%"></i></div></div>`).join('');
  if ($('toasts').dataset.h !== th) { $('toasts').innerHTML = th; $('toasts').dataset.h = th; }
  if (S.prompts && S.prompts.length && $('modal').classList.contains('hidden') && UI.S && !S.over) {
    const pr = S.prompts[0], a = S.armies.find(q => q.id === pr.id);
    if (!a) S.prompts.shift(); else { let w = 0; for (const q of a.sq) w += q.w || 0; const hosp = nearestHospital(S, a); centerOn(a.x, a.y); RN.sel = { k: 'army', id: a.id };
      showModal('Тяжёлые потери', `<div class="bad" style="font-size:18px">Армия несёт большие потери!</div><div style="margin-top:6px">Осталось <b>${units(a)}</b> из ${a.pk || a.maxU} бойцов.${w ? ' Раненых, которых можно вернуть: <b>' + w + '</b>.' : ''}</div><div class="dim" style="margin-top:6px">${hosp ? 'Отступите к госпиталю: армию вылечат, а раненых вернут в строй. Отступающие не отвечают на удары.' : 'Госпиталей нет — отступить к ближайшему городу? Постройте госпиталь, чтобы восстанавливать армии.'}</div>`, btn('retreatyes:' + a.id, 'Отступить', 'gold') + btn('retreatno:' + a.id, 'Продолжить бой')); }
  }
  const pl = planState(); $('plan').innerHTML = pl.done ? 'ПЛАН ВЫПОЛНЕН ★' : `ПЛАН РАЗВИТИЯ ${pl.i + 1} из ${PLAN.length} ▶`;
  $('plan').style.top = `calc(${UI.toasts.length ? 122 + 56 * UI.toasts.length : 190}px + var(--sat))`;
  if (RN.minimap && !$('mm').classList.contains('hidden')) { UI.mm = (UI.mm || 0) + 1; if (UI.mm % 4 === 0) drawMinimap(S); }
}
const PLAN = [
  ['Постройте ферму на лугу (вкладка СТРОЙКА)', p => p.flags.b_farm], ['Постройте лесопилку в лесу', p => p.flags.b_sawmill], ['Постройте рудник на залежи в горах', p => p.flags.b_mine],
  ['Постройте казармы', p => p.flags.b_barracks], ['Постройте мастерскую — она делает луки', p => p.flags.b_workshop], ['Постройте кузню — мечи и доспехи', p => p.flags.b_forge],
  ['Постройте конюшню — для конницы', p => p.flags.b_stable], ['Постройте рынок', p => p.flags.b_market], ['Обучите отряд в казармах (ВКЛАДКА АРМИЯ → КАЗАРМЫ)', p => p.flags.squad],
  ['Улучшите город', p => p.flags.upg], ['Основайте второй город (ГОРОДА → НОВЫЙ)', (p, S) => S.cities.filter(c => c.owner === 0).length >= 2], ['Постройте порт на берегу', p => p.flags.b_port],
  ['Постройте торговое судно в порту', p => p.flags.ship], ['Заключите торговое соглашение (ПОЛИТ. → ТОРГОВЛЯ)', p => p.flags.trade], ['Объедините отряды в армию', p => p.flags.army], ['Захватите любой город — подведите армию', p => p.flags.capture]
];
function planState() { const S = UI.S, p = me(); for (let i = 0; i < PLAN.length; i++) if (!PLAN[i][1](p, S)) return { i, done: false }; return { i: PLAN.length, done: true }; }

/* ----- панель (sheet) ----- */
function setSheet(s) { UI.sheet = s; UI.sheetMin = false; UI.lastHtml = ''; renderSheet(true); }
function closeSheet() { UI.sheet = null; if (RN.place) RN.place = null; RN.sel = null; renderSheet(true); }
function renderSheet(force) {
  const sh = UI.sheet, el = $('sheet'); if (!sh) { el.classList.add('hidden'); return; }
  const r = sheetHtml(sh); if (!r) { UI.sheet = null; el.classList.add('hidden'); return; }
  const html = `<div class="hd"><div class="tt">${r.title}</div>${btn('minsheet', UI.sheetMin ? '▴' : '▾', 'x')}${btn('closesheet', '✕', 'x')}</div><div class="bd">${r.body}</div>`;
  if (html === UI.lastHtml && !force) return; UI.lastHtml = html;
  const old = el.querySelector('.bd'); const st = old ? old.scrollTop : 0; el.innerHTML = html; el.classList.remove('hidden'); el.classList.toggle('min', !!UI.sheetMin); const nb = el.querySelector('.bd'); if (nb) nb.scrollTop = st;
}
function needTxt(c, p) { return Object.keys(c).map(k => `<span class="${p.stock[k] >= c[k] ? '' : 'bad'}">${IMG(k, 16)}${c[k]}</span>`).join(' '); }
function sheetHtml(sh) {
  const S = UI.S, p = me();
  switch (sh.k) {
    case 'city': {
      const c = S.cities[sh.id]; if (!c) return null; const mine = c.owner === 0, ap = Math.max(0, S.cities.length);
      let b = `<div class="row">${sw(ownerCol(S, c.owner))} ${pname(S, c.owner)} · уровень ${c.level}</div><div class="dim">Гарнизон: ${garrison(c)} · граница: ${c.radius.toFixed(1)} из ${cityMaxR(c)} клеток</div>`;
      if (c.cap) b += `<div class="${c.cap.by === 0 ? 'gd' : 'bad'}">Захват: ${pname(S, c.cap.by)} ${Math.floor(c.cap.p * 100)}%${c.cap.p >= 1 ? ' — удержание…' : ''}</div><div class="pb"><i style="width:${c.cap.p * 100}%"></i></div>`;
      if (mine) { b += `<div class="dim">Доход: +${(0.6 + 0.45 * c.level).toFixed(1)} золота/с</div>`; b += c.level < 5 ? `<div class="row">Улучшение: ${needTxt(upgradeCost(c), p)}</div><div class="btns">${btn('upgrade:' + c.id, 'Улучшить город', 'gold')}</div>` : '<div class="gl">Максимальный уровень</div>'; }
      else b += `<div class="dim">${c.owner < 0 ? 'Вольный город — его можно захватить без войны.' : atWar(S, 0, c.owner) ? 'Вы воюете. Подведите армию, чтобы начать захват.' : 'Объявите войну, чтобы напасть.'}</div>`;
      return { title: (c.isCap ? '★ ' : '') + esc(c.name), body: b };
    }
    case 'bld': {
      const b0 = S.buildings.find(q => q.id === sh.id); if (!b0) return null; const d = BD[b0.type], mine = b0.owner === 0; let b = `<div class="row">${bldIcon(b0.type, 32)} <div class="dim">${d.d}</div></div>`;
      if (!mine) return { title: d.n, body: b + `<div class="dim">Владелец: ${pname(S, b0.owner)}</div>` };
      if (b0.type === 'farm') b += '<div class="gd">+0.7 еды/с</div>';
      if (b0.type === 'sawmill') b += '<div class="gd">Дерево: чем гуще лес вокруг, тем выше добыча.</div>';
      if (b0.type === 'mine') b += `<div class="gd">${b0.res === 2 ? 'Золотой рудник +0.45 золота/с' : 'Железный рудник +0.45 железа/с'}</div>`;
      if (b0.type === 'market') b += '<div class="gd">+0.7 золота/с</div>';
      const rc = RECIPES[b0.type];
      if (rc) {
        b += '<div class="dim">Производит предметы для отрядов. Выберите, что делать:</div><div class="btns">';
        for (const k in rc) { const r = rc[k]; b += btn(`recipe:${b0.id}:${k}`, `${IMG(r.out, 18)} ${RNAME[r.out]}`, b0.recipe === k ? 'on' : ''); }
        const r = rc[b0.recipe]; b += `</div><div class="row">Цена: ${needTxt(r.in, p)} · ${r.t}с · на складе: ${Math.floor(p.stock[r.out])}</div>`;
        b += b0.busy ? `<div class="gl">Производится: ${RNAME[r.out]} — ${Math.floor(100 * b0.pt / r.t)}% (ещё ${Math.ceil(r.t - b0.pt)}с)</div><div class="pb"><i style="width:${100 * b0.pt / r.t}%"></i></div>` : `<div class="bad">${b0.status || 'Ожидание ресурсов'}</div>`;
      }
      if (STRUCT[b0.type]) { b += `<div class="row">Прочность: ${Math.max(0, Math.floor(b0.hp))} из ${b0.mhp}</div><div class="pb"><i style="width:${100 * Math.max(0, b0.hp) / b0.mhp}%"></i></div>`; }
      if (b0.type === 'gate') b += '<div class="dim">Свои войска и союзники проходят, враги — нет, пока ворота не сломают.</div>';
      if (b0.type === 'tower') {
        if (b0.gar && b0.gar.s.length) { const hpS = b0.gar.s.reduce((x, y) => x + y.hp, 0) / (b0.gar.s.length * SQ.archer.hp); b += `<div class="gd">В башне лучники: ${b0.gar.s.length} из 10 · здоровье ${Math.round(hpS * 100)}%. Стреляют дальше и получают на 40% меньше урона.</div><div class="btns">${btn('ungar:' + b0.id, 'Вывести отряд')}</div>`; }
        else b += '<div class="dim">Башня пуста. Выберите армию с лучниками и нажмите на башню — отряд лучников займёт её.</div>';
      }
      if (b0.type === 'moat') b += b0.wet ? '<div class="gd">Ров залит водой: вражеские бойцы в нём слабеют на 3–7% и медленнее идут.</div>' : '<div class="bad">Ров не соединён с водоёмом — воды в нём нет.</div>';
      if (b0.type === 'academy') {
        const hc = hasCommander(S, 0), q0 = b0.queue[0];
        b += '<div class="dim">Обучает одного полководца (только одного за раз). Он усиливает войска своего вида рядом с собой: +25% урона и на 20% меньше получаемого урона в радиусе 4,5 клетки.</div>';
        if (q0) { const pr = Math.max(0, Math.min(1, 1 - q0.left / (q0.total || 60))); b += `<div class="gl">Обучение: ${CMD[q0.cls].n} — ${Math.floor(pr * 100)}% (ещё ${Math.ceil(q0.left)}с)</div><div class="pb"><i style="width:${pr * 100}%"></i></div>`; }
        else if (hc && hc !== true) { const q = hc.sq.find(z => z.t === 'cmd'); b += `<div class="gd">Полководец: ${CMD[q.cls].n} (${CMD[q.cls].of})</div><div class="btns">${btn('selarmy:' + hc.id, 'Найти армию')}</div>`; }
        else { const cost = Object.assign({ gold: SQ.cmd.gold, food: SQ.cmd.food }, SQ.cmd.need); b += '<div class="row">Цена: ' + needTxt(cost, p) + ' · ' + SQ.cmd.t + 'с</div><div class="list">'; for (const k in CMD) b += `<div class="it">${IMG(CMD[k].ico, 26)}<div class="g">${CMD[k].n}<div class="dim">ведёт ${CMD[k].of}</div></div>${btn('cmd:' + b0.id + ':' + k, 'Обучить')}</div>`; b += '</div>'; }
      }
      if (b0.type === 'hospital') {
        let near = 0, wnd = 0; for (const a of S.armies) if (a.owner === 0 && !a.ship && dist(a.x, a.y, b0.x + 0.5, b0.y + 0.5) <= 3.5) { near++; for (const q of a.sq) wnd += q.w || 0; }
        b += `<div class="gd">Лечит армии рядом (радиус 3,5 клетки): +2 здоровья/с. Возвращает раненых бойцов в строй — по одному раз в 5 с (8 золота и 4 еды за бойца).</div><div class="row">Армий рядом: ${near} · раненых ждёт возвращения: ${wnd}</div>${b0.treated ? '<div class="gl">Идёт лечение…</div>' : ''}`;
      }
      if (b0.type === 'barracks' || b0.type === 'siege') {
        b += b0.type === 'siege' ? '<div class="dim">Осадные машины делаются по одной. Выберите машину:</div>' : '<div class="dim">Отряд = 10 воинов. Выберите отряд для обучения:</div>';
        for (const t in SQ) { if (SQ[t].bld !== b0.type) continue; const q = SQ[t], cost = Object.assign({ gold: q.gold, food: q.food }, q.need); b += `<div class="list"><div class="it">${IMG(q.ico, 28)}<div class="g">${q.n}<div class="dim">${needTxt(cost, p)} · ${q.t}с</div></div>${btn(`train:${b0.id}:${t}`, 'Обучить')}</div></div>`; }
        if (b0.queue.length) { const q0 = b0.queue[0], T0 = q0.total || SQ[q0.t].t, pr = Math.max(0, Math.min(1, 1 - q0.left / T0)); b += `<div class="gl">${(SQ[q0.t].cnt || 10) === 1 ? 'Сборка машины: ' + SQ[q0.t].n + ' — ' + Math.floor(pr * 100) + '% (выкатится, когда будет готова, ещё ' + Math.ceil(q0.left) + 'с)' : 'Обучение: ' + SQ[q0.t].n + ' — вышло ' + Math.min(SQ[q0.t].cnt || 10, Math.floor(pr * (SQ[q0.t].cnt || 10))) + ' из ' + (SQ[q0.t].cnt || 10) + ' (ещё ' + Math.ceil(q0.left) + 'с)'}</div><div class="pb"><i style="width:${pr * 100}%"></i></div>`; if (b0.queue.length > 1) b += '<div class="dim">В очереди: ' + b0.queue.slice(1).map(q => SQ[q.t].n).join(', ') + '</div>'; } else b += '<div class="dim">Очередь пуста</div>';
      }
      if (b0.type === 'port') {
        b += `<div class="btns">${btn('ship:' + b0.id + ':trade', `${IMG('ship', 18)} Торговое судно`)}${btn('ship:' + b0.id + ':war', `${IMG('ship', 18)} Военное судно`)}</div><div class="dim">Торговое: ${needTxt(shipCost('trade'), p)}<br>Военное (перевозит войска): ${needTxt(shipCost('war'), p)}</div>`;
        b += '<div class="dim">Чтобы погрузить армию, выберите её и нажмите на порт. Затем выберите корабль и укажите берег.</div>';
      }
      b += `<div class="btns">${btn('demo1:' + b0.id, 'Снести')}</div>`; return { title: d.n, body: b };
    }
    case 'army': {
      const a = S.armies.find(q => q.id === sh.id); if (!a) return null; const mine = a.owner === 0 || allied(S, 0, a.owner);
      let b = `<div class="row chips">${sw(ownerCol(S, a.owner))}<span>${units(a)} воинов</span>` + a.sq.map(q => `<span class="chip">${IMG(SQ[q.t].ico, 18)}${q.u}<i style="width:${Math.round(100 * q.hp / (sqMax(q.t) * SQ[q.t].hp))}%"></i></span>`).join('') + `</div>`;
      { let w = 0; for (const q of a.sq) w += q.w || 0; if (w) b += `<div class="dim">Раненых можно вернуть в госпитале: ${w}</div>`; }
      { const cq = a.sq.find(z => z.t === 'cmd'); if (cq) b += `<div class="gl">${CMD[cq.cls].n} (${CMD[cq.cls].of}): +25% урона и −20% получаемого урона войскам этого вида в радиусе 4,5 клетки</div>`; }
      b += `<div class="dim">${a.sg ? 'Осада укрепления' : a.fight ? 'В бою!' : a.ship ? 'На корабле' : a.st === 'move' ? 'В походе' : 'Ожидает'}${combo(a) > 1 ? ' · бонус рода войск +' + Math.round((combo(a) - 1) * 100) + '%' : ''}</div>`;
      if (a.owner === 0 && !a.ship) b += `<div class="btns tiny">${btn('merge', IMG('merge', 16) + ' Объед.')}${btn('split', IMG('split', 16) + ' Разд.')}${btn('stop', 'Стоять')}${btn('retreat', 'Отступ.')}</div>`;
      return { title: a.owner === 0 ? 'Армия' : mine ? 'Армия союзника' : 'Чужая армия', body: b };
    }
    case 'ship': {
      const s = S.ships.find(q => q.id === sh.id); if (!s) return null; let b = `<div class="dim">${s.kind === 'war' ? 'Военное судно: перевозит войска, в боях не участвует.' : 'Торговое судно: возит товары между портами партнёров.'}</div>`;
      b += `<div class="row">${s.cargo ? 'Груз: армия на борту' : 'Трюм пуст'} · ${s.path.length ? 'в пути' : 'стоит'}</div>`;
      if (s.kind === 'war') b += `<div class="btns">${btn('unload', 'Высадить')}${btn('stopship', 'Стоять')}</div><div class="dim">Нажмите на воду или на берег, чтобы отправить корабль. Войска высадятся на указанный берег.</div>`; else b += `<div class="dim">Корабль плавает сам, если есть торговое соглашение с владельцем порта.</div>`;
      return { title: s.kind === 'war' ? 'Военный корабль' : 'Торговый корабль', body: b };
    }
    case 'place': {
      const pl = RN.place; if (!pl) return null; const isC = pl.type === 'city', line = (pl.type === 'wall' || pl.type === 'moat'), name = isC ? 'Новый город' : BD[pl.type].n;
      const tiles = pl.x != null ? (line && pl.x2 != null ? lineTiles(pl.x, pl.y, pl.x2, pl.y2) : [{ x: pl.x, y: pl.y }]) : [], one = isC ? cityCost(S, 0) : BD[pl.type].cost, cost = {}; for (const k in one) cost[k] = one[k] * Math.max(1, tiles.length);
      let msg = line ? 'Нажмите на карту: первая точка, затем вторая — построится линия' : 'Нажмите на карту — выберите место', ok = false, cls = 'dim';
      if (pl.x != null) { let good = 0, e0 = null; for (const t of tiles) { const e = siteOk(S, 0, pl.type, t.x, t.y); if (!e) good++; else e0 = e0 || e; } if (!good) { msg = e0; cls = 'bad'; } else if (!canAfford(p, cost)) { msg = 'Не хватает ресурсов'; cls = 'bad'; } else { msg = line && tiles.length > 1 ? `Подходит клеток: ${good} из ${tiles.length}` : 'Место подходит'; cls = 'gd'; ok = true; } if (line && pl.x2 == null && ok) msg += ' · нажмите вторую точку для линии'; }
      let extra = ''; if (isC && pl.x != null) { const cl = cityClaim(S, 0, pl.x, pl.y); extra = `<div class="dim">Территория вокруг: суши ${cl.land} клеток, свободной ${cl.free}${cl.forest ? '; лес под городом вырубят' : ''}. Чем больше свободной земли, тем лучше место.</div>`; }
      return { title: name, body: `<div class="row">${needTxt(cost, p)}</div><div class="${cls}">${msg}</div>${extra}<div class="btns">${btn('confirmplace', 'Построить', ok ? 'gold' : 'dis')}${btn('cancelplace', 'Отмена')}</div>` };
    }
    case 'list': return listHtml(sh);
  }
  return null;
}
function listHtml(sh) {
  const S = UI.S, p = me(); let b = '', title = '';
  switch (sh.what) {
    case 'cities': title = 'Города'; b = '<div class="list">' + S.cities.filter(c => c.owner === 0).map(c => `<div class="it">${IMG('t_city', 24)}<div class="g">${c.isCap ? '★ ' : ''}${esc(c.name)}<div class="dim">ур. ${c.level} · граница ${c.radius.toFixed(0)}</div></div>${btn('selcity:' + c.id, 'Выбрать')}</div>`).join('') + '</div>'; break;
    case 'armies': title = 'Армии'; { const l = S.armies.filter(a => a.owner === 0 && !a.ship); b = l.length ? '<div class="list">' + l.map(a => `<div class="it">${IMG(SQ[a.sq[0].t].ico, 24)}<div class="g">Отрядов: ${a.sq.length} · воинов ${units(a)}<div class="dim">${a.fight ? 'в бою' : a.st === 'move' ? 'в походе' : 'ожидает'}</div></div>${btn('selarmy:' + a.id, 'Выбрать')}</div>`).join('') + '</div>' : '<div class="dim">Армий нет. Обучите отряд в казармах.</div>'; } break;
    case 'barracks': title = 'Казармы'; { const l = S.buildings.filter(x => x.owner === 0 && x.type === 'barracks'); b = l.length ? '<div class="list">' + l.map(x => `<div class="it">${bldIcon('barracks', 24)}<div class="g">Казармы<div class="dim">очередь: ${x.queue.length}</div></div>${btn('selbld:' + x.id, 'Открыть')}</div>`).join('') + '</div>' : '<div class="dim">Постройте казармы во вкладке СТРОЙКА.</div>'; } break;
    case 'ports': title = 'Порты'; { const l = S.buildings.filter(x => x.owner === 0 && x.type === 'port'); b = l.length ? '<div class="list">' + l.map(x => `<div class="it">${bldIcon('port', 24)}<div class="g">Порт</div>${btn('selbld:' + x.id, 'Открыть')}</div>`).join('') + '</div>' : '<div class="dim">Портов нет. Порт строится на берегу.</div>'; } break;
    case 'ships': title = 'Корабли'; { const l = S.ships.filter(x => x.owner === 0); b = l.length ? '<div class="list">' + l.map(x => `<div class="it">${IMG('ship', 24)}<div class="g">${x.kind === 'war' ? 'Военный' : 'Торговый'}<div class="dim">${x.cargo ? 'с войсками' : x.path.length ? 'в пути' : 'стоит'}</div></div>${btn('selship:' + x.id, 'Выбрать')}</div>`).join('') + '</div>' : '<div class="dim">Кораблей нет. Постройте их в порту.</div>'; } break;
    case 'stock': title = 'Склад'; b = STOCK_KEYS.map(k => `<div class="row sp"><span>${IMG(k, 22)} ${RNAME[k]}</span><b>${Math.floor(p.stock[k])}</b></div>`).join(''); break;
    case 'dip': title = 'Дипломатия'; b = '<div class="list">' + S.players.filter(q => q.id !== 0).map(q => {
      const r = rel(S, 0, q.id), ci = claimInfo(S, 0, q.id), theirs = claimInfo(S, q.id, 0);
      const st = r.st === 'war' ? '<span class="bad">ВОЙНА</span>' : r.ally ? '<span class="gd">союз</span>' : r.st === 'truce' ? '<span class="gl">перемирие</span>' : '<span class="gd">мир</span>';
      let btns = '';
      if (q.alive) {
        if (r.st === 'war') btns += btn('offer:truce:' + q.id, 'Мир');
        else {
          if (ci && ci.ready) btns += btn('war:' + q.id, 'Объявить войну', 'gold'); else if (!ci && !r.ally) btns += btn('claim:' + q.id, 'Претензия');
          if (r.ally) btns += btn('breakally:' + q.id, 'Разорвать союз'); else if (r.st === 'peace') btns += btn('offer:ally:' + q.id, 'Союз');
          if (!r.ally && r.st !== 'war' && !passLeft(S, 0, q.id)) btns += btn('offer:pass:' + q.id, 'Право прохода');
        }
      }
      return `<div class="it"><div class="g">${sw(q.col)} ${q.name}${q.alive ? '' : ' (пал)'}<div class="dim">${st} · отношение ${r.op > 20 ? 'тёплое' : r.op < -20 ? 'холодное' : 'нейтральное'}${r.trade ? ' · торговля' : ''}</div>${ci ? `<div class="gl">Претензия: ${ci.ready ? 'готова — можно объявить войну' : ci.day + ' из 3 дней'}</div>` : ''}${passLeft(S, 0, q.id) ? `<div class="gd">Ваши войска могут проходить ещё ${Math.ceil(passLeft(S, 0, q.id) / DAY)} дн.</div>` : ''}${passLeft(S, q.id, 0) ? `<div class="dim">Они могут проходить по вашей земле ещё ${Math.ceil(passLeft(S, q.id, 0) / DAY)} дн.</div>` : ''}${theirs ? '<div class="bad">Готовит претензии к вам: ' + (theirs.ready ? 'война возможна!' : theirs.day + ' из 3 дней') + '</div>' : ''}<div class="btns tiny">${btns}</div></div></div>`;
    }).join('') + '</div><div class="dim">Армии не могут входить на чужую территорию без войны, союза или права прохода. Войну можно объявить только через 3 дня после предъявления претензии.</div>'; break;
    case 'trade': title = 'Торговля'; b = '<div class="list">' + S.players.filter(q => q.id !== 0 && q.alive).map(q => { const r = rel(S, 0, q.id); return `<div class="it"><div class="g">${sw(q.col)} ${q.name}<div class="dim">${r.trade ? '<span class="gd">соглашение действует</span>' : r.st === 'war' ? 'идёт война' : 'нет соглашения'}</div></div>${r.trade ? btn('canceltrade:' + q.id, 'Расторгнуть') : r.st === 'peace' ? btn('offer:trade:' + q.id, 'Предложить') : ''}</div>`; }).join('') + '</div><div class="dim">Соглашение даёт золото обеим сторонам. Торговые суда из портов привозят ещё больше.</div>'; break;
    case 'eco': title = 'Экономика'; { const rr = p.rate; const bc = {}; S.buildings.forEach(x => { if (x.owner === 0) bc[x.type] = (bc[x.type] || 0) + 1; }); b = ['gold', 'wood', 'food', 'iron'].map(k => `<div class="row sp"><span>${IMG(k, 22)} ${RNAME[k]}</span><span>${fmt(p.stock[k])} <span class="${rr[k] < 0 ? 'bad' : 'gd'}">${rr[k] >= 0 ? '+' : ''}${rr[k].toFixed(2)}/с</span></span></div>`).join('') + '<div class="gl" style="margin-top:6px">Здания</div>' + (Object.keys(bc).map(k => `<div class="row sp"><span>${BD[k].n}</span><b>${bc[k]}</b></div>`).join('') || '<div class="dim">пока нет</div>') + `<div class="dim" style="margin-top:6px">Отряды едят еду и стоят золота. Без еды армия слабеет.</div>`; } break;
    case 'ult': title = 'Ультиматум'; b = '<div class="dim">Потребуйте 150 золота. Слабый соперник заплатит, сильный ответит войной.</div><div class="list">' + S.players.filter(q => q.id !== 0 && q.alive).map(q => `<div class="it"><div class="g">${sw(q.col)} ${q.name}<div class="dim">мощь ${Math.round(milPower(S, q.id))} против вашей ${Math.round(milPower(S, 0))}</div></div>${rel(S, 0, q.id).st === 'war' ? '' : btn('offer:ult:' + q.id, 'Потребовать')}</div>`).join('') + '</div>'; break;
    case 'rate': title = 'Рейтинг'; { const tg = S.opts.win === 'score' ? S.opts.target : 0; b = (tg ? `<div class="dim">Победа: первый, кто наберёт ${tg} очков</div>` : '<div class="dim">Победа: захват всех столиц</div>') + '<div class="list">' + S.players.slice().sort((x, y) => y.score - x.score).map((q, i) => `<div class="it"><b>${i + 1}</b>${sw(q.col)}<div class="g">${q.name}${q.alive ? '' : ' (пал)'}<div class="dim">хозяйство ${q.eco} · война ${q.mil}</div>${tg ? `<div class="pb"><i style="width:${Math.min(100, 100 * q.score / tg)}%"></i></div>` : ''}</div><b class="gl">${q.score}</b></div>`).join('') + '</div>'; } break;
    case 'plan': title = 'План развития'; { const ps = planState(); b = PLAN.map((q, i) => `<div class="row ${i < ps.i || ps.done ? 'dim' : i === ps.i ? 'gl' : ''}">${i < ps.i || ps.done ? '✔' : i === ps.i ? '▶' : '•'} ${q[0]}</div>`).join(''); } break;
  }
  return { title, body: b };
}
function openList(what) { RN.sel = null; if (RN.place) RN.place = null; setSheet({ k: 'list', what }); }
setInterval(() => { if (UI.S && UI.sheet) renderSheet(); }, 300);

/* ----- действия ----- */
function say(t, ok) { UI.S.toast.push({ title: ok ? 'Готово' : 'Нельзя', text: t, t: UI.S.time }); }
function selEntity(k, id) { RN.sel = { k, id }; setSheet({ k, id }); }
function selArmy() { const s = RN.sel; if (s && s.k === 'army') { const a = UI.S.armies.find(q => q.id === s.id); if (a && a.owner === 0) return a; } return null; }
const ACT = {
  tab(t) { UI.tab = t; renderTabs(); },
  pause() { UI.paused = !UI.paused; renderTabs(); },
  speed() { UI.speed = UI.speed === 1 ? 2 : UI.speed === 2 ? 4 : 1; renderTabs(); },
  closesheet() { closeSheet(); },
  bpage(n) { UI.bpage = +n; renderTabs(); },
  ungar(id) { const b = UI.S.buildings.find(q => q.id === +id); if (b) { ungarrison(UI.S, b); say('Отряд покинул башню', true); renderSheet(true); } },
  minsheet() { UI.sheetMin = !UI.sheetMin; renderSheet(true); },
  center() { const c = UI.S.cities.find(c => c.owner === 0); if (c) centerOn(c.x + 1, c.y + 1); },
  mm() { $('mm').classList.toggle('hidden'); RN.minimap = $('mm'); drawMinimap(UI.S); },
  borders() { RN.showTerr = !RN.showTerr; },
  stock() { openList('stock'); }, save() { say(saveGame(UI.S) ? 'Игра сохранена' : 'Не удалось сохранить', true); }, plan() { openList('plan'); }, rate() { openList('rate'); },
  menu() { showMenuModal(); }, cities() { openList('cities'); }, armies() { openList('armies'); }, barr() { openList('barracks'); }, ports() { openList('ports'); }, ships() { openList('ships'); },
  dip() { openList('dip'); }, trade() { openList('trade'); }, eco() { openList('eco'); }, ult() { openList('ult'); },
  place(t) { RN.sel = null; RN.place = { type: t, x: null, y: null }; setSheet({ k: 'place' }); },
  confirmplace() {
    const pl = RN.place; if (!pl || pl.x == null) return; const S = UI.S, isLine = (pl.type === 'wall' || pl.type === 'moat') && pl.x2 != null;
    const tiles = isLine ? lineTiles(pl.x, pl.y, pl.x2, pl.y2) : [{ x: pl.x, y: pl.y }]; let built = 0, fail = '', todo = tiles.slice();
    for (let pass = 0; pass < tiles.length && todo.length; pass++) { let pr = false; for (const t of todo.slice()) { const e = doBuild(S, 0, pl.type, t.x, t.y); if (!e) { built++; pr = true; todo.splice(todo.indexOf(t), 1); } else fail = e; } if (!pr) break; }
    if (!built) say(fail || 'Нельзя построить', false); else { say(built > 1 ? 'Построено клеток: ' + built : 'Построено', true); recomputeAll(); if (pl.type === 'city') { RN.place = null; closeSheet(); return; } }
    pl.x = null; pl.y = null; pl.x2 = null; pl.y2 = null; renderSheet(true);
  },
  cancelplace() { RN.place = null; closeSheet(); },
  upg() { const s = RN.sel; if (s && s.k === 'city' && UI.S.cities[s.id].owner === 0) ACT.upgrade(s.id); else openList('cities'); },
  upgrade(id) { const e = upgradeCity(UI.S, UI.S.cities[+id]); say(e || 'Город улучшен', !e); renderSheet(true); },
  selcity(id) { const c = UI.S.cities[+id]; centerOn(c.x + 1, c.y + 1); selEntity('city', c.id); },
  selarmy(id) { const a = UI.S.armies.find(q => q.id === +id); if (a) { centerOn(a.x, a.y); selEntity('army', a.id); } },
  selbld(id) { const b = UI.S.buildings.find(q => q.id === +id); if (b) { centerOn(b.x, b.y); selEntity('bld', b.id); } },
  selship(id) { const s = UI.S.ships.find(q => q.id === +id); if (s) { centerOn(s.x, s.y); selEntity('ship', s.id); } },
  recipe(a) { const [id, k] = a.split(':'); const b = UI.S.buildings.find(q => q.id === +id); if (b) { b.recipe = k; b.busy = false; renderSheet(true); } },
  cmd(a) { const [id, k] = a.split(':'); const b = UI.S.buildings.find(q => q.id === +id); const e = enqueueCmd(UI.S, b, k); if (e) say(e, false); renderSheet(true); },
  train(a) { const [id, t] = a.split(':'); const b = UI.S.buildings.find(q => q.id === +id); const e = enqueue(UI.S, b, t); if (e) say(e, false); renderSheet(true); },
  ship(a) { const [id, k] = a.split(':'); const b = UI.S.buildings.find(q => q.id === +id); const e = buildShip(UI.S, b, k); say(e || 'Судно построено', !e); },
  demo() { say('Нажмите на своё здание и выберите «Снести»', true); },
  demo1(id) { const b = UI.S.buildings.find(q => q.id === +id); if (b) { demolish(UI.S, b); closeSheet(); } },
  merge() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); const n = mergeNear(UI.S, a); say(n ? 'Отряды объединены' : 'Рядом нет своих армий (нужно подойти ближе)', !!n); renderSheet(true); },
  split() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); const n = splitArmy(UI.S, a); say(n ? 'Армия разделена' : 'В армии один отряд', !!n); },
  stop() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); stopArmy(a); },
  retreat() { const a = selArmy(); if (!a) return say('Сначала выберите свою армию', false); const e = retreat(UI.S, a); if (e) say(e, false); },
  stopship() { const s = RN.sel; if (s && s.k === 'ship') { const sh = UI.S.ships.find(q => q.id === s.id); if (sh) { sh.path = []; } } else openList('ships'); },
  unload() { const s = RN.sel; if (s && s.k === 'ship') { const sh = UI.S.ships.find(q => q.id === s.id); const e = sh && unloadShip(UI.S, sh); say(e || 'Войска высажены', !e); } else say('Выберите военный корабль', false); },
  claim(id) { const e = startClaim(UI.S, 0, +id); say(e || 'Претензия предъявлена: 3 дня', !e); renderSheet(true); },
  breakally(id) { breakAlly(UI.S, 0, +id); renderSheet(true); },
  retreatyes(id) { const S = UI.S; S.prompts.shift(); const a = S.armies.find(q => q.id === +id); $('modal').classList.add('hidden'); UI.paused = UI.pausedBefore || false; renderTabs(); if (a) { const e = retreat(S, a); say(e || 'Армия отступает', !e); } },
  retreatno(id) { UI.S.prompts.shift(); $('modal').classList.add('hidden'); UI.paused = UI.pausedBefore || false; renderTabs(); },
  war(id) { const e = declareWar(UI.S, 0, +id); say(e || 'Война объявлена', !e); renderSheet(true); },
  offer(a) { const [k, id] = a.split(':'); const e = offer(UI.S, 0, +id, k); say(e || 'Предложение отправлено', !e); renderSheet(true); },
  canceltrade(id) { cancelTrade(UI.S, 0, +id); renderSheet(true); },
  inbox() { showInbox(); }, answer(a) { const [id, y] = a.split(':'); answerMsg(UI.S, +id, y === '1'); showInbox(); },
  inboxtab(t) { UI.inboxTab = t; showInbox(); },
  closemodal() { $('modal').classList.add('hidden'); UI.paused = UI.pausedBefore || false; renderTabs(); },
  newgame() { showCreate(); }, continue() { const S = loadGame(); if (S) startGame(S); else showModal('Сохранение', 'Сохранений пока нет.'); },
  howto() { showModal('Как играть', HOWTO); },
  tomenu() { $('modal').classList.add('hidden'); $('hud').classList.add('hidden'); $('sheet').classList.add('hidden'); $('menu').classList.remove('hidden'); UI.S = null; RN.S = null; RN.sel = null; RN.place = null; initDemo(); $('contBtn').classList.toggle('hidden', !localStorage.getItem('kc_save')); },
  save2() { say(saveGame(UI.S) ? 'Игра сохранена' : 'Ошибка', true); ACT.closemodal(); },
  cset(a) { const [k, v] = a.split('='); UI.create[k] = isNaN(+v) ? v : +v; if (!UI.create.sandbox && UI.create.bots < 1) UI.create.bots = 1; showCreate(); },
  start() { const o = Object.assign({}, UI.create); o.sandbox = !!o.sandbox; $('create').classList.add('hidden'); startGame(newGame(o)); },
  back() { $('create').classList.add('hidden'); $('menu').classList.remove('hidden'); }
};
function recomputeAll() { calcRates(UI.S); calcScore(UI.S); }
document.addEventListener('click', e => { const t = e.target.closest('[data-act]'); if (!t) return; const s = t.dataset.act, i = s.indexOf(':'), n = i < 0 ? s : s.slice(0, i), a = i < 0 ? '' : s.slice(i + 1); if (ACT[n]) { ACT[n](a); } });
$('mm') && $('mm').addEventListener('click', e => { const r = $('mm').getBoundingClientRect(), S = UI.S; if (S) centerOn((e.clientX - r.left) / r.width * S.W, (e.clientY - r.top) / r.height * S.H); });

/* ----- нажатия на карту ----- */
UI.mapTap = function (tx, ty) {
  const S = UI.S; if (!S || S.over) return;
  if (RN.place) { const pl = RN.place, px = Math.floor(tx - (pl.type === 'city' ? 0.5 : 0)), py = Math.floor(ty - (pl.type === 'city' ? 0.5 : 0)); if (pl.type === 'wall' || pl.type === 'moat') { if (pl.x == null || pl.x2 != null) { pl.x = px; pl.y = py; pl.x2 = null; pl.y2 = null; } else { pl.x2 = px; pl.y2 = py; } } else { pl.x = px; pl.y = py; } renderSheet(true); return; }
  const pick = () => {
    const mg = markerHit(S, tx, ty);
    if (mg) { const cur = RN.sel; const idx = cur && cur.k === 'army' ? mg.list.findIndex(q => q.id === cur.id) : -1; const a = mg.list[(idx + 1) % mg.list.length]; return { k: 'army', id: a.id }; }
    let best = null, bd = 0.6;
    for (const a of S.armies) if (!a.ship && canSee(S, a)) { const d = dist(a.x, a.y, tx, ty); if (d < bd) { bd = d; best = { k: 'army', id: a.id }; } }
    for (const s of S.ships) if (canSee(S, s)) { const d = dist(s.x, s.y, tx, ty); if (d < bd) { bd = d; best = { k: 'ship', id: s.id }; } }
    if (best) return best;
    const o = S.occ[Math.floor(ty) * S.W + Math.floor(tx)]; if (o >= 1000000) return { k: 'city', id: o - 1000000 }; if (o >= 0) return { k: 'bld', id: o };
    return null;
  };
  const hit = pick(), cur = RN.sel;
  const ownA = cur && cur.k === 'army' ? S.armies.find(a => a.id === cur.id && a.owner === 0) : null, ownS = cur && cur.k === 'ship' ? S.ships.find(a => a.id === cur.id && a.owner === 0) : null;
  if (ownA && !(hit && hit.k === 'army' && S.armies.find(a => a.id === hit.id).owner === 0)) { const e = orderMove(S, ownA, tx, ty); if (e) say(e, false); return; }
  if (ownS && ownS.kind === 'war' && !(hit && hit.k === 'ship' && S.ships.find(a => a.id === hit.id).owner === 0)) { const e = orderShip(S, ownS, tx, ty); if (e) say(e, false); return; }
  if (hit) selEntity(hit.k, hit.id); else closeSheet();
};

/* ----- модальные окна ----- */
const HOWTO = `<b class="gl">Цель.</b> Развивайте королевство, захватывайте города и копите очки (или захватите все столицы).<br><br>
<b class="gl">Управление.</b> Тяните карту пальцем, щипком — масштаб. Нажмите на здание, город или армию — откроется панель.<br><br>
<b class="gl">Стройка.</b> Вкладка СТРОЙКА: выберите здание и нажмите на карту. Ферма — на лугу, лесопилка — в лесу, рудник — на залежи, порт — на берегу. Строить можно только в своих границах; границы растут со временем.<br><br>
<b class="gl">Войска.</b> Мастерская делает луки, кузня — мечи и доспехи, конюшня — коней. В казармах выберите отряд (10 воинов). Лучники — луки; рыцари — мечи и доспех; конница — кони.<br><br>
<b class="gl">Армии.</b> Выберите армию и нажмите на карту — она пойдёт. Рядом стоящие отряды можно объединить. Конница бьёт лучников, тяжёлые рыцари — конницу, лучники — лёгких рыцарей.<br><br>
<b class="gl">Захват.</b> Подведите армию к городу: поверх города появится сетка цвета захватчика. Когда сетка заполнится и армия удержит город, он ваш.<br><br>
<b class="gl">Укрепления.</b> Вкладка СТРОЙКА → УКРЕПЛ.: стены и ров строятся линией (нажмите две точки). Ров должен начинаться у воды. Башня вмещает отряд лучников: выберите армию и нажмите на башню. Ворота свои пропускают, враги — нет. Осадная мастерская делает катапульты (бьют издалека по войскам и стенам) и тараны (быстро ломают ворота и стены) — по одной штуке. Обычные воины ломают укрепления очень медленно.<br><br>
<b class="gl">Полководцы.</b> Королевская академия (СТРОЙКА → УКРЕПЛ.) — только одна. В ней обучают одного полководца: Воеводу (лучники), Капитана (лёгкие рыцари), Лорда-командора (тяжёлые рыцари) или Маршала (конница). Пока он рядом, войска его вида бьют сильнее и получают меньше урона. Берегите его.<br><br>
<b class="gl">Море.</b> В порту стройте суда. Военное судно перевозит армию: выберите армию и нажмите на порт, затем выберите корабль и нажмите на берег.<br><br>
<b class="gl">Политика.</b> Во вкладке ПОЛИТ. объявляйте войну, просите мир, торгуйте. Все предложения приходят во ВХОДЯЩИЕ.`;
function showModal(title, body, foot) { UI.pausedBefore = UI.paused; if (UI.S) UI.paused = true; $('modal').innerHTML = `<div class="box"><div class="hd"><div class="tt">${title}</div></div><div class="bd">${body}</div><div class="ft">${foot || btn('closemodal', 'Закрыть')}</div></div>`; $('modal').classList.remove('hidden'); }
function showMenuModal() { showModal('Меню', '<div class="dim">Игра приостановлена.</div>', btn('closemodal', 'Продолжить', 'gold') + btn('save2', 'Сохранить') + btn('howto', 'Как играть') + btn('tomenu', 'В главное меню')); }
function showInbox() {
  const S = UI.S; const f = UI.inboxTab; const types = { all: null, war: ['war'], trade: ['trade', 'truce'], econ: ['econ', 'info'] };
  const list = S.msgs.filter(m => !types[f] || types[f].includes(m.type)); list.forEach(m => { if (!m.kind) m.read = true; });
  const ic = { war: 'skull', trade: 'cart', truce: 'scroll', econ: 'chart', info: 'flag' };
  let body = `<div class="tabs2">${[['all', 'Все'], ['war', 'Война'], ['trade', 'Торговля'], ['econ', 'Страна']].map(t => btn('inboxtab:' + t[0], t[1], f === t[0] ? 'on' : '')).join('')}</div>`;
  body += list.length ? list.map(m => `<div class="msg ${m.read ? '' : 'unread'}">${IMG(ic[m.type] || 'mail', 28)}<div class="g"><b>${esc(m.title)}</b> <span class="dim">${clock(m.t)}</span><div class="dim">${esc(m.text)}</div>${m.kind && !m.done ? `<div class="btns">${btn('answer:' + m.id + ':1', 'Принять', 'gold')}${btn('answer:' + m.id + ':0', 'Отказать')}</div>` : m.kind ? '<div class="dim">ответ дан</div>' : ''}</div></div>`).join('') : '<div class="dim" style="padding:10px">Сообщений нет.</div>';
  showModal(IMG('mail', 24) + ' Входящие', body); $('modal').querySelector('.bd').style.padding = '0 6px 6px';
}
function showCreate() {
  const c = UI.create, seg = (k, opts) => `<div class="seg">${opts.map(o => btn(`cset:${k}=${o[0]}`, o[1], c[k] === o[0] ? 'on' : '')).join('')}</div>`;
  $('menu').classList.add('hidden'); const el = $('create'); el.classList.remove('hidden');
  el.innerHTML = `<h2>Новая игра</h2>
  <div class="grp"><div class="lb">Тип карты</div>${seg('type', [['continent', 'Материк'], ['continents', 'Континенты'], ['islands', 'Острова'], ['lakes', 'Озёра']])}</div>
  <div class="grp"><div class="lb">Размер карты</div>${seg('size', [['s', 'Малая'], ['m', 'Средняя'], ['l', 'Большая']])}</div>
  <div class="grp"><div class="lb">Режим игры</div>${seg('sandbox', [[0, 'Обычный'], [1, 'Песочница']])}${c.sandbox ? '<div class="dim" style="margin-top:4px">Бесконечные ресурсы, без победы и поражения. Соперников можно не добавлять.</div>' : ''}</div>
  <div class="grp"><div class="lb">Число соперников</div>${seg('bots', (c.sandbox ? [[0, '0']] : []).concat([[1, '1'], [2, '2'], [3, '3'], [4, '4'], [5, '5'], [6, '6']]))}</div>
  <div class="grp"><div class="lb">Сложность соперников</div>${seg('diff', [['easy', 'Лёгкая'], ['normal', 'Средняя'], ['hard', 'Сложная'], ['mixed', 'Разная']])}</div>
  ${c.sandbox ? '' : `<div class="grp"><div class="lb">Конец игры</div>${seg('win', [['score', 'По очкам'], ['caps', 'Захват столиц']])}</div>`}
  ${!c.sandbox && c.win === 'score' ? `<div class="grp"><div class="lb">Очков для победы</div>${seg('target', [[10000, '10 000'], [20000, '20 000'], [30000, '30 000'], [50000, '50 000']])}</div>` : ''}
  <div class="grp"><div class="lb">Ваше государство: ${PNAMES[c.nation]}</div><div class="seg">${PNAMES.map((n, i) => btn('cset:nation=' + i, sw(PCOL[i]), c.nation === i ? 'on' : '')).join('')}</div></div>
  <div class="col" style="margin:8px auto;width:100%"><button class="px big gold" data-act="start">НАЧАТЬ ИГРУ</button><button class="px" data-act="back">НАЗАД</button></div>`;
}
