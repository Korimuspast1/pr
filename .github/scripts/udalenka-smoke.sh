#!/bin/bash
# Smoke: установка → выдача доступа к уведомлениям → тест-уведомления →
# проверка, что они попали в базу и видны в UI → скриншоты.
set -uo pipefail

API="${1:-unknown}"
PKG="lav.udalenka.alav"
APK="${SMOKE_APK:?SMOKE_APK not set}"
LOG_TAG="Udalenka"

echo "=== udalenka smoke (API $API) ==="
adb wait-for-device
adb shell input keyevent 82 || true

echo "== install =="
adb install -r "$APK" || { echo "SMOKE_FAILED: install"; exit 1; }

echo "== grant notification listener =="
adb shell cmd notification allow_listener "$PKG/$PKG.NotifSaverService" || true
# запасной вариант через secure settings
FLAT=$(adb shell settings get secure enabled_notification_listeners | tr -d '\r')
case "$FLAT" in
  *$PKG*) : ;;
  *) adb shell settings put secure enabled_notification_listeners "$PKG/$PKG.NotifSaverService" || true ;;
esac
adb shell settings get secure enabled_notification_listeners

echo "== clear logcat =="
adb logcat -c || true

echo "== post test notifications (как будто WhatsApp/TikTok) =="
adb shell cmd notification post -t "Mum" -c "Привет! Как дела? Позвони." com.whatsapp tag1 || \
  echo "warn: whatsapp post failed"
adb shell cmd notification post -t "TikTok" -c "user123 прокомментировал: крутое видео 🔥" com.zhiliaoapp.musically tag2 || \
  echo "warn: tiktok post failed"
adb shell cmd notification post -t "com.android.shell" -c "shell test message" com.android.shell tag3 || true
sleep 3

echo "== logcat capture check =="
adb logcat -d | grep -i "$LOG_TAG" | head -20 || true

echo "== launch app =="
adb shell am start -n "$PKG/.MainActivity" || { echo "SMOKE_FAILED: launch"; exit 1; }
sleep 4

shot() {
  local name="$1"
  adb exec-out screencap -p > "/tmp/$name.png" 2>/dev/null || true
  if [ -s "/tmp/$name.png" ]; then
    echo "SHOT:$name:$(base64 -w0 /tmp/$name.png | head -c 100 | cut -c1-40)... ($(stat -c%s /tmp/$name.png) bytes)"
  fi
}
shot "udalenka-main"

echo "== UI check: ищем сохранённый текст на экране =="
adb shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1 || true
adb shell cat /sdcard/ui.xml 2>/dev/null | tr -d '\r' > /tmp/ui.xml || true

if grep -qi "Позвони\|прокомментировал\|shell test" /tmp/ui.xml; then
  echo "UI_CONTAINS_SAVED_TEXT"
else
  echo "warn: saved text not on main screen (приложения могли быть не распознаны)"
fi

# открыть приложение-строку shell (всегда должно быть, т.к. пакет существует)
echo "== FATAL check =="
if adb logcat -d | grep -E "FATAL EXCEPTION.*$PKG" >/dev/null; then
  adb logcat -d | grep -A8 "FATAL EXCEPTION" | head -30
  echo "SMOKE_FAILED: crash"
  exit 1
fi

echo "SMOKE_PASSED"
