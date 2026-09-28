"""RYU (Ryuu Notation) — текстовый формат структурированных данных.

Модель данных: дерево (карты, списки, атомы). Синтаксис — обход дерева
курсором: одна строка = один узел, руна в начале строки задаёт роль/тип,
вложенность выражается стеком открытых «гнёзд», а не скобками,
отступами, кавычками или запятыми.

Публичный API:
    loads(text)  -> дерево Python
    dumps(tree)  -> str в формате RYU
    load(fp) / dump(obj, fp)
"""

from .errors import RyuuError, RyuuParseError, RyuuWriteError
from .values import Link
from .parser import loads
from .writer import dumps

__version__ = "1.0.0"

__all__ = [
    "loads", "dumps", "load", "dump",
    "Link",
    "RyuuError", "RyuuParseError", "RyuuWriteError",
    "__version__",
]


def load(fp):
    """Прочитать RYU из файла/потока и вернуть дерево."""
    return loads(fp.read())


def dump(obj, fp):
    """Записать дерево в файл/поток в формате RYU."""
    fp.write(dumps(obj))
