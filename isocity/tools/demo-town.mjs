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
  // a couple of parks raise the value of the streets around them
  place(sim, 'parkHard', 16, 20);
  place(sim, 'parkHard', 24, 20);

  // a player builds the first utilities right away
  place(sim, 'water', 19, 16);
  place(sim, 'power', 21, 16);
  return sim;
}

/** Try to put one service on a free tile near the town centre. */
export function place(sim, kind, gx, gy) {
  const w = sim.world;
  for (let r = 0; r < 6; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        const x = gx + dx;
        const y = gy + dy;
        if (!w.inBounds(x, y)) continue;
        if (w.get(x, y) === T.ROAD || w.get(x, y) === T.WATER) continue;
        if (w.occ[y * w.n + x] >= 0) continue;
        if (!w.hasRoadNeighbour(x, y)) continue;
        const res =
          kind === 'warehouse' ? sim.buildWarehouse(x, y) : kind === 'parkHard' ? sim.buildPark(x, y) : sim.buildService(kind, x, y);
        if (res.ok) return res;
      }
    }
  }
  return { ok: false };
}

/**
 * A stand-in for the player: once a day the town adds whatever service is
 * missing and already unlocked, while there is money for it. Tests use this so
 * they exercise the systems instead of starving the town.
 */
export function maintainServices(sim) {
  const order = ['water', 'power', 'busstop', 'school', 'clinic', 'landfill', 'warehouse', 'police', 'fire', 'landmark'];
  const counts = sim.serviceCounts();
  const houses = sim.countDone('house');
  for (const kind of order) {
    if (!sim.systems.isUnlocked(kind)) continue;
    const want = kind === 'water' ? Math.ceil(houses / 14) : kind === 'busstop' ? 2 : kind === 'power' ? Math.ceil(houses / 18) : 1;
    if ((counts[kind] || 0) >= Math.max(1, want)) continue;
    const spot = kind === 'water' ? [19, 16] : [18 + Object.keys(counts).length * 2, 16];
    const res = place(sim, kind, spot[0], spot[1]);
    if (res.ok) return res;
  }
  return { ok: false };
}

function defaultZone(x, y) {
  // industry in the south-east, shops along the avenues, homes everywhere else
  if (x >= 24 && y >= 22) return 'i';
  if (x === 16 || y === 18) return (x + y) % 2 === 0 ? 'c' : 'r';
  if ((x + y) % 6 === 0) return 'c';
  return 'r';
}
