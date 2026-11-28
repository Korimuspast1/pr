#!/usr/bin/env bash
# Installs the pieces of the Android SDK this build needs, on a runner that may or may not already
# have them. Called by .github/workflows/nimbus-android.yml from the nimbus-android directory.
#
# Everything it learns is printed as a GitHub check annotation (::notice / ::error) rather than
# only to stdout: the Actions logs of this repository cannot be downloaded from where it is
# maintained, while annotations are served by the API.
#
# Usage: COMPILE_SDK=37 bash tools/ci-android-sdk.sh

set -uo pipefail

compile_sdk="${COMPILE_SDK:-37}"
# cmdline-tools 22.0 - the build android-actions/setup-android@v4 fetches, taken from
# dl.google.com directly so that no third-party action stands between the build and the SDK.
cmdline_tools_build="${CMDLINE_TOOLS_BUILD:-15859902}"

note() { echo "::notice title=Android SDK::$*"; }
die() {
    echo "::error title=Android SDK::$*"
    exit 1
}
# An annotation message has to be a single line, and a long one is a useless one.
flat() { tr '\n\r' '  ' | tr -s ' ' | cut -c1-900; }

root="${ANDROID_HOME:-$HOME/.android/sdk}"
note "ANDROID_HOME=${ANDROID_HOME:-unset} ANDROID_SDK_ROOT=${ANDROID_SDK_ROOT:-unset}"
if ! mkdir -p "$root" 2>/dev/null || [ ! -w "$root" ]; then
    wanted="$root"
    root="$HOME/.android/sdk"
    mkdir -p "$root" 2>/dev/null || die "no writable place for an SDK ($root)"
    note "$wanted is not writable, falling back to $root"
fi

sdkmanager="$root/cmdline-tools/latest/bin/sdkmanager"
if [ ! -x "$sdkmanager" ]; then
    url="https://dl.google.com/android/repository/commandlinetools-linux-${cmdline_tools_build}_latest.zip"
    archive=$(mktemp)
    out=$(curl --fail --location --retry 3 --retry-delay 5 --connect-timeout 30 \
        --output "$archive" "$url" 2>&1) ||
        die "download of $url failed: $(printf '%s' "$out" | flat)"
    note "cmdline-tools downloaded: $(du -h "$archive" | cut -f1)"
    rm -rf /tmp/nimbus-cmdline-tools
    mkdir -p /tmp/nimbus-cmdline-tools
    out=$(unzip -q "$archive" -d /tmp/nimbus-cmdline-tools 2>&1) ||
        die "the download is not a zip: $(printf '%s' "$out" | flat)"
    note "cmdline-tools unpacked to: $(ls /tmp/nimbus-cmdline-tools | flat)"
    mkdir -p "$root/cmdline-tools"
    mv /tmp/nimbus-cmdline-tools/cmdline-tools "$root/cmdline-tools/latest" 2>/dev/null ||
        die "unexpected cmdline-tools layout: $(ls -R /tmp/nimbus-cmdline-tools | head -20 | flat)"
    rm -f "$archive"
fi
[ -x "$sdkmanager" ] || die "no sdkmanager at $sdkmanager"
note "sdkmanager: $sdkmanager"

# sdkmanager is a Java program: a missing JDK reports as an SDK failure and is not one.
out=$(java -version 2>&1 | head -1) || die "no java on PATH"
note "java: $out"

# The licences have to be accepted before anything else, because they are what lets AGP download
# a component nobody installed here yet.
out=$(yes | "$sdkmanager" --licenses 2>&1) ||
    note "licence acceptance ended non-zero: $(printf '%s' "$out" | tail -c 400 | flat)"

# A package that is not in the repository is reported, not fatal: AGP fetches what it is missing
# on its own once the licences are accepted, and says so plainly if it cannot.
out=$("$sdkmanager" "platforms;android-${compile_sdk}" "build-tools;36.0.0" "platform-tools" 2>&1) ||
    note "install ended non-zero: $(printf '%s' "$out" | tail -c 600 | flat)"

note "installed: $("$sdkmanager" --list_installed 2>&1 | awk 'NR>2 {printf "%s ", $1}' | flat)"

{
    echo "ANDROID_HOME=$root"
    echo "ANDROID_SDK_ROOT=$root"
} >>"$GITHUB_ENV"
# Written next to settings.gradle.kts, where Gradle looks first for the SDK location.
echo "sdk.dir=$root" >local.properties
note "ANDROID_HOME=$root exported, sdk.dir written to local.properties"
