#!/bin/sh
set -eu
cd "$(dirname "$0")"
# Zig includes the Windows cross-compilation toolchain.
python3 -m ziglang cc -target x86_64-windows-gnu -O2 -s -municode -Wl,--subsystem,windows main.c -o 'Editor 1.exe' -lcomdlg32 -lshell32 -lgdi32 -luser32 -luuid
