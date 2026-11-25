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

async function queryOsrmUrl(url: string, timeoutMs: number): Promise<RoadRouteResult | null> {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(url, { signal: controller.signal });
    window.clearTimeout(timer);
    if (!res.ok) return null;

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
    return null;
  } catch {
    window.clearTimeout(timer);
    return null;
  }
}

/**
 * Fetches real street-by-street geometry between waypoints using OSRM.
 * Uses dedicated OSM pedestrian/bike routers when walking/cycling and falls back
 * to driving OSRM or offline geometry if unreachable.
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

  const urls: string[] = [];
  if (profile === 'foot') {
    urls.push(`https://routing.openstreetmap.de/routed-foot/route/v1/driving/${coordStr}?overview=full&geometries=geojson`);
  } else if (profile === 'bike') {
    urls.push(`https://routing.openstreetmap.de/routed-bike/route/v1/driving/${coordStr}?overview=full&geometries=geojson`);
  }
  urls.push(`https://router.project-osrm.org/route/v1/driving/${coordStr}?overview=full&geometries=geojson`);

  for (const url of urls) {
    const result = await queryOsrmUrl(url, 5500);
    if (result) return result;
  }

  const dist = Math.round(totalDistance(waypoints));
  return {
    roadPoints: waypoints,
    distanceMeters: dist,
    durationSec: Math.round(dist / Math.max(1, (speedKmh * 1000) / 3600)),
    isRealRoad: false,
  };
}
