#!/usr/bin/env python3
"""Render the 60 s vertical edit from the downloaded source video.

Every shot is rendered separately (zoom / pan / tilt / glitch / caption), then the
shots are concatenated and muxed with the generated 128 BPM track. Shot lengths
are locked to the beat grid, so cuts land on the beat.

usage:
    python3 make_edit.py            # full render
    python3 make_edit.py --preview  # first 4 shots only
"""
import os
import subprocess
import sys

import imageio_ffmpeg

FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
SRC = os.environ.get("SRC", "../../edits/source/mono-vajbkoding.mp4")
MUSIC = os.environ.get("MUSIC", "out/music.wav")
OUTDIR = "out"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
W, H, FPS = 1080, 1920, 30
BPM = 128.0
BEAT = 60.0 / BPM

# (source_in_seconds, beats, style, caption, caption_size)
# style: punch_in | punch_out | pan_l | pan_r | tilt_up | tilt_down | glitch | fit
SHOTS = [
    # ---- intro (8 beats) ----
    (220.3, 4, "fit",       "ГАЙД НА ВАЙБКОДИНГ", 72),
    (160.5, 4, "punch_in",  "", 0),
    # ---- build (24 beats) ----
    (520.4, 4, "punch_in",  "", 0),
    (80.5,  4, "fit",       "", 0),
    (740.4, 4, "glitch",    "СДЕЛАНО ИИ", 74),
    (320.4, 4, "pan_l",     "", 0),
    (640.4, 4, "punch_in",  "КОГДА ОНО ЗАРАБОТАЛО", 46),
    (880.4, 4, "punch_out", "", 0),
    # ---- drop 1 (32 beats) ----
    (160.2, 2, "glitch",    "", 0),
    (740.2, 2, "pan_r",     "", 0),
    (880.2, 2, "punch_in",  "", 0),
    (320.2, 2, "pan_l",     "", 0),
    (520.2, 2, "punch_in",  "ЛОКАЛЬНЫЕ МОДЕЛИ", 44),
    (680.2, 2, "fit",       "", 0),
    (400.2, 2, "fit",       "", 0),
    (840.2, 2, "fit",       "АГЕНТЫ И ЦЕНЫ", 50),
    (360.2, 2, "fit",       "", 0),
    (540.2, 2, "fit",       "", 0),
    (560.2, 2, "fit",       "", 0),
    (960.2, 2, "fit",       "МОНО", 66),
    (420.2, 2, "pan_r",     "", 0),
    (140.2, 2, "punch_in",  "", 0),
    (660.2, 2, "fit",       "", 0),
    (640.2, 2, "punch_in",  "", 0),
    # ---- breakdown (16 beats) ----
    (220.5, 8, "fit",       "ЕСЛИ УМЕЕШЬ ПОЛЬЗОВАТЬСЯ ИИ-АГЕНТАМИ", 46),
    (820.4, 8, "fit",       "УМЕЕШЬ ЛИ ТЫ ВАЙБКОДИТЬ?", 52),
    # ---- drop 2 (32 beats) ----
    (520.6, 2, "glitch",    "", 0),
    (700.2, 2, "pan_r",     "", 0),
    (740.6, 2, "punch_in",  "", 0),
    (800.2, 2, "fit",       "ТЕСТЫ ПРОШЛИ", 54),
    (780.2, 2, "punch_in",  "", 0),
    (560.5, 2, "fit",       "", 0),
    (920.2, 2, "punch_in",  "", 0),
    (180.2, 2, "punch_in",  "", 0),
    (440.2, 2, "fit",       "", 0),
    (340.2, 2, "punch_in",  "", 0),
    (720.2, 2, "fit",       "", 0),
    (500.2, 2, "fit",       "MCP", 62),
    (320.6, 2, "pan_l",     "", 0),
    (680.6, 2, "fit",       "", 0),
    (140.6, 2, "punch_in",  "", 0),
    (980.2, 2, "fit",       "", 0),
    # ---- outro (16 beats) ----
    (860.4, 4, "punch_in",  "", 0),
    (220.4, 4, "fit",       "ГАЙД НА ВАЙБКОДИНГ", 68),
    (0.0,   8, "black",     "СМОТРИ ПОЛНОСТЬЮ", 58),
]

FLASH = {0, 8, 26}          # shots that open with a white hit
SHAKE = {"punch_in", "punch_out", "glitch"}


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(" ".join(cmd) + "\n---\n" + r.stderr[-4000:] + "\n")
        raise SystemExit(f"ffmpeg failed ({r.returncode})")


def esc(text):
    return (text.replace("\\", "\\\\").replace(":", "\\:")
                .replace("'", "\u2019").replace("%", "\\%"))


def caption_chain(text, size, yfrac=0.70):
    if not text:
        return ""
    t = esc(text)
    return (
        f",drawtext=fontfile={FONT}:text='{t}':fontcolor=white:fontsize={size}"
        f":borderw=8:bordercolor=black@0.92:line_spacing=12"
        f":x=(w-text_w)/2:y=h*{yfrac}+22*exp(-11*t):alpha='min(1,t*7)'"
    )


def zoom_expr(style, dur):
    """returns (z, x, y) zoompan expressions in input pixels"""
    cx, cy = "(iw-iw/zoom)/2", "(ih-ih/zoom)/2"
    jx = "+7*sin(time*41)" if style in SHAKE else ""
    jy = "+7*cos(time*37)" if style in SHAKE else ""
    if style == "punch_in":
        return "min(1+0.0016*on,1.14)", cx + jx, cy + jy
    if style == "punch_out":
        return "max(1.14-0.0016*on,1.0)", cx + jx, cy + jy
    if style == "glitch":
        return "1.06+0.02*sin(on/1.7)", cx + "+16*sin(on*2.3)", cy + "+16*cos(on*1.9)"
    if style == "pan_l":
        return "1.15", f"(iw-iw/zoom)*(1-min(1,time/{dur:.3f}))", cy
    if style == "pan_r":
        return "1.15", f"(iw-iw/zoom)*min(1,time/{dur:.3f})", cy
    if style == "tilt_up":
        return "1.12", cx, f"(ih-ih/zoom)*(1-min(1,time/{dur:.3f}))"
    if style == "tilt_down":
        return "1.12", cx, f"(ih-ih/zoom)*min(1,time/{dur:.3f})"
    return "1.04", cx, cy


def build_graph(style, dur, caption, size, flash):
    """returns filter_complex producing [out]"""
    if style == "black":
        chain = "[0:v]scale=1080:1920,setsar=1,noise=alls=7:allf=t,vignette=PI/4.2"
        chain += caption_chain(caption, size, 0.455)
        chain += (
            f",drawtext=fontfile={FONT}:text='моно':fontcolor=0xB9A6FF:fontsize=44"
            f":borderw=4:bordercolor=black@0.8:x=(w-text_w)/2:y=h*0.53:alpha='min(1,max(0,(t-0.6)*4))'"
        )
        chain += ",format=yuv420p[out]"
        return chain
    if style == "fit":
        head = (
            "[0:v]fps=30,split=2[s0][s1];"
            # tiny, heavily blurred backdrop: no legible text, just light blobs
            "[s0]scale=270:480:force_original_aspect_ratio=increase,crop=270:480,"
            "gblur=sigma=24,scale=1080:1920:flags=bicubic,"
            "eq=brightness=-0.30:saturation=1.35:contrast=0.95[sbg];"
            "[s1]scale=1080:-2:flags=lanczos[sfg];"
            "[sbg][sfg]overlay=(W-w)/2:(H-h)/2[comp];"
            "[comp]scale=1140:2027:flags=lanczos,"
            "zoompan=z='min(1+0.0007*on,1.04)':x='(iw-iw/zoom)/2':y='(ih-ih/zoom)/2'"
            f":d=1:s={W}x{H}:fps=30"
        )
    else:
        z, x, y = zoom_expr(style, dur)
        head = (
            "[0:v]fps=30,scale=1080:1920:force_original_aspect_ratio=increase:flags=lanczos,"
            "crop=1080:1920,setsar=1,scale=1404:2496:flags=lanczos,"
            f"zoompan=z='{z}':x='{x}':y='{y}':d=1:s={W}x{H}:fps=30"
        )

    tail = ""
    if style == "glitch":
        tail += ",rgbashift=rh=12:bh=-12:gv=4,noise=alls=9:allf=t+u"
    elif style in ("punch_in", "punch_out"):
        tail += ",noise=alls=4:allf=t"
    if flash:
        tail += ",eq=brightness=0.5:enable='lt(t,0.05)'"
    tail += ",eq=brightness=0.045:contrast=1.07:saturation=1.2"
    tail += ",unsharp=5:5:1.0:5:5:0.0"
    tail += ",vignette=PI/5.4"
    yfrac = {"fit": 0.755, "black": 0.46}.get(style, 0.78)
    tail += caption_chain(caption, size, yfrac)
    tail += ",setsar=1,format=yuv420p[out]"
    return head + tail


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    preview = "--preview" in sys.argv
    total = sum(s[1] for s in SHOTS) * BEAT
    print(f"{len(SHOTS)} shots / {total:.2f}s")

    parts = []
    for i, (src_in, beats, style, cap, size) in enumerate(SHOTS):
        dur = beats * BEAT
        part = f"{OUTDIR}/shot_{i:03d}.mp4"
        parts.append(part)
        graph = build_graph(style, dur, cap, size, i in FLASH)
        src_args = (
            ["-f", "lavfi", "-i",
             f"gradients=s={W}x{H}:r=30:d={dur + 0.2:.3f}:c0=0x0a0a14:c1=0x241040:speed=0.02"]
            if style == "black" else
            ["-ss", f"{src_in:.3f}", "-t", f"{dur + 0.10:.3f}", "-i", SRC]
        )
        run([
            FFMPEG, "-y", "-v", "error", *src_args,
            "-filter_complex", graph, "-map", "[out]", "-an",
            "-r", str(FPS), "-t", f"{dur:.4f}",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-maxrate", "12M", "-bufsize", "24M",
            "-pix_fmt", "yuv420p", part,
        ])
        first = cap.split("\n")[0][:34] if cap else ""
        print(f"  {i:02d} {style:<10} {dur:5.3f}s  {first!r}")
        if preview and i >= 3:
            print("preview: stop before concat")
            return

    lst = f"{OUTDIR}/concat.txt"
    with open(lst, "w") as fh:
        for p in parts:
            fh.write(f"file '{os.path.abspath(p)}'\n")

    video = f"{OUTDIR}/video_only.mp4"
    run([FFMPEG, "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", lst,
         "-c", "copy", video])

    final = f"{OUTDIR}/mono-vajbkoding-edit-60s.mp4"
    run([FFMPEG, "-y", "-v", "error", "-i", video, "-i", MUSIC,
         "-map", "0:v", "-map", "1:a",
         "-c:v", "libx264", "-preset", "fast", "-crf", "21",
         "-profile:v", "high", "-level", "4.1", "-pix_fmt", "yuv420p",
         "-movflags", "+faststart",
         "-af", "loudnorm=I=-14:TP=-1.0:LRA=11",
         "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-shortest",
         "-metadata", "title=ГАЙД НА ВАЙБКОДИНГ — 60s edit",
         final])
    print("done:", final)


if __name__ == "__main__":
    main()
