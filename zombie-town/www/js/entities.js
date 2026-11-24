/* Entities: Citizen, Zombie, Projectile, Particle + BFS pathfinding. */
'use strict';
var ZT = window.ZT;

/* ---------- Pathfinding (BFS with parent tracking) ---------- */
ZT.Pathfind = (function(){
  const W=ZT.MAP_W, H=ZT.MAP_H;
  const dist=new Int32Array(W*H), parent=new Int32Array(W*H), queue=new Int32Array(W*H);
  function bfs(sx,sy,tx,ty,forZombie,grid){
    if(!ZT.Map.inBounds(sx,sy)||!ZT.Map.inBounds(tx,ty)) return null;
    // if target blocked, find nearest walkable neighbor
    if(!ZT.Map.isWalkable(tx,ty,forZombie,grid)){
      let found=null;
      outer: for(let r=1;r<=3;r++)for(let dy=-r;dy<=r;dy++)for(let dx=-r;dx<=r;dx++){
        const nx=tx+dx, ny=ty+dy;
        if(ZT.Map.isWalkable(nx,ny,forZombie,grid)){ found={x:nx,y:ny}; break outer; }
      }
      if(!found) return null;
      tx=found.x; ty=found.y;
    }
    dist.fill(-1);
    let qh=0,qt=0;
    const s=ZT.Map.idx(sx,sy), t=ZT.Map.idx(tx,ty);
    dist[s]=0; parent[s]=-1; queue[qt++]=s;
    while(qh<qt){
      const cur=queue[qh++]; if(cur===t) break;
      const cx=cur%W, cy=(cur/W)|0, d=dist[cur]+1;
      const nb=[[1,0],[-1,0],[0,1],[0,-1]];
      for(let i=0;i<4;i++){
        const nx=cx+nb[i][0], ny=cy+nb[i][1];
        if(!ZT.Map.inBounds(nx,ny)) continue;
        const ni=ZT.Map.idx(nx,ny);
        if(dist[ni]!==-1) continue;
        if(!ZT.Map.isWalkable(nx,ny,forZombie,grid)) continue;
        dist[ni]=d; parent[ni]=cur; queue[qt++]=ni;
      }
    }
    if(dist[t]===-1) return null;
    const path=[]; let cur=t;
    while(cur!==-1 && cur!==s){ path.push({x:cur%W,y:(cur/W)|0}); cur=parent[cur]; }
    path.reverse();
    return path;
  }
  return { bfs };
})();

/* ---------- Shared agent movement helper ---------- */
function moveAlongPath(agent, dt, speed, game){
  if(!agent.path || agent.pathIdx>=agent.path.length) return true; // arrived
  const wp = agent.path[agent.pathIdx];
  const target = ZT.Map.tileToWorld(wp.x, wp.y);
  const dx = target.x-agent.x, dy = target.y-agent.y;
  const d = Math.hypot(dx,dy);
  const step = speed*dt;
  if(d < Math.max(3, step)){
    agent.x=target.x; agent.y=target.y; agent.pathIdx++;
  } else {
    agent.x += dx/d*step; agent.y += dy/d*step;
    agent.dir = dx>=0?1:-1;
  }
  // road speed bonus
  const t = ZT.Map.getTile(wp.x, wp.y);
  if(t===2) agent.roadBonus = 1.5; else agent.roadBonus = 1;
  agent.moving = true;
  return agent.pathIdx>=agent.path.length;
}
function setPath(agent, tx, ty, forZombie, game){
  const s = ZT.Map.worldToTile(agent.x, agent.y);
  agent.path = ZT.Pathfind.bfs(s.x, s.y, tx, ty, forZombie, game.buildingGrid);
  agent.pathIdx = 0;
  agent.pathTarget = {x:tx,y:ty};
  return !!agent.path;
}
function separation(agent, list, radius, dt){
  // cheap push-away from neighbors (sampled)
  let px=0, py=0, n=0;
  for(let i=0;i<list.length;i++){
    const o=list[i]; if(o===agent||o.dead) continue;
    const dx=agent.x-o.x, dy=agent.y-o.y;
    const d2=dx*dx+dy*dy;
    if(d2<radius*radius && d2>0.01){ const d=Math.sqrt(d2); px+=dx/d; py+=dy/d; n++; }
  }
  if(n){ agent.x+=px*dt*14; agent.y+=py*dt*14; }
}

/* ---------- Citizen ---------- */
let citizenId = 1;
ZT.Citizen = function(x,y,opts){
  opts=opts||{};
  this.id = citizenId++;
  this.x=x; this.y=y; this.dir=1; this.moving=false;
  this.name = opts.name || ZT.makeName();
  this.age = opts.age || (18+((Math.random()*50)|0));
  this.child = this.age<16;
  this.shirt = ['#e53935','#1e88e5','#43a047','#fb8c00','#8e24aa','#00acc1','#f4511e','#7cb342','#3949ab','#c0ca33'][((Math.random()*10)|0)];
  this.skin = ['#ffccbc','#ffe0b2','#d7ccc8','#8d6e63','#5d4037'][((Math.random()*5)|0)];
  this.hp=100; this.maxHp=100;
  this.hunger=80; this.thirst=80; this.rest=80; this.happiness=70;
  this.job='idle'; this.home=null; this.work=null;
  this.state='arrive'; this.stateT=0; this.thinkT=Math.random()*0.5;
  this.path=null; this.pathIdx=0; this.pathTarget=null;
  this.infected=0; this.infectedT=0; this.dead=false; this.leaving=false;
  this.friend=null; this.weapon = this.age>=16 ? (Math.random()<0.3?'bat':Math.random()<0.5?'spear':'fists') : 'fists';
  this.combatSkill = 0.5+Math.random()*0.8;
  this.bob = Math.random()*6;
  this.targetZombie=null; this.attackCd=0;
  this.follow=false;
  this.exp=0; this.expT=0; this.sneak=false; this.hidden=false; // adaptation: experience, stealth, hiding
};
ZT.makeName = function(){
  const pool = (ZT.settings.lang==='en') ? ZT.NAMES_EN : ZT.NAMES_RU;
  return pool[(Math.random()*pool.length)|0] + ' ' + ZT.SURNAMES[(Math.random()*ZT.SURNAMES.length)|0];
};
ZT.Citizen.prototype = {
  speed(){ return (this.child?30:38) * (this.infected?0.8:1) * (this.sneak?0.55:1); },
  visibility(){
    if(this.hidden) return 0.12;
    if(this.sneak) return 0.45;
    if(this.state==='sleep') return 0.5;
    return 1.0;
  },
  needsUpdate(dt, game){
    this.hunger=Math.max(0,this.hunger-dt*0.55);
    this.thirst=Math.max(0,this.thirst-dt*0.7);
    if(this.state==='sleep') this.rest=Math.min(100,this.rest+dt*9);
    else this.rest=Math.max(0,this.rest-dt*0.35);
    if(this.infected>0){ this.infectedT-=dt; if(this.infectedT<=0){ this.turnZombie(game); } }
    // happiness from needs + housing + safety
    const needAvg=(this.hunger+this.thirst+this.rest)/3;
    let h=needAvg*0.6 + (this.home?25:0) + (this.friend?5:0);
    if(this.infected>0) h-=40;
    this.happiness += (Math.max(0,Math.min(100,h))-this.happiness)*dt*0.2;
    if(this.hp<this.maxHp && !this.infected) this.hp=Math.min(this.maxHp,this.hp+dt*0.4);
    this.expT+=dt;
    if(this.expT>10){ this.expT=0; this.exp=Math.min(100,this.exp+1); }
  },
  think(game){
    // decide what to do
    const danger = game.zombies.length>0 && game.nearestZombieDist(this)<160;
    if(danger && this.job!=='guard'){
      this.hidden=false;
      this.flee(game); return;
    }
    // stay hidden while zombies are near
    if(this.state==='hide'){
      const zd=game.nearestZombieDist(this);
      if(zd>320 || !this.home || this.home.destroyed || this.hunger<20 || this.thirst<20){ this.hidden=false; this.state='socialize'; }
      else return;
    }
    // hide in house when a zombie is approaching but not yet on top of us
    if(game.zombies.length>0 && this.job!=='guard' && this.home && !this.home.destroyed){
      const zd=game.nearestZombieDist(this);
      const hd=Math.hypot(this.home.cx-this.x,this.home.cy-this.y);
      if(zd<300 && zd>=160 && hd<200){
        this.state='hide'; this.hidden=true;
        setPath(this, this.home.tileX, this.home.tileY, false, game);
        return;
      }
    }
    // sneak (slow, quiet) when zombies roam nearby
    this.sneak = game.zombies.length>0 && game.nearestZombieDist(this)<430 && this.job!=='guard';
    if(this.infected>0 && this.state!=='flee'){ this.flee(game); return; }
    if(this.hunger<35){ this.goEat(game); return; }
    if(this.thirst<35){ this.goDrink(game); return; }
    if(this.rest<25){ this.goSleep(game); return; }
    if(this.job==='guard'){ this.guardPatrol(game); return; }
    if(this.job==='builder' && game.constructionSites.length){ this.goBuild(game); return; }
    if(this.work && this.job!=='idle'){ this.goWork(game); return; }
    // socialize near center / friend
    if(!this.path || this.pathIdx>=this.path.length){
      const c=game.centerTile;
      const tx=c.x+(((Math.random()*9)|0)-4), ty=c.y+(((Math.random()*7)|0)-3);
      setPath(this, tx, ty, false, game);
      this.state='socialize';
    }
  },
  flee(game){
    if(this.state==='flee' && this.path && this.pathIdx<this.path.length) return;
    this.state='flee';
    const safe = game.safehouseTile() || game.centerTile;
    setPath(this, safe.x, safe.y, false, game);
    game.audio && game.audio.sfx('alarm-short');
  },
  goEat(game){
    const b = game.findBuilding('farm') || game.findBuilding('kitchen') || game.findBuilding('center');
    if(!b){ this.state='socialize'; return; }
    this.state='toEat'; this.eatTarget=b;
    setPath(this, b.tileX, b.tileY, false, game);
  },
  goDrink(game){
    const b = game.findBuilding('well') || game.findBuilding('center');
    if(!b){ this.state='socialize'; return; }
    this.state='toDrink'; this.drinkTarget=b;
    setPath(this, b.tileX, b.tileY, false, game);
  },
  goSleep(game){
    if(!this.home){ this.state='socialize'; return; }
    this.state='toSleep';
    setPath(this, this.home.tileX, this.home.tileY, false, game);
  },
  goWork(game){
    if(!this.work || this.work.destroyed){ this.state='socialize'; return; }
    this.state='toWork';
    setPath(this, this.work.tileX, this.work.tileY, false, game);
  },
  goBuild(game){
    const site = game.constructionSites[0];
    if(!site){ this.state='socialize'; return; }
    this.state='toBuild'; this.buildTarget=site;
    setPath(this, site.tileX, site.tileY, false, game);
  },
  guardPatrol(game){
    // stay near walls/towers or center; engage zombies in range
    const z = game.nearestZombie(this, 130);
    if(z){ this.targetZombie=z; this.state='fight'; return; }
    if(!this.path || this.pathIdx>=this.path.length){
      const post = game.guardPost(this);
      setPath(this, post.x, post.y, false, game);
      this.state='patrol';
    }
  },
  update(dt, game){
    if(this.dead) return;
    this.bob += dt*8;
    this.needsUpdate(dt, game);
    this.thinkT-=dt;
    if(this.thinkT<=0){ this.thinkT=0.6+Math.random()*0.4; this.think(game); }
    this.attackCd-=dt;
    const spd = this.speed()*(this.roadBonus||1)*(this.state==='flee'?1.35:1);
    if(this.state==='hide' && (!this.path || this.pathIdx>=this.path.length)) this.hidden=true;
    this.moving=false;
    switch(this.state){
      case 'arrive': {
        if(!this.path){ const c=game.centerTile; setPath(this,c.x,c.y,false,game); }
        if(moveAlongPath(this,dt,spd,game)){ this.state='socialize'; game.onCitizenArrived(this); }
        break; }
      case 'toEat': {
        if(moveAlongPath(this,dt,spd,game)){
          if(game.resources.food>0){ game.resources.food--; this.hunger=100; game.audio&&game.audio.sfx('eat'); }
          else { this.hunger=Math.max(0,this.hunger-10); }
          this.state='socialize';
        } break; }
      case 'toDrink': {
        if(moveAlongPath(this,dt,spd,game)){
          if(game.resources.water>0){ game.resources.water--; this.thirst=100; game.audio&&game.audio.sfx('drink'); }
          this.state='socialize';
        } break; }
      case 'toSleep': {
        if(moveAlongPath(this,dt,spd,game)){ this.state='sleep'; }
        break; }
      case 'sleep': {
        if(this.rest>=99 || this.thirst<15){ this.state='socialize'; }
        break; }
      case 'toWork': {
        if(moveAlongPath(this,dt,spd,game)){ this.state='work'; }
        break; }
      case 'work': {
        if(!this.work||this.work.destroyed){ this.state='socialize'; break; }
        this.workT=(this.workT||0)+dt;
        if(this.workT>2){ this.workT=0; game.produceAt(this.work, this); }
        if(this.hunger<20||this.thirst<20||this.rest<10) this.state='socialize';
        break; }
      case 'toBuild': {
        if(moveAlongPath(this,dt,spd,game)){
          if(this.buildTarget && !this.buildTarget.destroyed){
            this.state='build';
          } else this.state='socialize';
        } break; }
      case 'build': {
        if(!this.buildTarget||this.buildTarget.destroyed){ this.state='socialize'; break; }
        this.buildTarget.progress += dt*this.combatSkill;
        if(Math.random()<dt*3) game.spawnParticles(this.x,this.y-10,'#ffcc80',2);
        if(Math.random()<dt*1.5) game.audio&&game.audio.sfx('hammer');
        if(this.buildTarget.progress>=this.buildTarget.def.buildTime) game.completeConstruction(this.buildTarget);
        break; }
      case 'patrol': case 'socialize': {
        moveAlongPath(this,dt,spd,game);
        break; }
      case 'flee': {
        moveAlongPath(this,dt,spd,game);
        // cornered? fight back with whatever is at hand
        const zc=game.nearestZombie(this,42);
        if(zc && this.attackCd<=0){
          this.attackCd=1.0;
          const dmg=(this.weapon==='spear'?12:this.weapon==='bat'?9:5)*this.combatSkill*(1+this.exp*0.005);
          game.damageZombie(zc,dmg,this);
          game.audio&&game.audio.sfx('hit');
          game.spawnParticles(zc.x,zc.y,'#b71c1c',2);
        }
        break; }
      case 'hide': {
        if(this.path && this.pathIdx<this.path.length) moveAlongPath(this,dt,spd,game);
        else this.hidden=true;
        break; }
      case 'fight': {
        const z=this.targetZombie;
        if(!z||z.dead){ this.targetZombie=null; this.state='socialize'; break; }
        const d=Math.hypot(z.x-this.x,z.y-this.y);
        if(d>46){ moveAlongPath(this,dt,spd*1.2,game); if(!this.path||this.pathIdx>=this.path.length) setPath(this,ZT.Map.worldToTile(z.x,z.y).x,ZT.Map.worldToTile(z.x,z.y).y,false,game); }
        else if(this.attackCd<=0){
          this.attackCd=0.9;
          const dmg=(this.weapon==='spear'?16:this.weapon==='bat'?12:7)*this.combatSkill*(1+this.exp*0.005);
          game.damageZombie(z,dmg,this);
          game.audio&&game.audio.sfx('hit');
          game.spawnParticles(z.x,z.y,'#b71c1c',3);
        }
        break; }
      case 'leaving': {
        if(!this.path){ const e=ZT.Map.edgePoint(); setPath(this,e.x,e.y,false,game); }
        if(moveAlongPath(this,dt,spd,game)){ this.dead=true; game.onCitizenLeft(this); }
        break; }
    }
    separation(this, game.citizens, 10, dt);
    // camera follow handled centrally in game.update via camera.follow property
  },
  takeDamage(dmg, fromZombie, game){
    if(this.dead) return;
    this.hp-=dmg;
    game.audio&&game.audio.sfx('hurt');
    if(fromZombie && this.infected<=0 && Math.random()<(this.job==='guard'?0.06:0.12)){
      this.infected=1; this.infectedT=22+Math.random()*10;
      game.notify('🦠 '+this.name+' заражён! Нужна клиника!', 'bad');
      game.audio&&game.audio.sfx('infect');
    }
    if(this.hp<=0){ this.die(game); }
    else if(this.job!=='guard' && this.state!=='flee'){ this.flee(game); }
  },
  die(game){
    this.dead=true;
    game.onCitizenDied(this);
  },
  turnZombie(game){
    // handled by game (needs spawn context)
    if(game) game.turnCitizen(this);
  },
  draw(g, game, cam){
    const frame = this.moving ? (Math.floor(this.bob)%2) : 0;
    const spr = ZT.Sprites.personSprite(this.shirt, this.skin, {zombie:this.infected>0, child:this.child, infected:this.infected>0}, frame);
    const bob = this.moving ? Math.sin(this.bob)*1.5 : 0;
    const sc = this.child?0.7:1;
    const w=16*sc, h=20*sc;
    g.drawImage(spr, this.x-w/2, this.y-h+4+bob, w, h);
    // job icon
    if(this.job!=='idle' && this.state!=='flee'){
      g.font='10px sans-serif'; g.textAlign='center';
      g.fillText(ZT.JOBS[this.job].icon, this.x, this.y-h-2+bob);
    }
    if(this.infected>0){
      g.font='11px sans-serif'; g.textAlign='center'; g.fillText('🦠', this.x, this.y-h-12+bob);
    }
    if(this.hp<this.maxHp){
      g.fillStyle='#000'; g.fillRect(this.x-8,this.y+3,16,3);
      g.fillStyle='#ef5350'; g.fillRect(this.x-8,this.y+3,16*(this.hp/this.maxHp),3);
    }
    if(this.follow){ g.strokeStyle='#ffeb3b'; g.lineWidth=2; g.strokeRect(this.x-10,this.y-24,20,26); }
  }
};

/* ---------- Zombie ---------- */
let zombieId = 1;
ZT.Zombie = function(x,y,type){
  this.id=zombieId++;
  this.x=x; this.y=y; this.dir=1; this.moving=false;
  this.type=type||'walker';
  this.def=ZT.ZOMBIES[this.type];
  this.hp=this.def.hp; this.maxHp=this.def.hp;
  this.state='seek'; this.stateT=0; this.thinkT=Math.random()*0.5;
  this.path=null; this.pathIdx=0;
  this.target=null; this.targetBuilding=null;
  this.attackCd=0; this.dead=false;
  this.bob=Math.random()*6; this.speedMul=1;
  this.screamT=0;
  this.seeR = type==='screamer'?230 : type==='runner'?185 : 170; // vision radius
  this.lastKnown=null; this.loseT=0; this.searchPause=0; // search memory
};
ZT.Zombie.prototype = {
  speed(game){
    let s=this.def.speed*this.speedMul;
    if(game && game.isNight()) s*=1.25;
    if(game && game.weather==='storm') s*=1.15;
    if(game && game.isDay()) s*=0.85;
    return s;
  },
  think(game){
    // 1) look for VISIBLE citizens (stealth/hiding/experience reduce detection)
    let best=null, bestD=1e9;
    const nightMul = game.isNight()?1.15:0.85;
    for(let i=0;i<game.citizens.length;i++){
      const c=game.citizens[i]; if(c.dead||c.leaving) continue;
      const vis = c.visibility ? c.visibility() : 1;
      const effR = this.seeR * vis * (1-(c.exp||0)*0.003) * nightMul;
      const d=Math.hypot(c.x-this.x,c.y-this.y);
      if(d<effR && d<bestD){ bestD=d; best=c; }
    }
    if(best){ this.target=best; this.targetBuilding=null; this.lastKnown={x:best.x,y:best.y}; this.loseT=0; return; }
    // 2) keep chasing a recently seen target within extended range
    if(this.target && !this.target.dead){
      const d=Math.hypot(this.target.x-this.x,this.target.y-this.y);
      if(d<this.seeR*1.6){ this.lastKnown={x:this.target.x,y:this.target.y}; return; }
      this.target=null; // lost the trail -> search last known position
    }
    // 3) investigate noise (construction, gunshots, alarm, destruction)
    const nz = game.nearestNoise ? game.nearestNoise(this, 340) : null;
    if(nz){ this.lastKnown={x:nz.x,y:nz.y}; this.target=null; this.targetBuilding=null; return; }
    // 4) search last known position, then sweep the town (houses first)
    if(this.lastKnown){ this.target=null; this.targetBuilding=null; return; }
    // otherwise attack nearest building
    let bb=null, bd=1e9;
    for(let i=0;i<game.buildings.length;i++){
      const b=game.buildings[i]; if(b.destroyed||b.ruin) continue;
      if(b.id==='center') continue;
      const d=Math.hypot(b.cx-this.x,b.cy-this.y);
      if(d<bd){ bd=d; bb=b; }
    }
    if(bb){ this.targetBuilding=bb; this.target=null; return; }
    this.target=null; this.targetBuilding=null;
  },
  update(dt, game){
    if(this.dead) return;
    this.bob+=dt*6;
    this.thinkT-=dt;
    if(this.thinkT<=0){ this.thinkT=0.8+Math.random()*0.5; this.think(game); }
    this.attackCd-=dt;
    this.moving=false;
    const spd=this.speed(game);
    // attack citizen
    if(this.target && !this.target.dead){
      const d=Math.hypot(this.target.x-this.x,this.target.y-this.y);
      if(d<=this.def.range+6){
        if(this.attackCd<=0){
          this.attackCd=this.def.attackRate;
          this.target.takeDamage(this.def.dmg, true, game);
          game.spawnParticles(this.target.x,this.target.y,'#b71c1c',3);
          game.audio&&game.audio.sfx('bite');
        }
        return;
      }
      // chase: direct steering, but route around walls/water via path when blocked
      const dx=this.target.x-this.x, dy=this.target.y-this.y, dd=Math.hypot(dx,dy)||1;
      const nx=this.x+dx/dd*spd*dt, ny=this.y+dy/dd*spd*dt;
      const nt=ZT.Map.worldToTile(nx,ny);
      const blocked=!ZT.Map.isWalkable(nt.x,nt.y,true,game.buildingGrid);
      if(blocked){
        if(!this.path||this.pathIdx>=this.path.length){
          const tt=ZT.Map.worldToTile(this.target.x,this.target.y);
          setPath(this,tt.x,tt.y,true,game);
        }
        if(this.path&&this.pathIdx<this.path.length) moveAlongPath(this,dt,spd,game);
        else { this.x=nx; this.y=ny; }
      } else {
        this.x=nx; this.y=ny; this.path=null;
      }
      this.dir=dx>=0?1:-1; this.moving=true;
    }
    // attack building
    else if(this.targetBuilding && !this.targetBuilding.destroyed){
      const b=this.targetBuilding;
      const d=Math.hypot(b.cx-this.x,b.cy-this.y);
      const reach=Math.max(b.def.w,b.def.h)*ZT.TILE/2+26;
      if(d<=reach){
        if(this.attackCd<=0){
          this.attackCd=this.def.attackRate;
          game.damageBuilding(b,this.def.dmg*1.6,this);
          game.audio&&game.audio.sfx('smash');
          game.spawnParticles(b.cx,b.cy,'#a1887f',4);
        }
        return;
      }
      if(!this.path||this.pathIdx>=this.path.length){
        setPath(this,b.tileX,b.tileY,true,game);
      }
      moveAlongPath(this,dt,spd,game);
    }
    // search last known position (where the trail went cold)
    else if(this.lastKnown){
      if(this.searchPause>0){ this.searchPause-=dt; this.moving=false; }
      else {
        const d=Math.hypot(this.lastKnown.x-this.x,this.lastKnown.y-this.y);
        if(d<22){
          this.searchPause=1.5; // look around, then give up and sweep elsewhere
          this.lastKnown=null; this.path=null;
        } else {
          if(!this.path||this.pathIdx>=this.path.length){
            const lt=ZT.Map.worldToTile(this.lastKnown.x,this.lastKnown.y);
            setPath(this,lt.x,lt.y,true,game);
          }
          moveAlongPath(this,dt,spd*0.85,game);
        }
      }
    }
    // wander / sweep toward town center, pausing to look around
    else {
      if(this.searchPause>0){ this.searchPause-=dt; this.moving=false; }
      else {
        if(!this.path||this.pathIdx>=this.path.length){
          const c=game.centerTile;
          setPath(this,c.x+(((Math.random()*11)|0)-5),c.y+(((Math.random()*9)|0)-4),true,game);
          if(Math.random()<0.25) this.searchPause=1.2;
        }
        moveAlongPath(this,dt,spd*0.7,game);
      }
    }
    // spitter ranged attack
    if(this.def.ranged && this.target && !this.target.dead){
      const d=Math.hypot(this.target.x-this.x,this.target.y-this.y);
      if(d<this.def.range && this.attackCd<=0){
        this.attackCd=this.def.attackRate;
        game.spawnProjectile(this.x,this.y,this.target.x,this.target.y,'#7cb342',this.def.dmg);
      }
    }
    // screamer buff
    if(this.type==='screamer'){
      this.screamT-=dt;
      if(this.screamT<=0){ this.screamT=6; game.buffZombiesNear(this,60,1.3,3); game.audio&&game.audio.sfx('scream'); }
    }
    separation(this, game.zombies, 12, dt);
  },
  takeDamage(dmg, attacker){
    if(this.dead) return;
    this.hp-=dmg;
    if(this.hp<=0){ this.dead=true; }
  },
  draw(g, game, cam){
    const frame = this.moving ? (Math.floor(this.bob)%2) : 0;
    const spr=ZT.Sprites.zombieSprite(this.type, frame);
    const bob=this.moving?Math.sin(this.bob)*2:0;
    const w=18,h=22;
    g.drawImage(spr,this.x-w/2,this.y-h+4+bob,w,h);
    if(this.hp<this.maxHp){
      g.fillStyle='#000'; g.fillRect(this.x-9,this.y+3,18,3);
      g.fillStyle='#8bc34a'; g.fillRect(this.x-9,this.y+3,18*(this.hp/this.maxHp),3);
    }
  }
};

/* ---------- Projectile & Particle ---------- */
ZT.Projectile = function(x,y,tx,ty,color,dmg){
  this.x=x; this.y=y; this.tx=tx; this.ty=ty; this.color=color||'#ffcc80'; this.dmg=dmg||10;
  this.speed=220; this.dead=false; this.life=1.2;
  const dx=tx-x, dy=ty-y, d=Math.hypot(dx,dy)||1;
  this.vx=dx/d*this.speed; this.vy=dy/d*this.speed;
};
ZT.Projectile.prototype = {
  update(dt, game){
    this.x+=this.vx*dt; this.y+=this.vy*dt; this.life-=dt;
    if(this.life<=0){ this.dead=true; return; }
    // hit zombie?
    for(let i=0;i<game.zombies.length;i++){
      const z=game.zombies[i]; if(z.dead) continue;
      if(Math.hypot(z.x-this.x,z.y-this.y)<12){
        game.damageZombie(z,this.dmg,null);
        game.spawnParticles(z.x,z.y,this.color,3);
        this.dead=true; return;
      }
    }
  },
  draw(g){
    g.fillStyle=this.color; g.fillRect(this.x-2,this.y-2,4,4);
    g.fillStyle='#fff'; g.fillRect(this.x-1,this.y-1,2,2);
  }
};
ZT.Particle = function(x,y,color,life,size){
  this.x=x; this.y=y; this.vx=(Math.random()-0.5)*40; this.vy=-20-Math.random()*30;
  this.color=color; this.life=life||0.7; this.maxLife=this.life; this.size=size||3; this.dead=false;
};
ZT.Particle.prototype = {
  update(dt){ this.x+=this.vx*dt; this.y+=this.vy*dt; this.vy+=60*dt; this.life-=dt; if(this.life<=0) this.dead=true; },
  draw(g){ g.globalAlpha=Math.max(0,this.life/this.maxLife); g.fillStyle=this.color; g.fillRect(this.x-this.size/2,this.y-this.size/2,this.size,this.size); g.globalAlpha=1; }
};
