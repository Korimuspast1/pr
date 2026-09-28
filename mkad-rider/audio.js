// ============================================================
//  Звук: двигатель (2 осциллятора + фильтр), ветер, авария
// ============================================================
export class GameAudio {
  constructor() {
    this.ok = false;
    this.muted = false;
  }

  init() {
    if (this.ok) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    const ctx = this.ctx = new AC();

    this.master = ctx.createGain();
    this.master.gain.value = 0.5;
    this.master.connect(ctx.destination);

    // --- двигатель ---
    this.engGain = ctx.createGain();
    this.engGain.gain.value = 0;

    // мягкий сатуратор
    const shaper = ctx.createWaveShaper();
    const n = 256, curve = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      const x = (i / (n - 1)) * 2 - 1;
      curve[i] = Math.tanh(x * 2.2) * 0.8;
    }
    shaper.curve = curve;

    this.engFilter = ctx.createBiquadFilter();
    this.engFilter.type = 'lowpass';
    this.engFilter.frequency.value = 400;
    this.engFilter.Q.value = 1.2;

    this.osc1 = ctx.createOscillator();
    this.osc1.type = 'sawtooth';
    this.osc2 = ctx.createOscillator();
    this.osc2.type = 'square';
    const g2 = ctx.createGain(); g2.gain.value = 0.45;
    this.osc1.connect(shaper);
    this.osc2.connect(g2); g2.connect(shaper);
    shaper.connect(this.engFilter);
    this.engFilter.connect(this.engGain);
    this.engGain.connect(this.master);
    this.osc1.start(); this.osc2.start();

    // --- ветер ---
    const len = ctx.sampleRate * 2;
    const buf = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    this.noiseBuf = buf;

    this.windSrc = ctx.createBufferSource();
    this.windSrc.buffer = buf;
    this.windSrc.loop = true;
    this.windFilter = ctx.createBiquadFilter();
    this.windFilter.type = 'bandpass';
    this.windFilter.frequency.value = 600;
    this.windFilter.Q.value = 0.5;
    this.windGain = ctx.createGain();
    this.windGain.gain.value = 0;
    this.windSrc.connect(this.windFilter);
    this.windFilter.connect(this.windGain);
    this.windGain.connect(this.master);
    this.windSrc.start();

    this.ok = true;
  }

  resume() { if (this.ok && this.ctx.state === 'suspended') this.ctx.resume(); }

  update(rpm, throttle, v) {
    if (!this.ok || this.muted) return;
    const t = this.ctx.currentTime;
    const freq = 42 + rpm * 210;
    this.osc1.frequency.setTargetAtTime(freq, t, 0.03);
    this.osc2.frequency.setTargetAtTime(freq * 0.502, t, 0.03);
    this.engFilter.frequency.setTargetAtTime(240 + rpm * 2100 + throttle * 500, t, 0.05);
    this.engGain.gain.setTargetAtTime(0.045 + throttle * 0.13 + rpm * 0.05, t, 0.08);
    const w = Math.min(v / 60, 1);
    this.windGain.gain.setTargetAtTime(w * w * w * 0.5, t, 0.15);
    this.windFilter.frequency.setTargetAtTime(420 + v * 16, t, 0.2);
  }

  crash() {
    if (!this.ok || this.muted) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuf;
    const f = ctx.createBiquadFilter();
    f.type = 'lowpass'; f.frequency.value = 900;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.9, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.8);
    src.connect(f); f.connect(g); g.connect(this.master);
    src.start(t); src.stop(t + 0.85);
    // двигатель глохнет
    this.engGain.gain.setTargetAtTime(0, t, 0.05);
    this.windGain.gain.setTargetAtTime(0, t, 0.1);
  }

  idle() {
    if (!this.ok) return;
    const t = this.ctx.currentTime;
    this.engGain.gain.setTargetAtTime(0, t, 0.1);
    this.windGain.gain.setTargetAtTime(0, t, 0.1);
  }

  setMuted(m) {
    this.muted = m;
    if (this.ok) this.master.gain.setTargetAtTime(m ? 0 : 0.5, this.ctx.currentTime, 0.05);
  }
}
