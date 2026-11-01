// Headless run of the economy: paints a plan, lets the town grow for N days and
// checks the books. Ends with "money audit 0.000000000" when nothing was
// printed or destroyed inside the city.
//
//   node tools/live-test.mjs [days] [seed]

import { Sim } from '../src/sim.js';
import { buildDemoTown } from './demo-town.mjs';

const days = Number(process.argv[2] || 30);
const sim = new Sim(Number(process.argv[3] || 7));
buildDemoTown(sim);

const rows = [];
let lastDay = sim.day;
while (sim.day <= days) {
  sim.update(0.1);
  if (sim.day !== lastDay) {
    lastDay = sim.day;
    rows.push(sim.stats());
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
console.log('дорога до края карты:', sim.townConnected(), '· машины на улицах:', sim.cars.length);
console.log('переселенцы принесли', sim.external.in.toFixed(0), '· уехавшие забрали', sim.external.out.toFixed(0), '· продано за границу города', sim.exportIncome.toFixed(0));
console.log('money audit', sim.audit().toFixed(9));
if (Math.abs(sim.audit()) > 0.01) throw new Error('в городе напечатали или сожгли деньги');
if (!sim.townConnected()) throw new Error('город потерял связь с краем карты');
if (s.pop < 10) throw new Error('город не вырос: жители не приезжают');
