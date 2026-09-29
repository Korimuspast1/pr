#!/usr/bin/env bash
# Сборка модуля profile-flex в .ttydyn.
#
# Требуется (на машине разработчика, не в песочнице):
#   * JDK 17+                      — javac
#   * Android SDK: platforms/android-34/android.jar и build-tools/*/d8
#   * python3                      — упаковка и подпись (tools/ttydyn.py)
#
# Переменные окружения:
#   ANDROID_HOME   путь к SDK (по умолчанию ~/Android/Sdk)
#   ANDROID_JAR    путь к android.jar (если нестандартный)
#   D8             путь к d8
#   TTY_KEY        приватный ключ для подписи (по умолчанию keys/dev.key.pem)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
BUILD="$HERE/build"
OUT="$ROOT/release"

ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
ANDROID_JAR="${ANDROID_JAR:-$(ls -d "$ANDROID_HOME"/platforms/android-*/android.jar 2>/dev/null | sort -V | tail -1 || true)}"
D8="${D8:-$(ls -d "$ANDROID_HOME"/build-tools/*/d8 2>/dev/null | sort -V | tail -1 || true)}"
TTY_KEY="${TTY_KEY:-$ROOT/keys/dev.key.pem}"

[ -n "$ANDROID_JAR" ] && [ -f "$ANDROID_JAR" ] || { echo "android.jar не найден, задайте ANDROID_JAR"; exit 1; }
[ -n "$D8" ] && [ -x "$D8" ] || { echo "d8 не найден, задайте D8"; exit 1; }

rm -rf "$BUILD"
mkdir -p "$BUILD/classes" "$OUT"

echo "==> javac"
find "$HERE/src" "$HERE/stubs" -name '*.java' > "$BUILD/sources.txt"
javac -source 8 -target 8 -nowarn -encoding UTF-8 \
      -bootclasspath "$ANDROID_JAR" \
      -d "$BUILD/classes" \
      @"$BUILD/sources.txt"

# Классы-заглушки com.windukk.* существуют в моде — в модуль их класть нельзя.
echo "==> вырезаем stub-классы"
rm -rf "$BUILD/classes/com/windukk/hook"

echo "==> d8"
find "$BUILD/classes" -name '*.class' > "$BUILD/classes.txt"
"$D8" --release --min-api 26 --lib "$ANDROID_JAR" --output "$BUILD" @"$BUILD/classes.txt"

if [ ! -f "$TTY_KEY" ]; then
  echo "==> ключ не найден, генерирую $TTY_KEY"
  python3 "$ROOT/tools/ttydyn.py" keygen --out "${TTY_KEY%.key.pem}"
fi

echo "==> pack"
python3 "$ROOT/tools/ttydyn.py" pack \
  --dex "$BUILD/classes.dex" \
  --out "$OUT/profile-flex.ttydyn" \
  --variant release \
  --payload-type full \
  --key "$TTY_KEY"

python3 "$ROOT/tools/ttydyn.py" inspect "$OUT/profile-flex.ttydyn"
