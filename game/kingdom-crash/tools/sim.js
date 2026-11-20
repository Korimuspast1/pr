'use strict';
// Автопрогон партий бот-против-бота с проверкой инвариантов.
const load = require('./load');
const dir = process.argv[2], N = +process.argv[3] || 6, TICKS = +process.argv[4] || 6000;
const G = load(dir);
const types = ['continent', 'continents', 'islands', 'lakes'], sizes = ['s', 'm', 'l'];
let fails = 0; const stats = { games: 0, over: 0, ms: 0, ticks: 0, issues: {} };
const issue = (k, d) => { stats.issues[k] = (stats.issues[k] || 0) + 1; if (stats.issues[k] <= 2) console.log('  ISSUE', k, d || ''); };
for (let g = 0; g < N; g++) {
  const o = { type: types[g % 4], size: sizes[(g >> 2) % 3], bots: 1 + (g * 5) % 6, diff: ['easy', 'normal', 'hard', 'mixed'][g % 4], win: g % 2 ? 'caps' : 'score', target: 30000, nation: g % 8, seed: 1000 + g * 7919 };
  let S, t0 = Date.now();
  try {
    S = G.newGame(o);
    // столицы: расстояния
    let md = 1e9; for (let i = 0; i < S.cities.length; i++) for (let j = i + 1; j < S.cities.length; j++) md = Math.min(md, G.dist(S.cities[i].x, S.cities[i].y, S.cities[j].x, S.cities[j].y));
    if (S.cities.length > 1 && md < G.CITY_MIN) issue('capitals_too_close', JSON.stringify(o) + ' min=' + md.toFixed(1));
    S.players.forEach(p => p.human = false);
    for (let i = 0; i < TICKS && !S.over; i++) {
      G.tick(S, 0.25);
      if (i % 40 === 0) {
        for (const a of S.armies) {
          if (!isFinite(a.x) || !isFinite(a.y)) { issue('army_nan', 't=' + S.time); continue; }
          if (a.x < 0 || a.y < 0 || a.x >= S.W || a.y >= S.H) { issue('army_oob', `t=${S.time} x=${a.x} y=${a.y}`); continue; }
          if (!a.ship && G.isWater(S.ter[Math.floor(a.y) * S.W + Math.floor(a.x)])) issue('army_on_water', 't=' + S.time);
          if (!a.ship && S.ter[Math.floor(a.y) * S.W + Math.floor(a.x)] === G.TR.MOUNT) issue('army_on_mount', 't=' + S.time);
          if (!a.sq.length) issue('army_empty');
          for (const q of a.sq) { if (q.t === 'cmd' && !q.cls) issue('cmd_nocls'); for (const s of q.s) if (!isFinite(s.hp)) issue('hp_nan'); if (q.s.some(s => s.hp <= 0) && !a.bat && !a.sg) issue('zombie_soldier', 't=' + S.time); }
          if (a.retreat && a.st === 'idle' && !a.path.length) a._rs = (a._rs || 0) + 1; else a._rs = 0;
          if (a._rs === 6) issue('retreat_stuck', `t=${S.time} owner=${a.owner}`);
        }
        for (const p of S.players) for (const k in p.stock) if (!isFinite(p.stock[k]) || p.stock[k] < -1e-6) issue('stock_bad', k + '=' + p.stock[k]);
        for (let i2 = 0; i2 < S.players.length; i2++) for (let j = i2 + 1; j < S.players.length; j++) { const r = S.rel[i2][j]; if (r.trade && (!S.players[i2].alive || !S.players[j].alive)) issue('trade_with_dead'); if (r.ally && r.st === 'war') issue('ally_at_war'); }
      }
    }
    stats.games++; if (S.over) stats.over++; stats.ticks += S.tk; stats.ms += Date.now() - t0;
    const sc = S.players.map(p => p.score).join('/');
    console.log(`#${g} ${o.type}/${o.size}/b${o.bots}/${o.diff} t=${S.time.toFixed(0)} cities=${S.cities.length} bld=${S.buildings.length} armies=${S.armies.length} ships=${S.ships.length} over=${S.over ? S.over.why : '-'} score=${sc} ${(Date.now() - t0)}ms`);
  } catch (e) { fails++; console.log(`#${g} CRASH ${JSON.stringify(o)} t=${S ? S.time : '-'}: ${e.stack.split('\n').slice(0, 4).join(' | ')}`); }
}
console.log('SUMMARY', JSON.stringify(stats), 'crashes=' + fails, 'ms/tick=' + (stats.ms / Math.max(1, stats.ticks)).toFixed(3));
