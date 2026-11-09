#!/usr/bin/env python3
"""Pure-Python unit tests for Custom Profile (no exteraGram runtime needed).

Run with the standard library only::

    python3 -m unittest discover -s custom-profile/tests -v
"""

from __future__ import annotations

import json
import os
import sys
import unittest
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT))

import core  # noqa: E402
import verify  # noqa: E402


class SanitizeTextTests(unittest.TestCase):
    def test_strips_control_and_bidi_characters(self):
        # RTL override + zero-width space + bell must not survive.
        dirty = "Ali\u202ece\u200b\u0007"
        self.assertEqual(core.sanitize_text(dirty, 32), "Alice")

    def test_collapses_newlines_by_default(self):
        self.assertEqual(core.sanitize_text("line1\nline2", 32), "line1 line2")

    def test_keeps_newlines_when_allowed(self):
        self.assertEqual(core.sanitize_text("a\nb", 32, allow_newlines=True), "a\nb")

    def test_enforces_length(self):
        self.assertEqual(len(core.sanitize_text("x" * 500, core.MAX_NAME_LENGTH)), core.MAX_NAME_LENGTH)

    def test_empty_inputs(self):
        self.assertEqual(core.sanitize_text(None, 10), "")


class UsernameTests(unittest.TestCase):
    def test_valid_username(self):
        self.assertEqual(core.validate_username("@good_name1"), "good_name1")

    def test_rejects_short_and_invalid(self):
        for value in ("abc", "1abcde", "bad-name", "with space", "\u043f\u0440\u0438\u0432\u0435\u0442"):
            self.assertIsNone(core.validate_username(value), value)

    def test_rejects_too_long(self):
        self.assertIsNone(core.validate_username("a" * (core.MAX_USERNAME_LENGTH + 1)))


class NumericClampTests(unittest.TestCase):
    def test_stars_clamped(self):
        self.assertEqual(core.clamp_stars("-5"), 0)
        self.assertEqual(core.clamp_stars("99999999999999999999999"), core.MAX_STARS)
        self.assertEqual(core.clamp_stars("garbage"), 0)

    def test_crown_level_clamped(self):
        self.assertEqual(core.clamp_crown_level("0"), core.MIN_CROWN_LEVEL)
        self.assertEqual(core.clamp_crown_level("1000"), core.MAX_CROWN_LEVEL)

    def test_color_index_clamped(self):
        self.assertEqual(core.clamp_color_index("99"), core.PEER_COLOR_MAX)
        self.assertEqual(core.clamp_color_index("-1"), core.PEER_COLOR_MIN)

    def test_emoji_document_id(self):
        self.assertEqual(core.sanitize_emoji_document_id("0"), 0)
        self.assertEqual(core.sanitize_emoji_document_id("-7"), 0)
        self.assertEqual(core.sanitize_emoji_document_id("5368324170671202286"), 5368324170671202286)


class WatermarkTests(unittest.TestCase):
    def test_appends_marker(self):
        self.assertEqual(core.with_watermark("Alice", True, "\u00b7preview", 64), "Alice \u00b7preview")

    def test_disabled(self):
        self.assertEqual(core.with_watermark("Alice", False, "\u00b7preview", 64), "Alice")

    def test_respects_limit(self):
        name = "N" * core.MAX_NAME_LENGTH
        result = core.with_watermark(name, True, "\u00b7preview", core.MAX_NAME_LENGTH)
        self.assertLessEqual(len(result), core.MAX_NAME_LENGTH)
        self.assertTrue(result.endswith("\u00b7preview"))

    def test_empty_marker_falls_back(self):
        self.assertTrue(core.with_watermark("Bob", True, "", 64).endswith(core.DEFAULT_WATERMARK))


class DisplayNameTests(unittest.TestCase):
    def test_split(self):
        self.assertEqual(core.split_display_name("John Ronald Tolkien"), ("John", "Ronald Tolkien"))

    def test_single_word(self):
        self.assertEqual(core.split_display_name("Alice"), ("Alice", ""))

    def test_empty_returns_none(self):
        self.assertIsNone(core.split_display_name("   "))


class BackupTests(unittest.TestCase):
    def test_unknown_keys_ignored(self):
        cleaned = core.sanitize_backup({"verified": True, "evil": "x", "restore_json": "{}"})
        self.assertEqual(cleaned.get("verified"), True)
        self.assertNotIn("evil", cleaned)
        self.assertNotIn("restore_json", cleaned)

    def test_values_are_coerced(self):
        cleaned = core.sanitize_backup(
            {
                "stars_amount": "-1",
                "stars_level": "500",
                "name_color_index": "42",
                "display_username": "not valid!",
                "display_bio": "x" * 900,
                "patch_mode": "7",
            }
        )
        self.assertEqual(cleaned["stars_amount"], "0")
        self.assertEqual(cleaned["stars_level"], str(core.MAX_CROWN_LEVEL))
        self.assertEqual(cleaned["name_color_index"], str(core.PEER_COLOR_MAX))
        self.assertEqual(cleaned["display_username"], "")
        self.assertLessEqual(len(cleaned["display_bio"]), core.MAX_BIO_LENGTH)
        self.assertEqual(cleaned["patch_mode"], 1)

    def test_non_dict(self):
        self.assertEqual(core.sanitize_backup(["a"]), {})
        self.assertEqual(core.sanitize_backup("nope"), {})

    def test_preset_payload_round_trip(self):
        payload = json.dumps({"verified": True, "premium": "1", "watermark_enabled": False})
        cleaned = core.sanitize_backup(json.loads(payload))
        self.assertTrue(cleaned["verified"])
        self.assertTrue(cleaned["premium"])
        self.assertFalse(cleaned["watermark_enabled"])


class VerifyTests(unittest.TestCase):
    def test_hash_of_known_bytes(self):
        self.assertEqual(
            verify.sha256_text("abc"),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
        )

    def test_no_manifest_is_not_a_failure(self):
        ok, report = verify.verify_installation(root=str(PROJECT))
        self.assertTrue(ok)
        self.assertIn("manifest", report)

    def test_detects_modified_file(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "main.py"
            target.write_text("print('real')\n", encoding="utf-8")
            original = verify.expected_hashes
            verify.expected_hashes = lambda: {"main.py": verify.sha256_text("print('other')\n")}
            try:
                ok, report = verify.verify_installation(root=tmp)
            finally:
                verify.expected_hashes = original
            self.assertFalse(ok)
            self.assertEqual(report["modified"], ["main.py"])

    def test_flags_unexpected_modules(self):
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "main.py").write_text("", encoding="utf-8")
            (Path(tmp) / "evil.py").write_text("", encoding="utf-8")
            original = verify.expected_hashes
            verify.expected_hashes = lambda: {"main.py": verify.sha256_text("")}
            try:
                ok, report = verify.verify_installation(root=tmp)
            finally:
                verify.expected_hashes = original
            self.assertFalse(ok)
            self.assertEqual(report["unexpected"], ["evil.py"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
