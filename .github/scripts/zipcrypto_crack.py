#!/usr/bin/env python3
"""Подбор пароля legacy ZipCrypto по словарю (только собственный архив).

Реализация Biham–Kocher/ZipCrypto: пароль проверяется по 12-байтному
заголовку шифрования, последний байт которого равен старшему байту CRC файла.
Проверка одного пароля — единицы микросекунд, словарь rockyou проходится
за минуты на нескольких ядрах.
"""

from __future__ import annotations

import multiprocessing as mp
import os
import struct
import sys
import zipfile


def make_table() -> list[int]:
    table = []
    for i in range(256):
        c = i
        for _ in range(8):
            c = (c >> 1) ^ 0xEDB88320 if c & 1 else c >> 1
        table.append(c)
    return table


TABLE = make_table()


def check_password(pwd: bytes, hdr: bytes, target: int) -> bool:
    k0, k1, k2 = 0x12345678, 0x23456789, 0x34567890
    tbl = TABLE
    for c in pwd:
        k0 = (k0 >> 8) ^ tbl[(k0 ^ c) & 0xFF]
        k1 = ((k1 + (k0 & 0xFF)) * 134775813 + 1) & 0xFFFFFFFF
        k2 = (k2 >> 8) ^ tbl[(k2 ^ (k1 >> 24)) & 0xFF]
    for c in hdr[:11]:
        t = (k2 | 2) & 0xFFFF
        p = c ^ (((t * (t ^ 1)) >> 8) & 0xFF)
        k0 = (k0 >> 8) ^ tbl[(k0 ^ p) & 0xFF]
        k1 = ((k1 + (k0 & 0xFF)) * 134775813 + 1) & 0xFFFFFFFF
        k2 = (k2 >> 8) ^ tbl[(k2 ^ (k1 >> 24)) & 0xFF]
    t = (k2 | 2) & 0xFFFF
    return (hdr[11] ^ (((t * (t ^ 1)) >> 8) & 0xFF)) == target


def read_first_encrypted_header(path: str) -> tuple[bytes, int]:
    with open(path, "rb") as fh:
        data = fh.read()
    off = data.find(b"PK\x03\x04")
    if off < 0:
        raise SystemExit("нет локального заголовка ZIP")
    flags = struct.unpack_from("<H", data, off + 6)[0]
    name_len = struct.unpack_from("<H", data, off + 26)[0]
    extra_len = struct.unpack_from("<H", data, off + 28)[0]
    data_off = off + 30 + name_len + extra_len
    hdr = data[data_off:data_off + 12]
    if len(hdr) < 12:
        raise SystemExit("не удалось прочитать заголовок шифрования")
    with zipfile.ZipFile(path) as zf:
        info = zf.infolist()[0]
    if flags & 0x8:
        # при data-дескрипторе CRC неизвестен шифратору: контрольный байт —
        # старший байт DOS-времени модификации
        y, mo, d, h, mi, sec = info.date_time
        dos_time = (h << 11) | (mi << 5) | (sec // 2)
        target = (dos_time >> 8) & 0xFF
    else:
        target = (info.CRC >> 24) & 0xFF
    return hdr, target


def verify_file(zip_path: str, pwd: str) -> bool:
    """Полная проверка: читаем все файлы и сверяем CRC."""
    try:
        with zipfile.ZipFile(zip_path) as zf:
            for i in zf.infolist():
                if i.is_dir():
                    continue
                zf.read(i, pwd=pwd.encode())
        return True
    except Exception:  # noqa: BLE001
        return False


def worker(args) -> str | None:
    path, start, end, hdr, target, zip_path = args
    tbl = TABLE
    with open(path, "rb", buffering=1 << 20) as fh:
        fh.seek(start)
        pos = start
        while pos < end:
            line = fh.readline()
            if not line:
                break
            pos += len(line)
            pwd = line.rstrip(b"\r\n")
            if not pwd or len(pwd) > 64:
                continue
            k0, k1, k2 = 0x12345678, 0x23456789, 0x34567890
            for c in pwd:
                k0 = (k0 >> 8) ^ tbl[(k0 ^ c) & 0xFF]
                k1 = ((k1 + (k0 & 0xFF)) * 134775813 + 1) & 0xFFFFFFFF
                k2 = (k2 >> 8) ^ tbl[(k2 ^ (k1 >> 24)) & 0xFF]
            for c in hdr[:11]:
                t = (k2 | 2) & 0xFFFF
                p = c ^ (((t * (t ^ 1)) >> 8) & 0xFF)
                k0 = (k0 >> 8) ^ tbl[(k0 ^ p) & 0xFF]
                k1 = ((k1 + (k0 & 0xFF)) * 134775813 + 1) & 0xFFFFFFFF
                k2 = (k2 >> 8) ^ tbl[(k2 ^ (k1 >> 24)) & 0xFF]
            t = (k2 | 2) & 0xFFFF
            if (hdr[11] ^ (((t * (t ^ 1)) >> 8) & 0xFF)) == target:
                cand = pwd.decode("utf-8", "ignore")
                # однобайтовая проверка даёт ~1/256 ложных срабатываний
                if verify_file(zip_path, cand):
                    return cand
    return None


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: zipcrypto_crack.py <zip> <wordlist> [procs]")
        return 64
    path, wordlist = sys.argv[1], sys.argv[2]
    procs = int(sys.argv[3]) if len(sys.argv) > 3 else (os.cpu_count() or 2)

    hdr, target = read_first_encrypted_header(path)
    size = os.path.getsize(wordlist)
    chunk = max(1 << 20, size // (procs * 8))
    jobs = []
    start = 0
    while start < size:
        end = min(size, start + chunk)
        if end < size:
            with open(wordlist, "rb") as fh:
                fh.seek(end)
                fh.readline()
                end = fh.tell()
        jobs.append((wordlist, start, end, hdr, target, path))
        start = end

    print(f"фрагментов: {len(jobs)}, процессов: {procs}, байт словаря: {size}")
    found = None
    with mp.Pool(procs) as pool:
        for i, res in enumerate(pool.imap_unordered(worker, jobs), 1):
            if res:
                found = res
                pool.terminate()
                break
            if i % 10 == 0:
                print(f"  обработано фрагментов: {i}/{len(jobs)}", flush=True)

    if not found:
        print("пароль не найден")
        return 1
    print(f"ПАРОЛЬ: {found!r}  (проверка CRC: {'OK' if verify_file(path, found) else 'не сошлось'})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
