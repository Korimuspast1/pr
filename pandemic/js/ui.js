/* ------------------------------------------------------------------
 * UI — карта, панели, покупки, экраны, звук и игровой цикл.
 * ------------------------------------------------------------------ */
(function () {
  'use strict';

  const DATA = window.GAME_DATA;
  const WORLD = window.WORLD;
  const SIM = window.PandemicSim;
  const clamp = SIM.clamp;
  const SVG_NS = 'http://www.w3.org/2000/svg';
  const $ = (id) => document.getElementById(id);

  const TOTAL_UPGRADES = DATA.UPGRADES.length;
  const DAY_MS = 620; // мс на один игровой день при 1×
  const CURE_RING_LEN = 2 * Math.PI * 42;

  /* ------------------------------------------------------------- утилиты */
  const fmt = (n) => {
    if (!n || n < 1) return '0';
    if (n >= 1e9) return (n / 1e9).toFixed(n >= 1e10 ? 0 : 2).replace('.', ',') + ' млрд';
    if (n >= 1e6) return (n / 1e6).toFixed(n >= 1e7 ? 0 : 1).replace('.', ',') + ' млн';
    if (n >= 1e3) return (n / 1e3).toFixed(0) + ' тыс';
    return Math.round(n).toLocaleString('ru-RU');
  };
  const fmtFull = (n) => Math.round(n).toLocaleString('ru-RU');
  const plural = (n, one, few, many) => {
    const m10 = n % 10;
    const m100 = n % 100;
    if (m10 === 1 && m100 !== 11) return one;
    if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few;
    return many;
  };
  const countryWord = (n) => plural(n, 'страна', 'страны', 'стран');
  const pct = (v) => {
    if (v > 0 && v < 0.0001) return '<0,01%';
    return (v * 100 >= 10 ? (v * 100).toFixed(0) : (v * 100).toFixed(1)).replace('.', ',') + '%';
  };

  const hexToRgb = (h) => {
    const n = parseInt(h.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  };
  const mix = (a, b, t) => {
    const A = hexToRgb(a);
    const B = hexToRgb(b);
    return `rgb(${Math.round(A[0] + (B[0] - A[0]) * t)},${Math.round(A[1] + (B[1] - A[1]) * t)},${Math.round(A[2] + (B[2] - A[2]) * t)})`;
  };
  const COLOR_STOPS = [
    [0.0, '#183a43'],
    [0.07, '#3c6242'],
    [0.2, '#7a7332'],
    [0.4, '#b05a2c'],
    [0.62, '#9c2526'],
    [0.82, '#65131a'],
    [1.0, '#2b0a10'],
  ];
  function heatColor(t) {
    t = clamp(t, 0, 1);
    for (let i = 1; i < COLOR_STOPS.length; i++) {
      if (t <= COLOR_STOPS[i][0]) {
        const [t0, c0] = COLOR_STOPS[i - 1];
        const [t1, c1] = COLOR_STOPS[i];
        return mix(c0, c1, (t - t0) / (t1 - t0));
      }
    }
    return COLOR_STOPS[COLOR_STOPS.length - 1][1];
  }

  /* ---------------------------------------------------------------- звук */
  const Sound = {
    on: true,
    ctx: null,
    init() {
      if (!this.ctx) {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (AC) this.ctx = new AC();
      }
      if (this.ctx && this.ctx.state === 'suspended') this.ctx.resume();
    },
    tone(freq, dur, type, gain, slideTo) {
      if (!this.on) return;
      this.init();
      if (!this.ctx) return;
      const t = this.ctx.currentTime;
      const o = this.ctx.createOscillator();
      const g = this.ctx.createGain();
      o.type = type || 'sine';
      o.frequency.setValueAtTime(freq, t);
      if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
      g.gain.setValueAtTime(0, t);
      g.gain.linearRampToValueAtTime(gain || 0.05, t + 0.012);
      g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
      o.connect(g).connect(this.ctx.destination);
      o.start(t);
      o.stop(t + dur + 0.02);
    },
    click() { this.tone(340, 0.08, 'triangle', 0.035); },
    buy() { this.tone(520, 0.12, 'triangle', 0.05, 880); },
    deny() { this.tone(180, 0.14, 'sawtooth', 0.04, 120); },
    alert() { this.tone(660, 0.18, 'square', 0.035, 440); },
    death() { this.tone(140, 0.35, 'sine', 0.06, 70); },
  };

  /* ----------------------------------------------------------------- UI */
  const UI = {
    game: null,
    paths: new Map(),
    pulses: new Map(),
    speed: 1,
    lastSpeed: 1,
    selected: null,
    hover: null,
    tab: 'transmission',
    running: false,
    started: false,
    lastEventKey: null,
    phase: 'start',
    opts: { pathogen: 'virus', difficulty: 'normal', start: 'RUS' },
    acc: 0,
    lastTick: 0,
    dirty: true,
  };

  /* ------------------------------------------------------------- карта */
  function buildMap() {
    const group = $('countries');
    const pulseGroup = $('pulses');
    const frag = document.createDocumentFragment();
    const pulseFrag = document.createDocumentFragment();

    for (const row of WORLD.countries) {
      const id = row[0];
      const path = document.createElementNS(SVG_NS, 'path');
      path.setAttribute('d', row[3]);
      path.setAttribute('class', 'country');
      path.dataset.id = id;
      path.setAttribute('vector-effect', 'non-scaling-stroke');
      frag.appendChild(path);
      UI.paths.set(id, path);

      const circle = document.createElementNS(SVG_NS, 'circle');
      circle.setAttribute('r', 0);
      circle.setAttribute('fill', '#ff6b52');
      circle.setAttribute('filter', 'url(#glow)');
      circle.setAttribute('class', 'pulse');
      circle.dataset.id = id;
      pulseFrag.appendChild(circle);
      UI.pulses.set(id, circle);
    }
    group.appendChild(frag);
    pulseGroup.appendChild(pulseFrag);

    // подсветка выбранного очага на стартовом экране
    setStartHighlight(UI.opts.start);

    group.addEventListener('mouseover', (e) => {
      const p = e.target.closest('.country');
      if (!p) return;
      UI.hover = p.dataset.id;
      UI.paths.forEach((el) => el.classList.remove('is-hover'));
      p.classList.add('is-hover');
      showTooltip(p.dataset.id);
    });
    group.addEventListener('mouseout', (e) => {
      const p = e.target.closest('.country');
      if (!p) return;
      p.classList.remove('is-hover');
      UI.hover = null;
      hideTooltip();
    });
    group.addEventListener('click', (e) => {
      const p = e.target.closest('.country');
      if (!p) return;
      selectCountry(p.dataset.id);
    });
    $('mapWrap').addEventListener('mousemove', (e) => moveTooltip(e));
    document.getElementById('hotList').addEventListener('click', (e) => {
      const el = e.target.closest('.hot');
      if (el && el.dataset.id) selectCountry(el.dataset.id);
    });
  }

  function setStartHighlight(id) {
    UI.paths.forEach((p, pid) => p.classList.toggle('country--start', pid === id && !UI.started));
  }

  function weatherName(c) {
    return (DATA.CLIMATES[c.climate] || { name: '—' }).name;
  }

  function showTooltip(id) {
    const tt = $('tooltip');
    const g = UI.game;
    const c = g ? g.byId.get(id) : null;
    const row = WORLD.countries.find((r) => r[0] === id);
    if (!row) return;
    const name = row[2];
    const pop = g ? c.pop : row[6];
    let html = `<h3>${name}</h3><div class="tt-sub">${weatherName(g ? c : { climate: row[8] })} климат · плотность ${Math.round(row[7])}/км²</div>`;
    html += `<div class="tt-row"><span>Население</span><b>${fmt(pop)}</b></div>`;
    if (g && c.exp > 0) {
      html += `<div class="tt-row"><span>Заражено</span><b>${fmt(c.exp)}</b></div>`;
      html += `<div class="tt-row"><span>Болеют</span><b>${fmt(c.inf)}</b></div>`;
      html += `<div class="tt-row"><span>Погибло</span><b style="color:#ff7a6c">${fmt(c.dead)}</b></div>`;
    }
    const tags = [];
    if (g && c.detected) tags.push('обнаружено');
    if (g && c.closed) tags.push('границы закрыты');
    if (row[10]) tags.push('без выхода к морю');
    if (g && c.exp >= c.pop * 0.999) tags.push('все заражены');
    if (tags.length) html += `<div class="tt-tag">${tags.map((t) => `<span>${t}</span>`).join('')}</div>`;
    tt.innerHTML = html;
  }

  function moveTooltip(e) {
    const tt = $('tooltip');
    if (!UI.hover) return;
    const wrap = $('mapWrap').getBoundingClientRect();
    const x = e.clientX - wrap.left;
    const y = e.clientY - wrap.top;
    tt.classList.add('is-visible');
    tt.style.left = clamp(x + 16, 8, wrap.width - tt.offsetWidth - 8) + 'px';
    tt.style.top = clamp(y + 16, 8, wrap.height - tt.offsetHeight - 8) + 'px';
  }

  function hideTooltip() {
    $('tooltip').classList.remove('is-visible');
  }

  function selectCountry(id) {
    if (!UI.started) {
      UI.opts.start = id;
      setStartHighlight(id);
      const row = WORLD.countries.find((r) => r[0] === id);
      $('pickName').textContent = row[2];
      $('pickMeta').textContent = `${fmt(row[6])} · ${(DATA.CLIMATES[row[8]] || {}).name || ''} климат`;
      Sound.click();
      return;
    }
    UI.selected = UI.selected === id ? null : id;
    UI.paths.forEach((p, pid) => p.classList.toggle('is-selected', pid === UI.selected));
  }

  /* -------------------------------------------------------- карта: рендер */
  function renderMap(time) {
    const g = UI.game;
    for (const row of WORLD.countries) {
      const id = row[0];
      const path = UI.paths.get(id);
      const pulse = UI.pulses.get(id);
      if (!g) {
        path.style.fill = '';
        continue;
      }
      const c = g.byId.get(id);
      const deadShare = c.dead / c.pop;
      const expShare = c.exp / c.pop;
      let fill;
      if (c.exp <= 0) fill = '#17363f';
      else fill = heatColor(deadShare * 1.35 + expShare * 0.13);
      path.style.fill = fill;
      path.classList.toggle('is-dead', deadShare > 0.9);
      path.classList.toggle('is-closed', c.closed && c.exp > 0);

      if (c.inf > 1 && deadShare < 0.995) {
        const pulseAmt = clamp(Math.log10(1 + c.inf) / 9, 0, 1);
        const r = 1.2 + pulseAmt * 5.5;
        const wob = 0.5 + 0.5 * Math.sin(time / 420 + (row[4] + row[5]) * 0.05);
        pulse.setAttribute('cx', row[4]);
        pulse.setAttribute('cy', row[5]);
        pulse.setAttribute('r', (r * (0.72 + 0.28 * wob)).toFixed(2));
        pulse.setAttribute('opacity', (0.12 + 0.4 * pulseAmt * wob).toFixed(2));
        pulse.setAttribute('fill', deadShare > 0.45 ? '#ff3b2f' : '#ffb03a');
      } else {
        pulse.setAttribute('r', 0);
      }
    }
  }

  /* ------------------------------------------------------------- HUD */
  function renderHud() {
    const g = UI.game;
    if (!g) return;
    const s = g.summary();
    $('statDay').textContent = s.day;
    const d = s.date.split('-');
    $('statDate').textContent = `${d[2]}.${d[1]}.${d[0]}`;
    $('statDna').textContent = Math.floor(g.dna);
    $('footDna').textContent = Math.floor(g.dna);
    $('statInf').textContent = fmt(s.exposed);
    $('statInfPct').textContent = pct(clamp(s.exposed / s.pop, 0, 1)) + ' населения';
    $('statDead').textContent = fmt(s.dead);
    $('statDeadPct').textContent = pct(clamp(s.dead / s.pop, 0, 1)) + ' населения';
    $('statCountries').innerHTML = `${s.countries}<small>/${WORLD.countries.length}</small>`;
    $('footOwned').textContent = `${s.owned} / ${TOTAL_UPGRADES} улучшений`;

    // лекарство
    const cure = s.cure;
    $('curePct').innerHTML = `${Math.floor(cure)}<span>%</span>`;
    $('cureRing').style.strokeDashoffset = CURE_RING_LEN * (1 - cure / 100);
    $('cureCard').classList.toggle('is-critical', cure > 55 && !g.over);
    const labs = g.countries.filter((c) => c.detected && c.response > 0.05).length;
    $('cureLabs').textContent = labs;
    $('cureResist').textContent = Math.round(g.stats.cureResist * 100) + '%';
    $('cureComplex').textContent = '×' + g.complexity().toFixed(2);
    const cureState = $('cureState');
    if (cure >= 100) {
      cureState.textContent = 'готово';
      cureState.className = 'chip chip--danger';
    } else if (cure > 0) {
      cureState.textContent = 'разработка';
      cureState.className = 'chip';
    } else if (g.cureStarted) {
      cureState.textContent = 'начато';
      cureState.className = 'chip';
    } else {
      cureState.textContent = 'не начато';
      cureState.className = 'chip chip--ghost';
    }

    // реакция мира
    $('awareVal').textContent = pct(s.awareness);
    $('awareBar').style.width = (s.awareness * 100).toFixed(1) + '%';
    $('closedVal').textContent = `${s.closed} / ${WORLD.countries.length}`;
    $('closedBar').style.width = ((s.closed / WORLD.countries.length) * 100).toFixed(1) + '%';
    $('detectedVal').textContent = `${s.detected} / ${WORLD.countries.length}`;
    $('exposedVal').textContent = fmt(s.exposed);

    // штамм
    const p = g.pathogen;
    $('strainName').textContent = g.name;
    $('strainType').textContent = p.name;
    const infVal = clamp((g.stats.inf + p.mods.infectivity + g.tune.betaBase) / 0.6, 0, 1);
    const lethVal = clamp((g.stats.leth + p.mods.lethality + 0.00135) / 0.1, 0, 1);
    const visVal = clamp(g.stats.severity / 45, 0, 1);
    $('barInf').style.width = infVal * 100 + '%';
    $('barLeth').style.width = lethVal * 100 + '%';
    $('barVis').style.width = visVal * 100 + '%';
    $('valInf').textContent = Math.round(infVal * 100);
    $('valLeth').textContent = Math.round(lethVal * 100);
    $('valVis').textContent = Math.round(visVal * 100);

    // статус на карте
    const selected = UI.selected ? g.byId.get(UI.selected) : null;
    if (selected) {
      const share = selected.exp / selected.pop;
      $('mapStatusTitle').textContent = `${selected.ru} · ${pct(clamp(share, 0, 1))} заражено`;
      const bits = [`население ${fmt(selected.pop)}`, `заражено ${fmt(selected.exp)}`, `погибло ${fmt(selected.dead)}`];
      if (selected.detected) bits.push('вспышка подтверждена');
      if (selected.closed) bits.push('границы закрыты');
      $('mapStatusSub').textContent = bits.join(' · ');
    } else {
      const phase = describePhase(g);
      $('mapStatusTitle').textContent = phase.title;
      $('mapStatusSub').textContent = phase.sub;
    }

    renderDash();

    // события
    renderLog();

    // улучшения
    updateUpgrades();
  }

  function renderDash() {
    const g = UI.game;
    const total = g.totalPop;
    const dead = g.deadTotal;
    const sick = g.infectedTotal;
    const immune = g.curedTotal;
    const healthy = Math.max(0, total - dead - sick - immune);
    const toPct = (v) => ((v / total) * 100).toFixed(3) + '%';
    $('segHealthy').style.width = toPct(healthy);
    $('segSick').style.width = toPct(sick);
    $('segDead').style.width = toPct(dead);
    $('segImmune').style.width = toPct(immune);
    $('dashTotal').textContent = fmt(total) + ' человек';
    $('lgHealthy').textContent = fmt(healthy);
    $('lgSick').textContent = fmt(sick);
    $('lgDead').textContent = fmt(dead);
    $('lgImmune').textContent = fmt(immune);

    const hot = g.countries
      .filter((c) => c.inf > 0)
      .sort((a, b) => b.inf - a.inf)
      .slice(0, 8);
    const list = $('hotList');
    if (!hot.length) {
      list.innerHTML = '<div class="hot hot--empty">Активных очагов нет. Пока что.</div>';
      return;
    }
    const maxInf = hot[0].inf || 1;
    list.innerHTML = hot
      .map((c) => {
        const share = clamp(c.exp / c.pop, 0, 1);
        return `<div class="hot" data-id="${c.id}">
          <span class="hot__name">${c.ru}</span>
          <span class="hot__val">${fmt(c.inf)}</span>
          <span class="hot__bar"><i style="width:${((c.inf / maxInf) * 100).toFixed(1)}%"></i></span>
        </div>`;
      })
      .join('');
  }

  function describePhase(g) {
    const countries = g.countries.filter((c) => c.exp > 0).length;
    const deadShare = g.deadTotal / g.totalPop;
    if (g.over) {
      return { title: g.outcome === 'victory' ? 'Человечество уничтожено' : 'Патоген уничтожен', sub: g.outcomeText || '' };
    }
    if (deadShare > 0.3) return { title: 'Коллапс цивилизации', sub: `${fmt(g.deadTotal)} погибших. Государства перестают существовать.` };
    if (countries > 90) return { title: 'Глобальная пандемия', sub: `${countries} ${countryWord(countries)} сообщают о случаях заражения.` };
    if (countries > 15) return { title: 'Эпидемия', sub: `${countries} ${countryWord(countries)} затронуто. ВОЗ созывает экстренное совещание.` };
    if (countries > 1) return { title: 'Вспышка', sub: `${countries} ${countryWord(countries)} затронуто. Ситуация под наблюдением.` };
    return { title: 'Латентная фаза', sub: 'Инфекция пока не вышла за пределы одной страны.' };
  }

  function renderLog() {
    const g = UI.game;
    const log = $('log');
    const key = g.events.length + ':' + (g.events[0] ? g.events[0].id : '');
    if (key === UI.lastEventKey) return;
    UI.lastEventKey = key;
    const items = g.events.slice(0, 40);
    const countEl = $('eventCount');
    if (countEl) countEl.textContent = g.events.length;
    log.innerHTML = items
      .map(
        (e) =>
          `<div class="log__item" data-tone="${e.tone}"><span class="log__day">д.${e.day}</span><span>${e.text}</span></div>`,
      )
      .join('');
    const latest = g.events[0];
    if (latest) {
      const t = $('tickerText');
      t.textContent = latest.text;
      t.classList.remove('is-flash');
      void t.offsetWidth;
      t.classList.add('is-flash');
    }
  }

  /* ----------------------------------------------------------- улучшения */
  function buildUpgrades() {
    const box = $('upgrades');
    box.innerHTML = '';
    UI.upgradeEls = new Map();
    const list = DATA.UPGRADES.filter((u) => u.cat === UI.tab).sort((a, b) => a.cost - b.cost);
    for (const u of list) {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'up';
      btn.dataset.id = u.id;
      btn.innerHTML = `
        <span class="up__icon">${u.icon}</span>
        <span class="up__body">
          <span class="up__name">${u.name}</span>
          <span class="up__desc">${u.desc}</span>
          <span class="up__req"></span>
        </span>
        <span class="up__cost">${u.cost}</span>`;
      btn.addEventListener('click', () => onBuy(u.id, btn));
      box.appendChild(btn);
      UI.upgradeEls.set(u.id, btn);
    }
    updateUpgrades(true);
  }

  function updateUpgrades(force) {
    const g = UI.game;
    if (!g || !UI.upgradeEls) return;
    for (const [id, el] of UI.upgradeEls) {
      const def = DATA.UPGRADES.find((u) => u.id === id);
      const cost = g.upgradeCost(def);
      const owned = g.owned.has(id);
      const check = g.canBuy(id);
      const missing = def.req.filter((r) => !g.owned.has(r));
      el.classList.toggle('is-owned', owned);
      el.classList.toggle('is-locked', !owned && check.reason === 'locked');
      el.classList.toggle('is-close', !owned && check.ok && g.dna - cost < 4);
      el.classList.toggle('is-poor', !owned && check.reason === 'cost');
      const costEl = el.querySelector('.up__cost');
      if (costEl) costEl.textContent = owned ? '' : cost;
      const reqEl = el.querySelector('.up__req');
      if (reqEl) {
        reqEl.textContent = missing.length
          ? 'Требуется: ' + missing.map((m) => (DATA.UPGRADES.find((u) => u.id === m) || {}).name).join(', ')
          : '';
      }
    }
    if (force) return;
  }

  function onBuy(id, el) {
    const g = UI.game;
    if (!g || !UI.started || g.over) return;
    const check = g.canBuy(id);
    if (!check.ok) {
      if (check.reason === 'cost') {
        el.classList.remove('just-denied');
        void el.offsetWidth;
        el.classList.add('just-denied');
        Sound.deny();
      }
      return;
    }
    g.buy(id);
    Sound.buy();
    el.classList.remove('just-bought');
    void el.offsetWidth;
    el.classList.add('just-bought');
    UI.dirty = true;
    renderHud();
  }

  /* --------------------------------------------------------------- цикл */

  // Симуляция идёт по таймеру, а не по кадрам: так время не зависит от
  // частоты отрисовки и не замирает в фоновой вкладке.
  function tick() {
    const now = performance.now();
    const dt = UI.lastTick ? Math.min(2000, now - UI.lastTick) : 0;
    UI.lastTick = now;
    if (!UI.started || !UI.game) return;
    if (UI.game.over) {
      if (!UI.endShown) showEnd();
      return;
    }
    if (UI.speed <= 0) return;
    UI.acc += dt * UI.speed;
    let advanced = 0;
    while (UI.acc >= DAY_MS && advanced < 40) {
      UI.acc -= DAY_MS;
      UI.game.step();
      advanced++;
      if (UI.game.over) break;
    }
    if (advanced > 0) renderHud();
    if (UI.game.over && !UI.endShown) showEnd();
  }

  function frame(t) {
    renderMap(t);
    requestAnimationFrame(frame);
  }

  /* -------------------------------------------------------------- старт */
  function buildStartScreen() {
    const pbox = $('pathogens');
    pbox.innerHTML = '';
    for (const p of DATA.PATHOGENS) {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'pathogen' + (p.id === UI.opts.pathogen ? ' is-active' : '');
      b.dataset.id = p.id;
      b.innerHTML = `<span class="pathogen__icon">${p.icon}</span><span class="pathogen__name">${p.name}</span><span class="pathogen__tag">${p.tagline}</span>`;
      b.addEventListener('click', () => {
        UI.opts.pathogen = p.id;
        pbox.querySelectorAll('.pathogen').forEach((x) => x.classList.toggle('is-active', x.dataset.id === p.id));
        Sound.click();
      });
      pbox.appendChild(b);
    }

    const dbox = $('difficulties');
    dbox.innerHTML = '';
    for (const d of DATA.DIFFICULTIES) {
      const b = document.createElement('button');
      b.type = 'button';
      b.textContent = d.name;
      b.className = d.id === UI.opts.difficulty ? 'is-active' : '';
      b.addEventListener('click', () => {
        UI.opts.difficulty = d.id;
        dbox.querySelectorAll('button').forEach((x) => x.classList.toggle('is-active', x.textContent === d.name));
        $('difficultyNote').textContent = d.desc;
        Sound.click();
      });
      dbox.appendChild(b);
    }
    $('difficultyNote').textContent = DATA.DIFFICULTIES.find((d) => d.id === UI.opts.difficulty).desc;

    const row = WORLD.countries.find((r) => r[0] === UI.opts.start);
    $('pickName').textContent = row[2];
    $('pickMeta').textContent = `${fmt(row[6])} · ${(DATA.CLIMATES[row[8]] || {}).name || ''} климат`;
  }

  function startGame() {
    const name = ($('strainInput').value || '').trim() || 'Штамм «Аврора»';
    UI.game = new SIM.Pandemic({
      pathogen: UI.opts.pathogen,
      difficulty: UI.opts.difficulty,
      start: UI.opts.start,
      name,
    });
    UI.started = true;
    UI.endShown = false;
    UI.selected = null;
    UI.speed = 1;
    UI.acc = 0;
    UI.lastTick = performance.now();
    UI.lastEventKey = null;
    UI.paths.forEach((p) => p.classList.remove('country--start', 'is-selected'));
    $('startScreen').classList.add('is-hidden');
    $('endScreen').classList.add('is-hidden');
    $('strainInput').value = name;
    setSpeed(1);
    buildUpgrades();
    renderHud();
    Sound.alert();
  }

  function showEnd() {
    UI.endShown = true;
    setSpeed(0, true);
    const g = UI.game;
    const s = g.summary();
    const victory = g.outcome === 'victory';
    const panel = $('endScreen').querySelector('.ending');
    panel.classList.toggle('ending--victory', victory);
    panel.classList.toggle('ending--defeat', !victory);
    $('endStamp').textContent = victory ? 'ВИРУС ПОБЕДИЛ' : 'ЧЕЛОВЕЧЕСТВО ВЫЖИЛО';
    $('endTitle').textContent = victory ? 'Человечество уничтожено' : 'Патоген остановлен';
    $('endText').textContent = g.outcomeText || '';
    $('endStats').innerHTML = `
      <div><label>Дней</label><b>${s.day}</b></div>
      <div><label>Погибло</label><b>${pct(s.dead / s.pop)}</b></div>
      <div><label>Лекарство</label><b>${Math.floor(s.cure)}%</b></div>
      <div><label>Стран</label><b>${s.countries}</b></div>
      <div><label>Мутаций</label><b>${g.mutations}</b></div>
      <div><label>Улучшений</label><b>${s.owned}</b></div>`;
    $('endScreen').classList.remove('is-hidden');
    if (victory) Sound.death();
    else Sound.alert();
  }

  function setSpeed(v, silent) {
    UI.speed = v;
    document.querySelectorAll('.speed button').forEach((b) => b.classList.toggle('is-active', +b.dataset.speed === v));
    if (v > 0) UI.lastSpeed = v;
    if (!silent) Sound.click();
  }

  /* ------------------------------------------------------------- события */
  function bindUI() {
    document.querySelectorAll('.speed button').forEach((b) => {
      b.addEventListener('click', () => {
        const v = +b.dataset.speed;
        setSpeed(v === 0 && UI.speed === 0 ? UI.lastSpeed : v);
        if (v === 0) {
          const t = $('tickerText');
          t.textContent = 'Пауза. Думайте спокойно — вирус подождёт.';
        }
      });
    });

    document.querySelectorAll('#tabs button').forEach((b) => {
      b.addEventListener('click', () => {
        UI.tab = b.dataset.cat;
        document.querySelectorAll('#tabs button').forEach((x) => x.classList.toggle('is-active', x === b));
        const cat = DATA.CATEGORIES.find((c) => c.id === UI.tab);
        $('tabHint').textContent = cat ? cat.hint : '';
        buildUpgrades();
        Sound.click();
      });
    });

    $('soundBtn').addEventListener('click', () => {
      Sound.on = !Sound.on;
      $('soundBtn').classList.toggle('is-off', !Sound.on);
      $('soundBtn').textContent = Sound.on ? '♪' : '×';
      if (Sound.on) Sound.click();
    });

    $('helpBtn').addEventListener('click', () => {
      $('helpScreen').classList.remove('is-hidden');
      Sound.click();
    });
    $('helpClose').addEventListener('click', () => {
      $('helpScreen').classList.add('is-hidden');
      Sound.click();
    });
    $('helpScreen').addEventListener('click', (e) => {
      if (e.target === $('helpScreen')) $('helpScreen').classList.add('is-hidden');
    });

    $('menuBtn').addEventListener('click', () => {
      if (!UI.started) return;
      if (UI.game && !UI.game.over && !confirm('Начать новую эпидемию? Текущий прогресс будет потерян.')) return;
      resetToStart();
    });

    $('launchBtn').addEventListener('click', startGame);
    $('againBtn').addEventListener('click', resetToStart);
    $('watchBtn').addEventListener('click', () => {
      $('endScreen').classList.add('is-hidden');
      UI.endShown = true;
    });

    $('strainInput').addEventListener('keydown', (e) => {
      if (e.key === 'Enter') startGame();
      e.stopPropagation();
    });

    document.addEventListener('keydown', (e) => {
      if (e.target.tagName === 'INPUT') return;
      if (e.code === 'Space') {
        e.preventDefault();
        if (!UI.started) return;
        setSpeed(UI.speed === 0 ? UI.lastSpeed : 0);
      }
      if (e.key === '1') setSpeed(1);
      if (e.key === '2') setSpeed(2);
      if (e.key === '3') setSpeed(4);
      if (e.key === 'Escape') {
        $('helpScreen').classList.add('is-hidden');
      }
      if (e.key === 'Escape' && UI.started) {
        UI.selected = null;
        UI.paths.forEach((p) => p.classList.remove('is-selected'));
      }
    });
  }

  function resetToStart() {
    UI.started = false;
    UI.game = null;
    UI.selected = null;
    UI.speed = 1;
    UI.endShown = false;
    UI.lastEventKey = null;
    $('endScreen').classList.add('is-hidden');
    $('startScreen').classList.remove('is-hidden');
    $('log').innerHTML = '';
    $('upgrades').innerHTML = '';
    $('tooltip').classList.remove('is-visible');
    $('strainName').textContent = '—';
    $('strainType').textContent = '—';
    $('mapStatusTitle').textContent = 'Выберите очаг заражения';
    $('mapStatusSub').textContent = 'Кликните по стране на карте, чтобы начать эпидемию именно там';
    $('tickerText').textContent = 'В мире спокойно. Пока что.';
    $('hotList').innerHTML = '<div class="hot hot--empty">Активных очагов нет. Пока что.</div>';
    ['segHealthy', 'segSick', 'segDead', 'segImmune'].forEach((id) => ($(id).style.width = '0%'));
    ['lgHealthy', 'lgSick', 'lgDead', 'lgImmune'].forEach((id) => ($(id).textContent = '—'));
    $('dashTotal').textContent = '7,58 млрд человек';
    document.querySelectorAll('.speed button').forEach((b) => b.classList.toggle('is-active', +b.dataset.speed === 1));
    setStartHighlight(UI.opts.start);
    renderEmptyHud();
  }

  function renderEmptyHud() {
    $('statDay').textContent = '0';
    $('statDna').textContent = '8';
    $('statInf').textContent = '0';
    $('statDead').textContent = '0';
    $('statInfPct').textContent = '0% населения';
    $('statDeadPct').textContent = '0% населения';
    $('statCountries').innerHTML = `0<small>/${WORLD.countries.length}</small>`;
    $('footDna').textContent = '0';
    $('footOwned').textContent = `0 / ${TOTAL_UPGRADES} улучшений`;
    $('curePct').innerHTML = '0<span>%</span>';
    $('cureRing').style.strokeDashoffset = CURE_RING_LEN;
    $('cureState').textContent = 'не начато';
    $('cureState').className = 'chip chip--ghost';
    $('cureLabs').textContent = '0';
    $('cureResist').textContent = '0%';
    $('cureComplex').textContent = '×1.00';
    $('awareVal').textContent = '0%';
    $('awareBar').style.width = '0%';
    $('closedVal').textContent = `0 / ${WORLD.countries.length}`;
    $('closedBar').style.width = '0%';
    $('detectedVal').textContent = '0';
    $('exposedVal').textContent = '0';
    ['barInf', 'barLeth', 'barVis'].forEach((id) => ($(id).style.width = '0%'));
    ['valInf', 'valLeth', 'valVis'].forEach((id) => ($(id).textContent = '0'));
    UI.paths.forEach((p) => {
      p.style.fill = '';
      p.classList.remove('is-closed', 'is-dead', 'is-selected');
    });
    UI.pulses.forEach((c) => c.setAttribute('r', 0));
    renderDashEmpty();
  }

  function renderDashEmpty() {
    $('hotList').innerHTML = '<div class="hot hot--empty">Активных очагов нет. Пока что.</div>';
    ['segHealthy', 'segSick', 'segDead', 'segImmune'].forEach((id) => ($(id).style.width = '0%'));
    ['lgHealthy', 'lgSick', 'lgDead', 'lgImmune'].forEach((id) => ($(id).textContent = '—'));
    $('dashTotal').textContent = '7,58 млрд человек';
  }

  /* --------------------------------------------------------------- init */
  function init() {
    buildMap();
    buildStartScreen();
    bindUI();
    renderEmptyHud();
    requestAnimationFrame(frame);
    setInterval(tick, 100);
    window.addEventListener('resize', () => hideTooltip());
    document.addEventListener('visibilitychange', () => {
      UI.lastTick = performance.now();
      UI.acc = Math.min(UI.acc, DAY_MS);
    });
  }

  window.PandemicUI = UI;
  document.addEventListener('DOMContentLoaded', init);
})();
