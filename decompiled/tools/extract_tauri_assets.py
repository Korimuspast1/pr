#!/usr/bin/env python3
"""Extract the Brotli-compressed Tauri web assets from RyuuManifestToolV2.exe.

This extractor is intentionally tied to the analyzed executable by SHA-256.
It locates asset names instead of relying on hard-coded file offsets.
Requires: pip install brotli
"""

from __future__ import annotations

import hashlib
from pathlib import Path
import sys

try:
    import brotli
except ImportError as exc:
    raise SystemExit("Missing dependency: python -m pip install brotli") from exc

EXPECTED_SHA256 = "25a33d74a2271607b1bcb2a4729fcbfc587a90d33165b2531482d286ee6d72bd"
ASSETS = {
    b"/assets/index--wENrqoK.js": ("assets/index--wENrqoK.js", b"function"),
    b"/vite.svg": ("vite.svg", b"<svg"),
    b"/index.html": ("index.html", b"<!DOCTYPE html>"),
    b"/assets/index-B1sg37uI.css": ("assets/index-B1sg37uI.css", b"*,:before"),
}


def decompress_stream(data: bytes, start: int) -> tuple[bytes, int]:
    decoder = brotli.Decompressor()
    output = bytearray()
    for end in range(start, len(data)):
        output.extend(decoder.process(data[end : end + 1]))
        if decoder.is_finished():
            return bytes(output), end + 1
    raise ValueError(f"unfinished Brotli stream at 0x{start:x}")


def main() -> None:
    exe = Path(sys.argv[1] if len(sys.argv) > 1 else "RyuuManifestToolV2.exe")
    out = Path(sys.argv[2] if len(sys.argv) > 2 else "decompiled/frontend-extracted")
    data = exe.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != EXPECTED_SHA256:
        raise SystemExit(f"Unexpected executable SHA-256: {digest}")

    for marker, (relative_name, expected_prefix) in ASSETS.items():
        search_from = 0
        extracted = None
        while True:
            marker_offset = data.find(marker, search_from)
            if marker_offset < 0:
                break
            start = marker_offset + len(marker)
            try:
                content, end = decompress_stream(data, start)
                if content.startswith(expected_prefix):
                    extracted = (content, start, end)
                    break
            except brotli.error:
                pass
            search_from = marker_offset + 1

        if extracted is None:
            raise ValueError(f"valid asset stream not found after marker: {marker!r}")
        content, start, end = extracted
        destination = out / relative_name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
        print(
            f"{relative_name}: {len(content)} bytes "
            f"(compressed 0x{start:x}..0x{end:x})"
        )


if __name__ == "__main__":
    main()
