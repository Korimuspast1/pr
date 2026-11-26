#!/usr/bin/env bash
# Runs one Gradle task in CI, working out what the task is actually called first.
#
# AGP 9 does not name the unit test task the way AGP 8 did - :app:testReleaseUnitTest does not
# exist any more - and the run logs cannot be downloaded from where this project is maintained.
# So the app module's task list is asked for, the part of it that matters is reported as a check
# annotation (which the API does serve), and the first candidate that exists is the one that runs.
#
# Usage: tools/ci-gradle.sh <candidate>|<candidate>... [extra gradle arguments]
#   tools/ci-gradle.sh 'testReleaseUnitTest|testRelease|test'
#   tools/ci-gradle.sh 'assembleRelease|assemble'

set -uo pipefail

wanted="${1:?usage: ci-gradle.sh '<task>|<fallback task>' [gradle arguments]}"
shift || true

note() { echo "::notice title=Gradle::$*"; }
die() {
    echo "::error title=Gradle::$*"
    exit 1
}
# Annotation messages are one line, and a long one is a useless one.
flat() { tr '\n\r' '  ' | tr -s ' ' | cut -c1-900; }
names() { tr ' ' '\n' | grep -v '^$'; }

# -q keeps this to task names and descriptions; it is a listing, not a build.
./gradlew --no-daemon -q :app:tasks --all >../tasks.log 2>&1
available=$(grep -oE '^[a-z][A-Za-z0-9]*' ../tasks.log | sort -u | tr '\n' ' ')

task=''
IFS='|' read -r -a candidates <<<"$wanted"
for candidate in "${candidates[@]}"; do
    if printf '%s' "$available" | names | grep -qx "$candidate"; then
        task=":app:$candidate"
        break
    fi
done
[ -n "$task" ] || die "no task called $(printf '%s' "$wanted" | tr '|' '/') - the app module has: $(printf '%s' "$available" | names | grep -E '^(test|assemble|bundle|build|check|lint)' | tr '\n' ' ' | flat)"

note "task $task, out of: $(printf '%s' "$available" | names | grep -E "^(test|assemble)" | tr '\n' ' ' | flat)"

./gradlew --no-daemon --stacktrace "$task" "$@" 2>&1 | tee ../gradle.log
exit "${PIPESTATUS[0]}"
