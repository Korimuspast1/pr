# AyuGram4A `rewrite` — security review

Scope: the `rewrite` branch of `AyuGram/AyuGram4A` at its last push
(**2023-09-11**, commit `7013145676…`), read directly from GitHub. Findings are
grouped by severity. Every finding cites the file and the code as it exists in
that tree, and says whether a patch in `security/patches/` fixes it.

Severity legend: **High** = user data or account can be harmed remotely;
**Medium** = meaningful risk under realistic conditions; **Low** = hardening /
defence in depth.

---

## High

### H1. The AyuSync client can send the sync token over plaintext HTTP/WS

`TMessagesProj/src/main/java/com/radolyn/ayugram/sync/AyuSyncConfig.java`

```java
private static String getWebSocketProtocol() {
    return AyuConfig.useSecureConnection ? "wss://" : "ws://";
}
...
public static String getProfileURL() {
    return getHTTPProtocol() + AyuConfig.getSyncServerURL() + "/ui/profile?token=" + AyuConfig.getSyncServerToken();
}
```

The default server is remote (`AyuConstants.DEFAULT_AYUSYNC_SERVER =
"ayusync.cloud"`, debug builds point at `ayusync-dev.radolyn.com:5000`). The
scheme is controlled by a single boolean that the user can flip in
*Settings → AyuSync*, and the profile URL additionally puts the **long-lived
token into a URL query string**, where it lands in browser history, proxy logs
and `Referer` headers.

`AyuInterceptor` then attaches that same token to every sync request:

```java
.addHeader("Authorization", AyuSyncConfig.getToken())
```

So one insecure toggle exposes a credential that authenticates a device to the
sync server. Message history, read states and device metadata flow through that
channel (see `AyuSyncController`).

**Fix:** `patches/0001-sync-https-only.patch`
— always use `https`/`wss`; refuse any configured server that is not HTTPS
unless it is a loopback/`10.0.2.2` development host; carry the token in an
`Authorization` header instead of a query string; reject `http://` URLs
case-insensitively by normalising the configured host first.

### H2. `usesCleartextTraffic="true"` is compiled into release builds

`TMessagesProj/src/main/AndroidManifest.xml`

```xml
<!-- AyuGram: remove for release `usesCleartextTraffic` -->
<application
    ...
    android:usesCleartextTraffic="true"
```

`BuildVars` shows the manifest flag was meant to be a debug-only convenience, but
it is unconditional, so **any** HTTP URL the app is asked to fetch (og:image
previews, in-app browser, updater, sync) can be downgraded by a network attacker.

**Fix:** `patches/0002-transport-and-manifest.patch`
— replace the blanket flag with a `networkSecurityConfig` that has
`cleartextTrafficPermitted="false"` in `base-config` and an explicit
`domain-config` that allows cleartext **only** for `localhost`, `127.0.0.1` and
`10.0.2.2` (local Bot API / dev server). The same patch flips
`allowBackup`/`restoreAnyVersion` (see M1) and drops the unused
`SYSTEM_ALERT_WINDOW` permission (see M4).

---

## Medium

### M1. Plaintext secret storage for the sync token

`AyuConfig` stores everything through `SharedPreferences`
(`preferences.getString("syncServerToken", "")`). On a rooted device, in a
`adb backup`, or after `allowBackup` restore, the token is readable in clear.

Combined with `android:allowBackup="true"` (manifest) and
`restoreAnyVersion="true"`, an attacker with physical ADB access can pull the
token without root.

**Fix:** shipped as ready-to-wire code, not as a patch, because it touches
`AyuConfig`, which pulls in the whole preferences stack:
[`features/AyuSecrets.java`](../features/AyuSecrets.java) — AES-256-GCM with a
non-exportable Android Keystore key, random IV per value, a one-shot
`migrateLegacy(...)` for existing plaintext values, and a hard rule that a
secret is **never** written in plaintext as a fallback. Wiring it is three lines
in `AyuConfig.getSyncServerToken()`/the token setter.
`patches/0002` already sets `android:allowBackup="false"` and
`restoreAnyVersion="false"`, which removes the `adb backup` extraction path.
(The `fullBackupContent`/`dataExtractionRules` variant is documented only: the
exclusion XML differs between API 30 and 31+.)

### M2. `AyuSyncWebSocketClient` accepts unbounded, unvalidated JSON and never verifies TLS identity beyond defaults

`AyuSyncWebSocketClient.onTextReceived` parses whatever the server sends with
Gson into `JsonObject` and dispatches by a string type with no size cap, no
schema check and no `try{}` around the dispatch:

```java
public void onTextReceived(String message) {
    var response = new Gson().fromJson(message, JsonObject.class);
    ...  // callers switch on response.get("type").getAsString()
}
```

A malicious or compromised sync server can push a multi-megabyte frame (heap
pressure), a missing `type` (`NullPointerException` on a socket thread), or a
type the client half-handles.

**Fix:** `patches/0003-sync-frame-hardening.patch`
— cap inbound frames at 1 MiB before parsing and reject frames without the
`type` discriminator, both with a logged reason. Oversized or malformed frames
are dropped rather than force-closing the socket, because the `close()`
overloads differ between builds of the bundled websocket client and the 60 s
read timeout already recovers a dead peer.

### M3. Release CI injects credential values into the build and prints layout info

`.github/workflows/release.yml` writes `API_KEYS` via
`DamianReeves/write-file-action@master` (an unpinned third-party action with
write access to the workspace), and the job runs `df -h` / `apt-get` with
`sudo` around it. Any future step — including a compromised action — can read
`API_KEYS`, `MAPS_KEY` and `CERT_KEY` from the workspace.

**Fix:** `security/PIPELINE-NOTES.md` (unpinnable in an overlay, because the
workflow belongs to upstream CI): use `secrets` only inside a step that
immediately removes the file, pin actions to commit SHAs, set
`permissions: contents: read` on the job, and prefer the official
`gradle/gradle-build-action` with `gradle-encrypted-properties`.

### M4. Debug-only `SYSTEM_ALERT_WINDOW` and `REQUEST_INSTALL_PACKAGES`

Manifest declares `SYSTEM_ALERT_WINDOW` and `REQUEST_INSTALL_PACKAGES` with no
`maxSdkVersion`/product-flavour gate. `SYSTEM_ALERT_WINDOW` is never requested
by UI code (only `Settings.canDrawOverlays` is *checked*, for the PiP and
EmbedBottomSheet paths inherited from Telegram), so the permission is
unnecessary. `REQUEST_INSTALL_PACKAGES` is needed only by the built-in updater.

**Fix:** applied by `patches/0002-transport-and-manifest.patch` — drops
`SYSTEM_ALERT_WINDOW` (the PiP and EmbedBottomSheet paths only *check*
`Settings.canDrawOverlays()`, they never request the permission) and annotates
`REQUEST_INSTALL_PACKAGES` as updater-only. Removing it from a debug flavour
needs a manifest overlay in `TMessagesProj/src/debug/`, which is left as a
one-line follow-up because this tree declares build variants in Gradle.

---

## Low / hardening

### L1. `Authorization` header is sent on every interceptor call, including redirects to other hosts

`AyuInterceptor` blindly attaches the token. OkHttp follows redirects by default
for GETs; a compromised/typo'd `syncServerURL` that redirects to another host
would leak the header.

**Fix:** part of `0001-sync-https-only.patch` — the header is only attached when
the request host matches the configured sync host.

### L2. No certificate pinning for the sync server

Acceptable for a self-hosted sync server, but worth an opt-in pin set: add a
`syncPins` preference (SHA-256) and, when non-empty, install
`OkHttpClient.Builder.certificatePinner(...)` where `AyuSyncController` builds
its client (line ~43). Not shipped as a patch: pinning a server the user may
change is a policy decision, and a wrong pin silently breaks sync.

### L3. `google-services.json` and the pinned Firebase project ship in the tree

`TMessagesProj/google-services.json` binds the fork to `ayugram-c6a31` with a
fixed signing-certificate hash. For a fork, replace it (upstream README says so
too) so crash reports never land in someone else's project.

### L4. No integrity self-check before the in-app updater installs an APK

The updater (exteraGram side) validates an MD5 digest, which is collision-broken
and does not prove *who* signed the file.
[`features/AyuUpdateVerifier.java`](../features/AyuUpdateVerifier.java) does
three checks in order: SHA-256 against a published digest, package-name match,
and **signer-certificate match with the installed build** — the check that
actually prevents a malicious "update". It is self-contained and drops in at the
point where the current code hashes the file (see the class javadoc).

---

## Already fine (checked, no change needed)

* No hard-coded Telegram `APP_ID`/`APP_HASH` in source; they come from
  `BuildConfig` (good — but see `docs/BUILD.md` about the test keys).
* `AyuSyncConfig` never sends messages on the user's behalf; the sync layer is
  read-only w.r.t. Telegram.
* Room database and history storage do not log message bodies at `Log.d`.

## Out of scope

The exteraGram/Telegram core (obfuscated method bodies, `AyuMessageUtils`,
`AyuHistoryHook`) is **not** in this repository in a reviewable form, so it was
not audited here. Treat that part as third-party code you trust or don't.
