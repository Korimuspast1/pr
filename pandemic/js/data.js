/* ------------------------------------------------------------------
 * GAME_DATA — весь контент игры: патогены, сложности, дерево эволюции.
 * Загружается как обычный <script>, доступен как window.GAME_DATA.
 * ------------------------------------------------------------------ */
(function (root) {
  'use strict';

  const PATHOGENS = [
    {
      id: 'virus',
      name: 'Вирус',
      tagline: 'Быстрая мутация',
      icon: 'VIR',
      color: '#ff5b4a',
      desc: 'Нестабильный геном: симптомы дешевле, мутации случаются в 2 раза чаще. Организм человека замечает вирус раньше.',
      mods: { infectivity: 0.005, lethality: 0.0012, complexity: 1.0, mutation: 1.9, symptomCost: 0.8, abilityCost: 1.1, dna: 1 },
    },
    {
      id: 'bacteria',
      name: 'Бактерия',
      tagline: 'Живучий универсал',
      icon: 'BAC',
      color: '#ffb03a',
      desc: 'Сбалансированный патоген. Легко приобретает устойчивость к лекарствам, но каждая способность стоит дороже.',
      mods: { infectivity: 0.008, lethality: 0.001, complexity: 1.12, mutation: 1.0, symptomCost: 1.0, abilityCost: 0.85, dna: 1.05 },
    },
    {
      id: 'fungus',
      name: 'Грибок',
      tagline: 'Споры и упорство',
      icon: 'FNG',
      color: '#8be36b',
      desc: 'Медленно захватывает носителей, зато споры переживают любые условия, а лечить его крайне сложно.',
      mods: { infectivity: 0.003, lethality: 0.0008, complexity: 1.3, mutation: 0.85, symptomCost: 1.0, abilityCost: 1.0, dna: 1.05, climateBonus: 0.18 },
    },
    {
      id: 'parasite',
      name: 'Паразит',
      tagline: 'Тихий убийца',
      icon: 'PAR',
      color: '#5ad1ff',
      desc: 'Долго остаётся незамеченным: симптомы проявляются поздно. Заражённые реже погибают, но и лечатся хуже.',
      mods: { infectivity: 0.006, lethality: 0.0006, complexity: 1.2, mutation: 1.05, symptomCost: 1.1, abilityCost: 0.95, dna: 1.05, stealth: 0.3 },
    },
    {
      id: 'prion',
      name: 'Прион',
      tagline: 'Неизлечимый',
      icon: 'PRN',
      color: '#c58cff',
      desc: 'Почти не поддаётся лечению и убивает медленно, но верно. Самый сложный патоген для терпеливых.',
      mods: { infectivity: 0.002, lethality: 0.0016, complexity: 1.55, mutation: 0.9, symptomCost: 1.0, abilityCost: 1.0, dna: 1.1, cureResist: 0.15 },
    },
  ];

  const DIFFICULTIES = [
    { id: 'casual', name: 'Лёгкая', desc: 'Мир реагирует медленно. Для первого запуска.', cure: 0.62, detect: 0.8, dna: 1.2, travel: 0.9 },
    { id: 'normal', name: 'Обычная', desc: 'Классический баланс. Рекомендуется.', cure: 1, detect: 1, dna: 1, travel: 1 },
    { id: 'brutal', name: 'Жестокая', desc: 'ВОЗ работает как часы, границы закрываются рано.', cure: 1.55, detect: 1.25, dna: 0.85, travel: 1.15 },
  ];

  /* Эффекты апгрейдов:
   *  inf       — прибавка к заразности
   *  leth      — прибавка к летальности (доля умерших среди заражённых в день)
   *  severity  — насколько симптом заметен (ускоряет обнаружение)
   *  air/water — уровни воздушного/морского распространения
   *  climate   — {cold, hot, arid, humid} прибавка к приспособленности
   *  cureResist— снижение скорости разработки лекарства
   *  stealth   — затрудняет обнаружение
   *  mutation  — множитель частоты мутаций
   *  dnaRate   — прибавка к доходу ДНК
   */
  const UPGRADES = [
    /* ------------------------------- ПЕРЕДАЧА ------------------------------ */
    { id: 'air1', cat: 'transmission', name: 'Воздушно-капельный путь I', icon: 'ВЗД', cost: 5, req: [],
      desc: 'Патоген выживает в каплях слюны и передаётся при кашле и разговоре.', fx: { inf: 0.022, air: 1 } },
    { id: 'air2', cat: 'transmission', name: 'Воздушно-капельный путь II', icon: 'ВЗД', cost: 12, req: ['air1'],
      desc: 'Мелкодисперсный аэрозоль держится в воздухе часами и проникает вглубь лёгких.', fx: { inf: 0.04, air: 2 } },
    { id: 'water1', cat: 'transmission', name: 'Водный путь I', icon: 'ВОД', cost: 6, req: [],
      desc: 'Микрочастицы заразы попадают в водопровод и водоёмы.', fx: { inf: 0.018, water: 1 } },
    { id: 'water2', cat: 'transmission', name: 'Водный путь II', icon: 'ВОД', cost: 14, req: ['water1'],
      desc: 'Патоген устойчив к хлорированию. Массовые вспышки в городах.', fx: { inf: 0.032, water: 2 } },
    { id: 'vector', cat: 'transmission', name: 'Насекомые-переносчики', icon: 'ВЕК', cost: 8, req: [],
      desc: 'Комары и мухи переносят заразу между людьми и животными.', fx: { inf: 0.02, climate: { hot: 0.2, humid: 0.15 } } },
    { id: 'rodent', cat: 'transmission', name: 'Грызуны', icon: 'ГРЗ', cost: 7, req: [],
      desc: 'Крысы разносят инфекцию в перенаселённых районах и трущобах.', fx: { inf: 0.018, climate: { arid: 0.1 } } },
    { id: 'livestock', cat: 'transmission', name: 'Домашний скот', icon: 'СКТ', cost: 8, req: [],
      desc: 'Зараза живёт в стадах и попадает к людям с едой и молоком.', fx: { inf: 0.024 } },
    { id: 'blood', cat: 'transmission', name: 'Кровь и жидкости', icon: 'КРВ', cost: 10, req: [],
      desc: 'Передача через кровь, слюну и медицинские процедуры.', fx: { inf: 0.03, severity: 2 } },
    { id: 'spores', cat: 'transmission', name: 'Спорообразование', icon: 'СПР', cost: 16, req: ['air1'],
      desc: 'Патоген формирует споры, живущие в почве и пыли годами.', fx: { inf: 0.03, climate: { arid: 0.2, cold: 0.1 } } },
    { id: 'bioaerosol', cat: 'transmission', name: 'Экстремальный биоаэрозоль', icon: 'БИО', cost: 26, req: ['air2'],
      desc: 'Один вдох — гарантированное заражение. Воздух становится оружием.', fx: { inf: 0.075, air: 3 } },
    { id: 'antibiotics', cat: 'transmission', name: 'Устойчивость к антибиотикам', icon: 'АБР', cost: 12, req: ['air1'],
      desc: 'Стандартные препараты не работают: врачи теряют драгоценное время.', fx: { cureResist: 0.08, inf: 0.008 } },

    /* ------------------------------- СИМПТОМЫ ------------------------------ */
    { id: 'runnyNose', cat: 'symptom', name: 'Насморк', icon: 'НАС', cost: 2, req: [],
      desc: 'Обильные выделения из носа ускоряют заражение окружающих.', fx: { inf: 0.012, severity: 1 } },
    { id: 'cough', cat: 'symptom', name: 'Кашель', icon: 'КШЛ', cost: 3, req: [],
      desc: 'Частые приступы кашля выбрасывают патоген в воздух.', fx: { inf: 0.016, severity: 2 } },
    { id: 'rash', cat: 'symptom', name: 'Сыпь', icon: 'СЫП', cost: 6, req: ['cough'],
      desc: 'Характерные высыпания по телу — первый сигнал для медиков.', fx: { inf: 0.012, severity: 3 } },
    { id: 'nausea', cat: 'symptom', name: 'Тошнота', icon: 'ТШН', cost: 5, req: ['runnyNose'],
      desc: 'Постоянная тошнота изматывает носителя.', fx: { inf: 0.012, severity: 2 } },
    { id: 'vomiting', cat: 'symptom', name: 'Рвота', icon: 'РВТ', cost: 8, req: ['nausea'],
      desc: 'Рвотные массы разносят инфекцию в быту.', fx: { inf: 0.024, severity: 3 } },
    { id: 'fever', cat: 'symptom', name: 'Лихорадка', icon: 'ЛИХ', cost: 8, req: ['cough'],
      desc: 'Температура выше 40 °C. Организм изнашивается.', fx: { leth: 0.0022, severity: 3 } },
    { id: 'sweating', cat: 'symptom', name: 'Потливость', icon: 'ПОТ', cost: 6, req: ['fever'],
      desc: 'Обильный пот насыщает патогеном всё, к чему прикасается носитель.', fx: { inf: 0.022, severity: 2 } },
    { id: 'fatigue', cat: 'symptom', name: 'Утомляемость', icon: 'УТМ', cost: 6, req: [],
      desc: 'Постоянная слабость снижает сопротивляемость.', fx: { inf: 0.012, severity: 1 } },
    { id: 'anaemia', cat: 'symptom', name: 'Анемия', icon: 'АНМ', cost: 10, req: ['fatigue'],
      desc: 'Кровь теряет способность переносить кислород.', fx: { leth: 0.005, severity: 3 } },
    { id: 'haemorrhage', cat: 'symptom', name: 'Кровотечение', icon: 'КРТ', cost: 15, req: ['anaemia'],
      desc: 'Внутренние кровотечения. Ужас в больницах.', fx: { leth: 0.009, severity: 5, inf: 0.01 } },
    { id: 'necrosis', cat: 'symptom', name: 'Некроз', icon: 'НКР', cost: 20, req: ['rash'],
      desc: 'Ткани отмирают: открытые язвы разносят инфекцию с новой силой.', fx: { leth: 0.012, inf: 0.024, severity: 6 } },
    { id: 'pneumonia', cat: 'symptom', name: 'Пневмония', icon: 'ПНМ', cost: 14, req: ['fever', 'cough'],
      desc: 'Лёгкие заполняются жидкостью. Смертность резко растёт.', fx: { leth: 0.01, inf: 0.02, severity: 4 } },
    { id: 'edema', cat: 'symptom', name: 'Отёк лёгких', icon: 'ОТЛ', cost: 18, req: ['pneumonia'],
      desc: 'Больной буквально тонет в собственной жидкости.', fx: { leth: 0.016, severity: 5 } },
    { id: 'encephalitis', cat: 'symptom', name: 'Энцефалит', icon: 'ЭНЦ', cost: 22, req: ['fever'],
      desc: 'Воспаление мозга: спутанность сознания и судороги.', fx: { leth: 0.016, severity: 4 } },
    { id: 'liver', cat: 'symptom', name: 'Печёночная недостаточность', icon: 'ПЕЧ', cost: 22, req: ['anaemia'],
      desc: 'Печень отказывает, кровь наполняется токсинами.', fx: { leth: 0.016, severity: 5 } },
    { id: 'kidney', cat: 'symptom', name: 'Почечная недостаточность', icon: 'ПОЧ', cost: 20, req: ['anaemia'],
      desc: 'Почки перестают фильтровать кровь.', fx: { leth: 0.014, severity: 5 } },
    { id: 'coma', cat: 'symptom', name: 'Кома', icon: 'КОМ', cost: 26, req: ['encephalitis'],
      desc: 'Носители впадают в кому и становятся идеальным инкубатором.', fx: { leth: 0.02, severity: 5 } },
    { id: 'paralysis', cat: 'symptom', name: 'Паралич', icon: 'ПРЛ', cost: 28, req: ['encephalitis'],
      desc: 'Массовый паралич приводит к коллапсу больниц.', fx: { leth: 0.018, severity: 5 } },
    { id: 'cytokine', cat: 'symptom', name: 'Цитокиновый шторм', icon: 'ЦТК', cost: 30, req: ['fever', 'sweating'],
      desc: 'Иммунная система убивает собственный организм.', fx: { leth: 0.028, severity: 5 } },
    { id: 'shock', cat: 'symptom', name: 'Геморрагический шок', icon: 'ШОК', cost: 32, req: ['haemorrhage'],
      desc: 'Давление падает, сознание угасает за часы.', fx: { leth: 0.034, severity: 6 } },
    { id: 'organFailure', cat: 'symptom', name: 'Полиорганная недостаточность', icon: 'ПОН', cost: 38, req: ['coma', 'pneumonia', 'kidney'],
      desc: 'Все системы организма отказывают одновременно.', fx: { leth: 0.045, severity: 7 } },

    /* ------------------------------ СПОСОБНОСТИ ---------------------------- */
    { id: 'cold1', cat: 'ability', name: 'Холодоустойчивость I', icon: 'ХЛД', cost: 7, req: [],
      desc: 'Патоген спокойно переносит низкие температуры.', fx: { climate: { cold: 0.3 } } },
    { id: 'cold2', cat: 'ability', name: 'Холодоустойчивость II', icon: 'ХЛД', cost: 15, req: ['cold1'],
      desc: 'Заражение эффективно даже в Арктике.', fx: { climate: { cold: 0.45 }, inf: 0.008 } },
    { id: 'heat1', cat: 'ability', name: 'Жароустойчивость I', icon: 'ЖРК', cost: 7, req: [],
      desc: 'Высокие температуры больше не мешают.', fx: { climate: { hot: 0.3, arid: 0.1 } } },
    { id: 'heat2', cat: 'ability', name: 'Жароустойчивость II', icon: 'ЖРК', cost: 15, req: ['heat1'],
      desc: 'Пустыни и тропики становятся домом для патогена.', fx: { climate: { hot: 0.45, arid: 0.25 }, inf: 0.008 } },
    { id: 'humid1', cat: 'ability', name: 'Влагоустойчивость', icon: 'ВЛГ', cost: 9, req: [],
      desc: 'Сырость тропиков только помогает распространению.', fx: { climate: { humid: 0.3 } } },
    { id: 'drug1', cat: 'ability', name: 'Устойчивость к лекарствам I', icon: 'ЛЕК', cost: 11, req: [],
      desc: 'Терапия замедляет патоген, но не убивает.', fx: { cureResist: 0.12 } },
    { id: 'drug2', cat: 'ability', name: 'Устойчивость к лекарствам II', icon: 'ЛЕК', cost: 24, req: ['drug1'],
      desc: 'Существующие протоколы лечения бесполезны.', fx: { cureResist: 0.18 } },
    { id: 'hardening1', cat: 'ability', name: 'Генетическая устойчивость I', icon: 'ГЕН', cost: 13, req: [],
      desc: 'Геном постоянно меняется, сбивая разработчиков вакцины.', fx: { cureResist: 0.12 } },
    { id: 'hardening2', cat: 'ability', name: 'Генетическая устойчивость II', icon: 'ГЕН', cost: 27, req: ['hardening1'],
      desc: 'Вакцина устаревает раньше, чем покидает лабораторию.', fx: { cureResist: 0.2 } },
    { id: 'latency1', cat: 'ability', name: 'Латентность I', icon: 'ЛТН', cost: 9, req: [],
      desc: 'Долгий инкубационный период: врачи замечают болезнь позже.', fx: { stealth: 0.35 } },
    { id: 'latency2', cat: 'ability', name: 'Латентность II', icon: 'ЛТН', cost: 19, req: ['latency1'],
      desc: 'Носители годами не подозревают, что больны.', fx: { stealth: 0.6 } },
    { id: 'hardeningEnv', cat: 'ability', name: 'Защита от внешней среды', icon: 'СРЕ', cost: 17, req: ['cold1', 'heat1'],
      desc: 'Патоген выживает везде: в снегу, в песке, в воде.', fx: { climate: { cold: 0.2, hot: 0.2, arid: 0.2, humid: 0.2 } } },
    { id: 'mutator', cat: 'ability', name: 'Повышенная мутация', icon: 'МУТ', cost: 14, req: [],
      desc: 'Бесплатные симптомы появляются вдвое чаще.', fx: { mutation: 2 } },
    { id: 'replication', cat: 'ability', name: 'Ускоренная репликация', icon: 'РЕП', cost: 18, req: ['mutator'],
      desc: 'Патоген размножается быстрее, чем иммунитет успевает реагировать.', fx: { inf: 0.02, dnaRate: 0.15 } },
  ];

  const CATEGORIES = [
    { id: 'transmission', name: 'Передача', hint: 'Как патоген переходит от человека к человеку' },
    { id: 'symptom', name: 'Симптомы', hint: 'Проявления болезни: заметность и смертность' },
    { id: 'ability', name: 'Способности', hint: 'Выживаемость, скрытность и сопротивление лечению' },
  ];

  const CLIMATES = {
    cold: { name: 'Холодный', icon: '❄' },
    mild: { name: 'Умеренный', icon: '☁' },
    warm: { name: 'Тёплый', icon: '☀' },
    tropical: { name: 'Тропический', icon: '☂' },
    arid: { name: 'Засушливый', icon: '⌁' },
  };

  root.GAME_DATA = { PATHOGENS, DIFFICULTIES, UPGRADES, CATEGORIES, CLIMATES };
  if (typeof module !== 'undefined' && module.exports) module.exports = root.GAME_DATA;
})(typeof globalThis !== 'undefined' ? globalThis : this);
