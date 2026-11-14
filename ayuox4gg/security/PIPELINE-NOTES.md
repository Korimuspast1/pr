# Release pipeline hardening (finding M3)

The build workflow lives upstream in `.github/workflows/release.yml`. An overlay
cannot patch someone else's CI retroactively, so this file lists the exact
changes to make in a fork that wants reproducible, trustworthy releases.

## What the upstream workflow does today

```yaml
on: push
jobs:
  build:
    runs-on: self-hosted
    steps:
      - uses: actions/checkout@v3
        with: {submodules: recursive, fetch-depth: 0, token: "${{ secrets.GH_TOKEN }}"}
      - uses: DamianReeves/write-file-action@master     # writes ./API_KEYS
        with:
          contents: |
            APP_ID = 6
            APP_HASH = "eb06d4abfb49dc3eeb1aeb98ae0f581e"
            MAPS_V2_API = ${{ secrets.MAPS_KEY }}
            SIGNING_KEY_PASSWORD = ${{ secrets.CERT_KEY }}
            ...
```

Problems, in order of severity:

1. **`@master` on a third-party action** with the repository checked out and
   secrets in the environment. A force-push to that action's default branch runs
   arbitrary code with `GH_TOKEN` and `MAPS_KEY` in scope. Pin every action to a
   commit SHA.
2. **The signing password and the Telegram API hash are written into the
   workspace** and never removed. Any later step — including a compromised
   dependency, a `gradle build` plugin or a poisoned cache — can exfiltrate them.
3. **`secrets.GH_TOKEN` is passed to `actions/checkout`** even though the job
   only needs read access. Use the default `GITHUB_TOKEN` with
   `permissions: contents: read`.
4. **`on: push` with no branch filter** means every branch push triggers a
   signed build on a self-hosted runner.
5. **No artefact integrity**: the produced APK is uploaded without a published
   SHA-256, so users cannot check what they downloaded (see
   `tools/forensics.py` for the consumer side).

## Recommended replacement

```yaml
name: Build release APK
on:
  push:
    tags: ['v*']            # only tags build releases
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    runs-on: self-hosted
    timeout-minutes: 90
    env:
      CCACHE_MAXSIZE: 10G
    steps:
      - uses: actions/checkout@v4          # or pin: actions/checkout@<sha> # v4.x.y
        with:
          submodules: recursive
          fetch-depth: 0
          persist-credentials: false

      - name: Materialise API keys (deleted in the same step)
        env:
          APP_ID: ${{ secrets.APP_ID }}
          APP_HASH: ${{ secrets.APP_HASH }}
          MAPS_KEY: ${{ secrets.MAPS_KEY }}
          CERT_KEY: ${{ secrets.CERT_KEY }}
        run: |
          umask 077
          trap 'rm -f API_KEYS' EXIT
          cat > API_KEYS <<EOF
          APP_ID = ${APP_ID}
          APP_HASH = "${APP_HASH}"
          MAPS_V2_API = ${MAPS_KEY}
          SIGNING_KEY_PASSWORD = ${CERT_KEY}
          SIGNING_KEY_ALIAS = ${CERT_KEY}
          SIGNING_KEY_STORE_PASSWORD = ${CERT_KEY}
          EOF

      - uses: actions/setup-java@v4
        with: {distribution: temurin, java-version: '17'}

      - run: ./gradlew --no-daemon assembleAfatRelease

      - name: Publish checksums
        run: |
          cd TMessagesProj/build/outputs/apk/afat/release
          sha256sum *.apk > SHA256SUMS
          sha256sum -c SHA256SUMS
          cat SHA256SUMS
```

And, once, on a trusted machine:

```bash
python3 ayuox4gg/tools/forensics.py app-release.apk --json > apk-forensics.json
# publish the certificate hash next to the release:
#   "signed by SHA-256: <cert sha256>"
```

Users then verify with:

```bash
sha256sum -c SHA256SUMS
python3 ayuox4gg/tools/forensics.py app-release.apk --expect-cert <cert sha256>
```

## Secrets inventory

| Secret | Where it is used | Rotation if leaked |
| --- | --- | --- |
| `CERT_KEY` (keystore password) | APK signing | **re-key the app** — all users must reinstall |
| `MAPS_KEY` | Google Maps SDK | rotate in Google Cloud console |
| `APP_HASH` | Telegram API | regenerate at my.telegram.org |
| `GH_TOKEN` | checkout | revoke the PAT |

The keystore itself (`TMessagesProj/config/extera.jks` in the tree) plus its
password is the single most damaging secret here: whoever holds both can ship an
"update" that Android will accept as genuine. Keep it out of the repository in a
fork, and sign with a private keystore.
