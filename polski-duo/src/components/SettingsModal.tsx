import React, { useState } from 'react';
import {
  Bell,
  CheckCircle2,
  Copy,
  Download,
  RefreshCw,
  Upload,
  X,
} from 'lucide-react';
import { exportAIStateJson, importAIStateJson } from '../ai/localAiEngine';
import { LearnerAIState } from '../types/learning';
import {
  cancelAndroidReminder,
  checkGitHubApkUpdate,
  getInstalledAppVersion,
  openApkDownload,
  scheduleAndroidReminder,
  triggerTestReminder,
  UpdateCheckResult,
} from '../utils/androidApp';

interface SettingsModalProps {
  state: LearnerAIState;
  onUpdateState: (updater: (prev: LearnerAIState) => LearnerAIState) => void;
  onClose: () => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  state,
  onUpdateState,
  onClose,
}) => {
  const [updateCheck, setUpdateCheck] = useState<UpdateCheckResult | null>(null);
  const [checkingUpdate, setCheckingUpdate] = useState(false);
  const [backupText, setBackupText] = useState('');
  const [statusMsg, setStatusMsg] = useState<string | null>(null);

  const installedVersion = getInstalledAppVersion();

  const handleCheckUpdate = async () => {
    setCheckingUpdate(true);
    const res = await checkGitHubApkUpdate();
    setUpdateCheck(res);
    setCheckingUpdate(false);
  };

  const handleToggleReminder = (enabled: boolean) => {
    onUpdateState((prev) => ({ ...prev, reminderEnabled: enabled }));
    if (enabled) {
      scheduleAndroidReminder(state.reminderTime);
      setStatusMsg(`Ежедневное напоминание запланировано на ${state.reminderTime}`);
    } else {
      cancelAndroidReminder();
      setStatusMsg('Напоминания отключены');
    }
  };

  const handleTimeChange = (newTime: string) => {
    onUpdateState((prev) => ({ ...prev, reminderTime: newTime }));
    if (state.reminderEnabled) {
      scheduleAndroidReminder(newTime);
      setStatusMsg(`Время напоминания обновлено: ${newTime}`);
    }
  };

  const handleDownloadBackup = () => {
    const json = exportAIStateJson(state);
    setBackupText(json);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `polski-orzel-backup-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setStatusMsg('Файл резервной копии сохранён!');
  };

  const handleCopyBackup = async () => {
    const json = exportAIStateJson(state);
    setBackupText(json);
    try {
      await navigator.clipboard.writeText(json);
      setStatusMsg('Код прогресса скопирован в буфер обмена!');
    } catch {
      setStatusMsg('Код сформирован ниже — выделите и скопируйте его.');
    }
  };

  const handleRestore = () => {
    const restored = importAIStateJson(backupText);
    if (!restored) {
      setStatusMsg('Ошибка: вставьте корректный JSON-бэкап PolskiOrzeł.');
      return;
    }
    onUpdateState(() => restored);
    setStatusMsg('Прогресс успешно восстановлен!');
  };

  const handleFileUpload = (ev: React.ChangeEvent<HTMLInputElement>) => {
    const file = ev.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const content = String(reader.result ?? '');
      const restored = importAIStateJson(content);
      if (restored) {
        onUpdateState(() => restored);
        setBackupText(content);
        setStatusMsg('Прогресс восстановлен из файла!');
      } else {
        setStatusMsg('Файл не похож на резервную копию PolskiOrzeł.');
      }
    };
    reader.readAsText(file);
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/45 flex items-center justify-center p-4">
      <div className="bg-white rounded-3xl border-2 border-[#E5E5E5] w-full max-w-xl max-h-[90vh] overflow-y-auto p-5 sm:p-6 space-y-5 shadow-xl">
        <div className="flex items-center justify-between border-b border-[#E5E5E5] pb-3">
          <div>
            <h2 className="text-xl font-black text-[#3C3C3C]">Настройки, Обновления и Бэкап</h2>
            <div className="text-xs font-bold text-[#777777]">Версия: {installedVersion}</div>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl hover:bg-[#F7F7F7] cursor-pointer">
            <X className="w-5 h-5 text-[#777777]" />
          </button>
        </div>

        {statusMsg && (
          <div className="rounded-xl bg-[#D7FFB8] border border-[#58CC02] px-3.5 py-2 text-xs font-black text-[#46A302] flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 shrink-0" />
            <span>{statusMsg}</span>
          </div>
        )}

        {/* #4 App Updater */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] p-4 space-y-3">
          <div className="flex items-center justify-between gap-2">
            <div>
              <div className="text-sm font-black text-[#3C3C3C]">Обновление APK с GitHub</div>
              <div className="text-xs font-bold text-[#777777]">
                Новые версии ставятся прямо поверх текущей без потери прогресса.
              </div>
            </div>
            <button
              onClick={handleCheckUpdate}
              disabled={checkingUpdate}
              className="duo-btn duo-btn-blue rounded-xl px-3.5 py-2 text-xs font-black flex items-center gap-1.5 shrink-0"
            >
              <RefreshCw className={`w-4 h-4 ${checkingUpdate ? 'animate-spin' : ''}`} />
              Проверить
            </button>
          </div>

          {updateCheck && (
            <div className="rounded-xl bg-[#F7FCFF] border border-[#84D8FF] p-3 space-y-2 text-xs">
              {updateCheck.ok ? (
                <>
                  <div className="font-black text-[#1899D6]">
                    Актуальный билд на GitHub: {updateCheck.latestSha}
                  </div>
                  <div className="font-bold text-[#4B4B4B]">{updateCheck.latestMessage}</div>
                  {updateCheck.latestDate && (
                    <div className="text-[#777777] font-semibold">Обновлено: {updateCheck.latestDate}</div>
                  )}
                  <button
                    onClick={openApkDownload}
                    className="duo-btn duo-btn-green rounded-xl px-4 py-2 text-xs font-black inline-flex items-center gap-1.5 mt-1"
                  >
                    <Download className="w-4 h-4" /> Скачать свежий PolskiOrzel.apk
                  </button>
                </>
              ) : (
                <div className="font-bold text-[#DC143C]">{updateCheck.error}</div>
              )}
            </div>
          )}
        </div>

        {/* #5 Daily Reminders */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] p-4 space-y-3">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-2.5">
              <Bell className="w-5 h-5 text-[#FF9600]" />
              <div>
                <div className="text-sm font-black text-[#3C3C3C]">Ежедневные напоминания</div>
                <div className="text-xs font-bold text-[#777777]">
                  Офлайн-уведомление в Android, чтобы не терять серию
                </div>
              </div>
            </div>
            <input
              type="checkbox"
              checked={state.reminderEnabled}
              onChange={(e) => handleToggleReminder(e.target.checked)}
              className="w-5 h-5 accent-[#58CC02] cursor-pointer"
            />
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <label className="text-xs font-extrabold text-[#4B4B4B] flex items-center gap-2">
              Время:
              <input
                type="time"
                value={state.reminderTime}
                onChange={(e) => handleTimeChange(e.target.value)}
                className="rounded-lg border border-[#E5E5E5] px-2.5 py-1 text-xs font-black"
              />
            </label>
            <button
              type="button"
              onClick={() => {
                triggerTestReminder();
                setStatusMsg('Тестовое уведомление отправлено!');
              }}
              className="px-3 py-1.5 rounded-xl bg-[#FFF0D6] text-[#CC7800] text-xs font-black cursor-pointer"
            >
              Тест уведомления
            </button>
          </div>
        </div>

        {/* #10 Progress Export & Import */}
        <div className="rounded-2xl border-2 border-[#E5E5E5] p-4 space-y-3">
          <div className="text-sm font-black text-[#3C3C3C]">Бэкап и перенос прогресса</div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={handleDownloadBackup}
              className="px-3 py-2 rounded-xl border-2 border-[#E5E5E5] bg-white hover:bg-[#F7F7F7] text-xs font-black flex items-center gap-1.5 cursor-pointer"
            >
              <Download className="w-4 h-4 text-[#58CC02]" /> Сохранить в файл (.json)
            </button>
            <button
              type="button"
              onClick={handleCopyBackup}
              className="px-3 py-2 rounded-xl border-2 border-[#E5E5E5] bg-white hover:bg-[#F7F7F7] text-xs font-black flex items-center gap-1.5 cursor-pointer"
            >
              <Copy className="w-4 h-4 text-[#1CB0F6]" /> Скопировать код
            </button>
            <label className="px-3 py-2 rounded-xl border-2 border-[#E5E5E5] bg-white hover:bg-[#F7F7F7] text-xs font-black flex items-center gap-1.5 cursor-pointer">
              <Upload className="w-4 h-4 text-[#CE82FF]" /> Загрузить файл
              <input type="file" accept=".json,application/json" onChange={handleFileUpload} className="hidden" />
            </label>
          </div>

          <textarea
            value={backupText}
            onChange={(e) => setBackupText(e.target.value)}
            placeholder="Вставьте сюда код резервной копии JSON для восстановления..."
            rows={3}
            className="w-full rounded-xl border-2 border-[#E5E5E5] p-2.5 text-xs font-mono outline-none focus:border-[#1CB0F6]"
          />

          {backupText.trim().length > 0 && (
            <button
              type="button"
              onClick={handleRestore}
              className="duo-btn duo-btn-purple rounded-xl px-4 py-2 text-xs font-black"
            >
              Восстановить из текста
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
