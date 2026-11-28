"""Shared constants, validation and sanitising helpers for Custom Profile.

Everything in this module is pure Python: it must stay importable (and testable)
outside the exteraGram process, so it never touches Java classes.
"""

from __future__ import annotations

import re
import unicodedata
from typing import Any, Dict, Optional

# ---------------------------------------------------------------------------
# Plugin identity
# ---------------------------------------------------------------------------
PLUGIN_VERSION = "2.0.0"

# Telegram-side hard limits. Values are enforced locally so a bad paste cannot
# produce an invalid TL object that the client would later serialise.
MAX_NAME_LENGTH = 64
MAX_BIO_LENGTH = 255
MAX_USERNAME_LENGTH = 32
MIN_USERNAME_LENGTH = 5
USERNAME_PATTERN = re.compile(r"^[A-Za-z][A-Za-z0-9_]*$")

# Telegram ships seven built-in peer colours (palette indices 0..6).
PEER_COLOR_MIN = 0
PEER_COLOR_MAX = 6

# Signed 64-bit ceiling used by the star counters.
MAX_STARS = 9_000_000_000_000_000_000
MIN_STARS = 0
MIN_CROWN_LEVEL = 1
MAX_CROWN_LEVEL = 99

# Default honesty marker appended to locally patched names.
DEFAULT_WATERMARK = "\u00b7preview"

# ---------------------------------------------------------------------------
# Stored settings
# ---------------------------------------------------------------------------
# Values are also used by the reset action.  Empty values mean "keep Telegram's
# real local object value", not "replace it with an empty field".
DEFAULTS: Dict[str, Any] = {
    "enabled": False,
    "scope": 0,
    # 0 = read path only (recommended, never mutates the local cache),
    # 1 = read path + incoming updates (legacy behaviour).
    "patch_mode": 0,
    "watermark_enabled": True,
    "watermark_text": DEFAULT_WATERMARK,
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
    "restore_json": "",
}

# Settings that only make sense as safety switches. They are never exported by
# a preset (a preset must not silently turn the honesty marker off).
PRESETS = {
    # index -> (label key, overrides)
    0: ("preset_keep", {}),
    1: (
        "preset_minimal",
        {
            "verified": False,
            "premium": True,
            "emoji_status_enabled": False,
            "stars_enabled": False,
            "display_name": "",
            "display_username": "",
            "display_bio": "",
            "name_color_enabled": False,
            "profile_color_enabled": False,
        },
    ),
    2: (
        "preset_full",
        {
            "verified": True,
            "premium": True,
            "emoji_status_enabled": False,
            "stars_enabled": True,
            "stars_amount": "1000000",
            "stars_level": "10",
            "name_color_enabled": True,
            "name_color_index": "2",
            "profile_color_enabled": True,
            "profile_color_index": "2",
        },
    ),
    3: (
        "preset_off",
        {
            "verified": False,
            "premium": False,
            "emoji_status_enabled": False,
            "stars_enabled": False,
            "display_name": "",
            "display_username": "",
            "display_bio": "",
            "name_color_enabled": False,
            "profile_color_enabled": False,
        },
    ),
}

PRESET_ITEMS = [PRESETS[index][0] for index in sorted(PRESETS)]

# Keys a settings backup is allowed to contain. Anything else is ignored on
# import, so a pasted blob can never inject an unexpected setting key.
EXPORTABLE_KEYS = tuple(DEFAULTS.keys())


# ---------------------------------------------------------------------------
# Coercion helpers
# ---------------------------------------------------------------------------
def as_bool(value: Any) -> bool:
    """Lenient truthiness for values coming from the host settings store."""
    if isinstance(value, str):
        return value.strip().lower() not in ("", "0", "false", "no", "off", "none", "null")
    return bool(value)


def as_int(
    value: Any,
    default: int = 0,
    minimum: Optional[int] = None,
    maximum: Optional[int] = None,
) -> int:
    """Parse an int from arbitrary input, clamping it into ``[min, max]``.

    A rejected or malformed value falls back to ``default`` (which is clamped
    too), so callers always receive a usable number.
    """
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


def as_text(value: Any) -> str:
    return str(value or "").strip()


# ---------------------------------------------------------------------------
# Sanitising
# ---------------------------------------------------------------------------
_CONTROL_AND_INVISIBLE = {
    # C0/C1 controls, bidi overrides, zero-width and other invisible formatting
    # characters. They are used for name spoofing (e.g. RTL override tricks),
    # so a local preview must never carry them into the UI.
    0x200B, 0x200C, 0x200D, 0x200E, 0x200F,
    0x202A, 0x202B, 0x202C, 0x202D, 0x202E,
    0x2060, 0x2061, 0x2062, 0x2063, 0x2064,
    0x2066, 0x2067, 0x2068, 0x2069,
    0xFEFF, 0x180E,
}


def sanitize_text(value: Any, max_length: int, allow_newlines: bool = False) -> str:
    """Return a UI-safe, length-limited copy of ``value``.

    * control characters and invisible formatting characters are dropped;
    * newlines are collapsed unless ``allow_newlines`` is set;
    * the result is trimmed to ``max_length`` characters.
    """
    text = str(value or "")
    cleaned = []
    for character in text:
        code = ord(character)
        if character in ("\n", "\r"):
            if allow_newlines:
                cleaned.append("\n")
            else:
                cleaned.append(" ")
            continue
        if character == "\t":
            cleaned.append(" ")
            continue
        if code < 0x20 or code == 0x7F or code in _CONTROL_AND_INVISIBLE:
            continue
        if unicodedata.category(character) in ("Cc", "Cf"):
            continue
        cleaned.append(character)
    result = "".join(cleaned)
    if not allow_newlines:
        result = " ".join(result.split())
    else:
        result = "\n".join(line.rstrip() for line in result.split("\n")).strip()
    return result[:max_length]


def validate_username(value: Any) -> Optional[str]:
    """Return a normalised username or ``None`` when it cannot be valid.

    Telegram usernames are 5-32 characters, start with a letter and contain
    only ``A-Z``, ``a-z``, ``0-9`` and ``_``.
    """
    candidate = str(value or "").strip().lstrip("@")
    if not candidate:
        return None
    if not (MIN_USERNAME_LENGTH <= len(candidate) <= MAX_USERNAME_LENGTH):
        return None
    if not USERNAME_PATTERN.match(candidate):
        return None
    return candidate


def split_display_name(value: Any) -> Optional[tuple]:
    """Split a display name into Telegram's first/last name pair.

    Returns ``None`` when the sanitised name is empty, so callers can keep the
    real name instead of writing an empty string.
    """
    cleaned = sanitize_text(value, MAX_NAME_LENGTH)
    if not cleaned:
        return None
    first, separator, last = cleaned.partition(" ")
    return first, (last.strip() if separator else "")


def with_watermark(name: str, watermark_enabled: bool, watermark_text: str, max_length: int) -> str:
    """Append the honesty marker, keeping the result inside Telegram's limits."""
    if not watermark_enabled:
        return name
    suffix = sanitize_text(watermark_text, 24) or DEFAULT_WATERMARK
    if not suffix:
        return name
    if not suffix.startswith(" "):
        suffix = " " + suffix
    limit = max(0, max_length - len(suffix))
    return (name[:limit] + suffix)[:max_length]


def clamp_color_index(value: Any) -> int:
    return as_int(value, 0, minimum=PEER_COLOR_MIN, maximum=PEER_COLOR_MAX)


def clamp_stars(value: Any) -> int:
    return as_int(value, 0, minimum=MIN_STARS, maximum=MAX_STARS)


def clamp_crown_level(value: Any) -> int:
    return as_int(value, MIN_CROWN_LEVEL, minimum=MIN_CROWN_LEVEL, maximum=MAX_CROWN_LEVEL)


def sanitize_emoji_document_id(value: Any) -> int:
    """Custom-emoji document IDs are positive signed 64-bit numbers."""
    parsed = as_int(value, 0, minimum=0, maximum=MAX_STARS)
    return parsed if parsed > 0 else 0


# ---------------------------------------------------------------------------
# Settings backup
# ---------------------------------------------------------------------------
def sanitize_backup(payload: Any) -> Dict[str, Any]:
    """Validate an imported settings blob.

    Only known keys survive, and every value is coerced to the type the plugin
    expects. This keeps a corrupt or hostile clipboard payload from writing an
    unexpected object into the host settings store.
    """
    if not isinstance(payload, dict):
        return {}
    result: Dict[str, Any] = {}
    for key in EXPORTABLE_KEYS:
        if key not in payload:
            continue
        raw = payload[key]
        if key in ("scope", "patch_mode"):
            result[key] = as_int(raw, 0, minimum=0, maximum=1)
        elif key in ("enabled", "watermark_enabled", "verified", "premium",
                     "emoji_status_enabled", "stars_enabled",
                     "name_color_enabled", "profile_color_enabled"):
            result[key] = as_bool(raw)
        elif key in ("stars_amount",):
            result[key] = str(clamp_stars(raw))
        elif key in ("stars_level",):
            result[key] = str(clamp_crown_level(raw))
        elif key in ("name_color_index", "profile_color_index"):
            result[key] = str(clamp_color_index(raw))
        elif key == "emoji_status_id":
            identifier = sanitize_emoji_document_id(raw)
            result[key] = str(identifier) if identifier else ""
        elif key == "display_bio":
            result[key] = sanitize_text(raw, MAX_BIO_LENGTH, allow_newlines=True)
        elif key == "display_name":
            result[key] = sanitize_text(raw, MAX_NAME_LENGTH)
        elif key == "display_username":
            result[key] = validate_username(raw) or ""
        elif key == "watermark_text":
            result[key] = sanitize_text(raw, 24)
        elif key == "restore_json":
            # Never re-import a nested blob.
            continue
        else:
            result[key] = str(raw)[:512]
    return result
