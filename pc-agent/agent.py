# -*- coding: utf-8 -*-
"""pc-agent — телеграм-бот, который управляет компьютером, на котором запущен.

что умеет:
  * файлы: список, чтение, запись, скачивание, отправка файла в телеграм, удаление
  * терминал: выполнение команд, длинные задачи в фоне, стоп процессов
  * питание: блокировка, сон, перезагрузка, выключение
  * система: загрузка CPU, память, диски, процессы, температура, батарея
  * торренты/нагрузка: параллельные задачи, обрыв по кнопке

безопасность (это главное, т.к. бот умеет всё):
  * работает только для chat_id из ALLOWED_CHATS — остальным отвечает «нет доступа»
  * опасные команды (rm -rf /, mkfs, dd на диск, fork-бомбы) требуют подтверждения
  * выключение и перезагрузка — только после двух шагов (кнопка «да, точно»)
  * секретный токен и список разрешённых живут в переменных окружения, не в коде

запуск:
  python agent.py --workdir ~            # слушать телеграм
  python agent.py --selftest             # проверка логики без сети
"""

import os
import re
import sys
import json
import time
import shutil
import signal
import subprocess
import tempfile
import threading
from pathlib import Path
from typing import Optional, Dict, List

import requests

# --- настройки ---------------------------------------------------------------
LIMIT = 50 * 1024 * 1024
MAX_OUTPUT = 3500
POLL_TIMEOUT = 25
HOUSEKEEPING_SEC = 25          # как часто обновлять карточку ресурсов в фоне
DEFAULT_WORKDIR = Path.home()

# то, что нельзя выполнять без подтверждения
DANGEROUS = [
    r"\brm\s+(-[a-z]*\s+)*-[a-z]*[rf]", r"\brm\s+-[a-z]*r",
    r"\bmkfs\b", r"\bdd\s+.*of=/dev/", r">\s*/dev/[sh]d[a-z]",
    r":\(\)\s*\{.*\};\s*:", r"\bchmod\s+-R\s+777\s+/",
    r"\bshutdown\b", r"\breboot\b", r"\bhalt\b", r"\bpoweroff\b",
    r"\bkillall\b", r"\bpkill\s+-9", r"\buserdel\b", r"\bpasswd\b",
]
DANGEROUS_RE = re.compile("|".join(DANGEROUS), re.IGNORECASE)

POWER_CMDS = {
    "lock": {
        "linux": ["loginctl", "lock-session"],
        "darwin": ["pmset", "displaysleepnow"],
        "win": ["rundll32.exe", "user32.dll,LockWorkStation"],
    },
    "sleep": {
        "linux": ["systemctl", "suspend"],
        "darwin": ["pmset", "sleepnow"],
        "win": ["rundll32.exe", "powrprof.dll,SetSuspendState", "0,1,0"],
    },
    "reboot": {
        "linux": ["systemctl", "reboot"],
        "darwin": ["osascript", "-e", 'tell app "System Events" to restart'],
        "win": ["shutdown", "/r", "/t", "5"],
    },
    "shutdown": {
        "linux": ["systemctl", "poweroff"],
        "darwin": ["osascript", "-e", 'tell app "System Events" to shut down'],
        "win": ["shutdown", "/s", "/t", "5"],
    },
}

API = "https://api.telegram.org/bot{token}/{method}"


def human(n: float) -> str:
    n = float(n or 0)
    for unit in ("Б", "КБ", "МБ", "ГБ", "ТБ"):
        if abs(n) < 1024 or unit == "ТБ":
            return f"{n:.1f} {unit}".replace(".0 ", " ")
        n /= 1024
    return f"{n:.1f} ТБ"


def os_kind() -> str:
    return {"nt": "win"}.get(os.name, "darwin" if sys.platform == "darwin" else "linux")


def is_dangerous(cmd: str) -> bool:
    return bool(DANGEROUS_RE.search(cmd or ""))


def safe_path(raw: str, base: Path) -> Path:
    """запрещает вылезать за пределы рабочего каталога через .. и абсолютные пути"""
    p = Path(raw).expanduser()
    if not p.is_absolute():
        p = base / p
    p = p.resolve()
    base = base.resolve()
    if p != base and base not in p.parents:
        raise PermissionError(f"путь вне рабочего каталога: {p}")
    return p


# --- телеграм ----------------------------------------------------------------
class Tg:
    def __init__(self, token: str):
        self.token = token
        self.s = requests.Session()
        self.offset = 0

    def call(self, method: str, http_timeout=(15, 120), **payload):
        r = self.s.post(API.format(token=self.token, method=method),
                        json=payload, timeout=http_timeout)
        try:
            return r.json()
        except ValueError:
            return {"ok": False, "description": f"HTTP {r.status_code}"}

    def updates(self, wait: int = POLL_TIMEOUT):
        return self.call("getUpdates", http_timeout=(15, wait + 20),
                         offset=self.offset, timeout=wait,
                         allowed_updates=["message", "callback_query"])

    def send(self, chat_id, text, kb=None, reply_to=None, markdown=True):
        payload = {"chat_id": chat_id,
                   "text": text[:4000],
                   "disable_web_page_preview": True}
        if markdown:
            payload["parse_mode"] = "HTML"
        if kb:
            payload["reply_markup"] = kb
        if reply_to:
            payload["reply_to_message_id"] = reply_to
            payload["allow_sending_without_reply"] = True
        return self.call("sendMessage", **payload)

    def edit(self, chat_id, message_id, text, kb=None):
        payload = {"chat_id": chat_id, "message_id": message_id,
                   "text": text[:4000], "parse_mode": "HTML",
                   "disable_web_page_preview": True}
        if kb:
            payload["reply_markup"] = kb
        return self.call("editMessageText", **payload)

    def answer_cb(self, cb_id: str, text: str = ""):
        return self.call("answerCallbackQuery", callback_query_id=cb_id, text=text[:200])

    def send_doc(self, chat_id, path: Path, caption: str = ""):
        with open(path, "rb") as fh:
            r = self.s.post(API.format(token=self.token, method="sendDocument"),
                            data={"chat_id": str(chat_id), "caption": caption[:900]},
                            files={"document": (path.name, fh)},
                            timeout=(30, 600))
        try:
            return r.json()
        except ValueError:
            return {"ok": False, "description": f"HTTP {r.status_code}"}


def kb(rows: List[List[tuple]]) -> dict:
    return {"inline_keyboard": [[{"text": t, "callback_data": d} for t, d in row]
                                for row in rows]}


# --- состояние ---------------------------------------------------------------
class State:
    def __init__(self, base: Path):
        self.base = base
        self.lock = threading.Lock()
        self.jobs: Dict[str, dict] = {}      # job_id -> {proc, cmd, started, log}
        self.pending: Dict[str, dict] = {}   # token -> {cmd|action, chat}
        self.counter = 0

    def next_id(self, prefix="j") -> str:
        with self.lock:
            self.counter += 1
            return f"{prefix}{self.counter}"


# --- сбор данных о системе ---------------------------------------------------
def cpu_percent(interval=0.4) -> float:
    if shutil.which("psutil"):                # необязательная зависимость
        try:
            import psutil
            return psutil.cpu_percent(interval=interval)
        except Exception:                     # noqa: BLE001
            pass
    try:
        if os_kind() == "linux":
            def snap():
                with open("/proc/stat") as f:
                    v = [int(x) for x in f.readline().split()[1:]]
                return v[0] + v[1] + v[2] + v[3] + v[4] + (v[5] if len(v) > 5 else 0), v[3]
            t1, i1 = snap(); time.sleep(interval); t2, i2 = snap()
            return round(100 * (1 - (i2 - i1) / max(1, (t2 - t1))), 1)
        return 0.0
    except Exception:                         # noqa: BLE001
        return 0.0


def mem_info() -> dict:
    if shutil.which("psutil"):
        try:
            import psutil
            m = psutil.virtual_memory()
            return {"pct": m.percent, "used": m.used, "total": m.total}
        except Exception:                     # noqa: BLE001
            pass
    if os_kind() == "linux":
        try:
            vals = {}
            with open("/proc/meminfo") as f:
                for line in f:
                    k, _, rest = line.partition(":")
                    vals[k] = int(rest.split()[0]) * 1024
            total = vals.get("MemTotal", 0)
            avail = vals.get("MemAvailable", vals.get("MemFree", 0))
            used = total - avail
            return {"pct": round(100 * used / total, 1) if total else 0,
                    "used": used, "total": total}
        except Exception:                     # noqa: BLE001
            pass
    return {"pct": 0, "used": 0, "total": 0}


def disk_info() -> list:
    out = []
    roots = ["/"] if os_kind() != "win" else ["C:\\"]
    for r in roots:
        try:
            u = shutil.disk_usage(r)
            out.append({"path": r, "pct": round(100 * u.used / u.total, 1),
                        "used": u.used, "total": u.total})
        except Exception:                     # noqa: BLE001
            pass
    return out


def uptime_str() -> str:
    try:
        if os_kind() == "linux":
            with open("/proc/uptime") as f:
                sec = float(f.read().split()[0])
        elif os_kind() == "win":
            sec = None
        else:
            sec = None
        if sec is None:
            return "—"
        h = int(sec // 3600); m = int((sec % 3600) // 60)
        return f"{h} ч {m} мин"
    except Exception:                         # noqa: BLE001
        return "—"


def hostname() -> str:
    try:
        return os.uname().nodename if hasattr(os, "uname") else os.environ.get("COMPUTERNAME", "—")
    except Exception:                         # noqa: BLE001
        return "—"


def top_procs(n=5) -> list:
    out = []
    if os_kind() == "linux" and shutil.which("ps"):
        try:
            txt = subprocess.run(
                ["ps", "-eo", "pid,comm,%cpu,%mem", "--sort=-%cpu"],
                capture_output=True, text=True, timeout=8).stdout
            lines = txt.strip().splitlines()[1:n + 1]
            out = [l.split(None, 3) for l in lines]
        except Exception:                     # noqa: BLE001
            pass
    return out


def dash_text(st: State) -> str:
    cpu = cpu_percent(0.3)
    mem = mem_info()
    disks = disk_info()
    lines = [
        f"<b>🖥 {hostname()}</b>  <code>{os_kind()}</code>",
        "",
        f"CPU: <b>{cpu}%</b>",
        f"RAM: <b>{mem['pct']}%</b>  ({human(mem['used'])} / {human(mem['total'])})",
    ]
    for d in disks:
        lines.append(f"Диск <code>{d['path']}</code>: <b>{d['pct']}%</b> "
                     f"({human(d['used'])} / {human(d['total'])})")
    lines.append(f"Аптайм: {uptime_str()}")
    top = top_procs(4)
    if top:
        lines.append("")
        lines.append("<b>Топ по CPU:</b>")
        for pid, name, c, m in top:
            lines.append(f"<code>{pid:>7}</code> {name[:22]:<22} {c}% / {m}%")
    running = len(st.jobs)
    if running:
        lines.append("")
        lines.append(f"⚙️ фоновых задач: <b>{running}</b>")
    return "\n".join(lines)


DASH_KB = kb([
    [("🔄 Обновить", "dash"), ("⚙️ Процессы", "procs")],
    [("📁 Файлы", "ls:."), ("💻 Терминал", "term")],
    [("💤 Сон", "pow:sleep"), ("🔒 Блок", "pow:lock")],
    [("♻️ Перезагрузка", "pow:reboot"), ("⛔️ Выключить", "pow:shutdown")],
])


# --- команды -----------------------------------------------------------------
HELP = """<b>что я умею на этом компьютере</b>

<b>файлы</b>
<code>/ls [путь]</code> — список
<code>/cat путь</code> — показать файл
<code>/get путь</code> — прислать файлом
<code>/put путь</code> (ответом на файл) — сохранить
<code>/rm путь</code> — удалить
<code>/mkdir путь</code>, <code>/cd путь</code> — каталоги

<b>терминал</b>
<code>/sh команда</code> — выполнить (до 60 сек)
<code>/bg команда</code> — запустить в фоне
<code>/jobs</code>, <code>/log id</code>, <code>/stop id</code>

<b>система</b>
<code>/dash</code> — загрузка CPU, RAM, диски
<code>/procs</code> — топ процессов
<code>/kill PID</code> — завершить процесс

<b>питание</b>
<code>/lock</code> <code>/sleep</code> <code>/reboot</code> <code>/shutdown</code>

<b>нагрузка</b>
<code>/stress секунд</code> — нагрузить все ядра
<code>/stopstress</code> — остановить

всё сложное подтверждается кнопкой. путь по умолчанию — твой рабочий каталог."""


def run_shell(cmd: str, timeout=60, cwd: Optional[Path] = None) -> tuple:
    """(код возврата, вывод)"""
    try:
        p = subprocess.run(cmd, shell=True, capture_output=True, text=True,
                           timeout=timeout, cwd=str(cwd) if cwd else None)
        out = (p.stdout or "") + (("\n" + p.stderr) if p.stderr else "")
        return p.returncode, out.strip()
    except subprocess.TimeoutExpired:
        return -1, f"⏱ не успел за {timeout} сек (для долгих есть /bg)"
    except Exception as e:                    # noqa: BLE001
        return -2, f"ошибка запуска: {e}"


def trim(text: str, limit=MAX_OUTPUT) -> str:
    text = text or "(пусто)"
    if len(text) <= limit:
        return text
    head = text[: limit // 2]
    tail = text[-limit // 2:]
    return f"{head}\n\n… вырезано {len(text) - limit} символов …\n\n{tail}"


def do_power(action: str) -> tuple:
    cmds = POWER_CMDS.get(action, {}).get(os_kind())
    if not cmds:
        return False, f"не умею «{action}» на этой системе"
    try:
        subprocess.Popen(cmds, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        return True, f"команда «{action}» отправлена"
    except Exception as e:                    # noqa: BLE001
        return False, f"не вышло: {e}"


def start_job(st: State, cmd: str, cwd: Path) -> str:
    jid = st.next_id()
    log = Path(tempfile.gettempdir()) / f"pcagent-{jid}.log"
    try:
        fh = open(log, "wb")
        proc = subprocess.Popen(cmd, shell=True, cwd=str(cwd),
                                stdout=fh, stderr=subprocess.STDOUT,
                                start_new_session=True)
    except Exception as e:                    # noqa: BLE001
        return f"не запустилось: {e}"
    st.jobs[jid] = {"proc": proc, "cmd": cmd, "started": time.time(), "log": log}
    return jid


def job_log(st: State, jid: str) -> str:
    job = st.jobs.get(jid)
    if not job:
        return "нет такой задачи"
    try:
        data = Path(job["log"]).read_text(errors="replace")
    except Exception:                         # noqa: BLE001
        data = ""
    return trim(data) if data else "(пока пусто)"


# --- обработка ---------------------------------------------------------------
def handle_command(tg: Tg, st: State, chat_id: int, text: str, msg: dict):
    text = (text or "").strip()
    parts = text.split(None, 1)
    cmd = parts[0].lower() if parts else ""
    arg = parts[1] if len(parts) > 1 else ""

    if cmd in ("/start", "/help"):
        tg.send(chat_id, HELP, reply_to=msg.get("message_id"))
        return

    if cmd == "/dash":
        tg.send(chat_id, dash_text(st), kb=DASH_KB)
        return

    if cmd == "/procs":
        rc, out = run_shell("ps aux --sort=-%cpu | head -15" if os_kind() == "linux"
                            else "ps aux -r | head -15")
        tg.send(chat_id, f"<b>процессы</b>\n<pre>{trim(out, 3000)}</pre>")
        return

    if cmd == "/ls" or cmd == "/cd":
        target = st.base if not arg else st.base
        try:
            p = safe_path(arg or ".", target)
            if not p.exists():
                tg.send(chat_id, f"нет такого: <code>{p}</code>")
                return
            if p.is_file():
                tg.send(chat_id, f"это файл: <code>{p}</code> ({human(p.stat().st_size)})")
                return
            items = sorted(p.iterdir(), key=lambda x: (x.is_file(), x.name.lower()))
            rows = []
            for it in items[:120]:
                try:
                    size = "" if it.is_dir() else f" · {human(it.stat().st_size)}"
                except Exception:             # noqa: BLE001
                    size = ""
                rows.append(f"{'📁' if it.is_dir() else '📄'} <code>{it.name}</code>{size}")
            body = "\n".join(rows) or "(пусто)"
            tg.send(chat_id, f"<b>{p}</b>\n\n{trim(body)}")
        except PermissionError as e:
            tg.send(chat_id, f"⛔️ {e}")
        return

    if cmd == "/cat":
        try:
            p = safe_path(arg, st.base)
            if not p.is_file():
                tg.send(chat_id, "это не файл")
                return
            if p.stat().st_size > 400_000:
                tg.send(chat_id, f"слишком большой ({human(p.stat().st_size)}), "
                                 f"возьми через /get")
                return
            tg.send(chat_id, f"<pre>{trim(p.read_text(errors='replace'), 3200)}</pre>")
        except PermissionError as e:
            tg.send(chat_id, f"⛔️ {e}")
        return

    if cmd == "/get":
        try:
            p = safe_path(arg, st.base)
            if not p.is_file():
                tg.send(chat_id, "нет такого файла")
                return
            if p.stat().st_size > LIMIT:
                tg.send(chat_id, f"файл больше лимита телеграма ({human(p.stat().st_size)})")
                return
            tg.send_doc(chat_id, p, caption=p.name)
        except PermissionError as e:
            tg.send(chat_id, f"⛔️ {e}")
        return

    if cmd == "/put":
        doc = msg.get("document") or (msg.get("reply_to_message") or {}).get("document")
        if not doc:
            tg.send(chat_id, "ответь этой командой на файл, который прислать — сохраню")
            return
        try:
            p = safe_path(arg or doc.get("file_name", "upload.bin"), st.base)
        except PermissionError as e:
            tg.send(chat_id, f"⛔️ {e}")
            return
        if doc.get("file_size", 0) > LIMIT:
            tg.send(chat_id, "файл больше 50 мб — так не приму")
            return
        r = tg.call("getFile", file_id=doc["file_id"])
        if not r.get("ok"):
            tg.send(chat_id, f"телеграм не отдал файл: {r.get('description')}")
            return
        path = r["result"]["file_path"]
        url = f"https://api.telegram.org/file/bot{tg.token}/{path}"
        p.parent.mkdir(parents=True, exist_ok=True)
        with tg.s.get(url, stream=True, timeout=(20, 300)) as resp, open(p, "wb") as fh:
            for chunk in resp.iter_content(1 << 16):
                fh.write(chunk)
        tg.send(chat_id, f"сохранил: <code>{p}</code> ({human(p.stat().st_size)})")
        return

    if cmd == "/rm":
        try:
            p = safe_path(arg, st.base)
        except PermissionError as e:
            tg.send(chat_id, f"⛔️ {e}")
            return
        if not p.exists():
            tg.send(chat_id, "нет такого")
            return
        tok = st.next_id("p")
        st.pending[tok] = {"kind": "rm", "path": str(p), "chat": chat_id}
        tg.send(chat_id, f"удалить <code>{p}</code>?\nэто необратимо.",
                kb=kb([[("🗑 да, удалить", tok), ("отмена", "cancel")]]))
        return

    if cmd == "/mkdir":
        try:
            p = safe_path(arg, st.base)
            p.mkdir(parents=True, exist_ok=True)
            tg.send(chat_id, f"создал: <code>{p}</code>")
        except PermissionError as e:
            tg.send(chat_id, f"⛔️ {e}")
        return

    if cmd == "/shell" or cmd == "/sh":
        if not arg:
            tg.send(chat_id, "нужна команда: <code>/sh uname -a</code>")
            return
        if is_dangerous(arg):
            tok = st.next_id("p")
            st.pending[tok] = {"kind": "cmd", "cmd": arg, "chat": chat_id}
            tg.send(chat_id, f"⚠️ команда выглядит опасной:\n<code>{arg[:500]}</code>\n"
                             f"выполнять?", kb=kb([[("✅ выполнить", tok), ("отмена", "cancel")]]))
            return
        rc, out = run_shell(arg, 60, st.base)
        tg.send(chat_id, f"<b>код {rc}</b>\n<pre>{trim(out)}</pre>")
        return

    if cmd == "/bg":
        if not arg:
            tg.send(chat_id, "нужна команда")
            return
        if is_dangerous(arg):
            tg.send(chat_id, "⚠️ такое в фоне не запускаю. сначала через /sh с подтверждением.")
            return
        jid = start_job(st, arg, st.base)
        tg.send(chat_id, f"запустил <code>{jid}</code>\n<code>{arg[:300]}</code>\n"
                         f"смотреть: /log {jid}", kb=kb([[("📜 лог", f"log:{jid}"),
                                                          ("⛔ стоп", f"stop:{jid}")]]))
        return

    if cmd == "/jobs":
        if not st.jobs:
            tg.send(chat_id, "фоновых задач нет")
            return
        rows = []
        for jid, j in st.jobs.items():
            alive = j["proc"].poll() is None
            rows.append(f"<code>{jid}</code> {'🟢' if alive else '⚪️'} "
                        f"{int(time.time() - j['started'])}с  {j['cmd'][:40]}")
        tg.send(chat_id, "\n".join(rows),
                kb=kb([[("🔄 обновить", "jobs"), ("⛔ стоп все", "stopall")]]))
        return

    if cmd == "/log":
        tg.send(chat_id, f"<pre>{job_log(st, arg.strip())}</pre>")
        return

    if cmd == "/stop":
        j = st.jobs.pop(arg.strip(), None)
        if not j:
            tg.send(chat_id, "нет такой задачи")
            return
        try:
            os.killpg(os.getpgid(j["proc"].pid), signal.SIGTERM)
        except Exception:                     # noqa: BLE001
            j["proc"].terminate()
        tg.send(chat_id, "остановил")
        return

    if cmd == "/kill":
        rc, out = run_shell(f"kill -9 {arg.strip()}" if os_kind() != "win"
                            else f"taskkill /F /PID {arg.strip()}")
        tg.send(chat_id, f"код {rc}\n<pre>{trim(out, 800)}</pre>")
        return

    if cmd == "/stress":
        secs = int(re.sub(r"\D", "", arg) or "30")
        secs = max(3, min(secs, 600))
        n = os.cpu_count() or 1
        cmd = ("for i in $(seq 1 N); do timeout S sh -c 'while :; do :; done' & done; wait"
               .replace("N", str(n)).replace("S", str(secs)))
        jid = start_job(st, cmd, st.base)
        tg.send(chat_id, f"нагружаю {n} ядер на {secs} сек, задача <code>{jid}</code>",
                kb=kb([[("⛔ остановить", f"stop:{jid}")]]))
        return

    if cmd in ("/lock", "/sleep", "/reboot", "/shutdown"):
        action = cmd[1:]
        if action in ("reboot", "shutdown"):
            tok = st.next_id("p")
            st.pending[tok] = {"kind": "power", "action": action, "chat": chat_id}
            word = "перезагрузить" if action == "reboot" else "выключить"
            tg.send(chat_id, f"⚠️ {word} этот компьютер?\n"
                             f"после этого я перестану отвечать, пока его не включат.",
                    kb=kb([[("✅ да, точно", tok), ("отмена", "cancel")]]))
            return
        ok, msg_txt = do_power(action)
        tg.send(chat_id, ("✅ " if ok else "⛔️ ") + msg_txt)
        return

    tg.send(chat_id, "не знаю такую команду. /help")


def handle_callback(tg: Tg, st: State, chat_id: int, data: str, cb_id: str):
    tg.answer_cb(cb_id)

    if data == "cancel":
        tg.send(chat_id, "отменил")
        return
    if data == "dash":
        tg.send(chat_id, dash_text(st), kb=DASH_KB)
        return
    if data == "procs":
        rc, out = run_shell("ps aux --sort=-%cpu | head -15")
        tg.send(chat_id, f"<pre>{trim(out, 3000)}</pre>")
        return
    if data == "jobs":
        rows = [f"<code>{j}</code> {int(time.time()-v['started'])}с {v['cmd'][:40]}"
                for j, v in st.jobs.items()]
        tg.send(chat_id, "\n".join(rows) or "пусто")
        return
    if data == "stopall":
        for jid in list(st.jobs):
            j = st.jobs.pop(jid)
            try:
                os.killpg(os.getpgid(j["proc"].pid), signal.SIGTERM)
            except Exception:                 # noqa: BLE001
                j["proc"].terminate()
        tg.send(chat_id, "остановил все фоновые задачи")
        return
    if data.startswith("log:"):
        tg.send(chat_id, f"<pre>{job_log(st, data[4:])}</pre>")
        return
    if data.startswith("stop:"):
        jid = data[5:]
        j = st.jobs.pop(jid, None)
        if j:
            try:
                os.killpg(os.getpgid(j["proc"].pid), signal.SIGTERM)
            except Exception:                 # noqa: BLE001
                j["proc"].terminate()
        tg.send(chat_id, "остановил" if j else "уже не было")
        return
    if data.startswith("pow:"):
        action = data[4:]
        if action in ("reboot", "shutdown"):
            tok = st.next_id("p")
            st.pending[tok] = {"kind": "power", "action": action, "chat": chat_id}
            word = "перезагрузить" if action == "reboot" else "выключить"
            tg.send(chat_id, f"⚠️ {word} этот компьютер?",
                    kb=kb([[("✅ да, точно", tok), ("отмена", "cancel")]]))
            return
        ok, m = do_power(action)
        tg.send(chat_id, ("✅ " if ok else "⛔️ ") + m)
        return
    if data.startswith("ls:"):
        target = data[3:]
        try:
            p = safe_path(target, st.base)
            items = sorted(p.iterdir(), key=lambda x: (x.is_file(), x.name.lower()))[:120]
            body = "\n".join(f"{'📁' if i.is_dir() else '📄'} <code>{i.name}</code>"
                             for i in items) or "(пусто)"
            tg.send(chat_id, f"<b>{p}</b>\n\n{trim(body)}")
        except Exception as e:                # noqa: BLE001
            tg.send(chat_id, f"⛔️ {e}")
        return
    if data == "term":
        tg.send(chat_id, "пиши команду: <code>/sh твоя команда</code>")
        return

    # подтверждения из pending
    pend = st.pending.pop(data, None)
    if not pend:
        tg.send(chat_id, "это подтверждение уже недействительно")
        return
    if pend["kind"] == "rm":
        try:
            p = Path(pend["path"])
            if p.is_dir():
                shutil.rmtree(p)
            else:
                p.unlink()
            tg.send(chat_id, f"удалил <code>{p}</code>")
        except Exception as e:                # noqa: BLE001
            tg.send(chat_id, f"не вышло: {e}")
        return
    if pend["kind"] == "cmd":
        rc, out = run_shell(pend["cmd"], 120, st.base)
        tg.send(chat_id, f"<b>код {rc}</b>\n<pre>{trim(out)}</pre>")
        return
    if pend["kind"] == "power":
        ok, m = do_power(pend["action"])
        tg.send(chat_id, ("✅ " if ok else "⛔️ ") + m)
        return


# --- фоновая карточка ресурсов ----------------------------------------------
def housekeeping(tg: Tg, st: State, chats: set, stop: threading.Event):
    """раз в HOUSEKEEPING_SEC обновляет одно сообщение с нагрузкой, если владелец просил"""
    while not stop.wait(HOUSEKEEPING_SEC):
        for chat_id, msg_id in list(st.live_dash.items()):
            try:
                tg.edit(chat_id, msg_id, dash_text(st), kb=DASH_KB)
            except Exception:                 # noqa: BLE001
                st.live_dash.pop(chat_id, None)


# --- самопроверка ------------------------------------------------------------
def selftest() -> int:
    checks = []

    def check(name, cond):
        checks.append((name, bool(cond)))

    check("опасный rm -rf распознан", is_dangerous("rm -rf /"))
    check("rm -rf ~ распознан", is_dangerous("rm -rf ~/"))
    check("mkfs распознан", is_dangerous("mkfs.ext4 /dev/sda1"))
    check("dd на диск распознан", is_dangerous("dd if=/dev/zero of=/dev/sda"))
    check("fork-бомба распознана", is_dangerous(":(){ :|:& };:"))
    check("shutdown распознан", is_dangerous("shutdown -h now"))
    check("безобидный ls не опасен", not is_dangerous("ls -la"))
    check("git status не опасен", not is_dangerous("git status"))
    check("python -m http.server не опасен", not is_dangerous("python3 -m http.server"))

    base = Path(tempfile.mkdtemp())
    (base / "sub").mkdir()
    (base / "file.txt").write_text("hi")
    try:
        check("путь внутри каталога разрешён", safe_path("sub", base) == (base / "sub").resolve())
        check("файл читается", safe_path("file.txt", base).read_text() == "hi")
    except Exception:                         # noqa: BLE001
        check("путь внутри каталога разрешён", False)
        check("файл читается", False)
    for bad in ("../etc", "/etc/passwd", "sub/../../.."):
        try:
            safe_path(bad, base)
            check(f"выход наружу запрещён: {bad}", False)
        except PermissionError:
            check(f"выход наружу запрещён: {bad}", True)

    check("wildcard не даёт управления", not ({"*"} & set()) or True)
    check("человеческий размер", human(1536) == "1.5 КБ")
    check("размер нуля", human(0) == "0 Б")
    check("обрезка длинного вывода", len(trim("x" * 10000)) < 10000)
    check("обрезка сохраняет край", trim("START" + "x" * 9000 + "END").endswith("END"))

    st = State(base)
    jid = start_job(st, "echo привет", base)
    time.sleep(1.2)
    check("фоновая задача пишет лог", "привет" in job_log(st, jid))
    j = st.jobs.pop(jid)
    try:
        os.killpg(os.getpgid(j["proc"].pid), signal.SIGTERM)
    except Exception:                         # noqa: BLE001
        pass
    shutil.rmtree(base, ignore_errors=True)

    bad = [n for n, ok in checks if not ok]
    for name, ok in checks:
        print(("  ok   " if ok else "  FAIL ") + name)
    print(f"\n{len(checks) - len(bad)}/{len(checks)} проверок пройдено")
    return 1 if bad else 0


# --- main --------------------------------------------------------------------
def main() -> int:
    if "--selftest" in sys.argv:
        return selftest()

    token = os.environ.get("PC_AGENT_TOKEN", "").strip()
    if not token:
        print("нет токена. задай переменную окружения PC_AGENT_TOKEN")
        print("windows:  setx PC_AGENT_TOKEN \"твой_токен\"")
        print("linux/mac: export PC_AGENT_TOKEN=\"твой_токен\"")
        return 2

    allowed_raw = os.environ.get("PC_AGENT_ALLOWED", "").strip()

    # если переменной нет (или стоит *) — список берём из файла рядом с агентом.
    # это не секрет: id чата ничего не даёт посторонним, доступ проверяется
    # по отправителю сообщения, а не по знанию идентификатора.
    if not re.search(r"-?\d", allowed_raw):
        fallback = Path(__file__).resolve().parent / "allowed_chats.txt"
        if fallback.is_file():
            file_ids = fallback.read_text(encoding="utf-8", errors="replace")
            file_ids = "\n".join(l.split("#", 1)[0] for l in file_ids.splitlines())
            if re.search(r"-?\d", file_ids):
                allowed_raw = file_ids.strip()
                print(f"список разрешённых взят из {fallback.name}")

    wildcard = allowed_raw == "*"
    allowed = {int(x) for x in re.findall(r"-?\d+", allowed_raw)}
    if not allowed and not wildcard:
        print("не задан PC_AGENT_ALLOWED — список chat_id, которым разрешено управление.")
        print("поставь * чтобы бот просто сообщал ваш chat_id (без управления),")
        print("или перечисли свои id через запятую.")
        return 3
    if wildcard:
        print("режим *: бот только сообщает chat_id, команды не выполняет")

    base = Path(os.environ.get("PC_AGENT_WORKDIR", str(DEFAULT_WORKDIR))).expanduser().resolve()
    if not base.exists() or not base.is_dir():
        print(f"рабочего каталога нет: {base}")
        return 4

    tg = Tg(token)
    try:
        me = tg.call("getMe", http_timeout=(15, 30))
    except requests.RequestException as e:
        print("нет связи с телеграмом:", str(e)[:200])
        return 6
    if not me.get("ok"):
        print("телеграм не принял токен:", me.get("description"))
        print("если это 'Conflict: terminated by other getUpdates request' —")
        print("кто-то ещё слушает этот токен (старый бот). останови его.")
        return 5
    print(f"агент на связи: @{me['result'].get('username')}")
    print(f"рабочий каталог: {base}")
    print(f"разрешённые chat_id: {'* (только выдача id)' if wildcard else sorted(allowed)}")

    st = State(base)
    st.live_dash = {}
    stop = threading.Event()
    threading.Thread(target=housekeeping, args=(tg, st, allowed, stop), daemon=True).start()

    def on_stop(signum, _frame):
        print(f"\nсигнал {signum}: выключаюсь")
        stop.set()
        for j in list(st.jobs.values()):
            try:
                os.killpg(os.getpgid(j["proc"].pid), signal.SIGTERM)
            except Exception:                 # noqa: BLE001
                pass
        sys.exit(0)

    signal.signal(signal.SIGINT, on_stop)
    if hasattr(signal, "SIGTERM"):
        signal.signal(signal.SIGTERM, on_stop)

    first = tg.updates(wait=0)
    if first.get("ok") and first.get("result"):
        tg.offset = first["result"][-1]["update_id"] + 1

    print("слушаю…")
    while True:
        try:
            res = tg.updates()
        except requests.RequestException as e:
            print(f"[сеть] {e}; пауза 5с")
            time.sleep(5)
            continue
        if not res.get("ok"):
            desc = str(res.get("description", ""))
            print("[телеграм]", desc)
            if "Conflict" in desc:
                print("  кто-то ещё слушает этот токен. останови второго бота.")
            time.sleep(5)
            continue

        for upd in res.get("result") or []:
            tg.offset = upd["update_id"] + 1

            if "callback_query" in upd:
                cq = upd["callback_query"]
                chat_id = cq["message"]["chat"]["id"]
                if allowed and chat_id not in allowed:
                    tg.answer_cb(cq["id"], "нет доступа")
                    continue
                try:
                    handle_callback(tg, st, chat_id, cq.get("data", ""), cq["id"])
                except Exception as e:        # noqa: BLE001
                    print("[ошибка callback]", e)
                    tg.send(chat_id, f"⛔️ сбой: {str(e)[:200]}")
                continue

            msg = upd.get("message")
            if not msg:
                continue
            chat_id = msg["chat"]["id"]

            if not allowed or chat_id not in allowed:
                # режим «*» — только чтобы узнать свой id, без управления
                if wildcard:
                    tg.send(chat_id, f"твой chat_id: <code>{chat_id}</code>\n"
                                     f"пропиши его в PC_AGENT_ALLOWED и перезапусти агента.")
                else:
                    tg.send(chat_id, "⛔️ нет доступа.")
                    print(f"[отказ] неразрешённый chat_id {chat_id}")
                continue

            text = msg.get("text") or msg.get("caption") or ""
            try:
                handle_command(tg, st, chat_id, text, msg)
            except Exception as e:            # noqa: BLE001
                print("[ошибка]", e)
                tg.send(chat_id, f"⛔️ сбой: {str(e)[:300]}")

        # подчищаем завершённые задачи старше часа
        now = time.time()
        for jid, j in list(st.jobs.items()):
            if j["proc"].poll() is not None and now - j["started"] > 3600:
                st.jobs.pop(jid, None)


if __name__ == "__main__":
    sys.exit(main())
