"""CLI: конвертация JSON <-> RYU и проверка файлов.

    python -m ryuu encode  data.json [data.ryu]
    python -m ryuu decode  data.ryu [data.json]
    python -m ryuu check   data.ryu
"""

import argparse
import base64
import json
import sys
from datetime import date, datetime, time

import ryuu
from ryuu.values import Link


def _to_jsonable(v):
    """Привести дерево RYU к JSON-совместимым типам."""
    if isinstance(v, dict):
        return {k: _to_jsonable(x) for k, x in v.items()}
    if isinstance(v, list):
        return [_to_jsonable(x) for x in v]
    if isinstance(v, (datetime, date, time)):
        return v.isoformat()
    if isinstance(v, (bytes, bytearray)):
        return base64.b64encode(bytes(v)).decode("ascii")
    if isinstance(v, Link):
        return str(v)
    if isinstance(v, float):
        if v != v or v in (float("inf"), float("-inf")):
            return repr(v)
    return v


def _count_nodes(v):
    maps = lists = atoms = 0

    def walk(x):
        nonlocal maps, lists, atoms
        if isinstance(x, dict):
            maps += 1
            for c in x.values():
                walk(c)
        elif isinstance(x, list):
            lists += 1
            for c in x:
                walk(c)
        else:
            atoms += 1

    walk(v)
    return maps, lists, atoms


def _read(path):
    if path == "-":
        return sys.stdin.read()
    with open(path, "r", encoding="utf-8") as fp:
        return fp.read()


def _write(path, text):
    if path == "-":
        sys.stdout.write(text)
    else:
        with open(path, "w", encoding="utf-8", newline="\n") as fp:
            fp.write(text)


def main(argv=None):
    p = argparse.ArgumentParser(
        prog="ryuu",
        description="RYU (Ryuu Notation) — конвертер и валидатор формата",
    )
    sub = p.add_subparsers(dest="cmd", required=True)

    pe = sub.add_parser("encode", help="JSON -> RYU")
    pe.add_argument("input", help="входной JSON-файл ('-' = stdin)")
    pe.add_argument("output", nargs="?", default="-", help="выходной .ryu ('-' = stdout)")

    pd = sub.add_parser("decode", help="RYU -> JSON")
    pd.add_argument("input", help="входной .ryu-файл ('-' = stdin)")
    pd.add_argument("output", nargs="?", default="-", help="выходной JSON ('-' = stdout)")

    pc = sub.add_parser("check", help="проверить синтаксис RYU")
    pc.add_argument("input", help="входной .ryu-файл ('-' = stdin)")

    a = p.parse_args(argv)

    if a.cmd == "encode":
        try:
            data = json.loads(_read(a.input))
        except json.JSONDecodeError as e:
            print(f"ryuu: invalid JSON input: {e}", file=sys.stderr)
            return 1
        try:
            _write(a.output, ryuu.dumps(data))
        except ryuu.RyuuWriteError as e:
            print(f"ryuu: {e}", file=sys.stderr)
            return 1
        return 0

    if a.cmd == "decode":
        try:
            tree = ryuu.loads(_read(a.input))
        except ryuu.RyuuParseError as e:
            print(f"ryuu: {e}", file=sys.stderr)
            return 1
        _write(a.output, json.dumps(_to_jsonable(tree), indent=2, ensure_ascii=False))
        return 0

    if a.cmd == "check":
        try:
            tree = ryuu.loads(_read(a.input))
        except ryuu.RyuuParseError as e:
            print(f"ryuu: {e}", file=sys.stderr)
            return 1
        maps, lists, atoms = _count_nodes(tree)
        kind = (
            "map" if isinstance(tree, dict)
            else "list" if isinstance(tree, list)
            else "scalar"
        )
        print(f"OK: root={kind}, maps={maps}, lists={lists}, atoms={atoms}")
        return 0

    return 2


if __name__ == "__main__":
    sys.exit(main())
