import React from 'react';
import {
  Activity,
  AlertCircle,
  BrainCircuit,
  CheckCircle2,
  Clock,
  Flame,
  RotateCcw,
  Sparkles,
  TrendingUp,
} from 'lucide-react';
import {
  computeCefrLevel,
  computeGlobalMasteryScore,
  getDueReviewCount,
} from '../ai/localAiEngine';
import { LEXICON, SKILL_META } from '../data/polishLexicon';
import { ErrorKind, LearnerAIState, SkillId } from '../types/learning';
import { MascotOrzel } from './MascotOrzel';

interface StatsDashboardProps {
  state: LearnerAIState;
  onStartReviewLesson: () => void;
  onResetProgress: () => void;
}

const ERROR_LABELS: Record<ErrorKind, { label: string; color: string }> = {
  diacritic_slip: { label: 'Польская диакритика (ą, ę, ł, ś...)', color: '#FFC800' },
  false_friend_trap: { label: 'Ложные друзья переводчика', color: '#DC143C' },
  case_error: { label: 'Падежи и окончания (Biernik/Dopełniacz)', color: '#FF4B4B' },
  conjugation_error: { label: 'Спряжение глаголов', color: '#CE82FF' },
  word_order: { label: 'Порядок слов в фразе', color: '#1CB0F6' },
  vocabulary_miss: { label: 'Запоминание слов', color: '#FF9600' },
};

export const StatsDashboard: React.FC<StatsDashboardProps> = ({
  state,
  onStartReviewLesson,
  onResetProgress,
}) => {
  const masteryScore = computeGlobalMasteryScore(state);
  const cefr = computeCefrLevel(state);
  const accuracy =
    state.totalExercises > 0
      ? Math.round((state.totalCorrect / state.totalExercises) * 100)
      : 100;
  const dueCount = getDueReviewCount(state);
  const learnedWords = Object.values(state.itemMemory).filter((m) => m.strength >= 0.55).length;
  const seenWords = Object.keys(state.itemMemory).length;

  const recentSessions = [...state.sessions].slice(0, 10).reverse();

  // Memory items sorted weakest -> strongest
  const memoryEntries = Object.values(state.itemMemory)
    .map((mem) => ({
      mem,
      lex: LEXICON.find((l) => l.id === mem.id),
    }))
    .filter((x) => Boolean(x.lex))
    .sort((a, b) => a.mem.strength - b.mem.strength);

  const totalErrors = Object.values(state.errorCounts).reduce((a, b) => a + b, 0);

  return (
    <div className="max-w-[760px] mx-auto pb-28 md:pb-16 pt-6 px-4 space-y-6">
      {/* Header Summary Card */}
      <div className="rounded-3xl border-2 border-[#E5E5E5] bg-gradient-to-r from-[#FFFDF9] via-white to-[#DDF4FF]/40 p-6 flex flex-col sm:flex-row items-center justify-between gap-5">
        <div className="flex items-center gap-4">
          <MascotOrzel mood="ai" size={88} className="shrink-0" />
          <div>
            <div className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-[#D7FFB8] text-[#46A302] text-xs font-black uppercase">
              <span>Статистика и модель ИИ</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-black text-[#3C3C3C] mt-1">
              Ваш польский: {cefr} ({masteryScore}/1000)
            </h1>
            <p className="text-xs sm:text-sm font-bold text-[#777777] mt-1">
              Адаптивный Tier {state.currentTier.toFixed(2)} · Всего опыта: {state.xp} XP · Уроков:{' '}
              {state.lessonsCompleted}
            </p>
          </div>
        </div>

        <button
          onClick={onStartReviewLesson}
          className="shrink-0 duo-btn duo-btn-green rounded-2xl px-5 py-3 text-xs font-black uppercase tracking-wider flex items-center gap-2"
        >
          <Sparkles className="w-4 h-4" />
          УСИЛИТЬ СЛАБЫЕ ({dueCount})
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="flex items-center gap-1.5 text-xs font-black text-[#777777] uppercase">
            <CheckCircle2 className="w-4 h-4 text-[#58CC02]" />
            Точность
          </div>
          <div className="text-2xl font-black text-[#3C3C3C] mt-1">{accuracy}%</div>
          <div className="text-[11px] font-bold text-[#AFAFAF]">
            {state.totalCorrect} из {state.totalExercises}
          </div>
        </div>

        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="flex items-center gap-1.5 text-xs font-black text-[#777777] uppercase">
            <TrendingUp className="w-4 h-4 text-[#CE82FF]" />
            Уровень ИИ
          </div>
          <div className="text-2xl font-black text-[#8549BA] mt-1">
            Tier {state.currentTier.toFixed(1)}
          </div>
          <div className="text-[11px] font-bold text-[#AFAFAF]">CEFR: {cefr}</div>
        </div>

        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="flex items-center gap-1.5 text-xs font-black text-[#777777] uppercase">
            <Activity className="w-4 h-4 text-[#1CB0F6]" />
            Словарь
          </div>
          <div className="text-2xl font-black text-[#1899D6] mt-1">{learnedWords}</div>
          <div className="text-[11px] font-bold text-[#AFAFAF]">
            Усвоено (изучено {seenWords})
          </div>
        </div>

        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="flex items-center gap-1.5 text-xs font-black text-[#777777] uppercase">
            <Flame className="w-4 h-4 text-[#FF9600]" />
            Серия дней
          </div>
          <div className="text-2xl font-black text-[#FF9600] mt-1">{state.streakDays}</div>
          <div className="text-[11px] font-bold text-[#AFAFAF]">Сегодня: {state.dailyXp} XP</div>
        </div>
      </div>

      {/* 8-Skill BKT Mastery Bars */}
      <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-black text-[#3C3C3C] flex items-center gap-2">
            <BrainCircuit className="w-5 h-5 text-[#A55EEA]" />
            Оценка навыков локальным ИИ (BKT)
          </h2>
          <span className="text-xs font-extrabold text-[#777777]">0% → 100%</span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {(Object.keys(SKILL_META) as SkillId[]).map((skillId) => {
            const meta = SKILL_META[skillId];
            const pct = Math.round((state.skillMastery[skillId] ?? 0) * 100);
            const attempts = state.skillAttempts[skillId] ?? 0;

            return (
              <div key={skillId} className="space-y-1.5">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-black text-[#3C3C3C]">{meta.label}</span>
                  <span className="font-black" style={{ color: meta.color }}>
                    {pct}%
                  </span>
                </div>
                <div className="h-3.5 rounded-full bg-[#F1F5F9] overflow-hidden">
                  <div
                    className="h-full rounded-full transition-all duration-500"
                    style={{ width: `${pct}%`, backgroundColor: meta.color }}
                  />
                </div>
                <div className="text-[11px] font-bold text-[#AFAFAF]">
                  Ответов: {attempts} · {meta.description}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Session Progression SVG Chart & Error Analytics */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* SVG Curve */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-5">
          <h3 className="text-base font-black text-[#3C3C3C] mb-1">
            Кривая сложности и точности
          </h3>
          <p className="text-xs font-bold text-[#777777] mb-4">
            Динамика последних уроков ({recentSessions.length})
          </p>

          {recentSessions.length === 0 ? (
            <div className="h-40 rounded-xl bg-[#F7F7F7] flex flex-col items-center justify-center text-center p-4">
              <Clock className="w-7 h-7 text-[#AFAFAF] mb-2" />
              <p className="text-xs font-extrabold text-[#777777]">
                Пройдите первый урок, чтобы ИИ построил график вашего прогресса!
              </p>
            </div>
          ) : (
            <div className="space-y-2">
              <svg viewBox="0 0 300 130" className="w-full h-36 overflow-visible">
                {/* Grid lines */}
                {[20, 55, 90].map((y) => (
                  <line
                    key={y}
                    x1="10"
                    y1={y}
                    x2="290"
                    y2={y}
                    stroke="#E5E5E5"
                    strokeDasharray="4 4"
                  />
                ))}

                {/* Accuracy Polyline */}
                {recentSessions.length >= 1 && (
                  <polyline
                    fill="none"
                    stroke="#58CC02"
                    strokeWidth="3"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    points={recentSessions
                      .map((s, idx) => {
                        const x =
                          recentSessions.length === 1
                            ? 150
                            : 20 + (idx / (recentSessions.length - 1)) * 260;
                        const y = 110 - (s.accuracy / 100) * 90;
                        return `${x},${y}`;
                      })
                      .join(' ')}
                  />
                )}

                {/* Tier Polyline */}
                {recentSessions.length >= 1 && (
                  <polyline
                    fill="none"
                    stroke="#CE82FF"
                    strokeWidth="3"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    points={recentSessions
                      .map((s, idx) => {
                        const x =
                          recentSessions.length === 1
                            ? 150
                            : 20 + (idx / (recentSessions.length - 1)) * 260;
                        const y = 110 - (Math.min(11, s.tierAfter) / 11) * 90;
                        return `${x},${y}`;
                      })
                      .join(' ')}
                  />
                )}

                {/* Data Points */}
                {recentSessions.map((s, idx) => {
                  const x =
                    recentSessions.length === 1
                      ? 150
                      : 20 + (idx / (recentSessions.length - 1)) * 260;
                  const yAcc = 110 - (s.accuracy / 100) * 90;
                  const yTier = 110 - (Math.min(11, s.tierAfter) / 11) * 90;
                  return (
                    <g key={s.id}>
                      <circle cx={x} cy={yAcc} r="4.5" fill="#58CC02" stroke="#FFF" strokeWidth="1.5" />
                      <circle cx={x} cy={yTier} r="4.5" fill="#A55EEA" stroke="#FFF" strokeWidth="1.5" />
                    </g>
                  );
                })}
              </svg>
              <div className="flex items-center justify-center gap-5 text-xs font-extrabold">
                <span className="flex items-center gap-1.5 text-[#46A302]">
                  <span className="w-3 h-3 rounded-full bg-[#58CC02]" /> Точность (%)
                </span>
                <span className="flex items-center gap-1.5 text-[#8549BA]">
                  <span className="w-3 h-3 rounded-full bg-[#CE82FF]" /> Сложность Tier
                </span>
              </div>
            </div>
          )}
        </div>

        {/* Error Pattern Diagnostics */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-5">
          <h3 className="text-base font-black text-[#3C3C3C] mb-1 flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-[#DC143C]" />
            Диагностика ошибок
          </h3>
          <p className="text-xs font-bold text-[#777777] mb-4">
            Что локальный ИИ корректирует в ваших уроках
          </p>

          {totalErrors === 0 ? (
            <div className="h-36 rounded-xl bg-[#F3FFE8] border border-[#58CC02]/30 flex flex-col items-center justify-center text-center p-4">
              <CheckCircle2 className="w-7 h-7 text-[#58CC02] mb-1.5" />
              <p className="text-xs font-extrabold text-[#46A302]">
                Ошибок пока не зафиксировано — так держать!
              </p>
            </div>
          ) : (
            <div className="space-y-2.5">
              {(Object.keys(ERROR_LABELS) as ErrorKind[]).map((ek) => {
                const count = state.errorCounts[ek] ?? 0;
                const pct = Math.round((count / totalErrors) * 100);
                return (
                  <div key={ek} className="space-y-1">
                    <div className="flex justify-between text-xs font-extrabold">
                      <span className="text-[#4B4B4B]">{ERROR_LABELS[ek].label}</span>
                      <span className="text-[#3C3C3C]">{count}</span>
                    </div>
                    <div className="h-2.5 rounded-full bg-[#F1F5F9] overflow-hidden">
                      <div
                        className="h-full rounded-full"
                        style={{
                          width: `${pct}%`,
                          backgroundColor: ERROR_LABELS[ek].color,
                        }}
                      />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* Live AI Journal & Word Memory Table */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* AI Thoughts Log */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-5 space-y-3">
          <h3 className="text-base font-black text-[#3C3C3C]">Журнал наблюдений ИИ</h3>
          <div className="space-y-2 max-h-60 overflow-y-auto pr-1">
            {state.aiThoughts.slice(0, 6).map((t) => (
              <div
                key={t.id}
                className="rounded-xl bg-[#F7F7F7] border border-[#E5E5E5] p-3 text-xs font-bold text-[#4B4B4B]"
              >
                {t.message}
              </div>
            ))}
          </div>
        </div>

        {/* Memory Retention Words */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-5 space-y-3">
          <h3 className="text-base font-black text-[#3C3C3C]">
            Память слов (FSRS интервалы)
          </h3>
          {memoryEntries.length === 0 ? (
            <p className="text-xs font-bold text-[#777777] py-6 text-center">
              Слова появятся здесь после первых упражнений.
            </p>
          ) : (
            <div className="space-y-2 max-h-60 overflow-y-auto pr-1">
              {memoryEntries.slice(0, 8).map(({ mem, lex }) => (
                <div
                  key={mem.id}
                  className="flex items-center justify-between gap-3 rounded-xl border border-[#E5E5E5] px-3 py-2"
                >
                  <div className="min-w-0">
                    <div className="text-sm font-black text-[#3C3C3C] truncate">
                      {lex!.pl} <span className="font-bold text-[#777777]">— {lex!.ru}</span>
                    </div>
                    <div className="text-[11px] font-bold text-[#AFAFAF]">
                      Попыток: {mem.attempts} · Ошибок: {mem.mistakes}
                    </div>
                  </div>
                  <div className="shrink-0 text-right">
                    <span
                      className={`text-xs font-black px-2 py-0.5 rounded-lg ${
                        mem.strength >= 0.7
                          ? 'bg-[#D7FFB8] text-[#46A302]'
                          : mem.strength >= 0.45
                            ? 'bg-[#FFF0D6] text-[#FF9600]'
                            : 'bg-[#FFE4E8] text-[#DC143C]'
                      }`}
                    >
                      {Math.round(mem.strength * 100)}%
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Reset Control */}
      <div className="flex justify-end pt-2">
        <button
          onClick={onResetProgress}
          className="inline-flex items-center gap-2 text-xs font-extrabold text-[#AFAFAF] hover:text-[#DC143C] cursor-pointer px-3 py-2 rounded-xl hover:bg-[#FFE4E8]/50"
        >
          <RotateCcw className="w-4 h-4" />
          Сбросить весь прогресс и статистику ИИ
        </button>
      </div>
    </div>
  );
};
