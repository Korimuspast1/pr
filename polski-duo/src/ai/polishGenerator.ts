import { LEXICON, VERBS } from '../data/polishLexicon';
import { Exercise, ExerciseType, LearnerAIState, MatchPair, SkillId } from '../types/learning';

let seqCounter = 0;
function nextId(prefix: string): string {
  seqCounter += 1;
  return `${prefix}_${Date.now().toString(36)}_${seqCounter}`;
}

function pickRandom<T>(arr: T[]): T {
  return arr[Math.floor(Math.random() * arr.length)];
}

function shuffle<T>(arr: T[]): T[] {
  const copy = [...arr];
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy;
}

interface RuNounForms {
  nom: string;
  acc: string;
  gen: string;
  ins: string;
  gender: 'm' | 'f' | 'n' | 'pl';
}

const RU_NOUN_MAP: Record<string, RuNounForms> = {
  noun_kot: { nom: 'кот', acc: 'кота', gen: 'кота', ins: 'котом', gender: 'm' },
  noun_pies: { nom: 'собака', acc: 'собаку', gen: 'собаки', ins: 'собакой', gender: 'f' },
  noun_dom: { nom: 'дом', acc: 'дом', gen: 'дома', ins: 'домом', gender: 'm' },
  noun_kawa: { nom: 'кофе', acc: 'кофе', gen: 'кофе', ins: 'кофе', gender: 'm' },
  noun_herbata: { nom: 'чай', acc: 'чай', gen: 'чая', ins: 'чаем', gender: 'm' },
  noun_woda: { nom: 'вода', acc: 'воду', gen: 'воды', ins: 'водой', gender: 'f' },
  noun_chleb: { nom: 'хлеб', acc: 'хлеб', gen: 'хлеба', ins: 'хлебом', gender: 'm' },
  noun_mleko: { nom: 'молоко', acc: 'молоко', gen: 'молока', ins: 'молоком', gender: 'n' },
  noun_jablko: { nom: 'яблоко', acc: 'яблоко', gen: 'яблока', ins: 'яблоком', gender: 'n' },
  noun_zupa: { nom: 'суп', acc: 'суп', gen: 'супа', ins: 'супом', gender: 'm' },
  noun_ksiazka: { nom: 'книга', acc: 'книгу', gen: 'книги', ins: 'книгой', gender: 'f' },
  noun_przyjaciel: { nom: 'друг', acc: 'друга', gen: 'друга', ins: 'другом', gender: 'm' },
  noun_brat: { nom: 'брат', acc: 'брата', gen: 'брата', ins: 'bratom', gender: 'm' },
  noun_siostra: { nom: 'сестра', acc: 'сестру', gen: 'сестры', ins: 'сестрой', gender: 'f' },
  noun_bilet: { nom: 'билет', acc: 'билет', gen: 'билета', ins: 'билетом', gender: 'm' },
  noun_telefon: { nom: 'телефон', acc: 'телефон', gen: 'телефона', ins: 'телефоном', gender: 'm' },
  noun_komputer: { nom: 'компьютер', acc: 'компьютер', gen: 'компьютера', ins: 'компьютером', gender: 'm' },
  noun_pieniadze: { nom: 'деньги', acc: 'деньги', gen: 'денег', ins: 'деньгами', gender: 'pl' },
  ff_sklep: { nom: 'магазин', acc: 'магазин', gen: 'магазина', ins: 'магазином', gender: 'm' },
  ff_owoce: { nom: 'фрукты', acc: 'фрукты', gen: 'фруктов', ins: 'фруктами', gender: 'pl' },
  ff_warzywa: { nom: 'овощи', acc: 'овощи', gen: 'овощей', ins: 'овощами', gender: 'pl' },
  ff_dywan: { nom: 'ковёр', acc: 'ковёр', gen: 'ковра', ins: 'ковром', gender: 'm' },
  ff_czas: { nom: 'время', acc: 'время', gen: 'времени', ins: 'временем', gender: 'n' },
  ff_lustro: { nom: 'зеркало', acc: 'зеркало', gen: 'зеркала', ins: 'зеркалом', gender: 'n' },
  ff_krzeslo: { nom: 'стул', acc: 'стул', gen: 'стула', ins: 'стулом', gender: 'm' },
  noun_programista: { nom: 'программист', acc: 'программиста', gen: 'программиста', ins: 'программистом', gender: 'm' },
  noun_lekarz: { nom: 'врач', acc: 'врача', gen: 'врача', ins: 'врачом', gender: 'm' },
  noun_student: { nom: 'студент', acc: 'студента', gen: 'студента', ins: 'студентом', gender: 'm' },
};

interface CombinatorialSentence {
  pl: string;
  ru: string;
  phoneticRu: string;
  skill: SkillId;
  tier: number;
  itemIds: string[];
  aiNote: string;
  grammarTag: string;
  cloze?: {
    before: string;
    after: string;
    answer: string;
    distractors: string[];
  };
  isFalseFriendTrap?: boolean;
}

const SUBJECTS: Array<{
  key: 'ja' | 'ty' | 'on' | 'ona' | 'my' | 'wy' | 'oni';
  pl: string;
  ru: string;
  conjKey: 'ja' | 'ty' | 'on' | 'my' | 'wy' | 'oni';
}> = [
  { key: 'ja', pl: 'Ja', ru: 'Я', conjKey: 'ja' },
  { key: 'ty', pl: 'Ty', ru: 'Ты', conjKey: 'ty' },
  { key: 'on', pl: 'On', ru: 'Он', conjKey: 'on' },
  { key: 'ona', pl: 'Ona', ru: 'Она', conjKey: 'on' },
  { key: 'my', pl: 'My', ru: 'Мы', conjKey: 'my' },
  { key: 'wy', pl: 'Wy', ru: 'Вы', conjKey: 'wy' },
  { key: 'oni', pl: 'Oni', ru: 'Они', conjKey: 'oni' },
];

const ADJ_RU_FORMS: Record<
  string,
  { m: string; m_acc_anim: string; f: string; f_acc: string; n: string }
> = {
  adj_goracy: { m: 'горячий', m_acc_anim: 'горячего', f: 'горячая', f_acc: 'горячую', n: 'горячее' },
  adj_zimny: { m: 'холодный', m_acc_anim: 'холодного', f: 'холодная', f_acc: 'холодную', n: 'холодное' },
  adj_swiezy: { m: 'свежий', m_acc_anim: 'свежего', f: 'свежая', f_acc: 'свежую', n: 'свежее' },
  adj_dobry: { m: 'хороший', m_acc_anim: 'хорошего', f: 'хорошая', f_acc: 'хорошую', n: 'хорошее' },
  adj_maly: { m: 'маленький', m_acc_anim: 'маленького', f: 'маленькая', f_acc: 'маленькую', n: 'маленькое' },
  adj_duzy: { m: 'большой', m_acc_anim: 'большого', f: 'большая', f_acc: 'большую', n: 'большое' },
  adj_nowy: { m: 'новый', m_acc_anim: 'нового', f: 'новая', f_acc: 'новую', n: 'новое' },
  adj_ciekawy: { m: 'интересный', m_acc_anim: 'интересного', f: 'интересная', f_acc: 'интересную', n: 'интересное' },
};

function buildSentencePool(): CombinatorialSentence[] {
  const pool: CombinatorialSentence[] = [];
  const getLex = (id: string) => LEXICON.find((x) => x.id === id)!;
  const getVerb = (id: string) => VERBS.find((v) => v.id === id)!;

  // 1. Simple "To jest [noun]" (Tier 1-2)
  const basicNouns = ['noun_kot', 'noun_pies', 'noun_dom', 'noun_kawa', 'noun_herbata', 'noun_chleb', 'noun_ksiazka', 'noun_telefon'];
  for (const nid of basicNouns) {
    const n = getLex(nid);
    const ruN = RU_NOUN_MAP[nid];
    pool.push({
      pl: `To jest ${n.pl}.`,
      ru: `Это ${ruN.nom}.`,
      phoneticRu: `то ест ${n.phoneticRu}`,
      skill: 'vocabulary_basic',
      tier: 1.5,
      itemIds: [nid],
      grammarTag: 'Mianownik (Именительный)',
      aiNote: `Конструкция «To jest ...» не изменяет падеж существительного (Mianownik).`,
    });
  }

  // 2. Drink / Food / Object SVO with Biernik (Tier 2.5 - 5)
  const drinkPairs: Array<{ verbId: string; nounId: string; adjId?: string; ruVerb: Record<string, string> }> = [
    { verbId: 'v_pic', nounId: 'noun_kawa', adjId: 'adj_goracy', ruVerb: { ja: 'пью', ty: 'пьёшь', on: 'пьёт', my: 'пьём', wy: 'пьёте', oni: 'пьют' } },
    { verbId: 'v_pic', nounId: 'noun_herbata', adjId: 'adj_goracy', ruVerb: { ja: 'пью', ty: 'пьёшь', on: 'пьёт', my: 'пьём', wy: 'пьёте', oni: 'пьют' } },
    { verbId: 'v_pic', nounId: 'noun_woda', adjId: 'adj_zimny', ruVerb: { ja: 'пью', ty: 'пьёшь', on: 'пьёт', my: 'пьём', wy: 'пьёте', oni: 'пьют' } },
    { verbId: 'v_pic', nounId: 'noun_mleko', adjId: 'adj_swiezy', ruVerb: { ja: 'пью', ty: 'пьёшь', on: 'пьёт', my: 'пьём', wy: 'пьёте', oni: 'пьют' } },
    { verbId: 'v_jesc', nounId: 'noun_chleb', adjId: 'adj_swiezy', ruVerb: { ja: 'ем', ty: 'ешь', on: 'ест', my: 'едим', wy: 'едите', oni: 'едят' } },
    { verbId: 'v_jesc', nounId: 'noun_zupa', adjId: 'adj_goracy', ruVerb: { ja: 'ем', ty: 'ешь', on: 'ест', my: 'едим', wy: 'едите', oni: 'едят' } },
    { verbId: 'v_jesc', nounId: 'noun_jablko', adjId: 'adj_swiezy', ruVerb: { ja: 'ем', ty: 'ешь', on: 'ест', my: 'едим', wy: 'едите', oni: 'едят' } },
    { verbId: 'v_czytac', nounId: 'noun_ksiazka', adjId: 'adj_ciekawy', ruVerb: { ja: 'читаю', ty: 'читаешь', on: 'читает', my: 'читаем', wy: 'читаете', oni: 'читают' } },
    { verbId: 'v_kupowac', nounId: 'noun_telefon', adjId: 'adj_nowy', ruVerb: { ja: 'покупаю', ty: 'покупаешь', on: 'покупает', my: 'покупаем', wy: 'покупаете', oni: 'покупают' } },
    { verbId: 'v_miec', nounId: 'noun_kot', adjId: 'adj_maly', ruVerb: { ja: 'имею', ty: 'имеешь', on: 'имеет', my: 'имеем', wy: 'имеете', oni: 'имеют' } },
    { verbId: 'v_widziec', nounId: 'noun_pies', adjId: 'adj_duzy', ruVerb: { ja: 'вижу', ty: 'видишь', on: 'видит', my: 'видим', wy: 'видите', oni: 'видят' } },
  ];

  for (const dp of drinkPairs) {
    const v = getVerb(dp.verbId);
    const n = getLex(dp.nounId);
    const ruN = RU_NOUN_MAP[dp.nounId];
    const adj = dp.adjId ? getLex(dp.adjId) : undefined;
    const ruAdj = dp.adjId ? ADJ_RU_FORMS[dp.adjId] : undefined;

    for (const subj of SUBJECTS.slice(0, 5)) {
      const verbPl = v.conj[subj.conjKey];
      const nounAcc = n.declension?.acc ?? n.pl;

      // Without adjective (Tier 2.5 - 3.5)
      if (dp.verbId !== 'v_miec') {
        pool.push({
          pl: `${subj.pl} ${verbPl} ${nounAcc}.`,
          ru: `${subj.ru} ${dp.ruVerb[subj.conjKey]} ${ruN.acc}.`,
          phoneticRu: `${subj.pl.toLowerCase()} ${verbPl} ${nounAcc}`,
          skill: n.gender === 'f' || n.gender === 'm_anim' ? 'cases' : 'verbs',
          tier: n.gender === 'f' || n.gender === 'm_anim' ? 3.5 : 2.8,
          itemIds: [dp.nounId, dp.verbId],
          grammarTag: 'Biernik + Спряжение',
          aiNote:
            n.gender === 'f'
              ? `После глагола «${v.infinitive}» женский род на -a переходит в -ę (${n.pl} → ${nounAcc}).`
              : n.gender === 'm_anim'
                ? `Одушевлённое существительное мужского рода в винительном падеже получает окончание -a (${n.pl} → ${nounAcc}).`
                : `Глагол «${v.infinitive}» в форме для «${subj.pl}» — «${verbPl}».`,
          cloze: {
            before: `${subj.pl} ${verbPl}`,
            after: '.',
            answer: nounAcc,
            distractors: [n.pl, n.declension?.gen ?? `${n.pl}y`, n.declension?.ins ?? `${n.pl}em`].filter(
              (x) => x !== nounAcc,
            ),
          },
        });
      }

      // With adjective (Tier 4.5 - 5.5)
      if (adj && adj.adjForms && ruAdj && dp.verbId !== 'v_miec') {
        const adjPlAcc =
          n.gender === 'f'
            ? adj.adjForms.f_acc
            : n.gender === 'm_anim'
              ? adj.adjForms.m_acc_anim
              : n.gender === 'n'
                ? adj.adjForms.n
                : adj.adjForms.m;

        const ruAdjWord =
          ruN.gender === 'f'
            ? ruAdj.f_acc
            : ruN.gender === 'n'
              ? ruAdj.n
              : n.gender === 'm_anim'
                ? ruAdj.m_acc_anim
                : ruAdj.m;

        pool.push({
          pl: `${subj.pl} ${verbPl} ${adjPlAcc} ${nounAcc}.`,
          ru: `${subj.ru} ${dp.ruVerb[subj.conjKey]} ${ruAdjWord} ${ruN.acc}.`,
          phoneticRu: `${subj.pl.toLowerCase()} ${verbPl} ${adjPlAcc} ${nounAcc}`,
          skill: 'cases',
          tier: 4.8,
          itemIds: [dp.nounId, dp.adjId!, dp.verbId],
          grammarTag: 'Biernik (Прилагательное + Существительное)',
          aiNote: `В винительном падеже согласуются оба слова: «${adjPlAcc} ${nounAcc}».`,
          cloze: {
            before: `${subj.pl} ${verbPl} ${adjPlAcc}`,
            after: '.',
            answer: nounAcc,
            distractors: [n.pl, n.declension?.gen ?? n.pl, n.declension?.ins ?? n.pl].filter((x) => x !== nounAcc),
          },
        });
      }
    }
  }

  // 3. Negation -> Dopełniacz (CRITICAL POLISH RULE! Tier 5 - 7)
  const negItems: Array<{
    pl: string;
    ru: string;
    answer: string;
    before: string;
    after: string;
    distractors: string[];
    itemIds: string[];
    note: string;
  }> = [
    {
      pl: 'Nie mam czasu.',
      ru: 'У меня нет времени.',
      before: 'Nie mam',
      after: '.',
      answer: 'czasu',
      distractors: ['czas', 'czasem', 'czasie'],
      itemIds: ['v_miec', 'ff_czas'],
      note: 'Правило отрицания: после «nie mam» всегда нужен Dopełniacz (родительный падеж) → czasu. И помните: czas = время!',
    },
    {
      pl: 'Nie piję czarnej kawy.',
      ru: 'Я не пью чёрный кофе.',
      before: 'Nie piję czarnej',
      after: '.',
      answer: 'kawy',
      distractors: ['kawę', 'kawa', 'kawą'],
      itemIds: ['v_pic', 'noun_kawa'],
      note: 'В утверждении «piję kawę» (Biernik), но при «nie» падеж меняется на Dopełniacz → «nie piję kawy»!',
    },
    {
      pl: 'Nie mam pieniędzy.',
      ru: 'У меня нет денег.',
      before: 'Nie mam',
      after: '.',
      answer: 'pieniędzy',
      distractors: ['pieniądze', 'pieniądz', 'pieniędzmi'],
      itemIds: ['v_miec', 'noun_pieniadze'],
      note: 'После «nie mam» требуется Dopełniacz множественного числа → pieniędzy.',
    },
    {
      pl: 'Nie lubię zimnej wody.',
      ru: 'Я не люблю холодную воду.',
      before: 'Nie lubię zimnej',
      after: '.',
      answer: 'wody',
      distractors: ['wodę', 'woda', 'wodą'],
      itemIds: ['v_lubic', 'noun_woda', 'adj_zimny'],
      note: 'При отрицании «nie lubię» винительный «zimną wodę» превращается в родительный «zimnej wody».',
    },
    {
      pl: 'On nie czyta tej książki.',
      ru: 'Он не читает эту книгу.',
      before: 'On nie czyta tej',
      after: '.',
      answer: 'książki',
      distractors: ['książkę', 'książka', 'książką'],
      itemIds: ['v_czytac', 'noun_ksiazka'],
      note: 'С «nie czyta» используем Dopełniacz → książki (вместо książkę).',
    },
    {
      pl: 'Nie widzę mojego kota.',
      ru: 'Я не вижу моего кота.',
      before: 'Nie widzę mojego',
      after: '.',
      answer: 'kota',
      distractors: ['kot', 'kotem', 'kocie'],
      itemIds: ['v_widziec', 'noun_kot'],
      note: 'Отрицание + одушевлённый мужской род в Dopełniacz → kota.',
    },
  ];

  for (const ni of negItems) {
    pool.push({
      pl: ni.pl,
      ru: ni.ru,
      phoneticRu: ni.pl.toLowerCase(),
      skill: 'cases',
      tier: 5.8,
      itemIds: ni.itemIds,
      grammarTag: 'Отрицание → Dopełniacz',
      aiNote: ni.note,
      cloze: {
        before: ni.before,
        after: ni.after,
        answer: ni.answer,
        distractors: ni.distractors,
      },
    });
  }

  // 4. Narzędnik (być + kim/czym, transport, z + kim/czym) Tier 5.5 - 7.5
  const narzednikItems = [
    {
      pl: 'Jestem programistą i pracuję w Warszawie.',
      ru: 'Я программист и работаю в Варшаве.',
      before: 'Jestem',
      after: 'i pracuję w Warszawie.',
      answer: 'programistą',
      distractors: ['programista', 'programistę', 'programisty'],
      itemIds: ['v_byc', 'noun_programista', 'noun_warszawa'],
      note: 'После глагола «być» (быть кем-то) в польском ВСЕГДА обязателен творительный падеж (Narzędnik): Jestem programistą (не programista!).',
    },
    {
      pl: 'Mój brat jest dobrym lekarzem.',
      ru: 'Мой брат — хороший врач.',
      before: 'Mój brat jest dobrym',
      after: '.',
      answer: 'lekarzem',
      distractors: ['lekarz', 'lekarza', 'lekarzu'],
      itemIds: ['noun_brat', 'v_byc', 'adj_dobry', 'noun_lekarz'],
      note: 'Профессия после «jest» ставится в Narzędnik: dobrym lekarzem.',
    },
    {
      pl: 'Jadę pociągiem do Krakowa.',
      ru: 'Я еду на поезде в Краков.',
      before: 'Jadę',
      after: 'do Krakowa.',
      answer: 'pociągiem',
      distractors: ['pociąg', 'pociągu', 'pociąga'],
      itemIds: ['noun_pociag', 'noun_krakow'],
      note: 'Транспорт без предлога ставится в Narzędnik: jechać pociągiem = ехать на поезде.',
    },
    {
      pl: 'Piję gorącą herbatę z mlekiem.',
      ru: 'Я пью горячий чай с молоком.',
      before: 'Piję gorącą herbatę z',
      after: '.',
      answer: 'mlekiem',
      distractors: ['mleko', 'mleka', 'mleku'],
      itemIds: ['v_pic', 'adj_goracy', 'noun_herbata', 'noun_mleko'],
      note: 'Предлог «z» («с») требует Narzędnik → z mlekiem.',
    },
  ];

  for (const nItem of narzednikItems) {
    pool.push({
      pl: nItem.pl,
      ru: nItem.ru,
      phoneticRu: nItem.pl.toLowerCase(),
      skill: 'cases',
      tier: 6.5,
      itemIds: nItem.itemIds,
      grammarTag: 'Narzędnik (Творительный)',
      aiNote: nItem.note,
      cloze: {
        before: nItem.before,
        after: nItem.after,
        answer: nItem.answer,
        distractors: nItem.distractors,
      },
    });
  }

  // 5. False Friends Context Sentences (Tier 3.5 - 7)
  const ffSentences: CombinatorialSentence[] = [
    {
      pl: 'Idę do sklepu po świeże owoce.',
      ru: 'Я иду в магазин за свежими фруктами.',
      phoneticRu: 'идэ до склепу по щвежэ овоцэ',
      skill: 'false_friends',
      tier: 4.2,
      itemIds: ['ff_sklep', 'ff_owoce', 'adj_swiezy'],
      grammarTag: 'Двойная ловушка: sklep + owoce',
      aiNote: 'Осторожно! «Sklep» = магазин (не склеп), а «owoce» = фрукты (не овощи)!',
      isFalseFriendTrap: true,
      cloze: {
        before: 'Idę do',
        after: 'po świeże owoce.',
        answer: 'sklepu',
        distractors: ['dworca', 'domu', 'lustra'],
      },
    },
    {
      pl: 'W pokoju leży piękny dywan i stoi krzesło.',
      ru: 'В комнате лежит красивый ковёр и стоит стул.',
      phoneticRu: 'в покою лежы пенкны дыван и стои кшэсуо',
      skill: 'false_friends',
      tier: 5.2,
      itemIds: ['ff_dywan', 'ff_krzeslo', 'adj_piekny'],
      grammarTag: 'Ложные друзья: dywan + krzesło',
      aiNote: '«Dywan» — это ковёр (не диван!), а «krzesło» — это обычный стул (не кресло!).',
      isFalseFriendTrap: true,
    },
    {
      pl: 'Przepraszam, zapomniałem o naszym spotkaniu.',
      ru: 'Извините, я забыл о нашей встрече.',
      phoneticRu: 'пшэпрашам, запомняуэм о нашым спотканю',
      skill: 'false_friends',
      tier: 6.2,
      itemIds: ['ff_zapomniec', 'w_przepraszam'],
      grammarTag: 'Ложный друг: zapomnieć',
      aiNote: 'Глагол «zapomnieć» означает «ЗАБЫТЬ», а не «запомнить» (запомнить = zapamiętać)!',
      isFalseFriendTrap: true,
    },
    {
      pl: 'Szukam nowego mieszkania w Krakowie.',
      ru: 'Я ищу новую квартиру в Кракове.',
      phoneticRu: 'шукам новэго мешканя в Кракове',
      skill: 'false_friends',
      tier: 5.5,
      itemIds: ['v_szukac', 'noun_krakow'],
      grammarTag: 'Ложный друг: szukać + Dopełniacz',
      aiNote: '«Szukać» = искать (требует Dopełniacz: nowego mieszkania), а «mieszkanie» = квартира.',
      isFalseFriendTrap: true,
    },
    {
      pl: 'Patrzę w lustro i widzę zmęczoną twarz.',
      ru: 'Я смотрю в зеркало и вижу уставшее лицо.',
      phoneticRu: 'патшэ в люстро и видзэ змэнчонау тфаж',
      skill: 'false_friends',
      tier: 6.0,
      itemIds: ['ff_lustro', 'v_widziec'],
      grammarTag: 'Ложный друг: lustro',
      aiNote: '«Lustro» — это зеркало (люстра = żyrandol), а «twarz» — лицо!',
      isFalseFriendTrap: true,
    },
  ];
  pool.push(...ffSentences);

  // 6. B1-B2 Complex Syntax & Idioms (Tier 7.5 - 10)
  const advSentences: CombinatorialSentence[] = [
    {
      pl: 'Uczę się języka polskiego, ponieważ planuję mieszkać w Polsce.',
      ru: 'Я учу польский язык, потому что планирую жить в Польше.',
      phoneticRu: 'учэ щен ензыка польскего, поневаж плануе мешкачь в Польсцэ',
      skill: 'syntax',
      tier: 7.8,
      itemIds: ['noun_jezyk_polski', 'id_poniewaz', 'v_mieszkac', 'noun_polska'],
      grammarTag: 'Сложноподчинённое предложение + Miejscownik',
      aiNote: 'Союз «ponieważ» (потому что) соединяет части предложения; «w Polsce» — предложный падеж (чередование k → c).',
    },
    {
      pl: 'Mimo że polskie przypadki są trudne, bardzo lubię ten język.',
      ru: 'Несмотря на то что польские падежи сложные, мне очень нравится этот язык.',
      phoneticRu: 'мимо жэ польске пшыпадки сон труднэ, бардзо любе тэн ензык',
      skill: 'syntax',
      tier: 8.5,
      itemIds: ['id_mimo_ze', 'adj_trudny', 'v_lubic'],
      grammarTag: 'Уступка (mimo że)',
      aiNote: 'Конструкция «mimo że» вводит придаточное уступки («несмотря на то что»).',
    },
    {
      pl: 'Wczoraj czytałem ciekawą książkę o historii Warszawy.',
      ru: 'Вчера я читал интересную книгу об истории Варшавы.',
      phoneticRu: 'фчорай чытауэм чекавон кщёнжкэ о хистории Варшавы',
      skill: 'verbs',
      tier: 7.4,
      itemIds: ['adv_wczoraj', 'v_czytac', 'adj_ciekawy', 'noun_ksiazka', 'noun_warszawa'],
      grammarTag: 'Прошедшее время (Czas przeszły)',
      aiNote: 'В мужском роде прошедшего времени глагол получает окончание -łem: czytałem.',
    },
    {
      pl: 'Z góry dziękuję za szybką odpowiedź i trzymam kciuki!',
      ru: 'Заранее спасибо за быстрый ответ и держу кулаки!',
      phoneticRu: 'з гуры дженькуе за шыбкон одповеджь и тшымам кчюки',
      skill: 'idioms_advanced',
      tier: 8.8,
      itemIds: ['id_z_gory_dziekuje', 'id_trzymam_kciuki'],
      grammarTag: 'Идиоматика и деловой стиль',
      aiNote: '«Z góry dziękuję» — стандартная вежливая формула в польской переписке, «trzymam kciuki» — пожелание удачи.',
    },
    {
      pl: 'Weź parasol na wszelki wypadek, bo może padać deszcz.',
      ru: 'Возьми зонт на всякий случай, потому что может пойти дождь.',
      phoneticRu: 'вэзь парасоль на фшэльки выпадэк, бо можэ падачь дэщч',
      skill: 'idioms_advanced',
      tier: 9.2,
      itemIds: ['id_na_wszelki'],
      grammarTag: 'Разговорная речь B1–B2',
      aiNote: '«Na wszelki wypadek» — на всякий случай; «bo» — разговорный союз «потому что».',
    },
  ];
  pool.push(...advSentences);

  return pool;
}

const SENTENCE_POOL = buildSentencePool();

function shouldShowPhonetics(state: LearnerAIState, tier: number): boolean {
  if (state.showPhoneticsOverride === 'always') return true;
  if (state.showPhoneticsOverride === 'off') return false;
  return tier <= 3.6;
}

function makeVocabularyChoice(item: typeof LEXICON[number], state: LearnerAIState, reverse: boolean): Exercise {
  const showPhonetics = shouldShowPhonetics(state, item.tier);

  // Pick 3 smart distractors from LEXICON
  const sameSkillOrClose = LEXICON.filter(
    (other) => other.id !== item.id && Math.abs(other.tier - item.tier) <= 3,
  );
  const distractors: string[] = [];

  if (!reverse && item.isFalseFriend && item.falseFriendTrapRu) {
    // Include the Russian false-friend trap as a deliberate AI distractor!
    distractors.push(item.falseFriendTrapRu);
  }

  for (const cand of shuffle(sameSkillOrClose)) {
    const val = reverse ? cand.pl : cand.ru;
    if (val !== (reverse ? item.pl : item.ru) && !distractors.includes(val)) {
      distractors.push(val);
    }
    if (distractors.length >= 3) break;
  }

  const correctAnswer = reverse ? item.pl : item.ru;
  const options = shuffle([correctAnswer, ...distractors.slice(0, 3)]);

  return {
    id: nextId('ex'),
    type: 'multiple_choice',
    skill: item.skill,
    tier: item.tier,
    promptRu: reverse ? 'Выберите правильный польский перевод:' : 'Как переводится это польское слово?',
    questionPl: reverse ? undefined : item.pl,
    questionRu: reverse ? item.ru : undefined,
    phoneticHint: !reverse && showPhonetics ? `[${item.phoneticRu}]` : undefined,
    audioTextPl: item.pl,
    options,
    correctAnswer,
    itemIds: [item.id],
    isFalseFriendTrap: item.isFalseFriend,
    grammarTag: item.isFalseFriend ? 'Ловушка «Ложный друг»' : item.category,
    aiNote:
      item.falseFriendNote ??
      `Пример в речи: «${item.examplePl}» (${item.exampleRu}).`,
  };
}

function makeSentenceTiles(sent: CombinatorialSentence): Exercise {
  const cleanPl = sent.pl.replace(/[.,!?]/g, '');
  const words = cleanPl.split(/\s+/);

  // Add 2-3 plausible distractor tiles depending on tier
  const distractorWordsPool = ['nie', 'jest', 'mam', 'dobry', 'czasu', 'sklepu', 'bardzo', 'w', 'do', 'kawę'];
  const extraCount = sent.tier >= 5 ? 3 : 2;
  const extraTiles = shuffle(distractorWordsPool.filter((w) => !words.includes(w))).slice(0, extraCount);

  return {
    id: nextId('ex'),
    type: 'word_tiles',
    skill: sent.skill,
    tier: sent.tier,
    promptRu: 'Соберите перевод на польский из слов:',
    questionRu: sent.ru,
    audioTextPl: sent.pl,
    tiles: shuffle([...words, ...extraTiles]),
    correctAnswer: cleanPl,
    acceptedAnswers: [cleanPl, sent.pl],
    itemIds: sent.itemIds,
    grammarTag: sent.grammarTag,
    isFalseFriendTrap: sent.isFalseFriendTrap,
    aiNote: sent.aiNote,
  };
}

function makeClozeExercise(sent: CombinatorialSentence): Exercise | null {
  if (!sent.cloze) return null;
  const options = shuffle([sent.cloze.answer, ...sent.cloze.distractors.slice(0, 3)]);
  return {
    id: nextId('ex'),
    type: 'cloze_grammar',
    skill: sent.skill,
    tier: sent.tier,
    promptRu: 'Выберите грамматически верную форму:',
    questionRu: sent.ru,
    clozeBefore: sent.cloze.before,
    clozeAfter: sent.cloze.after,
    audioTextPl: sent.pl,
    options,
    correctAnswer: sent.cloze.answer,
    itemIds: sent.itemIds,
    grammarTag: sent.grammarTag,
    isFalseFriendTrap: sent.isFalseFriendTrap,
    aiNote: sent.aiNote,
  };
}

function makeTypingExercise(
  targetPl: string,
  promptRuText: string,
  ruSource: string,
  skill: SkillId,
  tier: number,
  itemIds: string[],
  aiNote: string,
  grammarTag?: string,
): Exercise {
  const clean = targetPl.replace(/[.!?]$/g, '').trim();
  return {
    id: nextId('ex'),
    type: 'type_translation',
    skill,
    tier,
    promptRu: promptRuText,
    questionRu: ruSource,
    audioTextPl: targetPl,
    correctAnswer: clean,
    acceptedAnswers: [clean, targetPl],
    itemIds,
    grammarTag: grammarTag ?? 'Письмо и диакритика',
    aiNote,
  };
}

function makeListeningExercise(
  plText: string,
  ruText: string,
  skill: SkillId,
  tier: number,
  itemIds: string[],
): Exercise {
  const clean = plText.replace(/[.,!?]/g, '').trim();
  const useTiles = tier < 5.5;
  if (useTiles) {
    const words = clean.split(/\s+/);
    const distractors = shuffle(['nie', 'proszę', 'bardzo', 'sklepu', 'czas', 'dziękuję'].filter((w) => !words.includes(w))).slice(0, 2);
    return {
      id: nextId('ex'),
      type: 'listening',
      skill,
      tier,
      promptRu: 'Прослушайте польскую фразу и соберите услышанное:',
      audioTextPl: plText,
      questionRu: `Подсказка перевода: ${ruText}`,
      tiles: shuffle([...words, ...distractors]),
      correctAnswer: clean,
      acceptedAnswers: [clean, plText],
      itemIds,
      grammarTag: 'Аудирование (pl-PL)',
      aiNote: `Полная фраза: «${plText}» — ${ruText}.`,
    };
  }

  return {
    id: nextId('ex'),
    type: 'listening',
    skill,
    tier,
    promptRu: 'Прослушайте и напишите фразу по-польски:',
    audioTextPl: plText,
    questionRu: `Перевод: ${ruText}`,
    correctAnswer: clean,
    acceptedAnswers: [clean, plText],
    itemIds,
    grammarTag: 'Диктант (pl-PL)',
    aiNote: `Правильное написание: «${plText}» (${ruText}).`,
  };
}

function makeMatchPairsExercise(tier: number, skillFocus?: SkillId): Exercise {
  const candidates = LEXICON.filter((item) => {
    const tierOk = Math.abs(item.tier - Math.min(tier, 7)) <= 3;
    if (skillFocus && skillFocus !== 'syntax' && skillFocus !== 'cases') {
      return tierOk && (item.skill === skillFocus || item.tier <= tier + 1);
    }
    return tierOk;
  });
  const picked = shuffle(candidates.length >= 5 ? candidates : LEXICON).slice(0, 5);
  const pairs: MatchPair[] = picked.map((p) => ({
    id: p.id,
    pl: p.pl,
    ru: p.ru,
    phoneticRu: p.phoneticRu,
  }));

  return {
    id: nextId('ex'),
    type: 'match_pairs',
    skill: skillFocus ?? 'vocabulary_basic',
    tier,
    promptRu: 'Соедините польские слова с русским переводом:',
    pairs,
    correctAnswer: 'MATCH_ALL',
    itemIds: picked.map((p) => p.id),
    grammarTag: 'Ассоциативная память',
    aiNote: 'Быстрое сопоставление укрепляет нейронную связь между польской орфографией и значением.',
  };
}

export function generateAdaptiveExercise(
  state: LearnerAIState,
  targetTier: number,
  skillFocus?: SkillId,
  usedTypes: ExerciseType[] = [],
): Exercise {
  const clampedTier = Math.max(1, Math.min(10, targetTier));

  // Filter lexicon & sentences near targetTier (and optionally skillFocus)
  const lexCandidates = LEXICON.filter((item) => {
    const dist = Math.abs(item.tier - clampedTier);
    const skillMatch = !skillFocus || item.skill === skillFocus;
    return (skillMatch && dist <= 3.5) || dist <= 2;
  });

  // Prioritize overdue or weak items from AI memory!
  const now = Date.now();
  const scoredLex = lexCandidates.map((item) => {
    const mem = state.itemMemory[item.id];
    let weight = 1.0;
    if (!mem) weight += 1.5; // unseen word
    else {
      if (mem.nextReviewAt <= now) weight += 2.2; // due for review
      if (mem.strength < 0.6) weight += 2.0; // weak item
      if (mem.mistakes > 0) weight += mem.mistakes * 0.5;
    }
    if (skillFocus && item.skill === skillFocus) weight += 2.5;
    return { item, weight: weight * (0.6 + Math.random() * 0.8) };
  });
  scoredLex.sort((a, b) => b.weight - a.weight);
  const chosenLex = scoredLex[0]?.item ?? pickRandom(LEXICON);

  const sentCandidates = SENTENCE_POOL.filter((s) => {
    const dist = Math.abs(s.tier - clampedTier);
    if (skillFocus) {
      return (s.skill === skillFocus && dist <= 3.5) || dist <= 2.2;
    }
    return dist <= 2.5;
  });
  const chosenSent =
    sentCandidates.length > 0 ? pickRandom(sentCandidates) : pickRandom(SENTENCE_POOL);

  // Pick exercise type dynamically based on tier + avoiding repeating same type back-to-back
  const lastType = usedTypes[usedTypes.length - 1];
  const candidateTypes: ExerciseType[] = [];

  if (clampedTier < 2.2) {
    candidateTypes.push('multiple_choice', 'multiple_choice', 'match_pairs', 'listening', 'word_tiles');
  } else if (clampedTier < 4.5) {
    candidateTypes.push('multiple_choice', 'word_tiles', 'cloze_grammar', 'match_pairs', 'listening', 'type_translation');
  } else if (clampedTier < 7.0) {
    candidateTypes.push('word_tiles', 'cloze_grammar', 'type_translation', 'multiple_choice', 'listening', 'match_pairs');
  } else {
    candidateTypes.push('type_translation', 'cloze_grammar', 'word_tiles', 'listening', 'multiple_choice');
  }

  const filteredTypes = candidateTypes.filter((t) => t !== lastType);
  const chosenType = pickRandom(filteredTypes.length > 0 ? filteredTypes : candidateTypes);

  if (chosenType === 'match_pairs') {
    return makeMatchPairsExercise(clampedTier, skillFocus);
  }

  if (chosenType === 'cloze_grammar') {
    const withCloze = sentCandidates.filter((s) => s.cloze);
    const targetCloze = withCloze.length > 0 ? pickRandom(withCloze) : SENTENCE_POOL.find((s) => s.cloze)!;
    const ex = makeClozeExercise(targetCloze);
    if (ex) return ex;
  }

  if (chosenType === 'word_tiles') {
    return makeSentenceTiles(chosenSent);
  }

  if (chosenType === 'type_translation') {
    if (clampedTier < 4.2) {
      return makeTypingExercise(
        chosenLex.pl,
        'Напишите это слово по-польски (используйте панель диакритики):',
        chosenLex.ru,
        chosenLex.skill,
        chosenLex.tier,
        [chosenLex.id],
        chosenLex.falseFriendNote ?? `Обратите внимание на написание: «${chosenLex.pl}» [${chosenLex.phoneticRu}].`,
      );
    }
    return makeTypingExercise(
      chosenSent.pl,
      'Переведите предложение на польский:',
      chosenSent.ru,
      chosenSent.skill,
      chosenSent.tier,
      chosenSent.itemIds,
      chosenSent.aiNote,
      chosenSent.grammarTag,
    );
  }

  if (chosenType === 'listening') {
    if (clampedTier < 3.5) {
      return makeListeningExercise(
        chosenLex.pl,
        chosenLex.ru,
        chosenLex.skill,
        chosenLex.tier,
        [chosenLex.id],
      );
    }
    return makeListeningExercise(
      chosenSent.pl,
      chosenSent.ru,
      chosenSent.skill,
      chosenSent.tier,
      chosenSent.itemIds,
    );
  }

  // multiple_choice (sometimes word, sometimes full phrase/verb)
  if (clampedTier >= 4.0 && Math.random() < 0.4) {
    const distractors = shuffle(SENTENCE_POOL.filter((s) => s.pl !== chosenSent.pl))
      .slice(0, 3)
      .map((s) => s.ru);
    return {
      id: nextId('ex'),
      type: 'multiple_choice',
      skill: chosenSent.skill,
      tier: chosenSent.tier,
      promptRu: 'Выберите точный перевод польского предложения:',
      questionPl: chosenSent.pl,
      audioTextPl: chosenSent.pl,
      options: shuffle([chosenSent.ru, ...distractors]),
      correctAnswer: chosenSent.ru,
      itemIds: chosenSent.itemIds,
      grammarTag: chosenSent.grammarTag,
      isFalseFriendTrap: chosenSent.isFalseFriendTrap,
      aiNote: chosenSent.aiNote,
    };
  }

  return makeVocabularyChoice(chosenLex, state, Math.random() < 0.45);
}

export function generatePlacementQuestion(stepTier: number, usedIds: string[]): Exercise {
  const sents = SENTENCE_POOL.filter(
    (s) => Math.abs(s.tier - stepTier) <= 1.6 && !usedIds.includes(s.pl),
  );
  if (stepTier >= 3.5 && sents.length > 0) {
    const s = pickRandom(sents);
    if (s.cloze && stepTier >= 5) {
      return makeClozeExercise(s)!;
    }
    const distractors = shuffle(SENTENCE_POOL.filter((o) => o.pl !== s.pl))
      .slice(0, 3)
      .map((o) => o.ru);
    return {
      id: nextId('plm'),
      type: 'multiple_choice',
      skill: s.skill,
      tier: s.tier,
      promptRu: `ИИ-калибровка (Уровень ${stepTier.toFixed(0)}): переведите фразу`,
      questionPl: s.pl,
      audioTextPl: s.pl,
      options: shuffle([s.ru, ...distractors]),
      correctAnswer: s.ru,
      itemIds: s.itemIds,
      grammarTag: s.grammarTag,
      aiNote: s.aiNote,
    };
  }

  const lex = LEXICON.filter(
    (l) => Math.abs(l.tier - stepTier) <= 1.5 && !usedIds.includes(l.id),
  );
  const chosen = lex.length > 0 ? pickRandom(lex) : pickRandom(LEXICON);
  const distractors = shuffle(LEXICON.filter((o) => o.id !== chosen.id))
    .slice(0, 3)
    .map((o) => o.ru);
  return {
    id: nextId('plm'),
    type: 'multiple_choice',
    skill: chosen.skill,
    tier: chosen.tier,
    promptRu: `ИИ-калибровка (Уровень ${stepTier.toFixed(0)}): выберите перевод`,
    questionPl: chosen.pl,
    audioTextPl: chosen.pl,
    options: shuffle([chosen.ru, ...distractors]),
    correctAnswer: chosen.ru,
    itemIds: [chosen.id],
    grammarTag: chosen.category,
    aiNote: chosen.falseFriendNote ?? `${chosen.pl} = ${chosen.ru}`,
  };
}
