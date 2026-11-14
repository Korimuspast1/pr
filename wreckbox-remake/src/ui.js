import { TOOLS, BUILDINGS } from './game.js';
import { MATERIALS } from './materials.js';

const STR = {
  ru: {
    'hud.cells': 'разрушено',
    'hud.people': 'жителей',
    'hud.lost': 'пострадало',
    'hud.rad': 'радиация',
    'tab.demolish': 'СНОС',
    'tab.build': 'СТРОЙКА',
    'hud.hint': 'ЛКМ — применить · ПКМ/колесо — панорама · H — скрыть интерфейс · T — замедление · P — пауза · R — новый город · F — полный экран · ~ — секрет',
    'panel.tool': 'Инструмент',
    'panel.yield': 'Мощность заряда',
    'panel.building': 'Тип здания',
    'panel.material': 'Материал',
    'panel.erase': 'ПКМ — стирать',
    'menu.sub': 'песочница разрушения в разрезе · ремейк',
    'menu.play': 'Играть',
    'menu.hint': 'ЛКМ — ломать · колесо — масштаб · H — интерфейс · T — замедление · P — пауза · R — новый город · ~ — секрет',
    'menu.city': 'Город',
    'menu.base': 'Военная база',
    'menu.atomgrad': 'Атомград',
    'menu.city.desc': 'кварталы, метро и путепроводы',
    'menu.base.desc': 'бункер, склад боеприпасов, ПВО',
    'menu.atomgrad.desc': 'реактор в разрезе и убежище',
    hints: {
      hammer: 'Бейте по стене — прочность считается по материалу. Держите ЛКМ, чтобы долбить.',
      ball: 'Тяжёлый шар падает с высоты и проламывает перекрытия.',
      dynamite: 'Заряд с фитилём 1.7 с. Рвёт бетон и сталь.',
      demolish: 'Нажмите на здание: заряды лягут на несущие узлы, дом сложится внутрь себя.',
      nuke: 'Ударная волна, огненный шторм, тени на стенах и радиоактивные осадки.',
      quake: 'Трещины в грунте и фундаментах, здания проседают и рвутся.',
      tornado: 'Воронка ходит по карте, вырывает куски и швыряет обломки.',
      tsunami: 'Волна идёт с края карты и сносит всё на пути.',
      meteor: 'Зажмите ЛКМ — метеоритный дождь. ПВО на базе сбивает мелкие.',
      lightning: 'Разряд бьёт в поверхность, поджигает и колет бетон.',
      blackhole: 'Всё стягивается в точку и исчезает.',
      build: 'Выберите тип дома и поставьте на грунт.',
      brush: 'Рисуйте материал; ПКМ — стирать.',
      citizen: 'Поставьте жителя — тряпичная кукла с реакцией на огонь и взрывы.',
    },
  },
  en: {
    'hud.cells': 'destroyed',
    'hud.people': 'citizens',
    'hud.lost': 'casualties',
    'hud.rad': 'radiation',
    'tab.demolish': 'DEMOLISH',
    'tab.build': 'BUILD',
    'hud.hint': 'LMB — use · RMB/wheel — pan · H — hide UI · T — slow-mo · P — pause · R — new city · F — fullscreen · ~ — secret',
    'panel.tool': 'Tool',
    'panel.yield': 'Yield',
    'panel.building': 'Building type',
    'panel.material': 'Material',
    'panel.erase': 'RMB — erase',
    'menu.sub': 'a cross-section destruction sandbox · remake',
    'menu.play': 'Play',
    'menu.hint': 'LMB — break things · wheel — zoom · H — UI · T — slow-mo · P — pause · R — new city · ~ — secret',
    'menu.city': 'City',
    'menu.base': 'Military base',
    'menu.atomgrad': 'Atomgrad',
    'menu.city.desc': 'blocks, metro and overpasses',
    'menu.base.desc': 'bunker, ammo depot, air defence',
    'menu.atomgrad.desc': 'reactor in section, shelter',
    hints: {
      hammer: 'Hit the wall — damage depends on the material. Hold LMB to keep swinging.',
      ball: 'A heavy ball drops from above and punches through slabs.',
      dynamite: 'A charge with a 1.7 s fuse. Cracks concrete and steel.',
      demolish: 'Click a building: charges go on load-bearing joints and it folds inward.',
      nuke: 'Shockwave, firestorm, shadows on walls and radioactive fallout.',
      quake: 'Cracks in soil and foundations; buildings sag and tear apart.',
      tornado: 'A funnel roams the map, ripping pieces out and flinging debris.',
      tsunami: 'A wave crosses from the edge of the map and sweeps everything away.',
      meteor: 'Hold LMB for a meteor shower. Base air defence shoots down small ones.',
      lightning: 'A bolt strikes the surface, ignites and splits concrete.',
      blackhole: 'Everything is dragged into a point and vanishes.',
      build: 'Pick a building type and place it on the ground.',
      brush: 'Paint material; RMB erases.',
      citizen: 'Spawn a citizen — a ragdoll that reacts to fire and blasts.',
    },
  },
};

export class UI {
  constructor(game, audio) {
    this.g = game;
    this.audio = audio;
    this.$ = (s) => document.querySelector(s);
    this.root = this.$('#hud');
    this.toolbar = this.$('#tools');
    this.panel = this.$('#side-panel');
    this.panelTitle = this.$('#panel-title');
    this.panelBody = this.$('#panel-body');
    this.toastEl = this.$('#toast');
    this.toastTimer = 0;
    this.statEls = {
      destroyed: this.$('#stat-destroyed'),
      people: this.$('#stat-people'),
      lost: this.$('#stat-lost'),
      rad: this.$('#stat-rad'),
    };
    this.consoleOpen = false;
    this.tab = 'demolish';
    this.buildToolbar();
    this.bindTabs();
    this.bind();
    this.applyLang(game.lang);
    this.onToolChanged(game);
    this.setStats({});
  }

  t(key) {
    return STR[this.g.lang][key];
  }

  buildToolbar() {
    this.toolbar.innerHTML = '';
    this.toolEls = {};
    for (const tool of TOOLS) {
      const el = document.createElement('button');
      el.className = 'tool';
      el.innerHTML = `<span class="kb">${tool.key}</span><span class="ic">${tool.icon}</span><span class="nm">${this.g.lang === 'ru' ? tool.ru : tool.en}</span>`;
      el.addEventListener('click', () => this.g.setTool(tool.id));
      this.toolbar.appendChild(el);
      this.toolEls[tool.id] = el;
    }
    if (this.tab) this.filterTools();
  }

  // СНОС / СТРОЙКА tabs, mirroring the reference UI: the demolish tab holds the
  // destruction tools, the build tab holds the building, brush and citizen.
  static TOOL_TAB = { build: 'build', brush: 'build', citizen: 'build' };

  bindTabs() {
    const tabs = this.$('#tabs');
    if (!tabs) return;
    tabs.querySelectorAll('[data-tab]').forEach((el) => {
      el.addEventListener('click', () => {
        this.tab = el.dataset.tab;
        tabs.querySelectorAll('[data-tab]').forEach((b) => b.classList.toggle('active', b.dataset.tab === this.tab));
        this.filterTools();
        this.audio.click();
      });
    });
    this.filterTools();
  }

  filterTools() {
    for (const [id, el] of Object.entries(this.toolEls || {})) {
      const tab = UI.TOOL_TAB[id] || 'demolish';
      el.style.display = tab === this.tab ? '' : 'none';
    }
  }

  bind() {
    const g = this.g;
    this.$('#btn-lang').addEventListener('click', () => {
      this.setLang(g.lang === 'ru' ? 'en' : 'ru');
    });
    this.$('#btn-sound').addEventListener('click', () => {
      this.audio.init();
      const muted = this.audio.vol.master > 0;
      this.audio.setVolume('master', muted ? 0 : 0.8);
      this.$('#btn-sound').textContent = muted ? '🔇' : '🔊';
    });
    this.$('#btn-pause').addEventListener('click', () => this.togglePause());
    this.$('#btn-help').addEventListener('click', () => this.$('#help').classList.toggle('hidden'));
    this.$('#btn-help-close').addEventListener('click', () => this.$('#help').classList.add('hidden'));
    this.$('#btn-full').addEventListener('click', () => this.toggleFullscreen());
    this.$('#map-select').addEventListener('change', (e) => {
      g.setMap(e.target.value);
      this.audio.init();
    });
    this.$('#btn-resume').addEventListener('click', () => this.togglePause(false));
    this.$('#btn-new').addEventListener('click', () => {
      g.setMap(g.mapName);
      this.togglePause(false);
    });

    for (const key of ['master', 'explosions', 'ambient', 'alerts']) {
      const el = this.$('#vol-' + key);
      el.addEventListener('input', () => {
        this.audio.init();
        this.audio.setVolume(key, parseFloat(el.value));
      });
    }
    this.$('#rec-mode').addEventListener('change', (e) => {
      this.audio.recording = e.target.checked;
      if (this.audio.recording) this.audio.setVolume('master', 0.6);
    });

    // main menu
    this.selectedMap = this.g.mapName;
    this.$('#btn-play').addEventListener('click', () => this.startGame());
    this.buildMapCards();

    // console
    const input = this.$('#console-input');
    input.addEventListener('keydown', (e) => {
      e.stopPropagation();
      if (e.key === 'Enter') {
        this.runCommand(input.value);
        input.value = '';
      } else if (e.key === 'Escape' || e.key === '`' || e.key === '~') {
        this.toggleConsole(false);
      }
    });
  }

  buildMapCards() {
    const host = this.$('#menu-maps');
    if (!host) return;
    host.innerHTML = '';
    this.mapCards = {};
    for (const id of ['city', 'base', 'atomgrad']) {
      const card = document.createElement('button');
      card.className = 'map-card' + (id === this.selectedMap ? ' sel' : '');
      card.innerHTML = `<canvas width="220" height="84"></canvas>
        <span class="mc-name">${this.t('menu.' + id)}</span>
        <span class="mc-desc">${this.t('menu.' + id + '.desc')}</span>`;
      card.addEventListener('click', () => {
        this.selectedMap = id;
        for (const [k, el] of Object.entries(this.mapCards)) el.classList.toggle('sel', k === id);
        this.audio.init();
        this.audio.click(true);
      });
      host.appendChild(card);
      this.mapCards[id] = card;
      this.drawMapPreview(card.querySelector('canvas'), id);
    }
  }

  // Tiny skyline sketches for the menu cards (no game state needed).
  drawMapPreview(cv, id) {
    const ctx = cv.getContext('2d');
    const W = cv.width, H = cv.height;
    const sky = ctx.createLinearGradient(0, 0, 0, H);
    sky.addColorStop(0, '#1d2a3d');
    sky.addColorStop(0.7, '#6b6a6c');
    sky.addColorStop(1, '#9c8f7c');
    ctx.fillStyle = sky;
    ctx.fillRect(0, 0, W, H);
    const gy = H - 16;
    ctx.fillStyle = '#6b4f3a';
    ctx.fillRect(0, gy, W, H - gy);
    ctx.fillStyle = '#3c3c44';
    ctx.fillRect(0, gy - 2, W, 2);
    const block = (x, w, h, c) => {
      ctx.fillStyle = c;
      ctx.fillRect(x, gy - h, w, h);
      ctx.fillStyle = 'rgba(160,216,232,0.65)';
      for (let yy = gy - h + 5; yy < gy - 4; yy += 7) {
        for (let xx = x + 3; xx < x + w - 3; xx += 6) ctx.fillRect(xx, yy, 3, 3);
      }
    };
    if (id === 'city') {
      block(10, 26, 44, '#b3b0a6');
      block(42, 20, 26, '#9c5a44');
      block(68, 30, 58, '#9a9a94');
      block(104, 24, 34, '#7b8794');
      block(134, 28, 68, '#b3b0a6');
      block(168, 22, 40, '#9c5a44');
      ctx.fillStyle = '#4f7a3a';
      ctx.beginPath(); ctx.arc(58, gy - 30, 7, 0, Math.PI * 2); ctx.fill();
    } else if (id === 'base') {
      block(116, 22, 34, '#9a9a94');
      ctx.fillStyle = '#8f7a3c';
      ctx.fillRect(24, gy - 12, 16, 12);
      ctx.fillRect(44, gy - 12, 16, 12);
      ctx.fillStyle = '#5b5f68';
      ctx.fillRect(70, gy - 10, 22, 6);
      ctx.fillRect(96, gy - 14, 14, 4);
      ctx.strokeStyle = '#5b5f68';
      ctx.beginPath(); ctx.moveTo(70, gy - 4); ctx.lineTo(140, gy - 4); ctx.stroke();
      ctx.fillStyle = '#6b6f7a';
      ctx.fillRect(6, gy + 4, 208, 12);
    } else {
      block(140, 24, 30, '#d7d2c4');
      block(170, 20, 24, '#b3b0a6');
      ctx.fillStyle = '#9a9a94';
      ctx.beginPath();
      ctx.moveTo(24, gy); ctx.quadraticCurveTo(40, gy - 46, 62, gy);
      ctx.lineTo(58, gy); ctx.quadraticCurveTo(42, gy - 36, 28, gy);
      ctx.fill();
      ctx.fillStyle = '#8ad6a0';
      ctx.beginPath(); ctx.arc(100, gy - 18, 8, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = '#6b6f7a';
      ctx.fillRect(78, gy + 4, 140, 12);
    }
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.fillRect(0, 0, W, H);
  }

  startGame() {
    this.audio.init();
    this.$('#menu').classList.add('hidden');
    this.g.setMap(this.selectedMap);
    this.$('#map-select').value = this.selectedMap;
    this.g.state = 'play';
    this.toast(this.g.lang === 'ru' ? 'ЛКМ — ломать. Начните с фундамента.' : 'LMB — break things. Start with the foundation.');
  }

  showMenu() {
    this.$('#menu').classList.remove('hidden');
    this.g.state = 'menu';
    this.buildMapCards();
  }

  setLang(lang) {
    this.g.lang = lang;
    this.$('#btn-lang').textContent = lang.toUpperCase();
    document.documentElement.lang = lang;
    this.applyLang(lang);
    this.onToolChanged(this.g);
    document.querySelectorAll('[data-i18n]').forEach((el) => {
      const k = el.getAttribute('data-i18n');
      const v = STR[lang][k];
      if (v) el.textContent = v;
    });
  }

  applyLang(lang) {
    this.buildToolbar();
    if (this.selectedMap) this.buildMapCards();
    this.$('#btn-lang').textContent = lang.toUpperCase();
    document.querySelectorAll('[data-i18n]').forEach((el) => {
      const k = el.getAttribute('data-i18n');
      const v = STR[lang][k];
      if (v) el.textContent = v;
    });
  }

  onMapLoaded(game) {
    const sel = this.$('#map-select');
    if (sel.value !== game.mapName) sel.value = game.mapName;
    this.toast(game.lang === 'ru' ? 'Карта загружена — ломайте' : 'Map loaded — break things');
  }

  onToolChanged(game) {
    const tool = TOOLS.find((t) => t.id === game.tool);
    for (const [id, el] of Object.entries(this.toolEls || {})) el.classList.toggle('selected', id === game.tool);
    const wantTab = UI.TOOL_TAB[game.tool] || 'demolish';
    if (this.tab !== wantTab) {
      this.tab = wantTab;
      const tabs = this.$('#tabs');
      if (tabs) tabs.querySelectorAll('[data-tab]').forEach((b) => b.classList.toggle('active', b.dataset.tab === wantTab));
      this.filterTools();
    }
    this.panel.classList.add('show');
    this.panelTitle.textContent = (this.g.lang === 'ru' ? tool.ru : tool.en).toUpperCase();
    const hint = STR[this.g.lang].hints[tool.id] || '';
    let html = `<div class="panel-row" style="font-size:11px;line-height:1.5;color:#7b7264">${hint}</div>`;
    if (tool.id === 'nuke') {
      html += `<div class="panel-row"><label>${this.t('panel.yield')} <b id="yield-val">${game.nukeYield} Мт</b></label>
        <input type="range" id="yield" min="1" max="50" step="1" value="${game.nukeYield}" /></div>
        <div class="panel-row" style="display:flex;gap:4px;flex-wrap:wrap">
        ${[1, 8, 20, 50].map((y) => `<button class="opt" data-yield="${y}" style="flex:1;padding:4px 0;text-align:center">${y}</button>`).join('')}
        </div>`;
    } else if (tool.id === 'build') {
      html += `<div class="opt-list">${BUILDINGS.map((b) => `<button class="opt ${b.id === game.building ? 'sel' : ''}" data-building="${b.id}">${this.g.lang === 'ru' ? b.ru : b.en}</button>`).join('')}</div>`;
    } else if (tool.id === 'brush') {
      html += `<div class="swatches">${[1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 18, 19, 20, 21].map((m) => `<button class="swatch ${m === game.brushMat ? 'sel' : ''}" data-mat="${m}" style="background:${MATERIALS[m].color}" title="${this.g.lang === 'ru' ? MATERIALS[m].ru : MATERIALS[m].en}"></button>`).join('')}</div>
        <div class="panel-row" style="font-size:10px;color:#8d877e;margin-top:6px">${this.t('panel.erase')}</div>`;
    }
    this.panelBody.innerHTML = html;

    const yieldEl = this.panelBody.querySelector('#yield');
    if (yieldEl) {
      yieldEl.addEventListener('input', () => {
        game.nukeYield = parseInt(yieldEl.value, 10);
        this.panelBody.querySelector('#yield-val').textContent = `${game.nukeYield} Мт`;
      });
    }
    this.panelBody.querySelectorAll('[data-yield]').forEach((el) => {
      el.addEventListener('click', () => {
        game.nukeYield = parseInt(el.dataset.yield, 10);
        const y = this.panelBody.querySelector('#yield');
        if (y) y.value = game.nukeYield;
        const lbl = this.panelBody.querySelector('#yield-val');
        if (lbl) lbl.textContent = `${game.nukeYield} Мт`;
      });
    });
    this.panelBody.querySelectorAll('[data-building]').forEach((el) => {
      el.addEventListener('click', () => {
        game.building = el.dataset.building;
        this.onToolChanged(game);
      });
    });
    this.panelBody.querySelectorAll('[data-mat]').forEach((el) => {
      el.addEventListener('click', () => {
        game.brushMat = parseInt(el.dataset.mat, 10);
        this.onToolChanged(game);
      });
    });
  }

  setStats(s) {
    const g = this.g;
    this.statEls.destroyed.textContent = (g.stats.destroyed || 0).toLocaleString('ru-RU');
    this.statEls.people.textContent = g.citizens.countAlive();
    this.statEls.lost.textContent = g.stats.lost || 0;
    let rad = 0;
    if (g.world) {
      const w = g.world;
      const cx = Math.floor(g.centerX() / g.cell);
      for (let dx = -6; dx <= 6; dx++) {
        for (let dy = -6; dy <= 6; dy++) {
          if (w.inB(cx + dx, Math.floor(g.cam.y / g.cell) + dy)) {
            rad += w.rad[w.idx(cx + dx, Math.floor(g.cam.y / g.cell) + dy)];
          }
        }
      }
    }
    this.statEls.rad.textContent = Math.round(rad * 100);
  }

  toast(text) {
    if (!text) return;
    this.toastEl.textContent = text;
    this.toastEl.classList.add('show');
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toastEl.classList.remove('show'), 2200);
  }

  togglePause(force) {
    const g = this.g;
    const pause = force !== undefined ? force : g.state === 'play';
    g.state = pause ? 'paused' : 'play';
    this.$('#pause').classList.toggle('hidden', !pause);
    this.$('#btn-pause').textContent = pause ? '▶' : '⏸';
    if (pause) this.audio.init();
  }

  toggleFullscreen() {
    if (!document.fullscreenElement) document.documentElement.requestFullscreen?.();
    else document.exitFullscreen?.();
  }

  toggleUI() {
    this.root.classList.toggle('hidden');
  }

  toggleConsole(force) {
    const open = force !== undefined ? force : !this.consoleOpen;
    this.consoleOpen = open;
    this.$('#console').classList.toggle('hidden', !open);
    if (open) {
      this.audio.init();
      this.$('#console-input').focus();
      if (!this.consoleLogInit) {
        this.consoleLogInit = true;
        this.log(this.g.lang === 'ru'
          ? 'Wreckbox 1.8 · отладочная консоль\nвведите help — список команд'
          : 'Wreckbox 1.8 · debug console\nhelp for commands');
      }
    } else {
      this.$('#console-input').blur();
    }
  }

  log(text) {
    const el = this.$('#console-log');
    const div = document.createElement('div');
    div.textContent = text;
    el.appendChild(div);
    el.scrollTop = el.scrollHeight;
  }

  runCommand(raw) {
    const cmd = raw.trim();
    if (!cmd) return;
    this.log('> ' + cmd);
    const g = this.g;
    const [head, ...args] = cmd.split(/\s+/);
    const a = parseFloat(args[0]);
    switch (head.toLowerCase()) {
      case 'help':
        this.log('boom [r]  nuke [mt]  quake  tornado  tsunami  meteor  rain [s]  wind [v]\nheal  people [n]  fps  map city|base|atomgrad  audio  disco  gravity\nabout  secret');
        break;
      case 'boom': {
        const cx = Math.floor(g.pointer.wx / g.cell), cy = Math.floor(g.pointer.wy / g.cell);
        g.world.explode(cx, cy, a || 10, 2, { fireChance: 0.8 });
        g.effects.explosion(g.pointer.wx, g.pointer.wy, (a || 10) * g.cell, 2);
        g.audio.explosion(2);
        break;
      }
      case 'nuke': {
        const cx = Math.floor(g.pointer.wx / g.cell), cy = Math.floor(g.pointer.wy / g.cell);
        g.nuke(cx, cy, a || 20);
        break;
      }
      case 'quake': g.startQuake(); break;
      case 'tornado': g.effects.tornado(g.pointer.wy, 1); break;
      case 'tsunami': g.effects.startTsunami(g.world.groundY * g.cell, 1, 1); break;
      case 'meteor': g.spawnMeteor(g.pointer.wx, g.pointer.wy, a || 2); break;
      case 'rain': g.fallout = Math.max(g.fallout, a || 20); g.audio.rain(4); break;
      case 'wind': g.wind = a; this.log('wind = ' + g.wind); break;
      case 'heal':
        for (const c of g.citizens.list) { c.hp = 100; c.alive = true; c.conscious = true; c.burning = 0; }
        this.log('citizens healed');
        break;
      case 'people': {
        const n = Math.min(40, a || 5);
        for (let i = 0; i < n; i++) g.citizens.spawn(Math.floor(g.pointer.wx / g.cell) + i * 2, Math.floor(g.pointer.wy / g.cell));
        break;
      }
      case 'map': if (args[0]) g.setMap(args[0]); break;
      case 'audio': this.log('audio ready: ' + this.audio.ready); break;
      case 'disco': {
        for (let i = 0; i < 60; i++) {
          setTimeout(() => {
            const cx = Math.floor(Math.random() * g.world.w);
            const cy = Math.floor(Math.random() * g.world.groundY);
            g.world.explode(cx, cy, 4 + Math.random() * 6, 1.5, { fireChance: 0.5 });
            g.effects.explosion(cx * g.cell, cy * g.cell, 100, 1);
            g.audio.explosion(0.6);
          }, i * 120);
        }
        break;
      }
      case 'gravity': this.log('gravity is a lie — всё падает вниз, как и положено'); break;
      case 'about':
        this.log('Wreckbox remake · фан-ремейк песочницы разрушений\nфизика: support BFS + load transfer + cantilever span\nбез сторонних библиотек, всё в браузере');
        break;
      case 'secret':
        this.log(this.g.lang === 'ru' ? 'Секрет: наберите disco и смотрите.' : 'Secret: type disco and watch.');
        break;
      case 'money':
      case 'lambo':
        this.log(this.g.lang === 'ru' ? 'Денег нет, зато есть кувалда.' : 'No money, but you have a sledgehammer.');
        break;
      default:
        this.log((this.g.lang === 'ru' ? 'неизвестная команда: ' : 'unknown command: ') + head);
    }
  }

  drawCursor(ctx, cam) {
    const g = this.g;
    const p = g.pointer;
    if (!p || p.x === undefined) return;
    const x = p.x, y = p.y;
    ctx.save();
    const tool = g.tool;
    let r = 10;
    if (tool === 'brush') r = 2.4 * g.cell * cam.zoom;
    else if (tool === 'hammer') r = 2.1 * g.cell * cam.zoom;
    else if (tool === 'nuke') r = (16 + g.nukeYield * 2.3) * g.cell * cam.zoom * 0.35;
    else if (tool === 'build') r = 14 * cam.zoom;
    else r = 13;
    ctx.strokeStyle = 'rgba(255, 220, 170, 0.85)';
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.stroke();
    ctx.strokeStyle = 'rgba(255, 220, 170, 0.25)';
    ctx.beginPath();
    ctx.moveTo(x - r - 5, y); ctx.lineTo(x - r + 3, y);
    ctx.moveTo(x + r - 3, y); ctx.lineTo(x + r + 5, y);
    ctx.moveTo(x, y - r - 5); ctx.lineTo(x, y - r + 3);
    ctx.moveTo(x, y + r - 3); ctx.lineTo(x, y + r + 5);
    ctx.stroke();
    if (tool === 'nuke') {
      ctx.setLineDash([4, 6]);
      ctx.strokeStyle = 'rgba(255, 120, 80, 0.55)';
      ctx.beginPath();
      ctx.arc(x, y, (16 + g.nukeYield * 2.3) * g.cell * cam.zoom, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
    ctx.restore();
  }
}
