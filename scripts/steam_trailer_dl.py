#!/usr/bin/env python3
"""Скачивание трейлера Steam (HLS-плейлист) и сборка в mp4.

Пример:
    python3 scripts/steam_trailer_dl.py \
        "https://video.akamai.steamstatic.com/store_trailers/12130/914/<hash>/<ts>/hls_264_master.m3u8?t=1447351212" \
        manhunt_trailer.mp4

Нужен ffmpeg (на GitHub-раннерах ubuntu-latest он уже установлен).
"""
import argparse
import os
import re
import subprocess
import sys
import time
import urllib.parse
import urllib.request

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
)


def fetch(url: str, retries: int = 6) -> bytes:
    last = None
    for attempt in range(retries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "*/*"})
            with urllib.request.urlopen(req, timeout=60) as resp:
                return resp.read()
        except Exception as exc:  # noqa: BLE001
            last = exc
            time.sleep(1.5 * (attempt + 1))
    raise SystemExit(f"не удалось скачать {url}: {last}")


def fetch_text(url: str) -> str:
    return fetch(url).decode("utf-8", "replace")


def unescape(text: str) -> str:
    # в HTML страницы Steam подчёркивания бывают экранированы как \_
    return text.replace("\\_", "_")


def resolve(base: str, uri: str) -> str:
    return urllib.parse.urljoin(base, unescape(uri).strip())


def parse_attrs(line: str) -> dict:
    attrs = {}
    for key, val in re.findall(r'([A-Z0-9-]+)=("[^"]*"|[^,]*)', line):
        attrs[key] = val.strip('"')
    return attrs


def media_playlist(master_url: str, uri: str) -> tuple[str, list[str]]:
    """Возвращает (url плейлиста, список url сегментов) вместе с init-сегментом."""
    playlist_url = resolve(master_url, uri)
    text = fetch_text(playlist_url)
    urls = []
    for line in text.splitlines():
        line = line.strip()
        if not line:
            continue
        if line.startswith("#EXT-X-MAP:"):
            init_uri = parse_attrs(line.split(":", 1)[1]).get("URI")
            if init_uri:
                urls.append(resolve(playlist_url, init_uri))
        elif not line.startswith("#"):
            urls.append(resolve(playlist_url, line))
    if not urls:
        raise SystemExit(f"в плейлисте нет сегментов: {playlist_url}")
    return playlist_url, urls


def download_to(urls: list[str], path: str) -> None:
    total = 0
    with open(path, "wb") as fh:
        for i, url in enumerate(urls, 1):
            data = fetch(url)
            fh.write(data)
            total += len(data)
            print(f"  [{i}/{len(urls)}] {len(data)} B  (всего {total} B)", flush=True)
    if total == 0:
        raise SystemExit(f"пустой поток: {path}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("master_url")
    ap.add_argument("output")
    args = ap.parse_args()

    master_url = args.master_url
    master = fetch_text(master_url)

    audio_uri = None
    variants = []
    for line in master.splitlines():
        line = line.strip()
        if line.startswith("#EXT-X-MEDIA:") and 'TYPE=AUDIO' in line:
            attrs = parse_attrs(line.split(":", 1)[1])
            if attrs.get("URI") and (audio_uri is None or attrs.get("DEFAULT") == "YES"):
                audio_uri = attrs["URI"]
        elif line and not line.startswith("#") and "m3u8" in line:
            variants.append(line)

    if not variants:
        raise SystemExit("в master-плейлисте нет видеопотоков")

    out_dir = os.path.dirname(os.path.abspath(args.output)) or "."
    os.makedirs(out_dir, exist_ok=True)

    print(f"видео: {len(variants)} поток(ов), аудио: {'да' if audio_uri else 'нет'}", flush=True)
    _, video_urls = media_playlist(master_url, variants[0])
    print(f"скачиваю видео ({len(video_urls)} файлов)", flush=True)
    video_path = os.path.join(out_dir, "_video.mp4")
    download_to(video_urls, video_path)

    inputs = ["-i", video_path]
    if audio_uri:
        _, audio_urls = media_playlist(master_url, audio_uri)
        print(f"скачиваю аудио ({len(audio_urls)} файлов)", flush=True)
        audio_path = os.path.join(out_dir, "_audio.mp4")
        download_to(audio_urls, audio_path)
        inputs += ["-i", audio_path]

    cmd = [
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "warning",
        *inputs,
        "-c", "copy", "-movflags", "+faststart",
        args.output,
    ]
    print("собираю mp4:", " ".join(cmd), flush=True)
    subprocess.run(cmd, check=True)

    for tmp in ("_video.mp4", "_audio.mp4"):
        p = os.path.join(out_dir, tmp)
        if os.path.exists(p):
            os.remove(p)

    size = os.path.getsize(args.output)
    print(f"готово: {args.output} ({size} B, {size / 1048576:.2f} MiB)", flush=True)
    subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries",
         "format=duration:stream=codec_name,width,height,sample_rate",
         "-of", "default=noprint_wrappers=1", args.output],
        check=False,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
