import React, { useMemo, useState } from 'react';
import { ArrowLeft, CheckCircle2, Layers, RotateCw, Volume2 } from 'lucide-react';
import { applyFlashcardReview } from '../../ai/localAiEngine';
import { LEXICON, SKILL_META } from '../../data/polishLexicon';
import { LearnerAIState } from '../../types/learning';
import { playUiSound, speakPolish } from '../../utils/sound';

interface FlashcardsReviewProps {
  state: LearnerAIState;
  onUpdateState: (updater: (prev: LearnerAIState) => LearnerAIState) => void;
  onBack: () => void;
}

export const FlashcardsReview: React.FC<FlashcardsReviewProps> = ({
  state,
  onUpdateState,
  onBack,
}) => {
  const deck = useMemo(() => {
    const now = Date.now();
    const sorted = [...LEXICON].sort((a, b) => {
      const ma = state.itemMemory[a.id];
      const mb = state.itemMemory[b.id];
      const dueA = ma ? (ma.nextReviewAt <= now ? -2 : ma.strength) : -0.5;
      const dueB = mb ? (mb.nextReviewAt <= now ? -2 : mb.strength) : -0.5;
      return dueA - dueB;
    });
    return sorted.slice(0, 12);
  }, []);

  const [idx, setIdx] = useState(0);
  const [flipped, setFlipped] = useState(false);
  const [reviewedCount, setReviewedCount] = useState(0);

  const card = deck[idx];
  const finished = idx >= deck.length;

  const handleRate = (grade: 0 | 1 | 2 | 3) => {
    if (!card) return;
    playUiSound(grade === 0 ? 'wrong' : 'correct', state.soundEnabled);
    onUpdateState((prev) => applyFlashcardReview(prev, card.id, card.skill, grade));
    setReviewedCount((c) => c + 1);
    setFlipped(false);
    setIdx((i) => i + 1);
    const nextCard = deck[idx + 1];
    if (nextCard) {
      speakPolish(nextCard.pl);
    }
  };

  if (finished || !card) {
    return (
      <div className="rounded-3xl border-2 border-[#CE82FF] bg-white p-6 text-center space-y-4">
        <CheckCircle2 className="w-14 h-14 text-[#58CC02] mx-auto" />
        <h2 className="text-2xl font-black text-[#3C3C3C]">Вечернее повторение FSRS завершено!</h2>
        <p className="text-sm font-bold text-[#64748B]">
          Повторено карточек: {reviewedCount}. Интервалы памяти в ИИ обновлены.
        </p>
        <div className="flex justify-center gap-3">
          <button
            onClick={() => {
              setIdx(0);
              setReviewedCount(0);
              setFlipped(false);
            }}
            className="duo-btn duo-btn-outline rounded-xl px-4 py-2.5 text-sm font-black"
          >
            Ещё 12 карточек
          </button>
          <button onClick={onBack} className="duo-btn duo-btn-purple rounded-xl px-5 py-2.5 text-sm font-black">
            В Практикум
          </button>
        </div>
      </div>
    );
  }

  const mem = state.itemMemory[card.id];
  const strengthPct = Math.round((mem?.strength ?? 0.3) * 100);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <button onClick={onBack} className="inline-flex items-center gap-2 text-sm font-black text-[#8549BA] cursor-pointer">
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>
        <span className="text-xs font-black px-3 py-1 rounded-full bg-[#FAF2FF] text-[#8549BA] flex items-center gap-1.5">
          <Layers className="w-3.5 h-3.5" /> Карточка {idx + 1}/{deck.length} · Память {strengthPct}%
        </span>
      </div>

      {/* Flip Card */}
      <div
        onClick={() => {
          setFlipped((f) => !f);
          if (!flipped) speakPolish(card.pl);
        }}
        className="rounded-3xl border-2 border-b-[6px] border-[#CE82FF] bg-white p-7 min-h-[270px] flex flex-col items-center justify-center text-center cursor-pointer transition-transform active:scale-[0.99] space-y-3"
      >
        <span
          className="text-[11px] font-black uppercase px-2.5 py-0.5 rounded-lg text-white"
          style={{ backgroundColor: SKILL_META[card.skill].color }}
        >
          {SKILL_META[card.skill].shortLabel}
        </span>

        <div className="flex items-center gap-3">
          <div className="text-3xl sm:text-4xl font-black text-[#3C3C3C]">{card.pl}</div>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              speakPolish(card.pl);
            }}
            className="w-10 h-10 rounded-xl duo-btn duo-btn-blue flex items-center justify-center"
          >
            <Volume2 className="w-5 h-5" />
          </button>
        </div>

        <div className="text-sm font-extrabold text-[#1899D6]">[{card.phoneticRu}]</div>

        {flipped ? (
          <div className="pt-3 border-t border-[#E5E5E5] w-full space-y-2 animate-pop">
            <div className="text-2xl font-black text-[#58CC02]">{card.ru}</div>
            {card.examplePl && (
              <div className="text-xs font-bold text-[#64748B]">
                «{card.examplePl}» — {card.exampleRu}
              </div>
            )}
            {card.falseFriendNote && (
              <div className="text-xs font-bold text-[#DC143C]">{card.falseFriendNote}</div>
            )}
          </div>
        ) : (
          <div className="text-xs font-extrabold text-[#AFAFAF] flex items-center gap-1.5 pt-4">
            <RotateCw className="w-4 h-4" /> Нажмите на карточку, чтобы увидеть перевод
          </div>
        )}
      </div>

      {/* FSRS Grade Buttons */}
      {flipped && (
        <div className="grid grid-cols-4 gap-2">
          <button
            onClick={() => handleRate(0)}
            className="rounded-2xl border-2 border-b-4 border-[#DC143C] bg-[#FFE4E8] py-3 px-2 text-center font-black text-xs text-[#A30D2B] cursor-pointer"
          >
            Забыл
            <span className="block text-[10px] font-bold opacity-80">5 мин</span>
          </button>
          <button
            onClick={() => handleRate(1)}
            className="rounded-2xl border-2 border-b-4 border-[#FF9600] bg-[#FFF0D6] py-3 px-2 text-center font-black text-xs text-[#CC7800] cursor-pointer"
          >
            Трудно
            <span className="block text-[10px] font-bold opacity-80">3 ч</span>
          </button>
          <button
            onClick={() => handleRate(2)}
            className="rounded-2xl border-2 border-b-4 border-[#1CB0F6] bg-[#DDF4FF] py-3 px-2 text-center font-black text-xs text-[#1899D6] cursor-pointer"
          >
            Помню
            <span className="block text-[10px] font-bold opacity-80">1 дн</span>
          </button>
          <button
            onClick={() => handleRate(3)}
            className="rounded-2xl border-2 border-b-4 border-[#58CC02] bg-[#D7FFB8] py-3 px-2 text-center font-black text-xs text-[#46A302] cursor-pointer"
          >
            Легко
            <span className="block text-[10px] font-bold opacity-80">3+ дн</span>
          </button>
        </div>
      )}
    </div>
  );
};
