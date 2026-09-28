#!/usr/bin/env python3
"""
swap_mod_sounds.py
==================

Take every sound asset out of a DONOR mod archive and push it into a TARGET
mod archive, replacing the target's own sounds. Written for game mods that ship
as .zip (BeamNG.drive, Assetto Corsa, GTA, RoN, Snowrunner, ...), where a
"mod" is just a zip full of assets.

The script never touches the originals: it always writes a brand new zip.

Typical use
-----------
    # see what would happen, change nothing
    python3 tools/swap_mod_sounds.py donor.zip target.zip -o out.zip --dry-run

    # actually build the patched mod
    python3 tools/swap_mod_sounds.py donor.zip target.zip -o out.zip

Matching strategies (--strategy)
--------------------------------
  basename   Replace a target sound when the donor has a sound with the same
             file name (case-insensitive), regardless of folder. Safest: the
             target mod keeps its own folder layout and file names, so any
             config/jbeam/ini that references those names keeps working.
             This is the default.

  tail       Like `basename`, but matches on the longest shared path suffix
             (e.g. sounds/engine/idle.ogg). Stricter; use when both mods share
             a common folder layout.

  mirror     Ignore names entirely. Pair up donor and target sounds in sorted
             order and overwrite target sound #1 with donor sound #1, #2 with
             #2, and so on. Use when the two mods name their files completely
             differently and you just want the donor audio in there.

Extra switches
--------------
  --inject-unmatched   Also copy donor sounds that matched nothing into the
                       target, under the folder where most target sounds live.
  --drop-unmatched     Delete target sounds that got no donor replacement.
  --ext .ogg,.wav      Override the list of extensions treated as "sound".
  --recurse-zips       Also look inside .zip files nested in either archive.
  --report report.json Write a machine-readable summary of every action.

Exit status is 0 on success, 1 if nothing could be matched.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import io
import json
import os
import posixpath
import shutil
import sys
import zipfile
from collections import Counter
from dataclasses import dataclass, field, asdict
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

# ---------------------------------------------------------------------------
# What counts as a "sound"
# ---------------------------------------------------------------------------

# Plain audio containers.
AUDIO_EXTS = {
    ".ogg", ".oga", ".wav", ".wave", ".mp3", ".flac", ".aac", ".m4a", ".mp4a",
    ".opus", ".aif", ".aiff", ".aifc", ".wma", ".ac3", ".caf", ".au", ".snd",
    ".voc", ".ape", ".wv", ".mpc", ".ra", ".amr", ".gsm", ".dts",
}

# Engine/middleware sound containers and banks.
BANK_EXTS = {
    ".bank",          # FMOD Studio bank (BeamNG, many UE/Unity titles)
    ".fsb",           # FMOD sample bank
    ".fev",           # FMOD Designer event file
    ".fdp",
    ".sfk",
    ".wem",           # Wwise encoded media
    ".bnk",           # Wwise soundbank
    ".pck",           # Wwise file package
    ".xwb", ".xsb",   # XACT wave/sound bank
    ".sab", ".mab",   # Frostbite
    ".asnd", ".sbao",
    ".awb", ".acb",   # CRI ADX2
    ".adx", ".hca",
    ".at9", ".vag", ".at3",
    ".sgh", ".sgb",
    ".rsm", ".bcwav", ".bfwav", ".brstm",
}

# Tracker / sequenced music, occasionally used by mods.
TRACKER_EXTS = {".xm", ".it", ".s3m", ".mod", ".mid", ".midi", ".rmi"}

DEFAULT_SOUND_EXTS = AUDIO_EXTS | BANK_EXTS | TRACKER_EXTS

# Folder names that strongly suggest audio content, used only for reporting.
SOUND_DIR_HINTS = ("sound", "sounds", "audio", "sfx", "fmod", "wwise", "voice", "music")

# Never rewrite these, even if an extension somehow matches.
SKIP_GLOBS = ("__MACOSX/*", "*/.DS_Store", ".DS_Store", "*/Thumbs.db")


def is_skipped(name: str) -> bool:
    return any(fnmatch.fnmatch(name, pat) for pat in SKIP_GLOBS)


# ---------------------------------------------------------------------------
# Data model
# ---------------------------------------------------------------------------


@dataclass
class Sound:
    """One sound asset found inside an archive."""

    path: str                      # path as stored in the zip (posix separators)
    size: int
    container: str = ""            # "" = top-level zip, else nested zip path
    data: Optional[bytes] = field(default=None, repr=False)

    @property
    def base(self) -> str:
        return posixpath.basename(self.path)

    @property
    def base_lower(self) -> str:
        return self.base.lower()

    @property
    def stem_lower(self) -> str:
        return posixpath.splitext(self.base)[0].lower()

    @property
    def ext_lower(self) -> str:
        return posixpath.splitext(self.base)[1].lower()

    @property
    def display(self) -> str:
        return f"{self.container}::{self.path}" if self.container else self.path

    def parts_lower(self) -> List[str]:
        return [p.lower() for p in self.path.split("/") if p not in ("", ".")]


@dataclass
class Replacement:
    target: str
    donor: str
    how: str
    old_size: int
    new_size: int


# ---------------------------------------------------------------------------
# Archive scanning
# ---------------------------------------------------------------------------


def human(n: float) -> str:
    for unit in ("B", "KB", "MB", "GB"):
        if abs(n) < 1024.0:
            return f"{n:3.1f}{unit}" if unit != "B" else f"{int(n)}B"
        n /= 1024.0
    return f"{n:.1f}TB"


def scan_archive(
    zip_path: str,
    sound_exts: Iterable[str],
    recurse_zips: bool = False,
    load_data: bool = False,
) -> Tuple[List[Sound], List[str]]:
    """Return (sounds, all_entry_names) for a zip archive."""
    sound_exts = {e.lower() for e in sound_exts}
    sounds: List[Sound] = []
    names: List[str] = []

    with zipfile.ZipFile(zip_path) as zf:
        for info in zf.infolist():
            if info.is_dir():
                continue
            name = info.filename.replace("\\", "/")
            names.append(name)
            if is_skipped(name):
                continue

            ext = posixpath.splitext(name)[1].lower()
            if ext in sound_exts:
                snd = Sound(path=name, size=info.file_size)
                if load_data:
                    snd.data = zf.read(info)
                sounds.append(snd)
            elif recurse_zips and ext == ".zip":
                try:
                    blob = zf.read(info)
                except Exception:
                    continue
                try:
                    with zipfile.ZipFile(io.BytesIO(blob)) as inner:
                        for sub in inner.infolist():
                            if sub.is_dir():
                                continue
                            subname = sub.filename.replace("\\", "/")
                            if is_skipped(subname):
                                continue
                            if posixpath.splitext(subname)[1].lower() in sound_exts:
                                s = Sound(path=subname, size=sub.file_size, container=name)
                                if load_data:
                                    s.data = inner.read(sub)
                                sounds.append(s)
                except zipfile.BadZipFile:
                    pass

    return sounds, names


# ---------------------------------------------------------------------------
# Matching
# ---------------------------------------------------------------------------


def tail_overlap(a: Sequence[str], b: Sequence[str]) -> int:
    """Number of trailing path components shared by a and b."""
    n = 0
    for x, y in zip(reversed(a), reversed(b)):
        if x != y:
            break
        n += 1
    return n


def build_index(sounds: List[Sound]) -> Dict[str, List[Sound]]:
    idx: Dict[str, List[Sound]] = {}
    for s in sounds:
        idx.setdefault(s.base_lower, []).append(s)
    return idx


def match_basename(
    donors: List[Sound], targets: List[Sound], loose_stem: bool = True
) -> Tuple[Dict[str, Sound], List[Sound], List[Sound]]:
    """Map target path -> donor sound, matching on file name."""
    by_base = build_index(donors)
    by_stem: Dict[str, List[Sound]] = {}
    for d in donors:
        by_stem.setdefault(d.stem_lower, []).append(d)

    mapping: Dict[str, Sound] = {}
    used: set = set()
    unmatched_targets: List[Sound] = []

    for t in targets:
        cands = by_base.get(t.base_lower)
        if not cands and loose_stem:
            # same stem, different extension (idle.wav -> idle.ogg)
            cands = [d for d in by_stem.get(t.stem_lower, []) if d.ext_lower != t.ext_lower]
        if not cands:
            unmatched_targets.append(t)
            continue
        # prefer the donor whose folder path best lines up with the target's
        best = max(cands, key=lambda d: tail_overlap(d.parts_lower()[:-1], t.parts_lower()[:-1]))
        mapping[t.path] = best
        used.add(id(best))

    unmatched_donors = [d for d in donors if id(d) not in used]
    return mapping, unmatched_donors, unmatched_targets


def match_tail(
    donors: List[Sound], targets: List[Sound], min_overlap: int = 2
) -> Tuple[Dict[str, Sound], List[Sound], List[Sound]]:
    """Map target -> donor requiring at least `min_overlap` shared trailing parts."""
    mapping: Dict[str, Sound] = {}
    used: set = set()
    unmatched_targets: List[Sound] = []

    donor_parts = [(d, d.parts_lower()) for d in donors]

    for t in targets:
        tp = t.parts_lower()
        best: Optional[Sound] = None
        best_score = 0
        for d, dp in donor_parts:
            score = tail_overlap(dp, tp)
            if score > best_score:
                best, best_score = d, score
        if best is not None and best_score >= min_overlap:
            mapping[t.path] = best
            used.add(id(best))
        else:
            unmatched_targets.append(t)

    unmatched_donors = [d for d in donors if id(d) not in used]
    return mapping, unmatched_donors, unmatched_targets


def match_mirror(
    donors: List[Sound], targets: List[Sound]
) -> Tuple[Dict[str, Sound], List[Sound], List[Sound]]:
    """Positional pairing: donor[i] replaces target[i] (both sorted by path)."""
    ds = sorted(donors, key=lambda s: s.path.lower())
    ts = sorted(targets, key=lambda s: s.path.lower())
    mapping: Dict[str, Sound] = {}

    if not ds:
        return mapping, [], ts

    for i, t in enumerate(ts):
        mapping[t.path] = ds[i % len(ds)]

    used_count = min(len(ds), len(ts))
    return mapping, ds[used_count:], []


# ---------------------------------------------------------------------------
# Writing the patched archive
# ---------------------------------------------------------------------------


def dominant_sound_dir(targets: List[Sound]) -> str:
    """Folder that holds the most target sounds, used for injected extras."""
    if not targets:
        return "sounds"
    dirs = Counter(posixpath.dirname(t.path) for t in targets)
    return dirs.most_common(1)[0][0] or "sounds"


def build_output(
    donor_zip: str,
    target_zip: str,
    out_zip: str,
    mapping: Dict[str, Sound],
    inject: List[Sound],
    drop: List[Sound],
    inject_dir: str,
    compresslevel: Optional[int] = None,
) -> Tuple[List[Replacement], List[str], List[str]]:
    """Write out_zip = target_zip with donor audio swapped in."""
    replacements: List[Replacement] = []
    injected: List[str] = []
    dropped: List[str] = []
    drop_set = {d.path for d in drop}

    # Pull the donor bytes we actually need, once.
    needed: Dict[Tuple[str, str], Optional[bytes]] = {}
    for d in list(mapping.values()) + inject:
        needed[(d.container, d.path)] = None

    with zipfile.ZipFile(donor_zip) as dz:
        top = {k: v for k, v in needed.items() if k[0] == ""}
        for (container, path) in top:
            needed[(container, path)] = dz.read(path)

        nested = sorted({k[0] for k in needed if k[0] != ""})
        for container in nested:
            blob = dz.read(container)
            with zipfile.ZipFile(io.BytesIO(blob)) as inner:
                for (c, path) in [k for k in needed if k[0] == container]:
                    needed[(c, path)] = inner.read(path)

    os.makedirs(os.path.dirname(os.path.abspath(out_zip)) or ".", exist_ok=True)

    with zipfile.ZipFile(target_zip) as tz, zipfile.ZipFile(
        out_zip, "w", compression=zipfile.ZIP_DEFLATED
    ) as oz:
        for info in tz.infolist():
            name = info.filename.replace("\\", "/")

            if info.is_dir():
                oz.writestr(info, b"")
                continue

            if name in drop_set:
                dropped.append(name)
                continue

            donor = mapping.get(name)
            if donor is not None:
                data = needed[(donor.container, donor.path)]
                assert data is not None
                new_info = zipfile.ZipInfo(name, date_time=info.date_time)
                new_info.compress_type = zipfile.ZIP_DEFLATED
                new_info.external_attr = info.external_attr
                new_info.create_system = info.create_system
                oz.writestr(new_info, data)
                replacements.append(
                    Replacement(
                        target=name,
                        donor=donor.display,
                        how="replaced",
                        old_size=info.file_size,
                        new_size=len(data),
                    )
                )
            else:
                oz.writestr(info, tz.read(info))

        # extra donor sounds that matched nothing
        existing = {i.filename.replace("\\", "/") for i in tz.infolist()}
        for d in inject:
            dest = posixpath.join(inject_dir, d.base) if inject_dir else d.base
            n = 1
            stem, ext = posixpath.splitext(dest)
            while dest in existing:
                dest = f"{stem}_{n}{ext}"
                n += 1
            existing.add(dest)
            data = needed[(d.container, d.path)]
            assert data is not None
            oz.writestr(dest, data)
            injected.append(dest)

    return replacements, injected, dropped


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def parse_exts(raw: str) -> set:
    out = set()
    for piece in raw.replace(";", ",").split(","):
        piece = piece.strip().lower()
        if not piece:
            continue
        if not piece.startswith("."):
            piece = "." + piece
        out.add(piece)
    return out


def main(argv: Optional[List[str]] = None) -> int:
    p = argparse.ArgumentParser(
        description="Move every sound from a donor mod zip into a target mod zip.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    p.add_argument("donor", help="mod #1 - the archive whose sounds you want to KEEP")
    p.add_argument("target", help="mod #2 - the archive whose sounds get REPLACED")
    p.add_argument("-o", "--output", help="path of the patched zip to write")
    p.add_argument(
        "--strategy",
        choices=("basename", "tail", "mirror"),
        default="basename",
        help="how donor sounds are paired with target sounds (default: basename)",
    )
    p.add_argument("--min-overlap", type=int, default=2, help="for --strategy tail")
    p.add_argument("--no-loose-stem", action="store_true",
                   help="for --strategy basename: require the extension to match too")
    p.add_argument("--inject-unmatched", action="store_true",
                   help="also add donor sounds that matched nothing")
    p.add_argument("--inject-dir", default=None,
                   help="folder for injected extras (default: the target's main sound folder)")
    p.add_argument("--drop-unmatched", action="store_true",
                   help="delete target sounds that received no donor replacement")
    p.add_argument("--ext", default=None,
                   help="comma-separated extension list overriding the built-in one")
    p.add_argument("--recurse-zips", action="store_true",
                   help="also scan zips nested inside the archives")
    p.add_argument("--report", default=None, help="write a JSON summary here")
    p.add_argument("--dry-run", action="store_true", help="analyse only, write nothing")
    p.add_argument("-q", "--quiet", action="store_true")

    args = p.parse_args(argv)

    for path in (args.donor, args.target):
        if not os.path.isfile(path):
            p.error(f"no such file: {path}")
        if not zipfile.is_zipfile(path):
            p.error(f"not a zip archive: {path}")

    out = args.output
    if not out and not args.dry_run:
        stem = os.path.splitext(os.path.basename(args.target))[0]
        out = f"{stem}_with_donor_sounds.zip"

    exts = parse_exts(args.ext) if args.ext else DEFAULT_SOUND_EXTS

    def say(*a):
        if not args.quiet:
            print(*a)

    say(f"donor  : {args.donor}  ({human(os.path.getsize(args.donor))})")
    say(f"target : {args.target}  ({human(os.path.getsize(args.target))})")
    say(f"strategy: {args.strategy}")
    say("")

    donors, donor_names = scan_archive(args.donor, exts, args.recurse_zips)
    targets, target_names = scan_archive(args.target, exts, args.recurse_zips)

    say(f"donor  : {len(donor_names):5d} entries, {len(donors):4d} sound files "
        f"({human(sum(s.size for s in donors))})")
    say(f"target : {len(target_names):5d} entries, {len(targets):4d} sound files "
        f"({human(sum(s.size for s in targets))})")
    say("")

    if not donors:
        print("ERROR: the donor archive contains no recognised sound files.", file=sys.stderr)
        print("       Try --ext to widen the extension list, or --recurse-zips", file=sys.stderr)
        print("       if the mod ships a zip inside the zip.", file=sys.stderr)
        _preview_exts(donor_names, "donor")
        return 1

    if not targets:
        print("ERROR: the target archive contains no recognised sound files.", file=sys.stderr)
        _preview_exts(target_names, "target")
        return 1

    if args.strategy == "basename":
        mapping, un_donors, un_targets = match_basename(
            donors, targets, loose_stem=not args.no_loose_stem
        )
    elif args.strategy == "tail":
        mapping, un_donors, un_targets = match_tail(donors, targets, args.min_overlap)
    else:
        mapping, un_donors, un_targets = match_mirror(donors, targets)

    say(f"matched   : {len(mapping)} target sound(s) will be overwritten")
    say(f"donor left: {len(un_donors)} donor sound(s) matched nothing")
    say(f"target left: {len(un_targets)} target sound(s) keep their original audio")
    say("")

    # A donor .wav written into a target .ogg keeps the target's name but has
    # the donor's container format. Most engines pick the decoder from the
    # extension, so this usually has to be re-encoded before it will play.
    xfmt = [
        (t, mapping[t])
        for t in sorted(mapping)
        if posixpath.splitext(t)[1].lower() != mapping[t].ext_lower
    ]

    if not args.quiet and mapping:
        say("replacement plan:")
        for tpath in sorted(mapping):
            d = mapping[tpath]
            flag = "  [!] format mismatch" if posixpath.splitext(tpath)[1].lower() != d.ext_lower else ""
            say(f"  {tpath}")
            say(f"      <- {d.display}  ({human(d.size)}){flag}")
        say("")

    if xfmt:
        say(f"WARNING: {len(xfmt)} replacement(s) put audio of one container format")
        say("         into a file named with a different extension:")
        for t, d in xfmt:
            say(f"           {posixpath.splitext(t)[1]} <- {d.ext_lower}   {posixpath.basename(t)}")
        say("         Most games choose the decoder from the extension, so these")
        say("         need re-encoding (e.g. ffmpeg -i in.wav out.ogg) or the sound")
        say("         will fail to load. Pass --no-loose-stem to skip such pairs.")
        say("")

    if not args.quiet and un_donors:
        say("donor sounds with no match"
            + (" (will be injected):" if args.inject_unmatched else " (ignored):"))
        for d in sorted(un_donors, key=lambda s: s.path.lower()):
            say(f"  {d.display}  ({human(d.size)})")
        say("")

    if not args.quiet and un_targets:
        say("target sounds left untouched"
            + (" (will be deleted):" if args.drop_unmatched else ":"))
        for t in sorted(un_targets, key=lambda s: s.path.lower()):
            say(f"  {t.path}  ({human(t.size)})")
        say("")

    if not mapping and not (args.inject_unmatched and un_donors):
        print("ERROR: nothing to do - no sound could be paired up.", file=sys.stderr)
        print("       Try --strategy tail, or --strategy mirror to pair them", file=sys.stderr)
        print("       positionally, or --inject-unmatched to just add the donor", file=sys.stderr)
        print("       audio alongside the originals.", file=sys.stderr)
        return 1

    inject_dir = args.inject_dir
    if inject_dir is None:
        inject_dir = dominant_sound_dir(targets)

    report = {
        "donor": os.path.abspath(args.donor),
        "target": os.path.abspath(args.target),
        "output": os.path.abspath(out) if out else None,
        "strategy": args.strategy,
        "donor_sounds": [s.display for s in donors],
        "target_sounds": [s.path for s in targets],
        "replacements": [],
        "injected": [],
        "dropped": [],
    }

    if args.dry_run:
        say("--dry-run: no file written.")
        if args.report:
            report["replacements"] = [
                asdict(Replacement(t, mapping[t].display, "planned", 0, mapping[t].size))
                for t in sorted(mapping)
            ]
            with open(args.report, "w", encoding="utf-8") as fh:
                json.dump(report, fh, indent=2, ensure_ascii=False)
            say(f"report -> {args.report}")
        return 0

    reps, injected, dropped = build_output(
        donor_zip=args.donor,
        target_zip=args.target,
        out_zip=out,
        mapping=mapping,
        inject=un_donors if args.inject_unmatched else [],
        drop=un_targets if args.drop_unmatched else [],
        inject_dir=inject_dir,
    )

    report["replacements"] = [asdict(r) for r in reps]
    report["injected"] = injected
    report["dropped"] = dropped

    say(f"wrote {out}  ({human(os.path.getsize(out))})")
    say(f"  replaced : {len(reps)}")
    say(f"  injected : {len(injected)}")
    say(f"  dropped  : {len(dropped)}")

    # sanity: the new zip must still open cleanly
    with zipfile.ZipFile(out) as z:
        bad = z.testzip()
    if bad:
        print(f"WARNING: corrupt entry in output: {bad}", file=sys.stderr)
        return 1
    say("  integrity: OK")

    if args.report:
        with open(args.report, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=2, ensure_ascii=False)
        say(f"report -> {args.report}")

    return 0


def _preview_exts(names: List[str], label: str) -> None:
    exts = Counter(posixpath.splitext(n)[1].lower() for n in names if posixpath.splitext(n)[1])
    if exts:
        print(f"\n  extensions present in the {label} archive:", file=sys.stderr)
        for ext, n in exts.most_common(25):
            print(f"    {ext:12s} x{n}", file=sys.stderr)


if __name__ == "__main__":
    sys.exit(main())
