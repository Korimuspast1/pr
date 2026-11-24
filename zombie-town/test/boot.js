/* Boot test: simulate real page load + New Game click */
'use strict';
const fs = require('fs');
const path = require('path');
function makeCtx(){ return { fillStyle:'',strokeStyle:'',globalAlpha:1,globalCompositeOperation:'source-over',font:'',textAlign:'',lineWidth:1,imageSmoothingEnabled:false,setTransform(){},fillRect(){},drawImage(){},save(){},restore(){},translate(){},scale(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},strokeRect(){},fillText(){},createRadialGradient(){return{addColorStop(){}};} }; }
function makeEl(id){ return { id, classList:{_s:new Set(),add(c){this._s.add(c);},remove(c){this._s.delete(c);},toggle(c){this._s.has(c)?this._s.delete(c):this._s.add(c);},contains(c){return this._s.has(c);}}, style:{},textContent:'',innerHTML:'',value:'3',checked:true,dataset:{},children:[],firstChild:null,onclick:null,appendChild(c){this.children.push(c);this.firstChild=this.children[0];return c;},removeChild(c){this.children=this.children.filter(x=>x!==c);},addEventListener(){},setPointerCapture(){},remove(){},querySelectorAll(){return[];},querySelector(){return null;},getBoundingClientRect(){return{left:0,top:0,width:800,height:600};},getContext(){return makeCtx();},width:0,height:0 }; }
const els={};
const listeners={};
global.window=global; global.innerWidth=800; global.innerHeight=600; global.devicePixelRatio=1;
global.requestAnimationFrame=()=>{}; global.addEventListener=(ev,fn)=>{ (listeners[ev]=listeners[ev]||[]).push(fn); };
const store=new Map();
global.localStorage={getItem:k=>store.has(k)?store.get(k):null,setItem:(k,v)=>store.set(k,String(v)),removeItem:k=>store.delete(k)};
global.document={
  readyState:'complete',
  getElementById:id=>els[id]||(els[id]=makeEl(id)),
  createElement:()=>makeEl('x'),
  querySelectorAll:()=>[],
  addEventListener:(ev,fn)=>{ (listeners['doc:'+ev]=listeners['doc:'+ev]||[]).push(fn); },
  hidden:false,
};
for(const f of ['config.js','sprites.js','map.js','entities.js','buildings.js','audio.js','save.js','ui.js','game.js']){
  eval(fs.readFileSync(path.join(__dirname,'..','www','js',f),'utf8'));
}
// boot should have run immediately (readyState complete)
if(!ZT.game){ console.error('FAIL: boot did not run'); process.exit(1); }
console.log('Boot OK, game created:', !!ZT.game);
if(typeof els['btn-new'].onclick!=='function'){ console.error('FAIL: btn-new has no handler'); process.exit(1); }
console.log('btn-new handler bound OK');
// simulate click
els['btn-new'].onclick();
if(!ZT.game.inGame){ console.error('FAIL: game did not start after New Game click'); process.exit(1); }
console.log('New Game click OK — inGame:', ZT.game.inGame, 'buildings:', ZT.game.buildings.length);
if(els['main-menu'].classList.contains('hidden')===false){ console.error('FAIL: main menu still visible'); process.exit(1); }
console.log('Main menu hidden OK');
for(let i=0;i<300;i++) ZT.game.update(0.1);
console.log('Simulated 30s — citizens:', ZT.game.citizens.length);
console.log('BOOT TEST PASSED');
process.exit(0);
