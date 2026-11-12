#!/usr/bin/env python3
"""Generate an MP4 where every on-screen sentence has at least 15 words.

The whole video is produced by code: the frames are drawn with Pillow and
piped as raw RGB into ffmpeg, and the quiet ambient pad under them is
synthesised sample by sample with the standard library only.

Requirements:
    pip install pillow imageio-ffmpeg

Usage:
    python3 media/sentences_video/make_video.py
    python3 media/sentences_video/make_video.py --out custom.mp4 --fps 30
"""

from __future__ import annotations

import argparse
import math
import struct
import subprocess
import sys
import tempfile
import wave
from dataclasses import dataclass
from pathlib import Path
from typing import List, Sequence, Tuple

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

# --------------------------------------------------------------------------
# Content
# --------------------------------------------------------------------------

MIN_WORDS = 15

SENTENCES: List[str] = [
    "Утренний свет медленно наполнял комнату, и казалось, что весь город "
    "ещё спит, забыв о своих обещаниях.",
    "Старый маяк стоял на краю скалы, и каждую ночь его луч напоминал "
    "рыбакам о доме.",
    "Мы редко замечаем, как много тихих решений мы принимаем каждый день, "
    "просто чтобы остаться собой.",
    "Хороший код похож на чистую комнату: в нём легко найти нужную вещь "
    "и приятно находиться.",
    "Зимой в Хельсинки темнеет рано, и окна домов загораются медленно, "
    "как будто город засыпает вместе с тобой.",
    "Ветер принёс запах моря, и на секунду показалось, что время можно "
    "остановить и просто дышать.",
]

TITLE = "Предложения"
TITLE_SUB = "каждое — не короче пятнадцати слов"
OUTRO_TITLE = "Сгенерировано кодом"
OUTRO_SUB = "Python + Pillow + ffmpeg"

# --------------------------------------------------------------------------
# Look and feel
# --------------------------------------------------------------------------

W, H = 1920, 1080
FPS = 30

MARGIN_X = 180
TEXT_MAX_W = W - 2 * MARGIN_X
BODY_SIZE = 66
LINE_GAP = 1.30

BG_TOP = (9, 13, 26)
BG_BOTTOM = (20, 27, 49)
INK = (244, 246, 255)
MUTED = (146, 158, 190)

ACCENTS = [
    (255, 184, 107),
    (127, 209, 255),
    (183, 155, 255),
    (110, 231, 183),
    (255, 143, 163),
    (255, 209, 102),
]

FONT_CANDIDATES = [
    Path("/usr/share/fonts/truetype/dejavu"),
    Path("/usr/share/fonts/TTF"),
    Path("/System/Library/Fonts/Supplemental"),
    Path("C:/Windows/Fonts"),
]


def _find_font(name: str) -> Path:
    for folder in FONT_CANDIDATES:
        candidate = folder / name
        if candidate.exists():
            return candidate
    raise SystemExit(
        f"Font {name} not found. Install DejaVu fonts or point FONT_CANDIDATES "
        "at a directory that contains them."
    )


FONT_BOLD = _find_font("DejaVuSans-Bold.ttf")
FONT_REGULAR = _find_font("DejaVuSans.ttf")

INTRO_SEC = 3.6
SCENE_SEC = 5.8
OUTRO_SEC = 4.2
REVEAL_STAGGER = 0.11
WORD_FADE = 0.50
SCENE_FADE_IN = 0.35
SCENE_FADE_OUT = 0.45


# --------------------------------------------------------------------------
# Small helpers
# --------------------------------------------------------------------------


def ease_out_cubic(x: float) -> float:
    x = min(max(x, 0.0), 1.0)
    return 1.0 - (1.0 - x) ** 3


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def mix(c1: Sequence[int], c2: Sequence[int], t: float) -> Tuple[int, int, int]:
    return tuple(int(round(lerp(c1[i], c2[i], t))) for i in range(3))  # type: ignore[return-value]


def with_alpha(color: Sequence[int], alpha: float) -> Tuple[int, int, int, int]:
    return (color[0], color[1], color[2], int(round(255 * min(max(alpha, 0.0), 1.0))))


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(path), size)


def make_background() -> Image.Image:
    """Vertical gradient, two soft light blobs, and a vignette."""
    base = Image.new("RGB", (W, H))
    draw = ImageDraw.Draw(base)
    for y in range(H):
        draw.line([(0, y), (W, y)], fill=mix(BG_TOP, BG_BOTTOM, y / (H - 1)))

    glow = Image.new("RGB", (W, H), (0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    gdraw.ellipse([-420, -560, 900, 560], fill=(46, 66, 128))
    gdraw.ellipse([W - 780, H - 460, W + 380, H + 420], fill=(58, 46, 124))
    glow = glow.filter(ImageFilter.GaussianBlur(220))
    base = ImageChops.screen(base, glow)

    # Vignette: bright in the middle, falling off towards the corners.
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).ellipse(
        [-W * 0.28, -H * 0.42, W * 1.28, H * 1.42], fill=255
    )
    mask = mask.filter(ImageFilter.GaussianBlur(260))
    return Image.composite(base, Image.new("RGB", (W, H), (3, 5, 12)), mask)


@dataclass
class WordTile:
    image: Image.Image
    x: int
    y: int
    start: float


def wrap_words(words: Sequence[str], fnt: ImageFont.FreeTypeFont,
               max_w: int) -> List[List[str]]:
    probe = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    lines: List[List[str]] = []
    current: List[str] = []
    for word in words:
        candidate = " ".join(current + [word])
        if probe.textlength(candidate, font=fnt) <= max_w or not current:
            current.append(word)
        else:
            lines.append(current)
            current = [word]
    if current:
        lines.append(current)
    return lines


def build_word_tiles(sentence: str, fnt: ImageFont.FreeTypeFont,
                     color: Sequence[int]) -> List[WordTile]:
    probe = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    space_w = probe.textlength(" ", font=fnt)
    ascent, descent = fnt.getmetrics()
    line_h = int((ascent + descent) * LINE_GAP)

    lines = wrap_words(sentence.split(), fnt, TEXT_MAX_W)
    block_h = line_h * len(lines)
    top = H // 2 - block_h // 2

    tiles: List[WordTile] = []
    index = 0
    for row, line in enumerate(lines):
        line_w = probe.textlength(" ".join(line), font=fnt)
        x = (W - line_w) / 2
        y = top + row * line_h
        for word in line:
            pad = 6
            bbox = probe.textbbox((0, 0), word, font=fnt)
            tw = int(bbox[2] - bbox[0]) + pad * 2
            th = int(bbox[3] - bbox[1]) + pad * 2
            tile = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
            tdraw = ImageDraw.Draw(tile)
            tdraw.text((pad - bbox[0], pad - bbox[1]), word,
                       font=fnt, fill=(color[0], color[1], color[2], 255))
            tiles.append(
                WordTile(
                    image=tile,
                    x=int(round(x)) - pad,
                    y=int(round(y)) - pad,
                    start=index * REVEAL_STAGGER,
                )
            )
            x += probe.textlength(word, font=fnt) + space_w
            index += 1
    return tiles


_ALPHA_LUTS: dict = {}


def alpha_lut(alpha: float) -> List[int]:
    key = int(round(alpha * 100))
    lut = _ALPHA_LUTS.get(key)
    if lut is None:
        lut = [int(round(v * key / 100)) for v in range(256)]
        _ALPHA_LUTS[key] = lut
    return lut


def draw_chip(draw: ImageDraw.Draw, xy: Tuple[int, int], text: str,
              fnt: ImageFont.FreeTypeFont, fg: Sequence[int],
              bg: Sequence[int]) -> None:
    pad_x, pad_y = 22, 12
    bbox = draw.textbbox((0, 0), text, font=fnt)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x, y = xy
    draw.rounded_rectangle(
        [x, y, x + tw + pad_x * 2, y + th + pad_y * 2],
        radius=(th + pad_y * 2) // 2,
        fill=(bg[0], bg[1], bg[2], bg[3] if len(bg) > 3 else 255),
    )
    draw.text((x + pad_x - bbox[0], y + pad_y - bbox[1]), text, font=fnt, fill=fg)


# --------------------------------------------------------------------------
# Scenes
# --------------------------------------------------------------------------


def render_intro(t: float, bg: Image.Image, total_words: int) -> Image.Image:
    frame = bg.copy()
    draw = ImageDraw.Draw(frame, "RGBA")
    ease = ease_out_cubic(t / 0.9)
    dy = int((1 - ease) * 26)
    alpha = ease

    f_big = font(FONT_BOLD, 132)
    f_sub = font(FONT_REGULAR, 46)
    f_chip = font(FONT_BOLD, 26)

    title = TITLE
    bbox = draw.textbbox((0, 0), title, font=f_big)
    draw.text(((W - (bbox[2] - bbox[0])) / 2 - bbox[0], H / 2 - 190 + dy),
              title, font=f_big, fill=with_alpha(INK, alpha))

    sub_ease = ease_out_cubic((t - 0.45) / 0.9)
    sub_bbox = draw.textbbox((0, 0), TITLE_SUB, font=f_sub)
    draw.text(
        ((W - (sub_bbox[2] - sub_bbox[0])) / 2 - sub_bbox[0],
         H / 2 - 6 + int((1 - sub_ease) * 20)),
        TITLE_SUB,
        font=f_sub,
        fill=with_alpha(MUTED, sub_ease),
    )

    chip_ease = ease_out_cubic((t - 0.8) / 0.8)
    chip_text = f"{len(SENTENCES)} ПРЕДЛОЖЕНИЙ · {total_words} СЛОВ"
    bbox = draw.textbbox((0, 0), chip_text, font=f_chip)
    chip_w = bbox[2] - bbox[0] + 44
    draw_chip(draw, (int((W - chip_w) / 2), int(H / 2 + 96)),
              chip_text, f_chip, with_alpha(ACCENTS[0], chip_ease),
              with_alpha((255, 255, 255), 0.05 * chip_ease))
    return frame


def render_sentence(index: int, t: float, bg: Image.Image,
                    tiles: List[WordTile], accent: Sequence[int],
                    sentence: str) -> Image.Image:
    frame = bg.copy()
    draw = ImageDraw.Draw(frame, "RGBA")

    f_chip = font(FONT_BOLD, 26)
    f_meta = font(FONT_REGULAR, 30)

    # Accent hairline above the text block.
    line_ease = ease_out_cubic(t / 0.7)
    line_w = 220 * line_ease
    draw.rounded_rectangle(
        [(W - line_w) / 2, H / 2 - 210, (W + line_w) / 2, H / 2 - 205],
        radius=3,
        fill=with_alpha(accent, 0.85),
    )

    # Word-by-word reveal.
    for tile in tiles:
        p = (t - tile.start) / WORD_FADE
        if p <= 0:
            continue
        a = ease_out_cubic(p)
        img = tile.image
        if a < 0.999:
            img = img.copy()
            img.putalpha(img.getchannel("A").point(alpha_lut(a)))
        frame.paste(img, (tile.x, tile.y + int((1 - a) * 16)), img)

    # Header chips.
    draw_chip(draw, (MARGIN_X, 92), f"{index + 1:02d} / {len(SENTENCES):02d}",
              f_chip, with_alpha(INK, 0.92), with_alpha((255, 255, 255), 0.07))
    count = len(sentence.split())
    count_text = f"{count} СЛОВ"
    bbox = draw.textbbox((0, 0), count_text, font=f_chip)
    chip_w = bbox[2] - bbox[0] + 44
    draw_chip(draw, (W - MARGIN_X - chip_w, 92), count_text,
              f_chip, with_alpha(accent, 0.95), with_alpha(accent, 0.12))

    # Footer caption.
    caption = "СГЕНЕРИРОВАНО КОДОМ"
    draw.text((MARGIN_X, H - 118), caption, font=f_meta,
              fill=with_alpha(MUTED, 0.75))

    # Progress bar.
    draw.rounded_rectangle(
        [MARGIN_X, H - 62, W - MARGIN_X, H - 58], radius=2,
        fill=with_alpha((255, 255, 255), 0.10),
    )
    progress = (index + min(t / SCENE_SEC, 1.0)) / len(SENTENCES)
    fill_w = (W - 2 * MARGIN_X) * progress
    if fill_w > 2:
        draw.rounded_rectangle(
            [MARGIN_X, H - 62, MARGIN_X + fill_w, H - 58], radius=2,
            fill=with_alpha(accent, 0.9),
        )
    return frame


def render_outro(t: float, bg: Image.Image, total_words: int) -> Image.Image:
    frame = bg.copy()
    draw = ImageDraw.Draw(frame, "RGBA")
    ease = ease_out_cubic(t / 0.9)

    f_big = font(FONT_BOLD, 96)
    f_sub = font(FONT_REGULAR, 42)
    f_meta = font(FONT_BOLD, 28)

    bbox = draw.textbbox((0, 0), OUTRO_TITLE, font=f_big)
    draw.text(((W - (bbox[2] - bbox[0])) / 2 - bbox[0], H / 2 - 120),
              OUTRO_TITLE, font=f_big, fill=with_alpha(INK, ease))

    sub_ease = ease_out_cubic((t - 0.4) / 0.9)
    bbox = draw.textbbox((0, 0), OUTRO_SUB, font=f_sub)
    draw.text(((W - (bbox[2] - bbox[0])) / 2 - bbox[0], H / 2 + 18),
              OUTRO_SUB, font=f_sub, fill=with_alpha(MUTED, sub_ease))

    meta_ease = ease_out_cubic((t - 0.75) / 0.8)
    meta = f"{len(SENTENCES)} ПРЕДЛОЖЕНИЙ · {total_words} СЛОВ · {MIN_WORDS}+ НА КАЖДОЕ"
    bbox = draw.textbbox((0, 0), meta, font=f_meta)
    chip_w = bbox[2] - bbox[0] + 44
    draw_chip(draw, (int((W - chip_w) / 2), int(H / 2 + 120)), meta, f_meta,
              with_alpha(ACCENTS[0], meta_ease),
              with_alpha((255, 255, 255), 0.06 * meta_ease))
    return frame


# --------------------------------------------------------------------------
# Audio
# --------------------------------------------------------------------------

CHORDS = [
    (110.00, 164.81, 220.00),   # A minor
    (87.31, 130.81, 174.61),    # F
    (98.00, 146.83, 196.00),    # G
    (110.00, 164.81, 220.00),   # A minor
    (130.81, 196.00, 261.63),   # C
    (87.31, 130.81, 174.61),    # F
    (98.00, 146.83, 196.00),    # G
    (110.00, 164.81, 220.00),   # A minor (outro)
]


def write_audio(path: Path, duration: float, sample_rate: int = 44100) -> None:
    """A quiet pad: one triad per section, cross-faded into the next."""
    total = int(duration * sample_rate)
    seg = duration / len(CHORDS)
    fade = min(1.0, seg * 0.5)
    samples = [0.0] * total

    for i, chord in enumerate(CHORDS):
        start = i * seg
        end = min(duration, start + seg + fade)
        s0 = int(start * sample_rate)
        s1 = int(end * sample_rate)
        for n in range(s0, s1):
            t = n / sample_rate
            local = t - start
            env = (max(0.0, min(1.0, local / fade))
                   * max(0.0, min(1.0, (end - t) / fade)))
            env = env ** 1.5
            if env <= 0.0:
                continue
            value = 0.0
            for k, f in enumerate(chord):
                value += math.sin(2 * math.pi * f * t + k) / (k + 1)
            samples[n] += value * env * 0.055

    # Global fade in/out and gentle limiter.
    fade_io = 2.0
    for n in range(total):
        t = n / sample_rate
        env = min(1.0, t / fade_io, max(0.0, (duration - t) / fade_io))
        samples[n] *= env
    peak = max(1e-6, max(abs(v) for v in samples))
    scale = min(1.0, 0.85 / peak)

    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        frames = bytearray()
        for v in samples:
            frames += struct.pack("<h", int(max(-1.0, min(1.0, v * scale)) * 32767))
        wf.writeframes(bytes(frames))


# --------------------------------------------------------------------------
# Render
# --------------------------------------------------------------------------


def render(out_path: Path, fps: int = FPS) -> None:
    for s in SENTENCES:
        count = len(s.split())
        if count < MIN_WORDS:
            raise SystemExit(f"Sentence has {count} words, need >= {MIN_WORDS}: {s}")

    total_words = sum(len(s.split()) for s in SENTENCES)
    duration = INTRO_SEC + SCENE_SEC * len(SENTENCES) + OUTRO_SEC

    from imageio_ffmpeg import get_ffmpeg_exe

    bg = make_background()
    body_font = font(FONT_REGULAR, BODY_SIZE)

    scenes = []
    for i, sentence in enumerate(SENTENCES):
        accent = ACCENTS[i % len(ACCENTS)]
        scenes.append(build_word_tiles(sentence, body_font, accent))

    with tempfile.TemporaryDirectory() as tmp:
        wav_path = Path(tmp) / "pad.wav"
        write_audio(wav_path, duration)

        cmd = [
            get_ffmpeg_exe(), "-y", "-loglevel", "error",
            "-f", "rawvideo", "-pix_fmt", "rgb24",
            "-s", f"{W}x{H}", "-r", str(fps), "-i", "-",
            "-i", str(wav_path),
            "-c:v", "libx264", "-preset", "slow", "-crf", "20",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-c:a", "aac", "-b:a", "128k",
            "-shortest", str(out_path),
        ]
        proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)

        frame_count = int(round(duration * fps))
        for f in range(frame_count):
            t = f / fps
            if t < INTRO_SEC:
                frame = render_intro(t, bg, total_words)
            elif t < INTRO_SEC + SCENE_SEC * len(SENTENCES):
                rel = t - INTRO_SEC
                index = min(int(rel // SCENE_SEC), len(SENTENCES) - 1)
                local = rel - index * SCENE_SEC
                accent = ACCENTS[index % len(ACCENTS)]
                frame = render_sentence(index, local, bg, scenes[index],
                                        accent, SENTENCES[index])

                # Cross-fade scene edges into the background.
                if local < SCENE_FADE_IN:
                    frame = Image.blend(bg, frame, local / SCENE_FADE_IN)
                if SCENE_SEC - local < SCENE_FADE_OUT:
                    k = (SCENE_SEC - local) / SCENE_FADE_OUT
                    frame = Image.blend(bg, frame, k)
            else:
                frame = render_outro(t - INTRO_SEC - SCENE_SEC * len(SENTENCES),
                                     bg, total_words)

            proc.stdin.write(frame.tobytes())
            if f % 150 == 0:
                print(f"  frame {f}/{frame_count}", flush=True)

        proc.stdin.close()
        proc.wait()
        if proc.returncode != 0:
            raise SystemExit("ffmpeg failed")

    print(f"done: {out_path} ({out_path.stat().st_size / 1e6:.2f} MB, "
          f"{duration:.1f} s, {total_words} words)")


def main() -> None:
    repo_root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--out",
        type=Path,
        default=repo_root / "media" / "sentences_video" / "sentences_15_words.mp4",
    )
    parser.add_argument("--fps", type=int, default=FPS)
    args = parser.parse_args()
    render(args.out, args.fps)


if __name__ == "__main__":
    sys.exit(main())
