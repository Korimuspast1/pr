#!/usr/bin/env python3
"""
Offline reference renderer for the liquid_glass.sksl optical model.

Runs the *same* shader source that the Android app ships, but through CPU Skia
(skia-python), so the look can be tuned and documented without an emulator:

    python3 tools/skialab.py                 # writes build/lab-*.png
    python3 tools/skialab.py --out card.png

Notes
-----
* Skia renders SkSL; AGSL is SkSL plus `layout(color)` uniforms. This shader
  only uses float uniforms and one `shader` child, so it is valid in both.
* Everything is drawn in device pixel space; each glass element is rendered by
  translating the canvas to its top-left corner and drawing a rect with the
  runtime shader as paint, exactly like `RenderEffect.createRuntimeShaderEffect`
  does on device (the element's own slice of the backdrop is the `content`).
"""
import argparse
import math
import os
import sys

import numpy as np
import skia

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SHADER = os.path.join(ROOT, "shader", "liquid_glass.sksl")


# --------------------------------------------------------------------------- #
#  backdrops (procedural, no external assets)
# --------------------------------------------------------------------------- #
def _mesh(w, h, seeds, seed=1):
    """Smooth multi-blob gradient, the classic iOS wallpaper look."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    img = np.zeros((h, w, 3), np.float32)
    rng = np.random.default_rng(seed)
    for (cx, cy, r, col) in seeds:
        d2 = ((xx - cx * w) ** 2 + (yy - cy * h) ** 2) / (2 * (r * w) ** 2)
        wgt = np.exp(-d2)[..., None]
        img += wgt * np.array(col, np.float32)
    img /= np.maximum(img.max(), 1e-6)
    # gentle vignette + film grain
    v = 1.0 - 0.25 * (((xx / w - 0.5) ** 2 + (yy / h - 0.5) ** 2) * 2.2)
    img *= v[..., None]
    img += rng.normal(0, 0.012, img.shape)
    return np.clip(img, 0, 1)


def backdrop_aurora(w, h):
    return _mesh(w, h, [
        (0.15, 0.12, 0.55, (0.15, 0.55, 1.00)),
        (0.85, 0.05, 0.50, (0.55, 0.15, 0.95)),
        (0.10, 0.85, 0.55, (0.95, 0.30, 0.65)),
        (0.75, 0.90, 0.60, (1.00, 0.70, 0.25)),
        (0.50, 0.45, 0.45, (0.05, 0.85, 0.85)),
    ], seed=7)


def backdrop_light(w, h):
    """Light, low-contrast backdrop: worst case for glass legibility."""
    return _mesh(w, h, [
        (0.20, 0.20, 0.75, (0.98, 0.97, 0.95)),
        (0.85, 0.30, 0.70, (0.93, 0.94, 0.99)),
        (0.50, 0.95, 0.80, (0.99, 0.95, 0.92)),
    ], seed=3)


def backdrop_night(w, h):
    return _mesh(w, h, [
        (0.20, 0.30, 0.30, (0.10, 0.05, 0.35)),
        (0.80, 0.20, 0.25, (0.05, 0.30, 0.55)),
        (0.60, 0.85, 0.35, (0.55, 0.05, 0.35)),
        (0.95, 0.75, 0.25, (1.00, 0.45, 0.10)),
    ], seed=11)


def backdrop_text(w, h):
    """High-frequency content so blur, lensing and dispersion are obvious."""
    img = _mesh(w, h, [
        (0.1, 0.1, 0.9, (0.10, 0.10, 0.14)),
        (0.9, 0.9, 0.9, (0.20, 0.16, 0.30)),
    ], seed=5)
    surface = new_surface(w, h)
    canvas = surface.getCanvas()
    canvas.clear(skia.ColorBLACK)
    arr = (img * 255).astype(np.uint8)
    rgba = np.dstack([arr, np.full((h, w, 1), 255, np.uint8)])
    canvas.drawImage(skia.Image.fromarray(rgba), 0, 0)
    lines = ["Liquid Glass", "optical model", "refraction", "bezel 48 px",
             "dispersion", "specular rim", "Regular / Clear", "0123456789"]
    tf = skia.Typeface.MakeFromName("DejaVu Sans", skia.FontStyle.Normal())
    for i in range(28):
        font = skia.Font(tf, 58 + (i % 3) * 10)
        paint = skia.Paint(AntiAlias=True,
                           Color=skia.Color4f(1, 1, 1, 0.9 if i % 3 else 0.5))
        canvas.drawString(lines[i % len(lines)], 60 + (i % 2) * 120, 90 + i * 78,
                          font, paint)
    return canvas_to_array(surface)


def new_surface(w, h):
    """RGBA_8888 raster surface: skia-python's fromarray/toarray then agree."""
    info = skia.ImageInfo.Make(int(w), int(h), skia.kRGBA_8888_ColorType,
                               skia.kPremul_AlphaType)
    return skia.Surface.MakeRaster(info)


def canvas_to_array(surface):
    img = surface.makeImageSnapshot()
    return img.toarray()


def arr_to_image(arr):
    """RGB(A) float [0,1] or uint8 numpy array -> skia.Image usable on N32 surfaces."""
    a = np.asarray(arr, np.float32)
    if a.max() <= 1.001:
        a = a * 255.0
    a = np.ascontiguousarray(np.clip(a, 0, 255).astype(np.uint8))
    if a.ndim == 3 and a.shape[2] == 3:
        a = np.dstack([a, np.full(a.shape[:2] + (1,), 255, np.uint8)])
    # Channel order: skia-python's fromarray/toarray use raw RGBA bytes for
    # kRGBA_8888 images, so no BGR swapping is needed anywhere.
    return skia.Image.fromarray(np.ascontiguousarray(a))


# --------------------------------------------------------------------------- #
#  the glass renderer
# --------------------------------------------------------------------------- #
DEFAULTS = dict(
    bevel=48.0,
    lens=22.0,
    lens_profile=0.55,
    depth=0.0,
    chroma=0.0,
    tint=(1.0, 1.0, 1.0, 0.06),
    highlight=1.0,
    highlight_width=1.2,
    highlight_falloff=1.0,
    light_dir=(-0.7071, -0.7071),
    inner_shadow=0.35,
    press=0.0,
    press_point=(0.0, 0.0),
    press_bulge=0.0,
    grain=1.0,
    opacity=1.0,
    blur=10.0,
    saturation=1.6,
    brightness=0.0,
)


def make_effect():
    src = open(SHADER, "r", encoding="utf-8").read()
    eff = skia.RuntimeEffect.MakeForShader(src)
    if eff is None:
        raise SystemExit("shader failed to compile")
    return eff


class Scene:
    def __init__(self, backdrop: np.ndarray, effect):
        self.h, self.w = backdrop.shape[:2]
        self.arr = backdrop
        self.image = arr_to_image(backdrop)
        self.effect = effect
        self.surface = new_surface(self.w, self.h)
        self.canvas = self.surface.getCanvas()
        self.canvas.drawImage(self.image, 0, 0)
        self._content = {}

    def glass(self, x, y, w, h, radius, **kw):
        p = dict(DEFAULTS)
        p.update(kw)
        radii = radius if isinstance(radius, (tuple, list)) else (radius,) * 4

        # content child: blurred + saturated backdrop, sampled in element-local space
        content = self.content(p["blur"], p["saturation"])
        # NOTE: SkSL shader local matrices are applied in the "local -> device"
        # direction, so to make element-local coords land on the backdrop we
        # translate by the *negated* element origin (verified numerically).
        content_shader = content.makeShader(
            skia.TileMode.kClamp, skia.TileMode.kClamp,
            skia.SamplingOptions(skia.FilterMode.kLinear),
            skia.Matrix.Translate(-x, -y),
        )

        b = skia.RuntimeShaderBuilder(self.effect)
        b.setChild("content", content_shader)
        b.setUniform("uOrigin", skia.V2(0.0, 0.0))
        b.setUniform("uSize", skia.V2(float(w), float(h)))
        b.setUniform("uRadii", skia.V4(*[float(r) for r in radii]))
        b.setUniform("uBevel", float(p["bevel"]))
        b.setUniform("uLens", float(p["lens"]))
        b.setUniform("uLensProfile", float(p["lens_profile"]))
        b.setUniform("uDepth", float(p["depth"]))
        b.setUniform("uChroma", float(p["chroma"]))
        b.setUniform("uTint", skia.V4(*[float(c) for c in p["tint"]]))
        b.setUniform("uHighlight", float(p["highlight"]))
        b.setUniform("uHighlightWidth", float(p["highlight_width"]))
        b.setUniform("uHighlightFalloff", float(p["highlight_falloff"]))
        b.setUniform("uLightDir", skia.V2(*[float(c) for c in p["light_dir"]]))
        b.setUniform("uInnerShadow", float(p["inner_shadow"]))
        b.setUniform("uPress", float(p["press"]))
        b.setUniform("uPressPoint", skia.V2(*[float(c) for c in p["press_point"]]))
        b.setUniform("uPressBulge", float(p["press_bulge"]))
        b.setUniform("uGrain", float(p["grain"]))
        b.setUniform("uOpacity", float(p["opacity"]))

        paint = skia.Paint(AntiAlias=True)
        paint.setShader(b.makeShader())
        self.canvas.save()
        self.canvas.translate(x, y)
        self.canvas.drawRect(skia.Rect.MakeWH(w, h), paint)
        self.canvas.restore()

    def content(self, blur, saturation):
        """Blurred + saturated copy of the backdrop (cached per parameter pair)."""
        key = (float(blur), float(saturation))
        if key in self._content:
            return self._content[key]
        surf = new_surface(self.w, self.h)
        canv = surf.getCanvas()
        paint = skia.Paint()
        if blur > 0:
            paint.setImageFilter(skia.ImageFilters.Blur(blur, blur))
        if abs(saturation - 1.0) > 1e-3:
            paint.setColorFilter(skia.ColorFilters.Matrix(saturation_matrix(saturation)))
        canv.drawImage(self.image, 0, 0, skia.SamplingOptions(), paint)
        img = surf.makeImageSnapshot()
        self._content[key] = img
        return img

    def save(self, path):
        img = self.surface.makeImageSnapshot()
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        img.save(path, skia.kPNG)
        print("wrote", path, f"{img.width()}x{img.height()}")


def _draw_image(image, color_filter, w, h):
    surf = new_surface(w, h)
    canv = surf.getCanvas()
    paint = skia.Paint(ColorFilter=color_filter)
    canv.drawImage(image, 0, 0, skia.SamplingOptions(), paint)
    return surf


def saturation_matrix(s):
    s = float(s)
    return [
        0.213 + 0.787 * s, 0.715 - 0.715 * s, 0.072 - 0.072 * s, 0, 0,
        0.213 - 0.213 * s, 0.715 + 0.285 * s, 0.072 - 0.072 * s, 0, 0,
        0.213 - 0.213 * s, 0.715 - 0.715 * s, 0.072 + 0.928 * s, 0, 0,
        0, 0, 0, 1, 0,
    ]


# --------------------------------------------------------------------------- #
#  reference cards
# --------------------------------------------------------------------------- #
def card_components(effect, out):
    W, H = 1080, 1920
    sc = Scene(backdrop_text(W, H), effect)
    # status-bar-ish toolbar (floating capsule)
    sc.glass(40, 90, 1000, 96, 48, bevel=40, lens=14, chroma=0.0, inner_shadow=0.25)
    # control-centre style grid of square tiles
    for i in range(4):
        sc.glass(40 + i * 252, 240, 220, 220, 56, bevel=44, lens=18, chroma=0.25,
                 tint=(0.55, 0.75, 1.0, 0.10))
    # main pill button
    sc.glass(140, 520, 800, 88, 44, bevel=38, lens=16, chroma=0.1, highlight=1.1)
    # bottom tab bar with a selected bubble (tinted)
    sc.glass(40, H - 190, 1000, 108, 54, bevel=46, lens=20, chroma=0.15, inner_shadow=0.4)
    sc.glass(240, H - 178, 200, 84, 42, bevel=34, lens=14, chroma=0.3,
             tint=(0.35, 0.65, 1.0, 0.35), highlight=1.4)
    # floating circular buttons + search bubble
    sc.glass(830, H - 330, 96, 96, 48, bevel=40, lens=22, chroma=0.2, highlight=1.2)
    # pressed droplet on a slider knob
    sc.glass(150, 760, 120, 72, 36, bevel=34, lens=26, chroma=0.45,
             press=1.0, press_point=(60, 36), press_bulge=18, highlight=1.6)
    # menu: taller surface, thicker slab
    sc.glass(560, 700, 420, 380, 44, bevel=56, lens=26, depth=1.0,
             inner_shadow=0.45, chroma=0.2)
    sc.save(os.path.join(out, "lab-components.png"))


def card_variants(effect, out):
    W, H = 1080, 1320
    sc = Scene(backdrop_night(W, H), effect)
    sc.glass(60, 80, 960, 340, 56, bevel=52, lens=24, inner_shadow=0.4)   # regular
    sc.glass(60, 480, 960, 340, 56, bevel=52, lens=24, inner_shadow=0.4,
             opacity=0.62, tint=(1, 1, 1, 0.02), highlight=1.3)          # clear (no dim)
    sc.glass(60, 880, 960, 340, 56, bevel=52, lens=24, inner_shadow=0.4,
             opacity=0.62, tint=(0, 0, 0, 0.35), highlight=1.3)          # clear + dim layer
    sc.save(os.path.join(out, "lab-variants.png"))


def card_light(effect, out):
    W, H = 1080, 1320
    sc = Scene(backdrop_light(W, H), effect)
    sc.glass(60, 90, 960, 96, 48, bevel=40, lens=12, inner_shadow=0.45,
             tint=(0.1, 0.1, 0.12, 0.05))
    sc.glass(60, 260, 960, 420, 44, bevel=48, lens=20, inner_shadow=0.25)
    sc.glass(60, 760, 460, 96, 48, bevel=36, lens=14, chroma=0.1)
    sc.glass(560, 760, 460, 96, 48, bevel=36, lens=14, chroma=0.1,
             tint=(0.2, 0.45, 1.0, 0.18))
    sc.save(os.path.join(out, "lab-light.png"))


def card_aurora(effect, out):
    W, H = 1080, 1920
    sc = Scene(backdrop_aurora(W, H), effect)
    sc.glass(60, 150, 960, 700, 60, bevel=60, lens=30, chroma=0.3, inner_shadow=0.3)
    sc.glass(60, 950, 960, 140, 70, bevel=48, lens=22, chroma=0.2)
    for i in range(3):
        sc.glass(80 + i * 320, 1180, 280, 280, 90, bevel=52, lens=26, chroma=0.35,
                 highlight=1.2)
    sc.save(os.path.join(out, "lab-aurora.png"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(ROOT, "build"))
    ap.add_argument("--only", default=None)
    args = ap.parse_args()
    effect = make_effect()
    os.makedirs(args.out, exist_ok=True)
    cards = dict(components=card_components, variants=card_variants,
                 light=card_light, aurora=card_aurora)
    for name, fn in cards.items():
        if args.only and args.only != name:
            continue
        fn(effect, args.out)


if __name__ == "__main__":
    main()
