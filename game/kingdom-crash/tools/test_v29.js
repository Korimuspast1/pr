'use strict';
// Проверки 2.9.0: мягкая графика (кубика Кэтмулла-Рома, виньетка, свечение,
// скруглённый/размытый «каменный» UI), скриншоты рендера в репозитории.
const fs = require('fs'), path = require('path');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const root = path.resolve(__dirname, '..');
const src = f => fs.readFileSync(path.join(root, f), 'utf8');

// ===== Версия =====
{
  ok(/^2\.(9|10)\.\d+$/.test(G.GAME_VERSION), 'GAME_VERSION 2.9.x/2.10.x (точную цифру держит test_v210.js)', G.GAME_VERSION);
}

// ===== Террейн: кубическая интерполяция вместо ступенчатой =====
{
  const t = src('www/js/terrain.js');
  ok(/const crs = \(p0, p1, p2, p3, t\)/.test(t), 'crs(): ядро Кэтмулла-Рома на месте');
  ok(/const bicub = \(T, tx, ty, fx0, fy0\)/.test(t), 'bicub(): двумерная кубика на месте');
  ok(!/const Wm = W - 1, Hm = H - 1/.test(t), 'размеры карты НЕ захватываются при загрузке модуля (регрессия «чёрная земля»)');
  ok(/bicub\(hgt, tx, ty, fx0, fy0\)/.test(t), 'высота рельефа сэмплируется кубикой');
  ok(/const bil = T => bicub\(T, tx, ty, fx0, fy0\);/.test(t), 'цветовой проход сэмплирует кубикой');
}

// ===== Рендер: мягкий свет =====
{
  const r = src('www/js/render.js');
  for (const fn of ['makeGlow', 'makeSoftBlob', 'makeVignette']) ok(r.includes('function ' + fn), 'render.js: ' + fn + '()');
  ok(/setCompositeOperation|globalCompositeOperation = 'multiply'/.test(r), 'бегущие тени облаков (multiply)');
  ok(/globalCompositeOperation = 'lighter'/.test(r), 'аддитивное свечение окон и светлячков');
  ok(r.includes('RN.vig'), 'виньетка кэшируется в RN.vig');
  ok(r.includes('RN.glow') && r.includes('RN.clShadow'), 'кэши мягких спрайтов в RN');
}

// ===== Стили: «мягкий камень» =====
{
  const css = src('www/style.css');
  ok(css.includes('2.9.0: «мягкий камень»'), 'мягкий блок стилей на месте');
  ok(/#cv \{ -webkit-filter: saturate\(\.74\) brightness\(1\.08\) contrast\(\.92\)/.test(css), 'кадр чуть ярче и светлее');
  ok(/\.px \{ border-radius: 5px;/.test(css), 'кнопки скруглены');
}

// ===== Скриншоты рендера в репозитории =====
{
  for (const f of ['before/spring_rain.png', 'before/fest_close_day.png', 'before/winter_snow_cart.png',
    'after/spring_rain.png', 'after/fest_close_day.png', 'after/winter_snow_cart.png']) {
    try { const st = fs.statSync(path.join(root, 'docs/screenshots/2.9.0', f)); ok(st.size > 20000, 'скриншот ' + f + ' на месте (' + st.size + ' б)'); }
    catch (e) { ok(false, 'скриншот ' + f + ' на месте', 'missing'); }
  }
}

console.log('\nИтого v2.9.0: ' + pass + ' ок, ' + fail + ' ошибок');
process.exit(fail ? 1 : 0);
