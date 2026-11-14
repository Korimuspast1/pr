'use strict';
/* ===== Генерация карты ===== */
function genMap(W, H, type, seed) {
  const rnd = mulberry32(seed + 5), nE = makeNoise(seed + 1), nW = makeNoise(seed + 31), nM = makeNoise(seed + 77), nF = makeNoise(seed + 191), nO = makeNoise(seed + 313);
  const ox = rnd() * 60, oy = rnd() * 60, fq = 1 + (rnd() - 0.5) * 0.5, blobs = [], lakes = [];
  const rot = () => rnd() * Math.PI, B = (cx, cy, r, asp) => blobs.push({ cx, cy, r, asp: asp || 1, ang: rot() });
  let frac;
  if (type === 'continents') {
    frac = 0.36 + rnd() * 0.08; const k = 2 + ((rnd() * 3) | 0); let tries = 0;
    while (blobs.length < k && tries++ < 400) { const r = 0.15 + rnd() * 0.12, cx = 0.18 + rnd() * 0.64, cy = 0.18 + rnd() * 0.64; if (blobs.every(p => Math.hypot(p.cx - cx, p.cy - cy) > p.r + r + 0.1)) B(cx, cy, r, 0.75 + rnd() * 0.6); }
  } else if (type === 'islands') {
    frac = 0.24 + rnd() * 0.08; const k = 8 + ((rnd() * 6) | 0); let tries = 0; B(0.3 + rnd() * 0.4, 0.3 + rnd() * 0.4, 0.15 + rnd() * 0.05, 0.8 + rnd() * 0.4);
    while (blobs.length < k && tries++ < 600) { const r = 0.05 + rnd() * 0.08, cx = 0.1 + rnd() * 0.8, cy = 0.1 + rnd() * 0.8; if (blobs.every(p => Math.hypot(p.cx - cx, p.cy - cy) > (p.r + r) * 0.9)) B(cx, cy, r, 0.7 + rnd() * 0.7); }
  } else if (type === 'lakes') {
    frac = 0.58 + rnd() * 0.08; B(0.5 + (rnd() - 0.5) * 0.1, 0.5 + (rnd() - 0.5) * 0.1, 0.55 + rnd() * 0.12, 0.85 + rnd() * 0.3);
    const k = 3 + ((rnd() * 4) | 0); let tries = 0; while (lakes.length < k && tries++ < 300) { const r = 0.06 + rnd() * 0.1, cx = 0.2 + rnd() * 0.6, cy = 0.2 + rnd() * 0.6; if (lakes.every(p => Math.hypot(p.cx - cx, p.cy - cy) > (p.r + r) * 0.9)) lakes.push({ cx, cy, r, asp: 0.7 + rnd() * 0.7, ang: rot() }); }
  } else {
    frac = 0.44 + rnd() * 0.08; const r0 = 0.34 + rnd() * 0.1; B(0.5 + (rnd() - 0.5) * 0.14, 0.5 + (rnd() - 0.5) * 0.14, r0, 0.8 + rnd() * 0.45);
    const k = (rnd() * 3) | 0; for (let j = 0; j < k; j++) { const a = rnd() * 6.283, d = r0 * (0.8 + rnd() * 0.5); B(0.5 + Math.cos(a) * d, 0.5 + Math.sin(a) * d, 0.1 + rnd() * 0.08, 0.8 + rnd() * 0.5); }
    if (rnd() < 0.5) lakes.push({ cx: 0.5 + (rnd() - 0.5) * 0.3, cy: 0.5 + (rnd() - 0.5) * 0.3, r: 0.06 + rnd() * 0.07, asp: 1, ang: rot() });
  }
  const fld = (b, x, y) => { const dx = x - b.cx, dy = y - b.cy, c = Math.cos(b.ang), s = Math.sin(b.ang), u = (dx * c + dy * s) / b.asp, v = (-dx * s + dy * c) * b.asp; return Math.hypot(u, v) / b.r; };
  const ter = new Uint8Array(W * H), res = new Uint8Array(W * H), E = new Float32Array(W * H), M = new Float32Array(W * H), F2 = new Float32Array(W * H);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const nx0 = x / W, ny0 = y / H, wx = nx0 + (nW(nx0 * 3 + ox, ny0 * 3 + oy, 3) - 0.5) * 0.16, wy = ny0 + (nW(nx0 * 3 + oy + 9, ny0 * 3 + ox + 9, 3) - 0.5) * 0.16, i = y * W + x;
    let L = -9; for (const b of blobs) L = Math.max(L, 1 - fld(b, wx, wy));
    for (const l of lakes) { const q = 1 - fld(l, wx, wy); if (q > 0) L -= q * 1.6; }
    const edge = Math.min(nx0, 1 - nx0, ny0, 1 - ny0); L -= Math.max(0, 0.07 - edge) * 8;
    E[i] = L * 0.9 + (nE(nx0 * 4.2 * fq + ox, ny0 * 4.2 * fq + oy, 5) - 0.5) * 0.7;
    M[i] = nM(nx0 * 4 + 50 + ox, ny0 * 4 + 50 + oy, 4); F2[i] = nF(nx0 * 17 + 5 + ox, ny0 * 17 + 5 + oy, 3);
  }
  const sorted = Array.from(E).sort((a, b) => a - b), thr = sorted[Math.floor((1 - frac) * sorted.length)], sp = (sorted[Math.floor(sorted.length * 0.9)] - sorted[Math.floor(sorted.length * 0.1)]) || 1, s = sp / 1.1;
  const le = []; for (let i = 0; i < W * H; i++) if (E[i] >= thr + 0.03 * s) le.push(E[i]); le.sort((a, b) => a - b);
  const hillT = le.length ? le[Math.floor(le.length * 0.88)] : 9, mountT = le.length ? le[Math.floor(le.length * 0.96)] : 9;
  // лес: небольшие рощи (≈13% суши), без сплошных массивов
  const sc = []; for (let i = 0; i < W * H; i++) if (E[i] >= thr + 0.03 * s && E[i] < hillT) sc.push(F2[i] * 0.65 + M[i] * 0.35); sc.sort((a, b) => a - b);
  const fT = sc.length ? sc[Math.floor(sc.length * 0.82)] : 9;
  for (let i = 0; i < W * H; i++) {
    const e = E[i], m = M[i], x = i % W, y = (i / W) | 0, nx = x / W, ny = y / H; let t;
    if (e < thr - 0.07 * s) t = TR.DEEP; else if (e < thr) t = TR.SHALLOW; else if (e < thr + 0.03 * s) t = TR.SAND;
    else if (e >= mountT) t = TR.MOUNT; else if (e >= hillT) t = TR.HILL;
    else if (F2[i] * 0.65 + m * 0.35 > fT) t = TR.FOREST; else if (m < 0.42) t = TR.PLAIN; else t = TR.GRASS;
    ter[i] = t;
    if ((t === TR.HILL || t === TR.MOUNT) && nO(nx * 14, ny * 14, 2) > 0.45 && rnd() < (t === TR.HILL ? 0.14 : 0.5)) res[i] = rnd() < 0.72 ? RES.IRON : RES.GOLD;
  }
  // прореживаем лес: никаких сплошных массивов
  for (let pass = 0; pass < 2; pass++) { const kill = []; for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; if (ter[i] !== TR.FOREST) continue; let n = 0; for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if ((dx || dy) && ter[(y + dy) * W + x + dx] === TR.FOREST) n++; if ((n >= 6 && rnd() < 0.6) || (n >= 4 && rnd() < 0.25)) kill.push(i); } for (const i of kill) ter[i] = M[i] < 0.42 ? TR.PLAIN : TR.GRASS; }
  // руда рассыпана по карте (не только в горах)
  { let land = 0; for (let i = 0; i < W * H; i++) if (ter[i] > 1) land++; const want = Math.floor(land / 55), spots = []; let tries = 0;
    for (let i = 0; i < W * H; i++) if (res[i]) spots.push([i % W, (i / W) | 0]);
    while (want > 0 && tries++ < want * 40) { const x = 2 + ((rnd() * (W - 4)) | 0), y = 2 + ((rnd() * (H - 4)) | 0), i = y * W + x, t = ter[i]; if (res[i] || !(t === TR.GRASS || t === TR.PLAIN || t === TR.HILL)) continue; if (spots.some(p => Math.hypot(p[0] - x, p[1] - y) < 4.5)) continue; res[i] = rnd() < 0.68 ? RES.IRON : RES.GOLD; spots.push([x, y]); if (spots.length > want + 60) break; } }
  const comp = labelComps(W, H, ter);
  return { ter, res, comp };
}
const compCat = t => t <= 1 ? 0 : t === TR.MOUNT ? 2 : 1; // вода / проходимая суша / горы
function labelComps(W, H, ter) {
  const comp = new Int32Array(W * H).fill(-1), q = new Int32Array(W * H); let id = 0;
  for (let s = 0; s < W * H; s++) {
    if (comp[s] >= 0) continue;
    const w = compCat(ter[s]); let h = 0, tl = 0; q[tl++] = s; comp[s] = id;
    while (h < tl) {
      const c = q[h++], cx = c % W, cy = (c / W) | 0;
      const nb = [cx > 0 ? c - 1 : -1, cx < W - 1 ? c + 1 : -1, cy > 0 ? c - W : -1, cy < H - 1 ? c + W : -1];
      for (const n of nb) if (n >= 0 && comp[n] < 0 && compCat(ter[n]) === w) { comp[n] = id; q[tl++] = n; }
    }
    id++;
  }
  return comp;
}

/* ===== Поиск пути A* ===== */
let _seen = null, _g = null, _from = null, _stamp = 0;
const ASTAR_COST = [1, 1, 1, 1, 1, 1.6, 1.8, 3.2]; // цена шага по видам местности
function astar(S, sx, sy, gx, gy, water, extra) {
  const W = S.W, H = S.H, ter = S.ter, N = W * H;
  if (!_seen || _seen.length !== N) { _seen = new Int32Array(N); _g = new Float32Array(N); _from = new Int32Array(N); _stamp = 0; }
  _stamp++;
  const pass0 = (x, y) => x >= 0 && y >= 0 && x < W && y < H && (isWater(ter[y * W + x]) === water) && (water || ter[y * W + x] !== TR.MOUNT);
  const pass = (x, y) => pass0(x, y) && (!extra || !extra(x, y));
  if (!pass0(sx, sy) || !pass0(gx, gy)) return null;
  const tc = water ? () => 1 : t => ASTAR_COST[t];
  const hp = [], push = (f, i) => { hp.push([f, i]); let k = hp.length - 1; while (k > 0) { const p = (k - 1) >> 1; if (hp[p][0] <= hp[k][0]) break; [hp[p], hp[k]] = [hp[k], hp[p]]; k = p; } };
  const pop = () => { const top = hp[0], last = hp.pop(); if (hp.length) { hp[0] = last; let k = 0; for (;;) { let l = 2 * k + 1, r = l + 1, m = k; if (l < hp.length && hp[l][0] < hp[m][0]) m = l; if (r < hp.length && hp[r][0] < hp[m][0]) m = r; if (m === k) break; [hp[m], hp[k]] = [hp[k], hp[m]]; k = m; } } return top; };
  const s = sy * W + sx, g = gy * W + gx;
  _seen[s] = _stamp; _g[s] = 0; _from[s] = -1; push(0, s);
  const D = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]];
  let it = 0;
  while (hp.length && it++ < 60000) {
    const [, c] = pop(); if (c === g) break;
    const cx = c % W, cy = (c / W) | 0;
    for (const d of D) {
      const nx = cx + d[0], ny = cy + d[1]; if (!pass(nx, ny)) continue;
      if (d[0] && d[1] && (!pass(cx + d[0], cy) || !pass(cx, cy + d[1]))) continue;
      const n = ny * W + nx, ng = _g[c] + (d[0] && d[1] ? 1.414 : 1) * tc(ter[n]);
      if (_seen[n] !== _stamp || ng < _g[n]) { _seen[n] = _stamp; _g[n] = ng; _from[n] = c; const dx = Math.abs(nx - gx), dy = Math.abs(ny - gy); push(ng + Math.max(dx, dy) + 0.414 * Math.min(dx, dy), n); }
    }
  }
  if (_seen[g] !== _stamp) return null;
  const path = []; let c = g; while (c !== s) { path.push({ x: c % W, y: (c / W) | 0 }); c = _from[c]; }
  return path.reverse();
}
// ближайший тайл нужного типа (вода/суша) вокруг точки
function nearestTile(S, x, y, water, maxR) {
  x = Math.floor(x); y = Math.floor(y);
  for (let r = 0; r <= (maxR || 6); r++)
    for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue;
      const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= S.W || ny >= S.H) continue;
      const tt = S.ter[ny * S.W + nx]; if (isWater(tt) === water && (water || tt !== TR.MOUNT)) return { x: nx, y: ny };
    }
  return null;
}
