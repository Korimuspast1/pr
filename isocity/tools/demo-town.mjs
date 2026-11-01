// A small player-like plan for tests and screenshots: a road from the edge of
// the map, a little grid of streets, and zones along them. Nothing is spawned -
// the town has to grow this by itself, and every zone tile touches a road.

import { T } from '../src/world.js';

export function buildDemoTown(sim, opts = {}) {
  const w = sim.world;
  const road = (x, y) => {
    if (!w.inBounds(x, y)) return;
    if (w.occ[y * w.n + x] >= 0) return;
    if (w.get(x, y) !== T.GRASS) return;
    sim.buildRoad(x, y);
  };

  // the entrance: a road from the top edge of the map into the town
  for (let y = 0; y <= 14; y++) road(20, y);
  // three avenues and three streets, three tiles apart
  for (const x of [12, 16, 20, 24, 28]) for (let y = 14; y <= 26; y++) road(x, y);
  for (const y of [14, 18, 22, 26]) for (let x = 12; x <= 28; x++) road(x, y);

  // zone the blocks: every tile here borders a street
  const pick = opts.zone || defaultZone;
  for (let x = 13; x <= 27; x++) {
    for (let y = 15; y <= 25; y++) {
      if (w.get(x, y) !== T.GRASS || w.occ[y * w.n + x] >= 0) continue;
      if (!w.hasRoadNeighbour(x, y)) continue;
      sim.zone(x, y, pick(x, y));
    }
  }
  return sim;
}

function defaultZone(x, y) {
  // industry in the south-east, shops along the avenues, homes everywhere else
  if (x >= 24 && y >= 22) return 'i';
  if (x === 16 || y === 18) return (x + y) % 2 === 0 ? 'c' : 'r';
  if ((x + y) % 6 === 0) return 'c';
  return 'r';
}
