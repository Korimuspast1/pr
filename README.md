# pr — Custom Profile plugin + AyuGram4A security overlay

This repository holds two deliverables.

## 1. [`custom-profile/`](custom-profile/) — exteraGram plugin **2.0.0**

A local-only visual profile editor for exteraGram (Elyx plugin). Rebuilt in this
round: read-only patch mode by default, input hardening (control/bidi/zero-width
stripping, username rules, clamping), an on-by-default `·preview` honesty marker
on patched names, presets with sanitised JSON import, a bundled SHA-256 integrity
manifest with an in-app diagnostics check, and a reproducible build.

* Install: [`release/custom-profile.elyx`](release/custom-profile.elyx)
* Verify: `release/custom-profile.elyx.sha256`
* Docs: [`custom-profile/README.md`](custom-profile/README.md)
* Tests: `python3 -m unittest discover -s custom-profile/tests -v` (50 passing)
* Rebuild: `python3 custom-profile/tools/build.py --write-sums`
* Preset designer: `python3 custom-profile/tools/preset.py --preset full --name "Alice"`

Requires exteraGram **12.5.1+**, SDK **1.4.5.3+**.

## 2. [`ayuox4gg/`](ayuox4gg/) — AyuGram4A security + modernisation overlay

Security review of `AyuGram/AyuGram4A` `rewrite` with **three verified patches**
(HTTPS-only sync, no blanket cleartext, encrypted token storage plan, WebSocket
frame limits, least-privilege manifest), a build-modernisation plan
(AGP 8 / SDK 35 / NDK 26), two drop-in Java hardening classes, and three
dependency-free tools:

```bash
python3 ayuox4gg/tools/security_audit.py --dir <ayu-tree>/TMessagesProj/src/main
python3 ayuox4gg/tools/forensics.py app.apk --expect-cert <sha256>
python3 ayuox4gg/tools/javaparse.py path/to/File.java
```

* Start here: [`ayuox4gg/README.md`](ayuox4gg/README.md)
* In Russian: [`ayuox4gg/docs/EXPLAINED_RU.md`](ayuox4gg/docs/EXPLAINED_RU.md)
* Build matrix / API keys: [`ayuox4gg/docs/BUILD.md`](ayuox4gg/docs/BUILD.md)

## Test everything

```bash
python3 -m unittest discover -s custom-profile/tests -v
python3 -m unittest discover -s ayuox4gg/tests -v
python3 custom-profile/tools/build.py --check
```

Both suites are standard-library only; they need no Android SDK, no JDK and no
network.

## License

`custom-profile/` is MIT (see its `LICENSE`). The `ayuox4gg/` patches and tools
are provided under the same MIT terms; the AyuGram4A files they modify remain
GPL-2.0 as upstream.
