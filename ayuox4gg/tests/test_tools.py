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
import setup_keys  # noqa: E402
import keystore  # noqa: E402
import unpack_apk  # noqa: E402

PATCHES = ROOT / "security" / "patches"


UPSTREAM = "https://github.com/AyuGram/AyuGram4A.git"
SPARSE_PATHS = (
    "/TMessagesProj/src/main/java/com/radolyn/*",
    # patch 0004 also touches the updater; without this path the check fails
    # with "No such file or directory" instead of testing the patch.
    "/TMessagesProj/src/main/java/com/exteragram/messenger/utils/UpdaterUtils.java",
    "/TMessagesProj/src/main/AndroidManifest.xml",
    "/TMessagesProj/src/main/res/values/ayu.xml",
    # patch 0005 rebrands the app name and touches two files outside the
    # radolyn/ subtree, plus the export UI lives under radolyn/ui/preferences.
    "/TMessagesProj/src/main/res/values/strings.xml",
    "/TMessagesProj/src/main/res/values/extera.xml",
    "/TMessagesProj/src/main/java/org/telegram/ui/Cells/DialogCell.java",
    "/TMessagesProj/src/main/java/org/telegram/ui/ProfileActivity.java",
    "/TMessagesProj/src/main/java/org/telegram/messenger/NotificationsService.java",
    "/TMessagesProj/src/main/java/com/exteragram/messenger/utils/LocaleUtils.java",
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


class KeystoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def test_refuses_to_write_inside_a_git_tree(self):
        repo = Path(self.tmp.name) / "repo"
        repo.mkdir()
        if subprocess.run(["git", "init", "-q", str(repo)], capture_output=True).returncode != 0:
            self.skipTest("git is not available")
        with self.assertRaises(SystemExit):
            keystore.confirm_safe_path(repo / "release.jks")

    def test_allows_paths_outside_a_git_tree(self):
        outside = Path(self.tmp.name) / "keys" / "release.jks"
        keystore.confirm_safe_path(outside)  # must not raise

    def test_parses_keytool_fingerprint(self):
        output = (
            "Owner: CN=Test\n"
            "\t SHA256: C3:F8:91:E8:4B:58:36:85\n"
        )
        self.assertEqual(keystore.parse_fingerprint(output), "c3f891e84b583685")

    def test_no_fingerprint(self):
        self.assertIsNone(keystore.parse_fingerprint("nothing here"))

    def test_env_output_never_contains_a_password(self):
        path = Path(self.tmp.name) / "k.jks"
        path.write_bytes(b"x")
        args = type("A", (), {"keystore": str(path), "alias": "ayuox4gg"})()
        import io
        import contextlib

        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            self.assertEqual(keystore.env(args), 0)
        printed = buffer.getvalue()
        self.assertIn("SIGNING_KEY_STORE_FILE =", printed)
        self.assertIn("<your password>", printed)


class SetupKeysTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / "API_KEYS"

    def test_writes_valid_file_with_strict_permissions(self):
        # Obviously fake values: tests must never contain a real API pair.
        code = setup_keys.main(["--app-id", "1234567",
                                "--app-hash", "0123456789abcdef0123456789abcdef",
                                "--output", str(self.path), "--force"])
        self.assertEqual(code, 0)
        text = self.path.read_text()
        self.assertIn("APP_ID = 1234567", text)
        self.assertEqual(self.path.stat().st_mode & 0o777, 0o600)

    def test_quoting_matches_each_consumer(self):
        """APP_HASH is a Java literal; SIGNING_* are raw property values.

        The two consumers read API_KEYS differently: `buildConfigField` splices
        APP_HASH into generated Java, where an unquoted value is a syntax error
        ("cannot find symbol" at the BuildConfig call site), while keytool gets
        SIGNING_KEY_ALIAS through Properties.getProperty(), which keeps quotes
        and would look for an alias literally named `"ayuox4gg"`. Getting either
        one backwards breaks the release build, so both directions are pinned.
        """
        code = setup_keys.main(["--app-id", "1234567",
                                "--app-hash", "0123456789abcdef0123456789abcdef",
                                "--sign-password", "s3cret-Pass",
                                "--sign-alias", "ayuox4gg",
                                "--output", str(self.path), "--force"])
        self.assertEqual(code, 0)
        text = self.path.read_text()

        # Java literals keep their quotes.
        self.assertIn('APP_HASH = "0123456789abcdef0123456789abcdef"', text)
        self.assertIn('MAPS_V2_API = ""', text)

        # Raw values have none.
        self.assertIn("SIGNING_KEY_ALIAS = ayuox4gg", text)
        self.assertIn("SIGNING_KEY_PASSWORD = s3cret-Pass", text)
        self.assertIn("SIGNING_KEY_STORE_PASSWORD = s3cret-Pass", text)

        values = setup_keys.parse_existing(self.path)
        self.assertEqual(values["SIGNING_KEY_ALIAS"], "ayuox4gg")
        self.assertEqual(values["SIGNING_KEY_PASSWORD"], "s3cret-Pass")

    def test_rejects_backslash_and_newlines(self):
        # Properties would treat a backslash as an escape and silently corrupt
        # the value; a newline would inject extra keys.
        with self.assertRaises(ValueError):
            setup_keys.write_file(self.path, "1234567",
                                  "0123456789abcdef0123456789abcdef",
                                  sign_password="back\\slash")
        with self.assertRaises(ValueError):
            setup_keys.write_file(self.path, "1234567",
                                  "0123456789abcdef0123456789abcdef",
                                  sign_alias="a\nb")

    def test_rejects_bad_hash_and_id(self):
        self.assertEqual(setup_keys.main(["--app-id", "abc", "--app-hash", "00" * 16, "--output", str(self.path)]), 1)
        self.assertEqual(setup_keys.main(["--app-id", "123", "--app-hash", "xyz", "--output", str(self.path)]), 1)
        self.assertFalse(self.path.exists())

    def test_check_redacts_the_hash(self):
        setup_keys.main(["--app-id", "123456", "--app-hash", "a" * 32, "--output", str(self.path)])
        code = setup_keys.main(["--check", str(self.path)])
        self.assertEqual(code, 0)
        # The redacted summary must not contain the full value.
        self.assertIn("123456", self.path.read_text())

    def test_check_missing_file(self):
        self.assertEqual(setup_keys.main(["--check", str(self.path)]), 2)

    def test_refuses_to_write_into_unignored_git_tree(self):
        repo = Path(self.tmp.name) / "repo"
        repo.mkdir(parents=True)
        init = subprocess.run(["git", "init", "-q", str(repo)], capture_output=True)
        if init.returncode != 0:
            self.skipTest("git is not available")
        (repo / ".gitignore").write_text("build/\n", encoding="utf-8")
        target = repo / "API_KEYS"
        code = setup_keys.main(["--app-id", "123", "--app-hash", "b" * 32, "--output", str(target)])
        self.assertEqual(code, 1, "should refuse: file is not git-ignored")
        self.assertFalse(target.exists())
        # With the file listed in .gitignore it is allowed.
        (repo / ".gitignore").write_text("build/\nAPI_KEYS\n", encoding="utf-8")
        self.assertTrue(setup_keys.is_git_ignored(target))
        self.assertEqual(setup_keys.main(["--app-id", "123", "--app-hash", "b" * 32,
                                          "--output", str(target)]), 0)


    def test_unpack_reassembles_and_verifies(self):
        """The APK ships as .part00/.part01/... inside the repo; rebuilding it
        must reproduce the recorded digest and refuse a corrupted chunk."""
        import hashlib
        import io
        import contextlib

        build = Path(self.tmp.name) / "artifacts" / "ayuox4gg-test"
        build.mkdir(parents=True)
        blob = bytes(range(256)) * 40
        chunk = 1024
        for index, offset in enumerate(range(0, len(blob), chunk)):
            (build / ("app.apk.part{:02d}".format(index))).write_bytes(
                blob[offset: offset + chunk])
        digest = hashlib.sha256(blob).hexdigest()
        (build / "README.md").write_text(
            "| | |\n|---|---|\n| sha256 | `{}` |\n".format(digest), encoding="utf-8")

        original = unpack_apk.ARTIFACTS
        unpack_apk.ARTIFACTS = Path(self.tmp.name) / "artifacts"
        self.addCleanup(setattr, unpack_apk, "ARTIFACTS", original)

        out = Path(self.tmp.name) / "out.apk"
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(unpack_apk.main(["ayuox4gg-test", "--out", str(out)]), 0)
        self.assertEqual(hashlib.sha256(out.read_bytes()).hexdigest(), digest)

        # A modified chunk must be reported, not silently accepted.
        part = build / "app.apk.part01"
        part.write_bytes(b"corrupted")
        with contextlib.redirect_stdout(io.StringIO()), \
                contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(unpack_apk.main(["ayuox4gg-test", "--verify-only"]), 1)

    def test_unpack_reports_an_unknown_build(self):
        import io
        import contextlib

        original = unpack_apk.ARTIFACTS
        unpack_apk.ARTIFACTS = Path(self.tmp.name) / "artifacts"
        self.addCleanup(setattr, unpack_apk, "ARTIFACTS", original)
        with contextlib.redirect_stdout(io.StringIO()), \
                contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                unpack_apk.main(["nope", "--verify-only"])


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
        # Apply them one by one, in order, the way bootstrap-ayu.sh does.
        # Checking each patch against the *pristine* tree is not enough: 0005
        # is generated against a tree that already has 0001-0004, and applying
        # it to the original files fails on ayu.xml, which 0001 edits too. That
        # mismatch shipped a build with the patch silently skipped, so the
        # sequence is now what gets tested.
        applied = []
        for patch in sorted(PATCHES.glob("*.patch")):
            result = subprocess.run(
                ["git", "apply", str(patch)], cwd=tree, capture_output=True, text=True
            )
            self.assertEqual(
                result.returncode, 0,
                "{}: {} (after {})".format(
                    patch.name, result.stderr, ", ".join(applied) or "nothing"),
            )
            applied.append(patch.name)
        self.assertTrue(applied, "no patches were applied")

    def test_rebrand_changes_text_but_never_resource_identifiers(self):
        """The rebrand must not rename `<string name=...>` ids.

        Rewriting every "Telegram" occurrence in strings.xml also rewrote the
        ids themselves (InviteToTelegram -> InviteToayu0x4gg), and the Java that
        references R.string.InviteToTelegram then failed to compile. Only the
        *text* may change; identifiers are the ABI between resources and code.
        """
        patch = PATCHES / "0005-built-features-and-rebrand.patch"
        if not patch.is_file():
            self.skipTest("patch 0005 not present")
        text = patch.read_text(encoding="utf-8", errors="replace")
        # A removed line that still carries the upstream id is fine; one whose
        # id has been renamed is the bug. Compare against what upstream has.
        removed = [
            line[1:] for line in text.splitlines()
            if line.startswith("-") and not line.startswith("---")
            and '<string name="' in line and "ayu0x4gg" in line
            and 'Telegram' not in line
        ]
        self.assertEqual(
            removed, [],
            "patch 0005 rewrites the id of a string resource, not just its text")

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

    def test_proprietary_standins_match_call_sites(self):
        """The stand-ins must expose exactly what the public code calls."""
        import re

        stubs = (ROOT / "proprietary-stubs")
        message_utils = (stubs / "AyuMessageUtils.java").read_text(encoding="utf-8")
        history_hook = (stubs / "AyuHistoryHook.java").read_text(encoding="utf-8")

        # Signatures verified against the three call sites in upstream rewrite.
        self.assertRegex(message_utils, r"public static void map\(AyuSavePreferences")
        self.assertRegex(message_utils, r"public static void mapMedia\(AyuSavePreferences")
        self.assertRegex(message_utils, r"public static void map\(EditedMessage")
        self.assertRegex(message_utils, r"public static void mapMedia\(EditedMessage")
        self.assertRegex(history_hook, r"public static Pair<Integer, Integer> getMinAndMaxIds\(")
        self.assertRegex(history_hook, r"public static void doHook\(")
        # Every argument the caller passes must appear in doHook's signature.
        for argument in ("currentAccount", "messages", "messagesDict", "startId", "endId",
                         "dialogId", "limit", "topicId", "isSecretChat"):
            self.assertIn(argument, history_hook, "doHook is missing " + argument)
        # They live in the package upstream imports.
        for text in (message_utils, history_hook):
            self.assertIn("package com.radolyn.ayugram.proprietary;", text)

    def test_signatures_that_javac_caught_are_pinned(self):
        """Regression for the first CI build: four javac errors, all in our code.

        Each assertion below corresponds to a real compiler error, so the same
        mistake cannot come back silently.
        """
        stubs = (ROOT / "proprietary-stubs")
        history_hook = (stubs / "AyuHistoryHook.java").read_text(encoding="utf-8")
        message_utils = (stubs / "AyuMessageUtils.java").read_text(encoding="utf-8")
        patch = (ROOT / "security/patches/0003-sync-frame-hardening.patch").read_text(encoding="utf-8")

        # 1. ChatActivity declares `SparseArray<MessageObject>[] messagesDict`;
        #    a HashMap parameter does not compile at the call site.
        self.assertIn("SparseArray<MessageObject>[] messagesDict", history_hook)
        self.assertNotIn("HashMap<Integer, MessageObject> messagesDict", history_hook)

        # 2. SerializedData in this tree only has readInt32(boolean).
        self.assertNotRegex(message_utils, r"readInt32\(\)")
        self.assertIn("readInt32(false)", message_utils)

        # 3. A field must not sit between @Override and the method it annotates.
        self.assertNotIn("@Override\n    /**", patch)
        self.assertIn("private static final int MAX_FRAME_BYTES", patch)

        # 4. API_KEYS quoting: APP_HASH is a Java literal, SIGNING_* are raw.
        values = {"app_id": "1234567", "app_hash": "0" * 32,
                  "sign_password": "pw", "sign_alias": "ayuox4gg"}
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "API_KEYS"
            setup_keys.write_file(out, values["app_id"], values["app_hash"],
                                  sign_password=values["sign_password"],
                                  sign_alias=values["sign_alias"])
            text = out.read_text(encoding="utf-8")
        self.assertIn('APP_HASH = "' + values["app_hash"] + '"', text)
        self.assertIn("SIGNING_KEY_ALIAS = ayuox4gg", text)

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
