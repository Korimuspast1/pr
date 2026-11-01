// Runs the REAL app (main.js) in a fake browser and saves what the player sees.
// This is the closest thing to a browser screenshot without a browser.
//
//   node tools/app-shot.mjs [out.png] [width] [height] [frames]

import { createCanvas } from '@napi-rs/canvas';
import { writeFileSync } from 'node:fs';

const out = process.argv[2] || 'app.png';
const W = Number(process.argv[3] || 1280);
const H = Number(process.argv[4] || 800);
const frames = Number(process.argv[5] || 400);

const listeners = {};
let mainCanvas = null;

function stubCanvas(w = 1, h = 1) {
  const c = createCanvas(w, h);
  c.clientWidth = w;
  c.clientHeight = h;
  c.getBoundingClientRect = () => ({ left: 0, top: 0, width: c.clientWidth, height: c.clientHeight });
  c.setPointerCapture = () => {};
  c.releasePointerCapture = () => {};
  c.addEventListener = (k, fn) => { (listeners[k] ||= []).push(fn); };
  const ctx = c.getContext('2d');
  if (!ctx.roundRect) ctx.roundRect = function (x, y, w2, h2) { this.rect(x, y, w2, h2); };
  return c;
}

mainCanvas = stubCanvas(W, H);

globalThis.window = {
  devicePixelRatio: 1,
  innerWidth: W,
  innerHeight: H,
  addEventListener: (k, fn) => { (listeners[k] ||= []).push(fn); },
};
globalThis.document = {
  getElementById: () => mainCanvas,
  createElement: () => stubCanvas(1, 1),
};
let pending = null;
globalThis.requestAnimationFrame = (fn) => { pending = fn; };
globalThis.performance = globalThis.performance || { now: () => Date.now() };

const { BUILD } = await import('../src/main.js');
const { sim, renderer } = window.city;

// let the town grow: advance the sim directly, then render real frames
for (let i = 0; i < 1440 * 6; i++) sim.update(0.1);
sim.paused = false;

let t = 0;
for (let i = 0; i < frames; i++) {
  const fn = pending;
  pending = null;
  t += 16.7;
  fn(t);
}

writeFileSync(out, mainCanvas.toBuffer('image/png'));
const s = sim.stats();
console.log(
  `сборка ${BUILD} · ${out} · день ${sim.day} ${String(Math.floor(sim.minute / 60)).padStart(2, '0')}:${String(sim.minute % 60).padStart(2, '0')} · ` +
    `жителей ${s.pop}, домов ${s.houses}, лавок ${s.shops}, заводов ${s.factories} · ` +
    `вне дорог: ${sim.citizens.filter((c) => !['home', 'sleep', 'work'].includes(c.state) && !sim.world.isRoad(Math.floor(c.x), Math.floor(c.y))).length}`,
);
