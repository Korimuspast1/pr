#!/usr/bin/env python3
"""
Compiles the shader *as the app ships it* (the string inside Shaders.java) with CPU
Skia, and checks the uniform set the Java code sets.

This catches the two failure modes that would otherwise only show up on a device:
  * an AGSL syntax error (the app would silently fall back to the frosted surface);
  * a uniform renamed in the shader but not in GlassRender (same silent fallback).

      python3 tools/verify_shader.py
      python3 tools/verify_shader.py --check-sync   # also tools/sync_shader.py --check

Needs `pip install skia-python`. On a machine without libGL, point LD_LIBRARY_PATH at
stub libEGL/libGL shared objects (CPU rendering never calls into them) — see
docs/HOW-TO-REPLICATE.md, section "Offline verification".
"""
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHADERS_JAVA = os.path.join(ROOT, "app", "src", "main", "java", "dev", "arena",
                            "liquidglasslab", "Shaders.java")
RENDER_JAVA = os.path.join(ROOT, "app", "src", "main", "java", "dev", "arena",
                           "liquidglasslab", "GlassRender.java")

REQUIRED = [
    "margin", "viewSize", "radii", "bevel", "refractPx", "falloff", "dispersion",
    "lightDir", "specStrength", "rimBandMax", "tintColor", "adaptiveTint", "dimAmount",
    "press", "touchPos", "touchAmp",
]


def shipped_shader_source() -> str:
    src = open(SHADERS_JAVA, encoding="utf-8").read()
    body = src[src.index('LIQUID_GLASS = ""'):]
    body = body[body.index("=") + 1:]
    parts = re.findall(r'"((?:[^"\\]|\\.)*)"', body)
    return "".join(p.encode().decode("unicode_escape") for p in parts)


def main() -> int:
    if "--check-sync" in sys.argv:
        rc = subprocess.call([sys.executable, os.path.join(ROOT, "tools", "sync_shader.py"),
                              "--check"])
        if rc != 0:
            return rc

    try:
        import skia
    except ImportError:
        print("skia-python is not installed; skipping the compile check")
        return 0

    src = shipped_shader_source()
    try:
        effect = skia.RuntimeEffect.MakeForShader(src)
    except Exception as exc:  # pragma: no cover - error path
        print("SHADER ERROR:", exc)
        return 1
    if effect is None:
        print("SHADER ERROR: compile returned None")
        return 1

    names = [u.name for u in effect.uniforms()]
    children = [c.name for c in effect.children()]
    missing = [n for n in REQUIRED if n not in names]
    extra = [n for n in names if n not in REQUIRED]
    render = open(RENDER_JAVA, encoding="utf-8").read()
    not_set = [n for n in names if ('"%s"' % n) not in render]

    print("shipped shader compiles: %d chars, %d uniforms, children=%s"
          % (len(src), len(names), children))
    ok = True
    if missing:
        print("missing uniforms:", missing)
        ok = False
    if extra:
        print("unexpected uniforms:", extra)
        ok = False
    if not_set:
        print("uniforms never set from Java:", not_set)
        ok = False
    if children != ["content"]:
        print("expected exactly one `shader content` child")
        ok = False
    print("OK" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
