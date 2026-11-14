#!/usr/bin/env python3
"""Reassemble an APK that the CI published in pieces inside this repository.

`Build ayuox4gg APK` cannot rely on GitHub release assets or on Actions
artifacts from every network, so it commits the APK as `*.part` chunks under
`ayuox4gg/artifacts/<tag>/`. This tool puts them back together and proves the
result is the file the build produced.

Usage
-----
    python3 ayuox4gg/tools/unpack_apk.py ayuox4gg-20261110-0316 --out ~/ayu.apk
    python3 ayuox4gg/tools/unpack_apk.py ayuox4gg-20261110-0316 --verify-only
    python3 ayuox4gg/tools/unpack_apk.py --list

The expected SHA-256 is read from the build's `README.md`, so a corrupted or
tampered chunk is reported instead of silently producing a broken APK.

Exit codes: 0 = written and verified, 1 = hash mismatch or missing parts,
2 = nothing to unpack.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"

# `split -d` produces foo.apk.part00, foo.apk.part01, ... — not `*.part`.
PART_RE = re.compile(r"\.part\d+$")


def parts_of(build_dir: Path) -> list:
    """Chunks of a published build, in order."""
    if not build_dir.is_dir():
        return []
    return sorted(p for p in build_dir.iterdir() if PART_RE.search(p.name))


def available() -> list:
    """Every published build, newest name last."""
    if not ARTIFACTS.is_dir():
        return []
    return sorted(p for p in ARTIFACTS.iterdir() if p.is_dir() and parts_of(p))


def expected_sha(build_dir: Path) -> str:
    """The sha256 recorded in the build's README.md, if any."""
    readme = build_dir / "README.md"
    if not readme.is_file():
        return ""
    match = re.search(r"\|\s*sha256\s*\|\s*`([0-9a-fA-F]{64})`\s*\|",
                      readme.read_text(encoding="utf-8", errors="replace"))
    return match.group(1).lower() if match else ""


def apk_name(build_dir: Path) -> str:
    """`foo.apk.part00` -> `foo.apk`."""
    for part in parts_of(build_dir):
        return PART_RE.sub("", part.name)
    return "ayuox4gg.apk"


def find_build(name: str) -> Path:
    candidate = ARTIFACTS / name
    if candidate.is_dir():
        return candidate
    # allow the shorter tag without a flavour/time suffix
    matches = [p for p in available() if p.name.startswith(name)]
    if len(matches) == 1:
        return matches[0]
    if not matches:
        raise SystemExit("ERROR: no published build named {!r} (see --list)".format(name))
    raise SystemExit("ERROR: {!r} is ambiguous: {}".format(
        name, ", ".join(p.name for p in matches)))


def digest_of(chunks) -> str:
    hasher = hashlib.sha256()
    for chunk in chunks:
        hasher.update(chunk.read_bytes())
    return hasher.hexdigest()


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description="Reassemble an APK published as .part chunks.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("tag", nargs="?", help="build tag, e.g. ayuox4gg-20261110-0316")
    parser.add_argument("--out", help="where to write the .apk (default: next to the parts)")
    parser.add_argument("--verify-only", action="store_true",
                        help="check the chunks against the recorded sha256, write nothing")
    parser.add_argument("--list", action="store_true", help="list published builds")
    args = parser.parse_args(argv)

    if args.list:
        builds = available()
        if not builds:
            print("no published builds yet (run the Build ayuox4gg APK workflow)")
            return 2
        for build in builds:
            parts = parts_of(build)
            size = sum(p.stat().st_size for p in parts)
            sha = expected_sha(build) or "(no digest recorded)"
            print("{:32} {:>3} parts  {:>10} bytes  {}".format(
                build.name, len(parts), size, sha[:16] + "…" if len(sha) > 16 else sha))
        return 0

    if not args.tag:
        parser.error("a build tag is required (or --list)")

    build_dir = find_build(args.tag)
    chunks = parts_of(build_dir)
    if not chunks:
        print("ERROR: {} contains no *.part files".format(build_dir), file=sys.stderr)
        return 2

    expected = expected_sha(build_dir)
    actual = digest_of(chunks)
    size = sum(c.stat().st_size for c in chunks)

    print("build    : {}".format(build_dir.name))
    print("parts    : {} ({:,} bytes)".format(len(chunks), size))
    print("sha256   : {}".format(actual))
    if expected:
        print("expected : {}".format(expected))
        if actual != expected:
            print("ERROR: the chunks do not hash to the recorded digest; "
                  "a part is missing or was modified", file=sys.stderr)
            return 1
        print("verdict  : OK")
    else:
        print("verdict  : no digest recorded in README.md, cannot verify")

    if args.verify_only:
        return 0

    out = Path(args.out).expanduser() if args.out else build_dir / apk_name(build_dir)
    out.parent.mkdir(parents=True, exist_ok=True)
    tmp = out.with_suffix(out.suffix + ".partial")
    with tmp.open("wb") as handle:
        for chunk in chunks:
            handle.write(chunk.read_bytes())
    tmp.replace(out)

    print("written  : {}".format(out))
    print("\nVerify it yourself before installing:")
    print("    python3 ayuox4gg/tools/forensics.py {} --expect-sha256 {}".format(out, actual))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
