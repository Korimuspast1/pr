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
  'Тихая Заводь', 'Рыбий Мыс', 'Грозовой Перевал', 'Красный Лог', 'Вешняя Роща', 'Дубрава', 'Соколиный Ряд', 'Вольный Град', 'Холодный Предел',
  'Белокамень', 'Волчий Лог', 'Туманный Берег', 'Княжий Двор', 'Золотая Нива', 'Чёрный Утёс', 'Ольховый Брод', 'Сторожевая Гора',
  'Янтарный Берег', 'Кузнецкий Посад', 'Высокий Стан', 'Дальний Рубеж', 'Северный Оплот', 'Южный Торг', 'Седой Курган', 'Ивовый Плёс',
  'Гремячий Ключ', 'Светлояр', 'Багряный Холм', 'Королевский Брод', 'Верхний Замок', 'Нижний Посад', 'Тростниковая Гавань', 'Сосновый Бор',
  'Берёзовый Яр', 'Вересковая Пустошь', 'Ястребиный Утёс', 'Мельничный Ручей', 'Купеческая Пристань', 'Рудный Кряж', 'Лебяжье Озеро',
  'Зимний Перевал', 'Старая Крепость', 'Новый Торг', 'Львиные Врата', 'Тихий Дол', 'Гордая Скала', 'Монастырский Холм', 'Журавлиный Луг', 'Терновый Венец'];

const STOCK_KEYS = ['gold', 'wood', 'food', 'iron', 'bow', 'spear', 'sword', 'armL', 'armH', 'horse', 'xbow'];
const ITEM_KEYS = STOCK_KEYS.slice(4); // предметы для отрядов
const ITEM_CAP = 300; // предел склада предметов
const RNAME = { gold: 'Золото', wood: 'Дерево', food: 'Еда', iron: 'Железо', bow: 'Луки', spear: 'Копья', sword: 'Мечи', armL: 'Лёгкие доспехи', armH: 'Тяжёлые доспехи', horse: 'Кони', xbow: 'Арбалеты' };
const RVAL = { gold: 1, wood: 0.6, food: 0.6, iron: 0.9, bow: 3, spear: 3, sword: 3, armL: 3, armH: 4, horse: 4, xbow: 4 };

const BD = {
  farm: { n: 'Ферма', cost: { wood: 30, gold: 20 }, site: 'field', pts: 20, d: 'Даёт еду. Только на лугах и равнинах.' },
  sawmill: { n: 'Лесопилка', cost: { wood: 10, gold: 30 }, site: 'forest', pts: 20, d: 'Даёт дерево. Только в лесу, чем гуще лес — тем больше.' },
  mine: { n: 'Рудник', cost: { wood: 40, gold: 40 }, site: 'ore', pts: 30, d: 'Железо или золото. Только на залежах в горах.' },
  barracks: { n: 'Казармы', cost: { wood: 80, gold: 80, iron: 20 }, site: 'free', pts: 40, d: 'Обучают отряды по 10 воинов.' },
  forge: { n: 'Кузня', cost: { wood: 60, gold: 80, iron: 40 }, site: 'free', pts: 40, d: 'Куёт мечи и доспехи.' },
  workshop: { n: 'Мастерская', cost: { wood: 70, gold: 60 }, site: 'free', pts: 35, d: 'Делает луки и копья.' },
  stable: { n: 'Конюшня', cost: { wood: 80, gold: 70, food: 40 }, site: 'free', pts: 35, d: 'Выращивает боевых коней.' },
  market: { n: 'Рынок', cost: { wood: 60, gold: 100 }, site: 'free', pts: 35, d: 'Приносит золото, усиливает торговлю.' },
  siege: { n: 'Осадная мастерская', cost: { wood: 120, gold: 100, iron: 50 }, site: 'free', pts: 45, d: 'Строит катапульты и тараны — по одной машине.' },
  wall: { n: 'Стена', cost: { wood: 6, iron: 3, gold: 6 }, site: 'free', pts: 2, hp: 400, d: 'Крепостная стена. Преграждает путь врагу, пока её не сломают.' },
  tower: { n: 'Башня', cost: { wood: 40, iron: 25, gold: 50 }, site: 'free', pts: 15, hp: 600, d: 'В башне можно разместить отряд лучников — они получают защиту.' },
  gate: { n: 'Ворота', cost: { wood: 25, iron: 10, gold: 20 }, site: 'gate', pts: 6, hp: 250, d: 'Свои проходят, враги — нет. Ставятся в стену.' },
  moat: { n: 'Ров', cost: { gold: 10, wood: 2 }, site: 'moat', pts: 1, d: 'Ров с водой: должен соединяться с водоёмом. Атакующие в нём слабеют на 3–7%.' },
  academy: { n: 'Королевская академия', cost: { wood: 150, gold: 200, iron: 40 }, site: 'free', pts: 60, one: true, d: 'Обучает одного полководца. Академия может быть только одна.' },
  hospital: { n: 'Госпиталь', cost: { wood: 90, gold: 100, food: 30 }, site: 'free', pts: 40, d: 'Лечит армии рядом и возвращает раненых бойцов.' },
  mill: { n: 'Мельница', cost: { wood: 60, gold: 50 }, site: 'field', pts: 30, d: 'Ветряная мельница. Фермы вплотную к ней дают на 50% больше еды.' },
  tavern: { n: 'Таверна', cost: { wood: 80, gold: 120 }, site: 'free', pts: 35, d: 'Здесь нанимают наёмников: сразу и только за золото, без оружия со склада.' },
  port: { n: 'Порт', cost: { wood: 120, gold: 120, iron: 20 }, site: 'coast', pts: 50, d: 'Строит торговые и военные суда. Только на берегу моря или большого озера.' },
  house: { n: 'Дом', cost: { wood: 25, gold: 10 }, site: 'free', pts: 8, d: 'Дом жителей. Селяне селятся в провинции и сами строят дома, рынки и фермы.' },
  prison: { n: 'Тюрьма', cost: { wood: 50, gold: 60 }, site: 'free', pts: 10, d: '5 мест для заключённых на каждую тюрьму. Пока тюрьмы нет, жителей нельзя сажать.' },
  storehouse: { n: 'Склад', cost: { wood: 60, gold: 50, iron: 10 }, site: 'free', pts: 25, d: 'Богатые закрома: +100 к лимиту предметов на складе. Можно строить несколько.' },
  granary: { n: 'Амбар', cost: { wood: 90, gold: 70, food: 20 }, site: 'free', pts: 30, d: 'Запасы хлеба: ваши армии едят на 25% меньше.' },
  townhall: { n: 'Ратуша', cost: { wood: 120, gold: 150 }, site: 'free', pts: 40, one: true, d: 'Сердце города: города приносят на 20% больше золота. Только одна.' },
  embassy: { n: 'Посольство', cost: { wood: 100, gold: 180 }, site: 'free', pts: 35, one: true, d: 'Дипломатический дом: торговый доход на 30% выше. Только одно.' },
};
const TRAIN_BLD = { barracks: 1, siege: 1, academy: 1 }; // здания с очередью обучения
const STRUCT = { wall: 1, tower: 1, gate: 1 };
const RAM_DPS = { wall: 28, gate: 40, tower: 24 }, CAT_DMG = { wall: 30, gate: 34, tower: 28 };

// рецепты производства предметов
const RECIPES = {
  forge: { sword: { out: 'sword', in: { iron: 2, wood: 1 }, t: 6 }, armL: { out: 'armL', in: { iron: 3 }, t: 6 }, armH: { out: 'armH', in: { iron: 6, gold: 2 }, t: 9 } },
  workshop: { bow: { out: 'bow', in: { wood: 2 }, t: 4 }, spear: { out: 'spear', in: { wood: 2, iron: 1 }, t: 5 }, xbow: { out: 'xbow', in: { wood: 2, iron: 2 }, t: 7 } },
  stable: { horse: { out: 'horse', in: { food: 5, gold: 1 }, t: 8 } }
};
const SQ = {
  cmd: { n: 'Полководец', rdy: 'готов', need: { iron: 30 }, gold: 250, food: 40, t: 60, hp: 160, atk: 3.2, spd: 1.3, range: 1.6, ico: 'cmd', cnt: 1, bld: 'academy' },
  cat: { n: 'Катапульта', rdy: 'готова', need: { wood: 120, iron: 40 }, gold: 60, food: 10, t: 40, hp: 120, atk: 1.5, spd: 0.8, range: 5.0, ico: 'cat', cnt: 1, siege: true, bld: 'siege' },
  ram: { n: 'Таран', rdy: 'готов', need: { wood: 150, iron: 20 }, gold: 50, food: 10, t: 35, hp: 220, atk: 2.5, spd: 1.0, range: 1.6, ico: 'ram', cnt: 1, siege: true, bld: 'siege' },
  cannon: { n: 'Пороховая пушка', rdy: 'готова', need: { iron: 45 }, gold: 130, food: 15, t: 45, hp: 95, atk: 6.5, spd: 0.8, range: 5.2, ico: 'cannon', cnt: 1, siege: true, bld: 'siege', d: 'Дальний огонь из пороховой пушки: рвёт стены и строй. Боится конницы вблизи' },
  militia: { n: 'Ополчение', rdy: 'готово', need: {}, gold: 35, food: 25, t: 12, hp: 16, atk: 2.2, spd: 1.2, range: 1.6, ico: 'militia', bld: 'barracks', d: 'Дёшево и без оружия, но слабо' },
  xbow: { n: 'Арбалетчики', rdy: 'готовы', need: { xbow: 10 }, gold: 45, food: 30, t: 22, hp: 24, atk: 3.8, spd: 1.2, range: 3.7, ico: 'xbow', bld: 'barracks', d: 'Бронебойные болты: крепче лучников по тяжёлой броне' },
  archer: { n: 'Лучники', rdy: 'готовы', need: { bow: 10 }, gold: 40, food: 30, t: 20, hp: 20, atk: 3.2, spd: 1.2, range: 3.0, ico: 'bow', bld: 'barracks', d: 'Бьют издалека, сильны против лёгких рыцарей' },
  spear: { n: 'Копейщики', rdy: 'готовы', need: { spear: 10 }, gold: 50, food: 30, t: 22, hp: 32, atk: 3.4, spd: 1.2, range: 1.6, ico: 'spear', bld: 'barracks', d: 'Гроза конницы' },
  light: { n: 'Лёгкие рыцари', rdy: 'готовы', need: { sword: 10, armL: 10 }, gold: 60, food: 30, t: 25, hp: 35, atk: 3.5, spd: 1.4, range: 1.6, ico: 'armL', bld: 'barracks', d: 'Быстрая пехота, бьёт лучников' },
  heavy: { n: 'Тяжёлые рыцари', rdy: 'готовы', need: { sword: 10, armH: 10 }, gold: 100, food: 40, t: 30, hp: 60, atk: 4.0, spd: 0.9, range: 1.6, ico: 'armH', bld: 'barracks', d: 'Крепкая броня, сильны против конницы' },
  cav: { n: 'Конница', rdy: 'готова', need: { horse: 10 }, gold: 90, food: 40, t: 30, hp: 45, atk: 4.5, spd: 2.2, range: 1.6, ico: 'horse', bld: 'barracks', d: 'Самая быстрая, сметает лучников' }
};
// множители урона: атакующий -> защитник
const MATCH = {
  archer: { archer: 1.0, light: 1.3, heavy: 0.6, cav: 0.9 },
  light: { archer: 1.2, light: 1.0, heavy: 0.7, cav: 0.9 },
  heavy: { archer: 1.2, light: 1.3, heavy: 1.0, cav: 1.25 },
  cav: { archer: 1.7, light: 1.2, heavy: 0.6, cav: 1.0 }
};
const mm = (a, b) => { const r = MATCH[a]; return r && r[b] !== undefined ? r[b] : 1; };
MATCH.archer.xbow = 0.9; MATCH.light.xbow = 1.15; MATCH.heavy.xbow = 0.75; MATCH.cav.xbow = 1.2; MATCH.archer.cat = 1.2; MATCH.archer.ram = 0.5; MATCH.light.cat = 1.3; MATCH.light.ram = 1.0; MATCH.heavy.cat = 1.2; MATCH.heavy.ram = 1.1; MATCH.cav.cat = 1.6; MATCH.cav.ram = 0.8;
MATCH.cat = { archer: 1, light: 1, heavy: 1, cav: 1 }; MATCH.ram = { archer: 0.5, light: 0.5, heavy: 0.5, cav: 0.5 };
// копейщики — гроза конницы; ополчение слабее всех
MATCH.xbow = { archer: 1.15, light: 1.2, heavy: 1.25, cav: 0.9, spear: 1.05, militia: 1.4, cat: 1.2, ram: 1.0, cannon: 0.9, xbow: 1.0, cmd: 1.0 };
MATCH.spear = { archer: 1.0, light: 0.9, heavy: 0.8, cav: 1.7, spear: 1.0, militia: 1.3, cat: 1.2, ram: 1.0, xbow: 1.35 };
MATCH.militia = { archer: 0.9, light: 0.7, heavy: 0.5, cav: 0.7, spear: 0.8, militia: 1.0, cat: 1.1, ram: 0.8, xbow: 1.25 };
MATCH.cannon = { archer: 1.3, light: 1.1, heavy: 1.3, cav: 0.5, spear: 1.0, militia: 1.4, cat: 1.2, ram: 1.2, cannon: 1.0, cmd: 1.1, xbow: 1.25 };
MATCH.cav.cannon = 1.9; MATCH.light.cannon = 1.6; MATCH.archer.cannon = 0.8; MATCH.heavy.cannon = 0.9; MATCH.spear.cannon = 1.1; MATCH.militia.cannon = 0.7;
MATCH.archer.spear = 1.2; MATCH.archer.militia = 1.3; MATCH.light.spear = 1.1; MATCH.light.militia = 1.4;
MATCH.heavy.spear = 1.2; MATCH.heavy.militia = 1.5; MATCH.cav.spear = 0.6; MATCH.cav.militia = 1.5;
const TMULT = [0, 0, 0.9, 1, 1, 0.7, 0.6, 0.35]; // скорость по местности
const CITY_MIN = 9;
const MAX_SQ_ARMY = 8;
const MOVE_MULT = 0.45; // замедление походов армий
const AURA_R = 4.5, AURA_ATK = 1.25, AURA_DEF = 0.8;
const CMD = { archer: { n: 'Воевода', of: 'лучников', ico: 'bow' }, light: { n: 'Капитан', of: 'лёгких рыцарей', ico: 'armL' }, heavy: { n: 'Лорд-командор', of: 'тяжёлых рыцарей', ico: 'armH' }, cav: { n: 'Маршал', of: 'конницы', ico: 'horse' } };
const DAY = 120; // секунд игрового времени в одних сутках (2 минуты)
const CLAIM_DAYS = 3; // дней на формирование претензии
const DIFF = { easy: { n: 'Лёгкая', mult: 0.8, think: 5.5 }, normal: { n: 'Средняя', mult: 1.0, think: 3.2 }, hard: { n: 'Сложная', mult: 1.25, think: 1.8 } };
/* ===== Времена года и погода =====
   Сезон меняется каждые 5 дней (SEASON_LEN). Погода детерминированно выбирается
   из сезона и времени: одна и та же минута игры во всех загрузках даёт одну погоду,
   поэтому ничего сохранять не нужно. цифры — веса выбора, 0-индексы сезонов в ok. */
const SEASON_LEN = DAY * 5;
const SEASONS = [
  { id: 'spring', n: 'весна', ico: '🌸', farm: 1.0, temp: 11 },
  { id: 'summer', n: 'лето',  ico: '☀️', farm: 1.15, temp: 23 },
  { id: 'autumn', n: 'осень', ico: '🍂', farm: 0.85, temp: 9 },
  { id: 'winter', n: 'зима',  ico: '❄️', farm: 0.35, temp: -6 },
];
const WEATHERS = [
  { id: 'clear',  w: 22, n: 'ясно',        ico: '☀️',  lo: 9,  hi: 27, move: 1.05, farm: 1.0,  range: 1.0,  desc: 'Войска идут быстрее, +5% к скорости.',                  ok: [0,1,2,3] },
  { id: 'partly', w: 16, n: 'переменная облачность', ico: '⛅', lo: 8, hi: 24, move: 1.0,  farm: 1.0,  range: 1.0,  desc: 'Обычная погода, без сюрпризов.',          ok: [0,1,2,3] },
  { id: 'cloud',  w: 13, n: 'пасмурно',    ico: '☁️',  lo: 6,  hi: 19, move: 1.0,  farm: 1.0,  range: 1.0,  desc: 'Тучи низко висят, но осадков нет.',        ok: [0,1,2,3] },
  { id: 'rain',   w: 12, n: 'дождь',       ico: '🌧️', lo: 5,  hi: 17, move: 0.85, farm: 1.3,  range: 0.9,  desc: 'Фермы +30% еды, войска на 15% медленнее, стрельба хуже.', ok: [0,1,2] },
  { id: 'fog',    w: 7,  n: 'туман',       ico: '🌫️', lo: 4,  hi: 14, move: 0.9,  farm: 1.0,  range: 0.65, desc: 'Стрельба вдвое короче видимости — осторожно с засадой.', ok: [0,1,2,3] },
  { id: 'wind',   w: 5,  n: 'сильный ветер', ico: '💨', lo: 3, hi: 18, move: 0.95, farm: 1.0,  range: 0.8,  desc: 'Стрелы сдувает: стрельба на 20% короче.',  ok: [0,1,2,3] },
  { id: 'storm',  w: 5,  n: 'гроза',       ico: '⛈️', lo: 7,  hi: 20, move: 0.7,  farm: 1.15, range: 0.75, desc: 'Ливень и молнии: +15% еды с ферм, войска на 30% медленнее, молния бьёт в здания.', ok: [0,1,2] },
  { id: 'heat',   w: 4,  n: 'аномальная жара', ico: '🔥', lo: 25, hi: 36, move: 0.95, farm: 0.55, range: 1.0, desc: 'Земля сохнет: фермы дают на 45% меньше еды.',     ok: [1] },
  { id: 'snow',   w: 8,  n: 'снегопад',    ico: '❄️',  lo: -12, hi: 0, move: 0.75, farm: 0.15,  range: 0.9,  desc: 'Фермы в снегу молчат, войска на 25% медленнее.',  ok: [3] },
  { id: 'frost',  w: 4,  n: 'лютая стужа', ico: '🥶',  lo: -25, hi: -10, move: 0.65, farm: 0.1,  range: 0.85, desc: 'Мороз сковывает: армия на 35% медленнее, фермы стоят.', ok: [3] },
];
/* погода за «кусок» игрового часа: детерминированно от семени партии и номера куска */
function weatherOf(S) {
  const season = Math.floor(S.time / SEASON_LEN) % 4;
  const bucket = Math.floor(S.time / (DAY / 2.2)); // новые тучи примерно раз в четверть дня
  const rnd = mulberry32(((S.seed ^ (bucket * 2654435761)) >>> 0) || 1);
  const pool = WEATHERS.filter(w => w.ok.indexOf(season) >= 0);
  let tw = 0; for (const w of pool) tw += w.w;
  let r = rnd() * tw, pick = pool[0];
  for (const w of pool) { r -= w.w; if (r <= 0) { pick = w; break; } }
  const h = todHours(S);
  const dayK = (Math.sin((h - 9) / 24 * 2 * Math.PI) + 1) / 2; // в 15:00 теплее всего
  const temp = Math.round(pick.lo + (pick.hi - pick.lo) * dayK);
  return Object.assign({ temp, season: SEASONS[season], seasonI: season }, pick);
}
function seasonOf(S) { return SEASONS[Math.floor(S.time / SEASON_LEN) % 4]; }
const GAME_VERSION = '2.9.1';
const MAPTYPES = ['continent', 'continents', 'islands', 'lakes', 'random'];
const CAP_MIN = 10.5; // минимальное расстояние между столицами
const SEA_MIN = 10; // меньший водоём — пруд: порт у него не строят
// особенности держав (по номеру державы)
const PERKS = [
  { id: 'gold', n: 'Казначеи', d: 'Города приносят на 25% больше золота' },
  { id: 'craft', n: 'Оружейники', d: 'Кузни, мастерские и конюшни работают на 30% быстрее' },
  { id: 'mason', n: 'Каменщики', d: 'Стены, башни и ворота на 40% прочнее' },
  { id: 'mine', n: 'Рудокопы', d: 'Рудники дают на 30% больше' },
  { id: 'drill', n: 'Военная школа', d: 'Отряды обучаются на 25% быстрее' },
  { id: 'wood', n: 'Лесорубы', d: 'Лесопилки дают на 30% больше дерева' },
  { id: 'sea', n: 'Мореходы', d: 'Порты приносят втрое больше золота, суда на 30% дешевле' },
  { id: 'farm', n: 'Землепашцы', d: 'Фермы дают на 30% больше еды' }
];
const perk = (p, id) => !!p && p.perk === id;
// наёмники в таверне: цена отряда в золоте
const MERC = { militia: 80, archer: 210, spear: 190, light: 250, cav: 300, heavy: 330 };

/* ===== Санкции (как в Victoria 3) ===== */
const SANCTIONS = {
  transit: { n: 'Закрыть транзит', cost: 40, days: 200, d: 'Отменить право прохода и закрыть границы: их войска не смогут ходить по вашей земле.' },
  embargo: { n: 'Торговое эмбарго', cost: 80, days: 200, d: 'Полный запрет торговли: соглашение расторгается, нового не будет.' },
  tariff: { n: 'Таможенные пошлины', cost: 60, days: 6, d: 'Пошлина с торговли: половина их торгового золота идёт в вашу казну.' },
  arms: { n: 'Запрет на оружие', cost: 70, days: 6, d: 'Кузни, мастерские и конюшни у них работают на 40% медленнее.' },
  food: { n: 'Продовольственное эмбарго', cost: 60, days: 6, d: 'Их фермы дают на 30% меньше еды.' },
  gold: { n: 'Финансовая блокада', cost: 70, days: 6, d: 'Их доход золота падает на 25%.' },
  ports: { n: 'Закрыть порты', cost: 80, days: 6, d: 'Порты приносят им вдвое меньше, торговые суда к ним не приходят.' },
  ore: { n: 'Запрет поставок руды', cost: 60, days: 6, d: 'Их рудники дают на 30% меньше железа.' },
  mercs: { n: 'Запрет найма наёмников', cost: 50, days: 6, d: 'Их таверны перестают нанимать отряды.' },
  seize: { n: 'Конфискация казны', cost: 100, days: 0, once: 1, d: 'Единоразово забрать до 250 золота из их казны. Отношения резко ухудшатся.' },
  envoys: { n: 'Отозвать послов', cost: 40, days: 4, d: 'Дипломатия заморожена: сделки с ними невозможны.' },
  protest: { n: 'Дипломатический протест', cost: 30, days: 0, once: 1, d: 'Публичный протест: их репутация и отношения с вами портятся.' }
};
const SAN_KEYS = Object.keys(SANCTIONS);

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

/* ===== Иконки: векторный набор SVG =====
   Единый стиль: тёмный контур (#241a12), тёплая «пергаментная» палитра,
   жирные формы — читаются и в 18 px, и на большой кнопке меню. */
const SV_INK = '#241a12';
const svgSword = '<path d="M12 2.2 13.5 6.1v6.4h-3V6.1Z" fill="#cdd6e2" stroke="#241a12" stroke-width="1.4" stroke-linejoin="round"/><path d="M9.2 12.5h5.6" stroke="#241a12" stroke-width="2.1" stroke-linecap="round"/><rect x="10.8" y="12.5" width="2.4" height="5" rx="0.9" fill="#8a5a2b" stroke="#241a12" stroke-width="1.3"/><circle cx="12" cy="19.4" r="1.15" fill="#f2c230" stroke="#241a12" stroke-width="1.2"/>';
const SV = {
  gold: '<circle cx="12" cy="12" r="8.3" fill="#f2c230" stroke="#241a12" stroke-width="1.7"/><circle cx="12" cy="12" r="4.7" fill="none" stroke="#c08a1c" stroke-width="1.5"/><path d="M8.5 9.3a5.1 5.1 0 0 1 3-1.6" fill="none" stroke="#ffe9a0" stroke-width="1.6" stroke-linecap="round"/>',
  wood: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><rect x="2" y="13.5" width="12" height="6" rx="3" fill="#a06a35"/><rect x="10" y="13.5" width="12" height="6" rx="3" fill="#8a5a2b"/><rect x="6" y="7.4" width="12" height="6" rx="3" fill="#b57e47"/><circle cx="4.8" cy="16.5" r="1.15" fill="#d9b380" stroke-width="1.2"/><circle cx="12.8" cy="16.5" r="1.15" fill="#d9b380" stroke-width="1.2"/><circle cx="8.8" cy="10.4" r="1.15" fill="#eac894" stroke-width="1.2"/></g>',
  food: '<g stroke="#241a12" stroke-width="1.4" stroke-linejoin="round" stroke-linecap="round"><path d="M12 21.5V5.5" fill="none" stroke-width="1.7"/><path d="M12 6.3C12 4 10.5 2.7 8.3 2.5c.3 2.1 1.7 3.4 3.7 3.8Z" fill="#e8c558"/><path d="M12 6.3c0-2.3 1.5-3.6 3.7-3.8-.3 2.1-1.7 3.4-3.7 3.8Z" fill="#f2d27a"/><path d="M12 11.3c0-2.3-1.5-3.6-3.7-3.8.3 2.1 1.7 3.4 3.7 3.8Z" fill="#e8c558"/><path d="M12 11.3c0-2.3 1.5-3.6 3.7-3.8-.3 2.1-1.7 3.4-3.7 3.8Z" fill="#f2d27a"/><path d="M12 16.3c0-2.3-1.5-3.6-3.7-3.8.3 2.1 1.7 3.4 3.7 3.8Z" fill="#e8c558"/><path d="M12 16.3c0-2.3 1.5-3.6 3.7-3.8-.3 2.1-1.7 3.4-3.7 3.8Z" fill="#f2d27a"/><path d="M12 21.3c0-2.3-1.5-3.6-3.7-3.8.3 2.1 1.7 3.4 3.7 3.8Z" fill="#e8c558"/><path d="M12 21.3c0-2.3 1.5-3.6 3.7-3.8-.3 2.1-1.7 3.4-3.7 3.8Z" fill="#f2d27a"/></g>',
  iron: '<g stroke="#241a12" stroke-width="1.7" stroke-linejoin="round"><path d="M7 8.6h10l2.6 5.5a1.6 1.6 0 0 1-1.5 2.1H5.9a1.6 1.6 0 0 1-1.5-2.1Z" fill="#b8c4d4"/><path d="M7.7 10.7h8.6" stroke="#e8eef6" stroke-width="1.3" stroke-linecap="round"/></g>',
  bow: '<g stroke-linecap="round" stroke-linejoin="round"><path d="M6.2 3.2c6.2 3 6.2 14.6 0 17.6" fill="none" stroke="#8a5a2b" stroke-width="2.3"/><path d="M6.2 3.2v17.6" stroke="#d8d2c4" stroke-width="1.3" fill="none"/><path d="M4 12h12.5" stroke="#241a12" stroke-width="1.7" fill="none"/><path d="M13.2 8.7 17.2 12l-4 3.3" fill="none" stroke="#b8c4d4" stroke-width="1.8"/></g>',
  sword: '<g transform="rotate(45 12 12)">' + svgSword + '</g>',
  armL: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><path d="M8.6 3.6h6.8l2.7 4.1-1.2 2 1.6 10a1.6 1.6 0 0 1-1.6 1.7H7.1a1.6 1.6 0 0 1-1.6-1.7l1.6-10-1.2-2Z" fill="#8a5a2b"/><path d="M9.2 7.6h5.6M8.3 12.2h7.4" stroke="#5a3a1a" stroke-width="1.4" fill="none"/><circle cx="12" cy="15.9" r="1.05" fill="#f2c230" stroke-width="1.1"/></g>',
  armH: '<g stroke="#241a12" stroke-width="1.7" stroke-linejoin="round"><path d="M12 2.6c4.5 0 6.9 3 6.9 7.1V18a1.6 1.6 0 0 1-1.6 1.6H6.7A1.6 1.6 0 0 1 5.1 18V9.7c0-4.1 2.4-7.1 6.9-7.1Z" fill="#b8c4d4"/><path d="M5.1 10.7h13.8" stroke="#6a707c" stroke-width="1.5" fill="none"/><path d="M8.9 14.1h2M13.1 14.1h2" stroke-width="1.5" stroke-linecap="round"/><path d="M9.2 5.1c.9-.7 1.9-1 2.8-1" stroke="#e8eef6" stroke-width="1.3" fill="none" stroke-linecap="round"/></g>',
  horse: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M15.9 3.1 13.2 5.3c-3.5.8-5.6 3.3-6.2 6.9l-2.5 5.4a1.5 1.5 0 0 0 2.1 1.8l3.1-1 1.3 3.2 2.3.5-1-3.6c2.6-.7 4.3-2.6 4.8-5.3l2-5.3c.5-1.9-.5-3.5-2.3-4.1Z" fill="#a06a35"/><circle cx="13.4" cy="8.7" r="0.7" fill="#241a12" stroke="none"/><path d="M14.3 5.4c-2.6.6-4.6 2.5-5.4 5" fill="none" stroke="#5a3a1a" stroke-width="1.3"/></g>',
  medal: '<path d="M8.4 2.6H4.2l3 7.4" fill="#c93a2e" stroke="#241a12" stroke-width="1.5" stroke-linejoin="round"/><path d="M15.6 2.6h4.2l-3 7.4" fill="#3b6fe0" stroke="#241a12" stroke-width="1.5" stroke-linejoin="round"/><circle cx="12" cy="14.4" r="6.3" fill="#f2c230" stroke="#241a12" stroke-width="1.7"/><path d="M12 11l1.14 2.3 2.53.37-1.83 1.79.43 2.53L12 17.46 9.73 18l.43-2.55-1.83-1.79 2.53-.37Z" fill="#b8861c"/>',
  mail: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><rect x="2.6" y="5.2" width="18.8" height="13.6" rx="1.9" fill="#ece0c4"/><path d="M3.8 7.2 12 13.4l8.2-6.2" fill="none"/></g><circle cx="12" cy="12.8" r="1.5" fill="#c93a2e" stroke="#241a12" stroke-width="1"/>',
  t_map: '<g stroke="#241a12" stroke-width="1.5" stroke-linejoin="round"><path d="M9 4.4 3.6 6.3v13.2L9 17.6l6 1.9 5.4-1.9V4.4L15 6.3Z" fill="#ece0c4"/><path d="M9 4.4v13.2M15 6.3v12.2" stroke="#a89880" fill="none"/><path d="M6.2 9.6c2 1.2 2.4-1.2 4.4.2M12.6 13c1.6 1 2.2-.8 3 .6" fill="none" stroke="#8a5a2b" stroke-dasharray="1.5 1.6"/></g>',
  t_city: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><path d="M5 20v-9.4h2.3V7.4h2.4v3.2h4.6V7.4h2.4v3.2H19V20Z" fill="#a8adb8"/><path d="M10.2 20v-4.3a1.8 1.8 0 0 1 3.6 0V20Z" fill="#241a12"/><path d="M5 20h14" stroke-width="1.3"/></g>',
  t_build: '<g transform="rotate(-45 12 12)" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><rect x="9.3" y="9.6" width="5.4" height="11.6" rx="1.2" fill="#8a5a2b"/><path d="M4.8 4.6h14.4l-1.7 5.6H6.5Z" fill="#b8c4d4"/></g>',
  t_army: '<g transform="rotate(45 12 12) scale(0.86) translate(1.9 1.9)">' + svgSword + '</g><g transform="rotate(-45 12 12) scale(0.86) translate(1.9 1.9)">' + svgSword + '</g>',
  t_pol: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><path d="M4.4 17.4 3 8.2l5.5 3.7L12 5l3.5 6.9L21 8.2l-1.4 9.2Z" fill="#f2c230"/><path d="M5.6 20.6h12.8" stroke-width="1.9" stroke-linecap="round"/><circle cx="12" cy="13" r="0.85" fill="#c93a2e" stroke="none"/><circle cx="7.3" cy="15.1" r="0.7" fill="#3b6fe0" stroke="none"/><circle cx="16.7" cy="15.1" r="0.7" fill="#3b6fe0" stroke="none"/></g>',
  t_fleet: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M4.8 15.4h14.4l-2.2 4.3a1.6 1.6 0 0 1-1.4 1H8.4a1.6 1.6 0 0 1-1.4-1Z" fill="#8a5a2b"/><path d="M12 3.4v12.2" fill="none"/><path d="M12 4.7c3.5.7 5.4 3.4 5.9 7.3H12Z" fill="#ece0c4"/><path d="M11 6.6c-2.3.8-3.7 2.8-4.1 5.9H11Z" fill="#d8d2c4"/></g>',
  ship: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M6 15.2h12l-1.8 3.7a1.4 1.4 0 0 1-1.2.9H9a1.4 1.4 0 0 1-1.2-.9Z" fill="#6b4423"/><path d="M12 5v10.4" fill="none"/><path d="M12 5.9 17 13.8h-5Z" fill="#ece0c4"/><path d="M11.1 7.6 8.2 13.4h2.9Z" fill="#d8d2c4"/></g>',
  cannon: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M3.2 13.9 16.6 7.3l1.9 3.9L5.1 17.8Z" fill="#3f4754"/><circle cx="7.6" cy="19" r="2.1" fill="#241a12"/><circle cx="7.6" cy="19" r="0.8" fill="#6a707c" stroke="none"/><circle cx="14.6" cy="17.2" r="2.1" fill="#241a12"/><circle cx="14.6" cy="17.2" r="0.8" fill="#6a707c" stroke="none"/><path d="M18.2 6.9 20.2 4.7" stroke="#e8832e" stroke-width="1.7"/></g>',
  cat: '<g stroke="#241a12" stroke-width="1.5" stroke-linejoin="round" stroke-linecap="round"><path d="M6.6 9.4 5.4 4.7 9.7 7a12.8 12.8 0 0 1 4.6 0l4.3-2.3-1.2 4.7a7.4 7.4 0 0 1 1.9 4.7c0 3.6-2.8 5.9-7.3 5.9s-7.3-2.3-7.3-5.9a7.4 7.4 0 0 1 1.9-4.7Z" fill="#d98a3a"/><circle cx="9.3" cy="13.7" r="0.55" fill="#241a12" stroke="none"/><circle cx="14.7" cy="13.7" r="0.55" fill="#241a12" stroke="none"/><path d="M11.2 16.2 12 17l.8-.8" fill="none"/></g>',
  ram: '<g stroke="#241a12" stroke-width="1.5" stroke-linejoin="round" stroke-linecap="round"><path d="M3.6 12.3h12.6" stroke="#6b4423" stroke-width="3.2" fill="none"/><path d="M13.2 10.2 19.2 12.3l-6 2.1Z" fill="#8f959e"/><path d="M6.8 13.6 5 18.2M12.4 13.6l1.8 4.6" fill="none"/><circle cx="4.4" cy="19.4" r="1.35" fill="#241a12"/><circle cx="15.4" cy="19.4" r="1.35" fill="#241a12"/></g>',
  cmd: '<g stroke="#241a12" stroke-width="1.5" stroke-linejoin="round" stroke-linecap="round"><path d="M7 13.2a5.2 5.2 0 0 1 10 0v5.9H7Z" fill="#b8c4d4"/><path d="M7 15.5h10" stroke="#6a707c" stroke-width="1.3" fill="none"/><path d="M12.2 7.9c-.4-3.2.9-5 3.2-6-.6 2.1-.1 3.8 1.2 5.2Z" fill="#c93a2e"/><circle cx="12" cy="17.6" r="0.55" fill="#241a12" stroke="none"/></g>',
  menu: '<g stroke="#ece0c4" stroke-width="2.2" stroke-linecap="round" fill="none"><path d="M4.5 6.6h15M4.5 12h15M4.5 17.4h15"/></g>',
  pause: '<rect x="7" y="5" width="3.7" height="14" rx="1" fill="#e8c558" stroke="#241a12" stroke-width="1.5"/><rect x="13.3" y="5" width="3.7" height="14" rx="1" fill="#e8c558" stroke="#241a12" stroke-width="1.5"/>',
  hour: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M6.4 3.5h11.2M6.4 20.5h11.2" fill="none" stroke-width="2.1"/><path d="M8 3.5c0 4.8 2.6 5.4 4 7.2-1.4 1.8-4 2.4-4 7.3M16 3.5c0 4.8-2.6 5.4-4 7.2 1.4 1.8 4 2.4 4 7.3" fill="rgba(138,208,232,0.18)"/><path d="M10.2 14.6h3.6L12 17Z" fill="#e8c558"/></g>',
  trophy: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><path d="M8 3.6h8v3.4a4 4 0 0 1-8 0Z" fill="#f2c230"/><path d="M8 5.4H5.3a3 3 0 0 0 3.1 3.6M16 5.4h2.7a3 3 0 0 1-3.1 3.6" fill="none" stroke-linecap="round"/><path d="M11 11h2v2.8h-2Z" fill="#c08a1c"/><path d="M9.4 17.2h5.2l.9 3.3H8.5Z" fill="#8a5a2b"/></g>',
  scroll: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><path d="M6 3.8h9.4L19 7.4v12.8H6Z" fill="#ece0c4"/><path d="M15.1 3.8v3.6H19" fill="#d8d2c4"/><path d="M9 10h6M9 13h6M9 16h4" stroke="#a89880" stroke-width="1.3" stroke-linecap="round"/></g>',
  globe: '<g stroke="#241a12" stroke-width="1.6"><circle cx="12" cy="12" r="8.4" fill="#3b6fe0"/><ellipse cx="12" cy="12" rx="4.1" ry="8.4" fill="none" stroke-width="1.3"/><path d="M3.6 12h16.8" fill="none" stroke-width="1.3"/><path d="M5.2 7.6h13.6M5.2 16.4h13.6" fill="none" stroke-width="1.1"/></g>',
  cart: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M3.8 8.6h13.4l-1.7 6.2H6.2Z" fill="#8a5a2b"/><path d="M17.2 8.6l3-2.1" fill="none" stroke-width="2"/><path d="M6.6 11.6h8.2" stroke="#5a3a1a" stroke-width="1.2" fill="none"/><circle cx="8.1" cy="17.8" r="1.9" fill="#b8c4d4"/><circle cx="14.5" cy="17.8" r="1.9" fill="#b8c4d4"/></g>',
  chart: '<g stroke-linecap="round" stroke-linejoin="round"><path d="M4.5 4.5v15h15" fill="none" stroke="#241a12" stroke-width="1.9"/><path d="M8 15.5v-4M12 15.5v-7M16 15.5V7" stroke="#5ad04a" stroke-width="2.4"/></g>',
  eye: '<path d="M2.5 12S6.6 6.4 12 6.4 21.5 12 21.5 12 17.4 17.6 12 17.6 2.5 12 2.5 12Z" fill="#ece0c4" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"/><circle cx="12" cy="12" r="3.4" fill="#3b6fe0" stroke="#241a12" stroke-width="1.5"/><circle cx="12" cy="12" r="1.25" fill="#241a12"/>',
  stop: '<path d="M9.2 2.9h5.6L19.1 7.2v5.6l-4.3 4.3H9.2L4.9 12.8V7.2Z" fill="#c93a2e" stroke="#241a12" stroke-width="1.7" stroke-linejoin="round"/><path d="M8.9 10h6.2" stroke="#ece0c4" stroke-width="2.3" stroke-linecap="round"/>',
  home: '<g stroke="#241a12" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round"><path d="M4.2 11.2 12 4.3l7.8 6.9" fill="none" stroke-width="2.2"/><path d="M6.6 9.7V20h10.8V9.7" fill="#a06a35"/><path d="M10.4 20v-4.8h3.2V20Z" fill="#241a12"/></g>',
  merge: '<g stroke="#241a12" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none"><path d="M4.5 5.8c5.8 0 6.4 6.2 13 6.2"/><path d="M4.5 18.2c5.8 0 6.4-6.2 13-6.2"/><path d="M14.4 8.9 18.4 12l-4 3.1"/></g>',
  split: '<g stroke="#241a12" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none"><path d="M4 12h6.4"/><path d="M10.4 12c4.8 0 4.3-6.3 9.6-6.3"/><path d="M10.4 12c4.8 0 4.3 6.3 9.6 6.3"/><path d="M17 2.9 20.6 5.7 17 8.5M17 15.5l3.6 2.8-3.6 2.8"/></g>',
  skull: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><path d="M12 3.1a7.2 7.2 0 0 0-7.2 7.2c0 2.4 1.2 3.9 2.2 4.9v2.9a1.6 1.6 0 0 0 1.6 1.6h6.8a1.6 1.6 0 0 0 1.6-1.6v-2.9c1-1 2.2-2.5 2.2-4.9A7.2 7.2 0 0 0 12 3.1Z" fill="#ece0c4"/><circle cx="9.3" cy="11.5" r="1.35" fill="#241a12" stroke="none"/><circle cx="14.7" cy="11.5" r="1.35" fill="#241a12" stroke="none"/><path d="M10 19.7v-1.7M12 19.7v-1.7M14 19.7v-1.7" stroke-width="1.2" stroke-linecap="round"/></g>',
  flag: '<path d="M6.4 21V3.6" stroke="#241a12" stroke-width="1.9" stroke-linecap="round"/><path d="M6.4 4.4c3.5-1.6 5.4 1.3 8.9.2 2-.6 3.4-.7 3.4-.7v9.4s-1.5.1-3.5.8c-3.5 1.1-5.5-1.9-8.8-.2Z" fill="#c93a2e" stroke="#241a12" stroke-width="1.5" stroke-linejoin="round"/>',
  xbow: '<g stroke-linejoin="round" stroke-linecap="round"><path d="M3.6 7.2c5.6 1.7 11.2 1.7 16.8 0" fill="none" stroke="#8a5a2b" stroke-width="2.1"/><path d="M4.4 7v4.4M19.6 7v4.4" stroke="#241a12" stroke-width="1.3" fill="none"/><path d="M4.4 9.2h15.2" stroke="#d8d2c4" stroke-width="1.1" fill="none"/><path d="M12 9.2V21" stroke="#6b4423" stroke-width="2.4" fill="none"/><path d="M12 4.6v3" stroke="#b8c4d4" stroke-width="1.6" fill="none"/></g>',
  bolt: '<path d="M13.5 2.4 6.3 13.3h4.7l-1.5 8.3 7.7-11h-4.7Z" fill="#ffd94a" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"/>',
  spear: '<g transform="rotate(45 12 12)" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M12 8.6V22.6" stroke="#6b4423" stroke-width="2.2" fill="none"/><path d="M12 1.6 14.4 8.4H9.6Z" fill="#cdd6e2"/><path d="M9.6 8.4h4.8" fill="none"/></g>',
  militia: '<g stroke-linecap="round" stroke-linejoin="round" fill="none"><path d="M12 10.6V22" stroke="#6b4423" stroke-width="2.3"/><path d="M6.3 2.6v3.4a5.7 5.7 0 0 0 11.4 0V2.6" stroke="#241a12" stroke-width="1.9"/><path d="M12 2.6v5.4" stroke="#241a12" stroke-width="1.9"/></g>',
  star: '<path d="M12 2.6l2.9 5.9 6.5.9-4.7 4.6 1.1 6.5L12 17.4 6.2 20.5l1.1-6.5L2.6 9.4l6.5-.9Z" fill="#f2c230" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"/>',
  check: '<path d="M4.6 12.6 10 18 19.5 5.8" fill="none" stroke="#241a12" stroke-width="4.4" stroke-linecap="round" stroke-linejoin="round"/><path d="M4.6 12.6 10 18 19.5 5.8" fill="none" stroke="#5ad04a" stroke-width="2.3" stroke-linecap="round" stroke-linejoin="round"/>',
  gear: '<g fill="#a8adb8" stroke="#241a12" stroke-width="1.5" stroke-linejoin="round"><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(45 12 12)"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(90 12 12)"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(135 12 12)"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(180 12 12)"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(225 12 12)"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(270 12 12)"/><rect x="10.5" y="1.7" width="3" height="4.4" rx="0.9" transform="rotate(315 12 12)"/><circle cx="12" cy="12" r="5.5"/></g><circle cx="12" cy="12" r="2" fill="#241a12"/>',
  note: '<g stroke-linecap="round" stroke-linejoin="round"><path d="M9.6 18.4V6L19 3.6v12.6" fill="none" stroke="#241a12" stroke-width="3"/><path d="M9.6 18.4V6L19 3.6v12.6" fill="none" stroke="#ece0c4" stroke-width="1.4"/><ellipse cx="7.1" cy="18.5" rx="2.5" ry="1.9" fill="#ece0c4" stroke="#241a12" stroke-width="1.4"/><ellipse cx="16.5" cy="16.3" rx="2.5" ry="1.9" fill="#ece0c4" stroke="#241a12" stroke-width="1.4"/></g>',
  snd: '<path d="M4 9.6v4.8h3.4L13 18.8V5.2L7.4 9.6Z" fill="#ece0c4" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"/><path d="M16.3 9.2a4.3 4.3 0 0 1 0 5.6M19 6.7a8 8 0 0 1 0 10.6" fill="none" stroke="#ece0c4" stroke-width="1.7" stroke-linecap="round"/>',
  lock: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><rect x="5" y="10.4" width="14" height="9.7" rx="1.9" fill="#c9a24a"/><path d="M8.2 10.4V8a3.8 3.8 0 0 1 7.6 0v2.4" fill="none" stroke-width="2"/><circle cx="12" cy="15.2" r="1.3" fill="#241a12" stroke="none"/></g>',
  mug: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round" stroke-linecap="round"><path d="M6 8.2h10v10.7a1.9 1.9 0 0 1-1.9 1.9H7.9A1.9 1.9 0 0 1 6 18.9Z" fill="#e8a83a"/><path d="M16 10.2h2.1a2.5 2.5 0 0 1 0 5h-2.1" fill="none"/><path d="M6 8.2C5.3 5.9 7.7 5 8.7 6.4c.5-2 3.5-2.3 4.4-.6 1.5-1.1 3.7.5 3 2.4Z" fill="#f4f0e0"/><path d="M9.3 12.4v4.6M12.5 12.4v4.6" stroke="#c97f1e" stroke-width="1.3" fill="none"/></g>',
  inf: '<path d="M12 12c-1.6-3.6-6.7-3.9-6.7 0s5.1 3.6 6.7 0c1.6 3.6 6.7 3.9 6.7 0s-5.1-3.6-6.7 0Z" fill="none" stroke="#ece0c4" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"/>',
  x: '<path d="M6.2 6.2l11.6 11.6M17.8 6.2 6.2 17.8" stroke="#241a12" stroke-width="4.2" stroke-linecap="round" fill="none"/><path d="M6.2 6.2l11.6 11.6M17.8 6.2 6.2 17.8" stroke="#ece0c4" stroke-width="2.2" stroke-linecap="round" fill="none"/>',
  shield: '<path d="M12 2.4 19.6 5.8v6.5c0 4.8-3.2 8-7.6 10.3-4.4-2.3-7.6-5.5-7.6-10.3V5.8Z" fill="#c93a2e" stroke="#241a12" stroke-width="1.7" stroke-linejoin="round"/><path d="M12 2.4 19.6 5.8v6.5c0 4.8-3.2 8-7.6 10.3Z" fill="#3b6fe0" stroke="#241a12" stroke-width="1.3" stroke-linejoin="round"/><path d="M12 2.4v20.2" stroke="#241a12" stroke-width="1.2"/>',
  t_folk: '<circle cx="12" cy="7.5" r="4" fill="#eec39a" stroke="#241a12" stroke-width="1.6"/><path d="M4.4 20.4c.8-4.4 4-6.5 7.6-6.5s6.8 2.1 7.6 6.5Z" fill="#c93a2e" stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"/>',
  t_san: '<circle cx="12" cy="12" r="8.5" fill="#c93a2e" stroke="#241a12" stroke-width="1.7"/><path d="M6.1 6.1l11.8 11.8" stroke="#ece0c4" stroke-width="2.7" stroke-linecap="round"/>',
  person: '<circle cx="12" cy="5.2" r="2.5" fill="#eec39a" stroke="#241a12" stroke-width="1.5"/><path d="M9.1 8.9h5.8l.9 7h-2.1l-.4 5.2h-3l-.4-5.2H8.2Z" fill="#3b6fe0" stroke="#241a12" stroke-width="1.5" stroke-linejoin="round"/>',
  prison: '<g stroke="#241a12" stroke-width="1.6" stroke-linejoin="round"><rect x="4.4" y="4.4" width="15.2" height="15.2" rx="1.4" fill="#0d0f12"/><path d="M9.4 4.4v15.2M12 4.4v15.2M14.6 4.4v15.2" stroke="#a8adb8" stroke-width="1.6"/><rect x="4.4" y="4.4" width="15.2" height="15.2" rx="1.4" fill="none"/></g>',
  chevup: '<path d="M6 14.6 12 8.6l6 6" fill="none" stroke="#ffd94a" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>',
  chevdn: '<path d="M6 9.4 12 15.4l6-6" fill="none" stroke="#ffd94a" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>'
};
const IMG = (k, s) => { const sz = s || 24; return `<svg class="ic" viewBox="0 0 24 24" width="${sz}" height="${sz}" aria-hidden="true">${SV[k] || SV.note}</svg>`; };

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
HF.cannon = function (c, x, y, col) { // пороховая пушка: бронзовый ствол на лафете с колёсами
  R_(c, x + 4, y + 11, 3, 4, '#5a3a1a'); R_(c, x + 13, y + 11, 3, 4, '#5a3a1a'); // стойки
  R_(c, x + 2, y + 9, 3, 3, '#3a2a16'); R_(c, x + 15, y + 9, 3, 3, '#3a2a16'); // колёса
  R_(c, x + 3, y + 10, 1, 1, '#c8a45a'); R_(c, x + 16, y + 10, 1, 1, '#c8a45a');
  R_(c, x + 1, y + 6, 18, 4, '#7a5a2e'); R_(c, x + 1, y + 6, 18, 1, '#9a7a44'); // лафет
  R_(c, x + 5, y + 3, 13, 4, '#b8862e'); R_(c, x + 5, y + 3, 13, 1, '#e8c465'); // бронзовый ствол
  R_(c, x + 4, y + 2, 3, 6, '#8a6420'); R_(c, x + 17, y + 4, 3, 3, '#e8c465'); // казённик и дуло
  R_(c, x + 19, y + 5, 1, 1, '#231a12');
  R_(c, x + 8, y + 0, 2, 3, '#4a4a52'); // затравка
};
HF.ram = function (c, x, y, col) {
  R_(c, x + 3, y + 10, 4, 4, '#2a1a0c'); R_(c, x + 4, y + 11, 2, 2, '#6a4a2a'); R_(c, x + 14, y + 10, 4, 4, '#2a1a0c'); R_(c, x + 15, y + 11, 2, 2, '#6a4a2a');
  R_(c, x + 3, y + 4, 1, 7, '#5a3a1a'); R_(c, x + 17, y + 4, 1, 7, '#5a3a1a'); R_(c, x + 1, y + 1, 19, 4, col); R_(c, x + 1, y + 1, 19, 1, shadeC(col, 1.35)); R_(c, x + 1, y + 4, 19, 1, shadeC(col, 0.6)); for (let i = 3; i < 19; i += 3) R_(c, x + i, y + 2, 1, 2, shadeC(col, 0.75));
  R_(c, x + 6, y + 5, 1, 3, '#c9b48a'); R_(c, x + 13, y + 5, 1, 3, '#c9b48a'); R_(c, x + 0, y + 7, 20, 3, '#8a5a2b'); R_(c, x + 0, y + 7, 20, 1, '#a8743a'); R_(c, x + 18, y + 6, 3, 5, '#7e838d'); R_(c, x + 18, y + 6, 3, 1, '#b8c0cc'); R_(c, x + 20, y + 7, 1, 3, '#4a4e58');
};
function shipSpriteCanvas(kind, col) { // 36 x 24
  const c = document.createElement('canvas'); c.width = 36; c.height = 24; const x = c.getContext('2d');
  const hull = kind === 'liner' ? '#2c2422' : kind === 'war' ? '#3a2a20' : '#8a5a2b', hl = kind === 'liner' ? '#4a3c34' : kind === 'war' ? '#5a4030' : '#b07840';
  R_(x, 3, 17, 30, 3, hull); R_(x, 3, 17, 30, 1, hl); R_(x, 5, 20, 26, 2, shadeC(hull, 0.7)); R_(x, 2, 15, 3, 3, hull); R_(x, 31, 15, 3, 3, hull); R_(x, 33, 14, 2, 2, hl);
  for (let i = 0; i < 6; i++) R_(x, 6 + i * 5, 18, 1, 2, shadeC(hull, 0.6));
  R_(x, 17, 1, 1, 17, '#5a3a1a'); R_(x, 9, 6, 1, 11, '#5a3a1a');
  if (kind === 'liner') { // линкор: три мачты, тяжёлый корпус, пороховые пушки за бортом
    R_(x, 2, 16, 32, 5, hull); R_(x, 2, 16, 32, 1, hl); R_(x, 4, 21, 28, 2, shadeC(hull, 0.65)); // корпус потяжелее
    R_(x, 1, 13, 4, 4, hull); R_(x, 31, 13, 4, 4, hull); R_(x, 34, 12, 2, 2, hl);
    R_(x, 17, 0, 1, 17, '#4a3018'); R_(x, 9, 3, 1, 14, '#4a3018'); R_(x, 25, 3, 1, 14, '#4a3018'); // три мачты
    R_(x, 10, 4, 7, 9, col); R_(x, 10, 4, 7, 1, shadeC(col, 1.35)); R_(x, 18, 2, 6, 11, col); R_(x, 18, 2, 6, 1, shadeC(col, 1.35));
    R_(x, 2, 7, 6, 9, col); R_(x, 2, 7, 6, 1, shadeC(col, 1.35));
    R_(x, 19, 6, 3, 3, '#f2c230'); R_(x, 20, 5, 1, 5, '#f2c230'); // герб
    for (let i = 0; i < 7; i++) R_(x, 4 + i * 4, 18, 2, 1, '#14100c'); // порты пушек
    for (let i = 0; i < 7; i++) R_(x, 4 + i * 4, 15, 1, 1, '#8a8f98'); // стволы пушек
    R_(x, 14, 0, 10, 2, col); R_(x, 15, 1, 8, 1, shadeC(col, 1.3));
  } else if (kind === 'war') {
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

/* ===== Новое в версии 2.0: мельница, таверна, копейщики, ополчение ===== */
// лопасти мельницы: hx, hy — ось; u — размер «мелкого пикселя»
function millBlades(c, hx, hy, ang, u) {
  for (let k = 0; k < 4; k++) {
    const a = ang + k * Math.PI / 2, cs = Math.cos(a), sn = Math.sin(a), px = -sn, py = cs;
    for (let i = 2; i <= 11; i++) {
      const x = hx + cs * i * u, y = hy + sn * i * u;
      R_(c, x - u / 2, y - u / 2, u, u, '#6a4a2a');
      if (i >= 4) { R_(c, x + px * u - u / 2, y + py * u - u / 2, u, u, '#efe6cc'); R_(c, x + px * 2 * u - u / 2, y + py * 2 * u - u / 2, u, u, i % 2 ? '#d8cca8' : '#efe6cc'); }
    }
  }
  R_(c, hx - u, hy - u, 2 * u, 2 * u, '#3a2410');
}
DRAW.mill = function (c, x, y, col) {
  rrect(c, x + 4, y + 27, 24, 5, '#8a8f78', 2);
  bricks(c, x + 9, y + 21, 14, 8, '#b8b0a0');
  planks(c, x + 10, y + 12, 12, 10, '#a8743a'); R_(c, x + 10, y + 12, 1, 10, '#c08a50'); R_(c, x + 21, y + 12, 1, 10, '#7a4e26');
  gable(c, x + 8, y + 5, 16, 8, col); R_(c, x + 8, y + 12, 16, 1, '#3a2410');
  R_(c, x + 14, y + 23, 4, 6, '#3a2410'); R_(c, x + 14, y + 23, 4, 1, '#6a4a2a'); win(c, x + 14, y + 15, 4, 4);
  R_(c, x + 4, y + 25, 4, 3, '#e8dcc0'); R_(c, x + 4, y + 25, 4, 1, '#fffaf0'); R_(c, x + 5, y + 24, 2, 1, '#c8b890'); R_(c, x + 24, y + 26, 4, 2, '#e8dcc0'); R_(c, x + 24, y + 26, 4, 1, '#fffaf0');
};
DRAW.tavern = function (c, x, y, col) {
  rrect(c, x + 1, y + 27, 30, 5, '#8a7a50', 2);
  R_(c, x + 3, y + 14, 22, 14, '#e8dcc0'); for (const i of [0, 7, 14, 21]) R_(c, x + 3 + i, y + 14, 1, 14, '#5a3a1a'); R_(c, x + 3, y + 14, 22, 1, '#5a3a1a'); R_(c, x + 3, y + 21, 22, 1, '#5a3a1a'); R_(c, x + 3, y + 27, 22, 1, '#3a2410');
  for (let i = 0; i < 6; i++) { R_(c, x + 4 + i, y + 15 + i, 1, 1, '#5a3a1a'); R_(c, x + 23 - i, y + 15 + i, 1, 1, '#5a3a1a'); }
  tiles(c, x + 1, y + 7, 26, 8, '#b8944a'); gable(c, x + 5, y + 2, 18, 6, '#b8944a'); R_(c, x + 1, y + 13, 26, 2, col);
  bricks(c, x + 19, y + 1, 4, 8, '#8a7a70'); R_(c, x + 18, y + 1, 6, 1, '#4a4040');
  R_(c, x + 11, y + 20, 6, 8, '#6a4a2a'); R_(c, x + 11, y + 20, 6, 1, '#8a6a3a'); R_(c, x + 16, y + 24, 1, 1, '#f2c230'); win(c, x + 5, y + 16, 4, 4); win(c, x + 19, y + 16, 4, 4);
  R_(c, x + 25, y + 12, 5, 1, '#3a2410'); R_(c, x + 29, y + 12, 1, 4, '#3a2410'); R_(c, x + 26, y + 15, 5, 5, '#6a4a2a'); R_(c, x + 26, y + 15, 5, 1, '#8a6a3a'); R_(c, x + 27, y + 16, 3, 3, '#f2c230'); R_(c, x + 27, y + 16, 3, 1, '#fff8d8');
  R_(c, x + 26, y + 23, 4, 5, '#8a5a2b'); R_(c, x + 26, y + 23, 4, 1, '#a8743a'); R_(c, x + 26, y + 25, 4, 1, '#3a2a18'); R_(c, x + 1, y + 24, 3, 4, '#8a5a2b'); R_(c, x + 1, y + 24, 3, 1, '#a8743a');
};
DRAW.house = function (c, x, y, col) {
  rrect(c, x + 2, y + 27, 28, 5, '#8a8f78', 2);
  // сруб
  planks(c, x + 5, y + 14, 22, 14, '#a8743a'); R_(c, x + 5, y + 14, 22, 1, '#c08a50'); R_(c, x + 5, y + 27, 22, 1, '#6a4826');
  // крыша
  gable(c, x + 2, y + 3, 28, 12, col); R_(c, x + 2, y + 13, 28, 2, '#5a3a1a');
  for (let j = 0; j < 11; j += 2) { const ww = Math.max(3, Math.round(28 * (j + 1) / 12)), xx = x + 2 + Math.floor((28 - ww) / 2); R_(c, xx, y + 3 + j, ww, 1, shadeC(col, 0.72)); }
  // дверь и окна
  R_(c, x + 13, y + 20, 7, 8, '#5a3a1a'); R_(c, x + 13, y + 20, 7, 1, '#8a5a30'); R_(c, x + 18, y + 24, 1, 1, '#f2c230');
  win(c, x + 7, y + 18, 5, 5); win(c, x + 21, y + 18, 5, 5);
  // печная труба с дымком
  bricks(c, x + 22, y + 2, 4, 7, '#8a7a70'); R_(c, x + 21, y + 2, 6, 1, '#4a4040');
  // забор и поленница
  for (let i = 0; i < 5; i++) R_(c, x + 1 + i * 3, y + 30, 1, 2, '#6a4826'); R_(c, x + 1, y + 30, 13, 1, '#8a6a3a');
  R_(c, x + 25, y + 24, 4, 3, '#8a5a2b'); R_(c, x + 25, y + 24, 4, 1, '#a8743a'); R_(c, x + 26, y + 22, 2, 2, '#c08a50');
};
DRAW.prison = function (c, x, y, col) {
  rrect(c, x + 2, y + 27, 28, 5, '#8a8f78', 2);
  bricks(c, x + 3, y + 10, 26, 18, '#8a8478');
  R_(c, x + 3, y + 9, 26, 2, '#6a655c');
  gable(c, x + 1, y + 3, 30, 7, col); R_(c, x + 1, y + 9, 30, 1, '#4a4038');
  for (const wx of [6, 14, 22]) { R_(c, x + wx, y + 13, 6, 7, '#14100c'); for (let i = 0; i < 6; i += 2) R_(c, x + wx + i, y + 13, 1, 7, '#b8c4d4'); }
  R_(c, x + 12, y + 20, 8, 8, '#3a2410'); R_(c, x + 12, y + 20, 8, 1, '#8a8f98'); R_(c, x + 16, y + 23, 1, 2, '#f2c230');
  flag(c, x + 15, y - 1, col);
};
DRAW.storehouse = function (c, x, y, col) { // склад: сруб с большими воротами и ящиками
  rrect(c, x + 2, y + 27, 28, 5, '#8a8f78', 2);
  planks(c, x + 4, y + 13, 24, 15, '#8a5a2b');
  gable(c, x + 2, y + 2, 28, 12, col);
  R_(c, x + 2, y + 12, 28, 2, '#3a2410');
  R_(c, x + 10, y + 18, 12, 10, '#4a3018'); R_(c, x + 10, y + 18, 12, 1, '#6a4a2a'); R_(c, x + 15, y + 18, 1, 10, '#2a1a0c');
  R_(c, x + 3, y + 23, 5, 5, '#a8743a'); R_(c, x + 3, y + 23, 5, 1, '#c08a50'); R_(c, x + 24, y + 23, 5, 5, '#a8743a'); R_(c, x + 24, y + 23, 5, 1, '#c08a50');
};
DRAW.granary = function (c, x, y, col) { // амбар: глиняный, соломенная крыша
  rrect(c, x + 2, y + 27, 28, 5, '#8a8f78', 2);
  bricks(c, x + 7, y + 12, 18, 16, '#c9a86a');
  gable(c, x + 5, y + 4, 22, 9, '#b8944a');
  R_(c, x + 5, y + 11, 22, 2, col);
  R_(c, x + 13, y + 20, 6, 8, '#5a3a1a'); R_(c, x + 13, y + 20, 6, 1, '#8a5a30');
  win(c, x + 9, y + 15, 4, 3); win(c, x + 19, y + 15, 4, 3);
};
DRAW.townhall = function (c, x, y, col) { // ратуша: белокаменная, башня с флагом
  rrect(c, x + 1, y + 27, 30, 5, '#8a8f78', 2);
  bricks(c, x + 5, y + 14, 22, 14, '#b8b0a0');
  bricks(c, x + 12, y + 4, 8, 11, '#a89f8f');
  gable(c, x + 10, y + 1, 12, 5, col);
  flag(c, x + 15, y + 0, col);
  R_(c, x + 14, y + 19, 4, 9, '#3a2410'); win(c, x + 8, y + 17, 4, 4); win(c, x + 20, y + 17, 4, 4); win(c, x + 14, y + 8, 4, 4);
};
DRAW.embassy = function (c, x, y, col) { // посольство: штукатурный фасад, колонны
  rrect(c, x + 2, y + 27, 28, 5, '#8a8f78', 2);
  R_(c, x + 4, y + 15, 24, 13, '#e8dcc0');
  tiles(c, x + 2, y + 8, 28, 8, '#7a5a8a');
  R_(c, x + 2, y + 14, 28, 2, col);
  R_(c, x + 4, y + 15, 2, 13, '#c9bfa4'); R_(c, x + 26, y + 15, 2, 13, '#c9bfa4');
  R_(c, x + 13, y + 19, 6, 9, '#3a2410'); R_(c, x + 13, y + 19, 6, 1, '#5a3a1a');
  win(c, x + 7, y + 17, 4, 4); win(c, x + 21, y + 17, 4, 4);
  R_(c, x + 11, y + 17, 1, 9, '#f4f0e0'); R_(c, x + 20, y + 17, 1, 9, '#f4f0e0');
  flag(c, x + 15, y + 2, col);
};
HF.spear = function (c, x, y, col) {
  R_(c, x + 4, y + 10, 2, 3, '#5a4a3a'); R_(c, x + 7, y + 10, 2, 3, '#5a4a3a'); R_(c, x + 3, y + 12, 3, 1, '#2a1808'); R_(c, x + 7, y + 12, 3, 1, '#2a1808');
  R_(c, x + 4, y + 5, 6, 5, '#8a6a3a'); R_(c, x + 4, y + 5, 6, 1, '#a8844a'); R_(c, x + 6, y + 5, 2, 5, col); R_(c, x + 4, y + 8, 6, 1, '#3a2410'); R_(c, x + 4, y + 9, 6, 1, '#6a4e2a');
  R_(c, x + 4, y + 1, 5, 4, SKIN); R_(c, x + 3, y, 7, 1, '#b8c4d4'); R_(c, x + 3, y + 1, 7, 1, '#6a707c'); R_(c, x + 7, y + 3, 1, 1, '#14100c'); R_(c, x + 4, y + 4, 4, 1, '#c9a47a');
  R_(c, x + 1, y + 5, 3, 5, col); R_(c, x + 1, y + 5, 3, 1, shadeC(col, 1.3)); R_(c, x + 1, y + 9, 3, 1, shadeC(col, 0.65)); R_(c, x + 2, y + 7, 1, 1, '#f2c230');
  R_(c, x + 10, y - 1, 1, 13, '#a8743a'); R_(c, x + 10, y - 1, 1, 2, '#eef2f8'); R_(c, x + 11, y, 1, 1, '#9aa4b4'); R_(c, x + 9, y + 5, 2, 2, SKIN);
};
HF.militia = function (c, x, y, col) {
  R_(c, x + 4, y + 10, 2, 3, '#6a4e2a'); R_(c, x + 7, y + 10, 2, 3, '#6a4e2a'); R_(c, x + 3, y + 12, 3, 1, '#2a1808'); R_(c, x + 7, y + 12, 3, 1, '#2a1808');
  R_(c, x + 3, y + 5, 7, 5, '#b89a6a'); R_(c, x + 3, y + 5, 7, 1, '#d0b488'); R_(c, x + 3, y + 8, 7, 1, '#5a3a1a'); R_(c, x + 3, y + 9, 7, 1, '#9a7e52'); R_(c, x + 5, y + 6, 3, 2, col);
  R_(c, x + 4, y + 1, 5, 4, SKIN); R_(c, x + 4, y, 5, 1, '#6a4a2a'); R_(c, x + 3, y + 1, 1, 2, '#6a4a2a'); R_(c, x + 7, y + 3, 1, 1, '#14100c'); R_(c, x + 4, y + 4, 4, 1, '#c9a47a');
  R_(c, x + 10, y + 1, 1, 11, '#8a5a2b'); R_(c, x + 9, y - 1, 1, 3, '#b8c4d4'); R_(c, x + 10, y, 1, 1, '#b8c4d4'); R_(c, x + 11, y - 1, 1, 3, '#b8c4d4'); R_(c, x + 9, y + 5, 2, 2, SKIN);
};
// житель: позы — i (стоит), w0/w1 (шаг), k0/k1 (работа), t (беседа)
HF.xbow = function (c, x, y, col) { // арбалетчик: кольчуга, капюшон, стальной арбалет
  R_(c, x + 4, y + 10, 2, 3, '#3a2a18'); R_(c, x + 7, y + 10, 2, 3, '#3a2a18'); R_(c, x + 3, y + 12, 3, 1, '#241608'); R_(c, x + 7, y + 12, 3, 1, '#241608');
  R_(c, x + 2, y + 4, 2, 6, '#5a4a3a'); R_(c, x + 2, y + 3, 1, 1, '#cfd6de'); // колчан с болтами
  R_(c, x + 3, y + 5, 7, 5, '#5a626e'); R_(c, x + 3, y + 5, 7, 1, '#7a8494'); R_(c, x + 3, y + 8, 7, 1, '#2a241c'); R_(c, x + 3, y + 9, 7, 1, '#3e444e'); // кольчуга
  R_(c, x + 4, y + 1, 5, 4, SKIN); R_(c, x + 3, y, 7, 2, '#4a4038'); R_(c, x + 3, y + 2, 1, 3, '#4a4038'); R_(c, x + 7, y + 3, 1, 1, '#14100c'); R_(c, x + 4, y + 4, 4, 1, '#c9a47a'); // голова в капюшоне
  R_(c, x + 9, y + 6, 4, 1, '#6a4a2a'); // приклад
  R_(c, x + 11, y + 3, 1, 7, '#8a8f98'); R_(c, x + 12, y + 2, 1, 1, '#c8cdd4'); R_(c, x + 12, y + 10, 1, 1, '#c8cdd4'); R_(c, x + 10, y + 6, 2, 1, '#e8e8e8'); // дуга и болт
};
HF.folk = function (c, x, y, col, cls) {
  cls = cls || 'i';
  const walk = cls[0] === 'w', work = cls[0] === 'k', talk = cls === 't', dance = cls[0] === 'd';
  const sw = walk ? (cls === 'w0' ? 1 : -1) : 0;
  const hair = ['#6a4a2a', '#3a2a1a', '#8a6a3a', '#5a3a2a'][(x + y + col.length) & 3];
  R_(c, x + 4 + sw, y + 10, 2, 3, '#5a3a1a'); R_(c, x + 7 - sw, y + 10, 2, 3, '#4a2e14'); // ноги вразнобой
  R_(c, x + 3 + sw, y + 12, 3, 1, '#2a1808'); R_(c, x + 7 - sw, y + 12, 3, 1, '#2a1808');
  R_(c, x + 3, y + 5, 7, 5, col); R_(c, x + 3, y + 5, 7, 1, shadeC(col, 1.28)); // рубаха
  R_(c, x + 3, y + 8, 7, 1, shadeC(col, 0.72)); R_(c, x + 6, y + 5, 1, 5, shadeC(col, 0.88));
  if (dance) {
    R_(c, x + 2, y + 2 - (cls === 'd0' ? 1.5 : 0), 1.6, 3.4, SKIN); R_(c, x + 9.6, y + 2 - (cls === 'd1' ? 1.5 : 0), 1.6, 3.4, SKIN); // руки вверх — танец
    R_(c, x + 1, y + 0.6 - (cls === 'd0' ? 1.5 : 0), 1.6, 1.6, SKIN); R_(c, x + 9.6, y + 0.6 - (cls === 'd1' ? 1.5 : 0), 1.6, 1.6, SKIN);
  } else if (work) {
    R_(c, x + 10, y + 4 - (cls === 'k0' ? 2 : 0), 2, 3, SKIN); // рука с молотком
    R_(c, x + 11, y + 2 - (cls === 'k0' ? 2 : 0), 2, 2, '#7a4a1a');
    R_(c, x + 2, y + 6, 1, 3, shadeC(col, 0.85));
  } else if (talk) {
    R_(c, x + 10, y + 5, 2, 3, SKIN); R_(c, x + 11, y + 4, 1, 1, SKIN); // жестикулирует
    R_(c, x + 2, y + 6, 1, 3, shadeC(col, 0.85));
  } else {
    R_(c, x + 2, y + 6 + sw, 1, 3, shadeC(col, 0.85)); R_(c, x + 10, y + 6 - sw, 1, 3, shadeC(col, 0.85)); // руки
  }
  R_(c, x + 4, y + 1, 5, 4, SKIN); R_(c, x + 4, y, 5, 2, hair); // голова
  R_(c, x + 3, y + 1, 1, 2, hair); R_(c, x + 7, y + 3, 1, 1, '#14100c'); R_(c, x + 4, y + 4, 4, 1, '#c9a47a');
  if (talk) R_(c, x + 6, y + 4, 1, 1, '#7a3020'); // рот открыт
};
