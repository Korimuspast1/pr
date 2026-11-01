// Isometric painter for the test city.
// Everything is drawn with flat polygons + a few gradients, no assets needed.

import { TW, TH, iso, corners, poly, fillPoly, box, wallQuad, wallPoint } from './iso.js';
import { clamp, hash2, shade, mix, rgba, smoothstep } from './util.js';
import { N } from './world.js';

const P = {
  grass: ['#7aa65b', '#82ad60', '#74a057', '#88b166'],
  grassDark: '#5f8a48',
  road: '#5a5f6b',
  roadLight: '#6b7180',
  roadEdge: '#4a4e58',
  line: '#d9dbe0',
  pavement: '#c9bda6',
  pavementLight: '#d6cab3',
  plaza: '#ded1b6',
  plazaDark: '#c9b994',
  water: '#4a93bd',
  waterDeep: '#3c7ba1',
  field: '#b08a55',
  crop: '#7fa04a',
  path: '#c2b49a',
};

const ROOFS = ['#b0523f', '#4f6a86', '#8a5a3c', '#5c7a63'];
const WALLS = ['#e9dcc4', '#f0e4d0', '#e3d2b6', '#efe6d4'];
const SHIRTS = [
  '#e0574d', '#3f7fb5', '#e2a33c', '#5aa469', '#8c6bb1', '#d9739b',
  '#4aa39c', '#c9713f', '#7691c4', '#b35a5a', '#6d8f4a', '#a86e9c',
  '#d8b04a', '#4f8a70', '#c25a44', '#5b6fb0', '#9a7a4a', '#7a5aa0',
];

const BUILDING_STYLE = {
  house: { wall: '#ece0c8', roof: '#a94b3b', trim: '#f6efe0', chimney: true },
  shop: { wall: '#f2e8d4', roof: '#3f6f6a', trim: '#fbf5e8', awning: '#2f8f83', sign: 'МАГАЗИН' },
  bakery: { wall: '#f4e7cd', roof: '#8a4a33', trim: '#fdf6e6', awning: '#c0563c', sign: 'ХЛЕБ', chimney: true },
  cafe: { wall: '#efe1c6', roof: '#6d4a3a', trim: '#fbf4e6', awning: '#a8452f', sign: 'КАФЕ' },
  townhall: { wall: '#e9e0cd', roof: '#48566b', trim: '#fbf6ea', columns: true },
  workshop: { wall: '#c9a97a', roof: '#6b6157', trim: '#e0cba7', timber: true, sign: 'МАСТЕРСКАЯ' },
  barn: { wall: '#a83c30', roof: '#6b3a2c', trim: '#d8b9a0', barnDoor: true },
};

const SUNRISE = 6.5;
const SUNSET = 20.5;

export class Renderer {
  constructor(canvas, sim) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.sim = sim;
    this.zoom = 1;
    this.pan = { x: 0, y: 0 };
    this.autoFit = true;
    this.time = 0;
    this.particles = [];
    this.coins = [];
    this.smoke = [];
    this.ripples = [];
    this.flare = [];
    this.hint = 6;

    this.resize();
  }

  resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 1.75);
    const w = this.canvas.clientWidth || this.canvas.width;
    const h = this.canvas.clientHeight || this.canvas.height;
    this.canvas.width = Math.round(w * dpr);
    this.canvas.height = Math.round(h * dpr);
    this.dpr = dpr;
    this.w = w;
    this.h = h;
    this.fit();
  }

  /** Fit the whole town into the viewport with a little air around it. */
  fit() {
    const worldW = N * TW;
    const worldH = N * TH + 300;
    const scale = Math.min(this.w / (worldW + 120), this.h / (worldH + 90));
    this.zoom = clamp(scale, 0.24, 2.2);
    this.pan.x = 0;
    this.pan.y = (N * TH) / 2 - 26;   // town centre sits in the middle of the screen
    this.autoFit = true;
  }

  worldToScreen(x, y) {
    const z = this.zoom;
    return {
      x: (x - this.pan.x) * z + this.w / 2,
      y: (y - this.pan.y) * z + this.h / 2 - 20 * z,
    };
  }

  screenToWorld(x, y) {
    const z = this.zoom;
    return {
      x: (x - this.w / 2) / z + this.pan.x,
      y: (y - this.h / 2 + 20 * z) / z + this.pan.y,
    };
  }

  beginFrame() {
    const { ctx } = this;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    // sky / backdrop
    const g = ctx.createLinearGradient(0, 0, 0, this.h);
    const hour = this.hour();
    const night = this.nightAmount(hour);
    const warm = this.duskWarmth(hour);
    const topNight = mix('#bfe0ef', '#0b1424', night);
    const midNight = mix('#dceef1', '#14203a', night);
    const botNight = mix('#e8ecd9', '#1c2b3c', night);
    g.addColorStop(0, warm > 0.03 ? mix(topNight, '#4c4a7c', warm * 0.75) : topNight);
    g.addColorStop(0.5, warm > 0.03 ? mix(midNight, '#d97a63', warm * 0.8) : midNight);
    g.addColorStop(1, warm > 0.03 ? mix(botNight, '#f7bd77', warm * 0.95) : botNight);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, this.w, this.h);

    ctx.save();
    ctx.translate(this.w / 2, this.h / 2 - 20 * this.zoom);
    ctx.scale(this.zoom, this.zoom);
    ctx.translate(-this.pan.x, -this.pan.y);
  }

  endFrame() {
    this.ctx.restore();
  }

  hour() {
    return this.sim.minute / 60;
  }

  nightAmount(hour) {
    // 0 by day, 1 deep at night, smooth at dawn and dusk
    if (hour >= SUNRISE + 2 && hour <= SUNSET - 1) return 0;
    if (hour > SUNSET - 1 && hour < SUNSET + 2.5) return smoothstep((hour - (SUNSET - 1)) / 3.5);
    if (hour >= SUNSET + 2.5 && hour <= SUNRISE - 1.5) return 1;
    if (hour > SUNRISE - 1.5 && hour <= SUNRISE + 2) return 1 - smoothstep((hour - (SUNRISE - 1.5)) / 3.5);
    return 0;
  }

  duskWarmth(hour) {
    const dawn = Math.exp(-Math.pow((hour - 6.6) / 1.0, 2));
    const dusk = Math.exp(-Math.pow((hour - 20.1) / 0.9, 2));
    return clamp(dawn + dusk, 0, 1);
  }

  update(dt) {
    this.time += dt;
    if (this.hint > 0) this.hint -= dt;

    // smoke from chimneys
    for (const b of this.sim.world.buildings) {
      if (!BUILDING_STYLE[b.type].chimney) continue;
      if (this.sim.rng() < dt * 0.9) {
        this.smoke.push({
          x: b.x + b.w * 0.72, y: b.y + b.d * 0.25, z: 34 + b.floors * 12,
          life: 0, max: 2.4 + this.sim.rng(), drift: 0.25,
        });
      }
    }
    for (let i = this.smoke.length - 1; i >= 0; i--) {
      const s = this.smoke[i];
      s.life += dt;
      s.z += dt * 9;
      if (s.life > s.max) this.smoke.splice(i, 1);
    }

    // money particles from real transactions
    for (const evt of this.sim.drainEvents()) this.spawnFromEvent(evt);
    for (let i = this.coins.length - 1; i >= 0; i--) {
      const c = this.coins[i];
      c.life += dt;
      c.z += dt * 18 * (1 - c.life / c.max);
      if (c.life >= c.max) this.coins.splice(i, 1);
    }
    for (let i = this.ripples.length - 1; i >= 0; i--) {
      this.ripples[i].life += dt;
      if (this.ripples[i].life > 3) this.ripples.splice(i, 1);
    }
  }

  eventPos(evt) {
    const sim = this.sim;
    if (evt.to && evt.to.door) return evt.to.door;
    if (evt.firm && evt.firm.door) return evt.firm.door;
    if (evt.firm && evt.firm.building) return { x: evt.firm.building.door.x + 0.5, y: evt.firm.building.door.y + 0.5 };
    if (evt.citizen) return evt.citizen.pos;
    if (evt.to && evt.to.pos) return evt.to.pos;
    return { x: 10, y: 10 };
  }

  spawnFromEvent(evt) {
    const kind = evt.type;
    if (kind === 'wage' || kind === 'bonus' || kind === 'benefit') {
      const p = evt.citizen ? evt.citizen.pos : this.eventPos(evt);
      this.addCoin(p, evt.amount, kind === 'wage' ? '#f2c14e' : '#8bd17c', '+');
    } else if (kind === 'purchase' || kind === 'welfare') {
      const p = this.eventPos(evt);
      this.addCoin({ x: p.x, y: p.y }, evt.amount, '#f4d06f', '');
    } else if (kind === 'rent' || kind === 'tax') {
      const th = this.sim.townhall;
      this.addCoin({ x: th.door.x + 0.5, y: th.door.y + 0.5 }, evt.amount, '#9fb7d4', '');
    } else if (kind === 'public' || kind === 'support') {
      const f = this.sim.firms.workshop;
      this.addCoin({ x: f.door.x, y: f.door.y }, evt.amount, '#b9a0e8', '');
    } else if (kind === 'trade') {
      this.sim.spawnTruck(evt.from, evt.to);
    } else if (kind === 'missed') {
      const p = evt.citizen.pos;
      this.addCoin(p, 0, '#d05a5a', '');
    }
  }

  addCoin(pos, amount, color, sign) {
    if (this.coins.length > 140) return;
    this.coins.push({
      x: pos.x + (Math.random() - 0.5) * 0.35,
      y: pos.y + (Math.random() - 0.5) * 0.35,
      z: 12 + Math.random() * 10,
      life: 0,
      max: 1.5 + Math.random() * 0.8,
      color,
      amount,
      sign,
      sway: (Math.random() - 0.5) * 12,
    });
  }

  draw() {
    const sim = this.sim;
    const world = sim.world;
    this.beginFrame();

    this.drawPlinth();
    this.drawGround(world);

    // collect drawable objects and sort them back-to-front
    const items = [];
    for (const b of world.buildings) items.push({ k: b.x + b.y, y: b.y, draw: () => this.drawBuilding(b) });
    for (const t of world.trees) items.push({ k: t.x + t.y, y: t.y, draw: () => this.drawTree(t) });
    for (const p of world.props) items.push({ k: p.x + p.y, y: p.y, draw: () => this.drawProp(p) });
    for (const c of sim.citizens) {
      if (!c.visible) continue;
      items.push({ k: c.pos.x + c.pos.y, y: c.pos.y, draw: () => this.drawCitizen(c) });
    }
    for (const t of sim.trucks) items.push({ k: t.pos.x + t.pos.y, y: t.pos.y, draw: () => this.drawTruck(t) });
    items.sort((a, b) => (a.k - b.k) || (a.y - b.y));
    for (const it of items) it.draw();

    this.drawSmoke();
    this.drawCoins();
    this.drawLighting(world);
    this.endFrame();
    if (this.hint > 0) this.drawHint();
  }

  // ---------------------------------------------------------------- ground
  /** A slice of earth under the town, so the diorama reads as a solid block. */
  drawPlinth() {
    const ctx = this.ctx;
    const t = 34;
    const c = corners(0, 0, N, N);
    const [n, e, s, w] = c;
    // front-right face
    fillPoly(ctx, [e, s, { x: s.x, y: s.y + t }, { x: e.x, y: e.y + t }], '#7a6244');
    // front-left face
    fillPoly(ctx, [s, w, { x: w.x, y: w.y + t }, { x: s.x, y: s.y + t }], '#634f38');
    // soil layers
    for (let i = 0; i < 3; i++) {
      const y0 = s.y + 8 + i * 9;
      ctx.strokeStyle = i % 2 ? 'rgba(90,72,50,0.55)' : 'rgba(120,98,68,0.45)';
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(w.x, y0 + (w.y - s.y));
      ctx.lineTo(s.x, y0);
      ctx.lineTo(e.x, y0 + (e.y - s.y));
      ctx.stroke();
    }
    ctx.save();
    ctx.globalAlpha = 0.25;
    fillPoly(ctx, [e, s, { x: s.x, y: s.y + t + 8 }, { x: e.x, y: e.y + t + 8 }], '#2b2016');
    ctx.restore();
  }

  drawGround(world) {
    const ctx = this.ctx;
    for (let y = 0; y < N; y++) {
      for (let x = 0; x < N; x++) {
        this.drawTile(world, x, y);
      }
    }
    // soft shadows cast on the ground (light comes from the upper left)
    for (const b of world.buildings) {
      const o = 0.22;
      ctx.save();
      ctx.globalAlpha = 0.13;
      fillPoly(ctx, corners(b.x + o, b.y + o, b.w, b.d), '#1b2a17');
      ctx.restore();
    }

    // pedestrian crossings on the two main streets
    for (const [cx, cy, vertical] of [
      [6, 6, true], [13, 6, true], [6, 13, false], [13, 13, false],
    ]) {
      for (let i = 0; i < 4; i++) {
        const u = 0.12 + i * 0.24;
        if (vertical) {
          const a = iso(cx + 0.62, cy + u);
          const b2 = iso(cx + 0.9, cy + u);
          const c2 = iso(cx + 0.9, cy + u + 0.13);
          const d2 = iso(cx + 0.62, cy + u + 0.13);
          fillPoly(ctx, [a, b2, c2, d2], 'rgba(230,232,236,0.75)');
        } else {
          const a = iso(cx + u, cy + 0.62);
          const b2 = iso(cx + u + 0.13, cy + 0.62);
          const c2 = iso(cx + u + 0.13, cy + 0.9);
          const d2 = iso(cx + u, cy + 0.9);
          fillPoly(ctx, [a, b2, c2, d2], 'rgba(230,232,236,0.75)');
        }
      }
    }

    // soft vignette on the town plate
    const c0 = corners(0, 0, N, N);
    ctx.save();
    ctx.globalAlpha = 0.16;
    poly(ctx, c0);
    ctx.strokeStyle = '#20301e';
    ctx.lineWidth = 10;
    ctx.stroke();
    ctx.restore();
  }

  drawTile(world, x, y) {
    const ctx = this.ctx;
    const tile = world.at(x, y);
    const t = tile.type;
    const key = world.walkKey(x, y);

    if (t === 'grass') {
      const h = hash2(x, y, 3);
      const base = P.grass[Math.floor(h * P.grass.length)];
      fillPoly(ctx, corners(x, y), base);
      if (h > 0.62) {
        // tufts
        const px = iso(x + 0.3, y + 0.35);
        const py = iso(x + 0.65, y + 0.6);
        ctx.fillStyle = rgba('#4f7838', 0.35);
        ctx.beginPath();
        ctx.ellipse(px.x, px.y, 3.2, 1.8, 0, 0, Math.PI * 2);
        ctx.ellipse(py.x, py.y, 2.4, 1.4, 0, 0, Math.PI * 2);
        ctx.fill();
      }
      return;
    }

    if (t === 'road') {
      fillPoly(ctx, corners(x, y), P.road);
      // asphalt grain
      const h = hash2(x, y, 5);
      if (h > 0.5) {
        const p = iso(x + 0.25 + h * 0.4, y + 0.3 + h * 0.35);
        ctx.fillStyle = rgba('#7d828f', 0.25);
        ctx.beginPath();
        ctx.ellipse(p.x, p.y, 5, 2.4, 0, 0, Math.PI * 2);
        ctx.fill();
      }
      // curbs where the road meets a non-road tile
      const isRoad = (dx, dy) => world.typeAt(x + dx, y + dy) === 'road';
      ctx.lineWidth = 1;
      if (!isRoad(-1, 0)) this.edge(x, y, 'w');
      if (!isRoad(0, -1)) this.edge(x, y, 'n');
      if (!isRoad(1, 0)) this.edge(x, y, 'e');
      if (!isRoad(0, 1)) this.edge(x, y, 's');
      // centre dashes run along the road axis
      if (x === 6 || x === 13) {
        const p0 = iso(x + 0.5, y + 0.18);
        const p1 = iso(x + 0.5, y + 0.82);
        ctx.strokeStyle = rgba(P.line, 0.55);
        ctx.lineWidth = 1.4;
        ctx.beginPath();
        ctx.moveTo(p0.x, p0.y);
        ctx.lineTo(p1.x, p1.y);
        ctx.stroke();
      } else if (y === 6 || y === 13) {
        const p0 = iso(x + 0.18, y + 0.5);
        const p1 = iso(x + 0.82, y + 0.5);
        ctx.strokeStyle = rgba(P.line, 0.55);
        ctx.lineWidth = 1.4;
        ctx.beginPath();
        ctx.moveTo(p0.x, p0.y);
        ctx.lineTo(p1.x, p1.y);
        ctx.stroke();
      }
      return;
    }

    if (t === 'pavement' || t === 'plaza') {
      const base = t === 'plaza' ? P.plaza : P.pavement;
      fillPoly(ctx, corners(x, y), base);
      const h = hash2(x, y, 7);
      if (h > 0.7) fillPoly(ctx, corners(x, y), t === 'plaza' ? P.plazaDark : P.pavementLight);
      // paving joints
      ctx.strokeStyle = rgba('#a99a7e', t === 'plaza' ? 0.35 : 0.25);
      ctx.lineWidth = 0.8;
      const a = iso(x + 0.5, y);
      const b = iso(x + 0.5, y + 1);
      const c = iso(x, y + 0.5);
      const d = iso(x + 1, y + 0.5);
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.moveTo(c.x, c.y);
      ctx.lineTo(d.x, d.y);
      ctx.stroke();
      return;
    }

    if (t === 'water') {
      const isWater = (dx, dy) => world.typeAt(x + dx, y + dy) === 'water';
      const rim = (ax, ay, bx, by) => {
        ctx.strokeStyle = 'rgba(150,148,130,0.95)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(ax, ay);
        ctx.lineTo(bx, by);
        ctx.stroke();
      };
      if (!isWater(-1, 0)) { const a = iso(x, y); const b2 = iso(x, y + 1); rim(a.x, a.y, b2.x, b2.y); }
      if (!isWater(0, -1)) { const a = iso(x, y); const b2 = iso(x + 1, y); rim(a.x, a.y, b2.x, b2.y); }
      if (!isWater(1, 0)) { const a = iso(x + 1, y); const b2 = iso(x + 1, y + 1); rim(a.x, a.y, b2.x, b2.y); }
      if (!isWater(0, 1)) { const a = iso(x, y + 1); const b2 = iso(x + 1, y + 1); rim(a.x, a.y, b2.x, b2.y); }
      const phase = this.time * 0.8 + x * 0.7 + y * 1.3;
      const w = 0.5 + 0.5 * Math.sin(phase);
      fillPoly(ctx, corners(x, y), mix(P.waterDeep, P.water, w * 0.6));
      ctx.save();
      ctx.globalAlpha = 0.35;
      for (let i = 0; i < 2; i++) {
        const yy = 0.28 + i * 0.36 + 0.06 * Math.sin(phase + i);
        const p0 = iso(x + 0.2, y + yy);
        const p1 = iso(x + 0.55, y + yy + 0.12);
        ctx.strokeStyle = '#cfe9f5';
        ctx.lineWidth = 1.1;
        ctx.beginPath();
        ctx.moveTo(p0.x, p0.y);
        ctx.lineTo(p1.x, p1.y);
        ctx.stroke();
      }
      ctx.restore();
      return;
    }

    if (t === 'field') {
      fillPoly(ctx, corners(x, y), P.field);
      for (let i = 0; i < 3; i++) {
        const yy = 0.18 + i * 0.3;
        const p0 = iso(x + 0.06, y + yy);
        const p1 = iso(x + 0.94, y + yy + 0.16);
        ctx.strokeStyle = rgba(P.crop, 0.65);
        ctx.lineWidth = 3.2;
        ctx.beginPath();
        ctx.moveTo(p0.x, p0.y);
        ctx.lineTo(p1.x, p1.y);
        ctx.stroke();
      }
      return;
    }

    fillPoly(ctx, corners(x, y), P.grass[0]);
  }

  edge(x, y, side) {
    const ctx = this.ctx;
    const a = side === 'w' ? iso(x, y) : side === 'n' ? iso(x, y) : side === 'e' ? iso(x + 1, y) : iso(x, y + 1);
    const b = side === 'w' ? iso(x, y + 1) : side === 'n' ? iso(x + 1, y) : side === 'e' ? iso(x + 1, y + 1) : iso(x + 1, y + 1);
    ctx.strokeStyle = rgba('#b6ac96', 0.85);
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
  }

  // -------------------------------------------------------------- buildings
  drawBuilding(b) {
    const ctx = this.ctx;
    const st = BUILDING_STYLE[b.type];
    const floors = b.floors || (b.type === 'townhall' ? 2 : 1);
    const floorH = b.type === 'barn' ? 26 : 22;
    const wall = floors * floorH + 6;
    const { x, y, w, d } = b;
    const night = this.nightAmount(this.hour());
    const lit = night > 0.25;

    // ground slab
    ctx.save();
    ctx.globalAlpha = 0.18;
    fillPoly(ctx, corners(x - 0.06, y - 0.06, w + 0.12, d + 0.12), '#243014');
    ctx.restore();
    fillPoly(ctx, corners(x - 0.02, y - 0.02, w + 0.04, d + 0.04), '#a99a80');

    const wallCol = st.wall;
    const faceX = shade(wallCol, -0.12);
    const faceY = shade(wallCol, -0.24);

    // walls
    const top = box(ctx, x, y, w, d, 0, wall, { roofTop: null, faceX, faceY });

    // storey band
    for (let f = 1; f < floors; f++) {
      const z = f * floorH;
      wallQuad(ctx, x, y, w, d, 'x', 0, 1, z - 1.5, z, 0, rgba('#00000022', 0.14));
      wallQuad(ctx, x, y, w, d, 'y', 0, 1, z - 1.5, z, 0, rgba('#00000022', 0.14));
    }

    // windows
    const cols = Math.max(2, Math.round(w * 1.6));
    for (let f = 0; f < floors; f++) {
      for (let c = 0; c < cols; c++) {
        const u = (c + 0.5) / cols;
        const v0 = f * floorH + 8;
        this.window(x, y, w, d, 'x', u, v0, lit);
        if (b.type !== 'workshop') this.window(x, y, w, d, 'y', u, v0, lit);
      }
    }

    // door on the side that faces the street (the door tile)
    const doorSide = b.door.y >= y + d ? 'x' : 'y';
    this.door(b, doorSide, wall, st, lit);

    // timber frame for the workshop
    if (st.timber) {
      for (let i = 0; i <= 2; i++) {
        wallQuad(ctx, x, y, w, d, 'x', i / 2 - 0.02, i / 2 + 0.02, 0, wall, 0, rgba('#5d4526', 0.7));
        wallQuad(ctx, x, y, w, d, 'y', i / 2 - 0.02, i / 2 + 0.02, 0, wall, 0, rgba('#5d4526', 0.7));
      }
    }

    if (st.columns) {
      for (let i = 0; i < 4; i++) {
        const u = 0.14 + i * 0.24;
        wallQuad(ctx, x, y, w, d, 'x', u - 0.025, u + 0.025, 2, wall - 2, 0, '#f7f1e2');
        wallQuad(ctx, x, y, w, d, 'y', u - 0.025, u + 0.025, 2, wall - 2, 0, '#e8dfcc');
      }
    }

    // roof
    const style = st.roof;
    if (b.type === 'workshop' || b.type === 'shop') {
      // shallow hip roof
      const ridge = 16;
      const rh = this.roof(x, y, w, d, wall, ridge, style);
      this.drawRoofGables(x, y, w, d, wall, ridge, style, rh);
    } else {
      const ridge = b.type === 'barn' ? 26 : 20;
      const rh = this.roof(x, y, w, d, wall, ridge, style);
      this.drawRoofGables(x, y, w, d, wall, ridge, style, rh);
    }

    if (st.chimney) {
      const cx = x + w * 0.72;
      const cy = y + d * 0.24;
      const z0 = wall + 22;
      box(ctx, cx, cy, 0.3, 0.3, z0, z0 + 18, {
        roofTop: '#7a4a3c', faceX: '#8a5a4a', faceY: '#6e453a',
      });
    }

    if (b.type === 'barn') {
      // big sliding door
      wallQuad(ctx, x, y, w, d, doorSide, 0.28, 0.72, 0, 20, 0, '#7d5a44');
      wallQuad(ctx, x, y, w, d, doorSide, 0.28, 0.72, 20, 21, 0, '#5d4232');
    }

    if (st.awning) this.awning(b, wall - 6, st.awning);
    if (st.sign) this.sign(b, wall, st.sign);

    if (lit) this.lampGlow(b);
  }

  roof(x, y, w, d, z0, rise, color) {
    const ctx = this.ctx;
    const cx = x + w / 2;
    // ridge runs along y (so the two slopes face +x and -x)
    const r0 = iso(cx, y, z0 + rise);
    const r1 = iso(cx, y + d, z0 + rise);
    // slope facing +x
    const a0 = iso(x + w, y, z0);
    const a1 = iso(x + w, y + d, z0);
    fillPoly(ctx, [r0, a0, a1, r1], shade(color, -0.06));
    // slope facing -x
    const b0 = iso(x, y, z0);
    const b1 = iso(x, y + d, z0);
    fillPoly(ctx, [r0, b0, b1, r1], shade(color, -0.28));
    // ridge highlight
    ctx.strokeStyle = shade(color, 0.18);
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(r0.x, r0.y);
    ctx.lineTo(r1.x, r1.y);
    ctx.stroke();
    return { r0, r1, a0, a1, b0, b1 };
  }

  drawRoofGables(x, y, w, d, z0, rise, color, rh) {
    const ctx = this.ctx;
    // gable triangles at y and y+d
    const cx = x + w / 2;
    const t0 = iso(x, y, z0);
    const t1 = iso(x + w, y, z0);
    const r0 = iso(cx, y, z0 + rise);
    fillPoly(ctx, [t0, t1, r0], shade(color, -0.42));
    const b0 = iso(x, y + d, z0);
    const b1 = iso(x + w, y + d, z0);
    const r1 = iso(cx, y + d, z0 + rise);
    fillPoly(ctx, [b0, b1, r1], shade(color, -0.18));
  }

  window(x, y, w, d, side, u, v, lit) {
    const ctx = this.ctx;
    const ww = 0.09;
    const wh = 9;
    const frame = side === 'x' ? '#5b4b3a' : '#4d3f31';
    wallQuad(ctx, x, y, w, d, side, u - ww - 0.015, u + ww + 0.015, v - 2, v + wh + 2, 0, rgba(frame, 0.55));
    if (lit) {
      const warm = side === 'x' ? '#ffd27a' : '#f7c267';
      wallQuad(ctx, x, y, w, d, side, u - ww, u + ww, v, v + wh, 0, warm);
      const p = wallPoint(x, y, w, d, side, u, v + wh / 2);
      ctx.save();
      ctx.globalAlpha = 0.35;
      ctx.fillStyle = '#ffca6a';
      ctx.beginPath();
      ctx.arc(p.x, p.y, 10, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    } else {
      const glass = side === 'x' ? '#94b4c4' : '#7d9dad';
      wallQuad(ctx, x, y, w, d, side, u - ww, u + ww, v, v + wh, 0, glass);
      wallQuad(ctx, x, y, w, d, side, u - ww, u + ww * 0.1, v + wh * 0.5, v + wh, 0, rgba('#ffffff', 0.25));
    }
  }

  door(b, side, wall, st, lit) {
    const ctx = this.ctx;
    const { x, y, w, d } = b;
    const u0 = 0.42;
    const u1 = 0.58;
    wallQuad(ctx, x, y, w, d, side, u0 - 0.03, u1 + 0.03, 0, 20, 0, rgba('#3c2f26', 0.35));
    wallQuad(ctx, x, y, w, d, side, u0, u1, 0, 17, 0, b.type === 'shop' ? '#3f6f6a' : '#6b4a36');
    if (lit) {
      wallQuad(ctx, x, y, w, d, side, u0, u1, 15, 15.8, 0, '#ffd58a');
    }
    // step
    const p = wallPoint(x, y, w, d, side, 0.5, 0, 0);
    ctx.save();
    ctx.globalAlpha = 0.18;
    ctx.fillStyle = '#000';
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 2, 11, 4.5, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  awning(b, z, color) {
    const ctx = this.ctx;
    const { x, y, w, d } = b;
    const side = b.door.y >= y + d ? 'x' : 'y';
    const p0 = wallPoint(x, y, w, d, side, 0.24, 0, z);
    const p1 = wallPoint(x, y, w, d, side, 0.76, 0, z);
    // stripes project out of the facade
    const out = 7;
    for (let i = 0; i < 6; i++) {
      const a = 0.24 + (0.52 * i) / 6;
      const bb = 0.24 + (0.52 * (i + 1)) / 6;
      const q0 = wallPoint(x, y, w, d, side, a, 0, z);
      const q1 = wallPoint(x, y, w, d, side, bb, 0, z);
      const s0 = { x: q0.x, y: q0.y + out };
      const s1 = { x: q1.x, y: q1.y + out };
      fillPoly(ctx, [q0, q1, s1, s0], i % 2 ? color : shade(color, 0.2));
    }
    const s0 = { x: p0.x, y: p0.y + out };
    const s1 = { x: p1.x, y: p1.y + out };
    fillPoly(ctx, [p0, p1, s1, s0], rgba(color, 0.12));
  }

  sign(b, wall, text) {
    const ctx = this.ctx;
    const { x, y, w, d } = b;
    const side = b.door.y >= y + d ? 'x' : 'y';
    const z = wall - 15;
    const p = wallPoint(x, y, w, d, side, 0.5, 0, z);
    const angle = side === 'x' ? Math.PI / 6 : -Math.PI / 6;
    ctx.save();
    ctx.translate(p.x, p.y);
    ctx.rotate(angle);
    ctx.fillStyle = 'rgba(28,34,30,0.82)';
    const tw = Math.max(34, text.length * 5.2);
    ctx.fillRect(-tw / 2, -6, tw, 12);
    ctx.fillStyle = '#f5e7c8';
    ctx.font = '600 7px system-ui, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, 0, 0.5);
    ctx.restore();
  }

  lampGlow(b) {
    const ctx = this.ctx;
    const { x, y, w, d } = b;
    const side = b.door.y >= y + d ? 'x' : 'y';
    const p = wallPoint(x, y, w, d, side, 0.5, 12, 0);
    const g = ctx.createRadialGradient(p.x, p.y, 2, p.x, p.y, 46);
    g.addColorStop(0, 'rgba(255,206,120,0.30)');
    g.addColorStop(1, 'rgba(255,206,120,0)');
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(p.x, p.y, 46, 0, Math.PI * 2);
    ctx.fill();
  }

  // ------------------------------------------------------------------ props
  drawTree(t) {
    const ctx = this.ctx;
    const s = t.size || 1;
    const p = iso(t.x, t.y, 0);
    const hue = hash2(t.x * 10, t.y * 10, 11);
    const dark = mix('#4d7038', '#39562c', hue);
    const mid = mix('#5f8a45', '#456630', hue);
    const light = mix('#7fb463', '#608f4a', hue);

    ctx.save();
    ctx.globalAlpha = 0.16;
    ctx.fillStyle = '#1d2a16';
    ctx.beginPath();
    ctx.ellipse(p.x + 5 * s, p.y + 2 * s, 13 * s, 6 * s, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    // trunk
    ctx.fillStyle = '#6c4a33';
    ctx.beginPath();
    ctx.moveTo(p.x - 2 * s, p.y);
    ctx.lineTo(p.x + 2 * s, p.y);
    ctx.lineTo(p.x + 1.2 * s, p.y - 16 * s);
    ctx.lineTo(p.x - 1.2 * s, p.y - 16 * s);
    ctx.closePath();
    ctx.fill();

    // canopy: flat blobs are far cheaper than per-blob gradients
    const blobs = [
      [0, -30, 15, mid], [-9, -22, 11, dark], [9, -23, 11, mid],
      [0, -18, 12, dark], [-4, -35, 9, light], [5, -34, 9, mid],
    ];
    for (const [dx, dz, r, col] of blobs) {
      ctx.fillStyle = col;
      ctx.beginPath();
      ctx.arc(p.x + dx * s, p.y + dz * s, r * s, 0, Math.PI * 2);
      ctx.fill();
    }
    // sun-lit top
    ctx.fillStyle = light;
    ctx.beginPath();
    ctx.arc(p.x - 3 * s, p.y - 34 * s, 6 * s, 0, Math.PI * 2);
    ctx.fill();
  }

  drawProp(p) {
    const ctx = this.ctx;
    const base = iso(p.x, p.y, 0);
    switch (p.kind) {
      case 'lamp': {
        ctx.strokeStyle = '#3f4652';
        ctx.lineWidth = 2.2;
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(base.x, base.y - 30);
        ctx.stroke();
        ctx.fillStyle = '#ffe0a3';
        ctx.beginPath();
        ctx.arc(base.x, base.y - 32, 3.2, 0, Math.PI * 2);
        ctx.fill();
        if (this.nightAmount(this.hour()) > 0.2) {
          const g = ctx.createRadialGradient(base.x, base.y - 30, 2, base.x, base.y - 30, 60);
          g.addColorStop(0, 'rgba(255,214,140,0.42)');
          g.addColorStop(1, 'rgba(255,214,140,0)');
          ctx.fillStyle = g;
          ctx.beginPath();
          ctx.arc(base.x, base.y - 30, 60, 0, Math.PI * 2);
          ctx.fill();
        }
        break;
      }
      case 'bench': {
        const rot = p.rot || 0;
        ctx.save();
        ctx.translate(base.x, base.y);
        ctx.rotate(rot ? Math.PI / 6 : -Math.PI / 6);
        ctx.fillStyle = '#8a6a44';
        ctx.fillRect(-11, -7, 22, 4);
        ctx.fillRect(-11, -2, 22, 3);
        ctx.fillStyle = '#4c4034';
        ctx.fillRect(-9, 1, 2.4, 5);
        ctx.fillRect(6.5, 1, 2.4, 5);
        ctx.restore();
        break;
      }
      case 'bush': {
        const s = (p.s || 0.35) * 18;
        for (const [dx, dz, r] of [[0, 0, s], [-s * 0.7, -s * 0.3, s * 0.7], [s * 0.7, -s * 0.2, s * 0.65]]) {
          ctx.fillStyle = '#5e8a48';
          ctx.beginPath();
          ctx.arc(base.x + dx, base.y + dz, r * 0.75, 0, Math.PI * 2);
          ctx.fill();
        }
        ctx.fillStyle = '#74a35b';
        ctx.beginPath();
        ctx.arc(base.x - 1, base.y - s * 0.55, s * 0.55, 0, Math.PI * 2);
        ctx.fill();
        break;
      }
      case 'stall': {
        const hue = p.hue || 0;
        const colors = ['#d95f4a', '#4f8fbf', '#e0a03c'];
        const col = colors[hue % colors.length];
        // counter
        box(ctx, p.x - 0.35, p.y - 0.2, 0.7, 0.4, 0, 8, {
          roofTop: '#a8794f', faceX: '#8a6240', faceY: '#77553a',
        });
        // canopy poles
        ctx.strokeStyle = '#6f5539';
        ctx.lineWidth = 1.4;
        for (const [dx, dy] of [[-0.32, -0.18], [0.32, -0.18], [-0.32, 0.18], [0.32, 0.18]]) {
          const a = iso(p.x + dx, p.y + dy, 0);
          const b2 = iso(p.x + dx, p.y + dy, 22);
          ctx.beginPath();
          ctx.moveTo(a.x, a.y);
          ctx.lineTo(b2.x, b2.y);
          ctx.stroke();
        }
        const c0 = corners(p.x - 0.4, p.y - 0.26, 0.8, 0.52, 24);
        fillPoly(ctx, c0, col);
        fillPoly(ctx, [c0[1], c0[2], { x: c0[2].x, y: c0[2].y + 4 }, { x: c0[1].x, y: c0[1].y + 4 }], shade(col, -0.25));
        fillPoly(ctx, [c0[2], c0[3], { x: c0[3].x, y: c0[3].y + 4 }, { x: c0[2].x, y: c0[2].y + 4 }], shade(col, -0.35));
        // goods
        ctx.fillStyle = '#e2b34a';
        ctx.beginPath();
        ctx.arc(base.x - 5, base.y - 11, 2.6, 0, Math.PI * 2);
        ctx.arc(base.x + 1, base.y - 9, 2.4, 0, Math.PI * 2);
        ctx.fill();
        break;
      }
      case 'crate': {
        box(ctx, p.x - 0.14, p.y - 0.12, 0.28, 0.24, 0, 9, {
          roofTop: '#c39a63', faceX: '#a87f4f', faceY: '#8f6a3f',
        });
        break;
      }
      case 'fence': {
        ctx.strokeStyle = '#8a7355';
        ctx.lineWidth = 1.6;
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(base.x, base.y - 9);
        ctx.stroke();
        ctx.strokeStyle = 'rgba(160,135,100,0.85)';
        ctx.lineWidth = 1.1;
        ctx.beginPath();
        ctx.moveTo(base.x - 9, base.y - 5);
        ctx.lineTo(base.x + 9, base.y - 8);
        ctx.stroke();
        break;
      }
      case 'flowers': {
        for (let i = 0; i < 5; i++) {
          const a = iso(p.x + (i % 3 - 1) * 0.08, p.y + (Math.floor(i / 3) - 0.5) * 0.08);
          ctx.fillStyle = ['#e2728f', '#e8c455', '#d7d2e0'][i % 3];
          ctx.beginPath();
          ctx.arc(a.x, a.y - 2, 1.7, 0, Math.PI * 2);
          ctx.fill();
        }
        break;
      }
      case 'hydrant': {
        ctx.fillStyle = '#c9503f';
        ctx.fillRect(base.x - 2, base.y - 8, 4, 8);
        ctx.beginPath();
        ctx.arc(base.x, base.y - 8, 2.4, 0, Math.PI * 2);
        ctx.fill();
        break;
      }
      case 'busstop': {
        // shelter facing the street
        box(ctx, p.x - 0.45, p.y - 0.16, 0.9, 0.32, 0, 26, { roofTop: null, faceX: '#7f95a3', faceY: '#6d8290' });
        const roof = corners(p.x - 0.5, p.y - 0.2, 1.0, 0.4, 28);
        fillPoly(ctx, roof, '#4f6470');
        fillPoly(ctx, [roof[1], roof[2], { x: roof[2].x, y: roof[2].y + 4 }, { x: roof[1].x, y: roof[1].y + 4 }], '#3c4e58');
        fillPoly(ctx, [roof[2], roof[3], { x: roof[3].x, y: roof[3].y + 4 }, { x: roof[2].x, y: roof[2].y + 4 }], '#33434c');
        // glass back wall
        fillPoly(ctx, [
          { x: base.x - 14, y: base.y - 24 }, { x: base.x + 14, y: base.y - 16 },
          { x: base.x + 14, y: base.y - 2 }, { x: base.x - 14, y: base.y - 10 },
        ], 'rgba(196,226,236,0.55)');
        // bench
        ctx.fillStyle = '#8a6a44';
        ctx.fillRect(base.x - 9, base.y - 9, 16, 3);
        break;
      }
      case 'signpost': {
        ctx.strokeStyle = '#8c939c';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(base.x, base.y - 22);
        ctx.stroke();
        ctx.fillStyle = '#3f6f9e';
        ctx.fillRect(base.x - 7, base.y - 27, 14, 6);
        ctx.fillStyle = 'rgba(255,255,255,0.85)';
        ctx.fillRect(base.x - 5, base.y - 25.5, 9, 1.4);
        break;
      }
      case 'car': {
        const hues = ['#c9553f', '#3d7fa8', '#d9b04a'];
        const col = hues[(p.hue || 0) % hues.length];
        const shadow = iso(p.x + 0.3, p.y + 0.2, 0);
        ctx.fillStyle = 'rgba(20,26,20,0.24)';
        ctx.beginPath();
        ctx.ellipse(shadow.x, shadow.y + 1, 18, 7, 0, 0, Math.PI * 2);
        ctx.fill();
        // wheels
        for (const [dx, dy] of [[-0.18, -0.1], [-0.18, 0.24], [0.36, -0.1], [0.36, 0.24]]) {
          const wpt = iso(p.x + dx, p.y + dy, 0);
          ctx.fillStyle = '#22262c';
          ctx.beginPath();
          ctx.ellipse(wpt.x, wpt.y, 3.2, 2.6, 0, 0, Math.PI * 2);
          ctx.fill();
        }
        // body
        box(ctx, p.x - 0.3, p.y - 0.14, 0.6, 0.28, 3, 10, {
          roofTop: shade(col, 0.06), faceX: shade(col, -0.15), faceY: shade(col, -0.3),
        });
        // cabin with windows
        box(ctx, p.x - 0.16, p.y - 0.1, 0.32, 0.2, 10, 17, {
          roofTop: shade(col, 0.1), faceX: '#a9cbd9', faceY: '#8fb3c3',
        });
        // windscreen highlight
        const ws = iso(p.x + 0.17, p.y + 0.0, 17);
        ctx.fillStyle = 'rgba(255,255,255,0.35)';
        ctx.fillRect(ws.x - 3, ws.y - 1, 5, 3);
        break;
      }
      case 'bikes': {
        ctx.strokeStyle = '#3f4650';
        ctx.lineWidth = 1.2;
        for (let i = 0; i < 3; i++) {
          const a = iso(p.x + i * 0.14, p.y + (i % 2) * 0.1);
          ctx.beginPath();
          ctx.arc(a.x - 3, a.y - 2, 3, 0, Math.PI * 2);
          ctx.arc(a.x + 3, a.y - 2, 3, 0, Math.PI * 2);
          ctx.stroke();
        }
        break;
      }
      case 'fountain': {
        this.drawFountain(p);
        break;
      }
      case 'flag': {
        ctx.strokeStyle = '#c8ccd4';
        ctx.lineWidth = 1.6;
        ctx.beginPath();
        ctx.moveTo(base.x, base.y);
        ctx.lineTo(base.x, base.y - 44);
        ctx.stroke();
        const wave = Math.sin(this.time * 2) * 3;
        ctx.fillStyle = '#2f6f9e';
        ctx.beginPath();
        ctx.moveTo(base.x, base.y - 44);
        ctx.lineTo(base.x + 18, base.y - 40 + wave);
        ctx.lineTo(base.x + 18, base.y - 30 + wave);
        ctx.lineTo(base.x, base.y - 33);
        ctx.closePath();
        ctx.fill();
        break;
      }
      case 'silo': {
        const r = 12;
        ctx.fillStyle = '#b8b2a3';
        ctx.fillRect(base.x - r, base.y - 52, r * 2, 52);
        ctx.fillStyle = 'rgba(0,0,0,0.10)';
        ctx.fillRect(base.x, base.y - 52, r, 52);
        for (let i = 1; i < 4; i++) {
          ctx.strokeStyle = 'rgba(120,114,102,0.5)';
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(base.x - r, base.y - 52 + i * 13);
          ctx.lineTo(base.x + r, base.y - 52 + i * 13);
          ctx.stroke();
        }
        ctx.fillStyle = '#8d8779';
        ctx.beginPath();
        ctx.ellipse(base.x, base.y - 52, r, r * 0.42, 0, 0, Math.PI * 2);
        ctx.fill();
        break;
      }
      default:
        break;
    }
  }

  drawFountain(p) {
    const ctx = this.ctx;
    const cx = p.x + 0.5;
    const cy = p.y + 0.5;
    // basin
    const b0 = corners(cx - 0.6, cy - 0.6, 1.2, 1.2, 0);
    fillPoly(ctx, b0, '#cfc3ab');
    const b1 = corners(cx - 0.6, cy - 0.6, 1.2, 1.2, 7);
    fillPoly(ctx, [b0[1], b0[2], b1[2], b1[1]], '#b8ac94');
    fillPoly(ctx, [b0[2], b0[3], b1[3], b1[2]], '#a89c86');
    const water = corners(cx - 0.45, cy - 0.45, 0.9, 0.9, 7);
    fillPoly(ctx, water, '#6db3d4');
    // jet
    const top = iso(cx, cy, 26 + Math.sin(this.time * 3) * 1.5);
    ctx.strokeStyle = 'rgba(200,240,255,0.75)';
    ctx.lineWidth = 1.8;
    ctx.beginPath();
    ctx.moveTo(iso(cx, cy, 8).x, iso(cx, cy, 8).y);
    ctx.lineTo(top.x, top.y);
    ctx.stroke();
    ctx.fillStyle = 'rgba(220,245,255,0.85)';
    ctx.beginPath();
    ctx.arc(top.x, top.y, 2.6, 0, Math.PI * 2);
    ctx.fill();
    // droplets
    for (let i = 0; i < 8; i++) {
      const t = (this.time * 0.9 + i / 8) % 1;
      const ang = i * 2.39;
      const rr = 2 + t * 14;
      const px = top.x + Math.cos(ang) * rr;
      const py = top.y + Math.sin(ang) * rr * 0.45 + t * t * 34;
      ctx.globalAlpha = 0.7 * (1 - t);
      ctx.beginPath();
      ctx.arc(px, py, 1.6, 0, Math.PI * 2);
      ctx.fillStyle = '#dff4ff';
      ctx.fill();
      ctx.globalAlpha = 1;
    }
  }

  // --------------------------------------------------------------- citizens
  drawCitizen(c) {
    const ctx = this.ctx;
    const p = iso(c.pos.x, c.pos.y, 0);
    const bob = c.moving ? Math.abs(Math.sin(c.walkPhase)) * 2.2 : 0;
    const shirt = SHIRTS[c.index % SHIRTS.length];
    const skin = ['#e8bb92', '#d9a276', '#c78a5f', '#f0cba6'][c.index % 4];

    // shadow
    ctx.save();
    ctx.globalAlpha = 0.22;
    ctx.fillStyle = '#16210f';
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 1, 5.4, 2.6, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    const y = p.y - bob;
    // legs
    const swing = c.moving ? Math.sin(c.walkPhase) * 2.6 : 0;
    ctx.strokeStyle = '#3c4150';
    ctx.lineWidth = 2.1;
    ctx.beginPath();
    ctx.moveTo(p.x - 1.4, y - 8);
    ctx.lineTo(p.x - 1.4 + swing * 0.5, y);
    ctx.moveTo(p.x + 1.4, y - 8);
    ctx.lineTo(p.x + 1.4 - swing * 0.5, y);
    ctx.stroke();

    // body
    ctx.fillStyle = shirt;
    ctx.beginPath();
    ctx.roundRect(p.x - 3.4, y - 17, 6.8, 10, 2.4);
    ctx.fill();
    // arms
    ctx.strokeStyle = shade(shirt, -0.18);
    ctx.lineWidth = 1.7;
    ctx.beginPath();
    ctx.moveTo(p.x - 3.4, y - 15);
    ctx.lineTo(p.x - 4.3 - swing * 0.3, y - 9);
    ctx.moveTo(p.x + 3.4, y - 15);
    ctx.lineTo(p.x + 4.3 + swing * 0.3, y - 9);
    ctx.stroke();
    // head
    ctx.fillStyle = skin;
    ctx.beginPath();
    ctx.arc(p.x, y - 20.6, 3.1, 0, Math.PI * 2);
    ctx.fill();
    // hair
    ctx.fillStyle = '#463229';
    ctx.beginPath();
    ctx.arc(p.x, y - 21.6, 3.1, Math.PI * 0.95, Math.PI * 2.05);
    ctx.fill();

    // carried bread while shopping
    if (c.state === 'return' && c.lastPurchase === 'bread') {
      ctx.fillStyle = '#d8a869';
      ctx.beginPath();
      ctx.ellipse(p.x + 5, y - 11, 2.4, 1.7, 0.4, 0, Math.PI * 2);
      ctx.fill();
    }
    // thoughtful bubble for the hungry
    if (c.hunger > 1.6) {
      ctx.save();
      ctx.globalAlpha = 0.85;
      ctx.fillStyle = '#fff3e0';
      ctx.beginPath();
      ctx.arc(p.x + 8, y - 26, 3.4, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = '#b4543f';
      ctx.font = '600 5px system-ui';
      ctx.textAlign = 'center';
      ctx.fillText('!', p.x + 8, y - 24.6);
      ctx.restore();
    }
  }

  drawTruck(t) {
    const ctx = this.ctx;
    const p = iso(t.pos.x, t.pos.y, 0);
    const dir = 1;
    ctx.save();
    ctx.globalAlpha = 0.2;
    ctx.fillStyle = '#101a12';
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 1, 11, 4.6, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    // cargo box
    fillPoly(ctx, [
      { x: p.x - 10, y: p.y - 12 }, { x: p.x + 2, y: p.y - 18 },
      { x: p.x + 2, y: p.y - 4 }, { x: p.x - 10, y: p.y + 2 },
    ], '#e8e2d2');
    fillPoly(ctx, [
      { x: p.x - 10, y: p.y + 2 }, { x: p.x + 2, y: p.y - 4 },
      { x: p.x + 2, y: p.y - 1 }, { x: p.x - 10, y: p.y + 5 },
    ], '#b9b2a2');
    fillPoly(ctx, [
      { x: p.x + 2, y: p.y - 18 }, { x: p.x + 9, y: p.y - 15 },
      { x: p.x + 9, y: p.y - 5 }, { x: p.x + 2, y: p.y - 4 },
    ], '#4f8fbf');
    ctx.fillStyle = '#2b3444';
    ctx.beginPath();
    ctx.arc(p.x - 5, p.y + 1, 2.4, 0, Math.PI * 2);
    ctx.arc(p.x + 5, p.y - 2, 2.4, 0, Math.PI * 2);
    ctx.fill();
  }

  drawSmoke() {
    const ctx = this.ctx;
    for (const s of this.smoke) {
      const t = s.life / s.max;
      const p = iso(s.x + t * s.drift, s.y, s.z);
      ctx.save();
      ctx.globalAlpha = 0.28 * (1 - t);
      ctx.fillStyle = '#dfe4e8';
      ctx.beginPath();
      ctx.arc(p.x, p.y, 2.5 + t * 7, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  drawCoins() {
    const ctx = this.ctx;
    for (const c of this.coins) {
      const t = c.life / c.max;
      const p = iso(c.x, c.y, c.z + Math.sin(t * Math.PI) * 6);
      ctx.save();
      ctx.globalAlpha = 1 - t * t;
      ctx.fillStyle = c.color;
      ctx.beginPath();
      ctx.arc(p.x + Math.sin(c.sway + t * 3) * 2, p.y, 2.6, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = 'rgba(255,255,255,0.7)';
      ctx.lineWidth = 0.8;
      ctx.stroke();
      if (c.amount >= 3 && t < 0.75) {
        ctx.fillStyle = 'rgba(28,32,26,0.75)';
        ctx.font = '600 7px system-ui, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(`${c.sign}${Math.round(c.amount)}`, p.x, p.y - 6 - t * 10);
      }
      ctx.restore();
    }
  }

  drawLighting(world) {
    const ctx = this.ctx;
    const hour = this.hour();
    const night = this.nightAmount(hour);
    const warm = this.duskWarmth(hour);

    if (night > 0.02) {
      ctx.save();
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      ctx.globalCompositeOperation = 'source-over';
      ctx.fillStyle = `rgba(18,28,52,${0.52 * night})`;
      ctx.fillRect(0, 0, this.w, this.h);
      ctx.restore();
    }
    if (warm > 0.02) {
      // warm sunset light pools around the diorama, the sky keeps its own colour
      const c = corners(0, 0, N, N, 0);
      const a = this.worldToScreen(c[0].x, c[0].y);
      const b = this.worldToScreen(c[2].x, c[2].y);
      const cx = (a.x + b.x) / 2;
      const cy = (a.y + b.y) / 2;
      const radius = Math.max(b.x - a.x, b.y - a.y) * 0.85;
      ctx.save();
      ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
      ctx.globalCompositeOperation = 'overlay';
      const g = ctx.createRadialGradient(cx, cy, radius * 0.1, cx, cy, radius);
      g.addColorStop(0, `rgba(255,150,70,${0.42 * warm})`);
      g.addColorStop(0.65, `rgba(255,140,80,${0.2 * warm})`);
      g.addColorStop(1, 'rgba(255,140,80,0)');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, this.w, this.h);
      ctx.restore();
    }
  }

  drawHint() {
    const ctx = this.ctx;
    const a = clamp(this.hint / 2, 0, 1) * 0.85;
    ctx.save();
    ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    ctx.globalAlpha = a;
    ctx.fillStyle = 'rgba(18,22,26,0.55)';
    ctx.font = '500 12px system-ui, sans-serif';
    ctx.textAlign = 'center';
    const text = 'перетаскивание — сдвиг · колесо — масштаб';
    const w = ctx.measureText(text).width + 26;
    ctx.beginPath();
    ctx.roundRect(this.w / 2 - w / 2, this.h - 46, w, 26, 13);
    ctx.fill();
    ctx.fillStyle = '#eee6d6';
    ctx.fillText(text, this.w / 2, this.h - 29);
    ctx.restore();
  }
}
