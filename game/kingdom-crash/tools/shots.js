// Проверка интерфейса в браузере: проходит основные экраны, собирает ошибки консоли и делает снимки.
// node tools/shots.js [ширина высота] [префикс]
const { chromium } = require((process.env.KC_TOOLS || process.env.HOME + '/.cache/kc-tools') + '/node_modules/playwright');
const path = require('path'), url = 'file://' + path.resolve(__dirname, '../www/index.html'), out = process.env.OUT || '/tmp/shots';
const W = +process.argv[2] || 390, H = +process.argv[3] || 844, pre = process.argv[4] || 'a';
(async () => {
  const b = await chromium.launch({ args: ['--no-sandbox'] });
  const ctx = await b.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 2, hasTouch: true, isMobile: true });
  const p = await ctx.newPage(); const errs = [];
  p.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errs.push(m.type() + ': ' + m.text()); });
  p.on('pageerror', e => errs.push('pageerror: ' + e.message + ' @ ' + (e.stack || '').split('\n').slice(1, 3).join(' | ')));
  let n = 0; const shot = async name => { n++; await p.screenshot({ path: `${out}/${pre}${String(n).padStart(2, '0')}_${name}.png` }); };
  const tap = async sel => { await p.tap(sel); await p.waitForTimeout(150); };
  await p.goto(url); await p.waitForTimeout(3000); await shot('menu');
  await tap('[data-act="settings"]'); await shot('settings'); await tap('[data-act="toggle:sfx"]'); await tap('[data-act="toggle:sfx"]'); await tap('[data-act="closemodal"]');
  await tap('[data-act="ach"]'); await shot('ach'); await tap('[data-act="closemodal"]');
  await tap('[data-act="records"]'); await shot('records'); await tap('[data-act="closemodal"]');
  await tap('[data-act="howto"]'); await shot('howto'); await tap('[data-act="closemodal"]');
  await tap('[data-act="newgame"]'); await tap('[data-act="cset:nation=3"]'); await tap('[data-act="cset:size=s"]'); await shot('create');
  await p.evaluate(() => document.getElementById('create').scrollTo(0, 9999)); await p.waitForTimeout(100); await shot('create_bottom');
  await tap('[data-act="start"]'); await p.waitForTimeout(2000); await shot('game');
  // ресурсы для проверки построек
  await p.evaluate(() => { const s = UI.S.players[0].stock; for (const k in s) s[k] = 5000; for (const k of ITEM_KEYS) s[k] = 120; });
  await tap('[data-act="tab:build"]'); await shot('tab_build'); await tap('[data-act="place:farm"]'); await shot('place_farm');
  // ставим здания через игровую логику на подходящие клетки
  const built = await p.evaluate(() => {
    const S = UI.S, res = {};
    for (const t of ['farm', 'mill', 'sawmill', 'mine', 'barracks', 'workshop', 'forge', 'stable', 'market', 'tavern', 'hospital', 'academy', 'siege', 'tower', 'wall', 'port']) {
      let done = false;
      for (let i = 0; i < S.W * S.H && !done; i++) { const x = i % S.W, y = (i / S.W) | 0; if (S.terr[i] === 0 && !siteOk(S, 0, t, x, y)) { done = !doBuild(S, 0, t, x, y); } }
      res[t] = done;
    }
    recomputeAll(); return res;
  });
  console.log('built', JSON.stringify(built));
  await tap('[data-act="cancelplace"]'); await tap('[data-act="bpage:1"]'); await shot('tab_build2');
  const openB = async t => { const id = await p.evaluate(t => { const b = UI.S.buildings.find(q => q.owner === 0 && q.type === t); if (!b) return null; ACT.selbld(b.id); return b.id; }, t); await p.waitForTimeout(350); return id; };
  for (const t of ['farm', 'mill', 'barracks', 'workshop', 'tavern', 'academy', 'port', 'tower', 'hospital', 'mine']) { if (await openB(t) != null) await shot('bld_' + t); }
  // обучение, наём, рецепт
  const bid = await openB('barracks'); if (bid != null) { await tap(`[data-act="train:${bid}:militia"]`); await tap(`[data-act="train:${bid}:spear"]`); await shot('train'); }
  const tid = await openB('tavern'); if (tid != null) { await tap(`[data-act="hire:${tid}:militia"]`); await p.waitForTimeout(400); await shot('hired'); }
  const wid = await openB('workshop'); if (wid != null) { await tap(`[data-act="recipe:${wid}:spear"]`); await tap(`[data-act="prod:${wid}"]`); await shot('recipe'); await tap(`[data-act="prod:${wid}"]`); }
  await tap('[data-act="closesheet"]');
  for (const [tab, acts] of [['city', ['cities']], ['army', ['armies', 'taverns', 'barr']], ['pol', ['dip', 'trade', 'eco', 'ult', 'rate']], ['fleet', ['ports', 'ships']], ['map', ['stock', 'plan', 'mm']]]) {
    await tap(`[data-act="tab:${tab}"]`); for (const a of acts) { await tap(`#bar [data-act="${a}"]`); await p.waitForTimeout(350); await shot(tab + '_' + a); }
  }
  await tap('#bar [data-act="mm"]'); await tap('[data-act="closesheet"]');
  // армия
  const aid = await p.evaluate(() => { const a = UI.S.armies.find(q => q.owner === 0); if (!a) return null; ACT.selarmy(a.id); return a.id; }); await p.waitForTimeout(350); if (aid != null) await shot('army');
  // город (свой и вольный)
  await p.evaluate(() => { ACT.selcity(capitalOf(UI.S, 0).id); }); await p.waitForTimeout(350); await shot('city');
  const fc = await p.evaluate(() => { const c = UI.S.cities.find(q => q.owner < 0); if (!c) return null; ACT.selcity(c.id); return c.id; }); await p.waitForTimeout(350); if (fc != null) await shot('freecity');
  await tap('[data-act="closesheet"]');
  // письма: предложение, событие с выбором, ультиматум
  await p.evaluate(() => { const S = UI.S; offer(S, 1, 0, 'trade'); offer(S, 1, 0, 'ult'); addMsg(S, 0, { type: 'econ', kind: 'event', ev: 'fair', title: 'Ярмарка', text: EVENTS.find(e => e.id === 'fair').text, exp: S.time + DAY }); });
  await p.waitForTimeout(400); await shot('hud_mail'); await tap('[data-act="inbox"]'); await shot('inbox');
  const pausedBefore = await p.evaluate(() => UI.paused);
  await tap('[data-act="inboxtab:war"]'); await shot('inbox_war'); await tap('[data-act="inboxtab:all"]');
  const mid = await p.evaluate(() => UI.S.msgs.find(m => m.kind === 'trade' && !m.done).id); await tap(`[data-act="answer:${mid}:1"]`); await shot('inbox_answered');
  await tap('[data-act="closemodal"]');
  console.log('pause before/after inbox:', pausedBefore, await p.evaluate(() => UI.paused), 'modal:', await p.evaluate(() => !!UI.modal));
  await tap('#burger'); await shot('menu_modal'); await tap('#modal [data-act="settings"]'); await shot('settings_ingame'); await tap('#modal [data-act="closemodal"]'); await shot('menu_modal_back'); await tap('#modal [data-act="closemodal"]');
  // всплывающие заметки
  await p.evaluate(() => { const S = UI.S; S.toast.push({ title: 'Богатый урожай', text: 'Амбары полны: +90 еды.', t: S.time, good: true, ev: 1 }); say('Не хватает ресурсов', false); }); await p.waitForTimeout(400); await shot('toasts');
  // время идёт?
  const t1 = await p.evaluate(() => UI.S.time); await p.waitForTimeout(1500); const t2 = await p.evaluate(() => UI.S.time); console.log('time advances:', t1.toFixed(2), '->', t2.toFixed(2));
  // сохранение и загрузка
  await tap('[data-act="tab:map"]'); await tap('#bar [data-act="save"]'); await p.waitForTimeout(300);
  console.log('hasSave:', await p.evaluate(() => hasSave()), 'size:', await p.evaluate(() => localStorage.getItem('kc_save').length));
  // конец игры
  await p.evaluate(() => { UI.S.over = { winner: 0, why: 'Набрано 30000 очков' }; }); await p.waitForTimeout(600); await shot('gameover');
  console.log('save after over:', await p.evaluate(() => hasSave()), 'records:', await p.evaluate(() => localStorage.getItem('kc_rec')));
  await tap('#modal [data-act="tomenu"]'); await p.waitForTimeout(800); await shot('menu_after');
  await tap('#menu [data-act="records"]'); await shot('records2'); await tap('#modal [data-act="closemodal"]');
  await tap('#menu [data-act="ach"]'); await shot('ach2'); await tap('#modal [data-act="closemodal"]');
  console.log('errors:', JSON.stringify(errs, null, 1));
  await b.close();
})().catch(e => { console.log('ERR', e.message); process.exit(1); });
