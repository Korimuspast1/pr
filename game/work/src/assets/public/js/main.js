'use strict';
/* ===== Запуск и главный цикл ===== */
let acc = 0, lastT = 0, lastUi = 0, autoSave = 0;
function startGame(S) {
  UI.S = S; RN.S = S; RN.sel = null; RN.place = null; UI.sheet = null; UI.shownOver = false; UI.paused = false; UI.speed = 1; UI.toasts = []; UI.tab = 'map';
  $('menu').classList.add('hidden'); $('create').classList.add('hidden'); $('modal').classList.add('hidden'); $('hud').classList.remove('hidden'); $('sheet').classList.add('hidden');
  $('fade').classList.remove('on'); DEMO.S = null; buildTerrain(S); RN.minimap = $('mm'); RN.cam.z = 3; const c = S.cities.find(c => c.owner === 0); centerOn(c.x + 1, c.y + 1);
  calcRates(S); calcScore(S); renderTabs(); updateHud();
  info(S, 0, 'Добро пожаловать!', 'Следуйте плану развития слева.', 'info');
}
/* ----- фон меню: живые пиксельные сцены игры ----- */
const DEMO = { S: null, acc: 0, next: 0, last: 0, ready: false };
function initDemo() {
  try {
    const S = newGame({ type: ['continent', 'continents', 'islands', 'lakes'][(Math.random() * 4) | 0], size: 's', bots: 3, diff: 'hard', win: 'caps', target: 0, nation: (Math.random() * 8) | 0 });
    S.players.forEach(p => p.human = false); for (let i = 0; i < 1500; i++) tick(S, 0.25);
    DEMO.S = S; DEMO.ready = false; DEMO.next = 0; buildTerrain(S); RN.S = S;
  } catch (e) { DEMO.S = null; }
}
function demoScene(S) {
  const pts = [];
  for (const c of S.cities) if (c.owner >= 0) pts.push([c.x + 1, c.y + 1, 4.4]);
  for (const a of S.armies) if (!a.ship) pts.push([a.x, a.y, a.fight ? 6 : 4.6], [a.x, a.y, a.fight ? 6 : 4.6]);
  for (const s of S.ships) pts.push([s.x, s.y, 5]);
  for (const b of S.buildings) if (b.owner >= 0 && (b.type === 'port' || b.type === 'barracks' || b.type === 'mine')) pts.push([b.x, b.y, 5]);
  if (!pts.length) pts.push([S.W / 2, S.H / 2, 3]);
  const p = pts[(Math.random() * pts.length) | 0]; RN.cam.z = p[2]; centerOn(p[0], p[1]); DEMO.drift = { x: (Math.random() - 0.5) * 6, y: (Math.random() - 0.5) * 4 };
}
function menuFrame(now) {
  const S = DEMO.S; if (!S) { frame(null, now); if (!DEMO.S && now - DEMO.last > 1500) { DEMO.last = now; initDemo(); } return; }
  const dt = Math.min(0.1, (now - (DEMO.t || now)) / 1000); DEMO.t = now; DEMO.acc += dt;
  while (DEMO.acc >= 0.25) { tick(S, 0.25); DEMO.acc -= 0.25; }
  if (S.over) { DEMO.S = null; return; }
  if (now > DEMO.next) {
    const f = $('fade'); if (!DEMO.ready) { demoScene(S); DEMO.ready = true; DEMO.next = now + 7000; f.classList.remove('on'); }
    else { f.classList.add('on'); DEMO.next = now + 700; DEMO.ready = false; }
  }
  if (DEMO.drift) { RN.cam.x += DEMO.drift.x * dt; RN.cam.y += DEMO.drift.y * dt; }
  frame(S, now);
}
function loop(now) {
  requestAnimationFrame(loop);
  const S = UI.S; if (!S) { menuFrame(now); return; }
  const dt = Math.min(0.1, (now - lastT) / 1000); lastT = now;
  if (!UI.paused && !S.over && $('modal').classList.contains('hidden')) {
    acc += dt * UI.speed; let n = 0;
    while (acc >= 0.25 && n < 40) { tick(S, 0.25); acc -= 0.25; n++; }
    if (n >= 40) acc = 0;
  }
  frame(S, now);
  if (now - lastUi > 250) { lastUi = now; updateHud(); }
  if (S.time - autoSave > 60 && !S.over) { autoSave = S.time; saveGame(S); }
  if (S.over && !UI.shownOver) { UI.shownOver = true; const w = S.over.winner, won = w === 0; localStorage.removeItem('kc_save'); showModal(won ? '★ Победа!' : 'Поражение', `<div class="${won ? 'gd' : 'bad'}" style="font-size:20px">${won ? 'Вы победили!' : (w >= 0 ? 'Победил ' + S.players[w].name : 'Игра окончена')}</div><div class="dim">${S.over.why}</div><div style="margin-top:8px">Ваши очки: <b class="gl">${me().score}</b> (хозяйство ${me().eco}, война ${me().mil})</div>`, btn('tomenu', 'В главное меню', 'gold')); }
}
window.addEventListener('load', () => {
  initIcons(); RN.cv = $('cv'); RN.ctx = RN.cv.getContext('2d'); resize(); initInput();
  window.addEventListener('resize', () => { resize(); clampCam(); });
  $('contBtn').classList.toggle('hidden', !localStorage.getItem('kc_save'));
  initDemo();
  document.fonts && document.fonts.load('16px PixelRU');
  requestAnimationFrame(loop);
});
document.addEventListener('visibilitychange', () => { if (document.hidden && UI.S && !UI.S.over) saveGame(UI.S); });
document.addEventListener('contextmenu', e => e.preventDefault());
