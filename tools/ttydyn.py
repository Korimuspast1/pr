#!/usr/bin/env python3
"""Инструмент для модулей TikTok You ``.ttydyn``.

Поддерживает четыре операции:

    python3 tools/ttydyn.py inspect module.ttydyn
    python3 tools/ttydyn.py keygen --out keys/dev
    python3 tools/ttydyn.py keycheck --key keys/dev.key.pem --pub keys/dev.pub.pem
    python3 tools/ttydyn.py pack --dex build/classes.dex --out release/module.ttydyn \\
        --key keys/dev.key.pem
    python3 tools/ttydyn.py verify release/module.ttydyn --pub keys/dev.pub.pem

Зависимостей нет: ECDSA P-256 и формат контейнера реализованы здесь.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import hashlib
import hmac
import json
import os
import re
import struct
import sys
import tempfile
import time
import zipfile
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

MANIFEST = "manifest.json"
SIGNATURE = "signature.der"
ZIP_TIME = (1980, 1, 1, 0, 0, 0)

# ---------------------------------------------------------------------------
# ECDSA P-256 (secp256r1)
# ---------------------------------------------------------------------------
P = 0xFFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF
A = P - 3
B = 0x5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B
GX = 0x6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296
GY = 0x4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5
N = 0xFFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551

Point = Optional[Tuple[int, int]]


def _inv(value: int, modulus: int) -> int:
    value %= modulus
    if value == 0:
        raise ValueError("обратного элемента для нуля нет")
    return pow(value, modulus - 2, modulus)


def _is_on_curve(point: Point) -> bool:
    if point is None:
        return False
    x, y = point
    return (
        0 <= x < P
        and 0 <= y < P
        and (y * y - (x * x * x + A * x + B)) % P == 0
    )


def _add(point1: Point, point2: Point) -> Point:
    if point1 is None:
        return point2
    if point2 is None:
        return point1
    x1, y1 = point1
    x2, y2 = point2
    if x1 == x2 and (y1 + y2) % P == 0:
        return None
    if point1 == point2:
        if y1 % P == 0:
            return None
        slope = (3 * x1 * x1 + A) * _inv(2 * y1, P) % P
    else:
        slope = (y2 - y1) * _inv(x2 - x1, P) % P
    x3 = (slope * slope - x1 - x2) % P
    return x3, (slope * (x1 - x3) - y1) % P


def _mul(scalar: int, point: Point = (GX, GY)) -> Point:
    if scalar < 0:
        raise ValueError("скаляр не может быть отрицательным")
    result: Point = None
    addend = point
    while scalar:
        if scalar & 1:
            result = _add(result, addend)
        addend = _add(addend, addend)
        scalar >>= 1
    return result


# DER helpers. ECDSA signatures use a SEQUENCE of two positive INTEGERs.
def _der_length(length: int) -> bytes:
    if length < 0:
        raise ValueError("отрицательная длина DER")
    if length < 0x80:
        return bytes([length])
    raw = length.to_bytes((length.bit_length() + 7) // 8, "big")
    return bytes([0x80 | len(raw)]) + raw


def _der_int(value: int) -> bytes:
    if value <= 0:
        raise ValueError("ECDSA INTEGER должен быть положительным")
    raw = value.to_bytes((value.bit_length() + 7) // 8 or 1, "big")
    if raw[0] & 0x80:
        raw = b"\x00" + raw
    return b"\x02" + _der_length(len(raw)) + raw


def der_encode(r: int, s: int) -> bytes:
    body = _der_int(r) + _der_int(s)
    return b"\x30" + _der_length(len(body)) + body


def _read_der_length(blob: bytes, offset: int) -> Tuple[int, int]:
    if offset >= len(blob):
        raise ValueError("обрезанная длина DER")
    first = blob[offset]
    offset += 1
    if not first & 0x80:
        return first, offset
    count = first & 0x7F
    if count == 0:
        raise ValueError("неопределённая длина DER")
    if count > 4 or offset + count > len(blob):
        raise ValueError("некорректная длина DER")
    if blob[offset] == 0:
        raise ValueError("не минимальная длина DER")
    length = int.from_bytes(blob[offset:offset + count], "big")
    if length < 0x80:
        raise ValueError("избыточная длинная форма DER")
    return length, offset + count


def _read_der_integer(blob: bytes, offset: int) -> Tuple[int, int]:
    if offset >= len(blob) or blob[offset] != 0x02:
        raise ValueError("ожидался DER INTEGER")
    length, body = _read_der_length(blob, offset + 1)
    end = body + length
    if length == 0 or end > len(blob):
        raise ValueError("обрезанный DER INTEGER")
    raw = blob[body:end]
    if raw[0] & 0x80:
        raise ValueError("ECDSA INTEGER имеет отрицательный знак")
    if len(raw) > 1 and raw[0] == 0 and not raw[1] & 0x80:
        raise ValueError("не минимальный DER INTEGER")
    return int.from_bytes(raw, "big"), end


def der_decode(blob: bytes) -> Tuple[int, int]:
    if not isinstance(blob, bytes) or len(blob) < 8 or blob[0] != 0x30:
        raise ValueError("не DER SEQUENCE")
    length, body = _read_der_length(blob, 1)
    end = body + length
    if end != len(blob):
        raise ValueError("лишние данные после DER SEQUENCE")
    r, offset = _read_der_integer(blob, body)
    s, offset = _read_der_integer(blob, offset)
    if offset != end:
        raise ValueError("в DER-подписи должно быть ровно два INTEGER")
    return r, s


def _rfc6979_k(private: int, digest: bytes) -> int:
    """Детерминированный nonce: одинаковые входы дают одинаковую подпись."""
    value = b"\x01" * 32
    key = b"\x00" * 32
    private_bytes = private.to_bytes(32, "big")
    key = hmac.new(key, value + b"\x00" + private_bytes + digest, hashlib.sha256).digest()
    value = hmac.new(key, value, hashlib.sha256).digest()
    key = hmac.new(key, value + b"\x01" + private_bytes + digest, hashlib.sha256).digest()
    value = hmac.new(key, value, hashlib.sha256).digest()
    while True:
        value = hmac.new(key, value, hashlib.sha256).digest()
        candidate = int.from_bytes(value, "big")
        if 1 <= candidate < N:
            return candidate
        key = hmac.new(key, value + b"\x00", hashlib.sha256).digest()
        value = hmac.new(key, value, hashlib.sha256).digest()


def ecdsa_sign(private: int, message: bytes) -> bytes:
    if not 1 <= private < N:
        raise ValueError("приватный ключ не является ключом P-256")
    digest = hashlib.sha256(message).digest()
    z = int.from_bytes(digest, "big")
    while True:
        nonce = _rfc6979_k(private, digest)
        point = _mul(nonce)
        if point is None:
            continue
        r = point[0] % N
        if r == 0:
            continue
        s = _inv(nonce, N) * (z + r * private) % N
        if s == 0:
            continue
        # Android accepts both forms, but low-s avoids malleable signatures.
        return der_encode(r, min(s, N - s))


def ecdsa_verify(public: Tuple[int, int], message: bytes, signature: bytes) -> bool:
    try:
        r, s = der_decode(signature)
        if not (1 <= r < N and 1 <= s < N) or not _is_on_curve(public):
            return False
        digest = hashlib.sha256(message).digest()
        z = int.from_bytes(digest, "big")
        inverse = _inv(s, N)
        point = _add(_mul(z * inverse % N), _mul(r * inverse % N, public))
        return point is not None and point[0] % N == r
    except (TypeError, ValueError, IndexError):
        return False


# ---------------------------------------------------------------------------
# Ключи: простой PEM-контейнер с 32 байтами scalar / 64 байтами X+Y
# ---------------------------------------------------------------------------
def _pem(tag: str, payload: bytes) -> str:
    body = base64.b64encode(payload).decode("ascii")
    lines = [body[index:index + 64] for index in range(0, len(body), 64)]
    return "-----BEGIN {0}-----\n{1}\n-----END {0}-----\n".format(tag, "\n".join(lines))


def _unpem(text: str) -> bytes:
    body = "".join(line.strip() for line in text.splitlines() if not line.startswith("-----"))
    try:
        return base64.b64decode(body, validate=True)
    except (binascii.Error, ValueError) as error:
        raise ValueError("повреждённый PEM-ключ") from error


def _read_private(path: Path) -> int:
    raw = _unpem(path.read_text(encoding="utf-8"))
    if len(raw) != 32:
        raise ValueError("приватный ключ должен содержать ровно 32 байта")
    private = int.from_bytes(raw, "big")
    if not 1 <= private < N:
        raise ValueError("приватный ключ вне диапазона P-256")
    return private


def _read_public(path: Path) -> Tuple[int, int]:
    raw = _unpem(path.read_text(encoding="utf-8"))
    if len(raw) != 64:
        raise ValueError("публичный ключ должен содержать 64 байта X+Y")
    public = int.from_bytes(raw[:32], "big"), int.from_bytes(raw[32:], "big")
    if not _is_on_curve(public):
        raise ValueError("публичный ключ не лежит на кривой P-256")
    return public


def keygen(out_prefix: Path) -> Tuple[Path, Path]:
    private = int.from_bytes(os.urandom(32), "big") % (N - 1) + 1
    public = _mul(private)
    if public is None:  # невозможно для корректного scalar, но лучше не писать битые ключи
        raise RuntimeError("не удалось вычислить публичный ключ")
    out_prefix.parent.mkdir(parents=True, exist_ok=True)
    key_path = Path(str(out_prefix) + ".key.pem")
    pub_path = Path(str(out_prefix) + ".pub.pem")
    key_path.write_text(_pem("TTYDYN PRIVATE KEY", private.to_bytes(32, "big")), encoding="utf-8")
    pub_path.write_text(
        _pem("TTYDYN PUBLIC KEY", public[0].to_bytes(32, "big") + public[1].to_bytes(32, "big")),
        encoding="utf-8",
    )
    os.chmod(key_path, 0o600)
    return key_path, pub_path


def _public_from_private(path: Path) -> Tuple[int, int]:
    public = _mul(_read_private(path))
    if public is None:
        raise ValueError("не удалось вычислить публичный ключ")
    return public


def _public_bytes(public: Tuple[int, int]) -> bytes:
    return public[0].to_bytes(32, "big") + public[1].to_bytes(32, "big")


# ---------------------------------------------------------------------------
# DEX
# ---------------------------------------------------------------------------
def _read_uleb128(blob: bytes, offset: int) -> Tuple[int, int]:
    result = shift = 0
    for _ in range(5):
        if offset >= len(blob):
            raise ValueError("обрезанный ULEB128 в DEX")
        byte = blob[offset]
        offset += 1
        result |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return result, offset
        shift += 7
    raise ValueError("слишком длинный ULEB128 в DEX")


def dex_classes(dex: bytes) -> List[str]:
    """Возвращает список классов, объявленных в DEX."""
    if len(dex) < 112 or dex[:4] != b"dex\n" or dex[4:7] not in (b"035", b"037", b"038", b"039"):
        raise ValueError("неподдерживаемый или повреждённый DEX-файл")
    try:
        header = struct.unpack_from("<20I", dex, 32)
        file_size, header_size = header[0], header[1]
        if header_size != 112 or file_size > len(dex):
            raise ValueError("некорректный заголовок DEX")
        string_ids_size, string_ids_off = header[6], header[7]
        type_ids_size, type_ids_off = header[8], header[9]
        class_defs_size, class_defs_off = header[16], header[17]
        if type_ids_size == 0:
            return []
        if string_ids_off + string_ids_size * 4 > len(dex):
            raise ValueError("выход за границы string_ids в DEX")
        if type_ids_off + type_ids_size * 4 > len(dex):
            raise ValueError("выход за границы type_ids в DEX")
        if class_defs_off + class_defs_size * 32 > len(dex):
            raise ValueError("выход за границы class_defs в DEX")

        strings: List[str] = []
        for index in range(string_ids_size):
            string_off = struct.unpack_from("<I", dex, string_ids_off + 4 * index)[0]
            utf16_size, data_off = _read_uleb128(dex, string_off)
            end = dex.find(b"\x00", data_off)
            if end < 0:
                raise ValueError("строка без завершающего нуля в DEX")
            raw = dex[data_off:end]
            # MUTF-8 обычно совпадает с UTF-8 для имён классов. replace нужен
            # только для диагностики повреждённого декса.
            strings.append(raw.decode("utf-8", "replace"))

        names: List[str] = []
        for index in range(class_defs_size):
            class_def_off = class_defs_off + 32 * index
            type_index = struct.unpack_from("<I", dex, class_def_off)[0]
            if type_index >= type_ids_size:
                raise ValueError("class_def с неверным type_idx")
            string_index = struct.unpack_from("<I", dex, type_ids_off + 4 * type_index)[0]
            if string_index >= len(strings):
                raise ValueError("type_id с неверным descriptor_idx")
            descriptor = strings[string_index]
            names.append(descriptor[1:-1].replace("/", ".") if descriptor.startswith("L") else descriptor)
        return names
    except struct.error as error:
        raise ValueError("обрезанный заголовок DEX") from error


# ---------------------------------------------------------------------------
# Сборка и контейнер
# ---------------------------------------------------------------------------
def build_manifest(
    dex_files: Sequence[Tuple[str, bytes]],
    variant: str,
    payload_type: str,
    dynamic_version: int,
    stable_api_version: int,
) -> Dict[str, object]:
    if not dex_files:
        raise ValueError("нужен хотя бы один DEX-файл")
    main_name, main_bytes = dex_files[0]
    entries = []
    hashes: Dict[str, str] = {}
    for name, blob in dex_files:
        digest = hashlib.sha256(blob).hexdigest()
        entries.append({"name": name, "payload": name, "targetSha256": digest, "targetSize": len(blob)})
        hashes[name] = digest
    return {
        "payloadType": payload_type,
        "variant": variant,
        "dynamicVersion": dynamic_version,
        "stableApiVersion": stable_api_version,
        "targetDexSha256": hashlib.sha256(main_bytes).hexdigest(),
        "targetSize": len(main_bytes),
        "dexFiles": entries,
        "hashes": hashes,
    }


def canonical_manifest(manifest: Dict[str, object]) -> bytes:
    return json.dumps(manifest, separators=(",", ":"), ensure_ascii=False, allow_nan=False).encode("utf-8")


def _write_zip(path: Path, manifest_bytes: bytes, dex_files: Sequence[Tuple[str, bytes]], signature: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        def put(name: str, payload: bytes) -> None:
            info = zipfile.ZipInfo(name, date_time=ZIP_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 0
            archive.writestr(info, payload)

        put(MANIFEST, manifest_bytes)
        for name, blob in dex_files:
            put(name, blob)
        if signature:
            put(SIGNATURE, signature)


def pack(
    dex_paths: Sequence[Path],
    out: Path,
    variant: str,
    payload_type: str,
    dynamic_version: Optional[int],
    stable_api_version: int,
    key: Optional[Path],
    name_from_hash: bool,
) -> Path:
    if not dex_paths:
        raise ValueError("не передан DEX-файл")
    dex_files: List[Tuple[str, bytes]] = []
    for index, path in enumerate(dex_paths):
        blob = path.read_bytes()
        if len(blob) < 4 or blob[:4] != b"dex\n":
            raise ValueError("{}: не DEX-файл".format(path))
        dex_files.append(("classes.dex" if index == 0 else "classes{}.dex".format(index + 1), blob))

    if dynamic_version is None:
        dynamic_version = int(time.time())
    if dynamic_version < 0 or stable_api_version < 0:
        raise ValueError("версии модуля не могут быть отрицательными")
    manifest = build_manifest(dex_files, variant, payload_type, dynamic_version, stable_api_version)
    manifest_bytes = canonical_manifest(manifest)
    signature = ecdsa_sign(_read_private(key), manifest_bytes) if key is not None else b""

    if name_from_hash:
        short = str(manifest["targetDexSha256"])[:12]
        build_id = hashlib.sha256(manifest_bytes).hexdigest()[:16]
        out = out.with_name("tty-{}-{}-{}_{}.ttydyn".format(variant, short, payload_type, build_id))

    # Write beside the destination and replace it only after ZIP is complete.
    out.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(prefix=out.name + ".", suffix=".tmp", dir=str(out.parent))
    os.close(fd)
    temporary = Path(temporary_name)
    try:
        _write_zip(temporary, manifest_bytes, dex_files, signature)
        temporary.replace(out)
    finally:
        temporary.unlink(missing_ok=True)
    return out


# ---------------------------------------------------------------------------
# Проверка / инспекция
# ---------------------------------------------------------------------------
def read_package(path: Path) -> Tuple[Dict[str, object], bytes, Dict[str, bytes], bytes]:
    try:
        with zipfile.ZipFile(path) as archive:
            infos = archive.infolist()
            names = [info.filename for info in infos]
            if len(names) != len(set(names)):
                raise ValueError("архив содержит повторяющиеся записи")
            if MANIFEST not in names:
                raise ValueError("нет manifest.json")
            manifest_bytes = archive.read(MANIFEST)
            try:
                manifest = json.loads(manifest_bytes.decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError) as error:
                raise ValueError("manifest.json не является корректным UTF-8 JSON") from error
            if not isinstance(manifest, dict):
                raise ValueError("manifest.json должен содержать JSON-объект")
            payloads = {name: archive.read(name) for name in names if name.endswith(".dex")}
            signature = archive.read(SIGNATURE) if SIGNATURE in names else b""
    except zipfile.BadZipFile as error:
        raise ValueError("повреждённый ZIP-контейнер") from error
    return manifest, manifest_bytes, payloads, signature


def _is_sha256(value: object) -> bool:
    return isinstance(value, str) and bool(re.fullmatch(r"[0-9a-fA-F]{64}", value))


def check_hashes(manifest: Dict[str, object], payloads: Dict[str, bytes]) -> List[str]:
    problems: List[str] = []
    entries = manifest.get("dexFiles")
    if not isinstance(entries, list) or not entries:
        return ["manifest.dexFiles отсутствует или пуст"]

    listed_payloads: set[str] = set()
    first_payload: Optional[bytes] = None
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            problems.append("dexFiles[{}] не является объектом".format(index))
            continue
        payload_name = entry.get("payload")
        if not isinstance(payload_name, str) or not payload_name:
            problems.append("dexFiles[{}].payload некорректен".format(index))
            continue
        if payload_name in listed_payloads:
            problems.append("повторяющийся payload: {}".format(payload_name))
        listed_payloads.add(payload_name)
        payload = payloads.get(payload_name)
        if payload is None:
            problems.append("нет payload {}".format(payload_name))
            continue
        if first_payload is None:
            first_payload = payload
        digest = entry.get("targetSha256")
        if not _is_sha256(digest):
            problems.append("некорректный targetSha256: {}".format(payload_name))
        elif hashlib.sha256(payload).hexdigest().lower() != str(digest).lower():
            problems.append("sha256 не совпадает: {}".format(payload_name))
        size = entry.get("targetSize")
        if not isinstance(size, int) or isinstance(size, bool) or size != len(payload):
            problems.append("размер не совпадает: {}".format(payload_name))

    for name in sorted(set(payloads) - listed_payloads):
        problems.append("лишний DEX без записи в manifest: {}".format(name))

    hashes = manifest.get("hashes")
    if not isinstance(hashes, dict):
        problems.append("manifest.hashes отсутствует или некорректен")
    else:
        for name, digest in hashes.items():
            if name not in payloads:
                problems.append("hashes ссылается на отсутствующий payload: {}".format(name))
            elif not _is_sha256(digest) or hashlib.sha256(payloads[name]).hexdigest().lower() != str(digest).lower():
                problems.append("hashes[{}] не совпадает".format(name))
        for name in sorted(listed_payloads - set(hashes)):
            problems.append("для {} нет записи в manifest.hashes".format(name))

    if first_payload is not None:
        target_hash = manifest.get("targetDexSha256")
        if not _is_sha256(target_hash) or hashlib.sha256(first_payload).hexdigest().lower() != str(target_hash).lower():
            problems.append("targetDexSha256 не совпадает с первым DEX")
        if manifest.get("targetSize") != len(first_payload):
            problems.append("targetSize не совпадает с первым DEX")
    return problems


def _timestamp(value: object) -> str:
    if not isinstance(value, int) or isinstance(value, bool):
        return "?"
    try:
        return time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime(value))
    except (OverflowError, OSError, ValueError):
        return "?"


def cmd_inspect(args: argparse.Namespace) -> int:
    path = Path(args.package)
    manifest, _manifest_bytes, payloads, signature = read_package(path)
    print("файл           : {} ({} байт)".format(path.name, path.stat().st_size))
    print("payloadType    : {}".format(manifest.get("payloadType")))
    print("variant        : {}".format(manifest.get("variant")))
    for field in ("dynamicVersion", "stableApiVersion"):
        print("{:<15}: {} ({})".format(field, manifest.get(field), _timestamp(manifest.get(field))))
    print("targetDexSha256: {}".format(manifest.get("targetDexSha256")))
    print("dex-файлы      : {}".format(", ".join(sorted(payloads)) or "нет"))
    problems = check_hashes(manifest, payloads)
    print("целостность    : {}".format("OK" if not problems else "; ".join(problems)))
    if signature:
        try:
            r, s = der_decode(signature)
            print("подпись        : ECDSA DER, {} байт (r/s по 32 байта: {})".format(
                len(signature), "да" if r.bit_length() <= 256 and s.bit_length() <= 256 else "нет"))
        except ValueError as error:
            print("подпись        : не разобрана ({})".format(error))
            problems.append("некорректная signature.der")
    else:
        print("подпись        : отсутствует")

    for name in sorted(payloads):
        classes = dex_classes(payloads[name])
        entry = "com.windukk.mod.Main" in classes
        print("{:<15}: {} классов, точка входа com.windukk.mod.Main — {}".format(
            name, len(classes), "есть" if entry else "НЕТ"))
        if args.classes:
            own = [class_name for class_name in classes
                   if not re.match(r"^(Z\.|ni\.shikatu|kotlin|com\.android\.tools)", class_name)]
            for item in sorted(own)[:args.classes]:
                print("    {}".format(item))
    return 0 if not problems else 1


def cmd_pack(args: argparse.Namespace) -> int:
    out = pack(
        [Path(path) for path in args.dex],
        Path(args.out),
        args.variant,
        args.payload_type,
        args.dynamic_version,
        args.stable_api_version,
        Path(args.key) if args.key else None,
        args.name_from_hash,
    )
    digest = hashlib.sha256(out.read_bytes()).hexdigest()
    print("собрано: {}\nsha256 : {}".format(out, digest))
    return 0


def cmd_keygen(args: argparse.Namespace) -> int:
    key_path, pub_path = keygen(Path(args.out))
    print("приватный ключ: {}\nпубличный ключ: {}".format(key_path, pub_path))
    return 0


def cmd_keycheck(args: argparse.Namespace) -> int:
    derived = _public_from_private(Path(args.key))
    if args.pub:
        actual = _read_public(Path(args.pub))
        if actual != derived:
            print("ключи: FAIL — публичный ключ не соответствует приватному")
            return 1
        print("ключи: OK")
    else:
        print(base64.b64encode(_public_bytes(derived)).decode("ascii"))
    return 0


def cmd_verify(args: argparse.Namespace) -> int:
    path = Path(args.package)
    manifest, manifest_bytes, payloads, signature = read_package(path)
    problems = check_hashes(manifest, payloads)
    if problems:
        print("целостность: FAIL —", "; ".join(problems))
        return 1
    print("целостность: OK")
    if not signature:
        print("подпись: отсутствует")
        return 1
    try:
        r, s = der_decode(signature)
        if not (1 <= r < N and 1 <= s < N):
            raise ValueError("r/s вне диапазона P-256")
    except ValueError as error:
        print("подпись: FAIL —", error)
        return 1
    if not args.pub:
        print("подпись: есть, но публичный ключ не передан (--pub)")
        return 0
    ok = ecdsa_verify(_read_public(Path(args.pub)), manifest_bytes, signature)
    print("подпись: {}".format("OK" if ok else "FAIL"))
    return 0 if ok else 1


def main() -> int:
    parser = argparse.ArgumentParser(description="инструмент для модулей TikTok You (.ttydyn)")
    sub = parser.add_subparsers(dest="command", required=True)

    inspect = sub.add_parser("inspect", help="показать содержимое и проверить хеши")
    inspect.add_argument("package")
    inspect.add_argument("--classes", type=int, default=0, help="показать N необфусцированных классов")
    inspect.set_defaults(func=cmd_inspect)

    pack_parser = sub.add_parser("pack", help="собрать .ttydyn из DEX")
    pack_parser.add_argument("--dex", nargs="+", required=True)
    pack_parser.add_argument("--out", required=True)
    pack_parser.add_argument("--variant", default="release")
    pack_parser.add_argument("--payload-type", default="full")
    pack_parser.add_argument("--dynamic-version", type=int, default=None)
    pack_parser.add_argument("--stable-api-version", type=int, default=1788704106)
    pack_parser.add_argument("--key", default=None, help="PEM приватного ключа (ttydyn keygen)")
    pack_parser.add_argument("--name-from-hash", action="store_true",
                             help="назвать файл как официальную сборку tty-<variant>-<hash>-<type>_<id>.ttydyn")
    pack_parser.set_defaults(func=cmd_pack)

    keygen_parser = sub.add_parser("keygen", help="создать пару ключей P-256")
    keygen_parser.add_argument("--out", required=True, help="префикс пути, например keys/dev")
    keygen_parser.set_defaults(func=cmd_keygen)

    keycheck_parser = sub.add_parser("keycheck", help="проверить соответствие приватного и публичного ключей")
    keycheck_parser.add_argument("--key", required=True)
    keycheck_parser.add_argument("--pub")
    keycheck_parser.set_defaults(func=cmd_keycheck)

    verify_parser = sub.add_parser("verify", help="проверить хеши и подпись")
    verify_parser.add_argument("package")
    verify_parser.add_argument("--pub", default=None)
    verify_parser.set_defaults(func=cmd_verify)

    args = parser.parse_args()
    try:
        return args.func(args)
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print("ERROR: {}".format(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
