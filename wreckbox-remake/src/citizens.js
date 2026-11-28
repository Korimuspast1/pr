// Ragdoll citizens: verlet bodies that walk, panic, burn and get thrown around.
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const rnd = (a = 1, b = 0) => b + Math.random() * (a - b);

const SKIN = ['#e8c39e', '#c98d63', '#8d5a3b', '#f0d2b4', '#a86b45'];
const SHIRT = ['#c94f4f', '#4f7fc9', '#e0c05a', '#5aa06a', '#b06fc0', '#e08a3c', '#3f4f6a', '#d0d0d8'];
const PANTS = ['#2f3a4f', '#3a3a3a', '#4a3a2a', '#2c3f2c', '#5b4a6b'];

export class Citizen {
  constructor(x, y, cell) {
    this.cell = cell;
    this.skin = SKIN[(Math.random() * SKIN.length) | 0];
    this.shirt = SHIRT[(Math.random() * SHIRT.length) | 0];
    this.pants = PANTS[(Math.random() * PANTS.length) | 0];
    this.hp = 100;
    this.alive = true;
    this.conscious = true;
    this.burning = 0;
    this.rad = 0;
    this.dir = Math.random() < 0.5 ? -1 : 1;
    this.speed = rnd(70, 40);
    this.state = 'walk';
    this.timer = rnd(3, 0.5);
    this.panic = 0;
    this.grounded = true;
    this.age = 0;
    this.head = null;

    // verlet skeleton
    const c = cell;
    const p = (x0, y0, r) => ({ x: x0, y: y0, px: x0, py: y0, r });
    this.pts = {
      head: p(x, y - 6.1 * c, 0.9 * c),
      chest: p(x, y - 4.6 * c, 0.85 * c),
      hip: p(x, y - 3.0 * c, 0.85 * c),
      handL: p(x - 0.8 * c, y - 2.2 * c, 0.45 * c),
      handR: p(x + 0.8 * c, y - 2.2 * c, 0.45 * c),
      footL: p(x - 0.55 * c, y - 0.5 * c, 0.55 * c),
      footR: p(x + 0.55 * c, y - 0.5 * c, 0.55 * c),
    };
    this.bones = [
      ['head', 'chest', 1.5 * c],
      ['chest', 'hip', 1.6 * c],
      ['chest', 'handL', 1.5 * c],
      ['chest', 'handR', 1.5 * c],
      ['hip', 'footL', 2.4 * c],
      ['hip', 'footR', 2.4 * c],
    ];
    this.key = Object.keys(this.pts);
  }

  get x() { return this.pts.chest.x; }
  get y() { return this.pts.chest.y; }

  applyImpulse(ix, iy) {
    for (const k of this.key) {
      const p = this.pts[k];
      p.px -= ix;
      p.py -= iy;
    }
  }

  blast(x, y, radius, power) {
    const dx = this.pts.chest.x - x;
    const dy = this.pts.chest.y - y;
    const d = Math.hypot(dx, dy) || 1;
    if (d > radius) return;
    const f = (1 - d / radius) * power;
    const nx = dx / d, ny = dy / d;
    for (const k of this.key) {
      const p = this.pts[k];
      const spread = rnd(0.7, 1.3);
      p.px -= nx * 13 * f * spread;
      p.py -= ny * 13 * f * spread - 3 * f;
    }
    this.damage(f * 48);
    this.panic = 12;
    if (this.hp > 0) this.conscious = Math.random() > 0.25;
  }

  damage(d) {
    if (!this.alive) return;
    this.hp -= d;
    if (this.hp <= 0) {
      this.hp = 0;
      this.alive = false;
      this.conscious = false;
      this.state = 'dead';
    }
  }

  update(dt, world, cell, ctx) {
    if (!this.alive) {
      // corpse: just settle
    }
    this.age += dt;
    this.panic = Math.max(0, this.panic - dt);
    this.timer -= dt;

    const grounded = this.isGrounded(world, cell);
    this.grounded = grounded;

    const speedScale = this.conscious ? 1 : 0.15;
    if (this.conscious && grounded && this.panic <= 0) {
      if (this.timer <= 0) {
        this.state = Math.random() < 0.55 ? 'walk' : 'idle';
        this.dir = Math.random() < 0.5 ? -1 : 1;
        this.timer = rnd(4, 1);
      }
      const move = this.state === 'walk' ? this.speed : 0;
      this.applyWalk(dt, this.dir * move * speedScale);
    } else if (this.conscious && grounded && this.panic > 0) {
      // run away from the closest danger
      let fx = this.dir;
      if (ctx && ctx.dangerX !== undefined) fx = this.x < ctx.dangerX ? -1 : 1;
      this.state = 'run';
      this.applyWalk(dt, fx * this.speed * 2.4);
      this.dir = fx;
    }

    // burning
    if (this.burning > 0) {
      this.burning -= dt;
      this.damage(dt * 26);
      if (ctx && Math.random() < 0.5) ctx.effects.fire(this.x, this.y, 1);
      if (Math.random() < 0.3) this.applyImpulse(rnd(12, -12), rnd(6, -14));
    }

    // radiation
    if (this.rad > 0) {
      this.rad = Math.max(0, this.rad - dt * 0.02);
      this.damage(dt * this.rad * 26);
    }

    this.integrate(dt, world, cell);
  }

  applyWalk(dt, vx) {
    const hip = this.pts.hip;
    const fl = this.pts.footL, fr = this.pts.footR;
    const phase = Math.sin(this.age * 9);
    hip.px -= vx * dt * 0.55;
    this.pts.chest.px -= vx * dt * 0.2;
    fl.px -= (vx + phase * 35) * dt;
    fr.px -= (vx - phase * 35) * dt;
    fl.py -= Math.max(0, phase) * 26 * dt;
    fr.py -= Math.max(0, -phase) * 26 * dt;
    this.pts.handL.px -= (vx * 0.75 - phase * 30) * dt;
    this.pts.handR.px -= (vx * 0.75 + phase * 30) * dt;
  }

  isGrounded(world, cell) {
    const x = Math.floor(this.pts.footL.x / cell);
    const y = Math.floor((this.pts.footL.y + this.pts.footL.r * 0.6) / cell);
    const x2 = Math.floor(this.pts.footR.x / cell);
    return world.solidAt(x, y) || world.solidAt(x2, y);
  }

  integrate(dt, world, cell) {
    const g = 1500;
    const damp = 0.988;
    for (const k of this.key) {
      const p = this.pts[k];
      const vx = (p.x - p.px) * damp;
      const vy = (p.y - p.py) * damp;
      p.px = p.x;
      p.py = p.y;
      p.x += vx;
      p.y += vy + g * dt * dt;
    }
    for (let it = 0; it < 2; it++) {
      for (const [a, b, len] of this.bones) {
        const pa = this.pts[a], pb = this.pts[b];
        const dx = pb.x - pa.x, dy = pb.y - pa.y;
        const d = Math.hypot(dx, dy) || 1;
        const diff = (d - len) / d * 0.5;
        pa.x += dx * diff; pa.y += dy * diff;
        pb.x -= dx * diff; pb.y -= dy * diff;
      }
      // keep the skeleton upright-ish when conscious and standing
      if (this.conscious && this.grounded && this.panic <= 0) {
        const h = this.pts.head, hip = this.pts.hip;
        const dx = h.x - hip.x;
        if (Math.abs(dx) > 4) {
          h.x -= dx * 0.08;
        }
      }
      this.collide(world, cell);
    }
  }

  collide(world, cell) {
    const W = world.w, H = world.h;
    for (const k of this.key) {
      const p = this.pts[k];
      const r = p.r;
      const cx = Math.floor(p.x / cell);
      const cy = Math.floor(p.y / cell);
      if (cx < 0) { p.x = r; p.px = p.x; continue; }
      if (cx >= W) { p.x = W * cell - r; p.px = p.x; continue; }
      if (cy >= H) { p.y = H * cell - r; p.py = p.y; continue; }
      if (cy < 0) continue;
      if (!world.solidAt(cx, cy)) continue;
      // find the shallowest escape direction
      const left = cx * cell, right = (cx + 1) * cell;
      const top = cy * cell, bottom = (cy + 1) * cell;
      const dl = p.x - left + r;
      const dr = right - p.x + r;
      const dt = p.y - top + r;
      const db = bottom - p.y + r;
      const m = Math.min(dl, dr, dt, db);
      const wasFast = Math.abs(p.x - p.px) + Math.abs(p.y - p.py);
      if (m === dt) { p.y = top - r; p.px = p.x - (p.x - p.px) * 0.7; p.py = p.y; }
      else if (m === dl) { p.x = left - r; p.px = p.x; }
      else if (m === dr) { p.x = right + r; p.px = p.x; }
      else { p.y = bottom + r; p.py = p.y; }
      if (wasFast > 34) this.damage(Math.min(60, (wasFast - 30) * 1.6));
    }
  }

  draw(ctx, cam) {
    const c = cam.zoom;
    const P = (p) => ({ x: (p.x - cam.x) * c, y: (p.y - cam.y) * c });
    const head = P(this.pts.head), chest = P(this.pts.chest), hip = P(this.pts.hip);
    const hl = P(this.pts.handL), hr = P(this.pts.handR);
    const fl = P(this.pts.footL), fr = P(this.pts.footR);
    const w = Math.max(1.4, 0.55 * this.cell * c);
    ctx.lineCap = 'round';
    ctx.strokeStyle = this.pants;
    ctx.lineWidth = w * 1.1;
    ctx.beginPath(); ctx.moveTo(hip.x, hip.y); ctx.lineTo(fl.x, fl.y); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(hip.x, hip.y); ctx.lineTo(fr.x, fr.y); ctx.stroke();
    ctx.strokeStyle = this.shirt;
    ctx.lineWidth = w * 1.35;
    ctx.beginPath(); ctx.moveTo(hip.x, hip.y); ctx.lineTo(chest.x, chest.y); ctx.stroke();
    ctx.lineWidth = w * 0.85;
    ctx.beginPath(); ctx.moveTo(chest.x, chest.y); ctx.lineTo(hl.x, hl.y); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(chest.x, chest.y); ctx.lineTo(hr.x, hr.y); ctx.stroke();
    // head
    const hr2 = Math.max(2, 0.9 * this.cell * c);
    ctx.fillStyle = this.skin;
    ctx.beginPath();
    ctx.arc(head.x, head.y, hr2, 0, Math.PI * 2);
    ctx.fill();
    // hair / hat
    ctx.fillStyle = 'rgba(0,0,0,0.45)';
    ctx.beginPath();
    ctx.arc(head.x, head.y - hr2 * 0.35, hr2 * 0.95, Math.PI, Math.PI * 2);
    ctx.fill();
    if (!this.alive) {
      ctx.strokeStyle = 'rgba(120,0,0,0.55)';
      ctx.lineWidth = Math.max(0.8, 0.25 * this.cell * c);
      ctx.beginPath();
      ctx.moveTo(head.x - hr2, head.y + hr2 * 0.2);
      ctx.lineTo(head.x + hr2, head.y + hr2 * 0.2);
      ctx.stroke();
    }
  }
}

export class Citizens {
  constructor(game) {
    this.g = game;
    this.list = [];
  }

  reset() { this.list.length = 0; }

  spawn(x, y) {
    const cell = this.g.cell;
    const c = new Citizen(x * cell + cell / 2, y * cell + cell, cell);
    this.list.push(c);
    return c;
  }

  countAlive() {
    let n = 0;
    for (const c of this.list) if (c.alive) n++;
    return n;
  }

  update(dt) {
    const world = this.g.world;
    const cell = this.g.cell;
    // danger tracking: the nearest fire cell seen recently, else the blast point
    const cx = this.g.dangerX !== undefined ? this.g.dangerX : this.g.centerX();
    const ctx = { effects: this.g.effects, dangerX: cx };
    for (const c of this.list) {
      if (c.x < -cell * 20 || c.y > world.h * cell + 400) continue;
      c.update(dt, world, cell, ctx);
      // catch fire from burning cells
      if (!c.burning) {
        const fx = Math.floor(c.x / cell), fy = Math.floor(c.y / cell);
        for (let dy = -1; dy <= 1 && !c.burning; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            if (world.fire[world.idx(fx + dx, fy + dy)] && Math.random() < 0.02) { c.burning = rnd(4, 1.5); break; }
          }
        }
      }
      if (c.alive && !c.burning) {
        const dx = Math.floor(c.x / cell), dy = Math.floor(c.y / cell);
        if (world.rad[world.idx(dx, dy)] > 0.05) {
          c.rad = Math.max(c.rad, world.rad[world.idx(dx, dy)] * (1 - world.shieldAt(dx, dy) * 0.8));
        }
      }
    }
    // remove bodies that fell off the world
    if (this.list.length > 90) this.list.splice(0, this.list.length - 90);
  }

  draw(ctx, cam) {
    for (const c of this.list) {
      if (c.x < cam.x - 200 || c.x > cam.x + ctx.canvas.width / cam.zoom + 200) continue;
      c.draw(ctx, cam);
    }
  }
}
