'use strict';
/* ===== Константы и описания ===== */
const TS = 16; // размер тайла в пикселях мира
const TR = { DEEP: 0, SHALLOW: 1, SAND: 2, GRASS: 3, PLAIN: 4, FOREST: 5, HILL: 6, MOUNT: 7 };
const isWater = t => t <= 1;
const RES = { NONE: 0, IRON: 1, GOLD: 2 };

const PCOL = ['#3b6fe0', '#d93a3a', '#44445a', '#e8c52e', '#9a4fd1', '#e8832e', '#2fc4c4', '#d65fa6'];
const PNAMES = ['Альтарос', 'Вельгард', 'Норвейн', 'Кронмарк', 'Ольстрия', 'Дракенхольм', 'Эльмар', 'Сарвония'];
const CITY_NAMES = ['Рокфорд', 'Белая Гавань', 'Тёмный Бор', 'Каменный Мост', 'Серебряный Ключ', 'Ветролом', 'Железная Скала',
  'Златоград', 'Ясный Холм', 'Звонкая Падь', 'Вороний Яр', 'Лунный Брод', 'Старый Дозор', 'Медвежий Угол', 'Орлиное Гнездо',
  'Тихая Заводь', 'Рыбий Мыс', 'Грозовой Перевал', 'Красный Лог', 'Вешняя Роща', 'Дубрава', 'Соколиный Ряд', 'Вольный Град', 'Холодный Предел'];

const STOCK_KEYS = ['gold', 'wood', 'food', 'iron', 'bow', 'sword', 'armL', 'armH', 'horse'];
const RNAME = { gold: 'Золото', wood: 'Дерево', food: 'Еда', iron: 'Железо', bow: 'Луки', sword: 'Мечи', armL: 'Лёгкие доспехи', armH: 'Тяжёлые доспехи', horse: 'Кони' };
const RVAL = { gold: 1, wood: 0.6, food: 0.6, iron: 0.9, bow: 3, sword: 3, armL: 3, armH: 4, horse: 4 };

const BD = {
  farm: { n: 'Ферма', cost: { wood: 30, gold: 20 }, site: 'field', pts: 20, d: 'Даёт еду. Только на лугах и равнинах.' },
  sawmill: { n: 'Лесопилка', cost: { wood: 10, gold: 30 }, site: 'forest', pts: 20, d: 'Даёт дерево. Только в лесу, чем гуще лес — тем больше.' },
  mine: { n: 'Рудник', cost: { wood: 40, gold: 40 }, site: 'ore', pts: 30, d: 'Железо или золото. Только на залежах в горах.' },
  barracks: { n: 'Казармы', cost: { wood: 80, gold: 80, iron: 20 }, site: 'free', pts: 40, d: 'Обучают отряды по 10 воинов.' },
  forge: { n: 'Кузня', cost: { wood: 60, gold: 80, iron: 40 }, site: 'free', pts: 40, d: 'Куёт мечи и доспехи.' },
  workshop: { n: 'Мастерская', cost: { wood: 70, gold: 60 }, site: 'free', pts: 35, d: 'Делает луки.' },
  stable: { n: 'Конюшня', cost: { wood: 80, gold: 70, food: 40 }, site: 'free', pts: 35, d: 'Выращивает боевых коней.' },
  market: { n: 'Рынок', cost: { wood: 60, gold: 100 }, site: 'free', pts: 35, d: 'Приносит золото, усиливает торговлю.' },
  siege: { n: 'Осадная мастерская', cost: { wood: 120, gold: 100, iron: 50 }, site: 'free', pts: 45, d: 'Строит катапульты и тараны — по одной машине.' },
  wall: { n: 'Стена', cost: { wood: 6, iron: 3, gold: 6 }, site: 'free', pts: 2, hp: 400, d: 'Крепостная стена. Преграждает путь врагу, пока её не сломают.' },
  tower: { n: 'Башня', cost: { wood: 40, iron: 25, gold: 50 }, site: 'free', pts: 15, hp: 600, d: 'В башне можно разместить отряд лучников — они получают защиту.' },
  gate: { n: 'Ворота', cost: { wood: 25, iron: 10, gold: 20 }, site: 'gate', pts: 6, hp: 250, d: 'Свои проходят, враги — нет. Ставятся в стену.' },
  moat: { n: 'Ров', cost: { gold: 10, wood: 2 }, site: 'moat', pts: 1, d: 'Ров с водой: должен соединяться с водоёмом. Атакующие в нём слабеют на 3–7%.' },
  academy: { n: 'Королевская академия', cost: { wood: 150, gold: 200, iron: 40 }, site: 'free', pts: 60, one: true, d: 'Обучает одного полководца. Академия может быть только одна.' },
  hospital: { n: 'Госпиталь', cost: { wood: 90, gold: 100, food: 30 }, site: 'free', pts: 40, d: 'Лечит армии рядом и возвращает раненых бойцов.' },
  port: { n: 'Порт', cost: { wood: 120, gold: 120, iron: 20 }, site: 'coast', pts: 50, d: 'Строит торговые и военные суда. Только на берегу.' }
};
const BORDER = ['academy', 'farm', 'sawmill', 'mine', 'barracks', 'hospital', 'forge', 'workshop', 'stable', 'siege', 'wall', 'tower', 'gate', 'moat', 'market', 'port'];
const STRUCT = { wall: 1, tower: 1, gate: 1 };
const RAM_DPS = { wall: 28, gate: 40, tower: 24 }, CAT_DMG = { wall: 30, gate: 34, tower: 28 };

// рецепты производства предметов
const RECIPES = {
  forge: { sword: { out: 'sword', in: { iron: 2, wood: 1 }, t: 6 }, armL: { out: 'armL', in: { iron: 3 }, t: 6 }, armH: { out: 'armH', in: { iron: 6, gold: 2 }, t: 9 } },
  workshop: { bow: { out: 'bow', in: { wood: 2 }, t: 4 } },
  stable: { horse: { out: 'horse', in: { food: 5, gold: 1 }, t: 8 } }
};
const SQ = {
  cmd: { n: 'Полководец', need: { iron: 30 }, gold: 250, food: 40, t: 60, hp: 160, atk: 3.2, spd: 1.3, range: 1.6, ico: 'cmd', cnt: 1, bld: 'academy' },
  cat: { n: 'Катапульта', need: { wood: 120, iron: 40 }, gold: 60, food: 10, t: 40, hp: 120, atk: 1.5, spd: 0.8, range: 5.0, ico: 'cat', cnt: 1, siege: true, bld: 'siege' },
  ram: { n: 'Таран', need: { wood: 150, iron: 20 }, gold: 50, food: 10, t: 35, hp: 220, atk: 2.5, spd: 1.0, range: 1.6, ico: 'ram', cnt: 1, siege: true, bld: 'siege' },
  archer: { n: 'Лучники', need: { bow: 10 }, gold: 40, food: 30, t: 20, hp: 20, atk: 3.2, spd: 1.2, range: 3.0, ico: 'bow', bld: 'barracks' },
  light: { n: 'Лёгкие рыцари', need: { sword: 10, armL: 10 }, gold: 60, food: 30, t: 25, hp: 35, atk: 3.5, spd: 1.4, range: 1.6, ico: 'armL', bld: 'barracks' },
  heavy: { n: 'Тяжёлые рыцари', need: { sword: 10, armH: 10 }, gold: 100, food: 40, t: 30, hp: 60, atk: 4.0, spd: 0.9, range: 1.6, ico: 'armH', bld: 'barracks' },
  cav: { n: 'Конница', need: { horse: 10 }, gold: 90, food: 40, t: 30, hp: 45, atk: 4.5, spd: 2.2, range: 1.6, ico: 'horse', bld: 'barracks' }
};
// множители урона: атакующий -> защитник
const MATCH = {
  archer: { archer: 1.0, light: 1.3, heavy: 0.6, cav: 0.9 },
  light: { archer: 1.2, light: 1.0, heavy: 0.7, cav: 0.9 },
  heavy: { archer: 1.2, light: 1.3, heavy: 1.0, cav: 1.25 },
  cav: { archer: 1.7, light: 1.2, heavy: 0.6, cav: 1.0 }
};
const mm = (a, b) => { const r = MATCH[a]; return r && r[b] !== undefined ? r[b] : 1; };
MATCH.archer.cat = 1.2; MATCH.archer.ram = 0.5; MATCH.light.cat = 1.3; MATCH.light.ram = 1.0; MATCH.heavy.cat = 1.2; MATCH.heavy.ram = 1.1; MATCH.cav.cat = 1.6; MATCH.cav.ram = 0.8;
MATCH.cat = { archer: 1, light: 1, heavy: 1, cav: 1 }; MATCH.ram = { archer: 0.5, light: 0.5, heavy: 0.5, cav: 0.5 };
const TMULT = [0, 0, 0.9, 1, 1, 0.7, 0.6, 0.35]; // скорость по местности
const CITY_MIN = 9;
const MAX_SQ_ARMY = 8;
const MOVE_MULT = 0.45; // замедление походов армий
const AURA_R = 4.5, AURA_ATK = 1.25, AURA_DEF = 0.8;
const CMD = { archer: { n: 'Воевода', of: 'лучников', ico: 'bow' }, light: { n: 'Капитан', of: 'лёгких рыцарей', ico: 'armL' }, heavy: { n: 'Лорд-командор', of: 'тяжёлых рыцарей', ico: 'armH' }, cav: { n: 'Маршал', of: 'конницы', ico: 'horse' } };
const DAY = 120; // секунд игрового времени в одних сутках (2 минуты)
const CLAIM_DAYS = 3; // дней на формирование претензии
const DIFF = { easy: { n: 'Лёгкая', mult: 0.8, think: 5.5 }, normal: { n: 'Средняя', mult: 1.0, think: 3.2 }, hard: { n: 'Сложная', mult: 1.25, think: 1.8 } };

/* ===== Генератор случайных чисел и шум ===== */
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
function makeNoise(seed) {
  const rnd = mulberry32(seed), P = new Float32Array(65536);
  for (let i = 0; i < 65536; i++) P[i] = rnd();
  const h = (a, b) => P[(a & 255) + ((b & 255) << 8)];
  const n2 = (x, y) => {
    const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi, u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
    const a = h(xi, yi), b = h(xi + 1, yi), c = h(xi, yi + 1), d = h(xi + 1, yi + 1);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  };
  return (x, y, oct) => { oct = oct || 4; let s = 0, a = 1, f = 1, t = 0; for (let o = 0; o < oct; o++) { s += a * n2(x * f, y * f); t += a; a *= 0.5; f *= 2; } return s / t; };
}

/* ===== Пиксельные иконки ===== */
const PAL = { K: '#1a1410', W: '#f4f0e0', G: '#8a8f98', g: '#555b66', Y: '#f2c230', y: '#b8861c', R: '#c93a2e', r: '#7d2018', B: '#3b6fe0', b: '#25408a', N: '#8a5a2b', n: '#5a3a1a', L: '#4aa83a', l: '#2b6a24', P: '#e8c49a', O: '#e8832e', C: '#6ad0e8', S: '#b8c4d4' };
const ICONMAP = {
  gold: ['..KKKK..', '.KYYYYK.', 'KYYyyYYK', 'KYyYYyYK', 'KYyYYyYK', 'KYYyyYYK', '.KYYYYK.', '..KKKK..'],
  wood: ['........', '.KKKKKK.', 'KPPNNNNK', 'KPnNNnNK', '.KKKKKK.', 'KPPNNNNK', 'KPnNNnNK', '.KKKKKK.'],
  food: ['..Y..Y..', '.YY.YY.Y', '.Y.YY.Y.', '..YYYY..', '..NNNN..', '...NN...', '...NN...', '...NN...'],
  iron: ['........', '........', '..KKKKKK', '.KSSSSGK', 'KSSSGGgK', 'KGGGggKK', '.KKKKK..', '........'],
  bow: ['..NNN...', '.N..W...', 'N...W...', 'N...W...', 'N...W...', 'N...W...', '.N..W...', '..NNN...'],
  sword: ['......SS', '.....SSK', '....SSK.', '...SSK..', 'N.SSK...', '.NSK....', '.NNK....', 'NN.K....'],
  armL: ['.KK..KK.', 'KLLKKLLK', 'KLLLLLLK', '.KLLLLK.', '..KLLK..', '..KLLK..', '..KLLK..', '..KKKK..'],
  armH: ['..KKKK..', '.KSSSSK.', 'KSSSSSSK', 'KSKKKKSK', 'KSSGGSSK', 'KSGGGGSK', '.KGGGGK.', '..KKKK..'],
  horse: ['..K.K...', '.KNNNK..', 'KNNnNNK.', 'KNNNNNNK', '.KNNNNPK', '..KNNNNK', '..KNNNK.', '...KKK..'],
  medal: ['.R....B.', '..R..B..', '...RB...', '..KYYK..', '.KYyyYK.', '.KYyyYK.', '..KYYK..', '...KK...'],
  mail: ['........', 'KKKKKKKK', 'KWKWWKWK', 'KWWKKWWK', 'KWWWWWWK', 'KWWWWWWK', 'KKKKKKKK', '........'],
  t_map: ['........', '...W....', '..WGW...', '.WGGGW..', '.GGGGGGL', 'GGGgGGLL', 'KKKKKKKK', '........'],
  t_city: ['K.K..K.K', 'KKK..KKK', 'KSKKKKSK', 'KSSSSSSK', 'KSKSSKSK', 'KSKSSKSK', 'KSSSSSSK', 'KKKKKKKK'],
  t_build: ['.....SSS', '....SSSK', '...NSSK.', '..NN.K..', '.NN.....', 'NN......', 'N.......', '........'],
  t_army: ['.S....S.', '.SS..SS.', '..SSSS..', '...SS...', '..SSSS..', '.NN..NN.', 'NN....NN', 'N......N'],
  t_pol: ['Y..Y..Y.', 'YY.YY.YY', 'YYYYYYYY', 'KYYYYYYK', 'KRYBYRYK', 'KYYYYYYK', 'KKKKKKKK', '........'],
  t_fleet: ['....W...', '...WWW..', '..WWWWW.', '....N...', 'NNNNNNNN', '.NNNNNN.', '..KKKK..', 'CCCCCCCC'],
  ship: ['....W...', '...WWW..', '..WWWWW.', '....N...', 'NNNNNNNN', '.NNNNNN.', '..KKKK..', '........'],
  cat: ['....KK..', '...KNNK.', '..KNK.K.', '.KNK..W.', 'KNNNNNNK', 'KnNNNNnK', '.KKgKKgK', '..K.KK.K'],
  ram: ['.RRRRRR.', 'RRRRRRRR', '.K.K.K.K', 'NNNNNNNG', 'nNNNNNGG', '.KgKK.KK', '..K.K...', '........'],
  cmd: ['.Y.Y.Y..', '.YYYYYY.', '.KYYYYK.', 'KYYRYYYK', '.KYYYYK.', '..KKKK..', '.RRKKRR.', '.R.KK.R.'],
  menu: ['........', 'WWWWWWWW', '........', 'WWWWWWWW', '........', 'WWWWWWWW', '........', '........'],
  pause: ['........', '.CC..CC.', '.CC..CC.', '.CC..CC.', '.CC..CC.', '.CC..CC.', '.CC..CC.', '........'],
  hour: ['KKKKKKKK', '.KYYYYK.', '..KYYK..', '...KK...', '..KYYK..', '.KYYYYK.', 'KKKKKKKK', '........'],
  trophy: ['KYYYYYYK', 'KYyYYyYK', 'KYyYYyYK', '.KYYYYK.', '..KYYK..', '...KK...', '..KYYK..', '.KKKKKK.'],
  scroll: ['.KKKKKK.', 'KWWWWWWK', 'KWKKKKWK', 'KWWWWWWK', 'KWKKKKWK', 'KWWWWWWK', '.KKKKKK.', '........'],
  globe: ['..KKKK..', '.KBBLBK.', 'KBLLBBBK', 'KBLLLBBK', 'KBBLLLBK', 'KBBBLBBK', '.KBBBBK.', '..KKKK..'],
  cart: ['............', '..YYy.WWY...', '.YYYyWWWWy..', 'nNNNNNNNNNn.', 'nNNNNNNNNNnn', '.nnnnnnnnn.n', '..KKK..KKK..', '.KNNNK.KNNNK', '.KNKNK.KNKNK', '..KKK..KKK..'],
  chart: ['........', 'K.......', 'K....R..', 'K..G.R..', 'K.GG.R.L', 'KGGGRRLL', 'KKKKKKKK', '........'],
  eye: ['........', '..KKKK..', '.KWWWWK.', 'KWWOOWWK', '.KWWWWK.', '..KKKK..', '........', '........'],
  stop: ['........', '.RRRRRR.', '.RRRRRR.', '.RRRRRR.', '.RRRRRR.', '.RRRRRR.', '.RRRRRR.', '........'],
  home: ['...KK...', '..KRRK..', '.KRRRRK.', 'KRRRRRRK', '.KWWWWK.', '.KWKKWK.', '.KWKKWK.', '.KKKKKK.'],
  merge: ['........', 'W..WW..W', 'WW.WW.WW', 'WWWWWWWW', 'WW.WW.WW', 'W..WW..W', '........', '........'],
  split: ['........', 'WW.WW.WW', 'W.WWWW.W', 'W..WW..W', 'W.WWWW.W', 'WW.WW.WW', '........', '........'],
  skull: ['..KKKK..', '.KWWWWK.', 'KWKWWKWK', 'KWKWWKWK', '.KWWWWK.', '..KWKWK.', '..KKKKK.', '........'],
  flag: ['.NRRRR..', '.NRRRR..', '.NRRR...', '.NR.....', '.N......', '.N......', '.N......', 'KKKK....']
};
const ICON = {}, ICONURL = {};
function initIcons() {
  const hexRgb = h => { const n = parseInt(h.slice(1), 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; };
  for (const k in ICONMAP) {
    const rows = ICONMAP[k], w = rows[0].length, h = rows.length, px = [];
    for (let j = 0; j < h; j++) { px[j] = []; for (let i = 0; i < w; i++) { const ch = rows[j][i]; px[j][i] = (ch !== '.' && PAL[ch]) ? PAL[ch] : null; } }
    const g = (i, j) => (i < 0 || j < 0 || i >= w || j >= h) ? null : px[j][i];
    const W2 = w * 2 + 2, H2 = h * 2 + 2, out = []; for (let j = 0; j < H2; j++) out.push(new Array(W2).fill(null));
    // Scale2x (EPX): сглаживание диагоналей
    for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) {
      const P = g(i, j); if (!P) continue; const A = g(i, j - 1), B = g(i + 1, j), C = g(i - 1, j), D = g(i, j + 1);
      const e = [A === C && A !== D && C !== B ? A : P, A === B && A !== C && B !== D ? B : P, D === C && D !== B && C !== A ? C : P, D === B && D !== A && B !== C ? D : P];
      out[2 * j + 1][2 * i + 1] = e[0] || P; out[2 * j + 1][2 * i + 2] = e[1] || P; out[2 * j + 2][2 * i + 1] = e[2] || P; out[2 * j + 2][2 * i + 2] = e[3] || P;
    }
    const c = document.createElement('canvas'); c.width = W2; c.height = H2; const x = c.getContext('2d'); const o = (i, j) => (i < 0 || j < 0 || i >= W2 || j >= H2) ? null : out[j][i];
    for (let j = 0; j < H2; j++) for (let i = 0; i < W2; i++) {
      const v = out[j][i];
      if (!v) { if (o(i - 1, j) || o(i + 1, j) || o(i, j - 1) || o(i, j + 1)) { x.fillStyle = '#1a1410'; x.fillRect(i, j, 1, 1); } continue; }
      let f = 1; if (!o(i, j - 1) || !o(i - 1, j)) f = 1.18; if (!o(i, j + 1) || !o(i + 1, j)) f = 0.82;
      const r = hexRgb(v); x.fillStyle = `rgb(${Math.min(255, r[0] * f) | 0},${Math.min(255, r[1] * f) | 0},${Math.min(255, r[2] * f) | 0})`; x.fillRect(i, j, 1, 1);
    }
    ICON[k] = c; ICONURL[k] = c.toDataURL();
  }
}
const IMG = (k, s) => `<img class="ic" src="${ICONURL[k] || ''}" style="width:${(s || 24)}px;height:${(s || 24)}px" alt="">`;

/* ===== Спрайты зданий (рисуются прямоугольниками) ===== */
const R_ = (c, x, y, w, h, col) => { c.fillStyle = col; c.fillRect(x, y, w, h); };
/* --- здания: мелкие пиксели, сетка 32x32 на тайл 16x16 --- */
const sh_ = (h, f) => shadeC(h, f);
function bricks(c, x, y, w, h, base) { R_(c, x, y, w, h, base); const m = sh_(base, 0.72); for (let j = 0; j < h; j += 3) { R_(c, x, y + j, w, 1, m); for (let i = ((j / 3) & 1) ? 0 : 2; i < w; i += 4) R_(c, x + i, y + j, 1, 3, m); } R_(c, x, y, w, 1, sh_(base, 1.2)); R_(c, x, y + h - 1, w, 1, sh_(base, 0.6)); }
function planks(c, x, y, w, h, base) { R_(c, x, y, w, h, base); for (let i = 2; i < w; i += 3) R_(c, x + i, y, 1, h, sh_(base, 0.74)); R_(c, x, y, w, 1, sh_(base, 1.2)); R_(c, x, y + h - 1, w, 1, sh_(base, 0.6)); }
function tiles(c, x, y, w, h, col) { R_(c, x, y, w, h, col); for (let j = 0; j < h; j += 2) { R_(c, x, y + j + 1, w, 1, sh_(col, 0.72)); for (let i = (j & 2) ? 0 : 1; i < w; i += 3) R_(c, x + i, y + j, 1, 2, sh_(col, 0.84)); } R_(c, x, y, w, 1, sh_(col, 1.3)); }
function gable(c, x, y, w, h, col) { for (let j = 0; j < h; j++) { const ww = Math.max(2, Math.round(w * (j + 1) / h)), xx = x + Math.floor((w - ww) / 2); R_(c, xx, y + j, ww, 1, j % 2 ? sh_(col, 0.82) : col); } R_(c, x + Math.floor(w / 2) - 1, y, 2, 1, sh_(col, 1.3)); for (let j = 0; j < h; j++) { const ww = Math.max(2, Math.round(w * (j + 1) / h)), xx = x + Math.floor((w - ww) / 2); R_(c, xx, y + j, 1, 1, sh_(col, 1.3)); R_(c, xx + ww - 1, y + j, 1, 1, sh_(col, 0.6)); } }
function rrect(c, x, y, w, h, col, r) { for (let j = 0; j < h; j++) { const e = Math.min(j, h - 1 - j), ins = e < r ? r - e : 0; R_(c, x + ins, y + j, w - 2 * ins, 1, col); } }
function win(c, x, y, w, h) { R_(c, x, y, w, h, '#1c2434'); R_(c, x + 1, y + 1, w - 2, h - 2, '#e8c860'); R_(c, x + Math.floor(w / 2), y, 1, h, '#5a3a1a'); }
function flag(c, x, y, col) { R_(c, x, y, 1, 9, '#4a3018'); R_(c, x, y - 1, 1, 1, '#f2c230'); } // полотнище рисуется анимированным
const DRAW = {
  farm(c, x, y, col) {
    for (let j = 0; j < 5; j++) { const yy = y + 2 + j * 4; R_(c, x + 1, yy, 18, 3, '#6b4a22'); R_(c, x + 1, yy + 2, 18, 1, '#4a3016'); for (let i = 0; i < 18; i += 2) { R_(c, x + 1 + i, yy, 1, 2, '#5cc040'); R_(c, x + 1 + i, yy, 1, 1, '#b8f070'); } }
    planks(c, x + 17, y + 14, 14, 10, '#b04a30'); R_(c, x + 17, y + 14, 14, 1, '#d8c8a0'); gable(c, x + 15, y + 6, 18, 9, col); R_(c, x + 21, y + 18, 6, 6, '#3a2010'); R_(c, x + 21, y + 18, 6, 1, '#d8c8a0'); R_(c, x + 24, y + 18, 1, 6, '#5a3a1a'); R_(c, x + 22, y + 19, 2, 1, '#8a5a30');
    R_(c, x + 25, y + 9, 2, 2, '#f4eac0'); R_(c, x + 6, y + 24, 6, 4, '#e8c850'); R_(c, x + 6, y + 24, 6, 1, '#f8e890'); R_(c, x + 7, y + 26, 4, 1, '#b89830'); R_(c, x + 13, y + 25, 5, 3, '#e8c850'); R_(c, x + 13, y + 25, 5, 1, '#f8e890');
    for (let i = 0; i < 8; i++) { R_(c, x + 1 + i * 4, y + 29, 1, 3, '#8a5a2b'); } R_(c, x + 1, y + 30, 30, 1, '#a8743a');
  },
  sawmill(c, x, y, col) {
    R_(c, x + 1, y + 28, 30, 3, '#b89a60');
    planks(c, x + 3, y + 12, 18, 14, '#9a6a3a'); gable(c, x + 1, y + 5, 22, 8, col); R_(c, x + 1, y + 13, 22, 1, '#3a2410'); R_(c, x + 7, y + 17, 7, 9, '#2a1808'); R_(c, x + 7, y + 17, 7, 1, '#5a3a1a'); R_(c, x + 9, y + 20, 3, 6, '#5a3a1a');
    R_(c, x + 15, y + 15, 4, 4, '#14100c'); R_(c, x + 16, y + 16, 2, 2, '#e8c860'); R_(c, x + 18, y + 3, 3, 5, '#6a6a70'); R_(c, x + 19, y + 1, 3, 2, '#c8c8d0'); R_(c, x + 21, y, 2, 1, '#d8d8e0');
    // пила
    R_(c, x + 21, y + 20, 8, 8, '#a8aeb8'); R_(c, x + 22, y + 19, 6, 10, '#a8aeb8'); R_(c, x + 20, y + 22, 10, 4, '#a8aeb8'); R_(c, x + 23, y + 23, 4, 2, '#4a4e58'); R_(c, x + 24, y + 22, 2, 4, '#4a4e58'); for (let i = 0; i < 4; i++) R_(c, x + 21 + i * 2, y + 19, 1, 1, '#dde'); 
    // брёвна
    for (let j = 0; j < 3; j++) for (let i = 0; i < 3 - (j === 2 ? 1 : 0); i++) { const bx = x + 2 + i * 6 + (j === 2 ? 3 : 0), by = y + 27 - j * 3; R_(c, bx, by, 6, 3, '#8a5a2b'); R_(c, bx, by, 6, 1, '#a8743a'); R_(c, bx + 5, by, 1, 3, '#d8b070'); R_(c, bx + 5, by + 1, 1, 1, '#8a5a2b'); }
    R_(c, x + 12, y + 29, 14, 2, '#e8d098');
  },
  mine(c, x, y, col, res) {
    const ore = res === 2 ? '#f2c230' : '#cfe0ff', ore2 = res === 2 ? '#fff0a0' : '#ffffff';
    rrect(c, x, y + 4, 32, 28, '#6e737c', 7); for (let j = 3; j < 26; j += 2) for (let i = 4; i < 28; i += 2) if (((i * 7 + j * 13) % 5) < 2) R_(c, x + i, y + 4 + j, 2, 1, '#80858e'); 
    R_(c, x + 4, y + 2, 24, 4, '#80858e'); R_(c, x + 9, y, 14, 3, '#9a9fa8'); R_(c, x + 12, y, 6, 1, '#dde4f0');
    R_(c, x + 7, y + 11, 18, 17, '#4a3018'); R_(c, x + 9, y + 13, 14, 15, '#0e0a08'); R_(c, x + 7, y + 11, 18, 2, '#8a5a2b'); R_(c, x + 7, y + 11, 2, 17, '#8a5a2b'); R_(c, x + 23, y + 11, 2, 17, '#8a5a2b'); R_(c, x + 6, y + 10, 20, 1, '#a8743a');
    R_(c, x + 15, y + 17, 2, 2, '#f8c850'); R_(c, x + 15, y + 16, 2, 1, '#8a8a90'); // фонарь
    R_(c, x + 10, y + 27, 12, 1, '#555b66'); R_(c, x + 10, y + 29, 12, 1, '#555b66'); for (let i = 0; i < 12; i += 3) R_(c, x + 10 + i, y + 28, 1, 3, '#5a3a1a');
    R_(c, x + 13, y + 22, 8, 5, '#6a4a2a'); R_(c, x + 13, y + 22, 8, 1, '#8a6a3a'); R_(c, x + 14, y + 20, 6, 2, ore); R_(c, x + 15, y + 19, 3, 1, ore2); R_(c, x + 14, y + 27, 2, 2, '#14100c'); R_(c, x + 19, y + 27, 2, 2, '#14100c');
    R_(c, x + 2, y + 22, 7, 6, ore); R_(c, x + 3, y + 20, 4, 2, ore); R_(c, x + 3, y + 22, 2, 1, ore2); R_(c, x + 24, y + 20, 6, 5, ore); R_(c, x + 26, y + 18, 3, 2, ore); R_(c, x + 26, y + 20, 1, 1, ore2); R_(c, x + 4, y + 8, 2, 2, ore); R_(c, x + 26, y + 9, 2, 2, ore2);
    flag(c, x + 27, y + 1, col);
  },
  barracks(c, x, y, col) {
    rrect(c, x + 1, y + 27, 30, 5, '#8a8f78', 2);
    bricks(c, x + 2, y + 12, 28, 16, '#b8b0a0'); for (let i = 0; i < 6; i++) R_(c, x + 2 + i * 5, y + 9, 3, 3, '#b8b0a0'), R_(c, x + 2 + i * 5, y + 9, 3, 1, '#d8d0c0');
    for (const tx of [0, 26]) { bricks(c, x + tx, y + 6, 6, 22, '#a8a090'); tiles(c, x + tx - 1, y + 1, 8, 6, col); R_(c, x + tx + 2, y + 3, 2, 3, '#14100c'); }
    R_(c, x + 11, y + 15, 10, 13, '#2a1808'); R_(c, x + 11, y + 15, 10, 1, '#8a8f98'); for (let i = 0; i < 10; i += 2) R_(c, x + 11 + i, y + 16, 1, 12, '#555b66'); for (let j = 0; j < 12; j += 3) R_(c, x + 11, y + 17 + j, 10, 1, '#555b66'); R_(c, x + 10, y + 14, 12, 2, '#d8d0c0');
    R_(c, x + 7, y + 17, 2, 3, '#14100c'); R_(c, x + 23, y + 17, 2, 3, '#14100c'); R_(c, x + 15, y + 6, 2, 8, '#4a3018'); flag(c, x + 15, y + 2, col);
    R_(c, x + 3, y + 30, 1, 2, '#4a3018'); R_(c, x + 5, y + 30, 1, 2, '#4a3018'); R_(c, x + 28, y + 30, 1, 2, '#4a3018');
  },
  forge(c, x, y, col) {
    rrect(c, x + 1, y + 26, 30, 6, '#8a7a60', 2);
    bricks(c, x + 3, y + 10, 24, 18, '#7a5a50'); gable(c, x + 1, y + 4, 28, 7, '#3a3234'); R_(c, x + 1, y + 10, 28, 1, '#1a1214'); R_(c, x + 3, y + 10, 24, 2, col);
    bricks(c, x + 20, y - 0, 6, 12, '#5a4a48'); R_(c, x + 19, y, 8, 2, '#3a3030'); R_(c, x + 21, y - 2, 4, 2, '#9a9aa2'); // дым
    R_(c, x + 6, y + 15, 10, 12, '#1a1010'); R_(c, x + 7, y + 17, 8, 10, '#e8601e'); R_(c, x + 8, y + 19, 6, 8, '#f8a830'); R_(c, x + 10, y + 22, 3, 5, '#fff0a0'); R_(c, x + 5, y + 14, 12, 2, '#6a6a70');
    R_(c, x + 19, y + 16, 5, 6, '#9aa0aa'); R_(c, x + 19, y + 16, 5, 1, '#d0d4dc'); R_(c, x + 18, y + 13, 7, 3, '#b8bec8'); R_(c, x + 20, y + 22, 3, 5, '#4a4e58');
    R_(c, x + 27, y + 21, 4, 6, '#6a4a2a'); R_(c, x + 27, y + 21, 4, 1, '#8a6a3a'); R_(c, x + 27, y + 24, 4, 1, '#3a2a18'); R_(c, x + 12, y + 12, 2, 2, '#f8c830'); R_(c, x + 2, y + 22, 1, 1, '#f8c830'); R_(c, x + 1, y + 25, 1, 1, '#f8a830');
  },
  workshop(c, x, y, col) {
    rrect(c, x + 1, y + 27, 30, 5, '#8a7a50', 2);
    R_(c, x + 4, y + 13, 22, 15, '#e8dcc0'); for (let i = 0; i < 22; i += 7) R_(c, x + 4 + i, y + 13, 1, 15, '#5a3a1a'); R_(c, x + 4, y + 13, 22, 1, '#5a3a1a'); R_(c, x + 4, y + 20, 22, 1, '#5a3a1a'); R_(c, x + 4, y + 27, 22, 1, '#3a2410');
    tiles(c, x + 2, y + 6, 26, 8, col); gable(c, x + 6, y + 1, 18, 6, col); bricks(c, x + 20, y + 1, 4, 8, '#8a7a70'); R_(c, x + 19, y + 1, 6, 1, '#4a4040'); R_(c, x + 2, y + 13, 26, 1, '#3a2410'); win(c, x + 7, y + 15, 5, 5); win(c, x + 19, y + 15, 5, 5);
    R_(c, x + 12, y + 20, 6, 8, '#6a4a2a'); R_(c, x + 12, y + 20, 6, 1, '#8a6a3a'); R_(c, x + 17, y + 24, 1, 1, '#f2c230');
    R_(c, x + 26, y + 18, 1, 7, '#a8743a'); R_(c, x + 27, y + 19, 1, 5, '#a8743a'); R_(c, x + 25, y + 19, 1, 5, '#e8e8e8'); R_(c, x + 28, y + 20, 3, 8, '#8a5a2b'); R_(c, x + 28, y + 20, 3, 1, '#a8743a'); R_(c, x + 29, y + 17, 1, 3, '#f4f0e0'); R_(c, x + 30, y + 18, 1, 2, '#f4f0e0');
    R_(c, x + 1, y + 24, 3, 4, '#8a5a2b'); R_(c, x + 1, y + 24, 3, 1, '#a8743a'); R_(c, x + 2, y + 21, 1, 3, '#f4f0e0');
  },
  stable(c, x, y, col) {
    rrect(c, x + 1, y + 27, 30, 5, '#a89060', 2);
    planks(c, x + 1, y + 13, 30, 15, '#a8743a'); tiles(c, x, y + 6, 32, 8, '#6a3a1a'); R_(c, x, y + 6, 32, 2, col); gable(c, x + 8, y + 1, 16, 6, '#6a3a1a'); R_(c, x + 15, y + 3, 2, 2, '#d8c8a0');
    R_(c, x + 3, y + 16, 9, 12, '#2a1808'); R_(c, x + 4, y + 19, 7, 9, '#4a3018'); R_(c, x + 5, y + 14, 5, 5, '#9a6a3a'); R_(c, x + 4, y + 13, 3, 3, '#7a4e26'); R_(c, x + 9, y + 15, 2, 2, '#9a6a3a'); R_(c, x + 8, y + 16, 1, 1, '#14100c'); R_(c, x + 5, y + 14, 1, 2, '#5a3a1a');
    R_(c, x + 20, y + 16, 9, 12, '#2a1808'); R_(c, x + 20, y + 16, 9, 12, '#3a2410'); for (let i = 20; i < 29; i += 4) { R_(c, x + i, y + 16, 1, 12, '#8a5a2b'); } R_(c, x + 20, y + 21, 9, 1, '#8a5a2b'); R_(c, x + 20, y + 26, 9, 1, '#8a5a2b');
    R_(c, x + 13, y + 22, 6, 6, '#e8c850'); R_(c, x + 13, y + 22, 6, 1, '#f8e890'); R_(c, x + 14, y + 25, 4, 1, '#b89830');
  },
  market(c, x, y, col) {
    rrect(c, x + 1, y + 9, 30, 22, '#a89868', 5); for (let j = 11; j < 29; j += 4) for (let i = 4; i < 28; i += 4) R_(c, x + i + ((j / 4) & 1) * 2, y + j, 3, 1, '#98885a');
    for (let k = 0; k < 2; k++) { const sx = x + 1 + k * 15, sy = y + 6 + k * 3; for (let i = 0; i < 14; i += 2) R_(c, sx + i, sy, 2, 6, (i / 2) % 2 ? '#f4f0e0' : col); R_(c, sx, sy + 6, 14, 1, sh_(col, 0.6)); R_(c, sx, sy - 1, 14, 1, sh_(col, 1.3)); R_(c, sx + 1, sy + 7, 1, 10, '#5a3a1a'); R_(c, sx + 12, sy + 7, 1, 10, '#5a3a1a'); R_(c, sx + 1, sy + 12, 12, 4, '#8a5a2b'); R_(c, sx + 1, sy + 12, 12, 1, '#a8743a'); }
    R_(c, x + 3, y + 17, 2, 2, '#e84a3a'); R_(c, x + 6, y + 17, 2, 2, '#f2c230'); R_(c, x + 9, y + 17, 2, 2, '#6cc04a'); R_(c, x + 18, y + 20, 2, 2, '#e8802e'); R_(c, x + 21, y + 20, 2, 2, '#d8d0b0'); R_(c, x + 24, y + 20, 2, 2, '#c84a6a');
    R_(c, x + 2, y + 25, 5, 5, '#8a5a2b'); R_(c, x + 2, y + 25, 5, 1, '#a8743a'); R_(c, x + 3, y + 27, 3, 1, '#5a3a1a'); R_(c, x + 26, y + 25, 5, 5, '#a8743a'); R_(c, x + 26, y + 25, 5, 1, '#c8944a'); R_(c, x + 12, y + 26, 2, 4, '#f2c230'); R_(c, x + 16, y + 26, 2, 4, '#f2c230'); flag(c, x + 15, y, col);
  },
  siege(c, x, y, col) {
    rrect(c, x + 1, y + 27, 30, 5, '#8a7a50', 2);
    planks(c, x + 2, y + 13, 24, 15, '#9a6a3a'); tiles(c, x + 0, y + 7, 28, 7, '#5a3a1a'); R_(c, x + 0, y + 7, 28, 2, col); gable(c, x + 6, y + 2, 16, 6, '#5a3a1a');
    R_(c, x + 6, y + 17, 14, 11, '#2a1808'); for (let i = 6; i < 20; i += 4) R_(c, x + i, y + 17, 1, 11, '#8a5a2b'); R_(c, x + 6, y + 22, 14, 1, '#8a5a2b'); R_(c, x + 5, y + 16, 16, 1, '#d8c8a0');
    R_(c, x + 21, y + 4, 2, 7, '#5a3a1a'); R_(c, x + 18, y + 3, 7, 2, '#6a4a2a'); R_(c, x + 17, y + 2, 3, 2, '#a0a4ac');
    R_(c, x + 24, y + 22, 7, 3, '#8a5a2b'); R_(c, x + 24, y + 22, 7, 1, '#a8743a'); R_(c, x + 29, y + 21, 2, 5, '#7e838d'); R_(c, x + 25, y + 25, 3, 3, '#2a1a0c'); R_(c, x + 1, y + 24, 3, 4, '#6a4a2a');
  },
  wall(c, x, y, col, res, mask) {
    mask = mask === undefined ? 10 : mask; const N = mask & 1, E = mask & 2, S_ = mask & 4, W = mask & 8, ST = '#b8b0a0';
    if (!(N || E || S_ || W)) { bricks(c, x + 9, y + 9, 14, 20, ST); for (let i = 0; i < 4; i++) R_(c, x + 9 + i * 4, y + 6, 3, 3, ST); return; }
    if (N) { bricks(c, x + 10, y, 12, 14, ST); R_(c, x + 10, y, 12, 1, '#d8d0c0'); }
    if (S_) { bricks(c, x + 10, y + 22, 12, 10, ST); }
    if (E) { bricks(c, x + 20, y + 13, 12, 16, ST); for (let i = 0; i < 3; i++) R_(c, x + 21 + i * 4, y + 10, 3, 3, ST), R_(c, x + 21 + i * 4, y + 10, 3, 1, '#d8d0c0'); }
    if (W) { bricks(c, x, y + 13, 12, 16, ST); for (let i = 0; i < 3; i++) R_(c, x + 1 + i * 4, y + 10, 3, 3, ST), R_(c, x + 1 + i * 4, y + 10, 3, 1, '#d8d0c0'); }
    bricks(c, x + 10, y + 12, 12, 17, '#c4bcac'); for (let i = 0; i < 3; i++) R_(c, x + 10 + i * 5, y + 9, 3, 3, '#c4bcac'), R_(c, x + 10 + i * 5, y + 9, 3, 1, '#e0d8c8');
  },
  tower(c, x, y, col, res, mask) {
    mask = mask || 0; if (mask & 2) bricks(c, x + 22, y + 13, 10, 16, '#b8b0a0'); if (mask & 8) bricks(c, x, y + 13, 10, 16, '#b8b0a0'); if (mask & 1) bricks(c, x + 10, y, 12, 12, '#b8b0a0'); if (mask & 4) bricks(c, x + 10, y + 26, 12, 6, '#b8b0a0');
    rrect(c, x + 3, y + 28, 26, 4, '#8a8f78', 2);
    bricks(c, x + 7, y + 10, 18, 19, '#bab2a2'); R_(c, x + 7, y + 10, 3, 19, '#d0c8b8'); R_(c, x + 22, y + 10, 3, 19, '#908878');
    for (let i = 0; i < 5; i++) { R_(c, x + 6 + i * 4, y + 5, 3, 5, '#bab2a2'); R_(c, x + 6 + i * 4, y + 5, 3, 1, '#e0d8c8'); }
    R_(c, x + 6, y + 9, 20, 2, '#8a8272'); R_(c, x + 12, y + 14, 2, 5, '#14100c'); R_(c, x + 18, y + 14, 2, 5, '#14100c'); R_(c, x + 13, y + 22, 6, 7, '#3a2410'); R_(c, x + 13, y + 22, 6, 1, '#6a4a2a');
    R_(c, x + 16, y + 0, 1, 7, '#4a3018'); R_(c, x + 17, y + 0, 5, 3, col); R_(c, x + 17, y + 0, 5, 1, shadeC(col, 1.35));
  },
  gate(c, x, y, col, res, mask) {
    mask = mask === undefined ? 10 : mask; const vert = (mask & 5) && !(mask & 10);
    if (!vert) {
      bricks(c, x, y + 13, 9, 16, '#b8b0a0'); bricks(c, x + 23, y + 13, 9, 16, '#b8b0a0');
      bricks(c, x + 7, y + 10, 4, 19, '#c4bcac'); bricks(c, x + 21, y + 10, 4, 19, '#c4bcac'); for (const px of [7, 21]) R_(c, x + px, y + 7, 4, 3, '#c4bcac');
      R_(c, x + 11, y + 12, 10, 2, '#a89a80'); planks(c, x + 11, y + 14, 10, 15, '#7a4e26'); for (let j = 17; j < 28; j += 5) R_(c, x + 11, y + j, 10, 1, '#3a3a40'); R_(c, x + 15, y + 14, 2, 15, '#2a1808'); R_(c, x + 15, y + 22, 2, 2, '#f2c230');
      R_(c, x + 6, y + 6, 20, 1, '#8a8272'); R_(c, x + 15, y + 2, 2, 4, '#4a3018');
    } else { // ворота в вертикальной стене: башенки сверху и снизу, створки видны с торца
      bricks(c, x + 11, y, 10, 8, '#b8b0a0'); bricks(c, x + 11, y + 24, 10, 8, '#b8b0a0');
      bricks(c, x + 9, y + 6, 14, 8, '#c4bcac'); bricks(c, x + 9, y + 20, 14, 9, '#c4bcac');
      for (let i = 0; i < 3; i++) { R_(c, x + 9 + i * 5, y + 3, 4, 3, '#c4bcac'); R_(c, x + 9 + i * 5, y + 3, 4, 1, '#e0d8c8'); R_(c, x + 9 + i * 5, y + 17, 4, 3, '#c4bcac'); }
      R_(c, x + 9, y + 14, 14, 6, '#2a2018'); planks(c, x + 10, y + 14, 5, 6, '#7a4e26'); R_(c, x + 10, y + 16, 5, 1, '#3a3a40'); R_(c, x + 10, y + 18, 5, 1, '#3a3a40'); R_(c, x + 17, y + 15, 5, 4, '#14100c'); R_(c, x + 12, y + 17, 1, 1, '#f2c230');
    }
    R_(c, x + 17, y + 2, 4, 3, col);
  },
  academy(c, x, y, col) {
    rrect(c, x + 1, y + 27, 30, 5, '#8a8f78', 2); R_(c, x + 3, y + 25, 26, 3, '#a8a090'); R_(c, x + 2, y + 27, 28, 2, '#8a8272');
    bricks(c, x + 4, y + 12, 24, 13, '#cfc7b6'); for (const i of [6, 12, 18, 24]) { R_(c, x + i, y + 13, 3, 12, '#f0ead8'); R_(c, x + i, y + 13, 1, 12, '#fffcf0'); R_(c, x + i - 1, y + 12, 5, 2, '#f4f0e0'); R_(c, x + i - 1, y + 12, 5, 1, '#f2c230'); R_(c, x + i - 1, y + 24, 5, 1, '#c8c0b0'); }
    gable(c, x + 2, y + 3, 28, 9, col); R_(c, x + 13, y + 6, 6, 4, '#f4f0e0'); R_(c, x + 15, y + 5, 2, 6, '#f2c230'); R_(c, x + 14, y + 7, 4, 2, '#f2c230');
    R_(c, x + 13, y + 17, 6, 8, '#3a2410'); R_(c, x + 13, y + 17, 6, 1, '#f2c230'); R_(c, x + 3, y + 0, 1, 5, '#4a3018'); R_(c, x + 4, y + 0, 4, 3, col); R_(c, x + 28, y + 0, 1, 5, '#4a3018'); R_(c, x + 24, y + 0, 4, 3, col);
  },
  hospital(c, x, y, col) {
    rrect(c, x + 1, y + 27, 30, 5, '#8a8f78', 2);
    R_(c, x + 3, y + 12, 26, 16, '#e8e2d2'); R_(c, x + 3, y + 12, 26, 1, '#ffffff'); R_(c, x + 3, y + 27, 26, 1, '#9a9484'); R_(c, x + 3, y + 12, 1, 16, '#f6f2e6'); R_(c, x + 28, y + 12, 1, 16, '#b8b2a0');
    tiles(c, x + 1, y + 7, 30, 6, col); gable(c, x + 8, y + 1, 16, 7, col);
    R_(c, x + 14, y + 3, 4, 5, '#f4f0e0'); R_(c, x + 15, y + 2, 2, 7, '#d8302a'); R_(c, x + 13, y + 4, 6, 3, '#d8302a');
    R_(c, x + 13, y + 20, 6, 8, '#4a3018'); R_(c, x + 13, y + 20, 6, 1, '#6a4a2a'); win(c, x + 5, y + 16, 5, 5); win(c, x + 22, y + 16, 5, 5);
    R_(c, x + 14, y + 14, 4, 4, '#f4f0e0'); R_(c, x + 15, y + 13, 2, 6, '#d8302a'); R_(c, x + 14, y + 15, 4, 2, '#d8302a');
    R_(c, x + 2, y + 25, 3, 3, '#4aa83a'); R_(c, x + 27, y + 25, 3, 3, '#4aa83a'); R_(c, x + 3, y + 24, 1, 1, '#e84a5a');
  },
  port(c, x, y, col) {
    rrect(c, x + 1, y + 16, 30, 15, '#d8c890', 4); for (let i = 0; i < 14; i++) R_(c, x + 3 + (i * 5) % 26, y + 18 + (i * 3) % 11, 1, 1, '#c0b078');
    planks(c, x + 1, y + 24, 30, 5, '#9a6a3a'); for (let i = 0; i < 30; i += 6) R_(c, x + 1 + i, y + 29, 2, 3, '#5a3a1a'); R_(c, x + 1, y + 23, 30, 1, '#5a3a1a');
    planks(c, x + 3, y + 8, 14, 11, '#a8743a'); gable(c, x + 1, y + 3, 18, 6, col); R_(c, x + 8, y + 12, 4, 7, '#2a1808'); win(c, x + 4, y + 11, 3, 3);
    R_(c, x + 24, y + 4, 2, 17, '#5a3a1a'); R_(c, x + 20, y + 4, 10, 2, '#5a3a1a'); R_(c, x + 21, y + 6, 1, 7, '#c8b890'); R_(c, x + 20, y + 13, 3, 2, '#8a8f98'); flag(c, x + 28, y + 12, col);
    R_(c, x + 19, y + 15, 4, 4, '#8a5a2b'); R_(c, x + 19, y + 15, 4, 1, '#c08a50'); R_(c, x + 5, y + 20, 4, 4, '#a8743a'); R_(c, x + 5, y + 20, 4, 1, '#c8944a'); R_(c, x + 11, y + 20, 3, 4, '#6a4a2a'); R_(c, x + 11, y + 20, 3, 1, '#8a6a3a'); R_(c, x + 27, y + 17, 3, 4, '#6a4a2a');
  }
};
function drawCitySprite(c, x, y, col, lvl, cap) { // 64 x 80 (мелкие пиксели), рисуется в 32 x 40
  const oy = y + 16; rrect(c, x + 2, oy + 14, 60, 48, '#a89868', 9); for (let i = 0; i < 30; i++) R_(c, x + 8 + (i * 17) % 46, oy + 18 + (i * 11) % 40, 3, 1, '#98885a');
  // стены
  bricks(c, x + 6, oy + 38, 52, 20, '#b8b0a0'); for (let i = 0; i < 13; i++) R_(c, x + 6 + i * 4, oy + 35, 3, 3, '#b8b0a0'), R_(c, x + 6 + i * 4, oy + 35, 3, 1, '#d8d0c0');
  for (const tx of [0, 52]) { bricks(c, x + tx, oy + 24, 12, 36, '#a8a090'); for (let i = 0; i < 3; i++) R_(c, x + tx + i * 4, oy + 20, 3, 4, '#a8a090'); tiles(c, x + tx - 1, oy + 12, 14, 9, col); gable(c, x + tx + 1, oy + 6, 10, 7, col); R_(c, x + tx + 5, oy + 27, 2, 4, '#14100c'); R_(c, x + tx + 5, oy + 38, 2, 4, '#14100c'); flag(c, x + tx + 5, oy, col); }
  // ворота
  bricks(c, x + 24, oy + 42, 16, 18, '#a8a090'); R_(c, x + 27, oy + 46, 10, 14, '#2a1808'); for (let i = 0; i < 10; i += 2) R_(c, x + 27 + i, oy + 46, 1, 14, '#555b66'); for (let j = 0; j < 14; j += 3) R_(c, x + 27, oy + 47 + j, 10, 1, '#555b66'); R_(c, x + 26, oy + 44, 12, 2, '#d8d0c0');
  // замок
  const kw = 20 + (cap ? 4 : 0), kx = x + 32 - kw / 2; bricks(c, kx, oy + 18, kw, 22, '#c4bcac'); for (let i = 0; i < kw; i += 4) R_(c, kx + i, oy + 15, 3, 3, '#c4bcac'), R_(c, kx + i, oy + 15, 3, 1, '#e0d8c8');
  tiles(c, x + 24, oy + 2, 16, 10, col); gable(c, x + 26, oy - 5, 12, 8, col); win(c, x + 29, oy + 22, 6, 7); R_(c, x + 28, oy + 30, 8, 10, '#2a1808'); if (!cap) flag(c, x + 31, oy - 12, col); R_(c, kx + 2, oy + 24, 2, 4, '#14100c'); R_(c, kx + kw - 4, oy + 24, 2, 4, '#14100c');
  // дома
  const hs = [[10, 20], [44, 20], [14, 30], [40, 30], [20, 14], [36, 14]], n = Math.min(hs.length, 1 + lvl);
  for (let i = 0; i < n; i++) { const h = hs[i]; R_(c, x + h[0], oy + h[1] + 4, 10, 7, '#e8dcc0'); R_(c, x + h[0], oy + h[1] + 4, 10, 1, '#5a3a1a'); R_(c, x + h[0] + 4, oy + h[1] + 7, 3, 4, '#5a3a1a'); R_(c, x + h[0] + 1, oy + h[1] + 6, 2, 2, '#e8c860'); gable(c, x + h[0] - 1, oy + h[1], 12, 5, col); if (i < 2) { R_(c, x + h[0] + 8, oy + h[1] - 1, 2, 4, '#8a7a70'); R_(c, x + h[0] + 7, oy + h[1] - 2, 4, 1, '#4a4040'); } }
  R_(c, x + 2, oy + 58, 60, 3, '#6a5a40');
  if (cap) { const kx2 = x + 26; R_(c, kx2, oy - 8, 12, 4, '#f2c230'); R_(c, kx2, oy - 13, 2, 6, '#f2c230'); R_(c, kx2 + 5, oy - 15, 2, 8, '#f2c230'); R_(c, kx2 + 10, oy - 13, 2, 6, '#f2c230'); R_(c, kx2 + 1, oy - 7, 10, 1, '#fff0a0'); R_(c, kx2 + 5, oy - 11, 2, 2, '#e8402c'); }
}
// цифры 3x5
const DIG = ['111101101101111', '010110010010111', '111001111100111', '111001111001111', '101101111001001', '111100111001111', '111100111101111', '111001001001001', '111101111101111', '111101111001111'];
function drawNum(c, n, x, y, col) {
  const s = String(n); c.fillStyle = col || '#fff';
  for (let k = 0; k < s.length; k++) { const d = DIG[+s[k]]; for (let j = 0; j < 5; j++) for (let i = 0; i < 3; i++) if (d[j * 3 + i] === '1') c.fillRect(x + k * 4 + i, y + j, 1, 1); }
}
function numW(n) { return String(n).length * 4 - 1; }
function drawFigure(c, x, y, t, col) {
  if (t === 'archer') { R_(c, x + 1, y, 3, 2, '#2b6a24'); R_(c, x + 1, y + 2, 3, 2, '#e8c49a'); R_(c, x, y + 4, 5, 4, '#4aa83a'); R_(c, x + 5, y + 1, 1, 7, '#8a5a2b'); R_(c, x + 6, y + 2, 1, 5, '#f4f0e0'); }
  else if (t === 'light') { R_(c, x + 1, y, 3, 3, '#b8c4d4'); R_(c, x + 1, y + 3, 3, 1, '#e8c49a'); R_(c, x, y + 4, 5, 4, col); R_(c, x + 5, y + 2, 1, 5, '#e8e8f0'); R_(c, x - 1, y + 4, 2, 3, '#8a5a2b'); }
  else if (t === 'heavy') { R_(c, x, y, 5, 3, '#8a8f98'); R_(c, x + 1, y + 1, 3, 1, '#14100c'); R_(c, x - 1, y + 3, 7, 5, '#555b66'); R_(c, x, y + 4, 5, 1, col); R_(c, x + 6, y + 1, 1, 6, '#e8e8f0'); }
  else { R_(c, x - 1, y + 4, 8, 3, '#8a5a2b'); R_(c, x + 6, y + 2, 2, 3, '#8a5a2b'); R_(c, x - 1, y + 7, 1, 2, '#5a3a1a'); R_(c, x + 5, y + 7, 1, 2, '#5a3a1a'); R_(c, x + 1, y + 1, 3, 3, '#b8c4d4'); R_(c, x + 1, y + 4, 3, 1, col); R_(c, x + 4, y - 1, 1, 5, '#e8e8f0'); }
}
function drawShip(c, x, y, kind, col, dir) {
  R_(c, x + 1, y + 8, 14, 3, kind === 'war' ? '#3a2a20' : '#8a5a2b'); R_(c, x + 3, y + 11, 10, 1, '#5a3a1a'); R_(c, x + 7, y, 1, 9, '#5a3a1a');
  if (kind === 'war') { R_(c, x + 3, y + 1, 9, 6, '#d8d0c0'); R_(c, x + 3, y + 1, 9, 2, col); R_(c, x + 13, y + 5, 2, 3, col); } else { R_(c, x + 3, y + 1, 8, 6, '#f4f0e0'); R_(c, x + 4, y + 3, 6, 1, '#d6b250'); }
}

/* ===== Детальные спрайты (мелкие пиксели: 1 ед. = 0.5 пикселя мира) ===== */
const SKIN = '#e8c49a';
function shadeC(hex, f) { const n = parseInt(hex.slice(1), 16); const r = Math.min(255, (n >> 16 & 255) * f) | 0, g = Math.min(255, (n >> 8 & 255) * f) | 0, b = Math.min(255, (n & 255) * f) | 0; return `rgb(${r},${g},${b})`; }
const HF = {
  archer(c, x, y, col) {
    R_(c, x + 4, y + 10, 2, 3, '#4a2e14'); R_(c, x + 7, y + 10, 2, 3, '#4a2e14'); R_(c, x + 3, y + 12, 3, 1, '#2a1808'); R_(c, x + 7, y + 12, 3, 1, '#2a1808');
    R_(c, x + 2, y + 4, 2, 6, '#8a5a2b'); R_(c, x + 2, y + 3, 1, 1, '#eee'); R_(c, x + 3, y + 2, 1, 2, '#eee'); // колчан со стрелами
    R_(c, x + 3, y + 5, 7, 5, '#3f9a34'); R_(c, x + 3, y + 5, 7, 1, '#5cbf4c'); R_(c, x + 3, y + 8, 7, 1, '#5a3a1a'); R_(c, x + 3, y + 9, 7, 1, '#2f7a28');
    R_(c, x + 4, y + 1, 5, 4, SKIN); R_(c, x + 3, y, 7, 2, '#2b6a24'); R_(c, x + 3, y + 2, 1, 3, '#2b6a24'); R_(c, x + 7, y + 3, 1, 1, '#14100c'); R_(c, x + 4, y + 4, 4, 1, '#c9a47a');
    R_(c, x + 9, y + 5, 2, 2, SKIN); R_(c, x + 11, y + 2, 1, 9, '#a8743a'); R_(c, x + 10, y + 2, 1, 1, '#a8743a'); R_(c, x + 10, y + 10, 1, 1, '#a8743a'); R_(c, x + 10, y + 3, 1, 7, '#e8e8e8');
  },
  light(c, x, y, col) {
    R_(c, x + 4, y + 10, 2, 3, '#6a707c'); R_(c, x + 7, y + 10, 2, 3, '#6a707c'); R_(c, x + 3, y + 12, 3, 1, '#2a1808'); R_(c, x + 7, y + 12, 3, 1, '#2a1808');
    R_(c, x + 4, y + 5, 6, 5, '#b8c4d4'); R_(c, x + 4, y + 5, 6, 1, '#dde5ee'); R_(c, x + 6, y + 5, 2, 5, col); R_(c, x + 4, y + 8, 6, 1, '#5a3a1a'); R_(c, x + 4, y + 9, 6, 1, '#8f98a6');
    R_(c, x + 4, y + 1, 5, 4, SKIN); R_(c, x + 4, y, 5, 2, '#b8c4d4'); R_(c, x + 4, y + 2, 1, 2, '#8f98a6'); R_(c, x + 7, y + 3, 1, 1, '#14100c'); R_(c, x + 8, y + 2, 1, 3, '#8f98a6'); R_(c, x + 6, y - 0, 1, 1, col);
    R_(c, x, y + 5, 4, 5, col); R_(c, x, y + 5, 4, 1, shadeC(col, 1.3)); R_(c, x + 1, y + 6, 2, 3, shadeC(col, 0.7)); R_(c, x + 1, y + 7, 2, 1, '#f2c230'); R_(c, x, y + 10, 3, 1, shadeC(col, 0.6));
    R_(c, x + 10, y + 0, 1, 9, '#eef2f8'); R_(c, x + 11, y + 0, 1, 8, '#9aa4b4'); R_(c, x + 9, y + 8, 3, 1, '#f2c230'); R_(c, x + 10, y + 9, 1, 2, '#5a3a1a'); R_(c, x + 9, y + 5, 2, 2, SKIN);
  },
  heavy(c, x, y, col) {
    R_(c, x + 3, y + 10, 3, 3, '#3a3f4a'); R_(c, x + 7, y + 10, 3, 3, '#3a3f4a'); R_(c, x + 2, y + 12, 4, 1, '#14100c'); R_(c, x + 7, y + 12, 4, 1, '#14100c');
    R_(c, x + 3, y + 5, 7, 6, '#555b66'); R_(c, x + 3, y + 5, 7, 1, '#8a8f98'); R_(c, x + 5, y + 6, 3, 4, '#6a707c'); R_(c, x + 3, y + 9, 7, 1, '#2a2e36'); R_(c, x + 6, y + 6, 1, 4, col);
    R_(c, x + 2, y + 4, 3, 2, '#8a8f98'); R_(c, x + 8, y + 4, 3, 2, '#8a8f98'); R_(c, x + 2, y + 5, 3, 1, '#555b66');
    R_(c, x + 3, y, 7, 5, '#8a8f98'); R_(c, x + 3, y, 7, 1, '#b8c4d4'); R_(c, x + 4, y + 2, 5, 1, '#14100c'); R_(c, x + 6, y + 3, 1, 2, '#555b66'); R_(c, x + 6, y - 0, 1, 1, col); R_(c, x + 9, y + 1, 1, 4, '#555b66');
    R_(c, x, y + 5, 4, 7, col); R_(c, x, y + 5, 4, 1, shadeC(col, 1.3)); R_(c, x + 1, y + 6, 2, 5, shadeC(col, 0.7)); R_(c, x + 1, y + 8, 2, 1, '#f2c230'); R_(c, x + 1.5, y + 7, 1, 3, '#f2c230');
    R_(c, x + 10, y + 1, 1, 8, '#e8e8f0'); R_(c, x + 11, y + 1, 1, 7, '#9aa4b4'); R_(c, x + 9, y + 9, 3, 1, '#f2c230'); R_(c, x + 10, y + 10, 1, 2, '#5a3a1a');
  },
  cav(c, x, y, col) { // шире: 16 x 14
    R_(c, x + 3, y + 11, 1, 3, '#5a3a1a'); R_(c, x + 5, y + 11, 1, 3, '#5a3a1a'); R_(c, x + 10, y + 11, 1, 3, '#5a3a1a'); R_(c, x + 12, y + 11, 1, 3, '#5a3a1a');
    R_(c, x + 2, y + 7, 12, 5, '#9a6a3a'); R_(c, x + 2, y + 7, 12, 1, '#b8844a'); R_(c, x + 2, y + 11, 12, 1, '#7a4e26'); R_(c, x, y + 7, 2, 2, '#5a3a1a'); R_(c, x, y + 9, 1, 3, '#5a3a1a');
    R_(c, x + 12, y + 3, 3, 6, '#9a6a3a'); R_(c, x + 13, y + 2, 3, 3, '#9a6a3a'); R_(c, x + 15, y + 3, 1, 2, '#7a4e26'); R_(c, x + 14, y + 3, 1, 1, '#14100c'); R_(c, x + 12, y + 2, 1, 5, '#5a3a1a');
    R_(c, x + 4, y + 8, 7, 3, col); R_(c, x + 4, y + 8, 7, 1, shadeC(col, 1.3)); R_(c, x + 4, y + 10, 7, 1, shadeC(col, 0.7));
    R_(c, x + 6, y + 4, 3, 4, '#b8c4d4'); R_(c, x + 6, y + 4, 3, 1, col); R_(c, x + 6, y + 1, 3, 3, SKIN); R_(c, x + 6, y, 3, 2, '#b8c4d4'); R_(c, x + 8, y + 2, 1, 1, '#14100c');
    R_(c, x + 9, y + 5, 2, 1, SKIN); R_(c, x + 10, y - 1, 1, 10, '#a8743a'); R_(c, x + 10, y - 3, 1, 2, '#eef2f8'); R_(c, x + 11, y - 1, 2, 2, col);
  }
};
HF.cmd = function (c, x, y, col, cls) {
  HF[cls || 'light'](c, x, y, col); R_(c, x + 4, y - 1, 5, 2, '#f2c230'); R_(c, x + 4, y - 2, 1, 1, '#f2c230'); R_(c, x + 6, y - 2, 1, 1, '#fff0a0'); R_(c, x + 8, y - 2, 1, 1, '#f2c230'); R_(c, x + 2, y + 4, 1, 7, '#c02820'); R_(c, x + 3, y + 4, 1, 6, '#8a1a14');
};
HF.cat = function (c, x, y, col) {
  R_(c, x + 3, y + 9, 5, 5, '#2a1a0c'); R_(c, x + 4, y + 10, 3, 3, '#6a4a2a'); R_(c, x + 5, y + 11, 1, 1, '#2a1a0c'); R_(c, x + 14, y + 9, 5, 5, '#2a1a0c'); R_(c, x + 15, y + 10, 3, 3, '#6a4a2a'); R_(c, x + 16, y + 11, 1, 1, '#2a1a0c');
  R_(c, x + 2, y + 7, 18, 3, '#8a5a2b'); R_(c, x + 2, y + 7, 18, 1, '#a8743a'); R_(c, x + 9, y + 2, 2, 6, '#6a4a2a'); R_(c, x + 13, y + 2, 2, 6, '#6a4a2a');
  R_(c, x + 4, y + 7, 2, 1, '#5a3a1a'); R_(c, x + 6, y + 6, 2, 1, '#5a3a1a'); R_(c, x + 8, y + 5, 2, 1, '#5a3a1a'); R_(c, x + 10, y + 4, 2, 1, '#5a3a1a'); R_(c, x + 12, y + 3, 2, 1, '#5a3a1a'); R_(c, x + 14, y + 2, 2, 1, '#5a3a1a');
  R_(c, x + 15, y + 0, 4, 2, '#5a3a1a'); R_(c, x + 16, y - 1, 2, 2, '#a0a4ac'); R_(c, x + 1, y + 3, 3, 2, col); R_(c, x + 1, y + 3, 3, 1, shadeC(col, 1.35)); R_(c, x + 1, y + 3, 1, 5, '#4a3018');
};
HF.ram = function (c, x, y, col) {
  R_(c, x + 3, y + 10, 4, 4, '#2a1a0c'); R_(c, x + 4, y + 11, 2, 2, '#6a4a2a'); R_(c, x + 14, y + 10, 4, 4, '#2a1a0c'); R_(c, x + 15, y + 11, 2, 2, '#6a4a2a');
  R_(c, x + 3, y + 4, 1, 7, '#5a3a1a'); R_(c, x + 17, y + 4, 1, 7, '#5a3a1a'); R_(c, x + 1, y + 1, 19, 4, col); R_(c, x + 1, y + 1, 19, 1, shadeC(col, 1.35)); R_(c, x + 1, y + 4, 19, 1, shadeC(col, 0.6)); for (let i = 3; i < 19; i += 3) R_(c, x + i, y + 2, 1, 2, shadeC(col, 0.75));
  R_(c, x + 6, y + 5, 1, 3, '#c9b48a'); R_(c, x + 13, y + 5, 1, 3, '#c9b48a'); R_(c, x + 0, y + 7, 20, 3, '#8a5a2b'); R_(c, x + 0, y + 7, 20, 1, '#a8743a'); R_(c, x + 18, y + 6, 3, 5, '#7e838d'); R_(c, x + 18, y + 6, 3, 1, '#b8c0cc'); R_(c, x + 20, y + 7, 1, 3, '#4a4e58');
};
function armySpriteCanvas(types, col) { // 52 x 28 мелких пикселей, до 4 фигур
  const c = document.createElement('canvas'); c.width = 52; c.height = 28; const x = c.getContext('2d');
  const ts = types.slice(0, 4).sort((a, b) => (a === 'cav') - (b === 'cav')), n = ts.length;
  R_(x, 2, 25, 42, 2, 'rgba(0,0,0,0.28)');
  const pos = n === 1 ? [[ts[0] === 'cav' ? 14 : 18, 11]] : n === 2 ? [[8, 7], [24, 11]] : n === 3 ? [[2, 7], [14, 12], [26, 7]] : [[1, 6], [10, 12], [19, 7], [28, 12]];
  ts.forEach((t, i) => HF[t](x, pos[i][0], pos[i][1], col));
  R_(x, 47, 0, 1, 24, '#5a3a1a'); R_(x, 48, 1, 4, 6, col); R_(x, 48, 1, 4, 1, shadeC(col, 1.35)); R_(x, 48, 6, 4, 1, shadeC(col, 0.65)); R_(x, 49, 3, 2, 2, '#f4f0e0');
  return c;
}
function shipSpriteCanvas(kind, col) { // 36 x 24
  const c = document.createElement('canvas'); c.width = 36; c.height = 24; const x = c.getContext('2d');
  const hull = kind === 'war' ? '#3a2a20' : '#8a5a2b', hl = kind === 'war' ? '#5a4030' : '#b07840';
  R_(x, 3, 17, 30, 3, hull); R_(x, 3, 17, 30, 1, hl); R_(x, 5, 20, 26, 2, shadeC(hull, 0.7)); R_(x, 2, 15, 3, 3, hull); R_(x, 31, 15, 3, 3, hull); R_(x, 33, 14, 2, 2, hl);
  for (let i = 0; i < 6; i++) R_(x, 6 + i * 5, 18, 1, 2, shadeC(hull, 0.6));
  R_(x, 17, 1, 1, 17, '#5a3a1a'); R_(x, 9, 6, 1, 11, '#5a3a1a');
  if (kind === 'war') {
    R_(x, 12, 3, 12, 11, col); R_(x, 12, 3, 12, 1, shadeC(col, 1.35)); R_(x, 12, 13, 12, 1, shadeC(col, 0.65)); R_(x, 16, 6, 4, 4, '#f2c230'); R_(x, 17, 5, 2, 6, '#f2c230');
    R_(x, 3, 8, 6, 8, '#d8d0c0'); R_(x, 3, 8, 6, 1, '#fff'); R_(x, 3, 12, 6, 2, col); for (let i = 0; i < 6; i++) R_(x, 6 + i * 5, 15, 3, 2, col);
    R_(x, 18, 0, 6, 3, col); R_(x, 34, 17, 2, 1, '#8a8f98');
  } else {
    R_(x, 12, 3, 12, 12, '#f4f0e0'); R_(x, 12, 3, 12, 1, '#fff'); R_(x, 12, 14, 12, 1, '#c9c0a8'); R_(x, 12, 8, 12, 2, col); R_(x, 3, 8, 6, 8, '#e8e0cc'); R_(x, 3, 8, 6, 1, '#fff'); R_(x, 3, 11, 6, 1, col);
    R_(x, 18, 0, 5, 3, '#d6b250'); R_(x, 26, 14, 4, 3, '#a8743a'); R_(x, 27, 14, 2, 1, '#c08a50');
  }
  return c;
}

// якоря анимации (мелкие пиксели относительно спрайта)
const CITY_HS = [[10, 20], [44, 20], [14, 30], [40, 30], [20, 14], [36, 14]];

// ров: mask = стороны (1=N,2=E,4=S,8=W), где есть вода/ров (берега там нет)
function drawMoat(c, x, y, mask, wet) {
  const W_ = wet ? '#3d82c6' : '#6a5436', W2 = wet ? '#5a9ee0' : '#7a6444', W3 = wet ? '#2c68a8' : '#54422a';
  R_(c, x, y, 32, 32, W_); for (let i = 0; i < 9; i++) R_(c, x + 3 + (i * 11) % 26, y + 4 + (i * 7) % 24, 3, 1, W2); for (let i = 0; i < 6; i++) R_(c, x + 2 + (i * 13) % 28, y + 8 + (i * 5) % 20, 2, 1, W3);
  const G = '#6a9a3a', D = '#8a6a3a', D2 = '#a8864c';
  if (!(mask & 1)) { R_(c, x, y, 32, 4, D); R_(c, x, y, 32, 2, G); R_(c, x, y + 4, 32, 1, D2); }
  if (!(mask & 4)) { R_(c, x, y + 28, 32, 4, D); R_(c, x, y + 30, 32, 2, G); R_(c, x, y + 27, 32, 1, D2); }
  if (!(mask & 8)) { R_(c, x, y, 4, 32, D); R_(c, x, y, 2, 32, G); R_(c, x + 4, y, 1, 32, D2); }
  if (!(mask & 2)) { R_(c, x + 28, y, 4, 32, D); R_(c, x + 30, y, 2, 32, G); R_(c, x + 27, y, 1, 32, D2); }
}
