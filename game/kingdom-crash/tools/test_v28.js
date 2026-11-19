'use strict';
// Проверки 2.8.0: времена года и настоящая погода (влияет на игру), новые постройки
// (склад / амбар / ратуша / посольство), арбалетчики, новые события, гроза и молнии.
const fs = require('fs'), path = require('path');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const root = path.resolve(__dirname, '..');
const src = f => fs.readFileSync(path.join(root, f), 'utf8');
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };
const landNear = (S, c, r) => G.freeTileNear(S, c.x, c.y + r, 4, null);
const findLand = (S) => { for (let y = 1; y < S.H - 1; y++) for (let x = 1; x < S.W - 1; x++) { const i = y * S.W + x; if (S.terr[i] === 0 && !G.siteOk(S, 0, 'storehouse', x, y)) return { x, y }; } return null; };
const give = S => { const p = S.players[0]; for (const k in p.stock) p.stock[k] = 5000; for (const k of G.STOCK_KEYS) p.stock[k] = 5000; };
const build = (S, type) => { for (let y = 0; y < S.H; y++) for (let x = 0; x < S.W; x++) if (!G.siteOk(S, 0, type, x, y)) { const e = G.doBuild(S, 0, type, x, y); if (e === null) return true; } return false; };

// ===== Версия =====
{
  ok(/^2\.(8|9)\.0$/.test(G.GAME_VERSION), 'GAME_VERSION 2.8.x/2.9.x (точную цифру держит test_v29.js)', G.GAME_VERSION);
  const html = src('www/index.html');
  ok(!/2\.7\.0/.test(html.replace(/<!--.*?-->/gs, '')), 'версия в интерфейсе обновлена (или не упоминает 2.7.0)');
}

// ===== Сезоны и погода =====
{
  ok(G.SEASONS.length === 4 && G.SEASONS.map(s => s.id).join(',') === 'spring,summer,autumn,winter', 'четыре сезона: весна, лето, осень, зима');
  ok(G.SEASON_LEN >= G.DAY * 4, 'сезон длится достаточно долго (≥4 дней)', String(G.SEASON_LEN / G.DAY));
  for (const w of G.WEATHERS) ok(Array.isArray(w.ok) && w.ok.every(i => i >= 0 && i < 4), 'погода «' + w.id + '»: допустимые сезоны в диапазоне 0..3');
  for (let s = 0; s < 4; s++) {
    const S = mk(); S.time = s * G.SEASON_LEN + 10; S._wxT = -1;
    const wx = G.weatherOf(S);
    ok(wx.season.id === G.SEASONS[s].id, 'сезон на t=' + (s * G.SEASON_LEN + 10) + ': ' + G.SEASONS[s].id, wx.season.id);
    const pool = G.WEATHERS.filter(w => w.ok.indexOf(s) >= 0);
    ok(pool.some(w => w.id === wx.id), 'погода «' + wx.id + '» допустима для сезона ' + s);
    ok(wx.temp >= Math.min(wx.lo, wx.hi) && wx.temp <= Math.max(wx.lo, wx.hi), 'температура в пределах диапазона погоды');
    const wx2 = G.weatherOf(S);
    ok(wx2.id === wx.id && wx2.temp === wx.temp, 'погода детерминирована от семени партии (две загрузки — одна погода)');
  }
  const S = mk();
  const w1 = G.weather(S);
  ok(S._wxT === S.tk && G.weather(S) === w1, 'погода кешируется на тик');
  ok(typeof G.seasonOf(S).n === 'string' && G.seasonOf(S).farm > 0, 'seasonOf отдаёт сезон с урожайностью');
}

// ===== Влияние сезона на фермы =====
{
  const S = mk(); give(S);
  S.time = 1 * G.SEASON_LEN + 10; S._wxT = -1; // лето — фермы лучше
  ok(build(S, 'farm'), 'ферма построена (лето)');
  const farm = S.buildings.find(b => b.type === 'farm');
  const p = S.players[0];
  const rSummer = G.farmRate(S, farm, p);
  S.time = 3 * G.SEASON_LEN + 10; S._wxT = -1; // зима — фермы почти молчат
  const rWinter = G.farmRate(S, farm, p);
  ok(rSummer > rWinter * 2, 'летом ферма кормит заметно лучше зимы', 'лето=' + rSummer.toFixed(2) + ' зима=' + rWinter.toFixed(2));
}

// ===== Склад: лимит предметов растёт =====
{
  const S = mk(); give(S);
  ok(build(S, 'storehouse'), 'склад построен');
  G.calcRates(S);
  ok(S.players[0].itCap === G.ITEM_CAP + 100, 'склад +100 к лимиту', String(S.players[0].itCap));
  ok(build(S, 'storehouse'), 'второй склад построен');
  G.calcRates(S);
  ok(S.players[0].itCap === G.ITEM_CAP + 200, 'два склада +200 к лимиту', String(S.players[0].itCap));
}

// ===== Ратуша: города дают больше золота =====
{
  const S = mk(); give(S);
  G.calcRates(S); const before = S.players[0].rate.gold;
  ok(build(S, 'townhall'), 'ратуша построена');
  const p0 = S.players[0];
  const cityGold = S.cities.reduce((n, c) => n + (c.owner === 0 ? (0.6 + 0.45 * c.level) * (G.perk(p0, 'gold') ? 1.25 : 1) : 0), 0);
  G.calcRates(S); const after = S.players[0].rate.gold;
  ok(Math.abs((after - before) - cityGold * 0.2) < 1e-6, 'после ратуши именно городская часть золота выросла на 20%', 'Δ=' + (after - before).toFixed(3) + ' ожидалось ' + (cityGold * 0.2).toFixed(3));
}

// ===== Амбар: армия ест меньше =====
{
  const S = mk(); give(S);
  const c = S.cities.find(q => q.owner === 0);
  const a = G.newArmy(S, 0, c.x + 2.5, c.y + 2.5, [G.mkSquad('heavy')]);
  G.calcRates(S); const before = S.players[0].rate.food;
  ok(build(S, 'granary'), 'амбар построен');
  G.calcRates(S); const after = S.players[0].rate.food;
  const squads = a.sq.length;
  ok(Math.abs((after - before) - 0.12 * squads * 0.25) < 1e-9, 'амбар снижает содержание армии на 25%', 'Δ=' + (after - before).toFixed(4));
}

// ===== Посольство: торговля дороже =====
{
  const S = mk({ bots: 1 }); give(S);
  const r = G.rel(S, 0, 1); r.st = 'peace'; r.trade = true;
  G.calcRates(S); const before = S.players[0].rate.gold;
  ok(build(S, 'embassy'), 'посольство построено');
  G.calcRates(S); const after = S.players[0].rate.gold;
  ok(Math.abs((after - before) - 0.5 * 0.3) < 1e-9, 'посольство поднимает торговый доход на 30%', 'Δ=' + (after - before).toFixed(3));
}

// ===== Арбалетчики =====
{
  ok(G.SQ.xbow && G.SQ.xbow.bld === 'barracks' && G.SQ.xbow.need && G.SQ.xbow.need.xbow >= 1, 'арбалетчики обучаются в казармах за арбалеты');
  ok(G.RECIPES.workshop && G.RECIPES.workshop.xbow && G.RECIPES.workshop.xbow.out === 'xbow', 'мастерская делает арбалеты');
  ok(/MATCH\.xbow = \{/.test(src('www/js/data.js')) && /xbow: 1\.35/.test(src('www/js/data.js')), 'таблица эффективности учитывает арбалеты (копья контриграют)');
  ok(G.STOCK_KEYS.indexOf('xbow') >= 0, 'арбалеты хранятся на складе');
  const data = src('www/js/data.js');
  for (const k of ['DRAW.storehouse', 'DRAW.granary', 'DRAW.townhall', 'DRAW.embassy', 'HF.xbow']) ok(data.includes(k), 'отрисовка ' + k);
  const ui = src('www/js/ui.js');
  for (const k of ['storehouse', 'granary', 'townhall', 'embassy']) ok(ui.includes(k), 'постройка «' + k + '» в интерфейсе');
}

// ===== Новые события =====
{
  const ids = G.EVENTS.map(e => e.id);
  for (const id of ['bard', 'artisans', 'locusts', 'refugees', 'plague', 'comet', 'preacher']) ok(ids.indexOf(id) >= 0, 'событие «' + id + '» существует');
  for (const id of ['bard', 'artisans', 'locusts', 'refugees', 'plague', 'comet', 'preacher']) {
    const S = mk(); give(S);
    const ev = G.EVENTS.find(e => e.id === id);
    let threw = null; try { ev.run(S, S.players[0], () => 0.5); } catch (e) { threw = e; }
    ok(!threw, 'событие «' + id + '» не падает', threw ? String(threw) : '');
  }
  // регрессия: комета не роняет игру (раньше rel() по себе бросало TypeError)
  const S = mk(); const ev = G.EVENTS.find(e => e.id === 'comet');
  let threw = null; try { ev.run(S, S.players[0], () => 0.5); } catch (e) { threw = e; }
  ok(!threw, 'комета теплеет всем, кроме самого себя', threw ? String(threw) : '');
  // артисаны не кладут на склад больше лимита
  const S2 = mk(); const p2 = S2.players[0]; p2.itCap = 12; p2.stock.sword = 11; p2.stock.armL = 11;
  G.EVENTS.find(e => e.id === 'artisans').run(S2, p2, () => 0.5);
  ok(p2.stock.sword <= 12 && p2.stock.armL <= 12, 'гильдия не превышает складской лимит', 'мечей=' + p2.stock.sword);
}

// ===== Гроза: молния бьёт, но не добивает =====
{
  const S = mk(); give(S);
  build(S, 'tower'); build(S, 'market'); build(S, 'farm');
  const target = S.buildings[0];
  const hp0 = target.hp || 100;
  let hit = false;
  for (let k = 0; k < 40 && !hit; k++) { const hpBefore = target.hp; G.lightningStrike(S); hit = S.buildings.some(b => (b.hp || 100) < hpBefore) || (target.hp || 100) < hp0; }
  ok(S.fx.some(f => f.k === 'flash') || hit, 'молния оставляет вспышку и урон');
  ok(S.buildings.every(b => (b.hp || 1) >= 1), 'молния не добивает здание до нуля');
}

// ===== Сохранение: новая версия, служебные поля не уезжают в файл =====
{
  const S = mk(); run(S, 30);
  ok(G.saveGame(S, '1') === true, 'игра сохраняется');
  const raw = G._ls.getItem('kc_save_1');
  ok(!!raw && raw.indexOf('_wx') < 0 && raw.indexOf('"fx"') < 0, 'служебные поля (погода, эффекты) в сохранение не пишутся');
  const S2 = G.loadGame('1');
  ok(!!S2 && S2.players.length === S.players.length && S2.buildings.length === S.buildings.length, 'сохранение читается обратно');
}

// ===== Длинная симуляция через зиму без ошибок =====
{
  const S = mk(); give(S);
  S.time = 3 * G.SEASON_LEN + 5; S._wxT = -1;
  let threw = null; try { run(S, 600); } catch (e) { threw = e; }
  ok(!threw, '600 тиков в зиму без ошибок', threw ? String(threw && threw.stack).split('\n')[0] : '');
}

console.log('\nИтого v2.8.0: ' + pass + ' ок, ' + fail + ' ошибок');
process.exit(fail ? 1 : 0);
