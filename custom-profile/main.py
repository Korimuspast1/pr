"""Custom Profile — local-only visual profile overrides for exteraGram.

The plugin deliberately changes only objects that already live in the client
process.  It never sends a Telegram request, writes an account setting, or
changes a profile on the server.
"""

from typing import Any, Dict, Iterable, List, Optional, Tuple

from base_plugin import BasePlugin, MethodHook
from hook_utils import find_class, get_private_field, set_private_field
from ui.bulletin import BulletinHelper
from ui.settings import Divider, EditText, Header, Input, Selector, Switch, Text

try:
    from elyx import strings
except Exception:  # Makes a source file easier to inspect outside exteraGram.
    def strings(key: str, *args, **kwargs) -> str:
        return key

try:
    from org.telegram.messenger import MessagesController, UserConfig
except Exception:
    MessagesController = None
    UserConfig = None


# Values are also used by the reset action.  Empty values mean "keep Telegram's
# real local object value", not "replace it with an empty field".
DEFAULTS: Dict[str, Any] = {
    "enabled": False,
    "scope": 0,
    "verified": False,
    "premium": False,
    "emoji_status_enabled": False,
    "emoji_status_id": "",
    "stars_enabled": False,
    "stars_amount": "1000000",
    "stars_level": "10",
    "display_name": "",
    "display_username": "",
    "display_bio": "",
    "name_color_enabled": False,
    "name_color_index": "0",
    "profile_color_enabled": False,
    "profile_color_index": "0",
}

_MISSING = object()


class _PatchArgumentsHook(MethodHook):
    """Patches User/UserFull arguments before the client stores or broadcasts them."""

    def __init__(self, plugin):
        self.plugin = plugin

    def before_hooked_method(self, param):
        self.plugin._patch_hook_values(param.args)


class _PatchReturnedUserHook(MethodHook):
    """Patches users retrieved from MessagesController/UserConfig caches."""

    def __init__(self, plugin):
        self.plugin = plugin

    def after_hooked_method(self, param):
        self.plugin._patch_user(param.getResult())


class _PatchReturnedFullUserHook(MethodHook):
    """Patches a full-profile object retrieved from MessagesController."""

    def __init__(self, plugin):
        self.plugin = plugin

    def after_hooked_method(self, param):
        self.plugin._patch_full_user(param.getResult())


class _PatchNotificationHook(MethodHook):
    """Covers fresh UserFull objects delivered to an already-open profile."""

    def __init__(self, plugin):
        self.plugin = plugin

    def before_hooked_method(self, param):
        self.plugin._patch_hook_values(param.args)


class CustomProfilePlugin(BasePlugin):
    """Local-only profile cosmetics for the account(s) signed in on this device."""

    def on_plugin_load(self):
        self._remembered: Dict[int, Tuple[Any, Dict[str, Any]]] = {}
        self._hook_handlers: List[MethodHook] = []
        self._hook_count = 0
        self._unavailable: List[str] = []

        self._install_hooks()
        self._apply_to_cached_profiles()

        if self._hook_count:
            self.log(strings("load_ok"))
        else:
            self.log(strings("load_partial"))

    def on_plugin_unload(self):
        # Hooks are removed by the host. Restore every object this instance
        # touched first, so turning off the plugin does not leave a stale local
        # look until a server refresh happens.
        self._restore_originals()
        self._hook_handlers = []

    # ------------------------------------------------------------------
    # Settings screen
    # ------------------------------------------------------------------
    def create_settings(self) -> List[Any]:
        return [
            Header(text=strings("settings_title")),
            Text(
                text=strings("settings_notice"),
                subtext=strings("settings_notice_subtext"),
                icon="msg_info",
            ),
            Switch(
                key="enabled",
                text=strings("enabled"),
                default=DEFAULTS["enabled"],
                subtext=strings("enabled_subtext"),
                icon="msg_settings",
                on_change=self._on_enabled_change,
            ),
            Selector(
                key="scope",
                text=strings("scope"),
                default=DEFAULTS["scope"],
                subtext=strings("scope_subtext"),
                items=[strings("scope_active"), strings("scope_all")],
                icon="msg_list",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=strings("badges_title")),
            Switch(
                key="verified",
                text=strings("verified"),
                default=DEFAULTS["verified"],
                subtext=strings("verified_subtext"),
                icon="msg_verified",
                on_change=self._on_visual_setting_change,
            ),
            Switch(
                key="premium",
                text=strings("premium"),
                default=DEFAULTS["premium"],
                subtext=strings("premium_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Switch(
                key="emoji_status_enabled",
                text=strings("emoji_status_enabled"),
                default=DEFAULTS["emoji_status_enabled"],
                subtext=strings("emoji_status_enabled_subtext"),
                icon="msg_emoji",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="emoji_status_id",
                text=strings("emoji_status_id"),
                default=DEFAULTS["emoji_status_id"],
                subtext=strings("emoji_status_id_subtext"),
                icon="msg_id",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=strings("stars_title")),
            Switch(
                key="stars_enabled",
                text=strings("stars_enabled"),
                default=DEFAULTS["stars_enabled"],
                subtext=strings("stars_enabled_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="stars_amount",
                text=strings("stars_amount"),
                default=DEFAULTS["stars_amount"],
                subtext=strings("stars_amount_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="stars_level",
                text=strings("stars_level"),
                default=DEFAULTS["stars_level"],
                subtext=strings("stars_level_subtext"),
                icon="msg_premium",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=strings("appearance_title")),
            Input(
                key="display_name",
                text=strings("display_name"),
                default=DEFAULTS["display_name"],
                subtext=strings("display_name_subtext"),
                icon="msg_edit",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="display_username",
                text=strings("display_username"),
                default=DEFAULTS["display_username"],
                subtext=strings("display_username_subtext"),
                icon="msg_link",
                on_change=self._on_visual_setting_change,
            ),
            EditText(
                key="display_bio",
                hint=strings("display_bio_hint"),
                default=DEFAULTS["display_bio"],
                multiline=True,
                max_length=255,
            ),
            Switch(
                key="name_color_enabled",
                text=strings("name_color_enabled"),
                default=DEFAULTS["name_color_enabled"],
                subtext=strings("name_color_enabled_subtext"),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="name_color_index",
                text=strings("name_color_index"),
                default=DEFAULTS["name_color_index"],
                subtext=strings("name_color_index_subtext"),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Switch(
                key="profile_color_enabled",
                text=strings("profile_color_enabled"),
                default=DEFAULTS["profile_color_enabled"],
                subtext=strings("profile_color_enabled_subtext"),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Input(
                key="profile_color_index",
                text=strings("profile_color_index"),
                default=DEFAULTS["profile_color_index"],
                subtext=strings("profile_color_index_subtext"),
                icon="msg_palette",
                on_change=self._on_visual_setting_change,
            ),
            Divider(),
            Header(text=strings("actions_title")),
            Text(
                text=strings("apply"),
                subtext=strings("apply_subtext"),
                icon="msg_retry",
                on_click=self._on_apply_click,
            ),
            Text(
                text=strings("reset"),
                subtext=strings("reset_subtext"),
                icon="msg_delete",
                red=True,
                on_click=self._on_reset_click,
            ),
            Text(
                text=strings("about"),
                subtext=strings("about_subtext"),
                icon="msg_info",
            ),
        ]

    def _on_enabled_change(self, new_value: Any):
        if self._as_bool(new_value):
            self._restore_originals()
            self._apply_to_cached_profiles()
        else:
            self._restore_originals()

    def _on_visual_setting_change(self, _new_value: Any):
        # Restoring first is important when a toggle is turned off: otherwise a
        # value changed by a previous setting would remain in an old cache item.
        self._restore_originals()
        if self._is_enabled():
            self._apply_to_cached_profiles()

    def _on_apply_click(self, _view: Any):
        self._restore_originals()
        if self._is_enabled():
            self._apply_to_cached_profiles()
        BulletinHelper.show_success(strings("applied"))

    def _on_reset_click(self, _view: Any):
        self._restore_originals()
        for key, value in DEFAULTS.items():
            self.set_setting(key, value, reload_settings=False)
        self.set_setting("enabled", False, reload_settings=True)
        BulletinHelper.show_info(strings("reset_done"))

    # ------------------------------------------------------------------
    # Hook setup
    # ------------------------------------------------------------------
    def _install_hooks(self):
        controller_class = find_class("org.telegram.messenger.MessagesController")
        user_config_class = find_class("org.telegram.messenger.UserConfig")
        notification_class = find_class("org.telegram.messenger.NotificationCenter")

        if controller_class:
            self._hook_all(controller_class, "putUser", _PatchArgumentsHook(self))
            self._hook_all(controller_class, "putUsers", _PatchArgumentsHook(self))
            self._hook_all(controller_class, "processUserInfo", _PatchArgumentsHook(self))
            self._hook_all(controller_class, "getUser", _PatchReturnedUserHook(self))
            self._hook_all(controller_class, "getUserFull", _PatchReturnedFullUserHook(self))
        else:
            self._unavailable.append("MessagesController")

        if user_config_class:
            self._hook_all(user_config_class, "getCurrentUser", _PatchReturnedUserHook(self))
        else:
            self._unavailable.append("UserConfig")

        # A profile that is already on screen receives a fresh UserFull through
        # NotificationCenter rather than via getUserFull(). This lightweight
        # hook only touches objects identified as our own User/UserFull.
        if notification_class:
            self._hook_all(notification_class, "postNotificationName", _PatchNotificationHook(self))
        else:
            self._unavailable.append("NotificationCenter")

    def _hook_all(self, clazz: Any, method_name: str, handler: MethodHook):
        try:
            hooks = self.hook_all_methods(clazz, method_name, handler)
            if hooks:
                self._hook_handlers.append(handler)
                self._hook_count += len(hooks)
        except Exception as error:
            self.log("Custom Profile: could not hook {} ({})".format(method_name, error))

    # ------------------------------------------------------------------
    # Object identification and safe local writes
    # ------------------------------------------------------------------
    def _is_enabled(self) -> bool:
        return self._as_bool(self.get_setting("enabled", DEFAULTS["enabled"]))

    @staticmethod
    def _as_bool(value: Any) -> bool:
        if isinstance(value, str):
            return value.strip().lower() not in ("", "0", "false", "no", "off")
        return bool(value)

    @staticmethod
    def _as_int(value: Any, default: int = 0, minimum: Optional[int] = None, maximum: Optional[int] = None) -> int:
        try:
            parsed = int(str(value).strip())
        except (TypeError, ValueError):
            try:
                parsed = int(default)
            except (TypeError, ValueError):
                parsed = 0
        if minimum is not None:
            parsed = max(minimum, parsed)
        if maximum is not None:
            parsed = min(maximum, parsed)
        return parsed

    @staticmethod
    def _setting_text(value: Any) -> str:
        return str(value or "").strip()

    def _account_numbers(self) -> Iterable[int]:
        if UserConfig is None:
            return []
        try:
            count = int(getattr(UserConfig, "MAX_ACCOUNT_COUNT"))
        except Exception:
            count = 4
        return range(max(1, min(count, 10)))

    def _selected_account(self) -> int:
        if UserConfig is None:
            return 0
        try:
            return int(getattr(UserConfig, "selectedAccount"))
        except Exception:
            return 0

    def _self_ids(self) -> List[int]:
        if UserConfig is None:
            return []
        active_only = self._as_int(self.get_setting("scope", DEFAULTS["scope"])) == 0
        accounts = [self._selected_account()] if active_only else self._account_numbers()
        result: List[int] = []
        for account in accounts:
            try:
                user_id = int(UserConfig.getInstance(account).getClientUserId())
                if user_id:
                    result.append(user_id)
            except Exception:
                continue
        return result

    def _is_target_user_id(self, user_id: Any) -> bool:
        try:
            return int(user_id) in self._self_ids()
        except (TypeError, ValueError):
            return False

    @staticmethod
    def _read_field(obj: Any, name: str, default: Any = _MISSING) -> Any:
        if obj is None:
            return default
        try:
            return getattr(obj, name)
        except Exception:
            try:
                value = get_private_field(obj, name)
                return default if value is None and default is _MISSING else value
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

    @staticmethod
    def _class_name(obj: Any) -> str:
        try:
            return str(obj.getClass().getName())
        except Exception:
            return ""

    def _looks_like_user(self, obj: Any) -> bool:
        name = self._class_name(obj)
        return name.endswith("$TL_user") or name.endswith("$TL_userEmpty") or (
            "TLRPC$" in name and self._read_field(obj, "premium", _MISSING) is not _MISSING
        )

    def _looks_like_full_user(self, obj: Any) -> bool:
        name = self._class_name(obj)
        return "UserFull" in name or "$TL_userFull" in name

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
        # Java Object[] and Java Lists both support length/index access in the
        # bridge. Cap traversal: notification payloads never need a huge walk.
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

        if self._as_bool(self.get_setting("verified", DEFAULTS["verified"])):
            self._set_local(user, "verified", True)
        if self._as_bool(self.get_setting("premium", DEFAULTS["premium"])):
            self._set_local(user, "premium", True)

        visual_name = self._setting_text(self.get_setting("display_name", DEFAULTS["display_name"]))
        if visual_name:
            first_name, separator, last_name = visual_name.partition(" ")
            self._set_local(user, "first_name", first_name)
            self._set_local(user, "last_name", last_name.strip() if separator else "")

        visual_username = self._setting_text(self.get_setting("display_username", DEFAULTS["display_username"]))
        if visual_username:
            self._set_local(user, "username", visual_username.lstrip("@"))

        if self._as_bool(self.get_setting("emoji_status_enabled", DEFAULTS["emoji_status_enabled"])):
            emoji_status = self._new_emoji_status()
            if emoji_status is not None:
                self._set_local(user, "emoji_status", emoji_status)

        if self._as_bool(self.get_setting("name_color_enabled", DEFAULTS["name_color_enabled"])):
            color = self._new_peer_color("name_color_index")
            if color is not None:
                self._set_local(user, "color", color)

        if self._as_bool(self.get_setting("profile_color_enabled", DEFAULTS["profile_color_enabled"])):
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

        visual_bio = self._setting_text(self.get_setting("display_bio", DEFAULTS["display_bio"]))
        if visual_bio:
            self._set_local(user_full, "about", visual_bio)

        if self._as_bool(self.get_setting("stars_enabled", DEFAULTS["stars_enabled"])):
            rating = self._new_star_rating()
            if rating is not None:
                self._set_local(user_full, "stars_rating", rating)
        return True

    def _new_emoji_status(self) -> Optional[Any]:
        document_id = self._as_int(self.get_setting("emoji_status_id", DEFAULTS["emoji_status_id"]), 0, 1)
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

        amount = self._as_int(
            self.get_setting("stars_amount", DEFAULTS["stars_amount"]),
            DEFAULTS["stars_amount"],
            minimum=0,
            maximum=9_000_000_000_000_000_000,
        )
        level = self._as_int(
            self.get_setting("stars_level", DEFAULTS["stars_level"]),
            DEFAULTS["stars_level"],
            minimum=1,
            maximum=99,
        )
        self._write_field(rating, "flags", 1)
        self._write_field(rating, "level", level)
        self._write_field(rating, "current_level_stars", amount)
        self._write_field(rating, "stars", amount)
        self._write_field(rating, "next_level_stars", min(9_000_000_000_000_000_000, amount + 1))
        return rating

    def _new_peer_color(self, setting_key: str) -> Optional[Any]:
        color_index = self._as_int(self.get_setting(setting_key, "0"), 0, minimum=0, maximum=999)
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
        if not self._is_enabled() or UserConfig is None or MessagesController is None:
            return
        for account in self._account_numbers():
            try:
                config = UserConfig.getInstance(account)
                user = config.getCurrentUser()
                if user is None:
                    continue
                if not self._is_target_user_id(self._read_field(user, "id", 0)):
                    continue
                self._patch_user(user)
                controller = MessagesController.getInstance(account)
                user_id = self._read_field(user, "id", 0)
                self._patch_user(controller.getUser(user_id))
                self._patch_full_user(controller.getUserFull(user_id))
            except Exception as error:
                self.log("Custom Profile: cache refresh failed ({})".format(error))
