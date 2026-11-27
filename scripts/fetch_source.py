#!/usr/bin/env python3
"""Fetch the source video for the edit from a runner that YouTube blocks.

Strategy: ask public YouTube frontends (Invidious / Piped / cobalt) to resolve a
progressive MP4 stream, then download that stream. Those services do the player
API call from their own IPs, which are usually not "confirm you're not a bot"
limited. Falls back to yt-dlp on the local IP.

Writes edits/source/raw.mp4 and a human readable log on stdout.
"""
import json
import os
import ssl
import sys
import time
import urllib.request
import urllib.error

VID = "40-Bp04QC6w"
OUT = "edits/source/raw.mp4"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/131.0 Safari/537.36")

CTX = ssl.create_default_context()
CTX.check_hostname = False
CTX.verify_mode = ssl.CERT_NONE


def log(*a):
    print(*a, flush=True)


def fetch(url, timeout=45, data=None, headers=None, limit=None):
    h = {"User-Agent": UA, "Accept": "*/*"}
    if headers:
        h.update(headers)
    req = urllib.request.Request(url, data=data, headers=h)
    with urllib.request.urlopen(req, timeout=timeout, context=CTX) as r:
        if limit:
            return r.read(limit), r.geturl(), dict(r.headers)
        return r.read(), r.geturl(), dict(r.headers)


def jget(url, timeout=30):
    raw, final, _ = fetch(url, timeout=timeout, limit=4_000_000,
                          headers={"Accept": "application/json"})
    return json.loads(raw.decode("utf-8", "replace"))


def download(url, headers=None, budget=1500):
    """stream url -> OUT, return size or 0"""
    h = {"User-Agent": UA, "Accept": "*/*", "Referer": "https://www.youtube.com/"}
    if headers:
        h.update(headers)
    try:
        req = urllib.request.Request(url, headers=h)
        total = 0
        t0 = time.time()
        with urllib.request.urlopen(req, timeout=60, context=CTX) as r, open(OUT + ".part", "wb") as f:
            while True:
                chunk = r.read(1 << 20)
                if not chunk:
                    break
                f.write(chunk)
                total += len(chunk)
                if total > 120_000_000:
                    break
                if time.time() - t0 > budget:
                    log("  ...download budget hit")
                    break
        if total < 2_000_000:
            os.remove(OUT + ".part")
            return 0
        os.replace(OUT + ".part", OUT)
        return total
    except Exception as e:
        log("  download error:", type(e).__name__, str(e)[:160])
        if os.path.exists(OUT + ".part"):
            os.remove(OUT + ".part")
        return 0


def try_invidious():
    hosts = []
    try:
        data, _, _ = fetch("https://api.invidious.io/instances.json?pretty=0", timeout=40,
                           limit=3_000_000)
        rows = json.loads(data.decode("utf-8", "replace"))
        for name, info in rows:
            if info.get("type") != "https":
                continue
            api = (info.get("api") or {})
            if isinstance(api, dict) and api.get("online", True) is False:
                continue
            uri = info.get("uri")
            if uri:
                hosts.append(uri.rstrip("/"))
    except Exception as e:
        log("instances.json failed:", type(e).__name__, str(e)[:120])

    for extra in ["https://yewtu.be", "https://inv.nadeko.net", "https://invidious.nerdvpn.de",
                  "https://iv.melmac.space", "https://invidious.f5.si", "https://invidious.privacyredirectcom",
                  "https://invidious.jing.rocks", "https://yt.artemislena.eu", "https://invidious.lunivers.trade"]:
        if extra not in hosts:
            hosts.append(extra)

    for h in hosts[:28]:
        for itag in (18, 22):
            url = f"{h}/latest_version?id={VID}&itag={itag}&local=true"
            log("invidious:", url)
            size = download(url)
            if size:
                log("  OK", size)
                return size
    return 0


def try_piped():
    hosts = ["https://pipedapi.kavin.rocks", "https://api.piped.private.coffee",
             "https://pipedapi.adminforge.de", "https://pipedapi.drgns.space",
             "https://pipedapi.leptons.xyz", "https://pipedapi.orangenet.cc",
             "https://pipedapi.reallyaweso.me", "https://piped-api.lunar.icu"]
    for h in hosts:
        try:
            d = jget(h + "/streams/" + VID, timeout=30)
        except Exception as e:
            log("piped err", h, type(e).__name__, str(e)[:100])
            continue
        streams = (d.get("videoStreams") or []) + (d.get("audioStreams") or [])
        # prefer progressive (video+audio) h264 <=720p
        cands = [s for s in streams if s.get("url") and not s.get("videoOnly")
                 and s.get("format") == "MPEG_4"]
        cands = sorted(cands, key=lambda s: int(s.get("height") or 999))
        for s in cands[:2]:
            log("piped:", h, s.get("quality"), s.get("height"))
            size = download(s["url"])
            if size:
                log("  OK", size)
                return size
    return 0


def try_cobalt():
    hosts = ["https://cobalt-api.kwiatekmiki.com", "https://cobalt-backend.canine.tools",
             "https://cobalt.api.timelessnesses.me", "https://api.cobalt.best",
             "https://cobalt-api.ayo.tf", "https://cobalt.255x.ru", "https://api.cobalt.tools",
             "https://cobalt-api.3kh0.net", "https://dl.khyerdev.ru"]
    body = json.dumps({"url": "https://www.youtube.com/watch?v=" + VID,
                       "videoQuality": "720", "downloadMode": "auto",
                       "youtubeVideoCodec": "h264", "filenameStyle": "basic"}).encode()
    for h in hosts:
        try:
            raw, _, _ = fetch(h + "/", timeout=45, data=body,
                              headers={"Accept": "application/json",
                                       "Content-Type": "application/json"},
                              limit=20000)
            d = json.loads(raw.decode("utf-8", "replace"))
        except Exception as e:
            log("cobalt err", h, type(e).__name__, str(e)[:110])
            continue
        log("cobalt:", h, d.get("status"), (d.get("error") or {}).get("code"))
        link = d.get("url") or d.get("tunnel")
        if link:
            size = download(link)
            if size:
                log("  OK", size)
                return size
    return 0


def main():
    os.makedirs("edits/source", exist_ok=True)
    t0 = time.time()
    for name, fn in (("invidious", try_invidious), ("piped", try_piped), ("cobalt", try_cobalt)):
        log("\n===== trying", name)
        try:
            size = fn()
        except Exception as e:
            log(name, "crashed:", type(e).__name__, str(e)[:200])
            size = 0
        if size:
            log(f"SUCCESS via {name}: {size} bytes in {time.time()-t0:.0f}s")
            with open(OUT, "rb") as f:
                head = f.read(12)
            log("magic:", head[:8].hex())
            return 0
    log("ALL STRATEGIES FAILED")
    return 1


if __name__ == "__main__":
    sys.exit(main())
