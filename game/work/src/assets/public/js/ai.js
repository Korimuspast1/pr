'use strict';
/* ===== Боты ===== */
function aiFindSite(S, p, type) {
  const cands = [];
  for (const c of S.cities) if (c.owner === p.id) {
    const cx = c.x + 1, cy = c.y + 1, r = Math.floor(c.radius);
    for (let y = Math.max(0, cy - r); y <= Math.min(S.H - 1, cy + r); y++) for (let x = Math.max(0, cx - r); x <= Math.min(S.W - 1, cx + r); x++) {
      if (siteOk(S, p.id, type, x, y)) continue; const d = dist(x, y, cx, cy);
      if (type === 'port' || d >= 2) cands.push({ x, y, d });
    }
  }
  if (!cands.length) return null;
  cands.sort((a, b) => a.d - b.d); const k = Math.min(cands.length, 6);
  return cands[(S.rnd() * k) | 0];
}
function aiCount(S, pid, type) { let n = 0; for (const b of S.buildings) if (b.owner === pid && b.type === type) n++; return n; }
function aiSquadStats(S, pid) {
  const cnt = { archer: 0, light: 0, heavy: 0, cav: 0 };
  for (const a of S.armies) if (a.owner === pid) for (const q of a.sq) if (cnt[q.t] !== undefined) cnt[q.t]++;
  for (const b of S.buildings) if (b.owner === pid && b.type === 'barracks') for (const q of b.queue) if (cnt[q.t] !== undefined) cnt[q.t]++;
  return cnt;
}
function aiThink(S, p) {
  const ai = p.ai; if (S.time < ai.next) return; ai.next = S.time + DIFF[p.diff].think * (0.8 + S.rnd() * 0.4);
  const mine = S.cities.filter(c => c.owner === p.id); if (!mine.length) return; const nc = mine.length;
  // --- строительство
  const plan = [['farm', 2], ['sawmill', 2], ['mine', 2], ['barracks', 1], ['hospital', 1], ['workshop', 1], ['forge', 1], ['farm', 3], ['stable', 1], ['sawmill', 3], ['market', 1], ['academy', p.diff === 'easy' ? 0 : 1], ['mine', 3],
    ['barracks', 2], ['siege', p.diff === 'easy' ? 0 : 1], ['hospital', 1 + Math.floor(nc / 3)], ['forge', 2], ['farm', 3 + nc * 2], ['port', 1], ['workshop', 2], ['stable', 2], ['sawmill', 3 + nc * 2], ['mine', 3 + nc], ['market', 1 + nc]];
  for (const [type, n] of plan) {
    if (aiCount(S, p.id, type) >= n) continue;
    if ((ai.fails[type] || 0) > 4) continue;
    if (type === 'port' && p.diff === 'easy') continue;
    if (!canAfford(p, BD[type].cost)) break;
    const site = aiFindSite(S, p, type);
    if (!site) { ai.fails[type] = (ai.fails[type] || 0) + 1; continue; }
    doBuild(S, p.id, type, site.x, site.y); break;
  }
  // новый город: выбираем место, где можно захватить больше свободной земли
  if (nc < 2 + Math.floor(S.time / 300) && nc < 12 && canAfford(p, cityCost(S, p.id)) && p.stock.gold > cityCost(S, p.id).gold + 100 && S.time > 120) {
    let best = null, bs = -1;
    for (let k = 0; k < 70; k++) { const c = mine[(S.rnd() * mine.length) | 0], a = S.rnd() * 6.283, r = CITY_MIN + 1 + S.rnd() * 6, x = Math.round(c.x + 1 + Math.cos(a) * r - 1), y = Math.round(c.y + 1 + Math.sin(a) * r - 1); if (cityOk(S, p.id, x, y)) continue; const cl = cityClaim(S, p.id, x, y), sc = cl.free * 2 + cl.land - cl.forest * 0.5; if (sc > bs) { bs = sc; best = { x, y }; } }
    if (best && bs > 25) doBuild(S, p.id, 'city', best.x, best.y);
  }
  // улучшение городов
  for (const c of mine) if (c.level < 3 + (p.diff === 'hard' ? 2 : 0) && p.stock.gold > upgradeCost(c).gold + 200 && canAfford(p, upgradeCost(c))) { upgradeCity(S, c); break; }
  // --- рецепты
  const tgt = { sword: 20, armL: 15, armH: 12 };
  for (const b of S.buildings) if (b.owner === p.id && b.type === 'forge') { let best = b.recipe, bd = -99; for (const k in tgt) { const d = tgt[k] - p.stock[k]; if (d > bd) { bd = d; best = k; } } if (!b.busy) b.recipe = best; }
  // --- обучение
  const st = aiSquadStats(S, p.id), totSq = st.archer + st.light + st.heavy + st.cav, want = { archer: 3, light: 2.5, heavy: 2, cav: 2.5 };
  for (const b of S.buildings) if (b.owner === p.id && b.type === 'barracks' && b.queue.length < 2 && totSq + b.queue.length < 6 + 5 * nc) {
    const ord = Object.keys(want).sort((a, c) => st[a] / want[a] - st[c] / want[c]);
    for (const t of ord) { if (enqueue(S, b, t) === null) { st[t]++; break; } }
  }
  for (const b of S.buildings) if (b.owner === p.id && b.type === 'siege' && b.queue.length < 1) { let nr = 0, nc2 = 0; for (const a of S.armies) if (a.owner === p.id) for (const q of a.sq) { if (q.t === 'ram') nr++; if (q.t === 'cat') nc2++; } if (nr + nc2 < 2 + nc) enqueue(S, b, nr <= nc2 ? 'ram' : 'cat'); }
  for (const b of S.buildings) if (b.owner === p.id && b.type === 'academy' && !b.queue.length && !hasCommander(S, p.id)) { const cn = { archer: 0, light: 0, heavy: 0, cav: 0 }; for (const a of S.armies) if (a.owner === p.id) for (const q of a.sq) if (cn[q.t] !== undefined) cn[q.t] += q.u; const cls = Object.keys(cn).sort((x, y) => cn[y] - cn[x])[0]; if (cn[cls] >= 10) enqueueCmd(S, b, cls); }
  // --- армии
  aiArmies(S, p, mine);
  // --- дипломатия
  if (S.tk % 40 === 0) aiDiplomacy(S, p);
  // --- торговые суда
  for (const b of S.buildings) if (b.owner === p.id && b.type === 'port' && S.ships.filter(s => s.home === b.id).length < 1 && S.players.some(q => q.id !== p.id && rel(S, p.id, q.id).trade)) buildShip(S, b, 'trade');
}
function aiDiplomacy(S, p) {
  const cch = { easy: 0.03, normal: 0.06, hard: 0.12 }[p.diff];
  for (const q of S.players) {
    if (q.id === p.id || !q.alive) continue; const r = rel(S, p.id, q.id);
    if (r.st === 'peace' && r.trade && !r.ally && r.op > 15 && S.rnd() < 0.15) offer(S, p.id, q.id, 'ally');
    if (r.st === 'peace' && !r.ally && S.time > 150 && !claimInfo(S, p.id, q.id) && S.rnd() < cch && milPower(S, q.id) < milPower(S, p.id) * 1.1 && !S.players.some(z => z.id !== p.id && z.alive && rel(S, p.id, z.id).st === 'war' && z.id !== q.id)) startClaim(S, p.id, q.id);
    if (r.st === 'peace' && !r.trade && S.rnd() < 0.3 && r.op > -20) offer(S, p.id, q.id, 'trade');
    if (r.st === 'war' && S.time - r.warSince > 120 && milPower(S, p.id) < milPower(S, q.id) * 0.7 && S.rnd() < 0.5) offer(S, p.id, q.id, 'truce');
  }
}
function aiArmies(S, p, mine) {
  const my = S.armies.filter(a => a.owner === p.id && !a.ship); if (!my.length) return;
  const cap = mine[0], hx = cap.x + 1, hy = cap.y + 1;
  my.sort((a, b) => units(b) - units(a)); const main = my[0];
  const need = { easy: 5, normal: 4, hard: 3 }[p.diff], start = { easy: 420, normal: 300, hard: 200 }[p.diff];
  for (const a of my) {
    if (a === main || a.st === 'fight') continue;
    if (a.sq.length >= need) { aiLead(S, p, a, hx, hy, need, start); continue; }
    if (dist(a.x, a.y, main.x, main.y) < 1.8 && a.st === 'idle') { if (main.sq.length + a.sq.length <= MAX_SQ_ARMY) mergeNear(S, main); }
    else if ((a.st === 'idle' || !a.path.length) && main.sq.length < MAX_SQ_ARMY) orderMove(S, a, main.x, main.y);
  }
  aiLead(S, p, main, hx, hy, need, start);
}
function aiLead(S, p, main, hx, hy, need, start) {
  if (main.st === 'fight') return;
  if (main.bat && !main.retreat && units(main) < main.maxU * 0.5) { retreat(S, main); return; }
  { const hs = nearestHospital(S, main); if (hs && dist(main.x, main.y, hs.x + 0.5, hs.y + 0.5) < 4 && !main.bat && !main.path.length) { const hurt = main.sq.some(q => q.s.some(x => x.hp < SQ[q.t].hp * 0.9) || (q.w > 0 && q.s.length < 10)); if (hurt) return; } }
  if (units(main) < main.maxU * 0.35 && main.sq.length < 3) { if (main.st === 'idle') retreat(S, main); return; }
  if (main.sq.length < need || S.time < start) { if (main.st === 'idle' && dist(main.x, main.y, hx, hy) > 6) orderMove(S, main, hx, hy + 1); return; }
  if (main.st === 'move' && main.path.length) return;
  const comp = S.comp[Math.floor(main.y) * S.W + Math.floor(main.x)];
  let best = null, bs = 1e9;
  for (const c of S.cities) {
    if (c.owner === p.id) continue; if (c.owner >= 0 && !atWar(S, p.id, c.owner)) continue;
    if (S.comp[c.y * S.W + c.x] !== comp) continue;
    let def = 0; for (const a of S.armies) if (a.owner === c.owner && c.owner >= 0 && dist(a.x, a.y, c.x + 1, c.y + 1) < 5) def += units(a);
    if (def > units(main) * 1.3) continue;
    const sc = dist(main.x, main.y, c.x + 1, c.y + 1) * (1 + c.level * 0.2) + (c.owner < 0 ? -10 : 0); if (sc < bs) { bs = sc; best = c; }
  }
  if (!best) {
    for (const q of S.players) { if (q.id === p.id || !q.alive) continue; const ci = claimInfo(S, p.id, q.id); if (ci && ci.ready && rel(S, p.id, q.id).st === 'peace' && S.rnd() < 0.7) { declareWar(S, p.id, q.id); break; } }
    const chance = { easy: 0.05, normal: 0.12, hard: 0.25 }[p.diff];
    if (S.rnd() < chance) {
      let tg = null, td = 1e9;
      for (const c of S.cities) if (c.owner >= 0 && c.owner !== p.id && S.comp[c.y * S.W + c.x] === comp) { const r = rel(S, p.id, c.owner); if (r.st === 'truce') continue; if (milPower(S, c.owner) > milPower(S, p.id) * 1.2) continue; const d = dist(main.x, main.y, c.x, c.y); if (d < td) { td = d; tg = c.owner; } }
      if (tg !== null) { const ci = claimInfo(S, p.id, tg); if (ci && ci.ready) declareWar(S, p.id, tg); else if (!ci) startClaim(S, p.id, tg); }
    }
    if (main.st === 'idle' && dist(main.x, main.y, hx, hy) > 6) orderMove(S, main, hx, hy + 1); return;
  }
  const er = orderMove(S, main, best.x + 1, best.y + 1);
  if (er && S.structs) { let tg = null, td = 14; for (const b of S.structs) { if (b.owner < 0 || b.owner === p.id || !atWar(S, p.id, b.owner)) continue; const d = dist(main.x, main.y, b.x, b.y) + dist(b.x, b.y, best.x, best.y) * 0.3; if (d < td) { td = d; tg = b; } } if (tg) orderMove(S, main, tg.x + 0.5, tg.y + 0.5); }
}
