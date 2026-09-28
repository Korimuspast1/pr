import * as THREE from './three.module.min.js';
import { bake } from './bake.js';

// ============================================================
//  Машины трафика: скруглённые кузова (экструзия профиля
//  с фаской), торовые колёса, стёкла, фары, поворотники.
//  Модель смотрит в +Z.
// ============================================================

const UP = new THREE.Vector3(0, 1, 0);

function mesh(geo, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(geo, mat);
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}

function roundedRectShape(w, h, r) {
  const s = new THREE.Shape();
  const x = -w / 2, y = -h / 2;
  s.moveTo(x + r, y);
  s.lineTo(x + w - r, y); s.quadraticCurveTo(x + w, y, x + w, y + r);
  s.lineTo(x + w, y + h - r); s.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  s.lineTo(x + r, y + h); s.quadraticCurveTo(x, y + h, x, y + h - r);
  s.lineTo(x, y + r); s.quadraticCurveTo(x, y, x + r, y);
  return s;
}

function roundedBoxGeo(w, h, d, r = 0.03) {
  r = Math.min(r, w / 2.1, h / 2.1, d / 2.1);
  const g = new THREE.ExtrudeGeometry(
    roundedRectShape(w - 2 * r, h - 2 * r, r * 0.85),
    { depth: Math.max(d - 2 * r, 0.01), bevelEnabled: true, bevelThickness: r, bevelSize: r * 0.92, bevelSegments: 3, curveSegments: 6 }
  );
  g.center();
  return g;
}

function profileMesh(pts, width, bevel, mat) {
  const s = new THREE.Shape();
  s.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i < pts.length; i++) s.lineTo(pts[i][0], pts[i][1]);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, {
    depth: width, bevelEnabled: true, bevelThickness: bevel,
    bevelSize: bevel * 0.9, bevelSegments: 4, curveSegments: 5, steps: 1
  });
  g.translate(0, 0, -width / 2);
  g.rotateY(-Math.PI / 2);
  const m = new THREE.Mesh(g, mat);
  m.castShadow = true;
  return m;
}

function buildCarWheel(R, rimR, matRubber, matRim) {
  const g = new THREE.Group();
  const tire = mesh(new THREE.TorusGeometry(R - 0.075, 0.075, 14, 30), matRubber);
  tire.rotation.y = Math.PI / 2;
  g.add(tire);
  const rim = mesh(new THREE.CylinderGeometry(rimR, rimR, 0.14, 20, 1, true), matRim);
  rim.rotation.z = Math.PI / 2;
  g.add(rim);
  for (let i = 0; i < 5; i++) {
    const sg = new THREE.Group();
    sg.add(mesh(new THREE.CylinderGeometry(0.012, 0.012, rimR * 1.7, 6), matRim, 0, 0, 0));
    sg.rotation.x = (i / 5) * Math.PI * 2;
    g.add(sg);
  }
  const hub = mesh(new THREE.CylinderGeometry(0.045, 0.045, 0.15, 10), matRim);
  hub.rotation.z = Math.PI / 2;
  g.add(hub);
  return g;
}

// общие материалы (создаются один раз)
let shared = null;
function sharedMats(T) {
  if (shared) return shared;
  shared = {
    rubber: new THREE.MeshStandardMaterial({ color: 0x17181b, roughness: 0.95, metalness: 0 }),
    rim: new THREE.MeshStandardMaterial({ color: 0x8f959c, roughness: 0.35, metalness: 0.9 }),
    trim: new THREE.MeshStandardMaterial({ color: 0x1c1e22, roughness: 0.6, metalness: 0.3 }),
    glass: new THREE.MeshPhysicalMaterial({ color: 0x10171d, roughness: 0.08, metalness: 0.35, envMapIntensity: 1.5 }),
    head: new THREE.MeshStandardMaterial({ color: 0xdfe9f5, emissive: 0xcfe0ff, emissiveIntensity: 0.8, roughness: 0.2 }),
    tail: new THREE.MeshStandardMaterial({ color: 0x6e0f0f, emissive: 0xff231a, emissiveIntensity: 0.85, roughness: 0.25 }),
    plate: new THREE.MeshStandardMaterial({ map: T.plate, roughness: 0.6 }),
    vanDecal: new THREE.MeshStandardMaterial({ map: T.vanText, transparent: true, roughness: 0.5 }),
    truckBox: new THREE.MeshStandardMaterial({ map: T.truckBox, roughness: 0.55, metalness: 0.1 }),
  };
  return shared;
}

// ---------- типы кузовов ----------
const KINDS = {
  sedan: {
    body: [[2.28, 0.40], [2.34, 0.62], [2.22, 0.80], [1.10, 0.88], [0.50, 1.34], [-0.50, 1.38], [-1.35, 1.02], [-2.02, 0.98], [-2.28, 0.84], [-2.30, 0.56], [-2.16, 0.40]],
    width: 1.44, bevel: 0.13,
    win: [[0.92, 0.92], [0.44, 1.29], [-0.44, 1.31], [-1.16, 0.99], [-1.16, 0.92]],
    wheelZ: [1.45, -1.45], wheelR: 0.31, wheelX: 0.80,
    windshield: { z0: 0.97, z1: 0.46, y0: 0.90, y1: 1.33, w: 1.28 },
    rear: { z0: -1.30, z1: -0.52, y0: 1.00, y1: 1.36, w: 1.24 },
    hlY: 0.66, hlZ: 2.29, tlY: 0.82, tlZ: -2.28,
    hw: 0.88, hl: 2.37,
  },
  hatch: {
    body: [[2.05, 0.40], [2.10, 0.60], [1.96, 0.75], [0.92, 0.83], [0.34, 1.30], [-0.72, 1.32], [-1.66, 1.08], [-1.94, 0.62], [-1.80, 0.40]],
    width: 1.36, bevel: 0.12,
    win: [[0.78, 0.87], [0.30, 1.25], [-0.65, 1.26], [-1.42, 1.00], [-1.42, 0.90]],
    wheelZ: [1.32, -1.30], wheelR: 0.29, wheelX: 0.74,
    windshield: { z0: 0.85, z1: 0.30, y0: 0.85, y1: 1.28, w: 1.20 },
    rear: { z0: -1.55, z1: -0.62, y0: 0.98, y1: 1.30, w: 1.16 },
    hlY: 0.62, hlZ: 2.06, tlY: 0.95, tlZ: -1.90,
    hw: 0.83, hl: 2.12,
  },
  van: {
    body: [[2.55, 0.55], [2.62, 1.10], [2.32, 1.86], [1.02, 2.04], [-2.45, 2.10], [-2.60, 1.35], [-2.44, 0.55]],
    width: 1.74, bevel: 0.14,
    win: [[1.95, 1.30], [1.20, 1.85], [-0.4, 1.88], [-1.6, 1.86], [-1.6, 1.30]],
    wheelZ: [1.72, -1.55], wheelR: 0.335, wheelX: 0.88,
    windshield: { z0: 2.42, z1: 1.30, y0: 1.20, y1: 1.92, w: 1.45 },
    rear: { z0: -2.5, z1: -2.2, y0: 1.0, y1: 1.9, w: 1.4 },
    hlY: 0.85, hlZ: 2.56, tlY: 1.3, tlZ: -2.56,
    hw: 0.97, hl: 2.72, decal: true,
  },
};

export function buildCar(kind, colorHex, T) {
  const S = sharedMats(T);
  const g = new THREE.Group();
  const K = KINDS[kind];
  const wheels = [];

  const bodyMat = new THREE.MeshPhysicalMaterial({
    color: colorHex, metalness: 0.72, roughness: 0.34,
    clearcoat: 1, clearcoatRoughness: 0.1, envMapIntensity: 1.15
  });

  // корпус
  g.add(profileMesh(K.body, K.width, K.bevel, bodyMat));
  const halfW = K.width / 2 + K.bevel * 0.9;

  // боковые стёкла (накладки)
  const winL = profileMesh(K.win, 0.02, 0.02, S.glass);
  winL.position.x = halfW - 0.015;
  g.add(winL);
  const winR = winL.clone();
  winR.position.x = -halfW + 0.015;
  g.add(winR);

  // лобовое и заднее стекло — наклонённые панели
  const ws = K.windshield;
  const dz = ws.z0 - ws.z1, dy = ws.y1 - ws.y0;
  const len = Math.hypot(dz, dy);
  const wsh = mesh(new THREE.BoxGeometry(ws.w, 0.02, len), S.glass, 0, (ws.y0 + ws.y1) / 2, (ws.z0 + ws.z1) / 2);
  wsh.rotation.x = Math.atan2(dy, dz) * (kind === 'van' ? -1 : -1);
  g.add(wsh);
  const rw = K.rear;
  const rdz = rw.z1 - rw.z0, rdy = rw.y1 - rw.y0;
  const rlen = Math.hypot(rdz, rdy);
  const rsh = mesh(new THREE.BoxGeometry(rw.w, 0.02, rlen), S.glass, 0, (rw.y0 + rw.y1) / 2, (rw.z0 + rw.z1) / 2);
  rsh.rotation.x = -Math.atan2(rdy, rdz);
  g.add(rsh);

  // арки колёс (тёмные "ниши")
  for (const z of K.wheelZ) for (const sx of [-1, 1]) {
    const arch = mesh(new THREE.CircleGeometry(K.wheelR + 0.10, 18, 0, Math.PI), S.trim, sx * (halfW - 0.035), K.wheelR, z);
    arch.rotation.y = sx > 0 ? Math.PI / 2 : -Math.PI / 2;
    arch.castShadow = false;
    g.add(arch);
  }

  // колёса (чуть выступают из арок)
  for (const z of K.wheelZ) for (const sx of [-1, 1]) {
    const w = buildCarWheel(K.wheelR, K.wheelR * 0.62, S.rubber, S.rim);
    w.position.set(sx * (halfW - 0.10), K.wheelR, z);
    w.userData.dynamic = true;   // вращается — не печём в корпус
    g.add(w);
    wheels.push({ mesh: w, r: K.wheelR });
  }

  // фары / задние фонари
  for (const sx of [-1, 1]) {
    const hl = mesh(roundedBoxGeo(0.32, 0.10, 0.09, 0.03), S.head, sx * (halfW - 0.28), K.hlY, K.hlZ);
    g.add(hl);
    const tl = mesh(roundedBoxGeo(0.28, 0.10, 0.07, 0.03), S.tail, sx * (halfW - 0.26), K.tlY, K.tlZ);
    g.add(tl);
  }
  // решётка
  g.add(mesh(roundedBoxGeo(0.95, 0.15, 0.06, 0.02), S.trim, 0, K.hlY - 0.16, K.hlZ));
  // бампера
  g.add(mesh(roundedBoxGeo(K.width * 0.94, 0.15, 0.24, 0.05), S.trim, 0, 0.42, K.hlZ - 0.02));
  g.add(mesh(roundedBoxGeo(K.width * 0.94, 0.15, 0.24, 0.05), S.trim, 0, 0.42, K.tlZ + 0.02));
  // зеркала
  for (const sx of [-1, 1]) {
    const arm = mesh(new THREE.CylinderGeometry(0.014, 0.014, 0.14, 8), S.trim, sx * (halfW + 0.05), K.win[0][1] + 0.06, K.win[0][0] - 0.12);
    arm.rotation.z = Math.PI / 2;
    g.add(arm);
    g.add(mesh(roundedBoxGeo(0.10, 0.07, 0.05, 0.02), S.trim, sx * (halfW + 0.11), K.win[0][1] + 0.10, K.win[0][0] - 0.12));
  }
  // номера (евроформат, случайный номер)
  const plateMat = new THREE.MeshStandardMaterial({
    map: T.plates[(Math.random() * T.plates.length) | 0], roughness: 0.6,
  });
  const pf = mesh(new THREE.PlaneGeometry(0.50, 0.11), plateMat, 0, 0.52, K.hlZ + 0.135);
  pf.castShadow = false;
  g.add(pf);
  const pr = mesh(new THREE.PlaneGeometry(0.50, 0.11), plateMat, 0, 0.58, K.tlZ - 0.14);
  pr.rotation.y = Math.PI;
  pr.castShadow = false;
  g.add(pr);

  // декаль на фургоне
  if (K.decal) {
    for (const sx of [-1, 1]) {
      const d = mesh(new THREE.PlaneGeometry(2.6, 0.65), S.vanDecal, sx * (halfW + 0.012), 1.45, -0.4);
      d.rotation.y = sx > 0 ? Math.PI / 2 : -Math.PI / 2;
      d.castShadow = false;
      g.add(d);
    }
  }

  // поворотники (собственные материалы — мигают)
  const blinkMat = side => new THREE.MeshStandardMaterial({ color: 0x7a4a10, emissive: 0xffa018, emissiveIntensity: 0.15, roughness: 0.3 });
  const blinkL = blinkMat(), blinkR = blinkMat();
  for (const sx of [-1, 1]) {
    const m = sx < 0 ? blinkL : blinkR;
    g.add(mesh(roundedBoxGeo(0.06, 0.05, 0.06, 0.02), m, sx * (halfW - 0.06), K.hlY + 0.02, K.hlZ - 0.06));
    g.add(mesh(roundedBoxGeo(0.06, 0.05, 0.06, 0.02), m, sx * (halfW - 0.06), K.tlY, K.tlZ + 0.06));
  }

  // печка: статичные детали сливаются в один меш на материал
  bake(g);

  return {
    group: g, wheels, hw: K.hw, hl: K.hl, blinkL, blinkR, kind,
    setPaint(hex) { bodyMat.color.set(hex); },
  };
}

// ---------- фура ----------
export function buildTruck(colorHex, T) {
  const S = sharedMats(T);
  const g = new THREE.Group();
  const wheels = [];
  const cabMat = new THREE.MeshPhysicalMaterial({
    color: colorHex, metalness: 0.7, roughness: 0.35, clearcoat: 1, clearcoatRoughness: 0.12, envMapIntensity: 1.1
  });

  // кабина
  g.add(profileMesh(
    [[2.30, 0.85], [2.42, 1.50], [2.16, 2.90], [0.90, 3.00], [0.55, 1.60], [0.50, 0.85]],
    2.0, 0.16, cabMat
  ));
  // лобовое стекло
  g.add(mesh(new THREE.BoxGeometry(1.7, 0.02, 1.05), S.glass, 0, 2.28, 1.62).rotateX(-0.72));
  // стёкла дверей
  for (const sx of [-1, 1]) {
    const d = mesh(new THREE.BoxGeometry(0.02, 0.75, 0.85), S.glass, sx * 1.16, 2.15, 0.9);
    g.add(d);
  }
  // фары, бампер
  for (const sx of [-1, 1]) {
    g.add(mesh(roundedBoxGeo(0.30, 0.16, 0.10, 0.03), S.head, sx * 0.72, 0.95, 2.38));
    g.add(mesh(roundedBoxGeo(0.30, 0.12, 0.10, 0.03), S.tail, sx * 0.95, 0.9, -5.15));
  }
  g.add(mesh(roundedBoxGeo(2.1, 0.3, 0.25, 0.06), S.trim, 0, 0.62, 2.38));
  // кузов
  const box = mesh(roundedBoxGeo(2.45, 2.45, 5.9, 0.09), S.truckBox, 0, 1.85, -2.55);
  g.add(box);
  // шасси
  g.add(mesh(roundedBoxGeo(1.1, 0.3, 8.4, 0.06), S.trim, 0, 0.62, -1.4));
  // брызговики
  for (const z of [-0.4, -2.4]) for (const sx of [-1, 1]) {
    g.add(mesh(new THREE.BoxGeometry(0.02, 0.45, 0.35), S.rubber, sx * 0.98, 0.45, z - 0.5));
  }
  // колёса: перед + 2 задние оси
  const axles = [1.72, -0.55, -2.35];
  for (const z of axles) for (const sx of [-1, 1]) {
    const w = buildCarWheel(0.45, 0.27, S.rubber, S.rim);
    w.position.set(sx * 0.88, 0.45, z);
    w.userData.dynamic = true;
    g.add(w);
    wheels.push({ mesh: w, r: 0.45 });
  }
  // номер (евроформат)
  const plateMat = new THREE.MeshStandardMaterial({
    map: T.plates[(Math.random() * T.plates.length) | 0], roughness: 0.6,
  });
  const pr = mesh(new THREE.PlaneGeometry(0.50, 0.11), plateMat, 0, 0.8, -5.52);
  pr.rotation.y = Math.PI;
  g.add(pr);

  const blinkL = new THREE.MeshStandardMaterial({ color: 0x7a4a10, emissive: 0xffa018, emissiveIntensity: 0.15, roughness: 0.3 });
  const blinkR = blinkL.clone();
  for (const sx of [-1, 1]) {
    const m = sx < 0 ? blinkL : blinkR;
    g.add(mesh(roundedBoxGeo(0.07, 0.06, 0.07, 0.02), m, sx * 1.0, 1.0, 2.36));
  }

  bake(g);

  return {
    group: g, wheels, hw: 1.16, hl: 4.65, blinkL, blinkR, kind: 'truck',
    setPaint(hex) { cabMat.color.set(hex); },
  };
}

export const CAR_COLORS = [
  0xd8dadd, 0xe9e9e9, 0x24262b, 0x70777e, 0x9d2424,
  0x1f3a6e, 0x365d8a, 0x5c6b46, 0x8a8f96, 0xbdb7aa,
];
export const TRUCK_COLORS = [0x1f5bbf, 0x9d2424, 0x2e6b3f, 0x3a4b5c, 0x8a6d3b];
