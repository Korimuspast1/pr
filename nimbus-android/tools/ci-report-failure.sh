#!/usr/bin/env bash
# Leaves one comment per job on the pull request saying why the build failed, and edits that
# comment on the next failure instead of adding another.
#
# The run logs cannot be downloaded from where this project is maintained, so the parts of them
# that matter - the compiler errors, the tests that failed and what they said, Gradle's own
# explanation - are put where the REST API can serve them. A branch that fails ten times on the
# way to green leaves one comment behind, not ten.
#
# If posting fails, this says so as an annotation and still exits 0: the job is already red
# because of the build, and a red reporting step would hide which of the two it was.
#
# `--clear` is the other half: a job that passes removes the comment its own earlier failure
# left, so a green pull request does not keep reporting a failure the newest commit fixed. It
# needs neither the checkout's build nor ../gradle.log, only the marker.
#
# Environment: GH_TOKEN, PR_NUMBER, MARKER, GITHUB_REPOSITORY, GITHUB_JOB, GITHUB_SHA.
# Run from the nimbus-android directory, after tools/ci-gradle.sh has written ../gradle.log.

set -uo pipefail

note() { echo "::notice title=Report::$*"; }
fail() { echo "::error title=Report::$*"; }
# Annotation messages are one line, and a long one is a useless one.
flat() { tr '\n\r' '  ' | tr -s ' ' | cut -c1-900; }

api="repos/${GITHUB_REPOSITORY:?}/issues/${PR_NUMBER:?}/comments"

# The id of the comment this job left last, if any. The newest of them, which is the one the
# failure path below edits, so the two halves always mean the same comment.
existing_comment() {
    local listing
    if listing=$(gh api "$api" --paginate \
        --jq '.[] | select(.body != null and (.body | contains(env.MARKER))) | .id' 2>&1); then
        printf '%s' "$listing" | tail -1
    else
        fail "listing the comments of #$PR_NUMBER failed: $(printf '%s' "$listing" | flat)"
    fi
}

if [ "${1:-}" = "--clear" ]; then
    : "${MARKER:?MARKER is required}"
    found=$(existing_comment)
    if [ -n "$found" ]; then
        out=$(gh api --method DELETE "repos/$GITHUB_REPOSITORY/issues/comments/$found" 2>&1) ||
            fail "deleting comment $found failed: $(printf '%s' "$out" | flat)"
        note "the build is green: cleared the failure comment $found"
    fi
    exit 0
fi

log="../gradle.log"
body=$(mktemp)
payload=$(mktemp)
trap 'rm -f "$body" "$payload"' EXIT

# All of them, not a slice: this list is what the next commit is made of.
compiler=$(grep -E '^e: ' "$log" 2>/dev/null | head -300)
warnings=$(grep -cE '^w: ' "$log" 2>/dev/null || true)
gradle_said=$(grep -B2 -A20 -E '^\* What went wrong' "$log" 2>/dev/null | head -70)

{
    echo "${MARKER:?MARKER is required}"
    echo
    echo "### Сборка упала: ${GITHUB_JOB:-build} (\`${GITHUB_SHA:0:7}\`)"
    echo
    echo '**Ошибки компилятора**'
    echo '```'
    if [ -n "$compiler" ]; then
        # The runner's path is noise; what is left is the file, the line and the message.
        printf '%s\n' "$compiler" | sed 's|file://.*/nimbus-android/app/src/||'
        echo
        echo "всего $(printf '%s\n' "$compiler" | grep -c '^e: ') ошибок, предупреждений: ${warnings:-0}"
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

existing=$(existing_comment)

# --input rather than -F body=@file: the report is markdown full of quotes and backticks, and a
# JSON document has no trouble with any of it.
python3 -c 'import json, sys; json.dump({"body": open(sys.argv[1], encoding="utf-8").read()}, open(sys.argv[2], "w", encoding="utf-8"))' \
    "$body" "$payload" || fail "could not build the request body"

if [ -n "$existing" ]; then
    # Editing goes to the comment's own endpoint, which has no issue number in it:
    # PATCH .../issues/<n>/comments/<id> is a 404.
    out=$(gh api --method PATCH "repos/$GITHUB_REPOSITORY/issues/comments/$existing" \
        --input "$payload" 2>&1) || {
        fail "editing comment $existing failed: $(printf '%s' "$out" | flat)"
        # So the errors are not lost with it: one annotation, as much of them as fits.
        fail "$(flat <"$body")"
    }
else
    out=$(gh api --method POST "$api" --input "$payload" 2>&1) || {
        fail "adding a comment to #$PR_NUMBER failed: $(printf '%s' "$out" | flat)"
        fail "$(flat <"$body")"
    }
fi

note "reported $(wc -l <"$body" | tr -d ' ') lines as comment ${existing:-new}"
exit 0
