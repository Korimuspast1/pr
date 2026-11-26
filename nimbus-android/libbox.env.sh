#!/usr/bin/env bash
# Fetches the sing-box core for Android (libbox.aar) into app/libs/, where app/build.gradle.kts
# picks it up. Source it (`. ./libbox.env.sh`) or run it; it exits non-zero if the AAR isn't there
# afterwards, because Gradle would otherwise fail on a missing file with a confusing message.
#
# The AAR is a build artifact of SagerNet/sing-box for Android, published per release by
# singbox-android/libbox. version.properties pins the tag, exactly as the desktop's
# tools/fetch_cores.py pins its cores. Nothing here needs Go or the NDK.

set -euo pipefail

here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
cd "$here"

version=$(sed -n 's/^SINGBOX_VERSION=//p' version.properties | tr -d '\r')
url=$(sed -n 's/^LIBBOX_AAR_URL=//p' version.properties | tr -d '\r')
dest="app/libs/libbox.aar"

if [ -z "$version" ]; then
    echo "libbox: no SINGBOX_VERSION in version.properties" >&2
    return 1 2>/dev/null || exit 1
fi
if [ -z "$url" ]; then
    url="https://github.com/singbox-android/libbox/releases/download/${version}/libbox.aar"
fi

mkdir -p app/libs

if [ -s "$dest" ]; then
    echo "libbox $version: $dest already present ($(du -h "$dest" | cut -f1))"
    return 0 2>/dev/null || exit 0
fi

echo "libbox $version: downloading $url"
tmp=$(mktemp)
# GitHub redirects release assets to a CDN; -L follows, --retry rides out the occasional 5xx.
curl --fail --location --retry 3 --retry-delay 5 --connect-timeout 30 --output "$tmp" "$url"

# An HTML error page instead of an AAR is the failure that used to slip through: an AAR is a zip.
if ! unzip -l "$tmp" >/dev/null 2>&1; then
    echo "libbox: $url did not return an AAR" >&2
    head -c 400 "$tmp" >&2 || true
    rm -f "$tmp"
    return 1 2>/dev/null || exit 1
fi

mv "$tmp" "$dest"
echo "libbox $version: $(du -h "$dest" | cut -f1) -> $dest"
