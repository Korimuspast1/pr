# ayuox4gg — AyuGram4A fork overlay

Work for [AyuGram/AyuGram4A](https://github.com/AyuGram/AyuGram4A) `rewrite`
(last push **2023-09-11**; the fork is a snapshot of exteraGram + Telegraher
patches, so *"not updated in 12 years"* is really *"~3 years of upstream
Android/Telegram changes, plus a 2023 dependency stack"*).

**Plain-language summary (RU): [`docs/EXPLAINED_RU.md`](docs/EXPLAINED_RU.md)**

| Path | What it is |
| --- | --- |
| [`security/FINDINGS.md`](security/FINDINGS.md) | Security review of the real `rewrite` sources: 4 high/medium + 5 hardening findings, each with file and code |
| [`security/patches/`](security/patches/) | Three `git apply`-able patches fixing them; verified against a fresh clone |
| [`security/PIPELINE-NOTES.md`](security/PIPELINE-NOTES.md) | Hardening the release CI (secrets in the workspace, unpinned actions) |
| [`compat/MODERNIZATION.md`](compat/MODERNIZATION.md) | AGP 8.7 / Gradle 8.9 / JDK 17 / SDK 35 / NDK 26 migration plan with diffs and a dead-line |
| [`features/AyuSecrets.java`](features/AyuSecrets.java) | AES-256-GCM + Android Keystore secret storage (no new deps) |
| [`features/AyuUpdateVerifier.java`](features/AyuUpdateVerifier.java) | SHA-256 + signer-certificate verification for in-app updates (replaces MD5) |
| [`tools/security_audit.py`](tools/security_audit.py) | 12-rule scanner for the fork-specific mistakes (cleartext sync, token in prefs, unguarded auth headers, …) |
| [`tools/forensics.py`](tools/forensics.py) | Offline APK forensics: hashes, v1/v2/v3 signer certificate, dangerous permissions |
| [`tools/javaparse.py`](tools/javaparse.py) | Java syntax check without a JDK |
| [`tests/`](tests/) | 22 unit tests for all of the above |
| [`docs/BUILD.md`](docs/BUILD.md) | What can/cannot be compiled here, and the `API_KEYS` / my.telegram.org answer |

## Measured effect

```
$ python3 ayuox4gg/tools/security_audit.py --dir TMessagesProj/src/main/java/com/radolyn --dir TMessagesProj/src/main
# upstream rewrite, before patches
3 high, 1 medium, 6 low
# after applying security/patches/*
0 high, 0 medium, 4 low
```

```bash
git clone --depth 1 -b rewrite https://github.com/AyuGram/AyuGram4A.git /tmp/ayu
cd /tmp/ayu
git apply --check /path/to/ayuox4gg/security/patches/*.patch   # → clean
git apply /path/to/ayuox4gg/security/patches/*.patch
```

## Why this is an overlay, not a rewritten tree

AyuGram4A needs the Android SDK/NDK, Maven Central and Google Maven, and four
Java classes that upstream deliberately does not publish. This sandbox can reach
`github.com`, `api.github.com` and `pypi.org` only, and has no JDK/Android SDK —
so an APK cannot be produced here. Everything in this folder applies cleanly to a
normal build machine; see [`docs/BUILD.md`](docs/BUILD.md).

## Quick start on a build machine

```bash
git clone --depth 1 -b rewrite https://github.com/AyuGram/AyuGram4A.git ayu && cd ayu
git apply /path/to/ayuox4gg/security/patches/*.patch
python3 /path/to/ayuox4gg/tools/javaparse.py TMessagesProj/src/main/java/com/radolyn/ayugram/sync/*.java
./gradlew assembleAfatRelease          # see docs/BUILD.md for API_KEYS and the missing classes
python3 /path/to/ayuox4gg/tools/forensics.py TMessagesProj/build/outputs/apk/afat/release/*.apk
```

## Branch note

Arena ties this session to `arena/8849556d-pr`, so a separate `ayuox4gg` branch
cannot be created from here; the work lives in this folder instead. After the PR
is merged, `git checkout -b ayuox4gg` from `main` gives you exactly that branch
name with this content already in it.
