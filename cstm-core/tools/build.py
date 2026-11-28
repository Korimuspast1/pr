#!/usr/bin/env python3
"""Copy the Python CSTM Core source to its installable .plugin release file."""
from __future__ import annotations

import argparse
import filecmp
import hashlib
import shutil
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
SOURCE = PROJECT / "cstm_core.py"
RELEASE = REPOSITORY / "release" / "cstm-core.plugin"
EXAMPLE = PROJECT / "examples" / "neon-profile.cstm"
EXAMPLE_RELEASE = REPOSITORY / "release" / "neon-profile.cstm"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="verify release files without copying")
    args = parser.parse_args()
    if not SOURCE.is_file() or not EXAMPLE.is_file():
        print("ERROR: source or example is missing", file=sys.stderr)
        return 1
    RELEASE.parent.mkdir(parents=True, exist_ok=True)
    if not args.check:
        shutil.copyfile(SOURCE, RELEASE)
        shutil.copyfile(EXAMPLE, EXAMPLE_RELEASE)
    if not RELEASE.is_file() or not EXAMPLE_RELEASE.is_file() or not filecmp.cmp(SOURCE, RELEASE, shallow=False) or not filecmp.cmp(EXAMPLE, EXAMPLE_RELEASE, shallow=False):
        print("ERROR: release does not match source", file=sys.stderr)
        return 1
    digest = hashlib.sha256(RELEASE.read_bytes()).hexdigest()
    print("OK {}\nsha256 {}".format(RELEASE.relative_to(REPOSITORY), digest))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
