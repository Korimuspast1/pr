"""CSTM Core — safe local preset loader for Custom Profile.

A .cstm file is JSON *data*, never Python code.  This plugin accepts a strict
allow-list of Custom Profile settings, so opening a preset cannot install a
plugin or execute a payload.
"""

import hashlib
import json
import os
from threading import Thread
from typing import Any, Dict, List, Tuple
from urllib.parse import urlparse
from urllib.request import Request, urlopen

from base_plugin import BasePlugin
from file_utils import FilesController, ensure_dir_exists, get_plugins_dir, read_file, write_file
from ui.bulletin import BulletinHelper
from ui.settings import Divider, Header, Input, Switch, Text

try:
    from elyx import SettingsController
except Exception:
    SettingsController = None


__id__ = "cstm_core"
__name__ = "CSTM Core"
__description__ = "Opens validated .cstm Custom Profile presets and can download them from an explicit HTTPS link. Presets are data only: no code execution or plugin installation."
__author__ = "Custom Profile"
__version__ = "1.0.0"
__icon__ = "exteraPlugins/1"
__app_version__ = ">=12.5.1"
__sdk_version__ = ">=1.4.4.3"
__requirements__ = []


FORMAT = "custom-profile-cstm"
FORMAT_VERSION = 1
TARGET_PLUGIN_ID = "custom_profile"
MAX_DOWNLOAD_BYTES = 64 * 1024
MAX_TEXT_LENGTHS = {
    "display_name": 64,
    "display_username": 32,
    "display_bio": 255,
}
BOOLEAN_KEYS = {
    "enabled",
    "verified",
    "premium",
    "emoji_status_enabled",
    "stars_enabled",
    "name_color_enabled",
    "profile_color_enabled",
}
INTEGER_KEYS = {
    "scope": (0, 1),
    "emoji_status_id": (0, 9_000_000_000_000_000_000),
    "stars_amount": (0, 9_000_000_000_000_000_000),
    "stars_level": (1, 99),
    "name_color_index": (0, 999),
    "profile_color_index": (0, 999),
}
TEXT_KEYS = set(MAX_TEXT_LENGTHS)
ALLOWED_KEYS = BOOLEAN_KEYS | set(INTEGER_KEYS) | TEXT_KEYS


class CstmValidationError(ValueError):
    """Raised for an unsupported or malformed .cstm document."""


class CstmCorePlugin(BasePlugin):
    """Registers the .cstm file handler and imports safe profile presets."""

    def on_plugin_load(self):
        self._file_handler_secret = None
        self._register_cstm_handler()
        self.log("CSTM Core loaded. .cstm files are treated as JSON data only.")

    def on_plugin_unload(self):
        if self._file_handler_secret is not None:
            try:
                FilesController.unregister("cstm", self._file_handler_secret)
            except Exception:
                pass
            self._file_handler_secret = None

    def create_settings(self) -> List[Any]:
        return [
            Header(text="CSTM Core"),
            Text(
                text="What CSTM files do",
                subtext="A .cstm is a validated JSON profile preset. It cannot contain Python, install a plugin, or make a Telegram purchase.",
                icon="msg_info",
            ),
            Switch(
                key="allow_downloads",
                text="Allow HTTPS preset downloads",
                default=True,
                subtext="Downloads only the URL you explicitly paste below. The file must end in .cstm and is limited to 64 KB.",
                icon="msg_download",
            ),
            Input(
                key="download_url",
                text="CSTM preset URL",
                default="",
                subtext="HTTPS only. Example: a raw GitHub link ending in .cstm.",
                icon="msg_link",
            ),
            Text(
                text="Download and apply preset",
                subtext="Downloads, validates, saves a local copy, then writes only approved Custom Profile settings.",
                icon="msg_download",
                on_click=self._on_download_click,
            ),
            Divider(),
            Header(text="Use a file from Telegram"),
            Text(
                text="Tap any .cstm document",
                subtext="When this plugin is enabled, opening a .cstm file in exteraGram validates and applies it. Afterwards open Custom Profile and tap Apply.",
                icon="msg_document",
            ),
            Text(
                text="Open Custom Profile afterwards",
                subtext="CSTM Core writes preset values only. Custom Profile remains the component that renders the local visual preview.",
                icon="msg_settings",
            ),
        ]

    # ------------------------------------------------------------------
    # CSTM file opening and downloading
    # ------------------------------------------------------------------
    def _register_cstm_handler(self):
        try:
            self._file_handler_secret = FilesController.register(
                FilesController.FileInfo(ext="cstm", on_click=self._on_cstm_file_open)
            )
        except Exception as error:
            self.log("CSTM Core: cannot register .cstm handler ({})".format(error))

    def _on_cstm_file_open(self, args: Any):
        try:
            path = str(args.file.getAbsolutePath())
            text = read_file(path)
            if text is None:
                raise CstmValidationError("could not read this file")
            if len(text.encode("utf-8")) > MAX_DOWNLOAD_BYTES:
                raise CstmValidationError("file is larger than 64 KB")
            name = self._apply_cstm_text(text)
            BulletinHelper.show_success("CSTM preset applied: {}. Open Custom Profile and tap Apply.".format(name))
        except CstmValidationError as error:
            BulletinHelper.show_error("CSTM was not applied: {}".format(error))
        except Exception as error:
            self.log("CSTM Core file error: {}".format(error))
            BulletinHelper.show_error("CSTM file could not be opened safely.")

    def _on_download_click(self, _view: Any):
        if not self._as_bool(self.get_setting("allow_downloads", True)):
            BulletinHelper.show_info("HTTPS downloads are disabled in CSTM Core settings.")
            return
        url = str(self.get_setting("download_url", "") or "").strip()
        try:
            self._validate_download_url(url)
        except CstmValidationError as error:
            BulletinHelper.show_error("CSTM download was not started: {}".format(error))
            return
        BulletinHelper.show_info("Downloading and validating the CSTM preset…")
        Thread(target=self._download_and_apply, args=(url,), daemon=True).start()

    def _download_and_apply(self, url: str):
        try:
            request = Request(url, headers={"Accept": "application/json, text/plain;q=0.9"})
            with urlopen(request, timeout=15) as response:
                final_url = response.geturl()
                self._validate_download_url(final_url)
                content_length = response.headers.get("Content-Length")
                if content_length and int(content_length) > MAX_DOWNLOAD_BYTES:
                    raise CstmValidationError("server reported a file larger than 64 KB")
                raw = response.read(MAX_DOWNLOAD_BYTES + 1)
            if len(raw) > MAX_DOWNLOAD_BYTES:
                raise CstmValidationError("download is larger than 64 KB")
            text = raw.decode("utf-8")
            name, normalized = self._parse_cstm(text)
            self._save_download(normalized)
            self._apply_profile_settings(normalized["profile"])
            BulletinHelper.show_success("CSTM preset applied: {}. Open Custom Profile and tap Apply.".format(name))
        except CstmValidationError as error:
            BulletinHelper.show_error("CSTM was not applied: {}".format(error))
        except Exception as error:
            self.log("CSTM Core download error: {}".format(error))
            BulletinHelper.show_error("CSTM download failed. Check the HTTPS link and try again.")

    @staticmethod
    def _validate_download_url(url: str):
        parsed = urlparse(url)
        if parsed.scheme.lower() != "https" or not parsed.netloc:
            raise CstmValidationError("only a complete HTTPS URL is allowed")
        if not parsed.path.lower().endswith(".cstm"):
            raise CstmValidationError("the URL path must end in .cstm")

    # ------------------------------------------------------------------
    # Strict data-only format and bridge to Custom Profile
    # ------------------------------------------------------------------
    def _apply_cstm_text(self, text: str) -> str:
        name, normalized = self._parse_cstm(text)
        self._apply_profile_settings(normalized["profile"])
        return name

    def _parse_cstm(self, text: str) -> Tuple[str, Dict[str, Any]]:
        try:
            payload = json.loads(text)
        except (TypeError, ValueError) as error:
            raise CstmValidationError("not valid JSON") from error
        if not isinstance(payload, dict):
            raise CstmValidationError("top level must be an object")
        if set(payload) - {"format", "version", "name", "profile"}:
            raise CstmValidationError("contains unknown top-level fields")
        if payload.get("format") != FORMAT:
            raise CstmValidationError("unsupported CSTM format")
        if payload.get("version") != FORMAT_VERSION:
            raise CstmValidationError("unsupported CSTM version")
        name = payload.get("name")
        if not isinstance(name, str) or not name.strip() or len(name.strip()) > 64:
            raise CstmValidationError("name must be 1–64 characters")
        profile = payload.get("profile")
        if not isinstance(profile, dict) or not profile:
            raise CstmValidationError("profile must be a non-empty object")
        unknown = set(profile) - ALLOWED_KEYS
        if unknown:
            raise CstmValidationError("contains unsupported setting: {}".format(sorted(unknown)[0]))

        normalized: Dict[str, Any] = {}
        for key, value in profile.items():
            normalized[key] = self._validate_profile_value(key, value)
        return name.strip(), {
            "format": FORMAT,
            "version": FORMAT_VERSION,
            "name": name.strip(),
            "profile": normalized,
        }

    @staticmethod
    def _validate_profile_value(key: str, value: Any) -> Any:
        if key in BOOLEAN_KEYS:
            if type(value) is not bool:
                raise CstmValidationError("{} must be true or false".format(key))
            return value
        if key in INTEGER_KEYS:
            if type(value) is bool:
                raise CstmValidationError("{} must be a number".format(key))
            try:
                number = int(value)
            except (TypeError, ValueError) as error:
                raise CstmValidationError("{} must be a whole number".format(key)) from error
            lower, upper = INTEGER_KEYS[key]
            if not lower <= number <= upper:
                raise CstmValidationError("{} is outside its allowed range".format(key))
            # Custom Profile stores numeric input as text. Keeping that shape
            # means the same preset works across both settings screens.
            return str(number)
        if key in TEXT_KEYS:
            if not isinstance(value, str):
                raise CstmValidationError("{} must be text".format(key))
            value = value.strip()
            if len(value) > MAX_TEXT_LENGTHS[key]:
                raise CstmValidationError("{} is too long".format(key))
            if key == "display_username" and value:
                username = value.lstrip("@")
                if not username.replace("_", "").isalnum():
                    raise CstmValidationError("display_username contains unsupported characters")
            return value
        raise CstmValidationError("unsupported setting")

    def _apply_profile_settings(self, profile: Dict[str, Any]):
        if SettingsController is None:
            raise CstmValidationError("this exteraGram build does not expose the shared settings API")
        try:
            settings = SettingsController(TARGET_PLUGIN_ID)
            for key, value in profile.items():
                settings.set_setting(key, value, reload_settings=False)
        except Exception as error:
            raise CstmValidationError("Custom Profile must be installed before importing a preset") from error

    @staticmethod
    def _as_bool(value: Any) -> bool:
        if isinstance(value, str):
            return value.strip().lower() not in ("", "0", "false", "no", "off")
        return bool(value)

    @staticmethod
    def _save_download(payload: Dict[str, Any]):
        text = json.dumps(payload, ensure_ascii=False, sort_keys=True, indent=2) + "\n"
        digest = hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]
        folder = os.path.join(get_plugins_dir(), "cstm_core", "downloads")
        ensure_dir_exists(folder)
        write_file(os.path.join(folder, "{}.cstm".format(digest)), text)
