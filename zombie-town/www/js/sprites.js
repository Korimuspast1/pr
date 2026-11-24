/* Procedural pixel-art sprites — no copyrighted assets, all drawn in code. */
'use strict';
var ZT = window.ZT;

ZT.Sprites = (function(){
  const cache = {};
  function make(w,h,fn){
    const c = document.createElement('canvas'); c.width=w; c.height=h;
    const g = c.getContext('2d'); g.imageSmoothingEnabled=false;
    fn(g,w,h);
    return c;
  }
  function px(g,x,y,w,h,col){ g.fillStyle=col; g.fillRect(x,y,w,h); }
  function fromMap(rows, palette, scale){
    const h=rows.length, w=rows[0].length;
    return make(w*scale,h*scale,(g)=>{
      for(let y=0;y<h;y++)for(let x=0;x<w;x++){
        const ch=rows[y][x]; const col=palette[ch];
        if(col){ g.fillStyle=col; g.fillRect(x*scale,y*scale,scale,scale); }
      }
    });
  }
  const S = 2; // pixel scale

  /* ---- terrain tiles (16x16 px each) ---- */
  function tileGrass(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#5da24a');
    for(let i=0;i<26;i++){ const x=(i*7)%32,y=(i*11)%32; px(g,x,y,2,2, i%3?'#4c8a3c':'#6fb45c'); }
    px(g,5,20,2,3,'#4c8a3c'); px(g,22,8,2,3,'#6fb45c');
  });}
  function tileDirt(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#9a7b4f');
    for(let i=0;i<16;i++){ px(g,(i*9)%30,(i*13)%30,3,2, i%2?'#86683f':'#ab8c5e'); }
  });}
  function tileRoad(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#8d8577');
    for(let i=0;i<14;i++){ px(g,(i*11)%30,(i*7)%30,3,2,'#7a7264'); }
    px(g,0,15,32,2,'#d8d2c0'); px(g,14,0,2,6,'#d8d2c0'); px(g,14,26,2,6,'#d8d2c0');
  });}
  function tileWater(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#3f7fbf');
    px(g,4,8,10,2,'#5aa0d8'); px(g,18,20,12,2,'#5aa0d8'); px(g,8,26,8,2,'#3570ab');
  });}
  function tileTree(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#5da24a');
    for(let i=0;i<10;i++){ px(g,(i*13)%30,(i*17)%30,2,2,'#4c8a3c'); }
    px(g,13,20,6,10,'#6b4a2a'); px(g,6,4,20,18,'#2f6b2f'); px(g,9,2,14,4,'#3f8a3f');
    px(g,11,8,4,4,'#57a857'); px(g,18,12,4,4,'#57a857'); px(g,14,16,3,3,'#e74c3c');
  });}
  function tileRock(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#5da24a');
    px(g,6,14,20,12,'#9e9e9e'); px(g,9,10,14,6,'#bdbdbd'); px(g,12,18,6,3,'#757575');
  });}
  function tileFlower(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#5da24a');
    const cols=['#ff8a80','#fff176','#f48fb1','#ce93d8'];
    for(let i=0;i<5;i++){ const x=4+(i*6)%24,y=4+(i*9)%24; px(g,x,y,3,3,cols[i%4]); px(g,x+1,y+1,1,1,'#fff'); }
  });}
  function tileSand(){ return make(16*S,16*S,(g)=>{
    px(g,0,0,32,32,'#e0cf9a');
    for(let i=0;i<10;i++){ px(g,(i*11)%30,(i*7)%30,2,2,'#d0bf82'); }
  });}

  /* ---- buildings: drawn per type at tile size (w,h in tiles) ---- */
  function drawBuilding(g, id, W, H){
    const w=W*32, h=H*32;
    const base=(col)=>{ px(g,2,h-8,w-4,6,col); };
    switch(id){
      case 'center': {
        px(g,4,10,w-8,h-14,'#d7ccc8'); px(g,4,10,w-8,6,'#a1887f');
        px(g,10,2,w-20,10,'#8d6e63'); px(g,10,2,w-20,3,'#ffcc80');
        px(g,w/2-4,18,8,12,'#5d4037'); px(g,8,20,8,8,'#ffe082'); px(g,w-16,20,8,8,'#ffe082');
        px(g,w/2-2,4,4,6,'#ffcc80'); break; }
      case 'house': {
        px(g,4,14,w-8,h-16,'#f5e6c8'); px(g,2,6,w-4,10,'#c62828');
        px(g,6,4,w-12,4,'#e53935'); px(g,w/2-4,20,8,10,'#6d4c41');
        px(g,7,17,6,6,'#ffe082'); px(g,w-13,17,6,6,'#ffe082');
        px(g,w-10,8,4,8,'#9e9e9e'); break; }
      case 'apartment': {
        px(g,3,4,w-6,h-6,'#eceff1'); px(g,3,4,w-6,4,'#90a4ae');
        for(let r=0;r<3;r++)for(let c=0;c<2;c++){ px(g,7+c*11,9+r*8,7,5, (r+c)%2?'#ffe082':'#b0bec5'); }
        px(g,w/2-4,h-12,8,10,'#546e7a'); break; }
      case 'skyscraper': {
        px(g,5,0,w-10,h-2,'#cfd8dc'); px(g,5,0,w-10,4,'#78909c');
        for(let r=0;r<6;r++)for(let c=0;c<2;c++){ px(g,8+c*10,6+r*5,6,3, (r*2+c)%3?'#ffe082':'#90a4ae'); }
        px(g,w/2-3,h-10,6,8,'#37474f'); px(g,w/2-1,2,2,4,'#ff5252'); break; }
      case 'farm': {
        px(g,2,6,w-4,h-8,'#c0a062');
        for(let r=0;r<4;r++)for(let c=0;c<6;c++){ px(g,4+c*5,8+r*6,3,4, (r+c)%2?'#7cb342':'#9ccc65'); }
        px(g,2,2,w-4,4,'#8d6e63'); break; }
      case 'kitchen': {
        px(g,3,12,w-6,h-14,'#ffe0b2'); px(g,1,5,w-2,8,'#ef6c00');
        px(g,w/2-5,20,10,10,'#4e342e'); px(g,6,16,7,7,'#ffcc80'); break; }
      case 'well': {
        px(g,6,14,w-12,h-16,'#9e9e9e'); px(g,4,8,w-8,6,'#757575');
        px(g,8,4,4,10,'#6d4c41'); px(g,w-12,4,4,10,'#6d4c41'); px(g,6,2,w-12,4,'#c62828');
        px(g,10,18,w-20,6,'#29b6f6'); break; }
      case 'storage': {
        px(g,2,10,w-4,h-12,'#a1887f'); px(g,2,6,w-4,6,'#8d6e63');
        px(g,6,16,10,8,'#d7ccc8'); px(g,w-16,16,10,8,'#d7ccc8'); break; }
      case 'workshop': {
        px(g,2,12,w-4,h-14,'#b0bec5'); px(g,0,5,w,8,'#546e7a');
        px(g,6,18,10,8,'#78909c'); px(g,w-18,18,12,8,'#ffcc80'); break; }
      case 'clinic': {
        px(g,3,12,w-6,h-14,'#ffffff'); px(g,1,5,w-2,8,'#e53935');
        px(g,w/2-3,16,6,10,'#e53935'); px(g,w/2-5,18,10,6,'#e53935'); break; }
      case 'cemetery': {
        px(g,2,h-8,w-4,6,'#7a6a4f');
        px(g,8,10,8,14,'#bdbdbd'); px(g,6,12,12,3,'#bdbdbd');
        px(g,w-18,14,8,10,'#9e9e9e'); px(g,w-16,16,4,2,'#757575'); break; }
      case 'safehouse': {
        px(g,3,10,w-6,h-12,'#8d6e63'); px(g,1,4,w-2,8,'#5d4037');
        px(g,w/2-6,18,12,12,'#3e2723'); px(g,w/2-8,8,16,4,'#ffcc80'); break; }
      case 'wall': {
        px(g,1,8,w-2,h-10,'#a1887f'); px(g,1,8,w-2,4,'#8d6e63');
        px(g,4,14,8,3,'#8d6e63'); px(g,w-12,20,8,3,'#8d6e63'); break; }
      case 'stonewall': {
        px(g,1,6,w-2,h-8,'#9e9e9e'); px(g,1,6,w-2,4,'#757575');
        px(g,5,14,10,3,'#757575'); px(g,w-15,20,10,3,'#757575'); break; }
      case 'gate': {
        px(g,1,4,6,h-6,'#6d4c41'); px(g,w-7,4,6,h-6,'#6d4c41');
        px(g,7,10,w-14,4,'#8d6e63'); px(g,7,20,w-14,4,'#8d6e63'); break; }
      case 'tower': {
        px(g,w/2-8,10,16,h-12,'#8d6e63'); px(g,w/2-12,2,24,10,'#6d4c41');
        px(g,w/2-9,4,6,6,'#ffe082'); px(g,w/2-3,14,6,6,'#ffe082'); break; }
      case 'turret': {
        px(g,6,16,w-12,h-18,'#546e7a'); px(g,4,8,w-8,10,'#37474f');
        px(g,w/2-2,2,4,10,'#263238'); px(g,w/2-2,2,4,3,'#ff5252'); break; }
      case 'spikes': {
        px(g,2,h-8,w-4,6,'#6d4c41');
        for(let i=0;i<4;i++){ px(g,4+i*7,10,3,12,'#bdbdbd'); px(g,4+i*7,8,3,3,'#eceff1'); } break; }
      case 'lamp': {
        px(g,w/2-2,12,4,h-14,'#37474f'); px(g,w/2-5,4,10,9,'#fff9c4'); px(g,w/2-3,6,6,5,'#fffde7'); break; }
      case 'bench': {
        px(g,4,14,w-8,4,'#8d6e63'); px(g,4,22,w-8,4,'#6d4c41');
        px(g,6,18,3,10,'#5d4037'); px(g,w-9,18,3,10,'#5d4037'); break; }
      case 'flowers': {
        px(g,3,h-8,w-6,5,'#6d4c41');
        const cols=['#ff8a80','#fff176','#f48fb1','#ce93d8','#90caf9'];
        for(let i=0;i<7;i++){ px(g,5+(i*4)%(w-10),8+(i*3)%10,4,4,cols[i%5]); } break; }
      case 'road': { px(g,0,0,w,h,'#8d8577'); px(g,0,15,w,2,'#d8d2c0'); break; }
      default: { px(g,4,8,w-8,h-10,'#bdbdbd'); }
    }
    base('#00000033');
  }

  function buildingSprite(id){
    const key='b_'+id;
    if(cache[key]) return cache[key];
    const def = ZT.BUILDINGS[id];
    const c = make(def.w*32, def.h*32, (g)=>drawBuilding(g,id,def.w,def.h));
    cache[key]=c; return c;
  }
  function tileSprite(t){
    const key='t_'+t;
    if(cache[key]) return cache[key];
    const map={0:tileGrass,1:tileDirt,2:tileRoad,3:tileWater,4:tileTree,5:tileRock,6:tileFlower,7:tileSand};
    const c=(map[t]||tileGrass)(); cache[key]=c; return c;
  }
  function constructionSprite(w,h){
    const key='c_'+w+'_'+h;
    if(cache[key]) return cache[key];
    const c=make(w*32,h*32,(g)=>{
      px(g,2,h*32-8,w*32-4,6,'#8d6e63');
      for(let x=4;x<w*32-4;x+=10){ px(g,x,8,3,h*32-16,'#a1887f'); }
      for(let y=10;y<h*32-8;y+=10){ px(g,2,y,w*32-4,3,'#a1887f'); }
      px(g,2,2,w*32-4,4,'#ffcc80');
    });
    cache[key]=c; return c;
  }
  function ruinSprite(w,h){
    const key='r_'+w+'_'+h;
    if(cache[key]) return cache[key];
    const c=make(w*32,h*32,(g)=>{
      px(g,2,h*32-10,w*32-4,8,'#5d4037');
      px(g,6,h*32-18,10,8,'#757575'); px(g,w*32-16,h*32-16,12,6,'#9e9e9e');
      px(g,10,10,6,6,'#424242'); px(g,w*32-14,8,5,5,'#616161');
    });
    cache[key]=c; return c;
  }
  function personSprite(shirt, skin, opts){
    opts=opts||{};
    const key='p_'+shirt+'_'+skin+'_'+(opts.zombie?'z':'')+'_'+(opts.child?'c':'')+'_'+(opts.infected?'i':'');
    if(cache[key]) return cache[key];
    const c=make(16,20,(g)=>{
      const s=skin, sh=shirt;
      px(g,5,0,6,6, opts.zombie?'#8bc34a':s);            // head
      px(g,5,0,6,2, opts.zombie?'#33691e':'#5d4037');    // hair
      if(opts.zombie){ px(g,6,3,1,1,'#000'); px(g,9,3,1,1,'#000'); px(g,7,5,2,1,'#000'); }
      else { px(g,6,3,1,1,'#263238'); px(g,9,3,1,1,'#263238'); }
      px(g,4,6,8,7, opts.infected?'#9ccc65':sh);          // body
      px(g,4,13,3,6,'#37474f'); px(g,9,13,3,6,'#37474f'); // legs
      px(g,3,7,1,5,opts.infected?'#9ccc65':sh); px(g,12,7,1,5,opts.infected?'#9ccc65':sh); // arms
      if(opts.child){ /* smaller handled by draw scale */ }
    });
    cache[key]=c; return c;
  }
  function zombieSprite(type){
    const key='z_'+type;
    if(cache[key]) return cache[key];
    const def=ZT.ZOMBIES[type];
    const c=make(18,22,(g)=>{
      const col=def.color;
      px(g,6,0,7,7,col); px(g,6,0,7,2,'#33691e');
      px(g,7,3,1,1,'#000'); px(g,11,3,1,1,'#000');
      px(g,8,5,3,1,'#000'); px(g,8,6,1,1,'#b71c1c'); // blood
      px(g,5,7,9,8,col); px(g,5,7,9,2,'#33691e');
      px(g,5,15,3,6,'#37474f'); px(g,11,15,3,6,'#37474f');
      px(g,3,8,2,6,col); px(g,14,8,2,6,col); // arms forward-ish
      if(type==='brute'){ px(g,3,5,13,4,col); }
      if(type==='screamer'){ px(g,8,7,3,3,'#000'); }
    });
    cache[key]=c; return c;
  }
  function glowSprite(){
    if(cache.glow) return cache.glow;
    const c=make(64,64,(g)=>{
      const gr=g.createRadialGradient(32,32,2,32,32,32);
      gr.addColorStop(0,'rgba(255,240,180,0.9)'); gr.addColorStop(1,'rgba(255,240,180,0)');
      g.fillStyle=gr; g.fillRect(0,0,64,64);
    });
    cache.glow=c; return c;
  }
  return { tileSprite, buildingSprite, constructionSprite, ruinSprite, personSprite, zombieSprite, glowSprite };
})();
