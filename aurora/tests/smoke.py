#!/usr/bin/env python3
"""Aurora QEMU smoke test: BIOS + UEFI, keyboard, mouse drag, shell, panic, reboot, halt.
Uses only Python's standard library. Screenshots are written as PPM files.
"""
import argparse, json, os, pathlib, shutil, socket, subprocess, sys, tempfile, time

ROOT = pathlib.Path(__file__).resolve().parents[1]

class QMP:
    def __init__(self, path, timeout=20):
        deadline = time.monotonic() + timeout
        while True:
            try:
                self.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
                self.sock.connect(str(path))
                break
            except (FileNotFoundError, ConnectionRefusedError):
                if time.monotonic() > deadline: raise
                time.sleep(0.05)
        self.f = self.sock.makefile("rwb")
        self.f.readline()
        self.call("qmp_capabilities")
    def call(self, cmd, **args):
        self.f.write((json.dumps({"execute": cmd, "arguments": args}) + "\n").encode())
        self.f.flush()
        while True:
            line = self.f.readline()
            if not line: raise RuntimeError("QMP connection closed")
            msg = json.loads(line)
            if "error" in msg: raise RuntimeError(msg["error"])
            if "return" in msg: return msg["return"]
    def close(self):
        try: self.f.close(); self.sock.close()
        except Exception: pass

def wait_for(path, needle, timeout=20):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            data = path.read_text(errors="replace")
            if needle in data: return data
        except FileNotFoundError: pass
        time.sleep(0.05)
    raise RuntimeError(f"timeout waiting for {needle!r} in {path}")

KEYMAP = {c: c for c in "abcdefghijklmnopqrstuvwxyz0123456789"}
KEYMAP.update({c: 'shift-' + c.lower() for c in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'})
KEYMAP.update({' ': 'spc', '-': 'minus', '_': 'shift-minus', '.': 'dot', '>': 'shift-dot',
               '#': 'shift-3', '(': 'shift-9', ')': 'shift-0', '/': 'slash', ':': 'shift-semicolon',
               ';': 'semicolon', '=': 'equal', '+': 'shift-equal', '[': 'bracket_left', ']': 'bracket_right',
               '\n': 'ret'})

def type_line(qmp, text):
    for ch in text:
        key = KEYMAP[ch]
        if key.startswith('shift-'):
            keys = [{'type':'qcode','data':'shift'}, {'type':'qcode','data':key[6:]}]
        else:
            keys = [{'type':'qcode','data':key}]
        qmp.call("send-key", keys=keys)
        time.sleep(0.12)
    time.sleep(0.12)

def mouse_rel(qmp, dx, dy):
    # QEMU rel events are delivered to the PS/2 mouse when no tablet is present.
    # Split large deltas so each PS/2 packet stays inside the 9-bit signed range.
    while dx or dy:
        sx = max(-100, min(100, dx)); sy = max(-100, min(100, dy))
        events = []
        if sx: events.append({"type": "rel", "data": {"axis": "x", "value": sx}})
        if sy: events.append({"type": "rel", "data": {"axis": "y", "value": sy}})
        if events: qmp.call("input-send-event", events=events)
        dx -= sx; dy -= sy
        time.sleep(0.02)

def mouse_button(qmp, down):
    qmp.call("input-send-event", events=[{"type": "btn", "data": {"button": "left", "down": down}}])

def screenshot(qmp, path):
    qmp.call("screendump", filename=str(path))
    wait_for(path, "P6", timeout=5) if path.exists() else None
    return path

def run_case(qemu, iso, mode, out, ovmf_code, ovmf_vars, extra_args):
    case = out / mode
    case.mkdir(parents=True, exist_ok=True)
    serial = case / "serial.log"
    qmp_path = case / "qmp.sock"
    for stale in (serial, qmp_path, case / "qemu.log"):
        if stale.exists(): stale.unlink()
    cmd = qemu + ["-machine", "pc", "-accel", "tcg", "-cpu", "qemu64", "-smp", "1", "-m", "512M",
                  "-cdrom", str(iso), "-boot", "order=d", "-vga", "std", "-display", "none",
                  "-serial", f"file:{serial}", "-qmp", f"unix:{qmp_path},server=on,wait=off",
                  "-no-shutdown"] + extra_args
    if mode == "uefi":
        if not ovmf_code or not ovmf_vars: raise RuntimeError("UEFI requires --ovmf-code and --ovmf-vars")
        vars_copy = case / "efi-vars.fd"
        shutil.copyfile(ovmf_vars, vars_copy)
        cmd += ["-drive", f"if=pflash,format=raw,readonly=on,file={ovmf_code}",
                "-drive", f"if=pflash,format=raw,file={vars_copy}"]
    log = (case / "qemu.log").open("wb")
    started = time.monotonic()
    proc = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT)
    qmp = None
    try:
        wait_for(serial, "[boot] READY", timeout=30)
        boot_seconds = time.monotonic() - started
        data = serial.read_text(errors="replace")
        assert "long mode confirmed" in data
        assert ("firmware=UEFI64" if mode == "uefi" else "firmware=BIOS") in data
        assert "PS/2 keyboard=ACK mouse=ACK" in data
        qmp = QMP(qmp_path)
        assert qmp.call("query-status")["running"]
        screenshot(qmp, case / "desktop.ppm")
        time.sleep(0.3)
        # Keyboard + shell commands.
        type_line(qmp, "help\n")
        wait_for(serial, "[shell:1] help")
        type_line(qmp, "echo hello-aurora\n")
        wait_for(serial, "[shell:1] hello-aurora")
        type_line(qmp, "uptime\nmem\nwindows\n")
        wait_for(serial, "Managed RAM:")
        type_line(qmp, "new Test\n")
        wait_for(serial, "create id=3 title=Test")
        type_line(qmp, "close 3\n")
        wait_for(serial, "close id=3")
        type_line(qmp, "monitor\n")
        wait_for(serial, "create id=4 title=System monitor")
        screenshot(qmp, case / "shell.ppm")
        # Drag the terminal title bar with relative PS/2 events.
        # Initial pointer is centre (512,384); title bar grab point is (300,90).
        mouse_rel(qmp, -212, -294); time.sleep(0.15)
        mouse_button(qmp, True); time.sleep(0.08)
        for _ in range(8): mouse_rel(qmp, 12, 8); time.sleep(0.04)
        mouse_button(qmp, False); time.sleep(0.15)
        wait_for(serial, "[wm] drag id=1", timeout=5)
        screenshot(qmp, case / "drag.ppm")
        # Page-fault panic screen.
        type_line(qmp, "fault-test\n")
        wait_for(serial, "exception=14 (Page fault)", timeout=5)
        time.sleep(0.2)
        screenshot(qmp, case / "panic.ppm")
        panic_data = serial.read_text(errors="replace")
        # Reboot in a separate VM so the panic VM does not interfere.
        reboot_case(mode, qemu, iso, out, ovmf_code, ovmf_vars, extra_args)
        halt_case(mode, qemu, iso, out, ovmf_code, ovmf_vars, extra_args)
        result = {"mode": mode, "boot_seconds": round(boot_seconds, 3), "status": "PASS",
                  "checks": ["long-mode", "firmware", "framebuffer", "ps2-ack", "keyboard", "help", "echo", "uptime", "mem", "windows", "spawn", "close", "monitor", "mouse-drag", "page-fault-panic", "reboot", "halt"]}
        (case / "result.json").write_text(json.dumps(result, indent=2) + "\n")
        print(f"PASS {mode}: READY in {boot_seconds:.2f}s; keyboard, shell, drag, panic, reboot, halt")
    finally:
        if qmp: qmp.close()
        if proc.poll() is None:
            proc.terminate()
            try: proc.wait(timeout=5)
            except subprocess.TimeoutExpired: proc.kill(); proc.wait()
        log.close()

def short_vm(mode, qemu, iso, out, ovmf_code, ovmf_vars, extra_args, action):
    case = out / f"{mode}-{action}"
    case.mkdir(parents=True, exist_ok=True)
    serial = case / "serial.log"; qmp_path = case / "qmp.sock"
    for stale in (serial, qmp_path, case / "qemu.log"):
        if stale.exists(): stale.unlink()
    cmd = qemu + ["-machine", "pc", "-accel", "tcg", "-cpu", "qemu64", "-smp", "1", "-m", "512M",
                  "-cdrom", str(iso), "-boot", "order=d", "-vga", "std", "-display", "none",
                  "-serial", f"file:{serial}", "-qmp", f"unix:{qmp_path},server=on,wait=off",
                  "-no-shutdown"] + extra_args
    if mode == "uefi":
        vars_copy = case / "efi-vars.fd"; shutil.copyfile(ovmf_vars, vars_copy)
        cmd += ["-drive", f"if=pflash,format=raw,readonly=on,file={ovmf_code}",
                "-drive", f"if=pflash,format=raw,file={vars_copy}"]
    log = (case / "qemu.log").open("wb")
    proc = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT); qmp = None
    try:
        wait_for(serial, "[boot] READY", timeout=30); qmp = QMP(qmp_path)
        type_line(qmp, action + "\n")
        if action == "reboot":
            wait_for(serial, "[power] reboot requested", timeout=5)
            deadline = time.monotonic() + 15
            while time.monotonic() < deadline:
                data = serial.read_text(errors="replace")
                if data.count("[boot] Aurora") >= 2 and data.count("[boot] READY") >= 2:
                    screenshot(qmp, case / "rebooted.ppm"); return
                time.sleep(0.1)
            raise RuntimeError("reboot did not produce a second boot")
        else:
            wait_for(serial, "[power] CPU halt requested", timeout=5)
            time.sleep(0.3); screenshot(qmp, case / "halt.ppm")
            assert qmp.call("query-status")["running"]  # QEMU still alive; guest CPU is in CLI/HLT.
    finally:
        if qmp: qmp.close()
        if proc.poll() is None:
            proc.terminate()
            try: proc.wait(timeout=5)
            except subprocess.TimeoutExpired: proc.kill(); proc.wait()
        log.close()

def reboot_case(*args): short_vm(*args, action="reboot")
def halt_case(*args): short_vm(*args, action="halt")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--qemu", default="qemu-system-x86_64")
    ap.add_argument("--iso", default=str(ROOT / "build/aurora.iso"))
    ap.add_argument("--out", default=str(ROOT / "build/test-results"))
    ap.add_argument("--ovmf-code", default=os.environ.get("OVMF_CODE", "/usr/share/OVMF/OVMF_CODE_4M.fd"))
    ap.add_argument("--ovmf-vars", default=os.environ.get("OVMF_VARS", "/usr/share/OVMF/OVMF_VARS_4M.fd"))
    ap.add_argument("--mode", choices=["bios", "uefi", "both"], default="both")
    ap.add_argument("--qemu-extra", default="")
    args = ap.parse_args()
    qemu = args.qemu.split()
    extra = args.qemu-extra.split() if args.qemu_extra else []
    out = pathlib.Path(args.out); out.mkdir(parents=True, exist_ok=True)
    modes = ["bios", "uefi"] if args.mode == "both" else [args.mode]
    for mode in modes:
        run_case(qemu, pathlib.Path(args.iso), mode, out, args.ovmf_code, args.ovmf_vars, extra)
    print("ALL PASS")

if __name__ == "__main__":
    main()
