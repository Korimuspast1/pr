// City mechanics layer.
//
// Everything here is about *systems around the buildings*: which services the
// town has, what covers which house, how dirty or noisy a street is, what the
// land is worth, which milestones are reached and which policies are on.
// The simulation (sim.js) reads this and keeps the money books honest; the
// renderer draws the result.
//
// One building kind may be a business (shop, factory, warehouse - it has a
// till and workers) or a public service (school, clinic, power plant - it is
// paid from the city budget and covers the tiles around it).

export const SERVICES = {
  water: {
    label: 'водонапорная башня',
    short: 'вода',
    field: 'water',
    cost: 240,
    upkeep: 10,
    jobs: 1,
    radius: 7,
    unlock: 4,
    icon: 'water',
    note: 'без воды дом не принимает новых жителей',
  },
  power: {
    label: 'электростанция',
    short: 'энергия',
    field: 'power',
    cost: 380,
    upkeep: 18,
    jobs: 2,
    radius: 8,
    unlock: 8,
    icon: 'power',
    note: 'без энергии завод стоит, а лавка торгует вполсилы',
  },
  school: {
    label: 'школа',
    short: 'школа',
    field: 'education',
    cost: 320,
    upkeep: 12,
    jobs: 2,
    radius: 6,
    unlock: 10,
    icon: 'school',
    note: 'дети учатся, взрослые с образованием получают больше',
  },
  busstop: {
    label: 'остановка',
    short: 'транспорт',
    field: 'transit',
    cost: 60,
    upkeep: 2,
    jobs: 0,
    radius: 4,
    unlock: 12,
    icon: 'bus',
    note: 'меньше машин и шума, настроение выше',
  },
  clinic: {
    label: 'поликлиника',
    short: 'здоровье',
    field: 'health',
    cost: 380,
    upkeep: 14,
    jobs: 2,
    radius: 7,
    unlock: 18,
    icon: 'clinic',
    note: 'лечит больных, иначе они не выходят на работу',
  },
  landfill: {
    label: 'свалка',
    short: 'мусор',
    field: 'waste',
    cost: 260,
    upkeep: 9,
    jobs: 1,
    radius: 5,
    unlock: 20,
    capacity: 240,
    icon: 'landfill',
    note: 'собирает мусор; переполнится - улицы запахнут',
  },
  warehouse: {
    label: 'склад',
    short: 'склад',
    field: 'logistics',
    cost: 300,
    upkeep: 14,
    jobs: 2,
    radius: 6,
    size: 2,
    unlock: 22,
    icon: 'warehouse',
    note: 'лавки берут товар со склада, а не с дальнего завода',
  },
  police: {
    label: 'полиция',
    short: 'порядок',
    field: 'safety',
    cost: 340,
    upkeep: 13,
    jobs: 2,
    radius: 7,
    unlock: 26,
    icon: 'police',
    note: 'без неё из касс пропадают деньги',
  },
  fire: {
    label: 'пожарная часть',
    short: 'пожарные',
    field: 'fire',
    cost: 340,
    upkeep: 13,
    jobs: 2,
    radius: 8,
    unlock: 30,
    icon: 'fire',
    note: 'иначе пожар сжигает здание дотла',
  },
  landmark: {
    label: 'достопримечательность',
    short: 'туризм',
    field: 'tourism',
    cost: 900,
    upkeep: 18,
    jobs: 3,
    radius: 9,
    size: 2,
    unlock: 40,
    icon: 'landmark',
    note: 'приносит деньги туристов каждый день',
  },
};

export const SERVICE_FIELDS = ['water', 'power', 'education', 'transit', 'health', 'waste', 'logistics', 'safety', 'fire', 'tourism'];

/** Milestones: what the town unlocks and what it is paid for reaching them. */
export const MILESTONES = [
  { pop: 4, grant: 200, label: 'первые жители', unlock: ['water'] },
  { pop: 8, grant: 350, label: 'поселение', unlock: ['power', 'school'] },
  { pop: 12, grant: 450, label: 'городок', unlock: ['busstop'] },
  { pop: 18, grant: 600, label: 'районный центр', unlock: ['clinic'] },
  { pop: 20, grant: 700, label: 'муниципалитет', unlock: ['landfill'] },
  { pop: 22, grant: 800, label: 'логистика', unlock: ['warehouse'] },
  { pop: 26, grant: 900, label: 'порядок', unlock: ['police'] },
  { pop: 30, grant: 1000, label: 'безопасность', unlock: ['fire'] },
  { pop: 40, grant: 1500, label: 'достопримечательность', unlock: ['landmark'] },
  { pop: 60, grant: 2500, label: 'большой город', unlock: [] },
];

/** Policies the player can switch on and off (clicked in the HUD). */
export const POLICIES = {
  nightShift: {
    label: 'ночная смена',
    note: 'заводы работают круглосуточно, но шумят',
    cost: 0,
    perDay: 0,
  },
  recycling: {
    label: 'раздельный сбор',
    note: 'мусора вдвое меньше, зато стоит денег',
    cost: 0,
    perDay: 25,
  },
  freeTransit: {
    label: 'бесплатный транспорт',
    note: 'настроение выше, городу дороже',
    cost: 0,
    perDay: 20,
  },
};

export const SEASONS = [
  { name: 'весна', heat: 0.6, mood: 0.02, tourism: 0.9 },
  { name: 'лето', heat: 0.2, mood: 0.05, tourism: 1.4 },
  { name: 'осень', heat: 0.8, mood: 0.0, tourism: 0.8 },
  { name: 'зима', heat: 1.4, mood: -0.05, tourism: 0.4 },
];

export const DAYS_PER_SEASON = 8; // so a year is 32 game days

export function seasonOf(day) {
  const i = Math.floor((day - 1) / DAYS_PER_SEASON) % SEASONS.length;
  return { ...SEASONS[i], index: i };
}

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

/**
 * Per-tile environment of the town: service coverage, pollution, noise and
 * land value. Recomputed when the town changes (buildings, roads) and once an
 * hour, because traffic changes the noise.
 */
export class Systems {
  constructor(sim) {
    this.sim = sim;
    const n = sim.world.n;
    this.n = n;
    this.serve = Object.fromEntries(SERVICE_FIELDS.map((f) => [f, new Uint8Array(n * n)]));
    this.pollution = new Float32Array(n * n);
    this.noise = new Float32Array(n * n);
    this.value = new Float32Array(n * n);
    this.version = -1;
    this.last = { pollution: 0, noise: 0, value: 0 };
    this.unlocked = new Set(['water']);
    this.reached = [];
    this.stats = { propertyTax: 0, tourism: 0, upkeep: 0, waste: 0, fires: 0, crime: 0 };
  }

  idx(x, y) {
    return y * this.n + x;
  }

  inBounds(x, y) {
    return x >= 0 && y >= 0 && x < this.n && y < this.n;
  }

  /** True when a service of `field` covers this tile. */
  served(field, x, y) {
    if (!this.inBounds(x, y)) return false;
    const map = this.serve[field];
    return !!map && map[this.idx(Math.round(x), Math.round(y))] > 0;
  }

  /** Everything one building can use, by its door tile. */
  coverageAt(b) {
    const x = Math.round(b.door.x);
    const y = Math.round(b.door.y);
    const out = {};
    for (const f of SERVICE_FIELDS) out[f] = this.served(f, x, y);
    return out;
  }

  pollutionAt(x, y) {
    let best = 0;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if (!this.inBounds(x + dx, y + dy)) continue;
        best = Math.max(best, this.pollution[this.idx(x + dx, y + dy)]);
      }
    }
    return best;
  }

  noiseAt(x, y) {
    let best = 0;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if (!this.inBounds(x + dx, y + dy)) continue;
        best = Math.max(best, this.noise[this.idx(x + dx, y + dy)]);
      }
    }
    return best;
  }

  landValueAt(x, y) {
    if (!this.inBounds(x, y)) return 0;
    return this.value[this.idx(x, y)];
  }

  /** Rebuild coverage, pollution, noise and land value. */
  recompute() {
    const sim = this.sim;
    const w = sim.world;
    const n = this.n;
    for (const f of SERVICE_FIELDS) this.serve[f].fill(0);
    this.pollution.fill(0);
    this.noise.fill(0);

    // --- services cover a square around their door
    for (const b of w.buildings) {
      if (b.state !== 'done' || b.abandoned) continue;
      const svc = SERVICES[b.kind];
      if (!svc) continue;
      const map = this.serve[svc.field];
      if (!map) continue;
      const cx = Math.round(b.door.x);
      const cy = Math.round(b.door.y);
      const r = svc.radius;
      for (let y = cy - r; y <= cy + r; y++) {
        for (let x = cx - r; x <= cx + r; x++) {
          if (!this.inBounds(x, y)) continue;
          if (Math.abs(x - cx) + Math.abs(y - cy) > r + 1) continue;
          map[this.idx(x, y)] = 1;
        }
      }
    }

    // --- factories smoke, traffic rumbles, parks are quiet
    for (const b of w.buildings) {
      if (b.state !== 'done' || b.abandoned) continue;
      if (b.kind === 'factory') {
        const strength = 1 + (b.w * b.d > 1 ? 0.6 : 0);
        this._spread(this.pollution, b.x + b.w / 2, b.y + b.d / 2, 5, 0.9 * strength);
        this._spread(this.noise, b.x + b.w / 2, b.y + b.d / 2, 4, 0.5);
      }
      if (b.kind === 'landfill') {
        this._spread(this.pollution, b.x + 0.5, b.y + 0.5, 4, 0.5);
      }
      if (SERVICES[b.kind] || b.kind === 'shop' || b.kind === 'house') {
        this._spread(this.noise, b.door.x, b.door.y, 2, 0.15);
      }
    }
    // cars add noise next to the roads they use
    for (const car of sim.cars) {
      this._spread(this.noise, car.x, car.y, 2, 0.35);
    }
    // parks clean the air a little
    for (const b of w.buildings) {
      if (b.kind !== 'park') continue;
      this._clean(this.pollution, b.x + 0.5, b.y + 0.5, 3, 0.35);
    }

    // --- land value: parks, landmarks and the waterfront raise it, dirt lowers it
    let total = 0;
    for (let y = 0; y < n; y++) {
      for (let x = 0; x < n; x++) {
        let v = 8;
        v += 4 * this._countNear('park', x, y, 5);
        v += 12 * this._countNear('landmark', x, y, 8);
        v += 6 * this._countNear('water', x, y, 4);
        v += 2 * this._countNear('busstop', x, y, 4);
        v -= 6 * this.pollutionAt(x, y);
        v -= 4 * this.noiseAt(x, y);
        v += w.get(x, y) === 1 ? 2 : 0; // a paved street in front
        this.value[this.idx(x, y)] = clamp(v, 0, 100);
        total += this.value[this.idx(x, y)];
      }
    }

    let pol = 0;
    let noise = 0;
    for (let i = 0; i < n * n; i++) {
      pol += this.pollution[i];
      noise += this.noise[i];
    }
    this.last = { pollution: pol / (n * n), noise: noise / (n * n), value: total / (n * n) };
    this.version = w.version;
  }

  _spread(field, cx, cy, radius, strength) {
    const n = this.n;
    for (let y = Math.floor(cy - radius); y <= cy + radius; y++) {
      for (let x = Math.floor(cx - radius); x <= cx + radius; x++) {
        if (!this.inBounds(x, y)) continue;
        const d = Math.hypot(x + 0.5 - cx, y + 0.5 - cy);
        if (d > radius) continue;
        const add = strength * (1 - d / (radius + 0.5));
        const i = this.idx(x, y);
        field[i] = Math.min(6, field[i] + add);
      }
    }
  }

  _clean(field, cx, cy, radius, strength) {
    const n = this.n;
    for (let y = Math.floor(cy - radius); y <= cy + radius; y++) {
      for (let x = Math.floor(cx - radius); x <= cx + radius; x++) {
        if (!this.inBounds(x, y)) continue;
        const d = Math.hypot(x + 0.5 - cx, y + 0.5 - cy);
        if (d > radius) continue;
        const i = this.idx(x, y);
        field[i] = Math.max(0, field[i] - strength * (1 - d / (radius + 0.5)));
      }
    }
  }

  _countNear(kind, x, y, r) {
    let count = 0;
    for (const b of this.sim.world.buildings) {
      if (b.kind !== kind || b.state !== 'done') continue;
      if (Math.abs(b.x - x) > r || Math.abs(b.y - y) > r) continue;
      count++;
    }
    return count;
  }

  /** Milestones by population: unlocks and one-off grants. */
  checkMilestones(pop) {
    const out = [];
    for (const m of MILESTONES) {
      if (pop < m.pop || this.reached.includes(m.pop)) continue;
      this.reached.push(m.pop);
      for (const u of m.unlock) this.unlocked.add(u);
      out.push(m);
    }
    return out;
  }

  isUnlocked(kind) {
    const svc = SERVICES[kind];
    if (!svc) return true;
    return this.unlocked.has(kind);
  }
}
