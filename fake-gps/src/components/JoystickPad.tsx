import React, { useEffect, useRef, useState } from 'react';
import { Navigation } from 'lucide-react';

interface JoystickPadProps {
  speedKmh: number;
  onChangeSpeed: (kmh: number) => void;
  onStep: (distanceMeters: number, bearingDeg: number) => void;
}

const SPEED_PRESETS = [
  { label: 'Пешком', kmh: 5 },
  { label: 'Вело', kmh: 18 },
  { label: 'Авто', kmh: 50 },
  { label: 'Турбо', kmh: 120 },
];

export const JoystickPad: React.FC<JoystickPadProps> = ({
  speedKmh,
  onChangeSpeed,
  onStep,
}) => {
  const padRef = useRef<HTMLDivElement | null>(null);
  const [dragging, setDragging] = useState(false);
  const [knob, setKnob] = useState({ x: 0, y: 0 }); // normalized -1..1
  const onStepRef = useRef(onStep);
  onStepRef.current = onStep;

  const maxRadius = 38;

  const updateFromPointer = (clientX: number, clientY: number) => {
    const el = padRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const cx = rect.left + rect.width / 2;
    const cy = rect.top + rect.height / 2;
    let dx = clientX - cx;
    let dy = clientY - cy;
    const dist = Math.hypot(dx, dy);
    if (dist > maxRadius) {
      dx = (dx / dist) * maxRadius;
      dy = (dy / dist) * maxRadius;
    }
    setKnob({ x: dx / maxRadius, y: dy / maxRadius });
  };

  useEffect(() => {
    if (!dragging) return;
    const interval = window.setInterval(() => {
      const mag = Math.hypot(knob.x, knob.y);
      if (mag < 0.12) return;
      // Bearing: 0 = North (-y), 90 = East (+x)
      const bearing = ((Math.atan2(knob.x, -knob.y) * 180) / Math.PI + 360) % 360;
      const speedMps = (speedKmh * 1000) / 3600;
      const stepMeters = speedMps * 0.2 * Math.min(1, mag);
      onStepRef.current(stepMeters, bearing);
    }, 200);
    return () => window.clearInterval(interval);
  }, [dragging, knob, speedKmh]);

  useEffect(() => {
    if (!dragging) return;
    const onMove = (e: PointerEvent) => updateFromPointer(e.clientX, e.clientY);
    const onUp = () => {
      setDragging(false);
      setKnob({ x: 0, y: 0 });
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
    window.addEventListener('pointercancel', onUp);
    return () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      window.removeEventListener('pointercancel', onUp);
    };
  }, [dragging]);

  return (
    <div className="rounded-2xl bg-slate-900/90 border border-slate-700/90 p-3 shadow-2xl backdrop-blur flex flex-col items-center gap-2.5 select-none">
      <div className="flex items-center gap-1.5 text-[11px] font-bold text-slate-300">
        <Navigation className="w-3.5 h-3.5 text-emerald-400" />
        <span>Джойстик · {speedKmh} км/ч</span>
      </div>

      {/* Circular Pad */}
      <div
        ref={padRef}
        onPointerDown={(e) => {
          e.preventDefault();
          setDragging(true);
          updateFromPointer(e.clientX, e.clientY);
        }}
        style={{ touchAction: 'none' }}
        className="relative w-28 h-28 rounded-full bg-slate-950/90 border-2 border-slate-700 flex items-center justify-center cursor-grab active:cursor-grabbing"
      >
        <span className="absolute top-1.5 text-[9px] font-black text-slate-500">N</span>
        <span className="absolute bottom-1.5 text-[9px] font-black text-slate-500">S</span>
        <span className="absolute left-2 text-[9px] font-black text-slate-500">W</span>
        <span className="absolute right-2 text-[9px] font-black text-slate-500">E</span>

        <div
          className={`w-11 h-11 rounded-full shadow-lg border-2 flex items-center justify-center transition-colors ${
            dragging
              ? 'bg-emerald-500 border-white'
              : 'bg-sky-500 border-sky-200'
          }`}
          style={{
            transform: `translate(${knob.x * maxRadius}px, ${knob.y * maxRadius}px)`,
          }}
        >
          <div className="w-3 h-3 rounded-full bg-white/80" />
        </div>
      </div>

      {/* Speed Chips */}
      <div className="grid grid-cols-2 gap-1 w-full">
        {SPEED_PRESETS.map((p) => (
          <button
            key={p.kmh}
            type="button"
            onClick={() => onChangeSpeed(p.kmh)}
            className={`px-2 py-1 rounded-lg text-[10px] font-bold cursor-pointer transition-colors ${
              speedKmh === p.kmh
                ? 'bg-emerald-500 text-slate-950'
                : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
            }`}
          >
            {p.label}
          </button>
        ))}
      </div>
    </div>
  );
};
