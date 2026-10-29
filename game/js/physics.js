(function (root) {
  "use strict";

  /* =========================================================
     physics.js — верими-физика (Verlet) с твёрдыми связями,
     угловыми "мышцами", трением, опорой на рельеф и ударами.
     ========================================================= */

  const PPM = 60;
  const GRAVITY = 9.81 * PPM; // px/s^2

  function normAngle(a) {
    while (a > Math.PI) a -= Math.PI * 2;
    while (a < -Math.PI) a += Math.PI * 2;
    return a;
  }

  class Point {
    constructor(x, y, opts) {
      opts = opts || {};
      this.x = x;
      this.y = y;
      this.px = x;
      this.py = y;
      this.ox = x; // позиция в начале кадра (для скорости удара)
      this.oy = y;
      this.r = opts.r != null ? opts.r : 4;
      this.mass = opts.mass != null ? opts.mass : 1;
      this.invMass = this.mass > 0 ? 1 / this.mass : 0;
      this.friction = opts.friction != null ? opts.friction : 0.32;
      this.restitution = opts.restitution != null ? opts.restitution : 0.02;
      this.drag = opts.drag != null ? opts.drag : 0.0007;
      this.pinned = false;
      this.grounded = false;
      this.groundNormal = { x: 0, y: -1 };
      this.impact = 0;
      this.ax = 0;
      this.ay = 0;
      this.owner = null;
      this.name = "";
    }

    get vx() {
      return this.x - this.px;
    }
    get vy() {
      return this.y - this.py;
    }
    setVelocity(vx, vy) {
      this.px = this.x - vx;
      this.py = this.y - vy;
    }
    addVelocity(vx, vy) {
      this.px -= vx;
      this.py -= vy;
    }
    speed() {
      return Math.hypot(this.x - this.px, this.y - this.py);
    }
    worldSpeed() {
      return Math.hypot(this.x - this.ox, this.y - this.oy);
    }
  }

  class Link {
    constructor(a, b, opts) {
      opts = opts || {};
      this.a = a;
      this.b = b;
      this.length = opts.length != null ? opts.length : Math.hypot(b.x - a.x, b.y - a.y);
      this.stiffness = opts.stiffness != null ? opts.stiffness : 1;
      this.type = "bone";
      this.alive = true;
    }
    solve() {
      const a = this.a;
      const b = this.b;
      const dx = b.x - a.x;
      const dy = b.y - a.y;
      const d = Math.hypot(dx, dy) || 0.0001;
      const w = a.invMass + b.invMass;
      if (w === 0) return;
      const diff = ((d - this.length) / d) * this.stiffness;
      const ka = (a.invMass / w) * diff;
      const kb = (b.invMass / w) * diff;
      a.x += dx * ka;
      a.y += dy * ka;
      b.x -= dx * kb;
      b.y -= dy * kb;
    }
  }

  /**
   * Joint — ограничитель сустава (только пределы, без «мышц»).
   * Работает как позиционная проекция: стабильно и не закачивает энергию.
   */
  class Joint {
    constructor(a, b, c, opts) {
      opts = opts || {};
      this.a = a;
      this.b = b;
      this.c = c;
      this.rest = this.current();
      this.min = opts.min != null ? opts.min : -Infinity;
      this.max = opts.max != null ? opts.max : Infinity;
      this.stiffness = opts.stiffness != null ? opts.stiffness : 0.5;
      this.alive = true;
    }

    current() {
      const a1 = Math.atan2(this.a.y - this.b.y, this.a.x - this.b.x);
      const a2 = Math.atan2(this.c.y - this.b.y, this.c.x - this.b.x);
      return normAngle(a2 - a1);
    }

    solve() {
      const bent = normAngle(this.current() - this.rest);
      let over = 0;
      if (bent < this.min) over = this.min - bent;
      else if (bent > this.max) over = this.max - bent;
      else return;
      if (!isFinite(over)) return;

      const a = this.a;
      const b = this.b;
      const c = this.c;
      const wa = a.invMass;
      const wc = c.invMass;
      const w = wa + wc;
      if (w === 0) return;
      const corr = over * this.stiffness;
      if (wa > 0) rotateAbout(a, b, (-corr * wa) / w);
      if (wc > 0) rotateAbout(c, b, (corr * wc) / w);
    }
  }

  function rotateAbout(p, c, ang) {
    const s = Math.sin(ang);
    const co = Math.cos(ang);
    const dx = p.x - c.x;
    const dy = p.y - c.y;
    p.x = c.x + dx * co - dy * s;
    p.y = c.y + dx * s + dy * co;
  }

  class World {
    constructor(terrain) {
      this.terrain = terrain;
      this.points = [];
      this.links = [];
      this.angles = [];
      this.gravity = GRAVITY;
      this.iterations = 8;
      this.impacts = [];
      this.time = 0;
      this.minX = 8;
      this.maxX = terrain.width - 8;
    }

    add(p) {
      p.owner = p.owner || null;
      this.points.push(p);
      return p;
    }

    link(a, b, opts) {
      const l = new Link(a, b, opts);
      this.links.push(l);
      return l;
    }

    joint(a, b, c, opts) {
      const j = new Joint(a, b, c, opts);
      this.angles.push(j);
      return j;
    }

    angle(a, b, c, opts) {
      return this.joint(a, b, c, opts);
    }

    step(dt) {
      this.time += dt;
      const gdt2 = this.gravity * dt * dt;

      // 1. интеграция
      for (let i = 0; i < this.points.length; i++) {
        const p = this.points[i];
        p.ox = p.x;
        p.oy = p.y;
        if (p.pinned) continue;
        const vx = (p.x - p.px) * (1 - p.drag);
        const vy = (p.y - p.py) * (1 - p.drag);
        p.px = p.x;
        p.py = p.y;
        p.x += vx + p.ax * dt * dt;
        p.y += vy + gdt2 + p.ay * dt * dt;
        p.ax = 0;
        p.ay = 0;
        p.grounded = false;
        p.impact = 0;
      }

      // 2. итерации связей
      for (let it = 0; it < this.iterations; it++) {
        for (let i = 0; i < this.links.length; i++) {
          const l = this.links[i];
          if (l.alive) l.solve();
        }
        for (let i = 0; i < this.angles.length; i++) {
          const a = this.angles[i];
          if (a.alive) a.solve();
        }
      }

      // 3. столкновения с рельефом и границами
      this.impacts.length = 0;
      for (let i = 0; i < this.points.length; i++) {
        this.collide(this.points[i]);
      }

      // 4. столкновения точек разных тел (мягкое расталкивание)
      for (let i = 0; i < this.points.length; i++) {
        const a = this.points[i];
        for (let j = i + 1; j < this.points.length; j++) {
          const b = this.points[j];
          if (a.owner === b.owner) continue;
          const dx = b.x - a.x;
          const dy = b.y - a.y;
          const minD = a.r + b.r;
          const d2 = dx * dx + dy * dy;
          if (d2 >= minD * minD || d2 < 1e-6) continue;
          const d = Math.sqrt(d2);
          const nx = dx / d;
          const ny = dy / d;
          const w = a.invMass + b.invMass;
          if (w === 0) continue;
          const pen = (minD - d) * 0.55;
          a.x -= nx * pen * (a.invMass / w);
          a.y -= ny * pen * (a.invMass / w);
          b.x += nx * pen * (b.invMass / w);
          b.y += ny * pen * (b.invMass / w);
          // гасим скорость сближения
          const rvx = b.x - b.px - (a.x - a.px);
          const rvy = b.y - b.py - (a.y - a.py);
          const vn = rvx * nx + rvy * ny;
          if (vn < 0) {
            const imp = vn * 0.5;
            a.px -= nx * imp * (a.invMass / w);
            a.py -= ny * imp * (a.invMass / w);
            b.px += nx * imp * (b.invMass / w);
            b.py += ny * imp * (b.invMass / w);
          }
        }
      }
    }

    collide(p) {
      const t = this.terrain;
      // границы мира
      if (p.x < this.minX) {
        p.x = this.minX;
        p.px = p.x + (p.px - p.x) * -0.2;
      } else if (p.x > this.maxX) {
        p.x = this.maxX;
        p.px = p.x + (p.px - p.x) * -0.2;
      }

      const gy = t.groundY(p.x);
      const sl = t.slope(p.x);
      const L = Math.hypot(1, sl);
      const nx = sl / L;
      const ny = -1 / L;
      const dist = (gy - p.y) / L; // расстояние от поверхности вдоль нормали

      if (dist < p.r) {
        const pen = p.r - dist;
        p.x += nx * pen;
        p.y += ny * pen;

        const vx = p.x - p.px;
        const vy = p.y - p.py;
        const vn = vx * nx + vy * ny;

        if (vn < 0) {
          let tx = vx - vn * nx;
          let ty = vy - vn * ny;
          const mu = p.friction;
          tx *= 1 - mu;
          ty *= 1 - mu;
          const bounce = -vn * p.restitution;
          p.px = p.x - (tx + nx * bounce);
          p.py = p.y - (ty + ny * bounce);

          const hitSpeed = -vn; // px/step
          if (hitSpeed > 1.2) {
            p.impact = hitSpeed;
            this.impacts.push({
              point: p,
              speed: hitSpeed,
              x: p.x - nx * p.r,
              y: p.y - ny * p.r,
              nx,
              ny,
            });
          }
        }
        p.grounded = true;
        p.groundNormal.x = nx;
        p.groundNormal.y = ny;
      }
    }

    /** взять точку в «руку» (мышь) — кинематическая пружина */
    grab(p) {
      this.grabbed = p;
      p.pinned = true;
      this.grabTarget = { x: p.x, y: p.y };
    }

    moveGrab(x, y) {
      if (!this.grabbed) return;
      // тянем с ограничением по скорости, чтобы не рвать связи
      const p = this.grabbed;
      const dx = x - p.x;
      const dy = y - p.y;
      const d = Math.hypot(dx, dy);
      const maxStep = 11; // px/шаг — примерно предел «силы руки»
      const k = d > maxStep ? maxStep / d : 1;
      p.x += dx * k;
      p.y += dy * k;
      p.px = p.x;
      p.py = p.y;
    }

    release(vx, vy) {
      const p = this.grabbed;
      if (!p) return;
      p.pinned = false;
      // ограничиваем скорость броска, чтобы не улетали в космос
      let sx = vx || 0;
      let sy = vy || 0;
      const sp = Math.hypot(sx, sy);
      const maxSp = 8;
      if (sp > maxSp) {
        sx = (sx / sp) * maxSp;
        sy = (sy / sp) * maxSp;
      }
      p.setVelocity(sx, sy);
      this.grabbed = null;
    }
  }

  root.Physics = {
    PPM,
    GRAVITY,
    Point,
    Link,
    Joint,
    AngleLink: Joint,
    World,
    normAngle,
    rotateAbout,
  };
})(typeof globalThis !== "undefined" ? globalThis : this);
