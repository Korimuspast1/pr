'use strict';
// Бенчмарк кадра и тика Kingdom Crash: где теряется время.
// Запуск: node tools/bench.js [размер_карты]
const { createCanvas, ImageData } = require(process.env.PROMO_CANVAS || '/home/user/pr/.cache/promo-tools/node_modules/@napi-rs/canvas');
const fs = require('fs'), path = require('path');
const W = 1080, H = 1920;
global.ImageData = ImageData;
global.window = { devicePixelRatio: 1, innerWidth: W, innerHeight: H, addEventListener() { } };
global.document = {
  createElement(tag) {
    if (tag === 'canvas') return createCanvas(2, 2);
    return { style: {}, getContext() { return null; }, addEventListener() { } };
  },
  getElementById() { return null; }, addEventListener() { }
};
global.localStorage = { getItem() { return null; }, setItem() { }, removeItem() { } };
if (!global.performance) global.performance = { now: () => Date.now() };

const DIR = path.resolve(__dirname, '../www/js');
const files = ['data.js', 'map.js', 'game.js', 'ai.js', 'terrain.js', 'render.js'];
let src = '';
for (const f of files) src += '\n;/* ' + f + ' */\n' + fs.readFileSync(path.join(DIR, f), 'utf8').replace(/^'use strict';/, '');
src = src.replace('function canSee(S, o) { return o.owner === 0 || allied(S, 0, o.owner) || visibleAt(S, o.x, o.y); }', 'function canSee() { return true; }');
src = src.replace('function visibleAt(', 'function _visibleAt(') + '\nfunction visibleAt() { return true; }\n';
// --- абляция секций frame() для поиска подвисания (BENCH_ABLATE=tint|clouds|vignette|afterarmies) ---
const ABL = process.env.BENCH_ABLATE || '';
if (ABL === 'terdraw') src = src.replace('  TERR.draw(c, cam, z, RN.cv.width, RN.cv.height); c.imageSmoothingEnabled = false;', '  // TERR.draw ablated');
if (ABL === 'tint') src = src.replace(/{ \/\/ смена времени суток[\s\S]*?\n  }\n  { \/\/ светлячки/, '{ // светлячки');
if (ABL === 'clouds') src = src.replace("  const cw = S.W * TS, off = (tm * 2.5) % cw; c.drawImage(RN.cloud, off, 0, cw, S.H * TS); c.drawImage(RN.cloud, off - cw, 0, cw, S.H * TS);\n", "  // clouds ablated\n");
if (ABL === 'vignette') src = src.replace(/{ \/\/ виньетка[\s\S]*?\n  }\n}/, '}');
if (ABL === 'fx') src = src.replace('  for (const f of S.fx) {', '  for (const f of []) {');
if (ABL === 'rain') src = src.replace('if (rain || snow || leaves) {', 'if (false) {');
if (ABL === 'trails') src = src.replace('if (S.trails) for (const k in S.trails) {', 'if (false) for (const k in S.trails) {');
// --- секционная разметка frame() для одного медленного кадра (BENCH_SECT=1) ---
if (process.env.BENCH_SECT) {
  src = src.replace('function frame(S, now) {', 'function frame(S, now) { const __t0 = performance.now(); const __marks = [["start", 0]]; const __mark = n => __marks.push([n, Math.round(performance.now() - __t0)]);');
  const R0 = src.indexOf('/* render.js */'); // метки ставим только в render.js — иначе попадаем в game.js
  const put = (anchor, name) => { const i = src.indexOf(anchor, R0); if (i < 0) throw new Error('anchor not found: ' + anchor); src = src.slice(0, i) + '__mark(' + JSON.stringify(name) + ');' + src.slice(i); };
  put('  TERR.draw(c, cam, z, RN.cv.width, RN.cv.height);', 'terr-pre');
  put('  // искры воды', 'sparkles');
  put('  // здания', 'buildings');
  put('  // города', 'cities');
  put('  // маршруты', 'routes');
  put('  // корабли', 'ships');
  put('  // обучение', 'barracks');
  put('  // армии', 'armies');
  put('  // эффекты боя', 'fx');
  put('  // выбранный город', 'sel');
  put('  drawCars(c, S, tm, vx0, vy0, vx1, vy1);', 'cars');
  put('  // облака\n  const cw = S.W * TS', 'clouds');
  put('  { // смена времени суток', 'tint');
  put('  { // светлячки', 'fireflies');
  put('  // значки армий', 'markers');
  put('  { // виньетка', 'vignette');
  src = src.replace('  { // виньетка', '  if (process.env.SECT_ALL || performance.now() - __t0 > 50) console.log((process.env.SECT_ALL ? "FRAME" : "SLOW FRAME"), JSON.stringify(__marks));\n  { // виньетка');
}
if (process.env.BENCH_TERR) {
  const t0anchor = '/* terrain.js */';
  const R1 = src.indexOf(t0anchor);
  let head = src.slice(0, R1), tail = src.slice(R1);
  tail = tail.replace('  function raster(x0, y0, w, h, step) {', '  function raster(x0, y0, w, h, step) { const __rt = nowMs();');
  tail = tail.replace('    return img;\n  }', '    globalThis.__rasterMs = (globalThis.__rasterMs || 0) + (nowMs() - __rt); globalThis.__rasterN = (globalThis.__rasterN || 0) + 1; return img;\n  }');
  tail = tail.replace('  function stepJobs(budgetMs) {', '  function stepJobs(budgetMs) { const __st = nowMs();');
  tail = tail.replace('        if (nowMs() - t0 >= budgetMs) return;', '        if (nowMs() - t0 >= budgetMs) { globalThis.__stepMs = (globalThis.__stepMs || 0) + (nowMs() - __st); return; }');
  tail = tail.replace('      finishJob(job); jobs.delete(key);\n    }\n  }', '      finishJob(job); jobs.delete(key);\n    }\n    globalThis.__stepMs = (globalThis.__stepMs || 0) + (nowMs() - __st);\n  }');
  tail = tail.replace('  function draw(c, cam, z, vw, vh) {\n    if (!S) return; stamp++;', '  function draw(c, cam, z, vw, vh) {\n    if (!S) return; stamp++; const __d0 = nowMs();');
  tail = tail.replace('      c.restore(); return;\n    }', '      globalThis.__drawMs = (globalThis.__drawMs || 0) + (nowMs() - __d0); c.restore(); return;\n    }');
  tail = tail.replace('    c.restore();\n  }\n  return { init,', '    globalThis.__drawMs = (globalThis.__drawMs || 0) + (nowMs() - __d0); c.restore();\n  }\n  return { init,');
  src = head + tail;
}
const names = ['newGame', 'tick', 'RN', 'frame', 'buildTerrain', 'drawTerr', 'centerOn', 'clampCam', 'TS', 'DAY', 'TR', 'SQ',
  'mkSquad', 'newArmy', 'doBuild', 'saveGame', 'armySoldiers', 'TERR'];
const body = src + '\n;const __o = {}; ' + names.map(n => `try { __o.${n} = ${n}; } catch (e) { __o.${n} = undefined; }`).join(' ') + ' return __o;';
const G = new Function('document', 'localStorage', 'window', 'performance', 'ImageData', body)(global.document, global.localStorage, global.window, global.performance, ImageData);

const cv = createCanvas(W, H);
const ctx = cv.getContext('2d');
Object.assign(G.RN, { cv, ctx, w: W, h: H, dpr: 1, showTerr: true });

function stats(arr) {
  const s = arr.slice().sort((a, b) => a - b), n = s.length;
  const mean = s.reduce((a, b) => a + b, 0) / n;
  const q = p => s[Math.min(n - 1, Math.floor(p * n))];
  return { mean: mean.toFixed(2), p50: q(0.5).toFixed(2), p95: q(0.95).toFixed(2), max: s[n - 1].toFixed(2) };
}
const fmtS = o => `mean ${o.mean}ms  p50 ${o.p50}  p95 ${o.p95}  max ${o.max}`;

const S = G.newGame({ type: 'continent', size: process.argv[2] || 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 4242, free: 1, events: 1 });
const p0 = S.players[0]; for (const k of ['gold', 'wood', 'food', 'iron']) p0.stock[k] = 99999;
const cap = S.cities.find(c => c.owner === 0);
const types = ['farm', 'sawmill', 'mill', 'market', 'barracks', 'tower', 'house', 'house', 'forge'];
for (const [i, type] of types.entries()) {
  const d = [[3, 0], [4, 1], [3, 3], [0, 4], [-2, 3], [-3, 0], [-2, -2], [1, -3], [4, -2]][i];
  let okb = false;
  for (let r = 0; r < 6 && !okb; r++) for (let dy = -r; dy <= r && !okb; dy++) for (let dx = -r; dx <= r && !okb; dx++) okb = G.doBuild(S, 0, type, cap.x + 2 + d[0] + dx, cap.y + 2 + d[1] + dy) === null;
}
G.newArmy(S, 0, cap.x + 5, cap.y + 5, [G.mkSquad('cmd'), G.mkSquad('heavy'), G.mkSquad('archer')]);
G.newArmy(S, 1, cap.x + 12, cap.y + 4, [G.mkSquad('light'), G.mkSquad('cav')]);
for (let t = 0; t < 60; t++) G.tick(S, 0.25);
G.buildTerrain(S);
console.log('map', S.W + 'x' + S.H, 'buildings', S.buildings.length, 'armies', S.armies.length);

// --- прогрев чанков вокруг базы ---
G.centerOn(cap.x + 2, cap.y + 2);
for (let i = 0; i < 40; i++) G.frame(S, 1000 + i * 100);

// --- 0) достроить всё разом (стоимость полной пересборки) ---
{ const a = performance.now(); G.TERR.drain(); console.log('drain  всего        :', (performance.now() - a).toFixed(0) + 'ms, участков', G.TERR.count()); }
// --- 1) стабильный кадр в покое (зум 1) ---
globalThis.__stepMs = 0; globalThis.__rasterN = 0; globalThis.__rasterMs = 0; globalThis.__drawMs = 0;
{
  const ts = [], slow = [];
  for (let i = 0; i < 120; i++) { const a = performance.now(); G.frame(S, 5000 + i * 100); const d = performance.now() - a; ts.push(d); if (d > 30) slow.push([i, d.toFixed(0)]); }
  console.log('frame  покой          :', fmtS(stats(ts)));
  console.log('медленные кадры:', JSON.stringify(slow));
  console.log('terr покой  stepMs', (globalThis.__stepMs || 0).toFixed(0), 'rasterN', globalThis.__rasterN || 0, 'drawMs', (globalThis.__drawMs || 0).toFixed(0));
}
// --- 2) кадр при панораме через всю карту (чанки достраиваются) ---
globalThis.__stepMs = 0; globalThis.__rasterN = 0; globalThis.__rasterMs = 0; globalThis.__drawMs = 0;
{
  const ts = [];
  const span = S.W * G.TS - G.RN.w / G.RN.cam.z;
  for (let i = 0; i < 90; i++) {
    G.RN.cam.x = (i / 89) * span; G.RN.cam.y = (i / 89) * span * 0.4;
    const a = performance.now(); G.frame(S, 20000 + i * 100); const d = performance.now() - a; ts.push(d); if (d > 50) console.log('slow панорама', i, 'cam.x', G.RN.cam.x.toFixed(0), d.toFixed(0) + 'ms');
  }
  console.log('frame  панорама       :', fmtS(stats(ts)));
  console.log('terr   stepMs', (globalThis.__stepMs || 0).toFixed(0), 'rasterN', globalThis.__rasterN || 0, 'rasterMs', (globalThis.__rasterMs || 0).toFixed(0), 'drawMs', (globalThis.__drawMs || 0).toFixed(0));
}
// --- 3) тик симуляции ---
{
  const ts = [];
  for (let i = 0; i < 300; i++) { const a = performance.now(); G.tick(S, 0.25); ts.push(performance.now() - a); }
  console.log('tick   x300           :', fmtS(stats(ts)));
}
// --- 4) автосохранение: сборка слепка + JSON ---
{
  const replacer = (k, v) => (k[0] === '_' || k === 'fx') ? undefined : v;
  const t0 = performance.now();
  const rels = []; for (let i = 0; i < S.players.length; i++) for (let j = i + 1; j < S.players.length; j++) rels.push([i, j, S.rel[i][j]]);
  const d = { v: 2, gv: '2.9.1', at: Date.now(), opts: S.opts, seed: S.seed, W: S.W, H: S.H, ter: null, res: null, players: S.players, cities: S.cities, buildings: S.buildings, armies: S.armies, ships: S.ships, msgs: S.msgs, rels, time: S.time, nid: S.nid, plan: S.plan, pending: S.pending || [], folk: S.folk || [], extra: S.extra ? Array.from(S.extra.entries()) : [], sanctions: S.sanctions || [] };
  const build = performance.now() - t0;
  const t1 = performance.now();
  const json = JSON.stringify(d, replacer);
  const ser = performance.now() - t1;
  console.log('save   build', build.toFixed(2) + 'ms', 'stringify', ser.toFixed(2) + 'ms', 'size', (json.length / 1024).toFixed(1) + ' KB', 'armies', S.armies.length, 'folk', (S.folk || []).length);
}
// --- 5) frame при минимальном зуме (обзорная карта) ---
{
  G.RN.cam.z = 0.8; const ts = [];
  for (let i = 0; i < 60; i++) { const a = performance.now(); G.frame(S, 30000 + i * 100); ts.push(performance.now() - a); }
  console.log('frame  z=0.8 обзор    :', fmtS(stats(ts)));
  G.RN.cam.z = 1;
}
// --- 6) оценка количества пешек для цифр ---
let soldiers = 0; for (const a of S.armies) for (const q of a.sq) soldiers += q.s.length;
console.log('солдат в армиях:', soldiers);
process.exit(0);
