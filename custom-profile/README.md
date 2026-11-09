# Custom Profile for exteraGram

**Custom Profile** is an installable [Elyx](https://plugins.exteragram.app/docs/elyx) plugin for exteraGram. It renders a decorated preview of **your own signed-in profile** locally, inside the client process.

> **Local preview, not an account unlock.** The plugin only edits Java model objects that already live inside the exteraGram process. It makes **no Telegram API requests**, writes **no account settings**, and changes nothing for other people. It cannot create a real Premium subscription, verification or Stars. Never present a local screenshot as proof to somebody else.

---

## What's new in 2.0.0

| Area | Change |
| --- | --- |
| **Safety** | Read-only patch mode is now the default. The old behaviour (mutating `MessagesController.putUser`/`getUser` cache objects) is opt-in under *Cache patch mode*. |
| **Input hardening** | Every text field is length-limited, stripped of C0/C1 control characters, zero-width and bidi-override characters (used for name spoofing) and re-validated before it is written into a TL object. |
| **Honesty marker** | A `·preview` suffix is appended to locally patched names by default so a screenshot can't be mistaken for a real profile. Fully configurable. |
| **Integrity check** | The archive ships a generated SHA-256 manifest of its own modules. If a module was edited or replaced inside a downloaded archive, the plugin says so in *Diagnostics* instead of running silently. |
| **Presets** | One-tap presets, copy/paste a preset as JSON. Imported blobs are sanitised: unknown keys are dropped and every value is clamped. |
| **Validation** | Usernames are checked against Telegram's real rules (5–32 chars, leading letter, `A-Za-z0-9_`). Colours are limited to the 7 palette indices, crown level to 1–99, stars to the signed-64-bit range. |
| **Structure** | Code split into `main.py` / `core.py` / `verify.py`; validation and sanitising live in pure Python and are covered by 27 unit tests. |
| **Build** | Reproducible archives (fixed timestamps), AST check against Python 3.11 syntax, `--write-sums` for a published `sha256`, `--no-manifest` for debugging. |

## Included controls

| Area | Controls |
| --- | --- |
| **Badges** | Native `Verified` check, Telegram Premium star, custom emoji status by a known custom-emoji document ID |
| **Stars** | Native profile star-rating crown, arbitrary local star amount, and crown level |
| **Profile text** | Local display name, local `@username`, profile bio |
| **Colour** | Name and profile colour through Telegram's built-in 7-colour palette |
| **Honesty** | On-by-default preview marker with configurable text |
| **Presets** | Minimal / full preview / everything off; copy & paste presets as JSON |
| **Scope** | Active account only or every account signed in on the device |
| **Safety** | Master switch, read-only cache mode, **Apply**, in-memory restore on disabling/unloading, restore-now action, reset to safe defaults |
| **Diagnostics** | Archive integrity check, hook count, active patch mode |

### About “major verified” / arbitrary badges

Telegram does not expose a general `major verified` or arbitrary-badge field for regular users. The official fields that current Android clients render for a personal user are the standard `verified` check, `premium`, emoji status, peer colours and the profile star rating. The plugin exposes those local visual fields instead of forging server-issued credentials.

## Installation

1. Download [`../release/custom-profile.elyx`](../release/custom-profile.elyx) from this repository.
2. In exteraGram, open **Settings → Plugins**, use **Import plugin**, select `custom-profile.elyx`, and enable **Custom Profile**.
3. Open the plugin settings and choose your visual options.
4. Tap **Apply to the open profile**. If your profile page was already open, close and reopen it.

Requires exteraGram **12.5.1+** and Plugin SDK **1.4.5.3+**.

### Verifying your download

Each release publishes `release/custom-profile.elyx.sha256`. Check it before installing:

```bash
sha256sum -c custom-profile.elyx.sha256     # Linux
shasum -a 256 -c custom-profile.elyx.sha256 # macOS
certutil -hashfile custom-profile.elyx SHA256  # Windows
```

After installation, **Settings → Plugins → Custom Profile → Diagnostics** reports whether every packaged module still matches the bundled manifest.

## Stars: what is and is not changed

The Stars control changes the **profile star rating** rendered by recent Telegram Android clients (the crown near a profile name and the rating sheet). It intentionally does **not** spoof a spendable Telegram Stars wallet balance: a balance is server-authoritative and must not be faked by a visual-only preview.

## Custom emoji status

For an emoji status, enter the numeric **document ID** of a custom emoji the client has already loaded. An invalid or unknown ID may render as nothing. The plugin never uploads or activates the status with Telegram.

## Development

The plugin has no third-party dependencies and keeps its pure-Python logic importable outside the client.

```bash
# Unit tests (standard library only)
python3 -m unittest discover -s custom-profile/tests -v

# Syntax + Python 3.11 syntax check, rebuild and validate the archive
python3 custom-profile/tools/build.py --write-sums

# Validate the committed archive without rebuilding
python3 custom-profile/tools/build.py --check
```

The build tool verifies that the resulting archive has `refmap.yml` at its root — the layout Elyx requires — and that the embedded manifest matches the embedded modules.

### Layout

```
custom-profile/
├── main.py            # BasePlugin: settings UI, hooks, patching
├── core.py            # pure-Python defaults, clamping and sanitising
├── verify.py          # archive integrity checks
├── metainfo.yml       # plugin metadata
├── refmap.yml         # Elyx entry points
├── strings/           # en / ru localisation
├── tools/build.py     # reproducible builder + validator
└── tests/test_core.py # unit tests
```

## License

[MIT](LICENSE)
