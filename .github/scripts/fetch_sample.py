#!/usr/bin/env python3
"""Скачивает файл по публичной ссылке MediaFire в указанный путь.

Скрипт нужен только потому, что машина, где идёт разбор, не имеет прямого
доступа к сети. Сам файл НЕ запускается: единственное, что с ним делают —
читают байты и считают хеши.
"""

from __future__ import annotations

import base64
import re
import sys
import urllib.error
import urllib.request

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0 Safari/537.36")

RE_DIRECT = re.compile(r'https://download\d+\.mediafire\.com/[^"\'\s<>]+')
RE_SCRAMBLED = re.compile(r'data-scrambled-url="([^"]+)"')


def get(url: str, referer: str | None = None) -> bytes:
    req = urllib.request.Request(url, headers={
        "User-Agent": UA,
        "Accept": "text/html,application/octet-stream,*/*",
        "Accept-Language": "en-US,en;q=0.9",
    })
    if referer:
        req.add_header("Referer", referer)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read()


def main() -> int:
    page_url, dest = sys.argv[1], sys.argv[2]
    html = get(page_url).decode("utf-8", "replace")
    print(f"страница: {len(html)} символов")

    links = RE_DIRECT.findall(html)
    if not links:
        scrambled = RE_SCRAMBLED.search(html)
        if scrambled:
            decoded = base64.b64decode(scrambled.group(1)).decode("utf-8", "replace")
            links = RE_DIRECT.findall(decoded)
            print("ссылка получена из data-scrambled-url")
    if not links:
        print("ПРОВАЛ: прямых ссылок не найдено. Фрагмент страницы:")
        print(html[:1500])
        return 1

    link = links[0]
    print(f"прямая ссылка: {link[:90]}…")
    data = get(link, referer=page_url)
    with open(dest, "wb") as fh:
        fh.write(data)
    print(f"сохранено: {len(data)} байт -> {dest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
