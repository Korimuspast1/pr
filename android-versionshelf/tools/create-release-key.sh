#!/usr/bin/env bash
# Creates a new, user-owned Android signing key outside version control.
# Run from a workstation where Java's keytool is installed.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DESTINATION="${1:-$ROOT/keystore/versionshelf-upload.jks}"
ALIAS="${2:-versionshelf-upload}"

command -v keytool >/dev/null || { echo "keytool (JDK 17+) is required." >&2; exit 1; }
mkdir -p "$(dirname "$DESTINATION")"
if [[ -e "$DESTINATION" ]]; then
  echo "Refusing to overwrite an existing signing key: $DESTINATION" >&2
  exit 1
fi

read -r -s -p "New keystore password: " STORE_PASSWORD; echo
read -r -s -p "Repeat keystore password: " STORE_PASSWORD_REPEAT; echo
[[ "$STORE_PASSWORD" == "$STORE_PASSWORD_REPEAT" ]] || { echo "Passwords differ." >&2; exit 1; }

keytool -genkeypair \
  -keystore "$DESTINATION" \
  -storetype PKCS12 \
  -alias "$ALIAS" \
  -keyalg RSA \
  -keysize 4096 \
  -validity 10000 \
  -storepass "$STORE_PASSWORD" \
  -keypass "$STORE_PASSWORD" \
  -dname "CN=VersionShelf, OU=Private Distribution, O=Owner, C=PL"

cat <<MESSAGE

Key created at: $DESTINATION
Keep it private, backed up offline, and out of Git. The certificate does not make APKs trusted by
Google automatically; only transparent provenance, safe behaviour and policy compliance can do that.

Build a signed release by passing these values from a secure terminal or CI secret store:
  ./gradlew :app:assembleRelease \\
    -PVERSIONSHELF_STORE_FILE="$DESTINATION" \\
    -PVERSIONSHELF_STORE_PASSWORD='…' \\
    -PVERSIONSHELF_KEY_ALIAS="$ALIAS" \\
    -PVERSIONSHELF_KEY_PASSWORD='…'
MESSAGE
