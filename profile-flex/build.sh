#!/usr/bin/env bash
# Сборка модуля profile-flex в .ttydyn.
#
# Требуется:
#   * JDK 17+                      — javac
#   * Android SDK: android.jar и d8
#   * python3                      — упаковка и подпись (tools/ttydyn.py)
#
# Переменные окружения:
#   ANDROID_HOME          путь к SDK (по умолчанию ~/Android/Sdk)
#   ANDROID_JAR           путь к android.jar (если нестандартный)
#   D8                    путь к d8
#   TTY_KEY               приватный ключ (по умолчанию keys/dev.key.pem)
#   TTY_PUB               соответствующий публичный ключ
#   TTY_GENERATE_KEY=1    создать новую dev-пару, если приватного ключа нет
#   TTY_VERSION            dynamicVersion (по умолчанию timestamp последнего коммита)
#   TTY_STABLE_API_VERSION stableApiVersion (по умолчанию 1788704106)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
BUILD="$HERE/build"
OUT="$ROOT/release"

ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
TTY_KEY="${TTY_KEY:-$ROOT/keys/dev.key.pem}"
TTY_PUB="${TTY_PUB:-${TTY_KEY%.key.pem}.pub.pem}"
TTY_GENERATE_KEY="${TTY_GENERATE_KEY:-0}"
TTY_STABLE_API_VERSION="${TTY_STABLE_API_VERSION:-1788704106}"

find_android_jar() {
  if [ -n "${ANDROID_JAR:-}" ]; then
    printf '%s\n' "$ANDROID_JAR"
    return
  fi
  find "$ANDROID_HOME/platforms" -mindepth 2 -maxdepth 2 -type f -name android.jar 2>/dev/null \
    | sort -V | tail -n 1 || true
}

find_d8() {
  if [ -n "${D8:-}" ]; then
    printf '%s\n' "$D8"
    return
  fi
  find "$ANDROID_HOME/build-tools" -mindepth 2 -maxdepth 2 -type f -name d8 2>/dev/null \
    | sort -V | tail -n 1 || true
}

ANDROID_JAR="$(find_android_jar)"
D8="$(find_d8)"

if [ -z "$ANDROID_JAR" ] || [ ! -f "$ANDROID_JAR" ]; then
  echo "ERROR: android.jar не найден. Укажите ANDROID_JAR=/path/to/android.jar" >&2
  exit 1
fi
if [ -z "$D8" ] || [ ! -f "$D8" ]; then
  echo "ERROR: d8 не найден. Укажите D8=/path/to/d8" >&2
  exit 1
fi
command -v javac >/dev/null 2>&1 || { echo "ERROR: javac не найден (нужен JDK 17+)" >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "ERROR: python3 не найден" >&2; exit 1; }

# Не создаём новую пару молча: пакет, подписанный новым ключом, не примет APK,
# в который зашит старый публичный ключ. CI явно передаёт TTY_GENERATE_KEY=1.
if [ ! -f "$TTY_KEY" ]; then
  if [ "$TTY_GENERATE_KEY" = "1" ]; then
    echo "==> генерирую новую dev-пару ключей: ${TTY_KEY%.key.pem}"
    rm -f "$TTY_KEY" "$TTY_PUB"
    python3 "$ROOT/tools/ttydyn.py" keygen --out "${TTY_KEY%.key.pem}"
  else
    echo "ERROR: приватный ключ не найден: $TTY_KEY" >&2
    echo "Для локальной dev-сборки: python3 tools/ttydyn.py keygen --out ${TTY_KEY%.key.pem}" >&2
    echo "Для одноразовой пары: TTY_GENERATE_KEY=1 ./profile-flex/build.sh" >&2
    exit 1
  fi
fi
if [ ! -f "$TTY_PUB" ]; then
  echo "ERROR: публичный ключ не найден: $TTY_PUB" >&2
  echo "Он должен быть парой к $TTY_KEY и должен быть зашит в APK мода" >&2
  exit 1
fi
python3 "$ROOT/tools/ttydyn.py" keycheck --key "$TTY_KEY" --pub "$TTY_PUB"

rm -rf "$BUILD"
mkdir -p "$BUILD/classes" "$OUT"

echo "==> javac"
find "$HERE/src" "$HERE/stubs" -type f -name '*.java' -print | sort > "$BUILD/sources.txt"
javac -source 8 -target 8 -nowarn -encoding UTF-8 \
      -bootclasspath "$ANDROID_JAR" \
      -d "$BUILD/classes" \
      @"$BUILD/sources.txt"

# Заглушки com.windukk.* нужны только javac. Реальные классы предоставляет APK мода.
echo "==> удаляю stub-классы"
rm -rf "$BUILD/classes/com/windukk/hook" "$BUILD/classes/com/windukk/stable"

find "$BUILD/classes" -type f -name '*.class' -print | sort > "$BUILD/classes.txt"
[ -s "$BUILD/classes.txt" ] || { echo "ERROR: javac не создал .class-файлы" >&2; exit 1; }

echo "==> d8"
"$D8" --release --min-api 26 --lib "$ANDROID_JAR" --output "$BUILD" @"$BUILD/classes.txt"
[ -s "$BUILD/classes.dex" ] || { echo "ERROR: d8 не создал classes.dex" >&2; exit 1; }

if [ -n "${TTY_VERSION:-}" ]; then
  VERSION="$TTY_VERSION"
else
  VERSION="$(git -C "$ROOT" log -1 --format=%ct 2>/dev/null || true)"
  VERSION="${VERSION:-$(date +%s)}"
fi

# payloadType=full совместим с загрузчиком TikTok You, но заменяет весь dynamic dex.
echo "==> pack"
python3 "$ROOT/tools/ttydyn.py" pack \
  --dex "$BUILD/classes.dex" \
  --out "$OUT/profile-flex.ttydyn" \
  --variant release \
  --payload-type full \
  --dynamic-version "$VERSION" \
  --stable-api-version "$TTY_STABLE_API_VERSION" \
  --key "$TTY_KEY"

python3 "$ROOT/tools/ttydyn.py" verify "$OUT/profile-flex.ttydyn" --pub "$TTY_PUB"
python3 "$ROOT/tools/ttydyn.py" inspect "$OUT/profile-flex.ttydyn"
