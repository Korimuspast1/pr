#!/usr/bin/env python3
"""Emulator smoke test: installs the APK, walks through main screens, reports crashes + tiny screenshots
as GitHub annotations (so they can be inspected without downloading artifacts)."""
import base64, io, os, re, subprocess, sys, time
from PIL import Image

PKG = "com.glass.activitylauncher"
API = os.environ.get("API", "?")


def sh(cmd, check=False):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if check and r.returncode != 0:
        print(r.stdout, r.stderr)
    return r.stdout


def esc(s):
    return s.replace('%', '%25').replace('\r', '').replace('\n', '%0A')


def shot(name):
    raw = subprocess.run("adb exec-out screencap -p", shell=True, capture_output=True).stdout
    try:
        im = Image.open(io.BytesIO(raw)).convert("RGB")
    except Exception as e:
        print("screenshot failed", e)
        return
    w = 300
    im = im.resize((w, int(im.height * w / im.width)))
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=50)
    b = base64.b64encode(buf.getvalue()).decode()
    print(f"::notice title=shot-{API}-{name}::{b}")


def dump():
    sh("adb shell uiautomator dump /sdcard/ui.xml")
    return sh("adb shell cat /sdcard/ui.xml")


def tap_text(text, contains=False, index=0):
    for _ in range(3):
        xml = dump()
        nodes = re.findall(r'<node [^>]*?(?:text|content-desc)="([^"]*)"[^>]*?bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', xml)
        hits = [n for n in nodes if (text in n[0] if contains else n[0] == text)]
        if len(hits) > index:
            t, x1, y1, x2, y2 = hits[index]
            x, y = (int(x1) + int(x2)) // 2, (int(y1) + int(y2)) // 2
            sh(f"adb shell input tap {x} {y}")
            time.sleep(2.5)
            return True
        time.sleep(2)
    print(f"::warning title=tap-{API}::text not found: {text}")
    return False


def crashed():
    log = sh("adb logcat -d -b crash")
    if "FATAL EXCEPTION" in log or "AndroidRuntime" in log:
        return log
    return None


def main():
    sh("adb logcat -c")
    out = sh("adb install -r GlassActivityLauncher.apk")
    print(out)
    sh(f"adb shell am start -W -n {PKG}/com.glass.launcher.MainActivity")
    time.sleep(15)
    shot("1-apps")
    # open a package
    sh("adb shell input keyevent 4"); time.sleep(1)
    tap_text("Settings")
    shot("2-settings")
    sh("adb shell input swipe 500 1600 500 400 300"); time.sleep(1.5)
    shot("3-settings-bottom")
    tap_text("Dark")
    time.sleep(2)
    shot("4-dark-settings")
    sh("adb shell input keyevent 4"); time.sleep(2)
    shot("5-apps-dark")
    tap_text("Activities")
    time.sleep(10)
    shot("6-activities")
    tap_text("Apps")
    time.sleep(2)
    if tap_text("Glass Launcher") or tap_text("Settings", contains=True, index=1):
        shot("7-package")
        tap_text("MainActivity", contains=True)
        shot("8-editor")
        tap_text("Add to favorites")
        tap_text("Intent builder")
        shot("9-editor-intent")
        sh("adb shell input keyevent 4"); time.sleep(2)
        sh("adb shell input keyevent 4"); time.sleep(2)
    tap_text("Favorites")
    shot("10-favorites")
    tap_text("Recents")
    shot("11-recents")
    c = crashed()
    pid = sh(f"adb shell pidof {PKG}").strip()
    if c:
        print("::error title=crash-" + API + "::" + esc(c[:30000]))
        sys.exit(1)
    print(f"::notice title=ok-{API}::no crash, pid={pid}")


if __name__ == "__main__":
    main()
