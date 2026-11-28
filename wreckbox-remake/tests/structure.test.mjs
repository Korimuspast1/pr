// Headless checks for the structural solver: run with `node tests/structure.test.mjs`
import { World } from '../src/world.js';
import { PANEL, CONCRETE, BRICK, EMPTY, DEBRIS } from '../src/materials.js';

let failures = 0;
function check(name, cond, extra = '') {
  console.log(`${cond ? 'OK  ' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
  if (!cond) failures++;
}

function topOf(world, mat) {
  let top = world.h;
  for (let y = 0; y < world.h; y++) {
    for (let x = 0; x < world.w; x++) if (world.at(x, y) === mat) { top = Math.min(top, y); }
  }
  return top;
}

function run(world, steps = 120) {
  for (let s = 0; s < steps; s++) {
    world.update(1 / 60, s / 60);
  }
}

// 1. A normal panel tower must stand still.
{
  const w = new World(160, 140);
  w.rect(0, 108, 160, 4, 3);       // ground slab
  w.rect(0, 112, 160, 28, 2);      // rock
  w.stampTower(60, 108, 24, 60, { mat: PANEL, floorH: 6 });
  w.settle();
  const before = w.stats.solid;
  run(w, 200);
  const lost = before - w.stats.solid;
  check('tower stands under its own weight', lost <= 12, `lost ${lost} cells of ${before}`);
}

// 2. Removing the whole ground floor brings the tower down and leaves rubble.
{
  const w = new World(160, 140);
  w.rect(0, 108, 160, 4, 3);
  w.rect(0, 112, 160, 28, 2);
  w.stampTower(60, 108, 24, 60, { mat: PANEL, floorH: 6 });
  w.settle();
  const topBefore = topOf(w, PANEL);
  w.rect(60, 102, 24, 6, EMPTY); // knock out the entire ground storey
  run(w, 400);
  const topAfter = topOf(w, PANEL);
  let debris = 0;
  for (let i = 0; i < w.n; i++) if (w.mat[i] === DEBRIS) debris++;
  check('tower drops when its support is removed', topAfter > topBefore + 4, `${topBefore} -> ${topAfter}`);
  check('collapse leaves rubble behind', debris > 50, `${debris} debris cells`);
}

// 3. A detached block must fall to the ground.
{
  const w = new World(80, 80);
  w.rect(0, 74, 80, 6, 2);
  w.rect(30, 20, 8, 8, CONCRETE);
  w.settle();
  const yBefore = 20;
  run(w, 160);
  let top = 999;
  for (let y = 0; y < w.h; y++) for (let x = 0; x < w.w; x++) if (w.at(x, y) === CONCRETE) top = Math.min(top, y);
  check('detached block falls down', top > yBefore + 20, `top now y=${top}`);
}

// 4. Explosion removes cells and chain-ignites nothing without fuel.
{
  const w = new World(100, 80);
  w.rect(0, 60, 100, 20, 2);
  w.rect(40, 40, 20, 20, BRICK);
  w.settle();
  const n = w.explode(50, 50, 10, 2);
  check('explosion destroys bricks', n > 20, `destroyed ${n}`);
}

// 5. A sheltered cell gets less radiation than an exposed one.
{
  const w = new World(60, 80);
  w.rect(0, 68, 60, 12, 2);
  for (let y = 40; y < 60; y++) w.rect(20, y, 20, 1, 3); // thick concrete roof
  const shielded = w.shieldAt(30, 65);
  const exposed = w.shieldAt(5, 65);
  check('overhead concrete shields fallout', shielded < exposed * 0.5, `${shielded.toFixed(3)} vs ${exposed.toFixed(3)}`);
}

console.log(failures ? `\n${failures} failure(s)` : '\nAll structural checks passed');
process.exit(failures ? 1 : 0);
