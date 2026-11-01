// The map: a blank grid of land that the player paints with roads and zones.
// Buildings grow on zoned tiles next to a road, and every walker is only ever
// allowed on road tiles - so nobody can walk through a wall.

export const N = 40;

export const T = {
  GRASS: 0,
  ROAD: 1,
  ZONE_R: 2,
  ZONE_C: 3,
  ZONE_I: 4,
  PARK: 5,
  WATER: 6,
};

export const ZONE_OF = {
  [T.ZONE_R]: 'r',
  [T.ZONE_C]: 'c',
  [T.ZONE_I]: 'i',
};

const N4 = [
  [0, -1],
  [1, 0],
  [0, 1],
  [-1, 0],
];

let nextBuildingId = 1;

export class World {
  constructor(n = N) {
    this.n = n;
    this.tiles = new Uint8Array(n * n);
    this.occ = new Int16Array(n * n).fill(-1);
    this.buildings = [];
    this.props = [];
    this.comp = new Int16Array(n * n);
    this.bridges = new Set(); // road tiles that are bridges over water
    // road networks that reach the border of the map: the only way in or out
    this.edgeComps = new Set();
    this.version = 0; // bumps whenever the ground looks different
    this.walkVersion = 0; // bumps whenever roads change

    // a little natural variation so blank land is not flat
    for (let y = 0; y < n; y++) {
      for (let x = 0; x < n; x++) {
        const h = ((x * 73856093) ^ (y * 19349663)) >>> 0;
        if (h % 23 === 0 && x > 1 && y > 1 && x < n - 2 && y < n - 2) {
          this.props.push({ kind: 'tree', x: x + 0.2 + ((h >> 5) % 60) / 100, y: y + 0.2 + ((h >> 9) % 60) / 100, s: 0.8 + ((h >> 3) % 40) / 100 });
        } else if (h % 17 === 0) {
          this.props.push({ kind: 'tuft', x: x + 0.3 + ((h >> 7) % 40) / 100, y: y + 0.4 + ((h >> 11) % 40) / 100 });
        } else if (h % 13 === 0) {
          this.props.push({ kind: 'bush', x: x + 0.3 + ((h >> 6) % 50) / 100, y: y + 0.4 + ((h >> 10) % 50) / 100 });
        } else if (h % 29 === 0) {
          this.props.push({ kind: 'rock', x: x + 0.4 + ((h >> 8) % 30) / 100, y: y + 0.4 + ((h >> 12) % 30) / 100 });
        }
      }
    }
    this.carveWater();
  }

  /**
   * Water on a blank map: a river along the south and a lake in the north-east.
   * Roads may cross water, but only as bridges - and a bridge costs five times
   * a normal road tile.
   */
  carveWater() {
    const n = this.n;
    for (let x = 0; x < n; x++) {
      // the river wiggles a little so it does not look like a ruler
      const shift = Math.round(Math.sin(x / 5) * 1.2);
      for (let k = 0; k < 2; k++) {
        const y = n - 5 + k + shift;
        if (y < 0 || y >= n) continue;
        this.tiles[y * n + x] = T.WATER;
      }
    }
    // a round lake in the north-east
    const cx = n - 7;
    const cy = 6;
    for (let y = cy - 4; y <= cy + 4; y++) {
      for (let x = cx - 5; x <= cx + 5; x++) {
        if (!this.inBounds(x, y)) continue;
        const d = Math.hypot((x - cx) / 5, (y - cy) / 3.4);
        if (d <= 1) this.tiles[y * n + x] = T.WATER;
      }
    }
    // no trees standing in the water
    this.props = this.props.filter((p) => this.get(Math.floor(p.x), Math.floor(p.y)) !== T.WATER);
    this.version++;
  }

  isWater(x, y) {
    return this.get(x, y) === T.WATER;
  }

  isBridge(x, y) {
    return this.bridges.has(y * this.n + x);
  }

  inBounds(x, y) {
    return x >= 0 && y >= 0 && x < this.n && y < this.n;
  }

  get(x, y) {
    if (!inB(this, x, y)) return -1;
    return this.tiles[y * this.n + x];
  }

  set(x, y, type) {
    if (!inB(this, x, y)) return false;
    const i = y * this.n + x;
    if (this.tiles[i] === type) return false;
    this.tiles[i] = type;
    this.version++;
    if (type === T.ROAD || this.comp[i] > 0) this.walkVersion++;
    return true;
  }

  isRoad(x, y) {
    return this.get(x, y) === T.ROAD;
  }

  buildingAt(x, y) {
    if (!inB(this, x, y)) return null;
    const id = this.occ[y * this.n + x];
    return id < 0 ? null : this.buildings.find((b) => b.id === id) || null;
  }

  zoneAt(x, y) {
    const t = this.get(x, y);
    return ZONE_OF[t] || null;
  }

  /** Neighbouring roads as a bit mask: 1 north, 2 east, 4 south, 8 west. */
  roadMask(x, y) {
    let m = 0;
    if (this.isRoad(x, y - 1)) m |= 1;
    if (this.isRoad(x + 1, y)) m |= 2;
    if (this.isRoad(x, y + 1)) m |= 4;
    if (this.isRoad(x - 1, y)) m |= 8;
    return m;
  }

  hasRoadNeighbour(x, y) {
    return this.roadMask(x, y) !== 0;
  }

  // ------------------------------------------------------------ connectivity
  /** Flood fill over roads: comp[] gets the same id for tiles on one network. */
  recomputeComponents() {
    this.comp.fill(0);
    let next = 0;
    const queue = [];
    for (let y = 0; y < this.n; y++) {
      for (let x = 0; x < this.n; x++) {
        const i = y * this.n + x;
        if (this.tiles[i] !== T.ROAD || this.comp[i]) continue;
        next++;
        this.comp[i] = next;
        queue.length = 0;
        queue.push(x, y);
        while (queue.length) {
          const cy = queue.pop();
          const cx = queue.pop();
          for (const [dx, dy] of N4) {
            const nx = cx + dx;
            const ny = cy + dy;
            if (!this.inBounds(nx, ny)) continue;
            const ni = ny * this.n + nx;
            if (this.tiles[ni] !== T.ROAD || this.comp[ni]) continue;
            this.comp[ni] = next;
            queue.push(nx, ny);
          }
        }
      }
    }
    this.edgeComps.clear();
    for (let i = 0; i < this.n; i++) {
      this._noteEdge(i, 0);
      this._noteEdge(i, this.n - 1);
      this._noteEdge(0, i);
      this._noteEdge(this.n - 1, i);
    }
    this.walkVersion++;
  }

  _noteEdge(x, y) {
    const c = this.comp[y * this.n + x];
    if (c > 0) this.edgeComps.add(c);
  }

  /** True when this road tile belongs to a network that leaves the map. */
  hasGate(x, y) {
    const c = this.componentAt(x, y);
    return c > 0 && this.edgeComps.has(c);
  }

  /** Border road tiles of one network (all of them when comp is omitted). */
  edgeRoads(comp) {
    const out = [];
    for (let i = 0; i < this.n; i++) {
      const pairs = [
        [i, 0],
        [i, this.n - 1],
        [0, i],
        [this.n - 1, i],
      ];
      for (const [x, y] of pairs) {
        if (!this.isRoad(x, y)) continue;
        if (comp && this.comp[y * this.n + x] !== comp) continue;
        out.push({ x, y });
      }
    }
    return out;
  }

  /** How many tiles are zoned at all (any colour). */
  countZones() {
    let n = 0;
    for (let i = 0; i < this.tiles.length; i++) if (this.tiles[i] === T.ZONE_R || this.tiles[i] === T.ZONE_C || this.tiles[i] === T.ZONE_I) n++;
    return n;
  }

  countRoads() {
    let n = 0;
    for (let i = 0; i < this.tiles.length; i++) if (this.tiles[i] === T.ROAD) n++;
    return n;
  }

  componentAt(x, y) {
    if (!inB(this, x, y)) return 0;
    return this.comp[y * this.n + x];
  }

  sameNetwork(a, b) {
    if (!a || !b) return false;
    const ca = this.componentAt(a.x, a.y);
    return ca > 0 && ca === this.componentAt(b.x, b.y);
  }

  /** Breadth-first path over road tiles, returned as tile centres. */
  pathRoads(from, to) {
    if (!from || !to) return null;
    const sx = Math.round(from.x);
    const sy = Math.round(from.y);
    const gx = Math.round(to.x);
    const gy = Math.round(to.y);
    if (!this.isRoad(sx, sy) || !this.isRoad(gx, gy)) return null;
    if (this.comp[sy * this.n + sx] !== this.comp[gy * this.n + gx]) return null;
    if (sx === gx && sy === gy) return [{ x: sx + 0.5, y: sy + 0.5 }];

    const total = this.n * this.n;
    const prev = new Int32Array(total).fill(-1);
    const seen = new Uint8Array(total);
    const q = [sy * this.n + sx];
    seen[q[0]] = 1;
    const goal = gy * this.n + gx;
    let head = 0;
    while (head < q.length) {
      const cur = q[head++];
      if (cur === goal) break;
      const cx = cur % this.n;
      const cy = (cur / this.n) | 0;
      for (const [dx, dy] of N4) {
        const nx = cx + dx;
        const ny = cy + dy;
        if (!this.inBounds(nx, ny)) continue;
        const ni = ny * this.n + nx;
        if (seen[ni] || this.tiles[ni] !== T.ROAD) continue;
        seen[ni] = 1;
        prev[ni] = cur;
        q.push(ni);
      }
    }
    if (!seen[goal]) return null;
    const out = [];
    let cur = goal;
    while (cur !== -1) {
      out.push({ x: (cur % this.n) + 0.5, y: ((cur / this.n) | 0) + 0.5 });
      if (cur === sy * this.n + sx) break;
      cur = prev[cur];
    }
    out.reverse();
    return out;
  }

  /** Nearest road tile around a footprint (used as the front door). */
  doorFor(x, y, w, d) {
    const candidates = [];
    for (let i = x; i < x + w; i++) {
      candidates.push({ x: i, y: y - 1 });
      candidates.push({ x: i, y: y + d });
    }
    for (let j = y; j < y + d; j++) {
      candidates.push({ x: x - 1, y: j });
      candidates.push({ x: x + w, y: j });
    }
    let best = null;
    for (const c of candidates) {
      if (!this.isRoad(c.x, c.y)) continue;
      if (!best || this.comp[c.y * this.n + c.x] > this.comp[best.y * this.n + best.x]) best = c;
    }
    return best;
  }

  // --------------------------------------------------------------- buildings
  canBuild(kind, x, y, w, d, zone) {
    for (let j = y; j < y + d; j++) {
      for (let i = x; i < x + w; i++) {
        if (!this.inBounds(i, j)) return false;
        const t = this.get(i, j);
        if (t === T.ROAD || t === T.WATER) return false;
        if (this.occ[j * this.n + i] >= 0) return false;
        if (zone && t !== zone) return false;
      }
    }
    const door = this.doorFor(x, y, w, d);
    if (!door) return false;
    if (this.comp[door.y * this.n + door.x] === 0) return false;
    return true;
  }

  addBuilding(kind, x, y, w, d, extra = {}) {
    const door = this.doorFor(x, y, w, d);
    if (!door) return null;
    const b = {
      id: nextBuildingId++,
      kind,
      x,
      y,
      w,
      d,
      door,
      doorSide: door.y >= y + d ? 'x' : 'y',
      variant: extra.variant ?? 0,
      state: extra.state || 'done',
      progress: extra.progress || 0,
      work: 0,
      ...extra,
    };
    this.buildings.push(b);
    for (let j = y; j < y + d; j++) {
      for (let i = x; i < x + w; i++) this.occ[j * this.n + i] = b.id;
    }
    this.version++;
    return b;
  }

  removeBuilding(b) {
    const i = this.buildings.indexOf(b);
    if (i < 0) return;
    this.buildings.splice(i, 1);
    for (let j = b.y; j < b.y + b.d; j++) {
      for (let i2 = b.x; i2 < b.x + b.w; i2++) {
        if (this.occ[j * this.n + i2] === b.id) this.occ[j * this.n + i2] = -1;
      }
    }
    this.version++;
  }

  // ------------------------------------------------------------------- props
  addProp(prop) {
    this.props.push(prop);
    return prop;
  }

  removePropsIn(x, y, w = 1, d = 1) {
    this.props = this.props.filter((p) => !(p.x >= x && p.y >= y && p.x < x + w && p.y < y + d));
  }

  /** Furnish a park tile: benches, trees, flowers, the odd fountain. */
  decoratePark(x, y) {
    this.removePropsIn(x, y);
    const h = hash(x, y);
    if (h % 3 === 0) this.props.push({ kind: 'tree', x: x + 0.5, y: y + 0.5, s: 1 });
    if (h % 5 === 1) this.props.push({ kind: 'bench', x: x + 0.5, y: y + 0.8, rot: h % 2 });
    if (h % 7 === 2) this.props.push({ kind: 'flowers', x: x + 0.34, y: y + 0.4 });
    if (h % 11 === 3) this.props.push({ kind: 'lamp', x: x + 0.76, y: y + 0.3 });
    if (h % 13 === 4) this.props.push({ kind: 'fountain', x: x + 0.1, y: y + 0.1 });
  }

  clearProps() {
    this.props = this.props.filter((p) => p.kind === 'tuft');
  }
}

function inB(world, x, y) {
  return x >= 0 && y >= 0 && x < world.n && y < world.n;
}

function hash(x, y) {
  let h = (x * 374761393 + y * 668265263) >>> 0;
  h = (h ^ (h >>> 13)) * 1274126177;
  return (h ^ (h >>> 16)) >>> 0;
}
