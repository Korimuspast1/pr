import {
  GROUND_Y, VIEW_W, VIEW_H, TAU, clamp, lerp, rand, chance, pick, dist,
  approachAngle, angleDiff, segPointDist,
} from './util.js';
import { WEAPON_BY_ID, WEAPONS, drawWeapon } from './weapons.js';

const SKIN_TONES = ['#d8a179', '#c78e6a', '#e8b896', '#b07a55', '#8d5c3d', '#6d4530'];
const CLOTH = [
  { a: '#43587a', b: '#2d3f59' }, { a: '#6b4a7a', b: '#4a3156' },
  { a: '#3f6b52', b: '#284a37' }, { a: '#8a5a33', b: '#5f3c20' },
  { a: '#5b5f66', b: '#3b3e44' }, { a: '#a33b3b', b: '#732626' },
  { a: '#c8b37a', b: '#93814e' },
];
const PANTS = ['#333a46', '#2f3d55', '#3a3a3f', '#4a3a2a', '#2b3340'];
const HAIR = ['#2a1d16', '#3d2a1a', '#161311', '#5a3a22', '#7a5a35', '#a8a29a'];

function shade(hex, f) {
  const n = parseInt(hex.slice(1), 16);
  let r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
  r = clamp(Math.round(r * f), 0, 255);
  g = clamp(Math.round(g * f), 0, 255);
  b = clamp(Math.round(b * f), 0, 255);
  return `rgb(${r},${g},${b})`;
}

let NEXT_ID = 1;

// Name / description of every body part.
const LIMB_DEFS = [
  { name: 'head', label: 'голова', radius: 5.6, kind: 'skin', bone: 'skull', mass: 5, parent: 'torso' },
  { name: 'torso', label: 'торс', radius: 4.6, kind: 'cloth', bone: 'spine', mass: 26, parent: null },
  { name: 'armN_u', label: 'плечо', radius: 2.8, kind: 'cloth', bone: 'arm', mass: 3, parent: 'torso' },
  { name: 'armN_l', label: 'предплечье', radius: 2.3, kind: 'skin', bone: 'arm', mass: 2, parent: 'armN_u' },
  { name: 'armF_u', label: 'плечо(д)', radius: 2.8, kind: 'cloth', bone: 'arm', mass: 3, parent: 'torso' },
  { name: 'armF_l', label: 'предплечье(д)', radius: 2.3, kind: 'skin', bone: 'arm', mass: 2, parent: 'armF_u' },
  { name: 'legN_u', label: 'бедро', radius: 3.3, kind: 'cloth', bone: 'leg', mass: 7, parent: 'torso' },
  { name: 'legN_l', label: 'голень', radius: 2.7, kind: 'cloth', bone: 'leg', mass: 4, parent: 'legN_u' },
  { name: 'legF_u', label: 'бедро(д)', radius: 3.3, kind: 'cloth', bone: 'leg', mass: 7, parent: 'torso' },
  { name: 'legF_l', label: 'голень(д)', radius: 2.7, kind: 'cloth', bone: 'leg', mass: 4, parent: 'legF_u' },
];

const BONES = [
  ['pelvis', 'chest'], ['chest', 'neck'], ['neck', 'head'],
  ['chest', 'shN'], ['shN', 'elN'], ['elN', 'wrN'],
  ['chest', 'shF'], ['shF', 'elF'], ['elF', 'wrF'],
  ['pelvis', 'hipN'], ['hipN', 'knN'], ['knN', 'anN'],
  ['pelvis', 'hipF'], ['hipF', 'knF'], ['knF', 'anF'],
  ['shN', 'shF'], ['hipN', 'hipF'], ['pelvis', 'neck'],
];

export class Person {
  constructor(game, opts = {}) {
    this.game = game;
    this.id = NEXT_ID++;
    this.isPlayer = !!opts.isPlayer;
    this.isNPC = !this.isPlayer;
    this.name = opts.name || 'Житель';
    this.skin = opts.skin || pick(SKIN_TONES);
    this.cloth = opts.cloth || pick(CLOTH);
    this.pants = opts.pants || pick(PANTS);
    this.hair = opts.hair || pick(HAIR);
    this.weapon = WEAPONS[0];

    this.pts = {};
    for (const k of ['pelvis', 'chest', 'neck', 'head', 'shN', 'elN', 'wrN', 'shF', 'elF', 'wrF',
      'hipN', 'knN', 'anN', 'hipF', 'knF', 'anF']) {
      this.pts[k] = { x: 0, y: 0, vx: 0, vy: 0 };
    }
    this.limbs = [];
    this.wounds = 0;
    this.mags = {};
    this.reset(opts.x || 320, opts.weapon || (this.isPlayer ? 'pistol' : pick(['knife', 'machete', 'axe', 'katana', 'pistol', 'smg', 'shotgun', 'rifle'])));
    this.ai = {
      timer: rand(0, 1), target: null, retarget: 0, aimErr: rand(-0.16, 0.16),
      reaction: rand(0.2, 0.55), burst: 0, burstT: 0, wander: rand(-1, 1), state: 'idle',
      aggression: rand(0.4, 0.62),
    };
  }

  reset(x, weaponId) {
    this.pos = { x, y: GROUND_Y };
    this.vel = { x: 0, y: 0 };
    this.facing = 1;
    this.aimAngle = 0;
    this.weaponAngle = 0;
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
    this.onGround = true;
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
    this.hitmark = 0;
    this.droppedWeapon = null;
    this.lastAttacker = null;
    this.stepT = 0;
    this.breath = rand(0, TAU);
    this.bloodSprayT = 0;
    this.poolT = 0;
    this.bleedTotal = 0;
    this.stumpBleed = 0;
    this.dashT = 0;
    this.dashCd = 0;
    this.dashDir = 1;
    this.limbs.length = 0;
    this.mags = {};
    for (const w of WEAPONS) if (w.magSize) this.mags[w.id] = w.magSize;
    this.setWeapon(weaponId || 'pistol');
    for (const d of LIMB_DEFS) {
      this.limbs.push({
        id: this.id * 100 + this.limbs.length,
        name: d.name, label: d.label, radius: d.radius, kind: d.kind, bone: d.bone,
        mass: d.mass, parent: d.parent,
        a: { x: 0, y: 0 }, b: { x: 0, y: 0 },
        skin: this.skin, cloth: this.cloth.a, pants: this.pants,
        health: 100, struct: 0, severed: false, parentSevered: false,
        broken: false, wounds: [], arterial: false,
        bleedRate: 0, exposed: false,
      });
    }
    this.bindLimbs();
    this.pose(0, true);
  }

  bindLimbs() {
    const L = this.limbs;
    const P = this.pts;
    const f = (n) => L.find((l) => l.name === n);
    // head
    let l = f('head'); l.a = P.neck; l.b = P.head; l.head = true;
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
    return Object.fromEntries(this.limbs.map((l) => [l.name, l]));
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

  /* ================================================================== */
  /*  INPUT / AI                                                        */
  /* ================================================================== */

  controlPlayer(dt, game) {
    if (!this.alive) { this.triggerHeld = false; return; }
    const inp = game.input;
    let mx = 0;
    if (inp.down('KeyA') || inp.down('ArrowLeft')) mx -= 1;
    if (inp.down('KeyD') || inp.down('ArrowRight')) mx += 1;

    const targetX = game.mouse.x, targetY = game.mouse.y;
    const shoulder = this.pts.shN;
    this.aimAngle = Math.atan2(targetY - shoulder.y, targetX - shoulder.x);
    this.facing = Math.cos(this.aimAngle) >= 0 ? 1 : -1;

    if (this.ragdoll) {
      this.moving = false;
      this.triggerHeld = false;
      this.vel.x *= 0.92;
      return;
    }

    this.dashCd = Math.max(0, this.dashCd - dt);
    if (inp.pressed('Space') && this.dashCd <= 0 && this.legsUsable()) {
      this.dashT = 0.17;
      this.dashCd = 0.55;
      this.dashDir = mx !== 0 ? mx : this.facing;
      this.facing = this.dashDir;
      game.audio.wizz();
    }
    const speed = this.legsUsable() ? (inp.down('ShiftLeft') || inp.down('ShiftRight') ? 96 : 64) : 0;
    const accel = 620;
    const want = mx * speed;
    if (this.dashT > 0) {
      this.dashT -= dt;
      this.vel.x = this.dashDir * 320;
      this.pos.x = clamp(this.pos.x + this.vel.x * dt, 12, VIEW_W - 12);
      this.moving = true;
      this.triggerHeld = game.mouse.down;
      this.dashCd = Math.max(this.dashCd, 0.05);
      return;
    }
    if (want !== 0 && speed > 0) {
      this.vel.x += clamp(want - this.vel.x, -accel * dt, accel * dt);
      this.moving = true;
      if (Math.abs(this.vel.x) > 20) this.facing = Math.sign(this.vel.x);
    } else {
      this.vel.x *= Math.exp(-14 * dt);
      this.moving = Math.abs(this.vel.x) > 8;
    }
    this.pos.x = clamp(this.pos.x + this.vel.x * dt, 12, VIEW_W - 12);

    if (inp.pressed('KeyR')) this.startReload();
    for (let i = 0; i < 8; i++) {
      if (inp.pressed('Digit' + (i + 1))) game.selectBar(i);
    }
    if (inp.pressed('Space') && this.ragdoll) { this.ragTime = Math.max(this.ragTime, 1.2); this.tryStandUp(); }

    this.triggerHeld = game.mouse.down;
  }

  updateAI(dt, game) {
    const ai = this.ai;
    this.triggerHeld = false;
    if (!this.alive) return;
    if (ai.frozen) { this.idleWander(dt); return; }

    if (this.ragdoll) {
      this.tryStandUp();
      return;
    }
    if (!this.legsUsable()) { this.vel.x *= 0.9; this.moving = false; return; }

    ai.retarget -= dt;
    if (!ai.target || !ai.target.alive || ai.retarget <= 0) {
      const all = game.entities.filter((e) => e !== this && e.alive);
      const player = all.find((e) => e.isPlayer);
      const otherNPCs = all.filter((e) => e.isNPC);
      // NPCs mostly hunt the player, but sometimes brawl among themselves.
      let candidates;
      if (!otherNPCs.length || chance(this.ai.aggression)) candidates = player ? [player] : [];
      else candidates = otherNPCs;
      let best = null, bestScore = -Infinity;
      for (const c of candidates) {
        const d = dist(this.pos.x, 0, c.pos.x, 0);
        let score = -d;
        if (c === this.lastAttacker) score += 220;
        if (c.isPlayer) score += 20;
        score += rand(-40, 40);
        if (score > bestScore) { bestScore = score; best = c; }
      }
      ai.target = best;
      ai.retarget = rand(3.5, 7);
      ai.aimErr = rand(-0.19, 0.19);
      ai.reaction = rand(0.12, 0.45);
    }

    const t = ai.target;
    if (!t) { this.idleWander(dt); return; }

    const dx = t.pts.chest.x - this.pos.x;
    const dy = t.pts.chest.y - (this.pos.y - 30);
    const d = Math.hypot(dx, dy);
    const wounded = this.health < 32 || this.blood < 1600;
    const melee = this.weapon.kind === 'melee';

    // Panic: run away when badly hurt.
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
      if (chance(dt * 0.4)) this.triggerHeld = true;
      return;
    }

    const wantDist = melee ? 20 : (this.weapon.id === 'shotgun' ? 70 : 130);
    if (d > wantDist * 1.08) {
      const dir = Math.sign(dx) || 1;
      this.vel.x += clamp(dir * 62 - this.vel.x, -520 * dt, 520 * dt);
      this.moving = true;
      this.facing = dir;
    } else {
      this.vel.x *= Math.exp(-10 * dt);
      this.moving = Math.abs(this.vel.x) > 8;
    }
    this.pos.x = clamp(this.pos.x + this.vel.x * dt, 12, VIEW_W - 12);

    // aim with reaction lag & wobble
    ai.reaction -= dt;
    const aimNoise = Math.sin(game.time * 1.7 + this.id) * 0.02 + Math.sin(game.time * 5.3 + this.id * 2) * 0.012;
    const wantAngle = Math.atan2(t.pts.chest.y - this.pts.shN.y, t.pts.chest.x - this.pts.shN.x)
      + ai.aimErr + aimNoise;
    this.aimAngle = approachAngle(this.aimAngle, wantAngle, 5.5 * dt);
    this.weaponAngle = this.aimAngle;
    this.facing = Math.cos(this.aimAngle) >= 0 ? 1 : -1;

    if (ai.reaction <= 0) {
      const aimOk = Math.abs(angleDiff(this.aimAngle, Math.atan2(t.pts.chest.y - this.pts.shN.y, t.pts.chest.x - this.pts.shN.x))) < 0.2;
      if (melee) {
        if (d < 26 && aimOk) this.triggerHeld = true;
      } else {
        if (aimOk && d < 260) this.triggerHeld = chance(dt * (this.weapon.auto ? 2.2 : 1.3));
        if (!this.mags[this.weapon.id] && this.reloading <= 0) this.startReload();
        if (this.mags[this.weapon.id] === 0 && this.reloading <= 0) this.startReload();
      }
    }
  }

  idleWander(dt) {
    if (this.ai.frozen) { this.moving = false; this.vel.x *= Math.exp(-6 * dt); return; }
    this.vel.x *= Math.exp(-6 * dt);
    this.moving = false;
    this.aimAngle = this.facing > 0 ? 0.2 : Math.PI - 0.2;
    this.weaponAngle = this.aimAngle;
  }

  legsUsable() {
    const ln = this.limbs.find((l) => l.name === 'legN_u');
    const lf = this.limbs.find((l) => l.name === 'legF_u');
    const sn = this.limbs.find((l) => l.name === 'legN_l');
    const sf = this.limbs.find((l) => l.name === 'legF_l');
    const bad = (l) => !l || l.severed || l.parentSevered || l.broken;
    return !(bad(ln) || bad(lf) || bad(sn) || bad(sf));
  }

  armUsable() {
    const u = this.limbs.find((l) => l.name === 'armN_u');
    const l = this.limbs.find((l) => l.name === 'armN_l');
    return !(u.severed || u.parentSevered || l.severed || l.parentSevered);
  }

  /* ================================================================== */
  /*  WEAPONS                                                           */
  /* ================================================================== */

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
    // gun
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
    this.extraSpread = (this.moving ? w.movingSpread : 0) + this.sprayHeat;
    this.game.combat.fire(this, w, this.weaponAngle);
    this.hitFlash = Math.max(this.hitFlash, 0);
    this.game.hitstop(w.id === 'shotgun' ? 0.02 : 0.008);
    if (!this.isPlayer) this.lastShotT = this.game.time;
  }

  kick(def) {
    this.aimKick += def.recoil;
    this.vel.x -= Math.cos(this.weaponAngle) * (def.pellets > 1 ? 70 : 26);
    if (def.pellets > 1 && this.ragdoll) {
      // shove ragdoll backwards
      const dir = -Math.sign(Math.cos(this.weaponAngle)) || 1;
      this.vel.x += dir * 30;
    }
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
        const off = this.swingOffset(u);
        const def = sw.def;
        this.weaponAngle = this.aimAngle + off;
        this.pose(0, false);
        const blade = this.game.combat.bladeWorld(this, def, this.weaponAngle);
        if (this.prevBlade && u > 0.16 && u < 0.78) {
          const speed = Math.abs(this.swingRate(u)) * def.bladeLen;
          this.game.combat.meleeSweep(this, def, this.prevBlade, blade, speed, sw.hitDone);
        }
        this.prevBlade = blade;
      }
    }

    if (this.triggerHeld) this.tryAttack();
    if (this.weapon.kind === 'gun' && this.mags[this.weapon.id] === 0 && this.reloading <= 0 && this.alive && !this.ragdoll) {
      this.startReload();
    }
  }

  swingOffset(u) {
    if (u < 0.22) return lerp(0, -1.05, u / 0.22) * (u / 0.22);
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

  /* ================================================================== */
  /*  POSE                                                              */
  /* ================================================================== */

  pose(dt, full = false) {
    const s = this.facing;
    const P = this.pts;
    const ground = this.pos.y;
    const spd = Math.abs(this.vel.x);
    if (this.moving) this.walkPhase += dt * (4 + spd * 0.09);
    else this.walkPhase += dt * 0.8;

    const bob = this.moving ? Math.abs(Math.sin(this.walkPhase)) * 1.1 : Math.sin(this.walkPhase) * 0.25;
    const hipY = ground - 19 - bob * 0.6;
    const lean = this.moving ? Math.min(1.6, spd * 0.014) * Math.sign(this.vel.x || 1) : 0;

    P.pelvis.x = this.pos.x;
    P.pelvis.y = hipY;
    P.chest.x = this.pos.x + s * (1.0 + lean);
    P.chest.y = hipY - 12.5;
    P.neck.x = P.chest.x + s * 1.6;
    P.neck.y = P.chest.y - 3.6;
    P.head.x = P.neck.x + s * 1.1 + Math.sin(this.breath + this.walkPhase * 0.5) * 0.3;
    P.head.y = P.neck.y - 5.2;

    const stride = this.moving ? Math.sin(this.walkPhase) : 0;
    const strideF = -stride;
    const liftN = this.moving ? Math.max(0, Math.sin(this.walkPhase)) * 2.2 : 0;
    const liftF = this.moving ? Math.max(0, Math.sin(this.walkPhase + Math.PI)) * 2.2 : 0;

    P.hipN.x = P.pelvis.x + s * 1.8; P.hipN.y = hipY + 0.6;
    P.knN.x = P.hipN.x + s * (2.0 + stride * 4.2); P.knN.y = hipY + 9.4 - liftN * 0.7;
    P.anN.x = P.knN.x + s * (0.2 - stride * 3.6); P.anN.y = ground - 2.4 - liftN;

    P.hipF.x = P.pelvis.x - s * 1.6; P.hipF.y = hipY + 0.8;
    P.knF.x = P.hipF.x + s * (1.8 + strideF * 4.2); P.knF.y = hipY + 9.4 - liftF * 0.7;
    P.anF.x = P.knF.x + s * (0.1 - strideF * 3.6); P.anF.y = ground - 2.2 - liftF;

    // Arms: near arm holds the weapon, far arm supports guns.
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
    // recoil pulls the hand back a touch
    const kick = this.aimKick * 6;
    hx -= Math.cos(aim) * kick; hy -= Math.sin(aim) * kick;
    const armUpper = 8.4, armLower = 8.2;
    const ik1 = solveIK(P.shN.x, P.shN.y, hx, hy, armUpper, armLower, 1);
    P.elN.x = ik1.ex; P.elN.y = ik1.ey;
    P.wrN.x = ik1.hx; P.wrN.y = ik1.hy;

    if (gun) {
      const supportX = P.shF.x + Math.cos(aim) * (reach - 2.5);
      const supportY = P.shF.y + Math.sin(aim) * (reach - 2.5);
      const ik2 = solveIK(P.shF.x, P.shF.y, supportX, supportY, armUpper, armLower, -1);
      P.elF.x = ik2.ex; P.elF.y = ik2.ey;
      P.wrF.x = ik2.hx; P.wrF.y = ik2.hy;
    } else {
      const swing = this.swing ? this.swingOffset(clamp(this.swing.t / this.swing.dur, 0, 1)) : 0;
      const ta = aim + swing * 0.2 + Math.PI * 0.12;
      const ik2 = solveIK(P.shF.x, P.shF.y, P.shF.x + Math.cos(ta) * 8, P.shF.y + Math.sin(ta) * 8, armUpper, armLower, -1);
      P.elF.x = ik2.ex; P.elF.y = ik2.ey;
      P.wrF.x = ik2.hx; P.wrF.y = ik2.hy;
    }

    // Broken limbs hang limp.
    for (const l of this.limbs) {
      if (l.broken && !l.severed) {
        const end = l.b === P.wrN ? P.elN : null;
        if (l.name === 'armN_u' || l.name === 'armF_u' || l.name === 'legN_u' || l.name === 'legF_u') {
          l.b.y += 4.5; l.b.x -= s * 1.5;
          if (l.b === P.elN) { P.wrN.x = P.elN.x - s * 1; P.wrN.y = P.elN.y + 7; }
          if (l.b === P.elF) { P.wrF.x = P.elF.x - s * 1; P.wrF.y = P.elF.y + 7; }
          if (l.b === P.knN) { P.anN.x = P.knN.x + s * 1; P.anN.y = Math.min(ground - 2, P.knN.y + 9); }
          if (l.b === P.knF) { P.anF.x = P.knF.x + s * 1; P.anF.y = Math.min(ground - 2, P.knF.y + 9); }
        }
      }
    }
  }

  /* ================================================================== */
  /*  PHYSICS                                                           */
  /* ================================================================== */

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
    if (!this.alive || !this.ragdoll) return;
    if (this.ragTime < 1.1) return;
    if (!this.legsUsable()) return;
    if (this.health < 22) return;
    const avgV = (Math.abs(this.pts.chest.vx) + Math.abs(this.pts.pelvis.vx)) * 0.5;
    if (avgV > 40) return;
    this.ragdoll = false;
    this.ragTime = 0;
    this.pos.x = this.pts.pelvis.x;
    this.pos.y = GROUND_Y;
    this.vel.x *= 0.3;
  }

  physics(dt, game) {
    if (!this.ragdoll) {
      if (!this.legsUsable() && this.alive && !this.ragdoll) {
        this.goRagdoll();
      }
      this.pos.y = GROUND_Y;
      return;
    }

    this.ragTime += dt;
    const P = this.pts;
    const G = 900;

    for (const k in P) {
      const p = P[k];
      p.vy += G * dt;
      p.vx *= 1 - 1.8 * dt;
      p.vy *= 1 - 0.4 * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
    }
    // constraints
    const stiffness = 0.72;
    for (let it = 0; it < 2; it++) {
      for (const [an, bn] of BONES) {
        const a = P[an], b = P[bn];
        if (!a || !b) continue;
        if (this.boneSevered(an, bn)) continue;
        let dx = b.x - a.x, dy = b.y - a.y;
        const d = Math.hypot(dx, dy) || 1e-6;
        const rest = this.restBones[an + bn];
        if (!rest) continue;
        const diff = (d - rest) / d * 0.5 * stiffness;
        dx *= diff; dy *= diff;
        if (this.pointMoves(an)) { a.x += dx; a.y += dy; }
        if (this.pointMoves(bn)) { b.x -= dx; b.y -= dy; }
      }
      // ground
      for (const k in P) {
        const p = P[k];
        const r = this.pointRadius(k);
        if (p.y > GROUND_Y - r) {
          p.y = GROUND_Y - r;
          if (p.vy > 0) p.vy *= -0.22;
          p.vx *= 0.78;
          p.vy *= 0.92;
        }
        if (p.x < 6) { p.x = 6; p.vx *= -0.4; }
        if (p.x > VIEW_W - 6) { p.x = VIEW_W - 6; p.vx *= -0.4; }
      }
    }

    const pelvis = P.pelvis;
    this.pos.x = pelvis.x;
    this.pos.y = GROUND_Y;
    this.vel.x = pelvis.vx;

    // pulsing blood pooling when lying
    if (this.alive && this.bleedTotal > 4 && pelvis.y > GROUND_Y - 30) {
      this.poolT -= dt;
      if (this.poolT <= 0) {
        this.poolT = rand(0.2, 0.55);
        const spot = pick(this.limbs.filter((l) => l.severed || l.wounds.length));
        if (spot) {
          const p = this.woundWorldPos(spot);
          game.gore.splatGround(p.x, GROUND_Y, rand(1.2, 3.6) * clamp(spot.bleedRate / 8, 0.5, 1.5), 0.4);
        }
      }
    }
  }

  pointMoves(k) {
    return true;
  }

  pointRadius(k) {
    if (k === 'head') return 5.2;
    if (k === 'pelvis' || k === 'chest') return 4.2;
    if (k === 'wrN' || k === 'wrF') return 1.8;
    if (k === 'anN' || k === 'anF') return 2.2;
    return 2.6;
  }

  boneSevered(an, bn) {
    const L = this.limbs;
    const byPts = (a, b) => L.find((l) => (l.a === this.pts[a] && l.b === this.pts[b]) || (l.b === this.pts[a] && l.a === this.pts[b]));
    const limb = byPts(an, bn);
    if (limb) {
      if (limb.severed || limb.parentSevered) return true;
      if (limb.name === 'torso') return false;
    }
    return false;
  }

  ensureRestBones() {
    if (this.restBones) return;
    this.restBones = {};
    const pose = { ...this.pts };
    // compute rest lengths from the T-pose (first pose was already computed)
    for (const [a, b] of BONES) {
      this.restBones[a + b] = Math.hypot(pose[b].x - pose[a].x, pose[b].y - pose[a].y);
    }
  }

  /* ================================================================== */
  /*  DAMAGE / GORE                                                     */
  /* ================================================================== */

  limbDamageMultiplier(limb) {
    if (limb.name === 'head') return 2.4;
    if (limb.name === 'torso') return 1.0;
    if (limb.bone === 'arm') return 0.42;
    return 0.5;
  }

  applyHit(limb, ev) {
    const game = this.game;
    this.hitFlash = 0.12;
    if (ev.attacker && ev.attacker !== this) {
      this.lastAttacker = ev.attacker;
      if (!this.ragdoll && (ev.type === 'blunt' || (ev.knock || 0) > 150 || ev.dmg > 42)) {
        this.goRagdoll();
      }
    }

    if (!this.alive && !limb.mass) return;

    const mult = this.limbDamageMultiplier(limb);
    const dmg = ev.dmg * mult * (limb.severed ? 0.4 : 1);
    this.health -= dmg;
    this.pain = Math.min(1.5, this.pain + ev.dmg / 60);

    if (this.alive && (this.isPlayer || this.isNPC)) {
      if (ev.dmg > 18 && chance(0.5)) game.audio.scream();
      if (this.isPlayer) {
        game.shake(Math.min(6, ev.dmg * 0.16));
        game.gore.screenBlood(ev.at.x, ev.at.y, clamp(ev.dmg / 45, 0.15, 1));
        game.damageFlash = Math.max(game.damageFlash, clamp(ev.dmg / 60, 0.12, 0.7));
      }
    }

    // structural (tissue) damage
    if (!limb.severed) {
      limb.struct = clamp(limb.struct + ev.struct * (ev.type === 'blunt' ? 1.25 : 1), 0, 1.4);
      limb.exposed = limb.struct > 0.42;
      if (limb.struct > 0.72 && (limb.bone === 'arm' || limb.bone === 'leg')) limb.broken = true;
    }

    // Wounds & bleeding
    const badness = ev.type === 'bullet' ? 1.35 : ev.type === 'slash' ? 1.2 : 0.8;
    const cut = ev.cut != null ? ev.cut : 0.2;
    const bleedAdd = (0.55 + dmg * 0.16) * badness * cut * (ev.bleedMul || 1) * (limb.name === 'head' ? 1.6 : 1);
    const isArterial = (limb.bone === 'leg' || limb.bone === 'arm') && ev.struct > 0.12 && chance(0.35 + ev.struct * 2);

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
    this.wounds++;
    if (isArterial) {
      limb.arterial = true;
      game.gore.spray(ev.at.x, ev.at.y, ev.dir.x, ev.dir.y, {
        count: 16, speed: 260, size: 2, spread: 0.5, mistChance: 0.15,
      });
      game.audio.splat();
    }

    // Knockback
    const kb = (ev.knock || 0) * 0.35;
    if (ev.dir && kb) {
      if (this.ragdoll) {
        this.applyImpulse({ x: ev.dir.x * kb * 0.35, y: ev.dir.y * kb * 0.2 });
      } else {
        this.vel.x += ev.dir.x * kb * 0.55;
        if (Math.abs(ev.dir.y) > 0.5 && ev.dmg > 25) this.goRagdoll({ x: ev.dir.x * 8, y: -40 });
      }
    }

    // Bone / skull effects
    if (limb.bone === 'skull') {
      if (limb.struct > 0.9 || (ev.type === 'bullet' && ev.dmg > 40 && chance(0.4))) {
        this.headDestroyed(ev);
        return;
      }
    }
    if (limb.name === 'torso' && limb.struct > 0.85 && ev.type !== 'bullet' && chance(0.5)) {
      this.gutSpill(ev);
    }

    // Sever check
    const canSever = ['armN_u', 'armN_l', 'armF_u', 'armF_l', 'legN_u', 'legN_l', 'legF_u', 'legF_l', 'head'].includes(limb.name);
    if (canSever && !limb.severed) {
      let severChance = (1 - clamp(limb.struct, 0, 1)) * 0.1 + limb.struct * 0.85 + cut * (ev.struct || 0) * 1.4 - 0.16;
      if (ev.type === 'bullet') severChance *= 0.55;
      if (limb.broken) severChance += 0.2;
      if (chance(clamp(severChance, 0, 0.95))) this.sever(limb, ev);
    }

    this.checkDeath(ev);
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
    const x = lerp(limb.a.x, limb.b.x, w.t);
    const y = lerp(limb.a.y, limb.b.y, w.t);
    const ang = Math.atan2(limb.b.y - limb.a.y, limb.b.x - limb.a.x) + Math.PI / 2;
    return { x: x + Math.cos(ang) * w.side * limb.radius * 0.9, y: y + Math.sin(ang) * w.side * limb.radius * 0.9 };
  }

  headDestroyed(ev) {
    const head = this.limbs.find((l) => l.name === 'head');
    if (!head.severed) this.sever(head, ev, true);
    this.deathCause = 'череп разрушен';
    this.die();
    const p = this.pts.head;
    this.game.gore.spray(p.x, p.y, 0, -1, { count: 40, speed: 320, size: 2.4, spread: 1.1, mistChance: 0.3 });
    this.game.gore.flesh(p.x, p.y, 0, -1, 10, 1.4);
    this.game.gore.boneShard(p.x, p.y, 0, -1, 8);
    this.game.gore.mist(p.x, p.y, 0, -1, 14, 1.4, 1.5);
    this.game.audio.sever();
  }

  gutSpill(ev) {
    const torso = this.limbs.find((l) => l.name === 'torso');
    const p = { x: (torso.a.x + torso.b.x) / 2, y: (torso.a.y + torso.b.y) / 2 };
    this.game.gore.flesh(p.x, p.y, rand(-0.4, 0.4), 1, 14, 1.2);
    this.game.gore.spray(p.x, p.y, 0, 1, { count: 22, speed: 150, size: 2.2, spread: 1.2, mistChance: 0.2 });
    torso.wounds.push({ t: 0.5, side: 1, r: 3.5, bleed: 9, arterial: true, kind: 'gut', age: 0 });
    torso.arterial = true;
    this.game.audio.sever();
    if (!this.ragdoll) this.goRagdoll();
  }

  sever(limb, ev, silent = false) {
    const game = this.game;
    limb.severed = true;
    limb.struct = 1.2;
    const ax = limb.a.x, ay = limb.a.y;
    const bx = limb.b.x, by = limb.b.y;
    const len = Math.hypot(bx - ax, by - ay);
    const ang = Math.atan2(by - ay, bx - ax);

    // Detached piece gets a life of its own.
    game.debris.push({
      type: 'limb',
      a: { x: ax, y: ay, vx: rand(-40, 40) + (ev?.dir?.x || 0) * 40, vy: rand(-90, -20) },
      b: { x: bx, y: by, vx: rand(-40, 40) + (ev?.dir?.x || 0) * 40, vy: rand(-90, -20) },
      len, rest: len, radius: limb.radius,
      color: limb.kind === 'skin' ? this.skin : limb.cloth,
      pants: limb.kind === 'cloth' && (limb.bone === 'leg'),
      kind: limb.kind, bone: limb.bone, rot: ang, vr: rand(-9, 9),
      wounds: limb.wounds.slice(-3),
      stumpA: true, stumpB: false,
      life: 60, bleed: limb.bone === 'head' ? 12 : 9,
      decay: true,
    });

    // The proximal stump keeps bleeding from the body.
    limb.stumpBleed = limb.bone === 'head' ? 55 : limb.bone === 'leg' ? 21 : 17;
    limb.arterial = true;

    // Children fall off with it.
    for (const l of this.limbs) {
      if (l.parent === limb.name && !l.severed) {
        l.parentSevered = true;
        l.struct = 1;
      }
    }

    // Whatever was in the severed hand clatters to the ground.
    if (limb.name === 'armN_u' || limb.name === 'armN_l') {
      if (this.weapon.id !== 'fists') {
        game.debris.push({
          type: 'weapon', def: this.weapon,
          x: this.pts.wrN.x, y: this.pts.wrN.y,
          vx: rand(-60, 60), vy: rand(-140, -50), rot: this.weaponAngle, vr: rand(-14, 14), life: 45,
        });
      }
      this.weapon = WEAPON_BY_ID.fists;
      this.swing = null;
      this.reloading = 0;
    }

    game.gore.spray(ax, ay, ev?.dir?.x || rand(-1, 1), ev?.dir?.y || -0.5, {
      count: 32, speed: 210, size: 2.3, spread: 0.8, mistChance: 0.25,
    });
    game.gore.mist(ax, ay, ev?.dir?.x || 0, -0.6, 12, 1.1, 1.4);
    game.gore.flesh(ax, ay, ev?.dir?.x || 0, -1, 6, 1);
    game.gore.boneShard(ax, ay, ev?.dir?.x || 0, -1, 4);
    game.audio.sever();
    game.addLog(`${this.isPlayer ? 'Вы' : this.name}: оторвано — ${limb.label}`, '#d98a7a');
    if (this.isPlayer) {
      game.shake(5);
      game.gore.screenBlood(ax, ay, 1.5);
    }

    if (limb.bone === 'leg') this.goRagdoll();
    if (limb.bone === 'skull') {
      this.deathCause = 'обезглавлен';
      this.die();
    }
    // Arms can't hold the weapon any more.
    if ((limb.name === 'armN_u' || limb.name === 'armN_l') && this.armUsable()) {
      // still usable if the other segment survived
    }
    this.checkDeath(ev);
  }

  checkDeath(ev) {
    if (!this.alive) return;
    const head = this.limbs.find((l) => l.name === 'head');
    const torso = this.limbs.find((l) => l.name === 'torso');
    if (head.severed) { this.deathCause = 'обезглавлен'; this.die(); return; }
    if (this.health <= 0) { this.deathCause = 'потерял слишком много крови'; this.die(); return; }
    if (torso.severed) { this.die(); }
  }

  die() {
    if (!this.alive) return;
    this.alive = false;
    this.game.addLog(`${this.isPlayer ? 'Вы' : this.name} погиб — ${this.deathCause || 'рана'}`, '#c9b98a');
    this.game.gore.splatGround(this.pts.pelvis.x, GROUND_Y, 4, 0.5);
    this.deadTime = 0;
    this.goRagdoll();
    this.triggerHeld = false;
    this.swing = null;
    this.reloading = 0;
    if (this.isPlayer) this.game.onPlayerDeath();
  }

  /* ================================================================== */
  /*  BLEEDING / HEALTH                                                 */
  /* ================================================================== */

  updateGore(dt, game) {
    if (this.hitFlash > 0) this.hitFlash -= dt;
    let total = 0;
    for (const limb of this.limbs) {
      limb.bleedRate = 0;
      if (limb.parentSevered) continue;
      for (const w of limb.wounds) {
        w.age += dt;
        // wounds clot slowly
        if (w.kind === 'bullet') w.bleed *= 1 - 0.02 * dt;
        else w.bleed *= 1 - 0.006 * dt;
        limb.bleedRate += w.bleed;
      }
      if (limb.severed) limb.bleedRate += limb.stumpBleed || 12;
      if (limb.bleedRate > 0) {
        total += limb.bleedRate;
        const p = this.woundWorldPos(limb);
        const arterial = limb.arterial || limb.severed;
        // a heart still beats: arterial spurts come in pulses
        const pulse = arterial ? 0.45 + 0.75 * Math.max(0, Math.sin(game.time * 7.2 + this.id)) : 1;
        const dirAng = arterial ? rand(0, TAU) : Math.PI / 2;
        game.gore.bleed(
          p.x, p.y,
          Math.cos(dirAng), Math.sin(dirAng),
          limb.bleedRate * (arterial ? 2.4 : 1.5) * pulse,
          arterial, dt
        );
        if (limb.severed && chance(dt * 3)) {
          game.gore.splatGround(p.x, GROUND_Y, rand(2, 5), 0.5);
        }
      }
    }
    this.bleedTotal = total;

    // Blood volume loss and hypovolemic shock
    this.blood = Math.max(0, this.blood - total * dt);
    if (this.alive) {
      if (this.blood < 3400) {
        const shock = (3400 - this.blood) / 3400;
        this.health -= shock * 4.5 * dt;
      }
      if (this.blood <= 0) this.health -= 10 * dt;
      if (this.health <= 0) {
        this.deathCause = this.deathCause || 'обескровлен';
        this.die();
      }
    }

    // Persistent blood pooling under badly wounded people
    if (total > 2) {
      this.poolT -= dt;
      if (this.poolT <= 0) {
        this.poolT = clamp(0.8 / Math.max(1, total * 0.06), 0.12, 0.9);
        const p = this.pts.pelvis;
        game.gore.splatGround(p.x + rand(-6, 6), GROUND_Y, clamp(total * 0.11, 0.6, 4.2), 0.36);
      }
    }
  }

  update(dt, game) {
    this.animT += dt;
    if (this.hitFlash > 0) this.hitFlash -= dt;

    if (this.isPlayer) this.controlPlayer(dt, game);
    else this.updateAI(dt, game);

    if (!this.alive) this.deadTime += dt;

    this.updateWeapon(dt);
    if (!this.ragdoll) this.pose(dt);
    this.ensureRestBones();
    this.physics(dt, game);
    this.updateGore(dt, game);

    if (this.ragdoll && this.alive && this.ragTime > 1.7 && this.health > 25 && this.legsUsable()) {
      if (!this.isPlayer) this.tryStandUp();
      else if (chance(dt * 0.7)) this.tryStandUp();
    }

    // Footstep audio
    if (this.moving && !this.ragdoll && this.animT > 0.2) {
      this.stepT -= dt;
      if (this.stepT <= 0) {
        this.stepT = 0.33;
        if (this.isPlayer) game.audio.step();
        else if (dist(this.pos.x, 0, game.player.pos.x, 0) < 140) game.audio.step();
      }
    }

    if (this.aimKick > 0.4) this.aimKick = 0.4;
    if (this.pain > 0 && chance(dt * 0.2)) this.pain *= 0.995;
  }

  /* ================================================================== */
  /*  DRAW                                                              */
  /* ================================================================== */

  draw(ctx, game) {
    if (this.ragdoll) this.drawRagdoll(ctx, game);
    else this.drawStanding(ctx, game);
    this.drawHealthBar(ctx);
  }

  drawLimbLine(ctx, a, b, r, color, outline = true) {
    if (a.x === undefined) return;
    const dx = b.x - a.x, dy = b.y - a.y;
    const len = Math.hypot(dx, dy);
    if (len < 0.4) {
      if (outline) {
        ctx.fillStyle = '#161018';
        ctx.fillRect(a.x - r - 0.6, a.y - r - 0.6, r * 2 + 1.2, r * 2 + 1.2);
      }
      ctx.fillStyle = color;
      ctx.fillRect(a.x - r, a.y - r, r * 2, r * 2);
      return;
    }
    const steps = Math.max(1, Math.ceil(len / 1.5));
    for (let s = 0; s <= steps; s++) {
      const t = s / steps;
      const x = a.x + dx * t, y = a.y + dy * t;
      const w = r * (1 - 0.18 * t) + 0.25;
      if (outline) {
        ctx.fillStyle = '#1a131c';
        ctx.fillRect(Math.round(x - w - 0.7), Math.round(y - w - 0.7), Math.ceil(w * 2 + 1.4), Math.ceil(w * 2 + 1.4));
      }
      ctx.fillStyle = color;
      ctx.fillRect(Math.round(x - w), Math.round(y - w), Math.ceil(w * 2), Math.ceil(w * 2));
    }
  }

  // Fat silhouette pass so all limbs read as one body.
  drawSolid(ctx, a, b, r, color) {
    const dx = b.x - a.x, dy = b.y - a.y;
    const len = Math.hypot(dx, dy);
    const steps = Math.max(1, Math.ceil(len / 1.5));
    ctx.fillStyle = color;
    for (let s = 0; s <= steps; s++) {
      const t = s / steps;
      const x = a.x + dx * t, y = a.y + dy * t;
      const w = r * (1 - 0.16 * t) + 0.7;
      ctx.fillRect(Math.round(x - w), Math.round(y - w), Math.ceil(w * 2), Math.ceil(w * 2));
    }
  }

  drawWoundsOn(ctx, limb) {
    if (!limb.wounds.length && !limb.severed) return;
    const ax = limb.a.x, ay = limb.a.y, bx = limb.b.x, by = limb.b.y;
    const ang = Math.atan2(by - ay, bx - ax);
    const nx = Math.cos(ang + Math.PI / 2), ny = Math.sin(ang + Math.PI / 2);
    for (const w of limb.wounds) {
      const x = lerp(ax, bx, w.t) + nx * w.side * limb.radius * 0.55;
      const y = lerp(ay, by, w.t) + ny * w.side * limb.radius * 0.55;
      const r = Math.max(0.9, w.r);
      ctx.fillStyle = w.kind === 'blunt' ? '#5a1a2a' : '#5b0609';
      ctx.beginPath();
      ctx.arc(x, y, r, 0, TAU);
      ctx.fill();
      ctx.fillStyle = 'rgba(20,2,4,0.6)';
      ctx.fillRect(Math.round(x - r * 0.5), Math.round(y - r * 0.5), Math.max(1, Math.round(r)), Math.max(1, Math.round(r)));
    }
    if (limb.struct > 0.45 && !limb.severed) {
      ctx.globalAlpha = clamp((limb.struct - 0.45) * 1.5, 0, 0.7);
      ctx.strokeStyle = '#7c0f13';
      ctx.lineWidth = limb.radius * 0.8;
      ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke();
      ctx.globalAlpha = 1;
    }
    if (limb.severed) {
      ctx.fillStyle = '#6d0609';
      ctx.beginPath(); ctx.arc(ax, ay, limb.radius * 0.9, 0, TAU); ctx.fill();
      ctx.fillStyle = '#3c0204';
      ctx.beginPath(); ctx.arc(ax, ay, limb.radius * 0.45, 0, TAU); ctx.fill();
    }
  }

  visibleLimbs() {
    const L = this.limb;
    return {
      L,
      backLeg: [L.legF_u, L.legF_l].filter((l) => !l.severed && !l.parentSevered),
      backArm: [L.armF_u, L.armF_l].filter((l) => !l.severed && !l.parentSevered),
      frontLeg: [L.legN_u, L.legN_l].filter((l) => !l.severed && !l.parentSevered),
      frontArm: [L.armN_u, L.armN_l].filter((l) => !l.severed && !l.parentSevered),
      torso: L.torso.severed ? null : L.torso,
      head: L.head.severed || L.head.parentSevered ? null : L.head,
    };
  }

  drawShadow(ctx) {
    const p = this.ragdoll ? this.pts.chest : this.pts.pelvis;
    ctx.globalAlpha = 0.32;
    ctx.fillStyle = '#0a0908';
    ctx.beginPath();
    ctx.ellipse(p.x, GROUND_Y + 2, this.ragdoll ? 16 : 10, 2.6, 0, 0, TAU);
    ctx.fill();
    ctx.globalAlpha = 1;
  }

  drawBody(ctx) {
    const P = this.pts;
    const v = this.visibleLimbs();
    const flicker = this.hitFlash > 0;
    const skin = flicker ? '#ffd7d0' : this.skin;
    const clothA = flicker ? '#ffd7d0' : this.cloth.a;
    const clothB = flicker ? '#ffd7d0' : shade(this.cloth.b, 0.7);
    const pants = flicker ? '#ffd7d0' : this.pants;
    const OUT = '#141019';

    const segs = [];
    const add = (limb, color, layer) => { if (limb) segs.push({ limb, color, layer }); };
    add(v.backArm[0], shade(clothA, 0.66), 0);
    add(v.backArm[1], shade(skin, 0.7), 0);
    add(v.backLeg[0], shade(pants, 0.7), 0);
    add(v.backLeg[1], shade(pants, 0.66), 0);
    add(v.frontLeg[0], pants, 1);
    add(v.frontLeg[1], shade(pants, 0.92), 1);
    add(v.torso, clothA, 1);
    add(v.frontArm[0], clothA, 2);
    add(v.frontArm[1], skin, 2);
    if (v.head) segs.push({ head: v.head, layer: 2 });
    segs.sort((a, b) => a.layer - b.layer);

    // silhouette pass
    for (const s of segs) {
      if (s.head) {
        ctx.fillStyle = OUT;
        ctx.beginPath(); ctx.arc(P.head.x, P.head.y, 6.4, 0, TAU); ctx.fill();
      } else {
        this.drawSolid(ctx, s.limb.a, s.limb.b, s.limb.radius, OUT);
      }
    }

    // colored pass
    for (const s of segs) {
      if (s.head) continue;
      this.drawLimbLine(ctx, s.limb.a, s.limb.b, s.limb.radius, s.color, false);
      if (s.limb.kind === 'cloth' && s.limb.bone === 'arm' && s.limb.name.endsWith('_u')) {
        // rolled sleeve edge
        ctx.fillStyle = shade(s.color, 0.8);
        ctx.fillRect(Math.round(s.limb.b.x - s.limb.radius), Math.round(s.limb.b.y - s.limb.radius), 2, Math.ceil(s.limb.radius * 2));
      }
    }

    // torso details
    if (v.torso) {
      const nx = P.neck.x, ny = P.neck.y, px2 = P.pelvis.x, py2 = P.pelvis.y;
      const ang = Math.atan2(ny - py2, nx - px2);
      const mx = (px2 + nx) / 2, my = (py2 + ny) / 2;
      ctx.save();
      ctx.translate(mx, my);
      ctx.rotate(ang - Math.PI / 2);
      // vest / jacket shade
      ctx.fillStyle = clothB;
      ctx.fillRect(-3.4, -5.5, 6.8, 9.5);
      // belt
      ctx.fillStyle = '#241d19';
      ctx.fillRect(-3.8, 4.2, 7.6, 2);
      ctx.fillStyle = '#8a7f63';
      ctx.fillRect(-0.8, 4.3, 1.6, 1.8);
      // backpack for some people
      if (this.id % 3 === 0 && !this.ragdoll) {
        ctx.fillStyle = '#3b3a2e';
        ctx.fillRect(-4.6, -5.2, 3.2, 7.5);
        ctx.fillStyle = '#565441';
        ctx.fillRect(-4.2, -4.6, 2.4, 2);
      }
      ctx.restore();
      this.drawWoundsOn(ctx, v.torso);
      if (v.torso.struct > 0.78) {
        ctx.fillStyle = '#7a1a1f';
        ctx.beginPath(); ctx.ellipse(mx + this.facing * 2, my + 3, 3.2, 4, 0.4, 0, TAU); ctx.fill();
        ctx.fillStyle = '#b1545a';
        ctx.fillRect(Math.round(mx + this.facing), Math.round(my + 2), 2, 2);
      }
    } else {
      // torso ripped off: stump
      ctx.fillStyle = '#5b0609';
      ctx.beginPath(); ctx.arc(P.pelvis.x, P.pelvis.y, 3.4, 0, TAU); ctx.fill();
    }

    // head
    if (v.head) {
      const hx = P.head.x, hy = P.head.y;
      const s = this.ragdoll ? Math.sign(P.head.x - P.neck.x || 1) : this.facing;
      ctx.fillStyle = skin;
      ctx.beginPath(); ctx.arc(hx, hy, 5.3, 0, TAU); ctx.fill();
      // jaw shade
      ctx.fillStyle = shade(skin, 0.86);
      ctx.fillRect(Math.round(hx - 3), Math.round(hy + 2.2), 6, 3);
      // hair
      ctx.fillStyle = this.hair;
      ctx.beginPath();
      ctx.arc(hx - s * 0.5, hy - 1.6, 5.15, Math.PI * 0.92, Math.PI * 2.18);
      ctx.fill();
      ctx.fillRect(Math.round(hx - s * 5.4), Math.round(hy - 2.5), 2.8, 3.6);
      // eyes / face
      if (this.alive) {
        ctx.fillStyle = '#1a1016';
        if (this.ragdoll) {
          ctx.fillRect(Math.round(hx - 2), Math.round(hy - 0.5), 1.6, 1.6);
          ctx.fillRect(Math.round(hx + 1.2), Math.round(hy - 0.5), 1.6, 1.6);
        } else {
          ctx.fillRect(Math.round(hx + s * 2.1), Math.round(hy - 0.6), 1.6, 1.8);
          if (this.pain > 0.35) {
            ctx.fillStyle = '#4a1010';
            ctx.fillRect(Math.round(hx + s * 1.3), Math.round(hy + 2.6), 2.6, 1.4);
          } else {
            ctx.fillStyle = 'rgba(70,25,25,0.75)';
            ctx.fillRect(Math.round(hx + s * 1.3), Math.round(hy + 2.4), 2.2, 1);
          }
        }
      } else {
        ctx.fillStyle = '#2a0a0a';
        ctx.fillRect(Math.round(hx - 2.2), Math.round(hy - 1.4), 2, 1);
        ctx.fillRect(Math.round(hx + 0.6), Math.round(hy - 1.4), 2, 1);
        ctx.fillRect(Math.round(hx - 1.4), Math.round(hy + 1.6), 2.8, 1.2);
      }
      this.drawWoundsOn(ctx, v.head);
      if (v.head.struct > 0.45 && this.alive) {
        ctx.fillStyle = 'rgba(90,6,9,0.6)';
        ctx.fillRect(Math.round(hx + s * 1.6), Math.round(hy + 0.6), 1.4, 3.6);
      }
    } else {
      ctx.fillStyle = '#5b0609';
      ctx.beginPath(); ctx.arc(P.neck.x, P.neck.y - 1, 2.8, 0, TAU); ctx.fill();
      ctx.fillStyle = '#e6ddc8';
      ctx.fillRect(Math.round(P.neck.x - 0.8), Math.round(P.neck.y - 1.6), 1.6, 1.6);
    }

    // shoes
    for (const l of [v.frontLeg[1], v.backLeg[1]]) {
      if (!l) continue;
      ctx.fillStyle = '#211a15';
      ctx.fillRect(Math.round(l.b.x - 3), Math.round(l.b.y - 1), 6, 3);
      ctx.fillStyle = '#3a2f26';
      ctx.fillRect(Math.round(l.b.x - 3), Math.round(l.b.y + 1.4), 6, 1);
    }
    // gloves / hands
    for (const l of [v.frontArm[1], v.backArm[1]]) {
      if (!l) continue;
      ctx.fillStyle = '#2f2a25';
      ctx.fillRect(Math.round(l.b.x - 1.4), Math.round(l.b.y - 1.4), 2.8, 2.8);
    }

    // weapon
    if (v.frontArm.length === 2 && this.weapon) {
      if (!this.ragdoll || this.alive) {
        drawWeapon(ctx, this.weapon, P.wrN.x, P.wrN.y, this.weaponAngle, 1, 0);
      }
    }
  }

  draw(ctx, game) {
    this.drawShadow(ctx);
    this.drawBody(ctx);
    if (!this.ragdoll && this.reloading > 0) {
      const k = 1 - this.reloading / this.reloadDur;
      ctx.fillStyle = 'rgba(0,0,0,0.5)';
      ctx.fillRect(Math.round(this.pos.x - 8), Math.round(this.pos.y + 4), 16, 2);
      ctx.fillStyle = '#e0c060';
      ctx.fillRect(Math.round(this.pos.x - 8), Math.round(this.pos.y + 4), Math.round(16 * k), 2);
    }
    this.drawHealthBar(ctx);
  }

  drawHealthBar(ctx) {
    if (!this.alive) return;
    const hurt = this.health < 99.5 || this.bleedTotal > 0.2;
    if (!hurt && !this.isPlayer) return;
    const p = this.ragdoll ? this.pts.chest : this.pts.head;
    const w = 20, x = Math.round(p.x - w / 2), y = Math.round(p.y - (this.ragdoll ? 13 : 12));
    ctx.fillStyle = 'rgba(10,8,12,0.75)';
    ctx.fillRect(x - 1, y - 1, w + 2, 6);
    ctx.fillStyle = '#3a1013';
    ctx.fillRect(x, y, w, 2);
    ctx.fillStyle = this.health > 50 ? '#c0272d' : '#8d1418';
    ctx.fillRect(x, y, Math.round(w * clamp(this.health / 100, 0, 1)), 2);
    ctx.fillStyle = '#241014';
    ctx.fillRect(x, y + 2.6, w, 1.6);
    ctx.fillStyle = '#8f0f16';
    ctx.fillRect(x, y + 2.6, Math.round(w * clamp(this.blood / 5000, 0, 1)), 1.6);
  }
}

/* 2-bone IK; bend direction picks the lower elbow for a natural look. */
function solveIK(sx, sy, tx, ty, l1, l2, prefer) {
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
  // pick the elbow that hangs lower (bigger y)
  let ex, ey;
  if (p1y > p2y) { ex = p1x; ey = p1y; } else { ex = p2x; ey = p2y; }
  return { ex, ey, hx: tx, hy: ty };
}
