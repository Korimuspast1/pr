import { LatLng } from '../types/gps';
import { distanceBetweenMeters } from './nativeMockGps';

export interface RoadRouteResult {
  roadPoints: LatLng[];
  distanceMeters: number;
  durationSec: number;
  isRealRoad: boolean;
}

function getOsrmProfile(speedKmh: number): 'foot' | 'bike' | 'driving' {
  if (speedKmh <= 8) return 'foot';
  if (speedKmh <= 25) return 'bike';
  return 'driving';
}

function totalDistance(pts: LatLng[]): number {
  let d = 0;
  for (let i = 0; i < pts.length - 1; i++) {
    d += distanceBetweenMeters(pts[i], pts[i + 1]);
  }
  return d;
}

/**
 * Fetches real street-by-street geometry between waypoints using OSRM.
 * Automatically falls back to interpolated waypoints if offline.
 */
export async function fetchRoadRoute(
  waypoints: LatLng[],
  speedKmh: number,
): Promise<RoadRouteResult> {
  if (waypoints.length < 2) {
    return {
      roadPoints: waypoints,
      distanceMeters: 0,
      durationSec: 0,
      isRealRoad: false,
    };
  }

  const profile = getOsrmProfile(speedKmh);
  const coordStr = waypoints.map((p) => `${p.lng.toFixed(6)},${p.lat.toFixed(6)}`).join(';');
  const url = `https://router.project-osrm.org/route/v1/${profile}/${coordStr}?overview=full&geometries=geojson`;

  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), 6500);

  try {
    const res = await fetch(url, { signal: controller.signal });
    window.clearTimeout(timer);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);

    const data = (await res.json()) as {
      code?: string;
      routes?: Array<{
        distance: number;
        duration: number;
        geometry?: { coordinates?: Array<[number, number]> };
      }>;
    };

    const route = data.routes?.[0];
    const coords = route?.geometry?.coordinates;
    if (data.code === 'Ok' && coords && coords.length >= 2) {
      const roadPoints: LatLng[] = coords.map(([lng, lat]) => ({
        lat: Number(lat.toFixed(6)),
        lng: Number(lng.toFixed(6)),
      }));
      return {
        roadPoints,
        distanceMeters: Math.round(route.distance),
        durationSec: Math.round(route.duration),
        isRealRoad: true,
      };
    }
  } catch {
    window.clearTimeout(timer);
  }

  // Offline fallback: subdivide segments cleanly
  const dist = Math.round(totalDistance(waypoints));
  return {
    roadPoints: waypoints,
    distanceMeters: dist,
    durationSec: Math.round(dist / Math.max(1, (speedKmh * 1000) / 3600)),
    isRealRoad: false,
  };
}
