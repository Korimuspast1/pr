import * as THREE from '../vendor/three.module.js';
import { AudioSys } from './audio.js';
import { World } from './world.js';
import { Human, NAMES_LIST } from './human.js';
import { WEAPONS, WEAPON_BY_ID, makeWeaponMesh } from './weapons3d.js';
import { clamp, rand, pick, chance, TAU, norm, dist, raySegment } from './util3.js';

const TOOLS = [
  { id: 'cut', key: 'Q', name: 'Резать', hint: 'Зажми <b>ЛКМ</b> и веди по телу — клинок режет ровно там, где прицел. Чем глубже водишь, тем быстрее отрывается конечность.' },
  { id: 'shoot', key: 'W', name: 'Стрелять', hint: '<b>ЛКМ</b> — огонь, <b>1–8</b> — оружие. Пули летят из центра прицела; дробовик даёт 9 дробин.' },
  { id: 'inspect', key: 'E', name: 'Осмотр', hint: 'Наведи на человека — справа появятся его данные. <b>Клик</b> закрепляет панель, <b>Esc</b> снимает.' },
  { id: 'spawn', key: 'R', name: 'Спавн', hint: 'Клик по земле — новый человек. <b>Shift</b> + клик — сразу с оружием.' },
  { id: 'grab', key: 'T', name: 'Тянуть', hint: 'Схвати человека и веди мышью — тело станет рэгдоллом и потянется за курсором.' },
  { id: 'heal', key: 'Y', name: 'Лечить', hint: 'Клик — остановить кровь, залечить раны и вывести из шока. Мёртвых не воскрешает.' },
  { id: 'delete', key: 'U', name: 'Удалить', hint: 'Клик по человеку — убрать его из песочницы.' },
];

const SETTINGS = [
  { id: 'ai', label: 'ИИ: МИРНЫЕ', labelOn: 'ИИ: АГРЕССИЯ' },
  { id: 'slow', label: 'ТЕМП x1' },
  { id: 'blood', label: 'СТЕРЕТЬ КРОВЬ' },
  { id: 'sound', label: 'ЗВУК ВКЛ', labelOn: 'ЗВУК ВЫКЛ' },
  { id: 'help', label: 'ПОМОЩЬ' },
  { id: 'pause', label: 'ПАУЗА' },
];

class App {
  constructor() {
    this.canvas = document.getElementById('view');
    try {
      this.renderer = new THREE.WebGLRenderer({ canvas: this.canvas, antialias: true, powerPreference: 'high-performance' });
    } catch (err) {
      this.webglFailed(err);
      return;
    }
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.08;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x0a0f17);
    this.camera = new THREE.PerspectiveCamera(58, window.innerWidth / window.innerHeight, 0.05, 400);
    this.scene.add(this.camera);

    this.audio = new AudioSys();
    this.world = new World(this.scene, this.renderer, this.audio);

    this.humans = [];
    this.debris = [];
    this.log = [];
    this.time = 0;
    this.timeScale = 1;
    this.paused = false;
    this.started = false;
    this.muted = false;
    this.aiAggro = false;
    this.tool = 'cut';
    this.weaponId = 'pistol';
    this.ammo = {};
    this.reloadT = 0;
    this.fireCd = 0;
    this.cutCd = new Map();
    this.hovered = null;
    this.pinned = null;
    this.grab = null;
    this.raycaster = new THREE.Raycaster();
    this.pointer = new THREE.Vector2(0, 0);
    this.mouse = { x: window.innerWidth / 2, y: window.innerHeight / 2, ndcX: 0, ndcY: 0, down: false, inside: false };
    this.keys = new Set();
    this.justPressed = new Set();

    this.cam = { target: new THREE.Vector3(0, 1.05, 0), yaw: 0.6, pitch: 0.30, dist: 8.5 };
    this.dragMode = null;
    this.lastMouse = { x: 0, y: 0 };
    this.recoil = 0;
    this.hoverMarker = this.makeHoverMarker();

    this.buildFPWeapon();
    this.bindInput();
    this.buildUI();
    this.spawnInitial();
    this.updateCamera();

    this.last = performance.now();
    this.loop = this.loop.bind(this);
    requestAnimationFrame(this.loop);
  }

  webglFailed(err) {
    console.error('[3D] WebGL недоступен:', err);
    const start = document.getElementById('start');
    if (!start) return;
    start.classList.remove('hidden');
    start.innerHTML = `<div class="card" style="text-align:center">
      <h1 style="font-size:28px">WEBGL НЕДОСТУПЕН</h1>
      <p class="sub">Браузер не дал контекст WebGL2. Включи аппаратное ускорение или открой игру в Chrome / Firefox.</p>
      <div class="note">${String((err && err.message) || err)}</div>
    </div>`;
  }

  /* ================================================================ */
  /*  UI                                                              */
  /* ================================================================ */

  buildUI() {
    const toolbar = document.getElementById('toolbar');
    toolbar.innerHTML = '';
    this.toolButtons = {};
    for (const t of TOOLS) {
      const b = document.createElement('button');
      b.className = 'tool' + (t.id === this.tool ? ' active' : '');
      b.innerHTML = `<span class="key">${t.key}</span>${t.name}`;
      b.onclick = () => this.setTool(t.id);
      toolbar.appendChild(b);
      this.toolButtons[t.id] = b;
    }

    const settings = document.getElementById('settings');
    settings.innerHTML = '';
    this.settingButtons = {};
    for (const s of SETTINGS) {
      const b = document.createElement('button');
      b.className = 'btn';
      b.textContent = s.label;
      b.onclick = () => this.onSetting(s.id);
      settings.appendChild(b);
      this.settingButtons[s.id] = b;
    }

    const hotbar = document.getElementById('hotbar');
    hotbar.innerHTML = '';
    this.slotButtons = [];
    WEAPONS.forEach((w, i) => {
      const b = document.createElement('button');
      b.className = 'slot' + (w.id === this.weaponId ? ' active' : '');
      b.innerHTML = `<span class="num">${i + 1}</span><span class="ammo"></span><span class="nm">${w.short}</span>`;
      b.onclick = () => this.selectWeapon(w);
      hotbar.appendChild(b);
      this.slotButtons.push(b);
    });

    const cols = document.getElementById('start-cols');
    cols.innerHTML = [
      ['Q', 'резать конечности'], ['W', 'стрелять'],
      ['E', 'осмотр NPC'], ['R', 'спавн людей'],
      ['T', 'тянуть тело'], ['Y', 'лечить'],
      ['ЛКМ', 'действие'], ['ПКМ', 'вращать камеру'],
      ['Колесо', 'зум'], ['Стрелки', 'двигать камеру'],
      ['1–8', 'оружие'], ['Space', 'слоу-мо'],
    ].map(([k, v]) => `<div class="item"><b>${k}</b>${v}</div>`).join('');

    const helpRows = [
      ['ЛКМ', 'действие', 'работает согласно выбранному инструменту'],
      ['ПКМ', 'камера', 'вращение вокруг точки взгляда'],
      ['СКМ', 'камера', 'сдвиг по земле (или стрелки)'],
      ['Колесо', 'зум', 'приближение и отдаление'],
      ['Стрелки', 'камера', 'движение по земле, Shift — быстрее'],
      ['Q / W / E / R / T / Y / U', 'инструменты', 'резать, стрелять, осмотр, спавн, тянуть, лечить, удалить'],
      ['1–8', 'оружие', 'кулаки, нож, мачете, топор, катана, пистолет, ПП, дробовик, СВД'],
      ['Space', 'время', 'x1 → x0.35 → x0.12'],
      ['F', 'житель', 'добавить случайного человека'],
      ['Backspace', 'трупы', 'убрать всех мёртвых'],
      ['P / M / H', 'служебное', 'пауза, звук, справка'],
    ];
    document.getElementById('help-rows').innerHTML = helpRows
      .map(([k, n, d]) => `<div class="help-row"><kbd>${k}</kbd><span class="nm">${n}</span><span class="ds">${d}</span></div>`)
      .join('');

    document.getElementById('help-close').onclick = () => this.toggleHelp(false);
    document.getElementById('pause-resume').onclick = () => this.onSetting('pause');
    const startBtn = document.getElementById('start-go');
    startBtn.onclick = () => this.begin();

    this.updateHint();
    this.updateSettingsUI();
  }

  begin() {
    if (this.started) return;
    this.started = true;
    document.getElementById('start').classList.add('hidden');
    this.audio.ensure();
    this.updateReticle();
  }

  setTool(id) {
    if (id === 'shoot' && WEAPON_BY_ID[this.weaponId].kind !== 'gun') this.selectWeapon(WEAPON_BY_ID.pistol);
    this.tool = id;
    for (const t of TOOLS) this.toolButtons[t.id].classList.toggle('active', t.id === id);
    if (this.grab && this.grab.point) this.grab.point.held = false;
    this.grab = null;
    this.updateHint();
    this.setFPWeapon();
    this.updateReticle();
  }

  updateHint() {
    const t = TOOLS.find((x) => x.id === this.tool);
    document.getElementById('hint').innerHTML = t ? t.hint : '';
  }

  updateReticle() {
    const r = document.getElementById('reticle');
    r.classList.toggle('on', this.started && (this.tool === 'shoot' || this.tool === 'cut'));
  }

  onSetting(id) {
    if (id === 'ai') {
      this.aiAggro = !this.aiAggro;
      for (const h of this.humans) { h.ai.enabled = this.aiAggro; h.ai.target = null; h.ai.state = 'idle'; }
      this.addLog(this.aiAggro ? 'ИИ: люди агрессивны' : 'ИИ: люди мирные', this.aiAggro ? 'warn' : 'good');
    } else if (id === 'slow') {
      const seq = [1, 0.35, 0.12];
      this.timeScale = seq[(seq.indexOf(this.timeScale) + 1) % seq.length];
    } else if (id === 'blood') {
      this.world.clearBlood();
      this.addLog('Кровь стёрта');
    } else if (id === 'sound') {
      this.muted = !this.muted;
      this.audio.setMuted(this.muted);
    } else if (id === 'help') {
      this.toggleHelp(document.getElementById('help').classList.contains('hidden'));
    } else if (id === 'pause') {
      this.paused = !this.paused;
    }
    this.updateSettingsUI();
  }

  updateSettingsUI() {
    const b = this.settingButtons;
    b.ai.textContent = this.aiAggro ? 'ИИ: АГРЕССИЯ' : 'ИИ: МИРНЫЕ';
    b.ai.classList.toggle('on', this.aiAggro);
    b.slow.textContent = this.timeScale === 1 ? 'ТЕМП x1' : `ТЕМП x${this.timeScale}`;
    b.slow.classList.toggle('on', this.timeScale !== 1);
    b.sound.textContent = this.muted ? 'ЗВУК ВЫКЛ' : 'ЗВУК ВКЛ';
    b.sound.classList.toggle('on', !this.muted);
    b.help.classList.toggle('on', !document.getElementById('help').classList.contains('hidden'));
    b.pause.classList.toggle('on', this.paused);
    b.pause.textContent = this.paused ? 'ИГРАТЬ' : 'ПАУЗА';
    const pauseModal = document.getElementById('pause');
    if (pauseModal) pauseModal.classList.toggle('hidden', !this.paused);
  }

  toggleHelp(on) {
    document.getElementById('help').classList.toggle('hidden', !on);
    this.updateSettingsUI();
  }

  addLog(text, kind = '') {
    this.log.unshift({ text, t: 5.5, kind });
    if (this.log.length > 5) this.log.pop();
    this.renderLog();
  }

  renderLog() {
    const el = document.getElementById('log');
    el.innerHTML = this.log.map((l) => `<div class="${l.kind}">${l.text}</div>`).join('');
  }

  selectWeapon(def) {
    this.weaponId = def.id;
    this.reloadT = 0;
    this.fireCd = 0;
    this.setTool(def.kind === 'gun' ? 'shoot' : 'cut');
    this.slotButtons.forEach((b, i) => b.classList.toggle('active', WEAPONS[i].id === def.id));
    this.setFPWeapon();
  }

  updateAmmoUI() {
    WEAPONS.forEach((w, i) => {
      const el = this.slotButtons[i]?.querySelector('.ammo');
      if (!el) return;
      if (w.kind !== 'gun') { el.textContent = ''; return; }
      const mag = this.ammo[w.id] == null ? w.magSize : this.ammo[w.id];
      el.textContent = this.reloadT > 0 && w.id === this.weaponId ? '···' : String(mag);
      el.style.color = mag > 0 ? 'var(--accent)' : 'var(--bad)';
    });
  }

  /* ================================================================ */
  /*  FIRST-PERSON WEAPON                                             */
  /* ================================================================ */

  buildFPWeapon() {
    this.fpGroup = new THREE.Group();
    this.fpGroup.position.set(0.24, -0.22, -0.42);
    this.camera.add(this.fpGroup);
    this.fpModel = null;
    this.fpBase = this.fpGroup.position.clone();
    this.setFPWeapon();
  }

  setFPWeapon() {
    if (this.fpModel) {
      this.fpGroup.remove(this.fpModel);
      this.fpModel = null;
    }
    const isShoot = this.tool === 'shoot';
    const def = WEAPON_BY_ID[this.weaponId];
    const showGun = isShoot && def.kind === 'gun';
    const showBlade = this.tool === 'cut';
    if (!showGun && !showBlade) return;
    const model = makeWeaponMesh(def);
    // add a simple hand so it does not float
    const hand = new THREE.Mesh(
      new THREE.SphereGeometry(0.05, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xc78e6a, roughness: 0.7 })
    );
    hand.position.set(0, -0.01, -0.02);
    const arm = new THREE.Mesh(
      new THREE.CylinderGeometry(0.045, 0.05, 0.5, 8),
      new THREE.MeshStandardMaterial({ color: 0x3d4a5c, roughness: 0.9 })
    );
    arm.rotation.x = Math.PI / 2;
    arm.position.set(0, -0.03, -0.28);
    model.add(hand, arm);
    model.traverse((o) => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = false; } });
    model.rotation.y = Math.PI;
    model.scale.setScalar(showGun ? 0.9 : 0.85);
    this.fpGroup.add(model);
    this.fpModel = model;
    this.fpDef = def;
  }

  fpMuzzleWorld() {
    if (!this.fpModel) return this.camera.position.clone().add(new THREE.Vector3(0, 0, -1));
    const def = this.fpDef || WEAPON_BY_ID[this.weaponId];
    const len = def.kind === 'gun' ? def.mz * 0.9 : def.mz * 0.85;
    const v = new THREE.Vector3(0, 0, len);
    this.fpModel.localToWorld(v);
    return v;
  }

  /* ================================================================ */
  /*  INPUT                                                           */
  /* ================================================================ */

  bindInput() {
    const c = this.canvas;
    window.addEventListener('resize', () => this.onResize());
    c.addEventListener('contextmenu', (e) => e.preventDefault());

    c.addEventListener('pointerdown', (e) => {
      if (!this.started) return;
      if (c.setPointerCapture) c.setPointerCapture(e.pointerId);
      this.updateMouse(e);
      if (e.button === 2) { this.dragMode = 'orbit'; this.lastMouse = { x: e.clientX, y: e.clientY }; return; }
      if (e.button === 1) { this.dragMode = 'pan'; this.lastMouse = { x: e.clientX, y: e.clientY }; return; }
      if (e.button === 0) {
        this.mouse.down = true;
        this.onPrimaryDown();
      }
    });

    c.addEventListener('pointermove', (e) => {
      this.updateMouse(e);
      if (this.dragMode === 'orbit') {
        const dx = e.clientX - this.lastMouse.x;
        const dy = e.clientY - this.lastMouse.y;
        this.cam.yaw -= dx * 0.005;
        this.cam.pitch = clamp(this.cam.pitch + dy * 0.004, -0.12, 1.35);
        this.lastMouse = { x: e.clientX, y: e.clientY };
      } else if (this.dragMode === 'pan') {
        const dx = e.clientX - this.lastMouse.x;
        const dy = e.clientY - this.lastMouse.y;
        this.panCamera(-dx * 0.012, -dy * 0.012);
        this.lastMouse = { x: e.clientX, y: e.clientY };
      }
      if (this.mouse.down && this.tool === 'cut') this.doCut();
      if (this.mouse.down && this.tool === 'grab') this.doGrabDrag();
    });

    c.addEventListener('pointerup', (e) => {
      if (e.button === 0) {
        this.mouse.down = false;
        if (this.grab) { if (this.grab.point) this.grab.point.held = false; this.grab = null; }
      }
      this.dragMode = null;
    });
    c.addEventListener('pointerleave', () => { this.mouse.inside = false; });

    c.addEventListener('wheel', (e) => {
      e.preventDefault();
      this.cam.dist = clamp(this.cam.dist * Math.exp(e.deltaY * 0.0011), 2.2, 26);
    }, { passive: false });

    window.addEventListener('keydown', (e) => {
      if (this.keys.has(e.code)) return;
      this.keys.add(e.code);
      if (!this.started) { if (e.code === 'Enter' || e.code === 'Space') this.begin(); return; }
      const k = e.code;
      if (k === 'KeyP') { this.paused = !this.paused; this.updateSettingsUI(); }
      if (k === 'KeyM') this.onSetting('sound');
      if (k === 'KeyH') this.toggleHelp(document.getElementById('help').classList.contains('hidden'));
      if (k === 'KeyG') this.onSetting('blood');
      if (k === 'KeyF') this.spawnHuman(this.cam.target.x + rand(-2, 2), this.cam.target.z + rand(-2, 2), { armed: true });
      if (k === 'Space') { e.preventDefault(); this.onSetting('slow'); }
      if (k === 'Escape') this.pinned = null;
      if (k === 'Backspace') this.clearCorpses();
      const t = TOOLS.find((x) => 'Key' + x.key === k);
      if (t) this.setTool(t.id);
      const num = /^Digit([1-9])$/.exec(k);
      if (num) this.selectWeapon(WEAPONS[parseInt(num[1], 10) - 1]);
    });
    window.addEventListener('keyup', (e) => this.keys.delete(e.code));
    window.addEventListener('blur', () => { this.keys.clear(); this.mouse.down = false; this.dragMode = null; });
  }

  updateMouse(e) {
    this.mouse.x = e.clientX;
    this.mouse.y = e.clientY;
    this.mouse.ndcX = (e.clientX / window.innerWidth) * 2 - 1;
    this.mouse.ndcY = -(e.clientY / window.innerHeight) * 2 + 1;
    this.mouse.inside = true;
    const r = document.getElementById('reticle');
    r.style.left = e.clientX + 'px';
    r.style.top = e.clientY + 'px';
  }

  panCamera(dx, dz) {
    const s = Math.sin(this.cam.yaw), c = Math.cos(this.cam.yaw);
    const b = this.world.bounds || { minX: -24, maxX: 24, minZ: -24, maxZ: 24 };
    this.cam.target.x = clamp(this.cam.target.x + dx * c + dz * s, b.minX + 1, b.maxX - 1);
    this.cam.target.z = clamp(this.cam.target.z - dx * s + dz * c, b.minZ + 1, b.maxZ - 1);
  }

  updateCamera(dt = 0) {
    const k = this.keys;
    const speed = (k.has('ShiftLeft') ? 9 : 4.2) * dt;
    if (dt > 0 && this.started) {
      let mx = 0, mz = 0;
      if (k.has('ArrowLeft')) mx -= 1;
      if (k.has('ArrowRight')) mx += 1;
      if (k.has('ArrowUp')) mz += 1;
      if (k.has('ArrowDown')) mz -= 1;
      if (mx || mz) this.panCamera(mx * speed, mz * speed);
    }
    const { target, yaw, pitch, dist } = this.cam;
    const cp = Math.cos(pitch), sp = Math.sin(pitch);
    const shake = this.shakeAmt || 0;
    this.camera.position.set(
      target.x + Math.sin(yaw) * dist * cp + (Math.random() - 0.5) * shake,
      target.y + sp * dist + (Math.random() - 0.5) * shake,
      target.z + Math.cos(yaw) * dist * cp + (Math.random() - 0.5) * shake
    );
    this.camera.lookAt(target);
    this.recoil *= 0.86;
    this.shakeAmt = (this.shakeAmt || 0) * 0.86;
  }

  setRayFromMouse() {
    this.raycaster.setFromCamera(new THREE.Vector2(this.mouse.ndcX, this.mouse.ndcY), this.camera);
    return this.raycaster.ray;
  }

  pickHuman(maxDist = 60) {
    const ray = this.setRayFromMouse();
    let best = null;
    for (const h of this.humans) {
      for (const l of h.limbs) {
        if (l.severed || l.parentSevered) continue;
        const a = h.pts[l.a], b = h.pts[l.b];
        const r = raySegment({ x: ray.origin.x, y: ray.origin.y, z: ray.origin.z }, { x: ray.direction.x, y: ray.direction.y, z: ray.direction.z }, a, b);
        if (r.dist <= l.r + 0.03 && r.rayT <= maxDist) {
          if (!best || r.rayT < best.t) {
            best = {
              human: h, limb: l.name, t: r.rayT,
              point: { x: ray.origin.x + ray.direction.x * r.rayT, y: ray.origin.y + ray.direction.y * r.rayT, z: ray.origin.z + ray.direction.z * r.rayT },
              segT: r.segT,
            };
          }
        }
      }
    }
    return best;
  }

  groundPoint() {
    const ray = this.setRayFromMouse();
    if (Math.abs(ray.direction.y) < 1e-4) return null;
    const t = -ray.origin.y / ray.direction.y;
    if (t < 0 || t > 200) return null;
    return { x: ray.origin.x + ray.direction.x * t, y: 0, z: ray.origin.z + ray.direction.z * t };
  }

  /* ================================================================ */
  /*  TOOLS                                                           */
  /* ================================================================ */

  onPrimaryDown() {
    const t = this.tool;
    if (t === 'cut') { this.doCut(true); return; }
    if (t === 'shoot') { this.tryFire(); return; }
    if (t === 'inspect') {
      const hit = this.pickHuman();
      this.pinned = hit ? (this.pinned === hit.human ? null : hit.human) : null;
      return;
    }
    if (t === 'spawn') {
      const p = this.groundPoint();
      if (p) {
        this.spawnHuman(p.x, p.z, { armed: this.keys.has('ShiftLeft') || this.keys.has('ShiftRight') });
        this.audio.step();
      }
      return;
    }
    if (t === 'grab') {
      const hit = this.pickHuman();
      if (!hit) return;
      const h = hit.human;
      if (!h.ragdoll) h.ragdollize(null);
      let bestPt = null, bestD = Infinity;
      for (const k in h.pts) {
        const p = h.pts[k];
        const d = dist(p, hit.point);
        if (d < bestD) { bestD = d; bestPt = p; }
      }
      h.wake();
      if (bestPt) bestPt.held = true;
      this.grab = { human: h, point: bestPt, depth: hit.t };
      return;
    }
    if (t === 'heal') {
      const hit = this.pickHuman();
      if (hit) this.heal(hit.human);
      return;
    }
    if (t === 'delete') {
      const hit = this.pickHuman();
      if (hit) this.removeHuman(hit.human);
    }
  }

  doGrabDrag() {
    if (!this.grab || !this.grab.point) return;
    const ray = this.setRayFromMouse();
    const d = this.grab.depth;
    const p = this.grab.point;
    const tp = {
      x: ray.origin.x + ray.direction.x * d,
      y: Math.max(0.12, ray.origin.y + ray.direction.y * d),
      z: ray.origin.z + ray.direction.z * d,
    };
    p.x += (tp.x - p.x) * 0.55;
    p.y += (tp.y - p.y) * 0.55;
    p.z += (tp.z - p.z) * 0.55;
    this.grab.human.wake();
  }

  doCut(force = false) {
    const hit = this.pickHuman(30);
    if (!hit) return;
    const now = this.time;
    const key = hit.human.id + ':' + hit.limb;
    const last = this.cutCd.get(key) || -99;
    if (!force && now - last < 0.085) return;
    this.cutCd.set(key, now);
    const def = WEAPON_BY_ID[this.weaponId].kind === 'melee' ? WEAPON_BY_ID[this.weaponId] : WEAPON_BY_ID.knife;
    const ray = this.raycaster.ray;
    const dir = { x: ray.direction.x, y: ray.direction.y, z: ray.direction.z };
    hit.human.applyHit(hit.limb, {
      type: 'slash',
      dmg: def.dmg * 0.22,
      struct: def.structDmg * 0.3,
      cut: def.cut,
      bleedMul: def.bleedMul,
      knock: 14,
      point: hit.point,
      dir,
    });
    this.world.spray(hit.point, dir, { count: 14, speed: 2.6, size: 0.028, spread: 0.9 });
    this.audio.cut();
    this.swingFP();
  }

  swingFP() {
    this.fpSwing = 1;
  }

  /* ---------------- shooting ---------------- */

  tryFire() {
    if (this.fireCd > 0 || this.reloadT > 0) return;
    const def = WEAPON_BY_ID[this.weaponId];
    if (def.kind !== 'gun') return;
    const mag = this.ammo[def.id] == null ? def.magSize : this.ammo[def.id];
    if (mag <= 0) {
      this.reloadT = def.reloadTime;
      this.audio.empty();
      return;
    }
    this.ammo[def.id] = mag - 1;
    this.fireCd = 60 / def.rpm;
    const muzzle = this.fpMuzzleWorld();
    const ray = this.setRayFromMouse();
    const origin = { x: this.camera.position.x, y: this.camera.position.y, z: this.camera.position.z };
    const baseDir = { x: ray.direction.x, y: ray.direction.y, z: ray.direction.z };
    for (let i = 0; i < def.pellets; i++) {
      const dir = this.spreadDir(baseDir, def.spread + (def.pellets > 1 ? 0 : 0.004));
      this.castBullet(origin, dir, def, muzzle);
    }
    this.world.muzzleFlash({ x: muzzle.x, y: muzzle.y, z: muzzle.z }, def.pellets > 1 ? 1.7 : 1);
    this.audio.shot(def.sound);
    this.recoil = def.shake;
    this.shakeAmt = def.shake * 0.12;
    this.mouseKick = def.shake;
    if (this.ammo[def.id] <= 0) this.reloadT = def.reloadTime;
    this.updateAmmoUI();
  }

  spreadDir(dir, spread) {
    const d = norm(dir);
    const up = Math.abs(d.y) < 0.9 ? { x: 0, y: 1, z: 0 } : { x: 1, y: 0, z: 0 };
    const t1 = norm({ x: d.y * up.z - d.z * up.y, y: d.z * up.x - d.x * up.z, z: d.x * up.y - d.y * up.x });
    const t2 = { x: d.y * t1.z - d.z * t1.y, y: d.z * t1.x - d.x * t1.z, z: d.x * t1.y - d.y * t1.x };
    const a = Math.random() * TAU;
    const r = Math.sqrt(Math.random()) * spread;
    return norm({
      x: d.x + t1.x * Math.cos(a) * r + t2.x * Math.sin(a) * r,
      y: d.y + t1.y * Math.cos(a) * r + t2.y * Math.sin(a) * r,
      z: d.z + t1.z * Math.cos(a) * r + t2.z * Math.sin(a) * r,
    });
  }

  // Returns the first thing the ray hits: human limb or the ground.
  traceShot(origin, dir, ignore) {
    let best = null;
    for (const h of this.humans) {
      if (h === ignore) continue;
      for (const l of h.limbs) {
        if (l.severed || l.parentSevered) continue;
        const a = h.pts[l.a], b = h.pts[l.b];
        const r = raySegment(origin, dir, a, b);
        if (r.dist <= l.r + 0.008) {
          if (!best || r.rayT < best.t) {
            best = {
              t: r.rayT, human: h, limb: l.name,
              point: { x: origin.x + dir.x * r.rayT, y: origin.y + dir.y * r.rayT, z: origin.z + dir.z * r.rayT },
            };
          }
        }
      }
    }
    let groundT = Infinity;
    if (dir.y < -1e-4) groundT = -origin.y / dir.y;
    if (groundT > 0 && groundT < (best ? best.t : Infinity)) {
      return { t: groundT, ground: true, point: { x: origin.x + dir.x * groundT, y: 0.002, z: origin.z + dir.z * groundT } };
    }
    return best;
  }

  castBullet(origin, dir, def, muzzle) {
    const hit = this.traceShot(origin, dir, null);
    const end = hit ? hit.point : {
      x: origin.x + dir.x * 90, y: origin.y + dir.y * 90, z: origin.z + dir.z * 90,
    };
    const tracerStart = muzzle || origin;
    this.world.tracer(tracerStart, end, { color: def.pellets > 1 ? 0xffc98a : 0xfff0c8, radius: def.pellets > 1 ? 0.008 : 0.012 });

    if (hit && hit.human) {
      const h = hit.human;
      h.applyHit(hit.limb, {
        type: 'bullet',
        dmg: def.dmg * (def.pellets > 1 ? rand(0.7, 1.2) : 1),
        struct: def.structMul * (def.pellets > 1 ? rand(0.7, 1.3) : 1),
        cut: 0.55,
        bleedMul: def.bleedMul,
        knock: def.dmg * 2,
        point: hit.point,
        dir,
      });
      this.world.mist(hit.point, dir, 6, 0.6);
      this.world.spray(hit.point, dir, { count: 6, speed: 2.4, size: 0.03, spread: 0.8 });
      this.audio.flesh();
    } else if (hit && hit.ground) {
      this.world.dust(hit.point, 6, 0x9a958c);
      this.world.spark(hit.point, { x: -dir.x, y: -dir.y, z: -dir.z }, 4);
      this.world.decal(hit.point.x, hit.point.z, rand(0.05, 0.12), 0.5);
      this.audio.impact();
    }
  }

  // used by NPCs
  fireBullet(origin, dir, def, shooter) {
    const base = norm(dir);
    for (let i = 0; i < def.pellets; i++) {
      const d = this.spreadDir(base, def.spread + 0.02);
      const hit = this.traceShot(origin, d, shooter);
      const end = hit ? hit.point : { x: origin.x + d.x * 90, y: origin.y + d.y * 90, z: origin.z + d.z * 90 };
      this.world.tracer(origin, end, { color: 0xffd0a0, radius: 0.01 });
      if (hit && hit.human) {
        hit.human.applyHit(hit.limb, {
          type: 'bullet',
          dmg: def.dmg * 0.8 * (def.pellets > 1 ? rand(0.7, 1.2) : 1),
          struct: def.structMul * 0.8 * (def.pellets > 1 ? rand(0.7, 1.3) : 1),
          cut: 0.55, bleedMul: def.bleedMul, knock: def.dmg * 1.6,
          point: hit.point, dir: d, attacker: shooter,
        });
        this.world.mist(hit.point, d, 5, 0.6);
        this.audio.flesh();
      } else if (hit && hit.ground) {
        this.world.dust(hit.point, 5, 0x9a958c);
        this.world.decal(hit.point.x, hit.point.z, rand(0.04, 0.1), 0.45);
      }
    }
    this.world.muzzleFlash(origin, def.pellets > 1 ? 1.5 : 1);
    this.audio.shot(def.sound);
  }

  dropWeapon(def, pos, dir) {
    const mesh = makeWeaponMesh(def);
    mesh.position.set(pos.x, pos.y, pos.z);
    this.scene.add(mesh);
    this.debris.push({
      kind: 'weapon', mesh,
      pos: { x: pos.x, y: pos.y, z: pos.z },
      vel: { x: dir.x * 1.5 + rand(-0.5, 0.5), y: rand(1.5, 3), z: dir.z * 1.5 + rand(-0.5, 0.5) },
      rot: { x: rand(0, TAU), y: rand(0, TAU), z: rand(0, TAU) },
      spin: { x: rand(-6, 6), y: rand(-6, 6), z: rand(-6, 6) },
      life: 120,
    });
  }

  heal(h) {
    if (!h.alive) { this.addLog(`${h.name}: мёртв — лечение не помогает`, 'bad'); return; }
    h.health = Math.min(100, h.health + 80);
    h.blood = Math.min(5000, h.blood + 2600);
    h.unconscious = false;
    h.pain = 0;
    for (const l of h.limbs) {
      l.struct = Math.max(0, l.struct - 0.7);
      l.broken = false;
      l.arterial = false;
      l.wounds.length = 0;
      l.stumpBleed = 0;
    }
    this.addLog(`${h.name}: обработан`, 'good');
    this.audio.reload();
  }

  removeHuman(h) {
    h.dispose();
    const i = this.humans.indexOf(h);
    if (i >= 0) this.humans.splice(i, 1);
    if (this.pinned === h) this.pinned = null;
    if (this.grab && this.grab.human === h) this.grab = null;
  }

  clearCorpses() {
    for (const h of [...this.humans]) if (!h.alive) this.removeHuman(h);
    this.addLog('Трупы убраны');
  }

  /* ================================================================ */
  /*  SPAWN / WORLD                                                   */
  /* ================================================================ */

  spawnInitial() {
    const pts = this.world.spawnPoints || [{ x: -1.6, z: 0.6 }, { x: 1.8, z: -0.4 }];
    const gear = ['knife', 'machete', 'fists', 'pistol'];
    for (let i = 0; i < Math.min(4, pts.length); i++) {
      this.spawnHuman(pts[i].x, pts[i].z, { weapon: gear[i], ai: false });
    }
  }

  spawnHuman(x, z, opts = {}) {
    if (this.humans.length >= 26) {
      const dead = this.humans.find((h) => !h.alive);
      if (dead) this.removeHuman(dead);
      else return null;
    }
    const armed = opts.weapon || (opts.armed ? pick(['pistol', 'smg', 'shotgun', 'rifle', 'knife', 'machete', 'katana', 'axe']) : pick(['fists', 'fists', 'knife', 'machete']));
    // never drop someone inside a wall
    let sx = x, sz = z;
    if (this.world.blocked && this.world.blocked(sx, sz, 0.45)) {
      const free = (this.world.spawnPoints || []).find((p) => !this.world.blocked(p.x, p.z, 0.45));
      if (free) { sx = free.x; sz = free.z; }
      else { sx += rand(-1.2, 1.2); sz += rand(-1.2, 1.2); }
    }
    const h = new Human(this, {
      x: clamp(sx, -32, 32),
      z: clamp(sz, -32, 32),
      weapon: armed,
      ai: opts.ai != null ? opts.ai : this.aiAggro,
      name: opts.name || pick(NAMES_LIST),
    });
    h.ai.enabled = this.aiAggro;
    this.humans.push(h);
    this.addLog(`${h.name}: появился`, '');
    return h;
  }

  makeHoverMarker() {
    const geo = new THREE.RingGeometry(0.34, 0.42, 28);
    geo.rotateX(-Math.PI / 2);
    const mat = new THREE.MeshBasicMaterial({ color: 0x8fd0ff, transparent: true, opacity: 0.7, depthWrite: false });
    const m = new THREE.Mesh(geo, mat);
    m.visible = false;
    this.scene.add(m);
    return m;
  }

  onDeath(h) {
    void h;
  }

  /* ================================================================ */
  /*  LOOP                                                            */
  /* ================================================================ */

  onResize() {
    this.camera.aspect = window.innerWidth / window.innerHeight;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(window.innerWidth, window.innerHeight);
  }

  loop(now) {
    let dtReal = (now - this.last) / 1000;
    this.last = now;
    if (!isFinite(dtReal)) dtReal = 1 / 60;
    dtReal = Math.min(dtReal, 0.05);
    this.time += dtReal;

    this.updateCamera(this.started ? dtReal : 0);

    if (this.started && !this.paused) {
      const dt = dtReal * this.timeScale;
      this.update(dt, dtReal);
    }
    this.renderer.render(this.scene, this.camera);

    requestAnimationFrame(this.loop);
  }

  update(dt, dtReal) {
    // log fade
    for (let i = this.log.length - 1; i >= 0; i--) {
      this.log[i].t -= dtReal;
      if (this.log[i].t <= 0) this.log.splice(i, 1);
    }
    if (this.logT == null) this.logT = 0;
    this.logT += dtReal;
    if (this.logT > 0.4) { this.logT = 0; this.renderLog(); }

    if (this.fireCd > 0) this.fireCd -= dt;
    if (this.reloadT > 0) {
      this.reloadT -= dt;
      if (this.reloadT <= 0) {
        const def = WEAPON_BY_ID[this.weaponId];
        this.ammo[def.id] = def.magSize;
        this.audio.reload();
      }
      this.updateAmmoUI();
    }
    // automatic fire
    const def = WEAPON_BY_ID[this.weaponId];
    if (def.auto && this.mouse.down && this.tool === 'shoot') this.tryFire();

    this.fpSwing = Math.max(0, (this.fpSwing || 0) - dt * 5.5);
    if (this.fpModel) {
      const sway = Math.sin(this.time * 2.1) * 0.004;
      const swing = this.fpSwing * this.fpSwing;
      this.fpGroup.position.set(
        this.fpBase.x + sway,
        this.fpBase.y + Math.cos(this.time * 1.7) * 0.003,
        this.fpBase.z + this.recoil * 0.12
      );
      this.fpGroup.rotation.set(
        this.recoil * 0.25 - swing * 0.5,
        -0.05 + swing * 0.4,
        -swing * 0.6
      );
    }

    for (const h of this.humans) h.update(dt, this);
    this.resolveBodyCollisions();
    for (let i = this.debris.length - 1; i >= 0; i--) this.updateDebris(this.debris[i], dt, i);
    this.world.update(dt);
    this.world.updateShadowFocus(this.cam.target);

    // hover highlight
    const hit = this.pickHuman();
    this.hovered = hit ? hit.human : null;
    if (this.hovered) {
      const p = this.hovered.ragdoll ? this.hovered.pts.chest : this.hovered.pts.pelvis;
      this.hoverMarker.position.set(p.x, 0.02, p.z);
      this.hoverMarker.visible = true;
      this.hoverMarker.material.color.set(this.pinned === this.hovered ? 0x8fd0ff : 0xdfe7f2);
      this.hoverMarker.material.opacity = this.pinned === this.hovered ? 0.95 : 0.4;
    } else {
      this.hoverMarker.visible = false;
    }

    this.ammoT = (this.ammoT || 0) + dtReal;
    if (this.ammoT > 0.15) { this.ammoT = 0; this.updateAmmoUI(); }
    this.inspectorTimer = (this.inspectorTimer || 0) + dtReal;
    if (this.inspectorTimer > 0.12) {
      this.inspectorTimer = 0;
      this.renderInspector();
    }
  }

  resolveBodyCollisions() {
    for (let i = 0; i < this.humans.length; i++) {
      for (let j = i + 1; j < this.humans.length; j++) {
        const a = this.humans[i], b = this.humans[j];
        if (!a.ragdoll && !b.ragdoll) {
          const dx = b.x - a.x, dz = b.z - a.z;
          const d = Math.hypot(dx, dz);
          if (d > 0.001 && d < 0.7) {
            const push = (0.7 - d) * 0.5;
            a.x -= (dx / d) * push; a.z -= (dz / d) * push;
            b.x += (dx / d) * push; b.z += (dz / d) * push;
          }
        } else {
          const pa = a.ragdoll ? a.pts.chest : a.pts.pelvis;
          const pb = b.ragdoll ? b.pts.chest : b.pts.pelvis;
          const dx = pb.x - pa.x, dy = pb.y - pa.y, dz = pb.z - pa.z;
          const d = Math.hypot(dx, dy, dz);
          const min = 0.34;
          if (d > 0.001 && d < min) {
            const k = (min - d) / d * 0.5;
            if (a.ragdoll && !a.pts.chest.held) { a.pts.chest.x -= dx * k; a.pts.chest.y -= dy * k; a.pts.chest.z -= dz * k; a.wake(); }
            if (b.ragdoll && !b.pts.chest.held) { b.pts.chest.x += dx * k; b.pts.chest.y += dy * k; b.pts.chest.z += dz * k; b.wake(); }
          }
        }
      }
    }
  }

  updateDebris(d, dt, index) {
    d.life -= dt;
    if (d.life <= 0) {
      if (d.mesh) { this.scene.remove(d.mesh); d.mesh.traverse?.((o) => { if (o.geometry) o.geometry.dispose(); }); }
      this.debris.splice(index, 1);
      return;
    }
    if (d.kind === 'weapon') {
      d.vel.y -= 9.81 * dt;
      d.pos.x += d.vel.x * dt; d.pos.y += d.vel.y * dt; d.pos.z += d.vel.z * dt;
      d.rot.x += d.spin.x * dt; d.rot.y += d.spin.y * dt; d.rot.z += d.spin.z * dt;
      if (d.pos.y < 0.03) {
        d.pos.y = 0.03;
        d.vel.y *= -0.25; d.vel.x *= 0.7; d.vel.z *= 0.7; d.spin.x *= 0.5; d.spin.y *= 0.5; d.spin.z *= 0.5;
        if (Math.abs(d.vel.y) < 0.2) { d.vel.y = 0; d.settled = true; }
      }
      d.mesh.position.set(d.pos.x, d.pos.y, d.pos.z);
      d.mesh.rotation.set(d.rot.x, d.rot.y, d.rot.z);
      return;
    }

    // limb debris: 2-point PBD body
    const g = -9.81;
    const a = d.a, b = d.b;
    d.va.y += g * dt; d.vb.y += g * dt;
    a.x += d.va.x * dt; a.y += d.va.y * dt; a.z += d.va.z * dt;
    b.x += d.vb.x * dt; b.y += d.vb.y * dt; b.z += d.vb.z * dt;
    for (let it = 0; it < 4; it++) {
      const dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
      const len = Math.hypot(dx, dy, dz) || 1e-6;
      const corr = ((len - d.rest) / len) * 0.5;
      a.x += dx * corr; a.y += dy * corr; a.z += dz * corr;
      b.x -= dx * corr; b.y -= dy * corr; b.z -= dz * corr;
      for (const p of [a, b]) {
        if (p.y < d.r) {
          p.y = d.r;
          if (p === a) { d.va.y *= -0.15; d.va.x *= 0.72; d.va.z *= 0.72; }
          else { d.vb.y *= -0.15; d.vb.x *= 0.72; d.vb.z *= 0.72; }
        }
      }
    }
    if (d.kind === 'head') {
      d.mesh.position.set(a.x, a.y, a.z);
      d.mesh.rotation.y += dt * 2;
    } else {
      orientCylinder(d.mesh, a, b, d.r);
    }
    // bleeding stump
    d.bleed = Math.max(0, d.bleed - dt * 1.2);
    if (d.bleed > 0.5 && chance(dt * 8)) {
      this.world.spray(a, { x: rand(-0.3, 0.3), y: 1, z: rand(-0.3, 0.3) }, { count: 1, speed: 1.2, size: 0.03, spread: 0.4 });
    }
  }

  /* ================================================================ */
  /*  INSPECTOR                                                       */
  /* ================================================================ */

  renderInspector() {
    const el = document.getElementById('inspector');
    const h = this.pinned || this.hovered;
    if (!h) { el.classList.add('hidden'); return; }
    el.classList.remove('hidden');
    const info = h.info();
    const status = !info.alive ? ['МЁРТВ', 'dead'] : info.unconscious ? ['БЕЗ СОЗНАНИЯ', 'uncon'] : ['ЖИВ', 'alive'];
    const bar = (label, value, max, cls, right) => `
      <div class="bar-row">
        <span class="lbl">${label}</span>
        <div class="bar ${cls}"><i style="width:${Math.round(clamp(value / max, 0, 1) * 100)}%"></i></div>
        <span class="val">${right}</span>
      </div>`;
    const limbs = [...info.limbs].sort((a, b) => {
      if (a.severed !== b.severed) return a.severed ? 1 : -1;
      return a.name.localeCompare(b.name, 'ru');
    });
    const limbRow = (l) => {
      const integrity = l.severed ? 0 : clamp(1 - l.struct, 0, 1);
      let cls = 'ok', txt = 'цела';
      if (l.parentSevered) { cls = 'gone'; txt = 'отделена'; }
      else if (l.severed) { cls = 'severed'; txt = 'оторвана'; }
      else if (l.broken) { cls = 'broken'; txt = 'перелом'; }
      else if (l.wounds) { cls = 'wound'; txt = `рана ×${l.wounds}`; }
      else if (l.struct > 0.3) { cls = 'broken'; txt = 'ушиб'; }
      const col = integrity > 0.6 ? '#6f9f6f' : integrity > 0.3 ? '#c9a24a' : '#c45555';
      return `<div class="limb">
        <span class="nm">${l.name}${l.arterial ? ' <span style="color:#e06767">●</span>' : ''}</span>
        <span class="integ"><i style="width:${Math.round(integrity * 100)}%;background:${l.severed ? '#5a2f2f' : col}"></i></span>
        <span class="st ${cls}">${txt}</span>
      </div>`;
    };
    const totalWounds = info.limbs.reduce((s, l) => s + l.wounds, 0);
    el.innerHTML = `
      <h2>${info.name} <span class="status ${status[1]}">${status[0]}</span></h2>
      ${!info.alive ? `<div class="cause">Причина: ${info.deathCause || 'неизвестна'}</div>` : ''}
      ${bar('Здоровье', info.health, 100, '', info.health < 18 ? 'КРИТИЧНО' : Math.round(info.health))}
      ${bar('Кровь', info.blood, 5000, 'blood', (info.blood / 1000).toFixed(2) + ' л')}
      ${bar('Боль', clamp(info.pain, 0, 1.5), 1.5, 'pain', Math.round(clamp(info.pain, 0, 1.5) / 1.5 * 100) + '%')}
      <div class="kv"><span>Кровопотеря</span><span style="color:${info.bleed > 1 ? '#e08b7b' : '#c9d4e3'}">${info.bleed.toFixed(1)} мл/с</span></div>
      <div class="kv"><span>Оружие</span><span>${info.weapon.name}${info.weapon.kind === 'gun' ? ` · ${info.mag}/${info.weapon.magSize}` : ''}</span></div>
      <div class="kv"><span>Состояние</span><span>${info.ragdoll ? 'лежит' : 'на ногах'}</span></div>
      <div class="sect"><span>КОНЕЧНОСТИ</span><span>ткани</span></div>
      ${limbs.map(limbRow).join('')}
      <div class="kv" style="margin-top:8px"><span>Ран всего</span><span>${totalWounds}</span></div>
      <div class="kv"><span>ИИ</span><span>${!info.ai ? 'выключен (мирный)' : info.target ? 'атакует ' + info.target : 'выбирает цель'}</span></div>
      <div class="pin-note">${this.pinned === h ? 'Панель закреплена · Esc — снять' : 'Клик — закрепить панель'}</div>
    `;
  }
}

/* segment helper for debris mesh */
function orientCylinder(mesh, a, b, r) {
  const dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
  const len = Math.hypot(dx, dy, dz) || 0.01;
  mesh.position.set((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
  mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), new THREE.Vector3(dx / len, dy / len, dz / len));
  mesh.scale.set(r, len, r);
}

window.addEventListener('DOMContentLoaded', () => {
  window.__app = new App();
});
