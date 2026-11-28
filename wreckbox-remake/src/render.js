import {
  TINT_R, TINT_G, TINT_B, TINTS, EMPTY, WATER, REACTOR, GLASS, GRASS, SOIL, ROCK,
  BRICK, CONCRETE, PANEL, AERATED, PLASTIC, WOOD, SLATE, STEEL, CASTIRON, LEAD,
  GLASSBLOCK, SAND, AMMO, WIRE, DEBRIS, PAL, isSolid,
} from './materials.js';

// Renderer: bright daylight, layered sky, and a per-cell bake with real detail
// (bricks, panels, window frames, cracks, green structural outlines).
const PPS = 4; // baked texels per world cell

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
    const flick = 0.88 + Math.sin(t * 16) * 0.07 + Math.random() * 0.05;

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
        const above = cy > 0 ? w.mat[i - w.w] : EMPTY;
        const below = cy < w.h - 1 ? w.mat[i + w.w] : EMPTY;
        const leftM = cx > 0 ? w.mat[i - 1] : EMPTY;
        const rightM = cx < w.w - 1 ? w.mat[i + 1] : EMPTY;
        const openAbove = !isSolid(above);
        const openBelow = !isSolid(below);
        const openLeft = !isSolid(leftM);
        const openRight = !isSolid(rightM);

        if (m === EMPTY) {
          const rad = w.rad[i];
          const enclosed = !this.isOutdoor(cx, cy);
          if (!enclosed && rad <= 0.06) {
            for (let py = 0; py < PPS; py++) {
              let p = ((by + py) * stride + bx) * 4;
              for (let px = 0; px < PPS; px++) { d[p + 3] = 0; p += 4; }
            }
            continue;
          }
          let ir = 0, ig = 0, ib = 0;
          if (enclosed) {
            const c = w.col[i];
            if (c) {
              ir = (c >> 16) & 255; ig = (c >> 8) & 255; ib = c & 255;
            } else {
              const warm = (w.tint[i] & 1) === 0;
              const shade = 0.97 + ((w.tint[i] % 5) * 0.012);
              ir = (warm ? 241 : 226) * shade;
              ig = (warm ? 231 : 236) * shade;
              ib = (warm ? 208 : 245) * shade;
            }
          }
          const ceilSolid = cy > 0 && isSolid(above);
          const floorSolid = cy < w.h - 1 && isSolid(below);
          for (let py = 0; py < PPS; py++) {
            const v = 1 + (floorSolid ? -(0.06 + py * 0.03) : 0) - (ceilSolid ? 0.05 * (1 - py / PPS) : 0);
            let p = ((by + py) * stride + bx) * 4;
            for (let px = 0; px < PPS; px++) {
              if (!enclosed) {
                const a = Math.min(0.42, rad * 0.5);
                d[p] = 150; d[p + 1] = 210; d[p + 2] = 130; d[p + 3] = (a * 255) | 0;
              } else {
                let rr = ir * v, g2 = ig * v, b2 = ib * v;
                if (rad > 0.06) {
                  rr = rr * 0.45 + 120 * 0.55;
                  g2 = g2 * 0.45 + 205 * 0.55;
                  b2 = b2 * 0.45 + 96 * 0.55;
                }
                d[p] = rr; d[p + 1] = g2; d[p + 2] = b2; d[p + 3] = 255;
              }
              p += 4;
            }
          }
          continue;
        }

        // --- solid cell: flat cartoon shading, no pixel dithering
        let r, gg, b;
        const override = w.col[i];
        if (override) {
          r = (override >> 16) & 255; gg = (override >> 8) & 255; b = override & 255;
        } else {
          const ti = (m * TINTS + w.tint[i]) | 0;
          r = TINT_R[ti]; gg = TINT_G[ti]; b = TINT_B[ti];
        }
        // low-frequency material variation (one value per cell, not per texel)
        const hsh = (cx * 73856093 ^ cy * 19349663 ^ (w.tint[i] * 83492791)) >>> 0;
        const varia = 0.96 + (hsh % 1000) / 1000 * 0.08;
        r *= varia; gg *= varia; b *= varia;

        // per-material accents, all smooth
        if (m === BRICK) {
          const course = cy % 2 === 0 ? 0.9 : 1.0;
          r *= course; gg *= course * 0.99; b *= course * 0.98;
        } else if (m === SOIL) {
          const band = (cy + ((cx / 50) | 0)) % 17;
          if (band === 0) { r *= 0.86; gg *= 0.84; b *= 0.84; }
          else if (band === 8) { r *= 1.08; gg *= 1.04; b *= 0.97; }
        } else if (m === GRASS) {
          const k = 0.9 + ((w.tint[i] % 5) * 0.05);
          r *= k; gg *= 1.05; b *= k * 0.9;
        } else if (m === WOOD) {
          if (openAbove) { r *= 1.05; gg *= 1.03; b *= 1.0; }
        } else if (m === WIRE) {
          r = 226; gg = 190; b = 104;
        } else if (m === STEEL || m === CASTIRON || m === LEAD) {
          gg *= 1.01; b *= 1.02;
        }

        if (m === WATER) {
          const s = Math.sin(t * 1.8 + cx * 0.35 + cy * 0.2) * 8;
          r += s; gg += s; b += s;
        }
        if (m === REACTOR) {
          const pulse = 0.7 + Math.sin(t * 3) * 0.3;
          r += 26 * pulse; gg += 64 * pulse; b += 26 * pulse;
        }

        // damage: overall darkening plus a soft crack pattern
        const dmg = w.dmg[i];
        if (dmg > 0) {
          const k = 1 - (dmg / 255) * 0.32;
          r *= k; gg *= k; b *= k;
        }
        if (w.fire[i]) {
          r = 250 * flick; gg = (126 + Math.sin(t * 12 + cx) * 36) * flick; b = 40;
        }

        const structural = m === CONCRETE || m === PANEL || m === STEEL || m === BRICK ||
          m === CASTIRON || m === AERATED || m === GLASSBLOCK || m === LEAD || m === WOOD ||
          m === PLASTIC || m === DEBRIS;
        const outAbove = openAbove && this.isOutdoor(cx, cy - 1);
        const outLeft = openLeft && this.isOutdoor(cx - 1, cy);
        const outRight = openRight && this.isOutdoor(cx + 1, cy);

        const isGlass = m === GLASS || override === GLASSC;
        for (let py = 0; py < PPS; py++) {
          const fy = py / (PPS - 1);
          // soft top light, darker bottom edge
          let k = 1.06 - fy * 0.14;
          if (openAbove) k += 0.1 * (1 - fy);
          if (openBelow) k -= 0.08 * fy;
          let p = ((by + py) * stride + bx) * 4;
          for (let px = 0; px < PPS; px++) {
            const fx = px / (PPS - 1);
            let rr = r * k, g2 = gg * k, b2 = b * k;
            if (openLeft && fx === 0) { rr *= 1.1; g2 *= 1.1; b2 *= 1.1; }
            if (openRight && fx === 1) { rr *= 0.9; g2 *= 0.9; b2 *= 0.9; }
            if (isGlass) {
              // window: sky reflection in the upper half, frame on the border
              const border = py === 0 || py === PPS - 1 || px === 0 || px === PPS - 1;
              if (border) { rr = 246; g2 = 249; b2 = 251; }
              else {
                const sheen = 1 - fy * 0.35;
                rr = 158 * sheen + 40; g2 = 205 * sheen + 30; b2 = 232 * sheen + 20;
              }
            }
            if (structural && (outAbove || outLeft || outRight)) {
              // green schematic skin along the silhouette
              const a = outAbove ? 0.5 * (1 - fy) : outLeft ? 0.34 * (1 - fx) : 0.3 * fx;
              if (a > 0.02) {
                rr = rr * (1 - a) + 88 * a;
                g2 = g2 * (1 - a) + 206 * a;
                b2 = b2 * (1 - a) + 146 * a;
              }
            }
            if (dmg > 140) {
              const crack = ((cx * 7 + cy * 13 + (hsh >> 5)) % 11 === 0) && px === 2;
              if (crack) { rr *= 0.65; g2 *= 0.65; b2 *= 0.65; }
            }
            d[p] = rr; d[p + 1] = g2; d[p + 2] = b2; d[p + 3] = 255;
            p += 4;
          }
        }
      }
    }
    this.bctx.putImageData(this.img, 0, 0);
  }

  // ----------------------------------------------------------- fall chunks
  // A detached building section is baked once into its own canvas and drawn as a
  // rigid, slowly tumbling body until it lands.
  chunkImage(c) {
    if (c.img) return c.img;
    const cv = document.createElement('canvas');
    cv.width = c.cw * PPS;
    cv.height = c.ch * PPS;
    const cx2 = cv.getContext('2d');
    const img = cx2.createImageData(cv.width, cv.height);
    const d = img.data;
    for (const cell of c.cells) {
      const m = cell.m;
      let r, gg, b;
      if (cell.c) { r = (cell.c >> 16) & 255; gg = (cell.c >> 8) & 255; b = cell.c & 255; }
      else { const ti = (m * TINTS + cell.t) | 0; r = TINT_R[ti]; gg = TINT_G[ti]; b = TINT_B[ti]; }
      for (let py = 0; py < PPS; py++) {
        const k = 1.05 - (py / (PPS - 1)) * 0.13;
        for (let px = 0; px < PPS; px++) {
          const p = (((cell.dy * PPS + py) * cv.width) + cell.dx * PPS + px) * 4;
          d[p] = r * k; d[p + 1] = gg * k; d[p + 2] = b * k; d[p + 3] = 255;
        }
      }
    }
    cx2.putImageData(img, 0, 0);
    c.img = cv;
    return cv;
  }

  drawChunks(ctx, cam) {
    const w = this.g.world;
    if (!w.chunks || !w.chunks.length) return;
    const cell = this.g.cell;
    const z = cam.zoom;
    for (const c of w.chunks) {
      const img = this.chunkImage(c);
      const wpx = c.cw * cell * z;
      const hpx = c.ch * cell * z;
      const x = (c.x * cell - cam.x) * z;
      const y = (c.y * cell - cam.y) * z;
      ctx.save();
      ctx.translate(x + wpx / 2, y + hpx / 2);
      ctx.rotate(c.rot);
      ctx.imageSmoothingEnabled = true;
      ctx.drawImage(img, -wpx / 2, -hpx / 2, wpx, hpx);
      ctx.restore();
    }
  }

  // --------------------------------------------------------------- props
  // Furniture and wall art are drawn as smooth vector shapes on top of the
  // cell grid, so rooms look illustrated rather than pixelated.
  drawProps(ctx, cam) {
    const g = this.g;
    const w = g.world;
    if (!w.props || !w.props.length) return;
    const cell = g.cell;
    const z = cam.zoom;
    const px = cell * z;
    const sx = (cx) => (cx * cell - cam.x) * z;
    const sy = (cy) => (cy * cell - cam.y) * z;
    const inView = (p) => {
      const X = sx(p.x), Y = sy(p.y);
      return X + p.w * px > -40 && X < g.canvas.width + 40 && Y + p.h * px > -40 && Y < g.canvas.height + 40;
    };
    const round = (X, Y, W, H, r) => {
      const rad = Math.max(0, Math.min(r, Math.min(W, H) / 2));
      ctx.beginPath();
      if (rad > 0.6) ctx.roundRect(X, Y, W, H, rad);
      else ctx.rect(X, Y, W, H);
    };
    for (const p of w.props) {
      if (!p.live && p.live !== undefined) continue;
      if (!inView(p)) continue;
      const X = sx(p.x), Y = sy(p.y);
      const W = p.w * px, H = p.h * px;
      ctx.save();
      switch (p.t) {
        case 'bed': {
          const c = p.c || 0xc9473f;
          // mattress
          ctx.fillStyle = '#f4efe4';
          round(X + W * 0.04, Y + H * 0.22, W * 0.92, H * 0.7, px * 0.22); ctx.fill();
          // blanket
          ctx.fillStyle = `#${c.toString(16).padStart(6, '0')}`;
          round(X + W * 0.04, Y + H * 0.5, W * 0.92, H * 0.42, px * 0.2); ctx.fill();
          // pillow
          ctx.fillStyle = '#ffffff';
          round(X + W * 0.12, Y + H * 0.28, W * 0.28, H * 0.2, px * 0.14); ctx.fill();
          // headboard
          ctx.fillStyle = '#a97a4a';
          round(X + W * 0.02, Y + H * 0.1, W * 0.12, H * 0.8, px * 0.14); ctx.fill();
          break;
        }
        case 'sofa': {
          const c = p.c || 0x4a7fc0;
          const hex = `#${c.toString(16).padStart(6, '0')}`;
          ctx.fillStyle = hex;
          round(X + W * 0.04, Y + H * 0.3, W * 0.92, H * 0.55, px * 0.25); ctx.fill();
          round(X + W * 0.04, Y + H * 0.18, W * 0.92, H * 0.3, px * 0.2); ctx.fill();
          ctx.fillStyle = 'rgba(255,255,255,0.22)';
          round(X + W * 0.12, Y + H * 0.24, W * 0.34, H * 0.16, px * 0.1); ctx.fill();
          ctx.fillStyle = 'rgba(0,0,0,0.12)';
          ctx.fillRect(X + W * 0.05, Y + H * 0.82, W * 0.9, H * 0.1);
          break;
        }
        case 'table': {
          ctx.fillStyle = '#b07f4e';
          round(X + W * 0.05, Y + H * 0.38, W * 0.9, H * 0.2, px * 0.1); ctx.fill();
          ctx.fillStyle = '#8a6039';
          ctx.fillRect(X + W * 0.16, Y + H * 0.56, W * 0.08, H * 0.4);
          ctx.fillRect(X + W * 0.76, Y + H * 0.56, W * 0.08, H * 0.4);
          break;
        }
        case 'counter': {
          ctx.fillStyle = '#c99a63';
          round(X, Y + H * 0.32, W, H * 0.3, px * 0.1); ctx.fill();
          ctx.fillStyle = '#e8e2d4';
          round(X + W * 0.06, Y + H * 0.22, W * 0.88, H * 0.12, px * 0.06); ctx.fill();
          ctx.fillStyle = '#8a6039';
          ctx.fillRect(X + 1, Y + H * 0.6, W - 2, H * 0.38);
          break;
        }
        case 'tv': {
          ctx.fillStyle = '#2c3138';
          round(X + W * 0.08, Y + H * 0.18, W * 0.84, H * 0.62, px * 0.12); ctx.fill();
          ctx.fillStyle = '#3d5a6c';
          round(X + W * 0.14, Y + H * 0.24, W * 0.72, H * 0.46, px * 0.08); ctx.fill();
          ctx.fillStyle = '#8a9099';
          ctx.fillRect(X + W * 0.42, Y + H * 0.8, W * 0.16, H * 0.14);
          break;
        }
        case 'monitor': {
          ctx.fillStyle = '#33383f';
          round(X + W * 0.12, Y + H * 0.05, W * 0.76, H * 0.5, px * 0.1); ctx.fill();
          ctx.fillStyle = '#7fd0e8';
          round(X + W * 0.18, Y + H * 0.1, W * 0.64, H * 0.4, px * 0.06); ctx.fill();
          break;
        }
        case 'fridge': {
          ctx.fillStyle = '#eef2f5';
          round(X + W * 0.08, Y, W * 0.84, H * 0.98, px * 0.14); ctx.fill();
          ctx.strokeStyle = 'rgba(150,160,170,0.7)';
          ctx.lineWidth = Math.max(1, px * 0.06);
          ctx.beginPath();
          ctx.moveTo(X + W * 0.1, Y + H * 0.42);
          ctx.lineTo(X + W * 0.9, Y + H * 0.42);
          ctx.stroke();
          ctx.fillStyle = '#b9c2cc';
          ctx.fillRect(X + W * 0.76, Y + H * 0.16, Math.max(1, px * 0.07), H * 0.18);
          break;
        }
        case 'washer': {
          ctx.fillStyle = '#f2f5f7';
          round(X + W * 0.06, Y + H * 0.06, W * 0.88, H * 0.92, px * 0.16); ctx.fill();
          ctx.fillStyle = '#9fb6c4';
          ctx.beginPath(); ctx.arc(X + W * 0.5, Y + H * 0.55, Math.min(W, H) * 0.26, 0, Math.PI * 2); ctx.fill();
          break;
        }
        case 'sink': {
          ctx.fillStyle = '#e7ecf0';
          round(X + W * 0.1, Y + H * 0.35, W * 0.8, H * 0.4, px * 0.14); ctx.fill();
          ctx.fillStyle = '#b7c3cc';
          ctx.fillRect(X + W * 0.46, Y + H * 0.1, Math.max(1, px * 0.08), H * 0.3);
          break;
        }
        case 'shelf': {
          ctx.fillStyle = '#8a6039';
          round(X, Y + H * 0.6, W, H * 0.16, px * 0.05); ctx.fill();
          const books = ['#c9473f', '#3f9c8b', '#4a7fc0', '#e0a63c', '#8a7fbf'];
          const n = Math.max(2, Math.round(W / px));
          for (let b = 0; b < n; b++) {
            ctx.fillStyle = books[(b + Math.round(p.x)) % books.length];
            const bw = (W / n) * 0.7;
            ctx.fillRect(X + (b + 0.15) * (W / n), Y + H * 0.22, bw, H * 0.38);
          }
          break;
        }
        case 'wardrobe': {
          ctx.fillStyle = '#9a6f42';
          round(X + W * 0.05, Y, W * 0.9, H * 0.98, px * 0.12); ctx.fill();
          ctx.strokeStyle = 'rgba(60,40,20,0.35)';
          ctx.lineWidth = Math.max(1, px * 0.05);
          ctx.beginPath();
          ctx.moveTo(X + W * 0.5, Y + H * 0.06);
          ctx.lineTo(X + W * 0.5, Y + H * 0.94);
          ctx.stroke();
          break;
        }
        case 'plant': {
          ctx.fillStyle = '#b9743f';
          round(X + W * 0.22, Y + H * 0.62, W * 0.56, H * 0.34, px * 0.08); ctx.fill();
          ctx.fillStyle = '#4f9e4a';
          ctx.beginPath();
          ctx.arc(X + W * 0.5, Y + H * 0.42, Math.min(W, H) * 0.3, 0, Math.PI * 2);
          ctx.arc(X + W * 0.32, Y + H * 0.55, Math.min(W, H) * 0.2, 0, Math.PI * 2);
          ctx.arc(X + W * 0.68, Y + H * 0.55, Math.min(W, H) * 0.2, 0, Math.PI * 2);
          ctx.fill();
          break;
        }
        case 'stool': {
          ctx.fillStyle = '#a97a4a';
          round(X + W * 0.15, Y + H * 0.4, W * 0.7, H * 0.22, px * 0.1); ctx.fill();
          ctx.fillStyle = '#8a6039';
          ctx.fillRect(X + W * 0.42, Y + H * 0.6, Math.max(1, px * 0.1), H * 0.36);
          break;
        }
        case 'rug': {
          ctx.fillStyle = `rgba(${(p.c >> 16) & 255},${(p.c >> 8) & 255},${p.c & 255},0.85)`;
          ctx.beginPath();
          ctx.ellipse(X + W / 2, Y + H * 0.62, W * 0.46, H * 0.3, 0, 0, Math.PI * 2);
          ctx.fill();
          break;
        }
        case 'picture': {
          ctx.fillStyle = '#8a6039';
          round(X + W * 0.12, Y + H * 0.2, W * 0.76, H * 0.6, px * 0.06); ctx.fill();
          ctx.fillStyle = `#${(p.c || 0x8fb3c9).toString(16).padStart(6, '0')}`;
          round(X + W * 0.2, Y + H * 0.28, W * 0.6, H * 0.44, px * 0.04); ctx.fill();
          break;
        }
        case 'car': {
          const c = p.c;
          const hex = `#${c.toString(16).padStart(6, '0')}`;
          // body
          ctx.fillStyle = hex;
          round(X + W * 0.02, Y + H * 0.42, W * 0.96, H * 0.38, px * 0.3); ctx.fill();
          // cabin
          round(X + W * 0.2, Y + H * 0.12, W * 0.58, H * 0.4, px * 0.3); ctx.fill();
          ctx.fillStyle = 'rgba(190,225,245,0.95)';
          round(X + W * 0.25, Y + H * 0.18, W * 0.22, H * 0.26, px * 0.12); ctx.fill();
          round(X + W * 0.51, Y + H * 0.18, W * 0.22, H * 0.26, px * 0.12); ctx.fill();
          // wheels
          ctx.fillStyle = '#2b2e33';
          ctx.beginPath();
          ctx.arc(X + W * 0.22, Y + H * 0.84, Math.min(W, H) * 0.16, 0, Math.PI * 2);
          ctx.arc(X + W * 0.78, Y + H * 0.84, Math.min(W, H) * 0.16, 0, Math.PI * 2);
          ctx.fill();
          ctx.fillStyle = '#8f959c';
          ctx.beginPath();
          ctx.arc(X + W * 0.22, Y + H * 0.84, Math.min(W, H) * 0.07, 0, Math.PI * 2);
          ctx.arc(X + W * 0.78, Y + H * 0.84, Math.min(W, H) * 0.07, 0, Math.PI * 2);
          ctx.fill();
          // headlight
          ctx.fillStyle = '#fff3c4';
          round(X + W * 0.9, Y + H * 0.5, W * 0.08, H * 0.12, px * 0.06); ctx.fill();
          break;
        }
        case 'tree': {
          const hex = `#${(p.c || 0x57a04a).toString(16).padStart(6, '0')}`;
          const trunkW = Math.max(2, W * 0.12);
          ctx.fillStyle = '#7d5734';
          round(X + W * 0.5 - trunkW / 2, Y + H * 0.4, trunkW, H * 0.6, trunkW * 0.4); ctx.fill();
          // foliage: three blobs, slight hue variation
          ctx.fillStyle = hex;
          ctx.beginPath();
          ctx.ellipse(X + W * 0.5, Y + H * 0.26, W * 0.5, H * 0.24, 0, 0, Math.PI * 2);
          ctx.ellipse(X + W * 0.28, Y + H * 0.36, W * 0.3, H * 0.16, 0, 0, Math.PI * 2);
          ctx.ellipse(X + W * 0.72, Y + H * 0.36, W * 0.3, H * 0.16, 0, 0, Math.PI * 2);
          ctx.fill();
          ctx.fillStyle = 'rgba(255,255,255,0.18)';
          ctx.beginPath();
          ctx.ellipse(X + W * 0.4, Y + H * 0.18, W * 0.22, H * 0.1, 0, 0, Math.PI * 2);
          ctx.fill();
          break;
        }
        case 'lampPost': {
          ctx.fillStyle = '#b9c0c7';
          ctx.fillRect(X + W * 0.42, Y, Math.max(1.5, W * 0.16), H);
          ctx.beginPath();
          ctx.arc(X + W * 0.5, Y + H * 0.04, Math.min(W, H) * 0.5, 0, Math.PI * 2);
          ctx.fillStyle = '#ffe9a8';
          ctx.fill();
          ctx.fillStyle = 'rgba(255,233,168,0.35)';
          ctx.beginPath(); ctx.arc(X + W * 0.5, Y + H * 0.04, Math.min(W, H) * 1.1, 0, Math.PI * 2); ctx.fill();
          break;
        }
        case 'roof': {
          const c = p.c;
          const hex = `#${c.toString(16).padStart(6, '0')}`;
          const base = Y + H;
          const inset = p.rise * px;
          ctx.beginPath();
          ctx.moveTo(X - px * 0.4, base);
          ctx.lineTo(X + inset, Y);
          ctx.lineTo(X + W - inset, Y);
          ctx.lineTo(X + W + px * 0.4, base);
          ctx.closePath();
          ctx.fillStyle = hex;
          ctx.fill();
          // tile courses
          ctx.save();
          ctx.clip();
          ctx.fillStyle = 'rgba(0,0,0,0.07)';
          const step = Math.max(4, H / Math.max(3, p.rise));
          for (let yy = Y; yy < base; yy += step) ctx.fillRect(X - px, yy, W + px * 2, Math.max(1, step * 0.22));
          ctx.fillStyle = 'rgba(255,255,255,0.12)';
          ctx.fillRect(X - px, Y, W + px * 2, Math.max(1, H * 0.12));
          ctx.restore();
          // eaves shadow and ridge cap
          ctx.fillStyle = 'rgba(30,20,10,0.25)';
          ctx.fillRect(X - px * 0.4, base - Math.max(1, px * 0.12), W + px * 0.8, Math.max(1, px * 0.12));
          ctx.fillStyle = 'rgba(255,255,255,0.22)';
          ctx.fillRect(X + inset, Y, Math.max(1, W - inset * 2), Math.max(1, px * 0.12));
          break;
        }
        case 'window': {
          ctx.fillStyle = '#eef4f8';
          round(X - px * 0.08, Y - px * 0.08, W + px * 0.16, H + px * 0.16, px * 0.2); ctx.fill();
          const grdW = ctx.createLinearGradient(0, Y, 0, Y + H);
          grdW.addColorStop(0, '#cfe9f6');
          grdW.addColorStop(1, '#7fb6d6');
          ctx.fillStyle = grdW;
          round(X + px * 0.05, Y + px * 0.05, W - px * 0.1, H - px * 0.1, px * 0.14); ctx.fill();
          ctx.fillStyle = 'rgba(255,255,255,0.4)';
          ctx.beginPath();
          ctx.moveTo(X + px * 0.1, Y + H * 0.85);
          ctx.lineTo(X + W * 0.55, Y + px * 0.06);
          ctx.lineTo(X + W * 0.8, Y + px * 0.06);
          ctx.lineTo(X + px * 0.35, Y + H * 0.85);
          ctx.closePath();
          ctx.fill();
          break;
        }
        case 'lamp': {
          ctx.strokeStyle = 'rgba(90,80,70,0.8)';
          ctx.lineWidth = Math.max(1, px * 0.07);
          ctx.beginPath();
          ctx.moveTo(X + W / 2, Y);
          ctx.lineTo(X + W / 2, Y + H * 0.4);
          ctx.stroke();
          ctx.fillStyle = '#ffe9a8';
          ctx.beginPath(); ctx.arc(X + W / 2, Y + H * 0.55, Math.min(W, H) * 0.3, 0, Math.PI * 2); ctx.fill();
          break;
        }
        default: break;
      }
      ctx.restore();
    }
  }

  // ------------------------------------------------------------ vector pass
  // The world is painted as smooth anti-aliased shapes (run-merged rectangles
  // with soft top light and a bottom shadow) instead of a pixel grid.
  drawWorld(ctx, cam) {
    const g = this.g;
    const w = g.world;
    const cell = g.cell;
    const z = cam.zoom;
    const W = g.canvas.width, H = g.canvas.height;
    const px = cell * z;
    const x0 = Math.floor(cam.x / cell) - 1;
    const y0 = Math.floor(cam.y / cell) - 1;
    const x1 = Math.ceil((cam.x + W / z) / cell) + 1;
    const y1 = Math.ceil((cam.y + H / z) / cell) + 1;
    const t = g.time;
    const GLASSC = PAL.glassCar;
    const flick = 0.9 + Math.sin(t * 16) * 0.06;
    const sx = (cx) => (cx * cell - cam.x) * z;
    const sy = (cy) => (cy * cell - cam.y) * z;
    ctx.lineCap = 'round';

    // Greedy rectangle decomposition: identical neighbouring cells are merged
    // into one smooth shape, so a wall reads as a wall instead of a cell grid.
    const cols = Math.max(0, x1 - x0), rowsN = Math.max(0, y1 - y0);
    const key = this.keyBuf && this.keyBuf.length >= cols * rowsN ? this.keyBuf : (this.keyBuf = new Int32Array(cols * rowsN));
    const used = this.usedBuf && this.usedBuf.length >= cols * rowsN ? this.usedBuf : (this.usedBuf = new Uint8Array(cols * rowsN));
    used.fill(0);
    for (let ry = 0; ry < rowsN; ry++) {
      const cy = y0 + ry;
      for (let rx = 0; rx < cols; rx++) {
        const cx = x0 + rx;
        let k = 0;
        if (cx >= 0 && cy >= 0 && cx < w.w && cy < w.h) {
          const i = cy * w.w + cx;
          const m = w.mat[i];
          if (m === EMPTY) {
            k = this.isOutdoor(cx, cy) ? 0
              : ((w.col[i] || ((w.tint[i] & 1) ? 0xf1e7d0 : 0xe2ecf5)) | 0x4000000) | 0x80000000;
          } else if (m === SOIL || m === ROCK || m === SAND || m === GRASS) {
            // natural ground: quantise the tint over 3x3 cell patches so the
            // cross-section reads as soft strata rather than a checkerboard
            const coarse = (w.tint[((cy >> 2) * 8 + (cx >> 2)) % w.n] & 3);
            k = (m | 0x1000000 | (coarse << 24)) | 0;
          } else {
            const stone = m === CONCRETE || m === PANEL || m === AERATED || m === BRICK ||
              m === SLATE || m === STEEL || m === WOOD || m === CASTIRON;
            const shade = stone
              ? (w.tint[((cy >> 1) * 4 + (cx >> 1)) % w.n] & 3)
              : (w.tint[i] & 7);
            k = (m | (w.col[i] ? 0x2000000 | (w.col[i] & 0xffffff) : 0x1000000 | (shade << 24))) | 0;
          }
        }
        key[ry * cols + rx] = k;
      }
    }

    // soft geological bands: thin darker/lighter strata across the soil section
    ctx.save();
    for (let cy = y0; cy < y1; cy += 1) {
      if (cy < 0 || cy >= w.h) continue;
      const band = (cy + ((Math.sin(cy * 0.7) * 3) | 0)) % 16;
      if (band !== 3 && band !== 4 && band !== 10) continue;
      let runStart = -1;
      for (let cx = x0; cx <= x1; cx++) {
        const inside = cx >= 0 && cx < w.w && w.mat[cy * w.w + cx] === SOIL;
        if (inside && runStart < 0) runStart = cx;
        if (!inside && runStart >= 0) {
          // wavy edge so the band does not look machine-drawn
          const yy = sy(cy) + Math.sin((runStart + cy) * 0.2) * px * 0.35;
          ctx.fillStyle = band === 9 ? 'rgba(255,235,200,0.07)' : 'rgba(40,24,10,0.13)';
          ctx.fillRect(sx(runStart), yy, (cx - runStart) * px, px * 0.5);
          runStart = -1;
        }
      }
    }
    ctx.restore();

    for (let ry = 0; ry < rowsN; ry++) {
      for (let rx = 0; rx < cols; rx++) {
        if (used[ry * cols + rx]) continue;
        const k = key[ry * cols + rx];
        if (!k) continue;
        // horizontal run
        const isGround = k > 0 && ((k & 0xff) === SOIL || (k & 0xff) === ROCK || (k & 0xff) === SAND || (k & 0xff) === GRASS);
        const isRubble = k > 0 && ((k & 0xff) === DEBRIS);
        const capW = isGround ? 999 : isRubble ? 22 : (k & 0x80000000) ? 11 : 34;
        let ww = 1;
        while (ww < capW && rx + ww < cols && key[ry * cols + rx + ww] === k && !used[ry * cols + rx + ww]) ww++;
        // vertical extension while the whole slice matches
        let hh = 1;
        outer: while (hh < ((isGround || isRubble) ? 999 : capW) && ry + hh < rowsN) {
          for (let x = rx; x < rx + ww; x++) {
            const idx = (ry + hh) * cols + x;
            if (key[idx] !== k || used[idx]) break outer;
          }
          hh++;
        }
        for (let y = ry; y < ry + hh; y++) for (let x = rx; x < rx + ww; x++) used[y * cols + x] = 1;

        const cx = x0 + rx, cy = y0 + ry;
        const X = sx(cx), Y = sy(cy);
        const WW = ww * px, HH = hh * px;
        this.paintRect(ctx, cam, cx, cy, ww, hh, k, X, Y, WW, HH, t, flick);
      }
    }
  }

  paintRect(ctx, cam, cx, cy, ww, hh, k, X, Y, WW, HH, t, flick) {
    const w = this.g.world;
    const px = this.g.cell * cam.zoom;
    const z = cam.zoom;
    const i = cy * w.w + cx;
    const indoor = k < 0;
    if (indoor) {
      const colour = k & 0x3ffffff;
      const rad = w.rad[i];
      let r = (colour >> 16) & 255, gg = (colour >> 8) & 255, b = colour & 255;
      if (rad > 0.06) { r = r * 0.45 + 120 * 0.55; gg = gg * 0.45 + 205 * 0.55; b = b * 0.45 + 96 * 0.55; }
      ctx.fillStyle = `rgb(${r | 0},${gg | 0},${b | 0})`;
      ctx.fillRect(X - 0.4, Y - 0.4, WW + 0.8, HH + 0.8);
      // two overlay bands instead of a per-rect gradient (much cheaper)
      ctx.fillStyle = 'rgba(0,0,0,0.12)';
      ctx.fillRect(X - 0.4, Y + HH * 0.62, WW + 0.8, HH * 0.38 + 0.4);
      return;
    }
    const m = (k & 0xff0000ff) >>> 0;
    const mat = this.g.world.mat[i];
    const override = w.col[i];
    let r, gg, b;
    if (override) { r = (override >> 16) & 255; gg = (override >> 8) & 255; b = override & 255; }
    else { const ti = (mat * TINTS + w.tint[i]) | 0; r = TINT_R[ti]; gg = TINT_G[ti]; b = TINT_B[ti]; }
    const hsh = ((cx * 73856093) ^ (cy * 19349663) ^ (w.tint[i] * 83492791)) >>> 0;
    const varia = 0.965 + (hsh % 1000) / 1000 * 0.07;
    r *= varia; gg *= varia; b *= varia;
    if (mat === BRICK) { const q = cy % 2 === 0 ? 0.93 : 1.0; r *= q; gg *= q * 0.99; b *= q * 0.98; }
    else if (mat === SOIL) {
      const band = (cy + ((cx / 50) | 0)) % 17;
      if (band === 0) { r *= 0.88; gg *= 0.86; b *= 0.86; }
      else if (band === 8) { r *= 1.06; gg *= 1.03; b *= 0.97; }
    } else if (mat === GRASS) { const q = 0.9 + ((w.tint[i] % 5) * 0.05); r *= q; gg *= 1.05; b *= q * 0.9; }
    else if (mat === WIRE) { r = 226; gg = 190; b = 104; }
    if (mat === WATER) { const q = Math.sin(t * 1.8 + cx * 0.35 + cy * 0.2) * 8; r += q; gg += q; b += q; }
    if (mat === REACTOR) { const q = 0.7 + Math.sin(t * 3) * 0.3; r += 26 * q; gg += 64 * q; b += 26 * q; }
    const dmg = w.dmg[i];
    if (dmg > 0) { const q = 1 - (dmg / 255) * 0.3; r *= q; gg *= q; b *= q; }
    if (w.fire[i]) { r = 252 * flick; gg = (128 + Math.sin(t * 12 + cx) * 34) * flick; b = 44; }

    const topOpen = cy === 0 || !isSolid(w.mat[i - w.w]);
    const botOpen = cy + hh >= w.h || !isSolid(w.mat[i + ww * 0 + hh * w.w]);
    const leftOpen = cx === 0 || !isSolid(w.mat[i - 1]);

    const isGlass = mat === GLASS || override === PAL.glassCar;
    if (isGlass) {
      const grd = ctx.createLinearGradient(0, Y, 0, Y + HH);
      grd.addColorStop(0, 'rgb(200,230,246)');
      grd.addColorStop(1, 'rgb(124,178,212)');
      ctx.fillStyle = grd;
      ctx.fillRect(X - 0.3, Y - 0.3, WW + 0.6, HH + 0.6);
      ctx.strokeStyle = 'rgba(252,254,255,0.85)';
      ctx.lineWidth = Math.max(1, px * 0.1);
      ctx.strokeRect(X + 0.5, Y + 0.5, WW - 1, HH - 1);
      return;
    }

    ctx.fillStyle = `rgb(${r | 0},${gg | 0},${b | 0})`;
    const big = WW > px * 3.2 || HH > px * 3.2;
    const radius = big ? 0 : Math.min(px * 0.16, Math.min(WW, HH) * 0.3);
    if (radius > 1.2) {
      ctx.beginPath();
      ctx.roundRect(X - 0.3, Y - 0.3, WW + 0.6, HH + 0.6, radius);
      ctx.fill();
    } else {
      ctx.fillRect(X - 0.3, Y - 0.3, WW + 0.6, HH + 0.6);
    }
    // ground texture: pebbles and soft pockets, hashed per cell so digging
    // reveals new shapes instead of a flat brown field
    if (mat === SOIL || mat === ROCK || mat === SAND) {
      const cellPx = px;
      const cx0 = Math.max(cx, Math.round(cam.x / this.g.cell));
      const cy0 = Math.max(cy, Math.round(cam.y / this.g.cell));
      const cx1 = Math.min(cx + ww, Math.round((cam.x + this.g.canvas.width / cam.zoom) / this.g.cell));
      const cy1 = Math.min(cy + hh, Math.round((cam.y + this.g.canvas.height / cam.zoom) / this.g.cell));
      for (let gy = cy0; gy < cy1; gy++) {
        for (let gx = cx0; gx < cx1; gx++) {
          const hsh2 = ((gx * 374761393) ^ (gy * 668265263)) >>> 0;
          const kind = hsh2 % 9;
          if (kind > 2) continue;
          const ox = ((hsh2 >> 5) % 100) / 100;
          const oy = ((hsh2 >> 11) % 100) / 100;
          const ex = (gx * cellPx - cam.x * z) + ox * cellPx;
          const ey = (gy * cellPx - cam.y * z) + oy * cellPx;
          const rr = cellPx * (kind === 2 ? 0.26 : 0.17);
          ctx.fillStyle = kind === 0 ? 'rgba(46,28,12,0.38)'
            : kind === 1 ? 'rgba(232,205,165,0.2)' : 'rgba(108,86,64,0.28)';
          ctx.beginPath();
          ctx.ellipse(ex, ey, rr, rr * 0.62, hsh2 % 3, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }

    // painted-on light: bright crest, darker base (small blocks only, so large
    // ground areas stay smooth instead of showing a grid of bands)
    if (!big) {
      ctx.fillStyle = `rgba(255,255,255,${topOpen ? 0.16 : 0.07})`;
      ctx.fillRect(X + 0.4, Y + 0.4, WW - 0.8, Math.max(0.8, HH * 0.14));
      ctx.fillStyle = `rgba(24,18,10,${botOpen ? 0.2 : 0.1})`;
      ctx.fillRect(X + 0.4, Y + HH * 0.86, WW - 0.8, Math.max(0.8, HH * 0.14));
    }

    if (dmg > 150) {
      ctx.fillStyle = 'rgba(40,34,26,0.3)';
      const cracks = Math.min(3, 1 + (dmg >> 6));
      for (let q = 0; q < cracks; q++) {
        const ox = ((hsh >> (q * 3)) % 9) / 9 * WW;
        ctx.fillRect(X + ox, Y, Math.max(1, px * 0.09), HH);
      }
    }

    const structural = mat === CONCRETE || mat === PANEL || mat === STEEL || mat === BRICK ||
      mat === CASTIRON || mat === AERATED || mat === GLASSBLOCK || mat === LEAD || mat === WOOD ||
      mat === PLASTIC;
    if (mat === SLATE) return;   // the vector roof prop replaces the stepped tiles
    if (structural) {
      const outAbove = topOpen && this.isOutdoor(cx, cy - 1);
      const outLeft = leftOpen && this.isOutdoor(cx - 1, cy);
      const outRight = cx + ww >= w.w || (!isSolid(w.mat[i + ww - 1 + 1]) && this.isOutdoor(cx + ww, cy));
      ctx.strokeStyle = 'rgba(78,198,140,0.85)';
      ctx.lineWidth = Math.max(1.4, px * 0.12);
      ctx.beginPath();
      if (outAbove) { ctx.moveTo(X, Y + 0.7); ctx.lineTo(X + WW, Y + 0.7); }
      if (outLeft) { ctx.moveTo(X + 0.7, Y); ctx.lineTo(X + 0.7, Y + HH); }
      if (outRight) { ctx.moveTo(X + WW - 0.7, Y); ctx.lineTo(X + WW - 0.7, Y + HH); }
      ctx.stroke();
    }
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
    if (g.frame - this.outdoorFrame > 6) {
      this.outdoorFrame = g.frame;
      this.computeOutdoor();
    }
    // The static world is cached in an offscreen layer and only repainted when
    // the camera moves or every few frames while things are changing.
    const W2 = g.canvas.width, H2 = g.canvas.height;
    if (!this.layer || this.layer.width !== W2 || this.layer.height !== H2) {
      this.layer = document.createElement('canvas');
      this.layer.width = W2;
      this.layer.height = H2;
      this.lctx = this.layer.getContext('2d');
      this.camKey = null;
    }
    const camKey = `${cam.x.toFixed(1)}|${cam.y.toFixed(1)}|${cam.zoom.toFixed(4)}|${W2}x${H2}`;
    if (camKey !== this.camKey || g.frame - this.layerFrame >= 4) {
      this.camKey = camKey;
      this.layerFrame = g.frame;
      this.lctx.clearRect(0, 0, W2, H2);
      this.drawWorld(this.lctx, cam);
      this.drawProps(this.lctx, cam);
      this.drawChunks(this.lctx, cam);
    }
    ctx.drawImage(this.layer, 0, 0);

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
