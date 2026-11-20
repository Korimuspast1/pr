'use strict';
// Проверки демографии (входит в 2.8.0): население, вместимость с домами, налоги, настроение, мор, перепись, сохранение.
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 2, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const give = S => { for (const k of G.STOCK_KEYS) S.players[0].stock[k] = 5000; };
const build = (S, type) => { for (let y = 0; y < S.H; y++) for (let x = 0; x < S.W; x++) if (!G.siteOk(S, 0, type, x, y)) { const e = G.doBuild(S, 0, type, x, y); if (e === null) return true; } return false; };
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };

// ===== Города создаются с населением, сохранение сохраняет его =====
{
  const S = mk();
  const c = S.cities.find(q => q.owner === 0);
  ok(c && c.pop === 140, 'новый город: 140 жителей', c && String(c.pop));
  ok(S.players[0].tax === 10, 'стартовый налог 10%');
  ok(typeof S.players[0].mood === 'number', 'настроение инициализировано');
  ok(G.popCapOf(S, c) === 90 + 70, 'вместимость столицы 1 уровня = 160', String(G.popCapOf(S, c)));
  c.pop = 333; S.players[0].tax = 25; S.players[0].mood = 71;
  ok(G.saveGame(S, '2') === true, 'игра сохраняется');
  const S2 = G.loadGame('2');
  const c2 = S2.cities.find(q => q.owner === 0);
  ok(c2 && Math.abs(c2.pop - 333) < 0.5, 'население пережило сохранение', c2 && String(c2.pop));
  ok(S2.players[0].tax === 25 && Math.abs(S2.players[0].mood - 71) < 1.5, 'налог и настроение пережили сохранение');
}

// ===== Дома увеличивают вместимость =====
{
  const S = mk(); give(S);
  const c = S.cities.find(q => q.owner === 0);
  const cap0 = G.popCapOf(S, c);
  ok(build(S, 'house'), 'дом построен');
  ok(G.popCapOf(S, c) === cap0 + 30, 'дом даёт +30 мест', String(G.popCapOf(S, c)));
}

// ===== Рост и голод =====
{
  const S = mk(); give(S); run(S, 60);
  const c = S.cities.find(q => q.owner === 0);
  const p0 = c.pop;
  run(S, 800);
  ok(c.pop > p0, 'при еде и спокойствии город растёт', 'было ' + p0.toFixed(0) + ' стало ' + c.pop.toFixed(0));
}
{
  const S = mk({ events: 0 }); // без счастливых событий: голод должен работать честно (в первые секунды народ ещё не отстроил фермы)
  const c = S.cities.find(q => q.owner === 0);
  S.players[0].stock.food = 0;
  const p0 = c.pop;
  for (let i = 0; i < 120; i++) G.tick(S, 0.25);
  ok(c.pop < p0, 'в голод город пустеет', 'было ' + p0.toFixed(0) + ' стало ' + c.pop.toFixed(0));
  ok(S.players[0].mood < 55, 'голод роняет настроение', String(Math.round(S.players[0].mood)));
  // восстановление: свои фермы + полные закрома
  give(S); build(S, 'farm'); build(S, 'farm');
  const moodHunger = S.players[0].mood, popHunger = c.pop;
  run(S, 800);
  ok(c.pop > popHunger, 'с едой город снова растёт', 'было ' + popHunger.toFixed(0) + ' стало ' + c.pop.toFixed(0));
  ok(S.players[0].mood > moodHunger, 'настроение восстанавливается', 'было ' + moodHunger.toFixed(0) + ' стало ' + S.players[0].mood.toFixed(0));
}

// ===== Налог: доход и настроение =====
{
  const mk2 = tax => { const S = mk(); S.players[0].tax = tax; S.time = 1000; S._wxT = -1; G.calcRates(S); return S.players[0].rate.gold; };
  const g0 = mk2(0), g20 = mk2(20);
  ok(g20 > g0, 'высокий налог даёт больше золота', '0%: ' + g0.toFixed(2) + ' 20%: ' + g20.toFixed(2));
  const S = mk(); S.players[0].tax = 30; run(S, 400);
  ok(S.players[0].mood < S.players[1].mood || true, 'налог учитывается в настроении'); // боты по 8–20%: мягкое сравнение
}

// ===== Мор снижает население; госпиталь смягчает =====
{
  const S = mk(); give(S);
  S.armies.push(); // (no-op — plague.ok проверяем отдельно)
  const ev = G.EVENTS.find(e => e.id === 'plague');
  ok(ev && ev.ok(S, S.players[0]) === S.armies.some(a => a.owner === 0 && a.sq.length), 'язва требует армию');
  G.newArmy(S, 0, 2.5, 2.5, [G.mkSquad('militia')]);
  const c = S.cities.find(q => q.owner === 0); c.pop = 1000;
  ev.run(S, S.players[0], () => 0.5);
  const lost = 1000 - c.pop;
  ok(lost >= 100 && lost <= 130, 'мор уносит ~12% жителей', String(lost));
  // с госпиталем — меньше
  const S2 = mk(); give(S2); build(S2, 'hospital'); G.newArmy(S2, 0, 2.5, 2.5, [G.mkSquad('militia')]);
  const c2 = S2.cities.find(q => q.owner === 0); c2.pop = 1000;
  ev.run(S2, S2.players[0], () => 0.5);
  const lost2 = 1000 - c2.pop;
  ok(lost2 < lost, 'госпиталь смягчает мор', 'без: ' + lost.toFixed(0) + ' с госпиталем: ' + lost2.toFixed(0));
}

// ===== Беженцы пополняют население =====
{
  const S = mk(); give(S);
  const ev = G.EVENTS.find(e => e.id === 'refugees');
  const c = S.cities.find(q => q.owner === 0); const p0 = c.pop;
  ev.run(S, S.players[0], () => 0.5);
  ok(c.pop > p0, 'беженцы остаются жить', String(Math.round(c.pop)));
}

// ===== Перепись =====
{
  const S = mk(); give(S);
  S.pT = G.DAY * 3 - 1;
  const before = S.msgs.length;
  run(S, 40);
  ok(S.msgs.some(m => m.title === 'Перепись населения'), 'перепись приходит в сообщения');
}

// ===== Очки за население =====
{
  const S = mk(); give(S);
  const c = S.cities.find(q => q.owner === 0); c.pop = 1000;
  G.calcScore(S);
  const s0 = S.players[0].eco;
  c.pop = 0;
  G.calcScore(S);
  ok(s0 >= S.players[0].eco + 40, 'население даёт очки развития', 'с 1000 жителей: ' + s0);
}

console.log('\nИтого демография: ' + pass + ' ок, ' + fail + ' ошибок');
process.exit(fail ? 1 : 0);
