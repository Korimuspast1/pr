#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Страшный рёв на 6 секунд — процедурный саунд-дизайн, без ассетов из игр.

Модель «источник + фильтр» (как у настоящей глотки):
  * источник — когерентный гармонический ряд (все гармоники в фазе!) с
    питч-дауном ~265 -> 38 Гц, vocal fry (субгармоника f0/2), джиттером;
  * фильтр — 6 формантных наборов (гласные U/A/E/O/U/R) на каскадных
    резонаторах iirpeak, между которыми звук плавно морфится;
  * сверху — отдельный визговый голос, дыхание, суб-бас, инфразвук 29 Гц,
    стартовый удар, реверб.

Запуск:
    pip install numpy scipy soundfile lameenc
    python render.py                 # 6-секундный WAV + MP3 + OGG
    python render.py --seed 777      # другой вариант рыка

Выход: scary-roar-6s.wav / .mp3 / .ogg (44.1 кГц, стерео, ровно 6.000 с).
"""

from __future__ import annotations

import argparse
from pathlib import Path

import lameenc
import numpy as np
import soundfile as sf
from scipy import signal

SR = 44100
DUR = 6.0
N = int(DUR * SR)
OUT = Path(__file__).resolve().parent
CHUNK = 32768

# F1/F2/F3 — форманты «глотки» (сдвинуты вниз, чтобы звучало крупнее человека)
VOWELS = {
    "U": (320, 680, 2100),    # закрытый, давящий
    "A": (720, 1150, 2600),   # открытый крик
    "E": (480, 1750, 2500),   # визгливый
    "O": (460, 820, 2350),    # круглый рык
    "R": (220, 520, 1600),    # низкое урчание
    "S": (980, 2250, 3450),   # визговый слой
}
# (доля длительности, гласная, ширина морфа)
STAGES = (
    (0.04, "U", 0.105),
    (0.17, "A", 0.110),
    (0.35, "E", 0.105),
    (0.55, "O", 0.115),
    (0.74, "U", 0.115),
    (0.93, "R", 0.120),
)


# ---------------------------------------------------------------------------
# Базовые кирпичики
# ---------------------------------------------------------------------------

def colored_noise(n: int, rng: np.random.Generator, alpha: float = 1.0) -> np.ndarray:
    """Шум со спектром ~1/f^alpha, нормализованный по СКО."""
    n = int(n)
    spec = np.fft.rfft(rng.standard_normal(n))
    freqs = np.maximum(np.fft.rfftfreq(n, d=1.0 / SR), 20.0)
    spec *= freqs ** (-alpha)
    out = np.fft.irfft(spec, n=n)
    return (out / (np.std(out) + 1e-12)).astype(np.float32)


def _filt(x, kind, cutoff, order=4):
    sos = signal.butter(order, cutoff, btype=kind, fs=SR, output="sos")
    axis = 0 if np.asarray(x).ndim > 1 else -1
    return signal.sosfilt(sos, x, axis=axis).astype(np.float32)


def lp(x, hz, order=4):
    return _filt(x, "lowpass", hz, order)


def hp(x, hz, order=4):
    return _filt(x, "highpass", hz, order)


def bp(x, lo, hi, order=4):
    return _filt(x, "bandpass", [lo, hi], order)


def norm(x):
    return (x / (np.std(x) + 1e-12)).astype(np.float32)


def shift(x, k):
    out = np.zeros_like(x)
    out[k:] = x[:-k]
    return out


def gauss(t, center, width):
    return np.exp(-0.5 * ((t - center) / width) ** 2).astype(np.float32)


def formant_filter(src: np.ndarray, freqs, qs=(7.0, 8.0, 9.0)) -> np.ndarray:
    """Каскад резонаторов — голосовой тракт с заданными формантами."""
    out = src.astype(np.float32).copy()
    for f, q in zip(freqs, qs):
        b, a = signal.iirpeak(float(f), float(q), fs=SR)
        out = signal.lfilter(b, a, out).astype(np.float32)
    return norm(out)


def make_ir(rng, decay=2.8, damp=2600.0):
    n = int(decay * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)).astype(np.float32)
    ir[:, 0] = lp(ir[:, 0], damp)
    ir[:, 1] = lp(ir[:, 1], damp * 0.88)
    for ch in (0, 1):
        for d in (0.017, 0.031, 0.047, 0.071, 0.103):
            idx = int((d + 0.004 * ch) * SR)
            if idx < n:
                ir[idx, ch] += rng.uniform(-1, 1) * 0.65
    ir *= np.exp(-t / (decay / 4.2))[:, None]
    return (ir / (np.sqrt(np.sum(ir ** 2)) + 1e-12)).astype(np.float32)


def reverb(x, rng, decay=2.8, mix=0.30):
    ir = make_ir(rng, decay)
    wet = np.stack(
        [signal.oaconvolve(x[:, ch], ir[:, ch], mode="full")[: len(x)] for ch in (0, 1)],
        axis=1,
    ).astype(np.float32)
    wet *= (np.sqrt(np.mean(x ** 2)) + 1e-9) / (np.sqrt(np.mean(wet ** 2)) + 1e-9) * 1.1
    return ((1 - mix) * x + mix * wet).astype(np.float32)


def master(x, peak=0.95):
    """Без компрессии: динамика рыка важнее громкости. Только мягкий лимитер."""
    x = hp(x, 24.0, 2)
    a = np.abs(x)
    x = np.where(a < 0.68, x,
                 np.sign(x) * (0.68 + 0.32 * np.tanh((a - 0.68) / 0.32))).astype(np.float32)
    return (x / max(np.max(np.abs(x)) / peak, 1e-9)).astype(np.float32)


def to_int16(x):
    rng = np.random.default_rng(7)
    d = (rng.random(x.shape).astype(np.float32) - 0.5) / 32767.0
    return np.clip(np.round((x + d) * 32767.0), -32768, 32767).astype("<i2")


def write_wav(path, x):
    pcm = to_int16(x)
    with sf.SoundFile(str(path), mode="w", samplerate=SR, channels=2, subtype="PCM_16") as f:
        for i in range(0, len(pcm), CHUNK):
            f.write(pcm[i:i + CHUNK])


def write_mp3(path, x, bitrate=192):
    enc = lameenc.Encoder()
    enc.set_bit_rate(bitrate)
    enc.set_in_sample_rate(SR)
    enc.set_channels(2)
    enc.set_quality(2)
    pcm = to_int16(x)
    parts = [enc.encode(pcm[i:i + CHUNK].tobytes()) for i in range(0, len(pcm), CHUNK)]
    parts.append(enc.flush())
    path.write_bytes(b"".join(parts))


def write_ogg(path, x):
    with sf.SoundFile(str(path), mode="w", samplerate=SR, channels=2,
                      format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(x), CHUNK):
            f.write(x[i:i + CHUNK])


# ---------------------------------------------------------------------------
# Рёв
# ---------------------------------------------------------------------------

def build_roar(rng: np.random.Generator) -> np.ndarray:
    t = np.arange(N) / SR
    u = t / DUR

    # --- 1. ПИТЧ: короткий вдох, обвал в суб-бас, два дополнительных толчка-крика
    f = 35.0 + 160.0 * np.exp(-u / 0.20)
    f += 82.0 * gauss(u, 0.33, 0.095)
    f += 52.0 * gauss(u, 0.59, 0.085)
    f = f.astype(np.float64)
    f *= 1.0 + 0.065 * np.sin(2 * np.pi * (4.1 * t + 0.2 * np.sin(2 * np.pi * 0.8 * t)))
    f += norm(lp(colored_noise(N, rng, 1.3), 12.0)) * 3.5
    f = np.clip(f, 22.0, 520.0)
    cycles = np.cumsum(f) / SR
    phase = 2.0 * np.pi * cycles

    # --- 2. ИСТОЧНИК: когерентный гармонический ряд (без случайных фаз!)
    raw = np.zeros(N, dtype=np.float32)
    for h in range(1, 56):
        raw += ((1.0 / h ** 0.82)
                * np.sin(h * phase + 0.11 * np.sin(2 * np.pi * 0.63 * t + 0.06 * h))).astype(np.float32)
    raw = lp(norm(raw), 3300.0, 4)   # режем лишние ВЧ-гармоники
    raw = norm(raw)
    # vocal fry: субгармоника f0/2 даёт «звериное» клокотание
    raw *= (0.67 + 0.33 * np.cos(np.pi * cycles + 0.6 * np.sin(2 * np.pi * 1.3 * t)))
    # джиттер/шиммер — живая нестабильность связок
    raw *= 1.0 + 0.20 * norm(lp(colored_noise(N, rng, 1.0), 70.0))
    raw = norm(raw)

    # --- 3. ФОРМАНТНЫЙ МОРФ: U -> A -> E -> O -> U -> R
    filtered = {name: formant_filter(raw, freqs) for name, freqs in VOWELS.items()}
    weights = []
    for center, name, width in STAGES:
        weights.append(gauss(u, center, width) + 0.015)
    wsum = np.sum(weights, axis=0) + 1e-9
    vocal = np.zeros(N, dtype=np.float32)
    for (_, name, _), w in zip(STAGES, weights):
        vocal += (w / wsum * filtered[name]).astype(np.float32)
    vocal = np.tanh(norm(vocal) * 1.18)          # лёгкий перегруз «в мясо»

    # --- 4. ОТДЕЛЬНЫЙ ВИЗГ (второй, высокий голос поверх рыка)
    f_s = 560.0 + 470.0 * gauss(u, 0.40, 0.13) - 190.0 * gauss(u, 0.62, 0.11)
    ph_s = 2 * np.pi * np.cumsum(f_s) / SR
    sh = np.zeros(N, dtype=np.float32)
    for h in range(1, 11):
        sh += ((1.0 / h ** 0.72) * np.sin(h * ph_s + 0.25 * np.sin(2 * np.pi * 5.0 * t))).astype(np.float32)
    sh = formant_filter(sh, VOWELS["S"], qs=(6.0, 7.0, 8.0))
    sh *= gauss(u, 0.41, 0.19) * (0.76 + 0.24 * np.sin(2 * np.pi * 11.5 * t)) * 0.46

    # --- 4b. ЧЕТКИЙ СРЕДНИЙ ТОН: гармоники 3..11 дают слышимый «голос»
    mid_tone = np.zeros(N, dtype=np.float32)
    for h in range(3, 12):
        mid_tone += ((1.0 / h ** 0.70) * np.sin(h * phase + 0.08 * np.sin(2 * np.pi * 0.9 * t))).astype(np.float32)
    mid_tone = norm(lp(mid_tone, 1100.0, 4))

    # --- 4c. ВДОХ перед рыком: короткий нарастающий шумовой свист
    inhale = norm(bp(colored_noise(N, rng, 0.8), 500, 2600))
    inhale *= (np.clip(t / 0.42, 0, 1) ** 2 * np.exp(-t / 0.20) * 0.16).astype(np.float32)

    # --- 5. ДЫХАНИЕ (не главный звук, а «шершавость» сверху)
    src_n = colored_noise(N, rng, 0.7)
    breath = (0.75 * bp(src_n, 190, 620) / (np.std(bp(src_n, 190, 620)) + 1e-9)
              + 0.50 * bp(src_n, 780, 2100) / (np.std(bp(src_n, 780, 2100)) + 1e-9)
              + 0.26 * bp(src_n, 2600, 6200) / (np.std(bp(src_n, 2600, 6200)) + 1e-9)).astype(np.float32)
    breath *= 0.60 + 0.40 * np.sin(2 * np.pi * 7.2 * t + 0.7)
    breath *= 0.75 + 0.25 * np.abs(norm(lp(colored_noise(N, rng, 0.5), 60.0)))

    # --- 6. НИЗ: суб-октава, инфразвук «тряски», коричневый гул, удар
    sub = np.tanh(1.9 * np.sin(0.5 * phase)).astype(np.float32)
    earth = (np.sin(2 * np.pi * 29.0 * t)
             * (0.55 + 0.45 * np.sin(2 * np.pi * 3.4 * t))).astype(np.float32)
    rumble = np.tanh(2.1 * norm(lp(colored_noise(N, rng, 2.0), 90.0))).astype(np.float32)
    impact = np.zeros(N, dtype=np.float32)
    ln = int(0.28 * SR)
    tt = np.arange(ln) / SR
    impact[:ln] = (0.62 * np.sin(2 * np.pi * 52 * tt) * np.exp(-tt / 0.075)
                   + 0.34 * norm(colored_noise(ln, rng, 0.4)) * np.exp(-tt / 0.042)).astype(np.float32)

    # --- 7. ОГИБАЮЩАЯ: выдохи-толчки + дребезг горла + чистый финал
    knots_t = np.array([0.00, 0.04, 0.13, 0.28, 0.46, 0.85, 1.35, 1.70, 1.95,
                        2.35, 2.75, 3.05, 3.35, 3.85, 4.25, 4.60, 5.10, 5.55, 6.00])
    knots_a = np.array([0.00, 0.88, 0.95, 0.34, 0.58, 0.96, 1.00, 0.60, 0.40,
                        0.90, 0.93, 0.46, 0.30, 0.74, 0.66, 0.44, 0.24, 0.09, 0.00])
    env = np.interp(t, knots_t, knots_a).astype(np.float32)
    env = signal.sosfilt(signal.butter(2, 26.0, fs=SR, output="sos"), env).astype(np.float32)
    env *= (0.84 + 0.16 * np.sin(2 * np.pi * (16.5 * t + 0.35 * np.sin(2 * np.pi * 2.2 * t))))

    # --- 8. СБОРКА
    x = (1.35 * vocal + 0.38 * mid_tone + 0.15 * breath + 0.52 * sub + 0.22 * earth
         + 0.10 * rumble + 0.42 * impact + sh + inhale)
    x = hp(x, 25.0, 2)
    x = lp(x, 6500.0, 2)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.88
    x *= env

    # предсмертный всхлип перед самым концом
    gs = int(4.95 * SR)
    gl = N - gs
    gt = np.arange(gl) / SR
    gasp = norm(bp(colored_noise(gl, rng, 0.6), 300, 2400)) * np.exp(-gt / 0.13)
    x[gs:] += (0.10 * gasp).astype(np.float32)

    # --- 9. СТЕРЕО + РЕВЕРБ + МАСТЕР
    left = x + 0.23 * shift(x, int(0.008 * SR))
    right = x + 0.23 * shift(x, int(0.013 * SR))
    stereo = reverb(np.stack([left, right], axis=1), rng, decay=2.8, mix=0.24)
    stereo = master(stereo)
    # гарантированно тихие края — без щелчков
    k = int(0.012 * SR)
    stereo[:k] *= np.linspace(0, 1, k)[:, None].astype(np.float32)
    stereo[-k:] *= np.linspace(1, 0, k)[:, None].astype(np.float32)
    return stereo


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=20261026)
    args = ap.parse_args()
    rng = np.random.default_rng(args.seed)
    OUT.mkdir(parents=True, exist_ok=True)

    x = build_roar(rng)
    write_wav(OUT / "scary-roar-6s.wav", x)
    write_mp3(OUT / "scary-roar-6s.mp3", x)
    write_ogg(OUT / "scary-roar-6s.ogg", x)

    rms = float(np.sqrt(np.mean(x ** 2)))
    print(f"готово: {len(x)/SR:.3f} с | peak {np.max(np.abs(x)):.3f} | RMS {rms:.4f}")


if __name__ == "__main__":
    main()
