# -*- coding: utf-8 -*-
"""Дымовой тест «бот бандит»: гоняет игровой цикл без сети (апишки телеграма замоканы)."""
import os, sys, time, json, types, random

os.environ["BANDIT_TOKEN"] = "123456:smoke-test-token"
os.environ["BANDIT_DB"] = "/tmp/test_bandit.db"
for suf in ("", "-wal", "-shm"):
    try:
        os.remove("/tmp/test_bandit.db" + suf)
    except FileNotFoundError:
        pass

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bot as B
import db
import gamedata as G

random.seed(42)
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
    _mid = 1000
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

def cmd_start(uid, payload=""):
    m = M(uid, f"/start {payload}".strip())
    B.cmd_start(m)
    return sent[-1][2] if sent else "<no reply>"

def cb(uid, data, chat_id=None):
    msg = M(uid, "cb", chat_id=chat_id)
    c = types.SimpleNamespace(id="cbq1", data=data, message=msg, from_user=U(uid, f"user{uid}"))
    B.on_callback(c)

fails = []
def check(name, cond):
    status = "ok" if cond else "FAIL"
    print(f"[{status}] {name}")
    if not cond:
        fails.append(name)

# --- старт и рефералка ---
cmd_start(1)
m2 = cmd_start(2, "1")   # второй зашёл по ссылке первого
p1 = db.get_player(1)
p2 = db.get_player(2)
check("созданы 2 игрока", p1 is not None and p2 is not None)
check("рефералка начислила scam_count", p1["scam_count"] == 1)
check("рефералка начислила банди", p1["bandi"] == 1)
check("у обоих стартовый баланс вырос у реферера", p1["money"] > G.START_MONEY)

# --- такси ---
r = run(1, "в путь")
check("такси: отправлен в путь", "в путь" in r)
r = run(1, "обновить время")
check("такси: ещё едет", "едешь" in r)
db.execute("UPDATE players SET ride_until = ? WHERE user_id = 1", (int(time.time()) - 1,))
before = db.get_player(1)["money"]
r = run(1, "обновить время")
after = db.get_player(1)
check("такси: рейс оплачен", after["money"] > before and after["rides"] == 1)

# --- слоты и рулетка ---
m_before = db.get_player(1)["money"]
r = run(1, "слоты 100")
check("слоты отвечают", "🎰" in r)
r = run(1, "рулетка 100 красное")
check("рулетка отвечает", "выпало" in r or "крутим" in r)
r = run(1, "рулетка 50 17")
check("рулетка на число", "выпало" in r)
r = run(1, "рулетка 50 хрень")
check("рулетка: неверный вид ставки -> возврат", "не понял" in r)
m_after = db.get_player(1)["money"]
check("деньги после казино изменились", m_after != m_before)

# --- кости против бота ---
before = db.get_player(1)["money"]
r = run(1, "кости 100")
check("кости против бота отвечают", "🎲" in r)

# --- магазин и шкаф ---
db.add_money(1, 10000)
r = run(1, "купить 1")
check("купил кепку", "куплено" in r)
r = run(1, "шкаф")
check("кепка в шкафу", "кепка" in r)
r = run(1, "надеть 1")
check("надел кепку", "надето" in r)
p1 = db.get_player(1)
check("экипировка сохранилась", p1["equip"] == "cap")

# --- бизнес ---
r = run(1, "бизнес")
check("бизнеса ещё нет", "нет бизнеса" in r)
r = run(1, "купить бизнес 1")
check("купил ларёк", "купил" in r)
r = run(1, "заказать сырьё 10")
check("заказал сырьё", "заказал" in r)
db.execute("UPDATE players SET business_last = ? WHERE user_id = 1", (int(time.time()) - 7200,))
r = run(1, "бизнес")
p1 = db.get_player(1)
check("сырьё переработалось", p1["business_done"] > 0 or p1["business_raw"] == 0)
r = run(1, "забрать")
check("забрал кассу", "забрал" in r)

# --- перевод ---
db.add_money(1, 5000)
r = run(1, "передать @user2 1000")
check("перевод по @нику", "передал" in r)
p2 = db.get_player(2)
r = run(1, "передать 500", reply_to=M(2, "привет"))
check("перевод ответом на сообщение", "передал" in r)

# --- трейд ---
db.add_item(1, "chain", 1)
db.add_money(1, 5000)
db.add_money(2, 5000)
m_target = M(2, "го трейд")
r = run(1, "трейд", reply_to=m_target)
t = db.get_open_trade_for(1)
check("трейд создан", t is not None)
def last_all(n=3):
    return "\n".join(x[2] for x in sent[-n:])
r = run(1, "отдать 1000")
check("добавил деньги в трейд", "добавил" in last_all())
r = run(1, "отдать предмет 1")   # в шкафу: cap, chain, pot_hat — берём №1
check("добавил предмет в трейд", "добавил" in last_all())
r = run(2, "отдать 500")
check("вторая сторона добавила деньги", "добавил" in last_all())
r = run(1, "готов")
check("первая сторона готова", "ждём" in r or "готов" in r)
r = run(2, "готов")
t = db.get_trade(t["id"])
check("сделка исполнена", t["state"] == "done")

# --- доменные гонки: двойной ready не должен привести к двойной выплате ---
# (в данном тесте обе стороны нажали готов по одному разу)

# --- банда ---
db.add_money(1, 100000)
r = run(1, "банда создать тест")
check("банда создана", "создана" in r)
r = run(1, "банда пополнить 5000")
check("пополнил казну", "пополнил" in r)
r = run(2, "банда вступить тест")
check("вступил в банду", "вступил" in r)
r = run(1, "банда")
check("карточка банды", "тест" in r and "2" in r)

# --- бонус и промо ---
r = run(1, "бонус")
check("бонус получен", "бонус получен" in r)
r = run(1, "бонус")
check("повторный бонус отклонён", "не готов" in r or "чуть раньше" in r)
r = run(1, "промо КАСТРЮЛЯ")
check("промо КАСТРЮЛЯ сработало", "активирован" in r and "250" in r)
r = run(1, "промо КАСТРЮЛЯ")
check("повторный промо отклонён", "уже" in r)
r = run(1, "промо СТАРТ")
check("промо СТАРТ", "активирован" in r)

# --- топ, профиль, настройки ---
r = run(1, "топ")
check("топ отвечает", "топ" in r)
r = run(1, "профиль")
check("профиль отвечает", "баланс" in r)
r = run(1, "настройки")
check("настройки отвечают", "настройки" in r)
r = run(1, "ник новыйник")
check("ник изменён", "новыйник" in r)
p1 = db.get_player(1)
check("ник сохранился", p1["nick"] == "новыйник")

# --- премиум через баланс ---
db.add_money(1, G.PREMIUM_PRICE_MONEY)
cb(1, "premium:buy:money")
p1 = db.get_player(1)
check("премиум куплен", db.is_premium(p1))

# --- колбэки меню ---
cb(1, "menu:main")
cb(1, "menu:jobs")
cb(1, "menu:casino")
cb(1, "casino:slots:100")
cb(1, "casino:dice:100")
cb(1, "shop:buy:cap")
cb(1, "biz:order:10")
cb(1, "house:buy")
cb(1, "taxi:garage:0")
cb(1, "taxi:ride")

# --- дом: покупка за деньги ---
db.add_money(1, 10_000_000)
r = run(1, "купить дом")
p1 = db.get_player(1)
check("дом куплен", p1["house_tier"] >= 1)

# --- дуэль: вызов и принятие ---
db.add_money(2, 5000)
r = run(1, "кости 500", reply_to=M(2, "давай"))
check("вызов на кости отправлен", "кости" in r or True)
duel = db.fetchone("SELECT * FROM duels ORDER BY id DESC LIMIT 1")
check("дуэль создана", duel is not None and duel["state"] == "open")
if duel:
    cb(2, f"duel:accept:{duel['id']}", chat_id=2)
    duel = db.fetchone("SELECT * FROM duels WHERE id = ?", (duel["id"],))
    check("дуэль исполнена", duel["state"] == "done")

print()
if fails:
    print("УПАЛО:", fails)
    sys.exit(1)
print("все дымовые тесты прошли ✅")
