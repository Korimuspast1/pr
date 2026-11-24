/* Soak-тест: каждый эпизод гоняется 3 минуты игрового времени с «ботом»,
   который ходит, бьёт, стреляет, ест и лечится. Ищем падения и проверяем поведение. */
const fs = require('fs'), path = require('path');
/* Тест логики не требует canvas: все offscreen-канвасы подменяются заглушкой. */
const fakeCanvas = () => ({
  width: 1, height: 1,
  getContext: () => new Proxy({ canvas: { width: 1, height: 1 }, createRadialGradient: () => ({ addColorStop() { } }), createLinearGradient: () => ({ addColorStop() { } }), measureText: () => ({ width: 10 }) }, { get: (t, k) => (k in t ? t[k] : () => { }), set: (t, k, v) => { t[k] = v; return true; } }),
  toDataURL: () => 'data:,', toBuffer: () => Buffer.alloc(0)
});
global.document = { createElement: () => fakeCanvas(), getElementById: () => null, addEventListener() { }, querySelectorAll: () => [] };
global.window = { innerWidth: 720, innerHeight: 1280, devicePixelRatio: 1, addEventListener() { } };
global.performance = { now: () => 0 };
const _ls = {};
global.localStorage = { getItem: k => (k in _ls ? _ls[k] : null), setItem: (k, v) => { _ls[k] = String(v); }, removeItem: k => { delete _ls[k]; } };
const ROOT = path.resolve(__dirname, '..');
const bundle = ['data.js', 'map.js', 'sprites.js', 'audio.js', 'story.js', 'game.js', 'render.js']
  .map(s => fs.readFileSync(path.join(ROOT, 'www/js', s), 'utf8').replace(/^'use strict';\s*/, '')).join('\n;\n');
const A = (0, eval)(bundle + '\n;({newGame,updatePlayer,updateZombies,updateBullets,updateGroup,updateWorld,updateObjectives,updateProps,attack,useItem,equip,interact,save,loadSave,clearSave,T,ITEM,ZK,throwBang,findPath,doorAt,walkable});');

function runChapter(ch, seconds, seed) {
  const G = A.newGame(ch, { seed });
  const rnd = mulberry32(seed + 1);
  const st = { ch, killed: 0, steps: 0, shotsFired: 0, errors: [], zombiesSeen: 0, maxNoise: 0, deaths: 0 };
  const dt = 1 / 60;
  const inp = { mx: 0, my: 0, sneak: false, sprint: false, danger: false, freeze: false };
  let dirT = 0, ang = rnd() * 6.28;
  for (let i = 0; i < seconds * 60; i++) {
    try {
      dirT -= dt;
      if (dirT <= 0) { dirT = 1 + rnd() * 3; ang = rnd() * 6.28; }
      inp.mx = Math.cos(ang); inp.my = Math.sin(ang);
      inp.sneak = rnd() < 0.3;
      inp.sprint = rnd() < 0.15;
      // пользовательские действия
      if (i % 90 === 0) {
        const keys = Object.keys(G.inventory).filter(k => G.inventory[k] > 0 && ['medkit', 'bandage', 'food', 'water', 'pills', 'battery'].includes(k));
        if (keys.length && rnd() < 0.5) A.useItem(G, keys[(rnd() * keys.length) | 0]);
      }
      if (i % 150 === 0) {
        const ws = ['bar', 'bat', 'knife', 'axe', 'pistol', 'shotgun', 'rifle'].filter(k => G.inventory[k] > 0);
        if (ws.length && rnd() < 0.6) A.equip(G, ws[(rnd() * ws.length) | 0]);
      }
      // атака ближайшего зомби
      let near = null, nd = 99;
      for (const z of G.zombies) { const d = Math.hypot(z.x - G.player.x, z.y - G.player.y); if (d < nd) { nd = d; near = z; } }
      if (near && nd < 2) {
        A.attack(G, near.x, near.y);
        if (G.player.weapon && A.ITEM[G.player.weapon].gun) st.shotsFired++;
      }
      if (i % 420 === 0 && rnd() < 0.5) A.throwBang(G, G.player.x + Math.cos(ang) * 4, G.player.y + Math.sin(ang) * 4);
      A.updatePlayer(G, dt, inp);
      A.updateZombies(G, dt);
      A.updateBullets(G, dt);
      A.updateGroup(G, dt);
      A.updateProps(G, dt);
      A.updateWorld(G, dt);
      A.updateObjectives(G);
      st.steps++;
      st.maxNoise = Math.max(st.maxNoise, G.player.noise);
      st.zombiesSeen = Math.max(st.zombiesSeen, G.zombies.length);
      if (G.player.dead) { st.deaths++; break; }
      if (G.over) break;
      if (!isFinite(G.player.x) || !isFinite(G.player.y)) { st.errors.push('NaN позиция игрока'); break; }
    } catch (e) {
      st.errors.push(e.message + ' @' + (e.stack || '').split('\n')[1]);
      break;
    }
  }
  st.killed = G.stats.killed;
  st.hp = Math.round(G.player.hp);
  st.time = Math.round(G.time);
  st.trust = Math.round(G.trust);
  st.alive = G.group.filter(n => !n.dead).length;
  st.inv = Object.entries(G.inventory).filter(([k, v]) => v > 0).map(([k, v]) => k + ':' + v).join(',');
  st.over = G.over ? G.over.kind : null;
  return st;
}

let fails = 0;
for (const ch of [1, 2, 3, 4, 5]) {
  for (const seed of [111, 222, 333]) {
    const st = runChapter(ch, 150, seed);
    const ok = st.errors.length === 0;
    if (!ok) fails++;
    console.log(`${ok ? 'OK  ' : 'FAIL'} эп.${ch} seed=${seed} t=${st.time}с hp=${st.hp} убито=${st.killed} выстрелов=${st.shotsFired} доверие=${st.trust} живых=${st.alive} шум=${st.maxNoise.toFixed(1)} зомби_max=${st.zombiesSeen} ${st.over || ''} ${st.errors.join('; ')}`);
  }
}
// проверка сохранения/загрузки
const G = A.newGame(2, { seed: 7 });
G.player.x = 10.5; G.player.y = 20.5; G.inventory.medkit = 3; G.trust = 77;
A.save(G);
const S = A.loadSave();
console.log('сохранение:', S ? `эп.${S.chapter} x=${S.player.x} медикаменты=${S.inventory.medkit} доверие=${S.trust}` : 'НЕТ');
if (!S) fails++;
// навигация: путь через полкарты
const G3 = A.newGame(1, { seed: 9 });
const p = A.findPath(G3.M, G3.player.x | 0, G3.player.y | 0, G3.M.exit.x, G3.M.exit.y, 30000, false);
console.log('путь до выхода:', p ? p.length + ' шагов' : 'НЕ НАЙДЕН');
if (!p) fails++;
// двери: закрытые непроходимы, открытые — проходимы
const d = G3.M.doors[0];
if (d) {
  d.open = 0; d.locked = false;
  const blocked = !A.walkable(G3.M, d.x, d.y);
  d.open = 1;
  const open = A.walkable(G3.M, d.x, d.y);
  console.log('двери: закрытая непроходима=' + blocked + ', открытая проходима=' + open);
  if (!blocked || !open) fails++;
}
// финал эпизода: телепорт к цели и к дому
function runToEnd(ch, seed) {
  const G = A.newGame(ch, { seed });
  const dt = 1 / 60, inp = { mx: 0, my: 0 };
  // выполнить сюжетные условия
  if (ch === 2 || ch === 3) {
    // собрать сюжетный лут телепортом
    for (const c of G.M.containers) {
      if (c.looted || !c.loot) continue;
      if (c.loot.some(i => i.k === 'medkit' || i.k === 'fuel')) { G.player.x = c.x + 0.5; G.player.y = c.y + 0.5; A.interact(G, c.x, c.y); }
    }
  }
  if (ch === 3) { G.flags.generatorOn = true; }
  if (ch === 5) { G.flags.journalFound = true; }
  const sh = G.M.safehouse;
  // сходим к цели и домой
  G.player.x = G.M.exit.x + 0.5; G.player.y = G.M.exit.y + 0.5;
  for (let i = 0; i < 120; i++) { A.updateObjectives(G); A.updateWorld(G, dt); }
  G.player.x = sh.x + 0.5; G.player.y = sh.y + 0.5;
  for (let i = 0; i < 120; i++) { A.updateObjectives(G); A.updateWorld(G, dt); }
  return G.over ? G.over.kind + (G.over.ending ? '/' + G.over.ending.n : '') : 'НЕ ЗАВЕРШЁН';
}
for (const ch of [1, 2, 3, 4, 5]) {
  const r = runToEnd(ch, 555);
  const ok = r.startsWith('chapter');
  if (!ok) fails++;
  console.log((ok ? 'OK  ' : 'FAIL') + ' финал эп.' + ch + ': ' + r);
}
console.log(fails ? '\nПРОВАЛОВ: ' + fails : '\nВСЁ ЧИСТО');
process.exit(fails ? 1 : 0);
