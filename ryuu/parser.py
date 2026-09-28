"""Разбор RYU (Ryuu Notation).

Документ — последовательность строк. Каждая значащая строка — одна
инструкция: необязательные подъёмы ``'`` и одна руна с полезной
нагрузкой. Разбороднозначный, однопроходный, O(строк).

Стек открытых узлов:
    * ``.`` — «гнездо» (именованное поле карты);
    * ``+`` — «элемент» (безымянный элемент списка);
    * список — создаётся неявно первым ``+`` внутри гнезда/элемента.

Правила привязки атомов:
    * верх стека — гнездо или элемент: атом становится значением узла,
      узел закрывается (атомы «запечатывают» своё гнездо);
    * верх стека — список: атом дописывается элементом, список остаётся
      открытым («голодный» список);
    * пустой стек: атом задаёт скалярный корень документа.

Правила закрытия:
    * ``'`` закрывает один открытый узел (можно ``''``, ``'''``...);
    * гнездо, закрытое без детей, — пустая карта ``{}``;
    * элемент, закрытый без детей, выбрасывается (строительные леса);
      ``null``-элемент пишется как ``+ !``;
    * конец файла закрывает всё автоматически.
"""

import base64
import math
import re
from datetime import date, datetime, time

from .errors import RyuuParseError
from .values import Link

__all__ = ["loads"]

_UNSET = object()

# Все руны формата (первый значащий символ строки).
RUNES = "'.,~#?%&!$|;+"
_ATOM_RUNES = "~#?%&!$|"

_BOOL_TRUE = {"yes", "true", "on", "1"}
_BOOL_FALSE = {"no", "false", "off", "0"}

_INT_RE = re.compile(
    r"^[+-]?(0[xX][0-9a-fA-F_]+|0[bB][01_]+|0[oO][0-7_]+|\d[\d_]*)$"
)
_FLOAT_RE = re.compile(
    r"^[+-]?((\d[\d_]*)?\.\d[\d_]*([eE][+-]?\d+)?|\d[\d_]*[eE][+-]?\d+)$"
)
_DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
_TIME_RE = re.compile(r"^\d{2}:\d{2}(:\d{2}(\.\d+)?)?$")


class _Node:
    __slots__ = ("kind", "name", "atom", "fields", "items", "drop")

    def __init__(self, kind, name=None):
        self.kind = kind          # "field" | "item" | "list"
        self.name = name          # имя поля (только для kind == "field")
        self.atom = _UNSET        # атомарное значение узла
        self.fields = {}          # дети-поля (режим карты)
        self.items = None         # дети-элементы (режим списка) — сам список
        self.drop = False         # выбросить пустой элемент


def _skip_one_space(s):
    """Пропустить ровно один пробел/таб после руны (правило одного пробела)."""
    if s[:1] in (" ", "\t"):
        return s[1:]
    return s


class _Parser:
    def __init__(self, text):
        self.text = text
        self.lines = text.split("\n")
        self.stack = []           # открытые узлы
        self.root = _UNSET        # корневой узел-контейнер ИЛИ скаляр
        self.root_container = None
        self.closed = False       # корень уже закрыт (дальше данных быть не может)

    # ---- утилиты -------------------------------------------------------

    def err(self, msg, line=None):
        raise RyuuParseError(msg, line if line is not None else self.lineno)

    @property
    def top(self):
        return self.stack[-1] if self.stack else None

    # ---- атомы ---------------------------------------------------------

    def _number(self, tok, line):
        t = tok.strip()
        if t in ("inf", "+inf"):
            return math.inf
        if t == "-inf":
            return -math.inf
        if t == "nan":
            return math.nan
        if _INT_RE.match(t):
            body = t.replace("_", "")
            neg = body.startswith("-")
            if body[0] in "+-":
                body = body[1:]
            if body[:2] in ("0x", "0X"):
                val = int(body, 16)
            elif body[:2] in ("0b", "0B"):
                val = int(body, 2)
            elif body[:2] in ("0o", "0O"):
                val = int(body, 8)
            else:
                val = int(body, 10)
            return -val if neg else val
        if _FLOAT_RE.match(t):
            return float(t.replace("_", ""))
        self.err(f"bad number: {tok!r}", line)

    def _boolean(self, tok, line):
        t = tok.strip().lower()
        if t in _BOOL_TRUE:
            return True
        if t in _BOOL_FALSE:
            return False
        self.err(f"bad boolean: {tok!r} (yes/no/true/false/on/off/1/0)", line)

    def _temporal(self, tok, line):
        t = tok.strip()
        if _DATE_RE.match(t):
            try:
                return date.fromisoformat(t)
            except ValueError:
                self.err(f"bad date/time: {tok!r} (expected ISO-8601)", line)
        if _TIME_RE.match(t):
            try:
                return time.fromisoformat(t)
            except ValueError:
                self.err(f"bad date/time: {tok!r} (expected ISO-8601)", line)
        s = t[:-1] + "+00:00" if t.endswith(("Z", "z")) else t
        try:
            return datetime.fromisoformat(s)
        except ValueError:
            self.err(f"bad date/time: {tok!r} (expected ISO-8601)", line)

    def _bytes(self, tok, line):
        t = "".join(tok.split())
        try:
            return base64.b64decode(t, validate=True)
        except Exception:
            self.err(f"bad base64 payload: {tok!r}", line)

    def _hollow(self, tok, line):
        t = tok.strip().lower()
        if t == "map":
            return {}
        if t == "list":
            return []
        self.err(f"bad hollow form: {tok!r} (expected 'map' or 'list')", line)

    def parse_atom(self, rune, payload, line):
        """Разобрать атом по руне. payload — строка после руны."""
        if rune == "~":
            return _skip_one_space(payload)
        if rune == "#":
            return self._number(payload, line)
        if rune == "?":
            return self._boolean(payload, line)
        if rune == "%":
            return self._temporal(payload, line)
        if rune == "&":
            t = payload.strip()
            if not t:
                self.err("empty link", line)
            return Link(t)
        if rune == "$":
            return self._bytes(payload, line)
        if rune == "|":
            return self._hollow(payload, line)
        if rune == "!":
            if payload.strip():
                self.err("void '!' takes no payload", line)
            return None
        self.err(f"unknown atom rune {rune!r}", line)

    # ---- привязка ------------------------------------------------------

    def bind_atom(self, value, line):
        top = self.top
        if top is None:
            if self.root is _UNSET and not self.closed:
                self.root = value
                self.closed = True
                return
            self.err("value outside of document root", line)
        if top.kind == "list":
            top.items.append(value)
        else:
            if top.fields:
                self.err("scalar value in a node that already has fields", line)
            top.atom = value
            self.stack.pop()

    # ---- открытие узлов ------------------------------------------------

    def _ensure_root(self, line):
        if self.stack:
            return
        if self.closed or self.root is not _UNSET:
            self.err("data after document root", line)
        node = _Node("item")      # неявная корневая карта
        self.root = node
        self.root_container = node
        self.stack.append(node)

    @staticmethod
    def _list_mode(node):
        """Узел в «режиме списка» (сам список или держатель списка)."""
        return node.kind == "list" or node.items is not None

    def open_field(self, name, rest, line):
        # '.' не может жить в списке: верхние list-mode узлы закрываются сами
        while self.stack and self._list_mode(self.top):
            self.lift(1, line)
        self._ensure_root(line)
        top = self.top
        if name in top.fields:
            self.err(f"duplicate field {name!r}", line)
        if name[0] in RUNES:
            self.err(f"field name must not start with a rune: {name!r}", line)
        field = _Node("field", name)
        top.fields[name] = field
        rest = rest.lstrip()
        if rest[:1] and rest[0] in _ATOM_RUNES:
            field.atom = self.parse_atom(rest[0], rest[1:], line)
        else:
            if rest.strip():
                self.err(
                    f"unexpected trailing payload after field name: {rest.strip()!r}",
                    line,
                )
            self.stack.append(field)

    def open_item(self, rest, line):
        if not self.stack:
            if self.closed or self.root is not _UNSET:
                self.err("data after document root", line)
            lst = _Node("list")
            lst.items = []
            self.root = lst
            self.root_container = lst
            self.stack.append(lst)
    def _innermost_list_index(self):
        """Индекс самого верхнего списка в стеке или None."""
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i].kind == "list":
                return i
        return None

    def open_item(self, rest, line):
        if not self.stack:
            if self.closed or self.root is not _UNSET:
                self.err("data after document root", line)
            lst = _Node("list")
            lst.items = []
            self.root = lst
            self.root_container = lst
            self.stack.append(lst)
        top = self.top
        if top.kind != "list":
            if top.atom is not _UNSET:
                self.err("internal: sealed node on stack", line)
            if top.fields or top.items is not None:
                # Узел заполнен. '+' означает «следующий элемент внутреннего
                # списка»: закрываем всё до этого списка (авто-сиблинг).
                k = self._innermost_list_index()
                if k is None:
                    if top.items is not None:
                        self.err(
                            "field already holds a list — "
                            "a closed list cannot be reopened",
                            line,
                        )
                    self.err(
                        "field already holds a map — it cannot become a list",
                        line,
                    )
                del self.stack[k + 1:]
                top = self.top
            else:
                # бездетный узел (гнездо или элемент) становится вложенным
                # списком
                lst = _Node("list")
                lst.items = []
                top.items = lst.items
                self.stack.append(lst)
                top = lst
        if top.kind != "list":
            self.err("'+' is only valid inside (or as) a list", line)
        rest = rest.lstrip()
        item = _Node("item")
        if rest[:1] and rest[0] in _ATOM_RUNES:
            item.atom = self.parse_atom(rest[0], rest[1:], line)
            top.items.append(item)
        else:
            if rest.strip():
                self.err(f"unexpected payload after '+': {rest!r}", line)
            top.items.append(item)
            self.stack.append(item)

    # ---- подъёмы -------------------------------------------------------

    def lift(self, count, line):
        for _ in range(count):
            if not self.stack:
                self.err("lift with nothing open", line)
            node = self.stack.pop()
            if node.kind == "item" and (
                node.atom is _UNSET and not node.fields and node.items is None
            ):
                node.drop = True
            if not self.stack and node is self.root_container:
                self.closed = True

    # ---- финализация ---------------------------------------------------

    def _finalize(self, node):
        if node.atom is not _UNSET:
            return node.atom
        if node.items is not None:
            out = []
            for v in node.items:
                if isinstance(v, _Node):
                    if v.drop:
                        continue
                    out.append(self._finalize(v))
                else:
                    out.append(v)
            return out
        if node.fields:
            return {k: self._finalize(v) for k, v in node.fields.items()}
        return {}

    def result(self):
        if self.root is _UNSET:
            return None
        if isinstance(self.root, _Node):
            return self._finalize(self.root)
        return self.root

    # ---- главный цикл ---------------------------------------------------

    def parse(self):
        i = 0
        n = len(self.lines)
        while i < n:
            raw = self.lines[i]
            self.lineno = i + 1
            i += 1

            # блочная строка ~~~ ... ~~~
            if raw.strip() == "~~~":
                content = []
                while True:
                    if i >= n:
                        self.err("unterminated block string (missing closing '~~~')",
                                 self.lineno)
                    inner = self.lines[i]
                    if inner.strip() == "~~~":
                        i += 1
                        break
                    content.append(inner)
                    i += 1
                self.bind_atom("\n".join(content), self.lineno)
                continue

            stripped = raw.lstrip(" \t")
            if not stripped or stripped[0] == ";":
                continue  # пустая строка или комментарий

            j = 0
            while j < len(stripped) and stripped[j] == "'":
                j += 1
            lifts = j
            head = stripped[j:]
            if not head:
                if lifts:
                    self.lift(lifts, self.lineno)
                continue

            rune = head[0]
            # payload для '~' берётся из исходной строки (правило одного
            # пробела), для остальных рун пробелы не значимы
            after = stripped[j + 1:]

            if lifts:
                self.lift(lifts, self.lineno)

            if rune == ".":
                after_l = after.lstrip(" \t")
                if not after_l:
                    self.err("field without a name", self.lineno)
                name = after_l.split()[0]
                name_end = after_l.find(name) + len(name)
                rest = after_l[name_end:]
                self.open_field(name, rest, self.lineno)
            elif rune == "+":
                self.open_item(after, self.lineno)
            elif rune in _ATOM_RUNES:
                self.bind_atom(self.parse_atom(rune, after, self.lineno),
                               self.lineno)
            else:
                self.err(f"unknown rune {rune!r}", self.lineno)

        return self.result()


def loads(text):
    """Разобрать документ RYU и вернуть дерево Python.

    Карты -> dict, списки -> list, атомы -> str/int/float/bool/None/
    datetime/date/time/bytes/Link.
    """
    if isinstance(text, bytes):
        text = text.decode("utf-8")
    if text.startswith("\ufeff"):
        text = text[1:]
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    return _Parser(text).parse()
