"""A tiny stand-in for the exteraGram plugin host.

The real host provides `base_plugin`, `hook_utils`, `ui.*`, `elyx` and the
Chaquopy Java bridge. None of that is available in CI, so this module installs
minimal fakes that behave just close enough to exercise the plugin end to end:
plugin settings, hook installation, TL-like objects and controller caches.
"""

from __future__ import annotations

import re
import sys
import types
from pathlib import Path
from typing import Any, Dict, List, Optional

PROJECT = Path(__file__).resolve().parents[1]


# ---------------------------------------------------------------------------
# YAML-ish localisation loader (the files only use `key: "value"` lines)
# ---------------------------------------------------------------------------
def load_strings(path: Path) -> Dict[str, str]:
    data: Dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.lstrip().startswith("#") or ":" not in line:
            continue
        key, _, value = line.partition(":")
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        data[key.strip()] = value
    return data


class Strings:
    """Callable, formatting stand-in for ``elyx.strings``."""

    def __init__(self, table: Dict[str, str]):
        self.table = table
        self.missing: List[str] = []

    def __call__(self, key: str, *args, **kwargs) -> str:
        if kwargs.get("default") is not None:
            return str(kwargs["default"])
        template = self.table.get(key)
        if template is None:
            self.missing.append(key)
            return key
        try:
            return template.format(*args, **kwargs)
        except (IndexError, KeyError):
            return template

    def get(self, key: str, default: Optional[str] = None):
        return self.table.get(key, default if default is not None else key)


# ---------------------------------------------------------------------------
# Java-ish objects
# ---------------------------------------------------------------------------
class JavaClass:
    def __init__(self, name: str):
        self._name = name

    def getName(self) -> str:
        return self._name

    def newInstance(self):
        return TLObject(self._name)

    def getDeclaredConstructor(self):
        class _Ctor:
            def setAccessible(self, _flag):
                return None

            def newInstance(self):
                return TLObject(name)

        name = self._name
        return _Ctor()


class TLObject:
    def __init__(self, class_name: str):
        self._class_name = class_name
        self.id = 0

    def getClass(self):
        return JavaClass(self._class_name)

    def __repr__(self):
        fields = {k: v for k, v in self.__dict__.items() if not k.startswith("_")}
        return "{}({})".format(self._class_name.rsplit("$", 1)[-1], fields)


class User(TLObject):
    def __init__(self, user_id: int = 0):
        super().__init__("org.telegram.tgnet.TLRPC$TL_user")
        self.id = user_id
        self.first_name = "Real"
        self.last_name = "Name"
        self.username = "real_user"
        self.verified = False
        self.premium = False
        self.emoji_status = None
        self.color = None
        self.profile_color = None


class UserFull(TLObject):
    def __init__(self, user_id: int = 0, user: Optional[User] = None):
        super().__init__("org.telegram.tgnet.TLRPC$TL_userFull")
        self.id = user_id
        self.user = user
        self.about = "real bio"
        self.stars_rating = None


class HookParam:
    def __init__(self, args=(), result=None):
        self.args = list(args)
        self._result = result

    def getResult(self):
        return self._result

    def setResult(self, value):
        self._result = value


# ---------------------------------------------------------------------------
# Host fakes
# ---------------------------------------------------------------------------
class BasePluginStub:
    def __init__(self):
        self.settings: Dict[str, Any] = dict(getattr(self, "default_settings", {}))
        self.log_lines: List[str] = []
        self.installed_hooks: List[Any] = []

    # settings ---------------------------------------------------------
    def get_setting(self, key: str, default: Any = None):
        return self.settings.get(key, default)

    def set_setting(self, key: str, value: Any, reload_settings: bool = False):
        self.settings[key] = value

    def log(self, message: str):
        self.log_lines.append(str(message))

    # hooks ------------------------------------------------------------
    def hook_all_methods(self, clazz, method_name: str, handler):
        class Unhook:
            def __init__(self, target, method, handler_):
                self.target = target
                self.method = method
                self.handler = handler_

        unhook = Unhook(clazz, method_name, handler)
        self.installed_hooks.append(unhook)
        return [unhook]

    def hook_method(self, method, handler, priority: int = 0):
        self.installed_hooks.append(method)
        return method


class MethodHookStub:
    pass


class BulletinRecorder:
    messages: List[tuple] = []

    @classmethod
    def show_info(cls, message, *_a, **_kw):
        cls.messages.append(("info", message))

    @classmethod
    def show_success(cls, message, *_a, **_kw):
        cls.messages.append(("success", message))

    @classmethod
    def show_error(cls, message, *_a, **_kw):
        cls.messages.append(("error", message))

    @classmethod
    def show_copied_to_clipboard(cls, *_a, **_kw):
        cls.messages.append(("copied", "ok"))


class _SettingStub:
    def __init__(self, **kwargs):
        self.kwargs = kwargs


def install(module_names: Optional[List[str]] = None) -> Strings:
    """Install every fake module and return the strings table in use."""
    strings = Strings(load_strings(PROJECT / "strings" / "strings_en.yml"))

    base_plugin = types.ModuleType("base_plugin")
    base_plugin.BasePlugin = BasePluginStub
    base_plugin.MethodHook = MethodHookStub
    base_plugin.MethodReplacement = MethodHookStub
    sys.modules["base_plugin"] = base_plugin

    registry: Dict[str, JavaClass] = {
        "org.telegram.tgnet.TLRPC$TL_emojiStatus": JavaClass("org.telegram.tgnet.TLRPC$TL_emojiStatus"),
        "org.telegram.tgnet.TLRPC$TL_peerColor": JavaClass("org.telegram.tgnet.TLRPC$TL_peerColor"),
        "org.telegram.tgnet.tl.TL_stars$Tl_starsRating": JavaClass(
            "org.telegram.tgnet.tl.TL_stars$Tl_starsRating"
        ),
        "org.telegram.messenger.MessagesController": JavaClass("org.telegram.messenger.MessagesController"),
        "org.telegram.messenger.UserConfig": JavaClass("org.telegram.messenger.UserConfig"),
        "org.telegram.messenger.NotificationCenter": JavaClass("org.telegram.messenger.NotificationCenter"),
    }

    hook_utils = types.ModuleType("hook_utils")
    hook_utils.find_class = lambda name: registry.get(name)
    hook_utils.get_private_field = lambda obj, name: getattr(obj, name, None)
    hook_utils.set_private_field = lambda obj, name, value: (
        setattr(obj, name, value) or True
    )
    hook_utils.registry = registry
    sys.modules["hook_utils"] = hook_utils

    ui = types.ModuleType("ui")
    bulletin = types.ModuleType("ui.bulletin")
    bulletin.BulletinHelper = BulletinRecorder
    settings_mod = types.ModuleType("ui.settings")
    for name in ("Divider", "EditText", "Header", "Input", "Selector", "Switch", "Text", "Custom"):
        setattr(settings_mod, name, type(name, (_SettingStub,), {}))
    ui.bulletin = bulletin
    ui.settings = settings_mod
    sys.modules["ui"] = ui
    sys.modules["ui.bulletin"] = bulletin
    sys.modules["ui.settings"] = settings_mod

    elyx = types.ModuleType("elyx")
    elyx.strings = strings
    elyx.settings = None
    sys.modules["elyx"] = elyx

    android_utils = types.ModuleType("android_utils")
    android_utils.copy_to_clipboard = lambda text: setattr(android_utils, "clipboard", text)
    android_utils.get_clipboard_text = lambda: getattr(android_utils, "clipboard", "")
    android_utils.clipboard = ""
    sys.modules["android_utils"] = android_utils

    # org.telegram.messenger: UserConfig + MessagesController fakes --------
    messenger = types.ModuleType("org.telegram.messenger")
    org = types.ModuleType("org")
    telegram = types.ModuleType("org.telegram")

    class FakeUserConfig:
        MAX_ACCOUNT_COUNT = 3
        selectedAccount = 0
        _instances: Dict[int, "FakeUserConfig"] = {}
        _users: Dict[int, User] = {}

        def __init__(self, account: int):
            self.account = account
            self.user = FakeUserConfig._users.setdefault(account, User(1000 + account))

        @classmethod
        def getInstance(cls, account: int = 0):
            return cls._instances.setdefault(account, cls(account))

        def getClientUserId(self):
            return self.user.id

        def getCurrentUser(self):
            return self.user

    class FakeMessagesController:
        _instances: Dict[int, "FakeMessagesController"] = {}

        def __init__(self):
            self.users: Dict[int, User] = {}
            self.full_users: Dict[int, UserFull] = {}

        @classmethod
        def getInstance(cls, account: int = 0):
            return cls._instances.setdefault(account, cls())

        def getUser(self, user_id):
            return self.users.get(int(user_id))

        def getUserFull(self, user_id):
            return self.full_users.get(int(user_id))

        def putUser(self, user, *args):
            self.users[user.id] = user

    messenger.UserConfig = FakeUserConfig
    messenger.MessagesController = FakeMessagesController
    messenger.fakes = (FakeUserConfig, FakeMessagesController, User, UserFull)
    org.telegram = telegram
    telegram.messenger = messenger
    for name, module in (("org", org), ("org.telegram", telegram), ("org.telegram.messenger", messenger)):
        sys.modules[name] = module

    if str(PROJECT) not in sys.path:
        sys.path.insert(0, str(PROJECT))
    return strings
