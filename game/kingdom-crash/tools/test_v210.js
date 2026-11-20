'use strict';
// Проверки 2.10.0: векторные SVG-иконки, отключаемые уведомления, скрываемая нижняя панель.
const fs = require('fs'), path = require('path');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const root = path.resolve(__dirname, '..');
const src = f => fs.readFileSync(path.join(root, f), 'utf8');

ok(G.GAME_VERSION === '2.10.0', 'GAME_VERSION = 2.10.0', G.GAME_VERSION);
ok(typeof G.IMG === 'function' && G.IMG('gold', 24).indexOf('<svg') === 0, 'IMG() отдаёт inline-SVG');
ok(typeof G.SV === 'object' && Object.keys(G.SV).length >= 55, 'набор векторных иконок на месте (>=55)', String(Object.keys(G.SV || {}).length));

{
  const d = src('www/js/data.js');
  ok(!d.includes('ICONMAP') && !d.includes('initIcons') && !d.includes('ICONURL'), 'пиксельный конвейер иконок удалён из data.js');
  ok(!src('www/js/main.js').includes('initIcons'), 'вызов initIcons удалён из main.js');
  ok(!src('www/js/render.js').includes('ICON.star'), 'звезда столицы — вектором на холсте');
}
{
  const u = src('www/js/ui.js');
  ok(/notify:\s*1/.test(u) && /panel:\s*1/.test(u), 'SET.notify / SET.panel по умолчанию включены');
  ok(u.includes("row('notify'") && u.includes("row('panel'"), 'переключатели в настройках');
  ok(u.includes('applyPanel') && u.includes('panel-off'), 'applyPanel() и класс HUD panel-off');
  ok(u.includes('paneltoggle'), 'кнопка-стрелка panelFab (data-act=paneltoggle)');
}
{
  const h = src('www/index.html');
  ok(h.includes('id="panelFab"'), 'кнопка panelFab есть в разметке');
  const c = src('www/style.css');
  ok(c.includes('#panelFab'), 'стили panelFab на месте');
}
console.log('Итого v2.10.0:', pass, 'ок,', fail, 'ошибок');
process.exit(fail ? 1 : 0);
