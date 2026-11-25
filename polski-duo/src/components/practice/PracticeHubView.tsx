import React, { useState } from 'react';
import {
  Award,
  Ear,
  Layers,
  MessageCircle,
  Puzzle,
  ShoppingBag,
  Sparkles,
  Timer,
} from 'lucide-react';
import { getDueReviewCount } from '../../ai/localAiEngine';
import { LearnerAIState } from '../../types/learning';
import { MascotOrzel } from '../MascotOrzel';
import { CaseBuilderTrainer } from './CaseBuilderTrainer';
import { FlashcardsReview } from './FlashcardsReview';
import { KartaPolakaTrainer } from './KartaPolakaTrainer';
import { MinimalPairsTrainer } from './MinimalPairsTrainer';
import { RoleplayStudio } from './RoleplayStudio';
import { ShopView } from './ShopView';
import { SprintGame } from './SprintGame';

interface PracticeHubViewProps {
  state: LearnerAIState;
  onUpdateState: (updater: (prev: LearnerAIState) => LearnerAIState) => void;
}

type PracticeMode =
  | 'hub'
  | 'roleplay'
  | 'karta'
  | 'cards'
  | 'sprint'
  | 'cases'
  | 'pairs'
  | 'shop';

export const PracticeHubView: React.FC<PracticeHubViewProps> = ({
  state,
  onUpdateState,
}) => {
  const [mode, setMode] = useState<PracticeMode>('hub');
  const dueCount = getDueReviewCount(state);

  const awardPractice = (xp: number, gems: number, label: string) => {
    const mult = Date.now() < state.xpBoostUntil ? 2 : 1;
    onUpdateState((prev) => ({
      ...prev,
      xp: prev.xp + xp * mult,
      dailyXp: prev.dailyXp + xp * mult,
      gems: prev.gems + gems,
      aiThoughts: [
        {
          id: `pr_${Date.now()}`,
          timestamp: Date.now(),
          kind: 'boost' as const,
          message: `Практикум «${label}»: +${xp * mult} XP и +${gems} кристаллов!`,
        },
        ...prev.aiThoughts,
      ].slice(0, 18),
    }));
  };

  const handleFinishSprint = (score: number, xpEarned: number, gemsEarned: number) => {
    const mult = Date.now() < state.xpBoostUntil ? 2 : 1;
    onUpdateState((prev) => ({
      ...prev,
      xp: prev.xp + xpEarned * mult,
      dailyXp: prev.dailyXp + xpEarned * mult,
      gems: prev.gems + gemsEarned,
      sprintBestScore: Math.max(prev.sprintBestScore, score),
    }));
  };

  return (
    <div className="max-w-[760px] mx-auto pb-28 md:pb-16 pt-6 px-4 space-y-6">
      {mode === 'roleplay' && (
        <RoleplayStudio state={state} onReward={awardPractice} onBack={() => setMode('hub')} />
      )}
      {mode === 'karta' && (
        <KartaPolakaTrainer state={state} onReward={awardPractice} onBack={() => setMode('hub')} />
      )}
      {mode === 'cards' && (
        <FlashcardsReview state={state} onUpdateState={onUpdateState} onBack={() => setMode('hub')} />
      )}
      {mode === 'sprint' && (
        <SprintGame state={state} onFinishSprint={handleFinishSprint} onBack={() => setMode('hub')} />
      )}
      {mode === 'cases' && (
        <CaseBuilderTrainer state={state} onReward={awardPractice} onBack={() => setMode('hub')} />
      )}
      {mode === 'pairs' && (
        <MinimalPairsTrainer state={state} onReward={awardPractice} onBack={() => setMode('hub')} />
      )}
      {mode === 'shop' && (
        <ShopView state={state} onUpdateState={onUpdateState} onBack={() => setMode('hub')} />
      )}

      {mode === 'hub' && (
        <>
          <div className="rounded-3xl border-2 border-[#E5E5E5] bg-gradient-to-br from-[#F0FAFF] via-white to-[#FFF8EB] p-6 flex flex-col sm:flex-row items-center gap-5">
            <MascotOrzel mood="cheering" outfit={state.equippedOutfit} size={96} className="shrink-0" />
            <div className="text-center sm:text-left">
              <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-[#DDF4FF] text-[#1899D6] text-xs font-black uppercase mb-2">
                <Sparkles className="w-3.5 h-3.5" /> 7 спец-тренажёров польского
              </div>
              <h1 className="text-2xl sm:text-3xl font-black text-[#3C3C3C]">Практикум и Игры</h1>
              <p className="text-sm font-bold text-[#64748B] mt-1">
                Живые диалоги с ИИ, Карта Поляка, карточки перед сном, спринт на время, падежи, трудные пары и магазин костюмов.
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <button
              onClick={() => setMode('roleplay')}
              className="rounded-2xl border-2 border-b-4 border-[#84D8FF] bg-white hover:bg-[#F0FAFF] p-5 text-left cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="w-11 h-11 rounded-2xl bg-[#1CB0F6] text-white flex items-center justify-center">
                  <MessageCircle className="w-6 h-6" />
                </span>
                <span className="text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#DDF4FF] text-[#1899D6]">
                  ГОЛОС + NLU ИИ
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">Живые ИИ-диалоги</div>
              <p className="text-xs font-bold text-[#777777]">
                Кофейня, вокзал, Żabka и поликлиника: отвечайте в микрофон своими словами.
              </p>
            </button>

            <button
              onClick={() => setMode('karta')}
              className="rounded-2xl border-2 border-b-4 border-[#FFB3C1] bg-white hover:bg-[#FFF5F7] p-5 text-left cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="w-11 h-11 rounded-2xl bg-[#DC143C] text-white flex items-center justify-center">
                  <Award className="w-6 h-6" />
                </span>
                <span className="text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#FFE4E8] text-[#DC143C]">
                  СОБЕСЕДОВАНИЕ
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">Карта Поляка и культура</div>
              <p className="text-xs font-bold text-[#777777]">
                Традиции, праздники, история Польши и устные ответы консулу.
              </p>
            </button>

            <button
              onClick={() => setMode('cards')}
              className="rounded-2xl border-2 border-b-4 border-[#E5C2FF] bg-white hover:bg-[#FAF2FF] p-5 text-left cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="w-11 h-11 rounded-2xl bg-[#CE82FF] text-white flex items-center justify-center">
                  <Layers className="w-6 h-6" />
                </span>
                <span className="text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#FAF2FF] text-[#8549BA]">
                  {dueCount > 0 ? `${dueCount} НА ПОВТОР` : 'FSRS · 2 МИН'}
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">Карточки перед сном</div>
              <p className="text-xs font-bold text-[#777777]">
                Умные флэш-карточки по модели памяти FSRS с озвучкой.
              </p>
            </button>

            <button
              onClick={() => setMode('sprint')}
              className="rounded-2xl border-2 border-b-4 border-[#FFD280] bg-white hover:bg-[#FFF8EB] p-5 text-left cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="w-11 h-11 rounded-2xl bg-[#FF9600] text-white flex items-center justify-center">
                  <Timer className="w-6 h-6" />
                </span>
                <span className="text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#FFF0D6] text-[#CC7800]">
                  РЕКОРД: {state.sprintBestScore}
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">Спринт на 60 секунд</div>
              <p className="text-xs font-bold text-[#777777]">
                Скоростной блиц на перевод с множителем комбо и наградой в кристаллах.
              </p>
            </button>

            <button
              onClick={() => setMode('cases')}
              className="rounded-2xl border-2 border-b-4 border-[#BCE994] bg-white hover:bg-[#F3FFE8] p-5 text-left cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="w-11 h-11 rounded-2xl bg-[#58CC02] text-white flex items-center justify-center">
                  <Puzzle className="w-6 h-6" />
                </span>
                <span className="text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#D7FFB8] text-[#46A302]">
                  5 ПАДЕЖЕЙ
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">Конструктор окончаний</div>
              <p className="text-xs font-bold text-[#777777]">
                Отработка -ę / -y / -ą / -em / -ie с разбором основы слова.
              </p>
            </button>

            <button
              onClick={() => setMode('pairs')}
              className="rounded-2xl border-2 border-b-4 border-[#84D8FF] bg-white hover:bg-[#F0FAFF] p-5 text-left cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="w-11 h-11 rounded-2xl bg-[#1899D6] text-white flex items-center justify-center">
                  <Ear className="w-6 h-6" />
                </span>
                <span className="text-[11px] font-black px-2.5 py-1 rounded-lg bg-[#DDF4FF] text-[#1899D6]">
                  СЛУХ + МИКРОФОН
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">Трудные пары (czy / trzy)</div>
              <p className="text-xs font-bold text-[#777777]">
                Минимальные пары: czy/trzy, być/bić, kąt/kot, proszę/prosię.
              </p>
            </button>

            <button
              onClick={() => setMode('shop')}
              className="sm:col-span-2 rounded-2xl border-2 border-b-4 border-[#FFC800] bg-[#FFFDF2] hover:bg-[#FFF5CC] p-5 text-left cursor-pointer flex items-center justify-between gap-4"
            >
              <div className="flex items-center gap-4">
                <span className="w-12 h-12 rounded-2xl bg-[#FFC800] text-white flex items-center justify-center shrink-0">
                  <ShoppingBag className="w-6 h-6" />
                </span>
                <div>
                  <div className="text-lg font-black text-[#3C3C3C]">
                    Магазин Орла и Награды ({state.gems} 💎)
                  </div>
                  <p className="text-xs font-bold text-[#777777]">
                    Костюмы для маскота (Гусар, Бариста, Студент, Король), темы оформления и 2X XP бустер.
                  </p>
                </div>
              </div>
              <span className="text-xs font-black text-[#B37800] shrink-0">Открыть →</span>
            </button>
          </div>
        </>
      )}
    </div>
  );
};
