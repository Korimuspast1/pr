/* Cozy Pixel Zombie Town — data-driven config (ScriptableObject equivalents) */
'use strict';
var ZT = window.ZT = window.ZT || {};

ZT.TILE = 32;            // px per tile (16px pixel-art scaled x2)
ZT.MAP_W = 56; ZT.MAP_H = 36;
ZT.SAVE_VERSION = 1;

/* Resources */
ZT.RES = {
  wood:    { icon:'🪵', name:{ru:'Дерево', en:'Wood'} },
  stone:   { icon:'🪨', name:{ru:'Камень', en:'Stone'} },
  food:    { icon:'🍞', name:{ru:'Еда', en:'Food'} },
  water:   { icon:'💧', name:{ru:'Вода', en:'Water'} },
  coins:   { icon:'🪙', name:{ru:'Монеты', en:'Coins'} },
  medicine:{ icon:'💊', name:{ru:'Лекарства', en:'Medicine'} },
  ammo:    { icon:'🔫', name:{ru:'Патроны', en:'Ammo'} },
  scrap:   { icon:'🔩', name:{ru:'Металлолом', en:'Scrap'} },
};
ZT.RES_ORDER = ['wood','stone','food','water','coins','medicine','ammo','scrap'];
ZT.BASE_CAP = { wood:200, stone:200, food:150, water:150, coins:999, medicine:50, ammo:200, scrap:100 };

/* Building definitions — data-driven (like ScriptableObjects).
   cat: housing/food/water/storage/production/medical/defense/decor/road/special
   cost: {res:amount}, hp, buildTime (sec of builder work), workers, capacity (beds/storage),
   produces: {res: perTick}, consumes, attract (migration), noise, upgrades: nextId */
ZT.BUILDINGS = {
  center:   { icon:'🏛️', name:{ru:'Центр города',en:'Town Center'}, cat:'special', w:3,h:3, cost:{}, hp:800, buildTime:0, workers:0, capacity:4, attract:10, desc:{ru:'Сердце города. Люди собираются здесь.',en:'Heart of the town.'} },
  house:    { icon:'🏠', name:{ru:'Домик',en:'House'}, cat:'housing', w:1,h:1, cost:{wood:10}, hp:120, buildTime:8, workers:0, capacity:3, attract:3, upgradesTo:'apartment', desc:{ru:'Жильё для 3 человек. Растёт в многоэтажку!',en:'Homes 3 citizens. Grows into an apartment!'} },
  apartment:{ icon:'🏢', name:{ru:'Многоэтажка',en:'Apartment'}, cat:'housing', w:1,h:1, cost:{wood:15,stone:15}, hp:260, buildTime:16, workers:0, capacity:7, attract:6, upgradesTo:'skyscraper', desc:{ru:'Жильё для 7 человек.',en:'Homes 7 citizens.'} },
  skyscraper:{icon:'🌆', name:{ru:'Небоскрёб',en:'Skyscraper'}, cat:'housing', w:1,h:1, cost:{stone:25,scrap:10}, hp:500, buildTime:28, workers:0, capacity:14, attract:10, desc:{ru:'Жильё для 14 человек. Символ процветания!',en:'Homes 14 citizens. A symbol of prosperity!'} },
  farm:     { icon:'🌾', name:{ru:'Ферма',en:'Farm'}, cat:'food', w:2,h:2, cost:{wood:8}, hp:100, buildTime:10, workers:2, produces:{food:1}, desc:{ru:'Фермеры выращивают еду.',en:'Farmers grow food.'} },
  kitchen:  { icon:'🍲', name:{ru:'Кухня',en:'Kitchen'}, cat:'food', w:1,h:1, cost:{wood:10,stone:5}, hp:120, buildTime:12, workers:1, consumes:{food:1}, produces:{coins:1}, attract:2, desc:{ru:'Готовит еду, повышает счастье.',en:'Cooks meals, boosts happiness.'} },
  well:     { icon:'🪣', name:{ru:'Колодец',en:'Well'}, cat:'water', w:1,h:1, cost:{stone:8}, hp:100, buildTime:8, workers:1, produces:{water:1}, desc:{ru:'Даёт воду для питья.',en:'Provides drinking water.'} },
  storage:  { icon:'📦', name:{ru:'Склад',en:'Storage'}, cat:'storage', w:2,h:1, cost:{wood:12}, hp:140, buildTime:10, workers:0, storageBonus:{wood:100,stone:100,food:80,water:80,scrap:50}, desc:{ru:'Увеличивает лимиты ресурсов.',en:'Increases resource caps.'} },
  workshop: { icon:'⚒️', name:{ru:'Мастерская',en:'Workshop'}, cat:'production', w:2,h:1, cost:{wood:15,stone:10}, hp:150, buildTime:14, workers:2, consumes:{wood:1,scrap:1}, produces:{medicine:1,ammo:2}, desc:{ru:'Делает лекарства и патроны.',en:'Crafts medicine and ammo.'} },
  clinic:   { icon:'🏥', name:{ru:'Клиника',en:'Clinic'}, cat:'medical', w:2,h:1, cost:{wood:12,stone:12}, hp:160, buildTime:14, workers:1, desc:{ru:'Лечит раненых и заражённых.',en:'Heals wounded and infected.'} },
  cemetery: { icon:'🪦', name:{ru:'Кладбище',en:'Cemetery'}, cat:'special', w:2,h:2, cost:{stone:10}, hp:80, buildTime:6, workers:0, attract:-2, desc:{ru:'Похороненные не тяготят город.',en:'Buried citizens no longer haunt the town.'} },
  safehouse:{ icon:'🛡️', name:{ru:'Убежище',en:'Safehouse'}, cat:'defense', w:2,h:2, cost:{wood:20,stone:10}, hp:400, buildTime:16, workers:0, capacity:10, desc:{ru:'Сюда бегут люди при тревоге.',en:'Citizens flee here during alarms.'} },
  wall:     { icon:'🧱', name:{ru:'Стена',en:'Wall'}, cat:'defense', w:1,h:1, cost:{wood:4}, hp:300, buildTime:4, workers:0, desc:{ru:'Блокирует зомби. Прочная!',en:'Blocks zombies. Sturdy!'} },
  stonewall:{ icon:'🗿', name:{ru:'Каменная стена',en:'Stone Wall'}, cat:'defense', w:1,h:1, cost:{stone:6}, hp:600, buildTime:6, workers:0, desc:{ru:'Очень прочная стена.',en:'Very strong wall.'} },
  gate:     { icon:'🚪', name:{ru:'Ворота',en:'Gate'}, cat:'defense', w:1,h:1, cost:{wood:8}, hp:250, buildTime:5, workers:0, gate:true, desc:{ru:'Люди проходят, зомби ломают.',en:'Citizens pass, zombies break it.'} },
  tower:    { icon:'🗼', name:{ru:'Сторожевая башня',en:'Watchtower'}, cat:'defense', w:1,h:1, cost:{wood:18}, hp:200, buildTime:12, workers:1, desc:{ru:'Охранник стреляет по зомби.',en:'A guard shoots zombies.'} },
  turret:   { icon:'🔫', name:{ru:'Турель',en:'Turret'}, cat:'defense', w:1,h:1, cost:{scrap:15,stone:10}, hp:220, buildTime:14, workers:0, desc:{ru:'Автоматически стреляет. Тратит патроны.',en:'Auto-shoots. Uses ammo.'} },
  spikes:   { icon:'⚔️', name:{ru:'Шипы',en:'Spike Trap'}, cat:'defense', w:1,h:1, cost:{wood:6,scrap:4}, hp:60, buildTime:4, workers:0, desc:{ru:'Ранит зомби, проходящих мимо.',en:'Wounds passing zombies.'} },
  lamp:     { icon:'💡', name:{ru:'Фонарь',en:'Lamp'}, cat:'decor', w:1,h:1, cost:{wood:2,scrap:1}, hp:40, buildTime:3, workers:0, attract:2, light:true, desc:{ru:'Светит ночью, +счастье.',en:'Lights the night, +happiness.'} },
  bench:    { icon:'🪑', name:{ru:'Скамейка',en:'Bench'}, cat:'decor', w:1,h:1, cost:{wood:3}, hp:40, buildTime:3, workers:0, attract:2, desc:{ru:'Место для отдыха.',en:'A place to rest.'} },
  flowers:  { icon:'🌸', name:{ru:'Клумба',en:'Flower Bed'}, cat:'decor', w:1,h:1, cost:{}, hp:20, buildTime:2, workers:0, attract:3, desc:{ru:'Красиво и уютно!',en:'Pretty and cozy!'} },
  road:     { icon:'🛣️', name:{ru:'Дорога',en:'Road'}, cat:'road', w:1,h:1, cost:{stone:1}, hp:9999, buildTime:1, workers:0, attract:1, desc:{ru:'Люди ходят быстрее.',en:'Citizens walk faster.'} },
};
ZT.CATS = [
  {id:'housing', icon:'🏠', name:{ru:'Жильё',en:'Housing'}},
  {id:'food', icon:'🌾', name:{ru:'Еда и вода',en:'Food & Water'}},
  {id:'production', icon:'⚒️', name:{ru:'Производство',en:'Production'}},
  {id:'defense', icon:'🛡️', name:{ru:'Защита',en:'Defense'}},
  {id:'decor', icon:'🌸', name:{ru:'Декор',en:'Decor'}},
  {id:'road', icon:'🛣️', name:{ru:'Дороги',en:'Roads'}},
  {id:'special', icon:'🏛️', name:{ru:'Прочее',en:'Other'}},
];

/* Jobs */
ZT.JOBS = {
  idle:     { icon:'💤', name:{ru:'Безработный',en:'Idle'} },
  builder:  { icon:'🏗️', name:{ru:'Строитель',en:'Builder'} },
  farmer:   { icon:'🌾', name:{ru:'Фермер',en:'Farmer'} },
  cook:     { icon:'🍲', name:{ru:'Повар',en:'Cook'} },
  carrier:  { icon:'🪣', name:{ru:'Водонос',en:'Water Carrier'} },
  worker:   { icon:'⚒️', name:{ru:'Рабочий',en:'Worker'} },
  medic:    { icon:'🏥', name:{ru:'Медик',en:'Medic'} },
  guard:    { icon:'🛡️', name:{ru:'Охранник',en:'Guard'} },
  woodcutter:{icon:'🪓', name:{ru:'Дровосек',en:'Woodcutter'} },
  miner:    { icon:'⛏️', name:{ru:'Шахтёр',en:'Miner'} },
};

/* Zombie types */
ZT.ZOMBIES = {
  walker:  { icon:'🧟', name:{ru:'Ходячий',en:'Walker'}, hp:60, speed:22, dmg:6, range:14, attackRate:1.0, xp:1, color:'#7cb342' },
  runner:  { icon:'🏃', name:{ru:'Бегун',en:'Runner'}, hp:35, speed:52, dmg:5, range:14, attackRate:0.7, xp:2, color:'#c0ca33' },
  brute:   { icon:'🦍', name:{ru:'Громила',en:'Brute'}, hp:220, speed:16, dmg:16, range:16, attackRate:1.4, xp:5, color:'#6d4c41' },
  spitter: { icon:'🤢', name:{ru:'Плевака',en:'Spitter'}, hp:45, speed:20, dmg:4, range:70, attackRate:1.6, xp:3, color:'#26a69a', ranged:true },
  screamer:{ icon:'😱', name:{ru:'Крикун',en:'Screamer'}, hp:50, speed:26, dmg:5, range:14, attackRate:1.0, xp:3, color:'#ab47bc' },
};

/* Names */
ZT.NAMES_RU = ['Иван','Мария','Пётр','Анна','Сергей','Ольга','Дмитрий','Елена','Алексей','Наталья','Михаил','Татьяна','Андрей','Юлия','Николай','Дарья','Владимир','Ксения','Павел','Алина','Григорий','София','Роман','Полина','Виктор','Анастасия','Евгений','Валерия','Олег','Милана','Тимур','Агата'];
ZT.NAMES_EN = ['Jack','Emma','Liam','Olivia','Noah','Ava','Ethan','Mia','Lucas','Zoe','Mason','Lily','James','Ruby','Henry','Ivy','Leo','Nora','Oscar','Ella','Max','Freya','Sam','Ada','Tom','Mila','Ben','Luna','Adam','Eva'];
ZT.SURNAMES = ['Иванов','Петрова','Сидоров','Кузнецова','Смирнов','Попова','Васильев','Соколова','Михайлов','Фёдорова','Smith','Brown','Taylor','Wilson','Davies','Evans','Thomas','Roberts','Walker','Wright'];

/* i18n */
ZT.I18N = {
  ru: { build:'Стройка', people:'Люди', zombies:'Зомби', alarm:'Тревога', menu:'Меню', place:'Поставить', cancel:'Отмена',
    'zombie-hint':'Спавнь зомби в любой момент! Они пойдут к городу.', intensity:'Интенсивность', spawnpoint:'Точка спавна',
    edge:'Край карты', forest:'Лес', nest:'Гнездо зомби', outbreak:'НАЧАТЬ ВСПЫШКУ', calm:'Успокоить город',
    newgame:'Новая игра', continue:'Продолжить', howto:'Как играть', settings:'Настройки', tagline:'Город сам заселяется и растёт — от домиков до небоскрёбов. А зомби спавнишь ты, когда захочешь!',
    sound:'Звук', music:'Музыка', vibration:'Вибрация', particles:'Частицы', quality:'Качество (лимит жителей)', language:'Язык / Language', difficulty:'Сложность вспышки',
    paused:'Пауза', resume:'Продолжить', save:'Сохранить', load:'Загрузить', tomenu:'В меню' },
  en: { build:'Build', people:'People', zombies:'Zombies', alarm:'Alarm', menu:'Menu', place:'Place', cancel:'Cancel',
    'zombie-hint':'Spawn zombies anytime! They will march to town.', intensity:'Intensity', spawnpoint:'Spawn point',
    edge:'Map edge', forest:'Forest', nest:'Zombie nest', outbreak:'START OUTBREAK', calm:'Calm the town',
    newgame:'New Game', continue:'Continue', howto:'How to Play', settings:'Settings', tagline:'The town settles and grows by itself — from cottages to skyscrapers. And you spawn the zombies whenever you want!',
    sound:'Sound', music:'Music', vibration:'Vibration', particles:'Particles', quality:'Quality (citizen cap)', language:'Язык / Language', difficulty:'Outbreak difficulty',
    paused:'Paused', resume:'Resume', save:'Save', load:'Load', tomenu:'Main Menu' },
};
ZT.t = function(key){ const lang = (ZT.settings && ZT.settings.lang) || 'ru'; return (ZT.I18N[lang] && ZT.I18N[lang][key]) || ZT.I18N.ru[key] || key; };
ZT.L = function(obj){ if(!obj) return ''; const lang = (ZT.settings && ZT.settings.lang) || 'ru'; return obj[lang] || obj.ru || obj.en || ''; };

/* Biomes — every new game picks one, so the world always looks different */
ZT.BIOMES = {
  summer: { name:{ru:'Лето',en:'Summer'}, grass:'#62a84e', grassD:'#52923f', grassL:'#72b85e',
    dirt:'#a07c4e', dirtD:'#8a6a3f', water:'#3d7dc2', waterL:'#63a5dd', waterD:'#2f66a3',
    tree:'#2e6b2e', treeL:'#57a857', trunk:'#6b4a2a', pine:false, snow:false },
  autumn: { name:{ru:'Осень',en:'Autumn'}, grass:'#9a9a4a', grassD:'#84843c', grassL:'#b0b05c',
    dirt:'#a07c4e', dirtD:'#8a6a3f', water:'#3d7dc2', waterL:'#63a5dd', waterD:'#2f66a3',
    tree:'#c96a1e', treeL:'#e8a33d', trunk:'#6b4a2a', pine:false, snow:false },
  winter: { name:{ru:'Зима',en:'Winter'}, grass:'#e6edf5', grassD:'#cfdbe8', grassL:'#ffffff',
    dirt:'#b8c4d4', dirtD:'#9fb0c4', water:'#2e5f8a', waterL:'#4a86b8', waterD:'#1f4a6e',
    tree:'#1f4a2e', treeL:'#2e6b3e', trunk:'#4a3421', pine:true, snow:true },
  desert: { name:{ru:'Пустыня',en:'Desert'}, grass:'#e3d29b', grassD:'#d2c184', grassL:'#f0e2b0',
    dirt:'#c9a86a', dirtD:'#b08d4f', water:'#3d7dc2', waterL:'#63a5dd', waterD:'#2f66a3',
    tree:'#5a8a3c', treeL:'#74a84e', trunk:'#6b4a2a', pine:false, snow:false },
};
ZT.BIOME_KEYS = Object.keys(ZT.BIOMES);
ZT.biome = 'summer';

/* Default settings */
ZT.settings = { sound:true, music:true, vibrate:true, particles:true, quality:'mid', lang:'ru', difficulty:'normal' };
ZT.qualityCaps = { low:{citizens:60, zombies:70}, mid:{citizens:100, zombies:110}, high:{citizens:140, zombies:160} };
