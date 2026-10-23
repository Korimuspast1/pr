import { VIEW_W, VIEW_H, GROUND_Y, TAU, clamp, rand, chance, pick, hash01, dist } from './util.js';
import { AudioSys } from './audio.js';
import { Gore } from './gore.js';
import { Combat, WEAPONS, WEAPON_BY_ID, BAR, drawWeapon } from './weapons.js';
import { Person } from './player.js';

/* ------------------------------------------------------------------ */
/*  INPUT                                                              */
/* ------------------------------------------------------------------ */
class Input {
  constructor(canvas) {
    this.keys = new Set();
    this.just = new Set();
    this.mouse = { x: VIEW_W * 0.7, y: VIEW_H * 0.5, down: false, clicked: false, wheel: 0 };

    window.addEventListener('keydown', (e) => {
      if (!this.keys.has(e.code)) this.just.add(e.code);
      this.keys.add(e.code);
      if (['Space', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Tab', 'KeyR'].includes(e.code)) e.preventDefault();
    });
    window.addEventListener('keyup', (e) => this.keys.delete(e.code));
    window.addEventListener('blur', () => { this.keys.clear(); this.mouse.down = false; });

    const map = (e) => {
      const r = canvas.getBoundingClientRect();
      this.mouse.x = clamp(((e.clientX - r.left) / r.width) * VIEW_W, 0, VIEW_W);
      this.mouse.y = clamp(((e.clientY - r.top) / r.height) * VIEW_H, 0, VIEW_H);
    };
    window.addEventListener('mousemove', map);
    canvas.addEventListener('mousedown', (e) => {
      map(e);
      this.mouse.down = true;
      this.mouse.clicked = true;
      if (e.button === 1 || e.button === 2) this.mouse.down = false;
    });
    window.addEventListener('mouseup', () => { this.mouse.down = false; });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      this.mouse.wheel += Math.sign(e.deltaY);
    }, { passive: false });
  }
  down(c) { return this.keys.has(c); }
  pressed(c) { return this.just.has(c); }
  endFrame() {
    this.just.clear();
    this.mouse.clicked = false;
    this.mouse.wheel = 0;
  }
}

/* ------------------------------------------------------------------ */
/*  GAME                                                               */
/* ------------------------------------------------------------------ */
class Game {
  constructor() {
    this.canvas = document.getElementById('game');
    this.ctx = this.canvas.getContext('2d');
    this.ctx.imageSmoothingEnabled = false;
    this.canvas.width = VIEW_W;
    this.canvas.height = VIEW_H;

    this.audio = new AudioSys();
    this.gore = new Gore(this.audio);
    this.combat = new Combat(this);
    this.input = new Input(this.canvas);
    this.mouse = this.input.mouse;

    this.entities = [];
    this.debris = [];
    this.log = [];
    this.time = 0;
    this.realTime = 0;
    this.paused = false;
    this.started = false;
    this.muted = false;
    this.shakeAmt = 0;
    this.damageFlash = 0;
    this.hitstopT = 0;
    this.timeScale = 1;
    this.barIndex = 4;
    this.npcRespawn = 0;
    this.player = new Person(this, { x: 230, isPlayer: true, weapon: 'pistol', name: 'Вы' });
    this.player.isNPC = false;
    this.entities.push(this.player);
    this.spawnNPC(455, 'machete');
    this.spawnNPC(520, 'smg');
    this.spawnNPC(150, 'axe');

    this.buildBackground();
    this.bindUI();
    this.last = performance.now();
    requestAnimationFrame(this.loop.bind(this));
  }

  bindUI() {
    document.getElementById('loading')?.remove();
    window.addEventListener('resize', () => this.resize());
    this.resize();
  }

  resize() {
    const wrap = document.getElementById('wrap');
    if (!wrap) return;
    const pad = 24;
    const aw = window.innerWidth - pad;
    const ah = window.innerHeight - pad;
    const scale = Math.max(1, Math.min(aw / VIEW_W, ah / (VIEW_H + 8)));
    this.canvas.style.width = Math.floor(VIEW_W * scale) + 'px';
    this.canvas.style.height = Math.floor(VIEW_H * scale) + 'px';
  }

  spawnNPC(x, weapon) {
    const names = ['Марк', 'Таня', 'Кирилл', 'Лена', 'Гоша', 'Рита', 'Дима', 'Соня', 'Витя', 'Юля'];
    const p = new Person(this, {
      x, weapon: weapon || pick(['knife', 'machete', 'axe', 'katana', 'pistol', 'smg', 'shotgun', 'rifle']),
      name: pick(names),
    });
    p.isNPC = true;
    p.ai.retarget = rand(0.5, 3);
    this.entities.push(p);
    return p;
  }

  /* ---------------- world art ---------------- */

  buildBackground() {
    const c = document.createElement('canvas');
    c.width = VIEW_W; c.height = VIEW_H;
    const g = c.getContext('2d');
    this.bgCanvas = c;

    // sky
    const sky = g.createLinearGradient(0, 0, 0, GROUND_Y);
    sky.addColorStop(0, '#1b1f2b');
    sky.addColorStop(0.55, '#333a49');
    sky.addColorStop(1, '#5c5b5e');
    g.fillStyle = sky;
    g.fillRect(0, 0, VIEW_W, VIEW_H);

    // pale sun
    g.fillStyle = 'rgba(226,205,168,0.20)';
    g.beginPath(); g.arc(505, 84, 30, 0, TAU); g.fill();
    g.fillStyle = 'rgba(240,226,196,0.75)';
    g.beginPath(); g.arc(505, 84, 15, 0, TAU); g.fill();

    // stars
    for (let i = 0; i < 90; i++) {
      const x = hash01(i * 3.7) * VIEW_W;
      const y = hash01(i * 9.1) * 190;
      const a = 0.15 + hash01(i * 5.3) * 0.5;
      g.fillStyle = `rgba(230,232,240,${a.toFixed(2)})`;
      g.fillRect(x | 0, y | 0, 1, 1);
    }

    // far hills
    g.fillStyle = '#252b36';
    g.beginPath();
    g.moveTo(0, 250);
    for (let x = 0; x <= VIEW_W; x += 8) {
      const y = 236 - Math.sin(x * 0.008) * 22 - Math.sin(x * 0.021 + 2) * 12;
      g.lineTo(x, y);
    }
    g.lineTo(VIEW_W, GROUND_Y); g.lineTo(0, GROUND_Y); g.closePath(); g.fill();

    // ruined skyline
    g.fillStyle = '#1c212a';
    let bx = 0;
    let i = 0;
    while (bx < VIEW_W) {
      const w = 12 + Math.floor(hash01(i * 4.1) * 26);
      const h = 18 + Math.floor(hash01(i * 7.3) * 52);
      g.fillRect(bx, GROUND_Y - 34 - h, w, h + 34);
      // broken top
      if (hash01(i * 2.2) > 0.5) g.fillRect(bx + 3, GROUND_Y - 39 - h, Math.max(3, w - 8), 6);
      // windows
      g.fillStyle = 'rgba(240,190,110,0.10)';
      for (let wy = GROUND_Y - 28 - h; wy < GROUND_Y - 12; wy += 7) {
        for (let wx = bx + 3; wx < bx + w - 3; wx += 6) {
          if (hash01(wx * 1.3 + wy * 7.7) > 0.72) g.fillRect(wx, wy, 2, 3);
        }
      }
      g.fillStyle = '#1c212a';
      bx += w + 3 + Math.floor(hash01(i * 9.9) * 8);
      i++;
    }

    // haze
    const haze = g.createLinearGradient(0, GROUND_Y - 80, 0, GROUND_Y);
    haze.addColorStop(0, 'rgba(120,110,110,0)');
    haze.addColorStop(1, 'rgba(150,130,120,0.22)');
    g.fillStyle = haze;
    g.fillRect(0, GROUND_Y - 80, VIEW_W, 80);

    // ground
    g.fillStyle = '#3c3a34';
    g.fillRect(0, GROUND_Y, VIEW_W, VIEW_H - GROUND_Y);
    g.fillStyle = '#4a463d';
    g.fillRect(0, GROUND_Y, VIEW_W, 3);
    g.fillStyle = '#33312b';
    g.fillRect(0, GROUND_Y + 3, VIEW_W, 2);

    // dirt speckles / pebbles / grass
    for (let n = 0; n < 900; n++) {
      const x = hash01(n * 1.77) * VIEW_W;
      const y = GROUND_Y + 2 + hash01(n * 3.31) * (VIEW_H - GROUND_Y);
      const s = hash01(n * 5.9);
      if (s > 0.75) {
        g.fillStyle = s > 0.93 ? '#575247' : '#2c2a25';
        g.fillRect(x | 0, y | 0, 1, 1);
      } else if (s > 0.62) {
        g.fillStyle = '#5b5647';
        g.fillRect(x | 0, y | 0, 2, 1);
      }
    }
    for (let n = 0; n < 90; n++) {
      const x = hash01(n * 13.3) * VIEW_W;
      const h = 2 + hash01(n * 4.4) * 4;
      g.strokeStyle = '#3f4a33';
      g.lineWidth = 1;
      g.beginPath();
      g.moveTo(x | 0, GROUND_Y + 1);
      g.lineTo((x + rand(-1.5, 1.5)) | 0, (GROUND_Y - h) | 0);
      g.stroke();
    }

    // foreground vignette-ish border
    g.fillStyle = 'rgba(10,10,14,0.35)';
    g.fillRect(0, VIEW_H - 6, VIEW_W, 6);
  }

  /* ---------------- events ---------------- */

  selectBar(i) {
    const id = BAR[i];
    if (!id) return;
    this.barIndex = i;
    if (this.player.alive) {
      this.player.setWeapon(id);
      this.audio.noise({ dur: 0.04, gain: 0.08, freq: 2200, freqEnd: 900 });
    }
  }

  addLog(text, color = '#cfc4ad') {
    const prev = this.log[0];
    if (prev && prev.text === text) { prev.t = Math.min(prev.t, 4); return; }
    this.log.unshift({ text, t: 5, color });
    if (this.log.length > 5) this.log.pop();
  }

  hitmarker(x, y) {
    this.combat.hitmarkers.push({ x, y, t: 0.3 });
    this.audio.hitmark();
  }

  shake(n) { this.shakeAmt = Math.min(14, Math.max(this.shakeAmt, n)); }

  hitstop(t) { this.hitstopT = Math.max(this.hitstopT, t); }

  onPlayerDeath() {
    this.playerDeathAt = this.time;
  }

  respawnPlayer() {
    const p = this.player;
    p.reset(210, this.weaponId() || 'pistol');
    p.isNPC = false;
    p.isPlayer = true;
    this.combat.clear();
  }

  weaponId() { return BAR[this.barIndex]; }

  /* ---------------- main loop ---------------- */

  loop(now) {
    const raf = requestAnimationFrame.bind(window);
    let dtReal = (now - this.last) / 1000;
    this.last = now;
    if (!isFinite(dtReal)) dtReal = 0.016;
    dtReal = Math.min(dtReal, 0.05);
    this.realTime += dtReal;

    const inp = this.input;
    if (inp.pressed('KeyP')) this.paused = !this.paused;
    if (inp.pressed('KeyT')) {
      this.timeScale = this.timeScale === 1 ? 0.35 : this.timeScale === 0.35 ? 0.12 : 1;
    }
    if (inp.pressed('KeyM')) {
      this.muted = !this.muted;
      this.audio.setMuted(this.muted);
    }
    if (inp.pressed('KeyG')) this.gore.clear();
    if (inp.pressed('KeyF')) {
      const alive = this.entities.filter((e) => e !== this.player && e.alive).length;
      if (alive < 4) this.spawnNPC(rand(80, VIEW_W - 80));
    }
    if (!this.started) {
      if (inp.mouse.clicked || inp.pressed('Enter')) {
        this.started = true;
        this.audio.ensure();
      }
    }

    if (!this.paused && this.started) {
      let dt = dtReal;
      if (this.hitstopT > 0) {
        this.hitstopT -= dtReal;
        dt *= 0.25;
      }
      this.update(dt * this.timeScale);
    }
    this.render();
    inp.endFrame();
    raf(this.loop.bind(this));
  }

  update(dt) {
    this.time += dt;

    // Respawn NPCs to keep the sandbox populated.
    const aliveNPCs = this.entities.filter((e) => e !== this.player && e.alive).length;
    if (aliveNPCs < 3) {
      this.npcRespawn -= dt;
      if (this.npcRespawn <= 0) {
        this.npcRespawn = rand(4, 8);
        const dead = this.entities.find((e) => e !== this.player && !e.alive && e.deadTime > 12);
        if (dead) {
          this.entities.splice(this.entities.indexOf(dead), 1);
        }
        if (aliveNPCs < 3) this.spawnNPC(chance(0.5) ? rand(60, 160) : rand(480, 580));
      }
    }

    for (const e of this.entities) e.update(dt, this);
    this.combat.update(dt, this);
    this.gore.update(dt);
    this.updateDebris(dt);

    for (let i = this.log.length - 1; i >= 0; i--) {
      this.log[i].t -= dt;
      if (this.log[i].t <= 0) this.log.splice(i, 1);
    }

    this.shakeAmt *= Math.exp(-6 * dt);
    if (this.shakeAmt < 0.05) this.shakeAmt = 0;
    this.damageFlash *= Math.exp(-3.2 * dt);

    // Respawn handled by key while dead
    if (!this.player.alive && this.input.pressed('KeyR')) this.respawnPlayer();

    // mouse wheel weapon select
    if (this.mouse.wheel) {
      this.barIndex = (this.barIndex + this.mouse.wheel + BAR.length) % BAR.length;
      this.selectBar(this.barIndex);
    }
  }

  updateDebris(dt) {
    for (let i = this.debris.length - 1; i >= 0; i--) {
      const d = this.debris[i];
      d.life -= dt;
      if (d.life <= 0) { this.debris.splice(i, 1); continue; }

      if (d.type === 'weapon') {
        d.vy += 900 * dt;
        d.x += d.vx * dt;
        d.y += d.vy * dt;
        d.rot += d.vr * dt;
        if (d.y > GROUND_Y - 1) {
          d.y = GROUND_Y - 1;
          d.vy *= -0.28;
          d.vx *= 0.7;
          d.vr *= 0.55;
          if (Math.abs(d.vr) < 0.5) d.vr = 0;
        }
        continue;
      }

      const G = 900;
      for (const key of ['a', 'b']) {
        const p = d[key];
        p.vy += G * dt;
        p.vx *= 1 - 1.2 * dt;
        p.x += p.vx * dt;
        p.y += p.vy * dt;
      }
      for (let it = 0; it < 2; it++) {
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
            if (p.vy > 0) p.vy *= -0.25;
            p.vx *= 0.8;
          }
          if (p.x < 4) { p.x = 4; p.vx *= -0.4; }
          if (p.x > VIEW_W - 4) { p.x = VIEW_W - 4; p.vx *= -0.4; }
        }
      }
      d.rot = Math.atan2(d.b.y - d.a.y, d.b.x - d.a.x);

      // freshly severed chunks spurt briefly
      if (d.life > 57 && chance(dt * 9)) {
        this.gore.spray(d.a.x, d.a.y, rand(-1, 1), rand(-1, 0), {
          count: 3, speed: 90, size: 1.6, spread: 0.7, mistChance: 0.25,
        });
      }
    }
  }

  /* ---------------- render ---------------- */

  render() {
    const ctx = this.ctx;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.imageSmoothingEnabled = false;
    ctx.clearRect(0, 0, VIEW_W, VIEW_H);

    const shakeX = rand(-this.shakeAmt, this.shakeAmt);
    const shakeY = rand(-this.shakeAmt, this.shakeAmt) * 0.7;
    ctx.save();
    ctx.translate(Math.round(shakeX), Math.round(shakeY));

    ctx.drawImage(this.bgCanvas, 0, 0);

    // blood decals live on the ground
    this.gore.drawDecals(ctx);

    // debris behind bodies
    this.drawDebris(ctx);

    for (const e of this.entities) e.draw(ctx, this);

    this.combat.drawUnder(ctx);
    this.gore.draw(ctx);

    // aim helper
    this.drawAim(ctx);

    this.combat.drawOver(ctx);
    ctx.restore();

    this.gore.drawScreen(ctx);

    if (this.damageFlash > 0.01) {
      ctx.fillStyle = `rgba(120,8,12,${(this.damageFlash * 0.5).toFixed(3)})`;
      ctx.fillRect(0, 0, VIEW_W, VIEW_H);
    }
    // steady red vignette when badly hurt
    const hp = clamp(this.player.health / 100, 0, 1);
    if (hp < 0.55 && this.player.alive) {
      const a = (0.55 - hp) * 0.5;
      const grad = ctx.createRadialGradient(VIEW_W / 2, VIEW_H / 2, 110, VIEW_W / 2, VIEW_H / 2, 340);
      grad.addColorStop(0, 'rgba(90,0,0,0)');
      grad.addColorStop(1, `rgba(90,0,0,${a.toFixed(2)})`);
      ctx.fillStyle = grad;
      ctx.fillRect(0, 0, VIEW_W, VIEW_H);
    }

    if (this.started) {
      this.drawHUD(ctx);
      this.drawLog(ctx);
      this.drawBar(ctx);
    }

    if (this.paused) this.drawOverlay(ctx, 'ПАУЗА', 'P — продолжить', 'pause');
    if (!this.player.alive && this.player.deadTime > 0.9) {
      this.deathOverlayA = Math.min(1, (this.deathOverlayA || 0) + 0.06);
      this.drawOverlay(ctx, 'ВЫ МЕРТВЫ', `Причина: ${this.player.deathCause || 'неизвестно'}`, 'death', this.deathOverlayA);
    } else {
      this.deathOverlayA = 0;
    }
    if (!this.started) this.drawStart(ctx);
  }

  drawAim(ctx) {
    if (!this.started || !this.player.alive) return;
    const p = this.player;
    const m = this.mouse;
    const spread = (p.weapon.kind === 'gun' ? p.weapon.spread + (p.moving ? p.weapon.movingSpread : 0) + p.sprayHeat : 0.02);
    const r = 2 + spread * 55;
    ctx.strokeStyle = 'rgba(240,240,230,0.55)';
    ctx.lineWidth = 1;
    ctx.beginPath(); ctx.arc(m.x, m.y, r, 0, TAU); ctx.stroke();
    ctx.fillStyle = 'rgba(255,255,255,0.85)';
    ctx.fillRect(Math.round(m.x) - 1, Math.round(m.y) - 1, 2, 2);
  }

  drawDebris(ctx) {
    for (const d of this.debris) {
      if (d.type === 'weapon') {
        drawWeapon(ctx, d.def, d.x, d.y, d.rot, 1, 0);
        continue;
      }
      const ang = d.rot;
      const nx = Math.cos(ang + Math.PI / 2), ny = Math.sin(ang + Math.PI / 2);
      // main chunk
      const col = d.kind === 'skin' ? '#c78e6a' : d.pants ? '#333a46' : d.color;
      const steps = Math.max(1, Math.ceil(d.len / 1.5));
      for (let s = 0; s <= steps; s++) {
        const t = s / steps;
        const x = d.a.x + (d.b.x - d.a.x) * t;
        const y = d.a.y + (d.b.y - d.a.y) * t;
        const w = d.radius * (1 - 0.2 * t);
        ctx.fillStyle = '#1a131c';
        ctx.fillRect(Math.round(x - w - 0.7), Math.round(y - w - 0.7), Math.ceil(w * 2 + 1.4), Math.ceil(w * 2 + 1.4));
        ctx.fillStyle = col;
        ctx.fillRect(Math.round(x - w), Math.round(y - w), Math.ceil(w * 2), Math.ceil(w * 2));
      }
      // stump
      ctx.fillStyle = '#6d0609';
      ctx.beginPath(); ctx.arc(d.a.x, d.a.y, d.radius * 0.95, 0, TAU); ctx.fill();
      ctx.fillStyle = '#3a0205';
      ctx.beginPath(); ctx.arc(d.a.x + nx * 0.3, d.a.y + ny * 0.3, d.radius * 0.45, 0, TAU); ctx.fill();
      // bone nub
      ctx.fillStyle = '#e6ddc8';
      ctx.fillRect(Math.round(d.a.x - 0.8), Math.round(d.a.y - 0.8), 1.6, 1.6);
    }
  }

  /* ---------------- HUD ---------------- */

  drawHUD(ctx) {
    const p = this.player;
    ctx.font = 'bold 8px "Courier New", monospace';
    ctx.textBaseline = 'top';

    // panel
    const px = 8, py = 8, pw = 172, ph = 58;
    ctx.fillStyle = 'rgba(12,12,16,0.68)';
    ctx.fillRect(px, py, pw, ph);
    ctx.strokeStyle = 'rgba(200,190,170,0.25)';
    ctx.lineWidth = 1;
    ctx.strokeRect(px + 0.5, py + 0.5, pw - 1, ph - 1);

    // health
    ctx.fillStyle = '#d8cdbb';
    ctx.fillText('ЖИЗНЬ', px + 6, py + 5);
    ctx.fillStyle = '#2a0f12';
    ctx.fillRect(px + 52, py + 6, 112, 5);
    ctx.fillStyle = p.health > 45 ? '#c0272d' : '#8d1418';
    ctx.fillRect(px + 52, py + 6, Math.round(112 * clamp(p.health / 100, 0, 1)), 5);
    ctx.fillStyle = 'rgba(255,255,255,0.15)';
    ctx.fillRect(px + 52, py + 6, Math.round(112 * clamp(p.health / 100, 0, 1)), 1);

    // blood
    ctx.fillStyle = '#d8cdbb';
    ctx.fillText('КРОВЬ', px + 6, py + 16);
    ctx.fillStyle = '#1b0c0e';
    ctx.fillRect(px + 52, py + 17, 112, 5);
    ctx.fillStyle = '#7e0d13';
    ctx.fillRect(px + 52, py + 17, Math.round(112 * clamp(p.blood / 5000, 0, 1)), 5);
    ctx.fillStyle = 'rgba(255,120,120,0.25)';
    ctx.fillRect(px + 52, py + 17, Math.round(112 * clamp(p.blood / 5000, 0, 1)), 1);

    const lostMl = Math.round((5000 - p.blood) / 10) / 100;
    ctx.fillStyle = p.bleedTotal > 1 ? '#e07272' : '#9a9284';
    ctx.fillText(`${p.bleedTotal > 0 ? p.bleedTotal.toFixed(1) : '0'} мл/с   −${(lostMl * 1000).toFixed(0)} мл`, px + 52, py + 26);

    // weapon / ammo
    ctx.fillStyle = '#e8dcc2';
    ctx.fillText(p.weapon.name, px + 6, py + 36);
    if (p.weapon.kind === 'gun') {
      const mag = p.mags[p.weapon.id] | 0;
      ctx.fillStyle = mag > 0 ? '#e8dcc2' : '#c95b5b';
      ctx.fillText(`${mag} / ${p.weapon.magSize}`, px + 118, py + 36);
    }

    // injuries summary
    const severed = p.limbs.filter((l) => l.severed && l.name !== 'head').map((l) => l.label);
    const broken = p.limbs.filter((l) => l.broken && !l.severed).map((l) => l.label);
    ctx.fillStyle = '#b9ab97';
    let line = '';
    if (severed.length) line += 'ОТОРВАНО: ' + severed.join(', ') + '  ';
    if (broken.length) line += 'ПЕРЕЛОМ: ' + broken.join(', ');
    if (!line) line = p.reloading > 0 ? 'ПЕРЕЗАРЯДКА...' : (p.armUsable() ? 'готов' : 'руки повреждены');
    ctx.fillStyle = severed.length || broken.length ? '#d07a6a' : '#8f8677';
    let disp = line;
    if (disp.length > 33) disp = disp.slice(0, 32) + '…';
    ctx.fillText(disp, px + 6, py + 46);

    // top right info
    const aliveCount = this.entities.filter((e) => e.alive).length;
    ctx.fillStyle = 'rgba(12,12,16,0.5)';
    ctx.fillRect(VIEW_W - 128, 8, 120, 20);
    ctx.fillStyle = '#a89e8c';
    ctx.fillText(`живых: ${aliveCount}   трупов: ${this.entities.filter((e) => !e.alive).length}`, VIEW_W - 122, 12);
    ctx.fillStyle = this.muted ? '#8a6a6a' : '#7f9a7f';
    ctx.fillText(this.muted ? 'звук: выкл (M)' : 'звук: вкл (M)', VIEW_W - 122, 20);
    if (this.timeScale !== 1) {
      ctx.fillStyle = '#8aa6c8';
      ctx.fillText('слоумо x' + this.timeScale, VIEW_W - 122, 28);
    }

    // controls hint (bottom left, fades)
    ctx.fillStyle = 'rgba(210,200,182,0.45)';
    ctx.font = 'bold 7px "Courier New", monospace';
    ctx.fillText('A/D — идти · ЛКМ — атака · R — перезарядка · 1–8 — оружие', 8, VIEW_H - 15);
    ctx.fillText('F — житель · G — кровь · M — звук · T — слоумо · P — пауза', 8, VIEW_H - 7);
  }

  drawBar(ctx) {
    const n = BAR.length;
    const slotW = 28, slotH = 24, gap = 3;
    const totalW = n * slotW + (n - 1) * gap;
    const x0 = Math.round((VIEW_W - totalW) / 2);
    const y0 = VIEW_H - slotH - 24;

    for (let i = 0; i < n; i++) {
      const def = WEAPON_BY_ID[BAR[i]];
      const x = x0 + i * (slotW + gap);
      const active = i === this.barIndex;
      ctx.fillStyle = active ? 'rgba(60,52,40,0.92)' : 'rgba(16,16,20,0.72)';
      ctx.fillRect(x, y0, slotW, slotH);
      ctx.strokeStyle = active ? 'rgba(226,196,120,0.9)' : 'rgba(140,130,115,0.25)';
      ctx.strokeRect(x + 0.5, y0 + 0.5, slotW - 1, slotH - 1);

      // mini weapon
      ctx.save();
      ctx.translate(x + 3, y0 + 6);
      ctx.scale(0.62, 0.62);
      const g = def.grip || { x: 2, y: 2 };
      ctx.translate(4 - g.x, 10 - g.y);
      drawWeapon(ctx, def, 0, 0, 0, 1, 0);
      ctx.restore();

      ctx.fillStyle = active ? '#f0dfae' : '#8a8272';
      ctx.font = 'bold 7px "Courier New", monospace';
      ctx.fillText(String(i + 1), x + 2, y0 + slotH - 9);
      if (def.kind === 'gun' && active) {
        ctx.fillStyle = '#c9b98a';
        ctx.fillText(String(this.player.mags[def.id] | 0), x + slotW - 10, y0 + slotH - 9);
      }
    }
  }

  drawOverlay(ctx, title, sub, kind, alpha = 1) {
    const bg = kind === 'death' ? 0.34 : 0.6;
    ctx.fillStyle = `rgba(6,4,8,${(bg * alpha).toFixed(3)})`;
    ctx.fillRect(0, 0, VIEW_W, VIEW_H);
    ctx.globalAlpha = alpha;
    ctx.textAlign = 'center';
    ctx.fillStyle = kind === 'death' ? '#c33a3a' : '#e6d9bd';
    ctx.font = 'bold 24px "Courier New", monospace';
    ctx.fillText(title, VIEW_W / 2, VIEW_H / 2 - 30);
    ctx.fillStyle = '#cfc3a9';
    ctx.font = 'bold 10px "Courier New", monospace';
    ctx.fillText(sub, VIEW_W / 2, VIEW_H / 2);
    ctx.fillStyle = 'rgba(220,210,190,0.7)';
    ctx.font = 'bold 9px "Courier New", monospace';
    const hint = this.paused ? 'P — вернуться в игру' : 'R — возродиться';
    ctx.fillText(hint, VIEW_W / 2, VIEW_H / 2 + 20);
    ctx.globalAlpha = 1;
    ctx.textAlign = 'left';
  }

  drawLog(ctx) {
    ctx.font = 'bold 8px "Courier New", monospace';
    ctx.textAlign = 'right';
    for (let i = 0; i < this.log.length; i++) {
      const e = this.log[i];
      const a = clamp(e.t, 0, 1) * 0.9;
      ctx.globalAlpha = a;
      ctx.fillStyle = 'rgba(10,9,12,0.55)';
      const w = ctx.measureText ? Math.max(40, ctx.measureText(e.text).width + 8) : 120;
      ctx.fillRect(VIEW_W - 8 - w, 42 + i * 11, w, 10);
      ctx.fillStyle = e.color;
      ctx.fillText(e.text, VIEW_W - 12, 44 + i * 11);
    }
    ctx.globalAlpha = 1;
    ctx.textAlign = 'left';
  }

  drawStart(ctx) {
    ctx.fillStyle = 'rgba(6,5,10,0.78)';
    ctx.fillRect(0, 0, VIEW_W, VIEW_H);
    ctx.textAlign = 'center';
    // title
    ctx.font = 'bold 30px "Courier New", monospace';
    ctx.fillStyle = '#8f1116';
    ctx.fillText('ПЕСОЧНИЦА', VIEW_W / 2 + 2, 54);
    ctx.fillStyle = '#e7dcc2';
    ctx.fillText('ПЕСОЧНИЦА', VIEW_W / 2, 52);
    ctx.font = 'bold 9px "Courier New", monospace';
    ctx.fillStyle = '#a33a3a';
    ctx.fillText('BLOOD SANDBOX · расчленение и баллистика', VIEW_W / 2, 72);

    ctx.font = 'bold 9px "Courier New", monospace';
    ctx.fillStyle = '#cfc4ad';
    const lines = [
      'A / D — движение    ПРОБЕЛ — рывок / встать    Shift — бег',
      'ЛКМ — удар/выстрел   R — перезарядка   1–8 / колесо — оружие',
      'F — житель   G — стереть кровь   T — слоумо   M — звук   P — пауза',
      '',
      'Клинок режет плоть: конечности отрываются, артерии фонтан.',
      'Дробовик и СВД пробивают насквозь. Кровь остаётся на земле.',
    ];
    lines.forEach((l, i) => ctx.fillText(l, VIEW_W / 2, 96 + i * 13));

    ctx.fillStyle = '#e0c887';
    ctx.font = 'bold 12px "Courier New", monospace';
    const blink = 0.6 + Math.sin(this.realTime * 4) * 0.4;
    ctx.globalAlpha = blink;
    ctx.fillText('КЛИКНИ, ЧТОБЫ НАЧАТЬ', VIEW_W / 2, VIEW_H - 58);
    ctx.globalAlpha = 1;
    ctx.textAlign = 'left';
    ctx.font = 'bold 8px "Courier New", monospace';
    ctx.fillStyle = 'rgba(200,190,170,0.4)';
    ctx.textAlign = 'center';
    ctx.fillText('кровь, конечности и раны симулируются в реальном времени', VIEW_W / 2, VIEW_H - 38);
    ctx.textAlign = 'left';
  }
}

window.addEventListener('DOMContentLoaded', () => {
  window.__game = new Game();
});
