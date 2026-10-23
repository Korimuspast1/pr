import * as THREE from '../vendor/three.module.js';

/* Weapon stats + simple 3D models. Dimensions are metres, model points +Z. */

export const WEAPONS = [
  {
    id: 'fists', name: 'Кулаки', short: 'Кулаки', kind: 'melee',
    dmg: 9, structDmg: 0.015, cut: 0.05, bladeLen: 4, swingTime: 0.34, bleedMul: 0.4, knock: 90,
  },
  {
    id: 'knife', name: 'Нож', short: 'Нож', kind: 'melee',
    dmg: 34, structDmg: 0.11, cut: 0.85, bladeLen: 10, swingTime: 0.3, bleedMul: 1.2, knock: 60,
  },
  {
    id: 'machete', name: 'Мачете', short: 'Мачете', kind: 'melee',
    dmg: 55, structDmg: 0.2, cut: 0.95, bladeLen: 20, swingTime: 0.4, bleedMul: 1.35, knock: 120,
  },
  {
    id: 'axe', name: 'Топор', short: 'Топор', kind: 'melee',
    dmg: 82, structDmg: 0.48, cut: 0.3, bladeLen: 17, swingTime: 0.52, bleedMul: 0.85, knock: 240,
  },
  {
    id: 'katana', name: 'Катана', short: 'Катана', kind: 'melee',
    dmg: 72, structDmg: 0.28, cut: 1.0, bladeLen: 27, swingTime: 0.36, bleedMul: 1.5, knock: 100,
  },
  {
    id: 'pistol', name: 'Пистолет', short: 'Пистолет', kind: 'gun', ammo: '9mm',
    magSize: 12, rpm: 340, auto: false, dmg: 26, pellets: 1, spread: 0.012, speed: 400,
    reloadTime: 1.6, sound: 'pistol', structMul: 0.06, bleedMul: 1.2, len: 10, shake: 0.25,
  },
  {
    id: 'smg', name: 'ПП «Вихрь»', short: 'ПП', kind: 'gun', ammo: '9mm',
    magSize: 30, rpm: 780, auto: true, dmg: 19, pellets: 1, spread: 0.022, speed: 420,
    reloadTime: 1.9, sound: 'smg', structMul: 0.05, bleedMul: 1.1, len: 17, shake: 0.16,
  },
  {
    id: 'shotgun', name: 'Дробовик', short: 'Дробовик', kind: 'gun', ammo: '12g',
    magSize: 6, rpm: 80, auto: false, dmg: 17, pellets: 9, spread: 0.075, speed: 380,
    reloadTime: 2.6, sound: 'shotgun', structMul: 0.055, bleedMul: 1.45, len: 25, shake: 0.55,
  },
  {
    id: 'rifle', name: 'СВД', short: 'СВД', kind: 'gun', ammo: '7.62',
    magSize: 10, rpm: 55, auto: false, dmg: 78, pellets: 1, spread: 0.004, speed: 780,
    reloadTime: 2.9, sound: 'rifle', structMul: 0.18, bleedMul: 1.75, len: 29, shake: 0.45,
  },
];

export const WEAPON_BY_ID = Object.fromEntries(WEAPONS.map((w) => [w.id, w]));

const STEEL = new THREE.MeshStandardMaterial({ color: 0xb9c2cd, roughness: 0.28, metalness: 0.9 });
const STEEL_DARK = new THREE.MeshStandardMaterial({ color: 0x5b636d, roughness: 0.4, metalness: 0.8 });
const BLACK = new THREE.MeshStandardMaterial({ color: 0x22252a, roughness: 0.5, metalness: 0.55 });
const WOOD = new THREE.MeshStandardMaterial({ color: 0x6b4a2c, roughness: 0.85 });
const WOOD_LIGHT = new THREE.MeshStandardMaterial({ color: 0x8a6039, roughness: 0.8 });
const TAN = new THREE.MeshStandardMaterial({ color: 0x8b7c5a, roughness: 0.8 });

function box(w, h, d, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}
function cyl(r, h, mat, x = 0, y = 0, z = 0, axis = 'z') {
  const m = new THREE.Mesh(new THREE.CylinderGeometry(r, r, h, 10), mat);
  m.position.set(x, y, z);
  if (axis === 'z') m.rotation.x = Math.PI / 2;
  m.castShadow = true;
  return m;
}

/**
 * Builds a weapon group. The grip sits at the origin, the barrel/blade points
 * along +Z, so it can be aligned to the hand-to-target direction.
 */
export function makeWeaponMesh(def) {
  const g = new THREE.Group();
  switch (def.id) {
    case 'fists':
      return g;
    case 'knife':
      g.add(box(0.028, 0.03, 0.1, WOOD, 0, 0, 0.03));
      g.add(box(0.006, 0.026, 0.22, STEEL, 0, 0.004, 0.2));
      g.add(box(0.02, 0.012, 0.03, STEEL_DARK, 0, -0.005, 0.105));
      break;
    case 'machete':
      g.add(box(0.03, 0.035, 0.12, WOOD, 0, 0, 0.04));
      g.add(box(0.008, 0.045, 0.38, STEEL, 0, 0.006, 0.3));
      g.add(box(0.008, 0.02, 0.06, STEEL, 0, 0.02, 0.47));
      break;
    case 'katana':
      g.add(box(0.028, 0.03, 0.24, BLACK, 0, 0, 0.06));
      g.add(box(0.075, 0.012, 0.02, STEEL_DARK, 0, 0, 0.19));
      g.add(box(0.006, 0.028, 0.62, STEEL, 0, 0.002, 0.5));
      break;
    case 'axe':
      g.add(box(0.035, 0.035, 0.5, WOOD, 0, 0, 0.24));
      g.add(box(0.02, 0.13, 0.16, STEEL_DARK, 0, 0, 0.47));
      g.add(box(0.012, 0.16, 0.1, STEEL, 0, 0.0, 0.55));
      break;
    case 'pistol':
      g.add(box(0.03, 0.055, 0.19, BLACK, 0, 0.02, 0.09));
      g.add(box(0.032, 0.02, 0.16, STEEL_DARK, 0, 0.052, 0.085));
      g.add(box(0.028, 0.09, 0.045, BLACK, 0, -0.03, 0.03));
      g.add(cyl(0.008, 0.03, STEEL, 0, 0.02, 0.19));
      break;
    case 'smg':
      g.add(box(0.045, 0.075, 0.3, BLACK, 0, 0.02, 0.16));
      g.add(cyl(0.011, 0.12, STEEL, 0, 0.03, 0.34));
      g.add(box(0.03, 0.1, 0.04, BLACK, 0, -0.04, 0.1));
      g.add(box(0.03, 0.13, 0.05, STEEL_DARK, 0, -0.07, 0.2));
      g.add(box(0.03, 0.045, 0.16, BLACK, 0, 0.0, 0.0));
      break;
    case 'shotgun':
      g.add(box(0.04, 0.06, 0.26, BLACK, 0, 0.02, 0.14));
      g.add(cyl(0.014, 0.62, STEEL_DARK, 0, 0.03, 0.56));
      g.add(cyl(0.011, 0.5, STEEL, 0, 0.005, 0.5));
      g.add(box(0.05, 0.05, 0.16, WOOD_LIGHT, 0, -0.005, 0.36));
      g.add(box(0.035, 0.09, 0.22, WOOD, 0, -0.01, -0.06));
      break;
    case 'rifle':
      g.add(box(0.04, 0.06, 0.34, BLACK, 0, 0.02, 0.18));
      g.add(cyl(0.012, 0.7, STEEL_DARK, 0, 0.02, 0.66));
      g.add(box(0.035, 0.05, 0.24, WOOD, 0, -0.005, -0.03));
      g.add(box(0.045, 0.1, 0.24, WOOD, 0, -0.02, -0.12));
      g.add(cyl(0.024, 0.24, BLACK, 0, 0.085, 0.22));
      g.add(box(0.028, 0.08, 0.05, STEEL_DARK, 0, -0.06, 0.22));
      break;
    default:
      g.add(box(0.04, 0.04, 0.2, BLACK, 0, 0, 0.1));
  }
  g.traverse((o) => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}

export { TAN };
