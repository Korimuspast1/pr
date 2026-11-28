#!/usr/bin/env python3
"""End-to-end tests for the plugin logic against a stubbed exteraGram host.

Run with the standard library only::

    python3 -m unittest discover -s custom-profile/tests -v
"""

from __future__ import annotations

import importlib
import sys
import unittest
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import runtime_stub  # noqa: E402

STRINGS = runtime_stub.install()

import core  # noqa: E402

# Import after the stubs are installed, exactly like the host does.
if "main" in sys.modules:
    del sys.modules["main"]
main = importlib.import_module("main")

UserConfig, MessagesController, User, UserFull = sys.modules[
    "org.telegram.messenger"
].fakes


def make_plugin(**settings):
    plugin = main.CustomProfilePlugin()
    for key, value in core.DEFAULTS.items():
        plugin.settings[key] = value
    plugin.settings.update(settings)
    plugin.on_plugin_load()
    return plugin


class LocalisationTests(unittest.TestCase):
    def test_every_used_key_exists_in_both_locales(self):
        """Guard against a setting rendering as a raw key on device."""
        source = (PROJECT / "main.py").read_text(encoding="utf-8")
        import re

        used = set(re.findall(r'st\("([a-z0-9_]+)"', source))
        # Colours are looked up dynamically as ``color_<index>``.
        used |= {"color_%d" % index for index in range(7)}
        used.discard("color_")
        for locale in ("en", "ru"):
            table = runtime_stub.load_strings(PROJECT / "strings" / ("strings_%s.yml" % locale))
            missing = sorted(key for key in used if key not in table)
            self.assertEqual(missing, [], "missing {} keys: {}".format(locale, missing))

    def test_metadata_placeholders_resolve(self):
        table = runtime_stub.load_strings(PROJECT / "strings" / "strings_en.yml")
        self.assertIn("plugin_description", table)
        metainfo = (PROJECT / "metainfo.yml").read_text(encoding="utf-8")
        self.assertIn("{plugin_description}", metainfo)


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        MessagesController._instances.clear()
        UserConfig._instances.clear()
        runtime_stub.BulletinRecorder.messages.clear()

    def test_read_only_mode_installs_no_cache_mutating_hooks(self):
        plugin = make_plugin(enabled=True, patch_mode=0)
        hooked = {hook.method for hook in plugin.installed_hooks}
        self.assertIn("getUser", hooked)
        self.assertIn("getUserFull", hooked)
        for forbidden in ("putUser", "putUsers", "processUserInfo"):
            self.assertNotIn(forbidden, hooked)

    def test_update_mode_installs_legacy_hooks(self):
        plugin = make_plugin(enabled=True, patch_mode=1)
        hooked = {hook.method for hook in plugin.installed_hooks}
        self.assertTrue({"putUser", "putUsers", "processUserInfo"} <= hooked)

    def test_disabled_plugin_does_nothing(self):
        plugin = make_plugin(enabled=False, verified=True, premium=True)
        user = User(UserConfig.getInstance(0).getClientUserId())
        before = (user.verified, user.premium, user.first_name)
        plugin._patch_user(user)
        self.assertEqual((user.verified, user.premium, user.first_name), before)

    def test_unload_restores_originals(self):
        plugin = make_plugin(enabled=True, verified=True, premium=True, display_name="Preview Person")
        user = User(UserConfig.getInstance(0).getClientUserId())
        plugin._patch_user(user)
        self.assertTrue(user.verified)
        self.assertEqual(user.first_name, "Preview ·preview")
        plugin.on_plugin_unload()
        self.assertFalse(user.verified)
        self.assertEqual(user.first_name, "Real")
        self.assertEqual(user.last_name, "Name")

    def test_never_patches_other_peoples_profiles(self):
        plugin = make_plugin(enabled=True, verified=True)
        stranger = User(999999)
        self.assertFalse(plugin._patch_user(stranger))
        self.assertFalse(stranger.verified)


class PatchBehaviourTests(unittest.TestCase):
    def setUp(self):
        MessagesController._instances.clear()
        UserConfig._instances.clear()
        runtime_stub.BulletinRecorder.messages.clear()

    def test_display_name_and_watermark(self):
        plugin = make_plugin(enabled=True, display_name="John Tolkien")
        user = User(UserConfig.getInstance(0).getClientUserId())
        plugin._patch_user(user)
        self.assertEqual(user.first_name, "John ·preview")
        self.assertEqual(user.last_name, "Tolkien")

    def test_watermark_can_be_disabled(self):
        plugin = make_plugin(enabled=True, display_name="John", watermark_enabled=False)
        user = User(UserConfig.getInstance(0).getClientUserId())
        plugin._patch_user(user)
        self.assertEqual(user.first_name, "John")

    def test_invalid_username_is_ignored(self):
        plugin = make_plugin(enabled=True, display_username="no spaces!")
        user = User(UserConfig.getInstance(0).getClientUserId())
        plugin._patch_user(user)
        self.assertEqual(user.username, "real_user")

    def test_valid_username_is_applied(self):
        plugin = make_plugin(enabled=True, display_username="@Good_Name")
        user = User(UserConfig.getInstance(0).getClientUserId())
        plugin._patch_user(user)
        self.assertEqual(user.username, "Good_Name")

    def test_bidi_override_is_stripped(self):
        plugin = make_plugin(enabled=True, display_name="Ali\u202ece", watermark_enabled=False)
        user = User(UserConfig.getInstance(0).getClientUserId())
        plugin._patch_user(user)
        self.assertEqual(user.first_name, "Alice")

    def test_star_rating_is_clamped(self):
        plugin = make_plugin(enabled=True, stars_enabled=True, stars_amount="99999999999999999999999", stars_level="500")
        user = User(UserConfig.getInstance(0).getClientUserId())
        full = UserFull(user.id, user)
        plugin._patch_full_user(full)
        rating = full.stars_rating
        self.assertIsNotNone(rating)
        self.assertEqual(rating.stars, core.MAX_STARS)
        self.assertEqual(rating.level, core.MAX_CROWN_LEVEL)

    def test_emoji_status_requires_positive_id(self):
        plugin = make_plugin(enabled=True, emoji_status_enabled=True, emoji_status_id="0")
        self.assertIsNone(plugin._new_emoji_status())
        plugin.settings["emoji_status_id"] = "5368324170671202286"
        status = plugin._new_emoji_status()
        self.assertIsNotNone(status)
        self.assertEqual(status.document_id, 5368324170671202286)

    def test_colors_clamped_to_palette(self):
        plugin = make_plugin(enabled=True, name_color_enabled=True, name_color_index="99")
        color = plugin._new_peer_color("name_color_index")
        self.assertEqual(color.color, core.PEER_COLOR_MAX)

    def test_about_is_length_limited(self):
        plugin = make_plugin(enabled=True, display_bio="x" * 900)
        user = User(UserConfig.getInstance(0).getClientUserId())
        full = UserFull(user.id, user)
        plugin._patch_full_user(full)
        self.assertEqual(len(full.about), core.MAX_BIO_LENGTH)

    def test_hook_param_errors_do_not_escape(self):
        hook = main._PatchReturnedUserHook(None)  # plugin=None => must not raise

        class Broken:
            def getResult(self):
                return object()

        hook.after_hooked_method(Broken())  # no assertion: simply must not raise


class PresetTests(unittest.TestCase):
    def setUp(self):
        MessagesController._instances.clear()
        UserConfig._instances.clear()
        runtime_stub.BulletinRecorder.messages.clear()

    def test_presets_never_touch_safety_keys(self):
        for index, (_, overrides) in core.PRESETS.items():
            for protected in ("watermark_enabled", "watermark_text", "patch_mode", "scope", "enabled"):
                self.assertNotIn(protected, overrides, "preset {} changes {}".format(index, protected))

    def test_preset_applies_settings(self):
        plugin = make_plugin(enabled=True)
        plugin._on_preset_change(2)  # full preview
        self.assertTrue(plugin.settings["verified"])
        self.assertTrue(plugin.settings["stars_enabled"])
        self.assertEqual(plugin.settings["watermark_enabled"], True)

    def test_copy_and_paste_preset_round_trip(self):
        plugin = make_plugin(enabled=True, verified=True, stars_amount="42")
        plugin._on_copy_preset_click(None)
        clipboard = sys.modules["android_utils"].clipboard
        self.assertIn("custom_profile", clipboard)

        plugin.settings["verified"] = False
        plugin.settings["stars_amount"] = "0"
        plugin._on_paste_preset_click(None)
        self.assertTrue(plugin.settings["verified"])
        self.assertEqual(str(plugin.settings["stars_amount"]), "42")

    def test_paste_rejects_garbage(self):
        plugin = make_plugin(enabled=True)
        sys.modules["android_utils"].clipboard = "not json"
        plugin._on_paste_preset_click(None)
        self.assertEqual(runtime_stub.BulletinRecorder.messages[-1][0], "error")


class HygieneTests(unittest.TestCase):
    def test_plugin_source_never_touches_the_network(self):
        forbidden = ("requests", "urllib", "socket", "http.client", "httpx", "TelegramClient", "sendRequest")
        for name in ("main.py", "core.py", "verify.py"):
            source = (PROJECT / name).read_text(encoding="utf-8")
            for token in forbidden:
                self.assertNotIn("import " + token, source, "{} imports {}".format(name, token))
                self.assertNotIn(token + ".", source, "{} uses {}".format(name, token))

    def test_no_hardcoded_credentials(self):
        pattern = ("api_id", "api_hash", "token =", "password =", "secret =")
        for name in ("main.py", "core.py", "verify.py", "metainfo.yml"):
            text = (PROJECT / name).read_text(encoding="utf-8").lower()
            for token in pattern:
                self.assertNotIn(token, text, "{} looks like it stores {}".format(name, token))


if __name__ == "__main__":
    unittest.main(verbosity=2)
