/* Procedural pixel-art sprites — detailed & recognizable, no copyrighted assets. */
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
  const S = 2; // pixel scale (16px art -> 32px tiles)

  /* ================= TERRAIN TILES (32x32, biome palettes) ================= */
  function pal(){ return ZT.BIOMES[ZT.biome] || ZT.BIOMES.summer; }
  function tileGrass(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32,P.grass);
    for(let i=0;i<30;i++){ const x=(i*7+3)%31,y=(i*11+2)%31; px(g,x,y,2,2, i%3?P.grassD:P.grassL); }
    px(g,6,22,2,4,P.grassD); px(g,24,10,2,4,P.grassL); px(g,16,28,2,3,P.grassD);
    px(g,2,14,3,2,P.grassL);
    if(P.snow){ px(g,8,6,6,2,'#fff'); px(g,20,20,8,2,'#fff'); px(g,4,26,5,2,'#f4f8fc'); }
  });}
  function tileDirt(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32,P.dirt);
    for(let i=0;i<18;i++){ px(g,(i*9+2)%29,(i*13+3)%29,3,2, i%2?P.dirtD:P.dirt); }
    px(g,8,8,2,2,P.dirtD); px(g,22,20,2,2,P.dirtD);
    if(P.snow){ px(g,4,4,8,2,'#fff'); px(g,18,24,9,2,'#fff'); }
  });}
  function tileRoad(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32, P.snow?'#a8b4c2':'#8f8a7d');
    for(let i=0;i<16;i++){ px(g,(i*11+1)%29,(i*7+2)%29,3,2, P.snow?'#98a4b2':'#7d786c'); }
    px(g,0,0,32,2, P.snow?'#8a96a4':'#6e6a5f'); px(g,0,30,32,2, P.snow?'#8a96a4':'#6e6a5f');
    px(g,14,4,4,8,'#e8e2cc'); px(g,14,20,4,8,'#e8e2cc');
    if(P.snow){ px(g,4,12,6,2,'#fff'); px(g,22,8,6,2,'#fff'); }
  });}
  function tileWater(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32,P.water);
    px(g,3,7,11,2,P.waterL); px(g,17,15,13,2,P.waterL); px(g,6,24,9,2,P.waterD);
    px(g,20,27,8,2,P.waterL); px(g,10,11,6,2,P.waterD);
    if(P.snow){ px(g,8,4,10,2,'#dcebf5'); px(g,18,20,9,2,'#dcebf5'); }
  });}
  function tileTree(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32,P.grass);
    for(let i=0;i<10;i++){ px(g,(i*13+2)%29,(i*17+3)%29,2,2,P.grassD); }
    if(P.pine){
      // snowy pine (like the reference)
      px(g,14,22,5,10,P.trunk);
      px(g,10,16,13,7,P.tree); px(g,7,10,19,8,P.tree); px(g,10,4,13,7,P.tree);
      px(g,12,16,9,2,'#fff'); px(g,9,10,15,2,'#fff'); px(g,12,4,9,2,'#fff');
      px(g,14,2,5,3,P.treeL);
    } else {
      px(g,14,20,5,12,P.trunk); px(g,15,22,3,8,P.trunk);
      px(g,7,5,19,16,P.tree); px(g,10,2,13,5,P.tree);
      px(g,9,9,5,5,P.treeL); px(g,19,12,5,5,P.treeL); px(g,13,16,4,4,P.treeL);
      if(!P.snow && ZT.biome==='summer'){ px(g,12,10,3,3,'#e74c3c'); px(g,20,16,3,3,'#e74c3c'); px(g,16,6,3,3,'#e74c3c'); }
      if(ZT.biome==='autumn'){ px(g,11,8,4,4,'#e8a33d'); px(g,20,14,4,4,'#c96a1e'); }
    }
  });}
  function tileRock(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32,P.grass);
    for(let i=0;i<8;i++){ px(g,(i*11+2)%29,(i*7+3)%29,2,2,P.grassD); }
    px(g,5,16,22,13,'#8d8d8d'); px(g,8,11,16,7,'#a8a8a8');
    px(g,11,14,6,3,'#c4c4c4'); px(g,18,20,6,3,'#6e6e6e'); px(g,7,24,8,3,'#6e6e6e');
    if(P.snow){ px(g,10,11,10,3,'#fff'); px(g,14,16,6,2,'#fff'); }
  });}
  function tileFlower(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32,P.grass);
    for(let i=0;i<12;i++){ px(g,(i*9+2)%29,(i*13+3)%29,2,2,P.grassD); }
    const cols=['#ff8a80','#fff176','#f48fb1','#ce93d8','#90caf9'];
    for(let i=0;i<6;i++){ const x=4+(i*5)%24,y=4+(i*8)%24; px(g,x,y,4,4,cols[i%5]); px(g,x+1,y+1,2,2,'#fff'); px(g,x+2,y+5,1,3,'#2e6b2e'); }
  });}
  function tileSand(){ const P=pal(); return make(32,32,(g)=>{
    px(g,0,0,32,32, P.snow?'#eef3f8':'#e3d29b');
    for(let i=0;i<12;i++){ px(g,(i*11+2)%29,(i*7+3)%29,2,2, P.snow?'#dbe6f0':'#d2c184'); }
    px(g,10,10,3,2, P.snow?'#c8d8e8':'#c4b078'); px(g,22,22,3,2, P.snow?'#c8d8e8':'#c4b078');
  });}

  /* ================= BUILDINGS ================= */
  function window_(g,x,y,w,h,lit){
    px(g,x,y,w,h, lit?'#ffe082':'#546e7a');
    px(g,x,y,w,1,'#37474f'); px(g,x,y+h-1,w,1,'#37474f');
    px(g,x,y,1,h,'#37474f'); px(g,x+w-1,y,1,h,'#37474f');
    px(g,x+w/2,y,1,h,'#37474f'); px(g,x,y+h/2,w,1,'#37474f');
  }
  function drawBuilding(g, id, W, H){
    const w=W*32, h=H*32;
    const shadow=()=>{ px(g,2,h-7,w-4,5,'#0000002e'); };
    switch(id){
      case 'center': { // town hall 3x3
        px(g,6,26,w-12,h-30,'#e8e0d0'); px(g,6,26,w-12,5,'#c9bda3');
        px(g,10,8,w-20,20,'#d8cdb4'); px(g,10,8,w-20,4,'#b71c1c');
        px(g,w/2-7,0,14,10,'#b71c1c'); px(g,w/2-1,0,2,10,'#7f0000');
        // clock
        px(g,w/2-8,12,16,16,'#fff'); px(g,w/2-8,12,16,2,'#37474f'); px(g,w/2-8,26,16,2,'#37474f');
        px(g,w/2-8,12,2,16,'#37474f'); px(g,w/2+6,12,2,16,'#37474f');
        px(g,w/2,18,1,6,'#37474f'); px(g,w/2,20,5,1,'#37474f');
        // flag
        px(g,w/2-1,-0,2,8,'#5d4037'); px(g,w/2+1,0,10,6,'#4caf50');
        // door + steps
        px(g,w/2-6,44,12,20,'#5d4037'); px(g,w/2-6,44,12,3,'#3e2723');
        px(g,w/2-10,64,20,4,'#9e9e9e'); px(g,w/2-13,68,26,4,'#bdbdbd');
        window_(g,16,34,12,12,true); window_(g,w-28,34,12,12,true);
        window_(g,16,52,12,12,true); window_(g,w-28,52,12,12,true);
        break; }
      case 'house': { // cozy cottage
        px(g,4,16,w-8,h-18,'#f7ead0'); px(g,4,16,w-8,3,'#e0cfa8');
        // pitched roof
        px(g,2,8,w-4,9,'#c62828'); px(g,5,5,w-10,4,'#e53935'); px(g,8,2,w-16,4,'#ef5350');
        px(g,2,15,w-4,2,'#8e0000');
        // chimney
        px(g,w-11,0,5,9,'#9e9e9e'); px(g,w-12,-0,7,3,'#757575');
        // door
        px(g,w/2-4,20,8,11,'#6d4c41'); px(g,w/2-4,20,8,2,'#4e342e'); px(g,w/2+2,25,2,2,'#ffcc80');
        // windows
        window_(g,6,19,7,7,true); window_(g,w-13,19,7,7,true);
        // foundation
        px(g,3,h-4,w-6,3,'#a1887f');
        break; }
      case 'apartment': { // 3-story block
        px(g,3,3,w-6,h-5,'#eceff1'); px(g,3,3,w-6,4,'#90a4ae'); px(g,3,h-6,w-6,3,'#b0bec5');
        for(let r=0;r<3;r++)for(let c=0;c<2;c++) window_(g,6+c*13,8+r*8,9,6,(r+c)%2===0);
        // balconies
        px(g,5,14,11,2,'#78909c'); px(g,16,22,11,2,'#78909c');
        // door
        px(g,w/2-4,h-12,8,10,'#546e7a'); px(g,w/2-4,h-12,8,2,'#37474f');
        break; }
      case 'skyscraper': { // glass tower
        px(g,4,2,w-8,h-4,'#37474f'); px(g,4,2,w-8,3,'#263238');
        px(g,6,6,w-12,h-12,'#546e7a');
        for(let r=0;r<6;r++)for(let c=0;c<2;c++){
          const lit=(r*2+c)%3!==0;
          px(g,8+c*11,8+r*4,8,3, lit?'#ffe082':'#78909c');
        }
        // antenna + beacon
        px(g,w/2-1,0,2,4,'#bdbdbd'); px(g,w/2-2,-0,4,2,'#ff5252');
        // entrance canopy
        px(g,w/2-9,h-10,18,3,'#263238'); px(g,w/2-4,h-7,8,5,'#ffe082');
        break; }
      case 'farm': { // barn + crop rows (2x2)
        px(g,2,2,w-4,h-4,'#c9a86a');
        for(let r=0;r<6;r++)for(let c=0;c<10;c++){ px(g,5+c*6,8+r*9,4,6, (r+c)%2?'#7cb342':'#9ccc65'); }
        // barn
        px(g,w-26,4,22,18,'#b71c1c'); px(g,w-28,0,26,6,'#7f0000');
        px(g,w-19,10,8,12,'#7f0000'); px(g,w-19,10,8,2,'#fff'); px(g,w-16,10,2,12,'#fff');
        break; }
      case 'kitchen': {
        px(g,3,14,w-6,h-16,'#ffe0b2'); px(g,1,6,w-2,9,'#ef6c00'); px(g,4,3,w-8,4,'#fb8c00');
        px(g,w-10,0,4,7,'#9e9e9e');
        px(g,w/2-5,20,10,11,'#4e342e'); px(g,w/2-5,20,10,2,'#3e2723');
        window_(g,6,17,7,7,true); window_(g,w-13,17,7,7,true);
        break; }
      case 'well': {
        px(g,5,18,w-10,h-20,'#9e9e9e'); px(g,5,18,w-10,3,'#757575');
        px(g,7,20,w-14,h-24,'#29b6f6'); px(g,9,22,w-18,3,'#63a5dd');
        px(g,7,6,4,14,'#6d4c41'); px(g,w-11,6,4,14,'#6d4c41');
        px(g,4,2,w-8,5,'#c62828'); px(g,4,2,w-8,2,'#e53935');
        px(g,w/2-3,12,6,5,'#8d6e63');
        break; }
      case 'storage': { // crates (2x1)
        px(g,2,10,w-4,h-12,'#a1887f'); px(g,2,6,w-4,6,'#8d6e63'); px(g,2,6,w-4,2,'#6d4c41');
        px(g,6,16,12,10,'#d7ccc8'); px(g,6,16,12,2,'#bcaaa4'); px(g,11,16,2,10,'#bcaaa4');
        px(g,w-18,16,12,10,'#d7ccc8'); px(g,w-18,16,12,2,'#bcaaa4'); px(g,w-13,16,2,10,'#bcaaa4');
        px(g,w/2-8,14,16,12,'#8d6e63');
        break; }
      case 'workshop': { // anvil + tools (2x1)
        px(g,2,12,w-4,h-14,'#b0bec5'); px(g,0,5,w,8,'#546e7a'); px(g,0,5,w,2,'#37474f');
        px(g,6,18,14,9,'#78909c'); px(g,9,20,8,2,'#546e7a');
        px(g,w-20,16,14,11,'#ffcc80'); px(g,w-18,18,4,4,'#37474f'); px(g,w-12,18,4,4,'#37474f');
        px(g,w/2-3,14,6,6,'#e53935');
        break; }
      case 'clinic': { // white + red cross (2x1)
        px(g,3,12,w-6,h-14,'#ffffff'); px(g,1,5,w-2,8,'#e53935'); px(g,1,5,w-2,2,'#b71c1c');
        px(g,w/2-3,16,6,12,'#e53935'); px(g,w/2-6,19,12,6,'#e53935');
        window_(g,8,17,10,8,true); window_(g,w-18,17,10,8,true);
        break; }
      case 'cemetery': { // gravestones + fence (2x2)
        px(g,2,h-8,w-4,6,'#7a6a4f');
        px(g,4,h-14,w-8,2,'#5d4037');
        for(let x=6;x<w-8;x+=8) px(g,x,h-16,2,4,'#5d4037');
        px(g,10,14,10,16,'#bdbdbd'); px(g,8,16,14,3,'#bdbdbd'); px(g,13,20,4,6,'#9e9e9e');
        px(g,w-24,18,10,12,'#9e9e9e'); px(g,w-22,20,6,2,'#757575');
        px(g,w/2-6,8,12,14,'#cfcfcf'); px(g,w/2-4,10,8,2,'#9e9e9e');
        break; }
      case 'safehouse': { // bunker (2x2)
        px(g,3,12,w-6,h-14,'#8d6e63'); px(g,1,5,w-2,8,'#5d4037'); px(g,1,5,w-2,2,'#3e2723');
        px(g,w/2-8,22,16,14,'#3e2723'); px(g,w/2-8,22,16,3,'#263238');
        px(g,w/2-10,10,20,5,'#ffcc80'); px(g,w/2-2,11,4,3,'#fff');
        window_(g,8,20,10,8,true); window_(g,w-18,20,10,8,true);
        break; }
      case 'wall': {
        px(g,1,9,w-2,h-11,'#a1887f');
        px(g,1,9,w-2,3,'#8d6e63'); px(g,1,9,4,4,'#8d6e63'); px(g,w-5,9,4,4,'#8d6e63');
        px(g,4,16,9,3,'#8d6e63'); px(g,w-13,22,9,3,'#8d6e63'); px(g,4,26,9,3,'#8d6e63');
        px(g,1,h-4,w-2,2,'#6d4c41');
        break; }
      case 'stonewall': {
        px(g,1,7,w-2,h-9,'#9e9e9e');
        px(g,1,7,w-2,3,'#757575'); px(g,1,7,5,5,'#757575'); px(g,w-6,7,5,5,'#757575');
        px(g,5,15,10,3,'#757575'); px(g,w-15,21,10,3,'#757575'); px(g,5,27,10,3,'#757575');
        px(g,1,h-4,w-2,2,'#616161');
        break; }
      case 'gate': {
        px(g,1,3,7,h-5,'#6d4c41'); px(g,w-8,3,7,h-5,'#6d4c41');
        px(g,1,3,7,3,'#4e342e'); px(g,w-8,3,7,3,'#4e342e');
        for(let x=10;x<w-10;x+=5){ px(g,x,10,3,h-16,'#8d6e63'); px(g,x,8,3,3,'#5d4037'); }
        px(g,8,14,w-16,3,'#6d4c41'); px(g,8,24,w-16,3,'#6d4c41');
        break; }
      case 'tower': {
        px(g,w/2-9,12,18,h-14,'#8d6e63'); px(g,w/2-9,12,18,3,'#6d4c41');
        px(g,w/2-13,2,26,11,'#6d4c41'); px(g,w/2-15,0,30,4,'#4e342e');
        window_(g,w/2-5,5,10,7,true);
        px(g,w/2-4,18,8,12,'#5d4037');
        px(g,w/2-11,20,4,10,'#6d4c41'); px(g,w/2+7,20,4,10,'#6d4c41');
        break; }
      case 'turret': {
        px(g,5,18,w-10,h-20,'#546e7a'); px(g,5,18,w-10,3,'#37474f');
        px(g,3,9,w-6,10,'#37474f'); px(g,3,9,w-6,3,'#263238');
        px(g,w/2-2,1,4,10,'#263238'); px(g,w/2-2,1,4,3,'#ff5252');
        px(g,8,22,5,5,'#78909c'); px(g,w-13,22,5,5,'#78909c');
        break; }
      case 'spikes': {
        px(g,2,h-8,w-4,6,'#6d4c41'); px(g,2,h-8,w-4,2,'#54371e');
        for(let i=0;i<4;i++){ px(g,4+i*7,12,3,12,'#bdbdbd'); px(g,4+i*7,9,3,4,'#eceff1'); px(g,5+i*7,13,1,8,'#757575'); }
        break; }
      case 'lamp': {
        px(g,w/2-2,14,4,h-16,'#37474f'); px(g,w/2-4,h-6,8,4,'#263238');
        px(g,w/2-6,4,12,11,'#fff9c4'); px(g,w/2-4,6,8,7,'#fffde7'); px(g,w/2-6,4,12,2,'#37474f');
        break; }
      case 'bench': {
        px(g,3,13,w-6,4,'#8d6e63'); px(g,3,20,w-6,4,'#6d4c41');
        px(g,5,17,3,11,'#5d4037'); px(g,w-8,17,3,11,'#5d4037');
        px(g,3,13,w-6,1,'#a1887f');
        break; }
      case 'flowers': {
        px(g,2,h-9,w-4,7,'#6d4c41'); px(g,2,h-9,w-4,2,'#54371e');
        const cols=['#ff8a80','#fff176','#f48fb1','#ce93d8','#90caf9'];
        for(let i=0;i<8;i++){ const x=5+(i*4)%(w-10), y=6+(i*3)%12; px(g,x,y,5,5,cols[i%5]); px(g,x+1,y+1,3,3,'#fff'); px(g,x+2,y+5,1,4,'#2e6b2e'); }
        break; }
      case 'road': { px(g,0,0,w,h,'#8f8a7d'); px(g,14,4,4,8,'#e8e2cc'); px(g,14,20,4,8,'#e8e2cc'); break; }
      default: { px(g,4,8,w-8,h-10,'#bdbdbd'); }
    }
    shadow();
  }

  function buildingSprite(id){
    const key='b_'+id;
    if(cache[key]) return cache[key];
    const def = ZT.BUILDINGS[id];
    const c = make(def.w*32, def.h*32, (g)=>drawBuilding(g,id,def.w,def.h));
    cache[key]=c; return c;
  }
  function tileSprite(t){
    const key='t_'+ZT.biome+'_'+t;
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
      px(g,14,20,4,4,'#ff7043');
    });
    cache[key]=c; return c;
  }
  function personSprite(shirt, skin, opts, frame){
    opts=opts||{}; frame=frame||0;
    const key='p_'+shirt+'_'+skin+'_'+(opts.zombie?'z':'')+'_'+(opts.child?'c':'')+'_'+(opts.infected?'i':'')+'_'+frame;
    if(cache[key]) return cache[key];
    const c=make(16,20,(g)=>{
      const s=skin, sh=shirt;
      px(g,5,0,6,6, opts.zombie?'#8bc34a':s);            // head
      px(g,5,0,6,2, opts.zombie?'#33691e':'#5d4037');    // hair
      if(opts.zombie){ px(g,6,3,1,1,'#000'); px(g,9,3,1,1,'#000'); px(g,7,5,2,1,'#000'); }
      else { px(g,6,3,1,1,'#263238'); px(g,9,3,1,1,'#263238'); }
      px(g,4,6,8,7, opts.infected?'#9ccc65':sh);          // body
      // animated legs: frame 0 = left forward, frame 1 = right forward
      if(frame===0){ px(g,3,13,3,6,'#37474f'); px(g,10,13,3,5,'#37474f'); }
      else { px(g,4,13,3,5,'#37474f'); px(g,9,13,3,6,'#37474f'); }
      // arms swing opposite to legs
      if(frame===0){ px(g,3,7,1,5,opts.infected?'#9ccc65':sh); px(g,12,7,1,4,opts.infected?'#9ccc65':sh); }
      else { px(g,3,7,1,4,opts.infected?'#9ccc65':sh); px(g,12,7,1,5,opts.infected?'#9ccc65':sh); }
    });
    cache[key]=c; return c;
  }
  function zombieSprite(type, frame){
    frame=frame||0;
    const key='z_'+type+'_'+frame;
    if(cache[key]) return cache[key];
    const def=ZT.ZOMBIES[type];
    const c=make(18,22,(g)=>{
      const col=def.color;
      px(g,6,0,7,7,col); px(g,6,0,7,2,'#33691e');
      px(g,7,3,1,1,'#000'); px(g,11,3,1,1,'#000');
      px(g,8,5,3,1,'#000'); px(g,8,6,1,1,'#b71c1c');
      px(g,5,7,9,8,col); px(g,5,7,9,2,'#33691e');
      // shambling legs
      if(frame===0){ px(g,4,15,3,6,'#37474f'); px(g,11,15,3,5,'#37474f'); }
      else { px(g,5,15,3,5,'#37474f'); px(g,10,15,3,6,'#37474f'); }
      // arms reaching forward, swaying
      const ay = frame===0?8:9;
      px(g,3,ay,2,6,col); px(g,14,ay,2,6,col);
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
