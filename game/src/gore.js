import { GROUND_Y, VIEW_W, VIEW_H, TAU, rand, randInt, clamp, pick, chance, segPointDist } from './util.js';

const BLOOD = ['#8c0d11', '#7a070b', '#a01317', '#650407', '#960f14'];
const BLOOD_DARK = ['#4d0406', '#5c0508', '#3c0204'];
const MAX_PARTS = 2600;

export class Gore {
  constructor(audio) {
    this.w = VIEW_W;
    this.h = VIEW_H;
    this.audio = audio || null;
    this.amount = 1; // blood volume multiplier

    this.canvas = typeof document !== 'undefined' ? document.createElement('canvas') : null;
    if (this.canvas) {
      this.canvas.width = this.w;
      this.canvas.height = this.h;
      this.ctx = this.canvas.getContext('2d');
      this.ctx.lineCap = 'round';
    } else {
      this.ctx = null;
    }

    this.parts = [];     // blood droplets + mist
    this.chunks = [];    // meat / organs / bone fragments
    this.casings = [];   // spent brass
    this.screen = [];    // blood on the "camera lens"
    this.pools = 0;
    this.decalArea = 0;
    this.splatSoundCd = 0;
    this.budget = 0; // per-frame spawn budget so heavy wounds can't flood the screen
  }

  clear() {
    this.parts.length = 0;
    this.chunks.length = 0;
    this.casings.length = 0;
    this.screen.length = 0;
    this.pools = 0;
    this.decalArea = 0;
    if (this.ctx) this.ctx.clearRect(0, 0, this.w, this.h);
  }

  /* ------------------------------------------------------------------ */
  /*  EMITTERS                                                           */
  /* ------------------------------------------------------------------ */

  // Directional blood spray. dx/dy is the direction of the wound's exit.
  spray(x, y, dx, dy, opts = {}) {
    const n = Math.min(Math.round((opts.count != null ? opts.count : 8) * this.amount), Math.max(0, this.budget));
    this.budget -= n;
    const speed = opts.speed != null ? opts.speed : 160;
    const spread = opts.spread != null ? opts.spread : 0.7;
    const size = opts.size != null ? opts.size : 1.25;
    const baseA = Math.atan2(dy, dx);
    for (let i = 0; i < n; i++) {
      if (this.parts.length >= MAX_PARTS) this.parts.splice(0, 40);
      const a = baseA + rand(-spread, spread);
      const s = speed * rand(0.25, 1.25);
      const mist = chance(opts.mistChance != null ? opts.mistChance : 0.18);
      this.parts.push({
        type: mist ? 'mist' : 'blood',
        x: x + rand(-1, 1), y: y + rand(-1, 1),
        vx: Math.cos(a) * s + rand(-30, 30),
        vy: Math.sin(a) * s + rand(-30, 30) - (opts.lift || 0),
        r: mist ? rand(2, 5) : size * rand(0.55, 1.5),
        life: mist ? rand(0.18, 0.42) : rand(0.6, 2.6),
        maxLife: 1,
        col: pick(BLOOD),
        decal: chance(0.85),
      });
      const p = this.parts[this.parts.length - 1];
      p.maxLife = p.life;
    }
  }

  // Continuous bleeding from a wound (ml/s).
  bleed(x, y, dx, dy, rate, arterial, dt) {
    const per = rate * dt * (arterial ? 0.4 : 0.28) * this.amount;
    let n = per;
    while (n > 0) {
      if (n < 1 && !chance(n)) break;
      n -= 1;
      const speed = arterial ? rand(120, 320) : rand(20, 95);
      const a = Math.atan2(dy, dx) + rand(-0.5, 0.5);
      if (this.parts.length >= MAX_PARTS) this.parts.splice(0, 40);
      this.parts.push({
        type: 'blood',
        x: x + rand(-1.4, 1.4), y: y + rand(-1.4, 1.4),
        vx: Math.cos(a) * speed, vy: Math.sin(a) * speed + (arterial ? rand(-30, 10) : 22),
        r: rand(0.7, arterial ? 1.9 : 1.2),
        life: rand(0.8, 2.4), maxLife: 2.4,
        col: pick(BLOOD), decal: true,
      });
    }
  }

  // Wet meat / organ chunks.
  flesh(x, y, dx, dy, count = 3, power = 1) {
    for (let i = 0; i < count; i++) {
      this.chunks.push({
        type: chance(0.55) ? 'meat' : 'organ',
        x, y,
        vx: dx * rand(20, 140) * power + rand(-60, 60),
        vy: dy * rand(20, 140) * power + rand(-110, -10),
        r: rand(1.5, 4.2),
        rot: rand(0, TAU), vr: rand(-14, 14),
        life: rand(1.2, 3.2), maxLife: 3.2,
        col: chance(0.7) ? '#5e0a0e' : '#320507',
        grounded: false,
      });
    }
  }

  boneShard(x, y, dx, dy, count = 4) {
    for (let i = 0; i < count; i++) {
      this.chunks.push({
        type: 'bone', x, y,
        vx: dx * rand(40, 200) + rand(-90, 90),
        vy: dy * rand(40, 200) + rand(-140, -30),
        r: rand(1, 2.6), rot: rand(0, TAU), vr: rand(-20, 20),
        life: rand(1.5, 3.6), maxLife: 3.6,
        col: chance(0.5) ? '#e6ddc8' : '#c9bca2', grounded: false,
      });
    }
  }

  mist(x, y, dx, dy, n = 6, spread = 0.9, power = 1) {
    for (let i = 0; i < n; i++) {
      const a = Math.atan2(dy, dx) + rand(-spread, spread);
      const s = rand(30, 150) * power;
      this.parts.push({
        type: 'mist', x: x + rand(-2, 2), y: y + rand(-2, 2),
        vx: Math.cos(a) * s, vy: Math.sin(a) * s - rand(0, 25),
        r: rand(2.5, 7), life: rand(0.16, 0.5), maxLife: 0.5,
        col: pick(BLOOD), decal: false,
      });
    }
  }

  dust(x, y, n = 5, col = '#8a8074') {
    for (let i = 0; i < n; i++) {
      this.parts.push({
        type: 'dust', x, y,
        vx: rand(-40, 40), vy: rand(-55, -8),
        r: rand(0.8, 2.4), life: rand(0.2, 0.6), maxLife: 0.6,
        col, decal: false,
      });
    }
  }

  spark(x, y, dx, dy, n = 3) {
    for (let i = 0; i < n; i++) {
      const a = Math.atan2(dy, dx) + rand(-0.8, 0.8);
      const s = rand(80, 260);
      this.parts.push({
        type: 'spark', x, y,
        vx: Math.cos(a) * s, vy: Math.sin(a) * s - 40,
        r: rand(0.6, 1.4), life: rand(0.08, 0.22), maxLife: 0.22,
        col: chance(0.5) ? '#ffd27a' : '#fff3c4', decal: false,
      });
    }
  }

  shell(x, y, vx, vy) {
    this.casings.push({ x, y, px: x, py: y, vx, vy, rot: rand(0, TAU), vr: rand(-24, 24), life: 22 });
    if (this.casings.length > 160) this.casings.shift();
  }

  screenBlood(fromX, fromY, strength = 1) {
    const cx = this.w / 2, cy = this.h / 2;
    const a = Math.atan2(fromY - cy, fromX - cx);
    const n = Math.round(randInt(2, 5) * strength);
    for (let i = 0; i < n; i++) {
      const rad = rand(40, 190);
      const ang = a + rand(-0.9, 0.9);
      this.screen.push({
        x: cx + Math.cos(ang) * rad, y: cy + Math.sin(ang) * rad * 0.7,
        r: rand(1.4, 4.6), vy: rand(4, 26), life: rand(2.5, 6), maxLife: 6,
        col: pick(BLOOD),
      });
    }
  }

  /* ------------------------------------------------------------------ */
  /*  DECALS                                                             */
  /* ------------------------------------------------------------------ */

  splatGround(x, y, r, alpha = 0.5, dirx = 0, diry = 0) {
    if (!this.ctx || r <= 0.15) return;
    // The world can only hold so much blood.
    if (this.decalArea > 62000) return;
    this.decalArea += r * r * 2.4;
    const c = this.ctx;
    c.save();
    c.globalAlpha = clamp(alpha, 0.02, 0.8);
    c.fillStyle = pick(BLOOD);
    const gy = GROUND_Y + rand(0, 10);
    c.beginPath();
    c.ellipse(x, gy, r, r * rand(0.35, 0.6), rand(-0.4, 0.4), 0, TAU);
    c.fill();
    if (r > 2) {
      const n = Math.min(5, (r / 2) | 0);
      for (let i = 0; i < n; i++) {
        c.beginPath();
        const ang = Math.atan2(diry, dirx) + rand(-1.4, 1.4);
        const dd = r * rand(0.8, 2.6);
        c.ellipse(x + Math.cos(ang) * dd, gy + Math.sin(ang) * dd * 0.3, rand(0.4, r * 0.4), rand(0.3, r * 0.25), rand(0, TAU), 0, TAU);
        c.fill();
      }
    }
    c.restore();
    this.pools++;
  }

  // Simple round mark on the decal layer (bullet holes, soot, etc).
  mark(x, y, col = '#241d18', r = 1) {
    if (!this.ctx) return;
    const c = this.ctx;
    c.save();
    c.globalAlpha = 0.45;
    c.fillStyle = col;
    c.beginPath();
    c.arc(x, y, r, 0, TAU);
    c.fill();
    c.restore();
  }

  // Long thin smear (dragging a corpse / sliding).
  smear(x, y, r) {
    if (!this.ctx) return;
    const c = this.ctx;
    c.save();
    c.globalAlpha = 0.22;
    c.fillStyle = pick(BLOOD_DARK);
    c.beginPath();
    c.ellipse(x, GROUND_Y + rand(0, 7), r * rand(1.2, 2.4), r * rand(0.4, 0.8), rand(-0.2, 0.2), 0, TAU);
    c.fill();
    c.restore();
  }

  /* ------------------------------------------------------------------ */
  /*  SIMULATION                                                         */
  /* ------------------------------------------------------------------ */

  update(dt) {
    this.budget = 90;
    const parts = this.parts;
    for (let i = parts.length - 1; i >= 0; i--) {
      const p = parts[i];
      p.life -= dt;
      if (p.life <= 0) { parts.splice(i, 1); continue; }
      if (p.type === 'blood') {
        p.vy += 760 * dt;
        p.vx *= 0.995;
        p.x += p.vx * dt;
        p.y += p.vy * dt;
        if (p.y >= GROUND_Y) {
          if (p.decal && chance(0.55)) this.splatGround(p.x, p.y, clamp(p.r * rand(1.2, 2.4), 0.6, 4), 0.42, p.vx, p.vy);
          parts.splice(i, 1);
          continue;
        }
        if (p.x < -20 || p.x > this.w + 20 || p.y > this.h + 20) { parts.splice(i, 1); continue; }
      } else if (p.type === 'mist') {
        p.vy += 60 * dt;
        p.vx *= 0.94; p.vy *= 0.94;
        p.x += p.vx * dt; p.y += p.vy * dt;
        p.r += dt * 16;
      } else if (p.type === 'dust') {
        p.vy += 90 * dt; p.vx *= 0.93; p.vy *= 0.96;
        p.x += p.vx * dt; p.y += p.vy * dt; p.r += dt * 2.5;
        if (p.y > GROUND_Y) { parts.splice(i, 1); continue; }
      } else if (p.type === 'spark') {
        p.vy += 700 * dt;
        p.x += p.vx * dt; p.y += p.vy * dt;
      }
    }

    // chunks
    for (let i = this.chunks.length - 1; i >= 0; i--) {
      const c = this.chunks[i];
      c.life -= dt;
      if (c.life <= 0) { this.chunks.splice(i, 1); continue; }
      c.vy += 780 * dt;
      c.x += c.vx * dt;
      c.y += c.vy * dt;
      c.rot += c.vr * dt;
      if (c.y > GROUND_Y) {
        c.y = GROUND_Y;
        if (!c.grounded) {
          c.grounded = true;
          c.vx *= 0.4; c.vy *= -0.25; c.vr *= 0.4;
          if (c.type !== 'bone') this.splatGround(c.x, c.y, c.r * rand(1.6, 3), 0.35);
        } else {
          c.vx *= 0.82; c.vy = 0; c.vr *= 0.8;
        }
      }
    }

    // brass casings
    for (let i = this.casings.length - 1; i >= 0; i--) {
      const c = this.casings[i];
      c.life -= dt;
      if (c.life <= 0) { this.casings.splice(i, 1); continue; }
      c.vy += 800 * dt;
      c.x += c.vx * dt; c.y += c.vy * dt; c.rot += c.vr * dt;
      if (c.y > GROUND_Y) {
        c.y = GROUND_Y;
        c.vy *= -0.35; c.vx *= 0.65; c.vr *= 0.6;
        if (Math.abs(c.vy) > 30 && this.audio) this.audio.casing();
      }
    }

    // lens blood
    for (let i = this.screen.length - 1; i >= 0; i--) {
      const s = this.screen[i];
      s.life -= dt;
      s.y += s.vy * dt;
      s.vy *= 0.995;
      if (s.life <= 0) this.screen.splice(i, 1);
    }
  }

  /* ------------------------------------------------------------------ */
  /*  RENDERING                                                          */
  /* ------------------------------------------------------------------ */

  drawDecals(ctx) {
    if (this.canvas) ctx.drawImage(this.canvas, 0, 0);
  }

  draw(ctx) {
    for (const p of this.parts) {
      if (p.type === 'mist') {
        ctx.globalAlpha = clamp(p.life / p.maxLife, 0, 1) * 0.25;
        ctx.fillStyle = p.col;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, TAU);
        ctx.fill();
      } else if (p.type === 'dust') {
        ctx.globalAlpha = clamp(p.life / p.maxLife, 0, 1) * 0.3;
        ctx.fillStyle = p.col;
        ctx.fillRect(p.x | 0, p.y | 0, Math.max(1, p.r * 2 | 0), Math.max(1, p.r * 2 | 0));
      } else {
        ctx.globalAlpha = 1;
        ctx.fillStyle = p.col;
        const s = Math.max(1, Math.round(p.r * 2));
        ctx.fillRect((p.x - s / 2) | 0, (p.y - s / 2) | 0, s, Math.max(1, s - 1));
      }
    }
    ctx.globalAlpha = 1;

    for (const c of this.chunks) {
      ctx.save();
      ctx.translate(c.x, c.y);
      ctx.rotate(c.rot);
      ctx.fillStyle = c.col;
      const w = Math.max(1, c.r * 2), h = Math.max(1, c.r * 1.4);
      if (c.type === 'bone') ctx.fillRect(-w / 2, -h / 2, w, h);
      else {
        ctx.beginPath();
        ctx.ellipse(0, 0, w / 2, h / 2, 0, 0, TAU);
        ctx.fill();
        ctx.fillStyle = 'rgba(0,0,0,0.25)';
        ctx.fillRect(-w / 4, -h / 4, 1, 1);
      }
      ctx.restore();
    }

    for (const c of this.casings) {
      ctx.save();
      ctx.translate(c.x, c.y);
      ctx.rotate(c.rot);
      ctx.fillStyle = '#c9a227';
      ctx.fillRect(-1.5, -0.6, 3, 1.2);
      ctx.fillStyle = '#8a6d14';
      ctx.fillRect(-1.5, -0.6, 1, 1.2);
      ctx.restore();
    }
  }

  drawScreen(ctx) {
    if (!this.screen.length) return;
    for (const s of this.screen) {
      const a = clamp(s.life / s.maxLife, 0, 1);
      ctx.globalAlpha = a * 0.85;
      ctx.fillStyle = s.col;
      ctx.beginPath();
      ctx.ellipse(s.x, s.y, s.r, s.r * 1.25, 0, 0, TAU);
      ctx.fill();
      ctx.globalAlpha = a * 0.35;
      ctx.fillStyle = BLOOD_DARK[0];
      ctx.beginPath();
      ctx.ellipse(s.x, s.y - s.r * 1.9, s.r * 0.35, s.r * 1.9, 0, 0, TAU);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }
}
