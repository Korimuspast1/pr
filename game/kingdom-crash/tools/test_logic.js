'use strict';
// Проверки исправленных ошибок и новых правил (без браузера).
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 4242, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };
const myCity = S => S.cities.find(c => c.owner === 0);
const landNear = (S, c, r) => G.freeTileNear(S, c.x, c.y + r, 4, null);
const army = (S, owner, x, y, types) => G.newArmy(S, owner, x + 0.5, y + 0.5, types.map(t => G.mkSquad(t)));

// --- создание мира
{
  for (const type of ['continent', 'continents', 'islands', 'lakes']) for (const size of ['s', 'm', 'l']) for (const bots of [1, 3, 6]) for (let k = 0; k < 3; k++) {
    const S = G.newGame({ type, size, bots, diff: 'normal', win: 'score', target: 30000, nation: k, seed: 100 + k * 31 + bots });
    const caps = S.cities.filter(c => c.isCap); let md = 1e9;
    for (let i = 0; i < caps.length; i++) for (let j = i + 1; j < caps.length; j++) md = Math.min(md, G.dist(caps[i].x, caps[i].y, caps[j].x, caps[j].y));
    ok(caps.length === bots + 1 && md >= 10.5, 'столицы далеко друг от друга', `${type}/${size}/b${bots} min=${md.toFixed(1)}`);
    let cd = 1e9; for (let i = 0; i < S.cities.length; i++) for (let j = i + 1; j < S.cities.length; j++) cd = Math.min(cd, G.dist(S.cities[i].x, S.cities[i].y, S.cities[j].x, S.cities[j].y));
    ok(cd >= G.CITY_MIN, 'города не вплотную', `${type}/${size}/b${bots} min=${cd.toFixed(1)}`);
    ok(new Set(S.players.map(p => p.name)).size === S.players.length, 'имена держав не повторяются');
    ok(S.players[0].ci === k && S.players[0].perk === G.PERKS[k].id, 'игроку досталась выбранная держава');
    for (const c of S.cities) for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const t = S.ter[(c.y + dy) * S.W + c.x + dx]; ok(!G.isWater(t) && t !== G.TR.MOUNT && !S.res[(c.y + dy) * S.W + c.x + dx], 'город стоит на ровной земле без руды'); }
  }
  const S0 = G.newGame({ type: 'bogus', size: 'zz', bots: 99, diff: 'x', win: '?', target: -5, nation: 77, free: 9 });
  ok(S0.opts.type === 'continent' && S0.opts.size === 'm' && S0.players.length === 7 && S0.opts.nation === 7 && S0.opts.target >= 1000, 'мусорные настройки приводятся к допустимым');
  ok(G.newGame({ sandbox: 1, bots: 0, size: 's' }).players.length === 1, 'песочница без соперников');
  ok(G.newGame({ free: 0, size: 's', bots: 2, seed: 5 }).cities.every(c => c.owner >= 0), 'без вольных городов');
  ok(mk().cities.some(c => c.owner < 0), 'вольные города на карте есть');
}
// --- отступление и приказы
{
  const S = mk(), c = myCity(S), p = landNear(S, c, 3), a = army(S, 0, p.x, p.y, ['archer', 'light']);
  ok(G.doBuild(S, 0, 'hospital', ...(() => { for (let r = 2; r < 6; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) if (!G.siteOk(S, 0, 'hospital', c.x + dx, c.y + dy)) return [c.x + dx, c.y + dy]; return [0, 0]; })()) === null, 'госпиталь построен');
  const far = G.reachTiles(S, 0, p.x, p.y, 40, 6).pop();
  ok(G.orderMove(S, a, far.x + 0.5, far.y + 0.5) === null && a.path.length > 0, 'армия пошла');
  ok(G.retreat(S, a) === null && a.retreat === true, 'отступление начато');
  G.stopArmy(a); ok(a.retreat === false, '«Стоять» снимает отступление (раньше армия переставала отвечать на удары)');
  G.retreat(S, a); G.orderMove(S, a, far.x + 0.5, far.y + 0.5); ok(a.retreat === false, 'новый приказ снимает отступление');
  run(S, 400); G.retreat(S, a); run(S, 400);
  const e = G.retreat(S, a); run(S, 8);
  ok(!a.retreat, 'повторное отступление на месте не «залипает»', JSON.stringify({ e, st: a.st, r: a.retreat, path: a.path.length }));
  ok(G.orderMove(S, a, -5, 3) !== null && G.orderMove(S, a, 3, S.H + 2) !== null && G.orderMove(S, a, NaN, 1) !== null, 'приказ за край карты отклоняется');
}
// --- разделение / объединение
{
  const S = mk(), c = myCity(S), p = landNear(S, c, 3), a = army(S, 0, p.x, p.y, ['archer', 'light', 'heavy', 'cav', 'spear', 'militia']);
  const before = S.armies.length; ok(G.splitArmy(S, a) === 5 && S.armies.length === before + 5, 'армия разделена на отряды');
  ok(S.armies.every(q => !G.isWater(S.ter[Math.floor(q.y) * S.W + Math.floor(q.x)]) && S.ter[Math.floor(q.y) * S.W + Math.floor(q.x)] !== G.TR.MOUNT), 'после разделения никто не оказался в воде или в горах');
  const lone = G.newArmy(S, 0, p.x + 0.5, p.y + 0.5, [(() => { const q = G.mkSquad('cmd'); q.cls = 'light'; return q; })()]);
  ok(G.combo(lone) === 1, 'одинокий полководец без штрафа', G.combo(lone));
  const sh = { id: S.nid++, owner: 0, kind: 'war', x: 1.5, y: 1.5, path: [], cargo: null, st: 'dock' }; S.ships.push(sh);
  const b = army(S, 0, p.x, p.y, ['archer', 'light']); b.ship = sh.id; sh.cargo = b.id;
  ok(G.splitArmy(S, b) === 0 && G.mergeNear(S, b) === 0, 'на корабле армию нельзя делить и объединять');
}
// --- дипломатия
{
  const S = mk({ bots: 2 }); S.players[1].stock.gold = 5000; army(S, 0, myCity(S).x, myCity(S).y + 3, ['heavy', 'heavy', 'heavy', 'heavy']);
  ok(G.offer(S, 0, 1, 'ult') === null, 'ультиматум отправлен'); ok(typeof G.offer(S, 0, 1, 'ult') === 'string', 'второй такой же, пока нет ответа, не уходит');
  const g0 = S.players[0].stock.gold; run(S, 60); const got = S.players[0].stock.gold - g0;
  ok(typeof G.offer(S, 0, 1, 'ult') === 'string', 'повторный ультиматум ограничен по времени (раньше — бесконечное золото)');
  for (let i = 0; i < 20; i++) G.offer(S, 0, 1, 'ult'); run(S, 60);
  ok(S.players[0].stock.gold - g0 < 400, 'золото ультиматумами не «накликать»', S.players[0].stock.gold - g0);
  const S2 = mk({ bots: 2 }); for (let i = 0; i < 10; i++) G.offer(S2, 0, 1, 'pass'); ok(S2.pending.length === 1, 'одинаковые предложения не копятся в очереди');
  // торговля с павшей державой
  const S3 = mk({ bots: 2 }); G.rel(S3, 0, 1).trade = true; G.calcRates(S3); const r1 = S3.players[0].rate.gold;
  for (const c of S3.cities) if (c.owner === 1) c.owner = 2; G.checkVictory(S3); G.calcRates(S3);
  ok(!S3.players[1].alive && !G.rel(S3, 0, 1).trade && S3.players[0].rate.gold < r1, 'торговля с павшей державой прекращается');
  ok(typeof G.offer(S3, 0, 1, 'trade') === 'string' && typeof G.startClaim(S3, 0, 1) === 'string', 'павшей державе нельзя ничего предложить');
  // устаревшее письмо
  const S4 = mk({ bots: 2 }); G.offer(S4, 1, 0, 'trade'); const m = S4.msgs.find(q => q.kind === 'trade'); ok(!!m, 'письмо с предложением пришло');
  ok(typeof G.offer(S4, 1, 0, 'trade') === 'string' && S4.msgs.filter(q => q.kind === 'trade').length === 1, 'бот не засыпает одинаковыми письмами');
  G.startWar(S4, 1, 0); ok(m.done === true, 'после объявления войны старое предложение теряет силу');
  G.answerMsg(S4, m.id, true); ok(!G.rel(S4, 0, 1).trade && G.rel(S4, 0, 1).st === 'war', 'торговлю с врагом через старое письмо не заключить');
  const S5 = mk({ bots: 2 }); G.offer(S5, 1, 0, 'ally'); const m5 = S5.msgs.find(q => q.kind === 'ally'); G.rel(S5, 0, 1).st = 'war'; const e5 = G.answerMsg(S5, m5.id, true);
  ok(typeof e5 === 'string' && !G.rel(S5, 0, 1).ally, 'союз с воюющей стороной не заключить');
  const S6 = mk({ bots: 2 }); G.offer(S6, 1, 0, 'ult'); run(S6, 4 * 121); ok(G.rel(S6, 0, 1).st === 'war', 'ультиматум без ответа — война');
}
// --- здания и производство
{
  const S = mk(), p = S.players[0], c = myCity(S); Object.assign(p.stock, { gold: 9e4, wood: 9e4, food: 9e4, iron: 9e4 });
  const site = t => { for (let r = 2; r < 7; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) if (!G.siteOk(S, 0, t, c.x + dx, c.y + dy)) return [c.x + dx, c.y + dy]; return null; };
  const build = t => { const s = site(t); return s && G.doBuild(S, 0, t, s[0], s[1]) === null ? S.buildings[S.buildings.length - 1] : null; };
  const f = build('forge'); ok(!!f, 'кузня'); run(S, 2); ok(f.busy, 'кузня работает');
  const iron0 = p.stock.iron; G.setRecipe(S, f, 'armH'); ok(p.stock.iron > iron0 && !f.busy, 'при смене изделия сырьё возвращается');
  p.stock.armH = 10; run(S, 8); ok(!f.busy && f.stop === 10 && /Достаточно/.test(f.status), 'при полном складе производство стоит, а не жжёт ресурсы', f.status);
  f.stop = null; p.stock.armH = G.ITEM_CAP; run(S, 8); ok(!f.busy && /Достаточно|полон/.test(f.status), 'без предела склад ограничен общим капом', f.status);
  f.off = true; p.stock.armH = 0; run(S, 8); ok(!f.busy, 'производство можно остановить');
  const bar = build('barracks'); ok(G.enqueue(S, bar, 'militia') === null && G.enqueue(S, bar, 'cmd') !== null && G.enqueue(S, bar, 'nope') !== null && G.enqueue(S, null, 'archer') !== null, 'очередь казарм проверяет вид отряда');
  const n0 = S.armies.length; run(S, 60); ok(S.armies.length === n0 + 1 && S.armies[S.armies.length - 1].sq[0].t === 'militia', 'ополчение обучено');
  const farm = build('farm'), r0 = G.farmRate(S, farm, p); let mill = null;
  for (let dy = -1; dy <= 1 && !mill; dy++) for (let dx = -1; dx <= 1; dx++) if ((dx || dy) && !G.siteOk(S, 0, 'mill', farm.x + dx, farm.y + dy)) { G.doBuild(S, 0, 'mill', farm.x + dx, farm.y + dy); mill = 1; break; }
  ok(mill && Math.abs(G.farmRate(S, farm, p) / r0 - 1.5) < 1e-9, 'мельница даёт соседней ферме +50%');
  const tv = build('tavern'), of = G.tavernOffer(S, tv); ok(of.length === 3 && of[0] === 'militia', 'в таверне три вида наёмников');
  const n1 = S.armies.length; ok(G.hireMerc(S, tv, of[1]) === null && S.armies.length === n1 + 1, 'наёмники наняты'); ok(typeof G.hireMerc(S, tv, of[1]) === 'string', 'между наймами пауза');
  ok(typeof G.hireMerc(S, bar, 'archer') === 'string' && typeof G.hireMerc(S, tv, 'cmd') === 'string', 'нанять можно только в таверне и только предложенных');
  // казармы на краю карты: отряд не должен появиться вне карты
  const S2 = mk(), far = { id: S2.nid++, type: 'barracks', x: 5, y: S2.H - 1, owner: 0, queue: [], recipe: null }; const rp = G.rallyPos(S2, far);
  ok(rp.x >= 0 && rp.y >= 0 && rp.x < S2.W && rp.y < S2.H, 'место выхода отряда всегда на карте', JSON.stringify(rp));
  ok(G.siteOk(S, 0, 'farm', -1, 0) && G.siteOk(S, 0, 'farm', 0, S.H) && G.siteOk(S, 0, 'nope', 3, 3), 'стройка за краем карты и неизвестного здания отклоняется');
}
// --- вольные города
{
  const S = mk(), fc = S.cities.find(c => c.owner < 0); ok(G.garrison(fc) >= 60, 'у вольного города крепкий гарнизон');
  const p = landNear(S, fc, 1), a = army(S, 0, p.x, p.y, ['heavy', 'heavy', 'heavy', 'heavy', 'light', 'light']); a.x = fc.x + 1; a.y = fc.y + 1.5;
  const u0 = G.units(a); run(S, 4 * 120);
  ok(fc.owner === 0 && S.players[0].flags.freecap === 1, 'вольный город захвачен без объявления войны'); ok(G.units(a) < u0, 'при штурме вольного города есть потери', G.units(a) + '/' + u0);
}
// --- победа и поражение
{
  const S = mk({ bots: 2 }); for (const c of S.cities) if (c.owner === 0) c.owner = 1; S.players[1].score = 10; S.players[2].score = 99; G.checkVictory(S);
  ok(S.over && S.over.winner === 2, 'при поражении победителем называется сильнейший, а не первый по списку', JSON.stringify(S.over));
  const S2 = mk({ sandbox: 1, bots: 1 }); for (const c of S2.cities) if (c.owner === 0) c.owner = 1; G.checkVictory(S2); ok(!!S2.over, 'песочница завершается, если городов не осталось (раньше игра зависала)');
  const S3 = mk({ bots: 2 }); G.rel(S3, 0, 1).ally = true; for (const c of S3.cities) if (c.owner === 1) c.owner = 2; G.checkVictory(S3); ok(!G.rel(S3, 0, 1).ally && !S3.armies.some(a => a.owner === 1), 'у павшей державы нет ни союзов, ни армий');
}
// --- сохранение
{
  const S = mk({ bots: 3 }); S.players.forEach(p => p.human = false); run(S, 3000); S.players[0].human = true;
  ok(G.saveGame(S) && G.hasSave(), 'сохранение записано'); const raw = G._ls.getItem('kc_save'); const L = G.loadGame();
  ok(!!L, 'сохранение читается', G.loadGame.error);
  if (L) {
    ok(L.cities.length === S.cities.length && L.buildings.length === S.buildings.length && L.armies.length === S.armies.length && L.ships.length === S.ships.length, 'после загрузки тот же мир');
    ok(L.players.every((p, i) => p.name === S.players[i].name && Math.abs(p.stock.gold - S.players[i].stock.gold) < 1e-6), 'казна и державы совпадают');
    ok(L.armies.every((a, i) => G.units(a) === G.units(S.armies[i])), 'численность армий совпадает');
    let crashed = null; try { run(L, 800); } catch (e) { crashed = e.stack; } ok(!crashed, 'загруженная партия идёт дальше', crashed);
  }
  const bad = (name, mut) => { const d = JSON.parse(raw); mut(d); G._ls.setItem('kc_save', JSON.stringify(d)); let L2 = null, err = null; try { L2 = G.loadGame(); if (L2) run(L2, 200); } catch (e) { err = e.stack; } ok(!err, 'порча сохранения не роняет игру: ' + name, err); return L2; };
  ok(bad('чужая версия', d => { d.v = 7; }) === null, 'неизвестная версия отклонена');
  ok(bad('обрезанная карта', d => { d.ter = d.ter.slice(0, 100); }) === null, 'битая карта отклонена');
  ok(bad('нет игроков', d => { d.players = []; }) === null, 'сохранение без игроков отклонено');
  ok(bad('армии не массив', d => { d.armies = 'x'; }) === null, 'битый список армий отклонён');
  ok(bad('повтор идентификаторов', d => { d.buildings[1].id = d.buildings[0].id; }) === null, 'повтор идентификаторов отклонён');
  ok(bad('здание вне карты', d => { d.buildings[0].x = 9999; }) === null, 'здание вне карты отклонено');
  const x = bad('разметка в названиях', d => { d.cities[0].name = '<img src=x onerror=alert(1)>'; d.players[1].name = '<b>X</b>'; d.players[1].col = '"><script>'; d.msgs.push({ id: 987654, t: 1, type: 'info', title: '<script>alert(1)</script>', text: '<i>x</i>' }); });
  ok(x && !/[<>"]/.test(x.cities[0].name) && x.players[1].name === S.players[1].name && /^#[0-9a-f]{6}$/.test(x.players[1].col) && x.msgs.every(m => !/[<>]/.test(m.title + m.text)), 'разметка из сохранения не попадает в интерфейс');
  const y = bad('мусор в числах', d => { d.players[0].stock.gold = 'NaN'; d.armies.forEach(a => { a.x = null; }); d.time = -5; d.cities[0].level = 99; d.cities[0].radius = 1e9; });
  ok(y === null || (isFinite(y.players[0].stock.gold) && y.players[0].stock.gold >= 0 && y.cities[0].level <= 5 && y.cities[0].radius <= 30 && y.armies.every(a => isFinite(a.x) && isFinite(a.y))), 'мусорные числа отброшены или ограничены');
  const z = bad('полководец без рода войск', d => { d.armies[0].sq[0].t = 'cmd'; delete d.armies[0].sq[0].cls; d.armies[0].sq[0].s = [{ hp: 1e9 }]; });
  ok(z && z.armies[0].sq[0].cls && z.armies[0].sq[0].s[0].hp <= G.SQ.cmd.hp, 'полководец из сохранения исправлен');
  for (let k = 0; k < 300; k++) { // случайная порча
    const d = JSON.parse(raw), keys = ['players', 'cities', 'buildings', 'armies', 'ships', 'msgs', 'rels', 'pending'], arr = d[keys[k % keys.length]];
    if (Array.isArray(arr) && arr.length) { const o = arr[(k * 7) % arr.length], ks = Object.keys(o || {}); if (ks.length) { const kk = ks[(k * 13) % ks.length]; o[kk] = [null, -1, 1e12, 'x', {}, [], NaN, true, '<b>'][k % 9]; } }
    G._ls.setItem('kc_save', JSON.stringify(d)); let err = null; try { const L3 = G.loadGame(); if (L3) run(L3, 120); } catch (e) { err = e.stack; }
    ok(!err, 'случайная порча #' + k, err);
  }
  G._ls.setItem('kc_save', '{{{'); ok(G.loadGame() === null, 'не-JSON отклонён'); G.dropSave(); ok(!G.hasSave(), 'сохранение удалено');
}
// --- старое сохранение (версия 1) открывается
{
  // fixtures/save_v1.json — партия, сохранённая игрой версии 1 (малый материк, два соперника, 2500 тактов)
  G._ls.setItem('kc_save', require('fs').readFileSync(require('path').join(__dirname, 'fixtures', 'save_v1.json'), 'utf8')); const L = G.loadGame(); ok(!!L, 'сохранение прежней версии открывается', G.loadGame.error);
  if (L) { let e = null; try { run(L, 1500); } catch (x) { e = x.stack; } ok(!e, 'и играется дальше', e); ok(L.players[0].stock.spear === 0, 'новые ресурсы добавлены'); }
}
console.log(`\nпройдено: ${pass}, провалено: ${fail}`); process.exit(fail ? 1 : 0);
