"""Ошибки RYU."""


class RyuuError(Exception):
    """Базовая ошибка формата RYU."""


class RyuuParseError(RyuuError):
    """Ошибка разбора документа RYU.

    Атрибуты:
        line   — номер строки (1-based) или None.
        lineno — синоним line.
    """

    def __init__(self, message, line=None):
        self.line = line
        self.lineno = line
        text = f"line {line}: {message}" if line is not None else message
        super().__init__(text)


class RyuuWriteError(RyuuError):
    """Объект нельзя представить в формате RYU."""
