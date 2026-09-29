#!/usr/bin/env bash
# Clone, patch and build the MyTelegram Android client.
# Usage: ./build-client.sh 192.168.1.25
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
SOURCE="$HERE/source/mytelegram-android"
OUT="$HERE/build/apk"
REPO="https://github.com/loyldg/mytelegram-android.git"
BRANCH="master"
SERVER_IP="${1:-${MYTELEGRAM_IP:-}}"

if [ -z "$SERVER_IP" ]; then
  echo "Usage: $0 SERVER_IP" >&2
  echo "Example: $0 192.168.1.25" >&2
  exit 2
fi
if [[ "$SERVER_IP" == *[[:space:]/\\]* ]]; then
  echo "ERROR: invalid server IP: $SERVER_IP" >&2
  exit 1
fi
command -v git >/dev/null 2>&1 || { echo "ERROR: git не найден" >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "ERROR: python3 не найден" >&2; exit 1; }
command -v java >/dev/null 2>&1 || { echo "ERROR: JDK 17 не найден" >&2; exit 1; }

if [ ! -d "$SOURCE/.git" ]; then
  mkdir -p "$(dirname "$SOURCE")"
  git clone --depth 1 --branch "$BRANCH" "$REPO" "$SOURCE"
fi

export MYTELEGRAM_IP="$SERVER_IP"
python3 "$HERE/patch-client.py" "$SOURCE" "$SERVER_IP"

cd "$SOURCE"
chmod +x ./gradlew
# The upstream project needs Android SDK 35 and NDK 21.4.7075529.
./gradlew :TMessagesProj_App:assembleAfatRelease --no-daemon --stacktrace

rm -rf "$OUT"
mkdir -p "$OUT"
find "$SOURCE" -type f -path '*/build/outputs/apk/*/*.apk' -exec cp -v {} "$OUT/" \;
if ! find "$OUT" -type f -name '*.apk' -print -quit | grep -q .; then
  echo "ERROR: APK не найден после Gradle-сборки" >&2
  exit 1
fi

echo "Готовые APK: $OUT"
