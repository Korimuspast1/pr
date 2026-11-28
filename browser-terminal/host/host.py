#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Browser Terminal — native messaging host.

Общается с расширением по протоколу Chrome Native Messaging:
каждое сообщение = 4 байта длины (little-endian, uint32) + JSON (UTF-8).
Хост читает сообщения из stdin и пишет ответы в stdout.

Протокол (расширение -> хост):
  {"kind": "run",       "id": N, "command": "ls -la"}
  {"kind": "interrupt"}                       # прервать текущую команду (Ctrl+C)
  {"kind": "ping",      "id": N}

Протокол (хост -> расширение):
  {"kind": "ready", "pid", "cwd", "home", "user", "host", "shell", "platform"}
  {"kind": "out",   "id": N, "data": "..."}   # порция stdout (маркер обрезается)
  {"kind": "err",   "id": N, "data": "..."}   # порция stderr
  {"kind": "done",  "id": N, "code": 0, "cwd": "/текущая/папка"}
  {"kind": "error", "id": N, "message": "..."}
  {"kind": "pong",  "id": N}

Как сохраняется cwd между командами (чтобы cd/ls работали «как в настоящем терминале»):
каждая команда выполняется в новом подпроцессе shell, но в конце скрипта shell печатает
маркер со статусом и текущим pwd; хост читает маркер, обрезает его из вывода и обновляет cwd.

Безопасность:
  * Хост регистрируется в браузере с allowed_origins = ID этого расширения,
    поэтому подключиться к нему может ТОЛЬКО оно (сайты и чужие расширения — нет).
  * Расширение НЕ объявляет externally_connectable и не имеет content_scripts,
    поэтому веб-страницы не могут отправлять команды в терминал.
  * Команды выполняются с правами текущего пользователя — это настоящий терминал.
  * Опциональный белый список команд: переменная окружения
    BROWSER_TERMINAL_WHITELIST=/путь/к/файлу (по одному имени команды в строке,
    # — комментарий). Если задан — все остальные команды отклоняются.
"""

import getpass
import json
import os
import queue
import re
import secrets
import selectors
import signal
import socket
import struct
import subprocess
import sys
import threading

HOST_NAME = "com.korimuspast.browserterminal"
CHUNK = 32 * 1024          # размер порции вывода; лимит сообщения host->Chrome = 1 МБ
MAX_INCOMING = 4 * 1024 * 1024
HOLD_BACK = 4096           # сколько байтов stdout держим, пока не увидим маркер целиком


def log(msg):
    """Диагностика идёт в stderr (видно в chrome://extensions -> ошибки расширения)."""
    try:
        sys.stderr.write("[browser-terminal-host] %s\n" % msg)
        sys.stderr.flush()
    except Exception:
        pass


def send(msg):
    data = json.dumps(msg, ensure_ascii=False).encode("utf-8")
    sys.stdout.buffer.write(struct.pack("<I", len(data)))
    sys.stdout.buffer.write(data)
    sys.stdout.buffer.flush()


def kill_proc(proc):
    """Убивает процесс вместе со всеми детьми (sleep & co не должны висеть)."""
    if proc is None or proc.poll() is not None:
        return
    try:
        if os.name == "nt":
            subprocess.run(
                ["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
        else:
            os.killpg(os.getpgid(proc.pid), signal.SIGKILL)
    except Exception:
        try:
            proc.kill()
        except Exception:
            pass


def whitelist_allows(command):
    path = os.environ.get("BROWSER_TERMINAL_WHITELIST")
    if not path:
        return True
    try:
        with open(path, "r", encoding="utf-8") as fh:
            rules = [ln.strip() for ln in fh
                     if ln.strip() and not ln.lstrip().startswith("#")]
    except OSError:
        return True
    if not rules:
        return True
    stripped = command.strip()
    if not stripped:
        return True
    first = stripped.split(None, 1)[0]
    base = os.path.basename(first)
    return any(first == r or base == r for r in rules)


class Session:
    def __init__(self):
        self.cwd = os.path.abspath(os.getcwd())
        self.proc = None

    @staticmethod
    def shell_path():
        if os.name == "nt":
            return None
        return os.environ.get("SHELL") or "/bin/sh"

    def spawn(self, command):
        """Создаёт подпроцесс shell; в конце скрипта shell печатает маркер статуса/pwd."""
        token = secrets.token_hex(8)
        if os.name == "nt":
            # cmd: !ERRORLEVEL! (delayed expansion) + команда cd в конце печатает текущую папку
            script = "(%s) 2>&1 & echo __BT_%s_RC!ERRORLEVEL!__ & cd" % (command, token)
            argv = ["cmd.exe", "/v:on", "/c", script]
            stop_re = re.compile(rb"__BT_" + token.encode() + rb"_RC\d+__\r?\n[^\r\n]+")
        else:
            shell = self.shell_path()
            script = (
                command
                + "\n__bt_rc=$?\n"
                + "printf '\\n__BT_" + token + "_RC_%s_PWD_%s__\\n' \"$__bt_rc\" \"$(pwd)\"\n"
            )
            argv = [shell, "-c", script]
            # маркер — одна строка вида __BT_<tok>_RC_<code>_PWD_<path>__
            stop_re = re.compile(rb"__BT_" + token.encode() + rb"_RC_\d+_PWD_[^\n]*__")
        kwargs = {}
        if os.name != "nt":
            kwargs["start_new_session"] = True  # своя группа процессов -> killpg убивает всё дерево
        proc = subprocess.Popen(
            argv,
            cwd=self.cwd,
            stdin=subprocess.DEVNULL,   # чтобы команды вроде cat не съели наш протокол из stdin
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env=os.environ.copy(),
            **kwargs
        )
        return proc, token.encode("ascii"), stop_re

    def run(self, command, msg_id, ctl):
        if not whitelist_allows(command):
            send({"kind": "error", "id": msg_id,
                  "message": "Команда отклонена белым списком (BROWSER_TERMINAL_WHITELIST)."})
            send({"kind": "done", "id": msg_id, "code": 126, "cwd": self.cwd})
            return

        proc, token, stop_re = self.spawn(command)
        self.proc = proc
        log("run #%s: %r (cwd=%s)" % (msg_id, command, self.cwd))

        sel = selectors.DefaultSelector()
        sel.register(proc.stdout, selectors.EVENT_READ, "out")
        sel.register(proc.stderr, selectors.EVENT_READ, "err")
        pending = bytearray()   # нераспознанный хвост stdout (маркер может резаться по границе)
        marker_bytes = b""      # найденный маркер (для парсинга code/cwd)
        open_pipes = 2
        marker_found = False
        try:
            while open_pipes > 0 and not marker_found:
                # неблокирующе забираем управляющие сообщения (interrupt/eof)
                try:
                    while True:
                        m = ctl.get_nowait()
                        k = m.get("kind")
                        if k in ("interrupt", "eof"):
                            if k == "eof":
                                ctl.put(m)  # вернём, чтобы main() корректно завершился
                            kill_proc(proc)
                except queue.Empty:
                    pass

                for key, _ in sel.select(timeout=0.1):
                    try:
                        chunk = os.read(key.fileobj.fileno(), CHUNK)
                    except OSError:
                        chunk = b""
                    if not chunk:
                        try:
                            sel.unregister(key.fileobj)
                        except Exception:
                            pass
                        open_pipes -= 1
                        if key.data == "out" and pending:
                            # EOF без маркера (например, команда exit) — отдать остаток
                            send({"kind": "out", "id": msg_id,
                                  "data": bytes(pending).decode("utf-8", "replace")})
                            pending.clear()
                        continue
                    if key.data == "err":
                        send({"kind": "err", "id": msg_id,
                              "data": chunk.decode("utf-8", "replace")})
                        continue
                    # stdout: ищем маркер, не отсылая его в терминал
                    pending += chunk
                    m = stop_re.search(bytes(pending))
                    if m:
                        clean = bytes(pending[:m.start()])
                        if clean:
                            send({"kind": "out", "id": msg_id,
                                  "data": clean.decode("utf-8", "replace")})
                        marker_bytes = bytes(pending[m.start():m.end()])
                        pending.clear()
                        marker_found = True
                    elif len(pending) > HOLD_BACK:
                        part = bytes(pending[:-HOLD_BACK])
                        del pending[:-HOLD_BACK]
                        send({"kind": "out", "id": msg_id,
                              "data": part.decode("utf-8", "replace")})
        finally:
            try:
                sel.close()
            except Exception:
                pass
            for p in (proc.stdout, proc.stderr):
                try:
                    if p:
                        p.close()
                except Exception:
                    pass
            try:
                proc.wait(timeout=5)
            except Exception:
                kill_proc(proc)
                try:
                    proc.wait(timeout=2)
                except Exception:
                    pass
            self.proc = None

        rc = proc.returncode if proc.returncode is not None else -1
        cwd = self.cwd
        if marker_found:
            if os.name == "nt":
                m = re.search(rb"__BT_" + token + rb"_RC(\d+)__\r?\n([^\r\n]+)", marker_bytes)
                if m:
                    rc = int(m.group(1))
                    cand = m.group(2).decode("utf-8", "replace").strip()
                    if cand and os.path.isdir(cand):
                        cwd = os.path.abspath(cand)
            else:
                m = re.search(rb"__BT_" + token + rb"_RC_(\d+)_PWD_(.*)__", marker_bytes)
                if m:
                    rc = int(m.group(1))
                    cand = m.group(2).decode("utf-8", "replace").strip("\r\n")
                    if cand and os.path.isdir(cand):
                        cwd = os.path.abspath(cand)
        self.cwd = cwd
        send({"kind": "done", "id": msg_id, "code": rc, "cwd": cwd})
        log("done #%s code=%s cwd=%s" % (msg_id, rc, cwd))


def main():
    ctl = queue.Queue()

    def reader():
        """Читает сообщения из stdin (протокол Chrome) и кладёт их в очередь."""
        try:
            while True:
                hdr = sys.stdin.buffer.read(4)
                if not hdr or len(hdr) < 4:
                    break
                (n,) = struct.unpack("<I", hdr)
                if n <= 0 or n > MAX_INCOMING:
                    break
                data = b""
                while len(data) < n:
                    part = sys.stdin.buffer.read(n - len(data))
                    if not part:
                        break
                    data += part
                if len(data) < n:
                    break
                try:
                    ctl.put(json.loads(data.decode("utf-8")))
                except Exception:
                    pass
        except Exception as exc:
            log("reader error: %r" % exc)
        finally:
            ctl.put({"kind": "eof"})

    threading.Thread(target=reader, daemon=True).start()

    session = Session()
    send({
        "kind": "ready",
        "host_name": HOST_NAME,
        "pid": os.getpid(),
        "cwd": session.cwd,
        "home": os.path.expanduser("~"),
        "user": getpass.getuser(),
        "host": socket.gethostname(),
        "shell": session.shell_path() or "cmd.exe",
        "platform": sys.platform,
    })
    log("ready cwd=%s user=%s" % (session.cwd, getpass.getuser()))

    while True:
        try:
            msg = ctl.get(timeout=0.5)
        except queue.Empty:
            continue
        kind = msg.get("kind")
        if kind == "eof":
            break
        if kind == "ping":
            send({"kind": "pong", "id": msg.get("id")})
        elif kind == "interrupt":
            kill_proc(session.proc)
        elif kind == "run":
            session.run(str(msg.get("command", "")), msg.get("id"), ctl)
        # прочие kind игнорируем

    kill_proc(session.proc)
    log("bye")


if __name__ == "__main__":
    main()
