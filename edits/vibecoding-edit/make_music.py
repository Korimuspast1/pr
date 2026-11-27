#!/usr/bin/env python3
"""
Original 60-second track for the edit. 128 BPM -> exactly 128 beats in 60 s.
Everything is synthesized from scratch (no samples), so no copyright issues.

Structure (bars of 4 beats, 1.875 s each):
  bars  0-1   intro pad
  bars  2-7   build  (kick, hats, bass, filtered lead)
  bars  8-15  drop 1
  bars 16-19  breakdown
  bars 20-27  drop 2
  bars 28-31  outro
"""
import numpy as np
import wave, sys

SR = 48000
BPM = 128.0
BEAT = 60.0 / BPM
BARS = 32
TOTAL = int(round(BARS * 4 * BEAT * SR))  # 60.0 s

rng = np.random.default_rng(7)
L = np.zeros(TOTAL)
R = np.zeros(TOTAL)


def add(sig, t, gain=1.0, pan=0.0):
    """mix a mono signal in at time t (seconds) with equal-power pan"""
    i = int(t * SR)
    if i >= TOTAL:
        return
    n = min(len(sig), TOTAL - i)
    gl = gain * np.cos((pan + 1) * np.pi / 4)
    gr = gain * np.sin((pan + 1) * np.pi / 4)
    L[i:i + n] += sig[:n] * gl
    R[i:i + n] += sig[:n] * gr


def env(n, a=0.005, d=0.2, s=0.0, r=0.05, sustain=0.0):
    e = np.zeros(n)
    na, nd = int(a * SR), int(d * SR)
    na = max(na, 1); nd = max(nd, 1)
    e[:min(na, n)] = np.linspace(0, 1, min(na, n))
    k = min(na + nd, n)
    if k > na:
        e[na:k] = np.linspace(1, sustain, k - na)
    if k < n:
        e[k:] = sustain
    return e


def expdec(n, tau):
    return np.exp(-np.arange(n) / (tau * SR))


def saw(f, n, detune=0.0, phase=0.0):
    t = np.arange(n) / SR
    x = ((f * (1 + detune)) * t + phase) % 1.0
    return 2 * x - 1


def lowpass(x, cutoff, order=2):
    """simple one-pole cascade low pass (cheap but fine for a beat)"""
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = x.copy()
    for _ in range(order):
        out = np.empty_like(y)
        acc = 0.0
        # vectorised iir via lfilter-free trick
        b = 1 - a
        acc = 0.0
        for i in range(len(y)):
            acc = b * y[i] + a * acc
            out[i] = acc
        y = out
    return y


def lowpass_fast(x, cutoff):
    """FFT low pass - much faster for long buffers"""
    X = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1 / SR)
    H = 1.0 / np.sqrt(1.0 + (freqs / cutoff) ** 4)
    return np.fft.irfft(X * H, n=len(x))


def highpass_fast(x, cutoff):
    X = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1 / SR)
    H = 1.0 - 1.0 / np.sqrt(1.0 + (freqs / cutoff) ** 4)
    return np.fft.irfft(X * H, n=len(x))


def noise(n):
    return rng.normal(0, 1, n)


def kick(gain=1.0):
    n = int(0.42 * SR)
    t = np.arange(n) / SR
    f = 48 + 150 * np.exp(-t / 0.032)
    ph = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(ph) * expdec(n, 0.11)
    click = noise(n) * expdec(n, 0.004) * 0.35
    return np.tanh((body + click) * 1.6) * gain


def clap(gain=1.0):
    n = int(0.28 * SR)
    body = noise(n) * expdec(n, 0.045)
    body = highpass_fast(body, 1200)
    body = lowpass_fast(body, 6500)
    tail = noise(n) * expdec(n, 0.12) * 0.35
    tail = highpass_fast(tail, 900)
    return (body + tail) * gain


def hat(gain=1.0, decay=0.028):
    n = int(0.09 * SR)
    x = noise(n) * expdec(n, decay)
    return highpass_fast(x, 7500) * gain


def crash(gain=1.0):
    n = int(1.6 * SR)
    x = noise(n) * expdec(n, 0.45)
    return highpass_fast(x, 3000) * gain


def boom(gain=1.0):
    n = int(1.2 * SR)
    t = np.arange(n) / SR
    f = 32 + 60 * np.exp(-t / 0.08)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * expdec(n, 0.35) * gain


def riser(dur, gain=1.0):
    n = int(dur * SR)
    x = noise(n)
    t = np.linspace(0, 1, n)
    # sweeping band emphasis approximated by time-varying low/high pass blend
    lo = highpass_fast(x, 400)
    hi = noise(n)
    out = np.zeros(n)
    for k in range(8):
        c = 500 * (1.13 ** k)
        seg = lowpass_fast(highpass_fast(hi, c * 0.6), c * 1.8)
        out += seg * np.clip(1 - abs(t - k / 7.0) * 3, 0, 1)
    out = out / max(1e-9, np.max(np.abs(out)))
    return out * np.linspace(0.05, 1, n) ** 2 * gain


def pluck(freq, dur, gain=1.0, bright=1.0):
    n = int(dur * SR)
    e = expdec(n, dur * 0.33)
    x = (saw(freq, n, 0.004) + saw(freq, n, -0.005) + 0.6 * saw(freq * 2.0, n, 0.0)) / 2.6
    x = lowpass_fast(x, 900 * bright + 2200 * bright)
    return x * e * gain


def pad(freqs, dur, gain=1.0):
    n = int(dur * SR)
    x = np.zeros(n)
    for i, f in enumerate(freqs):
        x += saw(f, n, 0.003 * (i + 1)) + saw(f, n, -0.003 * (i + 1))
    x /= (2 * len(freqs))
    atk = np.clip(np.linspace(0, 1, n) * 6, 0, 1)
    rel = np.clip(np.linspace(1, 0, n) * 6, 0, 1)
    return lowpass_fast(x, 2600) * atk * rel * gain


def bass_note(freq, dur, gain=1.0):
    n = int(dur * SR)
    x = (saw(freq, n, 0.0) + 0.5 * np.sin(2 * np.pi * freq * np.arange(n) / SR)) / 1.5
    x = lowpass_fast(x, 380)
    e = np.ones(n)
    e *= np.clip(np.linspace(0, 1, n) * 40, 0, 1)
    e *= expdec(n, dur * 0.6)
    return x * e * gain


def mtof(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


# --- harmony: Am - F - C - G, one bar each -------------------------------
prog = [
    (57, [57, 60, 64, 69]),   # Am
    (53, [53, 57, 60, 65]),   # F
    (48, [48, 55, 60, 64]),   # C
    (55, [55, 59, 62, 67]),   # G
]

# --- pad: whole track, sidechained later ---------------------------------
pad_buf = np.zeros(TOTAL)
for bar in range(BARS):
    root, chord = prog[bar % 4]
    freqs = [mtof(m) for m in chord]
    dur = 4 * BEAT
    g = 0.30 if bar < 2 else 0.22
    pad_buf_l = np.zeros(int(dur * SR) + SR)
    seg = pad(freqs, dur + 0.3, g)
    i = int(bar * dur * SR)
    n = min(len(seg), TOTAL - i)
    pad_buf[i:i + n] += seg[:n]

# --- drums ---------------------------------------------------------------
def section(bar):
    if bar < 2:
        return "intro"
    if bar < 8:
        return "build"
    if bar < 16:
        return "drop1"
    if bar < 20:
        return "break"
    if bar < 28:
        return "drop2"
    return "outro"


for bar in range(BARS):
    t0 = bar * 4 * BEAT
    sec = section(bar)
    full = sec in ("drop1", "drop2")
    building = sec == "build"
    out = sec == "outro"

    if sec == "intro":
        if bar == 1:
            for b in (0, 2):
                add(hat(0.18), t0 + b * BEAT, pan=0.2)
        continue

    # kick
    if full or building or out:
        for b in range(4):
            if building and bar < 4:
                if b in (0, 2):
                    add(kick(0.9), t0 + b * BEAT)
            else:
                add(kick(0.95), t0 + b * BEAT)
        if full and bar % 4 == 3:
            # syncopated extra kick before the next bar
            add(kick(0.8), t0 + 3.5 * BEAT)

    # clap on 2 & 4
    if (full or out) and not building:
        for b in (1, 3):
            add(clap(0.45), t0 + b * BEAT, pan=-0.05)

    # hats: 8ths, 16th roll on last beat of every 4th bar
    if not (sec == "break"):
        for k in range(8):
            g = 0.16 if k % 2 else 0.1
            add(hat(g), t0 + k * 0.5 * BEAT, pan=0.25 if k % 2 else -0.2)
        if bar % 4 == 3 and not out:
            for k in range(4):
                add(hat(0.13, decay=0.02), t0 + 3 * BEAT + k * 0.25 * BEAT, pan=0.3)

    # bass: 8ths, root with octave pops
    if full or building or out:
        root, _ = prog[bar % 4]
        pattern = [0, 0, 12, 0, 0, 12, 0, 7]
        for k, off in enumerate(pattern):
            g = 0.5 if full else (0.34 if building else 0.3)
            add(bass_note(mtof(root - 12 + off), 0.5 * BEAT * 0.92, g),
                t0 + k * 0.5 * BEAT)

    # lead arpeggio 16ths
    if full or sec == "break":
        root, chord = prog[bar % 4]
        seq = [0, 2, 1, 3, 2, 1, 3, 2]
        for k in range(16):
            m = chord[seq[k % len(seq)]] + 12
            g = 0.15 if full else 0.2
            add(pluck(mtof(m), 0.16 if full else 0.30, g, bright=1.0),
                t0 + k * 0.25 * BEAT, pan=0.15 * (1 if k % 2 else -1))

# --- transitions ---------------------------------------------------------
add(riser(2 * 4 * BEAT, 0.34), (8 - 2) * 4 * BEAT)     # into drop 1
add(riser(2 * 4 * BEAT, 0.40), (20 - 2) * 4 * BEAT)    # into drop 2
for bt in (8, 20, 28):
    add(boom(0.85), bt * 4 * BEAT)
    add(crash(0.28), bt * 4 * BEAT, pan=0.1)
add(boom(0.5), 0.0)
add(crash(0.12), 16 * 4 * BEAT)                         # breakdown marker

# --- sidechain everything to the kick ------------------------------------
side = np.ones(TOTAL)
for bar in range(BARS):
    if section(bar) in ("build", "drop1", "drop2", "outro") and not (section(bar) == "build" and bar < 4):
        for b in range(4):
            i = int((bar * 4 + b) * BEAT * SR)
            n = int(0.22 * SR)
            if i + n < TOTAL:
                side[i:i + n] *= np.linspace(0.45, 1.0, n) ** 1.6

pad_buf = pad_buf * side
L += pad_buf * 0.9
R += pad_buf * 0.9

L *= side
R *= side

# --- simple stereo delay on the lead-ish content -------------------------
def pingpong(x, delay_s=0.75 * BEAT, fb=0.28, mix=0.18):
    d = int(delay_s * SR)
    y = x.copy()
    for k in range(1, 4):
        sh = np.zeros_like(x)
        sh[d * k:] = x[:-d * k] * (fb ** k)
        y += sh * mix
    return y

L2 = pingpong(L)
R2 = pingpong(R)
L = 0.85 * L + 0.15 * L2
R = 0.85 * R + 0.15 * R2

# --- master: high-pass, soft clip, fades, normalise ----------------------
L = highpass_fast(L, 28)
R = highpass_fast(R, 28)

L = np.tanh(L * 1.25) / np.tanh(1.25)
R = np.tanh(R * 1.25) / np.tanh(1.25)

fi = int(0.02 * SR)
fo = int(0.9 * SR)
for ch in (L, R):
    ch[:fi] *= np.linspace(0, 1, fi)
    ch[-fo:] *= np.linspace(1, 0, fo) ** 1.5

peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
L = L / peak * 0.90
R = R / peak * 0.90

out = np.stack([L, R], axis=1)
pcm = (np.clip(out, -1, 1) * 32767).astype("<i2")

path = sys.argv[1] if len(sys.argv) > 1 else "music.wav"
with wave.open(path, "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(pcm.tobytes())

print(f"wrote {path}: {len(pcm)/SR:.2f}s  rms={np.sqrt(np.mean(out**2)):.3f}")
