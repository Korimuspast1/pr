'use strict';
/* =====================================================================
   DEAD INSTINCT — рендер: вид сверху, свет/тьма, дождь, туман, инстинкты
   ===================================================================== */

const CAM = { x: 0, y: 0, z: 30, shake: 0, tx: 0, ty: 0, follow: true };

const RN = {
  cv: null, ctx: null, W: 0, H: 0, dpr: 1,
  lightCv: null, lightG: null,
  time: 0
};

/* ---------- инициализация ---------- */
function initRenderer(cv) {
  RN.cv = cv;
  RN.ctx = cv.getContext('2d', { alpha: false });
  RN.lightCv = document.createElement('canvas');
  RN.lightG = RN.lightCv.getContext('2d');
  resizeRenderer();
  window.addEventListener('resize', resizeRenderer);
}
function resizeRenderer() {
  if (!RN.cv) return;
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  RN.W = Math.floor(window.innerWidth);
  RN.H = Math.floor(window.innerHeight);
  RN.dpr = dpr;
  RN.cv.width = Math.floor(RN.W * dpr);
  RN.cv.height = Math.floor(RN.H * dpr);
  RN.cv.style.width = RN.W + 'px';
  RN.cv.style.height = RN.H + 'px';
  RN.lightCv.width = RN.cv.width; RN.lightCv.height = RN.cv.height;
  RN.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  RN.lightG.setTransform(dpr, 0, 0, dpr, 0, 0);
  RN.ctx.imageSmoothingEnabled = false;
  const z = RN.W < 400 ? 30 : RN.W < 560 ? 34 : RN.W < 820 ? 40 : 48;
  CAM.z = z;
}

function clampCam(G) {
  const M = G.M;
  const halfW = RN.W / CAM.z / 2, halfH = RN.H / CAM.z / 2;
  CAM.x = clamp(CAM.x, Math.min(halfW, M.W / 2), Math.max(M.W - halfW, M.W / 2));
  CAM.y = clamp(CAM.y, Math.min(halfH, M.H / 2), Math.max(M.H - halfH, M.H / 2));
}

function centerOn(G, x, y) { CAM.x = x; CAM.y = y; CAM.follow = true; clampCam(G); }

function worldToScreen(x, y) {
  return { x: (x - CAM.x) * CAM.z + RN.W / 2, y: (y - CAM.y) * CAM.z + RN.H / 2 };
}
function screenToWorld(sx, sy) {
  return { x: (sx - RN.W / 2) / CAM.z + CAM.x, y: (sy - RN.H / 2) / CAM.z + CAM.y };
}

/* ---------- видимость ---------- */
function computeVis(G) {
  const M = G.M, P = G.player;
  M.vis.fill(0);
  const daylight = !G.night;
  const R = P.torch ? 10.5 : daylight ? 12 : 5.6;
  const px = P.x | 0, py = P.y | 0;
  const R2 = Math.ceil(R);
  for (let dy = -R2; dy <= R2; dy++) for (let dx = -R2; dx <= R2; dx++) {
    const x = px + dx, y = py + dy;
    if (!inside(M, x, y)) continue;
    const d = Math.hypot(dx, dy);
    if (d > R) continue;
    // затухание у края
    if (d > R - 1.4 && Math.random() < (d - (R - 1.4)) / 1.4) continue;
    if (!losTile(M, px, py, x, y)) continue;
    const i = y * M.W + x;
    M.vis[i] = 1; M.seen[i] = 1;
  }
  // внутри комнаты — видно всю комнату, если в ней стоишь
  const room = roomAt(M, px, py);
  if (room) for (let y = room.y; y < room.y + room.h; y++) for (let x = room.x; x < room.x + room.w; x++) {
    if (inside(M, x, y)) { const i = y * M.W + x; M.vis[i] = 1; M.seen[i] = 1; }
  }
  // инстинкт: зомби «чувствуются» сквозь стены
  if (G.instinctFlash > 0.02) {
    for (const z of G.zombies) {
      const d = Math.hypot(z.x - P.x, z.y - P.y);
      if (d < 14) { const i = (z.y | 0) * M.W + (z.x | 0); if (inside(M, z.x | 0, z.y | 0)) M.vis[i] = Math.max(M.vis[i], 2); }
    }
  }
}

/* луч по тайлам с прозрачностью для леса */
function losTile(M, x0, y0, x1, y1) {
  let dx = Math.abs(x1 - x0), dy = Math.abs(y1 - y0);
  let sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
  let x = x0, y = y0, guard = 0;
  while (guard++ < 64) {
    if (x === x1 && y === y1) return true;
    const e2 = 2 * err;
    if (e2 > -dy) { err -= dy; x += sx; }
    if (e2 < dx) { err += dx; y += sy; }
    if (x === x1 && y === y1) return true;
    const t = tileAt(M, x, y);
    if (t === T.WALL) {
      // окна: 12% стен прозрачны (детерминированно)
      if (hash2(x, y, 7) < 0.12) continue;
      return false;
    }
    if (t === T.TREE && hash2(x, y, 3) < 0.55) return false;
    if (t === T.CAR) return false;
  }
  return true;
}

/* ---------- тайлы ---------- */
const TILE_CACHE = new Map();
function tileSprite(t, vx, vy) {
  const key = t + '_' + (vx & 3) + '_' + (vy & 3);
  if (TILE_CACHE.has(key)) return TILE_CACHE.get(key);
  const { c, g } = makeCanvas(16, 16);
  const R = (x, y, w, h, col) => { g.fillStyle = col; g.fillRect(x, y, w, h); };
  const rnd = mulberry32((t * 977 + (vx & 3) * 37 + (vy & 3) * 101) | 0);
  switch (t) {
    case T.DEEP: R(0, 0, 16, 16, '#14242e'); for (let i = 0; i < 5; i++) R(rnd() * 14 | 0, rnd() * 14 | 0, 2, 1, '#1c3440'); break;
    case T.WATER: R(0, 0, 16, 16, '#22404f'); for (let i = 0; i < 6; i++) R(rnd() * 13 | 0, rnd() * 14 | 0, 3, 1, '#2e5468'); for (let i = 0; i < 2; i++) R(rnd() * 12 | 0, rnd() * 14 | 0, 2, 1, '#3e6e82'); break;
    case T.SWAMP: R(0, 0, 16, 16, '#333a26'); for (let i = 0; i < 5; i++) R(rnd() * 14 | 0, rnd() * 14 | 0, 2, 2, '#28301e'); R(rnd() * 12 | 0, rnd() * 12 | 0, 3, 1, '#48543a'); break;
    case T.SAND: R(0, 0, 16, 16, '#6e6450'); for (let i = 0; i < 5; i++) R(rnd() * 14 | 0, rnd() * 14 | 0, 2, 1, '#7e7460'); break;
    case T.GRASS: R(0, 0, 16, 16, '#3a4832'); for (let i = 0; i < 10; i++) R(rnd() * 15 | 0, rnd() * 15 | 0, 1, 2, '#46583a'); if (rnd() < 0.4) R(rnd() * 14 | 0, rnd() * 14 | 0, 2, 1, '#566a44'); break;
    case T.DIRT: R(0, 0, 16, 16, '#4a4436'); for (let i = 0; i < 8; i++) R(rnd() * 15 | 0, rnd() * 15 | 0, 2, 1, '#544e3e'); break;
    case T.ROAD: R(0, 0, 16, 16, '#34343a'); R(0, 0, 16, 1, '#3e3e46'); for (let i = 0; i < 7; i++) R(rnd() * 15 | 0, rnd() * 15 | 0, 2, 1, '#2a2a2f'); if (rnd() < 0.25) R(rnd() * 14 | 0, rnd() * 14 | 0, 2, 2, '#414150'); break;
    case T.RUBBLE: R(0, 0, 16, 16, '#3a352c'); for (let i = 0; i < 9; i++) R(rnd() * 14 | 0, rnd() * 12 | 0, 2 + rnd() * 3 | 0, 2, rnd() < 0.5 ? '#5a5444' : '#47423a'); break;
    case T.FLOOR: R(0, 0, 16, 16, '#5e5448'); for (let i = 0; i < 4; i++) R(0, i * 4, 16, 1, '#4e453b'); if (rnd() < 0.3) R(rnd() * 12 | 0, rnd() * 12 | 0, 3, 3, '#6c6052'); R(0, 15, 16, 1, '#463e34'); break;
    case T.WALL: R(0, 0, 16, 16, '#2a2622'); R(0, 0, 16, 12, '#5e564a'); for (let i = 0; i < 4; i++) R((i * 5 + (rnd() * 2 | 0)) % 14, 2 + (rnd() * 8 | 0), 4, 2, '#6a6254'); R(0, 9, 16, 3, '#4a443a'); R(0, 13, 16, 3, '#1f1c18'); break;
    case T.DOOR: R(0, 0, 16, 16, '#2a2622'); R(2, 0, 12, 16, '#5a4630'); R(3, 2, 10, 12, '#6e5638'); R(12, 7, 2, 2, '#e8c86a'); break;
    case T.TREE: R(0, 0, 16, 16, '#2c3a26'); for (let i = 0; i < 12; i++) R(rnd() * 12 | 0, rnd() * 12 | 0, 4, 4, rnd() < 0.5 ? '#223020' : '#3a4c2e'); R(6, 11, 4, 5, '#31281c'); break;
    case T.CAR: R(0, 0, 16, 16, '#2c2c34'); R(1, 2, 14, 12, '#4a4a56'); R(2, 4, 12, 7, '#242c34'); R(3, 5, 10, 3, '#34485a'); R(2, 12, 12, 1, '#606070'); break;
    case T.BED: R(0, 0, 16, 16, '#4a443c'); R(1, 1, 14, 14, '#5a5a66'); R(2, 2, 12, 5, '#7c7c88'); R(2, 8, 12, 6, '#6e4040'); break;
    default: R(0, 0, 16, 16, '#2f3a2a');
  }
  TILE_CACHE.set(key, c);
  return c;
}

/* ---------- отрисовка кадра ---------- */
function render(G, dt) {
  const ctx = RN.ctx;
  RN.time += dt;
  const M = G.M, P = G.player;

  // камера
  if (CAM.follow) {
    const lead = 0.25;
    CAM.tx = P.x + Math.cos(P.dir || 0) * lead;
    CAM.ty = P.y + Math.sin(P.dir || 0) * lead;
    CAM.x = lerp(CAM.x, CAM.tx, Math.min(1, dt * 6));
    CAM.y = lerp(CAM.y, CAM.ty, Math.min(1, dt * 6));
  }
  clampCam(G);
  let shakeX = 0, shakeY = 0;
  if (CAM.shake > 0) {
    CAM.shake = Math.max(0, CAM.shake - dt * 3);
    if (G.settings.shake) { shakeX = (Math.random() - 0.5) * CAM.shake * 8; shakeY = (Math.random() - 0.5) * CAM.shake * 8; }
  }
  const ox = RN.W / 2 - CAM.x * CAM.z + shakeX, oy = RN.H / 2 - CAM.y * CAM.z + shakeY;
  const z = CAM.z;

  computeVis(G);

  ctx.fillStyle = '#0a0c0b';
  ctx.fillRect(0, 0, RN.W, RN.H);

  const vx0 = Math.max(0, Math.floor(CAM.x - RN.W / 2 / z) - 1), vx1 = Math.min(M.W - 1, Math.ceil(CAM.x + RN.W / 2 / z) + 1);
  const vy0 = Math.max(0, Math.floor(CAM.y - RN.H / 2 / z) - 1), vy1 = Math.min(M.H - 1, Math.ceil(CAM.y + RN.H / 2 / z) + 1);

  // пол/земля
  ctx.imageSmoothingEnabled = false;
  for (let y = vy0; y <= vy1; y++) for (let x = vx0; x <= vx1; x++) {
    const i = y * M.W + x;
    if (!M.seen[i]) continue;
    const vis = M.vis[i];
    let t = M.tiles[i];
    if (t === T.DOOR) t = M.floor[i] === 2 ? T.FLOOR : T.FLOOR;
    const spr = tileSprite(t === T.WALL ? T.WALL : t, x, y);
    ctx.globalAlpha = vis ? 1 : 0.62;
    ctx.drawImage(spr, ox + x * z, oy + y * z, z + 1, z + 1);
  }
  ctx.globalAlpha = 1;

  // кровь и мусор на земле
  for (const p of M.props) {
    if (p.x < vx0 || p.x > vx1 || p.y < vy0 || p.y > vy1) continue;
    const i = p.y * M.W + p.x;
    if (!M.seen[i]) continue;
    ctx.globalAlpha = M.vis[i] ? 1 : 0.3;
    const sx = ox + p.x * z, sy = oy + p.y * z;
    if (p.kind === 'blood') bloodSplat(ctx, sx + z / 2, sy + z / 2, z * 0.3, 0.55);
    else if (p.kind === 'body') {
      ctx.fillStyle = '#2a2420'; ctx.fillRect(sx + z * 0.2, sy + z * 0.35, z * 0.6, z * 0.3);
      ctx.fillStyle = '#3a3a3a'; ctx.fillRect(sx + z * 0.15, sy + z * 0.25, z * 0.3, z * 0.2);
      bloodSplat(ctx, sx + z * 0.5, sy + z * 0.6, z * 0.3, 0.6);
    } else if (p.kind === 'crate') {
      ctx.fillStyle = '#4a3a28'; ctx.fillRect(sx + z * 0.2, sy + z * 0.2, z * 0.6, z * 0.6);
      ctx.fillStyle = '#5a4a34'; ctx.fillRect(sx + z * 0.25, sy + z * 0.25, z * 0.5, z * 0.2);
    } else if (p.kind === 'plank') {
      ctx.fillStyle = '#4a3a28'; ctx.fillRect(sx, sy + z * 0.15, z, z * 0.2); ctx.fillRect(sx, sy + z * 0.65, z, z * 0.2);
    }
  }
  ctx.globalAlpha = 1;

  // контейнеры
  for (const c of M.containers) {
    if (c.x < vx0 || c.x > vx1 || c.y < vy0 || c.y > vy1) continue;
    const i = c.y * M.W + c.x;
    if (!M.seen[i]) continue;
    if (c.looted) continue;
    if (c.onFloor) {
      ctx.globalAlpha = M.vis[i] ? 1 : 0.35;
      const icon = iconItem(c.loot && c.loot[0] ? c.loot[0].k : 'food');
      ctx.drawImage(icon, ox + (c.x + 0.15) * z, oy + (c.y + 0.15) * z, z * 0.7, z * 0.7);
      ctx.globalAlpha = 1;
      continue;
    }
    ctx.globalAlpha = M.vis[i] ? 1 : 0.3;
    const sx = ox + c.x * z, sy = oy + c.y * z;
    if (c.kind === 'shelf') { ctx.fillStyle = '#3a3028'; ctx.fillRect(sx + z * 0.1, sy + z * 0.15, z * 0.8, z * 0.7); ctx.fillStyle = '#5a4a34'; ctx.fillRect(sx + z * 0.15, sy + z * 0.2, z * 0.7, z * 0.1); ctx.fillRect(sx + z * 0.15, sy + z * 0.5, z * 0.7, z * 0.1); }
    else if (c.kind === 'cabinet') { ctx.fillStyle = '#2a3a3a'; ctx.fillRect(sx + z * 0.15, sy + z * 0.1, z * 0.7, z * 0.8); ctx.fillStyle = '#4a6a6a'; ctx.fillRect(sx + z * 0.2, sy + z * 0.15, z * 0.6, z * 0.3); }
    else if (c.kind === 'car') { ctx.fillStyle = '#33333a'; ctx.fillRect(sx + z * 0.1, sy + z * 0.2, z * 0.8, z * 0.6); ctx.fillStyle = '#1f2a33'; ctx.fillRect(sx + z * 0.2, sy + z * 0.3, z * 0.6, z * 0.25); }
    else { ctx.fillStyle = '#4a4238'; ctx.fillRect(sx + z * 0.25, sy + z * 0.25, z * 0.5, z * 0.5); }
    ctx.globalAlpha = 1;
  }

  // двери
  for (const d of M.doors) {
    if (d.x < vx0 || d.x > vx1 || d.y < vy0 || d.y > vy1) continue;
    const i = d.y * M.W + d.x;
    if (!M.seen[i]) continue;
    const sx = ox + d.x * z, sy = oy + d.y * z;
    ctx.globalAlpha = M.vis[i] ? 1 : 0.3;
    if (d.broken) {
      ctx.fillStyle = '#3a3028'; ctx.fillRect(sx + z * 0.1, sy + z * 0.4, z * 0.3, z * 0.25); ctx.fillRect(sx + z * 0.6, sy + z * 0.5, z * 0.3, z * 0.2);
    } else if (d.open > 0.6) {
      ctx.fillStyle = '#4a3a28'; ctx.fillRect(sx + z * 0.05, sy + z * 0.05, z * 0.2, z * 0.9);
    } else {
      ctx.fillStyle = '#5a4a34'; ctx.fillRect(sx + z * 0.1, sy + z * 0.3, z * 0.8, z * 0.4);
      ctx.fillStyle = '#c8b06a'; ctx.fillRect(sx + z * 0.75, sy + z * 0.45, z * 0.08, z * 0.1);
      const hpFrac = clamp(d.hp / d.max, 0, 1);
      if (hpFrac < 0.99) { ctx.fillStyle = '#111'; ctx.fillRect(sx + z * 0.1, sy + z * 0.1, z * 0.8, z * 0.12); ctx.fillStyle = hpFrac > 0.5 ? '#7a8a4a' : '#a44'; ctx.fillRect(sx + z * 0.1, sy + z * 0.1, z * 0.8 * hpFrac, z * 0.12); }
    }
    ctx.globalAlpha = 1;
  }

  // трупы
  for (const c of G.corpses) {
    if (c.x < vx0 || c.x > vx1 || c.y < vy0 || c.y > vy1) continue;
    const i = (c.y | 0) * M.W + (c.x | 0);
    if (!M.seen[i]) continue;
    ctx.globalAlpha = clamp(1 - c.t / 26, 0, 1) * (M.vis[i] ? 1 : 0.3);
    const sx = ox + c.x * z, sy = oy + c.y * z;
    ctx.fillStyle = '#3a3a2c'; ctx.fillRect(sx + z * 0.1, sy + z * 0.5, z * 0.8, z * 0.3);
    ctx.fillStyle = '#6a7a4a'; ctx.fillRect(sx + z * 0.7, sy + z * 0.45, z * 0.25, z * 0.25);
    bloodSplat(ctx, sx + z * 0.5, sy + z * 0.65, z * 0.35, 0.5 * clamp(1 - c.t / 26, 0, 1));
    ctx.globalAlpha = 1;
  }

  // маркеры цели
  const activeObj = G.objectives.find(q => !q.done);
  for (const o of G.objectives) {
    if (o.done || !o.marker || o !== activeObj) continue;
    const m = o.marker;
    const sx = ox + m.x * z, sy = oy + m.y * z;
    const pulse = 0.6 + Math.sin(RN.time * 3) * 0.25;
    ctx.strokeStyle = 'rgba(232,196,90,' + pulse + ')';
    ctx.lineWidth = 3;
    ctx.strokeRect(sx + z * 0.1, sy + z * 0.1, z * 0.8, z * 0.8);
    ctx.fillStyle = 'rgba(232,196,90,' + (pulse * 0.25) + ')';
    ctx.fillRect(sx, sy, z, z);
  }

  // персонажи: сортировка по y
  const actors = [];
  for (const z2 of G.zombies) actors.push({ kind: 'z', y: z2.y, o: z2 });
  for (const f of G.group) if (!f.dead) actors.push({ kind: 'f', y: f.y, o: f });
  if (!P.dead) actors.push({ kind: 'p', y: P.y, o: P });
  actors.sort((a, b) => a.y - b.y);

  for (const a of actors) {
    const o = a.o;
    const ti = (o.y | 0) * M.W + (o.x | 0);
    const visible = M.vis[ti] === 1 || inside(M, o.x | 0, o.y | 0) && M.vis[ti] === 1;
    const sensed = o.senseT > 0;
    if (a.kind !== 'p' && !visible && !sensed) continue;
    const sx = ox + (o.x - 0.5) * z, sy = oy + (o.y - 0.8) * z;
    const frame = Math.floor((o.anim || 0) % 2);
    if (a.kind === 'p') {
      const spr = spriteHuman('player', frame, { run: P.stance === 'sprint', armsForward: P.stance === 'sprint', bob: P.stance === 'crawl' ? 0 : 0, blood: P.hp < 50 });
      drawSpriteRotated(ctx, spr, sx, sy, z, o.dir || 0);
      // конус фонаря
      if (P.torch && P.battery > 0) drawTorch(ctx, o, z, ox, oy);
      // индикатор шума
      drawNoiseRing(ctx, o, z, ox, oy);
    } else if (a.kind === 'f') {
      const spr = spriteHuman(o.id, frame, { run: false, blood: o.hp < o.maxHp * 0.5 });
      drawSpriteRotated(ctx, spr, sx, sy, z, o.dir || 0);
      // имя и полоска
      ctx.font = '9px PixelRU'; ctx.textAlign = 'center';
      ctx.fillStyle = o.sayT > 0 ? '#e8e4d8' : 'rgba(200,200,180,0.7)';
      ctx.fillText(o.n, sx + z * 0.5, sy - z * 0.2);
      if (o.hp < o.maxHp) {
        ctx.fillStyle = '#111'; ctx.fillRect(sx + z * 0.15, sy - z * 0.1, z * 0.7, 3);
        ctx.fillStyle = o.hp / o.maxHp > 0.5 ? '#6a9a5a' : '#a44'; ctx.fillRect(sx + z * 0.15, sy - z * 0.1, z * 0.7 * (o.hp / o.maxHp), 3);
      }
    } else {
      const spr = spriteZombie(o.kind, frame, {});
      if (!visible && sensed) ctx.globalAlpha = 0.35;
      drawSpriteRotated(ctx, spr, sx, sy, z * (o.boss ? 1.25 : 1), o.dir || 0);
      ctx.globalAlpha = 1;
      if (!visible && sensed) {
        // силуэт-инстинкт
        ctx.strokeStyle = 'rgba(200,80,60,0.7)'; ctx.lineWidth = 2;
        ctx.strokeRect(sx + z * 0.1, sy + z * 0.2, z * 0.8, z * 1.1);
      }
    }
    // состояние зомби
    if (a.kind === 'z') {
      const hpFrac = o.hp / o.maxHp;
      if (hpFrac < 0.999) {
        ctx.fillStyle = '#111'; ctx.fillRect(sx + z * 0.1, sy - z * 0.15, z * 0.8, 4);
        ctx.fillStyle = '#8a2424'; ctx.fillRect(sx + z * 0.1, sy - z * 0.15, z * 0.8 * hpFrac, 4);
      }
      if (o.boss) {
        ctx.font = '10px PixelRU'; ctx.textAlign = 'center'; ctx.fillStyle = '#d66';
        ctx.fillText('ГРОМИЛА', sx + z * 0.5, sy - z * 0.35);
      }
    }
  }

  // пули
  for (const b of G.bullets) {
    const sx = ox + b.x * z, sy = oy + b.y * z;
    ctx.fillStyle = b.flare ? '#ff8a3a' : '#ffe08a';
    ctx.fillRect(sx - 1.5, sy - 1.5, 3, 3);
  }

  // частицы
  for (const p of G.particles) {
    const sx = ox + p.x * z, sy = oy + p.y * z;
    const a = clamp(p.life / p.max, 0, 1);
    ctx.globalAlpha = a;
    ctx.fillStyle = p.col;
    const s = Math.max(1, z * 0.12 * (p.size / 2) * (p.kind === 'flash' ? a * 2.5 : 1));
    ctx.fillRect(sx - s / 2, sy - s / 2, s, s);
  }
  ctx.globalAlpha = 1;

  // ---------- тьма и свет ----------
  drawDarkness(G, ox, oy, z, vx0, vx1, vy0, vy1);

  // ---------- погода ----------
  drawWeather(G);

  // ---------- вспышки ----------
  if (G.dangerFlash > 0.02) {
    const a = G.dangerFlash * 0.35;
    const grd = ctx.createRadialGradient(RN.W / 2, RN.H / 2, Math.min(RN.W, RN.H) * 0.25, RN.W / 2, RN.H / 2, Math.max(RN.W, RN.H) * 0.75);
    grd.addColorStop(0, 'rgba(120,0,0,0)');
    grd.addColorStop(1, 'rgba(150,10,10,' + a + ')');
    ctx.fillStyle = grd; ctx.fillRect(0, 0, RN.W, RN.H);
  }
  if (G.instinctFlash > 0.02) {
    const a = G.instinctFlash * 0.12;
    ctx.fillStyle = 'rgba(180,220,255,' + a + ')'; ctx.fillRect(0, 0, RN.W, RN.H);
  }

  // ---------- текст в мире: всплывающие значения ----------
  ctx.textAlign = 'center';
  for (const f of G.floaters) {
    const sx = ox + f.x * z, sy = oy + f.y * z;
    const a = clamp(f.life, 0, 1);
    ctx.globalAlpha = a;
    ctx.font = '12px PixelRU';
    ctx.fillStyle = '#000'; ctx.fillText(f.text, sx + 1, sy + 1);
    ctx.fillStyle = f.col; ctx.fillText(f.text, sx, sy);
  }
  ctx.globalAlpha = 1;

  // ---------- виньетка и зерно ----------
  drawVignette(ctx);
  drawGrain(ctx);

  // ---------- направление опасности ----------
  if (G.flags.dangerDir !== undefined && G.dangerFlash > 0.3) {
    const ang = G.flags.dangerDir;
    const cx = RN.W / 2, cy = RN.H / 2, R = Math.min(RN.W, RN.H) * 0.42;
    ctx.save();
    ctx.translate(cx, cy); ctx.rotate(ang);
    ctx.globalAlpha = 0.5 * G.dangerFlash;
    ctx.fillStyle = '#c33';
    ctx.beginPath(); ctx.moveTo(R, 0); ctx.lineTo(R - 14, -7); ctx.lineTo(R - 14, 7); ctx.closePath(); ctx.fill();
    ctx.restore();
    ctx.globalAlpha = 1;
  }

  // ---------- компас к цели ----------
  drawCompass(G, ctx);
}

function drawSpriteRotated(ctx, spr, sx, sy, z, dir) {
  ctx.save();
  ctx.translate(sx + z * 0.5, sy + z * 0.5);
  // у людей вид сверху-сбоку: зеркалим по горизонтали при движении влево
  if (Math.cos(dir) < -0.2) ctx.scale(-1, 1);
  ctx.drawImage(spr, -z * 0.5, -z * 0.62, z, z * 1.5);
  ctx.restore();
}

function drawTorch(ctx, P, z, ox, oy) {
  const sx = ox + P.x * z, sy = oy + P.y * z;
  const grd = ctx.createRadialGradient(sx, sy, 0, sx, sy, z * 7);
  grd.addColorStop(0, 'rgba(255,230,170,0.10)');
  grd.addColorStop(0.5, 'rgba(255,220,150,0.05)');
  grd.addColorStop(1, 'rgba(255,220,150,0)');
  ctx.fillStyle = grd;
  ctx.beginPath(); ctx.arc(sx, sy, z * 7, 0, 6.284); ctx.fill();
}

function drawNoiseRing(ctx, P, z, ox, oy) {
  if (P.noise < 1) return;
  const sx = ox + P.x * z, sy = oy + P.y * z;
  const r = z * (1 + P.noise * 0.5);
  ctx.strokeStyle = 'rgba(200,200,160,' + clamp(0.14 + P.noise * 0.01, 0, 0.35) + ')';
  ctx.lineWidth = 1;
  ctx.beginPath(); ctx.arc(sx, sy, r, 0, 6.284); ctx.stroke();
}

/* ---------- тьма: рисуем чёрный слой и «вырезаем» свет ---------- */
function drawDarkness(G, ox, oy, z, vx0, vx1, vy0, vy1) {
  const ctx = RN.ctx, M = G.M, P = G.player;
  const lg = RN.lightG;
  const dark = G.night ? 0.68 : 0.22;
  lg.setTransform(RN.dpr, 0, 0, RN.dpr, 0, 0);
  lg.clearRect(0, 0, RN.W, RN.H);
  lg.fillStyle = 'rgba(0,0,0,' + dark + ')';
  lg.fillRect(0, 0, RN.W, RN.H);
  lg.globalCompositeOperation = 'destination-out';
  // фонарь игрока
  if (P.torch && P.battery > 0) {
    const sx = ox + P.x * z, sy = oy + P.y * z;
    const grd = lg.createRadialGradient(sx, sy, z * 0.5, sx, sy, z * 8);
    grd.addColorStop(0, 'rgba(0,0,0,1)');
    grd.addColorStop(0.55, 'rgba(0,0,0,0.75)');
    grd.addColorStop(1, 'rgba(0,0,0,0)');
    lg.fillStyle = grd;
    lg.beginPath(); lg.arc(sx, sy, z * 8, 0, 6.284); lg.fill();
  } else {
    // слабый свет вокруг игрока
    const sx = ox + P.x * z, sy = oy + P.y * z;
    const grd = lg.createRadialGradient(sx, sy, z * 0.4, sx, sy, z * 3.2);
    grd.addColorStop(0, 'rgba(0,0,0,0.75)');
    grd.addColorStop(1, 'rgba(0,0,0,0)');
    lg.fillStyle = grd;
    lg.beginPath(); lg.arc(sx, sy, z * 3.2, 0, 6.284); lg.fill();
  }
  // источники света на карте
  for (const L of M.lights) {
    if (L.x < vx0 - 4 || L.x > vx1 + 4 || L.y < vy0 - 4 || L.y > vy1 + 4) continue;
    let on = true;
    if (L.kind === 'room' || L.kind === 'safe') on = (G.flags.generatorOn || M.kind === 'hospital' || Math.random() < 0.998);
    if (L.kind === 'street') on = true;
    if (!on) continue;
    const sx = ox + L.x * z, sy = oy + L.y * z;
    const flick = 0.85 + Math.sin(RN.time * 9 + L.flicker * 40) * 0.15;
    const r = L.r * z * flick;
    const grd = lg.createRadialGradient(sx, sy, 0, sx, sy, r);
    grd.addColorStop(0, 'rgba(0,0,0,0.92)');
    grd.addColorStop(0.6, 'rgba(0,0,0,0.45)');
    grd.addColorStop(1, 'rgba(0,0,0,0)');
    lg.fillStyle = grd;
    lg.beginPath(); lg.arc(sx, sy, r, 0, 6.284); lg.fill();
  }
  lg.globalCompositeOperation = 'source-over';
  ctx.drawImage(RN.lightCv, 0, 0, RN.W, RN.H);

  // туман памяти: где уже был, но не видно сейчас — сизая дымка
  ctx.fillStyle = 'rgba(16,21,23,0.34)';
  for (let y = vy0; y <= vy1; y++) for (let x = vx0; x <= vx1; x++) {
    const i = y * M.W + x;
    if (M.seen[i] && !M.vis[i]) ctx.fillRect(ox + x * z, oy + y * z, z + 1, z + 1);
  }
  // цветные блики света
  ctx.globalCompositeOperation = 'lighter';
  for (const L of M.lights) {
    if (L.x < vx0 - 4 || L.x > vx1 + 4 || L.y < vy0 - 4 || L.y > vy1 + 4) continue;
    if ((L.kind === 'room' || L.kind === 'safe') && !G.flags.generatorOn && Math.random() < 0.02) continue;
    const sx = ox + L.x * z, sy = oy + L.y * z;
    const r = L.r * z * 0.8;
    const grd = ctx.createRadialGradient(sx, sy, 0, sx, sy, r);
    grd.addColorStop(0, hexA(L.col, 0.13));
    grd.addColorStop(1, hexA(L.col, 0));
    ctx.fillStyle = grd;
    ctx.beginPath(); ctx.arc(sx, sy, r, 0, 6.284); ctx.fill();
  }
  ctx.globalCompositeOperation = 'source-over';
}

function hexA(hex, a) {
  const h = hex.replace('#', '');
  const r = parseInt(h.slice(0, 2), 16), g = parseInt(h.slice(2, 4), 16), b = parseInt(h.slice(4, 6), 16);
  return 'rgba(' + r + ',' + g + ',' + b + ',' + a + ')';
}

/* ---------- дождь и туман ---------- */
const RAIN = [];
function drawWeather(G) {
  const ctx = RN.ctx;
  const inten = G.weather.rain;
  if (RAIN.length < 160) for (let i = RAIN.length; i < 160; i++) RAIN.push({ x: Math.random() * RN.W, y: Math.random() * RN.H, v: 380 + Math.random() * 260, l: 8 + Math.random() * 10 });
  if (G.weather.rain > 0.15) {
    ctx.strokeStyle = 'rgba(150,180,200,' + (0.09 + inten * 0.16) + ')';
    ctx.lineWidth = 1;
    ctx.beginPath();
    const wind = G.weather.wind;
    for (const r of RAIN) {
      r.y += r.v * 0.016 * (RN.dtScale || 1); r.x += r.v * 0.016 * wind * 0.5 * (RN.dtScale || 1);
      if (r.y > RN.H) { r.y = -10; r.x = Math.random() * RN.W; }
      if (r.x > RN.W) r.x = 0;
      ctx.moveTo(r.x, r.y); ctx.lineTo(r.x - wind * r.l * 0.4, r.y + r.l);
    }
    ctx.stroke();
  }
  if (G.weather.fog > 0.3) {
    const grd = ctx.createRadialGradient(RN.W / 2, RN.H / 2, RN.H * 0.2, RN.W / 2, RN.H / 2, RN.H * 0.85);
    grd.addColorStop(0, 'rgba(60,70,70,0)');
    grd.addColorStop(1, 'rgba(60,70,70,' + (0.35 * G.weather.fog) + ')');
    ctx.fillStyle = grd; ctx.fillRect(0, 0, RN.W, RN.H);
  }
}

function drawVignette(ctx) {
  const grd = ctx.createRadialGradient(RN.W / 2, RN.H / 2, Math.min(RN.W, RN.H) * 0.35, RN.W / 2, RN.H / 2, Math.max(RN.W, RN.H) * 0.72);
  grd.addColorStop(0, 'rgba(0,0,0,0)');
  grd.addColorStop(1, 'rgba(0,0,0,0.55)');
  ctx.fillStyle = grd; ctx.fillRect(0, 0, RN.W, RN.H);
}

const GRAIN = [];
function drawGrain(ctx) {
  if (GRAIN.length === 0) for (let i = 0; i < 90; i++) GRAIN.push({ x: Math.random(), y: Math.random(), a: Math.random() * 0.06 });
  ctx.fillStyle = '#fff';
  for (const g of GRAIN) {
    ctx.globalAlpha = g.a * (0.5 + Math.random());
    ctx.fillRect((g.x * RN.W + Math.random() * 3) | 0, (g.y * RN.H + Math.random() * 3) | 0, 1, 1);
  }
  ctx.globalAlpha = 1;
}

/* ---------- компас к цели ---------- */
function drawCompass(G, ctx) {
  const o = G.objectives.find(q => !q.done && q.marker);
  if (!o) return;
  const dx = o.marker.x - G.player.x, dy = o.marker.y - G.player.y;
  const d = Math.hypot(dx, dy);
  if (d < 6) return;
  const ang = Math.atan2(dy, dx);
  const cx = RN.W / 2, cy = RN.H / 2, R = Math.min(RN.W, RN.H) * 0.36;
  const px = cx + Math.cos(ang) * R, py = cy + Math.sin(ang) * R;
  ctx.save();
  ctx.translate(px, py); ctx.rotate(ang);
  ctx.globalAlpha = 0.7 + Math.sin(RN.time * 2.5) * 0.2;
  ctx.fillStyle = '#000'; ctx.fillRect(-6, -8, 18, 16);
  ctx.fillStyle = '#e8c45a';
  ctx.beginPath(); ctx.moveTo(11, 0); ctx.lineTo(-3, -7); ctx.lineTo(-3, 7); ctx.closePath(); ctx.fill();
  ctx.restore();
  ctx.globalAlpha = 1;
  ctx.font = '10px PixelRU'; ctx.textAlign = 'center'; ctx.fillStyle = 'rgba(232,196,90,0.85)'; ctx.fillText(Math.round(d) + 'м', px, py + 20);
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { initRenderer, render, CAM, RN, centerOn, worldToScreen, screenToWorld, tileSprite };
}
