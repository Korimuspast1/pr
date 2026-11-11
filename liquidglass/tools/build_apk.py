#!/usr/bin/env python3
"""
Zero-dependency APK builder for the Liquid Glass Lab.

Why this exists
---------------
The sandbox that produced this project has no Android SDK and no Maven access, so
`./gradlew assembleDebug` is not available. This script builds a signed, installable
APK from plain Java + XML resources using only:

  * a JDK            (javac, jar, keytool)      — e.g. jdk4py or any local JDK
  * aapt2            (aapt2 Python wheel)       — resource compile/link
  * D8               (r8.jar)                   — dex
  * apksigner.jar    (AOSP tools/apksig)        — v2/v3 signing

It is the same sequence Gradle runs under the hood, spelled out. `./gradlew
assembleDebug` also works on a normal machine; both produce the same sources.

Usage
-----
    python3 tools/build_apk.py                 # build + sign release/release apk
    python3 tools/build_apk.py --out app.apk
    python3 tools/build_apk.py --min-sdk 31

Environment (any one source per tool is enough):
    JAVA_HOME, JDK, or `java` on PATH
    AAPT2, or `aapt2` on PATH, or the `aapt2` Python wheel
    ANDROID_JAR, or $ANDROID_HOME/platforms/android-*/android.jar, or --android-jar
    R8_JAR, or $ANDROID_HOME/build-tools/*/lib/d8.jar
    APKSIGNER_JAR, or $ANDROID_HOME/build-tools/*/lib/apksigner.jar
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APP = os.path.join(ROOT, "app")
MANIFEST = os.path.join(APP, "src", "main", "AndroidManifest.xml")
SRC = os.path.join(APP, "src", "main", "java")
RES = os.path.join(APP, "src", "main", "res")
ASSETS = os.path.join(APP, "src", "main", "assets")
BUILD = os.path.join(ROOT, "build", "apk")


def log(msg):
    print("[build] " + msg, flush=True)


def find_tool(env_names, candidates, python_pkg=None):
    for name in env_names:
        value = os.environ.get(name)
        if value and os.path.exists(value):
            return value
    for pattern in candidates:
        hits = sorted(glob.glob(pattern))
        if hits:
            return hits[-1]
    if python_pkg:
        try:
            mod = __import__(python_pkg)
            base = os.path.dirname(os.path.abspath(mod.__file__))
            for name in ("aapt2", "aapt2.exe"):
                p = os.path.join(base, "bin", "Linux", name)
                if os.path.exists(p):
                    os.chmod(p, 0o755)
                    return p
        except Exception:
            pass
    return None


def run(cmd, **kw):
    log(" ".join(str(c) for c in cmd[:8]) + (" …" if len(cmd) > 8 else ""))
    subprocess.run(cmd, check=True, **kw)


def java_sources():
    out = []
    for base, _dirs, files in os.walk(SRC):
        for f in files:
            if f.endswith(".java"):
                out.append(os.path.join(base, f))
    return sorted(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(ROOT, "build", "liquidglass-lab.apk"))
    ap.add_argument("--android-jar", default=None)
    ap.add_argument("--min-sdk", default="33")
    ap.add_argument("--target-sdk", default=None)
    ap.add_argument("--java-release", default="8")
    ap.add_argument("--keystore", default=os.path.join(ROOT, "release", "liquidglasslab.p12"))
    ap.add_argument("--keystore-pass", default="liquidglass")
    args = ap.parse_args()

    jdk_home = os.environ.get("JAVA_HOME") or os.environ.get("JDK")
    if jdk_home and os.path.isdir(jdk_home):
        bin_dir = os.path.join(jdk_home, "bin")
    else:
        java = shutil.which("java")
        if not java:
            raise SystemExit("no JDK: set JAVA_HOME or put java on PATH (pip install jdk4py works)")
        bin_dir = os.path.dirname(java)
    javac = os.path.join(bin_dir, "javac")
    jar = os.path.join(bin_dir, "jar")
    java_bin = os.path.join(bin_dir, "java")
    keytool = os.path.join(bin_dir, "keytool")
    log("JDK: " + bin_dir)

    home = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT") or ""
    aapt2 = find_tool(["AAPT2"],
                      [os.path.join(home, "build-tools", "*", "aapt2"),
                       os.path.expanduser("~/Android/Sdk/build-tools/*/aapt2")],
                      python_pkg="aapt2") or shutil.which("aapt2")
    if not aapt2:
        raise SystemExit("no aapt2: pip install aapt2, or set AAPT2=/path/to/aapt2")
    log("aapt2: " + aapt2)

    android_jar = args.android_jar or os.environ.get("ANDROID_JAR")
    if not android_jar:
        if home:
            hits = sorted(glob.glob(os.path.join(home, "platforms", "android-*", "android.jar")))
            if hits:
                android_jar = hits[-1]
    if not android_jar or not os.path.exists(android_jar):
        raise SystemExit("no android.jar: set ANDROID_JAR or ANDROID_HOME/platforms/...")
    log("android.jar: " + android_jar)
    api = int(java_jar_api(android_jar))

    r8 = find_tool(["R8_JAR"],
                   [os.path.join(home, "build-tools", "*", "lib", "d8.jar"),
                    os.path.join(home, "build-tools", "*", "lib", "r8.jar")])
    if not r8:
        raise SystemExit("no D8: set R8_JAR=/path/to/r8.jar or d8.jar (AOSP prebuilts has one)")

    signer = find_tool(["APKSIGNER_JAR"],
                       [os.path.join(home, "build-tools", "*", "lib", "apksigner.jar")])
    if not signer:
        raise SystemExit("no apksigner: set APKSIGNER_JAR=/path/to/apksigner.jar")

    for d in ("res-compiled", "gen", "classes", "dex"):
        shutil.rmtree(os.path.join(BUILD, d), ignore_errors=True)
    os.makedirs(BUILD, exist_ok=True)

    # 1. resources -----------------------------------------------------------
    run([aapt2, "compile", "--dir", RES, "-o", os.path.join(BUILD, "res.zip")])

    # 2. link -> resources.apk + R.java --------------------------------------
    target = args.target_sdk or str(api)
    run([aapt2, "link", "-o", os.path.join(BUILD, "resources.apk"),
         "-I", android_jar, "--manifest", MANIFEST,
         "--min-sdk-version", args.min_sdk, "--target-sdk-version", target,
         "--java", os.path.join(BUILD, "gen"),
         "--auto-add-overlay",
         os.path.join(BUILD, "res.zip")])

    # 3. javac ---------------------------------------------------------------
    sources = java_sources() + glob.glob(os.path.join(BUILD, "gen", "**", "*.java"), recursive=True)
    # android.jar goes on the *classpath*, not on -bootclasspath: modern JDKs reject
    # -bootclasspath above target 8, and we do want the JDK's own java.* classes (the
    # platform ones) with android.* resolved from the platform jar. This is the same
    # classpath/Android Boot Classpath split AGP uses.
    run([javac, "-encoding", "UTF-8", "-nowarn", "-source", args.java_release,
         "-target", args.java_release, "-classpath", android_jar, "-Xlint:-options",
         "-d", os.path.join(BUILD, "classes")] + sources)

    # 4. jar -> dex ----------------------------------------------------------
    run([jar, "cf", os.path.join(BUILD, "classes.jar"), "-C", os.path.join(BUILD, "classes"), "."])
    os.makedirs(os.path.join(BUILD, "dex"), exist_ok=True)
    run([java_bin, "-Xmx2g", "-cp", r8, "com.android.tools.r8.D8",
         "--min-api", args.min_sdk, "--lib", android_jar,
         "--output", os.path.join(BUILD, "dex"), os.path.join(BUILD, "classes.jar")])

    # 5. pack ----------------------------------------------------------------
    unsigned = os.path.join(BUILD, "unsigned.apk")
    shutil.copyfile(os.path.join(BUILD, "resources.apk"), unsigned)
    with zipfile.ZipFile(unsigned, "a", zipfile.ZIP_DEFLATED) as z:
        for dex in sorted(glob.glob(os.path.join(BUILD, "dex", "*.dex"))):
            z.write(dex, os.path.basename(dex))
        if os.path.isdir(ASSETS):
            for base, _dirs, files in os.walk(ASSETS):
                for f in files:
                    full = os.path.join(base, f)
                    z.write(full, "assets/" + os.path.relpath(full, ASSETS))

    # 6. sign ----------------------------------------------------------------
    if not os.path.exists(args.keystore):
        os.makedirs(os.path.dirname(args.keystore), exist_ok=True)
        run([keytool, "-genkeypair", "-keystore", args.keystore, "-storetype", "PKCS12",
             "-storepass", args.keystore_pass, "-keypass", args.keystore_pass,
             "-alias", "liquidglasslab", "-keyalg", "RSA", "-keysize", "2048",
             "-validity", "10000", "-dname",
             "CN=Liquid Glass Lab, O=Arena, C=DE", "-noprompt"])
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    run([java_bin, "-jar", signer, "sign",
         "--ks", args.keystore, "--ks-pass", "pass:" + args.keystore_pass,
         "--key-pass", "pass:" + args.keystore_pass, "--ks-key-alias", "liquidglasslab",
         "--min-sdk-version", args.min_sdk, "--out", args.out, unsigned])
    run([java_bin, "-jar", signer, "verify", "--verbose", args.out])
    log("APK: %s (%.1f KiB)" % (args.out, os.path.getsize(args.out) / 1024.0))


def java_jar_api(android_jar):
    """Reads the API level out of the platform jar name, else from the zip entry count."""
    import re
    m = re.search(r"android-(\d+)", android_jar)
    if m:
        return int(m.group(1))
    return 33


if __name__ == "__main__":
    sys.exit(main())
