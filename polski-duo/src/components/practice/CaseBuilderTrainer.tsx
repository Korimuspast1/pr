import React, { useMemo, useState } from 'react';
import { ArrowLeft, CheckCircle2, Puzzle, Volume2 } from 'lucide-react';
import { CASE_BUILDER_ITEMS } from '../../data/practiceData';
import { LearnerAIState, PolishCase } from '../../types/learning';
import { playUiSound, speakPolish } from '../../utils/sound';

interface CaseBuilderTrainerProps {
  state: LearnerAIState;
  onReward: (xp: number, gems: number, label: string) => void;
  onBack: () => void;
}

const CASE_ORDER: Array<{ id: PolishCase; ruHint: string }> = [
  { id: 'Mianownik', ruHint: 'Кто? Что?' },
  { id: 'Dopełniacz', ruHint: 'Кого? Чего? (нет / для / до)' },
  { id: 'Biernik', ruHint: 'Кого? Что? (вижу / пью / хочу)' },
  { id: 'Narzędnik', ruHint: 'С кем? С чем? (с / быть кем-то)' },
  { id: 'Miejscownik', ruHint: 'О ком? Где? (в / на / о)' },
];

export const CaseBuilderTrainer: React.FC<CaseBuilderTrainerProps> = ({
  state,
  onReward,
  onBack,
}) => {
  const [wordIdx, setWordIdx] = useState(0);
  const [caseIdx, setCaseIdx] = useState(1); // Start with Dopełniacz
  const [picked, setPicked] = useState<string | null>(null);
  const [correctCount, setCorrectCount] = useState(0);

  const word = CASE_BUILDER_ITEMS[wordIdx];
  const currentCase = CASE_ORDER[caseIdx];
  const target = word.cases[currentCase.id];

  const allForms = useMemo(() => {
    const uniq = Array.from(new Set(Object.values(word.cases).map((c) => c.form)));
    return uniq.sort((a, b) => a.localeCompare(b));
  }, [word]);

  const handlePick = (form: string) => {
    if (picked) return;
    setPicked(form);
    const ok = form === target.form;
    playUiSound(ok ? 'correct' : 'wrong', state.soundEnabled);
    if (ok) {
      setCorrectCount((c) => c + 1);
      onReward(8, 2, `Падежи: ${word.cases.Mianownik.form}`);
    }
    speakPolish(target.context.replace('___', target.form));
  };

  const handleNext = () => {
    setPicked(null);
    if (caseIdx + 1 < CASE_ORDER.length) {
      setCaseIdx((i) => i + 1);
    } else {
      setCaseIdx(0);
      setWordIdx((w) => (w + 1) % CASE_BUILDER_ITEMS.length);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <button onClick={onBack} className="inline-flex items-center gap-2 text-sm font-black text-[#FF9600] cursor-pointer">
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>
        <span className="text-xs font-black px-3 py-1 rounded-full bg-[#FFF0D6] text-[#CC7800]">
          Верно собрано: {correctCount}
        </span>
      </div>

      {/* Word Tabs */}
      <div className="flex flex-wrap gap-2">
        {CASE_BUILDER_ITEMS.map((item, i) => (
          <button
            key={item.id}
            onClick={() => {
              setWordIdx(i);
              setCaseIdx(1);
              setPicked(null);
            }}
            className={`px-3 py-1.5 rounded-xl text-xs font-black border-2 cursor-pointer ${
              i === wordIdx
                ? 'bg-[#FF9600] border-[#CC7800] text-white'
                : 'bg-white border-[#E5E5E5] text-[#4B4B4B]'
            }`}
          >
            {item.cases.Mianownik.form} ({item.ru})
          </button>
        ))}
      </div>

      <div className="rounded-3xl border-2 border-[#E5E5E5] bg-white p-5 space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <span className="text-[11px] font-black uppercase text-[#FF9600] flex items-center gap-1">
              <Puzzle className="w-3.5 h-3.5" /> {word.genderLabel} · {currentCase.id}
            </span>
            <div className="text-xs font-bold text-[#777777]">{currentCase.ruHint}</div>
          </div>
          <button
            type="button"
            onClick={() => speakPolish(target.context.replace('___', target.form))}
            className="w-10 h-10 rounded-xl duo-btn duo-btn-blue flex items-center justify-center"
          >
            <Volume2 className="w-5 h-5" />
          </button>
        </div>

        {/* Context Sentence */}
        <div className="rounded-2xl bg-[#FFFDF9] border-2 border-[#FFE0A3] p-4 text-center space-y-1">
          <div className="text-xl sm:text-2xl font-black text-[#3C3C3C]">
            {target.context.split('___')[0]}
            <span className="inline-block min-w-[90px] border-b-4 border-[#FF9600] px-2 text-[#FF9600]">
              {picked ? target.form : '...'}
            </span>
            {target.context.split('___')[1]}
          </div>
          <div className="text-sm font-bold text-[#777777]">{target.ru}</div>
        </div>

        {/* Stem + Ending Breakdown */}
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-2.5">
          {allForms.map((form) => {
            const isRight = picked && form === target.form;
            const isWrong = picked === form && form !== target.form;
            return (
              <button
                key={form}
                disabled={Boolean(picked)}
                onClick={() => handlePick(form)}
                className={`rounded-2xl border-2 border-b-4 py-3 px-4 font-black text-base cursor-pointer ${
                  isRight
                    ? 'bg-[#D7FFB8] border-[#58CC02] text-[#46A302]'
                    : isWrong
                      ? 'bg-[#FFE4E8] border-[#DC143C] text-[#DC143C]'
                      : 'bg-white border-[#E5E5E5] hover:bg-[#FFF0D6]'
                }`}
              >
                {form}
              </button>
            );
          })}
        </div>

        {picked && (
          <div className="rounded-2xl bg-[#D7FFB8]/60 border border-[#58CC02] p-3.5 flex items-center justify-between gap-3">
            <div className="flex items-center gap-2 text-xs sm:text-sm font-bold text-[#3C3C3C]">
              <CheckCircle2 className="w-5 h-5 text-[#46A302] shrink-0" />
              <span>
                Основа + окончание: <strong>{word.stem}</strong> +{' '}
                <span className="px-1.5 py-0.5 rounded bg-[#FFC800] text-[#3C3C3C] font-black">
                  -{target.ending}
                </span>{' '}
                → {target.form}
              </span>
            </div>
            <button onClick={handleNext} className="duo-btn duo-btn-green rounded-xl px-4 py-2 text-xs font-black shrink-0">
              Дальше →
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
