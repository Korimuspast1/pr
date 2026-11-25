import React, { useEffect, useRef, useState } from 'react';
import {
  BrainCircuit,
  ChevronDown,
  ChevronUp,
  Database,
  Edit3,
  Send,
  Sparkles,
  Square,
  ThumbsDown,
  ThumbsUp,
  Trash2,
} from 'lucide-react';
import { ChatMessage } from '../types/ai';

interface ChatViewProps {
  messages: ChatMessage[];
  activeModelName: string;
  isGenerating: boolean;
  deepThink: boolean;
  useRag: boolean;
  onSend: (text: string) => void;
  onStop: () => void;
  onClear: () => void;
  onRate: (msgId: string, rating: 'up' | 'down', idealReply?: string) => void;
  onOpenModels: () => void;
}

const STARTER_PROMPTS = [
  'Привет! Расскажи, как ты работаешь без API',
  'У меня 3 яблока, я отдал 1 и купил ещё 4. Сколько стало?',
  'Как мне скачать и улучшить Gemma 3 на телефоне?',
];

export const ChatView: React.FC<ChatViewProps> = ({
  messages,
  activeModelName,
  isGenerating,
  deepThink,
  useRag,
  onSend,
  onStop,
  onClear,
  onRate,
  onOpenModels,
}) => {
  const [input, setInput] = useState('');
  const [openThoughts, setOpenThoughts] = useState<Record<string, boolean>>({});
  const [editingMsgId, setEditingMsgId] = useState<string | null>(null);
  const [idealDraft, setIdealDraft] = useState('');
  const bottomRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages.length, isGenerating]);

  const submit = () => {
    const clean = input.trim();
    if (!clean || isGenerating) return;
    setInput('');
    onSend(clean);
  };

  return (
    <div className="flex flex-col h-full">
      {/* Compact active status bar */}
      <div className="px-4 py-2.5 border-b border-white/10 bg-slate-950/80 flex items-center justify-between gap-2 shrink-0">
        <button
          type="button"
          onClick={onOpenModels}
          className="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-white/5 border border-white/10 hover:border-sky-400/50 cursor-pointer min-w-0"
        >
          <span className="w-2 h-2 rounded-full bg-emerald-400 shrink-0" />
          <span className="text-xs font-bold text-white truncate">{activeModelName}</span>
        </button>

        <div className="flex items-center gap-1.5 shrink-0">
          {deepThink && (
            <span className="px-2 py-1 rounded-lg bg-violet-500/20 border border-violet-400/30 text-[10px] font-bold text-violet-200 flex items-center gap-1">
              <BrainCircuit className="w-3 h-3" /> Мысли
            </span>
          )}
          {useRag && (
            <span className="px-2 py-1 rounded-lg bg-sky-500/20 border border-sky-400/30 text-[10px] font-bold text-sky-200 flex items-center gap-1">
              <Database className="w-3 h-3" /> RAG
            </span>
          )}
          {messages.length > 0 && (
            <button
              type="button"
              onClick={onClear}
              className="p-1.5 rounded-lg bg-white/5 text-slate-400 hover:text-rose-400 cursor-pointer"
              title="Очистить чат"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      </div>

      {/* Messages area */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
        {messages.length === 0 && (
          <div className="max-w-lg mx-auto pt-8 text-center space-y-5">
            <div className="w-14 h-14 mx-auto rounded-2xl bg-gradient-to-br from-violet-500/30 to-sky-500/30 border border-white/15 flex items-center justify-center">
              <Sparkles className="w-7 h-7 text-sky-300" />
            </div>
            <div className="space-y-1.5">
              <h2 className="text-lg font-extrabold text-white">PocketAI Studio</h2>
              <p className="text-xs text-slate-400 leading-relaxed">
                Общайся со встроенной нейросетью прямо сейчас или скачай настоящую{' '}
                <span className="text-sky-300 font-semibold">Gemma 3 / Qwen 2.5</span> во вкладке «Модели» — всё работает локально без API.
              </p>
            </div>
            <div className="space-y-2 text-left">
              {STARTER_PROMPTS.map((p) => (
                <button
                  key={p}
                  type="button"
                  onClick={() => onSend(p)}
                  className="w-full px-3.5 py-2.5 rounded-2xl bg-white/5 hover:bg-white/10 border border-white/10 text-xs font-medium text-slate-200 cursor-pointer text-left transition-colors"
                >
                  {p}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((msg) => {
          const isUser = msg.role === 'user';
          const showThought = Boolean(openThoughts[msg.id]);

          return (
            <div
              key={msg.id}
              className={`flex flex-col ${isUser ? 'items-end' : 'items-start'} max-w-2xl mx-auto w-full`}
            >
              <div
                className={`rounded-2xl px-4 py-3 text-sm leading-relaxed max-w-[90%] ${
                  isUser
                    ? 'bg-sky-500 text-slate-950 font-semibold rounded-br-sm'
                    : 'bg-slate-900/90 border border-white/10 text-slate-100 rounded-bl-sm'
                }`}
              >
                {/* Collapsible Thinking Process */}
                {!isUser && msg.thinking && (
                  <div className="mb-2.5">
                    <button
                      type="button"
                      onClick={() => setOpenThoughts((p) => ({ ...p, [msg.id]: !p[msg.id] }))}
                      className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-violet-500/15 border border-violet-400/30 text-[11px] font-bold text-violet-300 cursor-pointer"
                    >
                      <BrainCircuit className="w-3.5 h-3.5" />
                      Ход мыслей ИИ
                      {showThought ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
                    </button>
                    {showThought && (
                      <div className="mt-1.5 p-2.5 rounded-xl bg-slate-950/80 border border-violet-500/20 text-[11px] text-slate-300 whitespace-pre-wrap font-mono">
                        {msg.thinking}
                      </div>
                    )}
                  </div>
                )}

                <div className="whitespace-pre-wrap break-words">{msg.content}</div>

                {/* Metadata + RAG pills + Feedback buttons */}
                {!isUser && (
                  <div className="mt-2.5 pt-2 border-t border-white/10 flex flex-wrap items-center justify-between gap-2 text-[10px] text-slate-400">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="font-semibold text-slate-300">{msg.modelName}</span>
                      {msg.tokensPerSec && (
                        <span className="px-1.5 py-0.5 rounded bg-white/5 text-emerald-300 font-mono">
                          {msg.tokensPerSec} tok/s
                        </span>
                      )}
                      {msg.ragTitles && msg.ragTitles.length > 0 && (
                        <span className="px-1.5 py-0.5 rounded bg-sky-500/15 text-sky-300">
                          RAG: {msg.ragTitles.join(', ')}
                        </span>
                      )}
                      {msg.usedAdapterCount ? (
                        <span className="px-1.5 py-0.5 rounded bg-amber-500/15 text-amber-300">
                          Адаптер ({msg.usedAdapterCount})
                        </span>
                      ) : null}
                    </div>

                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        onClick={() => onRate(msg.id, 'up')}
                        className={`p-1 rounded-lg cursor-pointer ${
                          msg.feedback === 'up'
                            ? 'bg-emerald-500/25 text-emerald-300'
                            : 'hover:bg-white/10 text-slate-400'
                        }`}
                        title="Отличный ответ (запомнить стиль)"
                      >
                        <ThumbsUp className="w-3.5 h-3.5" />
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setEditingMsgId(msg.id);
                          setIdealDraft(msg.content);
                        }}
                        className={`p-1 rounded-lg cursor-pointer ${
                          msg.feedback === 'down'
                            ? 'bg-rose-500/25 text-rose-300'
                            : 'hover:bg-white/10 text-slate-400'
                        }`}
                        title="Исправить ответ (дообучить адаптер)"
                      >
                        <ThumbsDown className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Inline correction box to teach the AI */}
              {editingMsgId === msg.id && (
                <div className="mt-2 w-full max-w-[90%] rounded-2xl bg-slate-900 border border-amber-400/40 p-3 space-y-2">
                  <div className="text-[11px] font-bold text-amber-300 flex items-center gap-1.5">
                    <Edit3 className="w-3.5 h-3.5" /> Как ИИ должен был ответить? (Адаптер запомнит)
                  </div>
                  <textarea
                    rows={2}
                    value={idealDraft}
                    onChange={(e) => setIdealDraft(e.target.value)}
                    className="w-full rounded-xl bg-slate-950 border border-white/15 p-2 text-xs text-white outline-none focus:border-amber-400"
                  />
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={() => setEditingMsgId(null)}
                      className="px-2.5 py-1 rounded-lg bg-white/5 text-xs text-slate-300 cursor-pointer"
                    >
                      Отмена
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        if (idealDraft.trim()) {
                          onRate(msg.id, 'down', idealDraft.trim());
                        }
                        setEditingMsgId(null);
                      }}
                      className="px-3 py-1 rounded-lg bg-amber-400 text-slate-950 font-bold text-xs cursor-pointer"
                    >
                      Сохранить в адаптер
                    </button>
                  </div>
                </div>
              )}
            </div>
          );
        })}
        <div ref={bottomRef} />
      </div>

      {/* Input Bar */}
      <div className="p-3 border-t border-white/10 bg-slate-950/95 shrink-0">
        <div className="max-w-2xl mx-auto flex items-center gap-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') submit();
            }}
            placeholder="Напиши сообщение нейросети..."
            className="flex-1 rounded-2xl bg-slate-900 border border-white/15 focus:border-sky-400 px-4 py-3 text-sm text-white placeholder-slate-400 outline-none"
          />
          {isGenerating ? (
            <button
              type="button"
              onClick={onStop}
              className="w-11 h-11 rounded-2xl bg-rose-500 text-white flex items-center justify-center cursor-pointer shrink-0"
              title="Остановить генерацию"
            >
              <Square className="w-4 h-4 fill-white" />
            </button>
          ) : (
            <button
              type="button"
              onClick={submit}
              className="w-11 h-11 rounded-2xl bg-sky-400 hover:bg-sky-300 text-slate-950 flex items-center justify-center cursor-pointer shrink-0"
              title="Отправить"
            >
              <Send className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
