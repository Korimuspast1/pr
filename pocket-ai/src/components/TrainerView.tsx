import React, { useEffect, useRef, useState } from 'react';
import { Activity, Cpu, Download, Play, RotateCcw, Sparkles, Square } from 'lucide-react';
import { MiniTransformer } from '../ai/miniTransformer';
import { TRAINER_DATASETS } from '../data/modelCatalog';

interface TrainerViewProps {
  model: MiniTransformer;
  onModelUpdated: (updated: MiniTransformer) => void;
}

export const TrainerView: React.FC<TrainerViewProps> = ({ model, onModelUpdated }) => {
  const [corpus, setCorpus] = useState<string>(TRAINER_DATASETS[0].text);
  const [lr, setLr] = useState(0.003);
  const [training, setTraining] = useState(false);
  const [stepCount, setStepCount] = useState(model.stepCount);
  const [loss, setLoss] = useState(model.lastLoss);
  const [lossHistory, setLossHistory] = useState<number[]>(model.lossHistory);

  const [testPrompt, setTestPrompt] = useState('Вопрос: Привет! Кто ты?\nОтвет:');
  const [sampleOutput, setSampleOutput] = useState('');
  const [temperature, setTemperature] = useState(0.35);

  const timerRef = useRef<number | null>(null);
  const tokensRef = useRef<number[]>([]);

  useEffect(() => {
    return () => {
      if (timerRef.current) window.clearInterval(timerRef.current);
    };
  }, []);

  const stopTraining = () => {
    if (timerRef.current) {
      window.clearInterval(timerRef.current);
      timerRef.current = null;
    }
    setTraining(false);
    onModelUpdated(model);
  };

  const startTraining = () => {
    if (training) {
      stopTraining();
      return;
    }

    // Ensure vocab covers all characters in custom corpus
    const missingChars = Array.from(new Set(Array.from(corpus))).filter(
      (ch) => !model.charToId.has(ch),
    );
    if (missingChars.length > 0) {
      const nextVocab = [...model.vocab, ...missingChars];
      const rebuilt = new MiniTransformer(nextVocab, {
        blockSize: model.config.blockSize,
        dModel: model.config.dModel,
        nHead: model.config.nHead,
        nLayer: model.config.nLayer,
      });
      tokensRef.current = rebuilt.encode(corpus);
      onModelUpdated(rebuilt);
      setStepCount(rebuilt.stepCount);
      setLoss(rebuilt.lastLoss);
      setLossHistory(rebuilt.lossHistory);
    } else {
      tokensRef.current = model.encode(corpus);
    }

    if (tokensRef.current.length < 16) return;

    setTraining(true);
    timerRef.current = window.setInterval(() => {
      const l = model.trainStep(tokensRef.current, lr, 3);
      setStepCount(model.stepCount);
      setLoss(l);
      setLossHistory([...model.lossHistory]);
      if (model.stepCount % 15 === 0) {
        onModelUpdated(model);
      }
    }, 35);
  };

  const handleResetFresh = () => {
    stopTraining();
    const vocab = Array.from(new Set(Array.from(corpus)));
    const fresh = new MiniTransformer(vocab, {
      blockSize: 40,
      dModel: 48,
      nHead: 4,
      nLayer: 2,
    });
    onModelUpdated(fresh);
    setStepCount(0);
    setLoss(fresh.lastLoss);
    setLossHistory([]);
    setSampleOutput('');
  };

  const handleGenerate = () => {
    const out = model.generate(testPrompt, 120, temperature, 5);
    setSampleOutput(out || '(пустой вывод — обучи ещё 50 шагов)');
  };

  const handleDownloadCheckpoint = () => {
    const blob = new Blob([JSON.stringify(model.exportCheckpoint())], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `pocketgpt-step-${model.stepCount}.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  // Build SVG path for loss chart
  const pts = lossHistory.length >= 2 ? lossHistory : [4.5, loss];
  const maxL = Math.max(5.0, ...pts);
  const minL = Math.min(0.1, ...pts);
  const span = Math.max(0.2, maxL - minL);
  const polyPoints = pts
    .map((v, i) => {
      const x = (i / Math.max(1, pts.length - 1)) * 300;
      const y = 76 - ((v - minL) / span) * 68;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');

  return (
    <div className="h-full overflow-y-auto px-4 py-4 space-y-4 max-w-2xl mx-auto">
      {/* Architecture Info + Live Loss Chart */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Cpu className="w-5 h-5 text-emerald-400" />
            <div>
              <div className="text-sm font-extrabold text-white">Своя нейросеть (Transformer GPT)</div>
              <div className="text-[11px] text-slate-400">
                {model.getParamCount().toLocaleString('ru-RU')} весов · {model.config.nLayer} слоя · {model.config.nHead} головы внимания · AdamW
              </div>
            </div>
          </div>
          <button
            type="button"
            onClick={handleDownloadCheckpoint}
            className="p-2 rounded-xl bg-white/5 hover:bg-white/10 text-slate-300 cursor-pointer"
            title="Скачать веса (.json)"
          >
            <Download className="w-4 h-4" />
          </button>
        </div>

        <div className="grid grid-cols-3 gap-2 text-center">
          <div className="p-2 rounded-xl bg-slate-950/70 border border-white/5">
            <div className="text-[10px] text-slate-400">Шаг (Epoch)</div>
            <div className="text-sm font-mono font-bold text-white">{stepCount}</div>
          </div>
          <div className="p-2 rounded-xl bg-slate-950/70 border border-white/5">
            <div className="text-[10px] text-slate-400">Ошибка (Loss)</div>
            <div className="text-sm font-mono font-bold text-emerald-300">{loss.toFixed(3)}</div>
          </div>
          <div className="p-2 rounded-xl bg-slate-950/70 border border-white/5">
            <div className="text-[10px] text-slate-400">Словарь</div>
            <div className="text-sm font-mono font-bold text-sky-300">{model.vocab.length} ток.</div>
          </div>
        </div>

        <div className="rounded-xl bg-slate-950 border border-white/10 p-2">
          <div className="flex items-center gap-1.5 text-[10px] text-slate-400 mb-1">
            <Activity className="w-3 h-3 text-emerald-400" /> График обучения (Loss вниз = умнее модель)
          </div>
          <svg viewBox="0 0 300 80" className="w-full h-20">
            <polyline
              fill="none"
              stroke="#34d399"
              strokeWidth="2.5"
              strokeLinejoin="round"
              strokeLinecap="round"
              points={polyPoints}
            />
          </svg>
        </div>
      </div>

      {/* Corpus & Training Controls */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-3">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <span className="text-xs font-bold text-white">Тексты для обучения своей модели</span>
          <div className="flex gap-1.5 flex-wrap">
            {TRAINER_DATASETS.map((ds) => (
              <button
                key={ds.id}
                type="button"
                onClick={() => setCorpus(ds.text)}
                className="px-2 py-1 rounded-lg bg-white/5 hover:bg-white/10 text-[10px] font-semibold text-sky-300 cursor-pointer"
              >
                {ds.name}
              </button>
            ))}
          </div>
        </div>

        <textarea
          rows={6}
          value={corpus}
          onChange={(e) => setCorpus(e.target.value)}
          className="w-full rounded-xl bg-slate-950 border border-white/15 p-3 text-xs font-mono text-slate-200 outline-none focus:border-emerald-400"
        />

        <div className="flex items-center justify-between gap-3 text-xs">
          <span className="text-slate-400">Learning Rate: <b className="text-white font-mono">{lr}</b></span>
          <input
            type="range"
            min={0.002}
            max={0.02}
            step={0.001}
            value={lr}
            onChange={(e) => setLr(Number(e.target.value))}
            className="w-40 accent-emerald-400"
          />
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={startTraining}
            className={`flex-1 py-2.5 rounded-xl font-bold text-xs flex items-center justify-center gap-1.5 cursor-pointer ${
              training
                ? 'bg-rose-500 text-white'
                : 'bg-emerald-400 hover:bg-emerald-300 text-slate-950'
            }`}
          >
            {training ? <Square className="w-3.5 h-3.5 fill-white" /> : <Play className="w-3.5 h-3.5 fill-slate-950" />}
            {training ? 'Остановить обучение' : 'Обучить нейросеть'}
          </button>
          <button
            type="button"
            onClick={handleResetFresh}
            className="px-3.5 py-2.5 rounded-xl bg-white/10 text-slate-200 text-xs font-bold flex items-center gap-1 cursor-pointer"
            title="Создать чистую модель с нуля"
          >
            <RotateCcw className="w-3.5 h-3.5" /> С нуля
          </button>
        </div>
      </div>

      {/* Live Inference Test */}
      <div className="rounded-2xl bg-slate-900/90 border border-white/10 p-4 space-y-2.5">
        <div className="text-xs font-bold text-white flex items-center gap-1.5">
          <Sparkles className="w-3.5 h-3.5 text-violet-400" /> Проверить свою обученную модель
        </div>
        <div className="flex gap-2">
          <input
            type="text"
            value={testPrompt}
            onChange={(e) => setTestPrompt(e.target.value)}
            className="flex-1 rounded-xl bg-slate-950 border border-white/15 px-3 py-2 text-xs text-white outline-none focus:border-violet-400"
          />
          <button
            type="button"
            onClick={handleGenerate}
            className="px-3.5 py-2 rounded-xl bg-violet-500 text-white font-bold text-xs cursor-pointer"
          >
            Сгенерировать
          </button>
        </div>

        <div className="flex items-center gap-2 text-[11px] text-slate-400">
          <span>Температура: {temperature}</span>
          <input
            type="range"
            min={0.15}
            max={1.1}
            step={0.05}
            value={temperature}
            onChange={(e) => setTemperature(Number(e.target.value))}
            className="w-28 accent-violet-400"
          />
        </div>

        {sampleOutput && (
          <div className="p-3 rounded-xl bg-slate-950 border border-violet-400/30 text-xs text-slate-100 whitespace-pre-wrap">
            {sampleOutput}
          </div>
        )}
      </div>
    </div>
  );
};
