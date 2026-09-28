#!/usr/bin/env python3
"""
limewire_download.py
====================

Download a file from a LimeWire share link using a headless browser.

LimeWire share links look like:

    https://limewire.com/d/<id>#<key>

The part after "#" is a client-side decryption key. It is never sent to the
server, so a plain HTTP GET only ever returns ciphertext or the HTML shell.
The file has to be fetched and decrypted by the page's own JavaScript, which
means a real browser is required.

Usage:
    python3 tools/limewire_download.py "https://limewire.com/d/xxx#yyy" -o out.zip

Diagnostics are always written next to the output:
    <out>.ui.txt           inventory of every clickable element, per pass
    <out>.page.html        final DOM (truncated)
    <out>.step*.png        screenshots at each stage
    <out>.network.log      every response + console message
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import traceback
from typing import Callable, List, Optional

try:
    from playwright.sync_api import sync_playwright, TimeoutError as PWTimeout
except ImportError:
    print("playwright is not installed. run:", file=sys.stderr)
    print("  pip install playwright && playwright install --with-deps chromium", file=sys.stderr)
    sys.exit(2)


CONSENT_TEXTS = [
    "Accept all", "Accept All", "Accept all cookies", "Allow all", "Allow",
    "Accept", "I agree", "Agree", "Got it", "Continue", "OK", "Okay",
    "Close", "Consent", "Zustimmen", "Alle akzeptieren", "No thanks",
    "Reject all", "Dismiss", "Skip", "Skip ad",
]

DOWNLOAD_RE = re.compile(r"^\s*(download(\s+(all|file|files|now))?)\s*$", re.I)

MAX_HTML = 400_000


def log(msg: str) -> None:
    print(f"[limewire] {msg}", flush=True)


# ---------------------------------------------------------------------------
# diagnostics
# ---------------------------------------------------------------------------

class Diag:
    def __init__(self, out: str):
        self.out = out
        self.net: List[str] = []
        self.ui: List[str] = []
        self.shot = 0
        os.makedirs(os.path.dirname(os.path.abspath(out)) or ".", exist_ok=True)

    def screenshot(self, page, tag: str) -> None:
        self.shot += 1
        path = f"{self.out}.step{self.shot:02d}_{tag}.png"
        try:
            page.screenshot(path=path, full_page=False)
            log(f"screenshot -> {os.path.basename(path)}")
        except Exception as e:
            log(f"screenshot failed ({tag}): {e}")

    def dump_ui(self, page, tag: str) -> None:
        """Record every plausible control so we can see what the page offers."""
        self.ui.append(f"\n########## UI INVENTORY: {tag} ##########")
        try:
            self.ui.append(f"url={page.url}")
            self.ui.append(f"title={page.title()!r}")
        except Exception:
            pass
        js = """
        () => {
          const out = [];
          const sel = 'button, a, [role=button], input[type=button], input[type=submit], input[type=checkbox], [class*=download i], [data-testid]';
          document.querySelectorAll(sel).forEach((el, i) => {
            const r = el.getBoundingClientRect();
            const cs = getComputedStyle(el);
            const vis = r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none';
            out.push({
              i, tag: el.tagName, type: el.type || '',
              text: (el.innerText || el.value || '').trim().slice(0, 90),
              aria: el.getAttribute('aria-label') || '',
              testid: el.getAttribute('data-testid') || '',
              cls: (el.className && el.className.toString ? el.className.toString() : '').slice(0, 110),
              href: (el.getAttribute('href') || '').slice(0, 130),
              dl: el.hasAttribute('download'),
              checked: el.type === 'checkbox' ? el.checked : null,
              vis, x: Math.round(r.x), y: Math.round(r.y),
              w: Math.round(r.width), h: Math.round(r.height)
            });
          });
          return out;
        }
        """
        try:
            items = page.evaluate(js)
        except Exception as e:
            self.ui.append(f"(evaluate failed: {e})")
            items = []
        for it in items:
            if not it["vis"] and not it["dl"]:
                continue
            self.ui.append(
                f"  [{it['i']:3d}] {it['tag']:6s}{('/'+it['type']) if it['type'] else '':10s} "
                f"vis={int(it['vis'])} chk={it['checked']} dl={int(it['dl'])} "
                f"{it['w']}x{it['h']}@({it['x']},{it['y']}) "
                f"text={it['text']!r} aria={it['aria']!r} testid={it['testid']!r} "
                f"cls={it['cls']!r} href={it['href']!r}"
            )
        # iframes often hold consent dialogs
        try:
            for f in page.frames:
                if f != page.main_frame:
                    self.ui.append(f"  IFRAME url={f.url}")
        except Exception:
            pass

    def flush(self, page=None) -> None:
        if page is not None:
            try:
                html = page.content()
                with open(self.out + ".page.html", "w", encoding="utf-8") as fh:
                    fh.write(html[:MAX_HTML])
                log(f"wrote {os.path.basename(self.out)}.page.html ({len(html)} chars)")
            except Exception as e:
                log(f"DOM dump failed: {e}")
        try:
            with open(self.out + ".ui.txt", "w", encoding="utf-8") as fh:
                fh.write("\n".join(self.ui))
        except Exception:
            pass
        try:
            with open(self.out + ".network.log", "w", encoding="utf-8") as fh:
                fh.write("\n".join(self.net[-4000:]))
        except Exception:
            pass


# ---------------------------------------------------------------------------
# clicking helpers
# ---------------------------------------------------------------------------

def visible_click(page, locator, timeout: int = 4000) -> bool:
    try:
        if not locator.is_visible():
            return False
        locator.scroll_into_view_if_needed(timeout=2000)
        locator.click(timeout=timeout)
        return True
    except Exception:
        try:
            locator.click(timeout=timeout, force=True)
            return True
        except Exception:
            return False


def dismiss_overlays(page, diag: Diag) -> None:
    for txt in CONSENT_TEXTS:
        for loc in (
            page.get_by_role("button", name=re.compile(rf"^\s*{re.escape(txt)}\s*$", re.I)),
            page.locator(f"button:has-text('{txt}')"),
        ):
            try:
                if loc.count() and visible_click(page, loc.first, 1500):
                    log(f"dismissed overlay via {txt!r}")
                    page.wait_for_timeout(500)
                    return
            except Exception:
                continue
    for frame in page.frames:
        if frame == page.main_frame:
            continue
        for txt in CONSENT_TEXTS[:8]:
            try:
                loc = frame.locator(f"button:has-text('{txt}')")
                if loc.count() and loc.first.is_visible():
                    loc.first.click(timeout=1200)
                    log(f"dismissed iframe overlay via {txt!r}")
                    page.wait_for_timeout(500)
                    return
            except Exception:
                continue


def select_all_files(page) -> None:
    """LimeWire folder shares need the files ticked before Download works."""
    for txt in ("Select all", "Select All"):
        loc = page.locator(f"text=/^\\s*{txt}\\s*$/i")
        try:
            if loc.count() and visible_click(page, loc.first, 2000):
                log(f"clicked {txt!r}")
                page.wait_for_timeout(400)
                return
        except Exception:
            pass
    try:
        boxes = page.locator("input[type=checkbox]")
        for i in range(min(boxes.count(), 25)):
            b = boxes.nth(i)
            try:
                if b.is_visible() and not b.is_checked():
                    b.check(timeout=1200)
                    log(f"checked checkbox #{i}")
            except Exception:
                pass
    except Exception:
        pass


def download_candidates(page) -> List:
    """Ordered list of things worth clicking to start a download."""
    out = []
    out.append(page.get_by_role("button", name=DOWNLOAD_RE))
    out.append(page.get_by_role("link", name=DOWNLOAD_RE))
    out.append(page.locator("a[download]"))
    out.append(page.locator("[data-testid*='download' i]"))
    out.append(page.locator("[class*='download' i]"))
    out.append(page.locator("button:has-text('Download')"))
    out.append(page.locator("a:has-text('Download')"))
    out.append(page.locator("[role=button]:has-text('Download')"))
    return out


# ---------------------------------------------------------------------------
# main download routine
# ---------------------------------------------------------------------------

def download(url: str, out: str, timeout_s: int = 900, headful: bool = False) -> bool:
    diag = Diag(out)
    got = False

    with sync_playwright() as pw:
        browser = pw.chromium.launch(
            headless=not headful,
            args=[
                "--no-sandbox", "--disable-dev-shm-usage",
                "--disable-blink-features=AutomationControlled",
                "--window-size=1440,1000",
            ],
        )
        ctx = browser.new_context(
            accept_downloads=True,
            user_agent=("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                        "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36"),
            viewport={"width": 1440, "height": 1000},
            locale="en-US",
        )
        ctx.set_default_timeout(30_000)
        page = ctx.new_page()

        def on_response(resp):
            try:
                h = resp.headers
                ct = h.get("content-type", "")
                cl = h.get("content-length", "")
                line = f"{resp.status} {resp.request.method} {resp.url[:220]} ct={ct} len={cl}"
                diag.net.append(line)
                if cl and cl.isdigit() and int(cl) > 1_000_000:
                    log(f"BIG RESPONSE {int(cl)/1024/1024:.1f}MB {resp.url[:160]}")
            except Exception:
                pass

        page.on("response", on_response)
        page.on("console", lambda m: diag.net.append(f"CONSOLE {m.type}: {m.text[:300]}"))
        page.on("pageerror", lambda e: diag.net.append(f"PAGEERROR {str(e)[:300]}"))
        ctx.on("page", lambda p: diag.net.append(f"NEWPAGE {p.url}"))

        # Catch downloads no matter which page starts them.
        holder = {"dl": None}
        ctx.on("download", lambda d: holder.__setitem__("dl", d))

        log(f"opening {url}")
        try:
            page.goto(url, wait_until="domcontentloaded", timeout=60_000)
        except PWTimeout:
            log("goto timed out, continuing")

        try:
            page.wait_for_load_state("networkidle", timeout=30_000)
        except PWTimeout:
            log("networkidle not reached, continuing")

        page.wait_for_timeout(3000)
        diag.screenshot(page, "loaded")
        diag.dump_ui(page, "after load")

        dismiss_overlays(page, diag)
        page.wait_for_timeout(800)
        select_all_files(page)
        page.wait_for_timeout(800)
        diag.screenshot(page, "prepared")
        diag.dump_ui(page, "after consent+select")

        deadline = time.time() + timeout_s
        attempt = 0

        for group_idx, loc_group in enumerate(download_candidates(page)):
            if time.time() > deadline or got:
                break
            try:
                n = loc_group.count()
            except Exception:
                continue
            for i in range(min(n, 6)):
                if time.time() > deadline or got:
                    break
                attempt += 1
                el = loc_group.nth(i)
                try:
                    if not el.is_visible():
                        continue
                    label = (el.inner_text() or "").strip()[:60]
                except Exception:
                    continue

                log(f"attempt {attempt}: group {group_idx} item {i} label={label!r}")
                remaining = max(45_000, int((deadline - time.time()) * 1000))
                try:
                    with page.expect_download(timeout=min(remaining, 300_000)) as dli:
                        if not visible_click(page, el, 6000):
                            raise PWTimeout("click failed")
                    dl = dli.value
                    log(f"download event: {dl.suggested_filename!r}")
                    dl.save_as(out)
                    got = True
                    break
                except PWTimeout:
                    log(f"attempt {attempt}: no download event")
                    if holder["dl"] is not None:
                        try:
                            holder["dl"].save_as(out)
                            log("recovered download from context handler")
                            got = True
                            break
                        except Exception as e:
                            log(f"recovery failed: {e}")
                    dismiss_overlays(page, diag)
                    page.wait_for_timeout(1200)
                except Exception as e:
                    log(f"attempt {attempt} error: {type(e).__name__}: {e}")
                    page.wait_for_timeout(1200)

        diag.screenshot(page, "final")
        diag.dump_ui(page, "final")
        diag.flush(page)

        if got and os.path.isfile(out):
            size = os.path.getsize(out)
            log(f"saved {out} ({size/1024/1024:.2f} MB)")
            with open(out, "rb") as fh:
                magic = fh.read(4)
            log(f"magic={magic!r}")
            if size == 0:
                log("ERROR: file is empty")
                got = False
        else:
            log("no download captured")
            got = False

        browser.close()

    return got


def main() -> int:
    p = argparse.ArgumentParser(description="Download a file from a LimeWire share link.")
    p.add_argument("url")
    p.add_argument("-o", "--output", required=True)
    p.add_argument("--timeout", type=int, default=900)
    p.add_argument("--headful", action="store_true")
    a = p.parse_args()

    if "#" not in a.url:
        log("WARNING: link has no '#key' fragment; LimeWire needs it to decrypt")

    try:
        ok = download(a.url, a.output, a.timeout, a.headful)
    except Exception:
        log("unhandled exception:")
        traceback.print_exc()
        return 1

    log("OK" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
