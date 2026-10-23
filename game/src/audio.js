// Fully procedural WebAudio SFX: no external assets.
export class AudioSys {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.noiseBuf = null;
    this.muted = false;
    this.volume = 0.5;
  }

  ensure() {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') this.ctx.resume();
      return true;
    }
    try {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return false;
      this.ctx = new AC();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.muted ? 0 : this.volume;
      this.master.connect(this.ctx.destination);

      const len = this.ctx.sampleRate * 1.5;
      this.noiseBuf = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
      const data = this.noiseBuf.getChannelData(0);
      for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
      return true;
    } catch (e) {
      return false;
    }
  }

  setMuted(m) {
    this.muted = m;
    if (this.master) this.master.gain.value = m ? 0 : this.volume;
  }

  now() { return this.ctx ? this.ctx.currentTime : 0; }

  noise({ dur = 0.2, gain = 0.4, freq = 1400, freqEnd = 300, q = 1, type = 'lowpass', delay = 0, attack = 0.001 }) {
    if (!this.ensure() || this.muted) return;
    const t = this.now() + delay;
    const src = this.ctx.createBufferSource();
    src.buffer = this.noiseBuf;
    src.playbackRate.value = 1;
    const flt = this.ctx.createBiquadFilter();
    flt.type = type;
    flt.Q.value = q;
    flt.frequency.setValueAtTime(freq, t);
    flt.frequency.exponentialRampToValueAtTime(Math.max(40, freqEnd), t + dur);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(gain, t + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(flt); flt.connect(g); g.connect(this.master);
    src.start(t); src.stop(t + dur + 0.02);
  }

  tone({ freq = 200, freqEnd = 80, dur = 0.18, gain = 0.3, type = 'sine', delay = 0, attack = 0.002 }) {
    if (!this.ensure() || this.muted) return;
    const t = this.now() + delay;
    const o = this.ctx.createOscillator();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    o.frequency.exponentialRampToValueAtTime(Math.max(20, freqEnd), t + dur);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(gain, t + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(this.master);
    o.start(t); o.stop(t + dur + 0.02);
  }

  shot(kind = 'pistol') {
    if (!this.ensure() || this.muted) return;
    switch (kind) {
      case 'pistol':
        this.noise({ dur: 0.22, gain: 0.5, freq: 4200, freqEnd: 260, q: 0.9 });
        this.tone({ freq: 160, freqEnd: 45, dur: 0.16, gain: 0.4, type: 'square' });
        this.noise({ dur: 0.35, gain: 0.09, freq: 900, freqEnd: 180, delay: 0.03 });
        break;
      case 'smg':
        this.noise({ dur: 0.12, gain: 0.34, freq: 5200, freqEnd: 500, q: 0.8 });
        this.tone({ freq: 200, freqEnd: 70, dur: 0.09, gain: 0.22, type: 'square' });
        break;
      case 'shotgun':
        this.noise({ dur: 0.5, gain: 0.75, freq: 2600, freqEnd: 90, q: 0.6 });
        this.tone({ freq: 110, freqEnd: 30, dur: 0.4, gain: 0.55, type: 'sawtooth' });
        this.noise({ dur: 0.7, gain: 0.12, freq: 700, freqEnd: 90, delay: 0.05 });
        break;
      case 'rifle':
        this.noise({ dur: 0.4, gain: 0.6, freq: 7000, freqEnd: 200, q: 1.2 });
        this.tone({ freq: 220, freqEnd: 40, dur: 0.3, gain: 0.4, type: 'square' });
        this.noise({ dur: 0.6, gain: 0.1, freq: 1400, freqEnd: 120, delay: 0.08 });
        break;
      default:
        this.noise({ dur: 0.2, gain: 0.4, freq: 3000, freqEnd: 200 });
    }
  }

  slash() {
    this.noise({ dur: 0.16, gain: 0.22, freq: 5200, freqEnd: 900, q: 1.4, type: 'bandpass' });
  }

  cut() {
    this.noise({ dur: 0.13, gain: 0.28, freq: 1100, freqEnd: 200, q: 0.7 });
    this.noise({ dur: 0.18, gain: 0.16, freq: 380, freqEnd: 120, delay: 0.02 });
  }

  sever() {
    this.noise({ dur: 0.22, gain: 0.4, freq: 700, freqEnd: 90, q: 0.7 });
    this.noise({ dur: 0.3, gain: 0.2, freq: 250, freqEnd: 60, delay: 0.03 });
    this.tone({ freq: 90, freqEnd: 35, dur: 0.25, gain: 0.22, type: 'triangle' });
  }

  flesh() {
    this.noise({ dur: 0.1, gain: 0.22, freq: 600, freqEnd: 160, q: 0.8 });
  }

  impact() {
    this.noise({ dur: 0.06, gain: 0.2, freq: 2400, freqEnd: 600, q: 1 });
  }

  casing() {
    this.tone({ freq: 2100 + Math.random() * 700, freqEnd: 1200, dur: 0.06, gain: 0.045, type: 'triangle' });
  }

  reload() {
    this.noise({ dur: 0.05, gain: 0.18, freq: 1800, freqEnd: 800 });
    this.noise({ dur: 0.06, gain: 0.2, freq: 900, freqEnd: 400, delay: 0.12 });
  }

  empty() {
    this.noise({ dur: 0.04, gain: 0.12, freq: 2600, freqEnd: 1500 });
  }

  hitmark() {
    this.tone({ freq: 1400, freqEnd: 900, dur: 0.05, gain: 0.07, type: 'square' });
  }

  scream() {
    const f = 300 + Math.random() * 180;
    this.tone({ freq: f, freqEnd: f * 0.45, dur: 0.55, gain: 0.09, type: 'sawtooth', attack: 0.03 });
    this.tone({ freq: f * 1.5, freqEnd: f * 0.7, dur: 0.4, gain: 0.04, type: 'square', attack: 0.05 });
  }

  step() {
    this.noise({ dur: 0.05, gain: 0.035, freq: 420, freqEnd: 120 });
  }

  wizz() {
    this.noise({ dur: 0.08, gain: 0.14, freq: 3800, freqEnd: 1200, q: 3, type: 'bandpass' });
  }

  splat() {
    this.noise({ dur: 0.12, gain: 0.08, freq: 500, freqEnd: 100 });
  }
}
