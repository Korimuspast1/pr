#!/data/data/com.termux/files/usr/bin/bash
# «Бот бандит» — кнопка запуска. делает всё само:
# ищет python → чистит кэш → ставит зависимости (venv или системно) → проверяет токен → запускает.

cd "$(dirname "$0")" || exit 1

PY=""
for c in python python3; do
    if command -v "$c" >/dev/null 2>&1; then PY="$c"; break; fi
done
if [ -z "$PY" ]; then
    echo "❌ python не найден. сделай:"
    echo "   pkg update && pkg install python -y"
    exit 1
fi

# битые старые копии и протухший кэш — частая причина «is_admin is not defined»
rm -rf __pycache__

if ! "$PY" -c "import telebot" >/dev/null 2>&1; then
    echo "▶ ставлю pyTelegramBotAPI…"
    if "$PY" -m venv .venv 2>/dev/null && [ -x .venv/bin/python ]; then
        .venv/bin/python -m pip install -q -r requirements.txt && PY="$(pwd)/.venv/bin/python"
    fi
    if ! "$PY" -c "import telebot" >/dev/null 2>&1; then
        "$PY" -m pip install -q -r requirements.txt \
            || "$PY" -m pip install -q --break-system-packages -r requirements.txt \
            || { echo "❌ не смог поставить pyTelegramBotAPI. вручную:"; \
                 echo "   $PY -m pip install pyTelegramBotAPI"; exit 1; }
    fi
    echo "✅ зависимости стоят ($($PY --version))"
fi

if [ -z "$BANDIT_TOKEN" ] && [ ! -s token.txt ]; then
    echo "❌ токен не найден."
    echo "   создай бота у @botfather → /newbot, потом:"
    echo "   echo 'ТОКЕН_СЮДА' > token.txt"
    echo "   и запусти ./start.sh ещё раз"
    exit 1
fi

command -v termux-wake-lock >/dev/null 2>&1 && termux-wake-lock
echo "▶ запускаю «бот бандит»…"
"$PY" bot.py
