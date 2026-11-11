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
    bevel=22.0,
    lens=22.0,
    falloff=5.0,
    dispersion=0.10,
    light_dir=(-0.7071, -0.7071),
    spec=1.0,
    rim_band=6.0,
    tint=(1.0, 1.0, 1.0, 0.0),
    adaptive=1.0,
    dim=0.0,
    press=0.0,
    touch=(0.0, 0.0),
    touch_amp=10.0,
    blur=8.0,
    saturation=1.5,
)


def make_effect():
    src = open(SHADER, "r", encoding="utf-8").read()
    eff = skia.RuntimeEffect.MakeForShader(src)
    if eff is None:
        raise SystemExit("shader failed to compile")
    return eff


class Scene:
    """CPU reference renderer. Every glass element is one pass of the same shader the
    app ships, with the element's own margin applied like the RenderNode does."""

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
        margin = int(round(max(p["lens"] * 1.6, p["blur"] * 1.5, 8)))

        content = self.content(p["blur"], p["saturation"])
        # Node-local coords -> backdrop coords. The node is drawn at (x - margin), so a
        # node-local point q must sample the backdrop at q + (x - margin); Skia applies
        # the shader's local matrix in the local -> shader direction, hence the +.
        content_shader = content.makeShader(
            skia.TileMode.kClamp, skia.TileMode.kClamp,
            skia.SamplingOptions(skia.FilterMode.kLinear),
            skia.Matrix.Translate(x - margin, y - margin),
        )

        b = skia.RuntimeShaderBuilder(self.effect)
        b.setChild("content", content_shader)
        b.setUniform("margin", float(margin))
        b.setUniform("viewSize", skia.V2(float(w), float(h)))
        b.setUniform("radii", skia.V4(*[float(r) for r in radii]))
        b.setUniform("bevel", float(p["bevel"]))
        b.setUniform("refractPx", float(p["lens"]))
        b.setUniform("falloff", float(p["falloff"]))
        b.setUniform("dispersion", float(p["dispersion"]))
        b.setUniform("lightDir", skia.V2(*[float(c) for c in p["light_dir"]]))
        b.setUniform("specStrength", float(p["spec"]))
        b.setUniform("rimBandMax", float(p["rim_band"]))
        b.setUniform("tintColor", skia.V4(*[float(c) for c in p["tint"]]))
        b.setUniform("adaptiveTint", float(p["adaptive"]))
        b.setUniform("dimAmount", float(p["dim"]))
        b.setUniform("press", float(p["press"]))
        b.setUniform("touchPos", skia.V2(*[float(c) for c in p["touch"]]))
        b.setUniform("touchAmp", float(p["touch_amp"]))

        paint = skia.Paint(AntiAlias=True)
        paint.setShader(b.makeShader())
        # the node covers the element plus the margin, exactly like the RenderNode
        self.canvas.save()
        self.canvas.translate(x - margin, y - margin)
        self.canvas.drawRect(skia.Rect.MakeWH(w + margin * 2, h + margin * 2), paint)
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
    """The component sheet: what the app puts on screen, rendered 1:1."""
    W, H = 1080, 1920
    sc = Scene(backdrop_aurora(W, H), effect)
    # toolbar capsule
    sc.glass(40, 90, 1000, 116, 58)
    # two pill buttons
    sc.glass(40, 250, 480, 116, 58)
    sc.glass(560, 250, 480, 116, 58, tint=(0.04, 0.52, 1.0, 0.85), adaptive=0.0)
    # menu slab
    sc.glass(40, 420, 700, 460, 52, bevel=28, lens=30, falloff=3.5, rim_band=9)
    # slider capsule
    sc.glass(40, 930, 1000, 116, 58)
    # tab bar
    sc.glass(40, 1600, 760, 124, 62)
    # search bubble
    sc.glass(830, 1600, 124, 124, 62)
    sc.save(os.path.join(out, "components-sheet.png"))


def card_variants(effect, out):
    W, H = 1080, 1320
    sc = Scene(backdrop_night(W, H), effect)
    sc.glass(60, 80, 960, 340, 56)                                     # regular
    sc.glass(60, 480, 960, 340, 56, adaptive=0.0, tint=(1, 1, 1, 0.02))  # clear
    sc.glass(60, 880, 960, 340, 56, adaptive=0.0, tint=(1, 1, 1, 0.02), dim=0.35)  # clear + dim
    sc.save(os.path.join(out, "variants-regular-clear.png"))


def card_lensing(effect, out):
    """Anatomy: the same strip over a grid, at three lens strengths."""
    W, H = 1080, 900
    sc = Scene(backdrop_text(W, H), effect)
    for i, lens in enumerate((0.0, 18.0, 40.0)):
        sc.glass(60, 80 + i * 270, 960, 200, 50, bevel=30, lens=lens, falloff=5.0,
                 dispersion=0.12 if lens else 0.0, blur=0.0)
    sc.save(os.path.join(out, "anatomy-lensing.png"))


def card_light(effect, out):
    W, H = 1080, 1320
    sc = Scene(backdrop_light(W, H), effect)
    sc.glass(60, 90, 960, 116, 58)
    sc.glass(60, 250, 960, 420, 52)
    sc.glass(60, 710, 460, 116, 58)
    sc.glass(560, 710, 460, 116, 58, tint=(0.04, 0.52, 1.0, 0.85), adaptive=0.0)
    sc.save(os.path.join(out, "lab-light.png"))


def card_aurora(effect, out):
    W, H = 1080, 1920
    sc = Scene(backdrop_aurora(W, H), effect)
    sc.glass(60, 150, 960, 700, 60, bevel=34, lens=34, dispersion=0.22)
    sc.glass(60, 950, 960, 140, 70)
    for i in range(3):
        sc.glass(80 + i * 320, 1180, 280, 280, 90, dispersion=0.18)
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
