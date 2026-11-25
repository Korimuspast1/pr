import { PRESET_CITIES } from '../data/presets';
import { LatLng, SavedPlace } from '../types/gps';
import { bearingBetweenDeg, distanceBetweenMeters, moveLatLng } from '../utils/nativeMockGps';

export interface AiAnalysis {
  realismScore: number; // 0..100
  statusLabel: string;
  tip: string;
  alertKind: 'ok' | 'caution';
}

export interface AiRoutePlan {
  waypoints: LatLng[];
  summary: string;
  isLoop: boolean;
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
  const turnFactor = turnAngle > 65 ? 0.56 : turnAngle > 30 ? 0.78 : 1.0;
  // Subtle organic +/- 7% human pace wave
  const organicWave = 0.93 + Math.sin(Date.now() / 1700) * 0.07;

  return Math.max(2, Number((baseSpeedKmh * turnFactor * organicWave).toFixed(1)));
}

/**
 * Generates varied AI waypoints around `center` for a circular walk/ride/drive loop.
 * Uses a rotating seed so each tap creates a fresh route shape, ready for road-snapping.
 */
export function generateAiLoopWaypoints(
  center: LatLng,
  speedKmh: number,
  seedOffset = 0,
  targetTotalKm?: number,
): LatLng[] {
  const defaultRadiusM = speedKmh <= 8 ? 320 : speedKmh <= 25 ? 850 : 1900;
  const radiusM = targetTotalKm
    ? Math.max(180, Math.min(18000, (targetTotalKm * 1000) / (2 * Math.PI)))
    : defaultRadiusM;

  const baseAngle = ((seedOffset * 67) % 360);
  const p1 = moveLatLng(center.lat, center.lng, radiusM * 0.95, baseAngle + 28);
  const p2 = moveLatLng(center.lat, center.lng, radiusM * 1.18, baseAngle + 115);
  const p3 = moveLatLng(center.lat, center.lng, radiusM * 1.05, baseAngle + 205);
  const p4 = moveLatLng(center.lat, center.lng, radiusM * 0.88, baseAngle + 285);
  return [center, p1, p2, p3, p4, center];
}

/**
 * Local AI Street-Grid Synthesizer:
 * Turns coarse waypoints into realistic multi-turn city-block street paths with chamfered
 * intersections and subtle road curvature when offline (never draws a raw straight stick).
 */
export function synthesizeAiStreetPath(waypoints: LatLng[]): LatLng[] {
  if (waypoints.length < 2) return waypoints;

  const result: LatLng[] = [waypoints[0]];

  for (let i = 0; i < waypoints.length - 1; i++) {
    const a = waypoints[i];
    const b = waypoints[i + 1];
    const dist = distanceBetweenMeters(a, b);
    const br = bearingBetweenDeg(a, b);

    if (dist < 140) {
      result.push(b);
      continue;
    }

    // Divide segment into realistic urban block steps (~160–320m each)
    const blockCount = Math.min(10, Math.max(2, Math.round(dist / 240)));
    const stepM = dist / blockCount;
    // Alternating lateral block offset creates realistic street turns instead of a straight line
    const lateralAmp = Math.min(110, Math.max(28, stepM * 0.24));
    const perpRight = (br + 90) % 360;
    const perpLeft = (br + 270) % 360;

    let prev = a;
    for (let k = 1; k < blockCount; k++) {
      const basePt = moveLatLng(a.lat, a.lng, stepM * k, br);
      const sideAzimuth = (i + k) % 2 === 0 ? perpRight : perpLeft;
      const offsetM = lateralAmp * (0.65 + ((k * 37) % 40) / 100);
      const cornerPt = moveLatLng(basePt.lat, basePt.lng, offsetM, sideAzimuth);

      // Chamfer the street turn (two points near the corner for smooth realistic bend)
      const segDist = distanceBetweenMeters(prev, cornerPt);
      const approachAz = bearingBetweenDeg(prev, cornerPt);
      if (segDist > 50) {
        const preTurn = moveLatLng(prev.lat, prev.lng, segDist * 0.82, approachAz);
        result.push(preTurn);
      }
      result.push(cornerPt);
      prev = cornerPt;
    }

    result.push(b);
  }

  return result;
}

/**
 * Parses a natural-language Russian/English route instruction from the user and lets
 * our embedded AI compose a tailored route plan (zero external API keys required).
 */
export async function composeAiRoutePlan(
  origin: LatLng,
  rawPrompt: string,
  speedKmh: number,
  places: SavedPlace[],
  seed: number,
): Promise<AiRoutePlan> {
  const prompt = rawPrompt.trim();
  const lower = prompt.toLowerCase();

  // 1. Direct coordinates ("52.24, 21.02")
  const coordMatch = prompt.match(/^(-?\d+(?:\.\d+)?)\s*[,\s]\s*(-?\d+(?:\.\d+)?)$/);
  if (coordMatch) {
    const lat = Number.parseFloat(coordMatch[1]);
    const lng = Number.parseFloat(coordMatch[2]);
    if (lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180) {
      const dest = { lat: Number(lat.toFixed(6)), lng: Number(lng.toFixed(6)) };
      return {
        waypoints: [origin, dest],
        summary: `ИИ-маршрут к ${lat.toFixed(4)}, ${lng.toFixed(4)}`,
        isLoop: false,
      };
    }
  }

  // 2. Distance request ("3 км", "1500 м", "пробежка 5 км")
  const kmMatch = lower.match(/(\d+(?:[.,]\d+)?)\s*(?:км|km)/);
  const mMatch = lower.match(/(\d+)\s*(?:м|m)(?!\w)/);
  if (kmMatch || mMatch) {
    const targetKm = kmMatch
      ? Number.parseFloat(kmMatch[1].replace(',', '.'))
      : Number.parseInt(mMatch![1], 10) / 1000;
    const clampedKm = Math.max(0.4, Math.min(40, targetKm));
    return {
      waypoints: generateAiLoopWaypoints(origin, speedKmh, seed, clampedKm),
      summary: `ИИ-круг ~${clampedKm.toFixed(1)} км по дорогам`,
      isLoop: true,
    };
  }

  // 3. Directional / figure-8 / patrol prompts
  const dirMap: Array<[RegExp, number, string]> = [
    [/(север|north)/, 0, 'на север'],
    [/(восток|east)/, 90, 'на восток'],
    [/(юг|south)/, 180, 'на юг'],
    [/(запад|west)/, 270, 'на запад'],
  ];
  for (const [rx, deg, label] of dirMap) {
    if (rx.test(lower)) {
      const legM = speedKmh <= 8 ? 800 : speedKmh <= 25 ? 2000 : 4500;
      const mid = moveLatLng(origin.lat, origin.lng, legM * 0.55, (deg + 22) % 360);
      const dest = moveLatLng(origin.lat, origin.lng, legM, deg);
      return {
        waypoints: [origin, mid, dest],
        summary: `ИИ-маршрут ${label}`,
        isLoop: false,
      };
    }
  }

  if (/(восьмерк|figure|зигзаг|патрул)/.test(lower)) {
    const r = speedKmh <= 8 ? 350 : speedKmh <= 25 ? 900 : 1800;
    const a1 = moveLatLng(origin.lat, origin.lng, r, 40);
    const a2 = moveLatLng(origin.lat, origin.lng, r * 1.1, 130);
    const b1 = moveLatLng(origin.lat, origin.lng, r, 220);
    const b2 = moveLatLng(origin.lat, origin.lng, r * 1.1, 310);
    return {
      waypoints: [origin, a1, a2, origin, b1, b2, origin],
      summary: 'ИИ-патруль «восьмёрка» по кварталам',
      isLoop: true,
    };
  }

  // 4. Destination lookup (Saved places -> Offline cities -> Keyless OSM Nominatim)
  if (prompt.length >= 2) {
    const saved = places.find((p) => p.name.toLowerCase().includes(lower));
    if (saved) {
      return {
        waypoints: [origin, { lat: saved.lat, lng: saved.lng }],
        summary: `ИИ-маршрут до «${saved.name}»`,
        isLoop: false,
      };
    }

    const preset = PRESET_CITIES.find(
      (c) => c.name.toLowerCase().includes(lower) || c.country.toLowerCase().includes(lower),
    );
    if (preset) {
      return {
        waypoints: [origin, { lat: preset.lat, lng: preset.lng }],
        summary: `ИИ-маршрут до ${preset.name}`,
        isLoop: false,
      };
    }

    // Try keyless OpenStreetMap Nominatim biased near current pin
    try {
      const viewbox = `${(origin.lng - 0.35).toFixed(4)},${(origin.lat + 0.35).toFixed(4)},${(origin.lng + 0.35).toFixed(4)},${(origin.lat - 0.35).toFixed(4)}`;
      const res = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&limit=1&viewbox=${viewbox}&q=${encodeURIComponent(prompt)}`,
        { headers: { 'Accept-Language': 'ru,en' } },
      );
      if (res.ok) {
        const data = (await res.json()) as Array<{ display_name: string; lat: string; lon: string }>;
        if (data.length > 0) {
          const dest = {
            lat: Number(Number.parseFloat(data[0].lat).toFixed(6)),
            lng: Number(Number.parseFloat(data[0].lon).toFixed(6)),
          };
          const shortName = data[0].display_name.split(',')[0];
          return {
            waypoints: [origin, dest],
            summary: `ИИ-маршрут до «${shortName}»`,
            isLoop: false,
          };
        }
      }
    } catch {
      // Fall back to smart local loop
    }
  }

  // 5. Default smart AI loop tailored to speed mode
  const modeName = speedKmh <= 8 ? 'Прогулка' : speedKmh <= 25 ? 'Вело-круг' : 'Авто-круг';
  return {
    waypoints: generateAiLoopWaypoints(origin, speedKmh, seed),
    summary: `ИИ: ${modeName.toLowerCase()} по дорогам`,
    isLoop: true,
  };
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
