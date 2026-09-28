import * as THREE from './three.module.min.js';

// ============================================================
//  Генерация всех текстур игры (процедурные, бесшовные)
// ============================================================

const rnd = (a, b) => a + Math.random() * (b - a);

function makeCanvas(w, h) {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  return c;
}

function tex(c, { srgb = true, aniso = 8 } = {}) {
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.colorSpace = srgb ? THREE.SRGBColorSpace : THREE.NoColorSpace;
  t.anisotropy = aniso;
  return t;
}

// мелкий шум точками
function noisePass(ctx, w, h, { count = 4000, size = 2, alpha = 0.05, light = 0.5 } = {}) {
  for (let i = 0; i < count; i++) {
    const l = Math.random() < light;
    ctx.fillStyle = l ? `rgba(255,255,255,${alpha})` : `rgba(0,0,0,${alpha})`;
    const s = rnd(size * 0.5, size * 1.8);
    ctx.fillRect(Math.random() * w, Math.random() * h, s, s);
  }
}

// детерминированный хэш (для повторяющихся объектов вдоль дороги)
export function hash(n) {
  const s = Math.sin(n * 127.1 + 13.7) * 43758.5453;
  return s - Math.floor(s);
}

// ---------- ДОРОГА: 2048 x 768 px == 44.4 м (по X) x 15 м (по Z) ----------
// мир: асфальт от x=-23.7 до x=+20.7; наши 5 полос 0..18.75
function roadCanvas() {
  const W = 2048, H = 768;
  const PX = W / 44.4;                       // пикселей на метр
  const u = x => (x + 22.2) * PX;            // мировой X -> пиксель
  const c = makeCanvas(W, H), ctx = c.getContext('2d');

  // базовый асфальт
  ctx.fillStyle = '#34363b';
  ctx.fillRect(0, 0, W, H);

  // крупные пятна / заплатки
  for (let i = 0; i < 34; i++) {
    ctx.fillStyle = Math.random() < 0.5
      ? `rgba(22,23,26,${rnd(0.05, 0.13)})`
      : `rgba(66,69,76,${rnd(0.05, 0.12)})`;
    ctx.fillRect(Math.random() * W, Math.random() * H, rnd(30, 240), rnd(18, 150));
  }
  noisePass(ctx, W, H, { count: 10000, size: 3, alpha: 0.06 });

  // продольные битумные швы (ремонт трещин)
  for (let i = 0; i < 4; i++) {
    ctx.strokeStyle = `rgba(14,14,16,${rnd(0.4, 0.6)})`;
    ctx.lineWidth = rnd(2, 5);
    let x = Math.random() * W;
    ctx.beginPath(); ctx.moveTo(x, -10);
    for (let y = 0; y <= H + 10; y += 42) { x += rnd(-12, 12); ctx.lineTo(x, y); }
    ctx.stroke();
  }

  // колеи (полирoванные тёмные следы в каждой полосе)
  const lanes = [1.875, 5.625, 9.375, 13.125, 16.875, -5.075, -8.825, -12.575, -16.325, -20.075];
  for (const lx of lanes) for (const o of [-0.78, 0.78]) {
    const g = ctx.createLinearGradient(u(lx + o) - 20, 0, u(lx + o) + 20, 0);
    g.addColorStop(0, 'rgba(18,19,22,0)');
    g.addColorStop(0.5, 'rgba(18,19,22,0.35)');
    g.addColorStop(1, 'rgba(18,19,22,0)');
    ctx.fillStyle = g;
    ctx.fillRect(u(lx + o) - 22, 0, 44, H);
  }

  // мелкие трещины
  for (let i = 0; i < 12; i++) {
    ctx.strokeStyle = `rgba(10,10,12,${rnd(0.25, 0.5)})`;
    ctx.lineWidth = rnd(1, 2.2);
    let x = Math.random() * W, y = Math.random() * H;
    ctx.beginPath(); ctx.moveTo(x, y);
    for (let k = 0; k < 6; k++) { x += rnd(-46, 46); y += rnd(-46, 46); ctx.lineTo(x, y); }
    ctx.stroke();
  }

  // ---- разметка ----
  const line = (x, wM, dashed) => {
    const px = u(x), pw = Math.max(wM * PX, 2);
    ctx.fillStyle = '#d8dce0';
    if (dashed) ctx.fillRect(px - pw / 2, 0, pw, H * (6 / 15));   // штрих 6 м из 15
    else ctx.fillRect(px - pw / 2, 0, pw, H);
  };
  line(0.5, 0.18);  line(18.3, 0.18);                    // наши края
  for (const x of [3.75, 7.5, 11.25, 15.0]) line(x, 0.15, true);
  line(-3.6, 0.18); line(-21.6, 0.18);                   // встречка
  for (const x of [-6.95, -10.7, -14.45, -18.2]) line(x, 0.15, true);

  // "состарить" разметку
  noisePass(ctx, W, H, { count: 3200, size: 3, alpha: 0.10 });

  return c;
}

// карта шероховатости дороги (разметка более гладкая/глянцевая)
function roadRoughCanvas() {
  const W = 2048, H = 768;
  const PX = W / 44.4;
  const u = x => (x + 22.2) * PX;
  const c = makeCanvas(W, H), ctx = c.getContext('2d');
  ctx.fillStyle = '#d2d2d2'; ctx.fillRect(0, 0, W, H);   // асфальт — шершавый
  ctx.fillStyle = '#8f8f8f';                             // краска — глаже
  const line = (x, wM, dashed) => {
    const px = u(x), pw = Math.max(wM * PX, 2);
    if (dashed) ctx.fillRect(px - pw / 2, 0, pw, H * 6 / 15);
    else ctx.fillRect(px - pw / 2, 0, pw, H);
  };
  line(0.5, 0.18); line(18.3, 0.18);
  for (const x of [3.75, 7.5, 11.25, 15.0]) line(x, 0.15, true);
  line(-3.6, 0.18); line(-21.6, 0.18);
  for (const x of [-6.95, -10.7, -14.45, -18.2]) line(x, 0.15, true);
  noisePass(ctx, W, H, { count: 2200, size: 4, alpha: 0.07 });
  return c;
}

// карта нормалей из карты высот (Собель)
function heightToNormal(src, strength = 1.6) {
  const w = src.width, h = src.height;
  const sctx = src.getContext('2d');
  const img = sctx.getImageData(0, 0, w, h);
  const d = img.data;
  const c = makeCanvas(w, h), ctx = c.getContext('2d');
  const out = ctx.createImageData(w, h);
  const o = out.data;
  const Hh = (x, y) => d[(((y + h) % h) * w + ((x + w) % w)) * 4] / 255;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const dx = (Hh(x + 1, y) - Hh(x - 1, y)) * strength;
    const dy = (Hh(x, y + 1) - Hh(x, y - 1)) * strength;
    const inv = 1 / Math.sqrt(dx * dx + dy * dy + 1);
    const i = (y * w + x) * 4;
    o[i] = (-dx * inv * 0.5 + 0.5) * 255;
    o[i + 1] = (-dy * inv * 0.5 + 0.5) * 255;
    o[i + 2] = (inv * 0.5 + 0.5) * 255;
    o[i + 3] = 255;
  }
  ctx.putImageData(out, 0, 0);
  return c;
}

// шумовая карта высот для нормалей асфальта
function noiseHeightCanvas(size = 256, blobs = 260) {
  const c = makeCanvas(size, size), ctx = c.getContext('2d');
  ctx.fillStyle = '#808080'; ctx.fillRect(0, 0, size, size);
  for (let i = 0; i < blobs; i++) {
    const r = rnd(2, 9);
    const x = Math.random() * size, y = Math.random() * size;
    const g2 = ctx.createRadialGradient(x, y, 0, x, y, r);
    const light = Math.random() < 0.5;
    g2.addColorStop(0, light ? 'rgba(255,255,255,0.20)' : 'rgba(0,0,0,0.20)');
    g2.addColorStop(1, 'rgba(128,128,128,0)');
    ctx.fillStyle = g2;
    ctx.beginPath(); ctx.arc(x, y, r, 0, 7); ctx.fill();
  }
  // мелкозернистый шум сверху
  noisePass(ctx, size, size, { count: 2500, size: 2, alpha: 0.10 });
  return c;
}

// ---------- ТРАВА ----------
function grassCanvas() {
  const S = 512;
  const c = makeCanvas(S, S), ctx = c.getContext('2d');
  ctx.fillStyle = '#4d6a38';
  ctx.fillRect(0, 0, S, S);
  for (let i = 0; i < 46; i++) {
    ctx.fillStyle = ['#547440', '#456231', '#5b7c45', '#516e3b', '#67804a'][i % 5];
    ctx.globalAlpha = rnd(0.10, 0.25);
    const x = Math.random() * S, y = Math.random() * S, r = rnd(18, 90);
    ctx.beginPath(); ctx.arc(x, y, r, 0, 7); ctx.fill();
  }
  ctx.globalAlpha = 1;
  // сухие проплешины
  for (let i = 0; i < 10; i++) {
    ctx.fillStyle = `rgba(122,124,74,${rnd(0.08, 0.2)})`;
    ctx.beginPath(); ctx.arc(Math.random() * S, Math.random() * S, rnd(10, 40), 0, 7); ctx.fill();
  }
  // травинки
  for (let i = 0; i < 3600; i++) {
    const x = Math.random() * S, y = Math.random() * S;
    ctx.strokeStyle = `rgba(${Math.random() < 0.5 ? '30,52,22' : '110,140,70'},${rnd(0.12, 0.3)})`;
    ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x + rnd(-2, 2), y - rnd(2, 5)); ctx.stroke();
  }
  return c;
}

// ---------- БЕТОН (ограждения, путепроводы) ----------
function concreteCanvas() {
  const S = 512;
  const c = makeCanvas(S, S), ctx = c.getContext('2d');
  ctx.fillStyle = '#b3b1ab';
  ctx.fillRect(0, 0, S, S);
  noisePass(ctx, S, S, { count: 5000, size: 3, alpha: 0.05 });
  // разводы/грязь
  for (let i = 0; i < 14; i++) {
    const x = Math.random() * S;
    const g = ctx.createLinearGradient(x, 0, x, S);
    g.addColorStop(0, `rgba(70,68,62,${rnd(0.06, 0.16)})`);
    g.addColorStop(1, 'rgba(70,68,62,0)');
    ctx.fillStyle = g;
    ctx.fillRect(x - rnd(6, 20), 0, rnd(12, 40), S);
  }
  // швы блоков
  ctx.fillStyle = 'rgba(40,40,40,0.55)';
  ctx.fillRect(0, 0, S, 5); ctx.fillRect(0, S / 2, S, 4);
  ctx.fillStyle = 'rgba(255,255,255,0.18)';
  ctx.fillRect(0, 6, S, 3); ctx.fillRect(0, S / 2 + 5, S, 3);
  // сколы
  for (let i = 0; i < 26; i++) {
    ctx.fillStyle = `rgba(60,58,54,${rnd(0.2, 0.5)})`;
    ctx.beginPath(); ctx.arc(Math.random() * S, Math.random() * S, rnd(1, 4), 0, 7); ctx.fill();
  }
  return c;
}

// ---------- сетка забора (альфа) ----------
function gridCanvas() {
  const S = 128;
  const c = makeCanvas(S, S), ctx = c.getContext('2d');
  ctx.clearRect(0, 0, S, S);
  ctx.strokeStyle = 'rgba(58,66,58,0.95)';
  ctx.lineWidth = 3;
  const step = 32;
  for (let i = 0; i <= S; i += step) {
    ctx.beginPath(); ctx.moveTo(i, 0); ctx.lineTo(i, S); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, i); ctx.lineTo(S, i); ctx.stroke();
  }
  return c;
}

// ---------- облака ----------
function cloudCanvas() {
  const S = 256;
  const c = makeCanvas(S, S), ctx = c.getContext('2d');
  ctx.clearRect(0, 0, S, S);
  for (let i = 0; i < 16; i++) {
    const x = rnd(40, S - 40), y = rnd(70, S - 60), r = rnd(18, 46);
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, 'rgba(255,255,255,0.75)');
    g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(x, y, r, 0, 7); ctx.fill();
  }
  return c;
}

// ---------- искра ----------
function sparkCanvas() {
  const S = 64;
  const c = makeCanvas(S, S), ctx = c.getContext('2d');
  const g = ctx.createRadialGradient(S / 2, S / 2, 0, S / 2, S / 2, S / 2);
  g.addColorStop(0, 'rgba(255,255,240,1)');
  g.addColorStop(0.3, 'rgba(255,210,120,0.9)');
  g.addColorStop(0.6, 'rgba(255,140,40,0.45)');
  g.addColorStop(1, 'rgba(255,120,20,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, S, S);
  return c;
}

// ---------- зелёные вывески МКАДа ----------
const GREEN = '#0d7a43';
function gantryCanvas(title, rows) {
  const W = 1024, H = 288;
  const c = makeCanvas(W, H), ctx = c.getContext('2d');
  ctx.fillStyle = GREEN; ctx.fillRect(0, 0, W, H);
  // легкая текстура
  noisePass(ctx, W, H, { count: 900, size: 2, alpha: 0.03 });
  // рамка
  ctx.strokeStyle = '#f2f5f2'; ctx.lineWidth = 6;
  ctx.strokeRect(10, 10, W - 20, H - 20);
  ctx.fillStyle = '#f2f5f2';
  ctx.textBaseline = 'middle';
  // заголовок
  ctx.font = 'bold 52px "Arial", sans-serif';
  ctx.textAlign = 'left';
  ctx.fillText(title, 46, 62);
  ctx.fillRect(40, 100, W - 80, 3);
  // строки
  ctx.font = 'bold 40px "Arial", sans-serif';
  rows.forEach((r, i) => {
    const y = 142 + i * 58;
    ctx.fillText(r.text, 66, y);
    if (r.dist) {
      ctx.font = 'bold 34px "Arial", sans-serif';
      ctx.textAlign = 'right';
      ctx.fillText(r.dist, W - 190, y);
      ctx.textAlign = 'left';
      ctx.font = 'bold 40px "Arial", sans-serif';
    }
  });
  // стрелка
  ctx.fillStyle = '#f2f5f2';
  ctx.beginPath();
  ctx.moveTo(W - 150, H / 2 - 34);
  ctx.lineTo(W - 60, H / 2);
  ctx.lineTo(W - 150, H / 2 + 34);
  ctx.closePath();
  ctx.fill();
  return c;
}

const GANTRY_SETS = [
  ['МКАД · ЮГ', [{ text: 'Выезд 47 · Пятницкое шоссе', dist: '2 км' }, { text: 'Выезд 46 · ул. Свободы' }]],
  ['МКАД · ЮГ', [{ text: 'Выезд 45 · Волоколамское шоссе', dist: '1 км' }]],
  ['МКАД · ЗАПАД', [{ text: 'Выезд 59 · Можайское шоссе', dist: '3 км' }, { text: 'Выезд 58 · Кутузовский проспект' }]],
  ['МКАД · СЕВЕР', [{ text: 'Выезд 68 · Ленинградское шоссе' }, { text: 'Химки · Шереметьево', dist: '4 км' }]],
  ['МКАД · СЕВЕР', [{ text: 'Выезд 76 · Дмитровское шоссе', dist: '2 км' }]],
  ['МКАД · ВОСТОК', [{ text: 'Выезд 85 · Ярославское шоссе' }, { text: 'Мытищи · Королёв', dist: '1 км' }]],
  ['МКАД · ВОСТОК', [{ text: 'Выезд 92 · Щёлковское шоссе', dist: '2 км' }]],
  ['МКАД · ЮГ', [{ text: 'Выезд 13 · Каширское шоссе' }, { text: 'Домодедово', dist: '5 км' }]],
];

// ---------- километровый столбик МКАД ----------
function kmCanvas(n) {
  const W = 256, H = 340;
  const c = makeCanvas(W, H), ctx = c.getContext('2d');
  ctx.fillStyle = '#eef0ee'; ctx.fillRect(0, 0, W, H);
  noisePass(ctx, W, H, { count: 350, size: 2, alpha: 0.04 });
  ctx.strokeStyle = '#1a1a1a'; ctx.lineWidth = 10;
  ctx.strokeRect(8, 8, W - 16, H - 16);
  ctx.fillStyle = '#111';
  ctx.textAlign = 'center';
  ctx.font = 'bold 44px "Arial", sans-serif';
  ctx.fillText('МКАД', W / 2, 66);
  ctx.font = 'bold 150px "Arial", sans-serif';
  ctx.fillText(String(n), W / 2, 205);
  ctx.font = 'bold 34px "Arial", sans-serif';
  ctx.fillText('МОСКВА', W / 2, 296);
  return c;
}

// ---------- знак 110 ----------
function sign110Canvas() {
  const S = 256;
  const c = makeCanvas(S, S), ctx = c.getContext('2d');
  ctx.clearRect(0, 0, S, S);
  ctx.beginPath(); ctx.arc(S / 2, S / 2, S / 2 - 4, 0, 7);
  ctx.fillStyle = '#f4f4f4'; ctx.fill();
  ctx.lineWidth = 26; ctx.strokeStyle = '#c1121f';
  ctx.beginPath(); ctx.arc(S / 2, S / 2, S / 2 - 17, 0, 7); ctx.stroke();
  ctx.fillStyle = '#111';
  ctx.font = 'bold 96px "Arial", sans-serif';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText('110', S / 2, S / 2 + 6);
  return c;
}

// ---------- номерной знак (европейский формат) ----------
// синяя полоса ЕС слева с кольцом звёзд + код страны, белый фон,
// чёрные знаки в стиле DIN. Формат РФ: Б ЦЦЦ ББ РР
const RU_LETTERS = 'АВЕКМНОРСТУХ';          // буквы, совпадающие с латиницей
const RU_REGIONS = [77, 97, 99, 177, 197, 199, 777, 750, 790];
function randomPlateText() {
  const L = () => RU_LETTERS[(Math.random() * RU_LETTERS.length) | 0];
  const D = () => String((Math.random() * 10) | 0);
  return `${L()} ${D()}${D()}${D()} ${L()}${L()} ${RU_REGIONS[(Math.random() * RU_REGIONS.length) | 0]}`;
}

function star5(ctx, cx, cy, r) {
  ctx.beginPath();
  for (let i = 0; i < 5; i++) {
    const a = -Math.PI / 2 + i * (Math.PI * 2 / 5);
    const a2 = a + Math.PI / 5;
    ctx.lineTo(cx + Math.cos(a) * r, cy + Math.sin(a) * r);
    ctx.lineTo(cx + Math.cos(a2) * r * 0.42, cy + Math.sin(a2) * r * 0.42);
  }
  ctx.closePath(); ctx.fill();
}

function plateCanvas(text = randomPlateText(), countryCode = 'RUS') {
  const W = 512, H = 112;
  const c = makeCanvas(W, H), ctx = c.getContext('2d');
  // белый фон с лёгким градиентом «металла»
  const grad = ctx.createLinearGradient(0, 0, 0, H);
  grad.addColorStop(0, '#fbfbf8'); grad.addColorStop(0.5, '#f1f1ec'); grad.addColorStop(1, '#e9e9e3');
  ctx.fillStyle = grad; ctx.fillRect(0, 0, W, H);
  // синяя полоса ЕС
  const bw = 66;
  ctx.fillStyle = '#003399'; ctx.fillRect(0, 0, bw, H);
  // кольцо из 12 золотых звёзд
  ctx.fillStyle = '#FFCC00';
  const cx = bw / 2, cy = H * 0.40, rr = 21;
  for (let i = 0; i < 12; i++) {
    const a = i * (Math.PI * 2 / 12) - Math.PI / 2;
    star5(ctx, cx + Math.cos(a) * rr, cy + Math.sin(a) * rr, 4.6);
  }
  // код страны
  ctx.fillStyle = '#fff';
  ctx.font = '900 27px "Arial", sans-serif';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText(countryCode, cx, H * 0.80);
  // чёрные знаки (в стиле DIN: узкие, жирные)
  ctx.fillStyle = '#14161a';
  ctx.font = '900 76px "Arial Narrow", "Arial", sans-serif';
  ctx.fillText(text, (bw + W) / 2 + 4, H / 2 + 3);
  // тонкая рамка
  ctx.strokeStyle = 'rgba(20,22,26,0.55)'; ctx.lineWidth = 5;
  ctx.strokeRect(2.5, 2.5, W - 5, H - 5);
  return c;
}


// ---------- надпись на фургоне ----------
function vanTextCanvas() {
  const W = 512, H = 128;
  const c = makeCanvas(W, H), ctx = c.getContext('2d');
  ctx.clearRect(0, 0, W, H);
  ctx.fillStyle = '#1d5bbf';
  ctx.fillRect(0, 0, W, 14);
  ctx.fillRect(0, H - 14, W, 14);
  ctx.fillStyle = '#20313f';
  ctx.font = 'bold 52px "Arial", sans-serif';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText('ДОСТАВКА · МОСКВА', W / 2, H / 2);
  return c;
}

// ---------- фура: боковая текстура ----------
function truckBoxCanvas() {
  const W = 1024, H = 512;
  const c = makeCanvas(W, H), ctx = c.getContext('2d');
  ctx.fillStyle = '#dfe3e6'; ctx.fillRect(0, 0, W, H);
  // гофра
  ctx.strokeStyle = 'rgba(120,128,134,0.35)'; ctx.lineWidth = 3;
  for (let x = 0; x < W; x += 26) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, H); ctx.stroke(); }
  noisePass(ctx, W, H, { count: 1200, size: 2, alpha: 0.03 });
  ctx.fillStyle = '#1d5bbf';
  ctx.fillRect(0, H - 96, W, 22);
  ctx.fillStyle = '#20313f';
  ctx.font = 'bold 74px "Arial", sans-serif';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText('ПЕРЕВОЗКИ ПО ВСЕЙ РОССИИ', W / 2, H / 2 - 30);
  ctx.font = 'bold 40px "Arial", sans-serif';
  ctx.fillStyle = '#5a6570';
  ctx.fillText('www.perevozki-moskva.ru', W / 2, H / 2 + 60);
  return c;
}

// ============================================================
export function createTextures(renderer) {
  const maxAniso = Math.min(8, renderer.capabilities.getMaxAnisotropy());

  const road = tex(roadCanvas(), { aniso: maxAniso });
  const roadRough = tex(roadRoughCanvas(), { srgb: false, aniso: maxAniso });
  const roadNormal = tex(heightToNormal(noiseHeightCanvas(256), 2.2), { srgb: false, aniso: 4 });
  roadNormal.repeat.set(10, 320);

  const grass = tex(grassCanvas(), { aniso: maxAniso });
  const concrete = tex(concreteCanvas(), { aniso: maxAniso });
  const grid = tex(gridCanvas(), { aniso: 2 });
  const cloud = tex(cloudCanvas(), { aniso: 2 });
  const spark = tex(sparkCanvas(), { aniso: 2 });

  const gantries = GANTRY_SETS.map(([t, r]) => tex(gantryCanvas(t, r), { aniso: maxAniso }));
  gantries.forEach(g => { g.wrapS = g.wrapT = THREE.ClampToEdgeWrapping; });

  const kmCache = new Map();
  const kmTex = n => {
    const key = ((n % 108) + 108) % 108 + 1;
    if (!kmCache.has(key)) {
      const t = tex(kmCanvas(key), { aniso: 4 });
      t.wrapS = t.wrapT = THREE.ClampToEdgeWrapping;
      kmCache.set(key, t);
    }
    return kmCache.get(key);
  };

  const plate = tex(plateCanvas(), { aniso: 4 });
  plate.wrapS = plate.wrapT = THREE.ClampToEdgeWrapping;
  // пул случайных европейских номеров
  const plates = [plate];
  for (let i = 0; i < 11; i++) {
    const p = tex(plateCanvas(), { aniso: 4 });
    p.wrapS = p.wrapT = THREE.ClampToEdgeWrapping;
    plates.push(p);
  }
  const sign110 = tex(sign110Canvas(), { aniso: 4 });
  sign110.wrapS = sign110.wrapT = THREE.ClampToEdgeWrapping;
  const vanText = tex(vanTextCanvas(), { aniso: 4 });
  vanText.wrapS = vanText.wrapT = THREE.ClampToEdgeWrapping;
  const truckBox = tex(truckBoxCanvas(), { aniso: maxAniso });
  truckBox.wrapS = truckBox.wrapT = THREE.ClampToEdgeWrapping;

  return { road, roadRough, roadNormal, grass, concrete, grid, cloud, spark, gantries, kmTex, plate, plates, sign110, vanText, truckBox };
}
