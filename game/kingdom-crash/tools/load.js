'use strict';
// Загружает игровую логику (без DOM) в Node для симуляций и тестов.
const fs = require('fs'), path = require('path');
module.exports = function load(dir, files) {
  files = files || ['data.js', 'map.js', 'game.js', 'ai.js'];
  let src = '';
  for (const f of files) src += '\n;/* ' + f + ' */\n' + fs.readFileSync(path.join(dir, f), 'utf8').replace(/^'use strict';/, '');
  const names = ['newGame', 'tick', 'saveGame', 'loadGame', 'TR', 'RES', 'isWater', 'SQ', 'BD', 'units', 'dist', 'doBuild', 'siteOk', 'orderMove', 'retreat', 'stopArmy', 'mergeNear', 'splitArmy',
    'offer', 'answerMsg', 'startClaim', 'declareWar', 'startWar', 'rel', 'calcRates', 'calcScore', 'checkVictory', 'enqueue', 'enqueueCmd', 'buildShip', 'orderShip', 'unloadShip', 'mkSquad', 'newArmy',
    'labelComps', 'genMap', 'astar', 'nearestTile', 'rallyPos', 'upgradeCity', 'demolish', 'flipCity', 'milPower', 'claimInfo', 'atWar', 'allied', 'blockedFor', 'structBlockedFor', 'garrisonSquad', 'ungarrison',
    'CITY_MIN', 'DAY', 'STOCK_KEYS', 'PNAMES', 'cityOk', 'cityCost', 'recomputeTerr', 'tickCombat', 'tickMove', 'hasCommander', 'combo', 'decideOffer', 'applyDeal', 'aiThink', 'MAX_SQ_ARMY', 'sqMax', 'syncSq', 'restoreGame', 'hasSave', 'dropSave', 'sanitizeOpts', 'hireMerc', 'tavernOffer', 'setRecipe', 'stopShip', 'armyCap', 'EVENTS', 'tickEvents', 'answerEvent', 'dealError', 'PERKS', 'perk', 'farmRate', 'millNear', 'freeTileNear', 'reachTiles', 'nearSea', 'garrison', 'killPlayer', 'spawnSquad', 'info', 'addMsg', 'pname', 'cdLeft', 'siteTile', 'GAME_VERSION', 'MERC', 'ITEM_CAP', 'RECIPES', 'CMD', 'aiArmies', 'arrive', 'cityClaim', 'upgradeCost', 'addCity', 'tickDiplomacy', 'compSize', 'nearestWater', 'landComp', 'orderSiege', 'orderGarrison',
    'applySanction', 'liftSanction', 'sanActive', 'sanOn', 'sanList', 'sanCdLeft', 'SANCTIONS', 'SAN_KEYS', 'tickFolk', 'tickExpand', 'folkGet', 'folkKill', 'folkPrison', 'makeFolk', 'folkBuild', 'freeBuild', 'linkTower', 'toggleTowerPass', 'listSaves', 'readSaveMeta', 'SAVE_SLOTS'];
  const ls = { _d: {}, getItem(k) { return k in this._d ? this._d[k] : null; }, setItem(k, v) { this._d[k] = String(v); }, removeItem(k) { delete this._d[k]; } };
  const doc = { createElement() { const ctx = new Proxy({}, { get: (t, k) => k in t ? t[k] : () => {}, set: (t, k, v) => { t[k] = v; return true; } }); return { width: 0, height: 0, getContext: () => ctx, toDataURL: () => '' }; } };
  const body = src + '\n;const __o = {}; ' + names.map(n => `try { __o.${n} = ${n}; } catch (e) {}`).join(' ') + ' return __o;';
  const fn = new Function('document', 'localStorage', 'window', body);
  const api = fn(doc, ls, {});
  api._ls = ls;
  return api;
};
