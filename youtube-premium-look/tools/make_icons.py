#!/usr/bin/env python3
"""Генерирует иконки расширения (icons/icon16/32/48/128.png).

Красный скруглённый квадрат + белая кнопка Play + золотой кружок
с белой галочкой (стиль «визуального Premium»).
"""
from pathlib import Path

from PIL import Image, ImageDraw

GOLD_1 = (255, 215, 107, 255)
GOLD_2 = (227, 161, 14, 255)
RED = (255, 0, 0, 255)
WHITE = (255, 255, 255, 255)


def make_icon(size: int) -> Image.Image:
    ss = 8  # суперсэмплинг для гладких краёв
    S = size * ss

    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # красный скруглённый квадрат
    m = int(S * 0.02)
    d.rounded_rectangle([m, m, S - m, S - m], radius=int(S * 0.22), fill=RED)

    # белая кнопка Play
    cx, cy = S * 0.42, S * 0.5
    w, h = S * 0.30, S * 0.36
    d.polygon(
        [(cx - w / 2, cy - h / 2), (cx - w / 2, cy + h / 2), (cx + w / 2, cy)],
        fill=WHITE,
    )

    # золотой кружок с галочкой
    r = S * 0.28
    bx, by = S - r * 1.05, S - r * 1.05
    d.ellipse([bx - r, by - r, bx + r, by + r], fill=GOLD_1, outline=GOLD_2,
              width=max(2, int(S * 0.03)))

    cw = r * 1.05
    d.line(
        [(bx - cw / 2, by + cw * 0.05),
         (bx - cw * 0.1, by + cw * 0.45),
         (bx + cw / 2, by - cw * 0.35)],
        fill=WHITE,
        width=max(3, int(S * 0.07)),
        joint="curve",
    )

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    out = Path(__file__).resolve().parent.parent / "icons"
    out.mkdir(exist_ok=True)
    for size in (16, 32, 48, 128):
        make_icon(size).save(out / f"icon{size}.png")
        print(f"icons/icon{size}.png — готово")


if __name__ == "__main__":
    main()
