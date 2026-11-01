// Pixel renderer: draw everything into a small buffer at 1:1 art pixels, then
// blow it up by a whole number with smoothing off. That is what keeps the
// picture crisp and "pixel" at any window size.

import { TW, TH, HW, HH, iso, corners, fill, pxLine, diamond } from './iso.js?v=2026-11-01-pixel-3';
import { T } from './world.js?v=2026-11-01-pixel-3';
import { drawGroundTile, drawBuilding, drawCitizen, drawTruck, drawProp, PAL } from './art.js?v=2026-11-01-pixel-3';

const TOOLS = [
  { id: 'bulldoze', key: '1', label: 'снос' },
  { id: 'road', key: '2', label: 'дорога' },
  { id: 'zoneR', key: '3', label: 'жильё' },
  { id: 'zoneC', key: '4', label: 'торговля' },
  { id: 'zoneI', key: '5', label: 'промзона' },
  { id: 'park', key: '6', label: 'парк' },
];

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
    this.build = '';
    this.resize();
    this.centerOn(17.5, 18.5);
  }

  // ------------------------------------------------------------------ setup
  resize() {
    const cssW = Math.max(320, this.canvas.clientWidth || window.innerWidth);
    const cssH = Math.max(240, this.canvas.clientHeight || window.innerHeight);
    this.pxScale = 2 * this.zoom;
    const bufW = Math.floor(cssW / this.pxScale);
    const bufH = Math.floor(cssH / this.pxScale);
    this.bufW = bufW;
    this.bufH = bufH;
    this.buf.width = bufW;
    this.buf.height = bufH;
    this.canvas.width = bufW * this.pxScale;
    this.canvas.height = bufH * this.pxScale;
    this.bctx.imageSmoothingEnabled = false;
    this.ctx.imageSmoothingEnabled = false;
    this.clampCamera();
  }

  worldBounds() {
    const w = this.sim.world;
    return {
      minX: -this.origin.x - 8,
      maxX: this.origin.x + 8,
      minY: -40,
      maxY: w.n * TH + 20,
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

    if (this.showHud) this.drawHud(ctx);
    this.present();
  }

  drawDepth(ctx) {
    const sim = this.sim;
    const items = [];
    for (const b of sim.world.buildings) {
      if (b.kind === 'park') continue;
      items.push({ k: (b.x + b.w) + (b.y + b.d), draw: () => drawBuilding(ctx, b, sim, this.night()) });
    }
    for (const p of sim.world.props) {
      items.push({ k: p.x + p.y + 1, draw: () => drawProp(ctx, p, sim) });
    }
    for (const c of sim.citizens) {
      if (!c.visible) continue;
      if (c.state === 'home' || c.state === 'work' || c.state === 'builder' || c.state === 'sleep') continue;
      items.push({ k: c.x + c.y + 1, draw: () => drawCitizen(ctx, c) });
    }
    for (const t of sim.trucks) {
      items.push({ k: t.x + t.y + 1, draw: () => drawTruck(ctx, t) });
    }
    items.sort((a, b) => a.k - b.k);
    for (const it of items) it.draw();
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
    ctx.drawImage(this.buf, 0, 0, this.bufW, this.bufH, 0, 0, this.bufW * this.pxScale, this.bufH * this.pxScale);
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
  drawHud(ctx) {
    const sim = this.sim;
    const s = sim.stats();
    const W = this.bufW;
    const H = this.bufH;

    // ---- top-left status
    const pad = 4;
    const lines = [
      [`день ${sim.day}, ${String(Math.floor(sim.minute / 60)).padStart(2, '0')}:${String(sim.minute % 60).padStart(2, '0')}${this.build ? ` · ${this.build}` : ''}`, '#e8e2d4'],
      [`бюджет ${Math.round(s.budget)}`, '#9be08a'],
      [`жителей ${s.pop} · работают ${s.employed}/${s.jobs}`, '#e8e2d4'],
      [`дома ${s.houses} · лавки ${s.shops} · заводы ${s.factories}`, '#c8bda8'],
      [`настроение ${(s.avgMood * 100).toFixed(0)}%${s.hungry ? ` · голодных ${s.hungry}` : ''}`, s.avgMood > 0.55 ? '#9be08a' : '#e0a05a'],
    ];
    let panelW = 0;
    ctx.font = '8px ui-monospace, monospace';
    for (const [text] of lines) panelW = Math.max(panelW, ctx.measureText(text).width);
    panelW = Math.ceil(panelW) + pad * 2 + 2;
    const panelH = lines.length * 9 + pad * 2;
    this.panel(ctx, pad, pad, panelW, panelH);
    lines.forEach(([text, color], i) => {
      ctx.fillStyle = color;
      ctx.fillText(text, pad + 4, pad + 4 + i * 9 + 6);
    });

    // ---- speed buttons, top-right
    const speeds = [
      { label: 'II', v: 0 },
      { label: '1', v: 1 },
      { label: '2', v: 2 },
      { label: '4', v: 4 },
    ];
    const bw = 16;
    let bx = W - pad - speeds.length * (bw + 2) + 2;
    for (const sp of speeds) {
      const active = sim.paused ? sp.v === 0 : sim.speed === sp.v;
      this.button(ctx, bx, pad, bw, 12, active);
      ctx.fillStyle = active ? '#1b2229' : '#e8e2d4';
      ctx.fillText(sp.label, bx + bw / 2 - ctx.measureText(sp.label).width / 2, pad + 9);
      bx += bw + 2;
    }

    // ---- toolbar
    const cell = 26;
    const gap = 2;
    const totalW = TOOLS.length * (cell + gap) - gap;
    const tx = Math.round((W - totalW) / 2);
    const ty = H - cell - 6;
    this.panel(ctx, tx - 4, ty - 4, totalW + 8, cell + 8);
    for (let i = 0; i < TOOLS.length; i++) {
      const t = TOOLS[i];
      const x = tx + i * (cell + gap);
      this.toolIcon(ctx, t.id, x, ty, cell, this.tool === t.id);
    }

    // ---- hint line on its own strip so it stays readable over the city
    ctx.font = '7px ui-monospace, monospace';
    const hint = this.toolHint();
    const hw = Math.ceil(ctx.measureText(hint).width) + 10;
    this.panel(ctx, Math.round((W - hw) / 2), ty - 18, hw, 12);
    ctx.fillStyle = '#cdc6b6';
    ctx.fillText(hint, Math.round((W - hw) / 2) + 5, ty - 9);

    if (this.toast) {
      const a = Math.min(1, this.toast.life);
      ctx.globalAlpha = a;
      ctx.font = '8px ui-monospace, monospace';
      const tw = ctx.measureText(this.toast.text).width;
      this.panel(ctx, (W - tw) / 2 - 6, ty - 30, tw + 12, 14);
      ctx.fillStyle = this.toast.error ? '#f0a08a' : '#e8e2d4';
      ctx.fillText(this.toast.text, (W - tw) / 2, ty - 20);
      ctx.globalAlpha = 1;
    }
  }

  panel(ctx, x, y, w, h) {
    ctx.fillStyle = 'rgba(16,22,28,0.86)';
    ctx.fillRect(x, y, w, h);
    ctx.fillStyle = 'rgba(255,255,255,0.10)';
    ctx.fillRect(x, y, w, 1);
    ctx.fillRect(x, y, 1, h);
    ctx.fillStyle = 'rgba(0,0,0,0.5)';
    ctx.fillRect(x, y + h - 1, w, 1);
    ctx.fillRect(x + w - 1, y, 1, h);
  }

  button(ctx, x, y, w, h, active) {
    ctx.fillStyle = active ? '#e8c46a' : 'rgba(255,255,255,0.10)';
    ctx.fillRect(x, y, w, h);
    ctx.fillStyle = active ? '#8a6d22' : 'rgba(255,255,255,0.18)';
    ctx.fillRect(x, y + h - 1, w, 1);
  }

  toolHint() {
    switch (this.tool) {
      case 'road':
        return 'дорога 20₽ · клик/протяжка · людей можно селить только вдоль дорог';
      case 'zoneR':
        return 'жилая зона: дома вырастут сами, если есть работа';
      case 'zoneC':
        return 'торговля: лавки появятся, когда жителям нужно больше хлеба';
      case 'zoneI':
        return 'промзона: заводы дают работу и товар для лавок';
      case 'park':
        return 'парк 60₽: скамейки и деревья, поднимает настроение';
      default:
        return 'снос: ПКМ тоже сносит · перетаскивание ср. кнопкой — камера';
    }
  }

  toolIcon(ctx, id, x, y, s, active) {
    const bg = active ? 'rgba(232,196,106,0.22)' : 'rgba(255,255,255,0.05)';
    ctx.fillStyle = bg;
    ctx.fillRect(x, y, s, s);
    ctx.fillStyle = active ? '#e8c46a' : 'rgba(255,255,255,0.16)';
    ctx.fillRect(x, y, s, 1);
    ctx.fillRect(x, y, 1, s);
    const cx = x + s / 2;
    const cy = y + s / 2;
    ctx.font = '8px ui-monospace, monospace';
    if (id === 'bulldoze') {
      ctx.fillStyle = '#e0a05a';
      ctx.fillRect(cx - 5, cy - 1, 10, 2);
      ctx.fillRect(cx - 2, cy - 5, 4, 4);
      ctx.fillStyle = '#8a8f98';
      ctx.fillRect(cx - 6, cy + 1, 12, 2);
    } else if (id === 'road') {
      ctx.fillStyle = '#7b818c';
      ctx.fillRect(cx - 7, cy - 3, 14, 5);
      ctx.fillStyle = '#d6d9de';
      ctx.fillRect(cx - 5, cy - 1, 3, 1);
      ctx.fillRect(cx + 1, cy - 1, 3, 1);
    } else if (id.startsWith('zone')) {
      const col = id === 'zoneR' ? '#6fbf5f' : id === 'zoneC' ? '#5b93d8' : '#cba64c';
      ctx.fillStyle = col;
      ctx.fillRect(cx - 6, cy - 4, 12, 8);
      ctx.fillStyle = 'rgba(0,0,0,0.25)';
      for (let i = 0; i < 3; i++) ctx.fillRect(cx - 6, cy - 4 + i * 3, 12, 1);
    } else if (id === 'park') {
      ctx.fillStyle = '#5d9342';
      ctx.fillRect(cx - 5, cy - 2, 10, 4);
      ctx.fillStyle = '#6b4a33';
      ctx.fillRect(cx - 1, cy + 2, 2, 3);
      ctx.fillStyle = '#7cc05a';
      ctx.fillRect(cx - 3, cy - 5, 6, 4);
    }
    ctx.fillStyle = 'rgba(232,226,212,0.55)';
    const idx = TOOLS.findIndex((t) => t.id === id);
    ctx.fillText(String(idx + 1), x + 2, y + 9);
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
