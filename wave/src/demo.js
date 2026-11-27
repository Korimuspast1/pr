// Офлайн-демо: маленькие синтезированные треки, которые сервер генерирует сам.
//
// Зачем: внешние каталоги (SoundCloud / Jamendo / Archive) требуют сети.
// Когда сети нет (локальная песочница, самолёт, упавший провайдер) сервис
// не должен показывать пустой экран — он отдаёт три демо-трека, и весь путь
// «поиск → карточка → воспроизведение → перемотка» остаётся рабочим.

const SAMPLE_RATE = 22050;

const DEMOS = [
  {
    id: 'night-city',
    title: 'Night City',
    artist: 'Wave Demo',
    genre: 'Synthwave',
    bpm: 92,
    chords: [
      [57, 60, 64], // Am
      [53, 57, 60], // F
      [48, 52, 55], // C
      [55, 59, 62], // G
    ],
    seconds: 24,
  },
  {
    id: 'lofi-desk',
    title: 'Lo-fi для работы',
    artist: 'Wave Demo',
    genre: 'Lo-fi',
    bpm: 74,
    chords: [
      [50, 53, 57], // Dm
      [55, 58, 62], // Gm
      [46, 50, 53], // Bb
      [48, 52, 55], // C
    ],
    seconds: 24,
  },
  {
    id: 'morning-jazz',
    title: 'Morning Jazz',
    artist: 'Wave Demo',
    genre: 'Jazz',
    bpm: 108,
    chords: [
      [53, 57, 60, 64], // Fmaj7
      [52, 55, 59, 62], // Em7
      [50, 53, 57, 60], // Dm7
      [48, 52, 55, 59], // Cmaj7
    ],
    seconds: 24,
  },
];

const cache = new Map();

export function demoStatus() {
  return {
    name: 'demo',
    configured: true,
    note: 'Офлайн-демо: треки генерируются на сервере, работают без интернета',
  };
}

export function demoList() {
  return DEMOS.map((d) => ({
    provider: 'demo',
    id: d.id,
    title: d.title,
    artist: d.artist,
    artwork: null,
    durationSec: d.seconds,
    url: null,
    genre: d.genre,
    playable: true,
    streamUrl: `/api/demo/${d.id}.wav`,
    source: 'Wave Demo',
    license: 'сгенерировано Wave (можно использовать свободно)',
  }));
}

export function hasDemo(id) {
  return DEMOS.some((d) => d.id === id);
}

const midiToHz = (n) => 440 * Math.pow(2, (n - 69) / 12);

function envelope(t, attack, decay, sustain, release, dur) {
  if (t < attack) return t / attack;
  if (t < attack + decay) return 1 - (1 - sustain) * ((t - attack) / decay);
  if (t < dur - release) return sustain;
  const r = Math.max(0, (dur - t) / release);
  return sustain * r;
}

/** Рендерит трек в 16-bit mono PCM WAV. */
export function renderWav(id) {
  if (cache.has(id)) return cache.get(id);

  const spec = DEMOS.find((d) => d.id === id);
  if (!spec) return null;

  const total = SAMPLE_RATE * spec.seconds;
  const buf = new Float32Array(total);
  const beat = 60 / spec.bpm;
  const bar = beat * 4;
  const bars = Math.ceil(spec.seconds / bar);

  for (let b = 0; b < bars; b++) {
    const chord = spec.chords[b % spec.chords.length];
    const barStart = b * bar;

    // пэд: аккорд держится весь такт
    for (const note of chord) {
      const hz = midiToHz(note + 12);
      const from = Math.floor(barStart * SAMPLE_RATE);
      const to = Math.min(total, Math.floor((barStart + bar * 1.02) * SAMPLE_RATE));
      for (let i = from; i < to; i++) {
        const t = (i - from) / SAMPLE_RATE;
        const env = envelope(t, 0.18, 0.5, 0.5, 0.7, bar);
        const phase = (2 * Math.PI * hz * i) / SAMPLE_RATE;
        buf[i] += 0.10 * env * (Math.sin(phase) + 0.25 * Math.sin(phase * 2));
      }
    }

    // бас на первой и третьей доле
    for (const offset of [0, 2]) {
      const hz = midiToHz(chord[0] - 12);
      const from = Math.floor((barStart + offset * beat) * SAMPLE_RATE);
      const dur = beat * 1.8;
      const to = Math.min(total, from + Math.floor(dur * SAMPLE_RATE));
      for (let i = from; i < to; i++) {
        const t = (i - from) / SAMPLE_RATE;
        const env = envelope(t, 0.01, 0.25, 0.35, 0.35, dur);
        buf[i] += 0.22 * env * Math.sin((2 * Math.PI * hz * i) / SAMPLE_RATE);
      }
    }

    // арпеджио восьмыми
    const steps = 8;
    for (let s = 0; s < steps; s++) {
      const note = chord[s % chord.length] + (s >= chord.length ? 12 : 0);
      const hz = midiToHz(note + 12);
      const from = Math.floor((barStart + (s * beat) / 2) * SAMPLE_RATE);
      const dur = beat / 2;
      const to = Math.min(total, from + Math.floor(dur * SAMPLE_RATE));
      for (let i = from; i < to; i++) {
        const t = (i - from) / SAMPLE_RATE;
        const env = envelope(t, 0.005, 0.12, 0.12, 0.14, dur);
        buf[i] += 0.13 * env * Math.sin((2 * Math.PI * hz * i) / SAMPLE_RATE);
      }
    }

    // хэт — короткий шум на каждой доле
    for (let s = 0; s < 4; s++) {
      const from = Math.floor((barStart + s * beat) * SAMPLE_RATE);
      const dur = 0.06;
      const to = Math.min(total, from + Math.floor(dur * SAMPLE_RATE));
      let seed = from % 233280;
      for (let i = from; i < to; i++) {
        seed = (seed * 9301 + 49297) % 233280;
        const noise = seed / 233280 - 0.5;
        const t = (i - from) / SAMPLE_RATE;
        buf[i] += 0.05 * noise * Math.max(0, 1 - t / dur);
      }
    }
  }

  // мягкий лимитер + fade in/out, чтобы не щёлкало
  const fade = Math.floor(SAMPLE_RATE * 0.35);
  let peak = 0;
  for (let i = 0; i < total; i++) peak = Math.max(peak, Math.abs(buf[i]));
  const gain = peak > 0 ? Math.min(1, 0.85 / peak) : 1;
  for (let i = 0; i < total; i++) {
    let v = Math.tanh(buf[i] * gain * 1.2);
    if (i < fade) v *= i / fade;
    if (i > total - fade) v *= (total - i) / fade;
    buf[i] = v;
  }

  const dataBytes = total * 2;
  const header = Buffer.alloc(44);
  header.write('RIFF', 0);
  header.writeUInt32LE(36 + dataBytes, 4);
  header.write('WAVE', 8);
  header.write('fmt ', 12);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20); // PCM
  header.writeUInt16LE(1, 22); // mono
  header.writeUInt32LE(SAMPLE_RATE, 24);
  header.writeUInt32LE(SAMPLE_RATE * 2, 28);
  header.writeUInt16LE(2, 32);
  header.writeUInt16LE(16, 34);
  header.write('data', 36);
  header.writeUInt32LE(dataBytes, 40);

  const pcm = Buffer.alloc(dataBytes);
  for (let i = 0; i < total; i++) {
    const s = Math.max(-1, Math.min(1, buf[i]));
    pcm.writeInt16LE(Math.round(s * 32767), i * 2);
  }

  const wav = Buffer.concat([header, pcm]);
  cache.set(id, wav);
  return wav;
}

/** Отдаёт WAV с поддержкой Range, чтобы работала перемотка. */
export function serveDemo(id, req, res) {
  const wav = renderWav(id);
  if (!wav) {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    return res.end('нет такого демо-трека');
  }

  const range = req.headers.range;
  if (range) {
    const m = /bytes=(\d*)-(\d*)/.exec(range);
    if (m) {
      const start = m[1] ? Number(m[1]) : 0;
      const end = m[2] ? Math.min(Number(m[2]), wav.length - 1) : wav.length - 1;
      if (start >= wav.length || start > end) {
        res.writeHead(416, { 'Content-Range': `bytes */${wav.length}` });
        return res.end();
      }
      const chunk = wav.subarray(start, end + 1);
      res.writeHead(206, {
        'Content-Type': 'audio/wav',
        'Content-Length': chunk.length,
        'Content-Range': `bytes ${start}-${end}/${wav.length}`,
        'Accept-Ranges': 'bytes',
        'Cache-Control': 'public, max-age=86400',
      });
      return res.end(chunk);
    }
  }

  res.writeHead(200, {
    'Content-Type': 'audio/wav',
    'Content-Length': wav.length,
    'Accept-Ranges': 'bytes',
    'Cache-Control': 'public, max-age=86400',
  });
  res.end(wav);
}
