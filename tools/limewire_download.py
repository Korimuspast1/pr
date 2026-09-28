#!/usr/bin/env python3
"""
limewire_download.py
====================

Download a file from a LimeWire share link using a headless browser.

LimeWire share links look like:

    https://limewire.com/d/<id>#<key>

The part after "#" is a client-side decryption key. It is never sent to the
server, so a plain HTTP GET only ever gets you ciphertext (or the HTML page).
The file has to be fetched and decrypted by the page's own JavaScript, which
means we need a real browser.

Usage:
    python3 tools/limewire_download.py "https://limewire.com/d/xxxx#yyyy" -o out.zip

Diagnostics (written next to the output when things go wrong):
    <out>.page.html        final DOM
    <out>.screenshot.png   what the page looked like
    <out>.network.log      every response the page made
"""

from __future__ import annotations

import argparse
import os
import re
import sys
import time
from typing import List, Optional

try:
    from playwright.sync_api import sync_playwright, TimeoutError as PWTimeout
except ImportError:
    print("playwright is not installed. run:", file=sys.stderr)
    print("  pip install playwright && playwright install --with-deps chromium", file=sys.stderr)
    sys.exit(2)


CONSENT_TEXTS = [
    "Accept all", "Accept All", "Accept all cookies", "Allow all", "Accept",
    "I agree", "Agree", "Got it", "Continue", "OK", "Close",
    "Consent", "Zustimmen", "Alle akzeptieren",
]

SELECT_ALL_TEXTS = ["Select all", "Select All", "Unselect all"]

DOWNLOAD_TEXTS = ["Download", "Download all", "Download file", "Download files"]


def log(msg: str) -> None:
    print(f"[limewire] {msg}", flush=True)


def try_click(page, texts: List[str], timeout: int = 2500, what: str = "") -> bool:
    """Click the first visible element matching any of the given texts."""
    for txt in texts:
        for loc in (
            page.get_by_role("button", name=re.compile(rf"^\s*{re.escape(txt)}\s*$", re.I)),
            page.get_by_role("link", name=re.compile(rf"^\s*{re.escape(txt)}\s*$", re.I)),
            page.locator(f"button:has-text('{txt}')"),
            page.locator(f"a:has-text('{txt}')"),
            page.locator(f"[role=button]:has-text('{txt}')"),
        ):
            try:
                n = loc.count()
            except Exception:
                continue
            for i in range(min(n, 4)):
                el = loc.nth(i)
                try:
                    if not el.is_visible():
                        continue
                    el.click(timeout=timeout)
                    log(f"clicked {what or 'element'}: {txt!r}")
                    return True
                except Exception:
                    continue
    return False


def dismiss_overlays(page) -> None:
    """Best-effort dismissal of cookie banners / consent iframes."""
    if try_click(page, CONSENT_TEXTS, timeout=1500, what="consent"):
        page.wait_for_timeout(600)
    for frame in page.frames:
        if frame == page.main_frame:
            continue
        for txt in CONSENT_TEXTS[:6]:
            try:
                loc = frame.locator(f"button:has-text('{txt}')")
                if loc.count() and loc.first.is_visible():
                    loc.first.click(timeout=1200)
                    log(f"clicked consent in iframe: {txt!r}")
                    page.wait_for_timeout(500)
                    return
            except Exception:
                continue


def save_diagnostics(page, out: str, netlog: List[str]) -> None:
    base = out
    try:
        with open(base + ".page.html", "w", encoding="utf-8") as fh:
            fh.write(page.content())
        log(f"wrote {base}.page.html")
    except Exception as e:
        log(f"could not dump DOM: {e}")
    try:
        page.screenshot(path=base + ".screenshot.png", full_page=True)
        log(f"wrote {base}.screenshot.png")
    except Exception as e:
        log(f"could not screenshot: {e}")
    try:
        with open(base + ".network.log", "w", encoding="utf-8") as fh:
            fh.write("\n".join(netlog))
        log(f"wrote {base}.network.log ({len(netlog)} entries)")
    except Exception as e:
        log(f"could not write netlog: {e}")


def download(url: str, out: str, timeout_s: int = 600, headful: bool = False) -> bool:
    os.makedirs(os.path.dirname(os.path.abspath(out)) or ".", exist_ok=True)
    netlog: List[str] = []

    with sync_playwright() as pw:
        browser = pw.chromium.launch(
            headless=not headful,
            args=["--no-sandbox", "--disable-dev-shm-usage", "--disable-blink-features=AutomationControlled"],
        )
        ctx = browser.new_context(
            accept_downloads=True,
            user_agent=(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36"
            ),
            viewport={"width": 1440, "height": 1000},
            locale="en-US",
        )
        ctx.set_default_timeout(30_000)
        page = ctx.new_page()

        def on_response(resp):
            try:
                ct = resp.headers.get("content-type", "")
                cl = resp.headers.get("content-length", "")
                netlog.append(f"{resp.status} {resp.request.method} {resp.url}  ct={ct} len={cl}")
            except Exception:
                pass

        page.on("response", on_response)
        page.on("console", lambda m: netlog.append(f"CONSOLE {m.type}: {m.text}"))

        log(f"opening {url}")
        try:
            page.goto(url, wait_until="domcontentloaded", timeout=60_000)
        except PWTimeout:
            log("goto timed out, continuing anyway")

        try:
            page.wait_for_load_state("networkidle", timeout=25_000)
        except PWTimeout:
            log("networkidle not reached, continuing")

        page.wait_for_timeout(2500)
        dismiss_overlays(page)

        title = ""
        try:
            title = page.title()
        except Exception:
            pass
        log(f"page title: {title!r}")

        # Some share pages list files with checkboxes and need an explicit
        # selection before the Download button does anything.
        try:
            boxes = page.locator("input[type=checkbox]")
            if boxes.count():
                for i in range(min(boxes.count(), 20)):
                    b = boxes.nth(i)
                    try:
                        if b.is_visible() and not b.is_checked():
                            b.check(timeout=1500)
                            log(f"checked file checkbox #{i}")
                    except Exception:
                        pass
        except Exception:
            pass

        deadline = time.time() + timeout_s

        # Attempt the download, retrying through a few UI shapes.
        for attempt in range(1, 5):
            if time.time() > deadline:
                break
            log(f"download attempt {attempt}")
            try:
                with page.expect_download(timeout=max(30_000, int((deadline - time.time()) * 1000))) as dl_info:
                    clicked = try_click(page, DOWNLOAD_TEXTS, timeout=5000, what="download")
                    if not clicked:
                        # fall back to any anchor that looks like a download
                        try:
                            a = page.locator("a[download], a[href*='download'], a[href*='/dl/']")
                            if a.count() and a.first.is_visible():
                                a.first.click(timeout=5000)
                                clicked = True
                                log("clicked fallback download anchor")
                        except Exception:
                            pass
                    if not clicked:
                        log("no download control found on this pass")
                        page.wait_for_timeout(2000)
                        dismiss_overlays(page)
                        raise PWTimeout("no clickable download control")

                dl = dl_info.value
                suggested = dl.suggested_filename
                log(f"download started: {suggested!r}")
                dl.save_as(out)
                size = os.path.getsize(out)
                log(f"saved {out} ({size/1024/1024:.1f} MB)")

                if size == 0:
                    log("ERROR: downloaded file is empty")
                    save_diagnostics(page, out, netlog)
                    browser.close()
                    return False

                with open(out, "wb+") as _:
                    pass
                browser.close()
                return True

            except PWTimeout as e:
                log(f"attempt {attempt} failed: {e}")
                page.wait_for_timeout(2500)
                dismiss_overlays(page)
                continue
            except Exception as e:
                log(f"attempt {attempt} error: {type(e).__name__}: {e}")
                page.wait_for_timeout(2500)
                continue

        log("all attempts failed")
        save_diagnostics(page, out, netlog)
        browser.close()
        return False


def main() -> int:
    p = argparse.ArgumentParser(description="Download a file from a LimeWire share link.")
    p.add_argument("url")
    p.add_argument("-o", "--output", required=True)
    p.add_argument("--timeout", type=int, default=600, help="overall seconds (default 600)")
    p.add_argument("--headful", action="store_true")
    a = p.parse_args()

    if "#" not in a.url:
        log("WARNING: the link has no '#key' fragment; LimeWire needs it to decrypt")

    ok = download(a.url, a.output, a.timeout, a.headful)
    if ok:
        log("OK")
        return 0
    log("FAILED")
    return 1


if __name__ == "__main__":
    sys.exit(main())
