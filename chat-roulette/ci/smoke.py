#!/usr/bin/env python3
"""Emulator smoke test for Glass Roulette: installs the APK, passes the 18+ gate,
walks through main screens, reports crashes + tiny screenshots as GitHub check runs."""
import base64, io, os, re, subprocess, sys, time
from PIL import Image

PKG = "m.chatrulet.m"
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
    os.makedirs("screenshots", exist_ok=True)
    im.save(f"screenshots/{API}-{name}.png")
    w = 360
    im2 = im.resize((w, int(im.height * w / im.width)), Image.LANCZOS)
    q = 70
    while True:
        buf = io.BytesIO()
        im2.save(buf, "JPEG", quality=q)
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


def tap_any(*texts):
    for t in texts:
        if tap_text(t):
            return True
    return False


def crashed():
    log = sh("adb logcat -d -b crash")
    if "FATAL EXCEPTION" in log or "AndroidRuntime" in log:
        return log
    return None


def back_in_app():
    """Возврат внутри приложения по стеклянной кнопке-стрелке; если не вышло — перезапуск."""
    if not tap_any("Back", "Назад"):
        sh(f"adb shell am start -W -n {PKG}/m.chatrulet.m.MainActivity")
        time.sleep(5)


def main():
    sh("adb logcat -c")
    out = sh("adb install -r GlassRoulette.apk")
    print(out)
    sh(f"adb shell am start -W -n {PKG}/m.chatrulet.m.MainActivity")
    time.sleep(12)
    shot("1-gate")
    tap_any("I am 18 or older", "Мне есть 18")
    time.sleep(2)
    shot("2-home")
    # settings
    tap_any("Settings", "Настройки")
    shot("3-settings")
    sh("adb shell input swipe 500 1600 500 400 300")
    time.sleep(1.5)
    shot("4-settings-bottom")
    tap_any("Dark", "Тёмная")
    time.sleep(2)
    shot("5-settings-dark")
    back_in_app()
    shot("6-home-dark")
    # rules
    tap_any("Rules", "Правила")
    shot("7-rules")
    back_in_app()
    # blocked list
    tap_any("Blocked", "Заблокированные")
    shot("8-blocked")
    back_in_app()
    shot("9-home-final")
    c = crashed()
    pid = sh(f"adb shell pidof {PKG}").strip()
    if c:
        print("::error title=crash-" + API + "::" + esc(c[:30000]))
        sys.exit(1)
    print(f"::notice title=ok-{API}::no crash, pid={pid}, missing taps={MISSING}")


if __name__ == "__main__":
    main()
