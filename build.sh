#!/usr/bin/env bash
# Сборка SteamFinder.exe (Windows x64) из Linux/macOS с помощью Zig.
# Требуется: python3 -m venv .zigvenv && .zigvenv/bin/pip install ziglang
set -euo pipefail

cd "$(dirname "$0")"

ZIG=${ZIG:-"$HOME/.zigvenv/bin/python -m ziglang"}
TARGET=x86_64-windows-gnu
OUT=dist/SteamFinder.exe

mkdir -p dist build

echo "==> Компилируем ресурсы (иконка + манифест)"
$ZIG rc /I src src/app.rc build/app.res

echo "==> Собираем $OUT"
$ZIG cc -target $TARGET -O2 -municode \
    -o "$OUT" \
    src/main.c src/json.c src/http.c build/app.res \
    -lwinhttp -lgdiplus -lole32 -lshell32 -lgdi32 -luser32 \
    -Wl,--subsystem,windows

echo "==> Готово: $OUT"
ls -la "$OUT"
