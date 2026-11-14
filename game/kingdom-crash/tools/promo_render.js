'use strict';
/* Рендер промо-кадров НАСТОЯЩИМ движком игры (без браузера).
   Использование: node tools/promo_render.js <каталог-куда> [A|B|C|all]
   Кадры 1080x1920 (вертикальные, для TikTok) — из тех же спрайтов и логики, что в игре. */
const path = require('path');
const fs = require('fs');
const { createCanvas, ImageData } = require(process.env.PROMO_CANVAS || '/home/user/pr/.cache/promo-tools/node_modules/@napi-rs/canvas');

// --- минимальное окружение для игрового кода ---
const OUT = path.resolve(process.argv[2] || '/tmp/promo');
const SCENE = (process.argv[3] || 'all').toUpperCase();
const W = 1080, H = 1920, FPS = 30;
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

// --- грузим движок ---
const DIR = path.resolve(__dirname, '../www/js');
const files = ['data.js', 'map.js', 'game.js', 'ai.js', 'terrain.js', 'render.js'];
let src = '';
for (const f of files) src += '\n;/* ' + f + ' */\n' + fs.readFileSync(path.join(DIR, f), 'utf8').replace(/^'use strict';/, '');
// для промо весь туман войны снимаем: все войска и эффекты видны
src = src.replace('function canSee(S, o) { return o.owner === 0 || allied(S, 0, o.owner) || visibleAt(S, o.x, o.y); }', 'function canSee() { return true; }');
src = src.replace('function visibleAt(', 'function _visibleAt(') + '\nfunction visibleAt() { return true; }\n';
const names = ['newGame', 'tick', 'RN', 'frame', 'buildTerrain', 'drawTerr', 'centerOn', 'clampCam', 'TS', 'DAY', 'TR', 'SQ', 'BD',
  'mkSquad', 'newArmy', 'doBuild', 'siteOk', 'upgradeCity', 'addCity', 'setRecipe', 'startWar', 'rel', 'makeFolk', 'folkBuild', 'isWater', 'orderMove', 'units',
  'calcRates', 'recomputeTerr', 'todHours', 'RECIPES', 'PERKS'];
const body = src + '\n;const __o = {}; ' + names.map(n => `try { __o.${n} = ${n}; } catch (e) { __o.${n} = undefined; }`).join(' ') + ' return __o;';
const G = new Function('document', 'localStorage', 'window', 'performance', 'ImageData', body)(global.document, global.localStorage, global.window, global.performance, ImageData);

let cv = createCanvas(W, H), ctx = cv.getContext('2d');
function freshCanvas() { cv = createCanvas(W, H); ctx = cv.getContext('2d'); G.RN.cv = cv; G.RN.ctx = ctx; G.RN.w = W; G.RN.h = H; G.RN.dpr = 1; }
G.RN.cv = cv; G.RN.ctx = ctx; G.RN.w = W; G.RN.h = H; G.RN.dpr = 1; G.RN.showTerr = true;

// --- вспомогательное ---
function buildAt(S, pid, type, x, y) { return G.doBuild(S, pid, type, x, y) === null; }
function ringBuild(S, pid, type, cx, cy, rMin, rMax) {
  for (let r = rMin; r <= rMax; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
    if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
    if (buildAt(S, pid, type, cx + dx, cy + dy)) return { x: cx + dx, y: cy + dy };
  }
  return null;
}
function setStock(S, pid, v) { const p = S.players[pid]; for (const k of ['gold', 'wood', 'food', 'iron']) p.stock[k] = v; }

function setupWorld() {
  const S = G.newGame({ type: 'continent', size: 'm', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 20261114, free: 1, events: 1 });
  for (let i = 0; i < S.players.length; i++) setStock(S, i, 99999);
  const cap = S.cities.find(c => c.owner === 0);
  // столица побольше
  try { G.upgradeCity(S, cap); G.upgradeCity(S, cap); } catch (e) { }
  // стены и башни к северу от столицы
  for (let i = -4; i <= 4; i++) { buildAt(S, 0, 'wall', cap.x + i, cap.y - 4); if (i % 4 === 0) ringBuild(S, 0, 'tower', cap.x + i, cap.y - 5, 0, 1); }
  buildAt(S, 0, 'gate', cap.x, cap.y - 4) || ringBuild(S, 0, 'gate', cap.x, cap.y - 4, 0, 2);
  // хозяйство вокруг
  for (const t of ['forge', 'sawmill', 'market', 'barracks', 'tavern', 'hospital', 'house', 'house', 'house', 'farm', 'mill', 'stable', 'workshop']) ringBuild(S, 0, t, cap.x, cap.y, 2, 6);
  // жители сами строят дома
  for (let i = 0; i < 26; i++) S.folk.push(G.makeFolk(S, cap));
  for (let i = 0; i < 400; i++) G.tick(S, 0.25);
  // точка боя: суша юго-западнее города (все армии в пределах 2.4 клетки — бой начнётся сам)
  let bx = cap.x - 5, by = cap.y + 2;
  outer: for (let r = 2; r <= 9; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
    const x = cap.x + dx, y = cap.y + dy, i = y * S.W + x;
    if (!G.isWater(S.ter[i]) && !G.isWater(S.ter[i + 1]) && !G.isWater(S.ter[i + S.W]) && !G.isWater(S.ter[i + S.W + 1])) { bx = x; by = y; break outer; }
  }
  const A1 = G.newArmy(S, 0, bx, by, [G.mkSquad('heavy'), G.mkSquad('heavy'), G.mkSquad('archer')]);
  const A2 = G.newArmy(S, 0, bx + 2, by + 1, [G.mkSquad('light'), G.mkSquad('archer'), G.mkSquad('spear')]);
  const E1 = G.newArmy(S, 1, bx, by - 2, [G.mkSquad('heavy'), G.mkSquad('light'), G.mkSquad('archer')]);
  const E2 = G.newArmy(S, 1, bx + 2, by - 2, [G.mkSquad('cav'), G.mkSquad('spear')]);
  G.startWar(S, 0, 1);
  return { S, cap, bx, by, A1, A2, E1, E2 };
}

function renderFrames(dir, n, step, camFn) {
  fs.mkdirSync(dir, { recursive: true });
  for (let i = 0; i < n; i++) {
    camFn(i / Math.max(1, n - 1), i);
    G.frame(S_REF, T_REF += step);
    fs.writeFileSync(path.join(dir, String(i).padStart(4, '0') + '.png'), cv.toBuffer('image/png'));
    if (i % 10 === 0) console.log('  кадр', i, '/', n);
  }
}

let S_REF = null, T_REF = 0;

function run() {
  const { S, cap, bx, by } = setupWorld();
  S_REF = S; T_REF = 100000;
  // полдень (TOD яркий)
  let best = null;
  for (let t = 0; t < 8; t += 0.25) { const h = G.todHours({ time: t * G.DAY }); if (h >= 10 && h <= 14) { best = t * G.DAY; break; } }
  if (best != null) S.time = best;
  console.log('столица:', cap.x, cap.y, 'tod:', G.todHours(S), 'армий:', S.armies.length, 'строений:', S.buildings.length);
  G.buildTerrain(S);
  S.dirtyT = true;

  const cam = G.RN.cam;
  const shot = (cx, cy, z) => { cam.z = z; G.centerOn(cx, cy); };

  if (SCENE === 'A' || SCENE === 'ALL') {
    console.log('сцена A: королевство');
    const dir = path.join(OUT, 'sceneA');
    fs.mkdirSync(dir, { recursive: true });
    const n = 78;
    for (let i = 0; i < n; i++) {
      const p = i / (n - 1);
      freshCanvas();
      shot(cap.x + 1 - 2.5 * p, cap.y + 2.5 - 2 * p, 2.3 + 0.8 * p);
      for (let k = 0; k < 3; k++) G.tick(S, 0.05); // DAY=120: не убегать в ночь
      T_REF += 1000 / FPS;
      G.frame(S, T_REF);
      fs.writeFileSync(path.join(dir, String(i).padStart(4, '0') + '.png'), cv.toBuffer('image/png'));
    }
    console.log('  готово', n);
  }

  if (SCENE === 'B' || SCENE === 'ALL') {
    console.log('сцена B: бой у стен');
    // бой: тикаем, пока армии дерутся
    const dir = path.join(OUT, 'sceneB');
    fs.mkdirSync(dir, { recursive: true });
    const n = 96;
    for (let i = 0; i < n; i++) {
      const p = i / (n - 1);
      freshCanvas();
      shot(cap.x - 1 - 0.8 * p, cap.y - 3.2 - 0.6 * p, 2.9 + 0.7 * p);
      for (let k = 0; k < 3; k++) G.tick(S, 0.05); // DAY=120: не убегать в ночь
      T_REF += 1000 / FPS;
      G.frame(S, T_REF);
      fs.writeFileSync(path.join(dir, String(i).padStart(4, '0') + '.png'), cv.toBuffer('image/png'));
      if (i % 24 === 0) console.log('  кадр', i, 'tod', G.todHours(S).toFixed(2), 'time', S.time.toFixed(0), 'бой:', S.armies.map(a => a.owner + ':' + (a.fight ? 'F' : '-')).join(' '));
    }
    console.log('  готово', n);
  }

  if (SCENE === 'C' || SCENE === 'ALL') {
    console.log('сцена C: земли');
    const dir = path.join(OUT, 'sceneC');
    fs.mkdirSync(dir, { recursive: true });
    const n = 66;
    for (let i = 0; i < n; i++) {
      const p = i / (n - 1);
      freshCanvas();
      shot(cap.x - 7 - 22 * p, cap.y + 4 + 12 * p, 1.9 - 0.65 * p);
      for (let k = 0; k < 3; k++) G.tick(S, 0.05); // DAY=120: не убегать в ночь
      T_REF += 1000 / FPS;
      G.frame(S, T_REF);
      fs.writeFileSync(path.join(dir, String(i).padStart(4, '0') + '.png'), cv.toBuffer('image/png'));
    }
    console.log('  готово', n);
  }
  console.log('все кадры в', OUT);
}
run();
