import { GROUND_Y, VIEW_W, TAU, rand, clamp, chance, pick, segSegClosest } from './util.js';

// Palette characters for the little pixel-art sprites.
const PAL = {
  '.': null,
  D: '#1b1c21', // outline
  L: '#4a4f58', // metal
  M: '#717783', // metal highlight
  S: '#9aa1ad', // shine
  W: '#6b4326', // wood
  w: '#8a5a33', // wood light
  G: '#2b2520', // grip
  Y: '#c9a227', // brass
  R: '#7d1f1f', // red accent
  K: '#0f1013',
};

function parseArt(rows) {
  const out = [];
  for (let y = 0; y < rows.length; y++) {
    for (let x = 0; x < rows[y].length; x++) {
      const c = PAL[rows[y][x]];
      if (c) out.push({ x, y, c });
    }
  }
  return out;
}

/* --------------------------------------------------------------------- */
/*  WEAPON CATALOG                                                        */
/* --------------------------------------------------------------------- */

function makeWeapon(def) {
  def.art = def.grid ? parseArt(def.grid) : [];
  def.artW = def.grid ? Math.max(...def.grid.map((r) => r.length)) : 0;
  def.artH = def.grid ? def.grid.length : 0;
  return def;
}

export const WEAPONS = [
  makeWeapon({
    id: 'fists', name: 'Кулаки', kind: 'melee', icon: 'fist',
    dmg: 9, structDmg: 0.012, cut: 0.0, bladeLen: 4, bladeW: 2, hilt: 1.5,
    swingTime: 0.3, bleedMul: 0.35, knock: 70, grip: { x: 1, y: 1 },
    grid: [
      '....',
      '.DD.',
      'DSMD',
      '.DD.',
    ],
  }),
  makeWeapon({
    id: 'knife', name: 'Нож', kind: 'melee', icon: 'knife',
    dmg: 34, structDmg: 0.1, cut: 0.85, bladeLen: 10, bladeW: 1.3, hilt: 3,
    swingTime: 0.28, bleedMul: 1.15, knock: 60, grip: { x: 2, y: 2.5 },
    grid: [
      '.............',
      '..DDDDDDDDSSS',
      '.DWWDMMMMMMSS',
      '.DwWDMMMMMMMS',
      '..DDDDDDDDSSS',
      '.............',
    ],
  }),
  makeWeapon({
    id: 'machete', name: 'Мачете', kind: 'melee', icon: 'knife',
    dmg: 54, structDmg: 0.19, cut: 0.95, bladeLen: 19, bladeW: 2.2, hilt: 4,
    swingTime: 0.36, bleedMul: 1.3, knock: 100, grip: { x: 2, y: 3 },
    grid: [
      '..................',
      '..DDDDDDDDDDDDDSSS',
      '.DWWDMMMMMMMMMMSSS',
      '.DWWDMMMMMMMMMMSSS',
      '.DwWDDDDDDDDDDDSSS',
      '..DD..............',
      '..................',
    ],
  }),
  makeWeapon({
    id: 'katana', name: 'Катана', kind: 'melee', icon: 'knife',
    dmg: 70, structDmg: 0.27, cut: 1.0, bladeLen: 25, bladeW: 1.5, hilt: 5,
    swingTime: 0.34, bleedMul: 1.45, knock: 95, grip: { x: 3, y: 2 },
    grid: [
      '............................',
      '..DDDDDDDDDDDDDDDDDDDDDDDSSS',
      '.DKKKDSSSSSSSSSSSSSSSSSSSSSS',
      '.DKKKDSSSSSSSSSSSSSSSSSSSSSS',
      '..DDDDDDDDDDDDDDDDDDDDDDDSSS',
      '............................',
    ],
  }),
  makeWeapon({
    id: 'axe', name: 'Топор', kind: 'melee', icon: 'axe',
    dmg: 78, structDmg: 0.46, cut: 0.25, bladeLen: 15, bladeW: 4, hilt: 4,
    swingTime: 0.46, bleedMul: 0.8, knock: 200, grip: { x: 2.5, y: 5.5 },
    grid: [
      '..................',
      '..................',
      '.......DDDDDDDSS..',
      '......DMMMMMMMMS..',
      '..DDDDDMMMMMMMMS..',
      '.DWWWWDMMMMMMMMS..',
      '.DWwWWDDDDDDDDSS..',
      '.DWWWWD...........',
      '..DDDDD...........',
      '..................',
    ],
  }),
  makeWeapon({
    id: 'pistol', name: 'Пистолет', kind: 'gun', icon: 'pistol',
    ammo: '9mm', magSize: 12, rpm: 330, auto: false, dmg: 24, pellets: 1,
    spread: 0.022, movingSpread: 0.055, recoil: 0.055, shake: 3.2,
    speed: 1750, reloadTime: 1.55, casing: true, tracer: false, pierce: 0,
    flash: 6, sound: 'pistol', structMul: 0.055, bleedMul: 1.2,
    grid: [
      '............',
      '..DDDDDDDD..',
      '.DSSSSSSSSD.',
      '.DMMMMMMMLD.',
      '.DLLLLLLLLD.',
      '..DDDDDDDD..',
      '..GGG..DD...',
      '.DGGGD.DD...',
      '.DGGGD.....',
      '.DGGGD.....',
      '..DDD......',
    ],
    grip: { x: 3.5, y: 5 },
    muzzle: { x: 11, y: 2.5 },
  }),
  makeWeapon({
    id: 'smg', name: 'ПП-19 «Вихрь»', kind: 'gun', icon: 'smg',
    ammo: '9mm', magSize: 30, rpm: 760, auto: true, dmg: 18, pellets: 1,
    spread: 0.03, movingSpread: 0.075, recoil: 0.032, shake: 2.2,
    speed: 1900, reloadTime: 1.9, casing: true, tracer: true, pierce: 0,
    flash: 6, sound: 'smg', structMul: 0.045, bleedMul: 1.1,
    grid: [
      '..............',
      '.DDDDDDDDDDD..',
      '.DSSSSSSSSSSD.',
      '.DMMMMMMMLLLLD',
      '.DLLLLLLLDDDD.',
      '..DDDDDDD.....',
      '..DGGGGD..YY..',
      '.DGGGGGGD.YY..',
      '.DGKKKGGD.....',
      '.DGYGGGD......',
      '..DGGGD.......',
      '...DDD........',
    ],
    grip: { x: 5, y: 6 },
    muzzle: { x: 13.5, y: 2.5 },
  }),
  makeWeapon({
    id: 'shotgun', name: 'Дробовик', kind: 'gun', icon: 'shotgun',
    ammo: '12g', magSize: 6, rpm: 78, auto: false, dmg: 16, pellets: 9,
    spread: 0.15, movingSpread: 0.1, recoil: 0.16, shake: 7,
    speed: 1550, reloadTime: 2.6, casing: true, tracer: false, pierce: 0,
    flash: 10, sound: 'shotgun', structMul: 0.05, bleedMul: 1.4, shellByShell: true,
    grid: [
      '....................',
      '.DDDDDDDDDDDDDDDD...',
      '.DWWWWWWWWWWWWWWD...',
      '.DSSSSSSSSSSSSSSD...',
      '.DWWWWWWWWWWWWWWD...',
      '..DDDDDDDDDDDDDD....',
      '...DWWWWWD....YYY...',
      '...DWWWWWD...YYYY...',
      '...DWwWWWD..DYYYD...',
      '...DWWWWWD..DDDD....',
      '....DDDDD...........',
    ],
    grip: { x: 8, y: 7 },
    muzzle: { x: 19, y: 2.8 },
  }),
  makeWeapon({
    id: 'rifle', name: 'СВД', kind: 'gun', icon: 'rifle',
    ammo: '762', magSize: 10, rpm: 55, auto: false, dmg: 72, pellets: 1,
    spread: 0.006, movingSpread: 0.16, recoil: 0.13, shake: 6.5,
    speed: 2900, reloadTime: 2.9, casing: true, tracer: true, pierce: 1,
    flash: 9, sound: 'rifle', structMul: 0.17, bleedMul: 1.7,
    grid: [
      '..........................',
      '...DDDDDDDDDDDDDDDDDDDD...',
      '..DWWWWWWWWWWWWWWWWWWWWD..',
      '..DSSSSSSSSSSSSSSSSSSSSD..',
      '...DDDDDDDDDDDDSDDDDDDDD..',
      '......DWWWWD...SDDD.......',
      '..YY..DWwWWD..DSD.........',
      '.DYYD.DWWWWD.DSD..........',
      '.DYYD.DWWWWDDSD...........',
      '..YY..DDDDDDDD............',
      '......................',
    ],
    grip: { x: 8.5, y: 7 },
    muzzle: { x: 25.5, y: 2.5 },
  }),
];

export const WEAPON_BY_ID = Object.fromEntries(WEAPONS.map((w) => [w.id, w]));

// Order shown in the bottom hotbar.
export const BAR = ['knife', 'machete', 'axe', 'katana', 'pistol', 'smg', 'shotgun', 'rifle'];

/* --------------------------------------------------------------------- */
/*  DRAWING                                                               */
/* --------------------------------------------------------------------- */

export function drawWeapon(ctx, def, x, y, angle, scale = 1, flashTimer = 0) {
  ctx.save();
  ctx.translate(Math.round(x), Math.round(y));
  ctx.rotate(angle);
  if (def.kind === 'gun') {
    ctx.translate(-(def.grip ? def.grip.x : 2) * scale, -(def.grip ? def.grip.y : 2) * scale);
    const px = 1 / scale;
    for (const p of def.art) {
      ctx.fillStyle = p.c;
      ctx.fillRect(p.x * scale, p.y * scale, scale + 0.3, scale + 0.3);
    }
    // Muzzle flash
    if (flashTimer > 0) {
      const gx = def.grip ? def.grip.x : 2, gy = def.grip ? def.grip.y : 2;
      const mx = (def.muzzle.x - gx + 0.6) * scale, my = (def.muzzle.y - gy) * scale;
      const a = clamp(flashTimer / 0.055, 0, 1);
      ctx.globalAlpha = a;
      ctx.fillStyle = '#ffe9a8';
      ctx.beginPath();
      const len = def.flash * rand(0.85, 1.15);
      ctx.moveTo(mx, my - 1.6); ctx.lineTo(mx + len, my - 0.4);
      ctx.lineTo(mx + len * 0.75, my + 0.2); ctx.lineTo(mx + len, my + 1.8);
      ctx.lineTo(mx, my + 1.8); ctx.closePath(); ctx.fill();
      ctx.fillStyle = '#fff8dd';
      ctx.fillRect(mx, my - 1, len * 0.45, 2.5);
      ctx.globalAlpha = 1;
    }
  } else {
    // Melee art is laid out horizontally with the handle on the left.
    if (def.art.length) {
      for (const p of def.art) {
        ctx.fillStyle = p.c;
        ctx.fillRect(p.x * scale, p.y * scale, scale + 0.3, scale + 0.3);
      }
    }
    if (def.id === 'fists') {
      ctx.fillStyle = '#c78e6a';
      ctx.fillRect(-1, -2, 4, 4);
    }
  }
  ctx.restore();
}

/* --------------------------------------------------------------------- */
/*  COMBAT / PROJECTILES                                                  */
/* --------------------------------------------------------------------- */

export class Combat {
  constructor(game) {
    this.game = game;
    this.bullets = [];
    this.impacts = [];
    this.flashes = [];
    this.hitmarkers = []; // world-space little X marks
  }

  clear() {
    this.bullets.length = 0;
    this.impacts.length = 0;
    this.flashes.length = 0;
    this.hitmarkers.length = 0;
  }

  muzzleWorld(shooter, def, angle) {
    const mx = def.muzzle ? def.muzzle.x : 8;
    const my = def.muzzle ? def.muzzle.y : 0;
    const gripX = def.grip ? def.grip.x : 2;
    const gripY = def.grip ? def.grip.y : 2;
    // Grip is placed at the shooter's hand.
    const hand = shooter.handPos(angle, true);
    const ca = Math.cos(angle), sa = Math.sin(angle);
    const lx = mx - gripX, ly = my - gripY;
    return {
      x: hand.x + ca * lx - sa * ly,
      y: hand.y + sa * lx + ca * ly,
      hand,
    };
  }

  fire(shooter, def, angle) {
    const game = this.game;
    const m = this.muzzleWorld(shooter, def, angle);

    const spreadBase = def.spread + (shooter.extraSpread || 0);
    for (let i = 0; i < def.pellets; i++) {
      const spread = angle + rand(-spreadBase, spreadBase) + (shooter.aimWobble || 0);
      const sp = def.speed * rand(0.94, 1.06);
      this.bullets.push({
        x: m.x, y: m.y, px: m.x, py: m.y,
        vx: Math.cos(spread) * sp,
        vy: Math.sin(spread) * sp,
        dmg: def.dmg * (def.pellets > 1 ? rand(0.7, 1.2) : 1),
        structMul: def.structMul, bleedMul: def.bleedMul,
        pierce: def.pierce || 0,
        tracer: def.tracer || def.pellets > 1,
        life: 1.6,
        trail: [],
      });
    }
    // Muzzle smoke + sparks
    const ca = Math.cos(angle), sa = Math.sin(angle);
    game.gore.dust(m.x, m.y, def.pellets > 1 ? 8 : 4, '#9c9a94');
    game.gore.spark(m.x, m.y, ca, sa, def.pellets > 1 ? 7 : 3);
    this.flashes.push({ x: m.x, y: m.y, a: angle, t: 0.055, size: def.flash, muzzle: m, def });
    if (def.casing && game.player === shooter) {
      game.audio.casing();
    }
    if (def.shellByShell) {
      game.audio.noise({ dur: 0.12, gain: 0.14, freq: 800, freqEnd: 200, delay: 0.12 });
    }

    shooter.kick(def);
    game.shake(def.shake);
    game.audio.shot(def.sound);

    // Shell ejection
    const perpA = angle - Math.PI / 2;
    game.gore.shell(
      m.x - ca * 4, m.y - sa * 4,
      Math.cos(perpA) * rand(25, 70) + rand(-15, 15),
      Math.sin(perpA) * rand(25, 70) - rand(30, 90)
    );
  }

  /* --- melee ---------------------------------------------------------- */

  bladeWorld(shooter, def, angle, reachMul = 1) {
    const hand = shooter.handPos(angle, false);
    const hx = hand.x + Math.cos(angle) * def.hilt;
    const hy = hand.y + Math.sin(angle) * def.hilt;
    const len = def.bladeLen * reachMul;
    return {
      ax: hx, ay: hy,
      bx: hx + Math.cos(angle) * len,
      by: hy + Math.sin(angle) * len,
    };
  }

  // Swept blade test between two blade positions. Returns list of hits.
  meleeSweep(shooter, def, prev, cur, swingSpeed, hitSet) {
    const game = this.game;
    const hits = [];
    for (const e of game.entities) {
      if (e === shooter) continue;
      for (const limb of e.limbs) {
        if (limb.severed || limb.parentSevered) continue;
        if (hitSet && hitSet.has(limb.id)) continue;
        const a = limb.a, b = limb.b || limb.a;
        // sample 3 interpolated blade positions to avoid tunnelling
        for (let s = 0; s <= 3; s++) {
          const t = s / 3;
          const ax = prev.ax + (cur.ax - prev.ax) * t;
          const ay = prev.ay + (cur.ay - prev.ay) * t;
          const bx = prev.bx + (cur.bx - prev.bx) * t;
          const by = prev.by + (cur.by - prev.by) * t;
          const res = segSegClosest(ax, ay, bx, by, a.x, a.y, b.x, b.y);
          if (res.d <= limb.radius + def.bladeW * 0.7) {
            const angleOffset = Math.abs(res.s - 0.55);
            const power = clamp(1.15 - angleOffset * 0.8, 0.45, 1.15) * clamp(swingSpeed / 9, 0.5, 1.35);
            const key = limb.id + ':' + Math.round(res.x) + ':' + Math.round(res.y);
            if (hits.some((h) => h.key === key)) continue;
            if (hitSet) hitSet.add(limb.id);
            hits.push({
              key, entity: e, limb,
              x: res.x, y: res.y,
              power,
              bladeAngle: Math.atan2(by - ay, bx - ax),
              dirx: Math.cos(Math.atan2(by - ay, bx - ax)),
              diry: Math.sin(Math.atan2(by - ay, bx - ax)),
            });
          }
        }
      }
    }
    for (const h of hits) {
      const dmg = def.dmg * h.power;
      const struct = def.structDmg * h.power;
      h.entity.applyHit(h.limb, {
        type: def.cut > 0.5 ? 'slash' : 'blunt',
        dmg, struct,
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
        size: 1.7, spread: 0.7, mistChance: 0.35,
      });
      game.hitmarker(h.x, h.y);
      game.audio.cut();
    }
    return hits;
  }

  /* --- projectile update ---------------------------------------------- */

  update(dt, game) {
    for (let i = this.bullets.length - 1; i >= 0; i--) {
      const b = this.bullets[i];
      b.life -= dt;
      if (b.life <= 0) { this.bullets.splice(i, 1); continue; }

      // Air drag + drop (very light for bullets, heavy for pellets).
      const drag = b.tracer ? 0.1 : 0.05;
      b.vx -= b.vx * drag * dt;
      b.vy -= b.vy * drag * dt;
      b.vy += 115 * dt;

      const dist = Math.hypot(b.vx, b.vy) * dt;
      const steps = Math.max(1, Math.ceil(dist / 5));
      let dead = false;

      for (let s = 0; s < steps && !dead; s++) {
        b.px = b.x; b.py = b.y;
        b.x += (b.vx * dt) / steps;
        b.y += (b.vy * dt) / steps;

        // near-miss whizz for the player
        const pl = game.player;
        if (pl.alive) {
          const d = Math.hypot(pl.pts.chest.x - b.x, pl.pts.chest.y - b.y);
          if (d < 16 && !b.whizzed) { b.whizzed = true; game.audio.wizz(); }
        }

        // Entities first
        let best = null;
        for (const e of game.entities) {
          for (const limb of e.limbs) {
            if (limb.severed) continue;
            const a = limb.a, c = limb.b || limb.a;
            const res = segSegClosest(b.px, b.py, b.x, b.y, a.x, a.y, c.x, c.y);
            if (res.d <= limb.radius + 0.4) {
              // distance along the bullet path
              const along = Math.hypot(res.x - b.px, res.y - b.py);
              if (!best || along < best.along) best = { limb, e, along, x: res.x, y: res.y };
            }
          }
        }

        if (best) {
          const hit = best;
          if (hit.e.alive || hit.limb.mass) {
            hit.e.applyHit(hit.limb, {
              type: 'bullet',
              dmg: b.dmg,
              struct: b.dmg * b.structMul,
              cut: 0.25,
              bleedMul: b.bleedMul,
              knock: b.dmg * 2.2,
              at: { x: hit.x, y: hit.y },
              dir: { x: b.vx, y: b.vy },
              attacker: game.player,
              bullet: true,
            });
            if (hit.e.alive && b.dmg > 20) game.hitmarker(hit.x, hit.y);
            game.gore.mist(hit.x, hit.y, b.vx, b.vy, 5, 0.7, 1.1);
            game.audio.flesh();
          } else {
            game.gore.mist(hit.x, hit.y, b.vx, b.vy, 3, 0.8, 0.8);
          }
          this.spawnImpact(hit.x, hit.y, 'flesh', b);
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

        // Ground
        if (!dead && b.y >= GROUND_Y) {
          this.spawnImpact(b.x, GROUND_Y, 'ground', b);
          game.gore.dust(b.x, GROUND_Y - 1, 5, '#6d6355');
          game.gore.mark(b.x, GROUND_Y + rand(0, 6), '#2b241d', rand(0.7, 1.4));
          if (Math.random() < 0.25) game.gore.splatGround(b.x + rand(-4, 4), GROUND_Y, rand(1, 2.4), 0.3);
          game.audio.impact();
          dead = true;
          break;
        }

        if (b.x < -30 || b.x > VIEW_W + 30 || b.y < -80) { dead = true; break; }
      }

      // trail history (for tracers)
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

  spawnImpact(x, y, type, b) {
    this.impacts.push({
      x, y, type, t: 0.12,
      a: Math.atan2(b.vy, b.vx),
    });
  }

  drawUnder(ctx) {
    // tracers and bullets
    for (const b of this.bullets) {
      const speed = Math.hypot(b.vx, b.vy);
      const len = clamp(speed * 0.012, 4, 26);
      const a = Math.atan2(b.vy, b.vx);
      const x2 = b.x - Math.cos(a) * len, y2 = b.y - Math.sin(a) * len;
      ctx.strokeStyle = b.tracer ? 'rgba(255,214,140,0.85)' : 'rgba(255,240,200,0.55)';
      ctx.lineWidth = b.tracer ? 1.4 : 1;
      ctx.beginPath();
      ctx.moveTo(x2 | 0, y2 | 0);
      ctx.lineTo(b.x | 0, b.y | 0);
      ctx.stroke();
      if (b.tracer) {
        ctx.fillStyle = 'rgba(255,255,220,0.9)';
        ctx.fillRect((b.x - 0.6) | 0, (b.y - 0.6) | 0, 2, 2);
      }
    }
    // muzzle flashes
    for (const f of this.flashes) {
      const a = clamp(f.t / 0.055, 0, 1);
      ctx.globalAlpha = a;
      ctx.fillStyle = '#ffe9a8';
      const cx = f.muzzle.x, cy = f.muzzle.y;
      const ca = Math.cos(f.a), sa = Math.sin(f.a);
      const size = f.size || 6;
      ctx.beginPath();
      ctx.moveTo(cx, cy - 2);
      ctx.lineTo(cx + ca * size * 1.6, cy + sa * size * 1.6 - 1);
      ctx.lineTo(cx + ca * size * 0.8, cy + sa * size * 0.8 + 3);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      ctx.fillRect(cx - 1, cy - 1, 3, 3);
      ctx.globalAlpha = 1;
    }
    // impact puffs
    for (const im of this.impacts) {
      const a = clamp(im.t / 0.12, 0, 1);
      ctx.globalAlpha = a;
      ctx.fillStyle = im.type === 'flesh' ? '#6d0a0d' : '#5c5346';
      ctx.beginPath();
      ctx.arc(im.x, im.y, 3 + (1 - a) * 5, 0, TAU);
      ctx.fill();
      ctx.globalAlpha = 1;
    }
  }

  drawOver(ctx) {
    for (const h of this.hitmarkers) {
      const a = clamp(h.t / 0.3, 0, 1);
      ctx.globalAlpha = a;
      ctx.strokeStyle = '#ffd7d7';
      ctx.lineWidth = 1;
      const s = 3;
      ctx.beginPath();
      ctx.moveTo(h.x - s, h.y - s); ctx.lineTo(h.x - 1, h.y - 1);
      ctx.moveTo(h.x + s, h.y - s); ctx.lineTo(h.x + 1, h.y - 1);
      ctx.moveTo(h.x - s, h.y + s); ctx.lineTo(h.x - 1, h.y + 1);
      ctx.moveTo(h.x + s, h.y + s); ctx.lineTo(h.x + 1, h.y + 1);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
  }
}
