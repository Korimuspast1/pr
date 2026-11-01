// The test city: a 20x20 tile diorama with four streets, a central square,
// a park, a farm and a handful of small businesses.

import { hash2 } from './util.js';

export const N = 20;

const WALKABLE = { road: true, pavement: true, plaza: true };

const tileKey = (x, y) => `${x},${y}`;

function building(type, name, x, y, w, d, door, extra = {}) {
  return { type, name, x, y, w, d, door, ...extra };
}

export function createWorld() {
  const tiles = new Array(N * N);
  for (let y = 0; y < N; y++) {
    for (let x = 0; x < N; x++) {
      tiles[y * N + x] = { x, y, type: 'grass', variant: Math.floor(hash2(x, y, 9) * 4) };
    }
  }
  const at = (x, y) => tiles[y * N + x];
  const setRect = (x, y, w, d, type, variant = 0) => {
    for (let j = y; j < y + d; j++) {
      for (let i = x; i < x + w; i++) {
        if (i < 0 || j < 0 || i >= N || j >= N) continue;
        at(i, j).type = type;
        at(i, j).variant = variant;
      }
    }
  };

  // --- streets -----------------------------------------------------------
  for (let y = 0; y < N; y++) {
    at(6, y).type = 'road';
    at(13, y).type = 'road';
  }
  for (let x = 0; x < N; x++) {
    at(x, 6).type = 'road';
    at(x, 13).type = 'road';
  }

  // --- squares, park, farm ------------------------------------------------
  setRect(7, 0, 6, 6, 'plaza'); // town-hall square
  setRect(7, 7, 6, 6, 'plaza'); // central square
  setRect(9, 16, 2, 2, 'water'); // park pond

  // footpaths through the park
  for (let y = 14; y < 20; y++) at(8, y).type = 'pavement';
  for (let x = 7; x < 12; x++) at(x, 18).type = 'pavement';

  // farm soil with crop rows
  setRect(3, 14, 3, 5, 'field');
  setRect(0, 14, 1, 5, 'field');

  // --- buildings ----------------------------------------------------------
  const buildings = [
    // north-west block
    building('house', 'Дом 1', 1, 1, 2, 2, { x: 1, y: 3 }, { palette: 0, floors: 2 }),
    building('house', 'Дом 2', 4, 1, 2, 2, { x: 4, y: 3 }, { palette: 1, floors: 2 }),
    building('cafe', 'Кафе', 1, 4, 2, 2, { x: 3, y: 4 }),

    // town-hall square
    building('townhall', 'Ратуша', 8, 1, 3, 3, { x: 11, y: 2 }),

    // north-east block
    building('shop', 'Магазин', 14, 1, 2, 2, { x: 14, y: 3 }),
    building('house', 'Дом 3', 17, 1, 2, 2, { x: 17, y: 3 }, { palette: 2, floors: 2 }),

    // west block
    building('bakery', 'Пекарня', 1, 7, 2, 2, { x: 3, y: 7 }),
    building('house', 'Дом 4', 4, 7, 2, 2, { x: 4, y: 9 }, { palette: 3, floors: 2 }),
    building('house', 'Дом 5', 1, 10, 2, 2, { x: 1, y: 12 }, { palette: 0, floors: 1 }),

    // workshop block
    building('house', 'Дом 6', 14, 7, 2, 2, { x: 14, y: 9 }, { palette: 1, floors: 2 }),
    building('workshop', 'Мастерская', 16, 8, 2, 2, { x: 16, y: 10 }),

    // farm
    building('barn', 'Ферма', 1, 15, 2, 2, { x: 1, y: 17 }),

    // south-east block
    building('house', 'Дом 7', 15, 15, 2, 2, { x: 15, y: 17 }, { palette: 2, floors: 1 }),
    building('house', 'Дом 8', 18, 15, 2, 2, { x: 18, y: 17 }, { palette: 3, floors: 2 }),
    building('house', 'Дом 9', 14, 18, 2, 2, { x: 14, y: 17 }, { palette: 0, floors: 2 }),
  ];

  // reserve building footprints before carving paths
  const blocked = new Set();
  for (const b of buildings) {
    for (let j = b.y; j < b.y + b.d; j++) {
      for (let i = b.x; i < b.x + b.w; i++) blocked.add(tileKey(i, j));
    }
  }

  // --- connect every door to the street network ---------------------------
  for (const b of buildings) {
    const door = { x: b.door.x, y: b.door.y };
    if (blocked.has(tileKey(door.x, door.y))) {
      throw new Error(`door of ${b.name} is inside the building`);
    }
    at(door.x, door.y).type = at(door.x, door.y).type === 'grass' ? 'pavement' : at(door.x, door.y).type;
    carveToRoad(at, blocked, door);
  }

  // --- props --------------------------------------------------------------
  const props = [];
  const trees = [];
  const addTree = (x, y, size = 1, kind = 'round') => trees.push({ kind, x, y, size });
  const addLamp = (x, y) => props.push({ kind: 'lamp', x, y });
  const addBench = (x, y, rot = 0) => props.push({ kind: 'bench', x, y, rot });
  const addBush = (x, y, s = 0.35) => props.push({ kind: 'bush', x, y, s });
  const addStall = (x, y, hue) => props.push({ kind: 'stall', x, y, hue });
  const addCrate = (x, y) => props.push({ kind: 'crate', x, y });
  const addFlowers = (x, y) => props.push({ kind: 'flowers', x, y });

  props.push({ kind: 'fountain', x: 9, y: 9 });
  props.push({ kind: 'busstop', x: 12.55, y: 8.4 });
  props.push({ kind: 'signpost', x: 5.5, y: 6.2 });
  props.push({ kind: 'signpost', x: 12.6, y: 12.5 });
  props.push({ kind: 'car', x: 6.35, y: 3.4, hue: 0 });
  props.push({ kind: 'car', x: 13.42, y: 16.4, hue: 1 });
  props.push({ kind: 'car', x: 3.2, y: 6.4, hue: 2 });
  props.push({ kind: 'bikes', x: 11.6, y: 6.6 });
  props.push({ kind: 'flag', x: 11.6, y: 0.6 });
  props.push({ kind: 'silo', x: 1.2, y: 18.4 });
  props.push({ kind: 'hydrant', x: 5.4, y: 8.4 });

  // central square: market stalls + benches around the fountain
  addStall(7.2, 8.2, 0);
  addStall(11.2, 8.2, 1);
  addStall(7.2, 11.2, 2);
  addStall(11.2, 11.2, 0);
  addBench(8.4, 10.9, 0);
  addBench(11.1, 10.4, 1);
  addBench(8.2, 7.6, 2);
  addFlowers(10.9, 11.9);
  addFlowers(7.6, 11.9);

  // town hall square
  addBench(7.6, 3.4, 0);
  addBench(11.6, 3.4, 1);
  addFlowers(7.4, 1.2);
  addFlowers(12.5, 4.4);

  // little gardens around the houses
  addBush(0.6, 2.4);
  addBush(3.4, 1.2);
  addBush(5.6, 3.6);
  addBush(16.4, 3.2);
  addBush(17.2, 4.4);
  addBush(2.6, 10.4);
  addBush(4.4, 11.2);
  addBush(13.4, 11.4);
  addBush(15.4, 13.2);
  addBush(17.2, 18.4);
  addBush(19.2, 17.4);
  addBush(2.4, 4.6);

  // workshop yard
  addCrate(15.4, 10.6);
  addCrate(15.9, 10.9);
  addCrate(18.4, 11.3);
  addCrate(18.6, 10.7);
  addCrate(2.6, 17.4);

  // farm fence
  for (let x = 2.2; x < 6; x += 0.7) props.push({ kind: 'fence', x, y: 13.7 });
  for (let y = 14; y < 19; y += 0.7) props.push({ kind: 'fence', x: 6.2, y });

  // street trees
  const streetTrees = [
    [3.2, 0.5, 0.85], [0.5, 4.6, 0.9], [5.4, 0.6, 0.8], [4.4, 4.4, 0.75],
    [7.6, 0.5, 0.8], [12.4, 0.6, 0.85], [15.4, 0.4, 0.8], [19.4, 3.2, 0.9],
    [13.4, 0.5, 0.75], [16.6, 4.6, 0.85], [0.6, 8.2, 0.8], [5.4, 6.9, 0.75],
    [2.4, 12.4, 0.85], [5.4, 11.6, 0.8], [14.4, 6.6, 0.8], [19.4, 8.6, 0.9],
    [15.4, 12.4, 0.75], [0.6, 13.4, 0.8], [2.2, 19.4, 0.9], [6.4, 19.4, 0.8],
    [7.6, 15.6, 0.85], [11.4, 14.6, 0.8], [12.4, 19.2, 0.9], [7.4, 19.4, 0.8],
    [13.4, 15.6, 0.85], [13.4, 19.4, 0.8], [17.4, 13.4, 0.8], [19.4, 13.4, 0.85],
  ];
  for (const [x, y, s] of streetTrees) addTree(x, y, s);

  // park
  addTree(10.6, 14.4, 1.05);
  addTree(11.6, 15.4, 0.9);
  addTree(7.4, 16.4, 0.95);
  addTree(9.2, 19.2, 0.9);
  addTree(11.2, 18.4, 0.85);
  addBench(7.6, 15.4, 0);
  addBench(11.5, 17.6, 1);
  addFlowers(7.4, 17.6);
  addFlowers(10.6, 19.4);

  // lamps along the four streets
  const lampSpots = [
    [5.4, 2.4], [5.4, 9.4], [5.4, 16.4], [12.4, 3.4], [12.4, 10.4], [12.4, 17.4],
    [2.4, 5.4], [9.4, 5.4], [16.4, 5.4], [2.4, 12.4], [9.4, 12.4], [16.4, 12.4],
    [7.4, 0.4], [12.4, 5.6], [19.4, 6.4], [0.4, 6.4], [19.4, 12.4], [0.4, 12.4],
  ];
  for (const [x, y] of lampSpots) addLamp(x, y);

  const world = {
    N,
    tiles,
    at: (x, y) => (x < 0 || y < 0 || x >= N || y >= N ? null : tiles[y * N + x]),
    typeAt: (x, y) => {
      if (x < 0 || y < 0 || x >= N || y >= N) return null;
      return tiles[y * N + x].type;
    },
    buildings,
    props,
    trees,
    blocked,
    isWalkable(x, y) {
      if (x < 0 || y < 0 || x >= N || y >= N) return false;
      if (blocked.has(tileKey(x, y))) return false;
      return !!WALKABLE[tiles[y * N + x].type];
    },
    walkKey: tileKey,
    findPath(from, to) {
      return findPath(this, from, to);
    },
  };
  return world;
}

/** Breadth-first search on the grass to the nearest street, then pave it. */
function carveToRoad(at, blocked, door) {
  const seen = new Set([tileKey(door.x, door.y)]);
  const prev = new Map();
  const queue = [door];
  let goal = null;
  while (queue.length) {
    const cur = queue.shift();
    if (at(cur.x, cur.y).type === 'road') {
      goal = cur;
      break;
    }
    const next = [
      { x: cur.x + 1, y: cur.y },
      { x: cur.x - 1, y: cur.y },
      { x: cur.x, y: cur.y + 1 },
      { x: cur.x, y: cur.y - 1 },
    ];
    for (const n of next) {
      if (n.x < 0 || n.y < 0 || n.x >= N || n.y >= N) continue;
      const k = tileKey(n.x, n.y);
      if (seen.has(k)) continue;
      if (blocked.has(k)) continue;
      const t = at(n.x, n.y);
      if (t.type === 'water' || t.type === 'field') continue;
      seen.add(k);
      prev.set(k, cur);
      queue.push(n);
    }
  }
  if (!goal) return;
  let cur = goal;
  while (cur) {
    const t = at(cur.x, cur.y);
    if (t.type === 'grass') t.type = 'pavement';
    cur = prev.get(tileKey(cur.x, cur.y));
  }
}

/** BFS path between two walkable tiles, returned as tile centres. */
function findPath(world, from, to) {
  const start = { x: Math.round(from.x), y: Math.round(from.y) };
  const goal = { x: Math.round(to.x), y: Math.round(to.y) };
  if (!world.isWalkable(start.x, start.y) || !world.isWalkable(goal.x, goal.y)) return null;
  const key = (p) => `${p.x},${p.y}`;
  if (key(start) === key(goal)) return [{ x: start.x + 0.5, y: start.y + 0.5 }];
  const prev = new Map();
  const seen = new Set([key(start)]);
  const queue = [start];
  let found = false;
  while (queue.length && !found) {
    const cur = queue.shift();
    for (const n of [
      { x: cur.x + 1, y: cur.y },
      { x: cur.x - 1, y: cur.y },
      { x: cur.x, y: cur.y + 1 },
      { x: cur.x, y: cur.y - 1 },
    ]) {
      const k = key(n);
      if (seen.has(k)) continue;
      if (!world.isWalkable(n.x, n.y)) continue;
      seen.add(k);
      prev.set(k, cur);
      if (n.x === goal.x && n.y === goal.y) {
        found = true;
        break;
      }
      queue.push(n);
    }
  }
  if (!found && key(start) !== key(goal)) {
    if (!seen.has(key(goal))) return null;
  }
  const tiles = [];
  let cur = goal;
  while (cur && key(cur) !== key(start)) {
    tiles.push({ x: cur.x + 0.5, y: cur.y + 0.5 });
    cur = prev.get(key(cur));
  }
  if (!cur) return null;
  tiles.push({ x: start.x + 0.5, y: start.y + 0.5 });
  tiles.reverse();
  return tiles;
}
