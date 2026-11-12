# -*- coding: utf-8 -*-
"""Слой базы данных: sqlite3, один файл bandit.db рядом с ботом."""

import json
import os
import sqlite3
import threading
import time

DB_PATH = os.environ.get(
    "BANDIT_DB",
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "bandit.db"),
)

_lock = threading.RLock()
_conn: sqlite3.Connection | None = None

SCHEMA = """
CREATE TABLE IF NOT EXISTS players (
    user_id       INTEGER PRIMARY KEY,
    username      TEXT    NOT NULL DEFAULT '',
    nick          TEXT    NOT NULL DEFAULT '',
    money         INTEGER NOT NULL DEFAULT 0,
    xp            INTEGER NOT NULL DEFAULT 0,
    bandi         INTEGER NOT NULL DEFAULT 0,
    rides         INTEGER NOT NULL DEFAULT 0,
    car_tier      INTEGER NOT NULL DEFAULT 0,
    house_tier    INTEGER NOT NULL DEFAULT 0,
    premium_until INTEGER NOT NULL DEFAULT 0,
    bonus_at      INTEGER NOT NULL DEFAULT 0,
    business_tier INTEGER NOT NULL DEFAULT -1,
    business_level INTEGER NOT NULL DEFAULT 1,
    business_raw  INTEGER NOT NULL DEFAULT 0,
    business_done INTEGER NOT NULL DEFAULT 0,
    business_last INTEGER NOT NULL DEFAULT 0,
    business_xp   INTEGER NOT NULL DEFAULT 0,
    scam_count    INTEGER NOT NULL DEFAULT 0,
    gang_id       INTEGER NOT NULL DEFAULT 0,
    equip         TEXT    NOT NULL DEFAULT '',
    ride_until    INTEGER NOT NULL DEFAULT 0,
    ride_car      INTEGER NOT NULL DEFAULT 0,
    settings      TEXT    NOT NULL DEFAULT '{}',
    created_at    INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS inventory (
    user_id INTEGER NOT NULL,
    item_id TEXT    NOT NULL,
    qty     INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (user_id, item_id)
);
CREATE TABLE IF NOT EXISTS gangs (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT UNIQUE NOT NULL,
    owner_id   INTEGER NOT NULL,
    treasury   INTEGER NOT NULL DEFAULT 0,
    created_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS trades (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    a_id       INTEGER NOT NULL,
    b_id       INTEGER NOT NULL,
    a_money    INTEGER NOT NULL DEFAULT 0,
    b_money    INTEGER NOT NULL DEFAULT 0,
    a_items    TEXT    NOT NULL DEFAULT '[]',
    b_items    TEXT    NOT NULL DEFAULT '[]',
    a_ready    INTEGER NOT NULL DEFAULT 0,
    b_ready    INTEGER NOT NULL DEFAULT 0,
    state      TEXT    NOT NULL DEFAULT 'open',
    created_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS promos_used (
    user_id INTEGER NOT NULL,
    code    TEXT    NOT NULL,
    PRIMARY KEY (user_id, code)
);
CREATE TABLE IF NOT EXISTS refs (
    referee_id  INTEGER PRIMARY KEY,
    referrer_id INTEGER NOT NULL,
    created_at  INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS duels (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    a_id       INTEGER NOT NULL,
    b_id       INTEGER NOT NULL,
    bet        INTEGER NOT NULL,
    chat_id    INTEGER NOT NULL,
    state      TEXT    NOT NULL DEFAULT 'open',
    created_at INTEGER NOT NULL
);
"""


def connect() -> sqlite3.Connection:
    global _conn
    with _lock:
        if _conn is None:
            _conn = sqlite3.connect(DB_PATH, check_same_thread=False)
            _conn.row_factory = sqlite3.Row
            _conn.execute("PRAGMA journal_mode=WAL")
            _conn.execute("PRAGMA foreign_keys=ON")
            _conn.executescript(SCHEMA)
        return _conn


def now() -> int:
    return int(time.time())


def fetchone(sql, args=()):
    with _lock:
        cur = connect().execute(sql, args)
        row = cur.fetchone()
        return dict(row) if row else None


def fetchall(sql, args=()):
    with _lock:
        cur = connect().execute(sql, args)
        return [dict(r) for r in cur.fetchall()]


def execute(sql, args=()):
    with _lock:
        cur = connect().execute(sql, args)
        connect().commit()
        return cur


def transaction():
    return _lock


# ---------- игроки ----------

def get_player(user_id: int):
    return fetchone("SELECT * FROM players WHERE user_id = ?", (user_id,))


def create_player(user_id: int, username: str, nick: str, money: int):
    execute(
        "INSERT OR IGNORE INTO players (user_id, username, nick, money, created_at) "
        "VALUES (?,?,?,?,?)",
        (user_id, username or "", nick, money, now()),
    )
    return get_player(user_id)


def touch_username(user_id: int, username: str):
    execute("UPDATE players SET username = ? WHERE user_id = ?", (username or "", user_id))


def ensure_player(user_id: int, username: str, default_money: int, nick: str):
    p = get_player(user_id)
    if p is None:
        p = create_player(user_id, username, nick, default_money)
    else:
        if (username or "") != p["username"]:
            touch_username(user_id, username)
            p = get_player(user_id)
    return p


def is_premium(p) -> bool:
    return p is not None and p["premium_until"] > now()


def buy_premium(user_id: int, days: int):
    p = get_player(user_id)
    base = max(p["premium_until"], now())
    execute("UPDATE players SET premium_until = ? WHERE user_id = ?",
            (base + days * 86400, user_id))


def spend_money(user_id: int, amount: int) -> bool:
    if amount < 0:
        return False
    with _lock:
        cur = connect().execute(
            "UPDATE players SET money = money - ? WHERE user_id = ? AND money >= ?",
            (amount, user_id, amount),
        )
        connect().commit()
        return cur.rowcount == 1


def add_money(user_id: int, amount: int):
    execute("UPDATE players SET money = money + ? WHERE user_id = ?", (amount, user_id))


def spend_bandi(user_id: int, amount: int) -> bool:
    with _lock:
        cur = connect().execute(
            "UPDATE players SET bandi = bandi - ? WHERE user_id = ? AND bandi >= ?",
            (amount, user_id, amount),
        )
        connect().commit()
        return cur.rowcount == 1


def add_bandi(user_id: int, amount: int):
    execute("UPDATE players SET bandi = bandi + ? WHERE user_id = ?", (amount, user_id))


def add_xp(user_id: int, amount: int):
    execute("UPDATE players SET xp = xp + ? WHERE user_id = ?", (amount, user_id))


def get_settings(p) -> dict:
    try:
        return json.loads(p["settings"] or "{}")
    except Exception:
        return {}


def set_setting(user_id: int, key: str, value):
    p = get_player(user_id)
    s = get_settings(p)
    s[key] = value
    execute("UPDATE players SET settings = ? WHERE user_id = ?",
            (json.dumps(s, ensure_ascii=False), user_id))


# ---------- инвентарь ----------

def add_item(user_id: int, item_id: str, qty: int = 1):
    execute(
        "INSERT INTO inventory (user_id, item_id, qty) VALUES (?,?,?) "
        "ON CONFLICT(user_id, item_id) DO UPDATE SET qty = qty + excluded.qty",
        (user_id, item_id, qty),
    )


def remove_item(user_id: int, item_id: str, qty: int = 1) -> bool:
    with _lock:
        cur = connect().execute(
            "UPDATE inventory SET qty = qty - ? WHERE user_id = ? AND item_id = ? AND qty >= ?",
            (qty, user_id, item_id, qty),
        )
        if cur.rowcount == 1:
            connect().execute(
                "DELETE FROM inventory WHERE user_id = ? AND item_id = ? AND qty <= 0",
                (user_id, item_id),
            )
        connect().commit()
        return cur.rowcount == 1


def get_inventory(user_id: int):
    return fetchall(
        "SELECT item_id, qty FROM inventory WHERE user_id = ? AND qty > 0 ORDER BY item_id",
        (user_id,),
    )


def move_item(from_id: int, to_id: int, item_id: str, qty: int = 1) -> bool:
    """Атомарный перенос предмета между игроками."""
    with _lock:
        cur = connect().execute(
            "UPDATE inventory SET qty = qty - ? WHERE user_id = ? AND item_id = ? AND qty >= ?",
            (qty, from_id, item_id, qty),
        )
        if cur.rowcount != 1:
            connect().rollback()
            return False
        connect().execute(
            "INSERT INTO inventory (user_id, item_id, qty) VALUES (?,?,?) "
            "ON CONFLICT(user_id, item_id) DO UPDATE SET qty = qty + excluded.qty",
            (to_id, item_id, qty),
        )
        connect().execute(
            "DELETE FROM inventory WHERE user_id = ? AND item_id = ? AND qty <= 0",
            (from_id, item_id),
        )
        connect().commit()
        return True


# ---------- рефералы ----------

def add_ref(referee_id: int, referrer_id: int) -> bool:
    with _lock:
        cur = connect().execute(
            "INSERT OR IGNORE INTO refs (referee_id, referrer_id, created_at) VALUES (?,?,?)",
            (referee_id, referrer_id, now()),
        )
        connect().commit()
        return cur.rowcount == 1


# ---------- трейды ----------

def create_trade(a_id: int, b_id: int) -> int:
    cur = execute(
        "INSERT INTO trades (a_id, b_id, created_at) VALUES (?,?,?)",
        (a_id, b_id, now()),
    )
    return cur.lastrowid


def get_trade(trade_id: int):
    return fetchone("SELECT * FROM trades WHERE id = ?", (trade_id,))


def get_open_trade_for(user_id: int):
    return fetchone(
        "SELECT * FROM trades WHERE state = 'open' AND (a_id = ? OR b_id = ?) "
        "ORDER BY id DESC LIMIT 1",
        (user_id, user_id),
    )


def update_trade(trade_id: int, **fields):
    if not fields:
        return
    cols = ", ".join(f"{k} = ?" for k in fields)
    args = list(fields.values()) + [trade_id]
    execute(f"UPDATE trades SET {cols} WHERE id = ?", args)


# ---------- банды ----------

def get_gang(gang_id: int):
    if not gang_id:
        return None
    return fetchone("SELECT * FROM gangs WHERE id = ?", (gang_id,))


def get_gang_by_name(name: str):
    return fetchone("SELECT * FROM gangs WHERE lower(name) = lower(?)", (name,))


def create_gang(name: str, owner_id: int) -> int:
    cur = execute(
        "INSERT INTO gangs (name, owner_id, created_at) VALUES (?,?,?)",
        (name, owner_id, now()),
    )
    return cur.lastrowid


def gang_members(gang_id: int):
    return fetchall(
        "SELECT user_id, nick FROM players WHERE gang_id = ? ORDER BY xp DESC",
        (gang_id,),
    )


# ---------- топы ----------

def top_players(limit: int = 10):
    return fetchall(
        "SELECT nick, money, xp FROM players ORDER BY money DESC LIMIT ?", (limit,)
    )


def count_players() -> int:
    row = fetchone("SELECT COUNT(*) AS c FROM players")
    return row["c"] if row else 0
