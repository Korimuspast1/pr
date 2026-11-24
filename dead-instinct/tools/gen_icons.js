/* Генерирует иконки лаунчера для Android (все плотности + адаптивные слои). */
const fs = require('fs');
const path = require('path');
const { createCanvas } = require('@napi-rs/canvas');
const ROOT = path.resolve(__dirname, '../android/res');

/* рисуем значок в квадрате size×size: рука из темноты + след укуса */
function drawIcon(size, withBg) {
  const c = createCanvas(size, size);
  const g = c.getContext('2d');
  const S = size / 32;                    // сетка 32×32
  const R = (x, y, w, h, col) => { g.fillStyle = col; g.fillRect(Math.round(x * S), Math.round(y * S), Math.ceil(w * S), Math.ceil(h * S)); };
  if (withBg) {
    g.fillStyle = '#0b0d0f'; g.fillRect(0, 0, size, size);
    // холодное свечение
    const grd = g.createRadialGradient(size * 0.5, size * 0.42, 0, size * 0.5, size * 0.5, size * 0.75);
    grd.addColorStop(0, '#2a1410'); grd.addColorStop(1, '#07080a');
    g.fillStyle = grd; g.fillRect(0, 0, size, size);
    // пятна крови
    g.fillStyle = 'rgba(122,20,20,0.55)';
    for (const [x, y, r] of [[6, 25, 1.6], [8, 26.5, 1.0], [25, 6, 1.2], [24, 8, 0.7]]) {
      g.beginPath(); g.arc(x * S, y * S, r * S, 0, 6.284); g.fill();
    }
  }
  // рука (тыльная сторона, тянется снизу)
  R(11, 14, 10, 16, '#7c6a52');        // предплечье/ладонь
  R(11, 14, 10, 3, '#8d7a5e');
  R(10, 8, 2.4, 7, '#8d7a5e');         // пальцы
  R(13, 6.5, 2.4, 8, '#8d7a5e');
  R(16, 6, 2.4, 8.5, '#8d7a5e');
  R(19, 7, 2.4, 8, '#8d7a5e');
  R(9.5, 16, 2.4, 5, '#7c6a52');       // большой палец
  // тени между пальцами
  R(12.4, 8, 0.8, 6, '#5c4c3a'); R(15.4, 7, 0.8, 6.5, '#5c4c3a'); R(18.4, 7.5, 0.8, 6, '#5c4c3a');
  // укус на запястье
  R(14, 20, 3, 2.4, '#5a1212');
  R(14.5, 19.4, 0.8, 1.2, '#d8d2b0'); R(15.6, 19.2, 0.8, 1.4, '#d8d2b0'); R(16.6, 19.6, 0.8, 1.0, '#d8d2b0');
  R(14.4, 21.6, 0.8, 1.0, '#d8d2b0'); R(15.8, 21.8, 0.8, 1.0, '#d8d2b0');
  // кровь стекает
  R(15.4, 22.6, 0.8, 3.2, '#7a1414');
  // грязь и раны
  R(12, 24, 1.4, 1.4, '#4a3e2e'); R(19, 22, 1.2, 1.2, '#4a3e2e');
  return c;
}

const sizes = { mdpi: 48, hdpi: 72, xhdpi: 96, xxhdpi: 144, xxxhdpi: 192 };
for (const [d, s] of Object.entries(sizes)) {
  const dir = path.join(ROOT, 'mipmap-' + d);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'ic_launcher.png'), drawIcon(s, true).toBuffer('image/png'));
  // адаптивные слои: фон/передний план 108dp
  const fgS = Math.round(s * 1.5), bgS = fgS;
  // foreground: значок по центру в «safe zone» (66/108)
  const fg = createCanvas(fgS, fgS);
  const fgCtx = fg.getContext('2d');
  const inner = drawIcon(Math.round(fgS * 0.62), false);
  fgCtx.drawImage(inner, Math.round(fgS * 0.19), Math.round(fgS * 0.19));
  fs.writeFileSync(path.join(dir, 'ic_launcher_fg.png'), fg.toBuffer('image/png'));
  const bg = createCanvas(bgS, bgS);
  const bgCtx = bg.getContext('2d');
  const grd = bgCtx.createRadialGradient(bgS * 0.5, bgS * 0.42, 0, bgS * 0.5, bgS * 0.5, bgS * 0.8);
  grd.addColorStop(0, '#2a1410'); grd.addColorStop(1, '#07080a');
  bgCtx.fillStyle = grd; bgCtx.fillRect(0, 0, bgS, bgS);
  bgCtx.fillStyle = 'rgba(122,20,20,0.45)';
  for (const [x, y, r] of [[0.18, 0.8, 0.05], [0.25, 0.85, 0.03], [0.8, 0.2, 0.04]]) {
    bgCtx.beginPath(); bgCtx.arc(x * bgS, y * bgS, r * bgS, 0, 6.284); bgCtx.fill();
  }
  fs.writeFileSync(path.join(dir, 'ic_launcher_bg.png'), bg.toBuffer('image/png'));
  console.log('иконки', d, sizes[d]);
}
// превью для README
fs.mkdirSync(path.join(__dirname, 'shots'), { recursive: true });
fs.writeFileSync(path.join(__dirname, 'shots', 'icon.png'), drawIcon(512, true).toBuffer('image/png'));
console.log('готово');
