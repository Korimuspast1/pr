"""Install-time integrity checks for Custom Profile.

A plugin archive is a ZIP of Python modules, and the host executes whatever is
inside it.  This module gives the shipped archive a cheap, offline way to prove
that the files which are actually loaded are the files that were built:

* every packaged file is hashed with SHA-256;
* the expected digests live in :mod:`manifest`, which the build tool writes into
  the archive;
* a mismatch (edited, truncated or replaced module) is reported instead of
  silently executed.

It is defence in depth, not a cryptographic signature: an attacker who can
rewrite the whole archive can rewrite the manifest too.  Its practical value is
catching a modified file inside a downloaded archive and telling the user which
file changed.  The official digest of the whole archive is published next to
the release and can be checked out of band.

The module only imports the standard library so it can also run in a plain
CPython (unit tests, CI) without the exteraGram runtime.
"""

from __future__ import annotations

import hashlib
import os
from typing import Dict, Iterable, List, Optional, Tuple

CHUNK_SIZE = 128 * 1024
#: Files inside the archive that can never be verified against themselves.
SELF_REFERENCING = ("manifest.py",)


def sha256_file(path: str) -> Optional[str]:
    """Return the hex SHA-256 of ``path`` or ``None`` when it is unreadable."""
    digest = hashlib.sha256()
    try:
        with open(path, "rb") as handle:
            while True:
                chunk = handle.read(CHUNK_SIZE)
                if not chunk:
                    break
                digest.update(chunk)
    except OSError:
        return None
    return digest.hexdigest()


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def expected_hashes() -> Dict[str, str]:
    """Return ``{relative_path: sha256}`` recorded by the build tool."""
    try:
        from manifest import FILES  # type: ignore
    except Exception:
        return {}
    if isinstance(FILES, dict):
        return {str(key): str(value) for key, value in FILES.items()}
    return {}


def _candidate_roots() -> Iterable[str]:
    """Yield plausible locations of the installed plugin directory."""
    seen: List[str] = []

    def _add(path: Optional[str]) -> Iterable[str]:
        if not path:
            return
        absolute = os.path.abspath(path)
        if absolute not in seen and os.path.isdir(absolute):
            seen.append(absolute)
            yield absolute

    yield from _add(os.path.dirname(os.path.abspath(__file__)))

    try:  # Elyx exposes the resolved environment; prefer its directory.
        from elyx import get_environment  # type: ignore

        environment = get_environment() or {}
        for key in ("dir", "directory", "path", "plugin_dir", "base_dir"):
            value = environment.get(key)
            if isinstance(value, str):
                yield from _add(value)
    except Exception:
        pass


def verify_installation(root: Optional[str] = None) -> Tuple[bool, Dict[str, object]]:
    """Compare packaged files with :mod:`manifest`.

    Returns ``(ok, report)`` where ``report`` contains ``missing``, ``modified``,
    ``unexpected`` and ``checked`` entries.  When no manifest is bundled (for
    example when running from a source checkout) the check is skipped and
    reported as unverified-but-expected rather than failing.
    """
    hashes = expected_hashes()
    report: Dict[str, object] = {
        "manifest": bool(hashes),
        "root": None,
        "checked": 0,
        "missing": [],
        "modified": [],
        "unexpected": [],
    }
    if not hashes:
        return True, report

    base = root or next(iter(_candidate_roots()), None)
    report["root"] = base
    if not base:
        report["missing"] = sorted(hashes)
        return False, report

    for relative, expected in sorted(hashes.items()):
        if relative in SELF_REFERENCING:
            continue
        path = os.path.join(base, *relative.split("/"))
        actual = sha256_file(path)
        report["checked"] = int(report["checked"]) + 1
        if actual is None:
            report["missing"].append(relative)  # type: ignore[union-attr]
        elif actual.lower() != expected.lower():
            report["modified"].append(relative)  # type: ignore[union-attr]

    # Extra .py files that are not covered by the manifest are suspicious: the
    # host would import them, yet nothing vouches for their content.
    known = set(hashes) | set(SELF_REFERENCING)
    try:
        for entry in sorted(os.listdir(base)):
            if entry.endswith(".py") and entry not in known:
                report["unexpected"].append(entry)  # type: ignore[union-attr]
    except OSError:
        pass

    ok = not (report["missing"] or report["modified"] or report["unexpected"])
    return ok, report


def describe(report: Dict[str, object]) -> str:
    """One-line human summary of a :func:`verify_installation` report."""
    if not report.get("manifest"):
        return "no manifest bundled"
    problems: List[str] = []
    for key, label in (("modified", "modified"), ("missing", "missing"), ("unexpected", "unexpected")):
        values = report.get(key) or []
        if values:
            problems.append("{}: {}".format(label, ", ".join(str(item) for item in values)))
    if not problems:
        return "ok ({} files verified)".format(report.get("checked", 0))
    return "; ".join(problems)
