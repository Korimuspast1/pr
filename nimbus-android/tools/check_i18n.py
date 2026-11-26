#!/usr/bin/env python3
"""Checks the English catalogue against the strings the app actually uses.

`i18n/LocaleEn.kt` is generated (tools/generate_kotlin_data.py) from the desktop's Russian source
plus EXTRA_TEXTS there, so it can fall behind the Kotlin screens: a new `tr("…")` with no English
entry shows the user the Russian. I18nTests.englishCoversEveryText fails the build for the same
reason, but this script runs without a JDK and says which file each string came from.

    python3 tools/check_i18n.py            # list what is missing
    python3 tools/check_i18n.py --strict   # ... and exit non-zero

It also reports placeholders that a translation lost or renamed, and Cyrillic left in an English
value, both of which the same test checks.
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "app" / "src" / "main" / "java"
CATALOGUE = SRC / "com" / "zalchik" / "nimbus" / "i18n" / "LocaleEn.kt"

# tr("…"), tr("…", "ctx"), plural(n, "one", "few", "many")
TR = re.compile(r'\btr\(\s*"((?:[^"\\]|\\.)*)"(?:\s*,\s*"((?:[^"\\]|\\.)*)")?')
PLURAL = re.compile(r'\bplural\([^,]+,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"')
ENTRY = re.compile(r'^\s*"((?:[^"\\]|\\.)*)" to (?:"((?:[^"\\]|\\.)*)"|Pair\("((?:[^"\\]|\\.)*)", "((?:[^"\\]|\\.)*)"\))', re.M)
PLACEHOLDER = re.compile(r"\{(\w+)\}")
CYRILLIC = re.compile(r"[\u0400-\u04FF]")


def unescape(text: str) -> str:
    """A Kotlin string literal's escapes, without touching the UTF-8 that is already decoded."""
    out = []
    i = 0
    while i < len(text):
        ch = text[i]
        if ch != "\\" or i + 1 >= len(text):
            out.append(ch)
            i += 1
            continue
        nxt = text[i + 1]
        if nxt == "u" and i + 5 < len(text) + 1:
            out.append(chr(int(text[i + 2 : i + 6], 16)))
            i += 6
            continue
        out.append({"n": "\n", "t": "\t", "r": "\r", "b": "\b", "$": "$"}.get(nxt, nxt))
        i += 2
    return "".join(out)


def catalogue() -> tuple[dict[str, str], set[str]]:
    """(Russian -> English, plural keys) from the generated file."""
    texts: dict[str, str] = {}
    plurals: set[str] = set()
    for m in ENTRY.finditer(CATALOGUE.read_text(encoding="utf-8")):
        key = unescape(m.group(1))
        if m.group(2) is not None:
            texts[key] = unescape(m.group(2))
        else:
            plurals.add(key.split("|")[-1])
            texts[key] = unescape(m.group(3))
    return texts, plurals


def used() -> tuple[dict[str, set[str]], dict[str, set[str]], dict[str, str]]:
    """(texts -> files, plural "one" forms -> files, text -> context) from the Kotlin sources."""
    texts: dict[str, set[str]] = {}
    plurals: dict[str, set[str]] = {}
    contexts: dict[str, str] = {}
    for path in sorted(SRC.rglob("*.kt")):
        if path == CATALOGUE:
            continue
        source = path.read_text(encoding="utf-8")
        name = path.relative_to(SRC / "com" / "zalchik" / "nimbus").as_posix()
        for m in TR.finditer(source):
            text = unescape(m.group(1))
            texts.setdefault(text, set()).add(name)
            if m.group(2):
                contexts[text] = unescape(m.group(2))
        for m in PLURAL.finditer(source):
            plurals.setdefault(unescape(m.group(1)), set()).add(name)
    return texts, plurals, contexts


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--strict", action="store_true", help="exit non-zero when anything is wrong")
    args = parser.parse_args()

    known, known_plurals = catalogue()
    texts, plurals, contexts = used()

    problems = 0

    missing = sorted(t for t in texts if t not in known and f"{contexts.get(t, '')}|{t}".lstrip("|") not in known)
    if missing:
        problems += len(missing)
        print(f"{len(missing)} text(s) with no English entry:")
        for text in missing:
            print(f'  "{text}"  <- {", ".join(sorted(texts[text]))}')

    missing_plurals = sorted(p for p in plurals if p not in known_plurals)
    if missing_plurals:
        problems += len(missing_plurals)
        print(f"{len(missing_plurals)} plural form(s) with no English entry:")
        for form in missing_plurals:
            print(f'  "{form}"  <- {", ".join(sorted(plurals[form]))}')

    for text, english in sorted(known.items()):
        source = text.split("|")[-1]
        want, have = set(PLACEHOLDER.findall(source)), set(PLACEHOLDER.findall(english))
        if want != have:
            problems += 1
            print(f'  placeholders {sorted(want)} became {sorted(have)}: "{text}" -> "{english}"')
        if CYRILLIC.search(english):
            problems += 1
            print(f'  Cyrillic left in the English: "{text}" -> "{english}"')

    if not problems:
        print(f"OK: {len(texts)} text(s) and {len(plurals)} plural form(s) used, {len(known)} catalogue entries.")
        return 0
    print(f"\n{problems} problem(s).")
    return 1 if args.strict else 0


if __name__ == "__main__":
    sys.exit(main())
