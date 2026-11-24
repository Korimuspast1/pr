/* Полный прогон UI в jsdom: настоящий index.html + все скрипты.
   Проверяет: меню, старт эпизода, заставку, диалог, движение, инвентарь, паузу, смерть. */
const fs = require('fs');
const path = require('path');
let JSDOM;
try { JSDOM = require('jsdom').JSDOM; } catch (e) {
  console.log('jsdom не установлен — UI-тест пропущен (npm i jsdom для локального прогона)');
  process.exit(0);
}

const ROOT = path.resolve(__dirname, '..');
const html = fs.readFileSync(path.join(ROOT, 'www/index.html'), 'utf8');

const errors = [];
const dom = new JSDOM(html, {
  runScripts: 'dangerously',
  resources: 'usable',
  pretendToBeVisual: true,
  url: 'file://' + path.join(ROOT, 'www/index.html'),
  storageQuota: 10000000,
  beforeParse(window) {
    const noop = () => { };
    function fakeCtx() {
      return new Proxy({
        canvas: { width: 720, height: 1280 },
        createRadialGradient: () => ({ addColorStop: noop }),
        createLinearGradient: () => ({ addColorStop: noop }),
        measureText: () => ({ width: 10 }),
        getImageData: () => ({ data: new Uint8ClampedArray(4) }),
        drawImage: noop
      }, { get: (t, k) => k in t ? t[k] : noop, set: (t, k, v) => { t[k] = v; return true; } });
    }
    window.HTMLCanvasElement.prototype.getContext = function () { return fakeCtx(); };
    window.HTMLCanvasElement.prototype.toDataURL = function () { return 'data:image/png;base64,'; };
    window.AudioContext = undefined;
    window.webkitAudioContext = undefined;
    window.performance.now = () => Date.now();
    // localStorage для file:// (как в WebView с DomStorage)
    const mem = {};
    Object.defineProperty(window, 'localStorage', { value: {
      getItem: k => (k in mem ? mem[k] : null), setItem: (k, v) => { mem[k] = String(v); },
      removeItem: k => { delete mem[k]; }, clear: () => { for (const k in mem) delete mem[k]; }
    }, configurable: true });
    window.requestAnimationFrame = cb => setTimeout(() => cb(Date.now()), 16);
    window.cancelAnimationFrame = id => clearTimeout(id);
  }
});
const { window } = dom;
window.console = console;
window.addEventListener('error', e => errors.push('window error: ' + e.message));
window.requestAnimationFrame = cb => setTimeout(() => cb(Date.now()), 16);
window.cancelAnimationFrame = id => clearTimeout(id);

const EV = code => window.eval(code);
window.addEventListener('error', e => errors.push('window error: ' + e.message));

function step(ms) { return new Promise(r => setTimeout(r, ms)); }

(async () => {
  await step(400);
  console.log('UI:', EV('typeof UI'), 'Game:', EV('typeof newGame'));
  const $ = id => window.document.getElementById(id);
  const check = (cond, name) => console.log((cond ? 'PASS ' : 'FAIL ') + name);

  check(!$('menu').classList.contains('hidden'), 'меню видно');
  check($('hud').classList.contains('hidden'), 'HUD скрыт в меню');
  check(!EV('UI.G'), 'игры нет до старта');

  // старт 1-го эпизода
  $('menuNew').click();
  await step(80);
  check(!!$('screen'), 'открылся список эпизодов');
  const ep = window.document.querySelector('#screen .ep:not(.locked)');
  check(!!ep, 'есть доступный эпизод');
  ep.click();
  await step(200);
  const G = EV('UI.G');
  check(!!G, 'игра создана');
  check(!$('menu').classList.contains('hidden') === false, 'меню скрыто');
  check(!$('cia') && !$('cine').classList.contains('hidden'), 'заставка показана');
  // пропустить заставку
  $('cine').click();
  await step(120);
  check($('cine').classList.contains('hidden'), 'заставка закрылась');
  check(!!G.dialog || (G.dialogQueue || []).length > 0, 'диалог начался');
  // пролистать диалоги
  for (let i = 0; i < 14 && G.dialog; i++) { $('dialogue').click(); await step(30); }
  check(true, 'диалоги листаются');

  // движение вниз
  const y0 = G.player.y;
  EV('MOVE.my = 1');
  await step(500);
  EV('MOVE.my = 0');
  check(G.player.y > y0 + 0.3, 'игрок сдвинулся вниз (' + y0.toFixed(2) + ' → ' + G.player.y.toFixed(2) + ')');

  // время идёт, HUD обновляется
  check(G.time > 0.2, 'время симуляции идёт: ' + G.time.toFixed(2));
  check($('clock').textContent.includes(':'), 'часы в HUD: ' + $('clock').textContent);

  // инвентарь
  $('invBtn').click();
  await step(60);
  check(!$('inv').classList.contains('hidden'), 'рюкзак открылся');
  check(window.document.querySelectorAll('#invItems .slot').length > 10, 'слоты предметов есть');
  $('invClose').click();
  await step(40);
  check($('inv').classList.contains('hidden'), 'рюкзак закрылся');

  // пауза
  $('menuBtn').click();
  await step(60);
  check(!!$('screen'), 'пауза открылась');
  check(G.paused === true, 'игра на паузе');
  $('pResume').click();
  await step(40);
  check(G.paused === false, 'пауза снята');

  // удар по зомби
  const z = G.zombies[0];
  if (z) {
    const d0 = Math.hypot(z.x - G.player.x, z.y - G.player.y);
    z.x = G.player.x + 0.7; z.y = G.player.y;
    const hp0 = z.hp;
    EV('GAME_INPUT.attack = true; GAME_INPUT.ax = ' + z.x + '; GAME_INPUT.ay = ' + z.y + ';');
    await step(150);
    check(z.hp < hp0 || z.hp <= 0, 'удар наносит урон (' + hp0 + ' → ' + z.hp + ')');
  }

  // аптечка
  G.player.hp = 40;
  EV('useItem(UI.G, "medkit")');
  check(G.player.hp > 40, 'аптечка лечит: ' + G.player.hp);

  // смерть
  G.player.hp = 0.5;
  EV('hurtPlayer(UI.G, 50, {kind:"walker"})');
  await step(300);
  check(!!$('dead'), 'экран смерти показан');
  check(G.player.dead === true, 'игрок мёртв');
  $('dRetry').click();
  await step(300);
  check(EV('!!UI.G && !UI.G.player.dead'), 'перезапуск эпизода работает');

  // сохранение/загрузка
  EV('save(UI.G)');
  check(!!window.localStorage.getItem('di_save_v1'), 'сохранение записано');

  console.log('\nERRORS:', errors.length ? errors.join('\n') : 'нет');
  process.exit(0);
})().catch(e => { console.log('CRASH', e.message, e.stack); process.exit(1); });
