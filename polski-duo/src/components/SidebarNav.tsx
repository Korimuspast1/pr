import React from 'react';
import { BarChart3, BookOpen, Compass, Infinity as InfinityIcon, Volume2, VolumeX } from 'lucide-react';
import { MascotOrzel } from './MascotOrzel';

export type NavTab = 'learn' | 'infinite' | 'stats' | 'guide';

interface SidebarNavProps {
  activeTab: NavTab;
  onSelectTab: (tab: NavTab) => void;
  soundEnabled: boolean;
  onToggleSound: () => void;
}

const NAV_ITEMS: Array<{ id: NavTab; label: string; icon: React.ReactNode; accent: string }> = [
  {
    id: 'learn',
    label: 'ОБУЧЕНИЕ',
    icon: <Compass className="w-6 h-6" />,
    accent: 'text-[#1CB0F6]',
  },
  {
    id: 'infinite',
    label: 'БЕСКОНЕЧНЫЙ ИИ',
    icon: <InfinityIcon className="w-6 h-6" />,
    accent: 'text-[#CE82FF]',
  },
  {
    id: 'stats',
    label: 'СТАТИСТИКА',
    icon: <BarChart3 className="w-6 h-6" />,
    accent: 'text-[#FF9600]',
  },
  {
    id: 'guide',
    label: 'СЛОВАРЬ И ЗВУКИ',
    icon: <BookOpen className="w-6 h-6" />,
    accent: 'text-[#DC143C]',
  },
];

export const SidebarNav: React.FC<SidebarNavProps> = ({
  activeTab,
  onSelectTab,
  soundEnabled,
  onToggleSound,
}) => {
  return (
    <>
      {/* Desktop Left Sidebar */}
      <aside className="hidden md:flex flex-col w-64 shrink-0 border-r-2 border-[#E5E5E5] bg-white px-4 py-6 sticky top-0 h-screen justify-between">
        <div>
          <div className="flex items-center gap-3 px-3 mb-8">
            <MascotOrzel mood="happy" size={44} />
            <div>
              <div className="text-[22px] font-black tracking-tight leading-none">
                <span className="text-[#58CC02]">Polski</span>
                <span className="text-[#DC143C]">Orzeł</span>
              </div>
              <span className="text-[11px] font-extrabold uppercase tracking-wider text-[#AFAFAF]">
                Польский · Локальный ИИ
              </span>
            </div>
          </div>

          <nav className="flex flex-col gap-2">
            {NAV_ITEMS.map((item) => {
              const active = activeTab === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => onSelectTab(item.id)}
                  className={`flex items-center gap-4 px-4 py-3.5 rounded-2xl text-left font-extrabold text-[14px] tracking-wide transition-all cursor-pointer border-2 ${
                    active
                      ? 'bg-[#DDF4FF] border-[#84D8FF] text-[#1899D6]'
                      : 'border-transparent text-[#777777] hover:bg-[#F7F7F7]'
                  }`}
                >
                  <span className={active ? 'text-[#1CB0F6]' : item.accent}>{item.icon}</span>
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        </div>

        <div className="space-y-3">
          <button
            onClick={onToggleSound}
            className="w-full flex items-center justify-between px-4 py-3 rounded-2xl border-2 border-[#E5E5E5] text-sm font-extrabold text-[#4B4B4B] hover:bg-[#F7F7F7] cursor-pointer"
          >
            <span className="flex items-center gap-2.5">
              {soundEnabled ? (
                <Volume2 className="w-5 h-5 text-[#58CC02]" />
              ) : (
                <VolumeX className="w-5 h-5 text-[#AFAFAF]" />
              )}
              Звуки и речь
            </span>
            <span
              className={`text-xs px-2 py-0.5 rounded-lg ${
                soundEnabled ? 'bg-[#D7FFB8] text-[#46A302]' : 'bg-[#F1F5F9] text-[#777777]'
              }`}
            >
              {soundEnabled ? 'ВКЛ' : 'ВЫКЛ'}
            </span>
          </button>

          <div className="rounded-2xl bg-[#FFFDF9] border-2 border-[#E5E5E5] p-3.5">
            <div className="flex items-center gap-2 mb-1">
              <span className="inline-block w-2.5 h-2.5 rounded-full bg-[#58CC02] animate-pulse" />
              <span className="text-xs font-extrabold uppercase tracking-wider text-[#4B4B4B]">
                Offline AI Engine
              </span>
            </div>
            <p className="text-xs font-semibold text-[#777777] leading-snug">
              Анализ знаний BKT + FSRS и генерация уроков работают 100% локально.
            </p>
          </div>
        </div>
      </aside>

      {/* Mobile Bottom Nav */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-white border-t-2 border-[#E5E5E5] flex justify-around py-2 px-1">
        {NAV_ITEMS.map((item) => {
          const active = activeTab === item.id;
          return (
            <button
              key={item.id}
              onClick={() => onSelectTab(item.id)}
              className={`flex flex-col items-center gap-0.5 px-3 py-1.5 rounded-xl font-extrabold text-[10px] ${
                active ? 'bg-[#DDF4FF] text-[#1899D6]' : 'text-[#777777]'
              }`}
            >
              <span className={active ? 'text-[#1CB0F6]' : item.accent}>{item.icon}</span>
              <span>{item.label.split(' ')[0]}</span>
            </button>
          );
        })}
      </nav>
    </>
  );
};
