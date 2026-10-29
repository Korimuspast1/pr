#!/usr/bin/env python3
"""Смайлики (SmileLang) — маленький язык программирования на эмодзи.

Идея простая: каждая конструкция обычного языка заменена смайликом.

    📦 имя ⬅ "Мир"          # let имя = "Мир"
    🗣 "Привет, " ➕ имя      # print
    🔂 3                    # повторить 3 раза
        🗣 "😊"
    🔚

ASCII-синонимы (let, print, if, ...) поддерживаются, чтобы язык было
удобно набирать на клавиатуре, но родной синтаксис — смайлики.

Запуск:
    python3 smileylang/smiley.py run пример.smile
    python3 smileylang/smiley.py -c '🗣 "Привет 😊"'
    python3 smileylang/smiley.py repl
"""

from __future__ import annotations

import argparse
import random as _random
import sys
import time as _time
from dataclasses import dataclass
from typing import Any, Callable, Dict, List, Optional, Sequence, Set, Tuple

__all__ = [
    "SmileyError",
    "Lexer",
    "Parser",
    "Interpreter",
    "Environment",
    "execute",
    "DEFAULT_MAX_STEPS",
]

DEFAULT_MAX_STEPS = 2_000_000
DEFAULT_MAX_OUTPUT_LINES = 100_000

# Невидимые селекторы вариации: "❤️" (U+2764 U+FE0F) и "❤" (U+2764) — одно и то же.
_VARIATION_SELECTORS = "\ufe0f\ufe0e"


# --------------------------------------------------------------------------
# Ошибки
# --------------------------------------------------------------------------


class SmileyError(Exception):
    """Ошибка языка: сообщение по-русски и, если известно, номер строки."""

    def __init__(self, message: str, line: Optional[int] = None):
        self.message = message
        self.line = line
        super().__init__(str(self))

    def __str__(self) -> str:
        if self.line is not None:
            return f"😵 Строка {self.line}: {self.message}"
        return f"😵 {self.message}"


# --------------------------------------------------------------------------
# Лексер
# --------------------------------------------------------------------------

# Порядок важен: длинные последовательности проверяются первыми.
EMOJI_TOKENS: Tuple[Tuple[str, str], ...] = (
    ("⬆🟰", "GE"),
    ("⬇🟰", "LE"),
    ("📦", "LET"),
    ("🗣", "PRINT"),
    ("❓", "IF"),
    ("🤔", "THEN"),
    ("🙃", "ELSE"),
    ("🔚", "END"),
    ("🔁", "WHILE"),
    ("🔂", "REPEAT"),
    ("🎁", "FUNC"),
    ("↩", "RETURN"),
    ("💔", "BREAK"),
    ("⏭", "CONTINUE"),
    ("✅", "TRUE"),
    ("❌", "FALSE"),
    ("🟰", "EQ"),
    ("🚫", "NE"),
    ("⬆", "GT"),
    ("⬇", "LT"),
    ("🤝", "AND"),
    ("🔀", "OR"),
    ("🙅", "NOT"),
    ("➕", "PLUS"),
    ("➖", "MINUS"),
    ("✖", "STAR"),
    ("➗", "SLASH"),
    ("📐", "MOD"),
    ("⬅", "ASSIGN"),
    ("🙈", "COMMENT"),
)

KEYWORD_TOKENS: Dict[str, str] = {
    # ASCII-синонимы
    "let": "LET",
    "print": "PRINT",
    "if": "IF",
    "then": "THEN",
    "else": "ELSE",
    "end": "END",
    "while": "WHILE",
    "repeat": "REPEAT",
    "func": "FUNC",
    "fun": "FUNC",
    "return": "RETURN",
    "break": "BREAK",
    "continue": "CONTINUE",
    "true": "TRUE",
    "false": "FALSE",
    "and": "AND",
    "or": "OR",
    "not": "NOT",
    "mod": "MOD",
    # русские синонимы
    "пусть": "LET",
    "сказать": "PRINT",
    "если": "IF",
    "то": "THEN",
    "иначе": "ELSE",
    "конец": "END",
    "пока": "WHILE",
    "повтори": "REPEAT",
    "функция": "FUNC",
    "вернуть": "RETURN",
    "стоп": "BREAK",
    "дальше": "CONTINUE",
    "истина": "TRUE",
    "ложь": "FALSE",
    "и": "AND",
    "или": "OR",
    "не": "NOT",
}

SYMBOL_TOKENS: Dict[str, str] = {
    "(": "LPAREN",
    ")": "RPAREN",
    ",": "COMMA",
    ";": "NEWLINE",
    "+": "PLUS",
    "-": "MINUS",
    "*": "STAR",
    "/": "SLASH",
    "%": "MOD",
}

TWO_CHAR_SYMBOLS: Dict[str, str] = {
    "==": "EQ",
    "!=": "NE",
    ">=": "GE",
    "<=": "LE",
    "//": "COMMENT",
}

ONE_CHAR_SYMBOLS: Dict[str, str] = {
    ">": "GT",
    "<": "LT",
    "=": "ASSIGN",
}


@dataclass(frozen=True)
class Token:
    kind: str
    value: Any
    line: int
    text: str = ""

    def __repr__(self) -> str:  # pragma: no cover - отладочный вывод
        return f"Token({self.kind!r}, {self.value!r}, строка {self.line})"


class Lexer:
    """Превращает текст программы в поток токенов."""

    def __init__(self, source: str):
        self.source = source
        self.pos = 0
        self.line = 1
        self.tokens: List[Token] = []

    # -- вспомогательное ---------------------------------------------------

    def _peek(self, offset: int = 0) -> str:
        index = self.pos + offset
        return self.source[index] if index < len(self.source) else ""

    def _match_emoji(self, key: str) -> Optional[int]:
        """Совпадение эмодзи-ключа, если в тексте есть селекторы вариации."""

        source = self.source
        index = self.pos
        length = len(source)
        for char in key:
            if index >= length or source[index] != char:
                return None
            index += 1
            while index < length and source[index] in _VARIATION_SELECTORS:
                index += 1
        return index

    def _add(self, kind: str, value: Any, line: int, text: str) -> None:
        self.tokens.append(Token(kind, value, line, text))

    # -- основной цикл -----------------------------------------------------

    def tokenize(self) -> List[Token]:
        source = self.source
        while self.pos < len(source):
            char = source[self.pos]

            if char in " \t\r":
                self.pos += 1
                continue

            if char == "\n":
                self._add("NEWLINE", "\n", self.line, "\n")
                self.line += 1
                self.pos += 1
                continue

            # комментарии: 🙈 или # или //
            if char == "#" or char == "🙈":
                self._skip_comment()
                continue
            if char == "/" and self._peek(1) == "/":
                self._skip_comment()
                continue

            # строки
            if char == '"' or char == "«":
                self._read_string()
                continue

            # числа
            if char.isdigit():
                self._read_number()
                continue

            # эмодзи-ключевые слова
            matched = self._read_emoji()
            if matched:
                continue

            # ASCII-операторы из двух символов
            pair = source[self.pos : self.pos + 2]
            if pair in TWO_CHAR_SYMBOLS:
                self._add(TWO_CHAR_SYMBOLS[pair], pair, self.line, pair)
                self.pos += 2
                continue

            if char in ONE_CHAR_SYMBOLS:
                self._add(ONE_CHAR_SYMBOLS[char], char, self.line, char)
                self.pos += 1
                continue

            if char in SYMBOL_TOKENS:
                self._add(SYMBOL_TOKENS[char], char, self.line, char)
                self.pos += 1
                continue

            # имена (буквы любых алфавитов, цифры, подчёркивание)
            if char.isalpha() or char == "_":
                self._read_name()
                continue

            raise SmileyError(f"не понимаю символ {char!r}", self.line)

        self._add("EOF", None, self.line, "")
        return self.tokens

    # -- части -------------------------------------------------------------

    def _skip_comment(self) -> None:
        source = self.source
        while self.pos < len(source) and source[self.pos] != "\n":
            self.pos += 1

    def _read_string(self) -> None:
        source = self.source
        opener = source[self.pos]
        closer = "»" if opener == "«" else '"'
        line = self.line
        self.pos += 1
        chars: List[str] = []
        while True:
            if self.pos >= len(source):
                raise SmileyError("строка не закрыта (не хватает кавычки)", line)
            char = source[self.pos]
            if char == closer:
                self.pos += 1
                break
            if char == "\n":
                raise SmileyError("строка не может переноситься на новую строку", line)
            if char == "\\":
                self.pos += 1
                escape = self._peek()
                mapping = {"n": "\n", "t": "\t", "\\": "\\", '"': '"', "«": "«", "»": "»"}
                if escape in mapping:
                    chars.append(mapping[escape])
                    self.pos += 1
                    continue
                if escape == "u":
                    code = source[self.pos + 1 : self.pos + 5]
                    try:
                        chars.append(chr(int(code, 16)))
                    except ValueError:
                        raise SmileyError(f"плохой \\u-код: {code!r}", line) from None
                    self.pos += 5
                    continue
                raise SmileyError(f"неизвестная escape-последовательность \\{escape}", line)
            chars.append(char)
            self.pos += 1
        self._add("STR", "".join(chars), line, "строка")

    def _read_number(self) -> None:
        source = self.source
        start = self.pos
        while self.pos < len(source) and (source[self.pos].isdigit() or source[self.pos] == "_"):
            self.pos += 1
        is_float = False
        if (
            self.pos + 1 < len(source)
            and source[self.pos] == "."
            and source[self.pos + 1].isdigit()
        ):
            is_float = True
            self.pos += 1
            while self.pos < len(source) and (source[self.pos].isdigit() or source[self.pos] == "_"):
                self.pos += 1
        raw = source[start : self.pos].replace("_", "")
        value: Any = float(raw) if is_float else int(raw)
        self._add("NUM", value, self.line, raw)

    def _read_emoji(self) -> bool:
        for key, kind in EMOJI_TOKENS:
            end = self._match_emoji(key)
            if end is None:
                continue
            raw = self.source[self.pos : end]
            if kind == "COMMENT":
                self.pos = end
                self._skip_comment()
                return True
            self._add(kind, raw, self.line, raw)
            self.pos = end
            return True
        return False

    def _read_name(self) -> None:
        source = self.source
        start = self.pos
        while self.pos < len(source) and (source[self.pos].isalnum() or source[self.pos] == "_"):
            self.pos += 1
        name = source[start : self.pos]
        lowered = name.lower()
        kind = KEYWORD_TOKENS.get(lowered)
        if kind is not None:
            self._add(kind, lowered, self.line, name)
        else:
            self._add("NAME", name, self.line, name)


# --------------------------------------------------------------------------
# Парсер
# --------------------------------------------------------------------------

# Узлы дерева — обычные кортежи: (тег, ...)


class Parser:
    def __init__(self, tokens: Sequence[Token]):
        self.tokens = list(tokens)
        self.pos = 0

    # -- вспомогательное ---------------------------------------------------

    def peek(self, offset: int = 0) -> Token:
        index = min(self.pos + offset, len(self.tokens) - 1)
        return self.tokens[index]

    def at(self, *kinds: str) -> bool:
        return self.peek().kind in kinds

    def advance(self) -> Token:
        token = self.peek()
        if token.kind != "EOF":
            self.pos += 1
        return token

    def match(self, *kinds: str) -> bool:
        if self.at(*kinds):
            self.advance()
            return True
        return False

    def expect(self, kind: str, human: str) -> Token:
        if self.at(kind):
            return self.advance()
        raise SmileyError(
            f"ожидалось {human}, а тут {describe(self.peek())}", self.peek().line
        )

    def skip_newlines(self) -> None:
        while self.at("NEWLINE"):
            self.advance()

    # -- программа ---------------------------------------------------------

    def parse_program(self) -> List[Tuple]:
        body, _ = self.parse_block(set())
        return body

    def parse_block(self, stop: Set[str]) -> Tuple[List[Tuple], Optional[str]]:
        body: List[Tuple] = []
        while True:
            self.skip_newlines()
            if self.at(*stop) or self.at("EOF"):
                return body, self.peek().kind
            body.append(self.parse_statement())
            if self.at(*stop) or self.at("EOF"):
                return body, self.peek().kind
            if not self.match("NEWLINE"):
                raise SmileyError(
                    f"лишнее после команды: {describe(self.peek())}", self.peek().line
                )

    def parse_statement(self) -> Tuple:
        token = self.peek()
        kind = token.kind

        if kind == "LET":
            self.advance()
            name = self.expect("NAME", "имя переменной после 📦")
            self.expect("ASSIGN", "⬅ (присваивание)")
            value = self.parse_expression()
            return ("let", name.value, value, token.line)

        if kind == "PRINT":
            self.advance()
            values = [self.parse_expression()]
            while self.match("COMMA"):
                values.append(self.parse_expression())
            return ("print", values, token.line)

        if kind == "IF":
            return self.parse_if()

        if kind == "WHILE":
            self.advance()
            condition = self.parse_expression()
            body, stopped = self.parse_block({"END"})
            if stopped != "END":
                raise SmileyError("цикл 🔁 не закрыт: не хватает 🔚", token.line)
            self.advance()
            return ("while", condition, body, token.line)

        if kind == "REPEAT":
            self.advance()
            count = self.parse_expression()
            body, stopped = self.parse_block({"END"})
            if stopped != "END":
                raise SmileyError("цикл 🔂 не закрыт: не хватает 🔚", token.line)
            self.advance()
            return ("repeat", count, body, token.line)

        if kind == "FUNC":
            self.advance()
            name = self.expect("NAME", "имя функции после 🎁")
            self.expect("LPAREN", "( со списком аргументов")
            params: List[str] = []
            if not self.at("RPAREN"):
                while True:
                    params.append(self.expect("NAME", "имя аргумента").value)
                    if not self.match("COMMA"):
                        break
            self.expect("RPAREN", ")")
            body, stopped = self.parse_block({"END"})
            if stopped != "END":
                raise SmileyError("функция 🎁 не закрыта: не хватает 🔚", token.line)
            self.advance()
            return ("func", name.value, params, body, token.line)

        if kind == "RETURN":
            self.advance()
            if self.at("NEWLINE", "EOF", "END", "ELSE"):
                return ("return", None, token.line)
            return ("return", self.parse_expression(), token.line)

        if kind == "BREAK":
            self.advance()
            return ("break", token.line)

        if kind == "CONTINUE":
            self.advance()
            return ("continue", token.line)

        if kind == "NAME" and self.peek(1).kind == "ASSIGN":
            name = self.advance()
            self.advance()
            value = self.parse_expression()
            return ("assign", name.value, value, token.line)

        expression = self.parse_expression()
        return ("expr", expression, token.line)

    def parse_if(self) -> Tuple:
        token = self.expect("IF", "❓")
        condition = self.parse_expression()
        self.match("THEN")
        then_body, stopped = self.parse_block({"ELSE", "END"})

        else_body: List[Tuple] = []
        if stopped == "ELSE":
            self.advance()
            if self.at("IF"):
                else_body = [self.parse_if()]
            else:
                else_body, stopped = self.parse_block({"END"})
                if stopped != "END":
                    raise SmileyError("🙃 не закрыт: не хватает 🔚", token.line)
                self.advance()
        elif stopped == "END":
            self.advance()
        else:
            raise SmileyError("❓ не закрыт: не хватает 🔚", token.line)

        return ("if", condition, then_body, else_body, token.line)

    # -- выражения ---------------------------------------------------------

    def parse_expression(self) -> Tuple:
        return self.parse_or()

    def parse_or(self) -> Tuple:
        node = self.parse_and()
        while self.at("OR"):
            line = self.advance().line
            node = ("binop", "🔀", node, self.parse_and(), line)
        return node

    def parse_and(self) -> Tuple:
        node = self.parse_not()
        while self.at("AND"):
            line = self.advance().line
            node = ("binop", "🤝", node, self.parse_not(), line)
        return node

    def parse_not(self) -> Tuple:
        if self.at("NOT"):
            line = self.advance().line
            return ("unop", "🙅", self.parse_not(), line)
        return self.parse_comparison()

    def parse_comparison(self) -> Tuple:
        node = self.parse_term()
        operators = {"EQ": "🟰", "NE": "🚫", "GT": "⬆", "LT": "⬇", "GE": "⬆🟰", "LE": "⬇🟰"}
        while self.peek().kind in operators:
            token = self.advance()
            node = ("binop", operators[token.kind], node, self.parse_term(), token.line)
        return node

    def parse_term(self) -> Tuple:
        node = self.parse_factor()
        while self.at("PLUS", "MINUS"):
            token = self.advance()
            symbol = "➕" if token.kind == "PLUS" else "➖"
            node = ("binop", symbol, node, self.parse_factor(), token.line)
        return node

    def parse_factor(self) -> Tuple:
        node = self.parse_unary()
        while self.at("STAR", "SLASH", "MOD"):
            token = self.advance()
            symbol = {"STAR": "✖", "SLASH": "➗", "MOD": "📐"}[token.kind]
            node = ("binop", symbol, node, self.parse_unary(), token.line)
        return node

    def parse_unary(self) -> Tuple:
        if self.at("MINUS"):
            token = self.advance()
            return ("unop", "➖", self.parse_unary(), token.line)
        return self.parse_call()

    def parse_call(self) -> Tuple:
        node = self.parse_primary()
        while self.at("LPAREN"):
            line = self.advance().line
            args: List[Tuple] = []
            if not self.at("RPAREN"):
                while True:
                    args.append(self.parse_expression())
                    if not self.match("COMMA"):
                        break
            self.expect("RPAREN", ")")
            node = ("call", node, args, line)
        return node

    def parse_primary(self) -> Tuple:
        token = self.peek()
        if token.kind == "NUM":
            self.advance()
            return ("num", token.value)
        if token.kind == "STR":
            self.advance()
            return ("str", token.value)
        if token.kind == "TRUE":
            self.advance()
            return ("bool", True)
        if token.kind == "FALSE":
            self.advance()
            return ("bool", False)
        if token.kind == "NAME":
            self.advance()
            return ("var", token.value, token.line)
        if token.kind == "LPAREN":
            self.advance()
            inner = self.parse_expression()
            self.expect("RPAREN", ")")
            return inner
        raise SmileyError(f"ожидалось значение, а тут {describe(token)}", token.line)


def describe(token: Token) -> str:
    """Человеческое описание токена для сообщений об ошибках."""

    names = {
        "NEWLINE": "конец строки",
        "EOF": "конец программы",
        "NAME": "имя",
        "NUM": "число",
        "STR": "строка",
        "ASSIGN": "⬅",
        "LPAREN": "(",
        "RPAREN": ")",
        "COMMA": ",",
        "END": "🔚",
    }
    if token.kind in names:
        return names[token.kind]
    if token.text:
        return token.text
    return token.kind


# --------------------------------------------------------------------------
# Значения и окружения
# --------------------------------------------------------------------------


def to_text(value: Any) -> str:
    """Как значение выглядит при выводе на экран."""

    if value is True:
        return "✅"
    if value is False:
        return "❌"
    if value is None:
        return "🤷"
    if isinstance(value, float):
        if value.is_integer() and abs(value) < 1e16:
            return str(int(value))
        return f"{value:g}"
    return str(value)


def truthy(value: Any) -> bool:
    if value is None or value is False:
        return False
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return value != 0
    if isinstance(value, str):
        return len(value) > 0
    return True


class Environment:
    def __init__(self, parent: Optional["Environment"] = None):
        self.vars: Dict[str, Any] = {}
        self.parent = parent

    def define(self, name: str, value: Any) -> None:
        self.vars[name] = value

    def get(self, name: str, line: Optional[int] = None) -> Any:
        env: Optional[Environment] = self
        while env is not None:
            if name in env.vars:
                return env.vars[name]
            env = env.parent
        raise SmileyError(f"переменная «{name}» не найдена", line)

    def assign(self, name: str, value: Any) -> None:
        env: Optional[Environment] = self
        while env is not None:
            if name in env.vars:
                env.vars[name] = value
                return
            env = env.parent
        self.vars[name] = value

    def has(self, name: str) -> bool:
        env: Optional[Environment] = self
        while env is not None:
            if name in env.vars:
                return True
            env = env.parent
        return False


# --------------------------------------------------------------------------
# Служебные сигналы
# --------------------------------------------------------------------------


class _Return(Exception):
    def __init__(self, value: Any):
        self.value = value


class _Break(Exception):
    pass


class _Continue(Exception):
    pass


class _StepLimit(Exception):
    pass


# --------------------------------------------------------------------------
# Интерпретатор
# --------------------------------------------------------------------------


class Interpreter:
    def __init__(
        self,
        stdin: Optional[Callable[[str], str]] = None,
        out: Optional[Callable[[str], None]] = None,
        max_steps: int = DEFAULT_MAX_STEPS,
        max_output_lines: int = DEFAULT_MAX_OUTPUT_LINES,
        env: Optional[Environment] = None,
        seed: Optional[int] = None,
    ):
        self.output: List[str] = []
        self.max_output_lines = max_output_lines
        self._emit = out
        self.stdin = stdin
        self.max_steps = max_steps
        self.steps = 0
        self.env = env or Environment()
        self.rng = _random.Random(seed)
        self.builtins: Dict[str, Tuple[int, int, str, Callable[..., Any]]] = {}
        self._register_builtins()

    # -- вывод -------------------------------------------------------------

    def emit(self, text: str) -> None:
        if len(self.output) >= self.max_output_lines:
            raise SmileyError(
                f"🗣 слишком много вывода: больше {self.max_output_lines} строк. "
                "Проверь, не крутится ли цикл бесконечно?"
            )
        self.output.append(text)
        if self._emit is not None:
            self._emit(text)

    def tick(self) -> None:
        self.steps += 1
        if self.steps > self.max_steps:
            raise _StepLimit()

    # -- встроенные функции ------------------------------------------------

    def _register_builtins(self) -> None:
        def add(names: Sequence[str], lo: int, hi: int, label: str, fn: Callable[..., Any]) -> None:
            for name in names:
                self.builtins[name] = (lo, hi, label, fn)

        add(
            ("длина", "len"),
            1,
            1,
            "длина(значение)",
            lambda value: len(self._as_text(value)),
        )
        add(("строка", "str"), 1, 1, "строка(значение)", self._as_text)
        add(("число", "int", "num"), 1, 1, "число(значение)", self._as_number)
        add(("случайное", "random"), 2, 2, "случайное(от, до)", self._random_int)
        add(("спросить", "input", "вопрос"), 0, 1, "спросить(вопрос)", self._ask)
        add(("время", "time"), 0, 0, "время()", lambda: int(_time.time()))
        add(("верхний", "upper"), 1, 1, "верхний(текст)", lambda v: self._as_text(v).upper())
        add(("нижний", "lower"), 1, 1, "нижний(текст)", lambda v: self._as_text(v).lower())
        add(("круглое", "round"), 1, 1, "круглое(число)", lambda v: round(self._as_number(v)))

    def _as_text(self, value: Any) -> str:
        if value is True:
            return "✅"
        if value is False:
            return "❌"
        if value is None:
            return ""
        return to_text(value)

    def _as_number(self, value: Any) -> Any:
        if isinstance(value, bool):
            return 1 if value else 0
        if isinstance(value, (int, float)):
            return value
        if isinstance(value, str):
            text = value.strip().replace(",", ".")
            try:
                return int(text)
            except ValueError:
                try:
                    return float(text)
                except ValueError:
                    raise SmileyError(f"не получается превратить «{value}» в число") from None
        raise SmileyError(f"не получается превратить {to_text(value)} в число")

    def _int_arg(self, value: Any, what: str) -> int:
        number = self._as_number(value)
        if isinstance(number, float):
            if not number.is_integer():
                raise SmileyError(f"{what} должно быть целым числом, а не {to_text(value)}")
            number = int(number)
        return number

    def _random_int(self, low: Any, high: Any) -> int:
        start = self._int_arg(low, "«от»")
        end = self._int_arg(high, "«до»")
        if start > end:
            start, end = end, start
        return self.rng.randint(start, end)

    def _ask(self, *args: Any) -> str:
        if self.stdin is None:
            raise SmileyError("здесь нельзя задать вопрос: ввод недоступен")
        prompt = self._as_text(args[0]) if args else ""
        return self.stdin(prompt)

    # -- запуск ------------------------------------------------------------

    def run(self, program: Sequence[Tuple]) -> None:
        try:
            self.exec_block(program, self.env)
        except _Return:
            raise SmileyError("↩ можно использовать только внутри 🎁 функции") from None
        except _Break:
            raise SmileyError("💔 можно использовать только внутри цикла") from None
        except _Continue:
            raise SmileyError("⏭ можно использовать только внутри цикла") from None
        except _StepLimit:
            raise SmileyError(
                "🤯 программа слишком долго работает: превышен лимит шагов "
                f"({self.max_steps}). Похоже на бесконечный цикл."
            ) from None
        except RecursionError:
            raise SmileyError("слишком глубокая рекурсия: функция вызывает себя без конца") from None

    def exec_block(self, body: Sequence[Tuple], env: Environment) -> None:
        for statement in body:
            self.exec_statement(statement, env)

    def exec_statement(self, node: Tuple, env: Environment) -> None:
        self.tick()
        tag = node[0]

        if tag == "let":
            _, name, value_node, _line = node
            env.define(name, self.eval(value_node, env))
            return

        if tag == "assign":
            _, name, value_node, _line = node
            env.assign(name, self.eval(value_node, env))
            return

        if tag == "print":
            _, value_nodes, _line = node
            self.emit(" ".join(to_text(self.eval(value_node, env)) for value_node in value_nodes))
            return

        if tag == "expr":
            self.eval(node[1], env)
            return

        if tag == "if":
            _, condition, then_body, else_body, _line = node
            if truthy(self.eval(condition, env)):
                self.exec_block(then_body, Environment(env))
            elif else_body:
                self.exec_block(else_body, Environment(env))
            return

        if tag == "while":
            _, condition, body, _line = node
            while truthy(self.eval(condition, env)):
                self.tick()
                try:
                    self.exec_block(body, Environment(env))
                except _Break:
                    break
                except _Continue:
                    continue
            return

        if tag == "repeat":
            _, count_node, body, line = node
            count = self._int_arg(self.eval(count_node, env), "число повторов")
            if count < 0:
                raise SmileyError("число повторов не может быть отрицательным", line)
            for _ in range(count):
                self.tick()
                try:
                    self.exec_block(body, Environment(env))
                except _Break:
                    break
                except _Continue:
                    continue
            return

        if tag == "func":
            _, name, params, body, _line = node
            env.define(name, ("function", name, list(params), body, env))
            return

        if tag == "return":
            _, value_node, _line = node
            raise _Return(self.eval(value_node, env) if value_node is not None else None)

        if tag == "break":
            raise _Break()

        if tag == "continue":
            raise _Continue()

        raise SmileyError(f"внутренняя ошибка: неизвестная команда {tag}")

    # -- выражения ---------------------------------------------------------

    def eval(self, node: Tuple, env: Environment) -> Any:
        self.tick()
        tag = node[0]

        if tag == "num" or tag == "str" or tag == "bool":
            return node[1]

        if tag == "var":
            _, name, line = node
            if env.has(name):
                return env.get(name)
            builtin = self.builtins.get(name)
            if builtin is not None:
                return ("builtin", name)
            raise SmileyError(f"переменная «{name}» не найдена", line)

        if tag == "unop":
            _, symbol, operand, line = node
            value = self.eval(operand, env)
            if symbol == "🙅":
                return not truthy(value)
            if symbol == "➖":
                return -self._arith_number(value, "➖", line)
            raise SmileyError(f"неизвестный оператор {symbol}", line)

        if tag == "binop":
            _, symbol, left_node, right_node, line = node
            if symbol == "🤝":
                left = self.eval(left_node, env)
                return self.eval(right_node, env) if truthy(left) else left
            if symbol == "🔀":
                left = self.eval(left_node, env)
                return left if truthy(left) else self.eval(right_node, env)
            left = self.eval(left_node, env)
            right = self.eval(right_node, env)
            return self.binary(symbol, left, right, line)

        if tag == "call":
            _, callee_node, arg_nodes, line = node
            callee = self.eval(callee_node, env)
            args = [self.eval(arg_node, env) for arg_node in arg_nodes]
            return self.call(callee, args, line)

        raise SmileyError(f"внутренняя ошибка: неизвестное выражение {tag}")

    def _arith_number(self, value: Any, symbol: str, line: int) -> Any:
        if isinstance(value, bool):
            return 1 if value else 0
        if isinstance(value, (int, float)):
            return value
        raise SmileyError(f"оператор {symbol} ждёт число, а получил {to_text(value)}", line)

    def binary(self, symbol: str, left: Any, right: Any, line: int) -> Any:
        if symbol == "➕":
            if isinstance(left, str) or isinstance(right, str):
                return self._as_text(left) + self._as_text(right)
            if isinstance(left, list) and isinstance(right, list):
                return left + right
            return self._arith_number(left, symbol, line) + self._arith_number(right, symbol, line)

        if symbol == "✖":
            if isinstance(left, str) and isinstance(right, (int, float)) and not isinstance(right, bool):
                return left * int(right)
            if isinstance(right, str) and isinstance(left, (int, float)) and not isinstance(left, bool):
                return right * int(left)
            return self._arith_number(left, symbol, line) * self._arith_number(right, symbol, line)

        if symbol in ("➖", "➗", "📐"):
            a = self._arith_number(left, symbol, line)
            b = self._arith_number(right, symbol, line)
            if symbol == "➖":
                return a - b
            if b == 0:
                raise SmileyError("деление на ноль — так нельзя 🙃", line)
            if symbol == "📐":
                return a % b
            result = a / b
            if isinstance(a, int) and isinstance(b, int) and result.is_integer():
                return int(result)
            return result

        if symbol == "🟰":
            return values_equal(left, right)
        if symbol == "🚫":
            return not values_equal(left, right)
        if symbol in ("⬆", "⬇", "⬆🟰", "⬇🟰"):
            a, b = self._comparable(left, right, symbol, line)
            if symbol == "⬆":
                return a > b
            if symbol == "⬇":
                return a < b
            if symbol == "⬆🟰":
                return a >= b
            return a <= b

        raise SmileyError(f"неизвестный оператор {symbol}", line)

    def _comparable(self, left: Any, right: Any, symbol: str, line: int) -> Tuple[Any, Any]:
        if isinstance(left, bool):
            left = int(left)
        if isinstance(right, bool):
            right = int(right)
        if isinstance(left, (int, float)) and isinstance(right, (int, float)):
            return left, right
        if isinstance(left, str) and isinstance(right, str):
            return left, right
        raise SmileyError(
            f"оператор {symbol} не умеет сравнивать {to_text(left)} и {to_text(right)}", line
        )

    def call(self, callee: Any, args: Sequence[Any], line: int) -> Any:
        if isinstance(callee, tuple) and callee and callee[0] == "builtin":
            name = callee[1]
            low, high, label, fn = self.builtins[name]
            if not low <= len(args) <= high:
                raise SmileyError(
                    f"функция {label} получила неподходящее число аргументов ({len(args)})", line
                )
            try:
                return fn(*args)
            except SmileyError as error:
                if error.line is None:
                    error.line = line
                raise

        if isinstance(callee, tuple) and callee and callee[0] == "function":
            _, name, params, body, closure = callee
            if len(args) != len(params):
                raise SmileyError(
                    f"функция «{name}» ждёт {_plural(len(params), 'аргумент', 'аргумента', 'аргументов')}"
                    f", а получила {len(args)}",
                    line,
                )
            local = Environment(closure)
            for param, value in zip(params, args):
                local.define(param, value)
            try:
                self.exec_block(body, local)
            except _Return as signal:
                return signal.value
            return None

        if isinstance(callee, str):
            raise SmileyError(f"«{callee}» — это не функция", line)
        raise SmileyError(f"{to_text(callee)} нельзя вызвать как функцию", line)


def _plural(count: int, one: str, few: str, many: str) -> str:
    """«1 аргумент», «2 аргумента», «5 аргументов»."""

    if count % 10 == 1 and count % 100 != 11:
        return f"{count} {one}"
    if 2 <= count % 10 <= 4 and not 12 <= count % 100 <= 14:
        return f"{count} {few}"
    return f"{count} {many}"


def values_equal(left: Any, right: Any) -> bool:
    if isinstance(left, bool) or isinstance(right, bool):
        return left == right
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        return left == right
    if type(left) is not type(right):
        return False
    return left == right


# --------------------------------------------------------------------------
# Удобный API
# --------------------------------------------------------------------------


def execute(
    source: str,
    *,
    stdin: Optional[Callable[[str], str]] = None,
    out: Optional[Callable[[str], None]] = None,
    max_steps: int = DEFAULT_MAX_STEPS,
    max_output_lines: int = DEFAULT_MAX_OUTPUT_LINES,
    env: Optional[Environment] = None,
    seed: Optional[int] = None,
) -> Interpreter:
    """Выполнить программу и вернуть интерпретатор (в .output — все строки)."""

    tokens = Lexer(source).tokenize()
    program = Parser(tokens).parse_program()
    interpreter = Interpreter(
        stdin=stdin,
        out=out,
        max_steps=max_steps,
        max_output_lines=max_output_lines,
        env=env,
        seed=seed,
    )
    interpreter.run(program)
    return interpreter


def run_to_string(source: str, **kwargs: Any) -> str:
    """Выполнить программу и вернуть весь вывод одной строкой."""

    interpreter = execute(source, **kwargs)
    return "\n".join(interpreter.output)


# --------------------------------------------------------------------------
# Командная строка
# --------------------------------------------------------------------------


def _run_file(path: str, max_steps: int) -> int:
    try:
        with open(path, "r", encoding="utf-8") as handle:
            source = handle.read()
    except OSError as error:
        print(f"😵 Не могу открыть файл {path}: {error}", file=sys.stderr)
        return 2

    def ask(prompt: str) -> str:
        try:
            return input(prompt)
        except EOFError:
            raise SmileyError("ввод закончился, а программа всё ещё спрашивает") from None

    try:
        execute(
            source,
            stdin=ask,
            out=lambda text: print(text, flush=True),
            max_steps=max_steps,
        )
    except SmileyError as error:
        print(error, file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("\n🙈 прервано", file=sys.stderr)
        return 130
    return 0


def _repl(max_steps: int) -> int:
    print("Смайлики 😊  Пустая строка — выполнить накопленное, 🙈 by / выход — выйти.")
    env = Environment()
    buffer: List[str] = []
    while True:
        try:
            line = input("… " if buffer else "😊 ")
        except (EOFError, KeyboardInterrupt):
            print()
            return 0
        if line.strip() in ("выход", "exit", "quit", "пока"):
            print("🙈 Пока!")
            return 0
        if line.strip() == "":
            if not buffer:
                continue
            source = "\n".join(buffer)
            buffer = []
            interpreter = Interpreter(
                stdin=lambda prompt: input(prompt),
                out=print,
                max_steps=max_steps,
                env=env,
            )
            try:
                program = Parser(Lexer(source).tokenize()).parse_program()
                interpreter.run(program)
                if len(program) == 1 and program[0][0] == "expr":
                    print(to_text(interpreter.eval(program[0][1], env)))
            except SmileyError as error:
                print(error)
            except RecursionError:
                print("😵 слишком глубокая рекурсия")
            continue
        buffer.append(line)


def _dump_tokens(path: str) -> int:
    with open(path, "r", encoding="utf-8") as handle:
        source = handle.read()
    try:
        for token in Lexer(source).tokenize():
            if token.kind == "EOF":
                print(token)
                break
            print(f"{token.line:>4}  {token.kind:<10} {token.text!r}")
    except SmileyError as error:
        print(error, file=sys.stderr)
        return 1
    return 0


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="smiley",
        description="Смайлики 😊 — язык программирования на эмодзи",
    )
    parser.add_argument("--max-steps", type=int, default=DEFAULT_MAX_STEPS, help="лимит шагов")
    subparsers = parser.add_subparsers(dest="command")

    run_parser = subparsers.add_parser("run", help="выполнить файл .smile")
    run_parser.add_argument("file")

    subparsers.add_parser("repl", help="интерактивный режим")
    subparsers.add_parser("tokens", help="показать токены файла").add_argument("file")

    args = parser.parse_args(list(argv) if argv is not None else None)

    if args.command == "run":
        return _run_file(args.file, args.max_steps)
    if args.command == "repl":
        return _repl(args.max_steps)
    if args.command == "tokens":
        return _dump_tokens(args.file)
    if args.command is None and getattr(args, "file", None):
        return _run_file(args.file, args.max_steps)

    parser.print_help()
    return 0


def cli(argv: Optional[Sequence[str]] = None) -> int:
    """Точка входа с поддержкой `-c "код"`."""

    raw = list(argv) if argv is not None else sys.argv[1:]
    if raw and raw[0] in ("-c", "--code"):
        code = raw[1] if len(raw) > 1 else ""
        try:
            execute(code, stdin=input, out=lambda text: print(text, flush=True))
        except SmileyError as error:
            print(error, file=sys.stderr)
            return 1
        return 0
    if raw and not raw[0].startswith("-") and raw[0] not in ("run", "repl", "tokens", "help"):
        # `smiley.py файл.smile` — короткая форма
        raw = ["run", *raw]
    return main(raw)


if __name__ == "__main__":
    sys.exit(cli())
