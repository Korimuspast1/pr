import { AppThemeId, MascotOutfit } from '../types/learning';

export interface RoleplayBranch {
  id: string;
  labelRu: string;
  samplePl: string;
  phoneticRu: string;
  keywords: string[]; // Polish stem/keyword hints matched phonetically & textually
  aiReplyPl: string;
  aiReplyRu: string;
  feedbackRu: string;
}

export interface RoleplayTurn {
  id: string;
  npcLinePl: string;
  npcLineRu: string;
  npcPhoneticRu: string;
  goalRu: string;
  branches: RoleplayBranch[];
}

export interface RoleplayScenario {
  id: string;
  title: string;
  location: string;
  npcName: string;
  level: string;
  description: string;
  turns: RoleplayTurn[];
}

export const ROLEPLAY_SCENARIOS: RoleplayScenario[] = [
  {
    id: 'rp_cafe',
    title: 'В кофейне Кракова',
    location: 'Kraków · Rynek Główny',
    npcName: 'Бариста Марек',
    level: 'A0–A1',
    description: 'Поздоровайтесь, закажите любимый напиток и попросите счёт.',
    turns: [
      {
        id: 't1',
        npcLinePl: 'Dzień dobry! Witam w naszej kawiarni. Co podać?',
        npcLineRu: 'Добрый день! Добро пожаловать в нашу кофейню. Что подать?',
        npcPhoneticRu: 'джень добры! витам в нашей кавярни. цо подачь?',
        goalRu: 'Закажите кофе или чай (например: «Poproszę kawę» или «Chcę herbatę z cytryną»).',
        branches: [
          {
            id: 'coffee',
            labelRu: 'Заказать кофе',
            samplePl: 'Dzień dobry, poproszę gorącą kawę z mlekiem.',
            phoneticRu: 'джень добры, попрошэ горонцоун кавэ з млекем',
            keywords: ['kawa', 'kawę', 'kawy', 'espresso', 'cappuccino', 'latte'],
            aiReplyPl: 'Świetny wybór! Gorąca kawa z mlekiem już się robi. Coś jeszcze do jedzenia?',
            aiReplyRu: 'Отличный выбор! Горячий кофе с молоком уже готовится. Что-нибудь ещё поесть?',
            feedbackRu: 'ИИ понял ваш заказ на кофе! Отлично использован Biernik («kawę»).',
          },
          {
            id: 'tea',
            labelRu: 'Заказать чай',
            samplePl: 'Poproszę gorącą herbatę.',
            phoneticRu: 'попрошэ горонцоун хэрбатэ',
            keywords: ['herbata', 'herbatę', 'herbaty', 'cytryn'],
            aiReplyPl: 'Oczywiście! Mamy pyszną herbatę z cytryną. Czy chciałby pan coś słodkiego?',
            aiReplyRu: 'Конечно! У нас вкусный чай с лимоном. Хотите что-нибудь сладкое?',
            feedbackRu: 'ИИ распознал заказ чая! Вежливая форма «Poproszę...» звучит очень естественно.',
          },
          {
            id: 'water',
            labelRu: 'Попросить воду',
            samplePl: 'Poproszę zimną wodę bez gazu.',
            phoneticRu: 'попрошэ жимноун водэ бэз газу',
            keywords: ['woda', 'wodę', 'wody', 'gazu'],
            aiReplyPl: 'Jasne, zimna woda bez gazu. Może jeszcze kawałek sernika?',
            aiReplyRu: 'Ясно, холодная вода без газа. Может, ещё кусочек сырника?',
            feedbackRu: 'Заказ воды распознан верно!',
          },
        ],
      },
      {
        id: 't2',
        npcLinePl: 'Mamy świeże pączki i pierogi. A może tylko rachunek?',
        npcLineRu: 'У нас есть свежие пончики и пероги. А может, только счёт?',
        npcPhoneticRu: 'мамы щвежэ пончки и пероги. а можэ тылько рахунек?',
        goalRu: 'Закажите пончик/пирожок или попросите счёт («Poproszę rachunek» / «Dziękuję, to wszystko»).',
        branches: [
          {
            id: 'bill',
            labelRu: 'Попросить счёт / расплатиться картой',
            samplePl: 'Dziękuję, to wszystko. Poproszę rachunek, płacę kartą.',
            phoneticRu: 'дженькуе, то вшыстко. попрошэ рахунек, пуацэ картон',
            keywords: ['rachunek', 'wszystko', 'karta', 'kartą', 'płacę', 'ile'],
            aiReplyPl: 'Dwadzieścia złotych. Proszę zbliżyć kartę. Dziękuję i miłego dnia!',
            aiReplyRu: 'Двадцать злотых. Приложите карту, пожалуйста. Спасибо и хорошего дня!',
            feedbackRu: 'Браво! Вы уверенно закрыли счёт в польской кофейне.',
          },
          {
            id: 'pastry',
            labelRu: 'Взять пончик / сладкое',
            samplePl: 'Tak, poproszę jednego pączka i rachunek.',
            phoneticRu: 'так, попрошэ еднэго пончка и рахунек',
            keywords: ['pączek', 'pączka', 'sernik', 'tak', 'poproszę', 'pierogi'],
            aiReplyPl: 'Proszę bardzo, ciepły pączek! Razem dwadzieścia pięć złotych. Smacznego!',
            aiReplyRu: 'Пожалуйста, тёплый пончик! Итого 25 злотых. Приятного аппетита!',
            feedbackRu: 'Отлично! В Польше «pączek» с розой или заварным кремом — must-try.',
          },
        ],
      },
    ],
  },
  {
    id: 'rp_station',
    title: 'На вокзале Warszawa Centralna',
    location: 'Warszawa · Dworzec Centralny',
    npcName: 'Кассир Пани Анна',
    level: 'A1–A2',
    description: 'Купите билет до Кракова и уточните платформу отправления.',
    turns: [
      {
        id: 's1',
        npcLinePl: 'Dzień dobry. Dokąd pan jedzie i na kiedy potrzebuje biletu?',
        npcLineRu: 'Добрый день. Куда вы едете и на когда нужен билет?',
        npcPhoneticRu: 'джень добры. доконд пан едже и на кеды потшэбуе билету?',
        goalRu: 'Назовите город и билет (например: «Poproszę jeden bilet do Krakowa na dzisiaj»).',
        branches: [
          {
            id: 'krakow',
            labelRu: 'Билет в Краков',
            samplePl: 'Poproszę jeden bilet do Krakowa na dzisiaj.',
            phoneticRu: 'попрошэ едэн билет до кракова на джищай',
            keywords: ['krakow', 'krakowa', 'bilet', 'jeden', 'dzisiaj', 'teraz'],
            aiReplyPl: 'Pociąg Intercity odjeżdża za dwadzieścia minut. Bilet normalny czy ulgowy?',
            aiReplyRu: 'Поезд Intercity отправляется через 20 минут. Обычный билет или льготный?',
            feedbackRu: 'Супер! Предлог «do» + Dopełniacz («do Krakowa») применён верно.',
          },
          {
            id: 'gdansk',
            labelRu: 'Билет в Гданьск',
            samplePl: 'Chcę kupić bilet do Gdańska.',
            phoneticRu: 'хцэ купичь билет до гданьска',
            keywords: ['gdańsk', 'gdańska', 'gdansk', 'kupić', 'pociąg'],
            aiReplyPl: 'Mamy bezpośredni pociąg nad morze. Bilet normalny czy ulgowy?',
            aiReplyRu: 'Есть прямой поезд к морю. Билет обычный или льготный?',
            feedbackRu: 'ИИ распознал маршрут до Гданьска!',
          },
        ],
      },
      {
        id: 's2',
        npcLinePl: 'Oto pański bilet. Czy ma pan jeszcze jakieś pytanie?',
        npcLineRu: 'Вот ваш билет. У вас есть ещё какой-нибудь вопрос?',
        npcPhoneticRu: 'ото паньски билет. чы ма пан ешчэ якещ пытане?',
        goalRu: 'Спросите, с какой платформы (peron) отправляется поезд, или поблагодарите.',
        branches: [
          {
            id: 'peron',
            labelRu: 'Уточнить платформу',
            samplePl: 'Z którego peronu odjeżdża pociąg? Dziękuję bardzo!',
            phoneticRu: 'з ктурэго пэрону одъежджа почонг? дженькуе бардзо!',
            keywords: ['peron', 'peronu', 'gdzie', 'kiedy', 'odjeżdża', 'tor'],
            aiReplyPl: 'Z peronu trzeciego, tor czwarty. Szczęśliwej podróży!',
            aiReplyRu: 'С третьей платформы, четвёртый путь. Счастливого пути!',
            feedbackRu: 'Блестяще! Теперь вы не потеряетесь на польском вокзале.',
          },
          {
            id: 'thanks',
            labelRu: 'Поблагодарить и попрощаться',
            samplePl: 'Nie, dziękuję bardzo. Do widzenia!',
            phoneticRu: 'не, дженькуе бардзо. до видзэня!',
            keywords: ['dziękuję', 'nie', 'widzenia', 'wszystko'],
            aiReplyPl: 'Proszę bardzo! Miłej podróży i do widzenia!',
            aiReplyRu: 'Пожалуйста! Приятной поездки и до свидания!',
            feedbackRu: 'Коротко, вежливо и абсолютно понятно!',
          },
        ],
      },
    ],
  },
  {
    id: 'rp_zabka',
    title: 'В магазине Żabka на кассе',
    location: 'Gdańsk · Żabka',
    npcName: 'Кассир Кася',
    level: 'A1',
    description: 'Купите продукты, ответьте про приложение Żappka и пакет.',
    turns: [
      {
        id: 'z1',
        npcLinePl: 'Cześć! Czy ma pan aplikację Żappka i potrzebuje reklamówki?',
        npcLineRu: 'Привет! У вас есть приложение Żappka и нужен ли пакет?',
        npcPhoneticRu: 'чещьчь! чы ма пан апликацъе жапка и потшэбуе рекламувки?',
        goalRu: 'Ответьте, нужен ли пакет (reklamówka/torba) и есть ли приложение.',
        branches: [
          {
            id: 'no_bag',
            labelRu: 'Без приложения и без пакета',
            samplePl: 'Nie mam aplikacji i nie potrzebuję reklamówki, dziękuję.',
            phoneticRu: 'не мам апликацъи и не потшэбуе рекламувки, дженькуе',
            keywords: ['nie', 'mam', 'reklamówki', 'torby', 'aplikacji', 'dziękuję'],
            aiReplyPl: 'Rozumiem! Zapraszamy też na gorącego hot-doga. Płatność kartą czy gotówką?',
            aiReplyRu: 'Понятно! Также предлагаем горячий хот-дог. Оплата картой или наличными?',
            feedbackRu: 'Отлично! В Польше «nie mam» и «nie potrzebuję» требуют Dopełniacz (reklamówki)!',
          },
          {
            id: 'yes_bag',
            labelRu: 'Попросить пакет',
            samplePl: 'Poproszę małą reklamówkę. Mam aplikację.',
            phoneticRu: 'попрошэ мауоун рекламувкэ. мам апликацъе',
            keywords: ['poproszę', 'tak', 'mam', 'reklamówkę', 'torbę'],
            aiReplyPl: 'Proszę bardzo, oto torba! Płaci pan kartą czy gotówką?',
            aiReplyRu: 'Пожалуйста, вот пакет! Платите картой или наличными?',
            feedbackRu: 'Здорово! В польских магазинах слово «reklamówka» (пакет) услышите каждый день.',
          },
        ],
      },
      {
        id: 'z2',
        npcLinePl: 'Czy potrzebuje pan potwierdzenia płatności?',
        npcLineRu: 'Нужен ли вам чек об оплате?',
        npcPhoneticRu: 'чы потшэбуе пан потфердзэня пуатнощчи?',
        goalRu: 'Скажите «Картой, чек не нужен» или «Да, пожалуйста, чек».',
        branches: [
          {
            id: 'card_no_receipt',
            labelRu: 'Картой, без чека',
            samplePl: 'Płacę kartą, dziękuję, nie potrzebuję potwierdzenia.',
            phoneticRu: 'пуацэ картон, дженькуе, не потшэбуе потфердзэня',
            keywords: ['kartą', 'karta', 'nie', 'dziękuję', 'potwierdzenia', 'gotówką'],
            aiReplyPl: 'Gotowe! Miłego wieczoru, cześć!',
            aiReplyRu: 'Готово! Приятного вечера, пока!',
            feedbackRu: 'Вы звучите как настоящий житель Польши!',
          },
        ],
      },
    ],
  },
  {
    id: 'rp_doctor',
    title: 'На приёме у врача',
    location: 'Wrocław · Przychodnia',
    npcName: 'Доктор Новак',
    level: 'A1+–A2',
    description: 'Объясните симптомы и узнайте, как принимать лекарство.',
    turns: [
      {
        id: 'd1',
        npcLinePl: 'Dzień dobry. Proszę usiąść. Co panu dolega? Co pana boli?',
        npcLineRu: 'Добрый день. Присаживайтесь. Что вас беспокоит? Что болит?',
        npcPhoneticRu: 'джень добры. прошэ ущонщчь. цо пану долега? цо пана боли?',
        goalRu: 'Скажите, что у вас болит голова/горло или температура (например: «Boli mnie głowa i mam gorączkę»).',
        branches: [
          {
            id: 'head',
            labelRu: 'Болит голова / горло',
            samplePl: 'Boli mnie głowa i gardło od wczoraj.',
            phoneticRu: 'боли мне гуова и гардэо од фчорай',
            keywords: ['boli', 'głowa', 'gardło', 'gorączka', 'gorączkę', 'kaszel'],
            aiReplyPl: 'Rozumiem, to wygląda na przeziębienie. Proszę pić dużo ciepłej herbaty z miodem.',
            aiReplyRu: 'Понимаю, это похоже на простуду. Пейте побольше тёплого чая с мёдом.',
            feedbackRu: 'Отлично! Конструкция «Boli mnie...» — ключевая фраза у врача.',
          },
        ],
      },
      {
        id: 'd2',
        npcLinePl: 'Przepiszę panu tabletki. Czy potrzebuje pan zwolnienia do pracy?',
        npcLineRu: 'Выпишу вам таблетки. Нужен ли вам больничный на работу?',
        npcPhoneticRu: 'пшэпишэ пану таблетки. чы потшэбуе пан звольненя до працы?',
        goalRu: 'Ответьте про рецепт / больничный или поблагодарите врача.',
        branches: [
          {
            id: 'rx',
            labelRu: 'Попросить рецепт / поблагодарить',
            samplePl: 'Tak, poproszę receptę i zwolnienie. Bardzo dziękuję, doktorze!',
            phoneticRu: 'так, попрошэ рэцэптэ и звольнене. бардзо дженькуе, доктожэ!',
            keywords: ['tak', 'nie', 'receptę', 'zwolnienie', 'dziękuję', 'apteka'],
            aiReplyPl: 'E-recepta jest już gotowa w aptece. Życzę szybkiego powrotu do zdrowia!',
            aiReplyRu: 'Электронный рецепт уже готов в аптеке. Желаю скорейшего выздоровления!',
            feedbackRu: 'Супер! Теперь вы сможете объясниться в польской поликлинике.',
          },
        ],
      },
    ],
  },
];

export interface KartaPolakaItem {
  id: string;
  topic: string;
  questionPl: string;
  questionRu: string;
  sampleAnswerPl: string;
  sampleAnswerRu: string;
  phoneticRu: string;
  options: string[];
  correctOption: string;
  factRu: string;
}

export const KARTA_POLAKA_ITEMS: KartaPolakaItem[] = [
  {
    id: 'kp_1',
    topic: 'Национальные символы',
    questionPl: 'Jakie są barwy polskiej flagi i co jest w godle Polski?',
    questionRu: 'Какие цвета на польском флаге и что изображено на гербе Польши?',
    sampleAnswerPl: 'Polska flaga jest biało-czerwona, a w godle jest biały orzeł w złotej koronie.',
    sampleAnswerRu: 'Польский флаг бело-красный, а на гербе — белый орёл в золотой короне.',
    phoneticRu: 'польска фляга ест бяуо-чэрвона, а в годле ест бяуы ожэу в зуотэй короне',
    options: [
      'Biało-czerwona flaga i biały orzeł w koronie',
      'Niebiesko-żółta flaga i złoty lew',
      'Zielono-biała flaga i czarny orzeł',
      'Czerwono-czarna flaga i srebrny koń',
    ],
    correctOption: 'Biało-czerwona flaga i biały orzeł w koronie',
    factRu: 'Белый цвет символизирует чистоту и благородство, красный — мужество и огонь.',
  },
  {
    id: 'kp_2',
    topic: 'Праздники и традиции',
    questionPl: 'Kiedy Polacy obchodzą Święto Niepodległości?',
    questionRu: 'Когда в Польше празднуют День Независимости?',
    sampleAnswerPl: 'Święto Niepodległości obchodzimy jedenastego listopada.',
    sampleAnswerRu: 'День Независимости мы отмечаем 11 ноября.',
    phoneticRu: 'щвенто неподлегуощчи обходжимы едэнастэго листопада',
    options: [
      '11 listopada (1918 roku)',
      '3 maja',
      '24 grudnia',
      '1 stycznia',
    ],
    correctOption: '11 listopada (1918 roku)',
    factRu: '11 ноября 1918 года Польша восстановила независимость после 123 лет разделов (Юзеф Пилсудский).',
  },
  {
    id: 'kp_3',
    topic: 'Рождество (Wigilia)',
    questionPl: 'Ile potraw tradycyjnie przygotowuje się na Wigilię i czym dzielą się Polacy?',
    questionRu: 'Сколько блюд традиционно готовят на Сочельник и чем делятся поляки?',
    sampleAnswerPl: 'Przygotowuje się dwanaście potraw i wszyscy dzielą się opłatkiem.',
    sampleAnswerRu: 'Готовят 12 постных блюд, и все делятся облаткой (opłatek).',
    phoneticRu: 'пшыготовуе ще дванащче потраф и вшысцы дзелоун ще опуаткем',
    options: [
      '12 potraw i dzielenie się opłatkiem',
      '7 potraw i malowanie jajek',
      '5 potraw i pieczenie kiełbasy',
      '3 potrawy i picie gorącej kawy',
    ],
    correctOption: '12 potraw i dzielenie się opłatkiem',
    factRu: 'На столе оставляют одно свободное место для неожиданного гостя (dla zbłąkanego wędrowca).',
  },
  {
    id: 'kp_4',
    topic: 'Великие поляки',
    questionPl: 'Kto powiedział, że «wstrzymał Słońce, ruszył Ziemię»?',
    questionRu: 'О ком говорят, что он «остановил Солнце и сдвинул Землю»?',
    sampleAnswerPl: 'To był wielki polski astronom Mikołaj Kopernik z Torunia.',
    sampleAnswerRu: 'Это был великий польский астроном Николай Коперник из Торуни.',
    phoneticRu: 'то быу вельки польски астроном микоуай копэрник з торуня',
    options: [
      'Mikołaj Kopernik',
      'Fryderyk Chopin',
      'Adam Mickiewicz',
      'Maria Skłodowska-Curie',
    ],
    correctOption: 'Mikołaj Kopernik',
    factRu: 'Коперник родился в Торуни (1473) и учился в Ягеллонском университете в Кракове.',
  },
  {
    id: 'kp_5',
    topic: 'Вопросы консула',
    questionPl: 'Dlaczego stara się pan / pani o Kartę Polaka?',
    questionRu: 'Почему вы претендуете на Карту Поляка? (Ключевой вопрос на собеседовании)',
    sampleAnswerPl: 'Czuję więź z polską kulturą, znam historię i chcę kultywować polskie tradycje.',
    sampleAnswerRu: 'Я чувствую связь с польской культурой, знаю историю и хочу беречь польские традиции.',
    phoneticRu: 'чуе венжь з польскоун культуроун, знам хисторье и хцэ культывовачь польске традыцъе',
    options: [
      'Czuję więź z polską kulturą i tradycjami',
      'Bo chcę kupić tani bilet na pociąg',
      'Nie lubię mówić po polsku',
      'Bo nie wiem, gdzie jest Warszawa',
    ],
    correctOption: 'Czuję więź z polską kulturą i tradycjami',
    factRu: 'На собеседовании консул особенно ценит искреннее знание семейных традиций и базовой истории Польши.',
  },
  {
    id: 'kp_6',
    topic: 'География и столицы',
    questionPl: 'Jaka rzeka przepływa przez Kraków i Warszawę i jakie były stolice Polski?',
    questionRu: 'Какая река течёт через Краков и Варшаву и какие города были столицами Польши?',
    sampleAnswerPl: 'Przez Kraków i Warszawę płynie Wisła. Stolicami były Gniezno, Kraków i Warszawa.',
    sampleAnswerRu: 'Через Краков и Варшаву течёт Висла. Столицами были Гнезно, Краков и Варшава.',
    phoneticRu: 'пшэз кракув и варшавэ пуыне висуа. столицами быуы гнезно, кракув и варшава',
    options: [
      'Wisła · Gniezno, Kraków i Warszawa',
      'Odra · Gdańsk i Poznań',
      'Bug · Lublin i Wrocław',
      'Warta · Toruń i Łódź',
    ],
    correctOption: 'Wisła · Gniezno, Kraków i Warszawa',
    factRu: 'Висла — самая длинная река Польши (1047 км), впадает в Балтийское море возле Гданьска.',
  },
];

export interface CaseBuilderItem {
  id: string;
  genderLabel: string;
  ru: string;
  stem: string;
  cases: {
    Mianownik: { form: string; ending: string; context: string; ru: string };
    Dopełniacz: { form: string; ending: string; context: string; ru: string };
    Biernik: { form: string; ending: string; context: string; ru: string };
    Narzędnik: { form: string; ending: string; context: string; ru: string };
    Miejscownik: { form: string; ending: string; context: string; ru: string };
  };
}

export const CASE_BUILDER_ITEMS: CaseBuilderItem[] = [
  {
    id: 'cb_kawa',
    genderLabel: 'Женский род (ona)',
    ru: 'кофе',
    stem: 'kaw',
    cases: {
      Mianownik: { form: 'kawa', ending: 'a', context: 'To jest gorąca ___ .', ru: 'Это горячий кофе.' },
      Dopełniacz: { form: 'kawy', ending: 'y', context: 'Nie piję ___ wieczorem.', ru: 'Я не пью кофе вечером.' },
      Biernik: { form: 'kawę', ending: 'ę', context: 'Poproszę gorącą ___ .', ru: 'Пожалуйста, горячий кофе.' },
      Narzędnik: { form: 'kawą', ending: 'ą', context: 'Siedzę w kawiarni z ___ .', ru: 'Я сижу в кофейне с кофе.' },
      Miejscownik: { form: 'kawie', ending: 'ie', context: 'Rozmawiamy o dobrej ___ .', ru: 'Мы говорим о хорошем кофе.' },
    },
  },
  {
    id: 'cb_herbata',
    genderLabel: 'Женский род (ona)',
    ru: 'чай',
    stem: 'herbat',
    cases: {
      Mianownik: { form: 'herbata', ending: 'a', context: 'Ta ___ jest bardzo dobra.', ru: 'Этот чай очень хороший.' },
      Dopełniacz: { form: 'herbaty', ending: 'y', context: 'Nie mam zielonej ___ .', ru: 'У меня нет зелёного чая.' },
      Biernik: { form: 'herbatę', ending: 'ę', context: 'Ty pijesz ciepłą ___ .', ru: 'Ты пьёшь тёплый чай.' },
      Narzędnik: { form: 'herbatą', ending: 'ą', context: 'Ciasto z gorącą ___ .', ru: 'Пирог с горячим чаем.' },
      Miejscownik: { form: 'herbacie', ending: 'cie', context: 'Myślę o polskiej ___ .', ru: 'Я думаю о польском чае.' },
    },
  },
  {
    id: 'cb_kot',
    genderLabel: 'Мужской одушевлённый (on)',
    ru: 'кот',
    stem: 'ko',
    cases: {
      Mianownik: { form: 'kot', ending: 't', context: 'Ten mały ___ śpi.', ru: 'Этот маленький кот спит.' },
      Dopełniacz: { form: 'kota', ending: 'ta', context: 'Nie widzę mojego ___ .', ru: 'Я не вижу моего кота.' },
      Biernik: { form: 'kota', ending: 'ta', context: 'Mam czarnego ___ .', ru: 'У меня есть чёрный кот.' },
      Narzędnik: { form: 'kotem', ending: 'tem', context: 'Bawię się z ___ .', ru: 'Я играю с котом.' },
      Miejscownik: { form: 'kocie', ending: 'cie', context: 'Czytam książkę o ___ .', ru: 'Я читаю книгу о коте.' },
    },
  },
  {
    id: 'cb_warszawa',
    genderLabel: 'Женский род (город)',
    ru: 'Варшава',
    stem: 'Warszaw',
    cases: {
      Mianownik: { form: 'Warszawa', ending: 'a', context: '___ jest stolicą Polski.', ru: 'Варшава — столица Польши.' },
      Dopełniacz: { form: 'Warszawy', ending: 'y', context: 'Jadę pociągiem do ___ .', ru: 'Я еду на поезде в Варшаву.' },
      Biernik: { form: 'Warszawę', ending: 'ę', context: 'Bardzo lubię ___ .', ru: 'Я очень люблю Варшаву.' },
      Narzędnik: { form: 'Warszawą', ending: 'ą', context: 'Zachwycam się ___ .', ru: 'Я восхищаюсь Варшавой.' },
      Miejscownik: { form: 'Warszawie', ending: 'ie', context: 'Mieszkam i pracuję w ___ .', ru: 'Я живу и работаю в Варшаве.' },
    },
  },
  {
    id: 'cb_pociag',
    genderLabel: 'Мужской неодушевлённый (on)',
    ru: 'поезд',
    stem: 'pociąg',
    cases: {
      Mianownik: { form: 'pociąg', ending: '∅', context: 'Szybki ___ już jedzie.', ru: 'Скорый поезд уже едет.' },
      Dopełniacz: { form: 'pociągu', ending: 'u', context: 'Nie ma mojego ___ .', ru: 'Моего поезда нет.' },
      Biernik: { form: 'pociąg', ending: '∅', context: 'Widzę duży ___ na peronie.', ru: 'Я вижу большой поезд на перроне.' },
      Narzędnik: { form: 'pociągiem', ending: 'iem', context: 'Jadę ___ do Gdańska.', ru: 'Я еду на поезде в Гданьск.' },
      Miejscownik: { form: 'pociągu', ending: 'u', context: 'Czytam gazetę w ___ .', ru: 'Я читаю газету в поезде.' },
    },
  },
];

export interface MinimalPairItem {
  id: string;
  contrastTitle: string;
  wordA: { pl: string; phoneticRu: string; ru: string };
  wordB: { pl: string; phoneticRu: string; ru: string };
  tipRu: string;
}

export const MINIMAL_PAIRS: MinimalPairItem[] = [
  {
    id: 'mp_czy_trzy',
    contrastTitle: 'czy vs trzy',
    wordA: { pl: 'czy', phoneticRu: 'чы', ru: 'ли / разве' },
    wordB: { pl: 'trzy', phoneticRu: 'тшы', ru: 'три' },
    tipRu: '«czy» = единый твёрдый [ч], а в «trzy» сначала слышится [т], затем твёрдый [ш] → [тшы].',
  },
  {
    id: 'mp_czesc_czesc',
    contrastTitle: 'cześć vs część',
    wordA: { pl: 'cześć', phoneticRu: 'чещьчь', ru: 'привет / пока' },
    wordB: { pl: 'część', phoneticRu: 'ченщьчь', ru: 'часть' },
    tipRu: 'В «część» есть носовой гласный «ę» ([ченщьчь]), а в «cześć» — обычный [е].',
  },
  {
    id: 'mp_byc_bic',
    contrastTitle: 'być vs bić',
    wordA: { pl: 'być', phoneticRu: 'бычь', ru: 'быть' },
    wordB: { pl: 'bić', phoneticRu: 'бичь', ru: 'бить' },
    tipRu: '«y» после «b» делает звук твёрдым [бычь], а «i» смягчает губы в нежное [бичь].',
  },
  {
    id: 'mp_kat_kot',
    contrastTitle: 'kąt vs kot',
    wordA: { pl: 'kąt', phoneticRu: 'конт', ru: 'угол' },
    wordB: { pl: 'kot', phoneticRu: 'кот', ru: 'кот' },
    tipRu: '«ą» даёт долгий носовой [он]: [конт] (угол) против короткого [кот] (кот).',
  },
  {
    id: 'mp_paczek_paczek',
    contrastTitle: 'pączek vs paczek',
    wordA: { pl: 'pączek', phoneticRu: 'пончек', ru: 'пончик' },
    wordB: { pl: 'paczek', phoneticRu: 'пачек', ru: 'посылок (род. пад.)' },
    tipRu: 'Носовое «ą» звучит как [он] (пончик), а чистое «a» — просто [а]!',
  },
  {
    id: 'mp_lozko_lyzka',
    contrastTitle: 'łóżko vs łyżka',
    wordA: { pl: 'łóżko', phoneticRu: 'уушко', ru: 'кровать' },
    wordB: { pl: 'łyżka', phoneticRu: 'уышка', ru: 'ложка' },
    tipRu: '«ó» звучит как русское [у] ([уушко]), а «y» — глубокое [ы] ([уышка]).',
  },
  {
    id: 'mp_prosze_prosie',
    contrastTitle: 'proszę vs prosie',
    wordA: { pl: 'proszę', phoneticRu: 'прошэ', ru: 'пожалуйста' },
    wordB: { pl: 'prosię', phoneticRu: 'проще', ru: 'поросёнок' },
    tipRu: 'Твёрдое «sz» ([прошэ]) vs мягкое «si» ([проще]) — одна ошибка и вместо «пожалуйста» выходит поросёнок!',
  },
];

export const SHOP_OUTFITS: Array<{
  id: MascotOutfit;
  name: string;
  desc: string;
  price: number;
}> = [
  { id: 'classic', name: 'Классический Орёл', desc: 'Бело-красный шарф и золотая корона.', price: 0 },
  { id: 'coffee', name: 'Краковский Бариста', desc: 'Кофейный фартук и ароматный эспрессо.', price: 80 },
  { id: 'student', name: 'Студент Ягеллонского', desc: 'Академическая шапочка и умные очки.', price: 120 },
  { id: 'hussar', name: 'Крылатый Гусар', desc: 'Легендарные перья и стальной шлем.', price: 200 },
  { id: 'king', name: 'Король на Вавеле', desc: 'Пурпурная мантия и корона с самоцветами.', price: 320 },
];

export const SHOP_THEMES: Array<{
  id: AppThemeId;
  name: string;
  desc: string;
  primary: string;
  price: number;
}> = [
  { id: 'poland', name: 'Бело-красная Польша', desc: 'Классическая палитра Duolingo + Польша.', primary: '#DC143C', price: 0 },
  { id: 'krakow', name: 'Королевский Краков', desc: 'Фиолетово-золотой стиль Вавеля.', primary: '#8549BA', price: 90 },
  { id: 'baltic', name: 'Балтийский Гданьск', desc: 'Свежий морской синий акцент.', primary: '#1899D6', price: 90 },
];
