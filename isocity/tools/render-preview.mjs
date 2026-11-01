// Renders the city to PNG files without a browser (used for docs & screenshots).
//
//   cd tools && npm install && cd ..
//   node tools/render-preview.mjs [outDir]
//
// Outputs day.png, evening.png, night.png and economy.png into the given folder.

import { createCanvas } from '@napi-rs/canvas';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { Sim } from '../src/economy.js';
import { Renderer } from '../src/render.js';
import { drawOverlay } from '../src/overlay.js';

globalThis.window = { devicePixelRatio: 1, addEventListener() {} };

const outDir = process.argv[2] || 'docs';
mkdirSync(outDir, { recursive: true });

const W = 1400;
const H = 900;

function render({ minute, overlay = false, file }) {
  const canvas = createCanvas(W, H);
  canvas.clientWidth = W;
  canvas.clientHeight = H;

  const sim = new Sim(11);
  sim.timeScale = 10;
  for (let i = 0; i < 72 * 3; i++) sim.update(0.1); // three days of city life
  sim.timeScale = 1;
  // jump to the requested time of day without skipping the schedule
  sim.absMinute -= sim.absMinute % 1440;
  sim.absMinute += minute;
  sim.minute = minute;
  for (let i = 0; i < 6; i++) sim.update(0.1); // ~10 minutes, so the clock stays put

  const r = new Renderer(canvas, sim);
  r.fit();
  r.hint = 0;
  for (let i = 0; i < 60; i++) r.update(0.05);
  r.draw();
  if (overlay) drawOverlay(r.ctx, r, sim, W, H);
  writeFileSync(join(outDir, file), canvas.toBuffer('image/png'));
  console.log('wrote', join(outDir, file));
}

render({ minute: 11 * 60, file: 'day.png' });
render({ minute: 11 * 60, overlay: true, file: 'economy.png' });
render({ minute: 19 * 60 + 20, file: 'evening.png' });
render({ minute: 21 * 60 + 30, file: 'night.png' });
