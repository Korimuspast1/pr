export interface LatLng {
  lat: number;
  lng: number;
}

export interface SavedPlace {
  id: string;
  name: string;
  subtitle?: string;
  lat: number;
  lng: number;
  isFavorite: boolean;
  lastUsedAt: number;
}

export interface MockConfig {
  accuracyMeters: number;
  altitudeMeters: number;
  jitterMeters: number;
  updateIntervalMs: number;
  speedKmh: number;
  loopRoute: boolean;
}

export type MapStyle = 'osm' | 'dark' | 'satellite';

export interface NativeMockStatus {
  isAndroid: boolean;
  hasLocationPermission: boolean;
  hasMockPermission: boolean;
  isSpoofing: boolean;
  isRoutePlaying: boolean;
  currentLat: number;
  currentLng: number;
  bearing: number;
  errorMessage: string;
}
