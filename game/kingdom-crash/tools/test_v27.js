'use strict';
// Проверки 2.7.0: свайп-тосты, одна музыка, сборка отрядов одного типа,
// понятный захват территории, маршрут из точек для батальонов.
const fs = require('fs'), path = require('path');
const load = require('./load'), G = load(__dirname + '/../www/js');
let pass = 0, fail = 0;
const ok = (c, name, extra) => { if (c) pass++; else { fail++; console.log('FAIL:', name, extra === undefined ? '' : extra); } };
const mk = (o) => G.newGame(Object.assign({ type: 'continent', size: 's', bots: 3, diff: 'normal', win: 'score', target: 30000, nation: 0, seed: 777, free: 1, events: 1 }, o || {}));
const findLand = (S, x0, y0) => { for (let y = y0; y < S.H - 3; y++) for (let x = x0; x < S.W - 3; x++) { const t = S.ter[y * S.W + x]; if (!G.isWater(t) && t !== G.TR.MOUNT && S.occ[y * S.W + x] === -1) return { x, y }; } return null; };

// ===== Сборка отрядов одного типа =====
{
  const S = mk();
  const a = G.newArmy(S, 0, 30.5, 30.5, [G.mkSquad('militia'), G.mkSquad('archer'), G.mkSquad('militia')]);
  const mil = () => a.sq.filter(q => q.t === 'militia');
  const m0 = mil()[0], m1 = mil()[1];
  m0.s = m0.s.slice(0, 4); G.syncSq(m0);
  m1.s = m1.s.slice(0, 3); G.syncSq(m1);
  m0.w = 2; m1.w = 1;
  const n = G.mergeSameType(S, a);
  ok(n === 1, 'ополчение собрано (один род войск)', n);
  const ml = mil();
  ok(ml.length === 1 && ml[0].u === 7, 'ополченцы в одном отряде из 7', ml.length + ':' + (ml[0] && ml[0].u));
  ok(ml[0].w === 3, 'раненые не потеряны при сборке', ml[0].w);
  const ar = a.sq.filter(q => q.t === 'archer');
  ok(ar.length === 1 && ar[0].u === 10, 'полный отряд лучников не тронут', ar.length + ':' + (ar[0] && ar[0].u));
  // полные + полные — собирать нечего
  const S2 = mk({ seed: 778 });
  const a2 = G.newArmy(S2, 0, 30.5, 30.5, [G.mkSquad('spear'), G.mkSquad('spear')]);
  ok(G.mergeSameType(S2, a2) === 0, 'два полных копейщика остаются как есть');
  // раненые переживают разбиение по 10
  const S3 = mk({ seed: 779 });
  const a3 = G.newArmy(S3, 0, 30.5, 30.5, [G.mkSquad('militia'), G.mkSquad('militia'), G.mkSquad('militia')]);
  const q3 = a3.sq.filter(q => q.t === 'militia');
  q3.forEach((q, i) => { q.s = q.s.slice(0, 8); G.syncSq(q); q.w = i + 2; }); // 24 воина + 2+3+4 = 9 ран.
  const n3 = G.mergeSameType(S3, a3);
  const all = a3.sq.filter(q => q.t === 'militia');
  const wsum = all.reduce((s, q) => s + (q.w || 0), 0), usum = all.reduce((s, q) => s + q.u, 0);
  ok(n3 === 1 && usum === 24 && wsum === 9, '24 ополченца + 9 раненых: всё на месте', 'отрядов: ' + all.length + ' воинов: ' + usum + ' ран.: ' + wsum);
  // полководец не сливается
  const S4 = mk({ seed: 780 });
  const cmd = G.mkSquad('cmd'); cmd.cls = 'cav';
  const a4 = G.newArmy(S4, 0, 30.5, 30.5, [cmd, G.mkSquad('archer'), G.mkSquad('archer')]);
  G.mergeSameType(S4, a4);
  ok(a4.sq.some(q => q.t === 'cmd'), 'полководец не тронут');
  ok(a4.sq.filter(q => q.t === 'archer').length === 2, 'лучники 10+10: компактно, не сливаются в третье');
}

// ===== Точки маршрута для батальонов =====
{
  const S = mk();
  const p = findLand(S, 4, 4);
  ok(!!p, 'стартовая клетка найдена');
  const freeSpot = (S, p0, rMin, rMax) => { for (let r = rMin; r <= rMax; r++) for (let yy = p0.y - r; yy <= p0.y + r; yy++) for (let xx = p0.x - r; xx <= p0.x + r; xx++) { if (yy < 1 || xx < 1 || yy >= S.H - 1 || xx >= S.W - 1) continue; if (Math.max(Math.abs(xx - p0.x), Math.abs(yy - p0.y)) !== r) continue; const t = S.ter[yy * S.W + xx]; if (G.isWater(t) || t === G.TR.MOUNT) continue; if (G.blockedFor(S, 0, xx, yy)) continue; if (S.comp[yy * S.W + xx] !== S.comp[p0.y * S.W + p0.x]) continue; return { x: xx, y: yy }; } return null; };
  const s1 = freeSpot(S, p, 5, 9), s2 = freeSpot(S, p, 2, 4), s3 = freeSpot(S, p, 5, 12);
  ok(s1 && s2 && s3, 'для маршрута нашлись свободные клетки', JSON.stringify([s1, s2, s3]));
  const a = G.newArmy(S, 0, p.x + 0.5, p.y + 0.5, [G.mkSquad('spear')]);
  const e1 = G.orderWaypoint(S, a, s1.x + 0.5, s1.y + 0.5);
  ok(!e1 && a.path.length > 0, 'первая точка сразу стала путём', e1);
  ok(!a.wp || a.wp.length === 0, 'очередь пуста после первой точки');
  const e2 = G.orderWaypoint(S, a, s2.x + 0.5, s2.y + 0.5);
  ok(!e2, 'вторая точка принята', e2);
  ok(a.wp && a.wp.length === 1, 'вторая точка стоит в очереди маршрута', a.wp && a.wp.length);
  const e3 = G.orderWaypoint(S, a, s3.x + 0.5, s3.y + 0.5);
  ok(!e3 && a.wp.length === 2, 'третья точка тоже в очереди', a.wp && (e3 || a.wp.length));
  // сохранение: маршрут переживает страницу
  ok(G.saveGame(S) !== false, 'игра сохранена');
  const L = G.loadGame();
  ok(!!L, 'сохранение читается', G.loadGame.error);
  const la = L && L.armies.find(x => x.id === a.id);
  ok(la && Array.isArray(la.wp) && la.wp.length === 2, 'маршрут из точек пережил сохранение', la && JSON.stringify(la.wp));
  G.dropSave();
  // дошли до первой точки — армия пошла ко второй из очереди
  const a1 = S.armies.find(q => q.id === a.id);
  a1.x = s1.x + 0.5; a1.y = s1.y + 0.5; // армия дошла до первой точки
  a1.path = []; a1.dest = null;
  G.tickMove(S, 0.001);
  ok(a1.wp.length === 1, 'первая точка маршрута взята в работу', a1.wp.length);
  ok(a1.st === 'move', 'армия идёт дальше по маршруту', a1.st);
  // новый приказ «простой клик» чистит маршрут
  const s4 = freeSpot(S, p, 1, 6);
  G.orderMove(S, a1, s4.x + 0.5, s4.y + 0.5);
  ok(a1.wp.length === 0, 'новый приказ с карты отменяет маршрут', a1.wp.length);
  // «Стоять» чистит маршрут
  a1.wp = [{ x: p.x + 5, y: p.y + 5 }];
  G.stopArmy(a1);
  ok(a1.wp.length === 0 && a1.path.length === 0, '«Стоять» чистит маршрут и путь');
  // за край карты — нельзя
  ok(!!G.orderWaypoint(S, a1, -5, -5), 'точка за краем карты отклонена');
}

// ===== Понятный захват территории =====
{
  const S = mk();
  const c0 = S.cities.find(c => c.owner === 0);
  ok(!!c0, 'у игрока есть город');
  G.flipCity(S, c0, 1);
  ok(c0.owner === 1, 'город перешёл врагу');
  ok(S.toast.some(t => t.title === 'Город потерян!' && t.bad), 'тост «Город потерян!» показан');
  ok(S.fx.some(f => f.k === 'cap' && f.big), 'большой эффект захвата на карте города');
  G.flipCity(S, c0, 0);
  ok(c0.owner === 0, 'город отвоёван');
  ok(S.toast.some(t => t.title === 'Город захвачен!' && t.good), 'тост «Город захвачен!» показан');
  // постройка игрока внутри вражеских границ → захват с тостом и эффектом
  const S2 = mk({ seed: 777 });
  const enemyCity = S2.cities.find(c => c.owner === 1) || S2.cities.find(c => c.owner > 0);
  const b1 = { id: S2.nid++, type: 'farm', x: enemyCity.x + 1, y: enemyCity.y + 1, owner: 0, hp: 10, mhp: 10, queue: [], lvl: 1 };
  S2.buildings.push(b1); S2.bmap.set(b1.id, b1);
  const fxB = S2.fx.length, ttB = S2.toast.length;
  G.recomputeTerr(S2);
  ok(b1.owner !== 0, 'постройка внутри вражеских границ перешла врагу', b1.owner);
  ok(S2.toast.length > ttB && S2.toast.some(t => t.title === 'Постройки захвачены!' && t.bad), 'тост о захвате построек');
  ok(S2.fx.length > fxB && S2.fx.some(f => f.k === 'cap'), 'эффект смены владельца на карте');
  // постройка врага внутри наших границ → отвоевана
  const ownCity = S2.cities.find(c => c.owner === 0);
  const b2 = { id: S2.nid++, type: 'farm', x: ownCity.x + 1, y: ownCity.y + 1, owner: 1, hp: 10, mhp: 10, queue: [], lvl: 1 };
  S2.buildings.push(b2); S2.bmap.set(b2.id, b2);
  G.recomputeTerr(S2);
  ok(b2.owner === 0, 'вражеская постройка внутри наших границ стала нашей', b2.owner);
  ok(S2.toast.some(t => t.title === 'Постройки под нашим флагом'), 'тост об отвоёванных постройках');
}

// ===== Статический контроль интерфейса и звука =====
{
  const css = fs.readFileSync(path.join(__dirname, '../www/style.css'), 'utf8');
  const toastRule = css.match(/\.toast\s*{[^}]*}/);
  ok(!!toastRule && /pointer-events:\s*auto/.test(toastRule[0]), 'тост получает касания (pointer-events:auto)');
  ok(!!toastRule && /touch-action:\s*none/.test(toastRule[0]), 'тост не прокручивает страницу при свайпе');
  ok(/\.toast \.tx\s*{/.test(css), 'у тоста есть кнопка закрытия');
  const ui = fs.readFileSync(path.join(__dirname, '../www/js/ui.js'), 'utf8');
  ok(ui.includes("btn('wp:' + a.id"), 'армия: кнопка режима маршрута');
  ok(ui.includes("btn('mergesq:' + a.id"), 'армия: кнопка сборки отрядов');
  ok(ui.includes("orderWaypoint(S, ownA, tx, ty)"), 'нажатие в режиме маршрута добавляет точку');
  const audio = fs.readFileSync(path.join(__dirname, '../www/js/audio.js'), 'utf8');
  ok(/trPlay\)\s*return;/.test(audio), 'лютня не запускается поверх саундтрека');
  ok(audio.includes('stopMusic(); // лютня уступает дорогу саундтреку'), 'саундтрек останавливает лютню');
  ok(audio.includes('if (ready && st.music) startMusic(); return false;'), 'стоп саундтрека возвращает лютню');
  ok(!audio.includes("addEventListener('ended', () => trackNext())"), 'автопереход по окончании трека отключён (песни не включаются сами)');
}

console.log('\nИтого v2.7.0: ' + pass + ' ок, ' + fail + ' ошибок');
process.exit(fail ? 1 : 0);
