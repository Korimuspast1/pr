import React, { useState } from 'react';
import { ArrowLeft, Award, CheckCircle2, Mic, Square, Volume2 } from 'lucide-react';
import { analyzePolishSpeech, SpeechAiReport } from '../../ai/speechAiEngine';
import { KARTA_POLAKA_ITEMS } from '../../data/practiceData';
import { LearnerAIState } from '../../types/learning';
import { playUiSound, speakPolish } from '../../utils/sound';
import { cancelPolishListening, startPolishListening, stopPolishListening } from '../../utils/speechInput';

interface KartaPolakaTrainerProps {
  state: LearnerAIState;
  onReward: (xp: number, gems: number, label: string) => void;
  onBack: () => void;
}

export const KartaPolakaTrainer: React.FC<KartaPolakaTrainerProps> = ({
  state,
  onReward,
  onBack,
}) => {
  const [idx, setIdx] = useState(0);
  const [selectedOpt, setSelectedOpt] = useState<string | null>(null);
  const [micListening, setMicListening] = useState(false);
  const [speechReport, setSpeechReport] = useState<SpeechAiReport | null>(null);
  const [micError, setMicError] = useState<string | null>(null);
  const [scoreCount, setScoreCount] = useState(0);
  const [done, setDone] = useState(false);

  const item = KARTA_POLAKA_ITEMS[idx];

  const handleChoose = (opt: string) => {
    if (selectedOpt) return;
    setSelectedOpt(opt);
    const ok = opt === item.correctOption;
    if (ok) {
      playUiSound('correct', state.soundEnabled);
      setScoreCount((c) => c + 1);
    } else {
      playUiSound('wrong', state.soundEnabled);
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
        const rep = analyzePolishSpeech(item.sampleAnswerPl, hyps, confs);
        setSpeechReport(rep);
        if (rep.isPass) {
          playUiSound('correct', state.soundEnabled);
          if (!selectedOpt) {
            setSelectedOpt(item.correctOption);
            setScoreCount((c) => c + 1);
          }
        }
      },
      onError: (_c, msg) => {
        setMicListening(false);
        setMicError(msg);
      },
    });
  };

  const handleNext = () => {
    cancelPolishListening();
    setSelectedOpt(null);
    setSpeechReport(null);
    setMicError(null);
    if (idx + 1 >= KARTA_POLAKA_ITEMS.length) {
      setDone(true);
      onReward(40, 20, 'Интервью на Карту Поляка');
    } else {
      setIdx((i) => i + 1);
    }
  };

  if (done) {
    return (
      <div className="rounded-3xl border-2 border-[#DC143C] bg-white p-6 text-center space-y-4">
        <Award className="w-14 h-14 text-[#FFC800] mx-auto" />
        <h2 className="text-2xl font-black text-[#3C3C3C]">Собеседование с консулом пройдено!</h2>
        <p className="text-sm font-bold text-[#64748B]">
          Верных ответов: {scoreCount} из {KARTA_POLAKA_ITEMS.length}. Начислено +40 XP и +20 💎!
        </p>
        <div className="flex justify-center gap-3">
          <button
            onClick={() => {
              setIdx(0);
              setScoreCount(0);
              setDone(false);
            }}
            className="duo-btn duo-btn-outline rounded-xl px-4 py-2.5 text-sm font-black"
          >
            Пройти заново
          </button>
          <button onClick={onBack} className="duo-btn duo-btn-crimson rounded-xl px-5 py-2.5 text-sm font-black">
            В Практикум
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <button onClick={onBack} className="inline-flex items-center gap-2 text-sm font-black text-[#DC143C] cursor-pointer">
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>
        <span className="text-xs font-black px-3 py-1 rounded-full bg-[#FFE4E8] text-[#DC143C]">
          Карта Поляка · {idx + 1}/{KARTA_POLAKA_ITEMS.length}
        </span>
      </div>

      <div className="rounded-3xl border-2 border-[#E5E5E5] bg-white p-5 space-y-4">
        <div className="text-xs font-black uppercase text-[#DC143C]">{item.topic}</div>
        <div className="flex items-start gap-3">
          <button
            onClick={() => speakPolish(item.questionPl)}
            className="w-11 h-11 rounded-xl duo-btn duo-btn-blue flex items-center justify-center shrink-0"
            title="Слушать вопрос консула"
          >
            <Volume2 className="w-5 h-5" />
          </button>
          <div>
            <div className="text-lg font-black text-[#3C3C3C]">{item.questionPl}</div>
            <div className="text-xs font-bold text-[#777777] mt-1">{item.questionRu}</div>
          </div>
        </div>

        {/* Multiple choice */}
        <div className="grid grid-cols-1 gap-2.5">
          {item.options.map((opt) => {
            const isSel = selectedOpt === opt;
            const isRight = selectedOpt && opt === item.correctOption;
            return (
              <button
                key={opt}
                disabled={Boolean(selectedOpt)}
                onClick={() => handleChoose(opt)}
                className={`rounded-2xl border-2 border-b-4 p-3.5 text-left text-sm font-extrabold cursor-pointer ${
                  isRight
                    ? 'bg-[#D7FFB8] border-[#58CC02] text-[#46A302]'
                    : isSel
                      ? 'bg-[#FFE4E8] border-[#DC143C] text-[#DC143C]'
                      : 'bg-white border-[#E5E5E5] hover:bg-[#F7F7F7]'
                }`}
              >
                {opt}
              </button>
            );
          })}
        </div>

        {/* Voice Practice for Consul Answer */}
        <div className="rounded-2xl border-2 border-[#DDF4FF] bg-[#F7FCFF] p-4 space-y-2.5">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs font-black uppercase text-[#1899D6]">
              Тренировка устного ответа консулу:
            </span>
            <button
              onClick={() => speakPolish(item.sampleAnswerPl)}
              className="text-xs font-black text-[#1899D6] flex items-center gap-1 cursor-pointer"
            >
              <Volume2 className="w-4 h-4" /> Слушать образец
            </button>
          </div>
          <div className="text-sm font-black text-[#3C3C3C]">{item.sampleAnswerPl}</div>
          <div className="text-xs font-bold text-[#777777]">
            {item.sampleAnswerRu} · <span className="text-[#1899D6]">[{item.phoneticRu}]</span>
          </div>

          <div className="flex items-center gap-3 pt-1">
            <button
              type="button"
              onClick={handleMic}
              className={`px-4 py-2 rounded-xl text-xs font-black flex items-center gap-2 cursor-pointer ${
                micListening ? 'bg-[#DC143C] text-white' : 'duo-btn duo-btn-blue'
              }`}
            >
              {micListening ? <Square className="w-4 h-4 fill-white" /> : <Mic className="w-4 h-4" />}
              {micListening ? 'Остановить' : 'Ответить голосом'}
            </button>

            {speechReport && (
              <span className="text-xs font-black text-[#46A302]">
                Точность речи: {speechReport.overallScore}% ({speechReport.bestTranscript})
              </span>
            )}
          </div>
          {micError && <div className="text-xs font-bold text-[#DC143C]">{micError}</div>}
        </div>

        {selectedOpt && (
          <div className="rounded-2xl bg-[#FFFDF2] border border-[#FFC800] p-3.5 flex items-start gap-2.5">
            <CheckCircle2 className="w-5 h-5 text-[#E5A900] shrink-0 mt-0.5" />
            <div className="text-xs font-bold text-[#4B4B4B]">{item.factRu}</div>
          </div>
        )}

        {selectedOpt && (
          <button
            onClick={handleNext}
            className="w-full duo-btn duo-btn-green rounded-2xl py-3 font-black text-sm uppercase"
          >
            {idx + 1 >= KARTA_POLAKA_ITEMS.length ? 'Завершить интервью' : 'Следующий вопрос →'}
          </button>
        )}
      </div>
    </div>
  );
};
