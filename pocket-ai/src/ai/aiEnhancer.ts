import { EnhancerConfig, FeedbackRule, KnowledgeNote } from '../types/ai';
import { MiniTransformer } from './miniTransformer';

const STOP_WORDS = new Set([
  'и', 'в', 'на', 'с', 'по', 'к', 'из', 'у', 'за', 'о', 'об', 'от', 'до', 'для', 'не', 'но', 'а', 'как', 'что', 'это',
  'ли', 'бы', 'же', 'то', 'я', 'ты', 'он', 'она', 'мы', 'вы', 'они', 'мой', 'твой', 'the', 'a', 'an', 'is', 'are', 'to',
]);

function tokenizeWords(text: string): string[] {
  return text
    .toLowerCase()
    .replace(/[^a-zа-яё0-9\s]/gi, ' ')
    .split(/\s+/)
    .filter((w) => w.length >= 2 && !STOP_WORDS.has(w));
}

function charTrigrams(text: string): Set<string> {
  const clean = text.toLowerCase().replace(/\s+/g, ' ').trim();
  const grams = new Set<string>();
  for (let i = 0; i <= clean.length - 3; i++) {
    grams.add(clean.slice(i, i + 3));
  }
  return grams;
}

function similarityScore(query: string, docText: string): number {
  const qWords = tokenizeWords(query);
  const dLower = docText.toLowerCase();
  let wordHits = 0;
  for (const w of qWords) {
    const stem = w.length > 4 ? w.slice(0, w.length - 2) : w;
    if (dLower.includes(stem)) wordHits += 1;
  }
  const wordScore = qWords.length > 0 ? wordHits / qWords.length : 0;

  const qGrams = charTrigrams(query);
  const dGrams = charTrigrams(docText);
  let overlap = 0;
  qGrams.forEach((g) => {
    if (dGrams.has(g)) overlap += 1;
  });
  const jaccard = qGrams.size + dGrams.size - overlap > 0 ? overlap / (qGrams.size + dGrams.size - overlap) : 0;

  return wordScore * 0.72 + jaccard * 0.28;
}

export function retrieveRelevantNotes(
  query: string,
  notes: KnowledgeNote[],
  topK = 2,
): KnowledgeNote[] {
  if (!notes.length) return [];
  const scored = notes
    .map((note) => ({
      note,
      score: similarityScore(query, `${note.title} ${note.content}`),
    }))
    .filter((x) => x.score >= 0.12)
    .sort((a, b) => b.score - a.score);

  return scored.slice(0, topK).map((x) => x.note);
}

export function retrieveMatchingFeedback(
  query: string,
  rules: FeedbackRule[],
  topK = 2,
): FeedbackRule[] {
  if (!rules.length) return [];
  return rules
    .map((rule) => ({ rule, score: similarityScore(query, rule.prompt) }))
    .filter((x) => x.score >= 0.14)
    .sort((a, b) => b.score - a.score)
    .slice(0, topK)
    .map((x) => x.rule);
}

export function buildEnhancedMessages(
  userQuery: string,
  history: Array<{ role: 'user' | 'assistant'; content: string }>,
  notes: KnowledgeNote[],
  rules: FeedbackRule[],
  cfg: EnhancerConfig,
) {
  const ragHits = cfg.useRag ? retrieveRelevantNotes(userQuery, notes, 2) : [];
  const adapterHits = cfg.useAdapter ? retrieveMatchingFeedback(userQuery, rules, 2) : [];

  const systemParts: string[] = [cfg.systemPrompt];

  if (ragHits.length > 0) {
    systemParts.push(
      'Локальная база знаний (используй эти проверенные факты):\n' +
        ragHits.map((n) => `- [${n.title}]: ${n.content}`).join('\n'),
    );
  }

  if (adapterHits.length > 0) {
    systemParts.push(
      'Предпочтения пользователя и исправленные примеры (следуй этому стилю):\n' +
        adapterHits
          .map((r) => `Запрос: "${r.prompt}" -> Эталонный ответ: "${r.idealReply}"`)
          .join('\n'),
    );
  }

  if (cfg.deepThink) {
    systemParts.push(
      'Перед финальным ответом кратко проверь логику и вычисления внутри тега <think>...</think>, затем дай точный, ясный ответ на русском языке.',
    );
  }

  const recentHistory = history.slice(-6).map((m) => ({
    role: m.role,
    content: m.content,
  }));

  const messages: Array<{ role: 'system' | 'user' | 'assistant'; content: string }> = [
    { role: 'system', content: systemParts.join('\n\n') },
    ...recentHistory,
    { role: 'user', content: userQuery },
  ];

  return {
    messages,
    ragTitles: ragHits.map((n) => n.title),
    usedAdapterCount: adapterHits.length,
  };
}

export function parseThoughtAndAnswer(raw: string): { thinking?: string; answer: string } {
  const match = raw.match(/<think>([\s\S]*?)<\/think>([\s\S]*)/i);
  if (match) {
    return {
      thinking: match[1].trim(),
      answer: match[2].trim() || raw.replace(/<\/?think>/gi, '').trim(),
    };
  }
  const openOnly = raw.match(/<think>([\s\S]*)/i);
  if (openOnly && !raw.includes('</think>')) {
    return {
      thinking: openOnly[1].trim(),
      answer: 'ИИ обдумывает ответ...',
    };
  }
  return { answer: raw.trim() };
}

/**
 * Lightweight local arithmetic / word-problem solver used to verify math or assist
 * the built-in PocketGPT-Mini model before a full GGUF Gemma model is downloaded.
 */
function trySolveMathOrLogic(query: string): { steps: string; result: string } | null {
  const lower = query.toLowerCase();

  // Direct expression: e.g. "25 * 4 + 12"
  const exprMatch = query.match(/(-?\d+(?:\.\d+)?(?:\s*[+\-*/]\s*-?\d+(?:\.\d+)?)+)/);
  if (exprMatch) {
    const clean = exprMatch[1].replace(/[^0-9+\-*/.\s]/g, '');
    try {
      const val = Function(`"use strict"; return (${clean});`)() as number;
      if (Number.isFinite(val)) {
        return {
          steps: `Вычисляю выражение ${clean} = ${val}.`,
          result: `Результат: **${Number(val.toFixed(4))}**`,
        };
      }
    } catch {
      // ignore
    }
  }

  // Classic word arithmetic ("было 3 ... отдал 1 ... купил 4")
  const nums = Array.from(lower.matchAll(/(\d+(?:[.,]\d+)?)/g)).map((m) =>
    Number.parseFloat(m[1].replace(',', '.')),
  );
  if (
    nums.length >= 2 &&
    /(сколько|осталось|стало|всего|яблок|руб|денег|плюс|минус)/.test(lower)
  ) {
    const clauses = lower.split(/(?:[,.;!?]|\s+и\s+|\s+а\s+|\s+потом\s+|\s+затем\s+)/);
    let total: number | null = null;
    const trace: string[] = [];

    for (const cl of clauses) {
      const m = cl.match(/(\d+(?:[.,]\d+)?)/);
      if (!m) continue;
      const n = Number.parseFloat(m[1].replace(',', '.'));
      if (total === null) {
        total = n;
        trace.push(`Начало: ${n}`);
      } else if (/(отдал|потратил|ушло|съел|потерял|вычесть|минус|продал|убрал)/.test(cl)) {
        total -= n;
        trace.push(`- ${n} → ${total}`);
      } else if (/(купил|дали|добав|прибав|получил|нашёл|нашел|плюс|еще|ещё)/.test(cl)) {
        total += n;
        trace.push(`+ ${n} → ${total}`);
      }
    }

    if (total !== null && trace.length >= 2) {
      return {
        steps: trace.join('; '),
        result: `Ответ: **${total}** (${trace.join(', ')}).`,
      };
    }
  }

  return null;
}

function isGibberishInput(text: string): boolean {
  const clean = text.trim().toLowerCase();
  if (clean.length < 3) return false;
  if (/^\d+$/.test(clean)) return false;

  const words = clean.split(/\s+/).filter(Boolean);
  if (words.length === 1 && clean.length >= 6) {
    const letters = clean.replace(/[^a-zа-яё]/gi, '');
    if (letters.length >= 6) {
      const vowels = (letters.match(/[аеёиоуыэюяaeiouy]/gi) ?? []).length;
      const ratio = vowels / letters.length;
      const hasKnownWord = /(привет|пока|спасибо|помоги|сделай|расскажи|сколько|gemma|qwen|llama|модел|нейро|память|план|код|тест)/.test(
        letters,
      );
      if (!hasKnownWord && (ratio < 0.2 || ratio > 0.72 || /(.)\1{3,}/.test(letters))) {
        return true;
      }
      // Random keyboard rows like "кцупйукп" / "фывапрол" / "qwertyui"
      if (!hasKnownWord && !/[аеёиоуыэюя][бвгджзйклмнпрстфхцчшщ][аеёиоуыэюя]/i.test(letters)) {
        return true;
      }
    }
  }
  return false;
}

function tryBuiltInConversation(query: string, personaId: string): { steps: string; answer: string } | null {
  const q = query.trim().toLowerCase();

  // Percentage calculation ("15% от 200")
  const pctMatch = q.match(/(\d+(?:[.,]\d+)?)\s*%\s*(?:от|of)\s*(\d+(?:[.,]\d+)?)/);
  if (pctMatch) {
    const p = Number.parseFloat(pctMatch[1].replace(',', '.'));
    const base = Number.parseFloat(pctMatch[2].replace(',', '.'));
    const ans = Number(((p / 100) * base).toFixed(4));
    return {
      steps: `Считаю ${p}% от ${base}: (${p} / 100) * ${base} = ${ans}.`,
      answer: `${p}% от ${base} = **${ans}**.`,
    };
  }

  // Greetings & identity
  if (/^(привет|приветик|здравствуй|хай|ку|hello|hi|hey)\b/i.test(q) && q.length < 30) {
    return {
      steps: 'Распознано приветствие. Представляюсь кратко и по делу.',
      answer:
        'Привет! Я **PocketAI** — локальный ИИ-ассистент, работаю прямо на твоём устройстве без облака и API-ключей.\n\nМогу:\n• Отвечать с пошаговой проверкой логики и по твоей **RAG-базе знаний**;\n• Запускать скачанные модели **Gemma 3 / Qwen 2.5 / SmolLM2** во вкладке **«Модели»**;\n• Обучать свою мини-нейросеть во вкладке **«Своя ИИ»**.',
    };
  }

  if (/(кто ты|ты кто|что ты такое|как тебя зовут)/.test(q)) {
    return {
      steps: 'Запрос о моей личности и назначении.',
      answer:
        'Я **PocketAI Studio** — твой персональный локальный ИИ. Сейчас активен встроенный гибридный режим (**PocketGPT-Mini + RAG + логический солвер**).\n\nЕсли хочешь полноценную большую LLM уровня GPT-класса прямо на телефоне, открой вкладку **«Модели»** и скачай **Gemma 3 270M** или **Gemma 3 1B** — они загрузятся в память устройства и будут работать офлайн.',
    };
  }

  if (/(как ты сделан|из чего ты|на чем ты|как устроен|как ты работаешь)/.test(q)) {
    return {
      steps: 'Запрос об архитектуре PocketAI Studio.',
      answer:
        'Я устроен из трёх локальных слоёв:\n1. **Движок `llama.cpp` (WebAssembly)** — запускает реальные `.gguf` модели (**Gemma 3, Qwen 2.5, Llama 3.2**) прямо в браузере и Android APK.\n2. **Улучшитель ответа (`Think → RAG → Адаптер`)** — проверяет вычисления, подтягивает факты из твоих заметок и учитывает твои оценки (лайки/исправления).\n3. **Своя нейросеть Transformer GPT** — модель на `Float32Array` с механизмом внимания и оптимизатором AdamW, которую можно дообучать во вкладке **«Своя ИИ»**.',
    };
  }

  if (/(как дела|как ты\??$|что нового)/.test(q)) {
    return {
      steps: 'Неформальный вопрос о состоянии.',
      answer: 'Всё отлично! Работаю локально, память и адаптер на месте. Чем помочь?',
    };
  }

  if (/(как скачать|где gemma|какую модель|какая модель лучше|установить gemma)/.test(q)) {
    return {
      steps: 'Подсказка по выбору и загрузке GGUF-моделей.',
      answer:
        'Чтобы запустить настоящую **Gemma** на телефоне или в браузере:\n1. Перейди во вкладку **«Модели»**.\n2. Нажми **«Скачать»** напротив **Gemma 3 270M IT (~250 МБ)** (быстрая) или **Gemma 3 1B IT (~806 МБ)** (самая умная).\n3. Модель сохранится в локальном кэше `OPFS` и больше не потребует интернета.',
    };
  }

  if (/(что ты умеешь|функции|возможности|помощь|help)/.test(q)) {
    return {
      steps: 'Перечисление возможностей приложения.',
      answer:
        'Вот что я умею прямо сейчас:\n• Решать примеры и текстовые задачки (с проверкой шагов);\n• Искать ответы в твоей **Базе знаний (RAG)** во вкладке «Улучшить»;\n• Запоминать твои исправления (нажми дизлайк под любым ответом и введи эталон);\n• Запускать **Gemma 3 / Qwen / SmolLM2** из вкладки «Модели»;\n• Обучать твою собственную Transformer-нейросеть на любых текстах во вкладке «Своя ИИ».',
    };
  }

  if (/(который час|сколько времени|какое сегодня число|дата|время)/.test(q)) {
    const now = new Date();
    return {
      steps: 'Читаю системное время устройства.',
      answer: `Сейчас **${now.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}**, дата: **${now.toLocaleDateString('ru-RU')}**.`,
    };
  }

  if (/(план|чек-лист|чеклист|как дообучить|улучшить модель|улучшить нейросеть)/.test(q)) {
    return {
      steps: 'Формирую структурированный план из 3 шагов.',
      answer:
        personaId === 'concise'
          ? '1. Скачай Gemma 3 270M/1B в «Модели».\n2. Добавь свои факты в RAG («Улучшить»).\n3. Исправляй ошибки через адаптер и включи «Глубокое мышление».'
          : 'Вот чёткий план, как выжать максимум из локальной модели:\n1. **Включи «Глубокое мышление»** — модель начнёт проверять логику перед ответом.\n2. **Заполни RAG-память** своими заметками и правилами (вкладка «Улучшить»).\n3. **Обучи адаптер на исправлениях**: поправь 3–5 ответов вручную через иконку оценки, чтобы ИИ перенял твой стиль.',
    };
  }

  if (/(код|функци|javascript|typescript|python|пример кода|совет по коду)/.test(q)) {
    return {
      steps: 'Подбираю практический шаблон и рекомендации по коду.',
      answer:
        'Для чистого кода держи 3 базовых правила:\n1. Разбивай логику на небольшие функции с понятными типами.\n2. Проверяй пустые входные данные и ошибки на границах.\n3. Для вычислений в JS/TS используй `Float32Array`.\n\nЕсли хочешь, чтобы я писал и отлаживал сложный код под твой проект — скачай **Qwen 2.5 0.5B** или **Gemma 3 1B** во вкладке **«Модели»**!',
    };
  }

  return null;
}

/**
 * Built-in local hybrid response generator when running purely on PocketGPT-Mini
 * (before user downloads Gemma 3 / Qwen / SmolLM2 GGUF).
 */
export function generateBuiltInReply(
  userQuery: string,
  miniModel: MiniTransformer,
  notes: KnowledgeNote[],
  rules: FeedbackRule[],
  cfg: EnhancerConfig,
): { answer: string; thinking?: string; ragTitles: string[]; usedAdapterCount: number } {
  const ragHits = cfg.useRag ? retrieveRelevantNotes(userQuery, notes, 2) : [];
  const adapterHits = cfg.useAdapter ? retrieveMatchingFeedback(userQuery, rules, 2) : [];

  const thinkingSteps: string[] = [];
  if (cfg.deepThink) {
    thinkingSteps.push(`1. Анализ запроса: «${userQuery.slice(0, 80)}»`);
    if (ragHits.length > 0) {
      thinkingSteps.push(`2. Найдено фактов в RAG-памяти: ${ragHits.map((n) => n.title).join(', ')}.`);
    }
    if (adapterHits.length > 0) {
      thinkingSteps.push(`3. Применены правила адаптации из оценок (${adapterHits.length}).`);
    }
  }

  // 0. Check for accidental keyboard mash / gibberish
  if (isGibberishInput(userQuery)) {
    thinkingSteps.push('2. Обнаружен случайный набор символов без читаемых слов.');
    return {
      answer:
        'Похоже, сообщение набрано случайно или с опечаткой. Напиши вопрос обычными словами — и я с радостью отвечу!',
      thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
      ragTitles: [],
      usedAdapterCount: 0,
    };
  }

  // 1. Exact/high-confidence user feedback correction takes highest priority
  if (cfg.useAdapter && adapterHits.length > 0) {
    const best = adapterHits[0];
    if (similarityScore(userQuery, best.prompt) >= 0.42) {
      thinkingSteps.push('4. Найден эталонный ответ из твоего дообучения.');
      return {
        answer: best.idealReply,
        thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
        ragTitles: ragHits.map((n) => n.title),
        usedAdapterCount: adapterHits.length,
      };
    }
  }

  // 2. Logic & Arithmetic verifier
  const math = trySolveMathOrLogic(userQuery);
  if (math) {
    thinkingSteps.push(`4. Проверка вычислений: ${math.steps}`);
    return {
      answer: math.result,
      thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
      ragTitles: ragHits.map((n) => n.title),
      usedAdapterCount: adapterHits.length,
    };
  }

  // 3. Built-in conversational intent engine
  const conv = tryBuiltInConversation(userQuery, cfg.personaId);
  if (conv) {
    thinkingSteps.push(`4. ${conv.steps}`);
    return {
      answer: conv.answer,
      thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
      ragTitles: ragHits.map((n) => n.title),
      usedAdapterCount: adapterHits.length,
    };
  }

  // 4. RAG Knowledge Base synthesis
  if (cfg.useRag && ragHits.length > 0) {
    const facts = ragHits.map((n) => `**${n.title}**: ${n.content}`).join('\n\n');
    thinkingSteps.push('4. Сборка ответа на основе локальной базы знаний без выдумок.');
    return {
      answer: `По моей локальной базе знаний:\n\n${facts}`,
      thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
      ragTitles: ragHits.map((n) => n.title),
      usedAdapterCount: adapterHits.length,
    };
  }

  // 5. Honest helpful answer instead of raw char-level gibberish
  thinkingSteps.push(
    `4. Встроенная мини-модель (${miniModel.getParamCount()} весов) не нашла точного факта в локальной базе и не выдумывает ответ.`,
  );

  return {
    answer:
      `По запросу «${userQuery}» у меня пока нет точных знаний во встроенной памяти.\n\nЧтобы я отвечал на любые свободные темы как полноценная нейросеть:\n• Перейди во вкладку **«Модели»** и скачай **Gemma 3 270M** или **Qwen 2.5 0.5B** (работает 100% локально),\n• Либо нажми иконку дизлайка под этим сообщением и задай мне правильный ответ — я запомню его навсегда!`,
    thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
    ragTitles: [],
    usedAdapterCount: adapterHits.length,
  };
}
