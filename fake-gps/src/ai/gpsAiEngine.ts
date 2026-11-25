import { LatLng } from '../types/gps';
import { bearingBetweenDeg, distanceBetweenMeters, moveLatLng } from '../utils/nativeMockGps';

export interface AiAnalysis {
  realismScore: number; // 0..100
  statusLabel: string;
  tip: string;
  alertKind: 'ok' | 'caution';
}

/**
 * Computes corner-aware speed factor so movement slows naturally at sharp road turns
 * and varies organically like a real human / car instead of robotic constant speed.
 */
export function computeHumanizedSpeedKmh(
  baseSpeedKmh: number,
  roadPoints: LatLng[],
  segIdx: number,
  aiEnabled: boolean,
): number {
  if (!aiEnabled || roadPoints.length < 3) return baseSpeedKmh;

  const idx = Math.max(1, Math.min(segIdx, roadPoints.length - 2));
  const b1 = bearingBetweenDeg(roadPoints[idx - 1], roadPoints[idx]);
  const b2 = bearingBetweenDeg(roadPoints[idx], roadPoints[idx + 1]);
  let turnAngle = Math.abs(b2 - b1);
  if (turnAngle > 180) turnAngle = 360 - turnAngle;

  // Slow down into sharp turns (up to 45% reduction at 90-180 deg turns)
  const turnFactor = turnAngle > 65 ? 0.58 : turnAngle > 30 ? 0.78 : 1.0;
  // Subtle organic +/- 7% human pace wave
  const organicWave = 0.93 + Math.sin(Date.now() / 1700) * 0.07;

  return Math.max(2, Number((baseSpeedKmh * turnFactor * organicWave).toFixed(1)));
}

/**
 * Generates 4 waypoints around `center` for an instant AI circular walk/drive loop
 * (subsequently snapped to real roads via OSRM!).
 */
export function generateAiLoopWaypoints(center: LatLng, speedKmh: number): LatLng[] {
  const radiusMeters = speedKmh <= 8 ? 320 : speedKmh <= 25 ? 850 : 1800;
  const p1 = moveLatLng(center.lat, center.lng, radiusMeters, 35);
  const p2 = moveLatLng(center.lat, center.lng, radiusMeters * 1.15, 130);
  const p3 = moveLatLng(center.lat, center.lng, radiusMeters * 0.95, 240);
  return [center, p1, p2, p3, center];
}

/**
 * Evaluates teleport realism & anti-detect safety based on jump distance and config.
 */
export function analyzeSpoofRealism(
  prevPoint: LatLng | null,
  nextPoint: LatLng,
  jitterMeters: number,
  isSpoofing: boolean,
  aiHumanize: boolean,
): AiAnalysis {
  let score = 78;
  let tip = 'Дрейф координат и скорость выглядят естественно.';
  let alertKind: AiAnalysis['alertKind'] = 'ok';

  if (jitterMeters >= 0.8 && jitterMeters <= 4.5) score += 12;
  else if (jitterMeters === 0) score -= 12;

  if (aiHumanize) score += 10;

  if (prevPoint && isSpoofing) {
    const jumpKm = distanceBetweenMeters(prevPoint, nextPoint) / 1000;
    if (jumpKm > 30) {
      score = Math.max(32, score - 45);
      alertKind = 'caution';
      tip = `Резкий прыжок на ${Math.round(jumpKm)} км во время активного GPS — лучше остановить спуфинг перед телепортом.`;
    }
  }

  return {
    realismScore: Math.min(99, Math.max(20, score)),
    statusLabel: score >= 85 ? 'Макс. реализм' : score >= 65 ? 'Норма' : 'Риск детекта',
    tip,
    alertKind,
  };
}
