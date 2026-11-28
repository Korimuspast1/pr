#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Smoke-тест native messaging хоста без браузера:
запускает host.py как подпроцесс и гоняет по нему протокол (4 байта длины + JSON).

Запуск:  python3 selftest.py
"""
import json
import os
import select
import struct
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    proc = subprocess.Popen(
        [sys.executable, os.path.join(HERE, "host.py")],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
    )

    def send(msg):
        data = json.dumps(msg).encode("utf-8")
        proc.stdin.write(struct.pack("<I", len(data)) + data)
        proc.stdin.flush()

    def recv(timeout=15):
        fd = proc.stdout.fileno()
        r, _, _ = select.select([fd], [], [], timeout)
        if not r:
            raise TimeoutError("host не ответил за %s сек" % timeout)
        hdr = b""
        while len(hdr) < 4:
            chunk = os.read(fd, 4 - len(hdr))
            if not chunk:
                return None
            hdr += chunk
        (n,) = struct.unpack("<I", hdr)
        data = b""
        while len(data) < n:
            chunk = os.read(fd, n - len(data))
            if not chunk:
                break
            data += chunk
        return json.loads(data.decode("utf-8"))

    def run_command(cid, command, timeout=15):
        send({"kind": "run", "id": cid, "command": command})
        out = []
        deadline = time.time() + timeout
        while time.time() < deadline:
            msg = recv(timeout=max(1, deadline - time.time()))
            if msg is None:
                break
            if msg.get("kind") in ("out", "err"):
                out.append(msg.get("data", ""))
            elif msg.get("kind") == "done":
                return "".join(out), msg
        raise TimeoutError("команда %r не завершилась" % command)

    failures = []

    def check(name, cond, extra=""):
        status = "PASS" if cond else "FAIL"
        print("[%s] %s %s" % (status, name, extra))
        if not cond:
            failures.append(name)

    ready = recv(10)
    check("ready", ready and ready.get("kind") == "ready", str(ready)[:120])

    out, done = run_command(1, "echo hello-terminal")
    check("echo", "hello-terminal" in out and done["code"] == 0, repr(out[:80]))

    out, done = run_command(2, "cd /tmp")
    check("cd /tmp", done["code"] == 0 and done["cwd"].replace("\\", "/").endswith("/tmp"),
          "cwd=%s" % done.get("cwd"))

    out, done = run_command(3, "pwd")
    check("pwd после cd", "/tmp" in out.replace("\\", "/") and done["code"] == 0, repr(out[:80]))

    out, done = run_command(4, "ls /definitely/not/exists-12345")
    check("код ошибки", done["code"] != 0 and "No such" in out,
          "code=%s out=%r" % (done["code"], out[-60:]))

    out, done = run_command(5, "printf 'a\\nb\\n'")
    check("multiline", "a\nb" in out, repr(out))

    out, done = run_command(7, "echo visible && ls /definitely/not/exists-12345")
    check("маркер не утекает в вывод", "__BT_" not in out and "visible" in out, repr(out[-80:]))

    # interrupt: длинная команда должна умереть по Ctrl+C
    send({"kind": "run", "id": 6, "command": "sleep 60"})
    time.sleep(0.5)
    send({"kind": "interrupt"})
    msg = recv(10)
    while msg and msg.get("kind") != "done":
        msg = recv(10)
    check("interrupt убивает команду", msg and msg.get("kind") == "done" and msg.get("id") == 6,
          str(msg))

    proc.stdin.close()
    try:
        proc.wait(timeout=5)
    except Exception:
        proc.kill()

    if failures:
        print("\nИТОГ: FAIL (%s)" % ", ".join(failures))
        return 1
    print("\nИТОГ: все тесты прошли (PASS)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
