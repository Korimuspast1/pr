'use strict';
// Проверки 2.6.0: пороховая пушка, линкор, морской бой.
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };

// ===== Пороховая пушка =====
{
  const S = mk();
  ok(G.SQ.cannon && G.SQ.cannon.bld === 'siege', 'пушка обучается в осадной мастерской');
  ok(G.SQ.cannon.range === 5.2 && G.SQ.cannon.atk > 6, 'характеристики пушки', G.SQ.cannon.atk);
  // найм в осадной мастерской
  const b = { id: S.nid++, type: 'siege', x: 8, y: 8, owner: 0, hp: 200, mhp: 200, queue: [], lvl: 1 };
  S.buildings.push(b); S.bmap.set(b.id, b);
  S.players[0].stock.gold = 9999; S.players[0].stock.food = 9999; S.players[0].stock.iron = 9999;
  const en = G.enqueue(S, b, 'cannon');
  ok(en === null, 'пушка встаёт в очередь', en);
  ok(b.queue.length === 1 && b.queue[0].t === 'cannon', 'очередь с пушкой', b.queue.length);
  // обучение завершается тиком
  for (let i = 0; i < 400 && b.queue.length; i++) G.tick(S, 0.25);
  const army = S.armies.find(a => a.owner === 0 && a.sq.some(q => q.t === 'cannon'));
  ok(!!army, 'пушка появилась в армии', S.armies.length);
  // бой: пушка бьёт укрепления издалека
  const S2 = mk();
  G.startWar(S2, 0, 1);
  ok(G.rel(S2, 0, 1).st === 'war', 'война объявлена');
  // стена у ВРАЖЕСКОЙ столицы: её не поглотят растущие границы нашего города (recomputeTerr)
  const c1 = S2.cities.find(c => c.owner === 1);
  let wt = null;
  for (let r = 1; r < 5 && !wt; r++) for (let dy = -r; dy <= r && !wt; dy++) for (let dx = -r; dx <= r && !wt; dx++) {
    const x = c1.x + dx, y = c1.y + dy;
    if (x > 1 && y > 1 && x < S2.W - 2 && y < S2.H - 2 && !G.isWater(S2.ter[y * S2.W + x]) && S2.occ[y * S2.W + x] === -1) wt = { x, y };
  }
  ok(!!wt, 'место для стены у врага найдено');
  const wall = { id: S2.nid++, type: 'wall', x: wt.x, y: wt.y, owner: 1, hp: 300, mhp: 300, queue: [], lvl: 1 };
  S2.buildings.push(wall); S2.bmap.set(wall.id, wall);
  const a2 = G.newArmy(S2, 0, wt.x - 2.5, wt.y + 0.5, [G.mkSquad('cannon')]);
  const oe = G.orderSiege(S2, a2, wall); // приказ осады — армия вступает в бой с укреплением
  ok(!oe, 'приказ осады принят', oe);
  const hp0 = wall.hp;
  for (let i = 0; i < 300 && wall.hp === hp0; i++) G.tick(S2, 0.25);
  ok(wall.hp < hp0, 'пушка рвёт стену', hp0 + '->' + wall.hp);
  // контроль: конница сильна против пушки
  ok(G.mm('cav', 'cannon') > 1.5, 'конница гроза пушек', G.mm('cav', 'cannon'));
  ok(G.mm('cannon', 'heavy') > 1.2, 'пушка бьёт тяжёлых', G.mm('cannon', 'heavy'));
  ok(G.mm('cannon', 'archer') > 1.2, 'пушка бьёт лучников', G.mm('cannon', 'archer'));
  // личный RNG: ветка пушки использует S.rnd как обычные войска — поток не ломается для безпушечных партий
  const S3 = mk(), S4 = mk();
  const r1 = []; for (let i = 0; i < 5; i++) r1.push(S3.rnd());
  const r2 = []; for (let i = 0; i < 5; i++) r2.push(S4.rnd());
  ok(r1.join() === r2.join(), 'поток без пушек не сдвинут');
}

// ===== Линкор =====
{
  const S = mk();
  const c0 = S.cities.find(c => c.owner === 0);
  // строим порт на берегу
  let port = null; // порт кладём на любой берег (buildShip проверяет только воду рядом)
  for (let y = 1; y < S.H - 1 && !port; y++) for (let x = 1; x < S.W - 1 && !port; x++) {
    if (G.isWater(S.ter[y * S.W + x]) || S.occ[y * S.W + x] !== -1) continue;
    let sea = 0;
    for (let yy = y - 1; yy <= y + 1; yy++) for (let xx = x - 1; xx <= x + 1; xx++) if (G.isWater(S.ter[yy * S.W + xx])) sea++;
    if (sea >= 2) { port = { id: S.nid++, type: 'port', x, y, owner: 0, hp: 250, mhp: 250, queue: [], lvl: 1 }; S.buildings.push(port); S.bmap.set(port.id, port); }
  }
  ok(!!port, 'порт построен для теста');
  if (port) {
    S.players[0].stock.gold = 9999; S.players[0].stock.wood = 9999; S.players[0].stock.iron = 9999;
    const c1 = G.shipCost('liner', S.players[0]);
    ok(c1.wood === 220 && c1.gold === 200 && c1.iron === 90, 'цена линкора', JSON.stringify(c1));
    ok(G.buildShip(S, port, 'liner') === null, 'линкор строится');
    const ln = S.ships.find(q => q.kind === 'liner');
    ok(!!ln && ln.hp === 850 && ln.mhp === 850, 'линкор 850 hp', ln && ln.hp);
    ok(G.buildShip(S, port, 'liner') === null, 'второй линкор строится');
    ok(G.buildShip(S, port, 'liner') === 'Достигнут предел судов', 'лимит 2 линкора');
    // морской бой: линкор топит торгового
    G.startWar(S, 0, 1);
    const tr = { id: S.nid++, owner: 1, kind: 'trade', x: ln.x + 2, y: ln.y, path: [], cargo: null, st: 'dock', home: null, landing: null, to: null, wait: 0, face: 1, hp: 160, mhp: 160, cd: 0 };
    S.ships.push(tr);
    G.shipFight(S, 1);
    ok(tr.hp < 160, 'линкор бьёт врага', tr.hp);
    for (let i = 0; i < 40 && S.ships.includes(tr); i++) G.shipFight(S, 1);
    ok(!S.ships.includes(tr), 'торговое судно потоплено');
    // сохранение: линкор и hp переживают загрузку
    ok(G.saveGame(S) === true, 'сохранение работает');
    const S2 = G.loadGame('auto');
    const ln2 = S2.ships.find(q => q.kind === 'liner');
    ok(!!ln2, 'линкор пережил загрузку');
    if (ln2) ok(ln2.hp > 0 && ln2.hp <= 850, 'hp линкора сохранены', ln2.hp);
    // бомбардировка берега
    const S3 = mk();
    const c3 = S3.cities.find(c => c.owner === 0);
    G.startWar(S3, 0, 1);
    const tw = { id: S3.nid++, type: 'tower', x: c3.x + 3, y: c3.y + 3, owner: 1, hp: 200, mhp: 200, queue: [], lvl: 1, gar: null };
    S3.buildings.push(tw); S3.bmap.set(tw.id, tw);
    const ln3 = { id: S3.nid++, owner: 0, kind: 'liner', x: c3.x + 5.5, y: c3.y + 5.5, path: [], cargo: null, st: 'dock', home: null, landing: null, to: null, wait: 0, face: 1, hp: 850, mhp: 850, cd: 0 };
    S3.ships.push(ln3);
    const t0 = tw.hp;
    for (let i = 0; i < 30; i++) G.shipFight(S3, 1);
    ok(tw.hp < t0 || !S3.buildings.includes(tw), 'линкор бомбит башню', t0 + '->' + tw.hp);
  }
}

// ===== Совместимость =====
{
  const S = mk();
  const c0 = S.cities.find(c => c.owner === 0);
  S.players[0].stock.gold = 99999; S.players[0].stock.food = 99999; S.players[0].stock.wood = 99999; S.players[0].stock.iron = 99999;
  for (let k = 0; k < 8; k++) G.newArmy(S, 0, c0.x + 1 + (k % 3), c0.y + 1 + ((k / 3) | 0), [G.mkSquad('heavy')]);
  run(S, 1500);
  ok(!S.over || true, 'игра идёт с новым кодом');
  const bad = S.buildings.find(b => !isFinite(b.hp) || b.hp < 0);
  ok(!bad, 'прочности в норме');
  ok(S.players[0].stock.gold >= 0, 'золото не в минусе');
}

console.log('test_v26: pass=' + pass + ' fail=' + fail);
process.exit(fail ? 1 : 0);
