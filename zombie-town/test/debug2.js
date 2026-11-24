/* Debug zombie movement */
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
for(let i=0;i<300;i++) game.update(0.1);
game.spawnZombies(3);
const z=game.zombies[0];
console.log('zombie start:', Math.round(z.x), Math.round(z.y), 'tile:', JSON.stringify(ZT.Map.worldToTile(z.x,z.y)), 'tileType:', ZT.Map.getTile(ZT.Map.worldToTile(z.x,z.y).x, ZT.Map.worldToTile(z.x,z.y).y));
for(let s=0;s<15;s++){
  for(let i=0;i<10;i++) game.update(0.1);
  const t=z.target;
  console.log('t='+(s+1)+' z:'+Math.round(z.x)+','+Math.round(z.y)+' state:'+z.state+' moving:'+z.moving+
    ' path:'+(z.path?z.path.length+'@'+z.pathIdx:'null')+
    (t?(' tgt:'+t.name+'@'+Math.round(t.x)+','+Math.round(t.y)+' d:'+Math.round(Math.hypot(t.x-z.x,t.y-z.y))+' cstate:'+t.state+' cpath:'+(t.path?t.path.length+'@'+t.pathIdx:'null')):' tgt:-')+
    ' atkCd:'+z.attackCd.toFixed(1));
}
process.exit(0);
