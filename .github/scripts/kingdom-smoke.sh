#!/bin/bash
# Smoke Kingdom Crash: установка → запуск → меню → новая игра → карта →
# скриншоты → проверка отсутствия краша.
set -uo pipefail

API="${1:-unknown}"
PKG="com.kingdomcrash.game"
APK="${SMOKE_APK:?SMOKE_APK not set}"

echo "=== kingdom smoke (API $API) ==="
adb wait-for-device
adb shell input keyevent 82 || true

echo "== install =="
adb install -r "$APK" || { echo "SMOKE_FAILED: install"; exit 1; }

adb logcat -c || true

# на слабом эмуляторе системный процесс периодически «зависает» — диалог закрываем кнопкой «Подождать»
waiter() {
  while :; do
    F=$(adb shell dumpsys window 2>/dev/null | grep -i "mCurrentFocus" | head -1)
    case "$F" in
      *Not?Responding*) adb shell input keyevent KEYCODE_DPAD_RIGHT; sleep 0.3; adb shell input keyevent KEYCODE_DPAD_CENTER;;
      # «Полноэкранный режим»: жмём и «Понятно», и назад — что-нибудь да сработает
      *ImmersiveModeConfirmation*) adb shell input keyevent KEYCODE_BACK; sleep 0.4; adb shell input tap 705 415; sleep 0.4; adb shell input keyevent KEYCODE_BACK;;
    esac
    sleep 2
  done
}
waiter & WPID=$!
trap 'kill $WPID 2>/dev/null' EXIT

echo "== launch =="
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1 \
  || adb shell am start -n "$PKG/.MainActivity" || { echo "SMOKE_FAILED: launch"; exit 1; }
sleep 10

shot() {
  local name="$1"
  adb exec-out screencap -p > "/tmp/$name.png" 2>/dev/null || true
  if [ -s "/tmp/$name.png" ]; then
    echo "SHOT:$name ($(stat -c%s /tmp/$name.png) bytes)"
    echo "SHOTB64_BEGIN:$name"
    base64 -w0 "/tmp/$name.png"
    echo ""
    echo "SHOTB64_END:$name"
  fi
}
shot "kingdom-menu"

WM_SIZE=$(adb shell wm size | grep -oE '[0-9]+x[0-9]+' | tail -1)
W=${WM_SIZE%x*}; H=${WM_SIZE#*x}
echo "display: ${W}x${H}"

echo "== ждём, пока игра вернёт фокус (диалоги закроет waiter) =="
for i in $(seq 1 10); do
  F=$(adb shell dumpsys window 2>/dev/null | grep -i "mCurrentFocus" | head -1)
  case "$F" in *$PKG*) break;; *) sleep 2;; esac
done
shot "kingdom-menu2"
# «НОВАЯ ИГРА» — большая кнопка в нижней части меню
adb shell input tap $((W/2)) $((H*68/100))
sleep 3
shot "kingdom-create"

echo "== start =="
# «НАЧАТЬ ИГРУ» — внизу экрана создания
adb shell input tap $((W/2)) $((H*84/100))
sleep 14
shot "kingdom-game"

echo "== zoom/pan на карте (свайп) =="
adb shell input swipe $((W/2)) $((H*40/100)) $((W/2)) $((H*60/100)) 400
sleep 2
shot "kingdom-map"

echo "== FATAL check =="
if adb logcat -d | grep -E "FATAL EXCEPTION.*$PKG" >/dev/null; then
  adb logcat -d | grep -A8 "FATAL EXCEPTION" | head -30
  echo "SMOKE_FAILED: crash"
  exit 1
fi
FOCUS=$(adb shell dumpsys window 2>/dev/null | grep -i "mCurrentFocus" | head -1)
echo "focus: $FOCUS"
echo "$FOCUS" | grep -q "$PKG" || echo "warn: app not in focus dump"

echo "SMOKE_PASSED"
