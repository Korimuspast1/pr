// Boots the real app loop against a fake browser + node canvas, so module
// wiring, the render loop and the overlay are exercised without a browser.

import { createCanvas } from '@napi-rs/canvas';

const W = 1200;
const H = 800;
const canvas = createCanvas(W, H);
canvas.clientWidth = W;
canvas.clientHeight = H;
const ctx = canvas.getContext('2d');
if (!ctx.roundRect) ctx.roundRect = function (x, y, w, h) { this.rect(x, y, w, h); };

const listeners = {};
globalThis.window = {
  devicePixelRatio: 1,
  addEventListener: (k, fn) => { (listeners[k] ||= []).push(fn); },
};
globalThis.document = { getElementById: () => canvas };
canvas.addEventListener = (k, fn) => { (listeners[k] ||= []).push(fn); };
canvas.setPointerCapture = () => {};
canvas.releasePointerCapture = () => {};

let frames = 0;
let pending = null;
globalThis.requestAnimationFrame = (fn) => { pending = fn; };
globalThis.performance = globalThis.performance || { now: () => Date.now() };

await import('../src/main.js');

// run 300 frames of ~16 ms
let t = 0;
for (let i = 0; i < 300 && pending; i++) {
  const fn = pending;
  pending = null;
  t += 16.7;
  fn(t);
  frames++;
}
console.log('frames rendered:', frames);
console.log('clock:', window.city.sim.minute, 'day', window.city.sim.day);
console.log('money drift:', window.city.sim.auditMoney().toFixed(9));
console.log('zoom', window.city.renderer.zoom.toFixed(2));

// exercise the optional overlay and the camera controls
window.city.renderer.hint = 0;
const { drawOverlay } = await import('../src/overlay.js');
drawOverlay(window.city.renderer.ctx, window.city.renderer, window.city.sim, W, H);
for (const fn of listeners.keydown || []) fn({ key: 'h', preventDefault() {} });
for (const fn of listeners.keydown || []) fn({ key: '2', preventDefault() {} });
for (const fn of listeners.wheel || []) fn({ clientX: 500, clientY: 400, deltaY: -120, preventDefault() {} });
console.log('zoom after wheel', window.city.renderer.zoom.toFixed(2), 'speed', window.city.sim.timeScale);
if (!pending) throw new Error('render loop stopped');
console.log('ok');
