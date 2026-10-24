import { TINT_R, TINT_G, TINT_B, TINTS, EMPTY, WATER, REACTOR, GLASS, isSolid } from './materials.js';

export class Renderer {
  constructor(game) {
    this.g = game;
    this.buf = document.createElement('canvas');
    this.bctx = this.buf.getContext('2d');
    this.img = null;
    this.worldRef = null;
  }

  ensureBuffer() {
    const w = this.g.world;
    if (!this.img || this.worldRef !== w) {
      this.worldRef = w;
      this.buf.width = w.w;
      this.buf.height = w.h;
      this.img = new ImageData(w.w, w.h);
      const d = this.img.data;
      for (let i = 3; i < d.length; i += 4) d[i] = 255;
    }
  }

  bakeRegion(x0, y0, x1, y1) {
    const w = this.g.world;
    this.ensureBuffer();
    const d = this.img.data;
    const t = this.g.time;
    const flick = 0.82 + Math.sin(t * 18) * 0.1 + Math.random() * 0.08;
    x0 = Math.max(0, x0 | 0); y0 = Math.max(0, y0 | 0);
    x1 = Math.min(w.w, x1 | 0); y1 = Math.min(w.h, y1 | 0);
    for (let y = y0; y < y1; y++) {
      const row = y * w.w;
      for (let x = x0; x < x1; x++) {
        const i = row + x;
        const m = w.mat[i];
        const p = i * 4;
        if (m === EMPTY) {
          const rad = w.rad[i];
          if (rad > 0.05) {
            // contaminated air: a translucent green haze, never a solid wall
            d[p] = 120; d[p + 1] = 200; d[p + 2] = 90;
            d[p + 3] = Math.min(120, rad * 70) | 0;
          } else {
            d[p] = 0; d[p + 1] = 0; d[p + 2] = 0; d[p + 3] = 0;
          }
          continue;
        }
        d[p + 3] = 255;
        let ti = (m * TINTS + w.tint[i]) | 0;
        let r = TINT_R[ti], g = TINT_G[ti], b = TINT_B[ti];
        const dmg = w.dmg[i];
        if (dmg > 0) {
          const k = 1 - (dmg / 255) * 0.42;
          r *= k; g *= k; b *= k;
          if (dmg > 110) {
            const crack = (x * 7 + y * 13 + dmg) % 9;
            if (crack === 0) { r *= 0.55; g *= 0.55; b *= 0.55; }
          }
        }
        if (w.fire[i]) {
          const f = flick;
          r = Math.min(255, 250 * f);
          g = Math.min(255, (110 + Math.sin(t * 12 + x) * 40) * f);
          b = 20;
        }
        if (m === WATER) {
          const s = Math.sin(t * 2 + x * 0.4 + y * 0.2) * 8;
          r += s; g += s; b += s;
        }
        if (m === REACTOR) {
          const pulse = 0.7 + Math.sin(t * 3) * 0.3;
          r += 40 * pulse; g += 80 * pulse; b += 40 * pulse;
        }
        if (m === GLASS) {
          const gl = Math.sin((x + y) * 0.9) > 0.86 ? 22 : 0;
          r += gl; g += gl; b += gl;
        }
        // material surface patterns: mortar, panel seams, rivets, grain
        if (m === 4) {                       // brick
          if (y % 2 === 0 || (x + ((y >> 1) % 2) * 2) % 4 === 0) { r *= 0.82; g *= 0.82; b *= 0.82; }
        } else if (m === 3 || m === 9 || m === 10) { // concrete / panel / aerated
          if (x % 4 === 0 || y % 3 === 0) { r *= 0.93; g *= 0.93; b *= 0.93; }
        } else if (m === 5 || m === 14) {    // steel / cast iron
          if (x % 5 === 2 && y % 4 === 2) { r *= 0.78; g *= 0.78; b *= 0.78; }
          else if (y % 6 === 0) { r *= 0.9; g *= 0.9; b *= 0.9; }
        } else if (m === 7) {                // wood grain
          if (y % 2 === 1) { r *= 0.9; g *= 0.9; b *= 0.9; }
        } else if (m === 11) {               // slate shingles
          if ((x + y) % 3 === 0) { r *= 0.88; g *= 0.88; b *= 0.88; }
        } else if (m === 13) {               // lead sheet seams
          if (x % 3 === 0) { r *= 0.88; g *= 0.88; b *= 0.9; }
        }
        if (y > w.groundY + 2) { r *= 0.86; g *= 0.86; b *= 0.86; }
        d[p] = r; d[p + 1] = g; d[p + 2] = b;
      }
    }
    this.bctx.putImageData(this.img, 0, 0);
  }

  bakeVisible() {
    const g = this.g;
    const cam = g.cam;
    const c = g.cell;
    const pad = 6;
    const x0 = cam.x / c - pad;
    const y0 = cam.y / c - pad;
    const x1 = (cam.x + g.canvas.width / cam.zoom) / c + pad;
    const y1 = (cam.y + g.canvas.height / cam.zoom) / c + pad;
    this.bakeRegion(x0, y0, x1, y1);
  }

  draw() {
    const g = this.g;
    const ctx = g.ctx;
    const cam = g.cam;
    const W = g.canvas.width, H = g.canvas.height;
    const shake = g.effects.shakeAmp;
    const ox = shake ? (Math.random() - 0.5) * shake * 2 : 0;
    const oy = shake ? (Math.random() - 0.5) * shake * 2 : 0;

    // sky (gradient cached: building it every frame is surprisingly expensive)
    if (!this.sky || this.skyH !== H) {
      this.skyH = H;
      this.sky = ctx.createLinearGradient(0, 0, 0, H);
      this.sky.addColorStop(0, '#1d2a3d');
      this.sky.addColorStop(0.45, '#5d6f85');
      this.sky.addColorStop(0.75, '#9c8f7c');
      this.sky.addColorStop(1, '#c8b39a');
    }
    ctx.fillStyle = this.sky;
    ctx.fillRect(0, 0, W, H);

    ctx.save();
    ctx.translate(ox, oy);
    this.bakeVisible();
    ctx.imageSmoothingEnabled = false;
    {
      const W0 = g.world.w, H0 = g.world.h;
      const c = g.cell;
      const srcX = Math.max(0, Math.floor(cam.x / c) - 1);
      const srcY = Math.max(0, Math.floor(cam.y / c) - 1);
      const srcW = Math.min(W0 - srcX, Math.ceil(g.canvas.width / cam.zoom / c) + 3);
      const srcH = Math.min(H0 - srcY, Math.ceil(g.canvas.height / cam.zoom / c) + 3);
      const dx = (srcX * c - cam.x) * cam.zoom;
      const dy = (srcY * c - cam.y) * cam.zoom;
      if (srcW > 0 && srcH > 0) {
        ctx.drawImage(this.buf, srcX, srcY, srcW, srcH, dx, dy, srcW * c * cam.zoom, srcH * c * cam.zoom);
      }
    }

    // power lines and lamps
    this.drawPower(ctx, cam);

    // effects (behind citizens)
    g.effects.draw(ctx, cam);

    // people
    g.citizens.draw(ctx, cam);

    // light glows
    this.drawLights(ctx, cam);

    ctx.restore();

    // post effects
    g.effects.drawOverlay(ctx, cam);
    if (!g.lowQ) this.drawRadiation(ctx, cam);
    if (!g.lowQ) this.drawVignette(ctx);

    if (g.ui) g.ui.drawCursor(ctx, cam);
  }

  drawPower(ctx, cam) {
    const w = this.g.world;
    if (!w.powerNodes || !w.powerNodes.length) return;
    ctx.save();
    ctx.lineWidth = Math.max(1, 1.6 * cam.zoom * 8 * 0.12);
    for (const l of w.powerLinks) {
      const a = w.powerNodes[l.a], b = w.powerNodes[l.b];
      if (!a || !b) continue;
      const ax = (a.x * this.g.cell - cam.x) * cam.zoom;
      const ay = (a.y * this.g.cell - cam.y) * cam.zoom;
      const bx = (b.x * this.g.cell - cam.x) * cam.zoom;
      const by = (b.y * this.g.cell - cam.y) * cam.zoom;
      const sag = Math.min(60, Math.abs(bx - ax) * 0.08) + 6;
      ctx.strokeStyle = l.cut ? 'rgba(40,40,40,0.8)' : 'rgba(24,24,26,0.85)';
      ctx.beginPath();
      ctx.moveTo(ax, ay);
      ctx.quadraticCurveTo((ax + bx) / 2, (ay + by) / 2 + sag, bx, by);
      ctx.stroke();
      if (!l.cut && !w.blackout) {
        ctx.strokeStyle = 'rgba(255,225,130,0.20)';
        ctx.lineWidth = Math.max(0.6, 0.6 * cam.zoom * 10);
        ctx.stroke();
        ctx.lineWidth = Math.max(1, 1.6 * cam.zoom * 8 * 0.12);
      }
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
      const r = l.r * cam.zoom * 2.2;
      const grd = ctx.createRadialGradient(x, y, 0, x, y, r);
      grd.addColorStop(0, 'rgba(255,224,150,0.35)');
      grd.addColorStop(1, 'rgba(255,200,100,0)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.arc(x, y, r, 0, Math.PI * 2);
      ctx.fill();
    }
    // reactor glow
    for (const n of w.powerNodes) {
      if (!n.plant || !n.on) continue;
      const x = (n.x * this.g.cell - cam.x) * cam.zoom;
      const y = (n.y * this.g.cell - cam.y) * cam.zoom;
      const r = 160 * cam.zoom;
      const grd = ctx.createRadialGradient(x, y, 0, x, y, r);
      grd.addColorStop(0, 'rgba(140,255,180,0.28)');
      grd.addColorStop(1, 'rgba(80,255,140,0)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.arc(x, y, r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  drawRadiation(ctx, cam) {
    const g = this.g;
    const w = g.world;
    if (w.stats.fallout < 20) return;
    // faint green veil + drifting fallout speckles, clipped to the screen
    const grd = ctx.createLinearGradient(0, 0, 0, ctx.canvas.height);
    grd.addColorStop(0, 'rgba(120,255,140,0.012)');
    grd.addColorStop(1, 'rgba(110,220,120,0.05)');
    ctx.fillStyle = grd;
    ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
    const n = Math.min(140, Math.round(w.stats.fallout / 6));
    ctx.fillStyle = 'rgba(190,240,170,0.35)';
    for (let i = 0; i < n; i++) {
      const x = (Math.random() * ctx.canvas.width) | 0;
      const y = (Math.random() * ctx.canvas.height) | 0;
      ctx.fillRect(x, y, 1, 1);
    }
  }

  drawVignette(ctx) {
    const W = ctx.canvas.width, H = ctx.canvas.height;
    if (!this.vignette) {
      const cv = document.createElement('canvas');
      cv.width = W; cv.height = H;
      const c = cv.getContext('2d');
      const grd = c.createRadialGradient(W / 2, H / 2, H * 0.35, W / 2, H / 2, H * 0.95);
      grd.addColorStop(0, 'rgba(0,0,0,0)');
      grd.addColorStop(1, 'rgba(0,0,0,0.42)');
      c.fillStyle = grd;
      c.fillRect(0, 0, W, H);
      this.vignette = cv;
    }
    ctx.drawImage(this.vignette, 0, 0);
  }
}
