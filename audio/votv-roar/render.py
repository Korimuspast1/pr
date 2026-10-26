#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Процедурный звук в духе "Earth-Shaking Roar" из Voices of the Void.

Это ОРИГИНАЛЬНЫЙ саунд-дизайн: никакие файлы из игры не используются.
Всё синтезируется с нуля: суб-басовый рёв с падающим питчем, формантный
"звериный" тембр, дисторшн, дыхание/шипение, инфразвуковая "тряска",
коричневый шум ветра, радио-статик, металлические призвуки и convolution-реверб.

Запуск:
    python render.py                 # ogg + mp3 эмбиент-луп, wav + mp3 одиночный рёв
    python render.py --wav-loop     # дополнительно несжатый WAV луп (для движков)
    python render.py --seed 12345   # другой вариант (другой шум/панорамирование)

Зависимости: numpy, scipy, soundfile, lameenc
    pip install numpy scipy soundfile lameenc
"""

from __future__ import annotations

import argparse
from pathlib import Path

import lameenc
import numpy as np
import soundfile as sf
from scipy import signal

SR = 44100                     # Гц
LOOP_SECONDS = 84.0            # итоговая длина бесшовного лупа
CROSSFADE = 6.0                # зона склейки лупа, с
OUT = Path(__file__).resolve().parent


# ----------------------------------------------------------------------------
# Базовые DSP-кирпичики
# ----------------------------------------------------------------------------

def colored_noise(n: int, rng: np.random.Generator, alpha: float = 1.0) -> np.ndarray:
    """Шум со спектром ~1/f^alpha (0 = белый, 1 = розовый, 2 = коричневый)."""
    n = int(n)
    white = rng.standard_normal(n)
    spec = np.fft.rfft(white)
    freqs = np.maximum(np.fft.rfftfreq(n, d=1.0 / SR), 20.0)
    spec *= freqs ** (-alpha)
    out = np.fft.irfft(spec, n=n)
    out /= np.std(out) + 1e-12
    return out.astype(np.float32)


def _filt(x: np.ndarray, kind: str, cutoff, order: int = 4) -> np.ndarray:
    sos = signal.butter(order, cutoff, btype=kind, fs=SR, output="sos")
    axis = 0 if x.ndim > 1 else -1
    return signal.sosfilt(sos, x, axis=axis).astype(np.float32)


def lp(x, hz, order=4):
    return _filt(x, "lowpass", hz, order)


def hp(x, hz, order=4):
    return _filt(x, "highpass", hz, order)


def bp(x, lo, hi, order=4):
    return _filt(x, "bandpass", [lo, hi], order)


def norm(x: np.ndarray) -> np.ndarray:
    return (x / (np.std(x) + 1e-12)).astype(np.float32)


def shift(x: np.ndarray, samples: int) -> np.ndarray:
    out = np.zeros_like(x)
    if samples:
        out[samples:] = x[:-samples]
    return out


def make_ir(rng: np.random.Generator, decay: float = 3.2, damp: float = 2400.0) -> np.ndarray:
    """Стерео impulse response: затухающий шум + ранние отражения."""
    n = int(decay * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)).astype(np.float32)
    ir[:, 0] = lp(ir[:, 0], damp)
    ir[:, 1] = lp(ir[:, 1], damp * 0.86)
    for ch in (0, 1):
        for delay in (0.017, 0.029, 0.043, 0.067, 0.099, 0.137):
            idx = int((delay + 0.005 * ch) * SR)
            if idx < n:
                ir[idx, ch] += rng.uniform(-1.0, 1.0) * 0.7
    ir *= np.exp(-t / (decay / 4.2))[:, None]
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    return ir


def reverb(x: np.ndarray, rng: np.random.Generator, decay=3.2, mix=0.3) -> np.ndarray:
    ir = make_ir(rng, decay)
    wet = np.stack(
        [signal.oaconvolve(x[:, ch], ir[:, ch], mode="full")[: len(x)] for ch in (0, 1)],
        axis=1,
    ).astype(np.float32)
    # выравниваем громкость «мокрого» сигнала по сухому
    dry_rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    wet_rms = np.sqrt(np.mean(wet ** 2)) + 1e-9
    wet *= (dry_rms / wet_rms) * 1.15
    return ((1.0 - mix) * x + mix * wet).astype(np.float32)


def master(x: np.ndarray, peak=0.89) -> np.ndarray:
    x = hp(x, 24.0, 2)
    # мягкий компрессор на базе огибающей (сглаживание ~8 Гц)
    env = lp(np.abs(x), 8.0, 2)
    threshold = 0.22
    ratio = 3.0
    safe_env = np.maximum(env, 1e-9)
    gain = np.where(env > threshold, (threshold + (env - threshold) / ratio) / safe_env, 1.0)
    gain = np.clip(lp(gain, 60.0, 2), 0.25, 1.0)
    x = x * gain
    x = np.tanh(x * 1.12).astype(np.float32)
    x /= max(np.max(np.abs(x)) / peak, 1e-9)
    return x


def to_int16(x: np.ndarray) -> np.ndarray:
    # TPDF-дизер перед 16-битным квантованием
    rng = np.random.default_rng(7)
    d = (rng.random(x.shape).astype(np.float32) - 0.5) / 32767.0
    return np.clip(np.round((x + d) * 32767.0), -32768, 32767).astype("<i2")


CHUNK = 32768  # libsndfile/lame не любят слишком большие одиночные буферы


def write_wav(path: Path, x: np.ndarray) -> None:
    pcm = to_int16(x)
    with sf.SoundFile(str(path), mode="w", samplerate=SR, channels=2, subtype="PCM_16") as f:
        for i in range(0, len(pcm), CHUNK):
            f.write(pcm[i:i + CHUNK])


def write_mp3(path: Path, x: np.ndarray, bitrate=192) -> None:
    enc = lameenc.Encoder()
    enc.set_bit_rate(bitrate)
    enc.set_in_sample_rate(SR)
    enc.set_channels(2)
    enc.set_quality(2)
    pcm = to_int16(x)
    parts = []
    for i in range(0, len(pcm), CHUNK):
        parts.append(enc.encode(pcm[i:i + CHUNK].tobytes()))
    parts.append(enc.flush())
    path.write_bytes(b"".join(parts))


def write_ogg(path: Path, x: np.ndarray) -> None:
    # OGG/Vorbis не имеет энкодерных «хвостов» — луп остаётся gapless
    with sf.SoundFile(str(path), mode="w", samplerate=SR, channels=2,
                      format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(x), CHUNK):
            f.write(x[i:i + CHUNK])


# ----------------------------------------------------------------------------
# Рёв
# ----------------------------------------------------------------------------

def formant_weight(f: float) -> float:
    peaks = ((120, 95, 1.00), (320, 140, 0.78), (740, 210, 0.52),
             (1520, 360, 0.30), (2900, 620, 0.15))
    return float(sum(a * np.exp(-0.5 * ((f - c) / w) ** 2) for c, w, a in peaks) + 0.04)


def make_roar(dur=7.0, tail=4.0, f_start=190.0, f_end=38.0,
              rng=None, intensity=1.0, shriek=0.7) -> np.ndarray:
    """Один «землетрясущий» рёв. Возвращает стерео float32 длиной dur+tail.

    Структура: удар на вдохе -> резкий питч-даун ~200->35 Гц -> несколько
    дыхательных толчков -> низкое урчание -> надрывный визг -> умирающие
    всхлипы в ревербе. Всё это даёт не «шумовую стену», а живую тварь.
    """
    rng = rng or np.random.default_rng(0)
    n = int((dur + tail) * SR)
    t = np.arange(n) / SR

    # --- питч: короткий подъём на вдохе, затем крутой обвал в суб-бас
    u = np.clip(t / (0.22 * dur), 0.0, 1.0)
    rise = 0.60 + 0.40 * np.sin(np.pi * 0.5 * u)
    fall = 0.74 * np.exp(-t / (0.19 * dur)) + 0.26 * np.exp(-t / (1.1 * dur))
    inst_f = (f_end + (f_start - f_end) * fall) * rise
    inst_f = inst_f.astype(np.float64)
    inst_f *= 1.0 + 0.075 * np.sin(2 * np.pi * (3.3 * t + 0.14 * np.sin(2 * np.pi * 0.9 * t)))
    inst_f += norm(lp(colored_noise(n, rng, 1.3), 9.0)) * 4.0
    inst_f = np.clip(inst_f, 16.0, 420.0)
    phase = 2.0 * np.pi * np.cumsum(inst_f) / SR
    phase += 0.16 * norm(lp(colored_noise(n, rng, 0.6), 260.0))

    # --- огибающая: отдельные «выдохи» вместо прямоугольного плато
    env = np.zeros(n, dtype=np.float32)
    for center, width, amp in ((0.09, 0.060, 0.60), (0.27, 0.095, 0.92),
                               (0.47, 0.115, 1.00), (0.68, 0.105, 0.74),
                               (0.87, 0.075, 0.40)):
        env += (amp * np.exp(-0.5 * ((t - center * dur) / (width * dur)) ** 2)).astype(np.float32)
    env /= env.max()
    env *= 1.0 - np.exp(-t / 0.10)                      # атака вдоха
    tail_mask = t >= dur
    env[tail_mask] *= np.exp(-(t[tail_mask] - dur) / (tail / 3.5))
    # дребезг горла ~18 Гц
    env *= (0.74 + 0.26 * np.sin(2 * np.pi * (17.5 * t + 0.5 * np.sin(2 * np.pi * 2.4 * t))))

    # --- гармонический рык: 3 spectral band с движущимися формантами
    f_avg = float(np.sqrt(f_start * f_end))
    raw = np.zeros(n, dtype=np.float32)
    for h in range(1, 90):
        if h * f_avg > 5200:
            break
        raw += ((1.0 / h ** 0.76) * np.sin(h * phase + rng.uniform(0, 6.28))).astype(np.float32)
    raw = np.tanh(norm(raw) * 2.9)
    dark = norm(lp(raw, 270.0))
    mid = norm(bp(raw, 230.0, 1200.0))
    bright = norm(bp(raw, 950.0, 4400.0))
    wd = 0.55 + 0.40 * np.exp(-((t - 0.85 * dur) / (0.55 * dur)) ** 2)
    wm = 0.55 + 0.45 * np.exp(-((t - 0.35 * dur) / (0.38 * dur)) ** 2)
    wb = 0.30 + 0.70 * np.exp(-((t - 0.17 * dur) / (0.19 * dur)) ** 2)
    tone = norm(0.95 * wd * dark + 0.80 * wm * mid + 0.52 * wb * bright)
    tone *= (0.78 + 0.22 * np.sin(2 * np.pi * (13.0 * t + 0.3 * np.sin(2 * np.pi * 2.1 * t))))

    # --- дыхание / шипящая глотка (три формантных полосы + гранулярный храп)
    src = colored_noise(n, rng, 0.7)
    b_lo, b_mid, b_hi = bp(src, 180, 500), bp(src, 550, 1400), bp(src, 1500, 4200)
    breath = (0.75 * b_lo / (np.std(b_lo) + 1e-9)
              + 0.55 * b_mid / (np.std(b_mid) + 1e-9)
              + 0.30 * b_hi / (np.std(b_hi) + 1e-9)).astype(np.float32)
    breath *= 0.62 + 0.38 * np.sin(2 * np.pi * 6.8 * t + 0.8)
    breath *= 0.72 + 0.28 * np.abs(norm(lp(colored_noise(n, rng, 0.5), 60.0)))

    # --- надрывный нечеловеческий визг на пике
    scream = np.zeros(n, dtype=np.float32)
    if shriek > 0:
        s_f = 430.0 + 1250.0 * np.exp(-0.5 * ((t - 0.42 * dur) / (0.20 * dur)) ** 2)
        s_f += 110.0 * np.sin(2 * np.pi * 1.7 * t)
        s_ph = 2 * np.pi * np.cumsum(s_f) / SR
        s_win = np.exp(-0.5 * ((t - 0.42 * dur) / (0.21 * dur)) ** 2)
        scream = (np.sin(s_ph) + 0.35 * np.sin(2.7 * s_ph)
                  + 0.22 * np.sin(4.1 * s_ph)).astype(np.float32)
        scream += 0.45 * norm(hp(colored_noise(n, rng, 0.3), 1800.0))
        scream *= (s_win * 0.16 * shriek
                   * (0.76 + 0.24 * np.sin(2 * np.pi * 11.0 * t))).astype(np.float32)

    # --- суб-октава, инфразвук «тряски» и стартовый удар
    sub = np.tanh(1.7 * np.sin(0.5 * phase)).astype(np.float32)
    earth = (np.sin(2 * np.pi * 27.0 * t)
             * (0.6 + 0.4 * np.sin(2 * np.pi * 3.1 * t))).astype(np.float32)
    rumble = np.tanh(2.4 * norm(lp(colored_noise(n, rng, 2.0), 80.0))).astype(np.float32)
    impact = np.zeros(n, dtype=np.float32)
    ln = int(0.32 * SR)
    tt = np.arange(ln) / SR
    impact[:ln] = (0.55 * np.sin(2 * np.pi * 47 * tt) * np.exp(-tt / 0.085)
                   + 0.28 * norm(colored_noise(ln, rng, 0.4)) * np.exp(-tt / 0.045)).astype(np.float32)

    x = (0.98 * tone + 0.36 * breath + 0.78 * sub + 0.30 * earth
         + 0.40 * rumble + 0.55 * impact + scream)
    x = np.tanh(1.32 * x).astype(np.float32)
    x *= env * intensity
    x = hp(x, 24.0, 2)
    x = lp(x, 6800.0, 2)

    # --- умирающие всхлипы в хвосте
    for frac, amp in ((0.16, 0.15), (0.45, 0.09), (0.74, 0.05)):
        pos = int((dur + frac * tail) * SR)
        gl = min(int(0.40 * SR), n - pos)
        if gl <= 0:
            continue
        gt = np.arange(gl) / SR
        gasp = norm(bp(colored_noise(gl, rng, 0.6), 260, 2600)) * np.exp(-gt / 0.12)
        x[pos:pos + gl] += (amp * gasp).astype(np.float32)

    # --- лёгкая стерео-декорреляция + объём
    left = x + 0.26 * shift(x, int(0.009 * SR))
    right = x + 0.26 * shift(x, int(0.014 * SR))
    stereo = np.stack([left, right], axis=1)
    stereo = reverb(stereo, rng, decay=3.4, mix=0.32)
    return stereo.astype(np.float32)


def pan(stereo: np.ndarray, p: float) -> np.ndarray:
    """p: -1 = слева, +1 = справа (equal-power)."""
    ang = (p + 1.0) * np.pi / 4.0
    return (stereo * np.array([np.cos(ang), np.sin(ang)], dtype=np.float32)).astype(np.float32)


# ----------------------------------------------------------------------------
# Фон (тёмный эмбиент)
# ----------------------------------------------------------------------------

def build_bed(n: int, rng: np.random.Generator) -> np.ndarray:
    t = np.arange(n) / SR
    bed = np.zeros((n, 2), dtype=np.float32)
    air = np.zeros((n, 2), dtype=np.float32)

    # 1. Дрон: диссонирующее созвучие E1–F1–B1 с медленным биением
    drone = np.zeros(n, dtype=np.float32)
    for f, a in ((41.20, 0.105), (43.65, 0.085), (61.74, 0.055),
                 (82.41, 0.032), (123.47, 0.016)):
        lfo = 0.5 + 0.5 * np.sin(2 * np.pi * (0.031 + 0.017 * rng.random()) * t
                                 + rng.uniform(0, 6.28)) ** 2
        drift = 0.22 * np.sin(2 * np.pi * 0.047 * t + rng.uniform(0, 6.28))
        drone += (a * lfo * np.sin(2 * np.pi * f * t + drift)).astype(np.float32)
    drone = np.tanh(drone * 2.2) * 0.55
    bed[:, 0] += drone
    bed[:, 1] += 0.92 * np.roll(drone, 317) + (0.018 * np.sin(2 * np.pi * 41.31 * t)).astype(np.float32)

    # 2. Ветер: коричневый шум, медленные порывы
    wind = norm(lp(colored_noise(n, rng, 1.7), 300.0))
    gust = (0.28 + 0.40 * np.sin(2 * np.pi * 0.043 * t + 1.1) ** 2
            + 0.24 * np.sin(2 * np.pi * 0.019 * t + 0.3) ** 2)
    bed[:, 0] += 0.085 * wind * gust
    bed[:, 1] += 0.080 * np.roll(norm(lp(colored_noise(n, rng, 1.7), 260.0)), 1201) * np.roll(gust, 997)

    # 3. Радио-статик + редкие потрескивания (фирменная «радио»-атмосфера VotV)
    hiss = norm(bp(colored_noise(n, rng, 0.4), 1300, 6500))
    gate = np.clip(np.sin(2 * np.pi * 0.087 * t + 0.7), 0, 1) ** 5
    air[:, 0] += 0.019 * hiss * (0.06 + gate)
    air[:, 1] += 0.017 * np.roll(norm(bp(colored_noise(n, rng, 0.4), 1100, 6000)), 733) * (0.06 + np.roll(gate, 431))
    crackle = np.zeros(n, dtype=np.float32)
    for _ in range(110):
        pos = rng.integers(0, n - int(0.1 * SR))
        ln = int(rng.uniform(0.004, 0.045) * SR)
        seg = norm(colored_noise(ln, rng, 0.2)) * np.exp(-np.linspace(0, 8, ln))
        crackle[pos:pos + ln] += (rng.uniform(0.006, 0.022) * seg).astype(np.float32)
    air[:, rng.integers(0, 2)] += crackle

    # 4. Приглушённые «голоса» в шуме — почти шёпот, но без слов
    whisper = norm(bp(colored_noise(n, rng, 0.9), 380, 1700))
    wenv = np.clip(0.5 + 0.5 * np.sin(2 * np.pi * 0.071 * t + 2.2)
                   + 0.3 * np.sin(2 * np.pi * 0.023 * t), 0, 1) ** 3
    air[:, 0] += 0.024 * whisper * wenv
    air[:, 1] += 0.020 * np.roll(whisper, 2113) * np.roll(wenv, 613)

    # 5. Редкие металлические призвуки с длинным затуханием
    for _ in range(9):
        pos = int(rng.uniform(0.04, 0.93) * n)
        ln = min(int(rng.uniform(2.5, 5.5) * SR), n - pos)
        tt = np.arange(ln) / SR
        f = rng.choice((287.0, 431.0, 619.0, 947.0, 1301.0))
        ping = np.sin(2 * np.pi * (f * tt + 0.4 * np.sin(2 * np.pi * 0.6 * tt)))
        ping = (rng.uniform(0.010, 0.028) * ping * np.exp(-tt / 1.0)).astype(np.float32)
        ch = rng.integers(0, 2)
        air[pos:pos + ln, ch] += ping

    # 6. Глухие суб-удары, как пульс чего-то под землёй
    thumps = np.zeros(n, dtype=np.float32)
    pos = int(1.5 * SR)
    while pos < n - int(1.5 * SR):
        ln = int(0.55 * SR)
        tt = np.arange(ln) / SR
        f = rng.uniform(28, 36)
        thumps[pos:pos + ln] += (rng.uniform(0.08, 0.17) * np.sin(2 * np.pi * f * tt)
                                 * np.exp(-tt / 0.16)).astype(np.float32)
        pos += int(rng.uniform(3.4, 6.2) * SR)
    bed[:, 0] += thumps
    bed[:, 1] += np.roll(thumps, 53)

    bed += reverb(air, rng, decay=3.8, mix=0.34) * 1.0
    return bed.astype(np.float32)


# ----------------------------------------------------------------------------
# Сборка лупа
# ----------------------------------------------------------------------------

EVENTS = (  # (старт, длительность, f_start, f_end, громкость, панорама, визг)
    (7.5, 6.0, 175.0, 42.0, 0.56, -0.38, 0.50),
    (28.0, 7.5, 205.0, 34.0, 0.76, 0.22, 0.95),
    (50.0, 6.5, 160.0, 39.0, 0.64, -0.16, 0.70),
    (68.0, 5.5, 130.0, 30.0, 0.38, 0.52, 0.30),
)


def build_loop(rng: np.random.Generator, wav_loop=False) -> dict:
    n = int((LOOP_SECONDS + CROSSFADE) * SR)
    t = np.arange(n) / SR
    bed = build_bed(n, rng)
    roars = np.zeros((n, 2), dtype=np.float32)
    presence = np.zeros(n, dtype=np.float32)

    for start, dur, fs, fe, gain, p, shriek in EVENTS:
        r = make_roar(dur=dur, tail=4.2, f_start=fs, f_end=fe,
                      rng=rng, intensity=gain, shriek=shriek)
        r = pan(r, p)
        pos = int(start * SR)
        end = min(pos + len(r), n)
        roars[pos:end] += r[:end - pos]

        # предупреждающий нарастающий инфразвуковой гул перед рёвом
        pre_n = int(1.8 * SR)
        pre = np.zeros((n, 2), dtype=np.float32)
        pp = max(pos - pre_n, 0)
        ln = pos - pp
        tt = np.arange(ln) / SR
        swell = (0.10 * gain * np.sin(2 * np.pi * 29 * tt)
                 * np.linspace(0, 1, ln) ** 2).astype(np.float32)
        pre[pp:pos, 0] += swell
        pre[pp:pos, 1] += 0.9 * swell
        bed += pre

        # маска «присутствия» для даккинга фона и тряски
        m = np.exp(-0.5 * ((t - (start + 0.45 * dur)) / (0.42 * dur + 1.3)) ** 2)
        presence = np.maximum(presence, m.astype(np.float32) * gain)

    bed *= (1.0 - 0.38 * presence)[:, None]
    mix = bed + roars

    # «тряска»: амплитудная модуляция всего микса во время рёва
    shake = 1.0 - 0.11 * presence * (0.5 + 0.5 * np.sin(2 * np.pi * 6.7 * t))
    mix *= shake[:, None].astype(np.float32)
    mix = master(mix)

    # бесшовная склейка: последние CROSSFADE секунд подмешиваются в начало
    f = int(CROSSFADE * SR)
    loop = mix[:-f].copy()
    fi = np.linspace(0, 1, f, dtype=np.float32)
    loop[:f] = mix[:f] * fi[:, None] + mix[-f:] * (1 - fi)[:, None]

    result = {"loop": loop}
    if wav_loop:
        result["loop_wav"] = loop
    return result


def build_oneshot(rng: np.random.Generator) -> np.ndarray:
    x = make_roar(dur=6.2, tail=4.6, f_start=200.0, f_end=36.0,
                  rng=rng, intensity=1.0, shriek=1.0)
    x = pan(x, 0.0)
    x = master(x, peak=0.95)
    # пара миллисекунд на краях — защита от щелчка
    k = int(0.008 * SR)
    x[:k] *= np.linspace(0, 1, k)[:, None].astype(np.float32)
    x[-k:] *= np.linspace(1, 0, k)[:, None].astype(np.float32)
    return x


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=20261026)
    ap.add_argument("--wav-loop", action="store_true", help="дополнительно WAV-версия лупа")
    args = ap.parse_args()
    rng = np.random.default_rng(args.seed)
    OUT.mkdir(parents=True, exist_ok=True)

    print("[1/2] синтез эмбиент-лупа ...")
    loop = build_loop(np.random.default_rng(args.seed + 1))["loop"]
    write_ogg(OUT / "earth-shaking-roar-ambient.ogg", loop)
    write_mp3(OUT / "earth-shaking-roar-ambient.mp3", loop)
    if args.wav_loop:
        write_wav(OUT / "earth-shaking-roar-ambient-loop.wav", loop)

    print("[2/2] синтез одиночного рёва ...")
    shot = build_oneshot(rng)
    write_wav(OUT / "earth-shaking-roar-oneshot.wav", shot)
    write_mp3(OUT / "earth-shaking-roar-oneshot.mp3", shot)

    print(f"готово: {OUT}")
    print(f"луп {len(loop)/SR:.1f} c | peak {np.max(np.abs(loop)):.3f} | RMS {np.sqrt(np.mean(loop**2)):.4f}")
    print(f"рёв  {len(shot)/SR:.1f} c | peak {np.max(np.abs(shot)):.3f} | RMS {np.sqrt(np.mean(shot**2)):.4f}")


if __name__ == "__main__":
    main()
