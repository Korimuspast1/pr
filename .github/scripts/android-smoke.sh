#!/usr/bin/env bash
set +e

API_LEVEL="${1:-34}"
cd "$(dirname "$0")/../.." || exit 2
LOG="android-smoke-api${API_LEVEL}.txt"
: > "$LOG"

APK="$(find release -name '*.apk' | head -n 1)"
echo "API=$API_LEVEL APK=$APK" | tee -a "$LOG"
sha256sum "$APK" | tee -a "$LOG"

adb logcat -c
adb install -r "$APK" > install-output.txt 2>&1
INSTALL_STATUS=$?
cat install-output.txt | tee -a "$LOG"
echo "install_status=$INSTALL_STATUS" | tee -a "$LOG"
if [ "$INSTALL_STATUS" -ne 0 ]; then
  adb logcat -d -v threadtime >> "$LOG" 2>&1
  exit "$INSTALL_STATUS"
fi

adb shell pm grant lav.visaroom.alav android.permission.POST_NOTIFICATIONS >/dev/null 2>&1 || true

echo "--- launch AuthActivity ---" | tee -a "$LOG"
adb shell am start -W -n lav.visaroom.alav/.AuthActivity 2>&1 | tee -a "$LOG"
sleep 5
echo "pid_after_auth=$(adb shell pidof lav.visaroom.alav 2>/dev/null)" | tee -a "$LOG"
adb shell dumpsys activity activities 2>/dev/null | grep -A3 -B3 'lav.visaroom.alav' | head -n 100 >> "$LOG"

# Root shell on the emulator may open the non-exported activity; this
# exercises ChatsActivity inflation/setup with no signed-in session.
adb root >/dev/null 2>&1 || true
adb wait-for-device

echo "--- launch ChatsActivity ---" | tee -a "$LOG"
adb shell am start -W -n lav.visaroom.alav/.ChatsActivity 2>&1 | tee -a "$LOG"
sleep 10
PID="$(adb shell pidof lav.visaroom.alav 2>/dev/null | tr -d '\r')"
echo "pid_after_chats=$PID" | tee -a "$LOG"
adb shell dumpsys activity activities 2>/dev/null | grep -A5 -B5 'lav.visaroom.alav' | head -n 160 >> "$LOG"
adb logcat -d -v threadtime >> "$LOG" 2>&1

if [ -z "$PID" ]; then
  echo "SMOKE_FAILED: process is not running" | tee -a "$LOG"
  exit 1
fi
if grep -Eq 'FATAL EXCEPTION|AndroidRuntime:.*lav\.visaroom|Process: lav\.visaroom\.alav' "$LOG"; then
  echo "SMOKE_FAILED: fatal exception in logcat" | tee -a "$LOG"
  exit 1
fi

echo "SMOKE_PASSED" | tee -a "$LOG"
