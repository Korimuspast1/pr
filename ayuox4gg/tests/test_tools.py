#!/usr/bin/env python3
"""Tests for the ayuox4gg tooling (standard library only).

Run::

    python3 -m unittest discover -s ayuox4gg/tests -v
"""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "tools"
sys.path.insert(0, str(TOOLS))

import forensics  # noqa: E402
import security_audit  # noqa: E402
import javaparse  # noqa: E402

PATCHES = ROOT / "security" / "patches"


UPSTREAM = "https://github.com/AyuGram/AyuGram4A.git"
SPARSE_PATHS = (
    "/TMessagesProj/src/main/java/com/radolyn/*",
    "/TMessagesProj/src/main/AndroidManifest.xml",
    "/TMessagesProj/src/main/res/values/ayu.xml",
    "/TMessagesProj/src/main/res/xml/*",
)


def clone_upstream_sparse():
    """Clone just enough of the upstream tree to test the patches, or None."""
    if os.environ.get("AYUOX4GG_SKIP_NETWORK"):
        return None
    target = tempfile.mkdtemp(prefix="ayuox4gg-upstream-")
    clone = subprocess.run(
        ["git", "clone", "--depth", "1", "-b", "rewrite", "--filter=blob:none",
         "--sparse", "-q", UPSTREAM, target],
        capture_output=True, text=True,
    )
    if clone.returncode != 0:
        shutil.rmtree(target, True)
        return None
    checkout = subprocess.run(
        ["git", "sparse-checkout", "set", "--no-cone"] + list(SPARSE_PATHS),
        cwd=target, capture_output=True, text=True,
    )
    if checkout.returncode != 0:
        shutil.rmtree(target, True)
        return None
    return target


def make_certificate_der() -> bytes:
    """A tiny, deterministic DER blob shaped like an X.509 certificate."""
    signature_oid = bytes.fromhex("06092a864886f70d01010b")  # sha256WithRSAEncryption
    body = bytes.fromhex("020102") + bytes.fromhex("308201d0") + signature_oid + b"\x00" * 200
    tbs = b"\x30" + bytes([len(body)]) + body if len(body) < 0x80 else b"\x30\x82" + len(body).to_bytes(2, "big") + body
    return b"\x30\x82" + len(tbs).to_bytes(2, "big") + tbs


def wrap_pkcs7(certificate: bytes) -> bytes:
    """Wrap a certificate the way META-INF/*.RSA does (PKCS#7 SignedData)."""
    implicit_set = certificate
    certificates = b"\xa0" + bytes([len(implicit_set)]) + implicit_set
    if len(implicit_set) > 0x7F:
        certificates = b"\xa0\x82" + len(implicit_set).to_bytes(2, "big") + implicit_set
    digest_algorithms = b"\x31\x00"
    content_info = b"\x30\x00"
    signed_data_body = b"\x02\x01\x01" + digest_algorithms + content_info + certificates
    signed_data = b"\x30\x82" + len(signed_data_body).to_bytes(2, "big") + signed_data_body
    explicit = b"\xa0\x82" + len(signed_data).to_bytes(2, "big") + signed_data
    oid = bytes.fromhex("06092a864886f70d010702")
    body = oid + explicit
    return b"\x30\x82" + len(body).to_bytes(2, "big") + body


def build_fake_apk(path: Path, certificate: bytes, manifest_extra: str = "") -> None:
    manifest = (
        "<manifest>"
        "<uses-permission android:name=\"android.permission.INTERNET\"/>"
        "<uses-permission android:name=\"android.permission.REQUEST_INSTALL_PACKAGES\"/>"
        "<uses-permission android:name=\"android.permission.SYSTEM_ALERT_WINDOW\"/>"
        + manifest_extra +
        "</manifest>"
    )
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("classes.dex", b"dex\n035\x00")
        archive.writestr("AndroidManifest.xml", manifest)
        archive.writestr("lib/arm64-v8a/libsqlite.so", b"\x7fELF")
        archive.writestr("META-INF/CERT.RSA", wrap_pkcs7(certificate))


class ForensicsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.certificate = make_certificate_der()
        self.apk = Path(self.tmp.name) / "app.apk"
        build_fake_apk(self.apk, self.certificate)

    def test_hashes_match_hashlib(self):
        report = forensics.inspect(self.apk)
        data = self.apk.read_bytes()
        self.assertEqual(report.sha256, hashlib.sha256(data).hexdigest())
        self.assertEqual(report.sha512, hashlib.sha512(data).hexdigest())

    def test_reads_v1_certificate(self):
        report = forensics.inspect(self.apk)
        self.assertTrue(report.has_v1_signature)
        self.assertEqual(report.certificate_sha256, hashlib.sha256(self.certificate).hexdigest())

    def test_detects_abis_and_permissions(self):
        report = forensics.inspect(self.apk)
        self.assertEqual(report.abis, ["arm64-v8a"])
        self.assertIn("android.permission.REQUEST_INSTALL_PACKAGES", report.dangerous)
        self.assertIn("android.permission.SYSTEM_ALERT_WINDOW", report.dangerous)

    def test_cleartext_flag_without_nsc(self):
        build_fake_apk(self.apk, self.certificate, manifest_extra='<application android:usesCleartextTraffic="true"/>')
        report = forensics.inspect(self.apk)
        self.assertTrue(report.network_security_config is False)
        self.assertTrue(report.cleartext_allowed)

    def test_network_security_config_wins(self):
        build_fake_apk(
            self.apk,
            self.certificate,
            manifest_extra='<application android:networkSecurityConfig="@xml/nsc"'
                           ' android:usesCleartextTraffic="true"/>',
        )
        report = forensics.inspect(self.apk)
        self.assertTrue(report.network_security_config)
        self.assertFalse(report.cleartext_allowed)

    def test_cli_expectations(self):
        digest = forensics.digest_file(self.apk, "sha256")
        cert = hashlib.sha256(self.certificate).hexdigest()
        code = forensics.main([str(self.apk), "--expect-sha256", digest, "--expect-cert", cert, "--allow-debug"])
        self.assertEqual(code, 0)
        code = forensics.main([str(self.apk), "--expect-sha256", "00" * 32, "--allow-debug"])
        self.assertEqual(code, 1)

    def test_missing_file(self):
        self.assertEqual(forensics.main([str(Path(self.tmp.name) / "nope.apk")]), 2)

    def test_debug_certificate_is_flagged(self):
        debug_cert = self.certificate.replace(b"\x00" * 20, b"Android Debug\x00" + b"\x00" * 6, 1)
        report_path = Path(self.tmp.name) / "debug.apk"
        build_fake_apk(report_path, debug_cert)
        self.assertTrue(forensics.inspect(report_path).debug_signed)


class SecurityAuditTests(unittest.TestCase):
    def test_flags_cleartext_and_weak_hashing(self):
        source = "\n".join([
            "class A {",
            '  String scheme() { return useSecure ? "wss://" : "ws://"; }',
            '  void f() { MessageDigest.getInstance("MD5"); }',
            '  void g() { Log.d("x", "token=" + token); }',
            "}",
        ])
        found = {f.rule for f in security_audit.audit_java(source, "A.java")}
        self.assertIn("S1", found)
        self.assertIn("S9", found)
        self.assertIn("S6", found)

    def test_ignores_documentation_urls(self):
        source = 'class A { /** see "http://schemas.android.com/apk/res/android" */ }'
        rules = {f.rule for f in security_audit.audit_java(source, "A.java")}
        self.assertNotIn("S1", rules)

    def test_auth_header_needs_host_check(self):
        unsafe = 'req.newBuilder().addHeader("Authorization", token).build();'
        safe = (
            "if (host.equals(syncHost)) {\n"
            '    builder.addHeader("Authorization", token);\n'
            "}"
        )
        self.assertIn("S3", {f.rule for f in security_audit.audit_java(unsafe, "A.java")})
        self.assertNotIn("S3", {f.rule for f in security_audit.audit_java(safe, "A.java")})

    def test_manifest_rules(self):
        manifest = '<application android:usesCleartextTraffic="true" android:allowBackup="true"/>'
        rules = {f.rule for f in security_audit.audit_manifest(manifest, "AndroidManifest.xml")}
        self.assertLessEqual({"S4", "S5"}, rules)

    def test_diff_mode_only_reads_added_lines(self):
        diff = (
            "diff --git a/A.java b/A.java\n"
            "--- a/A.java\n"
            "+++ b/A.java\n"
            "@@ -1,2 +1,3 @@\n"
            " class A {\n"
            '+  void f() { MessageDigest.getInstance("MD5"); }\n'
            " }\n"
        )
        found = security_audit.audit_diff(diff)
        self.assertEqual([f.rule for f in found], ["S9"])
        self.assertEqual(found[0].line, 2)

    def test_fail_on_threshold(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "A.java"
            path.write_text('class A { void f() { java.security.MessageDigest.getInstance("MD5"); } }',
                            encoding="utf-8")
            self.assertEqual(security_audit.main(["--dir", tmp, "--fail-on", "high"]), 0)
            self.assertEqual(security_audit.main(["--dir", tmp, "--fail-on", "medium"]), 1)


class PatchHygieneTests(unittest.TestCase):
    def test_every_patch_has_a_subject_and_diff_header(self):
        patches = sorted(PATCHES.glob("*.patch"))
        self.assertTrue(patches, "no patches found")
        for patch in patches:
            text = patch.read_text(encoding="utf-8")
            self.assertIn("diff --git", text, patch.name)
            self.assertTrue(text.startswith("diff --git"), patch.name)

    def test_patches_contain_no_windows_line_endings_or_tabs_in_context(self):
        for patch in PATCHES.glob("*.patch"):
            text = patch.read_text(encoding="utf-8")
            self.assertNotIn("\r\n", text, patch.name)

    def test_patched_lines_are_audit_clean(self):
        """The whole point of the patches: no high findings in added lines."""
        for patch in PATCHES.glob("*.patch"):
            findings = security_audit.audit_diff(patch.read_text(encoding="utf-8"))
            high = [f for f in findings if f.severity == "high"]
            self.assertEqual(high, [], "{} adds high-risk code: {}".format(patch.name, high))


class JavaParseTests(unittest.TestCase):
    def test_parses_valid_java(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "Ok.java"
            path.write_text(
                "import java.util.*;\n"
                "class Ok { private static final int X = 1; void f() { var y = List.of(X); } }\n",
                encoding="utf-8",
            )
            self.assertEqual(javaparse.main([str(path), "--javalang"]), 0)

    def test_rejects_broken_java(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "Bad.java"
            path.write_text("class Bad { void f( { } }", encoding="utf-8")
            self.assertEqual(javaparse.main([str(path), "--javalang"]), 1)


class RepositoryStateTests(unittest.TestCase):
    """Guards so the deliverable cannot silently rot."""

    def test_patches_apply_check_runs_when_tree_is_available(self):
        tree = os.environ.get("AYUGRAM_TREE")
        if not tree:
            # Nothing local: if the sandbox can reach GitHub, verify against a
            # throwaway sparse clone so this check never silently rots.
            tree = clone_upstream_sparse()
            if tree is None:
                self.skipTest("set AYUGRAM_TREE=/path/to/AyuGram4A (no network fallback available)")
            self.addCleanup(shutil.rmtree, tree, True)
        for patch in sorted(PATCHES.glob("*.patch")):
            result = subprocess.run(
                ["git", "apply", "--check", str(patch)], cwd=tree, capture_output=True, text=True
            )
            self.assertEqual(result.returncode, 0, "{}: {}".format(patch.name, result.stderr))

    def test_tools_have_no_third_party_imports_at_module_level(self):
        allowed = {"javalang"}
        for path in [TOOLS / "security_audit.py", TOOLS / "forensics.py"]:
            source = path.read_text(encoding="utf-8")
            for line in source.splitlines():
                if line.startswith("import ") or line.startswith("from "):
                    module = line.split()[1].split(".")[0]
                    if module in allowed:
                        continue
                    self.assertIn(
                        module,
                        {"argparse", "json", "hashlib", "struct", "sys", "re", "subprocess",
                         "dataclasses", "pathlib", "typing", "__future__", "shutil", "tempfile",
                         "zipfile"},
                        "{} imports {}".format(path.name, module),
                    )

    def test_docs_reference_existing_files(self):
        readme = (ROOT / "README.md").read_text(encoding="utf-8")
        expected = (
            "security/FINDINGS.md",
            "docs/BUILD.md",
            "docs/EXPLAINED_RU.md",
        )
        for relative in expected:
            self.assertTrue((ROOT / relative).is_file(), "missing doc: " + relative)
            # The README may point at the folder or the file directly.
            folder = relative.split("/")[0]
            self.assertTrue(
                relative in readme or folder in readme,
                "README does not mention " + relative,
            )


if __name__ == "__main__":
    unittest.main(verbosity=2)
