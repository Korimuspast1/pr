#!/usr/bin/env python3
"""Генерация пиксельных текстур интерфейса (камень, пергамент, дерево) и значка приложения.
Рисуется в «дизайнерских» пикселях и увеличивается без сглаживания, чтобы пиксели были крупными и чёткими."""
import random, os, sys
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'www', 'img')
os.makedirs(OUT, exist_ok=True)

def clamp(v): return max(0, min(255, int(round(v))))
def shade(c, f): return tuple(clamp(x * f) for x in c)
def jit(c, a, rnd): d = rnd.uniform(-a, a); return tuple(clamp(x + d) for x in c)
def save(img, name, scale=2):
    img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    p = os.path.join(OUT, name); img.save(p, optimize=True); return p

def bricks(base, mortar, seed, W=48, H=24, bw=24, bh=12, contrast=1.0):
    """Кладка: ряды со сдвигом, у каждого камня свой оттенок, свет сверху-слева."""
    rnd = random.Random(seed); im = Image.new('RGB', (W, H), mortar); px = im.load()
    for r in range(H // bh):
        y0 = r * bh; off = (r % 2) * (bw // 2)
        for b in range(W // bw):
            x0 = off + b * bw; col = jit(base, 9 * contrast, rnd)
            hi, lo = shade(col, 1 + 0.16 * contrast), shade(col, 1 - 0.2 * contrast)
            for y in range(y0 + 1, y0 + bh):
                for x in range(x0 + 1, x0 + bw):
                    c = col
                    if y == y0 + 1 or x == x0 + 1: c = hi
                    if y == y0 + bh - 1 or x == x0 + bw - 1: c = lo
                    px[x % W, y] = c
            for _ in range(10):  # зернистость
                x = rnd.randrange(x0 + 2, x0 + bw - 1); y = rnd.randrange(y0 + 2, y0 + bh - 1)
                px[x % W, y] = shade(col, rnd.choice([0.88, 0.92, 1.07, 1.1]))
            if rnd.random() < 0.45:  # трещинка или скол
                x = rnd.randrange(x0 + 4, x0 + bw - 5); y = rnd.randrange(y0 + 3, y0 + bh - 4)
                for k in range(rnd.randrange(2, 5)):
                    px[(x + k) % W, y + (k // 2) % 2] = shade(col, 0.68)
    return im

def slab(base, seed, W=32, H=32, amp=5):
    """Гладкая каменная плита: мелкое зерно и редкие царапины."""
    rnd = random.Random(seed); im = Image.new('RGB', (W, H), base); px = im.load()
    for y in range(H):
        for x in range(W):
            px[x, y] = jit(base, amp, rnd)
    for _ in range(14):
        x, y = rnd.randrange(W), rnd.randrange(H); c = shade(base, rnd.choice([0.82, 0.86, 1.12]))
        for k in range(rnd.randrange(2, 4)): px[(x + k) % W, y] = c
    for _ in range(3):
        x, y = rnd.randrange(W), rnd.randrange(H)
        for k in range(rnd.randrange(4, 8)): px[(x + k) % W, (y + k // 2) % H] = shade(base, 0.74)
    return im

def parchment(seed, W=32, H=32):
    rnd = random.Random(seed); base = (226, 208, 164); im = Image.new('RGB', (W, H), base); px = im.load()
    for y in range(H):
        for x in range(W): px[x, y] = jit(base, 4, rnd)
    for _ in range(22):  # волокна
        x, y = rnd.randrange(W), rnd.randrange(H); c = shade(base, rnd.choice([0.9, 0.93, 1.05]))
        for k in range(rnd.randrange(2, 6)): px[(x + k) % W, y] = c
    for _ in range(5):  # пятнышки
        x, y = rnd.randrange(W), rnd.randrange(H); px[x, y] = shade(base, 0.82); px[(x + 1) % W, y] = shade(base, 0.88)
    return im

def wood(seed, W=32, H=16, ph=8):
    rnd = random.Random(seed); base = (112, 78, 44); im = Image.new('RGB', (W, H), base); px = im.load()
    for r in range(H // ph):
        col = jit(base, 8, rnd)
        for y in range(r * ph, r * ph + ph):
            g = shade(col, rnd.choice([0.94, 1.0, 1.0, 1.05]))
            for x in range(W): px[x, y] = g
        for x in range(W):
            px[x, r * ph] = shade(col, 1.22); px[x, r * ph + ph - 1] = shade(col, 0.6)
        for _ in range(5):  # прожилки
            x, y = rnd.randrange(W), r * ph + rnd.randrange(2, ph - 2); c = shade(col, rnd.choice([0.8, 0.85, 1.12]))
            for k in range(rnd.randrange(3, 9)): px[(x + k) % W, y] = c
        x = rnd.randrange(W); y = r * ph + ph // 2   # сучок
        px[x, y] = shade(col, 0.6); px[(x + 1) % W, y] = shade(col, 0.7)
    return im

save(bricks((150, 144, 134), (58, 54, 50), 11), 'stone.png')                    # светлая кладка: панели-«стены»
save(bricks((110, 106, 99), (44, 41, 38), 23, contrast=0.9), 'stone_m.png')     # кладка потемнее
save(slab((60, 57, 53), 5), 'slab.png')                                         # тёмная плита: фон окон
save(slab((84, 80, 74), 6, amp=4), 'slab_l.png')                                # плита посветлее: строки списков
save(slab((126, 121, 112), 7, amp=6), 'slab_b.png')                             # камень кнопок
save(parchment(3), 'parch.png')
save(wood(9), 'wood.png')

# ---------- значок приложения ----------
def icon_art():
    """36x36: каменный замок с золотой короной на тёмно-красном поле."""
    S = 36; im = Image.new('RGBA', (S, S), (0, 0, 0, 0)); px = im.load(); rnd = random.Random(1)
    def R(x, y, w, h, c):
        for j in range(y, y + h):
            for i in range(x, x + w):
                if 0 <= i < S and 0 <= j < S: px[i, j] = c + (255,) if len(c) == 3 else c
    K = (26, 20, 16); ST = (176, 170, 158); SL = (206, 200, 188); SD = (128, 122, 112); G = (242, 194, 48); GD = (170, 124, 26)
    def tower(x, y, w, h):
        R(x - 1, y - 1, w + 2, h + 2, K); R(x, y, w, h, ST); R(x, y, 1, h, SL); R(x + w - 1, y, 1, h, SD)
        for j in range(y + 2, y + h, 3):
            R(x, j, w, 1, SD)
            for i in range(x + (2 if (j // 3) % 2 else 0), x + w, 4): R(i, j - 2, 1, 2, SD)
    # башни по бокам, стена и надвратная башня
    tower(5, 15, 8, 16); tower(23, 15, 8, 16); tower(11, 20, 14, 11); tower(13, 12, 10, 9)
    for tx in (5, 23):   # зубцы
        for i in range(3): R(tx - 1 + i * 3, 11, 4, 4, K); R(tx + i * 3, 12, 2, 3, ST)
        R(tx + 3, 19, 2, 4, K)
    for i in range(3): R(12 + i * 4, 8, 4, 4, K); R(13 + i * 4, 9, 2, 3, ST)
    # ворота
    R(15, 23, 6, 8, K); R(16, 24, 4, 7, (74, 48, 24)); R(18, 24, 1, 7, K); R(16, 27, 4, 1, (58, 58, 64))
    R(16, 15, 4, 4, K); R(17, 16, 2, 2, (232, 200, 96))
    # корона
    R(13, 2, 10, 5, K); R(14, 4, 8, 2, G); R(14, 2, 2, 3, G); R(17, 2, 2, 3, G); R(20, 2, 2, 3, G); R(14, 6, 8, 1, GD); R(17, 4, 2, 1, (232, 64, 44))
    # флажки
    R(8, 6, 1, 6, K); R(9, 6, 4, 3, (232, 64, 44)); R(27, 6, 1, 6, K); R(23, 6, 4, 3, (232, 64, 44))
    # земля
    R(2, 31, 32, 1, K); R(2, 32, 32, 2, (74, 110, 52)); R(2, 34, 32, 1, (52, 82, 40))
    return im

def icon_bg(size):
    """Фон значка: тёмно-красное полотнище с лёгким узором."""
    im = Image.new('RGB', (size, size), (122, 28, 22)); px = im.load(); rnd = random.Random(4)
    for y in range(size):
        for x in range(size):
            c = (122, 28, 22) if ((x // 6 + y // 6) % 2 == 0) else (112, 24, 20)
            px[x, y] = jit(c, 3, rnd)
    return im

def make_icons(res_dir):
    art = icon_art(); dens = {'mdpi': 1, 'hdpi': 1.5, 'xhdpi': 2, 'xxhdpi': 3, 'xxxhdpi': 4}
    for d, k in dens.items():
        os.makedirs(os.path.join(res_dir, 'mipmap-' + d), exist_ok=True)
        # обычный значок 48dp: рисунок на красном поле в тёмной рамке
        s = int(48 * k); bg = icon_bg(48).resize((s, s), Image.NEAREST).convert('RGBA')
        a = art.resize((int(36 * k), int(36 * k)), Image.NEAREST); bg.alpha_composite(a, ((s - a.width) // 2, (s - a.height) // 2))
        bw = max(1, int(round(2 * k)))
        for y in range(s):
            for x in range(s):
                if x < bw or y < bw or x >= s - bw or y >= s - bw: bg.putpixel((x, y), (26, 20, 16, 255))
        bg.save(os.path.join(res_dir, 'mipmap-' + d, 'ic_launcher.png'), optimize=True)
        # адаптивный значок 108dp: рисунок в безопасной зоне (66dp), фон отдельным слоем
        s2 = int(108 * k); fg = Image.new('RGBA', (s2, s2), (0, 0, 0, 0)); a2 = art.resize((int(54 * k), int(54 * k)), Image.NEAREST)
        fg.alpha_composite(a2, ((s2 - a2.width) // 2, (s2 - a2.height) // 2)); fg.save(os.path.join(res_dir, 'mipmap-' + d, 'ic_launcher_fg.png'), optimize=True)
        icon_bg(108).resize((s2, s2), Image.NEAREST).save(os.path.join(res_dir, 'mipmap-' + d, 'ic_launcher_bg.png'), optimize=True)
    # крупный вариант для предпросмотра и веб-значка
    big = icon_bg(48).resize((432, 432), Image.NEAREST).convert('RGBA'); a = art.resize((324, 324), Image.NEAREST); big.alpha_composite(a, (54, 54))
    big.convert('RGB').save(os.path.join(OUT, 'icon.png'), optimize=True)

make_icons(os.path.join(os.path.dirname(__file__), '..', 'android', 'res'))
print('ok:', sorted(os.listdir(OUT)))
