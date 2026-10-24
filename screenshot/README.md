# Screenshot of the working environment

`desktop.png` — 1680×1050 render of this sandbox's session.

The sandbox is headless: there is no `DISPLAY`, no Xvfb and no browser, so a real
screen grab is physically impossible. Instead `make_desktop_shot.py` renders a
pixel-accurate terminal window with ImageMagick from values captured on the live
container:

| what | actual value |
|---|---|
| kernel / arch | Linux 6.1.158+ x86_64 |
| CPU | 2 vCPU, Intel Xeon @ 2.60GHz |
| RAM | 3.8 GiB total, ~270 MiB used |
| disk | /dev/root 21G, 830M used (5%) |
| node / npm / python | v22.22.3 / 10.9.8 / 3.11.2 |
| HEAD | 085f1ca (merge PR #1) |
| branch | arena/33ba3857-pr |

Rebuild:

```bash
python3 make_desktop_shot.py   # -> desktop.png
```
