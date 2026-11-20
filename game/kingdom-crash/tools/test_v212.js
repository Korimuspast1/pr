'use strict';
// Проверки 2.12.0: исправлен «баг при отдалении камеры» (пиксельная минификация + облака вне карты),
// обзорная карта плотнее (16 точек/клетка), камера центрирует маленькую карту, боты думают заметно умнее.
const fs = require('fs'), path = require('path');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const root = path.resolve(__dirname, '..');
const src = f => fs.readFileSync(path.join(root, f), 'utf8');

ok(G.GAME_VERSION === '2.12.0', 'GAME_VERSION = 2.12.0', G.GAME_VERSION);

{ // CSS: канвас больше не сжимается ближним соседом (главная причина «ступенек» и полос на телефоне)
  const css = src('www/style.css');
  const cvRules = css.match(/[^{}]*#cv[^{}]*\{[^}]*\}/g) || [];
  ok(cvRules.length > 0, 'правило #cv найдено', String(cvRules.length));
  ok(cvRules.every(r => !/image-rendering:\s*pixelated/.test(r)), 'канвас ни в одном правиле #cv не pixelated', cvRules[0]);
}
{ // облака рисуются строго внутри карты
  const r = src('www/js/render.js');
  ok(r.includes('c.rect(0, 0, cw, S.H * TS); c.clip()'), 'облака обрезаются по границам карты');
  ok(r.includes('if (mw <= vw) cam.x = (mw - vw) / 2'), 'маленькая карта центрируется при отдалении');
  ok(r.includes('if (mh <= vh) cam.y = (mh - vh) / 2'), 'маленькая карта по вертикали центрируется');
}
{ // обзорная карта стала плотнее
  const t = src('www/js/terrain.js');
  ok(/const PPT = 32, CT = 8, CPX = PPT \* CT, OV = 16;/.test(t), 'обзорная карта 16 точек на клетку');
}
{ // ИИ: ключевые умные механизмы присутствуют в коде
  const a = src('www/js/ai.js');
  ok(a.includes('function aiReaction'), 'реакция на дефициты (aiReaction)');
  ok(a.includes('aiTowerDef'), 'оборона укреплений противника в расчёте целей');
  ok(a.includes('aiWars'), 'учёт войн (второго фронта без нужды не открывает)');
  ok(a.includes('defense'), 'оборонительный план: армия идёт к городу под угрозой');
  ok(a.includes("'tower'"), 'боты строят башни при войне');
  ok(a.includes('storehouse'), 'боты строят склад, когда закрыт лимит предметов');
  ok(a.includes('granary'), 'боты строят амбар при проседающей еде');
  ok(a.includes('engaged'), 'бот предпочитает слабого, уже повязанного в войне');
}
{ // ИИ: симуляция — бот строит экономику и не ломается (детерминированно по seed)
  const o = { type: 'continent', size: 's', bots: 2, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 9090, free: 1, events: 0 };
  const S = G.newGame(o);
  S.players.forEach(p => p.human = false);
  for (let i = 0; i < 1600 && !S.over; i++) G.tick(S, 0.25); // ~400 секунд
  const bot = S.players[1];
  ok(bot.alive, 'бот выживает на длинной дистанции');
  const bld = S.buildings.filter(b => b.owner === bot.id).length;
  ok(bld >= 15, 'бот построил развитую экономику', 'зданий: ' + bld);
  const myFarms = S.buildings.filter(b => b.owner === bot.id && b.type === 'farm').length;
  ok(myFarms >= 2, 'бот строит фермы без подсказки', 'ферм: ' + myFarms);
  const finiteStock = Object.keys(bot.stock).every(k => Number.isFinite(bot.stock[k]));
  ok(finiteStock, 'склад бота конечен');
}
console.log('Итого v2.12.0:', pass, 'ок,', fail, 'ошибок');
process.exit(fail ? 1 : 0);
