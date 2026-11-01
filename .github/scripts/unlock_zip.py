#!/usr/bin/env python3
"""Снимает шифрование с ZIP-архива и разбирает APK статически.

Пароль не подбирается «в лоб»: перебирается короткий список типовых паролей,
которые используют распространители таких сборок. Файлы не запускаются.
"""

from __future__ import annotations

import os
import sys
import zipfile

try:  # AES-шифрование zipfile не умеет, pyzipper умеет
    import pyzipper  # type: ignore
except Exception:  # noqa: BLE001
    pyzipper = None

PASSWORDS = [
    "", "1234", "12345", "123456", "123", "123456789", "12345678", "1234567",
    "1111", "2222", "0000", "111111", "123123", "123321", "1", "12", "2026",
    "2025", "2024", "password", "pass", "admin", "root", "qwerty", "abc123",
    "vpn", "VPn", "VPN", "fakevpn", "fake", "buttonvpn", "button", "ButtonVPN",
    "buttonvpnfree", "freevpn", "vpnfree", "apk", "android", "mediafire",
    "infected", "virus", "malware", "sample", "test", "telegram", "tg",
    "777", "666", "999", "159753", "112233", "102030", "1q2w3e", "qwe123",
    "password123", "admin123", "vpn123", "1234qwer", "a123456", "1a2b3c",
    "мойпароль", "пароль", "Россия", "россия", "україна", "ukraine",
    "!@#$%^", "@123456", "1234!", "2023", "2022", "2021", "vpn2026",
    "buttonvpn2026", "ButtonVPN2026", "FakeVPN2026", "fakevpn2026",
]


def try_open(zf: zipfile.ZipFile, pwd: bytes) -> tuple[bool, dict]:
    """Пробует прочитать все файлы архива с паролем; True, если все открылись."""
    out = {}
    for info in zf.infolist():
        if info.is_dir():
            continue
        try:
            with zf.open(info, pwd=pwd) as fh:
                out[info.filename] = fh.read()  # проверка CRC произойдёт при чтении
        except Exception:  # noqa: BLE001
            return False, {}
    return True, out


def main() -> int:
    src, outdir = sys.argv[1], sys.argv[2]
    os.makedirs(outdir, exist_ok=True)
    opener = pyzipper.AESZipFile if pyzipper is not None else zipfile.ZipFile
    with opener(src) as zf:
        infos = zf.infolist()
        print("Содержимое с флагами:")
        for i in infos:
            method = {0: "stored", 8: "deflate", 12: "bzip2", 14: "lzma",
                      93: "zstd", 99: "AES"}.get(i.compress_type, str(i.compress_type))
            has_aes_extra = bool(i.extra) and b"\x01\x99" in i.extra
            print(f"  {i.filename!r} size={i.file_size} method={method} "
                  f"flag=0x{i.flag_bits:04x} encrypted={bool(i.flag_bits & 0x1)} "
                  f"aes_extra={has_aes_extra}")

        if not any(i.flag_bits & 0x1 for i in infos):
            ok, files = True, {}
            for i in infos:
                if not i.is_dir():
                    files[i.filename] = zf.read(i)
            print("Архив без шифрования")
        else:
            ok, files, used = False, {}, None
            for cand in PASSWORDS:
                good, got = try_open(zf, cand.encode("utf-8"))
                if good:
                    ok, files, used = True, got, cand
                    break
            if ok:
                print(f"ПАРОЛЬ ПОДОБРАН: {used!r}")
            else:
                print("ПАРОЛЬ НЕ НАЙДЕН среди типовых — файлы извлечь не удалось")
                return 2

    print("\nИзвлечено:")
    paths = []
    for name, blob in files.items():
        safe = os.path.basename(name) or "unnamed.bin"
        dest = os.path.join(outdir, safe)
        with open(dest, "wb") as fh:
            fh.write(blob)
        paths.append(dest)
        print(f"  {safe}: {len(blob)} байт")
    with open(os.path.join(outdir, "list.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(paths))
    return 0


if __name__ == "__main__":
    sys.exit(main())
