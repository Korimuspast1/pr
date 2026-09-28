#!/usr/bin/env python3
"""
limewire_download.py
====================

Download a file from a LimeWire share link using a headless browser.

LimeWire share links look like:

    https://limewire.com/d/<id>#<key>

The part after "#" is a client-side decryption key. It is never sent to the
server, so a plain HTTP GET only ever returns ciphertext or the HTML shell.
The file must be fetched and decrypted by the page's own JavaScript, so a real
browser is required.

The share page is wrapped in a lot of advertising. A full-page "Before you go"
interstitial (MGID/Brainberries) appears over the UI and swallows the click on
the Download button, so this script:

  1. aborts requests to ad/tracker domains before they load,
  2. strips leftover overlay nodes out of the DOM,
  3. fires the click from inside the page with element.click(), which cannot
     be intercepted by an overlay the way a real mouse click is.

Usage:
    python3 tools/limewire_download.py "https://limewire.com/d/xxx#yyy" -o out.zip

Diagnostics are always written next to the output:
    <out>.ui.txt        inventory of every clickable element, per pass
    <out>.page.html     final DOM (truncated)
    <out>.stepNN_*.jpg  screenshots at each stage
    <out>.network.log   responses + console messages
"""

from __future__ import annotations

import argparse
import os
import re
import sys
import time
import traceback
from typing import List, Optional

try:
    from playwright.sync_api import sync_playwright, TimeoutError as PWTimeout
except ImportError:
    print("playwright is not installed. run:", file=sys.stderr)
    print("  pip install playwright && playwright install --with-deps chromium", file=sys.stderr)
    sys.exit(2)


# ---------------------------------------------------------------------------
# ad / tracker blocking
# ---------------------------------------------------------------------------

# Any request whose URL contains one of these substrings is aborted. Derived
# from a real capture of the LimeWire download page plus the usual suspects.
BLOCK_TOKENS = [
    "mgid.com", "brainberries", "dailyfeed", "zemanta", "outbrain", "taboola",
    "pubmatic.com", "seedtag.com", "openwebmp.com", "yellowblue.io",
    "bidswitch.net", "doubleclick.net", "googlesyndication", "googletagservices",
    "googletagmanager", "google-analytics", "adservice.google",
    "fwmrm.net", "a-mo.net", "openx.net", "rubiconproject.com", "adnxs.com",
    "bidr.io", "adsrvr.org", "1rx.io", "33across.com", "tapad.com",
    "analytics.yahoo.com", "sitescout.com", "creativecdn.com", "inmobi.com",
    "lijit.com", "amazon-adsystem.com", "id5-sync.com", "cognitivlabs.com",
    "thrtle.com", "adx.opera.com", "pmbmonetize", "rlcdn.com", "sascdn.com",
    "tracookiepixel", "criteo.com", "criteo.net", "smartadserver", "casalemedia",
    "sharethrough", "teads.tv", "adform.net", "quantserve", "quantcast",
    "scorecardresearch", "moatads", "adsafeprotected", "everesttech",
    "bluekai", "krxd.net", "demdex.net", "agkn.com", "crwdcntrl.net",
    "exelator.com", "mathtag.com", "turn.com", "spotxchange", "springserve",
    "ad-delivery", "adnium", "adroll", "onesignal", "pushnami", "propellerads",
    "clickadu", "hilltopads", "adsterra", "popads", "popcash", "exoclick",
    "juicyads", "trafficjunky", "media.net", "ezoic", "sovrn", "gumgum",
    "yieldmo", "unrulymedia", "districtm", "sonobi", "triplelift",
    "nativo", "revcontent", "content.ad", "engageya", "plista",
    "temu.com", "sentry.io", "hotjar", "fullstory", "mixpanel", "segment.io",
    "amplitude.com", "clarity.ms", "facebook.net", "connect.facebook",
    "prebid", "adlightning", "confiant", "geoedge",
]

# Never block these even if a token accidentally matches.
ALLOW_TOKENS = ["limewire.com", "limewire.net", "stripe.com", "stripe.network"]


def should_block(url: str) -> bool:
    u = url.lower()
    if any(a in u for a in ALLOW_TOKENS):
        return False
    return any(t in u for t in BLOCK_TOKENS)


# ---------------------------------------------------------------------------
# DOM cleanup executed inside the page
# ---------------------------------------------------------------------------

NUKE_OVERLAYS_JS = r"""
() => {
  const removed = [];
  const vw = innerWidth, vh = innerHeight;
  const AD_HINT = /mgid|brainberries|outbrain|taboola|advert|sponsor|interstitial|before you go/i;

  // 1. any iframe that is not LimeWire's own
  document.querySelectorAll('iframe').forEach(f => {
    const src = f.src || '';
    if (!/limewire\.(com|net)/i.test(src)) { removed.push('iframe:' + src.slice(0,80)); f.remove(); }
  });

  // 2. big fixed/absolute overlays sitting on top of everything
  document.querySelectorAll('body *').forEach(el => {
    if (!el.isConnected) return;
    const cs = getComputedStyle(el);
    if (cs.position !== 'fixed' && cs.position !== 'absolute') return;
    const r = el.getBoundingClientRect();
    const big = r.width >= vw * 0.5 && r.height >= vh * 0.4;
    const z = parseInt(cs.zIndex || '0', 10) || 0;
    const txt = (el.innerText || '').slice(0, 200);
    if ((big && z >= 100) || AD_HINT.test(txt) || AD_HINT.test(el.className || '') || AD_HINT.test(el.id || '')) {
      // don't remove something that itself holds the Download button
      if (el.querySelector('button')) {
        const hasDl = Array.from(el.querySelectorAll('button')).some(b => /download/i.test(b.innerText || ''));
        if (hasDl) return;
      }
      removed.push((el.tagName + '#' + (el.id||'') + '.' + (el.className||'')).slice(0,90));
      el.remove();
    }
  });

  // 3. un-freeze the page if an overlay locked scrolling
  document.documentElement.style.overflow = '';
  document.body.style.overflow = '';
  document.body.style.position = '';
  return removed;
}
"""

FIND_DOWNLOAD_BUTTONS_JS = r"""
() => {
  const out = [];
  document.querySelectorAll('button, a, [role=button]').forEach((el, i) => {
    const t = (el.innerText || el.getAttribute('aria-label') || '').trim();
    if (!/download/i.test(t)) return;
    if (/report/i.test(t)) return;           // "Report This Download"
    const r = el.getBoundingClientRect();
    out.push({ idx: i, text: t.slice(0, 80), w: Math.round(r.width), h: Math.round(r.height),
               vis: r.width > 0 && r.height > 0 });
  });
  return out;
}
"""

CLICK_DOWNLOAD_JS = r"""
(which) => {
  const cands = [];
  document.querySelectorAll('button, a, [role=button]').forEach(el => {
    const t = (el.innerText || el.getAttribute('aria-label') || '').trim();
    if (!/download/i.test(t)) return;
    if (/report/i.test(t)) return;
    cands.push(el);
  });
  if (!cands.length) return 'no-candidates';
  const el = cands[Math.min(which, cands.length - 1)];
  el.scrollIntoView({block: 'center'});
  el.click();
  return 'clicked:' + (el.innerText || '').trim().slice(0, 60);
}
"""

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
        path = f"{self.out}.step{self.shot:02d}_{tag}.jpg"
        try:
            page.screenshot(path=path, type="jpeg", quality=55, full_page=False)
            log(f"screenshot -> {os.path.basename(path)}")
        except Exception as e:
            log(f"screenshot failed ({tag}): {e}")

    def dump_ui(self, page, tag: str) -> None:
        self.ui.append(f"\n########## UI: {tag} ##########")
        try:
            self.ui.append(f"url={page.url}")
            self.ui.append(f"title={page.title()!r}")
        except Exception:
            pass
        js = """
        () => {
          const out = [];
          document.querySelectorAll('button, a, [role=button], input[type=checkbox]').forEach((el, i) => {
            const r = el.getBoundingClientRect();
            const cs = getComputedStyle(el);
            const vis = r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none';
            if (!vis) return;
            out.push({ i, tag: el.tagName, text: (el.innerText || el.value || '').trim().slice(0,80),
                       cls: (el.className||'').toString().slice(0,60),
                       w: Math.round(r.width), h: Math.round(r.height),
                       x: Math.round(r.x), y: Math.round(r.y),
                       checked: el.type === 'checkbox' ? el.checked : null });
          });
          return out;
        }
        """
        try:
            for it in page.evaluate(js):
                self.ui.append(
                    f"  [{it['i']:3d}] {it['tag']:6s} {it['w']}x{it['h']}@({it['x']},{it['y']}) "
                    f"chk={it['checked']} text={it['text']!r} cls={it['cls']!r}"
                )
        except Exception as e:
            self.ui.append(f"(evaluate failed: {e})")

    def flush(self, page=None) -> None:
        if page is not None:
            try:
                html = page.content()
                with open(self.out + ".page.html", "w", encoding="utf-8") as fh:
                    fh.write(html[:MAX_HTML])
            except Exception as e:
                log(f"DOM dump failed: {e}")
        for name, data in ((".ui.txt", self.ui), (".network.log", self.net[-3000:])):
            try:
                with open(self.out + name, "w", encoding="utf-8") as fh:
                    fh.write("\n".join(data))
            except Exception:
                pass


# ---------------------------------------------------------------------------
# download
# ---------------------------------------------------------------------------

def nuke(page, diag: Diag, label: str = "") -> None:
    try:
        removed = page.evaluate(NUKE_OVERLAYS_JS)
        if removed:
            log(f"removed {len(removed)} overlay node(s) {label}")
            diag.net.append(f"NUKED {label}: {removed}")
    except Exception as e:
        log(f"overlay cleanup failed: {e}")


def download(url: str, out: str, timeout_s: int = 900) -> bool:
    diag = Diag(out)
    got = False
    blocked = {"n": 0}

    with sync_playwright() as pw:
        browser = pw.chromium.launch(
            headless=True,
            args=["--no-sandbox", "--disable-dev-shm-usage",
                  "--disable-blink-features=AutomationControlled",
                  "--mute-audio", "--window-size=1440,1000"],
        )
        ctx = browser.new_context(
            accept_downloads=True,
            user_agent=("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                        "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36"),
            viewport={"width": 1440, "height": 1000},
            locale="en-US",
        )
        ctx.set_default_timeout(30_000)

        def router(route):
            try:
                if should_block(route.request.url):
                    blocked["n"] += 1
                    route.abort()
                else:
                    route.continue_()
            except Exception:
                try:
                    route.continue_()
                except Exception:
                    pass

        ctx.route("**/*", router)

        page = ctx.new_page()

        def on_response(resp):
            try:
                h = resp.headers
                cl = h.get("content-length", "")
                diag.net.append(f"{resp.status} {resp.request.method} {resp.url[:200]} "
                                f"ct={h.get('content-type','')} len={cl}")
                if cl.isdigit() and int(cl) > 5_000_000:
                    log(f"BIG RESPONSE {int(cl)/1048576:.1f}MB {resp.url[:140]}")
            except Exception:
                pass

        page.on("response", on_response)
        page.on("console", lambda m: diag.net.append(f"CONSOLE {m.type}: {m.text[:250]}"))
        page.on("pageerror", lambda e: diag.net.append(f"PAGEERROR {str(e)[:250]}"))

        holder = {"dl": None}
        ctx.on("download", lambda d: holder.__setitem__("dl", d))

        log(f"opening {url}")
        try:
            page.goto(url, wait_until="domcontentloaded", timeout=60_000)
        except PWTimeout:
            log("goto timed out, continuing")

        try:
            page.wait_for_load_state("networkidle", timeout=25_000)
        except PWTimeout:
            log("networkidle not reached, continuing")

        page.wait_for_timeout(2500)
        log(f"blocked {blocked['n']} ad/tracker requests so far")
        diag.screenshot(page, "loaded")
        diag.dump_ui(page, "after load")

        nuke(page, diag, "pre-click")
        page.wait_for_timeout(600)

        # tick any file checkboxes (folder shares need an explicit selection)
        try:
            boxes = page.locator("input[type=checkbox]")
            for i in range(min(boxes.count(), 20)):
                b = boxes.nth(i)
                try:
                    if b.is_visible() and not b.is_checked():
                        b.check(timeout=1200)
                        log(f"checked checkbox #{i}")
                except Exception:
                    pass
        except Exception:
            pass

        diag.screenshot(page, "prepared")
        diag.dump_ui(page, "prepared")

        try:
            cands = page.evaluate(FIND_DOWNLOAD_BUTTONS_JS)
            log(f"download candidates in DOM: {cands}")
        except Exception:
            cands = []

        deadline = time.time() + timeout_s
        n_cands = max(1, len(cands))

        for which in range(min(n_cands, 4)):
            if got or time.time() > deadline:
                break
            nuke(page, diag, f"attempt{which}")
            remaining_ms = max(60_000, int((deadline - time.time()) * 1000) - 30_000)
            log(f"attempt {which}: dispatching in-page click, waiting up to {remaining_ms//1000}s")
            try:
                with page.expect_download(timeout=min(remaining_ms, 420_000)) as dli:
                    res = page.evaluate(CLICK_DOWNLOAD_JS, which)
                    log(f"  js click -> {res}")
                    if res == "no-candidates":
                        raise PWTimeout("no download button in DOM")
                dl = dli.value
                log(f"download event: {dl.suggested_filename!r}; saving...")
                dl.save_as(out)
                got = True
            except PWTimeout as e:
                log(f"attempt {which}: no download ({e})")
                if holder["dl"] is not None:
                    try:
                        holder["dl"].save_as(out)
                        log("recovered download from context handler")
                        got = True
                        break
                    except Exception as ex:
                        log(f"  recovery failed: {ex}")
                diag.screenshot(page, f"fail{which}")
                page.wait_for_timeout(1500)
            except Exception as e:
                log(f"attempt {which}: {type(e).__name__}: {e}")
                page.wait_for_timeout(1500)

        log(f"blocked {blocked['n']} ad/tracker requests in total")
        diag.screenshot(page, "final")
        diag.dump_ui(page, "final")
        diag.flush(page)

        if got and os.path.isfile(out) and os.path.getsize(out) > 0:
            size = os.path.getsize(out)
            with open(out, "rb") as fh:
                magic = fh.read(4)
            log(f"saved {out} ({size/1048576:.2f} MB) magic={magic!r}")
        else:
            log("no usable download captured")
            got = False

        browser.close()

    return got


def main() -> int:
    p = argparse.ArgumentParser(description="Download a file from a LimeWire share link.")
    p.add_argument("url")
    p.add_argument("-o", "--output", required=True)
    p.add_argument("--timeout", type=int, default=900)
    a = p.parse_args()

    if "#" not in a.url:
        log("WARNING: link has no '#key' fragment; LimeWire needs it to decrypt")

    try:
        ok = download(a.url, a.output, a.timeout)
    except Exception:
        log("unhandled exception:")
        traceback.print_exc()
        return 1

    log("OK" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
