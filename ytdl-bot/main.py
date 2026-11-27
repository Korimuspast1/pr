# -*- coding: utf-8 -*-
"""Точка входа для облачных хостингов, которым нужен main.py.

Всё равно требует переменную окружения YTDL_BOT_TOKEN.
Локально удобнее: python bot.py --selftest / python bot.py
"""

import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

if __name__ == "__main__":
    import bot
    sys.exit(bot.main())
