# -*- coding: utf-8 -*-
"""Игровые данные «бот бандит»: машины, бизнесы, дома, шмот, промокоды.

Числа подобраны под собственную экономику клона (оригинал закрыт,
поэтому баланс срисован по мотивам, а не скопирован 1 в 1).
"""

# ---------- Такси ----------
# (name, rides_needed, price, pay_per_ride, seconds)
CARS = [
    {"name": "жигули «копейка»",        "rides": 0,    "price": 0,          "pay": 40,    "time": 12.0},
    {"name": "ваз «девятка»",           "rides": 10,   "price": 400,        "pay": 60,    "time": 11.0},
    {"name": "toyota camry",            "rides": 25,   "price": 1200,       "pay": 85,    "time": 10.0},
    {"name": "bmw 5 серия",             "rides": 50,   "price": 3500,       "pay": 120,   "time": 9.0},
    {"name": "mercedes e-класса",       "rides": 90,   "price": 8000,       "pay": 170,   "time": 8.0},
    {"name": "audi a6",                 "rides": 140,  "price": 15000,      "pay": 240,   "time": 7.0},
    {"name": "lexus gs",                "rides": 200,  "price": 30000,      "pay": 330,   "time": 6.0},
    {"name": "bmw 7 серия",             "rides": 280,  "price": 60000,      "pay": 450,   "time": 5.5},
    {"name": "bentley continental",     "rides": 380,  "price": 120000,     "pay": 620,   "time": 5.0},
    {"name": "rolls-royce ghost",       "rides": 500,  "price": 250000,     "pay": 850,   "time": 4.5},
    {"name": "lamborghini aventador",   "rides": 650,  "price": 500000,     "pay": 1150,  "time": 4.0},
    {"name": "bugatti chiron",          "rides": 850,  "price": 1000000,    "pay": 1600,  "time": 3.5},
    {"name": "личный самолёт shamal ✈",  "rides": 1000, "price": 0,          "pay": 2500,  "time": 3.0, "premium": True},
]

# ---------- Дома ----------
# (name, price, taxi_bonus)
HOUSES = [
    {"name": "нет дома (снимаешь комнату)", "price": 0,         "bonus": 0.0},
    {"name": "хрущёвка",                    "price": 50000,     "bonus": 0.02},
    {"name": "сталинка",                    "price": 250000,    "bonus": 0.05},
    {"name": "коттедж",                     "price": 1000000,   "bonus": 0.10},
    {"name": "особняк",                     "price": 5000000,   "bonus": 0.20},
    {"name": "замок «бандит»",              "price": 25000000,  "bonus": 0.35},
]

# ---------- Бизнес ----------
# сырьё заказывается за деньги, со временем перерабатывается в деньги
BUSINESSES = [
    {"name": "ларёк «шаурма у дяди вани»",  "price": 5000,      "raw_cost": 15,   "pay": 40,    "speed": 3.0},
    {"name": "чебуречная",                  "price": 20000,     "raw_cost": 50,   "pay": 140,   "speed": 4.0},
    {"name": "ресторан «у василисы»",       "price": 80000,     "raw_cost": 180,  "pay": 520,   "speed": 6.0},
    {"name": "ночной клуб «алиби»",         "price": 350000,    "raw_cost": 700,  "pay": 2100,  "speed": 8.0},
    {"name": "баня «у марго»",              "price": 1200000,   "raw_cost": 2600, "pay": 8200,  "speed": 11.0},
    {"name": "элитный бордель",             "price": 5000000,   "raw_cost": 9000, "pay": 30000, "speed": 15.0},
]
BUSINESS_MAX_LEVEL = 15

def business_speed(tier: int, level: int, premium: bool) -> float:
    base = BUSINESSES[tier]["speed"] * (1.0 + 0.35 * (level - 1))
    return base * (1.25 if premium else 1.0)

def business_pay(tier: int, level: int, premium: bool) -> float:
    base = BUSINESSES[tier]["pay"] * (1.0 + 0.25 * (level - 1))
    return base * (1.25 if premium else 1.0)

def warehouse_cap(level: int) -> int:
    return 100 + 150 * level

def business_xp_need(level: int) -> int:
    return 80 * level

# ---------- Скам (рефералка) ----------
SCAM_RANKS = [
    {"count": 0,   "name": "новичок",       "reward": 0},
    {"count": 5,   "name": "скамаут",       "reward": 5000},
    {"count": 15,  "name": "жулик",         "reward": 25000},
    {"count": 30,  "name": "мошенник",      "reward": 100000},
    {"count": 60,  "name": "аферист",       "reward": 500000},
    {"count": 100, "name": "дон скама",     "reward": 2500000},
    {"count": 200, "name": "легенда скама", "reward": 10000000},
]

def scam_rank(count: int):
    cur = SCAM_RANKS[0]
    nxt = None
    for i, r in enumerate(SCAM_RANKS):
        if count >= r["count"]:
            cur = r
            nxt = SCAM_RANKS[i + 1] if i + 1 < len(SCAM_RANKS) else None
    return cur, nxt

# ---------- Шмот ----------
CLOTHES = [
    {"id": "cap",      "name": "кепка «бандит»",        "price": 750,     "cur": "$"},
    {"id": "jacket",   "name": "кожаная куртка",        "price": 10000,   "cur": "$"},
    {"id": "glasses",  "name": "золотые очки",          "price": 5000,    "cur": "$"},
    {"id": "chain",    "name": "золотая цепь",          "price": 25000,   "cur": "$"},
    {"id": "balaclava","name": "балаклава",             "price": 30000,   "cur": "$"},
    {"id": "suit",     "name": "костюм босса",          "price": 100000,  "cur": "$"},
    {"id": "watch",    "name": "швейцарские часы",      "price": 250000,  "cur": "$"},
    {"id": "ring",     "name": "черепа из золота",      "price": 500000,  "cur": "$"},
    {"id": "crown",    "name": "корона бандита",        "price": 15,      "cur": "bandi"},
    {"id": "pot_hat",  "name": "кастрюля",              "price": 0,       "cur": "none"},  # только по промо
]
CLOTHES_BY_ID = {c["id"]: c for c in CLOTHES}

# ---------- Промокоды ----------
PROMOS = {
    "СТАРТ":    {"money": 1000,   "bandi": 0,  "item": None},
    "ТАКСИ":    {"money": 10000,  "bandi": 0,  "item": None},
    "БИЗНЕС":   {"money": 25000,  "bandi": 0,  "item": None},
    "BANDIT":   {"money": 0,      "bandi": 10, "item": None},
    "КАСТРЮЛЯ": {"money": 250000, "bandi": 0,  "item": "pot_hat"},
}

# ---------- Казино ----------
SLOT_SYMBOLS = ["🍒", "🍋", "🔔", "⭐", "💎", "7️⃣"]
SLOT_WEIGHTS = [30, 25, 18, 13, 9, 5]
SLOT_TRIPLE = {"🍒": 2, "🍋": 3, "🔔": 5, "⭐": 8, "💎": 15, "7️⃣": 30}

ROULETTE_REDS = {1,3,5,7,9,12,14,16,18,19,21,23,25,27,30,32,34,36}

# ---------- Премиум ----------
PREMIUM_PRICE_MONEY = 100_000_000
PREMIUM_PRICE_BANDI = 150
PREMIUM_DAYS = 30
START_MONEY = 1000
GANGS_COST = 25000
TRADE_FEE = 0.0

def fmt_money(n) -> str:
    n = int(n)
    sign = "-" if n < 0 else ""
    return f"{sign}${abs(n):,}".replace(",", ".")

def fmt_num(n) -> str:
    return f"{int(n):,}".replace(",", ".")

def level_info(xp: int):
    """возвращает (level, xp_into_level, xp_needed_for_next)"""
    level = 1
    rem = int(xp)
    need = 100 * level
    while rem >= need:
        rem -= need
        level += 1
        need = 100 * level
    return level, rem, need
