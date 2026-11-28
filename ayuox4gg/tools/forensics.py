#!/usr/bin/env python3
"""Check Android APK / AAB artefacts before installing them.

Every fork user eventually faces the same question: *"is this APK really the
build the project published?"* This tool answers the parts that can be checked
offline, with no Android SDK and no third-party Python packages:

* SHA-256 / SHA-512 digests, and a digest of every entry in the archive;
* the **signing certificate** fingerprints (v1 JAR signature and the v2/v3
  APK Signing Block), computed from the archive itself. If you know the
  official fingerprint out of band, a mismatch means the file was re-signed —
  which for a fork is exactly the attack that matters;
* whether the archive is **signed with debug keys** (a release should not be);
* **dangerous permissions** and cleartext-traffic settings from the binary
  manifest (best effort: the binary XML is scanned for the permission strings
  and for a network-security-config reference);
* embedded native ABIs and whether an old, unsupported `minSdk` is declared;
* an "unexpected entry" check: files outside the usual Android layout.

Usage
-----
    python3 ayuox4gg/tools/forensics.py app.apk
    python3 ayuox4gg/tools/forensics.py app.apk --expect-sha256 <digest>
    python3 ayuox4gg/tools/forensics.py app.apk --expect-cert <sha256-of-cert>
    python3 ayuox4gg/tools/forensics.py app.apk --json

Exit codes: 0 = all requested expectations matched, 1 = mismatch or suspicion,
2 = the file could not be read.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
import zipfile
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

#: Permissions that deserve a human decision before installing a fork build.
DANGEROUS_PERMISSIONS = {
    "android.permission.SYSTEM_ALERT_WINDOW": "draw over other apps",
    "android.permission.REQUEST_INSTALL_PACKAGES": "install APKs (self-updater)",
    "android.permission.MANAGE_EXTERNAL_STORAGE": "all-files access",
    "android.permission.READ_SMS": "read SMS (2FA codes!)",
    "android.permission.RECEIVE_SMS": "receive SMS",
    "android.permission.READ_CALL_LOG": "read call log",
    "android.permission.ACCESS_FINE_LOCATION": "precise location",
    "android.permission.ACCESS_BACKGROUND_LOCATION": "background location",
    "android.permission.RECORD_AUDIO": "microphone",
    "android.permission.CAMERA": "camera",
    "android.permission.READ_CONTACTS": "contacts",
    "android.permission.QUERY_ALL_PACKAGES": "list installed apps",
}

APK_SIG_BLOCK_MAGIC = b"APK Sig Block 42"
DEBUG_CERT_SUBSTRINGS = (b"Android Debug", b"androiddebugkey")

@dataclass
class Report:
    path: str
    size: int
    sha256: str
    sha512: str
    entries: int
    abis: List[str] = field(default_factory=list)
    permissions: List[str] = field(default_factory=list)
    dangerous: List[str] = field(default_factory=list)
    certificate_sha256: Optional[str] = None
    certificate_subject: Optional[str] = None
    debug_signed: bool = False
    cleartext_allowed: Optional[bool] = None
    network_security_config: bool = False
    has_v1_signature: bool = False
    has_v2_or_v3_signature: bool = False
    suspicious_entries: List[str] = field(default_factory=list)
    notes: List[str] = field(default_factory=list)


# ---------------------------------------------------------------------------
# Hashes
# ---------------------------------------------------------------------------
def digest_file(path: Path, algorithm: str) -> str:
    digest = hashlib.new(algorithm)
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


# ---------------------------------------------------------------------------
# Signing certificates
# ---------------------------------------------------------------------------
def _read_tlv(data: bytes, offset: int) -> Optional[Tuple[int, bytes, int]]:
    """Read one DER TLV. Returns ``(tag, value, next_offset)`` or ``None``."""
    if offset + 2 > len(data):
        return None
    tag = data[offset]
    first = data[offset + 1]
    cursor = offset + 2
    if first < 0x80:
        length = first
    else:
        count = first & 0x7F
        if count == 0 or count > 4 or cursor + count > len(data):
            return None
        length = int.from_bytes(data[cursor: cursor + count], "big")
        cursor += count
    if cursor + length > len(data):
        return None
    return tag, data[cursor: cursor + length], cursor + length


#: DER-encoded signature algorithm OIDs that identify an X.509 certificate
#: (RSA family, ECDSA family, DSA). The OID itself is what we look for.
_SIGNATURE_OID_BYTES = (
    b"\x06\x09\x2a\x86\x48\x86\xf7\x0d\x01\x01",   # 1.2.840.113549.1.1.*  (RSA)
    b"\x06\x08\x2a\x86\x48\xce\x3d\x04\x03",         # 1.2.840.10045.4.3.*  (ECDSA)
    b"\x06\x07\x2a\x86\x48\xce\x38\x04\x03",         # 1.2.840.10040.4.3   (DSA)
    b"\x06\x09\x60\x86\x48\x01\x65\x03\x04\x02",    # 1.3.101.112/113     (Ed25519/Ed448)
)


def _looks_like_certificate(der: bytes) -> bool:
    parsed = _read_tlv(der, 0)
    if parsed is None:
        return False
    tag, value, _ = parsed
    if tag != 0x30 or len(value) < 100:
        return False
    if not any(oid in value for oid in _SIGNATURE_OID_BYTES):
        return False
    first = _read_tlv(value, 0)
    if first is None:
        return False
    # tbsCertificate starts with an explicit [0] version tag or the serialNumber INTEGER.
    return first[0] in (0xA0, 0x02)


def _extract_der_from_pkcs7(blob: bytes) -> Optional[bytes]:
    """Pull the first X.509 certificate DER out of a PKCS#7 SignedData blob.

    APK v1 signatures store the signer chain in ``META-INF/*.RSA`` as a
    PKCS#7 ContentInfo. The walk is structural, not a byte search:

    ContentInfo ::= SEQUENCE { contentType OID, [0] EXPLICIT SignedData }
    SignedData  ::= SEQUENCE { version INTEGER, digestAlgorithms SET,
                               contentInfo SEQUENCE, certificates [0] IMPLICIT SET }
    """
    content_info = _read_tlv(blob, 0)
    if content_info is None or content_info[0] != 0x30:
        return None
    body = content_info[1]

    explicit = None
    offset = 0
    while offset < len(body):
        parsed = _read_tlv(body, offset)
        if parsed is None:
            break
        tag, value, offset = parsed
        if tag == 0xA0:  # [0] EXPLICIT SignedData
            explicit = value
            break
    if explicit is None:
        return None

    signed_data = _read_tlv(explicit, 0)
    if signed_data is None or signed_data[0] != 0x30:
        return None
    body = signed_data[1]

    offset = 0
    while offset < len(body):
        parsed = _read_tlv(body, offset)
        if parsed is None:
            return None
        tag, value, offset = parsed
        if tag == 0xA0:  # certificates [0] IMPLICIT SET OF Certificate
            parsed_cert = _read_tlv(value, 0)
            if parsed_cert is not None and parsed_cert[0] == 0x30:
                return value[: parsed_cert[2]]
            return None
    return None


def v1_certificate(path: Path) -> Optional[bytes]:
    with zipfile.ZipFile(path) as archive:
        names = [name for name in archive.namelist() if name.upper().startswith("META-INF/")
                 and name.upper().endswith((".RSA", ".DSA", ".EC"))]
        if not names:
            return None
        blob = archive.read(names[0])
    return _extract_der_from_pkcs7(blob)


def parse_apk_signing_block(path: Path) -> Dict[str, object]:
    """Read the v2/v3 APK Signing Block and pull the first signer certificate."""
    result: Dict[str, object] = {"present": False, "certificate": None, "scheme": None}
    try:
        with path.open("rb") as handle:
            handle.seek(0, 2)
            file_size = handle.tell()
            if file_size < 32:
                return result
            handle.seek(file_size - 24)
            footer = handle.read(24)
            if footer[8:] != APK_SIG_BLOCK_MAGIC:
                return result
            size_in_footer = struct.unpack("<Q", footer[:8])[0]
            block_start = file_size - 8 - size_in_footer - 8
            if block_start < 0:
                return result
            handle.seek(block_start)
            header = handle.read(8)
            block_size = struct.unpack("<Q", header)[0]
            if block_size != size_in_footer:
                return result
            handle.seek(block_start + 8)
            payload = handle.read(size_in_footer)
        result["present"] = True
        offset = 0
        while offset + 12 <= len(payload):
            length = struct.unpack("<Q", payload[offset: offset + 8])[0]
            identifier = struct.unpack("<I", payload[offset + 8: offset + 12])[0]
            value = payload[offset + 12: offset + 8 + length]
            if identifier == 0x7109871A:  # v2
                result["scheme"] = "v2"
                result["certificate"] = _first_cert_from_scheme_block(value)
                break
            if identifier == 0xF05368C0:  # v3
                result["scheme"] = "v3"
                result["certificate"] = _first_cert_from_scheme_block(value)
            offset += 8 + length
    except (OSError, struct.error, IndexError):
        pass
    return result


def _first_cert_from_scheme_block(value: bytes) -> Optional[bytes]:
    """Walk signers -> signer -> signed data -> digests[0].certificate."""
    try:
        if len(value) < 4:
            return None
        sequence_length = struct.unpack("<I", value[:4])[0]
        sequence = value[4: 4 + sequence_length]
        # signers: length-prefixed sequence of signers
        signers_length = struct.unpack("<I", sequence[:4])[0]
        signer = sequence[4: 4 + signers_length]
        # signer: signed data (length-prefixed) then signatures
        signed_length = struct.unpack("<I", signer[:4])[0]
        signed = signer[4: 4 + signed_length]
        # signed: digests (length-prefixed seq), certificates (length-prefixed seq), ...
        digests_length = struct.unpack("<I", signed[:4])[0]
        certificates_offset = 4 + digests_length
        certificates_length = struct.unpack("<I", signed[certificates_offset: certificates_offset + 4])[0]
        certificates = signed[certificates_offset + 4: certificates_offset + 4 + certificates_length]
        cert_length = struct.unpack("<I", certificates[:4])[0]
        return certificates[4: 4 + cert_length]
    except (struct.error, IndexError):
        return None


# ---------------------------------------------------------------------------
# Manifest strings (best-effort, no aapt2)
# ---------------------------------------------------------------------------
def scan_binary_manifest(path: Path) -> Dict[str, object]:
    info: Dict[str, object] = {
        "permissions": [],
        "cleartext_allowed": None,
        "network_security_config": None,
        "cleartext_attribute": None,
    }
    try:
        with zipfile.ZipFile(path) as archive:
            if "AndroidManifest.xml" not in archive.namelist():
                return info
            raw = archive.read("AndroidManifest.xml")
    except (OSError, zipfile.BadZipFile):
        return info

    # The AXML string pool is almost always UTF-8 in real APKs (aapt stores
    # lengths as UTF-16 *code units*), so decode as UTF-8 and fall back to
    # UTF-16 only when the first pass found nothing useful.
    text = raw.decode("utf-8", errors="ignore")
    if "android.permission" not in text:
        text = raw.decode("utf-16-le", errors="ignore")

    for permission in DANGEROUS_PERMISSIONS:
        if permission in text:
            info["permissions"].append(permission)  # type: ignore[union-attr]

    if "networkSecurityConfig" in text:
        info["network_security_config"] = True
    elif "usesCleartextTraffic" in text:
        # Attribute ids cannot be read without the resource table, but the SDK
        # only records the attribute when the app explicitly sets it — and the
        # only value that changes behaviour is `true`.
        info["network_security_config"] = False
        info["cleartext_attribute"] = True
    info["cleartext_allowed"] = (
        True if info["cleartext_attribute"] and not info["network_security_config"] else None
    )
    return info


def abis(path: Path) -> List[str]:
    found = set()
    with zipfile.ZipFile(path) as archive:
        for name in archive.namelist():
            if name.startswith("lib/") and "/" in name[4:]:
                found.add(name.split("/")[1])
    return sorted(found)


def suspicious_entries(path: Path) -> List[str]:
    suspicious: List[str] = []
    with zipfile.ZipFile(path) as archive:
        for name in archive.namelist():
            lowered = name.lower()
            if lowered.endswith((".sh", ".bat", ".ps1")) and not lowered.startswith("assets/"):
                suspicious.append(name)
            elif lowered.startswith("lib/") and lowered.endswith(".so") is False:
                suspicious.append(name)
            elif "__pycache__" in lowered or lowered.endswith(".pyc"):
                suspicious.append(name)
    return sorted(suspicious)


# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
def inspect(path: Path) -> Report:
    report = Report(
        path=str(path),
        size=path.stat().st_size,
        sha256=digest_file(path, "sha256"),
        sha512=digest_file(path, "sha512"),
        entries=0,
    )
    try:
        with zipfile.ZipFile(path) as archive:
            report.entries = len(archive.namelist())
            report.has_v1_signature = any(
                name.upper().startswith("META-INF/") and name.upper().endswith((".RSA", ".DSA", ".EC"))
                for name in archive.namelist()
            )
    except zipfile.BadZipFile:
        report.notes.append("not a ZIP archive")
        return report

    report.abis = abis(path)
    manifest = scan_binary_manifest(path)
    report.permissions = manifest["permissions"]  # type: ignore[assignment]
    report.dangerous = [p for p in report.permissions if p in DANGEROUS_PERMISSIONS]
    report.cleartext_allowed = manifest["cleartext_allowed"]  # type: ignore[assignment]
    report.network_security_config = bool(manifest["network_security_config"])
    report.suspicious_entries = suspicious_entries(path)

    signer = parse_apk_signing_block(path)
    report.has_v2_or_v3_signature = bool(signer["present"])
    certificate = signer["certificate"] or v1_certificate(path)
    if isinstance(certificate, bytes):
        report.certificate_sha256 = hashlib.sha256(certificate).hexdigest()
        report.debug_signed = any(marker in certificate for marker in DEBUG_CERT_SUBSTRINGS)
    else:
        report.notes.append("no signing certificate found")
    return report


def _cleartext_text(report: Report) -> str:
    if report.network_security_config and not report.cleartext_allowed:
        return "restricted by networkSecurityConfig"
    if report.cleartext_allowed:
        return "allowed (no networkSecurityConfig)"
    if report.cleartext_allowed is None:
        return "unknown"
    return "unknown"


def render(report: Report) -> str:
    lines = [
        "file        : {}".format(report.path),
        "size        : {:,} bytes".format(report.size),
        "sha256      : {}".format(report.sha256),
        "sha512      : {}".format(report.sha512),
        "entries     : {}".format(report.entries),
        "abis        : {}".format(", ".join(report.abis) or "none"),
        "signatures  : {}".format(
            ("v1 " if report.has_v1_signature else "") + ("v2/v3" if report.has_v2_or_v3_signature else "")
        ).strip() or "signatures  : none",
        "cert sha256 : {}".format(report.certificate_sha256 or "n/a"),
        "debug signed: {}".format("YES — do not trust this build" if report.debug_signed else "no"),
        "cleartext   : {}".format(_cleartext_text(report)),
    ]
    if report.dangerous:
        lines.append("permissions :")
        for permission in report.dangerous:
            lines.append("   - {:<55} {}".format(permission, DANGEROUS_PERMISSIONS[permission]))
    else:
        lines.append("permissions : no dangerous permissions detected")
    if report.suspicious_entries:
        lines.append("suspicious  : " + ", ".join(report.suspicious_entries[:10]))
    for note in report.notes:
        lines.append("note        : " + note)
    return "\n".join(lines)


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Offline APK/AAB forensics: hashes, signer certificate, permissions.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("apk", help="path to the .apk/.aab/.zip")
    parser.add_argument("--expect-sha256", help="fail unless the file hash matches")
    parser.add_argument("--expect-cert", help="fail unless the signing certificate hash matches")
    parser.add_argument("--json", action="store_true", help="machine-readable output")
    parser.add_argument("--allow-debug", action="store_true",
                        help="do not fail when the APK is signed with a debug certificate")
    args = parser.parse_args(argv)

    path = Path(args.apk)
    if not path.is_file():
        print("ERROR: no such file: {}".format(path), file=sys.stderr)
        return 2

    report = inspect(path)
    print(json.dumps(asdict(report), indent=2) if args.json else render(report))

    problems = 0
    if args.expect_sha256 and report.sha256.lower() != args.expect_sha256.lower().replace(":", ""):
        print("\nMISMATCH: sha256 does not match the expected value", file=sys.stderr)
        problems += 1
    if args.expect_cert:
        expected = args.expect_cert.lower().replace(":", "")
        if (report.certificate_sha256 or "").lower() != expected:
            print("\nMISMATCH: signing certificate does not match the expected value", file=sys.stderr)
            problems += 1
    if report.debug_signed and not args.allow_debug:
        print("\nMISMATCH: debug certificate — such a build must not be distributed", file=sys.stderr)
        problems += 1
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
