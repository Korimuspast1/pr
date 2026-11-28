/* Renders every model and the map to PNG so they can be checked without WebGL.
   Output: game3d/shots/*.png */
import * as THREE from '../vendor/three.module.js';
import { render, sheet, savePng } from './raster.mjs';
import { WEAPONS, makeWeaponMesh } from '../src/weapons3d.js';
import { Human } from '../src/human.js';
import { World } from '../src/world.js';

const OUT = new URL('../shots/', import.meta.url).pathname;
const shots = [];

const stubAudio = new Proxy({}, { get: () => () => {} });
const stubApp = (scene) => ({
  scene,
  humans: [],
  debris: [],
  audio: stubAudio,
  addLog() {},
  onDeath() {},
  world: {
    spray() {}, mist() {}, dust() {}, spark() {}, chunk() {}, decal() {},
    tracer() {}, muzzleFlash() {}, clearBlood() {}, update() {},
  },
});

const fit = (obj, pad = 1.22) => {
  const box = new THREE.Box3().setFromObject(obj);
  const size = new THREE.Vector3();
  const center = new THREE.Vector3();
  box.getSize(size);
  box.getCenter(center);
  return { center, size, scale: Math.max(size.x, size.y, size.z) * pad };
};

/* ---------------- weapons ---------------- */
console.log('weapons…');
const weaponCells = [];
for (const def of WEAPONS) {
  if (def.id === 'fists') continue;
  const scene = new THREE.Scene();
  const g = makeWeaponMesh(def);
  scene.add(g);
  const { center, scale } = fit(g);
  const img = render(scene, {
    width: 460, height: 260, dir: [0.97, 0.32, -0.16], up: [0, 1, 0],
    target: [center.x, center.y, center.z], scale: Math.max(scale * 1.1, 0.3),
    bg: [0.07, 0.08, 0.1], light: [-0.34, 0.9, -0.27],
    shadowRange: 2, shadowSize: 512, exposure: 1.05,
  });
  weaponCells.push(img);
  shots.push(savePng(`${OUT}model-weapon-${def.id}.png`, img));
}
const wSheet = sheet(weaponCells, { cols: 4, cellW: 320, cellH: 190 });
shots.push(savePng(`${OUT}models-weapons.png`, wSheet));

/* ---------------- human ---------------- */
console.log('human…');
const scene = new THREE.Scene();
const app = stubApp(scene);
const h = new Human(app, { x: 0, z: 0, weapon: 'rifle', ai: false, name: 'Модель' });
app.humans.push(h);
h.yaw = 0;
for (let i = 0; i < 8; i++) h.update(1 / 60, app);
const hb = fit(h.group, 1.18);
const humanCells = [];
for (const [name, dir] of [
  ['front', [0, 0.12, -1]],
  ['side', [1, 0.12, 0]],
  ['back', [0, 0.12, 1]],
  ['three-quarter', [0.7, 0.35, -0.7]],
]) {
  const img = render(scene, {
    width: 360, height: 560, dir, up: [0, 1, 0],
    target: [hb.center.x, 0.95, hb.center.z], scale: 1.95,
    bg: [0.07, 0.08, 0.1],
  });
  humanCells.push(img);
  shots.push(savePng(`${OUT}model-human-${name}.png`, img));
}
const hSheet = sheet(humanCells, { cols: 4, cellW: 270, cellH: 420 });
shots.push(savePng(`${OUT}models-human.png`, hSheet));

// close-up of head + hands
const headImg = render(scene, {
  width: 420, height: 420, dir: [0.22, 0.03, -1], up: [0, 1, 0],
  target: [h.pts.head.x, h.pts.head.y - 0.01, h.pts.head.z], scale: 0.6,
  bg: [0.07, 0.08, 0.1],
});
shots.push(savePng(`${OUT}model-human-face.png`, headImg));

const upperImg = render(scene, {
  width: 520, height: 520, dir: [0.35, 0.1, -1], up: [0, 1, 0],
  target: [h.pts.chest.x, 1.32, h.pts.chest.z], scale: 1.15,
  bg: [0.07, 0.08, 0.1],
});
shots.push(savePng(`${OUT}model-human-upper.png`, upperImg));

/* ---------------- ragdoll ---------------- */
console.log('ragdoll…');
const app2 = stubApp(scene);
const scene2 = new THREE.Scene();
const app3 = stubApp(scene2);
const r = new Human(app3, { x: 0, z: 0, weapon: 'knife', ai: false, name: 'Рэгдолл' });
app3.humans.push(r);
r.ragdollize({ x: 0.8, y: 0.4, z: 0.2 }, { ...r.pts.chest });
r.health = 0;
r.deathCause = 'тест';
r.die();
for (let i = 0; i < 240; i++) r.update(1 / 60, app3);
const rb = fit(r.group, 1.15);
const ragImg = render(scene2, {
  width: 560, height: 420, dir: [0.4, 0.55, 1], up: [0, 1, 0],
  target: [rb.center.x, Math.max(0.3, rb.center.y), rb.center.z],
  scale: Math.max(1.5, rb.scale * 1.25),
  bg: [0.07, 0.08, 0.1],
});
shots.push(savePng(`${OUT}model-ragdoll.png`, ragImg));
void app2;

/* ---------------- map ---------------- */
console.log('map…');
const mapScene = new THREE.Scene();
const world = new World(mapScene, {}, stubAudio);
for (let i = 0; i < 4; i++) world.update(1 / 30);
const SUN = [-0.34, 0.90, -0.27];
const NIGHT_SKY = [[0.055, 0.070, 0.105], [0.150, 0.160, 0.195]];
const NIGHT_FOG = { color: [0.055, 0.068, 0.090], density: 0.018 };

const top = render(mapScene, {
  width: 900, height: 900, dir: [0, 1, 0.001], up: [0, 0, -1],
  target: [0, 0, 0], scale: 62, bg: [0.04, 0.05, 0.07],
  light: SUN, shadowRange: 38, exposure: 0.85,
});
shots.push(savePng(`${OUT}map-top.png`, top));
const persp = render(mapScene, {
  width: 1000, height: 560, dir: [0.75, 0.45, 0.75], up: [0, 1, 0],
  target: [0, 2, 0], scale: 46, bg: [0.05, 0.06, 0.08],
  light: SUN, shadowRange: 44, sky: NIGHT_SKY, fog: NIGHT_FOG, exposure: 0.9,
});
shots.push(savePng(`${OUT}map-perspective.png`, persp));

/* ---------------- people in the yard ---------------- */
const yardApp = stubApp(mapScene);
yardApp.world = {
  spray() {}, mist() {}, dust() {}, spark() {}, chunk() {}, decal() {},
  tracer() {}, muzzleFlash() {}, clearBlood() {}, update() {},
  blocked: (x, z) => world.blocked(x, z, 0.4),
  resolvePoint: (p, r) => world.resolvePoint(p, r),
};
const actors = [];
for (const [x, z, w, yaw] of [
  [-1.5, 3.4, 'rifle', 0.4], [2.6, 6.6, 'shotgun', -1.1], [-5.4, -2.2, 'machete', 2.4],
]) {
  const a = new Human(yardApp, { x, z, weapon: w, ai: false, name: 'Житель' });
  a.yaw = yaw;
  a.aimTarget = { x: x + Math.sin(yaw) * 4, y: 1.35, z: z + Math.cos(yaw) * 4 };
  yardApp.humans.push(a);
  actors.push(a);
}
for (let i = 0; i < 20; i++) for (const a of actors) a.update(1 / 60, yardApp);

/* ---------------- eye-level views ---------------- */
const views = [
  ['yard-west', [10, 1.7, 9.5], [-14, 1.6, 2], 66],
  ['yard-north', [2, 1.7, 9], [4, 1.4, -17], 66],
  ['yard-containers', [1, 1.7, 17], [-11, 1.5, 5], 62],
  ['yard-south', [-8, 1.7, -6], [1.5, 1.5, 24], 64],
  ['street', [-16, 1.7, -24.5], [12, 2.4, -25], 62],
  ['gate', [0, 1.7, -8], [-1, 1.4, -20], 64],
  ['yard-wide', [18, 2.6, 24], [-8, 1.2, -2], 70],
  ['yard-people', [4.5, 1.75, 9.5], [-2.5, 1.2, 2.0], 60],
];
for (const [name, eye, target, fov] of views) {
  const img = render(mapScene, {
    width: 1000, height: 560, eye, target, fov,
    light: SUN, shadowRange: 40, shadowFocus: [eye[0], 0, eye[2]],
    sky: NIGHT_SKY, fog: NIGHT_FOG, shadowSize: 1200, exposure: 0.95,
  });
  shots.push(savePng(`${OUT}map-eyelevel-${name}.png`, img));
}

console.log(`\n${shots.length} images → ${OUT}`);
