/* Save system: JSON to localStorage with versioning. */
'use strict';
var ZT = window.ZT;

ZT.Save = (function(){
  const KEY='zombieTownSave_';
  function serialize(game){
    return {
      version: ZT.SAVE_VERSION,
      savedAt: Date.now(),
      map: ZT.Map.serialize(),
      time: game.time, day: game.day, timeOfDay: game.timeOfDay,
      weather: game.weather, weatherT: game.weatherT,
      resources: game.resources,
      stats: game.stats,
      outbreak: game.outbreak,
      buildings: game.buildings.map(b=>({
        id:b.id, x:b.tileX, y:b.tileY, hp:b.hp, progress:b.progress,
        constructed:b.constructed, ruin:b.ruin, destroyed:b.destroyed, burning:b.burning, level:b.level
      })),
      citizens: game.citizens.filter(c=>!c.dead).map(c=>({
        x:c.x, y:c.y, name:c.name, age:c.age, job:c.job, hp:c.hp,
        hunger:c.hunger, thirst:c.thirst, rest:c.rest, happiness:c.happiness,
        homeUid: c.home?c.home.uid:null, infected:c.infected, infectedT:c.infectedT
      })),
      zombies: game.zombies.filter(z=>!z.dead).map(z=>({x:z.x,y:z.y,type:z.type,hp:z.hp})),
      settings: ZT.settings,
    };
  }
  function save(game, slot){
    try{
      localStorage.setItem(KEY+(slot||0), JSON.stringify(serialize(game)));
      return true;
    }catch(e){ console.warn('Save failed', e); return false; }
  }
  function loadRaw(slot){
    try{
      const s=localStorage.getItem(KEY+(slot||0));
      return s?JSON.parse(s):null;
    }catch(e){ return null; }
  }
  function hasSave(slot){ return !!localStorage.getItem(KEY+(slot||0)); }
  function slotInfo(slot){
    const d=loadRaw(slot);
    if(!d) return null;
    return { date:new Date(d.savedAt).toLocaleString(), day:d.day, pop:(d.citizens||[]).length };
  }
  function wipe(slot){ localStorage.removeItem(KEY+(slot||0)); }
  return { save, loadRaw, hasSave, slotInfo, wipe };
})();
