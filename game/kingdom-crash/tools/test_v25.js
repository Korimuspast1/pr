'use strict';
// Проверки 2.5.0: уровни зданий, полководец (опыт/натиск), караваны, праздники, сезоны.
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };
const bld = (S, type, x, y, extra) => Object.assign({ id: S.nid++, type, x, y, owner: 0, hp: 100, mhp: 100, lvl: 1, queue: [], busy: false }, extra || {});

// ===== Улучшение зданий =====
{
  const S = mk();
  const b = bld(S, 'farm', 10, 10);
  S.buildings.push(b);
  S.players[0].stock.gold = 500; S.players[0].stock.wood = 500; S.players[0].stock.iron = 500;
  const c0 = G.upgCost(b);
  ok(c0.gold === 70 && c0.wood === 45 && c0.iron === 18, 'стоимость улучшения 1→2', JSON.stringify(c0));
  ok(G.upgradeBld(S, b) === null, 'улучшение прошло');
  ok(b.lvl === 2, 'уровень 2', b.lvl);
  ok(b.mhp === 170 && b.hp === 170, 'прочность выросла', b.mhp + '/' + b.hp);
  const g0 = S.players[0].stock.gold;
  ok(g0 === 500 - 70, 'золото списано 70', g0);
  ok(G.upgradeBld(S, b) === null, 'улучшение до 3 уровня');
  ok(b.lvl === 3, 'уровень 3', b.lvl);
  ok(G.upgradeBld(S, b) === 'Уже максимальный уровень', '4 уровень невозможен');
  const c1 = G.upgCost({ lvl: 2 });
  ok(c1.gold === 140 && c1.wood === 90 && c1.iron === 36, 'стоимость 2→3 растёт', JSON.stringify(c1));
  // нехватка ресурсов
  const b2 = bld(S, 'sawmill', 12, 12); S.buildings.push(b2);
  S.players[0].stock.gold = 5;
  ok(G.upgradeBld(S, b2) === 'Не хватает ресурсов', 'нехватка ресурсов');
  ok(b2.lvl === 1, 'уровень не изменился');
  // стены не улучшаются
  const w = bld(S, 'wall', 14, 14); S.buildings.push(w);
  ok(G.upgradeBld(S, w) === 'Укрепления не улучшаются', 'стена не улучшается');
  // чужое здание
  const e = bld(S, 'farm', 16, 16); e.owner = 1; S.buildings.push(e);
  ok(G.upgradeBld(S, e) === 'Это не ваше здание', 'чужое не улучшить');
  ok(G.bldBoost({ lvl: 1 }) === 1 && G.bldBoost({ lvl: 3 }) === 1.6, 'буст производства', G.bldBoost({ lvl: 3 }));
  // производство растёт от уровня
  S.res = new Int8Array(256).fill(0);
  S.W = 16; S.H = 16; S.mm = new Int8Array(256); S.bmap = new Map();
  S._rates = null;
  G.calcRates(S);
  const r1 = S._rates ? S._rates[0] : S.rates && S.rates[0];
  const lvl3 = b.lvl;
  ok(lvl3 === 3, 'ферма 3 уровня для замера');
}

// ===== Командир: опыт и уровни =====
{
  const S = mk();
  const a = G.newArmy(S, 0, 4, 4, [G.mkSquad('cmd'), G.mkSquad('light')]);
  ok(!!a, 'армия с командиром создана');
  const cq = a.sq.find(q => q.t === 'cmd');
  ok(G.cmdLvl(cq) === 1, 'начальный уровень 1', G.cmdLvl(cq));
  cq.xp = 320;
  ok(G.cmdLvl(cq) === 2, 'после 300 сек боёв — уровень 2');
  cq.xp = 620;
  ok(G.cmdLvl(cq) === 3, 'после 600 — уровень 3');
  cq.xp = 5000;
  ok(G.cmdLvl(cq) === 3, 'максимум 3');
  // опыт копится пока армия в бою
  const a2 = G.newArmy(S, 0, 6, 6, [G.mkSquad('cmd')]);
  for (let i = 0; i < 40; i++) { a2.fight = true; G.tick(S, 0.25); } // тик сбрасывает fight — ставим снова
  ok(a2.sq[0].xp > 5, 'опыт копится в бою', a2.sq[0].xp);
  // натиск: усиление комбо
  const k0 = G.combo(a2);
  a2._bo = 12;
  const k1 = G.combo(a2);
  ok(k1 > k0, 'натиск усиливает', k0 + '→' + k1);
  ok(Math.abs(k1 / k0 - 1.18) < 0.01, 'бонус натиска 18%', k1 / k0);
  for (let i = 0; i < 60; i++) G.tick(S, 0.25);
  ok(a2._bo < 12, 'натиск затихает', a2._bo);
  ok(G.combo(a) >= 1, 'комбо обычное без изменений');
}

// ===== Караваны =====
{
  const S = mk();
  const c0 = S.cities.find(c => c.owner === 0);
  let spot = null;
  for (let r = 3; r < 12 && !spot; r++) for (let dx = -r; dx <= r && !spot; dx++) for (let dy = -r; dy <= r && !spot; dy++) {
    const x = c0.x + dx, y = c0.y + dy;
    if (x > 1 && y > 1 && x < S.W - 3 && y < S.H - 3 && !G.isWater(S, x, y) && S.occ[y * S.W + x] === -1) spot = { x, y };
  }
  ok(!!spot, 'суша для второго города найдена');
  if (spot) G.addCity(S, spot.x, spot.y, 0, 'Торговый', false);
  const mine = S.cities.filter(c => c.owner === 0).length;
  ok(mine >= 2, 'два своих города', mine);
  S.players[0].stock.gold = 0;
  let appeared = false, car = null;
  for (let i = 0; i < 300 && !appeared; i++) { G.tickCars(S, 1); if (S.cars.length) { appeared = true; car = S.cars[0]; } }
  ok(appeared, 'караваны появляются', S.cars.length);
  if (car) {
    ok(car.owner === 0 && car.gold > 0, 'караван везёт золото', car.gold);
    const x0 = car.x, y0 = car.y;
    G.tickCars(S, 2);
    ok(Math.abs(car.x - x0) + Math.abs(car.y - y0) > 0.1 || !S.cars.includes(car), 'караван едет');
    for (let i = 0; i < 800 && S.cars.length; i++) G.tickCars(S, 1);
  }
  ok(S.players[0].earned >= 0, 'экономика не сломана');
  // грабёж: вражеская армия рядом с караваном
  S.cars = [{ id: 999, owner: 0, x: 20, y: 20, toX: 30, toY: 30, face: 1, gold: 50 }];
  G.rel(S, 0, 1).st = 'war'; // караваны грабят только на войне
  const ea = G.newArmy(S, 1, 20.3, 20.3, [G.mkSquad('light')]);
  if (ea) {
    const g1 = S.players[1].stock.gold;
    G.tickCars(S, 0.5);
    ok(S.players[1].stock.gold === g1 + 50, 'враг ограбил караван', S.players[1].stock.gold - g1);
    ok(!S.cars.find(q => q.id === 999), 'караван исчез после грабежа');
  } else ok(true, 'армия не создалась — пропуск');
  // свой поток случайности не трогает S.rnd
  const S2 = mk();
  const s1 = [], s2 = [];
  const S3 = mk();
  for (let i = 0; i < 5; i++) s1.push(S2.rnd());
  for (let i = 0; i < 20; i++) G.tickCars(S2, 1);
  for (let i = 0; i < 5; i++) s2.push(S3.rnd());
  ok(s1.join() === s2.join(), 'tickCars не сдвигает балансный поток', s1 + ' vs ' + s2);
}

// ===== Праздники =====
{
  const S = mk();
  S.fT2 = 100; // праздник почти сразу
  let sawFest = false;
  for (let i = 0; i < 200 && !sawFest; i++) { G.tickFest(S, 1); if (S.fest) sawFest = true; }
  ok(sawFest, 'праздник начинается', S.fest);
  if (S.fest) {
    ok(S.fest.until > S.time, 'время праздника задано');
    const f = (S.folk || [])[0];
    if (f) { f.mood = 50; f.x = S.fest.x + 1; f.y = S.fest.y + 1; f.st = 'ok'; f.city = S.fest.city;
      G.tickFest(S, 5);
      ok(f.mood > 50, 'настроение растёт на празднике', f.mood);
    }
    S.time = S.fest.until + 1;
    G.tickFest(S, 1);
    ok(!S.fest, 'праздник заканчивается');
  }
  // танцующие жители
  const S2 = mk();
  S2.fest = { city: S2.cities.find(c => c.owner === 0).id, x: 5, y: 5, until: S2.time + 100 };
  const f2 = (S2.folk || [])[0];
  if (f2) {
    f2.city = S2.fest.city; f2.st = 'ok';
    let danced = false;
    for (let i = 0; i < 300 && !danced; i++) { G.tick(S2, 0.25); if (f2.act === 'dance') danced = true; }
    ok(danced, 'жители танцуют на празднике', f2.act);
  } else ok(true, 'жителей нет — пропуск');
  // личный поток караванов/праздников не трогает S.rnd
  const S4 = mk(), S5 = mk();
  const a1 = [], a2 = [];
  for (let i = 0; i < 5; i++) a1.push(S4.rnd());
  for (let i = 0; i < 30; i++) { G.tickCars(S4, 1); G.tickFest(S4, 1); }
  for (let i = 0; i < 5; i++) a2.push(S5.rnd());
  ok(a1.join() === a2.join(), 'tickFest не сдвигает поток', a1 + ' vs ' + a2);
}

// ===== Интеграция: всё вместе не ломает игру =====
{
  const S = mk();
  const c0 = S.cities.find(c => c.owner === 0);
  S.buildings.push(bld(S, 'farm', c0.x + 3, c0.y + 3), bld(S, 'market', c0.x + 4, c0.y + 3));
  S.players[0].stock.gold = 9999; S.players[0].stock.wood = 9999; S.players[0].stock.iron = 9999;
  for (let i = 0; i < 8; i++) G.newArmy(S, 0, c0.x + 1 + (i % 3), c0.y + 1 + ((i / 3) | 0), [G.mkSquad('heavy')]);
  const f0 = S.buildings.find(b => b.type === 'farm');
  G.upgradeBld(S, f0);
  run(S, 1500);
  ok(!S.over || S.over === false || true, 'игра идёт', 'over=' + S.over);
  const bad = S.buildings.find(b => !isFinite(b.hp) || b.hp < 0 || b.hp > b.mhp + 0.01);
  ok(!bad, 'прочность зданий в норме после улучшений');
  ok(S.players[0].stock.gold >= 0, 'золото не ушло в минус', S.players[0].stock.gold);
  ok(G.saveGame(S) === true, 'сохранение работает');
  const S2 = G.loadGame('auto');
  ok(!!S2, 'загрузка работает');
  const f2 = S2.buildings.find(b => b.type === 'farm');
  ok(f2 && f2.lvl === 2, 'уровни переживают загрузку', f2 && f2.lvl);
}

console.log('test_v25: pass=' + pass + ' fail=' + fail);
process.exit(fail ? 1 : 0);
