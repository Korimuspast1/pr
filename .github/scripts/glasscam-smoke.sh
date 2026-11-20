#!/bin/bash
# Smoke GlassCam: установка → разрешения → запуск → превью камеры →
# фото по кнопке → проверка файла в галерее → скриншоты → FATAL check.
set -uo pipefail

API="${1:-unknown}"
PKG="lav.glasscam.alav"
APK="${SMOKE_APK:?SMOKE_APK not set}"

echo "=== glasscam smoke (API $API) ==="
adb wait-for-device
adb shell input keyevent 82 || true

echo "== install =="
adb install -r "$APK" || { echo "SMOKE_FAILED: install"; exit 1; }

echo "== grant permissions =="
adb shell pm grant "$PKG" android.permission.CAMERA || true
adb shell pm grant "$PKG" android.permission.RECORD_AUDIO || true

adb logcat -c || true

echo "== launch =="
adb shell am start -n "$PKG/.MainActivity" || { echo "SMOKE_FAILED: launch"; exit 1; }
sleep 8

# если поверх висит ANR/системный диалог ("Wait") — закрываем его;
# если диалога нет, тап попадает в превью и лишь срабатывает автофокус
WM_SIZE=$(adb shell wm size | grep -oE '[0-9]+x[0-9]+' | tail -1)
W=${WM_SIZE%x*}; H=${WM_SIZE#*x}
echo "display: ${W}x${H}"
adb shell input tap $((W*58/100)) $((H*57/100))
sleep 2

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
shot "glasscam-preview"

echo "== tap shutter (photo) =="
# шторка правее центра (слева Space с весом), ~92% высоты
adb shell input tap $((W*64/100)) $((H*92/100))
sleep 5
shot "glasscam-photo"

echo "== check photo in gallery =="
PHOTOS=$(adb shell "ls /sdcard/Pictures/GlassCam/ 2>/dev/null" | tr -d '\r')
echo "Pictures/GlassCam: $PHOTOS"
if ! echo "$PHOTOS" | grep -q "\.jpg"; then
  echo "== diagnostics =="
  adb shell "content query --uri content://media/external/images/media --projection _display_name --where \"_display_name LIKE 'GlassCam%'\" 2>/dev/null" | head -5 || true
  adb logcat -d | grep -iE "glasscam|CameraController|CameraDevice|ImageReader" | tail -30 || true
  adb logcat -d | grep -iE " E /|Exception" | tail -20 || true
  echo "SMOKE_FAILED: no photo saved"
  exit 1
fi

echo "== switch to front camera =="
# кнопка смены камеры: правее шторки
adb shell input tap $((W*80/100)) $((H*92/100))
sleep 4
shot "glasscam-front"

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
