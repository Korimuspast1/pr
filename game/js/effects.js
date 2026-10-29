(function (root) {
  "use strict";

  /* =========================================================
     effects.js — кровь, брызги, пыль, лужи
     ========================================================= */

  const MAX_DECALS = 700;
  const MAX_PARTICLES = 900;

  class Effects {
    constructor(terrain) {
      this.terrain = terrain;
      this.particles = [];
      this.decals = [];
      this.decalGrid = new Map(); // ключ ячейки -> список декалей (для слияния луж)
      this.cell = 48;
    }

    /* ---------------- кровь ---------------- */

    /**
     * Брызги крови из точки.
     * @param {number} x
     * @param {number} y
     * @param {number} count
     * @param {number} dirX -1..1 направление удара
     * @param {number} dirY -1..1 (вверх отрицательное)
     */
    bloodBurst(x, y, count, dirX, dirY) {
      const n = Math.min(70, Math.round(count));
      for (let i = 0; i < n; i++) {
        if (this.particles.length >= MAX_PARTICLES) break;
        const spread = 0.95;
        const ang =
          Math.atan2((dirY || 0) - 0.75, dirX || 0.2) + (Math.random() - 0.5) * 2 * spread;
        const speed = 90 + Math.random() * 460;
        this.particles.push({
          kind: "blood",
          x: x + (Math.random() - 0.5) * 8,
          y: y + (Math.random() - 0.5) * 10,
          vx: Math.cos(ang) * speed + (dirX || 0) * 120,
          vy: Math.sin(ang) * speed + (dirY || 0) * 90,
          r: 1.0 + Math.random() * 2.1,
          life: 0.6 + Math.random() * 0.9,
          age: 0,
        });
      }
      // сразу немного крови на месте
      this.addDecal(x, y - 4, 2 + Math.random() * 3.2);
    }

    bloodDrip(x, y) {
      for (let i = 0; i < 3; i++) {
        if (this.particles.length >= MAX_PARTICLES) return;
        this.particles.push({
          kind: "blood",
          x: x + (Math.random() - 0.5) * 9,
          y: y + (Math.random() - 0.5) * 8,
          vx: (Math.random() - 0.5) * 40,
          vy: 20 + Math.random() * 60,
          r: 1.4 + Math.random() * 2,
          life: 1.4,
          age: 0,
        });
      }
    }

    dust(x, y, count, dir) {
      for (let i = 0; i < count; i++) {
        if (this.particles.length >= MAX_PARTICLES) break;
        this.particles.push({
          kind: "dust",
          x: x + (Math.random() - 0.5) * 12,
          y: y - 1,
          vx: (Math.random() - 0.5) * 70 + (dir || 0) * 30,
          vy: -20 - Math.random() * 55,
          r: 2 + Math.random() * 4,
          life: 0.45 + Math.random() * 0.5,
          age: 0,
        });
      }
    }

    /* ---------------- декали ---------------- */

    addDecal(x, y, r) {
      const gy = this.terrain.groundY(x);
      // кровь на земле — лужа у поверхности
      const decal = {
        x,
        y: Math.min(y, gy),
        r,
        alpha: 0.75,
        seed: Math.random() * 100,
      };
      this.decals.push(decal);
      if (this.decals.length > MAX_DECALS) this.decals.shift();
    }

    /* ---------------- обновление ---------------- */

    update(dt) {
      const t = this.terrain;
      for (let i = this.particles.length - 1; i >= 0; i--) {
        const p = this.particles[i];
        p.age += dt;
        if (p.age >= p.life) {
          if (p.kind === "blood" && Math.random() < 0.85) {
            this.addDecal(p.x, p.y, 1.3 + p.r * 1.1);
          }
          this.particles.splice(i, 1);
          continue;
        }

        if (p.kind === "dust") {
          p.vx *= 1 - dt * 3;
          p.vy *= 1 - dt * 2.2;
          p.vy -= 10 * dt;
        } else {
          p.vy += 1500 * dt; // px/s^2 — кровь тяжелее воздуха
          p.vx *= 1 - dt * 0.6;
        }

        p.x += p.vx * dt;
        p.y += p.vy * dt;

        if (p.kind === "blood") {
          const gy = t.groundY(p.x);
          if (p.y >= gy - 1) {
            // капля упала
            this.addDecal(p.x, gy, 1.2 + p.r * 1.2);
            this.particles.splice(i, 1);
          }
        }
      }
    }

    /* ---------------- отрисовка ---------------- */

    drawGround(ctx, cam) {
      // лужи рисуем до персонажей, они лежат на земле
      for (const d of this.decals) {
        if (d.x < cam.left - 40 || d.x > cam.right + 40) continue;
        const gy = this.terrain.groundY(d.x);
        const yy = Math.min(d.y, gy) + 1;
        ctx.globalAlpha = 0.42;
        ctx.fillStyle = "#6f1414";
        ctx.beginPath();
        ctx.ellipse(d.x, yy, d.r * 1.2 + 1, d.r * 0.42 + 0.6, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 0.24;
        ctx.fillStyle = "#3d0b0b";
        ctx.beginPath();
        ctx.ellipse(d.x + d.r * 0.25, yy + 0.8, d.r * 0.62, d.r * 0.22, 0, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    drawParticles(ctx, cam) {
      for (const p of this.particles) {
        if (p.x < cam.left - 30 || p.x > cam.right + 30) continue;
        const k = 1 - p.age / p.life;
        if (p.kind === "blood") {
          ctx.globalAlpha = Math.max(0, Math.min(1, k * 1.6));
          ctx.fillStyle = "#9e1616";
          // вытянутая капля по направлению движения
          const sp = Math.hypot(p.vx, p.vy);
          const len = Math.min(7.5, 2 + sp * 0.009);
          const ang = Math.atan2(p.vy, p.vx);
          ctx.save();
          ctx.translate(p.x, p.y);
          ctx.rotate(ang);
          ctx.beginPath();
          ctx.ellipse(0, 0, len, p.r, 0, 0, Math.PI * 2);
          ctx.fill();
          ctx.restore();
        } else {
          ctx.globalAlpha = k * 0.32;
          ctx.fillStyle = "#b9ab8d";
          ctx.beginPath();
          ctx.arc(p.x, p.y, p.r * (1 + (1 - k) * 1.6), 0, Math.PI * 2);
          ctx.fill();
        }
      }
      ctx.globalAlpha = 1;
    }

    get particleCount() {
      return this.particles.length;
    }
  }

  root.Effects = Effects;
})(typeof globalThis !== "undefined" ? globalThis : this);
