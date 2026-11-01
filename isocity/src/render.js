// Pixel renderer: draw everything into a small buffer at 1:1 art pixels, then
// blow it up by a whole number with smoothing off. That is what keeps the
// picture crisp and "pixel" at any window size.

import { TW, TH, HW, HH, iso, corners, fill, pxLine, diamond } from './iso.js?v=2026-11-01-pixel-7';
import { T } from './world.js?v=2026-11-01-pixel-7';
import { drawGroundTile, drawBuilding, drawCitizen, drawTruck, drawCar, drawProp, PAL } from './art.js?v=2026-11-01-pixel-7';
import { POLICIES, SERVICES } from './mechanics.js?v=2026-11-01-pixel-7';

const FONT = "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif";
const HINT_SIZE = 13;

const TOOLS = [
  { id: 'bulldoze', key: '1', label: 'снос', row: 0 },
  { id: 'road', key: '2', label: 'дорога', row: 0 },
  { id: 'zoneR', key: '3', label: 'жильё', row: 0 },
  { id: 'zoneC', key: '4', label: 'торговля', row: 0 },
  { id: 'zoneI', key: '5', label: 'промзона', row: 0 },
  { id: 'park', key: '6', label: 'парк', row: 0 },
  // public services and city works, unlocked by population milestones
  // city services: no hotkeys, they are chosen with the mouse
  { id: 'water', key: '', label: 'вода', row: 1, service: true },
  { id: 'power', key: '', label: 'энергия', row: 1, service: true },
  { id: 'school', key: '', label: 'школа', row: 1, service: true },
  { id: 'busstop', key: '', label: 'автобус', row: 1, service: true },
  { id: 'clinic', key: '', label: 'клиника', row: 1, service: true },
  { id: 'landfill', key: '', label: 'свалка', row: 1, service: true },
  { id: 'warehouse', key: '', label: 'склад', row: 1, service: true },
  { id: 'police', key: '', label: 'полиция', row: 1, service: true },
  { id: 'fire', key: '', label: 'пожарные', row: 1, service: true },
  { id: 'landmark', key: '', label: 'туризм', row: 1, service: true },
];


/** Small toolbar glyphs for the ten city services (12 px tall). */
const SERVICE_ICONS = {
  water: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#5aa9e6';
    ctx.beginPath();
    ctx.moveTo(cx, cy - 8);
    ctx.quadraticCurveTo(cx + 7, cy + 1, cx, cy + 6);
    ctx.quadraticCurveTo(cx - 7, cy + 1, cx, cy - 8);
    ctx.fill();
    ctx.fillStyle = 'rgba(255,255,255,0.55)';
    ctx.fillRect(cx - 3, cy, 2, 3);
  },
  power: (ctx, cx, cy, gold) => {
    ctx.strokeStyle = gold;
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(cx - 6, cy + 7);
    ctx.lineTo(cx - 2, cy - 8);
    ctx.moveTo(cx + 6, cy + 7);
    ctx.lineTo(cx + 2, cy - 8);
    ctx.moveTo(cx - 4, cy + 1);
    ctx.lineTo(cx + 4, cy + 1);
    ctx.moveTo(cx - 5, cy - 4);
    ctx.lineTo(cx + 5, cy - 4);
    ctx.stroke();
    ctx.fillStyle = '#f2d492';
    ctx.beginPath();
    ctx.moveTo(cx + 1, cy - 3);
    ctx.lineTo(cx - 3, cy + 4);
    ctx.lineTo(cx + 1, cy + 4);
    ctx.lineTo(cx - 1, cy + 9);
    ctx.lineTo(cx + 5, cy + 1);
    ctx.lineTo(cx + 1, cy + 1);
    ctx.closePath();
    ctx.fill();
  },
  school: (ctx, cx, cy, gold, active) => {
    ctx.fillStyle = '#e8dcc0';
    ctx.fillRect(cx - 9, cy - 2, 18, 9);
    ctx.fillStyle = active ? '#c25b4b' : '#a94f42';
    ctx.beginPath();
    ctx.moveTo(cx - 11, cy - 2);
    ctx.lineTo(cx, cy - 9);
    ctx.lineTo(cx + 11, cy - 2);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = '#5c6a78';
    ctx.fillRect(cx - 2, cy + 2, 4, 5);
  },
  busstop: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#8e8577';
    ctx.fillRect(cx - 1, cy - 4, 2, 11);
    ctx.fillStyle = '#5b93d8';
    ctx.fillRect(cx - 8, cy - 9, 16, 8);
    ctx.fillStyle = '#eaf1fb';
    ctx.beginPath();
    ctx.arc(cx - 4, cy - 5, 1.6, 0, Math.PI * 2);
    ctx.arc(cx + 4, cy - 5, 1.6, 0, Math.PI * 2);
    ctx.fill();
  },
  clinic: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#eef2f3';
    ctx.fillRect(cx - 8, cy - 8, 16, 16);
    ctx.fillStyle = '#d9534f';
    ctx.fillRect(cx - 2, cy - 6, 4, 12);
    ctx.fillRect(cx - 6, cy - 2, 12, 4);
  },
  landfill: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#7d8a6a';
    ctx.fillRect(cx - 8, cy - 2, 16, 7);
    ctx.fillStyle = '#5d6a4e';
    ctx.fillRect(cx - 9, cy - 4, 18, 3);
    ctx.fillStyle = 'rgba(255,255,255,0.22)';
    ctx.fillRect(cx - 4, cy + 1, 3, 4);
    ctx.fillRect(cx + 1, cy + 1, 3, 4);
  },
  warehouse: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#8e9aa6';
    ctx.fillRect(cx - 10, cy - 4, 20, 9);
    ctx.fillStyle = '#b8c2cc';
    ctx.beginPath();
    ctx.moveTo(cx - 11, cy - 4);
    ctx.lineTo(cx - 4, cy - 9);
    ctx.lineTo(cx + 4, cy - 9);
    ctx.lineTo(cx + 11, cy - 4);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = '#5f6a76';
    ctx.fillRect(cx - 3, cy, 2, 5);
    ctx.fillRect(cx + 1, cy, 2, 5);
  },
  police: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#4a6fa5';
    ctx.beginPath();
    ctx.moveTo(cx, cy - 9);
    ctx.lineTo(cx + 8, cy - 5);
    ctx.lineTo(cx + 6, cy + 3);
    ctx.lineTo(cx, cy + 8);
    ctx.lineTo(cx - 6, cy + 3);
    ctx.lineTo(cx - 8, cy - 5);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = '#f2d492';
    ctx.fillRect(cx - 1, cy - 5, 2, 8);
  },
  fire: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#e07a3c';
    ctx.beginPath();
    ctx.moveTo(cx, cy - 9);
    ctx.quadraticCurveTo(cx + 8, cy - 1, cx + 3, cy + 7);
    ctx.quadraticCurveTo(cx, cy + 9, cx - 3, cy + 6);
    ctx.quadraticCurveTo(cx - 7, cy, cx, cy - 9);
    ctx.fill();
    ctx.fillStyle = '#f6d76a';
    ctx.beginPath();
    ctx.arc(cx, cy + 3, 3, 0, Math.PI * 2);
    ctx.fill();
  },
  landmark: (ctx, cx, cy, gold) => {
    ctx.fillStyle = '#d8cfae';
    ctx.fillRect(cx - 9, cy + 4, 18, 4);
    ctx.fillRect(cx - 6, cy - 3, 3, 8);
    ctx.fillRect(cx - 1, cy - 3, 3, 8);
    ctx.fillRect(cx + 4, cy - 3, 3, 8);
    ctx.fillStyle = '#c9a24a';
    ctx.beginPath();
    ctx.moveTo(cx - 7, cy - 3);
    ctx.lineTo(cx, cy - 9);
    ctx.lineTo(cx + 7, cy - 3);
    ctx.closePath();
    ctx.fill();
  },
};

export class Renderer {
  constructor(canvas, sim) {
    this.canvas = canvas;
    this.sim = sim;
    this.ctx = canvas.getContext('2d');
    this.zoom = 1;
    this.pxScale = 2;
    this.buf = document.createElement('canvas');
    this.bctx = this.buf.getContext('2d');
    this.ground = document.createElement('canvas');
    this.ground.width = sim.world.n * TW;
    this.ground.height = sim.world.n * TH;
    this.gctx = this.ground.getContext('2d');
    this.groundVersion = -1;
    this.origin = { x: (sim.world.n * TW) / 2, y: 0 };
    this.cam = { x: 0, y: 0 };
    this.tool = 'road';
    this.hover = null;
    this.time = 0;
    this.smoke = [];
    this.toast = null;
    this.showHud = true;
    this.summaryOpen = false;
    this.mechanicsOpen = false;
    this.build = '';
    this.dpr = 1;
    this.cssW = 0;
    this.cssH = 0;
    this.resize();
    this.centerOn(17.5, 18.5);
  }

  // ------------------------------------------------------------------ setup
  resize() {
    // Two layers live on one canvas:
    //   * the city, drawn into a small buffer and blown up by whole pixels,
    //   * the HUD, drawn straight at screen resolution in CSS pixels.
    // That is why the interface keeps its size and stays sharp at any zoom.
    const dpr = Math.min(2, Math.max(1, window.devicePixelRatio || 1));
    const cssW = Math.max(320, this.canvas.clientWidth || window.innerWidth);
    const cssH = Math.max(240, this.canvas.clientHeight || window.innerHeight);
    this.dpr = dpr;
    this.cssW = cssW;
    this.cssH = cssH;
    this.pxScale = 2 * this.zoom;
    const bufW = Math.max(1, Math.floor(cssW / this.pxScale));
    const bufH = Math.max(1, Math.floor(cssH / this.pxScale));
    this.bufW = bufW;
    this.bufH = bufH;
    this.buf.width = bufW;
    this.buf.height = bufH;
    this.canvas.width = Math.round(bufW * this.pxScale * dpr);
    this.canvas.height = Math.round(bufH * this.pxScale * dpr);
    // keep the CSS box exactly equal to the backing store: the browser has
    // nothing to rescale, so every art pixel lands on whole screen pixels
    if (this.canvas.style) {
      this.canvas.style.width = `${this.canvas.width / dpr}px`;
      this.canvas.style.height = `${this.canvas.height / dpr}px`;
    }
    this.bctx.imageSmoothingEnabled = false;
    this.ctx.imageSmoothingEnabled = false;
    this.clampCamera();
  }

  worldBounds() {
    const w = this.sim.world;
    return {
      minX: -this.origin.x - 40,
      maxX: this.origin.x + 40,
      minY: -60,
      maxY: w.n * TH + 40,
    };
  }

  centerOn(gx, gy) {
    const p = iso(gx, gy);
    this.cam.x = p.x - this.bufW / 2;
    this.cam.y = p.y - this.bufH / 2;
    this.clampCamera();
  }

  clampCamera() {
    const b = this.worldBounds();
    const viewW = this.bufW;
    const viewH = this.bufH;
    if (b.maxX - b.minX > viewW) this.cam.x = Math.max(b.minX, Math.min(b.maxX - viewW, this.cam.x));
    else this.cam.x = (b.minX + b.maxX) / 2 - viewW / 2;
    if (b.maxY - b.minY > viewH) this.cam.y = Math.max(b.minY, Math.min(b.maxY - viewH, this.cam.y));
    else this.cam.y = (b.minY + b.maxY) / 2 - viewH / 2;
  }

  panBy(dx, dy) {
    this.cam.x += dx;
    this.cam.y += dy;
    this.clampCamera();
  }

  setZoom(z, anchor) {
    const next = Math.max(1, Math.min(3, z));
    if (next === this.zoom) return;
    const before = anchor ? this.screenToWorld(anchor.cx, anchor.cy) : null;
    this.zoom = next;
    this.resize();
    if (before) {
      const after = this.screenToWorld(anchor.cx, anchor.cy);
      this.cam.x += before.wx - after.wx;
      this.cam.y += before.wy - after.wy;
      this.clampCamera();
    }
  }

  // ------------------------------------------------------------- coordinates
  screenToWorld(cssX, cssY) {
    const bx = cssX / this.pxScale;
    const by = cssY / this.pxScale;
    return { wx: bx + this.cam.x, wy: by + this.cam.y };
  }

  worldToScreen(wx, wy) {
    return { x: (wx - this.cam.x) * this.pxScale, y: (wy - this.cam.y) * this.pxScale };
  }

  tileAt(cssX, cssY) {
    const { wx, wy } = this.screenToWorld(cssX, cssY);
    const x = wy / TH + wx / TW;
    const y = wy / TH - wx / TW;
    const tx = Math.floor(x);
    const ty = Math.floor(y);
    if (!this.sim.world.inBounds(tx, ty)) return null;
    return { x: tx, y: ty };
  }

  // ------------------------------------------------------------------ ground
  rebuildGround() {
    const w = this.sim.world;
    const g = this.gctx;
    g.clearRect(0, 0, this.ground.width, this.ground.height);
    g.imageSmoothingEnabled = false;
    for (let y = 0; y < w.n; y++) {
      for (let x = 0; x < w.n; x++) {
        drawGroundTile(g, w, x, y, this.origin.x, this.origin.y);
      }
    }
    // a darker rim so the map reads as an island
    g.strokeStyle = 'rgba(20,30,24,0.5)';
    const c = corners(0, 0, w.n, w.n);
    for (let i = 0; i < 4; i++) {
      const a = c[i];
      const b = c[(i + 1) % 4];
      pxLine(g, a.x + this.origin.x, a.y, b.x + this.origin.x, b.y, 'rgba(30,44,32,0.6)');
    }
    this.groundVersion = w.version;
  }

  // ------------------------------------------------------------------- frame
  update(dt) {
    this.time += dt;
    this.sim.nightAmount = this.night();
    // chimney smoke from factories and houses with chimneys
    for (const b of this.sim.world.buildings) {
      if (b.state !== 'done') continue;
      const hasChimney = b.kind === 'factory' || (b.kind === 'house' && b.variant % 2 === 0);
      if (!hasChimney) continue;
      if (this.sim.rng() < dt * 1.4) {
        this.smoke.push({ x: b.x + b.w * 0.75, y: b.y + b.d * 0.28, z: 22, life: 0, max: 1.6 + this.sim.rng() * 0.8 });
      }
    }
    for (let i = this.smoke.length - 1; i >= 0; i--) {
      const s = this.smoke[i];
      s.life += dt;
      s.z += dt * 6;
      if (s.life > s.max) this.smoke.splice(i, 1);
    }
    if (this.toast) {
      this.toast.life -= dt;
      if (this.toast.life <= 0) this.toast = null;
    }
  }

  night() {
    const h = this.sim.minute / 60;
    if (h >= 8 && h <= 18) return 0;
    if (h > 18 && h < 21) return (h - 18) / 3;
    if (h >= 21 || h < 4) return 1;
    if (h >= 4 && h < 6) return 1 - (h - 4) / 2;
    return 0;
  }

  draw() {
    const ctx = this.bctx;
    if (this.sim.world.version !== this.groundVersion) this.rebuildGround();

    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = this.night() > 0.4 ? '#111a26' : '#1d2b33';
    ctx.fillRect(0, 0, this.bufW, this.bufH);

    // ground cache
    ctx.drawImage(this.ground, Math.round(-this.origin.x - this.cam.x), Math.round(-this.cam.y));

    ctx.save();
    ctx.translate(Math.round(-this.cam.x), Math.round(-this.cam.y));

    this.drawHover(ctx);
    this.drawDepth(ctx);
    this.drawSmoke(ctx);
    this.drawNightTint(ctx);

    ctx.restore();

    this.present();
    if (this.showHud) this.drawHud(this.ctx);
  }

  drawDepth(ctx) {
    const sim = this.sim;
    const items = [];
    for (const b of sim.world.buildings) {
      if (b.kind === 'park') continue;
      items.push({
        k: (b.x + b.w) + (b.y + b.d),
        draw: () => {
          drawBuilding(ctx, b, sim, this.night());
          if (b.fire) this.drawFire(ctx, b);
        },
      });
    }
    if (sim.waste > 12) this.drawTrash(ctx);
    for (const p of sim.world.props) {
      items.push({ k: p.x + p.y + 1, draw: () => drawProp(ctx, p, sim) });
    }
    for (const c of sim.citizens) {
      if (!c.visible) continue;
      if (c.state === 'home' || c.state === 'work' || c.state === 'builder' || c.state === 'sleep') continue;
      items.push({ k: c.x + c.y + 1, draw: () => drawCitizen(ctx, c) });
    }
    for (const t of sim.trucks) {
      items.push({ k: t.x + t.y + 1, draw: () => drawTruck(ctx, t, this.night()) });
    }
    for (const car of sim.cars || []) {
      items.push({ k: car.x + car.y + 1, draw: () => drawCar(ctx, car, this.night()) });
    }
    items.sort((a, b) => a.k - b.k);
    for (const it of items) it.draw();
  }

  /** Flames on a burning building; the fire brigade arrives with the fire. */
  drawFire(ctx, b) {
    const t = this.time * 6;
    for (let i = 0; i < 5; i++) {
      const p = iso(b.x + 0.25 + (i % 3) * 0.22, b.y + 0.3 + Math.floor(i / 3) * 0.25, 6 + ((t + i * 1.7) % 6));
      const h = 3 + ((Math.sin(t + i) + 1) * 1.5);
      ctx.fillStyle = i % 2 ? '#e8a13a' : '#e0573d';
      ctx.fillRect(p.x - 2, p.y - h, 4, h + 2);
      ctx.fillStyle = '#f7dd8a';
      ctx.fillRect(p.x - 1, p.y - h + 1, 2, h);
    }
  }

  /** Litter on the streets when the dumps are full. */
  drawTrash(ctx) {
    for (const b of this.sim.world.buildings) {
      if (b.state !== 'done' || b.kind === 'park') continue;
      const h = (b.id * 7919) % 5;
      if (h > 2) continue;
      const p = iso(b.door.x + 0.2, b.door.y + 0.3, 0);
      ctx.fillStyle = '#7a7158';
      ctx.fillRect(p.x - 2, p.y - 2, 3, 2);
      ctx.fillRect(p.x + 2, p.y - 1, 2, 1);
    }
  }

  drawSmoke(ctx) {
    for (const s of this.smoke) {
      const p = iso(s.x, s.y, s.z);
      const t = s.life / s.max;
      ctx.globalAlpha = 0.5 * (1 - t);
      ctx.fillStyle = '#dfe6ea';
      const r = 1 + Math.round(t * 2);
      ctx.fillRect(p.x - r, p.y - r, r * 2, r * 2);
    }
    ctx.globalAlpha = 1;
  }

  drawHover(ctx) {
    if (!this.hover) return;
    const { x, y } = this.hover;
    const tool = this.tool;
    const valid = this.canPaint(x, y);
    const c = corners(x, y);
    const color = valid ? toolColor(tool) : '#e0574d';
    for (let i = 0; i < 4; i++) {
      const a = c[i];
      const b = c[(i + 1) % 4];
      pxLine(ctx, a.x, a.y, b.x, b.y, color);
    }
    if (tool === 'zoneR' || tool === 'zoneC' || tool === 'zoneI') {
      fill(ctx, c, tool === 'zoneR' ? PAL.zoneR : tool === 'zoneC' ? PAL.zoneC : PAL.zoneI);
    } else if (tool === 'road') {
      fill(ctx, c, 'rgba(80,86,96,0.45)');
    }
  }

  drawNightTint(ctx) {
    const n = this.night();
    if (n <= 0.02) return;
    ctx.globalAlpha = 0.5 * n;
    ctx.fillStyle = '#131f38';
    ctx.fillRect(Math.round(-this.cam.x) - 40, Math.round(-this.cam.y) - 40, this.bufW + 80, this.bufH + 80);
    ctx.globalAlpha = 1;
  }

  present() {
    const ctx = this.ctx;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.imageSmoothingEnabled = false;
    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    // fill the window even when the buffer does not divide evenly by the scale
    ctx.drawImage(this.buf, 0, 0, this.bufW, this.bufH, 0, 0, this.canvas.width, this.canvas.height);
  }

  canPaint(x, y) {
    const w = this.sim.world;
    const t = this.tool;
    if (t === 'bulldoze') return w.get(x, y) !== T.GRASS;
    if (t === 'road') return w.get(x, y) !== T.ROAD;
    if (t === 'park') return w.get(x, y) !== T.PARK && w.occ[y * w.n + x] < 0;
    const type = t === 'zoneR' ? T.ZONE_R : t === 'zoneC' ? T.ZONE_C : T.ZONE_I;
    return w.get(x, y) !== T.ROAD && w.get(x, y) !== type;
  }

  // -------------------------------------------------------------------- HUD
  // The interface is drawn on the screen canvas in CSS pixels, on top of the
  // pixel city. It therefore keeps its size at every zoom level and its text
  // stays smooth instead of being blown up with the map.
  layout() {
    const W = this.cssW;
    const H = this.cssH;
    const pad = 14;

    const speeds = [];
    const bw = 40;
    const bh = 30;
    const gap = 6;
    const values = [0, 1, 2, 4];
    const speedsW = values.length * bw + (values.length - 1) * gap;
    let sx = W - pad - speedsW;
    for (let i = 0; i < values.length; i++) {
      speeds.push({ x: sx, y: pad, w: bw, h: bh, v: values[i] });
      sx += bw + gap;
    }
    const summaryButton = { x: W - pad - speedsW - 8 - 96, y: pad, w: 96, h: bh };
    const summaryW = Math.min(370, Math.max(280, W - 40));
    const summary = { x: W - pad - summaryW, y: pad + bh + 10, w: summaryW, h: 430 };
    const mechW = Math.min(340, Math.max(260, W - 40));
    const mechanics = { x: pad, y: pad + 132, w: mechW, h: 430 };

    // two rows: town building above, city services below
    const cell = 50;
    const cgap = 6;
    const rowGap = 6;
    const rows = [TOOLS.filter((t) => t.row === 0), TOOLS.filter((t) => t.row === 1)];
    const rowW = rows.map((r) => r.length * cell + (r.length - 1) * cgap);
    const totalW = Math.max(...rowW);
    const toolbarH = rows.length * cell + rowGap;
    const tx = Math.round((W - totalW) / 2);
    const ty = H - toolbarH - 16;
    const tools = [];
    rows.forEach((rowTools, ri) => {
      const rw = rowW[ri];
      const rx = Math.round((W - rw) / 2);
      rowTools.forEach((t, i) => {
        tools.push({
          x: rx + i * (cell + cgap),
          y: ty + ri * (cell + rowGap),
          w: cell,
          h: cell,
          id: t.id,
          key: t.key,
          label: t.label,
          service: !!t.service,
          locked: t.service ? !this.sim.systems.isUnlocked(t.id) : false,
        });
      });
    });

    return {
      pad,
      speeds,
      summaryButton,
      summary,
      mechanics,
      toolsButton: { x: summaryButton.x - 104, y: pad, w: 96, h: bh },
      tools,
      toolbar: { x: tx - 10, y: ty - 10, w: totalW + 20, h: toolbarH + 20 },
      hintY: ty - 46,
    };
  }

  hudHit(cssX, cssY) {
    if (!this.showHud) return null;
    const L = this.layout();
    const hit = (r) => cssX >= r.x && cssX <= r.x + r.w && cssY >= r.y && cssY <= r.y + r.h;
    for (const sp of L.speeds) if (hit(sp)) return { kind: 'speed', v: sp.v };
    if (hit(L.summaryButton)) return { kind: 'summary' };
    if (hit(L.toolsButton)) return { kind: 'mechanics' };
    for (const t of L.tools) if (hit(t)) return { kind: 'tool', id: t.id };
    if (this.mechanicsOpen) {
      for (const r of this.mechHits || []) {
        if (cssX >= r.x && cssX <= r.x + r.w && cssY >= r.y && cssY <= r.y + r.h) {
          return { kind: 'mech', action: r.action };
        }
      }
    }
    return null;
  }

  drawHud(ctx) {
    const sim = this.sim;
    const s = sim.stats();
    const W = this.cssW;
    const H = this.cssH;
    const L = this.layout();
    const hint = sim.statusHint ? sim.statusHint() : null;

    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    ctx.textBaseline = 'alphabetic';
    ctx.globalAlpha = 1;

    // ---- status, top left
    const clock = `${String(Math.floor(sim.minute / 60)).padStart(2, '0')}:${String(sim.minute % 60).padStart(2, '0')}`;
    const delta = sim.budgetToday ? sim.budgetToday() : null;
    const rows = [
      [`день ${sim.day} · ${clock}`, 15, 600, '#f5efe1'],
      [
        `бюджет ${Math.round(s.budget).toLocaleString('ru-RU')} ₽` +
          (delta === null ? '' : ` · ${delta >= 0 ? '+' : '−'}${Math.abs(Math.round(delta)).toLocaleString('ru-RU')} за день`),
        15,
        600,
        '#9be08a',
      ],
      [`жителей ${s.pop} · работают ${s.employed}/${s.jobs}`, 13, 400, '#e7e1d3'],
      [`дома ${s.houses} · лавки ${s.shops} · заводы ${s.factories}`, 13, 400, '#bfb7a4'],
      [
        `${s.season} · налог ${Math.round(s.tax * 100)}%` + (s.loan > 0 ? ` · долг ${Math.round(s.loan)} ₽` : '') + ` · мусор ${s.waste.toFixed(0)}`,
        13,
        400,
        '#bfb7a4',
      ],
      [
        `${s.hungry ? `голодных ${s.hungry} · ` : ''}настроение ${Math.round(s.avgMood * 100)}%`,
        13,
        400,
        s.avgMood > 0.55 ? '#a9c9a0' : '#e0a05a',
      ],
    ];
    if (hint) rows.push([hint, 13, 500, '#f2c368']);
    const hints = [];
    if (!this.summaryOpen) hints.push('E — экономика города: доходы, товары, люди');
    if (!this.mechanicsOpen) hints.push('M — механики: службы, среда, политика');
    if (hints.length) rows.push([hints.join('  ·  '), 12, 400, 'rgba(226,220,204,0.45)']);

    const lineH = 20;
    const padding = 12;
    let panelW = 0;
    for (const [text, size, weight] of rows) {
      ctx.font = `${weight} ${size}px ${FONT}`;
      panelW = Math.max(panelW, ctx.measureText(text).width);
    }
    const panelH = rows.length * lineH + padding * 2 - 4;
    panelW = Math.ceil(panelW) + padding * 2;
    this.panel(ctx, L.pad, L.pad, panelW, panelH, 10);
    this.statusBottom = L.pad + panelH;

    let lineY = L.pad + padding + 12;
    for (const [text, size, weight, color] of rows) {
      ctx.font = `${weight} ${size}px ${FONT}`;
      ctx.fillStyle = color;
      ctx.fillText(text, L.pad + padding, lineY);
      lineY += lineH;
    }

    // ---- speed buttons, top right
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    for (const sp of L.speeds) {
      const active = sim.paused ? sp.v === 0 : sim.speed === sp.v;
      this.button(ctx, sp.x, sp.y, sp.w, sp.h, active);
      ctx.font = `600 13px ${FONT}`;
      ctx.fillStyle = active ? '#22201a' : '#ded7c8';
      const label = sp.v === 0 ? '❙❙' : `×${sp.v}`;
      ctx.fillText(label, sp.x + sp.w / 2, sp.y + sp.h / 2 + 0.5);
    }
    // ---- mechanics toggle
    const mb = L.toolsButton;
    this.button(ctx, mb.x, mb.y, mb.w, mb.h, this.mechanicsOpen);
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.font = `600 12px ${FONT}`;
    ctx.fillStyle = this.mechanicsOpen ? '#22201a' : '#ded7c8';
    ctx.fillText('механики', mb.x + mb.w / 2, mb.y + mb.h / 2 + 0.5);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';

    // ---- summary toggle, next to the speed buttons
    const sb = L.summaryButton;
    this.button(ctx, sb.x, sb.y, sb.w, sb.h, this.summaryOpen);
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.font = `600 12px ${FONT}`;
    ctx.fillStyle = this.summaryOpen ? '#22201a' : '#ded7c8';
    ctx.fillText('экономика', sb.x + sb.w / 2, sb.y + sb.h / 2 + 0.5);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';

    if (this.summaryOpen) this.drawSummary(ctx, L.summary);
    if (this.mechanicsOpen) {
      // never cover the status panel: it grows with the hint lines
      L.mechanics.y = Math.max(L.mechanics.y, this.statusBottom + 10);
      this.drawMechanics(ctx, L.mechanics);
    }

    // ---- toolbar, bottom centre
    this.panel(ctx, L.toolbar.x, L.toolbar.y, L.toolbar.w, L.toolbar.h, 14);
    for (const t of L.tools) {
      const active = this.tool === t.id;
      ctx.fillStyle = active ? 'rgba(232,196,106,0.20)' : 'rgba(255,255,255,0.05)';
      this.roundRect(ctx, t.x, t.y, t.w, t.h, 10);
      ctx.fill();
      ctx.lineWidth = 1;
      ctx.strokeStyle = active ? 'rgba(232,196,106,0.85)' : 'rgba(255,255,255,0.10)';
      this.roundRect(ctx, t.x + 0.5, t.y + 0.5, t.w - 1, t.h - 1, 10);
      ctx.stroke();
      this.toolIcon(ctx, t.id, t.x + t.w / 2, t.y + 18, active);
      ctx.font = `500 9px ${FONT}`;
      ctx.fillStyle = active ? '#f0d79a' : 'rgba(224,216,200,0.72)';
      ctx.textAlign = 'center';
      ctx.fillText(t.label, t.x + t.w / 2, t.y + t.h - 7);
      if (t.key) {
        ctx.font = `600 9px ${FONT}`;
        ctx.fillStyle = 'rgba(232,226,212,0.42)';
        ctx.fillText(t.key, t.x + 6, t.y + 12);
      }
      ctx.textAlign = 'left';
      if (t.locked) {
        ctx.fillStyle = 'rgba(10,14,20,0.62)';
        this.roundRect(ctx, t.x, t.y, t.w, t.h, 10);
        ctx.fill();
        ctx.fillStyle = 'rgba(232,226,212,0.7)';
        ctx.textAlign = 'center';
        ctx.font = `600 15px ${FONT}`;
        ctx.fillText('🔒', t.x + t.w / 2, t.y + t.h / 2 + 3);
        ctx.textAlign = 'left';
        ctx.font = `600 9px ${FONT}`;
        ctx.fillStyle = 'rgba(232,226,212,0.55)';
        ctx.fillText(t.label, t.x + 5, t.y + t.h - 5);
      }
    }

    // ---- tool hint above the toolbar
    const text = this.toolHint();
    ctx.font = `400 ${HINT_SIZE}px ${FONT}`;
    const hintW = Math.ceil(ctx.measureText(text).width) + 24;
    const hx = Math.round((W - hintW) / 2);
    this.panel(ctx, hx, L.hintY, hintW, 26, 13);
    ctx.fillStyle = '#d8d2c2';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, W / 2, L.hintY + 13.5);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';

    // ---- toast
    if (this.toast) {
      ctx.globalAlpha = Math.max(0, Math.min(1, this.toast.life));
      ctx.font = `500 13px ${FONT}`;
      const tw = Math.ceil(ctx.measureText(this.toast.text).width) + 26;
      const tx = Math.round((W - tw) / 2);
      const ty = L.hintY - 40;
      this.panel(ctx, tx, ty, tw, 30, 12, this.toast.error ? 'rgba(52,20,18,0.92)' : undefined);
      ctx.fillStyle = this.toast.error ? '#ffb39c' : '#f0eadb';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(this.toast.text, W / 2, ty + 15.5);
      ctx.textAlign = 'left';
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    }

    // ---- build stamp, bottom right: makes a stale cached page obvious
    if (this.build) {
      ctx.font = `500 11px ${FONT}`;
      ctx.fillStyle = 'rgba(232,226,212,0.38)';
      ctx.textAlign = 'right';
      ctx.fillText(this.build, W - L.pad, H - 12);
      ctx.textAlign = 'left';
    }
    ctx.imageSmoothingEnabled = false;
  }

  /** What the whole city earned, spent, produced and bought. */
  drawSummary(ctx, box) {
    const sim = this.sim;
    const s = sim.stats();
    const L = sim.ledger;
    const T = sim.total;
    const G = sim.goods;
    const money = (v) => `${Math.round(v).toLocaleString('ru-RU')} ₽`;
    const num = (v) => Math.round(v).toLocaleString('ru-RU');
    const sign = (v) => `${v >= 0 ? '+' : '−'}${Math.abs(Math.round(v)).toLocaleString('ru-RU')}`;
    const stock = sim.firms.reduce((a, f) => a + f.stock, 0);
    const spent = L.construction + L.welfare + L.imports + L.contractor;
    const today = sim.budgetToday();

    const rows = [];
    const section = (label) => rows.push({ kind: 'section', label });
    const row = (label, a, b, color) => rows.push({ kind: 'row', label, a, b, color });
    const note = (label) => rows.push({ kind: 'note', label });

    section('деньги города');
    row('бюджет', money(s.budget), '', '#9be08a');
    row('изменение за день', sign(today), '', today >= 0 ? '#9be08a' : '#e0a05a');
    row('потрачено за день', money(spent), '', '#d8d2c2');

    section('оборот · сегодня / всего');
    row('аренда жителей', money(L.rent), money(T.rent));
    row('налоги', money(L.taxes), money(T.taxes));
    row('выручка лавок', money(L.sales), money(T.sales));
    row('товар у заводов', money(L.restock), money(T.restock));
    row('экспорт за границу', money(L.exports), money(T.exports));
    row('зарплаты', money(L.wages), money(T.wages));
    row('стройка', money(L.construction), money(T.construction));

    section('товары · всего за игру');
    row('произведено на заводах', num(G.produced), '');
    row('куплено жителями', num(G.sold), '');
    row('вывезено на экспорт', num(G.exported), '');
    row('завезено извне', num(G.imported), '');
    row('лежит на полках сейчас', num(stock), '');
    row('лавок / заводов', `${s.shops} / ${s.factories}`, '');

    section('люди');
    row('жителей', num(s.pop), '');
    row('работают', `${s.employed} из ${s.jobs}`, '');
    row('безработных', num(Math.max(0, s.pop - s.employed)), '');
    row('голодных', num(s.hungry), '');
    const avgCash = s.pop ? sim.citizens.reduce((a, c) => a + c.cash, 0) / s.pop : 0;
    row('средняя касса жителя', money(avgCash), '');
    note('снаружи приходят только экспорт и сбережения приезжих');

    // pick the roomiest typesetting that still fits between the top bar and
    // the toolbar, dropping the least important rows last
    // keep the panel clear of the toolbar at the bottom of the screen
    const toolbarTop = this.layout().toolbar.y - 16;
    const room = Math.min(this.cssH - 12, toolbarTop) - box.y;
    const metrics = (d) => ({
      padY: 12 * d,
      titleGap: 36 * d,
      rowH: 19 * d,
      headH: 23 * d,
      noteH: 26 * d,
      fs: 12 * d,
      headFs: 10 * d,
      titleFs: 13 * d,
      noteFs: 11 * d,
    });
    const fit = (m) => {
      let total = m.padY * 2 + m.titleGap;
      const used = [];
      for (const r of rows) {
        const add = r.kind === 'section' ? m.headH : r.kind === 'note' ? m.noteH : m.rowH;
        if (total + add > room) break;
        total += add;
        used.push(r);
      }
      return { total, used };
    };
    let m = metrics(1);
    let list = [];
    let h = 0;
    for (const d of [1, 0.94, 0.88, 0.82, 0.76, 0.7, 0.64, 0.58]) {
      m = metrics(d);
      const f = fit(m);
      list = f.used;
      h = f.total + 6;
      if (list.length === rows.length) break;
    }

    const x = box.x;
    const y = box.y;
    const w = box.w;
    this.panel(ctx, x, y, w, h, 12);

    let cy = y + m.padY;
    ctx.font = `600 ${m.titleFs}px ${FONT}`;
    ctx.fillStyle = '#f0eadb';
    ctx.fillText('сводка города', x + 14, cy + m.titleFs);
    ctx.font = `500 ${m.headFs}px ${FONT}`;
    ctx.fillStyle = 'rgba(226,220,204,0.45)';
    ctx.textAlign = 'right';
    const colRight = x + w - 92 * Math.max(0.8, m.fs / 12);
    ctx.fillText('сегодня', colRight, cy + m.titleFs);
    ctx.fillText('всего', x + w - 14, cy + m.titleFs);
    ctx.textAlign = 'left';
    cy += m.titleGap;

    for (const r of list) {
      if (r.kind === 'section') {
        ctx.font = `600 ${m.headFs}px ${FONT}`;
        ctx.fillStyle = 'rgba(226,220,204,0.45)';
        ctx.fillText(r.label.toUpperCase(), x + 14, cy + m.headFs);
        ctx.fillStyle = 'rgba(255,255,255,0.08)';
        ctx.fillRect(x + 14, cy + m.headFs + 4, w - 28, 1);
        cy += m.headH;
      } else if (r.kind === 'note') {
        ctx.font = `400 ${m.noteFs}px ${FONT}`;
        ctx.fillStyle = 'rgba(226,220,204,0.42)';
        ctx.fillText(r.label, x + 14, cy + m.noteFs);
        cy += m.noteH;
      } else {
        const base = cy + m.rowH - 5;
        ctx.font = `400 ${m.fs}px ${FONT}`;
        ctx.fillStyle = r.color || '#ded7c8';
        ctx.fillText(r.label, x + 14, base);
        ctx.textAlign = 'right';
        if (r.b) {
          ctx.fillStyle = 'rgba(226,220,204,0.55)';
          ctx.fillText(r.b, x + w - 14, base);
          ctx.fillStyle = r.color || '#f0eadb';
          ctx.fillText(r.a, colRight, base);
        } else {
          ctx.fillText(r.a, x + w - 14, base);
        }
        ctx.textAlign = 'left';
        cy += m.rowH;
      }
    }
  }

  /** Live state of the systems: what is covered, what is missing, policies. */
  drawMechanics(ctx, box) {
    const sim = this.sim;
    const s = sim.stats();
    const sys = sim.systems;
    const built = sim.world.buildings.filter((b) => b.state === 'done' && b.kind !== 'park');
    const countServed = (field) => built.filter((b) => sys.served(field, b.door.x, b.door.y)).length;
    const num = (v) => Math.round(v).toLocaleString('ru-RU');

    const rows = [];
    const section = (label) => rows.push({ kind: 'section', label });
    const row = (label, value, action, on) => rows.push({ kind: 'row', label, value, action, on });

    section('службы · охват зданий');
    row('вода', `${countServed('water')} / ${built.length}`, null, countServed('water') > 0);
    row('энергия', `${countServed('power')} / ${built.length}`, null, countServed('power') > 1);
    row('школы', `${s.provided.school || 0} · учеников ${s.students}`, null, (s.provided.school || 0) > 0);
    row('поликлиники', `${s.provided.clinic || 0} · больных ${s.sick}`, null, (s.provided.clinic || 0) > 0);
    row('полиция', `${s.provided.police || 0} · краж ${sim.metrics.crimes}`, null, (s.provided.police || 0) > 0);
    row('пожарные', `${s.provided.fire || 0} · пожаров ${sim.metrics.fires}`, null, (s.provided.fire || 0) > 0);
    row('свалка', `${s.provided.landfill || 0} · мусор ${s.waste.toFixed(0)}`, null, s.waste < 8);
    row('остановки', `${s.provided.busstop || 0}`, null, (s.provided.busstop || 0) > 0);
    row('склады', `${s.provided.warehouse || 0}`, null, (s.provided.warehouse || 0) > 0);
    row('туризм · доход', `${Math.round(sim.metrics.tourism)} ₽`, null, (s.provided.landmark || 0) > 0);

    section('среда');
    row('загрязнение', `${(s.pollution * 100).toFixed(0)}%`, null, s.pollution < 0.25);
    row('шум', `${(s.noise * 100).toFixed(0)}%`, null, s.noise < 0.3);
    row('цена земли', `${s.landValue.toFixed(1)}`, null, s.landValue > 12);
    row('машин на улицах', `${sim.cars.length}`, null, true);

    section('люди');
    row('детей в школе', `${s.students}`, null, true);
    row('с образованием', `${s.educated}`, null, s.educated > 0);
    row('пенсионеры', `${s.retired}`, null, true);
    row('родилось / умерло', `${sim.metrics.births} / ${sim.metrics.deaths}`, null, true);

    section('деньги и политика');
    row('налог', `${Math.round(sim.tax * 100)}%`, 'taxUp', true);
    row('долг города', `${num(sim.loan)} ₽`, 'loan', sim.loan > 0);
    row('проценты за день', `${Math.round(sim.loan * sim.loanRate)} ₽`, null, true);
    for (const [id, p] of Object.entries(POLICIES)) {
      row(p.label, sim.policies[id] ? 'вкл' : 'выкл', `policy:${id}`, sim.policies[id]);
    }
    row('взять кредит 500 ₽', '', 'borrow', true);
    row('погасить 500 ₽', '', 'repay', sim.loan > 0);

    // fit into the free space under the status panel
    const toolbarTop = this.layout().toolbar.y - 16;
    const room = Math.min(this.cssH - 12, toolbarTop) - box.y;
    const metrics = (d) => ({ padY: 10 * d, rowH: 17 * d, headH: 20 * d, fs: 12 * d, headFs: 9.5 * d, titleFs: 13 * d });
    const fit = (m) => {
      let total = m.padY * 2 + 26;
      const used = [];
      for (const r of rows) {
        const add = r.kind === 'section' ? m.headH : m.rowH;
        if (total + add > room) break;
        total += add;
        used.push(r);
      }
      return { total, used };
    };
    let m = metrics(1);
    let list = [];
    let h = 0;
    for (const d of [1, 0.94, 0.88, 0.82, 0.76, 0.7]) {
      m = metrics(d);
      const f = fit(m);
      list = f.used;
      h = f.total + 4;
      if (list.length === rows.length) break;
    }

    const x = box.x;
    const y = box.y;
    const w = box.w;
    this.panel(ctx, x, y, w, h, 12);
    ctx.font = `600 ${m.titleFs}px ${FONT}`;
    ctx.fillStyle = '#f0eadb';
    ctx.fillText('механики города', x + 14, y + m.padY + m.titleFs);
    let cy = y + m.padY + 24;
    this.mechHits = [];

    for (const r of list) {
      if (r.kind === 'section') {
        ctx.font = `600 ${m.headFs}px ${FONT}`;
        ctx.fillStyle = 'rgba(226,220,204,0.45)';
        ctx.fillText(r.label.toUpperCase(), x + 14, cy + m.headFs);
        ctx.fillStyle = 'rgba(255,255,255,0.08)';
        ctx.fillRect(x + 14, cy + m.headFs + 4, w - 28, 1);
        cy += m.headH;
        continue;
      }
      const base = cy + m.rowH - 5;
      const clickable = !!r.action;
      if (clickable) {
        ctx.fillStyle = 'rgba(255,255,255,0.05)';
        this.roundRect(ctx, x + 8, cy + 1, w - 16, m.rowH - 2, 6);
        ctx.fill();
        this.mechHits.push({ x: x + 8, y: cy + 1, w: w - 16, h: m.rowH - 2, action: r.action });
      }
      ctx.font = `400 ${m.fs}px ${FONT}`;
      ctx.fillStyle = r.on === false ? 'rgba(224,180,150,0.9)' : '#ded7c8';
      ctx.fillText(r.label, x + 14, base);
      ctx.textAlign = 'right';
      ctx.fillStyle = r.on === false ? '#e0a05a' : '#f0eadb';
      ctx.fillText(r.value || '', x + w - 14, base);
      ctx.textAlign = 'left';
      cy += m.rowH;
    }
  }

  roundRect(ctx, x, y, w, h, r) {
    const rr = Math.min(r, w / 2, h / 2);
    ctx.beginPath();
    ctx.moveTo(x + rr, y);
    ctx.lineTo(x + w - rr, y);
    ctx.quadraticCurveTo(x + w, y, x + w, y + rr);
    ctx.lineTo(x + w, y + h - rr);
    ctx.quadraticCurveTo(x + w, y + h, x + w - rr, y + h);
    ctx.lineTo(x + rr, y + h);
    ctx.quadraticCurveTo(x, y + h, x, y + h - rr);
    ctx.lineTo(x, y + rr);
    ctx.quadraticCurveTo(x, y, x + rr, y);
    ctx.closePath();
  }

  panel(ctx, x, y, w, h, r = 8, fill) {
    this.roundRect(ctx, x, y, w, h, r);
    ctx.fillStyle = fill || 'rgba(12,17,24,0.84)';
    ctx.fill();
    ctx.lineWidth = 1;
    ctx.strokeStyle = 'rgba(255,255,255,0.11)';
    this.roundRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, r);
    ctx.stroke();
  }

  button(ctx, x, y, w, h, active) {
    this.roundRect(ctx, x, y, w, h, 8);
    ctx.fillStyle = active ? '#e8c46a' : 'rgba(255,255,255,0.07)';
    ctx.fill();
    ctx.lineWidth = 1;
    ctx.strokeStyle = active ? 'rgba(255,240,200,0.75)' : 'rgba(255,255,255,0.12)';
    this.roundRect(ctx, x + 0.5, y + 0.5, w - 1, h - 1, 8);
    ctx.stroke();
  }

  toolHint() {
    const sim = this.sim;
    if (!sim.townConnected()) {
      return this.tool === 'road'
        ? 'доведи дорогу до края карты — по ней приедут люди и привезут материалы'
        : 'без дороги к краю карты город не строится и никто не приезжает';
    }
    const svc = SERVICES[this.tool];
    if (svc) {
      if (!sim.systems.isUnlocked(this.tool)) return `${svc.short}: откроется, когда в городе будет ${svc.unlock} жителей`;
      return `${svc.short} ${svc.cost} ₽ · ${svc.note} · содержание ${svc.upkeep} ₽/день`;
    }
    if (this.tool === 'warehouse') {
      if (!sim.systems.isUnlocked('warehouse')) return 'склад: откроется, когда в городе будет 22 жителя';
      return 'склад 300 ₽: закупает товар оптом и снабжает лавки по 6.5 ₽ за единицу';
    }
    switch (this.tool) {
      case 'road':
        return 'дорога 12 ₽ · клик или протяжка · жители ходят только по дорогам';
      case 'zoneR':
        return 'жилая зона: дома вырастут сами, когда появятся рабочие места';
      case 'zoneC':
        return 'торговля: лавки появятся, когда жителям нужно больше хлеба';
      case 'zoneI':
        return 'промзона: заводы дают работу и товар для лавок';
      case 'park':
        return 'парк 60 ₽: деревья и скамейки, поднимают настроение';
      default:
        return 'снос: ПКМ тоже сносит · колесо — масштаб · WASD — камера';
    }
  }

  toolIcon(ctx, id, cx, cy, active) {
    const gold = active ? '#f2d492' : '#cfc8b8';
    if (id === 'bulldoze') {
      ctx.strokeStyle = gold;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(cx - 7, cy - 5);
      ctx.lineTo(cx + 7, cy + 5);
      ctx.moveTo(cx + 7, cy - 5);
      ctx.lineTo(cx - 7, cy + 5);
      ctx.stroke();
    } else if (id === 'road') {
      ctx.fillStyle = active ? '#5b6068' : '#4b5057';
      this.roundRect(ctx, cx - 12, cy - 6, 24, 12, 3);
      ctx.fill();
      ctx.fillStyle = gold;
      for (let i = -1; i <= 1; i++) ctx.fillRect(cx + i * 8 - 2, cy - 1, 5, 2);
    } else if (id.startsWith('zone')) {
      const col = id === 'zoneR' ? '#6fbf5f' : id === 'zoneC' ? '#5b93d8' : '#cba64c';
      ctx.fillStyle = col;
      ctx.globalAlpha = active ? 0.95 : 0.8;
      this.roundRect(ctx, cx - 11, cy - 7, 22, 14, 3);
      ctx.fill();
      ctx.globalAlpha = 1;
      ctx.fillStyle = 'rgba(0,0,0,0.28)';
      for (let i = 0; i < 3; i++) ctx.fillRect(cx - 11, cy - 5 + i * 5, 22, 1);
      if (id === 'zoneR') {
        ctx.fillStyle = '#f0f0e4';
        ctx.fillRect(cx - 3, cy - 2, 6, 5);
        ctx.fillStyle = '#c25b4b';
        ctx.beginPath();
        ctx.moveTo(cx - 5, cy - 2);
        ctx.lineTo(cx, cy - 6);
        ctx.lineTo(cx + 5, cy - 2);
        ctx.closePath();
        ctx.fill();
      } else if (id === 'zoneC') {
        ctx.fillStyle = '#f0e0c0';
        ctx.fillRect(cx - 4, cy - 3, 8, 6);
        ctx.fillStyle = '#9c6a3c';
        ctx.fillRect(cx - 4, cy - 5, 8, 2);
      } else {
        ctx.fillStyle = '#cfc8b8';
        ctx.fillRect(cx - 5, cy - 4, 10, 7);
        ctx.fillStyle = '#8e8577';
        ctx.fillRect(cx - 1, cy - 8, 2, 4);
      }
    } else if (id === 'park') {
      ctx.fillStyle = '#5d9342';
      ctx.fillRect(cx - 1, cy, 2, 6);
      ctx.fillStyle = active ? '#8fd08a' : '#7cc05a';
      ctx.beginPath();
      ctx.arc(cx, cy - 3, 7, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = 'rgba(0,0,0,0.16)';
      ctx.beginPath();
      ctx.arc(cx - 2, cy - 5, 3, 0, Math.PI * 2);
      ctx.fill();
    } else if (SERVICE_ICONS[id]) {
      SERVICE_ICONS[id](ctx, cx, cy, gold, active);
    }
  }

  toastMessage(text, error = false) {
    this.toast = { text, life: 2.4, error };
  }
}

export function toolColor(tool) {
  if (tool === 'road') return '#c8ccd4';
  if (tool === 'zoneR') return '#7cd06a';
  if (tool === 'zoneC') return '#6fa8ec';
  if (tool === 'zoneI') return '#e8c46a';
  if (tool === 'park') return '#8fd08a';
  return '#e0574d';
}

export { TOOLS, diamond, TW, TH, HW, HH };
