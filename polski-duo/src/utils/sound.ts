let audioCtx: AudioContext | null = null;

function getAudioContext(): AudioContext | null {
  if (typeof window === 'undefined') return null;
  const Ctx = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
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

  const playTone = (freq: number, start: number, dur: number, type: OscillatorType = 'sine', vol = 0.09) => {
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
    playTone(587.33, now, 0.14, 'triangle', 0.1); // D5
    playTone(880.0, now + 0.09, 0.24, 'triangle', 0.11); // A5
  } else if (kind === 'wrong') {
    playTone(230, now, 0.16, 'sawtooth', 0.06);
    playTone(185, now + 0.12, 0.22, 'sawtooth', 0.06);
  } else if (kind === 'complete') {
    const notes = [523.25, 659.25, 783.99, 1046.5];
    notes.forEach((n, idx) => playTone(n, now + idx * 0.09, 0.28, 'triangle', 0.1));
  }
}

export function speakPolish(text: string): void {
  if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
  try {
    window.speechSynthesis.cancel();
    const utter = new SpeechSynthesisUtterance(text);
    utter.lang = 'pl-PL';
    utter.rate = 0.92;
    const voices = window.speechSynthesis.getVoices();
    const plVoice = voices.find((v) => v.lang.toLowerCase().startsWith('pl'));
    if (plVoice) utter.voice = plVoice;
    window.speechSynthesis.speak(utter);
  } catch {
    // ignore speech synthesis restrictions
  }
}
