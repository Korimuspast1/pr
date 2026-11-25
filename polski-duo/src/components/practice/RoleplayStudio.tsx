import React, { useEffect, useState } from 'react';
import {
  ArrowLeft,
  BrainCircuit,
  CheckCircle2,
  MapPin,
  MessageCircle,
  Mic,
  Send,
  Sparkles,
  Square,
  Volume2,
} from 'lucide-react';
import { analyzePolishSpeech, polishToPhonetic } from '../../ai/speechAiEngine';
import { ROLEPLAY_SCENARIOS, RoleplayBranch, RoleplayScenario } from '../../data/practiceData';
import { LearnerAIState } from '../../types/learning';
import { playUiSound, speakPolish } from '../../utils/sound';
import { cancelPolishListening, startPolishListening, stopPolishListening } from '../../utils/speechInput';
import { MascotOrzel } from '../MascotOrzel';

interface RoleplayStudioProps {
  state: LearnerAIState;
  onReward: (xp: number, gems: number, label: string) => void;
  onBack: () => void;
}

interface ChatBubble {
  sender: 'npc' | 'user' | 'ai';
  pl?: string;
  ru: string;
  phonetic?: string;
  score?: number;
}

function matchBranchNlu(
  spokenHypotheses: string[],
  branches: RoleplayBranch[],
): { branch: RoleplayBranch; score: number; bestTranscript: string } {
  let bestBranch = branches[0];
  let bestScore = -1;
  let bestTranscript = spokenHypotheses[0] ?? '';

  for (const hyp of spokenHypotheses) {
    const hypLower = hyp.toLowerCase();
    const hypPhon = polishToPhonetic(hypLower, true);

    for (const br of branches) {
      const speechReport = analyzePolishSpeech(br.samplePl, [hyp]);
      let kwHits = 0;
      for (const kw of br.keywords) {
        const kwPhon = polishToPhonetic(kw, true);
        if (hypLower.includes(kw.toLowerCase()) || (kwPhon.length >= 3 && hypPhon.includes(kwPhon))) {
          kwHits += 1;
        }
      }
      const nluScore = Math.min(100, Math.round(speechReport.overallScore * 0.65 + Math.min(45, kwHits * 20)));
      if (nluScore > bestScore) {
        bestScore = nluScore;
        bestBranch = br;
        bestTranscript = hyp;
      }
    }
  }

  return { branch: bestBranch, score: Math.max(35, bestScore), bestTranscript };
}

export const RoleplayStudio: React.FC<RoleplayStudioProps> = ({ state, onReward, onBack }) => {
  const [scenario, setScenario] = useState<RoleplayScenario | null>(null);
  const [turnIdx, setTurnIdx] = useState(0);
  const [chat, setChat] = useState<ChatBubble[]>([]);
  const [micListening, setMicListening] = useState(false);
  const [micPartial, setMicPartial] = useState('');
  const [micError, setMicError] = useState<string | null>(null);
  const [typedReply, setTypedReply] = useState('');
  const [completed, setCompleted] = useState(false);

  useEffect(() => {
    return () => cancelPolishListening();
  }, []);

  const startScenario = (sc: RoleplayScenario) => {
    setScenario(sc);
    setTurnIdx(0);
    setCompleted(false);
    setMicError(null);
    setTypedReply('');
    const firstTurn = sc.turns[0];
    setChat([
      {
        sender: 'npc',
        pl: firstTurn.npcLinePl,
        ru: firstTurn.npcLineRu,
        phonetic: firstTurn.npcPhoneticRu,
      },
    ]);
    speakPolish(firstTurn.npcLinePl);
  };

  const applyUserResponse = (hypotheses: string[], forcedBranch?: RoleplayBranch) => {
    if (!scenario || completed) return;
    const turn = scenario.turns[turnIdx];
    const matched = forcedBranch
      ? { branch: forcedBranch, score: 96, bestTranscript: forcedBranch.samplePl }
      : matchBranchNlu(hypotheses, turn.branches);

    const nextChat: ChatBubble[] = [
      ...chat,
      {
        sender: 'user',
        pl: matched.bestTranscript,
        ru: matched.branch.labelRu,
        score: matched.score,
      },
      {
        sender: 'ai',
        ru: matched.branch.feedbackRu,
      },
      {
        sender: 'npc',
        pl: matched.branch.aiReplyPl,
        ru: matched.branch.aiReplyRu,
      },
    ];

    playUiSound('correct', state.soundEnabled);
    speakPolish(matched.branch.aiReplyPl);

    if (turnIdx + 1 < scenario.turns.length) {
      const nextTurn = scenario.turns[turnIdx + 1];
      setChat([
        ...nextChat,
        {
          sender: 'npc',
          pl: nextTurn.npcLinePl,
          ru: nextTurn.npcLineRu,
          phonetic: nextTurn.npcPhoneticRu,
        },
      ]);
      setTurnIdx(turnIdx + 1);
    } else {
      setChat(nextChat);
      setCompleted(true);
      onReward(35, 15, `Диалог: ${scenario.title}`);
    }
  };

  const handleMic = () => {
    if (micListening) {
      stopPolishListening();
      setMicListening(false);
      return;
    }
    setMicError(null);
    setMicPartial('');
    setMicListening(true);
    startPolishListening({
      onPartial: (t) => setMicPartial(t),
      onResult: (hyps) => {
        setMicListening(false);
        setMicPartial('');
        applyUserResponse(hyps);
      },
      onError: (_c, msg) => {
        setMicListening(false);
        setMicError(msg);
      },
    });
  };

  if (!scenario) {
    return (
      <div className="space-y-5">
        <button
          onClick={onBack}
          className="inline-flex items-center gap-2 text-sm font-black text-[#1899D6] hover:underline cursor-pointer"
        >
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>

        <div className="rounded-3xl border-2 border-[#84D8FF] bg-[#F0FAFF] p-5 flex items-center gap-4">
          <MascotOrzel mood="ai" outfit={state.equippedOutfit} size={76} className="shrink-0" />
          <div>
            <h2 className="text-xl font-black text-[#3C3C3C]">Живые ИИ-диалоги в Польше</h2>
            <p className="text-xs sm:text-sm font-bold text-[#64748B] mt-1">
              Собеседник говорит голосом по-польски, а локальный NLU-ИИ понимает смысл вашего ответа в микрофон (кофе, чай, билет, симптомы) и продолжает сцену.
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {ROLEPLAY_SCENARIOS.map((sc) => (
            <button
              key={sc.id}
              onClick={() => startScenario(sc)}
              className="rounded-2xl border-2 border-b-4 border-[#E5E5E5] hover:border-[#1CB0F6] bg-white p-5 text-left transition-all cursor-pointer space-y-2"
            >
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-black uppercase px-2.5 py-0.5 rounded-lg bg-[#DDF4FF] text-[#1899D6]">
                  {sc.level}
                </span>
                <span className="text-xs font-extrabold text-[#777777] flex items-center gap-1">
                  <MapPin className="w-3.5 h-3.5 text-[#DC143C]" />
                  {sc.location}
                </span>
              </div>
              <div className="text-lg font-black text-[#3C3C3C]">{sc.title}</div>
              <p className="text-xs font-bold text-[#777777]">{sc.description}</p>
              <div className="pt-1 text-xs font-black text-[#58CC02] flex items-center gap-1.5">
                <MessageCircle className="w-4 h-4" /> Начать диалог с {sc.npcName} →
              </div>
            </button>
          ))}
        </div>
      </div>
    );
  }

  const currentTurn = scenario.turns[turnIdx];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <button
          onClick={() => {
            cancelPolishListening();
            setScenario(null);
          }}
          className="inline-flex items-center gap-2 text-sm font-black text-[#1899D6] cursor-pointer"
        >
          <ArrowLeft className="w-4 h-4" /> Все сценарии
        </button>
        <span className="text-xs font-black px-3 py-1 rounded-full bg-[#FFE4E8] text-[#DC143C]">
          {scenario.npcName} · Шаг {Math.min(turnIdx + 1, scenario.turns.length)}/{scenario.turns.length}
        </span>
      </div>

      {/* Chat Log */}
      <div className="rounded-3xl border-2 border-[#E5E5E5] bg-[#F9FAFB] p-4 space-y-3 max-h-[420px] overflow-y-auto">
        {chat.map((b, i) => (
          <div
            key={i}
            className={`flex ${b.sender === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            {b.sender === 'ai' ? (
              <div className="rounded-xl bg-[#FAF2FF] border border-[#CE82FF] px-3 py-2 text-xs font-bold text-[#8549BA] flex items-center gap-2 max-w-[90%]">
                <BrainCircuit className="w-4 h-4 shrink-0" />
                <span>{b.ru}</span>
              </div>
            ) : (
              <div
                className={`rounded-2xl px-4 py-3 max-w-[85%] space-y-1 ${
                  b.sender === 'user'
                    ? 'bg-[#1CB0F6] text-white'
                    : 'bg-white border-2 border-[#E5E5E5] text-[#3C3C3C]'
                }`}
              >
                {b.pl && (
                  <div className="flex items-center gap-2">
                    <span className="font-black text-sm sm:text-base">{b.pl}</span>
                    {b.sender === 'npc' && (
                      <button
                        type="button"
                        onClick={() => speakPolish(b.pl!)}
                        className="p-1 rounded-lg bg-[#DDF4FF] text-[#1899D6] cursor-pointer shrink-0"
                        title="Озвучить"
                      >
                        <Volume2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                )}
                <div className={`text-xs font-bold ${b.sender === 'user' ? 'text-white/90' : 'text-[#777777]'}`}>
                  {b.ru}
                </div>
                {b.phonetic && (
                  <div className="text-[11px] font-extrabold text-[#1899D6]">[{b.phonetic}]</div>
                )}
                {b.score !== undefined && (
                  <div className="text-[11px] font-black text-[#D7FFB8]">
                    ИИ-понимание: {b.score}%
                  </div>
                )}
              </div>
            )}
          </div>
        ))}
      </div>

      {completed ? (
        <div className="rounded-2xl border-2 border-[#58CC02] bg-[#D7FFB8] p-5 text-center space-y-3">
          <div className="inline-flex items-center gap-2 text-lg font-black text-[#46A302]">
            <CheckCircle2 className="w-6 h-6" /> Диалог успешно завершён! +35 XP · +15 💎
          </div>
          <div className="flex justify-center gap-3">
            <button
              onClick={() => startScenario(scenario)}
              className="duo-btn duo-btn-outline rounded-xl px-4 py-2.5 text-sm font-black"
            >
              Пройти снова
            </button>
            <button
              onClick={() => setScenario(null)}
              className="duo-btn duo-btn-green rounded-xl px-5 py-2.5 text-sm font-black"
            >
              Другая сцена
            </button>
          </div>
        </div>
      ) : (
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4 space-y-3">
          <div className="text-xs font-black uppercase text-[#1899D6] flex items-center gap-1.5">
            <Sparkles className="w-4 h-4" /> Задача: {currentTurn.goalRu}
          </div>

          {/* Voice Reply + Text Reply */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={handleMic}
              className={`w-12 h-12 rounded-xl flex items-center justify-center shrink-0 cursor-pointer ${
                micListening
                  ? 'bg-[#DC143C] text-white animate-pulse'
                  : 'duo-btn duo-btn-blue'
              }`}
              title="Ответить в микрофон"
            >
              {micListening ? <Square className="w-5 h-5 fill-white" /> : <Mic className="w-5 h-5" />}
            </button>

            <input
              type="text"
              value={micPartial || typedReply}
              onChange={(e) => setTypedReply(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && typedReply.trim()) {
                  applyUserResponse([typedReply.trim()]);
                  setTypedReply('');
                }
              }}
              placeholder={micListening ? 'Говорите по-польски...' : 'Или напишите свой ответ...'}
              className="flex-1 rounded-xl border-2 border-[#E5E5E5] px-3.5 py-2.5 text-sm font-bold outline-none focus:border-[#1CB0F6]"
            />

            <button
              type="button"
              disabled={!typedReply.trim()}
              onClick={() => {
                if (!typedReply.trim()) return;
                applyUserResponse([typedReply.trim()]);
                setTypedReply('');
              }}
              className="w-11 h-11 rounded-xl duo-btn duo-btn-green flex items-center justify-center disabled:opacity-40"
            >
              <Send className="w-4 h-4" />
            </button>
          </div>

          {micError && <div className="text-xs font-bold text-[#DC143C]">{micError}</div>}

          {/* Quick Suggested Phrases */}
          <div className="space-y-1.5">
            <div className="text-[11px] font-extrabold text-[#777777]">Подсказки (можно сказать вслух или нажать):</div>
            <div className="flex flex-wrap gap-2">
              {currentTurn.branches.map((br) => (
                <button
                  key={br.id}
                  type="button"
                  onClick={() => applyUserResponse([br.samplePl], br)}
                  className="rounded-xl border border-[#E5E5E5] bg-[#F7F7F7] hover:bg-[#DDF4FF] px-3 py-1.5 text-left text-xs font-extrabold text-[#3C3C3C] cursor-pointer"
                >
                  <span className="text-[#1899D6]">{br.samplePl}</span>
                  <span className="block text-[10px] text-[#777777]">[{br.phoneticRu}]</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
