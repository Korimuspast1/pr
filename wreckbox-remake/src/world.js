import {
  EMPTY, WATER, AMMO, REACTOR, DEBRIS, ROCK, WIRE, WOOD, SOIL, SAND, GRASS, ASPHALT,
  DENSITY, STRENGTH, HARDNESS, FLAMMABLE, SHELTER, SPAN, TINTS, isSolid,
} from './materials.js';

// World: pixel-cell grid + structural solver + fire + radiation + power grid.
// The solver is intentionally simple but behaves like a real load path:
//   * support is a BFS from the ground (foundation cells)
//   * load is pushed from the top of the map down through supported cells
//   * cells over their bearing capacity crack, lose strength and collapse
//   * unsupported pieces detach and fall cell by cell

export class World {
  constructor(w, h) {
    this.w = w;
    this.h = h;
    const n = (this.n = w * h);
    this.mat = new Uint8Array(n);
    this.dmg = new Uint8Array(n);
    this.tint = new Uint8Array(n);
    this.fire = new Uint8Array(n);
    this.rad = new Float32Array(n);
    this.load = new Float32Array(n);
    this.support = new Uint8Array(n);
    this.span = new Uint8Array(n);
    // 1 = this cell is virgin terrain: it counts as bedrock until you touch it
    this.terrainAnchor = new Uint8Array(n);
    this.falling = new Uint8Array(n);
    this.fallDist = new Uint8Array(n);
    this.vx = new Int8Array(n);
    this.stamp = new Int32Array(n);
    this.stack = new Int32Array(n);
    this.groundY = Math.floor(h * 0.78);
    this.time = 0;
    this.wind = 0.35;
    this.hooks = { destroy: null, blast: null, fire: null, splash: null };
    this.pendingBlasts = [];
    this.brokenCount = 0;
    this.destroyedTotal = 0;
    this.powerNodes = [];
    this.powerLinks = [];
    this.lights = [];
    this.blackout = false;
    this.dirty = true;
    this.fallList = [];
    this.structTimer = 0;
    this.supportStamp = 0;
    this.stats = { solid: 0, falling: 0, fires: 0, fallout: 0 };
    this._rnd = 1234567;
  }

  rnd() {
    this._rnd = (this._rnd * 1664525 + 1013904223) >>> 0;
    return this._rnd / 4294967296;
  }

  // Capture which cells are untouched natural terrain. Those act as bedrock;
  // as soon as the player digs or paints, the anchor is lost and the ground
  // above behaves like loose material.
  captureTerrainAnchors() {
    const { n, mat, terrainAnchor } = this;
    terrainAnchor.fill(0);
    for (let i = 0; i < n; i++) {
      const m = mat[i];
      if (!m) continue;
      if (m === SOIL || m === ROCK || m === SAND || m === GRASS || m === ASPHALT) {
        terrainAnchor[i] = 1;
      }
    }
  }

  clearAnchor(i) { if (this.terrainAnchor[i]) this.terrainAnchor[i] = 0; }

  idx(x, y) { return y * this.w + x; }
  inB(x, y) { return x >= 0 && y >= 0 && x < this.w && y < this.h; }
  at(x, y) { return this.inB(x, y) ? this.mat[y * this.w + x] : 0; }
  solidAt(x, y) { return isSolid(this.at(x, y)); }

  set(x, y, m, dmg = 0) {
    if (!this.inB(x, y)) return;
    const i = y * this.w + x;
    this.clearAnchor(i);
    this.mat[i] = m;
    this.dmg[i] = dmg;
    this.tint[i] = (this.rnd() * TINTS) | 0;
    this.dirty = true;
  }

  rect(x, y, w, h, m, dmg = 0) {
    const x0 = Math.max(0, x | 0), y0 = Math.max(0, y | 0);
    const x1 = Math.min(this.w, (x + w) | 0), y1 = Math.min(this.h, (y + h) | 0);
    for (let yy = y0; yy < y1; yy++) {
      for (let xx = x0; xx < x1; xx++) {
        const i = yy * this.w + xx;
        this.mat[i] = m;
        this.dmg[i] = dmg;
        this.tint[i] = (this.rnd() * TINTS) | 0;
      }
    }
    this.dirty = true;
  }

  line(x0, y0, x1, y1, m, thickness = 1, dmg = 0) {
    const dx = x1 - x0, dy = y1 - y0;
    const steps = Math.max(1, Math.round(Math.hypot(dx, dy)));
    for (let s = 0; s <= steps; s++) {
      const x = Math.round(x0 + (dx * s) / steps);
      const y = Math.round(y0 + (dy * s) / steps);
      if (thickness <= 1) this.set(x, y, m, dmg);
      else this.rect(x - (thickness >> 1), y - (thickness >> 1), thickness, thickness, m, dmg);
    }
  }

  disc(cx, cy, r, m, dmg = 0, hollow = false) {
    const r2 = r * r;
    for (let y = Math.floor(cy - r); y <= Math.ceil(cy + r); y++) {
      for (let x = Math.floor(cx - r); x <= Math.ceil(cx + r); x++) {
        const d2 = (x - cx) ** 2 + (y - cy) ** 2;
        if (d2 > r2) continue;
        if (hollow && d2 < (r - 1.6) ** 2) continue;
        this.set(x, y, m, dmg);
      }
    }
  }

  // ---------------------------------------------------------------- buildings
  stampTower(x, groundY, width, height, opts = {}) {
    const mat = opts.mat ?? 9; // panel
    const floorH = opts.floorH ?? 6;
    const glass = opts.glass ?? 6;
    const frame = opts.frame ?? 3;
    const wallT = opts.wallT ?? 1;
    const floors = Math.max(1, Math.floor(height / floorH));
    const h = floors * floorH;
    // foundation slab
    this.rect(x - 1, groundY - 1, width + 2, 2, 3);
    // side walls
    this.rect(x, groundY - h, wallT, h, mat);
    this.rect(x + width - wallT, groundY - h, wallT, h, mat);
    // floor slabs + windows
    for (let f = 0; f <= floors; f++) {
      const y = groundY - f * floorH;
      this.rect(x, y, width, 1, frame);
      if (f === floors) break;
      // corner columns
      this.rect(x, y - floorH, 1, floorH - 1, mat);
      this.rect(x + width - 1, y - floorH, 1, floorH - 1, mat);
      // window band
      const inner = width - 2;
      if (opts.windows !== false) {
        for (let wx = x + 1; wx < x + width - 1; wx++) {
          const isMullion = (wx - x) % 5 === 0;
          if (isMullion) { this.set(wx, y - 3, frame); this.set(wx, y - 2, frame); continue; }
          this.set(wx, y - 1, glass);
          this.set(wx, y - 2, glass);
          this.set(wx, y - 3, glass);
        }
      } else if (inner > 0) {
        this.rect(x + 1, y - 3, inner, 3, glass);
      }
    }
    // load-bearing interior columns keep the floor slabs short-spanned
    const colStep = opts.colStep ?? 6;
    if (opts.columns !== false) {
      for (let cx = x + colStep; cx < x + width - 1; cx += colStep) {
        this.rect(cx, groundY - h, 1, h, mat);
      }
    }
    // interior partitions on some floors
    if (opts.partitions !== false) {
      for (let f = 1; f < floors; f++) {
        const y = groundY - f * floorH;
        if ((f & 1) === 0) this.rect(x + 2, y - floorH + 1, 1, floorH - 2, 10);
      }
    }
    this.dirty = true;
    return { x, y: groundY - h, w: width, h, floors };
  }

  stampHouse(x, groundY, width, floors, opts = {}) {
    const wall = opts.mat ?? 4;
    const roof = opts.roof ?? 11;
    const floorH = opts.floorH ?? 7;
    const t = 2; // exterior wall thickness (brick / aerated houses)
    const h = floors * floorH;
    this.rect(x - 1, groundY - 1, width + 2, 2, 3);   // footing
    for (let f = 0; f < floors; f++) {
      const y = groundY - f * floorH;
      this.rect(x, y - floorH + 1, t, floorH - 1, wall);              // left wall
      this.rect(x + width - t, y - floorH + 1, t, floorH - 1, wall);  // right wall
      this.rect(x, y, width, 1, 3);                                   // floor slab
      this.rect(x + t, y - floorH + 1, width - t * 2, 1, 7);          // ceiling joists
      // interior room divider
      if (width > 14) this.rect(x + Math.floor(width / 2), y - floorH + 1, 1, floorH - 1, wall);
      // furniture so the interior reads as lived-in
      if (f === 0) {
        this.rect(x + t + 1, y - 1, 5, 1, 7);      // bed
        this.rect(x + width - t - 4, y - 2, 2, 2, 7); // table + chair
      } else {
        this.rect(x + t + 2, y - 2, 3, 2, 7);
        this.rect(x + width - t - 5, y - 1, 4, 1, 7);
      }
    }
    // entrance: a door void in the middle with a concrete frame
    const dx = x + Math.floor(width / 2) - 4;
    this.rect(dx, groundY - 4, 6, 4, EMPTY);
    this.rect(dx - 1, groundY - 4, 1, 4, 3);
    this.rect(dx + 6, groundY - 4, 1, 4, 3);
    this.rect(dx - 1, groundY - 5, 8, 1, 7);
    // pitched roof: timber attic with a slate skin and king posts down to the
    // ceiling, so no part of the roof is a long unsupported cantilever
    const rise = Math.max(2, Math.round(width * 0.24));
    for (let i = 0; i < rise; i++) {
      const yy = groundY - h - i - 1;
      const left = x + i - 1;
      const wRoof = width - i * 2 + 2;
      if (wRoof <= 0) break;
      this.rect(left, yy, wRoof, 1, WOOD);
      this.rect(left, yy, Math.min(2, wRoof), 1, roof);
      if (wRoof > 2) this.rect(left + wRoof - Math.min(2, wRoof), yy, Math.min(2, wRoof), 1, roof);
    }
    for (let px = x + 3; px < x + width - 2; px += 5) {
      this.rect(px, groundY - h - rise + 2, 1, rise, WOOD);
    }
    // ridge beam
    this.rect(x + rise - 2, groundY - h - rise, Math.max(2, width - rise * 2 + 2), 1, roof);
    this.dirty = true;
    return { x, y: groundY - h - rise, w: width, h: h + rise };
  }

  // ----------------------------------------------------------------- damage
  destroy(i, soft = 1) {
    const m = this.mat[i];
    if (!m) return false;
    const x = i % this.w, y = (i / this.w) | 0;
    this.clearAnchor(i);
    this.mat[i] = EMPTY;
    this.dmg[i] = 0;
    this.fire[i] = 0;
    this.rad[i] = 0;
    this.brokenCount++;
    this.destroyedTotal++;
    this.dirty = true;
    if (this.hooks.destroy) this.hooks.destroy(x, y, m, soft);
    if (m === AMMO) {
      this.pendingBlasts.push({ x, y, r: 15 + this.rnd() * 6, power: 2.4, delay: 30 + this.rnd() * 150, type: 'ammo' });
    } else if (m === REACTOR) {
      this.pendingBlasts.push({ x, y, r: 46, power: 3.4, delay: 120, type: 'reactor' });
    }
    return true;
  }

  // Structural failure: the cell loses integrity and becomes loose rubble.
  crumble(i, soft = 0.5) {
    const m = this.mat[i];
    if (!m) return;
    if (m === AMMO || m === REACTOR) { this.destroy(i, soft); return; }
    const x = i % this.w, y = (i / this.w) | 0;
    this.clearAnchor(i);
    this.mat[i] = DEBRIS;
    this.dmg[i] = 0;
    this.fire[i] = 0;
    this.tint[i] = (this.rnd() * TINTS) | 0;
    this.support[i] = 0;
    this.falling[i] = 1;
    this.fallDist[i] = 0;
    this.fallList.push(i);
    this.dirty = true;
    if (this.hooks.crumble) this.hooks.crumble(x, y, m);
  }

  damageCell(x, y, amount) {
    if (!this.inB(x, y)) return 0;
    const i = y * this.w + x;
    const m = this.mat[i];
    if (!m) return 0;
    const hd = HARDNESS[m] || 1;
    const add = amount / Math.max(0.25, hd);
    if (this.dmg[i] + add >= 255) {
      this.destroy(i);
      return 1;
    }
    this.dmg[i] += add;
    this.dirty = true;
    return 0;
  }

  // Destroy cells inside a radius, respecting hardness.
  explode(cx, cy, radius, power = 1, opts = {}) {
    const budget = opts.budget ?? 24000;
    let destroyed = 0;
    const r2 = radius * radius;
    const y0 = Math.max(0, Math.floor(cy - radius));
    const y1 = Math.min(this.h - 1, Math.ceil(cy + radius));
    const x0 = Math.max(0, Math.floor(cx - radius));
    const x1 = Math.min(this.w - 1, Math.ceil(cx + radius));
    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) {
        const dx = x - cx, dy = y - cy;
        const d2 = dx * dx + dy * dy;
        if (d2 > r2) continue;
        const i = y * this.w + x;
        const m = this.mat[i];
        if (!m) continue;
        const d = Math.sqrt(d2);
        const fall = 1 - d / radius;
        const hd = Math.max(0.2, HARDNESS[m]);
        const force = fall * fall * power * 3.6;
        if (d < radius * 0.25 || force > hd * 0.7) {
          this.destroy(i, fall);
          destroyed++;
          if (destroyed > budget) return destroyed;
        } else if (force > hd * 0.25) {
          this.dmg[i] = Math.min(255, this.dmg[i] + (force / hd) * 70);
          if (this.dmg[i] >= 255) { this.destroy(i, fall * 0.5); destroyed++; }
        }
      }
    }
    if (opts.ignite !== false) this.igniteRing(cx, cy, radius * 1.15, opts.fireChance ?? 0.3);
    return destroyed;
  }

  ignite(x, y, chance = 1) {
    if (!this.inB(x, y)) return;
    const i = y * this.w + x;
    const m = this.mat[i];
    if (!m || this.fire[i]) return;
    if (FLAMMABLE[m] <= 0) return;
    if (this.rnd() > chance * FLAMMABLE[m]) return;
    this.fire[i] = 1;
    if (this.hooks.fire) this.hooks.fire(x, y);
  }

  igniteRing(cx, cy, r, chance) {
    const steps = Math.max(12, (r * 3) | 0);
    for (let s = 0; s < steps; s++) {
      const a = this.rnd() * Math.PI * 2;
      const rr = r * (0.4 + this.rnd() * 0.6);
      this.ignite(Math.round(cx + Math.cos(a) * rr), Math.round(cy + Math.sin(a) * rr), chance);
    }
  }

  // Radiation attenuation from materials overhead (fallout shelters).
  shieldAt(x, y) {
    let sum = 0;
    for (let yy = y - 1; yy >= Math.max(0, y - 90); yy--) {
      const m = this.mat[yy * this.w + x];
      if (m) sum += SHELTER[m];
      if (sum > 12) break;
    }
    return Math.exp(-sum / 5.2);
  }

  radAt(x, y) {
    if (!this.inB(x, y)) return 0;
    return this.rad[y * this.w + x];
  }

  addRadiation(cx, cy, r, amount) {
    const r2 = r * r;
    for (let y = Math.max(0, Math.floor(cy - r)); y <= Math.min(this.h - 1, cy + r); y++) {
      for (let x = Math.max(0, Math.floor(cx - r)); x <= Math.min(this.w - 1, cx + r); x++) {
        const d2 = (x - cx) ** 2 + (y - cy) ** 2;
        if (d2 > r2) continue;
        const i = y * this.w + x;
        this.rad[i] = Math.min(1.6, this.rad[i] + amount * (1 - Math.sqrt(d2) / r));
      }
    }
  }

  // ------------------------------------------------------------------ power
  addPowerNode(x, y, plant = false, lightR = 0) {
    const node = { x, y, plant, on: true, lightR };
    this.powerNodes.push(node);
    return node;
  }

  addPowerLink(a, b) { this.powerLinks.push({ a, b, cut: false }); }

  cutPower(cx, cy, r) {
    const r2 = r * r;
    for (const link of this.powerLinks) {
      if (link.cut) continue;
      const a = this.powerNodes[link.a], b = this.powerNodes[link.b];
      const steps = 24;
      let bad = 0;
      for (let s = 0; s <= steps; s++) {
        const x = a.x + ((b.x - a.x) * s) / steps;
        const y = a.y + ((b.y - a.y) * s) / steps;
        if ((x - cx) ** 2 + (y - cy) ** 2 < r2) { bad++; }
      }
      if (bad > 3) link.cut = true;
    }
    for (const node of this.powerNodes) {
      if (node.plant) continue;
      if ((node.x - cx) ** 2 + (node.y - cy) ** 2 < r2) node.on = false;
    }
    this.solvePower();
  }

  solvePower() {
    const nodes = this.powerNodes;
    const seen = new Uint8Array(nodes.length);
    const q = [];
    for (let i = 0; i < nodes.length; i++) if (nodes[i].plant && nodes[i].on) { seen[i] = 1; q.push(i); }
    const adj = nodes.map(() => []);
    for (const l of this.powerLinks) {
      if (l.cut) continue;
      adj[l.a].push(l.b); adj[l.b].push(l.a);
    }
    for (let qi = 0; qi < q.length; qi++) {
      const v = q[qi];
      for (const nb of adj[v]) if (!seen[nb] && nodes[nb].on) { seen[nb] = 1; q.push(nb); }
    }
    let anyOff = false;
    for (let i = 0; i < nodes.length; i++) {
      if (!nodes[i].plant) { nodes[i].on = !!seen[i] && nodes[i].on; if (!nodes[i].on) anyOff = true; }
    }
    this.blackout = anyOff && q.length === 0;
  }

  // ------------------------------------------------------------------ solver
  recomputeSupport() {
    const { w, h, mat, support, stack } = this;
    support.fill(0);
    let sp = 0;
    const bottom = h - 1;
    for (let x = 0; x < w; x++) {
      const i = bottom * w + x;
      if (isSolid(mat[i])) { support[i] = 1; stack[sp++] = i; }
    }
    // untouched natural terrain acts as bedrock
    const anch = this.terrainAnchor;
    for (let i = 0; i < this.n; i++) {
      if (anch[i] && mat[i] && !support[i]) { support[i] = 1; stack[sp++] = i; }
    }
    let falling = 0;
    while (sp > 0) {
      const i = stack[--sp];
      const x = i % w, y = (i / w) | 0;
      for (let dy = -1; dy <= 1; dy++) {
        const ny = y + dy;
        if (ny < 0 || ny >= h) continue;
        for (let dx = -1; dx <= 1; dx++) {
          if (!dx && !dy) continue;
          const nx = x + dx;
          if (nx < 0 || nx >= w) continue;
          const j = ny * w + nx;
          if (support[j]) continue;
          if (!isSolid(mat[j])) continue;
          support[j] = 1;
          stack[sp++] = j;
        }
      }
    }
    this.fallList.length = 0;
    let solid = 0;
    for (let i = 0; i < this.n; i++) {
      const m = mat[i];
      if (!m) { this.falling[i] = 0; continue; }
      solid++;
      if (!support[i] && m !== WATER) {
        this.falling[i] = 1;
        this.fallList.push(i);
        falling++;
      } else this.falling[i] = 0;
    }
    this.stats.solid = solid;
    this.stats.falling = falling;
    this.dirty = true;
  }

  // Horizontal reach (in lateral steps) of every supported cell from the ground.
  // Rows are solved bottom-up: resting on the cell below is free, resting on a
  // diagonal or reaching across the row costs lateral steps. This is what makes
  // an undercut wall snap off instead of hanging in the air.
  computeSpan() {
    const { w, h, mat, support, span } = this;
    span.fill(255);
    for (let x = 0; x < w; x++) {
      const i = (h - 1) * w + x;
      if (isSolid(mat[i])) span[i] = 0;
      const j = (h - 2) * w + x;
      if (mat[j] === ROCK) span[j] = 0;
    }
    // untouched natural terrain is a valid load path (span 0)
    const anchor = this.terrainAnchor;
    for (let i = 0; i < this.n; i++) {
      if (anchor[i] && mat[i]) span[i] = 0;
    }
    for (let y = h - 2; y >= 0; y--) {
      const row = y * w, bel = row + w;
      for (let x = 0; x < w; x++) {
        const i = row + x;
        if (!isSolid(mat[i]) || anchor[i]) continue;
        let best = 255;
        if (isSolid(mat[bel + x])) best = span[bel + x];
        if (x > 0 && isSolid(mat[bel + x - 1])) best = Math.min(best, span[bel + x - 1] + 1);
        if (x < w - 1 && isSolid(mat[bel + x + 1])) best = Math.min(best, span[bel + x + 1] + 1);
        span[i] = best;
      }
      // lateral relaxation across the row (beam action)
      for (let x = 1; x < w; x++) {
        const i = row + x;
        if (!isSolid(mat[i]) || anchor[i]) continue;
        const j = row + x - 1;
        if (isSolid(mat[j]) && span[j] + 1 < span[i]) span[i] = span[j] + 1;
      }
      for (let x = w - 2; x >= 0; x--) {
        const i = row + x;
        if (!isSolid(mat[i]) || anchor[i]) continue;
        const j = row + x + 1;
        if (isSolid(mat[j]) && span[j] + 1 < span[i]) span[i] = span[j] + 1;
      }
    }
  }

  solveLoad() {
    const { w, h, mat, support, load, dmg } = this;
    load.fill(0);
    const toBreak = [];
    for (let y = 0; y < h - 1; y++) {
      const row = y * w;
      const below = row + w;
      for (let x = 0; x < w; x++) {
        const i = row + x;
        if (!support[i]) continue;
        const m = mat[i];
        if (!m || m === DEBRIS || m === WIRE) continue;
        const l = load[i] + DENSITY[m];
        if (load[i] > 4000) load[i] = 4000;
        const d = dmg[i] / 255;
        const cap = STRENGTH[m] * (1 - d * 0.62);
        const stress = l * 2;
        if (stress > cap) {
          const over = (stress - cap) / Math.max(1, cap);
          const add = 28 + over * 110;
          if (dmg[i] + add >= 255) { toBreak.push(i); continue; }
          dmg[i] += add;
          this.dirty = true;
        }
        // hand the pressure down when something carries it, otherwise push it
        // sideways along the beam towards the nearest wall/column
        if (mat[below + x] && support[below + x]) {
          load[below + x] += l;
        } else {
          const lft = x > 0 && mat[i - 1] && support[i - 1];
          const rgt = x < w - 1 && mat[i + 1] && support[i + 1];
          if (lft && rgt) { load[i - 1] += l * 0.5; load[i + 1] += l * 0.5; }
          else if (lft) load[i - 1] += l * 0.8;
          else if (rgt) load[i + 1] += l * 0.8;
        }
      }
    }
    for (const i of toBreak) this.crumble(i);
    this.brokenCount += toBreak.length;
    return toBreak.length;
  }

  // Cantilever check: a cell too far (laterally) from a load path snaps off.
  applySpanStress() {
    const { w, h, mat, support, span, dmg, load } = this;
    let snapped = 0;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        const m = mat[i];
        if (!m || !support[i] || m === DEBRIS || m === WIRE || m === GRASS) continue;
        if (this.terrainAnchor[i]) continue;
        const c = span[i];
        if (c === 255) {
          // hanging from its neighbours without a load path to the ground
          this.crumble(i);
          snapped++;
          continue;
        }
        if (c < 2) continue;
        let limit = SPAN[m];
        if (!limit) { limit = 2; }
        // cracked sections lose strength, so a weakened beam reaches less far
        limit *= 1 - (dmg[i] / 255) * 0.55;
        if (c > limit) {
          const over = c - limit;
          dmg[i] = Math.min(255, dmg[i] + 18 + over * 6);
          if (dmg[i] >= 250) {
            this.crumble(i);
            snapped++;
          }
          this.dirty = true;
        }
      }
    }
    return snapped;
  }

  structuralStep() {
    this.recomputeSupport();
    this.computeSpan();
    this.solveLoad();
    this.applySpanStress();
    this.solvePower();
    this.supportStamp++;
    this.dirty = true;
  }

  // ------------------------------------------------------------------- fire
  stepFire(dt) {
    const { w, h, mat, fire, dmg } = this;
    let fires = 0;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const i = y * w + x;
        if (!fire[i]) continue;
        fires++;
        const m = mat[i];
        if (!m) { fire[i] = 0; continue; }
        if (fire[i] === 1) {
          if (this.hooks.fire) this.hooks.fire(x, y);
          fire[i] = 18 + ((this.rnd() * 10) | 0);
          this.dmg[i] = Math.min(255, this.dmg[i] + 12);
        }
        fire[i]--;
        if (this.dmg[i] >= 255) { this.destroy(i, 0.2); continue; }
        // spread to neighbours while the fire is strong
        if (fire[i] > 8 && this.rnd() < 0.28) {
          for (let k = 0; k < 3; k++) {
            const nx = x + ((this.rnd() * 3) | 0) - 1;
            const ny = y + ((this.rnd() * 3) | 0) - 1;
            this.ignite(nx, ny, 0.8);
          }
        }
        if (fire[i] > 4 && this.rnd() < 0.35 && this.hooks.fire) this.hooks.fire(x, y);
      }
    }
    this.stats.fires = fires;
  }

  stepRadiation() {
    const rad = this.rad;
    let count = 0;
    for (let i = 0; i < this.n; i++) {
      if (rad[i] > 0.0005) { rad[i] *= 0.9992; count++; }
      else if (rad[i]) rad[i] = 0;
    }
    this.stats.fallout = count;
  }

  // ----------------------------------------------------------------- gravity
  stepFalling() {
    const { w, h, mat, support } = this;
    const list = this.fallList;
    if (!list.length) return;
    // bottom-up so stacks fall together
    list.sort((a, b) => b - a);
    let moved = 0;
    for (let idx = 0; idx < list.length; idx++) {
      const i = list[idx];
      if (!mat[i]) continue;
      const x = i % w, y = (i / w) | 0;
      if (y >= h - 1) { support[i] = 1; continue; }
      const j = i + w;
      if (!mat[j] || mat[j] === WATER) {
        this.moveCell(i, j);
        moved++;
      } else {
        const dir = this.rnd() < 0.5 ? 1 : -1;
        const x2 = x + dir;
        if (x2 >= 0 && x2 < w && !mat[j + dir]) { this.moveCell(i, j + dir); moved++; }
        else if (x2 - dir * 2 >= 0 && x2 - dir * 2 < w && !mat[j - dir]) { this.moveCell(i, j - dir); moved++; }
        else {
          // jammed: crack slowly so piles flatten out
          if (this.dmg[i] < 250) this.dmg[i] = Math.min(255, this.dmg[i] + 0.9);
          else this.destroy(i, 0.3);
          support[i] = 1;
        }
      }
    }
    for (const b of this.pendingBlasts) {
      // nothing
    }
    if (moved) this.dirty = true;
  }

  moveCell(i, j) {
    this.mat[j] = this.mat[i];
    this.dmg[j] = this.dmg[i];
    this.tint[j] = this.tint[i];
    this.fire[j] = this.fire[i];
    this.fallDist[j] = Math.min(255, this.fallDist[i] + 1);
    this.mat[i] = EMPTY;
    this.dmg[i] = 0;
    this.fire[i] = 0;
    this.fallDist[i] = 0;
  }

  // ------------------------------------------------------------------- tick
  update(dt, time) {
    this.time = time;
    this.structTimer += dt;
    let didStruct = false;
    if (this.structTimer >= 0.1) {
      this.structTimer = 0;
      this.structuralStep();
      didStruct = true;
    }
    this.stepFalling();
    if (didStruct) this.stepFire(dt * 3);
    this.stepRadiation();
    // pending chain blasts
    for (let i = this.pendingBlasts.length - 1; i >= 0; i--) {
      const b = this.pendingBlasts[i];
      b.delay -= dt * 1000;
      if (b.delay <= 0) {
        this.pendingBlasts.splice(i, 1);
        const n = this.explode(b.x, b.y, b.r, b.power, { fireChance: 0.9 });
        if (this.hooks.blast) this.hooks.blast(b.x, b.y, b.r, b.power, b.type, n);
        if (b.type === 'reactor') {
          this.addRadiation(b.x, b.y, b.r * 1.8, 1.2);
          this.rad[b.y * this.w + b.x] = 1.6;
        }
      }
    }
  }

  // Remove small unsupported islands left behind by hand-authored geometry.
  // (Anything a player builds goes through the normal collapse rules.)
  cleanUnsupported(maxSize = 400) {
    this.recomputeSupport();
    const { w, mat, support } = this;
    const seen = new Uint8Array(this.n);
    let removed = 0;
    for (let i = 0; i < this.n; i++) {
      if (!mat[i] || support[i] || seen[i]) continue;
      const q = [i];
      seen[i] = 1;
      const island = [];
      while (q.length) {
        const j = q.pop();
        island.push(j);
        const x = j % w, y = (j / w) | 0;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= this.h) continue;
            const jj = ny * w + nx;
            if (seen[jj] || !mat[jj] || support[jj]) continue;
            seen[jj] = 1;
            q.push(jj);
          }
        }
        if (island.length > maxSize) break;
      }
      if (island.length <= maxSize) {
        for (const j of island) { this.mat[j] = EMPTY; this.dmg[j] = 0; removed++; }
      }
    }
    this.dirty = true;
    return removed;
  }

  // helper: settle all unsupported cells instantly (used on map load)
  settle() {
    for (let k = 0; k < 40; k++) {
      this.recomputeSupport();
      if (!this.fallList.length) break;
      this.stepFalling();
    }
    this.recomputeSupport();
    this.computeSpan();
    this.solveLoad();
  }
}
