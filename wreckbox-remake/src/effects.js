// Visual effects: particles, shockwaves, fire, nuke flash & shadows, weather.
const rnd = (a = 1, b = 0) => b + Math.random() * (a - b);
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

export class Effects {
  constructor(game) {
    this.g = game;
    this.particles = [];
    this.rings = [];
    this.flashes = [];
    this.bolts = [];
    this.shocks = [];
    this.shadowMask = null;
    this.shadowTimer = 0;
    this.maxParticles = 4200;
    this.budget = 260;
    this.dustClouds = [];
    this.meteors = [];
    this.tornadoes = [];
    this.blackholes = [];
    this.tsunami = null;
    this.shakeAmp = 0;
    this.shakeTime = 0;
    this.flashAlpha = 0;
    this.flashColor = '#fff';
    this.sprites = null;
  }

  // Pre-rendered sprites: per-particle gradients are far too slow at 60 fps.
  makeSprites() {
    const mk = (size, stops) => {
      const cv = document.createElement('canvas');
      cv.width = cv.height = size;
      const c = cv.getContext('2d');
      const g = c.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
      for (const [pos, col] of stops) g.addColorStop(pos, col);
      c.fillStyle = g;
      c.beginPath();
      c.arc(size / 2, size / 2, size / 2, 0, Math.PI * 2);
      c.fill();
      return cv;
    };
    this.sprites = {
      flame: mk(64, [[0, 'rgba(255,246,205,1)'], [0.35, 'rgba(255,174,64,0.95)'], [0.7, 'rgba(200,70,10,0.45)'], [1, 'rgba(110,20,0,0)']]),
      smoke: mk(64, [[0, 'rgba(255,255,255,0.55)'], [0.55, 'rgba(255,255,255,0.32)'], [1, 'rgba(255,255,255,0)']]),
      glow: mk(64, [[0, 'rgba(255,255,255,1)'], [0.5, 'rgba(255,255,255,0.35)'], [1, 'rgba(255,255,255,0)']]),
    };
  }

  reset() {
    this.particles.length = 0;
    this.rings.length = 0;
    this.flashes.length = 0;
    this.bolts.length = 0;
    this.shocks.length = 0;
    this.meteors.length = 0;
    this.tornadoes.length = 0;
    this.blackholes.length = 0;
    this.tsunami = null;
    this.shakeAmp = 0;
    this.flashAlpha = 0;
    this.shadowMask = null;
  }

  add(p) {
    const list = this.particles;
    // per-frame spawn budget keeps mass destruction from tanking the framerate
    if (this.budget <= 0 && !p.mushroom) return;
    this.budget--;
    if (list.length >= this.maxParticles) {
      // drop the oldest throwaway particle, keep the big smoke columns
      let victim = -1;
      for (let i = 0; i < list.length; i += 3) {
        const t = list[i].type;
        if (t === 'chip' || t === 'spark' || t === 'water') { victim = i; break; }
      }
      if (victim < 0) list.shift(); else list.splice(victim, 1);
    }
    list.push(p);
  }

  shake(amp, time = 0.5) {
    this.shakeAmp = Math.max(this.shakeAmp, amp);
    this.shakeTime = Math.max(this.shakeTime, time);
  }

  flash(alpha, color = '#ffffff', decay = 3) {
    this.flashes.push({ a: alpha, color, decay });
  }

  // -------------------------------------------------------------- particle kit
  smoke(x, y, n = 6, power = 1, dark = false) {
    for (let i = 0; i < n; i++) {
      this.add({
        type: 'smoke', x: x + rnd(4, -4), y: y + rnd(4, -4),
        vx: rnd(40, -40) * power, vy: rnd(-10, -50) * power,
        life: rnd(3.2, 1.2) * power, max: 3.4, size: rnd(14, 5) * power,
        color: dark ? '#2a2a2c' : '#9a948c', grow: rnd(26, 10), drag: 0.985,
      });
    }
  }

  dust(x, y, n = 8, power = 1) {
    for (let i = 0; i < n; i++) {
      this.add({
        type: 'dust', x: x + rnd(10, -10), y: y + rnd(6, -6),
        vx: rnd(70, -70) * power, vy: rnd(20, -60) * power,
        life: rnd(1.6, 0.5), max: 1.8, size: rnd(7, 2) * power,
        color: '#c8bda9', grow: rnd(10, 2), drag: 0.94, grav: 60,
      });
    }
  }

  sparks(x, y, n = 10, power = 1, color = '#ffd27a') {
    for (let i = 0; i < n; i++) {
      const a = rnd(Math.PI * 2), s = rnd(420, 60) * power;
      this.add({
        type: 'spark', x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s - 40,
        life: rnd(0.9, 0.25), max: 1, size: rnd(2.4, 0.8), color,
        drag: 0.975, grav: 520, glow: true,
      });
    }
  }

  chips(x, y, n = 8, color = '#9c5a44', power = 1) {
    for (let i = 0; i < n; i++) {
      const a = rnd(Math.PI * 2), s = rnd(260, 40) * power;
      this.add({
        type: 'chip', x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s - 60,
        life: rnd(1.8, 0.6), max: 2, size: rnd(2.2, 1), color,
        drag: 0.99, grav: 520, rot: rnd(6), spin: rnd(14, -14),
      });
    }
  }

  glassBurst(x, y, n = 10) {
    for (let i = 0; i < n; i++) {
      const a = rnd(Math.PI * 2), s = rnd(320, 60);
      this.add({
        type: 'chip', x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s - 60,
        life: rnd(1.6, 0.5), max: 2, size: rnd(2.6, 1), color: '#bfe9f5',
        drag: 0.99, grav: 620, rot: rnd(6), spin: rnd(18, -18), glow: Math.random() < 0.4,
      });
    }
  }

  fire(x, y, n = 1) {
    for (let i = 0; i < n; i++) {
      this.add({
        type: 'flame', x: x + rnd(3, -3), y: y + rnd(2, -2),
        vx: rnd(16, -16), vy: rnd(-30, -90),
        life: rnd(0.85, 0.3), max: 0.9, size: rnd(5, 2),
        drag: 0.97, grow: -6,
      });
    }
  }

  ember(x, y, n = 1) {
    for (let i = 0; i < n; i++) {
      this.add({
        type: 'spark', x, y, vx: rnd(30, -30), vy: rnd(-20, -70),
        life: rnd(2.4, 0.8), max: 2.4, size: rnd(1.6, 0.6), color: '#ff9a3c', drag: 0.995, grav: -8, glow: true,
      });
    }
  }

  water(x, y, n = 6, power = 1) {
    for (let i = 0; i < n; i++) {
      this.add({
        type: 'water', x: x + rnd(6, -6), y: y + rnd(4, -4),
        vx: rnd(220, -220) * power, vy: rnd(-80, -360) * power,
        life: rnd(1.6, 0.5), max: 1.8, size: rnd(3.4, 1.2),
        color: '#8fd0ef', drag: 0.99, grav: 700,
      });
    }
  }

  ring(x, y, r = 20, color = '#fff5c9', width = 3, speed = 520, life = 0.6) {
    this.rings.push({ x, y, r, color, width, speed, life, max: life });
  }

  explosion(x, y, radius, power = 1, type = 'normal') {
    const r = radius;
    this.smoke(x, y, Math.min(40, 10 + r), 0.8 + r / 40, false);
    this.dust(x, y, Math.min(30, 6 + r * 0.5), 1 + r / 60);
    this.sparks(x, y, Math.min(60, 12 + r), 0.7 + r / 30);
    this.ring(x, y, r * 0.4, '#fff0c0', 5, 700, 0.5);
    this.ring(x, y, r * 0.7, '#ffb347', 2.5, 420, 0.9);
    this.shocks.push({ x, y, r: r * 0.5, speed: r * 4.5, life: 0.7, max: 0.7, power });
    this.shake(Math.min(26, 4 + r * 0.35), 0.6);
    if (type === 'nuke') {
      this.nukeBoom(x, y, radius);
    } else {
      this.flash(Math.min(0.5, 0.1 + r / 220), '#ffe6b0', 6);
      this.fire(x, y, Math.min(24, 6 + r * 0.4));
    }
  }

  nukeBoom(x, y, r) {
    this.flash(0.95, '#ffffff', 1.1);
    this.ring(x, y, 10, '#ffffff', 16, 1900, 1.4);
    this.ring(x, y, 10, '#ffe9a8', 9, 1350, 2.2);
    this.ring(x, y, 10, '#ff9d4d', 6, 900, 3.2);
    this.shocks.push({ x, y, r: 10, speed: 900, life: 2.6, max: 2.6, power: 6 });
    this.shake(48, 2.6);
    // fireball
    for (let i = 0; i < 220; i++) {
      const a = rnd(Math.PI * 2), d = rnd(r * 0.9, 0);
      this.add({
        type: 'flame', x: x + Math.cos(a) * d, y: y + Math.sin(a) * d,
        vx: Math.cos(a) * rnd(220, 20), vy: Math.sin(a) * rnd(220, 20) - 60,
        life: rnd(2.4, 0.7), max: 2.4, size: rnd(22, 8), drag: 0.96, grow: 12,
      });
    }
    // mushroom stem + cap
    for (let i = 0; i < 360; i++) {
      const t = i / 360;
      this.add({
        type: 'smoke',
        x: x + rnd(60, -60), y: y - rnd(r * 1.6, 0),
        vx: rnd(50, -50), vy: rnd(-140, -420),
        life: rnd(11, 5), max: 11, size: rnd(64, 22), color: i % 3 ? '#7a6c61' : '#b09b87',
        drag: 0.99, grow: rnd(30, 10), mushroom: true, t,
      });
    }
    // fallout rain later
    setTimeout(() => {
      if (this.g && this.g.state === 'play') this.g.startFallout();
    }, 1400);
    this.computeShadowMask(x, y, Math.min(220, r * 3.2));
  }

  // Ray-marched lighting mask: walls behind solid geometry stay dark.
  computeShadowMask(x, y, radius) {
    const world = this.g.world;
    const size = 220;
    const cv = document.createElement('canvas');
    cv.width = size; cv.height = size;
    const ctx = cv.getContext('2d');
    ctx.fillStyle = 'rgba(0,0,0,0.97)';
    ctx.fillRect(0, 0, size, size);
    // carve out lit area from the blast centre
    const img = ctx.getImageData(0, 0, size, size);
    const data = img.data;
    const cx = size / 2, cy = size / 2;
    const scale = radius / (size / 2);
    const lit = new Uint8Array(size * size);
    const q = [Math.floor(cy) * size + Math.floor(cx)];
    lit[q[0]] = 1;
    while (q.length) {
      const i = q.pop();
      const px = i % size, py = (i / size) | 0;
      const wx = x + (px - cx) * scale;
      const wy = y + (py - cy) * scale;
      if (world.solidAt(Math.round(wx), Math.round(wy))) continue;
      for (let d = 0; d < 4; d++) {
        const nx = px + (d === 0 ? 1 : d === 1 ? -1 : 0);
        const ny = py + (d === 2 ? 1 : d === 3 ? -1 : 0);
        if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
        const ni = ny * size + nx;
        if (lit[ni]) continue;
        lit[ni] = 1;
        q.push(ni);
      }
    }
    for (let i = 0; i < size * size; i++) {
      if (lit[i]) data[i * 4 + 3] = 0;
      else {
        const px = i % size, py = (i / size) | 0;
        const d = Math.hypot(px - cx, py - cy) / (size / 2);
        data[i * 4 + 3] = clamp(1 - d, 0, 1) * 210;
      }
    }
    ctx.putImageData(img, 0, 0);
    this.shadowMask = { canvas: cv, x, y, radius, life: 2.6, max: 2.6 };
  }

  lightning(x0, y0, x1, y1, damage = true) {
    const pts = [{ x: x0, y: y0 }];
    const segs = 16;
    for (let i = 1; i <= segs; i++) {
      const t = i / segs;
      const jitter = i === segs ? 0 : rnd(24, -24) * (1 - t * 0.4);
      pts.push({ x: x0 + (x1 - x0) * t + jitter, y: y0 + (y1 - y0) * t });
    }
    this.bolts.push({ pts, life: 0.28, max: 0.28 });
    this.sparks(x1, y1, 26, 1.2, '#cfe8ff');
    this.smoke(x1, y1, 8, 0.7, true);
    this.flash(0.22, '#dceaff', 9);
    this.shake(6, 0.3);
    if (damage && this.g) {
      this.g.world.explode(x1, y1, 9, 1.5, { fireChance: 0.6 });
      this.g.world.igniteRing(x1, y1, 14, 0.5);
    }
  }

  meteor(x0, y0, tx, ty, size = 1) {
    const d = Math.hypot(tx - x0, ty - y0) || 1;
    const speed = Math.max(1100, d / 1.5);
    this.meteors.push({ x: x0, y: y0, tx, ty, size, speed, life: 6, trail: [] });
    this.shake(2, 0.2);
  }

  tornado(y, strength = 1) {
    this.tornadoes.push({ x: this.g.visibleCenterX(), y, strength, life: 26, max: 26, age: 0, drift: rnd(30, -30) });
  }

  startTsunami(y, dir = 1, strength = 1) {
    const g = this.g;
    const viewW = g.canvas.width / g.cam.zoom;
    // start just off the visible edge so the wall arrives within a second or two
    const startX = dir > 0 ? g.cam.x - 120 : g.cam.x + viewW + 120;
    this.tsunami = {
      y, dir, strength, height: 34, speed: 640,
      x: startX,
      life: 24, max: 24,
    };
  }

  blackhole(x, y) {
    this.blackholes.push({ x, y, life: 8, max: 8, r: 26 });
    this.shake(8, 1.4);
  }

  // ----------------------------------------------------------------- update
  update(dt) {
    const g = this.g;
    const w = g.world;
    this.budget = 260;

    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.life -= dt;
      if (p.life <= 0) { this.particles.splice(i, 1); continue; }
      p.vy += (p.grav ?? 0) * dt;
      const d = p.drag ?? 0.99;
      p.vx *= Math.pow(d, dt * 60);
      p.vy *= Math.pow(d, dt * 60);
      if (p.type === 'smoke') {
        p.vx += g.wind * 26 * dt;
        p.vy -= 12 * dt;
      }
      if (p.mushroom) {
        p.vx += Math.cos(p.life * 2 + p.t * 8) * 26 * dt;
      }
      // wind from tornadoes
      for (const t of this.tornadoes) {
        const dx = p.x - t.x, dy = p.y - t.y;
        const d2 = dx * dx + dy * dy + 1;
        if (d2 < 90000) {
          const f = 9000 / d2;
          p.vx += (-dy / Math.sqrt(d2)) * f * 60 * dt * t.strength;
          p.vy += (dx / Math.sqrt(d2)) * f * 30 * dt * t.strength - f * 6 * dt;
        }
      }
      for (const b of this.blackholes) {
        const dx = b.x - p.x, dy = b.y - p.y;
        const d2 = dx * dx + dy * dy + 1;
        if (d2 < 250000) {
          const f = 2600 / d2;
          p.vx += dx * f * 60 * dt;
          p.vy += dy * f * 60 * dt;
        }
      }
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      if (p.grow) p.size += p.grow * dt;
      if (p.spin) p.rot += p.spin * dt;
      // collide with ground for solid particle kinds
      if (p.type === 'chip' || p.type === 'water') {
        const cx = p.x / g.cell, cy = p.y / g.cell;
        if (w.solidAt(cx | 0, cy | 0)) {
          p.vy *= -0.28;
          p.vx *= 0.6;
          p.y -= 1.2;
          if (p.type === 'water') p.life = Math.min(p.life, 0.2);
        }
      }
    }

    for (let i = this.rings.length - 1; i >= 0; i--) {
      const r = this.rings[i];
      r.r += r.speed * dt;
      r.speed *= 0.985;
      r.life -= dt;
      if (r.life <= 0) this.rings.splice(i, 1);
    }
    for (let i = this.flashes.length - 1; i >= 0; i--) {
      const f = this.flashes[i];
      f.a -= f.decay * dt;
      if (f.a <= 0) this.flashes.splice(i, 1);
    }
    for (let i = this.bolts.length - 1; i >= 0; i--) {
      this.bolts[i].life -= dt;
      if (this.bolts[i].life <= 0) this.bolts.splice(i, 1);
    }
    for (let i = this.shocks.length - 1; i >= 0; i--) {
      const s = this.shocks[i];
      s.r += s.speed * dt;
      s.speed *= 0.97;
      s.life -= dt;
      if (s.life <= 0) this.shocks.splice(i, 1);
    }
    for (let i = this.meteors.length - 1; i >= 0; i--) {
      const m = this.meteors[i];
      const dx = m.tx - m.x, dy = m.ty - m.y;
      const d = Math.hypot(dx, dy);
      const sp = m.speed || 1400;
      m.life -= dt;
      if (m.life <= 0) { this.meteors.splice(i, 1); continue; }
      if (d < sp * dt + 6) {
        this.meteors.splice(i, 1);
        g.meteorImpact(m.tx, m.ty, m.size);
        continue;
      }
      m.x += (dx / d) * sp * dt;
      m.y += (dy / d) * sp * dt;
      m.trail.push({ x: m.x, y: m.y });
      if (m.trail.length > 22) m.trail.shift();
      if (Math.random() < 0.7) this.fire(m.x, m.y, 1);
      if (Math.random() < 0.35) this.smoke(m.x, m.y, 1, 0.5, true);
    }
    for (let i = this.tornadoes.length - 1; i >= 0; i--) {
      const t = this.tornadoes[i];
      t.age += dt;
      t.life -= dt;
      t.x += t.drift * dt;
      if (t.life <= 0) { this.tornadoes.splice(i, 1); continue; }
      // spin up particles along the funnel
      if (Math.random() < 0.9) {
        const h = rnd(120, 0);
        const rad = 6 + h * 0.22;
        const a = t.age * 9 + h * 0.06;
        this.add({
          type: 'dust', x: t.x + Math.cos(a) * rad, y: t.y - h + Math.sin(a) * rad * 0.35,
          vx: Math.cos(a + 1.6) * 220, vy: -40 - h * 0.4,
          life: rnd(1.2, 0.4), max: 1.4, size: rnd(5, 1.5), color: '#8e8375', drag: 1, grow: 2,
        });
      }
      // tear the world apart
      if (Math.random() < 0.5) {
        const h = rnd(120, 0);
        const rad = 6 + h * 0.22;
        const a = t.age * 9 + h * 0.06;
        const cx = Math.round((t.x + Math.cos(a) * rad) / 26);
        const cy = Math.round((t.y - h + Math.sin(a) * rad * 0.35) / 26);
        if (w.solidAt(cx, cy) && Math.random() < 0.35) w.damageCell(cx, cy, 90 * t.strength);
      }
    }
    for (const b of this.blackholes) {
      b.life -= dt;
      if (b.life <= 0) continue;
      if (Math.random() < 0.6) {
        const a = rnd(Math.PI * 2), d = rnd(300, 120);
        this.add({
          type: 'spark', x: b.x + Math.cos(a) * d, y: b.y + Math.sin(a) * d,
          vx: -Math.cos(a) * 260, vy: -Math.sin(a) * 260, life: 0.9, max: 1,
          size: 1.6, color: '#c9a6ff', drag: 1, glow: true,
        });
      }
    }
    for (let i = this.blackholes.length - 1; i >= 0; i--) if (this.blackholes[i].life <= 0) this.blackholes.splice(i, 1);

    if (this.tsunami) {
      const t = this.tsunami;
      t.x += t.dir * (t.speed || 640) * dt;
      t.life -= dt;
      const top = t.y - t.height;
      for (let i = 0; i < 14; i++) {
        const px = t.x + rnd(50, -50);
        this.add({
          type: 'water', x: px, y: rnd(t.y, top), vx: t.dir * rnd(420, 180),
          vy: rnd(-60, -320), life: rnd(1.2, 0.4), max: 1.4,
          size: rnd(4, 1.4), color: '#9fd8ef', drag: 0.99, grav: 640,
        });
      }
      // the wave crushes whatever stands at its foot
      const foot = Math.round(t.x / this.g.cell);
      const gy = Math.round(t.y / this.g.cell);
      for (let d = -3; d <= 3; d++) {
        for (let dy = 0; dy < 26; dy++) {
          const cx = foot + d;
          const cy = gy - dy;
          if (!w.solidAt(cx, cy)) continue;
          if (Math.random() < 0.55) w.destroy(w.idx(cx, cy), 0.5);
          else w.damageCell(cx, cy, 140);
        }
      }
      const viewW = this.g.canvas.width / this.g.cam.zoom;
      const offscreen = t.dir > 0 ? t.x > this.g.cam.x + viewW + 240 : t.x < this.g.cam.x - 240;
      if (offscreen || t.life <= 0) this.tsunami = null;
    }

    if (this.shadowMask) {
      this.shadowMask.life -= dt;
      if (this.shadowMask.life <= 0) this.shadowMask = null;
    }
    if (this.shakeTime > 0) {
      this.shakeTime -= dt;
      if (this.shakeTime <= 0) this.shakeAmp = 0;
      else this.shakeAmp *= 0.94;
    }
  }

  // ------------------------------------------------------------------- draw
  draw(ctx, cam) {
    const cell = this.g.cell;
    // black hole lens
    for (const b of this.blackholes) {
      const sx = (b.x - cam.x) * cam.zoom, sy = (b.y - cam.y) * cam.zoom;
      const r = (b.r + Math.sin(performance.now() / 300) * 3) * cam.zoom;
      const grd = ctx.createRadialGradient(sx, sy, r * 0.2, sx, sy, r * 3.2);
      grd.addColorStop(0, 'rgba(0,0,0,1)');
      grd.addColorStop(0.35, 'rgba(20,6,40,0.85)');
      grd.addColorStop(0.75, 'rgba(90,40,160,0.25)');
      grd.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.arc(sx, sy, r * 3.2, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = 'rgba(190,150,255,0.85)';
      ctx.lineWidth = 2 * cam.zoom;
      ctx.beginPath();
      ctx.arc(sx, sy, r, 0, Math.PI * 2);
      ctx.stroke();
    }

    // tornado funnels
    for (const t of this.tornadoes) {
      const baseY = t.y - cam.y;
      for (let h = 0; h < 150; h += 4) {
        const rad = (6 + h * 0.22) * cam.zoom;
        const a = t.age * 9 + h * 0.06;
        const x = (t.x + Math.cos(a) * (rad / cam.zoom) - cam.x) * cam.zoom;
        const y = (baseY - h) * cam.zoom;
        ctx.fillStyle = 'rgba(190,180,170,0.18)';
        ctx.beginPath();
        ctx.arc(x + Math.sin(a) * rad * 0.4, y, rad * 0.75, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    // tsunami wall
    if (this.tsunami) {
      const t = this.tsunami;
      const sx = (t.x - cam.x) * cam.zoom;
      const sy = (t.y - cam.y) * cam.zoom;
      const h = t.height * cam.zoom;
      const grd = ctx.createLinearGradient(sx, sy - h, sx, sy);
      grd.addColorStop(0, 'rgba(220,245,255,0.9)');
      grd.addColorStop(0.25, 'rgba(90,170,210,0.85)');
      grd.addColorStop(1, 'rgba(25,70,110,0.9)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.moveTo(sx - t.dir * 90 * cam.zoom, sy + 30 * cam.zoom);
      ctx.quadraticCurveTo(sx - t.dir * 40 * cam.zoom, sy - h, sx + t.dir * 26 * cam.zoom, sy - h * 0.9);
      ctx.quadraticCurveTo(sx + t.dir * 10 * cam.zoom, sy - h * 0.3, sx + t.dir * 70 * cam.zoom, sy + 30 * cam.zoom);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = 'rgba(235,250,255,0.8)';
      ctx.lineWidth = 2;
      ctx.stroke();
    }

    // particles
    if (!this.sprites) this.makeSprites();
    const sp = this.sprites;
    const lowQ = this.g.lowQ;
    for (const p of this.particles) {
      const x = (p.x - cam.x) * cam.zoom;
      const y = (p.y - cam.y) * cam.zoom;
      if (x < -80 || y < -80 || x > ctx.canvas.width + 80 || y > ctx.canvas.height + 80) continue;
      // adaptive quality: during heavy scenes skip the confetti-sized stuff
      if (lowQ && (p.type === 'chip' || p.type === 'spark')) continue;
      const k = p.life / p.max;
      if (p.type === 'smoke' || p.type === 'dust') {
        const r = Math.max(0.6, p.size * cam.zoom);
        ctx.globalAlpha = (p.type === 'smoke' ? 0.5 : 0.55) * Math.min(1, k * 1.6) * (p.color === '#2a2a2c' ? 1.4 : 1);
        if (p.color === '#2a2a2c') {
          ctx.fillStyle = 'rgba(30,30,34,0.5)';
          ctx.beginPath();
          ctx.arc(x, y, r, 0, Math.PI * 2);
          ctx.fill();
        } else if (p.color !== '#c8bda9') {
          // tinted smoke: fill style once per particle is cheap enough here
          ctx.fillStyle = p.color;
          ctx.beginPath();
          ctx.arc(x, y, r, 0, Math.PI * 2);
          ctx.fill();
        } else {
          ctx.drawImage(sp.smoke, x - r, y - r, r * 2, r * 2);
          if (p.color === '#2a2a2c') {
            ctx.globalCompositeOperation = 'source-over';
          }
        }
      } else if (p.type === 'flame') {
        const r = Math.max(1, p.size * cam.zoom * 1.6);
        ctx.globalAlpha = Math.min(1, k * 1.8);
        ctx.drawImage(sp.flame, x - r, y - r, r * 2, r * 2);
      } else if (p.type === 'spark') {
        const r = Math.max(0.9, p.size * cam.zoom * 2.2);
        ctx.globalAlpha = Math.min(1, k * 1.5);
        ctx.drawImage(sp.glow, x - r, y - r, r * 2, r * 2);
      } else {
        ctx.globalAlpha = Math.min(1, k * 2);
        ctx.fillStyle = p.color;
        ctx.save();
        ctx.translate(x, y);
        if (p.rot) ctx.rotate(p.rot);
        const r = Math.max(0.5, p.size * cam.zoom);
        ctx.fillRect(-r, -r, r * 2, r * 2);
        ctx.restore();
      }
    }
    ctx.globalCompositeOperation = 'source-over';
    ctx.globalAlpha = 1;

    // shockwaves
    for (const s of this.shocks) {
      const a = Math.max(0, s.life / s.max);
      ctx.strokeStyle = `rgba(255,255,255,${a * 0.35})`;
      ctx.lineWidth = Math.max(1, 6 * a * cam.zoom);
      ctx.beginPath();
      ctx.arc((s.x - cam.x) * cam.zoom, (s.y - cam.y) * cam.zoom, s.r * cam.zoom, 0, Math.PI * 2);
      ctx.stroke();
    }

    // rings
    for (const r of this.rings) {
      const a = Math.max(0, r.life / r.max);
      ctx.strokeStyle = r.color;
      ctx.globalAlpha = a * 0.8;
      ctx.lineWidth = r.width * cam.zoom * a;
      ctx.beginPath();
      ctx.arc((r.x - cam.x) * cam.zoom, (r.y - cam.y) * cam.zoom, r.r * cam.zoom, 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    // lightning
    for (const b of this.bolts) {
      const a = b.life / b.max;
      ctx.strokeStyle = `rgba(220,238,255,${a})`;
      ctx.shadowColor = '#9ecbff';
      ctx.shadowBlur = 18;
      ctx.lineWidth = 3 * cam.zoom;
      ctx.beginPath();
      b.pts.forEach((p, i) => {
        const x = (p.x - cam.x) * cam.zoom, y = (p.y - cam.y) * cam.zoom;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      });
      ctx.stroke();
      ctx.shadowBlur = 0;
    }

    // meteors
    for (const m of this.meteors) {
      for (let i = 0; i < m.trail.length; i++) {
        const t = m.trail[i];
        const a = i / m.trail.length;
        ctx.fillStyle = `rgba(255,${140 + a * 90},${60 + a * 100},${a * 0.7})`;
        ctx.beginPath();
        ctx.arc((t.x - cam.x) * cam.zoom, (t.y - cam.y) * cam.zoom, m.size * 2.2 * a * cam.zoom, 0, Math.PI * 2);
        ctx.fill();
      }
      const x = (m.x - cam.x) * cam.zoom, y = (m.y - cam.y) * cam.zoom;
      const grd = ctx.createRadialGradient(x, y, 0, x, y, m.size * 9 * cam.zoom);
      grd.addColorStop(0, '#fff7d6');
      grd.addColorStop(0.4, '#ffbb55');
      grd.addColorStop(1, 'rgba(255,80,0,0)');
      ctx.fillStyle = grd;
      ctx.beginPath();
      ctx.arc(x, y, m.size * 9 * cam.zoom, 0, Math.PI * 2);
      ctx.fill();
    }

    // ballistic tracers (aimed projectiles like nukes / dynamite bags)
    for (const p of this.g.projectiles) {
      const x = (p.x - cam.x) * cam.zoom, y = (p.y - cam.y) * cam.zoom;
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(Math.atan2(p.vy, p.vx));
      ctx.fillStyle = p.color || '#d9d2c4';
      ctx.fillRect(-4 * cam.zoom, -2 * cam.zoom, 8 * cam.zoom, 4 * cam.zoom);
      ctx.fillStyle = '#ffb347';
      ctx.beginPath();
      ctx.arc(-4 * cam.zoom, 0, 2.4 * cam.zoom, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  drawOverlay(ctx, cam) {
    // nuke flash shadow mask
    if (this.shadowMask) {
      const s = this.shadowMask;
      const a = clamp(s.life / s.max, 0, 1);
      const x = (s.x - cam.x) * cam.zoom - s.radius * cam.zoom;
      const y = (s.y - cam.y) * cam.zoom - s.radius * cam.zoom;
      const d = s.radius * 2 * cam.zoom;
      ctx.save();
      ctx.globalAlpha = a * 0.95;
      ctx.imageSmoothingEnabled = true;
      ctx.drawImage(s.canvas, x, y, d, d);
      ctx.restore();
    }
    for (const f of this.flashes) {
      if (f.a <= 0) continue;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.globalAlpha = clamp(f.a, 0, 1);
      ctx.fillStyle = f.color;
      ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
      ctx.restore();
    }
  }
}
