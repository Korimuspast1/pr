# What can and cannot be built — honestly

## TL;DR for this sandbox

| Artefact | Status here | Why |
| --- | --- | --- |
| `release/custom-profile.elyx` (exteraGram plugin) | ✅ **built, verified, reproducible** | pure Python + ZIP, no toolchain needed |
| Plugin unit + integration tests (50) | ✅ passing | pure Python, host stubbed |
| Java/Python tooling (`ayuox4gg/tools/*`) | ✅ working, 22 tests | standard library only |
| Security patches applied to upstream sources | ✅ verified with `git apply` | real tree cloned from GitHub |
| Patched Java files parse | ✅ `javalang` (Java grammar) | no JDK/Android classpath available |
| **AyuGram4A APK** | ⚠️ **built on GitHub Actions, not here** | needs Android SDK, NDK, Gradle and ~2 GB from hosts this sandbox cannot reach — `.github/workflows/build-ayuox4gg.yml` does it on a runner |

Test it:

```bash
# java syntax of every file this overlay ships
python3 ayuox4gg/tools/javaparse.py ayuox4gg/features/*.java

# apply + verify the security patches against a fresh upstream clone
git clone --depth 1 -b rewrite https://github.com/AyuGram/AyuGram4A.git /tmp/ayu
cd /tmp/ayu && git apply --check /path/to/ayuox4gg/security/patches/*.patch
```

## The real build: `Build ayuox4gg APK`

`.github/workflows/build-ayuox4gg.yml` produces an installable APK from upstream
sources with this overlay applied. It runs on a stock `ubuntu-latest` runner:

| Step | What it does |
| --- | --- |
| Fetch | downloads `AyuGram/AyuGram4A` at the pinned commit `701314567` (2023-07-28) as a tarball — 87 MB instead of a 536 MB clone, and it skips the private submodule |
| Overlay | `scripts/bootstrap-ayu.sh`: `API_KEYS` (mode 600), stand-ins for `proprietary/`, patches `0001`–`0003` |
| Signing | replaces `TMessagesProj/config/extera.jks` with our own key (finding X1: the upstream one is public and its password is in the repo) |
| Build | `assembleArm64Release` (or `assembleAfatRelease` via the `flavor` input) with NDK 21.4.7075529 / CMake 3.10.2 |
| Verify | `tools/forensics.py` + `apksigner verify --print-certs`; the SHA-256 goes into the run summary |
| Publish | artifact `ayuox4gg-arm64-apk` (APK + `.sha256`), plus `ayuox4gg-signing-key` when the key was generated for this run |

### Trigger it

```bash
# the run is expensive (~40-70 min of native compilation), so it has a marker
echo "rebuild $(date -u)" > ayuox4gg/REBUILD
git add ayuox4gg/REBUILD && git commit -m "ci: rebuild" && git push
```

`workflow_dispatch` with a `flavor` choice (`arm64` / `afat`) works too, but from
an account whose token has the `workflow` scope.

### Signing: read this before publishing an update

*If no keystore secret is configured*, the workflow generates a throw-away
4096-bit RSA key, signs the APK with it, and uploads both the keystore and its
password as the `ayuox4gg-signing-key` artifact. That APK installs fine — but the
**next** build gets a different key, and Android refuses to update an installed
app across signing identities. Pick the key once, then pin it:

```bash
# 1. download the ayuox4gg-signing-key artifact and unpack it
base64 -w0 release.jks > key.b64

# 2. store it as repository secrets (needs admin rights on the repo)
gh secret set AYUOX4GG_KEYSTORE_B64      < key.b64
gh secret set AYUOX4GG_KEYSTORE_PASSWORD < password.txt
gh secret set AYUOX4GG_KEY_ALIAS         --body ayuox4gg
rm key.b64                      # do not leave the key lying around
```

Optional, to send Telegram your own credentials instead of the public pair the
official builds use: `AYUOX4GG_APP_ID`, `AYUOX4GG_APP_HASH`.

### Verify a build yourself

```bash
python3 ayuox4gg/tools/forensics.py ayu.apk --expect-cert <SHA-256 of the cert>
python3 ayuox4gg/tools/forensics.py ayu.apk --expect-sha256 <digest from the run summary>
```

The certificate digest of the generated key is printed in the run log
(`SHA256:` line) and in `apksigner verify --print-certs`.

## Builds that actually ran

| Run | Result |
| --- | --- |
| first | failed at `Apply ayuox4gg overlay` (a `$GITHUB_ENV` value used inside the same step) |
| second | native CMake/NDK **succeeded**; javac failed on 4 errors, all in this overlay |
| third | fixed those; `AyuUpdateVerifier` failed to compile (`NameNotFoundException` not caught) |
| fourth | **success** — APK in `ayuox4gg/artifacts/ayuox4gg-20261110-0447/` |
| fifth | success, published manifest was missing, so the build was unverifiable |
| sixth | **success** — `ayuox4gg-20261111-0115` |
| seventh | success, but the dex proved patch 0005 had been skipped (`bootstrap-ayu.sh` only warns) |
| eighth | failed in javac: the rebrand had renamed 55 string **ids**, so `R.string.*` no longer resolved |
| ninth | failed in javac: `getDatabase()` returns Telegram's own SQLite wrapper, not Android's |
| tenth | **success** — `ayuox4gg-20261111-1212`, verified below |

The last published build:

| | |
| --- | --- |
| file | `ayuGram-arm64-v8a-11112026.apk` (43,355,975 bytes, 10,594 entries) |
| sha256 | `7a73da99927a7b171829b30bb43ac04cbc10005b35fc4504687a12e728ca08cd` |
| certificate sha256 | `56879abfb5c6400df066ac87bfd5ebf77bc9bc235bb772a4ed7b01fbb7193f02` |
| native | `lib/arm64-v8a/libtmessages.45.so`, 23.3 MB |
| cleartext | restricted by `networkSecurityConfig` (patch 0002) |
| debug signed | no |

```bash
python3 ayuox4gg/tools/unpack_apk.py --list
python3 ayuox4gg/tools/unpack_apk.py ayuox4gg-20261111-1212 --out ~/ayu.apk
python3 ayuox4gg/tools/forensics.py ~/ayu.apk \
  --expect-sha256 7a73da99927a7b171829b30bb43ac04cbc10005b35fc4504687a12e728ca08cd
```

Verified inside that APK's dex (not just in the repository): the proprietary
stand-ins, patches 0001-0004, `AyuSecrets` (`ayuox4gg.secrets.v1`,
`ayu_secrets`) and `AyuUpdateVerifier` (`signing certificate mismatch`,
`refusing to install update`) — and, from patch 0005, `AyuChatExporter`,
`AyuChatExportActivity`, `ayuox4gg-exports` and the four badge preference keys
(`localVerified`, `localPremiumBadge`, `localScam`, `localFake`).

The UI strings live in `resources.arsc`, not in dex, so check them there: the
1212 APK contains "Export a chat to a .txt file", "Local verified badge" and
the new `ayu0x4gg` app name, and no longer contains the old
"AyuGram Preferences".

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

### The closed `proprietary/` submodule

`TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary` is a **git
submodule** pointing at `https://github.com/AyuGram/AyuGram4A_proprietary.git`,
which returns **404 — it is private**. Three public files call 8 methods from it,
so an unmodified clone cannot compile:

| Call site | Methods used |
| --- | --- |
| `com/radolyn/ayugram/messages/AyuMessagesController.java` | `AyuMessageUtils.map(...)`, `.mapMedia(...)` (×4) |
| `com/radolyn/ayugram/ui/AyuMessageHistory.java` | `AyuMessageUtils.map(...)`, `.mapMedia(...)` (×2) |
| `org/telegram/ui/ChatActivity.java` | `AyuHistoryHook.getMinAndMaxIds(...)`, `.doHook(...)` (×2) |

`ayuox4gg/proprietary-stubs/` contains **public stand-ins with those exact
signatures**, so the tree compiles:

* text, links, reply/forward metadata, flags, topics and dates are stored and
  rendered properly — deleted/edited **text** history works;
* media graphs are not re-materialised (no file copying, no TL document
  serialisation), so media messages are recorded without their attachment;
* the in-chat splice of deleted messages (`AyuHistoryHook.doHook`) is a no-op —
  the separate history screen still works.

Copy them in with the bootstrap script (below), or manually:

```bash
mkdir -p TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary
cp ayuox4gg/proprietary-stubs/*.java TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary/
git rm --cached TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary   # drop the gitlink
```

Replacing these two files with the real proprietary classes restores full
behaviour without touching anything else.

### One-command bootstrap

```bash
./ayuox4gg/scripts/bootstrap-ayu.sh --tree /path/to/AyuGram4A \
    --app-id <your id> --app-hash <your 32-hex hash>
```

It writes `API_KEYS` (mode 600, refuses to write anywhere git would not ignore
it), installs the stand-ins, drops the private submodule entry, applies the three
security patches and runs the checks. Verified end-to-end against a fresh
`rewrite` clone: `API_KEYS` ignored, gitlink dropped, 3/3 patches applied,
stand-ins parse.

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
