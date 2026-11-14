#!/usr/bin/env bash
# Установка native messaging хоста для Browser Terminal (Linux / macOS).
#
# Использование:
#   ./install.sh <ID_РАСШИРЕНИЯ> [moz-extension-UUID]
#
#   ID_РАСШИРЕНИЯ берётся на странице chrome://extensions (режим разработчика,
#   после «Загрузить распакованное расширение» у расширения появляется поле ID).
#   Второй аргумент (необязательный) — внутренний UUID расширения в Firefox
#   (about:debugging -> This Firefox -> у расширения поле «Внутренний UUID»).
set -euo pipefail

EXT_ID="${1:-}"
FF_ID="${2:-}"
if [ -z "$EXT_ID" ]; then
  echo "Ошибка: не указан ID расширения."
  echo "Использование: ./install.sh <ID_РАСШИРЕНИЯ> [moz-extension-UUID для Firefox]"
  echo "ID расширения: chrome://extensions -> режим разработчика -> поле ID у расширения."
  exit 1
fi

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOST_WRAPPER="$DIR/host/run-host.sh"
TEMPLATE="$DIR/host/com.korimuspast.browserterminal.json"
HOST_NAME="com.korimuspast.browserterminal"

chmod +x "$HOST_WRAPPER" "$DIR/host/host.py" 2>/dev/null || true

# Экранируем путь для JSON (на всякий случай: \ -> \\)
esc() { printf '%s' "$1" | sed 's/\\/\\\\/g'; }

make_manifest() { # $1 = выходной файл, $2 = origin (chrome-extension://ID/ или moz-extension://UUID/)
  sed -e "s|__HOST_ABS_PATH__|$(esc "$HOST_WRAPPER")|g" \
      -e "s|__EXTENSION_ID__|$EXT_ID|g" \
      "$TEMPLATE" > "$1"
  # для Firefox origin другой — подменим, если надо
  if [ "$2" != "chrome-extension://$EXT_ID/" ]; then
    sed -i.bak -e "s|chrome-extension://$EXT_ID/|$2|" "$1" && rm -f "$1.bak"
  fi
}

installed=0

case "$(uname -s)" in
  Darwin)
    BASE="$HOME/Library/Application Support"
    CANDIDATES=(
      "Google/Chrome"
      "Chromium"
      "BraveSoftware/Brave-Browser"
      "Microsoft Edge"
      "Vivaldi"
      "Opera"
    )
    ;;
  *)
    BASE="${XDG_CONFIG_HOME:-$HOME/.config}"
    CANDIDATES=(
      "google-chrome"
      "chromium"
      "chromium-browser"
      "BraveSoftware/Brave-Browser"
      "microsoft-edge"
      "vivaldi"
      "Opera"
      "thorium-browser"
    )
    ;;
esac

for c in "${CANDIDATES[@]}"; do
  cfg="$BASE/$c"
  if [ -d "$cfg" ]; then
    mkdir -p "$cfg/NativeMessagingHosts"
    make_manifest "$cfg/NativeMessagingHosts/$HOST_NAME.json" "chrome-extension://$EXT_ID/"
    echo "OK  $cfg/NativeMessagingHosts/$HOST_NAME.json"
    installed=$((installed + 1))
  fi
done

# Firefox (если дан UUID): ~/.mozilla/native-messaging-hosts
if [ -n "$FF_ID" ]; then
  if [ "$(uname -s)" = "Darwin" ]; then
    FF_DIR="$HOME/Library/Application Support/Mozilla/NativeMessagingHosts"
  else
    FF_DIR="$HOME/.mozilla/native-messaging-hosts"
  fi
  mkdir -p "$FF_DIR"
  make_manifest "$FF_DIR/$HOST_NAME.json" "moz-extension://$FF_ID/"
  echo "OK  $FF_DIR/$HOST_NAME.json (Firefox)"
  installed=$((installed + 1))
fi

if [ "$installed" -eq 0 ]; then
  echo "Не найдено ни одной папки конфигурации браузера в $BASE."
  echo "Установите Chrome/Chromium/Brave/Edge/Vivaldi или создайте папку вручную:"
  echo "  mkdir -p \"$BASE/google-chrome/NativeMessagingHosts\" и запустите скрипт снова."
  exit 1
fi

echo
echo "Готово! Хост зарегистрирован для $installed профиля(ей) браузера."
echo "Теперь откройте расширение кликом по его иконке — должна открыться вкладка терминала."
echo "Проверить хост без браузера: python3 host/selftest.py"
