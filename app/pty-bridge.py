#!/usr/bin/env python3
"""
Bridges stdin/stdout (plain pipes from Node) to a real PTY running bash.

Protocol:
  stdin  : raw bytes -> pty. A control frame "\x01R<cols>,<rows>\x01" resizes.
  stdout : raw pty output -> Node, streamed to the browser terminal.
  stderr : diagnostics only.
"""
import os
import pty
import select
import struct
import sys
import fcntl
import termios


def set_size(fd, cols, rows):
    try:
        fcntl.ioctl(fd, termios.TIOCSWINSZ, struct.pack("HHHH", rows, cols, 0, 0))
    except Exception:
        pass


def main():
    shell = os.environ.get("PTY_SHELL", "/bin/bash")
    cols = int(os.environ.get("PTY_COLS", "120"))
    rows = int(os.environ.get("PTY_ROWS", "32"))

    pid, fd = pty.fork()
    if pid == 0:
        os.environ["TERM"] = "xterm-256color"
        os.environ["COLORTERM"] = "truecolor"
        os.environ["PS1"] = r"\u@\h:\w\$ "
        try:
            os.execvp(shell, [shell, "-i"])
        except Exception:
            os._exit(127)

    set_size(fd, cols, rows)
    stdin_fd = sys.stdin.fileno()

    while True:
        try:
            r, _, _ = select.select([fd, stdin_fd], [], [], 0.25)
        except (OSError, ValueError):
            break

        if fd in r:
            try:
                data = os.read(fd, 65536)
            except OSError:
                break
            if not data:
                break
            os.write(1, data)

        if stdin_fd in r:
            try:
                data = os.read(stdin_fd, 65536)
            except OSError:
                break
            if not data:
                break
            if data.startswith(b"\x01R"):
                end = data.find(b"\x01", 2)
                if end > 0:
                    try:
                        c, rr = data[2:end].decode().split(",")
                        set_size(fd, int(c), int(rr))
                    except Exception:
                        pass
                    data = data[end + 1:]
                else:
                    data = b""
            if data:
                try:
                    os.write(fd, data)
                except OSError:
                    break

    try:
        os.close(fd)
    except Exception:
        pass
    try:
        os.waitpid(pid, os.WNOHANG)
    except Exception:
        pass


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
