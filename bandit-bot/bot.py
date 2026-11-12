# -*- coding: utf-8 -*-
"""«Бот бандит» — Telegram-клон легендарного ВК-бота. Полный игровой цикл:

работа (такси / скам / бизнес), казино (слоты, кости, рулетка), шмот,
трейды в стиле cs, банды, дома, промокоды, премиум, рефералка.

Запуск: см. README.md (в термуксе — termux: https://... )
"""

import html
import json
import os
import random
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import telebot
from telebot import types

import db
import gamedata as G

HERE = os.path.dirname(os.path.abspath(__file__))


def load_token() -> str:
    tok = os.environ.get("BANDIT_TOKEN", "").strip()
    if tok:
        return tok
    token_file = os.path.join(HERE, "token.txt")
    if os.path.exists(token_file):
        with open(token_file, "r", encoding="utf-8") as f:
            tok = f.read().strip()
        if tok:
            return tok
    print("не найден токен бота.")
    print("получи токен у @botfather в телеграме и либо:")
    print("  echo 'ТОКЕН' > token.txt")
    print("либо задай переменную окружения BANDIT_TOKEN")
    sys.exit(1)


TOKEN = load_token()
bot = telebot.TeleBot(TOKEN, parse_mode="HTML")

# временные состояния ожидания ввода (трейд, ник): user_id -> dict
STATE = {}
BOT_USERNAME = None


# ---------------- утилиты ----------------

def now() -> int:
    return int(time.time())


def esc(s) -> str:
    return html.escape(str(s))


def money(n) -> str:
    return G.fmt_money(n)


def default_nick() -> str:
    return f"бандит{random.randint(1000, 9999)}"


def player_from(m):
    u = m.from_user
    return db.ensure_player(u.id, u.username or "", G.START_MONEY, default_nick())


def premium(p) -> bool:
    return db.is_premium(p)


def display_name(p) -> str:
    badge = "👑 " if premium(p) else ""
    return badge + esc(p["nick"] or "безымянный")


def pretty_date(ts: int) -> str:
    return time.strftime("%d.%m.%Y", time.localtime(ts))


def parse_amount(token: str, balance: int | None = None) -> int | None:
    """парсит '5000', 'всё'/'все', 'половина'. возвращает int или None"""
    t = token.lower().strip().replace(" ", "").replace(".", "")
    if t in ("всё", "все", "all"):
        return balance
    if t in ("половина", "half", "50%"):
        return None if balance is None else max(1, balance // 2)
    if t.isdigit():
        v = int(t)
        return v if v > 0 else None
    return None


def grant_xp(user_id: int, amount: int) -> str | None:
    """начисляет опыт, возвращает текст про новый уровень или None"""
    before = db.get_player(user_id)
    old_level, _, _ = G.level_info(before["xp"])
    db.add_xp(user_id, amount)
    after = db.get_player(user_id)
    new_level, _, _ = G.level_info(after["xp"])
    if new_level > old_level:
        reward = new_level * 250
        db.add_money(user_id, reward)
        return f"⭐ level up! теперь ты {new_level} уровень (+{money(reward)})"
    return None


def mention(m) -> str:
    return f'<a href="tg://user?id={m.from_user.id}">{esc(m.from_user.first_name or "игрок")}</a>'


def get_target(m):
    """цель из reply либо из @username в тексте"""
    if m.reply_to_message and m.reply_to_message.from_user:
        return m.reply_to_message.from_user
    mm = re.search(r"@([A-Za-z0-9_]{3,32})", m.text or "")
    if mm:
        uname = mm.group(1).lower()
        row = db.fetchone("SELECT user_id FROM players WHERE lower(username) = ?", (uname,))
        if row:
            return type("U", (), {"id": row["user_id"], "username": uname,
                                   "first_name": uname})()
    return None


# ---------------- бизнес: переработка сырья по времени ----------------

def process_business(p):
    tier = p["business_tier"]
    if tier < 0:
        return p
    t = now()
    last = p["business_last"] or t
    elapsed = t - last
    if elapsed <= 0:
        return p
    prem = premium(p)
    speed = G.business_speed(tier, p["business_level"], prem)  # единиц в минуту
    can = int(elapsed * speed / 60)
    units = min(can, p["business_raw"])
    if units <= 0:
        db.execute("UPDATE players SET business_last = ? WHERE user_id = ?", (t, p["user_id"]))
        return db.get_player(p["user_id"])
    pay = int(G.business_pay(tier, p["business_level"], prem) * units)
    new_level = p["business_level"]
    new_xp = p["business_xp"] + units
    leveled = 0
    while new_level < G.BUSINESS_MAX_LEVEL and new_xp >= G.business_xp_need(new_level):
        new_xp -= G.business_xp_need(new_level)
        new_level += 1
        leveled += 1
    db.execute(
        "UPDATE players SET business_raw = business_raw - ?, business_done = business_done + ?, "
        "business_xp = ?, business_level = ?, business_last = ? WHERE user_id = ?",
        (units, pay, new_xp, new_level, t, p["user_id"]),
    )
    return db.get_player(p["user_id"])


# ---------------- карточки (тексты) ----------------

def profile_text(p, compact=False) -> str:
    lvl, rem, need = G.level_info(p["xp"])
    gang = db.get_gang(p["gang_id"])
    car = G.CARS[p["car_tier"]]
    house = G.HOUSES[p["house_tier"]]
    lines = [
        f"👤 <b>{display_name(p)}</b>",
        f"💰 баланс: <b>{money(p['money'])}</b>",
        f"⭐ уровень: {lvl} · опыт {rem}/{need}",
        f"💎 банди: {G.fmt_num(p['bandi'])}",
    ]
    if not compact:
        lines.append(f"🚕 поездок: {p['rides']} · тачка: {esc(car['name'])}")
        lines.append(f"🏠 дом: {esc(house['name'])} (+{int(house['bonus']*100)}% к такси)")
        if p["business_tier"] >= 0:
            b = G.BUSINESSES[p["business_tier"]]
            lines.append(f"🏢 бизнес: {esc(b['name'])} · ур. {p['business_level']}")
        else:
            lines.append("🏢 бизнес: нет (напиши «бизнес»)")
        rank, nxt = G.scam_rank(p["scam_count"])
        lines.append(f"💸 скам: ранг «{rank['name']}» · приглашений: {p['scam_count']}")
        if gang:
            lines.append(f"🏴 банда: «{esc(gang['name'])}» · казна {money(gang['treasury'])}")
        if p["equip"]:
            item = G.CLOTHES_BY_ID.get(p["equip"])
            if item:
                lines.append(f"👕 надето: {esc(item['name'])}")
    if premium(p):
        lines.append(f"👑 премиум до {pretty_date(p['premium_until'])}")
    return "\n".join(lines)


def help_text() -> str:
    return (
        "📖 <b>команды</b>\n\n"
        "👤 <b>я</b> — профиль\n"
        "💼 <b>работа</b> — все работы\n\n"
        "🪙 <b>работа</b> → <b>такси</b> / <b>извоз</b> — такси-меню\n"
        "• <b>в путь</b> — отправиться в рейс\n"
        "• <b>обновить время</b> — завершить рейс когда приедешь\n"
        "• <b>мой транспорт</b> — гараж и автосалон\n\n"
        "💸 <b>скам</b> — скам-ссылка и твой ранг (7 рангов)\n\n"
        "🏢 <b>бизнес</b> — твой бизнес\n"
        "• <b>заказать сырьё 10</b> (или 50/100) — заказать сырьё\n"
        "• <b>забрать</b> — забрать заработанное\n"
        "• <b>купить бизнес 2</b> — купить бизнес по номеру\n\n"
        "🎰 <b>казино</b>\n"
        "• <b>слоты 500</b> — игровой автомат (<b>всё</b>, <b>половина</b> тоже можно)\n"
        "• <b>кости 500</b> — против бота; с ответом на сообщение — против игрока\n"
        "• <b>рулетка 500 красное</b> — ставки: красное/чёрное/чёт/нечет/1-18/19-36/"
        "дюжина1-3/зеро/число 0-36\n\n"
        "👕 <b>шмот</b> — магазин одежды · <b>шкаф</b> — твой гардероб\n"
        "• <b>купить 3</b> — купить предмет №3\n"
        "• <b>надеть 3</b> / <b>снять</b> — надеть / снять\n\n"
        "🏠 <b>дом</b> — жильё (даёт +% к оплате такси)\n\n"
        "🤝 <b>трейд</b> (ответом на сообщение) — обмен в стиле cs\n"
        "• <b>отдать 5000</b> — добавить деньги в трейд\n"
        "• <b>отдать всё</b> — добавить все деньги\n"
        "• <b>отдать предмет 2</b> — добавить предмет №2\n"
        "• <b>готов</b> / <b>отмена</b>\n\n"
        "🏴 <b>банда</b> — банды: <b>банда создать название</b>, <b>банда вступить название</b>, "
        "<b>банда выход</b>, <b>банда пополнить 5000</b>, <b>банда снять 5000</b>\n\n"
        "💰 <b>передать @ник 5000</b> (или ответом: <b>передать 5000</b> / <b>передать всё</b>)\n"
        "👀 <b>глянуть</b> (ответом) — профиль игрока\n"
        "🏆 <b>топ</b> — богатейшие бандиты\n"
        "🎁 <b>бонус</b> — раз в 4 часа\n"
        "🎟 <b>промо КОД</b> — промокоды (попробуй «СТАРТ»)\n"
        "👑 <b>премиум</b> — премиум-подписка\n"
        "⚙️ <b>настройки</b> — настройки профиля\n"
        "🔗 <b>реферал</b> — твоя скам-ссылка\n\n"
        "валюта — <b>$</b>, донатится за счёт игры или премиума 👑"
    )


# ---------------- клавиатуры ----------------

def kb_main(is_admin_user: bool = False):
    kb = types.InlineKeyboardMarkup(row_width=2)
    kb.add(
        types.InlineKeyboardButton("👤 профиль", callback_data="menu:profile"),
        types.InlineKeyboardButton("💼 работа", callback_data="menu:jobs"),
        types.InlineKeyboardButton("🎰 казино", callback_data="menu:casino"),
        types.InlineKeyboardButton("👕 шмот", callback_data="menu:shop"),
        types.InlineKeyboardButton("🏴 банда", callback_data="menu:gang"),
        types.InlineKeyboardButton("🏆 топ", callback_data="menu:top"),
        types.InlineKeyboardButton("🎁 бонус", callback_data="menu:bonus"),
        types.InlineKeyboardButton("❓ команды", callback_data="menu:help"),
    )
    if is_admin_user:
        kb.add(types.InlineKeyboardButton("👑 админка", callback_data="adm:open"))
    return kb


def kb_jobs():
    kb = types.InlineKeyboardMarkup(row_width=3)
    kb.add(
        types.InlineKeyboardButton("🚕 такси", callback_data="menu:taxi"),
        types.InlineKeyboardButton("💸 скам", callback_data="menu:scam"),
        types.InlineKeyboardButton("🏢 бизнес", callback_data="menu:business"),
    )
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:main"))
    return kb


def kb_taxi():
    kb = types.InlineKeyboardMarkup(row_width=2)
    kb.add(
        types.InlineKeyboardButton("🚀 отправиться в путь", callback_data="taxi:ride"),
        types.InlineKeyboardButton("🔄 обновить время", callback_data="taxi:update"),
    )
    kb.add(types.InlineKeyboardButton("🚗 автосалон / гараж", callback_data="taxi:garage:0"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:jobs"))
    return kb


def kb_business(p):
    kb = types.InlineKeyboardMarkup(row_width=3)
    kb.add(
        types.InlineKeyboardButton("📦 сырьё 10", callback_data="biz:order:10"),
        types.InlineKeyboardButton("📦 сырьё 50", callback_data="biz:order:50"),
        types.InlineKeyboardButton("📦 сырьё 100", callback_data="biz:order:100"),
    )
    kb.add(types.InlineKeyboardButton("💰 забрать кассу", callback_data="biz:withdraw"))
    nxt = (p["business_tier"] + 1) if p["business_tier"] >= 0 else 0
    if nxt < len(G.BUSINESSES):
        b = G.BUSINESSES[nxt]
        kb.add(types.InlineKeyboardButton(
            f"🛒 купить «{b['name']}» за {money(b['price'])}",
            callback_data=f"biz:buy:{nxt}"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:jobs"))
    return kb


def kb_casino():
    kb = types.InlineKeyboardMarkup(row_width=3)
    kb.add(
        types.InlineKeyboardButton("слоты 100", callback_data="casino:slots:100"),
        types.InlineKeyboardButton("слоты 500", callback_data="casino:slots:500"),
        types.InlineKeyboardButton("слоты 2000", callback_data="casino:slots:2000"),
    )
    kb.add(
        types.InlineKeyboardButton("🎲 кости 100", callback_data="casino:dice:100"),
        types.InlineKeyboardButton("🎲 кости 500", callback_data="casino:dice:500"),
        types.InlineKeyboardButton("🎲 кости 2000", callback_data="casino:dice:2000"),
    )
    kb.add(types.InlineKeyboardButton("🎯 рулетка (как ставить)", callback_data="casino:roulette_help"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:main"))
    return kb


def kb_shop():
    kb = types.InlineKeyboardMarkup(row_width=1)
    for c in G.CLOTHES:
        if c["cur"] == "none":
            continue
        price = money(c["price"]) if c["cur"] == "$" else f"{c['price']} банди"
        kb.add(types.InlineKeyboardButton(f"{c['name']} — {price}", callback_data=f"shop:buy:{c['id']}"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:main"))
    return kb


def kb_trade(trade_id: int):
    kb = types.InlineKeyboardMarkup(row_width=4)
    kb.add(
        types.InlineKeyboardButton("➕ деньги", callback_data=f"trade:money:{trade_id}"),
        types.InlineKeyboardButton("➕ предмет", callback_data=f"trade:item:{trade_id}"),
        types.InlineKeyboardButton("✅ готов", callback_data=f"trade:ready:{trade_id}"),
        types.InlineKeyboardButton("❌ отмена", callback_data=f"trade:cancel:{trade_id}"),
    )
    return kb


def kb_duel(duel_id: int):
    kb = types.InlineKeyboardMarkup(row_width=2)
    kb.add(
        types.InlineKeyboardButton("🎲 принять вызов", callback_data=f"duel:accept:{duel_id}"),
        types.InlineKeyboardButton("🏳️ отказаться", callback_data=f"duel:decline:{duel_id}"),
    )
    return kb


def kb_settings(p):
    s = db.get_settings(p)
    compact = bool(s.get("compact"))
    kb = types.InlineKeyboardMarkup(row_width=1)
    kb.add(types.InlineKeyboardButton(
        f"компактный профиль: {'вкл ✅' if compact else 'выкл ❌'}",
        callback_data="settings:toggle:compact"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:profile"))
    return kb


def kb_premium():
    kb = types.InlineKeyboardMarkup(row_width=1)
    kb.add(types.InlineKeyboardButton(f"💰 купить за {money(G.PREMIUM_PRICE_MONEY)} (30 дней)",
                                      callback_data="premium:buy:money"))
    kb.add(types.InlineKeyboardButton(f"💎 купить за {G.PREMIUM_PRICE_BANDI} банди (30 дней)",
                                      callback_data="premium:buy:bandi"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="menu:main"))
    return kb


# ---------------- отображение ----------------

def send_profile(m, p, target_p=None):
    p2 = target_p or p
    s = db.get_settings(p2)
    text = profile_text(p2, compact=bool(s.get("compact")))
    if target_p is not None:
        text = "👀 <b>глянул чужой профиль:</b>\n\n" + text
    bot.reply_to(m, text, reply_markup=kb_main(is_admin(m.from_user.id)) if target_p is None else None)


def taxi_card(p) -> str:
    car = G.CARS[p["car_tier"]]
    t = now()
    if p["ride_until"] > t:
        status = f"в пути… осталось {int(p['ride_until'] - t) + 1} сек"
    else:
        status = "свободен, ждёт заказа"
    house = G.HOUSES[p["house_tier"]]
    pay = int(car["pay"] * (1 + house["bonus"]))
    return (
        f"🚕 <b>такси</b>\n\n"
        f"тачка: <b>{esc(car['name'])}</b>\n"
        f"оплата за рейс: {money(pay)} (+{int(house['bonus']*100)}% от дома)\n"
        f"время рейса: {car['time']:.0f} сек\n"
        f"поездок наезжено: {p['rides']}\n"
        f"статус: {status}"
    )


def business_card(p) -> str:
    if p["business_tier"] < 0:
        b = G.BUSINESSES[0]
        return (
            "🏢 <b>бизнес</b>\n\n"
            "у тебя пока нет бизнеса.\n"
            "самый дешёвый — «" + esc(b["name"]) + f"» за {money(b['price'])}.\n"
            "дальше — только круче: от ларька до элитного борделя 🖤"
        )
    p = process_business(p)
    b = G.BUSINESSES[p["business_tier"]]
    lvl = p["business_level"]
    need = G.business_xp_need(lvl)
    cap = G.warehouse_cap(lvl)
    prem = premium(p)
    speed = G.business_speed(p["business_tier"], lvl, prem)
    pay = G.business_pay(p["business_tier"], lvl, prem)
    return (
        f"🏢 <b>{esc(b['name'])}</b>\n\n"
        f"уровень: {lvl}/{G.BUSINESS_MAX_LEVEL} · опыт {p['business_xp']}/{need}\n"
        f"скорость переработки: {speed:.1f} ед/мин{' (👑 +25%)' if prem else ''}\n"
        f"доход с единицы: {money(int(pay))}\n\n"
        f"📦 сырьё на складе: {p['business_raw']}/{cap}\n"
        f"💰 готово к снятию: <b>{money(p['business_done'])}</b>\n\n"
        f"закажи сырьё кнопками ниже — со временем оно превратится в деньги."
    )


def scam_card(p) -> str:
    me = BOT_USERNAME or "bot"
    link = f"https://t.me/{me}?start={p['user_id']}"
    rank, nxt = G.scam_rank(p["scam_count"])
    lines = [
        "💸 <b>скам</b>",
        "",
        "твоя ссылка:",
        f"<code>{link}</code>",
        "",
        f"каждый, кто зайдёт по ней, пришлёт тебе <b>в среднем $2.000–4.500</b>",
        "• 5% шанс получить <b>×5</b>",
        "• 5% шанс получить <b>×0.2</b>",
        "• 👑 премиум даёт <b>×3</b> к выхлопу",
        "• +1 💎 банди за каждого",
        "",
        f"ранг: «{rank['name']}» · приглашений: {p['scam_count']}",
    ]
    if nxt:
        lines.append(f"следующий ранг: «{nxt['name']}» — ещё {nxt['count'] - p['scam_count']} чел. (награда {money(nxt['reward'])})")
    else:
        lines.append("ты достиг максимального ранга. легенда. 🏆")
    return "\n".join(lines)


def gang_card(p) -> str:
    gang = db.get_gang(p["gang_id"])
    if not gang:
        return (
            "🏴 <b>банда</b>\n\n"
            "ты пока не в банде.\n\n"
            f"создать свою: <b>банда создать название</b> — {money(G.GANGS_COST)}\n"
            "вступить в чужую: <b>банда вступить название</b>"
        )
    members = db.gang_members(gang["id"])
    owner = db.fetchone("SELECT nick FROM players WHERE user_id = ?", (gang["owner_id"],))
    lines = [
        f"🏴 <b>банда «{esc(gang['name'])}»</b>",
        "",
        f"лидер: {esc(owner['nick']) if owner else '???'}",
        f"участников: {len(members)}/30",
        f"казна: {money(gang['treasury'])}",
        "",
        "участники: " + ", ".join(esc(mtr["nick"]) for mtr in members[:15]) + ("…" if len(members) > 15 else ""),
        "",
        "<b>банда пополнить 5000</b> — в казну\n<b>банда снять 5000</b> — из казны (только лидер)",
    ]
    return "\n".join(lines)


# ---------------- такси ----------------

def start_ride(m, p):
    t = now()
    if p["ride_until"] > t:
        left = int(p["ride_until"] - t) + 1
        bot.reply_to(m, f"ты и так в пути. осталось {left} сек. потом жми «обновить время»")
        return
    car = G.CARS[p["car_tier"]]
    until = t + car["time"]
    db.execute("UPDATE players SET ride_until = ?, ride_car = ? WHERE user_id = ?",
               (until, p["car_tier"], p["user_id"]))
    bot.reply_to(
        m,
        f"🚕 ты отправился в путь на «{esc(car['name'])}».\n"
        f"рейс займёт ~{car['time']:.0f} сек. когда приедешь — жми «обновить время»",
        reply_markup=types.InlineKeyboardMarkup().add(
            types.InlineKeyboardButton("🔄 обновить время", callback_data="taxi:update")),
    )


def finish_ride(m, p):
    t = now()
    if p["ride_until"] > t:
        left = int(p["ride_until"] - t) + 1
        bot.reply_to(m, f"ещё едешь. осталось {left} сек 🕐")
        return
    car = G.CARS[p["ride_car"]]
    house = G.HOUSES[p["house_tier"]]
    pay = int(car["pay"] * (1 + house["bonus"]))
    db.add_money(p["user_id"], pay)
    db.execute("UPDATE players SET rides = rides + 1, ride_until = 0 WHERE user_id = ?", (p["user_id"],))
    db.add_xp(p["user_id"], 15)
    lvl_msg = grant_xp(p["user_id"], 0)
    total = db.get_player(p["user_id"])
    text = (
        f"🏠 ты вернулся домой. за рейс получено <b>{money(pay)}</b>\n"
        f"поездка №{total['rides']} · всего наезжено {total['rides']}"
    )
    if lvl_msg:
        text += "\n" + lvl_msg
    bot.reply_to(m, text, reply_markup=kb_taxi())


def garage_text(p, page: int) -> str:
    page = max(0, min(page, len(G.CARS) - 1))
    car = G.CARS[page]
    owned = page == p["car_tier"]
    rides_ok = p["rides"] >= car["rides"]
    prem_ok = (not car.get("premium")) or premium(p)
    lines = [
        f"🚗 <b>автосалон / гараж</b> ({page + 1}/{len(G.CARS)})",
        "",
        f"<b>{esc(car['name'])}</b>" + (" ✅ твой" if owned else ""),
        f"цена: {money(car['price']) if car['price'] else ('бесплатно (👑)' if car.get('premium') else 'бесплатно')}",
        f"открывается после {car['rides']} поездок (сейчас {p['rides']})",
        f"оплата за рейс: {money(car['pay'])} · время: {car['time']:.0f} сек",
    ]
    if car.get("premium"):
        lines.append("👑 только для премиума")
    if not owned:
        if not rides_ok:
            lines.append("🔒 ещё не открыто — наезди больше рейсов")
        elif not prem_ok:
            lines.append("👑 нужна премиум-подписка")
        else:
            price = car["price"]
            lines.append(f"✅ можно купить за {money(price)} — жми кнопку ниже")
    return "\n".join(lines)


def kb_garage(p, page: int):
    page = max(0, min(page, len(G.CARS) - 1))
    car = G.CARS[page]
    kb = types.InlineKeyboardMarkup(row_width=3)
    prev_p = (page - 1) % len(G.CARS)
    next_p = (page + 1) % len(G.CARS)
    kb.add(
        types.InlineKeyboardButton("◀️", callback_data=f"taxi:garage:{prev_p}"),
        types.InlineKeyboardButton("🚗 выбрать", callback_data=f"taxi:buy:{page}"),
        types.InlineKeyboardButton("▶️", callback_data=f"taxi:garage:{next_p}"),
    )
    kb.add(types.InlineKeyboardButton("⬅️ к такси", callback_data="menu:taxi"))
    return kb


def try_buy_car(p, page: int) -> str:
    car = G.CARS[page]
    if page == p["car_tier"]:
        return "это и так твоя тачка 🙂"
    if p["rides"] < car["rides"]:
        return f"🔒 рано. нужно {car['rides']} поездок, у тебя {p['rides']}"
    if car.get("premium") and not premium(p):
        return "👑 личный самолёт shamal — только для премиума"
    if car["price"] > 0 and not db.spend_money(p["user_id"], car["price"]):
        return f"не хватает денег. нужно {money(car['price'])}"
    db.execute("UPDATE players SET car_tier = ? WHERE user_id = ?", (page, p["user_id"]))
    return f"🎉 куплено! теперь твоя тачка — «{esc(car['name'])}»"


# ---------------- казино ----------------

def roll_slots():
    return random.choices(G.SLOT_SYMBOLS, weights=G.SLOT_WEIGHTS, k=3)


def slots_text(p, bet: int) -> str:
    if not db.spend_money(p["user_id"], bet):
        return f"не хватает денег на ставку {money(bet)}"
    a, b, c = roll_slots()
    p2 = db.get_player(p["user_id"])
    if a == b == c:
        mult = G.SLOT_TRIPLE[a]
        win = bet * mult
        db.add_money(p["user_id"], win)
        grant_xp(p["user_id"], 20)
        res = (a + " " + b + " " + c) + f"\n\n🎰 <b>ТРИ В РЯД!</b> множитель ×{mult}\nвыигрыш: <b>{money(win)}</b>"
    elif a == b or b == c or a == c:
        win = bet * 2
        db.add_money(p["user_id"], win)
        res = (a + " " + b + " " + c) + f"\n\nпара! возврат ×2: <b>{money(win)}</b>"
    else:
        res = (a + " " + b + " " + c) + f"\n\nмимо. минус {money(bet)}"
    return res


def roulette_spin(p, bet: int, kind: str) -> str:
    if not db.spend_money(p["user_id"], bet):
        return f"не хватает денег на ставку {money(bet)}"
    n = random.randint(0, 36)
    if n == 0:
        color_ru, emoji = "зеро", "🟢"
    elif n in G.ROULETTE_REDS:
        color_ru, emoji = "красное", "🔴"
    else:
        color_ru, emoji = "чёрное", "⚫"

    kind_n = kind.lower().strip()
    straight = None
    m = re.fullmatch(r"(зеро|0)", kind_n)
    if m:
        straight = 0
    elif kind_n.isdigit() and 0 <= int(kind_n) <= 36:
        straight = int(kind_n)

    mult = 0  # полный возврат в ставках (включая саму ставку)
    label = kind_n
    if straight is not None:
        if n == straight:
            mult = 36
        label = f"число {straight}"
    elif kind_n in ("красное", "красный"):
        mult = 2 if (n != 0 and n in G.ROULETTE_REDS) else 0
        label = "красное"
    elif kind_n in ("чёрное", "черное", "чёрный", "черный"):
        mult = 2 if (n != 0 and n not in G.ROULETTE_REDS) else 0
        label = "чёрное"
    elif kind_n in ("чёт", "чет", "чётное", "четное"):
        mult = 2 if (n != 0 and n % 2 == 0) else 0
        label = "чёт"
    elif kind_n in ("нечет", "нечёт", "нечётное", "нечетное"):
        mult = 2 if (n % 2 == 1) else 0
        label = "нечет"
    elif kind_n == "1-18":
        mult = 2 if 1 <= n <= 18 else 0
        label = "1–18"
    elif kind_n == "19-36":
        mult = 2 if 19 <= n <= 36 else 0
        label = "19–36"
    elif kind_n in ("дюжина1", "1-12"):
        mult = 3 if 1 <= n <= 12 else 0
        label = "дюжина 1–12"
    elif kind_n in ("дюжина2", "13-24"):
        mult = 3 if 13 <= n <= 24 else 0
        label = "дюжина 13–24"
    elif kind_n in ("дюжина3", "25-36"):
        mult = 3 if 25 <= n <= 36 else 0
        label = "дюжина 25–36"
    else:
        db.add_money(p["user_id"], bet)  # вернуть ставку — вид ставки не распознан
        return ("не понял вид ставки. примеры: <b>рулетка 500 красное</b>, "
                "<b>рулетка 100 17</b>, <b>рулетка 200 дюжина2</b>, <b>рулетка всё нечет</b>")

    if mult > 0:
        win = bet * mult
        db.add_money(p["user_id"], win)
        grant_xp(p["user_id"], 25)
        outcome = f"🎉 выпало <b>{n}</b> ({emoji} {color_ru}) — ставка «{esc(label)}» зашла!\nвозврат: <b>{money(win)}</b>"
    else:
        outcome = f"💸 выпало <b>{n}</b> ({emoji} {color_ru}) — ставка «{esc(label)}» не зашла. минус {money(bet)}"
    return outcome


def dice_vs_bot(p, bet: int) -> str:
    if not db.spend_money(p["user_id"], bet):
        return f"не хватает денег на ставку {money(bet)}"
    mine = random.randint(1, 6)
    foe = random.randint(1, 6)
    head = f"🎲 ты: <b>{mine}</b> · бот: <b>{foe}</b>\n\n"
    if mine > foe:
        win = int(bet * 1.8)
        db.add_money(p["user_id"], bet + win)
        grant_xp(p["user_id"], 15)
        return head + f"выиграл! (+{money(win)} сверху)"
    if mine == foe:
        db.add_money(p["user_id"], bet)
        return head + "ничья. ставка возвращена."
    return head + f"бот выиграл. минус {money(bet)}"


# ---------------- шмот ----------------

def shop_list_text() -> str:
    lines = ["👕 <b>шмот</b> — магазин одежды", "", "жми кнопку, чтобы купить:"]
    for i, c in enumerate([c for c in G.CLOTHES if c["cur"] != "none"], start=1):
        price = money(c["price"]) if c["cur"] == "$" else f"{c['price']} 💎"
        lines.append(f"{i}. {esc(c['name'])} — {price}")
    lines.append("")
    lines.append("купить текстом: <b>купить 3</b> · примерить: <b>шкаф</b>")
    return "\n".join(lines)


def wardrobe_text(p) -> str:
    inv = db.get_inventory(p["user_id"])
    if not inv:
        return "👕 <b>шкаф</b>\n\nпусто. загляни в <b>шмот</b>"
    lines = ["👕 <b>шкаф</b>", ""]
    for i, row in enumerate(inv, start=1):
        item = G.CLOTHES_BY_ID.get(row["item_id"])
        name = esc(item["name"]) if item else esc(row["item_id"])
        mark = " ✅ надето" if p["equip"] == row["item_id"] else ""
        qty = f" ×{row['qty']}" if row["qty"] > 1 else ""
        lines.append(f"{i}. {name}{qty}{mark}")
    lines.append("")
    lines.append("<b>надеть 2</b> — надеть предмет №2 · <b>снять</b> — снять всё")
    return "\n".join(lines)


def buy_clothes(p, idx_or_name: str) -> str:
    salable = [c for c in G.CLOTHES if c["cur"] != "none"]
    item = None
    if idx_or_name.isdigit():
        i = int(idx_or_name) - 1
        if 0 <= i < len(salable):
            item = salable[i]
    if item is None:
        low = idx_or_name.lower()
        for c in G.CLOTHES:
            if low == c["id"] or low in c["name"].lower():
                item = c
                break
    if item is None:
        return "не понял, что покупать. напиши «шмот» и смотри номера"
    if item["cur"] == "none":
        return "эту шмотку нельзя купить — только добыть"
    if item["cur"] == "$":
        if not db.spend_money(p["user_id"], item["price"]):
            return f"не хватает денег. нужно {money(item['price'])}"
    else:
        if not db.spend_bandi(p["user_id"], item["price"]):
            return f"не хватает банди. нужно {item['price']} 💎"
    db.add_item(p["user_id"], item["id"], 1)
    return f"🎉 куплено: {esc(item['name'])}! надеть: <b>надеть {item['id']}</b>"


def wear_item(p, idx_or_name: str) -> str:
    inv = db.get_inventory(p["user_id"])
    if not inv:
        return "шкаф пуст — нечего надевать"
    item_id = None
    if idx_or_name.isdigit():
        i = int(idx_or_name) - 1
        if 0 <= i < len(inv):
            item_id = inv[i]["item_id"]
    else:
        low = idx_or_name.lower()
        for row in inv:
            item = G.CLOTHES_BY_ID.get(row["item_id"])
            if item and (low == item["id"] or low in item["name"].lower()):
                item_id = row["item_id"]
                break
    if item_id is None:
        return "не нашёл такой предмет в шкафу. смотри номера в «шкаф»"
    db.execute("UPDATE players SET equip = ? WHERE user_id = ?", (item_id, p["user_id"]))
    item = G.CLOTHES_BY_ID.get(item_id)
    return f"👕 надето: {esc(item['name']) if item else esc(item_id)}"


# ---------------- банды ----------------

def gang_create(p, name: str) -> str:
    name = name.strip()
    if p["gang_id"]:
        return "ты уже в банде. сначала <b>банда выход</b>"
    if not (3 <= len(name) <= 20):
        return "название — от 3 до 20 символов"
    if re.search(r"[<>&]", name):
        return "в названии нельзя < > &"
    if db.get_gang_by_name(name):
        return "банда с таким названием уже есть"
    if not db.spend_money(p["user_id"], G.GANGS_COST):
        return f"не хватает денег. нужно {money(G.GANGS_COST)}"
    gid = db.create_gang(name, p["user_id"])
    db.execute("UPDATE players SET gang_id = ? WHERE user_id = ?", (gid, p["user_id"]))
    return f"🏴 банда «{esc(name)}» создана! ты лидер. казну пополняют: <b>банда пополнить 5000</b>"


def gang_join(p, name: str) -> str:
    if p["gang_id"]:
        return "ты уже в банде"
    gang = db.get_gang_by_name(name.strip())
    if not gang:
        return "банда не найдена. проверь название"
    members = db.gang_members(gang["id"])
    if len(members) >= 30:
        return "в банде уже 30 человек — мест нет"
    db.execute("UPDATE players SET gang_id = ? WHERE user_id = ?", (gang["id"], p["user_id"]))
    return f"🏴 ты вступил в банду «{esc(gang['name'])}». грабьте на здоровье"


def gang_leave(p) -> str:
    gang = db.get_gang(p["gang_id"])
    if not gang:
        return "ты и так не в банде"
    db.execute("UPDATE players SET gang_id = 0 WHERE user_id = ?", (p["user_id"],))
    remaining = db.gang_members(gang["id"])
    if not remaining:
        db.execute("DELETE FROM gangs WHERE id = ?", (gang["id"],))
        return f"ты вышел из банды «{esc(gang['name'])}». банда распалась — все ушли"
    if gang["owner_id"] == p["user_id"]:
        new_owner = remaining[0]["user_id"]
        db.execute("UPDATE gangs SET owner_id = ? WHERE id = ?", (new_owner, gang["id"]))
        return f"ты вышел. новым лидером стал {esc(remaining[0]['nick'])}"
    return f"ты вышел из банды «{esc(gang['name'])}»"


# ---------------- трейды ----------------

def trade_text(t) -> str:
    a = db.get_player(t["a_id"])
    b = db.get_player(t["b_id"])

    def side(p, money_sum, items_json, ready):
        items = json.loads(items_json)
        parts = []
        if money_sum:
            parts.append(money(money_sum))
        for it in items:
            item = G.CLOTHES_BY_ID.get(it)
            parts.append(esc(item["name"]) if item else esc(it))
        return f"{'✅' if ready else '▫️'} <b>{esc(p['nick']) if p else '?'}</b>: " + (", ".join(parts) if parts else "ничего")

    return (
        f"🤝 <b>трейд #{t['id']}</b>\n\n"
        + side(a, t["a_money"], t["a_items"], t["a_ready"]) + "\n"
        + side(b, t["b_money"], t["b_items"], t["b_ready"]) + "\n\n"
        "когда обе стороны нажмут «готов» — произойдёт обмен.\n"
        "изменение оффера снимает «готов» у обоих."
    )


def trade_side(t, user_id: int):
    if t["a_id"] == user_id:
        return "a"
    if t["b_id"] == user_id:
        return "b"
    return None


def trade_reset_ready(trade_id: int):
    db.update_trade(trade_id, a_ready=0, b_ready=0)


def trade_add_money(t, user_id: int, amount: int | None, raw: str):
    p = db.get_player(user_id)
    bal = p["money"]
    amt = parse_amount(raw, bal) if raw else amount
    if not amt or amt <= 0:
        return "сколько? пример: <b>отдать 5000</b> или <b>отдать всё</b>"
    amt = min(amt, bal)
    side = trade_side(t, user_id)
    with db.transaction():
        if not db.spend_money(user_id, amt):
            return "не хватает денег"
        if side == "a":
            db.update_trade(t["id"], a_money=t["a_money"] + amt, a_ready=0, b_ready=0)
        else:
            db.update_trade(t["id"], b_money=t["b_money"] + amt, a_ready=0, b_ready=0)
    return f"➕ добавил {money(amt)} в трейд"


def trade_add_item(t, user_id: int, idx: int, item_id: str | None = None) -> str:
    inv = db.get_inventory(user_id)
    if item_id is None:
        i = idx - 1
        if i < 0 or i >= len(inv):
            return "неверный номер предмета. смотри «шкаф»"
        item_id = inv[i]["item_id"]
    side = trade_side(t, user_id)
    if not db.remove_item(user_id, item_id, 1):
        return "нет такого предмета"
    p = db.get_player(user_id)
    if p["equip"] == item_id:
        db.execute("UPDATE players SET equip = '' WHERE user_id = ?", (user_id,))
    if side == "a":
        items = json.loads(t["a_items"]) + [item_id]
        db.update_trade(t["id"], a_items=json.dumps(items, ensure_ascii=False), a_ready=0, b_ready=0)
    else:
        items = json.loads(t["b_items"]) + [item_id]
        db.update_trade(t["id"], b_items=json.dumps(items, ensure_ascii=False), a_ready=0, b_ready=0)
    item = G.CLOTHES_BY_ID.get(item_id)
    return f"➕ добавил «{esc(item['name']) if item else esc(item_id)}» в трейд"


def trade_ready(t, user_id: int) -> str:
    side = trade_side(t, user_id)
    db.update_trade(t["id"], **{f"{side}_ready": 1})
    t2 = db.get_trade(t["id"])
    if t2["a_ready"] and t2["b_ready"]:
        return trade_execute(t2)
    who = "ты" if side == "a" else "ты"
    return f"{who} готов. ждём вторую сторону…"


def trade_execute(t) -> str:
    a, b = t["a_id"], t["b_id"]
    with db.transaction():
        cur = db.execute("UPDATE trades SET state = 'done' WHERE id = ? AND state = 'open'", (t["id"],))
        if cur.rowcount != 1:
            return "трейд уже закрыт"
        if t["a_money"]:
            db.add_money(b, t["a_money"])
        if t["b_money"]:
            db.add_money(a, t["b_money"])
        for it in json.loads(t["a_items"]):
            db.add_item(b, it, 1)
        for it in json.loads(t["b_items"]):
            db.add_item(a, it, 1)
    db.add_xp(a, 20)
    db.add_xp(b, 20)
    return "🤝 сделка прошла! приятно иметь дело 🤝"


def trade_cancel(t, user_id: int) -> str:
    a, b = t["a_id"], t["b_id"]
    with db.transaction():
        cur = db.execute("UPDATE trades SET state = 'cancelled' WHERE id = ? AND state = 'open'", (t["id"],))
        if cur.rowcount != 1:
            return "трейд уже закрыт"
        if t["a_money"]:
            db.add_money(a, t["a_money"])
        if t["b_money"]:
            db.add_money(b, t["b_money"])
        for it in json.loads(t["a_items"]):
            db.add_item(a, it, 1)
        for it in json.loads(t["b_items"]):
            db.add_item(b, it, 1)
    return "❌ трейд отменён, всё возвращено"


# =====================================================================
# обработчики сообщений и колбэков
# =====================================================================

def order_raw(p, amount: int):
    if p["business_tier"] < 0:
        return "у тебя ещё нет бизнеса. напиши «бизнес»"
    cap = G.warehouse_cap(p["business_level"])
    free = cap - p["business_raw"]
    amount = min(amount, free)
    if amount <= 0:
        return f"склад полон ({cap} ед). жди, пока переработается"
    cost = G.BUSINESSES[p["business_tier"]]["raw_cost"] * amount
    if not db.spend_money(p["user_id"], cost):
        return f"не хватает денег на сырьё: нужно {money(cost)}"
    db.execute(
        "UPDATE players SET business_raw = business_raw + ?, "
        "business_last = CASE WHEN business_last = 0 THEN ? ELSE business_last END "
        "WHERE user_id = ?",
        (amount, now(), p["user_id"]),
    )
    p2 = db.get_player(p["user_id"])
    return f"📦 заказал сырьё: {amount} ед за {money(cost)}\nсклад: {p2['business_raw']}/{cap}"


def withdraw_business(p):
    p = process_business(p)
    done = p["business_done"]
    if done <= 0:
        return "касса пуста. закажи сырьё и подожди"
    with db.transaction():
        cur = db.execute("UPDATE players SET business_done = 0, money = money + ? "
                         "WHERE user_id = ? AND business_done = ?", (done, p["user_id"], done))
        if cur.rowcount != 1:
            return "уже забираешь…"
    msg = grant_xp(p["user_id"], 25)
    text = f"💰 забрал из бизнеса <b>{money(done)}</b>"
    if msg:
        text += "\n" + msg
    return text


def buy_business(p, idx: int):
    if idx < 0 or idx >= len(G.BUSINESSES):
        return "нет такого бизнеса. смотри «бизнес»"
    if p["business_tier"] >= idx:
        return "у тебя и так бизнес не хуже 🙂"
    b = G.BUSINESSES[idx]
    if not db.spend_money(p["user_id"], b["price"]):
        return f"не хватает денег. «{esc(b['name'])}» стоит {money(b['price'])}"
    db.execute("UPDATE players SET business_tier = ?, business_last = ? WHERE user_id = ?",
               (idx, now(), p["user_id"]))
    return (f"🎉 купил «{esc(b['name'])}»! сырьё и прогресс сохранились.\n"
            f"закажи сырьё и жми «забрать», когда накапает касса")


def house_card(p):
    house = G.HOUSES[p["house_tier"]]
    nxt_idx = p["house_tier"] + 1
    lines = [
        "🏠 <b>дом</b>",
        "",
        f"сейчас: <b>{esc(house['name'])}</b> (+{int(house['bonus']*100)}% к оплате такси)",
    ]
    kb = types.InlineKeyboardMarkup()
    if nxt_idx < len(G.HOUSES):
        nxt = G.HOUSES[nxt_idx]
        lines.append(f"следующий: {esc(nxt['name'])} за {money(nxt['price'])} (+{int(nxt['bonus']*100)}%)")
        kb.add(types.InlineKeyboardButton(
            f"🛒 купить «{nxt['name']}» за {money(nxt['price'])}",
            callback_data="house:buy"))
    else:
        lines.append("дальше только замок «бандит»… и тот у тебя есть 👑")
    return "\n".join(lines), kb


def do_bonus(p):
    t = now()
    cd = 4 * 3600
    if p["bonus_at"] and t - p["bonus_at"] < cd:
        left = cd - (t - p["bonus_at"])
        return f"🎁 бонус пока не готов. осталось {int(left // 60)} мин", False
    cur = db.execute("UPDATE players SET bonus_at = ? WHERE user_id = ? AND bonus_at <= ?",
                     (t, p["user_id"], t - cd))
    if cur.rowcount != 1:
        return "🎁 бонус уже получен чуть раньше. заходи позже", False
    base = random.randint(800, 2500)
    if premium(p):
        base *= 2
    db.add_money(p["user_id"], base)
    return f"🎁 бонус получен: <b>{money(base)}</b>{' (👑 ×2)' if premium(p) else ''}! заходи через 4 часа", True


def do_promo(p, code: str):
    code = code.strip().upper()
    promo = G.PROMOS.get(code)
    if not promo:
        return "такого промо не существует 🤔"
    cur = db.execute("INSERT OR IGNORE INTO promos_used (user_id, code) VALUES (?,?)",
                     (p["user_id"], code))
    if cur.rowcount != 1:
        return "ты уже активировал этот промокод"
    bits = []
    if promo["money"]:
        db.add_money(p["user_id"], promo["money"])
        bits.append(money(promo["money"]))
    if promo["bandi"]:
        db.add_bandi(p["user_id"], promo["bandi"])
        bits.append(f"{promo['bandi']} 💎 банди")
    if promo["item"]:
        db.add_item(p["user_id"], promo["item"], 1)
        item = G.CLOTHES_BY_ID.get(promo["item"])
        bits.append(f"предмет «{esc(item['name']) if item else promo['item']}»")
    return f"🎟 промокод «{esc(code)}» активирован: " + ", ".join(bits) + "!"


def do_transfer(m, p, text: str):
    target = get_target(m)
    tokens = text.split()
    amount_tok = None
    for tok in tokens[1:]:
        if tok.startswith("@"):
            continue
        amount_tok = tok
    if amount_tok is None:
        return "сколько передаём? пример: <b>передать @ник 5000</b> или <b>передать всё</b> (ответом на сообщение)"
    if target is None:
        return "кому? ответь на сообщение игрока или укажи @ник: <b>передать @ник 5000</b>"
    if target.id == m.from_user.id:
        return "сам себе передавать деньги — уровень жадности 100 🙄"
    tp = db.get_player(target.id)
    if tp is None:
        return "этот игрок ещё не в игре"
    amt = parse_amount(amount_tok, p["money"])
    if not amt:
        return "не понял сумму. пример: <b>передать @ник 5000</b> / <b>передать всё</b>"
    if amt > p["money"]:
        amt = p["money"]
    with db.transaction():
        if not db.spend_money(p["user_id"], amt):
            return "не хватает денег"
        db.add_money(target.id, amt)
    try:
        bot.send_message(target.id, f"💸 тебе передали <b>{money(amt)}</b> (от {esc(p['nick'])})")
    except Exception:
        pass
    return f"💸 передал {money(amt)} игроку «{esc(tp['nick'])}»"


def do_top():
    rows = db.top_players(10)
    if not rows:
        return "пока пусто."
    medals = ["🥇", "🥈", "🥉"]
    lines = ["🏆 <b>топ бандитов по деньгам</b>", ""]
    for i, r in enumerate(rows):
        prefix = medals[i] if i < 3 else f"{i + 1}."
        lines.append(f"{prefix} {esc(r['nick'])} — {money(r['money'])}")
    return "\n".join(lines)


def duel_challenge(m, p, bet: int):
    target = get_target(m)
    if target is None:
        return "ответь на сообщение игрока: <b>кости 500</b>", None
    if target.id == m.from_user.id:
        return "сам с собой в кости не играют", None
    tp = db.get_player(target.id)
    if tp is None:
        return "этот игрок ещё не в игре", None
    if p["money"] < bet:
        return f"не хватает денег. ставка {money(bet)}", None
    duel_id = db.execute(
        "INSERT INTO duels (a_id, b_id, bet, chat_id, created_at) VALUES (?,?,?,?,?)",
        (m.from_user.id, target.id, bet, m.chat.id, now()),
    ).lastrowid
    return (f"🎲 {target.first_name if hasattr(target, 'first_name') else '@' + str(target.username)}, "
            f"тебя вызвали на кости на <b>{money(bet)}</b>!"), duel_id


def duel_accept(duel_id: int, user_id: int):
    d = db.fetchone("SELECT * FROM duels WHERE id = ?", (duel_id,))
    if not d or d["state"] != "open":
        return None, "дуэль уже неактуальна"
    if d["b_id"] != user_id:
        return None, "это не твой вызов"
    if now() - d["created_at"] > 300:
        db.execute("UPDATE duels SET state = 'expired' WHERE id = ? AND state = 'open'", (duel_id,))
        return None, "вызов протух (5 минут)"
    bet = d["bet"]
    with db.transaction():
        cur = db.execute("UPDATE duels SET state = 'rolling' WHERE id = ? AND state = 'open'",
                         (duel_id,))
        if cur.rowcount != 1:
            return None, "дуэль уже неактуальна"
        if not db.spend_money(d["a_id"], bet):
            db.execute("UPDATE duels SET state = 'cancelled' WHERE id = ?", (duel_id,))
            return None, "у вызывающего не хватает денег — дуэль отменена"
        if not db.spend_money(d["b_id"], bet):
            db.add_money(d["a_id"], bet)
            db.execute("UPDATE duels SET state = 'cancelled' WHERE id = ?", (duel_id,))
            return None, "у тебя не хватает денег — дуэль отменена"
        db.execute("UPDATE duels SET state = 'done' WHERE id = ?", (duel_id,))
    d = db.fetchone("SELECT * FROM duels WHERE id = ?", (duel_id,))
    return d, None


def duel_finish(chat_id: int, d):
    msg_a = bot.send_dice(chat_id, emoji="🎲")
    msg_b = bot.send_dice(chat_id, emoji="🎲")
    va = msg_a.dice.value
    vb = msg_b.dice.value
    a = db.get_player(d["a_id"])
    b = db.get_player(d["b_id"])
    bet = d["bet"]
    pot = bet * 2
    if va > vb:
        db.add_money(d["a_id"], pot)
        db.add_xp(d["a_id"], 30)
        winner, wname = d["a_id"], a["nick"] if a else "игрок 1"
        text = f"🎲 {va} : {vb} — победил <b>{esc(wname)}</b> и забирает <b>{money(pot)}</b>!"
    elif vb > va:
        db.add_money(d["b_id"], pot)
        db.add_xp(d["b_id"], 30)
        bname = b["nick"] if b else "игрок 2"
        text = f"🎲 {va} : {vb} — победил <b>{esc(bname)}</b> и забирает <b>{money(pot)}</b>!"
    else:
        db.add_money(d["a_id"], bet)
        db.add_money(d["b_id"], bet)
        text = f"🎲 {va} : {vb} — ничья. ставки возвращены"
    bot.send_message(chat_id, text)


def start_trade(m, p):
    target = get_target(m)
    if target is None:
        return "ответь на сообщение игрока командой <b>трейд</b>"
    if target.id == m.from_user.id:
        return "сам с собой трейдиться нельзя"
    tp = db.get_player(target.id)
    if tp is None:
        return "этот игрок ещё не в игре"
    open_t = db.get_open_trade_for(m.from_user.id)
    if open_t:
        return None  # покажем существующий трейд
    tid = db.create_trade(m.from_user.id, target.id)
    return tid


# ---------------- /start и любое сообщение ----------------

@bot.message_handler(commands=["start"])
def cmd_start(m):
    uid = m.from_user.id
    existed = db.get_player(uid) is not None
    p = player_from(m)
    payload = ""
    parts = (m.text or "").split(maxsplit=1)
    if len(parts) > 1:
        payload = parts[1].strip()

    if payload.isdigit():
        ref_id = int(payload)
        if (not existed) and ref_id != uid:
            ref = db.get_player(ref_id)
            if ref is not None and db.add_ref(uid, ref_id):
                base = random.uniform(2000, 4500)
                roll = random.random()
                mult = 5.0 if roll < 0.05 else (0.2 if roll < 0.10 else 1.0)
                if premium(ref):
                    mult *= 3
                amt = int(base * mult)
                old_rank, _ = G.scam_rank(ref["scam_count"])
                db.add_money(ref_id, amt)
                db.add_bandi(ref_id, 1)
                db.execute("UPDATE players SET scam_count = scam_count + 1 WHERE user_id = ?",
                           (ref_id,))
                ref2 = db.get_player(ref_id)
                new_rank, _ = G.scam_rank(ref2["scam_count"])
                note = f"💸 по твоей скам-ссылке кто-то зашёл! +{money(amt)} и +1 💎"
                if new_rank["name"] != old_rank["name"]:
                    db.add_money(ref_id, new_rank["reward"])
                    note += (f"\n"
                             f"🔺 твой ранг повышен до «{new_rank['name']}»! "
                             f"награда за ранг: {money(new_rank['reward'])}")
                try:
                    bot.send_message(ref_id, note)
                except Exception:
                    pass

    lvl, rem, need = G.level_info(p["xp"])
    bot.reply_to(
        m,
        f"yo, <b>{display_name(p)}</b>! 🚬\n\n"
        f"это «бот бандит» — криминальная жизнь начинается здесь.\n"
        f"такси, скам, бизнесы, казино, банды и трейды — всё как в старом добром ВК-боте.\n\n"
        f"баланс: {money(p['money'])} · уровень {lvl}\n"
        f"жми кнопки или пиши «команды»",
        reply_markup=kb_main(is_admin(m.from_user.id)),
    )


def handle_pending(m, p, text: str) -> bool:
    st = STATE.get(m.from_user.id)
    if not st:
        return False
    low = text.lower().strip()
    uid = m.from_user.id

    if low in ("отмена", "cancel", "стоп"):
        STATE.pop(uid, None)
        t = db.get_trade(st.get("trade_id") or 0)
        if t and t["state"] == "open" and trade_side(t, uid):
            bot.reply_to(m, trade_cancel(t, uid))
        else:
            bot.reply_to(m, "ок.")
        return True

    if st["kind"] == "trade_money":
        t = db.get_trade(st["trade_id"])
        if not t or t["state"] != "open":
            STATE.pop(uid, None)
            bot.reply_to(m, "трейд уже закрыт")
            return True
        res = trade_add_money(t, uid, None, low)
        STATE.pop(uid, None)
        t = db.get_trade(st["trade_id"])
        bot.reply_to(m, res + "\n\n" + trade_text(t), reply_markup=kb_trade(t["id"]))
        return True

    if st["kind"] == "trade_item":
        t = db.get_trade(st["trade_id"])
        if not t or t["state"] != "open":
            STATE.pop(uid, None)
            bot.reply_to(m, "трейд уже закрыт")
            return True
        if not low.isdigit():
            bot.reply_to(m, "введи номер предмета из «шкаф» (или «отмена»)")
            return True
        res = trade_add_item(t, uid, int(low))
        STATE.pop(uid, None)
        t = db.get_trade(st["trade_id"])
        bot.reply_to(m, res + "\n\n" + trade_text(t), reply_markup=kb_trade(t["id"]))
        return True

    return False


@bot.message_handler(func=lambda m: True, content_types=["text"])
def on_text(m):
    text = (m.text or "").strip()
    if not text:
        return
    p = player_from(m)

    # админка (только для избранных id)
    if is_admin(m.from_user.id):
        if handle_admin(m, text):
            return

    # ожидания ввода (трейд) проверяем первыми
    if handle_pending(m, p, text):
        return

    low = text.lower().strip()
    parts = low.split()
    cmd = parts[0] if parts else ""
    rest = text[len(cmd):].strip()
    rest_low = rest.lower()

    def reply(txt, kb=None):
        bot.reply_to(m, txt, reply_markup=kb)

    # --- мультисловные команды ---
    if low in ("в путь", "поехали", "отправиться в путь", "за рулём", "за рулем", "ехать"):
        start_ride(m, p)
        return
    if low in ("обновить время", "обновить", "прибытие", "приехать"):
        finish_ride(m, p)
        return
    if low in ("мой транспорт", "транспорт", "гараж", "автосалон"):
        page = p["car_tier"]
        bot.reply_to(m, garage_text(p, page), reply_markup=kb_garage(p, page))
        return
    if low.startswith("заказать сырьё") or low.startswith("заказать сырье"):
        num = re.search(r"\d+", low)
        reply(order_raw(p, int(num.group()) if num else 10))
        return
    if low.startswith("сырьё") or low.startswith("сырье"):
        num = re.search(r"\d+", low)
        reply(order_raw(p, int(num.group()) if num else 10))
        return
    if low.startswith("купить бизнес"):
        num = re.search(r"\d+", low)
        reply(buy_business(p, int(num.group()) - 1 if num else -1))
        return
    if low == "купить дом" or low.startswith("купить дом"):
        card, kb = house_card(p)
        nxt = p["house_tier"] + 1
        if nxt < len(G.HOUSES):
            reply(buy_house(p))
        else:
            reply(card, kb)
        return

    # --- однословные и с аргументами ---
    if cmd in ("профиль", "я", "анкета", "me"):
        send_profile(m, p)
        return
    if cmd in ("работа", "работы", "заработок"):
        reply("💼 <b>работа</b> — выбирай, чем бабосы делать:", kb_jobs())
        return
    if cmd in ("такси", "извоз", "таксист"):
        p = process_business(p)
        reply(taxi_card(p), kb_taxi())
        return
    if cmd in ("скам", "скамер"):
        reply(scam_card(p))
        return
    if cmd == "бизнес":
        reply(business_card(p), kb_business(p))
        return
    if cmd == "забрать":
        reply(withdraw_business(p))
        return
    if cmd in ("казино", "ace", "казик"):
        reply("🎰 <b>казино «лас-вегас»</b>\n\nставь ответственно. или нет — мне всё равно 😎", kb_casino())
        return
    if cmd == "слоты":
        bet = parse_amount(rest_low, p["money"]) if rest_low else None
        if not bet:
            reply("сколько ставим? пример: <b>слоты 500</b> / <b>слоты всё</b> / <b>слоты половина</b>")
            return
        reply("🎰 крутим…\n\n" + slots_text(p, bet))
        return
    if cmd == "рулетка":
        tokens = rest_low.split()
        if len(tokens) >= 2:
            bet = parse_amount(tokens[0], p["money"])
            kind = " ".join(tokens[1:])
        elif len(tokens) == 1:
            bet = None
            kind = tokens[0]
        else:
            bet = None
            kind = ""
        if not bet:
            reply("пример: <b>рулетка 500 красное</b> / <b>рулетка всё 17</b> / <b>рулетка 200 дюжина2</b>")
            return
        reply("🎯 крутим рулетку…\n\n" + roulette_spin(p, bet, kind))
        return
    if cmd == "кости":
        bet = parse_amount(rest_low.split()[0] if rest_low else "", p["money"]) if rest_low else None
        if not bet:
            reply("пример: <b>кости 500</b> (против бота) или ответь на сообщение: <b>кости 500</b>")
            return
        if m.reply_to_message and m.reply_to_message.from_user:
            text_out, duel_id = duel_challenge(m, p, bet)
            if duel_id:
                bot.reply_to(m, text_out, reply_markup=kb_duel(duel_id))
            else:
                reply(text_out)
        else:
            reply(dice_vs_bot(p, bet))
        return
    if cmd in ("шмот", "магазин", "одежда"):
        reply(shop_list_text(), kb_shop())
        return
    if cmd == "шкаф":
        reply(wardrobe_text(p))
        return
    if cmd == "купить":
        if not rest_low:
            reply("что купить? «купить 3» — шмот под номером, «купить бизнес 2», «купить дом»")
            return
        reply(buy_clothes(p, rest_low))
        return
    if cmd in ("надеть", "надетье"):
        if not rest_low:
            reply("что надеть? смотри номера в «шкаф»: <b>надеть 2</b>")
            return
        reply(wear_item(p, rest_low))
        return
    if cmd == "снять":
        db.execute("UPDATE players SET equip = '' WHERE user_id = ?", (p["user_id"],))
        reply("👕 разделся")
        return
    if cmd == "дом":
        card, kb = house_card(p)
        reply(card, kb)
        return
    if cmd == "ник":
        newnick = rest.strip()
        if not (1 <= len(newnick) <= 24):
            reply("ник — от 1 до 24 символов")
            return
        if any(ord(ch) < 32 for ch in newnick):
            reply("в нике нельзя управляющие символы")
            return
        db.execute("UPDATE players SET nick = ? WHERE user_id = ?", (newnick, p["user_id"]))
        reply(f"ник изменён на «{esc(newnick)}»")
        return
    if cmd == "передать":
        reply(do_transfer(m, p, text))
        return
    if cmd == "топ":
        reply(do_top())
        return
    if cmd == "бонус":
        txt, _ = do_bonus(p)
        reply(txt)
        return
    if cmd == "промо":
        if not rest_low:
            reply("пример: <b>промо СТАРТ</b>")
            return
        reply(do_promo(p, rest_low))
        return
    if cmd in ("реферал", "рефка", "реф", "ref"):
        reply(scam_card(p))
        return
    if cmd == "премиум":
        prem_txt = (
            "👑 <b>премиум</b>\n\n"
            "что даёт:\n"
            "• скам ×3 к выхлопу\n"
            "• бизнес +25% к скорости и доходу\n"
            "• бонус ×2\n"
            "• личный самолёт shamal в такси (1000 поездок)\n"
            "• значок 👑 в профиле\n"
            f"• 30 дней\n\n"
            f"цена: {money(G.PREMIUM_PRICE_MONEY)} или {G.PREMIUM_PRICE_BANDI} 💎"
        )
        reply(prem_txt, kb_premium())
        return
    if cmd == "настройки":
        reply("⚙️ <b>настройки</b>", kb_settings(p))
        return
    if cmd == "глянуть":
        if not (m.reply_to_message and m.reply_to_message.from_user):
            reply("ответь на сообщение игрока: <b>глянуть</b>")
            return
        tp = db.get_player(m.reply_to_message.from_user.id)
        if tp is None:
            reply("этот игрок ещё не в игре")
            return
        send_profile(m, p, target_p=tp)
        return
    if cmd == "трейд":
        tid = start_trade(m, p)
        if tid is None:
            t = db.get_open_trade_for(m.from_user.id)
            if t:
                reply(trade_text(t), kb_trade(t["id"]))
            else:
                reply("ответь на сообщение игрока: <b>трейд</b>")
            return
        if isinstance(tid, str):
            reply(tid)
            return
        t = db.get_trade(tid)
        bot.reply_to(m, trade_text(t), reply_markup=kb_trade(t["id"]))
        return
    if cmd == "готов":
        t = db.get_open_trade_for(m.from_user.id)
        if not t:
            reply("нет активного трейда")
            return
        result = trade_ready(t, m.from_user.id)
        t2 = db.get_trade(t["id"])
        if t2["state"] == "done":
            bot.reply_to(m, result)
        else:
            reply(result + "\n\n" + trade_text(t2), kb_trade(t2["id"]))
        return
    if cmd == "отмена":
        t = db.get_open_trade_for(m.from_user.id)
        if not t:
            reply("нет активного трейда\nкстати, в трейде свои кнопки — ими и жми 🙂")
            return
        reply(trade_cancel(t, m.from_user.id))
        return
    if cmd == "отдать":
        t = db.get_open_trade_for(m.from_user.id)
        if not t:
            reply("нет активного трейда. ответь на сообщение игрока: <b>трейд</b>")
            return
        m2 = re.match(r"отдать\s+(?:предмет|вещь|шмот)\s+(\d+)", low)
        if m2:
            reply(trade_add_item(t, m.from_user.id, int(m2.group(1))))
            t2 = db.get_trade(t["id"])
            bot.reply_to(m, trade_text(t2), reply_markup=kb_trade(t2["id"]))
            return
        arg = low[len("отдать"):].strip()
        reply(trade_add_money(t, m.from_user.id, None, arg))
        t2 = db.get_trade(t["id"])
        bot.reply_to(m, trade_text(t2), reply_markup=kb_trade(t2["id"]))
        return
    if cmd == "банда":
        sub = rest_low.split()[0] if rest_low else ""
        arg = rest[len(sub):].strip() if sub else ""
        if not sub:
            reply(gang_card(p))
            return
        if sub == "создать":
            reply(gang_create(p, arg))
            return
        if sub in ("вступить", "войти"):
            reply(gang_join(p, arg))
            return
        if sub in ("выход", "выйти", "покинуть"):
            reply(gang_leave(p))
            return
        if sub in ("пополнить", "внести"):
            amt = parse_amount(arg, p["money"])
            if not amt:
                reply("пример: <b>банда пополнить 5000</b>")
                return
            if p["gang_id"] == 0:
                reply("ты не в банде")
                return
            if db.spend_money(p["user_id"], amt):
                db.execute("UPDATE gangs SET treasury = treasury + ? WHERE id = ?",
                           (amt, p["gang_id"]))
                reply(f"🏴 пополнил банду на {money(amt)}")
            else:
                reply("не хватает денег")
            return
        if sub in ("снять", "забрать"):
            gang = db.get_gang(p["gang_id"])
            if not gang:
                reply("ты не в банде")
                return
            if gang["owner_id"] != m.from_user.id:
                reply("только лидер может снимать из казны")
                return
            amt = parse_amount(arg, gang["treasury"])
            if not amt:
                reply("пример: <b>банда снять 5000</b>")
                return
            amt = min(amt, gang["treasury"])
            db.execute("UPDATE gangs SET treasury = treasury - ? WHERE id = ?",
                       (amt, gang["id"]))
            db.add_money(m.from_user.id, amt)
            reply(f"🏴 снял из банды {money(amt)}")
            return
        reply("не понял. <b>банда</b> — карточка, <b>банда создать X</b>, <b>банда вступить X</b>, <b>банда выход</b>")
        return
    if cmd in ("команды", "помощь", "help", "commands"):
        reply(help_text())
        return
    if cmd in ("меню", "старт"):
        reply("🚬 что делаем, бандит?", kb_main(is_admin(m.from_user.id)))
        return

    # неизвестное сообщение: молча реагируем только в лс
    if m.chat.type == "private":
        reply("не понял 🤔 напиши «команды» — там всё расписано", kb_main(is_admin(m.from_user.id)))


def buy_house(p):
    nxt = p["house_tier"] + 1
    if nxt >= len(G.HOUSES):
        return "больше некуда расширяться 👑"
    nxt_h = G.HOUSES[nxt]
    if not db.spend_money(p["user_id"], nxt_h["price"]):
        return f"не хватает денег. «{esc(nxt_h['name'])}» стоит {money(nxt_h['price'])}"
    db.execute("UPDATE players SET house_tier = ? WHERE user_id = ?", (nxt, p["user_id"]))
    return f"🏠 куплен дом: <b>{esc(nxt_h['name'])}</b>! теперь такси платит на {int(nxt_h['bonus']*100)}% больше"


# ---------------- колбэки ----------------

@bot.callback_query_handler(func=lambda c: True)
def on_callback(c):
    data = c.data or ""
    m = c.message
    p = db.ensure_player(c.from_user.id, c.from_user.username or "",
                         G.START_MONEY, default_nick())
    try:
        if data.startswith("adm:") and not is_admin(c.from_user.id):
            bot.answer_callback_query(c.id, "не велено 🚫", show_alert=True)
        else:
            bot.answer_callback_query(c.id)
    except Exception:
        pass

    if data.startswith("adm:"):
        if not is_admin(c.from_user.id):
            return
        adm_wizard(c)
        return

    def send(txt, kb=None):
        bot.send_message(m.chat.id, txt, reply_markup=kb)

    if data == "menu:main":
        send("🚬 что делаем, бандит?", kb_main(is_admin(c.from_user.id)))
    elif data == "menu:profile":
        send(profile_text(p, compact=bool(db.get_settings(p).get("compact"))), kb_main(is_admin(c.from_user.id)))
    elif data == "menu:jobs":
        send("💼 <b>работа</b> — выбирай, чем бабосы делать:", kb_jobs())
    elif data == "menu:taxi":
        send(taxi_card(p), kb_taxi())
    elif data == "menu:scam":
        send(scam_card(p))
    elif data == "menu:business":
        send(business_card(p), kb_business(p))
    elif data == "menu:casino":
        send("🎰 <b>казино «лас-вегас»</b>", kb_casino())
    elif data == "menu:shop":
        send(shop_list_text(), kb_shop())
    elif data == "menu:gang":
        send(gang_card(p))
    elif data == "menu:top":
        send(do_top())
    elif data == "menu:bonus":
        txt, _ = do_bonus(p)
        send(txt)
    elif data == "menu:help":
        send(help_text())
    elif data == "taxi:ride":
        start_ride(m, p)
    elif data == "taxi:update":
        finish_ride(m, p)
    elif data.startswith("taxi:garage:"):
        page = int(data.split(":")[2])
        try:
            bot.edit_message_text(garage_text(p, page), m.chat.id, m.message_id,
                                  reply_markup=kb_garage(p, page))
        except Exception:
            send(garage_text(p, page), kb_garage(p, page))
    elif data.startswith("taxi:buy:"):
        page = int(data.split(":")[2])
        p = db.get_player(c.from_user.id)
        msg = try_buy_car(p, page)
        p = db.get_player(c.from_user.id)
        try:
            bot.edit_message_text(garage_text(p, page), m.chat.id, m.message_id,
                                  reply_markup=kb_garage(p, page))
        except Exception:
            pass
        send(msg)
    elif data.startswith("biz:order:"):
        send(order_raw(p, int(data.split(":")[2])))
    elif data == "biz:withdraw":
        send(withdraw_business(p))
    elif data.startswith("biz:buy:"):
        send(buy_business(p, int(data.split(":")[2])))
    elif data.startswith("casino:slots:"):
        arg = data.split(":")[2]
        bet = p["money"] if arg == "all" else int(arg)
        bet = max(1, min(bet, p["money"]))
        send("🎰 крутим…\n\n" + slots_text(p, bet))
    elif data.startswith("casino:dice:"):
        bet = int(data.split(":")[2])
        bet = max(1, min(bet, p["money"]))
        bot.send_dice(m.chat.id, emoji="🎲")
        send(dice_vs_bot(p, bet))
    elif data == "casino:roulette_help":
        send("🎯 <b>рулетка</b>\n\n"
             "<b>рулетка 500 красное</b> / <b>чёрное</b> / <b>чёт</b> / <b>нечет</b> / <b>1-18</b> / <b>19-36</b>\n"
             "<b>рулетка 200 дюжина1</b> (1–12) / <b>дюжина2</b> (13–24) / <b>дюжина3</b> (25–36)\n"
             "<b>рулетка 100 17</b> — ставка на число 0–36 (выплата ×36)\n"
             "<b>рулетка всё зеро</b>\n\n"
             "красное/чёрное/чёт/нечет/1-18/19-36 — ×2 · дюжины — ×3 · число — ×36")
    elif data.startswith("shop:buy:"):
        send(buy_clothes(p, data.split(":")[2]))
    elif data.startswith("trade:money:"):
        tid = int(data.split(":")[2])
        t = db.get_trade(tid)
        if not t or t["state"] != "open" or not trade_side(t, c.from_user.id):
            send("трейд закрыт или ты не участник")
            return
        STATE[c.from_user.id] = {"kind": "trade_money", "trade_id": tid}
        send("сколько добавить? напиши сумму (или «всё»). «отмена» — передумал")
    elif data.startswith("trade:item:"):
        tid = int(data.split(":")[2])
        t = db.get_trade(tid)
        if not t or t["state"] != "open" or not trade_side(t, c.from_user.id):
            send("трейд закрыт или ты не участник")
            return
        STATE[c.from_user.id] = {"kind": "trade_item", "trade_id": tid}
        send(wardrobe_text(p) + "\n\nвведи номер предмета (или «отмена»)")
    elif data.startswith("trade:ready:"):
        tid = int(data.split(":")[2])
        t = db.get_trade(tid)
        if not t or t["state"] != "open" or not trade_side(t, c.from_user.id):
            send("трейд закрыт или ты не участник")
            return
        result = trade_ready(t, c.from_user.id)
        t2 = db.get_trade(tid)
        if t2["state"] == "done":
            send(result)
        else:
            send(result + "\n\n" + trade_text(t2), kb_trade(tid))
    elif data.startswith("trade:cancel:"):
        tid = int(data.split(":")[2])
        t = db.get_trade(tid)
        if not t or t["state"] != "open" or not trade_side(t, c.from_user.id):
            send("трейд закрыт или ты не участник")
            return
        send(trade_cancel(t, c.from_user.id))
    elif data.startswith("duel:accept:"):
        duel_id = int(data.split(":")[2])
        d, err = duel_accept(duel_id, c.from_user.id)
        if err:
            send(err)
            return
        duel_finish(m.chat.id, d)
    elif data.startswith("duel:decline:"):
        duel_id = int(data.split(":")[2])
        d = db.fetchone("SELECT * FROM duels WHERE id = ?", (duel_id,))
        if d and d["state"] == "open" and d["b_id"] == c.from_user.id:
            db.execute("UPDATE duels SET state = 'declined' WHERE id = ?", (duel_id,))
            send("🏳️ вызов отклонён")
        else:
            send("вызов уже неактуален")
    elif data == "premium:buy:money":
        if db.spend_money(p["user_id"], G.PREMIUM_PRICE_MONEY):
            db.buy_premium(p["user_id"], G.PREMIUM_DAYS)
            send("👑 премиум активирован на 30 дней! наслаждайся: скам ×3, бизнес +25%, бонус ×2, самолёт shamal")
        else:
            send(f"не хватает денег. нужно {money(G.PREMIUM_PRICE_MONEY)}")
    elif data == "premium:buy:bandi":
        if db.spend_bandi(p["user_id"], G.PREMIUM_PRICE_BANDI):
            db.buy_premium(p["user_id"], G.PREMIUM_DAYS)
            send("👑 премиум активирован на 30 дней! наслаждайся: скам ×3, бизнес +25%, бонус ×2, самолёт shamal")
        else:
            send(f"не хватает банди. нужно {G.PREMIUM_PRICE_BANDI} 💎")
    elif data == "settings:toggle:compact":
        s = db.get_settings(p)
        db.set_setting(p["user_id"], "compact", not bool(s.get("compact")))
        p = db.get_player(p["user_id"])
        try:
            bot.edit_message_text("⚙️ <b>настройки</b>\n\nобновил ✅", m.chat.id, m.message_id,
                                  reply_markup=kb_settings(p))
        except Exception:
            send("⚙️ настройки обновлены", kb_settings(p))
    elif data == "house:buy":
        p = db.get_player(c.from_user.id)
        send(buy_house(p))
    else:
        send("неизвестная кнопка 🤔")


def main():
    try:
        me = bot.get_me()
    except Exception as e:
        print("❌ не могу достучаться до телеграма:", e)
        print()
        print("что проверить:")
        print("  1) интернет на этом устройстве (открой любой сайт)")
        print("  2) токен верный — у @botfather команда /token; скопируй заново в token.txt")
        print("  3) сеть не режется (vpn / блокировки / нет интернета в термуксе)")
        sys.exit(1)
    global BOT_USERNAME
    BOT_USERNAME = me.username
    print(f"✅ @{me.username} запущен. «бот бандит» ждёт игроков…")
    print("   Ctrl+C — остановить бота")
    db.connect()
    bot.infinity_polling(skip_pending=True, timeout=60, long_polling_timeout=60,
                         interval=1, restart_on_change=False)


if __name__ == "__main__":
    main()


# =====================================================================
# АДМИНКА (только для ADMIN_IDS, наружу не светится)
# =====================================================================

ADMIN_ID = 1398491942  # главный админ
ADMIN_IDS = {ADMIN_ID}
for _tok in os.environ.get("BANDIT_ADMINS", "").split(","):
    _tok = _tok.strip()
    if _tok.isdigit():
        ADMIN_IDS.add(int(_tok))


def is_admin(uid: int) -> bool:
    return uid in ADMIN_IDS


class AdminError(Exception):
    pass


def admin_resolve(m, tokens):
    """определяет цель админ-выдачи: ответ на сообщение либо первый токен (id / @ник).
    возвращает (строка игрока из бд, оставшиеся аргументы)"""
    if m.reply_to_message and m.reply_to_message.from_user:
        p = db.get_player(m.reply_to_message.from_user.id)
        if not p:
            raise AdminError("игрок не в игре")
        return p, tokens
    if not tokens:
        raise AdminError("укажи цель: id или @ник (или ответь на сообщение игрока)")
    tok = tokens[0]
    if tok.startswith("@"):
        uname = tok[1:].lower()
        row = db.fetchone("SELECT user_id FROM players WHERE lower(username) = ?", (uname,))
        if not row:
            raise AdminError(f"игрок @{uname} не найден среди играющих")
        return db.get_player(row["user_id"]), tokens[1:]
    if tok.isdigit():
        p = db.get_player(int(tok))
        if not p:
            raise AdminError(f"игрок id {tok} не найден")
        return p, tokens[1:]
    raise AdminError("цель: id или @ник (или ответь на сообщение)")


def _num(args, i, default=None):
    try:
        return int(args[i])
    except (IndexError, ValueError):
        return default


def _give_all(t):
    uid = t["user_id"]
    db.execute("UPDATE players SET money = money + ?, bandi = bandi + ?, xp = xp + ?, "
               "rides = rides + 1000, car_tier = ?, house_tier = ?, "
               "business_tier = 5, business_level = ?, business_raw = 0, business_done = 0, "
               "business_xp = 0, business_last = ? "
               "WHERE user_id = ?",
               (100_000_000, 1000, 10000, len(G.CARS) - 1, len(G.HOUSES) - 1,
                G.BUSINESS_MAX_LEVEL, now(), uid))
    db.buy_premium(uid, 365)
    for c in G.CLOTHES:
        db.add_item(uid, c["id"], 1)
    return ("🎁 выдано ВСЁ: $100.000.000, 1000 💎, 10000 опыта, 1000 поездок, "
            "личный самолёт shamal ✈, замок «бандит» 🏰, элитный бордель (ур. 15), "
            "весь шмот по 1 штуке, премиум на 365 дней. аппетита 😈")


def admin_apply(admin_uid: int, t, what: str, args) -> str:
    uid = t["user_id"]
    nick = esc(t["nick"])

    if what == "all":
        return _give_all(t)

    if what in ("money", "деньги", "бабки", "cash"):
        amt = _num(args, 0)
        if amt is None:
            return "сколько? пример: <b>выдать деньги @ник 100000</b> (минус — забрать)"
        db.execute("UPDATE players SET money = MAX(0, money + ?) WHERE user_id = ?", (amt, uid))
        verb = "выдал" if amt >= 0 else "забрал"
        p2 = db.get_player(uid)
        return f"💰 {verb} {money(abs(amt))} → «{nick}» (баланс: {money(p2['money'])})"

    if what in ("bandi", "банди", "алмазы", "гемы"):
        amt = _num(args, 0)
        if amt is None:
            return "сколько? пример: <b>выдать банди @ник 100</b>"
        db.execute("UPDATE players SET bandi = MAX(0, bandi + ?) WHERE user_id = ?", (amt, uid))
        verb = "выдал" if amt >= 0 else "забрал"
        p2 = db.get_player(uid)
        return f"💎 {verb} {abs(amt)} банди → «{nick}» (теперь: {G.fmt_num(p2['bandi'])})"

    if what in ("xp", "опыт", "exp", "expу"):
        amt = _num(args, 0)
        if amt is None:
            return "сколько? пример: <b>выдать опыт @ник 5000</b>"
        db.execute("UPDATE players SET xp = MAX(0, xp + ?) WHERE user_id = ?", (amt, uid))
        lvl, rem, need = G.level_info(db.get_player(uid)["xp"])
        return f"⭐ опыт {'+' if amt >= 0 else ''}{amt} → «{nick}» (уровень {lvl}, {rem}/{need})"

    if what in ("premium", "премиум"):
        days = _num(args, 0, 30)
        if days >= 0:
            base = max(t["premium_until"], now())
            db.execute("UPDATE players SET premium_until = ? WHERE user_id = ?",
                       (base + days * 86400, uid))
            return f"👑 премиум +{days} дн → «{nick}» (до {pretty_date(db.get_player(uid)['premium_until'])})"
        db.execute("UPDATE players SET premium_until = MAX(0, premium_until + ?) WHERE user_id = ?",
                   (days * 86400, uid))
        return f"👑 премиум {days} дн (забрано) → «{nick}»"

    if what in ("item", "предмет", "шмот", "вещь"):
        if not args:
            return "что выдать? пример: <b>выдать предмет @ник chain 3</b> или <b>… всё</b>"
        tok = args[0].lower()
        qty = _num(args, 1, 1) or 1
        if tok in ("всё", "все", "all"):
            for c in G.CLOTHES:
                db.add_item(uid, c["id"], qty)
            return f"👕 выдал весь шмот ×{qty} → «{nick}»"
        item = G.CLOTHES_BY_ID.get(tok)
        if item is None:
            low = tok
            for c in G.CLOTHES:
                if low in c["id"] or low in c["name"].lower():
                    item = c
                    break
        if item is None:
            ids = ", ".join(c["id"] for c in G.CLOTHES)
            return f"не нашёл предмет «{esc(tok)}». id: {ids}"
        db.add_item(uid, item["id"], qty)
        return f"👕 выдал «{esc(item['name'])}» ×{qty} → «{nick}»"

    if what in ("car", "машина", "авто", "тачка"):
        tier = _num(args, 0)
        if tier is None:
            return f"какой тир? 0..{len(G.CARS) - 1}. пример: <b>выдать машина @ник 12</b>"
        tier = max(0, min(tier, len(G.CARS) - 1))
        db.execute("UPDATE players SET car_tier = ? WHERE user_id = ?", (tier, uid))
        return f"🚕 выдал «{esc(G.CARS[tier]['name'])}» → «{nick}»"

    if what in ("house", "дом"):
        tier = _num(args, 0)
        if tier is None:
            return f"какой тир? 0..{len(G.HOUSES) - 1}. пример: <b>выдать дом @ник 5</b>"
        tier = max(0, min(tier, len(G.HOUSES) - 1))
        db.execute("UPDATE players SET house_tier = ? WHERE user_id = ?", (tier, uid))
        return f"🏠 выдал «{esc(G.HOUSES[tier]['name'])}» → «{nick}»"

    if what in ("business", "бизнес"):
        tier = _num(args, 0)
        if tier is None:
            return "какой тир? -1 убрать, 0..5 (5 = элитный бордель), потом уровень 1..15"
        if tier == -1:
            db.execute("UPDATE players SET business_tier = -1, business_level = 1, "
                       "business_raw = 0, business_done = 0, business_xp = 0 WHERE user_id = ?", (uid,))
            return f"🏢 бизнес убран → «{nick}»"
        tier = max(0, min(tier, len(G.BUSINESSES) - 1))
        level = _num(args, 1, 1) or 1
        level = max(1, min(level, G.BUSINESS_MAX_LEVEL))
        db.execute("UPDATE players SET business_tier = ?, business_level = ?, "
                   "business_xp = 0, business_last = ? WHERE user_id = ?",
                   (tier, level, now(), uid))
        return f"🏢 выдал «{esc(G.BUSINESSES[tier]['name'])}» ур. {level} → «{nick}»"

    if what in ("rides", "поездки", "рейсы"):
        n = _num(args, 0, 0)
        db.execute("UPDATE players SET rides = MAX(0, rides + ?) WHERE user_id = ?", (n, uid))
        p2 = db.get_player(uid)
        return f"🚕 поездки {'+' if n >= 0 else ''}{n} → «{nick}» (всего {p2['rides']})"

    if what in ("nick", "ник"):
        newnick = " ".join(args).strip()
        if not (1 <= len(newnick) <= 24):
            return "ник — от 1 до 24 символов"
        if any(ord(ch) < 32 for ch in newnick):
            return "в нике нельзя управляющие символы"
        db.execute("UPDATE players SET nick = ? WHERE user_id = ?", (newnick, uid))
        return f"✏️ «{nick}» теперь «{esc(newnick)}»"

    return ("что выдаём? варианты: <b>деньги · банди · опыт · премиум · предмет · машина · "
            "дом · бизнес · поездки · ник · всё</b>\n"
            "пример: <b>выдать деньги @ник 100000</b>")


WHAT_MAP = {
    "деньги": "money", "money": "money", "бабки": "money", "cash": "money",
    "банди": "bandi", "bandi": "bandi", "алмазы": "bandi", "гемы": "bandi",
    "опыт": "xp", "xp": "xp", "exp": "xp",
    "премиум": "premium", "premium": "premium",
    "предмет": "item", "item": "item", "шмот": "item", "вещь": "item",
    "машина": "car", "авто": "car", "car": "car", "тачка": "car",
    "дом": "house", "house": "house",
    "бизнес": "business", "business": "business",
    "поездки": "rides", "рейсы": "rides", "rides": "rides",
    "ник": "nick", "nick": "nick",
    "всё": "all", "все": "all", "all": "all",
}


def admin_prompt(kind: str) -> str:
    prompts = {
        "money": "кому и сколько? примеры: <b>@ник 100000</b> · <b>1398491942 50000</b>\nили ответь на сообщение и пиши только сумму (можно с минусом)",
        "bandi": "кому и сколько банди? пример: <b>@ник 100</b>\nили ответь на сообщение и пиши только число",
        "xp": "кому и сколько опыта? пример: <b>@ник 5000</b>\nили ответь на сообщение и пиши только число",
        "premium": "кому и на сколько дней? пример: <b>@ник 30</b>\nминус — забрать премиум. или ответь на сообщение и пиши только дни",
        "item": "что и кому? формат: <b>цель предмет [кол-во]</b>\nпример: <b>@ник chain 3</b> · <b>@ник всё</b> — весь шмот",
        "car": f"кому и какой тир? <b>@ник 12</b> (0..{len(G.CARS) - 1}, 12 = shamal ✈)\nили ответь на сообщение и пиши только тир",
        "house": f"кому и какой тир? <b>@ник 5</b> (0..{len(G.HOUSES) - 1}, 5 = замок «бандит»)\nили ответь на сообщение и пиши только тир",
        "business": "кому и какой бизнес? <b>цель тир [уровень]</b>\nтир: -1 убрать · 0..5 (5 = элитный бордель) · уровень 1..15",
        "rides": "кому и сколько поездок добавить? <b>@ник 100</b>\nили ответь на сообщение и пиши только число",
    }
    return prompts.get(kind, "что выдаём?")


def kb_admin():
    kb = types.InlineKeyboardMarkup(row_width=3)
    kb.add(
        types.InlineKeyboardButton("💰 деньги", callback_data="adm:money"),
        types.InlineKeyboardButton("💎 банди", callback_data="adm:bandi"),
        types.InlineKeyboardButton("⭐ опыт", callback_data="adm:xp"),
    )
    kb.add(
        types.InlineKeyboardButton("👑 премиум", callback_data="adm:premium"),
        types.InlineKeyboardButton("🚕 поездки", callback_data="adm:rides"),
        types.InlineKeyboardButton("👕 предмет", callback_data="adm:item"),
    )
    kb.add(
        types.InlineKeyboardButton("🚗 машина", callback_data="adm:car"),
        types.InlineKeyboardButton("🏠 дом", callback_data="adm:house"),
        types.InlineKeyboardButton("🏢 бизнес", callback_data="adm:business"),
    )
    kb.add(
        types.InlineKeyboardButton("🎁 выдать ВСЁ", callback_data="adm:all"),
        types.InlineKeyboardButton("📊 статистика", callback_data="adm:stats"),
        types.InlineKeyboardButton("🔄 сброс", callback_data="adm:reset"),
    )
    return kb


def admin_stats() -> str:
    row = db.fetchone("SELECT COUNT(*) AS c, COALESCE(SUM(money),0) AS m, "
                      "COALESCE(SUM(rides),0) AS r, COALESCE(SUM(bandi),0) AS b FROM players")
    gangs = db.fetchone("SELECT COUNT(*) AS c FROM gangs")
    trades = db.fetchone("SELECT COUNT(*) AS c FROM trades WHERE state = 'open'")
    prem = db.fetchone("SELECT COUNT(*) AS c FROM players WHERE premium_until > ?", (now(),))
    return (
        "📊 <b>статистика бота</b>\n\n"
        f"игроков: {row['c']}\n"
        f"👑 премиумов: {prem['c']}\n"
        f"💰 денег в экономике: {money(row['m'])}\n"
        f"💎 банди всего: {G.fmt_num(row['b'])}\n"
        f"🚕 поездок наезжено: {G.fmt_num(row['r'])}\n"
        f"🏴 банд: {gangs['c']}\n"
        f"🤝 открытых трейдов: {trades['c']}\n"
        f"🗄 база: <code>{esc(os.path.basename(db.DB_PATH))}</code>"
    )


def wipe_player(uid: int):
    with db.transaction():
        # вернуть эскроу открытых трейдов
        open_trades = db.fetchall(
            "SELECT * FROM trades WHERE state = 'open' AND (a_id = ? OR b_id = ?)",
            (uid, uid),
        )
        for t in open_trades:
            trade_cancel(t, uid)
        # распустить банду, если лидер
        owned = db.fetchall("SELECT id FROM gangs WHERE owner_id = ?", (uid,))
        for g in owned:
            db.execute("UPDATE players SET gang_id = 0 WHERE gang_id = ?", (g["id"],))
            db.execute("DELETE FROM gangs WHERE id = ?", (g["id"],))
        db.execute("DELETE FROM inventory WHERE user_id = ?", (uid,))
        db.execute("DELETE FROM promos_used WHERE user_id = ?", (uid,))
        db.execute("DELETE FROM refs WHERE referee_id = ? OR referrer_id = ?", (uid, uid))
        db.execute("DELETE FROM players WHERE user_id = ?", (uid,))


def admin_consume(m, st, text: str) -> bool:
    uid = m.from_user.id
    kind = st["kind"][len("admin:"):]
    tokens = text.split()

    if kind == "reset_confirm":
        STATE.pop(uid, None)
        if text.lower().strip() in ("да", "yes", "y", "д"):
            target_id = st["target"]
            wipe_player(target_id)
            bot.reply_to(m, f"🔄 игрок id {target_id} полностью сброшен")
        else:
            bot.reply_to(m, "ок, никого не трогаю")
        return True

    try:
        target, args = admin_resolve(m, tokens)
    except AdminError as e:
        bot.reply_to(m, "🚫 " + str(e) + "\n(или «отмена»)")
        return True

    if kind == "reset":
        STATE[uid] = {"kind": "admin:reset_confirm", "target": target["user_id"]}
        bot.reply_to(m, f"точно сбросить «{esc(target['nick'])}» (id {target['user_id']})?\n"
                        f"удалится весь прогресс, шмот и банда. напиши «да»")
        return True

    msg = admin_apply(uid, target, kind, args)
    STATE.pop(uid, None)
    bot.reply_to(m, msg)
    return True


def handle_admin(m, text: str) -> bool:
    """админские команды; True — сообщение обработано и съедено"""
    uid = m.from_user.id
    low = text.lower().strip()
    parts = low.split()
    cmd = parts[0] if parts else ""
    rest = text.split(maxsplit=1)[1] if len(parts) > 1 else ""

    # ожидание ввода для админки
    st = STATE.get(uid)
    if st and str(st.get("kind", "")).startswith("admin:"):
        if low in ("отмена", "cancel"):
            STATE.pop(uid, None)
            bot.reply_to(m, "ок, отменил")
            return True
        if st["kind"] == "admin:wiz":
            return wizard_consume(m, st, text)
        return admin_consume(m, st, text)

    if cmd == "админка":
        bot.reply_to(m, "👑 <b>админка бандита</b>\n\nвыбирай кнопку или пиши команды:\n"
                        "<b>выдать деньги/банди/опыт/премиум/предмет/машина/дом/бизнес/поездки/ник/всё</b>\n"
                        "<b>сброс</b> — вайп игрока · <b>статистика</b> — цифры бота",
                     reply_markup=kb_admin())
        return True

    if cmd == "статистика":
        bot.reply_to(m, admin_stats())
        return True

    if cmd == "сброс":
        tokens = rest.split() if rest else []
        try:
            target, _ = admin_resolve(m, tokens)
        except AdminError as e:
            bot.reply_to(m, "🚫 " + str(e))
            return True
        STATE[uid] = {"kind": "admin:reset_confirm", "target": target["user_id"]}
        bot.reply_to(m, f"точно сбросить «{esc(target['nick'])}» (id {target['user_id']})?\n"
                        f"удалится весь прогресс, шмот и банда. напиши «да»")
        return True

    if cmd == "выдать":
        sub = rest.split(maxsplit=1)
        what_ru = sub[0].lower() if sub else ""
        argstr = sub[1] if len(sub) > 1 else ""
        what = WHAT_MAP.get(what_ru)
        if what is None:
            bot.reply_to(m, "что выдаём? <b>деньги · банди · опыт · премиум · предмет · машина · "
                            "дом · бизнес · поездки · ник · всё</b>\n"
                            "пример: <b>выдать деньги @ник 100000</b>")
            return True
        tokens = argstr.split()
        try:
            target, args = admin_resolve(m, tokens)
        except AdminError as e:
            bot.reply_to(m, "🚫 " + str(e))
            return True
        bot.reply_to(m, admin_apply(uid, target, what, args))
        return True

    return False


# =====================================================================
# АДМИНКА — чистый кнопко-интерфейс (ни одного текста)
# =====================================================================

AMOUNT_PRESETS = {
    "money":   [("+10К", 10_000), ("+100К", 100_000), ("+1М", 1_000_000),
                ("+10М", 10_000_000), ("+100М", 100_000_000),
                ("−10К", -10_000), ("−100К", -100_000), ("−1М", -1_000_000),
                ("забрать всё", "-all")],
    "bandi":   [("+10", 10), ("+50", 50), ("+100", 100), ("+500", 500), ("+1000", 1000),
                ("−50", -50), ("−100", -100), ("−500", -500)],
    "xp":      [("+500", 500), ("+2500", 2500), ("+10К", 10_000),
                ("+50К", 50_000), ("−10К", -10_000)],
    "premium": [("+7 дней", 7), ("+30 дней", 30), ("+90 дней", 90),
                ("+365 дней", 365), ("−30 (забрать)", -30)],
    "rides":   [("+10", 10), ("+50", 50), ("+100", 100),
                ("+1000", 1000), ("−100", -100)],
}

AMT_PROMPT = {
    "money":   "сколько денег выдаём?",
    "bandi":   "сколько банди выдаём?",
    "xp":      "сколько опыта выдаём?",
    "premium": "на сколько дней продливаем премиум?",
    "rides":   "сколько поездок добавляем?",
}

WIZ_ANY = ("money", "bandi", "xp", "premium", "rides")


def kb_amount(what: str):
    kb = types.InlineKeyboardMarkup(row_width=3)
    kb.add(*[types.InlineKeyboardButton(label, callback_data=f"adm:amt:{val}")
             for label, val in AMOUNT_PRESETS[what]])
    kb.add(
        types.InlineKeyboardButton("✏️ своё число", callback_data="adm:custom"),
        types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"),
    )
    return kb


def kb_cars():
    kb = types.InlineKeyboardMarkup(row_width=2)
    for i, car in enumerate(G.CARS):
        kb.add(types.InlineKeyboardButton(f"{i} {car['name'][:18]}",
                                          callback_data=f"adm:carsel:{i}"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"))
    return kb


def kb_houses():
    kb = types.InlineKeyboardMarkup(row_width=2)
    for i, house in enumerate(G.HOUSES):
        kb.add(types.InlineKeyboardButton(f"{i} {house['name'][:20]}",
                                          callback_data=f"adm:housesel:{i}"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"))
    return kb


def kb_businesses():
    kb = types.InlineKeyboardMarkup(row_width=2)
    for i, b in enumerate(G.BUSINESSES):
        kb.add(types.InlineKeyboardButton(f"{i} {b['name'][:20]}",
                                          callback_data=f"adm:bizsel:{i}"))
    kb.add(types.InlineKeyboardButton("➖ убрать бизнес", callback_data="adm:bizsel:-1"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"))
    return kb


def kb_biz_levels():
    kb = types.InlineKeyboardMarkup(row_width=4)
    kb.add(*[types.InlineKeyboardButton(f"ур. {lvl}", callback_data=f"adm:bizlvl:{lvl}")
             for lvl in (1, 5, 10, 15)])
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"))
    return kb


def kb_items():
    kb = types.InlineKeyboardMarkup(row_width=2)
    for c in G.CLOTHES:
        kb.add(types.InlineKeyboardButton(c["name"][:22],
                                          callback_data=f"adm:itemid:{c['id']}"))
    kb.add(types.InlineKeyboardButton("🎁 весь шмот", callback_data="adm:itemid:all"))
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"))
    return kb


def kb_qty():
    kb = types.InlineKeyboardMarkup(row_width=5)
    kb.add(*[types.InlineKeyboardButton(f"×{q}", callback_data=f"adm:qty:{q}")
             for q in (1, 3, 5, 10, 25)])
    kb.add(types.InlineKeyboardButton("⬅️ назад", callback_data="adm:open"))
    return kb


def kb_admin_target():
    kb = types.InlineKeyboardMarkup(row_width=2)
    players = db.fetchall("SELECT user_id, nick FROM players ORDER BY xp DESC LIMIT 10")
    for row in players:
        nick = (row["nick"] or "???")[:18]
        kb.add(types.InlineKeyboardButton(f"{nick} ({row['user_id']})",
                                          callback_data=f"adm:tgt:{row['user_id']}"))
    kb.add(
        types.InlineKeyboardButton("✏️ ввести id/@ник", callback_data="adm:customtgt"),
        types.InlineKeyboardButton("❌ отмена", callback_data="adm:cancel"),
    )
    return kb


def kb_confirm():
    kb = types.InlineKeyboardMarkup(row_width=2)
    kb.add(
        types.InlineKeyboardButton("✅ да", callback_data="adm:cfmy"),
        types.InlineKeyboardButton("❌ нет", callback_data="adm:cfmn"),
    )
    return kb


def kb_admin_back():
    kb = types.InlineKeyboardMarkup(row_width=2)
    kb.add(
        types.InlineKeyboardButton("⬅️ в админку", callback_data="adm:open"),
        types.InlineKeyboardButton("🏠 в меню", callback_data="menu:main"),
    )
    return kb


def _parse_signed(s: str):
    t = s.lower().strip().replace(" ", "")
    if t in ("всё", "все", "all"):
        return "all"
    if t in ("-всё", "-все", "-all"):
        return "-all"
    if re.fullmatch(r"-?\d+", t):
        return int(t)
    return None


def wiz_apply(admin_uid: int, t, st) -> str:
    what = st["what"]

    def amt_or_all(current: int):
        amt = st.get("amount", 0)
        if amt == "all":
            return 10**15
        if amt == "-all":
            return -current
        return amt

    if what == "money":
        return admin_apply(admin_uid, t, "money", [str(amt_or_all(t["money"]))])
    if what == "bandi":
        return admin_apply(admin_uid, t, "bandi", [str(amt_or_all(t["bandi"]))])
    if what == "xp":
        return admin_apply(admin_uid, t, "xp", [str(amt_or_all(t["xp"]))])
    if what == "premium":
        return admin_apply(admin_uid, t, "premium", [str(st.get("amount", 30))])
    if what == "rides":
        return admin_apply(admin_uid, t, "rides", [str(st.get("amount", 0))])
    if what == "car":
        return admin_apply(admin_uid, t, "car", [str(st.get("tier", 0))])
    if what == "house":
        return admin_apply(admin_uid, t, "house", [str(st.get("tier", 0))])
    if what == "business":
        tier = st.get("tier", 0)
        if tier < 0:
            return admin_apply(admin_uid, t, "business", ["-1"])
        return admin_apply(admin_uid, t, "business",
                           [str(tier), str(st.get("level", 1))])
    if what == "item":
        item = st.get("item", "cap")
        qty = str(st.get("qty", 1))
        if item == "all":
            return admin_apply(admin_uid, t, "item", ["всё", qty])
        return admin_apply(admin_uid, t, "item", [item, qty])
    if what == "all":
        return admin_apply(admin_uid, t, "all", [])
    return "не знаю, что выдаём 🙂"


def wiz_finish(chat_id: int, admin_uid: int, st, target):
    what = st["what"]
    if what in ("reset", "all"):
        STATE[admin_uid] = {**st, "target": target["user_id"], "await": None}
        if what == "reset":
            txt = (f"точно сбросить «{esc(target['nick'])}» (id {target['user_id']})?\n"
                   f"удалится весь прогресс, шмот и банда.")
        else:
            txt = f"точно выдать ВСЁ игроку «{esc(target['nick'])}»?"
        bot.send_message(chat_id, txt, reply_markup=kb_confirm())
        return
    res = wiz_apply(admin_uid, target, st)
    STATE.pop(admin_uid, None)
    bot.send_message(chat_id, res, reply_markup=kb_admin_back())


def wizard_consume(m, st, text: str) -> bool:
    uid = m.from_user.id
    low = text.lower().strip()
    await_ = st.get("await")

    if await_ == "amount":
        amt = _parse_signed(low)
        if amt is None:
            bot.reply_to(m, "число (можно с минусом), «всё» или «отмена»")
            return True
        st["amount"] = amt
        st.pop("await", None)
        STATE[uid] = st
        bot.reply_to(m, "кому?", reply_markup=kb_admin_target())
        return True

    if await_ == "target":
        try:
            target, _ = admin_resolve(m, text.split())
        except AdminError as e:
            bot.reply_to(m, "🚫 " + str(e) + "\n(или ответь на сообщение игрока)")
            return True
        wiz_finish(m.chat.id, uid, st, target)
        return True

    bot.reply_to(m, "жми кнопки 🙂", reply_markup=kb_admin())
    return True


def adm_wizard(c):
    """единая точка входа всех adm:* кнопок (только после is_admin-check)"""
    m = c.message
    chat = m.chat.id
    uid = c.from_user.id
    parts = (c.data or "").split(":")
    act = parts[1] if len(parts) > 1 else ""

    def send(t, kb=None):
        bot.send_message(chat, t, reply_markup=kb)

    if act == "open":
        send("👑 <b>админка бандита</b>\n\nтолько кнопки: выбери, что выдаём", kb_admin())
        return
    if act == "stats":
        send(admin_stats(), kb_admin_back())
        return
    if act == "cancel":
        STATE.pop(uid, None)
        send("ок, отменил", kb_admin())
        return

    # --- выбор категории ---
    if act in WIZ_ANY:
        STATE[uid] = {"kind": "admin:wiz", "what": act}
        send(AMT_PROMPT[act], kb_amount(act))
        return
    if act == "car":
        STATE[uid] = {"kind": "admin:wiz", "what": "car"}
        send("какую тачку выдаём?", kb_cars())
        return
    if act == "house":
        STATE[uid] = {"kind": "admin:wiz", "what": "house"}
        send("какой дом выдаём?", kb_houses())
        return
    if act == "business":
        STATE[uid] = {"kind": "admin:wiz", "what": "business"}
        send("какой бизнес выдаём?", kb_businesses())
        return
    if act == "item":
        STATE[uid] = {"kind": "admin:wiz", "what": "item"}
        send("что выдаём?", kb_items())
        return

    # --- промежуточные шаги ---
    if act == "carsel":
        st = STATE.get(uid) or {"kind": "admin:wiz", "what": "car"}
        st.update({"what": "car", "tier": int(parts[2])})
        STATE[uid] = st
        send("кому?", kb_admin_target())
        return
    if act == "housesel":
        st = STATE.get(uid) or {"kind": "admin:wiz", "what": "house"}
        st.update({"what": "house", "tier": int(parts[2])})
        STATE[uid] = st
        send("кому?", kb_admin_target())
        return
    if act == "bizsel":
        tier = int(parts[2])
        st = STATE.get(uid) or {"kind": "admin:wiz", "what": "business"}
        st.update({"what": "business", "tier": tier})
        STATE[uid] = st
        if tier < 0:
            send("кому убираем бизнес?", kb_admin_target())
        else:
            send("какой уровень?", kb_biz_levels())
        return
    if act == "bizlvl":
        st = STATE.get(uid)
        if not st or st.get("what") != "business":
            send("сначала выбери бизнес 🙂", kb_admin())
            return
        st["level"] = int(parts[2])
        STATE[uid] = st
        send("кому?", kb_admin_target())
        return
    if act == "itemid":
        st = STATE.get(uid) or {"kind": "admin:wiz", "what": "item"}
        st.update({"what": "item", "item": parts[2]})
        STATE[uid] = st
        send("сколько штук?", kb_qty())
        return
    if act == "qty":
        st = STATE.get(uid)
        if not st or st.get("what") != "item":
            send("сначала выбери предмет 🙂", kb_admin())
            return
        st["qty"] = int(parts[2])
        STATE[uid] = st
        send("кому?", kb_admin_target())
        return
    if act == "amt":
        st = STATE.get(uid)
        if not st or st.get("what") not in WIZ_ANY:
            send("сначала выбери, что выдаём 🙂", kb_admin())
            return
        raw = parts[2]
        try:
            st["amount"] = raw if raw in ("all", "-all") else int(raw)
        except ValueError:
            send("не понял сумму", kb_admin())
            return
        STATE[uid] = st
        send("кому?", kb_admin_target())
        return

    # --- свой ввод (для тех, кому кнопки малы) ---
    if act == "custom":
        st = STATE.get(uid) or {"kind": "admin:wiz", "what": "money"}
        st.update({"kind": "admin:wiz", "await": "amount"})
        STATE[uid] = st
        send("напиши число (можно с минусом, «всё» / «-всё»). «отмена» — назад")
        return
    if act == "customtgt":
        st = STATE.get(uid)
        if not st:
            send("сначала выбери, что выдаём 🙂", kb_admin())
            return
        st["await"] = "target"
        STATE[uid] = st
        send("напиши id или @ник игрока (или ответь на его сообщение)")
        return

    # --- выбор цели ---
    if act == "tgt":
        st = STATE.get(uid)
        if not st or st.get("kind") != "admin:wiz":
            send("сначала выбери, что выдаём 🙂", kb_admin())
            return
        target = db.get_player(int(parts[2]))
        if not target:
            send("игрок не найден")
            return
        wiz_finish(chat, uid, st, target)
        return

    # --- подтверждения ---
    if act == "cfmy":
        st = STATE.pop(uid, None)
        if not st or st.get("what") not in ("reset", "all"):
            send("нечего подтверждать")
            return
        target = db.get_player(st.get("target") or 0)
        if not target:
            send("игрок не найден (может, уже сброшен?)")
            return
        if st["what"] == "reset":
            wipe_player(target["user_id"])
            send(f"🔄 игрок «{esc(target['nick'])}» (id {target['user_id']}) сброшен в полный ноль")
        else:
            send(_give_all(target))
        send("дальше?", kb_admin_back())
        return
    if act == "cfmn":
        STATE.pop(uid, None)
        send("ок, отменил", kb_admin())
        return

    # --- спецдействия ---
    if act == "reset":
        STATE[uid] = {"kind": "admin:wiz", "what": "reset"}
        send("кого сбрасываем?", kb_admin_target())
        return
    if act == "all":
        STATE[uid] = {"kind": "admin:wiz", "what": "all"}
        send("кому выдать ВСЁ? 😈", kb_admin_target())
        return

    send("неизвестная кнопка", kb_admin())
