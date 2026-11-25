import React, { useState } from 'react';
import {
  BrainCircuit,
  Database,
  FlaskConical,
  Plus,
  Sliders,
  Sparkles,
  Trash2,
} from 'lucide-react';
import { PERSONA_PRESETS } from '../data/modelCatalog';
import { BenchmarkRun, EnhancerConfig, FeedbackRule, KnowledgeNote } from '../types/ai';

interface EnhancerViewProps {
  config: EnhancerConfig;
  notes: KnowledgeNote[];
  rules: FeedbackRule[];
  benchmarks: BenchmarkRun[];
  benchmarkBusy: boolean;
  onChangeConfig: (next: EnhancerConfig) => void;
  onAddNote: (title: string, content: string) => void;
  onDeleteNote: (id: string) => void;
  onDeleteRule: (id: string) => void;
  onRunBenchmark: () => void;
}

export const EnhancerView: React.FC<EnhancerViewProps> = ({
  config,
  notes,
  rules,
  benchmarks,
  benchmarkBusy,
  onChangeConfig,
  onAddNote,
  onDeleteNote,
  onDeleteRule,
  onRunBenchmark,
}) => {
  const [noteTitle, setNoteTitle] = useState('');
  const [noteContent, setNoteContent] = useState('');

  const handleCreateNote = () => {
    if (!noteTitle.trim() || !noteContent.trim()) return;
    onAddNote(noteTitle.trim(), noteContent.trim());
    setNoteTitle('');
    setNoteContent('');
  };

  return (
    <div className="h-full overflow-y-auto px-4 py-4 space-y-5 max-w-2xl mx-auto">
      {/* Core Upgrade Toggles */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="flex items-center gap-2 text-sm font-extrabold text-white">
          <Sparkles className="w-4 h-4 text-violet-400" /> Улучшитель нейросети (апгрейд Gemma / LLM)
        </div>

        <label className="flex items-center justify-between gap-3 p-3 rounded-xl bg-slate-950/70 border border-white/5 cursor-pointer">
          <div>
            <div className="text-xs font-bold text-white flex items-center gap-1.5">
              <BrainCircuit className="w-3.5 h-3.5 text-violet-400" /> Глубокое мышление (Think → Verify)
            </div>
            <div className="text-[11px] text-slate-400">
              Заставляет маленькую модель сначала проверить логику в {'<think>'}, а потом отвечать
            </div>
          </div>
          <input
            type="checkbox"
            checked={config.deepThink}
            onChange={(e) => onChangeConfig({ ...config, deepThink: e.target.checked })}
            className="w-4 h-4 accent-violet-500"
          />
        </label>

        <label className="flex items-center justify-between gap-3 p-3 rounded-xl bg-slate-950/70 border border-white/5 cursor-pointer">
          <div>
            <div className="text-xs font-bold text-white flex items-center gap-1.5">
              <Database className="w-3.5 h-3.5 text-sky-400" /> Локальная RAG-память
            </div>
            <div className="text-[11px] text-slate-400">
              Автоматически подставляет релевантные факты из твоих заметок прямо в контекст
            </div>
          </div>
          <input
            type="checkbox"
            checked={config.useRag}
            onChange={(e) => onChangeConfig({ ...config, useRag: e.target.checked })}
            className="w-4 h-4 accent-sky-400"
          />
        </label>

        <label className="flex items-center justify-between gap-3 p-3 rounded-xl bg-slate-950/70 border border-white/5 cursor-pointer">
          <div>
            <div className="text-xs font-bold text-white flex items-center gap-1.5">
              <Sliders className="w-3.5 h-3.5 text-amber-400" /> Адаптация по оценкам (Few-Shot)
            </div>
            <div className="text-[11px] text-slate-400">
              Использует твои лайки и исправления, чтобы модель не повторяла старые ошибки
            </div>
          </div>
          <input
            type="checkbox"
            checked={config.useAdapter}
            onChange={(e) => onChangeConfig({ ...config, useAdapter: e.target.checked })}
            className="w-4 h-4 accent-amber-400"
          />
        </label>
      </div>

      {/* Persona + Inference Parameters */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="text-xs font-bold uppercase tracking-wider text-slate-400">Профиль и параметры генерации</div>

        <div className="grid grid-cols-2 gap-2">
          {PERSONA_PRESETS.map((p) => {
            const active = config.personaId === p.id;
            return (
              <button
                key={p.id}
                type="button"
                onClick={() =>
                  onChangeConfig({ ...config, personaId: p.id, systemPrompt: p.systemPrompt })
                }
                className={`p-2.5 rounded-xl border text-left cursor-pointer ${
                  active
                    ? 'bg-sky-500/20 border-sky-400 text-white'
                    : 'bg-slate-950/60 border-white/10 text-slate-300'
                }`}
              >
                <div className="text-xs font-bold">{p.name}</div>
                <div className="text-[10px] text-slate-400 mt-0.5 line-clamp-2">{p.description}</div>
              </button>
            );
          })}
        </div>

        <textarea
          rows={2}
          value={config.systemPrompt}
          onChange={(e) => onChangeConfig({ ...config, systemPrompt: e.target.value })}
          className="w-full rounded-xl bg-slate-950 border border-white/15 p-2.5 text-xs text-slate-200 outline-none focus:border-sky-400"
        />

        <div className="grid grid-cols-2 gap-3 text-xs">
          <div>
            <div className="flex justify-between text-slate-300 mb-1">
              <span>Температура</span>
              <span className="font-mono text-sky-300">{config.temperature}</span>
            </div>
            <input
              type="range"
              min={0.1}
              max={1.2}
              step={0.05}
              value={config.temperature}
              onChange={(e) => onChangeConfig({ ...config, temperature: Number(e.target.value) })}
              className="w-full accent-sky-400"
            />
          </div>

          <div>
            <div className="flex justify-between text-slate-300 mb-1">
              <span>Макс. токенов</span>
              <span className="font-mono text-sky-300">{config.maxTokens}</span>
            </div>
            <input
              type="range"
              min={64}
              max={1024}
              step={32}
              value={config.maxTokens}
              onChange={(e) => onChangeConfig({ ...config, maxTokens: Number(e.target.value) })}
              className="w-full accent-sky-400"
            />
          </div>
        </div>
      </div>

      {/* Knowledge Base RAG */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="text-xs font-bold uppercase tracking-wider text-slate-400">
          Локальная база знаний ({notes.length})
        </div>

        <div className="space-y-2">
          <input
            type="text"
            value={noteTitle}
            onChange={(e) => setNoteTitle(e.target.value)}
            placeholder="Заголовок факта (напр. Мой проект)"
            className="w-full rounded-xl bg-slate-950 border border-white/15 px-3 py-2 text-xs text-white outline-none focus:border-sky-400"
          />
          <textarea
            rows={2}
            value={noteContent}
            onChange={(e) => setNoteContent(e.target.value)}
            placeholder="Текст, правила или документация, которые ИИ должен знать..."
            className="w-full rounded-xl bg-slate-950 border border-white/15 px-3 py-2 text-xs text-white outline-none focus:border-sky-400"
          />
          <button
            type="button"
            onClick={handleCreateNote}
            className="px-3.5 py-2 rounded-xl bg-sky-400 text-slate-950 font-bold text-xs flex items-center gap-1.5 cursor-pointer"
          >
            <Plus className="w-3.5 h-3.5" /> Добавить в память ИИ
          </button>
        </div>

        <div className="space-y-2">
          {notes.map((n) => (
            <div
              key={n.id}
              className="p-3 rounded-xl bg-slate-950/70 border border-white/10 flex items-start justify-between gap-2"
            >
              <div className="min-w-0">
                <div className="text-xs font-bold text-sky-300">{n.title}</div>
                <div className="text-[11px] text-slate-300 mt-0.5">{n.content}</div>
              </div>
              <button
                type="button"
                onClick={() => onDeleteNote(n.id)}
                className="p-1 text-slate-500 hover:text-rose-400 cursor-pointer shrink-0"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>
      </div>

      {/* Learned Feedback Rules */}
      {rules.length > 0 && (
        <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-2">
          <div className="text-xs font-bold uppercase tracking-wider text-amber-300">
            Выученные исправления ({rules.length})
          </div>
          {rules.map((r) => (
            <div
              key={r.id}
              className="p-3 rounded-xl bg-slate-950/70 border border-amber-400/20 flex items-start justify-between gap-2"
            >
              <div className="min-w-0 space-y-1">
                <div className="text-[11px] text-slate-400">Запрос: {r.prompt}</div>
                <div className="text-xs text-emerald-300 font-medium">Эталон: {r.idealReply}</div>
              </div>
              <button
                type="button"
                onClick={() => onDeleteRule(r.id)}
                className="p-1 text-slate-500 hover:text-rose-400 cursor-pointer shrink-0"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Benchmark Before vs After */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="flex items-center justify-between gap-2">
          <div>
            <div className="text-xs font-extrabold text-white flex items-center gap-1.5">
              <FlaskConical className="w-4 h-4 text-emerald-400" /> Бенчмарк: Базовая vs Улучшенная
            </div>
            <div className="text-[11px] text-slate-400">
              Сравнивает чистый ответ модели против версии с Think + RAG + Адаптером
            </div>
          </div>
          <button
            type="button"
            disabled={benchmarkBusy}
            onClick={onRunBenchmark}
            className="px-3.5 py-2 rounded-xl bg-emerald-400 hover:bg-emerald-300 disabled:opacity-50 text-slate-950 font-bold text-xs cursor-pointer shrink-0"
          >
            {benchmarkBusy ? 'Тестируем...' : 'Запустить тест'}
          </button>
        </div>

        {benchmarks.map((b, idx) => (
          <div key={idx} className="rounded-xl bg-slate-950/80 border border-white/10 p-3 space-y-2">
            <div className="text-xs font-bold text-white">{b.prompt}</div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px]">
              <div className="p-2.5 rounded-lg bg-white/5 border border-white/10 space-y-1">
                <div className="flex justify-between font-bold text-slate-400">
                  <span>Базовая</span>
                  <span>{b.baseScore}% · {b.baseMs}мс</span>
                </div>
                <div className="text-slate-300 whitespace-pre-wrap">{b.baseReply}</div>
              </div>
              <div className="p-2.5 rounded-lg bg-emerald-500/10 border border-emerald-400/30 space-y-1">
                <div className="flex justify-between font-bold text-emerald-300">
                  <span>Улучшенная (ИИ+)</span>
                  <span>{b.enhancedScore}% · {b.enhancedMs}мс</span>
                </div>
                <div className="text-slate-100 whitespace-pre-wrap">{b.enhancedReply}</div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
