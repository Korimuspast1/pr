import React, { useState } from 'react';
import { AlertTriangle, BookMarked, BookOpen, Layers, Search, Volume2 } from 'lucide-react';
import { LEXICON, POLISH_CASES_GUIDE, POLISH_PHONETICS } from '../data/polishLexicon';
import { LearnerAIState } from '../types/learning';
import { speakPolish } from '../utils/sound';

interface GuidebookViewProps {
  state: LearnerAIState;
}

type GuideTab = 'phonetics' | 'false_friends' | 'cases' | 'lexicon';

export const GuidebookView: React.FC<GuidebookViewProps> = ({ state }) => {
  const [tab, setTab] = useState<GuideTab>('phonetics');
  const [query, setQuery] = useState('');

  const falseFriends = LEXICON.filter((i) => i.isFalseFriend);
  const filteredLexicon = LEXICON.filter(
    (item) =>
      item.pl.toLowerCase().includes(query.toLowerCase()) ||
      item.ru.toLowerCase().includes(query.toLowerCase()) ||
      item.category.toLowerCase().includes(query.toLowerCase()),
  );

  return (
    <div className="max-w-[760px] mx-auto pb-28 md:pb-16 pt-6 px-4 space-y-6">
      <div className="rounded-3xl border-2 border-[#E5E5E5] bg-gradient-to-r from-[#FFE4E8]/60 via-white to-[#DDF4FF]/50 p-6">
        <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-[#DC143C] mb-1">
          <BookOpen className="w-4 h-4" />
          Справочник для русскоязычных
        </div>
        <h1 className="text-2xl sm:text-3xl font-black text-[#3C3C3C]">
          Звуки, падежи и словарь Польши
        </h1>
        <p className="text-sm font-bold text-[#64748B] mt-1">
          Нажимайте на карточки, чтобы услышать произношение и запомнить отличия от русского языка.
        </p>

        {/* Sub-tabs */}
        <div className="flex flex-wrap gap-2 mt-4">
          {(
            [
              { id: 'phonetics', label: 'Звуки и буквы', icon: <Volume2 className="w-4 h-4" /> },
              { id: 'false_friends', label: 'Ложные друзья', icon: <AlertTriangle className="w-4 h-4" /> },
              { id: 'cases', label: 'Падежи (Przypadki)', icon: <Layers className="w-4 h-4" /> },
              { id: 'lexicon', label: `Весь словарь (${LEXICON.length})`, icon: <BookMarked className="w-4 h-4" /> },
            ] as const
          ).map((t) => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`px-4 py-2 rounded-xl text-xs font-black border-2 border-b-4 cursor-pointer transition-all inline-flex items-center gap-1.5 ${
                tab === t.id
                  ? 'bg-[#1CB0F6] border-[#1899D6] text-white'
                  : 'bg-white border-[#E5E5E5] text-[#4B4B4B] hover:bg-[#F7F7F7]'
              }`}
            >
              {t.icon}
              <span>{t.label}</span>
            </button>
          ))}
        </div>
      </div>

      {/* TAB 1: PHONETICS */}
      {tab === 'phonetics' && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {POLISH_PHONETICS.map((item) => (
            <button
              key={item.symbol}
              onClick={() => speakPolish(item.examplePl)}
              className="rounded-2xl border-2 border-b-4 border-[#E5E5E5] bg-white hover:border-[#1CB0F6] p-4 text-left transition-all cursor-pointer group"
            >
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-3">
                  <span className="px-3 py-1.5 rounded-xl bg-[#DDF4FF] text-[#1899D6] text-lg font-black">
                    {item.symbol}
                  </span>
                  <div>
                    <div className="font-black text-sm text-[#3C3C3C]">{item.name}</div>
                    <div className="text-xs font-extrabold text-[#1CB0F6]">{item.approxRu}</div>
                  </div>
                </div>
                <Volume2 className="w-5 h-5 text-[#AFAFAF] group-hover:text-[#1CB0F6]" />
              </div>

              <p className="text-xs font-bold text-[#64748B] leading-snug mb-2">{item.tip}</p>
              <div className="text-xs font-black text-[#3C3C3C] bg-[#F7F7F7] rounded-lg px-2.5 py-1.5">
                Пример: <span className="text-[#DC143C]">{item.examplePl}</span> — {item.exampleRu}
              </div>
            </button>
          ))}
        </div>
      )}

      {/* TAB 2: FALSE FRIENDS */}
      {tab === 'false_friends' && (
        <div className="space-y-3">
          {falseFriends.map((ff) => (
            <div
              key={ff.id}
              className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3"
            >
              <div className="space-y-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-lg font-black text-[#DC143C]">{ff.pl}</span>
                  <span className="text-xs font-extrabold text-[#AFAFAF]">[{ff.phoneticRu}]</span>
                  <span className="text-sm font-black text-[#58CC02]">= {ff.ru}</span>
                  {ff.falseFriendTrapRu && (
                    <span className="inline-flex items-center gap-1 text-[11px] font-black px-2 py-0.5 rounded-lg bg-[#FFE4E8] text-[#DC143C]">
                      <AlertTriangle className="w-3 h-3" /> НЕ «{ff.falseFriendTrapRu}»!
                    </span>
                  )}
                </div>
                <p className="text-xs font-bold text-[#4B4B4B] leading-snug">
                  {ff.falseFriendNote}
                </p>
                <div className="text-xs font-bold text-[#777777]">
                  «{ff.examplePl}» — {ff.exampleRu}
                </div>
              </div>

              <button
                onClick={() => speakPolish(ff.examplePl)}
                className="shrink-0 w-10 h-10 rounded-xl duo-btn duo-btn-outline flex items-center justify-center"
                title="Озвучить пример"
              >
                <Volume2 className="w-5 h-5 text-[#1CB0F6]" />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* TAB 3: CASES */}
      {tab === 'cases' && (
        <div className="space-y-3">
          {POLISH_CASES_GUIDE.map((c) => (
            <div key={c.name} className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-4 space-y-2">
              <div className="flex items-center justify-between">
                <h3 className="text-base font-black text-[#3C3C3C]">{c.name}</h3>
                <span className="text-xs font-black px-2.5 py-1 rounded-lg bg-[#FFF0D6] text-[#CC7800]">
                  {c.question}
                </span>
              </div>
              <p className="text-xs sm:text-sm font-bold text-[#4B4B4B] leading-snug">{c.rule}</p>
              <div className="flex items-center justify-between gap-3 rounded-xl bg-[#F7F7F7] p-3">
                <div>
                  <div className="text-sm font-black text-[#1899D6]">{c.examplePl}</div>
                  <div className="text-xs font-bold text-[#777777]">{c.exampleRu}</div>
                </div>
                <button
                  onClick={() => speakPolish(c.examplePl)}
                  className="p-2 rounded-lg bg-white border border-[#E5E5E5] cursor-pointer hover:border-[#1CB0F6]"
                >
                  <Volume2 className="w-4 h-4 text-[#1CB0F6]" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* TAB 4: LEXICON */}
      {tab === 'lexicon' && (
        <div className="space-y-4">
          <div className="relative">
            <Search className="w-5 h-5 text-[#AFAFAF] absolute left-3.5 top-3.5" />
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Поиск слова (по-польски, по-русски или по теме)..."
              className="w-full rounded-2xl border-2 border-[#E5E5E5] bg-white focus:border-[#1CB0F6] outline-none pl-11 pr-4 py-3 text-sm font-extrabold"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {filteredLexicon.map((item) => {
              const mem = state.itemMemory[item.id];
              const pct = mem ? Math.round(mem.strength * 100) : 0;
              return (
                <div
                  key={item.id}
                  className="rounded-2xl border-2 border-[#E5E5E5] bg-white p-3.5 flex items-center justify-between gap-2"
                >
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-black text-base text-[#3C3C3C] truncate">
                        {item.pl}
                      </span>
                      <span className="text-[11px] font-bold text-[#AFAFAF]">
                        [{item.phoneticRu}]
                      </span>
                    </div>
                    <div className="text-sm font-extrabold text-[#58CC02]">{item.ru}</div>
                    <div className="text-[11px] font-bold text-[#777777] mt-0.5">
                      {item.category} · Tier {item.tier} {mem ? `· Память ${pct}%` : ''}
                    </div>
                  </div>
                  <button
                    onClick={() => speakPolish(item.pl)}
                    className="shrink-0 w-9 h-9 rounded-xl border border-[#E5E5E5] flex items-center justify-center hover:bg-[#DDF4FF] cursor-pointer"
                  >
                    <Volume2 className="w-4 h-4 text-[#1CB0F6]" />
                  </button>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
};
