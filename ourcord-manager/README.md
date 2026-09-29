# OurCord Manager

OurCord Manager is an Android 9+ installer and patch manager for a customized Discord client.
It downloads the selected Discord build on the device, patches its split APKs with LSPatch,
injects the configured React Native mod bundle, signs the result locally, and opens Android's
package installer. The repository does **not** redistribute Discord APK files.

## Features

- Download and patch supported Discord versions on-device
- Separate package name (`app.ourcord`) so it can coexist with stock Discord
- Local APK signing and Android package-installer integration
- Plugins/themes through the injected mod runtime
- Update checks, patch logs, custom icon/color, and Shizuku installation option
- English and Russian UI resources

## Build

Requirements: JDK 17 and Android SDK Platform 34.

```bash
./gradlew :app:assembleRelease
```

Output: `app/build/outputs/apk/release/ourcord-manager-2.0.0.apk`.

## Legal and security notice

Discord is a third-party proprietary service. Using a modified client may violate Discord's
Terms of Service and can put an account at risk. Review source code and use a test account.
Never install plugins from untrusted sources. OurCord Manager does not include a Discord APK.

## Upstream and license

This project is a branded derivative of `revenge-mod/revenge-manager`, itself derived from
Bunny/Vendetta Manager, and retains the Open Software License 3.0 in [LICENSE](LICENSE).
The manager downloads only the OurCord loader published by this repository. The loader embeds
and updates the repository's own `ourcord-runtime` bundle. LSPatch and third-party libraries
retain their respective licenses; see the in-app Libraries screen.
