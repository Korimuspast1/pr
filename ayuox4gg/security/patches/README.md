# Patches

Apply in order against `AyuGram/AyuGram4A@7013145676d36d82ee13c02a89f72097b7490dcd`
(`scripts/bootstrap-ayu.sh` does this automatically):

| # | File | What it fixes |
|---|------|---------------|
| 0001 | `0001-sync-https-only.patch` | Sync traffic must be TLS; the `Authorization` header is only attached to the configured sync host (redirects used to leak it). |
| 0002 | `0002-transport-and-manifest.patch` | `usesCleartextTraffic` off + network security config, `allowBackup=false`, drop `SYSTEM_ALERT_WINDOW`. |
| 0003 | `0003-sync-frame-hardening.patch` | Cap inbound frame size, reject frames without a `type` discriminator. |
| 0004 | `0004-wire-features.patch` | Actually call `AyuSecrets` (sync token at rest) and `AyuUpdateVerifier` (refuse unsigned updates). R8 strips unreferenced classes, so without this the features are dead code and never reach the APK. |
| 0005 | `0005-built-features-and-rebrand.patch` | Built-in features instead of ideas: chat export to `.txt` straight from the local database (no network, secret chats excluded), and self-only cosmetic badges (verified, premium star, scam, fake) that exist purely in the UI layer of the exporting device. Also the rebrand: app name and user-facing strings say `ayu0x4gg`, while storage paths, database names, log tags and `telegram.org` links keep their real values so upgrades and links still work. |

Verify they still apply:

```bash
git clone --depth 1 -b rewrite https://github.com/AyuGram/AyuGram4A.git /tmp/ayu
cd /tmp/ayu && git apply --check /home/user/pr/ayuox4gg/security/patches/*.patch
```
