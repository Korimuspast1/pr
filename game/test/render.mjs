// Renders real frames of the sandbox to PNG for visual review.
import { createCanvas } from '@napi-rs/canvas';
import { mkdir, writeFile } from 'node:fs/promises';

const noop = () => {};
const listeners = {};
const canvas = createCanvas(1280, 720);
canvas.style = {};
canvas.addEventListener = noop;
canvas.getBoundingClientRect = () => ({ left: 0, top: 0, width: 640, height: 360, right: 640, bottom: 360 });
const wrap = { style: {}, appendChild: noop };

globalThis.window = {
  innerWidth: 1440, innerHeight: 900,
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  removeEventListener: noop,
};
globalThis.document = {
  getElementById: (id) => (id === 'game' ? canvas : id === 'wrap' ? wrap : null),
  createElement: (t) => {
    if (t !== 'canvas') return { style: {}, remove: noop };
    const c = createCanvas(1280, 720);
    c.addEventListener = noop;
    return c;
  },
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  body: { appendChild: noop },
};
globalThis.performance = { now: () => Date.now() };
globalThis.requestAnimationFrame = noop;

await import(new URL('../src/main.js', import.meta.url).href);
const game = globalThis.window.__game;

await mkdir(new URL('../shots2/', import.meta.url), { recursive: true });
const out = (name) => writeFile(new URL('../shots2/' + name, import.meta.url), canvas.toBuffer('image/png'));

const step = (sec, fn) => {
  const n = Math.round(sec * 60);
  for (let i = 0; i < n; i++) {
    if (fn) fn(i);
    game.update(1 / 60, 1 / 60, false);
  }
  game.render();
};

// 1) title
game.render();
await out('01-title.png');

// 2) calm sandbox
game.started = true;
for (const h of game.humans) { h.ai.enabled = false; h.pos.x = [150, 330, 500][game.humans.indexOf(h)] || h.pos.x; }
game.cursor.x = 330; game.cursor.y = 250; game.cursor.inside = true;
game.setTool('inspect');
step(0.8);
game.pinned = game.humans[1];
step(0.2);
await out('02-inspector.png');

// 3) cutting a limb with a katana
const victim = game.spawnHuman(300, { weapon: 'knife', ai: false });
game.setTool('cut');
game.weaponId = 'katana';
game.pinned = null;
game.cursor.down = true;
for (let i = 0; i < 130; i++) {
  const t = i / 60;
  game.cursor.x = victim.pts.shN.x - 18 + Math.sin(t * 9) * 12;
  game.cursor.y = victim.pts.shN.y - 6 + Math.cos(t * 9) * 9;
  game.update(1 / 60, 1 / 60, false);
}
game.render();
await out('03-cutting.png');
game.cursor.down = false;

// 4) shooting from the edge of the frame
const target = game.humans[2];
game.setTool('shoot');
game.weaponId = 'pistol';
game.cursor.x = target.pts.chest.x;
game.cursor.y = target.pts.chest.y;
game.cursor.down = true;
step(0.5);
game.render();
await out('04-shooting.png');
game.cursor.down = false;
step(1.2);
await out('05-after-shots.png');

// 5) shotg un blast close up
game.weaponId = 'shotgun';
const victim2 = game.spawnHuman(360, { weapon: 'fists', ai: false });
game.cursor.x = victim2.pts.chest.x;
game.cursor.y = victim2.pts.chest.y;
game.cursor.down = true;
step(0.2);
game.cursor.down = false;
step(0.5);
await out('06-shotgun.png');

// 6) ragdoll pile from a brawl
game.aiAggro = true;
for (const h of game.humans) { h.ai.enabled = true; h.ai.retarget = 0; }
game.setTool('inspect');
step(14);
game.cursor.x = 320; game.cursor.y = 260;
await out('07-brawl.png');

// 7) help overlay
game.showHelp = true;
step(0.05);
await out('08-help.png');
game.showHelp = false;

// 8) slow motion + blood
game.timeScale = 0.12;
step(0.6);
await out('09-slowmo.png');

console.log('rendered');
console.log('severed:', game.humans.map((h) => h.limbs.filter((l) => l.severed).length).join(','));
console.log('alive:', game.humans.filter((h) => h.alive).length, 'dead:', game.humans.filter((h) => !h.alive).length);
console.log('debris limbs:', game.debris.filter((d) => d.type === 'limb').length);
process.exit(0);
