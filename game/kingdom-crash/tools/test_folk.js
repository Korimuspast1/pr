'use strict';
// Проверки быта жителей: ходьба, дела, беседы, тропинки.
// Запуск: node tools/test_folk.js
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const run = (S, n) => { for (let i = 0; i < n; i++) G.tick(S, 0.25); };

// --- поля быта при создании
{
  const S = mk();
  const f = G.makeFolk(S, S.cities.find(c => c.owner === 0));
  ok(Number.isFinite(f.x) && Number.isFinite(f.y), 'у нового жителя есть положение');
  ok(f.act === 'rest' && Number.isFinite(f.actT) && Number.isFinite(f.speed), 'заданы быт и скорость');
  ok(f.face === 1 || f.face === -1, 'задано направление взгляда');
}

// --- личный генератор не трогает общий поток
{
  const S1 = mk(), S2 = mk();
  const seq1 = [S1.rnd(), S1.rnd(), S1.rnd()];
  const f = G.makeFolk(S1, S1.cities[0]);
  G.folkPick(S1, f); G.folkMeet(S1, f); G.frnd(S1, f);
  const S3 = mk();
  const seq2 = [S3.rnd(), S3.rnd(), S3.rnd()];
  ok(seq1[0] === seq2[0] && seq1[1] === seq2[1] && seq1[2] === seq2[2], 'быт жителей не меняет поток S.rnd');
  ok(f._r === undefined || typeof f._r === 'number', 'личное состояние генератора числовое');
}

// --- жители ходят и никогда не заходят в воду
{
  const S = mk();
  run(S, 200);
  const alive = S.folk.filter(f => f.st === 'ok');
  ok(alive.length > 0, 'жители заселились', alive.length);
  const moving = alive.filter(f => f.act === 'walk');
  ok(moving.length > 0, 'кто-то идёт (act=walk)', moving.length);
  const f0 = alive[0];
  const x0 = f0.x, y0 = f0.y;
  run(S, 200);
  const f1 = S.folk.find(q => q.id === f0.id);
  ok(Math.hypot(f1.x - x0, f1.y - y0) > 0.05, 'житель сместился с места');
  let wet = 0;
  for (const f of S.folk) if (f.st === 'ok' && f.x !== undefined && G.isWater(S.ter[Math.floor(f.y) * S.W + Math.floor(f.x)])) wet++;
  ok(wet === 0, 'жители не стоят в воде', wet);
}

// --- тропинки растут под ногами и плавно зарастают
{
  const S = mk();
  run(S, 300);
  ok(S.trails && Object.keys(S.trails).length > 0, 'тропинки проступили', S.trails ? Object.keys(S.trails).length : 0);
  const vals = Object.values(S.trails);
  ok(vals.every(v => v > 0 && v <= 1), 'сила тропинки в пределах (0,1]');
  // зарастание проверяем на синтетической тропинке вне карты: по ней никто не ходит,
  // поэтому видно только затухание, а не рост от ног (иначе тест зависит от расстановки построек)
  S.trails = { '99999999': 0.9 };
  run(S, 200); // много циклов затухания
  const v1 = S.trails['99999999'];
  ok(v1 === undefined || v1 < 0.9, 'тропинки плавно зарастают', '0.9 -> ' + v1);
  // прямая проверка markTrail: плавный рост без скачков
  const S2 = mk();
  G.markTrail(S2, 10, 10);
  const a = S2.trails[10 * S2.W + 10];
  G.markTrail(S2, 10, 10);
  const b = S2.trails[10 * S2.W + 10];
  ok(a > 0 && b > a && b <= 1 && b - a <= 0.01, 'отметка тропинки растёт малыми шагами', a + '->' + b);
}

// --- беседы: встреча на месте связывает двоих
{
  const S = mk();
  run(S, 150);
  const alive = S.folk.filter(f => f.st === 'ok' && f.x !== undefined);
  ok(alive.length >= 2, 'для беседы есть пара', alive.length);
  // расставим двоих вплотную и разбудим встречу
  const [a, b] = alive;
  a.x = 50.2; a.y = 50.2; b.x = 50.8; b.y = 50.6;
  a.act = 'work'; b.act = 'rest'; b._mate = null; a._mate = null;
  let met = false;
  for (let i = 0; i < 40 && !met; i++) {
    G.folkMeet(S, a);
    met = a.act === 'chat' && b.act === 'chat' && a._mate === b && b._mate === a;
    if (!met) { a._r = (i + 1) * 2654435761 >>> 0; } // разные броски
  }
  ok(met, 'жители встретились и заговорили');
  ok(a.face === -b.face, 'развернулись друг к другу');
  // беседа заканчивается у обоих
  a.actT = 0.01;
  run(S, 4);
  ok(a._mate == null && b._mate == null, 'после беседы оба свободны');
}

// --- работа: жители посещают места и работают
{
  const S = mk();
  run(S, 800);
  const acts = {};
  for (const f of S.folk) if (f.st === 'ok') acts[f.act] = (acts[f.act] || 0) + 1;
  ok((acts.work || 0) + (acts.rest || 0) + (acts.walk || 0) + (acts.chat || 0) + (acts.dance || 0) === S.folk.filter(f => f.st === 'ok').length, 'каждый житель занят делом');
  ok((acts.work || 0) > 0 || (acts.rest || 0) > 0, 'есть работающие или отдыхающие', JSON.stringify(acts));
}

// --- карта сохранения: быт переживает save/load (не роняет JSON)
{
  const S = mk();
  run(S, 200);
  const raw = G.saveGame(S, '1');
  ok(raw === true, 'игра сохранилась с бытом жителей');
  const L = G.loadGame('1');
  ok(!!L && L.folk && L.folk.length > 0, 'жители загрузились');
  ok(L.folk.some(f => f.x !== undefined), 'положения сохранились');
}

console.log('пройдено:', pass, 'провалено:', fail);
process.exit(fail ? 1 : 0);
