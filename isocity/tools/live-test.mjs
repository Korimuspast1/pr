// Headless run of the economy: paints a plan, lets the town grow for N days and
// checks the books. Ends with "money audit 0.000000000" when nothing was
// printed or destroyed inside the city.
//
//   node tools/live-test.mjs [days] [seed]

import { Sim } from '../src/sim.js';
import { buildDemoTown, maintainServices } from './demo-town.mjs';

const days = Number(process.argv[2] || 30);
const sim = new Sim(Number(process.argv[3] || 7));
buildDemoTown(sim);

const rows = [];
let lastDay = sim.day;
while (sim.day <= days) {
  sim.update(0.1);
  if (sim.minute === 9 * 60) maintainServices(sim);
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

// ------------------------------------------------ the systems really did something
const counts = sim.serviceCounts();
const m = sim.metrics;
const levels = sim.world.buildings.filter((b) => b.kind === 'house').map((b) => b.level || 1);
const up = levels.filter((l) => l > 1).length;
const alive = {
  'службы построены': Object.values(counts).reduce((a, n) => a + n, 0),
  'вехи роста': sim.milestones.length,
  'сезон': sim.currentSeason().name,
  'родилось': m.births,
  'вышло на пенсию': m.retired,
  'с образованием': sim.citizens.filter((c) => c.edu > 0.5).length,
  'заболело': m.sick,
  'краж': m.crimes,
  'пожаров': m.fires,
  'туризм, ₽': Math.round(m.tourism),
  'налог на землю, ₽': Math.round(m.propertyTax),
  'содержание служб, ₽': Math.round(m.upkeep),
  'домов выросло до 2–3 этажей': up,
  'заброшено': sim.world.buildings.filter((b) => b.abandoned).length,
  'мусор на улицах': Math.round(sim.waste),
  'политик включено': Object.values(sim.policies).filter(Boolean).length,
};
console.log('механики:', JSON.stringify(alive));
const dead = [];
if (alive['службы построены'] < 4) dead.push('службы');
if (alive['вехи роста'] < 3) dead.push('вехи');
if (m.births + m.retired === 0) dead.push('возраст');
if (Math.round(m.tourism) <= 0) dead.push('туризм');
if (Math.round(m.propertyTax) <= 0) dead.push('налог на землю');
if (Math.round(m.upkeep) <= 0) dead.push('содержание');
if (up === 0) dead.push('рост домов');
if (m.crimes + m.fires + m.sick === 0) dead.push('события');
if (dead.length) throw new Error(`механики не сработали: ${dead.join(', ')}`);
console.log('переселенцы принесли', sim.external.in.toFixed(0), '· уехавшие забрали', sim.external.out.toFixed(0), '· продано за границу города', sim.exportIncome.toFixed(0));
console.log('money audit', sim.audit().toFixed(9));
if (Math.abs(sim.audit()) > 0.01) throw new Error('в городе напечатали или сожгли деньги');
if (!sim.townConnected()) throw new Error('город потерял связь с краем карты');
if (s.pop < 10) throw new Error('город не вырос: жители не приезжают');
