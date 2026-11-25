import React from 'react';
import {
  BarChart3,
  BookOpen,
  Compass,
  Gamepad2,
  Infinity as InfinityIcon,
  Settings,
  Volume2,
  VolumeX,
} from 'lucide-react';
import { MascotOutfit } from '../types/learning';
import { MascotOrzel } from './MascotOrzel';

export type NavTab = 'learn' | 'infinite' | 'practice' | 'stats' | 'guide';

interface SidebarNavProps {
  activeTab: NavTab;
  onSelectTab: (tab: NavTab) => void;
  soundEnabled: boolean;
  onToggleSound: () => void;
  onOpenSettings: () => void;
  outfit: MascotOutfit;
}

const NAV_ITEMS: Array<{ id: NavTab; label: string; mobileLabel: string; icon: React.ReactNode; accent: string }> = [
  {
    id: 'learn',
    label: 'ОБУЧЕНИЕ',
    mobileLabel: 'Учить',
    icon: <Compass className="w-6 h-6" />,
    accent: 'text-[#1CB0F6]',
  },
  {
    id: 'practice',
    label: 'ПРАКТИКУМ',
    mobileLabel: 'Игры',
    icon: <Gamepad2 className="w-6 h-6" />,
    accent: 'text-[#58CC02]',
  },
  {
    id: 'infinite',
    label: 'БЕСКОНЕЧНЫЙ ИИ',
    mobileLabel: 'ИИ',
    icon: <InfinityIcon className="w-6 h-6" />,
    accent: 'text-[#CE82FF]',
  },
  {
    id: 'stats',
    label: 'СТАТИСТИКА',
    mobileLabel: 'Статы',
    icon: <BarChart3 className="w-6 h-6" />,
    accent: 'text-[#FF9600]',
  },
  {
    id: 'guide',
    label: 'СЛОВАРЬ И ЗВУКИ',
    mobileLabel: 'Звуки',
    icon: <BookOpen className="w-6 h-6" />,
    accent: 'text-[#DC143C]',
  },
];

export const SidebarNav: React.FC<SidebarNavProps> = ({
  activeTab,
  onSelectTab,
  soundEnabled,
  onToggleSound,
  onOpenSettings,
  outfit,
}) => {
  return (
    <>
      {/* Desktop Left Sidebar */}
      <aside className="hidden md:flex flex-col w-64 shrink-0 border-r-2 border-[#E5E5E5] bg-white px-4 py-6 sticky top-0 h-screen justify-between">
        <div>
          <div className="flex items-center gap-3 px-3 mb-7">
            <MascotOrzel mood="happy" outfit={outfit} size={46} />
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

          <nav className="flex flex-col gap-1.5">
            {NAV_ITEMS.map((item) => {
              const active = activeTab === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => onSelectTab(item.id)}
                  className={`flex items-center gap-3.5 px-4 py-3 rounded-2xl text-left font-extrabold text-[13px] tracking-wide transition-all cursor-pointer border-2 ${
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

        <div className="space-y-2.5">
          <button
            onClick={onOpenSettings}
            className="w-full flex items-center justify-between px-4 py-2.5 rounded-2xl border-2 border-[#E5E5E5] text-sm font-extrabold text-[#4B4B4B] hover:bg-[#F7F7F7] cursor-pointer"
          >
            <span className="flex items-center gap-2.5">
              <Settings className="w-4 h-4 text-[#1899D6]" />
              Обновы и Бэкап
            </span>
          </button>

          <button
            onClick={onToggleSound}
            className="w-full flex items-center justify-between px-4 py-2.5 rounded-2xl border-2 border-[#E5E5E5] text-sm font-extrabold text-[#4B4B4B] hover:bg-[#F7F7F7] cursor-pointer"
          >
            <span className="flex items-center gap-2.5">
              {soundEnabled ? (
                <Volume2 className="w-4 h-4 text-[#58CC02]" />
              ) : (
                <VolumeX className="w-4 h-4 text-[#AFAFAF]" />
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
        </div>
      </aside>

      {/* Mobile Floating Settings Button */}
      <button
        onClick={onOpenSettings}
        className="md:hidden fixed top-3 right-3 z-40 w-10 h-10 rounded-full bg-white/95 border-2 border-[#E5E5E5] shadow-sm flex items-center justify-center text-[#4B4B4B] cursor-pointer"
        aria-label="Настройки, обновления и бэкап"
      >
        <Settings className="w-5 h-5" />
      </button>

      {/* Mobile Bottom Nav */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-white border-t-2 border-[#E5E5E5] flex justify-around py-1.5 px-1">
        {NAV_ITEMS.map((item) => {
          const active = activeTab === item.id;
          return (
            <button
              key={item.id}
              onClick={() => onSelectTab(item.id)}
              className={`flex flex-col items-center gap-0.5 px-2.5 py-1.5 rounded-xl font-extrabold text-[10px] ${
                active ? 'bg-[#DDF4FF] text-[#1899D6]' : 'text-[#777777]'
              }`}
            >
              <span className={active ? 'text-[#1CB0F6]' : item.accent}>{item.icon}</span>
              <span>{item.mobileLabel}</span>
            </button>
          );
        })}
      </nav>
    </>
  );
};
