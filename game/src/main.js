import {
  VIEW_W, VIEW_H, GROUND_Y, RENDER_SCALE, TAU, clamp, rand, randInt, chance, pick,
  dist, hash01, roundRect, shade, segSegClosest,
} from './util.js';
import { AudioSys } from './audio.js';
import { Gore } from './gore.js';
import { Combat, WEAPONS, WEAPON_BY_ID, GUN_IDS, drawWeapon } from './weapons.js';
import { Person } from './player.js';

const W = VIEW_W;
const H = VIEW_H;
const FONT = '"Segoe UI", system-ui, -apple-system, sans-serif';

const TOOLS = [
  { id: 'cut', key: 'Q', name: 'Резать', hint: 'Зажми ЛКМ и проведи по конечности — клинок режет там, где курсор' },
  { id: 'shoot', key: 'W', name: 'Стрелять', hint: 'ЛКМ — огонь. Стрелок за кадром, пули летят из-за края точно в прицел' },
  { id: 'inspect', key: 'E', name: 'Осмотр', hint: 'Клик по человеку — закрепить панель данных. Наведение показывает данные' },
  { id: 'spawn', key: 'R', name: 'Спавн', hint: 'Клик по земле — новый человек. Shift+клик — сразу с оружием' },
  { id: 'grab', key: 'T', name: 'Тянуть', hint: 'Зажми человека и тащи — тело станет рэгдоллом и потянется за курсором' },
  { id: 'heal', key: 'Y', name: 'Лечить', hint: 'Клик — остановить кровь, залечить раны и вывести из шока' },
  { id: 'delete', key: 'U', name: 'Удалить', hint: 'Клик по человеку — убрать его из песочницы' },
];

const LIMB_STATUS = (l) => {
  if (l.parentSevered) return { text: 'отделена', col: '#7c4a4a' };
  if (l.severed) return { text: 'ОТОРВАНА', col: '#e05555' };
  if (l.broken) return { text: 'перелом', col: '#e0a055' };
  if (l.wounds.length) return { text: `рана ×${l.wounds.length}`, col: '#e08b7b' };
  if (l.struct > 0.35) return { text: 'ушиб', col: '#c9b98a' };
  return { text: 'цела', col: '#7fae7f' };
};

class Game {
  constructor() {
    this.canvas = document.getElementById('game');
    this.ctx = this.canvas.getContext('2d');
    this.canvas.width = W * RENDER_SCALE;
    this.canvas.height = H * RENDER_SCALE;

    this.audio = new AudioSys();
    this.gore = new Gore(this.audio);
    this.combat = new Combat(this);
    this.input = new Input(this.canvas);

    this.humans = [];
    this.debris = [];
    this.log = [];
    this.time = 0;
    this.realTime = 0;
    this.camX = W / 2;
    this.paused = false;
    this.started = false;
    this.showHelp = false;
    this.muted = false;
    this.timeScale = 1;
    this.shakeAmt = 0;
    this.hitstopT = 0;

    this.tool = 'cut';
    this.weaponId = 'pistol';
    this.ammo = {};
    this.reloadT = 0;
    this.fireCd = 0;
    this.cutPrev = null;
    this.cutCd = new Map();
    this.grab = null;
    this.hovered = null;
    this.pinned = null;
    this.inspected = null;
    this.aiAggro = false;

    this.cursor = { x: W * 0.55, y: H * 0.55, down: false, justDown: false, justUp: false, rdown: false, inside: false };
    this.aimAngle = 0;

    this.hudRects = [];

    this.buildBackground();
    this.resize();
    window.addEventListener('resize', () => this.resize());

    this.spawnHuman(230, { weapon: 'knife', ai: false });
    this.spawnHuman(360, { weapon: 'machete', ai: false });
    this.spawnHuman(470, { weapon: 'fists', ai: false });

    this.last = performance.now();
    requestAnimationFrame(this.loop.bind(this));
  }

  /* ================= setup ================= */

  resize() {
    const pad = 28;
    const aw = window.innerWidth - pad;
    const ah = window.innerHeight - pad;
    const scale = Math.max(0.5, Math.min(aw / W, ah / H));
    this.canvas.style.width = Math.floor(W * scale) + 'px';
    this.canvas.style.height = Math.floor(H * scale) + 'px';
  }

  spawnHuman(x, opts = {}) {
    const armed = opts.weapon || (opts.forceArmed ? pick(GUN_IDS.concat(['knife', 'machete', 'katana', 'axe'])) : (chance(0.45) ? pick(GUN_IDS) : pick(['fists', 'knife', 'machete'])));
    const p = new Person(this, {
      x: clamp(x, 24, W - 24),
      weapon: armed,
      ai: opts.ai != null ? opts.ai : this.aiAggro,
      skin: opts.skin,
    });
    this.humans.push(p);
    if (this.humans.length > 40) {
      const dead = this.humans.find((h) => !h.alive);
      if (dead) this.humans.splice(this.humans.indexOf(dead), 1);
    }
    return p;
  }

  removeHuman(p) {
    const i = this.humans.indexOf(p);
    if (i >= 0) this.humans.splice(i, 1);
    if (this.pinned === p) this.pinned = null;
    if (this.grab && this.grab.human === p) this.grab = null;
    if (this.inspected === p) this.inspected = null;
  }

  /* ================= input ================= */

  installInput() {
    const inp = this.input;
    window.addEventListener('keydown', (e) => {
      if (e.repeat) return;
      const k = e.code;
      if (k === 'KeyP') this.paused = !this.paused;
      if (k === 'KeyM') { this.muted = !this.muted; this.audio.setMuted(this.muted); }
      if (k === 'KeyG') { this.gore.clear(); this.addLog('Кровь стёрта'); }
      if (k === 'KeyF') this.spawnHuman(rand(60, W - 60), { forceArmed: true });
      if (k === 'KeyH') this.showHelp = !this.showHelp;
      if (k === 'Space') this.cycleTimeScale();
      if (k === 'Escape') { this.pinned = null; }
      if (k === 'Tab') {
        e.preventDefault();
        const i = TOOLS.findIndex((t) => t.id === this.tool);
        this.setTool(TOOLS[(i + 1) % TOOLS.length].id);
      }
      const toolByKey = TOOLS.find((t) => 'Key' + t.key === k);
      if (toolByKey) this.setTool(toolByKey.id);
      const num = /^Digit([1-8])$/.exec(k);
      if (num) this.selectWeapon(WEAPONS[parseInt(num[1], 10) - 1]);
      if (k === 'Backspace') {
        for (const h of [...this.humans]) if (!h.alive) this.removeHuman(h);
        this.addLog('Трупы убраны');
      }
      if (!this.started) { this.started = true; this.audio.ensure(); }
    });
  }

  setTool(id) {
    this.tool = id;
    this.cutPrev = null;
    this.grab = null;
    if (id !== 'inspect') this.pinned = id === 'inspect' ? this.pinned : this.pinned;
  }

  selectWeapon(def) {
    if (!def) return;
    this.weaponId = def.id;
    this.reloadT = 0;
    if (def.kind === 'gun') this.setTool('shoot');
    else this.setTool('cut');
    this.addLog(`Оружие: ${def.name}`, '#cfc4ad');
  }

  cycleTimeScale() {
    const seq = [1, 0.35, 0.12];
    const i = seq.indexOf(this.timeScale);
    this.timeScale = seq[(i + 1) % seq.length];
  }

  uiButton(x, y, w, h, label, active, id) {
    this.hudRects.push({ x, y, w, h, id });
    const ctx = this.ctx;
    roundRect(ctx, x, y, w, h, 3);
    ctx.fillStyle = active ? 'rgba(96,84,56,0.95)' : 'rgba(14,17,24,0.82)';
    ctx.fill();
    ctx.strokeStyle = active ? 'rgba(233,203,120,0.9)' : 'rgba(150,162,180,0.28)';
    ctx.lineWidth = 0.8;
    ctx.stroke();
    ctx.fillStyle = active ? '#ffe9b0' : '#c9d1de';
    ctx.font = `600 8px ${FONT}`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(label, x + w / 2, y + h / 2 + 0.5);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'top';
  }

  handleUI() {
    const c = this.cursor;
    const clicked = this.input.mouse.clicked;
    if (!clicked) return false;
    for (const r of this.hudRects) {
      if (c.x >= r.x && c.x <= r.x + r.w && c.y >= r.y && c.y <= r.y + r.h) {
        const id = r.id;
        if (id === 'ai') {
          this.aiAggro = !this.aiAggro;
          for (const h of this.humans) { h.ai.enabled = this.aiAggro; h.ai.target = null; h.ai.state = 'idle'; }
          this.addLog(this.aiAggro ? 'ИИ: люди агрессивны' : 'ИИ: люди мирные', this.aiAggro ? '#e08b7b' : '#8fc08f');
        } else if (id === 'slow') this.cycleTimeScale();
        else if (id === 'blood') { this.gore.clear(); this.addLog('Кровь стёрта'); }
        else if (id === 'sound') { this.muted = !this.muted; this.audio.setMuted(this.muted); }
        else if (id === 'pause') this.paused = !this.paused;
        else if (id === 'help') this.showHelp = !this.showHelp;
        else if (id.startsWith('tool:')) this.setTool(id.slice(5));
        else if (id.startsWith('weapon:')) this.selectWeapon(WEAPON_BY_ID[id.slice(7)]);
        else if (id === 'clear') { for (const h of [...this.humans]) this.removeHuman(h); this.gore.clear(); this.combat.clear(); this.debris.length = 0; }
        return true;
      }
    }
    return false;
  }

  /* ================= world ================= */

  buildBackground() {
    const c = document.createElement('canvas');
    c.width = W * RENDER_SCALE;
    c.height = H * RENDER_SCALE;
    const g = c.getContext('2d');
    g.setTransform(RENDER_SCALE, 0, 0, RENDER_SCALE, 0, 0);
    this.bgCanvas = c;

    const sky = g.createLinearGradient(0, 0, 0, GROUND_Y);
    sky.addColorStop(0, '#0d1018');
    sky.addColorStop(0.45, '#1a2030');
    sky.addColorStop(0.78, '#333b4d');
    sky.addColorStop(1, '#5b5a60');
    g.fillStyle = sky;
    g.fillRect(0, 0, W, H);

    // stars
    for (let i = 0; i < 150; i++) {
      const x = hash01(i * 3.7) * W;
      const y = hash01(i * 9.1) * 190;
      const a = 0.12 + hash01(i * 5.3) * 0.55;
      g.fillStyle = `rgba(226,232,244,${a.toFixed(3)})`;
      g.beginPath();
      g.arc(x, y, hash01(i * 7.7) > 0.85 ? 0.9 : 0.55, 0, TAU);
      g.fill();
    }

    // moon
    const mx = 512, my = 62;
    const glow = g.createRadialGradient(mx, my, 4, mx, my, 62);
    glow.addColorStop(0, 'rgba(255,240,210,0.30)');
    glow.addColorStop(0.35, 'rgba(230,220,200,0.10)');
    glow.addColorStop(1, 'rgba(230,220,200,0)');
    g.fillStyle = glow;
    g.beginPath(); g.arc(mx, my, 62, 0, TAU); g.fill();
    g.fillStyle = '#f4ecd6';
    g.beginPath(); g.arc(mx, my, 14, 0, TAU); g.fill();
    g.fillStyle = 'rgba(210,200,180,0.55)';
    g.beginPath(); g.arc(mx - 4, my - 3, 3.2, 0, TAU); g.fill();
    g.beginPath(); g.arc(mx + 4.5, my + 4, 2.2, 0, TAU); g.fill();

    // clouds
    for (let i = 0; i < 7; i++) {
      const cx = hash01(i * 2.3) * W;
      const cy = 56 + hash01(i * 7.1) * 90;
      const cw = 60 + hash01(i * 4.4) * 130;
      const ch = 8 + hash01(i * 8.8) * 13;
      g.fillStyle = `rgba(150,165,190,${0.05 + hash01(i) * 0.05})`;
      g.beginPath();
      g.ellipse(cx, cy, cw, ch, 0, 0, TAU);
      g.fill();
    }

    // distant hills
    g.fillStyle = '#141a25';
    g.beginPath();
    g.moveTo(0, 250);
    for (let x = 0; x <= W; x += 6) {
      const y = 232 - Math.sin(x * 0.008) * 22 - Math.sin(x * 0.021 + 2) * 12;
      g.lineTo(x, y);
    }
    g.lineTo(W, GROUND_Y); g.lineTo(0, GROUND_Y); g.closePath(); g.fill();

    // far skyline
    g.fillStyle = '#0f141d';
    let bx = -10, i = 0;
    while (bx < W + 20) {
      const bw = 14 + Math.floor(hash01(i * 4.1) * 30);
      const bh = 20 + Math.floor(hash01(i * 7.3) * 60);
      g.fillRect(bx, GROUND_Y - 30 - bh, bw, bh + 26);
      if (hash01(i * 2.2) > 0.6) g.fillRect(bx + 4, GROUND_Y - 38 - bh, Math.max(4, bw - 10), 9);
      bx += bw + 4 + Math.floor(hash01(i * 9.9) * 9);
      i++;
    }

    // near ruins with windows
    i = 100;
    bx = -6;
    while (bx < W + 20) {
      const bw = 22 + Math.floor(hash01(i * 3.3) * 34);
      const bh = 16 + Math.floor(hash01(i * 6.7) * 44);
      const top = GROUND_Y - 22 - bh;
      g.fillStyle = '#171d28';
      g.fillRect(bx, top, bw, bh + 22);
      g.fillStyle = '#1d2431';
      g.fillRect(bx + 2, top + 2, bw - 4, 3);
      // windows
      for (let wy = top + 7; wy < GROUND_Y - 26; wy += 8) {
        for (let wx = bx + 4; wx < bx + bw - 4; wx += 7) {
          const lit = hash01(wx * 1.3 + wy * 7.7 + i) > 0.7;
          if (lit) {
            const warm = hash01(wx + wy * 3.1) > 0.25;
            g.fillStyle = warm ? 'rgba(240,196,120,0.5)' : 'rgba(150,200,230,0.4)';
            g.fillRect(wx, wy, 2.4, 3.2);
          }
        }
      }
      // rubble
      g.fillStyle = '#12171f';
      if (hash01(i * 1.7) > 0.5) {
        g.beginPath();
        g.moveTo(bx + bw, GROUND_Y - 22);
        g.lineTo(bx + bw + 5, GROUND_Y - 8);
        g.lineTo(bx + bw, GROUND_Y);
        g.closePath();
        g.fill();
      }
      bx += bw + 5 + Math.floor(hash01(i * 5.5) * 10);
      i++;
    }

    // fog
    const fog = g.createLinearGradient(0, GROUND_Y - 90, 0, GROUND_Y);
    fog.addColorStop(0, 'rgba(120,130,150,0)');
    fog.addColorStop(1, 'rgba(150,158,172,0.20)');
    g.fillStyle = fog;
    g.fillRect(0, GROUND_Y - 90, W, 90);

    // ground
    const gr = g.createLinearGradient(0, GROUND_Y, 0, H);
    gr.addColorStop(0, '#4a4a4c');
    gr.addColorStop(0.15, '#3b3b3e');
    gr.addColorStop(1, '#232327');
    g.fillStyle = gr;
    g.fillRect(0, GROUND_Y, W, H - GROUND_Y);
    g.fillStyle = 'rgba(180,188,200,0.22)';
    g.fillRect(0, GROUND_Y, W, 1.2);
    g.fillStyle = 'rgba(0,0,0,0.25)';
    g.fillRect(0, GROUND_Y + 2.2, W, 2);

    // asphalt texture
    for (let n = 0; n < 900; n++) {
      const x = hash01(n * 1.77) * W;
      const y = GROUND_Y + 3 + hash01(n * 3.31) * (H - GROUND_Y);
      const a = 0.03 + hash01(n * 5.9) * 0.08;
      g.fillStyle = `rgba(220,225,235,${a.toFixed(3)})`;
      g.fillRect(x, y, 1.6, 1.2);
    }
    // cracks
    g.strokeStyle = 'rgba(0,0,0,0.22)';
    g.lineWidth = 0.8;
    for (let n = 0; n < 10; n++) {
      let x = hash01(n * 11.1) * W;
      let y = GROUND_Y + 8 + hash01(n * 3.3) * 40;
      g.beginPath();
      g.moveTo(x, y);
      for (let s = 0; s < 4; s++) {
        x += rand(-16, 16);
        y += rand(-4, 6);
        g.lineTo(x, y);
      }
      g.stroke();
    }
    // grass tufts
    for (let n = 0; n < 55; n++) {
      const x = hash01(n * 13.3) * W;
      const h = 2 + hash01(n * 4.4) * 4;
      g.strokeStyle = 'rgba(80,96,64,0.85)';
      g.lineWidth = 0.9;
      g.beginPath();
      g.moveTo(x, GROUND_Y + 1);
      g.quadraticCurveTo(x + 2, GROUND_Y - h * 0.6, x + rand(-2, 2), GROUND_Y - h);
      g.stroke();
    }
  }

  addLog(text, color = '#cfc4ad') {
    if (this.log[0] && this.log[0].text === text) { this.log[0].t = Math.min(this.log[0].t, 4); return; }
    this.log.unshift({ text, t: 5, color });
    if (this.log.length > 4) this.log.pop();
  }

  hitmarker(x, y) {
    this.combat.hitmarkers.push({ x, y, t: 0.3 });
    this.audio.hitmark();
  }

  shake(n) { this.shakeAmt = Math.min(12, Math.max(this.shakeAmt, n)); }
  hitstop(t) { this.hitstopT = Math.max(this.hitstopT, t); }

  humanAt(x, y, radius = 4) {
    let best = null, bestD = radius;
    for (const h of this.humans) {
      // head / torso first
      const parts = [h.pts.head, h.pts.chest, h.pts.pelvis];
      for (const p of parts) {
        const d = dist(x, y, p.x, p.y) - 6;
        if (d < bestD) { bestD = d; best = h; }
      }
      for (const l of h.limbs) {
        if (l.severed) continue;
        const r = segSegClosest(x, y, x, y, l.a.x, l.a.y, l.b.x, l.b.y);
        if (r.d - l.radius < bestD) { bestD = r.d - l.radius; best = h; }
      }
    }
    return best;
  }

  /* ================= loop ================= */

  loop(now) {
    let dtReal = (now - this.last) / 1000;
    this.last = now;
    if (!isFinite(dtReal)) dtReal = 1 / 60;
    dtReal = Math.min(dtReal, 0.05);
    this.realTime += dtReal;

    this.updateCursor();
    if (!this.started) {
      if (this.cursor.justDown || this.input.pressed('Enter')) {
        this.started = true;
        this.audio.ensure();
      }
    }

    const uiClick = this.started ? this.handleUI() : false;

    if (!this.paused && this.started) {
      let dt = dtReal * this.timeScale;
      if (this.hitstopT > 0) { this.hitstopT -= dtReal; dt *= 0.25; }
      this.update(dt, dtReal, uiClick);
    }
    this.render();
    this.input.endFrame();
    requestAnimationFrame(this.loop.bind(this));
  }

  updateCursor() {
    const m = this.input.mouse;
    this.cursor.x = m.x;
    this.cursor.y = m.y;
    this.cursor.down = m.down;
    this.cursor.justDown = m.clicked;
    this.cursor.rdown = m.rdown;
    this.cursor.inside = m.inside;
  }

  update(dt, dtReal, uiClick) {
    this.time += dt;
    const c = this.cursor;

    for (let i = this.log.length - 1; i >= 0; i--) {
      this.log[i].t -= dtReal;
      if (this.log[i].t <= 0) this.log.splice(i, 1);
    }

    this.hovered = this.humanAt(c.x, c.y, 5);
    this.inspected = this.pinned && this.humans.includes(this.pinned) ? this.pinned : this.hovered;

    for (const h of this.humans) h.update(dt, this);
    this.combat.update(dt, this);
    this.gore.update(dt);
    this.updateDebris(dt);
    this.resolveBodyCollisions(dt);
    this.handleTool(dt, uiClick);

    this.shakeAmt *= Math.exp(-6 * dtReal);
    if (this.shakeAmt < 0.03) this.shakeAmt = 0;
  }

  resolveBodyCollisions(dt) {
    for (let i = 0; i < this.humans.length; i++) {
      for (let j = i + 1; j < this.humans.length; j++) {
        const a = this.humans[i], b = this.humans[j];
        if (Math.abs(a.pos.x - b.pos.x) > 30) continue;
        if (!a.ragdoll && !b.ragdoll) {
          const d = b.pos.x - a.pos.x;
          const overlap = 13 - Math.abs(d);
          if (overlap > 0) {
            const push = Math.sign(d || 1) * overlap * 0.5;
            a.pos.x -= push; b.pos.x += push;
          }
        } else {
          const pa = a.ragdoll ? a.pts.chest : a.pts.pelvis;
          const pb = b.ragdoll ? b.pts.chest : b.pts.pelvis;
          const dx = pb.x - pa.x, dy = pb.y - pa.y;
          const d = Math.hypot(dx, dy);
          const min = 9;
          if (d > 0.01 && d < min) {
            const k = (min - d) / d * 0.5;
            if (a.ragdoll) { pa.x -= dx * k; pa.y -= dy * k; a.wake(); }
            if (b.ragdoll) { pb.x += dx * k; pb.y += dy * k; b.wake(); }
          }
        }
      }
    }
  }

  /* ================= tools ================= */

  activeMelee() {
    const def = WEAPON_BY_ID[this.weaponId];
    return def && def.kind === 'melee' ? def : WEAPON_BY_ID.knife;
  }

  handleTool(dt, uiClick) {
    const c = this.cursor;
    const tool = this.tool;

    if (tool === 'cut') this.toolCut(dt, uiClick);
    else if (tool === 'shoot') this.toolShoot(dt, uiClick);
    else if (tool === 'grab') this.toolGrab(dt);
    else if (tool === 'heal') { if (c.justDown && !uiClick && this.hovered) this.healHuman(this.hovered); }
    else if (tool === 'delete') { if (c.justDown && !uiClick && this.hovered) this.removeHuman(this.hovered); }
    else if (tool === 'spawn') {
      if (c.justDown && !uiClick && c.y > GROUND_Y - 40) {
        this.spawnHuman(c.x, { forceArmed: this.input.down('ShiftLeft') || this.input.down('ShiftRight') });
        this.audio.step();
      }
    } else if (tool === 'inspect') {
      if (c.justDown && !uiClick) {
        if (this.hovered) { this.pinned = this.pinned === this.hovered ? null : this.hovered; }
        else this.pinned = null;
      }
    }
  }

  healHuman(h) {
    h.blood = Math.min(5000, h.blood + 2600);
    h.health = Math.min(100, h.health + 70);
    for (const l of h.limbs) {
      l.struct = Math.max(0, l.struct - 0.65);
      l.broken = false;
      l.arterial = false;
      l.wounds.length = 0;
      l.bleedRate = 0;
      l.stumpBleed = 0;
    }
    if (h.unconscious) { h.unconscious = false; if (h.ragdoll) h.ragTime = Math.max(h.ragTime, 1.3); }
    if (!h.alive) {
      this.addLog(`${h.name}: мёртв — лечение не помогает`, '#c98f8f');
      return;
    }
    this.addLog(`${h.name}: обработан`, '#8fc08f');
    this.audio.reload();
  }

  toolCut(dt, uiClick) {
    const c = this.cursor;
    const def = this.activeMelee();
    const speed = this.cutPrev ? dist(this.cutPrev.x, this.cutPrev.y, c.x, c.y) : 0;

    if (c.down && !uiClick) {
      if (this.cutPrev) {
        const power = clamp(speed / 7, 0.3, 2.0);
        for (const h of this.humans) {
          for (const limb of h.limbs) {
            if (limb.severed || limb.parentSevered) continue;
            const res = segSegClosest(this.cutPrev.x, this.cutPrev.y, c.x, c.y, limb.a.x, limb.a.y, limb.b.x, limb.b.y);
            if (res.d > limb.radius + 1.4) continue;
            const last = this.cutCd.get(limb.id) || -1;
            if (this.time - last < 0.07) continue;
            this.cutCd.set(limb.id, this.time);
            h.applyHit(limb, {
              type: 'slash',
              dmg: def.dmg * 0.5 * power,
              struct: def.structDmg * 0.55 * power,
              cut: def.cut,
              bleedMul: def.bleedMul,
              knock: 16 * power,
              at: { x: res.x, y: res.y },
              dir: { x: Math.cos(Math.atan2(c.y - this.cutPrev.y, c.x - this.cutPrev.x)), y: Math.sin(Math.atan2(c.y - this.cutPrev.y, c.x - this.cutPrev.x)) },
              weapon: def,
            });
            this.gore.spray(res.x, res.y, c.x - this.cutPrev.x, c.y - this.cutPrev.y, {
              count: Math.round(3 + 8 * power), speed: 80 + 150 * power, size: 1.5, spread: 0.6, mistChance: 0.3,
            });
            this.audio.cut();
            this.hitmarker(res.x, res.y);
            if (speed > 5) this.gore.flesh(res.x, res.y, 0, 0, 1, 0.5);
          }
        }
        if (speed > 4) this.audio.slash();
      }
      this.cutPrev = { x: c.x, y: c.y };
    } else {
      this.cutPrev = null;
    }
  }

  toolShoot(dt, uiClick) {
    const c = this.cursor;
    const def = WEAPON_BY_ID[this.weaponId];
    this.fireCd = Math.max(0, this.fireCd - dt);

    if (this.reloadT > 0) {
      this.reloadT -= dt;
      if (this.reloadT <= 0) {
        this.ammo[def.id] = def.magSize;
        this.audio.reload();
      }
      return;
    }
    if (def.kind !== 'gun') { this.setTool('cut'); return; }

    const mag = this.ammo[def.id] == null ? def.magSize : this.ammo[def.id];
    if (c.down && !uiClick && this.fireCd <= 0 && !this.hoveredOverUI) {
      if (mag <= 0) {
        this.reloadT = Math.max(0.7, def.reloadTime * 0.6);
        this.audio.empty();
        return;
      }
      const shot = this.fireFromCursor(def);
      this.ammo[def.id] = mag - 1;
      this.fireCd = 60 / def.rpm;
      this.aimKick = Math.min(6, (this.aimKick || 0) + def.recoil * 14);
      this.shake(def.shake * 0.8);
      this.hitstop(def.id === 'shotgun' ? 0.018 : 0.006);
      if (shot) this.audio.casing();
      if (this.ammo[def.id] <= 0) this.reloadT = Math.max(0.8, def.reloadTime * 0.75);
    }
    this.aimKick = (this.aimKick || 0) * Math.exp(-8 * dt);
  }

  // The shooter sits outside the frame: bullets enter from the edge and travel
  // exactly through the crosshair.
  shotOrigin() {
    const c = this.cursor;
    const cx = W / 2, cy = H / 2 + 6;
    let dx = c.x - cx, dy = c.y - cy;
    let len = Math.hypot(dx, dy);
    if (len < 8) {
      dx = Math.cos(this.aimAngle);
      dy = Math.sin(this.aimAngle);
      len = 1;
    }
    const ux = dx / len, uy = dy / len;
    // distance from the cursor back to the screen border
    let t = Infinity;
    if (ux > 0.0001) t = Math.min(t, c.x / ux);
    else if (ux < -0.0001) t = Math.min(t, (W - c.x) / -ux);
    if (uy > 0.0001) t = Math.min(t, c.y / uy);
    else if (uy < -0.0001) t = Math.min(t, (H - c.y) / -uy);
    if (!isFinite(t)) t = 200;
    const edge = Math.max(30, t);
    // the weapon sits just inside the frame, as if held by the viewer
    const gunD = Math.max(6, edge - 30);
    const def = WEAPON_BY_ID[this.weaponId];
    const muzzleOff = def && def.muzzle ? def.muzzle.x : 8;
    return {
      x: c.x - ux * (gunD + muzzleOff + 4),
      y: c.y - uy * (gunD + muzzleOff + 4),
      gunX: c.x - ux * gunD,
      gunY: c.y - uy * gunD,
      angle: Math.atan2(uy, ux),
      ux, uy,
    };
  }

  fireFromCursor(def) {
    const o = this.shotOrigin();
    this.aimAngle = o.angle;
    this.lastShotAt = this.time;
    this.combat.fireFromPoint(o.x, o.y, o.angle, def, { extraSpread: 0 });
    return true;
  }

  toolGrab(dt) {
    const c = this.cursor;
    if (c.justDown && !this.hoveredOverUI) {
      const h = this.humanAt(c.x, c.y, 6);
      if (h) {
        if (!h.ragdoll) h.goRagdoll();
        h.wake();
        let best = null, bestD = Infinity;
        for (const k in h.pts) {
          const p = h.pts[k];
          const d = dist(c.x, c.y, p.x, p.y);
          if (d < bestD) { bestD = d; best = p; }
        }
        this.grab = { human: h, point: best };
      }
    }
    if (!c.down) { this.grab = null; return; }
    if (!this.grab) return;
    const g = this.grab;
    const p = g.point;
    const nx = p.x + (c.x - p.x) * 0.5;
    const ny = p.y + (c.y - p.y) * 0.5;
    p.vx = (nx - p.x) / Math.max(dt, 1 / 120);
    p.vy = (ny - p.y) / Math.max(dt, 1 / 120);
    p.x = nx; p.y = ny;
    g.human.wake();
  }

  updateDebris(dt) {
    for (let i = this.debris.length - 1; i >= 0; i--) {
      const d = this.debris[i];
      if (!d || d.type === 'weapon') {
        if (!d) { this.debris.splice(i, 1); continue; }
        d.vy += 900 * dt;
        d.x += d.vx * dt;
        d.y += d.vy * dt;
        d.rot += d.vr * dt;
        if (d.y > GROUND_Y - 1) {
          d.y = GROUND_Y - 1;
          d.vy *= -0.22;
          d.vx *= 0.6;
          d.vr *= 0.5;
          if (Math.abs(d.vy) < 12) { d.vy = 0; d.vx *= 0.9; }
        }
        continue;
      }
      if (Math.hypot(d.a.x - (this.camX || 0), d.a.y) > 2000) { this.debris.splice(i, 1); continue; }
      const G = 900;
      for (const key of ['a', 'b']) {
        const p = d[key];
        p.vy += G * dt;
        p.vx *= 1 - 0.9 * dt;
        p.x += p.vx * dt;
        p.y += p.vy * dt;
      }
      for (let it = 0; it < 3; it++) {
        let dx = d.b.x - d.a.x, dy = d.b.y - d.a.y;
        const len = Math.hypot(dx, dy) || 1e-6;
        const diff = ((len - d.rest) / len) * 0.5 * 0.8;
        dx *= diff; dy *= diff;
        d.a.x += dx; d.a.y += dy;
        d.b.x -= dx; d.b.y -= dy;
        for (const key of ['a', 'b']) {
          const p = d[key];
          const r = d.radius;
          if (p.y > GROUND_Y - r) {
            p.y = GROUND_Y - r;
            if (p.vy > 0) p.vy *= -0.1;
            p.vx *= 0.8;
          }
          if (p.x < 4) { p.x = 4; p.vx *= -0.3; }
          if (p.x > W - 4) { p.x = W - 4; p.vx *= -0.3; }
        }
      }
      d.rot = Math.atan2(d.b.y - d.a.y, d.b.x - d.a.x);
      d.bleed = Math.max(0, (d.bleed || 0) - dt * 3);
      if ((d.bleed || 0) > 0.4 && chance(dt * 5)) {
        this.gore.bleed(d.a.x, d.a.y, rand(-1, 1), Math.PI / 2, 30 * d.bleed, false, dt);
      }
    }
  }

  /* ================= render ================= */

  render() {
    const ctx = this.ctx;
    ctx.setTransform(RENDER_SCALE, 0, 0, RENDER_SCALE, 0, 0);
    ctx.clearRect(0, 0, W, H);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';

    const sx = this.shakeAmt ? rand(-this.shakeAmt, this.shakeAmt) : 0;
    const sy = this.shakeAmt ? rand(-this.shakeAmt, this.shakeAmt) * 0.7 : 0;

    ctx.save();
    ctx.translate(Math.round(sx * 2) / 2, Math.round(sy * 2) / 2);
    ctx.drawImage(this.bgCanvas, 0, 0, W, H);
    this.gore.drawDecals(ctx);

    // dead bodies first, then the living, so living read clearly
    const sorted = [...this.humans].sort((a, b) => (a.alive === b.alive ? 0 : a.alive ? 1 : -1));
    this.drawDebris(ctx);
    for (const h of sorted) h.draw(ctx, this);

    this.combat.drawUnder(ctx);
    this.gore.draw(ctx);
    this.combat.drawOver(ctx);
    this.drawCursor(ctx);
    ctx.restore();

    this.gore.drawScreen(ctx);

    // frame vignette
    const vg = ctx.createRadialGradient(W / 2, H / 2, 150, W / 2, H / 2, 380);
    vg.addColorStop(0, 'rgba(0,0,0,0)');
    vg.addColorStop(1, 'rgba(0,0,0,0.42)');
    ctx.fillStyle = vg;
    ctx.fillRect(0, 0, W, H);

    if (this.started) {
      this.drawHUD(ctx);
      this.drawInspector(ctx);
      this.drawLog(ctx);
    }
    if (this.showHelp) this.drawHelp(ctx);
    if (!this.started) this.drawStart(ctx);
    if (this.paused) this.drawPause(ctx);
  }

  drawDebris(ctx) {
    for (const d of this.debris) {
      if (!d) continue;
      if (d.type === 'weapon') {
        drawWeapon(ctx, d.def, d.x, d.y, d.rot, 1, 0, 0.6);
        continue;
      }
      const nx = Math.cos(d.rot + Math.PI / 2), ny = Math.sin(d.rot + Math.PI / 2);
      ctx.lineCap = 'round';
      const col = d.kind === 'skin' ? '#c78e6a' : d.color;
      this.lineStroke(ctx, d.a, d.b, d.radius * 2 + 1.6, '#15141a');
      this.lineStroke(ctx, d.a, d.b, d.radius * 2, col);
      // stump
      ctx.fillStyle = '#6d0609';
      ctx.beginPath(); ctx.arc(d.a.x, d.a.y, d.radius * 0.95, 0, TAU); ctx.fill();
      ctx.fillStyle = '#3a0205';
      ctx.beginPath(); ctx.arc(d.a.x + nx * 0.4, d.a.y + ny * 0.4, d.radius * 0.42, 0, TAU); ctx.fill();
      ctx.fillStyle = '#e8dfc9';
      ctx.beginPath(); ctx.arc(d.a.x + 0.7, d.a.y - 0.5, 0.8, 0, TAU); ctx.fill();
      if (d.shoe) {
        ctx.fillStyle = '#231f19';
        roundRect(ctx, d.b.x - 3.4, d.b.y - 1.8, 7, 3.6, 1.5);
        ctx.fill();
      }
      for (const w of d.wounds || []) {
        const wx = d.a.x + (d.b.x - d.a.x) * w.t + nx * w.r;
        const wy = d.a.y + (d.b.y - d.a.y) * w.t + ny * w.r;
        ctx.fillStyle = '#5b0609';
        ctx.beginPath(); ctx.arc(wx, wy, Math.max(0.8, w.r), 0, TAU); ctx.fill();
      }
    }
  }

  lineStroke(ctx, a, b, width, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
  }

  drawCursor(ctx) {
    if (!this.cursor.inside) return;
    const c = this.cursor;
    const tool = this.tool;

    if (tool === 'cut') {
      const def = this.activeMelee();
      let ang = -0.5;
      if (this.cutPrev) {
        const dx = c.x - this.cutPrev.x, dy = c.y - this.cutPrev.y;
        if (Math.hypot(dx, dy) > 0.8) ang = Math.atan2(dy, dx);
      }
      // blade trail
      if (this.cutPrev && c.down) {
        ctx.strokeStyle = 'rgba(255,255,255,0.2)';
        ctx.lineWidth = 2.4;
        ctx.beginPath();
        ctx.moveTo(this.cutPrev.x, this.cutPrev.y);
        ctx.lineTo(c.x, c.y);
        ctx.stroke();
      }
      drawWeapon(ctx, def, c.x, c.y, ang, 1, 0, 0.85);
      ctx.strokeStyle = 'rgba(230,235,240,0.5)';
      ctx.lineWidth = 0.7;
      ctx.beginPath(); ctx.arc(c.x, c.y, 2.6, 0, TAU); ctx.stroke();
    } else if (tool === 'shoot') {
      const def = WEAPON_BY_ID[this.weaponId];
      if (def.kind !== 'gun') return;
      const o = this.shotOrigin();
      const flash = this.time - (this.lastShotAt || -1) < 0.06 ? 1 : 0;
      // forearm coming in from the edge of the frame
      const hx = o.gunX - o.ux * 12, hy = o.gunY - o.uy * 12;
      ctx.save();
      ctx.lineCap = 'round';
      ctx.strokeStyle = '#1a141a';
      ctx.lineWidth = 5.4;
      ctx.beginPath();
      ctx.moveTo(o.gunX - o.ux * 34, o.gunY - o.uy * 34);
      ctx.lineTo(hx, hy);
      ctx.stroke();
      ctx.strokeStyle = '#b98a63';
      ctx.lineWidth = 4.2;
      ctx.beginPath();
      ctx.moveTo(o.gunX - o.ux * 32, o.gunY - o.uy * 32);
      ctx.lineTo(hx, hy);
      ctx.stroke();
      ctx.globalAlpha = 0.95;
      drawWeapon(ctx, def, o.gunX, o.gunY, o.angle, 1, flash, 0);
      ctx.restore();

      // crosshair
      const spread = 2 + def.spread * 120 + (this.reloadT > 0 ? 6 : 0);
      ctx.strokeStyle = 'rgba(235,240,245,0.7)';
      ctx.lineWidth = 0.8;
      for (let i = 0; i < 4; i++) {
        const a = i * Math.PI / 2;
        ctx.beginPath();
        ctx.moveTo(c.x + Math.cos(a) * spread, c.y + Math.sin(a) * spread);
        ctx.lineTo(c.x + Math.cos(a) * (spread + 3.4), c.y + Math.sin(a) * (spread + 3.4));
        ctx.stroke();
      }
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      ctx.beginPath(); ctx.arc(c.x, c.y, 0.9, 0, TAU); ctx.fill();

      // faint guide from the muzzle to the crosshair
      ctx.globalAlpha = 0.14;
      ctx.strokeStyle = '#dfe7f2';
      ctx.beginPath();
      ctx.moveTo(o.gunX + Math.cos(o.angle) * 10, o.gunY + Math.sin(o.angle) * 10);
      ctx.lineTo(c.x, c.y);
      ctx.stroke();
      ctx.globalAlpha = 1;

      if (this.reloadT > 0) {
        ctx.strokeStyle = 'rgba(240,210,130,0.9)';
        ctx.lineWidth = 1.6;
        ctx.beginPath();
        ctx.arc(c.x, c.y, 10, -Math.PI / 2, -Math.PI / 2 + TAU * (1 - this.reloadT / Math.max(0.8, def.reloadTime * 0.75)));
        ctx.stroke();
      }
    } else if (tool === 'inspect') {
      ctx.strokeStyle = 'rgba(140,205,255,0.85)';
      ctx.lineWidth = 1;
      ctx.beginPath(); ctx.arc(c.x, c.y, 7, 0, TAU); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(c.x + 5, c.y + 5); ctx.lineTo(c.x + 9, c.y + 9); ctx.stroke();
    } else if (tool === 'spawn') {
      ctx.strokeStyle = 'rgba(150,235,170,0.9)';
      ctx.lineWidth = 1.4;
      ctx.beginPath();
      ctx.moveTo(c.x - 4, c.y); ctx.lineTo(c.x + 4, c.y);
      ctx.moveTo(c.x, c.y - 4); ctx.lineTo(c.x, c.y + 4);
      ctx.stroke();
      ctx.globalAlpha = 0.25;
      ctx.strokeStyle = '#a8f0b8';
      ctx.beginPath(); ctx.ellipse(c.x, GROUND_Y + 1, 12, 3, 0, 0, TAU); ctx.stroke();
      ctx.globalAlpha = 1;
    } else if (tool === 'grab') {
      ctx.strokeStyle = this.grab ? 'rgba(255,200,120,0.95)' : 'rgba(220,220,230,0.7)';
      ctx.lineWidth = 1.2;
      ctx.beginPath(); ctx.arc(c.x, c.y, 5, 0, TAU); ctx.stroke();
      if (this.grab) {
        ctx.beginPath();
        ctx.moveTo(c.x, c.y);
        ctx.lineTo(this.grab.point.x, this.grab.point.y);
        ctx.setLineDash([2, 2]);
        ctx.stroke();
        ctx.setLineDash([]);
      }
    } else if (tool === 'heal') {
      ctx.strokeStyle = 'rgba(130,230,150,0.95)';
      ctx.lineWidth = 1.8;
      ctx.beginPath();
      ctx.moveTo(c.x - 3.5, c.y); ctx.lineTo(c.x + 3.5, c.y);
      ctx.moveTo(c.x, c.y - 3.5); ctx.lineTo(c.x, c.y + 3.5);
      ctx.stroke();
    } else if (tool === 'delete') {
      ctx.strokeStyle = 'rgba(255,120,120,0.95)';
      ctx.lineWidth = 1.4;
      ctx.beginPath();
      ctx.moveTo(c.x - 3.5, c.y - 3.5); ctx.lineTo(c.x + 3.5, c.y + 3.5);
      ctx.moveTo(c.x + 3.5, c.y - 3.5); ctx.lineTo(c.x - 3.5, c.y + 3.5);
      ctx.stroke();
    }

    // hover highlight
    if (this.hovered && tool !== 'delete') {
      const h = this.hovered;
      const p = h.ragdoll ? h.pts.chest : h.pts.pelvis;
      ctx.strokeStyle = h === this.pinned ? 'rgba(140,205,255,0.9)' : 'rgba(220,228,240,0.4)';
      ctx.lineWidth = 0.8;
      ctx.setLineDash([3, 3]);
      ctx.beginPath(); ctx.ellipse(p.x, GROUND_Y + 1, 15, 4, 0, 0, TAU); ctx.stroke();
      ctx.setLineDash([]);
      ctx.font = `600 8px ${FONT}`;
      ctx.fillStyle = 'rgba(10,14,20,0.7)';
      const label = `${h.name}${h.alive ? '' : ' · мёртв'}`;
      const w = ctx.measureText(label).width + 8;
      roundRect(ctx, p.x - w / 2, p.y - 36, w, 12, 3);
      ctx.fill();
      ctx.fillStyle = h.alive ? '#dfe7f2' : '#c99a9a';
      ctx.textAlign = 'center';
      ctx.fillText(label, p.x, p.y - 27);
      ctx.textAlign = 'left';
    }
  }

  /* ================= HUD ================= */

  drawHUD(ctx) {
    this.hudRects.length = 0;

    // ---- row 1: tools (left) + help/pause (right)
    let x = 8;
    const y = 8;
    for (const t of TOOLS) {
      const active = this.tool === t.id;
      const w = 24 + t.name.length * 4.9;
      roundRect(ctx, x, y, w, 19, 3);
      ctx.fillStyle = active ? 'rgba(104,90,58,0.96)' : 'rgba(12,16,23,0.84)';
      ctx.fill();
      ctx.strokeStyle = active ? 'rgba(240,208,124,0.95)' : 'rgba(150,162,180,0.24)';
      ctx.lineWidth = 0.8;
      ctx.stroke();
      ctx.font = `700 7px ${FONT}`;
      ctx.fillStyle = active ? '#ffe9b0' : '#7f8aa0';
      ctx.fillText(t.key, x + 5, y + 5);
      ctx.font = `600 8.5px ${FONT}`;
      ctx.fillStyle = active ? '#ffe9b0' : '#c9d1de';
      ctx.fillText(t.name, x + 13, y + 6);
      this.hudRects.push({ x, y, w, h: 19, id: 'tool:' + t.id });
      x += w + 3;
    }
    // help + pause pinned right
    this.uiButton(W - 8 - 46, y, 46, 19, this.showHelp ? 'ЗАКРЫТЬ' : 'ПОМОЩЬ', this.showHelp, 'help');
    this.uiButton(W - 8 - 46 - 3 - 50, y, 50, 19, this.paused ? 'ИГРАТЬ' : 'ПАУЗА', this.paused, 'pause');

    // ---- row 2: world settings
    ctx.font = `600 8px ${FONT}`;
    const row2 = [
      ['ai', this.aiAggro ? 'ИИ: АГРЕССИЯ' : 'ИИ: МИРНЫЕ', this.aiAggro],
      ['slow', this.timeScale === 1 ? 'ТЕМП x1' : `ТЕМП x${this.timeScale}`, this.timeScale !== 1],
      ['blood', 'СТЕРЕТЬ КРОВЬ', false],
      ['sound', this.muted ? 'ЗВУК ВЫКЛ' : 'ЗВУК ВКЛ', !this.muted],
    ];
    let sx = 8;
    const sy = 31;
    for (const [id, label, active] of row2) {
      const w = Math.max(44, ctx.measureText(label).width + 12);
      this.uiButton(sx, sy, w, 16, label, active, id);
      sx += w + 4;
    }

    // ---- tool hint (above weapon bar)
    const t = TOOLS.find((tt) => tt.id === this.tool);
    const hint = t ? t.hint : '';
    ctx.font = `500 8px ${FONT}`;
    const hw = ctx.measureText(hint).width + 14;
    roundRect(ctx, 8, H - 61, hw, 14, 3);
    ctx.fillStyle = 'rgba(8,11,17,0.72)';
    ctx.fill();
    ctx.fillStyle = 'rgba(214,223,236,0.9)';
    ctx.fillText(hint, 14, H - 57);

    // ---- weapon bar
    const n = WEAPONS.length;
    const slotW = 45, slotH = 30, gap = 3;
    const totalW = n * slotW + (n - 1) * gap;
    const bx = Math.round((W - totalW) / 2);
    const by = H - slotH - 8;
    for (let i = 0; i < n; i++) {
      const def = WEAPONS[i];
      const active = this.weaponId === def.id;
      const x2 = bx + i * (slotW + gap);
      roundRect(ctx, x2, by, slotW, slotH, 3);
      ctx.fillStyle = active ? 'rgba(104,90,58,0.96)' : 'rgba(12,16,23,0.85)';
      ctx.fill();
      ctx.strokeStyle = active ? 'rgba(240,208,124,0.95)' : 'rgba(150,162,180,0.22)';
      ctx.lineWidth = 0.8;
      ctx.stroke();
      // thumbnail
      ctx.save();
      ctx.translate(x2 + 6, by + 10);
      const sc = def.kind === 'gun' ? (def.id === 'pistol' ? 0.62 : 0.5) : 0.5;
      ctx.scale(sc, sc);
      if (def.kind === 'gun') ctx.translate(-def.muzzle.x * 0.3, 3);
      else ctx.translate(2, 4);
      drawWeapon(ctx, def, 0, 0, -0.15, 1, 0, 0.4);
      ctx.restore();
      // number + name
      ctx.font = `700 7px ${FONT}`;
      ctx.fillStyle = active ? '#ffe9b0' : '#7f8aa0';
      ctx.textAlign = 'left';
      ctx.fillText(String(i + 1), x2 + 3, by + 3);
      ctx.font = `600 6.5px ${FONT}`;
      ctx.fillStyle = active ? '#f4e6be' : '#b4bdcc';
      ctx.fillText(def.short, x2 + 3, by + slotH - 10);
      if (def.kind === 'gun' && active) {
        const mag = this.ammo[def.id] == null ? def.magSize : this.ammo[def.id];
        ctx.font = `700 7px ${FONT}`;
        ctx.fillStyle = mag > 0 ? '#d5deeb' : '#e07373';
        ctx.textAlign = 'right';
        ctx.fillText(this.reloadT > 0 ? '···' : `${mag}`, x2 + slotW - 3, by + 3);
        ctx.textAlign = 'left';
      }
      this.hudRects.push({ x: x2, y: by, w: slotW, h: slotH, id: 'weapon:' + def.id });
    }

    // ---- selected person name
    const h = this.inspected;
    if (h) {
      ctx.font = `700 9px ${FONT}`;
      ctx.fillStyle = '#dfe7f2';
      ctx.fillText(`${h.name}${h.alive ? '' : ' · мёртв'}`, 10, 52);
    }
  }

  drawInspector(ctx) {
    const h = this.inspected;
    if (!h) return;
    const pw = 202;
    const px = W - pw - 8;
    const py = 52;
    const ph = H - py - 66;

    roundRect(ctx, px, py, pw, ph, 4);
    ctx.fillStyle = 'rgba(10,13,19,0.88)';
    ctx.fill();
    ctx.strokeStyle = 'rgba(140,205,255,0.35)';
    ctx.lineWidth = 0.8;
    ctx.stroke();

    let y = py + 7;
    const left = px + 8;
    const right = px + pw - 8;

    ctx.font = `700 10px ${FONT}`;
    ctx.fillStyle = '#e8eef8';
    ctx.fillText(h.name, left, y);
    const status = h.alive ? (h.unconscious ? 'БЕЗ СОЗНАНИЯ' : 'ЖИВ') : 'МЁРТВ';
    const stCol = h.alive ? (h.unconscious ? '#e0b060' : '#7fce8f') : '#e07373';
    ctx.font = `700 8px ${FONT}`;
    ctx.textAlign = 'right';
    ctx.fillStyle = stCol;
    ctx.fillText(status, right, y + 1);
    ctx.textAlign = 'left';
    y += 15;

    if (!h.alive && h.deathCause) {
      ctx.font = `500 7.5px ${FONT}`;
      ctx.fillStyle = '#c98f8f';
      ctx.fillText(`причина: ${h.deathCause}`, left, y);
      y += 11;
    }

    // bars
    const bar = (label, value, max, col, warn) => {
      ctx.font = `500 7.5px ${FONT}`;
      ctx.fillStyle = '#9aa6ba';
      ctx.fillText(label, left, y + 1);
      const bw = 108, bx = px + 62;
      roundRect(ctx, bx, y, bw, 6, 2);
      ctx.fillStyle = 'rgba(255,255,255,0.08)';
      ctx.fill();
      roundRect(ctx, bx, y, Math.max(1, bw * clamp(value / max, 0, 1)), 6, 2);
      ctx.fillStyle = warn ? '#e07373' : col;
      ctx.fill();
      ctx.textAlign = 'right';
      ctx.fillStyle = '#d5dde9';
      ctx.font = `600 7.5px ${FONT}`;
      ctx.fillText(warn ? 'КРИТИЧНО' : `${Math.round(value)}`, right, y + 1);
      ctx.textAlign = 'left';
      y += 11;
    };
    bar('Здоровье', h.health, 100, h.health > 45 ? '#c94b4b' : '#e07373', h.health < 18);
    bar('Кровь', h.blood / 50, 100, '#a4282e', h.blood < 1600);
    ctx.font = `500 7.5px ${FONT}`;
    ctx.fillStyle = '#9aa6ba';
    ctx.fillText('Кровопотеря', left, y + 1);
    ctx.fillStyle = h.bleedTotal > 1 ? '#e08b7b' : '#8f9bb0';
    ctx.fillText(`${h.bleedTotal.toFixed(1)} мл/с`, left + 62, y + 1);
    ctx.fillStyle = '#8f9bb0';
    ctx.fillText(`${((5000 - h.blood) / 1000).toFixed(2)} л`, right - 34, y + 1);
    y += 11;
    ctx.fillStyle = '#9aa6ba';
    ctx.fillText('Боль', left, y + 1);
    ctx.fillStyle = h.pain > 0.8 ? '#e0a055' : '#8f9bb0';
    ctx.fillText(Math.round(clamp(h.pain, 0, 1.5) * 100) + '%', left + 62, y + 1);
    ctx.fillStyle = '#8f9bb0';
    ctx.fillText(h.weapon.name, right - 60, y + 1);
    if (h.weapon.kind === 'gun') {
      ctx.fillStyle = '#cfd8e6';
      ctx.textAlign = 'right';
      ctx.fillText(`${h.mags[h.weapon.id] | 0}/${h.weapon.magSize}`, right, y + 1);
      ctx.textAlign = 'left';
    }
    y += 13;

    // separator
    ctx.strokeStyle = 'rgba(150,170,200,0.18)';
    ctx.lineWidth = 0.7;
    ctx.beginPath(); ctx.moveTo(left, y); ctx.lineTo(right, y); ctx.stroke();
    y += 6;

    ctx.font = `700 7.5px ${FONT}`;
    ctx.fillStyle = '#8fa2bd';
    ctx.fillText('КОНЕЧНОСТИ', left, y);
    ctx.textAlign = 'right';
    ctx.fillText('ткани', right, y);
    ctx.textAlign = 'left';
    y += 9;

    const limbs = [...h.limbs].sort((a, b) => {
      if (a.severed !== b.severed) return a.severed ? 1 : -1;
      return a.label.localeCompare(b.label, 'ru');
    });
    for (const l of limbs) {
      const st = LIMB_STATUS(l);
      const integrity = l.severed ? 0 : clamp(1 - l.struct, 0, 1);
      ctx.font = `500 7.5px ${FONT}`;
      ctx.fillStyle = l.severed ? '#7c5a5a' : '#c3cddc';
      ctx.fillText(l.label, left, y);
      // integrity mini bar
      const bw = 34, bx = px + 92;
      roundRect(ctx, bx, y + 1, bw, 4.5, 1.5);
      ctx.fillStyle = 'rgba(255,255,255,0.07)';
      ctx.fill();
      roundRect(ctx, bx, y + 1, Math.max(0.6, bw * integrity), 4.5, 1.5);
      ctx.fillStyle = l.severed ? '#5a2f2f' : integrity > 0.6 ? '#6f9f6f' : integrity > 0.3 ? '#c9a24a' : '#c45555';
      ctx.fill();
      ctx.fillStyle = st.col;
      ctx.textAlign = 'right';
      ctx.font = `500 7px ${FONT}`;
      ctx.fillText(st.text, right, y);
      ctx.textAlign = 'left';
      y += 9.4;
    }

    y += 4;
    const wounds = h.limbs.reduce((s, l) => s + l.wounds.length, 0);
    ctx.font = `500 7.5px ${FONT}`;
    ctx.fillStyle = '#9aa6ba';
    ctx.fillText('Ран всего', left, y);
    ctx.fillStyle = '#d5dde9';
    ctx.fillText(String(wounds), left + 62, y);
    y += 10;
    ctx.fillStyle = '#9aa6ba';
    ctx.fillText('ИИ', left, y);
    ctx.fillStyle = '#d5dde9';
    ctx.fillText(
      !h.ai.enabled ? 'выключен (мирный)' :
        !h.alive ? '—' :
          h.unconscious ? 'без сознания' :
            h.ai.target ? `атакует ${h.ai.target.name}` : 'выбирает цель',
      left + 30, y);
    y += 12;
    ctx.fillStyle = 'rgba(150,165,190,0.55)';
    ctx.font = `500 7px ${FONT}`;
    ctx.fillText(this.pinned === h ? 'панель закреплена · Esc снять' : 'панель за наведением · клик закрепить', left, y);
  }

  drawLog(ctx) {
    ctx.font = `600 8px ${FONT}`;
    ctx.textAlign = 'left';
    for (let i = 0; i < this.log.length; i++) {
      const e = this.log[i];
      const a = clamp(e.t, 0, 1);
      ctx.globalAlpha = a * 0.95;
      const w = ctx.measureText(e.text).width + 12;
      const y = H - 78 - i * 12;
      roundRect(ctx, 8, y, w, 11, 3);
      ctx.fillStyle = 'rgba(8,11,16,0.72)';
      ctx.fill();
      ctx.fillStyle = e.color;
      ctx.fillText(e.text, 14, y + 3);
    }
    ctx.globalAlpha = 1;
  }

  drawStart(ctx) {
    ctx.fillStyle = 'rgba(5,7,12,0.82)';
    ctx.fillRect(0, 0, W, H);
    ctx.textAlign = 'center';
    ctx.font = `800 26px ${FONT}`;
    ctx.fillStyle = '#8f1116';
    ctx.fillText('ПЕСОЧНИЦА', W / 2 + 1.5, 74 + 1.5);
    ctx.fillStyle = '#efe3c8';
    ctx.fillText('ПЕСОЧНИЦА', W / 2, 74);
    ctx.font = `600 9px ${FONT}`;
    ctx.fillStyle = '#a85050';
    ctx.fillText('кровавый sandbox · рэгдолл · баллистика · расчленение', W / 2, 90);

    ctx.font = `500 8.5px ${FONT}`;
    ctx.fillStyle = '#cfd6e2';
    const lines = [
      'Ты не персонаж — ты режиссёр этой сцены.',
      '',
      'Q — резать клинком    W — стрелять    E — осмотр данных человека',
      'R — спавн человека    T — тянуть тело    Y — лечить    U — удалить',
      '1–8 — оружие    Space — слоумо    H — помощь    P — пауза',
      '',
      'Курсор режет там, куда ведёшь. Пули летят из-за края экрана точно в прицел.',
      'Кровь, переломы, оторванные конечности и шок считаются в реальном времени.',
    ];
    lines.forEach((l, i) => ctx.fillText(l, W / 2, 122 + i * 13));

    ctx.font = `700 11px ${FONT}`;
    ctx.globalAlpha = 0.65 + Math.sin(this.realTime * 4) * 0.35;
    ctx.fillStyle = '#e6c98a';
    ctx.fillText('КЛИКНИ ИЛИ НАЖМИ ЛЮБУЮ КЛАВИШУ', W / 2, H - 54);
    ctx.globalAlpha = 1;
    ctx.textAlign = 'left';
  }

  drawPause(ctx) {
    ctx.fillStyle = 'rgba(6,8,12,0.55)';
    ctx.fillRect(0, 0, W, H);
    ctx.textAlign = 'center';
    ctx.font = `700 22px ${FONT}`;
    ctx.fillStyle = '#e6d9bd';
    ctx.fillText('ПАУЗА', W / 2, H / 2 - 8);
    ctx.font = `500 9px ${FONT}`;
    ctx.fillStyle = '#aab4c4';
    ctx.fillText('P — продолжить', W / 2, H / 2 + 12);
    ctx.textAlign = 'left';
  }

  drawHelp(ctx) {
    const pw = 300, ph = 196;
    const px = (W - pw) / 2, py = (H - ph) / 2;
    ctx.fillStyle = 'rgba(4,6,10,0.6)';
    ctx.fillRect(0, 0, W, H);
    roundRect(ctx, px, py, pw, ph, 6);
    ctx.fillStyle = 'rgba(14,18,26,0.96)';
    ctx.fill();
    ctx.strokeStyle = 'rgba(190,205,230,0.35)';
    ctx.lineWidth = 0.8;
    ctx.stroke();

    ctx.font = `700 12px ${FONT}`;
    ctx.fillStyle = '#efe3c8';
    ctx.fillText('Как играть', px + 14, py + 14);
    const rows = [
      ['Q', 'Резать', 'проведи курсором по конечности'],
      ['W', 'Стрелять', 'ЛКМ огонь, 1–8 оружие, перезарядка авто'],
      ['E', 'Осмотр', 'наведи на человека — данные справа'],
      ['R', 'Спавн', 'клик по земле, Shift — сразу с оружием'],
      ['T', 'Тянуть', 'тащи тело курсором'],
      ['Y', 'Лечить', 'остановить кровь и залечить раны'],
      ['U', 'Удалить', 'убрать человека'],
      ['Space', 'Слоумо', 'x1 → x0.35 → x0.12'],
      ['P', 'Пауза', 'H — эта справка'],
    ];
    let y = py + 36;
    for (const [k, name, desc] of rows) {
      ctx.font = `700 8px ${FONT}`;
      ctx.fillStyle = '#e6c98a';
      ctx.fillText(k, px + 16, y);
      ctx.font = `600 8px ${FONT}`;
      ctx.fillStyle = '#dfe7f2';
      ctx.fillText(name, px + 48, y);
      ctx.font = `500 8px ${FONT}`;
      ctx.fillStyle = '#9aa6ba';
      ctx.fillText(desc, px + 104, y);
      y += 14;
    }
    ctx.font = `500 7.5px ${FONT}`;
    ctx.fillStyle = 'rgba(160,175,200,0.7)';
    ctx.fillText('Люди умирают от разрушения жизненно важных органов и кровопотери,', px + 16, py + ph - 26);
    ctx.fillText('а не от царапин: раны надо наносить серьёзные.', px + 16, py + ph - 16);
    ctx.font = `600 8px ${FONT}`;
    ctx.fillStyle = '#9aa6ba';
    ctx.fillText('H или клик — закрыть', px + pw - 100, py + 14);
  }
}

/* ------------------------------------------------------------------ */
/*  INPUT                                                              */
/* ------------------------------------------------------------------ */
class Input {
  constructor(canvas) {
    this.keys = new Set();
    this.just = new Set();
    this.wheel = 0;
    this.mouse = { x: W * 0.55, y: H * 0.55, down: false, clicked: false, rdown: false, inside: false };
    const map = (e) => {
      const r = canvas.getBoundingClientRect();
      this.mouse.x = clamp(((e.clientX - r.left) / r.width) * W, 0, W);
      this.mouse.y = clamp(((e.clientY - r.top) / r.height) * H, 0, H);
      this.mouse.inside = true;
    };
    window.addEventListener('mousemove', map);
    window.addEventListener('mouseleave', () => { this.mouse.inside = false; });
    canvas.addEventListener('mousedown', (e) => {
      map(e);
      if (e.button === 0) { this.mouse.down = true; this.mouse.clicked = true; }
      if (e.button === 2) this.mouse.rdown = true;
    });
    window.addEventListener('mouseup', (e) => {
      if (e.button === 0) this.mouse.down = false;
      if (e.button === 2) this.mouse.rdown = false;
    });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      this.wheel = (this.wheel || 0) + Math.sign(e.deltaY);
    }, { passive: false });
    window.addEventListener('keydown', (e) => {
      if (!this.keys.has(e.code)) this.just.add(e.code);
      this.keys.add(e.code);
      if (['Space', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.code)) e.preventDefault();
    });
    window.addEventListener('keyup', (e) => this.keys.delete(e.code));
    window.addEventListener('blur', () => { this.keys.clear(); this.mouse.down = false; });
  }
  down(code) { return this.keys.has(code); }
  pressed(code) { return this.just ? this.just.has(code) : false; }
  endFrame() {
    this.mouse.clicked = false;
    this.just.clear();
    this.wheel = 0;
  }
}

/* ------------------------------------------------------------------ */
// boot
const game = new Game();
game.installInput();
window.__game = game;

// wheel-select weapons
const origUpdate = game.update.bind(game);
game.update = (dt, dtReal, uiClick) => {
  const wheel = game.input.wheel;
  if (wheel) {
    const i = WEAPONS.findIndex((w) => w.id === game.weaponId);
    const next = WEAPONS[(i + (wheel > 0 ? 1 : -1) + WEAPONS.length) % WEAPONS.length];
    game.selectWeapon(next);
  }
  // update() runs while paused? keep this defensive
  origUpdate(dt, dtReal, uiClick);
  game.input.wheel = 0;
};
game.input.wheel = 0;

// hover-over-UI guard: rebuild rects each frame and test here
Object.defineProperty(game, 'hoveredOverUI', {
  get() {
    const c = game.cursor;
    for (const r of game.hudRects) {
      if (c.x >= r.x && c.x <= r.x + r.w && c.y >= r.y && c.y <= r.y + r.h) return true;
    }
    return false;
  },
});
