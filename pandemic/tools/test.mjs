/* Проверки инвариантов симуляции: node tools/test.mjs */
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);

globalThis.window = {};
require('../data/world.js');
globalThis.WORLD = globalThis.window.WORLD;
require('../js/data.js');
require('../js/sim.js');

const { Pandemic } = globalThis.PandemicSim;
const DATA = globalThis.GAME_DATA;

let passed = 0;
const test = (name, fn) => {
  fn();
  passed++;
  console.log('  ✓ ' + name);
};

console.log('Проверка мира');
test('все страны имеют геометрию, население и климат', () => {
  assert.equal(globalThis.WORLD.countries.length, 175);
  for (const c of globalThis.WORLD.countries) {
    assert.ok(c[3].startsWith('M'), c[0] + ' без пути');
    assert.ok(c[6] > 0, c[0] + ' без населения');
    assert.ok(['cold', 'mild', 'warm', 'tropical', 'arid'].includes(c[8]), c[0] + ' странный климат');
  }
});

test('соседи ссылаются на существующие страны', () => {
  const game = new Pandemic({ seed: 1 });
  const ids = new Set(globalThis.WORLD.countries.map((c) => c[0]));
  for (const c of game.countries) {
    for (const b of c.borders) assert.ok(ids.has(b), `${c.id} -> ${b}`);
  }
});

console.log('Проверка правил эволюции');
test('нельзя купить улучшение без требований', () => {
  const game = new Pandemic({ seed: 2 });
  assert.equal(game.canBuy('bioaerosol').reason, 'locked');
  game.owned.add('air1');
  game.dna = 100;
  assert.equal(game.canBuy('air2').ok, true);
  assert.equal(game.buy('air2').ok, true);
  assert.equal(game.canBuy('air2').reason, 'owned');
});

test('стоимость зависит от патогена', () => {
  const virus = new Pandemic({ pathogen: 'virus', seed: 3 });
  const fungus = new Pandemic({ pathogen: 'fungus', seed: 3 });
  assert.ok(virus.upgradeCost(DATA.UPGRADES.find((u) => u.id === 'fever')) <
    fungus.upgradeCost(DATA.UPGRADES.find((u) => u.id === 'fever')));
});

test('мутация выдаёт только доступные симптомы', () => {
  const game = new Pandemic({ seed: 4 });
  for (let i = 0; i < 40; i++) game.grantMutation();
  for (const id of game.owned) {
    const def = DATA.UPGRADES.find((u) => u.id === id);
    assert.equal(def.cat, 'symptom');
    for (const r of def.req) assert.ok(game.owned.has(r), id + ' без ' + r);
  }
});

console.log('Проверка симуляции');
test('инварианты держатся на всём прогоне', () => {
  const game = new Pandemic({ pathogen: 'virus', difficulty: 'normal', start: 'BRA', seed: 5 });
  const pick = () => {
    for (const u of [...DATA.UPGRADES].sort((a, b) => a.cost - b.cost)) if (game.canBuy(u.id).ok) return game.buy(u.id);
    return null;
  };
  for (let i = 0; i < 420 && !game.over; i++) {
    game.step();
    if (i % 2 === 0) pick();
    for (const c of game.countries) {
      assert.ok(c.pop > 0);
      assert.ok(c.dead >= 0 && c.dead <= c.pop + 1e-6, `${c.id} dead=${c.dead} pop=${c.pop}`);
      assert.ok(c.exp >= 0 && c.exp <= c.pop + 1e-6, `${c.id} exp=${c.exp} pop=${c.pop}`);
      assert.ok(c.inf >= -1e-9 && c.inf <= c.exp + 1e-6, `${c.id} inf=${c.inf}`);
      assert.ok(Number.isFinite(c.exp) && Number.isFinite(c.inf));
    }
    const s = game.summary();
    assert.ok(Number.isFinite(s.infected) && s.infected >= 0);
    assert.ok(s.dead <= game.totalPop + 1);
    assert.ok(s.cure >= 0 && s.cure <= 100);
  }
});

test('заражение расползается по миру', () => {
  const game = new Pandemic({ pathogen: 'virus', difficulty: 'normal', start: 'RUS', seed: 6 });
  for (let i = 0; i < 260 && !game.over; i++) game.step();
  const touched = game.countries.filter((c) => c.exp > 0).length;
  assert.ok(touched > 80, 'затронуто всего ' + touched + ' стран');
  assert.ok(game.summary().dead > 0);
});

test('без симптомов человечество выживает', () => {
  const game = new Pandemic({ pathogen: 'virus', difficulty: 'normal', start: 'RUS', seed: 7 });
  for (let i = 0; i < 900 && !game.over; i++) {
    game.step();
    if (i % 2 === 0) {
      for (const u of [...DATA.UPGRADES].filter((u) => u.cat !== 'symptom').sort((a, b) => a.cost - b.cost)) {
        if (game.canBuy(u.id).ok) {
          game.buy(u.id);
          break;
        }
      }
    }
  }
  assert.equal(game.outcome, 'defeat');
  assert.ok(game.deadTotal / game.totalPop < 0.5);
});

test('патоген может победить', () => {
  const game = new Pandemic({ pathogen: 'virus', difficulty: 'casual', start: 'RUS', seed: 8 });
  for (let i = 0; i < 900 && !game.over; i++) {
    game.step();
    if (i % 2 === 0) {
      for (const u of [...DATA.UPGRADES].sort((a, b) => a.cost - b.cost)) {
        if (game.canBuy(u.id).ok) {
          game.buy(u.id);
          break;
        }
      }
    }
  }
  assert.equal(game.outcome, 'victory');
});

test('детерминизм по seed', () => {
  const run = () => {
    const game = new Pandemic({ pathogen: 'bacteria', difficulty: 'brutal', start: 'IND', seed: 99 });
    for (let i = 0; i < 120; i++) game.step();
    return Math.round(game.deadTotal) + ':' + Math.round(game.infectedTotal);
  };
  assert.equal(run(), run());
});

console.log(`\nВсе проверки пройдены: ${passed}`);
