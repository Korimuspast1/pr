# -*- coding: utf-8 -*-
"""ytdl-bot — Telegram-бот: кидаешь ссылку на видео → получаешь файл.

Возможности:
  * YouTube (обычное видео, Shorts, youtu.be), TikTok, Instagram Reels, VK видео
  * максимальное доступное качество: пробуем 1080p → 720p → 480p
  * если файл не влезает в лимит Bot API (50 МБ) — сжимаем через ffmpeg под лимит
  * прогресс приходит редактированием одного сообщения, без спама

Запуск: python bot.py            (нужен env YTDL_BOT_TOKEN)
Проверка без сети/токена: python bot.py --selftest
"""

import os
import re
import sys
import time
import json
import signal
import shutil
import tempfile
import subprocess
from pathlib import Path
from typing import Optional

import requests

API = "https://api.telegram.org/bot{token}/{method}"

# --- ограничения -------------------------------------------------------------
TELEGRAM_LIMIT = 50 * 1024 * 1024        # жёсткий лимит Bot API на отправку файла
SAFE_UPLOAD = 47 * 1024 * 1024           # оставляем запас на multipart-обёртку
MAX_SOURCE_BYTES = 2 * 1024 * 1024 * 1024  # больше 2 ГБ не тянем — незачем
MAX_DURATION_SEC = 2 * 3600              # длиннее 2 часов бот не берёт
RUN_LIMIT_SEC = 330 * 60                 # сами выходим до таймаута Actions (350 мин)

# какие сайты принимаем (защита от подсовывания произвольных URL)
ALLOWED_HOSTS = (
    "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com",
    "youtu.be", "youtube-nocookie.com", "www.youtube-nocookie.com",
    "tiktok.com", "www.tiktok.com", "vm.tiktok.com", "vt.tiktok.com",
    "instagram.com", "www.instagram.com",
    "vk.com", "m.vk.com", "vkvideo.ru",
)

# лестница качества: сначала лучшее, ниже — если не влезло в лимит
QUALITY_LADDER = [
    "bv*[vcodec^=avc1][height<=1080]+ba[ext=m4a]/b[ext=mp4]/b",
    "bv*[vcodec^=avc1][height<=720]+ba[ext=m4a]/b[ext=mp4]/b",
    "bv*[vcodec^=avc1][height<=480]+ba[ext=m4a]/b",
    "bv*[height<=360]+ba/b",
]

URL_RE = re.compile(r"https?://[^\s<>\"']+", re.IGNORECASE)


# --- ссылки ------------------------------------------------------------------
def extract_host(url: str) -> str:
    m = re.match(r"https?://([^/:?#]+)", url, re.IGNORECASE)
    return (m.group(1) or "").lower() if m else ""


def is_allowed(url: str) -> bool:
    host = extract_host(url)
    return bool(host) and host in ALLOWED_HOSTS


def find_links(text: str) -> list:
    """достаёт из сообщения только разрешённые ссылки, без дублей"""
    out, seen = [], set()
    for raw in URL_RE.findall(text or ""):
        url = raw.rstrip(").,;!?]}\u00bb\"'")
        if url in seen:
            continue
        seen.add(url)
        out.append(url)
    return out


def normalize(url: str) -> str:
    """чистим трекинг-хвосты, которые ломают yt-dlp у tiktok/ig"""
    url = url.split("&is_from_webapp")[0]
    for junk in ("?si=", "?igsh=", "?_r=", "?utm_source=", "?utm_medium="):
        if junk in url:
            url = url.split(junk)[0]
    return url


# --- телеграм ----------------------------------------------------------------
class Tg:
    def __init__(self, token: str):
        self.token = token
        self.session = requests.Session()
        self.offset = 0

    def call(self, method: str, http_timeout=(15, 120), **payload):
        """http_timeout — таймаут HTTP-запроса; остальное уходит полями в API.
        имена специально разные, иначе HTTP-таймаут затирал поле `timeout`
        у getUpdates и long polling вырождался в спам запросами."""
        r = self.session.post(API.format(token=self.token, method=method),
                             json=payload, timeout=http_timeout)
        try:
            return r.json()
        except ValueError:
            return {"ok": False, "description": f"нечитаемый ответ {r.status_code}"}

    def get_updates(self, poll_timeout=25):
        return self.call("getUpdates", http_timeout=(15, poll_timeout + 20),
                         offset=self.offset, timeout=poll_timeout,
                         allowed_updates=["message"])


    def text(self, chat_id: int, text: str, reply_to: Optional[int] = None):
        payload = {"chat_id": chat_id, "text": text, "disable_web_page_preview": True}
        if reply_to:
            payload["reply_to_message_id"] = reply_to
            payload["allow_sending_without_reply"] = True
        return self.call("sendMessage", **payload)

    def edit(self, chat_id: int, message_id: int, text: str):
        return self.call("editMessageText", chat_id=chat_id, message_id=message_id,
                         text=text, disable_web_page_preview=True)

    def send_video(self, chat_id: int, path: Path, caption: str,
                   reply_to: Optional[int] = None, duration: Optional[int] = None):
        data = {"chat_id": str(chat_id), "caption": caption, "supports_streaming": "true"}
        if reply_to:
            data["reply_to_message_id"] = str(reply_to)
        if duration:
            data["duration"] = str(int(duration))
        with open(path, "rb") as fh:
            files = {"video": (path.name, fh, "video/mp4")}
            r = self.session.post(API.format(token=self.token, method="sendVideo"),
                                  data=data, files=files, timeout=(30, 900))
        try:
            return r.json()
        except ValueError:
            return {"ok": False, "description": f"HTTP {r.status_code}"}


# --- скачивание --------------------------------------------------------------
def human(n: Optional[float]) -> str:
    if not n:
        return "?"
    for unit in ("Б", "КБ", "МБ", "ГБ"):
        if n < 1024 or unit == "ГБ":
            return f"{n:.0f} {unit}" if unit != "ГБ" else f"{n:.1f} {unit}"
        n /= 1024
    return f"{n:.1f} ГБ"


def ffmpeg_ok() -> bool:
    return shutil.which("ffmpeg") is not None


def compress(src: Path, dst: Path, duration: float) -> Optional[Path]:
    """пережимаем под лимит целевым битрейтом; None — если не получилось"""
    if not ffmpeg_ok() or duration <= 0:
        return None
    total_kbits = (SAFE_UPLOAD * 8) / duration / 1000
    audio_k = 96
    video_k = int(total_kbits - audio_k)
    if video_k < 120:                     # не влезает даже в мусорное качество
        return None
    scale = ("scale=-2:'min(720,ih)'" if video_k > 1200
             else "scale=-2:'min(480,ih)'")
    cmd = [
        "ffmpeg", "-y", "-loglevel", "error", "-i", str(src),
        "-c:v", "libx264", "-preset", "veryfast", "-vf", scale,
        "-b:v", f"{video_k}k", "-maxrate", f"{int(video_k * 1.3)}k",
        "-bufsize", f"{int(video_k * 2)}k",
        "-c:a", "aac", "-b:a", f"{audio_k}k", "-movflags", "+faststart",
        str(dst),
    ]
    try:
        subprocess.run(cmd, check=True, timeout=1800)
    except Exception as e:                # noqa: BLE001
        print(f"[ffmpeg] не вышло: {e}")
        return None
    return dst if dst.exists() and dst.stat().st_size > 0 else None


def download(url: str, workdir: Path, quality: str, progress_cb) -> dict:
    """качает одним форматом из лестницы; возвращает {path,title,duration,webpage}"""
    import yt_dlp

    last = {"pct": -1, "t": 0.0}

    def hook(d):
        if d.get("status") != "downloading":
            return
        total = d.get("total_bytes") or d.get("total_bytes_estimate") or 0
        got = d.get("downloaded_bytes") or 0
        pct = int(got * 100 / total) if total else 0
        if pct - last["pct"] >= 12 or time.time() - last["t"] > 4:
            last["pct"], last["t"] = pct, time.time()
            progress_cb(pct, got, total)

    opts = {
        "format": quality,
        "outtmpl": str(workdir / "video.%(ext)s"),
        "merge_output_format": "mp4",
        "noplaylist": True,
        "quiet": True,
        "no_warnings": True,
        "nocheckcertificate": True,
        "retries": 3,
        "fragment_retries": 5,
        "concurrent_fragment_downloads": 4,
        "max_filesize": MAX_SOURCE_BYTES,
        "progress_hooks": [hook],
        # tiktok/instagram часто отдают webm/странное — просим mp4
        "postprocessors": [{"key": "FFmpegVideoRemuxer", "preferedformat": "mp4"}],
        "socket_timeout": 30,
    }
    with yt_dlp.YoutubeDL(opts) as ydl:
        info = ydl.extract_info(url, download=True)
        if info.get("entries"):                       # на всякий случай
            info = info["entries"][0]
        path = Path(ydl.prepare_filename(info))
        if not path.exists():                         # remux меняет расширение
            for cand in workdir.glob("video.*"):
                path = cand
                break
        return {
            "path": path,
            "title": (info.get("title") or "видео").strip(),
            "duration": info.get("duration") or 0,
            "webpage": info.get("webpage_url") or url,
        }


def grab_best(url: str, workdir: Path, progress_cb) -> dict:
    """перебирает лестницу качества, пока файл не влезет в лимит телеграма"""
    errors = []
    for i, quality in enumerate(QUALITY_LADDER):
        try:
            res = download(url, workdir, quality, progress_cb)
        except Exception as e:                        # noqa: BLE001
            msg = str(e).splitlines()[0][:180]
            errors.append(msg)
            # если формат просто недоступен — пробуем ниже, иначе выходим
            if "Requested format" in msg or "not available" in msg:
                continue
            raise RuntimeError(msg) from e

        path = res["path"]
        size = path.stat().st_size
        if size <= SAFE_UPLOAD:
            res["size"] = size
            res["compressed"] = False
            return res

        # не влезло: пробуем сжать именно этот файл
        small = compress(path, workdir / "small.mp4", float(res["duration"] or 0))
        if small and small.stat().st_size <= SAFE_UPLOAD:
            res["path"] = small
            res["size"] = small.stat().st_size
            res["compressed"] = True
            return res

        # иначе чистим и идём на следующую ступень
        for f in workdir.glob("video.*"):
            f.unlink(missing_ok=True)
        errors.append(f"{quality[:24]}…: {human(size)} > лимита")
    raise RuntimeError("не удалось уложиться в 50 МБ: " + "; ".join(errors[-2:]))


# --- обработка одного сообщения ----------------------------------------------
HELP = (
    "кинь ссылку — пришлю файлом в лучшем качестве.\n\n"
    "что умею: youtube (включая shorts), tiktok, instagram reels, vk видео.\n"
    "лимит телеграма — 50 мб на файл; если видео больше, сожму под лимит "
    "и скажу об этом.\n\n"
    "команды: /start /help"
)


def handle(tg: Tg, msg: dict, stats: dict):
    chat_id = msg["chat"]["id"]
    text = msg.get("text") or msg.get("caption") or ""
    reply_to = msg.get("message_id")

    if text.startswith("/start") or text.startswith("/help"):
        tg.text(chat_id, HELP, reply_to)
        return

    links = [u for u in find_links(text) if is_allowed(u)]
    if not links:
        if find_links(text):
            tg.text(chat_id, "эту ссылку не поддерживаю. умею: youtube, tiktok, "
                             "instagram reels, vk видео.", reply_to)
        else:
            tg.text(chat_id, "кинь ссылку на видео — youtube, tiktok, instagram "
                             "или vk. пример: https://youtu.be/dQw4w9WgXcQ", reply_to)
        return

    url = normalize(links[0])
    note = tg.text(chat_id, "взял в работу, смотрю что там по качеству…", reply_to)
    note_id = (note.get("result") or {}).get("message_id")

    def progress(pct, got, total):
        if not note_id:
            return
        tg.edit(chat_id, note_id,
                f"качаю: {pct}%\n{human(got)} из {human(total)}")

    workdir = Path(tempfile.mkdtemp(prefix="ytdl-"))
    started = time.time()
    try:
        res = grab_best(url, workdir, progress)

        if note_id:
            tg.edit(chat_id, note_id,
                    f"скачал ({human(res['size'])}). отправляю, это самая долгая часть…")

        caption = res["title"][:180]
        if res["compressed"]:
            caption += "\n\n(сжал под лимит телеграма 50 МБ)"
        else:
            caption += f"\n\n{human(res['size'])} · оригинал"

        sent = tg.send_video(chat_id, res["path"], caption, reply_to,
                             duration=res["duration"])
        if not sent.get("ok"):
            raise RuntimeError("телеграм не принял файл: "
                               + str(sent.get("description"))[:200])

        if note_id:
            tg.edit(chat_id, note_id,
                    f"готово за {int(time.time() - started)} сек · {human(res['size'])}"
                    + (" · сжато" if res["compressed"] else " · без пережатия"))
        stats["done"] += 1

    except Exception as e:                            # noqa: BLE001
        err = str(e).splitlines()[0][:220]
        print(f"[error] {url}: {err}")
        stats["fail"] += 1
        if note_id:
            tg.edit(chat_id, note_id, f"не вышло.\n{err}")
        else:
            tg.text(chat_id, f"не вышло.\n{err}", reply_to)
    finally:
        shutil.rmtree(workdir, ignore_errors=True)


# --- самопроверка (без сети и без токена) ------------------------------------
def selftest() -> int:
    checks = []

    def check(name, cond):
        checks.append((name, bool(cond)))

    check("youtube ссылка принята",
          is_allowed("https://www.youtube.com/watch?v=abc"))
    check("youtu.be принят", is_allowed("https://youtu.be/abc"))
    check("shorts принят", is_allowed("https://youtube.com/shorts/abc?si=xx"))
    check("tiktok принят", is_allowed("https://vm.tiktok.com/ZMabc/"))
    check("мусорный хост отклонён", not is_allowed("https://evil.example/x"))
    check("localhost отклонён", not is_allowed("http://127.0.0.1:8080/x"))
    check("file:// отклонён", not is_allowed("file:///etc/passwd"))

    links = find_links("смотри https://youtu.be/abc, и ещё https://youtu.be/abc")
    check("дубли склеены", len(links) == 1)
    check("запятая не прилипла", links[0] == "https://youtu.be/abc")

    check("si-хвост срезан",
          normalize("https://youtu.be/abc?si=track") == "https://youtu.be/abc")
    check("лестница качества не пустая", len(QUALITY_LADDER) >= 3)
    check("лимит ниже телеграмовского", SAFE_UPLOAD < TELEGRAM_LIMIT)

    bad = [n for n, ok in checks if not ok]
    for name, ok in checks:
        print(("  ok   " if ok else "  FAIL ") + name)
    print(f"\n{len(checks) - len(bad)}/{len(checks)} проверок пройдено")
    return 1 if bad else 0


# --- main --------------------------------------------------------------------
def main() -> int:
    if "--selftest" in sys.argv:
        return selftest()

    token = os.environ.get("YTDL_BOT_TOKEN", "").strip()
    if not token:
        print("нет токена: добавь секрет YTDL_BOT_TOKEN в настройках репозитория.")
        print("settings → secrets and variables → actions → new repository secret")
        return 2
    if not ffmpeg_ok():
        print("внимание: ffmpeg не найден, сжатие больших видео будет недоступно")

    tg = Tg(token)
    me = tg.call("getMe")
    if not me.get("ok"):
        print("телеграм не принял токен:", me.get("description"))
        return 3
    print(f"бот на связи: @{me['result'].get('username')}")

    stopping = {"flag": False}

    def stop(signum, _frame):
        print(f"\nсигнал {signum}: дорабатываю текущее сообщение и выхожу")
        stopping["flag"] = True

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)

    stats = {"done": 0, "fail": 0}
    started = time.time()

    # вежливо снимаем старые апдейты, чтобы не отвечать на вчерашние ссылки
    first = tg.get_updates(poll_timeout=0)
    if first.get("ok") and first.get("result"):
        tg.offset = first["result"][-1]["update_id"] + 1

    print("слушаю сообщения…")
    while not stopping["flag"]:
        if time.time() - started > RUN_LIMIT_SEC:
            print("подошёл к лимиту времени Actions — выхожу сам")
            break
        try:
            res = tg.get_updates()
        except requests.RequestException as e:
            print(f"[сеть] {e}; пауза 5с")
            time.sleep(5)
            continue
        if not res.get("ok"):
            print("[телеграм]", res.get("description"))
            time.sleep(5)
            continue
        for upd in res.get("result") or []:
            tg.offset = upd["update_id"] + 1
            msg = upd.get("message")
            if not msg:
                continue
            try:
                handle(tg, msg, stats)
            except Exception as e:                    # noqa: BLE001
                print("[фатально на сообщении]", str(e)[:200])

    print(f"итого: отправлено {stats['done']}, ошибок {stats['fail']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
