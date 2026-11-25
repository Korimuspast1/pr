interface AndroidSttBridge {
  isAvailable: () => boolean;
  isListening: () => boolean;
  startListening: () => void;
  stopListening: () => void;
  cancelListening: () => void;
}

export type SttNativeEvent =
  | { type: 'ready' }
  | { type: 'speech_start' }
  | { type: 'speech_end' }
  | { type: 'rms'; value: number }
  | { type: 'partial'; text: string }
  | { type: 'result'; hypotheses: string[]; confidences: number[] }
  | { type: 'error'; code: string; message: string };

declare global {
  interface Window {
    PolskiAndroidSTT?: AndroidSttBridge;
    __polskiSttEvent?: (ev: SttNativeEvent) => void;
  }
}

export interface SpeechSessionCallbacks {
  onReady?: () => void;
  onRms?: (level01: number) => void;
  onPartial?: (text: string) => void;
  onResult: (hypotheses: string[], confidences: number[]) => void;
  onError: (code: string, message: string) => void;
}

interface BrowserSpeechRecognitionAlternative {
  transcript: string;
  confidence: number;
}

interface BrowserSpeechRecognitionResult {
  isFinal: boolean;
  length: number;
  [index: number]: BrowserSpeechRecognitionAlternative;
}

interface BrowserSpeechRecognitionEvent {
  results: {
    length: number;
    [index: number]: BrowserSpeechRecognitionResult;
  };
}

interface BrowserSpeechRecognition {
  lang: string;
  interimResults: boolean;
  maxAlternatives: number;
  continuous: boolean;
  onstart: (() => void) | null;
  onresult: ((ev: BrowserSpeechRecognitionEvent) => void) | null;
  onerror: ((ev: { error: string }) => void) | null;
  onend: (() => void) | null;
  start: () => void;
  stop: () => void;
  abort: () => void;
}

let activeWebRecognition: BrowserSpeechRecognition | null = null;
let rmsPulseTimer: number | null = null;

export function getSpeechRecognitionSupport(): 'android' | 'web' | 'none' {
  if (typeof window === 'undefined') return 'none';
  if (window.PolskiAndroidSTT) {
    try {
      return window.PolskiAndroidSTT.isAvailable() ? 'android' : 'none';
    } catch {
      return 'android';
    }
  }
  const w = window as unknown as {
    SpeechRecognition?: new () => BrowserSpeechRecognition;
    webkitSpeechRecognition?: new () => BrowserSpeechRecognition;
  };
  if (w.SpeechRecognition || w.webkitSpeechRecognition) {
    return 'web';
  }
  return 'none';
}

export function stopPolishListening(): void {
  if (rmsPulseTimer !== null) {
    window.clearInterval(rmsPulseTimer);
    rmsPulseTimer = null;
  }
  if (typeof window !== 'undefined' && window.PolskiAndroidSTT) {
    try {
      window.PolskiAndroidSTT.stopListening();
    } catch {
      // ignore
    }
  }
  if (activeWebRecognition) {
    try {
      activeWebRecognition.stop();
    } catch {
      // ignore
    }
    activeWebRecognition = null;
  }
}

export function cancelPolishListening(): void {
  if (rmsPulseTimer !== null) {
    window.clearInterval(rmsPulseTimer);
    rmsPulseTimer = null;
  }
  if (typeof window !== 'undefined') {
    window.__polskiSttEvent = undefined;
    if (window.PolskiAndroidSTT) {
      try {
        window.PolskiAndroidSTT.cancelListening();
      } catch {
        // ignore
      }
    }
  }
  if (activeWebRecognition) {
    try {
      activeWebRecognition.abort();
    } catch {
      // ignore
    }
    activeWebRecognition = null;
  }
}

export function startPolishListening(callbacks: SpeechSessionCallbacks): void {
  cancelPolishListening();

  // 1. Native Android APK SpeechRecognizer Bridge
  if (typeof window !== 'undefined' && window.PolskiAndroidSTT) {
    window.__polskiSttEvent = (ev: SttNativeEvent) => {
      if (ev.type === 'ready') {
        callbacks.onReady?.();
      } else if (ev.type === 'rms') {
        // Android RMS dB ranges roughly from -2 to +10
        const norm = Math.max(0.08, Math.min(1, (ev.value + 2) / 12));
        callbacks.onRms?.(norm);
      } else if (ev.type === 'partial') {
        callbacks.onPartial?.(ev.text);
      } else if (ev.type === 'result') {
        window.__polskiSttEvent = undefined;
        callbacks.onResult(ev.hypotheses ?? [], ev.confidences ?? []);
      } else if (ev.type === 'error') {
        window.__polskiSttEvent = undefined;
        callbacks.onError(ev.code, ev.message);
      }
    };

    try {
      window.PolskiAndroidSTT.startListening();
    } catch {
      window.__polskiSttEvent = undefined;
      callbacks.onError('exception', 'Не удалось запустить распознавание речи Android.');
    }
    return;
  }

  // 2. Browser Web Speech API (Chrome / Edge / Safari)
  const w = window as unknown as {
    SpeechRecognition?: new () => BrowserSpeechRecognition;
    webkitSpeechRecognition?: new () => BrowserSpeechRecognition;
  };
  const RecCtor = w.SpeechRecognition || w.webkitSpeechRecognition;

  if (!RecCtor) {
    callbacks.onError(
      'unavailable',
      'Распознавание речи не поддерживается в этом браузере. Используйте Android APK или Chrome.',
    );
    return;
  }

  try {
    const rec = new RecCtor();
    activeWebRecognition = rec;
    rec.lang = 'pl-PL';
    rec.interimResults = true;
    rec.maxAlternatives = 5;
    rec.continuous = false;

    let gotFinal = false;

    rec.onstart = () => {
      callbacks.onReady?.();
      rmsPulseTimer = window.setInterval(() => {
        callbacks.onRms?.(0.25 + Math.random() * 0.65);
      }, 140);
    };

    rec.onresult = (ev: BrowserSpeechRecognitionEvent) => {
      const lastIdx = ev.results.length - 1;
      if (lastIdx < 0) return;
      const result = ev.results[lastIdx];
      if (!result.isFinal) {
        callbacks.onPartial?.(result[0]?.transcript ?? '');
        return;
      }
      gotFinal = true;
      if (rmsPulseTimer !== null) {
        window.clearInterval(rmsPulseTimer);
        rmsPulseTimer = null;
      }
      const hypotheses: string[] = [];
      const confidences: number[] = [];
      for (let i = 0; i < result.length; i++) {
        const alt = result[i];
        if (alt?.transcript) {
          hypotheses.push(alt.transcript);
          confidences.push(alt.confidence || Math.max(0.55, 0.9 - i * 0.08));
        }
      }
      activeWebRecognition = null;
      callbacks.onResult(hypotheses, confidences);
    };

    rec.onerror = (ev: { error: string }) => {
      if (rmsPulseTimer !== null) {
        window.clearInterval(rmsPulseTimer);
        rmsPulseTimer = null;
      }
      activeWebRecognition = null;
      const map: Record<string, string> = {
        'not-allowed': 'Нет доступа к микрофону. Разрешите микрофон в настройках браузера.',
        'no-speech': 'Микрофон не услышал речь. Попробуйте сказать громче.',
        network: 'Для распознавания речи нужен интернет.',
        aborted: 'Запись остановлена.',
      };
      callbacks.onError(ev.error, map[ev.error] ?? 'Ошибка микрофона. Попробуйте ещё раз.');
    };

    rec.onend = () => {
      if (rmsPulseTimer !== null) {
        window.clearInterval(rmsPulseTimer);
        rmsPulseTimer = null;
      }
      if (!gotFinal && activeWebRecognition === rec) {
        activeWebRecognition = null;
        callbacks.onError('no_match', 'Речь не распознана. Нажмите на микрофон и произнесите фразу.');
      }
    };

    rec.start();
  } catch {
    callbacks.onError('exception', 'Не удалось запустить микрофон.');
  }
}
