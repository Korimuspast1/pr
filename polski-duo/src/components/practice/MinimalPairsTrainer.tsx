import React, { useState } from 'react';
import { ArrowLeft, CheckCircle2, Ear, Mic, Square, Volume2 } from 'lucide-react';
import { analyzePolishSpeech, SpeechAiReport } from '../../ai/speechAiEngine';
import { MINIMAL_PAIRS } from '../../data/practiceData';
import { LearnerAIState } from '../../types/learning';
import { playUiSound, speakPolish } from '../../utils/sound';
import { cancelPolishListening, startPolishListening, stopPolishListening } from '../../utils/speechInput';

interface MinimalPairsTrainerProps {
  state: LearnerAIState;
  onReward: (xp: number, gems: number, label: string) => void;
  onBack: () => void;
}

export const MinimalPairsTrainer: React.FC<MinimalPairsTrainerProps> = ({
  state,
  onReward,
  onBack,
}) => {
  const [idx, setIdx] = useState(0);
  const [targetSide, setTargetSide] = useState<'A' | 'B'>('A');
  const [heardPick, setHeardPick] = useState<'A' | 'B' | null>(null);
  const [micListening, setMicListening] = useState(false);
  const [speechReport, setSpeechReport] = useState<SpeechAiReport | null>(null);
  const [micError, setMicError] = useState<string | null>(null);

  const pair = MINIMAL_PAIRS[idx];
  const targetWord = targetSide === 'A' ? pair.wordA : pair.wordB;

  const playTarget = (slow = false) => {
    speakPolish(targetWord.pl, slow);
  };

  const handleEarPick = (side: 'A' | 'B') => {
    if (heardPick) return;
    setHeardPick(side);
    const ok = side === targetSide;
    playUiSound(ok ? 'correct' : 'wrong', state.soundEnabled);
    if (ok) {
      onReward(6, 2, `Трудные пары: ${pair.contrastTitle}`);
    }
  };

  const handleMic = () => {
    if (micListening) {
      stopPolishListening();
      setMicListening(false);
      return;
    }
    setMicError(null);
    setSpeechReport(null);
    setMicListening(true);
    startPolishListening({
      onResult: (hyps, confs) => {
        setMicListening(false);
        const rep = analyzePolishSpeech(targetWord.pl, hyps, confs);
        setSpeechReport(rep);
        playUiSound(rep.isPass ? 'correct' : 'wrong', state.soundEnabled);
        if (rep.isPass) {
          onReward(10, 4, `Произношение пары: ${targetWord.pl}`);
        }
      },
      onError: (_c, msg) => {
        setMicListening(false);
        setMicError(msg);
      },
    });
  };

  const nextPair = () => {
    cancelPolishListening();
    setHeardPick(null);
    setSpeechReport(null);
    setMicError(null);
    setTargetSide(Math.random() < 0.5 ? 'A' : 'B');
    setIdx((i) => (i + 1) % MINIMAL_PAIRS.length);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <button onClick={onBack} className="inline-flex items-center gap-2 text-sm font-black text-[#DC143C] cursor-pointer">
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>
        <span className="text-xs font-black px-3 py-1 rounded-full bg-[#FFE4E8] text-[#DC143C]">
          Пара {idx + 1}/{MINIMAL_PAIRS.length} · {pair.contrastTitle}
        </span>
      </div>

      <div className="rounded-3xl border-2 border-[#E5E5E5] bg-white p-5 space-y-5">
        {/* Step 1: Ear Discrimination */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-xs font-black uppercase text-[#1899D6] flex items-center gap-1.5">
              <Ear className="w-4 h-4" /> Шаг 1 · Угадайте слово на слух
            </span>
            <div className="flex gap-2">
              <button onClick={() => playTarget(false)} className="duo-btn duo-btn-blue rounded-xl px-3 py-2 text-xs font-black flex items-center gap-1.5">
                <Volume2 className="w-4 h-4" /> Слушать
              </button>
              <button onClick={() => playTarget(true)} className="duo-btn duo-btn-outline rounded-xl px-3 py-2 text-xs font-black">
                0.65x
              </button>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            {(['A', 'B'] as const).map((side) => {
              const w = side === 'A' ? pair.wordA : pair.wordB;
              const isRight = heardPick && side === targetSide;
              const isWrong = heardPick === side && side !== targetSide;
              return (
                <button
                  key={side}
                  onClick={() => handleEarPick(side)}
                  className={`rounded-2xl border-2 border-b-4 p-4 text-center cursor-pointer ${
                    isRight
                      ? 'bg-[#D7FFB8] border-[#58CC02] text-[#46A302]'
                      : isWrong
                        ? 'bg-[#FFE4E8] border-[#DC143C] text-[#DC143C]'
                        : 'bg-white border-[#E5E5E5] hover:bg-[#F7F7F7]'
                  }`}
                >
                  <div className="text-xl font-black">{w.pl}</div>
                  <div className="text-xs font-bold text-[#1899D6]">[{w.phoneticRu}]</div>
                  <div className="text-xs font-bold text-[#777777] mt-0.5">{w.ru}</div>
                </button>
              );
            })}
          </div>
        </div>

        {/* Tip */}
        <div className="rounded-2xl bg-[#FFFDF2] border border-[#FFC800] p-3.5 text-xs font-bold text-[#4B4B4B]">
          💡 {pair.tipRu}
        </div>

        {/* Step 2: Say the target word into the mic */}
        <div className="rounded-2xl border-2 border-[#DDF4FF] bg-[#F7FCFF] p-4 space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-xs font-black uppercase text-[#1899D6]">
              Шаг 2 · Теперь скажите в микрофон: «{targetWord.pl}»
            </div>
            <button
              type="button"
              onClick={handleMic}
              className={`px-4 py-2 rounded-xl text-xs font-black flex items-center gap-1.5 cursor-pointer ${
                micListening ? 'bg-[#DC143C] text-white' : 'duo-btn duo-btn-blue'
              }`}
            >
              {micListening ? <Square className="w-4 h-4 fill-white" /> : <Mic className="w-4 h-4" />}
              {micListening ? 'Стоп' : 'Говорить'}
            </button>
          </div>

          {speechReport && (
            <div className="flex items-center gap-2 text-xs font-black text-[#46A302]">
              <CheckCircle2 className="w-4 h-4" />
              ИИ-оценка: {speechReport.overallScore}% — распознано «{speechReport.bestTranscript}»
            </div>
          )}
          {micError && <div className="text-xs font-bold text-[#DC143C]">{micError}</div>}
        </div>

        <button onClick={nextPair} className="w-full duo-btn duo-btn-green rounded-2xl py-3 text-sm font-black uppercase">
          Следующая трудная пара →
        </button>
      </div>
    </div>
  );
};
