#!/usr/bin/env python3
"""Build and validate the installable Custom Profile .elyx archive.

Run from anywhere:
    python3 custom-profile/tools/build.py
    python3 custom-profile/tools/build.py --check
"""

from __future__ import annotations

import argparse
import hashlib
import sys
import zipfile
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
ARCHIVE = REPOSITORY / "release" / "custom-profile.elyx"
FILES = (
    "refmap.yml",
    "metainfo.yml",
    "main.py",
    "README.md",
    "LICENSE",
    "strings/strings_en.yml",
    "strings/strings_ru.yml",
)
REQUIRED_ROOT = {"refmap.yml", "metainfo.yml", "main.py"}


def validate_source() -> None:
    missing = [relative for relative in FILES if not (PROJECT / relative).is_file()]
    if missing:
        raise RuntimeError("Missing package source file(s): " + ", ".join(missing))


def build() -> Path:
    validate_source()
    ARCHIVE.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ARCHIVE, "w", compression=zipfile.ZIP_DEFLATED) as package:
        # Directory records make strict Elyx archive readers happy.
        package.writestr("strings/", b"")
        for relative in FILES:
            package.write(PROJECT / relative, relative)
    return ARCHIVE


def validate_archive() -> str:
    if not ARCHIVE.is_file():
        raise RuntimeError("Archive does not exist: {}".format(ARCHIVE))
    with zipfile.ZipFile(ARCHIVE) as package:
        names = set(package.namelist())
        missing = REQUIRED_ROOT - names
        if missing:
            raise RuntimeError("Archive is missing required root file(s): " + ", ".join(sorted(missing)))
        if any(name.startswith("custom-profile/") for name in names):
            raise RuntimeError("Archive contains an invalid wrapper directory")
        if package.read("refmap.yml").decode("utf-8") != (PROJECT / "refmap.yml").read_text(encoding="utf-8"):
            raise RuntimeError("Archive refmap.yml does not match source")
    return hashlib.sha256(ARCHIVE.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="validate the existing archive without rebuilding")
    args = parser.parse_args()
    try:
        if not args.check:
            build()
        digest = validate_archive()
    except (OSError, RuntimeError, zipfile.BadZipFile) as error:
        print("ERROR:", error, file=sys.stderr)
        return 1
    print("OK {}\nsha256 {}".format(ARCHIVE.relative_to(REPOSITORY), digest))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
