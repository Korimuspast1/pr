#!/usr/bin/env python3
"""ttydyn — инспектор, сборщик и подписчик модулей TikTok You (.ttydyn).

Формат разобран по официальному модулю, описание: docs/ttydyn-format.md.

    python3 tools/ttydyn.py inspect  module.ttydyn
    python3 tools/ttydyn.py keygen   --out keys/dev
    python3 tools/ttydyn.py pack     --dex build/classes.dex --out release/profile-flex.ttydyn \
                                     --key keys/dev.key.pem
    python3 tools/ttydyn.py verify   release/profile-flex.ttydyn --pub keys/dev.pub.pem

Зависимостей нет: ECDSA P-256 реализован здесь же.
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
import time
import zipfile
from pathlib import Path
from typing import Dict, List, Optional, Tuple

MANIFEST = "manifest.json"
SIGNATURE = "signature.der"
ZIP_TIME = (1980, 1, 1, 0, 0, 0)  # как в официальном модуле — детерминированная сборка

# ---------------------------------------------------------------------------
# ECDSA P-256 (secp256r1), минимальная реализация
# ---------------------------------------------------------------------------
P = 0xFFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF
A = P - 3
B = 0x5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B
GX = 0x6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296
GY = 0x4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5
N = 0xFFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551


def _inv(x: int, m: int) -> int:
    return pow(x, m - 2, m)


def _add(p1, p2):
    if p1 is None:
        return p2
    if p2 is None:
        return p1
    x1, y1 = p1
    x2, y2 = p2
    if x1 == x2 and (y1 + y2) % P == 0:
        return None
    if p1 == p2:
        lam = (3 * x1 * x1 + A) * _inv(2 * y1, P) % P
    else:
        lam = (y2 - y1) * _inv(x2 - x1, P) % P
    x3 = (lam * lam - x1 - x2) % P
    return (x3, (lam * (x1 - x3) - y1) % P)


def _mul(k: int, point=(GX, GY)):
    result = None
    addend = point
    while k:
        if k & 1:
            result = _add(result, addend)
        addend = _add(addend, addend)
        k >>= 1
    return result


def _der_int(value: int) -> bytes:
    raw = value.to_bytes((value.bit_length() + 8) // 8 or 1, "big")
    return b"\x02" + bytes([len(raw)]) + raw


def der_encode(r: int, s: int) -> bytes:
    body = _der_int(r) + _der_int(s)
    return b"\x30" + bytes([len(body)]) + body


def der_decode(blob: bytes) -> Tuple[int, int]:
    if len(blob) < 8 or blob[0] != 0x30:
        raise ValueError("не DER SEQUENCE")
    offset = 2
    values = []
    for _ in range(2):
        if blob[offset] != 0x02:
            raise ValueError("не DER INTEGER")
        length = blob[offset + 1]
        values.append(int.from_bytes(blob[offset + 2:offset + 2 + length], "big"))
        offset += 2 + length
    return values[0], values[1]


def _rfc6979_k(priv: int, digest: bytes) -> int:
    # Детерминированный nonce: сборка одного и того же модуля даёт один и тот же файл.
    v = b"\x01" * 32
    k = b"\x00" * 32
    priv_bytes = priv.to_bytes(32, "big")
    for prefix in (b"\x00", b"\x01"):
        k = hmac.new(k, v + prefix + priv_bytes + digest, hashlib.sha256).digest()
        v = hmac.new(k, v, hashlib.sha256).digest()
    while True:
        v = hmac.new(k, v, hashlib.sha256).digest()
        candidate = int.from_bytes(v, "big")
        if 1 <= candidate < N:
            return candidate
        k = hmac.new(k, v + b"\x00", hashlib.sha256).digest()
        v = hmac.new(k, v, hashlib.sha256).digest()


def ecdsa_sign(priv: int, message: bytes) -> bytes:
    digest = hashlib.sha256(message).digest()
    z = int.from_bytes(digest, "big")
    while True:
        k = _rfc6979_k(priv, digest)
        point = _mul(k)
        r = point[0] % N
        if r == 0:
            continue
        s = _inv(k, N) * (z + r * priv) % N
        if s == 0:
            continue
        if s > N // 2:  # low-s, как принято в Android-верификаторах
            s = N - s
        return der_encode(r, s)


def ecdsa_verify(pub: Tuple[int, int], message: bytes, signature: bytes) -> bool:
    try:
        r, s = der_decode(signature)
    except ValueError:
        return False
    if not (1 <= r < N and 1 <= s < N):
        return False
    z = int.from_bytes(hashlib.sha256(message).digest(), "big")
    w = _inv(s, N)
    point = _add(_mul(z * w % N), _mul(r * w % N, pub))
    return point is not None and point[0] % N == r


# --- хранение ключей: простой PEM-контейнер (raw scalar / raw point) --------
def _pem(tag: str, payload: bytes) -> str:
    body = base64.b64encode(payload).decode()
    lines = [body[i:i + 64] for i in range(0, len(body), 64)]
    return "-----BEGIN {0}-----\n{1}\n-----END {0}-----\n".format(tag, "\n".join(lines))


def _unpem(text: str) -> bytes:
    body = "".join(line for line in text.splitlines() if not line.startswith("-----"))
    return base64.b64decode(body)


def keygen(out_prefix: Path) -> Tuple[Path, Path]:
    priv = int.from_bytes(os.urandom(32), "big") % (N - 1) + 1
    pub = _mul(priv)
    out_prefix.parent.mkdir(parents=True, exist_ok=True)
    key_path = Path(str(out_prefix) + ".key.pem")
    pub_path = Path(str(out_prefix) + ".pub.pem")
    key_path.write_text(_pem("TTYDYN PRIVATE KEY", priv.to_bytes(32, "big")), encoding="utf-8")
    pub_path.write_text(
        _pem("TTYDYN PUBLIC KEY", pub[0].to_bytes(32, "big") + pub[1].to_bytes(32, "big")),
        encoding="utf-8",
    )
    os.chmod(key_path, 0o600)
    return key_path, pub_path


def load_private(path: Path) -> int:
    return int.from_bytes(_unpem(path.read_text(encoding="utf-8")), "big")


def load_public(path: Path) -> Tuple[int, int]:
    raw = _unpem(path.read_text(encoding="utf-8"))
    return int.from_bytes(raw[:32], "big"), int.from_bytes(raw[32:64], "big")


# ---------------------------------------------------------------------------
# DEX
# ---------------------------------------------------------------------------
def dex_classes(dex: bytes) -> List[str]:
    """Возвращает список классов, объявленных в dex (для проверки точки входа)."""
    if dex[:4] != b"dex\n":
        raise ValueError("не DEX-файл")
    header = struct.unpack_from("<20I", dex, 32)
    string_ids_size, string_ids_off = header[6], header[7]
    type_ids_off = header[9]
    class_defs_size, class_defs_off = header[16], header[17]

    def uleb(off: int) -> Tuple[int, int]:
        result = shift = 0
        while True:
            byte = dex[off]
            off += 1
            result |= (byte & 0x7F) << shift
            shift += 7
            if not byte & 0x80:
                return result, off

    strings = []
    for index in range(string_ids_size):
        off = struct.unpack_from("<I", dex, string_ids_off + 4 * index)[0]
        size, data_off = uleb(off)
        raw = dex[data_off:data_off + size * 4]
        end = raw.find(b"\x00")
        strings.append(raw[: end if end >= 0 else len(raw)].decode("utf-8", "replace"))

    names = []
    for index in range(class_defs_size):
        type_index = struct.unpack_from("<I", dex, class_defs_off + 32 * index)[0]
        string_index = struct.unpack_from("<I", dex, type_ids_off + 4 * type_index)[0]
        descriptor = strings[string_index]
        names.append(descriptor[1:-1].replace("/", ".") if descriptor.startswith("L") else descriptor)
    return names


# ---------------------------------------------------------------------------
# Сборка
# ---------------------------------------------------------------------------
def build_manifest(dex_files: List[Tuple[str, bytes]], variant: str, payload_type: str,
                   dynamic_version: int, stable_api_version: int) -> Dict:
    main_name, main_bytes = dex_files[0]
    entries = []
    hashes = {}
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


def canonical_manifest(manifest: Dict) -> bytes:
    return json.dumps(manifest, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def pack(dex_paths: List[Path], out: Path, variant: str, payload_type: str,
         dynamic_version: Optional[int], stable_api_version: int,
         key: Optional[Path], name_from_hash: bool) -> Path:
    dex_files = []
    for index, path in enumerate(dex_paths):
        blob = path.read_bytes()
        if blob[:4] != b"dex\n":
            raise SystemExit("{}: не DEX-файл".format(path))
        dex_files.append(("classes.dex" if index == 0 else "classes{}.dex".format(index + 1), blob))

    manifest = build_manifest(dex_files, variant, payload_type,
                              dynamic_version or int(time.time()), stable_api_version)
    manifest_bytes = canonical_manifest(manifest)

    if key is not None:
        signature = ecdsa_sign(load_private(key), manifest_bytes)
    else:
        signature = b""

    if name_from_hash:
        short = manifest["targetDexSha256"][:12]
        build_id = hashlib.sha256(manifest_bytes).hexdigest()[:20]
        out = out.with_name("tty-{}-{}-{}_{}.ttydyn".format(variant, short, payload_type, build_id))

    out.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(out, "w", compression=zipfile.ZIP_DEFLATED) as archive:
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
    return out


# ---------------------------------------------------------------------------
# Проверка / инспекция
# ---------------------------------------------------------------------------
def read_package(path: Path) -> Tuple[Dict, bytes, Dict[str, bytes], bytes]:
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if MANIFEST not in names:
            raise SystemExit("{}: нет manifest.json".format(path))
        manifest_bytes = archive.read(MANIFEST)
        manifest = json.loads(manifest_bytes)
        payloads = {name: archive.read(name) for name in names if name.endswith(".dex")}
        signature = archive.read(SIGNATURE) if SIGNATURE in names else b""
    return manifest, manifest_bytes, payloads, signature


def check_hashes(manifest: Dict, payloads: Dict[str, bytes]) -> List[str]:
    problems = []
    for entry in manifest.get("dexFiles", []):
        payload = payloads.get(entry["payload"])
        if payload is None:
            problems.append("нет payload {}".format(entry["payload"]))
            continue
        if hashlib.sha256(payload).hexdigest() != entry["targetSha256"]:
            problems.append("sha256 не совпадает: {}".format(entry["payload"]))
        if len(payload) != entry["targetSize"]:
            problems.append("размер не совпадает: {}".format(entry["payload"]))
    for name, digest in manifest.get("hashes", {}).items():
        if name in payloads and hashlib.sha256(payloads[name]).hexdigest() != digest:
            problems.append("hashes[{}] не совпадает".format(name))
    return problems


def cmd_inspect(args) -> int:
    path = Path(args.package)
    manifest, manifest_bytes, payloads, signature = read_package(path)
    print("файл           : {} ({} байт)".format(path.name, path.stat().st_size))
    print("payloadType    : {}".format(manifest.get("payloadType")))
    print("variant        : {}".format(manifest.get("variant")))
    for field in ("dynamicVersion", "stableApiVersion"):
        value = manifest.get(field)
        stamp = time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime(value)) if isinstance(value, int) else "?"
        print("{:<15}: {} ({})".format(field, value, stamp))
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
    else:
        print("подпись        : отсутствует")

    for name in sorted(payloads):
        classes = dex_classes(payloads[name])
        entry = "com.windukk.mod.Main" in classes
        print("{:<15}: {} классов, точка входа com.windukk.mod.Main — {}".format(
            name, len(classes), "есть" if entry else "НЕТ"))
        if args.classes:
            own = [c for c in classes if not re.match(r"^(Z\.|ni\.shikatu|kotlin|com\.android\.tools)", c)]
            for item in sorted(own)[: args.classes]:
                print("    {}".format(item))
    return 0 if not problems else 1


def cmd_pack(args) -> int:
    out = pack(
        [Path(p) for p in args.dex],
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


def cmd_keygen(args) -> int:
    key_path, pub_path = keygen(Path(args.out))
    print("приватный ключ: {}\nпубличный ключ: {}".format(key_path, pub_path))
    return 0


def cmd_verify(args) -> int:
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
    if not args.pub:
        print("подпись: есть, но публичный ключ не передан (--pub)")
        return 0
    ok = ecdsa_verify(load_public(Path(args.pub)), manifest_bytes, signature)
    print("подпись: {}".format("OK" if ok else "FAIL"))
    return 0 if ok else 1


def main() -> int:
    parser = argparse.ArgumentParser(description="инструмент для модулей TikTok You (.ttydyn)")
    sub = parser.add_subparsers(dest="command", required=True)

    inspect = sub.add_parser("inspect", help="показать содержимое и проверить хеши")
    inspect.add_argument("package")
    inspect.add_argument("--classes", type=int, default=0, help="показать N необфусцированных классов")
    inspect.set_defaults(func=cmd_inspect)

    pack_parser = sub.add_parser("pack", help="собрать .ttydyn из dex")
    pack_parser.add_argument("--dex", nargs="+", required=True)
    pack_parser.add_argument("--out", required=True)
    pack_parser.add_argument("--variant", default="release")
    pack_parser.add_argument("--payload-type", default="full")
    pack_parser.add_argument("--dynamic-version", type=int, default=None)
    pack_parser.add_argument("--stable-api-version", type=int, default=1788704106)
    pack_parser.add_argument("--key", default=None, help="PEM приватного ключа (ttydyn keygen)")
    pack_parser.add_argument("--name-from-hash", action="store_true",
                             help="назвать файл как официальные сборки: tty-<variant>-<hash>-<type>_<id>.ttydyn")
    pack_parser.set_defaults(func=cmd_pack)

    keygen_parser = sub.add_parser("keygen", help="создать пару ключей P-256")
    keygen_parser.add_argument("--out", required=True, help="префикс пути, например keys/dev")
    keygen_parser.set_defaults(func=cmd_keygen)

    verify_parser = sub.add_parser("verify", help="проверить хеши и подпись")
    verify_parser.add_argument("package")
    verify_parser.add_argument("--pub", default=None)
    verify_parser.set_defaults(func=cmd_verify)

    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
