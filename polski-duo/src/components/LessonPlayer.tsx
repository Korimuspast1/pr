import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  AlertTriangle,
  BrainCircuit,
  CheckCircle2,
  Flame,
  Gauge,
  Heart,
  Infinity as InfinityIcon,
  Lightbulb,
  Sparkles,
  Volume2,
  X,
  XCircle,
} from 'lucide-react';
import {
  applyExerciseOutcome,
  evaluateAnswer,
  recordCompletedSession,
} from '../ai/localAiEngine';
import { generateAdaptiveExercise, generatePlacementQuestion } from '../ai/polishGenerator';
import { LEXICON, SKILL_META } from '../data/polishLexicon';
import {
  AnswerEvaluation,
  Exercise,
  LearnerAIState,
  SessionLog,
  SkillId,
} from '../types/learning';
import {
  canInstallAndroidPolishTts,
  openAndroidTtsInstaller,
  playUiSound,
  speakPolish,
  SpeakStatus,
} from '../utils/sound';
import { MascotOrzel } from './MascotOrzel';

const POLISH_CHARS = ['ą', 'ć', 'ę', 'ł', 'ń', 'ó', 'ś', 'ź', 'ż'];

export interface LessonConfig {
  mode: SessionLog['mode'];
  title: string;
  nodeId?: string;
  skillFocus?: SkillId;
  initialExercises: Exercise[];
  isInfinite?: boolean;
  isPlacement?: boolean;
}

interface LessonPlayerProps {
  config: LessonConfig;
  aiState: LearnerAIState;
  onUpdateState: (updater: (prev: LearnerAIState) => LearnerAIState) => void;
  onExit: () => void;
}

export const LessonPlayer: React.FC<LessonPlayerProps> = ({
  config,
  aiState,
  onUpdateState,
  onExit,
}) => {
  const [exercises, setExercises] = useState<Exercise[]>(config.initialExercises);
  const [index, setIndex] = useState(0);
  const [selectedOption, setSelectedOption] = useState<string>('');
  const [typedText, setTypedText] = useState<string>('');
  const [selectedTiles, setSelectedTiles] = useState<number[]>([]);

  // Match pairs state
  const [matchedIds, setMatchedIds] = useState<string[]>([]);
  const [selectedPair, setSelectedPair] = useState<{ side: 'left' | 'right'; id: string } | null>(null);
  const [wrongPairShake, setWrongPairShake] = useState(false);

  const [evaluation, setEvaluation] = useState<AnswerEvaluation | null>(null);
  const [hintLevel, setHintLevel] = useState<number>(0);
  const [speakingActive, setSpeakingActive] = useState<boolean>(false);
  const [speakStatus, setSpeakStatus] = useState<SpeakStatus | null>(null);
  const [correctCount, setCorrectCount] = useState(0);
  const [answeredCount, setAnsweredCount] = useState(0);
  const [combo, setCombo] = useState(0);
  const [maxCombo, setMaxCombo] = useState(0);
  const [finished, setFinished] = useState(false);

  // Placement staircase state
  const [placementTier, setPlacementTier] = useState(3.2);
  const [placementHistoryIds, setPlacementHistoryIds] = useState<string[]>([]);

  const tierStartRef = useRef(aiState.currentTier);
  const questionStartRef = useRef(Date.now());
  const inputRef = useRef<HTMLInputElement | null>(null);

  const currentExercise = exercises[index];

  // Shuffle right column once per match_pairs exercise
  const shuffledRightPairs = useMemo(() => {
    if (!currentExercise?.pairs) return [];
    const arr = [...currentExercise.pairs];
    for (let i = arr.length - 1; i > 0; i--) {
      const j = (i * 7 + currentExercise.id.length) % (i + 1);
      [arr[i], arr[j]] = [arr[j], arr[i]];
    }
    return arr;
  }, [currentExercise]);

  const triggerSpeak = (text: string, slow = false) => {
    const status = speakPolish(text, slow);
    setSpeakStatus(status);
    setSpeakingActive(true);
    setTimeout(() => setSpeakingActive(false), 650);
  };

  useEffect(() => {
    questionStartRef.current = Date.now();
    if (currentExercise?.audioTextPl && currentExercise.type === 'listening') {
      triggerSpeak(currentExercise.audioTextPl, false);
    }
  }, [index, currentExercise?.id]);

  const resetInputs = () => {
    setSelectedOption('');
    setTypedText('');
    setSelectedTiles([]);
    setMatchedIds([]);
    setSelectedPair(null);
    setWrongPairShake(false);
    setEvaluation(null);
    setHintLevel(0);
    setSpeakStatus(null);
  };

  const canCheck = useMemo(() => {
    if (!currentExercise || evaluation) return false;
    if (currentExercise.type === 'multiple_choice' || currentExercise.type === 'cloze_grammar') {
      return selectedOption.length > 0;
    }
    if (currentExercise.type === 'word_tiles') {
      return selectedTiles.length > 0;
    }
    if (currentExercise.type === 'listening') {
      return currentExercise.tiles ? selectedTiles.length > 0 : typedText.trim().length > 0;
    }
    if (currentExercise.type === 'type_translation') {
      return typedText.trim().length > 0;
    }
    if (currentExercise.type === 'match_pairs') {
      return matchedIds.length === (currentExercise.pairs?.length ?? 5);
    }
    return false;
  }, [currentExercise, evaluation, selectedOption, selectedTiles, typedText, matchedIds]);

  const handleTileTap = (tileIdx: number) => {
    if (evaluation) return;
    playUiSound('tap', aiState.soundEnabled);
    setSelectedTiles((prev) =>
      prev.includes(tileIdx) ? prev.filter((x) => x !== tileIdx) : [...prev, tileIdx],
    );
  };

  const handleInsertPolishChar = (ch: string) => {
    if (evaluation) return;
    playUiSound('tap', aiState.soundEnabled);
    setTypedText((prev) => prev + ch);
    inputRef.current?.focus();
  };

  const handleMatchTap = (side: 'left' | 'right', id: string) => {
    if (evaluation || matchedIds.includes(id)) return;
    playUiSound('tap', aiState.soundEnabled);

    if (!selectedPair || selectedPair.side === side) {
      setSelectedPair({ side, id });
      return;
    }

    if (selectedPair.id === id) {
      const updated = [...matchedIds, id];
      setMatchedIds(updated);
      setSelectedPair(null);
      if (updated.length === (currentExercise.pairs?.length ?? 5)) {
        submitAnswer('MATCH_ALL');
      }
    } else {
      setWrongPairShake(true);
      playUiSound('wrong', aiState.soundEnabled);
      setSelectedPair(null);
      setTimeout(() => {
        setWrongPairShake(false);
      }, 300);
    }
  };

  const submitAnswer = (overrideAnswer?: string) => {
    if (!currentExercise || evaluation) return;

    let userAnswer = overrideAnswer ?? '';
    if (!overrideAnswer) {
      if (currentExercise.type === 'multiple_choice' || currentExercise.type === 'cloze_grammar') {
        userAnswer = selectedOption;
      } else if (
        currentExercise.type === 'word_tiles' ||
        (currentExercise.type === 'listening' && currentExercise.tiles)
      ) {
        userAnswer = selectedTiles.map((idx) => currentExercise.tiles![idx]).join(' ');
      } else if (
        currentExercise.type === 'type_translation' ||
        (currentExercise.type === 'listening' && !currentExercise.tiles)
      ) {
        userAnswer = typedText;
      } else if (currentExercise.type === 'match_pairs') {
        userAnswer = matchedIds.length === (currentExercise.pairs?.length ?? 5) ? 'MATCH_ALL' : '';
      }
    }

    const responseMs = Math.max(500, Date.now() - questionStartRef.current);
    const result = evaluateAnswer(currentExercise, userAnswer, aiState.currentTier);
    setEvaluation(result);
    setAnsweredCount((c) => c + 1);

    if (result.isCorrect) {
      playUiSound('correct', aiState.soundEnabled);
      setCorrectCount((c) => c + 1);
      setCombo((c) => {
        const next = c + 1;
        setMaxCombo((m) => Math.max(m, next));
        return next;
      });
    } else {
      playUiSound('wrong', aiState.soundEnabled);
      setCombo(0);
    }

    if (currentExercise.audioTextPl) {
      speakPolish(currentExercise.audioTextPl);
    }

    // Update Local AI cognitive model immediately!
    onUpdateState((prev) =>
      applyExerciseOutcome(prev, currentExercise, result, responseMs, Boolean(config.isInfinite)),
    );

    // Update placement staircase
    if (config.isPlacement) {
      setPlacementTier((t) =>
        result.isCorrect ? Math.min(9.5, t + 1.5) : Math.max(1.2, t - 1.1),
      );
      setPlacementHistoryIds((ids) => [...ids, ...currentExercise.itemIds, currentExercise.questionPl ?? '']);
    }
  };

  const finishSession = (finalAnswered: number, finalCorrect: number) => {
    const accuracy = finalAnswered > 0 ? Math.round((finalCorrect / finalAnswered) * 100) : 100;
    const xpBonus = config.isInfinite ? Math.min(80, finalCorrect * 5) : 25;
    playUiSound('complete', aiState.soundEnabled);

    onUpdateState((prev) => {
      let updated = recordCompletedSession(prev, {
        mode: config.mode,
        unitTitle: config.title,
        nodeId: config.nodeId,
        xpBonus,
        accuracy,
        tierBefore: tierStartRef.current,
        questionsCount: finalAnswered,
      });

      if (config.isPlacement) {
        const calibratedTier = Math.max(1.2, Math.min(9.5, Number(placementTier.toFixed(1))));
        updated = {
          ...updated,
          placementDone: true,
          currentTier: calibratedTier,
          aiThoughts: [
            {
              id: `plm_${Date.now()}`,
              timestamp: Date.now(),
              kind: 'insight',
              message: `ИИ-калибровка завершена: ваш стартовый уровень сложности установлен на Tier ${calibratedTier.toFixed(1)}!`,
            },
            ...updated.aiThoughts,
          ],
        };
      }
      return updated;
    });
    setFinished(true);
  };

  const handleNext = () => {
    if (config.isInfinite) {
      // Dynamically generate the next exercise on the fly from latest AI state!
      const nextEx = generateAdaptiveExercise(
        aiState,
        aiState.currentTier,
        config.skillFocus,
        [currentExercise.type],
      );
      setExercises((prev) => [...prev, nextEx]);
      setIndex((i) => i + 1);
      resetInputs();
      return;
    }

    if (config.isPlacement) {
      if (index + 1 >= 6) {
        finishSession(answeredCount, correctCount);
        return;
      }
      const nextTier = evaluation?.isCorrect
        ? Math.min(9.5, placementTier)
        : Math.max(1.2, placementTier);
      const nextEx = generatePlacementQuestion(nextTier, placementHistoryIds);
      setExercises((prev) => [...prev, nextEx]);
      setIndex((i) => i + 1);
      resetInputs();
      return;
    }

    if (index + 1 >= exercises.length) {
      finishSession(answeredCount, correctCount);
      return;
    }

    setIndex((i) => i + 1);
    resetInputs();
  };

  // Compute progressive infinite AI hints for the current exercise
  const hintDetails = useMemo(() => {
    if (!currentExercise) return null;
    const linkedLex = LEXICON.filter((l) => currentExercise.itemIds.includes(l.id));
    const phonetics = linkedLex.map((l) => `${l.pl} [${l.phoneticRu}]`).join(' · ');

    let step2Text = '';
    let eliminatedOptions: string[] = [];

    if (
      (currentExercise.type === 'multiple_choice' || currentExercise.type === 'cloze_grammar') &&
      currentExercise.options
    ) {
      eliminatedOptions = currentExercise.options
        .filter((o) => o !== currentExercise.correctAnswer)
        .slice(0, 2);
      step2Text = 'ИИ убрал лишние варианты (режим 50/50)!';
    } else if (currentExercise.type === 'match_pairs' && currentExercise.pairs?.length) {
      const firstUnmatched =
        currentExercise.pairs.find((p) => !matchedIds.includes(p.id)) ?? currentExercise.pairs[0];
      step2Text = `Совпадающая пара: «${firstUnmatched.pl}» ↔ «${firstUnmatched.ru}».`;
    } else {
      const words = currentExercise.correctAnswer.trim().split(/\s+/);
      if (words.length > 1) {
        step2Text = `Начало фразы: «${words[0]} ${words.length > 2 ? words[1] : ''}...» (всего слов: ${words.length}).`;
      } else {
        const w = currentExercise.correctAnswer;
        const prefix = w.slice(0, Math.max(1, Math.ceil(w.length / 2)));
        step2Text = `Слово начинается на: «${prefix}...» (букв: ${w.length}).`;
      }
    }

    return {
      step1: currentExercise.aiNote + (phonetics ? ` Фонетика: ${phonetics}.` : ''),
      step2: step2Text,
      step3: `Точный ответ: «${currentExercise.correctAnswer}»`,
      eliminatedOptions,
    };
  }, [currentExercise, matchedIds]);

  const handleRequestHint = () => {
    if (evaluation) return;
    playUiSound('tap', aiState.soundEnabled);
    setHintLevel((lvl) => (lvl >= 3 ? 3 : lvl + 1));
  };

  // Lesson Finished Celebration Screen
  if (finished) {
    const accuracy = answeredCount > 0 ? Math.round((correctCount / answeredCount) * 100) : 100;
    const totalXp = correctCount * (config.isInfinite ? 12 : 10) + 25;
    const tierDelta = aiState.currentTier - tierStartRef.current;

    return (
      <div className="fixed inset-0 z-50 bg-white flex flex-col items-center justify-center p-6 animate-pop">
        <div className="max-w-md w-full text-center space-y-6">
          <div className="flex justify-center">
            <MascotOrzel mood="cheering" size={136} className="animate-float" />
          </div>

          <div>
            <h1 className="text-3xl font-black text-[#FFC800] tracking-tight">
              {config.isPlacement ? 'Калибровка ИИ завершена!' : 'Урок пройден! Świetnie!'}
            </h1>
            <p className="text-sm font-bold text-[#777777] mt-1">
              Локальный ИИ обновил ваш профиль знаний и модель памяти.
            </p>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="rounded-2xl border-2 border-[#FFC800] bg-[#FFFDF2] p-3">
              <div className="text-[11px] font-black uppercase text-[#E5A900]">Опыт</div>
              <div className="text-2xl font-black text-[#FF9600] mt-1">+{totalXp}</div>
            </div>
            <div className="rounded-2xl border-2 border-[#58CC02] bg-[#F3FFE8] p-3">
              <div className="text-[11px] font-black uppercase text-[#46A302]">Точность</div>
              <div className="text-2xl font-black text-[#58CC02] mt-1">{accuracy}%</div>
            </div>
            <div className="rounded-2xl border-2 border-[#CE82FF] bg-[#FAF2FF] p-3">
              <div className="text-[11px] font-black uppercase text-[#A55EEA]">Сложность</div>
              <div className="text-xl font-black text-[#8549BA] mt-1">
                Tier {aiState.currentTier.toFixed(1)}
              </div>
              <div className="text-[10px] font-extrabold text-[#A55EEA]">
                {tierDelta >= 0 ? `+${tierDelta.toFixed(2)}` : tierDelta.toFixed(2)}
              </div>
            </div>
          </div>

          <div className="rounded-2xl border-2 border-[#E5E5E5] bg-[#F7F7F7] p-4 text-left">
            <div className="flex items-center gap-2 text-xs font-black uppercase text-[#A55EEA] mb-1">
              <BrainCircuit className="w-4 h-4" />
              Вывод локального ИИ (Макс. серия: {maxCombo})
            </div>
            <p className="text-sm font-bold text-[#4B4B4B]">
              {aiState.aiThoughts[0]?.message}
            </p>
          </div>

          <button
            onClick={onExit}
            className="w-full duo-btn duo-btn-green rounded-2xl py-4 text-base font-black uppercase tracking-wider"
          >
            ПРОДОЛЖИТЬ
          </button>
        </div>
      </div>
    );
  }

  if (!currentExercise) return null;

  const progressPct = config.isInfinite
    ? Math.min(100, ((answeredCount % 10) / 10) * 100 || 8)
    : Math.round(((index + (evaluation ? 1 : 0)) / (config.isPlacement ? 6 : exercises.length)) * 100);

  return (
    <div className="fixed inset-0 z-50 bg-white flex flex-col">
      {/* Top Progress Header */}
      <header className="max-w-4xl w-full mx-auto px-4 pt-5 pb-3 flex items-center gap-4">
        <button
          onClick={() => {
            if (config.isInfinite && answeredCount > 0) {
              finishSession(answeredCount, correctCount);
            } else {
              onExit();
            }
          }}
          className="p-2 rounded-xl text-[#AFAFAF] hover:text-[#4B4B4B] hover:bg-[#F7F7F7] cursor-pointer"
          title={config.isInfinite ? 'Завершить и сохранить результат' : 'Выйти из урока'}
        >
          <X className="w-6 h-6 stroke-[2.8]" />
        </button>

        {/* Progress Bar */}
        <div className="flex-1 h-4 rounded-full bg-[#E5E5E5] overflow-hidden relative">
          <div
            className={`h-full rounded-full transition-all duration-300 relative ${
              config.isInfinite ? 'bg-[#CE82FF]' : 'bg-[#58CC02]'
            }`}
            style={{ width: `${Math.max(6, progressPct)}%` }}
          >
            <div className="absolute top-1 left-2 right-2 h-1 rounded-full bg-white/30" />
          </div>
        </div>

        {/* Status Badges */}
        <div className="flex items-center gap-3 shrink-0">
          <span className="hidden sm:inline-flex items-center gap-1 px-2.5 py-1 rounded-xl bg-[#FAF2FF] border border-[#CE82FF]/40 text-xs font-black text-[#8549BA]">
            <BrainCircuit className="w-3.5 h-3.5" />
            Tier {aiState.currentTier.toFixed(1)}
          </span>

          {combo >= 2 && (
            <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-xl bg-[#FFF0D6] text-xs font-black text-[#FF9600] animate-pop">
              <Flame className="w-3.5 h-3.5 fill-[#FF9600]" />x{combo}
            </span>
          )}

          {config.isInfinite ? (
            <button
              onClick={() => finishSession(answeredCount, correctCount)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-[#F5E8FF] text-[#8549BA] font-black text-xs cursor-pointer hover:bg-[#EBD5FF]"
            >
              <InfinityIcon className="w-4 h-4" />
              Завершить ({answeredCount})
            </button>
          ) : (
            <div className="flex items-center gap-1 font-black text-[#FF4B4B]" title="Бесконечные сердечки">
              <Heart className="w-5 h-5 fill-[#FF4B4B]" />
              <InfinityIcon className="w-4 h-4 stroke-[3]" />
            </div>
          )}
        </div>
      </header>

      {/* Main Exercise Body */}
      <main className="flex-1 overflow-y-auto px-4 py-4 flex flex-col items-center justify-center">
        <div className="max-w-2xl w-full space-y-6 animate-pop" key={currentExercise.id}>
          {/* Skill & Grammar Pill + Infinite Hint Button */}
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-2">
              <span
                className="text-[11px] font-black uppercase tracking-wider px-2.5 py-1 rounded-lg text-white"
                style={{ backgroundColor: SKILL_META[currentExercise.skill].color }}
              >
                {SKILL_META[currentExercise.skill].shortLabel}
              </span>
              {currentExercise.grammarTag && (
                <span className="text-[11px] font-extrabold px-2.5 py-1 rounded-lg bg-[#F1F5F9] text-[#4B4B4B]">
                  {currentExercise.grammarTag}
                </span>
              )}
              {currentExercise.isFalseFriendTrap && (
                <span className="inline-flex items-center gap-1 text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#FFE4E8] text-[#DC143C]">
                  <AlertTriangle className="w-3 h-3" /> Ловушка для русских
                </span>
              )}
            </div>

            {!evaluation && (
              <button
                type="button"
                onClick={handleRequestHint}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl border-2 border-b-4 border-[#FFC800] bg-[#FFFDF2] hover:bg-[#FFF5CC] text-[#B37800] text-xs font-black cursor-pointer active:translate-y-[1px]"
                title="Бесконечные подсказки ИИ на каждом уровне"
              >
                <Lightbulb className="w-4 h-4 fill-[#FFC800] text-[#E5A900]" />
                <span>
                  {hintLevel === 0
                    ? 'Подсказка'
                    : hintLevel < 3
                      ? `Ещё подсказка (${hintLevel}/3)`
                      : 'Подсказка 3/3'}
                </span>
                <InfinityIcon className="w-3.5 h-3.5 stroke-[2.8]" />
              </button>
            )}
          </div>

          {/* Progressive Infinite AI Hint Card */}
          {hintLevel > 0 && hintDetails && (
            <div className="rounded-2xl border-2 border-[#FFC800] bg-[#FFFDF2] p-3.5 space-y-1.5 animate-pop">
              <div className="flex items-center justify-between text-[11px] font-black uppercase text-[#B37800]">
                <span className="flex items-center gap-1.5">
                  <Lightbulb className="w-3.5 h-3.5 fill-[#FFC800]" />
                  Подсказка ИИ (Уровень {hintLevel}/3 · ∞)
                </span>
                {hintLevel < 3 && !evaluation && (
                  <button
                    type="button"
                    onClick={handleRequestHint}
                    className="underline cursor-pointer hover:text-[#805500]"
                  >
                    Показать подробнее →
                  </button>
                )}
              </div>
              <p className="text-xs sm:text-sm font-bold text-[#4B4B4B] leading-snug">
                {hintDetails.step1}
              </p>
              {hintLevel >= 2 && (
                <p className="text-xs sm:text-sm font-extrabold text-[#1899D6]">
                  {hintDetails.step2}
                </p>
              )}
              {hintLevel >= 3 && (
                <p className="text-xs sm:text-sm font-black text-[#46A302]">
                  {hintDetails.step3}
                </p>
              )}
            </div>
          )}

          <h2 className="text-2xl sm:text-[26px] font-black text-[#3C3C3C] leading-snug">
            {currentExercise.promptRu}
          </h2>

          {/* Question Speech Bubble */}
          {(currentExercise.questionPl || currentExercise.questionRu || currentExercise.type === 'listening') && (
            <div className="flex items-start gap-4">
              <MascotOrzel
                mood={evaluation ? (evaluation.isCorrect ? 'cheering' : 'oops') : 'thinking'}
                size={76}
                className="shrink-0 hidden sm:block"
              />
              <div className="flex-1 rounded-2xl border-2 border-[#E5E5E5] bg-white p-4 sm:p-5 relative shadow-xs">
                <div className="flex items-center gap-3 flex-wrap">
                  {currentExercise.audioTextPl && (
                    <div className="flex items-center gap-2 shrink-0">
                      <button
                        type="button"
                        onClick={() => triggerSpeak(currentExercise.audioTextPl!, false)}
                        className={`w-12 h-12 rounded-xl duo-btn duo-btn-blue flex items-center justify-center ${
                          speakingActive ? 'ring-4 ring-[#84D8FF] scale-105' : ''
                        }`}
                        title="Произнести по-польски"
                      >
                        <Volume2 className="w-6 h-6" />
                      </button>
                      <button
                        type="button"
                        onClick={() => triggerSpeak(currentExercise.audioTextPl!, true)}
                        className="h-12 px-3 rounded-xl duo-btn duo-btn-outline flex items-center gap-1.5 text-xs font-black text-[#1899D6]"
                        title="Произнести медленно (0.65x)"
                      >
                        <Gauge className="w-4 h-4" />
                        <span>0.65x</span>
                      </button>
                    </div>
                  )}

                  <div className="flex-1 min-w-[180px]">
                    {currentExercise.type === 'listening' ? (
                      <div>
                        <div className="text-base font-black text-[#1CB0F6]">
                          Нажмите на динамик, чтобы услышать фразу
                        </div>
                        {currentExercise.questionRu && (
                          <div className="text-sm font-bold text-[#777777] mt-1">
                            {currentExercise.questionRu}
                          </div>
                        )}
                        {(speakStatus === 'android_needs_pl_pack' || speakStatus === 'fallback_chime') && (
                          <div className="mt-2 rounded-xl bg-[#FFFDF2] border border-[#FFC800] px-3 py-2 text-xs font-bold text-[#805500] flex flex-wrap items-center justify-between gap-2">
                            <span>
                              Голосовой пакет pl-PL не найден. Произношение:{' '}
                              <strong className="text-[#1899D6]">
                                [{LEXICON.find((l) => currentExercise.itemIds.includes(l.id))?.phoneticRu ?? currentExercise.audioTextPl}]
                              </strong>
                            </span>
                            {canInstallAndroidPolishTts() && (
                              <button
                                type="button"
                                onClick={openAndroidTtsInstaller}
                                className="px-2 py-1 rounded-lg bg-[#1CB0F6] text-white font-black cursor-pointer"
                              >
                                Установить голос
                              </button>
                            )}
                          </div>
                        )}
                      </div>
                    ) : (
                      <>
                        {currentExercise.questionPl && (
                          <div className="text-xl sm:text-2xl font-black text-[#3C3C3C]">
                            {currentExercise.questionPl}
                          </div>
                        )}
                        {currentExercise.questionRu && (
                          <div className="text-lg sm:text-xl font-extrabold text-[#4B4B4B]">
                            {currentExercise.questionRu}
                          </div>
                        )}
                      </>
                    )}

                    {currentExercise.phoneticHint && (
                      <div className="text-xs font-extrabold text-[#1899D6] mt-1">
                        Произношение: {currentExercise.phoneticHint}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* === EXERCISE MECHANIC 1: MULTIPLE CHOICE === */}
          {currentExercise.type === 'multiple_choice' && currentExercise.options && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {currentExercise.options.map((opt, idx) => {
                const isSelected = selectedOption === opt;
                const isCorrectOpt = evaluation && opt === currentExercise.correctAnswer;
                const isEliminated =
                  !evaluation &&
                  hintLevel >= 2 &&
                  Boolean(hintDetails?.eliminatedOptions.includes(opt));
                return (
                  <button
                    key={opt}
                    disabled={Boolean(evaluation) || isEliminated}
                    onClick={() => {
                      playUiSound('tap', aiState.soundEnabled);
                      setSelectedOption(opt);
                    }}
                    className={`rounded-2xl border-2 border-b-4 p-4 text-left font-extrabold text-base flex items-center gap-3 transition-all ${
                      isEliminated
                        ? 'bg-[#F7F7F7] border-[#E5E5E5] text-[#AFAFAF] opacity-45 line-through cursor-not-allowed'
                        : isCorrectOpt
                          ? 'bg-[#D7FFB8] border-[#58CC02] text-[#46A302] cursor-pointer'
                          : isSelected && evaluation && !evaluation.isCorrect
                            ? 'bg-[#FFE4E8] border-[#DC143C] text-[#DC143C] cursor-pointer'
                            : isSelected
                              ? 'bg-[#DDF4FF] border-[#1CB0F6] text-[#1899D6] cursor-pointer'
                              : 'bg-white border-[#E5E5E5] hover:bg-[#F7F7F7] text-[#3C3C3C] cursor-pointer'
                    }`}
                  >
                    <span className="w-7 h-7 rounded-lg border-2 border-current/25 flex items-center justify-center text-xs font-black shrink-0">
                      {idx + 1}
                    </span>
                    <span className="leading-snug">{opt}</span>
                  </button>
                );
              })}
            </div>
          )}

          {/* === EXERCISE MECHANIC 2: CLOZE GRAMMAR === */}
          {currentExercise.type === 'cloze_grammar' && currentExercise.options && (
            <div className="space-y-5">
              <div className="rounded-2xl border-2 border-[#E5E5E5] bg-[#FFFDF9] p-5 text-xl font-black text-center text-[#3C3C3C]">
                <span>{currentExercise.clozeBefore} </span>
                <span className="inline-block min-w-[110px] px-3 py-1 mx-1 rounded-xl border-2 border-dashed border-[#1CB0F6] bg-[#DDF4FF]/50 text-[#1899D6]">
                  {selectedOption || '______'}
                </span>
                <span>{currentExercise.clozeAfter}</span>
              </div>

              <div className="grid grid-cols-2 gap-3">
                {currentExercise.options.map((opt) => {
                  const isSelected = selectedOption === opt;
                  const isEliminated =
                    !evaluation &&
                    hintLevel >= 2 &&
                    Boolean(hintDetails?.eliminatedOptions.includes(opt));
                  return (
                    <button
                      key={opt}
                      disabled={Boolean(evaluation) || isEliminated}
                      onClick={() => {
                        playUiSound('tap', aiState.soundEnabled);
                        setSelectedOption(opt);
                      }}
                      className={`rounded-2xl border-2 border-b-4 py-3.5 px-4 font-black text-base ${
                        isEliminated
                          ? 'bg-[#F7F7F7] border-[#E5E5E5] text-[#AFAFAF] opacity-45 line-through cursor-not-allowed'
                          : isSelected
                            ? 'bg-[#DDF4FF] border-[#1CB0F6] text-[#1899D6] cursor-pointer'
                            : 'bg-white border-[#E5E5E5] hover:bg-[#F7F7F7] cursor-pointer'
                      }`}
                    >
                      {opt}
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {/* === EXERCISE MECHANIC 3: WORD TILES / LISTENING TILES === */}
          {(currentExercise.type === 'word_tiles' ||
            (currentExercise.type === 'listening' && currentExercise.tiles)) &&
            currentExercise.tiles && (
              <div className="space-y-5">
                {/* Answer Drop Zone */}
                <div className="min-h-[76px] rounded-2xl border-2 border-[#E5E5E5] bg-[#F7F7F7] p-3 flex flex-wrap gap-2 items-center">
                  {selectedTiles.length === 0 ? (
                    <span className="text-sm font-bold text-[#AFAFAF] px-2">
                      Нажимайте на слова ниже по порядку...
                    </span>
                  ) : (
                    selectedTiles.map((tileIdx) => (
                      <button
                        key={`sel_${tileIdx}`}
                        disabled={Boolean(evaluation)}
                        onClick={() => handleTileTap(tileIdx)}
                        className="duo-btn duo-btn-outline rounded-xl px-3.5 py-2 font-extrabold text-base text-[#3C3C3C] bg-white"
                      >
                        {currentExercise.tiles![tileIdx]}
                      </button>
                    ))
                  )}
                </div>

                {/* Available Word Bank */}
                <div className="flex flex-wrap gap-2.5 justify-center">
                  {currentExercise.tiles.map((word, idx) => {
                    const isUsed = selectedTiles.includes(idx);
                    return (
                      <button
                        key={`bank_${idx}_${word}`}
                        disabled={isUsed || Boolean(evaluation)}
                        onClick={() => handleTileTap(idx)}
                        className={`rounded-xl px-4 py-2.5 font-extrabold text-base border-2 border-b-4 transition-all ${
                          isUsed
                            ? 'bg-[#E5E5E5] border-[#E5E5E5] text-transparent cursor-default'
                            : 'bg-white border-[#E5E5E5] text-[#3C3C3C] hover:bg-[#F7F7F7] cursor-pointer active:translate-y-[2px]'
                        }`}
                      >
                        {word}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

          {/* === EXERCISE MECHANIC 4: TYPE TRANSLATION / DICTATION === */}
          {(currentExercise.type === 'type_translation' ||
            (currentExercise.type === 'listening' && !currentExercise.tiles)) && (
            <div className="space-y-3">
              <input
                ref={inputRef}
                type="text"
                disabled={Boolean(evaluation)}
                value={typedText}
                onChange={(e) => setTypedText(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && canCheck && !evaluation) {
                    submitAnswer();
                  }
                }}
                placeholder="Напишите по-польски..."
                className="w-full rounded-2xl border-2 border-[#E5E5E5] bg-[#F7F7F7] focus:bg-white focus:border-[#1CB0F6] outline-none px-4 py-4 text-lg font-extrabold text-[#3C3C3C]"
                autoFocus
              />

              {/* Polish Diacritic Virtual Key Bar */}
              <div className="flex flex-wrap items-center gap-1.5 justify-center">
                <span className="text-xs font-extrabold text-[#777777] mr-1">
                  Польские буквы:
                </span>
                {POLISH_CHARS.map((ch) => (
                  <button
                    key={ch}
                    type="button"
                    disabled={Boolean(evaluation)}
                    onClick={() => handleInsertPolishChar(ch)}
                    className="w-9 h-9 rounded-xl border-2 border-b-4 border-[#E5E5E5] bg-white hover:bg-[#DDF4FF] hover:border-[#1CB0F6] font-black text-base text-[#3C3C3C] cursor-pointer active:translate-y-[1px]"
                  >
                    {ch}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* === EXERCISE MECHANIC 5: MATCH PAIRS === */}
          {currentExercise.type === 'match_pairs' && currentExercise.pairs && (
            <div
              className={`grid grid-cols-2 gap-4 ${wrongPairShake ? 'animate-shake' : ''}`}
            >
              <div className="space-y-2.5">
                {currentExercise.pairs.map((p) => {
                  const matched = matchedIds.includes(p.id);
                  const selected = selectedPair?.side === 'left' && selectedPair.id === p.id;
                  return (
                    <button
                      key={`l_${p.id}`}
                      disabled={matched}
                      onClick={() => handleMatchTap('left', p.id)}
                      className={`w-full rounded-2xl border-2 border-b-4 p-3.5 font-extrabold text-sm sm:text-base transition-all cursor-pointer ${
                        matched
                          ? 'bg-[#D7FFB8]/60 border-[#58CC02]/40 text-[#46A302] opacity-60'
                          : selected
                            ? 'bg-[#DDF4FF] border-[#1CB0F6] text-[#1899D6]'
                            : 'bg-white border-[#E5E5E5] hover:bg-[#F7F7F7]'
                      }`}
                    >
                      {p.pl}
                    </button>
                  );
                })}
              </div>

              <div className="space-y-2.5">
                {shuffledRightPairs.map((p) => {
                  const matched = matchedIds.includes(p.id);
                  const selected = selectedPair?.side === 'right' && selectedPair.id === p.id;
                  return (
                    <button
                      key={`r_${p.id}`}
                      disabled={matched}
                      onClick={() => handleMatchTap('right', p.id)}
                      className={`w-full rounded-2xl border-2 border-b-4 p-3.5 font-extrabold text-sm sm:text-base transition-all cursor-pointer ${
                        matched
                          ? 'bg-[#D7FFB8]/60 border-[#58CC02]/40 text-[#46A302] opacity-60'
                          : selected
                            ? 'bg-[#DDF4FF] border-[#1CB0F6] text-[#1899D6]'
                            : 'bg-white border-[#E5E5E5] hover:border-[#1CB0F6] hover:bg-[#DDF4FF]/40'
                      }`}
                    >
                      {p.ru}
                    </button>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      </main>

      {/* Bottom Action / AI Feedback Drawer */}
      <footer
        className={`border-t-2 px-4 py-4 sm:py-5 transition-colors ${
          evaluation
            ? evaluation.isCorrect
              ? evaluation.isMinorDiacriticSlip
                ? 'bg-[#FFF5D6] border-[#FFC800]'
                : 'bg-[#D7FFB8] border-[#58CC02]'
              : 'bg-[#FFE4E8] border-[#DC143C]'
            : 'bg-white border-[#E5E5E5]'
        }`}
      >
        <div className="max-w-4xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          {evaluation ? (
            <div className="flex items-start gap-3 flex-1">
              {evaluation.isCorrect ? (
                <CheckCircle2
                  className={`w-8 h-8 shrink-0 mt-0.5 ${
                    evaluation.isMinorDiacriticSlip ? 'text-[#E5A900]' : 'text-[#46A302]'
                  }`}
                />
              ) : (
                <XCircle className="w-8 h-8 shrink-0 mt-0.5 text-[#DC143C]" />
              )}
              <div>
                <div
                  className={`text-lg font-black ${
                    evaluation.isCorrect
                      ? evaluation.isMinorDiacriticSlip
                        ? 'text-[#B37800]'
                        : 'text-[#46A302]'
                      : 'text-[#A30D2B]'
                  }`}
                >
                  {evaluation.feedbackTitle}
                </div>
                {!evaluation.isCorrect && (
                  <div className="text-sm font-black text-[#3C3C3C] mt-0.5">
                    Правильный ответ: <span className="underline">{evaluation.correctDisplay}</span>
                  </div>
                )}
                <p className="text-sm font-bold text-[#4B4B4B] mt-1 leading-snug">
                  {evaluation.feedbackExplanation}
                </p>
              </div>
            </div>
          ) : (
            <div className="flex items-center gap-2 text-xs font-extrabold text-[#777777]">
              <Sparkles className="w-4 h-4 text-[#CE82FF]" />
              <span>Локальный ИИ анализирует скорость и точность ответа</span>
            </div>
          )}

          {evaluation ? (
            <button
              onClick={handleNext}
              className={`w-full sm:w-auto min-w-[180px] duo-btn rounded-2xl py-3.5 px-8 font-black text-base uppercase tracking-wider ${
                evaluation.isCorrect ? 'duo-btn-green' : 'duo-btn-crimson'
              }`}
            >
              {config.isInfinite ? 'СЛЕДУЮЩЕЕ ∞' : 'ДАЛЕЕ'}
            </button>
          ) : (
            <button
              disabled={!canCheck}
              onClick={() => submitAnswer()}
              className={`w-full sm:w-auto min-w-[180px] rounded-2xl py-3.5 px-8 font-black text-base uppercase tracking-wider transition-all ${
                canCheck
                  ? 'duo-btn duo-btn-green'
                  : 'bg-[#E5E5E5] text-[#AFAFAF] border-b-4 border-[#D4D4D4] cursor-not-allowed'
              }`}
            >
              ПРОВЕРИТЬ
            </button>
          )}
        </div>
      </footer>
    </div>
  );
};
