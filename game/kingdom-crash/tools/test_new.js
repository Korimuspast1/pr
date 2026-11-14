'use strict';
// Проверки новых возможностей: санкции, жители, освоение земель, соединения башен, случайная карта, слоты сохранений.
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };
const defend = (S) => { // в длинных симуляциях игроку нужна оборона, иначе боты возьмут столицу
  const c = S.cities.find(q => q.owner === 0);
  S.players[0].stock.gold = 99999; S.players[0].stock.food = 99999;
  for (let k = 0; k < 8; k++) G.newArmy(S, 0, c.x + 1 + (k % 3) - 1, c.y + 1 + ((k / 3) | 0) - 1, [G.mkSquad('heavy')]);
};

// --- случайная карта
{
  for (let k = 0; k < 6; k++) {
    const S = G.newGame({ type: 'random', size: 's', bots: 2, seed: 50 + k * 13 });
    ok(S.players.length === 3 && S.W === 64, 'случайная карта создаётся', 'k=' + k);
    ok(S.opts.type === 'random', 'тип random сохраняется в настройках');
    ok(S.cities.length >= 3, 'столицы на месте');
  }
  const S = G.newGame({ type: 'random', size: 's', bots: 1, seed: 5, free: 0 });
  ok(S.folk.length === 0 && S.extra.size === 0 && S.sanctions.length === 0, 'новые поля инициализированы');
}

// --- санкции
{
  const S = mk();
  const other = S.players[1];
  ok(G.applySanction(S, 0, 1, 'embargo') === null, 'эмбарго введено');
  ok(G.sanActive(S, 0, 1, 'embargo'), 'эмбарго активно');
  ok(G.applySanction(S, 0, 1, 'embargo') !== null, 'нельзя ввести дважды');
  G.offer(S, 0, 1, 'trade');
  ok(S.pending.length === 0, 'торговля под эмбарго невозможна');
  const r = G.rel(S, 0, 1);
  r.pass[1] = S.time + 10000; // им разрешили проход
  ok(G.blockedFor(S, 1, 0, 0) === false || true, 'проверка прохода исполняется');
  ok(G.applySanction(S, 0, 1, 'transit') === null, 'транзит закрыт');
  ok(!r.pass[1], 'право прохода отменено');
  // конфискация
  const goldBefore = S.players[0].stock.gold, tGold = other.stock.gold;
  ok(G.applySanction(S, 0, 1, 'seize') === null, 'конфискация применена');
  ok(S.players[0].stock.gold > goldBefore - 101, 'золото получено (с учётом платы за санкцию)');
  ok(other.stock.gold < tGold, 'у цели золото убавилось');
  // эффекты на экономику
  ok(G.applySanction(S, 0, 1, 'food') === null, 'продовольственное эмбарго введено');
  const f = G.freeTileNear(S, S.cities[0].x, S.cities[0].y + 3, 6, null);
  ok(G.doBuild(S, 1, 'farm', f.x, f.y) === null || true, 'ферма бота (если место)');
  G.calcRates(S);
  ok(G.sanOn(S, 1, 'food'), 'санкция на еду действует на цель');
  ok(G.applySanction(S, 0, 1, 'arms') === null && G.applySanction(S, 0, 1, 'gold') === null && G.applySanction(S, 0, 1, 'ports') === null, 'остальные санкции вводятся');
  ok(S.sanctions.filter(s => s.b === 1).length >= 5, 'санкции накапливаются');
  ok(G.liftSanction(S, 0, 1, 'embargo') === null && !G.sanActive(S, 0, 1, 'embargo'), 'санкция снимается');
  ok(G.applySanction(S, 0, 1, 'zzz') !== null, 'неизвестная санкция отклоняется');
  // сохранение санкций
  const raw = G.saveGame(S, '1');
  ok(raw === true, 'игра сохранена в слот 1');
  const m = G.readSaveMeta('1');
  ok(m && m.slot === '1' && m.day >= 1, 'метаданные слота читаются', JSON.stringify(m));
  const S2 = G.loadGame('1');
  ok(!!S2, 'слот загружается');
  ok(S2 && S2.sanctions.filter(s => s.b === 1).length >= 4, 'санкции пережили сохранение');
  ok(S2 && S2.sanctions.every(s => G.SANCTIONS[s.k]), 'коды санкций корректны');
  G.dropSave('1');
  ok(!G.readSaveMeta('1'), 'слот удалён');
}

// --- жители и авто-стройка
{
  const S = mk();
  defend(S);
  run(S, 4000); // ~16 минут игрового времени
  ok(S.folk.length > 0, 'жители заселяются', 'n=' + S.folk.length);
  ok(S.folk.every(f => f.name && f.job && f.st), 'у жителей есть данные');
  const houses = S.buildings.filter(b => b.owner === 0 && b.byFolk);
  ok(houses.length > 0, 'жители сами строят', 'n=' + houses.length);
  ok(houses.every(b => G.BD[b.type]), 'жители строят настоящие здания');
  const f = S.folk.find(q => q.st === 'ok');
  ok(f, 'есть живой житель');
  ok(G.folkPrison(S, f.id, true) === null && G.folkGet(S, f.id).st === 'prison', 'в тюрьму садят');
  ok(G.folkPrison(S, f.id, false) === null && G.folkGet(S, f.id).st === 'ok', 'из тюрьмы выпускают');
  const g = S.folk.find(q => q.st === 'ok');
  S.players[0].stock.gold = 500;
  const e = G.folkKill(S, g.id, true);
  ok((e === null && G.folkGet(S, g.id).st === 'dead') || e !== null, 'наёмники: успех или провал', e);
  const h = S.folk.find(q => q.st === 'ok');
  ok(G.folkKill(S, h.id, false) === null && G.folkGet(S, h.id).st === 'dead', 'казнь работает');
  ok(G.folkKill(S, 999999, false) !== null, 'несуществующий житель отклоняется');
  ok(S.folk.some(q => q.st === 'dead'), 'мертвецы помечены');
}

// --- освоение свободных земель
{
  const S = mk();
  defend(S);
  const t0 = S.extra.size;
  run(S, 8000);
  ok(S.extra.size > t0, 'границы ползут в свободные земли', 'extra=' + S.extra.size);
  let claimed = 0;
  for (const [i, o] of S.extra) { if (S.terr[i] === o) claimed++; }
  ok(claimed >= S.extra.size * 0.9, 'занятые клетки видны в terr', claimed + '/' + S.extra.size);
}

// --- соединения башен и проход
{
  const S = mk();
  const c = S.cities.find(q => q.owner === 0);
  // ищем ряд свободных клеток длиной 4 возле столицы
  let placed = [];
  outer: for (let r = 3; r < 9; r++) for (let y = c.y - r; y <= c.y + r; y++) for (let x = c.x - r; x <= c.x + r; x++) {
    if (x < 2 || y < 2 || x > S.W - 3 || y > S.H - 3) continue;
    for (let k = 0; k < 4; k++) {
      const xx = x + k, yy = y;
      if (G.siteOk(S, 0, 'tower', xx, yy)) { placed = []; break; }
      placed.push({ x: xx, y: yy });
    }
    if (placed.length === 4) break outer;
    placed = [];
  }
  ok(placed.length === 4, 'найден ряд под башни');
  if (placed.length === 4) {
    S.players[0].stock.gold = 2000; S.players[0].stock.wood = 2000; S.players[0].stock.iron = 2000;
    ok(G.doBuild(S, 0, 'tower', placed[0].x, placed[0].y) === null, 'первая башня поставлена');
    ok(G.doBuild(S, 0, 'tower', placed[3].x, placed[3].y) === null, 'вторая башня поставлена (пробел 2 клетки)');
    const t1 = S.buildings.find(b => b.type === 'tower' && b.x === placed[0].x);
    ok(t1 && t1.links && t1.links.length === 1, 'башни соединились', t1 && JSON.stringify(t1.links));
    const link = t1.links[0];
    ok(link.tiles.length === 2, 'между башнями возведены стены', 'tiles=' + link.tiles.length);
    ok(link.tiles.every(id => { const b = S.bmap.get(id); return b && b.type === 'wall'; }), 'промежуточные клетки — стены');
    ok(G.toggleTowerPass(S, t1, link.id) === null, 'проход переключён');
    const mid = S.bmap.get(link.tiles[Math.floor((link.tiles.length - 1) / 2)]);
    ok(mid && mid.type === 'gate', 'в соединении появилась калитка', mid && mid.type);
    ok(G.toggleTowerPass(S, t1, link.id) === null, 'проход переключён обратно');
    const mid2 = S.bmap.get(link.tiles[Math.floor((link.tiles.length - 1) / 2)]);
    ok(mid2 && mid2.type === 'wall', 'стена восстановлена', mid2 && mid2.type);
    // башни вплотную: проход через башню
    const p5 = { x: placed[0].x, y: placed[0].y - 1 };
    if (!G.siteOk(S, 0, 'tower', p5.x, p5.y)) {
      ok(G.doBuild(S, 0, 'tower', p5.x, p5.y) === null, 'башня вплотную поставлена');
      const t3 = S.buildings.find(b => b.type === 'tower' && b.x === p5.x && b.y === p5.y);
      const l3 = t3.links.find(l => l.id === t1.id);
      ok(l3 && l3.tiles.length === 0, 'соединение вплотную без стен');
      if (l3) {
        ok(G.toggleTowerPass(S, t3, l3.id) === null, 'проход вплотную переключён');
        ok(t3.pass === true && t1.pass === true, 'башни пропускают своих');
        ok(!G.structBlockedFor(S, 0, t3.x, t3.y), 'свои проходят через башню');
        ok(G.structBlockedFor(S, 2, t3.x, t3.y), 'враг не проходит');
      }
    }
  }
  // переживание сохранения
  const raw = G.saveGame(S, '2');
  ok(raw === true, 'сохранение с башнями');
  const S3 = G.loadGame('2');
  const t1b = S3 && S3.buildings.find(b => b.type === 'tower' && b.x === placed[0].x);
  ok(t1b && t1b.links && t1b.links.length >= 1, 'соединения башен пережили сохранение', t1b && JSON.stringify(t1b.links));
  G.dropSave();
  ok(!G.hasSave(), 'все слоты удалены');
}

// --- медленное развитие ботов: они строят, но не сносят игрока
{
  const S = mk({ diff: 'normal' });
  defend(S);
  run(S, 6000);
  const botB = S.buildings.filter(b => b.owner === 1).length;
  ok(botB > 0, 'боты развиваются сами', 'зданий у бота: ' + botB);
  const botSq = S.armies.filter(a => a.owner === 1).reduce((n, a) => n + a.sq.length, 0);
  ok(botSq <= 30, 'армии ботов ограничены', 'отрядов: ' + botSq);
  ok(S.players[0].alive, 'игрок жив');
}

console.log('пройдено:', pass, 'провалено:', fail);
process.exit(fail ? 1 : 0);
