# Custom Profile for exteraGram

**Custom Profile** is an installable [Elyx](https://plugins.exteragram.app/docs/elyx) plugin for exteraGram. It lets you preview a decorated version of **your own signed-in profile** in the local client.

> **Local preview, not an account unlock.** The plugin only changes live model objects inside the exteraGram process. It does not make a Telegram API request, does not write to your Telegram account, and other people cannot see the changes. It creates no real Premium subscription, verification, or Stars. Do not use a local screenshot as proof for another person.

## Included controls

| Area | Controls |
| --- | --- |
| **Badges** | Native `Verified` check, Telegram Premium star, and custom emoji status by a known custom-emoji document ID |
| **Stars** | Native profile star-rating crown, arbitrary local star amount, and crown level |
| **Profile text** | Local display name, local `@username`, and profile bio |
| **Colour** | Name and profile colour through Telegram's built-in palette index |
| **Scope** | Active account only or every account signed in on the device |
| **Safety** | Master switch, **Apply** action, in-memory restore on disabling/unloading, and reset to safe defaults |

### About “Major verified” / arbitrary badges

Telegram does not expose a general `major verified` or arbitrary-badge field for regular users. The official fields that current Android clients render for a personal user are the standard `verified` check, `premium`, emoji status, peer colours, and the profile star rating. This plugin exposes those local visual fields instead of forging server-issued credentials.

## Installation

1. Download [`../release/custom-profile.elyx`](../release/custom-profile.elyx) from this repository.
2. In exteraGram, open **Settings → Plugins**, use **Import plugin**, select `custom-profile.elyx`, and enable **Custom Profile**.
3. Open the plugin settings and choose your visual options.
4. Tap **Apply to the open profile**. If your profile page was already open, close and reopen it.

The archive uses the current structured Elyx format and requires:

- exteraGram **12.5.1+**
- Plugin SDK **1.4.4.3+**

## Stars: what is and is not changed

The Stars control changes the **profile star rating** used by recent Telegram Android clients (the crown near a profile name and the rating sheet). It intentionally does **not** spoof a spendable Telegram Stars wallet balance. A spendable balance is server-authoritative and must not be used for a visual-only preview.

## Custom emoji status

For an emoji status, enter the numeric **document ID** of a custom emoji the client has already loaded. An invalid or unknown ID may render as nothing. The plugin never uploads or activates the status with Telegram.

## Development

The source archive is self-contained and has no Python dependencies.

```bash
# Syntax check
python3 -m py_compile main.py

# Build/rebuild the distributable archive from the repository root
python3 custom-profile/tools/build.py

# Validate the committed archive
python3 custom-profile/tools/build.py --check
```

The build tool verifies that the resulting archive has `refmap.yml` at its root—the layout required by Elyx.

## License

[MIT](LICENSE)
