# -*- coding: utf-8 -*-
"""Запускалка агента без настройки переменных окружения.

кладёшь токен в config.json рядом — и запускаешь этот файл двойным кликом
(или: python run.py). всё остальное подхватится само.
"""

import json
import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
CFG = HERE / "config.json"
EXAMPLE = HERE / "config.example.json"


def load() -> dict:
    if not CFG.exists():
        if EXAMPLE.exists():
            CFG.write_text(EXAMPLE.read_text(encoding="utf-8"), encoding="utf-8")
        print("создал config.json — впиши туда токен и свой chat_id, потом запусти снова")
        sys.exit(0)
    try:
        cfg = json.loads(CFG.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        print(f"config.json сломан: {e}")
        sys.exit(1)
    return cfg


def main() -> int:
    cfg = load()

    token = str(cfg.get("token", "")).strip()
    if not token or "ВСТАВЬ" in token.upper():
        print("в config.json не заполнено поле token (токен от @BotFather)")
        return 1

    allowed = cfg.get("allowed_chats") or cfg.get("allowed_chat")
    if isinstance(allowed, (int, str)):
        allowed = [allowed]
    allowed = ",".join(str(x) for x in (allowed or []))

    os.environ["PC_AGENT_TOKEN"] = token
    os.environ["PC_AGENT_ALLOWED"] = allowed
    if cfg.get("workdir"):
        os.environ["PC_AGENT_WORKDIR"] = str(cfg["workdir"])

    if not allowed:
        print("в config.json не заполнено allowed_chats — узнай свой id: запусти,")
        print("напиши боту /start, он пришлёт chat_id, и впиши его сюда.")
        os.environ["PC_AGENT_ALLOWED"] = "*"

    sys.path.insert(0, str(HERE))
    import agent
    return agent.main()


if __name__ == "__main__":
    sys.exit(main())
