#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
VOID — наш язык невозможного.
Один файл. Ноль зависимостей. Полностью с нуля.
Лексер + Парсер + AST + Интерпретатор + REPL
Запуск: python void.py file.void
        python void.py --repl
        python void.py --ast file.void
        python void.py --tokens file.void
"""
import sys
import re
import time
import math
import random

VERSION = "0.1.0-невозможность"

# ─────────────────────────────────────────────
# ТОКЕНЫ
# ─────────────────────────────────────────────

KEYWORDS = {
    # русский : каноническое имя
    "бездна": "ABYSS",
    "abyss": "ABYSS",
    "призвать": "SUMMON",
    "summon": "SUMMON",
    "связать": "BIND",
    "bind": "BIND",
    "let": "BIND",
    "эхо": "ECHO",
    "echo": "ECHO",
    "print": "ECHO",
    "вернуть": "RETURN",
    "return": "RETURN",
    "если": "IF",
    "if": "IF",
    "иначе": "ELSE",
    "else": "ELSE",
    "пока": "WHILE",
    "while": "WHILE",
    "петля": "LOOP",
    "loop": "LOOP",
    "для": "FOR",
    "for": "FOR",
    "в": "IN",
    "in": "IN",
    "парадокс": "PARADOX",
    "paradox": "PARADOX",
    "коллапс": "COLLAPSE",
    "collapse": "COLLAPSE",
    "шепот": "WHISPER",
    "whisper": "WHISPER",
    "закрепить": "ANCHOR",
    "anchor": "ANCHOR",
    "пустота": "VOID_LIT",
    "void": "VOID_LIT",
    "истина": "TRUE_LIT",
    "true": "TRUE_LIT",
    "ложь": "FALSE_LIT",
    "false": "FALSE_LIT",
    "остановить": "BREAK",
    "break": "BREAK",
    "продолжить": "CONTINUE",
    "continue": "CONTINUE",
    "поглотить": "IMPORT",
    "import": "IMPORT",
    "и": "AND",
    "and": "AND",
    "или": "OR",
    "or": "OR",
    "не": "NOT",
}

# Для операторов "и"/"или" нужно не путать с идентификаторами — лексер обработает отдельно

TOKEN_SPEC = [
    ("COMMENT",   r"#.*"),
    ("WHISPER_KW", r"шепот|whisper"),  # обработаем отдельно, пока пропустим как COMMENT?
    ("STRING",    r'"(?:\\.|[^"\\])*"'),
    ("NUMBER",    r"\d+(?:\.\d+)?"),
    ("IDENT",     r"[A-Za-zА-Яа-я_][A-Za-zА-Яа-я0-9_]*"),
    ("PARADOX_OP", r"\?\!"),
    ("RANGE",     r"\.\."),
    ("ARROW",     r"->"),
    ("FATARROW",  r"=>"),
    ("EQEQ",      r"=="),
    ("NEQ",       r"!="),
    ("LEQ",       r"<="),
    ("GEQ",       r">="),
    ("AND2",      r"&&"),
    ("OR2",       r"\|\|"),
    ("ASSIGN",    r"="),
    ("LT",        r"<"),
    ("GT",        r">"),
    ("PLUS",      r"\+"),
    ("MINUS",     r"-"),
    ("STAR",      r"\*"),
    ("SLASH",     r"/"),
    ("PERCENT",   r"%"),
    ("BANG",      r"!"),
    ("TILDE",     r"~"),
    ("LPAREN",    r"\("),
    ("RPAREN",    r"\)"),
    ("LBRACE",    r"\{"),
    ("RBRACE",    r"\}"),
    ("LBRACK",    r"\["),
    ("RBRACK",    r"\]"),
    ("COMMA",     r","),
    ("SEMI",      r";"),
    ("COLON",     r":"),
    ("DOT",       r"\."),
    ("NEWLINE",   r"\n"),
    ("SKIP",      r"[ \t\r]+"),
    ("MISMATCH",  r"."),
]

TOK_REGEX = re.compile("|".join(f"(?P<{n}>{p})" for n, p in TOKEN_SPEC))

class Token:
    __slots__ = ("type", "value", "line", "col")
    def __init__(self, type, value, line, col):
        self.type = type
        self.value = value
        self.line = line
        self.col = col
    def __repr__(self):
        return f"Token({self.type}, {self.value!r}, {self.line}:{self.col})"

def lex(source, filename="<input>"):
    tokens = []
    line = 1
    col = 1
    pos = 0
    for m in TOK_REGEX.finditer(source):
        kind = m.lastgroup
        val = m.group()
        # подсчет позиции
        # col отслеживаем приблизительно
        if kind == "NEWLINE":
            line += 1
            col = 1
            continue
        elif kind == "SKIP" or kind == "COMMENT":
            col += len(val)
            continue
        elif kind == "MISMATCH":
            raise SyntaxError(f"[{filename}:{line}:{col}] неожиданный символ {val!r}")
        elif kind == "IDENT":
            low = val.lower()
            # проверка на ключевые слова
            if low in KEYWORDS:
                # особые случаи: "в"/"и"/"не"/"или" — это ключевые, но могут быть и идентификаторами?
                # считаем ключевыми только если они стоят отдельно — а они и стоят отдельно как IDENT
                kw = KEYWORDS[low]
                tokens.append(Token(kw, val, line, col))
            else:
                tokens.append(Token("IDENT", val, line, col))
        elif kind == "STRING":
            # убрать кавычки и обработать экранирования
            inner = val[1:-1]
            inner = inner.replace(r'\"', '"').replace(r"\\", "\\").replace(r"\n", "\n").replace(r"\t", "\t")
            tokens.append(Token("STRING", inner, line, col))
        elif kind == "NUMBER":
            if "." in val:
                tokens.append(Token("NUMBER", float(val), line, col))
            else:
                tokens.append(Token("NUMBER", int(val), line, col))
        else:
            tokens.append(Token(kind, val, line, col))
        col += len(val)
    tokens.append(Token("EOF", "", line, col))
    return tokens

# ─────────────────────────────────────────────
# AST УЗЛЫ
# ─────────────────────────────────────────────

class Node: pass

class Program(Node):
    def __init__(self, stmts): self.stmts = stmts
    def __repr__(self): return f"Program({self.stmts})"

class VarDecl(Node):
    def __init__(self, name, expr, is_paradox=False): self.name=name; self.expr=expr; self.is_paradox=is_paradox

class FuncDecl(Node):
    def __init__(self, name, params, body): self.name=name; self.params=params; self.body=body

class AbyssBlock(Node):
    def __init__(self, name, body): self.name=name; self.body=body

class IfStmt(Node):
    def __init__(self, cond, then_block, else_block): self.cond=cond; self.then_block=then_block; self.else_block=else_block

class WhileStmt(Node):
    def __init__(self, cond, body): self.cond=cond; self.body=body

class LoopStmt(Node):
    def __init__(self, body): self.body=body

class ForStmt(Node):
    def __init__(self, var, iterable, body): self.var=var; self.iterable=iterable; self.body=body

class EchoStmt(Node):
    def __init__(self, expr): self.expr=expr

class ReturnStmt(Node):
    def __init__(self, expr): self.expr=expr

class CollapseStmt(Node):
    def __init__(self, expr, target): self.expr=expr; self.target=target

class BreakStmt(Node): pass
class ContinueStmt(Node): pass
class WhisperStmt(Node):
    def __init__(self, text): self.text=text

class ExprStmt(Node):
    def __init__(self, expr): self.expr=expr

# Expr nodes
class BinaryOp(Node):
    def __init__(self, op, left, right): self.op=op; self.left=left; self.right=right
    def __repr__(self): return f"({self.left} {self.op} {self.right})"

class UnaryOp(Node):
    def __init__(self, op, expr): self.op=op; self.expr=expr
    def __repr__(self): return f"({self.op}{self.expr})"

class ParadoxChoice(Node):
    def __init__(self, left, right): self.left=left; self.right=right
    def __repr__(self): return f"({self.left} ?! {self.right})"

class Literal(Node):
    def __init__(self, value): self.value=value
    def __repr__(self): return repr(self.value)

class Var(Node):
    def __init__(self, name): self.name=name
    def __repr__(self): return self.name

class Call(Node):
    def __init__(self, callee, args): self.callee=callee; self.args=args
    def __repr__(self): return f"{self.callee}({self.args})"

class ArrayLit(Node):
    def __init__(self, elems): self.elems=elems

class RangeExpr(Node):
    def __init__(self, start, end): self.start=start; self.end=end

class Block(Node):
    def __init__(self, stmts): self.stmts=stmts

# ─────────────────────────────────────────────
# ПАРСЕР
# ─────────────────────────────────────────────

class Parser:
    def __init__(self, tokens, filename="<input>"):
        self.tokens = tokens
        self.filename = filename
        self.pos = 0

    def peek(self): return self.tokens[self.pos]
    def peek_type(self): return self.tokens[self.pos].type
    def advance(self):
        t = self.tokens[self.pos]
        self.pos += 1
        return t
    def match(self, *types):
        if self.peek_type() in types:
            return self.advance()
        return None
    def expect(self, type, msg=""):
        t = self.peek()
        if t.type != type:
            raise SyntaxError(f"[{self.filename}:{t.line}:{t.col}] ожидалось {type}, получено {t.type} ({t.value!r}) {msg}")
        return self.advance()
    def at_end(self): return self.peek_type() == "EOF"

    def parse(self):
        stmts = []
        while not self.at_end():
            # пропускаем лишние ;
            if self.peek_type() == "SEMI":
                self.advance()
                continue
            stmts.append(self.parse_statement())
        return Program(stmts)

    def parse_statement(self):
        t = self.peek_type()
        if t == "BIND":
            return self.parse_var_decl()
        elif t == "PARADOX":
            return self.parse_paradox_decl()
        elif t == "SUMMON":
            return self.parse_func_decl()
        elif t == "ABYSS":
            return self.parse_abyss()
        elif t == "IF":
            return self.parse_if()
        elif t == "WHILE":
            return self.parse_while()
        elif t == "LOOP":
            return self.parse_loop()
        elif t == "FOR":
            return self.parse_for()
        elif t == "ECHO":
            return self.parse_echo()
        elif t == "RETURN":
            return self.parse_return()
        elif t == "COLLAPSE":
            return self.parse_collapse()
        elif t == "BREAK":
            self.advance()
            self.match("SEMI")
            return BreakStmt()
        elif t == "CONTINUE":
            self.advance()
            self.match("SEMI")
            return ContinueStmt()
        elif t == "WHISPER":
            self.advance()
            # шепот может быть со строкой или без
            txt = ""
            if self.peek_type() == "STRING":
                txt = self.advance().value
            self.match("SEMI")
            return WhisperStmt(txt)
        elif t == "IMPORT":
            # поглотить "file.void";
            self.advance()
            path = self.expect("STRING").value
            self.match("SEMI")
            # пока просто whisper, импорт обработаем в интерпретаторе если нужно
            # создадим узел как WhisperStmt с меткой импорта
            # проще: вернем ExprStmt с импортом? сделаем отдельный узел
            # сделаем как WhisperStmt с префиксом IMPORT:
            return WhisperStmt(f"IMPORT:{path}")
        else:
            # expression statement — присваивание или вызов
            # проверим присваивание: IDENT = expr ;
            # но IDENT может быть началом выражения
            # сохраним позицию
            if t == "IDENT":
                # lookahead
                nxt = self.tokens[self.pos+1] if self.pos+1 < len(self.tokens) else None
                if nxt and nxt.type == "ASSIGN":
                    name = self.advance().value
                    self.advance() # =
                    expr = self.parse_expr()
                    self.match("SEMI")
                    # это присваивание как VarDecl без let? трактуем как повторное привязывание
                    # создадим узел BinaryOp присваивания? проще — VarDecl с флагом reassignment
                    # сделаем ExprStmt с BinaryOp("=", Var(name), expr)
                    return ExprStmt(BinaryOp("=", Var(name), expr))
            expr = self.parse_expr()
            self.match("SEMI")
            return ExprStmt(expr)

    def parse_var_decl(self):
        self.expect("BIND")
        name = self.expect("IDENT").value
        expr = None
        if self.match("ASSIGN"):
            expr = self.parse_expr()
        self.match("SEMI")
        return VarDecl(name, expr, is_paradox=False)

    def parse_paradox_decl(self):
        self.expect("PARADOX")
        name = self.expect("IDENT").value
        self.expect("FATARROW")
        expr = self.parse_expr()
        self.match("SEMI")
        return VarDecl(name, expr, is_paradox=True)

    def parse_func_decl(self):
        self.expect("SUMMON")
        name = self.expect("IDENT").value
        self.expect("LPAREN")
        params = []
        if self.peek_type() != "RPAREN":
            params.append(self.expect("IDENT").value)
            while self.match("COMMA"):
                params.append(self.expect("IDENT").value)
        self.expect("RPAREN")
        body = self.parse_block()
        return FuncDecl(name, params, body)

    def parse_abyss(self):
        self.expect("ABYSS")
        name = None
        if self.peek_type() == "IDENT":
            name = self.advance().value
        body = self.parse_block()
        return AbyssBlock(name, body)

    def parse_if(self):
        self.expect("IF")
        cond = self.parse_expr()
        then_b = self.parse_block()
        else_b = None
        if self.match("ELSE"):
            else_b = self.parse_block()
        return IfStmt(cond, then_b, else_b)

    def parse_while(self):
        self.expect("WHILE")
        cond = self.parse_expr()
        body = self.parse_block()
        return WhileStmt(cond, body)

    def parse_loop(self):
        self.expect("LOOP")
        body = self.parse_block()
        return LoopStmt(body)

    def parse_for(self):
        self.expect("FOR")
        var = self.expect("IDENT").value
        self.expect("IN")
        iterable = self.parse_expr()
        body = self.parse_block()
        return ForStmt(var, iterable, body)

    def parse_echo(self):
        self.expect("ECHO")
        expr = self.parse_expr()
        self.match("SEMI")
        return EchoStmt(expr)

    def parse_return(self):
        self.expect("RETURN")
        expr = None
        if self.peek_type() not in ("SEMI", "RBRACE", "EOF"):
            expr = self.parse_expr()
        self.match("SEMI")
        return ReturnStmt(expr)

    def parse_collapse(self):
        self.expect("COLLAPSE")
        expr = self.parse_expr()
        target = None
        if self.match("ARROW"):
            target = self.expect("IDENT").value
        self.match("SEMI")
        return CollapseStmt(expr, target)

    def parse_block(self):
        self.expect("LBRACE")
        stmts = []
        while self.peek_type() != "RBRACE" and not self.at_end():
            if self.peek_type() == "SEMI":
                self.advance()
                continue
            stmts.append(self.parse_statement())
        self.expect("RBRACE")
        return Block(stmts)

    # ── выражения ──

    def parse_expr(self):
        return self.parse_paradox_choice()

    def parse_paradox_choice(self):
        left = self.parse_or()
        while self.match("PARADOX_OP"):
            right = self.parse_or()
            left = ParadoxChoice(left, right)
        return left

    def parse_or(self):
        left = self.parse_and()
        while self.peek_type() == "OR" or self.peek_type() == "OR2":
            op = self.advance().type
            right = self.parse_and()
            left = BinaryOp("или", left, right)
        return left

    def parse_and(self):
        left = self.parse_equality()
        while self.peek_type() == "AND" or self.peek_type() == "AND2":
            op = self.advance().type
            right = self.parse_equality()
            left = BinaryOp("и", left, right)
        return left

    def parse_equality(self):
        left = self.parse_comparison()
        while self.peek_type() in ("EQEQ", "NEQ"):
            op = self.advance().value
            right = self.parse_comparison()
            left = BinaryOp(op, left, right)
        return left

    def parse_comparison(self):
        left = self.parse_range()
        while self.peek_type() in ("LT", "GT", "LEQ", "GEQ"):
            op = self.advance().value
            right = self.parse_range()
            left = BinaryOp(op, left, right)
        return left

    def parse_range(self):
        left = self.parse_term()
        if self.peek_type() == "RANGE":
            self.advance()
            right = self.parse_term()
            return RangeExpr(left, right)
        return left

    def parse_term(self):
        left = self.parse_factor()
        while self.peek_type() in ("PLUS", "MINUS"):
            op = self.advance().value
            right = self.parse_factor()
            left = BinaryOp(op, left, right)
        return left

    def parse_factor(self):
        left = self.parse_unary()
        while self.peek_type() in ("STAR", "SLASH", "PERCENT"):
            op = self.advance().value
            right = self.parse_unary()
            left = BinaryOp(op, left, right)
        return left

    def parse_unary(self):
        if self.peek_type() in ("BANG", "TILDE", "NOT", "MINUS"):
            op = self.advance().value
            # "не" приходит как NOT с value "не"
            expr = self.parse_unary()
            return UnaryOp(op, expr)
        return self.parse_primary()

    def parse_primary(self):
        t = self.peek()
        if t.type == "NUMBER":
            self.advance()
            return Literal(t.value)
        elif t.type == "STRING":
            self.advance()
            return Literal(t.value)
        elif t.type in ("VOID_LIT",):
            self.advance()
            return Literal(None)  # пустота = None
        elif t.type in ("TRUE_LIT",):
            self.advance()
            return Literal(True)
        elif t.type in ("FALSE_LIT",):
            self.advance()
            return Literal(False)
        elif t.type == "IDENT":
            name = self.advance().value
            node = Var(name)
            if self.peek_type() == "LPAREN":
                self.advance()
                args = []
                if self.peek_type() != "RPAREN":
                    args.append(self.parse_expr())
                    while self.match("COMMA"):
                        args.append(self.parse_expr())
                self.expect("RPAREN")
                node = Call(node, args)
            return node
        elif t.type == "LPAREN":
            self.advance()
            expr = self.parse_expr()
            self.expect("RPAREN")
            # может быть вызов? уже учтено выше, но (expr)(args) не поддерживаем
            return expr
        elif t.type == "LBRACK":
            self.advance()
            elems = []
            if self.peek_type() != "RBRACK":
                elems.append(self.parse_expr())
                while self.match("COMMA"):
                    elems.append(self.parse_expr())
            self.expect("RBRACK")
            return ArrayLit(elems)
        else:
            raise SyntaxError(f"[{self.filename}:{t.line}:{t.col}] неожиданное выражение: {t.type} ({t.value!r})")

# ─────────────────────────────────────────────
# РАНТАЙМ
# ─────────────────────────────────────────────

class VoidError(Exception): pass
class ReturnException(Exception):
    def __init__(self, value): self.value = value
class BreakException(Exception): pass
class ContinueException(Exception): pass

class ParadoxValue:
    """Ленивое значение. Хранит AST и окружение, вычисляется при коллапсе."""
    def __init__(self, expr, env, interp):
        self.expr = expr
        self.env = env  # ссылка на окружение где создан
        self.interp = interp
        self.collapsed = False
        self.cached = None
    def collapse(self):
        if self.collapsed:
            return self.cached
        # вычисляем в сохраненном окружении, но с текущим интерпретатором для вложенных вызовов?
        # Сохраняем текущее окружение интерпретатора, подменяем на self.env
        old_env = self.interp.env
        self.interp.env = self.env
        try:
            val = self.interp.eval_expr(self.expr)
            # если val тоже Paradox — коллапсируем рекурсивно
            if isinstance(val, ParadoxValue):
                val = val.collapse()
            self.cached = val
            self.collapsed = True
            return val
        finally:
            self.interp.env = old_env
    def __repr__(self):
        if self.collapsed:
            return f"<парадокс: коллапсирован {self.cached!r}>"
        return f"<парадокс: {self.expr}>"

class Environment:
    def __init__(self, parent=None):
        self.vars = {}
        self.parent = parent
    def define(self, name, value):
        self.vars[name] = value
    def get(self, name):
        if name in self.vars:
            return self.vars[name]
        if self.parent:
            return self.parent.get(name)
        raise VoidError(f"неизвестная переменная '{name}' — она растворилась в бездне")
    def set(self, name, value):
        if name in self.vars:
            self.vars[name] = value
            return
        if self.parent:
            try:
                self.parent.set(name, value)
                return
            except VoidError:
                pass
        # если не найдено — определяем в текущем
        self.vars[name] = value
    def has(self, name):
        if name in self.vars: return True
        if self.parent: return self.parent.has(name)
        return False

class Function:
    def __init__(self, decl, closure_env):
        self.decl = decl
        self.closure = closure_env
    def __repr__(self):
        return f"<функция {self.decl.name}/{len(self.decl.params)}>"

class Interpreter:
    def __init__(self, filename="<input>"):
        self.filename = filename
        self.global_env = Environment()
        self.env = self.global_env
        self.output = []
        self.start_time = time.time()
        self.setup_builtins()

    def setup_builtins(self):
        def builtin_len(x):
            if x is None: return 0
            if isinstance(x, (str, list)): return len(x)
            return 1
        def builtin_str(x):
            if x is None: return "пустота"
            if x is True: return "истина"
            if x is False: return "ложь"
            return str(x)
        def builtin_num(s):
            try:
                if isinstance(s, (int, float)): return s
                if "." in str(s): return float(s)
                return int(s)
            except: return None
        def builtin_type(x):
            if x is None: return "пустота"
            if isinstance(x, bool): return "булево"
            if isinstance(x, int): return "число"
            if isinstance(x, float): return "число"
            if isinstance(x, str): return "строка"
            if isinstance(x, list): return "массив"
            if isinstance(x, Function): return "функция"
            if isinstance(x, ParadoxValue): return "парадокс"
            return "неизвестное"
        def builtin_time():
            return time.time() - self.start_time
        def builtin_random():
            return random.random()
        def builtin_abs(x): return abs(x) if isinstance(x,(int,float)) else None
        def builtin_min(*args):
            if len(args)==1 and isinstance(args[0], list): args=args[0]
            return min(args) if args else None
        def builtin_max(*args):
            if len(args)==1 and isinstance(args[0], list): args=args[0]
            return max(args) if args else None
        # регистрируем и русские и английские имена
        builtins = {
            "длина": builtin_len, "len": builtin_len,
            "строка": builtin_str, "str": builtin_str,
            "число": builtin_num, "num": builtin_num,
            "тип": builtin_type, "type": builtin_type,
            "время": builtin_time, "time": builtin_time,
            "случайно": builtin_random, "random": builtin_random,
            "абс": builtin_abs, "abs": builtin_abs,
            "мин": builtin_min, "min": builtin_min,
            "макс": builtin_max, "max": builtin_max,
        }
        for k,v in builtins.items():
            self.global_env.define(k, v)

    # ── выполнение программ ──

    def run(self, program: Program):
        # 1 фаза: поднять все функции в глобальную область (чтобы можно было вызвать до определения — "время")
        for stmt in program.stmts:
            if isinstance(stmt, FuncDecl):
                self.global_env.define(stmt.name, Function(stmt, self.env))
            elif isinstance(stmt, AbyssBlock):
                # бездна не выполняется сразу? В нашей семантике abyss main — точка входа, но остальные — просто блоки
                pass
        # 2 фаза: выполнить
        # ищем abyss main
        main_abyss = None
        other_stmts = []
        for stmt in program.stmts:
            if isinstance(stmt, AbyssBlock) and (stmt.name == "main" or stmt.name == "главная"):
                main_abyss = stmt
            elif isinstance(stmt, AbyssBlock):
                # остальные бездны — определяем как функции без параметров? или просто выполняем?
                other_stmts.append(stmt)
            elif not isinstance(stmt, FuncDecl):
                other_stmts.append(stmt)
        try:
            if main_abyss:
                self.exec_block(main_abyss.body)
                # выполнить остальные стейтменты вне main? нет
            else:
                for stmt in other_stmts:
                    self.exec_stmt(stmt)
                # и бездны без main тоже выполним
                for stmt in other_stmts:
                    if isinstance(stmt, AbyssBlock):
                        self.exec_block(stmt.body)
        except ReturnException:
            pass # return вне функции игнорим

    def exec_block(self, block: Block):
        prev = self.env
        self.env = Environment(prev)
        try:
            for stmt in block.stmts:
                self.exec_stmt(stmt)
        finally:
            self.env = prev

    def exec_stmt(self, stmt):
        if isinstance(stmt, VarDecl):
            if stmt.is_paradox:
                pv = ParadoxValue(stmt.expr, self.env, self)
                self.env.define(stmt.name, pv)
            else:
                val = None
                if stmt.expr is not None:
                    val = self.eval_expr(stmt.expr)
                self.env.define(stmt.name, val)
        elif isinstance(stmt, FuncDecl):
            self.env.define(stmt.name, Function(stmt, self.env))
        elif isinstance(stmt, AbyssBlock):
            self.exec_block(stmt.body)
        elif isinstance(stmt, IfStmt):
            cond = self.eval_expr(stmt.cond)
            if self.is_truthy(cond):
                self.exec_block(stmt.then_block)
            elif stmt.else_block:
                self.exec_block(stmt.else_block)
        elif isinstance(stmt, WhileStmt):
            while self.is_truthy(self.eval_expr(stmt.cond)):
                try:
                    self.exec_block(stmt.body)
                except BreakException:
                    break
                except ContinueException:
                    continue
        elif isinstance(stmt, LoopStmt):
            while True:
                try:
                    self.exec_block(stmt.body)
                except BreakException:
                    break
                except ContinueException:
                    continue
        elif isinstance(stmt, ForStmt):
            iterable = self.eval_expr(stmt.iterable)
            # iterable может быть RangeExpr -> list, ArrayLit -> list, или Paradox
            if isinstance(iterable, ParadoxValue):
                iterable = iterable.collapse()
            lst = self.to_iterable(iterable)
            for val in lst:
                # each iteration new env? используем блок для изоляции, но переменную цикла определяем во внешнем новом env
                # Создаем окружение итерации
                prev = self.env
                self.env = Environment(prev)
                self.env.define(stmt.var, val)
                try:
                    self.exec_block(stmt.body)
                except BreakException:
                    self.env = prev
                    break
                except ContinueException:
                    pass
                finally:
                    # если не break, вернем env
                    if self.env.parent == prev or self.env == prev:
                        pass
                    self.env = prev
        elif isinstance(stmt, EchoStmt):
            val = self.eval_expr(stmt.expr)
            if isinstance(val, ParadoxValue):
                val = val.collapse()
            out = self.stringify(val)
            print(out)
            self.output.append(out)
        elif isinstance(stmt, ReturnStmt):
            val = None
            if stmt.expr:
                val = self.eval_expr(stmt.expr)
                if isinstance(val, ParadoxValue):
                    val = val.collapse()
            raise ReturnException(val)
        elif isinstance(stmt, CollapseStmt):
            val = self.eval_expr(stmt.expr)
            if isinstance(val, ParadoxValue):
                val = val.collapse()
            # если val все еще Paradox (вложенный) — коллапснуть
            while isinstance(val, ParadoxValue):
                val = val.collapse()
            if stmt.target:
                self.env.define(stmt.target, val)
        elif isinstance(stmt, BreakStmt):
            raise BreakException()
        elif isinstance(stmt, ContinueStmt):
            raise ContinueException()
        elif isinstance(stmt, WhisperStmt):
            if stmt.text.startswith("IMPORT:"):
                path = stmt.text[7:]
                self.do_import(path)
            else:
                pass # шепот игнорируется, но это канон
        elif isinstance(stmt, ExprStmt):
            self.eval_expr(stmt.expr)
        else:
            raise VoidError(f"неизвестный стейтмент {stmt}")

    def do_import(self, path):
        # простой импорт — загрузить файл и выполнить в текущем окружении
        import os
        base = os.path.dirname(self.filename) if self.filename != "<input>" else "."
        full = os.path.join(base, path)
        if not os.path.exists(full):
            # пробуем относительно void-lang/
            alt = os.path.join(os.path.dirname(__file__) if "__file__" in globals() else ".", path)
            if os.path.exists(alt):
                full = alt
            else:
                raise VoidError(f"не удалось поглотить '{path}' — файл не найден")
        with open(full, encoding="utf-8") as f:
            src = f.read()
        toks = lex(src, full)
        parser = Parser(toks, full)
        prog = parser.parse()
        # выполнить в текущем env (без создания нового)
        for stmt in prog.stmts:
            if isinstance(stmt, FuncDecl):
                self.env.define(stmt.name, Function(stmt, self.env))
        for stmt in prog.stmts:
            if not isinstance(stmt, FuncDecl) and not isinstance(stmt, AbyssBlock):
                self.exec_stmt(stmt)

    # ── выражения ──

    def eval_expr(self, node):
        if isinstance(node, Literal):
            return node.value
        elif isinstance(node, Var):
            val = self.env.get(node.name)
            return val
        elif isinstance(node, ArrayLit):
            return [self.eval_expr(e) for e in node.elems]
        elif isinstance(node, RangeExpr):
            a = self.eval_expr(node.start)
            b = self.eval_expr(node.end)
            if isinstance(a, ParadoxValue): a = a.collapse()
            if isinstance(b, ParadoxValue): b = b.collapse()
            a = int(a) if a is not None else 0
            b = int(b) if b is not None else 0
            return list(range(a, b))
        elif isinstance(node, Call):
            callee = self.eval_expr(node.callee)
            args = []
            for a in node.args:
                v = self.eval_expr(a)
                if isinstance(v, ParadoxValue):
                    v = v.collapse()
                args.append(v)
            if isinstance(callee, Function):
                return self.call_function(callee, args)
            elif callable(callee):
                try:
                    return callee(*args)
                except Exception as e:
                    raise VoidError(f"ошибка вызова встроенной функции: {e}")
            else:
                raise VoidError(f"'{node.callee}' не является функцией")
        elif isinstance(node, UnaryOp):
            val = self.eval_expr(node.expr)
            if isinstance(val, ParadoxValue):
                val = val.collapse()
            op = node.op.lower() if isinstance(node.op, str) else node.op
            if op in ("!", "~", "не", "not"):
                return not self.is_truthy(val)
            elif op == "-":
                if val is None: return None
                return -val
            return val
        elif isinstance(node, BinaryOp):
            # присваивание
            if node.op == "=":
                # node.left должен быть Var
                if not isinstance(node.left, Var):
                    raise VoidError("слева от = должна быть переменная")
                val = self.eval_expr(node.right)
                if isinstance(val, ParadoxValue):
                    val = val.collapse()
                # set в ближайшей области где переменная есть, иначе в текущей
                self.env.set(node.left.name, val)
                return val
            # обычная бинарная операция
            left = self.eval_expr(node.left)
            if isinstance(left, ParadoxValue):
                left = left.collapse()
            right = self.eval_expr(node.right)
            if isinstance(right, ParadoxValue):
                right = right.collapse()
            op = node.op
            # логические
            if op in ("и", "and", "&&"):
                return self.is_truthy(left) and self.is_truthy(right)
            if op in ("или", "or", "||"):
                return self.is_truthy(left) or self.is_truthy(right)
            # сравнения
            if op == "==": return left == right
            if op == "!=": return left != right
            if op == "<":  return left is not None and right is not None and left < right
            if op == ">":  return left is not None and right is not None and left > right
            if op == "<=": return left is not None and right is not None and left <= right
            if op == ">=": return left is not None and right is not None and left >= right
            # арифметика
            # пустота ведет себя как 0 для +, как 1 для *? сделаем: пустота = 0 для арифметики, но ?! ее ловит
            if op == "+":
                if left is None: left = 0
                if right is None: right = 0
                if isinstance(left, str) or isinstance(right, str):
                    return self.stringify(left) + self.stringify(right)
                if isinstance(left, list) and isinstance(right, list):
                    return left + right
                return left + right
            if op == "-":
                if left is None: left = 0
                if right is None: right = 0
                return left - right
            if op == "*":
                if left is None or right is None: return 0
                if isinstance(left, str) and isinstance(right, int):
                    return left * right
                if isinstance(left, list) and isinstance(right, int):
                    return left * right
                return left * right
            if op == "/":
                if right == 0 or right is None:
                    raise VoidError("деление на пустоту/ноль — невозможно")
                if left is None: left = 0
                return left / right
            if op == "%":
                if right == 0 or right is None:
                    raise VoidError("деление на пустоту/ноль — невозможно")
                if left is None: left = 0
                return left % right
            raise VoidError(f"неизвестный оператор {op}")
        elif isinstance(node, ParadoxChoice):
            # a ?! b — если a невозможно (ошибка, пустота, ложь как ошибка), взять b
            try:
                left = self.eval_expr(node.left)
                if isinstance(left, ParadoxValue):
                    left = left.collapse()
                # считаем "невозможным" если пустота/None или ложь? для строгости — только пустота и ошибка
                # но сделаем: если left is None -> невозможно
                # если left is False и это результат деления — уже ошибка
                # иначе считаем что если left == None -> берем правую
                if left is None:
                    raise VoidError("пустота — невозможное значение")
                return left
            except (VoidError, ZeroDivisionError, TypeError, ValueError) as e:
                right = self.eval_expr(node.right)
                if isinstance(right, ParadoxValue):
                    right = right.collapse()
                return right
        else:
            raise VoidError(f"неизвестное выражение {node}")

    def call_function(self, func: Function, args):
        if len(args) != len(func.decl.params):
            raise VoidError(f"функция {func.decl.name} ожидала {len(func.decl.params)} аргументов, получено {len(args)}")
        prev = self.env
        self.env = Environment(func.closure)
        for name, val in zip(func.decl.params, args):
            self.env.define(name, val)
        try:
            self.exec_block(func.decl.body)
            return None # пустота если нет return
        except ReturnException as r:
            return r.value
        finally:
            self.env = prev

    def is_truthy(self, val):
        if val is None: return False
        if val is False: return False
        if val == 0: return False
        if val == "": return False
        if isinstance(val, list) and len(val)==0: return False
        if isinstance(val, ParadoxValue):
            return self.is_truthy(val.collapse())
        return True

    def to_iterable(self, val):
        if isinstance(val, list): return val
        if isinstance(val, str): return list(val)
        if isinstance(val, range): return list(val)
        # если число — диапазон 0..число
        if isinstance(val, (int, float)): return list(range(int(val)))
        return [val]

    def stringify(self, val):
        if val is None: return "пустота"
        if val is True: return "истина"
        if val is False: return "ложь"
        if isinstance(val, float):
            if val.is_integer(): return str(int(val))
            return str(val)
        if isinstance(val, list):
            return "[" + ", ".join(self.stringify(x) for x in val) + "]"
        if isinstance(val, Function): return f"<функция {val.decl.name}>"
        if isinstance(val, ParadoxValue): return "<парадокс>"
        return str(val)

# ─────────────────────────────────────────────
# CLI
# ─────────────────────────────────────────────

def run_file(path):
    with open(path, encoding="utf-8") as f:
        src = f.read()
    toks = lex(src, path)
    parser = Parser(toks, path)
    prog = parser.parse()
    interp = Interpreter(path)
    interp.run(prog)
    return interp

def run_source(src, filename="<repl>"):
    toks = lex(src, filename)
    parser = Parser(toks, filename)
    prog = parser.parse()
    interp = Interpreter(filename)
    interp.run(prog)
    return interp

ASCII = r"""
         ⌀  VOID  ⌀
    язык невозможного v0.1
  напиши невозможное — запусти неизбежное
  ─────────────────────────────────────
  команды REPL: exit, clear, help
"""

HELP = """
VOID REPL помощь:
  введи код VOID и нажми Enter — он выполнится
  многострочный: открой { и пиши до }
  команды:
    exit / выход  — выйти
    clear / очисти — очистить
    help / помощь — эта справка
  примеры:
    эхо "привет";
    связать x = 2 + 2;
    призвать фиб(n) { если n <= 1 { вернуть n; } вернуть фиб(n-1)+фиб(n-2); }
    эхо фиб(10);
"""

def repl():
    print(ASCII)
    print("VOID REPL — пиши на русском или английском. 'help' для справки.\n")
    buf = ""
    interp = Interpreter("<repl>")
    # загружаем глобальные функции чтобы они сохранялись между вводами
    # будем использовать один интерпретатор и допарсивать
    while True:
        prompt = "⌀ " if not buf else "  "
        try:
            line = input(prompt)
        except EOFError:
            print("\nвыход из бездны...")
            break
        if not buf:
            stripped = line.strip().lower()
            if stripped in ("exit", "выход", "quit", "q"):
                print("до встречи в невозможности ⌀")
                break
            if stripped in ("clear", "очисти"):
                print("\x1bc", end="")
                print(ASCII)
                continue
            if stripped in ("help", "помощь", "?"):
                print(HELP)
                continue
            if not line.strip():
                continue
        buf += line + "\n"
        # проверяем баланс скобок
        opens = buf.count("{") - buf.count("}")
        if opens > 0:
            continue
        # пробуем выполнить
        src = buf.strip()
        buf = ""
        if not src:
            continue
        # добавляем точку с запятой если нет
        try:
            toks = lex(src, "<repl>")
            parser = Parser(toks, "<repl>")
            prog = parser.parse()
            # выполним используя текущий интерпретатор (чтобы сохранить состояние)
            # но нужно поднять функции
            for stmt in prog.stmts:
                if isinstance(stmt, FuncDecl):
                    interp.global_env.define(stmt.name, Function(stmt, interp.env))
            for stmt in prog.stmts:
                if isinstance(stmt, FuncDecl):
                    continue
                # выполним стейтмент
                try:
                    interp.exec_stmt(stmt)
                except ReturnException:
                    pass
        except SyntaxError as e:
            print(f"синтаксическая невозможность: {e}")
        except VoidError as e:
            print(f"ошибка бездны: {e}")
        except Exception as e:
            print(f"неизвестная ошибка: {e}")

def main():
    import argparse
    ap = argparse.ArgumentParser(description="VOID — язык невозможного", prog="void")
    ap.add_argument("file", nargs="?", help="файл .void для выполнения")
    ap.add_argument("--repl", action="store_true", help="интерактивный режим")
    ap.add_argument("--ast", action="store_true", help="показать AST")
    ap.add_argument("--tokens", action="store_true", help="показать токены")
    ap.add_argument("--version", action="store_true", help="версия")
    args = ap.parse_args()

    if args.version:
        print(f"VOID {VERSION}")
        print("сделано с нуля, без зависимостей. наше.")
        return

    if args.repl or not args.file:
        if args.file and not args.repl:
            # выполним файл, но если флаги --ast/--tokens
            pass
        if args.repl:
            repl()
            return
        # если файл не указан и не repl — запустить repl
        if not args.file:
            repl()
            return

    if args.tokens:
        with open(args.file, encoding="utf-8") as f:
            src = f.read()
        toks = lex(src, args.file)
        for t in toks:
            print(t)
        return

    if args.ast:
        with open(args.file, encoding="utf-8") as f:
            src = f.read()
        toks = lex(src, args.file)
        parser = Parser(toks, args.file)
        prog = parser.parse()
        print(prog)
        for s in prog.stmts:
            print(" ", s, getattr(s, "name", ""), getattr(s, "body", ""))
        return

    # обычный запуск
    try:
        interp = run_file(args.file)
    except SyntaxError as e:
        print(f"синтаксическая ошибка: {e}", file=sys.stderr)
        sys.exit(1)
    except VoidError as e:
        print(f"ошибка времени выполнения: {e}", file=sys.stderr)
        sys.exit(1)
    except FileNotFoundError:
        print(f"файл не найден: {args.file}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()
