import React from 'react';
import { ArrowLeft, Check, Gem, Palette, Sparkles, Zap } from 'lucide-react';
import { SHOP_OUTFITS, SHOP_THEMES } from '../../data/practiceData';
import { AppThemeId, LearnerAIState, MascotOutfit } from '../../types/learning';
import { playUiSound } from '../../utils/sound';
import { MascotOrzel } from '../MascotOrzel';

interface ShopViewProps {
  state: LearnerAIState;
  onUpdateState: (updater: (prev: LearnerAIState) => LearnerAIState) => void;
  onBack: () => void;
}

export const ShopView: React.FC<ShopViewProps> = ({ state, onUpdateState, onBack }) => {
  const boostActive = Date.now() < state.xpBoostUntil;
  const boostMins = boostActive ? Math.max(1, Math.ceil((state.xpBoostUntil - Date.now()) / 60000)) : 0;

  const buyOrEquipOutfit = (id: MascotOutfit, price: number) => {
    const owned = state.unlockedOutfits.includes(id);
    if (owned) {
      playUiSound('tap', state.soundEnabled);
      onUpdateState((p) => ({ ...p, equippedOutfit: id }));
      return;
    }
    if (state.gems < price) {
      playUiSound('wrong', state.soundEnabled);
      return;
    }
    playUiSound('complete', state.soundEnabled);
    onUpdateState((p) => ({
      ...p,
      gems: p.gems - price,
      unlockedOutfits: [...p.unlockedOutfits, id],
      equippedOutfit: id,
    }));
  };

  const buyOrEquipTheme = (id: AppThemeId, price: number) => {
    const owned = state.unlockedThemes.includes(id);
    if (owned) {
      playUiSound('tap', state.soundEnabled);
      onUpdateState((p) => ({ ...p, equippedTheme: id }));
      return;
    }
    if (state.gems < price) {
      playUiSound('wrong', state.soundEnabled);
      return;
    }
    playUiSound('complete', state.soundEnabled);
    onUpdateState((p) => ({
      ...p,
      gems: p.gems - price,
      unlockedThemes: [...p.unlockedThemes, id],
      equippedTheme: id,
    }));
  };

  const buyXpBoost = () => {
    const price = 60;
    if (state.gems < price) {
      playUiSound('wrong', state.soundEnabled);
      return;
    }
    playUiSound('complete', state.soundEnabled);
    onUpdateState((p) => ({
      ...p,
      gems: p.gems - price,
      xpBoostUntil: Date.now() + 15 * 60 * 1000,
    }));
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <button onClick={onBack} className="inline-flex items-center gap-2 text-sm font-black text-[#1899D6] cursor-pointer">
          <ArrowLeft className="w-4 h-4" /> Назад в Практикум
        </button>
        <div className="flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-[#DDF4FF] border border-[#84D8FF] font-black text-[#1899D6]">
          <Gem className="w-4 h-4" /> {state.gems} кристаллов
        </div>
      </div>

      {/* 2X XP Booster */}
      <div className="rounded-3xl border-2 border-[#FFC800] bg-[#FFFDF2] p-5 flex flex-col sm:flex-row items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-2xl bg-[#FFC800] text-white flex items-center justify-center shrink-0">
            <Zap className="w-7 h-7 fill-white" />
          </div>
          <div>
            <div className="text-lg font-black text-[#3C3C3C]">Ускоритель 2X XP на 15 минут</div>
            <p className="text-xs font-bold text-[#777777]">
              {boostActive
                ? `Активен! Осталось ~${boostMins} мин — все уроки приносят двойной опыт.`
                : 'Удваивает весь получаемый опыт XP в уроках и бесконечном ИИ.'}
            </p>
          </div>
        </div>
        <button
          disabled={boostActive || state.gems < 60}
          onClick={buyXpBoost}
          className="duo-btn duo-btn-green rounded-2xl px-5 py-3 text-sm font-black shrink-0 disabled:opacity-50"
        >
          {boostActive ? 'АКТИВЕН 2X' : 'КУПИТЬ · 60 💎'}
        </button>
      </div>

      {/* Mascot Outfits */}
      <div className="space-y-3">
        <h3 className="text-lg font-black text-[#3C3C3C] flex items-center gap-2">
          <Sparkles className="w-5 h-5 text-[#CE82FF]" /> Костюмы Орлика Белика
        </h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {SHOP_OUTFITS.map((o) => {
            const owned = state.unlockedOutfits.includes(o.id);
            const equipped = state.equippedOutfit === o.id;
            return (
              <div
                key={o.id}
                className={`rounded-2xl border-2 p-4 flex items-center gap-3.5 ${
                  equipped ? 'border-[#58CC02] bg-[#F3FFE8]' : 'border-[#E5E5E5] bg-white'
                }`}
              >
                <MascotOrzel outfit={o.id} size={64} className="shrink-0" />
                <div className="flex-1 min-w-0">
                  <div className="font-black text-sm text-[#3C3C3C]">{o.name}</div>
                  <p className="text-xs font-bold text-[#777777] mb-2">{o.desc}</p>
                  <button
                    disabled={!owned && state.gems < o.price}
                    onClick={() => buyOrEquipOutfit(o.id, o.price)}
                    className={`px-3 py-1.5 rounded-xl text-xs font-black cursor-pointer disabled:opacity-45 ${
                      equipped
                        ? 'bg-[#58CC02] text-white'
                        : owned
                          ? 'bg-[#DDF4FF] text-[#1899D6]'
                          : 'bg-[#FFF0D6] text-[#CC7800]'
                    }`}
                  >
                    {equipped ? (
                      <span className="inline-flex items-center gap-1"><Check className="w-3.5 h-3.5" /> Надет</span>
                    ) : owned ? (
                      'Надеть'
                    ) : (
                      `Открыть · ${o.price} 💎`
                    )}
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* App Themes */}
      <div className="space-y-3">
        <h3 className="text-lg font-black text-[#3C3C3C] flex items-center gap-2">
          <Palette className="w-5 h-5 text-[#1CB0F6]" /> Темы оформления
        </h3>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {SHOP_THEMES.map((t) => {
            const owned = state.unlockedThemes.includes(t.id);
            const equipped = state.equippedTheme === t.id;
            return (
              <div
                key={t.id}
                className={`rounded-2xl border-2 p-4 space-y-2 ${
                  equipped ? 'border-[#1CB0F6] bg-[#F0FAFF]' : 'border-[#E5E5E5] bg-white'
                }`}
              >
                <div className="w-8 h-8 rounded-xl" style={{ backgroundColor: t.primary }} />
                <div className="font-black text-sm text-[#3C3C3C]">{t.name}</div>
                <p className="text-xs font-bold text-[#777777]">{t.desc}</p>
                <button
                  disabled={!owned && state.gems < t.price}
                  onClick={() => buyOrEquipTheme(t.id, t.price)}
                  className={`w-full py-2 rounded-xl text-xs font-black cursor-pointer disabled:opacity-45 ${
                    equipped
                      ? 'bg-[#1CB0F6] text-white'
                      : owned
                        ? 'bg-[#E5E5E5] text-[#3C3C3C]'
                        : 'bg-[#FFF0D6] text-[#CC7800]'
                  }`}
                >
                  {equipped ? 'Активна' : owned ? 'Применить' : `Купить · ${t.price} 💎`}
                </button>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
