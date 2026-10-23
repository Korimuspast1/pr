import {
  GROUND_Y, VIEW_W, TAU, clamp, lerp, rand, chance, pick, dist,
  approachAngle, angleDiff, segPointDist, shade, roundRect,
} from './util.js';
import { WEAPON_BY_ID, WEAPONS, drawWeapon } from './weapons.js';

const SKIN_TONES = ['#d8a179', '#c78e6a', '#e8b896', '#b07a55', '#8d5c3d', '#6d4530'];
const CLOTH = [
  { a: '#43587a', b: '#2d3f59' }, { a: '#6b4a7a', b: '#4a3156' },
  { a: '#3f6b52', b: '#284a37' }, { a: '#7d5637', b: '#5a3b22' },
  { a: '#5b5f66', b: '#3b3e44' }, { a: '#7a3b3b', b: '#4e2424' },
  { a: '#b9a479', b: '#8a774d' },
];
const PANTS = ['#333a46', '#2f3d55', '#3a3a3f', '#4a3a2a', '#2b3340'];
const HAIR = ['#2a1d16', '#3d2a1a', '#161311', '#5a3a22', '#7a5a35', '#a8a29a'];
const NAMES = ['Марк', 'Таня', 'Кирилл', 'Лена', 'Гоша', 'Рита', 'Дима', 'Соня', 'Витя', 'Юля',
  'Артём', 'Настя', 'Пётр', 'Оля', 'Серёга', 'Маша', 'Илья', 'Вера', 'Костя', 'Даша'];

const LIMB_DEFS = [
  { name: 'head', label: 'голова', radius: 5.6, kind: 'skin', bone: 'skull', mass: 5, parent: 'torso' },
  { name: 'torso', label: 'торс', radius: 4.8, kind: 'cloth', bone: 'spine', mass: 26, parent: null },
  { name: 'armN_u', label: 'плечо (ближ.)', radius: 2.8, kind: 'cloth', bone: 'arm', mass: 3, parent: 'torso' },
  { name: 'armN_l', label: 'предплечье (ближ.)', radius: 2.3, kind: 'skin', bone: 'arm', mass: 2, parent: 'armN_u' },
  { name: 'armF_u', label: 'плечо (дальн.)', radius: 2.8, kind: 'cloth', bone: 'arm', mass: 3, parent: 'torso' },
  { name: 'armF_l', label: 'предплечье (дальн.)', radius: 2.3, kind: 'skin', bone: 'arm', mass: 2, parent: 'armF_u' },
  { name: 'legN_u', label: 'бедро (ближ.)', radius: 3.4, kind: 'cloth', bone: 'leg', mass: 7, parent: 'torso' },
  { name: 'legN_l', label: 'голень (ближ.)', radius: 2.8, kind: 'cloth', bone: 'leg', mass: 4, parent: 'legN_u' },
  { name: 'legF_u', label: 'бедро (дальн.)', radius: 3.4, kind: 'cloth', bone: 'leg', mass: 7, parent: 'torso' },
  { name: 'legF_l', label: 'голень (дальн.)', radius: 2.8, kind: 'cloth', bone: 'leg', mass: 4, parent: 'legF_u' },
];

const BONES = [
  ['pelvis', 'chest'], ['chest', 'neck'], ['neck', 'head'],
  ['chest', 'shN'], ['shN', 'elN'], ['elN', 'wrN'],
  ['chest', 'shF'], ['shF', 'elF'], ['elF', 'wrF'],
  ['pelvis', 'hipN'], ['hipN', 'knN'], ['knN', 'anN'],
  ['pelvis', 'hipF'], ['hipF', 'knF'], ['knF', 'anF'],
  ['shN', 'shF'], ['hipN', 'hipF'], ['pelvis', 'neck'],
];

const POINTS = ['pelvis', 'chest', 'neck', 'head', 'shN', 'elN', 'wrN', 'shF', 'elF', 'wrF',
  'hipN', 'knN', 'anN', 'hipF', 'knF', 'anF'];

export const SKIN_PRESETS = SKIN_TONES;
export const CLOTH_PRESETS = CLOTH;
export const PANT_PRESETS = PANTS;
export const HAIR_PRESETS = HAIR;
export const NAME_PRESETS = NAMES;

let NEXT_ID = 1;

export class Person {
  constructor(game, opts = {}) {
    this.game = game;
    this.id = NEXT_ID++;
    this.name = opts.name || pick(NAMES);
    this.skin = opts.skin || pick(SKIN_TONES);
    this.cloth = opts.cloth || pick(CLOTH);
    this.pants = opts.pants || pick(PANTS);
    this.hair = opts.hair || pick(HAIR);
    this.weapon = WEAPONS[0];

    this.pts = {};
    for (const k of POINTS) this.pts[k] = { x: 0, y: 0, vx: 0, vy: 0 };
    this.limbs = [];

    this.ai = {
      enabled: opts.ai !== false,
      aggression: rand(0.35, 0.62),
      timer: rand(0, 1), target: null, retarget: 0, aimErr: rand(-0.16, 0.16),
      reaction: rand(0.2, 0.55), state: 'idle',
    };

    this.reset(opts.x != null ? opts.x : rand(80, VIEW_W - 80), opts.weapon || 'fists');
    if (opts.hp) this.health = opts.hp;
    this.createdAt = game ? game.time : 0;
  }

  reset(x, weaponId) {
    this.pos = { x, y: GROUND_Y };
    this.vel = { x: 0, y: 0 };
    this.facing = chance(0.5) ? 1 : -1;
    this.aimAngle = this.facing > 0 ? 0.15 : Math.PI - 0.15;
    this.weaponAngle = this.aimAngle;
    this.aimKick = 0;
    this.extraSpread = 0;
    this.sprayHeat = 0;
    this.health = 100;
    this.blood = 5000;
    this.alive = true;
    this.deadTime = 0;
    this.deathCause = '';
    this.ragdoll = false;
    this.ragTime = 0;
    this.moving = false;
    this.walkPhase = rand(0, TAU);
    this.animT = 0;
    this.fireCooldown = 0;
    this.swing = null;
    this.prevBlade = null;
    this.reloading = 0;
    this.triggerHeld = false;
    this.hitFlash = 0;
    this.pain = 0;
    this.lastAttacker = null;
    this.stepT = 0;
    this.breath = rand(0, TAU);
    this.poolT = 0;
    this.bleedTotal = 0;
    this.stumpBleed = 0;
    this.bloody = 0;
    this.unconscious = false;
    this.sleeping = false;
    this.sleepTimer = 0;
    this.deathTime = 0;
    this.restBones = null;
    this.limbs.length = 0;
    this.mags = {};
    for (const w of WEAPONS) if (w.magSize) this.mags[w.id] = w.magSize;
    this.weapon = WEAPON_BY_ID[weaponId] || WEAPONS[0];
    for (const d of LIMB_DEFS) {
      this.limbs.push({
        id: this.id * 100 + this.limbs.length,
        name: d.name, label: d.label, radius: d.radius, kind: d.kind, bone: d.bone,
        mass: d.mass, parent: d.parent,
        a: { x: 0, y: 0 }, b: { x: 0, y: 0 },
        health: 100, struct: 0, severed: false, parentSevered: false,
        broken: false, wounds: [], arterial: false,
        bleedRate: 0, exposed: false,
      });
    }
    this.bindLimbs();
    this.pose(0);
  }

  bindLimbs() {
    const L = this.limbs, P = this.pts;
    const f = (n) => L.find((l) => l.name === n);
    let l = f('head'); l.a = P.neck; l.b = P.head;
    l = f('torso'); l.a = P.pelvis; l.b = P.neck;
    l = f('armN_u'); l.a = P.shN; l.b = P.elN;
    l = f('armN_l'); l.a = P.elN; l.b = P.wrN;
    l = f('armF_u'); l.a = P.shF; l.b = P.elF;
    l = f('armF_l'); l.a = P.elF; l.b = P.wrF;
    l = f('legN_u'); l.a = P.hipN; l.b = P.knN;
    l = f('legN_l'); l.a = P.knN; l.b = P.anN;
    l = f('legF_u'); l.a = P.hipF; l.b = P.knF;
    l = f('legF_l'); l.a = P.knF; l.b = P.anF;
  }

  get limb() {
    const o = {};
    for (const l of this.limbs) o[l.name] = l;
    return o;
  }

  setWeapon(id) {
    const w = WEAPON_BY_ID[id];
    if (!w) return;
    this.weapon = w;
    this.swing = null;
    this.reloading = 0;
  }

  handPos() {
    return this.pts.wrN;
  }

  headPos() {
    return this.ragdoll ? this.pts.head : this.pts.head;
  }

  /* ================= AI ================= */

  updateAI(dt, game) {
    const ai = this.ai;
    this.triggerHeld = false;
    if (!this.alive) return;
    if (!ai.enabled) { this.idleWander(dt); return; }
    if (this.ragdoll) { this.tryStandUp(); return; }
    if (!this.legsUsable()) { this.vel.x *= 0.9; this.moving = false; return; }

    ai.retarget -= dt;
    if (!ai.target || !ai.target.alive || ai.retarget <= 0) {
      const all = game.humans.filter((e) => e !== this && e.alive);
      let candidates = all;
      if (all.length > 1 && chance(1 - ai.aggression)) {
        candidates = all.filter((e) => e !== this.lastAttacker);
      }
      let best = null, bestScore = -Infinity;
      for (const c of candidates) {
        let score = -dist(this.pos.x, 0, c.pos.x, 0) + rand(-60, 60);
        if (c === this.lastAttacker) score += 400;
        if (score > bestScore) { bestScore = score; best = c; }
      }
      ai.target = best;
      ai.retarget = rand(4, 8);
      ai.aimErr = rand(-0.18, 0.18);
    }

    const t = ai.target;
    if (!t) { this.idleWander(dt); return; }

    const dx = t.pts.chest.x - this.pos.x;
    const dy = t.pts.chest.y - this.pts.chest.y;
    const d = Math.hypot(dx, dy);
    const wounded = this.health < 32 || this.blood < 1600;
    const meleeW = this.weapon.kind === 'melee';

    if (wounded && chance(dt * 0.6)) ai.state = 'flee';
    if (ai.state === 'flee' && !wounded) ai.state = 'fight';

    if (ai.state === 'flee') {
      const dir = Math.sign(this.pos.x - t.pos.x) || 1;
      this.vel.x += clamp(dir * 80 - this.vel.x, -600 * dt, 600 * dt);
      this.moving = true;
      this.pos.x = clamp(this.pos.x + this.vel.x * dt, 12, VIEW_W - 12);
      this.facing = dir;
      this.aimAngle = Math.atan2(dy, dx);
      this.weaponAngle = this.aimAngle;
      return;
    }

    const wantDist = meleeW ? 22 : (this.weapon.id === 'shotgun' ? 75 : 135);
    if (d > wantDist * 1.08) {
      const dir = Math.sign(dx) || 1;
      this.vel.x += clamp(dir * 64 - this.vel.x, -520 * dt, 520 * dt);
      this.moving = true;
      this.facing = dir;
    } else {
      this.vel.x *= Math.exp(-10 * dt);
      this.moving = Math.abs(this.vel.x) > 8;
    }
    this.pos.x = clamp(this.pos.x + this.vel.x * dt, 12, VIEW_W - 12);

    ai.reaction -= dt;
    const noise = Math.sin(game.time * 1.7 + this.id) * 0.02 + Math.sin(game.time * 5.3 + this.id * 2) * 0.012;
    const wantAngle = Math.atan2(t.pts.chest.y - this.pts.shN.y, t.pts.chest.x - this.pts.shN.x) + ai.aimErr + noise;
    this.aimAngle = approachAngle(this.aimAngle, wantAngle, 5.5 * dt);
    this.facing = Math.cos(this.aimAngle) >= 0 ? 1 : -1;

    if (ai.reaction <= 0) {
      const aimOk = Math.abs(angleDiff(this.aimAngle, Math.atan2(t.pts.chest.y - this.pts.shN.y, t.pts.chest.x - this.pts.shN.x))) < 0.22;
      if (meleeW) {
        if (d < 30 && aimOk) this.triggerHeld = true;
      } else {
        if (aimOk && d < 280) this.triggerHeld = chance(dt * (this.weapon.auto ? 2.4 : 1.3));
        if (this.mags[this.weapon.id] === 0 && this.reloading <= 0) this.startReload();
      }
    }
  }

  idleWander(dt) {
    this.vel.x *= Math.exp(-6 * dt);
    this.moving = false;
    this.weaponAngle = this.aimAngle;
  }

  /* ================= weapons ================= */

  tryAttack() {
    if (!this.alive || this.ragdoll || !this.armUsable() || this.reloading > 0) return;
    const w = this.weapon;
    if (this.fireCooldown > 0) return;
    if (w.kind === 'melee') {
      if (this.swing) return;
      this.swing = { t: 0, dur: w.swingTime, def: w, hitDone: new Set() };
      this.prevBlade = null;
      this.game.audio.slash();
      this.fireCooldown = w.swingTime * 0.9;
      return;
    }
    const mag = this.mags[w.id] | 0;
    if (mag <= 0) {
      this.game.audio.empty();
      this.fireCooldown = 0.25;
      this.startReload();
      return;
    }
    this.mags[w.id] = mag - 1;
    this.fireCooldown = 60 / w.rpm;
    this.sprayHeat = clamp(this.sprayHeat + (w.auto ? 0.011 : 0.006), 0, 0.11);
    this.game.combat.fire(this, w, this.weaponAngle);
  }

  kick(def) {
    this.aimKick += def.recoil;
    this.vel.x -= Math.cos(this.weaponAngle) * 20;
  }

  startReload() {
    if (!this.alive || this.ragdoll || !this.armUsable()) return;
    const w = this.weapon;
    if (!w.magSize || this.reloading > 0) return;
    if ((this.mags[w.id] | 0) >= w.magSize) return;
    this.reloading = w.reloadTime;
    this.reloadWeapon = w;
    this.reloadDur = w.reloadTime;
    this.game.audio.reload();
  }

  updateWeapon(dt) {
    this.fireCooldown = Math.max(0, this.fireCooldown - dt);
    this.aimKick *= Math.exp(-9 * dt);
    this.sprayHeat *= Math.exp(-1.6 * dt);
    this.pain = Math.max(0, this.pain - dt);

    if (this.reloading > 0) {
      this.reloading -= dt;
      if (this.reloading <= 0) {
        const w = this.reloadWeapon;
        if (w) this.mags[w.id] = w.magSize;
        this.reloading = 0;
      }
    }

    if (this.swing) {
      const sw = this.swing;
      sw.t += dt;
      const u = sw.t / sw.dur;
      if (u >= 1) {
        this.swing = null;
        this.prevBlade = null;
      } else {
        this.weaponAngle = this.aimAngle + this.aimKick + this.swingOffset(u);
        this.pose(0);
        const blade = this.game.combat.bladeWorld(this, sw.def, this.weaponAngle);
        if (this.prevBlade && u > 0.16 && u < 0.8) {
          const speed = Math.abs(this.swingRate(u)) * sw.def.bladeLen;
          this.game.combat.meleeSweep(this, sw.def, this.prevBlade, blade, speed, sw.hitDone);
        }
        this.prevBlade = blade;
      }
    }
    if (this.triggerHeld) this.tryAttack();
  }

  swingOffset(u) {
    if (u < 0.22) return lerp(0, -1.05, u / 0.22);
    if (u < 0.55) {
      const k = (u - 0.22) / 0.33;
      return lerp(-1.05, 1.45, k * k * (3 - 2 * k));
    }
    const k = (u - 0.55) / 0.45;
    return lerp(1.45, 0, k * k);
  }

  swingRate(u) {
    const h = 0.01;
    return (this.swingOffset(Math.min(1, u + h)) - this.swingOffset(Math.max(0, u - h))) / (2 * h) / this.swing.dur;
  }

  legsUsable() {
    const L = this.limb;
    const bad = (l) => !l || l.severed || l.parentSevered || l.broken;
    return !(bad(L.legN_u) || bad(L.legF_u) || bad(L.legN_l) || bad(L.legF_l));
  }

  armUsable() {
    const L = this.limb;
    return !(L.armN_u.severed || L.armN_u.parentSevered || L.armN_l.severed || L.armN_l.parentSevered);
  }

  /* ================= pose ================= */

  pose(dt) {
    const s = this.facing;
    const P = this.pts;
    const ground = this.pos.y;
    const spd = Math.abs(this.vel.x);
    if (this.moving) this.walkPhase += dt * (4 + spd * 0.09);
    else this.walkPhase += dt * 0.8;

    const bob = this.moving ? Math.abs(Math.sin(this.walkPhase)) * 1.1 : Math.sin(this.walkPhase) * 0.2;
    const leanish = this.moving ? Math.min(1.6, spd * 0.014) * Math.sign(this.vel.x || 1) : 0;
    const hipY = ground - 19 - bob * 0.5;

    P.pelvis.x = this.pos.x;
    P.pelvis.y = hipY;
    P.chest.x = this.pos.x + s * (1.0 + leanish * 0.8);
    P.chest.y = hipY - 12.5;
    P.neck.x = P.chest.x + s * 1.6;
    P.neck.y = P.chest.y - 3.6;
    P.head.x = P.neck.x + s * 1.1 + Math.sin(this.breath + this.walkPhase * 0.5) * 0.25;
    P.head.y = P.neck.y - 5.2;

    const stride = this.moving ? Math.sin(this.walkPhase) : 0;
    const strideF = -stride;
    const liftN = this.moving ? Math.max(0, Math.sin(this.walkPhase)) * 2.0 : 0;
    const liftF = this.moving ? Math.max(0, Math.sin(this.walkPhase + Math.PI)) * 2.0 : 0;

    P.hipN.x = P.pelvis.x + s * 1.8; P.hipN.y = hipY + 0.6;
    P.knN.x = P.hipN.x + s * (2.0 + stride * 4.0); P.knN.y = hipY + 9.2 - liftN * 0.7;
    P.anN.x = P.knN.x + s * (0.2 - stride * 3.4); P.anN.y = ground - 2.4 - liftN;

    P.hipF.x = P.pelvis.x - s * 1.6; P.hipF.y = hipY + 0.8;
    P.knF.x = P.hipF.x + s * (1.8 + strideF * 4.0); P.knF.y = hipY + 9.2 - liftF * 0.7;
    P.anF.x = P.knF.x + s * (0.1 - strideF * 3.4); P.anF.y = ground - 2.2 - liftF;

    const aim = this.weaponAngle;
    const shoulderDrop = this.moving ? Math.sin(this.walkPhase) * 0.5 : 0;
    P.shN.x = P.chest.x + s * 1.9;
    P.shN.y = P.chest.y - 1.2 + shoulderDrop;
    P.shF.x = P.chest.x - s * 2.1;
    P.shF.y = P.chest.y - 0.8 - shoulderDrop;

    const gun = this.weapon.kind === 'gun';
    const reach = gun ? (this.weapon.id === 'pistol' ? 11 : 13) : (this.weapon.id === 'fists' ? 7 : 10);
    let hx = P.shN.x + Math.cos(aim) * reach;
    let hy = P.shN.y + Math.sin(aim) * reach;
    const kick = this.aimKick * 6;
    hx -= Math.cos(aim) * kick; hy -= Math.sin(aim) * kick;
    const ik1 = solveIK(P.shN.x, P.shN.y, hx, hy, 8.4, 8.2);
    P.elN.x = ik1.ex; P.elN.y = ik1.ey;
    P.wrN.x = ik1.hx; P.wrN.y = ik1.hy;

    if (gun) {
      const ik2 = solveIK(P.shF.x, P.shF.y,
        P.shF.x + Math.cos(aim) * (reach - 2.5), P.shF.y + Math.sin(aim) * (reach - 2.5), 8.4, 8.2);
      P.elF.x = ik2.ex; P.elF.y = ik2.ey;
      P.wrF.x = ik2.hx; P.wrF.y = ik2.hy;
    } else {
      const swing = this.swing ? this.swingOffset(clamp(this.swing.t / this.swing.dur, 0, 1)) : 0;
      const ta = aim + swing * 0.2 + Math.PI * 0.12;
      const ik2 = solveIK(P.shF.x, P.shF.y,
        P.shF.x + Math.cos(ta) * 8, P.shF.y + Math.sin(ta) * 8, 8.4, 8.2);
      P.elF.x = ik2.ex; P.elF.y = ik2.ey;
      P.wrF.x = ik2.hx; P.wrF.y = ik2.hy;
    }

    // limp limbs
    for (const l of this.limbs) {
      if (!l.broken || l.severed) continue;
      if (l.name === 'armN_u') { P.wrN.x = P.elN.x - s * 1; P.wrN.y = P.elN.y + 7; }
      if (l.name === 'armF_u') { P.wrF.x = P.elF.x - s * 1; P.wrF.y = P.elF.y + 7; }
      if (l.name === 'legN_u') { P.anN.x = P.knN.x + s * 1; P.anN.y = Math.min(ground - 2, P.knN.y + 9); }
      if (l.name === 'legF_u') { P.anF.x = P.knF.x + s * 1; P.anF.y = Math.min(ground - 2, P.knF.y + 9); }
    }
  }

  /* ================= physics ================= */

  goRagdoll(impulse = null) {
    if (this.ragdoll) {
      if (impulse) this.applyImpulse(impulse);
      return;
    }
    this.ragdoll = true;
    this.ragTime = 0;
    const vx = this.vel.x;
    for (const k in this.pts) {
      const p = this.pts[k];
      p.vx = vx * 0.85 + rand(-12, 12);
      p.vy = rand(-6, 4);
    }
    if (impulse) this.applyImpulse(impulse);
  }

  applyImpulse({ x = 0, y = 0 }) {
    for (const k in this.pts) {
      this.pts[k].vx += x * rand(0.7, 1.2);
      this.pts[k].vy += y * rand(0.7, 1.2);
    }
  }

  tryStandUp() {
    if (!this.alive || !this.ragdoll || this.unconscious) return;
    if (this.ragTime < 1.2) return;
    if (!this.legsUsable() || this.health < 22) return;
    const avgV = (Math.abs(this.pts.chest.vx) + Math.abs(this.pts.pelvis.vx)) * 0.5;
    if (avgV > 40) return;
    this.ragdoll = false;
    this.ragTime = 0;
    this.pos.x = this.pts.pelvis.x;
    this.pos.y = GROUND_Y;
    this.vel.x *= 0.3;
  }

  ensureRestBones() {
    if (this.restBones) return;
    this.restBones = {};
    for (const [a, b] of BONES) {
      this.restBones[a + b] = Math.hypot(this.pts[b].x - this.pts[a].x, this.pts[b].y - this.pts[a].y) || 1;
    }
  }

  wake() {
    this.sleeping = false;
    this.sleepTimer = 0;
  }

  // Momentum delivered at a specific point of the body: the nearby points take
  // most of it, the rest is transmitted through the joint chain.
  applyImpulseAt(px, py, ix, iy) {
    this.wake();
    const P = this.pts;
    let totalW = 0;
    const ws = [];
    for (const k in P) {
      const p = P[k];
      const d2 = (p.x - px) * (p.x - px) + (p.y - py) * (p.y - py);
      const w = 1 / (d2 + 18);
      ws.push([p, w]);
      totalW += w;
    }
    for (const [p, w] of ws) {
      const k = w / totalW;
      p.vx += ix * k * 2.0;
      p.vy += iy * k * 2.0;
    }
  }

  // Keeps elbows and knees from bending the wrong way.
  limitJoint(a, b, c, minAbs, maxAbs) {
    const a1 = Math.atan2(a.y - b.y, a.x - b.x);
    const a2 = Math.atan2(c.y - b.y, c.x - b.x);
    let d = angleDiff(a1, a2);
    const absd = Math.abs(d);
    if (absd < minAbs || absd > maxAbs) {
      const target = Math.sign(d || 1) * clamp(absd, minAbs, maxAbs);
      const len = Math.hypot(c.x - b.x, c.y - b.y);
      const na = a1 + target;
      c.x = b.x + Math.cos(na) * len;
      c.y = b.y + Math.sin(na) * len;
    }
  }

  stepRagdoll(h) {
    const P = this.pts;
    const G = 900;

    // Position-based integration: remember the old position so the solver can
    // derive the real velocity from how far each point actually moved.
    for (const k in P) {
      const p = P[k];
      p.px0 = p.x;
      p.py0 = p.y;
      p.vy += G * h;
      p.x += p.vx * h;
      p.y += p.vy * h;
    }

    for (let it = 0; it < 6; it++) {
      for (const [an, bn] of BONES) {
        if (this.boneSevered(an, bn)) continue;
        const a = P[an], b = P[bn];
        const rest = this.restBones[an + bn];
        if (!rest) continue;
        const dx = b.x - a.x, dy = b.y - a.y;
        const d = Math.hypot(dx, dy) || 1e-6;
        // the torso is stiff, hands and feet stay softer so poses look organic
        const stiff = (an === 'pelvis' || bn === 'pelvis' || an === 'chest' || bn === 'chest') ? 1 : 0.85;
        const corr = ((d - rest) / d) * 0.5 * stiff;
        const cx = dx * corr, cy = dy * corr;
        a.x += cx; a.y += cy;
        b.x -= cx; b.y -= cy;
      }
      this.limitJoint(P.shN, P.elN, P.wrN, 0.28, 2.85);
      this.limitJoint(P.shF, P.elF, P.wrF, 0.28, 2.85);
      this.limitJoint(P.hipN, P.knN, P.anN, 0.45, 2.95);
      this.limitJoint(P.hipF, P.knF, P.anF, 0.45, 2.95);

      for (const k2 in P) {
        const p = P[k2];
        const r = this.pointRadius(k2);
        if (p.y > GROUND_Y - r) {
          p.y = GROUND_Y - r;
          if (p.py0 !== undefined) {
            // friction: only a fraction of the horizontal slide survives
            p.x = p.px0 + (p.x - p.px0) * 0.82;
            p.y = p.py0 + (p.y - p.py0) * 0.35;
          }
        }
        if (p.x < 5) p.x = 5 + (p.x - 5) * 0.2;
        if (p.x > VIEW_W - 5) p.x = (VIEW_W - 5) + (p.x - (VIEW_W - 5)) * 0.2;
      }
    }

    // velocity from the solved positions
    const invH = 1 / h;
    for (const k in P) {
      const p = P[k];
      const grounded = p.y >= GROUND_Y - this.pointRadius(k) - 0.4;
      const damp = grounded ? 0.78 : 0.995;
      p.vx = (p.x - p.px0) * invH * damp;
      p.vy = (p.y - p.py0) * invH * damp;
      if (Math.abs(p.vx) < 0.4) p.vx = 0;
      if (Math.abs(p.vy) < 0.4) p.vy = 0;
    }
  }

  physics(dt, game) {
    if (!this.ragdoll) {
      if (!this.legsUsable() && this.alive) this.goRagdoll();
      this.pos.y = GROUND_Y;
      return;
    }
    this.ragTime += dt;
    if (this.sleeping) {
      this.pos.x = this.pts.pelvis.x;
      this.pos.y = GROUND_Y;
      this.vel.x = 0;
      return;
    }

    const steps = 2;
    for (let i = 0; i < steps; i++) this.stepRagdoll(dt / steps);

    // settle to rest so bodies stop buzzing on the ground
    let maxV = 0;
    for (const k in this.pts) {
      const p = this.pts[k];
      maxV = Math.max(maxV, Math.hypot(p.vx, p.vy));
    }
    if (maxV < 9 && this.ragTime > 0.5) {
      this.sleepTimer += dt;
      if (this.sleepTimer > 0.35) {
        this.sleeping = true;
        for (const k in this.pts) { this.pts[k].vx = 0; this.pts[k].vy = 0; }
      }
    } else {
      this.sleepTimer = 0;
    }

    this.pos.x = this.pts.pelvis.x;
    this.pos.y = GROUND_Y;
    this.vel.x = this.pts.pelvis.vx;

    if (this.alive && this.bleedTotal > 4) {
      this.poolT -= dt;
      if (this.poolT <= 0) {
        this.poolT = rand(0.2, 0.55);
        const spot = pick(this.limbs.filter((l) => l.severed || l.wounds.length));
        if (spot) {
          const p = this.woundWorldPos(spot);
          game.gore.splatGround(p.x, GROUND_Y, rand(1.2, 3.2) * clamp(spot.bleedRate / 8, 0.5, 1.5), 0.4);
        }
      }
    }
  }

  pointRadius(k) {
    if (k === 'head') return 5.4;
    if (k === 'pelvis' || k === 'chest') return 4.4;
    if (k === 'wrN' || k === 'wrF') return 1.9;
    if (k === 'anN' || k === 'anF') return 2.3;
    return 2.8;
  }

  boneSevered(an, bn) {
    const limb = this.limbs.find((l) =>
      (l.a === this.pts[an] && l.b === this.pts[bn]) || (l.b === this.pts[an] && l.a === this.pts[bn]));
    if (limb) return !!limb.severed || !!limb.parentSevered;
    return false;
  }

  /* ================= damage ================= */

  limbDamageMultiplier(limb) {
    if (limb.name === 'head') return 2.4;
    if (limb.name === 'torso') return 1.0;
    if (limb.bone === 'arm') return 0.42;
    return 0.5;
  }

  enterable() { return true; }

  applyHit(limb, ev) {
    const game = this.game;
    this.hitFlash = 0.12;
    if (ev.attacker && ev.attacker !== this) {
      this.lastAttacker = ev.attacker;
      if (!this.ragdoll && (ev.type === 'blunt' || (ev.knock || 0) > 150 || ev.dmg > 42)) this.goRagdoll();
    }

    if (!this.alive && !limb.mass) return;

    const mult = this.limbDamageMultiplier(limb);
    const dmg = ev.dmg * mult * (limb.severed ? 0.4 : 1);
    this.health -= dmg;
    this.pain = Math.min(1.5, this.pain + ev.dmg / 60);
    this.bloody = Math.min(1, this.bloody + dmg * 0.003);

    if (this.alive && ev.dmg > 18 && chance(0.5)) game.audio.scream();

    if (!limb.severed) {
      limb.struct = clamp(limb.struct + ev.struct * (ev.type === 'blunt' ? 1.25 : 1), 0, 1.4);
      if (ev.type === 'slash') limb.cuts = (limb.cuts || 0) + 1;
      limb.exposed = limb.struct > 0.42;
      if (limb.struct > 0.72 && (limb.bone === 'arm' || limb.bone === 'leg')) limb.broken = true;
    }

    const badness = ev.type === 'bullet' ? 1.35 : ev.type === 'slash' ? 1.2 : 0.8;
    const cut = ev.cut != null ? ev.cut : 0.2;
    const partMul = limb.name === 'head' ? 1.7 : limb.name === 'torso' ? 1.15 : 1;
    // millilitres per second: a hole through an artery can empty a person fast
    const bleedAdd = (7 + dmg * 2.1) * badness * cut * (ev.bleedMul || 1) * partMul;
    const isArterial = (limb.bone === 'leg' || limb.bone === 'arm' || limb.name === 'torso')
      && ev.struct > 0.12 && chance(0.3 + ev.struct * 1.8);

    const wound = {
      t: this.woundParam(limb, ev.at),
      side: chance(0.5) ? 1 : -1,
      r: clamp(0.7 + ev.dmg * 0.035 + (ev.type === 'bullet' ? 0.6 : 0), 0.8, 3.2),
      bleed: bleedAdd,
      arterial: isArterial,
      kind: ev.type,
      age: 0,
    };
    limb.wounds.push(wound);
    if (limb.wounds.length > 9) limb.wounds.shift();
    if (isArterial) {
      limb.arterial = true;
      game.gore.spray(ev.at.x, ev.at.y, ev.dir.x, ev.dir.y, {
        count: 16, speed: 260, size: 1.7, spread: 0.5, mistChance: 0.15,
      });
      game.audio.splat();
    }

    // Momentum transfer: a bullet carries little mass, so it mostly shakes the
    // tissue it hit instead of throwing the whole body.
    if (ev.dir) {
      const impulse = ev.type === 'bullet' ? ev.dmg * 0.45 : (ev.knock || 0) * 0.22;
      const at = ev.at || this.woundWorldPos(limb);
      if (this.ragdoll) {
        this.applyImpulseAt(at.x, at.y, ev.dir.x * impulse, ev.dir.y * impulse);
      } else {
        this.vel.x += ev.dir.x * impulse * 0.12;
        if ((ev.knock || 0) > 170 || (ev.type === 'blunt' && ev.dmg > 32)) {
          this.goRagdoll({ x: ev.dir.x * 4, y: -18 });
        }
      }
    }

    if (this.alive) {
      if (!this.unconscious && (this.health < 16 || this.blood < 1400 || this.pain > 1.25)) {
        this.unconscious = true;
        if (!this.ragdoll) this.goRagdoll();
        game.addLog(`${this.name}: потерял сознание`, '#e0b060');
      }
      if (this.unconscious && (this.health <= 0 || this.blood <= 0)) {
        if (!this.deathCause) this.deathCause = this.blood < 2600 ? 'кровопотеря' : 'травмы, несовместимые с жизнью';
        this.die();
      }
    }

    if (limb.bone === 'skull' && (limb.struct > 0.9 || (ev.type === 'bullet' && ev.dmg > 40 && chance(0.4)))) {
      this.headDestroyed(ev);
      return;
    }
    if (limb.name === 'torso' && limb.struct > 0.85 && ev.type !== 'bullet' && chance(0.5)) this.gutSpill();

    const canSever = ['armN_u', 'armN_l', 'armF_u', 'armF_l', 'legN_u', 'legN_l', 'legF_u', 'legF_l', 'head'].includes(limb.name);
    if (canSever && !limb.severed) {
      const struct = clamp(limb.struct, 0, 1);
      let severChance = struct * 0.7 + cut * (ev.struct || 0) * 1.2 - 0.3;
      if (ev.type === 'slash') severChance += (limb.cuts || 0) * 0.055;
      if (struct > 0.75 && cut > 0.8) severChance += 0.28;
      if (struct < 0.45) severChance *= 0.2;          // skin and muscle must be cut first
      if (ev.type === 'bullet') severChance *= 0.4;   // bullets rarely take a limb clean off
      if (limb.broken) severChance += 0.15;
      if (ev.type === 'blunt') severChance *= 0.45;
      if (chance(clamp(severChance, 0, 0.9))) this.sever(limb, ev);
    }
    this.checkDeath();
  }

  woundParam(limb, at) {
    if (!at) return rand(0.2, 0.8);
    const ax = limb.a.x, ay = limb.a.y, bx = limb.b.x, by = limb.b.y;
    const abx = bx - ax, aby = by - ay;
    const len2 = abx * abx + aby * aby || 1;
    return clamp(((at.x - ax) * abx + (at.y - ay) * aby) / len2, 0.1, 0.9);
  }

  woundWorldPos(limb) {
    if (limb.severed) return { x: limb.a.x, y: limb.a.y };
    const w = limb.wounds.length ? limb.wounds[limb.wounds.length - 1] : null;
    if (!w) return { x: (limb.a.x + limb.b.x) / 2, y: (limb.a.y + limb.b.y) / 2 };
    const ang = Math.atan2(limb.b.y - limb.a.y, limb.b.x - limb.a.x) + Math.PI / 2;
    return {
      x: lerp(limb.a.x, limb.b.x, w.t) + Math.cos(ang) * w.side * limb.radius * 0.9,
      y: lerp(limb.a.y, limb.b.y, w.t) + Math.sin(ang) * w.side * limb.radius * 0.9,
    };
  }

  headDestroyed(ev) {
    const head = this.limb.head;
    if (!head.severed) this.sever(head, ev, true);
    this.deathCause = 'череп разрушен';
    this.die();
    const p = this.pts.head;
    this.game.gore.spray(p.x, p.y, 0, -1, { count: 40, speed: 320, size: 2.0, spread: 1.1, mistChance: 0.3 });
    this.game.gore.flesh(p.x, p.y, 0, -1, 10, 1.4);
    this.game.gore.boneShard(p.x, p.y, 0, -1, 8);
    this.game.gore.mist(p.x, p.y, 0, -1, 14, 1.4, 1.5);
    this.game.audio.sever();
  }

  gutSpill() {
    const torso = this.limb.torso;
    const p = { x: (torso.a.x + torso.b.x) / 2, y: (torso.a.y + torso.b.y) / 2 };
    this.game.gore.flesh(p.x, p.y, rand(-0.4, 0.4), 1, 14, 1.2);
    this.game.gore.spray(p.x, p.y, 0, 1, { count: 22, speed: 150, size: 1.8, spread: 1.2, mistChance: 0.2 });
    torso.wounds.push({ t: 0.5, side: 1, r: 3.5, bleed: 9, arterial: true, kind: 'gut', age: 0 });
    torso.arterial = true;
    this.game.audio.sever();
    if (!this.ragdoll) this.goRagdoll();
  }

  sever(limb, ev = {}, silent = false) {
    const game = this.game;
    if (limb.severed) return;
    limb.severed = true;
    limb.struct = 1.2;
    const ax = limb.a.x, ay = limb.a.y;
    const bx = limb.b.x, by = limb.b.y;
    const len = Math.hypot(bx - ax, by - ay) || 6;
    const ang = Math.atan2(by - ay, bx - ax);
    const dirx = ev.dir ? ev.dir.x : rand(-0.6, 0.6);
    const diry = ev.dir ? ev.dir.y : -0.8;

    game.debris.push({
      type: 'limb',
      a: { x: ax, y: ay, vx: rand(-40, 40) + dirx * 60, vy: rand(-100, -20) + diry * 40 },
      b: { x: bx, y: by, vx: rand(-40, 40) + dirx * 60, vy: rand(-100, -20) + diry * 40 },
      len, rest: len, radius: limb.radius,
      color: limb.kind === 'skin' ? this.skin : (limb.bone === 'leg' ? this.pants : this.cloth.a),
      kind: limb.kind, bone: limb.bone, rot: ang, vr: rand(-9, 9),
      wounds: limb.wounds.slice(-3),
      life: 999, bleed: 9,
      pattern: limb.name, pants: this.pants, shoe: limb.bone === 'leg',
    });

    limb.stumpBleed = limb.bone === 'head' ? 55 : limb.bone === 'leg' ? 21 : 17;
    limb.arterial = true;

    for (const l of this.limbs) {
      if (l.parent === limb.name && !l.severed) {
        l.parentSevered = true;
        l.struct = 1;
      }
    }

    if (limb.name === 'armN_u' || limb.name === 'armN_l') {
      if (this.weapon.id !== 'fists') {
        game.debris.push({
          type: 'weapon', def: this.weapon,
          x: this.pts.wrN.x, y: this.pts.wrN.y,
          vx: rand(-60, 60), vy: rand(-140, -50), rot: this.weaponAngle, vr: rand(-14, 14), life: 999,
        });
      }
      this.weapon = WEAPON_BY_ID.fists;
      this.swing = null;
      this.reloading = 0;
    }

    game.gore.spray(ax, ay, dirx, diry, { count: 32, speed: 210, size: 1.9, spread: 0.8, mistChance: 0.25 });
    game.gore.mist(ax, ay, dirx, diry, 12, 1.1, 1.4);
    game.gore.flesh(ax, ay, dirx, diry, 6, 1);
    game.gore.boneShard(ax, ay, dirx, diry, 4);
    game.audio.sever();
    game.addLog(`${this.name}: оторвано — ${limb.label}`, '#e08b7b');

    if (limb.bone === 'leg') this.goRagdoll();
    if (limb.bone === 'skull') {
      this.deathCause = 'обезглавлен';
      this.die();
    }
    this.checkDeath();
  }

  checkDeath() {
    if (!this.alive) return;
    const head = this.limb.head;
    const torso = this.limb.torso;
    if (head.severed) { this.deathCause = 'обезглавлен'; this.die(); return; }
    if (this.health <= 0) { this.deathCause = 'потерял слишком много крови'; this.die(); return; }
    if (torso.severed) this.die();
  }

  die() {
    if (!this.alive) return;
    this.alive = false;
    this.deadTime = 0;
    this.deathTime = 0;
    this.unconscious = false;
    for (const l of this.limbs) l.arterial = false;
    this.goRagdoll();
    this.triggerHeld = false;
    this.swing = null;
    this.reloading = 0;
    this.game.addLog(`${this.name} погиб — ${this.deathCause || 'рана'}`, '#c9b98a');
    this.game.gore.splatGround(this.pts.pelvis.x, GROUND_Y, 4, 0.5);
    if (this.game.inspected === this) this.game.needsInspectRefresh = true;
  }

  /* ================= bleeding ================= */

  updateGore(dt, game) {
    if (this.hitFlash > 0) this.hitFlash -= dt;
    let total = 0;
    for (const limb of this.limbs) {
      limb.bleedRate = 0;
      if (limb.parentSevered) continue;
      for (const w of limb.wounds) {
        w.age += dt;
        // clots slowly while the heart still beats
        const clot = this.alive ? (w.kind === 'bullet' ? 0.022 : 0.008) : 0.55;
        w.bleed *= 1 - clot * dt;
        limb.bleedRate += w.bleed;
      }
      if (limb.severed) limb.bleedRate += limb.stumpBleed || 12;
      if (limb.bleedRate > 0) {
        total += limb.bleedRate;
        const p = this.woundWorldPos(limb);
        const arterial = limb.arterial || limb.severed;
        const pulse = arterial ? 0.45 + 0.75 * Math.max(0, Math.sin(game.time * 7.2 + this.id)) : 1;
        const dirAng = arterial ? rand(0, TAU) : Math.PI / 2;
        game.gore.bleed(p.x, p.y, Math.cos(dirAng), Math.sin(dirAng),
          limb.bleedRate * (arterial ? 2.4 : 1.5) * pulse, arterial, dt);
        if (limb.severed && chance(dt * 3)) game.gore.splatGround(p.x, GROUND_Y, rand(2, 5), 0.5);
      }
    }
    this.bleedTotal = total;
    this.blood = Math.max(0, this.blood - total * dt);
    if (this.alive) {
      // hypovolemic shock: the less blood, the faster the body fails
      if (this.blood < 3000) {
        const s2 = (3000 - this.blood) / 3000;
        this.health -= s2 * s2 * 18 * dt;
      }
      if (this.blood <= 0) this.health -= 12 * dt;
      if (this.health <= 0) {
        if (!this.deathCause) this.deathCause = this.blood < 2600 ? 'кровопотеря' : 'травмы, несовместимые с жизнью';
        this.die();
      }
      // recovery from unconsciousness when the bleeding has been stopped
      if (this.unconscious && this.blood > 2100 && this.health > 26 && this.bleedTotal < 3) {
        this.unconscious = false;
        game.addLog(`${this.name}: пришёл в себя`, '#8fc08f');
      }
    }
    if (total > 2) {
      this.poolT -= dt;
      if (this.poolT <= 0) {
        this.poolT = clamp(0.8 / Math.max(1, total * 0.06), 0.12, 0.9);
        const p = this.pts.pelvis;
        game.gore.splatGround(p.x + rand(-6, 6), GROUND_Y, clamp(total * 0.1, 0.6, 4.0), 0.36);
      }
    }

    // A heart that has stopped does not spray: bleeding dies down after death.
    if (!this.alive) {
      this.deathTime += dt;
      for (const l of this.limbs) {
        if (l.severed) l.stumpBleed = (l.stumpBleed || 0) * (1 - 3 * dt);
      }
    }
  }

  update(dt, game) {
    this.animT += dt;
    if (this.unconscious) {
      this.triggerHeld = false;
      this.vel.x *= 0.9;
      if (!this.ragdoll) this.goRagdoll();
    } else if (this.alive) {
      this.updateAI(dt, game);
    } else {
      this.triggerHeld = false;
    }
    if (!this.alive) this.deadTime += dt;
    this.updateWeapon(dt);
    if (!this.ragdoll) this.pose(dt);
    this.ensureRestBones();
    this.physics(dt, game);
    this.updateGore(dt, game);

    if (this.ragdoll && this.alive && !this.unconscious && this.ragTime > 2.4 && this.health > 28 && this.legsUsable()) {
      if (chance(dt * 0.9)) this.tryStandUp();
    }
    if (this.moving && !this.ragdoll) {
      this.stepT -= dt;
      if (this.stepT <= 0) {
        this.stepT = 0.34;
        if (dist(this.pos.x, 0, game.camX, 0) < 160) game.audio.step();
      }
    }
    if (this.aimKick > 0.4) this.aimKick = 0.4;
  }

  legsUnusableSafe() {
    return this.legsUsable();
  }

  /* ================= drawing ================= */

  line(ctx, a, b, width, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
  }

  seg(ctx, a, b, r, color, outline = true) {
    ctx.lineCap = 'round';
    if (outline) this.line(ctx, a, b, r * 2 + 1.7, '#15141a');
    this.line(ctx, a, b, r * 2, color);
    // subtle top highlight
    ctx.globalAlpha = 0.16;
    this.line(ctx, { x: a.x, y: a.y - r * 0.45 }, { x: b.x, y: b.y - r * 0.45 }, r * 0.75, '#ffffff');
    ctx.globalAlpha = 1;
  }

  drawWounds(ctx, limb) {
    if (!limb.wounds.length && !limb.severed) return;
    const ang = Math.atan2(limb.b.y - limb.a.y, limb.b.x - limb.a.x) + Math.PI / 2;
    for (const w of limb.wounds) {
      const x = lerp(limb.a.x, limb.b.x, w.t) + Math.cos(ang) * w.side * limb.radius * 0.55;
      const y = lerp(limb.a.y, limb.b.y, w.t) + Math.sin(ang) * w.side * limb.radius * 0.55;
      const r = Math.max(0.8, w.r);
      ctx.fillStyle = w.kind === 'blunt' ? '#5a1a2a' : '#5b0609';
      ctx.beginPath();
      ctx.ellipse(x, y, r, r * 0.8, rand(0, TAU), 0, TAU);
      ctx.fill();
      ctx.fillStyle = 'rgba(25,2,4,0.6)';
      ctx.beginPath();
      ctx.arc(x, y, r * 0.45, 0, TAU);
      ctx.fill();
      if (w.arterial) {
        ctx.fillStyle = '#b3121a';
        ctx.beginPath();
        ctx.arc(x, y, r * 0.3, 0, TAU);
        ctx.fill();
      }
    }
    if (limb.struct > 0.45 && !limb.severed) {
      ctx.globalAlpha = clamp((limb.struct - 0.45) * 1.5, 0, 0.7);
      ctx.strokeStyle = '#7c0f13';
      ctx.lineWidth = limb.radius * 0.8;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(limb.a.x, limb.a.y);
      ctx.lineTo(limb.b.x, limb.b.y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
    if (limb.severed) {
      ctx.fillStyle = '#6d0609';
      ctx.beginPath(); ctx.arc(limb.a.x, limb.a.y, limb.radius * 0.95, 0, TAU); ctx.fill();
      ctx.fillStyle = '#3c0204';
      ctx.beginPath(); ctx.arc(limb.a.x, limb.a.y, limb.radius * 0.45, 0, TAU); ctx.fill();
      ctx.fillStyle = '#e8dfc9';
      ctx.beginPath(); ctx.arc(limb.a.x + 0.6, limb.a.y - 0.4, 0.7, 0, TAU); ctx.fill();
    }
  }

  drawShadow(ctx) {
    const p = this.ragdoll ? this.pts.chest : this.pts.pelvis;
    ctx.globalAlpha = 0.3;
    ctx.fillStyle = '#0a0908';
    ctx.beginPath();
    ctx.ellipse(p.x, GROUND_Y + 2, this.ragdoll ? 17 : 11, 2.6, 0, 0, TAU);
    ctx.fill();
    ctx.globalAlpha = 1;
  }

  drawBody(ctx) {
    const P = this.pts;
    const L = this.limb;
    const flicker = this.hitFlash > 0;
    const skin = flicker ? '#ffd7d0' : this.skin;
    const clothA = flicker ? '#ffd7d0' : this.cloth.a;
    const pants = flicker ? '#ffd7d0' : this.pants;
    const s = this.ragdoll ? Math.sign(P.head.x - P.neck.x || this.facing) || 1 : this.facing;

    const backArm = [L.armF_u, L.armF_l].filter((l) => !l.severed && !l.parentSevered);
    const backLeg = [L.legF_u, L.legF_l].filter((l) => !l.severed && !l.parentSevered);
    const frontLeg = [L.legN_u, L.legN_l].filter((l) => !l.severed && !l.parentSevered);
    const frontArm = [L.armN_u, L.armN_l].filter((l) => !l.severed && !l.parentSevered);
    const torso = L.torso.severed ? null : L.torso;
    const head = (L.head.severed || L.head.parentSevered) ? null : L.head;

    // ---- back layer
    for (const l of backLeg) this.seg(ctx, l.a, l.b, l.radius, l.bone === 'leg' ? shade(pants, 0.72) : shade(skin, 0.72));
    for (const l of backArm) this.seg(ctx, l.a, l.b, l.radius, l.kind === 'skin' ? shade(skin, 0.74) : shade(clothA, 0.7));
    if (backLeg.length) this.drawWounds(ctx, backLeg[backLeg.length - 1]);
    if (backArm.length) this.drawWounds(ctx, backArm[backArm.length - 1]);

    // shoes
    for (const l of backLeg) {
      if (l.bone !== 'leg' || l.name.endsWith('_u')) continue;
      ctx.fillStyle = '#1e1a16';
      roundRect(ctx, l.b.x - 3.4, l.b.y - 1.6, 7, 3.4, 1.4);
      ctx.fill();
    }

    // ---- torso
    if (torso) {
      this.seg(ctx, P.pelvis, P.chest, 5.0, clothA, false);
      this.seg(ctx, P.chest, P.neck, 4.2, clothA, false);
      // vest
      const ang = Math.atan2(P.neck.y - P.chest.y, P.neck.x - P.chest.x);
      ctx.save();
      ctx.translate(P.chest.x, P.chest.y + 1.5);
      ctx.rotate(ang);
      ctx.fillStyle = shade(this.cloth.b, 0.85);
      roundRect(ctx, -5.2, -2.6, 10.4, 10.4, 2.6);
      ctx.fill();
      ctx.strokeStyle = 'rgba(10,10,14,0.35)';
      ctx.lineWidth = 0.4;
      ctx.stroke();
      // collar
      ctx.fillStyle = shade(clothA, 1.15);
      roundRect(ctx, -2.2, -3.0, 4.4, 2.0, 0.8);
      ctx.fill();
      ctx.restore();
      // belt
      ctx.strokeStyle = '#241d19';
      ctx.lineWidth = 2.2;
      ctx.beginPath();
      ctx.moveTo(P.pelvis.x - 4.2, P.pelvis.y - 0.4);
      ctx.lineTo(P.pelvis.x + 4.2, P.pelvis.y - 0.4);
      ctx.stroke();
      ctx.fillStyle = '#b4a171';
      ctx.fillRect(P.pelvis.x - 0.9, P.pelvis.y - 1.6, 1.8, 2.2);
      this.drawWounds(ctx, torso);
      if (torso.struct > 0.8) {
        const mx = (P.pelvis.x + P.chest.x) / 2, my = (P.pelvis.y + P.chest.y) / 2;
        ctx.fillStyle = '#7a1a1f';
        ctx.beginPath();
        ctx.ellipse(mx + s * 2, my + 3, 3.0, 3.8, 0.4, 0, TAU);
        ctx.fill();
        ctx.fillStyle = '#c06a6f';
        ctx.beginPath(); ctx.arc(mx + s * 2.4, my + 2.4, 0.9, 0, TAU); ctx.fill();
      }
    } else {
      ctx.fillStyle = '#5b0609';
      ctx.beginPath(); ctx.arc(P.pelvis.x, P.pelvis.y, 3.4, 0, TAU); ctx.fill();
    }

    // ---- front leg
    for (const l of frontLeg) this.seg(ctx, l.a, l.b, l.radius, l.bone === 'leg' ? (l.name.endsWith('_u') ? pants : shade(pants, 0.92)) : skin);
    for (const l of frontLeg) if (l.name.endsWith('_l')) {
      ctx.fillStyle = '#231f19';
      roundRect(ctx, l.b.x - 3.6, l.b.y - 1.7, 7.4, 3.6, 1.5);
      ctx.fill();
    }

    // ---- head
    if (head) {
      const hx = P.head.x, hy = P.head.y;
      const ha = Math.atan2(P.head.y - P.neck.y, P.head.x - P.neck.x);
      ctx.fillStyle = '#15141a';
      ctx.beginPath(); ctx.arc(hx, hy, 6.2, 0, TAU); ctx.fill();
      ctx.fillStyle = skin;
      ctx.beginPath(); ctx.arc(hx, hy, 5.5, 0, TAU); ctx.fill();
      // jaw shadow
      ctx.fillStyle = shade(skin, 0.86);
      ctx.beginPath();
      ctx.ellipse(hx + Math.cos(ha) * 1.2, hy + Math.sin(ha) * 1.2 + 1.6, 4.0, 3.0, 0, 0, Math.PI);
      ctx.fill();
      // hair
      ctx.fillStyle = this.hair;
      ctx.beginPath();
      ctx.ellipse(hx - Math.cos(ha) * 0.8, hy - Math.sin(ha) * 0.8 - 1.4, 5.4, 4.6, ha, Math.PI * 0.95, Math.PI * 2.1);
      ctx.fill();
      ctx.beginPath();
      ctx.ellipse(hx - Math.cos(ha) * 4.2, hy - Math.sin(ha) * 4.2 + 0.4, 1.8, 2.6, ha, 0, TAU);
      ctx.fill();
      // face
      const ex = Math.cos(ha), ey = Math.sin(ha);
      const px = -ey, py = ex;
      ctx.fillStyle = '#1a1016';
      if (this.alive) {
        if (this.ragdoll) {
          ctx.beginPath(); ctx.arc(hx + ex * 2.2 + px * 1.6, hy + ey * 2.2 + py * 1.6, 0.75, 0, TAU); ctx.fill();
          ctx.beginPath(); ctx.arc(hx + ex * 2.2 - px * 1.6, hy + ey * 2.2 - py * 1.6, 0.75, 0, TAU); ctx.fill();
        } else {
          ctx.beginPath(); ctx.arc(hx + ex * 2.4 - px * 0.9, hy + ey * 2.4 - py * 0.9, 0.85, 0, TAU); ctx.fill();
          ctx.beginPath(); ctx.arc(hx + ex * 2.4 + px * 1.3, hy + ey * 2.4 + py * 1.3, 0.7, 0, TAU); ctx.fill();
        }
        if (this.pain > 0.35) {
          ctx.fillStyle = '#4a1010';
          ctx.beginPath();
          ctx.ellipse(hx + ex * 3.2, hy + ey * 3.2 + 1.4, 1.4, 1.0, ha, 0, TAU);
          ctx.fill();
        }
      } else {
        ctx.strokeStyle = '#2a0a0a';
        ctx.lineWidth = 0.8;
        const cxx = hx + ex * 2.4, cyy = hy + ey * 2.4;
        ctx.beginPath(); ctx.moveTo(cxx - 1.2, cyy - 1); ctx.lineTo(cxx + 1.2, cyy + 1); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(cxx + 1.2, cyy - 1); ctx.lineTo(cxx - 1.2, cyy + 1); ctx.stroke();
      }
      this.drawWounds(ctx, head);
      if (head.struct > 0.45 && this.alive) {
        ctx.fillStyle = 'rgba(90,6,9,0.6)';
        ctx.beginPath();
        ctx.ellipse(hx + ex * 2.6, hy + ey * 2.6 + 1.8, 0.8, 2.2, ha, 0, TAU);
        ctx.fill();
      }
    } else {
      ctx.fillStyle = '#5b0609';
      ctx.beginPath(); ctx.arc(P.neck.x, P.neck.y - 1, 2.8, 0, TAU); ctx.fill();
      ctx.fillStyle = '#e8dfc9';
      ctx.beginPath(); ctx.arc(P.neck.x + 0.5, P.neck.y - 1.4, 0.8, 0, TAU); ctx.fill();
    }

    // ---- front arm + weapon
    for (const l of frontArm) this.seg(ctx, l.a, l.b, l.radius, l.kind === 'skin' ? skin : clothA);
    for (const l of frontArm) this.drawWounds(ctx, l);
    // gloves
    if (L.armN_l && !L.armN_l.severed && !L.armN_l.parentSevered) {
      ctx.fillStyle = '#2f2a25';
      ctx.beginPath(); ctx.arc(P.wrN.x, P.wrN.y, 2.0, 0, TAU); ctx.fill();
    }
    if (L.armF_l && !L.armF_l.severed && !L.armF_l.parentSevered) {
      ctx.fillStyle = shade('#2f2a25', 0.85);
      ctx.beginPath(); ctx.arc(P.wrF.x, P.wrF.y, 1.8, 0, TAU); ctx.fill();
    }
    if (frontArm.length === 2 && this.weapon && this.weapon.id !== 'fists') {
      drawWeapon(ctx, this.weapon, P.wrN.x, P.wrN.y, this.weaponAngle, 1, 0, this.bloody);
    }
  }

  drawSelection(ctx, color = '#7fd4ff', label = null) {
    const p = this.ragdoll ? this.pts.chest : this.pts.pelvis;
    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = 1;
    ctx.globalAlpha = 0.85;
    ctx.setLineDash([3, 3]);
    ctx.beginPath();
    ctx.ellipse(p.x, GROUND_Y + 1, 15, 4, 0, 0, TAU);
    ctx.stroke();
    ctx.setLineDash([]);
    if (label) {
      ctx.globalAlpha = 1;
      ctx.font = 'bold 8px "Segoe UI", system-ui, sans-serif';
      ctx.textAlign = 'center';
      const w = ctx.measureText(label).width + 8;
      ctx.fillStyle = 'rgba(8,10,16,0.78)';
      roundRect(ctx, p.x - w / 2, p.y - 30, w, 12, 3);
      ctx.fill();
      ctx.fillStyle = color;
      ctx.fillText(label, p.x, p.y - 21);
      ctx.textAlign = 'left';
    }
    ctx.restore();
  }

  draw(ctx, game) {
    this.drawShadow(ctx);
    this.drawBody(ctx);
  }
}

function solveIK(sx, sy, tx, ty, l1, l2) {
  let dx = tx - sx, dy = ty - sy;
  let d = Math.hypot(dx, dy);
  const maxD = l1 + l2 - 0.02;
  const minD = Math.abs(l1 - l2) + 0.02;
  if (d > maxD) {
    const k = maxD / (d || 1);
    tx = sx + dx * k; ty = sy + dy * k;
    dx = tx - sx; dy = ty - sy; d = maxD;
  } else if (d < minD) {
    const k = minD / (d || 1);
    tx = sx + dx * k; ty = sy + dy * k;
    dx = tx - sx; dy = ty - sy; d = minD;
  }
  const a = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
  const h = Math.sqrt(Math.max(0, l1 * l1 - a * a));
  const ux = dx / d, uy = dy / d;
  const mx = sx + ux * a, my = sy + uy * a;
  const p1x = mx - uy * h, p1y = my + ux * h;
  const p2x = mx + uy * h, p2y = my - ux * h;
  let ex, ey;
  if (p1y > p2y) { ex = p1x; ey = p1y; } else { ex = p2x; ey = p2y; }
  return { ex, ey, hx: tx, hy: ty };
}
