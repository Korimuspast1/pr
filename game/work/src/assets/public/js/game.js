'use strict';
/* ===== Ядро игры Kingdom Crash ===== */
const dist = (ax, ay, bx, by) => Math.hypot(ax - bx, ay - by);
function canAfford(p, c) { for (const k in c) if ((p.stock[k] || 0) < c[k]) return false; return true; }
function pay(p, c) { for (const k in c) p.stock[k] -= c[k]; }
function costText(c) { return Object.keys(c).map(k => c[k] + ' ' + RNAME[k].toLowerCase()).join(', '); }
const SIZES = { s: 64, m: 96, l: 128 };

function newGame(o) {
  const seed = o.seed || (((Math.random() * 1e9) ^ (Date.now() & 0x7fffffff)) >>> 0) % 1000000000;
  const W = SIZES[o.size] || 96, H = W, rnd = mulberry32(seed + 99);
  const map = genMap(W, H, o.type, seed);
  const S = { opts: o, seed, W, H, ter: map.ter, res: map.res, comp: map.comp, occ: new Int32Array(W * H).fill(-1), terr: new Int8Array(W * H).fill(-2),
    players: [], cities: [], buildings: [], bmap: new Map(), mm: new Uint8Array(W * H), structs: [], armies: [], ships: [], msgs: [], rel: [], time: 0, nid: 1, over: null, dirtyT: true, fx: [], tk: 0, rnd: rnd, toast: [], plan: 0, speedNote: 0 };
  const n = 1 + o.bots;
  const names = PNAMES.slice(); const mine = o.nation || 0;
  const order = [mine]; for (let i = 0; i < names.length && order.length < n; i++) if (i !== mine) order.push(i);
  for (let i = 0; i < n; i++) {
    const st = {}; STOCK_KEYS.forEach(k => st[k] = 0); Object.assign(st, { gold: 450, wood: 320, food: 260, iron: 120 });
    const diff = i === 0 ? 'normal' : (o.diff === 'mixed' ? ['easy', 'normal', 'hard'][(rnd() * 3) | 0] : o.diff);
    S.players.push({ id: i, name: names[order[i]], col: PCOL[order[i]], ci: order[i], human: i === 0, diff, mult: DIFF[diff].mult, stock: st, rate: { gold: 0, wood: 0, food: 0, iron: 0 },
      earned: 0, kills: 0, wins: 0, captured: 0, alive: true, ai: { next: 3 + i, fails: {} }, flags: {}, eco: 0, mil: 0, score: 0, lastFood: -999, lastGold: -999 });
    S.rel[i] = [];
  }
  for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) { const r = { st: 'peace', op: ((rnd() * 40) | 0) - 20, truceUntil: 0, trade: false, warSince: 0, ally: false, cl: {} }; S.rel[i][j] = r; S.rel[j][i] = r; }
  // столицы
  const cand = [];
  const goodSpot = (x, y) => {
    for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const t = S.ter[(y + dy) * W + x + dx]; if (t !== TR.GRASS && t !== TR.PLAIN) return false; }
    let land = 0, tot = 0; for (let yy = -6; yy <= 6; yy++) for (let xx = -6; xx <= 6; xx++) { const X = x + xx, Y = y + yy; if (X < 0 || Y < 0 || X >= W || Y >= H) continue; tot++; if (!isWater(S.ter[Y * W + X])) land++; }
    return land / tot > 0.72;
  };
  for (let k = 0; k < 6000 && cand.length < 500; k++) { const x = 6 + ((rnd() * (W - 14)) | 0), y = 6 + ((rnd() * (H - 14)) | 0); if (goodSpot(x, y)) cand.push({ x, y }); }
  if (!cand.length) { return newGame(Object.assign({}, o, { seed: seed + 1, type: 'continent' })); }
  const chosen = [cand[(rnd() * cand.length) | 0]];
  while (chosen.length < n) {
    let best = null, bd = -1;
    for (const c of cand) { let md = 1e9; for (const ch of chosen) md = Math.min(md, dist(c.x, c.y, ch.x, ch.y)); if (md > bd) { bd = md; best = c; } }
    chosen.push(best);
  }
  // игрок - ближайший к центру из выбранных? оставляем случайным
  const cn = CITY_NAMES.slice().sort(() => rnd() - 0.5);
  chosen.forEach((c, i) => { ensureNearby(S, c.x + 1, c.y + 1, rnd); addCity(S, c.x, c.y, i, cn.pop(), true); S.cities[S.cities.length - 1].radius = 6; });
  // нейтральных городов нет: все города строят игроки
  S.cities.forEach(c => { if (c.owner < 0) c.radius = 4; });
  recomputeTerr(S); calcVision(S);
  return S;
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
function garrison(c) { return 30 * c.level + (c.isCap ? 40 : 0); }
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
  for (const b of S.buildings) { const o = t[b.y * W + b.x]; if (o !== -2 && o !== b.owner) { b.owner = o; b.busy = false; b.queue = []; b.gar = null; } }
  S.dirtyT = true;
}
function bidx(S, id) { return S.bmap.get(id); }

/* ===== Строительство ===== */
function siteOk(S, pid, type, x, y) {
  if (x < 0 || y < 0 || x >= S.W || y >= S.H) return 'Вне карты';
  if (type === 'city') return cityOk(S, pid, x, y);
  if (BD[type].one && S.buildings.some(b => b.type === type && b.owner === pid)) return 'Здание может быть только одно';
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
    case 'coast': if (!(t === TR.SAND || t === TR.GRASS || t === TR.PLAIN) || !nearWater(S, x, y)) return 'Только на берегу'; break;
    default: if (t === TR.FOREST || t === TR.MOUNT || isWater(t) || r) return 'Здесь нельзя строить';
  }
  return null;
}
function cityOk(S, pid, x, y) {
  if (!citySpot(S, x, y)) return 'Нужна свободная ровная земля 2×2';
  const cx = x + 1, cy = y + 1; let own = 0;
  for (const c of S.cities) { const d = dist(cx, cy, c.x + 1, c.y + 1); if (d < CITY_MIN) return 'Слишком близко к другому городу'; if (c.owner === pid) own++; }
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
  const p = S.players[pid], err = siteOk(S, pid, type, x, y); if (err) return err;
  if (type === 'city') { const cost = cityCost(S, pid); if (!canAfford(p, cost)) return 'Не хватает ресурсов'; pay(p, cost); clearForest(S, x, y); const c = addCity(S, x, y, pid, nextCityName(S), false); c.radius = 4; recomputeTerr(S); return null; }
  const d = BD[type]; if (!canAfford(p, d.cost)) return 'Не хватает ресурсов'; pay(p, d.cost);
  { const oi = S.occ[y * S.W + x]; if (type === 'gate' && oi >= 0 && oi < 1000000) { const ob = S.bmap.get(oi); if (ob) demolish(S, ob); } }
  const b = { id: S.nid++, type, x, y, owner: pid, recipe: type === 'forge' ? 'sword' : type === 'workshop' ? 'bow' : type === 'stable' ? 'horse' : null, busy: false, pt: 0, queue: [], status: '', res: S.res[y * S.W + x], t0: S.time, hp: d.hp || 0, mhp: d.hp || 0, gar: null, wet: false };
  S.buildings.push(b); S.bmap.set(b.id, b); S.occ[y * S.W + x] = b.id; p.flags['b_' + type] = 1; if (STRUCT[type]) refreshStructs(S); if (type === 'moat') recomputeMoat(S); return null;
}
function nextCityName(S) { const used = new Set(S.cities.map(c => c.name)); const free = CITY_NAMES.filter(n => !used.has(n)); return free.length ? free[(S.rnd() * free.length) | 0] : 'Город ' + (S.cities.length + 1); }
function demolish(S, b) { if (b.gar && b.gar.s.length) ungarrison(S, b); S.occ[b.y * S.W + b.x] = -1; S.buildings = S.buildings.filter(q => q !== b); S.bmap.delete(b.id); if (STRUCT[b.type]) refreshStructs(S); if (b.type === 'moat') recomputeMoat(S); }
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
  const b = structAtTile(S, x, y); if (!b) return false; if (b.type === 'wall' || b.type === 'tower') return true;
  if (b.type === 'gate') { if (b.owner < 0 || o < 0) return true; return !(b.owner === o || allied(S, o, b.owner) || passLeft(S, o, b.owner) > 0); } return false;
}
function structBlockedAt(S, o, x, y) { return structBlockedFor(S, o, Math.floor(x), Math.floor(y)); }
function destroyStruct(S, b, by) {
  const nm = BD[b.type].n; S.fx.push({ k: 'rubble', x: b.x + 0.5, y: b.y + 0.5, t: S.time }); if (b.gar && b.gar.s.length) ungarrison(S, b);
  const ow = b.owner; demolish(S, b); S.armies.forEach(a => { if (a.siege === b.id) { a.siege = null; a.sg = false; } });
  if (ow === 0) info(S, 0, nm + ' разрушена', 'Враг сломал укрепление.', 'war'); if (by === 0) info(S, 0, nm + ' разрушена', 'Укрепление врага уничтожено.', 'info');
}
function garrisonSquad(S, a, T) {
  if (T.gar && T.gar.s.length) return 'Башня занята'; const i = a.sq.findIndex(q => q.t === 'archer'); if (i < 0) return 'В армии нет лучников';
  const q = a.sq.splice(i, 1)[0]; T.gar = q; syncSq(q); a.maxU = Math.max(1, units(a)); if (!a.sq.length) S.armies = S.armies.filter(z => z !== a);
  if (a.owner === 0) info(S, 0, 'Башня занята', 'Лучники заняли башню и получили защиту.', 'info'); return null;
}
function ungarrison(S, T) {
  if (!T.gar) return; const q = T.gar; T.gar = null; if (!q.s.length) return; const pt = nearestTile(S, T.x + 0.5, T.y + 1.5, false, 4) || { x: T.x, y: T.y };
  const a = newArmy(S, T.owner, pt.x + 0.5, pt.y + 0.5, [q]); return a;
}
function upgradeCost(c) { return { gold: 120 * c.level, wood: 80 * c.level, food: 30 * c.level }; }
function upgradeCity(S, c) { const p = S.players[c.owner]; if (!p || c.level >= 5) return 'Максимальный уровень'; const k = upgradeCost(c); if (!canAfford(p, k)) return 'Не хватает ресурсов'; pay(p, k); c.level++; p.flags.upg = 1; return null; }

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
  const r = rel2(S, a, b); if (r.st === 'war') return 'Вы уже воюете'; if (r.ally) return 'Это ваш союзник'; if (claimInfo(S, a, b)) return 'Претензия уже предъявлена';
  r.cl[a] = { start: S.time }; r.op -= 15;
  addMsg(S, b, { type: 'war', title: 'Претензии на ваши земли', text: pname(S, a) + ' готовит повод для войны. Через 3 дня он сможет объявить вам войну.' });
  if (a === 0) addMsg(S, 0, { type: 'war', title: 'Претензия начата', text: 'Через 3 дня можно будет объявить войну: ' + pname(S, b) }); S.players[a].flags.claim = 1; return null;
}
function startWar(S, a, b) {
  const r = rel2(S, a, b); r.st = 'war'; r.warSince = S.time; r.trade = false; r.ally = false; r.op = Math.min(r.op, -50); delete r.cl[a];
  addMsg(S, b, { type: 'war', title: 'Объявлена война!', text: pname(S, a) + ' объявил вам войну.' });
  if (a === 0) addMsg(S, 0, { type: 'war', title: 'Война', text: 'Вы объявили войну: ' + pname(S, b) }); S.players[a].flags.war = 1;
}
function declareWar(S, a, b) {
  const r = rel2(S, a, b); if (r.st === 'war') return 'Уже идёт война'; if (r.st === 'truce' && r.truceUntil > S.time) return 'Действует перемирие'; if (r.ally) return 'Сначала разорвите союз';
  const c = claimInfo(S, a, b); if (!c) return 'Сначала предъявите претензию (3 дня)'; if (!c.ready) return 'Претензия будет готова через ' + Math.ceil(c.left / DAY) + ' дн.';
  startWar(S, a, b); return null;
}
function breakAlly(S, a, b) { const r = rel2(S, a, b); r.ally = false; r.op -= 20; addMsg(S, b, { type: 'info', title: 'Союз разорван', text: pname(S, a) + ' разорвал союз.' }); }
function canEnter(S, o, t) { if (atWar(S, o, t) || allied(S, o, t)) return true; const r = rel(S, o, t); return !!(r.pass && r.pass[o] > S.time); }
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
function offer(S, from, to, kind) {
  const r = rel(S, from, to);
  if (kind === 'trade' && r.st !== 'peace') return 'Торговля возможна только в мире';
  if (kind === 'trade' && r.trade) return 'Соглашение уже есть';
  if (kind === 'truce' && r.st !== 'war') return 'Вы не воюете';
  if (kind === 'ult' && r.st === 'war') return 'Вы уже воюете';
  if (kind === 'ally' && (r.st !== 'peace' || r.ally)) return r.ally ? 'Союз уже заключён' : 'Союз возможен только в мире';
  if (kind === 'pass') { if (r.st === 'war') return 'Во время войны проход закрыт'; if (r.ally) return 'Союзникам проход открыт'; if (passLeft(S, from, to) > 0) return 'Право прохода уже есть'; }
  if (to === 0) { addMsg(S, 0, { type: kind === 'trade' || kind === 'ally' || kind === 'pass' ? 'trade' : kind === 'truce' ? 'truce' : 'war', kind, from, title: kind === 'trade' ? 'Торговое предложение' : kind === 'pass' ? 'Просьба о проходе войск' : kind === 'ally' ? 'Предложение союза' : kind === 'truce' ? 'Просьба о перемирии' : 'Ультиматум!', text: kind === 'ult' ? pname(S, from) + ' требует 150 золота, иначе — война.' : pname(S, from) + (kind === 'trade' ? ' предлагает торговое соглашение.' : kind === 'ally' ? ' предлагает союз: общая видимость и защита.' : kind === 'pass' ? ' просит разрешить проход своих войск по вашей территории на 3 дня.' : ' просит о перемирии.') }); return null; }
  if (from === 0) { S.pending = S.pending || []; S.pending.push({ at: S.time + 3 + S.rnd() * 5, from, to, kind }); return null; }
  decideOffer(S, from, to, kind); return null;
}
function decideOffer(S, from, to, kind) {
  const r = rel(S, from, to); let ok = false;
  if (kind === 'trade') ok = r.st === 'peace' && !r.trade && r.op > -35;
  else if (kind === 'pass') ok = r.st !== 'war' && (r.op >= 0 || r.trade);
  else if (kind === 'ally') { const common = S.players.some(z => z.id !== from && z.id !== to && z.alive && rel(S, from, z.id).st === 'war' && rel(S, to, z.id).st === 'war'); ok = r.st === 'peace' && !r.ally && r.op >= 10 && (r.trade || common); }
  else if (kind === 'truce') ok = r.st === 'war' && (S.time - r.warSince > 150 || milPower(S, to) < milPower(S, from) * 0.9);
  else if (kind === 'ult') { ok = milPower(S, to) * 1.4 < milPower(S, from) && S.players[to].stock.gold >= 150; if (ok) { S.players[to].stock.gold -= 150; S.players[from].stock.gold += 150; r.op -= 15; } else { r.op -= 25; if (r.st !== 'war') startWar(S, to, from); } if (from === 0) info(S, 0, ok ? 'Ультиматум принят' : 'Ультиматум отвергнут', pname(S, to) + (ok ? ' уплатил 150 золота.' : ' ответил войной!'), 'war'); return; }
  applyDeal(S, from, to, kind, ok);
}
function applyDeal(S, from, to, kind, ok) {
  const r = rel(S, from, to);
  if (ok) { if (kind === 'trade') { r.trade = true; r.op += 10; S.players[from].flags.trade = 1; S.players[to].flags.trade = 1; } else if (kind === 'truce') { r.st = 'truce'; r.truceUntil = S.time + 240; r.op += 20; } else if (kind === 'ally') { r.ally = true; r.op += 15; } else if (kind === 'pass') { r.pass = r.pass || {}; r.pass[from] = S.time + 3 * DAY; r.op += 3; } }
  else r.op -= 5;
  if (from === 0) info(S, 0, (ok ? 'Принято: ' : 'Отказ: ') + (kind === 'trade' ? 'торговля' : kind === 'ally' ? 'союз' : kind === 'pass' ? 'право прохода' : 'перемирие'), pname(S, to) + (ok ? ' согласился.' : ' отклонил предложение.'), kind === 'trade' || kind === 'ally' || kind === 'pass' ? 'trade' : 'truce');
  if (ok && to === 0) info(S, 0, 'Соглашение заключено', pname(S, from), kind === 'trade' || kind === 'ally' || kind === 'pass' ? 'trade' : 'truce');
}
function answerMsg(S, id, yes) {
  const m = S.msgs.find(q => q.id === id); if (!m || m.done) return; m.done = true; m.read = true;
  const from = m.from; if (m.kind === 'ult') { if (yes && S.players[0].stock.gold >= 150) { S.players[0].stock.gold -= 150; S.players[from].stock.gold += 150; rel(S, 0, from).op -= 10; } else if (rel(S, 0, from).st !== 'war') startWar(S, from, 0); return; }
  applyDeal(S, from, 0, m.kind, yes); if (m.kind === 'trade' && yes) S.players[0].flags.trade = 1;
}
function cancelTrade(S, a, b) { const r = rel(S, a, b); r.trade = false; r.op -= 8; }

/* ===== Войска ===== */
const BAT = 0.3; // затяжные бои: меньше урона за секунду
const FORD = { cmd: -2, ram: -1, heavy: 0, light: 1, cav: 2, archer: 3, cat: 4 };
function mkSquad(t) { const n = SQ[t].cnt || 10, s = []; for (let i = 0; i < n; i++) s.push({ hp: SQ[t].hp }); return { t, u: n, hp: n * SQ[t].hp, s, w: 0 }; }
const sqMax = t => SQ[t].cnt || 10;
function ensureS(q) { if (!q.s) { q.s = []; const n = q.u | 0, h = q.hp / Math.max(1, n); for (let i = 0; i < n; i++) q.s.push({ hp: h }); q.w = q.w || 0; } for (const x of q.s) { x.x = undefined; x.y = undefined; } }
function syncSq(q) { q.u = q.s.length; let h = 0; for (const x of q.s) h += x.hp; q.hp = h; }
function armySoldiers(a) { const key = a.sq.map(q => q.t + (q.cls || '') + q.s.length).join(); if (a._sk === key) return a._sl; const l = []; for (const q of a.sq) for (const s of q.s) l.push({ s, t: q.t, cls: q.cls }); l.sort((p, q) => FORD[p.t] - FORD[q.t]); a._sk = key; return a._sl = l; }
const FS = {};
function formSlots(n) { if (FS[n]) return FS[n]; const rows = n <= 5 ? 1 : n <= 14 ? 2 : n <= 24 ? 3 : n <= 40 ? 4 : n <= 60 ? 5 : 6, cols = Math.max(1, Math.ceil(n / rows)), sx = n > 40 ? 3.0 : 3.6, sy = 2.4, f = []; for (let i = 0; i < n; i++) { const r = Math.floor(i / cols), cc = i % cols; f.push({ ox: (cc - (cols - 1) / 2) * sx + (r & 1 ? 0.9 : 0), oy: (r - (rows - 1) / 2) * sy + 1 }); } return FS[n] = { f, w: cols * sx + 1, h: rows * sy + 3 }; }
function reapArmy(S, a, killer, wound) {
  for (const q of a.sq) { for (let i = q.s.length - 1; i >= 0; i--) if (q.s[i].hp <= 0) { const s = q.s[i]; q.s.splice(i, 1); if (killer >= 0) S.players[killer].kills++; if (wound && sqMax(q.t) > 1 && q.w + q.s.length < sqMax(q.t) && S.rnd() < 0.65) q.w++; if (s.x !== undefined) S.fx.push({ k: 'rip', x: s.x, y: s.y, t: S.time }); } syncSq(q); }
  cleanArmy(S, a, killer);
}
const units = a => a.sq.reduce((s, q) => s + q.u, 0);
function newArmy(S, owner, x, y, sq) { const a = { id: S.nid++, owner, x, y, sq, path: [], dest: null, st: 'idle', ship: null, maxU: sq.reduce((s, q) => s + q.u, 0), face: 1, boardPort: null }; S.armies.push(a); return a; }
function armySpd(S, a) { let s = 9; for (const q of a.sq) s = Math.min(s, q.t === 'cmd' && q.cls === 'cav' ? 2.2 : q.t === 'cmd' ? SQ[q.cls].spd : SQ[q.t].spd); s *= MOVE_MULT; const i = Math.floor(a.y) * S.W + Math.floor(a.x); return s * TMULT[S.ter[i]] * (S.mm[i] === 1 ? 0.5 : 1); }
function combo(a) { const t = new Set(a.sq.map(q => q.t).filter(x => x !== 'cmd')); return 1 + 0.08 * (t.size - 1); }
function mergeNear(S, a) {
  let n = 0;
  for (const b of S.armies.slice()) {
    if (b === a || b.owner !== a.owner || b.ship || dist(a.x, a.y, b.x, b.y) > 1.8 || a.sq.length + b.sq.length > MAX_SQ_ARMY) continue;
    a.sq.push(...b.sq); a.maxU += b.maxU; S.armies = S.armies.filter(q => q !== b); n++;
  }
  if (n) S.players[a.owner].flags.army = 1; return n;
}
function splitArmy(S, a) {
  if (a.sq.length < 2) return 0; const rest = a.sq.splice(1);
  rest.forEach((q, i) => { const n = newArmy(S, a.owner, a.x + 0.4 * (i + 1), a.y + 0.3, [q]); const pt = nearestTile(S, n.x, n.y, false, 2); if (pt) { n.x = pt.x + 0.5; n.y = pt.y + 0.5; } });
  a.maxU = units(a); return rest.length;
}
function orderMove(S, a, tx, ty) {
  if (a.ship) return 'Войска на корабле — управляйте кораблём';
  const gx0 = Math.floor(tx), gy0 = Math.floor(ty), gb = structAtTile(S, gx0, gy0);
  if (gb) {
    if (gb.owner !== a.owner && !allied(S, a.owner, gb.owner) && atWar(S, a.owner, gb.owner)) return orderSiege(S, a, gb);
    if (gb.type === 'tower' && (gb.owner === a.owner || allied(S, a.owner, gb.owner))) return orderGarrison(S, a, gb);
  }
  a.siege = null; a.sg = false; a.garrison = null; return moveTo(S, a, tx, ty);
}
function moveTo(S, a, tx, ty) {
  let gx = Math.floor(tx), gy = Math.floor(ty);
  if (isWater(S.ter[gy * S.W + gx]) || S.ter[gy * S.W + gx] === TR.MOUNT) { const p = nearestTile(S, gx, gy, false, 3); if (!p) return 'Туда нельзя: горы или вода'; gx = p.x; gy = p.y; }
  const sx = Math.floor(a.x), sy = Math.floor(a.y), o = a.owner;
  if (S.comp[sy * S.W + sx] !== S.comp[gy * S.W + gx]) return 'Нет пути по суше (вода или горы) — нужен корабль';
  if (blockedFor(S, o, gx, gy)) return 'Чужая территория: объявите войну или запросите право прохода';
  const startBlocked = blockedFor(S, o, sx, sy);
  const path = astar(S, sx, sy, gx, gy, false, (x, y) => (!startBlocked && blockedFor(S, o, x, y)) || structBlockedFor(S, o, x, y));
  if (!path) return 'Путь закрыт: стены, чужие границы, горы или вода';
  a.path = path; a.dest = { x: gx, y: gy }; a.st = 'move'; a.boardPort = null;
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
function stopArmy(a) { a.path = []; a.dest = null; a.st = 'idle'; a.boardPort = null; a.siege = null; a.sg = false; a.garrison = null; }
function nearestOwnCity(S, a) { let best = null, bd = 1e9; for (const c of S.cities) if (c.owner === a.owner) { const d = dist(a.x, a.y, c.x + 1, c.y + 1); if (d < bd && S.comp[(c.y) * S.W + c.x] === S.comp[Math.floor(a.y) * S.W + Math.floor(a.x)]) { bd = d; best = c; } } return best; }
function nearestHospital(S, a) { let best = null, bd = 1e9; const cp = S.comp[Math.floor(a.y) * S.W + Math.floor(a.x)]; for (const b of S.buildings) if (b.type === 'hospital' && b.owner === a.owner) { const d = dist(a.x, a.y, b.x + 0.5, b.y + 0.5); if (d < bd && S.comp[b.y * S.W + b.x] === cp) { bd = d; best = b; } } return best; }
function retreat(S, a) {
  const h = nearestHospital(S, a); let e;
  if (h) e = orderMove(S, a, h.x + 0.5, h.y + 1.6); else { const c = nearestOwnCity(S, a); if (!c) return 'Некуда отступать'; e = orderMove(S, a, c.x + 1, c.y + 1); }
  if (!e) { a.retreat = true; a.fight = false; } return e;
}
function tickHospital(S, dt) {
  for (const b of S.buildings) {
    if (b.type !== 'hospital' || b.owner < 0) continue; const p = S.players[b.owner]; b.treated = 0; b.rt = (b.rt || 0) + dt; let rv = false; if (b.rt >= 5) { b.rt = 0; rv = true; }
    for (const a of S.armies) {
      if (a.owner !== b.owner || a.ship || a.bat || a.path.length || dist(a.x, a.y, b.x + 0.5, b.y + 0.5) > 3.5) continue;
      for (const q of a.sq) { let hl = false; for (const s of q.s) if (s.hp < SQ[q.t].hp) { s.hp = Math.min(SQ[q.t].hp, s.hp + 2.2 * dt); hl = true; } if (hl) b.treated++;
        if (rv && q.w > 0 && q.s.length < sqMax(q.t) && canAfford(p, { gold: 8, food: 4 })) { pay(p, { gold: 8, food: 4 }); q.w--; q.s.push({ hp: SQ[q.t].hp * 0.6 }); b.treated++; } syncSq(q); }
    }
  }
}

/* ===== Корабли ===== */
function shipCost(kind) { return kind === 'war' ? { wood: 120, gold: 100, iron: 30 } : { wood: 80, gold: 80 }; }
function buildShip(S, port, kind) {
  const p = S.players[port.owner], c = shipCost(kind); if (!canAfford(p, c)) return 'Не хватает ресурсов';
  if (S.ships.filter(s => s.owner === port.owner && s.kind === kind).length >= (kind === 'war' ? 4 : 5)) return 'Достигнут предел судов';
  const w = nearestTile(S, port.x, port.y, true, 3); if (!w) return 'Нет выхода в море';
  pay(p, c); const sh = { id: S.nid++, owner: port.owner, kind, x: w.x + 0.5, y: w.y + 0.5, path: [], cargo: null, st: 'dock', home: port.id, landing: null, to: null, wait: 0, face: 1 }; S.ships.push(sh); p.flags.ship = 1; return null;
}
function orderShip(S, sh, tx, ty) {
  let gx = Math.floor(tx), gy = Math.floor(ty), landing = null;
  if (!isWater(S.ter[gy * S.W + gx])) { landing = { x: gx, y: gy }; const w = nearestTile(S, gx, gy, true, 3); if (!w) return 'Нет подхода с моря'; gx = w.x; gy = w.y; }
  const sx = Math.floor(sh.x), sy = Math.floor(sh.y);
  if (S.comp[sy * S.W + sx] !== S.comp[gy * S.W + gx]) return 'Нет морского пути';
  const path = astar(S, sx, sy, gx, gy, true); if (!path) return 'Путь не найден';
  sh.path = path; sh.st = 'sail'; sh.landing = landing; return null;
}
function unloadShip(S, sh) {
  if (!sh.cargo) return 'Корабль пуст'; const a = S.armies.find(q => q.id === sh.cargo); if (!a) { sh.cargo = null; return null; }
  const tgt = sh.landing || { x: sh.x, y: sh.y }; const p = nearestTile(S, tgt.x, tgt.y, false, 3); if (!p) return 'Рядом нет берега';
  if (dist(sh.x, sh.y, p.x + 0.5, p.y + 0.5) > 4) return 'Подойдите ближе к берегу';
  if (blockedFor(S, a.owner, p.x, p.y)) return 'Нельзя высаживаться на чужой территории без войны или права прохода';
  a.x = p.x + 0.5; a.y = p.y + 0.5; a.ship = null; a.st = 'idle'; sh.cargo = null; sh.landing = null; return null;
}
function tickShips(S, dt) {
  for (const sh of S.ships) {
    const p = S.players[sh.owner];
    if (sh.path.length) {
      const w = sh.path[0], dx = w.x + 0.5 - sh.x, dy = w.y + 0.5 - sh.y, d = Math.hypot(dx, dy), step = (sh.kind === 'war' ? 2.0 : 1.7) * dt;
      if (d <= step) { sh.x = w.x + 0.5; sh.y = w.y + 0.5; sh.path.shift(); } else { sh.x += dx / d * step; sh.y += dy / d * step; } if (dx) sh.face = dx > 0 ? 1 : -1;
      if (!sh.path.length) {
        sh.st = 'dock';
        if (sh.kind === 'trade' && sh.to != null) { // доставка груза
          const dst = bidx(S, sh.to);
          if (dst && dst.owner >= 0 && sh.leg === 'out') { const g = 30 + Math.round(sh.len * 1.2); p.stock.gold += g; p.earned += g; p.flags.shipTrade = 1; if (dst.owner !== sh.owner) { S.players[dst.owner].stock.gold += g / 2; } sh.leg = 'back'; sh.to = sh.home; sh.wait = 2; const h = bidx(S, sh.home); if (h) { const w2 = nearestTile(S, h.x, h.y, true, 3); const pp = w2 && astar(S, Math.floor(sh.x), Math.floor(sh.y), w2.x, w2.y, true); if (pp) { sh.path = pp; sh.st = 'sail'; } } }
          else sh.leg = 'idle';
        } else if (sh.cargo && sh.landing) { unloadShip(S, sh); }
      }
    } else if (sh.kind === 'trade') {
      sh.wait -= dt; if (sh.wait > 0) continue; sh.wait = 4;
      const h = bidx(S, sh.home); if (!h || h.owner !== sh.owner) continue;
      let best = null, bd = 1e9;
      for (const b of S.buildings) if (b.type === 'port' && b.owner >= 0 && b.owner !== sh.owner && rel(S, sh.owner, b.owner).trade) { const d = dist(h.x, h.y, b.x, b.y); if (d < bd) { const w = nearestTile(S, b.x, b.y, true, 3); if (w && S.comp[w.y * S.W + w.x] === S.comp[Math.floor(sh.y) * S.W + Math.floor(sh.x)]) { bd = d; best = { b, w }; } } }
      if (best) { const pp = astar(S, Math.floor(sh.x), Math.floor(sh.y), best.w.x, best.w.y, true); if (pp) { sh.path = pp; sh.st = 'sail'; sh.to = best.b.id; sh.leg = 'out'; sh.len = pp.length; } }
    }
    if (sh.cargo) { const a = S.armies.find(q => q.id === sh.cargo); if (a) { a.x = sh.x; a.y = sh.y; } else sh.cargo = null; }
  }
}

/* ===== Экономика ===== */
function calcRates(S) {
  for (const p of S.players) p.rate = { gold: 0, wood: 0, food: 0, iron: 0 };
  for (const c of S.cities) if (c.owner >= 0) { const r = S.players[c.owner].rate; r.gold += 0.6 + 0.45 * c.level; r.food -= 0.1 * c.level; }
  for (const b of S.buildings) {
    if (b.owner < 0) continue; const p = S.players[b.owner], r = p.rate, m = p.mult;
    if (b.type === 'farm') r.food += 0.7 * m;
    else if (b.type === 'sawmill') { let f = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const X = b.x + dx, Y = b.y + dy; if (X >= 0 && Y >= 0 && X < S.W && Y < S.H && S.ter[Y * S.W + X] === TR.FOREST) f++; } r.wood += (0.45 + 0.08 * f) * m; }
    else if (b.type === 'mine') { if (b.res === RES.GOLD) r.gold += 0.45 * m; else r.iron += 0.45 * m; }
    else if (b.type === 'market') r.gold += 0.7 * m; else if (b.type === 'port') r.gold += 0.3 * m;
  }
  for (const a of S.armies) { const r = S.players[a.owner].rate; r.food -= 0.12 * a.sq.length; r.gold -= 0.06 * a.sq.length; }
  for (let i = 0; i < S.players.length; i++) for (let j = i + 1; j < S.players.length; j++) if (S.rel[i][j].trade) { const ports = n => S.buildings.filter(b => b.owner === n && b.type === 'port').length; for (const k of [i, j]) S.players[k].rate.gold += 0.5 + 0.25 * Math.min(ports(i), ports(j)); }
}
function tickEcon(S, dt) {
  for (const p of S.players) {
    if (!p.alive) continue; const s = p.stock;
    for (const k in p.rate) { const v = p.rate[k] * dt; s[k] += v; if (v > 0) p.earned += v * RVAL[k]; if (s[k] < 0) s[k] = 0; }
    if (s.food <= 0.01) { for (const a of S.armies.slice()) if (a.owner === p.id) { for (const q of a.sq) for (const x of q.s) x.hp -= 0.15 * dt; reapArmy(S, a, -1, false); } if (S.time - p.lastFood > 70) { p.lastFood = S.time; addMsg(S, p.id, { type: 'econ', title: 'Голод!', text: 'Кончилась еда — войска слабеют. Стройте фермы.' }); } }
    if (s.gold <= 0.5 && S.time - p.lastGold > 90) { p.lastGold = S.time; addMsg(S, p.id, { type: 'econ', title: 'Казна пуста', text: 'Золота нет, расходы на армию не покрыты.' }); }
    for (const k of ['bow', 'sword', 'armL', 'armH', 'horse']) if (s[k] > 300) s[k] = 300;
  }
  if (S.opts.sandbox) { const s0 = S.players[0].stock; for (const k of STOCK_KEYS) s0[k] = 99999; }
}
function tickProd(S, dt) {
  for (const b of S.buildings) {
    if (b.owner < 0) continue; const p = S.players[b.owner];
    const rc = RECIPES[b.type]; if (rc) {
      const r = rc[b.recipe]; if (!r) continue;
      if (!b.busy) { if (canAfford(p, r.in)) { pay(p, r.in); b.busy = true; b.pt = 0; b.status = ''; } else b.status = 'Не хватает ресурсов'; }
      else { b.pt += dt * p.mult; if (b.pt >= r.t) { p.stock[r.out]++; p.earned += RVAL[r.out]; b.busy = false; } }
    } else if ((b.type === 'barracks' || b.type === 'siege' || b.type === 'academy') && b.queue.length) {
      const q = b.queue[0]; q.left -= dt * p.mult;
      if (q.left <= 0) { b.queue.shift(); const pt = rallyPos(S, b), sq0 = mkSquad(q.t); if (q.cls) sq0.cls = q.cls; const na = newArmy(S, b.owner, pt.x, pt.y, [sq0]); p.flags.squad = 1; if (b.type === 'siege' || b.type === 'academy') { na.born = S.time; na.from = { x: b.x + 0.5, y: b.y + 0.95 }; na.bdur = 1.8; } if (b.owner === 0) info(S, 0, 'Отряд готов', SQ[q.t].n + ' вышли из казарм.', 'info'); }
    }
  }
}
function rallyPos(S, b) {
  let x = b.x + 0.5, y = b.y + 1.9; const t = S.ter[Math.floor(y) * S.W + Math.floor(x)];
  if (isWater(t) || t === TR.MOUNT) { const p = nearestTile(S, b.x + 0.5, b.y + 1.5, false, 3) || { x: b.x, y: b.y }; x = p.x + 0.5; y = p.y + 0.5; } return { x, y };
}
function hasCommander(S, pid) { for (const a of S.armies) if (a.owner === pid) for (const q of a.sq) if (q.t === 'cmd') return a; for (const b of S.buildings) if (b.owner === pid && b.type === 'academy' && b.queue.some(q => q.t === 'cmd')) return true; return null; }
function enqueueCmd(S, b, cls) {
  const p = S.players[b.owner], d = SQ.cmd; if (b.type !== 'academy' || !CMD[cls]) return 'Нельзя'; if (hasCommander(S, b.owner)) return 'Полководец у вас уже есть'; const cost = Object.assign({ gold: d.gold, food: d.food }, d.need);
  if (!canAfford(p, cost)) return 'Не хватает: ' + costText(cost); pay(p, cost); b.queue.push({ t: 'cmd', cls, left: d.t, total: d.t }); return null;
}
function enqueue(S, b, t) {
  const p = S.players[b.owner], d = SQ[t]; if (d.bld !== b.type) return 'Этот отряд здесь не обучают'; if (b.queue.length >= 3) return 'Очередь заполнена';
  const cost = Object.assign({ gold: d.gold, food: d.food }, d.need); if (!canAfford(p, cost)) return 'Не хватает: ' + costText(cost);
  pay(p, cost); b.queue.push({ t, left: d.t, total: d.t }); return null;
}

/* ===== Бой ===== */
function defMult(S, a) { const t = S.ter[Math.floor(a.y) * S.W + Math.floor(a.x)]; return t === TR.FOREST ? 0.85 : t === TR.HILL ? 0.8 : t === TR.MOUNT ? 0.7 : 1; }
function cleanArmy(S, a, killer) {
  a.sq = a.sq.filter(q => q.hp > 0 && q.u > 0);
  if (!a.sq.length) { S.armies = S.armies.filter(q => q !== a); if (killer >= 0) { const kp = S.players[killer]; kp.wins++; if (killer === 0) info(S, 0, 'Победа в сражении', 'Армия ' + pname(S, a.owner) + ' разбита.', 'info'); if (a.owner === 0) info(S, 0, 'Армия уничтожена', 'Враг разбил ваш отряд.', 'war'); } return true; }
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
    const out = (1 - (s.mD || 0)) * combo(a) * (S.players[o].human ? 1 : S.players[o].mult) * (CL.length && auraAt(CL, o, u.tt, s.x, s.y) ? AURA_ATK : 1);
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
    for (const g of tw.gar.s) {
      let e = null, ed = 3.7; for (const v of U) { if (!atWar(S, tw.owner, v.a.owner) || v.a.owner === tw.owner) continue; const d = Math.hypot(v.s.x - tx, v.s.y - ty); if (d < ed) { ed = d; e = v; } }
      if (!e) continue; g.atk = S.time; const mult = SQ.archer.atk * dt * mm('archer', e.tt) * BAT * 1.15 * defMult(S, e.a) * dfa2(e) * (ow && ow.human ? 1 : ow ? ow.mult : 1);
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
  for (const a of arm.slice()) { if ((a.bat || a.sg) && a.sq.some(q => q.s.some(x => x.hp <= 0))) reapArmy(S, a, a.lastBy != null ? a.lastBy : -1, true); }
  for (const a of arm) { if (a.st === 'fight' && !a.fight) a.st = 'idle'; if (a.bat || a.sg) for (const q of a.sq) syncSq(q); }
  S.prompts = S.prompts || [];
  for (const a of arm) if (a.owner === 0 && a.bat && !a.retreat && S.armies.includes(a)) { const n = units(a); if (a.pk && n <= a.pk * 0.55 && n < a.maxU * 0.7 && (!a.askT || S.time > a.askT) && !S.prompts.some(p => p.id === a.id)) { a.askT = S.time + 80; S.prompts.push({ id: a.id }); } }
  if (S.fx.length > 240) S.fx.splice(0, S.fx.length - 240);
}
function tickMove(S, dt) {
  for (const a of S.armies) {
    if (!a.ship) { if (blockedFor(S, a.owner, Math.floor(a.x), Math.floor(a.y))) { a.tres = (a.tres || 0) + dt; if (a.tres > 25 && !a.fight) { a.tres = 0; const e = retreat(S, a); if (!e && a.owner === 0) info(S, 0, 'Отход армии', 'Армия покидает чужую территорию.', 'info'); } } else a.tres = 0; }
    if (a.ship || a.fight || !a.path.length) { if (!a.ship && !a.fight && a.st === 'move' && !a.path.length) { a.st = 'idle'; } continue; }
    const w = a.path[0], cx0 = Math.floor(a.x), cy0 = Math.floor(a.y);
    if (structBlockedFor(S, a.owner, w.x, w.y)) { stopArmy(a); continue; }
    if (blockedFor(S, a.owner, w.x, w.y) && !blockedFor(S, a.owner, cx0, cy0)) { stopArmy(a); if (a.owner === 0) info(S, 0, 'Армия остановлена', 'Граница закрыта: нужна война или право прохода.', 'info'); continue; }
    const dx = w.x + 0.5 - a.x, dy = w.y + 0.5 - a.y, d = Math.hypot(dx, dy), step = armySpd(S, a) * dt * 1.0;
    if (dx) a.face = dx > 0 ? 1 : -1;
    if (d <= step) { a.x = w.x + 0.5; a.y = w.y + 0.5; a.path.shift(); } else { a.x += dx / d * step; a.y += dy / d * step; }
    if (!a.path.length) {
      a.st = 'idle'; a.retreat = false;
      if (a.garrison != null) { const T = bidx(S, a.garrison); a.garrison = null; if (T && dist(a.x, a.y, T.x + 0.5, T.y + 0.5) < 2.4) garrisonSquad(S, a, T); }
      if (a.siege != null) { const T = bidx(S, a.siege); if (T && dist(a.x, a.y, T.x + 0.5, T.y + 0.5) <= siegeReach(a) + 0.7) a.sg = true; }
      if (a.boardPort != null) { const port = bidx(S, a.boardPort); const sh = port && S.ships.find(s => s.owner === a.owner && s.kind === 'war' && !s.cargo && s.st === 'dock' && dist(s.x, s.y, port.x + 0.5, port.y + 0.5) < 4); if (sh) { sh.cargo = a.id; a.ship = sh.id; a.x = sh.x; a.y = sh.y; if (a.owner === 0) info(S, 0, 'Погрузка', 'Войска на борту. Выберите корабль и укажите берег.', 'info'); } a.boardPort = null; }
    }
  }
}
function tickCities(S, dt) {
  for (const c of S.cities) {
    const cx = c.x + 1, cy = c.y + 1, near = S.armies.filter(a => !a.ship && dist(a.x, a.y, cx, cy) <= 2.6);
    const def = near.filter(a => a.owner === c.owner), att = near.filter(a => a.owner !== c.owner && atWar(S, a.owner, c.owner));
    if (c.owner >= 0 && att.length) { // стрелки города
      const tg = att[0]; const q = tg.sq[(S.rnd() * tg.sq.length) | 0]; if (q && q.s.length) { q.s[(S.rnd() * q.s.length) | 0].hp -= 4 * c.level * dt * BAT * 2; reapArmy(S, tg, c.owner, true); }
    }
    if (!att.length) { if (c.cap) { c.cap.p -= 0.02 * dt; c.cap.hold = 0; if (c.cap.p <= 0) c.cap = null; } continue; }
    if (def.length) continue;
    const byO = {}; att.forEach(a => byO[a.owner] = (byO[a.owner] || 0) + units(a));
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
  const old = c.owner; c.owner = A; c.cap = null; const ap = S.players[A]; ap.captured++; ap.flags.capture = 1;
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
function checkVictory(S) {
  if (S.over) return;
  for (const p of S.players) { const was = p.alive; p.alive = S.cities.some(c => c.owner === p.id); if (was && !p.alive) { S.armies = S.armies.filter(a => a.owner !== p.id); S.ships = S.ships.filter(s => s.owner !== p.id); info(S, 0, 'Государство пало', p.name + ' больше не существует.', 'info'); } }
  const alive = S.players.filter(p => p.alive);
  if (S.opts.sandbox) return;
  if (!S.players[0].alive) { S.over = { winner: alive.length ? alive[0].id : -1, why: 'Вы потеряли все города' }; return; }
  if (S.opts.win === 'score') { const w = S.players.find(p => p.score >= S.opts.target); if (w) { S.over = { winner: w.id, why: 'Набрано ' + S.opts.target + ' очков' }; return; } }
  else { const caps = S.cities.filter(c => c.isCap); const own = new Set(caps.map(c => c.owner)); if (own.size === 1 && !own.has(-1)) { S.over = { winner: caps[0].owner, why: 'Захвачены все столицы' }; return; } }
  if (alive.length === 1) S.over = { winner: alive[0].id, why: 'Остался единственный правитель' };
}

/* ===== Главный шаг ===== */
function tick(S, dt) {
  if (S.over) return; S.time += dt; S.tk++;
  if (S.tk % 4 === 0) { calcRates(S); calcVision(S); }
  tickEcon(S, dt); tickProd(S, dt); tickMove(S, dt); tickCombat(S, dt); tickHospital(S, dt); tickCities(S, dt); tickShips(S, dt); tickTerr(S, dt);
  for (const r of [].concat(...S.rel.map(x => x.filter(Boolean)))) if (r.st === 'truce' && r.truceUntil <= S.time) { r.st = 'peace'; }
  if (S.pending) { const rest = []; for (const q of S.pending) { if (q.at <= S.time) decideOffer(S, q.from, q.to, q.kind); else rest.push(q); } S.pending = rest; }
  if (S.tk % 8 === 0) { for (const p of S.players) if (!p.human && p.alive) aiThink(S, p); calcScore(S); checkVictory(S); }
}

/* ===== Сохранение ===== */
function b64(u8) { let s = ''; for (let i = 0; i < u8.length; i++) s += String.fromCharCode(u8[i]); return btoa(s); }
function unb64(s) { const b = atob(s), u = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) u[i] = b.charCodeAt(i); return u; }
function saveGame(S) {
  const rels = []; for (let i = 0; i < S.players.length; i++) for (let j = i + 1; j < S.players.length; j++) rels.push([i, j, S.rel[i][j]]);
  const d = { v: 1, opts: S.opts, seed: S.seed, W: S.W, H: S.H, ter: b64(S.ter), res: b64(S.res), players: S.players, cities: S.cities, buildings: S.buildings, armies: S.armies, ships: S.ships, msgs: S.msgs, rels, time: S.time, nid: S.nid, plan: S.plan, pending: S.pending || [] };
  try { localStorage.setItem('kc_save', JSON.stringify(d, (k, v) => (k[0] === '_' || k === 'fx') ? undefined : v)); return true; } catch (e) { return false; }
}
function loadGame() {
  let d; try { d = JSON.parse(localStorage.getItem('kc_save')); } catch (e) { return null; } if (!d || d.v !== 1) return null;
  const W = d.W, H = d.H, ter = unb64(d.ter), res = unb64(d.res);
  const S = { opts: d.opts, seed: d.seed, W, H, ter, res, comp: labelComps(W, H, ter), occ: new Int32Array(W * H).fill(-1), terr: new Int8Array(W * H).fill(-2), players: d.players, cities: d.cities, buildings: d.buildings, armies: d.armies, ships: d.ships, msgs: d.msgs, rel: [], time: d.time, nid: d.nid, over: null, dirtyT: true, fx: [], tk: 0, rnd: mulberry32(d.seed + (d.nid | 0)), toast: [], plan: d.plan || 0, pending: d.pending };
  S.players.forEach((p, i) => S.rel[i] = []); d.rels.forEach(r => { S.rel[r[0]][r[1]] = r[2]; S.rel[r[1]][r[0]] = r[2]; });
  S.cities.forEach(c => { for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) S.occ[(c.y + dy) * W + c.x + dx] = 1000000 + c.id; });
  S.buildings.forEach(b => S.occ[b.y * W + b.x] = b.id);
  for (const a of S.armies) for (const q of a.sq) { ensureS(q); syncSq(q); }
  S.bmap = new Map(); S.mm = new Uint8Array(W * H); for (const b of S.buildings) { S.bmap.set(b.id, b); if (b.gar) { ensureS(b.gar); syncSq(b.gar); } } refreshStructs(S); recomputeMoat(S);
  recomputeTerr(S); calcScore(S); calcVision(S); return S;
}
