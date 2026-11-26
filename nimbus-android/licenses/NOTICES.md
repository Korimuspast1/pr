# Third-party notices

What ships inside a built APK, and under which licence. The full texts of the two font licences
are next to this file (`OFL-Montserrat.txt`, `OFL-IBMPlex.txt`), as is sing-box's own
`LICENSE-sing-box`.

| Component | Licence | Source |
| --- | --- | --- |
| sing-box (the core, linked as `libbox.aar`) | GPL-3.0-or-later | https://github.com/SagerNet/sing-box |
| libbox (the Android bindings of sing-box) | GPL-3.0-or-later | https://github.com/singbox-android/libbox |
| OkHttp | Apache-2.0 | https://github.com/square/okhttp |
| ZXing (`core`, `android-embedded`) | Apache-2.0 | https://github.com/zxing/zxing · https://github.com/journeyapps/zxing-android-embedded |
| AndroidX, Kotlin, Jetpack Compose | Apache-2.0 | https://android.googlesource.com · https://github.com/JetBrains/kotlin |
| Montserrat | SIL Open Font License 1.1 | `OFL-Montserrat.txt` |
| IBM Plex Mono | SIL Open Font License 1.1 | `OFL-IBMPlex.txt` |

Full text of the Apache License 2.0: https://www.apache.org/licenses/LICENSE-2.0.txt
Full text of the GNU GPL 3.0: https://www.gnu.org/licenses/gpl-3.0.txt

## The GPL-3.0 part

Desktop Nimbus starts sing-box as a separate process, which keeps the two programs at arm's
length. A phone has no such separation: libbox is linked into the application and the core runs
inside its process, so **the built APK is a derivative work of sing-box and is distributed under
the GPL-3.0**. Anyone redistributing a build therefore has to pass on the same licence.

The corresponding source of the linked core is the sing-box tree at the tag named by
`SINGBOX_VERSION` in `version.properties`, plus the libbox bindings at the same tag; both
repositories are public and the prebuilt `libbox.aar` is published per release by
`singbox-android/libbox`. The source of this application is this repository. Nothing here needs a
build of the core by hand: `libbox.env.sh` fetches the published AAR.

## Data

Country names by IP come from the provider's own endpoint, which uses
[IP Geolocation by DB-IP](https://db-ip.com) (CC BY 4.0). No geolocation database is bundled, and
no IP leaves the phone except to the endpoints named in `engine/ExitIp.kt`.

Rule sets (`geoip-*.srs`, `geosite-*.srs`) are downloaded by the core at run time from
`SagerNet/sing-geoip` and `SagerNet/sing-geosite`, which publish them under the MIT licence.
