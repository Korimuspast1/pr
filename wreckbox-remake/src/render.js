import {
  TINT_R, TINT_G, TINT_B, TINTS, EMPTY, WATER, REACTOR, GLASS, GRASS, SOIL, ROCK,
  BRICK, CONCRETE, PANEL, AERATED, PLASTIC, WOOD, SLATE, STEEL, CASTIRON, LEAD,
  GLASSBLOCK, SAND, AMMO, WIRE, DEBRIS, PAL, isSolid,
} from './materials.js';

// Renderer: bright daylight, layered sky, and a per-cell bake with real detail
// (bricks, panels, window frames, cracks, green structural outlines).
const PPS = 3; // baked pixels per world cell

const rnd = (a = 1, b = 0) => b + Math.random() * (a - b);

export class Renderer {
  constructor(game) {
    this.g = game;
    this.buf = document.createElement('canvas');
    this.bctx = this.buf.getContext('2d');
    this.img = null;
    this.cols = 0;
    this.rows = 0;
    this.x0 = 0;
    this.y0 = 0;
    this.clouds = null;
    this.skylineFar = null;
    this.skylineNear = null;
    this.sky = null;
    this.skyH = 0;
    this.vignette = null;
    this.time = 0;
    this.outdoor = null;
    this.outdoorFrame = -999;
  }

  // Which empty cells are "outside" (connected to the sky)? Used to draw the
  // green structural outline only along the building silhouette, like the
  // original's schematic skin, instead of on every room edge.
  computeOutdoor() {
    const w = this.g.world;
    const n = w.n;
    if (!this.outdoor || this.outdoor.length !== n) this.outdoor = new Uint8Array(n);
    const out = this.outdoor;
    out.fill(0);
    const q = new Int32Array(n);
    let head = 0, tail = 0;
    for (let x = 0; x < w.w; x++) {
      const i = x;
      if (!w.mat[i]) { out[i] = 1; q[tail++] = i; }
    }
    while (head < tail) {
      const i = q[head++];
      const x = i % w.w, y = (i / w.w) | 0;
      if (x > 0) { const j = i - 1; if (!out[j] && !w.mat[j]) { out[j] = 1; q[tail++] = j; } }
      if (x < w.w - 1) { const j = i + 1; if (!out[j] && !w.mat[j]) { out[j] = 1; q[tail++] = j; } }
      if (y > 0) { const j = i - w.w; if (!out[j] && !w.mat[j]) { out[j] = 1; q[tail++] = j; } }
      if (y < w.h - 1) { const j = i + w.w; if (!out[j] && !w.mat[j]) { out[j] = 1; q[tail++] = j; } }
    }
  }

  isOutdoor(x, y) {
    const w = this.g.world;
    if (x < 0 || y < 0 || x >= w.w || y >= w.h) return true;
    return this.outdoor[y * w.w + x] === 1;
  }

  // ------------------------------------------------------------------ scenery
  makeCloudSheet(seed, puffs, scale) {
    const W = 1024, H = 320;
    const cv = document.createElement('canvas');
    cv.width = W; cv.height = H;
    const c = cv.getContext('2d');
    let s = seed;
    const r = () => {
      s = (s * 1664525 + 1013904223) >>> 0;
      return s / 4294967296;
    };
    for (let p = 0; p < puffs; p++) {
      const cx = r() * W;
      const cy = H * (0.25 + r() * 0.45);
      const size = (28 + r() * 46) * scale;
      const lobes = 5 + ((r() * 5) | 0);
      // shadow under the cloud
      c.fillStyle = 'rgba(170,195,215,0.55)';
      for (let i = 0; i < lobes; i++) {
        const a = (i / lobes) * Math.PI * 2;
        c.beginPath();
        c.ellipse(cx + Math.cos(a) * size * 0.62, cy + 8 * scale + Math.abs(Math.sin(a)) * size * 0.18,
          size * 0.62, size * 0.3, 0, 0, Math.PI * 2);
        c.fill();
      }
      // bright body
      c.fillStyle = 'rgba(255,255,255,0.97)';
      for (let i = 0; i < lobes; i++) {
        const a = (i / lobes) * Math.PI * 2 + r();
        const rr = size * (0.42 + r() * 0.34);
        c.beginPath();
        c.arc(cx + Math.cos(a) * size * 0.6, cy - size * 0.1 + Math.sin(a) * size * 0.22, rr, 0, Math.PI * 2);
        c.fill();
      }
      c.beginPath();
      c.arc(cx, cy - size * 0.18, size * 0.62, 0, Math.PI * 2);
      c.fill();
    }
    return cv;
  }

  makeSkyline(seed, color, height, cellSize, litColor) {
    const W = 2048, H = 420;
    const cv = document.createElement('canvas');
    cv.width = W; cv.height = H;
    const c = cv.getContext('2d');
    let s = seed;
    const r = () => {
      s = (s * 1664525 + 1013904223) >>> 0;
      return s / 4294967296;
    };
    c.fillStyle = color;
    let x = 0;
    while (x < W) {
      const w = 26 + r() * 70;
      const h = height * (0.35 + r() * 0.65);
      c.fillRect(x, H - h, w, h);
      // rooftop details
      if (r() < 0.4) c.fillRect(x + w * 0.3, H - h - 14, 4, 14);
      if (r() < 0.3) {
        c.fillRect(x + w * 0.1, H - h - 8, w * 0.35, 4);
      }
      // windows
      if (litColor) {
        c.fillStyle = litColor;
        for (let yy = H - h + 10; yy < H - 8; yy += 10) {
          for (let xx = x + 6; xx < x + w - 6; xx += 9) {
            if (r() < 0.55) c.fillRect(xx, yy, 4, 5);
          }
        }
        c.fillStyle = color;
      }
      x += w + 4 + r() * 16;
    }
    return cv;
  }

  ensureScenery() {
    if (this.clouds) return;
    this.clouds = [
      { sheet: this.makeCloudSheet(7, 5, 1.35), speed: 5, parallax: 0.035, y: 0.1, alpha: 0.5 },
      { sheet: this.makeCloudSheet(23, 6, 1.0), speed: 9, parallax: 0.07, y: 0.16, alpha: 0.8 },
      { sheet: this.makeCloudSheet(59, 5, 0.7), speed: 15, parallax: 0.12, y: 0.24, alpha: 0.95 },
    ];
    // pre-scale every sheet once: per-frame scaled blits are far too slow
    for (const layer of this.clouds) {
      const k = 0.9 + layer.parallax * 2.2;
      layer.scaled = this.scaleSheet(layer.sheet, k);
    }
    this.skylineFar = this.makeSkyline(11, 'rgba(174,203,224,0.85)', 250, 1, 'rgba(196,220,238,0.35)');
    this.skylineNear = this.makeSkyline(97, 'rgba(150,183,208,0.92)', 330, 1, 'rgba(178,208,230,0.45)');
    this.skylineFarS = this.scaleSheet(this.skylineFar, 0.9);
    this.skylineNearS = this.scaleSheet(this.skylineNear, 1.0);
  }

  scaleSheet(sheet, k) {
    if (k === 1) return sheet;
    const c = document.createElement('canvas');
    c.width = Math.max(1, Math.round(sheet.width * k));
    c.height = Math.max(1, Math.round(sheet.height * k));
    const cx = c.getContext('2d');
    cx.imageSmoothingEnabled = true;
    cx.drawImage(sheet, 0, 0, c.width, c.height);
    return c;
  }

  drawSky(ctx, cam) {
    const W = ctx.canvas.width, H = ctx.canvas.height;
    // The gradient and the sun bloom are expensive to rasterise every frame, so
    // they are baked once into offscreen canvases and blitted.
    if (!this.skyCanvas || this.skyW !== W || this.skyH !== H) {
      this.skyW = W;
      this.skyH = H;
      const c = (this.skyCanvas = document.createElement('canvas'));
      c.width = W; c.height = H;
      const cx = c.getContext('2d');
      const grad = cx.createLinearGradient(0, 0, 0, H);
      grad.addColorStop(0, '#61aee0');
      grad.addColorStop(0.35, '#86c3ea');
      grad.addColorStop(0.62, '#b6dcf3');
      grad.addColorStop(1, '#e3f1f8');
      cx.fillStyle = grad;
      cx.fillRect(0, 0, W, H);

      const b = (this.bloomCanvas = document.createElement('canvas'));
      const bs = (b.width = b.height = 512);
      const bx = b.getContext('2d');
      const bloom = bx.createRadialGradient(bs / 2, bs / 2, 0, bs / 2, bs / 2, bs / 2);
      bloom.addColorStop(0, 'rgba(255,252,225,0.92)');
      bloom.addColorStop(0.10, 'rgba(255,246,200,0.40)');
      bloom.addColorStop(0.42, 'rgba(255,240,190,0.12)');
      bloom.addColorStop(1, 'rgba(255,240,190,0)');
      bx.fillStyle = bloom;
      bx.fillRect(0, 0, bs, bs);
    }
    ctx.drawImage(this.skyCanvas, 0, 0);

    // sun with bloom
    const sunX = W * 0.78 - cam.x * 0.006;
    const sunY = H * 0.13 - cam.y * 0.006;
    const br = H * 0.42;
    ctx.drawImage(this.bloomCanvas, sunX - br, sunY - br, br * 2, br * 2);
    ctx.fillStyle = '#fffdf0';
    ctx.beginPath();
    ctx.arc(sunX, sunY, H * 0.032, 0, Math.PI * 2);
    ctx.fill();

    // parallax skyline at the horizon
    this.ensureScenery();
    const horizon = (this.g.world.groundY * this.g.cell - cam.y) * cam.zoom;
    const drawLayer = (sheet, parallax, offsetY) => {
      const sw = sheet.width;
      const sh = sheet.height;
      let x = -(((cam.x * parallax) % sw) + sw) % sw;
      const y = Math.round(horizon - sh + offsetY);
      while (x < W) {
        ctx.drawImage(sheet, x, y);
        x += sw;
      }
    };
    drawLayer(this.skylineFarS, 0.18, 26);
    drawLayer(this.skylineNearS, 0.32, 6);

    // clouds
    for (const layer of this.clouds) {
      const sheet = layer.scaled;
      const sh = sheet.height;
      const sw = sheet.width;
      ctx.globalAlpha = layer.alpha;
      let x = -(((cam.x * layer.parallax + this.time * layer.speed) % sw) + sw) % sw;
      const y = Math.round(H * layer.y - cam.y * layer.parallax * 0.9);
      while (x < W) {
        ctx.drawImage(sheet, x, y);
        x += sw;
      }
      ctx.globalAlpha = 1;
    }
  }

  // ---------------------------------------------------------------- world bake
  ensureBuffer(cols, rows) {
    if (this.cols === cols && this.rows === rows && this.img) return;
    this.cols = cols;
    this.rows = rows;
    this.buf.width = cols * PPS;
    this.buf.height = rows * PPS;
    this.img = this.bctx.createImageData(cols * PPS, rows * PPS);
  }

  bakeView() {
    const g = this.g;
    const w = g.world;
    if (g.frame - this.outdoorFrame > 6) {
      this.outdoorFrame = g.frame;
      this.computeOutdoor();
    }
    const cell = g.cell;
    const cam = g.cam;
    const cols = Math.ceil(g.canvas.width / cam.zoom / cell) + 4;
    const rows = Math.ceil(g.canvas.height / cam.zoom / cell) + 4;
    this.ensureBuffer(cols, rows);
    const x0 = Math.floor(cam.x / cell) - 2;
    const y0 = Math.floor(cam.y / cell) - 2;
    this.x0 = x0;
    this.y0 = y0;
    const d = this.img.data;
    const stride = cols * PPS;
    const t = g.time;
    const GLASSC = PAL.glassCar;
    const flick = 0.86 + Math.sin(t * 16) * 0.08 + Math.random() * 0.06;

    for (let ry = 0; ry < rows; ry++) {
      const cy = y0 + ry;
      for (let rx = 0; rx < cols; rx++) {
        const cx = x0 + rx;
        const bx = rx * PPS;
        const by = ry * PPS;
        if (cx < 0 || cy < 0 || cx >= w.w || cy >= w.h) {
          for (let py = 0; py < PPS; py++) {
            let p = ((by + py) * stride + bx) * 4;
            for (let px = 0; px < PPS; px++) { d[p + 3] = 0; p += 4; }
          }
          continue;
        }
        const i = cy * w.w + cx;
        const m = w.mat[i];
        if (m === EMPTY) {
          const rad = w.rad[i];
          const enclosed = !this.isOutdoor(cx, cy);
          // interior "back wall": rooms must not show the sky through them
          let ir = 0, ig = 0, ib = 0;
          if (enclosed) {
            const c = w.col[i];
            if (c) {
              ir = (c >> 16) & 255; ig = (c >> 8) & 255; ib = c & 255;
              // subtle vertical shading so flat wallpaper still has depth
              const belowSolid2 = cy < w.h - 1 && isSolid(w.mat[i + w.w]);
              if (belowSolid2) { ir *= 0.9; ig *= 0.9; ib *= 0.92; }
            } else {
              // default room wall: warm cream or cool plaster, with a darker
              // skirting row where the wall meets the floor slab
              const belowSolid = cy < w.h - 1 && isSolid(w.mat[i + w.w]);
              const shade = 0.97 + ((w.tint[i] % 5) * 0.012);
              const warm = (w.tint[i] & 1) === 0;
              ir = (warm ? 240 : 224) * shade;
              ig = (warm ? 229 : 235) * shade;
              ib = (warm ? 205 : 244) * shade;
              if (belowSolid) { ir *= 0.82; ig *= 0.82; ib *= 0.84; }
            }
          }
          // wall décor: small framed picture or a shelf on some room walls
          let decor = 0, decorCol = 0;
          if (enclosed && cy > 1 && isSolid(w.mat[i - w.w])) {
            const dh = ((cx * 13 + cy * 29 + (w.col[i] & 255)) % 11);
            if (dh === 0) { decor = 1; decorCol = 0xc9a24a; }
            else if (dh === 1) { decor = 1; decorCol = 0x8fb3c9; }
            else if (dh === 2) { decor = 1; decorCol = 0xc98a92; }
          }
          // ceiling lamp: warm bulb tucked under the slab, plus a soft halo
          let lampGlow = 0;
          if (enclosed && cy > 0 && isSolid(w.mat[i - w.w])) {
            const hsh = ((cx * 37 + cy * 61 + (w.tint[i] & 7)) % 5);
            if (hsh === 0) lampGlow = 1;
          }
          for (let py = 0; py < PPS; py++) {
            let p = ((by + py) * stride + bx) * 4;
            for (let px = 0; px < PPS; px++) {
              if (enclosed) {
                d[p] = ir; d[p + 1] = ig; d[p + 2] = ib; d[p + 3] = 255;
                if (decor && py === 1) {
                  if (px === 1) { d[p] = 92; d[p + 1] = 74; d[p + 2] = 52; }
                  else { d[p] = (decorCol >> 16) & 255; d[p + 1] = (decorCol >> 8) & 255; d[p + 2] = decorCol & 255; }
                } else if (lampGlow) {
                  if (py === 0 && px === 1) { d[p] = 255; d[p + 1] = 226; d[p + 2] = 140; }
                  else {
                    const k = py === 0 ? 0.55 : 0.26;
                    d[p] = d[p] + (255 - d[p]) * k;
                    d[p + 1] = d[p + 1] + (236 - d[p + 1]) * k;
                    d[p + 2] = d[p + 2] + (170 - d[p + 2]) * k;
                  }
                }
                if (rad > 0.06) {
                  d[p] = d[p] * 0.5 + 150 * 0.5;
                  d[p + 1] = d[p + 1] * 0.5 + 215 * 0.5;
                  d[p + 2] = d[p + 2] * 0.5 + 120 * 0.5;
                }
              } else if (rad > 0.06) {
                d[p] = 150; d[p + 1] = 215; d[p + 2] = 120;
                d[p + 3] = Math.min(90, rad * 55) | 0;
              } else d[p + 3] = 0;
              p += 4;
            }
          }
          continue;
        }

        // base colour
        let r, gg, b;
        const override = w.col[i];
        if (override) {
          r = (override >> 16) & 255; gg = (override >> 8) & 255; b = override & 255;
          const k = 0.94 + (w.tint[i] / 15) * 0.12;
          r *= k; gg *= k; b *= k;
        } else {
          const ti = (m * TINTS + w.tint[i]) | 0;
          r = TINT_R[ti]; gg = TINT_G[ti]; b = TINT_B[ti];
        }

        // material micro-patterns on a 3x3 footprint
        const pat = (w.tint[i] + cx * 3 + cy * 5) & 3;
        switch (m) {
          case BRICK:
            if (cy % 2 === 0 || (cx + ((cy >> 1) % 2) * 2) % 4 === 0) { r *= 0.87; gg *= 0.85; b *= 0.84; }
            break;
          case CONCRETE: case PANEL: case AERATED: case PLASTIC:
            if (pat === 0) { r *= 0.96; gg *= 0.96; b *= 0.96; }
            if (cx % 4 === 0) { r *= 0.93; gg *= 0.93; b *= 0.93; }
            break;
          case STEEL: case CASTIRON: case LEAD:
            if ((cx % 4 === 2) && (cy % 4 === 2)) { r *= 0.78; gg *= 0.8; b *= 0.84; }
            else if (cy % 5 === 0) { r *= 0.93; gg *= 0.94; b *= 0.95; }
            break;
          case WOOD:
            if (cy % 2 === 1) { r *= 0.9; gg *= 0.88; b *= 0.86; }
            if (pat === 1) { r *= 0.95; gg *= 0.93; b *= 0.9; }
            break;
          case SLATE:
            if ((cx + cy) % 3 === 0) { r *= 0.88; gg *= 0.89; b *= 0.9; }
            break;
          case GLASSBLOCK:
            if ((cx + cy) % 3 === 0) { r *= 0.9; gg *= 0.95; b *= 0.96; }
            break;
          case SOIL: {
            if (pat === 0) { r *= 0.86; gg *= 0.84; b *= 0.82; }
            else if (pat === 2) { r *= 1.06; gg *= 1.04; b *= 1.02; }
            // soft geological strata so the cross-section is not flat brown
            const band = ((cy + ((cx / 40) | 0)) % 13);
            if (band === 0) { r *= 0.82; gg *= 0.8; b *= 0.8; }
            else if (band === 6) { r *= 1.1; gg *= 1.05; b *= 0.97; }
            if (pat === 3 && ((cx * 5 + cy * 3) % 11 === 0)) { r *= 0.62; gg *= 0.6; b *= 0.58; }
            break;
          }
          case ROCK:
            if (pat === 1) { r *= 1.1; gg *= 1.1; b *= 1.1; }
            break;
          case SAND:
            if (pat === 3) { r *= 0.93; gg *= 0.91; b *= 0.88; }
            break;
          case GRASS:
            {
              const k = 0.86 + ((w.tint[i] % 5) * 0.07);
              r *= k; gg *= 1.06; b *= k * 0.9;
            }
            break;
          case WIRE:
            r = 224; gg = 186; b = 96;
            break;
          default: break;
        }

        if (m === WATER) {
          const s = Math.sin(t * 2 + cx * 0.4 + cy * 0.2) * 10;
          r += s; gg += s; b += s;
        }
        if (m === REACTOR) {
          const pulse = 0.7 + Math.sin(t * 3) * 0.3;
          r += 30 * pulse; gg += 70 * pulse; b += 30 * pulse;
        }

        // damage: darken, then thin cracks
        const dmg = w.dmg[i];
        if (dmg > 0) {
          const k = 1 - (dmg / 255) * 0.35;
          r *= k; gg *= k; b *= k;
        }
        const cracked = dmg > 120 && ((cx * 5 + cy * 3 + (dmg >> 3)) % 7 === 0);
        if (cracked) { r *= 0.5; gg *= 0.5; b *= 0.5; }

        if (w.fire[i]) {
          r = 250 * flick; gg = (120 + Math.sin(t * 12 + cx) * 40) * flick; b = 30;
        }

        // ambient light: sun from the upper left, sky bounce from above
        const above = cy > 0 ? w.mat[i - w.w] : EMPTY;
        const below = cy < w.h - 1 ? w.mat[i + w.w] : EMPTY;
        const left = cx > 0 ? w.mat[i - 1] : EMPTY;
        const right = cx < w.w - 1 ? w.mat[i + 1] : EMPTY;
        const openAbove = !isSolid(above);
        const openLeft = !isSolid(left);
        const openRight = !isSolid(right);
        const openBelow = !isSolid(below);

        // write the 3x3 block
        for (let py = 0; py < PPS; py++) {
          const edgeY = py === 0 ? (openAbove ? 1.18 : 1.02) : py === PPS - 1 ? (openBelow ? 0.82 : 0.96) : 1;
          let p = ((by + py) * stride + bx) * 4;
          for (let px = 0; px < PPS; px++) {
            let rr = r, g2 = gg, b2 = b;
            let k = edgeY;
            if (px === 0 && openLeft) k *= 1.14;
            if (px === PPS - 1 && openRight) k *= 0.9;

            // painted window panes (col == glass): white frame + sky sheen
            if (override === GLASSC) {
              rr = 150; g2 = 200; b2 = 226; k = 1;
              if (px === 0 || px === PPS - 1) { rr = 246; g2 = 248; b2 = 250; }
              if (py === 1 && px === 1) { rr = 214; g2 = 238; b2 = 250; }
              if (py === PPS - 1) { rr = 226; g2 = 231; b2 = 236; }
            }

            // glass: white frame + sheen
            if (m === GLASS) {
              const frame = (py === 0 || py === PPS - 1 || px === 0 || px === PPS - 1);
              if (frame) { rr = 244; g2 = 250; b2 = 254; k = 1; }
              else if (px === py) { rr = 236; g2 = 248; b2 = 255; }
            } else if (m === GLASSBLOCK) {
              if (px === 1 && py === 1) { rr = 250; g2 = 255; b2 = 255; }
            } else if (m === WIRE) {
              if (py === 1) { rr = 255; g2 = 232; b2 = 140; } else { rr = 176; g2 = 138; b2 = 52; }
            }

            d[p] = rr * k;
            d[p + 1] = g2 * k;
            d[p + 2] = b2 * k;
            d[p + 3] = 255;
            p += 4;
          }
        }

        // green structural outline along the silhouette only (schematic skin)
        const structural = m === CONCRETE || m === PANEL || m === STEEL || m === BRICK ||
          m === CASTIRON || m === AERATED || m === GLASSBLOCK || m === LEAD || m === WOOD ||
          m === PLASTIC || m === DEBRIS;
        const outAbove = openAbove && this.isOutdoor(cx, cy - 1);
        const outLeft = openLeft && this.isOutdoor(cx - 1, cy);
        const outRight = openRight && this.isOutdoor(cx + 1, cy);
        if (structural && (outAbove || outLeft || outRight)) {
          const gr = 96, gg2 = 208, gb = 150;
          const blend = (p, rr, g2, b2, a) => {
            d[p] = d[p] * (1 - a) + rr * a;
            d[p + 1] = d[p + 1] * (1 - a) + g2 * a;
            d[p + 2] = d[p + 2] * (1 - a) + b2 * a;
          };
          if (outAbove) {
            for (let px = 0; px < PPS; px++) blend((by * stride + bx + px) * 4, gr, gg2, gb, 0.72);
          }
          if (outLeft) {
            for (let py = 0; py < PPS; py++) blend(((by + py) * stride + bx) * 4, gr, gg2, gb, 0.55);
          }
          if (outRight) {
            for (let py = 0; py < PPS; py++) blend(((by + py) * stride + bx + PPS - 1) * 4, gr, gg2, gb, 0.45);
          }
        }
      }
    }
    this.bctx.putImageData(this.img, 0, 0);
  }

  // ------------------------------------------------------------------- draw
  draw() {
    const g = this.g;
    const ctx = g.ctx;
    const cam = g.cam;
    const W = g.canvas.width, H = g.canvas.height;
    this.time += 1 / 60;
    const shake = g.effects.shakeAmp;
    const ox = shake ? (Math.random() - 0.5) * shake * 2 : 0;
    const oy = shake ? (Math.random() - 0.5) * shake * 2 : 0;

    this.drawSky(ctx, cam);

    ctx.save();
    ctx.translate(ox, oy);
    this.bakeView();
    const cell = g.cell;
    const dx = (this.x0 * cell - cam.x) * cam.zoom;
    const dy = (this.y0 * cell - cam.y) * cam.zoom;
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(this.buf, dx, dy, this.cols * cell * cam.zoom, this.rows * cell * cam.zoom);

    this.drawPower(ctx, cam);
    g.effects.draw(ctx, cam);
    g.citizens.draw(ctx, cam);
    this.drawLights(ctx, cam);
    ctx.restore();

    g.effects.drawOverlay(ctx, cam);
    if (!g.lowQ) this.drawRadiation(ctx, cam);
    if (!g.lowQ) this.drawVignette(ctx);

    if (g.ui) g.ui.drawCursor(ctx, cam);
  }

  drawPower(ctx, cam) {
    const g = this.g;
    const w = g.world;
    if (!w.powerNodes || !w.powerNodes.length) return;
    ctx.save();
    ctx.lineWidth = Math.max(1, 1.4 * cam.zoom);
    for (const l of w.powerLinks) {
      const a = w.powerNodes[l.a], b = w.powerNodes[l.b];
      if (!a || !b) continue;
      const ax = (a.x * g.cell - cam.x) * cam.zoom;
      const ay = (a.y * g.cell - cam.y) * cam.zoom;
      const bx = (b.x * g.cell - cam.x) * cam.zoom;
      const by = (b.y * g.cell - cam.y) * cam.zoom;
      const sag = Math.min(70, Math.abs(bx - ax) * 0.09) + 6;
      ctx.strokeStyle = l.cut ? 'rgba(70,60,50,0.8)' : 'rgba(54,58,64,0.85)';
      ctx.beginPath();
      ctx.moveTo(ax, ay);
      ctx.quadraticCurveTo((ax + bx) / 2, (ay + by) / 2 + sag, bx, by);
      ctx.stroke();
    }
    ctx.restore();
  }

  drawLights(ctx, cam) {
    const w = this.g.world;
    if (w.blackout) return;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    for (const l of this.g.lights) {
      if (!l) continue;
      const x = (l.x * this.g.cell - cam.x) * cam.zoom;
      const y = (l.y * this.g.cell - cam.y) * cam.zoom;
      const r = l.r * cam.zoom * 1.6;
      if (x < -r || y < -r || x > ctx.canvas.width + r || y > ctx.canvas.height + r) continue;
      const grd = ctx.createRadialGradient(x, y, 0, x, y, r);
      grd.addColorStop(0, 'rgba(255,236,170,0.30)');
      grd.addColorStop(1, 'rgba(255,210,120,0)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.arc(x, y, r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  drawRadiation(ctx, cam) {
    const w = this.g.world;
    if (w.stats.fallout < 20) return;
    ctx.fillStyle = 'rgba(150,225,120,0.05)';
    ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
  }

  drawVignette(ctx) {
    const W = ctx.canvas.width, H = ctx.canvas.height;
    if (!this.vignette || this.vignette.width !== W || this.vignette.height !== H) {
      const cv = document.createElement('canvas');
      cv.width = W; cv.height = H;
      const c = cv.getContext('2d');
      const grd = c.createRadialGradient(W / 2, H / 2, H * 0.55, W / 2, H / 2, H * 1.05);
      grd.addColorStop(0, 'rgba(0,0,0,0)');
      grd.addColorStop(1, 'rgba(20,30,40,0.22)');
      c.fillStyle = grd;
      c.fillRect(0, 0, W, H);
      this.vignette = cv;
    }
    ctx.drawImage(this.vignette, 0, 0);
  }
}
