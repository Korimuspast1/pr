"""Custom Profile 2.0.0 — local-only visual profile overrides for exteraGram.

Design goals of the 2.0 rewrite
-------------------------------
* **Local only.** The plugin never sends a Telegram request, never writes an
  account setting and never changes a profile on the server. It only edits Java
  model objects that already live inside the exteraGram process.
* **Read-only by default.** Earlier versions patched cached ``User`` objects
  in place inside ``MessagesController.putUser``/``getUser``. Incoming update
  mode keeps that behaviour, but the default (``patch_mode = 0``) patches only
  the objects handed back by ``getUser``/``getUserFull`` for rendering, so the
  client cache always stores Telegram's real values.
* **Honest by default.** A ``·preview`` marker is appended to locally patched
  names until the user turns it off, and the README documents clearly that
  screenshots must not be used as proof of anything.
* **Input-safe.** Everything the user types is length-limited, stripped of
  control and bidi-override characters, and validated before it is written into
  a TL object.
* **Self-checking.** The shipped archive carries a SHA-256 manifest of its own
  modules; a modified or missing module is reported at load time.
"""

from __future__ import annotations

import json
from typing import Any, Dict, Iterable, List, Optional, Tuple

from base_plugin import BasePlugin, MethodHook

try:
    from hook_utils import find_class, get_private_field, set_private_field
except Exception:  # pragma: no cover - the host always provides these

    def find_class(name):
        return None

    def get_private_field(obj, name):
        return None

    def set_private_field(obj, name, value):
        return False


from ui.bulletin import BulletinHelper
from ui.settings import Divider, EditText, Header, Input, Selector, Switch, Text

from core import (
    DEFAULTS,
    MAX_BIO_LENGTH,
    MAX_CROWN_LEVEL,
    MAX_NAME_LENGTH,
    MAX_STARS,
    MIN_CROWN_LEVEL,
    PLUGIN_VERSION,
    PRESET_ITEMS,
    PRESETS,
    as_bool,
    as_int,
    as_text,
    clamp_color_index,
    clamp_crown_level,
    clamp_stars,
    sanitize_backup,
    sanitize_emoji_document_id,
    sanitize_text,
    split_display_name,
    validate_username,
    with_watermark,
)
from verify import describe as describe_verification, verify_installation

try:  # Elyx exposes the plugin-localised string table.
    from elyx import strings as st
except Exception:  # Makes a source file easier to inspect outside exteraGram.

    def st(key: str, *args, **kwargs) -> str:
        if kwargs.get("default") is not None:
            return str(kwargs["default"])
        return key


_MISSING = object()


class CustomProfilePlugin(BasePlugin):
    """Local-only profile cosmetics for the account(s) signed in on this device."""

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------
    def on_plugin_load(self):
        self._remembered: Dict[int, Tuple[Any, Dict[str, Any]]] = {}
        self._hook_count = 0
        self._unavailable: List[str] = []
        self._last_error: Optional[str] = None
        self._verified_state: Optional[bool] = None
        self._verified_detail: str = ""

        self._self_ids_cache: Optional[List[int]] = None

        self._run_security_check()
        self._install_hooks()
        self._apply_to_cached_profiles()

        if self._hook_count:
            self.log("Custom Profile {}: {} hook(s) installed".format(PLUGIN_VERSION, self._hook_count))
        else:
            self.log("Custom Profile {}: no hooks installed".format(PLUGIN_VERSION))

    def on_plugin_unload(self):
        # Hooks are removed by the host. Restore every object this instance
        # touched first, so turning the plugin off never leaves a stale local
        # look behind until the next server refresh.
        self._restore_originals()

    # ------------------------------------------------------------------
    # Security / diagnostics
    # ------------------------------------------------------------------
    def _run_security_check(self):
        try:
            ok, report = verify_installation()
        except Exception as error:  # never let a check break the plugin
            self._verified_state = None
            self._verified_detail = str(error)
            return
        self._verified_state = ok
        self._verified_detail = describe_verification(report)
        if not ok:
            self.log("Custom Profile: archive integrity check failed -> " + self._verified_detail)

    @property
    def _integrity_icon(self) -> str:
        return "msg_info" if self._verified_state is not False else "msg_error"

    def _integrity_text(self) -> str:
        if self._verified_state is None:
            return st("integrity_unknown", default="Archive check: skipped")
        if self._verified_state:
            return st("integrity_ok", default="Archive check: verified")
        return st("integrity_failed", default="Archive check: files modified")

    # ------------------------------------------------------------------
    # Settings screen
    # ------------------------------------------------------------------
    def create_settings(self) -> List[Any]:
        return [
            Header(text=st("settings_title")),
            Text(
                text=st("settings_notice"),
                subtext=st("settings_notice_subtext"),
                icon="msg_info",
            ),
            Switch(
                key="enabled",
                text=st("enabled"),
                default=DEFAULTS["enabled"],
                subtext=st("enabled_subtext"),
                icon="msg_settings",
                on_change=self._on_enabled_change,
            ),
            Selector(
                key="scope",
                text=st("scope"),
                default=DEFAULTS["scope"],
                subtext=st("scope_subtext"),
                items=[st("scope_active"), st("scope_all")],
                icon="msg_list",
                on_change=self._on_visual_setting_change,
            ),
            Selector(
                key="patch_mode",
                text=st("patch_mode"),
                default=DEFAULTS["patch_mode"],
                subtext=st("patch_mode_subtext"),
                items=[st("patch_mode_read_only"), st("patch_mode_updates")],
                icon="msg_security",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=st("badges_title")),
            Switch(
                key="verified",
                text=st("verified"),
                default=DEFAULTS["verified"],
                subtext=st("verified_subtext"),
                icon="msg_verified",
                on_change=self._on_visual_setting_change,
            ),
            Switch(
                key="premium",
                text=st("premium"),
                default=DEFAULTS["premium"],
                subtext=st("premium_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Switch(
                key="emoji_status_enabled",
                text=st("emoji_status_enabled"),
                default=DEFAULTS["emoji_status_enabled"],
                subtext=st("emoji_status_enabled_subtext"),
                icon="msg_emoji",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="emoji_status_id",
                text=st("emoji_status_id"),
                default=DEFAULTS["emoji_status_id"],
                subtext=st("emoji_status_id_subtext"),
                icon="msg_id",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=st("stars_title")),
            Switch(
                key="stars_enabled",
                text=st("stars_enabled"),
                default=DEFAULTS["stars_enabled"],
                subtext=st("stars_enabled_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="stars_amount",
                text=st("stars_amount"),
                default=DEFAULTS["stars_amount"],
                subtext=st("stars_amount_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="stars_level",
                text=st("stars_level"),
                default=DEFAULTS["stars_level"],
                subtext=st("stars_level_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=st("appearance_title")),
            Input(
                key="display_name",
                text=st("display_name"),
                default=DEFAULTS["display_name"],
                subtext=st("display_name_subtext"),
                icon="msg_edit",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="display_username",
                text=st("display_username"),
                default=DEFAULTS["display_username"],
                subtext=st("display_username_subtext"),
                icon="msg_link",
                on_change=self._on_visual_setting_change,
            ),
            EditText(
                key="display_bio",
                hint=st("display_bio_hint"),
                default=DEFAULTS["display_bio"],
                multiline=True,
                max_length=MAX_BIO_LENGTH,
            ),
            Switch(
                key="name_color_enabled",
                text=st("name_color_enabled"),
                default=DEFAULTS["name_color_enabled"],
                subtext=st("name_color_enabled_subtext"),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Selector(
                key="name_color_index",
                text=st("name_color_index"),
                default=0,
                items=self._color_items(),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Switch(
                key="profile_color_enabled",
                text=st("profile_color_enabled"),
                default=DEFAULTS["profile_color_enabled"],
                subtext=st("profile_color_enabled_subtext"),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Selector(
                key="profile_color_index",
                text=st("profile_color_index"),
                default=0,
                items=self._color_items(),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=st("honesty_title")),
            Switch(
                key="watermark_enabled",
                text=st("watermark_enabled"),
                default=DEFAULTS["watermark_enabled"],
                subtext=st("watermark_enabled_subtext"),
                icon="msg_info",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="watermark_text",
                text=st("watermark_text"),
                default=DEFAULTS["watermark_text"],
                subtext=st("watermark_text_subtext"),
                icon="msg_edit",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=st("themes_title")),
            Selector(
                key="preset",
                text=st("preset"),
                default=0,
                subtext=st("preset_subtext"),
                items=PRESET_ITEMS,
                icon="msg_settings",
                on_change=self._on_preset_change,
            ),
            Divider(),
            Header(text=st("actions_title")),
            Text(
                text=st("apply"),
                subtext=st("apply_subtext"),
                icon="msg_retry",
                on_click=self._on_apply_click,
            ),
            Text(
                text=st("panels_title"),
                subtext=st("panels_subtext"),
                icon="msg_retry",
                create_sub_fragment=self._create_panels_page,
            ),
            Text(
                text=st("reset"),
                subtext=st("reset_subtext"),
                icon="msg_delete",
                red=True,
                on_click=self._on_reset_click,
            ),
            Divider(),
            Header(text=st("diagnostics_title")),
            Text(
                text=self._integrity_text(),
                subtext=self._verified_detail or st("integrity_subtext"),
                icon=self._integrity_icon,
                on_click=self._on_integrity_click,
            ),
            Text(
                text=st("status"),
                subtext=self._status_line(),
                icon="msg_info",
                on_click=self._on_status_click,
            ),
            Text(
                text=st("about"),
                subtext=st("about_subtext"),
                icon="msg_info",
                create_sub_fragment=self._create_about_page,
            ),
        ]

    def _create_about_page(self) -> List[Any]:
        return [
            Header(text=st("about_page_title")),
            Text(text=st("about_limits"), icon="msg_info"),
            Divider(),
            Text(text=st("about_no_requests"), icon="msg_done"),
            Text(text=st("about_no_server_writes"), icon="msg_done"),
            Text(text=st("about_restore"), icon="msg_done"),
            Text(text=st("about_watermark"), icon="msg_done"),
            Divider(),
            Text(text=st("about_version").format(version=PLUGIN_VERSION), icon="msg_info"),
        ]

    def _create_panels_page(self) -> List[Any]:
        return [
            Header(text=st("panels_title")),
            Text(text=st("panels_info"), icon="msg_info"),
            Text(
                text=st("panel_copy_preset"),
                subtext=st("panel_copy_preset_subtext"),
                icon="msg_copy",
                on_click=self._on_copy_preset_click,
            ),
            Text(
                text=st("panel_paste_preset"),
                subtext=st("panel_paste_preset_subtext"),
                icon="msg_link",
                on_click=self._on_paste_preset_click,
            ),
            Divider(),
            Text(
                text=st("panel_restore_now"),
                subtext=st("panel_restore_now_subtext"),
                icon="msg_retry",
                on_click=self._on_restore_now_click,
            ),
        ]

    # ------------------------------------------------------------------
    # Settings callbacks
    # ------------------------------------------------------------------
    def _on_enabled_change(self, new_value: Any):
        self._restore_originals()
        if as_bool(new_value):
            self._apply_to_cached_profiles()

    def _on_visual_setting_change(self, _new_value: Any):
        # Restoring first matters when a toggle is turned off: otherwise a value
        # written by an earlier setting would survive inside the cache object.
        self._restore_originals()
        if self._is_enabled():
            self._apply_to_cached_profiles()

    def _on_preset_change(self, new_value: Any):
        index = as_int(new_value, 0, minimum=0, maximum=max(PRESETS))
        _, overrides = PRESETS.get(index, PRESETS[0])
        self._restore_originals()
        for key, value in overrides.items():
            self.set_setting(key, value)
        # A preset never changes scope, patch mode or the honesty marker.
        if self._is_enabled():
            self._apply_to_cached_profiles()
        BulletinHelper.show_info(st("preset_applied"))

    def _on_apply_click(self, _view: Any):
        self._restore_originals()
        if self._is_enabled():
            self._apply_to_cached_profiles()
        BulletinHelper.show_success(st("applied"))

    def _on_reset_click(self, _view: Any):
        self._restore_originals()
        for key, value in DEFAULTS.items():
            self.set_setting(key, value, reload_settings=False)
        self.set_setting("enabled", False, reload_settings=True)
        BulletinHelper.show_info(st("reset_done"))

    def _on_integrity_click(self, _view: Any):
        self._run_security_check()
        if self._verified_state is False:
            BulletinHelper.show_error(st("integrity_failed"))
        else:
            BulletinHelper.show_success(self._integrity_text())

    def _on_status_click(self, _view: Any):
        BulletinHelper.show_info(self._status_line())

    def _on_restore_now_click(self, _view: Any):
        self._restore_originals()
        BulletinHelper.show_info(st("restored_now"))

    def _on_copy_preset_click(self, _view: Any):
        payload = self._export_preset()
        try:
            from android_utils import copy_to_clipboard

            copy_to_clipboard(payload)
            BulletinHelper.show_copied_to_clipboard()
        except Exception:
            # Fall back to storing the blob and showing it in the status panel.
            self.set_setting("restore_json", payload)
            BulletinHelper.show_info(st("preset_stored"))

    def _on_paste_preset_click(self, _view: Any):
        try:
            from android_utils import get_clipboard_text

            payload = get_clipboard_text()
        except Exception:
            payload = None
        if not payload:
            payload = as_text(self.get_setting("restore_json", ""))
        if not payload:
            BulletinHelper.show_error(st("paste_empty"))
            return
        try:
            parsed = json.loads(payload)
        except (TypeError, ValueError):
            BulletinHelper.show_error(st("paste_invalid"))
            return
        cleaned = sanitize_backup(parsed)
        if not cleaned:
            BulletinHelper.show_error(st("paste_invalid"))
            return
        self._restore_originals()
        for key, value in cleaned.items():
            self.set_setting(key, value)
        if self._is_enabled():
            self._apply_to_cached_profiles()
        BulletinHelper.show_success(st("paste_applied"))

    def _export_preset(self) -> str:
        data = {
            key: self.get_setting(key, DEFAULTS[key])
            for key in DEFAULTS
            if key not in ("restore_json", "enabled")
        }
        data["_plugin"] = "custom_profile"
        data["_version"] = PLUGIN_VERSION
        return json.dumps(data, ensure_ascii=False, separators=(",", ":"))

    def _status_line(self) -> str:
        if not self._is_enabled():
            return st("status_disabled")
        return st("status_active").format(
            hooks=self._hook_count,
            mode=st("patch_mode_read_only") if self._read_only_mode() else st("patch_mode_updates"),
        )

    @staticmethod
    def _color_items() -> List[str]:
        return ["{}: {}".format(index, st("color_" + str(index))) for index in range(7)]

    # ------------------------------------------------------------------
    # Hook setup
    # ------------------------------------------------------------------
    def _install_hooks(self):
        controller_class = find_class("org.telegram.messenger.MessagesController")
        user_config_class = find_class("org.telegram.messenger.UserConfig")

        if controller_class:
            self._hook_all(controller_class, "getUser", _PatchReturnedUserHook(self))
            self._hook_all(controller_class, "getUserFull", _PatchReturnedFullUserHook(self))
            if not self._read_only_mode():
                # Opt-in legacy behaviour: hooking putUser mutates the shared
                # cache object, so we only install these when the user asks for
                # it (patch_mode = 1).
                self._hook_all(controller_class, "putUser", _PatchArgumentsHook(self))
                self._hook_all(controller_class, "putUsers", _PatchArgumentsHook(self))
                self._hook_all(controller_class, "processUserInfo", _PatchArgumentsHook(self))
        else:
            self._unavailable.append("MessagesController")

        if user_config_class:
            self._hook_all(user_config_class, "getCurrentUser", _PatchReturnedUserHook(self))
        else:
            self._unavailable.append("UserConfig")

    def _hook_all(self, clazz: Any, method_name: str, handler: MethodHook):
        try:
            hooks = self.hook_all_methods(clazz, method_name, handler)
            if hooks:
                self._hook_count += len(hooks)
        except Exception as error:
            self.log("Custom Profile: could not hook {} ({})".format(method_name, error))

    # ------------------------------------------------------------------
    # Values / account helpers
    # ------------------------------------------------------------------
    def _is_enabled(self) -> bool:
        return as_bool(self.get_setting("enabled", DEFAULTS["enabled"]))

    def _read_only_mode(self) -> bool:
        return as_int(self.get_setting("patch_mode", DEFAULTS["patch_mode"])) == 0

    def _setting_bool(self, key: str) -> bool:
        return as_bool(self.get_setting(key, DEFAULTS.get(key, False)))

    def _account_numbers(self) -> Iterable[int]:
        try:
            from org.telegram.messenger import UserConfig

            count = int(getattr(UserConfig, "MAX_ACCOUNT_COUNT"))
            return range(max(1, min(count, 10)))
        except Exception:
            return range(4)

    def _selected_account(self) -> int:
        try:
            from org.telegram.messenger import UserConfig

            return int(getattr(UserConfig, "selectedAccount"))
        except Exception:
            return 0

    def _self_ids(self) -> List[int]:
        if self._self_ids_cache is not None:
            return self._self_ids_cache
        result: List[int] = []
        try:
            from org.telegram.messenger import UserConfig
        except Exception:
            return result
        active_only = as_int(self.get_setting("scope", DEFAULTS["scope"])) == 0
        accounts = [self._selected_account()] if active_only else list(self._account_numbers())
        for account in accounts:
            try:
                user_id = int(UserConfig.getInstance(account).getClientUserId())
                if user_id > 0 and user_id not in result:
                    result.append(user_id)
            except Exception:
                continue
        self._self_ids_cache = result
        return result

    def _is_target_user_id(self, user_id: Any) -> bool:
        try:
            return int(user_id) in self._self_ids()
        except (TypeError, ValueError):
            return False

    # ------------------------------------------------------------------
    # Reflection helpers (defensive: every read/write may fail)
    # ------------------------------------------------------------------
    @staticmethod
    def _read_field(obj: Any, name: str, default: Any = None) -> Any:
        if obj is None:
            return default
        try:
            return getattr(obj, name)
        except Exception:
            try:
                value = get_private_field(obj, name)
                return default if value is None else value
            except Exception:
                return default

    @staticmethod
    def _write_field(obj: Any, name: str, value: Any) -> bool:
        if obj is None:
            return False
        try:
            setattr(obj, name, value)
            return True
        except Exception:
            try:
                return bool(set_private_field(obj, name, value))
            except Exception:
                return False

    def _remember(self, obj: Any, name: str):
        if obj is None:
            return
        key = id(obj)
        if key not in self._remembered:
            self._remembered[key] = (obj, {})
        fields = self._remembered[key][1]
        if name not in fields:
            fields[name] = self._read_field(obj, name, _MISSING)

    def _set_local(self, obj: Any, name: str, value: Any) -> bool:
        self._remember(obj, name)
        return self._write_field(obj, name, value)

    def _restore_originals(self):
        for obj, fields in list(getattr(self, "_remembered", {}).values()):
            for name, value in fields.items():
                if value is not _MISSING:
                    self._write_field(obj, name, value)
        if hasattr(self, "_remembered"):
            self._remembered.clear()
        self._self_ids_cache = None

    @staticmethod
    def _class_name(obj: Any) -> str:
        try:
            return str(obj.getClass().getName())
        except Exception:
            return ""

    def _looks_like_user(self, obj: Any) -> bool:
        name = self._class_name(obj)
        if name.endswith("$TL_user") or name.endswith("$TL_userEmpty"):
            return True
        return "TLRPC$" in name and self._read_field(obj, "premium", _MISSING) is not _MISSING

    def _looks_like_full_user(self, obj: Any) -> bool:
        name = self._class_name(obj)
        return "UserFull" in name

    # ------------------------------------------------------------------
    # Local profile patching
    # ------------------------------------------------------------------
    def _patch_hook_values(self, values: Any):
        if not self._is_enabled():
            return
        for value in self._walk_values(values):
            if self._looks_like_user(value):
                self._patch_user(value)
            elif self._looks_like_full_user(value):
                self._patch_full_user(value)

    def _walk_values(self, value: Any, depth: int = 0) -> Iterable[Any]:
        """Walk small Java/Python argument arrays without treating strings as arrays."""
        if value is None or depth > 2 or isinstance(value, (str, bytes, bytearray)):
            return []
        output = [value]
        if isinstance(value, (list, tuple)):
            for item in value:
                output.extend(self._walk_values(item, depth + 1))
            return output
        try:
            length = len(value)
        except Exception:
            return output
        if 0 <= length <= 32:
            for index in range(length):
                try:
                    output.extend(self._walk_values(value[index], depth + 1))
                except Exception:
                    break
        return output

    def _patch_user(self, user: Any) -> bool:
        if not self._is_enabled() or user is None:
            return False
        user_id = self._read_field(user, "id", _MISSING)
        if user_id is _MISSING or not self._is_target_user_id(user_id):
            return False

        if self._setting_bool("verified"):
            self._set_local(user, "verified", True)
        if self._setting_bool("premium"):
            self._set_local(user, "premium", True)

        parts = split_display_name(self.get_setting("display_name", DEFAULTS["display_name"]))
        if parts:
            first_name, last_name = parts
            watermark_on = self._setting_bool("watermark_enabled")
            watermark = self.get_setting("watermark_text", DEFAULTS["watermark_text"])
            first_name = with_watermark(first_name, watermark_on, watermark, MAX_NAME_LENGTH)
            self._set_local(user, "first_name", first_name)
            self._set_local(user, "last_name", last_name)

        username = validate_username(self.get_setting("display_username", DEFAULTS["display_username"]))
        if username:
            self._set_local(user, "username", username)

        if self._setting_bool("emoji_status_enabled"):
            emoji_status = self._new_emoji_status()
            if emoji_status is not None:
                self._set_local(user, "emoji_status", emoji_status)

        if self._setting_bool("name_color_enabled"):
            color = self._new_peer_color("name_color_index")
            if color is not None:
                self._set_local(user, "color", color)

        if self._setting_bool("profile_color_enabled"):
            color = self._new_peer_color("profile_color_index")
            if color is not None:
                self._set_local(user, "profile_color", color)
        return True

    def _patch_full_user(self, user_full: Any) -> bool:
        if not self._is_enabled() or user_full is None:
            return False
        user_id = self._read_field(user_full, "id", _MISSING)
        if user_id is _MISSING or not self._is_target_user_id(user_id):
            return False

        nested_user = self._read_field(user_full, "user", None)
        if nested_user is not None:
            self._patch_user(nested_user)

        bio = sanitize_text(
            self.get_setting("display_bio", DEFAULTS["display_bio"]),
            MAX_BIO_LENGTH,
            allow_newlines=True,
        )
        if bio:
            self._set_local(user_full, "about", bio)

        if self._setting_bool("stars_enabled"):
            rating = self._new_star_rating()
            if rating is not None:
                self._set_local(user_full, "stars_rating", rating)
        return True

    def _new_emoji_status(self) -> Optional[Any]:
        document_id = sanitize_emoji_document_id(self.get_setting("emoji_status_id", ""))
        if not document_id:
            return None
        clazz = find_class("org.telegram.tgnet.TLRPC$TL_emojiStatus")
        status = self._new_java_instance(clazz)
        if status is None:
            self._mark_unavailable("emoji status")
            return None
        self._write_field(status, "document_id", document_id)
        self._write_field(status, "flags", 0)
        return status

    def _new_star_rating(self) -> Optional[Any]:
        # Current Telegram Android schema. The fallback supports clients that
        # carried this generated class in TLRPC instead of TL_stars.
        clazz = find_class("org.telegram.tgnet.tl.TL_stars$Tl_starsRating")
        if clazz is None:
            clazz = find_class("org.telegram.tgnet.TLRPC$Tl_starsRating")
        rating = self._new_java_instance(clazz)
        if rating is None:
            self._mark_unavailable("star rating")
            return None

        amount = clamp_stars(self.get_setting("stars_amount", DEFAULTS["stars_amount"]))
        level = clamp_crown_level(self.get_setting("stars_level", DEFAULTS["stars_level"]))
        self._write_field(rating, "flags", 1)
        self._write_field(rating, "level", level)
        self._write_field(rating, "current_level_stars", amount)
        self._write_field(rating, "stars", amount)
        self._write_field(rating, "next_level_stars", min(MAX_STARS, amount + 1))
        return rating

    def _new_peer_color(self, setting_key: str) -> Optional[Any]:
        color_index = clamp_color_index(self.get_setting(setting_key, "0"))
        clazz = find_class("org.telegram.tgnet.TLRPC$TL_peerColor")
        color = self._new_java_instance(clazz)
        if color is None:
            self._mark_unavailable("peer color")
            return None
        self._write_field(color, "flags", 1)
        self._write_field(color, "color", color_index)
        return color

    @staticmethod
    def _new_java_instance(clazz: Any) -> Optional[Any]:
        if clazz is None:
            return None
        try:
            return clazz.newInstance()
        except Exception:
            try:
                constructor = clazz.getDeclaredConstructor()
                constructor.setAccessible(True)
                return constructor.newInstance()
            except Exception:
                return None

    def _mark_unavailable(self, feature: str):
        if feature not in self._unavailable:
            self._unavailable.append(feature)
            self.log("Custom Profile: {} is unavailable in this client build".format(feature))

    def _apply_to_cached_profiles(self):
        if not self._is_enabled():
            return
        try:
            from org.telegram.messenger import MessagesController, UserConfig
        except Exception:
            return
        for account in self._account_numbers():
            try:
                config = UserConfig.getInstance(account)
                user = config.getCurrentUser()
                if user is None:
                    continue
                user_id = self._read_field(user, "id", 0)
                if not self._is_target_user_id(user_id):
                    continue
                self._patch_user(user)
                controller = MessagesController.getInstance(account)
                self._patch_user(controller.getUser(user_id))
                self._patch_full_user(controller.getUserFull(user_id))
            except Exception as error:
                self._last_error = str(error)
                self.log("Custom Profile: cache refresh failed ({})".format(error))


def _safe_log(plugin, message: str):
    """Log through the host plugin when possible; never raise from a hook."""
    try:
        plugin.log(message)
    except Exception:
        pass


class _PatchArgumentsHook(MethodHook):
    """Patches User/UserFull arguments before the client stores or broadcasts them."""

    def __init__(self, plugin: CustomProfilePlugin):
        self.plugin = plugin

    def before_hooked_method(self, param):
        try:
            self.plugin._patch_hook_values(param.args)
        except Exception as error:  # a hook must never crash the client
            _safe_log(self.plugin, "Custom Profile: update hook error ({})".format(error))


class _PatchReturnedUserHook(MethodHook):
    """Patches users retrieved from MessagesController/UserConfig caches."""

    def __init__(self, plugin: CustomProfilePlugin):
        self.plugin = plugin

    def after_hooked_method(self, param):
        try:
            self.plugin._patch_user(param.getResult())
        except Exception as error:
            _safe_log(self.plugin, "Custom Profile: user hook error ({})".format(error))


class _PatchReturnedFullUserHook(MethodHook):
    """Patches a full-profile object retrieved from MessagesController."""

    def __init__(self, plugin: CustomProfilePlugin):
        self.plugin = plugin

    def after_hooked_method(self, param):
        try:
            self.plugin._patch_full_user(param.getResult())
        except Exception as error:
            _safe_log(self.plugin, "Custom Profile: profile hook error ({})".format(error))
