// Closed-loop city economy.
//
// The single rule everything here obeys: no money is ever created or
// destroyed. Every agent starts with a fixed endowment and afterwards money
// only moves from one balance to another:
//
//   businesses --wages--> households --purchases--> businesses
//   households --rent--> town budget --wages/public works/welfare--> households & businesses
//
// A business that cannot cover its payroll pays what it has and keeps the rest
// as arrears, settled as soon as cash comes in. Prices and wages adapt daily,
// so the town finds its own equilibrium instead of being tuned by hand.

import { createWorld } from './world.js';
import { clamp, mulberry32 } from './util.js';

export const HOUR = 60;
export const WAKE = 6 * HOUR + 30;
export const LEAVE_HOME = 7 * HOUR;
export const SHIFT_START = 8 * HOUR;
export const SHIFT_END = 16 * HOUR;
export const HOME_TIME = 18 * HOUR;
export const SLEEP = 22 * HOUR + 30;
export const SETTLE_DAY = 0.5 * HOUR;
export const IDLE_TIME = 9 * HOUR;

export const MINUTES_PER_SECOND = 20; // at 1x: one game hour every 3 seconds

const NAMES = [
  'Аня', 'Борис', 'Вера', 'Глеб', 'Даша', 'Егор', 'Жанна', 'Захар', 'Инна',
  'Кирилл', 'Лена', 'Марк', 'Нина', 'Олег', 'Полина', 'Роман', 'Соня', 'Тимур',
];

// Design volumes are the anchor for cost-plus pricing: they describe how much
// a business is built to sell per day, so prices stay proportionate to wages.
const FIRM_DEFS = [
  {
    id: 'farm', building: 'Ферма', slots: 4, wage: 58, price: 5.6,
    good: 'flour', goodName: 'мука', rate: 1.7, units: 52, startCash: 800,
  },
  {
    id: 'bakery', building: 'Пекарня', slots: 2, wage: 64, price: 9.3,
    good: 'bread', goodName: 'хлеб', rate: 2.5, units: 40,
    needs: { good: 'flour', per: 1 }, source: 'farm', inputTarget: 48, startCash: 700,
  },
  {
    id: 'shop', building: 'Магазин', slots: 2, wage: 62, price: 12.7,
    good: 'bread', goodName: 'хлеб', rate: 0, units: 40,
    source: 'bakery', stockTarget: 52, startCash: 700, startStock: 20,
  },
  {
    id: 'cafe', building: 'Кафе', slots: 2, wage: 58, price: 13.4,
    good: 'meal', goodName: 'обед', rate: 1.3, units: 12,
    needs: { good: 'flour', per: 1 }, source: 'farm', inputTarget: 16, startCash: 500,
  },
  {
    id: 'workshop', building: 'Мастерская', slots: 2, wage: 64, price: 43,
    good: 'furniture', goodName: 'мебель', rate: 0.18, units: 3, startCash: 600,
  },
  {
    id: 'city', building: 'Ратуша', slots: 2, wage: 52, price: 0,
    good: null, goodName: null, rate: 0, units: 0, public: true, startCash: 0,
  },
];

const HOUSEHOLD_CASH = 40;
const RENT = 17;
const MUNICIPAL_START = 300;
const WELFARE_DAILY = 26;
const HOURS_PER_DAY = SHIFT_END / HOUR - SHIFT_START / HOUR; // 8

class Citizen {
  constructor(sim, index, name, home, pos) {
    this.sim = sim;
    this.index = index;
    this.name = name;
    this.home = home;
    this.pos = { x: pos.x, y: pos.y };
    this.homeDoor = { x: pos.x, y: pos.y };
    this.path = null;
    this.pathIndex = 0;
    this.state = 'home';
    this.visible = true;
    this.work = null;
    this.cash = HOUSEHOLD_CASH;
    this.hunger = 0.4;
    this.morale = 0.8;
    this.walkPhase = (index * 1.7) % (Math.PI * 2);
    this.moving = false;
    this.dwell = 0;
    this.errands = [];
    this.incomeToday = 0;
    this.spentToday = 0;
    this.lastPurchase = null;
  }

  setState(state) {
    this.state = state;
  }

  walkTo(target) {
    const path = this.sim.world.findPath(this.pos, target);
    if (!path || path.length < 2) {
      this.path = null;
      this.pathIndex = 0;
      this.pos.x = target.x;
      this.pos.y = target.y;
      return false;
    }
    this.path = path;
    this.pathIndex = 1;
    return true;
  }

  stopWalking() {
    this.path = null;
    this.pathIndex = 0;
  }

  move(dt) {
    this.moving = false;
    if (!this.path) return true;
    const speed = 2.05;
    let remaining = speed * dt;
    while (remaining > 0 && this.pathIndex < this.path.length) {
      const p = this.path[this.pathIndex];
      const dx = p.x - this.pos.x;
      const dy = p.y - this.pos.y;
      const dist = Math.hypot(dx, dy);
      if (dist <= remaining) {
        this.pos.x = p.x;
        this.pos.y = p.y;
        remaining -= dist;
        this.pathIndex++;
      } else {
        this.pos.x += (dx / dist) * remaining;
        this.pos.y += (dy / dist) * remaining;
        this.walkPhase += remaining * 4.4;
        remaining = 0;
        this.moving = true;
      }
    }
    if (this.pathIndex < this.path.length) this.moving = true;
    return this.pathIndex >= this.path.length;
  }
}

class Firm {
  constructor(sim, def, building) {
    this.sim = sim;
    this.id = def.id;
    this.building = building;
    this.public = !!def.public;
    this.slots = def.slots;
    this.baseWage = def.wage;
    this.wage = def.wage; // daily wage of one worker
    this.basePrice = def.price;
    this.price = def.price;
    this.good = def.good;
    this.goodName = def.goodName;
    this.rate = def.rate;
    this.units = def.units; // design volume per day
    this.needs = def.needs || null;
    this.sourceId = def.source || null;
    this.stockTarget = def.stockTarget || 0;
    this.inputTarget = def.inputTarget || 0;
    this.stock = def.startStock || 0;
    this.inputStock = 0;
    this.cash = def.startCash || 0;
    this.employees = [];
    this.arrears = [];
    this.wageBillToday = 0;
    this.revenueToday = 0;
    this.materialsToday = 0;
    this.soldToday = 0;
    this.producedToday = 0;
    this.profitToday = 0;
    this.salesEwma = def.units || 4;
    this.priceHistory = [def.price];
    this.wageHistory = [def.wage];
    this.history = [];
    this.open = true;
  }

  get door() {
    const d = this.building.door;
    return { x: d.x + 0.5, y: d.y + 0.5 };
  }

  get till() {
    return this.public ? this.sim.municipality.cash : this.cash;
  }

  set till(v) {
    if (this.public) this.sim.municipality.cash = v;
    else this.cash = v;
  }

  get workersPresent() {
    let n = 0;
    for (const c of this.employees) if (c.state === 'work') n++;
    return n;
  }

  /** One hour of wages, split fairly when the till cannot cover everybody. */
  payWages(hours) {
    const sim = this.sim;
    const present = this.employees.filter((c) => c.state === 'work');
    if (!present.length) return;
    const dueEach = (this.wage / HOURS_PER_DAY) * hours;
    const need = dueEach * present.length;
    const available = Math.max(0, this.till);
    const pool = Math.min(need, available);
    if (pool > 0) this.till -= pool;
    const share = pool / present.length;
    for (const c of present) {
      if (share > 0.005) {
        c.cash += share;
        this.wageBillToday += share;
        c.incomeToday += share;
        if (this.public) sim.ledger.cityWages += share;
        else sim.ledger.wages += share;
        sim.record({ type: 'wage', firm: this, citizen: c, amount: share });
      }
      const missing = dueEach - share;
      if (missing > 0.005) {
        this.arrears.push({ citizen: c, amount: missing });
        sim.ledger.unpaid += missing;
      }
    }
  }

  settleArrears() {
    const sim = this.sim;
    for (let i = this.arrears.length - 1; i >= 0; i--) {
      const a = this.arrears[i];
      const pay = Math.min(a.amount, this.till);
      if (pay > 0.005) {
        this.till -= pay;
        a.citizen.cash += pay;
        this.wageBillToday += pay;
        a.citizen.incomeToday += pay;
        sim.ledger.wages += pay;
        sim.record({ type: 'wage', firm: this, citizen: a.citizen, amount: pay, late: true });
        a.amount -= pay;
      }
      if (a.amount <= 0.005) this.arrears.splice(i, 1);
    }
  }

  /** Buy raw material (or resale stock) from the supplier business. */
  restock() {
    if (!this.sourceId || this.public) return;
    const supplier = this.sim.firms[this.sourceId];
    if (!supplier) return;
    const toInput = this.rate > 0;
    const target = this.orderTarget(toInput);
    const current = toInput ? this.inputStock : this.stock;
    const need = target - current;
    if (need < 1) return;
    const unitPrice = supplier.price;
    if (unitPrice <= 0) return;
    // never spend the wage money: keep six hours of payroll aside
    const reserve = this.slots * (this.wage / HOURS_PER_DAY) * 6;
    const wallet = Math.max(0, Math.min(this.cash * 0.5, this.cash - reserve));
    const qty = Math.floor(Math.min(need, wallet / unitPrice, supplier.stock));
    if (qty < 1) return;
    const total = qty * unitPrice;
    this.cash -= total;
    supplier.cash += total;
    supplier.stock -= qty;
    supplier.revenueToday += total;
    supplier.soldToday += qty;
    this.materialsToday += total;
    if (toInput) this.inputStock += qty;
    else this.stock += qty;
    this.sim.ledger.tradeVolume += total;
    this.sim.tradeTrucks(supplier, this, qty);
  }

  /** How much to keep on the shelf: enough for tomorrow's expected demand. */
  orderTarget(toInput) {
    // Never stock less than the shop was built for: a bad day must not shrink
    // tomorrow's supply, otherwise the shortage feeds itself.
    const expected = Math.max(this.units * 0.9, this.salesEwma * 1.05 + 3);
    if (toInput) return Math.min(expected * (this.needs ? this.needs.per : 1), this.inputTarget || expected);
    const cap = this.units * 1.2 + 4;
    return Math.min(expected, this.stockTarget || cap, cap);
  }

  produce(hours) {
    if (this.public || this.rate <= 0) return;
    const workers = this.workersPresent;
    if (workers <= 0) return;
    let potential = this.rate * workers * hours;
    // do not bake more than the town is going to eat
    const shelf = Math.max(this.salesEwma * 1.25, this.units * 0.5) + 4;
    potential = Math.min(potential, Math.max(0, shelf - this.stock));
    if (this.needs) potential = Math.min(potential, this.inputStock / this.needs.per);
    if (potential <= 0.001) return;
    if (this.needs) this.inputStock = Math.max(0, this.inputStock - potential * this.needs.per);
    this.stock += potential;
    this.producedToday += potential;
  }

  /** Sell one unit to a household. */
  sellTo() {
    if (this.stock < 1) return 0;
    this.stock -= 1;
    this.revenueToday += this.price;
    this.soldToday += 1;
    return this.price;
  }

  /** Daily adaptation: cost-plus price, wage from the till. */
  adapt() {
    if (this.public) {
      const dailyBill = this.slots * this.wage;
      if (this.sim.municipality.cash < dailyBill * 0.4) this.wage = Math.max(this.baseWage * 0.7, this.wage * 0.98);
      else if (this.sim.municipality.cash > dailyBill * 2.2) this.wage = Math.min(this.baseWage * 1.25, this.wage * 1.02);
      this.wageHistory.push(this.wage);
      this.history.push({ day: this.sim.day, revenue: 0, cost: this.wageBillToday, profit: -this.wageBillToday, wage: this.wage, cash: 0, sold: 0, price: 0 });
      if (this.history.length > 90) this.history.shift();
      if (this.wageHistory.length > 140) this.wageHistory.shift();
      return;
    }

    this.salesEwma = this.salesEwma * 0.72 + this.soldToday * 0.28;

    // Cost-plus on the design volume, with the margin moving with the till:
    // a business that is short of cash earns more per unit, a comfortable one
    // eases prices down and sells more.
    const supplier = this.sourceId ? this.sim.firms[this.sourceId] : null;
    const inputPerUnit = this.needs ? this.needs.per : (this.sourceId ? 1 : 0);
    const designMaterials = supplier ? this.units * inputPerUnit * supplier.price : 0;
    const unitCost = (this.slots * this.wage + designMaterials) / Math.max(0.5, this.units);
    const bill = this.slots * this.wage;
    const cushion = (this.cash - bill * 2.5) / (bill * 8);
    let margin = 1.06 - clamp(cushion, -0.06, 0.04);
    const cover = this.stock / Math.max(1, this.soldToday);
    if (cover > 1.6) margin -= 0.02;
    if (cover < 0.3) margin += 0.03;
    margin = clamp(margin, 1.0, 1.45);
    this.price += (unitCost * margin - this.price) * 0.25;
    this.price = clamp(this.price, this.basePrice * 0.55, this.basePrice * 2.4);
    this.price = Math.round(this.price * 10) / 10;

    // wages follow the till, gently: the margin already does the heavy lifting
    let wageTarget = this.wage;
    if (this.cash > bill * 4) wageTarget = this.wage * 1.02;
    else if (this.cash < bill * 0.8) wageTarget = this.wage * 0.98;
    wageTarget = clamp(wageTarget, this.baseWage * 0.75, this.baseWage * 1.35);
    this.wage += (wageTarget - this.wage) * 0.5;
    this.wage = Math.round(this.wage * 10) / 10;

    this.profitToday = this.revenueToday - this.wageBillToday - this.materialsToday;
    this.history.push({
      day: this.sim.day, revenue: this.revenueToday, cost: this.wageBillToday + this.materialsToday,
      profit: this.profitToday, price: this.price, wage: this.wage,
      sold: this.soldToday, cash: this.cash,
    });
    if (this.history.length > 90) this.history.shift();
    this.priceHistory.push(this.price);
    this.wageHistory.push(this.wage);
    if (this.priceHistory.length > 140) this.priceHistory.shift();
    if (this.wageHistory.length > 140) this.wageHistory.shift();
  }

  resetDay() {
    this.revenueToday = 0;
    this.wageBillToday = 0;
    this.materialsToday = 0;
    this.soldToday = 0;
    this.producedToday = 0;
    this.profitToday = 0;
  }
}

export class Sim {
  constructor(seed = 7) {
    this.rng = mulberry32(seed);
    this.world = createWorld();
    this.day = 1;
    this.minute = 6 * HOUR + 40;
    this.absMinute = this.minute;
    this.timeScale = 1;
    this.paused = false;
    this.events = [];
    this.trucks = [];
    this.ledger = this.emptyLedger();
    this.message = '';
    this.pending = 0;
    this.setup();
  }

  emptyLedger() {
    return {
      wages: 0, cityWages: 0, unpaid: 0, purchases: 0, tradeVolume: 0,
      rent: 0, welfare: 0, bonus: 0, benefits: 0, publicWorks: 0,
    };
  }

  setup() {
    const w = this.world;
    const byName = (n) => w.buildings.find((b) => b.name === n);

    this.firms = {};
    for (const def of FIRM_DEFS) {
      const firm = new Firm(this, def, byName(def.building));
      if (def.id === 'bakery') firm.inputStock = 20;
      if (def.id === 'cafe') firm.inputStock = 8;
      this.firms[def.id] = firm;
    }
    this.firmList = Object.values(this.firms);
    this.businesses = this.firmList.filter((f) => !f.public);

    this.houses = w.buildings.filter((b) => b.type === 'house');
    this.townhall = byName('Ратуша');
    this.municipality = { cash: MUNICIPAL_START };

    this.citizens = [];
    let nameIndex = 0;
    for (const house of this.houses) {
      for (let k = 0; k < 2; k++) {
        const c = new Citizen(this, nameIndex, NAMES[nameIndex % NAMES.length], house, {
          x: house.door.x + 0.5 + (k === 0 ? -0.2 : 0.2),
          y: house.door.y + 0.5 + (k === 0 ? -0.14 : 0.14),
        });
        this.citizens.push(c);
        nameIndex++;
      }
    }

    // one job per citizen, several citizens per business
    const slots = [];
    for (const firm of this.firmList) {
      for (let i = 0; i < firm.slots; i++) slots.push(firm);
    }
    const order = this.citizens.map((c) => c.index);
    for (let i = order.length - 1; i > 0; i--) {
      const j = Math.floor(this.rng() * (i + 1));
      [order[i], order[j]] = [order[j], order[i]];
    }
    for (let i = 0; i < order.length && i < slots.length; i++) {
      const c = this.citizens[order[i]];
      c.work = slots[i];
      slots[i].employees.push(c);
    }
    this.unemployed = this.citizens.filter((c) => !c.work);
    this.initialTotal = this.totalMoney();
  }

  totalMoney() {
    let sum = this.municipality.cash;
    for (const c of this.citizens) sum += c.cash;
    for (const f of this.firmList) sum += f.cash;
    return sum;
  }

  /** Money in the town must never change: every ruble has an owner. */
  auditMoney() {
    return this.totalMoney() - this.initialTotal;
  }

  record(evt) {
    this.events.push(evt);
    if (this.events.length > 500) this.events.splice(0, this.events.length - 500);
  }

  drainEvents() {
    const e = this.events;
    this.events = [];
    return e;
  }

  tradeTrucks(supplier, buyer, qty) {
    if (qty <= 0) return;
    if (this.trucks.length > 4) return;
    if (this.rng() > 0.55) return;
    this.spawnTruck(supplier, buyer);
  }

  update(dtReal) {
    if (this.paused) return;
    const dt = Math.min(dtReal, 0.1) * this.timeScale;
    this.pending += dt * MINUTES_PER_SECOND;
    let guard = 0;
    while (this.pending >= 1 && guard++ < 400) {
      this.pending -= 1;
      this.tickMinute();
    }
    for (const c of this.citizens) this.updateCitizen(c, dt);
    for (let i = this.trucks.length - 1; i >= 0; i--) {
      if (this.updateTruck(this.trucks[i], dt)) this.trucks.splice(i, 1);
    }
  }

  tickMinute() {
    this.absMinute += 1;
    const minute = this.absMinute % 1440;
    this.minute = minute;

    if (minute === 0) this.day += 1;
    if (minute % 60 === 0) this.hourTick(minute);

    if (minute === WAKE) this.wakeUp();
    if (minute === LEAVE_HOME) this.startCommute();
    if (minute === SHIFT_END) this.endOfShift();
    if (minute === HOME_TIME) this.goHome();
    if (minute === 20 * HOUR) this.eveningBread();
    if (minute === SLEEP) this.goToSleep();
    if (minute === IDLE_TIME) this.idleTime();
    if (minute === 1 * HOUR) {
      this.collectRent();
      this.collectCorporateTax();
    }
    if (minute === 12 * HOUR) {
      this.publicWorks();
      this.supportBusinesses();
    }
    if (minute === SETTLE_DAY) this.settleDay();
  }

  hourTick(minute) {
    const atWork = minute >= SHIFT_START && minute < SHIFT_END;
    for (const firm of this.firmList) {
      firm.settleArrears();
      if (atWork) {
        firm.payWages(1);
        firm.produce(1);
      }
      firm.restock();
    }
  }

  /** People without a job still go out: the square, the park, the benches. */
  idleTime() {
    const spots = [
      { x: 9.5, y: 8.2 }, { x: 10.5, y: 8.4 }, { x: 9.2, y: 10.6 }, { x: 10.8, y: 10.4 },
      { x: 8.4, y: 3.2 }, { x: 11.6, y: 3.4 }, { x: 10.2, y: 18.6 }, { x: 8.6, y: 16.4 },
      { x: 12.4, y: 15.4 }, { x: 7.6, y: 8.4 },
    ];
    for (const c of this.citizens) {
      if (c.work || c.state === 'asleep') continue;
      const spot = spots[c.index % spots.length];
      c.setState('idle');
      c.walkTo({ x: spot.x + (this.rng() - 0.5) * 0.5, y: spot.y + (this.rng() - 0.5) * 0.5 });
    }
  }

  /** Whoever is still hungry at 20:00 nips out for bread. */
  eveningBread() {
    const shop = this.firms.shop;
    for (const c of this.citizens) {
      if (c.state === 'asleep' || c.hunger < 1.0) continue;
      if (shop.stock < 1) break;
      const price = shop.price;
      if (c.cash >= price) {
        shop.cash += shop.sellTo(c);
        c.cash -= price;
        c.spentToday += price;
        c.hunger = clamp(c.hunger - 0.7, 0, 2.5);
        this.ledger.purchases += price;
        this.record({ type: 'purchase', from: c, to: shop, amount: price, good: 'bread' });
      } else if (this.municipality.cash >= price) {
        shop.cash += shop.sellTo(c);
        this.municipality.cash -= price;
        this.ledger.welfare += price;
        c.hunger = clamp(c.hunger - 0.7, 0, 2.5);
        this.record({ type: 'welfare', citizen: c, to: shop, amount: price });
      }
    }
  }

  wakeUp() {
    for (const c of this.citizens) {
      c.visible = true;
      c.setState('home');
      c.errands = [];
    }
    this.message = 'Утро. Город просыпается.';
  }

  startCommute() {
    for (const c of this.citizens) {
      if (!c.work) continue;
      c.setState('commute');
      const target = { ...c.work.door };
      target.x += (c.index % 2 ? 0.24 : -0.24);
      target.y += (c.index % 3 ? -0.2 : 0.2);
      c.walkTo(target);
    }
  }

  endOfShift() {
    for (const c of this.citizens) {
      if (c.state === 'work') {
        this.planErrands(c);
        this.nextErrand(c);
      } else if (c.state === 'home' || c.state === 'idle') {
        // people without a job still go out for bread
        this.planErrands(c);
        this.nextErrand(c);
      }
    }
  }

  goHome() {
    for (const c of this.citizens) {
      if (c.state === 'commute' || c.state === 'work' || c.state === 'queue' || c.state === 'idle') {
        c.setState('return');
        c.errands = [];
        c.walkTo(c.homeDoor);
      }
    }
  }

  goToSleep() {
    for (const c of this.citizens) {
      c.stopWalking();
      c.pos.x = c.homeDoor.x;
      c.pos.y = c.homeDoor.y;
      c.setState('asleep');
      c.visible = false;
    }
    this.message = 'Ночь. Все спят дома.';
  }

  /** After the shift: bread first, then maybe a meal or a piece of furniture. */
  planErrands(c) {
    const list = [];
    const shop = this.firms.shop;
    const cafe = this.firms.cafe;
    const workshop = this.firms.workshop;
    if (shop.stock >= 1) list.push({ firm: shop, kind: 'bread', count: 2 });
    if (cafe.stock >= 1 && this.rng() < 0.88) list.push({ firm: cafe, kind: 'meal' });
    // townsfolk with money to spare eat out more and furnish their homes
    if (cafe.stock >= 2 && c.cash > 90 && this.rng() < 0.5) list.push({ firm: cafe, kind: 'meal' });
    if (workshop.stock >= 1 && c.cash > workshop.price * 1.25 && this.rng() < 0.3) {
      list.push({ firm: workshop, kind: 'furniture' });
    } else if (workshop.stock >= 2 && c.cash > 160 && this.rng() < 0.5) {
      list.push({ firm: workshop, kind: 'furniture' });
    }
    c.errands = list;
  }

  nextErrand(c) {
    const next = c.errands.shift();
    if (!next) {
      c.setState('return');
      c.walkTo(c.homeDoor);
      return;
    }
    c.setState('queue');
    const t = { x: next.firm.door.x, y: next.firm.door.y };
    t.x += ((c.index % 3) - 1) * 0.2;
    t.y += ((c.index % 2) - 0.5) * 0.18;
    c.target = next;
    c.walkTo(t);
  }

  updateCitizen(c, dt) {
    if (c.state === 'asleep') return;
    const arrived = c.move(dt);
    const minute = this.minute;

    switch (c.state) {
      case 'commute':
        if (arrived) {
          c.stopWalking();
          if (minute < SHIFT_END) c.setState('work');
          else {
            this.planErrands(c);
            this.nextErrand(c);
          }
        }
        break;
      case 'work':
        if (minute >= SHIFT_END) {
          this.planErrands(c);
          this.nextErrand(c);
        }
        break;
      case 'queue':
        if (arrived) {
          c.stopWalking();
          c.dwell -= dt;
          if (c.dwell <= 0) {
            c.dwell = 0.9 + this.rng() * 1.2;
            this.buy(c);
            this.nextErrand(c);
          }
        }
        break;
      case 'return':
        if (arrived) {
          c.stopWalking();
          c.setState('home');
        }
        break;
      default:
        break;
    }
  }

  /** A household buys something; if it cannot pay, the town pays for the food. */
  buy(c) {
    const target = c.target;
    if (!target) return;
    const firm = target.firm;
    if (firm.stock < 1) {
      c.morale = clamp(c.morale - 0.02, 0, 1);
      return;
    }
    const price = firm.price;
    const want = Math.min(target.count || 1, Math.floor(firm.stock));
    let bought = 0;
    for (let i = 0; i < want && c.cash >= firm.price; i++) {
      const paid = firm.sellTo(c);
      firm.cash += paid;
      c.cash -= paid;
      bought++;
      this.ledger.purchases += paid;
      this.record({ type: 'purchase', from: c, to: firm, amount: paid, good: firm.good, kind: target.kind });
    }
    if (bought > 0) {
      c.spentToday += bought * price;
      c.hunger = clamp(c.hunger - (target.kind === 'meal' ? 1.1 : 0.55 * bought), 0, 2.5);
      c.morale = clamp(c.morale + 0.03, 0, 1);
      return;
    }
    // Bread is subsidised out of the town budget when a household is broke.
    if (target.kind === 'bread' && this.municipality.cash >= price) {
      firm.cash += firm.sellTo(c);
      this.municipality.cash -= price;
      this.ledger.welfare += price;
      c.hunger = clamp(c.hunger - 0.6, 0, 2.5);
      c.morale = clamp(c.morale - 0.02, 0, 1);
      this.record({ type: 'welfare', citizen: c, to: firm, amount: price });
      return;
    }
    c.hunger = clamp(c.hunger + 0.3, 0, 2.5);
    c.morale = clamp(c.morale - 0.07, 0, 1);
    this.record({ type: 'missed', citizen: c });
  }

  /** Firms pay a small tax on idle cash; the town spends it back into the loop. */
  collectCorporateTax() {
    for (const firm of this.businesses) {
      const payroll = firm.slots * firm.wage;
      const inventory = (firm.stock + firm.inputStock) * firm.price * 0.6;
      const reserve = payroll * 3 + inventory;
      const excess = firm.cash - reserve;
      if (excess <= 0) continue;
      const tax = excess * 0.5;
      firm.cash -= tax;
      this.municipality.cash += tax;
      this.ledger.corporateTax = (this.ledger.corporateTax || 0) + tax;
      this.record({ type: 'tax', firm, amount: tax });
    }
  }

  collectRent() {
    for (const c of this.citizens) {
      // rent plus a small property tax on large savings: the town collects it
      // and spends it on wages, welfare and public works, so it circulates
      const wealthTax = Math.max(0, c.cash - 130) * 0.07;
      const due = RENT + (c.work ? 0 : 2) + wealthTax;
      const pay = Math.min(due, Math.max(0, c.cash - 3));
      if (pay > 0.005) {
        c.cash -= pay;
        this.municipality.cash += pay;
        this.ledger.rent += pay;
        if (wealthTax > 0.5) this.ledger.tax = (this.ledger.tax || 0) + Math.min(wealthTax, pay);
        c.spentToday += pay;
        this.record({ type: 'rent', from: c, amount: pay });
      }
    }
    for (const c of this.unemployed) {
      const pay = Math.min(WELFARE_DAILY, this.municipality.cash);
      if (pay > 0.005) {
        this.municipality.cash -= pay;
        c.cash += pay;
        c.incomeToday += pay;
        this.ledger.benefits += pay;
        this.record({ type: 'benefit', to: c, amount: pay });
      }
    }
  }

  /**
   * A business that has run out of cash and stock cannot sell its way out.
   * The town can advance it money from the budget; it is a loan, not a gift,
   * and it is paid back out of revenue later.
   */
  supportBusinesses() {
    for (const firm of this.businesses) {
      const hourly = (firm.slots * firm.wage) / HOURS_PER_DAY;
      if (firm.cash > hourly * 2 || firm.stock + firm.inputStock > 2) continue;
      const free = this.municipality.cash - 120;
      if (free < 60) continue;
      const amount = Math.min(free, 220);
      this.municipality.cash -= amount;
      firm.cash += amount;
      firm.debt = (firm.debt || 0) + amount;
      this.ledger.support = (this.ledger.support || 0) + amount;
      this.record({ type: 'support', firm, amount });
    }
  }

  repayDebts() {
    for (const firm of this.businesses) {
      if (!firm.debt) continue;
      const payroll = firm.slots * firm.wage;
      const spare = firm.cash - payroll * 3.2;
      if (spare <= 0) continue;
      const amount = Math.min(firm.debt, spare);
      firm.cash -= amount;
      this.municipality.cash += amount;
      firm.debt -= amount;
      this.ledger.repaid = (this.ledger.repaid || 0) + amount;
    }
  }

  publicWorks() {
    const workshop = this.firms.workshop;
    const reserve = this.firms.city.slots * this.firms.city.wage * 1.2;
    const spend = Math.max(0, this.municipality.cash - reserve) * 0.95;
    if (spend < workshop.price) return;
    const qty = Math.min(Math.floor(spend / workshop.price), Math.floor(workshop.stock));
    if (qty < 1) return;
    const total = qty * workshop.price;
    this.municipality.cash -= total;
    workshop.cash += total;
    workshop.revenueToday += total;
    workshop.soldToday += qty;
    workshop.stock = Math.max(0, workshop.stock - qty);
    this.ledger.publicWorks += total;
    this.record({ type: 'public', to: workshop, amount: total, qty });
  }

  settleDay() {
    this.repayDebts();
    for (const firm of this.firmList) firm.adapt();

    // surplus cash goes back to the people who earned it - but only above what
    // the business needs to keep running (payroll buffer + inventory on hand)
    for (const firm of this.businesses) {
      const inventory = (firm.stock + firm.inputStock) * firm.price * 0.6;
      const reserve = firm.slots * firm.wage * 3 + inventory;
      const surplus = firm.cash - reserve * 2;
      if (surplus > 0 && firm.employees.length) {
        const per = (surplus * 0.35) / firm.employees.length;
        for (const c of firm.employees) {
          firm.cash -= per;
          c.cash += per;
          c.incomeToday += per;
          this.ledger.bonus += per;
          this.record({ type: 'bonus', firm, citizen: c, amount: per });
        }
      }
    }

    for (const c of this.citizens) {
      c.hunger = clamp(c.hunger + 0.55, 0, 2.5);
      if (c.hunger > 1.4) c.morale = clamp(c.morale - 0.1, 0, 1);
      c.incomeToday = 0;
      c.spentToday = 0;
    }
    for (const firm of this.firmList) firm.resetDay();

    const drift = this.auditMoney();
    this.dayStats = { day: this.day, total: this.totalMoney(), drift };
    if (Math.abs(drift) > 0.01) {
      console.warn(`[экономика] деньги изменились на ${drift.toFixed(4)} — где-то печатают или сжигают рубли`);
    }
    this.ledger = this.emptyLedger();
  }

  spawnTruck(from, to) {
    if (from.id === to.id) return;
    const path = this.world.findPath(from.door, to.door);
    if (!path || path.length < 2) return;
    this.trucks.push({ path, index: 1, pos: { ...path[0] }, from, to });
    if (this.trucks.length > 6) this.trucks.shift();
  }

  updateTruck(truck, dt) {
    let remaining = dt * 3.2;
    while (remaining > 0 && truck.index < truck.path.length) {
      const p = truck.path[truck.index];
      const dx = p.x - truck.pos.x;
      const dy = p.y - truck.pos.y;
      const dist = Math.hypot(dx, dy);
      if (dist <= remaining) {
        truck.pos.x = p.x;
        truck.pos.y = p.y;
        remaining -= dist;
        truck.index++;
      } else {
        truck.pos.x += (dx / dist) * remaining;
        truck.pos.y += (dy / dist) * remaining;
        remaining = 0;
      }
    }
    return truck.index >= truck.path.length;
  }

  setSpeed(s) {
    this.timeScale = s;
  }
}
