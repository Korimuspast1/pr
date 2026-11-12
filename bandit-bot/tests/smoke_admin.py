# -*- coding: utf-8 -*-
"""Дымовой тест админки (1398491942): выдача всего, что угодно, и защита от чужих."""
import os, sys, time, types, random

os.environ["BANDIT_TOKEN"] = "123456:smoke-admin"
os.environ["BANDIT_DB"] = "/tmp/test_bandit_admin.db"
for suf in ("", "-wal", "-shm"):
    try:
        os.remove("/tmp/test_bandit_admin.db" + suf)
    except FileNotFoundError:
        pass

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bot as B
import db
import gamedata as G

ADMIN = 1398491942
sent = []

def fake_send_message(chat_id, text, **kw):
    sent.append(("msg", chat_id, text))

def fake_reply_to(m, text, **kw):
    sent.append(("reply", m.chat.id, text))

def fake_send_dice(chat_id, emoji=None):
    return types.SimpleNamespace(dice=types.SimpleNamespace(value=random.randint(1, 6)))

def fake_answer(*a, **k):
    pass

def fake_edit(text, chat_id, message_id, **kw):
    sent.append(("edit", chat_id, text))

B.bot.send_message = fake_send_message
B.bot.reply_to = fake_reply_to
B.bot.send_dice = fake_send_dice
B.bot.answer_callback_query = fake_answer
B.bot.edit_message_text = fake_edit

class U:
    def __init__(self, uid, uname):
        self.id = uid
        self.username = uname
        self.first_name = uname.title()

class Chat:
    def __init__(self, cid, ctype="private"):
        self.id = cid
        self.type = ctype

class M:
    _mid = 5000
    def __init__(self, uid, text, reply_to=None, chat_id=None, ctype="private"):
        self.from_user = U(uid, f"user{uid}")
        self.text = text
        self.reply_to_message = reply_to
        self.chat = Chat(chat_id or uid, ctype)
        M._mid += 1
        self.message_id = M._mid

def run(uid, text, reply_to=None):
    m = M(uid, text, reply_to=reply_to)
    B.on_text(m)
    return sent[-1][2] if sent else "<no reply>"

def cb(uid, data, chat_id=None):
    msg = M(uid, "cb", chat_id=chat_id)
    c = types.SimpleNamespace(id="q", data=data, message=msg, from_user=U(uid, f"user{uid}"))
    B.on_callback(c)

def last_all(n=3):
    return "\n".join(x[2] for x in sent[-n:])

fails = []
def check(name, cond):
    print(f"[{'ok' if cond else 'FAIL'}] {name}")
    if not cond:
        fails.append(name)

# игрок-жертва и админ
run(ADMIN, "/start") if False else None
m = M(ADMIN, "/start"); B.cmd_start(m)
m = M(777, "/start"); B.cmd_start(m)
p = db.get_player(777)
check("игрок создан", p is not None)
money0 = p["money"]

# --- обычный игрок НЕ админ ---
r = run(777, "админка")
check("обычный не видит админку", "админка бандита" not in r)
cb(777, "adm:money")
check("обычный не может жать админ-кнопки", db.get_player(777)["money"] == money0)
r = run(777, "выдать деньги 777 999999999")
check("обычному игроку выдать нельзя", db.get_player(777)["money"] == money0)

# --- админ выдаёт деньги ---
r = run(ADMIN, "выдать деньги 777 50000")
check("админ выдал деньги", "выдал" in r and db.get_player(777)["money"] == money0 + 50000)
r = run(ADMIN, "выдать деньги @user777 25000")
check("выдача по @нику", db.get_player(777)["money"] == money0 + 75000)
r = run(ADMIN, "выдать деньги 777 -10000")
check("забрать деньги можно", db.get_player(777)["money"] == money0 + 65000)
money_before = db.get_player(777)["money"]
r = run(ADMIN, "выдать деньги 777 нечисло")
check("мусорная сумма отклонена", "сколько" in r and db.get_player(777)["money"] == money_before)

# --- банди ---
r = run(ADMIN, "выдать банди 777 300")
check("банди выданы", db.get_player(777)["bandi"] == 300)

# --- опыт ---
r = run(ADMIN, "выдать опыт 777 10000")
check("опыт выдан", db.get_player(777)["xp"] == 10000)

# --- премиум ---
r = run(ADMIN, "выдать премиум 777 30")
p = db.get_player(777)
check("премиум выдан", db.is_premium(p))

# --- предметы ---
r = run(ADMIN, "выдать предмет 777 chain 5")
inv = {i["item_id"]: i["qty"] for i in db.get_inventory(777)}
check("предмет выдан ×5", inv.get("chain") == 5)
r = run(ADMIN, "выдать предмет 777 всё")
inv = {i["item_id"]: i["qty"] for i in db.get_inventory(777)}
check("весь шмот выдан", len(inv) == len(G.CLOTHES))

# --- машина / дом / бизнес ---
r = run(ADMIN, "выдать машина 777 12")
check("выдан shamal", db.get_player(777)["car_tier"] == 12)
r = run(ADMIN, "выдать дом 777 5")
check("выдан замок", db.get_player(777)["house_tier"] == 5)
r = run(ADMIN, "выдать бизнес 777 5 15")
p = db.get_player(777)
check("выдан элитный бордель ур.15", p["business_tier"] == 5 and p["business_level"] == 15)

# --- поездки ---
r = run(ADMIN, "выдать поездки 777 100")
check("поездки добавлены", db.get_player(777)["rides"] >= 100)

# --- ник ---
r = run(ADMIN, "выдать ник 777 мегазадрот")
check("ник изменён админом", db.get_player(777)["nick"] == "мегазадрот")

# --- выдача ответом на сообщение ---
reply_msg = M(777, "привет")
m = M(ADMIN, "выдать деньги 12345", reply_to=reply_msg)
B.on_text(m)
check("выдача ответом идёт в ответившего, а не в 12345",
      db.get_player(777)["money"] == money0 + 65000 + 12345)

# --- сброс с подтверждением ---
r = run(ADMIN, "сброс 777")
check("сброс просит подтверждение", "напиши «да»" in r)
check("до «да» игрок цел", db.get_player(777) is not None)
r = run(ADMIN, "нет")
check("отказ от сброса", db.get_player(777) is not None)
r = run(ADMIN, "сброс 777")
r = run(ADMIN, "да")
check("после «да» игрок стёрт", db.get_player(777) is None)
check("инвентарь стёрт", db.get_inventory(777) == [])

# --- кнопко-админка БЕЗ текста (только нажатия) ---
m = M(777, "/start"); B.cmd_start(m)  # пересоздаём игрока после сброса
coins_before = db.get_player(777)["money"]
cb(ADMIN, "adm:money")
cb(ADMIN, "adm:amt:100000")
cb(ADMIN, "adm:tgt:777")
check("кнопки: деньги выданы без текста", db.get_player(777)["money"] == coins_before + 100_000)
cb(ADMIN, "adm:car")
cb(ADMIN, "adm:carsel:12")
cb(ADMIN, "adm:tgt:777")
check("кнопки: выдан shamal", db.get_player(777)["car_tier"] == 12)
cb(ADMIN, "adm:house")
cb(ADMIN, "adm:housesel:5")
cb(ADMIN, "adm:tgt:777")
check("кнопки: выдан замок", db.get_player(777)["house_tier"] == 5)
cb(ADMIN, "adm:item")
cb(ADMIN, "adm:itemid:chain")
cb(ADMIN, "adm:qty:3")
cb(ADMIN, "adm:tgt:777")
inv = {i["item_id"]: i["qty"] for i in db.get_inventory(777)}
check("кнопки: выданы 3 цепи", inv.get("chain") == 3)
cb(ADMIN, "adm:business")
cb(ADMIN, "adm:bizsel:5")
cb(ADMIN, "adm:bizlvl:15")
cb(ADMIN, "adm:tgt:777")
p = db.get_player(777)
check("кнопки: элитный бордель ур.15", p["business_tier"] == 5 and p["business_level"] == 15)
cb(ADMIN, "adm:premium")
cb(ADMIN, "adm:amt:30")
cb(ADMIN, "adm:tgt:777")
check("кнопки: премиум выдан", db.is_premium(db.get_player(777)))
cb(ADMIN, "adm:rides")
cb(ADMIN, "adm:amt:100")
cb(ADMIN, "adm:tgt:777")
check("кнопки: поездки добавлены", db.get_player(777)["rides"] >= 100)

# --- своё число (единственный текст во всей админке — число) ---
m = M(777, "/start")  # не пересоздаём, просто проверяем ввод
cb(ADMIN, "adm:money")
cb(ADMIN, "adm:custom")
coins_before = db.get_player(777)["money"]
r = run(ADMIN, "777777")   # своё число — сумма
cb(ADMIN, "adm:tgt:777")
check("своё число + выбор цели кнопкой", db.get_player(777)["money"] == coins_before + 777_777)

# --- меню: админская кнопка только у админа ---
def kb_texts(kb):
    return [b.text for row in kb.keyboard for b in row]
check("кнопка «админка» в меню только у админа",
      any("админка" in t for t in kb_texts(B.kb_main(True)))
      and not any("админка" in t for t in kb_texts(B.kb_main(False))))

# --- сброс кнопками с подтверждением ---
cb(ADMIN, "adm:reset")
cb(ADMIN, "adm:tgt:777")
check("сброс ждёт «да»", db.get_player(777) is not None)
cb(ADMIN, "adm:cfmn")
check("отмена сброса кнопкой", db.get_player(777) is not None)

# --- «выдать всё» текстом ---
r = run(ADMIN, "выдать всё 777")
p = db.get_player(777)
check("выдано всё: самолёт", p["car_tier"] == 12)
check("выдано всё: замок", p["house_tier"] == 5)
check("выдано всё: элитный бордель", p["business_tier"] == 5 and p["business_level"] == G.BUSINESS_MAX_LEVEL)
check("выдано всё: премиум 365 дней", db.is_premium(p))
check("выдано всё: деньги ≥ 100M", p["money"] >= 100_000_000)
inv = db.get_inventory(777)
check("выдано всё: весь шмот", len({i["item_id"] for i in inv}) == len(G.CLOTHES))

# --- статистика ---
r = run(ADMIN, "статистика")
check("статистика отвечает", "игроков" in r and "денег в экономике" in r)

# --- админка открывается кнопками ---
r = run(ADMIN, "админка")
check("админка открывается", "админка бандита" in r)

print()
if fails:
    print("УПАЛО:", fails)
    sys.exit(1)
print("админка работает ✅")
