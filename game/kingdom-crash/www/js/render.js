'use strict';
/* ===== Отрисовка карты ===== */
const RN = { cv: null, ctx: null, dpr: 1, w: 0, h: 0, cam: { x: 0, y: 0, z: 3 }, terrain: null, terrCv: null, cloud: null, cache: {}, S: null, sel: null, place: null, minimap: null, showTerr: true, mask: null };
const hash = (x, y, k) => { let h = (Math.imul(x, 73856093) ^ Math.imul(y, 19349663) ^ Math.imul(k, 83492791)) >>> 0; h = Math.imul(h ^ h >>> 13, 1274126177) >>> 0; return ((h ^ h >>> 16) >>> 0) / 4294967296; };
const TCOL = ['#1f4f8e', '#4a98c6', '#e0ce98', '#58943c', '#9ea654', '#3a6c3a', '#868c54', '#84868c']; // цвета клеток: мини-карта и ещё не построенные участки
function shade(hex, f) { const n = parseInt(hex.slice(1), 16); let r = n >> 16 & 255, g = n >> 8 & 255, b = n & 255; r = Math.max(0, Math.min(255, r * f)) | 0; g = Math.max(0, Math.min(255, g * f)) | 0; b = Math.max(0, Math.min(255, b * f)) | 0; return `rgb(${r},${g},${b})`; }
function newCv(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; }

function buildTerrain(S) {
  const W = S.W, H = S.H;
  TERR.init(S); RN.terrain = TERR;
  // облака
  const cw = W * 4, ch = H * 4, cc = newCv(cw, ch), cx = cc.getContext('2d'), nz = makeNoise(S.seed + 4242);
  for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) { const v = nz(x / 28, y / 28, 3); if (v > 0.6) { cx.fillStyle = v > 0.66 ? 'rgba(255,255,255,0.30)' : 'rgba(255,255,255,0.16)'; cx.fillRect(x, y, 1, 1); } }
  RN.cloud = cc; RN.terrCv = newCv(W * 8, H * 8); S.dirtyT = true; RN.cache = {};
}
function drawTerr(S) {
  const W = S.W, H = S.H, c = RN.terrCv.getContext('2d'); c.clearRect(0, 0, W * 8, H * 8);
  const own = (x, y) => (x < 0 || y < 0 || x >= W || y >= H) ? -3 : S.terr[y * W + x];
  const col = o => o === -1 ? '#bbbbbb' : S.players[o].col;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const o = S.terr[y * W + x]; if (o === -2) continue; const px = x * 8, py = y * 8, cl = col(o);
    c.globalAlpha = o === -1 ? 0.10 : 0.20; R_(c, px, py, 8, 8, cl); c.globalAlpha = 1;
    const e = (dx, dy) => own(x + dx, y + dy) !== o;
    c.fillStyle = cl;
    if (e(0, -1)) for (let i = 0; i < 8; i += 2) c.fillRect(px + i, py, 1, 1), c.fillRect(px + i + 1, py + 1, 1, 1);
    if (e(0, 1)) for (let i = 0; i < 8; i += 2) c.fillRect(px + i, py + 7, 1, 1), c.fillRect(px + i + 1, py + 6, 1, 1);
    if (e(-1, 0)) for (let i = 0; i < 8; i += 2) c.fillRect(px, py + i, 1, 1), c.fillRect(px + 1, py + i + 1, 1, 1);
    if (e(1, 0)) for (let i = 0; i < 8; i += 2) c.fillRect(px + 7, py + i, 1, 1), c.fillRect(px + 6, py + i + 1, 1, 1);
  }
  S.dirtyT = false;
}
function sprite(key, w, h, fn) { let s = RN.cache[key]; if (!s) { s = newCv(w, h); fn(s.getContext('2d')); RN.cache[key] = s; } return s; }
function volSprite(key, w, h, depth, fn) { // спрайт с объёмом: боковая грань, тень
  return sprite(key, w + depth + 2, h + 3, out => {
    const b = newCv(w, h); fn(b.getContext('2d'));
    const tint = col => { const t = newCv(w, h), x = t.getContext('2d'); x.drawImage(b, 0, 0); x.globalCompositeOperation = 'source-atop'; x.fillStyle = col; x.fillRect(0, 0, w, h); return t; };
    const sh = tint('rgba(0,0,0,0.5)'), sd = tint('rgba(12,10,40,0.45)');
    out.globalAlpha = 0.45; out.drawImage(sh, depth + 1, 2); out.globalAlpha = 1;
    for (let k = depth; k >= 1; k--) out.drawImage(sd, k, 0);
    out.drawImage(b, 0, 0);
  });
}
function bldSprite(b, col, mask) { const st = b.type === 'wall', dp = STRUCT[b.type] && (mask & 2) ? 0 : 3; return volSprite('b' + b.type + (st ? '' : col) + b.res + (mask || 0), 32, 32, dp, c => DRAW[b.type](c, 0, 0, col, b.res, mask)); }
function structMask(S, b) {
  const t = b.type, nb = (dx, dy) => { const x = b.x + dx, y = b.y + dy; if (x < 0 || y < 0 || x >= S.W || y >= S.H) return false; if (t === 'moat') { if (isWater(S.ter[y * S.W + x])) return true; const o = S.occ[y * S.W + x], mb = o >= 0 && o < 1000000 ? S.bmap.get(o) : null; return !!(mb && mb.type === 'moat'); } const sb = structAtTile(S, x, y); return !!sb; };
  return (nb(0, -1) ? 1 : 0) | (nb(1, 0) ? 2 : 0) | (nb(0, 1) ? 4 : 0) | (nb(-1, 0) ? 8 : 0);
}
function moatSprite(mask, wet) { return sprite('m' + mask + wet, 32, 32, c => drawMoat(c, 0, 0, mask, wet)); }
function lineTiles(x0, y0, x1, y1) { const l = []; let dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, er = dx + dy, x = x0, y = y0; for (let n = 0; n < 400; n++) { l.push({ x, y }); if (x === x1 && y === y1) break; const e2 = 2 * er; if (e2 >= dy) { er += dy; x += sx; } if (e2 <= dx) { er += dx; y += sy; } } return l; }
function drawFig(c, t, col, fx, fy, face, cls) { const sp = figSprite(t, col, cls); c.save(); c.translate(fx, fy); c.scale(face, 1); c.drawImage(sp, -sp.width * 0.125, -sp._feet * 0.25, sp.width * 0.25, sp.height * 0.25); c.restore(); }
function citySprite(ct, col) { return volSprite('c' + col + ct.level + ct.isCap, 64, 96, 4, c => drawCitySprite(c, 0, 16, col, ct.level, ct.isCap)); }
const drawSpr = (c, spr, x, y) => c.drawImage(spr, x, y, spr.width * 0.5, spr.height * 0.5);
/* ----- анимации ----- */
function smoke(c, wx, wy, tm, seed, sc) { for (let k = 0; k < 4; k++) { const ph = (tm * 0.42 + k / 4 + seed * 0.37) % 1, a = (1 - ph) * 0.6, sz = (0.7 + ph * 1.5) * (sc || 1); c.globalAlpha = a; R_(c, wx + Math.sin(ph * 7 + k + seed) * 0.7 + ph * 1.3 - sz / 2, wy - ph * 7 - sz / 2, sz, sz, k % 2 ? '#e0e0e8' : '#b4b4c0'); } c.globalAlpha = 1; }
function waveFlag(c, wx, wy, col, tm, seed) { for (let i = 0; i < 6; i++) { const yo = Math.sin(tm * 5 + i * 0.9 + seed) * 0.45 * (i / 5 + 0.15); R_(c, wx + i * 0.5, wy + yo, 0.5, 2, col); R_(c, wx + i * 0.5, wy + yo, 0.5, 0.4, shadeC(col, 1.4)); R_(c, wx + i * 0.5, wy + yo + 1.6, 0.5, 0.4, shadeC(col, 0.62)); } }
function animBuilding(c, type, ox, oy, col, tm, seed, res, bb) {
  const H = 0.5;
  switch (type) {
    case 'forge': { smoke(c, ox + 23 * H, oy + 1, tm, seed); const f = Math.floor(tm * 9 + seed) % 3; R_(c, ox + (8 + f) * H, oy + (19 + f % 2) * H, 4 * H, 3 * H, f === 1 ? '#fff4b0' : '#ffb840'); if (f === 2) R_(c, ox + 3 * H, oy + (20 - (tm * 8 % 3)) * H, 0.5, 0.5, '#ffd860'); break; }
    case 'sawmill': { smoke(c, ox + 19.5 * H, oy + 3 * H, tm, seed, 0.8); const a = tm * 7 + seed; for (let k = 0; k < 3; k++) { const an = a + k * 2.09; R_(c, ox + (25 + Math.cos(an) * 3.6) * H, oy + (24 + Math.sin(an) * 3.6) * H, 0.7, 0.7, '#fff'); } break; }
    case 'hospital': { if (bb && bb.treated > 0) for (let k = 0; k < 3; k++) { const ph = (tm * 0.8 + k / 3 + seed * 0.2) % 1, px = ox + (6 + k * 9) * H + Math.sin(ph * 6 + k) * 0.6, py = oy + 4 - ph * 7; c.globalAlpha = 1 - ph; R_(c, px - 0.3, py - 1, 0.6, 2, '#7dff7d'); R_(c, px - 1, py - 0.3, 2, 0.6, '#7dff7d'); c.globalAlpha = 1; } R_(c, ox + 14.5 * H, oy + 14.5 * H, 0.0, 0.0, '#fff'); break; }
    case 'workshop': smoke(c, ox + 22 * H, oy + 1 * H, tm, seed, 0.7); break;
    case 'tavern': smoke(c, ox + 21 * H, oy + 1 * H, tm, seed, 0.7); break;
    case 'mill': millBlades(c, ox + 16 * H, oy + 9 * H, tm * 1.1 + seed, H); break;
    case 'barracks': waveFlag(c, ox + 16 * H, oy + 2 * H, col, tm, seed); break;
    case 'market': waveFlag(c, ox + 16 * H, oy + 0, col, tm, seed); break;
    case 'port': waveFlag(c, ox + 29 * H, oy + 12 * H, col, tm, seed); break;
    case 'mine': { waveFlag(c, ox + 28 * H, oy + 1 * H, col, tm, seed); const f = Math.floor(tm * 6 + seed) % 3; R_(c, ox + 15 * H, oy + 17 * H, 2 * H, 2 * H, f === 0 ? '#ffe890' : '#f8c850'); if (Math.floor(tm * 2 + seed) % 4 === 0) R_(c, ox + 4 * H, oy + 21 * H, 0.5, 0.5, '#fff'); if (Math.floor(tm * 2 + seed) % 4 === 2) R_(c, ox + 27 * H, oy + 19 * H, 0.5, 0.5, '#fff'); break; }
    case 'farm': { for (let j = 0; j < 5; j++) { const px = (tm * 5 + j * 3 + seed) % 22; if (px < 18) R_(c, ox + (1 + px) * H, oy + (2 + j * 4) * H, 0.5, 0.5, '#d8ff98'); } break; }
    case 'house': smoke(c, ox + 24 * H, oy + 2 * H, tm, seed, 0.55); break;
    case 'prison': { const f = Math.floor(tm * 2 + seed) % 2; R_(c, ox + 15.5 * H, oy + 12 * H, 1, 1, f ? '#ffe890' : '#c8a848'); break; }
  }
}
function ownerCol(S, o) { return o < 0 ? '#9a9a9a' : S.players[o].col; }
// где можно строить: маску считаем один раз и обновляем, только когда на карте что-то изменилось
// (раньше каждая клетка экрана проверялась заново в каждом кадре)
function siteMask(S, type) {
  const m = RN.mask; if (m && m.S === S && m.type === type && m.sv === S.sv && m.n === S.buildings.length) return m.ok;
  const ok = new Uint8Array(S.W * S.H), taken = BD[type].one && S.buildings.some(b => b.type === type && b.owner === 0);
  if (!taken) for (let i = 0; i < ok.length; i++) if (S.terr[i] === 0 && !siteTile(S, 0, type, i % S.W, (i / S.W) | 0)) ok[i] = 1;
  RN.mask = { S, type, sv: S.sv, n: S.buildings.length, ok }; return ok;
}

function resize() {
  const cv = RN.cv; RN.dpr = Math.min(2, window.devicePixelRatio || 1); RN.w = window.innerWidth; RN.h = window.innerHeight;
  cv.width = RN.w * RN.dpr; cv.height = RN.h * RN.dpr; cv.style.width = RN.w + 'px'; cv.style.height = RN.h + 'px';
}
function clampCam() {
  const S = RN.S; if (!S) return; const cam = RN.cam, vw = RN.w / cam.z, vh = RN.h / cam.z, mw = S.W * TS, mh = S.H * TS;
  cam.x = Math.max(-vw * 0.3, Math.min(mw - vw * 0.7, cam.x)); cam.y = Math.max(-vh * 0.3, Math.min(mh - vh * 0.7, cam.y));
}
function centerOn(tx, ty) { RN.cam.x = tx * TS - RN.w / RN.cam.z / 2; RN.cam.y = ty * TS - RN.h / RN.cam.z / 2; clampCam(); }

function frame(S, now) {
  const c = RN.ctx, cam = RN.cam, z = cam.z * RN.dpr; c.setTransform(1, 0, 0, 1, 0, 0); c.imageSmoothingEnabled = false;
  c.fillStyle = '#10222f'; c.fillRect(0, 0, RN.cv.width, RN.cv.height);
  if (!S || !RN.terrain) return;
  if (S.dirtyT) drawTerr(S);
  if (S.patches && S.patches.length) { for (const p of S.patches) TERR.dirty(p.x, p.y, p.r || 2); S.patches = []; }
  c.setTransform(z, 0, 0, z, -cam.x * z, -cam.y * z); c.imageSmoothingEnabled = false;
  const vx0 = Math.max(0, Math.floor(cam.x / TS) - 1), vy0 = Math.max(0, Math.floor(cam.y / TS) - 1), vx1 = Math.min(S.W, Math.ceil((cam.x + RN.w / cam.z) / TS) + 1), vy1 = Math.min(S.H, Math.ceil((cam.y + RN.h / cam.z) / TS) + 1);
  TERR.draw(c, cam, z, RN.cv.width, RN.cv.height); c.imageSmoothingEnabled = false;
  // искры воды
  const tm = now / 1000;
  for (let y = vy0; y < vy1; y++) for (let x = vx0; x < vx1; x++) if (S.ter[y * S.W + x] <= 1 && hash(x, y, Math.floor(tm * 1.2) % 7) > 0.93) R_(c, x * TS + ((hash(x, y, 3) * 12) | 0), y * TS + ((hash(x, y, 4) * 12) | 0), 3, 1, 'rgba(255,255,255,0.55)');
  if (RN.showTerr) c.drawImage(RN.terrCv, 0, 0, S.W * TS, S.H * TS);
  // подсветка допустимых мест
  const pl = RN.place;
  if (pl && pl.type !== 'city') {
    const okm = siteMask(S, pl.type); c.globalAlpha = 0.35;
    for (let y = vy0; y < vy1; y++) for (let x = vx0; x < vx1; x++) if (okm[y * S.W + x]) R_(c, x * TS, y * TS, TS, TS, '#7dff7d');
    c.globalAlpha = 1;
  }
  // здания
  for (const b of S.buildings) { if (b.type !== 'moat' || b.x < vx0 - 1 || b.x > vx1 || b.y < vy0 - 1 || b.y > vy1) continue; c.drawImage(moatSprite(structMask(S, b), b.wet ? 1 : 0), b.x * TS, b.y * TS, TS, TS); if (b.wet) { const k = Math.floor(tm * 1.5 + b.id) % 5; if (k < 2) R_(c, b.x * TS + 3 + hash(b.x, b.y, k) * 8, b.y * TS + 3 + hash(b.x, b.y, k + 9) * 8, 2, 0.5, 'rgba(255,255,255,0.5)'); } }
  const vis = []; for (const b of S.buildings) { if (b.type === 'moat' || b.x < vx0 - 1 || b.x > vx1 || b.y < vy0 - 1 || b.y > vy1) continue; vis.push(b); } vis.sort((p, q) => p.y - q.y || p.x - q.x);
  for (const b of vis) {
    const st = STRUCT[b.type];
    drawSpr(c, bldSprite(b, ownerCol(S, b.owner), st ? structMask(S, b) : 0), b.x * TS, b.y * TS);
    if (RN.cam.z >= 1.1) { TERR.over(c, b.x, b.y + 1); TERR.over(c, b.x - 1, b.y + 1); TERR.over(c, b.x + 1, b.y + 1); }
    if (b.owner >= -1) animBuilding(c, b.type, b.x * TS, b.y * TS, ownerCol(S, b.owner), now / 1000, b.id, b.res, b);
    if (st && b.hp < b.mhp) { const f = Math.max(0, b.hp / b.mhp); if (f < 0.6) for (let k = 0; k < 3; k++) R_(c, b.x * TS + 3 + hash(b.x, b.y, k) * 9, b.y * TS + 3 + hash(b.x, b.y, k + 5) * 9, 0.5, 2.2 + k, '#2a2420'); R_(c, b.x * TS + 2, b.y * TS - 1.4, 12, 1.3, '#14100c'); R_(c, b.x * TS + 2.3, b.y * TS - 1.1, 11.4 * f, 0.7, f > 0.5 ? '#7dff7d' : f > 0.25 ? '#ffd24a' : '#ff6a4a'); }
    if (b.type === 'tower' && b.gar && b.gar.s.length) { const col = ownerCol(S, b.owner), n = Math.min(3, b.gar.s.length); for (let k = 0; k < n; k++) { const sh = b.gar.s[k], bob = tm - (sh.atk || -9) < 0.3 ? -0.3 : 0; drawFig(c, 'archer', col, b.x * TS + 4.2 + k * 3.8, b.y * TS + 4.6 + bob, 1); } }
  }
  // города
  for (const ct of S.cities) {
    drawSpr(c, citySprite(ct, ownerCol(S, ct.owner)), ct.x * TS, ct.y * TS - 14); animCity(c, ct, ownerCol(S, ct.owner), now / 1000);
    if (ct.cap) { const col = ownerCol(S, ct.cap.by), total = 64, filled = Math.floor(ct.cap.p * total); for (let i = 0; i < total; i++) { const gx = ct.x * TS + (i % 8) * 4, gy = ct.y * TS + ((i / 8) | 0) * 4; c.globalAlpha = i < filled ? 0.5 : 0.12; R_(c, gx, gy, 4, 4, col); c.globalAlpha = 0.55; c.strokeStyle = col; c.lineWidth = 0.4; c.strokeRect(gx + 0.2, gy + 0.2, 3.6, 3.6); } c.globalAlpha = 1; }
  }
  // маршруты: стрелка направления с учётом воды, гор и границ
  const sel = RN.sel;
  for (const a of S.armies) { if (a.owner !== 0 || a.ship || !a.path.length) continue; const isSel = sel && sel.k === 'army' && sel.id === a.id; if (isSel || (a.st === 'move')) drawRoute(c, a.x, a.y, a.path, isSel ? 1 : 0.35, tm, '#ffe27a'); }
  if (sel && sel.k === 'ship') { const a = S.ships.find(q => q.id === sel.id); if (a && a.path.length) drawRoute(c, a.x, a.y, a.path, 1, tm, '#bfe8ff'); }
  // корабли
  for (const sh of S.ships) {
    if (!canSee(S, sh)) continue; const col = ownerCol(S, sh.owner), bob = Math.sin(tm * 2 + sh.id) > 0 ? 0 : 0.5, sp = sprite('sh' + sh.kind + col, 36, 24, cc => cc.drawImage(shipSpriteCanvas(sh.kind, col), 0, 0));
    c.save(); c.translate(sh.x * TS, sh.y * TS + bob); c.scale(sh.face || 1, 1); c.drawImage(sp, -6, -5.5, 12, 8); c.restore();
    if (sel && sel.k === 'ship' && sel.id === sh.id) selBox(c, sh.x * TS - 7, sh.y * TS - 6, 14, 11, tm);
    if (sh.cargo) R_(c, sh.x * TS - 1.5, sh.y * TS - 7, 3, 1.5, col);
  }
  // обучение: бойцы выходят из казарм по одному и строятся
  for (const b of S.buildings) {
    if (b.type !== 'barracks' || !b.queue.length || !canSee(S, { owner: b.owner, x: b.x, y: b.y })) continue;
    const q = b.queue[0], T = q.total || SQ[q.t].t, prog = Math.max(0, Math.min(1, 1 - q.left / T)), col = ownerCol(S, b.owner), rp = rallyPos(S, b), cnt = SQ[q.t].cnt || 10, fm = formSlots(cnt), p10 = prog * cnt, doorX = (b.x + 0.5) * TS, doorY = (b.y + 0.95) * TS;
    for (let i = 0; i < cnt && i < fm.f.length; i++) {
      if (p10 < i) continue; const pr = Math.min(1, (p10 - i) / 0.55), e = 1 - (1 - pr) * (1 - pr), tx = rp.x * TS + fm.f[i].ox, ty = rp.y * TS + fm.f[i].oy;
      const fx = doorX + (tx - doorX) * e, fy = doorY + (ty - doorY) * e, walk = pr < 1, by = walk ? -Math.abs(Math.sin(tm * 9 + i * 1.7)) * 0.5 : 0;
      drawFig(c, q.t, col, fx, fy + by, tx >= doorX ? 1 : -1);
    }
  }
  // армии: каждый боец живёт отдельно (в бою — собственные позиции, вне боя — строй)
  for (const a of S.armies) {
    if (a.ship || !canSee(S, a)) continue; const col = ownerCol(S, a.owner), x = a.x * TS, y = a.y * TS, list = armySoldiers(a), fm = formSlots(Math.max(1, list.length)), moving = a.st === 'move' && a.path.length && !a.fight;
    if (a.fight && a.ft) { const mx = (a.x + a.ft.x) / 2 * TS, my = (a.y + a.ft.y) / 2 * TS; dust(c, mx, my, tm, a.id); }
    if (sel && sel.k === 'army' && sel.id === a.id) { c.strokeStyle = Math.floor(tm * 3) % 2 ? '#fff' : '#ffe27a'; c.lineWidth = 0.5; c.beginPath(); c.ellipse(x, y, fm.w / 2 + 2.5, fm.h / 2 + 2.2, 0, 0, 6.3); c.stroke(); }
    const draw = [];
    for (let i = 0; i < list.length; i++) {
      const e = list[i], s2 = e.s, sl = fm.f[i], inB = s2.x !== undefined, dx = inB ? s2.x * TS : x + sl.ox, dy = inB ? s2.y * TS : y + sl.oy;
      if (a.born != null && a.from) { const ag = S.time - a.born, du = a.bdur || 0.85; if (ag < du + list.length * 0.09 + 0.1) { const pr = Math.max(0, Math.min(1, (ag - i * 0.09) / du)); if (pr <= 0) continue; const ez = 1 - (1 - pr) * (1 - pr), fx0 = a.from.x * TS, fy0 = a.from.y * TS; s2._dx = fx0 + (dx - fx0) * ez; s2._dy = fy0 + (dy - fy0) * ez; draw.push({ e, s: s2, inB, walk: pr < 1, i }); continue; } }
      if (s2._dx === undefined) { s2._dx = dx; s2._dy = dy; } const mvx = dx - s2._dx, mvy = dy - s2._dy, k = inB ? 0.5 : 0.2; s2._dx += mvx * k; s2._dy += mvy * k;
      draw.push({ e, s: s2, inB, walk: moving || Math.hypot(mvx, mvy) > 0.35, i });
    }
    draw.sort((p, q) => p.s._dy - q.s._dy);
    for (const d of draw) {
      const s2 = d.s, st = S.time - (s2.atk || -9), face = d.inB ? (s2.face || 1) : (a.face || 1); let fx = s2._dx, fy = s2._dy, by = d.walk ? -Math.abs(Math.sin(tm * 9 + d.i * 1.7)) * 0.45 : 0;
      if (d.inB && st < 0.24 && d.e.t !== 'archer') { fx += face * Math.sin(st / 0.24 * Math.PI) * 0.9; by -= 0.15; }
      drawFig(c, d.e.t, col, fx, fy + by, face, d.e.cls);
      if (d.e.t === 'cmd') { c.save(); c.globalAlpha = 0.5 + 0.25 * Math.sin(tm * 3); c.strokeStyle = '#ffe27a'; c.lineWidth = 0.5; c.beginPath(); c.ellipse(fx, fy + 0.4, 2.8, 1.1, 0, 0, 6.3); c.stroke(); if (sel && sel.k === 'army' && sel.id === a.id) { c.globalAlpha = 0.45; c.setLineDash([2, 2]); c.lineWidth = 0.6; c.beginPath(); c.ellipse(fx, fy, AURA_R * TS, AURA_R * TS * 0.8, 0, 0, 6.3); c.stroke(); } c.restore(); }
      const mh = SQ[d.e.t].hp; if (s2.hp < mh * 0.98) { R_(c, fx - 1.5, fy - 4.6, 3, 0.55, '#14100c'); R_(c, fx - 1.4, fy - 4.5, 2.8 * Math.max(0, s2.hp / mh), 0.35, s2.hp / mh > 0.5 ? '#7dff7d' : s2.hp / mh > 0.25 ? '#ffd24a' : '#ff6a4a'); }
    }
    const bx = x - fm.w / 2 - 1.8; R_(c, bx, y - fm.h / 2 - 3.5, 0.4, 5.5, '#5a3a1a'); c.save(); c.translate(bx + 0.4, y - fm.h / 2 - 3.4); c.scale(0.55, 0.55); waveFlag(c, 0, 0, col, tm, a.id); c.restore();
    const f = Math.max(0.05, Math.min(1, units(a) / armyCap(a))), by2 = y + fm.h / 2 + 1.6; R_(c, x - fm.w / 2 - 0.5, by2, fm.w + 1, 1.1, '#14100c'); R_(c, x - fm.w / 2 - 0.2, by2 + 0.3, (fm.w + 0.4) * f, 0.5, a.owner === 0 ? '#7dff7d' : '#ff7d7d');
  }
  // эффекты боя: летящие стрелы и удары
  for (const f of S.fx) {
    const age = S.time - f.t; if (age < 0) continue;
    if (f.k === 'arr') {
      if (age > f.d + 0.15 || (!visibleAt(S, f.x1, f.y1) && !visibleAt(S, f.x0, f.y0))) continue;
      const p = Math.min(1, age / f.d), dd = Math.hypot(f.x1 - f.x0, f.y1 - f.y0) * TS, ah = Math.min(14, 3 + dd * 0.35), pos = q => [(f.x0 + (f.x1 - f.x0) * q) * TS, (f.y0 + (f.y1 - f.y0) * q) * TS - 3 - Math.sin(Math.PI * q) * ah];
      const a1 = pos(p), a2 = pos(Math.min(1, p + 0.04));
      if (p < 1) { c.save(); c.translate(a1[0], a1[1]); c.rotate(Math.atan2(a2[1] - a1[1], a2[0] - a1[0])); R_(c, -1.8, -0.2, 2.8, 0.4, '#e8dcc0'); R_(c, 1, -0.5, 0.9, 1, '#d8dee8'); R_(c, -1.9, -0.6, 0.7, 1.2, '#c04a3a'); c.restore(); }
      else { c.globalAlpha = 1 - (age - f.d) / 0.15; R_(c, a1[0] - 0.5, a1[1] - 0.5, 1, 1, '#fff'); R_(c, a1[0] - 1.2, a1[1], 0.5, 0.5, '#ffd870'); R_(c, a1[0] + 0.8, a1[1] - 0.8, 0.5, 0.5, '#ffd870'); c.globalAlpha = 1; }
    } else if (f.k === 'rock') {
      if (age > f.d + 0.5 || (!visibleAt(S, f.x1, f.y1) && !visibleAt(S, f.x0, f.y0))) continue; const p = Math.min(1, age / f.d), ah = 18, x = (f.x0 + (f.x1 - f.x0) * p) * TS, gy = (f.y0 + (f.y1 - f.y0) * p) * TS, y = gy - 2 - Math.sin(Math.PI * p) * ah;
      if (p < 1) { R_(c, x - 0.8, gy - 0.3, 1.6, 0.6, 'rgba(0,0,0,0.3)'); R_(c, x - 1, y - 1, 2, 2, '#6a6e78'); R_(c, x - 0.6, y - 0.8, 1, 1, '#a0a4ac'); }
      else { const q = (age - f.d) / 0.5; c.globalAlpha = 1 - q; for (let k = 0; k < 6; k++) R_(c, x + Math.cos(k * 1.05) * q * 4 - 0.5, gy + Math.sin(k * 1.05) * q * 2 - q * 3, 1, 1, k % 2 ? '#8a8272' : '#d8cfae'); c.globalAlpha = 1; }
    } else if (f.k === 'bash') {
      if (age < 0.45 && visibleAt(S, f.x, f.y)) { const q = age / 0.45, x = f.x * TS, y = f.y * TS; c.globalAlpha = 1 - q; for (let k = 0; k < 5; k++) R_(c, x + Math.cos(k * 1.3 + f.t * 3) * q * 3, y + Math.sin(k * 1.3 + f.t * 3) * q * 2 - q * 2, 0.7, 0.7, k % 2 ? '#b8b0a0' : '#8a5a2b'); R_(c, x - 1, y - 1, 2, 0.5, '#fff'); c.globalAlpha = 1; }
    } else if (f.k === 'rubble') {
      if (age < 25 && visibleAt(S, f.x, f.y)) { c.globalAlpha = Math.max(0, 1 - age / 25) * 0.9; const x = f.x * TS, y = f.y * TS; for (let k = 0; k < 7; k++) R_(c, x - 6 + hash(Math.floor(f.x), Math.floor(f.y), k) * 12, y - 3 + hash(Math.floor(f.x), Math.floor(f.y), k + 7) * 8, 1.6, 1.2, k % 2 ? '#8a8272' : '#6a6458'); c.globalAlpha = 1; }
    } else if (f.k === 'rip') { if (age < 9 && visibleAt(S, f.x, f.y)) { c.globalAlpha = Math.max(0, 1 - age / 9) * 0.85; const x = f.x * TS, y = f.y * TS; R_(c, x - 1.8, y - 0.5, 3.6, 1, '#3a1a1a'); R_(c, x - 1.5, y - 0.8, 3, 0.8, '#7a2a2a'); R_(c, x + 1.2, y - 1.2, 0.6, 0.6, '#d8d0c0'); c.globalAlpha = 1; }
    } else if (f.k === 'hit' && age < 0.32 && visibleAt(S, f.x, f.y)) {
      const q = age / 0.32, x = f.x * TS, y = f.y * TS - 2; c.globalAlpha = 1 - q; c.strokeStyle = '#fff'; c.lineWidth = 0.5; c.beginPath(); c.moveTo(x - 1.5, y - 1.5); c.lineTo(x + 1.5, y + 1.5); c.moveTo(x + 1.5, y - 1.5); c.lineTo(x - 1.5, y + 1.5); c.stroke();
      for (let k = 0; k < 4; k++) R_(c, x + Math.cos(k * 1.9 + f.t * 7) * q * 3.5, y + Math.sin(k * 1.9 + f.t * 7) * q * 2.5 - q, 0.6, 0.6, k % 2 ? '#ffc850' : '#fff'); c.globalAlpha = 1;
    }
  }
  // выбранный город/здание
  if (sel && (sel.k === 'city')) { const ct = S.cities[sel.id]; if (ct) selBox(c, ct.x * TS - 2, ct.y * TS - 2, 36, 36, tm); }
  if (sel && sel.k === 'bld') { const b = S.buildings.find(q => q.id === sel.id); if (b) selBox(c, b.x * TS - 1, b.y * TS - 1, 18, 18, tm); }
  // призрак постройки (для стен и рва — линия)
  if (pl && pl.x != null) {
    const col = S.players[0].col, line = (pl.type === 'wall' || pl.type === 'moat') && pl.x2 != null ? lineTiles(pl.x, pl.y, pl.x2, pl.y2) : [{ x: pl.x, y: pl.y }];
    for (const t of line) {
      const ok = !siteOk(S, 0, pl.type, t.x, t.y); c.globalAlpha = 0.8;
      if (pl.type === 'city') drawSpr(c, citySprite({ level: 1, isCap: false }, col), t.x * TS, t.y * TS - 14);
      else if (pl.type === 'moat') c.drawImage(moatSprite(15, 1), t.x * TS, t.y * TS, TS, TS);
      else drawSpr(c, bldSprite({ type: pl.type, res: S.res[t.y * S.W + t.x] }, col, STRUCT[pl.type] ? (pl.type === 'tower' ? 0 : (line.length > 1 ? (Math.abs(pl.x2 - pl.x) >= Math.abs(pl.y2 - pl.y) ? 10 : 5) : 10)) : 0), t.x * TS, t.y * TS);
      if (pl.type === 'mill') millBlades(c, t.x * TS + 8, t.y * TS + 4.5, 0.4, 0.5);
      c.globalAlpha = 1; c.strokeStyle = ok ? '#7dff7d' : '#ff5a4a'; c.lineWidth = 0.8; const sz = pl.type === 'city' ? 32 : 16; c.strokeRect(t.x * TS + 0.5, t.y * TS + 0.5, sz - 1, sz - 1);
      if (pl.type === 'city') { c.save(); c.globalAlpha = 0.12; c.fillStyle = '#fff'; c.beginPath(); c.ellipse((t.x + 1) * TS, (t.y + 1) * TS, 4 * TS, 4 * TS, 0, 0, 6.3); c.fill(); c.globalAlpha = 0.9; c.setLineDash([2, 2]); c.strokeStyle = '#fff'; c.lineWidth = 0.7; c.stroke(); c.restore(); }
    }
  }
  // облака
  const cw = S.W * TS, off = (tm * 2.5) % cw; c.drawImage(RN.cloud, off, 0, cw, S.H * TS); c.drawImage(RN.cloud, off - cw, 0, cw, S.H * TS);
  { // смена времени суток (плавно)
    const tn = todTint(todHours(S)), dark = Math.max(0, Math.min(1, (255 - tn[1]) / 150));
    { // тёплый тон + время суток. Приглушение красок вынесено в стили (filter у холста): раньше оно делалось здесь
      // отдельным полноэкранным проходом в каждом кадре и на слабых телефонах съедало большую часть времени кадра.
      // Тон слегка усилен: приглушение теперь накладывается после него.
      const r0 = tn[0] * 0.98, g0 = tn[1] * 0.95, b0 = tn[2] * 0.9, lum = 0.213 * r0 + 0.715 * g0 + 0.072 * b0, bo = v => Math.max(0, Math.min(255, lum + (v - lum) / 0.64)) | 0;
      c.save(); c.setTransform(1, 0, 0, 1, 0, 0); c.globalCompositeOperation = 'multiply'; c.fillStyle = `rgb(${bo(r0)},${bo(g0)},${bo(b0)})`; c.fillRect(0, 0, RN.cv.width, RN.cv.height); c.restore(); c.imageSmoothingEnabled = false;
    }
    if (dark > 0.12) { c.globalAlpha = Math.min(1, dark * 1.3);
      for (const ct of S.cities) { const ox = ct.x * TS, oy = ct.y * TS - 14; if (ct.x < vx0 - 3 || ct.x > vx1 || ct.y < vy0 - 3 || ct.y > vy1) continue; R_(c, ox + 14.5, oy + 27.5, 3, 3.5, '#ffd870'); const n = Math.min(6, 1 + ct.level); for (let i = 0; i < n; i++) { const h = CITY_HS[i]; R_(c, ox + (h[0] + 1.5) * 0.5, oy + (32 + h[1] + 6) * 0.5, 1, 1, '#ffd870'); R_(c, ox + (h[0] + 6.5) * 0.5, oy + (32 + h[1] + 6) * 0.5, 0.5, 1, '#ffb850'); } }
      for (const b of S.buildings) { if (b.x < vx0 - 1 || b.x > vx1 || b.y < vy0 - 1 || b.y > vy1) continue; if (b.type === 'forge') { c.globalAlpha = dark * 0.35; R_(c, b.x * TS - 2, b.y * TS + 3, 14, 12, '#ff9a3a'); c.globalAlpha = Math.min(1, dark * 1.3); } else if (b.type === 'barracks' || b.type === 'workshop' || b.type === 'stable' || b.type === 'port' || b.type === 'tavern' || b.type === 'mill') R_(c, b.x * TS + 7.5, b.y * TS + 9, 1, 1.5, '#ffd870'); }
      c.globalAlpha = 1; }
  }
  // значки армий (экранное пространство, постоянный размер)
  c.setTransform(RN.dpr, 0, 0, RN.dpr, 0, 0); c.imageSmoothingEnabled = false;
  for (const g of armyClusters(S)) {
    const m = markerPos(g, cam); if (m.x < -20 || m.tip < -30 || m.x > RN.w + 20 || m.tip > RN.h + 40) continue;
    const col = ownerCol(S, g.owner), selG = sel && sel.k === 'army' && g.list.some(q => q.id === sel.id), fight = g.list.some(q => q.fight), bounce = selG ? Math.abs(Math.sin(tm * 6)) * 3 : 0;
    c.globalAlpha = 0.5; R_(c, m.x - 6, m.tip + 1, 12, 2, '#000'); c.globalAlpha = 1;
    c.drawImage(markerCanvas(fight ? '#ff3a2a' : col), m.x - 11, m.tip - 26 - bounce, 22, 26);
    const nb = g.list.length > 1 ? String(g.list.length) : String(g.sq);
    c.save(); c.translate(m.x + 5, m.tip - 31 - bounce); c.fillStyle = '#14100c'; c.fillRect(-1, -1, numW(nb) * 2 + 4, 13); c.fillStyle = g.list.length > 1 ? '#f2c230' : '#fff'; c.scale(2, 2); drawNum(c, nb, 0.5, 0, g.list.length > 1 ? '#f2c230' : '#fff'); c.restore();
    if (selG) { c.strokeStyle = '#ffe27a'; c.lineWidth = 2; c.strokeRect(m.x - 12, m.tip - 27 - bounce, 24, 28); }
  }
  // подписи городов (экранное пространство)
  c.setTransform(RN.dpr, 0, 0, RN.dpr, 0, 0); c.font = '16px PixelRU, monospace'; c.textAlign = 'center'; c.textBaseline = 'middle';
  if (cam.z >= 2) for (const ct of S.cities) {
    const sx = (ct.x * TS + 16 - cam.x) * cam.z, sy = (ct.y * TS - 14 - cam.y) * cam.z; if (sx < -80 || sy < -30 || sx > RN.w + 80 || sy > RN.h + 30) continue;
    const nm = ct.name; c.lineWidth = 4; c.strokeStyle = '#14100c'; c.strokeText(nm, sx, sy); c.fillStyle = ct.owner < 0 ? '#d8d2c4' : '#fff'; c.fillText(nm, sx, sy);
    if (ct.isCap && ICON.star) c.drawImage(ICON.star, Math.round(sx + c.measureText(nm).width / 2 + 3), Math.round(sy - 10), 18, 18); // столица
  }
}
function armyClusters(S) {
  const cl = [], used = new Set();
  for (const a of S.armies) { if (a.ship || used.has(a.id) || !canSee(S, a)) continue; const g = { owner: a.owner, list: [a] }; used.add(a.id);
    for (const b of S.armies) if (!b.ship && !used.has(b.id) && b.owner === a.owner && canSee(S, b) && dist(a.x, a.y, b.x, b.y) < 0.9) { g.list.push(b); used.add(b.id); }
    g.x = g.list.reduce((s2, q) => s2 + q.x, 0) / g.list.length; g.y = g.list.reduce((s2, q) => s2 + q.y, 0) / g.list.length; g.sq = g.list.reduce((s2, q) => s2 + q.sq.length, 0); cl.push(g); }
  return cl;
}
const MK = {};
function markerCanvas(col) {
  if (MK[col]) return MK[col]; const rows = ['.KKKKKKKKK.', 'KtttttttttK', 'KTTTTTTTTTK', 'KTTWTTTWTTK', 'KTTTWTWTTTK', 'KTTTTWTTTTK', 'KTTTWTWTTTK', 'KTTWTTTWTTK', 'KTTTTTTTTTK', '.KTTTTTTTK.', '..KTTTTTK..', '...KTTTK...', '....KKK....'];
  const c = newCv(11, 13), x = c.getContext('2d'); rows.forEach((r, j) => { for (let i = 0; i < 11; i++) { const ch = r[i]; if (ch === '.') continue; x.fillStyle = ch === 'K' ? '#14100c' : ch === 'W' ? '#fff' : ch === 't' ? shadeC(col, 1.4) : col; x.fillRect(i, j, 1, 1); } }); return MK[col] = c;
}
function markerPos(g, cam) { return { x: (g.x * TS - cam.x) * cam.z, tip: (g.y * TS - 7 - cam.y) * cam.z - 2 }; }
function markerHit(S, tx, ty) { // тап по значку армии
  const cam = RN.cam; let best = null, bd = 1e9;
  for (const g of armyClusters(S)) { const m = markerPos(g, cam), cx = m.x, cy = m.tip - 14, px = (tx * TS - cam.x) * cam.z, py = (ty * TS - cam.y) * cam.z, d = Math.max(Math.abs(px - cx), Math.abs(py - cy)); if (d < 17 && d < bd) { bd = d; best = g; } }
  return best;
}
function animCity(c, ct, col, tm) {
  const ox = ct.x * TS, oy = ct.y * TS - 14, H = 0.5, sd = ct.id * 1.7;
  waveFlag(c, ox + 6 * H, oy + 32 * H, col, tm, sd); waveFlag(c, ox + 58 * H, oy + 32 * H, col, tm, sd + 1); if (!ct.isCap) waveFlag(c, ox + 32 * H, oy + 20 * H, col, tm, sd + 2);
  const n = Math.min(2, 1 + ct.level); for (let i = 0; i < n; i++) { const h = CITY_HS[i]; smoke(c, ox + (h[0] + 9) * H, oy + (32 + h[1] - 2) * H, tm, sd + i * 2.3, 0.7); }
}
const FIGC = {};
function figSprite(t, col, cls) { const k = t + col + (cls || ''); if (FIGC[k]) return FIGC[k]; if (t === 'cmd') { const c = newCv(20, 19); HF.cmd(c.getContext('2d'), 1, 4, col, cls); c._feet = 17; return FIGC[k] = c; } const big = t === 'cat' || t === 'ram', c = newCv(big ? 28 : 18, big ? 20 : 16); HF[t](c.getContext('2d'), big ? 3 : 1, big ? 3 : 1, col); c._feet = big ? 17 : 14; return FIGC[k] = c; }
function drawRoute(c, x0, y0, path, alpha, tm, col) {
  if (!path.length) return; const pts = [[x0 * TS, y0 * TS]]; for (const p of path) pts.push([(p.x + 0.5) * TS, (p.y + 0.5) * TS]);
  c.save(); c.globalAlpha = alpha; c.lineJoin = 'round'; c.lineCap = 'round';
  c.strokeStyle = 'rgba(0,0,0,0.55)'; c.lineWidth = 2.2; c.beginPath(); pts.forEach((p, i) => i ? c.lineTo(p[0], p[1]) : c.moveTo(p[0], p[1])); c.stroke();
  c.strokeStyle = col; c.lineWidth = 1.1; c.setLineDash([3, 2]); c.lineDashOffset = -tm * 8; c.beginPath(); pts.forEach((p, i) => i ? c.lineTo(p[0], p[1]) : c.moveTo(p[0], p[1])); c.stroke(); c.setLineDash([]);
  // шевроны вдоль пути и наконечник
  let acc = 0; for (let i = 1; i < pts.length; i++) { const dx = pts[i][0] - pts[i - 1][0], dy = pts[i][1] - pts[i - 1][1], l = Math.hypot(dx, dy); acc += l; if (acc >= 28 && i < pts.length - 1) { acc = 0; arrowHead(c, pts[i][0], pts[i][1], Math.atan2(dy, dx), 2.2, col); } }
  const n = pts.length, e = pts[n - 1], pr = pts[Math.max(0, n - 2)]; arrowHead(c, e[0], e[1], Math.atan2(e[1] - pr[1], e[0] - pr[0]), 4.2, col);
  c.restore();
}
function arrowHead(c, x, y, ang, sz, col) { c.save(); c.translate(x, y); c.rotate(ang); c.fillStyle = 'rgba(0,0,0,0.65)'; c.beginPath(); c.moveTo(sz + 0.9, 0); c.lineTo(-sz * 0.6 - 0.5, -sz * 0.9 - 0.7); c.lineTo(-sz * 0.6 - 0.5, sz * 0.9 + 0.7); c.closePath(); c.fill(); c.fillStyle = col; c.beginPath(); c.moveTo(sz, 0); c.lineTo(-sz * 0.6, -sz * 0.8); c.lineTo(-sz * 0.6, sz * 0.8); c.closePath(); c.fill(); c.restore(); }
function dust(c, mx, my, tm, seed) { for (let k = 0; k < 4; k++) { const ph = (tm * 1.4 + k / 4 + seed * 0.3) % 1, sz = 1 + ph * 2.2; c.globalAlpha = (1 - ph) * 0.55; R_(c, mx + Math.cos(k * 1.7 + seed) * ph * 4 - sz / 2, my + Math.sin(k * 1.7 + seed) * ph * 2 - sz / 2 - ph, sz, sz * 0.8, '#d8cfae'); } c.globalAlpha = 1; }
const TOD = [[0, 52, 62, 120], [4, 62, 72, 130], [6, 200, 150, 150], [7.5, 255, 226, 190], [9, 255, 255, 255], [17, 255, 255, 255], [18.5, 255, 205, 150], [20, 150, 120, 150], [21.5, 74, 84, 134], [24, 52, 62, 120]];
function todTint(h) { for (let i = 0; i < TOD.length - 1; i++) { const a = TOD[i], b = TOD[i + 1]; if (h >= a[0] && h <= b[0]) { let t = (h - a[0]) / (b[0] - a[0]); t = t * t * (3 - 2 * t); return [a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, a[3] + (b[3] - a[3]) * t]; } } return [255, 255, 255]; }
function selBox(c, x, y, w, h, tm) { c.strokeStyle = Math.floor(tm * 3) % 2 ? '#fff' : '#ffe27a'; c.lineWidth = 1; const l = 4; c.beginPath(); for (const [px, py, dx, dy] of [[x, y, 1, 1], [x + w, y, -1, 1], [x, y + h, 1, -1], [x + w, y + h, -1, -1]]) { c.moveTo(px, py + dy * l); c.lineTo(px, py); c.lineTo(px + dx * l, py); } c.stroke(); }

/* ===== Миникарта ===== */
function drawMinimap(S) {
  const mm = RN.minimap; if (!mm || !S || !RN.terrain) return; const c = mm.getContext('2d'), sz = mm.width; c.imageSmoothingEnabled = false;
  c.imageSmoothingEnabled = true; c.drawImage(TERR.overview(), 0, 0, sz, sz); c.imageSmoothingEnabled = false; if (RN.showTerr) c.drawImage(RN.terrCv, 0, 0, sz, sz); const k = sz / S.W;
  for (const ct of S.cities) R_(c, ct.x * k - 1, ct.y * k - 1, 4, 4, ownerCol(S, ct.owner));
  for (const a of S.armies) if (!a.ship && canSee(S, a)) R_(c, a.x * k - 1, a.y * k - 1, 3, 3, ownerCol(S, a.owner));
  c.strokeStyle = '#fff'; c.lineWidth = 1; c.strokeRect(RN.cam.x / TS * k, RN.cam.y / TS * k, RN.w / RN.cam.z / TS * k, RN.h / RN.cam.z / TS * k);
}

/* ===== Ввод ===== */
function initInput() {
  const el = RN.cv, ptrs = new Map(); let down = null, pinch = 0;
  const toWorld = (sx, sy) => ({ x: sx / RN.cam.z + RN.cam.x, y: sy / RN.cam.z + RN.cam.y });
  el.addEventListener('pointerdown', e => { try { el.setPointerCapture(e.pointerId); } catch (x) { /* указатель уже отпущен */ } ptrs.set(e.pointerId, { x: e.clientX, y: e.clientY }); if (ptrs.size === 1) down = { x: e.clientX, y: e.clientY, t: performance.now(), moved: 0 }; else { down = null; const [a, b] = [...ptrs.values()]; pinch = Math.hypot(a.x - b.x, a.y - b.y); } });
  el.addEventListener('pointermove', e => {
    const p = ptrs.get(e.pointerId); if (!p) return; const dx = e.clientX - p.x, dy = e.clientY - p.y; p.x = e.clientX; p.y = e.clientY;
    if (ptrs.size === 1) { if (down) { down.moved += Math.abs(dx) + Math.abs(dy); } RN.cam.x -= dx / RN.cam.z; RN.cam.y -= dy / RN.cam.z; clampCam(); }
    else if (ptrs.size === 2) { const [a, b] = [...ptrs.values()], d = Math.hypot(a.x - b.x, a.y - b.y), mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2; if (pinch) zoomAt(mx, my, d / pinch); pinch = d; }
  });
  // tap — нажатие засчитывается только при обычном отпускании пальца; прерванный системой жест (pointercancel) нажатием не считается
  const up = (e, tap) => {
    const had = ptrs.has(e.pointerId); ptrs.delete(e.pointerId); if (ptrs.size < 2) pinch = 0;
    if (tap && had && down && ptrs.size === 0 && down.moved < 10 && performance.now() - down.t < 450) { const w = toWorld(e.clientX, e.clientY); UI.mapTap(w.x / TS, w.y / TS); }
    if (ptrs.size === 0) down = null;
  };
  el.addEventListener('pointerup', e => up(e, true)); el.addEventListener('pointercancel', e => up(e, false));
  el.addEventListener('wheel', e => { e.preventDefault(); zoomAt(e.clientX, e.clientY, e.deltaY < 0 ? 1.15 : 0.87); }, { passive: false });
}
function zoomAt(sx, sy, f) { const cam = RN.cam, wx = sx / cam.z + cam.x, wy = sy / cam.z + cam.y; cam.z = Math.max(0.3, Math.min(7, cam.z * f)); cam.x = wx - sx / cam.z; cam.y = wy - sy / cam.z; clampCam(); }
