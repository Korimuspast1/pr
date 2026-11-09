#!/usr/bin/env python3
"""Custom Profile preset designer — build and preview presets without a device.

The web UI in exteraGram is fine for tapping toggles, but a preset is just a
JSON blob, and JSON is much easier to iterate on at a desk. This tool uses the
plugin's *own* validation code (``core.py``), so what it prints is exactly what
the plugin will accept after sanitising.

Examples
--------
    # Show what the "full preview" preset will look like on screen
    python3 custom-profile/tools/preset.py --preset full --name "Alice Example"

    # Turn a hand-written JSON preset into a safe, clipboard-ready blob
    python3 custom-profile/tools/preset.py --import my_preset.json

    # Start from "everything off" and add just two things
    python3 custom-profile/tools/preset.py --preset off --set premium=true --color 3

    # Machine-readable output for scripts
    python3 custom-profile/tools/preset.py --preset minimal --json

Exit codes: 0 = ok, 1 = the input could not be used, 2 = bad arguments.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT))

import core  # noqa: E402

PRESET_ALIASES = {
    "keep": 0,
    "minimal": 1,
    "full": 2,
    "off": 3,
}


def load_preset(spec: str) -> Dict[str, Any]:
    """Resolve ``--preset`` into a settings dict."""
    if spec in PRESET_ALIASES:
        _, overrides = core.PRESETS[PRESET_ALIASES[spec]]
        return dict(overrides)
    candidate = Path(spec)
    if candidate.is_file():
        text = candidate.read_text(encoding="utf-8")
    elif spec.lstrip().startswith("{"):
        text = spec
    else:
        raise SystemExit(
            "unknown preset {!r}; use one of {} or a JSON file".format(
                spec, ", ".join(sorted(PRESET_ALIASES))
            )
        )
    return parse_json(text)


def parse_json(text: str) -> Dict[str, Any]:
    try:
        parsed = json.loads(text)
    except ValueError as error:
        raise SystemExit("invalid JSON: {}".format(error))
    if not isinstance(parsed, dict):
        raise SystemExit("a preset must be a JSON object")
    return core.sanitize_backup(parsed)


def to_bool(value: str) -> Any:
    lowered = value.strip().lower()
    if lowered in ("1", "true", "yes", "on"):
        return True
    if lowered in ("0", "false", "no", "off"):
        return False
    return value


def apply_overrides(settings: Dict[str, Any], overrides: List[str]) -> Dict[str, Any]:
    for item in overrides:
        if "=" not in item:
            raise SystemExit("--set expects key=value, got {!r}".format(item))
        key, _, raw = item.partition("=")
        key = key.strip()
        if key not in core.DEFAULTS or key in ("restore_json",):
            raise SystemExit(
                "unknown setting {!r}; known keys: {}".format(
                    key, ", ".join(sorted(k for k in core.DEFAULTS if k != "restore_json"))
                )
            )
        settings[key] = core.sanitize_backup({key: to_bool(raw)}).get(key, settings.get(key))
    return settings


def render(settings: Dict[str, Any]) -> List[str]:
    """Human-readable summary of the local preview."""
    lines: List[str] = []
    name = core.sanitize_text(settings.get("display_name", ""), core.MAX_NAME_LENGTH)
    watermark = core.as_bool(settings.get("watermark_enabled", True))
    marker = core.sanitize_text(settings.get("watermark_text", core.DEFAULT_WATERMARK), 24)
    if name:
        first, last = core.split_display_name(name) or ("", "")
        first = core.with_watermark(first, watermark, marker, core.MAX_NAME_LENGTH)
        shown = " ".join(part for part in (first, last) if part)
        lines.append("name      : " + shown)
    else:
        lines.append("name      : (real name kept)")
    if not watermark:
        lines.append("            ! preview marker is OFF: a screenshot will look real")

    username = core.validate_username(settings.get("display_username", ""))
    if username:
        lines.append("username  : @" + username)
    elif settings.get("display_username"):
        lines.append("username  : (rejected by Telegram rules, real one kept)")
    else:
        lines.append("username  : (real one kept)")

    bio = core.sanitize_text(settings.get("display_bio", ""), core.MAX_BIO_LENGTH, allow_newlines=True)
    lines.append("bio       : " + (bio.replace("\n", " / ") if bio else "(real one kept)"))

    badges = []
    if core.as_bool(settings.get("verified", False)):
        badges.append("verified")
    if core.as_bool(settings.get("premium", False)):
        badges.append("premium")
    if core.as_bool(settings.get("emoji_status_enabled", False)):
        document_id = core.sanitize_emoji_document_id(settings.get("emoji_status_id", ""))
        badges.append("emoji-status" + (" ({})".format(document_id) if document_id else " (no id)"))
    lines.append("badges    : " + (", ".join(badges) if badges else "none"))

    if core.as_bool(settings.get("stars_enabled", False)):
        lines.append(
            "stars     : {} stars, crown level {}".format(
                core.clamp_stars(settings.get("stars_amount", 0)),
                core.clamp_crown_level(settings.get("stars_level", 1)),
            )
        )
    else:
        lines.append("stars     : off")

    colors = []
    if core.as_bool(settings.get("name_color_enabled", False)):
        colors.append("name={}".format(core.clamp_color_index(settings.get("name_color_index", 0))))
    if core.as_bool(settings.get("profile_color_enabled", False)):
        colors.append("profile={}".format(core.clamp_color_index(settings.get("profile_color_index", 0))))
    lines.append("colours   : " + (", ".join(colors) if colors else "default"))

    mode = core.as_int(settings.get("patch_mode", 0))
    lines.append("patch mode: " + ("read-only (recommended)" if mode == 0 else "updates + cache"))
    return lines


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Build, sanitise and preview Custom Profile presets.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--preset", default="keep", help="keep|minimal|full|off, a JSON file or inline JSON")
    source.add_argument("--import", dest="import_file", help="sanitise an existing JSON preset file")
    parser.add_argument("--set", action="append", default=[], metavar="KEY=VALUE", help="override one setting")
    parser.add_argument("--name", help="display name")
    parser.add_argument("--username", help="local @username")
    parser.add_argument("--bio", help="local bio")
    parser.add_argument("--stars", help="profile star amount")
    parser.add_argument("--level", help="crown level (1-99)")
    parser.add_argument("--color", help="palette index 0-6 for both name and profile colour")
    parser.add_argument("--no-watermark", action="store_true", help="disable the preview marker")
    parser.add_argument("--json", action="store_true", help="print only the sanitised JSON preset")
    args = parser.parse_args(argv)

    if args.import_file:
        path = Path(args.import_file)
        if not path.is_file():
            print("ERROR: no such file: {}".format(path), file=sys.stderr)
            return 1
        settings = parse_json(path.read_text(encoding="utf-8"))
    else:
        settings = load_preset(args.preset)

    settings = apply_overrides(settings, args.set)

    if args.name is not None:
        settings["display_name"] = core.sanitize_text(args.name, core.MAX_NAME_LENGTH)
    if args.username is not None:
        cleaned = core.validate_username(args.username)
        if cleaned is None:
            print(
                "ERROR: {!r} is not a valid Telegram username "
                "(5-32 chars, leading letter, A-Za-z0-9_)".format(args.username),
                file=sys.stderr,
            )
            return 1
        settings["display_username"] = cleaned
    if args.bio is not None:
        settings["display_bio"] = core.sanitize_text(args.bio, core.MAX_BIO_LENGTH, allow_newlines=True)
    if args.stars is not None:
        settings["stars_enabled"] = True
        settings["stars_amount"] = str(core.clamp_stars(args.stars))
    if args.level is not None:
        settings["stars_enabled"] = True
        settings["stars_level"] = str(core.clamp_crown_level(args.level))
    if args.color is not None:
        index = core.clamp_color_index(args.color)
        settings["name_color_enabled"] = True
        settings["name_color_index"] = str(index)
        settings["profile_color_enabled"] = True
        settings["profile_color_index"] = str(index)
    if args.no_watermark:
        settings["watermark_enabled"] = False

    payload = {key: settings.get(key, core.DEFAULTS[key]) for key in core.DEFAULTS if key != "restore_json"}
    payload["_plugin"] = "custom_profile"
    payload["_version"] = core.PLUGIN_VERSION
    blob = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))

    if args.json:
        print(blob)
        return 0

    print("Custom Profile preset preview")
    print("=" * 40)
    for line in render(payload):
        print(line)
    print("=" * 40)
    print("JSON (paste it into Presets → Paste a preset):")
    print(blob)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
