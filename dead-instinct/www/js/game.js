'use strict';
/* =====================================================================
   DEAD INSTINCT — ядро: состояние, физика, инстинкты, зомби, звук, доверие
   ===================================================================== */

const NOISE_UNIT = 2.2;          // 1 = шёпот; 600 = выстрел пистолета
const NOISE = {
  walk: 2.0, sneak: 0.45, run: 14.0, search: 3.0, breakDoor: 9.0, gun9: 34, gun12: 40, gun762: 48,
  flare: 30, bang: 22, alarm: 55, generator: 12, scream: 26, swarm: 18
};

function newGame(chapter, opts) {
  opts = opts || {};
  const cfg = chapterConfig(chapter);
  const seed = opts.seed || ((Math.random() * 1e9) >>> 0);
  const M = genCity(cfg.map, seed, chapter);
  M.timeOfDay = cfg.night; M.weather = cfg.weather;

  const G = {
    ver: GAME_VERSION,
    chapter, seed,
    M,
    player: {
      x: M.safehouse.x + 0.5, y: M.safehouse.y + 0.2, vx: 0, vy: 0, dir: 0,
      hp: 100, maxHp: 100, stamina: 100, maxStamina: 100,
      hunger: 22, thirst: 18, infect: 0, temp: 0,
      noise: 0, stance: 'walk', sprint: false, hidden: false, frozenTime: 0,
      dying: 0, dead: false, light: true, battery: 3.0, torch: false,
      weapon: 'bar', gunAmmo: { ammo9: 0, ammo12: 0, ammo762: 0 }, cooldown: 0, reload: 0,
      bleed: 0, pain: 0, aim: { x: 0, y: 0 }, lastSafe: 0
    },
    inventory: { medkit: 1, bandage: 2, pills: 0, food: 1, water: 1, battery: 1, fuel: 0, flare: 1, bar: 1, bat: 0, axe: 0, knife: 0, pistol: 0, shotgun: 0, rifle: 0, ammo9: 0, ammo12: 0, ammo762: 0 },
    group: [],
    zombies: [],
    zombiesKilled: 0,
    corpses: [],
    bullets: [],
    particles: [],
    floaters: [],
    markers: [],
    trust: 62,
    flags: {},
    stats: { killed: 0, shots: 0, looted: 0, saved: 0, died: 0, quietKills: 0, hours: 0, decisions: [] },
    chapterName: CHAPTERS[chapter - 1].n,
    spawnEvery: cfg.spawnEvery || 22,
    chapterIntro: CHAPTERS[chapter - 1].intro,
    time: 0,
    clock: (chapter === 1 ? 17 : chapter === 2 ? 21 : chapter === 3 ? 3 : chapter === 4 ? 1 : 4) * 60 + 40,
    objectives: objectivesFor(chapter, M),
    intro: DIA['ch' + chapter + '_intro'] || [],
    introIndex: 0,
    over: null,
    events: [],
    hints: [],
    radio: false,
    safehouseId: M.safehouse.room,
    lastPathAge: 0,
    weather: { rain: cfg.weather === 'rain' || cfg.weather === 'storm' ? 0.9 : 0.2, wind: 0.4, fog: cfg.weather === 'fog' ? 0.9 : cfg.weather === 'storm' ? 0.55 : 0.25 },
    instinctFlash: 0,
    dangerFlash: 0,
    lastSfx: new Map(),
    rng: mulberry32(seed + 7),
    autoSave: 0,
    discovered: {},
    banner: null,
    dialog: null,
    choice: null,
    paused: false,
    settings: { sound: true, shake: true, blood: true, hints: true }
  };

  // группа
  for (const id of groupFor(chapter)) {
    const d = NPCS.find(n => n.id === id); if (!d) continue;
    G.group.push({
      id: d.id, n: d.n, role: d.role, d: d.d, sk: Object.assign({ melee: 0, weapon: 0, med: 0, stealth: 0, craft: 0, gut: 0 }, d.sk),
      hp: d.hp, maxHp: d.hp, x: G.player.x, y: G.player.y, target: null, path: [], pathI: 0,
      dead: false, infected: false, bleed: 0, state: 'follow', trust: 0, col: d.col, hair: d.hair, sex: d.sex,
      cd: 0, lastSeen: 0, say: 0, sayText: '', sayT: 0, wanderT: 0, look: 0, anim: 0
    });
  }
  G.flags.yanWounded = chapter >= 1;
  G.night = G.clock < 6 * 60 || G.clock > 20 * 60;
  if (G.night && G.weather.fog > 0.5) G.player.battery = 3;

  // стартовый лут: немного в рюкзак
  if (chapter >= 2) { G.inventory.medkit += 1; G.inventory.bandage += 2; G.inventory.pistol += 1; G.inventory.ammo9 += 8; G.inventory.flare += 1; G.player.gunAmmo.ammo9 = 9; }
  if (chapter >= 3) { G.inventory.fuel = 1; G.inventory.pills = 2; G.inventory.ammo9 += 10; }
  if (chapter >= 4) { G.inventory.shotgun = 1; G.inventory.ammo12 = 8; G.player.gunAmmo.ammo12 = 5; G.inventory.medkit += 1; }
  if (chapter >= 5) { G.inventory.rifle = 1; G.inventory.ammo762 = 10; G.player.gunAmmo.ammo762 = 5; G.inventory.medkit += 1; G.inventory.flare += 1; }

  // лут по карте: раскладываем зомби и предметы
  const rnd = G.rng;
  const freeSpots = [];
  for (let y = 2; y < M.H - 2; y++) for (let x = 2; x < M.W - 2; x++) {
    if (!walkable(M, x, y)) continue;
    if (Math.abs(x - G.player.x) < 6 && Math.abs(y - G.player.y) < 6) continue;
    freeSpots.push([x, y]);
  }
  shuffle(freeSpots, rnd);
  let si = 0, zi = 0;
  // предметы на земле
  for (let i = 0; i < 24 + chapter * 5 && si < freeSpots.length; i++) {
    const k = pickWeighted(rnd, LOOT_TABLE);
    const p = freeSpots[si++];
    M.containers.push({ id: M.containers.length, x: p[0], y: p[1], kind: 'floor', room: roomAt(M, p[0], p[1]), loot: [{ k, n: ['food', 'water', 'bandage', 'ammo9'].includes(k) ? 1 + (rnd() * 2 | 0) : 1 }], looted: 0, onFloor: true });
  }
  // зомби
  spawnZombies(G, cfg.zombies);
  // особые зомби
  if (chapter >= 2) spawnZombieAt(G, 'runner', freeSpots[si++]);
  if (chapter >= 3) { spawnZombieAt(G, 'screamer', freeSpots[si++]); spawnZombieAt(G, 'bloater', freeSpots[si++]); }
  if (chapter >= 4) { spawnZombieAt(G, 'brute', freeSpots[si++]); spawnZombieAt(G, 'runner', freeSpots[si++]); spawnZombieAt(G, 'runner', freeSpots[si++]); }
  if (chapter >= 5) { const b = spawnZombieAt(G, 'brute', freeSpots[si++]); if (b) { b.boss = true; b.hp = b.maxHp = 520; b.xp = 40; } }
  // кейс сюжета
  placeStoryProp(G, chapter);

  // подсказки
  G.hints.push('Не беги: журнал шагов слышен далеко. Иди шагом или крадучись.');
  const tip = [
    'Осмотрись: в комнате есть ящик. Подойди и нажми «ОБЫСКАТЬ».',
    'Аптечки светятся на земле у цели. Их две — но тайников больше.',
    'Канистры лежат у склада. Генератор — в убежище, у южной стены.',
    'У чужих много оружия. Но у них есть и дети.',
    'Журнал — у последней двери. Громила уже ждёт.'
  ][chapter - 1];
  if (tip) G.hints.push(tip);
  return G;
}

function shuffle(a, rnd) { for (let i = a.length - 1; i > 0; i--) { const j = (rnd() * (i + 1)) | 0; const t = a[i]; a[i] = a[j]; a[j] = t; } }

function placeStoryProp(G, ch) {
  const M = G.M, rnd = G.rng;
  M.flags = M.flags || {};
  // свободные точки рядом с целью
  const spotsNear = (px, py, r, n) => {
    const out = [];
    for (let t = 0; t < 700 && out.length < n; t++) {
      const a = rnd() * 6.283, d = 1.2 + rnd() * r;
      const x = px + Math.cos(a) * d | 0, y = py + Math.sin(a) * d | 0;
      if (!walkable(M, x, y)) continue;
      if (out.some(p => p[0] === x && p[1] === y)) continue;
      out.push([x, y]);
    }
    return out;
  };
  const addPickup = (x, y, kind) => {
    M.containers.push({ id: M.containers.length, x, y, kind: 'story', loot: rollLoot(rnd, kind, ch, 0), looted: 0, lootKind: kind });
  };
  const ex = M.exit;
  if (ch === 1) {
    // в квартире — нож/бита и бинты
    const sh = M.safehouse;
    const sp = spotsNear(sh.x, sh.y, 4, 2);
    if (sp[0]) M.containers.push({ id: M.containers.length, x: sp[0][0], y: sp[0][1], kind: 'story', loot: [{ k: 'bat', n: 1 }, { k: 'bandage', n: 2 }] });
    if (sp[1]) M.containers.push({ id: M.containers.length, x: sp[1][0], y: sp[1][1], kind: 'story', loot: [{ k: 'food', n: 1 }, { k: 'water', n: 1 }, { k: 'battery', n: 1 }] });
    M.flags.roomLoot = true;
  }
  if (ch === 2) {
    const sp = spotsNear(ex.x, ex.y, 6, 4);
    addPickup(sp[0][0], sp[0][1], 'medkit');
    addPickup(sp[1][0], sp[1][1], 'medkit');
    if (sp[2]) addPickup(sp[2][0], sp[2][1], 'medkit');
    if (sp[3]) addPickup(sp[3][0], sp[3][1], 'medkit');
  }
  if (ch === 3) {
    const sp = spotsNear(ex.x, ex.y, 7, 4);
    for (let i = 0; i < Math.min(4, sp.length); i++) addPickup(sp[i][0], sp[i][1], 'fuel');
  }
  if (ch === 4) {
    const sp = spotsNear(ex.x, ex.y, 7, 3);
    for (const p of sp) M.containers.push({ id: M.containers.length, x: p[0], y: p[1], kind: 'story', loot: [{ k: 'shotgun', n: 1 }, { k: 'ammo12', n: 4 }] });
  }
  if (ch === 5) {
    const sp = spotsNear(ex.x, ex.y, 8, 2);
    if (sp[0]) addPickup(sp[0][0], sp[0][1], 'journal');
    M.flags.journalAt = sp[0] ? { x: sp[0][0], y: sp[0][1] } : { x: ex.x, y: ex.y };
    if (sp[1]) M.containers.push({ id: M.containers.length, x: sp[1][0], y: sp[1][1], kind: 'story', loot: [{ k: 'medkit', n: 1 }, { k: 'pills', n: 2 }] });
  }
}

function spawnZombies(G, count) {
  const M = G.M, rnd = G.rng;
  const tries = 4000;
  for (let i = 0, made = 0; i < tries && made < count; i++) {
    const x = 3 + Math.floor(rnd() * (M.W - 6)), y = 3 + Math.floor(rnd() * (M.H - 6));
    const d = Math.hypot(x - G.player.x, y - G.player.y);
    if (d < 12) continue;
    if (!walkable(M, x, y, true)) continue;
    const kinds = ['walker', 'walker', 'walker', 'crawler'];
    if (G.chapter >= 2) kinds.push('runner', 'runner');
    if (G.chapter >= 3) kinds.push('screamer');
    if (G.chapter >= 4) kinds.push('bloater');
    if (G.chapter >= 5) kinds.push('brute');
    const k = kinds[(rnd() * kinds.length) | 0];
    spawnZombieAt(G, k, [x, y]);
    made++;
  }
}

function spawnZombieAt(G, kind, pos) {
  if (!pos) return null;
  const M = G.M;
  const z = {
    id: G.zombies.length, kind, x: pos[0] + 0.5, y: pos[1] + 0.5,
    hp: ZK[kind].hp, maxHp: ZK[kind].hp, state: 'idle', dir: Math.random() * 6.28,
    target: null, path: null, pathI: 0, seenT: 0, hearT: 0, lastSeen: null, wander: 0,
    atk: 0, stun: 0, speedMul: 0.9 + Math.random() * 0.2, noiseT: 0, xp: ZK[kind].xp, boss: false,
    anim: Math.random() * 10, investigate: 0
  };
  G.zombies.push(z);
  return z;
}

/* ---------- вспомогательные ---------- */
function playerTile(G) { return { x: Math.floor(G.player.x), y: Math.floor(G.player.y) }; }

function addNoise(G, x, y, amount, source) {
  G.player.noise = Math.max(G.player.noise, amount);
  for (const z of G.zombies) {
    if (z.stun > 0) continue;
    const d = Math.hypot(z.x - x, z.y - y);
    const hear = ZK[z.kind].hear * (amount > 20 ? 2.2 : 1) * (G.M.weather === 'storm' ? 1.15 : 1);
    if (d <= (NOISE_UNIT * amount) / 6 + hear) {
      z.hearT = 5.5;
      z.lastSeen = { x, y };
      z.investigate = 1;
    }
  }
  G.lastNoise = { x, y, amount, t: G.time };
}

function addParticle(G, kind, x, y, opts) {
  opts = opts || {};
  if (G.particles.length > 420) G.particles.shift();
  G.particles.push({
    kind, x, y, vx: opts.vx || 0, vy: opts.vy || 0, life: opts.life || 0.5, max: opts.life || 0.5,
    col: opts.col || '#a33', size: opts.size || 2, grav: opts.grav || 0
  });
}

function floater(G, text, x, y, col) {
  if (G.floaters.length > 40) G.floaters.shift();
  G.floaters.push({ text, x, y, col: col || '#fff', life: 1.1 });
}

function say(F, text, dur) { F.sayText = text; F.sayT = dur || 3.2; }

/* ---------- обновление игрока ---------- */
function updatePlayer(G, dt, inp) {
  const P = G.player, M = G.M;
  if (P.dead) return;
  P.cooldown = Math.max(0, P.cooldown - dt);
  P.reload = Math.max(0, P.reload - dt);
  P.stamina = clamp(P.stamina + (P.sprint && (inp.mx || inp.my) ? -26 : 14) * dt, 0, 100);
  P.hunger = clamp(P.hunger + dt * 0.018, 0, 100);
  P.thirst = clamp(P.thirst + dt * 0.026, 0, 100);
  if (P.hunger > 85 || P.thirst > 85) P.hp -= dt * 0.7;
  if (P.infect > 0) {
    P.infect += dt * (0.012 + P.bleed * 0.05 + (P.hunger > 70 ? 0.04 : 0));
    if (P.infect > 60 && Math.random() < dt * 0.22) P.temp = Math.min(1, P.temp + 0.1);
    if (P.infect >= 100) { P.dead = true; G.over = { kind: 'dead', why: 'Заражение дошло до сердца.' }; }
  } else P.temp = Math.max(0, P.temp - dt * 0.05);
  if (P.bleed > 0) { P.hp -= P.bleed * 4 * dt; if (Math.random() < dt * 1.4) addParticle(G, 'blood', P.x, P.y, { life: 0.35, col: '#7a1a1a' }); }
  P.bleed = Math.max(0, P.bleed - dt * 0.12);
  if (P.hp <= 0 && !P.dead) { P.dead = true; G.over = { kind: 'dead', why: 'Тело не выдержало.' }; }
  if (P.battery <= 0) P.torch = false;
  if (P.torch) P.battery = Math.max(0, P.battery - dt * 0.028);

  // движение
  let mx = inp.mx || 0, my = inp.my || 0;
  const mag = Math.hypot(mx, my);
  if (mag > 1) { mx /= mag; my /= mag; }
  P.sprint = !!inp.sprint && P.stamina > 1 && (inp.mx || inp.my);
  P.stance = inp.sneak ? 'sneak' : P.sprint ? 'sprint' : 'walk';
  const spd = P.stance === 'sprint' ? 3.55 : P.stance === 'sneak' ? 1.35 : 2.15;
  const water = isWater(M, P.x | 0, P.y | 0);
  let nx = P.x + mx * spd * dt * (water ? 0.45 : 1), ny = P.y + my * spd * dt * (water ? 0.45 : 1);
  if (inp.mx || inp.my) { P.dir = Math.atan2(my, mx); P.frozenTime = 0; }
  else P.frozenTime += dt;
  // столкновения
  if (!collides(M, nx, P.y, 0.28)) P.x = nx;
  if (!collides(M, P.x, ny, 0.28)) P.y = ny;
  P.x = clamp(P.x, 1, M.W - 2); P.y = clamp(P.y, 1, M.H - 2);
  P.anim = (P.anim || 0) + dt * (mag ? (P.stance === 'sprint' ? 11 : P.stance === 'sneak' ? 4 : 6.5) : 0);

  // шум
  let noise = 0;
  if (mag) noise = P.stance === 'sprint' ? NOISE.run : P.stance === 'sneak' ? NOISE.sneak : NOISE.walk;
  if (water) noise *= 1.8;
  if (P.torch) noise += 0.4;
  P.noise = Math.max(P.noise * Math.pow(0.02, dt), noise);
  if (noise > 0.4 && Math.random() < dt * 5) {
    const stealth = 1 - 0.15 * (P.skillStealth || 0);
    addNoise(G, P.x, P.y, noise * (P.stance === 'sneak' ? stealth : 1), P.stance);
  }
  // скрытность: стоя в тени/за стеной и не двигаясь
  const inDark = isDark(M, P.x | 0, P.y | 0);
  P.hidden = (P.frozenTime > 1.1 && !P.torch && (inDark || isWater(M, P.x | 0, P.y | 0))) || (P.frozenTime > 2.4 && P.stance === 'sneak');
  if (P.hidden) P.noise = 0;
}

function collides(M, x, y, r) {
  const pts = [[x - r, y - r], [x + r, y - r], [x - r, y + r], [x + r, y + r]];
  for (const p of pts) {
    const tx = p[0] | 0, ty = p[1] | 0;
    if (SOLID[tileAt(M, tx, ty)]) return true;
    if (tileAt(M, tx, ty) === T.DEEP) return true;
    const d = doorAt(M, tx, ty);
    if (d && (d.locked || d.open < 0.6)) return true;
  }
  return false;
}

/* ---------- оружие ---------- */
function currentWeapon(G) {
  const w = G.player.weapon;
  if (!w || !ITEM[w]) return null;
  return ITEM[w];
}

function attack(G, aimX, aimY) {
  const P = G.player;
  if (P.cooldown > 0 || P.dead) return;
  const w = currentWeapon(G);
  const d = Math.hypot(aimX - P.x, aimY - P.y);
  P.dir = Math.atan2(aimY - P.y, aimX - P.x);
  if (!w) {
    // кулаки
    P.cooldown = 0.5;
    if (d <= 1.2) strikeZombie(G, aimX, aimY, 8, 1, 1.2);
    return;
  }
  if (w.gun) {
    // огнестрел
    const ammoKey = w.gun.ammo;
    if (P.reload > 0) return;
    if (P.gunAmmo[ammoKey] <= 0) {
      if (G.inventory[ammoKey] > 0) { reload(G, ammoKey, w); return; }
      floater(G, 'нет патронов', P.x, P.y - 0.8, '#e8a'); say(G.player, 'Пусто. Нужно передёрнуть… нечем.', 2);
      P.cooldown = 0.4; return;
    }
    P.gunAmmo[ammoKey]--;
    G.stats.shots++;
    P.cooldown = w.gun.rate * (1 - 0.08 * (P.skillWeapon || 0));
    const spread = w.gun.range > 10 ? 0.03 : 0.09;
    G.bullets.push({ x: P.x, y: P.y, tx: aimX, ty: aimY, spd: 26, dmg: w.gun.dmg * (1 + 0.1 * (P.skillWeapon || 0)), range: w.gun.range, dist: 0, spread, trail: [] });
    addNoise(G, P.x, P.y, w.gun.noise > 900 ? NOISE.gun762 : w.gun.noise > 700 ? NOISE.gun12 : NOISE.gun9, 'gun');
    addParticle(G, 'flash', P.x, P.y, { life: 0.08, col: '#ffd27a' });
    G.dangerFlash = 0.6;
    return;
  }
  // ближний бой
  const wd = w.weapon || { dmg: 10, spd: 0.5, noise: 2, reach: 1.0 };
  P.cooldown = wd.spd * (1 - 0.06 * (P.skillMelee || 0));
  if (d <= wd.reach + 0.35) {
    strikeZombie(G, aimX, aimY, wd.dmg * (1 + 0.08 * (P.skillMelee || 0)), wd.noise, wd.reach + 0.3);
  } else addParticle(G, 'whoosh', P.x + Math.cos(P.dir) * 0.6, P.y + Math.sin(P.dir) * 0.6, { life: 0.2 });
  addNoise(G, P.x, P.y, wd.noise, 'melee');
}

function reload(G, ammoKey, w) {
  const P = G.player;
  const need = ITEM[w.gun.ammo].stack;
  const have = G.inventory[ammoKey] || 0;
  if (have <= 0) return;
  const take = Math.min(need, have);
  G.inventory[ammoKey] -= take;
  P.gunAmmo[ammoKey] += take;
  P.cooldown = 1.1 * (1 - 0.08 * (P.skillWeapon || 0));
  say(P, 'Перезарядка', 1.2);
  if (G.inventory[ammoKey] <= 0) delete G.inventory[ammoKey];
}

function strikeZombie(G, ax, ay, dmg, noise, reach) {
  const P = G.player;
  let best = null, bd = 9;
  for (const z of G.zombies) {
    if (z.hp <= 0) continue;
    const d = Math.hypot(z.x - P.x, z.y - P.y);
    if (d > reach + 0.2) continue;
    const ad = Math.abs(Math.atan2(z.y - P.y, z.x - P.x) - P.dir);
    const ang = Math.min(ad, Math.PI * 2 - ad);
    if (ang > 1.0) continue;
    if (d < bd) { bd = d; best = z; }
  }
  if (best) {
    hitZombie(G, best, dmg, 'melee');
    addParticle(G, 'blood', best.x, best.y, { life: 0.4, col: '#8a1a1a', vx: (Math.random() - 0.5) * 3, vy: (Math.random() - 0.5) * 3 });
    if (noise < 4) G.stats.quietKills++;
  }
}

function hitZombie(G, z, dmg, how) {
  z.hp -= dmg;
  z.stun = Math.max(z.stun, how === 'melee' ? 0.32 : 0.12);
  if (z.hp <= 0) {
    z.state = 'dead';
    G.stats.killed++;
    G.corpses.push({ x: z.x, y: z.y, kind: z.kind, t: 0, dir: z.dir });
    if (G.corpses.length > 60) G.corpses.shift();
    floater(G, '+' + z.xp + ' опыта', z.x, z.y - 0.6, '#ebc35e');
    if (z.boss) { G.flags.bossKilled = true; G.banner = { text: 'ГРОМИЛА ПОВЕРЖЕН', sub: 'Инстинкт оказался быстрее', t: 4 }; }
    // лут с боссов и иногда с обычных
    if (z.boss || (z.kind === 'bloater' && Math.random() < 0.6)) {
      G.M.containers.push({ id: G.M.containers.length, x: Math.floor(z.x), y: Math.floor(z.y), kind: 'floor', loot: z.boss ? [{ k: 'medkit', n: 1 }, { k: 'ammo12', n: 6 }, { k: 'pills', n: 2 }] : [{ k: 'bandage', n: 2 }], onFloor: true });
    }
  } else if (how === 'melee' && Math.random() < 0.25) z.state = 'stagger';
}

/* стрельба: полёт пуль */
function updateBullets(G, dt) {
  for (let i = G.bullets.length - 1; i >= 0; i--) {
    const b = G.bullets[i];
    const ang = Math.atan2(b.ty - b.y, b.tx - b.x);
    const dx = Math.cos(ang) * b.spd * dt, dy = Math.sin(ang) * b.spd * dt;
    const steps = 3;
    let hit = false;
    for (let s = 0; s < steps && !hit; s++) {
      b.x += dx / steps; b.y += dy / steps; b.dist += Math.hypot(dx, dy) / steps;
      const tx = b.x | 0, ty = b.y | 0;
      if (SOLID[tileAt(G.M, tx, ty)]) { hit = true; break; }
      for (const z of G.zombies) {
        if (z.hp <= 0) continue;
        if (Math.hypot(z.x - b.x, z.y - b.y) < 0.55) {
          hitZombie(G, z, b.dmg * (1 + (Math.random() - 0.5) * 0.2), 'gun');
          addParticle(G, 'blood', z.x, z.y, { life: 0.5, col: '#8a1a1a' });
          hit = true; break;
        }
      }
      if (b.dist > b.range) hit = true;
    }
    if (hit) G.bullets.splice(i, 1);
  }
}

/* ---------- зомби ---------- */
function updateZombies(G, dt) {
  const P = G.player, M = G.M;
  let living = 0;
  for (const z of G.zombies) {
    if (z.hp <= 0) continue;
    living++;
    z.stun = Math.max(0, z.stun - dt);
    z.anim += dt * (z.kind === 'crawler' ? 3 : 4.5) * (z.state === 'chase' ? 1.5 : 1);
    if (z.stun > 0) continue;
    const k = ZK[z.kind];
    // восприятие
    const dP = Math.hypot(z.x - P.x, z.y - P.y);
    const stealthMul = P.hidden ? 0.08 : P.stance === 'sneak' ? 0.45 : P.torch ? 1.6 : 1;
    const sight = k.sight * stealthMul * (isDark(M, P.x | 0, P.y | 0) && !P.torch ? 0.5 : 1) * (G.weather.fog > 0.6 ? 0.75 : 1);
    const seeP = dP < sight && lineOfSight(M, z.x | 0, z.y | 0, P.x | 0, P.y | 0) && !P.dead;
    if (seeP) { z.seenT = 4.5; z.lastSeen = { x: P.x, y: P.y }; z.state = 'chase'; }
    else z.seenT = Math.max(0, z.seenT - dt);
    z.hearT = Math.max(0, z.hearT - dt);
    if (!seeP && z.hearT > 0 && z.lastSeen) { if (z.state !== 'chase' || z.seenT <= 0) z.state = 'investigate'; }

    // цель
    let tx = null, ty = null;
    if (z.seenT > 0 && z.lastSeen) { tx = z.lastSeen.x; ty = z.lastSeen.y; }
    else if (z.hearT > 0 && z.lastSeen) { tx = z.lastSeen.x; ty = z.lastSeen.y; }
    if (tx === null) {
      z.wander -= dt;
      if (z.wander <= 0) {
        z.wander = 2 + Math.random() * 4;
        if (Math.random() < 0.16) z.state = 'idle'; else {
          z.state = 'wander';
          const a = Math.random() * 6.28, r = 3 + Math.random() * 6;
          z.wanderTo = { x: clamp(z.x + Math.cos(a) * r, 1, M.W - 2), y: clamp(z.y + Math.sin(a) * r, 1, M.H - 2) };
        }
      }
      if (z.wanderTo) { tx = z.wanderTo.x; ty = z.wanderTo.y; }
    }

    // атака
    if (dP < 1.15 && !P.dead) {
      z.atk -= dt;
      if (z.atk <= 0) {
        z.atk = 0.85;
        hurtPlayer(G, k.dmg * (0.85 + Math.random() * 0.3), z);
        if (z.kind === 'bloater' || z.boss) { addNoise(G, z.x, z.y, 6, 'attack'); }
      }
      continue;
    }

    // движение по пути
    if (tx !== null) {
      const chasing = z.seenT > 0;
      const spd = k.spd * z.speedMul * (chasing ? 1 : 0.62) * (isWater(M, z.x | 0, z.y | 0) ? 0.5 : 1) * (isSwamp(M, z.x | 0, z.y | 0) ? 0.75 : 1);
      z.pathT = (z.pathT || 0) - dt;
      if (!z.path || z.pathT <= 0) {
        z.pathT = 0.85 + Math.random() * 0.5;
        z.path = findPath(M, z.x | 0, z.y | 0, tx | 0, ty | 0, 700, true);
        z.pathI = 0;
      }
      let moved = false;
      if (z.path && z.pathI < z.path.length) {
        const n = z.path[z.pathI];
        const ddx = n.x + 0.5 - z.x, ddy = n.y + 0.5 - z.y, dl = Math.hypot(ddx, ddy);
        if (dl < 0.22) z.pathI++;
        else {
          let vx = ddx / dl, vy = ddy / dl;
          // расталкивание
          for (const o of G.zombies) {
            if (o === z || o.hp <= 0) continue;
            const od = Math.hypot(o.x - z.x, o.y - z.y);
            if (od < 0.7 && od > 0.001) { vx += (z.x - o.x) / od * 0.35; vy += (z.y - o.y) / od * 0.35; }
          }
          const vn = Math.hypot(vx, vy) || 1;
          const nx2 = z.x + vx / vn * spd * dt, ny2 = z.y + vy / vn * spd * dt;
          if (!zcollides(M, nx2, z.y)) z.x = nx2;
          if (!zcollides(M, z.x, ny2)) z.y = ny2;
          z.dir = Math.atan2(vy, vx);
          moved = true;
        }
      }
      if (!moved && chasing) {
        // прямое преследование, если путь не найден
        const ddx = P.x - z.x, ddy = P.y - z.y, dl = Math.hypot(ddx, ddy) || 1;
        const nx2 = z.x + ddx / dl * spd * dt, ny2 = z.y + ddy / dl * spd * dt;
        if (!zcollides(M, nx2, z.y)) z.x = nx2;
        if (!zcollides(M, z.x, ny2)) z.y = ny2;
        z.dir = Math.atan2(ddy, ddx);
      }
    }
    // блуждающий визг крикуна
    if (z.kind === 'screamer' && z.seenT > 0) {
      z.scream = (z.scream || 0) + dt;
      if (z.scream > 6) { z.scream = 0; addNoise(G, z.x, z.y, NOISE.scream, 'scream'); floater(G, 'КРИК!', z.x, z.y - 0.7, '#e46'); }
    }
  }
  G.zombies = G.zombies.filter(z => z.hp > 0);
  G.livingZombies = living;
  // жёсткий предел: слишком много зомби — самый дальний уходит
  if (G.zombies.length > 70) {
    let far = -1, fd = -1;
    for (let i = 0; i < G.zombies.length; i++) { const d = Math.hypot(G.zombies[i].x - P.x, G.zombies[i].y - P.y); if (d > fd) { fd = d; far = i; } }
    if (far >= 0 && fd > 25) G.zombies.splice(far, 1);
  }
}

function zcollides(M, x, y) {
  const tx = x | 0, ty = y | 0;
  if (SOLID[tileAt(M, tx, ty)]) return true;
  if (tileAt(M, tx, ty) === T.DEEP) return true;
  const d = doorAt(M, tx, ty);
  if (d && (d.locked || d.open < 0.6)) return true;
  return false;
}

function hurtPlayer(G, dmg, src) {
  const P = G.player;
  if (P.dead) return;
  P.hp -= dmg;
  P.bleed = Math.min(1.2, P.bleed + 0.18);
  P.infect = Math.min(100, P.infect + (src && src.kind === 'bloater' ? 6 : 2.2));
  G.dangerFlash = 1;
  addParticle(G, 'blood', P.x, P.y, { life: 0.5, col: '#a11', vx: (Math.random() - 0.5) * 2.4, vy: (Math.random() - 0.5) * 2.4 });
  if (P.hp <= 0) { P.hp = 0; P.dead = true; G.over = { kind: 'dead', why: 'Зубы сомкнулись на твоей руке.' }; }
}

/* ---------- болтающиеся предметы и двери ---------- */
function updateProps(G, dt) {
  const P = G.player;
  for (const d of G.M.doors) {
    d.bang = Math.max(0, d.bang - dt);
    if (!d.broken && d.hp <= 0) { d.broken = true; d.open = 1; d.locked = false; }
  }
  for (const z of G.zombies) {
    if (z.hp <= 0 || z.seenT <= 0) continue;
    // зомби ломают закрытые двери рядом
    for (const d of G.M.doors) {
      if (d.broken || (!d.locked && d.open > 0.5)) continue;
      if (Math.abs(d.x + 0.5 - z.x) < 1.1 && Math.abs(d.y + 0.5 - z.y) < 1.1) {
        d.hp -= 14 * dt; d.bang = 0.4;
        if (Math.random() < dt * 2.2) addNoise(G, d.x, d.y, 3.5, 'door');
      }
    }
  }
  // шум шагов трущоб
}

/* ---------- инстинкты ---------- */
function instinctPulse(G) {
  const P = G.player;
  if (P.instCd > 0) return;
  P.instCd = 1.2;
  G.instinctFlash = 1;
  // инстинктивное чутьё: подсветить следы и живых за стенами
  for (const z of G.zombies) {
    const d = Math.hypot(z.x - P.x, z.y - P.y);
    if (d < 13) {
      z.senseT = 2.5;
      // тени за стенами — только направление
      if (!lineOfSight(G.M, P.x | 0, P.y | 0, z.x | 0, z.y | 0)) {
        G.M.vis[z.x | 0 + (z.y | 0) * G.M.W] = 2;
      }
    }
  }
  // подсветить ближайший угол опасности
  let worst = null, wd = 1e9;
  for (const z of G.zombies) {
    const d = Math.hypot(z.x - P.x, z.y - P.y);
    if (d < 14 && d < wd) { wd = d; worst = z; }
  }
  if (worst) G.flags.dangerDir = Math.atan2(worst.y - P.y, worst.x - P.x);
  addNoise(G, P.x, P.y, 0.6, 'breath');
}

function dangerSense(G, dt) {
  const P = G.player;
  P.dangerCd = Math.max(0, (P.dangerCd || 0) - dt);
  if (G.input && G.input.danger && P.dangerCd <= 0) {
    P.dangerCd = 14;
    G.dangerFlash = 1.4;
    G.dangerSlow = 2.2;                   // замедление времени
    for (const z of G.zombies) {
      const d = Math.hypot(z.x - P.x, z.y - P.y);
      if (d < 18) z.senseT = 3;
    }
    // сортировка угроз: ближайшая
    let near = null, nd = 1e9;
    for (const z of G.zombies) {
      const d = Math.hypot(z.x - P.x, z.y - P.y);
      if (d < nd) { nd = d; near = z; }
    }
    if (near) G.flags.dangerDir = Math.atan2(near.y - P.y, near.x - P.x);
  }
}

function freezeBreath(G, dt) {
  const P = G.player;
  P.freezeT = Math.max(0, (P.freezeT || 0) - dt);
  if (G.input && G.input.freeze && P.freezeT <= 0 && P.stamina > 12 && !P.dead) {
    P.freezeT = 3.2;
    P.stamina -= 14;
    P.frozenTime = 3.2;
    P.hidden = true;
    for (const z of G.zombies) {
      if (Math.hypot(z.x - P.x, z.y - P.y) < 10) { z.seenT = 0; z.hearT = 0; z.lastSeen = null; z.state = 'wander'; z.path = null; }
    }
    floater(G, 'затаился', P.x, P.y - 0.9, '#8bd');
    addNoise(G, P.x, P.y, 0.4, 'breath');
  }
}

function throwBang(G, x, y) {
  const P = G.player;
  if ((G.inventory.flare > 0) && G.input && G.input.flare) {
    G.inventory.flare--; if (G.inventory.flare <= 0) delete G.inventory.flare;
    G.bullets.push({ flare: true, x: P.x, y: P.y, tx: x, ty: y, spd: 12, range: 6, dist: 0, burn: 12 });
    return;
  }
  if (!G.inventory.bang && !G.flags.canBang) {
    // «бросок банки» — расходуется жестянка/вода
    if ((G.inventory.water || 0) > 0) {
      G.inventory.water--; if (G.inventory.water <= 0) delete G.inventory.water;
    } else if ((G.inventory.food || 0) > 0) {
      G.inventory.food--; if (G.inventory.food <= 0) delete G.inventory.food;
    } else { floater(G, 'нечего бросить', P.x, P.y - 0.8, '#e8a'); return; }
  }
  addNoise(G, x, y, NOISE.bang, 'bang');
  addParticle(G, 'bang', x, y, { life: 0.7, col: '#d9c07a', size: 3 });
  floater(G, 'дзынь!', x, y - 0.6, '#e8d27a');
  G.flags.lastBang = { x, y, t: G.time };
}

function updateFlares(G, dt) {
  for (let i = G.bullets.length - 1; i >= 0; i--) {
    const b = G.bullets[i];
    if (b.flare) {
      b.burn -= dt;
      if (b.burn <= 0) {
        // при приземлении: огонь и запах
        addNoise(G, b.tx, b.ty, NOISE.flare, 'flare');
        G.M.lights.push({ x: b.tx, y: b.ty, r: 3.4, col: '#ff6a3a', kind: 'flare', t: 12, flicker: Math.random() });
        for (const z of G.zombies) {
          const d = Math.hypot(z.x - b.tx, z.y - b.ty);
          if (d < 6) { z.state = 'flee'; z.lastSeen = { x: z.x * 2 - b.tx, y: z.y * 2 - b.ty }; z.hearT = 4; }
        }
        G.bullets.splice(i, 1);
        continue;
      }
      const ang = Math.atan2(b.ty - b.y, b.tx - b.x);
      b.x += Math.cos(ang) * b.spd * dt; b.y += Math.sin(ang) * b.spd * dt;
      addParticle(G, 'fire', b.x, b.y, { life: 0.3, col: '#ff9a3a', vx: (Math.random() - 0.5) * 2, vy: (Math.random() - 0.5) * 2 });
    }
  }
}

/* ---------- группа ---------- */
const GROUP_IDEAL = { dist: 2.2, sprint: 4.5 };

function updateGroup(G, dt) {
  const P = G.player;
  for (const F of G.group) {
    if (F.dead) continue;
    F.sayT = Math.max(0, F.sayT - dt);
    F.cd = Math.max(0, F.cd - dt);
    // дистанция до игрока
    const d = Math.hypot(F.x - P.x, F.y - P.y);
    F.anim = (F.anim || 0) + dt * (d > 0.3 ? (P.stance === 'sprint' ? 9 : 5) : 0);
    const follow = P.stance === 'sprint' ? 1.9 : 1.45;
    F.pathT = (F.pathT || 0) - dt;
    if (d > 1.4) {
      if (F.pathT <= 0) {
        F.pathT = 0.5;
        F.path = findPath(G.M, F.x | 0, F.y | 0, P.x | 0, P.y | 0, 1400, false);
        F.pathI = 0;
      }
      // путь к игроку
      moveAlongPath(G, F, dt, follow, 0.28);
    } else if (d < 0.9) {
      // отойти, чтобы не толпиться
      const ang = Math.atan2(F.y - P.y, F.x - P.x);
      const nx = F.x + Math.cos(ang) * dt * 1.2, ny = F.y + Math.sin(ang) * dt * 1.2;
      if (!collides(G.M, nx, F.y, 0.24)) F.x = nx;
      if (!collides(G.M, F.x, ny, 0.24)) F.y = ny;
    }
    // заражение союзника
    if (F.infect > 0 && F.infect < 100) {
      F.infect += dt * 0.008;
      if (F.infect >= 100) { F.dead = true; G.stats.died++; say(F, 'Нет… нет, не так…', 3); G.trust -= 8; }
    }
    if (F.hp <= 0) {
      F.dead = true; G.stats.died++;
      G.trust -= 14;
      floater(G, F.n + ' погиб', F.x, F.y - 0.8, '#e55');
      G.banner = { text: F.n.toUpperCase() + ' ПОГИБ', sub: 'Доверие группы упало', t: 3.4 };
    }
    // боевое поведение: ближайший зомби в 3 тайлах
    if (F.cd <= 0) {
      let best = null, bd = 3.4;
      for (const z of G.zombies) {
        if (z.hp <= 0) continue;
        const dz = Math.hypot(z.x - F.x, z.y - F.y);
        if (dz < bd && lineOfSight(G.M, F.x | 0, F.y | 0, z.x | 0, z.y | 0)) { bd = dz; best = z; }
      }
      if (best) {
        F.attackT = (F.attackT || 0) + dt;
        F.cd = 0.95;
        const meleeBonus = 1 + 0.10 * F.sk.melee;
        // у Яна — винтовка, но стреляет редко и громко
        if (F.id === 'yan' && (F.ammo === undefined)) F.ammo = 6;
        if (F.id === 'yan' && F.ammo > 0 && bd > 1.6 && Math.random() < 0.5) {
          F.ammo--;
          hitZombie(G, best, 70 * (1 + 0.1 * F.sk.weapon), 'gun');
          addNoise(G, F.x, F.y, NOISE.gun762, 'gun');
          addParticle(G, 'flash', F.x, F.y, { life: 0.08, col: '#ffd27a' });
          say(F, 'Тихо! …по возможности.', 2);
        } else {
          hitZombie(G, best, 16 * meleeBonus, 'melee');
          addNoise(G, F.x, F.y, 2.2, 'melee');
        }
        addParticle(G, 'blood', best.x, best.y, { life: 0.4, col: '#8a1a1a' });
      }
    }
    // подбирает лут рядом, когда спокойно
    if (F.cd <= 0 && Math.random() < dt * 0.35) {
      for (const c of G.M.containers) {
        if (c.looted || !c.loot) continue;
        if (Math.hypot(c.x + 0.5 - F.x, c.y + 0.5 - F.y) < 1.1) {
          lootContainer(G, c, F);
          break;
        }
      }
    }
    // лечение игрока
    if ((G.player.hp < G.player.maxHp * 0.55) && F.sk.med >= 2 && F.cd <= 0 && Math.random() < dt * 0.3) {
      F.cd = 4;
      healPlayer(G, 6 + F.sk.med * 2, false);
      say(F, 'Держись. Я рядом.', 2.4);
    }
  }
}

function moveAlongPath(G, F, dt, spd, r) {
  if (!F.path || F.pathI >= F.path.length) return false;
  const n = F.path[F.pathI];
  const dx = n.x + 0.5 - F.x, dy = n.y + 0.5 - F.y, dl = Math.hypot(dx, dy);
  if (dl < 0.24) { F.pathI++; return true; }
  const vx = dx / dl * spd * dt, vy = dy / dl * spd * dt;
  if (!collides(G.M, F.x + vx, F.y, r)) F.x += vx;
  if (!collides(G.M, F.x, F.y + vy, r)) F.y += vy;
  F.dir = Math.atan2(dy, dx);
  return true;
}

/* ---------- лут ---------- */
function lootContainer(G, c, by) {
  if (c.looted || !c.loot) return;
  c.looted = 1;
  G.stats.looted++;
  let names = [];
  for (const it of c.loot) {
    if (!it) continue;
    G.inventory[it.k] = (G.inventory[it.k] || 0) + it.n;
    names.push(ITEM[it.k].n + (it.n > 1 ? ' ×' + it.n : ''));
  }
  if (names.length) floater(G, names.join(', '), c.x + 0.5, c.y - 0.2, '#e8d27a');
  // событие сюжета
  for (const it of c.loot) {
    if (!it) continue;
    if (G.chapter === 2 && it.k === 'medkit') {
      const o = G.objectives.find(o => o.id === 'medkits');
      if (o) { o.got = (o.got || 0) + it.n; o.text = 'Найди аптечки (' + Math.min(o.need, o.got) + '/' + o.need + ')'; if (o.got >= o.need && !o.done) { o.done = true; G.banner = { text: 'АПТЕЧКИ НАЙДЕНЫ', sub: 'Ян доживёт до утра', t: 3.5 }; } }
    }
    if (G.chapter === 3 && it.k === 'fuel') {
      const o = G.objectives.find(o => o.id === 'fuel');
      if (o) { o.got = (o.got || 0) + it.n; o.text = 'Найди топливо (' + Math.min(o.need, o.got) + '/' + o.need + ')'; if (o.got >= o.need) o.done = true; }
    }
    if (G.chapter === 5 && it.k === 'journal' && !G.flags.journalFound) {
      G.flags.journalFound = true;
      const o = G.objectives.find(o => o.id === 'journal');
      if (o) o.done = true;
      G.banner = { text: 'ЖУРНАЛ ИЗОЛЯТОРА', sub: 'Дата за две недели до вспышки', t: 4 };
    }
  }
  save(G);
}

function healPlayer(G, amount, cured) {
  const P = G.player;
  P.hp = clamp(P.hp + amount, 0, P.maxHp);
  if (cured) P.infect = clamp(P.infect - 12, 0, 100);
  P.bleed = Math.max(0, P.bleed - 0.25);
  floater(G, '+' + Math.round(amount) + ' HP', P.x, P.y - 0.8, '#7f7');
}

function useItem(G, key) {
  const inv = G.inventory;
  if (!inv[key] || inv[key] <= 0) { floater(G, 'нет предмета', G.player.x, G.player.y - 0.8, '#e8a'); return; }
  const P = G.player;
  const mult = 1 + 0.25 * (P.skillMed || 0);
  if (key === 'medkit') { inv.medkit--; if (!inv.medkit) delete inv.medkit; healPlayer(G, 45 * mult, true); say(P, 'Так. Дыши. Ты в порядке.', 2); }
  else if (key === 'bandage') { inv.bandage--; if (!inv.bandage) delete inv.bandage; healPlayer(G, 18 * mult, true); }
  else if (key === 'pills') { inv.pills--; if (!inv.pills) delete inv.pills; P.infect = clamp(P.infect - 22, 0, 100); floater(G, 'жар спадает', P.x, P.y - 0.8, '#8cf'); }
  else if (key === 'food') { inv.food--; if (!inv.food) delete inv.food; P.hunger = clamp(P.hunger - 42, 0, 100); floater(G, 'сыт', P.x, P.y - 0.8, '#cbf'); }
  else if (key === 'water') { inv.water--; if (!inv.water) delete inv.water; P.thirst = clamp(P.thirst - 50, 0, 100); floater(G, 'питьё', P.x, P.y - 0.8, '#acd'); }
  else if (key === 'battery') { inv.battery--; if (!inv.battery) delete inv.battery; P.battery = clamp(P.battery + 2.5, 0, 3); floater(G, 'фонарь заряжен', P.x, P.y - 0.8, '#fd2'); }
  else if (key === 'flare') { inv.flare--; if (!inv.flare) delete inv.flare; throwBang(G, P.x + Math.cos(P.dir) * 4, P.y + Math.sin(P.dir) * 4); }
  else if (key === 'fuel') {
    // заправка генератора в убежище
    const gen = G.M.props.find(p => p.kind === 'generator');
    if (gen && Math.hypot(gen.x + 0.5 - P.x, gen.y + 0.5 - P.y) < 2) {
      inv.fuel--; if (!inv.fuel) delete inv.fuel;
      gen.fuel += 1; gen.on = true;
      floater(G, 'генератор заправлен', P.x, P.y - 0.8, '#fd2');
      const o = G.objectives.find(o => o.id === 'gen'); if (o) o.done = true;
      G.flags.generatorOn = true;
    } else floater(G, 'нужно у генератора', P.x, P.y - 0.8, '#e8a');
  }
  // оружие — снаряжение
  else if (ITEM[key] && (ITEM[key].weapon || ITEM[key].gun)) { equip(G, key); return; }
}

function equip(G, key) {
  const inv = G.inventory;
  if (!inv[key] || inv[key] <= 0) return;
  G.player.weapon = key;
  floater(G, ITEM[key].n, G.player.x, G.player.y - 0.8, '#e8d27a');
  if (ITEM[key].gun) {
    const ak = ITEM[key].gun.ammo;
    G.player.gunAmmo[ak] = G.player.gunAmmo[ak] || 0;
    if (G.player.gunAmmo[ak] === 0 && (G.inventory[ak] || 0) > 0) reload(G, ak, ITEM[key]);
  }
}

/* ---------- двери ---------- */
function interact(G, x, y) {
  const M = G.M;
  // дверь
  const d = doorAt(M, x | 0, y | 0);
  if (d) {
    if (d.broken) return;
    d.open = d.open > 0.6 ? 0 : 1;
    d.locked = false;
    addNoise(G, x, y, 1.6, 'door');
    return { type: 'door' };
  }
  // контейнер
  for (const c of M.containers) {
    if (c.looted) continue;
    if (c.x === (x | 0) && c.y === (y | 0)) { lootContainer(G, c); return { type: 'loot' }; }
  }
  // генератор/радио
  for (const p of M.props) {
    if (p.x === (x | 0) && p.y === (y | 0)) {
      if (p.kind === 'generator') {
        if (p.fuel > 0) { p.on = !p.on; floater(G, p.on ? 'генератор включён' : 'генератор выключен', x + 0.5, y, '#fd2'); G.flags.generatorOn = p.on; }
        else floater(G, 'нет топлива', x + 0.5, y, '#e8a');
        return { type: 'prop', p };
      }
      if (p.kind === 'radio') { G.radio = !G.radio; floater(G, G.radio ? 'радио включено' : 'радио выключено', x + 0.5, y, '#8cf'); return { type: 'radio' }; }
    }
  }
  // журнал
  if (G.chapter === 5 && M.flags.journalAt && Math.hypot(x - M.flags.journalAt.x, y - M.flags.journalAt.y) < 1.5 && !G.flags.journalFound) {
    G.flags.journalFound = true;
    const o = G.objectives.find(o => o.id === 'journal'); if (o) o.done = true;
    G.banner = { text: 'ЖУРНАЛ ИЗОЛЯТОРА', sub: 'Дата за две недели до вспышки', t: 4 };
    return { type: 'journal' };
  }
  return null;
}

/* ---------- цели и выход ---------- */
function updateObjectives(G) {
  const P = G.player, M = G.M;
  const sh = M.safehouse;
  const dHome = Math.hypot(P.x - sh.x, P.y - sh.y);
  const dExit = Math.hypot(P.x - M.exit.x, P.y - M.exit.y);
  const o = id => G.objectives.find(q => q.id === id);
  if (G.chapter === 1) {
    if (o('look') && !o('look').done && dHome < 5 && G.flags.looked) o('look').done = true;
    if (o('weapon') && !o('weapon').done && G.flags.weapon) o('weapon').done = true;
    if (o('out') && !o('out').done && dHome > 6) o('out').done = true;
    if (o('gate') && !o('gate').done && dExit < 3) o('gate').done = true;
    if (o('gate') && o('gate').done && o('home')) { o('home').active = true; if (dHome < 3) finishChapter(G); }
  } else if (G.chapter === 2) {
    if (o('pharm') && !o('pharm').done && dExit < 4) { o('pharm').done = true; }
    if (o('medkits') && o('medkits').done && !G.flags.javaChoice && o('choice')) { G.choice = CHOICES.ch2; G.flags.javaChoice = true; }
    if (o('back') && dHome < 3 && o('medkits') && o('medkits').done) { o('back').done = true; finishChapter(G); }
  } else if (G.chapter === 3) {
    if (o('shop') && !o('shop').done && dExit < 4) o('shop').done = true;
    if (o('gen') && G.flags.generatorOn) o('gen').done = true;
    if (o('back') && dHome < 3 && G.flags.generatorOn) { o('back').done = true; finishChapter(G); }
  } else if (G.chapter === 4) {
    if (o('choice') && !G.flags.javaChoice2 && dExit < 6) { G.choice = CHOICES.ch4; G.flags.javaChoice2 = true; }
    if (o('mall') && o('choice') && !G.choice) o('mall').done = true;
    if (o('back') && dHome < 3 && G.flags.javaChoice2) { o('back').done = true; finishChapter(G); }
  } else if (G.chapter === 5) {
    if (o('hosp') && !o('hosp').done && dExit < 5) o('hosp').done = true;
    if (o('journal') && G.flags.journalFound) o('journal').done = true;
    if (o('leave') && dExit < 3 && G.flags.journalFound) { o('leave').done = true; finishChapter(G); }
  }
  // провал цели по шуму
  const quietObj = G.objectives.find(q => q.quiet);
  if (quietObj && !quietObj.done && G.lastNoise && G.lastNoise.amount > 20) {
    quietObj.failed = true; quietObj.name = quietObj;
    quietObj.text = 'Дойди тихо до ворот (провалено)';
    quietObj.quiet = false;
    G.trust -= 3;
  }
}

function finishChapter(G) {
  if (G.over) return;
  const ending = G.chapter >= 5 ? endingFor(G) : null;
  G.over = { kind: 'chapter', ending };
  save(G);
}

/* ---------- события и диалоги ---------- */
function pushDialog(G, lines) {
  if (!lines || !lines.length) return;
  G.dialogQueue = G.dialogQueue || [];
  G.dialogQueue = G.dialogQueue.concat(lines);
}

function drainDialog(G) {
  if (G.dialog) return;
  if (!G.dialogQueue || !G.dialogQueue.length) return;
  G.dialog = G.dialogQueue.shift();
}

function resolveChoice(G, idx) {
  if (!G.choice) return;
  const opt = G.choice.opts[idx];
  if (!opt) return;
  G.choice = null;
  applyChoice(G, opt);
}

function applyChoice(G, opt) {
  G.trust = clamp(G.trust + opt.trust, 0, 100);
  G.flags.goodMan = opt.id === 'share' || opt.id === 'talk' || opt.id === 'sneak';
  if (opt.loot) {
    const rnd = G.rng;
    G.inventory.medkit = (G.inventory.medkit || 0) + 1;
    G.inventory.ammo9 = (G.inventory.ammo9 || 0) + 6;
    G.inventory.food = (G.inventory.food || 0) + 2;
    G.stats.looted += 3;
    floater(G, 'припасы забраны', G.player.x, G.player.y - 1, '#e8d27a');
  }
  if (opt.id === 'kill') {
    G.flags.killedInnocents = true;
    G.trust = clamp(G.trust - 20, 0, 100);
    for (const F of G.group) if (F.id === 'mara' && Math.random() < 0.9) say(F, 'Ты… ты понимаешь, что ты сейчас сделал?', 4);
    G.stats.died++;
  }
  if (opt.id === 'take') G.flags.robbed = true;
  // последствия для сюжета
  if (G.chapter === 4 && opt.id === 'talk') G.flags.ally = true;
}

/* ---------- доверие ---------- */
function trustLevel(G) {
  const t = G.trust;
  if (t >= 80) return { n: 'СЕМЬЯ', col: '#8fd66a' };
  if (t >= 60) return { n: 'ДРУЗЬЯ', col: '#d6c46a' };
  if (t >= 40) return { n: 'ПОПУТЧИКИ', col: '#d6a66a' };
  if (t >= 20) return { n: 'НА ГРАНИ', col: '#d67a5a' };
  return { n: 'ЧУЖИЕ', col: '#d65a5a' };
}

function updateTrust(G, dt) {
  // доверие медленно падает, когда игрок жесток или не помогает
  if (G.trust < 20 && Math.random() < dt * 0.02) {
    // кто-то уходит
    const alive = G.group.filter(n => !n.dead);
    if (alive.length && !G.flags.leaveWarned) {
      G.flags.leaveWarned = true;
      say(alive[0], 'Я пойду один. С тобой мы все не доживём до утра.', 4);
    }
  }
}

/* ---------- свет и время ---------- */
function updateWorld(G, dt) {
  G.time += dt;
  G.clock = (G.clock + dt * 1.2) % 1440;
  G.weather.rain = clamp(G.weather.rain + (Math.random() - 0.5) * dt * 0.05, 0.05, 1);
  G.instinctFlash = Math.max(0, G.instinctFlash - dt * 1.8);
  G.dangerFlash = Math.max(0, G.dangerFlash - dt * 1.4);
  if (G.dangerSlow > 0) G.dangerSlow = Math.max(0, G.dangerSlow - dt);
  // инстинкты-таймеры зомби
  for (const z of G.zombies) if (z.senseT) z.senseT = Math.max(0, z.senseT - dt);
  // частицы
  for (let i = G.particles.length - 1; i >= 0; i--) {
    const p = G.particles[i];
    p.life -= dt;
    if (p.life <= 0) { G.particles.splice(i, 1); continue; }
    p.x += p.vx * dt; p.y += p.vy * dt; p.vy += p.grav * dt;
  }
  for (let i = G.floaters.length - 1; i >= 0; i--) {
    const f = G.floaters[i]; f.life -= dt; f.y -= dt * 0.7;
    if (f.life <= 0) G.floaters.splice(i, 1);
  }
  // трупы
  for (const c of G.corpses) c.t += dt;
  // радио: случайные сообщения
  if (G.radio && !G.flags.radioBanter && G.time > 30) {
    G.flags.radioBanter = true;
    pushDialog(G, [{ who: 'radio', text: '…школа номер семь… мы ещё держимся… принесите лекарства…' }]);
  }
  // смена суток
  G.night = G.clock < 6 * 60 || G.clock > 20 * 60;
  // шум затихает
  G.player.noise = Math.max(0, G.player.noise - dt * 4);
}

/* ---------- сохранение ---------- */
function save(G) {
  try {
    const S = {
      ver: GAME_VERSION, chapter: G.chapter, seed: G.seed, time: G.time, clock: G.clock,
      player: Object.assign({}, G.player), inventory: G.inventory, trust: G.trust, flags: G.flags,
      objectives: G.objectives, stats: G.stats, group: G.group.map(n => ({ id: n.id, hp: n.hp, dead: n.dead, infect: n.infect, sk: n.sk, x: n.x, y: n.y })),
      over: G.over, saved: Date.now()
    };
    localStorage.setItem(SAVE_KEY, JSON.stringify(S));
  } catch (e) { }
}
function loadSave() {
  try {
    const s = localStorage.getItem(SAVE_KEY);
    if (!s) return null;
    const S = JSON.parse(s);
    if (!S || !S.chapter) return null;
    return S;
  } catch (e) { return null; }
}
function clearSave() { try { localStorage.removeItem(SAVE_KEY); } catch (e) { } }

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { newGame, updatePlayer, updateZombies, updateBullets, updateGroup, findPath, attack, interact, hurtPlayer, addNoise, equip, useItem, updateObjectives, updateWorld, updateProps, save, loadSave, ZK, NOISE };
}
