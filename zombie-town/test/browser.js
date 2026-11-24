/* Browser-like test: all scripts concatenated in ONE scope (like real <script> tags). */
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

function makeCtx(){ return { fillStyle:'',strokeStyle:'',globalAlpha:1,globalCompositeOperation:'source-over',font:'',textAlign:'',lineWidth:1,imageSmoothingEnabled:false,setTransform(){},fillRect(){},drawImage(){},save(){},restore(){},translate(){},scale(){},beginPath(){},moveTo(){},lineTo(){},stroke(){},strokeRect(){},fillText(){},createRadialGradient(){return{addColorStop(){}};} }; }
function makeEl(id){ return { id, classList:{_s:new Set(),add(c){this._s.add(c);},remove(c){this._s.delete(c);},toggle(c){this._s.has(c)?this._s.delete(c):this._s.add(c);},contains(c){return this._s.has(c);}}, style:{},textContent:'',innerHTML:'',value:'3',checked:true,dataset:{},children:[],firstChild:null,onclick:null,appendChild(c){this.children.push(c);this.firstChild=this.children[0];return c;},removeChild(c){this.children=this.children.filter(x=>x!==c);},addEventListener(){},setPointerCapture(){},remove(){},querySelectorAll(){return[];},querySelector(){return null;},getBoundingClientRect(){return{left:0,top:0,width:800,height:600};},getContext(){return makeCtx();},width:0,height:0 }; }
const els={};
const store=new Map();
const sandbox = {
  console, setTimeout, setInterval, clearInterval, Math, JSON, Date, Array, Object, Number, String, Boolean, Map, Set, Promise, Error, TypeError, SyntaxError,
  innerWidth:800, innerHeight:600, devicePixelRatio:1,
  requestAnimationFrame:()=>{},
  addEventListener:()=>{},
  localStorage:{getItem:k=>store.has(k)?store.get(k):null,setItem:(k,v)=>store.set(k,String(v)),removeItem:k=>store.delete(k)},
  document:{
    readyState:'complete',
    getElementById:id=>els[id]||(els[id]=makeEl(id)),
    createElement:()=>makeEl('x'),
    querySelectorAll:()=>[],
    addEventListener(){},
    hidden:false,
  },
  navigator:{},
};
sandbox.window = sandbox;
sandbox.globalThis = sandbox;
vm.createContext(sandbox);

const files = ['config.js','sprites.js','map.js','entities.js','buildings.js','audio.js','save.js','ui.js','game.js'];
let combined = '';
for(const f of files){
  combined += '\n;//===== '+f+' =====\n' + fs.readFileSync(path.join(__dirname,'..','www','js',f),'utf8');
}
try{
  vm.runInContext(combined, sandbox, {filename:'bundle.js'});
  console.log('All scripts loaded in one scope OK (no redeclaration errors)');
}catch(e){
  console.error('FAIL loading scripts:', e.message);
  process.exit(1);
}
const ZT = sandbox.ZT;
if(!ZT || !ZT.Game || !ZT.UI){ console.error('FAIL: ZT.Game/ZT.UI missing'); process.exit(1); }
if(!ZT.game){ console.error('FAIL: boot did not create game'); process.exit(1); }
console.log('Boot OK — game created');
if(typeof els['btn-new'].onclick!=='function'){ console.error('FAIL: btn-new handler missing'); process.exit(1); }
els['btn-new'].onclick();
if(!ZT.game.inGame){ console.error('FAIL: New Game did not start'); process.exit(1); }
console.log('New Game OK — buildings:', ZT.game.buildings.length);
for(let i=0;i<1200;i++) ZT.game.update(0.1);
console.log('Simulated 120s — citizens:', ZT.game.citizens.length, 'buildings:', ZT.game.buildings.length, 'beds:', ZT.game.totalBeds());
ZT.game.spawnZombies(5);
for(let i=0;i<300;i++) ZT.game.update(0.1);
console.log('After zombies — zombies:', ZT.game.zombies.length, 'killed:', ZT.game.stats.zombiesKilled, 'citizens:', ZT.game.citizens.length);
console.log('BROWSER TEST PASSED');
process.exit(0);
