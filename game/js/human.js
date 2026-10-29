(function (root) {
  "use strict";

  const P = root.Physics;
  const Point = P.Point;
  const GRAVITY = P.GRAVITY;

  /* =========================================================
     human.js — персонаж: кинематическая поза + IK-ноги,
     при нокдауне/смерти превращается в физический рэгдолл.
     ========================================================= */

  const BODY = {
    standHeight: 70, // таз над землёй
    torsoLen: 41, // таз -> грудь
    neckLen: 18, // грудь -> голова
    thigh: 34, // таз -> колено
    shin: 32, // колено -> стопа
    arm: 40, // грудь -> кисть
    headR: 11,
    footR: 7,
  };

  const DEFAULT_COLORS = {
    skin: "#e3b18d",
    shirt: "#3f6fa8",
    pants: "#2f3b4d",
    hair: "#2a2118",
    shoe: "#20262f",
  };

  let ID = 0;

  class Human {
    constructor(world, x, opts) {
      opts = opts || {};
      this.world = world;
      this.terrain = world.terrain;
      this.id = ++ID;
      this.name = opts.name || "Human " + this.id;
      this.colors = Object.assign({}, DEFAULT_COLORS, opts.colors || {});
      this.facing = opts.facing || 1;

      this.maxHealth = 100;
      this.health = 100;
      this.dead = false;
      this.mode = "alive"; // alive | ragdoll | dead
      this.ragdollTimer = 0;

      this.rootX = x;
      this.rootY = this.terrain.groundY(x) - BODY.standHeight;
      this.rootVx = 0;
      this.rootVy = 0;
      this.airborne = false;
      this.landTimer = 0;

      this.torsoAngle = 0;
      this.torsoVel = 0;
      this.lean = 0;
      this.walkPhase = 0;
      this.distWalked = 0;
      this.stepTimer = 0;
      this.armSwing = 0;
      this.bloodiness = 0;
      this.damageFlash = 0;
      this.deadTimer = 0;
      this.bleedTimer = 0;
      this.dismembered = { handL: false, handR: false };

      // --- точки скелета ---
      const mk = (dx, dy, o) => {
        const p = new Point(this.rootX + dx, this.rootY + dy, o);
        p.owner = this;
        world.add(p);
        return p;
      };

      this.pelvis = mk(0, 0, { r: 8, mass: 7, friction: 0.55 });
      this.chest = mk(0, -BODY.torsoLen, { r: 9, mass: 6, friction: 0.5 });
      this.head = mk(1 * this.facing, -BODY.torsoLen - BODY.neckLen, { r: BODY.headR, mass: 3.4, friction: 0.42 });

      this.handL = mk(-5, 6, { r: 4.5, mass: 1.1, friction: 0.28 });
      this.handR = mk(5, 6, { r: 4.5, mass: 1.1, friction: 0.28 });

      this.kneeL = mk(-7, 33, { r: 5, mass: 2.6, friction: 0.4 });
      this.footL = mk(-11, BODY.standHeight - 2, { r: BODY.footR, mass: 2, friction: 0.78, restitution: 0 });
      this.kneeR = mk(7, 33, { r: 5, mass: 2.6, friction: 0.4 });
      this.footR = mk(11, BODY.standHeight - 2, { r: BODY.footR, mass: 2, friction: 0.78, restitution: 0 });

      this.parts = {
        pelvis: this.pelvis,
        chest: this.chest,
        head: this.head,
        handL: this.handL,
        handR: this.handR,
        kneeL: this.kneeL,
        footL: this.footL,
        kneeR: this.kneeR,
        footR: this.footR,
      };

      // --- кости ---
      const W = world;
      this.bones = [
        W.link(this.pelvis, this.chest, { stiffness: 1 }),
        W.link(this.chest, this.head, { stiffness: 1 }),
        W.link(this.pelvis, this.head, { stiffness: 0.9, length: (BODY.torsoLen + BODY.neckLen) * 0.96 }),
        W.link(this.chest, this.handL, { stiffness: 1, length: BODY.arm }),
        W.link(this.chest, this.handR, { stiffness: 1, length: BODY.arm }),
        W.link(this.pelvis, this.kneeL, { stiffness: 1, length: BODY.thigh }),
        W.link(this.kneeL, this.footL, { stiffness: 1, length: BODY.shin }),
        W.link(this.pelvis, this.kneeR, { stiffness: 1, length: BODY.thigh }),
        W.link(this.kneeR, this.footR, { stiffness: 1, length: BODY.shin }),
        W.link(this.kneeL, this.kneeR, { stiffness: 0.05 }),
      ];

      // --- ограничители суставов ---
      this.joints = {
        neck: W.joint(this.pelvis, this.chest, this.head, { min: -1.2, max: 1.2 }),
        kneeL: W.joint(this.pelvis, this.kneeL, this.footL, { min: -0.05, max: 2.5 }),
        kneeR: W.joint(this.pelvis, this.kneeR, this.footR, { min: -0.05, max: 2.5 }),
        hipL: W.joint(this.chest, this.pelvis, this.kneeL, { min: -1.5, max: 1.5 }),
        hipR: W.joint(this.chest, this.pelvis, this.kneeR, { min: -1.5, max: 1.5 }),
      };

      // --- цели позы ---
      this.targets = {};
      for (const key in this.parts) {
        this.targets[key] = { x: this.parts[key].x, y: this.parts[key].y };
      }

      this.legs = {
        L: { foot: this.footL, knee: this.kneeL, state: "stance", plantX: this.footL.x, swingT: 0, fromX: 0, toX: 0, offX: -11 },
        R: { foot: this.footR, knee: this.kneeR, state: "stance", plantX: this.footR.x, swingT: 0, fromX: 0, toX: 0, offX: 11 },
      };
    }

    /* ---------------- геттеры ---------------- */

    get x() {
      return this.rootX;
    }
    get y() {
      return this.rootY;
    }
    get speed() {
      return Math.abs(this.rootVx);
    }

    /* ---------------- урон ---------------- */

    damage(amount, hx, hy, dirX, dirY) {
      if (amount <= 0 || this.dead) return;
      this.health -= amount;
      this.damageFlash = Math.min(1, this.damageFlash + amount / 45);
      this.bloodiness = Math.min(1, this.bloodiness + amount / 170);

      const fx = this.world.effects;
      if (fx) {
        fx.bloodBurst(
          hx != null ? hx : this.chest.x,
          hy != null ? hy : this.chest.y,
          Math.min(52, 9 + amount * 1.6),
          dirX || 0,
          dirY || 0
        );
      }

      if (amount > 18 && this.mode === "alive") {
        this.knockdown(1.1 + Math.min(2.2, amount / 55), dirX || -this.facing, dirY || -0.5);
      }
      if (this.health <= 0) this.die();
    }

    knockdown(seconds, dirX, dirY) {
      if (this.mode === "dead") return;
      if (this.mode === "alive") this._enterRagdoll(dirX || 0, (dirY != null ? dirY : -0.3) * 120);
      this.ragdollTimer = Math.max(this.ragdollTimer, seconds);
      this.torsoVel += (dirX || 0) * 1.5;
    }

    die() {
      if (this.dead) return;
      this.health = 0;
      this.dead = true;
      this.deadTimer = 0;
      if (this.mode === "alive") this._enterRagdoll(this.facing * 0.4, -80);
      this.mode = "dead";
      const fx = this.world.effects;
      if (fx) {
        fx.bloodBurst(this.chest.x, this.chest.y, 60, 0, -0.4);
        fx.bloodBurst(this.head.x, this.head.y, 34, 0, -0.6);
      }
    }

    dismember(side) {
      if (this.dismembered[side]) return;
      this.dismembered[side] = true;
      const hand = this.parts[side];
      for (const l of this.world.links) {
        if (l.alive && (l.a === hand || l.b === hand)) l.alive = false;
      }
      const fx = this.world.effects;
      if (fx) fx.bloodBurst(hand.x, hand.y, 55, 0, -0.2);
    }

    /* ---------------- обновление ---------------- */

    update(dt, input) {
      input = input || {};
      this.damageFlash = Math.max(0, this.damageFlash - dt * 3.2);
      this.bloodiness = Math.min(1, this.bloodiness + dt * 0.0035);

      if (this.mode === "alive") {
        this._move(dt, input);
        this._gait(dt);
        this._pose(dt);
        this._servo(dt);
        this._checkImpacts(dt);
      } else {
        this.deadTimer += dt;
        if (this.dead || this.health <= 0) {
          // кровь из ран время от времени
          this.bleedTimer -= dt;
          if (this.bleedTimer <= 0 && this.bloodiness > 0.1) {
            this.bleedTimer = 0.55 + Math.random() * 0.9;
            const fx = this.world.effects;
            const p = Math.random() < 0.5 ? this.chest : this.head;
            if (fx) fx.bloodDrip(p.x, p.y + 6);
          }
        } else {
          this.ragdollTimer -= dt;
          if (this.ragdollTimer <= 0 && this.canStand()) this._exitRagdoll();
        }
      }
    }

    _enterRagdoll(dirX, dirY) {
      this.mode = "ragdoll";
      const vx = this.rootVx * (1 / 120) + (dirX || 0) * 3.2;
      const vy = this.rootVy * (1 / 120) + (dirY || 0) * (1 / 120);
      for (const key in this.parts) {
        const p = this.parts[key];
        p.px = p.x - vx;
        p.py = p.y - vy;
      }
      this.torsoVel = 0;
      const fx = this.world.effects;
      if (fx) fx.bloodBurst(this.chest.x, this.chest.y, 16, dirX || 0, -0.4);
    }

    _exitRagdoll() {
      this.mode = "alive";
      this.rootX = this.pelvis.x;
      this.rootY = this.terrain.groundY(this.rootX) - BODY.standHeight;
      this.rootVx = 0;
      this.rootVy = 0;
      this.airborne = false;
      this.torsoAngle = 0;
      this.torsoVel = 0;
      for (const key in this.legs) {
        const leg = this.legs[key];
        leg.state = "stance";
        leg.plantX = this.rootX + leg.offX;
        leg.swingT = 0;
      }
      this.health = Math.max(this.health, 12);
    }

    canStand() {
      const g = this.terrain.groundY(this.pelvis.x);
      return this.pelvis.y < g - 30 && Math.abs(this.chest.x - this.pelvis.x) < 45;
    }

    /* ---------------- движение корня ---------------- */

    _move(dt, input) {
      const dir = (input.right ? 1 : 0) - (input.left ? 1 : 0);
      if (dir !== 0) this.facing = dir;

      const baseSpeed = input.run ? 340 : 190;
      const want = dir * baseSpeed;
      const accel = this.airborne ? 900 : 2400;
      const dv = want - this.rootVx;
      const maxDv = accel * dt;
      this.rootVx += Math.max(-maxDv, Math.min(maxDv, dv));

      if (Math.abs(this.rootVx) < 2) this.rootVx = 0;

      // прыжок
      if (input.jump && !this.airborne && this.landTimer <= 0) {
        this.rootVy = -400;
        this.airborne = true;
        this.landTimer = 0.12;
      }
      this.landTimer = Math.max(0, this.landTimer - dt);

      this.rootX += this.rootVx * dt;
      this.rootX = Math.max(12, Math.min(this.terrain.width - 12, this.rootX));

      const groundY = this.terrain.groundY(this.rootX);
      const floor = groundY - BODY.standHeight;

      if (this.airborne) {
        this.rootVy += GRAVITY * dt;
        this.rootY += this.rootVy * dt;
        if (this.rootY >= floor) {
          const impact = this.rootVy;
          this.rootY = floor;
          this.rootVy = 0;
          this.airborne = false;
          const fx = this.world.effects;
          if (fx) fx.dust(this.rootX, groundY, 6, 0);
          if (impact > 900) {
            this.damage((impact - 900) * 0.12, this.rootX, groundY, 0, 0);
          }
        }
      } else {
        this.rootY += (floor - this.rootY) * Math.min(1, dt * 22);
      }

      // наклон корпуса
      const leanTarget =
        (dir !== 0 ? 0.16 : 0) * this.facing * Math.min(1, Math.abs(this.rootVx) / 220) +
        (this.airborne ? 0.1 * this.facing : 0);
      this.lean += (leanTarget - this.lean) * Math.min(1, dt * 7);

      const k = 130;
      const c = 2 * Math.sqrt(k) * 0.9;
      this.torsoVel += (k * (this.lean - this.torsoAngle) - c * this.torsoVel) * dt;
      this.torsoAngle += this.torsoVel * dt;
      this.torsoAngle = Math.max(-0.9, Math.min(0.9, this.torsoAngle));
    }

    /* ---------------- походка ---------------- */

    _gait(dt) {
      const moving = Math.abs(this.rootVx) > 18;
      const speedN = Math.min(1, Math.abs(this.rootVx) / 200);
      const stride = 26 + speedN * 26;
      this.stepTimer = Math.max(0, this.stepTimer - dt);

      if (this.airborne) {
        for (const key of ["L", "R"]) {
          const leg = this.legs[key];
          leg.state = "stance";
          leg.plantX = this.rootX + leg.offX;
        }
        return;
      }

      for (const key of ["L", "R"]) {
        const leg = this.legs[key];
        const foot = leg.foot;
        const plantedGroundY = this.terrain.groundY(leg.plantX) - BODY.footR - 1;

        if (leg.state === "stance") {
          // стопа держится на месте, пока не пора шагнуть
          const behind = (this.rootX - leg.plantX) * this.facing;
          const others = this.legs[key === "L" ? "R" : "L"];
          const needStep =
            (moving && behind > stride * 0.55 && this.stepTimer <= 0 && others.state !== "swing") ||
            Math.abs(this.rootX - leg.plantX) > stride * 1.25;

          if (needStep) {
            leg.state = "swing";
            leg.swingT = 0;
            leg.fromX = leg.plantX;
            leg.toX = this.rootX + this.facing * stride * (this.facing * this.rootVx >= 0 ? 0.62 : 0.3);
            this.stepTimer = 0.16;
          } else {
            const tx = leg.plantX;
            const ty = plantedGroundY;
            this.targets[key === "L" ? "footL" : "footR"] = { x: tx, y: ty };
            const kx = (this.rootX + tx) / 2 + this.facing * 7;
            const ky = (this.rootY + ty) / 2;
            this.targets[key === "L" ? "kneeL" : "kneeR"] = { x: kx, y: ky };
          }
        }

        if (leg.state === "swing") {
          leg.swingT += dt / 0.17;
          const t = Math.min(1, leg.swingT);
          const e = t * t * (3 - 2 * t);
          const x = leg.fromX + (leg.toX - leg.fromX) * e;
          const gy = this.terrain.groundY(x) - BODY.footR - 1;
          const lift = Math.sin(Math.PI * t) * (13 + 12 * speedN);
          const y = gy - lift;
          this.targets[key === "L" ? "footL" : "footR"] = { x, y };
          const kx = (this.rootX + x) / 2 + this.facing * 8;
          const ky = (this.rootY + y) / 2;
          this.targets[key === "L" ? "kneeL" : "kneeR"] = { x: kx, y: ky };

          if (t >= 1) {
            leg.state = "stance";
            leg.plantX = x;
            leg.swingT = 0;
            const fx = this.world.effects;
            if (fx) fx.dust(x, this.terrain.groundY(x), 3, this.facing);
          }
        }
      }

      if (moving) this.walkPhase += dt * (4.5 + speedN * 5);
      const swing = Math.sin(this.walkPhase) * 0.55 * (moving ? 1 : 0);
      this.armSwing += (swing - this.armSwing) * Math.min(1, dt * 12);
    }

    /* ---------------- поза ---------------- */

    _pose(dt) {
      const a = this.torsoAngle;
      const upX = Math.sin(a) * this.facing;
      const upY = -Math.cos(a);
      const sideX = Math.cos(a) * this.facing;
      const sideY = Math.sin(a);

      const t = this.targets;
      t.pelvis = { x: this.rootX, y: this.rootY };

      // грудь и голова на оси корпуса
      const chestX = this.rootX + upX * BODY.torsoLen;
      const chestY = this.rootY + upY * BODY.torsoLen;
      t.chest = { x: chestX, y: chestY };

      const headX = chestX + upX * BODY.neckLen + this.facing * 1;
      const headY = chestY + upY * BODY.neckLen;
      t.head = { x: headX, y: headY };

      // руки машут в такт
      const swing = this.armSwing;
      const air = this.airborne ? 1 : 0;
      const push = this.world.effects && this.airborne ? 0 : 0;

      const hands = [
        { key: "handL", side: -1, sw: swing },
        { key: "handR", side: 1, sw: -swing },
      ];
      const downX = -upX;
      const downY = -upY;
      for (const h of hands) {
        // плечо
        const sx = chestX + upX * 3 + sideX * 5.5 * h.side;
        const sy = chestY + upY * 3 + sideY * 5.5 * h.side;
        // кисть чуть впереди корпуса, качается вдоль взгляда
        let hx = sx + sideX * (h.sw * 18) + downX * 37 + sideX * 2 * h.side;
        let hy = sy + sideY * (h.sw * 18) + downY * 37 + sideY * 2 * h.side;
        if (air) {
          hx = sx + upX * 14 + sideX * 7;
          hy = sy + upY * 14 + sideY * 7;
        }
        if (h.side < 0) {
          // дальняя рука уходит за корпус
          hx -= this.facing * 7;
          hy -= 1;
        }
        t[h.key] = { x: hx, y: hy };
      }

      // таз чуть смещён по ходу движения
      t.pelvis.x += this.facing * 1;
    }

    /* ---------------- серво: тянем точки к целям ---------------- */

    _servo(dt) {
      const stiff = {
        pelvis: 0.9,
        chest: 0.75,
        head: 0.62,
        handL: 0.4,
        handR: 0.4,
        kneeL: 0.55,
        kneeR: 0.55,
        footL: 0.75,
        footR: 0.75,
      };

      for (const key in this.targets) {
        const p = this.parts[key];
        const tg = this.targets[key];
        // цель не должна уходить под землю
        const gy = this.terrain.groundY(tg.x);
        const minY = gy - p.r - 0.5;
        const ty = Math.min(tg.y, minY);

        const s = 1 - Math.pow(1 - stiff[key], dt * 60);
        p.x += (tg.x - p.x) * s;
        p.y += (ty - p.y) * s;
        // обнуляем накопленную скорость — позиция кинематическая
        p.px = p.x - (p.x - p.px) * 0.15;
        p.py = p.y - (p.y - p.py) * 0.15;
      }
    }

    /* ---------------- падения и урон ---------------- */

    _checkImpacts(dt) {
      // урон только от ударов корпусом/головой — шаги и приседания не считаются
      for (const key of ["head", "chest", "pelvis"]) {
        const p = this.parts[key];
        if (p.impact > 4.2) {
          const speed = p.impact * 120; // px/s
          const dmg = (speed - 500) * 0.05 * (key === "head" ? 2.2 : 1);
          if (dmg > 1.5) this.damage(dmg, p.x, p.y, 0, 0);
        }
      }
    }

    /* ---------------- отрисовка ---------------- */

    draw(ctx, cam) {
      if (this.pelvis.x < cam.left - 220 || this.pelvis.x > cam.right + 220) return;

      const palette = makePalette(this.colors, this.bloodiness);
      const facing = this.facing;
      const pelvis = this.pelvis;
      const chest = this.chest;
      const head = this.head;

      // --- тень ---
      const gy = this.terrain.groundY(pelvis.x);
      ctx.globalAlpha = 0.18;
      ctx.fillStyle = "#000";
      ctx.beginPath();
      ctx.ellipse(pelvis.x, gy + 1, 19, 4.2, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;

      // --- система координат корпуса ---
      const frame = bodyFrame(pelvis, chest, facing);
      const { frontX, frontY, upX, upY } = frame;

      const shoulderNear = {
        x: chest.x - upX * 2 + frontX * 3.2,
        y: chest.y - upY * 2 + frontY * 3.2,
      };
      const shoulderFar = {
        x: chest.x - upX * 2 - frontX * 2.4,
        y: chest.y - upY * 2 - frontY * 2.4,
      };

      // --- дальняя рука (за корпусом) ---
      drawArm(ctx, shoulderFar, this.handL, facing, -1, palette.armFar, palette.skinFar, palette);

      // --- дальняя нога ---
      drawLeg(ctx, pelvis, this.kneeL, this.footL, facing, palette.pantsFar, palette.shoeFar, -1);

      // --- корпус ---
      drawTorso(ctx, pelvis, chest, frame, palette);

      // --- шея ---
      const neckFrom = { x: chest.x + upX * 4, y: chest.y + upY * 4 };
      const neckTo = {
        x: chest.x + (head.x - chest.x) * 0.72,
        y: chest.y + (head.y - chest.y) * 0.72,
      };
      capsule(ctx, neckFrom, neckTo, 5.4, 4.8, palette.skinShade);

      // --- воротник ---
      const collar = { x: chest.x + upX * 2.4, y: chest.y + upY * 2.4 };
      circle(ctx, collar.x + frontX * 3, collar.y + frontY * 3, 3.6, palette.shirt);
      circle(ctx, collar.x - frontX * 3, collar.y - frontY * 3, 3.6, palette.shirt);

      // --- голова ---
      drawHead(ctx, head, facing, palette, this.dead, this.colors.hair);

      // --- ближняя нога ---
      drawLeg(ctx, pelvis, this.kneeR, this.footR, facing, palette.pants, palette.shoe, 1);

      // --- ближняя рука ---
      drawArm(ctx, shoulderNear, this.handR, facing, 1, palette.shirt, palette.skin, palette);

      if (this.dismembered.handL) circle(ctx, this.handL.x, this.handL.y, 5, palette.skinFar);
      if (this.dismembered.handR) circle(ctx, this.handR.x, this.handR.y, 5, palette.skin);

      if (this.damageFlash > 0.02) {
        ctx.globalAlpha = Math.min(0.45, this.damageFlash);
        circle(ctx, chest.x, chest.y, 27, "#ff3b3b");
        ctx.globalAlpha = 1;
      }

      // --- полоска здоровья ---
      if (!this.dead) {
        const w = 36;
        const hh = 5;
        const hx0 = head.x - w / 2;
        const hy0 = head.y - BODY.headR - 20;
        ctx.fillStyle = "rgba(8,12,18,0.7)";
        roundRect(ctx, hx0 - 1, hy0 - 1, w + 2, hh + 2, 3);
        ctx.fill();
        const hp = Math.max(0, this.health) / this.maxHealth;
        ctx.fillStyle = hp > 0.6 ? "#5ad46a" : hp > 0.3 ? "#e8c14a" : "#e2513f";
        roundRect(ctx, hx0, hy0, w * hp, hh, 2.5);
        ctx.fill();
      }

      // --- отладка: скелет ---
      if (this.debugSkeleton) {
        ctx.strokeStyle = "rgba(0,255,200,0.6)";
        ctx.lineWidth = 1;
        for (const l of this.bones) {
          if (!l.alive) continue;
          ctx.beginPath();
          ctx.moveTo(l.a.x, l.a.y);
          ctx.lineTo(l.b.x, l.b.y);
          ctx.stroke();
        }
        for (const k in this.parts) {
          const p = this.parts[k];
          circle(ctx, p.x, p.y, 2.5, "rgba(255,80,80,0.9)");
        }
      }
    }
  }

  /* ---------------- геометрия и рисование ---------------- */

  function bodyFrame(pelvis, chest, facing) {
    let dx = chest.x - pelvis.x;
    let dy = chest.y - pelvis.y;
    const len = Math.hypot(dx, dy) || 1;
    dx /= len;
    dy /= len;
    return {
      upX: dx,
      upY: dy,
      // «перед» — перпендикуляр к оси корпуса в сторону взгляда
      frontX: -dy * facing,
      frontY: dx * facing,
    };
  }

  /** двухзвенная IK: положение локтя/колена */
  function ik2(root, target, l1, l2, bend) {
    let dx = target.x - root.x;
    let dy = target.y - root.y;
    let d = Math.hypot(dx, dy);
    const maxD = (l1 + l2) * 0.998;
    if (d < 0.001) {
      dx = 0;
      dy = 1;
      d = 0.001;
    }
    if (d > maxD) {
      const k = maxD / d;
      dx *= k;
      dy *= k;
      d = maxD;
    }
    const ux = dx / d;
    const uy = dy / d;
    const a = (d * d + l1 * l1 - l2 * l2) / (2 * d);
    const h = Math.sqrt(Math.max(0, l1 * l1 - a * a));
    const nx = -uy;
    const ny = ux;
    return {
      x: root.x + ux * a + nx * h * bend,
      y: root.y + uy * a + ny * h * bend,
      handX: root.x + ux * d,
      handY: root.y + uy * d,
    };
  }

  /** капсула между двумя точками с разной толщиной на концах */
  function capsule(ctx, a, b, wA, wB, color) {
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const len = Math.hypot(dx, dy) || 1;
    const nx = -dy / len;
    const ny = dx / len;
    ctx.beginPath();
    ctx.moveTo(a.x + nx * wA, a.y + ny * wA);
    ctx.lineTo(b.x + nx * wB, b.y + ny * wB);
    ctx.arc(b.x, b.y, wB, Math.atan2(ny, nx), Math.atan2(-ny, -nx), true);
    ctx.lineTo(a.x - nx * wA, a.y - ny * wA);
    ctx.arc(a.x, a.y, wA, Math.atan2(-ny, -nx), Math.atan2(ny, nx), true);
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.fill();
  }

  const ARM_UPPER = 19;
  const ARM_FORE = 21;

  function drawArm(ctx, shoulder, hand, facing, side, sleeve, skin, palette) {
    // локоть слегка назад, как у расслабленной руки
    const joint = ik2(shoulder, hand, ARM_UPPER, ARM_FORE, -facing * 0.22);
    const elbow = { x: joint.x, y: joint.y };
    const tip = { x: joint.handX, y: joint.handY };

    capsule(ctx, shoulder, elbow, 5.4, 4.2, sleeve);
    capsule(ctx, elbow, tip, 4.0, 3.5, skin);
    capsule(ctx, tip, { x: tip.x - facing * 1.2, y: tip.y + 4.4 }, 3.6, 3.0, skin);
  }

  function drawLeg(ctx, pelvis, knee, foot, facing, pants, shoe, side) {
    capsule(ctx, pelvis, knee, 6.9, 5.2, pants);
    capsule(ctx, knee, foot, 5.0, 3.7, pants);
    // обувь: слегка повторяет наклон голени, но остаётся «стоящей»
    const ang = Math.atan2(foot.y - knee.y, foot.x - knee.x) - Math.PI / 2;
    const tilt = Math.max(-0.45, Math.min(0.45, ang));
    ctx.save();
    ctx.translate(foot.x, foot.y + 0.5);
    ctx.rotate(tilt);
    ctx.fillStyle = shoe;
    roundRect(ctx, -5.5, -4.6, 15 * facing, 9.2, 4.2);
    ctx.fill();
    // носок обуви
    ctx.beginPath();
    ctx.ellipse(facing * 8, 0, 4.2, 4.6, 0, 0, Math.PI * 2);
    ctx.fill();
    // подошва
    ctx.fillStyle = "rgba(0,0,0,0.35)";
    roundRect(ctx, -5.5, 3.2, 15 * facing, 2.2, 1.1);
    ctx.fill();
    ctx.restore();
  }

  function drawTorso(ctx, pelvis, chest, frame, palette) {
    const { upX, upY, frontX, frontY } = frame;
    const len = Math.hypot(chest.x - pelvis.x, chest.y - pelvis.y) || 1;

    // полуширины: бёдра -> талия -> грудь -> плечи
    const wHip = 8.6;
    const wWaist = 7.4;
    const wChest = 10.3;
    const wShoulder = 11.4;

    const pt = (t, w) => ({
      x: pelvis.x + upX * len * t + frontX * w,
      y: pelvis.y + upY * len * t + frontY * w,
    });

    const hipF = pt(-0.12, wHip + 0.8);
    const hipB = pt(-0.12, -(wHip + 0.8));
    const waistF = pt(0.44, wWaist);
    const waistB = pt(0.44, -wWaist);
    const chestF = pt(0.84, wChest);
    const chestB = pt(0.84, -wChest);
    const shoulderF = pt(0.99, wShoulder);
    const shoulderB = pt(0.99, -wShoulder);

    const shape = () => {
      ctx.beginPath();
      ctx.moveTo(hipF.x, hipF.y);
      ctx.quadraticCurveTo(waistF.x, waistF.y, chestF.x, chestF.y);
      ctx.quadraticCurveTo(shoulderF.x + upX * 2, shoulderF.y + upY * 2, shoulderF.x, shoulderF.y);
      ctx.quadraticCurveTo(
        chest.x + upX * 5,
        chest.y + upY * 5,
        shoulderB.x,
        shoulderB.y
      );
      ctx.quadraticCurveTo(shoulderB.x - upX * 1, shoulderB.y - upY * 1, chestB.x, chestB.y);
      ctx.quadraticCurveTo(waistB.x, waistB.y, hipB.x, hipB.y);
      ctx.quadraticCurveTo(
        pelvis.x - upX * 8,
        pelvis.y - upY * 8,
        hipF.x,
        hipF.y
      );
      ctx.closePath();
    };

    // футболка
    shape();
    ctx.fillStyle = palette.shirt;
    ctx.fill();

    // объём
    ctx.save();
    shape();
    ctx.clip();
    ctx.globalAlpha = 0.5;
    ctx.fillStyle = palette.shirtLight;
    ctx.beginPath();
    ctx.ellipse(
      chest.x + frontX * 4.5 - upX * 3,
      chest.y + frontY * 4.5 - upY * 3,
      6.5,
      15,
      Math.atan2(upY, upX) - Math.PI / 2,
      0,
      Math.PI * 2
    );
    ctx.fill();
    ctx.globalAlpha = 0.18;
    ctx.fillStyle = "#000";
    ctx.beginPath();
    ctx.ellipse(
      chest.x - frontX * 5,
      chest.y - frontY * 5 - 2,
      6,
      17,
      Math.atan2(upY, upX) - Math.PI / 2,
      0,
      Math.PI * 2
    );
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.restore();

    // шорты: пояс от талии вниз, поверх бёдер
    const sF = pt(0.16, wHip + 0.4);
    const sB = pt(0.16, -(wHip + 0.4));
    const bF = pt(-0.12, wHip + 1.0);
    const bB = pt(-0.12, -(wHip + 1.0));
    const bottomF = pt(-0.42, wHip - 1.2);
    const bottomB = pt(-0.42, -(wHip - 1.2));
    ctx.beginPath();
    ctx.moveTo(sF.x, sF.y);
    ctx.quadraticCurveTo(bF.x, bF.y, bottomF.x, bottomF.y);
    ctx.lineTo(bottomB.x, bottomB.y);
    ctx.quadraticCurveTo(bB.x, bB.y, sB.x, sB.y);
    ctx.closePath();
    ctx.fillStyle = palette.pants;
    ctx.fill();

    // резинка
    ctx.strokeStyle = "rgba(0,0,0,0.2)";
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.moveTo(sF.x, sF.y);
    ctx.lineTo(sB.x, sB.y);
    ctx.stroke();
  }

  function drawHead(ctx, head, facing, palette, dead, hairColor) {
    const r = BODY.headR;
    // лицо
    circle(ctx, head.x, head.y, r, palette.skin);
    // ухо
    circle(ctx, head.x - facing * r * 0.66, head.y + 0.5, 2.1, palette.skinShade);

    // причёска
    ctx.beginPath();
    ctx.arc(head.x, head.y - 1.4, r + 0.6, Math.PI * 0.98, Math.PI * 2.02);
    ctx.fillStyle = hairColor || palette.hair;
    ctx.fill();
    ctx.beginPath();
    ctx.arc(head.x - facing * r * 0.48, head.y - 3, r * 0.6, 0, Math.PI * 2);
    ctx.fill();

    // глаз
    const ex = head.x + facing * r * 0.42;
    const ey = head.y + 1.2;
    if (dead) {
      ctx.strokeStyle = "#20232b";
      ctx.lineWidth = 1.7;
      ctx.beginPath();
      ctx.moveTo(ex - 2.4, ey - 2.4);
      ctx.lineTo(ex + 2.4, ey + 2.4);
      ctx.moveTo(ex + 2.4, ey - 2.4);
      ctx.lineTo(ex - 2.4, ey + 2.4);
      ctx.stroke();
    } else {
      circle(ctx, ex, ey, 1.9, "#20232b");
      circle(ctx, ex + 0.55, ey - 0.65, 0.7, "#ffffff");
      // бровь
      ctx.strokeStyle = "rgba(40,30,20,0.55)";
      ctx.lineWidth = 1.1;
      ctx.beginPath();
      ctx.moveTo(ex - facing * 2, ey - 4.2);
      ctx.lineTo(ex + facing * 2.4, ey - 5);
      ctx.stroke();
    }
  }

  function circle(ctx, x, y, r, color) {
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fillStyle = color;
    ctx.fill();
  }

  function roundRect(ctx, x, y, w, h, r) {
    const rr = Math.min(r, Math.abs(w) / 2, Math.abs(h) / 2);
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

  function hexToRgb(hex) {
    const h = hex.replace("#", "");
    const n = parseInt(h.length === 3 ? h.split("").map((c) => c + c).join("") : h, 16);
    return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 };
  }

  function mix(a, b, t) {
    if (a.startsWith("rgb")) return a;
    const A = hexToRgb(a);
    const B = hexToRgb(b);
    return `rgb(${Math.round(A.r + (B.r - A.r) * t)},${Math.round(A.g + (B.g - A.g) * t)},${Math.round(
      A.b + (B.b - A.b) * t
    )})`;
  }

  function shade(color, amount) {
    if (color.startsWith("rgb")) {
      const m = color.match(/(\d+)\D+(\d+)\D+(\d+)/);
      if (!m) return color;
      const f = (v) => Math.max(0, Math.min(255, Math.round(Number(v) * (1 + amount))));
      return `rgb(${f(m[1])},${f(m[2])},${f(m[3])})`;
    }
    return mix(color, amount < 0 ? "#000000" : "#ffffff", Math.abs(amount));
  }

  function makePalette(C, blood) {
    const b = Math.max(0, Math.min(1, blood));
    const skin = mix(mix(C.skin, "#8f2a22", b * 0.55), "#000", 0);
    const shirt = mix(C.shirt, "#6b1414", b * 0.5);
    const pants = mix(C.pants, "#4a1010", b * 0.4);
    return {
      skin,
      skinFar: shade(skin, -0.22),
      skinShade: shade(skin, -0.14),
      shirt,
      shirtFar: shade(shirt, -0.26),
      pants,
      pantsFar: shade(pants, -0.34),
      pantsLight: shade(pants, 0.08),
      hair: C.hair,
      shirtLight: shade(shirt, 0.12),
      armFar: shade(shirt, -0.1),
      outline: shade(shirt, -0.35),
      shoe: C.shoe,
      shoeFar: shade(C.shoe, -0.25),
    };
  }

  root.Human = Human;
  root.HumanBody = BODY;
  root.DrawHelpers = { circle, roundRect, mix, shade, hexToRgb, capsule, ik2, bodyFrame };
})(typeof globalThis !== "undefined" ? globalThis : this);
