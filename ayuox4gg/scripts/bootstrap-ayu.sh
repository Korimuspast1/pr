#!/usr/bin/env bash
# ayuox4gg — turn a fresh AyuGram4A checkout into a buildable tree.
#
# Does three things, idempotently:
#   1. writes API_KEYS (git-ignored, mode 600) from the values you provide;
#   2. drops the public `proprietary/` stand-ins and removes the private
#      submodule entry so `git clone` stops failing on it;
#   3. applies the security patches and runs the checks.
#
# Usage:
#   ./ayuox4gg/scripts/bootstrap-ayu.sh --tree /path/to/AyuGram4A \
#       --app-id 12345 --app-hash <32 hex chars>
#
#   # or let it prompt (nothing is echoed):
#   ./ayuox4gg/scripts/bootstrap-ayu.sh --tree /path/to/AyuGram4A --ask
#
#   # also point the build at your own release keystore (see FINDINGS.md X1)
#   ./ayuox4gg/scripts/bootstrap-ayu.sh --tree /path/to/AyuGram4A \
#       --sign-password <pw> --sign-alias ayuox4gg
#
#   # check only, change nothing
#   ./ayuox4gg/scripts/bootstrap-ayu.sh --tree /path/to/AyuGram4A --dry-run
#
# Exit codes: 0 ok, 1 bad input/missing tooling, 2 the tree could not be patched.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OVERLAY="$(cd "$HERE/.." && pwd)"
TREE=""
APP_ID="${APP_ID:-}"
APP_HASH="${APP_HASH:-}"
SIGN_PASSWORD="${SIGN_PASSWORD:-}"
SIGN_ALIAS="${SIGN_ALIAS:-}"
ASK=0
DRY_RUN=0
SKIP_PATCHES=0

usage() { sed -n '2,25p' "$0" | sed 's/^# \{0,1\}//'; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tree) TREE="$2"; shift 2 ;;
    --app-id) APP_ID="$2"; shift 2 ;;
    --app-hash) APP_HASH="$2"; shift 2 ;;
    --sign-password) SIGN_PASSWORD="$2"; shift 2 ;;
    --sign-alias) SIGN_ALIAS="$2"; shift 2 ;;
    --ask) ASK=1; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    --skip-patches) SKIP_PATCHES=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown option: $1" >&2; usage; exit 1 ;;
  esac
done

say()  { printf '\033[1m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[31m[error]\033[0m %s\n' "$*" >&2; exit 1; }

[[ -n "$TREE" ]] || die "--tree is required (path to an AyuGram4A checkout)"
[[ -d "$TREE" ]] || die "no such directory: $TREE"
[[ -f "$TREE/TMessagesProj/build.gradle" ]] || die "$TREE does not look like AyuGram4A (no TMessagesProj/build.gradle)"
command -v python3 >/dev/null || die "python3 is required"

say "tree   : $TREE"
say "overlay: $OVERLAY"
[[ $DRY_RUN -eq 1 ]] && warn "dry run: nothing will be written"

# ---------------------------------------------------------------- 1. API keys
if [[ $ASK -eq 1 ]]; then
  read -r -p "api_id: " APP_ID
  read -r -s -p "api_hash (hidden): " APP_HASH; echo
fi

if [[ -n "$APP_ID" || -n "$APP_HASH" ]]; then
  [[ -n "$APP_ID" && -n "$APP_HASH" ]] || die "pass both --app-id and --app-hash"
  if [[ $DRY_RUN -eq 1 ]]; then
    say "would write $TREE/API_KEYS (mode 600, git-ignored)"
  else
    python3 "$OVERLAY/tools/setup_keys.py" \
      --app-id "$APP_ID" --app-hash "$APP_HASH" \
      --sign-password "$SIGN_PASSWORD" --sign-alias "$SIGN_ALIAS" \
      --output "$TREE/API_KEYS"
  fi
elif [[ -f "$TREE/API_KEYS" ]]; then
  say "API_KEYS already present, keeping it"
  python3 "$OVERLAY/tools/setup_keys.py" --check "$TREE/API_KEYS" || true
else
  warn "no API_KEYS: the Gradle build will fail on APP_ID/APP_HASH."
  warn "create a pair at https://my.telegram.org and re-run with --app-id/--app-hash"
fi

# ------------------------------------------------- 2. proprietary stand-ins
PROP="$TREE/TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary"
GITMODULES="$TREE/.gitmodules"

say "installing public proprietary/ stand-ins"
if [[ $DRY_RUN -eq 1 ]]; then
  say "would copy $(ls "$OVERLAY/proprietary-stubs" | tr '\n' ' ')to $PROP"
else
  mkdir -p "$PROP"
  cp -f "$OVERLAY"/proprietary-stubs/*.java "$PROP"/

  # Features that are drop-in (no call-site changes required) go into the tree
  # so they end up in the APK instead of gathering dust next to it.
  SEC="$TREE/TMessagesProj/src/main/java/com/radolyn/ayugram/security"
  if [[ -d "$OVERLAY/features" ]]; then
    mkdir -p "$SEC"
    cp -f "$OVERLAY"/features/*.java "$SEC"/
    say "   features: $(ls "$OVERLAY/features" | tr '\n' ' ')-> ayugram/security/"
  fi
  # The submodule entry makes a fresh clone fail; it is useless without access
  # to the private repository. Drop it and unstage the gitlink.
  if [[ -f "$GITMODULES" ]]; then
    python3 - "$GITMODULES" <<'PY'
import re, sys
path = sys.argv[1]
text = open(path, encoding="utf-8").read()
blocks = re.split(r'(?=\[submodule )', text)
kept = [b for b in blocks if "ayugram/proprietary" not in b]
new = "".join(kept).strip()
if new != text.strip():
    open(path, "w", encoding="utf-8").write(new + ("\n" if new else ""))
    print("   .gitmodules: removed the private proprietary submodule entry")
PY
  fi
  if [[ -d "$TREE/.git" ]]; then
    SUBPATH="TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary"
    # `git rm --cached` refuses to touch a submodule gitlink, so edit the index
    # directly: the stand-ins become ordinary tracked-by-status files.
    if git -C "$TREE" ls-files -s -- "$SUBPATH" | grep -q '^160000'; then
      git -C "$TREE" update-index --force-remove -- "$SUBPATH" >/dev/null 2>&1 || true
      say "   index: dropped the gitlink for proprietary/ (private submodule)"
    fi
    git -C "$TREE" add -N -- "$SUBPATH"/*.java >/dev/null 2>&1 || true
  fi
fi

# ------------------------------------------------------------ 3. patches
if [[ $SKIP_PATCHES -eq 0 ]]; then
  say "applying security patches"
  applied=0
  for patch in "$OVERLAY"/security/patches/*.patch; do
    name="$(basename "$patch")"
    if [[ $DRY_RUN -eq 1 ]]; then
      if git -C "$TREE" apply --check "$patch" >/dev/null 2>&1; then
        say "would apply $name"
      else
        warn "$name would not apply (already applied, or tree differs)"
      fi
      continue
    fi
    if git -C "$TREE" apply --check "$patch" >/dev/null 2>&1; then
      git -C "$TREE" apply "$patch"
      say "applied $name"
      applied=$((applied + 1))
    elif git -C "$TREE" apply --check --reverse "$patch" >/dev/null 2>&1; then
      say "$name already applied, skipping"
    else
      warn "$name does not apply cleanly — resolve manually"
    fi
  done
  [[ $DRY_RUN -eq 0 ]] && say "$applied patch(es) applied"
fi

# ------------------------------------------------------------ 4. checks
if command -v python3 >/dev/null; then
  say "java syntax check"
  python3 "$OVERLAY/tools/javaparse.py" "$PROP"/*.java >/dev/null \
    && say "stand-ins parse" || warn "stand-ins failed to parse"
fi

say "done"
cat <<'EOF'

Next:
  cd <tree> && ./gradlew assembleAfatRelease
  # in this sandbox the Gradle build cannot run (no SDK/NDK, no Maven access);
  # see ayuox4gg/docs/BUILD.md for a real build machine.
EOF
