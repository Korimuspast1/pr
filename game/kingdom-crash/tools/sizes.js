// Проверка раскладки на маленьком экране и в альбомной ориентации.
const { chromium } = require((process.env.KC_TOOLS || process.env.HOME + '/.cache/kc-tools') + '/node_modules/playwright');
const path = require('path'), url = 'file://' + path.resolve(__dirname, '../www/index.html'), out = process.env.OUT || '/tmp/shots';
(async () => {
  const b = await chromium.launch({ args: ['--no-sandbox'] });
  for (const [W, H, pre] of [[320, 568, 's'], [844, 390, 'l'], [412, 915, 'b']]) {
    const ctx = await b.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 2, hasTouch: true, isMobile: true });
    const p = await ctx.newPage(); const errs = [];
    p.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errs.push(m.text()); }); p.on('pageerror', e => errs.push('pageerror: ' + e.message));
    const tap = async sel => { await p.tap(sel); await p.waitForTimeout(150); };
    await p.goto(url); await p.waitForTimeout(1800); await p.screenshot({ path: `${out}/${pre}1_menu.png` });
    await tap('#menu [data-act="newgame"]'); await p.screenshot({ path: `${out}/${pre}2_create.png` });
    await tap('[data-act="cset:size=s"]'); await p.evaluate(() => { const c = document.getElementById('create'); c.scrollTop = c.scrollHeight; }); await p.waitForTimeout(100);
    await tap('[data-act="start"]'); await p.waitForTimeout(1500); await p.screenshot({ path: `${out}/${pre}3_game.png` });
    await p.evaluate(() => { const s = UI.S.players[0].stock; for (const k in s) s[k] = 12345; });
    await tap('[data-act="tab:build"]'); await tap('#bar [data-act="place:barracks"]'); await p.screenshot({ path: `${out}/${pre}4_build.png` }); await tap('#sheet [data-act="cancelplace"]');
    await tap('[data-act="tab:pol"]'); await tap('#bar [data-act="dip"]'); await p.screenshot({ path: `${out}/${pre}5_dip.png` });
    await tap('#inboxBtn'); await p.screenshot({ path: `${out}/${pre}6_inbox.png` }); await tap('#modal [data-act="closemodal"]');
    await tap('#burger'); await p.screenshot({ path: `${out}/${pre}7_menu.png` }); await tap('#modal [data-act="ach"]'); await p.screenshot({ path: `${out}/${pre}8_ach.png` });
    // ничего не должно вылезать за экран по ширине
    const over = await p.evaluate(() => { const r = []; for (const el of document.querySelectorAll('#hud *, #modal *')) { const q = el.getBoundingClientRect(); if (q.width && (q.right > innerWidth + 1 || q.left < -1)) r.push((el.id || el.className || el.tagName) + ':' + Math.round(q.left) + '..' + Math.round(q.right)); } return r.slice(0, 8); });
    console.log(W + 'x' + H, 'errors:', errs.length, errs.slice(0, 3), 'overflow:', JSON.stringify(over));
    await ctx.close();
  }
  await b.close();
})().catch(e => { console.log('ERR', e.message); process.exit(1); });
