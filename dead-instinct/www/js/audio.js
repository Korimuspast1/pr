'use strict';
/* =====================================================================
   DEAD INSTINCT — звук: синтез на лету (WebAudio), без файлов
   Дождь, ветер, скрипы, шаги, выстрелы, дыхание, сердцебиение.
   ===================================================================== */
const SND = {
  ctx: null, master: null, enabled: true, ambient: null, started: false,
  last: new Map(), hb: 0, breathe: 0, windGain: null, rainGain: null, noiseBuf: null
};

function sndInit() {
  if (SND.ctx) return;
  const AC = window.AudioContext || window.webkitAudioContext;
  if (!AC) return;
  try {
    SND.ctx = new AC();
    SND.master = SND.ctx.createGain();
    SND.master.gain.value = 0.55;
    SND.master.connect(SND.ctx.destination);
    // буфер шума
    const len = SND.ctx.sampleRate * 2;
    const buf = SND.ctx.createBuffer(1, len, SND.ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    SND.noiseBuf = buf;
  } catch (e) { SND.ctx = null; }
}

function noiseSource() {
  const s = SND.ctx.createBufferSource();
  s.buffer = SND.noiseBuf; s.loop = true;
  return s;
}

function env(gain, t0, a, d, peak) {
  gain.gain.setValueAtTime(0.0001, t0);
  gain.gain.exponentialRampToValueAtTime(peak, t0 + a);
  gain.gain.exponentialRampToValueAtTime(0.0001, t0 + a + d);
}

function tone(freq, type, dur, vol, slideTo, when) {
  if (!SND.ctx || !SND.enabled) return;
  const t0 = (when || SND.ctx.currentTime) + 0.001;
  const o = SND.ctx.createOscillator();
  const g = SND.ctx.createGain();
  o.type = type || 'sine';
  o.frequency.setValueAtTime(freq, t0);
  if (slideTo) o.frequency.exponentialRampToValueAtTime(Math.max(20, slideTo), t0 + dur);
  env(g, t0, 0.006, dur, vol);
  o.connect(g); g.connect(SND.master);
  o.start(t0); o.stop(t0 + dur + 0.05);
}

function noiseBurst(dur, vol, filterFreq, type, slideTo, when) {
  if (!SND.ctx || !SND.enabled) return;
  const t0 = (when || SND.ctx.currentTime) + 0.001;
  const s = noiseSource();
  const f = SND.ctx.createBiquadFilter();
  f.type = type || 'lowpass';
  f.frequency.setValueAtTime(filterFreq || 1200, t0);
  if (slideTo) f.frequency.exponentialRampToValueAtTime(Math.max(60, slideTo), t0 + dur);
  const g = SND.ctx.createGain();
  env(g, t0, 0.004, dur, vol);
  s.connect(f); f.connect(g); g.connect(SND.master);
  s.start(t0); s.stop(t0 + dur + 0.05);
}

function clamp(v, a, b) { return v < a ? a : v > b ? b : v; }

/* ---------- отдельные звуки ---------- */
function sfx(kind, arg) {
  if (!SND.ctx || !SND.enabled) return;
  const now = SND.ctx.currentTime;
  // анти-спам одинаковых звуков
  const last = SND.last.get(kind) || 0;
  const minGap = { step: 0.22, drip: 0.4, click: 0.04, moan: 1.6, heartbeat: 0.5, hit: 0.06 }[kind] || 0.03;
  if (now - last < minGap) return;
  SND.last.set(kind, now);
  switch (kind) {
    case 'click': tone(880, 'square', 0.05, 0.08, 660); break;
    case 'step': noiseBurst(0.09, 0.07, 900, 'bandpass', 500); break;
    case 'stepSneak': noiseBurst(0.05, 0.025, 600, 'lowpass'); break;
    case 'stepRun': noiseBurst(0.07, 0.12, 1400, 'bandpass', 700); break;
    case 'water': noiseBurst(0.16, 0.08, 900, 'bandpass', 1500); break;
    case 'hit': noiseBurst(0.1, 0.22, 500, 'lowpass', 120); tone(90, 'square', 0.1, 0.12, 40); break;
    case 'hitFlesh': noiseBurst(0.18, 0.2, 380, 'lowpass', 90); break;
    case 'gun9': noiseBurst(0.16, 0.5, 2400, 'lowpass', 150); tone(140, 'square', 0.12, 0.28, 40); break;
    case 'gun12': noiseBurst(0.28, 0.6, 1600, 'lowpass', 100); tone(90, 'square', 0.22, 0.34, 34); break;
    case 'gun762': noiseBurst(0.34, 0.55, 3000, 'lowpass', 120); tone(120, 'sawtooth', 0.26, 0.28, 40); break;
    case 'reload': tone(520, 'square', 0.05, 0.06, 400); setTimeout(() => tone(760, 'square', 0.05, 0.06, 600), 90); break;
    case 'door': noiseBurst(0.32, 0.13, 700, 'lowpass', 260); break;
    case 'doorBreak': noiseBurst(0.5, 0.3, 900, 'lowpass', 120); tone(70, 'square', 0.4, 0.16, 40); break;
    case 'loot': tone(660, 'square', 0.06, 0.07); setTimeout(() => tone(990, 'square', 0.08, 0.07), 70); break;
    case 'moan':
      tone(120 + Math.random() * 40, 'sawtooth', 1.5, 0.05, 78);
      noiseBurst(1.3, 0.035, 420, 'bandpass', 220);
      break;
    case 'scream':
      tone(700, 'sawtooth', 0.7, 0.16, 1500);
      tone(950, 'square', 0.6, 0.09, 2000);
      noiseBurst(0.8, 0.16, 2600, 'bandpass', 900);
      break;
    case 'hurt': tone(180, 'square', 0.18, 0.16, 70); noiseBurst(0.2, 0.12, 700, 'lowpass'); break;
    case 'heartbeat': tone(58, 'sine', 0.13, 0.22, 44); setTimeout(() => tone(52, 'sine', 0.11, 0.14, 40), 180); break;
    case 'breath': noiseBurst(0.5, 0.035, 700, 'bandpass', 400); break;
    case 'eat': noiseBurst(0.3, 0.07, 500, 'lowpass'); break;
    case 'heal': tone(420, 'sine', 0.18, 0.08, 640); break;
    case 'flare': noiseBurst(0.6, 0.2, 2200, 'bandpass', 400); break;
    case 'zombieAlert': tone(220, 'sawtooth', 0.4, 0.08, 300); break;
    case 'scare': noiseBurst(0.9, 0.32, 2000, 'lowpass', 60); tone(50, 'sawtooth', 0.9, 0.18, 30); break;
    case 'radio': noiseBurst(0.9, 0.05, 1800, 'bandpass', 1200); break;
    case 'win': [523, 659, 784, 1047].forEach((f, i) => setTimeout(() => tone(f, 'square', 0.3, 0.08), i * 160)); break;
    case 'lose': [392, 330, 262, 196].forEach((f, i) => setTimeout(() => tone(f, 'sawtooth', 0.42, 0.09), i * 220)); break;
  }
}

/* ---------- атмосфера ---------- */

function startAmbient(G) {
  sndInit();
  if (!SND.ctx || !SND.enabled) return;
  if (SND.ctx.state === 'suspended') SND.ctx.resume();
  stopAmbient();
  const ctx = SND.ctx;
  const t0 = ctx.currentTime;
  // дождь
  const rain = noiseSource();
  const rf = ctx.createBiquadFilter(); rf.type = 'highpass'; rf.frequency.value = 1400;
  const rg = ctx.createGain(); rg.gain.value = 0.0001;
  rain.connect(rf); rf.connect(rg); rg.connect(SND.master);
  rain.start();
  rg.gain.setTargetAtTime(G && G.weather && G.weather.rain > 0.3 ? 0.045 : 0.012, t0, 1.2);
  // ветер
  const wind = noiseSource();
  const wf = ctx.createBiquadFilter(); wf.type = 'lowpass'; wf.frequency.value = 380;
  const wg = ctx.createGain(); wg.gain.value = 0.0001;
  wind.connect(wf); wf.connect(wg); wg.connect(SND.master);
  wind.start();
  wg.gain.setTargetAtTime(0.03, t0, 2.0);
  // гудение
  const hum = ctx.createOscillator(); hum.type = 'sine'; hum.frequency.value = 46;
  const hg = ctx.createGain(); hg.gain.value = 0.0001;
  hum.connect(hg); hg.connect(SND.master);
  hum.start();
  hg.gain.setTargetAtTime(0.018, t0, 2.5);
  SND.ambient = { rain, wind, hum, rg, wg, hg };
  SND.started = true;
}

function stopAmbient() {
  if (!SND.ambient) return;
  try {
    const a = SND.ambient;
    a.rg.gain.setTargetAtTime(0.0001, SND.ctx.currentTime, 0.3);
    a.wg.gain.setTargetAtTime(0.0001, SND.ctx.currentTime, 0.3);
    a.hg.gain.setTargetAtTime(0.0001, SND.ctx.currentTime, 0.3);
    setTimeout(() => { try { a.rain.stop(); a.wind.stop(); a.hum.stop(); } catch (e) { } }, 700);
  } catch (e) { }
  SND.ambient = null;
}

/* ---------- атмосферные события по состоянию игры ---------- */
function ambientTick(G, dt) {
  if (!SND.ctx || !SND.enabled || !G) return;
  const P = G.player;
  // дождь громче
  if (SND.ambient && G.weather) SND.ambient.rg.gain.setTargetAtTime(0.012 + G.weather.rain * 0.045, SND.ctx.currentTime, 0.8);
  // сердцебиение при низком HP или близкой опасности
  let near = 99;
  for (const z of G.zombies) { const d = Math.hypot(z.x - P.x, z.y - P.y); if (d < near) near = d; }
  const danger = near < 5 || P.hp < 35;
  SND.hb -= dt;
  if (danger && SND.hb <= 0) { sfx('heartbeat'); SND.hb = near < 3 ? 0.85 : 1.25; }
  // дыхание
  SND.breathe -= dt;
  if (SND.breathe <= 0) { sfx('breath'); SND.breathe = P.stamina < 40 ? 1.6 : 3.4; }
  // шаги
  const moving = Math.hypot(P.vx || 0, P.vy || 0) > 0.01 || G._moving;
  if (G._moving) {
    G._stepT = (G._stepT || 0) - dt;
    if (G._stepT <= 0) {
      G._stepT = P.stance === 'sprint' ? 0.26 : P.stance === 'sneak' ? 0.62 : 0.42;
      sfx(P.stance === 'sprint' ? 'stepRun' : P.stance === 'sneak' ? 'stepSneak' : 'step');
      if (isWater(G.M, P.x | 0, P.y | 0)) sfx('water');
    }
  }
  // стоны зомби рядом
  if (Math.random() < dt * 0.12 && near < 12) sfx('moan');
  // скрипы
  if (Math.random() < dt * 0.05) noiseBurst(0.6, 0.02, 1200, 'bandpass', 300);
}

SND.click = function () { sfx('click'); };

/* Сердцебиение с силой, зависящей от страха: два удара, тем громче, чем страшнее.
   Отдельная функция, чтобы приём «сердце бьётся чаще» читался игроком. */
function heart(fear) {
  if (!SND.ctx || !SND.enabled) return;
  const now = SND.ctx.currentTime;
  const last = SND.last.get('heart-custom') || 0;
  if (now - last < 0.34) return;
  SND.last.set('heart-custom', now);
  const f = clamp(fear, 0, 1);
  const v1 = 0.1 + f * 0.3, v2 = v1 * 0.62;
  tone(56 + f * 8, 'sine', 0.13, v1, 38);
  setTimeout(() => tone(46 + f * 6, 'sine', 0.16, v2, 32), 165);
  if (f > 0.75) setTimeout(() => tone(44, 'sine', 0.1, v2 * 0.5, 30), 330);
}

const SFX = { heart: heart, play: sfx };

if (typeof module !== 'undefined' && module.exports) module.exports = { SND, sfx, sndInit, startAmbient, stopAmbient, ambientTick, SFX };
if (typeof window !== 'undefined') { window.SFX = SFX; }
