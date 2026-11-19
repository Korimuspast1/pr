#!/usr/bin/env python3
"""Emulator smoke test: installs the APK, walks through main screens, reports crashes + tiny screenshots
as GitHub annotations (so they can be inspected without downloading artifacts)."""
import base64, io, os, re, subprocess, sys, time
from PIL import Image

PKG = "com.glass.activitylauncher"
MISSING = []
API = os.environ.get("API", "?")


def sh(cmd, check=False):
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if check and r.returncode != 0:
        print(r.stdout, r.stderr)
    return r.stdout


def esc(s):
    return s.replace('%', '%25').replace('\r', '').replace('\n', '%0A')


def shot(name, delay=1.5):
    time.sleep(delay)
    sh("adb shell screencap -p /sdcard/s.png")
    sh("adb pull /sdcard/s.png /tmp/s.png")
    try:
        im = Image.open("/tmp/s.png").convert("RGB")
    except Exception as e:
        print("screenshot failed", e)
        return
    w = 360
    im = im.resize((w, int(im.height * w / im.width)), Image.LANCZOS)
    q = 70
    while True:
        buf = io.BytesIO()
        im.save(buf, "JPEG", quality=q)
        b = base64.b64encode(buf.getvalue()).decode()
        if len(b) < 64000 or q <= 20:
            break
        q -= 10
    publish(f"shot-{API}-{name}", b)


def publish(name, text):
    """Store data in a check run (readable through the REST API)."""
    import json, urllib.request
    token = os.environ.get("GH_TOKEN")
    repo = os.environ.get("GITHUB_REPOSITORY")
    sha = os.environ.get("GITHUB_SHA")
    if not token:
        return
    body = json.dumps({"name": name, "head_sha": sha, "status": "completed", "conclusion": "neutral",
                       "output": {"title": name, "summary": "screenshot", "text": text}}).encode()
    req = urllib.request.Request(f"https://api.github.com/repos/{repo}/check-runs", data=body, method="POST",
                                 headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json"})
    try:
        urllib.request.urlopen(req).read()
    except Exception as e:
        print("publish failed", e)


def dump():
    sh("adb shell uiautomator dump /sdcard/ui.xml")
    return sh("adb shell cat /sdcard/ui.xml")


def tap_text(text, contains=False, index=0):
    for _ in range(3):
        xml = dump()
        nodes = []
        for tag in re.findall(r'<node [^>]*>', xml):
            b = re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', tag)
            if not b:
                continue
            for attr in ("text", "content-desc"):
                m = re.search(attr + r'="([^"]*)"', tag)
                if m and m.group(1):
                    nodes.append((m.group(1),) + b.groups())
        hits = [n for n in nodes if (text in n[0] if contains else n[0] == text)]
        if len(hits) > index:
            t, x1, y1, x2, y2 = hits[index]
            x, y = (int(x1) + int(x2)) // 2, (int(y1) + int(y2)) // 2
            sh(f"adb shell input tap {x} {y}")
            time.sleep(2.5)
            return True
        time.sleep(2)
    print(f"tap: text not found: {text}")
    MISSING.append(text)
    return False


def find(text):
    xml = dump()
    for tag in re.findall(r'<node [^>]*>', xml):
        m = re.search(r'text="([^"]*)"', tag)
        b = re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', tag)
        if m and b and m.group(1) == text:
            x1, y1, x2, y2 = map(int, b.groups())
            return (x1 + x2) // 2, (y1 + y2) // 2
    return None


def drag_tabbar():
    a = find("All")
    r = find("Recents")
    if not a or not r:
        MISSING.append("tabbar"); return
    y = a[1] - 20
    # drag the droplet from "All" to "Recents" slowly and capture mid-drag
    p = subprocess.Popen(f"adb shell input swipe {a[0]} {y} {r[0]} {y} 3000", shell=True)
    time.sleep(1.3)
    shot("12-drag-mid", delay=0)
    p.wait()
    time.sleep(2)
    shot("13-drag-end")


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
    tap_text("package / name")
    sh("adb shell input text glass"); time.sleep(2)
    sh("adb shell input keyevent 111"); time.sleep(1)
    shot("6b-search")
    if tap_text("Glass Launcher"):
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
    tap_text("All")
    drag_tabbar()
    c = crashed()
    pid = sh(f"adb shell pidof {PKG}").strip()
    if c:
        print("::error title=crash-" + API + "::" + esc(c[:30000]))
        sys.exit(1)
    print(f"::notice title=ok-{API}::no crash, pid={pid}, missing taps={MISSING}")


if __name__ == "__main__":
    main()
