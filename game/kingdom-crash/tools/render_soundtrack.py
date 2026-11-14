#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Рендер саундтрека Kingdom Crash из мелодий игры (порт audio.js: щипок
Карплуса-СТРОНГA, бас, боевые эффекты). Пишет www/soundtrack/*.mp3
(дорожки для внутриигрового плеера и веса APK) и WAV-микс для трейлера.

Запуск: .cache/apk-venv/bin/python tools/render_soundtrack.py [--trailer-only]
"""
import os, sys, struct, wave, subprocess
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
WWW = os.path.join(HERE, '..', 'www')
SND_DIR = os.path.join(WWW, 'soundtrack')
SR = 44100
FFMPEG = os.environ.get('FFMPEG_BIN') or '/home/user/pr/.cache/apk-venv/lib/python3.11/site-packages/imageio_ffmpeg/binaries/ffmpeg-linux-x86_64-v9.0.2'

# ---- мелодии игры (SONGS из audio.js: темп, бас, ноты [MIDI, доли]) ----
SONGS = [
    { 'n': 'Пляска', 'file': '01-plyaska', 'beat': 60 / 104, 'bassEvery': 3, 'gap': 21,
      'mel': [
        [62, 1], [65, 1], [69, 1], [67, 1], [65, .5], [64, .5], [62, 1], [65, 1], [69, 1], [72, 1], [71, .5], [69, .5], [67, 1], [69, 1],
        [74, 1], [72, .5], [71, .5], [69, 1], [67, 1], [69, .5], [71, .5], [72, 1], [69, 1], [67, .5], [65, .5], [64, 1], [62, 2], [0, 1],
        [62, .5], [64, .5], [65, 1], [69, 1], [67, 1], [64, 1], [60, 1], [65, .5], [67, .5], [69, 1], [74, 1], [72, 1], [69, 1], [65, 1],
        [67, 1], [71, .5], [69, .5], [67, 1], [65, 1], [64, 1], [62, 1], [64, .5], [65, .5], [64, 1], [60, 1], [62, 3]],
      'bass': [50, 50, 53, 55, 50, 55, 57, 50, 50, 48, 53, 53, 55, 50, 45, 50] },
    { 'n': 'Вечерняя баллада', 'file': '02-vechernyaya-ballada', 'beat': 60 / 76, 'bassEvery': 4, 'gap': 24,
      'mel': [
        [69, 1.5], [72, .5], [71, 1], [69, 1], [67, 1.5], [64, .5], [65, 2], [0, .5],
        [64, 1], [65, 1], [67, 1], [69, 1.5], [72, .5], [74, 1], [72, 1], [71, 1.5], [69, .5], [67, 2], [0, .5],
        [69, 1], [71, 1], [72, 1], [74, 1.5], [76, .5], [74, 1], [72, 1], [71, 1], [69, 1], [67, 1], [65, 1], [64, 2], [0, 1],
        [62, 1], [64, 1], [65, 1], [67, 1.5], [65, .5], [64, 1], [62, 3], [0, 1]],
      'bass': [45, 48, 50, 43, 45, 41, 45, 40] },
    { 'n': 'Марш дружины', 'file': '03-marsh-druzhiny', 'beat': 60 / 112, 'bassEvery': 2, 'gap': 16,
      'mel': [
        [67, 1], [67, .5], [69, .5], [70, 1], [67, 1], [65, 1], [67, 2], [0, .5], [67, .5],
        [70, 1], [70, .5], [72, .5], [74, 1], [70, 1], [67, 1], [70, 2], [0, .5], [70, .5],
        [74, 1], [72, .5], [70, .5], [72, 1], [70, 1], [67, .5], [65, .5], [67, 1], [70, 1], [67, 1], [65, 1], [62, 1], [67, 3]],
      'bass': [55, 52, 55, 50, 55, 52, 50, 45] },
    { 'n': 'Ярмарка', 'file': '04-yarmarka', 'beat': 60 / 126, 'bassEvery': 3, 'gap': 18,
      'mel': [
        [65, .5], [67, .5], [69, .5], [72, .5], [74, 1], [72, .5], [69, .5], [70, 1], [69, .5], [67, .5], [65, 1], [67, 1],
        [69, .5], [72, .5], [74, .5], [77, .5], [76, 1], [74, .5], [72, .5], [74, 1], [72, .5], [69, .5], [67, 1], [65, 1],
        [64, .5], [65, .5], [67, .5], [69, .5], [70, 1], [69, .5], [67, .5], [65, 1], [67, .5], [69, .5], [65, 1], [64, 1], [65, 3]],
      'bass': [53, 53, 55, 57, [53, 55], 53, 50] if False else [53, 53, 55, 57, 53, 55, 53, 50] },
]

_PLUCKS = {}

def pluck(midi, detune=0.0):
    """Щипок струны — точный порт pluck() из audio.js."""
    key = (midi, round(detune, 4))
    if key in _PLUCKS:
        return _PLUCKS[key]
    f = 440.0 * (2.0 ** ((midi - 69) / 12.0)) * (1.0 + detune)
    N = max(2, int(round(SR / f)))
    ln = int(SR * 1.8)
    rng = np.random.default_rng(midi * 7919 + int((detune + 1) * 1000) + 13)
    line = np.zeros(N)
    prev = 0.0
    for i in range(N):
        prev = prev * 0.5 + (rng.random() * 2.0 - 1.0) * 0.5
        line[i] = prev
    damp = 0.994 if midi > 64 else 0.996
    out = np.empty(ln)
    k = 0
    for i in range(ln):
        a = line[k]
        out[i] = a
        line[k] = (a + line[(k + 1) % N]) * 0.5 * damp
        k = (k + 1) % N
    fade = int(SR * 0.1)
    out[ln - fade:] *= np.linspace(1.0, 0.0, fade)
    _PLUCKS[key] = out
    return out

def add_note(buf, midi, t, vol, detune=0.0):
    p = pluck(midi, detune)
    i0 = int(t * SR)
    i1 = min(len(buf), i0 + len(p))
    if i0 >= len(buf) or i1 <= i0:
        return
    buf[i0:i1] += p[:i1 - i0] * vol

def schroeder_reverb(x, mix=0.16):
    """Лёгкий реверб: 4 гребёнки + 2 allpass (последовательно блоками)."""
    out = np.zeros_like(x)
    for D, g in [(1557, 0.78), (1617, 0.76), (1491, 0.745), (1422, 0.73)]:
        y = np.zeros_like(x)
        y[:D] = x[:D]
        for i in range(D, len(x), D):
            j = min(i + D, len(x))
            y[i:j] = x[i:j] + g * y[i - D:j - D]
        out += y * 0.25
    for D, g in [(225, 0.7), (556, 0.7)]:
        y = np.zeros_like(out)
        y[:D] = -g * out[:D]
        for i in range(D, len(out), D):
            j = min(i + D, len(out))
            y[i:j] = -g * out[i:j] + out[i - D:j - D] + g * y[i - D:j - D]
        out = y
    return x * (1 - mix) + out * mix

def render_song(song, duration):
    """Мелодия + бас, L/R с лёгким расстроем — стерео, как в игре."""
    n = int(duration * SR)
    L = np.zeros(n)
    R = np.zeros(n)
    mel = song['mel']
    bass = song['bass']
    beat = song['beat']
    t = 0.0
    idx = 0
    nb = 0
    t_pass = 0
    rng = np.random.default_rng(42)
    while t < duration:
        m = mel[idx]
        up = 12 if t_pass % 2 == 1 else 0
        if nb % song['bassEvery'] == 0:
            b = bass[(nb // song['bassEvery']) % len(bass)]
            add_note(L, b, t, 0.5 * 0.62, -0.0035)
            add_note(R, b, t, 0.5 * 0.62, 0.0035)
            add_note(L, b + 7, t + beat * 2, 0.2 * 0.62, -0.0035)
            add_note(R, b + 7, t + beat * 2, 0.2 * 0.62, 0.0035)
        if m[0]:
            v = (0.4 + rng.random() * 0.1) * 0.62
            add_note(L, m[0] + up, t, v, -0.0035)
            add_note(R, m[0] + up, t, v, 0.0035)
        t += m[1] * beat
        idx += 1
        nb += 1
        if idx >= len(mel):
            idx = 0
            nb = 0
            t_pass += 1
            if t_pass % 2 == 0:
                t += beat * song['bassEvery'] * 2  # короткая пауза между проходами
            else:
                t += beat * song['gap'] * 0.06
    L = schroeder_reverb(L, 0.14)
    R = schroeder_reverb(R, 0.14)
    return L, R

# ---- эффекты игры (FX из audio.js: те же частоты и огибающие) ----
def tone(buf, f, at, dur, wave='sine', vol=0.2, f2=None):
    n = int(dur * SR)
    if n <= 0:
        return
    i0 = int(at * SR)
    i1 = min(len(buf), i0 + n)
    n = i1 - i0
    if n <= 0:
        return
    freqs = np.linspace(f, f2 if f2 else f, n)
    ph = np.cumsum(2 * np.pi * freqs / SR)
    if wave == 'square':
        w = np.sign(np.sin(ph))
    elif wave == 'triangle':
        w = np.abs(np.mod(ph / np.pi, 2) - 1) * 2 - 1
    elif wave == 'sawtooth':
        w = np.mod(ph / np.pi, 2) - 1
    else:
        w = np.sin(ph)
    env = np.ones(n)
    a = max(1, int(0.008 * SR))
    env[:a] = np.linspace(0.0001, 1, a)
    env *= np.exp(-np.linspace(0, 5.5, n))
    buf[i0:i1] += w * env * vol

def noise_fx(buf, at, dur, vol, freq, q=1.0, f2=None):
    """Полосковый шум (приближение biquad bandpass из игры)."""
    n = int(dur * SR)
    if n <= 0:
        return
    i0 = int(at * SR)
    i1 = min(len(buf), i0 + n)
    n = i1 - i0
    if n <= 0:
        return
    rng = np.random.default_rng(int(at * 1000) + int(freq))
    x = rng.standard_normal(n)
    # сглаживание ≈ lowpass, разница ≈ highpass; комбинация даёт полосу
    k = max(2, int(SR / max(200.0, freq)))
    sm = np.convolve(x, np.ones(k) / k, mode='same')
    band = x - sm
    if f2:
        k2 = max(2, int(SR / max(200.0, f2)))
        sm2 = np.convolve(x, np.ones(k2) / k2, mode='same')
        t = np.linspace(0, 1, n)
        band = band * (1 - t) + (x - sm2) * t
    env = np.ones(n)
    a = max(1, int(0.005 * SR))
    env[:a] = np.linspace(0.0001, 1, a)
    env *= np.exp(-np.linspace(0, 5, n))
    buf[i0:i1] += band * env * vol

def horn_fx(buf, at, f, dur, vol):
    n = int(dur * SR)
    i0 = int(at * SR)
    i1 = min(len(buf), i0 + n)
    n = i1 - i0
    if n <= 0:
        return
    t = np.arange(n) / SR
    w = np.sign(np.sin(2 * np.pi * f * t)) * 0.5 + np.sign(np.sin(2 * np.pi * f * 1.006 * t)) * 0.5
    # огибающая как в horn(): атака, плато, спад
    env = np.interp(t, [0, 0.1, max(0.11, dur - 0.18), dur], [0.0001, 1, 1, 0.0001])
    k = max(2, int(SR / 500))  # грубый lowpass — тембр рога
    w = np.convolve(w, np.ones(k) / k, mode='same')
    buf[i0:i1] += w * env * vol

def fx_whoosh(buf, at, vol=0.5):
    noise_fx(buf, at, 0.45, vol, 900, 1.2, 2400)

def fx_clash(buf, at, vol=0.16):
    noise_fx(buf, at, 0.06, vol, 3800 + (int(at * 997) % 1500), 4)
    tone(buf, 1500 + (int(at * 733) % 900), at, 0.07, 'triangle', 0.05)

def fx_horn(buf, at):
    horn_fx(buf, at, 174.6, 0.7, 0.32)
    horn_fx(buf, at + 0.72, 233.1, 1.1, 0.36)

def fx_arrow(buf, at):
    noise_fx(buf, at, 0.1, 0.12, 3200, 1.5, 1200)

def fx_coin(buf, at):
    tone(buf, 1568, at, 0.07, 'triangle', 0.14)
    tone(buf, 2093, at + 0.07, 0.16, 'triangle', 0.14)

def fx_win(buf, at):
    for i, f in enumerate([523.3, 659.3, 784, 1046.5, 784, 1046.5]):
        tone(buf, f, at + i * 0.16, 0.9 if i == 5 else 0.24, 'triangle', 0.2)
    horn_fx(buf, at, 130.8, 1.9, 0.12)

def write_wav(path, L, R):
    data = np.stack([L, R], axis=1)
    peak = np.max(np.abs(data)) or 1.0
    data = data / peak * 0.89
    pcm = (data * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())

def to_mp3(wav_path, mp3_path, kbps=128):
    subprocess.run([FFMPEG, '-y', '-hide_banner', '-loglevel', 'error',
                    '-i', wav_path, '-codec:a', 'libmp3lame', '-b:a', f'{kbps}k',
                    '-ar', '44100', '-ac', '2', mp3_path], check=True)

def main():
    trailer_only = '--trailer-only' in sys.argv
    os.makedirs(SND_DIR, exist_ok=True)
    os.makedirs('/tmp/promo', exist_ok=True)
    if not trailer_only:
        for s in SONGS:
            wav = f"/tmp/promo/{s['file']}.wav"
            mp3 = os.path.join(SND_DIR, s['file'] + '.mp3')
            print('♪', s['n'], '...')
            L, R = render_song(s, 182)
            write_wav(wav, L, R)
            to_mp3(wav, mp3, 128)
            print('  →', mp3, os.path.getsize(mp3), 'байт')
    # ---- микс для трейлера (10 c) ----
    print('♪ микс трейлера ...')
    n = int(10.0 * SR)
    L = np.zeros(n)
    R = np.zeros(n)
    # сегменты мелодий (обрывки — те же ноты игры)
    for (song_i, t0, dur) in [(0, 0.0, 2.75), (2, 2.55, 3.35), (1, 5.75, 2.45), (3, 8.0, 2.0)]:
        s = SONGS[song_i]
        segL, segR = render_song(s, dur)
        i0 = int(t0 * SR)
        i1 = min(n, i0 + len(segL))
        L[i0:i1] += segL[:i1 - i0] * 0.9
        R[i0:i1] += segR[:i1 - i0] * 0.9
    for buf in (L, R):
        fx_whoosh(buf, 0.0, 0.35)
        fx_whoosh(buf, 2.55, 0.5)
        fx_horn(buf, 2.62)
        for t in [3.3, 3.7, 4.15, 4.55, 5.0, 5.35]:
            fx_clash(buf, t, 0.2)
        fx_arrow(buf, 3.05)
        fx_arrow(buf, 4.7)
        fx_whoosh(buf, 5.75, 0.5)
        fx_whoosh(buf, 7.98, 0.45)
        fx_coin(buf, 8.05)
        fx_win(buf, 8.35)
    # плавный конец
    fade = int(0.7 * SR)
    for buf in (L, R):
        buf[n - fade:] *= np.linspace(1, 0, fade)
    write_wav('/tmp/promo/trailer_audio.wav', L, R)
    print('  → /tmp/promo/trailer_audio.wav')

if __name__ == '__main__':
    main()
