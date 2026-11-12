#!/data/data/com.termux/files/usr/bin/bash
# лёгкая кнопка запуска «бот бандит» в термуксе
cd "$(dirname "$0")" || exit 1

if [ -z "$BANDIT_TOKEN" ] && [ ! -s token.txt ]; then
    echo "токен не найден."
    echo "создай бота у @botfather, потом:"
    echo "  echo 'ТОКЕН_СЮДА' > token.txt"
    echo "и запусти ./start.sh ещё раз"
    exit 1
fi

python bot.py
