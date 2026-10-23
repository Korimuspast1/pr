import { GROUND_Y, VIEW_W, VIEW_H, RENDER_SCALE, TAU, rand, randInt, clamp, pick, chance } from './util.js';

const BLOOD = ['#7c0c12', '#67070c', '#8d1017', '#56040a', '#720a10', '#941218'];
const BLOOD_DARK = ['#430407', '#4f060a', '#340204'];
const MAX_PARTS = 1800;

// Blood, flesh, brass and decals. Everything is drawn with smooth shapes.
export class Gore {
  constructor(audio) {
    this.w = VIEW_W;
    this.h = VIEW_H;
    this.audio = audio || null;
    this.amount = 1;

    this.canvas = typeof document !== 'undefined' ? document.createElement('canvas') : null;
    if (this.canvas) {
      this.canvas.width = VIEW_W * RENDER_SCALE;
      this.canvas.height = VIEW_H * RENDER_SCALE;
      this.ctx = this.canvas.getContext('2d');
      this.ctx.setTransform(RENDER_SCALE, 0, 0, RENDER_SCALE, 0, 0);
      this.ctx.lineCap = 'round';
      this.ctx.lineJoin = 'round';
    } else {
      this.ctx = null;
    }

    this.parts = [];
    this.chunks = [];
    this.casings = [];
    this.screen = [];
    this.pools = 0;
    this.decalArea = 0;
    this.budget = 0;
  }

  clear() {
    this.parts.length = 0;
    this.chunks.length = 0;
    this.casings.length = 0;
    this.pools = 0;
    this.decalArea = 0;
    if (this.ctx) this.ctx.clearRect(0, 0, this.w, this.h);
  }

  /* ---------------- emitters ---------------- */

  spray(x, y, dx, dy, opts = {}) {
    const n = Math.min(Math.round((opts.count != null ? opts.count : 8) * this.amount), Math.max(0, this.budget));
    this.budget -= n;
    const speed = opts.speed != null ? opts.speed : 160;
    const spread = opts.spread != null ? opts.spread : 0.7;
    const size = opts.size != null ? opts.size : 1.15;
    const baseA = Math.atan2(dy, dx);
    for (let i = 0; i < n; i++) {
      if (this.parts.length >= MAX_PARTS) this.parts.splice(0, 40);
      const a = baseA + rand(-spread, spread);
      const s = speed * rand(0.25, 1.25);
      const mist = chance(opts.mistChance != null ? opts.mistChance : 0.18);
      const p = {
        type: mist ? 'mist' : 'blood',
        x: x + rand(-1, 1), y: y + rand(-1, 1),
        vx: Math.cos(a) * s + rand(-24, 24),
        vy: Math.sin(a) * s + rand(-24, 24) - (opts.lift || 0),
        r: mist ? rand(1.8, 4.4) : size * rand(0.55, 1.45),
        life: mist ? rand(0.18, 0.42) : rand(0.6, 2.6),
        maxLife: 1,
        col: pick(BLOOD),
      };
      p.maxLife = p.life;
      this.parts.push(p);
    }
  }

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
        r: rand(0.55, arterial ? 1.7 : 1.05),
        life: rand(0.8, 2.4), maxLife: 2.4,
        col: pick(BLOOD),
      });
    }
  }

  flesh(x, y, dx, dy, count = 3, power = 1) {
    for (let i = 0; i < count; i++) {
      this.chunks.push({
        type: chance(0.55) ? 'meat' : 'organ',
        x, y,
        vx: dx * rand(20, 140) * power + rand(-60, 60),
        vy: dy * rand(20, 140) * power + rand(-110, -10),
        r: rand(1.3, 3.8),
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
        r: rand(0.9, 2.2), rot: rand(0, TAU), vr: rand(-20, 20),
        life: rand(1.5, 3.6), maxLife: 3.6,
        col: chance(0.5) ? '#efe7d4' : '#d3c6ae', grounded: false,
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
        r: rand(2.2, 6), life: rand(0.16, 0.5), maxLife: 0.5,
        col: pick(BLOOD),
      });
    }
  }

  dust(x, y, n = 5, col = '#8a8074') {
    for (let i = 0; i < n; i++) {
      this.parts.push({
        type: 'dust', x, y,
        vx: rand(-40, 40), vy: rand(-55, -8),
        r: rand(0.8, 2.2), life: rand(0.2, 0.6), maxLife: 0.6,
        col,
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
        r: rand(0.5, 1.1), life: rand(0.08, 0.22), maxLife: 0.22,
        col: chance(0.5) ? '#ffd27a' : '#fff3c4',
      });
    }
  }

  shell(x, y, vx, vy) {
    this.casings.push({ x, y, vx, vy, rot: rand(0, TAU), vr: rand(-24, 24), life: 26 });
    if (this.casings.length > 120) this.casings.shift();
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

  /* ---------------- decals ---------------- */

  decalBust() {
    if (!this.ctx) return;
    if (this.decalArea > 70000) return false;
    return true;
  }

  splatGround(x, y, r, alpha = 0.5, dirx = 0, diry = 0) {
    if (!this.ctx || r <= 0.15 || this.decalArea > 70000) return;
    const c = this.ctx;
    this.decalArea += r * r * 2.2;
    const gy = GROUND_Y + rand(0, 12);
    c.save();
    c.globalAlpha = clamp(alpha, 0.02, 0.62);
    c.fillStyle = r > 2.6 ? pick(BLOOD_DARK) : pick(BLOOD);
    // main blob
    c.beginPath();
    c.ellipse(x, gy, r, r * rand(0.34, 0.56), rand(-0.5, 0.5), 0, TAU);
    c.fill();
    // lobes for an organic silhouette
    const lobes = 2 + Math.min(4, Math.floor(r / 2));
    for (let i = 0; i < lobes; i++) {
      const ang = rand(0, TAU);
      const dd = r * rand(0.6, 1.7);
      c.beginPath();
      c.ellipse(x + Math.cos(ang) * dd, gy + Math.sin(ang) * dd * 0.35,
        rand(0.5, r * 0.55), rand(0.4, r * 0.32), rand(0, TAU), 0, TAU);
      c.fill();
    }
    // directional satellites / spray
    if (r > 1.6) {
      const dirAng = Math.atan2(diry, dirx);
      for (let i = 0; i < Math.min(8, 2 + r); i++) {
        const ang = dirAng + rand(-1.2, 1.2);
        const dd = r * rand(1.2, 3.6);
        const sx = x + Math.cos(ang) * dd;
        const sy = gy + Math.sin(ang) * dd * 0.3;
        c.beginPath();
        c.ellipse(sx, sy, rand(0.35, Math.max(0.6, r * 0.3)), rand(0.3, Math.max(0.5, r * 0.22)), rand(0, TAU), 0, TAU);
        c.fill();
      }
    }
    c.restore();
    this.pools++;
  }

  smear(x, y, r) {
    if (!this.ctx) return;
    const c = this.ctx;
    c.save();
    c.globalAlpha = 0.2;
    c.fillStyle = pick(BLOOD_DARK);
    c.beginPath();
    c.ellipse(x, GROUND_Y + rand(0, 7), r * rand(1.2, 2.4), r * rand(0.4, 0.8), rand(-0.2, 0.2), 0, TAU);
    c.fill();
    c.restore();
  }

  mark(x, y, col = '#241d18', r = 1) {
    if (!this.ctx) return;
    const c = this.ctx;
    c.save();
    c.globalAlpha = 0.4;
    c.fillStyle = col;
    c.beginPath();
    c.arc(x, y, r, 0, TAU);
    c.fill();
    c.restore();
  }

  /* ---------------- simulation ---------------- */

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
          if (chance(0.5)) this.splatGround(p.x, p.y, clamp(p.r * rand(1.1, 2.2), 0.5, 3.4), 0.45, p.vx, p.vy);
          parts.splice(i, 1);
          continue;
        }
        if (p.x < -20 || p.x > this.w + 20 || p.y > this.h + 20) { parts.splice(i, 1); continue; }
      } else if (p.type === 'mist') {
        p.vy += 60 * dt;
        p.vx *= 0.94; p.vy *= 0.94;
        p.x += p.vx * dt; p.y += p.vy * dt;
        p.r += dt * 14;
      } else if (p.type === 'dust') {
        p.vy += 90 * dt; p.vx *= 0.93; p.vy *= 0.96;
        p.x += p.vx * dt; p.y += p.vy * dt; p.r += dt * 2.2;
        if (p.y > GROUND_Y) { parts.splice(i, 1); continue; }
      } else if (p.type === 'spark') {
        p.vy += 700 * dt;
        p.x += p.vx * dt; p.y += p.vy * dt;
      }
    }

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
          if (c.type !== 'bone') this.splatGround(c.x, c.y, c.r * rand(1.6, 2.8), 0.35);
        } else {
          c.vx *= 0.82; c.vy = 0; c.vr *= 0.8;
        }
      }
    }

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

    for (let i = this.screen.length - 1; i >= 0; i--) {
      const s = this.screen[i];
      s.life -= dt;
      s.y += s.vy * dt;
      s.vy *= 0.995;
      if (s.life <= 0) this.screen.splice(i, 1);
    }
  }

  /* ---------------- drawing ---------------- */

  drawDecals(ctx) {
    if (this.canvas) ctx.drawImage(this.canvas, 0, 0, this.w, this.h);
  }

  draw(ctx) {
    for (const p of this.parts) {
      if (p.type === 'spark') {
        ctx.globalAlpha = 1;
        ctx.fillStyle = p.col;
        ctx.beginPath();
        ctx.arc(p.x, p.y, Math.max(0.4, p.r), 0, TAU);
        ctx.fill();
        continue;
      }
      if (p.type === 'mist') {
        const a = clamp(p.life / p.maxLife, 0, 1) * 0.22;
        ctx.globalAlpha = a;
        ctx.fillStyle = p.col;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, TAU);
        ctx.fill();
        continue;
      }
      if (p.type === 'dust') {
        ctx.globalAlpha = clamp(p.life / p.maxLife, 0, 1) * 0.3;
        ctx.fillStyle = p.col;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, TAU);
        ctx.fill();
        continue;
      }
      // blood: a flying droplet stretched along its velocity
      const sp = Math.hypot(p.vx, p.vy);
      const stretch = 1 + Math.min(2.6, sp / 260);
      const ang = Math.atan2(p.vy, p.vx);
      ctx.globalAlpha = 1;
      ctx.fillStyle = p.col;
      ctx.beginPath();
      ctx.ellipse(p.x, p.y, p.r * stretch, p.r, ang, 0, TAU);
      ctx.fill();
      if (p.r > 1.7) {
        ctx.globalAlpha = 0.35;
        ctx.fillStyle = '#e8b7b7';
        ctx.beginPath();
        ctx.arc(p.x - Math.cos(ang) * p.r * 0.3, p.y - Math.sin(ang) * p.r * 0.3, p.r * 0.35, 0, TAU);
        ctx.fill();
      }
    }
    ctx.globalAlpha = 1;

    for (const c of this.chunks) {
      ctx.save();
      ctx.translate(c.x, c.y);
      ctx.rotate(c.rot);
      ctx.fillStyle = c.col;
      ctx.beginPath();
      if (c.type === 'bone') ctx.rect(-c.r, -c.r * 0.45, c.r * 2, c.r * 0.9);
      else ctx.ellipse(0, 0, c.r, c.r * 0.72, 0, 0, TAU);
      ctx.fill();
      if (c.type !== 'bone') {
        ctx.fillStyle = 'rgba(0,0,0,0.28)';
        ctx.beginPath();
        ctx.ellipse(c.r * 0.2, c.r * 0.15, c.r * 0.4, c.r * 0.28, 0, 0, TAU);
        ctx.fill();
      }
      ctx.restore();
    }

    for (const c of this.casings) {
      ctx.save();
      ctx.translate(c.x, c.y);
      ctx.rotate(c.rot);
      ctx.fillStyle = '#c9a227';
      ctx.fillRect(-1.6, -0.55, 3.2, 1.1);
      ctx.fillStyle = '#8a6d14';
      ctx.fillRect(-1.6, -0.55, 0.9, 1.1);
      ctx.restore();
    }
  }

  drawScreen(ctx) {
    if (!this.screen.length) return;
    for (const s of this.screen) {
      const a = clamp(s.life / s.maxLife, 0, 1);
      ctx.globalAlpha = a * 0.8;
      ctx.fillStyle = s.col;
      ctx.beginPath();
      ctx.ellipse(s.x, s.y, s.r, s.r * 1.3, 0, 0, TAU);
      ctx.fill();
      ctx.globalAlpha = a * 0.3;
      ctx.fillStyle = BLOOD_DARK[0];
      ctx.beginPath();
      ctx.ellipse(s.x, s.y - s.r * 1.9, s.r * 0.32, s.r * 1.9, 0, 0, TAU);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }
}
