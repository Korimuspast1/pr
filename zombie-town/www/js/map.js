/* Map: terrain generation, grid helpers, flow-field pathfinding (BFS). */
'use strict';
const ZT = window.ZT;

ZT.Map = (function(){
  const W = ZT.MAP_W, H = ZT.MAP_H, T = ZT.TILE;
  let tiles = new Uint8Array(W*H);   // 0 grass 1 dirt 2 road 3 water 4 tree 5 rock 6 flower 7 sand
  let seed = 1;

  function rng(){ seed = (seed*1664525 + 1013904223) >>> 0; return seed/4294967296; }

  function idx(x,y){ return y*W+x; }
  function inBounds(x,y){ return x>=0 && y>=0 && x<W && y<H; }
  function getTile(x,y){ return inBounds(x,y) ? tiles[idx(x,y)] : 3; }
  function setTile(x,y,t){ if(inBounds(x,y)) tiles[idx(x,y)]=t; }
  function tileToWorld(x,y){ return { x: x*T+T/2, y: y*T+T/2 }; }
  function worldToTile(wx,wy){ return { x: Math.floor(wx/T), y: Math.floor(wy/T) }; }

  function generate(newSeed){
    seed = newSeed || ((Math.random()*1e9)|0);
    tiles = new Uint8Array(W*H);
    // base grass with dirt patches
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const r=rng();
      tiles[idx(x,y)] = r<0.06?6 : r<0.12?1 : 0;
    }
    // lake (water) — blob
    const lx = 6+((rng()*(W-12))|0), ly = 4+((rng()*(H-8))|0);
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const d = Math.hypot(x-lx, (y-ly)*1.4);
      if(d < 3.2+rng()*1.2) tiles[idx(x,y)]=3;
    }
    // forest clusters (trees)
    for(let c=0;c<7;c++){
      const cx=(rng()*W)|0, cy=(rng()*H)|0;
      for(let i=0;i<26;i++){
        const x=cx+(((rng()*7)|0)-3), y=cy+(((rng()*7)|0)-3);
        if(inBounds(x,y) && tiles[idx(x,y)]!==3 && rng()<0.75) tiles[idx(x,y)]=4;
      }
    }
    // rocks
    for(let i=0;i<26;i++){
      const x=(rng()*W)|0, y=(rng()*H)|0;
      if(tiles[idx(x,y)]!==3) tiles[idx(x,y)]=5;
    }
    // town center clearing
    const cx=(W/2)|0, cy=(H/2)|0;
    for(let y=cy-4;y<cy+5;y++)for(let x=cx-5;x<cx+6;x++){
      if(inBounds(x,y) && tiles[idx(x,y)]!==3) tiles[idx(x,y)] = (Math.abs(x-cx)<=2&&Math.abs(y-cy)<=2)?2:1;
    }
    // a road crossing through center
    for(let x=2;x<W-2;x++){ if(tiles[idx(x,cy)]!==3) tiles[idx(x,cy)]=2; }
    for(let y=2;y<H-2;y++){ if(tiles[idx(cx,y)]!==3) tiles[idx(cx,y)]=2; }
    return { cx, cy };
  }

  /* Walkability: water blocked; buildings block (except gates for citizens). */
  function isWalkable(x,y, forZombie, buildingGrid){
    if(!inBounds(x,y)) return false;
    const t = tiles[idx(x,y)];
    if(t===3) return false; // water
    const b = buildingGrid ? buildingGrid[idx(x,y)] : null;
    if(b){
      if(b.def.gate) return !forZombie; // citizens pass gates, zombies must break
      return false;
    }
    return true;
  }

  /* Flow field: BFS distance from target over walkable cells. Reused by all agents. */
  const DIST = new Int32Array(W*H);
  const QUEUE = new Int32Array(W*H);
  function computeFlow(tx, ty, forZombie, buildingGrid){
    DIST.fill(-1);
    if(!inBounds(tx,ty)) return;
    let qh=0, qt=0;
    QUEUE[qt++]=idx(tx,ty); DIST[idx(tx,ty)]=0;
    while(qh<qt){
      const cur=QUEUE[qh++]; const cx=cur%W, cy=(cur/W)|0; const d=DIST[cur]+1;
      const nb=[[1,0],[-1,0],[0,1],[0,-1]];
      for(let i=0;i<4;i++){
        const nx=cx+nb[i][0], ny=cy+nb[i][1];
        if(!inBounds(nx,ny)) continue;
        const ni=idx(nx,ny);
        if(DIST[ni]!==-1) continue;
        if(!isWalkable(nx,ny,forZombie,buildingGrid)) continue;
        DIST[ni]=d; QUEUE[qt++]=ni;
      }
    }
  }
  /* Next step direction from (x,y) tile toward target following flow field. */
  function flowDir(x,y){
    if(!inBounds(x,y)) return null;
    const cur=DIST[idx(x,y)];
    if(cur<=0) return null;
    let best=null, bestD=cur;
    const nb=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
    for(let i=0;i<8;i++){
      const nx=x+nb[i][0], ny=y+nb[i][1];
      if(!inBounds(nx,ny)) continue;
      const d=DIST[idx(nx,ny)];
      if(d>=0 && d<bestD){ bestD=d; best={x:nb[i][0],y:nb[i][1]}; }
    }
    return best;
  }
  function distAt(x,y){ return inBounds(x,y)?DIST[idx(x,y)]:-1; }

  /* Find a free buildable tile near center matching preferences. */
  function findSpot(cx, cy, w, h, buildingGrid, preferNearRoad){
    let best=null, bestScore=1e9;
    for(let y=2;y<H-h-2;y++)for(let x=2;x<W-w-2;x++){
      let ok=true;
      for(let by=0;by<h&&ok;by++)for(let bx=0;bx<w;bx++){
        const t=getTile(x+bx,y+by);
        if(t===3||t===4||t===5) { ok=false; break; }
        if(buildingGrid && buildingGrid[idx(x+bx,y+by)]) { ok=false; break; }
      }
      if(!ok) continue;
      const dCenter=Math.hypot(x+w/2-cx, y+h/2-cy);
      let nearRoad=0;
      for(let by=0;by<h;by++)for(let bx=0;bx<w;bx++){
        const t=getTile(x+bx,y+by);
        if(t===2) nearRoad=1;
      }
      const score=dCenter + (preferNearRoad&&!nearRoad?6:0) + rng()*4;
      if(score<bestScore){ bestScore=score; best={x,y}; }
    }
    return best;
  }

  function edgePoint(){
    const side=(rng()*4)|0;
    if(side===0) return { x:1, y:2+((rng()*(H-4))|0) };
    if(side===1) return { x:W-2, y:2+((rng()*(H-4))|0) };
    if(side===2) return { x:2+((rng()*(W-4))|0), y:1 };
    return { x:2+((rng()*(W-4))|0), y:H-2 };
  }
  function forestPoint(){
    for(let i=0;i<40;i++){ const x=(rng()*W)|0,y=(rng()*H)|0; if(getTile(x,y)===4) return {x,y}; }
    return edgePoint();
  }
  function serialize(){ return { seed, tiles: Array.from(tiles) }; }
  function deserialize(data){
    seed=data.seed; tiles=new Uint8Array(data.tiles);
    if(tiles.length!==W*H){ const c=generate(seed); return c; }
    return { cx:(W/2)|0, cy:(H/2)|0 };
  }
  return { W,H,T, generate, getTile, setTile, tileToWorld, worldToTile, inBounds,
           isWalkable, computeFlow, flowDir, distAt, findSpot, edgePoint, forestPoint,
           serialize, deserialize, idx };
})();
