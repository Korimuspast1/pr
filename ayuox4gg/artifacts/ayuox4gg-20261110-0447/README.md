# ayuox4gg-20261110-0447

Built from `AyuGram/AyuGram4A@7013145676d36d82ee13c02a89f72097b7490dcd` with the ayuox4gg overlay.

| | |
|---|---|
| apk | `ayuGram-arm64-v8a-10112026.apk` |
| size | 43345883 bytes |
| sha256 | `ae60b8165028e66082f2cd804c5af5524fb3ad2002e723792482318f1dd1650f` |
| parts | 6 |
| flavor | arm64 |
| generated signing key | yes — see `release.jks` / `password.txt` |

## Install from here

```bash
python3 ayuox4gg/tools/unpack_apk.py ayuox4gg-20261110-0447 --out ~/ayu.apk
python3 ayuox4gg/tools/forensics.py ~/ayu.apk --expect-sha256 ae60b8165028e66082f2cd804c5af5524fb3ad2002e723792482318f1dd1650f
```

Security patches 0001-0003 are applied; the upstream public keystore is
replaced (finding X1).

This build generated a fresh signing key. Store `release.jks` and
`password.txt` as repository secrets (AYUOX4GG_KEYSTORE_B64 / _PASSWORD /
_KEY_ALIAS) so the next build is signed by the same identity and installs
over this one.
