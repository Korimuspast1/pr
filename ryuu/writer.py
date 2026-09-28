"""Каноническая запись дерева в RYU (Ryuu Notation).

Писатель зеркалит стек парсера и пользуется его «умными» правилами
закрытия, поэтому подъёмы ``'`` появляются только там, где они
действительно нужны:

    * перед ``.`` — никогда, если выше целевой карты только узлы
      в режиме списка (парсер закрывает их сам);
    * перед ``+`` — один подъём опускается: заполненный элемент-карту
      парсер закрывает сам (авто-сиблинг).

Канонические решения:
    * скаляры и пустые формы пишутся в одну строку с именем поля
      (``. age # 30``) или элемента (``+ ~ text``);
    * контейнеры — отдельной строкой с детьми ниже;
    * строки с переводами строк — блоком ``~~~``;
    * булевы значения — ``yes``/``no``.
"""

import base64
import math
from datetime import date, datetime, time

from .errors import RyuuWriteError
from .parser import RUNES
from .values import Link

__all__ = ["dumps"]

_BLOCK = object()  # маркер «строка-блок»

# Режимы узлов в стеке писателя (зеркало стека парсера):
#   "map"   — узел в режиме карты (корневая карта, поле-карта, элемент-карта)
#   "lhold" — держатель списка (поле или элемент, ставшие списком)
#   "list"  — сам список
_LIST_MODE = ("lhold", "list")


def _fmt_number(v):
    if isinstance(v, int):
        return str(v)
    if math.isnan(v):
        return "nan"
    if math.isinf(v):
        return "inf" if v > 0 else "-inf"
    s = repr(v)
    if "." not in s and "e" not in s and "E" not in s:
        s += ".0"
    return s


def _atom_payload(v):
    """Вернуть '<руна> <нагрузка>' для скаляра, либо _BLOCK."""
    if v is None:
        return "!"
    if isinstance(v, bool):
        return "? yes" if v else "? no"
    if isinstance(v, (int, float)):
        return f"# {_fmt_number(v)}"
    if isinstance(v, str):
        if isinstance(v, Link):
            return f"& {v}"
        if "\n" in v:
            return _BLOCK
        return "~ " + v if v else "~"
    if isinstance(v, (datetime, date, time)):
        return f"% {v.isoformat()}"
    if isinstance(v, (bytes, bytearray)):
        return f"$ {base64.b64encode(bytes(v)).decode('ascii')}"
    raise RyuuWriteError(
        f"cannot represent {type(v).__name__} value in RYU: {v!r}"
    )


def _check_name(name):
    if not isinstance(name, str) or not name:
        raise RyuuWriteError(f"invalid field name: {name!r} (must be non-empty str)")
    if any(c.isspace() for c in name):
        raise RyuuWriteError(
            f"invalid field name {name!r}: whitespace is not allowed "
            "(write it as a single token, e.g. release_date)"
        )
    if name[0] in RUNES:
        raise RyuuWriteError(
            f"invalid field name {name!r}: must not start with a rune {name[0]!r}"
        )


class _Writer:
    def __init__(self):
        self.out = []
        self.stack = []  # режимы открытых узлов, зеркало стека парсера

    def emit(self, line):
        self.out.append(line)

    # --- подъёмы ----------------------------------------------------------

    def _close_for_field(self, target):
        """Закрыть узлы выше target перед строкой '. name'.

        Если все узлы выше целевой карты — в режиме списка, подъёмы не
        нужны: руну '.' парсер закрывает их сам.
        """
        i = len(self.stack) - 1
        while i > target and self.stack[i] in _LIST_MODE:
            i -= 1
        if i == target:
            del self.stack[target + 1:]
            return
        k = len(self.stack) - target - 1  # все узлы выше целевой карты
        self.emit("'" * k)
        del self.stack[target + 1:]

    def _close_for_item(self, target):
        """Закрыть узлы выше target перед строкой '+'.

        Руна '+' сама закрывает всё до внутреннего списка (авто-сиблинг),
        поэтому подъёмы нужны лишь для того, чтобы внутренним списком
        оказался target: над ним не должно остаться других списков.
        Поднимаемся до самого нижнего списка над target (включительно).
        """
        j = None
        for i in range(target + 1, len(self.stack)):
            if self.stack[i] == "list":
                j = i
                break
        if j is not None:
            self.emit("'" * (len(self.stack) - j))
        del self.stack[target + 1:]

    # --- блок-строка -------------------------------------------------------

    def block(self, prefix, s):
        """prefix — '' (корень), '. name' или '+'."""
        if any(part.strip() == "~~~" for part in s.split("\n")):
            raise RyuuWriteError(
                "string contains a '~~~' line — it would terminate the block"
            )
        if prefix:
            self.emit(prefix)
        self.emit("~~~")
        self.out.extend(s.split("\n"))
        self.emit("~~~")

    # --- поля ----------------------------------------------------------------

    def field(self, name, v):
        _check_name(name)
        if isinstance(v, dict):
            if not v:
                self.emit(f". {name} | map")
                return
            self.emit(f". {name}")
            self.stack.append("map")
            self.map_fields(v)
            return
        if isinstance(v, list):
            if not v:
                self.emit(f". {name} | list")
                return
            self.emit(f". {name}")
            self.stack.append("lhold")
            self.items(v)
            return
        ap = _atom_payload(v)
        if ap is _BLOCK:
            self.block(f". {name}", v)
        else:
            self.emit(f". {name} {ap}")

    def map_fields(self, m):
        target = len(self.stack) - 1  # индекс карты-владельца
        for k, v in m.items():
            self._close_for_field(target)
            self.field(k, v)

    # --- элементы --------------------------------------------------------------

    def items(self, lst):
        self.stack.append("list")
        target = len(self.stack) - 1  # индекс списка-владельца
        for v in lst:
            self._close_for_item(target)
            if isinstance(v, dict):
                if not v:
                    self.emit("+ | map")
                    continue
                self.emit("+")
                self.stack.append("map")
                self.map_fields(v)
                continue
            if isinstance(v, list):
                if not v:
                    self.emit("+ | list")
                    continue
                self.emit("+")
                self.stack.append("lhold")
                self.items(v)
                continue
            ap = _atom_payload(v)
            if ap is _BLOCK:
                self.block("+", v)
            else:
                self.emit(f"+ {ap}")

    # --- корень ------------------------------------------------------------------

    def root(self, obj):
        if isinstance(obj, dict):
            if not obj:
                self.emit("| map")
                return
            self.stack.append("map")
            self.map_fields(obj)
            return
        if isinstance(obj, list):
            if not obj:
                self.emit("| list")
                return
            self.items(obj)
            return
        ap = _atom_payload(obj)
        if ap is _BLOCK:
            self.block("", obj)
        else:
            self.emit(ap)


def dumps(obj):
    """Записать дерево Python в канонический RYU."""
    w = _Writer()
    w.root(obj)
    text = "\n".join(w.out)
    return text + "\n" if text else ""
