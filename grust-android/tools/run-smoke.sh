#!/usr/bin/env bash
# The entry point the emulator step calls: finds the debug APK, runs the walkthrough, and leaves a
# log the next workflow step can turn into annotations.
#
# This is a file rather than a few lines of inline `script:` in the workflow because
# reactivecircus/android-emulator-runner runs the script with /bin/sh - dash on the runner - where
# `set -o pipefail` is an error, and because a multiline shell script inside YAML is a place where
# quoting mistakes are invisible until a runner has booted an emulator to find out.

set -uo pipefail

workspace="${GITHUB_WORKSPACE:?GITHUB_WORKSPACE is not set}"
out="$workspace/screenshots-capture"
log="$workspace/smoke.log"

mkdir -p "$out"
apk=$(find "$workspace/grust-android/app/build/outputs/apk/debug" -name '*.apk' 2>/dev/null | head -1)

{
    echo "workspace=$workspace"
    echo "pwd=$(pwd)"
    echo "apk=${apk:-missing}"
    echo "adb=$(command -v adb || echo none)"
} >"$log"

if [ -z "$apk" ]; then
    echo "::error title=Smoke::no debug APK under app/build/outputs/apk/debug"
    exit 1
fi

bash "$workspace/grust-android/tools/ci-smoke.sh" "$apk" "$out" >>"$log" 2>&1
status=$?
echo "smoke exit=$status" >>"$log"
ls -l "$out" >>"$log" 2>&1
exit "$status"
