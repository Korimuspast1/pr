'use strict';
/* ===== Боты ===== */
function bldAt(S, x, y) { if (x < 0 || y < 0 || x >= S.W || y >= S.H) return null; const o = S.occ[y * S.W + x]; return o >= 0 && o < 1000000 ? S.bmap.get(o) || null : null; }

function aiFindSite(S, p, type) {
  if (BD[type].one && S.buildings.some(b => b.type === type && b.owner === p.id)) return null;
  const cands = [];
  for (const c of S.cities) if (c.owner === p.id) {
    const cx = c.x + 1, cy = c.y + 1, r = Math.floor(c.radius);
    for (let y = Math.max(0, cy - r); y <= Math.min(S.H - 1, cy + r); y++) for (let x = Math.max(0, cx - r); x <= Math.min(S.W - 1, cx + r); x++) {
      if (siteTile(S, p.id, type, x, y)) continue; const d = dist(x, y, cx, cy);
      if (type === 'port' || d >= 2) cands.push({ x, y, d });
    }
  }
  if (!cands.length) return null;
  if (type === 'mill') { // мельницу ставим там, где она накроет больше ферм
    let best = null, bn = 1;
    for (const q of cands) {
      let n = 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const b = bldAt(S, q.x + dx, q.y + dy); if (b && b.type === 'farm' && b.owner === p.id && !millNear(S, b)) n++; }
      if (n > bn) { bn = n; best = q; }
    }
    return best;
  }
  cands.sort((a, b) => a.d - b.d); const k = Math.min(cands.length, 6);
  return cands[(S.rnd() * k) | 0];
}
function aiSquadStats(S, pid) {
  const cnt = { archer: 0, light: 0, heavy: 0, cav: 0, spear: 0, militia: 0 };
  for (const a of S.armies) if (a.owner === pid) for (const q of a.sq) if (cnt[q.t] !== undefined) cnt[q.t]++;
  for (const b of S.buildings) if (b.owner === pid && b.type === 'barracks') for (const q of b.queue) if (cnt[q.t] !== undefined) cnt[q.t]++;
  return cnt;
}
function aiThink(S, p) {
  const ai = p.ai; if (S.time < ai.next) return; ai.next = S.time + DIFF[p.diff].think * (1.15 + S.rnd() * 0.5); // боты развиваются в своём темпе — не слишком быстро
  const mine = S.cities.filter(c => c.owner === p.id); if (!mine.length) return; const nc = mine.length, easy = p.diff === 'easy';
  if (S.time - (ai.failsAt || 0) > 150) { ai.fails = {}; ai.failsAt = S.time; } // границы растут: неудачные места пробуем заново
  const own = S.buildings.filter(b => b.owner === p.id), cnt = {}; for (const b of own) cnt[b.type] = (cnt[b.type] || 0) + 1;

  // --- строительство
  const plan = [['farm', 2], ['sawmill', 2], ['mine', 2], ['barracks', 1], ['hospital', 1], ['workshop', 1], ['forge', 1], ['farm', 3], ['mill', 1], ['stable', 1], ['sawmill', 3], ['market', 1], ['academy', easy ? 0 : 1], ['mine', 3], ['house', 3],
    ['barracks', 2], ['siege', easy ? 0 : 1], ['hospital', 1 + Math.floor(nc / 3)], ['forge', 2], ['farm', 3 + nc * 2], ['port', 1], ['workshop', 2], ['stable', 2], ['tavern', easy ? 0 : 1], ['sawmill', 3 + nc * 2], ['mine', 3 + nc], ['market', 1 + nc], ['mill', 1 + Math.floor(nc / 2)], ['house', 3 + nc * 2]];
  for (const [type, n] of plan) {
    if ((cnt[type] || 0) >= n) continue;
    if ((ai.fails[type] || 0) > 4) continue;
    if (type === 'port' && easy) continue;
    if (!canAfford(p, BD[type].cost)) break;
    const site = aiFindSite(S, p, type);
    if (!site) { ai.fails[type] = (ai.fails[type] || 0) + 1; continue; }
    doBuild(S, p.id, type, site.x, site.y); break;
  }
  // новый город: выбираем место, где можно занять больше свободной земли
  if (nc < 2 + Math.floor(S.time / 300) && nc < 12 && canAfford(p, cityCost(S, p.id)) && p.stock.gold > cityCost(S, p.id).gold + 100 && S.time > 120) {
    let best = null, bs = -1;
    for (let k = 0; k < 70; k++) {
      const c = mine[(S.rnd() * mine.length) | 0], a = S.rnd() * 6.283, r = CITY_MIN + 1 + S.rnd() * 6, x = Math.round(c.x + 1 + Math.cos(a) * r - 1), y = Math.round(c.y + 1 + Math.sin(a) * r - 1);
      if (cityOk(S, p.id, x, y)) continue; const cl = cityClaim(S, p.id, x, y), sc = cl.free * 2 + cl.land - cl.forest * 0.5; if (sc > bs) { bs = sc; best = { x, y }; }
    }
    if (best && bs > 25) doBuild(S, p.id, 'city', best.x, best.y);
  }
  // улучшение городов
  for (const c of mine) if (c.level < 2 + (p.diff === 'hard' ? 2 : 1) && p.stock.gold > upgradeCost(c).gold + 200 && canAfford(p, upgradeCost(c))) { upgradeCity(S, c); break; }

  // --- что производить: то, чего на складе не хватает сильнее всего
  const want2 = { forge: { sword: 20, armL: 15, armH: 12 }, workshop: { bow: 15, spear: 12 } };
  for (const b of own) { const tgt = want2[b.type]; if (!tgt || b.busy) continue; let best = b.recipe, bd = -99; for (const k in tgt) { const d = tgt[k] - p.stock[k]; if (d > bd) { bd = d; best = k; } } b.recipe = best; }

  // --- обучение (число отрядов ограничено: иначе к концу партии их сотни)
  const st = aiSquadStats(S, p.id), capSq = Math.min(5 + 3 * nc, 30), want = { archer: 3, light: 2.5, heavy: 2, cav: 2.5, spear: 2 };
  let totSq = st.archer + st.light + st.heavy + st.cav + st.spear + st.militia;
  for (const b of own) if (b.type === 'barracks' && b.queue.length < 2 && totSq < capSq) {
    const ord = Object.keys(want).sort((a, c) => st[a] / want[a] - st[c] / want[c]); let done = false;
    for (const t of ord) { if (enqueue(S, b, t) === null) { st[t]++; totSq++; done = true; break; } }
    // оружия ещё нет — для обороны сойдёт и ополчение
    if (!done && totSq < 3 && S.time > 90 && enqueue(S, b, 'militia') === null) { st.militia++; totSq++; }
  }
  for (const b of own) if (b.type === 'siege' && b.queue.length < 1) { let nr = 0, nc2 = 0; for (const a of S.armies) if (a.owner === p.id) for (const q of a.sq) { if (q.t === 'ram') nr++; if (q.t === 'cat') nc2++; } if (nr + nc2 < Math.min(2 + nc, 6)) enqueue(S, b, nr <= nc2 ? 'ram' : 'cat'); }
  for (const b of own) if (b.type === 'academy' && !b.queue.length && !hasCommander(S, p.id)) { const cn = { archer: 0, light: 0, heavy: 0, cav: 0 }; for (const a of S.armies) if (a.owner === p.id) for (const q of a.sq) if (cn[q.t] !== undefined) cn[q.t] += q.u; const cls = Object.keys(cn).sort((x, y) => cn[y] - cn[x])[0]; if (cn[cls] >= 10) enqueueCmd(S, b, cls); }
  // богатый бот нанимает наёмников в таверне
  if (!easy && p.stock.gold > 700 && totSq < capSq) for (const b of own) if (b.type === 'tavern' && (b.cd || 0) <= S.time) { const of = tavernOffer(S, b), t = of[1 + ((S.rnd() * 2) | 0)]; if (p.stock.gold > MERC[t] + 400 && hireMerc(S, b, t) === null) break; }

  // --- армии
  aiArmies(S, p, mine);
  // --- дипломатия
  if (S.tk % 40 === 0) aiDiplomacy(S, p);
  // --- торговые суда
  for (const b of own) if (b.type === 'port' && S.ships.filter(s => s.home === b.id).length < 1 && S.players.some(q => q.id !== p.id && q.alive && rel(S, p.id, q.id).trade)) buildShip(S, b, 'trade');
}
function aiDiplomacy(S, p) {
  const cch = { easy: 0.02, normal: 0.045, hard: 0.09 }[p.diff], pw = milPower(S, p.id);
  for (const q of S.players) {
    if (q.id === p.id || !q.alive) continue; const r = rel(S, p.id, q.id);
    if (r.st === 'peace' && r.trade && !r.ally && r.op > 15 && S.rnd() < 0.15) offer(S, p.id, q.id, 'ally');
    if (r.st === 'peace' && !r.ally && S.time > 150 && !claimInfo(S, p.id, q.id) && S.rnd() < cch && milPower(S, q.id) < pw * 1.1 && !S.players.some(z => z.id !== p.id && z.alive && rel(S, p.id, z.id).st === 'war' && z.id !== q.id)) startClaim(S, p.id, q.id);
    if (r.st === 'peace' && !r.trade && S.rnd() < 0.3 && r.op > -20) offer(S, p.id, q.id, 'trade');
    if (r.st === 'war' && S.time - r.warSince > 120 && pw < milPower(S, q.id) * 0.7 && S.rnd() < 0.5) offer(S, p.id, q.id, 'truce');
    // сильный бот может потребовать дань у слабого игрока
    if (q.human && p.diff !== 'easy' && r.st === 'peace' && !r.ally && S.time > 3 * DAY && S.rnd() < (p.diff === 'hard' ? 0.1 : 0.05) && pw > milPower(S, q.id) * 1.6 + 20) offer(S, p.id, q.id, 'ult');
  }
}
/* Армии бота. Самые крупные армии — ударные, они идут в поход. Остальные отряды стягиваются к ближайшей
   неполной армии и вливаются в неё (раньше в бой ходила одна армия, а десятки отрядов стояли без дела). */
function aiArmies(S, p, mine) {
  const my = S.armies.filter(a => a.owner === p.id && !a.ship); if (!my.length) return;
  const cap = mine[0], hx = cap.x + 1, hy = cap.y + 1;
  my.sort((a, b) => b.sq.length - a.sq.length || units(b) - units(a));
  const need = { easy: 5, normal: 4, hard: 3 }[p.diff], start = { easy: 520, normal: 420, hard: 320 }[p.diff], maxLead = { easy: 1, normal: 2, hard: 4 }[p.diff];
  const leaders = [], hubs = [];
  for (const a of my) {
    if (!S.armies.includes(a)) continue; // уже влилась в другую армию
    if (!leaders.length || (a.sq.length >= need && leaders.length < maxLead)) { leaders.push(a); if (a.sq.length < MAX_SQ_ARMY) hubs.push(a); continue; }
    if (a.st === 'fight') continue;
    const cp = landComp(S, Math.floor(a.x), Math.floor(a.y)); let t = null, td = 30;
    for (const h of hubs) { if (h.sq.length + a.sq.length > MAX_SQ_ARMY || !S.armies.includes(h) || landComp(S, Math.floor(h.x), Math.floor(h.y)) !== cp) continue; const d = dist(a.x, a.y, h.x, h.y); if (d < td) { td = d; t = h; } }
    if (!t) { // присоединиться не к кому: сама становится точкой сбора и держится у столицы
      if (a.sq.length < MAX_SQ_ARMY) hubs.push(a);
      if (a.st === 'idle' && dist(a.x, a.y, hx, hy) > 8 && S.time > (a.aiT || 0)) { a.aiT = S.time + 20; orderMove(S, a, hx, hy + 1); }
      continue;
    }
    if (td < 1.8) { if (t.st !== 'fight') mergeNear(S, t); }
    else if ((a.st === 'idle' || !a.path.length) && S.time > (a.aiT || 0)) { a.aiT = S.time + 8; if (orderMove(S, a, t.x, t.y)) a.aiT = S.time + 25; }
  }
  for (const a of leaders) aiLead(S, p, a, hx, hy, need, start);
}
function aiLead(S, p, main, hx, hy, need, start) {
  if (!S.armies.includes(main) || main.st === 'fight') return;
  const u = units(main);
  if (main.bat && !main.retreat && u < (main.pk || u) * 0.5) { retreat(S, main); return; }
  if (main.retreat) return;
  { // у госпиталя ждём, пока подлечат (вернуть раненых можно, только если есть чем платить)
    const hs = nearestHospital(S, main);
    if (hs && dist(main.x, main.y, hs.x + 0.5, hs.y + 0.5) < 4 && !main.bat && !main.path.length) {
      const rich = canAfford(p, { gold: 8, food: 4 }), hurt = main.sq.some(q => q.s.some(x => x.hp < SQ[q.t].hp * 0.9) || (rich && q.w > 0 && q.s.length < sqMax(q.t)));
      if (hurt) return;
    }
  }
  const wait = S.time <= (main.aiT || 0);
  if (u < armyCap(main) * 0.35 && main.sq.length < 3) { if (main.st === 'idle' && !wait) { main.aiT = S.time + 20; retreat(S, main); } return; }
  if (main.sq.length < need || S.time < start) { if (main.st === 'idle' && !wait && dist(main.x, main.y, hx, hy) > 6) { main.aiT = S.time + 10; orderMove(S, main, hx, hy + 1); } return; }
  if (main.st === 'move' && main.path.length) return;
  if (main.sg && main.siege != null && bidx(S, main.siege)) return; // осада идёт — продолжаем
  const comp = S.comp[Math.floor(main.y) * S.W + Math.floor(main.x)], bad = main.aiBad || (main.aiBad = {});
  let best = null, bs = 1e9;
  for (const c of S.cities) {
    if (c.owner === p.id) continue; if (c.owner >= 0 && !atWar(S, p.id, c.owner)) continue;
    if (S.comp[c.y * S.W + c.x] !== comp || bad[c.id] > S.time) continue;
    if (c.owner < 0 && u < garrison(c) * 0.5) continue; // вольный город малой армией не взять
    let def = 0; if (c.owner >= 0) for (const a of S.armies) if (a.owner === c.owner && dist(a.x, a.y, c.x + 1, c.y + 1) < 5) def += units(a);
    if (def > u * 1.3) continue;
    const sc = dist(main.x, main.y, c.x + 1, c.y + 1) * (1 + c.level * 0.2) + (c.owner < 0 ? -10 : 0); if (sc < bs) { bs = sc; best = c; }
  }
  if (!best) {
    for (const q of S.players) { if (q.id === p.id || !q.alive) continue; const ci = claimInfo(S, p.id, q.id); if (ci && ci.ready && rel(S, p.id, q.id).st === 'peace' && S.rnd() < 0.7) { declareWar(S, p.id, q.id); break; } }
    const chance = { easy: 0.05, normal: 0.12, hard: 0.25 }[p.diff];
    if (S.rnd() < chance) {
      let tg = null, td = 1e9; const pw = milPower(S, p.id);
      for (const c of S.cities) if (c.owner >= 0 && c.owner !== p.id && S.comp[c.y * S.W + c.x] === comp) { const r = rel(S, p.id, c.owner); if (r.st === 'truce' || r.ally) continue; if (milPower(S, c.owner) > pw * 1.2) continue; const d = dist(main.x, main.y, c.x, c.y); if (d < td) { td = d; tg = c.owner; } }
      if (tg !== null) { const ci = claimInfo(S, p.id, tg); if (ci && ci.ready) declareWar(S, p.id, tg); else if (!ci) startClaim(S, p.id, tg); }
    }
    if (main.st === 'idle' && !wait && dist(main.x, main.y, hx, hy) > 6) { main.aiT = S.time + 10; orderMove(S, main, hx, hy + 1); }
    return;
  }
  const er = orderMove(S, main, best.x + 1, best.y + 1);
  if (er) { // дороги нет: ломаем ближайшее вражеское укрепление, а если и его нет — на время забываем об этой цели
    let tg = null, td = 14;
    for (const b of S.structs) { if (b.owner < 0 || b.owner === p.id || !atWar(S, p.id, b.owner)) continue; const d = dist(main.x, main.y, b.x, b.y) + dist(b.x, b.y, best.x, best.y) * 0.3; if (d < td) { td = d; tg = b; } }
    if (!tg || orderMove(S, main, tg.x + 0.5, tg.y + 0.5)) bad[best.id] = S.time + 60;
  }
}
