/* ------------------------------------------------------------------
 * PandemicSim — вся математика эпидемии, без единой строчки DOM.
 * Работает и в браузере (window.PandemicSim), и в Node (module.exports)
 * — так его можно прогонять в тестах без графики.
 * ------------------------------------------------------------------ */
(function (root) {
  'use strict';

  const DATA = root.GAME_DATA;
  const WORLD = root.WORLD;

  const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
  const lerp = (a, b, t) => a + (b - a) * t;

  function mulberry32(seed) {
    let a = seed >>> 0;
    return function () {
      a |= 0;
      a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const BASE_CLIMATE = { cold: 0.62, mild: 0.92, warm: 1.04, tropical: 1.12, arid: 0.8 };

  function parseCountry(row) {
    return {
      id: row[0],
      name: row[1],
      ru: row[2],
      path: row[3],
      cx: row[4],
      cy: row[5],
      pop: row[6],
      density: row[7],
      climate: row[8],
      wealth: row[9],
      landlocked: !!row[10],
      borders: row[11] ? row[11].split(',') : [],
      region: row[12],
      bbox: [row[13], row[14], row[15], row[16]],
      // динамика
      exp: 0, // всего заражено за эпидемию (накопительно)
      inf: 0, // болеют прямо сейчас
      dead: 0,
      rec: 0,
      detected: false,
      closed: false,
      response: 0,
      everInfected: false,
    };
  }

  const DEFAULT_TUNE = {
    betaBase: 0.115,
    betaInf: 0.55,
    founderBoost: 0.6,
    founderCap: 4000,
    landSeed: 0.03,
    airSeed: 0.0045,
    seaSeed: 0.003,
    seedThreshold: 40,
    cureBase: 4.2,
    dnaBase: 0.3,
    dnaInf: 0.85,
  };

  class Pandemic {
    constructor(opts) {
      opts = opts || {};
      this.tune = Object.assign({}, DEFAULT_TUNE, opts.tune);
      const T = this.tune;
      const data = DATA;
      this.pathogen = data.PATHOGENS.find((p) => p.id === (opts.pathogen || 'virus')) || data.PATHOGENS[0];
      this.difficulty = data.DIFFICULTIES.find((d) => d.id === (opts.difficulty || 'normal')) || data.DIFFICULTIES[1];
      this.upgradeDefs = new Map(data.UPGRADES.map((u) => [u.id, u]));

      this.seed = opts.seed == null ? (Math.random() * 1e9) | 0 : opts.seed;
      this.rng = mulberry32(this.seed);

      this.name = opts.name || 'Нулевой пациент';
      this.day = 0;
      this.date = new Date(Date.UTC(2026, 0, 1));
      this.dna = 8;
      this.owned = new Set();
      this.over = false;
      this.outcome = null;

      this.countries = WORLD.countries.map(parseCountry);
      this.byId = new Map(this.countries.map((c) => [c.id, c]));
      for (const c of this.countries) {
        c.borders = c.borders.filter((b) => this.byId.has(b));
      }

      this.totalPop = this.countries.reduce((a, c) => a + c.pop, 0);
      this.events = [];
      this.stats = this.computeStats();

      const startId = this.byId.has(opts.start) ? opts.start : 'RUS';
      this.startId = startId;
      const start = this.byId.get(startId);
      if (start) {
        start.exp = 4;
        start.inf = 4;
        start.everInfected = true;
        this.pushEvent('day0', `Нулевой пациент зафиксирован: ${start.ru}`, 'info');
      }
      this.infectedTotal = 4;
      this.exposedTotal = 4;
      this.deadTotal = 0;
      this.curedTotal = 0;
      this.detectedCount = 1;
      this.closedCount = 0;
      this.awareness = 0;
      this.researchPower = 0;
      this.cure = 0;
      this.cureStarted = false;
      this.mutations = 0;
      this.lastEventDay = {};
    }

    /* ------------------------------------------------------------ утилиты */

    pushEvent(kind, text, tone) {
      this.events.unshift({ day: this.day, kind, text, tone: tone || 'info', id: this.events.length + '-' + this.day });
      if (this.events.length > 90) this.events.pop();
    }

    complexity() {
      return this.pathogen.mods.complexity * (1 + 0.012 * this.stats.symptomCount);
    }

    computeStats() {
      const s = {
        inf: 0,
        leth: 0,
        severity: 0,
        air: 0,
        water: 0,
        cureResist: 0,
        stealth: 0,
        mutation: 1,
        dnaRate: 0,
        climate: { cold: 0, mild: 0, warm: 0, tropical: 0, arid: 0, humid: 0 },
        symptomCount: 0,
        transmissionCount: 0,
        abilityCount: 0,
      };
      for (const id of this.owned) {
        const u = this.upgradeDefs.get(id);
        if (!u) continue;
        const fx = u.fx || {};
        s.inf += fx.inf || 0;
        s.leth += fx.leth || 0;
        s.severity += fx.severity || 0;
        s.air = Math.max(s.air, fx.air || 0);
        s.water = Math.max(s.water, fx.water || 0);
        s.cureResist += fx.cureResist || 0;
        s.stealth += fx.stealth || 0;
        s.mutation *= fx.mutation || 1;
        s.dnaRate += fx.dnaRate || 0;
        if (fx.climate) {
          for (const k of Object.keys(fx.climate)) s.climate[k] = (s.climate[k] || 0) + fx.climate[k];
        }
        if (u.cat === 'symptom') s.symptomCount++;
        if (u.cat === 'transmission') s.transmissionCount++;
        if (u.cat === 'ability') s.abilityCount++;
      }
      s.cureResist = Math.min(0.85, s.cureResist + (this.pathogen.mods.cureResist || 0));
      s.severityMul = 1 + s.severity * 0.055;
      return s;
    }

    climateFactor(c) {
      let base = BASE_CLIMATE[c.climate] ?? 0.9;
      const bonus = this.stats.climate[c.climate] || 0;
      base += bonus;
      if (c.climate === 'tropical') base += this.stats.climate.humid || 0;
      return clamp(base, 0.15, 1.85);
    }

    upgradeCost(u) {
      const p = this.pathogen.mods;
      const catMul = u.cat === 'symptom' ? p.symptomCost : u.cat === 'ability' ? p.abilityCost : 1;
      return Math.max(1, Math.round(u.cost * catMul));
    }

    /* ------------------------------------------------------- покупка ДНК */

    canBuy(id) {
      const u = this.upgradeDefs.get(id);
      if (!u) return { ok: false, reason: 'missing' };
      if (this.owned.has(id)) return { ok: false, reason: 'owned' };
      const missing = u.req.filter((r) => !this.owned.has(r));
      if (missing.length) return { ok: false, reason: 'locked', missing };
      if (this.dna < this.upgradeCost(u)) return { ok: false, reason: 'cost' };
      return { ok: true };
    }

    buy(id) {
      const check = this.canBuy(id);
      if (!check.ok) return check;
      const u = this.upgradeDefs.get(id);
      this.dna -= this.upgradeCost(u);
      this.owned.add(id);
      this.stats = this.computeStats();
      this.pushEvent('buy', `Эволюция: ${u.name}`, 'good');
      return { ok: true, upgrade: u };
    }

    grantMutation(forcedId) {
      const candidates = DATA.UPGRADES.filter(
        (u) => u.cat === 'symptom' && !this.owned.has(u.id) && u.req.every((r) => this.owned.has(r)),
      );
      if (!candidates.length) return null;
      const u = forcedId ? candidates.find((c) => c.id === forcedId) : candidates[Math.floor(this.rng() * candidates.length)];
      if (!u) return null;
      this.owned.add(u.id);
      this.stats = this.computeStats();
      this.mutations++;
      this.pushEvent('mutation', `Мутация: ${u.name}`, 'mutation');
      return u;
    }

    /* ------------------------------------------------------ основной шаг */

    step() {
      if (this.over) return this;
      this.day++;
      this.date = new Date(Date.UTC(2026, 0, 1) + this.day * 86400000);

      const s = this.stats;
      const p = this.pathogen.mods;
      let infectedTotal = 0;
      let deadTotal = 0;
      let curedTotal = 0;

      // 1. Внутри стран: заражение, смерти, выздоровление
      for (const c of this.countries) {
        if (c.exp > 0 && c.exp < c.pop) {
          const climateF = this.climateFactor(c);
          const densityF = clamp(0.65 + 0.55 * Math.log10(1 + c.density / 15), 0.7, 2.1);
          const healthF = 1 - 0.33 * c.wealth;
          const responseF = 1 - 0.42 * c.response;
          const founder = 1 + this.tune.founderBoost * Math.max(0, 1 - c.exp / this.tune.founderCap);
          let beta =
            (this.tune.betaBase + (s.inf + p.infectivity) * this.tune.betaInf) *
            climateF * densityF * healthF * responseF * founder;
          // Вакцинация: после готовности лекарства новые заражения прекращаются
          if (this.cure >= 100) beta = 0;
          const cap = Math.max(0, 1 - c.exp / c.pop);
          c.exp = Math.min(c.pop, c.exp + c.exp * beta * cap);
        }

        if (c.exp > 0) {
          c.inf = Math.max(0, c.exp - c.dead - c.rec);
          const leth = (0.00135 + s.leth + p.lethality) * (1 - 0.22 * c.wealth);
          if (leth > 0) {
            const deaths = c.inf * Math.min(1, leth);
            c.dead += deaths;
            c.inf -= deaths;
          }
          if (this.cure >= 100 && c.inf > 0) {
            const cured = c.inf * 0.62;
            c.inf -= cured;
            c.rec += cured;
          }
          if (c.inf < 0.0001) c.inf = 0;
          if (c.dead >= c.pop * 0.9995) {
            c.dead = c.pop;
            c.inf = 0;
            c.exp = c.pop;
          }
        }

        infectedTotal += c.inf;
        deadTotal += c.dead;
        curedTotal += c.rec;
      }

      this.infectedTotal = infectedTotal;
      this.exposedTotal = this.countries.reduce((a, c) => a + c.exp, 0);
      this.deadTotal = deadTotal;
      this.curedTotal = curedTotal;

      // 2. Перелёты и переходы через границы
      this.spread();

      // 3. Обнаружение и реакция государств
      this.detectAndRespond();

      // 4. Разработка лекарства
      this.research();

      // 5. ДНК, мутации
      const infFactor = clamp(Math.log10(1 + this.exposedTotal) / 9.6, 0, 1);
      const dnaRate = this.tune.dnaBase + this.tune.dnaInf * Math.pow(infFactor, 0.6);
      this.dna += dnaRate * this.difficulty.dna * p.dna * (1 + this.stats.dnaRate);

      if (this.rng() < 0.0065 * this.stats.mutation * p.mutation) {
        this.grantMutation();
      }

      this.checkOutcome();
      return this;
    }

    spread() {
      const s = this.stats;
      const travelMul = this.difficulty.travel;
      // Источники распространения — страны, где прямо сейчас есть живые носители
      const sources = this.countries.filter((c) => c.inf >= this.tune.seedThreshold);
      if (!sources.length) return;

      for (const src of sources) {
        const prevalence = clamp(src.exp / src.pop, 0, 1);
        const strength = clamp(0.25 + prevalence * 14 + Math.log10(1 + src.exp) * 0.13, 0, 2.6);
        const outboundMul = src.closed ? 0.12 : 1;

        // сухопутные соседи
        for (const nbId of src.borders) {
          const nb = this.byId.get(nbId);
          if (!nb || nb.inf > 0) continue;
          let chance = this.tune.landSeed * strength * travelMul * outboundMul * (nb.closed ? 0.08 : 1);
          chance *= 0.55 + 0.9 * (nb.density > 60 ? 1 : 0.7);
          if (this.rng() < chance) this.infectCountry(nb, src);
        }

        // воздушное сообщение
        const hub = src.wealth > 0.55 || s.air > 0;
        let airChance = this.tune.airSeed * strength * travelMul * outboundMul * (hub ? 1 : 0.4) * (1 + s.air * 0.55);
        if (this.rng() < airChance) {
          const target = this.randomTarget(src, (c) => !c.landlocked && (c.wealth > 0.32 || this.rng() < 0.3));
          if (target) this.infectCountry(target, src);
        }

        // морские порты
        if (!src.landlocked && (s.water > 0 || src.wealth > 0.6)) {
          const seaChance = this.tune.seaSeed * strength * travelMul * outboundMul * (1 + s.water * 1.1);
          if (this.rng() < seaChance) {
            const target = this.randomTarget(src, (c) => !c.landlocked);
            if (target) this.infectCountry(target, src);
          }
        }
      }
    }

    randomTarget(src, predicate) {
      const pool = [];
      let weight = 0;
      for (const c of this.countries) {
        if (c.inf > 0 || c === src || !predicate(c)) continue;
        const w = Math.pow(c.pop, 0.42) * (0.4 + c.wealth);
        pool.push([c, w]);
        weight += w;
      }
      if (!pool.length) return null;
      let r = this.rng() * weight;
      for (const [c, w] of pool) {
        r -= w;
        if (r <= 0) return c;
      }
      return pool[pool.length - 1][0];
    }

    infectCountry(target, src) {
      const seeded = clamp(Math.round(2 + Math.log10(1 + target.pop) * 0.7), 1, 12);
      target.exp = Math.min(target.pop, target.exp + seeded);
      target.inf = Math.max(target.inf, Math.min(target.exp - target.dead - target.rec, target.exp));
      target.everInfected = true;
      if (!target.detected) {
        this.pushEvent('spread', `Первые случаи заражения: ${target.ru}`, 'warn');
      }
      // поощрение игрока за расширение ареала
      this.dna += 0.4;
    }

    detectAndRespond() {
      const s = this.stats;
      let detected = 0;
      let closedCount = 0;
      let awarenessWeight = 0;
      let researchPower = 0;

      for (const c of this.countries) {
        if (c.inf > 0 && !c.detected) {
          const threshold = (Math.max(18, c.pop * 0.000012) * (1 + s.stealth)) / s.severityMul;
          if (c.inf >= threshold) {
            c.detected = true;
            this.pushEvent('detected', `${c.ru}: вспышка подтверждена лабораторно`, 'warn');
          }
        }

        if (c.detected) {
          c.response = clamp(c.response + 0.014 * (0.55 + c.wealth) * this.difficulty.detect, 0, 1);
          if (!c.closed && c.response > 0.32 + 0.25 * this.rng()) {
            c.closed = true;
            this.pushEvent('closed', `${c.ru} закрывает границы`, 'warn');
          }
        }

        if (c.detected) {
          detected++;
          awarenessWeight += c.pop * (0.35 + 0.65 * c.response);
          if (c.response > 0.05) researchPower += (c.pop / this.totalPop) * Math.pow(c.wealth, 1.4) * c.response;
        }
        if (c.closed) closedCount++;
        if (c.dead > 0 && c.inf === 0 && c.dead >= c.pop * 0.995 && !c.wiped) {
          c.wiped = true;
          this.pushEvent('wiped', `${c.ru}: население уничтожено`, 'death');
        }
      }

      this.detectedCount = detected;
      this.closedCount = closedCount;
      this.awareness = clamp(awarenessWeight / this.totalPop, 0, 1);
      this.researchPower = clamp(researchPower, 0, 1);
    }

    research() {
      if (!this.cureStarted && this.awareness > 0.02) {
        this.cureStarted = true;
        this.pushEvent('cure', 'ВОЗ объявляет о начале работ над лекарством', 'good');
      }
      if (!this.cureStarted) return;

      const rate =
        (this.tune.cureBase * this.researchPower * this.difficulty.cure * (1 - this.stats.cureResist)) /
        this.complexity() *
        (0.35 + 0.65 * this.awareness);
      this.cure = clamp(this.cure + rate, 0, 100);

      const milestones = [25, 50, 75, 100];
      for (const m of milestones) {
        if (this.cure >= m && this.lastEventDay['cure' + m] !== true) {
          this.lastEventDay['cure' + m] = true;
          if (m === 100) {
            this.pushEvent('cure', 'ЛЕКАРСТВО ГОТОВО. Начинается массовая вакцинация.', 'death');
          } else {
            this.pushEvent('cure', `Лекарство разработано на ${m}%`, 'good');
          }
        }
      }
    }

    checkOutcome() {
      const deadShare = this.deadTotal / this.totalPop;
      if (deadShare >= 0.995) {
        this.finish(
          'victory',
          this.cure >= 100
            ? 'Лекарство появилось слишком поздно. Человечество уничтожено.'
            : 'Человечество уничтожено. Планета принадлежит патогену.',
        );
        return;
      }
      if (this.cure >= 100 && this.infectedTotal < this.totalPop * 0.0008) {
        this.finish('defeat', 'Вакцинация завершена. Патоген полностью уничтожен.');
        return;
      }
      if (this.infectedTotal === 0 && this.exposedTotal === 0) {
        this.finish('defeat', 'Инфекция угасла, не успев начаться.');
      }
    }

    finish(outcome, text) {
      this.over = true;
      this.outcome = outcome;
      this.outcomeText = text;
      this.pushEvent('end', text, outcome === 'victory' ? 'death' : 'good');
    }

    /* ---------------------------------------------------------- сводки */

    summary() {
      return {
        day: this.day,
        date: this.date.toISOString().slice(0, 10),
        dna: Math.floor(this.dna),
        infected: this.infectedTotal,
        exposed: this.exposedTotal,
        dead: this.deadTotal,
        cured: this.curedTotal,
        pop: this.totalPop,
        detected: this.detectedCount,
        closed: this.closedCount,
        awareness: this.awareness,
        cure: this.cure,
        countries: this.countries.filter((c) => c.inf > 0 || c.dead > 0 || c.rec > 0).length,
        owned: this.owned.size,
      };
    }

    countryView(c) {
      return {
        id: c.id,
        name: c.ru,
        pop: c.pop,
        inf: c.inf,
        dead: c.dead,
        rec: c.rec,
        detected: c.detected,
        closed: c.closed,
        climate: c.climate,
        wealth: c.wealth,
      };
    }
  }

  const api = { Pandemic, parseCountry, clamp, lerp, mulberry32 };
  root.PandemicSim = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof globalThis !== 'undefined' ? globalThis : this);
