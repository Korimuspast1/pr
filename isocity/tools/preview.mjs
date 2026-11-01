// Renders the real renderer (city + HUD) to PNG without a browser.
//
//   cd tools && npm install && cd ..
//   node tools/preview.mjs [w] [h] [out.png] [minute] [zoom] [gx] [gy] [days]

import { createCanvas } from '@napi-rs/canvas';
import { writeFileSync } from 'node:fs';
import { Sim } from '../src/sim.js';
import { Renderer } from '../src/render.js';
import { buildDemoTown } from './demo-town.mjs';

const W = Number(process.argv[2] || 1280);
const H = Number(process.argv[3] || 800);
const out = process.argv[4] || 'preview.png';
const minute = Number(process.argv[5] || 620);
const zoom = Number(process.argv[6] || 2);
const gx = Number(process.argv[7] || 20);
const gy = Number(process.argv[8] || 18);
const days = Number(process.argv[9] || 14);
const summary = process.argv[10] !== 'nosummary';

function fakeCanvas(w, h) {
  const c = createCanvas(Math.max(1, w), Math.max(1, h));
  c.clientWidth = w;
  c.clientHeight = h;
  c.getBoundingClientRect = () => ({ left: 0, top: 0, width: w, height: h });
  return c;
}

globalThis.window = { devicePixelRatio: 1, addEventListener() {}, innerWidth: W, innerHeight: H };
globalThis.document = { createElement: (tag) => fakeCanvas(tag === 'canvas' ? 1 : 1, 1) };

const sim = new Sim(7);
buildDemoTown(sim);
for (let i = 0; i < 1440 * days; i++) sim.update(0.1);
// park the clock at the requested minute and let the walkers step out
sim.absMinute = sim.absMinute - (sim.absMinute % 1440) + minute;
sim.minute = minute;
for (let i = 0; i < 30; i++) sim.update(0.1);

const canvas = fakeCanvas(W, H);
const r = new Renderer(canvas, sim);
r.build = 'pixel-5';
r.summaryOpen = summary;
r.zoom = zoom;
r.resize();
r.centerOn(gx, gy);
for (let i = 0; i < 30; i++) r.update(0.05);
r.draw();
writeFileSync(out, canvas.toBuffer('image/png'));

const s = sim.stats();
console.log(
  `wrote ${out} · день ${sim.day} ${Math.floor(sim.minute / 60)}:${String(sim.minute % 60).padStart(2, '0')} · ` +
    `жителей ${s.pop}, домов ${s.houses}, лавок ${s.shops}, заводов ${s.factories}, машин ${sim.cars.length} · drift ${s.drift.toFixed(3)}`,
);
