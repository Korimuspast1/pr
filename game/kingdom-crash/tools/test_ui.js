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

  // ПОЛИТ. открывает меню
  click('[data-act="tab:pol"]');
  await wait(50);
  ok(ev('!!UI.modal') && /держав|санкци/i.test(d.getElementById('modal').textContent), 'меню ПОЛИТ. открылось');

  // ошибок JS не было
  ok(errs.length === 0, 'нет ошибок JS', errs.slice(0, 3).join(' | '));

  console.log('пройдено:', pass, 'провалено:', fail);
  process.exit(fail ? 1 : 0);
})().catch(e => { console.log('FAIL: сбой теста', e.message, e.stack ? e.stack.split('\n')[1] : ''); process.exit(1); });
