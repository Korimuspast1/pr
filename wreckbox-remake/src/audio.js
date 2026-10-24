// Procedural sound: everything is synthesised with WebAudio, no assets needed.
export class AudioEngine {
  constructor() {
    this.ctx = null;
    this.ready = false;
    this.vol = { master: 0.8, explosions: 0.9, ambient: 0.5, alerts: 0.7 };
    this.recording = false;
    this.lastPlayed = new Map();
    this.ambientOn = false;
    this.geigerTimer = 0;
  }

  init() {
    if (this.ctx) return;
    const Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return;
    const ctx = (this.ctx = new Ctx());
    this.master = ctx.createGain();
    this.master.gain.value = this.vol.master;
    this.comp = ctx.createDynamicsCompressor();
    this.comp.threshold.value = -14;
    this.comp.ratio.value = 6;
    this.master.connect(this.comp).connect(ctx.destination);

    this.busExpl = ctx.createGain();
    this.busAmb = ctx.createGain();
    this.busAlert = ctx.createGain();
    this.busExpl.gain.value = this.vol.explosions;
    this.busAmb.gain.value = this.vol.ambient;
    this.busAlert.gain.value = this.vol.alerts;
    this.busExpl.connect(this.master);
    this.busAmb.connect(this.master);
    this.busAlert.connect(this.master);

    // reusable noise buffer
    const len = ctx.sampleRate * 2;
    const buf = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    this.noiseBuf = buf;

    this.startAmbient();
    this.ready = true;
  }

  setVolume(kind, v) {
    this.vol[kind] = v;
    if (!this.ready) return;
    const map = { master: this.master, explosions: this.busExpl, ambient: this.busAmb, alerts: this.busAlert };
    map[kind].gain.setTargetAtTime(v, this.ctx.currentTime, 0.05);
  }

  noise(dur = 0.4, gain = 0.5, filterType = 'lowpass', f0 = 1200, f1 = 120, bus = this.busExpl) {
    if (!this.ready) return;
    const ctx = this.ctx;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuf;
    src.loop = true;
    src.playbackRate.value = 0.7 + Math.random() * 0.6;
    const flt = ctx.createBiquadFilter();
    flt.type = filterType;
    flt.frequency.setValueAtTime(f0, ctx.currentTime);
    flt.frequency.exponentialRampToValueAtTime(Math.max(30, f1), ctx.currentTime + dur);
    const g = ctx.createGain();
    const peak = gain * (this.recording ? 0.55 : 1);
    g.gain.setValueAtTime(0.0001, ctx.currentTime);
    g.gain.exponentialRampToValueAtTime(Math.max(0.0002, peak), ctx.currentTime + 0.012);
    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + dur);
    src.connect(flt).connect(g).connect(bus || this.master);
    src.start();
    src.stop(ctx.currentTime + dur + 0.05);
  }

  tone(freq, dur, gain = 0.2, type = 'sine', bus = this.busAlert, slideTo = null) {
    if (!this.ready) return;
    const ctx = this.ctx;
    const o = ctx.createOscillator();
    o.type = type;
    o.frequency.setValueAtTime(freq, ctx.currentTime);
    if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, ctx.currentTime + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, ctx.currentTime);
    g.gain.exponentialRampToValueAtTime(gain, ctx.currentTime + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + dur);
    o.connect(g).connect(bus || this.master);
    o.start();
    o.stop(ctx.currentTime + dur + 0.02);
  }

  throttle(key, ms) {
    const now = performance.now();
    const last = this.lastPlayed.get(key) || 0;
    if (now - last < ms) return false;
    this.lastPlayed.set(key, now);
    return true;
  }

  // ------------------------------------------------------------- material hits
  hit(material, strength = 1) {
    if (!this.ready) return;
    switch (material) {
      case 'glass':
        if (!this.throttle('glass', 45)) return;
        this.noise(0.18, 0.22 * strength, 'highpass', 5200, 2600);
        this.tone(2200 + Math.random() * 1400, 0.12, 0.05, 'triangle');
        break;
      case 'metal':
        if (!this.throttle('metal', 45)) return;
        this.noise(0.12, 0.16 * strength, 'bandpass', 2400, 900);
        this.tone(320 + Math.random() * 120, 0.28, 0.06, 'square', this.busExpl, 180);
        break;
      case 'brick':
        if (!this.throttle('brick', 35)) return;
        this.noise(0.2, 0.3 * strength, 'lowpass', 1600, 320);
        break;
      case 'concrete':
      case 'panel':
        if (!this.throttle('concrete', 40)) return;
        this.noise(0.26, 0.34 * strength, 'lowpass', 1100, 200);
        break;
      case 'wood':
        if (!this.throttle('wood', 40)) return;
        this.noise(0.13, 0.22 * strength, 'bandpass', 900, 500);
        break;
      case 'dirt':
      case 'sand':
        if (!this.throttle('dirt', 60)) return;
        this.noise(0.22, 0.2 * strength, 'lowpass', 600, 120);
        break;
      case 'rubble':
        if (!this.throttle('rubble', 50)) return;
        this.noise(0.18, 0.16 * strength, 'lowpass', 900, 200);
        break;
      default:
        if (!this.throttle('stone', 45)) return;
        this.noise(0.2, 0.22 * strength, 'lowpass', 900, 160);
    }
  }

  collapse(scale = 1) {
    if (!this.ready) return;
    if (!this.throttle('collapse', 220)) return;
    this.noise(0.9 * Math.min(2, scale), 0.35 * Math.min(1.4, scale), 'lowpass', 700, 70);
    this.tone(58, 0.9, 0.16, 'sine', this.busExpl, 32);
  }

  explosion(power = 1, type = 'normal') {
    if (!this.ready) return;
    const ctx = this.ctx;
    const p = Math.min(3, Math.max(0.4, power));
    this.noise(0.9 * p, 0.7, 'lowpass', 2400, 60);
    const o = ctx.createOscillator();
    o.type = 'sine';
    o.frequency.setValueAtTime(120 * (1 / p), ctx.currentTime);
    o.frequency.exponentialRampToValueAtTime(28, ctx.currentTime + 1.1 * p);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, ctx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.55, ctx.currentTime + 0.02);
    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 1.2 * p);
    o.connect(g).connect(this.busExpl);
    o.start();
    o.stop(ctx.currentTime + 1.3 * p);
    if (type === 'nuke') {
      this.noise(4.2, 0.75, 'lowpass', 500, 40);
      this.tone(40, 3.6, 0.4, 'sine', this.busExpl, 18);
      setTimeout(() => this.noise(5, 0.4, 'lowpass', 300, 30), 700);
    }
    if (type === 'ammo') {
      setTimeout(() => this.noise(0.4, 0.3, 'bandpass', 1800, 400), 120);
    }
  }

  quake() {
    if (!this.ready) return;
    this.noise(3.6, 0.5, 'lowpass', 240, 40);
    this.tone(30, 3.4, 0.3, 'sine', this.busExpl, 22);
  }

  rain(seconds = 5) {
    if (!this.ready) return;
    this.noise(seconds, 0.22, 'highpass', 3800, 1400, this.busAmb);
  }

  siren() {
    if (!this.ready) return;
    const t = this.ctx.currentTime;
    for (let i = 0; i < 3; i++) {
      const o = this.ctx.createOscillator();
      const g = this.ctx.createGain();
      o.type = 'sawtooth';
      o.frequency.setValueAtTime(420, t + i * 0.55);
      o.frequency.linearRampToValueAtTime(760, t + i * 0.55 + 0.28);
      o.frequency.linearRampToValueAtTime(420, t + i * 0.55 + 0.55);
      g.gain.setValueAtTime(0.0001, t + i * 0.55);
      g.gain.linearRampToValueAtTime(0.12, t + i * 0.55 + 0.05);
      g.gain.linearRampToValueAtTime(0.0001, t + i * 0.55 + 0.55);
      o.connect(g).connect(this.busAlert);
      o.start(t + i * 0.55);
      o.stop(t + i * 0.55 + 0.6);
    }
  }

  alarm() {
    if (!this.ready) return;
    this.tone(880, 0.16, 0.08, 'square', this.busAlert, 660);
    setTimeout(() => this.tone(880, 0.16, 0.08, 'square', this.busAlert, 660), 200);
  }

  click(up = false) {
    if (!this.ready) return;
    this.tone(up ? 1250 : 900, 0.05, 0.06, 'triangle', this.master);
  }

  geiger(intensity = 1) {
    if (!this.ready) return;
    const now = performance.now();
    if (now - this.geigerTimer < 40 + Math.random() * 260 / Math.max(0.2, intensity)) return;
    this.geigerTimer = now;
    this.noise(0.02, 0.05 + Math.random() * 0.05, 'highpass', 4200, 3200, this.busAlert);
  }

  startAmbient() {
    if (this.ambientOn || !this.ctx) return;
    this.ambientOn = true;
    const ctx = this.ctx;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuf;
    src.loop = true;
    src.playbackRate.value = 0.25;
    const flt = ctx.createBiquadFilter();
    flt.type = 'lowpass';
    flt.frequency.value = 420;
    const g = ctx.createGain();
    g.gain.value = 0.05;
    src.connect(flt).connect(g).connect(this.busAmb);
    src.start();
    this.ambientSrc = { src, g };
  }

  setMenuMusic() {}
}
