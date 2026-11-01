// City simulation.
//
// Two rules hold the whole model together:
//   1. A walker is only ever on a road tile, so nobody can cut through a wall.
//   2. Money is never printed inside the city: wages come out of a business
//      till, construction is paid by the city budget and lands in the builders'
//      pockets. The only outside money is the savings a newcomer brings and the
//      savings a leaver takes away - both are reported separately in the ledger.

import { World, N, T } from './world.js?v=2026-11-01-pixel-7';
import { Systems, SERVICES, POLICIES, MILESTONES, seasonOf } from './mechanics.js?v=2026-11-01-pixel-7';

export const MIN_PER_SEC = 10; // game minutes per real second at 1x speed
export const HOUR = 60;

export const COSTS = {
  road: 12,
  bridge: 60,
  park: 60,
  house: 100,
  shop: 150,
  factory: 240,
  warehouse: 260,
  apartment: 160,
};

// working capital the town hands to a new business so it can stock its shelves
export const CAPITAL = { shop: 100, factory: 140, warehouse: 120 };

// what a public service building costs the town every day (see mechanics.js)
export const SERVICE_KINDS = Object.keys(SERVICES);

// salaries and habits of a citizen, in game terms
const SCHOOL_AGE = 18;
const RETIRE_AGE = 65;
const PENSION = 22; // ₽ per retired citizen per day, paid by the town

const HOURS = 8; // length of a working shift

const NAMES = [
  'Аня', 'Борис', 'Вера', 'Глеб', 'Даша', 'Егор', 'Жанна', 'Захар', 'Инна', 'Кирилл',
  'Лена', 'Марк', 'Нина', 'Олег', 'Полина', 'Роман', 'Соня', 'Тимур', 'Ульяна', 'Фёдор',
  'Эля', 'Юра', 'Яна', 'Артём', 'Влада', 'Денис', 'Ева', 'Жора', 'Зоя', 'Илья',
  'Ксюша', 'Лёва', 'Мила', 'Назар', 'Оля', 'Паша', 'Рита', 'Сева', 'Тоня', 'Филипп',
];

const SHIRTS = ['#e0574d', '#3f7fb5', '#e2a33c', '#5aa469', '#8c6bb1', '#d9739b', '#4aa39c', '#c9713f'];
const SKINS = ['#e8bb92', '#d9a276', '#c78a5f', '#f0cba6'];
// price the outside market pays for a unit of factory output
export const EXPORT_PRICE = 4.4;

const CAR_COLORS = ['#d9564c', '#e6dcc6', '#5b8fd0', '#e0a83c', '#6fae72', '#9d8ec7', '#d9803f'];

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);

// public services pay a little better than shops: the town competes for staff
function svcWageFor(kind) {
  if (SERVICES[kind]) return 38;
  if (kind === 'cityhall') return 42;
  if (kind === 'shop') return 52;
  return 56;
}
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

    // an empty plot of land: the town has its budget and nothing else yet
    this.budget = 3200;
    this.citizens = [];
    this.firms = [];
    this.sites = [];
    this.trucks = [];
    this.cars = [];
    this.nameIndex = 0;
    this.colors = 0;

    this.ledger = this.emptyLedger();   // today
    this.total = this.emptyLedger();    // since the town was founded
    // what the whole city produced, bought and shipped, in units of goods
    this.goods = { produced: 0, sold: 0, exported: 0, imported: 0, restocked: 0 };
    // things worth telling the player about (the UI turns these into toasts)
    this.events = []; // capped, the UI drains it every frame
    // money that crossed the border of the city: savings of newcomers, money
    // taken away by leavers, goods sold to and bought from the outside world
    this.external = { in: 0, out: 0 };
    this.exportIncome = 0;
    this.works = 0; // road & park money waiting to be paid out as wages

    // ---- systems around the buildings
    this.systems = new Systems(this);
    this.tax = 0.15; // income tax the town takes from every wage
    this.loan = 0; // what the town owes the bank
    this.loanRate = 0.004; // daily interest
    this.policies = { nightShift: false, recycling: false, freeTransit: false };
    this.season = seasonOf(1);
    this.waste = 0; // garbage that no landfill could take
    this.history = []; // one row per day for the little chart
    this.milestones = [];
    this.metrics = {
      fires: 0,
      crimes: 0,
      births: 0,
      deaths: 0,
      retired: 0,
      sick: 0,
      graduated: 0,
      tourism: 0,
      propertyTax: 0,
      upkeep: 0,
    };
    this.systemsDirty = true;

    this.seedTown();
    this.world.recomputeComponents();
    this.systems.recompute();
    this.initialBudget = this.budget;
    this.budgetDayStart = this.budget;
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
      imports: 0,
      exports: 0,
      contractor: 0,
      propertyTax: 0,
      maintenance: 0,
      interest: 0,
      tourism: 0,
      waste: 0,
      crime: 0,
      credit: 0,
      grants: 0,
      migrationIn: 0,
      migrationOut: 0,
    };
  }

  // ------------------------------------------------------------- the start
  seedTown() {
    // Nothing is pre-built any more. Bare land, the city budget and one rule:
    // roads, goods and people only come in through the edge of the map.
    this.world.recomputeComponents();
  }

  // ------------------------------------------------------- the way in and out
  /** Some road reaches the border of the map: the only entrance to the town. */
  townConnected() {
    return this.world.edgeComps.size > 0;
  }

  /** Is this road on the entrance network (so people can reach it from outside)? */
  connectedToEdge(x, y) {
    return this.world.hasGate(x, y);
  }

  /** A border road tile on the same network as `home` - where newcomers step in. */
  entryFor(home) {
    const comp = home ? this.world.componentAt(home.door.x, home.door.y) : 0;
    const roads = this.world.edgeRoads(comp || undefined);
    if (!roads.length) return null;
    return roads[Math.floor(this.rng() * roads.length)];
  }

  /** Short line for the HUD that tells the player what is missing right now. */
  statusHint() {
    const ready = this.world.buildings.filter((b) => b.state === 'done');
    if (ready.length && this.citizens.length === 0 && !ready.some((b) => b.kind === 'water')) {
      return 'построй водонапорную башню — без воды жители не приедут';
    }
    if (this.tax > 0.25) return 'налог высокий: жители недовольны, доходы падают';
    if (this.loan > 800) return 'долг растёт: проценты съедают бюджет';
    if (this.waste > 6) return 'мусор некуда девать — поставь свалку';
    if (!this.townConnected()) {
      return this.world.countRoads() === 0
        ? 'проведи дорогу от края карты — по ней приедут люди'
        : 'дорога не доходит до края карты: ни стройки, ни жителей';
    }
    if (this.countDone('house') === 0 && this.sites.length === 0) {
      return 'размечай жилую зону у дороги — город построит первый дом';
    }
    if (this.citizens.length === 0) return 'первый дом готов: жители приедут с края карты';
    return null;
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
      wage: svcWageFor(building.kind),
      baseWage: svcWageFor(building.kind),
      price: building.kind === 'shop' ? 11 : building.kind === 'factory' || building.kind === 'warehouse' ? 6.5 : 0,
      basePrice: building.kind === 'shop' ? 11 : building.kind === 'factory' || building.kind === 'warehouse' ? 6.5 : 0,
      revenueToday: 0,
      wageBillToday: 0,
      soldToday: 0,
      boughtToday: 0,
      producedToday: 0,
      salesEwma: building.kind === 'shop' ? 8 : 12,
      arrears: 0,
    };
    const svc = SERVICES[building.kind];
    firm.jobs = opts.jobs ?? (svc ? svc.jobs : building.kind === 'shop' ? 1 : building.kind === 'warehouse' ? 2 : 3);
    // a new shop fills its shelves from outside: the town imports goods and
    // pays for them, so this is money going out, not money appearing
    if (building.kind === 'shop' && !opts.public) {
      const importPrice = 7.5;
      const qty = Math.min(14, Math.floor(firm.cash / importPrice));
      if (qty > 0) {
        const cost = qty * importPrice;
        firm.cash -= cost;
        firm.stock = qty;
        this.external.out += cost;
        this.ledger.imports += cost;
        this.goods.imported += qty;
      }
    }
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
      age: opts.age ?? (opts.child ? 4 : 20 + Math.floor(this.rng() * 38)),
      // grown-ups bring the schooling they got elsewhere; a school in town
      // teaches the children, and grown-ups who never finished can catch up
      edu: opts.edu ?? (opts.child ? 0 : this.rng() < 0.35 ? 0.5 + this.rng() * 0.5 : 0.1 + this.rng() * 0.25),
      health: 1,
      sick: false,
      sickDays: 0,
      retired: false,
      school: false,
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
      // nobody can arrive without a road that leaves the map
      const edge = this.entryFor(home);
      if (!edge) {
        if (home && home.residents) home.residents = home.residents.filter((r) => r !== c);
        return null;
      }
      // newcomers bring their own savings: it is outside money, reported as
      // such. A baby born in town brings nothing.
      if (opts.savings !== false) {
        c.cash = 60;
        this.external.in += c.cash;
        this.ledger.migrationIn += c.cash;
      } else {
        c.cash = 0;
      }
      c.x = edge.x + 0.5;
      c.y = edge.y + 0.5;
      c.state = 'newcomer';
      this.walkTo(c, home.door);
      this.note({ type: 'arrive', name: c.name, cash: c.cash });
    }
    this.citizens.push(c);
    return c;
  }

  edgeRoadTile(comp) {
    const roads = this.world.edgeRoads(comp);
    if (roads.length) return roads[Math.floor(this.rng() * roads.length)];
    return null;
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
    if (this.minute % 5 === 0) this.carHour();
    if (this.minute === 8 * HOUR) this.schoolRun();
    if (this.minute === 13 * HOUR) this.schoolOut();

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
      case 12 * HOUR + 30:
        this.lunch();
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
    // the world changes under the simulation: coverage, pollution, noise
    this.systems.recompute();
    this.systemsDirty = false;
    this.fireHour();
    this.checkMilestoneGrants();
    for (let i = this.firms.length - 1; i >= 0; i--) this.firmHour(this.firms[i], minute);
    this.assignBuilders();
    this.buildersHour();
    this.contractorHour();
    this.cityCrewHour();
    this.develop(minute);
    this.maybeMigrate(minute);
    this.checkDepartures();
  }

  /** Reaching a population milestone unlocks services and pays a grant. */
  checkMilestoneGrants() {
    const reached = this.systems.checkMilestones(this.citizens.length);
    for (const m of reached) {
      this.budget += m.grant;
      this.external.in += m.grant;
      this.ledger.grants += m.grant;
      this.milestones.push(m);
      this.note({ type: 'toast', text: `${m.label}: +${m.grant} ₽ и новые службы` });
    }
  }

  firmHour(firm, minute) {
    const nightOk = this.policies.nightShift && (firm.kind === 'factory' || firm.kind === 'warehouse');
    const atWork = (minute >= 8 * HOUR && minute < 16 * HOUR) || (nightOk && (minute >= 20 * HOUR || minute < 6 * HOUR));
    const present = firm.workers.filter(
      (c) => (c.state === 'work' || c.state === 'toWork') && !c.sick && !c.retired && c.age >= SCHOOL_AGE,
    );
    if (atWork && present.length) {
      const due = (firm.wage / HOURS) * present.length;
      const pool = firm.public ? Math.min(this.budget, due) : Math.min(firm.cash, due);
      if (pool > 0.01) {
        if (firm.public) this.budget -= pool;
        else firm.cash -= pool;
        const each = pool / present.length;
        for (const c of present) {
          const tax = each * this.tax;
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
      // a factory without electricity just stands there
      const powered = this.systems.served('power', firm.building.door.x, firm.building.door.y);
      const workers = firm.workers.filter((c) => c.state === 'work').length;
      const shiftBonus = this.policies.nightShift ? 1.15 : 1;
      const skill = 1 + 0.25 * this.avgEducation(firm);
      if (workers) firm.powerless = !powered;
      if (workers && powered) {
        const made = workers * 1.05 * shiftBonus * skill;
        firm.stock += made;
        firm.producedToday += made;
        this.goods.produced += made;
      }
      // goods the shops did not take are sold out of town. That is the only
      // real income the city has from the outside world, and it is reported
      // as outside money in the ledger.
      const surplus = firm.stock - 12;
      if (surplus > 0.5) {
        const qty = Math.min(surplus, 7);
        const total = qty * EXPORT_PRICE;
        firm.stock -= qty;
        firm.cash += total;
        firm.revenueToday += total;
        firm.soldToday += qty;
        this.exportIncome += total;
        this.ledger.exports += total;
        this.goods.exported += qty;
      }
    }

    // a warehouse buys from factories, so shops can restock from it
    if (firm.kind === 'warehouse' && atWork) {
      const target = 48;
      if (firm.stock < target) {
        const supplier = this.bestSupplier(firm.building.door);
        if (!supplier && firm.stock < 6) {
          // no warehouse and no factory: buy goods outside town, dearer
          const importPrice = 9;
          const qty = Math.floor(Math.min(8, firm.cash / importPrice));
          if (qty > 0) {
            const total = qty * importPrice;
            firm.cash -= total;
            firm.stock += qty;
            firm.boughtToday += qty;
            this.external.out += total;
            this.ledger.imports += total;
            this.goods.imported += qty;
          }
        }
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
            this.ledger.restock += total;
            this.goods.restocked += qty;
            this.spawnTruck(supplier.building, firm.building);
          }
        }
      }
    }

    if (firm.kind === 'shop') {
      const target = Math.min(26, Math.max(6, firm.salesEwma * 2.4));
      if (firm.stock < target) {
        const supplier = this.bestSupplier(firm.building.door);
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
            this.goods.restocked += qty;
            this.spawnTruck(supplier.building, firm.building);
          }
        }
      }
    }
  }

  /**
   * Cheapest place with goods a shop (or a warehouse) can reach: a warehouse
   * first, then a factory. `fromDoor` limits the choice to one road network.
   */
  bestSupplier(fromDoor) {
    let best = null;
    let bestScore = Infinity;
    for (const f of this.firms) {
      if (f.kind !== 'factory' && f.kind !== 'warehouse') continue;
      if (f.stock < 1) continue;
      if (fromDoor && !this.world.sameNetwork(f.building.door, fromDoor)) continue;
      const dist = fromDoor ? Math.hypot(f.building.door.x - fromDoor.x, f.building.door.y - fromDoor.y) : 0;
      const score = f.price * 10 + dist * 0.4 - (f.kind === 'warehouse' ? 14 : 0);
      if (score < bestScore) {
        bestScore = score;
        best = f;
      }
    }
    return best;
  }

  factoryWithStock() {
    return this.bestSupplier(null);
  }

  /** Average education of the workers of one firm (0..1). */
  avgEducation(firm) {
    if (!firm.workers.length) return 0;
    let sum = 0;
    for (const c of firm.workers) sum += c.edu || 0;
    return sum / firm.workers.length;
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
      // a builder may have left town or died: never pay a ghost
      site.builders = site.builders.filter((c) => this.citizens.includes(c));
      if (!site.builders.length) {
        site.idle = (site.idle || 0) + 1;
        continue;
      }
      site.idle = 0;
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
          this.ledger.wages += each;
        }
      }
      if (site.progressNeeded && site.work >= site.progressNeeded) this.finishSite(site);
    }
  }

  /**
   * If a site has no townsfolk to work on it, the town hires a crew from
   * outside. Their pay leaves the city (reported as outside money), so the
   * books stay honest - it is not printed money, it is a real expense.
   */
  contractorHour() {
    if (this.minute < 6 * HOUR || this.minute > 21 * HOUR) return;
    for (const site of this.sites.slice()) {
      if (site.state !== 'site' || site.builders.length) continue;
      // give the townsfolk a day to take the job before paying an outside crew
      if (this.citizens.length > 0 && (site.idle || 0) < 8) continue;
      const due = Math.min(site.escrow, 12);
      if (due > 0.01) {
        site.escrow -= due;
        this.external.out += due;
        this.ledger.contractor += due;
      }
      site.work += 1.15;
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
    if (b.kind === 'house') {
      b.residents = b.residents || [];
      b.level = 1;
      b.capacity = 2;
    }
    b.waste = 0;
    b.site = null;
    this.note({ type: 'done', kind: b.kind });
    const idx = this.sites.indexOf(site);
    if (idx >= 0) this.sites.splice(idx, 1);
    this.world.version++;
  }

  // ------------------------------------------------------------ development
  /** Beds people can actually move into: a house needs water and repair. */
  freeHomeSlots() {
    let free = 0;
    for (const b of this.world.buildings) {
      if (b.kind !== 'house' || b.state !== 'done' || b.abandoned) continue;
      if (!this.systems.served('water', b.door.x, b.door.y)) continue;
      const cap = b.capacity || 2;
      free += Math.max(0, cap - (b.residents ? b.residents.length : 0));
    }
    return free;
  }

  /** Houses that can take somebody in right now. */
  openHomes() {
    return this.world.buildings.filter(
      (b) =>
        b.kind === 'house' &&
        b.state === 'done' &&
        !b.abandoned &&
        this.systems.served('water', b.door.x, b.door.y) &&
        (b.residents || []).length < (b.capacity || 2),
    );
  }

  vacancies() {
    let v = 0;
    for (const f of this.firms) v += Math.max(0, f.jobs - f.workers.length);
    return v;
  }

  countDone(kind) {
    return this.world.buildings.filter((b) => b.kind === kind && b.state === 'done').length;
  }

  /** Built plus already under construction: stops the town from starting the
   *  same project three times while the first one is still a hole in the ground. */
  countPlanned(kind) {
    const sites = this.sites.filter((s) => s.building.kind === kind).length;
    return this.countDone(kind) + sites;
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
  /**
   * Everything the town would like to build right now, most urgent first.
   * The list matters: if there is no free lot for a factory, the town still
   * builds a house for the next family instead of standing still.
   */
  wishlist() {
    if (!this.townConnected()) return [];
    const pop = this.citizens.length;
    const shops = this.countPlanned('shop');
    const factories = this.countPlanned('factory');
    const houses = this.countPlanned('house');
    const emptyShelves = this.firms.filter((f) => f.kind === 'shop' && f.stock < 3).length;
    const jobNeed = this.unemployedCount();
    const bedNeed = this.vacancies() - this.freeHomeSlots();
    const list = [];

    // an empty town bootstraps itself: one bed first, then work for its people
    if (houses === 0 && this.sites.length === 0) list.push('house');
    if (shops === 0 && houses > 0 && jobNeed >= 1) list.push('shop');
    if (factories === 0 && shops > 0) list.push('factory');

    if (emptyShelves > 0 && shops > 0 && factories * 12 < shops * 10) list.push('factory');
    if (bedNeed > 0 && pop < 70) list.push('house');
    if (jobNeed >= 2 && shops < Math.max(1, Math.floor(pop / 4) + 1)) list.push('shop');
    if (jobNeed >= 2) list.push('factory');
    if (jobNeed >= 1 && shops === 0) list.push('shop');
    return list;
  }

  chooseKind() {
    return this.wishlist()[0] || null;
  }

  /** Build one new lot of whatever the town needs, if the budget allows. */
  develop(minute) {
    if (minute < 6 * HOUR || minute > 21 * HOUR) return;
    if (this.sites.length >= 3) return;
    // materials come by road: without a road to the edge nothing is built
    if (!this.townConnected()) return;
    for (const kind of this.wishlist()) {
      const zone = kind === 'house' ? T.ZONE_R : kind === 'shop' ? T.ZONE_C : T.ZONE_I;
      const needed = COSTS[kind] + (CAPITAL[kind] || 0);
      if (this.budget < needed + 40) continue;
      // houses grow where there are jobs, businesses grow where people live
      const compFilter = kind === 'house' ? this.jobComponents() : this.residentComponents();
      // new homes are only built where the water tower reaches
      if (kind === 'house' && !this.world.buildings.some((b) => b.kind === 'water' && b.state === 'done')) continue;
      // factories prefer a 2x2 lot but happily take a single tile
      const sizes = kind === 'factory' ? [2, 1] : [1];
      for (const size of sizes) {
        const spot = this.findSpot(zone, size, compFilter, kind === 'house' ? 'water' : null);
        if (!spot) continue;
        this.startSite(kind, spot.x, spot.y, size);
        return;
      }
    }
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

  findSpot(zone, size, compFilter, needService) {
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
      const door = w.doorFor(c.x, c.y, size, size);
      // materials arrive by road: a lot behind a cut-off street stays empty
      if (!door || !w.hasGate(door.x, door.y)) continue;
      if (needService && !this.systems.served(needService, door.x, door.y)) continue;
      if (compFilter && compFilter.size) {
        if (!compFilter.has(w.componentAt(door.x, door.y))) continue;
      }
      // do not grow on a tile another site is waiting for
      const taken = this.sites.some((s) => c.x >= s.x && c.y >= s.y && c.x < s.x + s.w && c.y < s.y + s.d);
      if (taken) continue;
      return c;
    }
    return null;
  }

  note(ev) {
    if (this.events.length > 40) this.events.shift();
    if (ev.building) delete ev.building;
    this.events.push(ev);
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
    this.note({ type: 'build', kind, price: cost + capital });
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
    // no road to the border of the map - simply nobody can drive into town
    if (!this.townConnected()) return;
    // people move in when there is a bed free and the town still has work
    if (this.freeHomeSlots() <= 0) return;
    // a town with no water tower cannot take anybody in
    if (!this.world.buildings.some((b) => b.kind === 'water' && b.state === 'done')) return;
    if (this.unemployedCount() >= 4) return;
    if (this.citizens.length >= 60) return;
    if (this.rng() > 0.5) return;
    const homes = this.openHomes();
    // the house itself has to stand on the entrance network
    const home = homes.find((b) => this.connectedToEdge(b.door.x, b.door.y));
    if (!home) return;
    if (!this.spawnCitizen(home, {})) return;
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
    this.note({ type: 'leave', name: c.name, cash: c.cash });
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
    const comp = c.home ? this.world.componentAt(c.home.door.x, c.home.door.y) : 0;
    const edge = this.edgeRoadTile(comp || undefined);
    if (!edge) {
      // no road reaches the border: they cannot walk out, so they simply leave
      this.external.out += c.cash;
      this.ledger.migrationOut += c.cash;
      this.removeCitizen(c);
      return;
    }
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
      if (this.isDependent(c) || c.sick) continue;
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

  /** Children and pensioners eat and sleep, but do not go to work. */
  isDependent(c) {
    return c.retired || c.age < SCHOOL_AGE;
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
      if (c.sick) {
        this.setState(c, 'home');
        c.visible = false;
        continue;
      }
      c.visible = true;
      if (this.isDependent(c)) {
        this.setState(c, c.age < SCHOOL_AGE ? 'idle' : 'walk');
        if (c.age < SCHOOL_AGE && this.systems.served('education', c.x, c.y)) {
          this.setState(c, 'toSchool');
          if (c.home) this.walkTo(c, c.home.door);
          else this.wander(c);
        } else this.wander(c);
        continue;
      }
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

  /** Children walk to school, pensioners walk in the park. */
  schoolRun() {
    for (const c of this.citizens) {
      if (c.state === 'leaving' || c.sick) continue;
      if (c.age < SCHOOL_AGE) {
        c.school = this.systems.served('education', c.x, c.y);
        if (c.school) {
          this.setState(c, 'toSchool');
          c.visible = true;
          if (c.home) this.walkTo(c, c.home.door);
          else this.wander(c);
        } else {
          this.setState(c, 'idle');
          this.wander(c);
        }
      } else if (c.retired) {
        this.setState(c, 'walk');
        c.visible = true;
        this.wander(c);
      }
    }
  }

  schoolOut() {
    for (const c of this.citizens) {
      if (c.state === 'toSchool' || c.state === 'school' || c.state === 'walk') {
        if (c.age < SCHOOL_AGE && c.school) c.edu = clamp(c.edu + 0.03, 0, 1);
        this.goHomeState(c);
      }
    }
  }

  /** Half of the workers step out at midday, so the streets stay alive. */
  lunch() {
    for (const c of this.citizens) {
      if (c.state !== 'work' || !c.work) continue;
      if (this.rng() < 0.4) continue;
      const shop = this.nearestShop(c);
      if (!shop) continue;
      c.shopTarget = shop;
      c.lunch = true;
      c.visible = true;
      this.setState(c, 'toShop');
      c.x = c.work.building.door.x + 0.5;
      c.y = c.work.building.door.y + 0.5;
      this.walkTo(c, shop.building.door);
    }
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
    // a shop without power cannot run its tills: half of the customers leave
    if (!this.systems.served('power', shop.building.door.x, shop.building.door.y) && this.rng() < 0.5) {
      c.mood = clamp(c.mood - 0.02, 0, 1);
      return false;
    }
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
        this.goods.sold += 1;
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
    // the books are read once a day: add up today, then start a fresh page
    for (const key of Object.keys(this.ledger)) this.total[key] += this.ledger[key];
    this.ledger = this.emptyLedger();
    this.budgetDayStart = this.budget;
    this.season = this.currentSeason();
    const season = this.season;

    // ---- rent, business rates and property tax keep the budget alive
    for (const c of this.citizens) {
      const rent = 16 + (c.age < SCHOOL_AGE ? 4 : c.retired ? 2 : 0);
      const pay = Math.min(rent, Math.max(0, c.cash - 4));
      if (pay > 0.01) {
        c.cash -= pay;
        this.budget += pay;
        this.ledger.rent += pay;
      } else {
        c.unpaid += 1;
      }
    }
    this.collectPropertyTax();
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

    // ---- services, policies and heating are paid to the outside world
    this.payUpkeep(season);

    // ---- the bank wants its interest, and the town repays what it can
    if (this.loan > 0.01) {
      const interest = this.loan * this.loanRate;
      this.budget -= interest;
      this.external.out += interest;
      this.ledger.interest += interest;
      if (this.budget > 900) {
        const pay = Math.min(this.loan, (this.budget - 900) * 0.3);
        if (pay > 0.01) {
          this.loan -= pay;
          this.budget -= pay;
          this.external.out += pay;
          this.ledger.interest += pay;
        }
      }
    }

    // ---- tourists bring money from outside, but only in a nice town
    this.collectTourism(season);

    // ---- pensions: the town looks after those who worked all their lives
    this.payPensions();

    // ---- waste, health, education, births, ageing and crime
    this.handleWaste();
    this.updateHealth();
    this.updateEducation();
    this.updateAging();
    this.checkCrime();

    // ---- everyday needs and the effect of the whole neighbourhood
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
    this.recalcMood(season);
    this.upgradeHouses();
    this.checkAbandonment();
    this.assignJobs();
    this.assignBuilders();
    this.recordHistory();
    this.systems.recompute();
  }

  // ------------------------------------------------------------ the budget
  /** Land value tax: what the town collects from every standing building. */
  collectPropertyTax() {
    let sum = 0;
    for (const b of this.world.buildings) {
      if (b.state !== 'done' || b.kind === 'park') continue;
      const value = this.systems.landValueAt(b.door.x, b.door.y);
      const rate = b.kind === 'house' ? 0.35 : 0.5;
      const tax = (value / 40) * rate * 14;
      let payer = null;
      if (b.kind === 'house') payer = (b.residents || [])[0] || null;
      else payer = b.firm || null;
      if (!tax || tax < 0.01) continue;
      if (payer && payer.cash >= tax) {
        payer.cash -= tax;
        this.budget += tax;
        sum += tax;
      }
    }
    this.ledger.propertyTax += sum;
    this.metrics.propertyTax = sum;
  }

  /** Upkeep of public buildings, policies and winter heating leave the city. */
  payUpkeep(season) {
    let upkeep = 0;
    for (const b of this.world.buildings) {
      if (b.state !== 'done') continue;
      const svc = SERVICES[b.kind];
      if (svc) upkeep += svc.upkeep;
    }
    for (const [id, on] of Object.entries(this.policies)) {
      if (on) upkeep += POLICIES[id].perDay;
    }
    // every standing building needs heat in the cold season
    const built = this.world.buildings.filter((b) => b.state === 'done').length;
    upkeep += built * season.heat * 0.35;
    if (upkeep <= 0.01) return;
    const paid = Math.min(upkeep, Math.max(0, this.budget));
    if (paid > 0.01) {
      this.budget -= paid;
      this.external.out += paid;
      this.ledger.maintenance += paid;
    }
    this.metrics.upkeep = paid;
  }

  collectTourism(season) {
    let landmarks = 0;
    let parks = 0;
    for (const b of this.world.buildings) {
      if (b.state !== 'done' || b.abandoned) continue;
      if (b.kind === 'landmark') landmarks++;
      if (b.kind === 'park') parks++;
    }
    if (!landmarks) return;
    const connected = this.world.buildings.some((b) => b.kind === 'landmark' && this.connectedToEdge(b.door.x, b.door.y));
    if (!connected) return;
    const mood = this.stats().avgMood;
    const income = landmarks * 26 * (1 + Math.min(1.2, parks * 0.12)) * season.tourism * (0.6 + mood * 0.7);
    this.budget += income;
    this.external.in += income;
    this.ledger.tourism += income;
    this.metrics.tourism += income;
  }

  payPensions() {
    for (const c of this.citizens) {
      if (!c.retired) continue;
      const pay = Math.min(PENSION, Math.max(0, this.budget));
      if (pay <= 0) break;
      this.budget -= pay;
      c.cash += pay;
      this.ledger.cityWages += pay;
    }
  }

  // ----------------------------------------------------------- daily systems
  handleWaste() {
    const recycling = this.policies.recycling ? 0.5 : 1;
    let made = 0;
    for (const b of this.world.buildings) {
      if (b.state !== 'done' || b.kind === 'park') continue;
      const per = b.kind === 'house' ? 0.4 * (b.capacity || 2) * 0.5 : b.kind === 'factory' ? 2 : b.kind === 'shop' ? 0.9 : 0.5;
      made += per * recycling;
    }
    let capacity = 0;
    for (const b of this.world.buildings) {
      if (b.kind === 'landfill' && b.state === 'done' && !b.abandoned) {
        capacity += SERVICES.landfill.capacity * 0.25; // a day of landfill capacity
      }
    }
    // a lorry takes a few units out of town every day; without a landfill the
    // rest simply piles up in the streets
    const built = this.world.buildings.filter((b) => b.state === 'done').length;
    const hauled = Math.min(8 + built * 0.8, made);
    const dumpCost = hauled * 1.1;
    if (this.budget >= dumpCost) {
      this.budget -= dumpCost;
      this.external.out += dumpCost;
      this.ledger.waste += dumpCost;
    }
    const taken = Math.min(made, capacity + hauled);
    const left = Math.max(0, made - taken);
    this.waste = clamp(this.waste + left - 1.2, 0, 60);
    this.metrics.waste = this.waste;
  }

  updateHealth() {
    const sim = this;
    let sick = 0;
    for (const c of this.citizens) {
      const b = c.home;
      const polluted = b ? this.systems.pollutionAt(b.door.x, b.door.y) : 0;
      const clinic = b ? this.systems.served('health', b.door.x, b.door.y) : false;
      if (c.sick) {
        c.sickDays += 1;
        const heal = (clinic ? 0.6 : 0.42) + (c.health > 0.7 ? 0.1 : 0);
        if (this.rng() < heal) {
          c.sick = false;
          c.sickDays = 0;
          c.health = clamp(c.health + 0.3, 0, 1);
        } else {
          c.health = clamp(c.health - 0.08, 0, 1);
        }
      } else {
        const risk =
          0.008 + polluted * 0.03 + Math.min(0.06, Math.max(0, this.waste - 10) * 0.004) - (clinic ? 0.02 : 0);
        if (this.rng() < Math.max(0, risk)) {
          c.sick = true;
          c.sickDays = 0;
        } else if (clinic) {
          c.health = clamp(c.health + 0.1, 0, 1);
        }
      }
      if (c.sick) sick++;
    }
    this.metrics.sick = sick;
  }

  updateEducation() {
    for (const c of this.citizens) {
      const covered = c.home ? this.systems.served('education', c.home.door.x, c.home.door.y) : false;
      if (c.age < SCHOOL_AGE) {
        if (c.school || covered) c.edu = clamp(c.edu + 0.05, 0, 1);
      } else if (c.age < 45 && covered && !c.retired && c.edu < 0.9) {
        // evening classes: a school nearby slowly lifts the whole street
        c.edu = clamp(c.edu + 0.01, 0, 1);
      }
    }
  }

  updateAging() {
    const children = [];
    for (const c of this.citizens) {
      const was = c.age;
      c.age += 0.5;
      if (was < SCHOOL_AGE && c.age >= SCHOOL_AGE) this.metrics.graduated += 1;
      if (c.age >= RETIRE_AGE && !c.retired) {
        c.retired = true;
        c.retiredOn = this.day;
        if (c.work) {
          c.work.workers = c.work.workers.filter((w) => w !== c);
          c.work = null;
        }
        this.metrics.retired += 1;
      }
      // old age: somewhere after 78 the odds catch up with everybody
      if (c.age > 78 && this.rng() < (c.age - 78) * 0.02) {
        this.buryCitizen(c);
      }
    }
    // births: a house with room, two grown-ups and a good mood gets a baby
    for (const b of this.world.buildings) {
      if (b.kind !== 'house' || b.state !== 'done' || b.abandoned) continue;
      const residents = b.residents || [];
      if (residents.length === 0 || residents.length >= (b.capacity || 2)) continue;
      const grown = residents.filter((r) => r.age >= SCHOOL_AGE && r.age < RETIRE_AGE && !r.sick);
      if (grown.length < 2) continue;
      if (residents.some((r) => r.age < SCHOOL_AGE)) continue;
      const mood = grown.reduce((a, r) => a + r.mood, 0) / grown.length;
      if (mood < 0.62) continue;
      if (this.rng() > 0.22) continue;
      const baby = this.spawnCitizen(b, { child: true, savings: false });
      if (baby) {
        baby.mood = 0.8;
        children.push(baby);
        this.metrics.births += 1;
        this.note({ type: 'toast', text: `в семье ${grown[0].name} родился ребёнок` });
      }
    }
    this.assignJobs();
    return children;
  }

  buryCitizen(c) {
    for (const site of this.sites) site.builders = site.builders.filter((b) => b !== c);
    for (const site of this.sites) if (!site.builders.length) site.idle = 99;
    // savings of the deceased stay in the city: they go to the town budget
    this.budget += Math.max(0, c.cash);
    c.cash = 0;
    if (c.home) c.home.residents = (c.home.residents || []).filter((r) => r !== c);
    if (c.work) {
      c.work.workers = c.work.workers.filter((w) => w !== c);
      c.work = null;
    }
    this.removeCitizen(c);
    this.metrics.deaths += 1;
  }

  checkCrime() {
    const safe = this.world.buildings.some((b) => b.kind === 'police' && b.state === 'done' && !b.abandoned);
    if (safe) return;
    if (this.firms.length < 2) return;
    if (this.rng() > 0.12) return;
    const open = this.firms.filter((f) => !f.public && !f.building.abandoned && f.cash > 30);
    if (!open.length) return;
    const victim = open[Math.floor(this.rng() * open.length)];
    const stolen = Math.min(victim.cash * 0.25, 45);
    victim.cash -= stolen;
    this.external.out += stolen; // the thief left town with it
    this.ledger.crime += stolen;
    this.metrics.crimes += 1;
    this.note({ type: 'toast', text: `кража в ${victim.kind === 'factory' ? 'заводе' : victim.kind === 'shop' ? 'лавке' : 'конторе'} · −${Math.round(stolen)} ₽`, error: true });
  }

  /** Mood from the whole neighbourhood, not only from a full belly. */
  recalcMood(season) {
    for (const c of this.citizens) {
      const b = c.home;
      if (!b) {
        c.mood = clamp(c.mood - 0.05, 0, 1);
        continue;
      }
      const pollution = this.systems.pollutionAt(b.door.x, b.door.y);
      const noise = this.systems.noiseAt(b.door.x, b.door.y);
      const value = this.systems.landValueAt(b.door.x, b.door.y);
      let d = 0;
      d += value > 16 ? 0.02 : -0.01;
      d -= pollution * 0.012;
      d -= noise * 0.008;
      d -= Math.min(0.05, Math.max(0, this.waste - 10) * 0.004);
      d += season.mood;
      if (this.policies.freeTransit && this.systems.served('transit', b.door.x, b.door.y)) d += 0.02;
      if (this.policies.nightShift && this.systems.noiseAt(b.door.x, b.door.y) > 0.6) d -= 0.02;
      if (this.systems.served('transit', b.door.x, b.door.y)) d += 0.01;
      if (!this.systems.served('water', b.door.x, b.door.y)) d -= 0.04;
      if (!this.systems.served('power', b.door.x, b.door.y)) d -= 0.03;
      if (this.loan > 400) d -= 0.01;
      c.mood = clamp(c.mood + d, 0, 1);
    }
  }

  /** A house in a good place with water and power grows into an apartment. */
  upgradeHouses() {
    for (const b of this.world.buildings) {
      if (b.kind !== 'house' || b.state !== 'done' || b.abandoned) continue;
      if (this.rng() > 0.25) continue;
      this.upgradeHouse(b);
    }
  }

  checkAbandonment() {
    for (const b of this.world.buildings) {
      if (b.state !== 'done') continue;
      if (b.kind === 'house') {
        const residents = b.residents || [];
        if (residents.length === 0) {
          b.emptyDays = (b.emptyDays || 0) + 1;
          const value = this.systems.landValueAt(b.door.x, b.door.y);
          if (b.emptyDays > 6 && value < 14) {
            b.abandoned = true;
            this.note({ type: 'toast', text: 'заброшенный дом: земля здесь дешёвая и нет условий', error: true });
          }
        } else {
          b.emptyDays = 0;
          const unhappy = residents.filter((r) => r.mood < 0.25).length;
          if (unhappy === residents.length && residents.length > 0) {
            b.abandoned = true;
          } else if (b.abandoned && this.systems.served('water', b.door.x, b.door.y) && this.systems.served('power', b.door.x, b.door.y)) {
            b.abandoned = false;
          }
        }
      } else if (b.firm && !b.firm.public) {
        const broke = b.firm.cash < -20;
        b.brokeDays = broke ? (b.brokeDays || 0) + 1 : 0;
        if (b.brokeDays > 4) b.abandoned = true;
        else if (b.abandoned && b.firm.cash > 60) b.abandoned = false;
      }
    }
    // abandoned buildings lose their firm jobs
    for (const b of this.world.buildings) {
      if (b.state !== 'done') continue;
      if (b.abandoned && b.firm) {
        if (!b.firm.closed) {
          for (const w of b.firm.workers.slice()) w.work = null;
          b.firm.workers = [];
          b.firm.closed = true;
        }
      } else if (b.firm) {
        b.firm.closed = false;
      }
    }
  }

  recordHistory() {
    const s = this.stats();
    this.history.push({
      day: this.day,
      budget: Math.round(s.budget),
      pop: s.pop,
      mood: s.avgMood,
      pollution: this.systems.last.pollution,
      value: this.systems.last.value,
    });
    if (this.history.length > 60) this.history.shift();
  }

  // ------------------------------------------------------------- fire & order
  fireHour() {
    for (const b of this.world.buildings.slice()) {
      if (b.state !== 'done' || b.kind === 'park') continue;
      if (b.fire) {
        const covered = this.systems.served('fire', b.door.x, b.door.y);
        b.fire += 1;
        if (covered && b.fire >= 2) {
          b.fire = 0;
          this.note({ type: 'toast', text: 'пожар потушен' });
        } else if (b.fire >= 3) {
          b.fire = 0;
          this.metrics.fires += 1;
          this.note({ type: 'toast', text: 'пожар: здание сгорело', error: true });
          this.demolishBuilding(b);
        }
        continue;
      }
      if (this.rng() > 0.00004) continue;
      b.fire = 1;
      this.note({
        type: 'toast',
        text: this.systems.served('fire', b.door.x, b.door.y) ? 'пожар! едет расчёт' : 'пожар! пожарной части нет',
        error: true,
      });
    }
  }

  // ---------------------------------------------------------------- politics
  setTax(value) {
    this.tax = clamp(value, 0.05, 0.35);
    this.note({ type: 'toast', text: `ставка налога: ${Math.round(this.tax * 100)}%` });
    return this.tax;
  }

  takeLoan(amount = 500) {
    this.loan += amount;
    this.budget += amount;
    this.external.in += amount;
    this.ledger.credit += amount;
    this.note({ type: 'toast', text: `кредит: +${amount} ₽ под ${(this.loanRate * 100).toFixed(1)}% в день` });
    return { ok: true, loan: this.loan, budget: this.budget };
  }

  repayLoan(amount = 500) {
    const pay = Math.min(this.loan, amount, Math.max(0, this.budget));
    if (pay <= 0) return { ok: false, reason: 'нечего платить' };
    this.loan -= pay;
    this.budget -= pay;
    this.external.out += pay;
    this.ledger.credit += pay;
    return { ok: true, loan: this.loan };
  }

  togglePolicy(id) {
    if (!POLICIES[id]) return false;
    this.policies[id] = !this.policies[id];
    this.note({ type: 'toast', text: `${POLICIES[id].label}: ${this.policies[id] ? 'включено' : 'выключено'}` });
    return this.policies[id];
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

  /** How much the city budget moved today (exact, from the books). */
  budgetToday() {
    return this.budget - (this.budgetDayStart ?? this.budget);
  }

  /** Internal money may only change by the savings that crossed the border. */
  audit() {
    return this.internalMoney() - this.initialMoney - this.external.in - this.exportIncome + this.external.out;
  }

  // ---------------------------------------------------------------- trucks
  /**
   * Traffic. Most cars drive in from the edge of the map, stop at a building
   * and drive back out, so you can actually watch them arrive and leave.
   * A few just run errands between two addresses in town.
   */
  carHour() {
    if (this.cars.length >= 8) return;
    if (!this.townConnected()) return;
    if (this.minute < 6 * HOUR || this.minute > 23 * HOUR) return;
    const stops = this.world.buildings.filter(
      (b) => b.state === 'done' && b.door && this.connectedToEdge(b.door.x, b.door.y),
    );
    if (!stops.length) return;

    for (let attempt = 0; attempt < 10; attempt++) {
      const a = stops[Math.floor(this.rng() * stops.length)];
      const b = stops[Math.floor(this.rng() * stops.length)];
      if (!a || !b) continue;
      const arrival = this.rng() < 0.55;
    // a bus stop nearby means fewer people drive
    if (this.systems.served('transit', a.door.x, a.door.y) && this.rng() < 0.5) continue;
      let path = null;
      if (arrival) {
        // in from the border of the map, out again
        const edge = this.entryFor(a);
        if (!edge) continue;
        const inPath = this.world.pathRoads(edge, a.door);
        const outPath = this.world.pathRoads(a.door, edge);
        if (!inPath || !outPath || inPath.length < 3 || outPath.length < 2) continue;
        path = inPath.concat(outPath.slice(1));
      } else {
        if (a === b) continue;
        const there = this.world.pathRoads(a.door, b.door);
        const back = this.world.pathRoads(b.door, a.door);
        if (!there || !back || there.length < 4 || back.length < 2) continue;
        path = there.concat(back.slice(1));
      }
      this.cars.push({
        path,
        index: 1,
        x: path[0].x,
        y: path[0].y,
        color: CAR_COLORS[Math.floor(this.rng() * CAR_COLORS.length)],
        speed: 1.7 + this.rng() * 0.9,
        arrival,
      });
      return;
    }
  }

  updateCar(car, dt) {
    // traffic jams: every other car on the same tile slows this one down
    let neighbours = 0;
    for (const other of this.cars) {
      if (other === car) continue;
      if (Math.abs(other.x - car.x) + Math.abs(other.y - car.y) < 1.4) neighbours++;
    }
    const jam = 1 / (1 + neighbours * 0.45);
    car.slow = neighbours > 0;
    let remain = car.speed * jam * dt;
    while (remain > 0 && car.index < car.path.length) {
      const p = car.path[car.index];
      const dx = p.x - car.x;
      const dy = p.y - car.y;
      const dist = Math.hypot(dx, dy);
      if (dist <= remain) {
        car.x = p.x;
        car.y = p.y;
        remain -= dist;
        car.index++;
      } else {
        car.x += (dx / dist) * remain;
        car.y += (dy / dist) * remain;
        remain = 0;
      }
    }
    return car.index >= car.path.length;
  }

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
    if (t === T.ZONE_R || t === T.ZONE_C || t === T.ZONE_I) return { ok: false };
    // never wipe a building by accident: the bulldozer is there for that
    if (w.occ[y * w.n + x] >= 0) return { ok: false, reason: 'тут здание, сначала снос' };
    // water is crossed by a bridge: five times the price of a street
    const bridge = t === T.WATER;
    const cost = bridge ? COSTS.bridge : COSTS.road;
    if (this.budget < cost) return { ok: false, reason: bridge ? 'нет денег на мост' : 'нет денег на дорогу' };
    this.budget -= cost;
    this.works += cost;
    this.ledger.construction += cost;
    w.removePropsIn(x, y);
    w.set(x, y, T.ROAD);
    if (bridge) w.bridges.add(y * w.n + x);
    w.recomputeComponents();
    if (bridge) this.note({ type: 'toast', text: 'мост построен' });
    return { ok: true, cost };
  }

  // -------------------------------------------------------------- services
  /** Is this service kind available yet (population milestone)? */
  serviceUnlocked(kind) {
    const svc = SERVICES[kind];
    if (!svc) return false;
    return this.systems.isUnlocked(kind);
  }

  /** Place a public building: school, clinic, power plant, bus stop... */
  buildService(kind, x, y) {
    const svc = SERVICES[kind];
    const w = this.world;
    if (!svc) return { ok: false };
    if (!this.serviceUnlocked(kind)) return { ok: false, reason: `${svc.label}: нужно ${svc.unlock} жителей` };
    if (!w.inBounds(x, y)) return { ok: false };
    if (!this.townConnected()) return { ok: false, reason: 'нет дороги до края карты — материалы не привезти' };
    const size = svc.size || 1;
    const t = w.get(x, y);
    if (t === T.ROAD || t === T.WATER || t === T.PARK) return { ok: false };
    if (this.budget < svc.cost) return { ok: false, reason: 'нет денег на постройку' };
    if (!w.canBuild(kind, x, y, size, size)) return { ok: false, reason: 'место занято' };
    const b = w.addBuilding(kind, x, y, size, size, { variant: 0 });
    if (!b) return { ok: false, reason: 'место занято' };
    this.budget -= svc.cost;
    this.ledger.construction += svc.cost;
    this.external.out += svc.cost; // hired outside crew and materials
    this.metrics.upkeep += svc.upkeep;
    if (svc.jobs > 0) this.addFirm(b, { public: true, capital: 0 });
    this.systems.recompute();
    this.systemsDirty = true;
    this.note({ type: 'build', kind, price: svc.cost });
    return { ok: true, cost: svc.cost };
  }

  /** A warehouse is a business the player places on industrial land. */
  buildWarehouse(x, y) {
    const w = this.world;
    if (!w.inBounds(x, y)) return { ok: false };
    if (!this.serviceUnlocked('warehouse')) return { ok: false, reason: 'склад: нужно 22 жителя' };
    if (!this.townConnected()) return { ok: false, reason: 'нет дороги до края карты' };
    const cost = COSTS.warehouse + CAPITAL.warehouse;
    if (this.budget < cost) return { ok: false, reason: 'нет денег на склад' };
    if (!w.canBuild('warehouse', x, y, 2, 2, T.ZONE_I)) return { ok: false, reason: 'склад ставится на свободную промзону 2×2' };
    const b = w.addBuilding('warehouse', x, y, 2, 2, { variant: 0 });
    if (!b) return { ok: false };
    this.budget -= cost;
    this.ledger.construction += COSTS.warehouse;
    this.external.out += COSTS.warehouse + CAPITAL.warehouse;
    this.addFirm(b, { capital: CAPITAL.warehouse });
    this.systems.recompute();
    this.note({ type: 'build', kind: 'warehouse', price: cost });
    return { ok: true, cost };
  }

  /** A house with money and services around it grows into an apartment. */
  upgradeHouse(b) {
    if (!b || b.kind !== 'house' || b.state !== 'done') return false;
    if (b.level >= 3) return false;
    if (!this.systems.served('water', b.door.x, b.door.y)) return false;
    if (!this.systems.served('power', b.door.x, b.door.y)) return false;
    const value = this.systems.landValueAt(b.door.x, b.door.y);
    if (value < 18 + b.level * 8) return false;
    const cost = COSTS.apartment;
    if (this.budget < cost) return false;
    this.budget -= cost;
    this.ledger.construction += cost;
    this.external.out += cost;
    b.level = (b.level || 1) + 1;
    b.capacity = 2 * b.level;
    b.upgradedOn = this.day;
    this.note({ type: 'toast', text: `дом вырос до ${b.level} уровня: ${b.capacity} жителей` });
    this.world.version++;
    return true;
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
    if (t === T.ROAD && w.isBridge(x, y)) {
      w.bridges.delete(y * w.n + x);
      w.set(x, y, T.WATER);
    } else {
      w.set(x, y, T.GRASS);
    }
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
      // only money that still sits in escrow comes back
      const site = b.site;
      if (this.sites.includes(site)) {
        this.budget += site.escrow;
        this.ledger.construction += site.escrow;
        site.escrow = 0;
        this.sites = this.sites.filter((s) => s !== site);
      }
      for (const c of site.builders) c.site = null;
      b.site = null;
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
    for (let i = this.cars.length - 1; i >= 0; i--) {
      if (this.updateCar(this.cars[i], dt)) this.cars.splice(i, 1);
    }
  }

  updateCitizen(c, dt) {
    if (c.sick && c.state !== 'home' && c.state !== 'sleep') {
      c.visible = false;
      this.setState(c, 'home');
    }
    if (c.state === 'school') {
      c.moving = false;
      return;
    }
    if (c.state === 'home' || c.state === 'sleep' || c.state === 'work' || c.state === 'builder') {
      c.moving = false;
      if (c.state === 'home') {
        if (this.minute >= 8 * HOUR && this.minute < 16 * HOUR && c.work && !c.sick && !this.isDependent(c)) {
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
          if (c.lunch && c.work) {
            c.lunch = false;
            this.setState(c, 'toWork');
            this.walkTo(c, c.work.building.door);
            if (this.minute >= 16 * HOUR) this.goHomeState(c);
          } else {
            this.goHomeState(c);
          }
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
      case 'toSchool':
        if (arrived) {
          this.setState(c, 'school');
          c.visible = false;
        }
        break;
      case 'walk':
        if (arrived) {
          this.setState(c, 'home');
          c.visible = false;
        }
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
        c.phase += remain * 1.9;
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
  /** How many of each public service the town runs. */
  serviceCounts() {
    const out = {};
    for (const b of this.world.buildings) {
      if (b.state !== 'done' || b.abandoned) continue;
      if (!SERVICES[b.kind]) continue;
      out[b.kind] = (out[b.kind] || 0) + 1;
    }
    return out;
  }

  /** Season and how much heating the town needs right now. */
  currentSeason() {
    return seasonOf(this.day);
  }

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
      students: this.citizens.filter((c) => c.age < SCHOOL_AGE).length,
      retired: this.citizens.filter((c) => c.retired).length,
      sick: this.citizens.filter((c) => c.sick).length,
      educated: this.citizens.filter((c) => c.age >= SCHOOL_AGE && c.edu > 0.5).length,
      tax: this.tax,
      loan: this.loan,
      season: this.season.name,
      waste: this.waste,
      landValue: this.systems.last.value,
      pollution: this.systems.last.pollution,
      noise: this.systems.last.noise,
      provided: this.serviceCounts(),
      budget: this.budget,
      money: this.internalMoney(),
      drift: this.audit(),
    };
  }
}
