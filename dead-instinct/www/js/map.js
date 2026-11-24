'use strict';
/* =====================================================================
   DEAD INSTINCT — генерация города, домов, дверей, вода, поиск пути, обзор
   ===================================================================== */

const DIRS8 = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]];

/* ---------- шум ---------- */
function hash2(x, y, seed) {
  let h = x * 374761393 + y * 668265263 + seed * 144665;
  h = (h ^ (h >> 13)) * 1274126177;
  return ((h ^ (h >> 16)) >>> 0) / 4294967296;
}
function vnoise(x, y, seed) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  const a = hash2(xi, yi, seed), b = hash2(xi + 1, yi, seed), c = hash2(xi, yi + 1, seed), d = hash2(xi + 1, yi + 1, seed);
  return lerp(lerp(a, b, u), lerp(c, d, u), v);
}
function fbm(x, y, seed, oct) {
  let s = 0, a = 0.5, f = 1, norm = 0;
  for (let i = 0; i < oct; i++) { s += vnoise(x * f, y * f, seed + i * 77) * a; norm += a; a *= 0.5; f *= 2; }
  return s / norm;
}

/* ---------- карта ---------- */
function emptyMap(W, H) {
  const n = W * H;
  return {
    W, H,
    tiles: new Uint8Array(n).fill(T.GRASS),
    floor: new Uint8Array(n),           // подтип пола (0=нет)
    dark: new Uint8Array(n),            // затемнённая зона (внутри)
    rooms: [], doors: [], containers: [], props: [], lights: [],
    seen: new Uint8Array(n), vis: new Uint8Array(n), explored: 0,
    roads: [], rivers: [], buildings: [], parks: [],
    safehouse: null, exit: null, spawn: { x: 0, y: 0 }, pois: {},
    name: 'Город', kind: 'city', timeOfDay: 0.8, weather: 'rain'
  };
}

function idx(M, x, y) { return y * M.W + x; }
function inside(M, x, y) { return x >= 0 && y >= 0 && x < M.W && y < M.H; }
function tileAt(M, x, y) { return inside(M, x, y) ? M.tiles[y * M.W + x] : T.WALL; }
function setTile(M, x, y, t) { if (inside(M, x, y)) M.tiles[y * M.W + x] = t; }
function isSolid(M, x, y) { return !!SOLID[tileAt(M, x, y)]; }
function isWater(M, x, y) { const t = tileAt(M, x, y); return t === T.WATER || t === T.DEEP; }
function isSwamp(M, x, y) { return tileAt(M, x, y) === T.SWAMP; }
function isDark(M, x, y) { return inside(M, x, y) && M.dark[y * M.W + x] === 1; }
function doorAt(M, x, y) {
  for (const d of M.doors) if (d.x === x && d.y === y) return d;
  return null;
}
/* проходимость для человека/зомби: стены/деревья/машины нет, вода — можно но медленно */
function walkable(M, x, y, forZombie) {
  if (!inside(M, x, y)) return false;
  const t = M.tiles[y * M.W + x];
  if (SOLID[t]) return false;
  if (t === T.DEEP) return false;
  const d = doorAt(M, x, y);
  if (d) {
    if (d.locked || d.open < 0.6) return false;
  }
  return true;
}
function moveCost(M, x, y) {
  const t = tileAt(M, x, y);
  if (t === T.WATER) return 2.6;
  if (t === T.SWAMP) return 1.9;
  if (t === T.RUBBLE) return 1.6;
  if (t === T.TREE) return 1.45;
  if (t === T.FOREST) return 1.35;
  if (t === T.ROAD) return 0.92;
  if (t === T.FLOOR) return 1;
  return 1.05;
}

/* ---------- дома ---------- */
function addRoom(M, x, y, w, h, kind, name) {
  const r = { id: M.rooms.length, x, y, w, h, kind, name: name || '', doors: [], lights: [], containers: [], shelves: [] };
  M.rooms.push(r); M.buildings.push(r);
  for (let yy = y; yy < y + h; yy++) for (let xx = x; xx < x + w; xx++) {
    if (!inside(M, xx, yy)) continue;
    const i = yy * M.W + xx;
    const edge = xx === x || yy === y || xx === x + w - 1 || yy === y + h - 1;
    if (edge) { M.tiles[i] = T.WALL; M.dark[i] = 1; M.floor[i] = 0; }
    else { M.tiles[i] = T.FLOOR; M.floor[i] = kind === 'bunker' ? 4 : 1; M.dark[i] = 1; }
  }
  return r;
}
function addDoor(M, room, x, y, side) {
  const i = y * M.W + x;
  M.tiles[i] = T.FLOOR; M.floor[i] = 2; M.dark[i] = 1;
  const d = { x, y, room, side: side || 's', open: 1, locked: false, hp: 100, max: 100, broken: false, bang: 0 };
  room.doors.push(d); M.doors.push(d);
  return d;
}
function addContainer(M, room, x, y, kind, lootKind) {
  if (!inside(M, x, y)) return null;
  const c = { id: M.containers.length, x, y, kind, room, loot: null, lootKind: lootKind && lootKind !== 'room' ? lootKind : null, looted: 0, searched: 0 };
  M.containers.push(c); if (room) room.containers.push(c);
  return c;
}
function addLight(M, x, y, r, col, kind) {
  const l = { x, y, r, col: col || '#ffca7a', kind: kind || 'lamp', flicker: Math.random() };
  M.lights.push(l); return l;
}

/* ---------- генерация города ---------- */
function genCity(kind, seed, chapter) {
  const W = MW, H = MH, M = emptyMap(W, H);
  M.kind = kind; M.seed = seed;
  const rnd = mulberry32(seed);
  M.name = kind === 'industrial' ? 'Заводской район' : kind === 'hospital' ? 'Клинический городок' : 'Старый город';
  M.timeOfDay = kind === 'industrial' ? 0.15 : kind === 'hospital' ? 0.06 : 0.55;
  M.weather = kind === 'industrial' ? 'storm' : kind === 'hospital' ? 'fog' : 'rain';

  /* --- земля --- */
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const i = y * W + x;
    const n = fbm(x * 0.09, y * 0.09, seed, 3);
    const m = fbm(x * 0.15 + 11, y * 0.15 + 7, seed + 9, 2);
    if (n > 0.58) M.tiles[i] = T.DIRT;
    else if (m > 0.62 && n < 0.5) M.tiles[i] = T.SWAMP;
    else M.tiles[i] = T.GRASS;
  }

  /* --- река вдоль верхнего края --- */
  const riverY = 5 + Math.floor(rnd() * 4);
  let ry = riverY;
  for (let rx = 0; rx < W; rx += 2) {
    const wdt = 2 + (fbm(rx * 0.12, 3, seed + 5, 2) > 0.55 ? 1 : 0);
    for (let dy = 0; dy < wdt; dy++) for (let dx = 0; dx < 2; dx++) {
      const X = rx + dx, Y = Math.round(ry) + dy;
      if (inside(M, X, Y)) setTile(M, X, Y, dy === 0 ? T.WATER : T.DEEP);
    }
    ry += (fbm(rx * 0.1, 7, seed + 3, 2) - 0.5) * 1.4;
    ry = clamp(ry, 5, H * 0.3);
  }

  const rows = [];
  let by = Math.max(Math.round(riverY) + 7, 13);
  while (by < H - 16) {
    let bh = 14 + Math.floor(rnd() * 4);
    if (by + bh > H - 12) bh = H - 12 - by;
    if (bh < 9) break;
    rows.push([by, bh]);
    by += bh + 2;
  }
  const cols = [];
  let bx = 4;
  while (bx < W - 12) {
    let bw = 15 + Math.floor(rnd() * 4);
    if (bx + bw > W - 6) bw = W - 6 - bx;
    if (bw < 10) break;
    cols.push([bx, bw]);
    bx += bw + 2;
  }
  M.rows = rows; M.cols = cols;
  for (const [x0, bw] of cols) for (let y = 2; y < H - 2; y++) setTile(M, x0 - 1, y, T.ROAD);
  for (const [y0] of rows) for (let x = 2; x < W - 2; x++) setTile(M, x, y0 - 1, T.ROAD);
  for (let i = 0; i < 120; i++) {
    const x = 2 + (rnd() * (W - 4) | 0), y = 2 + (rnd() * (H - 4) | 0);
    if (tileAt(M, x, y) === T.ROAD && rnd() < 0.5) setTile(M, x, y, T.RUBBLE);
  }

  /* --- кварталы --- */
  const kinds = ['house', 'house', 'house', 'warehouse', 'office', 'shop'];
  const spec = [];
  const wanted = chapter || 1;
  if (wanted === 1) spec.push(['store', 'Магазин у дома'], ['med', 'Аптечный киоск']);
  if (wanted === 2) spec.push(['med', 'Аптека «Здоровье»'], ['store', 'Магазин «Ласточка»'], ['med', 'Пункт помощи']);
  if (wanted === 3) spec.push(['police', 'Оружейная'], ['warehouse', 'Склад'], ['med', 'Медпункт завода']);
  if (wanted === 4) spec.push(['store', 'Торговый центр'], ['med', 'Пункт помощи']);
  if (wanted === 5) spec.push(['med', 'Поликлиника №3'], ['med', 'Изолятор']);

  const used = [];
  const lotFree = (x, y, w, h) => {
    if (x < 2 || y < 2 || x + w >= W - 1 || y + h >= H - 1) return false;
    for (let yy = y; yy < y + h; yy++) for (let xx = x; xx < x + w; xx++) {
      if (!inside(M, xx, yy)) return false;
      const t = M.tiles[yy * W + xx];
      if (t === T.WALL || t === T.FLOOR || t === T.ROAD || t === T.RUBBLE || t === T.WATER || t === T.DEEP) return false;
    }
    for (const u of used) if (x < u.x + u.w && x + w > u.x && y < u.y + u.h && y + h > u.y) return false;
    return true;
  };

  const makeBuilding = (x, y, w, h, kind2, name) => {
    if (w < 5 || h < 4 || !lotFree(x, y, w, h)) return null;
    used.push({ x, y, w, h });
    const room = addRoom(M, x, y, w, h, kind2, name);
    const dx = x + 1 + Math.floor(rnd() * Math.max(1, w - 2));
    addDoor(M, room, dx, y + h - 1, 's');
    if (w > 6 && rnd() < 0.75) addDoor(M, room, x, y + 1 + Math.floor(rnd() * Math.max(1, h - 2)), 'w');
    else if (h > 5) addDoor(M, room, x + w - 1, y + 1 + Math.floor(rnd() * Math.max(1, h - 2)), 'e');
    for (let yy = y + 1; yy < y + h - 1; yy++) for (let xx = x + 1; xx < x + w - 1; xx++) {
      if (doorAt(M, xx, yy)) continue;
      const q = rnd();
      if (q < 0.07) setTile(M, xx, yy, T.BED);
      else if (q < 0.15) addContainer(M, room, xx, yy, 'shelf', 'room');
      else if (q < 0.18) M.props.push({ kind: 'crate', x: xx, y: yy });
      else if (q < 0.21) M.props.push({ kind: rnd() < 0.5 ? 'blood' : 'body', x: xx, y: yy });
    }
    if (rnd() < 0.75) addLight(M, x + (w >> 1), y + (h >> 1), 4.6, '#ffca7a', 'room');
    return room;
  };

  // делим квартал на участки
  function splitBlock(x0, y0, w0, h0, depth) {
    if (depth <= 0) return [[x0, y0, w0, h0]];
    const canV = w0 >= 13, canH = h0 >= 11;
    if (!canV && !canH) return [[x0, y0, w0, h0]];
    const vertical = canV && (!canH || w0 >= h0 || rnd() < 0.5);
    if (vertical) {
      const cut = Math.floor(w0 * (0.42 + rnd() * 0.16));
      return splitBlock(x0, y0, cut, h0, depth - 1).concat(splitBlock(x0 + cut + 1, y0, w0 - cut - 1, h0, depth - 1));
    }
    const cut = Math.floor(h0 * (0.42 + rnd() * 0.16));
    return splitBlock(x0, y0, w0, cut, depth - 1).concat(splitBlock(x0, y0 + cut + 1, w0, h0 - cut - 1, depth - 1));
  }

  const blocks = [];
  for (const [rx, rw] of cols) for (const [ry2, rh] of rows) {
    const x0 = rx, y0 = ry2, w0 = Math.min(rw, W - 3 - rx), h0 = Math.min(rh, H - 3 - ry2);
    if (w0 < 8 || h0 < 7) continue;
    blocks.push({ x: x0, y: y0, w: w0, h: h0 });
  }
  // один квартал оставляем под убежище с двором
  let shBlock = null, bestD = 1e9;
  for (const b of blocks) {
    const d = Math.hypot(b.x + b.w / 2 - W * 0.55, b.y + b.h / 2 - H * 0.7);
    if (d < bestD && b.w >= 12 && b.h >= 10) { bestD = d; shBlock = b; }
  }
  if (!shBlock) shBlock = blocks[0];
  const sh = shBlock ? makeSafehouseAt(M, rnd, shBlock) : makeSafehouse(M, rnd, wanted);
  M.safehouse = sh;

  const lotList = [];
  for (const b of blocks) {
    if (shBlock && b === shBlock) continue;
    const isPark = rnd() < 0.16;
    if (isPark) {
      M.parks.push({ x: b.x, y: b.y, w: b.w, h: b.h });
      for (let y = b.y; y < b.y + b.h; y++) for (let x = b.x; x < b.x + b.w; x++) {
        if (!inside(M, x, y) || isWater(M, x, y)) continue;
        const q = rnd();
        if (q < 0.24) setTile(M, x, y, T.TREE);
        else if (q < 0.31) setTile(M, x, y, T.SWAMP);
        else if (q < 0.34) setTile(M, x, y, T.RUBBLE);
        else if (q < 0.38) addContainer(M, null, x, y, 'car', 'car');
      }
      if (rnd() < 0.6) addLight(M, b.x + (b.w >> 1), b.y + (b.h >> 1), 8, '#9fd6b8', 'park');
      continue;
    }
    const lots = splitBlock(b.x + 1, b.y + 1, b.w - 2, b.h - 2, 2);
    for (const l of lots) lotList.push(l);
  }
  // спец-здания — первыми лотами у дорог
  for (const s2 of spec) {
    if (!lotList.length) break;
    // ищем лот подходящего размера
    let idx = lotList.findIndex(l => l[2] >= 7 && l[3] >= 6);
    if (idx < 0) idx = 0;
    const l = lotList.splice(idx, 1)[0];
    const r = makeBuilding(l[0], l[1], Math.max(5, l[2]), Math.max(4, l[3]), s2[0], s2[1]);
    if (r) M.pois[s2[0]] = M.pois[s2[0]] || { x: r.x + (r.w >> 1), y: r.y + (r.h >> 1), room: r.id, name: s2[1] };
  }
  for (const l of lotList) {
    const w = l[2], h = l[3];
    if (w < 5 || h < 4) continue;
    const kind2 = kinds[Math.floor(rnd() * kinds.length)];
    if (!makeBuilding(l[0], l[1], w, h, kind2, '')) {
      // не влезло — хлам
      for (let y = l[1]; y < l[1] + h; y++) for (let x = l[0]; x < l[0] + w; x++) {
        if (!inside(M, x, y)) continue;
        const q = rnd();
        if (q < 0.12) setTile(M, x, y, T.RUBBLE);
        else if (q < 0.16) addContainer(M, null, x, y, rnd() < 0.5 ? 'car' : 'bin', 'car');
      }
    }
  }
  // оставшиеся спец-здания
  while (spec.length) {
    const s3 = spec.shift();
    for (let t = 0; t < 260; t++) {
      const w = 7 + Math.floor(rnd() * 3), h = 6 + Math.floor(rnd() * 2);
      const x = 3 + Math.floor(rnd() * (W - w - 6)), y = 4 + Math.floor(rnd() * (H - h - 8));
      const r = makeBuilding(x, y, w, h, s3[0], s3[1]);
      if (r) { M.pois[s3[0]] = M.pois[s3[0]] || { x: r.x + (r.w >> 1), y: r.y + (r.h >> 1), room: r.id, name: s3[1] }; break; }
    }
  }
  // дворы между домами
  for (let y = 2; y < H - 2; y++) for (let x = 2; x < W - 2; x++) {
    const t = M.tiles[y * W + x];
    if (t !== T.GRASS && t !== T.DIRT) continue;
    const q = rnd();
    if (q < 0.012) setTile(M, x, y, T.TREE);
    else if (q < 0.03) setTile(M, x, y, T.RUBBLE);
    else if (q < 0.038) addContainer(M, null, x, y, 'car', 'car');
    else if (q < 0.042) addContainer(M, null, x, y, 'bin', 'bin');
  }

  /* --- убежище --- */
  // M.safehouse уже поставлен выше (makeSafehouseAt / makeSafehouse)
  // запасной выход: мост в правом верхнем углу
  const _ = M.props.find(p => p.kind === 'bridge');

  /* --- цель эпизода --- */
  const near = (key, fb) => M.pois[key] ? { x: M.pois[key].x, y: M.pois[key].y, kind: key } : fb;
  if (wanted === 5) M.exit = near('med', { x: W - 8, y: H - 8, kind: 'gate' });
  else if (wanted === 3) M.exit = near('warehouse', near('police', { x: 6, y: H - 8, kind: 'gate' }));
  else if (wanted === 2) M.exit = near('med', { x: W >> 1, y: H >> 1, kind: 'med' });
  else if (wanted === 4) M.exit = near('store', { x: W - 8, y: H - 8, kind: 'gate' });
  else M.exit = { x: W - 9, y: H - 9, kind: 'gate' };
  for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
    const X = M.exit.x + dx, Y = M.exit.y + dy;
    if (!inside(M, X, Y)) continue;
    const t = tileAt(M, X, Y);
    if (t === T.WALL || t === T.TREE || t === T.DEEP) setTile(M, X, Y, T.ROAD);
  }
  // выйти можно только с карты: marker exit — а «вернуться в убежище» отдельно
  M.spawn = { x: sh.x, y: sh.y + 1 };
  const sp = nearestFree(M, sh.x, sh.y + 2, 5);
  if (sp) M.spawn = sp;

  for (const [cx0] of cols) for (const [ry0] of rows) if (rnd() < 0.32) addLight(M, cx0 - 1, ry0 - 1, 6.2, '#ffd28a', 'street');
  // мост
  for (let y = riverY - 1; y < riverY + 5; y++) if (inside(M, 2, y) && isWater(M, 1, y)) setTile(M, 1, y, T.ROAD);

  finishMap(M);
  return M;
}

/* убежище в конкретном квартале: дом + двор, дверь с юга, генератор, радио */
function makeSafehouseAt(M, rnd, b) {
  const w = Math.min(b.w - 2, 10), h = Math.min(b.h - 2, 8);
  const x = b.x + 1 + ((b.w - 2 - w) >> 1), y = b.y + 1 + ((b.h - 2 - h) >> 1) - (b.h > h + 4 ? 1 : 0);
  const room = addRoom(M, x, y, w, h, 'safehouse', 'Убежище');
  room.safe = true;
  const door = addDoor(M, room, x + (w >> 1), y + h - 1, 's');
  door.hp = door.max = 300;
  door.locked = false;
  // окно-«бойница»: одна стена прозрачна
  room.windows = [[x + 1, y], [x + w - 2, y]];
  setTile(M, x + 1, y + 1, T.BED);
  setTile(M, x + 2, y + 1, T.BED);
  addContainer(M, room, x + w - 2, y + 1, 'cabinet', 'safe');
  addContainer(M, room, x + w - 2, y + 2, 'shelf', 'room');
  addContainer(M, room, x + 1, y + h - 2, 'bin', 'safe');
  M.props.push({ kind: 'generator', x: x + w - 2, y: y + h - 2, room: room.id, fuel: 0, on: false });
  M.props.push({ kind: 'radio', x: x + 3, y: y + h - 2, room: room.id });
  addLight(M, x + (w >> 1), y + (h >> 1), 5.6, '#ffcf8a', 'safe');
  // двор перед домом
  for (let yy = y + h; yy < b.y + b.h; yy++) for (let xx = b.x; xx < b.x + b.w; xx++) {
    if (!inside(M, xx, yy)) continue;
    const q = rnd();
    if (q < 0.08) setTile(M, xx, yy, T.RUBBLE);
    else if (q < 0.12) setTile(M, xx, yy, T.TREE);
  }
  return { x: x + (w >> 1), y: y + (h >> 1), room: room.id, x0: x, y0: y, w, h, door };
}

function makeSafehouse(M, rnd, chapter) {
  const W = M.W, H = M.H;
  // ищем свободное место в нижней части карты
  for (let attempt = 0; attempt < 400; attempt++) {
    const w = 9 + Math.floor(rnd() * 3), h = 7 + Math.floor(rnd() * 2);
    const x = 6 + Math.floor(rnd() * (W - w - 12)), y = Math.floor(H * 0.55) + Math.floor(rnd() * Math.max(1, H * 0.38 - h));
    let ok = true;
    for (let yy = y - 1; yy <= y + h && ok; yy++) for (let xx = x - 1; xx <= x + w; xx++) {
      if (!inside(M, xx, yy)) { ok = false; break; }
      const t = M.tiles[yy * W + xx];
      if (t === T.WALL || t === T.FLOOR || t === T.ROAD || t === T.TREE || t === T.WATER || t === T.DEEP) { ok = false; break; }
    }
    if (!ok) continue;
    const room = addRoom(M, x, y, w, h, 'safehouse', 'Убежище');
    room.safe = true;
    // дверь с юга + окно
    const door = addDoor(M, room, x + (w >> 1), y + h - 1, 's');
    door.hp = door.max = 260;
    // кровать и ящики
    setTile(M, x + 1, y + 1, T.BED);
    setTile(M, x + 2, y + 1, T.BED);
    addContainer(M, room, x + w - 2, y + 1, 'cabinet', 'safe');
    addContainer(M, room, x + w - 2, y + 2, 'cabinet', 'safe');
    addContainer(M, room, x + 1, y + h - 2, 'bin', 'safe');
    // генератор (объект) и радио
    M.props.push({ kind: 'generator', x: x + w - 2, y: y + h - 2, room: room.id, fuel: 0, on: false });
    M.props.push({ kind: 'radio', x: x + 3, y: y + h - 2, room: room.id });
    addLight(M, x + (w >> 1), y + (h >> 1), 5.4, '#ffcf8a', 'safe');
    room.name = 'Убежище';
    return { x: x + (w >> 1), y: y + (h >> 1), room: room.id, x0: x, y0: y, w, h, door };
  }
  // запасной вариант: строим в любом месте
  const w = 9, h = 7, x = 8, y = H - 14;
  const room = addRoom(M, x, y, w, h, 'safehouse', 'Убежище');
  room.safe = true;
  const door = addDoor(M, room, x + (w >> 1), y + h - 1, 's');
  door.hp = door.max = 260;
  setTile(M, x + 1, y + 1, T.BED);
  addContainer(M, room, x + w - 2, y + 1, 'cabinet', 'safe');
  M.props.push({ kind: 'generator', x: x + w - 2, y: y + h - 2, room: room.id, fuel: 0, on: false });
  M.props.push({ kind: 'radio', x: x + 3, y: y + h - 2, room: room.id });
  addLight(M, x + (w >> 1), y + (h >> 1), 5.4, '#ffcf8a', 'safe');
  return { x: x + (w >> 1), y: y + (h >> 1), room: room.id, x0: x, y0: y, w, h, door };
}

/* --- расстановка лута в контейнерах --- */
function rollLoot(rnd, kind, chapter, gut) {
  const out = [];
  let table = kind === 'med' ? LOOT_MED : kind === 'police' ? LOOT_ARM : kind === 'food' ? LOOT_FOOD : LOOT_TABLE;
  if (kind === 'medkit' || kind === 'journal' || kind === 'fuel') table = STORY_LOOT[kind] || LOOT_TABLE;
  const n = (kind === 'medkit' || kind === 'journal') ? 1 : kind === 'safe' ? 2 : (kind === 'car' || kind === 'bin') ? (rnd() < 0.5 ? 1 : 0) : 1 + (rnd() < 0.55 ? 1 : 0);
  for (let i = 0; i < n; i++) {
    const k = pickWeighted(rnd, table);
    const rare = kind === 'med' || kind === 'police';
    let count = 1;
    if (['ammo9', 'ammo12', 'ammo762', 'food', 'water', 'bandage'].includes(k)) count = 1 + Math.floor(rnd() * (rare ? 4 : 3));
    out.push({ k, n: count });
  }
  return out;
}

/* --- финальная обработка карты --- */
function finishMap(M) {
  const W = M.W, H = M.H;
  // контейнеры получают лут
  const rnd = mulberry32((M.seed || 1) + 4242);
  for (const c of M.containers) {
    if (!c.loot) {
      let kind = c.lootKind || 'room';
      if (c.kind === 'car') kind = 'car'; else if (c.kind === 'bin') kind = 'bin';
      if (c.room && c.room.kind === 'med') kind = 'med';
      if (c.room && c.room.kind === 'police') kind = 'police';
      c.lootKind = kind;
      c.loot = rollLoot(rnd, kind, 1, 0);
    }
  }
  // мусор: мертвецы на улицах (декор) + кровь
  for (let i = 0; i < 90; i++) {
    const x = 3 + Math.floor(rnd() * (W - 6)), y = 3 + Math.floor(rnd() * (H - 6));
    const t = M.tiles[y * W + x];
    if (t === T.ROAD || t === T.GRASS || t === T.DIRT || t === T.RUBBLE) {
      M.props.push({ kind: rnd() < 0.45 ? 'body' : 'blood', x, y });
    }
  }
  M.explored = 0;
  M.seen.fill(0);
  return M;
}

/* ---------- проходимость и поиск пути ---------- */
class MinHeap {
  constructor() { this.a = []; }
  push(node) {
    const a = this.a; a.push(node); let i = a.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (a[p].f <= a[i].f) break;
      const t = a[p]; a[p] = a[i]; a[i] = t; i = p;
    }
  }
  pop() {
    const a = this.a; const top = a[0]; const last = a.pop();
    if (a.length) {
      a[0] = last; let i = 0;
      for (;;) {
        const l = i * 2 + 1, r = l + 1; let s = i;
        if (l < a.length && a[l].f < a[s].f) s = l;
        if (r < a.length && a[r].f < a[s].f) s = r;
        if (s === i) break;
        const t = a[s]; a[s] = a[i]; a[i] = t; i = s;
      }
    }
    return top;
  }
  get size() { return this.a.length; }
}

const NavCache = {
  M: null, g: null, closed: null, stamp: 0, gen: 0,
  reset(M) { this.M = M; this.g = new Float32Array(M.W * M.H); this.closed = new Uint32Array(M.W * M.H); this.stamp = 0; this.gen = 0; },
  step() { this.stamp++; if (this.stamp > 4000000000) { this.closed.fill(0); this.stamp = 1; } }
};

/* A*: от (sx,sy) к (tx,ty). Возвращает массив точек или null. zc=true — зомби (боятся только стен). */
function findPath(M, sx, sy, tx, ty, maxNodes, zt) {
  sx |= 0; sy |= 0; tx |= 0; ty |= 0;
  if (!inside(M, sx, sy) || !inside(M, tx, ty)) return null;
  if (sx === tx && sy === ty) return [{ x: tx, y: ty }];
  if (!NavCache.M || NavCache.M !== M) NavCache.reset(M);
  NavCache.step();
  const W = M.W, H = M.H, nc = NavCache, g = nc.g, closed = nc.closed, stamp = nc.stamp;
  const start = sy * W + sx, goal = ty * W + tx;
  const heap = new MinHeap();
  const h = (i) => { const x = i % W, y = (i / W) | 0; const dx = Math.abs(x - tx), dy = Math.abs(y - ty); return (dx + dy) + (Math.SQRT2 - 2) * Math.min(dx, dy); };
  g[start] = 0; closed[start] = 0;
  heap.push({ i: start, f: h(start), d: 0 });
  const from = nc.from || (nc.from = new Int32Array(W * H));
  let nodes = 0, found = false;
  while (heap.size && nodes < (maxNodes || 2600)) {
    const cur = heap.pop();
    if (cur.d !== g[cur.i] && closed[cur.i] === stamp) continue;
    if (closed[cur.i] === stamp) continue;
    closed[cur.i] = stamp; nodes++;
    if (cur.i === goal) { found = true; break; }
    const cx = cur.i % W, cy = (cur.i / W) | 0;
    for (let k = 0; k < 8; k++) {
      const dx = DIRS8[k][0], dy = DIRS8[k][1], nx = cx + dx, ny = cy + dy;
      if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
      const ni = ny * W + nx;
      if (closed[ni] === stamp) continue;
      if (!zt && SOLID[M.tiles[ni]]) continue;
      if (zt && SOLID[M.tiles[ni]]) continue;
      if (M.tiles[ni] === T.DEEP) continue;
      const d = doorAt(M, nx, ny);
      if (d && (d.locked || d.open < 0.6)) continue;
      if (dx && dy) { // не срезать углы
        const a = cy * W + nx, b = ny * W + cx;
        if (SOLID[M.tiles[a]] || SOLID[M.tiles[b]]) continue;
        const d2 = doorAt(M, nx, cy);
        if (d2 && (d2.locked || d2.open < 0.6)) continue;
        const d3 = doorAt(M, cx, ny);
        if (d3 && (d3.locked || d3.open < 0.6)) continue;
      }
      if (M.tiles[ni] === T.RUBBLE && zt && Math.random() < 0.35) continue;
      const ng = g[cur.i] + (dx && dy ? 1.4142 : 1) * moveCost(M, nx, ny) * (zt ? 0.9 : 1);
      if (closed[ni] !== stamp || ng < g[ni]) {
        g[ni] = ng; from[ni] = cur.i;
        heap.push({ i: ni, f: ng + h(ni), d: ng });
      }
    }
  }
  if (!found) {
    // цель рядом, но недостижима (например, за закрытой дверью) — ведём к ближайшей точке
    return null;
  }
  const path = []; let i = goal;
  let guard = 0;
  while (i !== start && guard++ < 5000) { path.push({ x: i % W, y: (i / W) | 0 }); i = from[i]; }
  path.reverse();
  return path;
}

/* линия видимости: false, если мешает стена/лес/машина */
function lineOfSight(M, x0, y0, x1, y1) {
  x0 |= 0; y0 |= 0; x1 |= 0; y1 |= 0;
  let dx = Math.abs(x1 - x0), dy = Math.abs(y1 - y0);
  let sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
  let x = x0, y = y0, guard = 0;
  while (guard++ < 200) {
    if (x === x1 && y === y1) return true;
    const e2 = 2 * err;
    if (e2 > -dy) { err -= dy; x += sx; }
    if (e2 < dx) { err += dx; y += sy; }
    if (x === x1 && y === y1) return true;
    const t = tileAt(M, x, y);
    if (t === T.WALL || t === T.TREE || t === T.CAR) return false;
  }
  return true;
}

/* ближайшая свободная точка */
function nearestFree(M, x, y, maxR) {
  x |= 0; y |= 0;
  if (walkable(M, x, y)) return { x, y };
  for (let r = 1; r <= (maxR || 8); r++) {
    for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const nx = x + dx, ny = y + dy;
      if (walkable(M, nx, ny)) return { x: nx, y: ny };
    }
  }
  return null;
}

/* комната по точке */
function roomAt(M, x, y) {
  for (const r of M.rooms) if (x >= r.x && x < r.x + r.w && y >= r.y && y < r.y + r.h) return r;
  return null;
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { genCity, findPath, lineOfSight, walkable, isSolid, tileAt, inside, roomAt, nearestFree, fbm };
}
