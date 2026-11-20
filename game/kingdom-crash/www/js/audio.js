'use strict';
/* ===== Звук =====
   Звуковых файлов в игре нет: эффекты и лютневая мелодия синтезируются на лету (Web Audio).
   Если звук недоступен или отключён — модуль молча ничего не делает. */
const SND = (() => {
  let ctx = null, sfx = null, mus = null, amb = null, ready = false, noiseBuf = null, timer = 0, song = null;
  const st = { sfx: true, music: true }, last = {}, plucks = {};

  function init() {
    if (ctx) return true;
    const AC = window.AudioContext || window.webkitAudioContext; if (!AC) return false;
    try {
      ctx = new AC();
      const comp = ctx.createDynamicsCompressor(); comp.connect(ctx.destination); // чтобы громкие моменты не хрипели
      sfx = ctx.createGain(); sfx.gain.value = 0.6; sfx.connect(comp);
      mus = ctx.createGain(); mus.gain.value = 0.5; mus.connect(comp);
      amb = ctx.createGain(); amb.gain.value = 0.7; amb.connect(comp); // звуки природы
      const n = ctx.sampleRate; noiseBuf = ctx.createBuffer(1, n, n); const d = noiseBuf.getChannelData(0); for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
    } catch (e) { ctx = null; return false; }
    return true;
  }
  // браузер разрешает звук только после первого касания
  function unlock() {
    if (!init()) return;
    if (ctx.state === 'suspended' && !document.hidden) { const p = ctx.resume(); if (p && p.catch) p.catch(() => {}); }
    ready = true; if (st.music) startMusic();
  }

  /* ----- кирпичики эффектов ----- */
  function tone(f, at, dur, type, vol, f2) {
    const t = ctx.currentTime + at, o = ctx.createOscillator(), g = ctx.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t); if (f2) o.frequency.exponentialRampToValueAtTime(f2, t + dur);
    g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(vol, t + 0.008); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(sfx); o.start(t); o.stop(t + dur + 0.03);
  }
  function noise(at, dur, vol, freq, q, f2) {
    const t = ctx.currentTime + at, s = ctx.createBufferSource(), f = ctx.createBiquadFilter(), g = ctx.createGain();
    s.buffer = noiseBuf; s.loop = true; f.type = 'bandpass'; f.frequency.setValueAtTime(freq, t); if (f2) f.frequency.exponentialRampToValueAtTime(f2, t + dur); f.Q.value = q || 1;
    g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(vol, t + 0.005); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f); f.connect(g); g.connect(sfx); s.start(t, Math.random() * 0.5); s.stop(t + dur + 0.03);
  }
  function horn(at, f, dur, vol) { // боевой рог
    const t = ctx.currentTime + at, o = ctx.createOscillator(), o2 = ctx.createOscillator(), lp = ctx.createBiquadFilter(), g = ctx.createGain();
    o.type = 'sawtooth'; o2.type = 'sawtooth'; o.frequency.value = f; o2.frequency.value = f * 1.006;
    lp.type = 'lowpass'; lp.frequency.setValueAtTime(320, t); lp.frequency.linearRampToValueAtTime(1100, t + dur * 0.4); lp.frequency.linearRampToValueAtTime(520, t + dur);
    g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(vol, t + 0.1); g.gain.setValueAtTime(vol, t + Math.max(0.11, dur - 0.18)); g.gain.linearRampToValueAtTime(0.0001, t + dur);
    o.connect(lp); o2.connect(lp); lp.connect(g); g.connect(sfx); o.start(t); o2.start(t); o.stop(t + dur + 0.05); o2.stop(t + dur + 0.05);
  }
  // [минимальный интервал между повторами, с; функция]
  const FX = {
    click: [0.05, () => { tone(1100, 0, 0.035, 'square', 0.04); }],
    thunder: [1.5, () => { noise(0, 1.3, 0.55, 130, 0.6, 42); tone(52, 0.06, 1.1, 'sine', 0.5, 30); noise(0.12, 0.9, 0.3, 90, 0.8, 36); }],
    tab: [0.06, () => { noise(0, 0.05, 0.12, 900, 1.2); tone(300, 0, 0.05, 'sine', 0.12, 180); }],
    ok: [0.1, () => { tone(660, 0, 0.08, 'triangle', 0.14); tone(990, 0.08, 0.14, 'triangle', 0.14); }],
    err: [0.15, () => { tone(196, 0, 0.14, 'square', 0.06, 130); }],
    build: [0.15, () => { noise(0, 0.07, 0.3, 380, 0.9); tone(130, 0, 0.1, 'sine', 0.3, 70); noise(0.13, 0.06, 0.22, 520, 0.9); tone(150, 0.13, 0.09, 'sine', 0.22, 80); }],
    coin: [0.1, () => { tone(1568, 0, 0.07, 'triangle', 0.1); tone(2093, 0.07, 0.16, 'triangle', 0.1); }],
    msg: [0.4, () => { tone(784, 0, 0.1, 'sine', 0.12); tone(1175, 0.11, 0.2, 'sine', 0.12); }],
    clash: [0.09, () => { noise(0, 0.06, 0.16, 3800 + Math.random() * 1500, 4); tone(1500 + Math.random() * 900, 0, 0.07, 'triangle', 0.05); }],
    arrow: [0.12, () => { noise(0, 0.1, 0.07, 3200, 1.5, 1200); }],
    rock: [0.2, () => { noise(0, 0.3, 0.4, 200, 0.7, 80); tone(80, 0, 0.3, 'sine', 0.4, 38); }],
    horn: [1.5, () => { horn(0, 174.6, 0.7, 0.16); horn(0.72, 233.1, 1.1, 0.18); }],
    ready: [0.5, () => { horn(0, 293.7, 0.25, 0.1); horn(0.26, 392, 0.4, 0.1); }],
    capture: [1, () => { [392, 523.3, 659.3, 784].forEach((f, i) => tone(f, i * 0.12, 0.3, 'triangle', 0.16)); }],
    lost: [1, () => { [392, 349.2, 293.7, 233.1].forEach((f, i) => tone(f, i * 0.16, 0.34, 'triangle', 0.15)); }],
    win: [3, () => { [523.3, 659.3, 784, 1046.5, 784, 1046.5].forEach((f, i) => tone(f, i * 0.16, i === 5 ? 0.9 : 0.24, 'triangle', 0.18)); horn(0, 130.8, 1.9, 0.1); }],
    lose: [3, () => { [293.7, 277.2, 220, 196, 146.8].forEach((f, i) => tone(f, i * 0.28, i === 4 ? 1.2 : 0.4, 'triangle', 0.16)); }],
    ach: [1, () => { [784, 987.8, 1174.7, 1568].forEach((f, i) => tone(f, i * 0.09, 0.3, 'sine', 0.13)); }]
  };
  function play(name) {
    if (!st.sfx || !ready || !ctx || ctx.state !== 'running') return; const fx = FX[name]; if (!fx) return;
    const now = ctx.currentTime; if (last[name] !== undefined && now - last[name] < fx[0]) return; last[name] = now;
    try { fx[1](); } catch (e) { /* звук не должен ронять игру */ }
  }

  /* ----- музыка: щипок струны (алгоритм Карплуса — Стронга) ----- */
  function pluck(midi) {
    if (plucks[midi]) return plucks[midi];
    const sr = ctx.sampleRate, f = 440 * Math.pow(2, (midi - 69) / 12), N = Math.max(2, Math.round(sr / f)), len = Math.floor(sr * 1.8), buf = ctx.createBuffer(1, len, sr), d = buf.getChannelData(0), line = new Float32Array(N);
    let prev = 0; for (let i = 0; i < N; i++) { prev = prev * 0.5 + (Math.random() * 2 - 1) * 0.5; line[i] = prev; } // приглушённый шум даёт тёплый тембр
    const damp = midi > 64 ? 0.994 : 0.996; let k = 0;
    for (let i = 0; i < len; i++) { const a = line[k]; d[i] = a; line[k] = (a + line[(k + 1) % N]) * 0.5 * damp; k = (k + 1) % N; }
    const fade = Math.floor(sr * 0.1); for (let i = 0; i < fade; i++) d[len - 1 - i] *= i / fade;
    return plucks[midi] = { buf, rate: f / (sr / N) }; // rate поправляет строй: длина линии задержки целая
  }
  function note(midi, t, vol) { const p = pluck(midi), s = ctx.createBufferSource(), g = ctx.createGain(); s.buffer = p.buf; s.playbackRate.value = p.rate; g.gain.value = vol; s.connect(g); g.connect(mus); s.start(t); }
  // несколько лютневых мелодий в средневековых ладах: [нота MIDI (0 — пауза), длительность в долях]
  const SONGS = [
    { n: 'Пляска', beat: 60 / 104, bassEvery: 3, gap: 21,
      mel: [
        [62, 1], [65, 1], [69, 1], [67, 1], [65, .5], [64, .5], [62, 1], [65, 1], [69, 1], [72, 1], [71, .5], [69, .5], [67, 1], [69, 1],
        [74, 1], [72, .5], [71, .5], [69, 1], [67, 1], [69, .5], [71, .5], [72, 1], [69, 1], [67, .5], [65, .5], [64, 1], [62, 2], [0, 1],
        [62, .5], [64, .5], [65, 1], [69, 1], [67, 1], [64, 1], [60, 1], [65, .5], [67, .5], [69, 1], [74, 1], [72, 1], [69, 1], [65, 1],
        [67, 1], [71, .5], [69, .5], [67, 1], [65, 1], [64, 1], [62, 1], [64, .5], [65, .5], [64, 1], [60, 1], [62, 3]],
      bass: [50, 50, 53, 55, 50, 55, 57, 50, 50, 48, 53, 53, 55, 50, 45, 50] },
    { n: 'Вечерняя баллада', beat: 60 / 76, bassEvery: 4, gap: 24,
      mel: [
        [69, 1.5], [72, .5], [71, 1], [69, 1], [67, 1.5], [64, .5], [65, 2], [0, .5],
        [64, 1], [65, 1], [67, 1], [69, 1.5], [72, .5], [74, 1], [72, 1], [71, 1.5], [69, .5], [67, 2], [0, .5],
        [69, 1], [71, 1], [72, 1], [74, 1.5], [76, .5], [74, 1], [72, 1], [71, 1], [69, 1], [67, 1], [65, 1], [64, 2], [0, 1],
        [62, 1], [64, 1], [65, 1], [67, 1.5], [65, .5], [64, 1], [62, 3], [0, 1]],
      bass: [45, 48, 50, 43, 45, 41, 45, 40] },
    { n: 'Марш дружины', beat: 60 / 112, bassEvery: 2, gap: 16,
      mel: [
        [67, 1], [67, .5], [69, .5], [70, 1], [67, 1], [65, 1], [67, 2], [0, .5], [67, .5],
        [70, 1], [70, .5], [72, .5], [74, 1], [70, 1], [67, 1], [70, 2], [0, .5], [70, .5],
        [74, 1], [72, .5], [70, .5], [72, 1], [70, 1], [67, .5], [65, .5], [67, 1], [70, 1], [67, 1], [65, 1], [62, 1], [67, 3]],
      bass: [55, 52, 55, 50, 55, 52, 50, 45] },
    { n: 'Ярмарка', beat: 60 / 126, bassEvery: 3, gap: 18,
      mel: [
        [65, .5], [67, .5], [69, .5], [72, .5], [74, 1], [72, .5], [69, .5], [70, 1], [69, .5], [67, .5], [65, 1], [67, 1],
        [69, .5], [72, .5], [74, .5], [77, .5], [76, 1], [74, .5], [72, .5], [74, 1], [72, .5], [69, .5], [67, 1], [65, 1],
        [64, .5], [65, .5], [67, .5], [69, .5], [70, 1], [69, .5], [67, .5], [65, 1], [67, .5], [69, .5], [65, 1], [64, 1], [65, 3]],
      bass: [53, 53, 55, 57, 53, 55, 53, 50] },
    { n: 'Утро в лесу', beat: 60 / 96, bassEvery: 4, gap: 22,
      mel: [
        [62, .75], [65, .75], [69, 1.5], [67, 1], [65, .75], [64, .75], [62, 1], [64, 1], [65, 1.5], [0, .5],
        [64, .75], [65, .75], [67, 1], [69, 1.5], [71, .75], [69, .75], [67, 1], [65, 1], [64, 1.5], [0, .5],
        [62, .75], [65, .75], [69, 1.5], [67, 1], [65, .75], [64, .75], [62, 1], [60, 1], [62, 2], [0, 1]],
      bass: [50, 45, 41, 48, 50, 45, 43, 45] },
    { n: 'Старая дорога', beat: 60 / 92, bassEvery: 4, gap: 24,
      mel: [
        [69, .75], [72, .75], [76, 1.5], [74, .75], [72, .75], [76, 1.5], [74, .75], [72, 1], [71, .75], [72, .75], [69, 2], [0, .5],
        [67, .75], [69, .75], [71, 1], [72, 1.5], [76, .75], [74, .75], [72, 1], [71, 1], [69, 1.5], [0, .5],
        [69, .75], [72, .75], [76, 1.5], [74, .75], [72, .75], [71, .75], [69, 1], [67, 1], [69, 2], [0, 1]],
      bass: [45, 41, 48, 45, 43, 40, 45, 40] },
    { n: 'Охота на оленя', beat: 60 / 132, bassEvery: 2, gap: 14,
      mel: [
        [64, .5], [67, .5], [71, 1], [76, 1.5], [74, .5], [71, .5], [74, 1], [71, .5], [67, 1], [71, .5], [72, .5], [71, 1], [0, .5],
        [71, .5], [76, .5], [78, 1], [76, 1.5], [74, .5], [72, .5], [71, 1], [67, .5], [69, .5], [71, 2], [0, .5],
        [64, .5], [67, .5], [71, 1], [76, 1], [74, .5], [71, .5], [67, 1], [69, .5], [71, 1], [64, 2.5], [0, .5]],
      bass: [40, 40, 45, 45, 40, 43, 40, 40] },
    { n: 'Тихая заводь', beat: 60 / 88, bassEvery: 4, gap: 26,
      mel: [
        [60, .75], [64, .75], [67, 1], [71, 1.5], [69, .75], [67, .75], [64, 1], [62, 1], [60, 2], [0, .5],
        [69, .75], [71, .75], [72, 1], [76, 1.5], [74, .75], [72, .75], [71, .75], [69, .75], [67, 2], [0, .5],
        [60, .75], [64, .75], [67, 1], [71, 1], [69, .75], [67, .75], [64, .75], [62, .75], [60, 2.5], [0, .75]],
      bass: [48, 43, 41, 48, 43, 41, 45, 48] },
    { n: 'Северный форт', beat: 60 / 100, bassEvery: 2, gap: 20,
      mel: [
        [67, .75], [69, .75], [71, 1], [74, 1.5], [72, .75], [71, .75], [69, 1], [71, .5], [72, .5], [71, 1], [0, .5],
        [76, 1], [74, .75], [72, .75], [71, 1.5], [69, .75], [67, 1], [69, .75], [71, 1], [67, 2], [0, .5],
        [67, .75], [69, .75], [71, 1], [74, 1], [72, .75], [71, .75], [69, .75], [67, .75], [64, .75], [67, 2.5], [0, .75]],
      bass: [43, 43, 50, 43, 45, 43, 41, 43] },
    { n: 'Таверна у порта', beat: 60 / 120, bassEvery: 3, gap: 16,
      mel: [
        [62, .5], [65, .5], [69, 1], [65, .5], [64, .5], [62, .5], [61, .5], [62, .5], [65, 1], [64, 1], [0, .5],
        [69, .5], [69, .5], [67, .5], [65, .5], [64, .5], [62, .5], [64, .5], [65, .5], [64, 1], [62, 1.5], [0, .5],
        [62, .5], [65, .5], [69, .5], [72, .5], [74, 1], [72, .5], [71, .5], [69, 1], [67, .5], [65, .5], [64, 1], [62, 1.5], [0, 1]],
      bass: [50, 50, 57, 55, 50, 53, 50, 45] }
  ];
  function schedule() {
    if (!ctx || ctx.state !== 'running' || !st.music || !song) return;
    const S0 = SONGS[song.s] || SONGS[0];
    if (song.t < ctx.currentTime) song.t = ctx.currentTime + 0.1; // после паузы не «догоняем» пропущенное
    while (song.t < ctx.currentTime + 0.7) {
      const m = S0.mel[song.i], up = song.pass % 2 === 1 ? 12 : 0;
      if (song.nb % S0.bassEvery === 0) { const b = S0.bass[(song.nb / S0.bassEvery) % S0.bass.length]; note(b, song.t, 0.5); note(b + 7, song.t + S0.beat * 2, 0.2); }
      if (m[0]) note(m[0] + up, song.t, 0.4 + Math.random() * 0.1);
      song.t += m[1] * S0.beat; song.beat += m[1]; song.i++; song.nb++;
      if (song.i >= S0.mel.length) {
        song.i = 0; song.beat = 0; song.nb = 0; song.pass++;
        if (song.pass >= 1) { song.pass = 0; let ns = song.s; while (ns === song.s && SONGS.length > 1) ns = (Math.random() * SONGS.length) | 0; song.s = ns; song.t += SONGS[ns].beat * 2.5; } // короткая пауза и следующая мелодия
        else song.t += S0.beat * S0.bassEvery * 2;
      }
    }
  }
  function startMusic() { if (timer || !ctx || !st.music || trPlay) return; song = { s: (Math.random() * SONGS.length) | 0, t: ctx.currentTime + 0.5, i: 0, beat: 0, nb: 0, pass: 0 }; timer = setInterval(schedule, 250); schedule(); }
  function next() { if (!song || !ctx) return; let ns = song.s; while (ns === song.s && SONGS.length > 1) ns = (Math.random() * SONGS.length) | 0; song.s = ns; song.i = 0; song.beat = 0; song.nb = 0; song.pass = 0; if (song.t > ctx.currentTime + 0.3) song.t = ctx.currentTime + 0.15; }
  function songName() { return song ? SONGS[song.s].n : ''; }
  function stopMusic() { if (timer) { clearInterval(timer); timer = 0; } song = null; }

  function set(k, v) { if (k === 'sfx') st.sfx = !!v; if (k === 'music') { st.music = !!v; if (st.music) { if (ready) startMusic(); } else stopMusic(); } }
  // приложение свернули — звук замолкает
  document.addEventListener('visibilitychange', () => {
    if (!ctx) return;
    if (document.hidden) { if (ctx.state === 'running') { const p = ctx.suspend(); if (p && p.catch) p.catch(() => {}); } }
    else if (ready) { const p = ctx.resume(); if (p && p.catch) p.catch(() => {}); }
  });
  ['pointerdown', 'touchend', 'keydown'].forEach(ev => window.addEventListener(ev, unlock, { passive: true }));
  /* ----- саундтрек: готовые записи мелодий игры (плеер в настройках) ----- */
  const TRACKS = [
    { n: 'Пляска', src: 'soundtrack/01-plyaska.mp3' },
    { n: 'Вечерняя баллада', src: 'soundtrack/02-vechernyaya-ballada.mp3' },
    { n: 'Марш дружины', src: 'soundtrack/03-marsh-druzhiny.mp3' },
    { n: 'Ярмарка', src: 'soundtrack/04-yarmarka.mp3' },
    { n: 'Утро в лесу', src: 'soundtrack/05-utro-v-lesu.mp3' },
    { n: 'Старая дорога', src: 'soundtrack/06-staraya-doroga.mp3' },
    { n: 'Охота на оленя', src: 'soundtrack/07-ohota-na-olenya.mp3' },
    { n: 'Тихая заводь', src: 'soundtrack/08-tihaya-zavod.mp3' },
    { n: 'Северный форт', src: 'soundtrack/09-severnyy-fort.mp3' },
    { n: 'Таверна у порта', src: 'soundtrack/10-taverna-u-porta.mp3' }
  ];
  let trEl = null, trIdx = 0, trPlay = false;
  function trackName() { return TRACKS[trIdx].n; }
  function trackPlaying() { return trPlay; }
  function trackEl() {
    if (!trEl && typeof window.Audio === 'function') {
      try {
        trEl = new window.Audio(); trEl.preload = 'none'; trEl.volume = 0.75;
        trEl.addEventListener('ended', () => { // песни не берутся сами: трек кончился — плеер ждёт явного «Слушать»/«Дальше»
          trIdx = (trIdx + 1) % TRACKS.length; trPlay = false;
          if (ready && st.music) startMusic(); // лютня подхватывает, если она включена
        });
        trEl.addEventListener('error', () => { trPlay = false; });
      } catch (e) { trEl = null; }
    }
    return trEl;
  }
  function trackToggle() {
    const el = trackEl(); if (!el) return false;
    if (trPlay) { try { el.pause(); } catch (e) {} trPlay = false; if (ready && st.music) startMusic(); return false; }
    try {
      stopMusic(); // лютня уступает дорогу саундтреку — одновременно играет только одна музыка
      if (!el.src || el.src.indexOf(TRACKS[trIdx].src) < 0) el.src = TRACKS[trIdx].src;
      const pr = el.play(); if (pr && pr.then) pr.catch(() => { trPlay = false; if (ready && st.music) startMusic(); });
      trPlay = true;
    } catch (e) { trPlay = false; if (ready && st.music) startMusic(); }
    return trPlay;
  }
  function trackNext() {
    trIdx = (trIdx + 1) % TRACKS.length;
    const el = trackEl(); if (!el) return;
    try {
      stopMusic();
      try { el.pause(); } catch (e) {}
      el.src = TRACKS[trIdx].src;
      const pr = el.play(); if (pr && pr.then) pr.catch(() => { trPlay = false; if (ready && st.music) startMusic(); });
      trPlay = true;
    } catch (e) { trPlay = false; }
  }
  /* ===== Звуки природы: ветер, птицы, говор людей ===== */
  let _ambT = null, _wind = null;
  function ambStart() {
    if (!ctx || _ambT || !amb) return;
    if (typeof SET !== 'undefined' && SET && !SET.ambient) return;
    try { // тихий ветер — фоновый шум
      const n = (ctx.sampleRate || 44100) * 2, buf = ctx.createBuffer(1, n, ctx.sampleRate || 44100), d = buf.getChannelData(0);
      for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * 0.5;
      const src = ctx.createBufferSource(); src.buffer = buf; src.loop = true;
      const fl = ctx.createBiquadFilter(); fl.type = 'lowpass'; fl.frequency.value = 380;
      const g = ctx.createGain(); g.gain.value = 0.055;
      src.connect(fl); fl.connect(g); g.connect(amb); src.start();
      _wind = { src, g };
    } catch (e) { }
    _ambT = setInterval(function () {
      if (!ctx || (typeof SET !== 'undefined' && SET && !SET.ambient)) return;
      const r = Math.random();
      if (r < 0.6) birds(); else if (r < 0.85) crowd();
    }, 3800);
  }
  function ambStop() {
    if (_ambT) { clearInterval(_ambT); _ambT = null; }
    if (_wind) { try { _wind.src.stop(); } catch (e) { } _wind = null; }
  }
  function _chirp(t, f0, f1, dur, vol) {
    const o = ctx.createOscillator(), g = ctx.createGain();
    o.type = 'sine'; o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(120, f1), t + dur);
    g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(vol, t + 0.015); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    o.connect(g); g.connect(amb); o.start(t); o.stop(t + dur + 0.05);
  }
  function birds() { // птицы поют: 2-4 коротких свиста
    if (!init() || !amb) return;
    const t = ctx.currentTime + 0.05, n = 2 + ((Math.random() * 3) | 0);
    for (let i = 0; i < n; i++) { const s0 = t + i * (0.14 + Math.random() * 0.22); _chirp(s0, 2200 + Math.random() * 1200, 1600 + Math.random() * 1400, 0.09 + Math.random() * 0.1, 0.075); }
  }
  function crowd() { // далёкий говор людей: шумовой всплеск
    if (!init() || !amb) return;
    const t = ctx.currentTime + 0.02, dur = 1.1 + Math.random() * 1.2;
    try {
      const n = (ctx.sampleRate || 44100) * 2, buf = ctx.createBuffer(1, n, ctx.sampleRate || 44100), d = buf.getChannelData(0);
      for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * 0.6;
      const src = ctx.createBufferSource(); src.buffer = buf;
      const fl = ctx.createBiquadFilter(); fl.type = 'bandpass'; fl.frequency.value = 500 + Math.random() * 500; fl.Q.value = 1.2;
      const g = ctx.createGain();
      g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(0.05, t + dur * 0.4); g.gain.linearRampToValueAtTime(0, t + dur);
      src.connect(fl); fl.connect(g); g.connect(amb); src.start(t); src.stop(t + dur + 0.1);
    } catch (e) { }
  }
  function fest() { // фанфары праздника
    if (!init() || !st.sfx) return;
    const t = ctx.currentTime + 0.03, notes = [523, 659, 784, 1046];
    for (let i = 0; i < notes.length; i++) {
      const o = ctx.createOscillator(), g = ctx.createGain(), o2 = ctx.createOscillator();
      o.type = 'triangle'; o2.type = 'sine';
      o.frequency.value = notes[i]; o2.frequency.value = notes[i] * 2;
      const stt = t + i * 0.16;
      g.gain.setValueAtTime(0, stt); g.gain.linearRampToValueAtTime(0.16, stt + 0.03); g.gain.exponentialRampToValueAtTime(0.001, stt + 0.5);
      o.connect(g); o2.connect(g); g.connect(sfx);
      o.start(stt); o.stop(stt + 0.55); o2.start(stt); o2.stop(stt + 0.55);
    }
  }
  return { play, set, unlock, next, songName, state: () => (ctx ? ctx.state : 'none'), trackToggle, trackNext, trackName, trackPlaying, ambStart, ambStop, birds, crowd, fest };
})();
