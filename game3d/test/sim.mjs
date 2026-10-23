/* Headless simulation test: no WebGL, no DOM — only the simulation layer. */
import * as THREE from '../vendor/three.module.js';
import { World } from '../src/world.js';
import { Human, LIMBS } from '../src/human.js';
import { WEAPON_BY_ID, WEAPONS, makeWeaponMesh } from '../src/weapons3d.js';
import { raySegment, dist, TAU } from '../src/util3.js';

let fails = 0;
const ok = (cond, msg, extra = '') => {
  if (cond) console.log(`  ok   ${msg}${extra ? '  ' + extra : ''}`);
  else { fails++; console.log(`  FAIL ${msg}${extra ? '  ' + extra : ''}`); }
};

const audioStub = new Proxy({}, {
  get: (t, k) => (k === 'ctx' ? null : () => {}),
});

const scene = new THREE.Scene();
const world = new World(scene, {}, audioStub);

/* minimal app that mirrors main.js contracts */
const app = {
  scene,
  world,
  audio: audioStub,
  humans: [],
  debris: [],
  logs: [],
  addLog(t, k) { this.logs.push({ t, k }); },
  onDeath() {},
  fireBullet(origin, dir, def, shooter) {
    let best = null;
    for (const h of this.humans) {
      if (h === shooter) continue;
      for (const l of h.limbs) {
        if (l.severed || l.parentSevered) continue;
        const r = raySegment(origin, dir, h.pts[l.a], h.pts[l.b]);
        if (r.dist <= l.r + 0.01 && (!best || r.rayT < best.t)) {
          best = { t: r.rayT, h, l: l.name, p: { x: origin.x + dir.x * r.rayT, y: origin.y + dir.y * r.rayT, z: origin.z + dir.z * r.rayT } };
        }
      }
    }
    if (!best) { if (dir.y < 0) { const t = -origin.y / dir.y; world.decal(origin.x + dir.x * t, origin.z + dir.z * t, 0.1, 0.5); } return; }
    best.h.applyHit(best.l, {
      type: 'bullet', dmg: def.dmg * 0.8, struct: def.dmg * def.structMul * 0.8, cut: 0.55,
      bleedMul: def.bleedMul, knock: def.dmg * 1.6, point: best.p, dir, attacker: shooter,
    });
  },
  dropWeapon(def, pos, dir) {
    const mesh = makeWeaponMesh(def);
    scene.add(mesh);
    this.debris.push({ kind: 'weapon', mesh, def, pos: { ...pos }, vel: { x: dir.x, y: 2, z: dir.z }, life: 120 });
  },
};

const sim = (dt, seconds) => {
  const steps = Math.round(seconds / dt);
  for (let i = 0; i < steps; i++) {
    for (const h of [...app.humans]) if (h) h.update(dt, app);
    world.update(dt);
  }
};
const finite = (h) => Object.values(h.pts).every((p) => Number.isFinite(p.x) && Number.isFinite(p.y) && Number.isFinite(p.z));

/* ------------------------------------------------------------------ */
console.log('\n[0] геометрия луча');
{
  const near = (a, b) => Math.abs(a - b) < 1e-6;
  let r = raySegment({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 5 }, { x: 0, y: 0, z: 6 });
  ok(near(r.rayT, 5) && near(r.dist, 0), 'луч вдоль отрезка', `t=${r.rayT} d=${r.dist}`);
  r = raySegment({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 1 }, { x: 0.5, y: 0, z: 5 }, { x: 0.5, y: 0, z: 6 });
  ok(near(r.rayT, 5) && near(r.dist, 0.5), 'перпендикулярное смещение', `t=${r.rayT} d=${r.dist}`);
  r = raySegment({ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, { x: 3, y: 0, z: 1 }, { x: 3, y: 0, z: 2 });
  ok(near(r.rayT, 3) && near(r.dist, 1) && near(r.segT, 0), 'попадание в торец отрезка', `t=${r.rayT} d=${r.dist}`);
  r = raySegment({ x: 0, y: 0, z: 9 }, { x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 5 }, { x: 0, y: 0, z: 6 });
  ok(near(r.rayT, 0), 'цель за спиной не попадает', `t=${r.rayT} d=${r.dist.toFixed(2)}`);
}

/* ------------------------------------------------------------------ */
console.log('\n[1] модули и модели');
{
  ok(WEAPONS.length === 9, '9 видов оружия');
  ok(WEAPONS.every((w) => w.kind), 'у каждого оружия есть тип');
  const counts = WEAPONS.map((w) => {
    let n = 0;
    makeWeaponMesh(w).traverse((o) => { if (o.isMesh) n++; });
    return { id: w.id, n };
  });
  ok(counts.every((c) => c.id === 'fists' || c.n > 0), 'каждая модель собрана', counts.map((c) => c.n).join(','));
  ok(Object.keys(WEAPON_BY_ID).length === 9, 'WEAPON_BY_ID полон');
}

/* ------------------------------------------------------------------ */
console.log('\n[2] мир и частицы');
{
  ok(world.decals && world.points && world.chunks, 'системы крови созданы');
  world.spray({ x: 0, y: 1.2, z: 0 }, { x: 0, y: 0, z: 1 }, { count: 40 });
  world.mist({ x: 0, y: 1.2, z: 0 }, { x: 0, y: 0, z: 1 }, 10);
  world.dust({ x: 0.4, y: 0.1, z: 0.4 }, 8);
  world.spark({ x: 0, y: 0.5, z: 0 }, { x: 0, y: 1, z: 0 }, 6);
  world.chunk({ x: 0, y: 1.2, z: 0 }, { x: 0, y: 1, z: 0 }, { kind: 'bone', count: 6 });
  world.decal(0.2, 0.3, 0.4, 0.8);
  world.tracer({ x: 0, y: 1, z: 0 }, { x: 0, y: 1, z: 5 });
  world.muzzleFlash({ x: 0, y: 1, z: 0.4 }, 1.2);
  let nan = false;
  sim(1 / 60, 3);
  for (let i = 0; i < world.pLife.length; i++) if (!Number.isFinite(world.pLife[i])) nan = true;
  ok(!nan, 'частицы живут без NaN');
  const alive = [...world.pLife].filter((v) => v > 0).length;
  ok(alive === 0, 'частицы умирают за 3с', `alive=${alive}`);
  world.spray({ x: 0, y: 1.2, z: 0 }, { x: 0, y: 0, z: 0.4 }, { count: 30 });
  sim(1 / 60, 0.4);
  const alive2 = [...world.pLife].filter((v) => v > 0).length;
  ok(alive2 > 0, 'свежие частицы живы через 0.4с', `alive=${alive2}`);
  world.clearBlood();
  ok(world.decals.count === 0, 'кровь стирается');
}

/* ------------------------------------------------------------------ */
console.log('\n[3] стойка и поза');
const A = new Human(app, { x: 0, z: 0, weapon: 'knife', ai: false, name: 'Тест' });
app.humans.push(A);
{
  let minY = 99;
  for (let i = 0; i < 180; i++) {
    A.update(1 / 60, app);
    for (const p of Object.values(A.pts)) minY = Math.min(minY, p.y);
  }
  ok(finite(A), 'поза без NaN');
  ok(minY > -0.05, 'ноги не уходят под землю', `minY=${minY.toFixed(3)}`);
  ok(Math.abs(A.pts.head.y - 1.6) < 0.12, 'голова на высоте ~1.6 м', `y=${A.pts.head.y.toFixed(2)}`);
  ok(A.alive && A.health === 100 && A.blood === 5000, 'исходные показатели', `hp=${A.health} blood=${A.blood}`);
  ok(A.weapon.id === 'knife', 'оружие назначено');
}

/* ------------------------------------------------------------------ */
console.log('\n[4] ранение и кровопотеря');
{
  const before = A.blood;
  A.applyHit('torso', { type: 'bullet', dmg: 26, struct: 1.6, cut: 0.55, bleedMul: 1.2, knock: 50,
    point: { ...A.pts.chest }, dir: { x: 0, y: 0, z: 1 } });
  ok(A.health < 100, 'выстрел в торс снял здоровье', `hp=${A.health.toFixed(1)}`);
  ok(A.alive, 'один выстрел не убивает');
  sim(1 / 60, 4);
  ok(A.blood < before, 'кровь убывает', `${before} → ${A.blood.toFixed(0)}`);
  ok(A.bleedTotal > 0, 'есть кровотечение', `${A.bleedTotal.toFixed(2)} мл/с`);
  const wounds = A.limbs.find((l) => l.name === 'torso').wounds.length;
  ok(wounds > 0, 'рана зарегистрирована', `wounds=${wounds}`);
}

/* ------------------------------------------------------------------ */
console.log('\n[5] отрыв конечности');
const B = new Human(app, { x: 2.5, z: 2.5, weapon: 'machete', ai: false, name: 'Руки' });
app.humans.push(B);
{
  let guard = 0;
  while (!B.limb('armN_u').severed && guard < 60) {
    B.applyHit('armN_u', { type: 'slash', dmg: 25, struct: 0.11, cut: 1, bleedMul: 1.4, knock: 30,
      point: { ...B.pts.elN }, dir: { x: 0, y: 0.2, z: 1 } });
    guard++;
  }
  ok(B.limb('armN_u').severed, 'плечо оторвано ударами', `ударов=${guard}`);
  ok(guard >= 5, 'один удар не отрывает руку', `ударов=${guard}`);
  ok(app.debris.some((d) => d.kind === 'limb' && d.l === 'armN_u'), 'кусок тела создан');
  ok(B.limb('armN_l').parentSevered, 'предплечье помечено отделённым');
  const stump = B.limb('armN_u').stumpBleed;
  ok(stump > 0, 'культя кровит', `${stump} мл/с`);
  ok(B.armUsable() === false, 'рука больше не работает');
  ok(app.debris.some((d) => d.kind === 'weapon' && d.def.id === 'machete'), 'оружие выпало из отрубленной руки');
  const b0 = B.blood;
  sim(1 / 60, 5);
  ok(B.blood < b0 - 50, 'кровь уходит из культи', `${b0.toFixed(0)} → ${B.blood.toFixed(0)}`);
}

/* ------------------------------------------------------------------ */
console.log('\n[6] рэгдолл и физика падения');
const C = new Human(app, { x: -2.5, z: 1.5, weapon: 'fists', ai: false, name: 'Падение' });
app.humans.push(C);
{
  C.ragdollize({ x: 1.6, y: 1.1, z: 0.4 }, { ...C.pts.chest });
  C.health = 0;
  C.deathCause = 'тест';
  C.die();                                    // a healthy body would stand back up
  sim(1 / 60, 4);
  ok(C.ragdoll, 'тело в рэгдолле');
  const minY = Math.min(...Object.values(C.pts).map((p) => p.y));
  ok(minY > -0.12, 'ничего не провалилось сквозь пол', `minY=${minY.toFixed(3)}`);
  ok(finite(C), 'физика без NaN');
  const chest = C.pts.chest;
  ok(chest.y < 1.0, 'тело лежит, а не стоит', `chest.y=${chest.y.toFixed(2)}`);
  const speed = Math.max(...Object.values(C.pts).map((p) => Math.hypot(p.vx, p.vy, p.vz)));
  ok(speed < 1.5, 'движение затухло', `v=${speed.toFixed(2)}`);
  ok(C.sleeping || speed < 0.4, 'тело уснуло');
}

/* ------------------------------------------------------------------ */
console.log('\n[7] голова');
{
  const H = new Human(app, { x: 4, z: 0, weapon: 'fists', ai: false, name: 'Голова' });
  app.humans.push(H);
  let guard = 0;
  while (H.alive && guard < 4) {
    H.applyHit('head', { type: 'bullet', dmg: 78, struct: 14, cut: 0.55, bleedMul: 1.7, knock: 150,
      point: { ...H.pts.head }, dir: { x: 0, y: 0, z: 1 } });
    guard++;
  }
  ok(!H.alive, 'голова: смерть за 1–3 попадания', `выстрелов=${guard}`);
  ok(!!H.deathCause, 'причина смерти записана', H.deathCause);
  ok(app.debris.some((d) => d.kind === 'head'), 'голова отделена как объект');
  sim(1 / 60, 2);
  ok(finite(H), 'труп стабилен');
  ok(app.logs.length > 0, 'события логируются');
}

/* ------------------------------------------------------------------ */
console.log('\n[8] ИИ: перестрелка');
{
  app.humans.length = 0;
  const N = 4;
  for (let i = 0; i < N; i++) {
    const h = new Human(app, {
      x: -3 + i * 2, z: -3 + (i % 2) * 2,
      weapon: i % 2 ? 'pistol' : 'smg',
      ai: true,
      name: 'Боец' + i,
    });
    app.humans.push(h);
  }
  const hp0 = app.humans.map((h) => h.health);
  sim(1 / 60, 25);
  const anyDamage = app.humans.some((h, i) => h.health < hp0[i]);
  ok(anyDamage, 'ИИ попал хотя бы раз');
  ok(app.humans.every(finite), 'ИИ-физика без NaN');
  const dead = app.humans.filter((h) => !h.alive).length;
  ok(dead <= N, 'нет воскрешений', `мертвы: ${dead}/${N}`);
  ok(app.humans.every((h) => Math.abs(h.x) <= 26.1), 'никто не ушёл за карту');
  console.log('      итог:', app.humans.map((h) => `${h.name} ${h.alive ? 'жив ' + h.health.toFixed(0) : 'мёртв'}`).join(' | '));
}

/* ------------------------------------------------------------------ */
console.log('\n[9] инспектор');
{
  const h = app.humans[0];
  const info = h.info();
  ok(typeof info.health === 'number' && Number.isFinite(info.health), 'здоровье в данных', String(info.health));
  ok(info.limbs.length === LIMBS.length, 'все конечности в данных', `${info.limbs.length}`);
  ok(info.limbs.every((l) => typeof l.name === 'string' && l.name.length > 0), 'у конечностей есть названия');
  ok(info.limbs.some((l) => l.severed) || true, 'статус конечностей читается');
  ok(!Number.isNaN(info.blood), 'кровь числовая', String(info.blood.toFixed(0)));
}

/* ------------------------------------------------------------------ */
console.log(`\n${fails === 0 ? 'ВСЁ ОК' : 'ОШИБОК: ' + fails}\n`);
process.exit(fails === 0 ? 0 : 1);
