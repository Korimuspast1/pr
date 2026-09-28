// ============================================================
//  Звук: СИНТЕЗ РЯДНОЙ «ЧЕТВЁРКИ» (спортбайк 600cc)
//  - 4 осциллятора: рык (f0), подголосок (f0, расстроен),
//    низкий лязг (f0/2), визг (f0*2 — растёт с оборотами)
//  - сатуратор + НЧ-фильтр, открывающийся от газа и оборотов
//  - впуск: шум через полосовой фильтр
//  - «пердёж» глушителем на сбросе газа (попкорн)
//  - отсечка на максималке, ветер, гул шин, стуки подвески
// ============================================================
export class GameAudio {
  constructor() {
    this.ok = false;
    this.muted = false;
    this._popCd = 0;
  }

  init() {
    if (this.ok) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    const ctx = this.ctx = new AC();

    // мастер + компрессор (склеивает слои, не даёт перегруз)
    this.master = ctx.createGain();
    this.master.gain.value = 0.55;
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14; comp.ratio.value = 5; comp.attack.value = 0.004; comp.release.value = 0.18;
    this.master.connect(comp); comp.connect(ctx.destination);

    // ---------- ДВИГАТЕЛЬ ----------
    this.engGain = ctx.createGain(); this.engGain.gain.value = 0;

    const shaper = ctx.createWaveShaper();
    const n = 1024, curve = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      const x = (i / (n - 1)) * 2 - 1;
      curve[i] = Math.tanh(x * 3.2) * 0.85;
    }
    shaper.curve = curve; shaper.oversample = '2x';

    this.engLP = ctx.createBiquadFilter();
    this.engLP.type = 'lowpass'; this.engLP.frequency.value = 420; this.engLP.Q.value = 0.9;

    this.engMix = ctx.createGain(); this.engMix.gain.value = 1;

    const mk = (type, gain) => {
      const o = ctx.createOscillator(); o.type = type;
      const g = ctx.createGain(); g.gain.value = gain;
      o.connect(g); g.connect(this.engMix); o.start();
      return { o, g };
    };
    this.oscGrowl = mk('sawtooth', 0.50);   // f0     — рык
    this.oscDet   = mk('sawtooth', 0.32);   // f0     — биения (расстроен)
    this.oscLow   = mk('square',   0.00);   // f0/2   — лязг на малых
    this.oscScream= mk('sawtooth', 0.00);   // f0*2   — визг на верхах
    this.oscDet.o.detune.value = 9;

    this.engMix.connect(shaper); shaper.connect(this.engLP); this.engLP.connect(this.engGain);
    this.engGain.connect(this.master);

    // неровность холостого хода (LFO на громкость движка)
    this.lfo = ctx.createOscillator(); this.lfo.type = 'sine'; this.lfo.frequency.value = 11;
    this.lfoGain = ctx.createGain(); this.lfoGain.gain.value = 0;
    this.lfo.connect(this.lfoGain); this.lfoGain.connect(this.engGain.gain);
    this.lfo.start();

    // отсечка: прямоугольник 15 Гц режет громкость на максималке
    this.lim = ctx.createOscillator(); this.lim.type = 'square'; this.lim.frequency.value = 15;
    this.limGain = ctx.createGain(); this.limGain.gain.value = 0;
    this.lim.connect(this.limGain); this.limGain.connect(this.engGain.gain);
    this.lim.start();

    // ---------- ШУМ (общий буфер) ----------
    const len = ctx.sampleRate * 2;
    const buf = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = buf.getChannelData(0);
    let last = 0;
    for (let i = 0; i < len; i++) { // розовый-ish шум: мягче для ушей
      const w = Math.random() * 2 - 1;
      last = last * 0.86 + w * 0.14;
      d[i] = last * 3.2;
    }
    this.noiseBuf = buf;
    const mkNoise = () => {
      const s = ctx.createBufferSource(); s.buffer = buf; s.loop = true; s.playbackRate.value = 0.9 + Math.random() * 0.2;
      return s;
    };

    // впуск (шум за полосовым фильтром, частота следует за оборотами)
    this.intakeSrc = mkNoise();
    this.intakeBP = ctx.createBiquadFilter(); this.intakeBP.type = 'bandpass';
    this.intakeBP.frequency.value = 900; this.intakeBP.Q.value = 1.4;
    this.intakeGain = ctx.createGain(); this.intakeGain.gain.value = 0;
    this.intakeSrc.connect(this.intakeBP); this.intakeBP.connect(this.intakeGain);
    this.intakeGain.connect(this.master); this.intakeSrc.start();

    // ветер
    this.windSrc = mkNoise();
    this.windFilter = ctx.createBiquadFilter(); this.windFilter.type = 'lowpass';
    this.windFilter.frequency.value = 500;
    this.windGain = ctx.createGain(); this.windGain.gain.value = 0;
    this.windSrc.connect(this.windFilter); this.windFilter.connect(this.windGain);
    this.windGain.connect(this.master); this.windSrc.start();

    // гул шин об асфальт
    this.tireSrc = mkNoise();
    this.tireBP = ctx.createBiquadFilter(); this.tireBP.type = 'bandpass';
    this.tireBP.frequency.value = 150; this.tireBP.Q.value = 0.55;
    this.tireGain = ctx.createGain(); this.tireGain.gain.value = 0;
    this.tireSrc.connect(this.tireBP); this.tireBP.connect(this.tireGain);
    this.tireGain.connect(this.master); this.tireSrc.start();

    this.ok = true;
  }

  resume() { if (this.ok && this.ctx.state === 'suspended') this.ctx.resume(); }

  // rpm 0..1, throttle 0..1, v м/с
  update(rpm, throttle, v, dt = 0.016, extra = {}) {
    if (!this.ok || this.muted) return;
    const t = this.ctx.currentTime;
    const R = 1900 + rpm * 12100;             // об/мин
    const f0 = R / 60 * 2;                    // частота вспышек 4-цил 4-такт: 63…467 Гц
    const T = 0.035;

    this.oscGrowl.o.frequency.setTargetAtTime(f0, t, T);
    this.oscDet.o.frequency.setTargetAtTime(f0, t, T);
    this.oscLow.o.frequency.setTargetAtTime(f0 * 0.5, t, T);
    this.oscScream.o.frequency.setTargetAtTime(f0 * 2, t, T);

    // баланс слоёв: внизу — лязг, наверху — визг
    this.oscLow.g.gain.setTargetAtTime(0.42 * Math.max(0, 1 - rpm * 1.6), t, T);
    this.oscScream.g.gain.setTargetAtTime(Math.pow(Math.max(rpm - 0.35, 0) / 0.65, 1.6) * (0.28 + throttle * 0.3), t, T);

    // фильтр открывается газом и оборотами
    this.engLP.frequency.setTargetAtTime(360 + rpm * 3000 + throttle * 1500 + v * 6, t, 0.05);

    // общая громкость движка
    let vol = 0.05 + throttle * 0.12 + rpm * 0.05;
    if (extra.wheelie > 0.1) vol *= 1.12;                       // вилли — эхо глушителя
    this.engGain.gain.setTargetAtTime(vol, t, 0.07);

    // холостой ход «лениво трясётся», на верхах ровно
    this.lfoGain.gain.setTargetAtTime(vol * 0.35 * Math.max(0, 1 - rpm * 3), t, 0.1);
    // отсечка
    this.limGain.gain.setTargetAtTime(rpm > 0.97 ? vol * 0.5 : 0, t, 0.05);

    // впуск
    this.intakeBP.frequency.setTargetAtTime(700 + rpm * 2600 + throttle * 400, t, 0.06);
    this.intakeGain.gain.setTargetAtTime(throttle * (0.05 + rpm * 0.10), t, 0.08);

    // ветер и шины
    const w = Math.min(v / 58, 1);
    this.windGain.gain.setTargetAtTime(w * w * w * 0.5, t, 0.15);
    this.windFilter.frequency.setTargetAtTime(380 + v * 17, t, 0.2);
    this.tireGain.gain.setTargetAtTime(Math.min(v / 36, 1) * (0.10 + (extra.edge || 0) * 0.5), t, 0.1);
    this.tireBP.frequency.setTargetAtTime(130 + (extra.edge || 0) * 160 + v * 1.2, t, 0.2);

    // «пердёж» на сбросе газа с высоких оборотов
    this._popCd -= dt;
    if (throttle < 0.12 && rpm > 0.45 && this._popCd <= 0 && Math.random() < dt * (1.5 + rpm * 5)) {
      this.pop(0.5 + rpm * 0.5);
      this._popCd = 0.03 + Math.random() * 0.09;
    }
  }

  // выхлопной «хлопок»
  pop(strength = 1) {
    if (!this.ok || this.muted) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuf; src.playbackRate.value = 0.5 + Math.random() * 0.4;
    const bp = ctx.createBiquadFilter(); bp.type = 'bandpass';
    bp.frequency.value = 240 + Math.random() * 260; bp.Q.value = 1.8;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.001, t);
    g.gain.exponentialRampToValueAtTime(0.4 * strength, t + 0.012);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.07 + Math.random() * 0.05);
    src.connect(bp); bp.connect(g); g.connect(this.master);
    src.start(t); src.stop(t + 0.16);
    // низкий «бубух» под хлопком
    const o = ctx.createOscillator(); o.type = 'sine';
    o.frequency.setValueAtTime(170, t); o.frequency.exponentialRampToValueAtTime(58, t + 0.09);
    const og = ctx.createGain();
    og.gain.setValueAtTime(0.22 * strength, t);
    og.gain.exponentialRampToValueAtTime(0.001, t + 0.1);
    o.connect(og); og.connect(this.master);
    o.start(t); o.stop(t + 0.12);
  }

  // переключение передачи: провал газа + хлопок + клац
  shift() {
    if (!this.ok || this.muted) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const g = this.engGain.gain, cur = g.value;
    g.cancelScheduledValues(t);
    g.setValueAtTime(cur * 0.25, t);
    g.setTargetAtTime(cur, t + 0.05, 0.06);
    this.pop(0.8);
    const src = ctx.createBufferSource(); src.buffer = this.noiseBuf; src.playbackRate.value = 2.4;
    const hp = ctx.createBiquadFilter(); hp.type = 'highpass'; hp.frequency.value = 2400;
    const cg = ctx.createGain();
    cg.gain.setValueAtTime(0.12, t); cg.gain.exponentialRampToValueAtTime(0.001, t + 0.05);
    src.connect(hp); hp.connect(cg); cg.connect(this.master);
    src.start(t); src.stop(t + 0.06);
  }

  // посадка после вилли: удар подвески
  land(intensity = 1) {
    if (!this.ok || this.muted) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const i = Math.min(intensity, 1.2);
    const o = ctx.createOscillator(); o.type = 'sine';
    o.frequency.setValueAtTime(85, t); o.frequency.exponentialRampToValueAtTime(38, t + 0.13);
    const og = ctx.createGain();
    og.gain.setValueAtTime(0.55 * i, t); og.gain.exponentialRampToValueAtTime(0.001, t + 0.18);
    o.connect(og); og.connect(this.master); o.start(t); o.stop(t + 0.2);
    const src = ctx.createBufferSource(); src.buffer = this.noiseBuf; src.playbackRate.value = 0.7;
    const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 260;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.4 * i, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.11);
    src.connect(lp); lp.connect(g); g.connect(this.master);
    src.start(t); src.stop(t + 0.13);
  }

  // наезд на шов / стык покрытия
  bump(intensity = 1) {
    if (!this.ok || this.muted) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const src = ctx.createBufferSource(); src.buffer = this.noiseBuf; src.playbackRate.value = 1.1;
    const bp = ctx.createBiquadFilter(); bp.type = 'bandpass';
    bp.frequency.value = 170 + Math.random() * 90; bp.Q.value = 1.2;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.16 * Math.min(intensity, 1.4), t);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.05);
    src.connect(bp); bp.connect(g); g.connect(this.master);
    src.start(t); src.stop(t + 0.06);
  }

  crash() {
    if (!this.ok || this.muted) return;
    const ctx = this.ctx, t = ctx.currentTime;
    const src = ctx.createBufferSource(); src.buffer = this.noiseBuf;
    const f = ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 1100;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.95, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.9);
    src.connect(f); f.connect(g); g.connect(this.master);
    src.start(t); src.stop(t + 0.95);
    // лязг металла
    for (const base of [520, 613, 880]) {
      const o = ctx.createOscillator(); o.type = 'triangle';
      o.frequency.value = base * (0.94 + Math.random() * 0.12);
      const og = ctx.createGain();
      og.gain.setValueAtTime(0.16, t + 0.02);
      og.gain.exponentialRampToValueAtTime(0.001, t + 0.55 + Math.random() * 0.3);
      o.connect(og); og.connect(this.master);
      o.start(t); o.stop(t + 0.9);
    }
    this.engGain.gain.setTargetAtTime(0, t, 0.05);
    this.intakeGain.gain.setTargetAtTime(0, t, 0.05);
    this.windGain.gain.setTargetAtTime(0, t, 0.1);
    this.tireGain.gain.setTargetAtTime(0, t, 0.1);
  }

  idle() {
    if (!this.ok) return;
    const t = this.ctx.currentTime;
    this.engGain.gain.setTargetAtTime(0, t, 0.12);
    this.intakeGain.gain.setTargetAtTime(0, t, 0.1);
    this.windGain.gain.setTargetAtTime(0, t, 0.1);
    this.tireGain.gain.setTargetAtTime(0, t, 0.1);
    this.lfoGain.gain.setTargetAtTime(0, t, 0.1);
    this.limGain.gain.setTargetAtTime(0, t, 0.1);
  }

  setMuted(m) {
    this.muted = m;
    if (this.ok) this.master.gain.setTargetAtTime(m ? 0 : 0.55, this.ctx.currentTime, 0.05);
  }
}
