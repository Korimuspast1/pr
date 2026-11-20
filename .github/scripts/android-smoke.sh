#!/usr/bin/env bash
set +e

API_LEVEL="${1:-35}"
MODE="${2:-${SMOKE_MODE:-release}}"
cd "$(dirname "$0")/../.." || exit 2
LOG="android-smoke-api${API_LEVEL}.txt"
# Preserve a preceding Gradle build log in the same file when CI has
# already created it; otherwise start with a fresh smoke log.
if [ ! -f "$LOG" ]; then
  : > "$LOG"
fi

if [ -n "${SMOKE_APK:-}" ]; then
  APK="$SMOKE_APK"
else
  APK="$(find release -name '*.apk' | head -n 1)"
fi

echo "--- emulator smoke ---" | tee -a "$LOG"
echo "API=$API_LEVEL MODE=$MODE APK=$APK" | tee -a "$LOG"
sha256sum "$APK" | tee -a "$LOG"

adb logcat -c
adb install -r "$APK" > install-output.txt 2>&1
INSTALL_STATUS=$?
cat install-output.txt | tee -a "$LOG"
echo "install_status=$INSTALL_STATUS" | tee -a "$LOG"
if [ "$INSTALL_STATUS" -ne 0 ]; then
  adb logcat -b crash -d -v threadtime >> "$LOG" 2>&1
  adb logcat -d -v threadtime >> "$LOG" 2>&1
  exit "$INSTALL_STATUS"
fi

# Start from the same local state as a clean install. Crucially, do not
# pre-grant POST_NOTIFICATIONS: the reported device crash happens before
# the chat list on a clean install, when that runtime permission has not
# yet been granted.
adb shell pm clear lav.visaroom.alav >/dev/null 2>&1 || true
adb shell pm revoke lav.visaroom.alav android.permission.POST_NOTIFICATIONS >/dev/null 2>&1 || true

APP_UID="$(adb shell cmd package list packages -U lav.visaroom.alav 2>/dev/null \
  | sed -n 's/.*uid://p' | tr -cd '0-9')"
echo "app_uid=$APP_UID" | tee -a "$LOG"

echo "--- package permissions before launch ---" >> "$LOG"
adb shell dumpsys package lav.visaroom.alav 2>/dev/null \
  | grep -A4 -B4 -E 'POST_NOTIFICATIONS|RECEIVE_BOOT_COMPLETED|grantedPermissions|runtime permissions' \
  | head -n 180 >> "$LOG"

if [ "$MODE" = "seeded" ]; then
  echo "--- launch debug SmokeSeedActivity (saves a test session, then opens ChatsActivity) ---" | tee -a "$LOG"
  adb shell am start -W -n lav.visaroom.alav/.SmokeSeedActivity 2>&1 | tee -a "$LOG"
  sleep 3
  EARLY_PID="$(adb shell pidof lav.visaroom.alav 2>/dev/null | tr -d '\r')"
  echo "pid_after_3s=$EARLY_PID" | tee -a "$LOG"
  echo "--- early app-UID log ---" >> "$LOG"
  if [ -n "$APP_UID" ]; then
    adb logcat -d --uid="$APP_UID" -v threadtime 2>&1 | tail -n 350 >> "$LOG"
  fi
  echo "--- early crash buffer ---" >> "$LOG"
  adb logcat -b crash -d -v threadtime 2>&1 | tail -n 350 >> "$LOG"
  sleep 17

  echo "--- force MessageCheckJobService job 1101 ---" >> "$LOG"
  adb shell cmd jobscheduler run -f lav.visaroom.alav 1101 2>&1 >> "$LOG"
  sleep 5
else
  echo "--- launch AuthActivity ---" | tee -a "$LOG"
  adb shell am start -W -n lav.visaroom.alav/.AuthActivity 2>&1 | tee -a "$LOG"
  sleep 5
  echo "pid_after_auth=$(adb shell pidof lav.visaroom.alav 2>/dev/null)" | tee -a "$LOG"
  adb shell dumpsys activity activities 2>/dev/null \
    | grep -A3 -B3 'lav.visaroom.alav' | head -n 100 >> "$LOG"

  # Root shell on the emulator may open the non-exported activity; this
  # exercises ChatsActivity inflation/setup with no signed-in session.
  adb root >/dev/null 2>&1 || true
  adb wait-for-device

  echo "--- launch ChatsActivity ---" | tee -a "$LOG"
  adb shell am start -W -n lav.visaroom.alav/.ChatsActivity 2>&1 | tee -a "$LOG"
  sleep 10
fi

PID="$(adb shell pidof lav.visaroom.alav 2>/dev/null | tr -d '\r')"
echo "pid_after_chats=$PID" | tee -a "$LOG"

echo "--- activity state ---" >> "$LOG"
adb shell dumpsys activity activities 2>/dev/null \
  | grep -A6 -B6 'lav.visaroom.alav' | head -n 220 >> "$LOG"

echo "--- focused window ---" >> "$LOG"
adb shell dumpsys window windows 2>/dev/null \
  | grep -E 'mCurrentFocus|mFocusedApp|lav\.visaroom\.alav|GrantPermissions|permissioncontroller' \
  | head -n 120 >> "$LOG"

echo "--- visible UI hierarchy (text/package excerpts) ---" >> "$LOG"
adb shell uiautomator dump /sdcard/visaroom-window.xml >/dev/null 2>&1 || true
adb shell cat /sdcard/visaroom-window.xml 2>/dev/null \
  | grep -oE 'text="[^"]*"|package="[^"]*"|content-desc="[^"]*"' \
  | head -n 160 >> "$LOG"

echo "--- JobScheduler state for visaroom ---" >> "$LOG"
adb shell dumpsys jobscheduler 2>/dev/null \
  | grep -A28 -B5 'lav\.visaroom\.alav' | head -n 260 >> "$LOG"

# Keep the raw log in a side file for extraction, but do not append the
# whole emulator log to the committed smoke log: Google/system services
# generate tens of thousands of lines during boot and previously pushed
# the app's own stack trace out of the tail that CI commits.
adb logcat -d -v threadtime > smoke-raw-logcat.txt 2>&1

echo "--- final app-UID log ---" >> "$LOG"
if [ -n "$APP_UID" ]; then
  adb logcat -d --uid="$APP_UID" -v threadtime 2>&1 | tail -n 600 >> "$LOG"
else
  echo "app UID unavailable" >> "$LOG"
fi

echo "--- final crash buffer ---" >> "$LOG"
adb logcat -b crash -d -v threadtime 2>&1 | tail -n 600 >> "$LOG"

# ActivityManager/JobScheduler lines are emitted under the system UID, so
# add a narrowly targeted extract from the raw log. Avoid generic words
# such as JobScheduler here: they match thousands of unrelated Google
# service lines during emulator boot.
{
  echo "--- filtered app-context extract ---"
  grep -n -A120 -B10 -E 'FATAL EXCEPTION|VisaroomSmoke|SmokeSeedActivity|ChatsActivity|MessageCheckJobService|lav\.visaroom\.alav|Process: lav\.visaroom' \
    smoke-raw-logcat.txt | tail -n 700 || true
} >> "$LOG"

if [ -z "$PID" ]; then
  echo "SMOKE_FAILED: process is not running" | tee -a "$LOG"
  exit 1
fi
if grep -Eq 'FATAL EXCEPTION|AndroidRuntime: Process: lav\.visaroom|Process: lav\.visaroom\.alav' "$LOG"; then
  echo "SMOKE_FAILED: fatal exception in logcat" | tee -a "$LOG"
  exit 1
fi

echo "SMOKE_PASSED" | tee -a "$LOG"
