import React from 'react';
import { CheckCircle2, Settings, ShieldAlert, Wrench, X } from 'lucide-react';
import { NativeMockStatus } from '../types/gps';
import {
  nativeOpenDeveloperSettings,
  nativeRequestLocationPermission,
} from '../utils/nativeMockGps';

interface SetupGuideModalProps {
  status: NativeMockStatus;
  onRefreshStatus: () => void;
  onClose: () => void;
}

export const SetupGuideModal: React.FC<SetupGuideModalProps> = ({
  status,
  onRefreshStatus,
  onClose,
}) => {
  return (
    <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4 backdrop-blur-xs">
      <div className="w-full max-w-lg rounded-3xl bg-slate-900 border border-slate-700 p-5 sm:p-6 space-y-4 shadow-2xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <div className="flex items-center gap-2.5">
            <Wrench className="w-5 h-5 text-sky-400" />
            <h2 className="text-lg font-extrabold text-white">Настройка Fake GPS на Android</h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-slate-800 text-slate-400 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <p className="text-xs sm:text-sm text-slate-300 leading-relaxed">
          Android требует разрешить фиктивные местоположения в системных настройках разработчика (без root-прав):
        </p>

        {/* Step 1: Location Permission */}
        <div className="rounded-2xl border border-slate-800 bg-slate-950/70 p-4 space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-bold text-white">1. Разрешение на геолокацию</span>
            {status.hasLocationPermission ? (
              <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-400">
                <CheckCircle2 className="w-4 h-4" /> Разрешено
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 text-xs font-bold text-amber-400">
                <ShieldAlert className="w-4 h-4" /> Требуется
              </span>
            )}
          </div>
          {!status.hasLocationPermission && (
            <button
              type="button"
              onClick={() => {
                nativeRequestLocationPermission();
                setTimeout(onRefreshStatus, 800);
              }}
              className="px-3.5 py-2 rounded-xl bg-sky-500 text-slate-950 font-bold text-xs cursor-pointer"
            >
              Выдать разрешение
            </button>
          )}
        </div>

        {/* Step 2: Developer Mode */}
        <div className="rounded-2xl border border-slate-800 bg-slate-950/70 p-4 space-y-1.5">
          <div className="text-sm font-bold text-white">2. Включите режим разработчика (если выключен)</div>
          <p className="text-xs text-slate-400 leading-relaxed">
            Откройте <strong>Настройки телефона → О телефоне → Номер сборки</strong> (или версия MIUI/HyperOS) и нажмите <strong>7 раз</strong>, пока не появится «Вы стали разработчиком».
          </p>
        </div>

        {/* Step 3: Mock Location App */}
        <div className="rounded-2xl border border-slate-800 bg-slate-950/70 p-4 space-y-2.5">
          <div className="flex items-center justify-between gap-2">
            <span className="text-sm font-bold text-white">3. Выберите FakeGpsPro для фиктивных GPS</span>
            {status.hasMockPermission ? (
              <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-400">
                <CheckCircle2 className="w-4 h-4" /> Готово
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 text-xs font-bold text-amber-400">
                <ShieldAlert className="w-4 h-4" /> Не выбрано
              </span>
            )}
          </div>
          <p className="text-xs text-slate-400 leading-relaxed">
            В открывшемся окне найдите пункт <strong>«Приложение для фиктивных местоположений»</strong> (Select mock location app) и выберите <strong>FakeGpsPro</strong>.
          </p>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={nativeOpenDeveloperSettings}
              className="px-3.5 py-2 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-xs inline-flex items-center gap-1.5 cursor-pointer"
            >
              <Settings className="w-4 h-4" /> Открыть Параметры разработчика
            </button>
            <button
              type="button"
              onClick={onRefreshStatus}
              className="px-3.5 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 font-bold text-xs cursor-pointer"
            >
              Проверить статус
            </button>
          </div>
        </div>

        {status.errorMessage && (
          <div className="rounded-xl border border-rose-500/40 bg-rose-500/10 p-3 text-xs font-semibold text-rose-300">
            {status.errorMessage}
          </div>
        )}

        <button
          type="button"
          onClick={onClose}
          className="w-full py-3 rounded-2xl bg-slate-800 hover:bg-slate-700 text-white font-bold text-sm cursor-pointer"
        >
          Закрыть
        </button>
      </div>
    </div>
  );
};
