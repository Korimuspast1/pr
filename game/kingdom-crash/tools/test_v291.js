'use strict';
// Проверки 2.9.1: хотфикс — песчинки на лугу/пляже убраны, караван зеркалится целиком.
const fs = require('fs'), path = require('path');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const root = path.resolve(__dirname, '..');
const src = f => fs.readFileSync(path.join(root, f), 'utf8');

ok(['2.9.1', '2.10.0', '2.11.0'].includes(G.GAME_VERSION), 'GAME_VERSION >= 2.9.1', G.GAME_VERSION);

{
  const t = src('www/js/terrain.js');
  ok(!t.includes('fl > 0.9972'), 'цветы-«песчинки» убраны из луга');
  ok(!t.includes('метёлки травы'), 'метёлки/одиночные пиксели травы убраны');
  ok(!t.includes('gr < 0.004 && L > 0.56'), 'камешки на пляже убраны');
}
{
  const r = src('www/js/render.js');
  ok(r.includes('c.scale(-1, 1)'), 'drawCars: зеркало всей телеги одной трансформацией');
  ok(r.includes('оглобли'), 'drawCars: новая телега (оглобли) на месте');
  ok(!/const sq = \(dx, dy, w, h, col\)/.test(r), 'drawCars: старое одностороннее зеркало удалено');
}
console.log('\nИтого v2.9.1: ' + pass + ' ок, ' + fail + ' ошибок');
process.exit(fail ? 1 : 0);
