// Headless sandbox test: physics stability, bleeding, death, tools.
import { createCanvas } from '@napi-rs/canvas';

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
game.started = true;
game.started = true;

const errors = [];
const step = (sec, fn) => {
  const n = Math.round(sec * 60);
  for (let i = 0; i < n; i++) {
    if (fn) fn(i);
    try { game.update(1 / 60, 1 / 60, false); game.render(); }
    catch (e) { errors.push(e); console.error('FRAME ERROR at', game.time.toFixed(2), e); throw e; }
  }
};
const finite = (v) => Number.isFinite(v);

console.log('humans at start:', game.humans.length);

// 1) ragdoll stability: drop a corpse hard and let it settle
const a = game.humans[0];
a.die();
a.goRagdoll({ x: 180, y: -120 });
step(4);
let maxV = 0;
for (const k in a.pts) maxV = Math.max(maxV, Math.hypot(a.pts[k].vx, a.pts[k].vy));
console.log('ragdoll settled:', a.sleeping, 'maxV:', maxV.toFixed(2), 'pos:', a.pts.chest.x.toFixed(1), a.pts.chest.y.toFixed(1));
if (!a.sleeping) errors.push(new Error('ragdoll did not settle'));

// 2) bullet lethality: how many pistol torso shots to kill
const countShots = (weaponId, target, aimFn) => {
  target.reset(target.pos.x, target.weapon.id);
  let shots = 0;
  for (let i = 0; i < 40 && target.alive; i++) {
    const at = aimFn(target);
    target.applyHit(target.limbs.find((l) => l.name === at), {
      type: 'bullet', dmg: 24, struct: 24 * 0.055, cut: 0.55, bleedMul: 1.2,
      knock: 24 * 2.2, at: { x: target.pts.chest.x, y: target.pts.chest.y },
      dir: { x: 1, y: 0 }, bullet: true,
    });
    shots++;
    step(0.4);
  }
  return { shots, dead: !target.alive, cause: target.deathCause, blood: target.blood };
};
const t1 = game.humans[1];
console.log('pistol body shots:', countShots('pistol', t1, () => 'torso'));

// 3) headshot must be lethal fast
const t2 = game.humans[2];
t2.reset(t2.pos.x, t2.weapon.id);
let hs = 0;
while (t2.alive && hs < 6) {
  t2.applyHit(t2.limbs.find((l) => l.name === 'head'), {
    type: 'bullet', dmg: 24, struct: 24 * 0.055, cut: 0.55, bleedMul: 1.2, knock: 20,
    at: { x: t2.pts.head.x, y: t2.pts.head.y }, dir: { x: 1, y: 0 }, bullet: true,
  });
  hs++;
  step(0.3);
}
console.log('headshots to kill:', hs, 'cause:', t2.deathCause);

// 4) a leg wound must not kill instantly
const t3 = game.spawnHuman(400);
t3.applyHit(t3.limbs.find((l) => l.name === 'legN_u'), {
  type: 'bullet', dmg: 24, struct: 24 * 0.055, cut: 0.55, bleedMul: 1.2, knock: 20,
  at: { x: t3.pts.knN.x, y: t3.pts.knN.y }, dir: { x: 1, y: 0 }, bullet: true,
});
const hpAfter = t3.health;
step(6);
console.log('leg shot: hp', hpAfter.toFixed(1), '->', t3.health.toFixed(1), 'alive:', t3.alive, 'bleed', t3.bleedTotal.toFixed(1));
if (!t3.alive) errors.push(new Error('leg shot killed instantly'));

// 5) bleed-out timing for a serious arterial wound
const t4 = game.spawnHuman(500);
const torso = t4.limbs.find((l) => l.name === 'torso');
torso.struct = 0.9;
torso.wounds.push({ t: 0.5, side: 1, r: 3, bleed: 180, arterial: true, kind: 'bullet', age: 0 });
torso.arterial = true;
let tt = 0;
while (t4.alive && tt < 120) { step(0.5); tt += 0.5; }
console.log('arterial bleed-out after', tt.toFixed(1), 's, cause:', t4.deathCause, 'blood left', t4.blood.toFixed(0));

// 6) corpse must stop bleeding after death
const before = t4.bleedTotal;
step(6);
console.log('corpse bleed rate:', before.toFixed(1), '->', t4.bleedTotal.toFixed(1));

// 7) tools: cut a limb via the real cursor handler
const victim = game.spawnHuman(330);
victim.ai.enabled = false;
game.setTool('cut');
game.weaponId = 'katana';
game.cursor.x = victim.pts.elN.x - 14;
game.cursor.y = victim.pts.elN.y;
game.cursor.down = true;
for (let i = 0; i < 60; i++) {
  game.cursor.x = victim.pts.shN.x - 10 + Math.sin(i / 3) * 16;
  game.cursor.y = victim.pts.shN.y + Math.cos(i / 3) * 10;
  game.update(1 / 60, 1 / 60, false);
}
game.cursor.down = false;
console.log('after cutting:', victim.deathCause || 'alive', 'severed:', victim.limbs.filter((l) => l.severed).map((l) => l.label).join(', ') || 'none');

// 8) spawn / heal / delete
const n0 = game.humans.length;
game.setTool('spawn');
game.cursor.x = 420; game.cursor.y = 300; game.cursor.justDown = true;
game.update(1 / 60, 1 / 60, false);
game.cursor.justDown = false;
const spawned = game.humans.length;
game.setTool('heal');
game.cursor.x = game.humans[game.humans.length - 1].pts.chest.x;
game.cursor.y = game.humans[game.humans.length - 1].pts.chest.y;
game.cursor.justDown = true;
game.update(1 / 60, 1 / 60, false);
game.cursor.justDown = false;
console.log('spawn worked:', spawned === n0 + 1, '| heal ok');

// 9) long run with aggressive AI
game.aiAggro = true;
for (const h of game.humans) { h.ai.enabled = true; h.ai.retarget = 0; }
step(25);
console.log('after 25s aggro — alive:', game.humans.filter((h) => h.alive).length, 'dead:', game.humans.filter((h) => !h.alive).length);
console.log('deaths:', game.humans.filter((h) => !h.alive).map((h) => h.deathCause).join(' | '));

// 10) NaN scan
let nan = null;
const scan = (obj, path, depth, seen) => {
  if (depth > 5 || !obj || typeof obj !== 'object' || seen.has(obj)) return;
  seen.add(obj);
  for (const k of Object.keys(obj)) {
    const v = obj[k];
    if (typeof v === 'number' && !Number.isFinite(v)) nan = path + '.' + k;
    else if (v && typeof v === 'object') scan(v, path + '.' + k, depth + 1, seen);
  }
};
for (const h of game.humans) scan(h, 'human' + h.id, 0, new Set());
if (nan) errors.push(new Error('NaN at ' + nan));
console.log(nan ? 'NaN at ' + nan : 'no NaN');

console.log(errors.length ? `FAILED (${errors.length})` : 'OK');
process.exit(errors.length ? 1 : 0);
