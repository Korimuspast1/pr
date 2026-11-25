import React, { useEffect, useRef, useState } from 'react';

interface JoystickPadProps {
  speedKmh: number;
  onChangeSpeed: (kmh: number) => void;
  onStep: (distanceMeters: number, bearingDeg: number) => void;
}

const SPEEDS = [5, 18, 50];

export const JoystickPad: React.FC<JoystickPadProps> = ({
  speedKmh,
  onChangeSpeed,
  onStep,
}) => {
  const padRef = useRef<HTMLDivElement | null>(null);
  const [dragging, setDragging] = useState(false);
  const [knob, setKnob] = useState({ x: 0, y: 0 });
  const onStepRef = useRef(onStep);
  onStepRef.current = onStep;

  const maxRadius = 32;

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
      const bearing = ((Math.atan2(knob.x, -knob.y) * 180) / Math.PI + 360) % 360;
      const speedMps = (speedKmh * 1000) / 3600;
      onStepRef.current(speedMps * 0.2 * Math.min(1, mag), bearing);
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
    <div className="rounded-3xl bg-slate-950/80 border border-white/15 p-2.5 shadow-2xl backdrop-blur-xl flex flex-col items-center gap-2 select-none">
      <div
        ref={padRef}
        onPointerDown={(e) => {
          e.preventDefault();
          setDragging(true);
          updateFromPointer(e.clientX, e.clientY);
        }}
        style={{ touchAction: 'none' }}
        className="relative w-24 h-24 rounded-full bg-white/5 border border-white/15 flex items-center justify-center cursor-grab active:cursor-grabbing"
      >
        <div
          className={`w-9 h-9 rounded-full shadow-lg border-2 border-white transition-colors ${
            dragging ? 'bg-emerald-400' : 'bg-sky-400'
          }`}
          style={{
            transform: `translate(${knob.x * maxRadius}px, ${knob.y * maxRadius}px)`,
          }}
        />
      </div>

      <div className="flex items-center gap-1">
        {SPEEDS.map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => onChangeSpeed(s)}
            className={`px-2 py-0.5 rounded-full text-[10px] font-bold cursor-pointer ${
              speedKmh === s
                ? 'bg-emerald-400 text-slate-950'
                : 'bg-white/10 text-slate-300 hover:bg-white/20'
            }`}
          >
            {s}
          </button>
        ))}
      </div>
    </div>
  );
};
