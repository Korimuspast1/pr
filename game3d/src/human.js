import * as THREE from '../vendor/three.module.js';
import {
  clamp, lerp, rand, chance, pick, TAU, v3, dist, norm, sub, add, mul, dot, cross,
  angleDiff, approachAngle,
} from './util3.js';
import { WEAPONS, WEAPON_BY_ID, makeWeaponMesh } from './weapons3d.js';

const SKIN = [0xd8a179, 0xc78e6a, 0xe8b896, 0xb07a55, 0x8d5c3d, 0x6d4530];
const SHIRT = [0x43587a, 0x6b4a7a, 0x3f6b52, 0x7d5637, 0x5b5f66, 0x7a3b3b, 0xb9a479, 0x2f4f6f];
const PANTS = [0x333a46, 0x2f3d55, 0x3a3a3f, 0x4a3a2a, 0x2b3340];
const HAIR = [0x2a1d16, 0x3d2a1a, 0x161311, 0x5a3a22, 0x7a5a35, 0xa8a29a];
const NAMES = ['Марк', 'Таня', 'Кирилл', 'Лена', 'Гоша', 'Рита', 'Дима', 'Соня', 'Витя', 'Юля',
  'Артём', 'Настя', 'Пётр', 'Оля', 'Серёга', 'Маша', 'Илья', 'Вера', 'Костя', 'Даша'];

export const SKIN_PRESETS = SKIN;
export const SHIRT_PRESETS = SHIRT;
export const NAMES_LIST = NAMES;

// standing rest pose, metres (Y up, +Z forward)
export const REST = {
  pelvis: [0, 0.95, 0],
  chest: [0, 1.30, 0],
  neck: [0, 1.44, 0],
  head: [0, 1.60, 0.01],
  shN: [0.20, 1.36, 0], shF: [-0.20, 1.36, 0],
  elN: [0.245, 1.10, 0.02], elF: [-0.245, 1.10, 0.02],
  wrN: [0.265, 0.86, 0.03], wrF: [-0.265, 0.86, 0.03],
  hipN: [0.10, 0.93, 0], hipF: [-0.10, 0.93, 0],
  knN: [0.12, 0.50, 0.01], knF: [-0.12, 0.50, 0.01],
  anN: [0.12, 0.08, 0.02], anF: [-0.12, 0.08, 0.02],
};
export const POINT_NAMES = Object.keys(REST);

// limb damage model: which points it spans, capsule radius, tissue type
export const LIMBS = [
  { name: 'head', label: 'голова', a: 'neck', b: 'head', r: 0.115, bone: 'skull', kind: 'skin', mass: 5, parent: 'torso' },
  { name: 'torso', label: 'торс', a: 'pelvis', b: 'chest', r: 0.17, bone: 'spine', kind: 'cloth', mass: 26, parent: null },
  { name: 'armN_u', label: 'плечо (пр.)', a: 'shN', b: 'elN', r: 0.055, bone: 'arm', kind: 'cloth', mass: 3, parent: 'torso' },
  { name: 'armN_l', label: 'предплечье (пр.)', a: 'elN', b: 'wrN', r: 0.047, bone: 'arm', kind: 'skin', mass: 2, parent: 'armN_u' },
  { name: 'armF_u', label: 'плечо (лев.)', a: 'shF', b: 'elF', r: 0.055, bone: 'arm', kind: 'cloth', mass: 3, parent: 'torso' },
  { name: 'armF_l', label: 'предплечье (лев.)', a: 'elF', b: 'wrF', r: 0.047, bone: 'arm', kind: 'skin', mass: 2, parent: 'armF_u' },
  { name: 'legN_u', label: 'бедро (пр.)', a: 'hipN', b: 'knN', r: 0.08, bone: 'leg', kind: 'pants', mass: 7, parent: 'torso' },
  { name: 'legN_l', label: 'голень (пр.)', a: 'knN', b: 'anN', r: 0.06, bone: 'leg', kind: 'pants', mass: 4, parent: 'legN_u' },
  { name: 'legF_u', label: 'бедро (лев.)', a: 'hipF', b: 'knF', r: 0.08, bone: 'leg', kind: 'pants', mass: 7, parent: 'torso' },
  { name: 'legF_l', label: 'голень (лев.)', a: 'knF', b: 'anF', r: 0.06, bone: 'leg', kind: 'pants', mass: 4, parent: 'legF_u' },
];

// distance constraints for the ragdoll solver
const BONES = [
  ['pelvis', 'chest'], ['chest', 'neck'], ['neck', 'head'],
  ['chest', 'shN'], ['shN', 'elN'], ['elN', 'wrN'],
  ['chest', 'shF'], ['shF', 'elF'], ['elF', 'wrF'],
  ['pelvis', 'hipN'], ['hipN', 'knN'], ['knN', 'anN'],
  ['pelvis', 'hipF'], ['hipF', 'knF'], ['knF', 'anF'],
  ['shN', 'shF'], ['hipN', 'hipF'], ['pelvis', 'neck'],
  ['hipN', 'hipF'],
];
// hard maximum distances: stops the body folding into itself
const MAX_DISTS = [
  ['head', 'pelvis', 0.82], ['head', 'chest', 0.42],
  ['wrN', 'chest', 0.86], ['wrF', 'chest', 0.86],
  ['wrN', 'head', 0.95], ['wrF', 'head', 0.95],
  ['anN', 'pelvis', 1.02], ['anF', 'pelvis', 1.02],
  ['knN', 'head', 1.25], ['knF', 'head', 1.25],
  ['elN', 'pelvis', 0.9], ['elF', 'pelvis', 0.9],
];

let NEXT_ID = 1;

export class Human {
  constructor(app, opts = {}) {
    this.app = app;
    this.id = NEXT_ID++;
    this.name = opts.name || pick(NAMES);
    this.skin = opts.skin || pick(SKIN);
    this.shirt = opts.shirt || pick(SHIRT);
    this.pants = opts.pants || pick(PANTS);
    this.hair = opts.hair || pick(HAIR);
    this.weapon = WEAPON_BY_ID[opts.weapon || 'fists'] || WEAPONS[0];
    this.ai = {
      enabled: opts.ai != null ? opts.ai : false,
      aggression: rand(0.35, 0.65),
      target: null, retarget: 0, aimErr: rand(-0.14, 0.14),
      reaction: rand(0.15, 0.45), state: 'idle',
    };
    this.group = new THREE.Group();
    this.pts = {};
    this.limbs = [];
    this.debris = [];
    this.weaponMesh = null;
    this.buildPoints(opts.x != null ? opts.x : rand(-3, 3), opts.z != null ? opts.z : rand(-3, 3));
    this.buildMeshes();
    this.resetStats();
  }

  /* ---------------- construction ---------------- */

  buildPoints(x, z) {
    this.x = x;
    this.z = z;
    this.yaw = rand(0, TAU);
    this.walkPhase = rand(0, TAU);
    this.breath = rand(0, TAU);
    for (const k of POINT_NAMES) {
      const [lx, ly, lz] = REST[k];
      this.pts[k] = { x: x + lx, y: ly, z: z + lz, px: 0, py: 0, pz: 0, ox: x + lx, oy: ly, oz: z + lz, inv: this.invMassFor(k) };
    }
    this.rest = {};
    for (const [a, b] of BONES) this.rest[a + b] = Math.max(0.01, dist(this.pts[a], this.pts[b]));
    this.limbs = LIMBS.map((d) => ({
      ...d,
      struct: 0, severed: false, parentSevered: false, broken: false,
      wounds: [], arterial: false, bleedRate: 0, stumpBleed: 0, cuts: 0,
    }));
  }

  invMassFor(k) {
    if (k === 'pelvis') return 1 / 22;
    if (k === 'chest') return 1 / 26;
    if (k === 'head') return 1 / 7;
    if (k === 'neck') return 1 / 5;
    if (k.includes('wr')) return 1 / 2.4;
    if (k.includes('an')) return 1 / 3.4;
    if (k.includes('el')) return 1 / 3;
    if (k.includes('kn')) return 1 / 5.5;
    if (k.includes('sh')) return 1 / 5;
    if (k.includes('hip')) return 1 / 7;
    return 1 / 5;
  }

  buildMeshes() {
    const M = (color, opts = {}) => {
      const { rough, metal, ...rest } = opts;
      return new THREE.MeshStandardMaterial({
        color, roughness: rough != null ? rough : 0.85, metalness: metal || 0.02, ...rest,
      });
    };
    this.matSkin = M(this.skin, { rough: 0.72 });
    this.matShirt = M(this.shirt, { rough: 0.92 });
    this.matPants = M(this.pants, { rough: 0.95 });
    this.matShoe = M(0x1e1a17, { rough: 0.8 });
    this.matHair = M(this.hair, { rough: 1 });
    this.matDark = M(0x5b0609, { rough: 0.55 });
    this.matBone = M(0xe6ddc8, { rough: 0.6 });
    this.matSteel = M(0x9aa2ad, { rough: 0.35, metal: 0.85 });
    this.matBloody = M(0x6d0609, { rough: 0.5 });

    this.segMeshes = {};
    const unitCyl = new THREE.CylinderGeometry(1, 1, 1, 8, 1, false);
    const shoeGeo = new THREE.BoxGeometry(0.11, 0.075, 0.25);
    const handGeo = new THREE.SphereGeometry(0.045, 8, 6);

    const addSeg = (name, mat, r1, r2) => {
      const m = new THREE.Mesh(unitCyl, mat);
      m.castShadow = true;
      m.receiveShadow = true;
      m.userData = { human: this, limb: name };
      this.group.add(m);
      this.segMeshes[name] = { mesh: m, r1, r2 };
      return m;
    };

    addSeg('armN_u', this.matShirt, 0.056, 0.05);
    addSeg('armN_l', this.matSkin, 0.047, 0.042);
    addSeg('armF_u', this.matShirt, 0.056, 0.05);
    addSeg('armF_l', this.matSkin, 0.047, 0.042);
    addSeg('legN_u', this.matPants, 0.082, 0.07);
    addSeg('legN_l', this.matPants, 0.062, 0.052);
    addSeg('legF_u', this.matPants, 0.082, 0.07);
    addSeg('legF_l', this.matPants, 0.062, 0.052);
    addSeg('torso', this.matShirt, 0.17, 0.15);
    addSeg('neckSeg', this.matSkin, 0.05, 0.05);

    // torso jacket block for volume
    this.chestMesh = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.42, 0.23), this.matShirt);
    this.chestMesh.castShadow = true;
    this.chestMesh.userData = { human: this, limb: 'torso' };
    this.group.add(this.chestMesh);

    this.pelvisMesh = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.2, 0.21), this.matPants);
    this.pelvisMesh.castShadow = true;
    this.pelvisMesh.userData = { human: this, limb: 'torso' };
    this.group.add(this.pelvisMesh);

    // head
    this.headMesh = new THREE.Mesh(new THREE.SphereGeometry(0.112, 16, 12), this.matSkin);
    this.headMesh.castShadow = true;
    this.headMesh.userData = { human: this, limb: 'head' };
    this.group.add(this.headMesh);
    this.hairMesh = new THREE.Mesh(new THREE.SphereGeometry(0.116, 16, 10, 0, TAU, 0, Math.PI * 0.62), this.matHair);
    this.hairMesh.castShadow = true;
    this.hairMesh.userData = { human: this, limb: 'head' };
    this.group.add(this.hairMesh);
    // eyes
    this.eyeN = new THREE.Mesh(new THREE.SphereGeometry(0.017, 6, 5), M(0x141017, { rough: 0.4 }));
    this.eyeF = this.eyeN.clone();
    this.group.add(this.eyeN, this.eyeF);

    // hands / feet
    this.handN = new THREE.Mesh(handGeo, this.matSkin);
    this.handF = new THREE.Mesh(handGeo, this.matSkin);
    this.footN = new THREE.Mesh(shoeGeo, this.matShoe);
    this.footF = new THREE.Mesh(shoeGeo, this.matShoe);
    for (const m of [this.handN, this.handF, this.footN, this.footF]) {
      m.castShadow = true;
      m.userData = { human: this, limb: m === this.footN || m === this.footF ? 'legL' : 'armL' };
      this.group.add(m);
    }

    // bloody overlay spheres for severe wounds on head/torso
    this.stumpNeck = new THREE.Mesh(new THREE.SphereGeometry(0.07, 10, 8), this.matDark);
    this.stumpNeck.visible = false;
    this.group.add(this.stumpNeck);

    // wound splotches + stumps (pooled; they follow the skeleton every frame)
    this.patchGeo = new THREE.SphereGeometry(1, 6, 4);
    this.stumpGeo = new THREE.SphereGeometry(1, 9, 7);
    this.matWound = M(0x5c060a, { rough: 0.5 });
    this.patches = [];
    this.stumps = [];

    this.group.position.set(0, 0, 0);
    this.app.scene.add(this.group);
  }

  /* ---------------- stats ---------------- */

  resetStats() {
    this.health = 100;
    this.blood = 5000;
    this.alive = true;
    this.unconscious = false;
    this.deathCause = '';
    this.ragdoll = false;
    this.ragTime = 0;
    this.sleeping = false;
    this.sleepTimer = 0;
    this.pain = 0;
    this.bleedTotal = 0;
    this.moving = false;
    this.attackCd = 0;
    this.reloadT = 0;
    this.swing = 0;
    this.aimAngle = 0;
    this.animTime = 0;
    this.hitFlash = 0;
    this.poolT = 0;
    this.lastAttacker = null;
    this.mags = {};
    for (const w of WEAPONS) if (w.magSize) this.mags[w.id] = w.magSize;
    this.attachWeapon();
  }

  attachWeapon() {
    if (this.weaponMesh) {
      this.group.remove(this.weaponMesh);
      this.weaponMesh = null;
    }
    if (this.weapon && this.weapon.id !== 'fists') {
      this.weaponMesh = makeWeaponMesh(this.weapon);
      this.group.add(this.weaponMesh);
    }
  }

  setWeapon(id) {
    this.weapon = WEAPON_BY_ID[id] || WEAPONS[0];
    this.attachWeapon();
  }

  addWoundPatch(limbName) {
    let p;
    if (this.patches.length >= 14) {
      p = this.patches.shift();
    } else {
      p = { mesh: new THREE.Mesh(this.patchGeo, this.matWound), limb: limbName, t: 0.5, dir: null };
      this.group.add(p.mesh);
    }
    p.limb = limbName;
    p.t = rand(0.2, 0.8);
    p.dir = { x: rand(-1, 1), y: rand(-1, 1), z: rand(-1, 1) };
    this.patches.push(p);
  }

  addStump(limbName) {
    const mesh = new THREE.Mesh(this.stumpGeo, this.matWound);
    this.group.add(mesh);
    this.stumps.push({ mesh, limb: limbName });
  }

  // Wounds and stumps live on the body group, not on the moving limb meshes:
  // positions are recomputed from the skeleton, so they always sit on the skin.
  updateWoundDecals() {
    for (const p of this.patches) {
      const l = this.limb(p.limb);
      if (!l || l.severed || l.parentSevered) { p.mesh.visible = false; continue; }
      const a = this.pts[l.a], b = this.pts[l.b];
      let ax = b.x - a.x, ay = b.y - a.y, az = b.z - a.z;
      const len2 = ax * ax + ay * ay + az * az || 1e-6;
      const d = p.dir;
      const along = (d.x * ax + d.y * ay + d.z * az) / len2;
      let ox = d.x - ax * along, oy = d.y - ay * along, oz = d.z - az * along;
      const ol = Math.hypot(ox, oy, oz) || 1;
      ox /= ol; oy /= ol; oz /= ol;
      const r = l.r * 0.92;
      p.mesh.position.set(
        a.x + ax * p.t + ox * r,
        a.y + ay * p.t + oy * r,
        a.z + az * p.t + oz * r
      );
      const size = 0.012 + l.r * 0.075;
      p.mesh.scale.set(size, size, size);
      p.mesh.visible = true;
    }
    for (const st of this.stumps) {
      const l = this.limb(st.limb);
      const a = this.pts[l.a];
      st.mesh.position.set(a.x, a.y, a.z);
      const r = l.r * 1.25;
      st.mesh.scale.set(r, r * 0.85, r);
      st.mesh.visible = !l.parentSevered;
    }
  }

  /* ---------------- helpers ---------------- */

  get forward() { return { x: Math.sin(this.yaw), y: 0, z: Math.cos(this.yaw) }; }
  get right() { return { x: Math.cos(this.yaw), y: 0, z: -Math.sin(this.yaw) }; }

  toWorld(lx, ly, lz) {
    const r = this.right, f = this.forward;
    return {
      x: this.x + r.x * lx + f.x * lz,
      y: ly,
      z: this.z + r.z * lx + f.z * lz,
    };
  }

  limb(name) { return this.limbs.find((l) => l.name === name); }
  limbByBone(bone) { return this.limbs.find((l) => l.bone === bone); }

  pointOf(name) { return this.pts[name]; }

  center() {
    return { x: (this.pts.pelvis.x + this.pts.chest.x) / 2, y: (this.pts.pelvis.y + this.pts.chest.y) / 2, z: (this.pts.pelvis.z + this.pts.chest.z) / 2 };
  }

  headPos() { return this.pts.head; }

  /* ---------------- standing pose ---------------- */

  pose(dt) {
    const speed = this.moving ? 1.35 : 0;
    this.walkPhase += dt * (this.moving ? 7.2 : 1.2);
    this.animTime += dt;
    const P = this.pts;

    const bob = this.moving ? Math.abs(Math.sin(this.walkPhase * 2)) * 0.025 : Math.sin(this.breath + this.animTime * 1.4) * 0.004;
    const hipY = 0.95 - bob + (this.crouch || 0);

    const stride = this.moving ? Math.sin(this.walkPhase) : 0;
    const strideF = -stride;
    const bendN = Math.max(0, -Math.sin(this.walkPhase)) * 0.9 + 0.08;
    const bendF = Math.max(0, Math.sin(this.walkPhase)) * 0.9 + 0.08;

    // --- legs
    const hipN = this.toWorld(0.10, hipY, 0);
    const hipF = this.toWorld(-0.10, hipY, 0);
    const thN = stride * 0.55, thF = strideF * 0.55;
    const knN = {
      x: hipN.x + this.forward.x * Math.sin(thN) * 0.43,
      y: hipN.y - Math.cos(thN) * 0.43,
      z: hipN.z + this.forward.z * Math.sin(thN) * 0.43,
    };
    const knF = {
      x: hipF.x + this.forward.x * Math.sin(thF) * 0.43,
      y: hipF.y - Math.cos(thF) * 0.43,
      z: hipF.z + this.forward.z * Math.sin(thF) * 0.43,
    };
    const shinN = thN - bendN, shinF = thF - bendF;
    const anN = {
      x: knN.x + this.forward.x * Math.sin(shinN) * 0.42,
      y: Math.max(0.075, knN.y - Math.cos(shinN) * 0.42),
      z: knN.z + this.forward.z * Math.sin(shinN) * 0.42,
    };
    const anF = {
      x: knF.x + this.forward.x * Math.sin(shinF) * 0.42,
      y: Math.max(0.075, knF.y - Math.cos(shinF) * 0.42),
      z: knF.z + this.forward.z * Math.sin(shinF) * 0.42,
    };

    // --- spine
    const lean = this.moving ? 0.06 : 0.015;
    const pelvis = this.toWorld(0, hipY, 0);
    const chest = this.toWorld(0, hipY + 0.35, lean * 0.6);
    const neck = this.toWorld(0, hipY + 0.49, lean);
    const head = this.toWorld(0, hipY + 0.645, lean + Math.sin(this.animTime * 1.7 + this.breath) * 0.004);

    // --- arms
    const aim = this.aimTarget || this.toWorld(0, hipY + 0.2, 1);
    const armed = this.weapon.kind === 'gun';
    const shN = this.toWorld(0.2, hipY + 0.41, 0.01);
    const shF = this.toWorld(-0.2, hipY + 0.41, 0.01);

    let handNTarget, handFTarget;
    if (armed) {
      const dir = norm(sub(aim, shN));
      handNTarget = add(shN, mul(dir, 0.60));
      handFTarget = add(shF, mul(dir, 0.50));
    } else {
      const sw = this.swingActive ? this.swingProgress : 0;
      const swingAng = sw < 0.5 ? lerp(-1.5, 0.9, sw / 0.5) : lerp(0.9, -0.2, (sw - 0.5) / 0.5);
      const dirAim = norm(sub(aim, shN));
      const handDirN = norm({
        x: dirAim.x + this.forward.x * (this.swingActive ? Math.sin(swingAng) : 0.25),
        y: dirAim.y + (this.swingActive ? Math.cos(swingAng) * 0.4 : -0.25),
        z: dirAim.z + this.forward.z * (this.swingActive ? Math.sin(swingAng) : 0.25),
      });
      handNTarget = add(shN, mul(handDirN, 0.58));
      const swingF = this.moving ? Math.sin(this.walkPhase) * 0.35 : 0;
      handFTarget = add(shF, mul(norm({ x: -this.forward.x * 0.3 + swingF, y: -0.9, z: -this.forward.z * 0.3 }), 0.56));
    }

    const elN = solveIK3(shN, handNTarget, 0.28, 0.25, { x: -this.forward.x, y: -1, z: -this.forward.z });
    const elF = solveIK3(shF, handFTarget, 0.28, 0.25, { x: -this.forward.x, y: -1, z: -this.forward.z });

    P.pelvis.x = pelvis.x; P.pelvis.y = pelvis.y; P.pelvis.z = pelvis.z;
    P.chest.x = chest.x; P.chest.y = chest.y; P.chest.z = chest.z;
    P.neck.x = neck.x; P.neck.y = neck.y; P.neck.z = neck.z;
    P.head.x = head.x; P.head.y = head.y; P.head.z = head.z;
    P.hipN.x = hipN.x; P.hipN.y = hipN.y; P.hipN.z = hipN.z;
    P.hipF.x = hipF.x; P.hipF.y = hipF.y; P.hipF.z = hipF.z;
    P.knN.x = knN.x; P.knN.y = knN.y; P.knN.z = knN.z;
    P.knF.x = knF.x; P.knF.y = knF.y; P.knF.z = knF.z;
    P.anN.x = anN.x; P.anN.y = anN.y; P.anN.z = anN.z;
    P.anF.x = anF.x; P.anF.y = anF.y; P.anF.z = anF.z;
    P.shN.x = shN.x; P.shN.y = shN.y; P.shN.z = shN.z;
    P.shF.x = shF.x; P.shF.y = shF.y; P.shF.z = shF.z;
    P.elN.x = elN.x; P.elN.y = elN.y; P.elN.z = elN.z;
    P.elF.x = elF.x; P.elF.y = elF.y; P.elF.z = elF.z;
    P.wrN.x = handNTarget.x; P.wrN.y = handNTarget.y; P.wrN.z = handNTarget.z;
    P.wrF.x = handFTarget.x; P.wrF.y = handFTarget.y; P.wrF.z = handFTarget.z;
  }

  /* ---------------- ragdoll solvers ---------------- */

  ragdollize(impulse, at) {
    if (this.ragdoll) {
      if (impulse) this.applyImpulseAt(at || this.center(), impulse);
      return;
    }
    this.ragdoll = true;
    this.ragTime = 0;
    this.sleeping = false;
    this.sleepTimer = 0;
    this.swingActive = false;
    for (const k of POINT_NAMES) {
      const p = this.pts[k];
      p.px = p.x; p.py = p.y; p.pz = p.z;
      p.vx = 0; p.vy = 0; p.vz = 0;
    }
    if (impulse) this.applyImpulseAt(at || this.center(), impulse);
  }

  wake() {
    this.sleeping = false;
    this.sleepTimer = 0;
  }

  applyImpulseAt(point, impulse) {
    this.wake();
    let total = 0;
    const ws = [];
    for (const k of POINT_NAMES) {
      const p = this.pts[k];
      const d2 = (p.x - point.x) ** 2 + (p.y - point.y) ** 2 + (p.z - point.z) ** 2;
      const w = 1 / (d2 + 0.05);
      ws.push([p, w]);
      total += w;
    }
    const strength = impulse.strength != null ? impulse.strength : 1;
    for (const [p, w] of ws) {
      const k = w / total;
      p.vx += impulse.x * k * strength;
      p.vy += impulse.y * k * strength;
      p.vz += impulse.z * k * strength;
    }
    for (const k of POINT_NAMES) {
      const p = this.pts[k];
      p.px = p.x - p.vx * 0.001;
      p.py = p.y - p.vy * 0.001;
      p.pz = p.z - p.vz * 0.001;
    }
  }

  stepPhysics(h) {
    const P = this.pts;
    const G = -9.81;
    const damp = Math.exp(-0.55 * h);

    for (const k of POINT_NAMES) {
      const p = P[k];
      if (p.held) continue;
      p.vy += G * h;
      p.vx *= damp; p.vy *= damp; p.vz *= damp;
      p.px = p.x; p.py = p.y; p.pz = p.z;
      p.x += p.vx * h;
      p.y += p.vy * h;
      p.z += p.vz * h;
    }

    const iterations = 7;
    for (let it = 0; it < iterations; it++) {
      for (const [an, bn] of BONES) {
        if (this.boneSevered(an, bn)) continue;
        const a = P[an], b = P[bn];
        const rest = this.rest[an + bn];
        const dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
        const d = Math.hypot(dx, dy, dz) || 1e-6;
        const ia = a.held ? 0 : a.inv, ib = b.held ? 0 : b.inv;
        const sum = ia + ib;
        if (sum <= 0) continue;
        const corr = ((d - rest) / d) * 0.9;
        const cx = dx * corr, cy = dy * corr, cz = dz * corr;
        a.x += cx * (ia / sum); a.y += cy * (ia / sum); a.z += cz * (ia / sum);
        b.x -= cx * (ib / sum); b.y -= cy * (ib / sum); b.z -= cz * (ib / sum);
      }
      for (const [an, bn, maxD] of MAX_DISTS) {
        if (this.boneSevered(an, bn)) continue;
        const a = P[an], b = P[bn];
        const dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
        const d = Math.hypot(dx, dy, dz);
        if (d <= maxD) continue;
        const ia = a.held ? 0 : a.inv, ib = b.held ? 0 : b.inv;
        const sum = ia + ib || 1;
        const corr = ((d - maxD) / d) * 0.6;
        const cx = dx * corr, cy = dy * corr, cz = dz * corr;
        a.x += cx * (ia / sum); a.y += cy * (ia / sum); a.z += cz * (ia / sum);
        b.x -= cx * (ib / sum); b.y -= cy * (ib / sum); b.z -= cz * (ib / sum);
      }
      // ground
      for (const k of POINT_NAMES) {
        const p = P[k];
        const r = this.pointRadius(k);
        if (p.y < r) {
          p.y = r;
          p.px = p.x; p.pz = p.z;
          // ground friction
          p.x = p.px + (p.x - p.px) * 0.75;
        }
        if (Math.abs(p.x) > 70) p.x = clamp(p.x, -70, 70);
        if (Math.abs(p.z) > 70) p.z = clamp(p.z, -70, 70);
      }
    }

    // derive velocity from the solved positions
    const invH = 1 / h;
    for (const k of POINT_NAMES) {
      const p = P[k];
      if (p.held) { p.px = p.x; p.py = p.y; p.pz = p.z; p.vx = p.vy = p.vz = 0; continue; }
      const grounded = p.y <= this.pointRadius(k) + 0.01;
      const f = grounded ? 0.72 : 0.995;
      p.vx = (p.x - p.px) * invH * f;
      p.vy = (p.y - p.py) * invH * f;
      p.vz = (p.z - p.pz) * invH * f;
      if (Math.abs(p.vx) < 0.03) p.vx = 0;
      if (Math.abs(p.vy) < 0.03) p.vy = 0;
      if (Math.abs(p.vz) < 0.03) p.vz = 0;
    }
  }

  pointRadius(k) {
    if (k === 'head') return 0.11;
    if (k === 'pelvis' || k === 'chest') return 0.16;
    if (k.includes('wr')) return 0.045;
    if (k.includes('an')) return 0.06;
    if (k.includes('el')) return 0.05;
    if (k.includes('kn')) return 0.07;
    return 0.07;
  }

  boneSevered(an, bn) {
    const l = this.limbs.find((lb) => (lb.a === an && lb.b === bn) || (lb.a === bn && lb.b === an));
    if (l) return l.severed || l.parentSevered;
    return false;
  }

  /* ---------------- per-frame ---------------- */

  update(dt, app) {
    this.animTime += dt;
    if (this.hitFlash > 0) this.hitFlash -= dt;
    if (this.attackCd > 0) this.attackCd -= dt;
    if (this.reloadT > 0) this.reloadT -= dt;

    if (this.alive && !this.unconscious) this.updateAI(dt, app);

    if (this.swingActive) {
      this.swingProgress += dt / Math.max(0.1, this.swingDuration);
      if (this.swingProgress >= 1) {
        this.swingActive = false;
        this.swingProgress = 0;
      } else {
        this.meleeHits(dt, app);
      }
    }

    if (this.ragdoll) {
      this.ragTime += dt;
      if (!this.sleeping) {
        const steps = 2;
        for (let i = 0; i < steps; i++) this.stepPhysics(dt / steps);
        let maxV = 0;
        for (const k of POINT_NAMES) {
          const p = this.pts[k];
          maxV = Math.max(maxV, Math.hypot(p.vx, p.vy, p.vz));
        }
        if (maxV < 0.35 && this.ragTime > 0.6) {
          this.sleepTimer += dt;
          if (this.sleepTimer > 0.45) {
            this.sleeping = true;
            for (const k of POINT_NAMES) { const p = this.pts[k]; p.vx = p.vy = p.vz = 0; }
          }
        } else this.sleepTimer = 0;
      }
      // let a healthy person get back up
      if (this.alive && !this.unconscious && this.ragTime > 2.6 && this.health > 35 && this.legsUsable()) {
        this.standUpTimer = (this.standUpTimer || 0) + dt;
        if (this.standUpTimer > 1.0) this.standUp();
      } else this.standUpTimer = 0;
      this.x = this.pts.pelvis.x;
      this.z = this.pts.pelvis.z;
    } else {
      this.pose(dt);
    }

    this.updateBleeding(dt, app);
    this.updateMeshes();
  }

  standUp() {
    this.ragdoll = false;
    this.ragTime = 0;
    this.sleeping = false;
    this.standUpTimer = 0;
    this.x = this.pts.pelvis.x;
    this.z = this.pts.pelvis.z;
    this.pose(0.016);
  }

  legsUsable() {
    const bad = (n) => {
      const l = this.limb(n);
      return !l || l.severed || l.parentSevered || l.broken;
    };
    return !(bad('legN_u') || bad('legF_u') || bad('legN_l') || bad('legF_l'));
  }

  armUsable() {
    const l1 = this.limb('armN_u'), l2 = this.limb('armN_l');
    return !(l1.severed || l1.parentSevered || l2.severed || l2.parentSevered);
  }

  /* ---------------- meshes ---------------- */

  updateMeshes() {
    const P = this.pts;
    const setSeg = (limbName, a, b, r1, r2) => {
      const rec = this.segMeshes[limbName];
      const l = this.limb(limbName);
      const hide = !rec || !l || l.severed || l.parentSevered;
      if (hide) { if (rec) rec.mesh.visible = false; return; }
      rec.mesh.visible = true;
      orientSegment(rec.mesh, a, b, r1, r2);
    };

    setSeg('torso', P.pelvis, P.chest, 0.16, 0.15);
    setSeg('neckSeg', P.neck, P.head, 0.05, 0.045);
    setSeg('armN_u', P.shN, P.elN, 0.056, 0.05);
    setSeg('armN_l', P.elN, P.wrN, 0.047, 0.042);
    setSeg('armF_u', P.shF, P.elF, 0.056, 0.05);
    setSeg('armF_l', P.elF, P.wrF, 0.047, 0.042);
    setSeg('legN_u', P.hipN, P.knN, 0.082, 0.07);
    setSeg('legN_l', P.knN, P.anN, 0.062, 0.052);
    setSeg('legF_u', P.hipF, P.knF, 0.082, 0.07);
    setSeg('legF_l', P.knF, P.anF, 0.062, 0.052);

    // chest / pelvis blocks
    const tL = this.limb('torso');
    const torsoVisible = tL && !tL.severed;
    this.chestMesh.visible = torsoVisible;
    this.pelvisMesh.visible = torsoVisible;
    if (torsoVisible) {
      const mid = { x: (P.pelvis.x + P.chest.x) / 2, y: (P.pelvis.y + P.chest.y) / 2, z: (P.pelvis.z + P.chest.z) / 2 };
      this.chestMesh.position.set(P.chest.x, P.chest.y, P.chest.z);
      this.chestMesh.quaternion.copy(quatFromTo({ x: 0, y: 1, z: 0 }, norm(sub(P.chest, P.pelvis))));
      this.pelvisMesh.position.set(P.pelvis.x, P.pelvis.y, P.pelvis.z);
      this.pelvisMesh.quaternion.copy(this.chestMesh.quaternion);
      void mid;
    }

    // head
    const headL = this.limb('head');
    const headVisible = headL && !headL.severed && !headL.parentSevered;
    this.headMesh.visible = headVisible;
    this.hairMesh.visible = headVisible;
    this.eyeN.visible = this.eyeF.visible = headVisible;
    this.stumpNeck.visible = !headVisible;
    if (headVisible) {
      this.headMesh.position.set(P.head.x, P.head.y, P.head.z);
      const up = norm(sub(P.head, P.neck));
      this.hairMesh.position.set(P.head.x + up.y * 0.02, P.head.y + 0.035, P.head.z + up.z * 0.02);
      this.hairMesh.quaternion.copy(quatFromTo({ x: 0, y: 1, z: 0 }, up));
      const fwd = norm({ x: Math.sin(this.yaw), y: 0, z: Math.cos(this.yaw) });
      const rightV = { x: Math.cos(this.yaw), y: 0, z: -Math.sin(this.yaw) };
      this.eyeN.position.set(P.head.x + fwd.x * 0.095 + rightV.x * 0.04, P.head.y + 0.02, P.head.z + fwd.z * 0.095 + rightV.z * 0.04);
      this.eyeF.position.set(P.head.x + fwd.x * 0.095 - rightV.x * 0.04, P.head.y + 0.02, P.head.z + fwd.z * 0.095 - rightV.z * 0.04);
    } else {
      this.stumpNeck.position.set(P.neck.x, P.neck.y, P.neck.z);
    }

    // hands / feet
    const armN = this.limb('armN_l');
    const armF = this.limb('armF_l');
    const legN = this.limb('legN_l');
    const legF = this.limb('legF_l');
    this.handN.visible = !(armN.severed || armN.parentSevered);
    this.handF.visible = !(armF.severed || armF.parentSevered);
    this.footN.visible = !(legN.severed || legN.parentSevered);
    this.footF.visible = !(legF.severed || legF.parentSevered);
    this.handN.position.set(P.wrN.x, P.wrN.y, P.wrN.z);
    this.handF.position.set(P.wrF.x, P.wrF.y, P.wrF.z);
    this.footN.position.set(P.anN.x, P.anN.y - 0.02, P.anN.z + 0.04);
    this.footF.position.set(P.anF.x, P.anF.y - 0.02, P.anF.z + 0.04);
    const fwd = norm({ x: Math.sin(this.yaw), y: 0, z: Math.cos(this.yaw) });
    const footQ = quatFromTo({ x: 0, y: 0, z: 1 }, fwd);
    this.footN.quaternion.copy(footQ);
    this.footF.quaternion.copy(footQ);

    this.updateWoundDecals();

    // weapon
    if (this.weaponMesh) {
      const L = this.limb('armN_l');
      const visible = !L.severed && !L.parentSevered;
      this.weaponMesh.visible = visible;
      if (visible) {
        const dir = this.aimDir || norm({ x: Math.sin(this.yaw), y: 0, z: Math.cos(this.yaw) });
        this.weaponMesh.position.set(P.wrN.x, P.wrN.y, P.wrN.z);
        this.weaponMesh.quaternion.copy(quatFromTo({ x: 0, y: 0, z: 1 }, dir));
      }
    }
  }

  /* ---------------- combat ---------------- */

  aimAt(targetPoint, dt) {
    const from = { x: this.pts.chest.x, y: this.pts.chest.y + 0.1, z: this.pts.chest.z };
    const d = sub(targetPoint, from);
    this.aimDir = norm(d);
    const yawTarget = Math.atan2(d.x, d.z);
    this.yaw = approachAngle(this.yaw, yawTarget, 7 * dt);
    this.aimTarget = add(from, mul(this.aimDir, 3));
  }

  fire(app) {
    if (this.attackCd > 0 || !this.alive || this.ragdoll || !this.armUsable()) return;
    const w = this.weapon;
    const L = this.limb('armN_l');
    if (L.severed || L.parentSevered) return;
    if (w.kind === 'melee') {
      this.swingActive = true;
      this.swingProgress = 0;
      this.swingDuration = w.swingTime;
      this.attackCd = w.swingTime * 0.95;
      this.swingHits = new Set();
      app.audio.slash();
      return;
    }
    const mag = this.mags[w.id] | 0;
    if (mag <= 0) {
      if (this.reloadT <= 0) this.reloadT = w.reloadTime;
      return;
    }
    this.mags[w.id] = mag - 1;
    this.attackCd = 60 / w.rpm;
    const muzzle = this.muzzlePos();
    const dir = this.aimDir || this.forward;
    app.fireBullet(muzzle, dir, w, this);
    if (this.mags[w.id] <= 0) this.reloadT = w.reloadTime;
  }

  muzzlePos() {
    const wr = this.pts.wrN;
    const dir = this.aimDir || norm({ x: Math.sin(this.yaw), y: 0, z: Math.cos(this.yaw) });
    const len = this.weapon.kind === 'gun' ? (this.weapon.len * 0.018 + 0.16) : 0.1;
    return { x: wr.x + dir.x * len, y: wr.y + dir.y * len, z: wr.z + dir.z * len };
  }

  // blade tip A/B in world space for melee sweep
  bladeSegment() {
    const wr = this.pts.wrN;
    const dir = this.aimDir || norm({ x: Math.sin(this.yaw), y: 0, z: Math.cos(this.yaw) });
    const lift = this.swingActive ? Math.sin(this.swingProgress * Math.PI) : 0.2;
    const a = { x: wr.x + dir.x * 0.12, y: wr.y + dir.y * 0.12 + lift * 0.1, z: wr.z + dir.z * 0.12 };
    const bl = this.weapon.bladeLen * 0.018;
    const b = { x: wr.x + dir.x * (0.12 + bl), y: wr.y + dir.y * (0.12 + bl) + lift * 0.1, z: wr.z + dir.z * (0.12 + bl) };
    return { a, b };
  }

  meleeHits(dt, app) {
    const w = this.weapon;
    const { a, b } = this.bladeSegment();
    const tt = this.swingProgress;
    if (tt < 0.25 || tt > 0.85) return;
    for (const other of app.humans) {
      if (other === this) continue;
      for (const l of other.limbs) {
        if (l.severed || l.parentSevered) continue;
        if (this.swingHits && this.swingHits.has(l.name + other.id)) continue;
        const pa = other.pts[l.a], pb = other.pts[l.b];
        const d = segSegDist(a, b, pa, pb);
        if (d < l.r + 0.06) {
          if (this.swingHits) this.swingHits.add(l.name + other.id);
          const dir = norm(sub(b, a));
          const hitPoint = { x: (pa.x + pb.x) / 2, y: (pa.y + pb.y) / 2, z: (pa.z + pb.z) / 2 };
          other.applyHit(l.name, {
            type: 'slash', dmg: w.dmg * 0.45, struct: w.structDmg * 0.55, cut: w.cut,
            bleedMul: w.bleedMul, knock: w.knock * 0.5, point: hitPoint, dir, attacker: this,
          });
          app.world.spray(hitPoint, dir, { count: 12, speed: 3.2, size: 0.03, spread: 0.9 });
          app.audio.cut();
        }
      }
    }
  }

  updateAI(dt, app) {
    if (!this.ai.enabled) {
      this.moving = false;
      if (!this.ragdoll) {
        const idleTarget = { x: this.pts.chest.x + Math.sin(this.yaw) * 2, y: this.pts.chest.y, z: this.pts.chest.z + Math.cos(this.yaw) * 2 };
        this.aimAt(idleTarget, dt);
      }
      return;
    }
    this.ai.retarget -= dt;
    if (!this.ai.target || !this.ai.target.alive || this.ai.retarget <= 0) {
      let best = null, bestD = Infinity;
      for (const h of app.humans) {
        if (h === this || !h.alive) continue;
        const d = Math.hypot(h.x - this.x, h.z - this.z);
        let score = d - (h === this.lastAttacker ? 8 : 0) + rand(0, 3);
        if (score < bestD) { bestD = score; best = h; }
      }
      this.ai.target = best;
      this.ai.retarget = rand(4, 9);
      this.ai.aimErr = rand(-0.12, 0.12);
    }
    const t = this.ai.target;
    if (!t) { this.moving = false; return; }

    const dx = t.x - this.x, dz = t.z - this.z;
    const d = Math.hypot(dx, dz);
    const wounded = this.health < 32 || this.blood < 1600;
    const melee = this.weapon.kind === 'melee';
    const want = melee ? 1.0 : 6.5;

    if (wounded && chance(dt * 0.7)) this.ai.state = 'flee';
    if (this.ai.state === 'flee' && !wounded) this.ai.state = 'fight';

    let moveX = 0, moveZ = 0;
    if (this.ai.state === 'flee') {
      moveX = -dx / (d || 1); moveZ = -dz / (d || 1);
    } else if (d > want * 1.1) {
      moveX = dx / (d || 1); moveZ = dz / (d || 1);
    } else if (d < want * 0.65 && !melee) {
      moveX = -dx / (d || 1); moveZ = -dz / (d || 1);
    }
    // simple separation from other people
    for (const h of app.humans) {
      if (h === this) continue;
      const ox = this.x - h.x, oz = this.z - h.z;
      const od = Math.hypot(ox, oz);
      if (od > 0.001 && od < 0.9) {
        moveX += (ox / od) * (0.9 - od) * 1.6;
        moveZ += (oz / od) * (0.9 - od) * 1.6;
      }
    }
    const mlen = Math.hypot(moveX, moveZ);
    this.moving = mlen > 0.05;
    if (this.moving) {
      const speed = (this.ai.state === 'flee' ? 2.6 : 1.5) * (this.legsUsable() ? 1 : 0);
      this.x += (moveX / mlen) * speed * dt;
      this.z += (moveZ / mlen) * speed * dt;
      this.x = clamp(this.x, -26, 26);
      this.z = clamp(this.z, -26, 26);
    }
    const chest = t.pts.chest;
    this.aimAt(chest, dt);

    if (this.ai.attackTimer > 0) { this.ai.attackTimer -= dt; return; }
    const aimOk = Math.abs(angleDiff(this.yaw, Math.atan2(dx, dz))) < 0.3;
    if (melee) {
      if (d < 1.35 && aimOk) { this.fire(app); this.ai.attackTimer = rand(0.2, 0.7); }
    } else if (aimOk && d < 24) {
      if (this.reloadT > 0) { this.mags[this.weapon.id] = this.weapon.magSize; return; }
      this.fire(app);
      this.ai.attackTimer = rand(0.05, this.weapon.auto ? 0.12 : 0.55);
    }
  }

  /* ---------------- damage ---------------- */

  applyHit(limbName, ev) {
    const app = this.app;
    const l = this.limb(limbName);
    if (!l) return;
    if (l.severed || l.parentSevered) return;
    this.hitFlash = 0.14;
    this.wake();
    if (ev.attacker && ev.attacker !== this) this.lastAttacker = ev.attacker;

    const mult = l.name === 'head' ? 2.6 : l.name === 'torso' ? 1 : 0.4;
    const dmg = ev.dmg * mult;
    this.pain = Math.min(1.5, this.pain + ev.dmg / 55);

    if (!this.alive) {
      // corpses still get dismembered
      this.damageTissue(l, ev, dmg);
      return;
    }

    this.health -= dmg;
    l.struct = clamp(l.struct + ev.struct * (ev.type === 'blunt' ? 1.3 : 1), 0, 1.4);
    if (ev.type === 'slash') l.cuts++;
    if (l.struct > 0.75 && (l.bone === 'arm' || l.bone === 'leg')) l.broken = true;

    const badness = ev.type === 'bullet' ? 1.4 : ev.type === 'slash' ? 1.2 : 0.85;
    const cut = ev.cut != null ? ev.cut : 0.3;
    const bleed = (7 + dmg * 2.0) * badness * cut * (ev.bleedMul || 1) * (l.name === 'head' ? 1.7 : l.name === 'torso' ? 1.2 : 1);
    const arterial = (l.bone === 'arm' || l.bone === 'leg' || l.name === 'torso') && ev.struct > 0.1 && chance(0.3 + ev.struct * 1.5);
    l.wounds.push({
      t: chance(0.5) ? 1 : -1,
      r: clamp(0.6 + dmg * 0.03, 0.7, 3),
      bleed, arterial, kind: ev.type, age: 0,
    });
    if (l.wounds.length > 8) l.wounds.shift();
    this.addWoundPatch(l.name);
    if (arterial) {
      l.arterial = true;
      app.world.spray(ev.point, ev.dir, { count: 26, speed: 5.5, size: 0.04, spread: 0.6 });
      app.audio.splat();
    } else {
      app.world.spray(ev.point, ev.dir, { count: Math.round(6 + dmg * 0.2), speed: 2.4, size: 0.028, spread: 0.7 });
    }

    // momentum at the impact point
    if (ev.dir) {
      const impulse = ev.type === 'bullet' ? ev.dmg * 0.03 : (ev.knock || 0) * 0.02;
      const at = ev.point || this.center();
      if (this.ragdoll) {
        this.applyImpulseAt(at, { x: ev.dir.x * impulse, y: ev.dir.y * impulse * 0.6, z: ev.dir.z * impulse });
      } else if ((ev.knock || 0) > 150 || (ev.type === 'blunt' && ev.dmg > 30)) {
        this.ragdollize({ x: ev.dir.x * 0.9, y: 0.4, z: ev.dir.z * 0.9 }, at);
      }
    }

    // skull destruction
    if (l.bone === 'skull' && (l.struct > 0.95 || (ev.type === 'bullet' && ev.dmg > 45 && chance(0.5)))) {
      this.headDestroyed(ev);
      return;
    }
    this.damageTissue(l, ev, dmg);

    if (!this.unconscious && (this.health < 16 || this.blood < 1400 || this.pain > 1.3)) {
      this.unconscious = true;
      if (!this.ragdoll) this.ragdollize(null);
      app.addLog(`${this.name}: потерял сознание`, 'warn');
    }
    if (this.health <= 0) {
      this.deathCause = this.blood < 2600 ? 'кровопотеря' : 'травмы, несовместимые с жизнью';
      this.die();
    }
  }

  damageTissue(l, ev, dmg) {
    // severing: tissue must be badly damaged first, then every further hit rolls
    if (['head', 'armN_u', 'armN_l', 'armF_u', 'armF_l', 'legN_u', 'legN_l', 'legF_u', 'legF_l'].includes(l.name)) {
      const struct = clamp(l.struct, 0, 1);
      const isLimb = l.bone === 'arm' || l.bone === 'leg';
      let sev = 0;
      if (isLimb) {
        sev = (struct - 0.5) * 1.6;
        sev += Math.min(0.24, (l.cuts || 0) * 0.03);
        sev += (ev.cut || 0) * (ev.type === 'slash' ? 0.2 : 0.04);
        if (ev.type === 'bullet') sev *= 0.4;
        if (ev.type === 'blunt') sev *= 0.4;
        if (l.broken) sev += 0.08;
        if (struct < 0.45) sev = 0;   // a limb never comes off from a fresh wound
      } else if (l.name === 'head') {
        sev = (struct - 0.85) * 1.1 + Math.min(0.2, (l.cuts || 0) * 0.025);
        if (ev.type === 'bullet' && ev.dmg > 50) sev += 0.1;
      }
      if (chance(clamp(sev, 0, 0.85))) this.sever(l, ev);
    }
    void dmg;
  }

  headDestroyed(ev) {
    const head = this.limb('head');
    if (!head.severed) this.sever(head, ev, true);
    this.deathCause = 'череп разрушен';
    this.die();
    const p = this.pts.head;
    const dir = ev.dir || { x: 0, y: 1, z: 0 };
    this.app.world.spray(p, dir, { count: 60, speed: 6, size: 0.05, spread: 1.2 });
    this.app.world.chunk(p, dir, { kind: 'meat', count: 10 });
    this.app.world.chunk(p, dir, { kind: 'bone', count: 8 });
    this.app.audio.sever();
  }

  sever(l, ev = {}, silent = false) {
    if (l.severed) return;
    const app = this.app;
    l.severed = true;
    l.struct = 1.3;
    const pa = this.pts[l.a], pb = this.pts[l.b];
    const dir = ev.dir || { x: 0, y: 1, z: 0 };

    // detached piece as its own two-point body
    const debris = {
      kind: l.name === 'head' ? 'head' : 'limb',
      l: l.name,
      a: { ...pa }, b: { ...pb },
      pa: { ...pa }, pb: { ...pb },
      va: { x: dir.x * 1.4, y: 1.2 + dir.y * 0.8, z: dir.z * 1.4 },
      vb: { x: dir.x * 1.4, y: 1.2 + dir.y * 0.8, z: dir.z * 1.4 },
      rest: Math.max(0.05, dist(pa, pb)),
      r: l.r, color: l.kind === 'skin' ? this.skin : l.kind === 'pants' ? this.pants : this.shirt,
      bleed: 12, life: 60,
      mesh: null,
    };
    if (l.name === 'head') {
      const g = new THREE.Group();
      const skull = new THREE.Mesh(new THREE.SphereGeometry(0.112, 14, 10), this.matSkin);
      skull.castShadow = true;
      const hair = new THREE.Mesh(new THREE.SphereGeometry(0.116, 14, 8, 0, TAU, 0, Math.PI * 0.62), this.matHair);
      g.add(skull, hair);
      debris.mesh = g;
    } else {
      const geo = new THREE.CylinderGeometry(1, 1, 1, 8);
      const mat = new THREE.MeshStandardMaterial({ color: debris.color, roughness: 0.85 });
      const m = new THREE.Mesh(geo, mat);
      m.castShadow = true;
      debris.mesh = m;
    }
    debris.mesh.position.set(pa.x, pa.y, pa.z);
    app.scene.add(debris.mesh);
    app.debris.push(debris);

    // stump bleeding + visible stump
    if (l.name !== 'head') this.addStump(l.name);
    l.stumpBleed = l.name === 'head' ? 80 : l.bone === 'leg' ? 70 : 45;
    l.arterial = true;
    for (const other of this.limbs) if (other.parent === l.name) other.parentSevered = true;

    // weapon drops
    if (l.name === 'armN_u' || l.name === 'armN_l') {
      if (this.weapon.id !== 'fists') {
        app.dropWeapon(this.weapon, this.pts.wrN, dir);
      }
      this.weapon = WEAPON_BY_ID.fists;
      this.attachWeapon();
    }

    app.world.spray(pa, dir, { count: 40, speed: 4.5, size: 0.04, spread: 0.9 });
    app.world.mist(pa, dir, 12, 1.1);
    app.world.chunk(pa, dir, { kind: 'meat', count: 6 });
    app.world.chunk(pa, dir, { kind: 'bone', count: 3 });
    app.audio.sever();
    app.addLog(`${this.name}: оторвано — ${l.label}`, 'bad');

    if (l.bone === 'leg') this.ragdollize({ x: dir.x * 1.5, y: 0.6, z: dir.z * 1.5 }, pa);
    if (l.bone === 'skull') {
      this.deathCause = 'обезглавлен';
      this.die();
    }
    if (!silent && this.alive && this.health <= 0) this.die();
  }

  die() {
    if (!this.alive) return;
    this.alive = false;
    this.unconscious = false;
    this.health = Math.max(0, this.health);
    this.deathCause = this.deathCause || 'неизвестно';
    this.ragdollize(null);
    this.swingActive = false;
    for (const l of this.limbs) l.arterial = false;
    this.app.addLog(`${this.name} погиб — ${this.deathCause || 'рана'}`, 'bad');
    this.app.world.decal(this.pts.pelvis.x, this.pts.pelvis.z, 0.55, 0.7);
    this.app.onDeath && this.app.onDeath(this);
  }

  /* ---------------- bleeding / shock ---------------- */

  updateBleeding(dt, app) {
    let total = 0;
    for (const l of this.limbs) {
      l.bleedRate = 0;
      if (l.parentSevered) continue;
      if (l.severed) {
        l.bleedRate = l.stumpBleed || 0;   // stump only: the piece is gone
      } else {
        for (const w of l.wounds) {
          w.age += dt;
          const clot = this.alive ? (w.kind === 'bullet' ? 0.03 : 0.012) : 0.6;
          w.bleed *= 1 - clot * dt;
          l.bleedRate += w.bleed;
        }
      }
      if (!this.alive) l.bleedRate *= 0.2;   // no heartbeat: the corpse barely drips
      if (l.bleedRate > 0.4) {
        total += l.bleedRate;
        if (this.blood <= 0) continue;
        const pa = this.pts[l.a], pb = this.pts[l.b];
        const t = l.wounds.length ? (l.wounds[l.wounds.length - 1].t > 0 ? 0.75 : 0.25) : 0.5;
        const p = l.severed
          ? { x: pa.x, y: pa.y, z: pa.z }
          : { x: lerp(pa.x, pb.x, t), y: lerp(pa.y, pb.y, t), z: lerp(pa.z, pb.z, t) };
        const arterial = l.arterial || l.severed;
        const rate = l.bleedRate * (arterial ? 2.2 : 1.2);
        const dir = arterial
          ? { x: rand(-1, 1), y: rand(0.5, 1.4), z: rand(-1, 1) }
          : { x: rand(-0.3, 0.3), y: -1, z: rand(-0.3, 0.3) };
        const n = Math.min(8, Math.ceil(rate * dt * 0.55));
        for (let i = 0; i < n; i++) {
          app.world.spray(p, dir, { count: 1, speed: arterial ? 3.4 : 1.2, size: 0.03, spread: 0.5 });
        }
      }
    }
    this.bleedTotal = total;
    this.blood = Math.max(0, this.blood - total * dt);
    if (this.alive) {
      if (this.blood < 3000) {
        const s = (3000 - this.blood) / 3000;
        this.health -= s * s * 20 * dt;
      }
      if (this.unconscious && this.blood > 2100 && this.health > 26 && this.bleedTotal < 3) {
        this.unconscious = false;
        app.addLog(`${this.name}: пришёл в себя`, 'good');
      }
      if (this.health <= 0) {
        this.deathCause = this.blood < 2600 ? 'кровопотеря' : 'травмы, несовместимые с жизнью';
        this.die();
      }
    }
  }

  /* ---------------- info for the inspector ---------------- */

  info() {
    return {
      name: this.name,
      alive: this.alive,
      unconscious: this.unconscious,
      ragdoll: this.ragdoll,
      deathCause: this.deathCause,
      health: this.health,
      blood: this.blood,
      bleed: this.bleedTotal,
      pain: this.pain,
      weapon: this.weapon,
      mag: this.mags[this.weapon.id],
      limbs: this.limbs.map((l) => ({
        name: l.label,
        struct: l.struct,
        severed: l.severed,
        parentSevered: l.parentSevered,
        broken: l.broken,
        wounds: l.wounds.length,
        arterial: l.arterial || l.severed,
        bleed: l.bleedRate,
      })),
      ai: this.ai.enabled,
      target: this.ai.target ? this.ai.target.name : null,
    };
  }

  dispose() {
    if (this.weaponMesh) this.group.remove(this.weaponMesh);
    this.app.scene.remove(this.group);
    this.group.traverse((o) => {
      if (o.geometry) o.geometry.dispose();
    });
  }
}

/* ---------------- helpers ---------------- */

const _up = new THREE.Vector3(0, 1, 0);
const _v1 = new THREE.Vector3();
const _v2 = new THREE.Vector3();
const _q = new THREE.Quaternion();

export function quatFromTo(from, to) {
  _v1.set(from.x, from.y, from.z).normalize();
  _v2.set(to.x, to.y, to.z).normalize();
  _q.setFromUnitVectors(_v1, _v2);
  return _q;
}

export function orientSegment(mesh, a, b, r1, r2) {
  const dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
  const length = Math.hypot(dx, dy, dz) || 0.001;
  mesh.position.set((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
  const q = new THREE.Quaternion().setFromUnitVectors(_up, new THREE.Vector3(dx / length, dy / length, dz / length));
  mesh.quaternion.copy(q);
  mesh.scale.set((r1 + r2) / 2, length, (r1 + r2) / 2);
}

export function solveIK3(root, target, l1, l2, hint) {
  const d = sub(target, root);
  let dl = Math.hypot(d.x, d.y, d.z);
  const maxD = (l1 + l2) * 0.98;
  const minD = 0.08;
  const clamped = clamp(dl, minD, maxD);
  const dir = dl > 1e-5 ? norm(d) : { x: 0, y: -1, z: 0 };
  const a = (l1 * l1 - l2 * l2 + clamped * clamped) / (2 * clamped);
  const h = Math.sqrt(Math.max(0, l1 * l1 - a * a));
  const base = add(root, mul(dir, a));
  let perp = sub(hint || { x: 0, y: -1, z: 0 }, mul(dir, dot(hint || { x: 0, y: -1, z: 0 }, dir)));
  const pl = Math.hypot(perp.x, perp.y, perp.z);
  if (pl < 1e-5) perp = { x: dir.z, y: 0, z: -dir.x };
  else perp = mul(perp, 1 / pl);
  const elbow = add(base, mul(perp, h));
  const hand = add(root, mul(dir, clamped));
  return elbow;
}

export function segSegDist(a1, a2, b1, b2) {
  // closest distance between segments: coarse grid, then local refinement
  const at = (t) => ({ x: lerp(a1.x, a2.x, t), y: lerp(a1.y, a2.y, t), z: lerp(a1.z, a2.z, t) });
  const bt = (s) => ({ x: lerp(b1.x, b2.x, s), y: lerp(b1.y, b2.y, s), z: lerp(b1.z, b2.z, s) });
  const gap = (t, s) => {
    const p = at(t), q = bt(s);
    return Math.hypot(p.x - q.x, p.y - q.y, p.z - q.z);
  };
  let best = Infinity, bt0 = 0, bs0 = 0;
  const N = 8;
  for (let i = 0; i <= N; i++) {
    for (let j = 0; j <= N; j++) {
      const d = gap(i / N, j / N);
      if (d < best) { best = d; bt0 = i / N; bs0 = j / N; }
    }
  }
  let step = 1 / N;
  for (let it = 0; it < 5; it++) {
    step *= 0.5;
    let improved = false;
    for (const dt of [-step, step]) {
      for (const ds of [-step, step]) {
        const t = clamp(bt0 + dt, 0, 1), s = clamp(bs0 + ds, 0, 1);
        const d = gap(t, s);
        if (d < best) { best = d; bt0 = t; bs0 = s; improved = true; }
      }
    }
    if (!improved) break;
  }
  return best;
}

export { NAMES, SKIN, SHIRT, PANTS, HAIR };
