#!/usr/bin/env bash
# Leaves one comment per job on the pull request saying why the build failed, and updates that
# comment on the next failure instead of adding another.
#
# The run logs cannot be downloaded from where this project is maintained, so the parts of them
# that matter - the compiler errors, the tests that failed and what they said, Gradle's own
# explanation - are put where the REST API can serve them. A branch that fails ten times on the
# way to green leaves one comment behind, not ten.
#
# Environment: GH_TOKEN, PR_NUMBER, MARKER, GITHUB_REPOSITORY, GITHUB_JOB, GITHUB_SHA.
# Run from the nimbus-android directory, after tools/ci-gradle.sh has written ../gradle.log.

set -uo pipefail

log="../gradle.log"
body=$(mktemp)
payload=$(mktemp)
trap 'rm -f "$body" "$payload"' EXIT

compiler=$(grep -E '^e: |error: ' "$log" 2>/dev/null | head -60)
gradle_said=$(grep -B2 -A20 -E '^\* What went wrong' "$log" 2>/dev/null | head -70)

{
    echo "${MARKER:?MARKER is required}"
    echo
    echo "### Сборка упала: ${GITHUB_JOB:-build} (\`${GITHUB_SHA:0:7}\`)"
    echo
    echo '**Ошибки компилятора**'
    echo '```'
    if [ -n "$compiler" ]; then
        printf '%s\n' "$compiler"
    else
        echo '(строк "e:" нет - до компиляции Kotlin не дошло)'
    fi
    echo '```'
    if [ -s ../test-report.md ]; then
        echo
        cat ../test-report.md
    fi
    echo
    echo '<details><summary>Что сказал Gradle</summary>'
    echo
    echo '```'
    if [ -n "$gradle_said" ]; then
        printf '%s\n' "$gradle_said"
    else
        tail -60 "$log" 2>/dev/null
    fi
    echo '```'
    echo
    echo '</details>'
} >"$body"

api="repos/${GITHUB_REPOSITORY:?}/issues/${PR_NUMBER:?}/comments"
existing=$(gh api "$api" --paginate \
    --jq '.[] | select(.body != null and (.body | contains(env.MARKER))) | .id' 2>/dev/null | tail -1)

# --input rather than -F body=@file: the report is markdown full of quotes and backticks, and a
# JSON document has no trouble with any of it.
python3 -c 'import json, sys; json.dump({"body": open(sys.argv[1], encoding="utf-8").read()}, open(sys.argv[2], "w", encoding="utf-8"))' \
    "$body" "$payload"

if [ -n "$existing" ]; then
    gh api --method PATCH "$api/$existing" --input "$payload" >/dev/null
else
    gh api --method POST "$api" --input "$payload" >/dev/null
fi
