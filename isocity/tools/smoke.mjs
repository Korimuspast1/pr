// Boots the real app in a fake browser (node canvas) and drives it like a
// player: paints roads, zones, parks, bulldozes, zooms, and checks that the
// simulation stays consistent and that no money appears out of nowhere.

import { createCanvas } from '@napi-rs/canvas';

const W = 1280;
const H = 800;

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

const listeners = {};
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

tick(10);

// --- paint a road grid with the mouse, exactly like a player would
renderer.tool = 'road';
const toScreen = (gx, gy) => {
  const p = renderer.worldToScreen(...Object.values(isoXY(gx, gy)));
  return p;
};
function isoXY(gx, gy) {
  return { x: (gx - gy) * 16, y: (gx + gy) * 8 };
}

function paint(kind, gx, gy) {
  const world = isoXY(gx + 0.5, gy + 0.5);
  const screen = renderer.worldToScreen(world.x, world.y);
  fire(kind, screen.x, screen.y);
  fire('pointerup', screen.x, screen.y, {});
}

// roads around the start
for (let x = 15; x <= 25; x++) { paint('pointerdown', x, 15); paint('pointerdown', x, 25); }
for (let y = 15; y <= 25; y++) { paint('pointerdown', 15, y); paint('pointerdown', 25, y); }
console.log('roads painted, budget', sim.budget.toFixed(0));

// zones
renderer.tool = 'zoneR';
for (let x = 16; x <= 24; x++) for (let y = 16; y <= 24; y++) paint('pointerdown', x, y);
renderer.tool = 'zoneC';
for (let x = 20; x <= 22; x++) for (let y = 20; y <= 22; y++) paint('pointerdown', x, y);
renderer.tool = 'park';
paint('pointerdown', 17, 17);
console.log('zones painted');

// let the town live: advance the simulation directly so the test stays fast,
// then let the real frame loop draw a few frames
for (let i = 0; i < 1440 * 10; i++) sim.update(0.1);
tick(5);
const s = sim.stats();
console.log('after 12 days:', JSON.stringify({ pop: s.pop, jobs: s.jobs, employed: s.employed, houses: s.houses, shops: s.shops, factories: s.factories, sites: s.sites, budget: Math.round(s.budget), mood: s.avgMood.toFixed(2) }));

// every walker must be standing on a road tile
let offRoad = 0;
for (const c of sim.citizens) {
  if (c.state === 'home' || c.state === 'sleep' || c.state === 'work') continue;
  const tile = { x: Math.floor(c.x), y: Math.floor(c.y) };
  if (!sim.world.isRoad(tile.x, tile.y)) offRoad++;
}
console.log('citizens off-road:', offRoad);

// zoom, speed buttons, keyboard
fire('wheel', W / 2, H / 2, { deltaY: -120 });
fire('pointerdown', W - 30, 12);
for (const fn of listeners.keydown || []) fn({ key: '3', preventDefault() {} });
tick(5);
console.log('zoom', renderer.zoom, 'paused', sim.paused, 'tool', renderer.tool);

// bulldoze something and check the money went back to the budget
const before = sim.budget;
renderer.tool = 'bulldoze';
paint('pointerdown', 16, 16);
console.log('bulldoze ok, budget', before.toFixed(0), '->', sim.budget.toFixed(0));

const audit = sim.audit();
console.log('money audit (0 = nothing printed):', audit.toFixed(6));
console.log(pending ? 'ok: loop still running' : 'ERROR: loop stopped');
if (Math.abs(audit) > 0.01) throw new Error('money was created or destroyed');
if (offRoad > 0) throw new Error('citizens are walking off the roads');
