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
    if OLD not in text:
        if 'std::string ipv4="{}";'.format(ip) in text:
            print("client already points to {}".format(ip))
            return 0
        print("upstream endpoint marker was not found; refusing a blind patch", file=sys.stderr)
        return 1
    target.write_text(text.replace(OLD, 'std::string ipv4="{}";'.format(ip), 1), encoding="utf-8")
    print("patched {} -> {}".format(target, ip))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
