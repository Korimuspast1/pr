#!/usr/bin/env python3
"""Create and inspect a release keystore for a fork — outside the repository.

Background: the upstream public repository ships a *release* keystore at
``TMessagesProj/config/extera.jks`` and uses it as ``signingConfigs.release``.
Anyone able to open that file can sign an APK that Android accepts as a genuine
update, so a fork must ship with its **own** key.

This helper keeps that key where it belongs: outside the work tree.

Usage
-----
    # create (prompts for passwords, never echoes them, never in git)
    python3 ayuox4gg/tools/keystore.py create --keystore ~/keys/ayuox4gg.jks

    # show the certificate fingerprint that users should verify against
    python3 ayuox4gg/tools/keystore.py fingerprint --keystore ~/keys/ayuox4gg.jks

    # generate the API_KEYS lines the Gradle build needs
    python3 ayuox4gg/tools/keystore.py env --keystore ~/keys/ayuox4gg.jks

Requires ``keytool`` (any JDK). No third-party Python packages.
"""

from __future__ import annotations

import argparse
import getpass
import shutil
import subprocess
import sys
from pathlib import Path
from typing import List, Optional


def keytool() -> str:
    from_environment = java_home_from_env()
    if from_environment:
        return from_environment
    found = shutil.which("keytool")
    if found:
        return found
    for candidate in sorted(Path("/usr/lib/jvm").glob("*/bin/keytool")):
        return str(candidate)
    raise SystemExit("ERROR: keytool not found — install a JDK (17 recommended)")


def java_home_from_env() -> Optional[str]:
    import os

    for variable in ("JAVA_HOME", "JDK_HOME"):
        value = os.environ.get(variable)
        if value and (Path(value) / "bin" / "keytool").is_file():
            return str(Path(value) / "bin" / "keytool")
    return None


def confirm_safe_path(keystore: Path) -> None:
    """Refuse to write a private key inside a git work tree."""
    parent = keystore.resolve().parent
    for directory in [parent, *parent.parents]:
        if (directory / ".git").exists():
            raise SystemExit(
                "ERROR: {} is inside a git work tree ({}).\n"
                "Put the keystore outside the repository, e.g. ~/keys/.".format(keystore, directory)
            )


def parse_fingerprint(output: str) -> Optional[str]:
    for line in output.splitlines():
        if "SHA256:" in line:
            return line.split("SHA256:", 1)[1].strip().replace(":", "").lower()
    return None


def create(args) -> int:
    keystore = Path(args.keystore).expanduser()
    if keystore.exists() and not args.force:
        raise SystemExit("ERROR: {} already exists (use --force to overwrite)".format(keystore))
    confirm_safe_path(keystore)
    keystore.parent.mkdir(parents=True, exist_ok=True)

    password = getpass.getpass("keystore password (min 6 chars): ")
    if len(password) < 6:
        raise SystemExit("ERROR: password too short")
    if getpass.getpass("repeat password: ") != password:
        raise SystemExit("ERROR: passwords differ")

    command: List[str] = [
        keytool(), "-genkeypair", "-v",
        "-keystore", str(keystore),
        "-storetype", "PKCS12",
        "-storepass:env", "AYU_KEYSTORE_PASSWORD",
        "-alias", args.alias,
        "-keyalg", "RSA", "-keysize", "4096", "-validity", "10000",
        "-dname", args.dname,
    ]
    import os

    environment = dict(os.environ, AYU_KEYSTORE_PASSWORD=password)
    result = subprocess.run(command, capture_output=True, text=True, env=environment)
    if result.returncode != 0:
        print(result.stderr.strip() or result.stdout.strip(), file=sys.stderr)
        return 1
    try:
        keystore.chmod(0o600)
    except OSError:
        pass
    fingerprint = parse_fingerprint(result.stdout)
    print("keystore : {}".format(keystore))
    print("alias    : {}".format(args.alias))
    print("sha256   : {}".format(fingerprint or "(run `fingerprint` to read it)"))
    print("\nAdd to API_KEYS (never commit it):")
    print("    SIGNING_KEY_STORE_FILE = {}".format(keystore))
    print("    SIGNING_KEY_ALIAS = {}".format(args.alias))
    print("    SIGNING_KEY_STORE_PASSWORD = <password>")
    print("    SIGNING_KEY_PASSWORD = <password>")
    return 0


def fingerprint(args) -> int:
    keystore = Path(args.keystore).expanduser()
    command = [keytool(), "-list", "-v", "-keystore", str(keystore), "-storepass:env", "AYU_KEYSTORE_PASSWORD"]
    import os

    result = subprocess.run(command, capture_output=True, text=True,
                            env=dict(os.environ, AYU_KEYSTORE_PASSWORD=args.password or ""))
    if result.returncode != 0:
        print("ERROR: could not read the keystore (wrong password?)", file=sys.stderr)
        return 1
    value = parse_fingerprint(result.stdout)
    if not value:
        print("ERROR: no certificate in this keystore", file=sys.stderr)
        return 1
    print(value)
    print("\nUsers verify your builds with:")
    print("    python3 ayuox4gg/tools/forensics.py app.apk --expect-cert {}".format(value))
    return 0


def env(args) -> int:
    keystore = Path(args.keystore).expanduser()
    if not keystore.is_file():
        print("ERROR: no such keystore: {}".format(keystore), file=sys.stderr)
        return 1
    print("SIGNING_KEY_STORE_FILE = {}".format(keystore))
    print("SIGNING_KEY_ALIAS = {}".format(args.alias))
    print("SIGNING_KEY_STORE_PASSWORD = <your password>")
    print("SIGNING_KEY_PASSWORD = <your password>")
    return 0


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0],
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    create_parser = sub.add_parser("create", help="generate a new release keystore")
    create_parser.add_argument("--keystore", default="~/keys/ayuox4gg.jks")
    create_parser.add_argument("--alias", default="ayuox4gg")
    create_parser.add_argument("--dname", default="CN=AyuGram4A fork, O=Personal build, C=PL")
    create_parser.add_argument("--force", action="store_true")
    create_parser.set_defaults(func=create)

    fingerprint_parser = sub.add_parser("fingerprint", help="print the SHA-256 of the signing certificate")
    fingerprint_parser.add_argument("--keystore", required=True)
    fingerprint_parser.add_argument("--password", help="keystore password (prompted if omitted)")
    fingerprint_parser.set_defaults(func=fingerprint)

    env_parser = sub.add_parser("env", help="print the API_KEYS lines for the Gradle build")
    env_parser.add_argument("--keystore", required=True)
    env_parser.add_argument("--alias", default="ayuox4gg")
    env_parser.set_defaults(func=env)

    args = parser.parse_args(argv)
    if args.command == "fingerprint" and not args.password:
        args.password = getpass.getpass("keystore password: ")
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
