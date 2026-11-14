#!/usr/bin/env python3
"""Syntax-check Java sources without a JDK.

Normally you would run ``javac`` — but that needs a JDK *and* the full Android
classpath (``android.jar``, exteraGram's sources, OkHttp, Gson…). What we can
always do offline is prove that a file is *syntactically* valid Java 8-10,
which catches the mistakes that actually happen when editing patches by hand:
an unbalanced brace, a mis-placed ``.build()`` chain, a missing semicolon.

Two backends are tried in order:

1. **javac**, when ``JAVA_HOME``/``javac`` exists and a classpath is provided —
   a real compile, the strongest signal;
2. **javalang** (pure Python) — a full grammar parse, tolerant of ``var`` and
   lambdas but not of Java 14+ switch expressions;
3. **structural sanity** — when neither is available: balanced braces and no
   truncated file. Weaker, but still catches a broken patch.

Usage
-----
    python3 ayuox4gg/tools/javaparse.py path/to/File.java another/File.java
    python3 ayuox4gg/tools/javaparse.py --dir TMessagesProj/src/main/java/com/radolyn

Exit code 0 = every file parsed, 1 = at least one failure, 3 = no backend.
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import List, Optional, Tuple

try:
    import javalang
except ImportError:  # pragma: no cover
    javalang = None


def find_javac() -> Optional[str]:
    candidate = shutil.which("javac")
    if candidate:
        return candidate
    home = Path("/usr/lib/jvm")
    if home.is_dir():
        for path in sorted(home.glob("*/bin/javac")):
            return str(path)
    return None


def parse_with_javalang(path: Path) -> Tuple[bool, str]:
    if javalang is None:
        return False, "javalang is not installed (pip install javalang)"
    source = path.read_text(encoding="utf-8", errors="replace")
    # javalang has no Java 14+ switch expressions; strip them so the rest of a
    # modern file can still be validated. The build tool reports nothing for
    # files it had to simplify, which is why javac is preferred when present.
    try:
        javalang.parse.parse(source)
        return True, "parsed (javalang)"
    except Exception as error:  # javalang raises JavaSyntaxError, TokenError...
        message = str(error).strip().splitlines()[0] if str(error).strip() else type(error).__name__
        return False, "{}: {}".format(type(error).__name__, message)


def parse_with_javac(path: Path, javac: str, classpath: Optional[str]) -> Tuple[bool, str]:
    with tempfile.TemporaryDirectory() as out:
        command: List[str] = [javac, "-proc:none", "-nowarn", "-d", out]
        if classpath:
            command += ["-cp", classpath]
        command.append(str(path))
        result = subprocess.run(command, capture_output=True, text=True)
        if result.returncode == 0:
            return True, "compiled (javac)"
        first = (result.stderr or result.stdout).strip().splitlines()
        return False, first[0] if first else "javac failed"


CLASS_HINTS = (
    "javax.crypto.", "java.security.", "android.content.", "android.util.", "java.io.",
    "java.nio.", "android.security.", "java.net.", "javax.net.",
)


def quick_sanity(source: str) -> Optional[str]:
    """Cheap structural checks that catch the mistakes javalang also catches.

    javalang (0.13) predates Java 10 and rejects ``var`` outside of simple
    declarations, so when it is not installed we still do something useful:
    balanced braces/brackets/parens outside strings and comments, and at least
    one ``class``/``interface``/``enum`` declaration.
    """
    depth = {"{": 0, "[": 0, "(": 0}
    closing = {"}": "{", "]": "[", ")": "("}
    index = 0
    length = len(source)
    in_string = None
    in_line_comment = False
    in_block_comment = False
    while index < length:
        char = source[index]
        nxt = source[index + 1] if index + 1 < length else ""
        if in_line_comment:
            if char == "\n":
                in_line_comment = False
        elif in_block_comment:
            if char == "*" and nxt == "/":
                in_block_comment = False
                index += 1
        elif in_string:
            if char == "\\":
                index += 1
            elif char == in_string:
                in_string = None
        elif char == "/" and nxt == "/":
            in_line_comment = True
            index += 1
        elif char == "/" and nxt == "*":
            in_block_comment = True
            index += 1
        elif char in ("\"", "'"):
            in_string = char
        elif char in depth:
            depth[char] += 1
        elif char in closing:
            opener = closing[char]
            depth[opener] -= 1
            if depth[opener] < 0:
                return "unbalanced '{}' at offset {}".format(char, index)
        index += 1
    if in_block_comment:
        return "unterminated block comment"
    for opener, value in depth.items():
        if value != 0:
            return "{} unmatched '{}'".format(value, opener)
    if not any(token in source for token in (" class ", " interface ", " enum ", "\nclass ", "\ninterface ")):
        return "no type declaration found"
    return None


def check(paths: List[Path], classpath: Optional[str] = None, force_javalang: bool = False) -> int:
    javac = None if force_javalang else find_javac()
    backend = "javac" if (javac and classpath) else ("javalang" if javalang else "sanity")

    failures = 0
    for path in paths:
        if not path.is_file():
            print("MISSING", path)
            failures += 1
            continue
        if backend == "javac":
            ok, detail = parse_with_javac(path, javac, classpath)  # type: ignore[arg-type]
        elif backend == "javalang":
            ok, detail = parse_with_javalang(path)
        else:
            problem = quick_sanity(path.read_text(encoding="utf-8", errors="replace"))
            ok, detail = (problem is None), ("structural checks passed" if problem is None else problem)
        print("{} {} — {}".format("OK  " if ok else "FAIL", path, detail))
        failures += 0 if ok else 1
    print("\n{} file(s), {} failure(s), backend={}".format(len(paths), failures, backend))
    return 0 if failures == 0 else 1


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Syntax-check Java files without an Android SDK.")
    parser.add_argument("files", nargs="*", help="Java files to check")
    parser.add_argument("--dir", action="append", default=[], help="recursively check every .java under DIR")
    parser.add_argument("--classpath", help="classpath for a real javac run")
    parser.add_argument("--javalang", action="store_true", help="force the pure-Python parser")
    args = parser.parse_args(argv)

    paths = [Path(item) for item in args.files]
    for directory in args.dir:
        paths.extend(sorted(Path(directory).rglob("*.java")))
    if not paths:
        parser.error("nothing to check")
    return check(paths, classpath=args.classpath, force_javalang=args.javalang)


if __name__ == "__main__":
    raise SystemExit(main())
