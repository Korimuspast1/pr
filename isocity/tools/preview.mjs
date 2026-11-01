// Renders the pixel city to PNG without a browser (docs + quick visual checks).
//
//   cd tools && npm install && cd ..
//   node tools/preview.mjs [width] [height] [out.png] [minute] [pxScale] [gx] [gy]

import { createCanvas } from '@napi-rs/canvas';
import { writeFileSync } from 'node:fs';
import { Sim } from '../src/sim.js';
import { Renderer } from '../src/render.js';
import { T } from '../src/world.js';

const W = Number(process.argv[2] || 1200);
const H = Number(process.argv[3] || 800);
const out = process.argv[4] || 'preview.png';
const minute = Number(process.argv[5] || 620);
const pxScale = Number(process.argv[6] || 3);
const gx = Number(process.argv[7] || 20);
const gy = Number(process.argv[8] || 20);

globalThis.window = { devicePixelRatio: 1, addEventListener() {}, innerWidth: W, innerHeight: H };
globalThis.document = {
  createElement: () => {
    const c = createCanvas(1, 1);
    c.clientWidth = W;
    c.clientHeight = H;
    return c;
  },
};

const sim = new Sim(7);
const w = sim.world;
// a player-like layout: roads first, then zones around them
for (let x = 12; x <= 28; x++) { sim.buildRoad(x, 16); sim.buildRoad(x, 24); }
for (let y = 16; y <= 24; y++) { sim.buildRoad(12, y); sim.buildRoad(28, y); }
for (let x = 16; x <= 24; x++) sim.buildRoad(x, 20);
for (let y = 16; y <= 24; y++) sim.buildRoad(20, y);
for (let x = 13; x <= 27; x++) {
  for (let y = 17; y <= 23; y++) {
    if (w.get(x, y) !== T.GRASS) continue;
    sim.zone(x, y, (x + y) % 4 === 0 ? 'c' : x > 22 && y > 21 ? 'i' : 'r');
  }
}
for (let i = 0; i < 1440 * 6; i++) sim.update(0.1);
sim.absMinute = sim.absMinute - (sim.absMinute % 1440) + minute;
sim.minute = minute;
for (let i = 0; i < 6; i++) sim.update(0.1);

const canvas = createCanvas(W, H);
canvas.clientWidth = W;
canvas.clientHeight = H;
const r = new Renderer(canvas, sim);
r.pxScale = pxScale;
r.bufW = Math.floor(W / pxScale);
r.bufH = Math.floor(H / pxScale);
r.buf.width = r.bufW;
r.buf.height = r.bufH;
canvas.width = r.bufW * pxScale;
canvas.height = r.bufH * pxScale;
r.bctx.imageSmoothingEnabled = false;
r.ctx.imageSmoothingEnabled = false;
r.centerOn(gx, gy);
for (let i = 0; i < 30; i++) r.update(0.05);
r.draw();
writeFileSync(out, canvas.toBuffer('image/png'));
const s = sim.stats();
console.log(`wrote ${out} · день ${sim.day} ${Math.floor(sim.minute / 60)}:${String(sim.minute % 60).padStart(2, '0')} · жителей ${s.pop}, домов ${s.houses}, лавок ${s.shops}, заводов ${s.factories}, настройка ${s.sites} · drift ${s.drift.toFixed(3)}`);
