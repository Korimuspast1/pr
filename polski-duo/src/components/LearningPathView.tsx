import React, { useState } from 'react';
import {
  AlertTriangle,
  BookOpen,
  BrainCircuit,
  Briefcase,
  Cat,
  Check,
  Clock,
  Coffee,
  Crown,
  Drama,
  Feather,
  Flame,
  Gem,
  Hand,
  Heart,
  Infinity as InfinityIcon,
  Languages,
  Mic,
  Puzzle,
  ShieldCheck,
  Sparkles,
  Star,
  Target,
  TrainFront,
  Trophy,
  Zap,
} from 'lucide-react';
import { LEARNING_PATH } from '../data/learningPath';
import { SKILL_META } from '../data/polishLexicon';
import { LearnerAIState, PathNode } from '../types/learning';
import { MascotOrzel, PolishFlagBadge } from './MascotOrzel';

function renderNodeIcon(iconKey: string): React.ReactNode {
  const cls = 'w-8 h-8 stroke-[2.4]';
  switch (iconKey) {
    case 'hand':
      return <Hand className={cls} />;
    case 'languages':
      return <Languages className={cls} />;
    case 'cat':
      return <Cat className={cls} />;
    case 'trophy':
      return <Trophy className={cls} />;
    case 'coffee':
      return <Coffee className={cls} />;
    case 'zap':
      return <Zap className={cls} />;
    case 'target':
      return <Target className={cls} />;
    case 'crown':
      return <Crown className={cls} />;
    case 'drama':
      return <Drama className={cls} />;
    case 'train':
      return <TrainFront className={cls} />;
    case 'alert':
      return <AlertTriangle className={cls} />;
    case 'shield':
      return <ShieldCheck className={cls} />;
    case 'briefcase':
      return <Briefcase className={cls} />;
    case 'clock':
      return <Clock className={cls} />;
    case 'puzzle':
      return <Puzzle className={cls} />;
    case 'feather':
      return <Feather className={cls} />;
    case 'mic':
      return <Mic className={cls} />;
    case 'infinity':
      return <InfinityIcon className="w-9 h-9 stroke-[2.8]" />;
    default:
      return <Star className={cls} />;
  }
}

interface LearningPathViewProps {
  state: LearnerAIState;
  onStartNodeLesson: (node: PathNode) => void;
  onStartPlacement: () => void;
  onStartInfinite: () => void;
  onOpenGuide: () => void;
}

const SNAKE_OFFSETS = [0, -48, -72, -48, 0, 48, 72, 48];

export const LearningPathView: React.FC<LearningPathViewProps> = ({
  state,
  onStartNodeLesson,
  onStartPlacement,
  onStartInfinite,
  onOpenGuide,
}) => {
  const [selectedNodeId, setSelectedNodeId] = useState<string>('n1_greetings');

  // Determine the first uncompleted node as the recommended "current" node
  const allNodes = LEARNING_PATH.flatMap((s) => s.nodes);
  const firstPending = allNodes.find((n) => (state.completedNodes[n.id] ?? 0) === 0);
  const activeRecommendedId = firstPending?.id ?? allNodes[allNodes.length - 1].id;

  return (
    <div className="max-w-[640px] mx-auto pb-28 md:pb-16 pt-4 px-4">
      {/* Mobile Top Status Bar */}
      <div className="flex lg:hidden items-center justify-between mb-4 px-2 py-2 rounded-2xl border-2 border-[#E5E5E5] bg-white">
        <div className="flex items-center gap-2">
          <PolishFlagBadge className="w-7 h-5" />
          <span className="font-black text-sm text-[#3C3C3C]">PolskiOrzeł</span>
        </div>
        <div className="flex items-center gap-3 text-sm font-black">
          <span className="flex items-center gap-1 text-[#FF9600]">
            <Flame className="w-4 h-4 fill-[#FF9600]" />
            {state.streakDays}
          </span>
          <span className="flex items-center gap-1 text-[#1CB0F6]">
            <Gem className="w-4 h-4 fill-[#1CB0F6]" />
            {state.gems}
          </span>
          <span className="flex items-center gap-1 text-[#FF4B4B]" title="Бесконечные сердечки">
            <Heart className="w-4 h-4 fill-[#FF4B4B]" />
            <InfinityIcon className="w-4 h-4 stroke-[3]" />
          </span>
        </div>
      </div>

      {/* Placement / AI Welcome Banner */}
      <div className="mb-6 rounded-2xl border-2 border-[#E5E5E5] bg-gradient-to-r from-[#FFFDF9] via-white to-[#FFE4E8]/45 p-4 flex flex-col sm:flex-row items-center justify-between gap-4">
        <div className="flex items-center gap-3.5">
          <MascotOrzel mood="happy" size={64} className="shrink-0 animate-float" />
          <div>
            <div className="inline-flex items-center gap-1.5 text-[11px] font-black uppercase tracking-wider px-2.5 py-0.5 rounded-full bg-[#FFE4E8] text-[#DC143C] mb-1">
              <PolishFlagBadge className="w-4 h-3" />
              <span>Польский для русскоязычных</span>
            </div>
            <h2 className="text-lg font-black text-[#3C3C3C] leading-tight">
              {state.placementDone
                ? `Ваш текущий темп: Tier ${state.currentTier.toFixed(1)}`
                : 'Начните с нуля или проверьте уровень с ИИ'}
            </h2>
            <p className="text-xs font-bold text-[#777777] mt-0.5">
              Уроки генерируются на ходу: от простых слов к падежам и ложным друзьям.
            </p>
          </div>
        </div>

        <button
          onClick={onStartPlacement}
          className="shrink-0 duo-btn duo-btn-blue rounded-xl px-4 py-2.5 text-xs font-black uppercase tracking-wide flex items-center gap-1.5"
        >
          <BrainCircuit className="w-4 h-4" />
          {state.placementDone ? 'ПЕРЕПРОВЕРИТЬ УРОВЕНЬ' : 'ИИ-ТЕСТ УРОВНЯ'}
        </button>
      </div>

      {/* Winding Sections & Nodes */}
      <div className="space-y-10">
        {LEARNING_PATH.map((section, sIdx) => (
          <section key={section.id} className="space-y-8">
            {/* Section Header Banner (Duolingo style) */}
            <div
              className="rounded-2xl p-4 sm:p-5 text-white flex items-center justify-between gap-3 shadow-sm"
              style={{
                backgroundColor: section.color,
                borderBottom: `4px solid ${section.shadowColor}`,
              }}
            >
              <div>
                <div className="flex items-center gap-2 mb-0.5">
                  <span className="text-[11px] font-black uppercase tracking-wider px-2 py-0.5 rounded-md bg-black/20">
                    Уровень {section.cefr}
                  </span>
                  <span className="text-xs font-extrabold opacity-90">
                    Раздел {sIdx + 1}
                  </span>
                </div>
                <h2 className="text-xl font-black tracking-tight">{section.title}</h2>
                <p className="text-sm font-bold opacity-95">{section.subtitle}</p>
              </div>

              <button
                onClick={onOpenGuide}
                className="shrink-0 rounded-xl bg-black/15 hover:bg-black/25 px-3 py-2.5 text-xs font-black uppercase tracking-wider flex items-center gap-1.5 cursor-pointer border border-white/25"
                title="Справочник по разделу"
              >
                <BookOpen className="w-4 h-4" />
                <span className="hidden sm:inline">Справочник</span>
              </button>
            </div>

            {/* Winding Node Snake */}
            <div className="relative flex flex-col items-center gap-7 py-2">
              {section.nodes.map((node, nIdx) => {
                const crowns = state.completedNodes[node.id] ?? 0;
                const isDone = crowns > 0;
                const isRecommended = node.id === activeRecommendedId;
                const isSelected = node.id === selectedNodeId;
                const offsetX = SNAKE_OFFSETS[(sIdx * 3 + nIdx) % SNAKE_OFFSETS.length];

                const nodeColor = node.isInfinite
                  ? '#CE82FF'
                  : isDone
                    ? '#FFC800'
                    : section.color;
                const nodeShadow = node.isInfinite
                  ? '#A55EEA'
                  : isDone
                    ? '#E5A900'
                    : section.shadowColor;

                return (
                  <div
                    key={node.id}
                    className="relative flex flex-col items-center"
                    style={{ transform: `translateX(${offsetX}px)` }}
                  >
                    {/* Start badge floating above recommended node */}
                    {isRecommended && (
                      <div className="mb-2 px-3 py-1 rounded-xl bg-white border-2 border-[#E5E5E5] shadow-xs text-xs font-black uppercase tracking-wider text-[#58CC02] animate-bounce">
                        НАЧАТЬ!
                      </div>
                    )}

                    {/* Mascot companion next to section's 2nd node */}
                    {nIdx === 1 && sIdx === 0 && (
                      <div className="hidden sm:block absolute -right-32 top-0 pointer-events-none">
                        <MascotOrzel mood="happy" size={82} className="animate-float" />
                      </div>
                    )}
                    {nIdx === 1 && sIdx === 2 && (
                      <div className="hidden sm:block absolute -left-32 top-0 pointer-events-none">
                        <MascotOrzel mood="thinking" size={82} />
                      </div>
                    )}

                    {/* Circular 3D Node Button */}
                    <div className="relative">
                      {isRecommended && (
                        <div
                          className="absolute -inset-2 rounded-full border-4 opacity-45 animate-pulse pointer-events-none"
                          style={{ borderColor: section.color }}
                        />
                      )}
                      <button
                        onClick={() => setSelectedNodeId(isSelected ? '' : node.id)}
                        className="relative w-[76px] h-[72px] rounded-full flex items-center justify-center text-3xl font-black text-white transition-transform active:translate-y-[3px] cursor-pointer"
                        style={{
                          backgroundColor: nodeColor,
                          boxShadow: `0 6px 0 ${nodeShadow}`,
                        }}
                        aria-label={node.title}
                      >
                        {renderNodeIcon(node.icon)}

                        {isDone && (
                          <span className="absolute -bottom-1 -right-1 w-7 h-7 rounded-full bg-white border-2 border-[#E5A900] flex items-center justify-center">
                            {crowns >= 3 ? (
                              <Crown className="w-4 h-4 text-[#E5A900] fill-[#FFC800]" />
                            ) : (
                              <Check className="w-4 h-4 text-[#58CC02] stroke-[3]" />
                            )}
                          </span>
                        )}
                      </button>
                    </div>

                    {/* Crown dots */}
                    {!node.isInfinite && (
                      <div className="flex items-center gap-1 mt-2.5">
                        {[1, 2, 3].map((lvl) => (
                          <Star
                            key={lvl}
                            className={`w-3.5 h-3.5 ${
                              crowns >= lvl
                                ? 'text-[#FFC800] fill-[#FFC800]'
                                : 'text-[#E5E5E5] fill-[#E5E5E5]'
                            }`}
                          />
                        ))}
                      </div>
                    )}

                    <div className="mt-1 text-center max-w-[165px]">
                      <div className="text-xs font-extrabold text-[#4B4B4B] leading-tight">
                        {node.title}
                      </div>
                      {node.isSpeaking && (
                        <div className="inline-flex items-center gap-1 mt-1 px-2 py-0.5 rounded-full bg-[#DDF4FF] text-[#1899D6] text-[10px] font-black uppercase">
                          <Mic className="w-2.5 h-2.5" /> Микрофон · ИИ
                        </div>
                      )}
                    </div>

                    {/* Popover Card when selected */}
                    {isSelected && (
                      <div
                        className="mt-3 z-20 w-[280px] sm:w-[310px] rounded-2xl p-4 text-white shadow-lg animate-pop"
                        style={{
                          backgroundColor: node.isInfinite ? '#A55EEA' : section.color,
                          borderBottom: `4px solid ${
                            node.isInfinite ? '#8549BA' : section.shadowColor
                          }`,
                        }}
                      >
                        <div className="flex items-center justify-between text-[11px] font-black uppercase tracking-wider opacity-90 mb-1">
                          <span>{SKILL_META[node.skillFocus].shortLabel}</span>
                          <span>Сложность: Tier {node.targetTier.toFixed(1)}</span>
                        </div>
                        <h3 className="text-lg font-black leading-tight">{node.title}</h3>
                        <p className="text-xs font-bold opacity-95 mt-1 mb-3">{node.subtitle}</p>

                        <button
                          onClick={() =>
                            node.isInfinite ? onStartInfinite() : onStartNodeLesson(node)
                          }
                          className="w-full rounded-xl bg-white text-[#3C3C3C] font-black text-sm py-2.5 px-4 shadow-[0_4px_0_rgba(0,0,0,0.18)] active:translate-y-[2px] cursor-pointer flex items-center justify-center gap-2"
                        >
                          <Sparkles className="w-4 h-4 text-[#CE82FF]" />
                          {node.isInfinite
                            ? 'ОТКРЫТЬ БЕСКОНЕЧНЫЙ ИИ'
                            : isDone
                              ? `ТРЕНИРОВАТЬСЯ (${crowns}/3 ★)`
                              : 'НАЧАТЬ УРОК · +10 XP'}
                        </button>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
};
