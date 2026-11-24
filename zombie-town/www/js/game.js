/* Cozy Pixel Zombie Town — main game: managers, loop, camera, input, director. */
'use strict';
var ZT = window.ZT;

ZT.Game = function(canvas){
  this.canvas=canvas;
  this.ctx=canvas.getContext('2d');
  this.audio=new ZT.Audio();
  this.camera={x:ZT.MAP_W*ZT.TILE/2, y:ZT.MAP_H*ZT.TILE/2, zoom:1, follow:null};
  this.buildings=[]; this.buildingGrid=new Array(ZT.MAP_W*ZT.MAP_H).fill(null);
  this.citizens=[]; this.zombies=[]; this.projectiles=[]; this.particles=[];
  this.constructionSites=[];
  this.resources={wood:0,stone:0,food:0,water:0,coins:0,medicine:0,ammo:0,scrap:0};
  this.time=0; this.day=1; this.timeOfDay=0.35; // 0..1, 0.35 = morning
  this.weather='clear'; this.weatherT=60;
  this.paused=false; this.speed=1; this.inGame=false;
  this.placement=null; this.selected=null;
  this.outbreak={active:false, intensity:3, waveT:0, phase:'calm', calmT:0};
  this.stats={zombiesKilled:0, buildingsLost:0, citizensDied:0, citizensBorn:0, zombiesSpawned:0};
  this.centerTile={x:(ZT.MAP_W/2)|0, y:(ZT.MAP_H/2)|0};
  this.nestTile=null;
  this.timers={migration:5, director:3, jobs:4, hud:0, autosave:120, birth:30, homeless:5, regrow:[]};
  this.noiseEvents=[];
  this.tutorialStep=0;
  this.dayLength=240; // seconds per full day
  this.resize();
  if(window.innerHeight>window.innerWidth) this.camera.zoom=0.72; // portrait: see more of the map
  this.bindInput();
  this.loop=this.loop.bind(this);
  requestAnimationFrame(this.loop);
};
ZT.Game.prototype = {
  resize(){
    const dpr = ZT.settings.quality==='low' ? 1 : Math.min(window.devicePixelRatio||1, 2);
    this.dpr=dpr;
    this.canvas.width=window.innerWidth*dpr;
    this.canvas.height=window.innerHeight*dpr;
  },
  /* ================= lifecycle ================= */
  newGame(){
    ZT.Map.generate();
    this.resetState();
    // starting town: center + house + farm
    this.placeBuilding('center', this.centerTile.x-1, this.centerTile.y-1, true);
    const hs=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,1,1,this.buildingGrid,true);
    if(hs) this.placeBuilding('house', hs.x, hs.y, true);
    const fs=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,2,2,this.buildingGrid,true);
    if(fs) this.placeBuilding('farm', fs.x, fs.y, true);
    this.nestTile=ZT.Map.forestPoint();
    this.resources={wood:60,stone:40,food:40,water:40,coins:20,medicine:5,ammo:20,scrap:10};
    this.enterGame();
    this.notify('🏘 Город создан ('+ZT.L(ZT.BIOMES[ZT.biome].name)+')! Люди уже идут сюда...','good');
    this.tutorialStep=0;
  },
  resetState(){
    this.buildings=[]; this.buildingGrid=new Array(ZT.MAP_W*ZT.MAP_H).fill(null);
    this.citizens=[]; this.zombies=[]; this.projectiles=[]; this.particles=[];
    this.constructionSites=[]; this.time=0; this.day=1; this.timeOfDay=0.35;
    this.weather='clear'; this.weatherT=60; this.paused=false; this.speed=1;
    this.placement=null; this.selected=null;
    this.outbreak={active:false,intensity:3,waveT:0,phase:'calm',calmT:0};
    this.stats={zombiesKilled:0,buildingsLost:0,citizensDied:0,citizensBorn:0,zombiesSpawned:0};
    this.timers={migration:4,director:2,jobs:2,hud:0,autosave:120,birth:30,homeless:5,regrow:[]};
    this.noiseEvents=[];
    this.camera.x=ZT.MAP_W*ZT.TILE/2; this.camera.y=ZT.MAP_H*ZT.TILE/2; this.camera.zoom=1; this.camera.follow=null;
  },
  enterGame(){
    this.inGame=true;
    ZT.UI.enterGame();
    this.audio.startMusic();
    this.updateTutorial();
  },
  toMenu(){
    this.inGame=false; this.paused=true;
    ZT.UI.toMenu();
  },
  saveTo(slot){ return ZT.Save.save(this, slot||0); },
  loadFrom(slot){
    const data=ZT.Save.loadRaw(slot||0);
    if(!data){ this.notify('Нет сохранения','bad'); return false; }
    this.resetState();
    const c=ZT.Map.deserialize(data.map);
    this.centerTile={x:c.cx,y:c.cy};
    this.time=data.time; this.day=data.day; this.timeOfDay=data.timeOfDay;
    this.weather=data.weather; this.weatherT=data.weatherT;
    this.resources=Object.assign({wood:0,stone:0,food:0,water:0,coins:0,medicine:0,ammo:0,scrap:0}, data.resources);
    this.stats=Object.assign(this.stats, data.stats);
    this.outbreak=Object.assign(this.outbreak, data.outbreak);
    if(data.settings) ZT.settings=Object.assign(ZT.settings, data.settings);
    // buildings
    (data.buildings||[]).forEach(bd=>{
      const b=this.placeBuilding(bd.id, bd.x, bd.y, true);
      b.hp=bd.hp; b.progress=bd.progress; b.constructed=bd.constructed;
      b.ruin=bd.ruin; b.destroyed=bd.destroyed; b.burning=bd.burning||0; b.level=bd.level||1;
      if(!b.constructed && !b.ruin) this.constructionSites.push(b);
    });
    // citizens
    (data.citizens||[]).forEach(cd=>{
      const c=new ZT.Citizen(cd.x, cd.y, {name:cd.name, age:cd.age});
      c.job=cd.job; c.hp=cd.hp; c.hunger=cd.hunger; c.thirst=cd.thirst; c.rest=cd.rest;
      c.happiness=cd.happiness; c.infected=cd.infected||0; c.infectedT=cd.infectedT||0;
      c.state='socialize';
      this.citizens.push(c);
    });
    // reassign homes
    this.citizens.forEach(c=>{
      const home=this.findFreeHousing(c);
      if(home){ home.residents.push(c); c.home=home; }
    });
    // zombies
    (data.zombies||[]).forEach(zd=>{
      const z=new ZT.Zombie(zd.x, zd.y, zd.type); z.hp=zd.hp;
      this.zombies.push(z);
    });
    this.nestTile=ZT.Map.forestPoint();
    this.enterGame();
    this.notify('📂 Загружено! День '+this.day,'good');
    return true;
  },
  /* ================= time / weather ================= */
  isNight(){ return this.timeOfDay>0.78 || this.timeOfDay<0.2; },
  isDay(){ return this.timeOfDay>0.28 && this.timeOfDay<0.72; },
  nightAlpha(){
    const t=this.timeOfDay;
    if(t>0.72&&t<0.82) return (t-0.72)/0.1*0.55;
    if(t>=0.82||t<0.14) return 0.55;
    if(t>=0.14&&t<0.24) return (0.24-t)/0.1*0.55;
    return 0;
  },
  updateTime(dt){
    this.time+=dt;
    this.timeOfDay+=dt/this.dayLength;
    if(this.timeOfDay>=1){ this.timeOfDay-=1; this.day++; this.notify('🌅 День '+this.day,'good'); }
    this.weatherT-=dt;
    if(this.weatherT<=0){
      this.weatherT=50+Math.random()*70;
      const r=Math.random();
      const winter = ZT.BIOMES[ZT.biome] && ZT.BIOMES[ZT.biome].snow;
      this.weather = winter
        ? (r<0.4?'clear':r<0.75?'snow':r<0.9?'fog':'storm')
        : (r<0.5?'clear':r<0.7?'rain':r<0.85?'fog':'storm');
      const names={clear:'☀️ Ясно',rain:'🌧️ Дождь',snow:'❄️ Снег',fog:'🌫️ Туман',storm:'⛈️ Гроза'};
      this.notify(names[this.weather]);
    }
    this.audio.setMusicMode(this.isNight()||this.outbreak.active ? 'night' : 'day');
  },
  /* ================= economy ================= */
  resourceCaps(){
    const caps=Object.assign({}, ZT.BASE_CAP);
    this.buildings.forEach(b=>{
      if(b.constructed && !b.ruin && b.def.storageBonus){
        for(const r in b.def.storageBonus) caps[r]=(caps[r]||0)+b.def.storageBonus[r];
      }
    });
    return caps;
  },
  addResource(r,n){
    const caps=this.resourceCaps();
    this.resources[r]=(this.resources[r]||0)+n;
    if(caps[r] && r!=='coins') this.resources[r]=Math.min(caps[r], this.resources[r]);
    if(this.resources[r]<0) this.resources[r]=0;
  },
  canAfford(def){
    for(const r in def.cost){ if((this.resources[r]||0)<def.cost[r]) return false; }
    return true;
  },
  produceAt(building, citizen){
    const id=building.id;
    if(id==='farm'){
      let n=2; if(this.weather==='rain') n=3; if(this.weather==='storm') n=1;
      this.addResource('food',n);
      this.spawnParticles(building.cx,building.cy-6,'#9ccc65',2);
    } else if(id==='well'){ this.addResource('water',2); this.spawnParticles(building.cx,building.cy-6,'#29b6f6',2); }
    else if(id==='kitchen'){
      if(this.resources.food>=1){ this.resources.food--; this.addResource('coins',1); if(Math.random()<0.3) this.audio.sfx('coin'); }
    } else if(id==='workshop'){
      if(this.resources.wood>=1 && this.resources.scrap>=1){
        this.resources.wood--; this.resources.scrap--;
        this.addResource('medicine',1); this.addResource('ammo',2);
      }
    } else if(building.isTree){
      this.addResource('wood',2); this.audio.sfx('chop');
      building.chops=(building.chops||0)+1;
      if(building.chops>=3){
        ZT.Map.setTile(building.tileX, building.tileY, 0);
        this.timers.regrow.push({x:building.tileX,y:building.tileY,t:70});
        building.destroyed=true;
        this.citizens.forEach(c=>{ if(c.work===building) c.work=null; });
      }
    } else if(building.isRock){
      this.addResource('stone',2); this.audio.sfx('chop');
      building.chops=(building.chops||0)+1;
      if(building.chops>=4){
        ZT.Map.setTile(building.tileX, building.tileY, 0);
        this.timers.regrow.push({x:building.tileX,y:building.tileY,t:90});
        building.destroyed=true;
        this.citizens.forEach(c=>{ if(c.work===building) c.work=null; });
      }
    }
  },
  /* ================= buildings ================= */
  placeBuilding(id, x, y, instant){
    const def=ZT.BUILDINGS[id];
    const b=new ZT.Building(id, x, y);
    if(instant){ b.constructed=true; b.progress=def.buildTime; }
    else { b.constructed=false; b.progress=0; this.constructionSites.push(b); }
    for(let by=0;by<b.h;by++)for(let bx=0;bx<b.w;bx++){
      this.buildingGrid[ZT.Map.idx(x+bx,y+by)]=b;
    }
    this.buildings.push(b);
    if(id==='road'){ ZT.Map.setTile(x,y,2); }
    return b;
  },
  canPlaceAt(id, x, y){
    const def=ZT.BUILDINGS[id];
    if(x<1||y<1||x+def.w>ZT.MAP_W-1||y+def.h>ZT.MAP_H-1) return false;
    for(let by=0;by<def.h;by++)for(let bx=0;bx<def.w;bx++){
      const gx=x+bx, gy=y+by;
      const t=ZT.Map.getTile(gx,gy);
      if(t===3) return false; // water
      if(id!=='road' && (t===4||t===5)) return false; // trees/rocks block
      if(this.buildingGrid[ZT.Map.idx(gx,gy)]) return false;
    }
    return true;
  },
  completeConstruction(b){
    b.constructed=true; b.progress=b.def.buildTime;
    this.constructionSites=this.constructionSites.filter(s=>s!==b);
    this.noise(b.cx,b.cy);
    this.notify('🏗 '+ZT.L(b.def.name)+' построен!','good');
    this.audio.sfx('build-done');
    this.spawnParticles(b.cx,b.cy,'#ffcc80',10);
    if(b.isHousing()) this.tryHouseCitizens();
  },
  demolishBuilding(b){
    if(b.id==='center'){ this.notify('Центр города нельзя снести!','bad'); return; }
    // refund 50%
    for(const r in b.def.cost) this.addResource(r, Math.floor(b.def.cost[r]/2));
    for(let by=0;by<b.h;by++)for(let bx=0;bx<b.w;bx++){
      this.buildingGrid[ZT.Map.idx(b.tileX+bx,b.tileY+by)]=null;
    }
    if(b.id==='road') ZT.Map.setTile(b.tileX,b.tileY,1);
    this.buildings=this.buildings.filter(x=>x!==b);
    this.constructionSites=this.constructionSites.filter(x=>x!==b);
    this.citizens.forEach(c=>{ if(c.home===b) c.home=null; if(c.work===b) c.work=null; });
    this.notify('🧨 '+ZT.L(b.def.name)+' снесено','');
    this.audio.sfx('smash');
  },
  repairBuilding(b){
    const cost=b.repairCost();
    for(const r in cost){ if((this.resources[r]||0)<cost[r]){ this.notify('Не хватает ресурсов!','bad'); this.audio.sfx('error'); return; } }
    for(const r in cost) this.resources[r]-=cost[r];
    b.hp=b.maxHp;
    this.notify('🔧 '+ZT.L(b.def.name)+' отремонтировано','good');
    this.audio.sfx('build-done');
  },
  rebuildBuilding(b){
    const cost={};
    for(const r in b.def.cost) cost[r]=Math.ceil(b.def.cost[r]/2);
    for(const r in cost){ if((this.resources[r]||0)<cost[r]){ this.notify('Не хватает ресурсов!','bad'); return; } }
    for(const r in cost) this.resources[r]-=cost[r];
    b.ruin=false; b.destroyed=false; b.hp=b.maxHp; b.constructed=false; b.progress=0;
    this.constructionSites.push(b);
    this.notify('🏗 Перестройка: '+ZT.L(b.def.name),'good');
  },
  upgradeBuilding(b){
    const nextId=b.def.upgradesTo;
    if(!nextId) return;
    const next=ZT.BUILDINGS[nextId];
    if(!this.canAfford(next)){ this.notify('Не хватает ресурсов на улучшение!','bad'); this.audio.sfx('error'); return; }
    for(const r in next.cost) this.resources[r]-=next.cost[r];
    const ratio=b.hp/b.maxHp;
    b.id=nextId; b.def=next; b.maxHp=next.hp; b.hp=Math.round(next.hp*ratio); b.level++;
    this.notify('⬆ '+ZT.L(next.name)+' готов!','good');
    this.audio.sfx('upgrade');
    this.spawnParticles(b.cx,b.cy,'#ffeb3b',12);
  },
  damageBuilding(b, dmg, attacker){
    if(b.destroyed||b.ruin||!b.constructed) return;
    b.damage(dmg, this, attacker);
  },
  onBuildingDestroyed(b, cause){
    this.stats.buildingsLost++;
    this.noise(b.cx,b.cy);
    this.notify('💥 '+ZT.L(b.def.name)+' разрушено!','bad');
    this.audio.sfx('smash');
    this.vibrate(120);
    this.spawnParticles(b.cx,b.cy,'#ff7043',16);
    this.spawnParticles(b.cx,b.cy,'#757575',10);
    // residents become homeless
    this.citizens.forEach(c=>{ if(c.home===b){ c.home=null; c.happiness=Math.max(0,c.happiness-20); } });
    // fire spreads to neighbor
    if(Math.random()<0.35){
      const nb=this.buildings.find(o=>o!==b&&!o.destroyed&&!o.ruin&&o.constructed&&Math.hypot(o.cx-b.cx,o.cy-b.cy)<70);
      if(nb) nb.burning=12;
    }
    // morale hit
    this.citizens.forEach(c=>{ if(!c.dead) c.happiness=Math.max(0,c.happiness-8); });
  },
  /* ================= population ================= */
  findBuilding(id){ return this.buildings.find(b=>b.id===id&&!b.destroyed&&!b.ruin&&b.constructed); },
  findFreeHousing(citizen){
    let best=null,bestD=1e9;
    this.buildings.forEach(b=>{
      if(!b.isHousing()||b.destroyed||b.ruin||!b.constructed) return;
      if(b.freeBeds()<=0) return;
      const d=Math.hypot(b.cx-citizen.x,b.cy-citizen.y);
      if(d<bestD){ bestD=d; best=b; }
    });
    return best;
  },
  totalBeds(){
    return this.buildings.reduce((s,b)=>s+(b.isHousing()&&b.constructed&&!b.ruin?b.def.capacity:0),0);
  },
  avgHappiness(){
    const alive=this.citizens.filter(c=>!c.dead);
    if(!alive.length) return 50;
    return alive.reduce((s,c)=>s+c.happiness,0)/alive.length;
  },
  safetyScore(){
    let s=90 - this.zombies.length*2.5;
    this.buildings.forEach(b=>{
      if(b.destroyed||b.ruin) return;
      if(b.id==='wall'||b.id==='stonewall') s+=1.5;
      if(b.id==='tower'||b.id==='turret') s+=4;
      if(b.id==='safehouse') s+=5;
    });
    return Math.max(0,Math.min(100,s));
  },
  spawnCitizen(){
    const cap=ZT.qualityCaps[ZT.settings.quality]||ZT.qualityCaps.mid;
    const alive=this.citizens.filter(c=>!c.dead).length;
    if(alive>=cap.citizens) return null;
    const e=ZT.Map.edgePoint();
    const w=ZT.Map.tileToWorld(e.x,e.y);
    const c=new ZT.Citizen(w.x,w.y);
    this.citizens.push(c);
    return c;
  },
  onCitizenArrived(c){
    const home=this.findFreeHousing(c);
    if(home){ home.residents.push(c); c.home=home; }
    this.notify('🏠 '+c.name+' прибыл в город!','good');
    this.audio.sfx('arrive');
    this.updateTutorial();
  },
  onCitizenLeft(c){
    this.notify('🚶 '+c.name+' покинул город','');
  },
  onCitizenDied(c){
    this.stats.citizensDied++;
    this.notify('⚰️ '+c.name+' погиб...','bad');
    this.audio.sfx('death');
    this.vibrate(150);
    this.spawnParticles(c.x,c.y,'#b71c1c',10);
    const hasCemetery=!!this.findBuilding('cemetery');
    if(hasCemetery) this.notify('🪦 '+c.name+' похоронен на кладбище','');
    else this.citizens.forEach(o=>{ if(!o.dead) o.happiness=Math.max(0,o.happiness-10); });
    this.citizens.forEach(o=>{ if(!o.dead) o.happiness=Math.max(0,o.happiness-5); });
    if(c.home) c.home.residents=c.home.residents.filter(x=>x!==c);
    this.buildings.forEach(b=>{ b.workers=b.workers.filter(x=>x!==c); });
    if(c.friend && !c.friend.dead) c.friend.happiness=Math.max(0,c.friend.happiness-15);
  },
  turnCitizen(c){
    c.dead=true;
    if(c.home) c.home.residents=c.home.residents.filter(x=>x!==c);
    this.buildings.forEach(b=>{ b.workers=b.workers.filter(x=>x!==c); });
    const z=new ZT.Zombie(c.x,c.y,'walker');
    this.zombies.push(z);
    this.notify('🧟 '+c.name+' превратился в зомби!','bad');
    this.audio.sfx('infect');
  },
  tryHouseCitizens(){
    this.citizens.forEach(c=>{
      if(c.dead||c.home) return;
      const home=this.findFreeHousing(c);
      if(home){ home.residents.push(c); c.home=home; }
    });
  },
  assignJobs(){
    const alive=this.citizens.filter(c=>!c.dead&&!c.leaving&&c.age>=16);
    // clear auto assignments (keep manual)
    alive.forEach(c=>{ if(!c.manualJob){ c.job='idle'; c.work=null; } });
    this.buildings.forEach(b=>{ b.workers=[]; });
    const free=alive.filter(c=>!c.manualJob);
    const take=(n)=>{ const r=free.splice(0,n); return r; };
    // builders for construction sites
    this.constructionSites.forEach(site=>{
      take(2).forEach(c=>{ c.job='builder'; c.work=site; });
    });
    // farmers
    this.buildings.filter(b=>b.id==='farm'&&b.constructed&&!b.ruin).forEach(f=>{
      take(2).forEach(c=>{ c.job='farmer'; c.work=f; f.workers.push(c); });
    });
    // water carriers
    this.buildings.filter(b=>b.id==='well'&&b.constructed&&!b.ruin).forEach(w=>{
      take(1).forEach(c=>{ c.job='carrier'; c.work=w; w.workers.push(c); });
    });
    // cooks
    this.buildings.filter(b=>b.id==='kitchen'&&b.constructed&&!b.ruin).forEach(k=>{
      take(1).forEach(c=>{ c.job='cook'; c.work=k; k.workers.push(c); });
    });
    // workers
    this.buildings.filter(b=>b.id==='workshop'&&b.constructed&&!b.ruin).forEach(w=>{
      take(2).forEach(c=>{ c.job='worker'; c.work=w; w.workers.push(c); });
    });
    // medics
    this.buildings.filter(b=>b.id==='clinic'&&b.constructed&&!b.ruin).forEach(cl=>{
      take(1).forEach(c=>{ c.job='medic'; c.work=cl; cl.workers.push(c); });
    });
    // guards: towers first, then extra if zombies
    this.buildings.filter(b=>b.id==='tower'&&b.constructed&&!b.ruin).forEach(t=>{
      take(1).forEach(c=>{ c.job='guard'; c.work=t; t.workers.push(c); });
    });
    if(this.zombies.length>0){
      const extra=Math.min(free.length, Math.ceil(alive.length*0.4));
      take(extra).forEach(c=>{ c.job='guard'; });
    }
    // woodcutters / miners if idle and resources low
    const idle=free;
    if(this.resources.wood<30) take(Math.min(2,idle.length)).forEach(c=>{ c.job='woodcutter'; });
    if(this.resources.stone<30) take(Math.min(2,idle.length)).forEach(c=>{ c.job='miner'; });
  },
  assignJob(c, job){
    c.job=job; c.manualJob = job!=='idle';
    if(job==='idle') c.manualJob=false;
    this.notify('💼 '+c.name+': '+ZT.L(ZT.JOBS[job].name));
  },
  treatCitizen(c){
    if(c.infected<=0) return;
    if(this.resources.medicine<1){ this.notify('Нет лекарств! Нужна мастерская 💊','bad'); return; }
    this.resources.medicine--;
    c.infected=0;
    this.notify('💊 '+c.name+' вылечен!','good');
    this.audio.sfx('heal');
  },
  followCitizen(c){
    this.citizens.forEach(o=>o.follow=false);
    c.follow=true;
    this.camera.follow=c;
    this.notify('📍 Слежу за '+c.name);
  },
  safehouseTile(){
    const s=this.findBuilding('safehouse');
    if(s) return {x:s.tileX,y:s.tileY};
    return this.centerTile;
  },
  guardPost(c){
    const posts=this.buildings.filter(b=>(b.id==='tower'||b.id==='wall'||b.id==='stonewall')&&b.constructed&&!b.ruin);
    if(posts.length){
      let best=posts[0],bd=1e9;
      posts.forEach(p=>{ const d=Math.hypot(p.cx-c.x,p.cy-c.y); if(d<bd){bd=d;best=p;} });
      return {x:best.tileX,y:best.tileY};
    }
    return {x:this.centerTile.x+(((Math.random()*7)|0)-3), y:this.centerTile.y+(((Math.random()*5)|0)-2)};
  },
  nearestZombie(from, maxDist){
    let best=null,bd=maxDist||1e9;
    for(let i=0;i<this.zombies.length;i++){
      const z=this.zombies[i]; if(z.dead) continue;
      const d=Math.hypot(z.x-from.x,z.y-from.y);
      if(d<bd){ bd=d; best=z; }
    }
    return best;
  },
  nearestZombieDist(from){
    const z=this.nearestZombie(from,1e9);
    return z?Math.hypot(z.x-from.x,z.y-from.y):1e9;
  },
  /* ================= zombies ================= */
  spawnZombies(n){
    const modeEl=document.getElementById('z-spawnpoint');
    const mode=modeEl?modeEl.value:'edge';
    let base;
    if(mode==='forest') base=ZT.Map.forestPoint();
    else if(mode==='nest'&&this.nestTile) base=this.nestTile;
    else base=ZT.Map.edgePoint();
    const types=['walker','walker','walker','walker','walker','runner','runner','spitter','screamer','brute'];
    for(let i=0;i<n;i++){
      const tx=base.x+(((Math.random()*5)|0)-2), ty=base.y+(((Math.random()*5)|0)-2);
      const w=ZT.Map.tileToWorld(tx,ty);
      const type=types[(Math.random()*types.length)|0];
      this.zombies.push(new ZT.Zombie(w.x,w.y,type));
    }
    this.stats.zombiesSpawned+=n;
    this.notify('🧟 Спавнено зомби: '+n,'bad');
    this.audio.sfx('groan');
    this.vibrate(80);
    this.updateTutorial();
  },
  startOutbreak(){
    const slider=document.getElementById('z-intensity');
    this.outbreak.intensity=slider?parseInt(slider.value):3;
    this.outbreak.active=true; this.outbreak.phase='first'; this.outbreak.waveT=8; this.outbreak.calmT=0;
    this.spawnZombies(this.outbreak.intensity*3);
    this.notify('☣ ВСПЫШКА НАЧАЛАСЬ!','bad');
    this.audio.sfx('alarm');
    this.vibrate(200);
  },
  stopOutbreak(){
    this.outbreak.active=false; this.outbreak.phase='calm';
    this.notify('🕊 Вспышка остановлена. Город выдыхает...','good');
    this.audio.sfx('heal');
  },
  outbreakPhaseText(){
    if(!this.outbreak.active) return this.outbreak.phase==='recovery'?'Восстановление':'Покой';
    return {first:'Первое заражение',panic:'Паника',siege:'Осада',horde:'Ночная орда',recovery:'Восстановление'}[this.outbreak.phase]||'Вспышка';
  },
  updateOutbreak(dt){
    if(!this.outbreak.active){
      if(this.outbreak.phase==='recovery'){
        this.outbreak.calmT+=dt;
        if(this.outbreak.calmT>20){ this.outbreak.phase='calm'; this.notify('🕊 Город снова в покое','good'); }
      }
      return;
    }
    // phase progression
    const zc=this.zombies.length;
    if(this.isNight()&&zc>8) this.outbreak.phase='horde';
    else if(zc>20||this.stats.buildingsLost>0) this.outbreak.phase='siege';
    else if(zc>8) this.outbreak.phase='panic';
    else if(zc===0){ this.outbreak.phase='recovery'; this.outbreak.calmT=0; this.notify('🌅 Орда отбита! Город восстанавливается','good'); return; }
    // waves
    this.outbreak.waveT-=dt;
    if(this.outbreak.waveT<=0){
      this.outbreak.waveT=18+Math.random()*10;
      const diffMul={peaceful:0.3,easy:0.6,normal:1,hard:1.6}[ZT.settings.difficulty]||1;
      let n=Math.round(this.outbreak.intensity*2*diffMul);
      if(this.isNight()) n=Math.round(n*1.5);
      if(n>0) this.spawnZombies(n);
    }
  },
  damageZombie(z, dmg, attacker){
    if(z.dead) return;
    z.takeDamage(dmg, attacker);
    if(z.dead){
      this.stats.zombiesKilled++;
      this.citizens.forEach(c=>{ if(!c.dead && Math.hypot(c.x-z.x,c.y-z.y)<130) c.exp=Math.min(100,(c.exp||0)+2); });
      this.spawnParticles(z.x,z.y,'#8bc34a',8);
      this.audio.sfx('groan');
      // loot
      if(Math.random()<0.3) this.addResource('coins',1+((Math.random()*3)|0));
      if(Math.random()<0.35) this.addResource('scrap',1+((Math.random()*2)|0));
      if(Math.random()<0.15) this.addResource('medicine',1);
    }
  },
  buffZombiesNear(z, radius, mul, dur){
    this.zombies.forEach(o=>{
      if(o.dead) return;
      if(Math.hypot(o.x-z.x,o.y-z.y)<radius){ o.speedMul=mul; o.buffT=dur; }
    });
  },
  spawnProjectile(x,y,tx,ty,color,dmg){
    this.noise(x,y);
    if(this.projectiles.length>220) return;
    this.projectiles.push(new ZT.Projectile(x,y,tx,ty,color,dmg));
  },
  spawnParticles(x,y,color,n){
    if(!ZT.settings.particles) return;
    for(let i=0;i<n;i++){
      if(this.particles.length>320) break;
      this.particles.push(new ZT.Particle(x+(Math.random()-0.5)*10,y+(Math.random()-0.5)*10,color));
    }
  },
  /* ================= director (auto city growth) ================= */
  updateDirector(dt){
    // migration
    this.timers.migration-=dt;
    if(this.timers.migration<=0){
      this.timers.migration=6;
      const alive=this.citizens.filter(c=>!c.dead).length;
      const freeBeds=this.totalBeds()-this.citizens.filter(c=>!c.dead&&c.home).length;
      const cap=ZT.qualityCaps[ZT.settings.quality]||ZT.qualityCaps.mid;
      if(freeBeds>0 && this.resources.food>8 && this.resources.water>8 && this.avgHappiness()>42 && alive<cap.citizens && this.zombies.length<5){
        this.spawnCitizen();
      }
    }
    // homeless claim beds
    this.timers.homeless-=dt;
    if(this.timers.homeless<=0){ this.timers.homeless=5; this.tryHouseCitizens(); }
    // jobs
    this.timers.jobs-=dt;
    if(this.timers.jobs<=0){ this.timers.jobs=4; this.assignJobs(); }
    // births
    this.timers.birth-=dt;
    if(this.timers.birth<=0){
      this.timers.birth=30;
      const alive=this.citizens.filter(c=>!c.dead);
      if(alive.length< (ZT.qualityCaps[ZT.settings.quality]||ZT.qualityCaps.mid).citizens && this.resources.food>20 && this.avgHappiness()>68){
        const home=this.buildings.find(b=>b.isHousing()&&b.constructed&&!b.ruin&&b.residents.filter(c=>!c.dead&&c.age>=18).length>=2);
        if(home){
          const child=new ZT.Citizen(home.cx,home.cy,{age:3+((Math.random()*10)|0)});
          child.state='socialize';
          this.citizens.push(child);
          this.stats.citizensBorn++;
          this.notify('👶 В городе родился житель!','good');
        }
      }
    }
    // unhappy leave
    this.citizens.forEach(c=>{
      if(c.dead||c.leaving) return;
      if(c.happiness<14 && this.zombies.length===0){
        c.unhappyT=(c.unhappyT||0)+dt;
        if(c.unhappyT>45){ c.leaving=true; c.state='leaving'; c.path=null; }
      } else c.unhappyT=0;
    });
    // construction orders
    this.timers.director-=dt;
    if(this.timers.director<=0){
      this.timers.director=3;
      this.directorBuild();
    }
    // tree/rock regrow
    for(let i=this.timers.regrow.length-1;i>=0;i--){
      const r=this.timers.regrow[i];
      r.t-=dt;
      if(r.t<=0){ ZT.Map.setTile(r.x,r.y, Math.random()<0.6?4:5); this.timers.regrow.splice(i,1); }
    }
  },
  directorBuild(){
    const alive=this.citizens.filter(c=>!c.dead).length;
    const freeBeds=this.totalBeds()-this.citizens.filter(c=>!c.dead&&c.home).length;
    const sites=this.constructionSites.length;
    // housing demand
    if(freeBeds<2 && alive>=2 && sites<2){
      const def=ZT.BUILDINGS.house;
      if(this.canAfford(def)){
        const spot=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,1,1,this.buildingGrid,true);
        if(spot){
          for(const r in def.cost) this.resources[r]-=def.cost[r];
          this.placeBuilding('house',spot.x,spot.y,false);
          this.autoRoad(spot.x,spot.y);
          this.notify('🏗 Строители начали новый домик!','');
          return;
        }
      }
    }
    // food / water
    if(this.resources.food<15 && alive>=3 && sites<2 && this.canAfford(ZT.BUILDINGS.farm)){
      const spot=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,2,2,this.buildingGrid,true);
      if(spot){ for(const r in ZT.BUILDINGS.farm.cost) this.resources[r]-=ZT.BUILDINGS.farm.cost[r]; this.placeBuilding('farm',spot.x,spot.y,false); this.notify('🏗 Новая ферма!',''); return; }
    }
    if(this.resources.water<15 && alive>=3 && sites<2 && this.canAfford(ZT.BUILDINGS.well)){
      const spot=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,1,1,this.buildingGrid,true);
      if(spot){ for(const r in ZT.BUILDINGS.well.cost) this.resources[r]-=ZT.BUILDINGS.well.cost[r]; this.placeBuilding('well',spot.x,spot.y,false); this.notify('🏗 Новый колодец!',''); return; }
    }
    // upgrades: full housing grows upward
    for(const b of this.buildings){
      if(!b.isHousing()||!b.constructed||b.ruin||!b.def.upgradesTo) continue;
      const residents=b.residents.filter(c=>!c.dead).length;
      if(residents<b.def.capacity) continue;
      const needPop = b.id==='house'?10:25;
      if(alive<needPop) continue;
      b.fullT=(b.fullT||0)+3;
      if(b.fullT>=15){
        b.fullT=0;
        const next=ZT.BUILDINGS[b.def.upgradesTo];
        if(this.canAfford(next)){
          this.upgradeBuilding(b);
          this.notify('🏙 Город растёт: '+ZT.L(next.name)+'!','good');
        }
      }
    }
    // workshop when town is big (medicine/ammo/scrap economy)
    if(alive>=15 && !this.findBuilding('workshop') && sites<2 && this.canAfford(ZT.BUILDINGS.workshop)){
      const spot=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,2,1,this.buildingGrid,true);
      if(spot){ for(const r in ZT.BUILDINGS.workshop.cost) this.resources[r]-=ZT.BUILDINGS.workshop.cost[r]; this.placeBuilding('workshop',spot.x,spot.y,false); this.notify('🏗 Новая мастерская!',''); return; }
    }
    // storage when resources near cap
    const caps=this.resourceCaps();
    if((this.resources.wood>=caps.wood*0.9||this.resources.stone>=caps.stone*0.9) && sites<2 && this.canAfford(ZT.BUILDINGS.storage)){
      const spot=ZT.Map.findSpot(this.centerTile.x,this.centerTile.y,2,1,this.buildingGrid,true);
      if(spot){ for(const r in ZT.BUILDINGS.storage.cost) this.resources[r]-=ZT.BUILDINGS.storage.cost[r]; this.placeBuilding('storage',spot.x,spot.y,false); this.notify('🏗 Новый склад!',''); return; }
    }
  },
  autoRoad(tx, ty){
    // draw a dirt/road path from center toward new building if far
    let x=this.centerTile.x, y=this.centerTile.y;
    let guard=0;
    while((x!==tx||y!==ty)&&guard++<60){
      if(x<tx) x++; else if(x>tx) x--;
      else if(y<ty) y++; else if(y>ty) y--;
      if(x===tx&&y===ty) break;
      const t=ZT.Map.getTile(x,y);
      if(t!==3&&t!==4&&t!==5&&!this.buildingGrid[ZT.Map.idx(x,y)]) ZT.Map.setTile(x,y,2);
    }
  },
  /* ================= alarm / misc ================= */
  triggerAlarm(){
    this.audio.sfx('alarm');
    this.vibrate(150);
    this.noise(this.centerTile.x*ZT.TILE,this.centerTile.y*ZT.TILE);
    this.notify('🚨 ТРЕВОГА! Все в убежище!','bad');
    this.citizens.forEach(c=>{ if(!c.dead&&c.job!=='guard') c.flee(this); });
  },
  setPaused(p){ this.paused=p; document.getElementById('btn-pause').textContent=p?'▶':'⏸'; },
  setSpeed(s){ this.speed=s; },
  notify(text, kind){
    ZT.UI.toast(text, kind);
    if(kind==='bad') this.vibrate(60);
  },
  vibrate(ms){ if(ZT.settings.vibrate && navigator.vibrate) navigator.vibrate(ms); },
  noise(x,y){
    this.noiseEvents.push({x,y,t:8});
    if(this.noiseEvents.length>24) this.noiseEvents.shift();
  },
  nearestNoise(from, maxDist){
    let best=null,bd=maxDist;
    for(let i=0;i<this.noiseEvents.length;i++){
      const n=this.noiseEvents[i];
      const d=Math.hypot(n.x-from.x,n.y-from.y);
      if(d<bd){ bd=d; best=n; }
    }
    return best;
  },
  updateTutorial(){
    if(!this.inGame) return;
    const alive=this.citizens.filter(c=>!c.dead).length;
    const beds=this.totalBeds();
    const hasDefense=this.buildings.some(b=>['wall','stonewall','tower','turret','gate'].includes(b.id));
    let step=-1, text='';
    if(alive<3){ step=0; text='🏘 Люди заселяются сами! Смотри, как растёт город...'; }
    else if(beds<6){ step=1; text='🏠 Строители возводят дома. Скоро появятся многоэтажки и небоскрёбы!'; }
    else if(this.stats.zombiesSpawned===0){ step=2; text='🧟 Нажми кнопку ЗОМБИ внизу и заспавнь первого зомби!'; }
    else if(!hasDefense){ step=3; text='🛡 Построй стену или башню — зомби уже гуляют!'; }
    else if(this.stats.zombiesKilled===0&&this.zombies.length===0){ step=4; text='⚔️ Отбей атаку! Охранники и башни помогут.'; }
    if(step>=0){ this.tutorialStep=step; ZT.UI.showTutorial(text); }
    else ZT.UI.hideTutorial();
  },
  /* ================= placement ================= */
  startPlacement(id){
    this.placement={id, x:this.centerTile.x+3, y:this.centerTile.y, valid:false};
    this.updatePlacementValidity();
    ZT.UI.show('placebar');
    document.getElementById('place-info').textContent=ZT.BUILDINGS[id].icon+' '+ZT.L(ZT.BUILDINGS[id].name)+' — '+this.costTextFor(id);
    this.audio.sfx('click');
  },
  costTextFor(id){
    const def=ZT.BUILDINGS[id];
    return Object.keys(def.cost).map(r=>ZT.RES[r].icon+' '+def.cost[r]).join(' ')||'бесплатно';
  },
  updatePlacementValidity(){
    if(!this.placement) return;
    this.placement.valid=this.canPlaceAt(this.placement.id,this.placement.x,this.placement.y) && this.canAfford(ZT.BUILDINGS[this.placement.id]);
  },
  confirmPlacement(){
    if(!this.placement) return;
    if(!this.placement.valid){ this.notify('Сюда нельзя поставить!','bad'); this.audio.sfx('error'); return; }
    const def=ZT.BUILDINGS[this.placement.id];
    for(const r in def.cost) this.resources[r]-=def.cost[r];
    this.placeBuilding(this.placement.id, this.placement.x, this.placement.y, false);
    this.audio.sfx('place');
    this.notify('🏗 '+ZT.L(def.name)+' — строим!','');
    this.cancelPlacement();
    this.updateTutorial();
  },
  cancelPlacement(){
    this.placement=null;
    ZT.UI.hide('placebar');
  },
  /* ================= input ================= */
  bindInput(){
    const pointers=new Map();
    let lastTap={t:0,x:0,y:0};
    let downInfo=null;
    const toWorld=(sx,sy)=>{
      const rect=this.canvas.getBoundingClientRect();
      const px=(sx-rect.left)/rect.width*window.innerWidth;
      const py=(sy-rect.top)/rect.height*window.innerHeight;
      return { x:(px-window.innerWidth/2)/this.camera.zoom+this.camera.x, y:(py-window.innerHeight/2)/this.camera.zoom+this.camera.y };
    };
    this.canvas.addEventListener('pointerdown',(e)=>{
      this.audio.resume();
      this.canvas.setPointerCapture(e.pointerId);
      pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});
      downInfo={x:e.clientX,y:e.clientY,t:Date.now(),moved:false};
      if(this.placement){
        const w=toWorld(e.clientX,e.clientY);
        const t=ZT.Map.worldToTile(w.x,w.y);
        this.placement.x=t.x; this.placement.y=t.y;
        this.updatePlacementValidity();
      }
    });
    this.canvas.addEventListener('pointermove',(e)=>{
      if(!pointers.has(e.pointerId)) return;
      const prev=pointers.get(e.pointerId);
      const dx=e.clientX-prev.x, dy=e.clientY-prev.y;
      pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});
      if(downInfo && Math.hypot(e.clientX-downInfo.x,e.clientY-downInfo.y)>10) downInfo.moved=true;
      if(pointers.size===2){
        // pinch zoom
        const pts=[...pointers.values()];
        const d=Math.hypot(pts[0].x-pts[1].x,pts[0].y-pts[1].y);
        if(this._pinchD) this.camera.zoom=Math.max(0.45,Math.min(3,this.camera.zoom*d/this._pinchD));
        this._pinchD=d;
        this.camera.follow=null;
      } else if(pointers.size===1 && !this.placement){
        this.camera.x-=dx/this.camera.zoom; this.camera.y-=dy/this.camera.zoom;
        this.camera.follow=null;
        this.clampCamera();
      } else if(this.placement){
        const w=toWorld(e.clientX,e.clientY);
        const t=ZT.Map.worldToTile(w.x,w.y);
        this.placement.x=t.x; this.placement.y=t.y;
        this.updatePlacementValidity();
      }
    });
    const up=(e)=>{
      pointers.delete(e.pointerId);
      if(pointers.size<2) this._pinchD=null;
      if(!downInfo) return;
      const dt=Date.now()-downInfo.t;
      const moved=downInfo.moved;
      downInfo=null;
      if(!this.inGame) return;
      if(moved) return;
      const w=toWorld(e.clientX,e.clientY);
      if(dt>550){ this.selectAt(w.x,w.y,true); } // long press = info
      else this.selectAt(w.x,w.y,false);
    };
    this.canvas.addEventListener('pointerup',up);
    this.canvas.addEventListener('pointercancel',up);
    this.canvas.addEventListener('wheel',(e)=>{
      e.preventDefault();
      this.camera.zoom=Math.max(0.45,Math.min(3,this.camera.zoom*(e.deltaY<0?1.12:0.9)));
      this.clampCamera();
    },{passive:false});
    window.addEventListener('resize',()=>this.resize());
    document.addEventListener('visibilitychange',()=>{ if(document.hidden&&this.inGame) this.saveTo(0); });
  },
  clampCamera(){
    const hw=window.innerWidth/2/this.camera.zoom, hh=window.innerHeight/2/this.camera.zoom;
    const mw=ZT.MAP_W*ZT.TILE, mh=ZT.MAP_H*ZT.TILE;
    this.camera.x=Math.max(hw-100,Math.min(mw-hw+100,this.camera.x));
    this.camera.y=Math.max(hh-100,Math.min(mh-hh+100,this.camera.y));
  },
  selectAt(wx, wy, info){
    // citizen first (within 22px)
    let bestC=null,bd=22;
    this.citizens.forEach(c=>{ if(c.dead) return; const d=Math.hypot(c.x-wx,c.y-wy); if(d<bd){bd=d;bestC=c;} });
    if(bestC){ ZT.UI.showInspectorCitizen(bestC); this.selected={type:'citizen',obj:bestC}; this.audio.sfx('click'); return; }
    // building
    const t=ZT.Map.worldToTile(wx,wy);
    const b=this.buildingGrid[ZT.Map.idx(t.x,t.y)];
    if(b){ ZT.UI.showInspectorBuilding(b); this.selected={type:'building',obj:b}; this.audio.sfx('click'); return; }
    // empty: close
    ZT.UI.hide('inspector');
    this.selected=null;
  },
  /* ================= update ================= */
  update(dt){
    if(!this.inGame) return;
    this.updateTime(dt);
    this.updateDirector(dt);
    this.updateOutbreak(dt);
    // buildings
    for(let i=0;i<this.buildings.length;i++) this.buildings[i].update(dt,this);
    // fire extinguish: citizens nearby help
    this.buildings.forEach(b=>{
      if(b.burning>0){
        const helper=this.citizens.some(c=>!c.dead&&Math.hypot(c.x-b.cx,c.y-b.cy)<46);
        if(helper) b.burning-=dt*2;
      }
    });
    // citizens
    for(let i=0;i<this.citizens.length;i++){
      const c=this.citizens[i];
      c.update(dt,this);
      // woodcutter/miner pseudo-work
      if((c.job==='woodcutter'||c.job==='miner') && !c.dead && c.state!=='flee' && c.state!=='leaving'){
        if(!c.work || c.work.destroyed){
          const want=c.job==='woodcutter'?4:5;
          let best=null,bd=1e9;
          for(let y=0;y<ZT.MAP_H;y++)for(let x=0;x<ZT.MAP_W;x++){
            if(ZT.Map.getTile(x,y)!==want) continue;
            const w=ZT.Map.tileToWorld(x,y);
            const d=Math.hypot(w.x-c.x,w.y-c.y);
            if(d<bd){ bd=d; best={tileX:x,tileY:y,destroyed:false,isTree:want===4,isRock:want===5,chops:0}; }
          }
          if(best){ c.work=best; c.state='toWork'; c.path=null; }
        }
      }
    }
    // zombies
    for(let i=0;i<this.zombies.length;i++) this.zombies[i].update(dt,this);
    // cleanup
    this.citizens=this.citizens.filter(c=>!c.dead);
    this.zombies=this.zombies.filter(z=>!z.dead);
    // projectiles & particles
    for(let i=0;i<this.projectiles.length;i++) this.projectiles[i].update(dt,this);
    this.projectiles=this.projectiles.filter(p=>!p.dead);
    for(let i=0;i<this.particles.length;i++) this.particles[i].update(dt);
    this.particles=this.particles.filter(p=>!p.dead);
    // zombie buffs decay
    this.zombies.forEach(z=>{ if(z.buffT>0){ z.buffT-=dt; if(z.buffT<=0) z.speedMul=1; } });
    for(let i=this.noiseEvents.length-1;i>=0;i--){ this.noiseEvents[i].t-=dt; if(this.noiseEvents[i].t<=0) this.noiseEvents.splice(i,1); }
    // ambient groans / birds
    if(Math.random()<dt*0.08 && this.zombies.length>0) this.audio.sfx('groan');
    if(Math.random()<dt*0.05 && this.isDay() && this.zombies.length===0) this.audio.sfx('bird');
    // camera follow
    if(this.camera.follow){
      if(this.camera.follow.dead) this.camera.follow=null;
      else { this.camera.x=this.camera.follow.x; this.camera.y=this.camera.follow.y; }
    }
    this.clampCamera();
    // autosave
    this.timers.autosave-=dt;
    if(this.timers.autosave<=0){ this.timers.autosave=120; this.saveTo(0); }
    // HUD
    this.timers.hud-=dt;
    if(this.timers.hud<=0){
      this.timers.hud=0.25;
      ZT.UI.renderResbar(); ZT.UI.renderClock();
      this.updateTutorial();
    }
  },
  /* ================= render ================= */
  render(){
    const g=this.ctx;
    const W=window.innerWidth, H=window.innerHeight;
    g.setTransform(this.dpr,0,0,this.dpr,0,0);
    g.fillStyle='#3d6b2f'; g.fillRect(0,0,W,H);
    if(!this.inGame) return;
    const cam=this.camera;
    g.save();
    g.translate(W/2,H/2);
    g.scale(cam.zoom,cam.zoom);
    g.translate(-cam.x,-cam.y);
    // visible range
    const vx0=cam.x-W/2/cam.zoom-40, vy0=cam.y-H/2/cam.zoom-40;
    const vx1=cam.x+W/2/cam.zoom+40, vy1=cam.y+H/2/cam.zoom+40;
    const tx0=Math.max(0,Math.floor(vx0/ZT.TILE)), ty0=Math.max(0,Math.floor(vy0/ZT.TILE));
    const tx1=Math.min(ZT.MAP_W-1,Math.floor(vx1/ZT.TILE)), ty1=Math.min(ZT.MAP_H-1,Math.floor(vy1/ZT.TILE));
    // tiles
    for(let y=ty0;y<=ty1;y++)for(let x=tx0;x<=tx1;x++){
      g.drawImage(ZT.Sprites.tileSprite(ZT.Map.getTile(x,y)), x*ZT.TILE, y*ZT.TILE);
    }
    // nest marker
    if(this.nestTile){
      g.font='18px sans-serif'; g.textAlign='center';
      g.fillText('🕳️', this.nestTile.x*ZT.TILE+16, this.nestTile.y*ZT.TILE+22);
    }
    // buildings
    this.buildings.forEach(b=>b.draw(g,this));
    // entities sorted by y
    const ents=[];
    this.citizens.forEach(c=>ents.push(c));
    this.zombies.forEach(z=>ents.push(z));
    ents.sort((a,b)=>a.y-b.y);
    ents.forEach(e=>e.draw(g,this,cam));
    // projectiles & particles
    this.projectiles.forEach(p=>p.draw(g));
    this.particles.forEach(p=>p.draw(g));
    // placement ghost
    if(this.placement){
      const def=ZT.BUILDINGS[this.placement.id];
      const gx=this.placement.x*ZT.TILE, gy=this.placement.y*ZT.TILE;
      g.globalAlpha=0.65;
      g.drawImage(ZT.Sprites.buildingSprite(this.placement.id), gx, gy);
      g.globalAlpha=1;
      g.strokeStyle=this.placement.valid?'#4caf50':'#f44336';
      g.lineWidth=3;
      g.strokeRect(gx+1,gy+1,def.w*ZT.TILE-2,def.h*ZT.TILE-2);
      g.fillStyle=this.placement.valid?'rgba(76,175,80,0.25)':'rgba(244,67,54,0.3)';
      g.fillRect(gx,gy,def.w*ZT.TILE,def.h*ZT.TILE);
    }
    // selection ring
    if(this.selected&&this.selected.obj&&!this.selected.obj.dead){
      const o=this.selected.obj;
      g.strokeStyle='#ffeb3b'; g.lineWidth=2;
      if(this.selected.type==='building') g.strokeRect(o.tileX*ZT.TILE+1,o.tileY*ZT.TILE+1,o.w*ZT.TILE-2,o.h*ZT.TILE-2);
      else g.strokeRect(o.x-10,o.y-24,20,26);
    }
    // night overlay
    const na=this.nightAlpha();
    if(na>0){
      g.fillStyle='rgba(8,12,48,'+na+')';
      g.fillRect(vx0,vy0,vx1-vx0,vy1-vy0);
      // lights
      g.globalCompositeOperation='lighter';
      const glow=ZT.Sprites.glowSprite();
      this.buildings.forEach(b=>{
        if(b.destroyed||b.ruin||!b.constructed) return;
        if(b.def.light||b.isHousing()||b.id==='center'){
          g.globalAlpha=0.5;
          g.drawImage(glow,b.cx-32,b.cy-32,64,64);
        }
      });
      g.globalAlpha=1;
      g.globalCompositeOperation='source-over';
    }
    // fog
    if(this.weather==='fog'){
      g.fillStyle='rgba(200,210,220,0.22)';
      g.fillRect(vx0,vy0,vx1-vx0,vy1-vy0);
    }
    // snow
    if(this.weather==='snow'){
      g.fillStyle='rgba(255,255,255,0.85)';
      const n=70;
      for(let i=0;i<n;i++){
        const rx=vx0+((i*97+this.time*40)%(vx1-vx0));
        const ry=vy0+((i*61+this.time*60)%(vy1-vy0));
        g.fillRect(rx,ry,2,2);
      }
    }
    // lightning flash during storm
    if(this.weather==='storm' && Math.random()<0.02){
      g.fillStyle='rgba(255,255,255,0.35)';
      g.fillRect(vx0,vy0,vx1-vx0,vy1-vy0);
      this.audio && this.audio.sfx('thunder');
    }
    // rain
    if(this.weather==='rain'||this.weather==='storm'){
      g.strokeStyle='rgba(150,180,255,0.5)'; g.lineWidth=1;
      g.beginPath();
      const n=this.weather==='storm'?90:50;
      for(let i=0;i<n;i++){
        const rx=vx0+((i*97+this.time*300)%(vx1-vx0));
        const ry=vy0+((i*61+this.time*500)%(vy1-vy0));
        g.moveTo(rx,ry); g.lineTo(rx-3,ry+10);
      }
      g.stroke();
    }
    g.restore();
  },
  loop(ts){
    requestAnimationFrame(this.loop);
    if(!this._last) this._last=ts;
    let dt=(ts-this._last)/1000;
    this._last=ts;
    if(dt>0.05) dt=0.05;
    if(!this.paused) this.update(dt*this.speed);
    this.render();
  }
};

/* boot — run once, as early as possible (DOMContentLoaded / load / immediately) */
(function(){
  var booted=false;
  function start(){
    if(booted) return; booted=true;
    try{
      var canvas=document.getElementById('game');
      if(!canvas) throw new Error('Canvas #game not found');
      var game=new ZT.Game(canvas);
      ZT.UI.bind(game);
      ZT.game=game;
      var hasSave=false;
      try{ hasSave=ZT.Save.hasSave(0); }catch(e){ hasSave=false; }
      document.getElementById('btn-continue').style.opacity=hasSave?'1':'0.5';
    }catch(e){
      console.error('Boot failed:', e);
      if(window.__ztError) window.__ztError('Ошибка запуска: '+(e&&e.message?e.message:e));
      else alert('Ошибка запуска: '+(e&&e.message?e.message:e));
    }
  }
  if(document.readyState==='loading'){
    document.addEventListener('DOMContentLoaded',start);
    window.addEventListener('load',start);
  } else {
    start();
  }
})();
