'use strict';
// Проверки v2.2.0: три кузницы с пределом склада, тюрьма и её обязательность,
// новые действия жителей, союзникам нельзя воевать, пилорама вырубает лес.
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };

// --- кузницы: предел склада, независимые рецепты, три штуки
{
  const S = mk();
  const p = S.players[0], c = S.cities.find(q => q.owner === 0);
  p.stock.gold = 5000; p.stock.wood = 5000; p.stock.iron = 5000;
  const tiles = [];
  for (let dy = -5; dy <= 5 && tiles.length < 3; dy++) for (let dx = -5; dx <= 5 && tiles.length < 3; dx++) {
    const x = c.x + dx, y = c.y + dy;
    if (G.siteOk(S, 0, 'forge', x, y) === null) tiles.push([x, y]);
  }
  ok(tiles.length === 3, 'нашлось три места под кузницы');
  for (const t of tiles) G.doBuild(S, 0, 'forge', t[0], t[1]);
  const fs = S.buildings.filter(b => b.type === 'forge' && b.owner === 0);
  ok(fs.length === 3, 'три кузницы построены', fs.length);
  ok(fs[0].stop === 10, 'предел склада по умолчанию 10');
  ok(G.setRecipe(S, fs[0], 'armL') === null, 'рецепт меняется');
  ok(G.setRecipe(S, fs[1], 'armH') === null, 'у второй кузницы свой рецепт');
  ok(fs[2].recipe === 'sword', 'у третьей остался меч');
  run(S, 500); // 125 секунд — все три наберут по 10 изделий
  ok((p.stock.sword | 0) === 10 && (p.stock.armL | 0) === 10 && (p.stock.armH | 0) === 10,
    'каждая кузница ровно по своему пределу', JSON.stringify([p.stock.sword, p.stock.armL, p.stock.armH]));
  ok(fs.every(b => !b.busy && /^Достаточно/.test(b.status)), 'кузницы остановились по пределу', fs.map(b => b.status).join('|'));
  const ironLeft = p.stock.iron;
  run(S, 200);
  ok(p.stock.iron >= ironLeft, 'железо больше не исчезает бесконтрольно', ironLeft + ' -> ' + p.stock.iron);
  fs[0].stop = 5; // ручная смена предела
  ok(fs[0].stop === 5, 'предел можно менять');
}

// --- тюрьма: без неё не сажать, 5 мест на тюрьму
{
  const S = mk();
  const c = S.cities.find(q => q.owner === 0), p = S.players[0];
  p.stock.gold = 9999; p.stock.wood = 9999;
  S.folk = [];
  const folk = [];
  for (let i = 0; i < 7; i++) { const f = G.makeFolk(S, c); S.folk.push(f); folk.push(f); }
  ok(G.folkPrison(S, folk[0].id, true) !== null, 'без тюрьмы посадить нельзя');
  let built = false;
  for (let dy = -6; dy <= 6 && !built; dy++) for (let dx = -6; dx <= 6 && !built; dx++) {
    const x = c.x + dx, y = c.y + dy;
    if (G.siteOk(S, 0, 'prison', x, y) === null) built = G.doBuild(S, 0, 'prison', x, y) === null;
  }
  ok(built, 'тюрьма построена');
  ok(G.folkPrison(S, folk[0].id, true) === null, 'с тюрьмой можно сажать');
  ok(G.folkPrison(S, folk[0].id, true) !== null, 'дважды сажать нельзя');
  for (let i = 1; i < 5; i++) ok(G.folkPrison(S, folk[i].id, true) === null, 'место ' + i + ' занято');
  ok(G.folkPrison(S, folk[5].id, true) !== null, 'шестого держать негде');
  ok(G.folkPrison(S, folk[0].id, false) === null, 'освободить можно');
  ok(G.folkPrison(S, folk[5].id, true) === null, 'освободившееся место занимается');
}

// --- новые действия жителей: подарок, пир, призыв
{
  const S = mk();
  const c = S.cities.find(q => q.owner === 0), p = S.players[0];
  p.stock.gold = 500;
  S.folk = [];
  const f = G.makeFolk(S, c); S.folk.push(f);
  const w0 = f.wealth, m0 = f.mood;
  ok(G.folkGift(S, f.id) === null, 'подарок вручён');
  ok(f.wealth > w0 && f.mood > m0, 'достаток и настроение выросли');
  ok(G.folkGift(S, f.id, true) !== null, 'без таверны пир не устроить');
  let built = false;
  for (let dy = -6; dy <= 6 && !built; dy++) for (let dx = -6; dx <= 6 && !built; dx++) {
    const x = c.x + dx, y = c.y + dy;
    if (G.siteOk(S, 0, 'tavern', x, y) === null) built = G.doBuild(S, 0, 'tavern', x, y) === null;
  }
  ok(built && G.folkGift(S, f.id, true) === null, 'пир с таверной проходит');
  const f2 = G.makeFolk(S, c); f2.age = 25; S.folk.push(f2);
  ok(G.folkDraft(S, f2.id) === null && f2.st === 'army', 'житель призван в ополчение');
  ok(G.folkDraft(S, f2.id) !== null, 'повторно не призывают');
  ok(G.folkGift(S, f2.id) !== null, 'служащему подарки не нужны');
  ok(G.folkGift(S, 999999) !== null, 'несуществующий житель отклонён');
}

// --- союзникам войну объявлять нельзя (и ультиматум её не запустит)
{
  const S = mk();
  const r = G.rel(S, 0, 1);
  r.ally = true; r.st = 'peace';
  ok(G.startWar(S, 0, 1) !== null, 'игрок не может напасть на союзника');
  ok(r.st !== 'war' && r.ally, 'война не началась, союз цел');
  ok(G.startWar(S, 1, 0) !== null, 'союзник не может напасть на игрока');
  ok(r.st !== 'war', 'по-прежнему мир');
  r.ally = false;
  ok(G.startWar(S, 0, 1) === undefined || G.startWar(S, 0, 1) === null || r.st === 'war', 'без союза война возможна');
}

// --- пилорама вырубает лес: на карте видно прогресс
{
  const S = mk();
  const c = S.cities.find(q => q.owner === 0), p = S.players[0];
  p.stock.gold = 9999; p.stock.wood = 9999;
  let spot = null;
  for (let dy = -5; dy <= 5 && !spot; dy++) for (let dx = -5; dx <= 5 && !spot; dx++) {
    const x = c.x + dx, y = c.y + dy;
    if (G.siteOk(S, 0, 'sawmill', x, y) === null) spot = [x, y];
  }
  ok(spot && G.doBuild(S, 0, 'sawmill', spot[0], spot[1]) === null, 'лесопилка построена');
  // кладём лес вокруг и смотрим, что его вырубают
  let forest = 0;
  for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
    const x = spot[0] + dx, y = spot[1] + dy, i = y * S.W + x;
    if (S.occ[i] < 0) { S.ter[i] = G.TR.FOREST; forest++; }
  }
  ok(forest > 0, 'лес под вырубку заложен', forest);
  run(S, 100); // 25 секунд — как минимум одно дерево срубят
  let left = 0;
  for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) { const i = (spot[1] + dy) * S.W + spot[0] + dx; if (S.ter[i] === G.TR.FOREST && S.occ[i] < 0) left++; }
  ok(left < forest, 'лесорубы вырубают лес', forest + ' -> ' + left);
}

console.log('пройдено:', pass, 'провалено:', fail);
process.exit(fail ? 1 : 0);
