# Nimbus for Android

**English** · [Русский](README.md)

The desktop [Nimbus](https://github.com/Zalchik/nimbus) ported to a phone: the same subscription,
link and balancer client, on the [sing-box](https://github.com/SagerNet/sing-box) core running
inside the app through Android's VPN service.

Subscriptions, settings and profiles are the same `settings.json` and `profiles.json` files the
desktop writes, so a data folder copied from a computer works as it is.

## Building

JDK 17 and the Android SDK are enough — no Go and no NDK, because the core is fetched prebuilt.

```sh
cd nimbus-android
. ./libbox.env.sh                      # downloads app/libs/libbox.aar (sing-box 1.14.4)
./gradlew :app:assembleRelease
```

APKs land in `app/build/outputs/apk/release/`: one per ABI (`arm64-v8a`, `armeabi-v7a`, `x86_64`)
plus a `universal` one. The release build is signed with the debug key so a CI artifact installs
straight away; add a `signingConfig` in `app/build.gradle.kts` for your own.

GitHub Actions builds the same artifacts: `.github/workflows/nimbus-android.yml`
(tests → APK per ABI → `SHA256SUMS.txt`).

## What it does

- **Subscriptions**: traffic, expiry, the provider's announcements, HWID, a user agent of your
  own, automatic updates on the panel's interval or yours. Full Xray JSON configs are translated
  to sing-box and run.
- **Links**: VLESS (Reality, Vision), VMess, Trojan, Shadowsocks (SIP002, 2022, legacy),
  Hysteria2 (hop ports and obfs included), SOCKS5, WireGuard.
- **Encrypted Happ links** (`happ://crypt…/…`, `crypt://…`): decrypted on the device — Happ's RSA
  key table and ChaCha20-Poly1305 (RFC 8439) are implemented in `engine/Happ.kt`, with no crypto
  dependency.
- **Deep links** from other clients: `nimbus://`, `happ://`, `v2rayn://`, `v2rayng://`, `clash://`,
  `clashmeta://`, `sing-box://`, plus Android's share-sheet text and opening a config file.
- **Balancers**: servers from any subscriptions and groups, either "keep the node" (switch only on
  a clear gain) or "always the fastest", checks from 10 s apart, a notification when the node
  changes. Inside sing-box this is a `urltest` group, so switching doesn't reconnect anything.
- **Routing**: the provider's own rules, "Russian sites direct", ad blocking, local-address
  bypass, profiles of your own by app and site, and a global per-app filter.
- **DNS**: remote and direct servers (DoH/DoT/QUIC/UDP/TCP), fakeip, domain sniffing out of the
  traffic (TLS/SNI, HTTP Host, QUIC), domain strategies.
- **Diagnostics**: real server latency, your exit IP with its country, sing-box's log, the config
  being built right now, and sending the log as a file.
- **Sharing**: as a link, a QR code, a JSON config, or a whole subscription.
- **Also**: a quick-settings tile, start after boot, auto-connect, dark/light/system theme,
  Russian and English, in-app updates from GitHub releases verified by SHA-256.

## How a phone differs from a computer

| Desktop | Android |
| --- | --- |
| Xray-core and sing-box, two processes | sing-box only, in-process (libbox) |
| Proxy and TUN modes | TUN is the real one; "proxy" stays for settings compatibility |
| Presets by program (`chrome.exe`) | profiles by package name (`package_name`) |
| Its own kill switch | Android's always-on VPN + "block connections without VPN" |
| `geoip.dat` / `geosite.dat` | sing-box rule sets, downloaded (`*.srs`) |
| WireGuard outbound | WireGuard as an `endpoint` (the outbound was removed from sing-box) |
| XHTTP / KCP transports | unsupported: sing-box has neither, and Nimbus says so before you connect |
| Updates by replacing files | an APK from a release, installed by the system |

Protocols and transports the mobile core doesn't have are never dropped silently: the link is
flagged and the app explains why the server won't run (`engine/Links.kt`, `ParsedLink.unsupported`).

## Layout

```
app/src/main/java/com/zalchik/nimbus/
├── util/       JSON, base64, URL parsing — portable, no android.jar
├── i18n/       Russian source strings + the English table (the language is Compose state)
├── data/       settings.json, profiles.json, paths, Happ's key table
├── engine/     links, Xray configs, sing-box config building, balancers, subscriptions,
│               deep links, sharing, latency, exit IP, QR, updates
├── bg/         libbox: the VPN service, command server/client, notification, tile, boot start
└── ui/         Compose: screens, app state, theme, widgets
```

`engine/` and `data/` know nothing about Android — which is why they are testable on a JVM and why
they match the desktop modules key for key. Everything platform-specific lives in `bg/` and `ui/`.

The tunnel's path: `bg/NimbusVpnService` (Android's VPN) → `bg/BoxService` (starts the core,
implements `CommandServerHandler`) → `bg/ServiceCommandClient` (status, groups, delays, log) →
`bg/ServiceController` (the Compose state the screens read).

## Tests

```sh
cd nimbus-android
./gradlew :app:testReleaseUnitTest
```

The tests mirror the desktop's `tests/test_core_logic.py` on the same fixtures: the RFC 8439
ChaCha20-Poly1305 vector, real encrypted Happ links, parsing every kind of share link, config
building (TUN, DNS, profiles, balancers, provider configs), translating an Xray config to
sing-box, subscription body formats, deep links, QR codes, updates, `settings.json` /
`profiles.json` compatibility, and the completeness of the English translations.

One test (`GeneratedConfigDump`) writes every config it builds to `app/build/generated-configs/`,
which CI keeps as an artifact: a change in sing-box's schema then shows up as a diff instead of a
guess.

## Updating the core

`version.properties` pins the sing-box version and the URL of its prebuilt `libbox.aar`, published
per release by [singbox-android/libbox](https://github.com/singbox-android/libbox/releases). To
move to another version:

1. change `SINGBOX_VERSION` and `LIBBOX_AAR_URL` in `version.properties`;
2. delete `app/libs/libbox.aar` and run `. ./libbox.env.sh` again;
3. check the API: gomobile silently skips method signatures it can't bind, so look at the
   generated Java classes in the AAR's `libbox-sources.jar` before calling anything new.

`data/HappKeys.kt` and `i18n/LocaleEn.kt` are generated from the desktop repository:
`python3 tools/generate_kotlin_data.py /path/to/nimbus` (Happ's key table and the English texts,
including the ones this port added). Icons: `python3 tools/make_icons.py`.

## Licenses

- sing-box: GPL-3.0 (text in `licenses/LICENSE-sing-box`). On a phone the core is linked into the
  app through libbox rather than run as a separate process as on a computer, so the built APK is
  distributed under the GPL-3.0.
- Montserrat and IBM Plex Mono fonts: SIL Open Font License (`licenses/OFL-*.txt`).
- OkHttp and ZXing: Apache-2.0.
- Country by IP: [IP Geolocation by DB-IP](https://db-ip.com) (CC BY 4.0).
