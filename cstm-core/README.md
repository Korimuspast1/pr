# CSTM Core — `.plugin` loader for `.cstm` presets

`CSTM Core` is a **single Python source plugin**. The installable release file is [`../release/cstm-core.plugin`](../release/cstm-core.plugin); its contents are identical to [`cstm_core.py`](cstm_core.py).

It adds support for the `.cstm` extension in exteraGram and integrates with the `Custom Profile` plugin.

## Safe design

A CSTM is **strict JSON data**, not a Python script and not a plugin archive. CSTM Core:

- registers a tap handler for `.cstm` files sent in Telegram;
- optionally downloads a preset only from the exact HTTPS URL pasted by the user;
- accepts only `custom-profile-cstm` version `1` JSON;
- has a 64 KB file limit, validates every key and every value, and rejects unknown fields;
- writes only approved settings for the `custom_profile` plugin;
- never executes a preset, installs a plugin, changes a Telegram account, or spoofs a wallet balance.

## Install order

1. Install [`Custom Profile`](../release/custom-profile.elyx).
2. Install [`CSTM Core`](../release/cstm-core.plugin) by sending it to Saved Messages and tapping it in exteraGram.
3. Enable both plugins.
4. Download a `.cstm` from a chat and tap it, **or** paste a direct HTTPS `.cstm` link into CSTM Core and press **Download and apply preset**.
5. Open Custom Profile and press **Apply to the open profile**.

## Example preset

[`neon-profile.cstm`](examples/neon-profile.cstm) is a valid preset. It is also published as [`../release/neon-profile.cstm`](../release/neon-profile.cstm).

```json
{
  "format": "custom-profile-cstm",
  "version": 1,
  "name": "Preset name",
  "profile": {
    "enabled": true,
    "verified": true,
    "premium": true,
    "stars_enabled": true,
    "stars_amount": 2500000,
    "stars_level": 12
  }
}
```

The only allowed `profile` fields are:

- booleans: `enabled`, `verified`, `premium`, `emoji_status_enabled`, `stars_enabled`, `name_color_enabled`, `profile_color_enabled`;
- numbers: `scope`, `emoji_status_id`, `stars_amount`, `stars_level`, `name_color_index`, `profile_color_index`;
- text: `display_name`, `display_username`, `display_bio`.

All other fields are deliberately rejected. This keeps `.cstm` distributable without turning it into an arbitrary-code loader.

## Build

```bash
python3 cstm-core/tools/build.py
python3 cstm-core/tools/build.py --check
```
