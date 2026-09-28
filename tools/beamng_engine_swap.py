#!/usr/bin/env python3
"""
beamng_engine_swap.py
=====================

Transplant the engine/exhaust sound of one BeamNG.drive vehicle mod into
another one.

Why a dedicated tool
--------------------
In BeamNG a vehicle does not just "have wav files". The chain is:

    <part>.jbeam   "soundConfig": "soundConfig_2T"
        -> "soundConfig_2T": { "sampleName": "motocross", ... }
            -> art/sound/blends/motocross.sfxBlend2D.json
                -> [["art/sound/engine/motocross/2000.wav", 2000], ...]

So dropping another mod's .wav files into the zip changes nothing: the blend
file still points at the old sample paths. This script moves the samples AND
rewires the blend, so the target vehicle really plays the donor's engine.

What it does
------------
1. copies every sound sample from the donor into the output, at the donor's
   own paths, so the donor's blend definitions stay valid;
2. replaces the "samples" array of each of the target's blend files with the
   donor's, keeping the target's own file name, eventName and header - which
   means no jbeam edit is needed for the engine and nothing else breaks;
3. deletes the target's now-unreferenced sample files;
4. optionally (--with-exhaust) adds the donor's exhaust blend and wires a
   soundConfigExhaust into the target's jbeam, mirroring how the mod's own
   4-stroke config does it. The patched jbeam is parsed back before being
   accepted; if it does not parse, the exhaust step is skipped and the engine
   swap is still delivered.

Usage
-----
    python3 tools/beamng_engine_swap.py donor.zip target.zip -o out.zip \
        --with-exhaust --report report.json
"""

from __future__ import annotations

import argparse
import json
import os
import posixpath
import re
import sys
import zipfile
from collections import OrderedDict
from typing import Dict, List, Optional, Tuple

AUDIO_EXTS = {".wav", ".ogg", ".oga", ".flac", ".mp3", ".aiff", ".aif", ".opus"}
BLEND_SUFFIX = ".sfxblend2d.json"


def log(m: str) -> None:
    print(m, flush=True)


def human(n: float) -> str:
    for u in ("B", "KB", "MB", "GB"):
        if abs(n) < 1024:
            return f"{int(n)}{u}" if u == "B" else f"{n:.1f}{u}"
        n /= 1024
    return f"{n:.1f}TB"


# ---------------------------------------------------------------------------
# jbeam / jsonc parsing (BeamNG allows // comments and trailing commas)
# ---------------------------------------------------------------------------

def strip_jsonc(text: str) -> str:
    out = []
    i, n = 0, len(text)
    in_str = False
    esc = False
    while i < n:
        c = text[i]
        if in_str:
            out.append(c)
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                in_str = False
            i += 1
            continue
        if c == '"':
            in_str = True
            out.append(c)
            i += 1
            continue
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] not in "\r\n":
                i += 1
            continue
        if c == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
            continue
        out.append(c)
        i += 1
    s = "".join(out)
    s = re.sub(r",(\s*[}\]])", r"\1", s)   # trailing commas
    return s


def insert_missing_commas(text: str) -> str:
    """jbeam lets you omit the comma between consecutive rows/entries.

        "slots": [
            ["type", "default", "description"]      <- no comma
            ["eng_transmission", "...", "..."],
        ]

    Strict JSON rejects that, so put the commas back. String-aware, and only
    fires when a value has just ended and another value is starting.
    """
    out: List[str] = []
    prev_sig = ""          # last significant (non-whitespace) char emitted
    i, n = 0, len(text)
    in_str = False
    esc = False

    while i < n:
        c = text[i]

        if in_str:
            out.append(c)
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == '"':
                in_str = False
                prev_sig = '"'
            i += 1
            continue

        if c.isspace():
            out.append(c)
            i += 1
            continue

        # a value is starting here
        if c in '[{"':
            # NB: `"" in ']}"'` is True in Python, so guard on prev_sig first
            ends_value = bool(prev_sig) and (
                prev_sig in ']}"' or prev_sig.isalnum() or prev_sig == "."
            )
            if ends_value:
                out.append(",")
            out.append(c)
            if c == '"':
                in_str = True
            prev_sig = c
            i += 1
            continue

        out.append(c)
        prev_sig = c
        i += 1

    return "".join(out)


def parse_jsonc(text: str):
    text = text.lstrip("\ufeff\r\n\t ")   # many mod jbeams carry a UTF-8 BOM
    cleaned = strip_jsonc(text)
    try:
        return json.loads(cleaned, object_pairs_hook=OrderedDict)
    except json.JSONDecodeError:
        # retry with jbeam's optional commas restored
        fixed = re.sub(r",(\s*[}\]])", r"\1", insert_missing_commas(cleaned))
        return json.loads(fixed, object_pairs_hook=OrderedDict)


# ---------------------------------------------------------------------------
# archive inspection
# ---------------------------------------------------------------------------

def entries(zf: zipfile.ZipFile) -> List[str]:
    return [i.filename.replace("\\", "/") for i in zf.infolist() if not i.is_dir()]


def find_blends(zf: zipfile.ZipFile) -> Dict[str, dict]:
    out: Dict[str, dict] = {}
    for name in entries(zf):
        if name.lower().endswith(BLEND_SUFFIX):
            try:
                out[name] = parse_jsonc(zf.read(name).decode("utf-8", "replace"))
            except Exception as e:
                log(f"  ! could not parse blend {name}: {e}")
    return out


def blend_role(path: str) -> str:
    return "exhaust" if "exhaust" in posixpath.basename(path).lower() else "engine"


def samples_of(blend: dict) -> List[List]:
    s = blend.get("samples")
    return s if isinstance(s, list) else []


def sample_paths(blend: dict) -> List[str]:
    paths = []
    for layer in samples_of(blend):
        if not isinstance(layer, list):
            continue
        for entry in layer:
            if isinstance(entry, list) and entry and isinstance(entry[0], str):
                paths.append(entry[0])
    return paths


def count_samples(blend: dict) -> int:
    return len(sample_paths(blend))


def pick_blend(blends: Dict[str, dict], role: str) -> Optional[Tuple[str, dict]]:
    cands = [(p, b) for p, b in blends.items() if blend_role(p) == role]
    if not cands:
        return None
    return max(cands, key=lambda pb: count_samples(pb[1]))


def dump_blend(blend: dict) -> bytes:
    """Serialise a blend the way BeamNG's own files look."""
    lines = ["{"]
    hdr = blend.get("header", {"version": 1})
    lines.append('    "header" : {')
    hitems = list(hdr.items())
    for k, (hk, hv) in enumerate(hitems):
        comma = "," if k < len(hitems) - 1 else ""
        lines.append(f'        "{hk}" : {json.dumps(hv)}{comma}')
    lines.append("    },")
    if "eventName" in blend:
        lines.append(f'    "eventName" : {json.dumps(blend["eventName"])},')
    lines.append('    "samples" :')
    lines.append("    [")
    layers = samples_of(blend)
    for li, layer in enumerate(layers):
        lines.append("        [")
        for entry in layer:
            lines.append(f'            [{json.dumps(entry[0])}, {json.dumps(entry[1])}],')
        lines.append("        ]" + ("," if li < len(layers) - 1 else ""))
    lines.append("    ]")
    lines.append("}")
    return ("\n".join(lines) + "\n").encode("utf-8")


# ---------------------------------------------------------------------------
# jbeam patching for the exhaust channel
# ---------------------------------------------------------------------------

EXHAUST_BLOCK = '''\t"{cfg}": {{
        "sampleName": "{sample}",

        "mainGain": {main_gain},
        "onLoadGain": 1.0,
        "offLoadGain": 0.7,

        "maxLoadMix": 0.75,
        "minLoadMix": 0,

        "eqLowGain": 8,
        "eqLowFreq": 450,
        "eqLowWidth": 0.3,

        "eqHighGain": 10,
        "eqHighFreq": 7500,
        "eqHighWidth": 0.25,

        "lowShelfGain": 0,
        "lowShelfFreq": 100,

        "highShelfGain": -6,
        "highShelfFreq": 8000,
    }},
'''


def patch_jbeam_exhaust(text: str, engine_cfg: str, exhaust_cfg: str,
                        exhaust_sample: str, main_gain: float) -> Optional[str]:
    """Add soundConfigExhaust next to soundConfig and define the block.

    Returns the patched text, or None if the shape was not recognised or the
    result does not parse.
    """
    # already wired?
    if re.search(r'"soundConfigExhaust"\s*:\s*"%s"' % re.escape(exhaust_cfg), text):
        return None

    ref = re.compile(r'^([ \t]*)"soundConfig"\s*:\s*"%s"\s*,?\s*$' % re.escape(engine_cfg), re.M)
    m = ref.search(text)
    if not m:
        return None
    indent = m.group(1)
    text2 = text[:m.end()] + f'\n{indent}"soundConfigExhaust": "{exhaust_cfg}",' + text[m.end():]

    # define the block right before the engine sound config definition
    defre = re.compile(r'^[ \t]*"%s"\s*:\s*\{' % re.escape(engine_cfg), re.M)
    m2 = defre.search(text2)
    if not m2:
        return None
    block = EXHAUST_BLOCK.format(cfg=exhaust_cfg, sample=exhaust_sample, main_gain=main_gain)
    text3 = text2[:m2.start()] + block + text2[m2.start():]

    try:
        parse_jsonc(text3)
    except Exception as e:
        log(f"  ! patched jbeam does not parse ({e}); skipping exhaust wiring")
        return None
    return text3


def find_engine_jbeam(zf: zipfile.ZipFile, engine_cfg_hint: Optional[str] = None
                      ) -> Optional[Tuple[str, str, str]]:
    """Locate the jbeam defining a soundConfig. Returns (path, text, cfgname)."""
    best = None
    for name in entries(zf):
        if not name.lower().endswith(".jbeam"):
            continue
        try:
            text = zf.read(name).decode("utf-8", "replace")
        except Exception:
            continue
        for m in re.finditer(r'"soundConfig"\s*:\s*"([^"]+)"', text):
            cfg = m.group(1)
            has_exh = bool(re.search(
                r'"soundConfig"\s*:\s*"%s"\s*,\s*\n\s*"soundConfigExhaust"' % re.escape(cfg), text))
            score = (0 if has_exh else 1, len(text))
            if best is None or score > best[0]:
                best = (score, name, text, cfg)
    if best is None:
        return None
    return best[1], best[2], best[3]


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser(description="Transplant a BeamNG mod's engine sound into another mod.")
    ap.add_argument("donor", help="mod whose SOUND you want")
    ap.add_argument("target", help="mod that should receive the sound")
    ap.add_argument("-o", "--output", required=True)
    ap.add_argument("--with-exhaust", action="store_true",
                    help="also bring the donor's exhaust layer and wire it into the jbeam")
    ap.add_argument("--keep-old-samples", action="store_true",
                    help="do not delete the target's original, now unreferenced, wav files")
    ap.add_argument("--report", default=None)
    a = ap.parse_args()

    for p in (a.donor, a.target):
        if not zipfile.is_zipfile(p):
            ap.error(f"not a zip: {p}")

    report: Dict[str, object] = {"donor": a.donor, "target": a.target, "output": a.output}

    dz = zipfile.ZipFile(a.donor)
    tz = zipfile.ZipFile(a.target)

    log(f"donor  : {a.donor}  ({human(os.path.getsize(a.donor))})")
    log(f"target : {a.target}  ({human(os.path.getsize(a.target))})")
    log("")

    dblends = find_blends(dz)
    tblends = find_blends(tz)
    log(f"donor blends : {list(dblends)}")
    log(f"target blends: {list(tblends)}")
    if not dblends:
        log("ERROR: the donor has no .sfxBlend2D.json - it does not define a custom engine sound.")
        return 1
    if not tblends:
        log("ERROR: the target has no .sfxBlend2D.json to rewire.")
        return 1

    d_engine = pick_blend(dblends, "engine")
    d_exhaust = pick_blend(dblends, "exhaust")
    if d_engine is None:
        log("ERROR: could not identify the donor's engine blend.")
        return 1
    log(f"\ndonor engine blend : {d_engine[0]}  ({count_samples(d_engine[1])} samples)")
    if d_exhaust:
        log(f"donor exhaust blend: {d_exhaust[0]}  ({count_samples(d_exhaust[1])} samples)")

    donor_sounds = [n for n in entries(dz) if posixpath.splitext(n)[1].lower() in AUDIO_EXTS]
    log(f"donor samples on disk: {len(donor_sounds)} "
        f"({human(sum(dz.getinfo(n).file_size for n in donor_sounds))})")

    # target samples referenced by its blends -> these become dead weight
    target_referenced = set()
    for b in tblends.values():
        target_referenced.update(sample_paths(b))
    log(f"target samples referenced by its blends: {len(target_referenced)}")

    # ------------------------------------------------------------------
    # decide the exhaust wiring before writing anything
    # ------------------------------------------------------------------
    exhaust_plan = None
    if a.with_exhaust and d_exhaust is not None:
        found = find_engine_jbeam(tz, None)
        if found is None:
            log("\n! no jbeam with a soundConfig found; skipping exhaust wiring")
        else:
            jb_path, jb_text, engine_cfg = found
            t_engine_blend = pick_blend(tblends, "engine")
            base_sample = posixpath.basename(t_engine_blend[0])[: -len(".sfxBlend2D.json")] \
                if t_engine_blend else "engine"
            exhaust_sample = f"{base_sample}_exhaust"
            exhaust_cfg = engine_cfg.replace("soundConfig", "soundConfigExhaust", 1)
            if exhaust_cfg == engine_cfg:
                exhaust_cfg = engine_cfg + "_exhaust"
            # match the engine config's gain so the exhaust does not drown it
            mg = 0.2
            m = re.search(r'"%s"\s*:\s*\{[^}]*?"mainGain"\s*:\s*([0-9.]+)'
                          % re.escape(engine_cfg), jb_text, re.S)
            if m:
                try:
                    mg = round(float(m.group(1)), 3)
                except ValueError:
                    pass
            patched = patch_jbeam_exhaust(jb_text, engine_cfg, exhaust_cfg, exhaust_sample, mg)
            if patched:
                blend_dir = posixpath.dirname(t_engine_blend[0]) if t_engine_blend \
                    else "art/sound/blends"
                exhaust_blend_path = posixpath.join(blend_dir, f"{exhaust_sample}.sfxBlend2D.json")
                exhaust_plan = {
                    "jbeam": jb_path, "jbeam_text": patched,
                    "engine_cfg": engine_cfg, "exhaust_cfg": exhaust_cfg,
                    "sample": exhaust_sample, "blend_path": exhaust_blend_path,
                    "main_gain": mg,
                }
                log(f"\nexhaust wiring: {jb_path}")
                log(f"  {engine_cfg} + {exhaust_cfg} -> sampleName {exhaust_sample!r} (mainGain {mg})")
                log(f"  new blend: {exhaust_blend_path}")
            else:
                log("\n! exhaust wiring not applied (shape not recognised or parse failed)")

    # ------------------------------------------------------------------
    # write the output
    # ------------------------------------------------------------------
    rewritten, dropped, added = [], [], []
    os.makedirs(os.path.dirname(os.path.abspath(a.output)) or ".", exist_ok=True)

    with zipfile.ZipFile(a.output, "w", zipfile.ZIP_DEFLATED) as oz:
        for info in tz.infolist():
            name = info.filename.replace("\\", "/")
            if info.is_dir():
                oz.writestr(info, b"")
                continue

            if name in tblends:
                role = blend_role(name)
                src = d_exhaust if (role == "exhaust" and d_exhaust) else d_engine
                new = OrderedDict(tblends[name])          # keep header + eventName
                new["samples"] = samples_of(src[1])       # donor's sample table
                oz.writestr(name, dump_blend(new))
                rewritten.append({"blend": name, "from": src[0],
                                  "samples": count_samples(new)})
                continue

            if name in target_referenced and not a.keep_old_samples:
                dropped.append(name)
                continue

            if exhaust_plan and name == exhaust_plan["jbeam"]:
                oz.writestr(info, exhaust_plan["jbeam_text"].encode("utf-8"))
                continue

            oz.writestr(info, tz.read(info))

        existing = set(entries(tz))
        for name in donor_sounds:
            if name in existing:
                continue
            oz.writestr(name, dz.read(name))
            added.append(name)

        if exhaust_plan:
            oz.writestr(exhaust_plan["blend_path"], dump_blend(d_exhaust[1]))
            added.append(exhaust_plan["blend_path"])

    with zipfile.ZipFile(a.output) as z:
        bad = z.testzip()
    if bad:
        log(f"ERROR: corrupt entry in output: {bad}")
        return 1

    log("")
    log(f"wrote {a.output}  ({human(os.path.getsize(a.output))})")
    log(f"  blends rewired   : {len(rewritten)}")
    for r in rewritten:
        log(f"      {r['blend']}  <- {r['from']}  ({r['samples']} samples)")
    log(f"  donor samples in : {len(added)}")
    log(f"  old samples out  : {len(dropped)}")
    for d in dropped:
        log(f"      {d}")
    log("  integrity        : OK")

    report.update({"rewritten": rewritten, "added": added, "dropped": dropped,
                   "exhaust": {k: v for k, v in (exhaust_plan or {}).items()
                               if k != "jbeam_text"} or None})
    if a.report:
        with open(a.report, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=2)
        log(f"report -> {a.report}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
