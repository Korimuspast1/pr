import { World } from './world.js';
import { Effects } from './effects.js';
import { Citizens } from './citizens.js';
import { MAPS, MAP_W, MAP_H } from './maps.js';
import {
  EMPTY, MATERIALS,
} from './materials.js';

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const rnd = (a = 1, b = 0) => b + Math.random() * (a - b);

export const TOOLS = [
  { id: 'hammer', ru: 'Кувалда', en: 'Sledge', icon: '🔨', key: '1' },
  { id: 'ball', ru: 'Шар-баба', en: 'Wrecking ball', icon: '⚫', key: '2' },
  { id: 'dynamite', ru: 'Динамит', en: 'Dynamite', icon: '🧨', key: '3' },
  { id: 'demolish', ru: 'Снос', en: 'Demolition', icon: '🏗', key: '4' },
  { id: 'nuke', ru: 'Ядерная бомба', en: 'Nuke', icon: '☢', key: '5' },
  { id: 'quake', ru: 'Землетрясение', en: 'Earthquake', icon: '〰', key: '6' },
  { id: 'tornado', ru: 'Торнадо', en: 'Tornado', icon: '🌪', key: '7' },
  { id: 'tsunami', ru: 'Цунами', en: 'Tsunami', icon: '🌊', key: '8' },
  { id: 'meteor', ru: 'Метеорит', en: 'Meteor', icon: '☄', key: '9' },
  { id: 'lightning', ru: 'Молния', en: 'Lightning', icon: '⚡', key: '0' },
  { id: 'blackhole', ru: 'Чёрная дыра', en: 'Black hole', icon: '🕳', key: '-' },
  { id: 'build', ru: 'Здание', en: 'Building', icon: '🏢', key: 'B' },
  { id: 'brush', ru: 'Материалы', en: 'Materials', icon: '🧱', key: 'M' },
  { id: 'citizen', ru: 'Житель', en: 'Citizen', icon: '🧍', key: 'C' },
];

export const BUILDINGS = [
  { id: 'panel', ru: 'Панельная 9эт.', en: 'Panel tower', mat: 9, w: 26, floors: 9 },
  { id: 'concrete', ru: 'Монолит 14эт.', en: 'Concrete tower', mat: 3, w: 30, floors: 14 },
  { id: 'steel', ru: 'Стальной небоскрёб', en: 'Steel high-rise', mat: 5, w: 28, floors: 18 },
  { id: 'brick', ru: 'Кирпичный дом', en: 'Brick house', mat: 4, w: 22, floors: 3 },
  { id: 'aerated', ru: 'Газобетонный дом', en: 'Aerated house', mat: 10, w: 24, floors: 3 },
  { id: 'bunker', ru: 'Бункер', en: 'Bunker', mat: 3, w: 28, floors: 2 },
];

const BRUSH_MATS = [1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 18, 19, 20, 21];

export class Game {
  constructor(canvas, audio, ui) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.audio = audio;
    this.ui = ui;
    this.cell = 26;
    this.cam = { x: 0, y: 0, zoom: 0.34 };
    this.effects = new Effects(this);
    this.citizens = new Citizens(this);
    this.projectiles = [];
    this.bombs = [];
    this.defense = [];
    this.lights = [];
    this.state = 'play';
    this.timeScale = 1;
    this.lang = 'ru';
    this.tool = 'hammer';
    this.building = 'panel';
    this.brushMat = 1;
    this.nukeYield = 8;
    this.showUI = true;
    this.stats = { destroyed: 0, people: 0, lost: 0, nukes: 0 };
    this.pointer = { x: 0, y: 0, wx: 0, wy: 0, cx: 0, cy: 0, down: false, button: 0, dragging: false, dragStart: null };
    this.keys = new Set();
    this.quake = null;
    this.fallout = 0;
    this.falloutIntensity = 0;
    this.time = 0;
    this.mapName = 'city';
    this.crumbleBurst = 0;
    this.notify = null;
    this.slowmo = false;
    this.secret = 0;
    this.frame = 0;
    this.wind = 0.35;
    this.defenseLog = 0;
    this.loadMap('city');
  }

  // -------------------------------------------------------------- world setup
  loadMap(name) {
    this.mapName = name;
    const built = MAPS[name].build();
    this.world = built.world;
    this.spawns = built.spawns;
    this.lights = built.lights || [];
    this.defense = built.defense || [];
    this.effects.reset();
    this.effects.g = this;
    this.citizens.reset();
    this.projectiles.length = 0;
    this.quake = null;
    this.fallout = 0;
    this.stats = { destroyed: 0, people: 0, lost: 0, nukes: 0 };
    this.time = 0;
    this.world.wind = this.wind;
    this.hookWorld();
    if (!this.world.terrainAnchor.some((v) => v)) this.world.captureTerrainAnchors();
    this.world.cleanUnsupported();
    this.world.settle();
    this.world.computeSpan();
    for (const [x, y] of this.spawns) this.citizens.spawn(x, y);
    this.stats.people = this.citizens.countAlive();
    this.world.recomputeSupport();
    // camera on the middle of the map, street near the bottom of the screen
    this.cam.zoom = 0.26;
    const groundPx = this.world.groundY * this.cell;
    this.cam.x = (MAP_W * this.cell) / 2 - this.canvas.width / 2 / this.cam.zoom;
    this.cam.y = groundPx - this.canvas.height * 0.8 / this.cam.zoom;
    this.clampCamera();
    if (this.ui) this.ui.onMapLoaded(this);
  }

  hookWorld() {
    const w = this.world;
    w.hooks.destroy = (x, y, m, soft) => {
      this.stats.destroyed++;
      const px = x * this.cell + this.cell / 2;
      const py = y * this.cell + this.cell / 2;
      if (this.frame % 4 === 0) {
        const mat = MATERIALS[m];
        if (m === 6) this.effects.glassBurst(px, py, 3);
        else if (m !== 17) this.effects.chips(px, py, 2, mat.color, 0.6);
        if (Math.random() < 0.5) this.effects.dust(px, py, 1, 0.5);
      }
      if (this.frame % 3 === 0 && MATERIALS[m].sound) this.audio.hit(MATERIALS[m].sound, 0.7);
    };
    w.hooks.crumble = (x, y, m) => {
      const px = x * this.cell, py = y * this.cell;
      this.crumbleBurst++;
      if (this.crumbleBurst % 3 === 0) this.effects.dust(px, py, 1, 0.8);
    };
    w.hooks.fire = (x, y) => {
      if (this.frame % 2 === 0) this.effects.fire(x * this.cell, y * this.cell, 1);
      this.dangerX = x * this.cell;
      this.dangerTimer = 2.5;
    };
    w.hooks.blast = (x, y, r, power, type, n) => {
      this.dangerX = x * this.cell;
      this.dangerTimer = 4;
      const px = x * this.cell, py = y * this.cell;
      const radius = r * this.cell;
      this.effects.explosion(px, py, radius, power, type);
      this.audio.explosion(power, type === 'nuke' ? 'nuke' : type === 'ammo' ? 'ammo' : 'normal');
      this.pushCitizens(px, py, radius * 1.6, power * 1.6);
      this.effects.computeShadowMask(px, py, Math.min(900, radius * 2.4));
    };
  }

  // -------------------------------------------------------------------- utils
  centerX() { return this.cam.x + this.canvas.width / 2 / this.cam.zoom; }
  visibleCenterX() { return this.centerX(); }

  cellAt(wx, wy) {
    return [Math.floor(wx / this.cell), Math.floor(wy / this.cell)];
  }

  screenToWorld(sx, sy) {
    const r = this.canvas.getBoundingClientRect();
    const x = (sx - r.left) * (this.canvas.width / r.width);
    const y = (sy - r.top) * (this.canvas.height / r.height);
    return { sx: x, sy: y, x: x / this.cam.zoom + this.cam.x, y: y / this.cam.zoom + this.cam.y };
  }

  clampCamera() {
    const viewW = this.canvas.width / this.cam.zoom;
    const viewH = this.canvas.height / this.cam.zoom;
    const maxX = MAP_W * this.cell - viewW;
    const maxY = MAP_H * this.cell - viewH;
    this.cam.x = clamp(this.cam.x, Math.min(0, maxX), Math.max(0, maxX));
    this.cam.y = clamp(this.cam.y, Math.min(0, maxY), Math.max(0, maxY));
  }

  pushCitizens(x, y, radius, power) {
    for (const c of this.citizens.list) {
      c.blast(x, y, radius, power * 3);
    }
  }

  toast(text) {
    if (this.ui) this.ui.toast(text);
  }

  setTool(id) {
    this.tool = id;
    if (this.ui) this.ui.onToolChanged(this);
    this.audio.click();
  }

  setMap(name) {
    this.loadMap(name);
    this.audio.click();
  }

  // ------------------------------------------------------------------ pointer
  pointerDown(e) {
    const p = this.screenToWorld(e.clientX, e.clientY);
    Object.assign(this.pointer, { down: true, button: e.button, x: p.sx, y: p.sy, wx: p.x, wy: p.y });
    const [cx, cy] = this.cellAt(p.x, p.y);
    this.pointer.cx = cx;
    this.pointer.cy = cy;
    this.pointer.dragStart = { x: p.x, y: p.y, camX: this.cam.x, camY: this.cam.y };
    if (e.button === 1 || e.button === 2 || this.spaceDown) {
      this.pointer.dragging = true;
      return;
    }
    if (this.state !== 'play') return;
    this.useTool(cx, cy, p.x, p.y, true);
  }

  pointerMove(e) {
    const p = this.screenToWorld(e.clientX, e.clientY);
    this.pointer.x = p.sx;
    this.pointer.y = p.sy;
    this.pointer.wx = p.x;
    this.pointer.wy = p.y;
    const [cx, cy] = this.cellAt(p.x, p.y);
    this.pointer.cx = cx;
    this.pointer.cy = cy;
    if (this.pointer.dragging && this.pointer.dragStart) {
      const d = this.pointer.dragStart;
      this.cam.x = d.camX - (p.x - d.x);
      this.cam.y = d.camY - (p.y - d.y);
      this.clampCamera();
    }
    if (this.pointer.down && this.state === 'play' && !this.pointer.dragging) {
      this.useTool(cx, cy, p.x, p.y, false);
    }
  }

  pointerUp() {
    this.pointer.down = false;
    this.pointer.dragging = false;
    this.pointer.dragStart = null;
  }

  wheel(e) {
    const p = this.screenToWorld(e.clientX, e.clientY);
    const before = { x: p.x, y: p.y };
    const k = Math.exp(-e.deltaY * 0.0013);
    this.cam.zoom = clamp(this.cam.zoom * k, 0.1, 1.4);
    this.cam.x = before.x - (p.sx / this.cam.zoom);
    this.cam.y = before.y - (p.sy / this.cam.zoom);
    this.clampCamera();
  }

  // -------------------------------------------------------------------- tools
  useTool(cx, cy, wx, wy, first) {
    const w = this.world;
    switch (this.tool) {
      case 'hammer': {
        this.hammer(cx, cy, first);
        break;
      }
      case 'ball': {
        if (!first) break;
        this.spawnBall(wx, wy);
        break;
      }
      case 'dynamite': {
        if (!first) break;
        this.placeDynamite(wx, wy);
        break;
      }
      case 'demolish': {
        if (!first) break;
        this.controlledDemolition(cx, cy);
        break;
      }
      case 'nuke': {
        if (!first) break;
        this.dropNuke(wx, wy);
        break;
      }
      case 'quake': {
        if (!first) break;
        this.startQuake();
        break;
      }
      case 'tornado': {
        if (!first) break;
        this.effects.tornado(wy, 1);
        this.audio.siren();
        this.toast(this.lang === 'ru' ? 'Торнадо!' : 'Tornado!');
        break;
      }
      case 'tsunami': {
        if (!first) break;
        this.effects.startTsunami(this.world.groundY * this.cell + 20, wx > MAP_W * this.cell / 2 ? -1 : 1, 1);
        this.audio.quake();
        this.toast(this.lang === 'ru' ? 'Цунами!' : 'Tsunami!');
        break;
      }
      case 'meteor': {
        if (first || Math.random() < 0.12) this.spawnMeteor(wx, wy, first ? 2.2 : rnd(1.4, 0.5));
        break;
      }
      case 'lightning': {
        if (!first) break;
        const tip = this.surfaceBelow(cx, cy);
        this.effects.lightning(wx, -200, tip.x, tip.y, false);
        w.explode(cx, tip.y / this.cell, 7, 1.4, { fireChance: 0.7 });
        this.audio.explosion(0.5);
        this.pushCitizens(tip.x, tip.y, 200, 2);
        break;
      }
      case 'blackhole': {
        if (!first) break;
        this.effects.blackhole(wx, wy);
        this.audio.quake();
        this.blackholeAt = { x: cx, y: cy, t: 4 };
        break;
      }
      case 'build': {
        if (!first && !this.pointer.down) break;
        this.placeBuilding(cx, cy);
        break;
      }
      case 'brush': {
        this.paint(cx, cy, first);
        break;
      }
      case 'citizen': {
        if (!first) break;
        this.citizens.spawn(cx, cy);
        this.stats.people++;
        break;
      }
      default: break;
    }
  }

  surfaceBelow(cx, cy) {
    const w = this.world;
    for (let y = Math.max(0, cy); y < w.h; y++) {
      if (w.solidAt(cx, y)) return { x: cx * this.cell, y: y * this.cell };
    }
    return { x: cx * this.cell, y: w.h * this.cell };
  }

  hammer(cx, cy, first) {
    const w = this.world;
    const r = first ? 2.4 : 1.7;
    let hit = false;
    for (let y = Math.floor(cy - r); y <= cy + r; y++) {
      for (let x = Math.floor(cx - r); x <= cx + r; x++) {
        const d = Math.hypot(x - cx, y - cy);
        if (d > r) continue;
        if (!w.solidAt(x, y)) continue;
        hit = true;
        w.damageCell(x, y, (first ? 420 : 210) * (1 - d / (r * 1.6)));
      }
    }
    if (hit) {
      this.effects.chips(cx * this.cell, cy * this.cell, first ? 6 : 2, '#b0a696', 0.8);
      this.effects.dust(cx * this.cell, cy * this.cell, 2, 0.5);
      this.audio.hit('concrete', 0.8);
      this.effects.shake(1.2, 0.12);
    }
  }

  spawnBall(wx, wy) {
    this.projectiles.push({
      x: wx, y: wy - 520, vx: rnd(30, -30), vy: 260, type: 'ball',
      radius: this.cell * 7, power: 2.2, life: 5,
    });
    this.audio.tone(140, 0.5, 0.12, 'triangle', this.audio.master, 90);
  }

  placeDynamite(wx, wy) {
    this.projectiles.push({
      x: wx, y: wy - 30, vx: rnd(20, -20), vy: 60, type: 'dynamite',
      radius: this.cell * 3.4, power: 1.5, fuse: 1.7, gravity: 900,
    });
    this.audio.click();
  }

  controlledDemolition(cx, cy) {
    const w = this.world;
    if (!w.solidAt(cx, cy)) { this.toast(this.lang === 'ru' ? 'Нужно попасть по зданию' : 'Aim at a building'); return; }
    // flood fill the structure (natural terrain is not part of the building)
    const TERRAIN = new Set([1, 2, 8, 16, 17, 18]);
    const seen = new Uint8Array(w.n);
    const q = [w.idx(cx, cy)];
    seen[q[0]] = 1;
    const cells = [];
    while (q.length && cells.length < 26000) {
      const i = q.pop();
      cells.push(i);
      const x = i % w.w, y = (i / w.w) | 0;
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= w.w || ny >= w.h) continue;
          // stay above the street: the pavement joins every building into one mess
          if (ny >= w.groundY) continue;
          // and keep the demolition local to the building you clicked
          if (Math.abs(nx - cx) > 60) continue;
          const j = ny * w.w + nx;
          if (seen[j] || !w.solidAt(nx, ny) || TERRAIN.has(w.mat[j])) continue;
          seen[j] = 1;
          q.push(j);
        }
      }
    }
    if (cells.length < 40) { this.toast(this.lang === 'ru' ? 'Слишком мелко' : 'Too small'); return; }
    // find the lowest row of the structure and blow it out every few columns
    let lowest = 0;
    for (const i of cells) lowest = Math.max(lowest, (i / w.w) | 0);
    const base = cells.filter((i) => ((i / w.w) | 0) >= lowest - 5).map((i) => [i % w.w, (i / w.w) | 0]);
    base.sort((a, b) => a[0] - b[0]);
    // a handful of charges along the bottom storey, like a real controlled blast
    const maxPicks = Math.max(3, Math.min(8, Math.round(Math.sqrt(cells.length) / 3)));
    const span = base.length ? base[base.length - 1][0] - base[0][0] : 1;
    const picks = [];
    for (let k = 0; k < maxPicks; k++) {
      const tx = base.length ? base[0][0] + Math.round((span * (k + 0.5)) / maxPicks) : cx;
      let best = base[0] || [cx, cy];
      for (const c of base) if (Math.abs(c[0] - tx) < Math.abs(best[0] - tx)) best = c;
      if (!picks.some((p) => Math.abs(p.x - best[0]) < 3)) picks.push({ x: best[0], y: best[1] });
    }
    if (picks.length === 0) picks.push({ x: cx, y: cy });
    this.audio.siren();
    this.toast(this.lang === 'ru' ? 'Заряды заложены — подрыв через 2 с' : 'Charges set — detonation in 2 s');
    picks.forEach((p, i) => {
      this.bombs.push({
        t: 2 + i * 0.22,
        fn: () => {
          w.explode(p.x, p.y, 7, 2.4, { fireChance: 0.15 });
          const px = p.x * this.cell, py = p.y * this.cell;
          this.effects.explosion(px, py, 5.5 * this.cell, 1.3, 'demo');
          this.audio.explosion(0.8);
          this.pushCitizens(px, py, 200, 1.5);
        },
      });
    });
  }

  dropNuke(wx, wy) {
    const y = clamp(wy, 100, MAP_H * this.cell - 100);
    this.projectiles.push({
      x: wx, y: -80, vx: 0, vy: 340, type: 'nuke', radius: 5 * this.cell,
      power: 3, gravity: 120, yield: this.nukeYield, targetY: y,
    });
    this.stats.nukes++;
    this.audio.alarm();
    this.audio.siren();
    this.toast(this.lang === 'ru' ? `Пуск! ${this.nukeYield} Мт` : `Launch! ${this.nukeYield} Mt`);
  }

  startQuake() {
    this.quake = { t: 7, power: 1.1 };
    this.audio.quake();
    this.effects.shake(16, 5);
    this.toast(this.lang === 'ru' ? 'Землетрясение!' : 'Earthquake!');
  }

  spawnMeteor(wx, wy, size = 1) {
    // comes in at ~35-55 degrees from the upper left, entering near the screen edge
    const ang = rnd(0.62, 0.95);
    const dist = 900 + Math.random() * 400;
    const sx = wx - Math.cos(ang) * dist * 1.15;
    const sy = wy - Math.sin(ang) * dist;
    this.effects.meteor(sx, Math.max(-400, sy), wx, wy, size);
    this.audio.tone(320, 0.6, 0.08, 'sawtooth', this.audio.master, 90);
  }

  meteorImpact(wx, wy, size) {
    const w = this.world;
    const [cx, cy] = this.cellAt(wx, wy);
    if (this.defense.some((d) => d.kind === 'aa' || d.kind === 'radar')) this.audio.alarm();
    const radius = 6 + size * 9;
    w.explode(cx, cy, radius, 1.2 + size, { fireChance: 0.4 });
    const px = cx * this.cell, py = cy * this.cell;
    this.effects.explosion(px, py, radius * this.cell, 1.1 + size, 'meteor');
    this.effects.dust(px, py, 40, 2);
    this.audio.explosion(1 + size);
    this.pushCitizens(px, py, radius * this.cell * 2, 2.4 * size);
    this.effects.shake(14 * size, 0.7);
    this.toast(size > 1.6 ? (this.lang === 'ru' ? 'Метеорит!' : 'Meteor impact!') : '');
  }

  placeBuilding(cx, cy) {
    const w = this.world;
    const spec = BUILDINGS.find((b) => b.id === this.building) || BUILDINGS[0];
    // ground under the cursor
    let gy = cy;
    while (gy < w.h && !w.solidAt(cx, gy)) gy++;
    const floor = gy;
    if (floor > w.groundY + 30) return;
    if (w.solidAt(cx, floor - 1) && floor < w.groundY) return;
    const bx = clamp(cx - Math.floor(spec.w / 2), 2, MAP_W - spec.w - 2);
    if (spec.id === 'brick' || spec.id === 'aerated') {
      w.stampHouse(bx, floor, spec.w, spec.floors, { mat: spec.mat, roof: 11, floorH: 8 });
    } else if (spec.id === 'bunker') {
      w.rect(bx, floor - 2, spec.w, 2, 3);
      w.rect(bx, floor - 20, spec.w, 18, EMPTY);
      w.rect(bx, floor - 20, spec.w, 2, 13);
      w.rect(bx, floor - 2, 2, 2, 3);
      w.rect(bx + spec.w - 2, floor - 2, 2, 2, 3);
      w.rect(bx, floor - 20, 2, 18, 3);
      w.rect(bx + spec.w - 2, floor - 20, 2, 18, 3);
      w.rect(bx + 12, floor - 20, 3, 12, 3); // vent
    } else {
      w.stampTower(bx, floor, spec.w, spec.floors * 6, { mat: spec.mat, glass: spec.id === 'steel' ? 6 : 12, floorH: 6, colStep: 6 });
    }
    w.settle();
    this.effects.dust(bx * this.cell, floor * this.cell, 30, 2);
    this.audio.collapse(1.4);
    this.toast(this.lang === 'ru' ? 'Здание построено' : 'Building placed');
  }

  paint(cx, cy, first) {
    const w = this.world;
    const r = first ? 3 : 2.4;
    for (let y = Math.floor(cy - r); y <= cy + r; y++) {
      for (let x = Math.floor(cx - r); x <= cx + r; x++) {
        if (Math.hypot(x - cx, y - cy) > r) continue;
        if (this.pointer.button === 2) w.set(x, y, EMPTY);
        else if (!w.solidAt(x, y) || first) w.set(x, y, this.brushMat);
      }
    }
    if (first) w.structuralStep();
  }

  // ------------------------------------------------------------------- update
  update(rawDt) {
    const dt = Math.min(0.05, rawDt) * (this.state === 'play' ? this.timeScale : 0);
    if (this.state !== 'play' || dt <= 0) {
      this.effects.update(0);
      return;
    }
    this.time += dt;
    this.frame++;
    this.wind += Math.sin(this.time * 0.13) * 0.0008 + rnd(0.001, -0.001);
    this.wind = clamp(this.wind, -0.8, 0.8);
    this.world.wind = this.wind;

    this.crumbleBurst = 0;
    if (this.dangerTimer > 0) {
      this.dangerTimer -= dt;
      if (this.dangerTimer <= 0) delete this.dangerX;
    }
    this.world.update(dt, this.time);
    this.citizens.update(dt);
    this.effects.update(dt);

    // falling citizens that die are counted once
    let alive = 0;
    for (const c of this.citizens.list) if (c.alive) alive++;
    this.stats.lost = Math.max(this.stats.lost, this.stats.people - alive);

    this.updateProjectiles(dt);
    this.updateBombs(dt);
    this.updateDefense(dt);
    this.updateHazards(dt);
    this.updateFallout(dt);

    if (this.fallout > 0 && this.frame % 90 === 0) this.audio.rain(2.4);

    // camera keyboard
    const speed = 900 / this.cam.zoom * dt;
    if (this.keys.has('a') || this.keys.has('arrowleft')) this.cam.x -= speed;
    if (this.keys.has('d') || this.keys.has('arrowright')) this.cam.x += speed;
    if (this.keys.has('w') || this.keys.has('arrowup')) this.cam.y -= speed;
    if (this.keys.has('s') || this.keys.has('arrowdown')) this.cam.y += speed;
    this.clampCamera();
  }

  updateProjectiles(dt) {
    const w = this.world;
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const p = this.projectiles[i];
      p.life = (p.life ?? 6) - dt;
      p.vy += (p.gravity ?? 600) * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      if (p.fuse !== undefined) {
        p.fuse -= dt;
        if (p.fuse <= 0) {
          this.projectiles.splice(i, 1);
          this.detonate(p);
          continue;
        }
      }
      const [cx, cy] = this.cellAt(p.x, p.y);
      const hitGround = w.solidAt(cx, cy) || p.y > w.h * this.cell;
      if (p.type === 'ball') {
        if (hitGround) {
          w.explode(cx, cy, 5.5, 2.2, { fireChance: 0.1 });
          this.effects.dust(p.x, p.y, 18, 1.6);
          this.effects.chips(p.x, p.y, 14, '#9a9a94', 1.3);
          this.audio.explosion(0.9);
          this.effects.shake(10, 0.5);
          this.pushCitizens(p.x, p.y, 260, 1.6);
          p.vy *= -0.28;
          p.vx = (p.vx + rnd(260, -260)) * 0.8;
          p.y -= (p.radius ?? 60) * 0.4;
          p.hits = (p.hits || 0) + 1;
          if (p.hits > 3) { this.projectiles.splice(i, 1); }
        }
        continue;
      }
      if (hitGround) {
        this.projectiles.splice(i, 1);
        this.detonate(p);
        continue;
      }
      if (p.type === 'nuke' && p.targetY && p.y >= p.targetY) {
        this.projectiles.splice(i, 1);
        this.detonate(p);
      }
    }
  }

  detonate(p) {
    const w = this.world;
    const [cx, cy] = this.cellAt(p.x, p.y);
    if (p.type === 'nuke') {
      this.nuke(cx, cy, p.yield);
      return;
    }
    const r = (p.radius ?? 40) / this.cell;
    w.explode(cx, cy, r, p.power ?? 1.4, { fireChance: 0.35 });
    this.effects.explosion(p.x, p.y, (p.radius ?? 40), p.power ?? 1, p.type);
    this.audio.explosion(p.power ?? 1);
    this.pushCitizens(p.x, p.y, (p.radius ?? 40) * 3.2, p.power ?? 1);
    w.cutPower(cx, cy, r);
  }

  nuke(cx, cy, megatons = 8) {
    const w = this.world;
    const r = Math.round(16 + megatons * 2.3);
    const px = cx * this.cell, py = cy * this.cell;

    this.audio.explosion(3, 'nuke');
    this.effects.nukeBoom(px, py, r * this.cell);
    this.effects.shake(46, 3);
    this.world.explode(cx, cy, r, 4.2, { fireChance: 0.92 });
    this.world.explode(cx, cy, r * 0.45, 7.5, { fireChance: 1 });
    this.world.addRadiation(cx, cy, r * 2.2, 1.4);
    this.world.cutPower(cx, cy, r * 3);
    this.pushCitizens(px, py, r * this.cell * 3.2, 9);
    this.fallout = Math.max(this.fallout, 26 + megatons * 1.4);
    this.falloutIntensity = Math.max(this.falloutIntensity, r);
    this.toast(this.lang === 'ru' ? `Ядерный удар: ${megatons} Мт` : `Nuclear strike: ${megatons} Mt`);
  }

  startFallout() {
    this.fallout = Math.max(this.fallout, 22);
    this.audio.alarm();
  }

  updateBombs(dt) {
    for (let i = this.bombs.length - 1; i >= 0; i--) {
      const b = this.bombs[i];
      b.t -= dt;
      if (b.t <= 0) { this.bombs.splice(i, 1); b.fn(); }
    }
  }

  updateDefense(dt) {
    if (!this.defense.length) return;
    const w = this.world;
    for (const d of this.defense) {
      d.cooldown = Math.max(0, (d.cooldown || 0) - dt);
      if (d.cooldown > 0) continue;
      if (w.solidAt(d.x, d.y)) continue; // destroyed
      const range = d.kind === 'radar' ? 2400 : 1500;
      let best = null;
      let bestD = range;
      let bestIsMeteor = false;
      for (const p of this.projectiles) {
        if (!['nuke', 'dynamite', 'ball', 'meteor'].includes(p.type)) continue;
        if (p.type === 'meteor' && (p.size ?? 1) > 1.6) continue;
        const dd = Math.hypot(p.x - d.x * this.cell, p.y - d.y * this.cell);
        if (dd < bestD) { bestD = dd; best = p; }
      }
      // small meteors are interceptable too
      for (const m of this.effects.meteors) {
        if (m.size > 1.6) continue;
        const dd = Math.hypot(m.x - d.x * this.cell, m.y - d.y * this.cell);
        if (dd < range * 0.8 && (!best || dd < bestD)) { best = m; bestD = dd; bestIsMeteor = true; }
      }
      if (best) {
        d.cooldown = d.kind === 'radar' ? 0.9 : 1.5;
        // tracer
        const sx = d.x * this.cell, sy = d.y * this.cell;
        this.effects.sparks(best.x, best.y, 10, 1, '#bfe6ff');
        this.effects.ring(best.x, best.y, 10, '#cfe9ff', 2, 400, 0.4);
        this.effects.smoke(best.x, best.y, 5, 0.7, true);
        this.audio.tone(1200, 0.08, 0.05, 'square', this.audio.busAlert, 300);
        this.audio.alarm();
        if (bestIsMeteor) {
          const mi = this.effects.meteors.indexOf(best);
          if (mi >= 0) this.effects.meteors.splice(mi, 1);
        } else {
          const idx = this.projectiles.indexOf(best);
          if (idx >= 0) this.projectiles.splice(idx, 1);
        }
        this.world.explode(this.cellAt(best.x, best.y)[0], this.cellAt(best.x, best.y)[1], best.type === 'nuke' ? 8 : 3, 0.6);
        this.effects.explosion(best.x, best.y, 60, 0.5, 'intercept');
        this.toast(this.lang === 'ru' ? 'ПВО сбила снаряд' : 'Air defence intercepted the projectile');
      }
    }
  }

  updateHazards(dt) {
    const w = this.world;
    if (this.quake) {
      this.quake.t -= dt;
      w.shakeSeed = (w.shakeSeed || 0) + dt;
      const p = this.quake.power;
      const n = 70;
      for (let i = 0; i < n; i++) {
        const cx = Math.floor(rnd(MAP_W));
        // mostly foundations, sometimes higher up: masonry cracks and sags
        const above = Math.random() < 0.55;
        const cy = above
          ? Math.floor(rnd(w.groundY, w.groundY * 0.45))
          : w.groundY + Math.floor(rnd(28, 0));
        const m = w.at(cx, cy);
        if (!m || m === 17) continue;
        if (Math.random() < 0.16 * p) w.damageCell(cx, cy, 200 * p);
      }
      this.effects.shake(3 + Math.random() * 6, 0.25);
      if (Math.random() < 0.12) {
        this.audio.hit('stone', 0.6);
        const cx = Math.floor(rnd(MAP_W)) * this.cell;
        this.effects.dust(cx, w.groundY * this.cell, 4, 1.4);
      }
      if (this.quake.t <= 0) this.quake = null;
    }
    if (this.blackholeAt) {
      const b = this.blackholeAt;
      b.t -= dt;
      const r = 9;
      for (let k = 0; k < 45; k++) {
        const x = Math.round(b.x + rnd(r, -r));
        const y = Math.round(b.y + rnd(r, -r));
        if (w.solidAt(x, y)) w.destroy(w.idx(x, y), 0.5);
      }
      if (b.t <= 0) this.blackholeAt = null;
    }
  }

  updateFallout(dt) {
    if (this.fallout <= 0) { this.falloutIntensity *= 0.98; return; }
    this.fallout -= dt;
    const w = this.world;
    const count = 26;
    for (let i = 0; i < count; i++) {
      const x = this.cam.x + Math.random() * (this.canvas.width / this.cam.zoom);
      const y = this.cam.y + Math.random() * (this.canvas.height / this.cam.zoom) * 0.5;
      this.effects.add({
        type: 'dust', x, y, vx: this.wind * 160 + rnd(20, -20), vy: rnd(70, 30),
        life: 2.2, max: 2.2, size: rnd(1.8, 0.7), color: '#cfd8b0', drag: 1, grav: 30,
      });
    }
    // deposition
    for (let i = 0; i < 8; i++) {
      const cx = Math.floor(rnd(MAP_W));
      const cy = Math.floor(rnd(w.h * 0.6, w.groundY - 40));
      if (!w.inB(cx, cy)) continue;
      const ii = w.idx(cx, cy);
      if (!w.mat[ii] && !w.mat[ii + w.w]) w.rad[ii] = Math.min(0.8, w.rad[ii] + 0.002);
    }
    if (this.fallout <= 0) this.falloutIntensity = 0;
  }

}
