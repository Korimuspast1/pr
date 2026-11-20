#!/bin/bash
# Smoke Bottle Flip 3D: установка → запуск → тапы (бутылка прыгает) →
# скриншоты → проверка отсутствия краша.
set -uo pipefail

API="${1:-unknown}"
PKG="lav.bottleflip.alav"
APK="${SMOKE_APK:?SMOKE_APK not set}"

echo "=== bottleflip smoke (API $API) ==="
adb wait-for-device
adb shell input keyevent 82 || true

echo "== install =="
adb install -r "$APK" || { echo "SMOKE_FAILED: install"; exit 1; }

adb logcat -c || true

echo "== launch =="
adb shell am start -n "$PKG/.MainActivity" || { echo "SMOKE_FAILED: launch"; exit 1; }
sleep 6

shot() {
  local name="$1"
  adb exec-out screencap -p > "/tmp/$name.png" 2>/dev/null || true
  if [ -s "/tmp/$name.png" ]; then
    echo "SHOT:$name ($(stat -c%s /tmp/$name.png) bytes)"
  fi
}
shot "bottleflip-start"

echo "== taps: прыжки бутылки =="
for i in 1 2 3 4 5; do
  adb shell input tap 700 500
  sleep 1
  adb shell input tap 700 500
  sleep 2
done
shot "bottleflip-game"

echo "== GL/FATAL check =="
if adb logcat -d | grep -E "FATAL EXCEPTION.*$PKG" >/dev/null; then
  adb logcat -d | grep -A8 "FATAL EXCEPTION" | head -30
  echo "SMOKE_FAILED: crash"
  exit 1
fi
# игра должна быть на переднем плане
FOCUS=$(adb shell dumpsys window 2>/dev/null | grep -i "mCurrentFocus" | head -1)
echo "focus: $FOCUS"
echo "$FOCUS" | grep -q "$PKG" || echo "warn: app not in focus dump"

echo "SMOKE_PASSED"
