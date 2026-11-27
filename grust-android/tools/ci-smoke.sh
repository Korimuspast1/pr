#!/usr/bin/env bash
# Drives the app on an emulator and keeps what it saw.
#
# Run by the "Emulator smoke test" job in .github/workflows/grust-android.yml:
#   bash tools/ci-smoke.sh <path to debug apk> <output directory>
#
# It is a smoke test, not a UI test suite: the point is to prove that the release APK starts, that
# every screen can be reached, and that nothing crashes on the way. The screenshots it leaves behind
# are the only way to look at the app without a phone, so they are what the job publishes.
#
# Elements are found by their text through uiautomator rather than by fixed coordinates: Compose
# exposes its semantics to accessibility, so "Побыть с ней" is a tappable node with bounds, and the
# script does not have to guess where a button is on a screen it has never seen.

set -uo pipefail

apk="${1:?usage: ci-smoke.sh <apk> <out dir>}"
out="${2:?usage: ci-smoke.sh <apk> <out dir>}"
mkdir -p "$out"

failures=0
fail() {
    echo "::error title=Smoke::$*"
    failures=$((failures + 1))
}
note() { echo "::notice title=Smoke::$*"; }

adb wait-for-device
# sys.boot_completed turns 1 only after the launcher is up, which is what the taps need.
for _ in $(seq 1 180); do
    [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = "1" ] && break
    sleep 2
done
adb shell input keyevent 82 >/dev/null 2>&1 || true   # dismiss the lock screen
sleep 2

adb logcat -c || true
adb install -r "$apk" || {
    fail "the APK did not install"
    exit 1
}
note "installed $(basename "$apk")"

# The debug variant carries a different application id (com.zalchik.grust.debug) but the same
# activity class, so the component is spelled out rather than derived from the file name.
adb shell am start -W -n com.zalchik.grust.debug/com.zalchik.grust.ui.MainActivity >/dev/null ||
    fail "the launcher activity did not start"
sleep 4

shot() {
    adb exec-out screencap -p >"$out/$1.png" 2>/dev/null
    if [ -s "$out/$1.png" ]; then
        note "screenshot $1.png ($(du -h "$out/$1.png" | cut -f1))"
    else
        fail "no screenshot $1.png"
    fi
}

# Center of the first node whose text contains $1, as "x y".
center_of() {
    local wanted="$1"
    adb shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1
    adb pull /sdcard/ui.xml /tmp/ui.xml >/dev/null 2>&1 || return 1
    python3 - "$wanted" <<'PY'
import re
import sys

wanted = sys.argv[1]
try:
    xml = open("/tmp/ui.xml", encoding="utf-8", errors="replace").read()
except OSError:
    sys.exit(1)

# text="..." ... bounds="[x1,y1][x2,y2]" - the two attributes are not always adjacent.
for node in re.finditer(r"<node\b[^>]*>", xml):
    tag = node.group(0)
    found = re.search(r'text="([^"]*)"', tag)
    bounds = re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', tag)
    if not found or not bounds:
        continue
    if wanted.lower() not in found.group(1).lower():
        continue
    x1, y1, x2, y2 = (int(bounds.group(i)) for i in range(1, 5))
    print(f"{(x1 + x2) // 2} {(y1 + y2) // 2}")
    sys.exit(0)
sys.exit(1)
PY
}

tap() {
    local label="$1"
    local point
    if ! point=$(center_of "$label"); then
        fail "no element with text \"$label\" on screen"
        return 1
    fi
    # shellcheck disable=SC2086
    adb shell input tap $point >/dev/null
    sleep 2
}

shot 01-entry

if tap "Побыть с ней"; then
    shot 02-today
    tap "Грустно" && shot 03-mood
    tap "Сохранить день" && shot 04-saved
fi

if tap "Статистика"; then shot 05-stats; fi
if tap "Дыхание"; then
    tap "Начать" || true
    sleep 3
    shot 06-breathe
fi
if tap "Дневник"; then shot 07-diary; fi
if tap "Сегодня"; then shot 08-today-again; fi

# A crash is the one thing a smoke test must not miss, and it does not always close the app.
if adb logcat -d 2>/dev/null | grep -E "FATAL EXCEPTION|AndroidRuntime.*E " | head -20 >/tmp/crash.log; then
    if [ -s /tmp/crash.log ]; then
        cat /tmp/crash.log
        fail "the app threw in the log"
    fi
fi

# The activity must still be the one in front after all the taps.
if ! adb shell dumpsys activity activities 2>/dev/null | grep -q "com.zalchik.grust.debug"; then
    fail "the app is not in the foreground at the end of the run"
fi

if [ "$failures" -ne 0 ]; then
    echo "::error title=Smoke::$failures problem(s) during the run"
    exit 1
fi
note "every screen was reached and nothing crashed"
