import * as THREE from '../vendor/three.module.js';

/* Weapon stats + models. Metres, the grip sits at the origin, +Z is forward. */

export const WEAPONS = [
  {
    id: 'fists', name: 'Кулаки', short: 'Кулаки', kind: 'melee',
    dmg: 9, structDmg: 0.015, cut: 0.05, mz: 0, swingTime: 0.34, bleedMul: 0.4, knock: 90,
  },
  {
    id: 'knife', name: 'Нож', short: 'Нож', kind: 'melee',
    dmg: 34, structDmg: 0.11, cut: 0.85, mz: 0.24, swingTime: 0.3, bleedMul: 1.2, knock: 60,
  },
  {
    id: 'machete', name: 'Мачете', short: 'Мачете', kind: 'melee',
    dmg: 55, structDmg: 0.2, cut: 0.95, mz: 0.55, swingTime: 0.4, bleedMul: 1.35, knock: 120,
  },
  {
    id: 'axe', name: 'Топор', short: 'Топор', kind: 'melee',
    dmg: 82, structDmg: 0.48, cut: 0.3, mz: 0.62, swingTime: 0.52, bleedMul: 0.85, knock: 240,
  },
  {
    id: 'katana', name: 'Катана', short: 'Катана', kind: 'melee',
    dmg: 72, structDmg: 0.28, cut: 1.0, mz: 0.95, swingTime: 0.36, bleedMul: 1.5, knock: 100,
  },
  {
    id: 'pistol', name: 'Пистолет', short: 'Пистолет', kind: 'gun', ammo: '9mm',
    magSize: 12, rpm: 340, auto: false, dmg: 26, pellets: 1, spread: 0.012, speed: 400,
    reloadTime: 1.6, sound: 'pistol', structMul: 0.06, bleedMul: 1.2, mz: 0.21, shake: 0.25,
  },
  {
    id: 'smg', name: 'ПП «Вихрь»', short: 'ПП', kind: 'gun', ammo: '9mm',
    magSize: 30, rpm: 780, auto: true, dmg: 19, pellets: 1, spread: 0.022, speed: 420,
    reloadTime: 1.9, sound: 'smg', structMul: 0.05, bleedMul: 1.1, mz: 0.44, shake: 0.16,
  },
  {
    id: 'shotgun', name: 'Дробовик', short: 'Дробовик', kind: 'gun', ammo: '12g',
    magSize: 6, rpm: 80, auto: false, dmg: 17, pellets: 9, spread: 0.075, speed: 380,
    reloadTime: 2.6, sound: 'shotgun', structMul: 0.055, bleedMul: 1.45, mz: 0.93, shake: 0.55,
  },
  {
    id: 'rifle', name: 'СВД', short: 'СВД', kind: 'gun', ammo: '7.62',
    magSize: 10, rpm: 55, auto: false, dmg: 78, pellets: 1, spread: 0.004, speed: 780,
    reloadTime: 2.9, sound: 'rifle', structMul: 0.12, bleedMul: 1.75, mz: 1.06, shake: 0.45,
  },
];

export const WEAPON_BY_ID = Object.fromEntries(WEAPONS.map((w) => [w.id, w]));

/* ---------------- materials ---------------- */
const STEEL = new THREE.MeshStandardMaterial({ color: 0xc3ccd6, roughness: 0.26, metalness: 0.92 });
const STEEL_DARK = new THREE.MeshStandardMaterial({ color: 0x6a727c, roughness: 0.38, metalness: 0.85 });
const BLACK = new THREE.MeshStandardMaterial({ color: 0x23262b, roughness: 0.48, metalness: 0.6 });
const POLY = new THREE.MeshStandardMaterial({ color: 0x1a1c1f, roughness: 0.82, metalness: 0.15 });
const WOOD = new THREE.MeshStandardMaterial({ color: 0x6b4526, roughness: 0.72 });
const WOOD_LIGHT = new THREE.MeshStandardMaterial({ color: 0x8f6236, roughness: 0.7 });
const BRASS = new THREE.MeshStandardMaterial({ color: 0xb08b3c, roughness: 0.35, metalness: 0.9 });

/* ---------------- primitive helpers ---------------- */
function mesh(geo, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(geo, mat);
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}
function box(w, h, d, mat, x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0) {
  const m = mesh(new THREE.BoxGeometry(w, h, d), mat, x, y, z);
  m.rotation.set(rx, ry, rz);
  return m;
}
function cyl(r1, r2, h, mat, x = 0, y = 0, z = 0, axis = 'z', seg = 12) {
  const m = mesh(new THREE.CylinderGeometry(r1, r2, h, seg), mat, x, y, z);
  if (axis === 'z') m.rotation.x = Math.PI / 2;
  if (axis === 'y') m.rotation.set(0, 0, 0);
  if (axis === 'x') m.rotation.z = Math.PI / 2;
  return m;
}
// rounded-ish trigger guard from three thin bars
function guard(mat, z0, y0) {
  const g = new THREE.Group();
  g.add(box(0.008, 0.05, 0.008, mat, 0, y0 - 0.05, z0));
  g.add(box(0.008, 0.008, 0.07, mat, 0, y0 - 0.075, z0 + 0.035));
  g.add(box(0.008, 0.03, 0.008, mat, 0, y0 - 0.06, z0 + 0.07));
  return g;
}
function sights(g, zFront, zRear, y, h = 0.012) {
  g.add(box(0.006, h, 0.012, BLACK, 0, y, zFront));
  g.add(box(0.03, h * 0.8, 0.012, BLACK, 0, y + 0.002, zRear));
  g.add(box(0.006, h * 1.4, 0.008, BLACK, -0.011, y + h * 0.6, zRear));
  g.add(box(0.006, h * 1.4, 0.008, BLACK, 0.011, y + h * 0.6, zRear));
}

// extruded blade from a 2D profile (x = along the blade, y = across)
function bladeMesh(pts, thick, mat, x = 0, y = 0, z = 0) {
  const shape = new THREE.Shape();
  shape.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i < pts.length; i++) shape.lineTo(pts[i][0], pts[i][1]);
  shape.closePath();
  const geo = new THREE.ExtrudeGeometry(shape, {
    depth: thick, bevelEnabled: true, bevelThickness: 0.0018, bevelSize: 0.0012, bevelSegments: 1,
  });
  geo.translate(0, 0, -thick / 2);
  geo.rotateY(-Math.PI / 2); // local +x becomes world +z, thickness stays on the X axis
  const m = new THREE.Mesh(geo, mat);
  m.position.set(x, y, z);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
}

/* ---------------- models ---------------- */
export function makeWeaponMesh(def) {
  const g = new THREE.Group();
  g.name = def.id;

  switch (def.id) {
    case 'fists': return g;

    case 'knife': {
      // puukko: round handle with grooves, guard, slim clip-point blade
      g.add(cyl(0.018, 0.02, 0.115, WOOD, 0, 0, 0.035, 'z', 10));
      for (let i = 0; i < 3; i++) g.add(cyl(0.0205, 0.0205, 0.005, STEEL_DARK, 0, 0, -0.002 + i * 0.033, 'z', 10));
      g.add(cyl(0.022, 0.02, 0.012, STEEL_DARK, 0, 0, -0.028, 'z', 10));
      g.add(box(0.046, 0.011, 0.016, STEEL_DARK, 0, 0, 0.098));
      g.add(bladeMesh([[0, 0.013], [0.118, 0.013], [0.152, 0.003], [0.144, -0.007], [0, -0.012]], 0.006, STEEL, 0, 0.0, 0.104));
      g.add(box(0.0068, 0.003, 0.13, STEEL_DARK, 0, -0.008, 0.18));
      break;
    }

    case 'machete': {
      // long working blade: thick spine, tapering belly, clipped point
      g.add(box(0.03, 0.04, 0.15, POLY, 0, 0, 0.05));
      for (let i = 0; i < 3; i++) g.add(box(0.032, 0.042, 0.007, BLACK, 0, 0, 0.005 + i * 0.045));
      g.add(box(0.034, 0.047, 0.014, STEEL_DARK, 0, 0, 0.128));
      g.add(bladeMesh([
        [0, 0.033], [0.09, 0.035], [0.3, 0.031], [0.42, 0.016], [0.49, -0.004],
        [0.43, -0.021], [0.1, -0.028], [0, -0.028],
      ], 0.0075, STEEL, 0, 0.0, 0.135));
      g.add(box(0.0085, 0.01, 0.34, STEEL_DARK, 0, 0.032, 0.3, 0, 0, 0.02));
      break;
    }

    case 'katana': {
      g.add(cyl(0.016, 0.018, 0.26, BLACK, 0, 0, 0.055, 'z', 10));
      for (let i = 0; i < 5; i++) {
        g.add(box(0.038, 0.036, 0.013, POLY, 0, 0, -0.05 + i * 0.048, Math.PI / 4, 0, 0));
      }
      g.add(cyl(0.021, 0.021, 0.012, STEEL_DARK, 0, 0, -0.075, 'z', 10));
      g.add(cyl(0.048, 0.048, 0.008, STEEL_DARK, 0, 0, 0.19, 'z', 14));
      g.add(box(0.012, 0.04, 0.014, BRASS, 0, 0, 0.203));
      // curved single-edged blade
      g.add(bladeMesh([
        [0, 0.017], [0.18, 0.021], [0.38, 0.03], [0.56, 0.044], [0.68, 0.06],
        [0.68, 0.048], [0.56, 0.03], [0.38, 0.014], [0.18, 0.003], [0, -0.002],
      ], 0.0068, STEEL, 0, 0.0, 0.21));
      g.add(box(0.0075, 0.006, 0.6, STEEL_DARK, 0, 0.048, 0.5, 0, 0, 0.045));
      break;
    }

    case 'axe': {
      // haft; the bit fans out below the head like a real felling axe
      g.add(cyl(0.017, 0.02, 0.44, WOOD, 0, 0, 0.2, 'z', 8));
      g.add(cyl(0.017, 0.019, 0.16, WOOD_LIGHT, 0, 0.008, 0.47, 'z', 8));
      g.add(cyl(0.022, 0.022, 0.06, STEEL_DARK, 0, 0.012, 0.575, 'z', 8));
      // eye around the haft + poll behind it
      g.add(box(0.042, 0.062, 0.075, STEEL_DARK, 0, 0.012, 0.578));
      g.add(box(0.05, 0.076, 0.038, STEEL_DARK, 0, 0.012, 0.53));
      // fan-shaped bit below the haft line
      g.add(bladeMesh([
        [0, 0.045], [0.075, 0.05], [0.115, 0.0], [0.125, -0.2],
        [0.035, -0.13], [0, -0.06],
      ], 0.013, STEEL, 0, 0.012, 0.49));
      g.add(box(0.026, 0.07, 0.06, STEEL_DARK, 0, 0.0, 0.55));
      break;
    }

    case 'pistol': {
      // slide + frame + raked grip
      g.add(box(0.031, 0.038, 0.185, BLACK, 0, 0.03, 0.092));
      g.add(box(0.027, 0.011, 0.19, STEEL_DARK, 0, 0.053, 0.092));
      for (let i = 0; i < 5; i++) g.add(box(0.033, 0.005, 0.009, STEEL_DARK, 0, 0.016, 0.02 + i * 0.028));
      g.add(box(0.029, 0.03, 0.145, BLACK, 0, 0.003, 0.078));
      g.add(cyl(0.007, 0.007, 0.028, STEEL, 0, 0.03, 0.198, 'z', 8));
      g.add(cyl(0.011, 0.011, 0.016, STEEL_DARK, 0, 0.03, 0.205, 'z', 10));
      g.add(box(0.028, 0.088, 0.042, POLY, 0, -0.038, 0.015, -0.32));
      g.add(box(0.03, 0.016, 0.045, BLACK, 0, -0.083, 0.0, -0.32));
      g.add(guard(BLACK, 0.048, -0.006));
      g.add(box(0.005, 0.026, 0.007, STEEL_DARK, 0, -0.016, 0.046, 0.25));
      g.add(box(0.018, 0.007, 0.028, STEEL_DARK, 0, -0.011, 0.095));
      sights(g, 0.183, 0.008, 0.058, 0.01);
      g.add(box(0.015, 0.011, 0.02, STEEL_DARK, 0, 0.04, -0.004));
      break;
    }

    case 'smg': {
      // compact receiver, short shroud, straight mag, wire stock
      g.add(box(0.044, 0.064, 0.24, BLACK, 0, 0.022, 0.12));
      g.add(box(0.038, 0.022, 0.2, STEEL_DARK, 0, 0.06, 0.11));
      g.add(box(0.036, 0.04, 0.14, POLY, 0, 0.016, 0.31));
      for (let i = 0; i < 3; i++) g.add(box(0.038, 0.011, 0.016, BLACK, 0, 0.033, 0.27 + i * 0.032));
      g.add(cyl(0.011, 0.011, 0.14, STEEL, 0, 0.026, 0.4, 'z', 10));
      g.add(cyl(0.015, 0.015, 0.026, STEEL_DARK, 0, 0.026, 0.47, 'z', 10));
      g.add(box(0.028, 0.1, 0.04, POLY, 0, -0.045, 0.045, -0.26));
      g.add(box(0.03, 0.016, 0.044, BLACK, 0, -0.096, 0.028, -0.26));
      g.add(guard(BLACK, 0.088, -0.008));
      g.add(box(0.028, 0.19, 0.046, STEEL_DARK, 0, -0.112, 0.165, 0.14));
      g.add(box(0.03, 0.014, 0.048, BLACK, 0, -0.203, 0.18, 0.14));
      g.add(box(0.009, 0.009, 0.19, STEEL_DARK, 0, 0.033, -0.07));
      g.add(box(0.009, 0.009, 0.19, STEEL_DARK, 0, 0.0, -0.07));
      g.add(box(0.032, 0.07, 0.011, POLY, 0, 0.017, -0.165));
      sights(g, 0.44, 0.015, 0.074, 0.01);
      break;
    }

    case 'shotgun': {
      g.add(box(0.048, 0.068, 0.28, BLACK, 0, 0.018, 0.16));
      g.add(cyl(0.0145, 0.0145, 0.62, STEEL_DARK, 0, 0.038, 0.6, 'z', 12));
      g.add(cyl(0.011, 0.011, 0.5, STEEL, 0, 0.01, 0.53, 'z', 10));
      g.add(box(0.052, 0.052, 0.18, WOOD_LIGHT, 0, 0.012, 0.44));
      for (let i = 0; i < 5; i++) g.add(box(0.054, 0.056, 0.012, WOOD, 0, 0.012, 0.38 + i * 0.028));
      g.add(box(0.038, 0.09, 0.21, WOOD, 0, -0.005, -0.02, -0.05));
      g.add(box(0.044, 0.11, 0.2, WOOD, 0, -0.022, -0.16, -0.08));
      g.add(box(0.046, 0.125, 0.022, POLY, 0, -0.032, -0.26, -0.08));
      g.add(guard(BLACK, 0.1, -0.022));
      g.add(box(0.005, 0.032, 0.008, STEEL_DARK, 0, -0.032, 0.105, 0.2));
      g.add(box(0.007, 0.014, 0.009, STEEL, 0, 0.056, 0.89));
      break;
    }

    case 'rifle': {
      g.add(box(0.042, 0.062, 0.32, BLACK, 0, 0.018, 0.2));
      g.add(box(0.038, 0.032, 0.24, WOOD, 0, 0.05, 0.29));
      g.add(cyl(0.011, 0.011, 0.66, STEEL_DARK, 0, 0.018, 0.66, 'z', 12));
      g.add(cyl(0.017, 0.017, 0.1, STEEL, 0, 0.018, 1.0, 'z', 10));
      g.add(box(0.008, 0.034, 0.06, STEEL, 0, 0.018, 1.02));
      // scope: tube, bells, rings
      g.add(cyl(0.026, 0.026, 0.3, BLACK, 0, 0.098, 0.22, 'z', 16));
      g.add(cyl(0.032, 0.026, 0.06, STEEL_DARK, 0, 0.098, 0.39, 'z', 16));
      g.add(cyl(0.03, 0.026, 0.05, STEEL_DARK, 0, 0.098, 0.04, 'z', 16));
      g.add(box(0.02, 0.05, 0.022, STEEL_DARK, 0, 0.066, 0.12));
      g.add(box(0.02, 0.05, 0.022, STEEL_DARK, 0, 0.066, 0.32));
      // wooden stock with cheek rest + butt pad
      g.add(box(0.04, 0.08, 0.26, WOOD, 0, -0.005, -0.07, -0.03));
      g.add(box(0.044, 0.11, 0.2, WOOD, 0, -0.026, -0.22, -0.07));
      g.add(box(0.032, 0.05, 0.17, WOOD_LIGHT, 0, 0.034, -0.1));
      g.add(box(0.048, 0.13, 0.024, POLY, 0, -0.036, -0.32, -0.07));
      // magazine + grip
      g.add(box(0.032, 0.11, 0.06, STEEL_DARK, 0, -0.07, 0.21, 0.08));
      g.add(box(0.03, 0.1, 0.042, WOOD, 0, -0.045, 0.09, -0.2));
      g.add(guard(BLACK, 0.14, -0.018));
      g.add(box(0.005, 0.032, 0.008, STEEL_DARK, 0, -0.022, 0.15, 0.2));
      break;
    }

    default:
      g.add(box(0.04, 0.04, 0.2, BLACK, 0, 0, 0.1));
  }

  g.traverse((o) => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  g.userData.weapon = def;
  return g;
}
