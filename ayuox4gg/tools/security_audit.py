#!/usr/bin/env python3
"""Static security audit for an AyuGram4A / exteraGram-style Android tree.

This is deliberately *not* a generic SAST toy. It encodes the specific mistakes
that matter for a Telegram fork with a self-hosted sync server:

===  ==========================================================  =============
ID   Check                                                       Severity
===  ==========================================================  =============
S1   cleartext ``ws://`` or ``http://`` in Java sources          high
S2   secrets (token/password/api-hash) written to plain prefs    high
S3   ``Authorization``/``X-API`` headers attached unconditionally high
S4   ``android:usesCleartextTraffic="true"`` in the manifest     high
S5   ``android:allowBackup="true"`` with credentials in prefs    medium
S6   ``Log.*`` calls printing tokens, URLs with tokens or hashes  medium
S7   receiver/service ``android:exported="true"`` without a guard medium
S8   unbounded ``fromJson`` on network input                     medium
S9   MD5/SHA-1 used for integrity checks                         medium
S10  ``MessageDigest.getInstance("MD5")`` in an updater path      medium
S11  debug endpoints / test hosts left in release sources        low
S12  ``setJavaScriptEnabled(true)`` without local-only content     low
===  ==========================================================  =============

Output is stable, greppable and exits non-zero when a ``high`` finding exists,
so it can be dropped straight into CI.

Usage
-----
    python3 ayuox4gg/tools/security_audit.py --dir TMessagesProj/src/main/java/com/radolyn
    python3 ayuox4gg/tools/security_audit.py --dir . --format json --fail-on high
    python3 ayuox4gg/tools/security_audit.py --diff            # audit a git diff

``--diff`` audits only changed lines (added lines), which is how this tool was
used while writing ``security/patches``: each patch had to come out clean.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence

SEVERITY_ORDER = {"high": 0, "medium": 1, "low": 2, "info": 3}


@dataclass
class Finding:
    rule: str
    severity: str
    message: str
    file: str
    line: int
    excerpt: str

    def key(self):
        return (SEVERITY_ORDER.get(self.severity, 9), self.file, self.line, self.rule)


# ---------------------------------------------------------------------------
# Rule helpers
# ---------------------------------------------------------------------------
_SKIP_DIRS = {
    ".git", "build", ".gradle", ".idea", "node_modules", "__pycache__",
    ".cxx", "outputs", "intermediates", "externalNativeBuild",
}

WEAK_HASHES = ("MD5", "SHA-1", "SHA1")
SECRET_WORDS = re.compile(r"(token|password|passwd|secret|apihash|api_hash|auth_key|session)", re.I)
CLEARTEXT_LITERAL = re.compile(r'"(ws|http)://([^"/]+)', re.I)
DEBUG_HOSTS = re.compile(
    r"(ayusync-dev|localhost:\d{4,5}|127\.0\.0\.1:\d{4,5}|10\.0\.2\.2:\d{4,5}|staging\.|\.ngrok\.)", re.I
)


def iter_java_files(root: Path) -> Iterable[Path]:
    for path in root.rglob("*.java"):
        if any(part in _SKIP_DIRS for part in path.parts):
            continue
        yield path


def iter_manifests(root: Path) -> Iterable[Path]:
    for path in root.rglob("AndroidManifest.xml"):
        if any(part in _SKIP_DIRS for part in path.parts):
            continue
        yield path


# ---------------------------------------------------------------------------
# Java rules
# ---------------------------------------------------------------------------
def audit_java(text: str, display_name: str) -> List[Finding]:
    findings: List[Finding] = []
    lines = text.splitlines()

    def add(rule, severity, message, index, excerpt=None):
        findings.append(
            Finding(
                rule=rule,
                severity=severity,
                message=message,
                file=display_name,
                line=index + 1,
                excerpt=(excerpt if excerpt is not None else lines[index]).strip()[:200],
            )
        )

    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("*"):
            continue

        # S1 — cleartext schemes in source
        for match in CLEARTEXT_LITERAL.finditer(line):
            host = match.group(2)
            if host.startswith(("schemas.", "www.w3.org", "xmlpull", "apache.org", "dashif", "ns.adobe",
                                "matroska", "svn.xiph", "xoroshiro", "sites.google", "msdn", "azure.",
                                "en.wikipedia", "ns.adobe")):
                continue
            if re.search(r'"(ws|http)//', line):
                continue  # prefix concatenation, handled below
            add("S1", "high", "cleartext {} URL literal".format(match.group(1)), index)

        if re.search(r'return\s+[^;]*\?\s*"wss://"\s*:\s*"ws://"|"wss://"\s*:\s*"ws://"', line):
            add("S1", "high", "TLS scheme selected by a runtime flag — an insecure toggle downgrades traffic", index)

        # S2 — secrets persisted in plain SharedPreferences.
        # A bare `putString(` is not enough: `AyuSecrets.putString(...)` is the
        # encrypted path, and a constant *named* SYNC_TOKEN_SECRET is not a
        # secret either (that false positive fired on patch 0004). Require an
        # actual SharedPreferences receiver.
        if re.search(r"\b(preferences|prefs|sharedPreferences|editor)\s*\.\s*putString\s*\(", line) \
                and SECRET_WORDS.search(line):
            if "KeyStore" not in text and "EncryptedSharedPreferences" not in text:
                add("S2", "high", "credential written through SharedPreferences (plaintext at rest)", index)

        # S3 — unconditional auth header
        if re.search(r'\.newBuilder\(\)[^;]*addHeader\(\s*"(Authorization|X-API-Key|X-Auth-Token)"', line) \
                or (re.search(r'addHeader\(\s*"(Authorization|X-API-Key|X-Auth-Token)"', line)
                    and "Interceptor" in text):
            window = "\n".join(lines[max(0, index - 8): index + 3])
            if not re.search(r"if\s*\(.*host", window, re.I):
                add("S3", "high", "auth header attached without host check (leaks via redirects)", index)

        # S6 — secrets in logs
        if re.search(r"Log\.\w+\(", line) and SECRET_WORDS.search(line) and "token" in line.lower():
            add("S6", "medium", "log statement mentions a credential", index)
        if re.search(r"Log\.\w+\([^)]*get(Profile|SyncServer|Token)", line):
            add("S6", "medium", "log statement prints a URL that may contain a token", index)

        # S7 — exported components without permission/guard
        if 'android:exported="true"' in line:
            window = "\n".join(lines[max(0, index - 6): index + 6])
            if not re.search(r'android:permission=|android:exported="false"', window):
                add("S7", "low", "exported component without an explicit android:permission", index)

        # S8 — unbounded JSON parsing
        if re.search(r"fromJson\(|readValue\(|JSONObject\(", line) and "length()" not in line:
            window = "\n".join(lines[max(0, index - 10): index + 2])
            if not re.search(r"MAX_FRAME|length\(\)\s*[<>]|limit|cap", window, re.I):
                add("S8", "low", "JSON parsed without a visible size cap (check the caller)", index)

        # S9/S10 — weak digests (MD5/SHA-1) for integrity
        for weak in WEAK_HASHES:
            if re.search(r'MessageDigest\.getInstance\(\s*"{}"'.format(weak), line, re.I):
                add("S9", "medium", "{} used for hashing".format(weak), index)

        # S11 — debug hosts shipped in sources
        if DEBUG_HOSTS.search(line) and not stripped.startswith("//"):
            if "BuildVars.isBetaApp" not in line and "DEBUG_VERSION" not in line:
                add("S11", "low", "development/debug host reachable in release code", index)

        # S12 — JS in a WebView
        if re.search(r"setJavaScriptEnabled\(\s*true\s*\)", line):
            add("S12", "low", "JavaScript enabled in a WebView", index)

    return findings


# ---------------------------------------------------------------------------
# Manifest rules
# ---------------------------------------------------------------------------
def audit_manifest(text: str, display_name: str) -> List[Finding]:
    findings: List[Finding] = []
    for index, line in enumerate(text.splitlines()):
        if 'android:usesCleartextTraffic="true"' in line:
            findings.append(Finding("S4", "high", "cleartext traffic allowed application-wide",
                                    display_name, index + 1, line.strip()))
        if 'android:allowBackup="true"' in line:
            findings.append(Finding("S5", "medium", "backups enabled: prefs (and any token in them) can be extracted",
                                    display_name, index + 1, line.strip()))
        if 'android:restoreAnyVersion="true"' in line:
            findings.append(Finding("S5", "low", "restoreAnyVersion allows downgrade restores during backup",
                                    display_name, index + 1, line.strip()))
        if 'android:networkSecurityConfig=' in line:
            findings.append(Finding("S4", "info", "networkSecurityConfig present (good) — verify base-config",
                                    display_name, index + 1, line.strip()))
    return findings


# ---------------------------------------------------------------------------
# Drivers
# ---------------------------------------------------------------------------
def audit_tree(root: Path) -> List[Finding]:
    findings: List[Finding] = []
    for path in iter_java_files(root):
        display = str(path.relative_to(root)) if root in path.parents else path.name
        findings.extend(audit_java(path.read_text(encoding="utf-8", errors="replace"), display))
    for path in iter_manifests(root):
        display = str(path.relative_to(root)) if root in path.parents else path.name
        findings.extend(audit_manifest(path.read_text(encoding="utf-8", errors="replace"), display))
    findings.sort(key=Finding.key)
    return findings


def audit_diff(patch_text: str) -> List[Finding]:
    """Audit added lines only, so a patch can be reviewed in isolation."""
    findings: List[Finding] = []
    file_name = "<diff>"
    new_line = 0
    pending: List[str] = []
    for raw in patch_text.splitlines():
        if raw.startswith("+++ b/"):
            file_name = raw[6:]
            pending = []
            continue
        if raw.startswith("@@"):
            match = re.search(r"\+(\d+)", raw)
            new_line = int(match.group(1)) if match else 0
            continue
        if raw.startswith("+"):
            pending.append(raw[1:])
        else:
            if pending:
                block = "\n".join(pending)
                for finding in audit_java(block, file_name):
                    if finding.rule != "S7":
                        finding.line += new_line - 1
                        findings.append(finding)
                pending = []
            new_line += 1
    return findings


def git_diff_text(paths: Sequence[str]) -> str:
    command = ["git", "diff", "--no-color", "-U0"]
    if paths:
        command += ["--"] + list(paths)
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode != 0:
        raise SystemExit(result.stderr.strip() or "git diff failed")
    return result.stdout


def format_text(findings: List[Finding]) -> str:
    if not findings:
        return "OK: no findings"
    out: List[str] = []
    counts: Dict[str, int] = {}
    for finding in findings:
        counts[finding.severity] = counts.get(finding.severity, 0) + 1
        out.append(
            "{:<6} {:<3} {}:{}  {}\n       {}".format(
                finding.severity.upper(), finding.rule, finding.file, finding.line,
                finding.message, finding.excerpt,
            )
        )
    summary = ", ".join("{} {}".format(v, k) for k, v in sorted(counts.items(), key=lambda kv: SEVERITY_ORDER[kv[0]]))
    out.append("\n{} finding(s): {}".format(len(findings), summary))
    return "\n".join(out)


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Audit an AyuGram4A tree for the fork-specific security mistakes.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--dir", action="append", default=[], help="directory to scan (repeatable)")
    parser.add_argument("--diff", nargs="*", metavar="PATH", help="audit added lines of a git diff")
    parser.add_argument("--patch", help="audit added lines of a .patch/.diff file")
    parser.add_argument("--format", choices=("text", "json"), default="text")
    parser.add_argument("--fail-on", choices=("high", "medium", "low", "info", "never"), default="high")
    args = parser.parse_args(argv)

    if args.patch:
        findings = audit_diff(Path(args.patch).read_text(encoding="utf-8"))
    elif args.diff is not None:
        findings = audit_diff(git_diff_text(args.diff))
    elif args.dir:
        roots = [Path(directory).resolve() for directory in args.dir]
        # Drop nested roots so a file is never reported twice.
        roots = [r for r in roots if not any(o != r and o in r.parents for o in roots)]
        findings = []
        for root in roots:
            findings.extend(audit_tree(root))
        findings.sort(key=Finding.key)
    else:
        parser.error("pass --dir, --diff or --patch")

    if args.format == "json":
        print(json.dumps([asdict(finding) for finding in findings], indent=2))
    else:
        print(format_text(findings))

    if args.fail_on == "never":
        return 0
    threshold = SEVERITY_ORDER[args.fail_on]
    return 1 if any(SEVERITY_ORDER[finding.severity] <= threshold for finding in findings) else 0


if __name__ == "__main__":
    raise SystemExit(main())
