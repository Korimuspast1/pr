interface AndroidTtsBridge {
  speak: (text: string, rate: number) => boolean;
  stop: () => void;
  isReady: () => boolean;
  isPolishSupported: () => boolean;
  openTtsInstall: () => void;
}

declare global {
  interface Window {
    PolskiAndroidTTS?: AndroidTtsBridge;
  }
}

let audioCtx: AudioContext | null = null;

function getAudioContext(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  const Ctx =
    window.AudioContext ||
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  if (!Ctx) return null;
  if (!audioCtx) {
    audioCtx = new Ctx();
  }
  if (audioCtx.state === 'suspended') {
    audioCtx.resume().catch(() => {});
  }
  return audioCtx;
}

export function playUiSound(kind: 'correct' | 'wrong' | 'tap' | 'complete', enabled: boolean): void {
  if (!enabled) return;
  const ctx = getAudioContext();
  if (!ctx) return;

  const now = ctx.currentTime;

  const playTone = (
    freq: number,
    start: number,
    dur: number,
    type: OscillatorType = 'sine',
    vol = 0.09,
  ) => {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, start);
    gain.gain.setValueAtTime(0.001, start);
    gain.gain.exponentialRampToValueAtTime(vol, start + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.001, start + dur);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(start);
    osc.stop(start + dur + 0.02);
  };

  if (kind === 'tap') {
    playTone(520, now, 0.06, 'triangle', 0.05);
  } else if (kind === 'correct') {
    playTone(587.33, now, 0.14, 'triangle', 0.1);
    playTone(880.0, now + 0.09, 0.24, 'triangle', 0.11);
  } else if (kind === 'wrong') {
    playTone(230, now, 0.16, 'sawtooth', 0.06);
    playTone(185, now + 0.12, 0.22, 'sawtooth', 0.06);
  } else if (kind === 'complete') {
    const notes = [523.25, 659.25, 783.99, 1046.5];
    notes.forEach((n, idx) => playTone(n, now + idx * 0.09, 0.28, 'triangle', 0.1));
  }
}

function playSyllableFallbackChime(text: string): void {
  const ctx = getAudioContext();
  if (!ctx) return;
  const words = text.trim().split(/\s+/).slice(0, 5);
  const now = ctx.currentTime;
  words.forEach((w, idx) => {
    const freq = 440 + ((w.charCodeAt(0) % 7) * 38);
    const start = now + idx * 0.16;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(freq, start);
    gain.gain.setValueAtTime(0.001, start);
    gain.gain.exponentialRampToValueAtTime(0.07, start + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.001, start + 0.13);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(start);
    osc.stop(start + 0.15);
  });
}

export type SpeakStatus = 'android_tts' | 'android_needs_pl_pack' | 'web_speech' | 'fallback_chime';

export function speakPolish(text: string, slow = false): SpeakStatus {
  if (typeof window === 'undefined' || !text.trim()) return 'fallback_chime';

  // 1. Native Android APK TextToSpeech Bridge
  const androidTts = window.PolskiAndroidTTS;
  if (androidTts) {
    try {
      const rate = slow ? 0.65 : 0.92;
      const spoke = androidTts.speak(text, rate);
      if (spoke) {
        return androidTts.isPolishSupported() ? 'android_tts' : 'android_needs_pl_pack';
      }
    } catch {
      // fall through to web speech
    }
  }

  // 2. Browser Web Speech API
  if ('speechSynthesis' in window) {
    try {
      window.speechSynthesis.cancel();
      const utter = new SpeechSynthesisUtterance(text);
      utter.lang = 'pl-PL';
      utter.rate = slow ? 0.65 : 0.92;
      const voices = window.speechSynthesis.getVoices();
      const plVoice = voices.find((v) => v.lang.toLowerCase().startsWith('pl'));
      if (plVoice) {
        utter.voice = plVoice;
      }
      window.speechSynthesis.speak(utter);
      return 'web_speech';
    } catch {
      // fall through to chime
    }
  }

  // 3. Fallback chime so tapping always gives audible feedback
  playSyllableFallbackChime(text);
  return 'fallback_chime';
}

export function canInstallAndroidPolishTts(): boolean {
  return typeof window !== 'undefined' && Boolean(window.PolskiAndroidTTS);
}

export function openAndroidTtsInstaller(): void {
  if (typeof window !== 'undefined' && window.PolskiAndroidTTS) {
    window.PolskiAndroidTTS.openTtsInstall();
  }
}
