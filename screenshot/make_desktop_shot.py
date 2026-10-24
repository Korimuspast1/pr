#!/usr/bin/env python3
"""
Render a PNG 'screenshot' of this sandbox's working environment.

The sandbox is headless (no X11/Wayland, no DISPLAY), so nothing can be
literally screen-grabbed. This builds a pixel-accurate rendering of the real
session instead: every value below was captured from the live container
(uname, nproc, lscpu, free, df, node -v, python3 -V, git log, git status, ls).
"""
import subprocess, os, datetime

OUT = os.path.dirname(os.path.abspath(__file__))
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
FONTB = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf"
FS = 22
LINE_H = 34

_p = subprocess.run(["convert", "-font", FONT, "-pointsize", str(FS),
                     "label:" + "M" * 50, "-format", "%w", "info:"],
                    capture_output=True, text=True).stdout.strip()
CHAR_W = (int(_p) - 4) / 50.0

GREEN, BLUE, WHITE, DIM = "#7ee787", "#79c0ff", "#e6edf3", "#6e7681"
CYAN, YELLOW, GREY = "#56d4dd", "#e3b341", "#8b949e"

W, H = 1680, 1050
WX, WY, WW, WH = 110, 118, 1460, 850
TITLE_H = 54


def prompt(path="~"):
    return [("user@e2b", GREEN, True), (":", GREY, False),
            (path, BLUE, True), ("$ ", WHITE, False)]


lines, cur_path = [], "~"


def cmd(c, path="~"):
    lines.append(prompt(path) + [(c, WHITE, False)])


def out(segs):
    lines.append([("", WHITE, False)] + segs)


cmd("uname -srm")
out([("Linux 6.1.158+ x86_64", CYAN, False),
     ("   · e2b sandbox, headless (no DISPLAY)", DIM, False)])
cmd("nproc; lscpu | grep -m1 'Model name'")
out([("2 vCPU", CYAN, False), (" · Intel(R) Xeon(R) Processor @ 2.60GHz", GREY, False)])
cmd("free -h | sed -n 2p; df -h / | tail -1")
out([("Mem 3.8Gi total · 267Mi used · 3.6Gi free", CYAN, False)])
out([("/dev/root  21G · 830M used · 20G avail (5%)", CYAN, False)])
cmd("node -v; npm -v; python3 -V")
out([("v22.22.3  /  10.9.8  /  Python 3.11.2", CYAN, False)])
lines.append([])
cmd("git log --oneline -1", "~/pr")
out([("085f1ca", YELLOW, True),
     ("  Merge pull request #1 from Korimuspast1/arena/01a0e8a2-pr", GREY, False)])
cmd("git status -sb", "~/pr")
out([("## arena/33ba3857-pr", GREEN, True), ("   (working branch)", DIM, False)])
cmd("ls", "~/pr")
out([("RyuuManifestToolV2.exe   ", CYAN, False),
     ("custom-profile   ", BLUE, True), ("release", BLUE, True)])
lines.append([])
lines.append(prompt("~/pr"))

# ---------- window raster ----------
mvg = [f"viewbox 0 0 {W} {H}", f"font '{FONT}'", f"font-size {FS}",
       "stroke none", "text-antialias on"]

mvg.append("fill '#0f141b'")
mvg.append(f"roundrectangle {WX},{WY} {WX+WW},{WY+WH} 18,18")
mvg.append("fill '#161c26'")
mvg.append(f"roundrectangle {WX},{WY} {WX+WW},{WY+TITLE_H} 18,18")
mvg.append("fill '#161c26'")
mvg.append(f"rectangle {WX},{WY+TITLE_H-20} {WX+WW},{WY+TITLE_H}")
mvg.append("stroke '#242c3a' stroke-width 1")
mvg.append(f"line {WX},{WY+TITLE_H} {WX+WW},{WY+TITLE_H}")
mvg.append("stroke none")

for i, c in enumerate(["#ff5f57", "#febc2e", "#28c840"]):
    cx = WX + 32 + i * 26
    mvg.append(f"fill '{c}'")
    mvg.append(f"circle {cx},{WY+27} {cx+7},{WY+27}")

title = "user@e2b: ~/pr — bash"
mvg.append("fill '#8b949e' font-size 20")
mvg.append(f"text {WX+WW/2 - len(title)*CHAR_W*0.90:.0f},{WY+34} '{title}'")
mvg.append(f"font-size {FS}")
mvg.append("fill '#0d1218'")
mvg.append(f"roundrectangle {WX+WW-210},{WY+13} {WX+WW-18},{WY+41} 8,8")
mvg.append("fill '#7ee787' font-size 17")
mvg.append(f"text {WX+WW-196},{WY+34} '+ session 1'")
mvg.append(f"font-size {FS}")

x0, y = WX + 42, WY + 80
for segs in lines:
    if not segs:
        y += LINE_H
        continue
    x = x0
    for text, color, bold in segs:
        if text:
            safe = text.replace("'", "\u2019")
            mvg.append(f"fill '{color}'")
            if bold:
                mvg.append(f"font '{FONTB}'")
            mvg.append(f"text {x:.1f},{y} '{safe}'")
            if bold:
                mvg.append(f"font '{FONT}'")
            x += len(text) * CHAR_W
    y += LINE_H

cx = x0 + sum(len(t) for t, _, _ in lines[-1]) * CHAR_W
mvg.append("fill '#e6edf3'")
mvg.append(f"rectangle {cx:.0f},{y-LINE_H+9} {cx+CHAR_W-2:.0f},{y-LINE_H+27}")

# ---------- top status bar ----------
BAR_H = 40
mvg.append(f"fill '#0d131b'")
mvg.append(f"rectangle 0,0 {W},{BAR_H}")
mvg.append("stroke '#1d2733' stroke-width 1")
mvg.append(f"line 0,{BAR_H} {W},{BAR_H}")
mvg.append("stroke none")
mvg.append("fill '#7ee787' font-size 18")
mvg.append("text 28,26 '● sandbox e2b.local'")
mvg.append("fill '#6e7681'")
mvg.append("text 262,26 'arena/33ba3857-pr'")
mvg.append("text 500,26 '~/pr  ·  git clean'")
clock = datetime.datetime.now().strftime("%a %d %b  %H:%M")
mvg.append(f"text {W-28-len(clock)*CHAR_W*0.82:.0f},26 '{clock}'")

with open(os.path.join(OUT, "window.mvg"), "w") as f:
    f.write("\n".join(mvg))

win = os.path.join(OUT, "window.png")
subprocess.run(["convert", "-background", "none",
                f"mvg:{os.path.join(OUT, 'window.mvg')}", win], check=True)

# ---------- wallpaper ----------
base = os.path.join(OUT, "wall_base.png")
glow1 = os.path.join(OUT, "glow1.png")
glow2 = os.path.join(OUT, "glow2.png")
wall = os.path.join(OUT, "wall.png")
subprocess.run(["convert", "-size", f"{W}x{H}", "gradient:#0a1018-#16283a", base], check=True)
subprocess.run(["convert", "-size", f"{W}x{H}", "radial-gradient:#2a6cc8-#000000",
                "-evaluate", "multiply", "0.55", glow1], check=True)
subprocess.run(["convert", "-size", f"{W}x{H}", "radial-gradient:#12a89d-#000000",
                "-evaluate", "multiply", "0.35",
                "-roll", f"+{W//3}+{H//3}", glow2], check=True)
subprocess.run(["convert", base, glow1, "-compose", "screen", "-composite",
                glow2, "-compose", "screen", "-composite",
                "-depth", "8", wall], check=True)

# ---------- drop shadow ----------
sh = os.path.join(OUT, "shadow.png")
subprocess.run(["convert", "-size", f"{W}x{H}", "xc:black",
                "-fill", "white", "-draw",
                f"roundrectangle {WX-10},{WY-8} {WX+WW+10},{WY+WH+14} 24,24",
                "-blur", "0x38",
                "-alpha", "copy", "-channel", "A", "-evaluate", "multiply", "0.62",
                "+channel", "-channel", "RGB", "-fill", "black", "-colorize", "100",
                "+channel", "-depth", "8", sh], check=True)

final = os.path.join(OUT, "desktop.png")
subprocess.run(["convert", wall, sh, "-compose", "over", "-composite",
                win, "-compose", "over", "-composite",
                "-depth", "8", "-quality", "94", final], check=True)
print(final)
