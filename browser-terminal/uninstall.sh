#!/usr/bin/env bash
# Удаление регистрации native messaging хоста Browser Terminal.
set -uo pipefail

HOST_NAME="com.korimuspast.browserterminal"
removed=0

case "$(uname -s)" in
  Darwin)
    BASE="$HOME/Library/Application Support"
    CANDIDATES=("Google/Chrome" "Chromium" "BraveSoftware/Brave-Browser" "Microsoft Edge" "Vivaldi" "Opera")
    FF_DIR="$HOME/Library/Application Support/Mozilla/NativeMessagingHosts"
    ;;
  *)
    BASE="${XDG_CONFIG_HOME:-$HOME/.config}"
    CANDIDATES=("google-chrome" "chromium" "chromium-browser" "BraveSoftware/Brave-Browser" "microsoft-edge" "vivaldi" "Opera" "thorium-browser")
    FF_DIR="$HOME/.mozilla/native-messaging-hosts"
    ;;
esac

for c in "${CANDIDATES[@]}"; do
  f="$BASE/$c/NativeMessagingHosts/$HOST_NAME.json"
  if [ -f "$f" ]; then
    rm -f "$f"
    echo "удалён $f"
    removed=$((removed + 1))
  fi
done
if [ -f "$FF_DIR/$HOST_NAME.json" ]; then
  rm -f "$FF_DIR/$HOST_NAME.json"
  echo "удалён $FF_DIR/$HOST_NAME.json"
  removed=$((removed + 1))
fi

[ "$removed" -eq 0 ] && echo "Ничего не найдено (хост, похоже, не был зарегистрирован)."
echo "Готово. Само расширение удаляется на странице chrome://extensions."
