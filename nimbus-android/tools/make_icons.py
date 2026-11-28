"""Draw Nimbus' monochrome launcher icon into the mipmap-* folders.

Pure stdlib (CI has no Pillow): a white cloud on #0A0A0A, matching the app's monochrome
palette and the adaptive icon's vector foreground.

    python3 tools/make_icons.py
"""
import struct
import zlib
from pathlib import Path

RES = Path(__file__).resolve().parent.parent / "app" / "src" / "main" / "res"
SIZES = {
    "mipmap-mdpi": 48,
    "mipmap-hdpi": 72,
    "mipmap-xhdpi": 96,
    "mipmap-xxhdpi": 144,
    "mipmap-xxxhdpi": 192,
}
BG = (10, 10, 10)
FG = (255, 255, 255)

# Cloud in normalised icon space (0..1): three circles + a rounded base bar.
CIRCLES = [(0.40, 0.60, 0.19), (0.62, 0.58, 0.16), (0.52, 0.44, 0.16)]
BAR = (0.24, 0.58, 0.76, 0.75)  # left, top, right, bottom
BAR_RADIUS = 0.07
EDGE = 0.010  # anti-aliasing half-width


def shape_distance(u: float, v: float) -> float:
    """Negative inside the glyph, positive outside."""
    dist = min(((u - cx) ** 2 + (v - cy) ** 2) ** 0.5 - r for cx, cy, r in CIRCLES)
    left, top, right, bottom = BAR
    dx = max(left + BAR_RADIUS - u, 0.0, u - (right - BAR_RADIUS))
    dy = max(top + BAR_RADIUS - v, 0.0, v - (bottom - BAR_RADIUS))
    rect = (dx * dx + dy * dy) ** 0.5 - BAR_RADIUS
    return min(dist, rect)


def alpha(u: float, v: float) -> float:
    dist = shape_distance(u, v)
    if dist <= -EDGE:
        return 1.0
    if dist >= EDGE:
        return 0.0
    return (EDGE - dist) / (2 * EDGE)


def render(size: int) -> bytes:
    raw = bytearray()
    pad = size * 0.10
    span = size - 2 * pad
    for y in range(size):
        raw.append(0)  # row filter: none
        for x in range(size):
            u = (x + 0.5 - pad) / span
            v = (y + 0.5 - pad) / span
            weight = alpha(u, v) if 0.0 <= u <= 1.0 and 0.0 <= v <= 1.0 else 0.0
            raw += bytes(int(round(BG[i] + (FG[i] - BG[i]) * weight)) for i in range(3))
    return bytes(raw)


def png(size: int, raw: bytes) -> bytes:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )


def main() -> None:
    for folder, size in SIZES.items():
        out = RES / folder
        out.mkdir(parents=True, exist_ok=True)
        (out / "ic_launcher.png").write_bytes(png(size, render(size)))
        print(f"{folder}/ic_launcher.png  {size}x{size}")


if __name__ == "__main__":
    main()
