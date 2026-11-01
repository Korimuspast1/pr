// Headless run of the economy: paints a plan, lets the town grow for N days and
// checks the books. Ends with "money audit 0.000000000" when nothing was
// printed or destroyed inside the city.
//
//   node tools/live-test.mjs [days] [seed]

import { Sim } from '../src/sim.js';
import { T } from '../src/world.js';

const days = Number(process.argv[2] || 30);
const sim = new Sim(Number(process.argv[3] || 7));
const w = sim.world;

const road = (x, y) => {
  if (w.occ[y * w.n + x] >= 0) return; // do not pave over the existing town
  if (w.get(x, y) === T.GRASS) sim.buildRoad(x, y);
};
for (let x = 9; x <= 31; x++) { road(x, 12); road(x, 28); road(x, 16); road(x, 24); }
for (let y = 12; y <= 28; y++) { road(9, y); road(31, y); road(14, y); road(26, y); }
for (let x = 10; x <= 30; x++) for (let y = 13; y <= 27; y++) {
  if (w.get(x, y) !== T.GRASS || w.occ[y * w.n + x] >= 0) continue;
  const bx = Math.floor((x - 10) / 5);
  const by = Math.floor((y - 14) / 5);
  sim.zone(x, y, (bx + by * 3) % 3 === 2 ? 'i' : (bx + by * 3) % 3 === 1 ? 'c' : 'r');
}

const rows = [];
let lastDay = sim.day;
while (sim.day <= days) {
  sim.update(0.1);
  if (sim.day !== lastDay) {
    lastDay = sim.day;
    const s = sim.stats();
    rows.push(s);
  }
}
const show = [0, 2, 4, 9, 19, 29, rows.length - 1].filter((i) => i >= 0 && i < rows.length);
for (const i of show) {
  const s = rows[i];
  console.log(
    `день ${String(i + 1).padStart(2)} · бюджет ${String(Math.round(s.budget)).padStart(4)} · людей ${String(s.pop).padStart(2)} · работают ${s.employed}/${s.jobs} · дома ${s.houses} лавки ${s.shops} заводы ${s.factories} · настроение ${(s.avgMood * 100).toFixed(0)}%`,
  );
}
const s = sim.stats();
console.log('переселенцы принесли', sim.external.in.toFixed(0), '· уехавшие забрали', sim.external.out.toFixed(0));
console.log('money audit', sim.audit().toFixed(9));
if (Math.abs(sim.audit()) > 0.01) throw new Error('в городе напечатали или сожгли деньги');
