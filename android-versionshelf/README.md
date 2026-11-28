# VersionShelf for Android

A compact black-and-white Android app for checking versions of installed **launchable** apps and installing an explicitly selected APK from a trusted catalogue. It is designed for private distribution, not for silently managing a device.

![No tracking](https://img.shields.io/badge/network-HTTPS%20catalogues%20only-111111?style=flat-square) ![No broad package permission](https://img.shields.io/badge/package%20visibility-launcher%20apps%20only-111111?style=flat-square)

## What the app does

- scans installed apps which have a launcher icon (for example, Discord, SoundCloud or Roblox if installed);
- shows the installed name, package ID, version name and version code;
- shows every version published in the selected catalogue, marking it as **older**, **installed** or **newer**;
- downloads only from HTTPS, validates the APK SHA-256, reads the archive package name and signing certificate, and refuses a differently signed update;
- invokes Android's **own** package installer after verification. It never installs, downgrades, grants permissions, or enables “unknown sources” silently;
- includes official F-Droid as the safe default and supports a private HTTPS catalogue for APKs that the operator owns or is authorised to distribute.

The UI is Russian and intentionally monochrome: black ground, off-white type and neutral grey panels, with no neon or decorative effects.

## Important boundaries

This project deliberately **does not** scrape random APK sites, bypass Google Play/Play Protect, fake reputation, forge an app certificate, or promise that any arbitrary proprietary app/version can be installed.

An Android signing certificate identifies who signed *this* app; it does not make software “not a virus” or guarantee Play approval. Google Play applies its own policies and scans. For legitimate distribution, use a stable package ID, your own protected signing key, accurate data-safety/policy declarations, a privacy policy where needed, and only APK sources you are entitled to provide.

The default F-Droid source contains free/open-source apps, so proprietary apps such as Roblox, Discord and SoundCloud will normally show **“not in selected catalogue.”** Add only authorised APKs to a private catalogue if you need to manage those apps.

Android itself may reject an older version (a downgrade), an APK with a different signing key, or a package incompatible with the device. VersionShelf still lets the user inspect and verify such catalogue entries; it cannot override Android's security model.

To avoid the restricted `QUERY_ALL_PACKAGES` permission, VersionShelf scans normal launchable apps only. Hidden/system packages are intentionally excluded. This scope covers ordinary user-facing apps while being suitable for a private utility and much less invasive.

## Sources

### Default: F-Droid

On first start the app is configured for the official HTTPS index:

```text
https://f-droid.org/repo/index-v2.json
```

Tap **«Обновить каталог»** after the device scan. The index can be several megabytes. VersionShelf only keeps entries corresponding to apps installed on the phone.

### Private catalogue

Open **«Настройки» → «Частный каталог»** and enter an HTTPS URL to a JSON document matching this schema. Every `sha256` is mandatory and is lowercase or uppercase hexadecimal. `signingCertificateSha256` is strongly recommended; it is the SHA-256 of the APK signing certificate.

```json
{
  "schema": 1,
  "apps": [
    {
      "packageId": "org.example.player",
      "versions": [
        {
          "versionName": "2.4.0",
          "versionCode": 20400,
          "releasedAt": "2026-09-29",
          "apkUrl": "https://downloads.example.org/player-2.4.0.apk",
          "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
          "signingCertificateSha256": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
        }
      ]
    }
  ]
}
```

The app rejects HTTP, credentials embedded in a URL, more than three redirects, malformed checksums, wrong package IDs, mismatched catalogue certificates, and APKs signed differently from the installed app. A private catalogue should itself be served over HTTPS by a domain you control. Keep the catalogue and APKs immutable/versioned and publish checksums from your release pipeline.

## Permissions and privacy

| Permission / visibility | Reason |
| --- | --- |
| `INTERNET` | Fetch a catalogue and an APK only after user action. |
| `REQUEST_INSTALL_PACKAGES` | Pass an already verified APK to Android's system installer. Android displays the confirmation and may require its per-app “install unknown apps” setting. |
| Launcher intent query | Locate ordinary applications with a launcher icon. No `QUERY_ALL_PACKAGES` permission is used. |

No analytics, ads, account login, broad storage permission, accessibility service, background receiver, or server backend is included. Downloaded verified APKs are kept in the app-specific Downloads directory until Android/the user clears them.

## Build

Requirements:

- JDK 17;
- Android SDK Platform 35 and Build Tools 35.x;
- an internet connection for Gradle's first dependency resolution, or a pre-populated Gradle cache.

```bash
cd android-versionshelf
./gradlew :app:assembleDebug
```

The debug APK is output at:

```text
app/build/outputs/apk/debug/app-debug.apk
```

### Release signing

Never commit a private key. Create a key owned by the distributor:

```bash
./tools/create-release-key.sh
```

Then use the command printed by that script, passing signing values through a secure CI secret store or shell. The keystore directory and `*.jks` / `*.keystore` files are ignored by Git. On Google Play, create/retain the required Play Console account and complete the current policy, data-safety and package-visibility declarations yourself; this repository cannot create those external legal/account artefacts or bypass review.

## Development notes

The app has no third-party runtime dependency and uses platform views rather than a UI framework. Gradle downloads only the Android Gradle Plugin and Kotlin plugin at build time. The included configuration targets API 35, has `minSdk 26`, and uses application ID `app.versionshelf`. Change that ID **before first public distribution** if it is not your permanent product ID.
