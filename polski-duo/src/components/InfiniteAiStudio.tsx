import React, { useState } from 'react';
import { BrainCircuit, Infinity as InfinityIcon, Layers, Mic, Sparkles, TrendingUp, Zap } from 'lucide-react';
import { computeCefrLevel, computeGlobalMasteryScore, getWeakestSkill } from '../ai/localAiEngine';
import { SKILL_META } from '../data/polishLexicon';
import { LearnerAIState, SkillId } from '../types/learning';
import { MascotOrzel } from './MascotOrzel';

interface InfiniteAiStudioProps {
  state: LearnerAIState;
  onStartInfiniteStream: (
    skillFocus?: SkillId,
    customStartTier?: number,
    forceSpeaking?: boolean,
  ) => void;
  onChangePhoneticsMode: (mode: LearnerAIState['showPhoneticsOverride']) => void;
}

const FOCUS_OPTIONS: Array<{ id: SkillId | 'auto'; title: string; subtitle: string; badge: string }> = [
  {
    id: 'auto',
    title: 'Умный Авто-поток ИИ',
    subtitle: 'Локальный ИИ сам чередует темы, подхватывает слабые места и плавно повышает Tier.',
    badge: 'РЕКОМЕНДУЕМ',
  },
  {
    id: 'cases',
    title: 'Падежи (Przypadki)',
    subtitle: 'Biernik, Dopełniacz с «nie», Narzędnik профессий и транспорта.',
    badge: 'ГРАММАТИКА',
  },
  {
    id: 'false_friends',
    title: 'Ложные друзья переводчика',
    subtitle: 'Тренировка коварных слов: sklep, owoce, dywan, zapomnieć, uroda.',
    badge: 'ДЛЯ РУССКИХ',
  },
  {
    id: 'verbs',
    title: 'Глаголы и спряжения',
    subtitle: 'Личные окончания być, mieć, lubić, pić, mówić и прошедшее время.',
    badge: 'ДЕЙСТВИЯ',
  },
  {
    id: 'phonetics',
    title: 'Диакритика и аудирование',
    subtitle: 'ą, ę, ł, ó, ś, ć, ź, ż, cz, sz, rz — чтение, слух и письмо.',
    badge: 'ПРОИЗНОШЕНИЕ',
  },
  {
    id: 'idioms_advanced',
    title: 'Идиомы и B1–B2',
    subtitle: 'Живые выражения, пословицы и сложный синтаксис.',
    badge: 'ПРОДВИНУТЫЙ',
  },
];

const DIFFICULTY_LADDER = [
  { range: 'Tier 1.0–2.9', label: 'Лёгкий старт (A0–A1)', desc: 'Отдельные слова, фонетическая транскрипция [джень добры], выбор из вариантов.' },
  { range: 'Tier 3.0–5.4', label: 'Уверенная база (A1–A1+)', desc: 'Короткие фразы, ложные друзья, сборка предложений из плиток, первый падеж Biernik.' },
  { range: 'Tier 5.5–7.9', label: 'Средний уровень (A2)', desc: 'Отрицание с Dopełniacz, Narzędnik, ручной ввод текста и контроль диакритики.' },
  { range: 'Tier 8.0–11.5', label: 'Свободный польский (B1–C1)', desc: 'Без подсказок, сложные союзы, идиомы, диктанты и строгая орфография.' },
];

export const InfiniteAiStudio: React.FC<InfiniteAiStudioProps> = ({
  state,
  onStartInfiniteStream,
  onChangePhoneticsMode,
}) => {
  const [selectedFocus, setSelectedFocus] = useState<SkillId | 'auto'>('auto');
  const [startFromEasy, setStartFromEasy] = useState(false);

  const cefr = computeCefrLevel(state);
  const masteryScore = computeGlobalMasteryScore(state);
  const weakest = getWeakestSkill(state);

  return (
    <div className="max-w-[740px] mx-auto pb-28 md:pb-16 pt-6 px-4 space-y-6">
      {/* Hero Banner */}
      <div className="rounded-3xl border-2 border-[#CE82FF] bg-gradient-to-br from-[#FAF2FF] via-white to-[#FFE4E8]/40 p-6 flex flex-col sm:flex-row items-center gap-5">
        <MascotOrzel mood="ai" size={104} className="shrink-0 animate-float" />
        <div className="flex-1 text-center sm:text-left">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-[#CE82FF]/20 text-[#8549BA] text-xs font-black uppercase tracking-wider mb-2">
            <InfinityIcon className="w-4 h-4" />
            Бесконечная генерация ИИ
          </div>
          <h1 className="text-2xl sm:text-3xl font-black text-[#3C3C3C] leading-tight">
            Бесконечный ИИ-Тренажёр
          </h1>
          <p className="text-sm font-bold text-[#64748B] mt-1.5 leading-relaxed">
            Занимайтесь сколько угодно без ограничений по жизням. После каждого ответа локальный ИИ пересчитывает ваш уровень и генерирует следующее задание — от простого к сложному.
          </p>
        </div>
      </div>

      {/* Live AI Snapshot Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="text-xs font-extrabold text-[#777777] uppercase">Текущий Tier ИИ</div>
          <div className="text-2xl font-black text-[#8549BA] mt-1 flex items-center gap-2">
            <TrendingUp className="w-5 h-5" />
            {state.currentTier.toFixed(2)}
          </div>
          <div className="text-xs font-bold text-[#AFAFAF] mt-0.5">Уровень: {cefr}</div>
        </div>

        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="text-xs font-extrabold text-[#777777] uppercase">Рейтинг знаний</div>
          <div className="text-2xl font-black text-[#58CC02] mt-1">{masteryScore} / 1000</div>
          <div className="text-xs font-bold text-[#AFAFAF] mt-0.5">Байесовская модель BKT</div>
        </div>

        <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4">
          <div className="text-xs font-extrabold text-[#777777] uppercase">Приоритет ИИ</div>
          <div className="text-lg font-black text-[#DC143C] mt-1 truncate">
            {SKILL_META[weakest].shortLabel}
          </div>
          <div className="text-xs font-bold text-[#AFAFAF] mt-0.5">
            Освоение {Math.round(state.skillMastery[weakest] * 100)}%
          </div>
        </div>
      </div>

      {/* Focus Selector */}
      <div className="space-y-3">
        <h2 className="text-lg font-black text-[#3C3C3C] flex items-center gap-2">
          <Layers className="w-5 h-5 text-[#CE82FF]" />
          Выберите направление бесконечной практики
        </h2>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {FOCUS_OPTIONS.map((opt) => {
            const active = selectedFocus === opt.id;
            return (
              <button
                key={opt.id}
                onClick={() => setSelectedFocus(opt.id)}
                className={`rounded-2xl border-2 border-b-4 p-4 text-left transition-all cursor-pointer ${
                  active
                    ? 'bg-[#FAF2FF] border-[#CE82FF] text-[#3C3C3C]'
                    : 'bg-white border-[#E5E5E5] hover:bg-[#F7F7F7]'
                }`}
              >
                <div className="flex items-center justify-between gap-2 mb-1">
                  <span className="font-black text-[15px] text-[#3C3C3C]">{opt.title}</span>
                  <span
                    className={`text-[10px] font-black px-2 py-0.5 rounded-md ${
                      active ? 'bg-[#CE82FF] text-white' : 'bg-[#F1F5F9] text-[#777777]'
                    }`}
                  >
                    {opt.badge}
                  </span>
                </div>
                <p className="text-xs font-bold text-[#777777] leading-snug">{opt.subtitle}</p>
              </button>
            );
          })}
        </div>
      </div>

      {/* Phonetic Hints & Escalation Options */}
      <div className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4 space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <div className="text-sm font-black text-[#3C3C3C]">
              Фонетическая подсказка по-русски <span className="text-[#1CB0F6]">[чещьчь]</span>
            </div>
            <div className="text-xs font-bold text-[#777777]">
              В режиме «Авто» ИИ сам убирает подсказки после Tier 3.6.
            </div>
          </div>

          <div className="flex rounded-xl bg-[#F1F5F9] p-1 gap-1 shrink-0">
            {(
              [
                { id: 'auto', label: 'Авто' },
                { id: 'always', label: 'Всегда' },
                { id: 'off', label: 'Скрыть' },
              ] as const
            ).map((m) => (
              <button
                key={m.id}
                onClick={() => onChangePhoneticsMode(m.id)}
                className={`px-3 py-1.5 rounded-lg text-xs font-black cursor-pointer ${
                  state.showPhoneticsOverride === m.id
                    ? 'bg-white text-[#1899D6] shadow-xs'
                    : 'text-[#777777]'
                }`}
              >
                {m.label}
              </button>
            ))}
          </div>
        </div>

        <label className="flex items-center gap-3 pt-2 border-t border-[#E5E5E5] cursor-pointer">
          <input
            type="checkbox"
            checked={startFromEasy}
            onChange={(e) => setStartFromEasy(e.target.checked)}
            className="w-5 h-5 accent-[#CE82FF] rounded cursor-pointer"
          />
          <div>
            <div className="text-sm font-black text-[#3C3C3C]">
              Начать разминку с самого лёгкого уровня (Tier 1.2 → ∞)
            </div>
            <div className="text-xs font-bold text-[#777777]">
              Сначала простые слова, затем ИИ постепенно разгонит сложность с каждой серией верных ответов.
            </div>
          </div>
        </label>
      </div>

      {/* Launch Buttons */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <button
          onClick={() =>
            onStartInfiniteStream(
              selectedFocus === 'auto' ? undefined : selectedFocus,
              startFromEasy ? 1.2 : undefined,
              false,
            )
          }
          className="w-full duo-btn duo-btn-purple rounded-2xl py-4 px-5 font-black text-base uppercase tracking-wider flex items-center justify-center gap-2.5"
        >
          <Sparkles className="w-5 h-5" />
          ЗАПУСТИТЬ ПОТОК ИИ
        </button>

        <button
          onClick={() =>
            onStartInfiniteStream(
              selectedFocus === 'auto' ? undefined : selectedFocus,
              startFromEasy ? 1.2 : undefined,
              true,
            )
          }
          className="w-full duo-btn duo-btn-blue rounded-2xl py-4 px-5 font-black text-base uppercase tracking-wider flex items-center justify-center gap-2.5"
        >
          <Mic className="w-5 h-5" />
          МИКРОФОН · ИИ-РЕЧЬ ∞
        </button>
      </div>

      {/* Progressive Difficulty Ladder */}
      <div className="rounded-2xl border-2 border-[#E5E5E5] bg-[#FFFDF9] p-5 space-y-3">
        <h3 className="text-sm font-black uppercase tracking-wider text-[#4B4B4B] flex items-center gap-2">
          <BrainCircuit className="w-4 h-4 text-[#A55EEA]" />
          Как локальный ИИ постепенно усложняет задания:
        </h3>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {DIFFICULTY_LADDER.map((step, i) => {
            const isCurrent =
              (i === 0 && state.currentTier < 3) ||
              (i === 1 && state.currentTier >= 3 && state.currentTier < 5.5) ||
              (i === 2 && state.currentTier >= 5.5 && state.currentTier < 8) ||
              (i === 3 && state.currentTier >= 8);
            return (
              <div
                key={step.range}
                className={`rounded-xl border-2 p-3 ${
                  isCurrent
                    ? 'border-[#58CC02] bg-[#F3FFE8]'
                    : 'border-[#E5E5E5] bg-white'
                }`}
              >
                <div className="flex items-center justify-between mb-1">
                  <span className="text-xs font-black text-[#8549BA]">{step.range}</span>
                  {isCurrent && (
                    <span className="text-[10px] font-black px-2 py-0.5 rounded-full bg-[#58CC02] text-white flex items-center gap-1">
                      <Zap className="w-3 h-3" /> ВЫ ЗДЕСЬ
                    </span>
                  )}
                </div>
                <div className="text-sm font-black text-[#3C3C3C]">{step.label}</div>
                <p className="text-xs font-bold text-[#777777] mt-0.5 leading-snug">{step.desc}</p>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
