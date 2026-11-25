import React from 'react';
import { BrainCircuit, Flame, Gem, Heart, Sparkles, Target, Zap } from 'lucide-react';
import {
  computeCefrLevel,
  computeGlobalMasteryScore,
  getDueReviewCount,
  getStrongestSkill,
  getWeakestSkill,
} from '../ai/localAiEngine';
import { SKILL_META } from '../data/polishLexicon';
import { LearnerAIState } from '../types/learning';
import { MascotOrzel, PolishFlagBadge } from './MascotOrzel';

interface RightPanelProps {
  state: LearnerAIState;
  onStartSmartLesson: () => void;
  onOpenInfinite: () => void;
  onOpenStats: () => void;
}

export const RightPanel: React.FC<RightPanelProps> = ({
  state,
  onStartSmartLesson,
  onOpenInfinite,
  onOpenStats,
}) => {
  const score = computeGlobalMasteryScore(state);
  const cefr = computeCefrLevel(state);
  const weakest = getWeakestSkill(state);
  const strongest = getStrongestSkill(state);
  const dueReviews = getDueReviewCount(state);
  const dailyPct = Math.min(100, Math.round((state.dailyXp / state.dailyGoalXp) * 100));
  const latestThought = state.aiThoughts[0];

  return (
    <aside className="hidden lg:flex flex-col w-[340px] shrink-0 px-5 py-6 gap-5 sticky top-0 h-screen overflow-y-auto">
      {/* Duolingo Status Strip */}
      <div className="flex items-center justify-between gap-2 bg-white px-2 py-1">
        <div className="flex items-center gap-1.5" title="Изучаемый язык: Польский">
          <PolishFlagBadge className="w-7 h-5" />
          <span className="text-xs font-black px-2 py-0.5 rounded-lg bg-[#FFE4E8] text-[#DC143C]">
            {cefr}
          </span>
        </div>

        <div className="flex items-center gap-1 font-extrabold text-[#FF9600]" title="Ударный режим">
          <Flame className="w-5 h-5 fill-[#FF9600]" />
          <span>{state.streakDays}</span>
        </div>

        <div className="flex items-center gap-1 font-extrabold text-[#1CB0F6]" title="Кристаллы">
          <Gem className="w-5 h-5 fill-[#1CB0F6]" />
          <span>{state.gems}</span>
        </div>

        <div className="flex items-center gap-1 font-extrabold text-[#FF4B4B]" title="Жизни">
          <Heart className="w-5 h-5 fill-[#FF4B4B]" />
          <span>{state.hearts}</span>
        </div>
      </div>

      {/* Live Local AI Analyzer Card */}
      <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4 shadow-xs">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <div className="w-9 h-9 rounded-xl bg-[#F5E8FF] flex items-center justify-center">
              <BrainCircuit className="w-5 h-5 text-[#A55EEA]" />
            </div>
            <div>
              <h3 className="text-[15px] font-black text-[#3C3C3C] leading-tight">
                Локальный ИИ-аналитик
              </h3>
              <p className="text-[12px] font-bold text-[#777777]">
                Сложность: Tier {state.currentTier.toFixed(1)} · {score}/1000
              </p>
            </div>
          </div>
          <MascotOrzel mood="ai" size={44} />
        </div>

        {latestThought && (
          <div className="rounded-xl bg-[#F7F7F7] border border-[#E5E5E5] p-3 mb-3">
            <p className="text-[13px] font-bold text-[#4B4B4B] leading-snug">
              «{latestThought.message}»
            </p>
          </div>
        )}

        <div className="grid grid-cols-2 gap-2 mb-3 text-xs">
          <div className="rounded-xl bg-[#D7FFB8]/50 border border-[#58CC02]/30 p-2.5">
            <div className="text-[#46A302] font-extrabold uppercase text-[10px]">Сильная зона</div>
            <div className="font-black text-[#3C3C3C] truncate mt-0.5">
              {SKILL_META[strongest].shortLabel} ({Math.round(state.skillMastery[strongest] * 100)}%)
            </div>
          </div>
          <div className="rounded-xl bg-[#FFE4E8]/70 border border-[#DC143C]/25 p-2.5">
            <div className="text-[#DC143C] font-extrabold uppercase text-[10px]">Фокус ИИ</div>
            <div className="font-black text-[#3C3C3C] truncate mt-0.5">
              {SKILL_META[weakest].shortLabel} ({Math.round(state.skillMastery[weakest] * 100)}%)
            </div>
          </div>
        </div>

        <button
          onClick={onStartSmartLesson}
          className="w-full duo-btn duo-btn-purple rounded-xl py-2.5 px-4 font-black text-sm flex items-center justify-center gap-2 mb-2"
        >
          <Sparkles className="w-4 h-4" />
          УМНЫЙ ИИ-УРОК ({dueReviews > 0 ? `${dueReviews} на повторе` : 'АДАПТИВНЫЙ'})
        </button>

        <button
          onClick={onOpenStats}
          className="w-full text-center text-xs font-extrabold text-[#1CB0F6] hover:underline py-1 cursor-pointer"
        >
          Открыть полную статистику ИИ →
        </button>
      </div>

      {/* Daily Goal & Quests */}
      <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-[16px] font-black text-[#3C3C3C]">Задания дня</h3>
          <span className="text-xs font-extrabold text-[#FF9600]">
            {state.dailyXp} / {state.dailyGoalXp} XP
          </span>
        </div>

        <div className="space-y-3">
          <div>
            <div className="flex items-center justify-between text-xs font-extrabold text-[#4B4B4B] mb-1">
              <span className="flex items-center gap-1.5">
                <Zap className="w-4 h-4 text-[#FFC800] fill-[#FFC800]" />
                Заработайте {state.dailyGoalXp} XP
              </span>
              <span>{dailyPct}%</span>
            </div>
            <div className="h-3 rounded-full bg-[#E5E5E5] overflow-hidden">
              <div
                className="h-full rounded-full bg-[#FFC800] transition-all duration-500"
                style={{ width: `${dailyPct}%` }}
              />
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between text-xs font-extrabold text-[#4B4B4B] mb-1">
              <span className="flex items-center gap-1.5">
                <Target className="w-4 h-4 text-[#58CC02]" />
                Пройдите 2 урока с ИИ
              </span>
              <span>{Math.min(2, state.lessonsCompleted)}/2</span>
            </div>
            <div className="h-3 rounded-full bg-[#E5E5E5] overflow-hidden">
              <div
                className="h-full rounded-full bg-[#58CC02] transition-all duration-500"
                style={{ width: `${Math.min(100, (state.lessonsCompleted / 2) * 100)}%` }}
              />
            </div>
          </div>
        </div>

        <button
          onClick={onOpenInfinite}
          className="mt-4 w-full duo-btn duo-btn-outline rounded-xl py-2.5 text-xs font-black tracking-wide uppercase text-[#4B4B4B]"
        >
          ∞ Запустить бесконечный режим
        </button>
      </div>
    </aside>
  );
};
