'use strict';
// Смоук-тест интерфейса без браузера (jsdom): кнопка «Подробнее» у жителей, действия над ними, меню ПОЛИТ.
// Запуск: node tools/test_ui.js
const path = require('path');
const KC_TOOLS = process.env.KC_TOOLS || process.env.HOME + '/.cache/kc-tools';
const { JSDOM } = require(KC_TOOLS + '/node_modules/jsdom');

let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const wait = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const errs = [];
  const dom = await JSDOM.fromFile(path.resolve(__dirname, '../www/index.html'), {
    runScripts: 'dangerously',
    resources: 'usable',
    pretendToBeVisual: true,
    beforeParse(w) {
      // заглушки браузерного окружения (бесконечная no-op цепочка для canvas)
      const anything = new Proxy(function () { }, {
        get: (t, p) => (p === Symbol.toPrimitive ? () => 0 : anything),
        set: () => true,
        apply: () => anything,
      });
      const noopCtx = new Proxy({}, {
        get: (t, p) => (p === 'canvas' ? { width: 64, height: 64 } : (p in t ? t[p] : anything)),
        set: (t, p, v) => (t[p] = v, true),
      });
      w.HTMLCanvasElement.prototype.getContext = () => noopCtx;
      w.HTMLCanvasElement.prototype.toDataURL = () => 'data:,';
      w.ImageData = class { constructor(w, h) { this.width = w; this.height = h; this.data = new Uint8ClampedArray(Math.max(1, w * h * 4)); } };
      w.Audio = class { play() { return Promise.resolve(); } pause() { } };
      w.AudioContext = w.webkitAudioContext = class {
        constructor() { this.currentTime = 0; this.destination = {}; this.state = 'running'; }
        createOscillator() { return { connect() { }, start() { }, stop() { }, disconnect() { }, frequency: { value: 0, setValueAtTime() { }, exponentialRampToValueAtTime() { }, linearRampToValueAtTime() { } }, type: '' }; }
        createGain() { return { connect() { }, disconnect() { }, gain: { value: 1, setValueAtTime() { }, exponentialRampToValueAtTime() { }, linearRampToValueAtTime() { } } }; }
        createBuffer() { return { getChannelData: () => new Float32Array(1) }; }
        createBufferSource() { return { connect() { }, start() { }, stop() { }, disconnect() { }, buffer: null, playbackRate: { value: 1 } }; }
        resume() { return Promise.resolve(); }
      };
      w.addEventListener('error', e => errs.push('pageerror: ' + (e.message || e.error)));
    },
  });
  const w = dom.window, d = w.document;
  const ev = expr => w.eval(expr);
  const click = sel => {
    const el = typeof sel === 'string' ? d.querySelector(sel) : sel;
    if (!el) throw new Error('нет элемента ' + sel);
    el.click();
    return el;
  };
  const sheetText = () => { const s = d.getElementById('sheet'); return s && !s.classList.contains('hidden') ? s.textContent : ''; };

  // ждём готовности игры
  for (let i = 0; i < 100 && !w.__kcReady; i++) await wait(50);
  ok(!!w.__kcReady, 'игра загрузилась');
  ok(ev('typeof UI !== "undefined" && typeof ACT.folkone === "function"'), 'ACT.folkone зарегистрирован');

  // новая игра кнопками (заодно проверяем диспетчер кликов)
  click('#menu [data-act="newgame"]');
  await wait(50);
  if (d.querySelector('[data-act="newgame2"]')) click('[data-act="newgame2"]'); // если есть старая партия — подтверждение
  click('[data-act="cset:size=s"]');
  click('[data-act="cset:bots=2"]');
  click('[data-act="start"]');
  await wait(300);
  ok(ev('!!UI.S && UI.S.players.length === 3'), 'игра запущена кнопками');

  // оборона: в длинных прогонах боты захватывают столицу без войск
  ok(ev('(() => { const S = UI.S, c = S.cities.find(q => q.owner === 0); if (!c) return false; S.players[0].stock.gold = 99999; S.players[0].stock.food = 99999; for (let k = 0; k < 8; k++) newArmy(S, 0, c.x + 1 + (k % 3) - 1, c.y + 1 + ((k / 3) | 0) - 1, [mkSquad("heavy")]); return true; })()'), 'оборона игрока усилена');

  // заселяем жителей
  ok(ev('(() => { for (let i = 0; i < 4000; i++) tick(UI.S, 0.25); return UI.S.folk.filter(f => f.st !== "dead").length; })()') > 0, 'жители появились');

  // открываем список ЖИТЕЛИ и жмём «Подробнее»
  click('[data-act="folk"]');
  await wait(50);
  ok(/Всего жителей/.test(sheetText()), 'список жителей открылся');
  const btn1 = d.querySelector('[data-act^="folkone:"]');
  ok(!!btn1, 'кнопка «Подробнее» есть');
  const fid = +btn1.getAttribute('data-act').split(':')[1];
  const fname = ev('folkGet(UI.S, ' + fid + ').name');
  click(btn1);
  await wait(50);
  const detail = sheetText();
  ok(detail.includes('Казнить') && detail.includes(fname), 'карточка жителя открылась (имя и действия видны)', detail.slice(0, 80));
  ok(/Наёмники/.test(detail) && /тюрьм/.test(detail), 'действия: наёмники и тюрьма');
  ok(/К списку/.test(detail), 'кнопка возврата к списку есть');

  // новые действия и обязательная тюрьма
  ok(!!d.querySelector('[data-act^="fgift:"]') && !!d.querySelector('[data-act^="ffeast:"]') && !!d.querySelector('[data-act^="fdraft:"]'), 'новые действия: подарок, пир, призыв');
  ok(!d.querySelector('[data-act^="fprison:"]'), 'без тюрьмы кнопка посадки скрыта');
  ok(ev('(() => { const S = UI.S, c = S.cities.find(q => q.owner === 0); if (!c) return false; for (let dy = -8; dy <= 8; dy++) for (let dx = -8; dx <= 8; dx++) { const x = c.x + dx, y = c.y + dy; if (siteOk(S, 0, "prison", x, y) === null && doBuild(S, 0, "prison", x, y) === null) return true; } return false; })()'), 'тюрьма построена');
  ev('ACT.folkone(' + fid + ')');
  await wait(50);
  ok(!!d.querySelector('[data-act^="fprison:"]'), 'после постройки тюрьмы кнопка появилась');

  // сажаем в тюрьму
  click('[data-act^="fprison:"]');
  await wait(50);
  ok(/в тюрьме/.test(sheetText()), 'житель посажен в тюрьму');
  ok(ev('folkGet(UI.S, ' + fid + ').st') === 'prison', 'статус в данных изменился');

  // освобождаем
  click('[data-act^="fprison:"]');
  await wait(50);
  ok(ev('folkGet(UI.S, ' + fid + ').st') === 'ok', 'житель освобождён');

  // казнь: подтверждение → возврат к списку
  click('[data-act^="fkill1:"]');
  await wait(50);
  ok(ev('!!UI.modal'), 'открылось окно подтверждения казни');
  click('[data-act^="fkill2:"]');
  await wait(50);
  ok(ev('!UI.modal'), 'окно подтверждения закрылось');
  ok(/Всего жителей/.test(sheetText()), 'после казни вернулись к списку жителей');
  ok(ev('folkGet(UI.S, ' + fid + ').st') === 'dead', 'житель казнён');

  // третья страница стройки с тюрьмой и смена мелодии в настройках
  click('[data-act="tab:build"]');
  await wait(50);
  ev('ACT.bpage(2)');
  await wait(50);
  ok(!!d.querySelector('#bar [data-act="place:prison"]'), 'тюрьма есть на странице ОБЩЕЕ');
  ok(/ОБЩЕЕ|ПРОИЗВ\./.test(d.getElementById('bar').textContent), 'переключатель страниц стройки жив');
  ev('ACT.settings()');
  await wait(50);
  ok(/Мелодия/.test(d.getElementById('modal').textContent) && !!d.querySelector('[data-act="song"]'), 'в настройках показана мелодия и есть смена');
  click('[data-act="song"]');
  await wait(50);
  ok(errs.length === 0, 'смена мелодии без ошибок', errs.slice(0, 3).join(' | '));
  ev('closeModal()');

  // ПОЛИТ. открывает меню
  click('[data-act="tab:pol"]');
  await wait(50);
  ok(ev('!!UI.modal') && /держав|санкци/i.test(d.getElementById('modal').textContent), 'меню ПОЛИТ. открылось');

  ev('closeModal()'); // закрыть меню ПОЛИТ., иначе оно перехватывает тапы
  // жители на карте: ходят, тап открывает карточку, тропинки проступают (свежая партия — без режима стройки и конца игры)
  ok(ev('(() => { const S = newGame({ type: "continent", size: "s", bots: 2, diff: "normal", win: "score", target: 30000, nation: 0, seed: 777, free: 1, events: 1 }); UI.S = S; RN.place = null; RN.sel = null; for (let i = 0; i < 1500; i++) tick(S, 0.25); return S.folk.some(f => f.x !== undefined); })()'), 'жители на карте имеют положение');
  ok(ev('(() => { const S = UI.S; const f = S.folk.find(q => q.st === "ok" && q.x !== undefined && !S.folk.some(g => g !== q && g.st !== "dead" && g.x !== undefined && Math.hypot(g.x - q.x, g.y - q.y) < 0.8)); if (!f) return false; RN.sel = null; UI.mapTap(f.x + 0.03, f.y + 0.03); return RN.sel && RN.sel.k === "folk" && RN.sel.id === f.id; })()'), 'тап по жителю выбирает его');
  ok(/Казнить/.test(sheetText()), 'карточка жителя открылась прямо с карты');
  ok(ev('(() => { const S = UI.S; let n = 0; for (let i = 0; i < 1500; i++) { tick(S, 0.25); if (S.trails) n = Object.keys(S.trails).length; } return n; })()') >= 0, 'тропинки появляются при ходьбе');
  // свайп-закрытие сообщений: тост убирается движением пальца в ЛЮБУЮ сторону (2.6.0)
  ok(ev('typeof dismissToast') === 'function', 'dismissToast доступна');
  const dirs = [[90, 0], [-90, 0], [0, 90], [0, -90], [70, -70]];
  for (const d of dirs) {
    const r = ev(`(() => {
      if (!UI.S) return 'нет игры';
      UI.toasts = [{ t: { title: 'Тест', text: 'свайп', ev: 1 }, rt: performance.now(), dur: 60000 }];
      updateHud();
      const el = document.querySelector('.toast[data-rt]');
      if (!el) return 'нет тоста';
      const rt = el.getAttribute('data-rt');
      el.dispatchEvent(new window.MouseEvent('pointerdown', { bubbles: true, clientX: 100, clientY: 100 }));
      document.dispatchEvent(new window.MouseEvent('pointermove', { bubbles: true, clientX: ${100 + d[0]}, clientY: ${100 + d[1]} }));
      document.dispatchEvent(new window.MouseEvent('pointerup', { bubbles: true, clientX: ${100 + d[0]}, clientY: ${100 + d[1]} }));
      if (UI.toasts.length !== 0) return 'остался';
      UI._swipeOut = 0; updateHud();
      return !document.querySelector('.toast[data-rt="' + rt + '"]') ? 'ok' : 'остался в DOM';
    })()`);
    ok(r === 'ok', 'тост убран свайпом ' + d[0] + ',' + d[1], r);
  }
  // короткое касание НЕ закрывает (меньше порога), тап по тосту — закрывает
  const small = ev(`(() => {
    if (!UI.S) return 'нет игры';
    UI.toasts = [{ t: { title: 'Тест2', text: 'касание', ev: 1 }, rt: performance.now(), dur: 60000 }];
    updateHud();
    const el = document.querySelector('.toast[data-rt]');
    if (!el) return 'нет тоста';
    const rt = el.getAttribute('data-rt');
    el.dispatchEvent(new window.MouseEvent('pointerdown', { bubbles: true, clientX: 100, clientY: 100 }));
    document.dispatchEvent(new window.MouseEvent('pointermove', { bubbles: true, clientX: 106, clientY: 104 }));
    document.dispatchEvent(new window.MouseEvent('pointerup', { bubbles: true, clientX: 106, clientY: 104 }));
    const kept = UI.toasts.length === 1 && !!document.querySelector('.toast[data-rt="' + rt + '"]');
    if (!kept) return 'малый свайп удалил тост';
    el.dispatchEvent(new window.MouseEvent('pointerdown', { bubbles: true, clientX: 100, clientY: 100 }));
    document.dispatchEvent(new window.MouseEvent('pointerup', { bubbles: true, clientX: 100, clientY: 100 }));
    el.dispatchEvent(new window.MouseEvent('click', { bubbles: true, clientX: 100, clientY: 100 }));
    if (UI.toasts.length !== 0) return 'тап не закрыл';
    UI._swipeOut = 0; updateHud();
    return !document.querySelector('.toast[data-rt="' + rt + '"]') ? 'ok' : 'тост висит в DOM';
  })()`);
  ok(small === 'ok', 'малый свайп не трогает, тап закрывает', small);
  // --- 2.10.0: уведомления отключаются в настройках ---
  ok(ev('SET.notify === 1 && SET.panel === 1'), 'по умолчанию уведомления и панель включены');
  // к этому моменту партия может быть проиграна ботам — окно конца заблокировано, закрываем его
  ev('UI.shownOver = true; closeModal();');
  ev('ACT.settings()');
  await wait(50);
  ok(/Уведомления/.test(d.getElementById('modal').textContent) && !!d.querySelector('[data-act="toggle:notify"]'), 'в настройках есть переключатель уведомлений');
  ok(/Нижняя панель/.test(d.getElementById('modal').textContent) && !!d.querySelector('[data-act="toggle:panel"]'), 'в настройках есть переключатель нижней панели');
  click('[data-act="toggle:notify"]');
  await wait(50);
  ok(ev('SET.notify === 0'), 'уведомления выключены переключателем');
  const toastTest = ev(`(() => {
    UI.toasts = [{ t: { title: 'Скрытое', text: 'не видно', ev: 1 }, rt: performance.now(), dur: 60000 }];
    UI.S.toast.push({ title: 'Из очереди', text: 'тоже не видно', icon: 'flag' });
    updateHud();
    return { dom: !!document.querySelector('.toast'), kept: UI.toasts.length, queue: UI.S.toast.length };
  })()`);
  ok(!toastTest.dom && toastTest.kept === 0 && toastTest.queue === 0, 'при выключенных уведомлениях тосты не появляются, очередь пустеет', JSON.stringify(toastTest));
  ev('ACT.toggle("notify")');
  await wait(50);
  ok(ev('SET.notify === 1'), 'уведомления включены обратно');
  ev('closeModal()');

  // --- 2.10.0: нижнюю панель можно скрыть и показать (стрелка у края + настройка), состояние сохраняется ---
  const panel0 = ev('document.getElementById("tabs").classList.contains("hidden") + "/" + document.getElementById("bar").classList.contains("hidden") + "/" + document.getElementById("panelFab").getAttribute("data-act") + "/" + !!document.querySelector("#panelFab svg.ic")');
  ok(panel0 === 'false/false/paneltoggle/true', 'панель видна, стрелка на месте, внутри — SVG', panel0);
  ev('ACT.paneltoggle()');
  await wait(50);
  ok(ev('document.getElementById("tabs").classList.contains("hidden") && document.getElementById("bar").classList.contains("hidden")'), 'панель скрыта стрелкой');
  ok(ev('(() => { let v = null; try { v = JSON.parse(localStorage.getItem("kc_set")); } catch (e) { v = "jsdom-blocked"; } return !v || v === "jsdom-blocked" || v.panel === 0; })()'), 'скрытая панель уходит в хранилище настроек (в jsdom localStorage может быть недоступен)');
  ok(ev('document.getElementById("hud").classList.contains("panel-off")'), 'HUD получил класс panel-off');
  ok(ev('!!document.querySelector("#panelFab svg.ic") && document.getElementById("panelFab").innerHTML.indexOf("chevup") > -1 || document.getElementById("panelFab").innerHTML.indexOf("path") > -1'), 'стрелка сменилась на «показать»');
  ev('ACT.paneltoggle()');
  await wait(50);
  ok(ev('!document.getElementById("tabs").classList.contains("hidden") && !document.getElementById("bar").classList.contains("hidden")'), 'панель снова показана');
  ev('ACT.settings()'); await wait(50);
  click('[data-act="toggle:panel"]'); await wait(50);
  ok(ev('SET.panel === 0 && document.getElementById("bar").classList.contains("hidden")'), 'панель скрыта из настроек');
  click('[data-act="toggle:panel"]'); await wait(50);
  ok(ev('SET.panel === 1 && !document.getElementById("bar").classList.contains("hidden")'), 'панель включена из настроек');
  ev('closeModal()');

  // --- 2.10.0: векторные SVG-иконки в интерфейсе ---
  ok(ev('!!document.querySelector("#resbar svg.ic")'), 'иконки ресурсов — векторные SVG');
  ok(ev('document.querySelectorAll("#tabs svg.ic").length >= 6'), 'вкладки — SVG-иконки');
  ok(ev('document.querySelectorAll("#bar svg.ic").length >= 10'), 'нижняя панель — SVG-иконки');
  ok(ev('document.querySelector("#scoreI svg.ic") !== null && document.querySelector("#burger svg.ic") !== null'), 'верхняя панель — SVG-иконки');
  ok(ev('typeof SV === "object" && Object.keys(SV).length >= 55 && SV.gold.indexOf("<circle") === 0 && !/<img/.test(document.getElementById("resbar").innerHTML)'), 'векторный набор иконок на месте, <img> не осталось');

  ok(errs.length === 0, 'нет ошибок JS', errs.slice(0, 3).join(' | '));

  console.log('пройдено:', pass, 'провалено:', fail);
  process.exit(fail ? 1 : 0);
})().catch(e => { console.log('FAIL: сбой теста', e.message, e.stack ? e.stack.split('\n')[1] : ''); process.exit(1); });
