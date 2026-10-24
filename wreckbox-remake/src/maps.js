import { PAL, mixCol } from './materials.js';
import { World } from './world.js';
import {
  EMPTY, SOIL, ROCK, CONCRETE, BRICK, STEEL, GLASS, WOOD, ASPHALT, PANEL,
  AERATED, SLATE, GLASSBLOCK, LEAD, CASTIRON, SAND, WATER, GRASS, AMMO, REACTOR, WIRE, PLASTIC,
} from './materials.js';

export const MAP_W = 460;
export const MAP_H = 300;
const GY = 232; // ground level (top of the street)

const rnd = (a = 1, b = 0) => b + Math.random() * (a - b);
const ri = (a, b) => Math.floor(rnd(a, b));

function ground(world, top = GY) {
  world.rect(0, top, MAP_W, 2, CONCRETE, 0, PAL.floorTile);   // sidewalk / pavement
  world.rect(0, top + 1, MAP_W, 1, CONCRETE, 0, 0xc4bfae);    // kerb edge
  world.rect(0, top + 2, MAP_W, 3, ASPHALT, 0, 0x3a3d42);     // street
  for (let x = 4; x < MAP_W; x += 14) world.setPainted(x, top + 3, ASPHALT, PAL.signYellow);
  world.rect(0, top + 5, MAP_W, 34, SOIL);          // soil
  world.rect(0, top + 39, MAP_W, MAP_H - top - 39, ROCK); // bedrock
  world.groundY = top;
}

const CAR_COLS = [PAL.carRed, PAL.carBlue, PAL.carTeal, PAL.carYellow, PAL.carWhite];

// Cars are vector props standing on the pavement (they carry no weight).
function car(world, x, y, colIdx = 0) {
  world.rect(x, y - 2, 9, 2, CASTIRON, 0, 0x4a4f57);   // kerb-side shadow slab
  world.prop('car', x, y - 6, 9, 6, { c: CAR_COLS[colIdx % CAR_COLS.length] });
}

// A tree is a vector prop: trunk + three overlapping foliage blobs.
function tree(world, x, y, h = 12) {
  const r = 4 + (h % 5) * 0.6;
  world.prop('tree', x - r, y - h - r * 1.6, r * 2, h + r * 1.6, {
    c: [PAL.leaf, PAL.leafDark, 0x6bb352][x % 3],
  });
}

function lamp(world, x, y) {
  world.prop('lampPost', x, y - 16, 2, 16);
  return { x: x + 2, y: y - 15, r: 60 };
}

// Underground room: slab, floor and walls, with columns carrying the ceiling.
function basement(world, x, y, w, h) {
  world.rect(x, y, w, 2, CONCRETE);              // ceiling slab
  world.rect(x, y + h - 1, w, 1, CONCRETE);      // floor
  world.rect(x, y, 2, h, CONCRETE);              // walls
  world.rect(x + w - 2, y, 2, h, CONCRETE);
  for (let i = x + 6; i < x + w - 3; i += 10) {
    world.rect(i, y + 2, 1, h - 3, CONCRETE);    // columns up to the slab
  }
  return { x, y, w, h };
}

function powerLine(world, x0, x1, y, poles = 3) {
  let prev = -1;
  for (let i = 0; i <= poles; i++) {
    const x = Math.round(x0 + ((x1 - x0) * i) / poles);
    world.rect(x, y - 22, 1, 22, CASTIRON);
    world.set(x + 1, y - 22, CASTIRON);
    world.set(x - 1, y - 22, CASTIRON);
    const node = world.addPowerNode(x, y - 22);
    if (prev >= 0) world.addPowerLink(prev, world.powerNodes.length - 1);
    prev = world.powerNodes.length - 1;
  }
  return world.powerNodes.length - 1;
}

function debrisPile(world, x, y, n = 40) {
  for (let i = 0; i < n; i++) {
    world.set(x + ri(-8, 8), y - ri(0, 4), Math.random() < 0.5 ? CONCRETE : BRICK);
  }
}

// ------------------------------------------------------------------ CITY
export function buildCity() {
  const world = new World(MAP_W, MAP_H);
  ground(world);

  // underground: metro tunnel + garage
  const metro = basement(world, 90, GY + 10, 120, 24);
  // the tunnel bore through the middle of the room
  world.rect(96, GY + 15, 108, 9, EMPTY);
  world.rect(90, GY + 17, 6, 6, EMPTY);
  world.rect(204, GY + 17, 14, 6, EMPTY);
  // portal frames keep the soil from dropping into the entrances
  world.rect(203, GY + 15, 17, 2, CONCRETE);
  world.rect(87, GY + 15, 9, 2, CONCRETE);
  world.rect(203, GY + 15, 2, 10, CONCRETE);
  world.rect(87, GY + 15, 2, 10, CONCRETE);
  for (let x = 98; x < 202; x += 13) world.rect(x, GY + 15, 2, 9, STEEL);
  world.rect(96, GY + 24, 108, 2, STEEL);   // rails
  // metro train standing in the tunnel: body, glazing bands and doors
  const trainY = GY + 19;
  for (let c = 0; c < 3; c++) {
    const tx = 112 + c * 26;
    world.rect(tx, trainY, 25, 5, STEEL, 0, c === 1 ? PAL.carYellow : PAL.carBlue);
    for (let wx = tx + 2; wx < tx + 23; wx += 3) {
      world.setPainted(wx, trainY + 1, STEEL, PAL.glassCar);
      world.setPainted(wx + 1, trainY + 1, STEEL, PAL.glassCar);
    }
    world.setPainted(tx + 12, trainY + 2, STEEL, PAL.awningWhite);
    world.setPainted(tx + 13, trainY + 2, STEEL, PAL.awningWhite);
  }
  // tunnel service lamps under the ceiling
  for (let lx = 100; lx < 202; lx += 16) world.paint(lx, GY + 15, PAL.lamp);

  const garage = basement(world, 300, GY + 8, 70, 22);
  world.rect(306, GY + 12, 58, 14, EMPTY);
  for (let cxx = 312; cxx < 362; cxx += 10) world.rect(cxx, GY + 10, 1, 20, CONCRETE);
  world.rect(300, GY + 15, 6, 7, EMPTY);
  // parked cars inside the garage
  car(world, 312, GY + 28, 1);
  car(world, 334, GY + 28, 3);
  car(world, 350, GY + 28, 2);
  for (let x = 310; x < 364; x += 12) world.rect(x, GY + 12, 2, 16, CONCRETE);

  const towers = [];
  // Chunky mid-rise blocks: wide, 3-6 storeys, brightly painted facades so the
  // city reads like the reference art instead of thin grey towers.
  const facades = [PAL.facBrick, PAL.facCream, PAL.facTeal, PAL.facSand,
    PAL.facOrange, PAL.facLilac, PAL.facMint, PAL.facRose];
  const specs = [
    { w: 46, h: 34, mat: BRICK, glass: GLASS, style: 'house', floors: 4 },
    { w: 38, h: 44, mat: PANEL, glass: GLASS, style: 'tower' },
    { w: 52, h: 30, mat: AERATED, glass: GLASS, style: 'house', floors: 3 },
    { w: 42, h: 38, mat: BRICK, glass: GLASS, style: 'house', floors: 4 },
    { w: 36, h: 50, mat: PANEL, glass: GLASS, style: 'tower' },
  ];
  let x = 6;
  let fi = 0;
  for (const s of specs) {
    const facade = facades[fi++ % facades.length];
    if (s.style === 'tower') {
      towers.push(world.stampTower(x, GY, s.w, s.h, {
        mat: s.mat, glass: s.glass, floorH: 6, colStep: 5, facade,
      }));
    } else {
      towers.push(world.stampHouse(x, GY, s.w, s.floors, {
        mat: s.mat, roof: SLATE, floorH: 6, facade,
      }));
    }
    x += s.w + 9;
  }
  // street furniture, left part
  for (let i = 0; i < 5; i++) car(world, 12 + i * 34, GY + 3, i % 2 ? STEEL : CASTIRON);
  tree(world, 60, GY, 13);
  tree(world, 130, GY, 16);
  tree(world, 176, GY, 11);
  lamp(world, 40, GY - 1);
  lamp(world, 210, GY - 1);

  // right downtown block
  let x2 = 250;
  const specs2 = [
    { w: 44, h: 52, mat: PANEL, glass: GLASS, style: 'tower' },
    { w: 40, h: 32, mat: BRICK, glass: GLASS, style: 'house', floors: 4 },
    { w: 50, h: 36, mat: AERATED, glass: GLASS, style: 'house', floors: 4 },
    { w: 38, h: 60, mat: PANEL, glass: GLASS, style: 'tower' },
    { w: 34, h: 28, mat: BRICK, glass: GLASS, style: 'house', floors: 3 },
  ];
  for (const s of specs2) {
    const facade = facades[fi++ % facades.length];
    if (s.style === 'tower') {
      towers.push(world.stampTower(x2, GY, s.w, s.h, {
        mat: s.mat, glass: s.glass, floorH: 6, colStep: 5, facade,
      }));
    } else {
      towers.push(world.stampHouse(x2, GY, s.w, s.floors, {
        mat: s.mat, roof: SLATE, floorH: 6, facade,
      }));
    }
    x2 += s.w + 9;
  }
  for (let i = 0; i < 5; i++) car(world, 256 + i * 34, GY + 3, i % 3 ? STEEL : CASTIRON);
  tree(world, 300, GY, 15);
  tree(world, 408, GY, 12);
  lamp(world, 340, GY - 1);
  lamp(world, 430, GY - 1);

  // rooftop details
  for (const t of towers) {
    if (t.floors > 8) {
      world.rect(t.x + 4, t.y - 6, 3, 6, STEEL);      // antenna
      world.rect(t.x + t.w - 8, t.y - 4, 5, 4, CASTIRON); // AC unit
    }
  }

  const lights = [lamp(world, 120, GY - 1), lamp(world, 300, GY - 1)];
  // power grid along the street
  powerLine(world, 30, 430, GY - 1, 6);
  world.addPowerNode(40, GY - 16, true, 0);

  const spawns = [];
  for (let i = 0; i < 16; i++) spawns.push([ri(10, 440), GY - 1]);
  // a couple of people on balconies
  spawns.push([70, GY - 30]);
  spawns.push([330, GY - 36]);

  world.captureTerrainAnchors();
  return { world, spawns, lights, name: 'city', defense: [] };
}

// ------------------------------------------------------------ MILITARY BASE
export function buildBase() {
  const world = new World(MAP_W, MAP_H);
  ground(world);

  // parade ground on top: asphalt square
  world.rect(90, GY - 2, 280, 3, ASPHALT);
  // power grid goes in first: buildings placed later cut it cleanly
  powerLine(world, 100, 400, GY - 1, 4);

  // bunker with ventilation, underground
  const bx = 60, by = GY + 6, bw = 150, bh = 40;
  world.rect(bx - 2, by - 2, bw + 4, bh + 4, SOIL);
  world.rect(bx, by, bw, bh, EMPTY);
  world.rect(bx - 2, by - 2, bw + 4, 2, CONCRETE);
  world.rect(bx - 2, by + bh, bw + 4, 2, CONCRETE);
  world.rect(bx - 2, by, 2, bh, CONCRETE);
  world.rect(bx + bw, by, 2, bh, CONCRETE);
  // columns (drawn before the slabs, which simply pass through them)
  for (let cx3 = bx + 6; cx3 < bx + bw - 4; cx3 += 8) world.rect(cx3, by - 2, 1, bh + 2, CONCRETE);
  // inner rooms
  world.rect(bx + 50, by, 2, bh, CONCRETE);
  world.rect(bx + 100, by, 2, bh, CONCRETE);
  world.rect(bx + 2, by + 20, 48, 2, CONCRETE);
  world.rect(bx + 52, by + 20, 48, 2, CONCRETE);
  world.rect(bx + 102, by + 14, 46, 2, CONCRETE);

  // bunker fit-out: khaki walls, bunks, crates, consoles (painted, never floating)
  const khaki = 0xcfc39c;
  for (let yy = by + 1; yy < by + bh - 1; yy++) {
    for (let xx = bx + 1; xx < bx + bw - 1; xx++) {
      const i = world.idx(xx, yy);
      if (!world.mat[i]) world.paint(xx, yy, khaki);
    }
  }
  const bunk = (x, y, w, h, m, c) => {
    for (let dy = 0; dy < h; dy++) for (let dx = 0; dx < w; dx++) {
      const y2 = y - dy;
      if (world.inB(x + dx, y2) && (dy === 0 || world.mat[world.idx(x + dx, y2 + 1)])) {
        world.setPainted(x + dx, y2, m, c);
      }
    }
  };
  for (let k = 0; k < 4; k++) {
    const bxx = bx + 6 + k * 11;
    bunk(bxx, by + bh - 1, 3, 1, WOOD, PAL.bedWhite);
    bunk(bxx + 2, by + bh - 2, 1, 1, WOOD, PAL.bedRed);
  }
  for (let k = 0; k < 6; k++) {
    const cx4 = bx + 8 + (k % 3) * 5;
    const cy4 = by + bh - 1 - ((k / 3) | 0) * 3;
    bunk(cx4, cy4, 2, 2, WOOD, 0x7f8a54);
  }
  for (const [cx5, cy5] of [[bx + 60, by + 14], [bx + 70, by + 14], [bx + 112, by + 8]]) {
    bunk(cx5, cy5, 4, 2, PLASTIC, PAL.tvDark);
  }
  for (let lx = bx + 14; lx < bx + bw - 6; lx += 22) world.paint(lx, by - 2, PAL.lamp);

  // lead lining inside the command-room ceiling (part of the slab)
  world.rect(bx + 2, by - 2, 46, 1, LEAD);
  // ventilation shaft with grille up to the surface
  world.rect(bx + 120, by - 12, 4, 52, CONCRETE);
  world.rect(bx + 121, by - 11, 2, 14, EMPTY);
  world.rect(bx + 120, by - 12, 4, 1, STEEL);
  world.rect(bx + 121, by + 4, 2, 40, EMPTY);
  world.addPowerNode(bx + 30, by + 8, true);
  world.addPowerNode(bx + 90, by + 10);
  world.addPowerNode(bx + 140, by + 10);
  world.addPowerLink(0, 1);
  world.addPowerLink(1, 2);

  // ammo depot (above ground, sandbag walls)
  const ax = 130, ay = GY;
  world.rect(ax - 3, ay - 14, 3, 14, SAND);
  world.rect(ax + 46, ay - 14, 3, 14, SAND);
  world.rect(ax, ay - 3, 46, 3, SAND);
  world.rect(ax, ay - 4, 46, 1, PANEL);
  for (let i = 0; i < 5; i++) {
    world.rect(ax + 4 + i * 8, ay - 4, 4, 4, AMMO);
    world.rect(ax + 5 + i * 8, ay - 7, 2, 3, AMMO);
  }

  // barracks (brick, two storeys)
  world.stampHouse(230, GY, 40, 3, { mat: BRICK, roof: SLATE, floorH: 6, facade: PAL.facBrick });
  world.stampHouse(290, GY, 30, 2, { mat: PANEL, roof: SLATE, floorH: 6, facade: PAL.facCream });

  // command tower with radar
  const tw = world.stampTower(350, GY, 26, 42, {
    mat: CONCRETE, glass: GLASSBLOCK, floorH: 6, colStep: 5, facade: PAL.facMint,
  });
  world.rect(355, tw.y - 10, 2, 10, STEEL);
  world.rect(352, tw.y - 16, 12, 1, STEEL);
  world.rect(348, tw.y - 22, 20, 2, STEEL);
  world.rect(352, tw.y - 20, 12, 1, GLASS);
  world.rect(354, tw.y - 19, 8, 3, STEEL);
  const radar = { x: 360, y: tw.y - 22, kind: 'radar', cooldown: 0 };

  // anti-aircraft guns on the parade ground
  const aa = [];
  for (const gx of [185, 275, 405]) {
    world.rect(gx, GY - 4, 8, 4, STEEL);
    world.rect(gx + 2, GY - 10, 3, 6, CASTIRON);
    world.rect(gx + 1, GY - 12, 5, 2, STEEL);
    aa.push({ x: gx + 4, y: GY - 12, kind: 'aa', cooldown: 0 });
  }

  // vehicles on the square
  for (let i = 0; i < 4; i++) {
    const vx = 96 + i * 26;
    const olive = i % 2 ? 0x6f7c4a : 0x59654a;
    world.rect(vx, GY - 7, 18, 5, STEEL, 0, olive);            // armour
    world.rect(vx + 2, GY - 10, 9, 3, STEEL, 0, mixCol(olive, 0xffffff, 0.12)); // turret
    world.rect(vx + 12, GY - 9, 8, 1, CASTIRON, 0, 0x3d423a);  // barrel
    world.rect(vx + 1, GY - 2, 16, 2, CASTIRON, 0, PAL.tire);  // tracks
    world.setPainted(vx + 5, GY - 6, STEEL, PAL.glassCar);
  }

  // perimeter fence: posts with two rails on the far left and right
  for (let fx = 12; fx < 96; fx += 12) {
    world.rect(fx, GY - 11, 1, 11, CASTIRON);
  }
  world.rect(12, GY - 11, 84, 1, CASTIRON);
  world.rect(12, GY - 6, 84, 1, CASTIRON);
  for (let fx = 418; fx < 452; fx += 12) world.rect(fx, GY - 11, 1, 11, CASTIRON);
  world.rect(418, GY - 11, 34, 1, CASTIRON);
  world.rect(418, GY - 6, 34, 1, CASTIRON);

  tree(world, 30, GY, 12);
  tree(world, 440, GY, 14);

  const spawns = [];
  for (let i = 0; i < 14; i++) spawns.push([ri(60, 420), GY - 1]);
  for (let i = 0; i < 6; i++) spawns.push([ri(bx + 6, bx + bw - 6), by + bh - 2]);
  spawns.push([356, tw.y + 10]);

  world.captureTerrainAnchors();
  return {
    world, spawns, name: 'base',
    defense: [...aa, radar],
    lights: [],
  };
}

// ---------------------------------------------------------------- ATOMGRAD
export function buildAtomgrad() {
  const world = new World(MAP_W, MAP_H);
  ground(world);

  // cooling tower (shell with stiffening rings, like the real thing)
  const cx = 120;
  for (let i = 0; i < 52; i++) {
    const t = i / 52;
    const half = 10 + Math.sin(t * Math.PI) * 7;
    const y = GY - 52 + i;
    world.rect(cx - half, y, 3, 1, CONCRETE);
    world.rect(cx + half - 3, y, 3, 1, CONCRETE);
    if (i % 4 === 0) world.rect(cx - half, y, half * 2, 1, CONCRETE);
    // inner service framework keeps the shell rings from reaching too far
    if (i % 2 === 0) {
      world.rect(cx - 4, y, 1, 2, STEEL);
      world.rect(cx + 4, y, 1, 2, STEEL);
      world.rect(cx - 8, y, 1, 2, STEEL);
      world.rect(cx + 8, y, 1, 2, STEEL);
    }
  }
  world.rect(cx - 10, GY - 2, 20, 2, CONCRETE);
  for (let k = -14; k <= 14; k += 7) world.rect(cx + k, GY - 8, 1, 8, STEEL);

  // reactor hall: outer shell, two interior floors, load-bearing steel frame
  const rx = 200;
  world.rect(rx, GY - 46, 90, 46, PANEL, 0, PAL.facSand);
  world.rect(rx, GY - 12, 90, 4, PANEL, 0, PAL.facTeal);
  world.rect(rx + 4, GY - 42, 82, 40, EMPTY);
  world.rect(rx + 4, GY - 42, 82, 2, CONCRETE, 0, PAL.concreteLight);   // ceiling under the roof
  world.rect(rx + 4, GY - 24, 82, 2, CONCRETE, 0, PAL.floorWood);   // mezzanine slab
  world.rect(rx + 4, GY - 6, 82, 2, CONCRETE, 0, PAL.floorWood);    // basement slab
  // roof trusses and skylight strips
  for (let tx = rx + 8; tx < rx + 86; tx += 10) world.rect(tx, GY - 45, 2, 3, STEEL, 0, PAL.metalRail);
  for (let tx = rx + 12; tx < rx + 84; tx += 10) world.paint(tx, GY - 46, PAL.glassCar);
  // steel frame: goes last so fittings and lead can't cut the load path
  for (let cx2 = rx + 4; cx2 < rx + 86; cx2 += 6) {
    world.rect(cx2, GY - 42, 1, 18, STEEL);
    world.rect(cx2, GY - 24, 1, 18, STEEL);
    world.rect(cx2, GY - 6, 1, 4, STEEL);
  }
  // lead shielding around the reactor room
  world.rect(rx + 36, GY - 40, 18, 2, LEAD);
  world.rect(rx + 36, GY - 40, 2, 16, LEAD);
  world.rect(rx + 52, GY - 40, 2, 16, LEAD);
  // reactor vessel on its pedestal
  world.rect(rx + 39, GY - 24, 12, 18, CONCRETE);
  world.rect(rx + 38, GY - 30, 14, 8, EMPTY);
  world.disc(rx + 45, GY - 28, 5, REACTOR);
  world.rect(rx + 42, GY - 24, 7, 2, STEEL);
  world.rect(rx + 20, GY - 20, 3, 14, CASTIRON);
  world.rect(rx + 70, GY - 20, 3, 14, CASTIRON);
  world.rect(rx + 5, GY - 15, 4, 8, GLASSBLOCK);
  world.rect(rx + 81, GY - 15, 4, 8, GLASSBLOCK);
  world.rect(rx + 14, GY - 18, 12, 10, CASTIRON);          // turbine
  world.rect(rx + 58, GY - 18, 10, 10, STEEL);             // pumps
  // control room on the mezzanine
  world.rect(rx + 66, GY - 42, 18, 14, CONCRETE);
  world.rect(rx + 68, GY - 40, 14, 10, EMPTY);
  world.rect(rx + 68, GY - 31, 14, 1, CASTIRON);
  // window band set into the walls of the control room
  world.rect(rx + 66, GY - 38, 2, 4, GLASSBLOCK);
  world.rect(rx + 82, GY - 38, 2, 4, GLASSBLOCK);
  // pipework running up the hall
  world.rect(rx + 30, GY - 42, 2, 40, STEEL);
  world.rect(rx + 60, GY - 42, 2, 18, CASTIRON);
  world.rect(rx + 26, GY - 30, 32, 2, STEEL);
  // stairs to the mezzanine
  for (let st = 0; st < 18; st++) world.rect(rx + 6 + st, GY - 7 - st, 2, 1, CONCRETE);

  // turbine hall
  world.stampTower(300, GY, 46, 34, { mat: PANEL, glass: GLASS, floorH: 6, colStep: 5, facade: PAL.facOrange });
  world.rect(304, GY - 6, 16, 6, CASTIRON);
  world.rect(326, GY - 6, 16, 6, CASTIRON);

  // town: aerated houses with glass blocks
  let x = 360;
  for (const s of [{ w: 24, h: 26 }, { w: 20, h: 22 }, { w: 28, h: 30 }, { w: 22, h: 24 }]) {
    world.stampHouse(x, GY, s.w, Math.max(2, Math.round(s.h / 7)), {
      mat: AERATED, roof: SLATE, floorH: 6, facade: [PAL.facCream, PAL.facMint][x % 2],
    });
    x += s.w + 8;
  }
  // left side town
  world.stampHouse(20, GY, 26, 3, { mat: BRICK, roof: SLATE, floorH: 6, facade: PAL.facBrick });
  world.stampHouse(54, GY, 22, 2, { mat: PANEL, roof: SLATE, floorH: 6, facade: PAL.facLilac });
  world.stampHouse(84, GY, 20, 2, { mat: AERATED, roof: SLATE, floorH: 6, facade: PAL.facMint });

  // civil defence shelter: concrete box with lead roof and its own vent
  const sx = 400, sy = GY + 8;
  world.rect(sx - 2, sy - 2, 52, 26, SOIL);
  world.rect(sx, sy - 2, 48, 24, EMPTY);
  for (let sx3 = sx + 4; sx3 < sx + 47; sx3 += 6) world.rect(sx3, sy - 4, 1, 26, CONCRETE);
  world.rect(sx - 2, sy - 4, 52, 2, LEAD);
  world.rect(sx - 2, sy - 2, 52, 2, CONCRETE);
  world.rect(sx - 2, sy + 20, 52, 3, CONCRETE);
  world.rect(sx - 2, sy, 3, 22, CONCRETE);
  world.rect(sx + 47, sy, 3, 22, CONCRETE);
  world.rect(sx + 10, sy + 8, 28, 2, CONCRETE);
  // vent
  world.rect(sx + 30, sy - 16, 3, 15, CONCRETE);
  world.rect(sx + 31, sy - 15, 1, 13, EMPTY);
  world.rect(sx + 30, sy - 16, 3, 1, STEEL);

  // power grid: plant feeds the town, with pylons
  world.addPowerNode(rx + 45, GY - 40, true, 0);
  let prevIdx = 0;
  for (const px of [330, 380, 430, 250, 180, 130]) {
    world.rect(px, GY - 26, 1, 26, CASTIRON);
    world.rect(px - 2, GY - 26, 5, 1, CASTIRON);
    world.addPowerNode(px, GY - 26);
    const idx = world.powerNodes.length - 1;
    world.addPowerLink(prevIdx, idx);
    prevIdx = idx;
  }

  tree(world, 60, GY, 12);
  tree(world, 175, GY, 14);
  tree(world, 355, GY, 11);
  for (let i = 0; i < 3; i++) car(world, 250 + i * 20, GY + 2, i + 1);

  const spawns = [];
  for (let i = 0; i < 12; i++) spawns.push([ri(20, 440), GY - 1]);
  spawns.push([sx + 20, sy + 20]);
  spawns.push([sx + 30, sy + 20]);

  world.captureTerrainAnchors();
  return { world, spawns, name: 'atomgrad', defense: [], lights: [], reactor: { x: rx + 45, y: GY - 32 } };
}

export const MAPS = {
  city: { build: buildCity, ru: 'Город', en: 'City' },
  base: { build: buildBase, ru: 'Военная база', en: 'Military base' },
  atomgrad: { build: buildAtomgrad, ru: 'Атомград', en: 'Atomgrad' },
};
