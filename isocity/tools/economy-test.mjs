// Headless check of the closed-loop economy.
//
//   node tools/economy-test.mjs [days] [seed]
//
// The last line must read "drift 0.000000000": the town has not created or
// destroyed a single ruble, no matter how many days it ran.

import { Sim } from '../src/economy.js';

const days = Number(process.argv[2] || 30);
const seed = Number(process.argv[3] || 7);

const sim = new Sim(seed);
sim.timeScale = 10; // 20 game minutes per simulation step

console.log(
  `старт: ${sim.totalMoney().toFixed(0)} ₽ · рабочих мест ${sim.firmList.reduce((a, f) => a + f.slots, 0)} · жителей ${sim.citizens.length} · без работы ${sim.unemployed.length}`,
);

let lastDay = sim.day;
let dayLedger = null;
while (sim.day <= days) {
  sim.update(0.1);
  if (sim.day !== lastDay) {
    lastDay = sim.day;
    const cash = sim.citizens.map((c) => c.cash).sort((a, b) => a - b);
    const L = sim.ledger;
    console.log(
      `день ${String(sim.day - 1).padStart(2)} · всего ${sim.totalMoney().toFixed(0)} ₽ · ` +
        `касса жителей ${cash[0].toFixed(0)}/${cash[Math.floor(cash.length / 2)].toFixed(0)}/${cash[cash.length - 1].toFixed(0)} · ` +
        `бюджет ${sim.municipality.cash.toFixed(0)} · голод ${(sim.citizens.reduce((a, c) => a + c.hunger, 0) / sim.citizens.length).toFixed(2)} · ` +
        `настроение ${(sim.citizens.reduce((a, c) => a + c.morale, 0) / sim.citizens.length).toFixed(2)}`,
    );
    dayLedger = { ...L };
  }
}

console.log('последний день, обороты:', Object.entries(dayLedger).map(([k, v]) => `${k}=${v.toFixed(0)}`).join(' '));
console.log('фирмы:', sim.firmList.map((f) => `${f.building.name}: ${f.cash.toFixed(0)} ₽, цена ${f.price.toFixed(1)}, ставка ${f.wage.toFixed(0)}, долг ${f.arrears.length}`).join(' | '));
console.log('drift', sim.auditMoney().toFixed(9));
