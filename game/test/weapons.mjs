// Checks every weapon, reload flow, death + respawn.
import { createCanvas } from '@napi-rs/canvas';
const noop = () => {};
const listeners = {};
const canvas = createCanvas(512, 288);
canvas.style = {}; canvas.addEventListener = noop;
canvas.getBoundingClientRect = () => ({ left: 0, top: 0, width: 512, height: 288 });
globalThis.window = { innerWidth: 1440, innerHeight: 900, addEventListener: (t, f) => { (listeners[t] ||= []).push(f); }, removeEventListener: noop };
globalThis.document = {
  getElementById: (id) => (id === 'game' ? canvas : id === 'wrap' ? { style: {} } : null),
  createElement: (t) => { if (t !== 'canvas') return { style: {}, remove: noop }; const c = createCanvas(512, 288); c.addEventListener = noop; return c; },
  addEventListener: (t, f) => { (listeners[t] ||= []).push(f); },
  body: { appendChild: noop },
};
globalThis.performance = { now: () => Date.now() };
globalThis.requestAnimationFrame = noop;
await import(new URL('../src/main.js', import.meta.url).href);
for (const f of listeners['DOMContentLoaded'] || []) f();
const game = window.__game;
const { WEAPONS } = await import(new URL('../src/weapons.js', import.meta.url).href);

game.started = true;
for (const e of game.entities) if (!e.isPlayer) { e.ai.frozen = true; e.ai.target = null; e.ai.retarget = 1e9; }
const dummy = game.spawnNPC(400);
dummy.ai.frozen = true;

const tick = (n) => { for (let i = 0; i < n; i++) { game.player.health = 100; game.update(1 / 60); } game.render(); };

for (const w of WEAPONS) {
  game.player.setWeapon(w.id);
  game.player.mags[w.id] = w.magSize || 0;
  game.input.mouse.down = true;
  game.mouse.x = dummy.pts.chest.x; game.mouse.y = dummy.pts.chest.y;
  tick(90);
  game.input.mouse.down = false;
  tick(120);
  const mag = game.player.mags[w.id];
  console.log(`${w.id.padEnd(9)} ammo=${mag}/${w.magSize ?? '-'} bullets=${game.combat.bullets.length} reloading=${game.player.reloading.toFixed(2)} ok`);
}

// death → respawn
game.player.applyHit(game.player.limbs.find((l) => l.name === 'head'), {
  type: 'bullet', dmg: 500, struct: 1, cut: 1, knock: 400,
  at: { x: game.player.pts.head.x, y: game.player.pts.head.y }, dir: { x: 1, y: 0 },
});
tick(10);
console.log('after headshot alive:', game.player.alive, '| cause:', game.player.deathCause);
game.input.just.add('KeyR');
game.update(1 / 60);
console.log('after respawn alive:', game.player.alive, 'hp:', game.player.health.toFixed(0), 'blood:', game.player.blood.toFixed(0), 'weapon:', game.player.weapon.id);
// switch all hotbar slots
for (let i = 0; i < 8; i++) { game.selectBar(i); }
console.log('hotbar ok, current:', game.player.weapon.id, '| barIndex', game.barIndex);
game.render();
console.log('OK');
