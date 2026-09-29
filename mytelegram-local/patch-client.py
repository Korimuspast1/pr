#!/usr/bin/env python3
"""Patch only the upstream MyTelegram Android DC endpoint.

The fork deliberately keeps the original client UI and MTProto layer. This
script changes the test endpoint from the upstream example IP to the private
server supplied by the operator; it never touches credentials or phone data.
"""

from __future__ import annotations

import ipaddress
import sys
from pathlib import Path

OLD = 'std::string ipv4="192.168.1.100";'
ABI_OLD = 'abiFilters "armeabi-v7a", "arm64-v8a", "x86", "x86_64"'
ABI_NEW = 'abiFilters "arm64-v8a"'


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: patch-client.py SOURCE_DIR SERVER_IP", file=sys.stderr)
        return 2
    source = Path(sys.argv[1]).resolve()
    try:
        ip = str(ipaddress.ip_address(sys.argv[2]))
    except ValueError:
        print("invalid IPv4/IPv6 address", file=sys.stderr)
        return 2

    target = source / "TMessagesProj/jni/tgnet/ConnectionsManager.cpp"
    if not target.is_file():
        print("missing client source: {}".format(target), file=sys.stderr)
        return 1
    text = target.read_text(encoding="utf-8")
    current = 'std::string ipv4="{}";'.format(ip)
    if OLD in text:
        target.write_text(text.replace(OLD, current, 1), encoding="utf-8")
    elif current in text:
        print("client already points to {}".format(ip))
    else:
        print("upstream endpoint marker was not found; refusing a blind patch", file=sys.stderr)
        return 1

    # A phone APK only needs arm64-v8a. Building four native ABIs made the
    # first CI build look stuck while compiling the large native MTProto tree.
    gradle_file = source / "TMessagesProj_App/build.gradle"
    if not gradle_file.is_file():
        print("missing Android app build file: {}".format(gradle_file), file=sys.stderr)
        return 1
    gradle_text = gradle_file.read_text(encoding="utf-8")
    abi_count = gradle_text.count(ABI_OLD)
    if abi_count:
        gradle_file.write_text(gradle_text.replace(ABI_OLD, ABI_NEW), encoding="utf-8")
        print("limited {} native variants to arm64-v8a".format(abi_count))
    elif gradle_text.count(ABI_NEW) < 3:
        print("native ABI list was not found; refusing a blind patch", file=sys.stderr)
        return 1
    print("patched {} -> {}".format(target, ip))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
