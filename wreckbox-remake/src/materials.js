// Materials of the destruction sandbox.
// Every cell of the world grid is one of these ids.
// density  - relative mass per cell (used by the structural solver)
// strength - bearing capacity per cell (load it can carry before cracking)
// hardness - resistance to hammers / blasts (1 = soft, 3 = very tough)
// flammable- 0..1 chance to catch fire from nearby flames
// shelter  - radiation attenuation of the material
// span     - how far a cell can reach sideways (in cells) before snapping off

export const EMPTY = 0;
export const SOIL = 1;
export const ROCK = 2;
export const CONCRETE = 3;
export const BRICK = 4;
export const STEEL = 5;
export const GLASS = 6;
export const WOOD = 7;
export const ASPHALT = 8;
export const PANEL = 9;
export const AERATED = 10;
export const SLATE = 11;
export const GLASSBLOCK = 12;
export const LEAD = 13;
export const CASTIRON = 14;
export const DEBRIS = 15;
export const SAND = 16;
export const WATER = 17;
export const GRASS = 18;
export const AMMO = 19;
export const REACTOR = 20;
export const WIRE = 21;
export const PLASTIC = 22;
export const MAT_COUNT = 23;

// [key, ru, en, hex color, density, strength, hardness, flammable, shelter, sound, span]
const DEFS = [
  ['empty', 'Воздух', 'Air', '#000000', 0, 0, 0, 0, 0, null, 0],
  ['soil', 'Грунт', 'Soil', '#7a5233', 2.0, 1400, 0.55, 0, 0.35, 'dirt', 3],
  ['rock', 'Скала', 'Bedrock', '#6b6b73', 3.0, 9000, 2.6, 0, 0.7, 'stone', 4],
  ['concrete', 'Бетон', 'Concrete', '#c9c6bc', 2.6, 2100, 1.5, 0, 0.75, 'concrete', 10],
  ['brick', 'Кирпич', 'Brick', '#c4674a', 1.9, 1250, 0.9, 0, 0.55, 'brick', 6],
  ['steel', 'Сталь', 'Steel', '#9aa7b4', 3.4, 4200, 2.4, 0, 0.8, 'metal', 22],
  ['glass', 'Стекло', 'Glass', '#bfe6f7', 1.2, 3000, 0.25, 0, 0.05, 'glass', 4],
  ['wood', 'Дерево', 'Wood', '#b98b57', 0.7, 900, 0.5, 0.75, 0.15, 'wood', 10],
  ['asphalt', 'Асфальт', 'Asphalt', '#4a4a52', 2.2, 1500, 1.0, 0, 0.3, 'stone', 6],
  ['panel', 'Панель', 'Panel', '#e3ded2', 2.0, 1500, 1.2, 0, 0.65, 'concrete', 9],
  ['aerated', 'Газобетон', 'Aerated', '#f0ebdd', 0.9, 800, 0.5, 0, 0.45, 'brick', 6],
  ['slate', 'Шифер', 'Slate', '#6f7076', 1.3, 600, 0.7, 0.1, 0.35, 'brick', 14],
  ['glassblock', 'Стеклоблок', 'Glass block', '#d9f2ea', 1.6, 1800, 0.6, 0, 0.3, 'glass', 6],
  ['lead', 'Свинец', 'Lead', '#8b8f9a', 5.0, 900, 1.4, 0, 2.4, 'metal', 8],
  ['castiron', 'Чугун', 'Cast iron', '#6f747e', 4.2, 1400, 1.8, 0, 0.9, 'metal', 11],
  ['debris', 'Обломки', 'Debris', '#9a938a', 1.4, 1e9, 0.5, 0, 0.4, 'rubble', 60],
  ['sand', 'Песок', 'Sand', '#e0c887', 1.6, 600, 0.25, 0, 0.3, 'dirt', 1],
  ['water', 'Вода', 'Water', '#3f8fc4', 1.0, 0, 0, 0, 0.2, null, 0],
  ['grass', 'Трава', 'Grass', '#6aa84f', 1.2, 2200, 0.3, 0.3, 0.2, 'dirt', 10],
  ['ammo', 'Боеприпасы', 'Ammo', '#b09a4a', 2.2, 500, 0.6, 0.9, 0.4, 'metal', 3],
  ['reactor', 'Реактор', 'Reactor', '#8fe0a8', 3.0, 3000, 2.0, 0, 0.9, 'metal', 5],
  ['wire', 'Провод', 'Wire', '#e0c060', 0.4, 1e9, 0.1, 0.2, 0, 'metal', 60],
  ['plastic', 'Пластик', 'Plastic', '#f2f2f2', 0.8, 700, 0.6, 0.6, 0.1, 'wood', 6],
];

export const MATERIALS = DEFS.map((d, id) => ({
  id,
  key: d[0], ru: d[1], en: d[2],
  color: d[3],
  rgb: [
    parseInt(d[3].slice(1, 3), 16),
    parseInt(d[3].slice(3, 5), 16),
    parseInt(d[3].slice(5, 7), 16),
  ],
  density: d[4], strength: d[5], hardness: d[6],
  flammable: d[7], shelter: d[8], sound: d[9], span: d[10],
}));

export const RGB_R = new Uint8Array(MAT_COUNT);
export const RGB_G = new Uint8Array(MAT_COUNT);
export const RGB_B = new Uint8Array(MAT_COUNT);
export const DENSITY = new Float32Array(MAT_COUNT);
export const STRENGTH = new Float32Array(MAT_COUNT);
export const HARDNESS = new Float32Array(MAT_COUNT);
export const FLAMMABLE = new Float32Array(MAT_COUNT);
export const SHELTER = new Float32Array(MAT_COUNT);
export const SPAN = new Float32Array(MAT_COUNT);

for (const m of MATERIALS) {
  RGB_R[m.id] = m.rgb[0];
  RGB_G[m.id] = m.rgb[1];
  RGB_B[m.id] = m.rgb[2];
  DENSITY[m.id] = m.density;
  STRENGTH[m.id] = m.strength;
  HARDNESS[m.id] = m.hardness;
  FLAMMABLE[m.id] = m.flammable;
  SHELTER[m.id] = m.shelter;
  SPAN[m.id] = m.span;
}

// Colour lookup with per-cell tint (16 steps) precomputed for the renderer.
export const TINTS = 16;
export const TINT_R = new Uint8Array(MAT_COUNT * TINTS);
export const TINT_G = new Uint8Array(MAT_COUNT * TINTS);
export const TINT_B = new Uint8Array(MAT_COUNT * TINTS);

(function buildTints() {
  let seed = 1337;
  const rnd = () => {
    seed = (seed * 1664525 + 1013904223) >>> 0;
    return seed / 4294967296;
  };
  for (let m = 0; m < MAT_COUNT; m++) {
    for (let t = 0; t < TINTS; t++) {
      const base = MATERIALS[m];
      const k = 0.9 + (t / (TINTS - 1)) * 0.2 + (rnd() - 0.5) * 0.05;
      const i = m * TINTS + t;
      TINT_R[i] = Math.min(255, base.rgb[0] * k) | 0;
      TINT_G[i] = Math.min(255, base.rgb[1] * k) | 0;
      TINT_B[i] = Math.min(255, base.rgb[2] * k) | 0;
    }
  }
})();

// Hand-picked decoration colours (furniture, cars, signs, awnings...). Any cell
// can carry an explicit RGB override, which is what gives the city its life.
export const PAL = {
  bedRed: 0xd8504f, bedWhite: 0xf2efe6, sofaRed: 0xc9473f, sofaTeal: 0x3f9c8b,
  sofaBlue: 0x4a7fc0, tableWood: 0xa97544, tvDark: 0x2c3138,
  plant: 0x4f9e4a, lamp: 0xffd465, doorBrown: 0x8a5a34, fridge: 0xe9eef2,
  signRed: 0xd23b3b, signBlue: 0x2f7bc4, signGreen: 0x36a05f, signYellow: 0xf0c246,
  awningRed: 0xc9473f, awningWhite: 0xf5f2ea, awningGreen: 0x3f9c62,
  carRed: 0xd64545, carWhite: 0xeceff2, carBlue: 0x3f6bbf, carYellow: 0xe9c04a,
  carTeal: 0x3fa9a0, tire: 0x2a2c30, glassCar: 0x9fd4e8,
  roofRed: 0xa5432f, roofGreen: 0x3f7a4e, roofGrey: 0x6b6f76,
  ac: 0xb9c0c7, tank: 0x8fa0ae, chimney: 0x9b6a4f,
  concreteLight: 0xdcd8cc, concreteDark: 0x9d9a90, panelTrim: 0xe8e4d8,
  floorWood: 0xc9a878, floorTile: 0xd7d2c4,
  woodDark: 0x7c5330, brickLight: 0xd07a58, metalRail: 0xc8ced6,
  hazard: 0xf0c246, sandbag: 0xd6c08a, leadSheet: 0x99a0ab,
  water: 0x3f8fc4, leaf: 0x57a04a, leafDark: 0x3d7a37, trunk: 0x7d5734,
  // facade palettes: brick red, cream, sand, teal, orange, lavender, mint
  facBrick: 0xb8553c, facCream: 0xe3cfa4, facSand: 0xd8b46b, facTeal: 0x4f9d8f,
  facOrange: 0xd8834a, facLilac: 0xa795c4, facMint: 0x8fbf9a, facWhite: 0xe4e0d6,
  facBlue: 0x6f8fbf, facRose: 0xc98a92,
};

export const isSolid = (m) => m !== EMPTY && m !== WATER;
export const nameOf = (m, lang) => (lang === 'ru' ? MATERIALS[m].ru : MATERIALS[m].en);

// Linear blend of two packed RGB colours.
export function mixCol(a, b, t) {
  const ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255;
  const br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
  return (((ar + (br - ar) * t) | 0) << 16) | (((ag + (bg - ag) * t) | 0) << 8) | ((ab + (bb - ab) * t) | 0);
}
