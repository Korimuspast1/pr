# What can and cannot be built — honestly

## TL;DR for this sandbox

| Artefact | Status here | Why |
| --- | --- | --- |
| `release/custom-profile.elyx` (exteraGram plugin) | ✅ **built, verified, reproducible** | pure Python + ZIP, no toolchain needed |
| Plugin unit + integration tests (50) | ✅ passing | pure Python, host stubbed |
| Java/Python tooling (`ayuox4gg/tools/*`) | ✅ working, 22 tests | standard library only |
| Security patches applied to upstream sources | ✅ verified with `git apply` | real tree cloned from GitHub |
| Patched Java files parse | ✅ `javalang` (Java grammar) | no JDK/Android classpath available |
| **AyuGram4A APK** | ❌ **not possible here** | needs Android SDK, NDK, Gradle and ~2 GB of downloads from hosts this sandbox cannot reach |

Test it:

```bash
# java syntax of every file this overlay ships
python3 ayuox4gg/tools/javaparse.py ayuox4gg/features/*.java

# apply + verify the security patches against a fresh upstream clone
git clone --depth 1 -b rewrite https://github.com/AyuGram/AyuGram4A.git /tmp/ayu
cd /tmp/ayu && git apply --check /path/to/ayuox4gg/security/patches/*.patch
```

## Why the APK cannot be produced here

The sandbox has outbound access to `github.com`, `codeload.github.com`,
`api.github.com`, `pypi.org`, `files.pythonhosted.org` **only**. The AyuGram
build needs:

| Requirement | Host | Reachable here |
| --- | --- | --- |
| Android SDK platform 33+, build-tools | `dl.google.com` | ❌ |
| Maven Central (OkHttp, Room, Gson, …) | `repo1.maven.org`, `repo.maven.apache.org` | ❌ |
| Google Maven (androidx, Firebase, Play Services) | `maven.google.com` | ❌ |
| Gradle distribution | `services.gradle.org` | ❌ |
| NDK 21.4.7075529 (CMake native build) | `dl.google.com` | ❌ |

There is also **no JDK, no Android SDK and no Gradle** installed, and only 2 GB
of RAM — a Telegram Android build wants 8 GB+ and ~30 minutes on a fast machine.

## What upstream itself requires

From `AyuGram4A/README.md`:

1. Clone, open in Android Studio (not "import").
2. **Implement `AyuMessageUtils` & `AyuHistoryHook`.** The repository ships
   these as stubs; upstream literally suggests finding "a reversed version". A
   usable build therefore also depends on reverse-engineered code that is not in
   the repository.
3. Replace `google-services.json` (otherwise crash reports go to the upstream
   Firebase project).
4. Generate a signing certificate.
5. Fill `API_KEYS`.
6. `./gradlew assembleAfatRelease`.

Add the overlay's patches on top:

```bash
cd /path/to/AyuGram4A
git apply ayuox4gg/security/patches/*.patch     # 3 patches, verified to apply
python3 ayuox4gg/tools/security_audit.py --dir TMessagesProj/src/main/java/com/radolyn --dir TMessagesProj/src/main
./gradlew assembleAfatRelease
python3 ayuox4gg/tools/forensics.py TMessagesProj/build/outputs/apk/afat/release/*.apk
```

## The `API_KEYS` question — do you need my.telegram.org?

Short answer: **not to compile, yes to actually log in.**

AyuGram4A does not hard-code the Telegram API credentials — `BuildVars` reads
them from `BuildConfig`, which `TMessagesProj/build.gradle` fills from the
`API_KEYS` file:

```groovy
buildConfigField 'int', 'APP_ID', localProperties.getProperty("APP_ID")
buildConfigField 'String', 'APP_HASH', localProperties.getProperty("APP_HASH")
```

If `API_KEYS` is missing, `localProperties.getProperty(...)` returns `null`,
`buildConfigField 'int', 'APP_ID', null` fails the build outright (a String field
would silently become the literal `"null"`).

So:

| What you want | What you need |
| --- | --- |
| Compile/experiment | any `APP_ID`/`APP_HASH` pair; the project's own CI uses Telegram's **public test keys** (`APP_ID = 6`, `APP_HASH = eb06d4…`) |
| Log in with your account | **your own** `APP_ID`/`APP_HASH` from <https://my.telegram.org> → *API development tools* |
| Ship to other people | definitely your own, plus a private signing key |

The public test keys are rate-limited and get abused; Telegram can and does
restrict apps that use them. For a personal build that will log in, create a
pair at my.telegram.org. It is free, takes a minute, and you do **not** need a
business account.

`MAPS_V2_API` (Google Maps) and the signing values are optional for a
side-load build: leave the maps key empty (map previews break, nothing else) and
sign with a local keystore (`keytool -genkeypair`).

## Recommended path if you want a *working* client today

AyuGram4A's last commit is from 2023 and it is a fork of a fork. Today:

1. **exteraGram** (live upstream) — install it, then use the
   `release/custom-profile.elyx` plugin from this repository.
2. **AyuGram Desktop** (`AyuGram/AyuGramDesktop`, last push 2026-10) — if you
   want AyuGram's ghost mode and local history on desktop.

If you still want the mobile fork: apply the overlay patches, keep the target SDK
current (see `compat/MODERNIZATION.md`), and use your own API keys.
