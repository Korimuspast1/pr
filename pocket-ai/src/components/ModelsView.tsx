import React, { useEffect, useState } from 'react';
import {
  CheckCircle2,
  Cpu,
  Download,
  FolderOpen,
  HardDrive,
  Search,
  Sparkles,
  Trash2,
} from 'lucide-react';
import { CachedModelInfo, HFSearchHit, searchHuggingFaceGGUF, wllamaService } from '../ai/wllamaEngine';
import { MODEL_CATALOG } from '../data/modelCatalog';
import { ModelCatalogItem } from '../types/ai';

interface ModelsViewProps {
  activeModelName: string;
  contextSize: number;
  onModelActivated: (name: string) => void;
  onSwitchToBuiltIn: () => void;
  onOpenChat: () => void;
}

export const ModelsView: React.FC<ModelsViewProps> = ({
  activeModelName,
  contextSize,
  onModelActivated,
  onSwitchToBuiltIn,
  onOpenChat,
}) => {
  const [cached, setCached] = useState<CachedModelInfo[]>([]);
  const [busyRepo, setBusyRepo] = useState<string | null>(null);
  const [progress, setProgress] = useState<{ loaded: number; total: number } | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [hfQuery, setHfQuery] = useState('');
  const [hfHits, setHfHits] = useState<HFSearchHit[]>([]);
  const [searchingHf, setSearchingHf] = useState(false);

  const refreshCache = async () => {
    const list = await wllamaService.listCachedModels();
    setCached(list);
  };

  useEffect(() => {
    refreshCache();
  }, []);

  const handleDownloadHF = async (repo: string, quant: string, label: string) => {
    setErrorMsg(null);
    setBusyRepo(repo);
    setProgress({ loaded: 0, total: 1 });
    try {
      await wllamaService.loadFromHuggingFace(repo, quant, label, contextSize, (loaded, total) => {
        setProgress({ loaded, total: Math.max(1, total) });
      });
      await refreshCache();
      onModelActivated(label);
      onOpenChat();
    } catch (e) {
      setErrorMsg(
        `Не удалось скачать ${label}: ${e instanceof Error ? e.message : String(e)}. Попробуй другую модель или выбери .gguf файл с устройства.`,
      );
    } finally {
      setBusyRepo(null);
      setProgress(null);
    }
  };

  const handleLocalFile = async (ev: React.ChangeEvent<HTMLInputElement>) => {
    const files = ev.target.files ? Array.from(ev.target.files) : [];
    if (!files.length) return;
    setErrorMsg(null);
    setBusyRepo('local');
    try {
      const label = await wllamaService.loadFromLocalFiles(files, contextSize);
      onModelActivated(label);
      onOpenChat();
    } catch (e) {
      setErrorMsg(`Ошибка загрузки файла: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setBusyRepo(null);
    }
  };

  const handleSearchHF = async () => {
    setSearchingHf(true);
    setErrorMsg(null);
    try {
      const hits = await searchHuggingFaceGGUF(hfQuery);
      setHfHits(hits);
    } catch (e) {
      setErrorMsg(`Поиск HuggingFace недоступен: ${e instanceof Error ? e.message : String(e)}`);
    } finally {
      setSearchingHf(false);
    }
  };

  const pct = progress ? Math.min(100, Math.round((progress.loaded / progress.total) * 100)) : 0;
  const loadedMb = progress ? (progress.loaded / (1024 * 1024)).toFixed(1) : '0';
  const totalMb = progress ? (progress.total / (1024 * 1024)).toFixed(1) : '?';

  return (
    <div className="h-full overflow-y-auto px-4 py-4 space-y-5 max-w-2xl mx-auto">
      {/* Built-in Ready-to-use Model */}
      <div className="rounded-2xl bg-gradient-to-br from-violet-950/60 to-slate-900 border border-violet-400/30 p-4 space-y-2.5">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Cpu className="w-5 h-5 text-violet-300" />
            <div>
              <div className="text-sm font-extrabold text-white">PocketGPT-Mini (встроенная)</div>
              <div className="text-[11px] text-slate-300">Уже вшита в приложение · 0 МБ скачивания · Обучается локально</div>
            </div>
          </div>
          {activeModelName.startsWith('PocketGPT') ? (
            <span className="px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-300 text-[11px] font-bold flex items-center gap-1">
              <CheckCircle2 className="w-3.5 h-3.5" /> Активна
            </span>
          ) : (
            <button
              type="button"
              onClick={onSwitchToBuiltIn}
              className="px-3 py-1.5 rounded-xl bg-violet-500 text-white font-bold text-xs cursor-pointer"
            >
              Выбрать
            </button>
          )}
        </div>
      </div>

      {/* Download progress */}
      {busyRepo && progress && (
        <div className="rounded-2xl bg-slate-900 border border-sky-400/40 p-4 space-y-2">
          <div className="flex items-center justify-between text-xs font-bold">
            <span className="text-sky-300">Загрузка весов GGUF на устройство...</span>
            <span className="text-white font-mono">
              {pct}% ({loadedMb} / {totalMb} МБ)
            </span>
          </div>
          <div className="h-2 rounded-full bg-slate-800 overflow-hidden">
            <div className="h-full bg-sky-400 transition-all" style={{ width: `${pct}%` }} />
          </div>
        </div>
      )}

      {errorMsg && (
        <div className="rounded-2xl bg-rose-950/60 border border-rose-400/40 p-3 text-xs text-rose-200">
          {errorMsg}
        </div>
      )}

      {/* Local GGUF Picker */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 flex items-center justify-between gap-3">
        <div>
          <div className="text-xs font-bold text-white">Открыть свой .gguf файл</div>
          <div className="text-[11px] text-slate-400">Если ты уже скачал Gemma / Qwen / Llama на телефон или ПК</div>
        </div>
        <label className="px-3.5 py-2 rounded-xl bg-white/10 hover:bg-white/15 border border-white/15 text-xs font-bold text-white flex items-center gap-1.5 cursor-pointer shrink-0">
          <FolderOpen className="w-4 h-4 text-sky-400" />
          Выбрать файл
          <input type="file" accept=".gguf" className="hidden" onChange={handleLocalFile} />
        </label>
      </div>

      {/* Cached Offline Models */}
      {cached.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
            <HardDrive className="w-3.5 h-3.5 text-emerald-400" /> Скачанные (работают офлайн)
          </h3>
          {cached.map((item) => (
            <div
              key={item.url}
              className="rounded-2xl bg-slate-900 border border-emerald-400/25 p-3.5 flex items-center justify-between gap-3"
            >
              <div className="min-w-0">
                <div className="text-xs font-bold text-white truncate">{item.name}</div>
                <div className="text-[11px] text-slate-400">{item.sizeMb} МБ · Кэш OPFS</div>
              </div>
              <div className="flex items-center gap-1.5 shrink-0">
                <button
                  type="button"
                  onClick={async () => {
                    setBusyRepo(item.url);
                    try {
                      await wllamaService.loadCachedModel(item, contextSize);
                      onModelActivated(item.name);
                      onOpenChat();
                    } finally {
                      setBusyRepo(null);
                    }
                  }}
                  className="px-3 py-1.5 rounded-xl bg-emerald-400 text-slate-950 font-bold text-xs cursor-pointer"
                >
                  Запустить
                </button>
                <button
                  type="button"
                  onClick={async () => {
                    await wllamaService.deleteCachedModel(item);
                    await refreshCache();
                  }}
                  className="p-1.5 rounded-xl bg-white/5 text-slate-400 hover:text-rose-400 cursor-pointer"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Curated Gemma / Qwen / SmolLM Catalog */}
      <div className="space-y-2.5">
        <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
          <Sparkles className="w-3.5 h-3.5 text-sky-400" /> Скачать лёгкую модель (llama.cpp WASM)
        </h3>

        {MODEL_CATALOG.map((m: ModelCatalogItem) => (
          <div
            key={m.id}
            className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-2"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-sm font-extrabold text-white">{m.name}</span>
                  {m.isGemma && (
                    <span className="px-2 py-0.5 rounded-full bg-sky-500/20 text-sky-300 text-[10px] font-bold">
                      Google Gemma
                    </span>
                  )}
                  <span className="px-2 py-0.5 rounded-full bg-white/10 text-slate-300 text-[10px] font-mono">
                    {m.sizeLabel}
                  </span>
                </div>
                <div className="text-[11px] text-slate-400 mt-1">{m.description}</div>
                <div className="text-[10px] text-slate-500 font-mono mt-1">{m.repo} ({m.quant})</div>
              </div>

              <button
                type="button"
                disabled={Boolean(busyRepo)}
                onClick={() => handleDownloadHF(m.repo, m.quant, m.name)}
                className="px-3.5 py-2 rounded-xl bg-sky-400 hover:bg-sky-300 disabled:opacity-50 text-slate-950 font-bold text-xs flex items-center gap-1.5 cursor-pointer shrink-0"
              >
                <Download className="w-3.5 h-3.5" />
                Скачать
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Live HuggingFace GGUF Search */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="text-xs font-bold text-white">Поиск любой другой GGUF-модели на HuggingFace</div>
        <div className="flex gap-2">
          <input
            type="text"
            value={hfQuery}
            onChange={(e) => setHfQuery(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearchHF()}
            placeholder="Например: gemma 3, qwen 0.5b, deepseek..."
            className="flex-1 rounded-xl bg-slate-950 border border-white/15 px-3 py-2 text-xs text-white outline-none focus:border-sky-400"
          />
          <button
            type="button"
            onClick={handleSearchHF}
            disabled={searchingHf}
            className="px-3.5 py-2 rounded-xl bg-white/10 hover:bg-white/15 text-xs font-bold text-white flex items-center gap-1.5 cursor-pointer"
          >
            <Search className="w-3.5 h-3.5" /> Найти
          </button>
        </div>

        {hfHits.length > 0 && (
          <div className="space-y-1.5">
            {hfHits.map((h) => (
              <div
                key={h.repo}
                className="px-3 py-2 rounded-xl bg-slate-950/70 border border-white/5 flex items-center justify-between gap-2"
              >
                <div className="min-w-0">
                  <div className="text-xs font-mono text-white truncate">{h.repo}</div>
                  <div className="text-[10px] text-slate-400">Скачиваний: {h.downloads.toLocaleString('ru-RU')}</div>
                </div>
                <button
                  type="button"
                  disabled={Boolean(busyRepo)}
                  onClick={() => handleDownloadHF(h.repo, 'Q4_K_M', h.repo.split('/').pop() || h.repo)}
                  className="px-2.5 py-1 rounded-lg bg-sky-400 text-slate-950 font-bold text-[11px] cursor-pointer shrink-0"
                >
                  Загрузить
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
