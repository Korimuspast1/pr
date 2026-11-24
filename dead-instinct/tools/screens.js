/* Реальные скриншоты игры в headless Chromium (Chrome for Testing из npm). */
const puppeteer = require('/tmp/nptest/node_modules/puppeteer-core');
const chromium = require('/tmp/nptest/node_modules/@sparticuz/chromium/build/index.js').default;
const path = require('path');
const OUT = path.join(__dirname, 'shots');
const URL = 'file://' + path.resolve(__dirname, '../www/index.html');
const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const browser = await puppeteer.launch({
    args: [...chromium.args, '--allow-file-access-from-files', '--no-sandbox', '--disable-dev-shm-usage', '--single-process', '--font-render-hinting=none', '--hide-scrollbars'],
    executablePath: await chromium.executablePath(), headless: 'shell',
    env: Object.assign({}, process.env, { LD_LIBRARY_PATH: '/tmp/al2023/lib:/tmp', FONTCONFIG_PATH: '/tmp/fonts', HOME: '/tmp' }),
    defaultViewport: { width: 412, height: 892, deviceScaleFactor: 2 }
  });
  const page = await browser.newPage();
  const errs = [];
  page.on('pageerror', e => errs.push('PAGEERROR: ' + e.message));
  page.on('console', m => { if (m.type() === 'error') errs.push('CONSOLE: ' + m.text()); });
  await page.goto(URL, { waitUntil: 'load' });
  await sleep(1200);
  const shot = async n => { await page.screenshot({ path: path.join(OUT, n + '.png') }); console.log('shot', n); };

  await shot('p1_menu');
  await page.evaluate(() => document.getElementById('menuNew').click());
  await sleep(500);
  await shot('p2_chapters');
  await page.evaluate(() => document.querySelector('#screen .ep:not(.locked)').click());
  await sleep(900);
  await shot('p3_cine');
  await page.evaluate(() => document.getElementById('cine').click());
  await sleep(700);
  await shot('p4_dialog');
  for (let i = 0; i < 10; i++) { await page.evaluate(() => document.getElementById('dialogue').click()); await sleep(160); }
  await sleep(600);
  await shot('p5_game');
  await page.evaluate(() => { const G = UI.G; if (G) { G.player.torch = true; } });
  await sleep(400);
  await shot('p6_torch');
  await page.evaluate(() => { const G = UI.G; if (G) { G.player.x = G.M.exit.x + 3; G.player.y = G.M.exit.y + 3; centerOn(G, G.player.x, G.player.y); } });
  await sleep(700);
  await shot('p7_goal');
  await page.evaluate(() => document.getElementById('invBtn').click());
  await sleep(400);
  await shot('p8_inventory');
  await page.evaluate(() => document.getElementById('invClose').click());
  await page.evaluate(() => document.getElementById('menuBtn').click());
  await sleep(400);
  await shot('p9_pause');
  await page.evaluate(() => document.getElementById('pResume').click());
  await sleep(200);
  // выбор
  await page.evaluate(() => { const G = UI.G; G.choice = CHOICES.ch2; });
  await sleep(500);
  await shot('p10_choice');
  await page.evaluate(() => { UI.G.choice = null; UI._choice = null; document.getElementById('choice').classList.add('hidden'); });
  // смерть
  await page.evaluate(() => { hurtPlayer(UI.G, 500, { kind: 'walker' }); });
  await sleep(800);
  await shot('p11_dead');
  console.log('ERRORS:', errs.length ? errs.join(' | ') : 'нет');
  await browser.close();
})().catch(e => { console.log('CRASH', e.message); process.exit(1); });
