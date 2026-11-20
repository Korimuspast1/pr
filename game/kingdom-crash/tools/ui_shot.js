'use strict';
// Скриншот игрового кадра 2.5.0: сезоны, погода, караваны, праздник, жители.
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
const names = ['newGame', 'tick', 'RN', 'frame', 'buildTerrain', 'drawTerr', 'centerOn', 'clampCam', 'TS', 'DAY', 'TR', 'SQ', 'BD',
  'mkSquad', 'newArmy', 'doBuild', 'siteOk', 'upgradeCity', 'addCity', 'setRecipe', 'startWar', 'rel', 'makeFolk', 'folkBuild', 'isWater', 'orderMove', 'units',
  'calcRates', 'recomputeTerr', 'todHours', 'RECIPES', 'PERKS', 'cmdLvl', 'tickCars', 'tickFest', 'TERR'];
const body = src + '\n;const __o = {}; ' + names.map(n => `try { __o.${n} = ${n}; } catch (e) { __o.${n} = undefined; }`).join(' ') + ' return __o;';
const G = new Function('document', 'localStorage', 'window', 'performance', 'ImageData', body)(global.document, global.localStorage, global.window, global.performance, ImageData);

let cv = createCanvas(W, H), ctx = cv.getContext('2d');
function freshCanvas() { cv = createCanvas(W, H); ctx = cv.getContext('2d'); G.RN.cv = cv; G.RN.ctx = ctx; G.RN.w = W; G.RN.h = H; G.RN.dpr = 1; }
G.RN.cv = cv; G.RN.ctx = ctx; G.RN.w = W; G.RN.h = H; G.RN.dpr = 1; G.RN.showTerr = true;

function buildAt(S, pid, type, x, y) { return G.doBuild(S, pid, type, x, y) === null; }
function setStock(S, pid, v) { const p = S.players[pid]; for (const k of ['gold', 'wood', 'food', 'iron']) p.stock[k] = v; }

const OUT = path.resolve(process.argv[2] || '/tmp/ui_shots');
fs.mkdirSync(OUT, { recursive: true });
const S = G.newGame({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 4242, free: 1, events: 1 });
for (let i = 0; i < S.players.length; i++) setStock(S, i, 99999);
const cap = S.cities.find(c => c.owner === 0);
const spots = [[3, 0], [4, 1], [3, 3], [0, 4], [-2, 3], [-3, 0], [-2, -2], [1, -3], [4, -2]];
const types = ['farm', 'sawmill', 'mill', 'market', 'barracks', 'tower', 'house', 'house', 'forge'];
for (let i = 0; i < spots.length; i++) { const d = spots[i]; let okb = false; for (let r = 0; r < 6 && !okb; r++) for (let dy = -r; dy <= r && !okb; dy++) for (let dx = -r; dx <= r && !okb; dx++) okb = buildAt(S, 0, types[i], cap.x + 2 + d[0] + dx, cap.y + 2 + d[1] + dy); }
G.newArmy(S, 0, cap.x + 5, cap.y + 5, [G.mkSquad('cmd'), G.mkSquad('heavy'), G.mkSquad('archer')]);
G.newArmy(S, 1, cap.x + 12, cap.y + 4, [G.mkSquad('light'), G.mkSquad('cav')]);
G.rel(S, 0, 1).st = 'war';
S.fest = { city: cap.id, x: cap.x + 2, y: cap.y + 2.2, until: S.time + 500 };
for (let i = 0; i < 24; i++) { try { G.makeFolk(S); } catch (e) { } }
for (let t = 0; t < 60; t++) G.tick(S, 0.25);
G.buildTerrain(S);
if (G.TERR && G.TERR.drain) G.TERR.drain(); // достроить все участки до единого снимка
let T_REF = 100;
S.cars = [{ id: 990, owner: 0, x: cap.x - 3.5, y: cap.y + 1.5, toX: cap.x + 10, toY: cap.y + 8, face: 1, gold: 60 }];
function shot(name, seasonIdx, cx, cy) {
  if (cx != null) G.centerOn(cx, cy);
  S.time = G.DAY * 10 * seasonIdx + 7.48; // +7.48: синус погоды на пике (снег/дождь гарантированы)
  freshCanvas();
  G.RN.sel = null; G.RN.place = null;
  G.frame(S, T_REF += 100);
  fs.writeFileSync(path.join(OUT, name + '.png'), cv.toBuffer('image/png'));
  console.log('shot', name);
}
G.RN.cam.z = 8;
shot('winter_snow_cart', 3.0, cap.x - 3.5, cap.y + 1.5); // зима, время такт синуса погоды → снег
shot('fest_close_day', 1.05, cap.x + 2, cap.y + 2);
shot('spring_rain', 0.02, cap.x + 2, cap.y + 2); // весна, дождь
