// Проверка сценариев в браузере: нажатия на карту, стройка, приказы, сохранение и загрузка, кнопка «Назад», сбои хранилища.
const { chromium } = require((process.env.KC_TOOLS || process.env.HOME + '/.cache/kc-tools') + '/node_modules/playwright');
const path = require('path'), url = 'file://' + path.resolve(__dirname, '../www/index.html'), out = process.env.OUT || '/tmp/shots';
let fails = 0; const ok = (c, m) => { if (!c) { fails++; console.log('  FAIL: ' + m); } else console.log('  ok: ' + m); };
(async () => {
  const b = await chromium.launch({ args: ['--no-sandbox'] });
  const ctx = await b.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, hasTouch: true, isMobile: true });
  const p = await ctx.newPage(); const errs = [];
  p.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errs.push(m.type() + ': ' + m.text()); });
  p.on('pageerror', e => errs.push('pageerror: ' + e.message + ' @ ' + (e.stack || '').split('\n').slice(1, 3).join(' | ')));
  const tap = async sel => { await p.tap(sel); await p.waitForTimeout(120); };
  const ev = f => p.evaluate(f);
  const tile = async (tx, ty) => { const q = await p.evaluate(([tx, ty]) => { centerOn(tx, ty + 2); const x = (tx * TS - RN.cam.x) * RN.cam.z, y = (ty * TS - RN.cam.y) * RN.cam.z, el = document.elementFromPoint(x, y); return { x, y, el: el ? el.id || el.className : '' }; }, [tx, ty]); if (q.el !== 'cv') console.log('  (нажатие попало не в карту: ' + q.el + ')'); await p.touchscreen.tap(q.x, q.y); await p.waitForTimeout(200); };
  await p.goto(url); await p.waitForTimeout(1500);
  ok(await ev(() => window.__kcBack()) === false, 'Назад в главном меню: приложение можно закрыть');
  await tap('#menu [data-act="howto"]'); ok(await ev(() => window.__kcBack()) === true && await ev(() => !UI.modal), 'Назад закрывает окно в меню');
  await tap('#menu [data-act="newgame"]'); ok(await ev(() => window.__kcBack()) === true && await ev(() => !document.getElementById('menu').classList.contains('hidden')), 'Назад с экрана новой игры ведёт в меню');
  await tap('#menu [data-act="newgame"]'); await tap('[data-act="cset:size=s"]'); await tap('[data-act="cset:bots=2"]'); await tap('[data-act="start"]'); await p.waitForTimeout(1500);
  ok(await ev(() => !!UI.S && UI.S.players.length === 3), 'игра началась, соперников 2');
  await ev(() => { const s = UI.S.players[0].stock; for (const k in s) s[k] = 3000; });
  // --- стройка нажатием на карту ---
  await tap('[data-act="tab:build"]'); await tap('#bar [data-act="place:farm"]');
  const spot = await ev(() => { const S = UI.S, c = capitalOf(S, 0); let best = null, bd = 1e9; for (let i = 0; i < S.W * S.H; i++) { const x = i % S.W, y = (i / S.W) | 0; if (S.terr[i] === 0 && !siteOk(S, 0, 'farm', x, y)) { const d = Math.hypot(x - c.x, y - c.y); if (d > 2.5 && d < bd) { bd = d; best = { x, y }; } } } return best; });
  ok(!!spot, 'есть место под ферму');
  await tile(spot.x + 0.5, spot.y + 0.5);
  ok(await ev(() => RN.place && RN.place.x) === spot.x, 'нажатие выбрало клетку');
  ok(await ev(() => /Место подходит/.test(document.getElementById('sheet').textContent)), 'окно пишет «Место подходит»');
  await p.screenshot({ path: out + '/f1_place.png' });
  await tap('#sheet [data-act="confirmplace"]');
  ok(await ev(() => UI.S.buildings.filter(q => q.owner === 0 && q.type === 'farm').length) === 1, 'ферма построена');
  ok(await ev(() => !!RN.place && RN.place.x === null), 'режим стройки остался включён для следующей постройки');
  await tile(spot.x + 0.5, spot.y + 0.5);
  ok(await ev(() => /Занято/.test(document.getElementById('sheet').textContent)), 'занятая клетка: «Занято»');
  ok(await ev(() => window.__kcBack()) === true && await ev(() => !RN.place && !UI.sheet), 'Назад выключает режим стройки');
  // стена линией
  await tap('#bar [data-act="bpage:1"]'); await tap('#bar [data-act="place:wall"]');
  const line = await ev(() => { const S = UI.S; for (let y = 2; y < S.H - 2; y++) for (let x = 2; x < S.W - 6; x++) { let good = true; for (let k = 0; k < 4; k++) if (S.terr[y * S.W + x + k] !== 0 || siteOk(S, 0, 'wall', x + k, y)) { good = false; break; } if (good) return { x, y }; } return null; });
  ok(!!line, 'есть место под стену из 4 клеток');
  await tile(line.x + 0.5, line.y + 0.5); await tile(line.x + 3.5, line.y + 0.5);
  ok(await ev(() => /Подходит клеток: 4 из 4/.test(document.getElementById('sheet').textContent)), 'линия из 4 клеток распознана');
  await p.screenshot({ path: out + '/f2_wall.png' });
  await tap('#sheet [data-act="confirmplace"]');
  ok(await ev(() => UI.S.buildings.filter(q => q.owner === 0 && q.type === 'wall').length) === 4, 'построено 4 клетки стены');
  await tap('#sheet [data-act="cancelplace"]');
  // --- выбор здания и армии нажатием, приказ на поход ---
  await tile(spot.x + 0.5, spot.y + 0.5);
  ok(await ev(() => RN.sel && RN.sel.k) === 'bld', 'нажатие на ферму открывает её окно');
  const cap = await ev(() => { const c = capitalOf(UI.S, 0); return { x: c.x, y: c.y }; });
  await tile(cap.x + 1, cap.y + 1); ok(await ev(() => RN.sel && RN.sel.k) === 'city', 'нажатие на город открывает его окно');
  await tap('#sheet [data-act^="upgrade:"]'); ok(await ev(() => capitalOf(UI.S, 0).level) === 2, 'город улучшен кнопкой');
  const arm = await ev(() => { const S = UI.S, c = capitalOf(S, 0), pt = freeTileNear(S, c.x, c.y + 3, 4, null); const a = newArmy(S, 0, pt.x + 0.5, pt.y + 0.5, [mkSquad('militia')]); return { id: a.id, x: a.x, y: a.y }; });
  await p.waitForTimeout(400); await tile(arm.x, arm.y);
  ok(await ev(() => RN.sel && RN.sel.k) === 'army', 'нажатие на армию выбирает её');
  const dest = await ev(() => { const S = UI.S, a = S.armies.find(q => q.owner === 0), r = reachTiles(S, 0, Math.floor(a.x), Math.floor(a.y), 40, 6); const t = r[r.length - 1]; return t; });
  await tile(dest.x + 0.5, dest.y + 0.5);
  ok(await ev(() => { const a = UI.S.armies.find(q => q.owner === 0); return a.path.length > 0 || a.st === 'move'; }), 'нажатие на землю отправляет армию в поход');
  await p.screenshot({ path: out + '/f3_move.png' });
  // нажатие за краем карты
  await ev(() => { RN.cam.x = -60; RN.cam.y = -60; }); await p.touchscreen.tap(20, 300); await p.waitForTimeout(150);
  ok(errs.length === 0, 'нажатие за краем карты не вызывает ошибок');
  ok(await ev(() => window.__kcBack()) === true, 'Назад в игре без окон открывает меню'); ok(await ev(() => UI.modal && UI.modal.kind) === 'menu', 'открыто меню паузы');
  const t0 = await ev(() => UI.S.time); await p.waitForTimeout(700); ok(await ev(() => UI.S.time) === t0, 'при открытом окне время стоит');
  await ev(() => window.__kcBack()); await p.waitForTimeout(700); ok(await ev(() => UI.S.time) > t0, 'после закрытия окна время идёт');
  ok(await ev(() => UI.paused) === false, 'пауза игрока не включилась сама');
  // пауза и скорость
  await tap('#bar [data-act="pause"]'); const t1 = await ev(() => UI.S.time); await p.waitForTimeout(600); ok(await ev(() => UI.S.time) === t1, 'ПАУЗА останавливает время');
  await tap('#inboxBtn'); await tap('#modal [data-act="inboxtab:war"]'); await tap('#modal [data-act="closemodal"]'); ok(await ev(() => UI.paused) === true, 'пауза сохраняется после писем (раньше сбрасывалась или залипала)');
  await tap('#bar [data-act="pause"]'); await tap('#bar [data-act="speed"]'); await tap('#bar [data-act="speed"]'); ok(await ev(() => UI.speed) === 4, 'скорость x4');
  const t2 = await ev(() => UI.S.time); await p.waitForTimeout(1000); const dt = await ev(() => UI.S.time) - t2; ok(dt > 2.5 && dt < 5.5, 'x4: за секунду прошло ' + dt.toFixed(2) + ' с игры');
  // предложение об отступлении
  await ev(() => { const S = UI.S, a = S.armies.find(q => q.owner === 0); a.pk = 10; S.prompts.push({ id: a.id }); }); await p.waitForTimeout(500);
  ok(await ev(() => UI.modal && UI.modal.kind) === 'prompt', 'показано окно «Тяжёлые потери»'); await p.screenshot({ path: out + '/f4_prompt.png' });
  await tap('#modal [data-act^="retreatno"]'); ok(await ev(() => !UI.modal && UI.S.prompts.length === 0), 'окно закрыто, запрос снят');
  // --- сохранение, выход, продолжение ---
  await tap('#burger'); await tap('#modal [data-act="asktomenu"]'); await p.screenshot({ path: out + '/f5_confirm.png' }); await tap('#modal [data-act="tomenu"]'); await p.waitForTimeout(500);
  ok(await ev(() => !UI.S && hasSave()), 'выход в меню сохранил партию');
  ok(await ev(() => !document.getElementById('contBtn').classList.contains('hidden')), 'кнопка ПРОДОЛЖИТЬ видна');
  await p.screenshot({ path: out + '/f6_menu_cont.png' });
  const before = await ev(() => { const d = JSON.parse(localStorage.getItem('kc_save')); return { time: d.time, b: d.buildings.length, a: d.armies.length }; });
  await tap('#menu [data-act="continue"]'); await p.waitForTimeout(1200);
  const after = await ev(() => ({ time: UI.S.time, b: UI.S.buildings.length, a: UI.S.armies.length, lvl: capitalOf(UI.S, 0).level }));
  ok(after.b === before.b && after.a === before.a && after.lvl === 2 && Math.abs(after.time - before.time) < 8, 'партия загружена: зданий ' + after.b + ', армий ' + after.a + ', уровень столицы ' + after.lvl);
  ok(await ev(() => UI.S.msgs.filter(m => m.title === 'Добро пожаловать!').length) === 1, 'приветствие не дублируется при загрузке');
  await p.screenshot({ path: out + '/f7_loaded.png' });
  // новая игра поверх сохранения спрашивает подтверждение
  await tap('#burger'); await tap('#modal [data-act="asktomenu"]'); await tap('#modal [data-act="tomenu"]'); await p.waitForTimeout(300);
  await tap('#menu [data-act="newgame"]'); ok(await ev(() => !!UI.modal), 'новая игра при живом сохранении: подтверждение'); await tap('#modal [data-act="closemodal"]');
  // повреждённое сохранение
  await ev(() => localStorage.setItem('kc_save', '{"v":2,"players":"<img src=x onerror=alert(1)>","pad":"' + 'x'.repeat(200) + '"}'));
  await tap('#menu [data-act="continue"]'); await p.waitForTimeout(300);
  ok(await ev(() => /не читается/.test(document.getElementById('modal').textContent)), 'повреждённое сохранение: понятное сообщение'); await p.screenshot({ path: out + '/f8_badsave.png' });
  ok(await ev(() => !document.querySelector('#modal img[src="x"]')), 'текст из сохранения не попал в разметку');
  await tap('#modal [data-act="dropsave"]'); ok(await ev(() => !hasSave() && document.getElementById('contBtn').classList.contains('hidden')), 'сохранение удалено, кнопка скрыта');
  // хранилище недоступно или переполнено
  await ev(() => { Storage.prototype.setItem = function () { throw new Error('quota'); }; });
  await tap('#menu [data-act="newgame"]'); await tap('[data-act="cset:sandbox=1"]'); await tap('[data-act="cset:bots=0"]'); await p.screenshot({ path: out + '/f9_create_sandbox.png' }); await tap('[data-act="start"]'); await p.waitForTimeout(1500);
  ok(await ev(() => !!UI.S && UI.S.opts.sandbox && UI.S.players.length === 1), 'песочница без соперников запускается');
  await tap('#bar [data-act="save"]'); await p.waitForTimeout(400);
  ok(await ev(() => /Не удалось сохранить партию/.test(document.getElementById('toasts').textContent)), 'сбой записи: игрок видит сообщение, игра не падает');
  await p.screenshot({ path: out + '/f10_sandbox.png' });
  // имена из данных партии экранируются
  await ev(() => { UI.S.cities[0].name = '<img src=x onerror="window.__xss=1">'; ACT.selcity(0); }); await p.waitForTimeout(400);
  ok(await ev(() => !window.__xss && !document.querySelector('#sheet img[src="x"]') && /<img/.test(document.querySelector('#sheet .tt').textContent)), 'название города выводится как текст');
  console.log('errors:', JSON.stringify(errs, null, 1)); console.log(fails ? 'ПРОВАЛЕНО: ' + fails : 'ВСЕ СЦЕНАРИИ ПРОЙДЕНЫ');
  await b.close(); process.exit(fails || errs.length ? 1 : 0);
})().catch(e => { console.log('ERR', e.message); process.exit(1); });
