'use strict';
/* ===== Ядро игры Kingdom Crash (версия 2.0) ===== */
const dist = (ax, ay, bx, by) => Math.hypot(ax - bx, ay - by);
function canAfford(p, c) { for (const k in c) if ((p.stock[k] || 0) < c[k]) return false; return true; }
function pay(p, c) { for (const k in c) p.stock[k] -= c[k]; }
function costText(c) { return Object.keys(c).map(k => c[k] + ' ' + RNAME[k].toLowerCase()).join(', '); }
const SIZES = { s: 64, m: 96, l: 128 };

// проверка и нормализация настроек партии (в том числе из сохранения)
function sanitizeOpts(o) {
  o = Object.assign({}, o || {});
  o.sandbox = !!o.sandbox;
  if (!MAPTYPES.includes(o.type)) o.type = 'continent';
  if (!SIZES[o.size]) o.size = 'm';
  o.bots = Math.max(o.sandbox ? 0 : 1, Math.min(6, o.bots | 0));
  if (!DIFF[o.diff] && o.diff !== 'mixed') o.diff = 'normal';
  if (o.win !== 'caps' && o.win !== 'score') o.win = 'score';
  o.target = Math.max(1000, Math.min(1000000, Math.floor(+o.target) || 30000));
  o.nation = Math.max(0, Math.min(PNAMES.length - 1, o.nation | 0));
  o.free = o.free === undefined ? 1 : Math.max(0, Math.min(2, o.free | 0));
  o.events = o.events === undefined ? 1 : (o.events ? 1 : 0);
  if (o.seed !== undefined) { o.seed = Math.floor(+o.seed) % 1000000000; if (!(o.seed > 0)) delete o.seed; }
  return o;
}
function shuffle(a, rnd) { for (let i = a.length - 1; i > 0; i--) { const j = (rnd() * (i + 1)) | 0, t = a[i]; a[i] = a[j]; a[j] = t; } return a; }
function landFrac(S, x, y, r) { let land = 0, tot = 0; for (let yy = -r; yy <= r; yy++) for (let xx = -r; xx <= r; xx++) { const X = x + xx, Y = y + yy; if (X < 0 || Y < 0 || X >= S.W || Y >= S.H) continue; tot++; if (!isWater(S.ter[Y * S.W + X])) land++; } return tot ? land / tot : 0; }
// места для столиц: ровная земля, вокруг суша, столицы далеко друг от друга
function pickCapitals(S, n, rnd) {
  const W = S.W, H = S.H;
  const flat = (x, y) => { for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const i = (y + dy) * W + x + dx, t = S.ter[i]; if ((t !== TR.GRASS && t !== TR.PLAIN) || S.res[i]) return false; } return true; };
  for (const frac of [0.72, 0.6, 0.5]) {
    const cand = [], seen = new Set();
    for (let k = 0; k < 6000 && cand.length < 500; k++) {
      const x = 6 + ((rnd() * (W - 14)) | 0), y = 6 + ((rnd() * (H - 14)) | 0), key = y * W + x;
      if (seen.has(key)) continue; seen.add(key);
      if (flat(x, y) && landFrac(S, x, y, 6) > frac) cand.push({ x, y });
    }
    if (cand.length < n) continue;
    let best = null, bestD = -1;
    for (let att = 0; att < 5; att++) {
      const ch = [cand[(rnd() * cand.length) | 0]]; let minD = 1e9;
      while (ch.length < n) {
        let b = null, bd = -1;
        for (const c of cand) { let md = 1e9; for (const q of ch) md = Math.min(md, dist(c.x, c.y, q.x, q.y)); if (md > bd) { bd = md; b = c; } }
        ch.push(b); minD = Math.min(minD, bd);
      }
      if (minD > bestD) { bestD = minD; best = ch; }
    }
    if (best && (n === 1 || bestD >= CAP_MIN)) return best;
  }
  return null;
}
// вольные города: никому не принадлежат, их можно захватить без объявления войны
function placeFreeCities(S, lvl, rnd, names) {
  const W = S.W, H = S.H, want = Math.round((W / 32 + 1) * (lvl > 1 ? 2 : 1)), cand = [];
  for (let k = 0; k < 5000 && cand.length < 400; k++) {
    const x = 4 + ((rnd() * (W - 10)) | 0), y = 4 + ((rnd() * (H - 10)) | 0);
    if (!citySpot(S, x, y)) continue; let ok = true;
    for (let dy = 0; dy < 2 && ok; dy++) for (let dx = 0; dx < 2; dx++) { const t = S.ter[(y + dy) * W + x + dx]; if (t !== TR.GRASS && t !== TR.PLAIN && t !== TR.SAND) { ok = false; break; } }
    if (ok && landFrac(S, x, y, 4) > 0.55) cand.push({ x, y });
  }
  for (let i = 0; i < want; i++) {
    const c = cand.find(q => citySpot(S, q.x, q.y) && S.cities.every(z => dist(q.x, q.y, z.x, z.y) >= CITY_MIN + 3)); if (!c) break;
    const ct = addCity(S, c.x, c.y, -1, names.length ? names.pop() : 'Вольный город', false); ct.radius = 4; ct.level = rnd() < 0.3 ? 2 : 1; ct.free = true;
  }
}
function newGame(o, tries) {
  o = sanitizeOpts(o); tries = tries || 0;
  const seed = o.seed || ((((Math.random() * 1e9) ^ (Date.now() & 0x7fffffff)) >>> 0) % 999999999) + 1;
  const W = SIZES[o.size], H = W, rnd = mulberry32(seed + 99);
  // «Случайная» карта: тип выбирается из семени — каждый новый мир неповторим
  const type = o.type === 'random' ? MAPTYPES[(rnd() * 4) | 0] : o.type;
  const map = genMap(W, H, type, seed);
  const S = { v: 2, opts: o, seed, W, H, ter: map.ter, res: map.res, comp: map.comp, occ: new Int32Array(W * H).fill(-1), terr: new Int8Array(W * H).fill(-2),
    players: [], cities: [], buildings: [], bmap: new Map(), mm: new Uint8Array(W * H), structs: [], armies: [], ships: [], msgs: [], rel: [], time: 0, nid: 1, over: null, dirtyT: true, fx: [], tk: 0, rnd: rnd, toast: [], plan: 0, sv: 0, pending: [], prompts: [], pend: [],
    folk: [], extra: new Map(), sanctions: [], fT: 0, eT: 0 };
  const n = 1 + o.bots, caps = pickCapitals(S, n, rnd);
  if (!caps) { // на этой карте столицы не разместить: другая карта, а в крайнем случае — материк
    if (tries >= 14) throw new Error('Не удалось создать карту');
    return newGame(Object.assign({}, o, { seed: (seed + 7919) % 999999999 + 1, type: tries >= 7 ? 'continent' : o.type }), tries + 1);
  }
  const others = []; for (let i = 0; i < PNAMES.length; i++) if (i !== o.nation) others.push(i);
  const order = [o.nation].concat(shuffle(others, rnd));
  for (let i = 0; i < n; i++) {
    const st = {}; STOCK_KEYS.forEach(k => st[k] = 0); Object.assign(st, { gold: 450, wood: 320, food: 260, iron: 120 });
    const diff = i === 0 ? 'normal' : (o.diff === 'mixed' ? ['easy', 'normal', 'hard'][(rnd() * 3) | 0] : o.diff);
    S.players.push({ id: i, name: PNAMES[order[i]], col: PCOL[order[i]], ci: order[i], perk: PERKS[order[i]].id, human: i === 0, diff, mult: DIFF[diff].mult, stock: st, rate: { gold: 0, wood: 0, food: 0, iron: 0 },
      earned: 0, kills: 0, wins: 0, captured: 0, alive: true, ai: { next: 3 + i, fails: {}, failsAt: 0 }, flags: {}, eco: 0, mil: 0, score: 0, lastFood: -999, lastGold: -999, evAt: 150 + rnd() * 200 });
    S.rel[i] = [];
  }
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) { const r = { st: 'peace', op: ((rnd() * 40) | 0) - 20, truceUntil: 0, trade: false, warSince: 0, ally: false, cl: {}, pass: {}, cd: {} }; S.rel[i][j] = r; S.rel[j][i] = r; }
  const cn = shuffle(CITY_NAMES.slice(), rnd);
  caps.forEach((c, i) => { ensureNearby(S, c.x + 1, c.y + 1, rnd); addCity(S, c.x, c.y, i, cn.pop(), true).radius = 6; });
  if (o.free) placeFreeCities(S, o.free, rnd, cn);
  S.comp = labelComps(W, H, S.ter); compSizes(S); // рельеф у столиц менялся — связность считаем заново
  recomputeTerr(S); calcVision(S);
  return S;
}
// размеры связных областей (моря, острова): чтобы отличать море от пруда
function compSizes(S) { let mx = 0; for (let i = 0; i < S.comp.length; i++) if (S.comp[i] > mx) mx = S.comp[i]; const n = new Int32Array(mx + 1); for (let i = 0; i < S.comp.length; i++) n[S.comp[i]]++; S.csz = n; }
function compSize(S, id) { if (!S.csz) compSizes(S); return S.csz[id] || 0; }
function nearSea(S, x, y) {
  for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= S.W || ny >= S.H) continue; const i = ny * S.W + nx; if (isWater(S.ter[i]) && compSize(S, S.comp[i]) >= SEA_MIN) return true; }
  return false;
}
// ближайшая клетка воды: из заданного водоёма (comp) либо из любого достаточно большого
function nearestWater(S, x, y, maxR, comp) {
  x = Math.floor(x); y = Math.floor(y);
  for (let r = 0; r <= maxR; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
    if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue; const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= S.W || ny >= S.H) continue;
    const i = ny * S.W + nx; if (!isWater(S.ter[i])) continue;
    if (comp != null ? S.comp[i] !== comp : compSize(S, S.comp[i]) < SEA_MIN) continue;
    return { x: nx, y: ny };
  }
  return null;
}
const landComp = (S, x, y) => { if (!(x >= 0 && y >= 0 && x < S.W && y < S.H)) return null; const t = S.ter[y * S.W + x]; return isWater(t) || t === TR.MOUNT ? null : S.comp[y * S.W + x]; };
// свободная клетка суши рядом с точкой: без укреплений, по возможности без построек
function freeTileNear(S, x, y, maxR, comp) {
  x = Math.floor(x); y = Math.floor(y); let fb = null;
  for (let r = 0; r <= (maxR || 4); r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
    if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue; const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= S.W || ny >= S.H) continue;
    const i = ny * S.W + nx, t = S.ter[i]; if (isWater(t) || t === TR.MOUNT || (comp != null && S.comp[i] !== comp) || structAtTile(S, nx, ny)) continue;
    if (S.occ[i] < 0) return { x: nx, y: ny }; if (!fb) fb = { x: nx, y: ny };
  }
  return fb;
}
// клетки, до которых можно дойти пешком от (x0, y0), не проходя сквозь стены и закрытые границы
function reachTiles(S, owner, x0, y0, need, maxD) {
  const W = S.W, H = S.H, s = y0 * W + x0, seen = new Set([s]), out = [], open = !blockedFor(S, owner, x0, y0); let fr = [s];
  for (let d = 0; d < (maxD || 4) && fr.length && out.length < need; d++) {
    const nf = [];
    for (const c of fr) { const cx = c % W, cy = (c / W) | 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= W || y >= H) continue; const i = y * W + x; if (seen.has(i)) continue; seen.add(i);
        const t = S.ter[i]; if (isWater(t) || t === TR.MOUNT || structBlockedFor(S, owner, x, y) || (open && blockedFor(S, owner, x, y))) continue;
        nf.push(i); out.push({ x, y });
      } }
    fr = nf;
  }
  return out;
}
function citySpot(S, x, y) {
  if (x < 1 || y < 1 || x + 2 > S.W - 1 || y + 2 > S.H - 1) return false;
  for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const i = (y + dy) * S.W + x + dx, t = S.ter[i]; if (isWater(t) || t === TR.MOUNT || S.occ[i] >= 0 || S.res[i]) return false; }
  return true;
}
function ensureNearby(S, cx, cy, rnd) { // гарантируем минимум леса и руды в стартовых границах
  const W = S.W, H = S.H, R = 5.6;
  const free = (x, y) => x > 1 && y > 1 && x < W - 2 && y < H - 2 && !isWater(S.ter[y * W + x]) && S.occ[y * W + x] < 0 && Math.hypot(x + 0.5 - cx, y + 0.5 - cy) > 2.6;
  const count = () => { let f = 0, ir = 0, au = 0; for (let y = Math.floor(cy - R - 1); y <= cy + R + 1; y++) for (let x = Math.floor(cx - R - 1); x <= cx + R + 1; x++) { if (x < 0 || y < 0 || x >= W || y >= H || Math.hypot(x + 0.5 - cx, y + 0.5 - cy) > R) continue; const i = y * W + x; if (S.ter[i] === TR.FOREST) f++; if (S.res[i] === RES.IRON) ir++; if (S.res[i] === RES.GOLD) au++; } return { f, ir, au }; };
  let k = count();
  for (let t = 0; t < 400 && (k.ir < 3 || k.au < 2); t++) {
    const a = rnd() * 6.283, r = 3.3 + rnd() * 2.2, x = Math.round(cx + Math.cos(a) * r - 0.5), y = Math.round(cy + Math.sin(a) * r - 0.5); if (!free(x, y) || Math.hypot(x + 0.5 - cx, y + 0.5 - cy) > R - 0.2) continue; const i = y * W + x;
    if (S.res[i]) continue; S.ter[i] = TR.HILL; S.res[i] = k.ir < 3 ? RES.IRON : RES.GOLD; if (S.res[i] === RES.IRON) k.ir++; else k.au++;
    for (const [dx, dy] of [[1, 0], [0, 1], [-1, 0], [0, -1]]) { const nx = x + dx, ny = y + dy; if (free(nx, ny) && (S.ter[ny * W + nx] === TR.GRASS || S.ter[ny * W + nx] === TR.PLAIN) && rnd() < 0.6) S.ter[ny * W + nx] = TR.HILL; }
  }
  for (let t = 0; t < 300 && k.f < 14; t++) {
    const a = rnd() * 6.283, r = 3.2 + rnd() * 2.4, bx = Math.round(cx + Math.cos(a) * r), by = Math.round(cy + Math.sin(a) * r);
    for (let j = 0; j < 8; j++) { const x = bx + Math.round((rnd() - 0.5) * 3.4), y = by + Math.round((rnd() - 0.5) * 3.4); if (!free(x, y) || Math.hypot(x + 0.5 - cx, y + 0.5 - cy) > R) continue; const i = y * W + x; if (S.ter[i] === TR.GRASS || S.ter[i] === TR.PLAIN || S.ter[i] === TR.SAND) { S.ter[i] = TR.FOREST; k.f++; } }
  }
}
function addCity(S, x, y, owner, name, cap) {
  const c = { id: S.cities.length, name, x, y, owner, level: 1, radius: 3, rr: 0, cap: null, isCap: !!cap, home: owner };
  S.cities.push(c); for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) S.occ[(y + dy) * S.W + x + dx] = 1000000 + c.id; return c;
}
function garrison(c) { return c.owner < 0 ? 60 + 40 * (c.level - 1) : 30 * c.level + (c.isCap ? 40 : 0); }
function cityMaxR(c) { return c.owner < 0 ? 4 : 4 + c.level * 3 + (c.isCap ? 2 : 0); }

/* ===== Территория ===== */
function recomputeTerr(S) {
  const W = S.W, H = S.H, t = S.terr; t.fill(-2); const best = new Float32Array(W * H).fill(9);
  for (const c of S.cities) {
    const cx = c.x + 1, cy = c.y + 1, r = Math.ceil(c.radius);
    for (let y = Math.max(0, cy - r - 1); y < Math.min(H, cy + r + 1); y++) for (let x = Math.max(0, cx - r - 1); x < Math.min(W, cx + r + 1); x++) {
      const d = Math.hypot(x + 0.5 - cx, y + 0.5 - cy); if (d > c.radius) continue; const ra = d / c.radius, i = y * W + x;
      if (ra < best[i]) { best[i] = ra; t[i] = c.owner; }
    }
  }
  for (const b of S.buildings) { const o = t[b.y * W + b.x]; if (o !== -2 && o !== b.owner) { b.owner = o; b.busy = false; b.queue = []; b.gar = null; b.cd = 0; } }
  if (S.extra) for (const [i, o] of S.extra) { if (t[i] === -2) t[i] = o; else if (t[i] !== o) S.extra.delete(i); } // ручные захваты; устаревшие — очищаем
  S.dirtyT = true; S.sv = (S.sv || 0) + 1;
}
function bidx(S, id) { return S.bmap.get(id); }

/* ===== Строительство ===== */
function siteOk(S, pid, type, x, y) {
  if (!(x >= 0 && y >= 0 && x < S.W && y < S.H)) return 'Вне карты';
  if (type === 'city') return cityOk(S, pid, x, y);
  if (!BD[type]) return 'Нельзя';
  if (BD[type].one && S.buildings.some(b => b.type === type && b.owner === pid)) return 'Здание может быть только одно';
  return siteTile(S, pid, type, x, y);
}
// проверка одной клетки без правила «только одно здание» — её вызывают в циклах по всей карте
function siteTile(S, pid, type, x, y) {
  const i = y * S.W + x, d = BD[type];
  if (S.occ[i] >= 0) { const ob = S.occ[i] < 1000000 ? S.bmap.get(S.occ[i]) : null; if (!(type === 'gate' && ob && ob.type === 'wall' && ob.owner === pid)) return 'Занято'; }
  if (S.terr[i] !== pid) return 'Не ваша территория';
  const t = S.ter[i], r = S.res[i];
  switch (d.site) {
    case 'forest': if (t !== TR.FOREST) return 'Только в лесу'; break;
    case 'ore': if (!r) return 'Только на залежах руды'; break;
    case 'field': if (t !== TR.GRASS && t !== TR.PLAIN) return 'Только на лугах и равнинах'; break;
    case 'moat': {
      if (!(t === TR.GRASS || t === TR.PLAIN || t === TR.SAND || (t === TR.HILL && !r))) return 'Ров копают на ровной земле';
      let ok = false; for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= S.W || ny >= S.H) continue; if (isWater(S.ter[ny * S.W + nx])) { ok = true; break; } const o2 = S.occ[ny * S.W + nx]; if (o2 >= 0 && o2 < 1000000) { const nb = S.bmap.get(o2); if (nb && nb.type === 'moat' && nb.wet) { ok = true; break; } } }
      if (!ok) return 'Ров должен соединяться с водоёмом'; break; }
    case 'coast': if (!(t === TR.SAND || t === TR.GRASS || t === TR.PLAIN) || !nearSea(S, x, y)) return 'Только на берегу моря или большого озера'; break;
    default: if (t === TR.FOREST || t === TR.MOUNT || isWater(t) || r) return 'Здесь нельзя строить';
  }
  return null;
}
function cityOk(S, pid, x, y) {
  if (!citySpot(S, x, y)) return 'Нужна свободная ровная земля 2×2';
  const cx = x + 1, cy = y + 1;
  for (const c of S.cities) if (dist(cx, cy, c.x + 1, c.y + 1) < CITY_MIN) return 'Слишком близко к другому городу';
  let near = false;
  for (let yy = -9; yy <= 9 && !near; yy++) for (let xx = -9; xx <= 9; xx++) { const X = cx + xx, Y = cy + yy; if (X < 0 || Y < 0 || X >= S.W || Y >= S.H) continue; if (S.terr[Y * S.W + X] === pid && Math.hypot(xx, yy) <= 9) { near = true; break; } }
  if (!near) return 'Слишком далеко от ваших границ (не дальше 9 клеток)';
  for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const o = S.terr[(y + dy) * S.W + x + dx]; if (o !== pid && o !== -2) return 'Чужая территория'; }
  return null;
}
function clearForest(S, x, y) { S.patches = S.patches || []; for (let dy = -1; dy <= 2; dy++) for (let dx = -1; dx <= 2; dx++) { const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= S.W || Y >= S.H) continue; const i = Y * S.W + X; if (S.ter[i] === TR.FOREST && dx >= 0 && dx <= 1 && dy >= 0 && dy <= 1) S.ter[i] = TR.GRASS; } S.patches.push({ x, y }); }
function cityClaim(S, pid, x, y) { const cx = x + 1, cy = y + 1; let land = 0, free = 0, forest = 0; for (let yy = Math.floor(cy - 5); yy <= cy + 5; yy++) for (let xx = Math.floor(cx - 5); xx <= cx + 5; xx++) { if (xx < 0 || yy < 0 || xx >= S.W || yy >= S.H || Math.hypot(xx + 0.5 - cx, yy + 0.5 - cy) > 4) continue; const i = yy * S.W + xx; if (isWater(S.ter[i])) continue; land++; if (S.terr[i] === -2) { free++; } if (S.ter[i] === TR.FOREST) forest++; } return { land, free, forest }; }
function cityCost(S, pid) { const n = S.cities.filter(c => c.owner === pid).length; return { gold: 150 + 60 * n, wood: 100, food: 50 }; }
function doBuild(S, pid, type, x, y) {
  const p = S.players[pid]; if (!p || !p.alive) return 'Нельзя';
  const err = siteOk(S, pid, type, x, y); if (err) return err;
  if (type === 'city') {
    const cost = cityCost(S, pid); if (!canAfford(p, cost)) return 'Не хватает ресурсов';
    pay(p, cost); clearForest(S, x, y); addCity(S, x, y, pid, nextCityName(S), false).radius = 4; p.flags.city = 1; if (p.ai) p.ai.fails = {};
    recomputeTerr(S); return null;
  }
  const d = BD[type]; if (!canAfford(p, d.cost)) return 'Не хватает ресурсов'; pay(p, d.cost);
  { const oi = S.occ[y * S.W + x]; if (type === 'gate' && oi >= 0 && oi < 1000000) { const ob = S.bmap.get(oi); if (ob) demolish(S, ob); } }
  const hp = d.hp ? Math.round(d.hp * (perk(p, 'mason') ? 1.4 : 1)) : 0;
  const b = { id: S.nid++, type, x, y, owner: pid, recipe: RECIPES[type] ? Object.keys(RECIPES[type])[0] : null, busy: false, pt: 0, queue: [], status: '', res: S.res[y * S.W + x], t0: S.time, hp, mhp: hp, gar: null, wet: false };
  S.buildings.push(b); S.bmap.set(b.id, b); S.occ[y * S.W + x] = b.id; p.flags['b_' + type] = 1; S.sv = (S.sv || 0) + 1;
  if (type === 'tower') linkTower(S, b);
  if (STRUCT[type]) refreshStructs(S); if (type === 'moat') recomputeMoat(S); return null;
}
function nextCityName(S) { const used = new Set(S.cities.map(c => c.name)); const free = CITY_NAMES.filter(n => !used.has(n)); return free.length ? free[(S.rnd() * free.length) | 0] : 'Город ' + (S.cities.length + 1); }
function demolish(S, b) {
  if (b.gar && b.gar.s.length) ungarrison(S, b);
  S.occ[b.y * S.W + b.x] = -1; S.buildings = S.buildings.filter(q => q !== b); S.bmap.delete(b.id); S.sv = (S.sv || 0) + 1;
  if (STRUCT[b.type]) refreshStructs(S); if (b.type === 'moat') recomputeMoat(S);
}
function refreshStructs(S) { S.structs = S.buildings.filter(b => STRUCT[b.type]); }
function recomputeMoat(S) {
  S.mm.fill(0); const moats = S.buildings.filter(b => b.type === 'moat'), at = new Map(); for (const b of moats) { b.wet = false; at.set(b.y * S.W + b.x, b); }
  const st = [], D4 = [[1, 0], [-1, 0], [0, 1], [0, -1]];
  for (const b of moats) for (const [dx, dy] of D4) { const nx = b.x + dx, ny = b.y + dy; if (nx >= 0 && ny >= 0 && nx < S.W && ny < S.H && isWater(S.ter[ny * S.W + nx])) { b.wet = true; st.push(b); break; } }
  while (st.length) { const b = st.pop(); for (const [dx, dy] of D4) { const nb = at.get((b.y + dy) * S.W + b.x + dx); if (nb && !nb.wet) { nb.wet = true; st.push(nb); } } }
  for (const b of moats) S.mm[b.y * S.W + b.x] = b.wet ? 1 : 2;
}
function structAtTile(S, x, y) { if (x < 0 || y < 0 || x >= S.W || y >= S.H) return null; const oc = S.occ[y * S.W + x]; if (oc < 0 || oc >= 1000000) return null; const b = S.bmap.get(oc); return b && STRUCT[b.type] ? b : null; }
function structBlockedFor(S, o, x, y) {
  const b = structAtTile(S, x, y); if (!b) return false; if (b.type === 'wall') return true;
  if (b.type === 'tower' && !b.pass) return true;
  if (b.type === 'gate' || (b.type === 'tower' && b.pass)) { if (b.owner < 0 || o < 0) return true; return !(b.owner === o || allied(S, o, b.owner) || passLeft(S, o, b.owner) > 0); } return false;
}
function structBlockedAt(S, o, x, y) { return structBlockedFor(S, o, Math.floor(x), Math.floor(y)); }
const BROKEN = { wall: 'Стена разрушена', tower: 'Башня разрушена', gate: 'Ворота разрушены' };
function destroyStruct(S, b, by) {
  const nm = BROKEN[b.type] || 'Укрепление разрушено'; S.fx.push({ k: 'rubble', x: b.x + 0.5, y: b.y + 0.5, t: S.time });
  const ow = b.owner; demolish(S, b); S.armies.forEach(a => { if (a.siege === b.id) { a.siege = null; a.sg = false; } });
  if (ow === 0) info(S, 0, nm, 'Враг сломал укрепление.', 'war'); if (by === 0) info(S, 0, nm, 'Укрепление врага уничтожено.', 'info');
}
function garrisonSquad(S, a, T) {
  if (!T || T.type !== 'tower') return 'Это не башня'; if (T.owner !== a.owner) return 'Это чужая башня';
  if (T.gar && T.gar.s.length) return 'Башня занята'; const i = a.sq.findIndex(q => q.t === 'archer'); if (i < 0) return 'В армии нет лучников';
  const q = a.sq.splice(i, 1)[0]; T.gar = q; syncSq(q); a._sk = null; a.maxU = Math.max(1, units(a)); if (!a.sq.length) S.armies = S.armies.filter(z => z !== a);
  if (a.owner === 0) info(S, 0, 'Башня занята', 'Лучники заняли башню и получили защиту.', 'info'); return null;
}
function ungarrison(S, T) {
  if (!T.gar) return null; const q = T.gar; T.gar = null; if (!q.s.length || T.owner < 0) return null;
  const pt = freeTileNear(S, T.x, T.y + 1, 4, null) || { x: T.x, y: T.y };
  for (const s of q.s) { s.x = undefined; s.y = undefined; }
  return newArmy(S, T.owner, pt.x + 0.5, pt.y + 0.5, [q]);
}
function upgradeCost(c) { return { gold: 120 * c.level, wood: 80 * c.level, food: 30 * c.level }; }
function upgradeCity(S, c) { const p = c && S.players[c.owner]; if (!p) return 'Нельзя'; if (c.level >= 5) return 'Максимальный уровень'; const k = upgradeCost(c); if (!canAfford(p, k)) return 'Не хватает ресурсов'; pay(p, k); c.level++; p.flags.upg = 1; return null; }

/* ===== Сообщения ===== */
function addMsg(S, to, m) { if (to !== 0) return; m.id = S.nid++; m.t = S.time; m.read = false; S.msgs.unshift(m); if (S.msgs.length > 80) S.msgs.pop(); }
function info(S, to, title, text, type) { addMsg(S, to, { type: type || 'info', title, text }); }

/* ===== Дипломатия ===== */
const rel = (S, a, b) => S.rel[a][b];
function atWar(S, a, b) { if (a === b) return false; if (a < 0 || b < 0) return true; return rel(S, a, b).st === 'war'; }
function pname(S, i) { return i < 0 ? 'Вольный город' : S.players[i].name; }
function rel2(S, a, b) { const r = rel(S, a, b); if (!r.cl) r.cl = {}; return r; }
function claimInfo(S, a, b) { const r = rel2(S, a, b), c = r.cl[a]; if (!c) return null; const ready = S.time >= c.start + CLAIM_DAYS * DAY; if (ready && S.time > c.start + 40 * DAY) { delete r.cl[a]; return null; } return { start: c.start, ready, left: Math.max(0, c.start + CLAIM_DAYS * DAY - S.time), day: Math.min(CLAIM_DAYS, Math.floor((S.time - c.start) / DAY)) }; }
function startClaim(S, a, b) {
  if (a === b || !S.players[a] || !S.players[b] || !S.players[b].alive) return 'Нельзя';
  const r = rel2(S, a, b); if (r.st === 'war') return 'Вы уже воюете'; if (r.ally) return 'Это ваш союзник'; if (claimInfo(S, a, b)) return 'Претензия уже предъявлена';
  r.cl[a] = { start: S.time }; r.op -= 15;
  addMsg(S, b, { type: 'war', title: 'Претензии на ваши земли', text: pname(S, a) + ' готовит повод для войны. Через 3 дня он сможет объявить вам войну.' });
  if (a === 0) addMsg(S, 0, { type: 'war', title: 'Претензия начата', text: 'Через 3 дня можно будет объявить войну: ' + pname(S, b) }); S.players[a].flags.claim = 1; return null;
}
function startWar(S, a, b) {
  const r = rel2(S, a, b); if (r.st === 'war') return;
  r.st = 'war'; r.warSince = S.time; r.trade = false; r.ally = false; r.op = Math.min(r.op, -50); r.cl = {}; r.pass = {};
  // прежние предложения между сторонами теряют силу
  if (S.pending) S.pending = S.pending.filter(q => q.kind === 'truce' || !((q.from === a && q.to === b) || (q.from === b && q.to === a)));
  if (a === 0 || b === 0) { const o = a === 0 ? b : a; for (const m of S.msgs) if (m.kind && m.kind !== 'event' && m.kind !== 'truce' && m.from === o && !m.done) { m.done = true; m.late = true; } }
  addMsg(S, b, { type: 'war', title: 'Объявлена война!', text: pname(S, a) + ' объявил вам войну.' });
  if (a === 0) addMsg(S, 0, { type: 'war', title: 'Война', text: 'Вы объявили войну: ' + pname(S, b) }); S.players[a].flags.war = 1;
}
function declareWar(S, a, b) {
  if (a === b || !S.players[a] || !S.players[b] || !S.players[b].alive) return 'Нельзя';
  const r = rel2(S, a, b); if (r.st === 'war') return 'Уже идёт война'; if (r.st === 'truce' && r.truceUntil > S.time) return 'Действует перемирие'; if (r.ally) return 'Сначала разорвите союз';
  const c = claimInfo(S, a, b); if (!c) return 'Сначала предъявите претензию (3 дня)'; if (!c.ready) return 'Претензия будет готова через ' + Math.ceil(c.left / DAY) + ' дн.';
  startWar(S, a, b); return null;
}
function breakAlly(S, a, b) { const r = rel2(S, a, b); r.ally = false; r.op -= 20; addMsg(S, b, { type: 'info', title: 'Союз разорван', text: pname(S, a) + ' разорвал союз.' }); }
function canEnter(S, o, t) { if (atWar(S, o, t) || allied(S, o, t)) return true; const r = rel(S, o, t); return !!(r.pass && r.pass[o] > S.time) && !sanActive(S, t, o, 'transit'); }
function blockedFor(S, o, x, y) { const t = S.terr[y * S.W + x]; return t >= 0 && t !== o && !canEnter(S, o, t); }
function passLeft(S, a, b) { const r = rel(S, a, b); return r.pass && r.pass[a] > S.time ? r.pass[a] - S.time : 0; }
function allied(S, a, b) { return a >= 0 && b >= 0 && a !== b && !!rel(S, a, b).ally; }
function todHours(S) { return (8 + (S.time / DAY) * 24) % 24; }
// зона видимости игрока: свои войска, города, здания, корабли и союзников
function calcVision(S) {
  const v = []; const mine = o => o === 0 || allied(S, 0, o);
  for (const a of S.armies) if (mine(a.owner) && !a.ship) v.push([a.x, a.y, 5.5]);
  for (const c of S.cities) if (mine(c.owner)) v.push([c.x + 1, c.y + 1, 7]);
  for (const b of S.buildings) if (mine(b.owner) && b.type !== 'wall' && b.type !== 'moat' && b.type !== 'gate') v.push([b.x + 0.5, b.y + 0.5, b.type === 'tower' ? 5 : 3.2]);
  for (const sh of S.ships) if (mine(sh.owner)) v.push([sh.x, sh.y, 4.5]);
  S.vis = v;
}
function visibleAt(S, x, y) { const v = S.vis; if (!v) return true; for (let i = 0; i < v.length; i++) { const q = v[i], dx = q[0] - x, dy = q[1] - y; if (dx * dx + dy * dy <= q[2] * q[2]) return true; } return false; }
function canSee(S, o) { return o.owner === 0 || allied(S, 0, o.owner) || visibleAt(S, o.x, o.y); }
function milPower(S, pid) { let s = 0; for (const a of S.armies) if (a.owner === pid) for (const q of a.sq) s += q.u * SQ[q.t].atk * SQ[q.t].hp / 100; return s; }
// можно ли сейчас заключить такую сделку: null — да, иначе причина
function dealError(S, from, to, kind) {
  const pf = S.players[from], pt = S.players[to]; if (from === to || !pf || !pt) return 'Нельзя';
  if (!pf.alive || !pt.alive) return 'Это государство пало';
  const r = rel(S, from, to);
  if (sanActive(S, from, to, 'envoys') || sanActive(S, to, from, 'envoys')) return 'Послы отозваны: дипломатия заморожена';
  if (kind === 'trade') { if (r.st !== 'peace') return 'Торговля возможна только в мире'; if (r.trade) return 'Соглашение уже есть'; if (sanActive(S, from, to, 'embargo') || sanActive(S, to, from, 'embargo')) return 'Действует торговое эмбарго'; }
  else if (kind === 'truce') { if (r.st !== 'war') return 'Вы не воюете'; }
  else if (kind === 'ult') { if (r.st === 'war') return 'Вы уже воюете'; if (r.st === 'truce') return 'Действует перемирие'; if (r.ally) return 'Союзникам ультиматумы не предъявляют'; }
  else if (kind === 'ally') { if (r.ally) return 'Союз уже заключён'; if (r.st !== 'peace') return 'Союз возможен только в мире'; }
  else if (kind === 'pass') { if (r.st === 'war') return 'Во время войны проход закрыт'; if (r.ally) return 'Союзникам проход открыт'; if (passLeft(S, from, to) > 0) return 'Право прохода уже есть'; if (sanActive(S, from, to, 'transit') || sanActive(S, to, from, 'transit')) return 'Транзит закрыт санкциями'; }
  else return 'Нельзя';
  return null;
}
// пауза между одинаковыми предложениями: чтобы нельзя было «закликать» соседа
function cdLeft(S, from, to, kind) { const r = rel(S, from, to), v = r.cd && r.cd[kind + from]; return v > S.time ? v - S.time : 0; }
function setCd(S, from, to, kind, sec) { const r = rel(S, from, to); if (!r.cd) r.cd = {}; r.cd[kind + from] = S.time + sec; }
const OFFER_TITLE = { trade: 'Торговое предложение', pass: 'Просьба о проходе войск', ally: 'Предложение союза', truce: 'Просьба о перемирии', ult: 'Ультиматум!' };
const OFFER_TEXT = { trade: ' предлагает торговое соглашение.', ally: ' предлагает союз: общая видимость и защита.', pass: ' просит разрешить проход своих войск по вашей территории на 3 дня.', truce: ' просит о перемирии.', ult: ' требует 150 золота, иначе — война. Ответ нужен в течение суток.' };
const OFFER_TYPE = { trade: 'trade', ally: 'trade', pass: 'trade', truce: 'truce', ult: 'war' };
function offer(S, from, to, kind) {
  const e = dealError(S, from, to, kind); if (e) return e;
  const cd = cdLeft(S, from, to, kind); if (cd > 0) return 'Слишком рано: повторить можно через ' + Math.max(1, Math.ceil(cd / DAY * 24)) + ' ч.';
  if (to === 0 && S.players[0].human) { // письмо игроку: не дублируем, пока прошлое без ответа
    if (S.msgs.some(m => m.kind === kind && m.from === from && !m.done)) return 'Письмо уже отправлено';
    setCd(S, from, to, kind, kind === 'ult' ? 4 * DAY : 2 * DAY);
    addMsg(S, 0, { type: OFFER_TYPE[kind], kind, from, title: OFFER_TITLE[kind], text: pname(S, from) + OFFER_TEXT[kind], exp: S.time + (kind === 'ult' ? DAY : 2 * DAY) }); return null;
  }
  if (from === 0 && S.players[0].human) {
    if (S.pending.some(q => q.from === from && q.to === to && q.kind === kind)) return 'Предложение уже отправлено — ждите ответа';
    S.pending.push({ at: S.time + 3 + S.rnd() * 5, from, to, kind }); return null;
  }
  decideOffer(S, from, to, kind); return null;
}
function decideOffer(S, from, to, kind) {
  const hum = from === 0 && S.players[0].human;
  if (dealError(S, from, to, kind)) { if (hum) info(S, 0, 'Предложение устарело', pname(S, to) + ': обстоятельства изменились.', 'info'); return; }
  const r = rel(S, from, to); let ok = false;
  if (kind === 'trade') ok = r.op > -35;
  else if (kind === 'pass') ok = r.op >= 0 || r.trade;
  else if (kind === 'ally') { const common = S.players.some(z => z.id !== from && z.id !== to && z.alive && rel(S, from, z.id).st === 'war' && rel(S, to, z.id).st === 'war'); ok = r.op >= 10 && (r.trade || common); }
  else if (kind === 'truce') ok = S.time - r.warSince > 150 || milPower(S, to) < milPower(S, from) * 0.9;
  else if (kind === 'ult') {
    ok = milPower(S, to) * 1.4 < milPower(S, from) && S.players[to].stock.gold >= 150; setCd(S, from, to, 'ult', 2 * DAY);
    if (ok) { S.players[to].stock.gold -= 150; S.players[from].stock.gold += 150; r.op -= 15; } else { r.op -= 25; startWar(S, to, from); }
    if (hum) info(S, 0, ok ? 'Ультиматум принят' : 'Ультиматум отвергнут', pname(S, to) + (ok ? ' уплатил 150 золота.' : ' ответил войной!'), 'war'); return;
  }
  if (!ok) setCd(S, from, to, kind, DAY / 2);
  applyDeal(S, from, to, kind, ok);
}
function applyDeal(S, from, to, kind, ok) {
  const r = rel(S, from, to);
  if (ok) {
    if (kind === 'trade') { r.trade = true; r.op += 10; S.players[from].flags.trade = 1; S.players[to].flags.trade = 1; }
    else if (kind === 'truce') { r.st = 'truce'; r.truceUntil = S.time + 240; r.op += 20; }
    else if (kind === 'ally') { r.ally = true; r.op += 15; S.players[from].flags.ally = 1; S.players[to].flags.ally = 1; }
    else if (kind === 'pass') { r.pass = r.pass || {}; r.pass[from] = S.time + 3 * DAY; r.op += 3; }
  } else r.op -= 5;
  const tp = kind === 'trade' || kind === 'ally' || kind === 'pass' ? 'trade' : 'truce';
  if (from === 0) info(S, 0, (ok ? 'Принято: ' : 'Отказ: ') + (kind === 'trade' ? 'торговля' : kind === 'ally' ? 'союз' : kind === 'pass' ? 'право прохода' : 'перемирие'), pname(S, to) + (ok ? ' согласился.' : ' отклонил предложение.'), tp);
  if (ok && to === 0) info(S, 0, 'Соглашение заключено', pname(S, from), tp);
}
// ответ игрока на письмо; возвращает текст ошибки или null
function answerMsg(S, id, yes) {
  const m = S.msgs.find(q => q.id === id); if (!m || m.done || !m.kind) return null;
  if (m.kind === 'event') return answerEvent(S, m, yes);
  m.done = true; m.read = true; const from = m.from;
  if (m.exp && S.time > m.exp) { m.late = true; return 'Срок предложения истёк'; }
  const e = dealError(S, from, 0, m.kind); if (e) { m.late = true; return yes ? 'Предложение устарело: ' + e.toLowerCase() : null; }
  if (m.kind === 'ult') {
    if (yes && S.players[0].stock.gold >= 150) { S.players[0].stock.gold -= 150; S.players[from].stock.gold += 150; rel(S, 0, from).op -= 10; return null; }
    startWar(S, from, 0); return yes ? 'В казне нет 150 золота — это война' : null;
  }
  applyDeal(S, from, 0, m.kind, yes); return null;
}
function cancelTrade(S, a, b) { const r = rel(S, a, b); r.trade = false; r.op -= 8; }

/* ===== Санкции (как в Victoria 3) ===== */
function sanActive(S, a, b, k) { return (S.sanctions || []).some(s => s.a === a && s.b === b && s.k === k && s.until > S.time); }
function sanOn(S, pid, k) { return (S.sanctions || []).some(s => s.b === pid && s.k === k && s.until > S.time); }
function sanList(S, a, b) { return (S.sanctions || []).filter(s => s.a === a && s.b === b && s.until > S.time); }
function sanCdLeft(S, a, b, k) { const r = rel(S, a, b), v = r.cd && r.cd['san' + k + a]; return v > S.time ? v - S.time : 0; }
function applySanction(S, a, b, k) {
  const d = SANCTIONS[k]; if (!d) return 'Неизвестная санкция';
  if (a === b || !S.players[a] || !S.players[b] || !S.players[b].alive || !S.players[a].alive) return 'Нельзя';
  const cd = sanCdLeft(S, a, b, k); if (cd > 0) return 'Повторить можно через ' + Math.max(1, Math.ceil(cd / DAY)) + ' дн.';
  if (!d.once && sanActive(S, a, b, k)) return 'Санкция уже действует';
  const p = S.players[a];
  if (S.opts.sandbox && p.human) { /* в песочнице золото бесконечно */ } else if (p.stock.gold < d.cost) return 'Не хватает золота: нужно ' + d.cost;
  if (!(S.opts.sandbox && p.human)) pay(p, { gold: d.cost });
  const r = rel2(S, a, b); r.cd = r.cd || {};
  if (d.once) {
    if (k === 'seize') {
      const t = S.players[b], g = Math.min(250, Math.floor(t.stock.gold)); t.stock.gold -= g; p.stock.gold += g; p.earned += g; r.op -= 30;
      addMsg(S, b, { type: 'war', title: 'Конфискация казны', text: pname(S, a) + ' конфисковал ' + g + ' золота.' });
      info(S, a, 'Конфискация', 'Из казны ' + pname(S, b) + ' изъято ' + g + ' золота.', 'econ');
    } else {
      r.op -= 12;
      addMsg(S, b, { type: 'info', title: 'Дипломатический протест', text: pname(S, a) + ' выразил решительный протест.' });
      info(S, a, 'Протест', 'Протест объявлен: ' + pname(S, b) + '.', 'info');
    }
    r.cd['san' + k + a] = S.time + 5 * DAY;
  } else {
    S.sanctions.push({ a, b, k, until: S.time + d.days * DAY });
    if (k === 'embargo' && r.trade) cancelTrade(S, a, b);
    if (k === 'transit') r.pass = {};
    r.op -= (k === 'envoys' ? 10 : 8);
    addMsg(S, b, { type: 'war', title: 'Санкции', text: pname(S, a) + ' ввёл санкцию: ' + d.n.toLowerCase() + '.' });
    info(S, a, 'Санкция введена', d.n + ' против ' + pname(S, b) + '.', 'econ');
    if (S.sanctions.length > 200) S.sanctions.splice(0, S.sanctions.length - 200);
  }
  S.players[a].flags.san = 1;
  return null;
}
function liftSanction(S, a, b, k) {
  const i = (S.sanctions || []).findIndex(s => s.a === a && s.b === b && s.k === k && s.until > S.time);
  if (i < 0) return 'Такой санкции нет';
  S.sanctions.splice(i, 1);
  info(S, 0, 'Санкция снята', SANCTIONS[k].n + ' против ' + pname(S, b) + ' отменена.', 'info');
  return null;
}

/* ===== Башни соединяются стеной, в соединении можно сделать проход ===== */
function freeBuild(S, pid, type, x, y) {
  if (!S.players[pid]) return null;
  const err = siteTile(S, pid, type, x, y); if (err) return null;
  { const oi = S.occ[y * S.W + x]; if (type === 'gate' && oi >= 0 && oi < 1000000) { const ob = S.bmap.get(oi); if (ob) demolish(S, ob); } }
  const d = BD[type], hp = d.hp ? Math.round(d.hp * (perk(S.players[pid], 'mason') ? 1.4 : 1)) : 0;
  const b = { id: S.nid++, type, x, y, owner: pid, recipe: RECIPES[type] ? Object.keys(RECIPES[type])[0] : null, busy: false, pt: 0, queue: [], status: '', res: S.res[y * S.W + x], t0: S.time, hp, mhp: hp, gar: null, wet: false };
  S.buildings.push(b); S.bmap.set(b.id, b); S.occ[y * S.W + x] = b.id;
  if (STRUCT[type]) refreshStructs(S); if (type === 'moat') recomputeMoat(S); S.sv = (S.sv || 0) + 1;
  return b;
}
function linkTower(S, T) {
  if (!T.links) T.links = [];
  for (const O of S.buildings) {
    if (O === T || O.type !== 'tower' || O.owner !== T.owner || O.owner < 0) continue;
    const dx = O.x - T.x, dy = O.y - T.y, ax = Math.abs(dx), ay = Math.abs(dy);
    if (!(dx === 0 || dy === 0 || ax === ay)) continue;
    const n = Math.max(ax, ay); if (n < 1 || n > 3) continue;
    if (T.links.some(l => l.id === O.id) || (O.links || []).some(l => l.id === T.id)) continue;
    const sx = dx ? Math.sign(dx) : 0, sy = dy ? Math.sign(dy) : 0, tiles = [];
    let ok = true;
    for (let k = 1; k < n; k++) {
      const x = T.x + sx * k, y = T.y + sy * k;
      if (siteTile(S, T.owner, 'wall', x, y)) { ok = false; break; }
      const w = freeBuild(S, T.owner, 'wall', x, y);
      if (!w) { ok = false; break; }
      tiles.push(w.id);
    }
    if (!ok) { for (const id of tiles) { const w = S.bmap.get(id); if (w) demolish(S, w); } continue; }
    T.links.push({ id: O.id, tiles, open: false });
    if (!O.links) O.links = [];
    O.links.push({ id: T.id, tiles, open: false });
    if (T.owner === 0) info(S, 0, 'Башни соединены', n > 1 ? 'Между башнями возведена стена. Нажмите на башню — можно сделать проход.' : 'Башни стоят вплотную. Нажмите на башню — можно открыть проход.', 'info');
    break;
  }
}
function toggleTowerPass(S, T, otherId) {
  const link = (T.links || []).find(l => l.id === +otherId); if (!link) return 'Соединение не найдено';
  const O = S.bmap.get(link.id); if (!O || O.type !== 'tower') return 'Соединение не найдено';
  const ol = (O.links || []).find(l => l.id === T.id), j = link.tiles.length ? Math.floor((link.tiles.length - 1) / 2) : -1;
  if (j < 0) {
    const open = !link.open;
    link.open = open; if (ol) ol.open = open;
    T.pass = open; O.pass = open;
    return null;
  }
  const midId = link.tiles[j], w = S.bmap.get(midId);
  if (!w) return 'Стена разрушена';
  const x = w.x, y = w.y;
  demolish(S, w);
  const nb = w.type === 'wall' ? freeBuild(S, T.owner, 'gate', x, y) : freeBuild(S, T.owner, 'wall', x, y);
  if (!nb) { freeBuild(S, T.owner, 'wall', x, y); return 'Не удалось изменить проход'; }
  link.tiles[j] = nb.id;
  if (ol) { const k = ol.tiles.indexOf(midId); if (k >= 0) ol.tiles[k] = nb.id; }
  const open = nb.type === 'gate';
  link.open = open; if (ol) ol.open = open;
  return null;
}

/* ===== Жители: селятся сами и строят ===== */
const FOLK_M = ['Андрей', 'Борис', 'Василий', 'Григорий', 'Дмитрий', 'Егор', 'Ждан', 'Захар', 'Иван', 'Кузьма', 'Лев', 'Максим', 'Никита', 'Олег', 'Пётр', 'Роман', 'Семён', 'Тимур', 'Фёдор', 'Ярослав'];
const FOLK_F = ['Анна', 'Богдана', 'Варвара', 'Глафира', 'Дарья', 'Елена', 'Жанна', 'Зинаида', 'Ирина', 'Ксения', 'Людмила', 'Мария', 'Наталья', 'Ольга', 'Полина', 'Раиса', 'Софья', 'Таисия', 'Ульяна', 'Яна'];
const FOLK_LAST = ['Соколов', 'Орлов', 'Волков', 'Медведев', 'Зайцев', 'Лисицын', 'Кузнецов', 'Мельников', 'Громов', 'Соловьёв', 'Воронов', 'Каменев', 'Беляев', 'Тихонов', 'Гордеев', 'Миронов', 'Фомин', 'Журавлёв', 'Нестеров', 'Прохоров'];
const FOLK_JOBS = ['Фермер', 'Кузнец', 'Торговец', 'Ремесленник', 'Рыбак', 'Лесоруб', 'Шахтёр', 'Стражник', 'Пекарь', 'Гончар', 'Портной', 'Мельник', 'Лекарь', 'Скотник', 'Плотник'];
function makeFolk(S, c) {
  const male = S.rnd() < 0.5;
  return { id: S.nid++, name: (male ? FOLK_M : FOLK_F)[(S.rnd() * 20) | 0] + ' ' + FOLK_LAST[(S.rnd() * 20) | 0], age: 16 + ((S.rnd() * 42) | 0), job: FOLK_JOBS[(S.rnd() * FOLK_JOBS.length) | 0], city: c ? c.id : 0, st: 'ok', wealth: 10 + ((S.rnd() * 80) | 0), mood: 45 + ((S.rnd() * 50) | 0) };
}
const FOLK_CAPS = { house: 60, farm: 24, market: 8, sawmill: 10, mill: 5 };
function folkBuild(S, f) {
  const mine = S.cities.filter(c => c.owner === 0); if (!mine.length) return false;
  const c = mine.find(q => q.id === f.city) || mine[(S.rnd() * mine.length) | 0];
  const kinds = ['house', 'house', 'house', 'house', 'farm', 'farm', 'market', 'sawmill', 'mill'];
  const cx = c.x + 1, cy = c.y + 1, r = Math.max(3, Math.floor(c.radius));
  const bc = {}; for (const b of S.buildings) if (b.owner === 0 && b.byFolk) bc[b.type] = (bc[b.type] || 0) + 1;
  for (let t = 0; t < 10; t++) {
    const type = kinds[(S.rnd() * kinds.length) | 0];
    if ((bc[type] || 0) >= (FOLK_CAPS[type] || 6)) continue;
    const x = cx + ((S.rnd() * (2 * r + 1)) | 0) - r, y = cy + ((S.rnd() * (2 * r + 1)) | 0) - r;
    if (x < 1 || y < 1 || x >= S.W - 1 || y >= S.H - 1) continue;
    const b = freeBuild(S, 0, type, x, y);
    if (b) {
      b.byFolk = f.id;
      S.toast.push({ title: 'Жители строят', text: f.name + ' построил: ' + BD[type].n.toLowerCase() + '.', t: S.time, good: true, ev: 1 });
      return true;
    }
  }
  return false;
}
function tickFolk(S, dt) {
  if (!S.folk) return;
  S.fT = (S.fT || 0) + dt;
  for (const f of S.folk) {
    if (f.st === 'dead') continue;
    f.age += dt / (DAY * 2); // два года за четверо суток
    if (f.age > 68 + (f.id % 20) && S.rnd() < 0.004) { f.st = 'dead'; if (S.players[0].alive) info(S, 0, 'Умер житель', f.name + ', ' + Math.round(f.age) + ' лет.', 'info'); }
  }
  if (S.fT < 12) return; S.fT = 0;
  const mine = S.cities.filter(c => c.owner === 0); if (!mine.length || !S.players[0].alive) return;
  const okN = S.folk.reduce((n, f) => n + (f.st === 'ok' ? 1 : 0), 0), cap = mine.length * 30 + 30;
  if (okN < cap && S.rnd() < 0.6) {
    const c = mine[(S.rnd() * mine.length) | 0], f = makeFolk(S, c);
    S.folk.push(f);
    if (S.folk.length % 10 === 1) S.toast.push({ title: 'Новые жители', text: 'В провинцию прибыли поселенцы. Всего жителей: ' + (S.folk.length + 1) + '.', t: S.time, good: true, ev: 1 });
    if (S.folk.length > 1500) S.folk = S.folk.filter(q => q.st !== 'dead').slice(-1200);
  }
  const byFolk = S.buildings.reduce((n, b) => n + (b.owner === 0 && b.byFolk ? 1 : 0), 0);
  if (byFolk < 90 && S.rnd() < 0.5) {
    const workers = S.folk.filter(f => f.st === 'ok');
    if (workers.length) folkBuild(S, workers[(S.rnd() * workers.length) | 0]);
  }
}
function folkGet(S, id) { return S.folk.find(q => q.id === +id); }
function folkKill(S, id, anon) {
  const f = folkGet(S, id); if (!f) return 'Житель не найден';
  if (f.st === 'dead') return 'Житель уже мёртв';
  const p = S.players[0];
  if (anon) {
    if (!(S.opts.sandbox && p.human) && p.stock.gold < 150) return 'Наёмники просят 150 золота';
    if (!(S.opts.sandbox && p.human)) pay(p, { gold: 150 });
    if (S.rnd() < 0.75) { f.st = 'dead'; info(S, 0, 'Заказ выполнен', 'Наёмники анонимно устранили ' + f.name + '. Следов нет.', 'info'); return null; }
    return 'Наёмники не справились — следов нет, но и цель жива';
  }
  f.st = 'dead';
  for (const g of S.folk) if (g.st === 'ok' && S.rnd() < 0.2) g.mood = Math.max(0, g.mood - 8);
  info(S, 0, 'Казнь', f.name + ' казнён по вашему приказу. Люди встревожены.', 'info');
  return null;
}
function folkPrison(S, id, jail) {
  const f = folkGet(S, id); if (!f) return 'Житель не найден';
  if (f.st === 'dead') return 'Житель мёртв';
  if (jail) {
    if (f.st === 'prison') return 'Уже в тюрьме';
    f.st = 'prison'; info(S, 0, 'Тюрьма', f.name + ' посажен в тюрьму.', 'info');
  } else {
    if (f.st !== 'prison') return 'Он не в тюрьме';
    f.st = 'ok'; info(S, 0, 'Освобождён', f.name + ' выпущен на свободу.', 'info');
  }
  return null;
}

/* ===== Границы медленно ползут в свободные земли ===== */
function tickExpand(S, dt) {
  if (!S.extra) return;
  S.eT = (S.eT || 0) + dt; if (S.eT < 5) return; S.eT = 0;
  if (S.over || !S.players[0].alive || S.extra.size >= 800) return;
  const W = S.W, H = S.H, t = S.terr, cand = [];
  for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
    const i = y * W + x; if (t[i] !== -2 || S.extra.has(i)) continue;
    const tt = S.ter[i]; if (isWater(tt) || tt === TR.MOUNT) continue;
    if (t[i - 1] === 0 || t[i + 1] === 0 || t[i - W] === 0 || t[i + W] === 0 || S.extra.get(i - 1) === 0 || S.extra.get(i + 1) === 0 || S.extra.get(i - W) === 0 || S.extra.get(i + W) === 0) cand.push(i);
    if (cand.length > 400) break;
  }
  if (!cand.length) return;
  let n = 1 + ((S.rnd() * 2) | 0);
  while (n-- > 0 && cand.length) { const j = (S.rnd() * cand.length) | 0; S.extra.set(cand[j], 0); cand.splice(j, 1); }
  recomputeTerr(S);
}

/* ===== Войска ===== */
const BAT = 0.3; // затяжные бои: меньше урона за секунду
const FORD = { cmd: -2, ram: -1, heavy: 0, spear: 0.5, light: 1, militia: 1.5, cav: 2, archer: 3, cat: 4 };
function mkSquad(t) { const n = SQ[t].cnt || 10, s = []; for (let i = 0; i < n; i++) s.push({ hp: SQ[t].hp }); return { t, u: n, hp: n * SQ[t].hp, s, w: 0 }; }
const sqMax = t => SQ[t].cnt || 10;
function syncSq(q) { q.u = q.s.length; let h = 0; for (const x of q.s) h += x.hp; q.hp = h; }
function armySoldiers(a) { const key = a.sq.map(q => q.t + (q.cls || '') + q.s.length).join(); if (a._sk === key) return a._sl; const l = []; for (const q of a.sq) for (const s of q.s) l.push({ s, t: q.t, cls: q.cls }); l.sort((p, q) => FORD[p.t] - FORD[q.t]); a._sk = key; return a._sl = l; }
const FS = {};
function formSlots(n) { if (FS[n]) return FS[n]; const rows = n <= 5 ? 1 : n <= 14 ? 2 : n <= 24 ? 3 : n <= 40 ? 4 : n <= 60 ? 5 : 6, cols = Math.max(1, Math.ceil(n / rows)), sx = n > 40 ? 3.0 : 3.6, sy = 2.4, f = []; for (let i = 0; i < n; i++) { const r = Math.floor(i / cols), cc = i % cols; f.push({ ox: (cc - (cols - 1) / 2) * sx + (r & 1 ? 0.9 : 0), oy: (r - (rows - 1) / 2) * sy + 1 }); } return FS[n] = { f, w: cols * sx + 1, h: rows * sy + 3 }; }
function reapArmy(S, a, killer, wound) {
  for (const q of a.sq) {
    if (!(q.w >= 0)) q.w = 0;
    for (let i = q.s.length - 1; i >= 0; i--) if (q.s[i].hp <= 0) {
      const s = q.s[i]; q.s.splice(i, 1); if (killer >= 0) S.players[killer].kills++;
      if (wound && sqMax(q.t) > 1 && q.w + q.s.length < sqMax(q.t) && S.rnd() < 0.65) q.w++;
      if (s.x !== undefined) S.fx.push({ k: 'rip', x: s.x, y: s.y, t: S.time });
    }
    syncSq(q);
  }
  a._sk = null; cleanArmy(S, a, killer);
}
const armyCap = a => a.sq.reduce((s, q) => s + sqMax(q.t), 0); // сколько бойцов в армии при полных отрядах
const units = a => a.sq.reduce((s, q) => s + q.u, 0);
function newArmy(S, owner, x, y, sq) { const a = { id: S.nid++, owner, x, y, sq, path: [], dest: null, st: 'idle', ship: null, maxU: sq.reduce((s, q) => s + q.u, 0), face: 1, boardPort: null }; S.armies.push(a); return a; }
function armySpd(S, a) { let s = 9; for (const q of a.sq) s = Math.min(s, q.t === 'cmd' && q.cls === 'cav' ? 2.2 : q.t === 'cmd' ? SQ[q.cls].spd : SQ[q.t].spd); s *= MOVE_MULT; const i = Math.floor(a.y) * S.W + Math.floor(a.x); return s * TMULT[S.ter[i]] * (S.mm[i] === 1 ? 0.5 : 1); }
function combo(a) { const t = new Set(a.sq.map(q => q.t).filter(x => x !== 'cmd')); return 1 + 0.08 * Math.max(0, t.size - 1); }
function mergeNear(S, a) {
  if (a.ship) return 0; let n = 0;
  for (const b of S.armies.slice()) {
    if (b === a || b.owner !== a.owner || b.ship || dist(a.x, a.y, b.x, b.y) > 1.8 || a.sq.length + b.sq.length > MAX_SQ_ARMY) continue;
    a.sq.push(...b.sq); a.maxU += b.maxU; a._sk = null; S.armies = S.armies.filter(q => q !== b); n++;
  }
  if (n) S.players[a.owner].flags.army = 1; return n;
}
function splitArmy(S, a) {
  if (a.ship || a.sq.length < 2) return 0;
  const rest = a.sq.splice(1), spots = reachTiles(S, a.owner, Math.floor(a.x), Math.floor(a.y), rest.length, 4);
  rest.forEach((q, i) => { const p = spots[i]; newArmy(S, a.owner, p ? p.x + 0.5 : a.x, p ? p.y + 0.5 : a.y, [q]).face = a.face; });
  a.maxU = units(a); a._sk = null; return rest.length;
}
function orderMove(S, a, tx, ty) {
  if (a.ship) return 'Войска на корабле — управляйте кораблём';
  if (!(tx >= 0 && ty >= 0 && tx < S.W && ty < S.H)) return 'Туда нельзя: край карты';
  a.retreat = false; // новый приказ отменяет отступление
  const gx0 = Math.floor(tx), gy0 = Math.floor(ty), gb = structAtTile(S, gx0, gy0);
  if (gb) {
    if (gb.owner !== a.owner && !allied(S, a.owner, gb.owner) && atWar(S, a.owner, gb.owner)) return orderSiege(S, a, gb);
    if (gb.type === 'tower' && gb.owner === a.owner) return orderGarrison(S, a, gb);
  }
  a.siege = null; a.sg = false; a.garrison = null; return moveTo(S, a, tx, ty);
}
function moveTo(S, a, tx, ty) {
  let gx = Math.floor(tx), gy = Math.floor(ty);
  if (!(gx >= 0 && gy >= 0 && gx < S.W && gy < S.H)) return 'Туда нельзя: край карты';
  if (isWater(S.ter[gy * S.W + gx]) || S.ter[gy * S.W + gx] === TR.MOUNT) { const p = nearestTile(S, gx, gy, false, 3); if (!p) return 'Туда нельзя: горы или вода'; gx = p.x; gy = p.y; }
  const sx = Math.floor(a.x), sy = Math.floor(a.y), o = a.owner;
  if (S.comp[sy * S.W + sx] !== S.comp[gy * S.W + gx]) return 'Нет пути по суше (вода или горы) — нужен корабль';
  if (blockedFor(S, o, gx, gy)) return 'Чужая территория: объявите войну или запросите право прохода';
  const startBlocked = blockedFor(S, o, sx, sy);
  const path = astar(S, sx, sy, gx, gy, false, (x, y) => (!startBlocked && blockedFor(S, o, x, y)) || structBlockedFor(S, o, x, y));
  if (!path) return 'Путь закрыт: стены, чужие границы, горы или вода';
  a.path = path; a.dest = { x: gx, y: gy }; a.st = 'move'; a.boardPort = null; a.arrived = !path.length; // уже на месте — прибытие обработает tickMove
  const bo = S.occ[gy * S.W + gx]; if (bo >= 0 && bo < 1000000) { const b = bidx(S, bo); if (b && b.type === 'port' && b.owner === a.owner) a.boardPort = b.id; }
  return null;
}
const siegeReach = a => a.sq.every(q => q.t === 'cat') ? 5.2 : 3.0;
function adjTiles(S, a, b, r, ideal) { // проходимые клетки рядом с укреплением, ближайшие к армии
  const l = []; for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) { const x = b.x + dx, y = b.y + dy; if (!dx && !dy) continue; if (x < 0 || y < 0 || x >= S.W || y >= S.H) continue; const t = S.ter[y * S.W + x]; if (isWater(t) || t === TR.MOUNT || structBlockedFor(S, a.owner, x, y) || S.occ[y * S.W + x] >= 1000000) continue; l.push({ x, y, d: (ideal ? Math.abs(dist(x + 0.5, y + 0.5, b.x + 0.5, b.y + 0.5) - ideal) : Math.ceil(dist(x + 0.5, y + 0.5, b.x + 0.5, b.y + 0.5) - 0.01)) * 100 + dist(x + 0.5, y + 0.5, a.x, a.y) }); }
  return l.sort((p, q) => p.d - q.d);
}
function orderSiege(S, a, b) {
  if (dist(a.x, a.y, b.x + 0.5, b.y + 0.5) <= siegeReach(a)) { a.siege = b.id; a.sg = true; a.path = []; a.st = 'idle'; return null; }
  const cat = a.sq.every(q => q.t === 'cat'), cand = adjTiles(S, a, b, cat ? 6 : 3, cat ? 4.2 : 0); let err = 'Не подойти к укреплению';
  for (const c of cand.slice(0, 5)) { const e = moveTo(S, a, c.x + 0.5, c.y + 0.5); if (!e) { a.siege = b.id; a.sg = false; a.garrison = null; return null; } err = e; }
  return err;
}
function orderGarrison(S, a, b) {
  if (!a.sq.some(q => q.t === 'archer')) return 'В армии нет лучников для башни'; if (b.gar && b.gar.s.length) return 'Башня уже занята';
  if (dist(a.x, a.y, b.x + 0.5, b.y + 0.5) <= 1.9) return garrisonSquad(S, a, b);
  const cand = adjTiles(S, a, b, 2); let err = 'Не подойти к башне';
  for (const c of cand.slice(0, 4)) { const e = moveTo(S, a, c.x + 0.5, c.y + 0.5); if (!e) { a.garrison = b.id; a.siege = null; return null; } err = e; }
  return err;
}
function stopArmy(a) { a.path = []; a.dest = null; a.st = 'idle'; a.boardPort = null; a.siege = null; a.sg = false; a.garrison = null; a.retreat = false; a.arrived = false; }
function nearestOwnCity(S, a) { let best = null, bd = 1e9; for (const c of S.cities) if (c.owner === a.owner) { const d = dist(a.x, a.y, c.x + 1, c.y + 1); if (d < bd && S.comp[(c.y) * S.W + c.x] === S.comp[Math.floor(a.y) * S.W + Math.floor(a.x)]) { bd = d; best = c; } } return best; }
function nearestHospital(S, a) { let best = null, bd = 1e9; const cp = S.comp[Math.floor(a.y) * S.W + Math.floor(a.x)]; for (const b of S.buildings) if (b.type === 'hospital' && b.owner === a.owner) { const d = dist(a.x, a.y, b.x + 0.5, b.y + 0.5); if (d < bd && S.comp[b.y * S.W + b.x] === cp) { bd = d; best = b; } } return best; }
function retreat(S, a) {
  if (a.ship) return 'Войска на корабле';
  const h = nearestHospital(S, a); let e = 'Некуда отступать';
  if (h) { const p = freeTileNear(S, h.x, h.y + 1, 3, landComp(S, Math.floor(a.x), Math.floor(a.y))); if (p) e = orderMove(S, a, p.x + 0.5, p.y + 0.5); }
  if (e) { const c = nearestOwnCity(S, a); if (c) e = orderMove(S, a, c.x + 1, c.y + 1); }
  if (!e && a.path.length) { a.retreat = true; a.fight = false; } // если армия уже на месте, отступать некуда — она продолжит бой
  return e;
}
function tickHospital(S, dt) {
  for (const b of S.buildings) {
    if (b.type !== 'hospital' || b.owner < 0) continue; const p = S.players[b.owner]; b.treated = 0; b.rt = (b.rt || 0) + dt; let rv = false; if (b.rt >= 5) { b.rt = 0; rv = true; }
    for (const a of S.armies) {
      if (a.owner !== b.owner || a.ship || a.bat || a.path.length || dist(a.x, a.y, b.x + 0.5, b.y + 0.5) > 3.5) continue;
      for (const q of a.sq) {
        let hl = false; for (const s of q.s) if (s.hp < SQ[q.t].hp) { s.hp = Math.min(SQ[q.t].hp, s.hp + 2.2 * dt); hl = true; } if (hl) b.treated++;
        if (rv && q.w > 0 && q.s.length < sqMax(q.t) && canAfford(p, { gold: 8, food: 4 })) { pay(p, { gold: 8, food: 4 }); q.w--; q.s.push({ hp: SQ[q.t].hp * 0.6 }); a._sk = null; b.treated++; }
        syncSq(q);
      }
    }
  }
}

/* ===== Корабли ===== */
function shipCost(kind, p) { const c = kind === 'war' ? { wood: 120, gold: 100, iron: 30 } : { wood: 80, gold: 80 }; if (perk(p, 'sea')) for (const k in c) c[k] = Math.round(c[k] * 0.7); return c; }
function buildShip(S, port, kind) {
  if (!port || port.type !== 'port' || port.owner < 0 || (kind !== 'war' && kind !== 'trade')) return 'Нужен свой порт';
  const p = S.players[port.owner], c = shipCost(kind, p); if (!canAfford(p, c)) return 'Не хватает ресурсов';
  if (S.ships.filter(s => s.owner === port.owner && s.kind === kind).length >= (kind === 'war' ? 4 : 5)) return 'Достигнут предел судов';
  const w = nearestWater(S, port.x, port.y, 3, null); if (!w) return 'Нет выхода в море';
  pay(p, c); const sh = { id: S.nid++, owner: port.owner, kind, x: w.x + 0.5, y: w.y + 0.5, path: [], cargo: null, st: 'dock', home: port.id, landing: null, to: null, wait: 0, face: 1 }; S.ships.push(sh); p.flags.ship = 1; if (kind === 'war') p.flags.warship = 1; return null;
}
function orderShip(S, sh, tx, ty) {
  if (!(tx >= 0 && ty >= 0 && tx < S.W && ty < S.H)) return 'Туда нельзя: край карты';
  let gx = Math.floor(tx), gy = Math.floor(ty), landing = null; const sx = Math.floor(sh.x), sy = Math.floor(sh.y), cp = S.comp[sy * S.W + sx];
  if (!isWater(S.ter[gy * S.W + gx])) { landing = { x: gx, y: gy }; const w = nearestWater(S, gx, gy, 3, cp); if (!w) return 'Нет подхода с моря'; gx = w.x; gy = w.y; }
  if (S.comp[gy * S.W + gx] !== cp) return 'Нет морского пути';
  const path = astar(S, sx, sy, gx, gy, true); if (!path) return 'Путь не найден';
  sh.path = path; sh.st = path.length ? 'sail' : 'dock'; sh.landing = landing;
  if (!path.length && sh.cargo && landing) return unloadShip(S, sh); // корабль уже у этого берега
  return null;
}
function stopShip(sh) { sh.path = []; sh.st = 'dock'; sh.landing = null; }
function unloadShip(S, sh) {
  if (!sh.cargo) return 'Корабль пуст'; const a = S.armies.find(q => q.id === sh.cargo); if (!a) { sh.cargo = null; return 'Корабль пуст'; }
  const tgt = sh.landing || { x: sh.x, y: sh.y }; const p = freeTileNear(S, tgt.x, tgt.y, 3, null); if (!p) return 'Рядом нет берега';
  if (dist(sh.x, sh.y, p.x + 0.5, p.y + 0.5) > 4) return 'Подойдите ближе к берегу';
  if (blockedFor(S, a.owner, p.x, p.y)) return 'Нельзя высаживаться на чужой территории без войны или права прохода';
  a.x = p.x + 0.5; a.y = p.y + 0.5; a.ship = null; a.st = 'idle'; a.path = []; a.retreat = false; sh.cargo = null; sh.landing = null; return null;
}
function shipDeliver(S, sh, p) { // торговое судно пришло в порт назначения
  const dst = bidx(S, sh.to);
  if (dst && dst.type === 'port' && dst.owner >= 0 && sh.leg === 'out' && (dst.owner === sh.owner || rel(S, sh.owner, dst.owner).st !== 'war')) {
    const g = 30 + Math.round(sh.len * 1.2); p.stock.gold += g; p.earned += g; p.flags.shipTrade = 1; if (dst.owner !== sh.owner) S.players[dst.owner].stock.gold += g / 2;
    sh.leg = 'back'; sh.to = sh.home; sh.wait = 2;
    const h = bidx(S, sh.home);
    if (h) { const sx = Math.floor(sh.x), sy = Math.floor(sh.y), w2 = nearestWater(S, h.x, h.y, 3, S.comp[sy * S.W + sx]), pp = w2 && astar(S, sx, sy, w2.x, w2.y, true); if (pp && pp.length) { sh.path = pp; sh.st = 'sail'; } }
  } else sh.leg = 'idle';
}
function tickShips(S, dt) {
  for (const sh of S.ships) {
    const p = S.players[sh.owner];
    if (sh.path.length) {
      const w = sh.path[0], dx = w.x + 0.5 - sh.x, dy = w.y + 0.5 - sh.y, d = Math.hypot(dx, dy), step = (sh.kind === 'war' ? 2.0 : 1.7) * dt;
      if (d <= step) { sh.x = w.x + 0.5; sh.y = w.y + 0.5; sh.path.shift(); } else { sh.x += dx / d * step; sh.y += dy / d * step; } if (dx) sh.face = dx > 0 ? 1 : -1;
      if (!sh.path.length) {
        sh.st = 'dock';
        if (sh.kind === 'trade' && sh.to != null) shipDeliver(S, sh, p);
        else if (sh.cargo && sh.landing) { const e = unloadShip(S, sh); if (e && sh.owner === 0) info(S, 0, 'Высадка не удалась', e, 'info'); }
      }
    } else if (sh.kind === 'trade') {
      sh.wait -= dt;
      if (sh.wait <= 0) {
        sh.wait = 4; let h = bidx(S, sh.home);
        if (!h || h.owner !== sh.owner || h.type !== 'port') { h = S.buildings.find(b => b.type === 'port' && b.owner === sh.owner); if (h) sh.home = h.id; } // порт приписки потерян — ищем другой
        if (h) {
          let best = null, bd = 1e9; const sx = Math.floor(sh.x), sy = Math.floor(sh.y), cp = S.comp[sy * S.W + sx];
          for (const b of S.buildings) if (b.type === 'port' && b.owner >= 0 && b.owner !== sh.owner && rel(S, sh.owner, b.owner).trade && !sanActive(S, sh.owner, b.owner, 'ports') && !sanActive(S, b.owner, sh.owner, 'ports') && !sanActive(S, sh.owner, b.owner, 'embargo') && !sanActive(S, b.owner, sh.owner, 'embargo')) { const d = dist(h.x, h.y, b.x, b.y); if (d < bd) { const w = nearestWater(S, b.x, b.y, 3, cp); if (w) { bd = d; best = { b, w }; } } }
          if (best) { const pp = astar(S, sx, sy, best.w.x, best.w.y, true); if (pp && pp.length) { sh.path = pp; sh.st = 'sail'; sh.to = best.b.id; sh.leg = 'out'; sh.len = pp.length; } }
        }
      }
    }
    if (sh.cargo) { const a = S.armies.find(q => q.id === sh.cargo); if (a) { a.x = sh.x; a.y = sh.y; } else sh.cargo = null; }
  }
}

/* ===== Экономика ===== */
function millNear(S, b) { // рядом с фермой (вплотную) стоит своя мельница
  for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { if (!dx && !dy) continue; const x = b.x + dx, y = b.y + dy; if (x < 0 || y < 0 || x >= S.W || y >= S.H) continue; const o = S.occ[y * S.W + x]; if (o < 0 || o >= 1000000) continue; const m = S.bmap.get(o); if (m && m.type === 'mill' && m.owner === b.owner) return true; }
  return false;
}
function farmRate(S, b, p) { return 0.7 * p.mult * (perk(p, 'farm') ? 1.3 : 1) * (millNear(S, b) ? 1.5 : 1); }
function calcRates(S) {
  const n = S.players.length, ports = new Array(n).fill(0), markets = new Array(n).fill(0);
  for (const p of S.players) p.rate = { gold: 0, wood: 0, food: 0, iron: 0 };
  for (const c of S.cities) if (c.owner >= 0) { const p = S.players[c.owner], r = p.rate; r.gold += (0.6 + 0.45 * c.level) * (perk(p, 'gold') ? 1.25 : 1); r.food -= 0.1 * c.level; }
  for (const b of S.buildings) {
    if (b.owner < 0) continue; const p = S.players[b.owner], r = p.rate, m = p.mult;
    if (b.type === 'farm') r.food += farmRate(S, b, p) * (sanOn(S, b.owner, 'food') ? 0.7 : 1);
    else if (b.type === 'sawmill') { let f = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const X = b.x + dx, Y = b.y + dy; if (X >= 0 && Y >= 0 && X < S.W && Y < S.H && S.ter[Y * S.W + X] === TR.FOREST) f++; } r.wood += (0.45 + 0.08 * f) * m * (perk(p, 'wood') ? 1.3 : 1); }
    else if (b.type === 'mine') { const v = 0.45 * m * (perk(p, 'mine') ? 1.3 : 1) * (b.res !== RES.GOLD && sanOn(S, b.owner, 'ore') ? 0.7 : 1); if (b.res === RES.GOLD) r.gold += v; else r.iron += v; }
    else if (b.type === 'market') { r.gold += 0.7 * m; markets[b.owner]++; }
    else if (b.type === 'port') { r.gold += 0.3 * m * (perk(p, 'sea') ? 3 : 1) * (sanOn(S, b.owner, 'ports') ? 0.5 : 1); ports[b.owner]++; }
  }
  for (const a of S.armies) { const r = S.players[a.owner].rate; r.food -= 0.12 * a.sq.length; r.gold -= 0.06 * a.sq.length; }
  // торговля: только между живыми державами; рынки её усиливают, пошлины делят доход
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) if (S.rel[i][j].trade && S.players[i].alive && S.players[j].alive) for (const k of [i, j]) {
    let g = 0.5 + 0.25 * Math.min(ports[i], ports[j]) + 0.05 * Math.min(4, markets[k]);
    for (const s of (S.sanctions || [])) if (s.k === 'tariff' && s.until > S.time && s.b === k && (s.a === i || s.a === j) && S.players[s.a].alive) { S.players[s.a].rate.gold += g * 0.5; g *= 0.5; break; }
    S.players[k].rate.gold += g;
  }
  for (const p of S.players) if (p.alive && sanOn(S, p.id, 'gold') && p.rate.gold > 0) p.rate.gold *= 0.75;
}
function tickEcon(S, dt) {
  for (const p of S.players) {
    if (!p.alive) continue; const s = p.stock;
    for (const k in p.rate) { const v = p.rate[k] * dt; s[k] += v; if (v > 0) p.earned += v * RVAL[k]; if (s[k] < 0) s[k] = 0; }
    if (s.food <= 0.01) { for (const a of S.armies.slice()) if (a.owner === p.id) { for (const q of a.sq) for (const x of q.s) x.hp -= 0.15 * dt; reapArmy(S, a, -1, false); } if (S.time - p.lastFood > 70) { p.lastFood = S.time; addMsg(S, p.id, { type: 'econ', title: 'Голод!', text: 'Кончилась еда — войска слабеют. Стройте фермы.' }); } }
    if (s.gold <= 0.5 && S.time - p.lastGold > 90) { p.lastGold = S.time; addMsg(S, p.id, { type: 'econ', title: 'Казна пуста', text: 'Золота нет, расходы на армию не покрыты.' }); }
    for (const k of ITEM_KEYS) if (s[k] > ITEM_CAP) s[k] = ITEM_CAP;
  }
  if (S.opts.sandbox) { const s0 = S.players[0].stock; for (const k of STOCK_KEYS) s0[k] = 99999; }
}
function spawnSquad(S, b, t, cls) { // новый отряд выходит из здания
  const pt = rallyPos(S, b), sq0 = mkSquad(t); if (cls) sq0.cls = cls;
  const na = newArmy(S, b.owner, pt.x, pt.y, [sq0]); S.players[b.owner].flags.squad = 1;
  if (b.type !== 'barracks') { na.born = S.time; na.from = { x: b.x + 0.5, y: b.y + 0.95 }; na.bdur = 1.8; }
  return na;
}
function tickProd(S, dt) {
  for (const b of S.buildings) {
    if (b.owner < 0) continue; const p = S.players[b.owner]; if (!p.alive) continue;
    const rc = RECIPES[b.type];
    if (rc) {
      const r = rc[b.recipe]; if (!r) continue;
      if (!b.busy) {
        if (b.off) b.status = 'Производство остановлено';
        else if (S.opts.sandbox && p.human) b.status = 'В песочнице склад бесконечен';
        else if ((p.stock[r.out] || 0) >= ITEM_CAP) b.status = 'Склад полон: ' + ITEM_CAP;
        else if (canAfford(p, r.in)) { pay(p, r.in); b.busy = true; b.pt = 0; b.status = ''; } else b.status = 'Не хватает ресурсов';
      } else { b.pt += dt * p.mult * (perk(p, 'craft') ? 1.3 : 1) * (sanOn(S, b.owner, 'arms') ? 0.6 : 1); if (b.pt >= r.t) { p.stock[r.out]++; p.earned += RVAL[r.out]; b.busy = false; } }
    } else if (TRAIN_BLD[b.type] && b.queue.length) {
      const q = b.queue[0]; q.left -= dt * p.mult * (perk(p, 'drill') ? 1.25 : 1);
      if (q.left <= 0) {
        b.queue.shift(); spawnSquad(S, b, q.t, q.cls);
        if (b.owner === 0) info(S, 0, b.type === 'siege' ? 'Машина готова' : b.type === 'academy' ? 'Полководец готов' : 'Отряд готов', SQ[q.t].n + ' — ' + SQ[q.t].rdy + ' к бою.', 'info');
      }
    }
  }
}
// смена изделия: сырьё за незаконченное изделие возвращается
function setRecipe(S, b, k) {
  if (!b || b.owner < 0 || !RECIPES[b.type] || !RECIPES[b.type][k]) return 'Нельзя'; if (b.recipe === k) return null;
  if (b.busy) { const p = S.players[b.owner], r = RECIPES[b.type][b.recipe]; if (r) for (const q in r.in) p.stock[q] += r.in[q]; b.busy = false; b.pt = 0; }
  b.recipe = k; return null;
}
// наёмники: три вида на выбор, набор меняется каждые сутки
function tavernOffer(S, b) {
  const ks = ['archer', 'spear', 'light', 'cav', 'heavy'], r = mulberry32(S.seed + b.id * 131 + Math.floor(S.time / DAY) * 977);
  const a = ks[(r() * ks.length) | 0]; let c = ks[(r() * ks.length) | 0]; if (c === a) c = ks[(ks.indexOf(a) + 1) % ks.length];
  return ['militia', a, c];
}
function hireMerc(S, b, t) {
  if (!b || b.type !== 'tavern' || b.owner < 0) return 'Нужна своя таверна'; const p = S.players[b.owner];
  if (sanOn(S, b.owner, 'mercs')) return 'Найм наёмников запрещён санкциями';
  if (!MERC[t] || !tavernOffer(S, b).includes(t)) return 'Таких наёмников сегодня нет';
  if ((b.cd || 0) > S.time) return 'Новые наёмники подойдут через ' + Math.ceil(b.cd - S.time) + ' с';
  if (!canAfford(p, { gold: MERC[t] })) return 'Не хватает золота: нужно ' + MERC[t];
  pay(p, { gold: MERC[t] }); b.cd = S.time + 45; spawnSquad(S, b, t); p.flags.merc = 1; return null;
}
function rallyPos(S, b) { // куда выходят новые отряды: свободная клетка под зданием или рядом
  const p = freeTileNear(S, b.x, b.y + 1, 4, landComp(S, b.x, b.y)) || freeTileNear(S, b.x, b.y, 6, null) || { x: b.x, y: b.y };
  return { x: p.x + 0.5, y: p.y + 0.5 };
}
function hasCommander(S, pid) { for (const a of S.armies) if (a.owner === pid) for (const q of a.sq) if (q.t === 'cmd') return a; for (const b of S.buildings) if (b.owner === pid && b.type === 'academy' && b.queue.some(q => q.t === 'cmd')) return true; return null; }
function enqueueCmd(S, b, cls) {
  if (!b || b.type !== 'academy' || b.owner < 0 || !CMD[cls]) return 'Нельзя'; const p = S.players[b.owner], d = SQ.cmd;
  if (hasCommander(S, b.owner)) return 'Полководец у вас уже есть'; const cost = Object.assign({ gold: d.gold, food: d.food }, d.need);
  if (!canAfford(p, cost)) return 'Не хватает: ' + costText(cost); pay(p, cost); b.queue.push({ t: 'cmd', cls, left: d.t, total: d.t }); return null;
}
function enqueue(S, b, t) {
  const d = SQ[t]; if (!b || !d || t === 'cmd' || d.bld !== b.type || b.owner < 0) return 'Этот отряд здесь не обучают';
  const p = S.players[b.owner]; if (b.queue.length >= 3) return 'Очередь заполнена';
  const cost = Object.assign({ gold: d.gold, food: d.food }, d.need); if (!canAfford(p, cost)) return 'Не хватает: ' + costText(cost);
  pay(p, cost); b.queue.push({ t, left: d.t, total: d.t }); return null;
}

/* ===== Бой ===== */
function defMult(S, a) { const t = S.ter[Math.floor(a.y) * S.W + Math.floor(a.x)]; return t === TR.FOREST ? 0.85 : t === TR.HILL ? 0.8 : t === TR.MOUNT ? 0.7 : 1; }
function cleanArmy(S, a, killer) {
  a.sq = a.sq.filter(q => q.hp > 0 && q.u > 0);
  if (!a.sq.length) {
    S.armies = S.armies.filter(q => q !== a);
    if (killer >= 0) { S.players[killer].wins++; if (killer === 0) info(S, 0, 'Победа в сражении', 'Армия ' + pname(S, a.owner) + ' разбита.', 'info'); }
    if (a.owner === 0) info(S, 0, 'Армия уничтожена', killer >= 0 ? 'Враг разбил ваш отряд.' : 'Отряд погиб.', 'war');
    return true;
  }
  return false;
}
function cmdList(S) { const l = []; for (const a of S.armies) { if (a.ship) continue; for (const q of a.sq) if (q.t === 'cmd' && q.s.length) { const s = q.s[0]; l.push({ o: a.owner, cls: q.cls, x: s.x !== undefined ? s.x : a.x, y: s.y !== undefined ? s.y : a.y }); } } return l; }
function auraAt(cl, o, t, x, y) { for (const c of cl) if (c.o === o && c.cls === t && Math.hypot(c.x - x, c.y - y) <= AURA_R) return true; return false; }
const CMDST = {}; function stOf(u) { if (u.t !== 'cmd') return SQ[u.t]; return CMDST[u.cls] || (CMDST[u.cls] = Object.assign({}, SQ[u.cls], { atk: SQ[u.cls].atk * 1.8, hp: SQ.cmd.hp })); }
function moveSoldier(S, s, tx, ty, step, o) { // шаг бойца; возвращает укрепление, которое преградило путь
  const mi = Math.floor(s.y) * S.W + Math.floor(s.x); if (S.mm[mi] === 1) step *= 0.55;
  const dx = tx - s.x, dy = ty - s.y, d = Math.hypot(dx, dy) || 1, k = Math.min(1, step / d), nx = s.x + dx * k, ny = s.y + dy * k;
  if (!structBlockedAt(S, o, nx, ny)) { s.x = nx; s.y = ny; return null; }
  const bl = structAtTile(S, Math.floor(nx), Math.floor(ny));
  if (!structBlockedAt(S, o, nx, s.y)) { s.x = nx; return bl; } if (!structBlockedAt(S, o, s.x, ny)) { s.y = ny; return bl; } return bl;
}
function tickCombat(S, dt) {
  const arm = S.armies.filter(a => !a.ship);
  for (const a of arm) { a.fight = false; a.ft = null; a.bat = false; }
  const towers = S.structs.filter(b => b.type === 'tower' && b.gar && b.gar.s.length);
  for (const a of arm) {
    if (a.siege != null) { const T = bidx(S, a.siege); if (!T || !atWar(S, a.owner, T.owner)) { a.siege = null; a.sg = false; } else if (dist(a.x, a.y, T.x + 0.5, T.y + 0.5) <= siegeReach(a) && !a.retreat) { a.sg = true; a.path = []; if (a.st === 'move') a.st = 'idle'; } }
    let t = null, td = 99; for (const b of arm) if (b !== a && atWar(S, a.owner, b.owner)) { const d = dist(a.x, a.y, b.x, b.y); if (d < td) { td = d; t = b; } }
    if (t && td <= 3.4) { a.bat = true; t.bat = true; a.ft = { x: t.x, y: t.y }; if (!t.ft) t.ft = { x: a.x, y: a.y };
      if (td <= 2.4) { if (!a.retreat) { a.fight = true; a.path = []; a.st = 'fight'; } if (!t.retreat) { t.fight = true; t.path = []; t.st = 'fight'; } } }
    for (const tw of towers) if (atWar(S, a.owner, tw.owner) && dist(a.x, a.y, tw.x + 0.5, tw.y + 0.5) <= 4.4) { a.bat = true; break; }
  }
  const U = [];
  for (const a of arm) {
    if (!a.bat && !a.sg) { if (a.wasBat) { a.wasBat = false; for (const q of a.sq) for (const s of q.s) { s.x = undefined; s.y = undefined; } } continue; }
    if (!a.wasBat) { a.wasBat = true; a.pk = units(a); }
    a._cb = combo(a);
    const list = armySoldiers(a), slots = formSlots(list.length).f;
    list.forEach((e, i) => { const s = e.s; if (s.x === undefined) { s.x = a.x + slots[i].ox / TS; s.y = a.y + slots[i].oy / TS; } U.push({ s, a, t: e.t, cls: e.cls, tt: e.t === 'cmd' ? e.cls : e.t, slot: slots[i] }); });
  }
  const dm = [], sdm = [], gdm = [], CL = cmdList(S);
  const hostileStructNear = (s, o, r) => { let best = null, bd = r; for (const b of S.structs) { if (b.owner === o || b.owner < 0 || !atWar(S, o, b.owner)) continue; const d = Math.hypot(b.x + 0.5 - s.x, b.y + 0.5 - s.y); if (d < bd) { bd = d; best = b; } } return best; };
  const bashFx = (s, b) => { if (!s.cd2 || s.cd2 <= S.time) { s.cd2 = S.time + 0.7 + S.rnd() * 0.4; S.fx.push({ k: 'bash', x: b.x + 0.5 + (S.rnd() - 0.5) * 0.5, y: b.y + 0.5 + (S.rnd() - 0.5) * 0.3, t: S.time }); } };
  for (const u of U) {
    const s = u.s, a = u.a, st = stOf(u), o = a.owner, sx = a.x + u.slot.ox / TS, sy = a.y + u.slot.oy / TS;
    if (a.retreat) { s.x = sx; s.y = sy; continue; }
    const mi = Math.floor(s.y) * S.W + Math.floor(s.x);
    if (S.mm[mi] === 1) { const mb = S.bmap.get(S.occ[mi]); if (mb && mb.owner !== o && !allied(S, o, mb.owner) && atWar(S, o, mb.owner)) { if (!s.mD) s.mD = 0.03 + S.rnd() * 0.04; s.mU = S.time + 5; } }
    if (s.mU && S.time > s.mU) { s.mD = 0; s.mU = 0; }
    const out = (1 - (s.mD || 0)) * a._cb * (S.players[o].human ? 1 : S.players[o].mult) * (CL.length && auraAt(CL, o, u.tt, s.x, s.y) ? AURA_ATK : 1);
    const dfa = e0 => (CL.length && auraAt(CL, e0.a.owner, e0.tt, e0.s.x, e0.s.y)) ? AURA_DEF : 1;
    let e = null, ed = 99; for (const v of U) { if (v.a.owner === o || !atWar(S, o, v.a.owner)) continue; const d = Math.hypot(v.s.x - s.x, v.s.y - s.y); if (d < ed) { ed = d; e = v; } }
    const T = a.siege != null ? bidx(S, a.siege) : null;
    const strike = (b, dps) => { sdm.push([b, dps * dt * (1 - (s.mD || 0)), o]); s.atk = S.time; bashFx(s, b); };
    if (u.t === 'ram') {
      const tg = T || hostileStructNear(s, o, 5);
      if (tg) { const cx = tg.x + 0.5, cy = tg.y + 0.5, d = Math.hypot(cx - s.x, cy - s.y); s.face = cx >= s.x ? 1 : -1;
        if (d > 0.9) { const bl = moveSoldier(S, s, cx, cy, st.spd * dt, o); if (bl && bl.owner !== o && atWar(S, o, bl.owner)) strike(bl, RAM_DPS[bl.type] || 20); } else strike(tg, RAM_DPS[tg.type] || 20); continue; }
    }
    if (u.t === 'cat') {
      let tgS = null, tgU = null; if (e && ed <= st.range) tgU = e; else if (T && Math.hypot(T.x + 0.5 - s.x, T.y + 0.5 - s.y) <= st.range) tgS = T;
      const goal = tgU || tgS || (e && ed <= 6 ? { s: e.s } : T ? { s: { x: T.x + 0.5, y: T.y + 0.5 } } : null);
      if (!tgU && !tgS) { if (goal) { const gd = Math.hypot(goal.s.x - s.x, goal.s.y - s.y); if (gd > st.range * 0.85) moveSoldier(S, s, goal.s.x, goal.s.y, st.spd * dt, o); } else { const dx = sx - s.x, dy = sy - s.y, d = Math.hypot(dx, dy); if (d > 0.02) { const k = Math.min(1, st.spd * dt / d); s.x += dx * k; s.y += dy * k; } } continue; }
      if (!s.cd || s.cd <= S.time) {
        s.cd = S.time + 3 + S.rnd() * 0.5; s.atk = S.time; const tx = tgU ? tgU.s.x : tgS.x + 0.5, ty = tgU ? tgU.s.y : tgS.y + 0.5; S.fx.push({ k: 'rock', x0: s.x, y0: s.y, x1: tx, y1: ty, t: S.time, d: 0.9 });
        if (tgU) { for (const v of U) { if (v.a.owner === o || !atWar(S, o, v.a.owner)) continue; if (Math.hypot(v.s.x - tx, v.s.y - ty) <= 0.85) dm.push([v.s, 24 * mm('cat', v.tt) * (v.tt === 'heavy' ? 0.75 : 1) * out, o, v.a, S.time + 0.9]); }
          for (const tw of towers) if (atWar(S, o, tw.owner) && Math.hypot(tw.x + 0.5 - tx, tw.y + 0.5 - ty) <= 0.9) sdm.push([tw, CAT_DMG.tower * out, o, S.time + 0.9]); }
        else { sdm.push([tgS, (CAT_DMG[tgS.type] || 28) * (1 - (s.mD || 0)), o, S.time + 0.9]); if (tgS.type === 'tower' && tgS.gar) for (const gs of tgS.gar.s) gdm.push([gs, 5, o, tgS, S.time + 0.9]); }
      }
      continue;
    }
    // обычные бойцы
    const reach = u.tt === 'archer' ? st.range * 0.9 : 0.3;
    let twc = null, twd = 99;
    if (u.tt === 'archer') for (const tw of towers) if (atWar(S, o, tw.owner)) { const d = Math.hypot(tw.x + 0.5 - s.x, tw.y + 0.5 - s.y); if (d < twd) { twd = d; twc = tw; } }
    if (twc && twd <= reach && (!e || twd < ed)) { // лучник стреляет по гарнизону башни (защита +40%)
      const gs = twc.gar.s[(S.rnd() * twc.gar.s.length) | 0]; gdm.push([gs, st.atk * dt * mm('archer', 'archer') * BAT * out * 0.6, o, twc, 0]); s.atk = S.time; s.face = twc.x + 0.5 >= s.x ? 1 : -1;
      if (!s.cd || s.cd <= S.time) { s.cd = S.time + 0.85 + S.rnd() * 0.3; S.fx.push({ k: 'arr', x0: s.x, y0: s.y, x1: twc.x + 0.5, y1: twc.y + 0.3, t: S.time, d: 0.3 + twd * 0.1 }); } continue;
    }
    if (e && ed <= 4.2) {
      s.face = e.s.x >= s.x ? 1 : -1;
      if (ed > reach) { const bl = moveSoldier(S, s, e.s.x, e.s.y, st.spd * 0.85 * dt, o); if (bl && bl.owner !== o && atWar(S, o, bl.owner) && u.tt !== 'archer') strike(bl, st.atk * 0.09); }
      else {
        dm.push([e.s, st.atk * dt * mm(u.tt, e.tt) * defMult(S, e.a) * BAT * out * dfa(e), o, e.a, 0]); s.atk = S.time;
        if (!s.cd || s.cd <= S.time) { s.cd = S.time + (u.tt === 'archer' ? 0.85 : 0.65) + S.rnd() * 0.3; if (u.tt === 'archer') S.fx.push({ k: 'arr', x0: s.x, y0: s.y, x1: e.s.x + (S.rnd() - 0.5) * 0.2, y1: e.s.y, t: S.time, d: 0.3 + ed * 0.1 }); else S.fx.push({ k: 'hit', x: (s.x + e.s.x) / 2, y: (s.y + e.s.y) / 2, t: S.time }); }
      }
      continue;
    }
    if (T && u.tt !== 'archer') { const cx = T.x + 0.5, cy = T.y + 0.5, d = Math.hypot(cx - s.x, cy - s.y); s.face = cx >= s.x ? 1 : -1; if (d > 0.9) { const bl = moveSoldier(S, s, cx, cy, st.spd * 0.85 * dt, o); if (bl && bl.owner !== o && atWar(S, o, bl.owner)) strike(bl, st.atk * 0.09); } else strike(T, st.atk * 0.09); continue; }
    const dx = sx - s.x, dy = sy - s.y, d = Math.hypot(dx, dy); if (d > 0.02) { const k = Math.min(1, 1.1 * dt / d); s.x += dx * k; s.y += dy * k; }
  }
  const dfa2 = e0 => (CL.length && auraAt(CL, e0.a.owner, e0.tt, e0.s.x, e0.s.y)) ? AURA_DEF : 1;
  // гарнизоны башен стреляют по врагам рядом
  for (const tw of towers) {
    const tx = tw.x + 0.5, ty = tw.y + 0.5, ow = S.players[tw.owner];
    let e = null, ed = 3.7; for (const v of U) { if (v.a.owner === tw.owner || !atWar(S, tw.owner, v.a.owner)) continue; const d = Math.hypot(v.s.x - tx, v.s.y - ty); if (d < ed) { ed = d; e = v; } }
    if (!e) continue;
    for (const g of tw.gar.s) {
      g.atk = S.time; const mult = SQ.archer.atk * dt * mm('archer', e.tt) * BAT * 1.15 * defMult(S, e.a) * dfa2(e) * (ow && ow.human ? 1 : ow ? ow.mult : 1);
      dm.push([e.s, mult, tw.owner, e.a, 0]);
      if (!g.cd || g.cd <= S.time) { g.cd = S.time + 0.8 + S.rnd() * 0.3; S.fx.push({ k: 'arr', x0: tx, y0: ty - 0.3, x1: e.s.x, y1: e.s.y, t: S.time, d: 0.3 + ed * 0.1 }); }
    }
  }
  // применяем урон (отложенные удары каменных ядер — по времени падения)
  S.pend = (S.pend || []).concat(dm.filter(d => d[4] > S.time).map(d => ({ k: 'u', d })), sdm.filter(d => d[3] > S.time).map(d => ({ k: 's', d })), gdm.filter(d => d[4] > S.time).map(d => ({ k: 'g', d })));
  const rest = []; for (const p of S.pend) { if (p.d[p.k === 'u' ? 4 : p.k === 's' ? 3 : 4] <= S.time) { if (p.k === 'u') dm.push(p.d); else if (p.k === 's') sdm.push(p.d); else gdm.push(p.d); } else rest.push(p); } S.pend = rest;
  for (const d of dm) { if (d[4] > S.time) continue; d[0].hp -= d[1] * (1 + (d[0].mD || 0)); d[3].lastBy = d[2]; }
  for (const d of sdm) { if (d[3] > S.time) continue; d[0].hp -= d[1]; d[0].lastBy = d[2]; }
  for (const d of gdm) { if (d[4] > S.time) continue; d[0].hp -= d[1]; d[3].lastBy = d[2]; }
  for (const tw of towers) { const q = tw.gar; let dead = false; for (let i = q.s.length - 1; i >= 0; i--) if (q.s[i].hp <= 0) { q.s.splice(i, 1); dead = true; if (tw.lastBy != null && S.players[tw.lastBy]) S.players[tw.lastBy].kills++; } syncSq(q); if (!q.s.length) tw.gar = null; else if (dead) q.w = Math.min(q.w + 1, 9); }
  for (const b of S.structs.slice()) if (b.hp <= 0) destroyStruct(S, b, b.lastBy != null ? b.lastBy : -1);
  for (const a of arm.slice()) { if (a.sq.some(q => q.s.some(x => x.hp <= 0))) reapArmy(S, a, a.lastBy != null ? a.lastBy : -1, true); }
  for (const a of arm) { if (a.st === 'fight' && !a.fight) a.st = 'idle'; if (a.bat || a.sg) for (const q of a.sq) syncSq(q); }
  S.prompts = S.prompts || [];
  if (S.players[0].human) for (const a of arm) if (a.owner === 0 && a.bat && !a.retreat && S.armies.includes(a)) { const n = units(a); if (a.pk && n <= a.pk * 0.55 && n < armyCap(a) * 0.7 && (!a.askT || S.time > a.askT) && !S.prompts.some(p => p.id === a.id)) { a.askT = S.time + 80; S.prompts.push({ id: a.id }); } }
  if (S.fx.length > 240) S.fx.splice(0, S.fx.length - 240);
}
function arrive(S, a) { // армия дошла до цели
  a.st = 'idle'; a.retreat = false;
  if (a.garrison != null) { const T = bidx(S, a.garrison); a.garrison = null; if (T && dist(a.x, a.y, T.x + 0.5, T.y + 0.5) < 2.4) garrisonSquad(S, a, T); if (!a.sq.length) return; }
  if (a.siege != null) { const T = bidx(S, a.siege); if (T && dist(a.x, a.y, T.x + 0.5, T.y + 0.5) <= siegeReach(a) + 0.7) a.sg = true; }
  if (a.boardPort != null) {
    const port = bidx(S, a.boardPort); a.boardPort = null;
    if (port && port.owner === a.owner) {
      const sh = S.ships.find(s => s.owner === a.owner && s.kind === 'war' && !s.cargo && s.st === 'dock' && dist(s.x, s.y, port.x + 0.5, port.y + 0.5) < 4);
      if (sh) { sh.cargo = a.id; a.ship = sh.id; a.x = sh.x; a.y = sh.y; if (a.owner === 0) info(S, 0, 'Погрузка', 'Войска на борту. Выберите корабль и укажите берег.', 'info'); }
      else if (a.owner === 0) info(S, 0, 'Корабля нет', 'В порту нет свободного военного корабля — постройте его или дождитесь возвращения.', 'info');
    }
  }
}
function tickMove(S, dt) {
  for (const a of S.armies) {
    if (a.ship) continue;
    if (blockedFor(S, a.owner, Math.floor(a.x), Math.floor(a.y))) {
      a.tres = (a.tres || 0) + dt;
      if (a.tres > 25 && !a.fight) { a.tres = 0; const was = a.retreat, e = retreat(S, a); if (!e && !was && a.owner === 0) info(S, 0, 'Отход армии', 'Армия покидает чужую территорию.', 'info'); }
    } else a.tres = 0;
    if (a.fight) continue;
    if (!a.path.length) { if (a.arrived) { a.arrived = false; arrive(S, a); } else if (a.st === 'move') a.st = 'idle'; continue; }
    const w = a.path[0], cx0 = Math.floor(a.x), cy0 = Math.floor(a.y);
    if (structBlockedFor(S, a.owner, w.x, w.y)) { stopArmy(a); continue; }
    if (blockedFor(S, a.owner, w.x, w.y) && !blockedFor(S, a.owner, cx0, cy0)) { stopArmy(a); if (a.owner === 0) info(S, 0, 'Армия остановлена', 'Граница закрыта: нужна война или право прохода.', 'info'); continue; }
    const dx = w.x + 0.5 - a.x, dy = w.y + 0.5 - a.y, d = Math.hypot(dx, dy), step = armySpd(S, a) * dt;
    if (dx) a.face = dx > 0 ? 1 : -1;
    if (d <= step) { a.x = w.x + 0.5; a.y = w.y + 0.5; a.path.shift(); } else { a.x += dx / d * step; a.y += dy / d * step; }
    if (!a.path.length) { a.arrived = false; arrive(S, a); }
  }
}
function tickCities(S, dt) {
  for (const c of S.cities) {
    const cx = c.x + 1, cy = c.y + 1, att = []; let def = 0;
    for (const a of S.armies) { if (a.ship || dist(a.x, a.y, cx, cy) > 2.6) continue; if (a.owner === c.owner) def++; else if (atWar(S, a.owner, c.owner)) att.push(a); }
    if (att.length) { // стрелки города бьют по самому слабому бойцу; у вольных городов оборона крепче
      const tg = att[0], dps = c.owner < 0 ? 6 + 4 * (c.level - 1) : 2.4 * c.level; let v = null;
      for (const q of tg.sq) for (const s of q.s) if (!v || s.hp < v.hp) v = s;
      if (v) { v.hp -= dps * dt; if (v.hp <= 0) reapArmy(S, tg, c.owner, true); }
    }
    const live = att.filter(a => a.sq.length);
    if (!live.length) { if (c.cap) { c.cap.p -= 0.02 * dt; c.cap.hold = 0; if (c.cap.p <= 0) c.cap = null; } continue; }
    if (def) continue;
    const byO = {}; live.forEach(a => byO[a.owner] = (byO[a.owner] || 0) + units(a));
    let A = -1, bu = -1; for (const k in byO) if (byO[k] > bu) { bu = byO[k]; A = +k; }
    const rate = Math.min(0.12, (bu / garrison(c)) / 20);
    if (c.cap && c.cap.by !== A) { c.cap.p -= rate * dt; if (c.cap.p <= 0) c.cap = { by: A, p: 0, hold: 0 }; }
    else {
      if (!c.cap) c.cap = { by: A, p: 0, hold: 0 };
      c.cap.p = Math.min(1, c.cap.p + rate * dt);
      if (c.cap.p >= 1) { c.cap.hold += dt; if (c.cap.hold >= 6) flipCity(S, c, A); }
    }
  }
}
function flipCity(S, c, A) {
  const old = c.owner; c.owner = A; c.cap = null; c.free = false; const ap = S.players[A]; ap.captured++; ap.flags.capture = 1;
  if (old < 0) ap.flags.freecap = 1; if (c.isCap && c.home !== A) ap.flags.capcap = 1; if (ap.ai) ap.ai.fails = {};
  if (c.level > 1) c.level--; if (old >= 0) c.radius = Math.max(3, c.radius * 0.7);
  addMsg(S, A, { type: 'info', title: 'Город захвачен!', text: c.name + ' теперь под вашим флагом.' });
  if (old >= 0) addMsg(S, old, { type: 'war', title: 'Город потерян!', text: pname(S, A) + ' захватил ' + c.name + '.' });
  recomputeTerr(S);
}
function tickTerr(S, dt) {
  let ch = false;
  for (const c of S.cities) { const m = cityMaxR(c); if (c.radius < m) { const o = Math.floor(c.radius * 2); c.radius = Math.min(m, c.radius + 0.1 * dt); if (Math.floor(c.radius * 2) !== o) ch = true; } else if (c.radius > m + 0.01) { c.radius = m; ch = true; } }
  if (ch) recomputeTerr(S);
}

/* ===== Очки и победа ===== */
function calcScore(S) {
  for (const p of S.players) {
    let eco = 0; for (const b of S.buildings) if (b.owner === p.id) eco += BD[b.type].pts; for (const c of S.cities) if (c.owner === p.id) eco += 60 * c.level + (c.isCap ? 100 : 0);
    eco += Math.floor(p.earned / 2);
    let mil = p.kills * 3 + p.wins * 120 + p.captured * 400; for (const a of S.armies) if (a.owner === p.id) mil += units(a) * 2;
    p.eco = Math.floor(eco); p.mil = Math.floor(mil); p.score = p.eco + p.mil;
  }
}
function killPlayer(S, p) { // держава пала: войска расходятся, договоры теряют силу
  S.armies = S.armies.filter(a => a.owner !== p.id); S.ships = S.ships.filter(s => s.owner !== p.id);
  for (const b of S.buildings) if (b.owner === p.id) { b.owner = -1; b.busy = false; b.queue = []; b.gar = null; }
  for (const q of S.players) if (q.id !== p.id) { const r = rel(S, p.id, q.id); r.st = 'peace'; r.trade = false; r.ally = false; r.cl = {}; r.pass = {}; r.truceUntil = 0; }
  if (S.pending) S.pending = S.pending.filter(q => q.from !== p.id && q.to !== p.id);
  for (const m of S.msgs) if (m.kind && m.kind !== 'event' && m.from === p.id && !m.done) { m.done = true; m.late = true; }
  refreshStructs(S); S.sv = (S.sv || 0) + 1;
  info(S, 0, 'Государство пало', p.name + ' больше не существует.', 'info');
}
function checkVictory(S) {
  if (S.over) return;
  for (const p of S.players) { const was = p.alive; p.alive = S.cities.some(c => c.owner === p.id); if (was && !p.alive) killPlayer(S, p); }
  const alive = S.players.filter(p => p.alive), top = l => l.slice().sort((a, b) => b.score - a.score)[0];
  if (!S.players[0].alive) { S.over = { winner: alive.length ? top(alive).id : -1, why: 'Вы потеряли все города' }; return; }
  if (S.opts.sandbox) return;
  if (S.opts.win === 'score') { const w = alive.filter(p => p.score >= S.opts.target); if (w.length) { S.over = { winner: top(w).id, why: 'Набрано ' + S.opts.target + ' очков' }; return; } }
  else { const caps = S.cities.filter(c => c.isCap), own = new Set(caps.map(c => c.owner)); if (caps.length > 1 && own.size === 1 && !own.has(-1)) { S.over = { winner: caps[0].owner, why: 'Захвачены все столицы' }; return; } }
  if (alive.length === 1 && S.players.length > 1) S.over = { winner: alive[0].id, why: 'Остался единственный правитель' };
}

/* ===== Главный шаг ===== */
/* ===== События ===== */
function capitalOf(S, pid) { return S.cities.find(c => c.owner === pid && c.isCap && c.home === pid) || S.cities.find(c => c.owner === pid); }
function giftSquad(S, p, t) { const c = capitalOf(S, p.id); if (!c) return null; const pt = freeTileNear(S, c.x, c.y + 2, 4, null) || { x: c.x, y: c.y }; newArmy(S, p.id, pt.x + 0.5, pt.y + 0.5, [mkSquad(t)]); p.flags.squad = 1; return c; }
const EVENTS = [
  { id: 'harvest', w: 3, good: 1, title: 'Богатый урожай', run(S, p, r) { const n = 60 + ((r() * 61) | 0); p.stock.food += n; return 'Амбары полны: +' + n + ' еды.'; } },
  { id: 'caravan', w: 3, good: 1, title: 'Купеческий караван', run(S, p, r) { const n = 80 + ((r() * 71) | 0); p.stock.gold += n; p.earned += n; return 'Купцы заплатили пошлину: +' + n + ' золота.'; } },
  { id: 'vein', w: 2, good: 1, title: 'Богатая жила', run(S, p, r) { const n = 40 + ((r() * 41) | 0); p.stock.iron += n; return 'Рудокопы нашли жилу: +' + n + ' железа.'; } },
  { id: 'timber', w: 2, good: 1, title: 'Сплав леса', run(S, p, r) { const n = 60 + ((r() * 61) | 0); p.stock.wood += n; return 'По реке пришли плоты: +' + n + ' дерева.'; } },
  { id: 'volunteers', w: 2, good: 1, title: 'Добровольцы', ok: S => S.time > 120, run(S, p) { const c = giftSquad(S, p, 'militia'); return c ? 'Горожане взялись за оружие: отряд ополчения ждёт у города ' + c.name + '.' : ''; } },
  { id: 'drought', w: 2, title: 'Засуха', ok: (S, p) => p.stock.food > 80, run(S, p) { const n = Math.min(150, Math.max(20, Math.round(p.stock.food * 0.2))); p.stock.food = Math.max(0, p.stock.food - n); return 'Часть урожая погибла: потеряно ' + n + ' еды.'; } },
  { id: 'fire', w: 2, title: 'Пожар на складе', ok: (S, p) => p.stock.wood > 80, run(S, p) { const n = Math.min(120, Math.max(15, Math.round(p.stock.wood * 0.15))); p.stock.wood = Math.max(0, p.stock.wood - n); return 'Огонь уничтожил ' + n + ' дерева.'; } },
  { id: 'thieves', w: 2, title: 'Воры в казне', ok: (S, p) => p.stock.gold > 150, run(S, p) { const n = Math.min(150, Math.max(20, Math.round(p.stock.gold * 0.1))); p.stock.gold = Math.max(0, p.stock.gold - n); return 'Из казны пропало ' + n + ' золота.'; } },
  { id: 'fair', w: 2, choice: 1, cost: { gold: 80 }, title: 'Ярмарка', text: 'Купцы просят 80 золота на большую ярмарку. Соседи оценят гостеприимство — отношения со всеми станут теплее.', ok: S => S.players.length > 1,
    run(S, p) { for (const q of S.players) if (q.id !== p.id && q.alive) rel(S, p.id, q.id).op += 10; return 'Ярмарка удалась: соседи стали относиться к вам теплее.'; } },
  { id: 'mercs', w: 2, choice: 1, cost: { gold: 150 }, title: 'Вольный отряд', text: 'Бродячий отряд воинов предлагает службу за 150 золота.', ok: S => S.time > 200,
    run(S, p, r) { const t = ['archer', 'light', 'spear'][(r() * 3) | 0], c = giftSquad(S, p, t); return c ? SQ[t].n + ' присягнули вам и ждут у города ' + c.name + '.' : ''; } }
];
function tickEvents(S) {
  for (const p of S.players) {
    if (!p.alive) continue; if (!(p.evAt > 0)) p.evAt = S.time + 150 + S.rnd() * 200; if (S.time < p.evAt) continue;
    p.evAt = S.time + 200 + S.rnd() * 180;
    const pool = EVENTS.filter(e => (!e.choice || p.human) && (!e.ok || e.ok(S, p))); if (!pool.length) continue;
    let tw = 0; for (const e of pool) tw += e.w; let r = S.rnd() * tw, ev = pool[0]; for (const e of pool) { r -= e.w; if (r <= 0) { ev = e; break; } }
    if (ev.choice) addMsg(S, p.id, { type: 'econ', kind: 'event', ev: ev.id, title: ev.title, text: ev.text, exp: S.time + DAY });
    else { const txt = ev.run(S, p, S.rnd); if (p.human && txt) { info(S, p.id, ev.title, txt, 'econ'); S.toast.push({ title: ev.title, text: txt, t: S.time, good: !!ev.good, ev: 1 }); } }
  }
}
function answerEvent(S, m, yes) { // ответ игрока на событие с выбором
  const ev = EVENTS.find(e => e.id === m.ev), p = S.players[0];
  if (!ev || !ev.choice || !yes) { m.done = true; m.read = true; return null; }
  if (m.exp && S.time > m.exp) { m.done = true; m.read = true; m.late = true; return 'Срок предложения истёк'; }
  if (!canAfford(p, ev.cost)) return 'Не хватает: ' + costText(ev.cost);
  m.done = true; m.read = true; pay(p, ev.cost); const txt = ev.run(S, p, S.rnd); if (txt) info(S, 0, ev.title, txt, 'econ'); return null;
}

/* ===== Главный шаг ===== */
function tickDiplomacy(S) {
  const n = S.players.length;
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) { const r = S.rel[i][j]; if (r.st === 'truce' && r.truceUntil <= S.time) r.st = 'peace'; }
  if (S.pending && S.pending.length) { const due = S.pending.filter(q => q.at <= S.time); if (due.length) { S.pending = S.pending.filter(q => q.at > S.time); for (const q of due) decideOffer(S, q.from, q.to, q.kind); } }
  // письма без ответа: срок вышел. Молчание в ответ на ультиматум — отказ
  for (const m of S.msgs) if (m.kind && !m.done && m.exp && S.time > m.exp) { m.done = true; m.late = true; if (m.kind === 'ult' && !dealError(S, m.from, 0, 'ult')) startWar(S, m.from, 0); }
}
function tick(S, dt) {
  if (S.over) return; S.time += dt; S.tk++;
  if (S.tk % 4 === 0) { calcRates(S); calcVision(S); }
  tickEcon(S, dt); tickProd(S, dt); tickMove(S, dt); tickCombat(S, dt); tickHospital(S, dt); tickCities(S, dt); tickShips(S, dt); tickTerr(S, dt); tickExpand(S, dt); tickFolk(S, dt);
  if (S.tk % 4 === 1) tickDiplomacy(S);
  if (S.tk % 8 === 0) { for (const p of S.players) if (!p.human && p.alive) aiThink(S, p); calcScore(S); checkVictory(S); }
  if (S.tk % 8 === 4 && S.opts.events && !S.opts.sandbox) tickEvents(S);
}

/* ===== Сохранение ===== */
function b64(u8) { let s = ''; for (let i = 0; i < u8.length; i++) s += String.fromCharCode(u8[i]); return btoa(s); }
function unb64(s) { const b = atob(s), u = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) u[i] = b.charCodeAt(i); return u; }
const SAVE_KEY = 'kc_save';
const SAVE_SLOTS = ['auto', '1', '2', '3', '4', '5'];
function saveKey(slot) { return slot === 'auto' ? SAVE_KEY : SAVE_KEY + '_' + slot; } // auto — старый ключ: старые сохранения работают
function saveMeta(S) { return { name: S.players[0].name, ci: S.players[0].ci, day: Math.floor(S.time / DAY) + 1, score: S.players[0].score, at: Date.now(), win: S.opts.win, type: S.opts.type }; }
function saveGame(S, slot) {
  if (!S || S.over) return false;
  slot = SAVE_SLOTS.indexOf(slot) >= 0 ? slot : 'auto';
  const rels = []; for (let i = 0; i < S.players.length; i++) for (let j = i + 1; j < S.players.length; j++) rels.push([i, j, S.rel[i][j]]);
  const d = { v: 2, gv: GAME_VERSION, at: Date.now(), opts: S.opts, seed: S.seed, W: S.W, H: S.H, ter: b64(S.ter), res: b64(S.res), players: S.players, cities: S.cities, buildings: S.buildings, armies: S.armies, ships: S.ships, msgs: S.msgs, rels, time: S.time, nid: S.nid, plan: S.plan, pending: S.pending || [],
    folk: S.folk || [], extra: S.extra ? Array.from(S.extra.entries()) : [], sanctions: S.sanctions || [], meta: saveMeta(S) };
  try { localStorage.setItem(saveKey(slot), JSON.stringify(d, (k, v) => (k[0] === '_' || k === 'fx') ? undefined : v)); return true; } catch (e) { return false; }
}
// краткие сведения о слотах: показываются в списке сохранений
function readSaveMeta(slot) {
  let raw = null;
  try { raw = localStorage.getItem(saveKey(slot)); } catch (e) { return null; } // хранилище недоступно — это не «повреждено»
  try {
    if (!raw || raw.length < 100 || raw[0] !== '{') return null;
    const d = JSON.parse(raw); if (!d || (d.v !== 1 && d.v !== 2)) return null;
    const m = d.meta && typeof d.meta === 'object' ? d.meta : {};
    return { slot, name: _s(m.name || (Array.isArray(d.players) && d.players[0] && d.players[0].name) || 'Партия', 28), ci: _i(m.ci, 0, PNAMES.length - 1, 0),
      day: _i(m.day, 1, 999999, Math.floor(_n(d.time, 0, 1e9, 0) / DAY) + 1), score: _i(m.score, 0, 1e12, 0), at: _n(m.at, 0, 2e12, 0) };
  } catch (e) { return { slot, broken: true, name: 'Повреждено', ci: 0, day: 1, score: 0, at: 0 }; }
}
function hasSave() { return SAVE_SLOTS.some(s => !!readSaveMeta(s)); }
function dropSave(slot) {
  try {
    if (slot && SAVE_SLOTS.indexOf(slot) >= 0) localStorage.removeItem(saveKey(slot));
    else for (const s of SAVE_SLOTS) localStorage.removeItem(saveKey(s));
  } catch (e) { /* хранилище недоступно */ }
}
function loadGame(slot) {
  loadGame.error = ''; let raw = null;
  slot = SAVE_SLOTS.indexOf(slot) >= 0 ? slot : 'auto';
  try { raw = localStorage.getItem(saveKey(slot)); } catch (e) { loadGame.error = 'Хранилище недоступно'; return null; }
  if (!raw) return null;
  try { return restoreGame(JSON.parse(raw)); } catch (e) { loadGame.error = String((e && e.message) || e); return null; }
}
/* Разбор сохранения. Данным из хранилища не доверяем: каждое поле проверяется и приводится к допустимому
   диапазону, объекты собираются заново. Испорченное сохранение даёт понятную ошибку, а не зависшую игру. */
const _n = (v, lo, hi, def) => { v = +v; return isFinite(v) ? (v < lo ? lo : v > hi ? hi : v) : def; };
const _i = (v, lo, hi, def) => Math.floor(_n(v, lo, hi, def));
const _s = (v, max) => String(v == null ? '' : v).replace(/[<>&"'`\\\u0000-\u001f]/g, '').slice(0, max || 40);
function _fail(m) { throw new Error('Сохранение повреждено: ' + m); }
function _squad(q) {
  if (!q || typeof q !== 'object' || !SQ[q.t]) return null; const t = q.t, mx = sqMax(t), mhp = SQ[t].hp, s = [];
  if (Array.isArray(q.s)) { for (const x of q.s) { if (s.length >= mx) break; const hp = _n(x && x.hp, 0, mhp, 0); if (hp > 0) s.push({ hp }); } }
  else { const k = _i(q.u, 0, mx, 0), h = _n(q.hp / Math.max(1, k), 0.1, mhp, mhp); for (let j = 0; j < k; j++) s.push({ hp: h }); }
  if (!s.length) return null;
  const o = { t, u: 0, hp: 0, s, w: _i(q.w, 0, mx, 0) }; if (t === 'cmd') o.cls = CMD[q.cls] ? q.cls : 'light'; syncSq(o); return o;
}
function _path(l, W, H, ter, water) { // путь принимаем, только если каждая клетка проходима
  if (!Array.isArray(l) || l.length > 4000) return []; const o = [];
  for (const p of l) { if (!p || !(p.x >= 0 && p.y >= 0 && p.x < W && p.y < H)) return []; const x = p.x | 0, y = p.y | 0, t = ter[y * W + x]; if (isWater(t) !== water || t === TR.MOUNT) return []; o.push({ x, y }); }
  return o;
}
function restoreGame(d) {
  if (!d || typeof d !== 'object' || (d.v !== 1 && d.v !== 2)) _fail('неизвестная версия');
  const W = d.W | 0, H = d.H | 0; if (W !== H || !(W === 64 || W === 96 || W === 128)) _fail('размер карты');
  let ter, res; try { ter = unb64(String(d.ter)); res = unb64(String(d.res)); } catch (e) { _fail('карта'); }
  if (ter.length !== W * H || res.length !== W * H) _fail('карта');
  for (let i = 0; i < W * H; i++) { if (ter[i] > 7) _fail('рельеф'); if (res[i] > 2) res[i] = 0; }
  if (!Array.isArray(d.players) || d.players.length < 1 || d.players.length > PNAMES.length) _fail('игроки');
  const n = d.players.length, time = _n(d.time, 0, 1e9, 0), pid = v => _i(v, -1, n - 1, -1);
  const S = { v: 2, opts: sanitizeOpts(d.opts), seed: _i(d.seed, 0, 2e9, 1), W, H, ter, res, comp: labelComps(W, H, ter), occ: new Int32Array(W * H).fill(-1), terr: new Int8Array(W * H).fill(-2),
    players: [], cities: [], buildings: [], bmap: new Map(), mm: new Uint8Array(W * H), structs: [], armies: [], ships: [], msgs: [], rel: [], time, nid: 1, over: null, dirtyT: true, fx: [], tk: 0, rnd: null, toast: [], plan: _i(d.plan, 0, 99, 0), sv: 0, pending: [], prompts: [], pend: [],
    folk: [], extra: new Map(), sanctions: [], fT: 0, eT: 0 };
  compSizes(S);
  const ids = new Set(); let maxId = 0;
  const takeId = v => { v = Math.floor(+v); if (!(v > 0 && v < 2e9) || ids.has(v)) _fail('идентификаторы'); ids.add(v); if (v > maxId) maxId = v; return v; };
  // державы: имя, цвет и особенность берём из своих таблиц, а не из файла
  d.players.forEach((p, i) => {
    if (!p || typeof p !== 'object') _fail('игрок'); const ci = _i(p.ci, 0, PNAMES.length - 1, i), diff = DIFF[p.diff] ? p.diff : 'normal', st = {}, fl = {};
    STOCK_KEYS.forEach(k => st[k] = _n(p.stock && p.stock[k], 0, 1e9, 0));
    if (p.flags && typeof p.flags === 'object') for (const k in p.flags) if (/^[A-Za-z_]{1,20}$/.test(k) && p.flags[k]) fl[k] = 1;
    S.players.push({ id: i, name: PNAMES[ci], col: PCOL[ci], ci, perk: PERKS[ci].id, human: i === 0, diff, mult: DIFF[diff].mult, stock: st, rate: { gold: 0, wood: 0, food: 0, iron: 0 },
      earned: _n(p.earned, 0, 1e12, 0), kills: _i(p.kills, 0, 1e9, 0), wins: _i(p.wins, 0, 1e9, 0), captured: _i(p.captured, 0, 1e6, 0), alive: true, ai: { next: _n(p.ai && p.ai.next, 0, 1e9, 0), fails: {}, failsAt: 0 }, flags: fl,
      eco: 0, mil: 0, score: 0, lastFood: _n(p.lastFood, -1e9, 1e9, -999), lastGold: _n(p.lastGold, -1e9, 1e9, -999), evAt: _n(p.evAt, 0, 1e9, time + 200) });
    S.rel[i] = [];
  });
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) { const r = { st: 'peace', op: 0, truceUntil: 0, trade: false, warSince: 0, ally: false, cl: {}, pass: {}, cd: {} }; S.rel[i][j] = r; S.rel[j][i] = r; }
  if (Array.isArray(d.rels)) for (const e of d.rels) {
    if (!Array.isArray(e)) continue; const i = e[0] | 0, j = e[1] | 0, q = e[2]; if (!(i >= 0 && j > i && j < n) || !q || typeof q !== 'object') continue; const r = S.rel[i][j];
    r.st = q.st === 'war' || q.st === 'truce' ? q.st : 'peace'; r.op = _n(q.op, -500, 500, 0); r.truceUntil = _n(q.truceUntil, 0, 2e9, 0); r.warSince = _n(q.warSince, 0, 2e9, 0);
    r.trade = !!q.trade && r.st === 'peace'; r.ally = !!q.ally && r.st === 'peace';
    for (const k of [i, j]) { if (q.cl && q.cl[k] && typeof q.cl[k] === 'object') r.cl[k] = { start: _n(q.cl[k].start, 0, 2e9, time) }; if (q.pass && q.pass[k] != null) r.pass[k] = _n(q.pass[k], 0, 2e9, 0); }
    if (q.cd && typeof q.cd === 'object') for (const k in q.cd) if (/^[a-z]{2,9}\d$/.test(k)) r.cd[k] = _n(q.cd[k], 0, 2e9, 0);
  }
  // города
  if (!Array.isArray(d.cities) || !d.cities.length || d.cities.length > 600) _fail('города');
  d.cities.forEach((c, i) => {
    if (!c || typeof c !== 'object') _fail('город'); const x = c.x | 0, y = c.y | 0; if (x !== c.x || y !== c.y || x < 1 || y < 1 || x > W - 3 || y > H - 3) _fail('место города');
    for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const k = (y + dy) * W + x + dx; if (S.occ[k] >= 0 || isWater(ter[k])) _fail('место города'); S.occ[k] = 1000000 + i; }
    const owner = pid(c.owner); let cap = null; if (c.cap && typeof c.cap === 'object' && pid(c.cap.by) >= 0) cap = { by: pid(c.cap.by), p: _n(c.cap.p, 0, 1, 0), hold: _n(c.cap.hold, 0, 60, 0) };
    S.cities.push({ id: i, name: _s(c.name, 28) || 'Город ' + (i + 1), x, y, owner, level: _i(c.level, 1, 5, 1), radius: _n(c.radius, 1, 30, 4), rr: 0, cap, isCap: !!c.isCap, home: pid(c.home), free: owner < 0 });
  });
  // здания
  if (!Array.isArray(d.buildings) || d.buildings.length > 20000) _fail('здания');
  const linkFix = [];
  for (const b of d.buildings) {
    if (!b || typeof b !== 'object' || !BD[b.type]) _fail('здание'); const x = b.x | 0, y = b.y | 0; if (x !== b.x || y !== b.y || !(x >= 0 && y >= 0 && x < W && y < H) || S.occ[y * W + x] >= 0) _fail('место здания');
    const type = b.type, dd = BD[type], id = takeId(b.id), rc = RECIPES[type];
    const o = { id, type, x, y, owner: pid(b.owner), recipe: rc ? (rc[b.recipe] ? b.recipe : Object.keys(rc)[0]) : null, busy: !!b.busy && !!rc, pt: _n(b.pt, 0, 1e4, 0), queue: [], status: '', res: res[y * W + x], t0: _n(b.t0, 0, 2e9, 0), hp: 0, mhp: 0, gar: null, wet: false, off: !!b.off, cd: _n(b.cd, 0, 2e9, 0), rt: _n(b.rt, 0, 10, 0) };
    if (dd.hp) { o.mhp = _n(b.mhp, 1, dd.hp * 2, dd.hp); o.hp = _n(b.hp, 1, o.mhp, o.mhp); }
    if (TRAIN_BLD[type] && Array.isArray(b.queue)) for (const q of b.queue.slice(0, 3)) { if (!q || !SQ[q.t] || SQ[q.t].bld !== type) continue; const e = { t: q.t, left: _n(q.left, 0, 1e4, SQ[q.t].t), total: _n(q.total, 1, 1e4, SQ[q.t].t) }; if (q.t === 'cmd') e.cls = CMD[q.cls] ? q.cls : 'light'; o.queue.push(e); }
    if (type === 'tower' && b.gar && o.owner >= 0) { const q = _squad(b.gar); if (q && q.t === 'archer') o.gar = q; }
    if (type === 'tower') { o.pass = !!b.pass; linkFix.push([o, Array.isArray(b.links) ? b.links.slice(0, 8) : []]); }
    o.byFolk = _i(b.byFolk, 0, 2e9, 0);
    S.buildings.push(o); S.bmap.set(id, o); S.occ[y * W + x] = id;
  }
  // соединения башен: проверяем после разбора всех зданий
  for (const [o, raws] of linkFix) {
    o.links = [];
    for (const l of raws) {
      if (!l || typeof l !== 'object') continue;
      const lid = Math.floor(+l.id); if (!(lid > 0) || lid === o.id) continue;
      const t2 = S.bmap.get(lid); if (!t2 || t2.type !== 'tower') continue;
      const tiles = []; if (Array.isArray(l.tiles)) for (const tid of l.tiles.slice(0, 4)) { const w = S.bmap.get(Math.floor(+tid)); if (w && (w.type === 'wall' || w.type === 'gate') && w.owner === o.owner) tiles.push(w.id); }
      o.links.push({ id: lid, tiles, open: !!l.open });
    }
  }
  // армии
  if (!Array.isArray(d.armies) || d.armies.length > 5000) _fail('армии');
  const rawShips = Array.isArray(d.ships) ? d.ships.filter(s => s && typeof s === 'object').slice(0, 200) : [], shipIds = new Set(rawShips.map(s => s.id));
  for (const a of d.armies) {
    if (!a || typeof a !== 'object') _fail('армия'); const owner = pid(a.owner); if (owner < 0) continue;
    const sq = []; if (Array.isArray(a.sq)) for (const q of a.sq.slice(0, MAX_SQ_ARMY)) { const z = _squad(q); if (z) sq.push(z); } if (!sq.length) continue;
    let x = _n(a.x, 0, W - 0.01, NaN), y = _n(a.y, 0, H - 0.01, NaN); if (!(x >= 0) || !(y >= 0)) _fail('место армии');
    const id = takeId(a.id), ship = a.ship != null && shipIds.has(a.ship) ? a.ship : null;
    if (ship == null) { const t = ter[Math.floor(y) * W + Math.floor(x)]; if (isWater(t) || t === TR.MOUNT) { const p = nearestTile(S, x, y, false, 12); if (!p) continue; x = p.x + 0.5; y = p.y + 0.5; } }
    const o = { id, owner, x, y, sq, path: ship == null ? _path(a.path, W, H, ter, false) : [], dest: null, st: 'idle', ship, maxU: 1, face: a.face === -1 ? -1 : 1, boardPort: null, siege: null, sg: false, garrison: null, retreat: false, tres: _n(a.tres, 0, 100, 0), askT: _n(a.askT, 0, 2e9, 0) };
    o.maxU = Math.max(units(o), _i(a.maxU, 1, 1000, 1)); if (o.path.length) { o.st = 'move'; o.retreat = !!a.retreat; }
    if (a.siege != null && S.bmap.has(a.siege)) o.siege = a.siege; if (a.boardPort != null && S.bmap.has(a.boardPort)) o.boardPort = a.boardPort; if (a.garrison != null && S.bmap.has(a.garrison)) o.garrison = a.garrison;
    S.armies.push(o);
  }
  // корабли
  const armyById = new Map(S.armies.map(a => [a.id, a]));
  for (const s of rawShips) {
    const owner = pid(s.owner); if (owner < 0) continue; let x = _n(s.x, 0, W - 0.01, NaN), y = _n(s.y, 0, H - 0.01, NaN); if (!(x >= 0) || !(y >= 0)) continue;
    if (!isWater(ter[Math.floor(y) * W + Math.floor(x)])) { const p = nearestTile(S, x, y, true, 12); if (!p) continue; x = p.x + 0.5; y = p.y + 0.5; }
    const id = takeId(s.id), o = { id, owner, kind: s.kind === 'war' ? 'war' : 'trade', x, y, path: _path(s.path, W, H, ter, true), cargo: null, st: 'dock', home: S.bmap.has(s.home) ? s.home : null, landing: null, to: S.bmap.has(s.to) ? s.to : null, wait: _n(s.wait, 0, 100, 0), face: s.face === -1 ? -1 : 1, leg: s.leg === 'out' || s.leg === 'back' ? s.leg : 'idle', len: _n(s.len, 0, 1e4, 0) };
    if (o.path.length) o.st = 'sail';
    if (s.landing && s.landing.x >= 0 && s.landing.y >= 0 && s.landing.x < W && s.landing.y < H) o.landing = { x: s.landing.x | 0, y: s.landing.y | 0 };
    const ca = armyById.get(s.cargo); if (ca && ca.ship === id && ca.owner === owner && o.kind === 'war') o.cargo = ca.id;
    S.ships.push(o);
  }
  { // армия «на корабле», которого нет, высаживается на ближайший берег
    const shipOf = new Map(S.ships.map(s => [s.id, s]));
    for (const a of S.armies) if (a.ship != null) { const sh = shipOf.get(a.ship); if (sh && sh.cargo === a.id) { a.x = sh.x; a.y = sh.y; } else { a.ship = null; const p = nearestTile(S, a.x, a.y, false, 24); if (p) { a.x = p.x + 0.5; a.y = p.y + 0.5; } } }
    S.armies = S.armies.filter(a => a.ship != null || !isWater(ter[Math.floor(a.y) * W + Math.floor(a.x)]));
  }
  // письма и отложенные предложения
  const KINDS = { trade: 1, ally: 1, pass: 1, truce: 1, ult: 1, event: 1 }, MTYPES = { info: 1, war: 1, trade: 1, truce: 1, econ: 1 };
  if (Array.isArray(d.msgs)) for (const m of d.msgs.slice(0, 80)) {
    if (!m || typeof m !== 'object' || !(m.id > 0) || ids.has(Math.floor(+m.id))) continue;
    const o = { id: takeId(m.id), t: _n(m.t, 0, 2e9, 0), read: !!m.read, type: MTYPES[m.type] ? m.type : 'info', title: _s(m.title, 80), text: _s(m.text, 400) };
    if (KINDS[m.kind]) {
      o.kind = m.kind; o.done = !!m.done; o.exp = _n(m.exp, 0, 2e9, 0); if (m.late) o.late = true;
      if (m.kind === 'event') { o.ev = _s(m.ev, 20); if (!EVENTS.some(e => e.id === o.ev && e.choice)) o.done = true; } else { o.from = pid(m.from); if (o.from < 1) o.done = true; }
    }
    S.msgs.push(o);
  }
  if (Array.isArray(d.pending)) for (const q of d.pending.slice(0, 60)) { if (!q || !KINDS[q.kind] || q.kind === 'event') continue; const f = pid(q.from), t = pid(q.to); if (f < 0 || t < 0 || f === t) continue; S.pending.push({ at: _n(q.at, 0, 2e9, time), from: f, to: t, kind: q.kind }); }
  // жители, освоенные земли, санкции
  if (Array.isArray(d.folk)) for (const f of d.folk.slice(0, 2000)) {
    if (!f || typeof f !== 'object' || !(f.id > 0) || ids.has(Math.floor(+f.id))) continue;
    const id = takeId(f.id), st = f.st === 'dead' ? 'dead' : f.st === 'prison' ? 'prison' : 'ok';
    S.folk.push({ id, name: _s(f.name, 40) || 'Житель', age: _n(f.age, 0, 200, 20), job: _s(f.job, 24), city: _i(f.city, 0, 600, 0), st, wealth: _n(f.wealth, 0, 100, 20), mood: _n(f.mood, 0, 100, 60) });
  }
  if (Array.isArray(d.extra)) for (const e of d.extra.slice(0, 4000)) { if (!Array.isArray(e)) continue; const i = Math.floor(+e[0]), o = Math.floor(+e[1]); if (i >= 0 && i < W * H && o === 0) S.extra.set(i, 0); }
  if (Array.isArray(d.sanctions)) for (const s of d.sanctions.slice(0, 200)) {
    if (!s || typeof s !== 'object' || !SANCTIONS[s.k]) continue;
    const a = pid(s.a), b = pid(s.b); if (a < 0 || b < 0 || a === b) continue;
    S.sanctions.push({ a, b, k: s.k, until: _n(s.until, 0, 2e9, 0) });
  }
  S.nid = Math.max(maxId + 1, _i(d.nid, 1, 2e9, 1)); S.rnd = mulberry32(S.seed + (S.nid | 0));
  refreshStructs(S); recomputeMoat(S); recomputeTerr(S);
  for (const p of S.players) p.alive = S.cities.some(c => c.owner === p.id);
  if (!S.players[0].alive) _fail('у игрока нет городов');
  calcRates(S); calcScore(S); calcVision(S);
  return S;
}
