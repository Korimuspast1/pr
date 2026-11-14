/* Map: an industrial city block with a yard in the middle.
   Built from boxes/cylinders with procedural textures, plus AABB colliders
   that the ragdolls and walking humans are pushed out of.

   Layout (top view, X right, Z down):
     road (z -28..-20) ─────────────────────────────
       north block of buildings
     sidewalk / fence / GATE
     ┌──── yard ────────────────────────┐
     │ warehouse   containers   shed    │  cross street (x 22..29)
     │ office      cars         garage  │
     └──────────────────────────────────┘
*/
import * as THREE from '../vendor/three.module.js';
import {
  asphaltTexture, concreteTexture, brickTexture, metalTexture, woodTexture, dirtTexture,
} from './textures.js';

const BOX = new THREE.BoxGeometry(1, 1, 1);
const CYL = new THREE.CylinderGeometry(0.5, 0.5, 1, 12);
const CYL8 = new THREE.CylinderGeometry(0.5, 0.5, 1, 8);
const PLANE = new THREE.PlaneGeometry(1, 1);

const t = (fn) => fn();

export function buildMap(scene) {
  const group = new THREE.Group();
  group.name = 'map';
  scene.add(group);

  const colliders = [];
  const lamps = [];
  const spawnPoints = [];
  const fires = [];

  /* ---------------- materials ---------------- */
  const matRoad = new THREE.MeshStandardMaterial({
    map: asphaltTexture(9), color: 0x9aa0a8, roughness: 0.94, metalness: 0.02,
  });
  const matWalk = new THREE.MeshStandardMaterial({
    map: concreteTexture(3), color: 0xa8a8a2, roughness: 0.95,
  });
  const matYard = new THREE.MeshStandardMaterial({
    map: dirtTexture(14), color: 0xa9a294, roughness: 0.98,
  });
  const matBrick = new THREE.MeshStandardMaterial({
    map: t(() => brickTexture(3)), color: 0xb8a098, roughness: 0.95,
  });
  const matConcrete = new THREE.MeshStandardMaterial({
    map: concreteTexture(2), color: 0x9c9c96, roughness: 0.93,
  });
  const matConcreteDark = new THREE.MeshStandardMaterial({
    map: concreteTexture(2), color: 0x6f716e, roughness: 0.95,
  });
  const matMetal = new THREE.MeshStandardMaterial({
    map: metalTexture(3), color: 0x8f9296, roughness: 0.68, metalness: 0.55,
  });
  const matRust = new THREE.MeshStandardMaterial({
    map: metalTexture(3), color: 0x9a6a4a, roughness: 0.85, metalness: 0.35,
  });
  const matWood = new THREE.MeshStandardMaterial({
    map: woodTexture(2), color: 0x9a7d5c, roughness: 0.92,
  });
  const matWoodDark = new THREE.MeshStandardMaterial({
    map: woodTexture(2), color: 0x6a5138, roughness: 0.95,
  });
  const matDarkMetal = new THREE.MeshStandardMaterial({ color: 0x2a2d31, roughness: 0.6, metalness: 0.65 });
  const matBlack = new THREE.MeshStandardMaterial({ color: 0x141518, roughness: 0.85 });
  const matRubber = new THREE.MeshStandardMaterial({ color: 0x141414, roughness: 0.95 });
  const matGlass = new THREE.MeshStandardMaterial({
    color: 0x121a22, roughness: 0.16, metalness: 0.55,
  });
  const matGlassLit = new THREE.MeshStandardMaterial({
    color: 0x2a2519, emissive: 0xffc078, emissiveIntensity: 0.85, roughness: 0.35,
  });
  const matGlassRed = new THREE.MeshStandardMaterial({
    color: 0x2a0e0e, emissive: 0xff5a3a, emissiveIntensity: 0.5, roughness: 0.3,
  });
  const matPaint = new THREE.MeshStandardMaterial({ color: 0xd8d4c2, roughness: 0.8 });
  const matPaintYellow = new THREE.MeshStandardMaterial({ color: 0xc9a83c, roughness: 0.8 });
  const matBlue = new THREE.MeshStandardMaterial({ color: 0x2f4a63, roughness: 0.7, metalness: 0.3 });
  const matRed = new THREE.MeshStandardMaterial({ color: 0x6c2b26, roughness: 0.75, metalness: 0.25 });
  const matGreen = new THREE.MeshStandardMaterial({ color: 0x2f4436, roughness: 0.8, metalness: 0.2 });
  const matWater = new THREE.MeshStandardMaterial({ color: 0x3a4146, roughness: 0.14, metalness: 0.5 });
  const matFire = new THREE.MeshStandardMaterial({
    color: 0x431200, emissive: 0xff7a1e, emissiveIntensity: 2.2, roughness: 0.9,
  });

  /* ---------------- primitives ---------------- */
  function addCollider(x, y, z, w, h, d, ry = 0) {
    const c = Math.abs(Math.cos(ry)), s = Math.abs(Math.sin(ry));
    const hw = (w * c + d * s) / 2;
    const hd = (w * s + d * c) / 2;
    colliders.push({
      x0: x - hw, x1: x + hw, z0: z - hd, z1: z + hd,
      y0: y - h / 2, y1: y + h / 2,
    });
  }

  // merge hundreds of small bars (fence mesh) into a single mesh
  function bakeBars(bars, mat) {
    if (!bars.length) return null;
    const base = new THREE.BoxGeometry(1, 1, 1);
    const bp = base.attributes.position, bn = base.attributes.normal;
    const bu = base.attributes.uv, bi = base.index;
    const vc = bp.count, ic = bi.count;
    const P = new Float32Array(vc * 3 * bars.length);
    const N = new Float32Array(vc * 3 * bars.length);
    const U = new Float32Array(vc * 2 * bars.length);
    const I = new Uint32Array(ic * bars.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), e = new THREE.Euler();
    const sc = new THREE.Vector3(), tr = new THREE.Vector3(), v = new THREE.Vector3(), nm = new THREE.Matrix3();
    bars.forEach((b, i) => {
      e.set(0, b.ry || 0, 0);
      q.setFromEuler(e);
      sc.set(b.w, b.h, b.d);
      tr.set(b.x, b.y, b.z);
      m.compose(tr, q, sc);
      nm.getNormalMatrix(m);
      for (let k = 0; k < vc; k++) {
        v.fromBufferAttribute(bp, k).applyMatrix4(m);
        const o3 = (i * vc + k) * 3;
        P[o3] = v.x; P[o3 + 1] = v.y; P[o3 + 2] = v.z;
        v.fromBufferAttribute(bn, k).applyMatrix3(nm).normalize();
        N[o3] = v.x; N[o3 + 1] = v.y; N[o3 + 2] = v.z;
        const o2 = (i * vc + k) * 2;
        U[o2] = bu.getX(k); U[o2 + 1] = bu.getY(k);
      }
      for (let k = 0; k < ic; k++) I[i * ic + k] = bi.getX(k) + i * vc;
    });
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(P, 3));
    geo.setAttribute('normal', new THREE.BufferAttribute(N, 3));
    geo.setAttribute('uv', new THREE.BufferAttribute(U, 2));
    geo.setIndex(new THREE.BufferAttribute(I, 1));
    geo.computeBoundingSphere();
    const mesh = new THREE.Mesh(geo, mat);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  // y is the vertical centre
  function box(w, h, d, x, y, z, mat, o = {}) {
    const m = new THREE.Mesh(BOX, mat);
    m.scale.set(w, h, d);
    m.position.set(x, y, z);
    if (o.ry) m.rotation.y = o.ry;
    if (o.rx) m.rotation.x = o.rx;
    if (o.rz) m.rotation.z = o.rz;
    m.castShadow = o.cast !== false;
    m.receiveShadow = o.recv !== false;
    (o.parent || group).add(m);
    if (o.collide !== false) addCollider(x, y, z, w, h, d, o.ry || 0);
    return m;
  }
  function cyl(r, h, x, y, z, mat, o = {}) {
    const m = new THREE.Mesh(o.lowPoly ? CYL8 : CYL, mat);
    m.scale.set(r * 2, h, r * 2);
    m.position.set(x, y, z);
    if (o.rx) m.rotation.x = o.rx;
    if (o.rz) m.rotation.z = o.rz;
    if (o.ry) m.rotation.y = o.ry;
    m.castShadow = o.cast !== false;
    m.receiveShadow = o.recv !== false;
    (o.parent || group).add(m);
    if (o.collide) addCollider(x, y, z, r * 2, h, r * 2);
    return m;
  }
  // flat decal-like quad (no collision, no shadow)
  function quad(w, d, x, y, z, mat, ry = 0) {
    const m = new THREE.Mesh(PLANE, mat);
    m.scale.set(w, d, 1);
    m.rotation.x = -Math.PI / 2;
    m.rotation.z = ry;
    m.position.set(x, y, z);
    m.receiveShadow = true;
    group.add(m);
    return m;
  }

  /* ---------------- ground: roads, sidewalks, yard ---------------- */
  const groundYard = new THREE.Mesh(new THREE.PlaneGeometry(210, 210), matYard);
  groundYard.rotation.x = -Math.PI / 2;
  groundYard.position.y = -0.02;
  groundYard.receiveShadow = true;
  group.add(groundYard);

  // main street (east-west) and cross street (north-south)
  const roadA = new THREE.Mesh(new THREE.PlaneGeometry(92, 8), matRoad);
  roadA.rotation.x = -Math.PI / 2;
  roadA.position.set(-1, 0.005, -24);
  roadA.receiveShadow = true;
  group.add(roadA);

  const roadB = new THREE.Mesh(new THREE.PlaneGeometry(7, 92), matRoad);
  roadB.rotation.x = -Math.PI / 2;
  roadB.position.set(25.5, 0.006, -2);
  roadB.receiveShadow = true;
  group.add(roadB);

  // sidewalks (raised, no collision so feet are never trapped)
  const walk = (w, d, x, z) => {
    const m = box(w, 0.12, d, x, 0.06, z, matWalk, { collide: false, cast: false });
    return m;
  };
  walk(92, 1.9, -1, -29.95);
  walk(92, 1.9, -1, -18.05);
  walk(1.9, 92, 21.05, -2);
  walk(1.9, 92, 29.95, -2);

  // curbs
  box(92, 0.14, 0.18, -1, 0.07, -28.9, matConcreteDark, { collide: false, cast: false });
  box(92, 0.14, 0.18, -1, 0.07, -19.1, matConcreteDark, { collide: false, cast: false });
  box(0.18, 0.14, 92, 22.0, 0.07, -2, matConcreteDark, { collide: false, cast: false });
  box(0.18, 0.14, 92, 29.0, 0.07, -2, matConcreteDark, { collide: false, cast: false });

  // lane markings
  for (let x = -42; x < 44; x += 5.2) {
    quad(2.4, 0.16, x, 0.02, -24 + 0.0, matPaint, 0);
  }
  for (let z = -44; z < 44; z += 5.2) {
    quad(0.16, 2.4, 25.5, 0.022, z, matPaint, 0);
  }
  // crosswalk at the junction
  for (let i = 0; i < 6; i++) {
    quad(0.55, 6.4, 20.2 + i * 0.95, 0.024, -24, matPaint, 0);
  }
  quad(6.4, 0.4, 25.5, 0.024, -19.4, matPaint, 0);

  // worn patches / slab repairs so the yard is not one flat colour
  const matDirt = new THREE.MeshStandardMaterial({ map: dirtTexture(3), color: 0x8b8375, roughness: 0.99 });
  const matPatch = new THREE.MeshStandardMaterial({ map: concreteTexture(2), color: 0x8e8c85, roughness: 0.96 });
  const matPatch2 = new THREE.MeshStandardMaterial({ map: concreteTexture(2), color: 0x7b786f, roughness: 0.97 });
  for (const [px, pz, pw, pd, pr, mat] of [
    [-6.5, 6.0, 9.0, 7.0, 0.12, matDirt],
    [7.0, -4.5, 7.0, 6.0, -0.2, matDirt],
    [2.0, 17.5, 8.0, 6.0, 0.3, matDirt],
    [-13.0, -11.0, 6.0, 5.0, 0.05, matDirt],
    [-2.5, -3.0, 5.0, 3.5, 0.02, matPatch],
    [9.5, 8.0, 4.0, 3.0, -0.1, matPatch],
    [-9.0, 15.0, 4.5, 3.0, 0.15, matPatch2],
    [13.0, -9.0, 3.5, 2.6, 0.25, matPatch2],
  ]) {
    quad(pw, pd, px, 0.008, pz, mat, pr);
  }

  // puddles: shallow wet patches, elliptical so they don't read as holes
  for (const [px, pz, pr, ex, ez] of [
    [-2, 12.5, 1.05, 1.5, 0.8], [9.5, 3.2, 0.72, 1.2, 1.0],
    [-17.5, 8.9, 0.62, 0.9, 1.6], [3.5, -15.5, 0.8, 1.3, 0.9],
  ]) {
    const p = new THREE.Mesh(new THREE.CircleGeometry(pr, 20), matWater);
    p.rotation.x = -Math.PI / 2;
    p.scale.set(ex, ez, 1);
    p.position.set(px, 0.016, pz);
    p.receiveShadow = true;
    group.add(p);
  }

  /* ---------------- walls with openings ---------------- */
  function wall(axis, a0, a1, fixed, h, thick, mat, openings = [], yBase = 0) {
    const ops = [...openings].sort((p, q) => p.from - q.from);
    let cur = a0;
    const seg = (s, e, yb = yBase, yt = yBase + h) => {
      const len = e - s;
      const hh = yt - yb;
      if (len <= 0.02 || hh <= 0.02) return;
      const c = (yb + yt) / 2;
      if (axis === 'x') box(len, hh, thick, (s + e) / 2, c, fixed, mat);
      else box(thick, hh, len, fixed, c, (s + e) / 2, mat);
    };
    for (const o of ops) {
      if (o.from > cur) seg(cur, o.from);
      const top = o.top != null ? o.top : 2.2;
      if (top < h) seg(o.from, o.to, yBase + top, yBase + h);
      cur = Math.max(cur, o.to);
    }
    if (cur < a1) seg(cur, a1);
  }

  function windows(side, x0, z0, x1, z1, rows = [{ y: 1.7, count: 5, w: 1.1, h: 1.4 }], lit = 0.45) {
    for (const row of rows) {
      const { y, count, w, h } = row;
      const len = side === 'n' || side === 's' ? (x1 - x0) : (z1 - z0);
      const step = len / (count + 1);
      for (let i = 1; i <= count; i++) {
        const off = i * step;
        const wx = side === 'n' || side === 's' ? x0 + off : (side === 'w' ? x0 : x1);
        const wz = side === 'e' || side === 'w' ? z0 + off : (side === 'n' ? z0 : z1);
        const isLit = Math.random() < lit;
        const mat = isLit ? matGlassLit : matGlass;
        const flip = side === 'n' ? -1 : side === 's' ? 1 : side === 'w' ? -1 : 1;
        const horiz = side === 'n' || side === 's';
        if (horiz) {
          box(w + 0.16, h + 0.16, 0.05, wx, y, wz + flip * 0.03, matConcreteDark, { collide: false, cast: false });
          box(w, h, 0.07, wx, y, wz + flip * 0.045, mat, { collide: false, cast: false });
          box(0.06, h, 0.09, wx, y, wz + flip * 0.05, matDarkMetal, { collide: false, cast: false });
        } else {
          box(0.05, h + 0.16, w + 0.16, wx + flip * 0.03, y, wz, matConcreteDark, { collide: false, cast: false });
          box(0.07, h, w, wx + flip * 0.045, y, wz, mat, { collide: false, cast: false });
          box(0.09, h, 0.06, wx + flip * 0.05, y, wz, matDarkMetal, { collide: false, cast: false });
        }
      }
    }
  }

  /* ---------------- buildings ---------------- */
  // North block: three storeys, faces the street (south side at z = -31)
  {
    const x0 = -34, x1 = 16, z0 = -41, z1 = -31, h = 10.4;
    wall('x', x0, x1, z1, h, 0.4, matBrick, [{ from: -20, to: -17, top: 2.6 }, { from: 2, to: 5, top: 2.6 }]);
    wall('x', x0, x1, z0, h, 0.4, matBrick);
    wall('z', z0, z1, x0, h, 0.4, matBrick);
    wall('z', z0, z1, x1, h, 0.4, matBrick);
    box(x1 - x0 + 0.8, 0.35, z1 - z0 + 0.8, (x0 + x1) / 2, h + 0.17, (z0 + z1) / 2, matConcreteDark);
    box(x1 - x0, 0.5, 1.2, (x0 + x1) / 2, h + 0.6, z1 - 0.5, matConcreteDark);
    windows('s', x0, z1, x1, z1, [
      { y: 2.0, count: 9, w: 1.2, h: 1.5 },
      { y: 5.2, count: 9, w: 1.2, h: 1.5 },
      { y: 8.3, count: 9, w: 1.0, h: 1.3 },
    ], 0.4);
    // fire escape
    for (let f = 0; f < 2; f++) {
      const y = 3.6 + f * 3.2;
      box(3.2, 0.1, 1.1, -8, y, z1 + 0.6, matDarkMetal, { collide: false });
      box(3.2, 0.06, 0.06, -8, y + 1.0, z1 + 1.1, matDarkMetal, { collide: false, cast: false });
      box(0.08, 1.0, 0.08, -9.5, y + 0.5, z1 + 1.1, matDarkMetal, { collide: false, cast: false });
      box(0.08, 1.0, 0.08, -6.5, y + 0.5, z1 + 1.1, matDarkMetal, { collide: false, cast: false });
    }
    // rooftop clutter
    box(2.2, 1.4, 1.6, 6, h + 1.05, -36, matMetal, { collide: false });
    cyl(0.9, 2.2, -14, h + 1.4, -38, matRust, { collide: false });
    cyl(0.9, 2.2, 12, h + 1.4, -35, matRust, { collide: false });
  }

  // East block across the cross street
  {
    const x0 = 32, x1 = 45, z0 = -17, z1 = 12, h = 7.6;
    wall('z', z0, z1, x0, h, 0.4, matBrick);
    wall('z', z0, z1, x1, h, 0.4, matBrick);
    wall('x', x0, x1, z0, h, 0.4, matBrick);
    wall('x', x0, x1, z1, h, 0.4, matBrick);
    box(x1 - x0 + 0.6, 0.3, z1 - z0 + 0.6, (x0 + x1) / 2, h + 0.15, (z0 + z1) / 2, matConcreteDark);
    windows('w', x0, z0, x0, z1, [
      { y: 1.9, count: 6, w: 1.1, h: 1.5 },
      { y: 5.0, count: 6, w: 1.1, h: 1.5 },
    ], 0.35);
  }

  // Warehouse on the west edge of the yard
  {
    const x0 = -34, x1 = -21, z0 = -16, z1 = 6, h = 7.4;
    wall('z', z0, z1, x0, h, 0.35, matMetal);
    wall('x', x0, x1, z0, h, 0.35, matMetal);
    wall('x', x0, x1, z1, h, 0.35, matMetal);
    wall('z', z0, z1, x1, h, 0.35, matMetal, [{ from: -4.5, to: 1.5, top: 5.0 }]);
    box(x1 - x0 + 0.5, 0.3, z1 - z0 + 0.5, (x0 + x1) / 2, h + 0.15, (z0 + z1) / 2, matRust);
    // roller door, half open
    box(5.8, 3.6, 0.18, x1 + 0.1, 4.0, -1.5, matRust, { collide: false, cast: false });
    box(0.2, 5.0, 0.2, x1 + 0.16, 2.5, -4.5, matDarkMetal, { collide: false });
    box(0.2, 5.0, 0.2, x1 + 0.16, 2.5, 1.5, matDarkMetal, { collide: false });
    box(0.3, 0.25, 6.2, x1 + 0.2, 5.1, -1.5, matDarkMetal, { collide: false });
    windows('e', x1, z0, x1, z1, [{ y: 6.3, count: 3, w: 1.0, h: 0.8 }], 0.5);
    // side lean-to and vents
    box(3.2, 2.6, 4.0, -19.4, 1.3, 8.2, matMetal);
    cyl(0.35, 1.1, -22.5, h + 0.55, -13, matDarkMetal, { collide: false });
    cyl(0.35, 1.1, -26.5, h + 0.55, -13, matDarkMetal, { collide: false });
    // stacked drums beside the wall
    for (let i = 0; i < 4; i++) {
      cyl(0.3, 0.9, -20.3, 0.45, 2.4 + i * 0.72, i % 2 ? matRust : matBlue, { lowPoly: true, collide: true });
    }
  }

  // Office block, south-west
  {
    const x0 = -34, x1 = -21, z0 = 13, z1 = 27, h = 6.9;
    wall('z', z0, z1, x0, h, 0.4, matBrick);
    wall('x', x0, x1, z0, h, 0.4, matBrick);
    wall('x', x0, x1, z1, h, 0.4, matBrick);
    wall('z', z0, z1, x1, h, 0.4, matBrick, [{ from: 19.4, to: 21.4, top: 2.4 }]);
    box(x1 - x0 + 0.6, 0.3, z1 - z0 + 0.6, (x0 + x1) / 2, h + 0.15, (z0 + z1) / 2, matConcreteDark);
    // canopy over the entrance
    box(3.2, 0.16, 2.4, x1 + 1.3, 3.0, 20.4, matConcreteDark, { collide: false });
    box(0.14, 3.0, 0.14, x1 + 2.8, 1.5, 19.2, matDarkMetal, { collide: false });
    box(0.14, 3.0, 0.14, x1 + 2.8, 1.5, 21.6, matDarkMetal, { collide: false });
    windows('e', x1, z0, x1, z1, [
      { y: 1.9, count: 3, w: 1.3, h: 1.5 },
      { y: 4.8, count: 3, w: 1.3, h: 1.5 },
    ], 0.6);
    windows('n', x0, z0, x1, z0, [{ y: 4.8, count: 3, w: 1.1, h: 1.3 }], 0.3);
    // sign
    box(2.6, 0.7, 0.12, x1 + 0.3, 3.8, 20.4, matPaint, { collide: false, cast: false });
  }

  // Garage row along the south edge
  {
    const x0 = -14, x1 = 14, z0 = 28, z1 = 35, h = 3.5;
    wall('x', x0, x1, z0, h, 0.35, matConcrete, [
      { from: -12.4, to: -9.2, top: 2.7 }, { from: -7.2, to: -4.0, top: 2.7 },
      { from: -2.0, to: 1.2, top: 2.7 }, { from: 3.2, to: 6.4, top: 2.7 },
      { from: 8.4, to: 11.6, top: 2.7 },
    ]);
    wall('x', x0, x1, z1, h, 0.35, matConcrete);
    wall('z', z0, z1, x0, h, 0.35, matConcrete);
    wall('z', z0, z1, x1, h, 0.35, matConcrete);
    box(x1 - x0 + 0.6, 0.28, z1 - z0 + 0.6, (x0 + x1) / 2, h + 0.14, (z0 + z1) / 2, matConcreteDark);
    // closed garage doors
    for (let i = 0; i < 5; i++) {
      const cx = -10.8 + i * 5.2;
      box(3.0, 2.6, 0.12, cx, 1.35, z0 - 0.06, matMetal, { collide: false, cast: false });
      for (let s = 0; s < 5; s++) box(3.0, 0.05, 0.14, cx, 0.35 + s * 0.5, z0 - 0.07, matDarkMetal, { collide: false, cast: false });
    }
    // wall graffiti-ish paint patches
    for (let i = 0; i < 4; i++) {
      box(1.4, 0.5, 0.05, -10 + i * 6, 2.2, z0 - 0.02, i % 2 ? matPaintYellow : matRed, { collide: false, cast: false });
    }
  }

  // Small wooden shed near the road gate
  {
    const x0 = 8, x1 = 15, z0 = -17, z1 = -11, h = 3.2;
    wall('x', x0, x1, z0, h, 0.25, matWoodDark, [{ from: 12.2, to: 13.4, top: 2.1 }]);
    wall('x', x0, x1, z1, h, 0.25, matWoodDark);
    wall('z', z0, z1, x0, h, 0.25, matWoodDark);
    wall('z', z0, z1, x1, h, 0.25, matWoodDark);
    // flat roof with an overhang + door frame
    box(x1 - x0 + 0.9, 0.24, z1 - z0 + 0.9, (x0 + x1) / 2, h + 0.12, (z0 + z1) / 2, matRust, { collide: false });
    box(0.14, 2.1, 0.14, 12.06, 1.05, z0 - 0.02, matWoodDark, { collide: false });
    box(0.14, 2.1, 0.14, 13.54, 1.05, z0 - 0.02, matWoodDark, { collide: false });
    box(1.7, 0.14, 0.14, 12.8, 2.17, z0 - 0.02, matWoodDark, { collide: false });
  }

  /* ---------------- yard props ---------------- */
  // shipping containers (one stacked)
  function container(cx, cz, ry, color, y = 0) {
    const w = 12, hgt = 2.62, d = 2.44;
    const mat = new THREE.MeshStandardMaterial({
      map: metalTexture(2), color, roughness: 0.72, metalness: 0.45,
    });
    box(w, hgt, d, cx, y + hgt / 2, cz, mat, { ry });
    // door end
    const cos = Math.cos(ry), sin = Math.sin(ry);
    box(0.12, hgt * 0.96, d * 0.9, cx + cos * (w / 2 + 0.06), y + hgt / 2, cz - sin * (w / 2 + 0.06), matDarkMetal, { ry, collide: false });
    for (let i = 0; i < 4; i++) {
      const off = -w / 2 + 1.2 + i * 3.2;
      box(0.1, hgt, 0.12, cx + cos * off, y + hgt / 2, cz - sin * off, matDarkMetal, { ry, collide: false, cast: false });
      box(0.1, hgt, 0.12, cx + cos * off, y + hgt / 2, cz - sin * off + d * 0.5, matDarkMetal, { ry, collide: false, cast: false });
    }
    // corner castings
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
      box(0.28, 0.16, 0.28, cx + cos * sx * (w / 2 - 0.1) + sin * sz * (d / 2 - 0.1), y + hgt - 0.08,
        cz - sin * sx * (w / 2 - 0.1) + cos * sz * (d / 2 - 0.1), matDarkMetal, { collide: false, cast: false });
    }
  }
  container(-9, 4.6, 0.06, 0x8a4a35);
  container(-9, 7.3, -0.03, 0x2f5866, 2.62);
  container(-3.5, 21.6, 0.05, 0x4a5a3c);

  // truck
  function truck(cx, cz, ry) {
    const cos = Math.cos(ry), sin = Math.sin(ry);
    const at = (along, side = 0) => [cx + cos * along + sin * side, cz - sin * along + cos * side];
    let p = at(2.6);
    box(2.5, 2.5, 2.1, p[0], 1.55, p[1], matBlue, { ry });
    p = at(2.9);
    box(2.3, 1.1, 0.12, p[0] + sin * 0.9, 2.35, p[1] + cos * 0.9, matGlass, { ry, collide: false, cast: false });
    p = at(-1.6);
    box(2.5, 2.7, 7.0, p[0], 1.9, p[1], matRust, { ry });
    p = at(2.2);
    box(2.6, 0.5, 0.3, p[0] - sin * 1.0, 0.65, p[1] - cos * 1.0, matDarkMetal, { ry, collide: false });
    box(2.6, 0.5, 0.3, p[0] + sin * 1.0, 0.65, p[1] + cos * 1.0, matDarkMetal, { ry, collide: false });
    for (const [along, side] of [[2.6, 1.1], [2.6, -1.1], [-2.5, 1.1], [-2.5, -1.1], [-4.8, 1.1], [-4.8, -1.1]]) {
      const q = at(along, side);
      const w = new THREE.Mesh(CYL, matRubber);
      w.scale.set(0.55, 1.05, 1.05);
      w.rotation.z = Math.PI / 2;
      w.rotation.y = ry;
      w.position.set(q[0], 0.42, q[1]);
      w.castShadow = true;
      group.add(w);
    }
    // cab lights
    const lp = at(3.4);
    box(0.35, 0.18, 0.1, lp[0] + sin * 0.75, 0.95, lp[1] + cos * 0.75, matGlassLit, { ry, collide: false, cast: false });
    box(0.35, 0.18, 0.1, lp[0] - sin * 0.75, 0.95, lp[1] - cos * 0.75, matGlassLit, { ry, collide: false, cast: false });
    void cos; void sin;
  }
  truck(-17.2, 13.5, Math.PI / 2 + 0.12);

  // cars
  function car(cx, cz, ry, opts = {}) {
    const bodyMat = opts.burned ? matRust : (opts.color || matRed);
    box(1.86, 0.62, 4.35, cx, 0.62, cz, bodyMat, { ry });
    box(1.72, 0.52, 2.25, cx, 1.18, cz, bodyMat, { ry });
    const cos = Math.cos(ry), sin = Math.sin(ry);
    // windows
    const off = 1.02;
    box(1.5, 0.4, 0.08, cx + sin * off, 1.24, cz + cos * off, matGlass, { ry, collide: false, cast: false });
    box(1.5, 0.4, 0.08, cx - sin * off, 1.24, cz - cos * off, matGlass, { ry, collide: false, cast: false });
    box(0.08, 0.4, 1.7, cx + cos * 1.1, 1.24, cz - sin * 1.1, matGlass, { ry, collide: false, cast: false });
    box(0.08, 0.4, 1.7, cx - cos * 1.1, 1.24, cz + sin * 1.1, matGlass, { ry, collide: false, cast: false });
    // wheels
    for (const [a, s] of [[1.35, 0.88], [1.35, -0.88], [-1.4, 0.88], [-1.4, -0.88]]) {
      const wx = cx + cos * a + sin * s;
      const wz = cz - sin * a + cos * s;
      const w = new THREE.Mesh(CYL, matRubber);
      w.scale.set(0.62, 0.24, 0.62);
      w.rotation.z = Math.PI / 2;
      w.rotation.y = ry;
      w.position.set(wx, 0.33, wz);
      w.castShadow = true;
      group.add(w);
    }
    // lights
    const hl = cx + cos * 2.16;
    const hz = cz - sin * 2.16;
    if (!opts.burned) {
      box(0.34, 0.16, 0.08, hl + sin * 0.6, 0.72, hz + cos * 0.6, matGlassLit, { ry, collide: false, cast: false });
      box(0.34, 0.16, 0.08, hl - sin * 0.6, 0.72, hz - cos * 0.6, matGlassLit, { ry, collide: false, cast: false });
    }
    const tr = cx - cos * 2.16, tz = cz + sin * 2.16;
    box(0.3, 0.14, 0.07, tr + sin * 0.62, 0.78, tz + cos * 0.62, matGlassRed, { ry, collide: false, cast: false });
    box(0.3, 0.14, 0.07, tr - sin * 0.62, 0.78, tz - cos * 0.62, matGlassRed, { ry, collide: false, cast: false });
    if (opts.burned) {
      box(1.6, 0.35, 2.0, cx, 1.24, cz, matBlack, { ry, collide: false });
    }
  }
  car(6.2, -0.6, -0.5, { burned: true });
  car(-9.6, 17.6, 0.42, { color: matGreen });
  car(13.4, 9.6, 1.9, { color: matBlue });

  // jersey barriers
  function barrier(cx, cz, ry) {
    box(2.0, 0.34, 0.6, cx, 0.17, cz, matConcrete, { ry });
    box(2.0, 0.62, 0.32, cx, 0.62, cz, matConcrete, { ry });
  }
  for (let i = 0; i < 4; i++) barrier(0.5 + i * 2.1, -4.6, 0.02);
  for (let i = 0; i < 3; i++) barrier(12.2, 3.4 + i * 2.1, Math.PI / 2 + 0.03);
  barrier(-13.5, -3.2, 0.4);
  for (let i = 0; i < 3; i++) barrier(-6.4 + i * 2.1, -9.6, 0.02);
  barrier(-16.4, 3.2, Math.PI / 2);

  // crates and pallets
  function crate(cx, cy, cz, s, ry = 0) {
    box(s, s * 0.8, s, cx, cy + s * 0.4, cz, matWood, { ry });
    box(s * 1.01, 0.06, 0.08, cx, cy + s * 0.55, cz + s * 0.5, matWoodDark, { ry, collide: false, cast: false });
  }
  crate(-18.4, 0, -12.6, 1.1, 0.1);
  crate(-17.2, 0, -12.2, 0.9, -0.2);
  crate(-18.1, 0.9, -12.5, 0.8, 0.3);
  crate(11.6, 0, 11.8, 1.0, 0.25);
  crate(12.7, 0, 12.4, 0.8, -0.1);
  crate(-3.2, 0, 23.2, 1.2, 0.15);
  crate(15.8, 0, 19.6, 0.9, 0.5);

  // barrels (some burning)
  function barrel(cx, cz, mat, burning = false) {
    cyl(0.3, 0.9, cx, 0.45, cz, mat, { lowPoly: true, collide: true });
    cyl(0.32, 0.06, cx, 0.86, cz, matDarkMetal, { collide: false, cast: false });
    cyl(0.32, 0.06, cx, 0.18, cz, matDarkMetal, { collide: false, cast: false });
    if (burning) {
      const f = new THREE.Mesh(CYL, matFire);
      f.scale.set(0.5, 0.22, 0.5);
      f.position.set(cx, 0.95, cz);
      group.add(f);
      const light = new THREE.PointLight(0xff7a2a, 16, 11, 2);
      light.position.set(cx, 1.3, cz);
      scene.add(light);
      fires.push({ light, base: 16, x: cx, z: cz, phase: Math.random() * 10 });
    }
  }
  barrel(-16.2, -6.4, matRed, true);
  barrel(-15.4, -7.2, matRust);
  barrel(-17.1, -7.0, matBlue);
  barrel(14.6, 1.8, matRust, true);
  barrel(15.4, 2.6, matBlue);
  barrel(-19.8, 18.4, matGreen);

  // dumpster
  box(2.3, 1.35, 1.35, 16.8, 0.75, 22.4, matGreen, { ry: 0.3 });
  box(2.35, 0.12, 1.4, 16.8, 1.46, 22.4, matDarkMetal, { ry: 0.3, collide: false });

  // tire piles
  for (let i = 0; i < 5; i++) {
    const tire = new THREE.Mesh(new THREE.TorusGeometry(0.32, 0.13, 6, 12), matRubber);
    tire.rotation.x = Math.PI / 2;
    tire.position.set(17.6 + Math.sin(i) * 0.1, 0.14 + i * 0.17, 15.4 + Math.cos(i) * 0.1);
    tire.castShadow = true;
    group.add(tire);
  }
  addCollider(17.6, 0.45, 15.4, 0.9, 0.9, 0.9);

  // pipes near the warehouse
  for (let i = 0; i < 3; i++) {
    const p = new THREE.Mesh(CYL, matRust);
    p.scale.set(0.34, 5.2, 0.34);
    p.rotation.z = Math.PI / 2;
    p.position.set(-13.4, 0.34 + i * 0.02, -10.4 + i * 0.7);
    p.castShadow = true;
    group.add(p);
  }
  addCollider(-13.4, 0.35, -9.7, 5.2, 0.7, 2.0);

  /* ---------------- fences ---------------- */
  const barBatch = [];
  function fence(x0, z0, x1, z1, h = 2.4, gateAt = null) {
    const dx = x1 - x0, dz = z1 - z0;
    const len = Math.hypot(dx, dz);
    const ux = dx / len, uz = dz / len;
    const ry = Math.atan2(dx, dz);
    const steps = Math.max(1, Math.round(len / 3));
    for (let i = 0; i <= steps; i++) {
      const d = (i / steps) * len;
      const px = x0 + ux * d, pz = z0 + uz * d;
      if (gateAt && d > gateAt[0] && d < gateAt[1]) continue;
      box(0.09, h, 0.09, px, h / 2, pz, matDarkMetal, { collide: false });
    }
    // panel
    const segs = [];
    let start = 0;
    if (gateAt) { segs.push([start, gateAt[0]], [gateAt[1], len]); } else segs.push([0, len]);
    for (const [a, b] of segs) {
      if (b - a < 0.2) continue;
      const segLen = b - a;
      const cx = x0 + ux * (a + b) / 2;
      const cz = z0 + uz * (a + b) / 2;
      const at = (d) => [x0 + ux * d, z0 + uz * d];
      // top + bottom rails
      for (const y of [0.16, h - 0.08]) {
        box(0.05, 0.05, segLen, cx, y, cz, matDarkMetal, { ry, collide: false, cast: false });
      }
      // horizontal wires
      for (let y = 0.55; y < h - 0.2; y += 0.5) {
        barBatch.push({ w: 0.02, h: 0.02, d: segLen, x: cx, y, z: cz, ry });
      }
      // vertical wires
      const n = Math.max(2, Math.floor(segLen / 0.26));
      for (let i = 1; i < n; i++) {
        const [wx, wz] = at(a + (i / n) * segLen);
        barBatch.push({ w: 0.02, h: h - 0.2, d: 0.02, x: wx, y: (h - 0.2) / 2 + 0.1, z: wz });
      }
      addCollider(cx, h / 2, cz, 0.14, h, segLen, ry);
    }
  }
  fence(-20, -18.1, 20, -18.1, 2.5, [19, 24]);          // gate to the street
  fence(20.2, -18.1, 20.2, 26, 2.5, [30, 34]);          // gate to the cross street
  fence(-20.6, 7.4, -20.6, 12.6, 2.2);                  // between warehouse and office
  fence(-14, 26.4, -14, 28, 2.2);

  // gate posts
  box(0.4, 3.0, 0.4, -0.6, 1.5, -18.1, matConcreteDark, { collide: false });
  box(0.4, 3.0, 0.4, 4.6, 1.5, -18.1, matConcreteDark, { collide: false });



  /* ---------------- lights ---------------- */
  function lamp(cx, cz, h = 6.2, on = true, yaw = 0) {
    cyl(0.11, h, cx, h / 2, cz, matDarkMetal, { collide: true });
    const armLen = 1.5;
    box(armLen, 0.1, 0.1, cx + Math.sin(yaw) * armLen / 2, h - 0.06, cz + Math.cos(yaw) * armLen / 2, matDarkMetal, { collide: false });
    const hx = cx + Math.sin(yaw) * armLen;
    const hz = cz + Math.cos(yaw) * armLen;
    box(0.55, 0.16, 0.3, hx, h - 0.14, hz, on ? matGlassLit : matDarkMetal, { collide: false, cast: false });
    if (on) {
      const l = new THREE.PointLight(0xffd6a0, 34, 22, 2);
      l.position.set(hx, h - 0.35, hz);
      scene.add(l);
      lamps.push({ light: l, base: 34, x: hx, z: hz, phase: Math.random() * 10 });
    }
  }
  lamp(-18, -19.6, 6.4, true, -0.5);
  lamp(1, -19.6, 6.4, true, 0.4);
  lamp(18.5, -19.4, 6.4, true, 2.6);
  lamp(-19.6, 11.2, 5.6, true, 1.4);
  lamp(17.2, 20.2, 5.6, false);
  lamp(13.9, -13.4, 4.8, true, 3.4);

  // wall lamps on the warehouse
  for (const z of [-11, -1, 4]) {
    box(0.3, 0.18, 0.24, -20.8, 5.2, z, matGlassLit, { collide: false, cast: false });
    const l = new THREE.PointLight(0xffb877, 12, 10, 2);
    l.position.set(-20.4, 5.1, z);
    scene.add(l);
    lamps.push({ light: l, base: 12, x: -20.4, z, phase: Math.random() * 10 });
  }

  /* ---------------- rubble / debris ---------------- */
  for (let i = 0; i < 46; i++) {
    const a = Math.random() * Math.PI * 2;
    const r = 6 + Math.random() * 18;
    const x = Math.cos(a) * r * 0.9 - 2;
    const z = Math.sin(a) * r * 0.8 + 4;
    const s = 0.12 + Math.random() * 0.45;
    const m = new THREE.Mesh(BOX, Math.random() < 0.6 ? matConcreteDark : matBrick);
    m.scale.set(s, s * 0.6, s * (0.6 + Math.random()));
    m.position.set(x, s * 0.3, z);
    m.rotation.set(Math.random(), Math.random() * 6, Math.random());
    m.castShadow = true;
    m.receiveShadow = true;
    group.add(m);
  }
  // a couple of broken walls inside the yard
  wall('x', -5, -1.5, -8.5, 1.4, 0.3, matBrick);
  wall('z', -8.5, -6.6, -5, 1.1, 0.3, matBrick);
  wall('x', 15, 19, 6.5, 1.1, 0.3, matBrick);

  /* ---------------- yard structures ---------------- */
  // loading dock along the warehouse
  {
    const x0 = -20.7, x1 = -17.3, z0 = -8.4, z1 = 0.4;
    box(x1 - x0, 1.15, z1 - z0, (x0 + x1) / 2, 0.57, (z0 + z1) / 2, matConcrete);
    box(x1 - x0 + 0.3, 0.1, z1 - z0 + 0.3, (x0 + x1) / 2, 1.19, (z0 + z1) / 2, matConcreteDark, { collide: false });
    // steps down to the yard
    for (let i = 0; i < 3; i++) {
      box(x1 - x0, 0.28 * (i + 1), 0.46, (x0 + x1) / 2, 0.14 * (i + 1), z1 + 0.32 + (2 - i) * 0.46, matConcrete, { collide: false });
    }
    // safety rail on the open side
    for (let i = 0; i <= 4; i++) {
      box(0.07, 0.95, 0.07, x1 - 0.12, 1.7, z0 + 0.4 + i * 2.0, matPaintYellow, { collide: false });
    }
    box(0.05, 0.05, z1 - z0 - 0.8, x1 - 0.12, 2.14, (z0 + z1) / 2, matPaintYellow, { collide: false, cast: false });
    // pallets waiting on the dock
    for (let i = 0; i < 3; i++) box(1.1, 0.13, 1.0, -19.1, 1.28 + i * 0.14, -6.0, matWood, { ry: 0.1, collide: false });
    box(1.2, 1.0, 1.0, -19.2, 1.75, -4.4, matWood, { ry: -0.15 });
  }

  // guard hut by the street gate
  {
    const cx = -4.7, cz = -15.0, w = 2.3, d = 2.1, h = 2.45;
    wall('x', cx - w / 2, cx + w / 2, cz + d / 2, h, 0.12, matWoodDark, [{ from: cx - 0.45, to: cx + 0.45, top: 2.0 }]);
    wall('x', cx - w / 2, cx + w / 2, cz - d / 2, h, 0.12, matWoodDark, [{ from: cx - 0.9, to: cx + 0.9, top: 1.15 }]);
    wall('z', cz - d / 2, cz + d / 2, cx - w / 2, h, 0.12, matWoodDark);
    // window band on the yard side
    wall('z', cz - d / 2, cz + d / 2, cx + w / 2, h, 0.12, matWoodDark, [{ from: cz - 0.4, to: cz + 0.7, top: 1.1 }]);
    box(w + 0.3, 0.16, d + 0.3, cx, h + 0.08, cz, matWoodDark, { collide: false });
    box(w + 0.1, 0.14, d + 0.1, cx, h + 0.22, cz, matRust, { collide: false });
    // desk + stool visible through the window
    box(1.5, 0.08, 0.7, cx, 0.78, cz, matWood, { collide: false });
    box(0.4, 0.06, 0.4, cx + 0.6, 0.48, cz + 0.5, matWoodDark, { collide: false });
    // door lamp
    box(0.22, 0.12, 0.16, cx, 2.1, cz + d / 2 + 0.1, matGlassLit, { collide: false, cast: false });
    const l = new THREE.PointLight(0xffcf9a, 9, 8, 2);
    l.position.set(cx, 2.0, cz + d / 2 + 0.3);
    scene.add(l);
    lamps.push({ light: l, base: 9, x: cx, z: cz + d / 2 + 0.3, phase: Math.random() * 10 });
  }

  // fence splitting the yard into two zones (a chokepoint between the gates)
  fence(-20.4, 12.6, 4.2, 12.6, 2.2, [6.2, 9.4]);
  for (const gx of [-14.2, -11.0]) {
    box(0.26, 2.6, 0.26, gx, 1.3, 12.6, matDarkMetal, { collide: false });
    box(0.3, 0.1, 0.3, gx, 2.62, 12.6, matPaintYellow, { collide: false, cast: false });
  }

  // crate cluster in the open middle of the yard
  crate(1.2, 0, 6.0, 1.3, 0.2);
  crate(2.65, 0, 6.6, 1.1, -0.1);
  crate(1.9, 1.04, 6.15, 1.0, 0.35);
  crate(0.1, 0, 7.4, 0.85, 0.5);
  // pallet stack + leaning pallets
  for (let i = 0; i < 6; i++) box(1.2, 0.13, 1.0, 6.6, 0.07 + i * 0.14, 6.9, matWood, { ry: 0.06, collide: false });
  addCollider(6.6, 0.42, 6.9, 1.3, 0.84, 1.1);
  box(1.15, 1.2, 0.1, 7.4, 0.62, 8.2, matWood, { rz: -0.18 });
  box(1.15, 1.2, 0.1, 7.7, 0.62, 8.35, matWoodDark, { rz: -0.12 });

  // concrete blocks for cover near the north fence
  for (const [bx, bz, br] of [[6.5, -10.2, 0.1], [8.2, -10.6, -0.2], [5.1, -11.4, 0.35]]) {
    box(1.4, 1.25, 1.4, bx, 0.62, bz, matConcrete, { ry: br });
    box(1.45, 0.1, 1.45, bx, 1.3, bz, matConcreteDark, { ry: br, collide: false });
  }

  // bins by the office and the yard fence
  for (const [bx, bz, c] of [[-18.6, 20.6, matGreen], [-17.6, 21.4, matBlue], [9.2, 13.4, matGreen]]) {
    cyl(0.32, 1.0, bx, 0.5, bz, c, { lowPoly: true, collide: true });
    cyl(0.34, 0.08, bx, 1.03, bz, matDarkMetal, { collide: false, cast: false });
  }

  // street furniture on the north sidewalk
  for (const sx of [-14, -3, 8]) {
    box(1.7, 0.09, 0.5, sx, 0.5, -20.2, matWood, { collide: false });
    box(0.09, 0.44, 0.44, sx - 0.72, 0.28, -20.2, matDarkMetal, { collide: false });
    box(0.09, 0.44, 0.44, sx + 0.72, 0.28, -20.2, matDarkMetal, { collide: false });
    cyl(0.09, 0.75, sx + 1.5, 0.4, -20.2, matDarkMetal, { lowPoly: true });
  }
  // bus shelter
  {
    const sx = 3.5, sz = -20.4;
    box(3.6, 0.12, 1.5, sx, 2.5, sz + 0.15, matDarkMetal, { collide: false });
    for (const dx of [-1.6, 1.6]) {
      box(0.1, 2.5, 0.1, sx + dx, 1.25, sz + 0.7, matDarkMetal, { collide: false });
      box(0.1, 0.1, 1.5, sx + dx, 2.45, sz + 0.15, matDarkMetal, { collide: false, cast: false });
    }
    box(3.4, 1.9, 0.08, sx, 1.45, sz + 0.72, matGlass, { collide: false, cast: false });
    box(3.2, 0.08, 0.45, sx, 0.55, sz - 0.2, matWood, { collide: false });
    box(3.2, 0.5, 0.06, sx, 0.3, sz - 0.4, matDarkMetal, { collide: false });
  }

  // road barricade by the gate
  for (const [bx, bz, br] of [[-2.2, -17.2, 0.25], [1.4, -17.6, -0.15]]) {
    box(2.2, 0.22, 0.1, bx, 0.92, bz, matPaintYellow, { ry: br, collide: false });
    box(2.2, 0.22, 0.1, bx, 0.55, bz, matRed, { ry: br, collide: false });
    for (const dx of [-0.9, 0.9]) {
      box(0.1, 1.05, 0.1, bx + Math.cos(br) * dx, 0.52, bz - Math.sin(br) * dx, matDarkMetal, { collide: false });
      box(0.5, 0.09, 0.09, bx + Math.cos(br) * dx, 0.05, bz - Math.sin(br) * dx, matDarkMetal, { collide: false });
    }
    addCollider(bx, 0.55, bz, 2.2, 1.1, 0.3, br);
  }

  // extra lamp on the new dividing fence
  lamp(-6.5, 14.0, 5.4, true, 0.9);
  // wall lamps on the office block
  for (const z of [16.5, 23.5]) {
    box(0.3, 0.18, 0.24, -20.8, 3.4, z, matGlassLit, { collide: false, cast: false });
    const l = new THREE.PointLight(0xffb877, 10, 9, 2);
    l.position.set(-20.2, 3.3, z);
    scene.add(l);
    lamps.push({ light: l, base: 10, x: -20.2, z, phase: Math.random() * 10 });
  }

  /* ---------------- spawn points ---------------- */
  spawnPoints.push(
    { x: -12, z: -2 }, { x: 0.5, z: 1.5 }, { x: 10, z: -8 },
    { x: -6, z: 16.5 }, { x: 8.5, z: 23.5 }, { x: -17.5, z: 22.5 },
    { x: 14, z: -14 }, { x: -1, z: -15.5 }, { x: 2, z: 10.5 },
  );

  // every fence wire in a single draw call (built last: fences are added in several places)
  const fenceMesh = bakeBars(barBatch, matDarkMetal);
  if (fenceMesh) group.add(fenceMesh);

  const bounds = { minX: -20.4, maxX: 20.0, minZ: -18.0, maxZ: 26.0 };

  return {
    group, colliders, lamps, fires, spawnPoints, bounds,
    grid: buildGrid(colliders, 4),
  };
}

/* ---------------- collider grid ---------------- */
function buildGrid(colliders, cell) {
  const cells = new Map();
  for (const c of colliders) {
    const x0 = Math.floor(c.x0 / cell), x1 = Math.floor(c.x1 / cell);
    const z0 = Math.floor(c.z0 / cell), z1 = Math.floor(c.z1 / cell);
    for (let x = x0; x <= x1; x++) {
      for (let z = z0; z <= z1; z++) {
        const k = x + ',' + z;
        let list = cells.get(k);
        if (!list) { list = []; cells.set(k, list); }
        list.push(c);
      }
    }
  }
  return { cells, cell, stamp: 1 };
}

export function queryColliders(grid, x, z, r, out) {
  out.length = 0;
  if (!grid) return out;
  const { cells, cell } = grid;
  const stamp = ++grid.stamp;
  const x0 = Math.floor((x - r) / cell), x1 = Math.floor((x + r) / cell);
  const z0 = Math.floor((z - r) / cell), z1 = Math.floor((z + r) / cell);
  for (let cx = x0; cx <= x1; cx++) {
    for (let cz = z0; cz <= z1; cz++) {
      const list = cells.get(cx + ',' + cz);
      if (!list) continue;
      for (const c of list) {
        if (c._q === stamp) continue;
        c._q = stamp;
        out.push(c);
      }
    }
  }
  return out;
}
