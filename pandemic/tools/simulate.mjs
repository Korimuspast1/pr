/* Headless-прогон симуляции: node tools/simulate.mjs [strategy] [pathogen] [difficulty] [start] */
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);

globalThis.window = {};
require('../data/world.js');
globalThis.WORLD = globalThis.window.WORLD;
require('../js/data.js');
require('../js/sim.js');

const { Pandemic } = globalThis.PandemicSim;
const DATA = globalThis.GAME_DATA;

const strategy = process.argv[2] || 'balanced';
const pathogen = process.argv[3] || 'virus';
const difficulty = process.argv[4] || 'normal';
const start = process.argv[5] || 'RUS';

const game = new Pandemic({ pathogen, difficulty, start, seed: 12345, name: 'Тест' });

const order = {
  balanced: ['air1', 'water1', 'cough', 'air2', 'fever', 'drug1', 'runnyNose', 'air2', 'bioaerosol', 'pneumonia', 'hardening1', 'organFailure', 'coma', 'shock'],
  stealth: ['latency1', 'air1', 'water1', 'cough', 'air2', 'latency2', 'hardening1', 'bacteria', 'drug1', 'organFailure'],
  rush: ['air1', 'air2', 'bioaerosol', 'cough', 'fever', 'pneumonia', 'coma', 'organFailure'],
};

function tryBuy() {
  const pool = [...DATA.UPGRADES].sort((a, b) => a.cost - b.cost);
  for (const u of pool) {
    if (game.canBuy(u.id).ok) {
      game.buy(u.id);
      return true;
    }
  }
  return false;
}

let buys = 0;
while (!game.over && game.day < 900) {
  game.step();
  if (game.day % 3 === 0) {
    while (tryBuy()) buys++;
  }
  if (game.day % 25 === 0) {
    const s = game.summary();
    console.log(
      `d${String(s.day).padStart(3)} dna=${String(s.dna).padStart(3)} inf=${fmt(s.infected)} dead=${fmt(s.dead)} ` +
      `ctry=${String(s.countries).padStart(3)} det=${String(s.detected).padStart(3)} cure=${s.cure.toFixed(1)}% ` +
      `own=${s.owned}`,
    );
  }
}
const s = game.summary();
console.log('---');
console.log(`outcome=${game.outcome} day=${s.day} dead=${fmt(s.dead)} (${((s.dead / s.pop) * 100).toFixed(1)}%) cure=${s.cure.toFixed(1)}% upgrades=${s.owned} dnaLeft=${s.dna}`);
console.log('events:', game.events.slice(0, 6).map((e) => `[${e.day}] ${e.text}`).join(' | '));

function fmt(n) {
  if (n >= 1e9) return (n / 1e9).toFixed(2) + 'B';
  if (n >= 1e6) return (n / 1e6).toFixed(2) + 'M';
  if (n >= 1e3) return (n / 1e3).toFixed(1) + 'K';
  return Math.round(n).toString();
}
