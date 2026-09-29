#!/usr/bin/env python3
"""Patch the upstream MyTelegram Android client for the local test lab.

The fork keeps the Telegram-like UI and MTProto client but must have a unique
Android application id. Using org.telegram.messenger makes Android treat the
APK as an update of the official Telegram app; a differently signed APK is
then rejected as an invalid package. This patch also changes the native DC
endpoint and limits the build to arm64-v8a.
"""

from __future__ import annotations

import ipaddress
import re
import sys
from pathlib import Path

OLD_ENDPOINT = 'std::string ipv4="192.168.1.100";'
ABI_OLD = 'abiFilters "armeabi-v7a", "arm64-v8a", "x86", "x86_64"'
ABI_NEW = 'abiFilters "arm64-v8a"'
OLD_PACKAGE = "org.telegram.messenger"
NEW_PACKAGE = "org.mytelegram.local"


def fail(message: str, code: int = 1) -> int:
    print(message, file=sys.stderr)
    return code


def patch_identity(source: Path) -> int:
    properties = source / "gradle.properties"
    if not properties.is_file():
        return fail(f"missing Gradle properties: {properties}")
    text = properties.read_text(encoding="utf-8")
    old = f"APP_PACKAGE={OLD_PACKAGE}"
    new = f"APP_PACKAGE={NEW_PACKAGE}"
    if old in text:
        properties.write_text(text.replace(old, new, 1), encoding="utf-8")
    elif new not in text:
        return fail("APP_PACKAGE marker was not found; refusing a blind package patch")

    # The Google Services plugin checks the application id against this JSON,
    # even though the local lab does not use production Firebase services.
    google_services = source / "TMessagesProj_App/google-services.json"
    if google_services.is_file():
        google_text = google_services.read_text(encoding="utf-8")
        google_text = google_text.replace(OLD_PACKAGE, NEW_PACKAGE)
        google_services.write_text(google_text, encoding="utf-8")

    # Do not leave the launcher presenting itself as the official Telegram app.
    # Keep the localized resources, changing only the app-name string entries.
    changed_names = 0
    found_names = 0
    for strings in (source / "TMessagesProj/src/main/res").glob("values*/strings.xml"):
        text = strings.read_text(encoding="utf-8")
        if re.search(r'<string\s+name="AppName">.*?</string>', text, flags=re.DOTALL):
            found_names += 1
        updated = re.sub(
            r'(<string\s+name="AppName">).*?(</string>)',
            r'\1MyTelegram Local\2',
            text,
            flags=re.DOTALL,
        )
        updated = re.sub(
            r'(<string\s+name="AppNameBeta">).*?(</string>)',
            r'\1MyTelegram Local Beta\2',
            updated,
            flags=re.DOTALL,
        )
        if updated != text:
            changed_names += 1
            strings.write_text(updated, encoding="utf-8")
    if found_names == 0:
        return fail("AppName resources were not found; refusing a blind label patch")
    print(f"set unique package {NEW_PACKAGE} and local app label in {changed_names} resource files")
    return 0


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: patch-client.py SOURCE_DIR SERVER_IP", file=sys.stderr)
        return 2
    source = Path(sys.argv[1]).resolve()
    try:
        ip = str(ipaddress.ip_address(sys.argv[2]))
    except ValueError:
        return fail("invalid IPv4/IPv6 address", 2)

    target = source / "TMessagesProj/jni/tgnet/ConnectionsManager.cpp"
    if not target.is_file():
        return fail(f"missing client source: {target}")
    text = target.read_text(encoding="utf-8")
    current_endpoint = f'std::string ipv4="{ip}";'
    if OLD_ENDPOINT in text:
        target.write_text(text.replace(OLD_ENDPOINT, current_endpoint, 1), encoding="utf-8")
    elif current_endpoint not in text:
        return fail("upstream endpoint marker was not found; refusing a blind patch")
    else:
        print(f"client already points to {ip}")

    gradle_file = source / "TMessagesProj_App/build.gradle"
    if not gradle_file.is_file():
        return fail(f"missing Android app build file: {gradle_file}")
    gradle_text = gradle_file.read_text(encoding="utf-8")
    abi_count = gradle_text.count(ABI_OLD)
    if abi_count:
        gradle_file.write_text(gradle_text.replace(ABI_OLD, ABI_NEW), encoding="utf-8")
        print(f"limited {abi_count} native variants to arm64-v8a")
    elif gradle_text.count(ABI_NEW) < 3:
        return fail("native ABI list was not found; refusing a blind ABI patch")

    return patch_identity(source)


if __name__ == "__main__":
    raise SystemExit(main())
