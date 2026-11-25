import { LEXICON, SKILL_META } from '../data/polishLexicon';
import {
  AiThought,
  AnswerEvaluation,
  CefrLevel,
  ErrorKind,
  Exercise,
  ExerciseType,
  ItemMemory,
  LearnerAIState,
  SessionLog,
  SkillId,
} from '../types/learning';
import { generateAdaptiveExercise } from './polishGenerator';

const STORAGE_KEY = 'polski_orzel_ai_state_v1';

export function createInitialAIState(): LearnerAIState {
  return {
    xp: 0,
    gems: 150,
    streakDays: 1,
    hearts: 5,
    maxHearts: 5,
    dailyXp: 0,
    dailyGoalXp: 50,
    lessonsCompleted: 0,
    totalExercises: 0,
    totalCorrect: 0,
    currentTier: 1.5,
    skillMastery: {
      phonetics: 0.22,
      vocabulary_basic: 0.25,
      everyday: 0.15,
      false_friends: 0.12,
      verbs: 0.15,
      cases: 0.10,
      syntax: 0.10,
      idioms_advanced: 0.05,
    },
    skillAttempts: {
      phonetics: 0,
      vocabulary_basic: 0,
      everyday: 0,
      false_friends: 0,
      verbs: 0,
      cases: 0,
      syntax: 0,
      idioms_advanced: 0,
    },
    errorCounts: {
      diacritic_slip: 0,
      false_friend_trap: 0,
      case_error: 0,
      conjugation_error: 0,
      word_order: 0,
      vocabulary_miss: 0,
    },
    itemMemory: {},
    completedNodes: {},
    sessions: [],
    aiThoughts: [
      {
        id: 'init_1',
        timestamp: Date.now(),
        kind: 'insight',
        message:
          'Локальный ИИ готов: начну с лёгкой базовой лексики (Tier 1.5) и фонетических подсказок, постепенно повышая сложность по мере ваших успехов.',
      },
    ],
    placementDone: false,
    soundEnabled: true,
    showPhoneticsOverride: 'auto',
  };
}

export function loadAIState(): LearnerAIState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return createInitialAIState();
    const parsed = JSON.parse(raw) as Partial<LearnerAIState>;
    const base = createInitialAIState();
    return {
      ...base,
      ...parsed,
      skillMastery: { ...base.skillMastery, ...(parsed.skillMastery ?? {}) },
      skillAttempts: { ...base.skillAttempts, ...(parsed.skillAttempts ?? {}) },
      errorCounts: { ...base.errorCounts, ...(parsed.errorCounts ?? {}) },
      itemMemory: parsed.itemMemory ?? {},
      completedNodes: parsed.completedNodes ?? {},
      sessions: parsed.sessions ?? [],
      aiThoughts: parsed.aiThoughts?.length ? parsed.aiThoughts : base.aiThoughts,
    };
  } catch {
    return createInitialAIState();
  }
}

export function saveAIState(state: LearnerAIState): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // ignore storage quota errors
  }
}

export function stripPolishDiacritics(str: string): string {
  const map: Record<string, string> = {
    ą: 'a',
    ć: 'c',
    ę: 'e',
    ł: 'l',
    ń: 'n',
    ó: 'o',
    ś: 's',
    ź: 'z',
    ż: 'z',
    Ą: 'A',
    Ć: 'C',
    Ę: 'E',
    Ł: 'L',
    Ń: 'N',
    Ó: 'O',
    Ś: 'S',
    Ź: 'Z',
    Ż: 'Z',
  };
  return str.replace(/[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]/g, (ch) => map[ch] ?? ch);
}

function normalizeAnswer(str: string): string {
  return str
    .trim()
    .toLowerCase()
    .replace(/[.,!?;:«»"']/g, '')
    .replace(/\s+/g, ' ');
}

export function evaluateAnswer(
  exercise: Exercise,
  userAnswer: string,
  currentTier: number,
): AnswerEvaluation {
  if (exercise.type === 'match_pairs') {
    const ok = userAnswer === 'MATCH_ALL';
    return {
      isCorrect: ok,
      isMinorDiacriticSlip: false,
      feedbackTitle: ok ? 'Świetnie! Все пары найдены!' : 'Не все пары совпали',
      feedbackExplanation: exercise.aiNote,
      correctDisplay: 'Все пары соединены верно',
    };
  }

  const normUser = normalizeAnswer(userAnswer);
  const candidates = exercise.acceptedAnswers?.length
    ? exercise.acceptedAnswers
    : [exercise.correctAnswer];
  const normTargets = candidates.map(normalizeAnswer);

  // 1. Exact match
  if (normTargets.includes(normUser)) {
    return {
      isCorrect: true,
      isMinorDiacriticSlip: false,
      feedbackTitle: pickPraise(),
      feedbackExplanation: exercise.aiNote,
      correctDisplay: exercise.correctAnswer,
    };
  }

  // 2. Check if only Polish diacritics differ (ą, ę, ł, ó, ś, ć, ź, ż, ń)
  const strippedUser = stripPolishDiacritics(normUser);
  const diacriticMatch = normTargets.some((t) => stripPolishDiacritics(t) === strippedUser);
  if (diacriticMatch && normUser.length > 0) {
    // At lower tiers (< 6.0), accept with gentle diacritic warning; at higher tiers, teach strict spelling
    const acceptSlip = currentTier < 6.0;
    return {
      isCorrect: acceptSlip,
      isMinorDiacriticSlip: true,
      errorKind: 'diacritic_slip',
      feedbackTitle: acceptSlip
        ? 'Верно, но следите за польской диакритикой!'
        : 'Почти точно — забыты диакритические знаки!',
      feedbackExplanation: `На уровне Tier ${currentTier.toFixed(1)} ИИ обращает внимание на буквы ą, ę, ł, ó, ś, ć, ź, ż. Правильно: «${exercise.correctAnswer}». ${exercise.aiNote}`,
      correctDisplay: exercise.correctAnswer,
    };
  }

  // 3. Classify mistake for AI feedback
  let errorKind: ErrorKind = 'vocabulary_miss';
  let explanation = exercise.aiNote;

  if (exercise.isFalseFriendTrap) {
    const ffItem = LEXICON.find(
      (l) => exercise.itemIds.includes(l.id) && l.falseFriendTrapRu,
    );
    if (
      ffItem &&
      ffItem.falseFriendTrapRu &&
      normUser.includes(normalizeAnswer(ffItem.falseFriendTrapRu.split(' ')[0]))
    ) {
      errorKind = 'false_friend_trap';
      explanation = `Вы попались в ловушку «ложного друга»! ${ffItem.falseFriendNote}`;
    }
  } else if (exercise.skill === 'cases' || exercise.type === 'cloze_grammar') {
    errorKind = 'case_error';
    explanation = `Ошибка в падеже/окончании. ${exercise.aiNote}`;
  } else if (exercise.skill === 'verbs') {
    errorKind = 'conjugation_error';
    explanation = `Проверьте личное окончание глагола. ${exercise.aiNote}`;
  } else if (exercise.type === 'word_tiles') {
    const userWords = new Set(normUser.split(' '));
    const targetWords = new Set(normTargets[0].split(' '));
    const overlap = [...userWords].filter((w) => targetWords.has(w)).length;
    if (overlap >= Math.max(2, targetWords.size - 1)) {
      errorKind = 'word_order';
      explanation = `Слова верны, но порядок или одно окончание отличаются. ${exercise.aiNote}`;
    }
  }

  return {
    isCorrect: false,
    isMinorDiacriticSlip: false,
    errorKind,
    feedbackTitle: 'Не совсем так — разбор ИИ:',
    feedbackExplanation: explanation,
    correctDisplay: exercise.correctAnswer,
  };
}

function pickPraise(): string {
  const list = [
    'Świetnie! Отлично!',
    'Brawo! Так держать!',
    'Doskonale! Безупречно!',
    'Super! Точно в цель!',
    'Znakomicie! Превосходно!',
  ];
  return list[Math.floor(Math.random() * list.length)];
}

export function computeGlobalMasteryScore(state: LearnerAIState): number {
  const weights: Record<SkillId, number> = {
    phonetics: 0.12,
    vocabulary_basic: 0.15,
    everyday: 0.15,
    false_friends: 0.12,
    verbs: 0.14,
    cases: 0.14,
    syntax: 0.10,
    idioms_advanced: 0.08,
  };
  let weighted = 0;
  (Object.keys(weights) as SkillId[]).forEach((k) => {
    weighted += (state.skillMastery[k] ?? 0) * weights[k];
  });
  return Math.round(weighted * 1000);
}

export function computeCefrLevel(state: LearnerAIState): CefrLevel {
  const score = computeGlobalMasteryScore(state);
  if (score < 160) return 'A0';
  if (score < 320) return 'A1';
  if (score < 460) return 'A1+';
  if (score < 620) return 'A2';
  if (score < 780) return 'B1';
  if (score < 910) return 'B2';
  return 'C1';
}

export function getWeakestSkill(state: LearnerAIState): SkillId {
  const skills = Object.keys(state.skillMastery) as SkillId[];
  // Prioritize skills relevant near current tier
  let weakest: SkillId = 'vocabulary_basic';
  let minVal = Infinity;
  for (const s of skills) {
    if (s === 'idioms_advanced' && state.currentTier < 4.5) continue;
    if (s === 'syntax' && state.currentTier < 3.5) continue;
    const val = state.skillMastery[s];
    if (val < minVal) {
      minVal = val;
      weakest = s;
    }
  }
  return weakest;
}

export function getStrongestSkill(state: LearnerAIState): SkillId {
  const skills = Object.keys(state.skillMastery) as SkillId[];
  let best: SkillId = 'vocabulary_basic';
  let maxVal = -1;
  for (const s of skills) {
    if (state.skillMastery[s] > maxVal) {
      maxVal = state.skillMastery[s];
      best = s;
    }
  }
  return best;
}

export function applyExerciseOutcome(
  prevState: LearnerAIState,
  exercise: Exercise,
  evaluation: AnswerEvaluation,
  responseMs: number,
  isInfiniteMode: boolean,
): LearnerAIState {
  const now = Date.now();
  const next: LearnerAIState = {
    ...prevState,
    skillMastery: { ...prevState.skillMastery },
    skillAttempts: { ...prevState.skillAttempts },
    errorCounts: { ...prevState.errorCounts },
    itemMemory: { ...prevState.itemMemory },
    aiThoughts: [...prevState.aiThoughts],
  };

  next.totalExercises += 1;
  next.skillAttempts[exercise.skill] = (next.skillAttempts[exercise.skill] ?? 0) + 1;

  // Bayesian Knowledge Tracing (BKT) update for the skill
  const pKnown = next.skillMastery[exercise.skill] ?? 0.15;
  const pSlip = 0.08;
  const pGuess = exercise.type === 'multiple_choice' ? 0.25 : 0.08;
  const pLearn = 0.12;

  let posterior: number;
  if (evaluation.isCorrect && !evaluation.isMinorDiacriticSlip) {
    const num = pKnown * (1 - pSlip);
    const den = num + (1 - pKnown) * pGuess;
    posterior = num / den;
    next.totalCorrect += 1;
  } else if (evaluation.isMinorDiacriticSlip && evaluation.isCorrect) {
    posterior = pKnown * 0.98;
    next.totalCorrect += 1;
  } else {
    const num = pKnown * pSlip;
    const den = num + (1 - pKnown) * (1 - pGuess);
    posterior = num / den;
  }

  // Transition step + clamp
  const updatedMastery = Math.max(
    0.04,
    Math.min(0.99, posterior + (1 - posterior) * (evaluation.isCorrect ? pLearn : 0.02)),
  );
  next.skillMastery[exercise.skill] = updatedMastery;

  // Error telemetry
  if (evaluation.errorKind) {
    next.errorCounts[evaluation.errorKind] = (next.errorCounts[evaluation.errorKind] ?? 0) + 1;
  }

  // Update FSRS-inspired Item Memory for each item
  for (const itemId of exercise.itemIds) {
    const cur: ItemMemory = next.itemMemory[itemId] ?? {
      id: itemId,
      strength: 0.25,
      attempts: 0,
      mistakes: 0,
      streak: 0,
      lastSeenAt: now,
      nextReviewAt: now,
      avgResponseMs: responseMs,
    };

    const newAttempts = cur.attempts + 1;
    const newStreak = evaluation.isCorrect ? cur.streak + 1 : 0;
    const newMistakes = evaluation.isCorrect ? cur.mistakes : cur.mistakes + 1;
    const deltaStrength = evaluation.isCorrect
      ? evaluation.isMinorDiacriticSlip
        ? 0.08
        : 0.18
      : -0.2;
    const newStrength = Math.max(0.05, Math.min(1, cur.strength + deltaStrength));

    // Spaced repetition interval in ms (from 2 mins to 7 days)
    const intervalMinutes = evaluation.isCorrect
      ? Math.pow(2.4, Math.min(8, newStreak)) * 3
      : 1;

    next.itemMemory[itemId] = {
      id: itemId,
      strength: newStrength,
      attempts: newAttempts,
      mistakes: newMistakes,
      streak: newStreak,
      lastSeenAt: now,
      nextReviewAt: now + intervalMinutes * 60 * 1000,
      avgResponseMs: Math.round((cur.avgResponseMs * cur.attempts + responseMs) / newAttempts),
    };
  }

  // Adaptive Difficulty Tier adjustment
  let tierDelta = 0;
  if (evaluation.isCorrect && !evaluation.isMinorDiacriticSlip) {
    const speedBonus = responseMs < 5500 ? 0.08 : 0.03;
    const challengeBonus = exercise.tier >= next.currentTier ? 0.12 : 0.05;
    tierDelta = isInfiniteMode ? (speedBonus + challengeBonus) * 1.35 : speedBonus + challengeBonus;
  } else if (!evaluation.isCorrect) {
    tierDelta = isInfiniteMode ? -0.18 : -0.1;
  }

  const prevTier = next.currentTier;
  next.currentTier = Math.max(1.0, Math.min(11.5, Number((prevTier + tierDelta).toFixed(2))));

  // Generate AI diagnostic thought on notable events
  const addThought = (kind: AiThought['kind'], message: string) => {
    next.aiThoughts = [
      { id: `th_${now}_${Math.random()}`, timestamp: now, kind, message },
      ...next.aiThoughts,
    ].slice(0, 18);
  };

  if (Math.floor(next.currentTier) > Math.floor(prevTier)) {
    addThought(
      'boost',
      `Сложность повышена до Tier ${next.currentTier.toFixed(1)}! Локальный ИИ открывает более сложные конструкции и сокращает подсказки.`,
    );
  } else if (evaluation.errorKind === 'false_friend_trap') {
    addThought(
      'warning',
      `Зафиксирована интерференция «ложного друга» (${exercise.grammarTag}). ИИ добавит проверочные повторения этой пары.`,
    );
  } else if (evaluation.errorKind === 'case_error') {
    addThought(
      'support',
      `Обнаружена ошибка в падеже (${SKILL_META.cases.shortLabel}). Следующие задания сфокусируются на окончаниях Biernik/Dopełniacz.`,
    );
  } else if (evaluation.errorKind === 'diacritic_slip') {
    addThought(
      'insight',
      `Отмечен пропуск польских диакритических знаков (ą, ę, ł, ó, ś, ć, ź, ż). Полезно чаще печатать ответы вручную.`,
    );
  }

  // Hearts & XP (Infinite Hearts enabled across all modes)
  if (evaluation.isCorrect) {
    const earned = isInfiniteMode ? 12 : 10;
    next.xp += earned;
    next.dailyXp += earned;
  }
  next.hearts = next.maxHearts;

  return next;
}

export function assembleAdaptiveLesson(
  state: LearnerAIState,
  options: {
    skillFocus?: SkillId;
    targetTier?: number;
    count?: number;
    mode: 'path' | 'review' | 'infinite';
    forceSpeaking?: boolean;
  },
): Exercise[] {
  const count = options.count ?? 7;
  const baseTier = options.targetTier ?? state.currentTier;
  const exercises: Exercise[] = [];
  const usedTypes: ExerciseType[] = [];
  const usedCorrect = new Set<string>();

  for (let i = 0; i < count; i++) {
    // Progressive intra-lesson curve: warm-up -> target -> stretch challenge!
    const intraProgress = count > 1 ? i / (count - 1) : 0.5;
    const stepTier = Math.max(1, baseTier - 0.5 + intraProgress * 1.2);

    // On review / auto mode, inject weakest skill on 30% of questions
    const chosenSkill =
      options.skillFocus ?? (i % 3 === 1 ? getWeakestSkill(state) : undefined);

    let ex = generateAdaptiveExercise(
      state,
      stepTier,
      chosenSkill,
      usedTypes,
      Boolean(options.forceSpeaking),
    );
    let retries = 0;
    while (usedCorrect.has(ex.correctAnswer) && retries < 6) {
      ex = generateAdaptiveExercise(
        state,
        stepTier,
        chosenSkill,
        usedTypes,
        Boolean(options.forceSpeaking),
      );
      retries++;
    }
    usedCorrect.add(ex.correctAnswer);
    usedTypes.push(ex.type);
    exercises.push(ex);
  }

  return exercises;
}

export function recordCompletedSession(
  prevState: LearnerAIState,
  params: {
    mode: SessionLog['mode'];
    unitTitle: string;
    nodeId?: string;
    xpBonus: number;
    accuracy: number;
    tierBefore: number;
    questionsCount: number;
  },
): LearnerAIState {
  const weakest = getWeakestSkill(prevState);
  const cefr = computeCefrLevel(prevState);
  const aiSummary =
    params.accuracy >= 85
      ? `Отличная точность (${params.accuracy}%). ИИ рекомендует двигаться к Tier ${Math.min(11, prevState.currentTier + 0.5).toFixed(1)} (текущий уровень: ${cefr}).`
      : `Точность ${params.accuracy}%. Локальный ИИ советует укрепить навык «${SKILL_META[weakest].label}».`;

  const next: LearnerAIState = {
    ...prevState,
    xp: prevState.xp + params.xpBonus,
    dailyXp: prevState.dailyXp + params.xpBonus,
    gems: prevState.gems + (params.accuracy >= 80 ? 15 : 8),
    lessonsCompleted: prevState.lessonsCompleted + 1,
    hearts: prevState.maxHearts, // restore hearts on lesson finish
    completedNodes: { ...prevState.completedNodes },
    sessions: [
      {
        id: `sess_${Date.now()}`,
        timestamp: Date.now(),
        mode: params.mode,
        unitTitle: params.unitTitle,
        xpEarned: params.xpBonus + params.questionsCount * 10,
        accuracy: params.accuracy,
        tierBefore: params.tierBefore,
        tierAfter: prevState.currentTier,
        questionsCount: params.questionsCount,
        aiSummary,
      },
      ...prevState.sessions,
    ].slice(0, 40),
    aiThoughts: [
      {
        id: `th_end_${Date.now()}`,
        timestamp: Date.now(),
        kind: (params.accuracy >= 80 ? 'boost' : 'insight') as AiThought['kind'],
        message: aiSummary,
      },
      ...prevState.aiThoughts,
    ].slice(0, 18),
  };

  if (params.nodeId) {
    const prevCrowns = next.completedNodes[params.nodeId] ?? 0;
    next.completedNodes[params.nodeId] = Math.min(3, prevCrowns + 1);
  }

  return next;
}

export function getDueReviewCount(state: LearnerAIState): number {
  const now = Date.now();
  return Object.values(state.itemMemory).filter(
    (m) => m.nextReviewAt <= now || m.strength < 0.55,
  ).length;
}
