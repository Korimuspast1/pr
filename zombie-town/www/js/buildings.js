/* Buildings: construction, production, damage, repair, upgrade, ruins. */
'use strict';
const ZT = window.ZT;

let buildingId = 1;
ZT.Building = function(id, tileX, tileY){
  this.uid = buildingId++;
  this.id = id;
  this.def = ZT.BUILDINGS[id];
  this.tileX = tileX; this.tileY = tileY;
  this.w = this.def.w; this.h = this.def.h;
  this.cx = tileX*ZT.TILE + this.w*ZT.TILE/2;
  this.cy = tileY*ZT.TILE + this.h*ZT.TILE/2;
  this.hp = this.def.hp; this.maxHp = this.def.hp;
  this.progress = 0;               // construction progress (0..buildTime)
  this.constructed = this.def.buildTime===0;
  this.destroyed = false; this.ruin = false;
  this.workers = [];               // citizen refs
  this.residents = [];             // for housing
  this.prodT = 0;
  this.burning = 0;                // fire timer
  this.infectedB = false;
  this.level = 1;
  this.fireT = 0;
};
ZT.Building.prototype = {
  isHousing(){ return this.def.cat==='housing'; },
  freeBeds(){ return this.isHousing() ? (this.def.capacity - this.residents.filter(c=>!c.dead).length) : 0; },
  canAfford(game){
    for(const r in this.def.cost){ if((game.resources[r]||0) < this.def.cost[r]) return false; }
    return true;
  },
  payCost(game){
    for(const r in this.def.cost){ game.resources[r]-=this.def.cost[r]; }
  },
  costText(){
    const parts=[];
    for(const r in this.def.cost) parts.push(ZT.RES[r].icon+' '+this.def.cost[r]);
    return parts.length?parts.join(' '):'—';
  },
  update(dt, game){
    if(this.destroyed) return;
    // fire
    if(this.burning>0){
      this.burning-=dt;
      this.hp-=dt*8;
      if(Math.random()<dt*4) game.spawnParticles(this.cx,this.cy-10,'#ff7043',2);
      if(Math.random()<dt*2) game.spawnParticles(this.cx,this.cy-10,'#ffcc80',1);
      if(this.hp<=0){ this.destroy(game,'fire'); return; }
    }
    if(!this.constructed) return;
    // production (needs workers if required)
    if(this.def.produces){
      const needWorkers=this.def.workers||0;
      const staffed = needWorkers===0 || this.workers.filter(c=>!c.dead).length>=needWorkers;
      if(staffed){
        this.prodT+=dt;
        if(this.prodT>=3){
          this.prodT=0;
          game.produceAt(this, null);
        }
      }
    }
    // turret auto-fire
    if(this.id==='turret'){
      this.turretCd=(this.turretCd||0)-dt;
      if(this.turretCd<=0){
        const z=game.nearestZombie(this, 150);
        if(z && game.resources.ammo>0){
          game.resources.ammo--;
          this.turretCd=0.8;
          game.spawnProjectile(this.cx,this.cy-14,z.x,z.y,'#ffcc80',18);
          game.audio&&game.audio.sfx('shoot');
        }
      }
    }
    // tower guard shooting handled via assigned guard citizen
    // spikes damage zombies standing on them
    if(this.id==='spikes'){
      this.spikeT=(this.spikeT||0)-dt;
      if(this.spikeT<=0){
        this.spikeT=0.5;
        for(let i=0;i<game.zombies.length;i++){
          const z=game.zombies[i];
          if(!z.dead && Math.abs(z.x-this.cx)<20 && Math.abs(z.y-this.cy)<20){
            game.damageZombie(z,6,null);
          }
        }
      }
    }
    // clinic heals infected nearby
    if(this.id==='clinic'){
      this.clinicT=(this.clinicT||0)-dt;
      if(this.clinicT<=0){
        this.clinicT=1;
        for(let i=0;i<game.citizens.length;i++){
          const c=game.citizens[i];
          if(c.dead) continue;
          if(Math.hypot(c.x-this.cx,c.y-this.cy)<70){
            if(c.infected>0 && game.resources.medicine>0){
              game.resources.medicine--; c.infected=0;
              game.notify('💊 '+c.name+' вылечен!', 'good');
              game.audio&&game.audio.sfx('heal');
            } else if(c.hp<c.maxHp){ c.hp=Math.min(c.maxHp,c.hp+4); }
          }
        }
      }
    }
  },
  damage(dmg, game, attacker){
    if(this.destroyed||this.ruin) return;
    if(!this.constructed) return;
    this.hp-=dmg;
    if(this.hp<=0) this.destroy(game,'zombie');
  },
  destroy(game, cause){
    this.destroyed=true; this.ruin=true;
    game.onBuildingDestroyed(this, cause);
  },
  repairCost(){
    const missing = 1 - this.hp/this.maxHp;
    return { wood: Math.max(1,Math.round(missing*10)), stone: Math.max(1,Math.round(missing*8)) };
  },
  draw(g, game){
    if(this.ruin){
      g.drawImage(ZT.Sprites.ruinSprite(this.w,this.h), this.tileX*ZT.TILE, this.tileY*ZT.TILE);
      return;
    }
    if(!this.constructed){
      g.drawImage(ZT.Sprites.constructionSprite(this.w,this.h), this.tileX*ZT.TILE, this.tileY*ZT.TILE);
      // progress bar
      const pct=this.progress/this.def.buildTime;
      g.fillStyle='#000'; g.fillRect(this.cx-20,this.cy-8,40,6);
      g.fillStyle='#ffcc80'; g.fillRect(this.cx-20,this.cy-8,40*pct,6);
      return;
    }
    const spr=ZT.Sprites.buildingSprite(this.id);
    g.drawImage(spr, this.tileX*ZT.TILE, this.tileY*ZT.TILE);
    // smoke for kitchen/workshop
    if((this.id==='kitchen'||this.id==='workshop') && Math.random()<0.02){
      game.spawnParticles(this.cx+6,this.tileY*ZT.TILE+4,'#bdbdbd',1);
    }
    // fire overlay
    if(this.burning>0){
      g.font='16px sans-serif'; g.textAlign='center';
      g.fillText('🔥', this.cx, this.cy-6+Math.sin(game.time*10)*2);
    }
    // damage flash / hp bar
    if(this.hp<this.maxHp){
      g.fillStyle='#000'; g.fillRect(this.cx-16,this.tileY*ZT.TILE-6,32,4);
      g.fillStyle=this.hp/this.maxHp>0.4?'#66bb6a':'#ef5350';
      g.fillRect(this.cx-16,this.tileY*ZT.TILE-6,32*(this.hp/this.maxHp),4);
    }
    // upgrade arrow for housing
    if(this.isHousing() && this.def.upgradesTo && this.residents.length>=this.def.capacity-1){
      g.font='12px sans-serif'; g.textAlign='center'; g.fillText('⬆', this.cx, this.tileY*ZT.TILE-10);
    }
  }
};
