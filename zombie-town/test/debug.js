/* Focused debug: 1 guard vs zombies */
'use strict';
const fs = require('fs');
const path = require('path');
function makeCtx(){ return { fillStyle:'',strokeStyle:'',globalAlpha:1,globalCompositeOperation:'source-over',font:'',textAlign:'',lineWidth:1,imageSmoothingEnabled:false,setTransform(){},fillRect(){},drawImage(){},save(){},restore(){},translate(){},scale(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},strokeRect(){},fillText(){},createRadialGradient(){return{addColorStop(){}};} }; }
function makeEl(){ return { classList:{_s:new Set(),add(c){this._s.add(c);},remove(c){this._s.delete(c);},toggle(c){this._s.has(c)?this._s.delete(c):this._s.add(c);},contains(c){return this._s.has(c);}}, style:{},textContent:'',innerHTML:'',value:'3',checked:true,dataset:{},children:[],firstChild:null,appendChild(c){this.children.push(c);this.firstChild=this.children[0];return c;},removeChild(c){this.children=this.children.filter(x=>x!==c);},addEventListener(){},setPointerCapture(){},remove(){},querySelectorAll(){return[];},querySelector(){return null;},getBoundingClientRect(){return{left:0,top:0,width:800,height:600};},getContext(){return makeCtx();},width:0,height:0 }; }
const els={};
global.window=global; global.innerWidth=800; global.innerHeight=600; global.devicePixelRatio=1;
global.requestAnimationFrame=()=>{}; global.addEventListener=()=>{};
const store=new Map();
global.localStorage={getItem:k=>store.has(k)?store.get(k):null,setItem:(k,v)=>store.set(k,String(v)),removeItem:k=>store.delete(k)};
global.document={getElementById:id=>els[id]||(els[id]=makeEl()),createElement:()=>makeEl(),querySelectorAll:()=>[],addEventListener(){},hidden:false};
for(const f of ['config.js','sprites.js','map.js','entities.js','buildings.js','audio.js','save.js','ui.js','game.js']){
  eval(fs.readFileSync(path.join(__dirname,'..','www','js',f),'utf8'));
}
const game=new ZT.Game(makeEl());
ZT.UI.bind(game);
game.newGame();
for(let i=0;i<1200;i++) game.update(0.1);
console.log('citizens:', game.citizens.length);
game.assignJobs();
const guards=game.citizens.filter(c=>c.job==='guard');
console.log('guards:', guards.length, guards.map(g=>g.name+':'+g.state).join(', '));
game.spawnZombies(5);
for(let s=0;s<30;s++){
  for(let i=0;i<10;i++) game.update(0.1);
  const g=game.citizens.find(c=>c.job==='guard');
  const z=game.zombies[0];
  console.log('t='+(s+1)+'s zombies:'+game.zombies.length+' killed:'+game.stats.zombiesKilled+
    (g?(' guard['+g.state+' hp'+Math.round(g.hp)+' pos'+Math.round(g.x)+','+Math.round(g.y)+' tgt:'+(g.targetZombie?Math.round(g.targetZombie.hp):'-')+']'):'')+
    (z?(' z0[hp'+Math.round(z.hp)+' pos'+Math.round(z.x)+','+Math.round(z.y)+' tgt:'+(z.target?z.target.name:(z.targetBuilding?z.targetBuilding.id:'-'))+']'):''));
}
process.exit(0);
