'use strict';
// Проверки 2.11.0: иконки сверху покрупнее, свечение городов убрано, 10 песен.
const fs = require('fs'), path = require('path'), assert = require('assert');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const root = path.resolve(__dirname, '..');
const src = f => fs.readFileSync(path.join(root, f), 'utf8');

ok(G.GAME_VERSION === '2.11.0', 'GAME_VERSION = 2.11.0', G.GAME_VERSION);

{
  const m = src('www/js/main.js');
  ok(m.includes("IMG('medal', 27)"), 'медаль счёта покрупнее (27px)');
  ok(m.includes("IMG('menu', 27)"), 'иконка меню покрупнее (27px)');
  ok(m.includes("IMG('mail', 38)"), 'конверт писем покрупнее (38px)');
  const u = src('www/js/ui.js');
  ok(u.includes("${IMG(k, 22)}"), 'иконки ресурсов 22px');
  const c = src('www/style.css');
  ok(c.includes('height: 24px; }') && c.includes('#resbar .r'), 'строка полосы ресурсов выше');
}
{
  const r = src('www/js/render.js');
  ok(!r.includes('ct.level * 12'), 'радиальное свечение над городами убрано');
  ok(!r.includes("(h[0] + 1.5) * 0.5"), 'блики окон города ночью убраны');
  const d = src('www/js/data.js');
  ok(!d.includes("R_(c, kx2, oy - 8, 12, 4, '#f2c230')"), 'неоновая корона столицы убрана (matte-gold)');
}
{
  const a = src('www/js/audio.js');
  for (const n of ['Утро в лесу', 'Старая дорога', 'Охота на оленя', 'Тихая заводь', 'Северный форт', 'Таверна у порта']) ok(a.includes("'" + n + "'"), 'новая мелодия в SONGS: ' + n);
  for (const f of ['05-utro-v-lesu', '06-staraya-doroga', '07-ohota-na-olenya', '08-tihaya-zavod', '09-severnyy-fort', '10-taverna-u-porta']) {
    ok(a.includes(f), 'трек в TRACKS: ' + f);
    const fp = path.join(root, 'www/soundtrack', f + '.mp3');
    ok(fs.existsSync(fp) && fs.statSync(fp).size > 1000000, 'файл саундтрека на месте: ' + f + '.mp3');
  }
  ok((a.match(/\{ n: '/g) || []).length >= 20, 'всего >= 20 записей (10 мелодий + 10 треков)');
  ok(src('tools/render_soundtrack.py').includes('05-utro-v-lesu'), 'рендерер знает новые мелодии');
}
console.log('Итого v2.11.0:', pass, 'ок,', fail, 'ошибок');
process.exit(fail ? 1 : 0);
