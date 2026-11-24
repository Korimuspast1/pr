'use strict';
/* =====================================================================
   DEAD INSTINCT — интерфейс: HUD, меню, инвентарь, диалоги, заставки
   ===================================================================== */

const UI = {
  G: null, screen: 'menu', invOpen: false, modal: null, lastHud: 0, hintT: 0, toastT: 0,
  dirty: true, banner: { t: 0 }, cine: null, lastInteract: 0, interTarget: null
};

const $ = id => document.getElementById(id);

function setIcon(el, canvas) {
  if (!el) return;
  try { el.src = canvas.toDataURL(); } catch (e) { }
  el.style.visibility = 'visible';
}
function paintIcons() {
  setIcon(document.querySelector('#iDanger img'), iconHud('eye'));
  setIcon(document.querySelector('#iBreath img'), iconHud('breath'));
  setIcon(document.querySelector('#iDraw img'), iconHud('bang'));
  setIcon(document.querySelector('#iInv img'), iconHud('menu'));
  setIcon(document.querySelector('#invBtn img'), iconHud('menu'));
  setIcon(document.querySelector('[data-act="sneak"] img'), iconHud('crouch'));
  setIcon(document.querySelector('[data-act="sprint"] img'), iconHud('run'));
  setIcon(document.querySelector('[data-act="light"] img'), iconHud('light'));
  setIcon(document.querySelector('#qMed img'), iconItem('medkit'));
  setIcon(document.querySelector('#qFood img'), iconItem('food'));
  setIcon(document.querySelector('#torchBtn img'), iconHud('light'));
}

function applyInsets() {
  // системные «вырезы» и панели Android 15+, чтобы HUD не уезжал под статус-бар
  const probe = document.createElement('div');
  probe.style.cssText = 'position:fixed;left:0;top:0;width:1px;height:1px;padding-top:env(safe-area-inset-top);padding-right:env(safe-area-inset-right);padding-bottom:env(safe-area-inset-bottom);padding-left:env(safe-area-inset-left);visibility:hidden;pointer-events:none';
  document.body.appendChild(probe);
  const cs = getComputedStyle(probe);
  const val = k => parseFloat(cs[k]) || 0;
  const top = val('paddingTop'), right = val('paddingRight'), bottom = val('paddingBottom'), left = val('paddingLeft');
  probe.remove();
  const st = document.documentElement.style;
  if (top > 0) st.setProperty('--sat', top + 'px');
  if (bottom > 0) st.setProperty('--sab', bottom + 'px');
}

function bootUI() {
  applyInsets();
  paintIcons();
  // главное меню
  document.body.classList.remove('ingame');
  $('menu').classList.remove('hidden');
  $('hud').classList.add('hidden');
  refreshContinue();
  $('menuNew').addEventListener('click', () => openChapters());
  $('menuCont').addEventListener('click', () => continueGame());
  $('menuHow').addEventListener('click', () => openHowto());
  $('menuSet').addEventListener('click', () => openSettings());
  $('menuCine').addEventListener('click', () => openCines());
  // HUD
  $('menuBtn').addEventListener('click', () => openPause());
  $('invClose').addEventListener('click', () => toggleInv(false));
  $('iInv').addEventListener('click', () => toggleInv(true));
  $('wSlot').addEventListener('click', () => toggleInv(true));
  $('qMed').addEventListener('click', () => {
    const G = UI.G; if (!G) return;
    if ((G.inventory.medkit || 0) > 0 && G.player.hp < 55) useItem(G, 'medkit');
    else if ((G.inventory.bandage || 0) > 0) useItem(G, 'bandage');
    else if ((G.inventory.medkit || 0) > 0) useItem(G, 'medkit');
    else if ((G.inventory.pills || 0) > 0) useItem(G, 'pills');
    else toast('Нет медикаментов');
    sfx('heal');
  });
  $('qFood').addEventListener('click', () => {
    const G = UI.G; if (!G) return;
    if (G.player.thirst >= G.player.hunger && (G.inventory.water || 0) > 0) useItem(G, 'water');
    else if ((G.inventory.food || 0) > 0) useItem(G, 'food');
    else if ((G.inventory.water || 0) > 0) useItem(G, 'water');
    else toast('Нечего есть');
    sfx('eat');
  });
  $('invBtn').addEventListener('click', () => toggleInv(true));
  $('torchBtn').addEventListener('click', () => { if (UI.G) { const P = UI.G.player; if (P.battery > 0) { P.torch = !P.torch; sfx('click'); } else toast('Нет заряда фонаря'); } });
  $('interactBtn').addEventListener('click', () => doInteract());
  for (const b of document.querySelectorAll('#actions .abtn')) {
    b.addEventListener('click', () => {
      if (!UI.G) return;
      const act = b.dataset.act;
      if (act === 'sneak') { MOVE.sneak = !MOVE.sneak; b.classList.toggle('on', MOVE.sneak); }
      if (act === 'sprint') { MOVE.sprintHold = !MOVE.sprintHold; b.classList.toggle('on', MOVE.sprintHold); }
      if (act === 'light') { const P = UI.G.player; if (P.battery > 0) { P.torch = !P.torch; b.classList.toggle('on', P.torch); } else toast('Нет заряда'); }
      sfx('click');
    });
  }
  $('iDanger').addEventListener('click', () => { if (UI.G) { GAME_INPUT.danger = true; setTimeout(() => GAME_INPUT.danger = false, 120); } });
  $('iBreath').addEventListener('click', () => { if (UI.G) { GAME_INPUT.freeze = true; setTimeout(() => GAME_INPUT.freeze = false, 120); } });
  $('iDraw').addEventListener('click', () => { if (UI.G) { const P = UI.G.player; throwBang(UI.G, P.x + Math.cos(P.dir) * 5, P.y + Math.sin(P.dir) * 5); } });
  // модалки
  $('modal').addEventListener('click', e => { if (e.target.id === 'modal') closeModal(); });
  $('dialogue').addEventListener('click', advanceDialog);
}

/* ---------- меню ---------- */
function refreshContinue() {
  const s = loadSave();
  $('menuCont').classList.toggle('hidden', !s);
  if (s) $('contInfo').textContent = 'Эпизод ' + s.chapter + ' · ' + (CHAPTERS[s.chapter - 1] ? CHAPTERS[s.chapter - 1].n : '');
}

function openChapters() {
  const save = loadSave();
  const unlocked = save ? save.chapter : 1;
  let html = '<div class="head"><h2>ВЫБЕРИ ЭПИЗОД</h2><button class="btn" id="chClose">✕</button></div><div class="body">';
  CHAPTERS.forEach(c => {
    const lock = c.id > unlocked;
    html += `<div class="ep panel ${lock ? 'locked' : ''}" data-ch="${c.id}">
      <div class="num">${lock ? '🔒' : c.id}</div>
      <div class="info"><div class="n">${c.n}</div><div class="s">${c.time} · ${lock ? 'откроется после предыдущего' : c.intro.slice(0, 64) + '…'}</div></div>
    </div>`;
  });
  html += '</div><div class="foot"><button class="btn dim" id="chBack">НАЗАД</button></div>';
  showScreen('chapters', html);
  $('chClose').onclick = $('chBack').onclick = closeScreen;
  document.querySelectorAll('#screen .ep:not(.locked)').forEach(el => {
    el.onclick = () => { closeScreen(); startChapter(+el.dataset.ch); };
  });
}

function openHowto() {
  const html = `<div class="head"><h2>КАК ИГРАТЬ</h2><button class="btn" id="hClose">✕</button></div>
  <div class="body" style="font-size:.8em;line-height:1.75">
    <p class="goldt">Это не шутер. Это история про выживание.</p>
    <p><b>Шум — твой главный враг.</b> Бег слышен на 14 тайлов, шаг — на 2, скрытность — на пол-тайла.
    Каждый выстрел привлекает всё живое в радиусе квартала. Зомби видят плохо, но слышат отлично.</p>
    <p><b>Инстинкты</b> (справа сверху): «Чувство беды» замедляет время и подсвечивает угрозу,
    «Затаиться» — замирание, зомби теряют след. «Отвести» — шум уводит стаю в сторону.</p>
    <p><b>Группа.</b> Люди идут за тобой и доверяют тебе. Доверие падает от жестокости и потерь —
    и когда оно упадёт совсем, кто-то уйдёт в ночь. Один здесь не выживает.</p>
    <p><b>Голод и заражение.</b> Ешь и пей, лечи раны: заражение растёт от укусов и от кровотечения.
    Бинты и аптечки останавливают жар, антибиотики — надёжнее.</p>
    <p><b>Фонарь</b> спасает от темноты, но свет в окнах — сигнал «еда здесь». Ночью без света видно на 4 тайла.</p>
    <p><b>Управление:</b> слева — виртуальный стик. Справа — стик камеры и кнопки действий.
    На ПК: WASD / стрелки — движение, мышь — камера, ЛКМ — удар/выстрел, Shift — бег, C — крадучись,
    E — действие, I — инвентарь, F — фонарь, Q/E/R — инстинкты.</p>
    <p class="dimt">Дойди до цели эпизода и вернись в убежище. Всё, что между — история.</p>
  </div><div class="foot"><button class="btn" id="hBack">ПОНЯТНО</button></div>`;
  showScreen('howto', html);
  $('hClose').onclick = $('hBack').onclick = closeScreen;
}

function openSettings() {
  const s = (UI.G && UI.G.settings) || { sound: true, shake: true, blood: true, hints: true };
  const row = (k, label) => `<button class="btn ${s[k] ? 'on' : ''}" data-set="${k}">${label}: <span class="goldt">${s[k] ? 'ВКЛ' : 'ВЫКЛ'}</span></button>`;
  const html = `<div class="head"><h2>НАСТРОЙКИ</h2><button class="btn" id="stClose">✕</button></div>
  <div class="body col" style="gap:.6em">
    ${row('sound', 'Звук')}
    ${row('shake', 'Тряска экрана')}
    ${row('blood', 'Кровь')}
    ${row('hints', 'Подсказки')}
    <div class="tiny dimt" style="margin-top:1em">Dead Instinct v${GAME_VERSION} · офлайн, без интернета и разрешений.<br>
    Игра полностью вымышлена. Все совпадения — случайны.</div>
  </div><div class="foot"><button class="btn" id="stBack">НАЗАД</button></div>`;
  showScreen('settings', html);
  $('stClose').onclick = $('stBack').onclick = () => { closeScreen(); refreshContinue(); };
  document.querySelectorAll('#screen [data-set]').forEach(el => el.onclick = () => {
    const k = el.dataset.set; s[k] = !s[k];
    if (UI.G) UI.G.settings[k] = s[k];
    SND.enabled = s.sound;
    el.classList.toggle('on', s[k]);
    el.innerHTML = { sound: 'Звук', shake: 'Тряска экрана', blood: 'Кровь', hints: 'Подсказки' }[k] + ': <span class="goldt">' + (s[k] ? 'ВКЛ' : 'ВЫКЛ') + '</span>';
    sfx('click');
  });
}

function openCines() {
  const html = `<div class="head"><h2>ЗАСТАВКИ</h2><button class="btn" id="cClose">✕</button></div>
  <div class="body">${CHAPTERS.map(c => `<div class="ep panel" data-cine="${c.id}"><div class="num">${c.id}</div><div class="info"><div class="n">${c.n}</div><div class="s">${c.time}</div></div></div>`).join('')}
  <div class="tiny dimt" style="margin-top:1em">Заставки открываются по мере прохождения. Можно посмотреть любую — без спойлеров туториала.</div></div>
  <div class="foot"><button class="btn" id="cBack">НАЗАД</button></div>`;
  showScreen('cines', html);
  $('cClose').onclick = $('cBack').onclick = closeScreen;
  document.querySelectorAll('#screen [data-cine]').forEach(el => el.onclick = () => {
    const ch = +el.dataset.cine;
    closeScreen();
    playCinematic(ch, () => bootUI());
  });
}

/* ---------- заставка эпизода ---------- */
function playCinematic(ch, done, auto) {
  const c = CHAPTERS[ch - 1];
  const el = $('cine');
  el.classList.remove('hidden');
  el.innerHTML = `<div class="ctitle">ЭПИЗОД ${c.id} · ${c.n}</div><div class="csub">${c.time}</div>
    <div class="ctext" id="ctext">${c.intro}</div>
    ${c.sub ? `<div id="csub2" style="margin-top:1.6em;color:#c8503c;font-size:.72em;letter-spacing:.16em;text-align:center;opacity:0;transition:opacity 1.4s .9s">${c.sub}</div>` : ''}
    <div class="tiny dimt" style="margin-top:2em">нажми, чтобы продолжить</div>
    <div class="cskip">${auto ? 'ЗАГРУЗКА…' : 'ПРОПУСТИТЬ ▸'}</div>`;
  setTimeout(() => { const t = $('ctext'); if (t) t.classList.add('show'); const s2 = $('csub2'); if (s2) s2.style.opacity = '1'; }, 80);
  const next = () => { el.classList.add('hidden'); el.onclick = null; SND.click(); done(); };
  el.onclick = next;
  if (auto) setTimeout(() => { if (!$('cine').classList.contains('hidden')) next(); }, 5200);
}

/* ---------- экраны ---------- */
function showScreen(name, html) {
  closeScreen();
  const d = document.createElement('div');
  d.className = 'screen'; d.id = 'screen'; d.dataset.screen = name;
  d.innerHTML = html;
  document.body.appendChild(d);
  UI.screen = name;
}
function closeScreen() {
  const s = $('screen'); if (s) s.remove();
  UI.screen = null;
}

/* ---------- загрузка/старт игор ---------- */
function startChapter(ch, fromSave) {
  purgeOverlays();
  closeScreen();
  UI.G = newGame(ch, {});
  UI.G.settings.sound = SND.enabled;
  if (fromSave) restoreFromSave(UI.G, fromSave);
  beginGame();
}

function beginGame() {
  const G = UI.G;
  document.body.classList.add('ingame');
  $('menu').classList.add('hidden');
  $('hud').classList.remove('hidden');
  centerOn(G, G.player.x, G.player.y);
  UI.dirty = true;
  startAmbient(G);
  if (!G.flags.cineShown) {
    G.flags.cineShown = true;
    playCinematic(G.chapter, () => {
      pushDialog(G, G.intro);
      drainDialog(G);
    }, true);
  } else if (G.intro.length) { pushDialog(G, G.intro); drainDialog(G); }
}

function continueGame() {
  const s = loadSave();
  if (!s) return toast('Нет сохранения');
  startChapter(s.chapter, s);
}

function restoreFromSave(G, s) {
  try {
    Object.assign(G.player, s.player);
    G.inventory = s.inventory || G.inventory;
    G.trust = s.trust !== undefined ? s.trust : G.trust;
    G.flags = Object.assign(G.flags, s.flags || {});
    G.objectives = s.objectives || G.objectives;
    G.stats = s.stats || G.stats;
    G.clock = s.clock || G.clock;
    G.player.x = clamp(G.player.x, 1, G.M.W - 2);
    G.player.y = clamp(G.player.y, 1, G.M.H - 2);
    if (s.group) for (const g of s.group) { const f = G.group.find(x => x.id === g.id); if (f) { f.hp = g.hp; f.dead = g.dead; f.infect = g.infect || 0; } }
    G.flags.cineShown = true;
    // безопасное место: не спавнить в стене
    if (collides(G.M, G.player.x, G.player.y, 0.28)) { G.player.x = G.M.safehouse.x + 0.5; G.player.y = G.M.safehouse.y + 0.5; }
  } catch (e) { }
}

/* ---------- пауза ---------- */
function openPause() {
  if (!UI.G) return;
  UI.G.paused = true;
  const html = `<div class="head"><h2>ПАУЗА</h2></div>
  <div class="body col" style="gap:.5em">
    <button class="btn gold" id="pResume">ПРОДОЛЖИТЬ</button>
    <button class="btn" id="pSave">СОХРАНИТЬ</button>
    <button class="btn" id="pHow">КАК ИГРАТЬ</button>
    <button class="btn" id="pSet">НАСТРОЙКИ</button>
    <button class="btn red" id="pMenu">В ГЛАВНОЕ МЕНЮ</button>
    <div class="tiny dimt" style="margin-top:1em">Эпизод ${UI.G.chapter} · ${UI.G.chapterName}<br>Убито заражённых: ${UI.G.stats.killed}<br>Длящих: ${UI.G.stats.quietKills}<br>Доверие: ${Math.round(UI.G.trust)}</div>
  </div>`;
  showScreen('pause', html);
  $('pResume').onclick = () => { closeScreen(); UI.G.paused = false; };
  $('pSave').onclick = () => { save(UI.G); toast('Сохранено'); };
  $('pHow').onclick = openHowto;
  $('pSet').onclick = openSettings;
  $('pMenu').onclick = () => { save(UI.G); closeScreen(); UI.G.paused = false; UI.G = null; bootUI(); };
}

/* ---------- инвентарь ---------- */
function toggleInv(force) {
  const on = force !== undefined ? force : !UI.invOpen;
  if (on && !UI.G) return;
  UI.invOpen = on;
  const el = $('inv');
  el.classList.toggle('hidden', !on);
  if (on) renderInv();
}

function renderInv() {
  const G = UI.G; if (!G) return;
  const el = $('inv'), items = $('invItems');
  items.innerHTML = '';
  const keys = ['medkit', 'bandage', 'pills', 'food', 'water', 'battery', 'fuel', 'flare', 'bar', 'bat', 'axe', 'knife', 'pistol', 'shotgun', 'rifle', 'ammo9', 'ammo12', 'ammo762'];
  for (const k of keys) {
    const n = G.inventory[k] || 0;
    const slot = document.createElement('div');
    slot.className = 'slot panel' + (n ? '' : ' empty');
    const isEq = (G.player.weapon === k);
    slot.innerHTML = `<img src="${iconItem(k).toDataURL()}" alt=""><div>${ITEM[k].n}</div><div class="n">${n ? '×' + n : ''}</div>${isEq ? '<div class="eq">✓</div>' : ''}`;
    if (n) slot.onclick = () => {
      if (isEq && (ITEM[k].weapon || ITEM[k].gun)) { showItemInfo(k); return; }
      if (ITEM[k].weapon || ITEM[k].gun) { equip(G, k); renderInv(); }
      else showItemInfo(k);
    };
    items.appendChild(slot);
  }
  // счётчик патронов
  const P = G.player;
  $('invInfo').innerHTML = `В руках: <span class="goldt">${P.weapon ? ITEM[P.weapon].n : 'кулаки'}</span>` +
    (ITEM[P.weapon] && ITEM[P.weapon].gun ? ` · заряд <span class="goldt">${P.gunAmmo[ITEM[P.weapon].gun.ammo] || 0}</span>` : '') +
    ` · фонарь <span class="goldt">${P.battery.toFixed(1)}</span>`;
}

function showItemInfo(k) {
  const G = UI.G, item = ITEM[k];
  const actions = [];
  const usable = ['medkit', 'bandage', 'pills', 'food', 'water', 'battery', 'flare', 'fuel'].includes(k);
  if (usable) actions.push({ t: 'ИСПОЛЬЗОВАТЬ', fn: () => { useItem(G, k); closeModal(); renderInv(); } });
  if (item.weapon || item.gun) actions.push({ t: 'ВЗЯТЬ В РУКИ', fn: () => { equip(G, k); closeModal(); renderInv(); } });
  actions.push({ t: 'ЗАКРЫТЬ', fn: closeModal });
  showModal(`${item.n}`, `<div class="tiny dimt">${item.d}</div>`, actions);
}

/* ---------- модалки ---------- */
function showModal(title, text, btns) {
  UI.modal = true;
  const el = $('modal');
  el.classList.remove('hidden');
  el.innerHTML = `<div class="box panel"><h3>${title}</h3><div class="mtext">${text}</div>
    <div class="btns">${btns.map((b, i) => `<button class="btn ${b.cls || ''}" data-b="${i}">${b.t}</button>`).join('')}</div></div>`;
  el.querySelectorAll('[data-b]').forEach(bt => bt.onclick = () => { SND.click(); btns[+bt.dataset.b].fn(); });
}
function closeModal() { UI.modal = null; const el = $('modal'); el.classList.add('hidden'); el.innerHTML = ''; }

function toast(text) {
  const el = $('toast');
  if (!el) return;
  el.textContent = text;
  el.classList.add('show');
  clearTimeout(UI.toastT);
  UI.toastT = setTimeout(() => el.classList.remove('show'), 2200);
}

/* ---------- диалоги ---------- */
const WHO_NAME = { you: 'ТЫ', mara: 'МАРА', yan: 'ЯН', vera: 'ВЕРА', dym: 'ДЫМ', stas: 'СТАС', luda: 'ЛЮДА', radio: 'РАДИО', narrator: '', bandit: 'ЧУЖОЙ', voice: 'ГОЛОС' };
function portraitFor(who) {
  if (['mara', 'yan', 'vera', 'dym', 'stas', 'luda'].includes(who)) return medallion(who).toDataURL();
  if (who === 'bandit') return spritePortrait('zWalker').toDataURL();
  if (who === 'narrator' || who === 'radio') return null;
  return null;
}

function advanceDialog() {
  const G = UI.G;
  if (!G) return;
  if (!G.dialog) { drainDialog(G); if (!G.dialog) { $('dialogue').classList.add('hidden'); return; } }
  // показать следующую реплику или закрыть
  G.dialogShown = (G.dialogShown || 0) + 1;
  if (G.dialogShown < (G.dialog.textLen || 1)) { G.dialogShown = 999; return; }
  drainDialogNext(G);
}

function drainDialogNext(G) {
  if (G.dialogQueue && G.dialogQueue.length) { G.dialog = G.dialogQueue.shift(); G.dialogShown = 0; }
  else { G.dialog = null; }
}

function renderDialogUI(G, dt) {
  const el = $('dialogue');
  if (!G.dialog) {
    if (!el.classList.contains('hidden')) el.classList.add('hidden');
    return;
  }
  el.classList.remove('hidden');
  const who = G.dialog.who;
  const lineKey = (G.flags._line || 0) + '_' + (G.dialog.text || '').length;
  if (UI._lastLine !== lineKey + who) {
    UI._lastLine = lineKey + who;
    UI._line = lineKey;
    const port = portraitFor(who);
    $('dlgPortrait').innerHTML = port ? `<img src="${port}" alt="">` : '';
    $('dlgPortrait').style.display = port ? 'block' : 'none';
    $('dlgWho').textContent = WHO_NAME[who] || who.toUpperCase();
    UI._lineStart = performance.now();
  }
  const full = G.dialog.text || '';
  const age = (performance.now() - (UI._lineStart || 0)) / 1000;
  const shown = Math.min(full.length, Math.floor(age * 46));
  $('dlgText').textContent = shown <= 0 ? full : full.slice(0, shown);
  $('dlgNext').textContent = shown >= full.length ? '▸' : '';
}

/* ---------- выбор ---------- */
function renderChoiceUI(G) {
  const el = $('choice');
  if (!G.choice) { el.classList.add('hidden'); el.innerHTML = ''; return; }
  if (UI._choice === G.choice) return;
  UI._choice = G.choice;
  el.classList.remove('hidden');
  el.innerHTML = `<div class="q">${G.choice.q}</div>` +
    G.choice.opts.map((o, i) => `<button class="btn opt" data-o="${i}"><span class="t">${o.t}</span><span class="d">${o.d}</span></button>`).join('');
  el.querySelectorAll('[data-o]').forEach(b => b.onclick = () => {
    const i = +b.dataset.o;
    SND.click();
    G.flags.choiceMade = (G.flags.choiceMade || 0) + 1;
    applyChoice(G, G.choice.opts[i]);
    G.choice = null; UI._choice = null;
    el.classList.add('hidden'); el.innerHTML = '';
    // последствия: реплика
    const reaction = i === 0 ? 'Ты выбрал человека вместо припасов. Это дорого. И это правильно.' :
      i === 1 ? 'Тишина — тоже ответ. Иногда единственный.' :
        'Ты сделал выбор. Кровь на руках не смывается дождём.';
    pushDialog(G, [{ who: 'mara', text: reaction }]);
    drainDialog(G);
  });
}

/* ---------- HUD ---------- */
function renderHud(G, dt, now) {
  if (!G) return;
  const P = G.player;
  // часы
  const hh = Math.floor(G.clock / 60) % 24, mm = Math.floor(G.clock % 60);
  $('clock').textContent = (hh < 10 ? '0' : '') + hh + ':' + (mm < 10 ? '0' : '') + mm;
  $('chap').textContent = 'ЭП. ' + G.chapter + ' · ' + G.chapterName;
  // шум
  const nIdx = clamp(Math.ceil(P.noise / 5), 0, 5);
  const bars = $('noiseBars').children;
  for (let i = 0; i < bars.length; i++) {
    bars[i].className = i < nIdx ? (i < 2 ? 'on1' : i < 4 ? 'on2' : 'on3') : '';
  }
  // полоски
  setBar('barHp', P.hp / P.maxHp);
  const hpt = $('hpTxt'); if (hpt) hpt.textContent = Math.max(0, Math.round(P.hp));
  setBar('barSt', P.stamina / 100);
  setBar('barHu', 1 - P.hunger / 100);
  setBar('barTh', 1 - P.thirst / 100);
  setBar('barIn', P.infect / 100);
  $('barInWrap').style.display = P.infect > 0.5 ? 'block' : 'none';
  // цели
  if (now - UI.lastHud > 240) {
    UI.lastHud = now;
    let oh = '<div class="ttl">ЦЕЛЬ</div>';
    for (const o of G.objectives) {
      const cls = o.done ? 'done' : (o === G.objectives.find(q => !q.done) ? 'act' : '');
      oh += `<div class="obj ${cls}">${o.text}</div>`;
    }
    $('objective').innerHTML = oh;
    const tl = trustLevel(G);
    $('trustBox').innerHTML = `<div>ДОВЕРИЕ ГРУППЫ: <span class="lv" style="color:${tl.col}">${tl.n}</span></div><div class="bar"><i style="width:${G.trust}%"></i></div>`;
    // партия
    let ph = '';
    for (const f of G.group) {
      ph += `<div class="medal ${f.dead ? 'dead' : ''}" title="${f.n}"><img src="${medallion(f.id).toDataURL()}" alt=""><div class="hp" style="width:${clamp(f.hp / f.maxHp * 100, 0, 100)}%"></div>${f.infect > 0 ? '<div class="inf" style="width:' + clamp(f.infect, 0, 100) + '%"></div>' : ''}</div>`;
    }
    $('party').innerHTML = ph;
    // быстрые слоты
    renderQuick(G);
  }
  // фонарь
  $('torchBtn').classList.toggle('on', P.torch);
  $('torchBtn').classList.toggle('dis', P.battery <= 0);
  // кнопка действия
  updateInteract(G);
  // баннер
  if (G.banner) {
    if (!UI.banner.text || UI.banner.text !== G.banner.text) {
      UI.banner = { text: G.banner.text, t: G.banner.t };
      $('banner').innerHTML = `<div class="b1">${G.banner.text}</div><div class="b2">${G.banner.sub || ''}</div>`;
    }
    UI.banner.t -= dt;
    $('banner').classList.toggle('show', UI.banner.t > 0);
    if (UI.banner.t <= 0) { G.banner = null; UI.banner.text = ''; }
  }
  // подсказки
  if (G.hints.length && UI.hintT <= 0) {
    UI.hintT = 14;
    $('hint').textContent = G.hints[Math.floor(Math.random() * G.hints.length)];
    $('hint').classList.add('show');
    setTimeout(() => $('hint').classList.remove('show'), 6000);
  }
  UI.hintT -= dt;
  renderDialogUI(G, dt);
  renderChoiceUI(G);
  // смерть
  if (G.over && G.over.kind === 'dead' && !UI._deadShown) { UI._deadShown = true; showDeath(G); }
}

function setBar(id, v) { const el = $(id); if (el) el.style.width = clamp(v * 100, 0, 100) + '%'; }

function renderQuick(G) {
  const P = G.player;
  const w = P.weapon;
  const k = w || 'bar';
  $('wIcon').src = iconItem(k).toDataURL();
  let ammo = '';
  if (w && ITEM[w] && ITEM[w].gun) ammo = (P.gunAmmo[ITEM[w].gun.ammo] || 0) + '/' + (G.inventory[ITEM[w].gun.ammo] || 0);
  $('wAmmo').textContent = ammo;
  $('medN').textContent = (G.inventory.medkit || 0) + (G.inventory.bandage || 0) || '';
  $('foodN').textContent = (G.inventory.food || 0) + (G.inventory.water || 0) || '';
  $('qMed').classList.toggle('dis', !(G.inventory.medkit || G.inventory.bandage));
  $('qFood').classList.toggle('dis', !(G.inventory.food || G.inventory.water));
}

function updateInteract(G) {
  const P = G.player;
  let target = null, label = '';
  // ближайший интересный объект в 1.6 тайла
  for (const c of G.M.containers) {
    if (c.looted || !c.loot) continue;
    if (Math.hypot(c.x + 0.5 - P.x, c.y + 0.5 - P.y) < 1.5) { target = { x: c.x, y: c.y }; label = 'ОБЫСКАТЬ'; break; }
  }
  if (!target) for (const d of G.M.doors) {
    if (Math.hypot(d.x + 0.5 - P.x, d.y + 0.5 - P.y) < 1.5 && !d.broken) { target = { x: d.x, y: d.y }; label = d.open > 0.6 ? 'ЗАКРЫТЬ' : 'ОТКРЫТЬ'; break; }
  }
  if (!target) for (const p of G.M.props) {
    if (p.kind === 'generator' || p.kind === 'radio') {
      if (Math.hypot(p.x + 0.5 - P.x, p.y + 0.5 - P.y) < 1.7) { target = { x: p.x, y: p.y }; label = p.kind === 'generator' ? 'ГЕНЕРАТОР' : 'РАДИО'; break; }
    }
  }
  if (!target && G.chapter === 5 && G.M.flags.journalAt && !G.flags.journalFound) {
    if (Math.hypot(G.M.flags.journalAt.x + 0.5 - P.x, G.M.flags.journalAt.y + 0.5 - P.y) < 1.8) { target = G.M.flags.journalAt; label = 'ЖУРНАЛ'; }
  }
  UI.interTarget = target;
  const btn = $('interactBtn');
  btn.classList.toggle('show', !!target);
  if (target) btn.textContent = label;
}

function doInteract() {
  const G = UI.G;
  if (!G || !UI.interTarget) return;
  const r = interact(G, UI.interTarget.x, UI.interTarget.y);
  sfx('click');
}

/* ---------- смерть ---------- */
function purgeOverlays() {
  for (const id of ['dead', 'screen']) { const el = $(id); if (el) el.remove(); }
  const inv = $('inv'); if (inv) inv.classList.add('hidden');
  const dl = $('dialogue'); if (dl) dl.classList.add('hidden');
  const ch = $('choice'); if (ch) { ch.classList.add('hidden'); ch.innerHTML = ''; }
  const ci = $('cine'); if (ci) ci.classList.add('hidden');
  const mo = $('modal'); if (mo) { mo.classList.add('hidden'); mo.innerHTML = ''; }
  UI._deadShown = false; UI._endShown = false; UI._choice = null; UI._lastLine = null; UI.invOpen = false; UI.modal = null; UI.screen = null;
}

function showDeath(G) {
  const tip = DEATH_TIPS[(Math.random() * DEATH_TIPS.length) | 0];
  const alive = G.group.filter(n => !n.dead).length;
  const html = `<div class="dt">ТЫ МЁРТВ</div>
    <div class="ds">${G.over.why || 'Инстинкт не помог.'}<br><br>
      <span class="dimt">Эпизод ${G.chapter} · ${G.chapterName}<br>
      Убито заражённых: ${G.stats.killed} · Тихо: ${G.stats.quietKills}<br>
      Людей рядом: ${alive}</span></div>
    <div class="tip">${tip}</div>
    <div class="col" style="width:min(320px,86vw)">
      <button class="btn gold" id="dRetry">ЭПИЗОД ЗАНОВО</button>
      <button class="btn" id="dMenu">В ГЛАВНОЕ МЕНЮ</button>
    </div>`;
  const d = document.createElement('div');
  d.id = 'dead'; d.innerHTML = html;
  document.body.appendChild(d);
  $('dRetry').onclick = () => { clearSave(); d.remove(); purgeOverlays(); UI.G = null; startChapter(G.chapter); };
  $('dMenu').onclick = () => { clearSave(); d.remove(); purgeOverlays(); UI.G = null; bootUI(); };
}

/* ---------- финал эпизода ---------- */
function showChapterEnd(G) {
  if (UI._endShown) return;
  UI._endShown = true;
  const ci0 = $('cine'); if (ci0) ci0.classList.add('hidden');
  const dl0 = $('dialogue'); if (dl0) dl0.classList.add('hidden');
  const isFinal = G.chapter >= 5;
  const nextCh = G.chapter + 1;
  let endText = '';
  if (isFinal) {
    const e = G.over.ending || ENDINGS.mixed;
    endText = `<div class="ctitle" style="color:${e.n === 'СВЕТ В КОНЦЕ ТОННЕЛЯ' ? 'var(--green)' : e.n === 'ПЕРЕРОЖДЕНИЕ' ? '#a86' : 'var(--gold)'}">${e.n}</div>
      <div class="ds">${e.t}</div><div class="tiny dimt" style="margin-bottom:1.4em">${e.d}</div>`;
  }
  const html = `<div class="head"><h2>${isFinal ? 'ФИНАЛ' : 'ЭПИЗОД ЗАВЕРШЁН'}</h2></div>
    <div class="body" style="text-align:center;padding-top:2em">
      ${endText}
      ${isFinal ? '' : `<div class="goldt" style="font-size:1.1em;letter-spacing:.1em">ЭПИЗОД ${G.chapter} ПРОЙДЕН</div>
      <div class="tiny dimt" style="margin:.8em 0 1.6em">${G.stats.killed} убито · ${G.stats.quietKills} тихо · доверие ${Math.round(G.trust)}<br>
      ${G.group.filter(n => !n.dead).length} из ${G.group.length} человек рядом</div>`}
      <div class="tiny dimt" style="line-height:1.7">${isFinal ? 'Спасибо, что выжил. История Dead Instinct на этом заканчивается — но город остался.' : 'Следующий эпизод откроется в главном меню.'}</div>
    </div>
    <div class="foot col" style="gap:.5em">
      ${isFinal ? '<button class="btn gold" id="eMenu">В ГЛАВНОЕ МЕНЮ</button>' : '<button class="btn gold" id="eNext">СЛЕДУЮЩИЙ ЭПИЗОД ▸</button><button class="btn" id="eMenu">В МЕНЮ</button>'}
    </div>`;
  showScreen('end', html);
  if (isFinal) $('eMenu').onclick = () => { clearSave(); closeScreen(); UI.G = null; bootUI(); };
  else {
    $('eNext').onclick = () => { closeScreen(); startChapter(nextCh); };
    $('eMenu').onclick = () => { closeScreen(); UI.G = null; bootUI(); };
  }
  // отметить прогресс
  try { const s = loadSave() || {}; s.chapter = Math.max(s.chapter || 1, Math.min(5, G.chapter + 1)); if (!s.player) localStorage.setItem('di_progress', String(Math.min(5, G.chapter + 1))); localStorage.setItem('di_progress', String(Math.min(5, G.chapter + 1))); } catch (e) { }
  clearSave();
}

/* ---------- возврат из игры (Android back) ---------- */
window.__diBack = function () {
  if ($('dead')) return true;
  if (UI.modal) { closeModal(); return true; }
  if (UI.invOpen) { toggleInv(false); return true; }
  if (UI.G && UI.G.choice) return true;
  if (UI.G && UI.G.dialog) { UI.G.dialog = null; $('dialogue').classList.add('hidden'); return true; }
  if (UI.screen) { closeScreen(); if (UI.G) UI.G.paused = false; return true; }
  if (UI.G) { openPause(); return true; }
  return false;
};
window.__diPause = function () { if (UI.G) { save(UI.G); UI.G.paused = true; stopAmbient(); } };
window.__diResume = function () { if (UI.G && !UI.screen) UI.G.paused = false; startAmbient(UI.G); };

if (typeof module !== 'undefined' && module.exports) module.exports = { bootUI, UI, toast };
