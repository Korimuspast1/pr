'use strict';
/* Трейлер 2.7.0 «KINGDOM CRASH» — кадры РЕАЛЬНОГО геймплея движком, 1080x1920, 30 fps, ~30 с.
   Сверху слева ВСЕГДА висит ссылка на ТГ-канал t.me/kingofnof.
   Использование: node tools/promo_trailer.js <каталог-кадров> [startFrame] [endFrame]
   Кадры накладываются оверлеем тут же (без ffmpeg-фильтров). */
const path = require('path');
const fs = require('fs');
const { createCanvas, ImageData, GlobalFonts } = require(process.env.PROMO_CANVAS || '/home/user/pr/.cache/promo-tools/node_modules/@napi-rs/canvas');
try { GlobalFonts.registerFromPath('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 'DejaVuB'); } catch (e) { console.log('font:', e.message); }
try { GlobalFonts.registerFromPath('/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf', 'DejaVuS'); } catch (e) { console.log('font serif:', e.message); }

const OUT = path.resolve(process.argv[2] || '/tmp/trailer');
const F0 = parseInt(process.argv[3] || '0', 10), F1 = parseInt(process.argv[4] || '899', 10);
const W = 1080, H = 1920, FPS = 30, TOTAL = 900;
const TG = 't.me/kingofnof';

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
// трейлер идёт в стабильный «золотой полдень»: время суток не плывёт в ночь
src = src.replace('function todHours(S) { return (8 + (S.time / DAY) * 24) % 24; }', 'function todHours(S) { return 11.8; }');
const names = ['newGame', 'tick', 'RN', 'frame', 'buildTerrain', 'drawTerr', 'centerOn', 'clampCam', 'TS', 'DAY', 'TR', 'SQ', 'BD',
  'mkSquad', 'newArmy', 'doBuild', 'siteOk', 'upgradeCity', 'addCity', 'setRecipe', 'startWar', 'rel', 'makeFolk', 'folkBuild', 'isWater', 'orderMove', 'units',
  'calcRates', 'recomputeTerr', 'todHours', 'RECIPES', 'PERKS', 'drawRoute'];
const body = src + '\n;const __o = {}; ' + names.map(n => `try { __o.${n} = ${n}; } catch (e) { __o.${n} = undefined; }`).join(' ') + ' return __o;';
const G = new Function('document', 'localStorage', 'window', 'performance', 'ImageData', body)(global.document, global.localStorage, global.window, global.performance, ImageData);

let cv = createCanvas(W, H), ctx = cv.getContext('2d');
function freshCanvas() { // один переиспользуемый холст: нативная память не растёт
  ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'source-over';
  if (ctx.setLineDash) ctx.setLineDash([]);
  ctx.clearRect(0, 0, W, H);
  G.RN.cv = cv; G.RN.ctx = ctx; G.RN.w = W; G.RN.h = H; G.RN.dpr = 1;
}
G.RN.cv = cv; G.RN.ctx = ctx; G.RN.w = W; G.RN.h = H; G.RN.dpr = 1; G.RN.showTerr = true;

function buildAt(S, pid, type, x, y) { return G.doBuild(S, pid, type, x, y) === null; }
function ringBuild(S, pid, type, cx, cy, rMin, rMax) {
  for (let r = rMin; r <= rMax; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
    if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
    if (buildAt(S, pid, type, cx + dx, cy + dy)) return { x: cx + dx, y: cy + dy };
  }
  return null;
}
function setStock(S, pid, v) { const p = S.players[pid]; for (const k of ['gold', 'wood', 'food', 'iron']) p.stock[k] = v; }

// ----- оверлей: ссылка на канал слева сверху, всегда на экране -----
function rr(c, x, y, w, h, r) {
  c.beginPath();
  c.moveTo(x + r, y);
  c.arcTo(x + w, y, x + w, y + h, r);
  c.arcTo(x + w, y + h, x, y + h, r);
  c.arcTo(x, y + h, x, y, r);
  c.arcTo(x, y, x + w, y, r);
  c.closePath();
}
function tgBadge(c, x, y, scale, alpha) {
  scale = scale || 1; alpha = alpha == null ? 1 : alpha;
  const w = 300 * scale, h = 56 * scale, px = x, py = y;
  c.save(); c.globalAlpha = alpha;
  // плашка
  c.fillStyle = 'rgba(12,14,20,0.72)'; rr(c, px, py, w, h, 12 * scale); c.fill();
  c.strokeStyle = 'rgba(255,255,255,0.28)'; c.lineWidth = 2 * scale; rr(c, px + 1, py + 1, w - 2, h - 2, 11 * scale); c.stroke();
  // иконка Telegram — круг с бумажным самолётиком
  const cx0 = px + 30 * scale, cy0 = py + h / 2, r0 = 17 * scale;
  c.fillStyle = '#2AABEE'; c.beginPath(); c.arc(cx0, cy0, r0, 0, 6.3); c.fill();
  c.fillStyle = '#fff';
  c.save(); c.translate(cx0, cy0); c.scale(scale * 1.0, scale * 1.0);
  c.beginPath();
  c.moveTo(-8.5, 0.5); c.lineTo(8.5, -7.5); c.lineTo(3.5, 8.5); c.lineTo(-0.5, 2.6); c.closePath(); c.fill();
  c.fillStyle = '#cfeefb';
  c.beginPath(); c.moveTo(-0.5, 2.6); c.lineTo(8.5, -7.5); c.lineTo(-3.2, 4.6); c.closePath(); c.fill();
  c.restore();
  // текст
  c.fillStyle = '#ffffff'; c.font = `bold ${Math.round(21 * scale)}px DejaVuB, sans-serif`;
  c.textBaseline = 'middle'; c.textAlign = 'left';
  c.fillText(TG, px + 58 * scale, py + h / 2 + 1 * scale);
  c.restore();
}
function outroCard(c, p) {
  // p: 0..1 прогресс заставки
  const e = p < 0.2 ? p / 0.2 : 1; // быстрое проявление
  const ease = 1 - Math.pow(1 - Math.min(1, e), 3);
  c.save();
  c.fillStyle = `rgba(6,8,14,${0.78 * ease})`; c.fillRect(0, 0, W, H);
  // золотые плашки под заголовок
  const cx = W / 2, y0 = H * 0.36;
  c.globalAlpha = ease;
  c.fillStyle = '#e8d79a'; c.font = 'bold 96px DejaVuS, serif'; c.textAlign = 'center'; c.textBaseline = 'middle';
  // тень
  c.shadowColor = 'rgba(0,0,0,0.9)'; c.shadowBlur = 24; c.shadowOffsetY = 6;
  c.fillText('KINGDOM', cx, y0);
  c.fillText('CRASH', cx, y0 + 108);
  c.shadowBlur = 0; c.shadowOffsetY = 0;
  c.fillStyle = '#f4e9c8'; c.font = 'bold 30px DejaVuB, sans-serif';
  c.fillText('С Р Е Д Н Е В Е К О В А Я   С Т Р А Т Е Г И Я', cx, y0 + 200);
  // разделитель
  c.strokeStyle = '#c9a84c'; c.lineWidth = 4; c.beginPath(); c.moveTo(cx - 200, y0 + 248); c.lineTo(cx + 200, y0 + 248); c.stroke();
  c.fillStyle = '#ffffff'; c.font = 'bold 34px DejaVuB, sans-serif';
  c.fillText('Строй. Воюй. Захватывай.', cx, y0 + 320);
  // большой бейдж канала
  tgBadge(c, cx - 150, y0 + 380, 1.35, ease);
  c.globalAlpha = ease;
  c.fillStyle = '#9fb4c8'; c.font = '26px DejaVuB, sans-serif';
  c.fillText('Android  ·  бесплатно', cx, y0 + 520);
  c.restore();
}
function fade(c, a) { if (a > 0) { c.save(); c.fillStyle = `rgba(0,0,0,${a})`; c.fillRect(0, 0, W, H); c.restore(); } }

// ----- мир -----
function setupWorld() {
  const S = G.newGame({ type: 'continent', size: 'l', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 20261115, free: 1, events: 1 });
  for (let i = 0; i < S.players.length; i++) setStock(S, i, 99999);
  const cap = S.cities.find(c => c.owner === 0);
  try { G.upgradeCity(S, cap); G.upgradeCity(S, cap); } catch (e) { }
  for (let i = -4; i <= 4; i++) { buildAt(S, 0, 'wall', cap.x + i, cap.y - 4); if (i % 4 === 0) ringBuild(S, 0, 'tower', cap.x + i, cap.y - 5, 0, 1); }
  buildAt(S, 0, 'gate', cap.x, cap.y - 4) || ringBuild(S, 0, 'gate', cap.x, cap.y - 4, 0, 2);
  for (const t of ['forge', 'sawmill', 'market', 'barracks', 'tavern', 'hospital', 'house', 'house', 'house', 'farm', 'mill', 'stable', 'workshop']) ringBuild(S, 0, t, cap.x, cap.y, 2, 6);
  for (let i = 0; i < 26; i++) S.folk.push(G.makeFolk(S, cap));
  for (let i = 0; i < 400; i++) G.tick(S, 0.25);
  // точка битвы
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
  // марш: свежая армия пойдёт колонной к вражеской границе
  const march = G.newArmy(S, 0, cap.x + 1, cap.y + 1, [G.mkSquad('spear'), G.mkSquad('spear'), G.mkSquad('cav')]);
  G.orderMove(S, march, cap.x - 6, cap.y + 6);
  return { S, cap, bx, by, march };
}

const world = setupWorld();
const { S, cap } = world;
S.RN_seedNote = 0;
S.time = 18; // ~11:36 по игровым часам: весь трейлер идёт при дневном свете
console.log('столица:', cap.x, cap.y, 'tod:', G.todHours(S).toFixed(2), 'армий:', S.armies.length, 'строений:', S.buildings.length);
G.buildTerrain(S);

let T_REF = 100000;
const phase = (i) => { // 0..TOTAL-1 → номер фазы и локальный прогресс
  if (i < 195) return [0, i / 194];          // 0.0–6.5s: от столицы — отъезд камеры
  if (i < 360) return [1, (i - 195) / 164];  // 6.5–12s: панорама + марш колонны
  if (i < 660) return [2, (i - 360) / 299];  // 12–22s: битва у стен
  if (i < 780) return [3, (i - 660) / 119];  // 22–26s: общий план, отъезд
  return [4, (i - 780) / 119];               // 26–30s: заставка
};

fs.mkdirSync(OUT, { recursive: true });
for (let i = F0; i <= F1; i++) {
  const [ph, p] = phase(i);
  // мир тикает медленно всю игру (бой идёт в реальном времени)
  const steps = ph === 2 ? 3 : 3;
  for (let k = 0; k < steps; k++) G.tick(S, 0.05);
  T_REF += 1000 / FPS;
  freshCanvas();
  const cam = G.RN.cam;
  if (ph === 0) { // отъезд от столицы: близко → широко
    const e = 1 - Math.pow(1 - p, 2.2);
    cam.z = 3.15 - 1.0 * e;
    G.centerOn(cap.x + 1 - 2.2 * e, cap.y + 2.2 - 2.0 * e);
  } else if (ph === 1) { // панорама к месту битвы + марш
    const e = p, s = e * e * (3 - 2 * e); // smoothstep
    cam.z = 2.1 + 1.45 * s;
    G.centerOn(cap.x - 1 - 4.6 * s, cap.y + 0.5 - 0.1 * s);
  } else if (ph === 2) { // бой: лёгкий дрейф + дрожь в моменты ударов
    const e = p, s = e * e * (3 - 2 * e);
    cam.z = 3.55 + 0.65 * s + Math.sin(i * 0.35) * 0.02;
    G.centerOn(cap.x - 5.6 + 1.3 * s, cap.y + 0.4 - 1.1 * s);
  } else if (ph === 3) { // общий план: отъезд вверх
    const e = p, s = 1 - Math.pow(1 - e, 2);
    cam.z = 3.5 - 1.15 * s;
    G.centerOn(cap.x - 4.3 + 2.6 * s, cap.y - 0.7 + 1.6 * s);
  } else { // заставка: медленный дрейф над городом
    const e = p, s = e * e * (3 - 2 * e);
    cam.z = 2.35 - 0.7 * s;
    G.centerOn(cap.x - 1.7 + 3.1 * s, cap.y + 0.9 - 1.8 * s);
  }
  G.frame(S, T_REF);
  // заставка: затемнение + текст
  if (ph === 4) outroCard(ctx, p);
  // въезд из черноты в первые 15 кадров
  fade(ctx, i < 15 ? 1 - i / 15 : 0);
  // мягкий выход в чёрное на последних 8 кадрах до заставки? нет — заставка сама тёмная
  // водяной знак-ссылка на канал — ВСЕГДА слева сверху
  tgBadge(ctx, 26, 34, 1.0, 0.96);
  fs.writeFileSync(path.join(OUT, String(i).padStart(4, '0') + '.jpg'), cv.toBuffer('image/jpeg', 90));
  if (i % 30 === 0) { const m = process.memoryUsage(); console.log('кадр', i, '/', TOTAL - 1, 'фаза', ph, 'tod', G.todHours(S).toFixed(2), 'армии в бою:', S.armies.filter(a => a.fight).length, 'rssMB', (m.rss / 1048576) | 0, 'heapMB', (m.heapUsed / 1048576) | 0, 'extMB', (m.external / 1048576) | 0); }
}
console.log('готово: кадры', F0, '–', F1, 'в', OUT);
