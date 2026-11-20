'use strict';
/* ===== Боты =====
   Боты думают как игрок: закрывают дефициты (еда, дерево, железо, золото) в первую очередь,
   держат оборону, когда рядом враг, выбирают слабые цели для войны и не начинают вторую войну,
   пока не кончилась первая. Сила от сложности: лёгкий — ошибки и медлительность, сложный — безошибочное
   производство, быстрая перестройка экономики и агрессия. */
function bldAt(S, x, y) { if (x < 0 || y < 0 || x >= S.W || y >= S.H) return null; const o = S.occ[y * S.W + x]; return o >= 0 && o < 1000000 ? S.bmap.get(o) || null : null; }
// список войн игрока (живые противники с отношением «война»)
function aiWars(S, pid) { const out = []; for (const q of S.players) if (q.id !== pid && q.alive && rel(S, pid, q.id).st === 'war') out.push(q.id); return out; }
// оборона укреплений противника у цели: башни сильно бьют — в расчёт
function aiTowerDef(S, pid, cx, cy, r) { let n = 0; for (const b of S.buildings) if (b.type === 'tower' && b.owner >= 0 && b.owner !== pid && atWar(S, pid, b.owner) && dist(b.x + 0.5, b.y + 0.5, cx + 1, cy + 1) <= r) n += 12 + ((b.gar && b.gar.s) ? b.gar.s.length * 2 : 0); return n; }

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
  if (type === 'tower') { // башню не в поле ставим, а прикрывая город: ближе к городу и рядом с врагом, если он рядом
    cands.sort((a, b) => a.d - b.d); return cands[0];
  }
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
  const cnt = { archer: 0, light: 0, heavy: 0, cav: 0, spear: 0, militia: 0, xbow: 0 };
  for (const a of S.armies) if (a.owner === pid) for (const q of a.sq) if (cnt[q.t] !== undefined) cnt[q.t]++;
  for (const b of S.buildings) if (b.owner === pid && b.type === 'barracks') for (const q of b.queue) if (cnt[q.t] !== undefined) cnt[q.t]++;
  return cnt;
}
/* Самое срочное строительство: закрывает дефициты и угрозы. Возвращает тип здания или null. */
function aiReaction(S, p, mine, cnt, own) {
  const nc = mine.length, rateF = p.rate ? p.rate.food : 0, rateW = p.rate ? p.rate.wood : 0, rateI = p.rate ? p.rate.iron : 0, rateG = p.rate ? p.rate.gold : 0;
  const war = aiWars(S, p.id).length > 0;
  const itemFull = ITEM_KEYS.some(k => p.stock[k] >= (p.itCap || ITEM_CAP));
  // госпиталь нужен, если армия большая и лечить негде
  const armySq = S.armies.reduce((n, a) => n + (a.owner === p.id ? a.sq.length : 0), 0);
  // 1) угроза: поблизости враг — башни и госпиталь в приоритете
  if (war) {
    if ((cnt.tower || 0) < 1 + Math.floor(nc / 2)) return 'tower';
    if ((cnt.hospital || 0) < 1 && armySq >= 4) return 'hospital';
  }
  // 2) еда: нехватка или отрицательный поток — ферма первой
  if (p.stock.food < 30 || rateF < 0) return 'farm';
  if (rateF < 0.8 && (cnt.granary || 0) < 1 && p.stock.food < 80) return 'granary';
  // 3) дерево: без леса не построить ничего
  if (p.stock.wood < 50 || rateW < 0.2) return 'sawmill';
  // 4) железо: если есть чем ковать и чем строить — рудник дефицитнее всего
  const hasForge = (cnt.forge || 0) + (cnt.workshop || 0) + (cnt.siege || 0) + (cnt.barracks || 0) > 0;
  if (hasForge && (p.stock.iron < 40 || rateI < 0.2)) return 'mine';
  // 5) золото: минус по казне или пустая казна — рынок
  if (rateG < 0 || p.stock.gold < 60) return 'market';
  // 6) предметы под завязку — склад (иначе ковка упирается в лимит)
  if (itemFull && (cnt.storehouse || 0) < 2 + nc) return 'storehouse';
  // 7) госпиталь по числу армий
  if (armySq >= 8 && (cnt.hospital || 0) < 1 + Math.floor(armySq / 12)) return 'hospital';
  return null;
}
function aiThink(S, p) {
  const ai = p.ai; if (S.time < ai.next) return; ai.next = S.time + DIFF[p.diff].think * (1.15 + S.rnd() * 0.5); // боты развиваются в своём темпе — не слишком быстро
  const mine = S.cities.filter(c => c.owner === p.id); if (!mine.length) return; const nc = mine.length, easy = p.diff === 'easy';
  if (S.time - (ai.failsAt || 0) > 150) { ai.fails = {}; ai.failsAt = S.time; } // границы растут: неудачные места пробуем заново
  const own = S.buildings.filter(b => b.owner === p.id), cnt = {}; for (const b of own) cnt[b.type] = (cnt[b.type] || 0) + 1;
  const atWar = aiWars(S, p.id).length > 0;

  // --- строительство: сперва закрываем дефицит/угрозу, потом плановое развитие
  const want = aiReaction(S, p, mine, cnt, own);
  let built = false;
  if (want && (ai.fails[want] || 0) <= 4) {
    if (canAfford(p, BD[want].cost)) {
      const site = aiFindSite(S, p, want);
      if (site) { doBuild(S, p.id, want, site.x, site.y); built = true; } else ai.fails[want] = (ai.fails[want] || 0) + 1;
    }
  }
  const plan = [['farm', 2], ['sawmill', 2], ['mine', 2], ['barracks', 1], ['hospital', 1], ['workshop', 1], ['forge', 1], ['farm', 3], ['mill', 1], ['stable', 1], ['sawmill', 3], ['market', 1], ['academy', easy ? 0 : 1], ['mine', 3], ['house', 3],
    ['granary', 1], ['barracks', 2], ['siege', easy ? 0 : 1], ['hospital', 1 + Math.floor(nc / 3)], ['forge', 2], ['townhall', easy ? 0 : 1], ['farm', 3 + nc * 2], ['port', 1], ['workshop', 2], ['stable', 2], ['storehouse', 1], ['embassy', easy ? 0 : 1], ['tavern', easy ? 0 : 1], ['sawmill', 3 + nc * 2], ['mine', 3 + nc], ['market', 1 + nc], ['mill', 1 + Math.floor(nc / 2)], ['house', 3 + nc * 2],
    ['tower', atWar ? 2 + Math.floor(nc / 2) : Math.floor(nc / 3)], ['storehouse', 2]];
  if (!built) for (const [type, n] of plan) {
    if ((cnt[type] || 0) >= n) continue;
    if ((ai.fails[type] || 0) > 4) continue;
    if (type === 'port' && easy) continue;
    if (!canAfford(p, BD[type].cost)) break;
    const site = aiFindSite(S, p, type);
    if (!site) { ai.fails[type] = (ai.fails[type] || 0) + 1; continue; }
    doBuild(S, p.id, type, site.x, site.y); break;
  }
  // новый город: выбираем место, где можно занять больше свободной земли (на войне не расширяемся)
  if (nc < 2 + Math.floor(S.time / 300) && nc < 12 && canAfford(p, cityCost(S, p.id)) && p.stock.gold > cityCost(S, p.id).gold + 100 && S.time > 120 && !atWar) {
    let best = null, bs = -1;
    for (let k = 0; k < 70; k++) {
      const c = mine[(S.rnd() * mine.length) | 0], a = S.rnd() * 6.283, r = CITY_MIN + 1 + S.rnd() * 6, x = Math.round(c.x + 1 + Math.cos(a) * r - 1), y = Math.round(c.y + 1 + Math.sin(a) * r - 1);
      if (cityOk(S, p.id, x, y)) continue; const cl = cityClaim(S, p.id, x, y), sc = cl.free * 2 + cl.land - cl.forest * 0.5; if (sc > bs) { bs = sc; best = { x, y }; }
    }
    if (best && bs > 25) doBuild(S, p.id, 'city', best.x, best.y);
  }
  // улучшение городов (на войне копи́м золото на армию, а не на ратушу)
  if (!atWar || p.stock.gold > upgradeCost(mine[0]).gold + 400) for (const c of mine) if (c.level < 2 + (p.diff === 'hard' ? 2 : 1) && p.stock.gold > upgradeCost(c).gold + 200 && canAfford(p, upgradeCost(c))) { upgradeCity(S, c); break; }

  // --- что производить: то, чего на складе не хватает сильнее всего
  const want2 = { forge: { sword: 20, armL: 15, armH: 12 }, workshop: { bow: 15, spear: 12, xbow: 8 } };
  for (const b of own) { const tgt = want2[b.type]; if (!tgt || b.busy) continue; let best = b.recipe, bd = -99; for (const k in tgt) { const d = tgt[k] - p.stock[k]; if (d > bd) { bd = d; best = k; } } b.recipe = best; }

  // --- обучение (число отрядов ограничено: иначе к концу партии их сотни)
  const st = aiSquadStats(S, p.id), capSq = Math.min(5 + 3 * nc, 30), want3 = { archer: 3, light: 2.5, heavy: 2, cav: 2.5, spear: 2, xbow: 1.5 };
  let totSq = st.archer + st.light + st.heavy + st.cav + st.spear + st.militia + st.xbow;
  for (const b of own) if (b.type === 'barracks' && b.queue.length < 2 && totSq < capSq) {
    const ord = Object.keys(want3).sort((a, c) => st[a] / want3[a] - st[c] / want3[c]); let done = false;
    // на войне с кавалерией — копейщики козырь: чуть подкручиваем приоритет копий, если коней много у врага
    let cavThreat = 0; const foes = aiWars(S, p.id); for (const f of foes) for (const a of S.armies) if (a.owner === f) for (const q of a.sq) if (q.t === 'cav') cavThreat += q.u;
    if (cavThreat > 60 && totSq > 0) ord.splice(ord.indexOf('spear'), 1), ord.unshift('spear');
    for (const t of ord) { if (enqueue(S, b, t) === null) { st[t]++; totSq++; done = true; break; } }
    // оружия ещё нет — для обороны сойдёт и ополчение
    if (!done && totSq < 3 && S.time > 90 && enqueue(S, b, 'militia') === null) { st.militia++; totSq++; }
  }
  for (const b of own) if (b.type === 'siege' && b.queue.length < 1) { let nr = 0, nc2 = 0; for (const a of S.armies) if (a.owner === p.id) for (const q of a.sq) { if (q.t === 'ram') nr++; if (q.t === 'cat') nc2++; } if (nr + nc2 < Math.min(2 + nc, 6)) enqueue(S, b, nr <= nc2 ? 'ram' : 'cat'); }
  for (const b of own) if (b.type === 'academy' && !b.queue.length && !hasCommander(S, p.id)) { const cn = { archer: 0, light: 0, heavy: 0, cav: 0 }; for (const a of S.armies) if (a.owner === p.id) for (const q of a.sq) if (cn[q.t] !== undefined) cn[q.t] += q.u; const cls = Object.keys(cn).sort((x, y) => cn[y] - cn[x])[0]; if (cn[cls] >= 10) enqueueCmd(S, b, cls); }
  // богатый бот нанимает наёмников в таверне (на войне — охотнее)
  const rich = atWar ? 450 : 700;
  if (!easy && p.stock.gold > rich && totSq < capSq) for (const b of own) if (b.type === 'tavern' && (b.cd || 0) <= S.time) { const of = tavernOffer(S, b), t = of[1 + ((S.rnd() * 2) | 0)]; if (p.stock.gold > MERC[t] + (atWar ? 250 : 400) && hireMerc(S, b, t) === null) break; }

  // --- армии
  aiArmies(S, p, mine);
  // --- дипломатия
  if (S.tk % 40 === 0) aiDiplomacy(S, p);
  // --- торговые суда
  for (const b of own) if (b.type === 'port' && S.ships.filter(s => s.home === b.id).length < 1 && S.players.some(q => q.id !== p.id && q.alive && rel(S, p.id, q.id).trade)) buildShip(S, b, 'trade');
}
function aiDiplomacy(S, p) {
  const cch = { easy: 0.02, normal: 0.05, hard: 0.1 }[p.diff], pw = milPower(S, p.id), wars = aiWars(S, p.id).length;
  for (const q of S.players) {
    if (q.id === p.id || !q.alive) continue; const r = rel(S, p.id, q.id);
    // союзы и торговля — с кем тепло
    if (r.st === 'peace' && r.trade && !r.ally && r.op > 15 && S.rnd() < 0.15) offer(S, p.id, q.id, 'ally');
    if (r.st === 'peace' && !r.trade && S.rnd() < 0.3 && r.op > -20) offer(S, p.id, q.id, 'trade');
    // просящая рука: бедному боту рынок выгоднее обиды
    if (r.st === 'peace' && !r.trade && p.stock.gold < 150 && S.rnd() < 0.45 && r.op >= -10) offer(S, p.id, q.id, 'trade');
    // перемирие: на чей хвост наступил сильнейший — тому просим мира
    if (r.st === 'war' && S.time - r.warSince > 120 && pw < milPower(S, q.id) * 0.75 && S.rnd() < 0.7) offer(S, p.id, q.id, 'truce');
    // новую войну не начинаем, пока идёт старая (одна-две на сложном, если врагуже и так обескровлен — у «тяжёлого» бота есть право на второй фронт против слабейшего)
    if (wars > 0) {
      if (!(p.diff === 'hard' && wars === 1 && q.human === false)) continue;
    }
    // на слабого соседа — претензия: предпочитаем того, кто и так воюет с кем-то (добивать легче)
    if (r.st === 'peace' && !r.ally && S.time > 150 && !claimInfo(S, p.id, q.id) && S.rnd() < cch) {
      const qp = milPower(S, q.id), engaged = aiWars(S, q.id).some(z => z !== p.id);
      const cap = engaged ? pw * 1.35 : pw * 1.1;
      if (qp < cap && !S.players.some(z => z.id !== p.id && z.alive && rel(S, p.id, z.id).st === 'war' && z.id !== q.id)) startClaim(S, p.id, q.id);
    }
    // сильный бот может потребовать дань у слабого игрока
    if (q.human && p.diff !== 'easy' && r.st === 'peace' && !r.ally && S.time > 3 * DAY && S.rnd() < (p.diff === 'hard' ? 0.1 : 0.05) && pw > milPower(S, q.id) * 1.6 + 20) offer(S, p.id, q.id, 'ult');
  }
}
/* Армии бота. Самые крупные армии — ударные, они идут в поход. Остальные отряды стягиваются к ближайшей
   неполной армии и вливаются в неё. Если какой-то мой город под угрозой — ближайшая ударная армия
   сначала идёт защищать его, война и захват ждут. */
function aiArmies(S, p, mine) {
  const my = S.armies.filter(a => a.owner === p.id && !a.ship); if (!my.length) return;
  const cap = mine[0], hx = cap.x + 1, hy = cap.y + 1;
  my.sort((a, b) => b.sq.length - a.sq.length || units(b) - units(a));
  // кому угрожает война: свои города, рядом с которыми стоит вражеская армия
  const warIds = aiWars(S, p.id), danger = [];
  if (warIds.length) for (const c of mine) {
    let td = 1e9;
    for (const a of S.armies) if (a.owner >= 0 && warIds.includes(a.owner)) { const d = dist(a.x, a.y, c.x + 1, c.y + 1); if (d < td) td = d; }
    if (td < 10) danger.push({ c, d: td });
  }
  danger.sort((a, b) => a.d - b.d);
  const need = { easy: 5, normal: 4, hard: 3 }[p.diff], start = { easy: 520, normal: 400, hard: 280 }[p.diff], maxLead = { easy: 1, normal: 2, hard: (warIds.length ? 2 : 4) }[p.diff];
  const leaders = [], hubs = [], defense = new Map();
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
  // оборонительный план: на каждый город под угрозой — своя ударная армия (или две на hard)
  const assigned = new Set();
  for (const { c } of danger) {
    let used = 0; const wantD = p.diff === 'hard' ? 2 : 1;
    for (const a of leaders) { if (used >= wantD) break; if (assigned.has(a.id)) continue; defense.set(a.id, c); assigned.add(a.id); used++; }
  }
  for (const a of leaders) aiLead(S, p, a, hx, hy, need, start, defense.get(a.id) || null);
}
function aiLead(S, p, main, hx, hy, need, start, defendCity) {
  if (!S.armies.includes(main) || main.st === 'fight') return;
  const u = units(main);
  // умираем — отступаем (оборона не отменяет отступление)
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
  // обороняемся: идём к своему городу под угрозой и стоим на нём
  if (defendCity) {
    const gd = dist(main.x, main.y, defendCity.x + 1, defendCity.y + 1);
    if (gd > 2.5 && main.st === 'idle' && !wait) { main.aiT = S.time + 10; orderMove(S, main, defendCity.x + 1, defendCity.y + 1); }
    return; // держимся у города: враг, если придёт, сам попадётся в бой
  }
  if (u < armyCap(main) * 0.35 && main.sq.length < 3) { if (main.st === 'idle' && !wait) { main.aiT = S.time + 20; retreat(S, main); } return; }
  if (main.sq.length < need || S.time < start) { if (main.st === 'idle' && !wait && dist(main.x, main.y, hx, hy) > 6) { main.aiT = S.time + 10; orderMove(S, main, hx, hy + 1); } return; }
  if (main.st === 'move' && main.path.length) return;
  if (main.sg && main.siege != null && bidx(S, main.siege)) return; // осада идёт — продолжаем
  const comp = S.comp[Math.floor(main.y) * S.W + Math.floor(main.x)], bad = main.aiBad || (main.aiBad = {});
  let best = null, bs = 1e9;
  for (const c of S.cities) {
    if (c.owner === p.id) continue; if (c.owner >= 0 && !atWar(S, p.id, c.owner)) continue;
    if (S.comp[c.y * S.W + c.x] !== comp || bad[c.id] > S.time) continue;
    let def = 0; if (c.owner >= 0) { for (const a of S.armies) if (a.owner === c.owner && dist(a.x, a.y, c.x + 1, c.y + 1) < 5) def += units(a); def += aiTowerDef(S, p.id, c.x, c.y, 4); }
    if (c.owner < 0 && u < garrison(c) * 0.5) continue; // вольный город малой армией не взять
    if (def > u * 1.35) continue; // не лезть на убой
    const score = dist(main.x, main.y, c.x + 1, c.y + 1) * (1 + c.level * 0.2) + (c.owner < 0 ? -10 : 0) + def * 0.12; if (score < bs) { bs = score; best = c; }
  }
  if (!best) {
    const warIds = aiWars(S, p.id);
    if (warIds.length === 0) { // войну начинаем, только когда ни с кем не воюем и кого-то готовим претензией
      for (const q of S.players) { if (q.id === p.id || !q.alive) continue; const ci = claimInfo(S, p.id, q.id); if (ci && ci.ready && rel(S, p.id, q.id).st === 'peace' && S.rnd() < 0.7) { declareWar(S, p.id, q.id); break; } }
      const chance = { easy: 0.05, normal: 0.12, hard: 0.25 }[p.diff];
      if (S.rnd() < chance) {
        const pw = milPower(S, p.id);
        let tg = null, ts = 1e9;
        for (const c of S.cities) if (c.owner >= 0 && c.owner !== p.id && S.comp[c.y * S.W + c.x] === comp) { const r = rel(S, p.id, c.owner); if (r.st === 'truce' || r.ally) continue; const qp = milPower(S, c.owner); if (qp > pw * 1.25) continue; const engaged = aiWars(S, c.owner).some(z => z !== p.id); const sc = qp * (engaged ? 0.7 : 1); if (sc < ts) { ts = sc; tg = c.owner; } }
        if (tg !== null) { const ci = claimInfo(S, p.id, tg); if (ci && ci.ready) declareWar(S, p.id, tg); else if (!ci) startClaim(S, p.id, tg); }
      }
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
