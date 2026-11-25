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

  // 1. Exact/high-confidence user feedback correction takes highest priority
  if (cfg.useAdapter && adapterHits.length > 0) {
    const best = adapterHits[0];
    if (similarityScore(userQuery, best.prompt) >= 0.45) {
      thinkingSteps.push('4. Найден эталонный ответ из твоего дообучения.');
      return {
        answer: best.idealReply,
        thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
        ragTitles: ragHits.map((n) => n.title),
        usedAdapterCount: adapterHits.length,
      };
    }
  }

  // 2. Logic & Arithmetic verifier (when deepThink is on or math query)
  const math = trySolveMathOrLogic(userQuery);
  if (math && (cfg.deepThink || /сколько|вычисли|=|\+|-|\*/i.test(userQuery))) {
    thinkingSteps.push(`4. Проверка вычислений: ${math.steps}`);
    return {
      answer: math.result,
      thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
      ragTitles: ragHits.map((n) => n.title),
      usedAdapterCount: adapterHits.length,
    };
  }

  // 3. RAG Knowledge Base synthesis
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

  // 4. Trained MiniTransformer neural generation
  const neuralOut = miniModel.generate(
    `Вопрос: ${userQuery}\nОтвет:`,
    130,
    Math.min(0.85, Math.max(0.2, cfg.temperature)),
    8,
  );
  thinkingSteps.push(`4. Генерация нейросетью PocketGPT-Mini (${miniModel.getParamCount()} весов, шаг ${miniModel.stepCount}).`);

  const cleanNeural =
    neuralOut && neuralOut.length >= 6
      ? neuralOut
      : 'Я работаю на встроенной мини-нейросети PocketGPT-Mini. Чтобы получить полные ответы уровня LLM, скачай Gemma 3 или Qwen 2.5 во вкладке «Модели», либо обучи меня на своих текстах во вкладке «Своя ИИ»!';

  return {
    answer: cleanNeural,
    thinking: cfg.deepThink ? thinkingSteps.join('\n') : undefined,
    ragTitles: [],
    usedAdapterCount: adapterHits.length,
  };
}
