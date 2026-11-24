// Проверка визуала страха: ставим игрока рядом со стаей и снимаем кадр.
const path = require('path');
const CH = '/tmp/nptest/node_modules';
const chromium = require(CH + '/@sparticuz/chromium/build/index.js').default;
const puppeteer = require(CH + '/puppeteer-core');
process.env.LD_LIBRARY_PATH = '/tmp/al2023/lib:/tmp';
process.env.FONTCONFIG_PATH = '/tmp/fonts';
(async () => {
  const exe = await chromium.executablePath();
  const browser = await puppeteer.launch({ args: chromium.args, executablePath: exe, headless: 'shell' });
  const page = await browser.newPage();
  await page.setViewport({ width: 412, height: 892, deviceScaleFactor: 2 });
  const errs = [];
  page.on('pageerror', e => errs.push(e.message));
  await page.goto('file://' + path.resolve(__dirname, '../www/index.html'));
  await page.evaluate(() => { try { localStorage.clear(); } catch (e) {} document.getElementById('menuNew').click(); });
  await new Promise(r => setTimeout(r, 600));
  await page.evaluate(() => document.querySelector('#screen .ep:not(.locked)').click());
  await new Promise(r => setTimeout(r, 800));
  await page.evaluate(() => { const c = document.getElementById('cine'); if (c) c.click(); });
  await new Promise(r => setTimeout(r, 700));
  // проматываем вступление
  for (let i = 0; i < 12; i++) {
    await page.evaluate(() => { const d = document.getElementById('dialogue'); if (d && !d.classList.contains('hidden')) d.click(); });
    await new Promise(r => setTimeout(r, 90));
  }
  const info = await page.evaluate(() => {
    const G = UI.G; if (!G) return 'нет игры';
    // подводим стаю вплотную
    for (let i = 0; i < 6; i++) {
      const z = G.zombies[i]; if (!z) continue;
      z.x = G.player.x + 1.2 + (i % 3); z.y = G.player.y + 0.9 * (i - 2);
    }
    G.player.torch = false; G.player.hp = G.player.maxHp * 0.45;
    return 'ok';
  });
  await new Promise(r => setTimeout(r, 2600));
  // принудительно разгоняем страх для проверки отрисовки
  await page.evaluate(() => { UI.G.fear = 0.92; });
  await new Promise(r => setTimeout(r, 300));
  const fear = await page.evaluate(() => UI.G && UI.G.fear);
  await page.screenshot({ path: path.resolve(__dirname, 'shots/p12_fear.png') });
  console.log('состояние:', info, '| страх:', fear, '| ошибки:', errs.length ? errs.slice(0, 3) : 'нет');
  await browser.close();
})();
