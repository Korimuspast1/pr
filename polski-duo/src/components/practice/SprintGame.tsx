import React, { useEffect, useMemo, useState } from 'react';
import { ArrowLeft, Flame, Timer, Trophy, Zap } from 'lucide-react';
import { LEXICON } from '../../data/polishLexicon';
import { LearnerAIState } from '../../types/learning';
import { playUiSound, speakPolish } from '../../utils/sound';

interface SprintGameProps {
  state: LearnerAIState;
  onFinishSprint: (score: number, xpEarned: number, gemsEarned: number) => void;
  onBack: () => void;
}

interface SprintQuestion {
  pl: string;
  correctRu: string;
  options: string[];
}

function makeSprintQuestion(tierCap: number): SprintQuestion {
  const pool = LEXICON.filter((l) => l.tier <= Math.max(4, tierCap + 2));
  const item = pool[Math.floor(Math.random() * pool.length)];
  const distractors = LEXICON.filter((l) => l.id !== item.id)
    .sort(() => Math.random() - 0.5)
    .slice(0, 3)
    .map((d) => d.ru);
  const options = [item.ru, ...distractors].sort(() => Math.random() - 0.5);
  return { pl: item.pl, correctRu: item.ru, options };
}

export const SprintGame: React.FC<SprintGameProps> = ({
  state,
  onFinishSprint,
  onBack,
}) => {
  const [phase, setPhase] = useState<'ready' | 'playing' | 'done'>('ready');
  const [secondsLeft, setSecondsLeft] = useState(60);
  const [score, setScore] = useState(0);
  const [streak, setStreak] = useState(0);
  const [question, setQuestion] = useState<SprintQuestion>(() => makeSprintQuestion(state.currentTier));
  const [flash, setFlash] = useState<'ok' | 'bad' | null>(null);

  useEffect(() => {
    if (phase !== 'playing') return;
    if (secondsLeft <= 0) {
      setPhase('done');
      const xp = score * 3;
      const gems = Math.min(40, Math.max(5, Math.floor(score / 2)));
      playUiSound('complete', state.soundEnabled);
      onFinishSprint(score, xp, gems);
      return;
    }
    const t = window.setTimeout(() => setSecondsLeft((s) => s - 1), 1000);
    return () => window.clearTimeout(t);
  }, [phase, secondsLeft]);

  const start = () => {
    setSecondsLeft(60);
    setScore(0);
    setStreak(0);
    setQuestion(makeSprintQuestion(state.currentTier));
    setPhase('playing');
  };

  const answer = (opt: string) => {
    if (phase !== 'playing') return;
    if (opt === question.correctRu) {
      const nextStreak = streak + 1;
      const pts = 1 + Math.floor(nextStreak / 4);
      setScore((s) => s + pts);
      setStreak(nextStreak);
      setFlash('ok');
      playUiSound('correct', state.soundEnabled);
    } else {
      setStreak(0);
      setFlash('bad');
      playUiSound('wrong', state.soundEnabled);
    }
    setTimeout(() => setFlash(null), 180);
    const nextQ = makeSprintQuestion(state.currentTier);
    setQuestion(nextQ);
  };

  const best = useMemo(() => Math.max(state.sprintBestScore, score), [state.sprintBestScore, score]);

  if (phase === 'ready') {
    return (
      <div className="space-y-4">
        <button onClick={onBack} className="inline-flex items-center gap-2 text-sm font-black text-[#FF9600] cursor-pointer">
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>

        <div className="rounded-3xl border-2 border-[#FF9600] bg-[#FFF8EB] p-6 text-center space-y-4">
          <Timer className="w-14 h-14 text-[#FF9600] mx-auto" />
          <h2 className="text-2xl font-black text-[#3C3C3C]">Спринт на 60 секунд!</h2>
          <p className="text-sm font-bold text-[#64748B]">
            Переведите максимум польских слов за минуту. Серия правильных ответов даёт множитель очков!
          </p>
          <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-white border border-[#FFC800] text-sm font-black text-[#B37800]">
            <Trophy className="w-4 h-4 text-[#FFC800]" /> Ваш рекорд: {state.sprintBestScore} очков
          </div>
          <div>
            <button onClick={start} className="duo-btn duo-btn-green rounded-2xl px-8 py-4 font-black text-lg">
              СТАРТ · 60 СЕК
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (phase === 'done') {
    const isNewRecord = score > state.sprintBestScore;
    return (
      <div className="rounded-3xl border-2 border-[#58CC02] bg-white p-6 text-center space-y-4">
        <Trophy className="w-14 h-14 text-[#FFC800] mx-auto" />
        <h2 className="text-2xl font-black text-[#3C3C3C]">
          {isNewRecord ? 'НОВЫЙ РЕКОРД!' : 'Финиш спринта!'}
        </h2>
        <div className="text-4xl font-black text-[#58CC02]">{score} очков</div>
        <p className="text-sm font-bold text-[#64748B]">
          Лучший результат: {best} · Получено +{score * 3} XP и +{Math.min(40, Math.max(5, Math.floor(score / 2)))} 💎
        </p>
        <div className="flex justify-center gap-3">
          <button onClick={start} className="duo-btn duo-btn-green rounded-xl px-5 py-2.5 text-sm font-black">
            Ещё забег!
          </button>
          <button onClick={onBack} className="duo-btn duo-btn-outline rounded-xl px-4 py-2.5 text-sm font-black">
            В Практикум
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between bg-white rounded-2xl border-2 border-[#E5E5E5] px-4 py-3">
        <div className={`flex items-center gap-2 font-black text-lg ${secondsLeft <= 10 ? 'text-[#DC143C] animate-pulse' : 'text-[#3C3C3C]'}`}>
          <Timer className="w-5 h-5 text-[#FF9600]" /> {secondsLeft}с
        </div>
        <div className="flex items-center gap-2 font-black text-sm text-[#FF9600]">
          <Flame className="w-4 h-4 fill-[#FF9600]" /> Серия {streak}
        </div>
        <div className="flex items-center gap-1.5 font-black text-lg text-[#58CC02]">
          <Zap className="w-5 h-5 fill-[#58CC02]" /> {score}
        </div>
      </div>

      <div
        className={`rounded-3xl border-2 p-6 text-center space-y-5 transition-colors ${
          flash === 'ok'
            ? 'bg-[#D7FFB8] border-[#58CC02]'
            : flash === 'bad'
              ? 'bg-[#FFE4E8] border-[#DC143C]'
              : 'bg-white border-[#E5E5E5]'
        }`}
      >
        <button
          type="button"
          onClick={() => speakPolish(question.pl)}
          className="text-3xl sm:text-4xl font-black text-[#3C3C3C] hover:text-[#1899D6] cursor-pointer"
        >
          {question.pl}
        </button>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {question.options.map((opt) => (
            <button
              key={opt}
              onClick={() => answer(opt)}
              className="rounded-2xl border-2 border-b-4 border-[#E5E5E5] bg-white hover:bg-[#DDF4FF] hover:border-[#1CB0F6] py-3.5 px-4 font-extrabold text-sm sm:text-base cursor-pointer active:translate-y-[1px]"
            >
              {opt}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
};
