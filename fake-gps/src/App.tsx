import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Bookmark,
  Compass,
  Gamepad2,
  MapPin,
  Play,
  Plus,
  Route,
  Search,
  Settings,
  Square,
  Star,
  Trash2,
  Wrench,
} from 'lucide-react';
import { JoystickPad } from './components/JoystickPad';
import { MapCanvas } from './components/MapCanvas';
import { SetupGuideModal } from './components/SetupGuideModal';
import { PRESET_CITIES } from './data/presets';
import { LatLng, MapStyle, MockConfig, NativeMockStatus, SavedPlace } from './types/gps';
import {
  bearingBetweenDeg,
  distanceBetweenMeters,
  getNativeMockStatus,
  isAndroidNative,
  moveLatLng,
  nativeStartRoute,
  nativeStartSpoofing,
  nativeStopRoute,
  nativeStopSpoofing,
  nativeUpdateLocation,
} from './utils/nativeMockGps';

const STORAGE_KEY = 'fake_gps_pro_state_v1';

type BottomTab = 'radar' | 'route' | 'places' | 'settings';

interface PersistedState {
  target: LatLng;
  config: MockConfig;
  places: SavedPlace[];
  mapStyle: MapStyle;
}

const DEFAULT_PERSISTED: PersistedState = {
  target: { lat: 52.2297, lng: 21.0122 }, // Warsaw center by default, pick any spot on map
  config: {
    accuracyMeters: 4,
    altitudeMeters: 112,
    jitterMeters: 1.5,
    updateIntervalMs: 600,
    speedKmh: 18,
    loopRoute: true,
  },
  places: [
    {
      id: 'p_waw',
      name: 'Варшава · Дворец культуры',
      lat: 52.2319,
      lng: 21.0067,
      isFavorite: true,
      lastUsedAt: Date.now(),
    },
    {
      id: 'p_krk',
      name: 'Краков · Главный рынок',
      lat: 50.0619,
      lng: 19.9369,
      isFavorite: true,
      lastUsedAt: Date.now() - 10000,
    },
  ],
  mapStyle: 'osm',
};

function loadState(): PersistedState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT_PERSISTED;
    const parsed = JSON.parse(raw) as Partial<PersistedState>;
    return {
      target: parsed.target ?? DEFAULT_PERSISTED.target,
      config: { ...DEFAULT_PERSISTED.config, ...(parsed.config ?? {}) },
      places: parsed.places?.length ? parsed.places : DEFAULT_PERSISTED.places,
      mapStyle: parsed.mapStyle ?? 'osm',
    };
  } catch {
    return DEFAULT_PERSISTED;
  }
}

export const App: React.FC = () => {
  const initial = useMemo(() => loadState(), []);

  const [target, setTarget] = useState<LatLng>(initial.target);
  const [config, setConfig] = useState<MockConfig>(initial.config);
  const [places, setPlaces] = useState<SavedPlace[]>(initial.places);
  const [mapStyle, setMapStyle] = useState<MapStyle>(initial.mapStyle);

  const [activeTab, setActiveTab] = useState<BottomTab>('radar');
  const [isSpoofing, setIsSpoofing] = useState(false);
  const [showJoystick, setShowJoystick] = useState(true);
  const [followTarget, setFollowTarget] = useState(false);
  const [bearing, setBearing] = useState(0);

  // Route Mode state
  const [routePoints, setRoutePoints] = useState<LatLng[]>([]);
  const [routePlaying, setRoutePlaying] = useState(false);
  const routeSegmentRef = useRef({ segIdx: 0, progressMeters: 0 });

  // Search state
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<Array<{ name: string; sub: string; lat: number; lng: number }>>([]);
  const [searchingOnline, setSearchingOnline] = useState(false);
  const [showSearchDropdown, setShowSearchDropdown] = useState(false);

  // Save favorite modal/input
  const [saveName, setSaveName] = useState('');

  // Android Mock Status & Setup modal
  const [nativeStatus, setNativeStatus] = useState<NativeMockStatus>(() =>
    getNativeMockStatus(initial.target.lat, initial.target.lng),
  );
  const [setupOpen, setSetupOpen] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToast(msg);
    setTimeout(() => setToast(null), 3200);
  };

  useEffect(() => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ target, config, places, mapStyle }),
    );
  }, [target, config, places, mapStyle]);

  const refreshNativeStatus = () => {
    const st = getNativeMockStatus(target.lat, target.lng);
    setNativeStatus(st);
    if (st.isAndroid) {
      setIsSpoofing(st.isSpoofing);
      setRoutePlaying(st.isRoutePlaying);
      if (st.isSpoofing) {
        setTarget({ lat: Number(st.currentLat.toFixed(6)), lng: Number(st.currentLng.toFixed(6)) });
      }
    }
  };

  // Poll native service coordinates smoothly while spoofing / route running on Android
  useEffect(() => {
    if (!isAndroidNative()) return;
    const id = window.setInterval(() => {
      const st = getNativeMockStatus(target.lat, target.lng);
      setNativeStatus(st);
      if (st.isSpoofing && st.isRoutePlaying) {
        setTarget({
          lat: Number(st.currentLat.toFixed(6)),
          lng: Number(st.currentLng.toFixed(6)),
        });
        setBearing(st.bearing);
      }
    }, 800);
    return () => window.clearInterval(id);
  }, [isSpoofing, routePlaying]);

  // Web browser route simulation loop (when not running inside Android native service)
  useEffect(() => {
    if (!routePlaying || isAndroidNative() || routePoints.length < 2) return;
    const tickMs = 250;
    const id = window.setInterval(() => {
      const speedMps = (config.speedKmh * 1000) / 3600;
      let step = speedMps * (tickMs / 1000);
      let { segIdx, progressMeters } = routeSegmentRef.current;

      while (step > 0) {
        if (segIdx >= routePoints.length - 1) {
          if (config.loopRoute) {
            segIdx = 0;
            progressMeters = 0;
          } else {
            setRoutePlaying(false);
            showToast('Маршрут завершён!');
            return;
          }
        }
        const a = routePoints[segIdx];
        const b = routePoints[segIdx + 1];
        const segLen = Math.max(0.5, distanceBetweenMeters(a, b));
        const rem = segLen - progressMeters;
        if (step < rem) {
          progressMeters += step;
          step = 0;
        } else {
          step -= rem;
          segIdx += 1;
          progressMeters = 0;
        }
      }

      routeSegmentRef.current = { segIdx, progressMeters };
      const curA = routePoints[Math.min(segIdx, routePoints.length - 2)];
      const curB = routePoints[Math.min(segIdx + 1, routePoints.length - 1)];
      const br = bearingBetweenDeg(curA, curB);
      const pos = moveLatLng(curA.lat, curA.lng, progressMeters, br);
      setBearing(Math.round(br));
      setTarget(pos);
    }, tickMs);
    return () => window.clearInterval(id);
  }, [routePlaying, routePoints, config.speedKmh, config.loopRoute]);

  const handlePickMapPoint = (pt: LatLng) => {
    if (activeTab === 'route') {
      setRoutePoints((prev) => [...prev, pt]);
      if (routePoints.length === 0) {
        setTarget(pt);
        if (isSpoofing) nativeUpdateLocation(pt.lat, pt.lng, 0, bearing);
      }
      return;
    }

    setFollowTarget(false);
    setTarget(pt);
    if (isSpoofing) {
      nativeUpdateLocation(pt.lat, pt.lng, 0, bearing);
    }
  };

  const handleToggleSpoofing = () => {
    if (isSpoofing) {
      nativeStopRoute();
      nativeStopSpoofing();
      setRoutePlaying(false);
      setIsSpoofing(false);
      showToast('Fake GPS остановлен');
      return;
    }

    if (isAndroidNative()) {
      const ok = nativeStartSpoofing(target.lat, target.lng, config);
      const st = getNativeMockStatus(target.lat, target.lng);
      setNativeStatus(st);
      if (!ok) {
        setSetupOpen(true);
        return;
      }
    }

    setIsSpoofing(true);
    setPlaces((prev) => [
      {
        id: `hist_${Date.now()}`,
        name: `Точка ${target.lat.toFixed(4)}, ${target.lng.toFixed(4)}`,
        lat: target.lat,
        lng: target.lng,
        isFavorite: false,
        lastUsedAt: Date.now(),
      },
      ...prev.filter((p) => p.isFavorite || Math.abs(p.lat - target.lat) > 0.002).slice(0, 14),
    ]);
    showToast(`GPS зафиксирован: ${target.lat.toFixed(5)}, ${target.lng.toFixed(5)}`);
  };

  const handleJoystickStep = (distanceMeters: number, br: number) => {
    setFollowTarget(true);
    setBearing(Math.round(br));
    setTarget((prev) => {
      const next = moveLatLng(prev.lat, prev.lng, distanceMeters, br);
      if (isSpoofing) {
        nativeUpdateLocation(next.lat, next.lng, config.speedKmh, br);
      }
      return next;
    });
  };

  const handleStartOrStopRoute = () => {
    if (routePlaying) {
      nativeStopRoute();
      setRoutePlaying(false);
      return;
    }
    if (routePoints.length < 2) {
      showToast('Поставьте минимум 2 точки на карте для маршрута!');
      return;
    }
    if (!isSpoofing) {
      const ok = nativeStartSpoofing(routePoints[0].lat, routePoints[0].lng, config);
      if (isAndroidNative() && !ok) {
        setSetupOpen(true);
        return;
      }
      setIsSpoofing(true);
    }
    routeSegmentRef.current = { segIdx: 0, progressMeters: 0 };
    setTarget(routePoints[0]);
    setFollowTarget(true);
    nativeStartRoute(routePoints, config.speedKmh, config.loopRoute);
    setRoutePlaying(true);
    showToast(`Маршрут запущен (${config.speedKmh} км/ч)`);
  };

  const handleSearch = async (query: string) => {
    setSearchQuery(query);
    const trimmed = query.trim();
    if (!trimmed) {
      setSearchResults([]);
      setShowSearchDropdown(false);
      return;
    }
    setShowSearchDropdown(true);

    // 1. Direct coordinates input (e.g. "52.2297, 21.0122")
    const coordMatch = trimmed.match(/^(-?\d+(?:\.\d+)?)\s*[,\s]\s*(-?\d+(?:\.\d+)?)$/);
    if (coordMatch) {
      const lat = Number.parseFloat(coordMatch[1]);
      const lng = Number.parseFloat(coordMatch[2]);
      if (lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180) {
        setSearchResults([
          {
            name: `Перейти к координатам`,
            sub: `${lat.toFixed(6)}, ${lng.toFixed(6)}`,
            lat,
            lng,
          },
        ]);
        return;
      }
    }

    // 2. Instant offline preset match
    const qLower = trimmed.toLowerCase();
    const localMatches = PRESET_CITIES.filter(
      (c) =>
        c.name.toLowerCase().includes(qLower) ||
        c.country.toLowerCase().includes(qLower),
    ).map((c) => ({
      name: c.name,
      sub: c.country,
      lat: c.lat,
      lng: c.lng,
    }));
    setSearchResults(localMatches);

    // 3. Online Nominatim search when 3+ chars
    if (trimmed.length >= 3) {
      try {
        setSearchingOnline(true);
        const res = await fetch(
          `https://nominatim.openstreetmap.org/search?format=json&limit=5&q=${encodeURIComponent(trimmed)}`,
          { headers: { 'Accept-Language': 'ru,en' } },
        );
        if (res.ok) {
          const data = (await res.json()) as Array<{
            display_name: string;
            lat: string;
            lon: string;
          }>;
          const online = data.map((d) => ({
            name: d.display_name.split(',')[0],
            sub: d.display_name.split(',').slice(1, 3).join(','),
            lat: Number.parseFloat(d.lat),
            lng: Number.parseFloat(d.lon),
          }));
          setSearchResults([...localMatches.slice(0, 3), ...online].slice(0, 6));
        }
      } catch {
        // Offline — localMatches already shown
      } finally {
        setSearchingOnline(false);
      }
    }
  };

  const jumpToPoint = (lat: number, lng: number, label?: string) => {
    const pt = { lat: Number(lat.toFixed(6)), lng: Number(lng.toFixed(6)) };
    setFollowTarget(true);
    setTarget(pt);
    setShowSearchDropdown(false);
    if (isSpoofing) {
      nativeUpdateLocation(pt.lat, pt.lng, 0, bearing);
    }
    if (label) {
      showToast(`Точка выбрана: ${label}`);
    }
  };

  const handleSaveFavorite = () => {
    const name = saveName.trim() || `Избранное (${target.lat.toFixed(3)}, ${target.lng.toFixed(3)})`;
    setPlaces((prev) => [
      {
        id: `fav_${Date.now()}`,
        name,
        lat: target.lat,
        lng: target.lng,
        isFavorite: true,
        lastUsedAt: Date.now(),
      },
      ...prev,
    ]);
    setSaveName('');
    showToast(`Сохранено в избранное: ${name}`);
  };

  return (
    <div className="h-screen w-screen flex flex-col bg-slate-950 text-slate-100 overflow-hidden select-none">
      {/* Top Floating Search & Status Header */}
      <header className="relative z-30 bg-slate-950/95 border-b border-slate-800 px-3 py-2.5 flex items-center gap-2">
        <div className="flex items-center gap-2 shrink-0">
          <div
            className={`w-9 h-9 rounded-xl flex items-center justify-center ${
              isSpoofing ? 'bg-emerald-500 text-slate-950' : 'bg-sky-500 text-slate-950'
            }`}
          >
            <Compass className="w-5 h-5 stroke-[2.5]" />
          </div>
          <div className="hidden sm:block leading-tight">
            <div className="font-black text-sm tracking-tight">FakeGpsPro</div>
            <div className="text-[10px] font-bold text-slate-400">
              {isSpoofing ? 'СПУФИНГ АКТИВЕН' : 'ГОТОВ К ВЫБОРУ ТОЧКИ'}
            </div>
          </div>
        </div>

        {/* Search Input */}
        <div className="relative flex-1">
          <div className="flex items-center bg-slate-900 border border-slate-700 focus-within:border-sky-500 rounded-xl px-3 py-2 gap-2">
            <Search className="w-4 h-4 text-slate-400 shrink-0" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => handleSearch(e.target.value)}
              onFocus={() => searchResults.length > 0 && setShowSearchDropdown(true)}
              placeholder="Город, адрес или 52.2297, 21.0122..."
              className="w-full bg-transparent text-xs sm:text-sm font-semibold text-white placeholder-slate-500 outline-none"
            />
            {searchingOnline && (
              <span className="text-[10px] font-bold text-sky-400 shrink-0">поиск...</span>
            )}
          </div>

          {showSearchDropdown && searchResults.length > 0 && (
            <div className="absolute left-0 right-0 mt-1.5 rounded-2xl bg-slate-900 border border-slate-700 shadow-2xl overflow-hidden z-50 max-h-64 overflow-y-auto">
              {searchResults.map((r, idx) => (
                <button
                  key={`${r.lat}_${r.lng}_${idx}`}
                  type="button"
                  onClick={() => jumpToPoint(r.lat, r.lng, r.name)}
                  className="w-full px-3.5 py-2.5 text-left hover:bg-slate-800 border-b border-slate-800/80 last:border-0 flex items-center justify-between gap-2 cursor-pointer"
                >
                  <div>
                    <div className="text-xs font-bold text-white">{r.name}</div>
                    <div className="text-[11px] text-slate-400">{r.sub}</div>
                  </div>
                  <span className="text-[10px] font-mono text-sky-400 shrink-0">
                    {r.lat.toFixed(3)}, {r.lng.toFixed(3)}
                  </span>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Joystick Toggle + Setup Button */}
        <button
          type="button"
          onClick={() => setShowJoystick((s) => !s)}
          className={`p-2.5 rounded-xl border text-xs font-bold cursor-pointer ${
            showJoystick
              ? 'bg-emerald-500/20 border-emerald-500/50 text-emerald-400'
              : 'bg-slate-900 border-slate-700 text-slate-300'
          }`}
          title="Показать/скрыть джойстик"
        >
          <Gamepad2 className="w-4 h-4" />
        </button>

        <button
          type="button"
          onClick={() => {
            refreshNativeStatus();
            setSetupOpen(true);
          }}
          className={`p-2.5 rounded-xl border text-xs font-bold cursor-pointer ${
            nativeStatus.isAndroid && !nativeStatus.hasMockPermission
              ? 'bg-amber-500/20 border-amber-500 text-amber-300 animate-pulse'
              : 'bg-slate-900 border-slate-700 text-slate-300'
          }`}
          title="Настройка Android Mock Location"
        >
          <Wrench className="w-4 h-4" />
        </button>
      </header>

      {/* Main Map Area */}
      <div className="relative flex-1 min-h-0">
        <MapCanvas
          target={target}
          isSpoofing={isSpoofing}
          accuracyMeters={config.accuracyMeters}
          mapStyle={mapStyle}
          onChangeMapStyle={setMapStyle}
          routeMode={activeTab === 'route'}
          routePoints={routePoints}
          onPickPoint={handlePickMapPoint}
          followTarget={followTarget}
        />

        {/* Floating Joystick Overlay */}
        {showJoystick && (
          <div className="absolute left-3 bottom-3 z-20">
            <JoystickPad
              speedKmh={config.speedKmh}
              onChangeSpeed={(kmh) => setConfig((c) => ({ ...c, speedKmh: kmh }))}
              onStep={handleJoystickStep}
            />
          </div>
        )}

        {/* Toast Banner */}
        {toast && (
          <div className="absolute bottom-3 right-3 z-20 px-3.5 py-2 rounded-xl bg-slate-900/95 border border-emerald-500/50 text-xs font-bold text-emerald-300 shadow-xl backdrop-blur">
            {toast}
          </div>
        )}
      </div>

      {/* Bottom Control Panel */}
      <section className="z-30 bg-slate-950 border-t border-slate-800 px-3 pt-2.5 pb-3 space-y-2.5">
        {/* Tab Content */}
        {activeTab === 'radar' && (
          <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-2.5">
            <div className="rounded-2xl bg-slate-900 border border-slate-800 px-3.5 py-2 flex items-center justify-between gap-3 flex-1">
              <div>
                <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                  Выбранная точка · Курс {bearing}°
                </div>
                <div className="text-sm sm:text-base font-mono font-bold text-white">
                  {target.lat.toFixed(6)}, {target.lng.toFixed(6)}
                </div>
              </div>
              <span
                className={`px-2.5 py-1 rounded-full text-[11px] font-black ${
                  isSpoofing
                    ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/40'
                    : 'bg-slate-800 text-slate-400'
                }`}
              >
                {isSpoofing ? '● В ЭФИРЕ' : '○ ОЖИДАНИЕ'}
              </span>
            </div>

            <button
              type="button"
              onClick={handleToggleSpoofing}
              className={`rounded-2xl px-6 py-3.5 font-black text-sm uppercase tracking-wider flex items-center justify-center gap-2 cursor-pointer transition-transform active:scale-[0.98] ${
                isSpoofing
                  ? 'bg-rose-600 hover:bg-rose-500 text-white shadow-lg shadow-rose-950/50'
                  : 'bg-emerald-500 hover:bg-emerald-400 text-slate-950 shadow-lg shadow-emerald-950/50'
              }`}
            >
              {isSpoofing ? (
                <>
                  <Square className="w-4 h-4 fill-white" /> Остановить
                </>
              ) : (
                <>
                  <Play className="w-4 h-4 fill-slate-950" /> Запустить Fake GPS
                </>
              )}
            </button>
          </div>
        )}

        {activeTab === 'route' && (
          <div className="space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="text-xs font-bold text-slate-300">
                Точек маршрута: <span className="text-amber-400 font-black">{routePoints.length}</span> (нажимайте на карту для добавления)
              </div>
              <div className="flex items-center gap-2">
                <label className="text-xs font-bold text-slate-300 flex items-center gap-1.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={config.loopRoute}
                    onChange={(e) => setConfig((c) => ({ ...c, loopRoute: e.target.checked }))}
                    className="accent-amber-500"
                  />
                  Повтор
                </label>
                <button
                  type="button"
                  onClick={() => {
                    nativeStopRoute();
                    setRoutePlaying(false);
                    setRoutePoints([]);
                  }}
                  className="px-2.5 py-1.5 rounded-xl bg-slate-900 border border-slate-700 text-xs font-bold text-slate-300 flex items-center gap-1 cursor-pointer"
                >
                  <Trash2 className="w-3.5 h-3.5 text-rose-400" /> Очистить
                </button>
                <button
                  type="button"
                  onClick={handleStartOrStopRoute}
                  className={`px-4 py-1.5 rounded-xl font-black text-xs flex items-center gap-1.5 cursor-pointer ${
                    routePlaying
                      ? 'bg-rose-600 text-white'
                      : 'bg-amber-500 text-slate-950'
                  }`}
                >
                  {routePlaying ? <Square className="w-3.5 h-3.5 fill-white" /> : <Play className="w-3.5 h-3.5 fill-slate-950" />}
                  {routePlaying ? 'Стоп маршрута' : 'Поехать по маршруту'}
                </button>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'places' && (
          <div className="space-y-2 max-h-44 overflow-y-auto">
            <div className="flex items-center gap-2">
              <input
                type="text"
                value={saveName}
                onChange={(e) => setSaveName(e.target.value)}
                placeholder="Название текущей точки..."
                className="flex-1 rounded-xl bg-slate-900 border border-slate-700 px-3 py-1.5 text-xs font-semibold text-white outline-none focus:border-sky-500"
              />
              <button
                type="button"
                onClick={handleSaveFavorite}
                className="px-3 py-1.5 rounded-xl bg-sky-500 text-slate-950 font-black text-xs flex items-center gap-1 cursor-pointer shrink-0"
              >
                <Plus className="w-3.5 h-3.5" /> Сохранить
              </button>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
              {places.map((p) => (
                <div
                  key={p.id}
                  className="rounded-xl bg-slate-900 border border-slate-800 px-3 py-2 flex items-center justify-between gap-2"
                >
                  <button
                    type="button"
                    onClick={() => jumpToPoint(p.lat, p.lng, p.name)}
                    className="text-left flex-1 min-w-0 cursor-pointer"
                  >
                    <div className="text-xs font-bold text-white truncate">{p.name}</div>
                    <div className="text-[10px] font-mono text-slate-400">
                      {p.lat.toFixed(4)}, {p.lng.toFixed(4)}
                    </div>
                  </button>
                  <button
                    type="button"
                    onClick={() =>
                      setPlaces((prev) =>
                        prev.map((x) => (x.id === p.id ? { ...x, isFavorite: !x.isFavorite } : x)),
                      )
                    }
                    className="p-1 cursor-pointer"
                  >
                    <Star
                      className={`w-4 h-4 ${
                        p.isFavorite ? 'text-amber-400 fill-amber-400' : 'text-slate-600'
                      }`}
                    />
                  </button>
                  <button
                    type="button"
                    onClick={() => setPlaces((prev) => prev.filter((x) => x.id !== p.id))}
                    className="p-1 text-slate-500 hover:text-rose-400 cursor-pointer"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {activeTab === 'settings' && (
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-xs">
            <label className="rounded-xl bg-slate-900 border border-slate-800 p-2 space-y-1">
              <span className="text-[10px] font-bold text-slate-400 block">Точность (м)</span>
              <input
                type="number"
                min={1}
                max={100}
                value={config.accuracyMeters}
                onChange={(e) =>
                  setConfig((c) => ({ ...c, accuracyMeters: Math.max(1, Number(e.target.value) || 4) }))
                }
                className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2 py-1 font-bold text-white"
              />
            </label>
            <label className="rounded-xl bg-slate-900 border border-slate-800 p-2 space-y-1">
              <span className="text-[10px] font-bold text-slate-400 block">Высота (м)</span>
              <input
                type="number"
                value={config.altitudeMeters}
                onChange={(e) =>
                  setConfig((c) => ({ ...c, altitudeMeters: Number(e.target.value) || 0 }))
                }
                className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2 py-1 font-bold text-white"
              />
            </label>
            <label className="rounded-xl bg-slate-900 border border-slate-800 p-2 space-y-1">
              <span className="text-[10px] font-bold text-slate-400 block">Микро-дрейф (м)</span>
              <input
                type="number"
                min={0}
                max={20}
                step={0.5}
                value={config.jitterMeters}
                onChange={(e) =>
                  setConfig((c) => ({ ...c, jitterMeters: Math.max(0, Number(e.target.value) || 0) }))
                }
                className="w-full bg-slate-950 border border-slate-700 rounded-lg px-2 py-1 font-bold text-white"
              />
            </label>
            <div className="rounded-xl bg-slate-900 border border-slate-800 p-2 flex flex-col justify-between">
              <span className="text-[10px] font-bold text-slate-400">Android права</span>
              <button
                type="button"
                onClick={() => {
                  refreshNativeStatus();
                  setSetupOpen(true);
                }}
                className="w-full py-1 rounded-lg bg-sky-500 text-slate-950 font-black text-[11px] cursor-pointer"
              >
                Мастер настройки
              </button>
            </div>
          </div>
        )}

        {/* Bottom Navigation Bar */}
        <nav className="grid grid-cols-4 gap-1.5 pt-1 border-t border-slate-800/80">
          {(
            [
              { id: 'radar', label: 'Точка', icon: <MapPin className="w-4 h-4" /> },
              { id: 'route', label: 'Маршрут', icon: <Route className="w-4 h-4" /> },
              { id: 'places', label: 'Места', icon: <Bookmark className="w-4 h-4" /> },
              { id: 'settings', label: 'Параметры', icon: <Settings className="w-4 h-4" /> },
            ] as const
          ).map((t) => {
            const active = activeTab === t.id;
            return (
              <button
                key={t.id}
                type="button"
                onClick={() => setActiveTab(t.id)}
                className={`py-2 rounded-xl flex items-center justify-center gap-1.5 text-xs font-bold cursor-pointer transition-colors ${
                  active
                    ? 'bg-slate-800 text-sky-400 border border-slate-700'
                    : 'text-slate-400 hover:text-slate-200'
                }`}
              >
                {t.icon}
                <span>{t.label}</span>
              </button>
            );
          })}
        </nav>
      </section>

      {setupOpen && (
        <SetupGuideModal
          status={nativeStatus}
          onRefreshStatus={refreshNativeStatus}
          onClose={() => setSetupOpen(false)}
        />
      )}
    </div>
  );
};

export default App;
