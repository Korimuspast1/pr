// Materials of the destruction sandbox.
// Every cell of the world grid is one of these ids.
// density  - relative mass per cell (used by the structural solver)
// strength - bearing capacity per cell (load it can carry before cracking)
// hardness - resistance to hammers / blasts (1 = soft, 3 = very tough)
// flammable- 0..1 chance to catch fire from nearby flames

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
export const MAT_COUNT = 22;

// [key, ru, en, hex color, density, strength, hardness, flammable, shelter(atm), sound]
const DEFS = [
  ['empty', 'Воздух', 'Air', '#000000', 0, 0, 0, 0, 0, null, 0],
  ['soil', 'Грунт', 'Soil', '#6b4f3a', 2.0, 1400, 0.55, 0, 0.35, 'dirt', 3],
  ['rock', 'Скала', 'Bedrock', '#474750', 3.0, 9000, 2.6, 0, 0.7, 'stone', 4],
  ['concrete', 'Бетон', 'Concrete', '#9a9a94', 2.6, 2100, 1.5, 0, 0.75, 'concrete', 10],
  ['brick', 'Кирпич', 'Brick', '#9c5a44', 1.9, 1250, 0.9, 0, 0.55, 'brick', 6],
  ['steel', 'Сталь', 'Steel', '#7b8794', 3.4, 4200, 2.4, 0, 0.8, 'metal', 22],
  ['glass', 'Стекло', 'Glass', '#9fd8e8', 1.2, 3000, 0.25, 0, 0.05, 'glass', 4],
  ['wood', 'Дерево', 'Wood', '#8a6136', 0.7, 900, 0.5, 0.75, 0.15, 'wood', 10],
  ['asphalt', 'Асфальт', 'Asphalt', '#3c3c44', 2.2, 1500, 1.0, 0, 0.3, 'stone', 6],
  ['panel', 'Панель', 'Panel', '#b3b0a6', 2.0, 1500, 1.2, 0, 0.65, 'concrete', 9],
  ['aerated', 'Газобетон', 'Aerated', '#d7d2c4', 0.9, 800, 0.5, 0, 0.45, 'brick', 6],
  ['slate', 'Шифер', 'Slate', '#5c5c62', 1.3, 600, 0.7, 0.1, 0.35, 'brick', 14],
  ['glassblock', 'Стеклоблок', 'Glass block', '#cfe8e0', 1.6, 1800, 0.6, 0, 0.3, 'glass', 6],
  ['lead', 'Свинец', 'Lead', '#6b6f7a', 5.0, 900, 1.4, 0, 2.4, 'metal', 8],
  ['castiron', 'Чугун', 'Cast iron', '#5b5f68', 4.2, 1400, 1.8, 0, 0.9, 'metal', 11],
  ['debris', 'Обломки', 'Debris', '#7a736a', 1.4, 1e9, 0.5, 0, 0.4, 'rubble', 60],
  ['sand', 'Песок', 'Sand', '#b9a06a', 1.6, 600, 0.25, 0, 0.3, 'dirt', 1],
  ['water', 'Вода', 'Water', '#2f6f9e', 1.0, 0, 0, 0, 0.2, null, 0],
  ['grass', 'Трава', 'Grass', '#4f7a3a', 1.2, 2200, 0.3, 0.3, 0.2, 'dirt', 10],
  ['ammo', 'Боеприпасы', 'Ammo', '#8f7a3c', 2.2, 500, 0.6, 0.9, 0.4, 'metal', 3],
  ['reactor', 'Реактор', 'Reactor', '#8ad6a0', 3.0, 3000, 2.0, 0, 0.9, 'metal', 5],
  ['wire', 'Провод', 'Wire', '#b8a04a', 0.4, 1e9, 0.1, 0.2, 0, 'metal', 60],
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

// Base rgb expanded for fast access
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
  // deterministic pseudo random per material+tint
  let seed = 1337;
  const rnd = () => {
    seed = (seed * 1664525 + 1013904223) >>> 0;
    return seed / 4294967296;
  };
  for (let m = 0; m < MAT_COUNT; m++) {
    for (let t = 0; t < TINTS; t++) {
      const base = MATERIALS[m];
      const k = 0.84 + (t / (TINTS - 1)) * 0.32 + (rnd() - 0.5) * 0.06;
      const i = m * TINTS + t;
      TINT_R[i] = Math.min(255, base.rgb[0] * k) | 0;
      TINT_G[i] = Math.min(255, base.rgb[1] * k) | 0;
      TINT_B[i] = Math.min(255, base.rgb[2] * k) | 0;
    }
  }
})();

export const isSolid = (m) => m !== EMPTY && m !== WATER;
export const nameOf = (m, lang) => (lang === 'ru' ? MATERIALS[m].ru : MATERIALS[m].en);
