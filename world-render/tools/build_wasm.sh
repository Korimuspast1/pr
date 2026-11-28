#!/usr/bin/env bash
# Builds the WebAssembly module used by the browser walk mode.
#
# No wasm-bindgen, no wasm-pack: a plain rustc cross-compile to
# wasm32-unknown-unknown. The page talks to it through plain C exports.
#
# Environment overrides (all optional):
#   RUSTC         compiler to call (default: rustc from PATH)
#   WASM_SYSROOT  sysroot that contains the wasm32-unknown-unknown std
#   RUSTFLAGS     extra flags, passed straight through
set -euo pipefail

cd "$(dirname "$0")/.."
mkdir -p web/wasm

RUSTC_BIN="${RUSTC:-rustc}"
SYSROOT_FLAG=()
if [ -n "${WASM_SYSROOT:-}" ]; then
  SYSROOT_FLAG=(--sysroot "$WASM_SYSROOT")
fi

"$RUSTC_BIN" \
  "${SYSROOT_FLAG[@]}" \
  --edition 2021 \
  --target wasm32-unknown-unknown \
  --crate-type cdylib \
  -O \
  -C strip=symbols \
  src/lib.rs \
  -o web/wasm/world_render.wasm

echo "built web/wasm/world_render.wasm ($(du -h web/wasm/world_render.wasm | cut -f1))"
