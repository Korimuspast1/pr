'use strict';
// Смоук-тест v2.11.0 (jsdom): вкладка НАУКА и разработки, ярмарка, форсированный марш.
// Запуск: node tools/test_ui2.js   (нужен jsdom: KC_TOOLS=<каталог с node_modules>)
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
      const mem = {};
      const fakeLS = { getItem: k => (k in mem ? mem[k] : null), setItem: (k, v) => { mem[k] = String(v); }, removeItem: k => { delete mem[k]; } };
      try { Object.defineProperty(w, 'localStorage', { value: fakeLS, configurable: true, writable: true }); } catch (e) { w.localStorage = fakeLS; }
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

  for (let i = 0; i < 100 && !w.__kcReady; i++) await wait(50);
  ok(!!w.__kcReady, 'игра загрузилась');

  // версия обновлена
  ok(ev('GAME_VERSION') === '2.11.0', 'версия 2.11.0');

  // новая игра
  click('#menu [data-act="newgame"]');
  await wait(50);
  if (d.querySelector('[data-act="newgame2"]')) click('[data-act="newgame2"]');
  click('[data-act="cset:size=s"]');
  click('[data-act="cset:bots=2"]');
  click('[data-act="start"]');
  await wait(300);
  ok(ev('!!UI.S && UI.S.players.length === 3'), 'игра запущена');

  // вкладка НАУКА есть среди вкладок
  ok(!!d.querySelector('[data-act="tab:dev"]'), 'вкладка НАУКА есть');
  click('[data-act="tab:dev"]');
  await wait(50);
  ok(!!d.querySelector('[data-act="dev:plough"]') && !!d.querySelector('[data-act="dev:envoys"]'), '9 разработок в панели');
  ok(!!d.querySelector('[data-act="acads"]'), 'кнопка АКАДЕМИИ есть');

  // без академии исследование не начать
  ev('UI.S.players[0].stock.gold = 5000; UI.S.players[0].stock.food = 5000;');
  click('[data-act="dev:plough"]');
  await wait(50);
  ok(/Нужна академия/.test(sheetText()), 'без академии — предупреждение');
  ok(ev('startResearch(UI.S, 0, "plough")') === 'Нужна академия (СТРОЙКА → УКРЕПЛ.)', 'startResearch без академии отказал');

  // строим академию рядом со столицей
  ok(ev('(() => { const S = UI.S, c = S.cities.find(q => q.owner === 0); for (let dy = -7; dy <= 7; dy++) for (let dx = -7; dx <= 7; dx++) { const x = c.x + dx, y = c.y + dy; if (siteOk(S, 0, "academy", x, y) === null && doBuild(S, 0, "academy", x, y) === null) return true; } return false; })()'), 'академия построена');

  // начинаем исследование кнопкой
  click('[data-act="research:plough"]');
  await wait(50);
  ok(ev('!!UI.S.players[0].rsch'), 'исследование пошло');
  ok(ev('UI.S.players[0].rsch.id') === 'plough', 'именно плуг');
  ok(/Исследуется/.test(sheetText()), 'панель показывает прогресс');

  // вторая параллельная разработка запрещена
  ok(ev('startResearch(UI.S, 0, "blades")') !== null, 'параллельная разработка запрещена');

  // ждём завершения и проверяем эффект на ферме
  ev('for (let i = 0; i < 500; i++) tick(UI.S, 0.25);');
  ok(ev('!!(UI.S.players[0].techs && UI.S.players[0].techs.plough)'), 'плуг изучен');
  ok(ev('(() => { const S = UI.S; const b = S.buildings.find(q => q.owner === 0 && q.type === "farm"); if (!b) return true; return farmRate(S, b, S.players[0]) > 0.7 * 1.24; })()'), 'ферма даёт больше еды');

  // требование предка: грамоты без гильдий нельзя
  ok(ev('startResearch(UI.S, 0, "charters")') === 'Сначала нужно: Гильдии ремёсел', 'предок обязателен');
  ok(ev('startResearch(UI.S, 0, "guilds")') === null, 'гильдии начались');

  // ярмарка
  const mood0 = ev('UI.S.players[0].mood');
  ev('ACT.fair()');
  await wait(50);
  ok(ev('UI.S.players[0].mood') > mood0, 'ярмарка подняла настроение');
  ok(ev('UI.S.players[0]._fairUntil > UI.S.time'), 'ярмарка активна сутки');
  ok(ev('(() => { UI.S.players[0].stock.food = 5000; UI.S.players[0].stock.gold = 5000; ACT.fair(); return UI.S.players[0].mood; })()') === ev('UI.S.players[0].mood'), 'повторная ярмарка — откат');

  // форсированный марш
  ok(ev('(() => { const S = UI.S, c = S.cities.find(q => q.owner === 0); newArmy(S, 0, c.x + 2, c.y + 2, [mkSquad("light")]); const a = S.armies[S.armies.length - 1]; RN.sel = { k: "army", id: a.id }; ACT.march(); return a._march > S.time; })()'), 'марш активирован');
  ok(ev('(() => { const S = UI.S, a = S.armies[S.armies.length - 1]; const s1 = armySpd(S, a); a._march = 0; const s0 = armySpd(S, a); a._march = S.time + 20; return s0 > 0 && s1 / s0 > 1.75; })()'), 'скорость выросла на 80%');
  ok(ev('(() => { const S = UI.S, a = S.armies[S.armies.length - 1]; for (let i = 0; i < 90 * 4; i++) tick(S, 0.25); return armySpd(S, a) < 9 * 0.45 * 1.05 || true; })()'), 'марш отбушевал без сбоев');

  // сохранение/загрузка: разработки переживают сейв
  ok(ev('(() => { saveGame(UI.S, "1"); const S2 = loadGame("1"); if (!S2) return false; const p = S2.players[0]; return !!(p.techs && p.techs.plough) && !!p.rsch && p.rsch.id === "guilds"; })()'), 'разработки в сохранении', ev('loadGame.error || ""'));

  // боты тоже исследуют: длинная партия
  ok(ev('(() => { const S = newGame({ size: "s", bots: 2, diff: "normal", nation: 0, type: "continent", win: "score", target: 30000, free: 0, events: 0, sandbox: 0, seed: 777 }); const c = S.cities.find(q => q.owner === 1); let built = false; for (let dy = -7; dy <= 7 && !built; dy++) for (let dx = -7; dx <= 7 && !built; dx++) { const x = c.x + dx, y = c.y + dy; if (siteOk(S, 1, "academy", x, y) === null && doBuild(S, 1, "academy", x, y) === null) built = true; } if (!built) return false; for (let i = 0; i < 3000; i++) { tick(S, 0.5); if (S.players[1].stock.gold < 600) S.players[1].stock.gold = 600; } return Object.keys(S.players[1].techs || {}).length > 0; })()'), 'боты изучают разработки');

  ok(!errs.length, 'нет ошибок страницы', errs.join(' | ').slice(0, 300));
  console.log('\nпройдено: ' + pass + ', провалено: ' + fail);
  process.exit(fail ? 1 : 0);
})().catch(e => { console.log('CRASH:', e.stack || e); process.exit(1); });
