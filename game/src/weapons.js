import { GROUND_Y, VIEW_W, TAU, clamp, rand, chance, pick, segSegClosest, roundRect, shade } from './util.js';

/* ------------------------------------------------------------------ */
/*  CATALOG                                                            */
/* ------------------------------------------------------------------ */

const melee = (o) => ({ kind: 'melee', reach: o.bladeLen, ...o });
const gun = (o) => ({ kind: 'gun', ...o });

export const WEAPONS = [
  melee({
    id: 'fists', name: 'Кулаки', short: 'Кулаки', icon: 'fist',
    dmg: 9, structDmg: 0.012, cut: 0.0, bladeLen: 4, bladeW: 1.6, hilt: 1.5,
    swingTime: 0.32, bleedMul: 0.35, knock: 80, color: '#c98d63',
  }),
  melee({
    id: 'knife', name: 'Нож', short: 'Нож', icon: 'knife',
    dmg: 34, structDmg: 0.1, cut: 0.85, bladeLen: 10, bladeW: 1.5, hilt: 3.2,
    swingTime: 0.28, bleedMul: 1.15, knock: 60, guard: 0.9, curve: 0.3,
  }),
  melee({
    id: 'machete', name: 'Мачете', short: 'Мачете', icon: 'machete',
    dmg: 56, structDmg: 0.19, cut: 0.95, bladeLen: 20, bladeW: 2.4, hilt: 4.2,
    swingTime: 0.36, bleedMul: 1.3, knock: 110, guard: 0, curve: 0.9, wide: 1,
  }),
  melee({
    id: 'axe', name: 'Топор', short: 'Топор', icon: 'axe',
    dmg: 80, structDmg: 0.46, cut: 0.25, bladeLen: 17, bladeW: 3.8, hilt: 12,
    swingTime: 0.48, bleedMul: 0.8, knock: 210, guard: 0, axe: true,
  }),
  melee({
    id: 'katana', name: 'Катана', short: 'Катана', icon: 'katana',
    dmg: 72, structDmg: 0.27, cut: 1.0, bladeLen: 27, bladeW: 1.35, hilt: 5.4,
    swingTime: 0.34, bleedMul: 1.45, knock: 95, guard: 1.6, curve: 1.7, tsuba: true,
  }),
  gun({
    id: 'pistol', name: 'Пистолет', short: 'Пистолет', icon: 'pistol', ammo: '9mm',
    magSize: 12, rpm: 340, auto: false, dmg: 24, pellets: 1,
    spread: 0.022, movingSpread: 0.055, recoil: 0.055, shake: 3.2,
    speed: 1750, reloadTime: 1.55, casing: true, tracer: false, pierce: 0,
    flash: 6, sound: 'pistol', structMul: 0.055, bleedMul: 1.2, len: 10,
    muzzle: { x: 10, y: -2.1 },
  }),
  gun({
    id: 'smg', name: 'ПП «Вихрь»', short: 'ПП', icon: 'smg', ammo: '9mm',
    magSize: 30, rpm: 760, auto: true, dmg: 18, pellets: 1,
    spread: 0.03, movingSpread: 0.075, recoil: 0.032, shake: 2.2,
    speed: 1900, reloadTime: 1.9, casing: true, tracer: true, pierce: 0,
    flash: 6, sound: 'smg', structMul: 0.045, bleedMul: 1.1, len: 17,
    muzzle: { x: 17, y: -2.2 },
  }),
  gun({
    id: 'shotgun', name: 'Дробовик', short: 'Дробовик', icon: 'shotgun', ammo: '12g',
    magSize: 6, rpm: 80, auto: false, dmg: 16, pellets: 9,
    spread: 0.15, movingSpread: 0.1, recoil: 0.16, shake: 7,
    speed: 1550, reloadTime: 2.6, casing: true, tracer: false, pierce: 0,
    flash: 10, sound: 'shotgun', structMul: 0.05, bleedMul: 1.4, len: 25,
    muzzle: { x: 25, y: -2.4 },
  }),
  gun({
    id: 'rifle', name: 'Снайперская СВД', short: 'СВД', icon: 'rifle', ammo: '7.62',
    magSize: 10, rpm: 55, auto: false, dmg: 74, pellets: 1,
    spread: 0.006, movingSpread: 0.16, recoil: 0.13, shake: 6.5,
    speed: 2900, reloadTime: 2.9, casing: true, tracer: true, pierce: 1,
    flash: 9, sound: 'rifle', structMul: 0.17, bleedMul: 1.7, len: 29,
    muzzle: { x: 29, y: -2.2 },
  }),
];

export const WEAPON_BY_ID = Object.fromEntries(WEAPONS.map((w) => [w.id, w]));
export const MELEE_IDS = ['fists', 'knife', 'machete', 'axe', 'katana'];
export const GUN_IDS = ['pistol', 'smg', 'shotgun', 'rifle'];

/* ------------------------------------------------------------------ */
/*  VECTOR SPRITES                                                     */
/* ------------------------------------------------------------------ */

const STEEL_LIGHT = '#e4e9ef';
const STEEL = '#aab3bf';
const STEEL_DARK = '#6b7480';
const WOOD = '#6f4b2c';
const WOOD_LIGHT = '#8a6039';
const DARK = '#23262c';
const POLY = '#2b2e34';

function metal(ctx, x0, y0, x1, y1) {
  const g = ctx.createLinearGradient(x0, y0, x0, y1);
  g.addColorStop(0, '#f2f5f8');
  g.addColorStop(0.35, STEEL);
  g.addColorStop(0.75, STEEL_DARK);
  g.addColorStop(1, '#4a515b');
  return g;
}

function drawGrip(ctx, len, halfH = 1.05, color = '#3a2a1e') {
  roundRect(ctx, -len, -halfH, len + 1.2, halfH * 2, halfH * 0.9);
  ctx.fillStyle = color;
  ctx.fill();
  ctx.strokeStyle = 'rgba(10,8,8,0.55)';
  ctx.lineWidth = 0.35;
  ctx.stroke();
  // wrap lines
  ctx.strokeStyle = 'rgba(0,0,0,0.25)';
  for (let x = -len + 0.8; x < 0; x += Math.max(0.9, len / 4)) {
    ctx.beginPath();
    ctx.moveTo(x, -halfH);
    ctx.lineTo(x + 0.5, halfH);
    ctx.stroke();
  }
}

function drawBladeShape(ctx, def, bloody = 0) {
  const bl = def.bladeLen;
  const bw = def.bladeW;
  const curve = def.curve || 0;
  const wide = def.wide ? 1.35 : 1;

  ctx.beginPath();
  ctx.moveTo(0.6, -bw * wide);
  ctx.quadraticCurveTo(bl * 0.62, -bw * wide * 0.92 - curve * 0.5, bl * 0.97, -bw * 0.22 - curve);
  ctx.quadraticCurveTo(bl * 1.03, -bw * 0.05, bl * 0.97, bw * 0.1 - curve);
  ctx.quadraticCurveTo(bl * 0.6, bw * wide * 0.86 - curve * 0.5, 0.6, bw * wide * 0.92);
  ctx.closePath();
  const g = ctx.createLinearGradient(0, -bw * wide, 0, bw * wide);
  g.addColorStop(0, STEEL_LIGHT);
  g.addColorStop(0.45, STEEL);
  g.addColorStop(1, STEEL_DARK);
  ctx.fillStyle = g;
  ctx.fill();
  ctx.strokeStyle = 'rgba(20,24,30,0.55)';
  ctx.lineWidth = 0.3;
  ctx.stroke();

  // cutting edge highlight
  ctx.beginPath();
  ctx.moveTo(1.2, bw * wide * 0.55);
  ctx.quadraticCurveTo(bl * 0.62, bw * wide * 0.5 - curve * 0.5, bl * 0.95, -curve);
  ctx.strokeStyle = 'rgba(255,255,255,0.55)';
  ctx.lineWidth = 0.3;
  ctx.stroke();

  // blood film
  if (bloody > 0.02) {
    ctx.save();
    ctx.globalAlpha = Math.min(0.85, bloody);
    ctx.fillStyle = '#7d0a10';
    ctx.beginPath();
    ctx.moveTo(1, -bw * wide * 0.2);
    ctx.quadraticCurveTo(bl * 0.7, -bw * wide * 0.4 - curve * 0.5, bl * 0.96, -curve);
    ctx.quadraticCurveTo(bl * 0.6, bw * wide * 0.7 - curve * 0.5, 1, bw * wide * 0.8);
    ctx.closePath();
    ctx.fill();
    // drips
    for (let i = 0; i < 3; i++) {
      const x = bl * (0.25 + i * 0.24);
      ctx.beginPath();
      ctx.ellipse(x, bw * wide * 0.55 + (i % 2) * 0.6, 0.35, 0.9, 0, 0, TAU);
      ctx.fill();
    }
    ctx.restore();
  }
}

export function drawMelee(ctx, def, bloody = 0) {
  if (def.id === 'fists') {
    ctx.fillStyle = '#c98d63';
    ctx.beginPath();
    ctx.arc(1.6, 0, 2.2, 0, TAU);
    ctx.fill();
    ctx.strokeStyle = '#8c5e3d';
    ctx.lineWidth = 0.4;
    ctx.stroke();
    return;
  }
  if (def.axe) {
    // handle
    roundRect(ctx, -4, -0.95, def.bladeLen + 4.4, 1.9, 0.9);
    ctx.fillStyle = WOOD;
    ctx.fill();
    ctx.strokeStyle = 'rgba(20,14,10,0.5)';
    ctx.lineWidth = 0.3;
    ctx.stroke();
    ctx.fillStyle = WOOD_LIGHT;
    ctx.fillRect(def.bladeLen * 0.2, -0.35, def.bladeLen * 0.55, 0.7);
    // head
    ctx.save();
    ctx.translate(def.bladeLen - 1.2, 0);
    ctx.beginPath();
    ctx.moveTo(-2.4, -1.1);
    ctx.lineTo(1.4, -4.4);
    ctx.quadraticCurveTo(4.6, -3.2, 4.9, 0);
    ctx.quadraticCurveTo(4.6, 3.2, 1.4, 4.4);
    ctx.lineTo(-2.4, 1.1);
    ctx.closePath();
    const g = ctx.createLinearGradient(0, -4.4, 0, 4.4);
    g.addColorStop(0, STEEL_LIGHT);
    g.addColorStop(0.5, STEEL);
    g.addColorStop(1, STEEL_DARK);
    ctx.fillStyle = g;
    ctx.fill();
    ctx.strokeStyle = 'rgba(20,24,30,0.6)';
    ctx.lineWidth = 0.3;
    ctx.stroke();
    if (bloody > 0.05) {
      ctx.globalAlpha = Math.min(0.85, bloody);
      ctx.fillStyle = '#7d0a10';
      ctx.beginPath();
      ctx.ellipse(3.6, 0.4, 1.5, 3.1, 0, 0, TAU);
      ctx.fill();
      ctx.globalAlpha = 1;
    }
    ctx.restore();
    return;
  }

  drawGrip(ctx, def.hilt, def.id === 'katana' ? 1.15 : 1.0, def.id === 'katana' ? '#1e2530' : '#3a2a1e');
  if (def.tsuba) {
    ctx.fillStyle = '#2b2f36';
    ctx.beginPath();
    ctx.ellipse(0.9, 0, 0.9, 3.1, 0, 0, TAU);
    ctx.fill();
    ctx.strokeStyle = '#8a8f96';
    ctx.lineWidth = 0.3;
    ctx.stroke();
  } else if (def.guard) {
    roundRect(ctx, 0.0, -def.bladeW - 1.5, 1.1, def.bladeW * 2 + 3, 0.4);
    ctx.fillStyle = '#4c5158';
    ctx.fill();
  }
  drawBladeShape(ctx, def, bloody);
}

function gunMetal(ctx, y0, y1) {
  const g = ctx.createLinearGradient(0, y0, 0, y1);
  g.addColorStop(0, '#5a616b');
  g.addColorStop(0.4, '#3b4049');
  g.addColorStop(1, '#22252b');
  return g;
}

export function drawGun(ctx, def, flashT = 0, flashScale = 1) {
  const L = def.len;
  ctx.lineJoin = 'round';
  const stroke = (c = 'rgba(0,0,0,0.45)') => { ctx.strokeStyle = c; ctx.lineWidth = 0.3; ctx.stroke(); };

  if (def.id === 'pistol') {
    // slide
    roundRect(ctx, -2.2, -3.6, L + 1.4, 3.4, 1.1);
    ctx.fillStyle = gunMetal(ctx, -3.6, -0.2);
    ctx.fill(); stroke();
    // barrel tip
    roundRect(ctx, L - 1.2, -3.0, 1.8, 2.2, 0.5);
    ctx.fillStyle = '#15171b';
    ctx.fill();
    // frame
    ctx.beginPath();
    ctx.moveTo(-1.2, -0.4);
    ctx.lineTo(L - 2.6, -0.4);
    ctx.lineTo(L - 3.4, 1.2);
    ctx.lineTo(-1.2, 1.2);
    ctx.closePath();
    ctx.fillStyle = POLY;
    ctx.fill();
    // grip
    ctx.beginPath();
    ctx.moveTo(-1.6, -0.4);
    ctx.lineTo(2.6, -0.4);
    ctx.lineTo(1.6, 7.0);
    ctx.lineTo(-2.8, 7.0);
    ctx.closePath();
    ctx.fillStyle = '#31343a';
    ctx.fill(); stroke();
    // trigger guard
    ctx.beginPath();
    ctx.arc(3.4, 2.2, 2.1, -0.2, Math.PI * 0.95);
    ctx.strokeStyle = '#2a2d33';
    ctx.lineWidth = 0.8;
    ctx.stroke();
    // sights
    ctx.fillStyle = '#8d949e';
    ctx.fillRect(L - 1.4, -4.2, 0.9, 0.9);
    ctx.fillRect(-1.6, -4.2, 0.9, 0.9);
  } else if (def.id === 'smg') {
    roundRect(ctx, -4.5, -4.6, L + 2, 4.2, 1.2);
    ctx.fillStyle = gunMetal(ctx, -4.6, -0.4);
    ctx.fill(); stroke();
    roundRect(ctx, L - 2.5, -3.4, 3.4, 2.0, 0.6);
    ctx.fillStyle = '#16181c';
    ctx.fill();
    // stock
    ctx.beginPath();
    ctx.moveTo(-4.2, -3.6);
    ctx.lineTo(-8.5, -1.6);
    ctx.lineTo(-8.5, 0.4);
    ctx.lineTo(-4.2, -0.9);
    ctx.closePath();
    ctx.fillStyle = POLY;
    ctx.fill(); stroke();
    // grip
    roundRect(ctx, -1.6, -0.6, 3.6, 6.2, 1);
    ctx.fillStyle = '#2b2e34';
    ctx.fill(); stroke();
    // mag
    ctx.save();
    ctx.translate(5.2, 1.4);
    ctx.rotate(0.12);
    roundRect(ctx, -1.5, 0, 3.2, 7.2, 0.8);
    ctx.fillStyle = '#25282d';
    ctx.fill(); stroke();
    ctx.restore();
    // rail
    ctx.fillStyle = '#4d535c';
    ctx.fillRect(1.4, -5.1, 6.5, 0.7);
  } else if (def.id === 'shotgun') {
    // stock
    ctx.beginPath();
    ctx.moveTo(-4.5, -3.4);
    ctx.lineTo(-11.5, -1.6);
    ctx.lineTo(-11.5, 2.2);
    ctx.lineTo(-4.5, 1.4);
    ctx.closePath();
    ctx.fillStyle = WOOD;
    ctx.fill(); stroke();
    // receiver
    roundRect(ctx, -4.5, -3.8, 12, 4.0, 1.0);
    ctx.fillStyle = '#33373d';
    ctx.fill(); stroke();
    // barrel
    roundRect(ctx, 7, -3.5, L - 7, 1.9, 0.7);
    ctx.fillStyle = gunMetal(ctx, -3.5, -1.6);
    ctx.fill(); stroke();
    // pump / forend
    roundRect(ctx, 10.5, -1.1, 5.2, 2.6, 0.8);
    ctx.fillStyle = WOOD_LIGHT;
    ctx.fill(); stroke('rgba(0,0,0,0.35)');
    // grip
    roundRect(ctx, -1.4, -0.4, 3.2, 5.4, 0.9);
    ctx.fillStyle = WOOD;
    ctx.fill(); stroke();
    // trigger guard
    ctx.beginPath();
    ctx.arc(3.6, 1.8, 1.8, 0, Math.PI);
    ctx.strokeStyle = '#2a2d33';
    ctx.lineWidth = 0.7;
    ctx.stroke();
  } else if (def.id === 'rifle') {
    // stock
    ctx.beginPath();
    ctx.moveTo(-4.5, -3.6);
    ctx.lineTo(-11.0, -2.2);
    ctx.lineTo(-11.0, 2.6);
    ctx.lineTo(-4.5, 1.6);
    ctx.closePath();
    ctx.fillStyle = WOOD;
    ctx.fill(); stroke();
    // body
    roundRect(ctx, -5, -3.8, 15, 3.8, 0.9);
    ctx.fillStyle = WOOD;
    ctx.fill(); stroke();
    // receiver
    roundRect(ctx, 8, -3.9, 7.5, 3.6, 0.8);
    ctx.fillStyle = '#33373d';
    ctx.fill(); stroke();
    // barrel
    roundRect(ctx, 14, -3.4, L - 14, 1.5, 0.5);
    ctx.fillStyle = gunMetal(ctx, -3.4, -1.9);
    ctx.fill(); stroke();
    // muzzle brake
    roundRect(ctx, L - 2.2, -3.7, 2.6, 2.2, 0.5);
    ctx.fillStyle = '#22262b';
    ctx.fill();
    // scope
    roundRect(ctx, 7.5, -8.2, 9.5, 2.4, 1.1);
    ctx.fillStyle = '#1d2025';
    ctx.fill(); stroke('rgba(0,0,0,0.5)');
    ctx.fillStyle = 'rgba(120,170,190,0.55)';
    ctx.beginPath(); ctx.arc(16.6, -7.0, 1.0, 0, TAU); ctx.fill();
    ctx.fillStyle = '#3a3f47';
    ctx.fillRect(9, -6.0, 1.2, 2.4);
    ctx.fillRect(14.4, -6.0, 1.2, 2.4);
    // magazine
    ctx.save();
    ctx.translate(9.6, 1.2);
    ctx.rotate(0.22);
    roundRect(ctx, -1.5, 0, 3.0, 5.4, 0.7);
    ctx.fillStyle = '#2a2d33';
    ctx.fill(); stroke();
    ctx.restore();
    // grip
    roundRect(ctx, -0.6, -0.4, 3.0, 5.0, 0.8);
    ctx.fillStyle = WOOD;
    ctx.fill(); stroke();
  }

  // muzzle flash
  if (flashT > 0) {
    const mx = def.muzzle.x, my = def.muzzle.y;
    const a = clamp(flashT / 0.055, 0, 1);
    const len = (def.flash || 6) * rand(0.85, 1.15) * (flashScale || 1);
    ctx.save();
    ctx.globalAlpha = a;
    const g = ctx.createRadialGradient(mx, my, 0, mx, my, len * 0.9);
    g.addColorStop(0, 'rgba(255,255,235,1)');
    g.addColorStop(0.35, 'rgba(255,214,130,0.9)');
    g.addColorStop(1, 'rgba(255,150,40,0)');
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(mx, my, len * 0.9, 0, TAU);
    ctx.fill();
    ctx.fillStyle = 'rgba(255,246,214,0.95)';
    ctx.beginPath();
    ctx.moveTo(mx, my - 1.4);
    ctx.lineTo(mx + len * 0.95, my - 0.3);
    ctx.lineTo(mx + len * 0.7, my + 0.1);
    ctx.lineTo(mx + len * 1.0, my + 1.5);
    ctx.lineTo(mx, my + 1.6);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }
}

export function drawWeapon(ctx, def, x, y, angle, scale = 1, flashT = 0, bloody = 0) {
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(angle);
  ctx.scale(scale, scale);
  if (def.kind === 'gun') drawGun(ctx, def, flashT, scale);
  else drawMelee(ctx, def, bloody);
  ctx.restore();
}

/* ------------------------------------------------------------------ */
/*  COMBAT                                                             */
/* ------------------------------------------------------------------ */

export class Combat {
  constructor(game) {
    this.game = game;
    this.bullets = [];
    this.impacts = [];
    this.flashes = [];
    this.hitmarkers = [];
    this.trails = [];
  }

  clear() {
    this.bullets.length = 0;
    this.impacts.length = 0;
    this.flashes.length = 0;
    this.hitmarkers.length = 0;
    this.trails.length = 0;
  }

  muzzleWorld(shooter, def, angle) {
    const hand = shooter.handPos();
    const ca = Math.cos(angle), sa = Math.sin(angle);
    const mx = def.muzzle ? def.muzzle.x : 8;
    const my = def.muzzle ? def.muzzle.y : 0;
    return { x: hand.x + ca * mx - sa * my, y: hand.y + sa * mx + ca * my };
  }

  // Fired by an NPC holding a gun.
  fire(shooter, def, angle) {
    const m = this.muzzleWorld(shooter, def, angle);
    // NPCs are soldiers, not aimbots
    this.fireFromPoint(m.x, m.y, angle, def, { kicker: shooter, extraSpread: 0.02, dmgMul: 0.8 });
    shooter.kick(def);
    this.game.shake(def.shake);
    if (def.casing && shooter) {
      const perp = angle - Math.PI / 2;
      this.game.gore.shell(m.x, m.y,
        Math.cos(perp) * rand(25, 70) + rand(-15, 15),
        Math.sin(perp) * rand(25, 70) - rand(30, 90));
    }
  }

  // Fired by the sandbox tools / off-screen shooter.
  fireFromPoint(x, y, angle, def, opts = {}) {
    const game = this.game;
    const spreadBase = def.spread + (opts.extraSpread || 0);
    const dmgMul = opts.dmgMul || 1;
    for (let i = 0; i < def.pellets; i++) {
      const sp = angle + rand(-spreadBase, spreadBase);
      const speed = def.speed * rand(0.94, 1.06);
      this.bullets.push({
        x, y, px: x, py: y,
        vx: Math.cos(sp) * speed,
        vy: Math.sin(sp) * speed,
        dmg: def.dmg * dmgMul * (def.pellets > 1 ? rand(0.7, 1.2) : 1),
        structMul: def.structMul,
        bleedMul: def.bleedMul,
        pierce: def.pierce || 0,
        tracer: def.tracer || def.pellets > 1,
        life: 2.0,
        trail: [],
      });
    }
    game.gore.dust(x, y, def.pellets > 1 ? 8 : 4, '#9c9a94');
    game.gore.spark(x, y, Math.cos(angle), Math.sin(angle), def.pellets > 1 ? 7 : 3);
    this.flashes.push({ x, y, a: angle, t: 0.06, size: def.flash, def });
    game.audio.shot(def.sound);
    game.shake(def.shake * 0.8);
  }

  bladeWorld(shooter, def, angle, reachMul = 1) {
    const hand = shooter.handPos();
    const hx = hand.x + Math.cos(angle) * def.hilt;
    const hy = hand.y + Math.sin(angle) * def.hilt;
    const len = def.bladeLen * reachMul;
    return { ax: hx, ay: hy, bx: hx + Math.cos(angle) * len, by: hy + Math.sin(angle) * len };
  }

  meleeSweep(shooter, def, prev, cur, swingSpeed, hitSet) {
    const game = this.game;
    const hits = [];
    for (const e of game.humans) {
      if (e === shooter) continue;
      for (const limb of e.limbs) {
        if (limb.severed || limb.parentSevered) continue;
        if (hitSet && hitSet.has(limb.id)) continue;
        const a = limb.a, b = limb.b || limb.a;
        for (let s = 0; s <= 3; s++) {
          const t = s / 3;
          const ax = prev.ax + (cur.ax - prev.ax) * t;
          const ay = prev.ay + (cur.ay - prev.ay) * t;
          const bx = prev.bx + (cur.bx - prev.bx) * t;
          const by = prev.by + (cur.by - prev.by) * t;
          const res = segSegClosest(ax, ay, bx, by, a.x, a.y, b.x, b.y);
          if (res.d <= limb.radius + def.bladeW * 0.7) {
            const power = clamp(1.15 - Math.abs(res.s - 0.55) * 0.8, 0.45, 1.15) * clamp(swingSpeed / 9, 0.5, 1.35);
            const key = limb.id + ':' + Math.round(res.x) + ':' + Math.round(res.y);
            if (hits.some((h) => h.key === key)) continue;
            hits.push({ key, entity: e, limb, x: res.x, y: res.y, power,
              dirx: Math.cos(Math.atan2(by - ay, bx - ax)),
              diry: Math.sin(Math.atan2(by - ay, bx - ax)) });
          }
        }
      }
    }
    for (const h of hits) {
      if (hitSet) hitSet.add(h.limb.id);
      h.entity.applyHit(h.limb, {
        type: def.cut > 0.5 ? 'slash' : 'blunt',
        dmg: def.dmg * h.power,
        struct: def.structDmg * h.power,
        cut: def.cut * h.power,
        bleedMul: def.bleedMul,
        knock: def.knock * h.power,
        at: { x: h.x, y: h.y },
        dir: { x: h.dirx, y: h.diry },
        attacker: shooter,
        weapon: def,
        swingSpeed,
      });
      game.gore.spray(h.x, h.y, h.dirx, h.diry, {
        count: Math.round(4 + 9 * h.power), speed: 90 + 130 * h.power,
        size: 1.5, spread: 0.7, mistChance: 0.35,
      });
      game.hitmarker(h.x, h.y);
      game.audio.cut();
    }
    return hits;
  }

  update(dt, game) {
    for (let i = this.bullets.length - 1; i >= 0; i--) {
      const b = this.bullets[i];
      b.life -= dt;
      if (b.life <= 0) { this.bullets.splice(i, 1); continue; }

      const drag = b.tracer ? 0.1 : 0.05;
      b.vx -= b.vx * drag * dt;
      b.vy -= b.vy * drag * dt;
      b.vy += 115 * dt;

      const dist = Math.hypot(b.vx, b.vy) * dt;
      const steps = Math.max(1, Math.ceil(dist / 4));
      let dead = false;

      for (let s = 0; s < steps && !dead; s++) {
        b.px = b.x; b.py = b.y;
        b.x += (b.vx * dt) / steps;
        b.y += (b.vy * dt) / steps;

        let best = null;
        for (const e of game.humans) {
          for (const limb of e.limbs) {
            if (limb.severed) continue;
            const a = limb.a, c = limb.b || limb.a;
            const res = segSegClosest(b.px, b.py, b.x, b.y, a.x, a.y, c.x, c.y);
            if (res.d <= limb.radius + 0.4) {
              const along = Math.hypot(res.x - b.px, res.y - b.py);
              if (!best || along < best.along) best = { limb, e, along, x: res.x, y: res.y };
            }
          }
        }

        if (best) {
          const hit = best;
          hit.e.applyHit(hit.limb, {
            type: 'bullet',
            dmg: b.dmg,
            struct: b.dmg * b.structMul,
            cut: 0.25,
            bleedMul: b.bleedMul,
            knock: b.dmg * 2.2,
            at: { x: hit.x, y: hit.y },
            dir: { x: b.vx, y: b.vy },
            bullet: true,
          });
          if (b.dmg > 20) game.hitmarker(hit.x, hit.y);
          game.gore.mist(hit.x, hit.y, b.vx, b.vy, 5, 0.7, 1.1);
          game.audio.flesh();
          this.impacts.push({ x: hit.x, y: hit.y, type: 'flesh', t: 0.12, a: Math.atan2(b.vy, b.vx) });
          if (b.pierce > 0) {
            b.pierce--;
            b.dmg *= 0.65;
            b.x += Math.cos(Math.atan2(b.vy, b.vx)) * 2.5;
            b.y += Math.sin(Math.atan2(b.vy, b.vx)) * 2.5;
          } else {
            dead = true;
            break;
          }
        }

        if (!dead && b.y >= GROUND_Y) {
          this.impacts.push({ x: b.x, y: GROUND_Y, type: 'ground', t: 0.12, a: Math.atan2(b.vy, b.vx) });
          game.gore.dust(b.x, GROUND_Y - 1, 5, '#6d6355');
          game.gore.mark(b.x, GROUND_Y + rand(0, 6), '#2b241d', rand(0.6, 1.2));
          if (Math.random() < 0.25) game.gore.splatGround(b.x + rand(-4, 4), GROUND_Y, rand(0.8, 2.0), 0.3);
          game.audio.impact();
          dead = true;
          break;
        }

        if (b.x < -80 || b.x > VIEW_W + 80 || b.y < -120) { dead = true; break; }
      }

      b.trail.push({ x: b.x, y: b.y });
      if (b.trail.length > 3) b.trail.shift();
      if (dead) this.bullets.splice(i, 1);
    }

    for (let i = this.flashes.length - 1; i >= 0; i--) {
      this.flashes[i].t -= dt;
      if (this.flashes[i].t <= 0) this.flashes.splice(i, 1);
    }
    for (let i = this.impacts.length - 1; i >= 0; i--) {
      this.impacts[i].t -= dt;
      if (this.impacts[i].t <= 0) this.impacts.splice(i, 1);
    }
    for (let i = this.hitmarkers.length - 1; i >= 0; i--) {
      this.hitmarkers[i].t -= dt;
      if (this.hitmarkers[i].t <= 0) this.hitmarkers.splice(i, 1);
    }
  }

  drawUnder(ctx) {
    for (const b of this.bullets) {
      const speed = Math.hypot(b.vx, b.vy);
      const len = clamp(speed * 0.014, 5, 34);
      const a = Math.atan2(b.vy, b.vx);
      const x2 = b.x - Math.cos(a) * len, y2 = b.y - Math.sin(a) * len;
      ctx.strokeStyle = b.tracer ? 'rgba(255,206,130,0.85)' : 'rgba(255,240,205,0.5)';
      ctx.lineWidth = b.tracer ? 1.3 : 0.9;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(x2, y2);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
      if (b.tracer) {
        ctx.fillStyle = 'rgba(255,252,225,0.95)';
        ctx.beginPath(); ctx.arc(b.x, b.y, 1.2, 0, TAU); ctx.fill();
      }
    }

    for (const im of this.impacts) {
      const a = clamp(im.t / 0.12, 0, 1);
      ctx.globalAlpha = a * 0.9;
      ctx.fillStyle = im.type === 'flesh' ? '#6d0a0d' : '#5c5346';
      ctx.beginPath();
      ctx.arc(im.x, im.y, 2 + (1 - a) * 5, 0, TAU);
      ctx.fill();
      ctx.globalAlpha = 1;
    }
  }

  drawOver(ctx) {
    for (const h of this.hitmarkers) {
      const a = clamp(h.t / 0.3, 0, 1);
      ctx.globalAlpha = a;
      ctx.strokeStyle = '#ffd9d9';
      ctx.lineWidth = 1.1;
      const s = 3.2;
      ctx.beginPath();
      ctx.moveTo(h.x - s, h.y - s); ctx.lineTo(h.x - 1.2, h.y - 1.2);
      ctx.moveTo(h.x + s, h.y - s); ctx.lineTo(h.x + 1.2, h.y - 1.2);
      ctx.moveTo(h.x - s, h.y + s); ctx.lineTo(h.x - 1.2, h.y + 1.2);
      ctx.moveTo(h.x + s, h.y + s); ctx.lineTo(h.x + 1.2, h.y + 1.2);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
  }
}
