/* Headless smoke test: stubs DOM, runs the real game code. */
'use strict';
const fs = require('fs');
const path = require('path');

function makeCtx(){
  return {
    fillStyle:'', strokeStyle:'', globalAlpha:1, globalCompositeOperation:'source-over',
    font:'', textAlign:'', lineWidth:1, imageSmoothingEnabled:false,
    setTransform(){}, fillRect(){}, drawImage(){}, save(){}, restore(){},
    translate(){}, scale(){}, beginPath(){}, moveTo(){}, lineTo(){}, stroke(){},
    strokeRect(){}, fillText(){}, createRadialGradient(){ return { addColorStop(){} }; },
  };
}
function makeEl(){
  const el = {
    classList:{ _s:new Set(), add(c){this._s.add(c);}, remove(c){this._s.delete(c);}, toggle(c){this._s.has(c)?this._s.delete(c):this._s.add(c);}, contains(c){return this._s.has(c);} },
    style:{}, textContent:'', innerHTML:'', value:'3', checked:true, dataset:{},
    children:[], firstChild:null,
    appendChild(c){ this.children.push(c); this.firstChild=this.children[0]; return c; },
    removeChild(c){ this.children=this.children.filter(x=>x!==c); },
    addEventListener(){}, setPointerCapture(){}, remove(){},
    querySelectorAll(){ return []; }, querySelector(){ return null; },
    getBoundingClientRect(){ return {left:0,top:0,width:800,height:600}; },
    getContext(){ return makeCtx(); },
    width:0, height:0,
  };
  return el;
}
const els = {};
global.window = global;
global.innerWidth = 800; global.innerHeight = 600;
global.devicePixelRatio = 1;
global.requestAnimationFrame = ()=>{};
global.addEventListener = ()=>{};
const store = new Map();
global.localStorage = { getItem:k=>store.has(k)?store.get(k):null, setItem:(k,v)=>store.set(k,String(v)), removeItem:k=>store.delete(k) };
global.document = {
  getElementById:(id)=> els[id] || (els[id]=makeEl()),
  createElement:()=>makeEl(),
  querySelectorAll:()=>[],
  addEventListener(){},
  hidden:false,
};

const files = ['config.js','sprites.js','map.js','entities.js','buildings.js','audio.js','save.js','ui.js','game.js'];
for(const f of files){
  const code = fs.readFileSync(path.join(__dirname,'..','www','js',f),'utf8');
  eval(code);
}

const canvas = makeEl();
const game = new ZT.Game(canvas);
ZT.UI.bind(game);
console.log('Game created OK');

game.newGame();
console.log('New game OK. Buildings:', game.buildings.length, 'Center:', JSON.stringify(game.centerTile));

// simulate 240 game-seconds (1 full day)
for(let i=0;i<2400;i++) game.update(0.1);
console.log('After 240s — citizens:', game.citizens.length, 'buildings:', game.buildings.length,
  'beds:', game.totalBeds(), 'food:', Math.floor(game.resources.food), 'wood:', Math.floor(game.resources.wood));

// spawn zombies manually
game.spawnZombies(10);
console.log('Spawned zombies:', game.zombies.length);
for(let i=0;i<600;i++) game.update(0.1);
console.log('After zombie fight — zombies:', game.zombies.length, 'citizens:', game.citizens.length,
  'killed:', game.stats.zombiesKilled, 'died:', game.stats.citizensDied, 'lost:', game.stats.buildingsLost);

// outbreak
game.startOutbreak();
for(let i=0;i<600;i++) game.update(0.1);
console.log('Outbreak — phase:', game.outbreakPhaseText(), 'zombies:', game.zombies.length, 'citizens:', game.citizens.length);
game.stopOutbreak();

// build defenses + placement
game.startPlacement('wall');
game.placement.x = game.centerTile.x+4; game.placement.y = game.centerTile.y;
game.updatePlacementValidity();
console.log('Placement valid:', game.placement.valid);
game.confirmPlacement();
console.log('Wall placed. Buildings:', game.buildings.length);

// save / load
const okSave = game.saveTo(0);
console.log('Save:', okSave);
const okLoad = game.loadFrom(0);
console.log('Load:', okLoad, 'citizens:', game.citizens.length, 'buildings:', game.buildings.length);

// long run: city growth to skyscrapers?
for(let i=0;i<3000;i++) game.update(0.1);
const types = {};
game.buildings.forEach(b=>{ types[b.id]=(types[b.id]||0)+1; });
console.log('Long run — citizens:', game.citizens.length, 'buildings:', JSON.stringify(types),
  'day:', game.day, 'happiness:', Math.round(game.avgHappiness()));

console.log('Final stats — died:', game.stats.citizensDied, 'born:', game.stats.citizensBorn, 'zombies left:', game.zombies.length, 'killed:', game.stats.zombiesKilled);
console.log('SMOKE TEST PASSED');
process.exit(0);
