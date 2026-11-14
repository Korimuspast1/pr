'use strict';
/* ===== Ландшафт =====
   Земля рисуется не клетками, а попиксельно: поля «суша», «высота», «лес», «сухость» и «песок» плавно
   переходят от клетки к клетке, поэтому берега, опушки и склоны получаются живыми, а не ступенчатыми.
   Рельеф освещён с северо-запада, как на физической карте. Поверх кладутся деревья, кусты, валуны и руда.
   Текстура вдвое мельче игровой клетки (32 точки на клетку — как у спрайтов зданий) и строится участками
   по мере надобности: вся карта в таком качестве в память телефона не поместилась бы. */
const TERR = (() => {
  const PPT = 32, CT = 8, CPX = PPT * CT, OV = 8; // точек на клетку, клеток в участке, точек на клетку в обзорной карте
  let S = null, W = 0, H = 0, F = null, chunks = new Map(), stamp = 0, over = null, spr = null, baked = -1;
  const NA = new Float32Array(65536), NB = new Float32Array(65536);

  // два «бесшовных» шума 256×256: из них берутся все неровности
  function bake(seed) {
    if (baked === seed) return; baked = seed; const rnd = mulberry32(seed * 7 + 13);
    for (const out of [NA, NB]) {
      out.fill(0); let amp = 0.5, tot = 0;
      for (let o = 0; o < 4; o++) {
        const n = 8 << o, g = new Float32Array(n * n), sc = n / 256; for (let i = 0; i < g.length; i++) g[i] = rnd();
        for (let y = 0; y < 256; y++) {
          const v = y * sc, y0 = Math.floor(v), fy = v - y0, sy = fy * fy * (3 - 2 * fy), r0 = (y0 % n) * n, r1 = ((y0 + 1) % n) * n;
          for (let x = 0; x < 256; x++) {
            const u = x * sc, x0 = Math.floor(u), fx = u - x0, sx = fx * fx * (3 - 2 * fx), c0 = x0 % n, c1 = (x0 + 1) % n;
            const a = g[r0 + c0] + (g[r0 + c1] - g[r0 + c0]) * sx, b = g[r1 + c0] + (g[r1 + c1] - g[r1 + c0]) * sx;
            out[(y << 8) | x] += (a + (b - a) * sy) * amp;
          }
        }
        tot += amp; amp *= 0.5;
      }
      for (let i = 0; i < 65536; i++) out[i] /= tot;
    }
  }
  const ns = (T, x, y) => {
    const x0 = Math.floor(x), y0 = Math.floor(y), fx = x - x0, fy = y - y0, r0 = (y0 & 255) << 8, r1 = ((y0 + 1) & 255) << 8, c0 = x0 & 255, c1 = (x0 + 1) & 255;
    const a = T[r0 | c0], b = T[r0 | c1], c = T[r1 | c0], d = T[r1 | c1];
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
  };
  const grain = (x, y) => { let h = Math.imul(x, 73856093) ^ Math.imul(y, 19349663); h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) & 255) / 255; };
  const th = (x, y, k) => { let h = (Math.imul(x, 73856093) ^ Math.imul(y, 19349663) ^ Math.imul(k, 83492791)) >>> 0; h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };

  function fields() {
    const n = W * H, t = S.ter;
    if (!F || F.land.length !== n) F = { land: new Float32Array(n), deep: new Float32Array(n), hgt: new Float32Array(n), forest: new Float32Array(n), dry: new Float32Array(n), sand: new Float32Array(n) };
    for (let i = 0; i < n; i++) {
      const q = t[i];
      F.land[i] = q > 1 ? 1 : 0; F.deep[i] = q === 0 ? 1 : 0; F.hgt[i] = q === TR.MOUNT ? 1 : q === TR.HILL ? 0.4 : 0;
      F.forest[i] = q === TR.FOREST ? 1 : 0; F.dry[i] = q === TR.PLAIN ? 1 : q === TR.SAND ? 0.5 : q === TR.HILL ? 0.35 : 0; F.sand[i] = q === TR.SAND ? 1 : 0;
    }
  }

  /* Попиксельная отрисовка прямоугольника. x0, y0 — левый верхний угол в точках (32 на клетку); w, h — размер
     результата; step — шаг выборки (1 — подробно, 4 — обзорная карта). */
  function raster(x0, y0, w, h, step) {
    const img = new ImageData(w, h), D = img.data, hw = w + 2, hb = new Float32Array(hw * (h + 2));
    const land = F.land, deep = F.deep, hgt = F.hgt, forest = F.forest, dry = F.dry, sand = F.sand, W1 = W - 1, H1 = H - 1, inv = 1 / PPT;
    // 1) высоты (с запасом в одну точку по краям — для освещения)
    for (let j = 0; j < h + 2; j++) {
      const Y = y0 + (j - 1) * step, v = (Y + 0.5) * inv - 0.5, ty = Math.floor(v), fy0 = v - ty, fy = fy0 * fy0 * (3 - 2 * fy0), ra = (ty < 0 ? 0 : ty > H1 ? H1 : ty) * W, rb = (ty + 1 < 0 ? 0 : ty + 1 > H1 ? H1 : ty + 1) * W;
      for (let i = 0; i < hw; i++) {
        const X = x0 + (i - 1) * step, u = (X + 0.5) * inv - 0.5, tx = Math.floor(u), fx0 = u - tx, fx = fx0 * fx0 * (3 - 2 * fx0), ca = tx < 0 ? 0 : tx > W1 ? W1 : tx, cb = tx + 1 < 0 ? 0 : tx + 1 > W1 ? W1 : tx + 1;
        const a = hgt[ra + ca], b = hgt[ra + cb], c = hgt[rb + ca], d = hgt[rb + cb]; let hv = a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        if (hv > 0.012) {
          const r = ns(NA, X * 0.42, Y * 0.42), rid = 1 - Math.abs(2 * r - 1), k = hv > 0.5 ? 1 : hv * 2;
          hv = hv * (0.66 + 0.62 * rid) + (ns(NB, X * 1.3, Y * 1.3) - 0.5) * 0.13 * k;
        }
        hb[j * hw + i] = hv;
      }
    }
    // 2) цвет
    const LK = 5.2 / step;
    for (let j = 0; j < h; j++) {
      const Y = y0 + j * step, v = (Y + 0.5) * inv - 0.5, ty = Math.floor(v), fy0 = v - ty, fy = fy0 * fy0 * (3 - 2 * fy0), ra = (ty < 0 ? 0 : ty > H1 ? H1 : ty) * W, rb = (ty + 1 < 0 ? 0 : ty + 1 > H1 ? H1 : ty + 1) * W;
      for (let i = 0; i < w; i++) {
        const X = x0 + i * step, u = (X + 0.5) * inv - 0.5, tx = Math.floor(u), fx0 = u - tx, fx = fx0 * fx0 * (3 - 2 * fx0), ca = tx < 0 ? 0 : tx > W1 ? W1 : tx, cb = tx + 1 < 0 ? 0 : tx + 1 > W1 ? W1 : tx + 1;
        const i00 = ra + ca, i10 = ra + cb, i01 = rb + ca, i11 = rb + cb, fxy = fx * fy;
        const bil = T => { const a = T[i00]; return a + (T[i10] - a) * fx + (T[i01] - a) * fy + (a - T[i10] - T[i01] + T[i11]) * fxy; };
        const L = bil(land) + (ns(NA, X * 0.9 + 37, Y * 0.9 + 11) - 0.5) * 0.34, gr = grain(X, Y);
        let r, g, b;
        if (L < 0.5) { // ----- вода: глубина, волны, пена у берега
          const sh = 0.5 - L; let t = bil(deep) * 0.85 + sh * 0.8; if (t > 1) t = 1;
          r = 52 + 92 * (1 - t) * (1 - t); g = 118 + 84 * (1 - t); b = 168 + 46 * (1 - t); // глубина — насыщенный синий, мель — бирюза
          const wv = ns(NB, X * 0.3 + Y * 0.12, Y * 1.15), wv2 = ns(NA, X * 0.85 - Y * 0.25, Y * 0.5 + 4);
          if (wv > 0.56) { const k = (wv - 0.56) * 3.2; r += 16 * k; g += 20 * k; b += 18 * k; } // гребни волн
          else if (wv < 0.42) { const k = (0.42 - wv) * 2.4; r -= 10 * k; g -= 12 * k; b -= 8 * k; }
          if (wv2 > 0.62 && sh > 0.1) { r += 10; g += 12; b += 10; } // рябь
          if (sh < 0.045) { if (ns(NA, X * 1.7 + 5, Y * 1.7) + wv2 * 0.5 > 0.85) { r = 236; g = 246; b = 250; } else { r = 168; g = 216; b = 228; } } // пена
          else if (sh < 0.13) { const k = (0.13 - sh) / 0.085 * 0.8; r += (150 - r) * k; g += (212 - g) * k; b += (222 - b) * k; }
          const m = 1 + (gr - 0.5) * 0.06; r *= m; g *= m; b *= m;
        } else { // ----- суша
          const k0 = (j + 1) * hw + i + 1, hv = hb[k0], sa = bil(sand) + (ns(NA, X * 0.8 + 90, Y * 0.8 + 50) - 0.5) * 0.4, big = ns(NB, X * 0.11, Y * 0.11);
          let li = 1 + (hb[k0 - 1] - hb[k0 + 1] + hb[k0 - hw] - hb[k0 + hw]) * LK; if (li < 0.52) li = 0.52; else if (li > 1.5) li = 1.5;
          if (hv > 0.9) { // снежные шапки с тенями
            const s2 = ns(NB, X * 1.6, Y * 1.6);
            if (hv > 0.96 || s2 > 0.42) { r = 242; g = 246; b = 252; li = 0.78 + (li - 0.78) * 0.8; }
            else { r = 196; g = 210; b = 232; li = 0.7 + (li - 0.7) * 0.85; }
            if (li < 0.85) { r = 178; g = 198; b = 228; }
          } else if (hv > 0.5) { // скала: слои породы, трещины, травяные полки
            const k = hv > 0.8 ? 1 : (hv - 0.5) / 0.3, lay = ns(NB, X * 0.9, Y * 2.6);
            r = 104 + 30 * k + 26 * lay; g = 102 + 28 * k + 24 * lay; b = 106 + 30 * k + 26 * lay;
            const bd = hv * 13 - Math.floor(hv * 13); if (bd < 0.18) li *= 0.8;
            li += (gr - 0.5) * 0.16;
            if (hv > 0.8 && ns(NA, X * 2.3, Y * 2.3) > 0.52) { r = 228; g = 234; b = 244; } // языки снега
            if (hv < 0.62 && ns(NB, X * 1.4 + 9, Y * 1.4) > 0.55) { r += 22; g += 30; b += 12; }
          } else if (L < 0.59 || sa > 0.5) { // пляж: песок, рябь, галька
            const rip = ns(NB, X * 0.5, Y * 2.8);
            r = 228 + 14 * rip; g = 206 + 14 * rip; b = 150 + 12 * rip;
            if (L < 0.545) { r = 206; g = 184; b = 132; }
            li += (gr - 0.5) * 0.12 + (ns(NA, X * 0.6 + 3, Y * 2.2) - 0.5) * 0.12;
            if (gr > 0.99) { r = 148; g = 138; b = 118; li *= 0.9; }
            else if (gr < 0.006 && L > 0.56) { r = 242; g = 232; b = 204; }
          } else {
            let dq = bil(dry) + (big - 0.5) * 0.7; dq = dq < 0 ? 0 : dq > 1 ? 1 : dq;
            r = 76 + 88 * dq; g = 152 + 26 * dq; b = 52 + 30 * dq; // сочный луг → сухая равнина
            let fo = (bil(forest) - 0.22) * 1.7; if (fo > 0) { if (fo > 1) fo = 1; fo *= 0.85; r += (46 - r) * fo; g += (96 - g) * fo; b += (44 - b) * fo; } // лесная подстилка
            if (hv > 0.1) { let k = (hv - 0.1) / 0.4; if (k > 1) k = 1; r += (138 - r) * k; g += (128 - g) * k; b += (82 - b) * k; if (hv > 0.36) { const q = (hv - 0.36) / 0.14; r += (116 - r) * q; g += (112 - g) * q; b += (104 - b) * q; } }
            const patch = ns(NB, X * 0.32 + 11, Y * 0.32 + 21);
            li += (ns(NA, X * 0.5 + 7, Y * 0.5 + 3) - 0.5) * 0.15 + (gr - 0.5) * 0.08 + (patch - 0.5) * 0.12; // пёстрые поляны
            if (hv < 0.08 && fo <= 0) { // метёлки травы и редкие цветы
              if (gr > 0.985) li *= 0.82; else if (gr < 0.012) li *= 1.16;
              else if (dq < 0.5 && step === 1) { const fl = grain(X + 911, Y + 577); if (fl > 0.9972) { r = 250; g = 238; b = 168; li = 1; } else if (fl < 0.0022) { r = 238; g = 148; b = 188; li = 1; } else if (fl > 0.4988 && fl < 0.5010) { r = 248; g = 248; b = 242; li = 1; } else if (fl > 0.7495 && fl < 0.7506) { r = 240; g = 196; b = 90; li = 1; } }
            }
          }
          r *= li; g *= li; b *= li;
        }
        const o = (j * w + i) << 2; D[o] = r; D[o + 1] = g; D[o + 2] = b; D[o + 3] = 255;
      }
    }
    return img;
  }

  /* ----- деревья, кусты, валуны, руда: маленькие рисунки, которые расставляются поверх земли ----- */
  const R = (c, x, y, w, h, col) => { c.fillStyle = col; c.fillRect(x, y, w, h); };
  function mk(w, h, fn) { const cv = document.createElement('canvas'); cv.width = w; cv.height = h; fn(cv.getContext('2d')); return cv; }
  function shadow(c, cx, cy, rx, ry) { c.fillStyle = 'rgba(8,22,10,0.30)'; for (let j = -ry; j <= ry; j++) { const ww = Math.round(rx * Math.sqrt(1 - (j * j) / (ry * ry + 0.01))); c.fillRect(cx - ww, cy + j, ww * 2 + 1, 1); } }
  function disc(c, cx, cy, r, col) { c.fillStyle = col; for (let j = -r; j <= r; j++) { const ww = Math.round(Math.sqrt(r * r - j * j + r * 0.8)); c.fillRect(Math.round(cx - ww), Math.round(cy + j), ww * 2 + 1, 1); } }
  function spruce(w, h, P) { // ель: ярусы свисающих лап, слева свет, справа тень
    return mk(w + 8, h + 3, c => {
      const cx = (w >> 1) + 1, bot = h - 4, tiers = 5;
      shadow(c, cx + 4, h, Math.round(w * 0.42), 2);
      R(c, cx - 1, bot, 3, 4, '#4e3018'); R(c, cx + 1, bot, 1, 4, '#33200e');
      for (let k = 0; k < tiers; k++) {
        const ya = Math.round(bot * k / (tiers + 0.6)), yb = Math.round(bot * (k + 1.6) / (tiers + 0.6)), wm = Math.max(3, Math.round(w * (0.32 + 0.68 * (k + 1) / tiers)));
        for (let y = ya; y < yb; y++) {
          const ww = Math.max(1, Math.round(wm * (y - ya + 1) / (yb - ya))), xa = cx - (ww >> 1);
          for (let x = 0; x < ww; x++) {
            const q = ww > 1 ? x / (ww - 1) : 0.4, edge = y >= yb - 2;
            R(c, xa + x, y, 1, 1, edge && x > 0 ? P[3] : q < 0.3 ? P[0] : q < 0.62 ? P[1] : P[2]);
          }
        }
        R(c, cx - (wm >> 1) + 1, ya, Math.max(1, wm >> 2), 1, P[0]); // светлая кромка яруса
      }
      R(c, cx - 1, 0, 1, 1, P[0]);
    });
  }
  function leafy(w, h, P, trunk) { // лиственное: крона с объёмом, ствол, ветви
    return mk(w + 8, h + 3, c => {
      const cx = (w >> 1) + 1, r = Math.floor(w / 2), cy = r + 1;
      shadow(c, cx + 4, h, Math.round(w * 0.46), 2);
      const tr = trunk || '#6a4826', trD = trunk ? '#a8a8b2' : '#42290f';
      R(c, cx - 1, cy + r - 3, 3, h - (cy + r - 3) + 1, tr); R(c, cx + 1, cy + r - 3, 1, h - (cy + r - 3) + 1, trD);
      R(c, cx - 3, cy + r - 1, 2, 2, tr); R(c, cx + 2, cy + r - 2, 2, 2, tr); // ветви
      disc(c, cx, cy, r, P[3]); disc(c, cx, cy - 1, r - 1, P[2]); disc(c, cx - 1, cy - 1, Math.max(2, r - 3), P[1]); disc(c, cx - 2, cy - 3, Math.max(1, r - 5), P[0]);
      for (let k = 0; k < r * 4; k++) {
        const a = th(w, h, k) * 6.283, d = th(w, k, h) * (r - 0.5), x = Math.round(cx + Math.cos(a) * d), y = Math.round(cy + Math.sin(a) * d * 0.92), v = th(k, w, h);
        R(c, x, y, 2, 1, v > 0.62 ? P[0] : v > 0.3 ? P[2] : P[3]);
      }
    });
  }
  function bush(P) { return mk(14, 10, c => { shadow(c, 8, 8, 5, 1); disc(c, 5, 5, 3, P[3]); disc(c, 5, 4, 2, P[1]); R(c, 3, 3, 2, 1, P[0]); R(c, 2, 4, 1, 1, P[2]); R(c, 7, 5, 1, 1, '#d8405a'); R(c, 4, 6, 1, 1, '#e05868'); R(c, 6, 3, 1, 1, '#c83848'); }); }
  function rock(w, h, hi, mid, lo) { return mk(w + 4, h + 3, c => {
    shadow(c, (w >> 1) + 3, h + 1, w >> 1, 1);
    for (let j = 0; j < h; j++) { const f = Math.sin(Math.PI * (j + 0.7) / (h + 0.4)), ww = Math.max(2, Math.round(w * (0.55 + 0.45 * f))), xa = ((w - ww) >> 1) + 1; R(c, xa, j, ww, 1, j < h * 0.35 ? hi : mid); R(c, xa + Math.round(ww * 0.6), j, ww - Math.round(ww * 0.6), 1, lo); if (j === 1) R(c, xa + 1, j, Math.max(1, ww >> 2), 1, '#e2e6ee'); }
    R(c, 2, h - 1, w - 2, 1, lo); R(c, 3, h - 2, 2, 1, '#5c8a3c'); // мох у основания
  }); }
  function ore(kind) { // залежь: груда камней с жилами — золото жёлтое, железо рыжее с синевой
    return mk(30, 24, c => {
      shadow(c, 17, 21, 12, 2);
      const st = [[3, 10, 11, 10], [11, 5, 12, 14], [19, 11, 9, 9]];
      for (const [x, y, w, h] of st) for (let j = 0; j < h; j++) { const f = Math.sin(Math.PI * (j + 1) / (h + 1)), ww = Math.max(3, Math.round(w * (0.6 + 0.4 * f))), xa = x + ((w - ww) >> 1); R(c, xa, y + j, ww, 1, j < 3 ? '#b2b5bd' : '#868a94'); R(c, xa + Math.round(ww * 0.62), y + j, ww - Math.round(ww * 0.62), 1, '#5b5f69'); }
      R(c, 3, 20, 25, 1, '#454851');
      const A = kind === 2 ? '#ffd640' : '#c8683a', B = kind === 2 ? '#fff3a0' : '#8fb4e6', C = kind === 2 ? '#c08a10' : '#7a3a1c';
      for (const [x, y] of [[6, 13], [9, 16], [14, 8], [16, 12], [13, 15], [21, 14], [23, 17], [18, 9]]) { R(c, x, y, 2, 2, A); R(c, x, y, 1, 1, B); R(c, x + 1, y + 2, 1, 1, C); }
    });
  }
  function sprites() {
    const CON = [['#4e9c4c', '#2f7440', '#1d5232', '#123522'], ['#469052', '#2a6c3e', '#1a4e30', '#103022'], ['#5aa650', '#37803e', '#22582c', '#143820'], ['#3e8448', '#265e38', '#18482c', '#0e2c1e']];
    const LEAF = [['#9ccc62', '#6aa648', '#467e34', '#2e5c2a'], ['#b2d26a', '#7cae4c', '#558638', '#38622c'], ['#88c25e', '#589c46', '#3a7436', '#27562a'], ['#c8b85a', '#a08e40', '#7a6c30', '#564c24']];
    spr = { con: [], leaf: [], bush: [bush(LEAF[0]), bush(LEAF[2])], rock: [rock(9, 6, '#b8bbc2', '#8c9099', '#5f636c'), rock(12, 8, '#b0b3ba', '#868a93', '#585c65'), rock(6, 4, '#bcbfc6', '#90949d', '#62666f'), rock(10, 7, '#a8abb4', '#7e828c', '#52565f')], ore: [null, ore(1), ore(2)] };
    for (let v = 0; v < 8; v++) spr.con.push(spruce(12 + (v % 4) * 2, 20 + ((v * 5) % 5) * 2, CON[v % 4]));
    for (let v = 0; v < 8; v++) spr.leaf.push(leafy(14 + (v % 4) * 2, 18 + (v % 3) * 2 + (v > 3 ? 2 : 0), LEAF[v % 4], v === 6 ? '#d8d4c8' : null));
  }
  // что растёт и лежит на клетке: список [рисунок, x, y основания] в точках карты
  function decor(tx, ty, out) {
    const i = ty * W + tx, t = S.ter[i], bx = tx * PPT, by = ty * PPT, T = (x, y) => (x < 0 || y < 0 || x >= W || y >= H) ? -1 : S.ter[y * W + x];
    if (S.res[i]) out.push([spr.ore[S.res[i]], bx + 1, by + 28]);
    if (t === TR.FOREST) {
      let fn = 0, hill = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const q = T(tx + dx, ty + dy); if ((dx || dy) && q === TR.FOREST) fn++; if (q === TR.HILL || q === TR.MOUNT) hill++; }
      const stand = ns(NB, tx * 3.1 + 40, ty * 3.1 + 80), dense = fn >= 5 ? 1 : fn >= 2 ? 0.9 : 0.74;
      for (let k = 0; k < 6; k++) {
        if (th(tx, ty, 100 + k) > dense) continue;
        const sx = k === 4 ? 16 : (k & 1) * 16 + 8, sy = k === 4 ? 18 : (k >> 1) * 15 + 12, x = bx + sx + (th(tx, ty, 110 + k) - 0.5) * 11, y = by + sy + (th(tx, ty, 120 + k) - 0.5) * 9;
        const con = (hill > 0 ? 0.8 : stand < 0.44 ? 0.85 : stand > 0.56 ? 0.1 : 0.5) > th(tx, ty, 130 + k), v = (th(tx, ty, 140 + k) * 8) | 0;
        out.push([con ? spr.con[v] : spr.leaf[v], x, y]);
      }
    } else if (t === TR.GRASS || t === TR.PLAIN) {
      if (!S.res[i] && th(tx, ty, 200) > (t === TR.GRASS ? 0.8 : 0.9)) out.push([spr.bush[t === TR.GRASS ? 0 : 1], bx + 4 + th(tx, ty, 201) * 20, by + 12 + th(tx, ty, 202) * 16]);
    } else if (t === TR.HILL) {
      if (!S.res[i]) for (let k = 0; k < 2; k++) if (th(tx, ty, 210 + k) > 0.55) out.push([spr.rock[(th(tx, ty, 213 + k) * 4) | 0], bx + 3 + th(tx, ty, 216 + k) * 22, by + 10 + th(tx, ty, 219 + k) * 18]);
    } else if (t === TR.SAND) {
      if (th(tx, ty, 230) > 0.86) out.push([spr.rock[2], bx + 4 + th(tx, ty, 231) * 22, by + 10 + th(tx, ty, 232) * 18]);
    }
  }
  function plant(c, tx0, ty0, tx1, ty1, ox, oy, k) { // k — масштаб (1 или 0.25)
    const list = []; for (let ty = Math.max(0, ty0); ty < Math.min(H, ty1); ty++) for (let tx = Math.max(0, tx0); tx < Math.min(W, tx1); tx++) decor(tx, ty, list);
    list.sort((a, b) => a[2] - b[2]);
    for (const [im, x, y] of list) {
      if (k === 1) c.drawImage(im, Math.round(x - (im.width - 8) / 2 - 1 - ox), Math.round(y - im.height + 3 - oy));
      else c.drawImage(im, (x - (im.width - 8) / 2 - 1) * k - ox, (y - im.height + 3) * k - oy, im.width * k, im.height * k);
    }
  }

  function makeChunk(cx, cy) {
    const cv = document.createElement('canvas'); cv.width = CPX; cv.height = CPX; const c = cv.getContext('2d');
    c.putImageData(raster(cx * CPX, cy * CPX, CPX, CPX, 1), 0, 0);
    plant(c, cx * CT - 1, cy * CT - 1, cx * CT + CT + 1, cy * CT + CT + 2, cx * CPX, cy * CPX, 1);
    return cv;
  }
  function overview() {
    if (over) return over;
    over = document.createElement('canvas'); over.width = W * OV; over.height = H * OV; const c = over.getContext('2d');
    c.putImageData(raster(0, 0, W * OV, H * OV, PPT / OV), 0, 0);
    c.imageSmoothingEnabled = true; plant(c, 0, 0, W, H, 0, 0, OV / PPT);
    return over;
  }

  function init(state) {
    S = state; W = S.W; H = S.H; chunks = new Map(); over = null; stamp = 0;
    bake(S.seed | 0); fields(); if (!spr) sprites();
  }
  // клетки изменились (вырубили лес под город): участки вокруг строятся заново
  function dirty(tx, ty, r) {
    if (!S) return; fields(); over = null; r = r || 1;
    for (let cy = Math.floor((ty - 1) / CT); cy <= Math.floor((ty + r + 1) / CT); cy++) for (let cx = Math.floor((tx - 1) / CT); cx <= Math.floor((tx + r + 1) / CT); cx++) chunks.delete(cy * 4096 + cx);
  }
  /* Рисует видимую часть карты. z — точек экрана на точку мира (масштаб × плотность экрана).
     Рисуем без преобразования и с целыми координатами: иначе между участками видны швы. */
  function draw(c, cam, z, vw, vh) {
    if (!S) return; stamp++;
    const X = wx => Math.round((wx - cam.x) * z), Y = wy => Math.round((wy - cam.y) * z);
    c.save(); c.setTransform(1, 0, 0, 1, 0, 0);
    if (z < 1.1) { c.imageSmoothingEnabled = true; const o = overview(); c.drawImage(o, X(0), Y(0), X(W * TS) - X(0), Y(H * TS) - Y(0)); c.restore(); return; }
    const cw = CT * TS, cx0 = Math.max(0, Math.floor(cam.x / cw)), cy0 = Math.max(0, Math.floor(cam.y / cw)), cx1 = Math.min(Math.ceil(W / CT) - 1, Math.floor((cam.x + vw / z) / cw)), cy1 = Math.min(Math.ceil(H / CT) - 1, Math.floor((cam.y + vh / z) / cw));
    c.imageSmoothingEnabled = z < 2; let made = 0, need = 0;
    for (let cy = cy0; cy <= cy1; cy++) for (let cx = cx0; cx <= cx1; cx++) {
      const key = cy * 4096 + cx; let ch = chunks.get(key); need++;
      if (!ch && made < 3) { ch = { cv: makeChunk(cx, cy), used: 0 }; chunks.set(key, ch); made++; }
      const dx = X(cx * cw), dy = Y(cy * cw), dw = X((cx + 1) * cw) - dx, dh = Y((cy + 1) * cw) - dy;
      if (ch) { ch.used = stamp; c.drawImage(ch.cv, dx, dy, dw, dh); }
      else for (let ty = 0; ty < CT; ty++) for (let tx = 0; tx < CT; tx++) { // участок ещё не готов: ровные цвета, через кадр-другой он появится
        const gx = cx * CT + tx, gy = cy * CT + ty; if (gx >= W || gy >= H) continue;
        c.fillStyle = TCOL[S.ter[gy * W + gx]]; const ax = X(gx * TS), ay = Y(gy * TS); c.fillRect(ax, ay, X((gx + 1) * TS) - ax, Y((gy + 1) * TS) - ay);
      }
    }
    // пока всё видимое готово — заранее строим участок по соседству, чтобы при прокрутке не было рывков
    if (!made) outer: for (let cy = cy0 - 1; cy <= cy1 + 1; cy++) for (let cx = cx0 - 1; cx <= cx1 + 1; cx++) {
      if (cx < 0 || cy < 0 || cx * CT >= W || cy * CT >= H || chunks.has(cy * 4096 + cx)) continue;
      chunks.set(cy * 4096 + cx, { cv: makeChunk(cx, cy), used: stamp }); break outer;
    }
    const cap = Math.max(48, need * 2 + 12);
    if (chunks.size > cap) { const old = Array.from(chunks.entries()).sort((a, b) => a[1].used - b[1].used); for (let k = 0; k < old.length && chunks.size > cap; k++) if (old[k][1].used !== stamp) chunks.delete(old[k][0]); }
    c.restore();
  }
  return { init, draw, dirty, overview, count: () => chunks.size };
})();
