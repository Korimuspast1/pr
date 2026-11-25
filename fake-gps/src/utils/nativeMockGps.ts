import { LatLng, MockConfig, NativeMockStatus } from '../types/gps';

interface FakeGpsAndroidBridge {
  startSpoofing: (
    lat: number,
    lng: number,
    accuracyMeters: number,
    altitudeMeters: number,
    jitterMeters: number,
    intervalMs: number,
  ) => boolean;
  stopSpoofing: () => void;
  updateLocation: (lat: number, lng: number, speedKmh: number, bearing: number) => void;
  startRoute: (pointsJson: string, speedKmh: number, loop: boolean) => boolean;
  stopRoute: () => void;
  getStatusJson: () => string;
  requestLocationPermission: () => void;
  openDeveloperSettings: () => void;
  openExternalUrl: (url: string) => void;
}

declare global {
  interface Window {
    FakeGpsAndroid?: FakeGpsAndroidBridge;
  }
}

export function isAndroidNative(): boolean {
  return typeof window !== 'undefined' && Boolean(window.FakeGpsAndroid);
}

export function getNativeMockStatus(fallbackLat: number, fallbackLng: number): NativeMockStatus {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    try {
      const raw = window.FakeGpsAndroid.getStatusJson();
      const parsed = JSON.parse(raw) as Partial<NativeMockStatus>;
      return {
        isAndroid: true,
        hasLocationPermission: Boolean(parsed.hasLocationPermission),
        hasMockPermission: Boolean(parsed.hasMockPermission),
        isSpoofing: Boolean(parsed.isSpoofing),
        isRoutePlaying: Boolean(parsed.isRoutePlaying),
        currentLat: parsed.currentLat ?? fallbackLat,
        currentLng: parsed.currentLng ?? fallbackLng,
        bearing: parsed.bearing ?? 0,
        errorMessage: parsed.errorMessage ?? '',
      };
    } catch {
      // fall through
    }
  }
  return {
    isAndroid: false,
    hasLocationPermission: true,
    hasMockPermission: true,
    isSpoofing: false,
    isRoutePlaying: false,
    currentLat: fallbackLat,
    currentLng: fallbackLng,
    bearing: 0,
    errorMessage: '',
  };
}

export function nativeStartSpoofing(lat: number, lng: number, config: MockConfig): boolean {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    return window.FakeGpsAndroid.startSpoofing(
      lat,
      lng,
      config.accuracyMeters,
      config.altitudeMeters,
      config.jitterMeters,
      config.updateIntervalMs,
    );
  }
  return true;
}

export function nativeStopSpoofing(): void {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    window.FakeGpsAndroid.stopSpoofing();
  }
}

export function nativeUpdateLocation(
  lat: number,
  lng: number,
  speedKmh: number,
  bearing: number,
): void {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    window.FakeGpsAndroid.updateLocation(lat, lng, speedKmh, bearing);
  }
}

export function nativeStartRoute(points: LatLng[], speedKmh: number, loop: boolean): boolean {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    return window.FakeGpsAndroid.startRoute(JSON.stringify(points), speedKmh, loop);
  }
  return true;
}

export function nativeStopRoute(): void {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    window.FakeGpsAndroid.stopRoute();
  }
}

export function nativeRequestLocationPermission(): void {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    window.FakeGpsAndroid.requestLocationPermission();
  }
}

export function nativeOpenDeveloperSettings(): void {
  if (typeof window !== 'undefined' && window.FakeGpsAndroid) {
    window.FakeGpsAndroid.openDeveloperSettings();
  }
}

/**
 * Computes new lat/lng after moving `distanceMeters` along `bearingDeg` (0 = North, 90 = East).
 */
export function moveLatLng(
  lat: number,
  lng: number,
  distanceMeters: number,
  bearingDeg: number,
): LatLng {
  const R = 6378137;
  const br = (bearingDeg * Math.PI) / 180;
  const dLat = (distanceMeters * Math.cos(br)) / R;
  const dLng = (distanceMeters * Math.sin(br)) / (R * Math.cos((lat * Math.PI) / 180));
  return {
    lat: Number((lat + (dLat * 180) / Math.PI).toFixed(6)),
    lng: Number((lng + (dLng * 180) / Math.PI).toFixed(6)),
  };
}

export function distanceBetweenMeters(a: LatLng, b: LatLng): number {
  const R = 6371000;
  const dLat = ((b.lat - a.lat) * Math.PI) / 180;
  const dLng = ((b.lng - a.lng) * Math.PI) / 180;
  const s1 = Math.sin(dLat / 2);
  const s2 = Math.sin(dLng / 2);
  const h =
    s1 * s1 +
    Math.cos((a.lat * Math.PI) / 180) * Math.cos((b.lat * Math.PI) / 180) * s2 * s2;
  return 2 * R * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
}

export function bearingBetweenDeg(a: LatLng, b: LatLng): number {
  const lat1 = (a.lat * Math.PI) / 180;
  const lat2 = (b.lat * Math.PI) / 180;
  const dLng = ((b.lng - a.lng) * Math.PI) / 180;
  const y = Math.sin(dLng) * Math.cos(lat2);
  const x =
    Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(dLng);
  return ((Math.atan2(y, x) * 180) / Math.PI + 360) % 360;
}
