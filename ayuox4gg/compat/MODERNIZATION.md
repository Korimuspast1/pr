# Build modernisation — AyuGram4A `rewrite`

This is a **plan with concrete file diffs**, not a wish list. It is written
against the tree as it stands on `rewrite` (2023-09-11) and is ordered so that
each step can be merged and tested on its own.

> **Reality check first.** *AyuGram4A is not 12 years old.* Its last commit is
> **2023-07-28** (pushed 2023-09-11) — under three years before the date of this
> work. What *is* old is everything it inherited: the fork snapshots
> **Telegram Android 9.6.6** and **exteraGram** of mid-2023, so the app still
> targets **Android 13 (API 33)**, builds with **AGP 7.4.2 / Gradle 7.5.1**, and
> ships **~90 dependencies pinned to 2023 versions**. Google Play, however,
> requires new app updates to target **API 35** from 31 Aug 2025 (**API 36**
> from Aug 2026). That is the real deadline this plan is about.
>
> The live, maintained upstream is **exteraGram** (the project this is forked
> from) and **AyuGramDesktop**. `AyuGram4A` itself has been dormant since 2023.

## Current vs target

| Item | `rewrite` today | Target | Why |
| --- | --- | --- | --- |
| `compileSdk` / `targetSdk` | 33 / 33 | 35 / 35 | Play policy; API 34/35 behaviour changes |
| `minSdk` | 21 | 24 | Android 5.x is EOL; exteraGram itself is 24+ |
| AGP | 7.4.2 | 8.7.x | required for `compileSdk 35` |
| Gradle | 7.5.1 | 8.9 | AGP 8.x requirement |
| JDK | 11 (Dockerfile) | 17 | AGP 8.x requirement |
| Kotlin | — (Java only) | none | **keep Java**: no Kotlin migration needed |
| Room | 2.5.2 | 2.6.1+ | API 34 compile fixes |
| OkHttp | 4.10.0 | 4.12.0 | TLS fixes, `CertificatePinner` |
| Firebase BOM | 32.1.0 | 33.x | transitive vulnerable Play Services |
| BoringSSL prebuilts | 2023 `.a` files | rebuild from source | old OpenSSL CVEs |
| SQLite | 2023 amalgamation | latest | CVE fixes in the amalgamation |
| NDK | 21.4.7075529 | 26.1 | 21 is EOL; 16 KB page-size support |
| `usesCleartextTraffic` | `true` | network-security-config | see `security/` |
| CI | `actions/*@v3` | v4 + pinned SHAs | Node 16 EOL, supply chain |

## Step 1 — toolchain (`build.gradle`, `gradle.properties`, wrapper, `Dockerfile`)

```diff
 // build.gradle (root)
-        classpath 'com.android.tools.build:gradle:7.4.2'
-        classpath 'com.google.gms:google-services:4.3.15'
-        classpath 'com.google.firebase:firebase-crashlytics-gradle:2.9.5'
+        classpath 'com.android.tools.build:gradle:8.7.3'
+        classpath 'com.google.gms:google-services:4.4.2'
+        classpath 'com.google.firebase:firebase-crashlytics-gradle:3.0.2'
```

```properties
# gradle/wrapper/gradle-wrapper.properties
-distributionUrl=https\://services.gradle.org/distributions/gradle-7.5.1-bin.zip
+distributionUrl=https\://services.gradle.org/distributions/gradle-8.9-bin.zip
```

```properties
# gradle.properties
+android.suppressUnsupportedCompileSdk=35
+# AGP 8 requires a namespace (already set) and removes buildConfig by default:
+android.defaults.buildfeatures.buildconfig=true
```

AGP 8 removes several long-deprecated APIs; the ones this tree actually uses:

* `BuildConfig` generation is **off** by default → the flag above (already
  present in the tree, good).
* `package` in `AndroidManifest.xml` is an error; the tree already uses
  `namespace` in Gradle and the manifest has no `package` attribute.
* `android.enableJetifier=true` still works but is deprecated; drop it once no
  `android.support.*` remain (none here).
* `android.nonFinalResIds=true` / `nonTransitiveRClass=true` stay.

## Step 2 — SDK levels

```diff
 android {
-    compileSdkVersion 33
-    buildToolsVersion '33.0.1'
+    compileSdk 35
+    buildToolsVersion '35.0.0'
     ...
-        minSdkVersion 21
-        targetSdkVersion 33
+        minSdkVersion 24
+        targetSdkVersion 35
```

Then handle the two API 34/35 behaviour changes that touch this codebase:

1. **`foregroundServiceType` is mandatory** (API 34). Every `<service>` in the
   manifest needs an explicit type; the media playback service needs
   `mediaPlayback|mediaProjection`, the VoIP service `phoneCall|microphone`,
   the download service `dataSync`. Add `tools:targetApi="34"` only where the
   type is intentionally absent.
2. **Edge-to-edge is enforced** (API 35). Window insets must be applied
   explicitly; Telegram's `ActionBar`/`FrameLayout` helpers already do this,
   but the plugin host (`com.exteragram.messenger.plugins.*`) and AyuGram's own
   preference activities do not.

## Step 3 — dependency refresh (safe order)

Update and re-test in this order; each line is one PR.

| # | Dependency | From | To | Risk |
| --- | --- | --- | --- | --- |
| 1 | OkHttp | 4.10.0 | 4.12.0 | none (API compatible) |
| 2 | Room | 2.5.2 | 2.6.1 | medium (codegen, API 34) |
| 3 | androidx.core | 1.10.0 | 1.13.1 | low |
| 4 | androidx.exifinterface | 1.3.6 | 1.3.7 | none |
| 5 | androidx.camera | 1.3.0-alpha07 | 1.4.x stable | medium — API churn |
| 6 | Firebase BOM | 32.1.0 | 33.x | medium — Crashlytics plugin bump |
| 7 | `billing` | 5.2.0 | 7.x | **high** — API rewrite |
| 8 | `stripe-android` | 2.0.2 | — | **remove** unless billing is used |
| 9 | `play-services-vision` | 20.1.3 | → ML Kit barcode | **high** — deprecated module |
| 10 | guava | 32.0.0-jre | 33.x | low |
| 11 | gson | 2.10.1 | 2.11.0 | low |

`dev.gustavoavila:java-android-websocket-client:2.0.2` (AyuSync) is unmaintained;
either keep it with the hardening patch in `security/`, or move to OkHttp's own
`WebSocket` (recommended — zero new dependencies).

## Step 4 — native code

```diff
-    ndkVersion "21.4.7075529"
+    ndkVersion "26.1.10909125"
```

* **BoringSSL**: replace `TMessagesProj/jni/boringssl/lib/libcrypto_*.a`
  prebuilts with a source build (`jni/boringssl` already contains the sources).
* **SQLite**: refresh the amalgamation to the current release.
* **16 KB page size**: required for new Play submissions from Nov 2025 for
  apps with native code; NDK 26+ emits the correctly aligned segments.

## Step 5 — CI

```diff
-      - uses: actions/checkout@v3
+      - uses: actions/checkout@v4        # pin to a commit SHA in production
...
-          java-version: '17'
+          java-version: '17'
```

Plus:

```yaml
permissions:
  contents: read
```

on the build job, and move `API_KEYS` creation into a step that deletes the file
in the same `run:` block (see `security/PIPELINE-NOTES.md`).

## What cannot be modernised without upstream work

* **Telegram Android 9.6.6 base** — every new Telegram feature (stories,
  gifts, star reactions, AI, …) is missing. Upstream's own diff is several
  thousand commits; this is exteraGram's job, not a fork patch.
* **`AyuMessageUtils` / `AyuHistoryHook`** — published only as stubs; see
  `docs/BUILD.md`.
* **Python plugin SDK** — exteraGram's plugin host is now at SDK `1.4.5.5`
  (CPython 3.11/Chaquopy 16). A 2023 build runs the 2023 host, so the
  `custom-profile` plugin that ships in this repository requires
  **exteraGram 12.5.1+**, not this fork release.

## Verification gates (run before merging any step)

```bash
./gradlew :TMessagesProj:lintAfatRelease      # Android Lint
./gradlew :TMessagesProj:assembleAfatRelease  # real build
python3 ayuox4gg/tools/security_audit.py --dir TMessagesProj/src/main/java/com/radolyn \
                                          --dir TMessagesProj/src/main
python3 ayuox4gg/tools/forensics.py TMessagesProj/build/outputs/apk/afat/release/app*.apk
```
