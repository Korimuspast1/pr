// City simulation.
//
// Two rules hold the whole model together:
//   1. A walker is only ever on a road tile, so nobody can cut through a wall.
//   2. Money is never printed inside the city: wages come out of a business
//      till, construction is paid by the city budget and lands in the builders'
//      pockets. The only outside money is the savings a newcomer brings and the
//      savings a leaver takes away - both are reported separately in the ledger.

import { World, N, T } from './world.js';

export const MIN_PER_SEC = 10; // game minutes per real second at 1x speed
export const HOUR = 60;

export const COSTS = {
  road: 12,
  park: 60,
  house: 100,
  shop: 150,
  factory: 240,
};

// working capital the town hands to a new business so it can stock its shelves
export const CAPITAL = { shop: 100, factory: 140 };

const HOURS = 8; // length of a working shift

const NAMES = [
  'Аня', 'Борис', 'Вера', 'Глеб', 'Даша', 'Егор', 'Жанна', 'Захар', 'Инна', 'Кирилл',
  'Лена', 'Марк', 'Нина', 'Олег', 'Полина', 'Роман', 'Соня', 'Тимур', 'Ульяна', 'Фёдор',
  'Эля', 'Юра', 'Яна', 'Артём', 'Влада', 'Денис', 'Ева', 'Жора', 'Зоя', 'Илья',
  'Ксюша', 'Лёва', 'Мила', 'Назар', 'Оля', 'Паша', 'Рита', 'Сева', 'Тоня', 'Филипп',
];

const SHIRTS = ['#e0574d', '#3f7fb5', '#e2a33c', '#5aa469', '#8c6bb1', '#d9739b', '#4aa39c', '#c9713f'];
const SKINS = ['#e8bb92', '#d9a276', '#c78a5f', '#f0cba6'];

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const rnd = (rng, a, b) => a + rng() * (b - a);

let nextCitizenId = 1;
let nextFirmId = 1;

function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export class Sim {
  constructor(seed = 3) {
    this.rng = mulberry32(seed);
    this.world = new World(N);
    this.day = 1;
    this.absMinute = 7 * HOUR;
    this.minute = 7 * HOUR;
    this.speed = 1;
    this.paused = false;
    this.pending = 0;

    this.budget = 900;
    this.citizens = [];
    this.firms = [];
    this.sites = [];
    this.trucks = [];
    this.nameIndex = 0;
    this.colors = 0;

    this.ledger = this.emptyLedger();
    this.external = { in: 0, out: 0 };
    this.works = 0; // road & park money waiting to be paid out as wages

    this.seedTown();
    this.world.recomputeComponents();
    this.assignJobs(); // roads exist now, so the founders can actually get hired
    this.initialBudget = this.budget;
    this.initialMoney = this.internalMoney();
    this.year = 1;
  }

  emptyLedger() {
    return {
      wages: 0,
      taxes: 0,
      rent: 0,
      sales: 0,
      restock: 0,
      construction: 0,
      welfare: 0,
      cityWages: 0,
      migrationIn: 0,
      migrationOut: 0,
    };
  }

  // ------------------------------------------------------------- the start
  seedTown() {
    const w = this.world;
    for (let x = 13; x <= 26; x++) w.set(x, 20, T.ROAD);
    for (let y = 16; y <= 24; y++) w.set(20, y, T.ROAD);

    const hall = w.addBuilding('cityhall', 16, 17, 3, 3, { variant: 0 });
    this.addFirm(hall, { public: true });

    const shop = w.addBuilding('shop', 21, 19, 1, 1, { variant: 0 });
    const shopFirm = this.addFirm(shop, { capital: 240 });
    shopFirm.stock = 18;

    const factory = w.addBuilding('factory', 22, 21, 2, 2, { variant: 0 });
    const factoryFirm = this.addFirm(factory, { capital: 320 });
    factoryFirm.stock = 10;

    const houses = [
      w.addBuilding('house', 14, 21, 1, 1, { variant: 0 }),
      w.addBuilding('house', 16, 21, 1, 1, { variant: 1 }),
      w.addBuilding('house', 18, 21, 1, 1, { variant: 2 }),
      w.addBuilding('house', 19, 22, 1, 1, { variant: 0 }),
    ];
    for (const h of houses) h.residents = [];

    // the founders: two people per house, six jobs are waiting for them
    for (const h of houses) {
      for (let i = 0; i < 2; i++) this.spawnCitizen(h, { founding: true });
    }
    this.assignJobs();
  }

  addFirm(building, opts = {}) {
    const firm = {
      id: nextFirmId++,
      building,
      public: !!opts.public,
      kind: building.kind,
      cash: opts.capital ?? 0,
      stock: 0,
      workers: [],
      wage: building.kind === 'cityhall' ? 42 : building.kind === 'shop' ? 52 : 56,
      baseWage: building.kind === 'cityhall' ? 42 : building.kind === 'shop' ? 52 : 56,
      price: building.kind === 'shop' ? 11 : building.kind === 'factory' ? 6.5 : 0,
      basePrice: building.kind === 'shop' ? 11 : building.kind === 'factory' ? 6.5 : 0,
      revenueToday: 0,
      wageBillToday: 0,
      soldToday: 0,
      boughtToday: 0,
      producedToday: 0,
      salesEwma: building.kind === 'shop' ? 8 : 12,
      arrears: 0,
    };
    firm.jobs = opts.public ? 2 : building.kind === 'shop' ? 1 : 3;
    this.firms.push(firm);
    building.firm = firm;
    if (opts.public) this.cityHall = firm;
    return firm;
  }

  // ---------------------------------------------------------------- people
  spawnCitizen(home, opts = {}) {
    const c = {
      id: nextCitizenId++,
      name: NAMES[this.nameIndex++ % NAMES.length],
      color: SHIRTS[this.colors % SHIRTS.length],
      skin: SKINS[this.colors % SKINS.length],
      home: home || null,
      work: null,
      x: 0,
      y: 0,
      path: null,
      pathIndex: 0,
      state: 'home',
      stateSince: 0,
      cash: opts.founding ? 80 : 0,
      hunger: 0.3,
      mood: 0.7,
      unpaid: 0,
      jobless: 0,
      moving: false,
      phase: Math.random() * 6.28,
      savedToday: 0,
      spentToday: 0,
      earnedToday: 0,
      visible: false,
    };
    if (home) {
      home.residents.push(c);
      c.x = home.door.x + 0.5;
      c.y = home.door.y + 0.5;
      c.state = 'home';
    }
    if (!opts.founding) {
      // newcomers bring their own savings: it is outside money, reported as such
      c.cash = 60;
      this.external.in += c.cash;
      this.ledger.migrationIn += c.cash;
      const edge = this.edgeRoadTile();
      c.x = edge.x + 0.5;
      c.y = edge.y + 0.5;
      c.state = 'newcomer';
      this.walkTo(c, home ? home.door : edge);
    }
    this.citizens.push(c);
    return c;
  }

  edgeRoadTile() {
    const w = this.world;
    const roads = [];
    for (let i = 0; i < w.n; i++) {
      if (w.isRoad(i, 0)) roads.push({ x: i, y: 0 });
      if (w.isRoad(i, w.n - 1)) roads.push({ x: i, y: w.n - 1 });
      if (w.isRoad(0, i)) roads.push({ x: 0, y: i });
      if (w.isRoad(w.n - 1, i)) roads.push({ x: w.n - 1, y: i });
    }
    if (roads.length) return roads[Math.floor(this.rng() * roads.length)];
    // no road reaches the border yet: come in at the city hall
    const hall = this.cityHall?.building;
    return hall ? hall.door : { x: 20, y: 20 };
  }

  tileOf(pos) {
    return { x: Math.floor(pos.x), y: Math.floor(pos.y) };
  }

  walkTo(citizen, targetTile) {
    const from = this.tileOf(citizen);
    const path = this.world.pathRoads(from, targetTile);
    if (!path || path.length < 1) {
      citizen.path = null;
      return false;
    }
    let start = 0;
    // skip the tile we are already standing on
    if (path.length > 1 && Math.hypot(path[0].x - citizen.x, path[0].y - citizen.y) < 0.3) start = 1;
    citizen.path = path;
    citizen.pathIndex = start;
    if (start >= path.length) citizen.path = null;
    return true;
  }

  setState(c, state) {
    c.state = state;
    c.stateSince = this.absMinute;
  }

  // -------------------------------------------------------------- schedule
  tickMinute() {
    this.absMinute++;
    this.minute = this.absMinute % 1440;
    if (this.minute === 0) {
      this.day++;
    }
    if (this.minute % 60 === 0) this.hourly(this.minute);

    switch (this.minute) {
      case 7 * HOUR:
        this.morning();
        break;
      case 8 * HOUR:
        this.shiftStart();
        break;
      case 16 * HOUR:
        this.shiftEnd();
        break;
      case 18 * HOUR + 30:
        this.evening();
        break;
      case 20 * HOUR:
        this.lateShopping();
        break;
      case 22 * HOUR + 30:
        this.night();
        break;
      case 30:
        this.dailyBooks();
        break;
      default:
        break;
    }
  }

  hourly(minute) {
    for (let i = this.firms.length - 1; i >= 0; i--) this.firmHour(this.firms[i], minute);
    this.buildersHour();
    this.cityCrewHour();
    this.develop(minute);
    this.maybeMigrate(minute);
    this.checkDepartures();
  }

  firmHour(firm, minute) {
    const atWork = minute >= 8 * HOUR && minute < 16 * HOUR;
    const present = firm.workers.filter((c) => c.state === 'work' || c.state === 'toWork');
    if (atWork && present.length) {
      const due = (firm.wage / HOURS) * present.length;
      const pool = firm.public ? Math.min(this.budget, due) : Math.min(firm.cash, due);
      if (pool > 0.01) {
        if (firm.public) this.budget -= pool;
        else firm.cash -= pool;
        const each = pool / present.length;
        for (const c of present) {
          const tax = each * 0.15;
          c.cash += each - tax;
          c.earnedToday += each - tax;
          this.budget += tax;
          this.ledger.taxes += tax;
          if (firm.public) this.ledger.cityWages += each;
          else {
            firm.wageBillToday += each;
            this.ledger.wages += each;
          }
        }
      }
      const missing = due - pool;
      if (missing > 0.01) firm.arrears += missing;
      else firm.arrears = Math.max(0, firm.arrears - 4);
    }

    if (firm.kind === 'factory' && atWork) {
      const workers = firm.workers.filter((c) => c.state === 'work').length;
      if (workers) {
        firm.stock += workers * 1.05;
        firm.producedToday += workers * 1.05;
      }
    }

    if (firm.kind === 'shop') {
      const target = Math.min(26, Math.max(6, firm.salesEwma * 2.4));
      if (firm.stock < target) {
        const supplier = this.factoryWithStock();
        if (supplier) {
          const qty = Math.floor(Math.min(target - firm.stock, supplier.stock, firm.cash / supplier.price));
          if (qty > 0) {
            const total = qty * supplier.price;
            firm.cash -= total;
            supplier.cash += total;
            firm.stock += qty;
            supplier.stock -= qty;
            supplier.soldToday += qty;
            supplier.revenueToday += total;
            firm.boughtToday += qty;
            this.ledger.restock += total;
            this.spawnTruck(supplier.building, firm.building);
          }
        }
      }
    }
  }

  factoryWithStock() {
    let best = null;
    for (const f of this.firms) {
      if (f.kind !== 'factory' || f.stock < 1) continue;
      if (!best || f.price < best.price) best = f;
    }
    return best;
  }

  /** Idle townsfolk take city jobs: they build roads and parks for the money
   *  the town has already set aside in the works fund. */
  cityCrewHour() {
    if (this.minute < 7 * HOUR || this.minute > 20 * HOUR) return;
    if (this.works <= 0.01) return;
    const crew = this.citizens.filter((c) => !c.work && !c.site && c.state !== 'asleep' && c.state !== 'leaving');
    if (!crew.length) return;
    const budgetForHour = Math.min(this.works, crew.length * 14);
    const each = budgetForHour / crew.length;
    this.works -= budgetForHour;
    for (const c of crew) {
      const tax = each * 0.12;
      c.cash += each - tax;
      c.earnedToday += each - tax;
      this.budget += tax;
      this.ledger.taxes += tax;
      this.ledger.construction += each;
    }
  }

  buildersHour() {
    if (this.minute < 7 * HOUR || this.minute > 20 * HOUR) return;
    const active = this.sites.filter((s) => s.state === 'site');
    if (!active.length) return;
    for (const site of active) {
      if (!site.builders.length) continue;
      site.work += site.builders.length * 2.6;
      const due = Math.min(site.escrow, site.builders.length * 14);
      if (due > 0.01) {
        site.escrow -= due;
        const each = due / site.builders.length;
        for (const c of site.builders) {
          const tax = each * 0.12;
          c.cash += each - tax;
          c.earnedToday += each - tax;
          this.budget += tax;
          this.ledger.taxes += tax;
          this.ledger.construction += each;
        }
      }
      if (site.progressNeeded && site.work >= site.progressNeeded) this.finishSite(site);
    }
  }

  finishSite(site) {
    const b = site.building;
    b.state = 'done';
    // leftover escrow goes back to the budget, it is still the town's money
    this.budget += site.escrow;
    site.escrow = 0;
    for (const c of site.builders) {
      c.site = null;
    }
    site.builders = [];
    if (b.kind !== 'house' && b.kind !== 'park') this.addFirm(b, { capital: site.capital });
    if (b.kind === 'house') b.residents = b.residents || [];
    const idx = this.sites.indexOf(site);
    if (idx >= 0) this.sites.splice(idx, 1);
    this.world.version++;
  }

  // ------------------------------------------------------------ development
  freeHomeSlots() {
    let free = 0;
    for (const b of this.world.buildings) {
      if (b.kind !== 'house' || b.state !== 'done') continue;
      free += 2 - (b.residents ? b.residents.length : 0);
    }
    return free;
  }

  vacancies() {
    let v = 0;
    for (const f of this.firms) v += Math.max(0, f.jobs - f.workers.length);
    return v;
  }

  countDone(kind) {
    return this.world.buildings.filter((b) => b.kind === kind && b.state === 'done').length;
  }

  unemployedCount() {
    return this.citizens.filter((c) => !c.work).length;
  }

  /**
   * Classic RCI logic: people follow jobs, businesses follow people.
   * A house only grows when there are more jobs than spare beds, a business
   * only grows when there are people without work or shelves running dry.
   */
  demand(kind) {
    const pop = this.citizens.length;
    if (kind === 'house') return this.vacancies() > this.freeHomeSlots() && pop < 70;
    if (kind === 'shop') {
      if (pop < 4) return false;
      return this.unemployedCount() >= 1 || this.countDone('shop') * 5 < pop;
    }
    if (kind === 'factory') {
      const shops = this.countDone('shop');
      if (!shops) return false;
      return this.unemployedCount() >= 2 || this.countDone('factory') * 12 < shops * 10;
    }
    return false;
  }

  /**
   * What the town needs next: a bed for a worker, a shop when shelves are
   * empty, or a workplace when people are idle. One lot per hour at most.
   */
  chooseKind() {
    const pop = this.citizens.length;
    const shops = this.countDone('shop');
    const factories = this.countDone('factory');
    const emptyShelves = this.firms.filter((f) => f.kind === 'shop' && f.stock < 3).length;
    const jobNeed = this.unemployedCount();
    const bedNeed = this.vacancies() - this.freeHomeSlots();

    if (emptyShelves > 0 && shops > 0 && factories * 12 < shops * 10) return 'factory';
    if (bedNeed > 0 && pop < 70) return 'house';
    if (jobNeed >= 2 && shops < Math.max(1, Math.floor(pop / 4) + 1)) return 'shop';
    if (jobNeed >= 2) return 'factory';
    if (jobNeed >= 1 && shops === 0) return 'shop';
    return null;
  }

  /** Build one new lot of whatever the town needs, if the budget allows. */
  develop(minute) {
    if (minute < 6 * HOUR || minute > 21 * HOUR) return;
    if (this.sites.length >= 3) return;
    const kind = this.chooseKind();
    if (!kind) return;
    const zone = kind === 'house' ? T.ZONE_R : kind === 'shop' ? T.ZONE_C : T.ZONE_I;
    const size = kind === 'factory' ? 2 : 1;
    const needed = COSTS[kind] + (CAPITAL[kind] || 0);
    if (this.budget < needed + 40) return;
    // houses grow where there are jobs, businesses grow where people live
    const compFilter = kind === 'house' ? this.jobComponents() : this.residentComponents();
    const spot = this.findSpot(zone, size, compFilter);
    if (!spot) return;
    this.startSite(kind, spot.x, spot.y, size);
  }

  /** Road networks that have people living on them. */
  residentComponents() {
    const set = new Set();
    for (const b of this.world.buildings) {
      if (b.kind !== 'house' || b.state !== 'done') continue;
      set.add(this.world.componentAt(b.door.x, b.door.y));
    }
    set.delete(0);
    return set;
  }

  /** Road networks that have at least one workplace. */
  jobComponents() {
    const set = new Set();
    for (const f of this.firms) set.add(this.world.componentAt(f.building.door.x, f.building.door.y));
    set.delete(0);
    return set;
  }

  findSpot(zone, size, compFilter) {
    const w = this.world;
    const candidates = [];
    for (let y = 0; y < w.n; y++) {
      for (let x = 0; x < w.n; x++) {
        if (w.get(x, y) !== zone) continue;
        if (w.occ[y * w.n + x] >= 0) continue;
        candidates.push({ x, y });
      }
    }
    // shuffle so growth does not follow the same corner every time
    for (let i = candidates.length - 1; i > 0; i--) {
      const j = Math.floor(this.rng() * (i + 1));
      [candidates[i], candidates[j]] = [candidates[j], candidates[i]];
    }
    for (const c of candidates) {
      if (!w.canBuild('x', c.x, c.y, size, size, zone)) continue;
      if (compFilter && compFilter.size) {
        const door = w.doorFor(c.x, c.y, size, size);
        if (!door || !compFilter.has(w.componentAt(door.x, door.y))) continue;
      }
      // do not grow on a tile another site is waiting for
      const taken = this.sites.some((s) => c.x >= s.x && c.y >= s.y && c.x < s.x + s.w && c.y < s.y + s.d);
      if (taken) continue;
      return c;
    }
    return null;
  }

  startSite(kind, x, y, size) {
    const cost = COSTS[kind];
    const capital = CAPITAL[kind] || 0;
    this.budget -= cost + capital;
    this.ledger.construction += cost;
    const b = this.world.addBuilding(kind, x, y, size, size, { state: 'site', progress: 0 });
    if (!b) {
      this.budget += cost + capital;
      return null;
    }
    const site = {
      building: b,
      x,
      y,
      w: size,
      d: size,
      state: 'site',
      work: 0,
      progressNeeded: kind === 'house' ? 8 : kind === 'shop' ? 12 : 20,
      escrow: cost,
      capital,
      builders: [],
    };
    b.site = site;
    this.sites.push(site);
    this.assignBuilders();
    return site;
  }

  assignBuilders() {
    const active = this.sites.filter((s) => s.state === 'site');
    for (const s of active) {
      while (s.builders.length < 2) {
        const free = this.citizens.find((c) => !c.work && !c.site && c.state !== 'asleep');
        if (!free) break;
        free.site = s;
        s.builders.push(free);
      }
    }
  }

  // -------------------------------------------------------------- migration
  maybeMigrate(minute) {
    if (minute % 2 !== 0) return;
    // people move in when there is a bed free and the town still has work
    if (this.freeHomeSlots() <= 0) return;
    if (this.unemployedCount() >= 4) return;
    if (this.citizens.length >= 60) return;
    if (this.rng() > 0.5) return;
    const home = this.world.buildings.find(
      (b) => b.kind === 'house' && b.state === 'done' && (b.residents || []).length < 2,
    );
    if (!home) return;
    this.spawnCitizen(home, {});
    this.assignJobs();
  }

  checkDepartures() {
    for (let i = this.citizens.length - 1; i >= 0; i--) {
      const c = this.citizens[i];
      if (c.state === 'leaving') continue;
      if (this.minute % 60 !== 0) continue;
      if (c.unpaid >= 3 || c.hunger > 1.9) {
        if (c.mood < 0.3) this.startLeaving(c);
      }
    }
  }

  startLeaving(c) {
    c.state = 'leaving';
    c.path = null;
    if (c.work) {
      c.work.workers = c.work.workers.filter((w) => w !== c);
      c.work = null;
    }
    if (c.home) c.home.residents = c.home.residents.filter((r) => r !== c);
    if (c.site) {
      c.site.builders = c.site.builders.filter((b) => b !== c);
      c.site = null;
    }
    const edge = this.edgeRoadTile();
    this.walkTo(c, edge);
  }

  removeCitizen(c) {
    const i = this.citizens.indexOf(c);
    if (i >= 0) this.citizens.splice(i, 1);
  }

  // --------------------------------------------------------------- jobs
  assignJobs() {
    for (const c of this.citizens) {
      if (c.work || c.state === 'leaving') continue;
      const firm = this.firmWithVacancy(c);
      if (!firm) continue;
      firm.workers.push(c);
      c.work = firm;
      if (c.site) {
        c.site.builders = c.site.builders.filter((b) => b !== c);
        c.site = null;
      }
    }
  }

  firmWithVacancy(c) {
    let best = null;
    let bestDist = Infinity;
    const here = c.home ? c.home.door : this.tileOf(c);
    for (const f of this.firms) {
      if (f.workers.length >= f.jobs) continue;
      if (!this.world.sameNetwork(f.building.door, here)) continue;
      const d = Math.hypot(f.building.door.x - c.x, f.building.door.y - c.y);
      if (d < bestDist) {
        bestDist = d;
        best = f;
      }
    }
    return best;
  }

  // ------------------------------------------------------------- daily life
  morning() {
    for (const c of this.citizens) {
      if (c.state === 'leaving') continue;
      c.visible = true;
      if (c.work) {
        this.setState(c, 'toWork');
        this.walkTo(c, c.work.building.door);
        c.visible = true;
      } else if (c.site) {
        this.setState(c, 'toSite');
        this.walkTo(c, c.site.building.door);
      } else {
        this.setState(c, 'idle');
        this.wander(c);
      }
    }
    this.assignJobs();
  }

  shiftStart() {
    for (const c of this.citizens) {
      if (c.state === 'toWork' || c.state === 'toSite' || c.state === 'idle' || c.state === 'newcomer') {
        if (c.work) this.setState(c, 'work');
        else if (c.site) this.setState(c, 'builder');
      }
    }
  }

  shiftEnd() {
    for (const c of this.citizens) {
      if (c.state === 'work') {
        this.setState(c, 'toShop');
        if (c.hunger > 0.25 && !this.goShopping(c)) this.goHomeState(c);
      } else if (c.state === 'builder') {
        this.goHomeState(c);
      }
    }
  }

  evening() {
    for (const c of this.citizens) {
      if (c.state === 'toWork' || c.state === 'idle') {
        if (c.hunger > 0.5 && this.goShopping(c)) continue;
        this.goHomeState(c);
      }
    }
  }

  lateShopping() {
    for (const c of this.citizens) {
      if (c.state === 'home' && c.hunger > 1.1) this.goShopping(c);
    }
  }

  night() {
    for (const c of this.citizens) {
      if (c.state === 'leaving') continue;
      if (c.home) {
        c.x = c.home.door.x + 0.5;
        c.y = c.home.door.y + 0.5;
        c.path = null;
        c.visible = false;
        this.setState(c, 'sleep');
      } else {
        this.startLeaving(c);
      }
    }
  }

  goHomeState(c) {
    if (!c.home) {
      this.startLeaving(c);
      return;
    }
    this.setState(c, 'toHome');
    this.walkTo(c, c.home.door);
  }

  goShopping(c) {
    const shop = this.nearestShop(c);
    if (!shop) return false;
    c.shopTarget = shop;
    this.setState(c, 'toShop');
    this.walkTo(c, shop.building.door);
    return true;
  }

  nearestShop(c) {
    let best = null;
    let bestScore = -Infinity;
    for (const f of this.firms) {
      if (f.kind !== 'shop' || f.stock < 1) continue;
      if (!this.world.sameNetwork(f.building.door, this.tileOf(c))) continue;
      const d = Math.hypot(f.building.door.x - c.x, f.building.door.y - c.y);
      const score = f.stock * 6 - d;
      if (score > bestScore) {
        bestScore = score;
        best = f;
      }
    }
    return best;
  }

  wander(c) {
    const w = this.world;
    for (let attempt = 0; attempt < 12; attempt++) {
      const x = Math.floor(this.rng() * w.n);
      const y = Math.floor(this.rng() * w.n);
      if (!w.isRoad(x, y)) continue;
      this.walkTo(c, { x, y });
      return;
    }
  }

  // ------------------------------------------------------------- purchasing
  buy(c) {
    const shop = c.shopTarget || this.nearestShop(c);
    if (!shop || shop.stock < 1) return false;
    const want = c.hunger > 0.9 ? 2 : 1;
    let bought = 0;
    for (let i = 0; i < want && shop.stock >= 1; i++) {
      const price = shop.price;
      if (c.cash >= price) {
        c.cash -= price;
        shop.cash += price;
        shop.stock -= 1;
        shop.soldToday += 1;
        shop.revenueToday += price;
        c.spentToday += price;
        this.ledger.sales += price;
        const tax = price * 0.1;
        shop.cash -= tax;
        this.budget += tax;
        this.ledger.taxes += tax;
        bought++;
      } else if (this.budget >= price) {
        // the town feeds its poorest citizens; the money still goes to the shop
        this.budget -= price;
        shop.cash += price;
        shop.stock -= 1;
        shop.soldToday += 1;
        shop.revenueToday += price;
        this.ledger.welfare += price;
        bought++;
      } else break;
    }
    if (bought) {
      c.hunger = clamp(c.hunger - 0.75 * bought, 0, 2);
      c.mood = clamp(c.mood + 0.04, 0, 1);
      shop.salesEwma = shop.salesEwma * 0.9 + bought * 0.9;
      return true;
    }
    c.mood = clamp(c.mood - 0.03, 0, 1);
    return false;
  }

  // ------------------------------------------------------------- money books
  dailyBooks() {
    // rent and business rates keep the city budget alive
    for (const c of this.citizens) {
      const rent = 12;
      const pay = Math.min(rent, Math.max(0, c.cash - 4));
      if (pay > 0.01) {
        c.cash -= pay;
        this.budget += pay;
        this.ledger.rent += pay;
      } else {
        c.unpaid += 1;
      }
    }
    for (const f of this.firms) {
      if (f.public) continue;
      const tax = Math.max(0, f.revenueToday) * 0.12;
      if (tax > 0.01) {
        f.cash -= tax;
        this.budget += tax;
        this.ledger.taxes += tax;
      }
      this.adaptFirm(f);
      f.revenueToday = 0;
      f.wageBillToday = 0;
      f.soldToday = 0;
      f.boughtToday = 0;
      f.producedToday = 0;
    }
    for (const c of this.citizens) {
      c.hunger = clamp(c.hunger + 0.6, 0, 2);
      if (c.hunger < 0.9) c.mood = clamp(c.mood + 0.05, 0, 1);
      else c.mood = clamp(c.mood - 0.05, 0, 1);
      if (c.work) c.jobless = 0;
      else c.jobless += 1;
      if (c.jobless > 4) c.mood = clamp(c.mood - 0.06, 0, 1);
      c.earnedToday = 0;
      c.spentToday = 0;
    }
    for (const b of this.world.buildings) {
      if (b.kind === 'house' && b.state === 'done' && (b.residents || []).length === 0 && b.emptyDays !== undefined) {
        // empty houses stay for a while, they keep their zone
      }
    }
    this.ledger = this.emptyLedger();
    this.assignJobs();
    this.assignBuilders();
  }

  adaptFirm(f) {
    const bill = f.jobs * f.wage;
    if (f.kind === 'shop') {
      const unitCost = (f.boughtToday ? 0 : 0) + 0;
      const sold = Math.max(1, f.salesEwma);
      const supplierPrice = this.firms.reduce((a, x) => (x.kind === 'factory' ? Math.min(a, x.price) : a), 99);
      let target = (supplierPrice * 1.9 + bill / Math.max(4, sold)) * 1.08;
      target = clamp(target, 6, 24);
      f.price += (target - f.price) * 0.25;
      f.price = Math.round(f.price * 10) / 10;
      f.salesEwma *= 0.82;
    } else if (f.kind === 'factory') {
      const sold = Math.max(2, f.salesEwma);
      let target = (bill / Math.max(6, sold)) * 1.5;
      target = clamp(target, 3, 14);
      f.price += (target - f.price) * 0.2;
      f.price = Math.round(f.price * 10) / 10;
      f.salesEwma *= 0.88;
    }
    if (f.public) return;
    if (f.cash < bill * 1.2) f.wage = Math.max(f.baseWage * 0.7, f.wage * 0.97);
    else if (f.cash > bill * 4) f.wage = Math.min(f.baseWage * 1.35, f.wage * 1.02);
    f.wage = Math.round(f.wage * 10) / 10;
  }

  // ------------------------------------------------------------------ money
  internalMoney() {
    let sum = this.budget;
    for (const c of this.citizens) sum += c.cash;
    for (const f of this.firms) sum += f.cash;
    for (const s of this.sites) sum += s.escrow + (s.capital || 0);
    sum += this.works;
    return sum;
  }

  /** Internal money may only change by the savings that crossed the border. */
  audit() {
    return this.internalMoney() - this.initialMoney - this.external.in + this.external.out;
  }

  // ---------------------------------------------------------------- trucks
  spawnTruck(from, to) {
    if (this.trucks.length > 3) return;
    const path = this.world.pathRoads(from.door, to.door);
    if (!path || path.length < 2) return;
    this.trucks.push({ path, index: 1, x: path[0].x, y: path[0].y, from, to });
  }

  // ------------------------------------------------------------------ tools
  buildRoad(x, y) {
    const w = this.world;
    if (!w.inBounds(x, y)) return { ok: false };
    const t = w.get(x, y);
    if (t === T.ROAD) return { ok: false };
    // never wipe a building by accident: the bulldozer is there for that
    if (w.occ[y * w.n + x] >= 0) return { ok: false, reason: 'тут здание, сначала снос' };
    if (this.budget < COSTS.road) return { ok: false, reason: 'нет денег на дорогу' };
    this.budget -= COSTS.road;
    this.works += COSTS.road;
    this.ledger.construction += COSTS.road;
    w.removePropsIn(x, y);
    w.set(x, y, T.ROAD);
    w.recomputeComponents();
    return { ok: true, cost: COSTS.road };
  }

  zone(x, y, kind) {
    const w = this.world;
    if (!w.inBounds(x, y)) return { ok: false };
    if (w.get(x, y) === T.ROAD || w.occ[y * w.n + x] >= 0) return { ok: false };
    const type = kind === 'r' ? T.ZONE_R : kind === 'c' ? T.ZONE_C : T.ZONE_I;
    if (w.get(x, y) === type) return { ok: false };
    w.removePropsIn(x, y);
    w.set(x, y, type);
    w.version++;
    return { ok: true };
  }

  buildPark(x, y) {
    const w = this.world;
    if (!w.inBounds(x, y)) return { ok: false };
    if (w.get(x, y) === T.PARK) return { ok: false };
    if (w.occ[y * w.n + x] >= 0) return { ok: false };
    if (this.budget < COSTS.park) return { ok: false, reason: 'нет денег на парк' };
    this.budget -= COSTS.park;
    this.works += COSTS.park;
    this.ledger.construction += COSTS.park;
    w.set(x, y, T.PARK);
    w.decoratePark(x, y);
    return { ok: true, cost: COSTS.park };
  }

  bulldoze(x, y) {
    const w = this.world;
    if (!w.inBounds(x, y)) return { ok: false };
    const b = w.buildingAt(x, y);
    if (b) {
      this.demolishBuilding(b);
      return { ok: true };
    }
    const t = w.get(x, y);
    if (t === T.GRASS) return { ok: false };
    w.removePropsIn(x, y);
    w.set(x, y, T.GRASS);
    if (t === T.ROAD) w.recomputeComponents();
    return { ok: true };
  }

  demolishBuilding(b) {
    const w = this.world;
    if (b.firm) {
      for (const c of b.firm.workers) c.work = null;
      // the till is the town's money until the owner spends it
      if (!b.firm.public && b.firm.cash > 0) {
        this.budget += b.firm.cash;
        b.firm.cash = 0;
      }
      this.firms = this.firms.filter((f) => f !== b.firm);
      if (b.firm === this.cityHall) this.cityHall = null;
    }
    if (b.kind === 'house' && b.residents) {
      for (const c of b.residents.slice()) {
        this.startLeaving(c);
      }
    }
    if (b.site) {
      this.budget += b.site.escrow + (b.site.capital || 0);
      b.site.escrow = 0;
      b.site.capital = 0;
      this.sites = this.sites.filter((s) => s !== b.site);
      for (const c of b.site.builders) c.site = null;
    }
    for (let j = b.y; j < b.y + b.d; j++) {
      for (let i = b.x; i < b.x + b.w; i++) {
        w.set(i, j, T.GRASS);
        w.removePropsIn(i, j);
      }
    }
    w.removeBuilding(b);
  }

  // ------------------------------------------------------------------ clock
  update(dtReal) {
    if (this.paused) return;
    const dt = Math.min(dtReal, 0.12) * this.speed;
    this.pending += dt * MIN_PER_SEC;
    let guard = 0;
    while (this.pending >= 1 && guard++ < 240) {
      this.pending -= 1;
      this.tickMinute();
    }
    for (const c of this.citizens) this.updateCitizen(c, dt);
    for (let i = this.trucks.length - 1; i >= 0; i--) {
      if (this.updateTruck(this.trucks[i], dt)) this.trucks.splice(i, 1);
    }
  }

  updateCitizen(c, dt) {
    if (c.state === 'home' || c.state === 'sleep' || c.state === 'work' || c.state === 'builder') {
      c.moving = false;
      if (c.state === 'home') {
        if (this.minute >= 8 * HOUR && this.minute < 16 * HOUR && c.work) {
          this.setState(c, 'toWork');
          this.walkTo(c, c.work.building.door);
        }
      }
      return;
    }

    const arrived = this.advance(c, dt);

    switch (c.state) {
      case 'newcomer':
        if (arrived) {
          this.setState(c, 'home');
          c.visible = false;
        }
        break;
      case 'toWork':
        if (arrived) {
          this.setState(c, 'work');
          c.visible = false;
        }
        break;
      case 'toShop':
        if (arrived) {
          this.buy(c);
          this.goHomeState(c);
        }
        break;
      case 'toHome':
        if (arrived) {
          this.setState(c, 'home');
          c.visible = false;
        }
        break;
      case 'toSite':
        if (arrived) this.setState(c, 'builder');
        break;
      case 'leaving':
        if (arrived) {
          this.external.out += c.cash;
          this.ledger.migrationOut += c.cash;
          this.removeCitizen(c);
        }
        break;
      default:
        if (arrived) this.setState(c, 'home');
        break;
    }
  }

  advance(c, dt) {
    if (!c.path || c.pathIndex >= c.path.length) {
      c.moving = false;
      return true;
    }
    const speed = 1.8;
    let remain = speed * dt;
    c.moving = false;
    while (remain > 0 && c.pathIndex < c.path.length) {
      const p = c.path[c.pathIndex];
      const dx = p.x - c.x;
      const dy = p.y - c.y;
      const dist = Math.hypot(dx, dy);
      if (dist <= remain) {
        c.x = p.x;
        c.y = p.y;
        remain -= dist;
        c.pathIndex++;
      } else {
        c.x += (dx / dist) * remain;
        c.y += (dy / dist) * remain;
        c.phase += remain * 3.2;
        c.moving = true;
        remain = 0;
      }
    }
    if (c.pathIndex < c.path.length) c.moving = true;
    return c.pathIndex >= c.path.length;
  }

  updateTruck(t, dt) {
    let remain = dt * 2.4;
    while (remain > 0 && t.index < t.path.length) {
      const p = t.path[t.index];
      const dx = p.x - t.x;
      const dy = p.y - t.y;
      const dist = Math.hypot(dx, dy);
      if (dist <= remain) {
        t.x = p.x;
        t.y = p.y;
        remain -= dist;
        t.index++;
      } else {
        t.x += (dx / dist) * remain;
        t.y += (dy / dist) * remain;
        remain = 0;
      }
    }
    return t.index >= t.path.length;
  }

  // ------------------------------------------------------------------- HUD
  stats() {
    const pop = this.citizens.length;
    const employed = this.citizens.filter((c) => c.work).length;
    const homeless = this.citizens.filter((c) => !c.home).length;
    const avgMood = pop ? this.citizens.reduce((a, c) => a + c.mood, 0) / pop : 0;
    const hungry = this.citizens.filter((c) => c.hunger > 1.2).length;
    return {
      pop,
      employed,
      homeless,
      hungry,
      avgMood,
      jobs: this.firms.reduce((a, f) => a + f.jobs, 0),
      houses: this.countDone('house'),
      shops: this.countDone('shop'),
      factories: this.countDone('factory'),
      sites: this.sites.length,
      works: this.works,
      budget: this.budget,
      money: this.internalMoney(),
      drift: this.audit(),
    };
  }
}
