// Boots the real app in a fake browser (node canvas) and drives it like a
// player: paints a road to the edge of the map, zones the land, lets the town
// grow, then checks every rule that matters:
//
//   * an empty map really is empty, and nothing grows without a road to the edge
//   * a road that reaches the border lets people and materials in
//   * every walker stands on a road tile, never inside a building
//   * cars drive on roads and the HUD does not scale with the zoom
//   * no money appears out of nowhere (audit = 0)

import { createCanvas } from '@napi-rs/canvas';

const W = 1280;
const H = 800;

const listeners = {};
function makeCanvas(w, h) {
  const c = createCanvas(w || 1, h || 1);
  c.clientWidth = w || W;
  c.clientHeight = h || H;
  c.getBoundingClientRect = () => ({ left: 0, top: 0, width: c.clientWidth, height: c.clientHeight });
  c.setPointerCapture = () => {};
  c.releasePointerCapture = () => {};
  c.addEventListener = (k, fn) => { (listeners[k] ||= []).push(fn); };
  return c;
}

const main = makeCanvas(W, H);
globalThis.window = {
  devicePixelRatio: 1,
  innerWidth: W,
  innerHeight: H,
  addEventListener: (k, fn) => { (listeners[k] ||= []).push(fn); },
};
globalThis.document = {
  getElementById: () => main,
  createElement: () => makeCanvas(1, 1),
};
let pending = null;
globalThis.requestAnimationFrame = (fn) => { pending = fn; };
globalThis.performance = globalThis.performance || { now: () => Date.now() };

await import('../src/main.js');
const { sim, renderer } = window.city;

function tick(frames = 1, ms = 16.7) {
  let t = 0;
  for (let i = 0; i < frames; i++) {
    const fn = pending;
    pending = null;
    t += ms;
    fn(t);
  }
}

function fire(kind, x, y, extra = {}) {
  const ev = {
    clientX: x,
    clientY: y,
    button: 0,
    pointerId: 1,
    shiftKey: false,
    preventDefault() {},
    touches: [],
    ...extra,
  };
  for (const fn of listeners[kind] || []) fn(ev);
}

const isoXY = (gx, gy) => ({ x: (gx - gy) * 16, y: (gx + gy) * 8 });

function paint(gx, gy, button = 0) {
  const world = isoXY(gx + 0.5, gy + 0.5);
  const screen = renderer.worldToScreen(world.x, world.y);
  fire('pointerdown', screen.x, screen.y, { button });
  fire('pointerup', screen.x, screen.y, { button });
}

const fail = (msg) => {
  console.error('FAIL:', msg);
  process.exitCode = 1;
};

tick(10);

// ---------------------------------------------------------------- empty start
if (sim.citizens.length !== 0) fail('карта должна начинаться без жителей');
if (sim.world.buildings.length !== 0) fail('карта должна начинаться без зданий');
if (sim.townConnected()) fail('в пустом городе неоткуда въехать');
console.log('empty map:', sim.citizens.length, 'citizens,', sim.world.buildings.length, 'buildings, gate', sim.townConnected());

// ------------------------------------------- a dead-end street changes nothing
renderer.tool = 'road';
for (let x = 15; x <= 25; x++) paint(x, 15);
renderer.tool = 'zoneR';
for (let x = 16; x <= 24; x++) paint(x, 16);
for (let i = 0; i < 1440 * 3; i++) sim.update(0.1);
tick(3);
const deadEnd = sim.stats();
console.log('dead-end street after 3 days:', JSON.stringify({ pop: deadEnd.pop, houses: deadEnd.houses, sites: deadEnd.sites, gate: sim.townConnected() }));
if (sim.townConnected()) fail('улица без выезда не должна считаться въездом');
if (deadEnd.pop !== 0 || deadEnd.houses !== 0) fail('без дороги к краю карты никто не должен приезжать и строиться');

// ------------------------------------------------ connect the street to the edge
renderer.tool = 'road';
for (let y = 0; y <= 15; y++) paint(20, y);
// a second street so the blocks have street frontage on both sides, joined
// to the main road so the whole block is on the entrance network
for (let x = 15; x <= 25; x++) paint(x, 19);
for (let y = 16; y <= 19; y++) paint(20, y);
tick(2);
console.log('road to the edge painted, connected:', sim.townConnected(), 'budget', sim.budget.toFixed(0));
if (!sim.townConnected()) fail('дорога до края карты не распознана');

// housetops at home plate
renderer.tool = 'zoneR';
for (let x = 16; x <= 24; x++) for (let y = 16; y <= 22; y++) {
  if (y === 17 || y === 21) continue;
  paint(x, y);
}
renderer.tool = 'zoneC';
for (let x = 16; x <= 19; x++) for (let y = 17; y <= 21; y++) {
  if (y === 19) continue;
  paint(x, y);
}
renderer.tool = 'zoneI';
for (let x = 22; x <= 24; x++) for (let y = 17; y <= 21; y++) {
  if (y === 19) continue;
  paint(x, y);
}
console.log('zones painted, budget', sim.budget.toFixed(0));

// let the town live: advance the simulation directly so the test stays fast,
// then let the real frame loop draw a few frames
for (let i = 0; i < 1440 * 12; i++) sim.update(0.1);
tick(5);
const s = sim.stats();
console.log('after 12 days:', JSON.stringify({ pop: s.pop, jobs: s.jobs, employed: s.employed, houses: s.houses, shops: s.shops, factories: s.factories, sites: s.sites, budget: Math.round(s.budget), mood: s.avgMood.toFixed(2) }));
if (s.pop <= 0) fail('с дорогой до края карты люди должны приезжать');
if (s.houses <= 0) fail('зоны должны застраиваться сами');

// every walker must be standing on a road tile
let offRoad = 0;
for (const c of sim.citizens) {
  if (c.state === 'home' || c.state === 'sleep' || c.state === 'work') continue;
  const tile = { x: Math.floor(c.x), y: Math.floor(c.y) };
  if (!sim.world.isRoad(tile.x, tile.y)) offRoad++;
}
console.log('citizens off-road:', offRoad);
if (offRoad > 0) fail('кто-то ходит не по дороге');

// there must be people and cars on the streets in the middle of the day
sim.absMinute = sim.absMinute - (sim.absMinute % 1440) + 10 * 60;
sim.minute = 10 * 60;
for (let i = 0; i < 40; i++) sim.update(0.1);
const walkers = sim.citizens.filter((c) => c.visible && c.state !== 'home' && c.state !== 'work').length;
console.log('walkers on the streets at 10:00:', walkers);
if (walkers === 0) fail('днём на улицах никого не видно');
if (sim.cars.length === 0) fail('по городу не ездят машины');

// cars keep to the roads too
let badCar = 0;
for (const car of sim.cars) {
  if (!sim.world.isRoad(Math.floor(car.x), Math.floor(car.y))) badCar++;
}
console.log('cars on the road:', sim.cars.length, 'off-road:', badCar);
if (badCar > 0) fail('машина съехала с дороги');

// --------------------------------------------- camera keys work in any layout
renderer.centerOn(20, 20);
const camBefore = { ...renderer.cam };
const press = (ev) => { for (const fn of listeners.keydown || []) fn(ev); };
const release = (ev) => { for (const fn of listeners.keyup || []) fn(ev); };
// Caps Lock on, Latin layout: the event carries "W" and code KeyW
press({ key: 'W', code: 'KeyW', preventDefault() {} });
tick(6);
release({ key: 'W', code: 'KeyW' });
// Russian layout: the same physical key reports "ц"
press({ key: 'ц', code: 'KeyA', preventDefault() {} });
tick(4);
release({ key: 'ц', code: 'KeyA' });
const camAfter = { ...renderer.cam };
const moved = Math.abs(camAfter.x - camBefore.x) > 0.5 || Math.abs(camAfter.y - camBefore.y) > 0;
console.log('WASD moves the camera in any layout:', moved);
if (!moved) fail('камера не двигается на WASD');

// ------------------------------------------------ zoom does not touch the HUD
const hudBefore = renderer.layout();
fire('wheel', W / 2, H / 2, { deltaY: -120 });
tick(2);
const hudAfter = renderer.layout();
console.log('zoom', renderer.zoom, '· HUD size stable:', JSON.stringify(hudBefore.tools[0]) === JSON.stringify(hudAfter.tools[0]), '· css', renderer.cssW + 'x' + renderer.cssH, '· buffer', renderer.bufW + 'x' + renderer.bufH);
if (JSON.stringify(hudBefore.tools[0]) !== JSON.stringify(hudAfter.tools[0])) fail('HUD поехал вместе с зумом');

// speed buttons and hotkeys hit the new HUD layer
const speedButton = renderer.layout().speeds[2];
fire('pointerdown', speedButton.x + speedButton.w / 2, speedButton.y + speedButton.h / 2);
tick(2);
for (const fn of listeners.keydown || []) fn({ key: '3', preventDefault() {} });
tick(2);
const toolCell = renderer.layout().tools[1];
fire('pointerdown', toolCell.x + toolCell.w / 2, toolCell.y + toolCell.h / 2);
tick(2);
console.log('speed ×', sim.speed, '· paused', sim.paused, '· tool', renderer.tool);
if (sim.speed !== 2) fail('кнопка скорости не сработала');

// ------------------------------- cutting the only road to the edge stops arrivals
renderer.tool = 'bulldoze';
paint(20, 0);
tick(2);
const popBefore = sim.citizens.length;
for (let i = 0; i < 1440 * 3; i++) sim.update(0.1);
tick(3);
console.log('entrance bulldozed, connected:', sim.townConnected(), '· pop', popBefore, '->', sim.citizens.length);
if (sim.townConnected()) fail('после сноса въезда дорога всё ещё считается выездом');
if (sim.citizens.length > popBefore) fail('без выезда к краю никто не должен приезжать');

// ------------------------------------------------- city books: toggles and fits
const booksBefore = renderer.summaryOpen;
for (const fn of listeners.keydown || []) fn({ key: 'e', code: 'KeyE', preventDefault() {} });
tick(1);
const booksKey = renderer.summaryOpen;
const booksBtn = renderer.layout().summaryButton;
fire('pointerdown', booksBtn.x + booksBtn.w / 2, booksBtn.y + booksBtn.h / 2);
tick(1);
console.log('economy panel: E toggles', booksBefore, '->', booksKey, '· button toggles', renderer.summaryOpen);
if (booksKey === booksBefore) fail('клавиша E не открывает сводку');
if (renderer.summaryOpen === booksKey) fail('кнопка «экономика» не переключает сводку');
renderer.summaryOpen = true;
tick(2);
const bars = renderer.layout();
const panelFits = bars.summary.y + 40 < bars.toolbar.y;
console.log('summary panel stays clear of the toolbar:', panelFits);
if (!panelFits) fail('сводка налезает на панель инструментов');
renderer.summaryOpen = false;
tick(1);

// ------------------------------------------------------------- money and sledge
renderer.tool = 'bulldoze';
const house = sim.world.buildings.find((b) => b.kind === 'house');
const before = sim.budget;
paint(house.door.x, house.door.y);
tick(2);
console.log('bulldoze ok, budget', before.toFixed(0), '->', sim.budget.toFixed(0));
console.log('money audit (0 = nothing printed):', sim.audit().toFixed(6));
if (Math.abs(sim.audit()) > 0.01) fail('в городе напечатали или сожгли деньги');

tick(30);
console.log('ok: loop still running');
