#!/usr/bin/env python3
"""Turns the unit test results into something readable without the run logs.

The Actions logs of this repository cannot be downloaded from where it is maintained, so a failing
test would otherwise be a red tick and nothing else. This reads the JUnit XML Gradle leaves behind
and prints, to stdout:

  * one check annotation per failing test, which the GitHub API serves, and
  * one summary annotation with the counts;

and it writes a markdown fragment of the failures to ../test-report.md, which the workflow folds
into the comment it leaves on the pull request.

Run from the nimbus-android directory after Gradle, whether or not it succeeded.
"""

from __future__ import annotations

import glob
import os
import xml.etree.ElementTree as ET

REPORT = os.path.join("..", "test-report.md")
# Annotation messages are single-line and short; the full text stays in the XML report.
LIMIT = 700


def flat(text: str | None) -> str:
    return " ".join((text or "").split())[:LIMIT]


def main() -> int:
    results = sorted(glob.glob(os.path.join("app", "build", "test-results", "*", "TEST-*.xml")))
    if not results:
        print("::notice title=Unit tests::no test results - the build did not get as far as running them")
        return 0

    total = failures = skipped = 0
    lines: list[str] = []

    for path in results:
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as exc:
            print(f"::error title=Unit tests::{path} is not readable XML: {flat(str(exc))}")
            continue
        for case in root.iter("testcase"):
            total += 1
            if case.find("skipped") is not None:
                skipped += 1
            problems = list(case.iter("failure")) + list(case.iter("error"))
            if not problems:
                continue
            failures += len(problems)
            klass = (case.get("classname") or "?").rsplit(".", 1)[-1]
            name = case.get("name") or "?"
            for problem in problems:
                detail = flat(problem.get("message") or problem.text)
                print(f"::error title={klass}.{name}::{detail or problem.get('type') or 'failed'}")
                lines.append(f"- `{klass}.{name}` — {detail or problem.get('type') or 'failed'}")

    ran = total - skipped
    summary = f"{total} tests, {ran} ran, {failures} failed, {skipped} skipped"
    level = "error" if failures else "notice"
    print(f"::{level} title=Unit tests::{summary}")

    with open(REPORT, "w", encoding="utf-8") as handle:
        handle.write(f"### {summary}\n\n")
        handle.write("\n".join(lines[:60]) if lines else "Все тесты прошли.\n")
        handle.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
