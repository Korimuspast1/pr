import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Bookmark,
  BrainCircuit,
  Gamepad2,
  MapPin,
  Play,
  Plus,
  Route,
  Search,
  Sparkles,
  Square,
  Star,
  Trash2,
  Wrench,
} from 'lucide-react';
import {
  analyzeSpoofRealism,
  composeAiRoutePlan,
  computeHumanizedSpeedKmh,
} from './ai/gpsAiEngine';
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
import { fetchRoadRoute } from './utils/roadRouter';

const STORAGE_KEY = 'fake_gps_pro_state_v2';

type DockTab = 'point' | 'route' | 'places';

interface PersistedState {
  target: LatLng;
  config: MockConfig;
  places: SavedPlace[];
  mapStyle: MapStyle;
  aiHumanize: boolean;
}

const DEFAULT_PERSISTED: PersistedState = {
  target: { lat: 52.2297, lng: 21.0122 },
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
      name: 'Варшава',
      lat: 52.2319,
      lng: 21.0067,
      isFavorite: true,
      lastUsedAt: Date.now(),
    },
    {
      id: 'p_krk',
      name: 'Краков',
      lat: 50.0619,
      lng: 19.9369,
      isFavorite: true,
      lastUsedAt: Date.now() - 10000,
    },
  ],
  mapStyle: 'dark',
  aiHumanize: true,
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
      mapStyle: parsed.mapStyle ?? 'dark',
      aiHumanize: parsed.aiHumanize ?? true,
    };
  } catch {
    return DEFAULT_PERSISTED;
  }
}

export const App: React.FC = () => {
  const initial = useMemo(() => loadState(), []);

  const [target, setTarget] = useState<LatLng>(initial.target);
  const [prevTarget, setPrevTarget] = useState<LatLng | null>(null);
  const [config, setConfig] = useState<MockConfig>(initial.config);
  const [places, setPlaces] = useState<SavedPlace[]>(initial.places);
  const [mapStyle, setMapStyle] = useState<MapStyle>(initial.mapStyle);
  const [aiHumanize, setAiHumanize] = useState<boolean>(initial.aiHumanize);

  const [dockTab, setDockTab] = useState<DockTab>('point');
  const [isSpoofing, setIsSpoofing] = useState(false);
  const [showJoystick, setShowJoystick] = useState(false);
  const [followTarget, setFollowTarget] = useState(false);
  const [bearing, setBearing] = useState(0);

  // Road routing & AI route composer state
  const [waypoints, setWaypoints] = useState<LatLng[]>([]);
  const [roadPath, setRoadPath] = useState<LatLng[]>([]);
  const [routeDistanceM, setRouteDistanceM] = useState(0);
  const [isRealRoad, setIsRealRoad] = useState(true);
  const [routingBusy, setRoutingBusy] = useState(false);
  const [routePlaying, setRoutePlaying] = useState(false);
  const [aiPrompt, setAiPrompt] = useState('');
  const [aiRouteSummary, setAiRouteSummary] = useState<string | null>(null);
  const [aiSeed, setAiSeed] = useState(0);
  const routeSegmentRef = useRef({ segIdx: 0, progressMeters: 0 });

  // Minimal search state
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<Array<{ name: string; sub: string; lat: number; lng: number }>>([]);
  const [showSearchDropdown, setShowSearchDropdown] = useState(false);
  const [saveName, setSaveName] = useState('');

  const [nativeStatus, setNativeStatus] = useState<NativeMockStatus>(() =>
    getNativeMockStatus(initial.target.lat, initial.target.lng),
  );
  const [setupOpen, setSetupOpen] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToast(msg);
    setTimeout(() => setToast(null), 2600);
  };

  useEffect(() => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ target, config, places, mapStyle, aiHumanize }),
    );
  }, [target, config, places, mapStyle, aiHumanize]);

  const aiAnalysis = useMemo(
    () => analyzeSpoofRealism(prevTarget, target, config.jitterMeters, isSpoofing, aiHumanize),
    [prevTarget, target, config.jitterMeters, isSpoofing, aiHumanize],
  );

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

  // Automatically snap waypoints to REAL roads whenever waypoints or transport speed profile change
  useEffect(() => {
    if (waypoints.length < 2) {
      setRoadPath(waypoints);
      setRouteDistanceM(0);
      return;
    }
    let cancelled = false;
    setRoutingBusy(true);
    fetchRoadRoute(waypoints, config.speedKmh).then((res) => {
      if (cancelled) return;
      setRoadPath(res.roadPoints);
      setRouteDistanceM(res.distanceMeters);
      setIsRealRoad(res.isRealRoad);
      setRoutingBusy(false);
    });
    return () => {
      cancelled = true;
    };
  }, [waypoints, config.speedKmh <= 8 ? 'foot' : config.speedKmh <= 25 ? 'bike' : 'car']);

  // Poll native Android service coordinates when route is playing
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
    }, 700);
    return () => window.clearInterval(id);
  }, [isSpoofing, routePlaying]);

  // Web browser route loop with AI corner-aware speed modulation
  useEffect(() => {
    const activeLine = roadPath.length >= 2 ? roadPath : waypoints;
    if (!routePlaying || isAndroidNative() || activeLine.length < 2) return;
    const tickMs = 220;
    const id = window.setInterval(() => {
      const { segIdx: curSeg } = routeSegmentRef.current;
      const dynSpeed = computeHumanizedSpeedKmh(config.speedKmh, activeLine, curSeg, aiHumanize);
      const speedMps = (dynSpeed * 1000) / 3600;
      let step = speedMps * (tickMs / 1000);
      let { segIdx, progressMeters } = routeSegmentRef.current;

      while (step > 0) {
        if (segIdx >= activeLine.length - 1) {
          if (config.loopRoute) {
            segIdx = 0;
            progressMeters = 0;
          } else {
            setRoutePlaying(false);
            return;
          }
        }
        const a = activeLine[segIdx];
        const b = activeLine[segIdx + 1];
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
      const curA = activeLine[Math.min(segIdx, activeLine.length - 2)];
      const curB = activeLine[Math.min(segIdx + 1, activeLine.length - 1)];
      const br = bearingBetweenDeg(curA, curB);
      const pos = moveLatLng(curA.lat, curA.lng, progressMeters, br);
      setBearing(Math.round(br));
      setTarget(pos);
    }, tickMs);
    return () => window.clearInterval(id);
  }, [routePlaying, roadPath, waypoints, config.speedKmh, config.loopRoute, aiHumanize]);

  const handlePickMapPoint = (pt: LatLng) => {
    if (dockTab === 'route') {
      setWaypoints((prev) => [...prev, pt]);
      if (waypoints.length === 0) {
        setPrevTarget(target);
        setTarget(pt);
      }
      return;
    }

    setFollowTarget(false);
    setPrevTarget(target);
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
    showToast('GPS активен');
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
    const pathToRun = roadPath.length >= 2 ? roadPath : waypoints;
    if (pathToRun.length < 2) {
      showToast('Нажмите 2 точки на карте');
      return;
    }
    if (!isSpoofing) {
      const ok = nativeStartSpoofing(pathToRun[0].lat, pathToRun[0].lng, config);
      if (isAndroidNative() && !ok) {
        setSetupOpen(true);
        return;
      }
      setIsSpoofing(true);
    }
    routeSegmentRef.current = { segIdx: 0, progressMeters: 0 };
    setTarget(pathToRun[0]);
    setFollowTarget(true);
    nativeStartRoute(pathToRun, config.speedKmh, config.loopRoute);
    setRoutePlaying(true);
  };

  const handleAiComposeRoute = async (overridePrompt?: string) => {
    const query = overridePrompt !== undefined ? overridePrompt : aiPrompt;
    setRoutingBusy(true);
    const nextSeed = aiSeed + 1;
    setAiSeed(nextSeed);
    const plan = await composeAiRoutePlan(target, query, config.speedKmh, places, nextSeed);
    setWaypoints(plan.waypoints);
    setAiRouteSummary(plan.summary);
    showToast(plan.summary);
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

    const coordMatch = trimmed.match(/^(-?\d+(?:\.\d+)?)\s*[,\s]\s*(-?\d+(?:\.\d+)?)$/);
    if (coordMatch) {
      const lat = Number.parseFloat(coordMatch[1]);
      const lng = Number.parseFloat(coordMatch[2]);
      if (lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180) {
        setSearchResults([{ name: 'Координаты', sub: `${lat.toFixed(5)}, ${lng.toFixed(5)}`, lat, lng }]);
        return;
      }
    }

    const qLower = trimmed.toLowerCase();
    const localMatches = PRESET_CITIES.filter(
      (c) => c.name.toLowerCase().includes(qLower) || c.country.toLowerCase().includes(qLower),
    ).map((c) => ({ name: c.name, sub: c.country, lat: c.lat, lng: c.lng }));
    setSearchResults(localMatches);

    if (trimmed.length >= 3) {
      try {
        const res = await fetch(
          `https://nominatim.openstreetmap.org/search?format=json&limit=4&q=${encodeURIComponent(trimmed)}`,
          { headers: { 'Accept-Language': 'ru,en' } },
        );
        if (res.ok) {
          const data = (await res.json()) as Array<{ display_name: string; lat: string; lon: string }>;
          const online = data.map((d) => ({
            name: d.display_name.split(',')[0],
            sub: d.display_name.split(',').slice(1, 3).join(','),
            lat: Number.parseFloat(d.lat),
            lng: Number.parseFloat(d.lon),
          }));
          setSearchResults([...localMatches.slice(0, 2), ...online].slice(0, 5));
        }
      } catch {
        // offline
      }
    }
  };

  const jumpToPoint = (lat: number, lng: number) => {
    const pt = { lat: Number(lat.toFixed(6)), lng: Number(lng.toFixed(6)) };
    setFollowTarget(true);
    setPrevTarget(target);
    setTarget(pt);
    setShowSearchDropdown(false);
    if (isSpoofing) nativeUpdateLocation(pt.lat, pt.lng, 0, bearing);
  };

  const handleSaveFavorite = () => {
    const name = saveName.trim() || `${target.lat.toFixed(3)}, ${target.lng.toFixed(3)}`;
    setPlaces((prev) => [
      { id: `fav_${Date.now()}`, name, lat: target.lat, lng: target.lng, isFavorite: true, lastUsedAt: Date.now() },
      ...prev,
    ]);
    setSaveName('');
    showToast('Сохранено');
  };

  return (
    <div className="relative h-screen w-screen bg-slate-950 text-slate-100 overflow-hidden select-none">
      {/* Fullscreen Map */}
      <MapCanvas
        target={target}
        isSpoofing={isSpoofing}
        accuracyMeters={config.accuracyMeters}
        mapStyle={mapStyle}
        onChangeMapStyle={setMapStyle}
        waypoints={waypoints}
        roadPath={roadPath}
        onPickPoint={handlePickMapPoint}
        followTarget={followTarget}
      />

      {/* Minimal Floating Top Search Pill */}
      <header className="absolute top-3 left-3 right-3 z-30 flex items-center gap-2 pointer-events-none">
        <div className="relative flex-1 pointer-events-auto">
          <div className="flex items-center bg-slate-950/85 border border-white/15 focus-within:border-sky-400 rounded-2xl px-3.5 py-2.5 gap-2.5 shadow-2xl backdrop-blur-xl">
            <span
              className={`w-2.5 h-2.5 rounded-full shrink-0 ${
                isSpoofing ? 'bg-emerald-400 animate-pulse' : 'bg-sky-400'
              }`}
            />
            <Search className="w-4 h-4 text-slate-400 shrink-0" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => handleSearch(e.target.value)}
              onFocus={() => searchResults.length > 0 && setShowSearchDropdown(true)}
              placeholder="Поиск или 52.23, 21.01"
              className="w-full bg-transparent text-sm font-medium text-white placeholder-slate-400 outline-none"
            />
          </div>

          {showSearchDropdown && searchResults.length > 0 && (
            <div className="absolute left-0 right-0 mt-1.5 rounded-2xl bg-slate-950/95 border border-white/15 shadow-2xl overflow-hidden z-50 max-h-56 overflow-y-auto backdrop-blur-xl">
              {searchResults.map((r, idx) => (
                <button
                  key={`${r.lat}_${r.lng}_${idx}`}
                  type="button"
                  onClick={() => jumpToPoint(r.lat, r.lng)}
                  className="w-full px-4 py-2.5 text-left hover:bg-white/10 border-b border-white/5 last:border-0 cursor-pointer"
                >
                  <div className="text-xs font-bold text-white">{r.name}</div>
                  <div className="text-[11px] text-slate-400">{r.sub}</div>
                </button>
              ))}
            </div>
          )}
        </div>

        <button
          type="button"
          onClick={() => setShowJoystick((s) => !s)}
          className={`pointer-events-auto w-11 h-11 rounded-2xl border flex items-center justify-center shadow-xl backdrop-blur-xl cursor-pointer ${
            showJoystick
              ? 'bg-emerald-500 border-emerald-400 text-slate-950'
              : 'bg-slate-950/85 border-white/15 text-slate-200'
          }`}
          title="Джойстик"
        >
          <Gamepad2 className="w-5 h-5" />
        </button>

        <button
          type="button"
          onClick={() => {
            refreshNativeStatus();
            setSetupOpen(true);
          }}
          className={`pointer-events-auto w-11 h-11 rounded-2xl border flex items-center justify-center shadow-xl backdrop-blur-xl cursor-pointer ${
            nativeStatus.isAndroid && !nativeStatus.hasMockPermission
              ? 'bg-amber-500 border-amber-300 text-slate-950 animate-pulse'
              : 'bg-slate-950/85 border-white/15 text-slate-200'
          }`}
          title="Настройка Android"
        >
          <Wrench className="w-4 h-4" />
        </button>
      </header>

      {/* Floating Joystick */}
      {showJoystick && (
        <div className="absolute left-3 bottom-44 z-20">
          <JoystickPad
            speedKmh={config.speedKmh}
            onChangeSpeed={(kmh) => setConfig((c) => ({ ...c, speedKmh: kmh }))}
            onStep={handleJoystickStep}
          />
        </div>
      )}

      {/* Minimal Toast */}
      {toast && (
        <div className="absolute top-16 left-1/2 -translate-x-1/2 z-30 px-4 py-1.5 rounded-full bg-slate-950/90 border border-white/15 text-xs font-semibold text-emerald-300 shadow-xl backdrop-blur-xl">
          {toast}
        </div>
      )}

      {/* Minimalist Floating Bottom Dock */}
      <div className="absolute bottom-3 left-3 right-3 z-30 max-w-xl mx-auto">
        <div className="rounded-3xl bg-slate-950/88 border border-white/15 p-3 shadow-2xl backdrop-blur-xl space-y-2.5">
          {dockTab === 'point' && (
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-sm font-bold text-white">
                    {target.lat.toFixed(5)}, {target.lng.toFixed(5)}
                  </span>
                  <button
                    type="button"
                    onClick={() => setAiHumanize((v) => !v)}
                    className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold cursor-pointer ${
                      aiHumanize
                        ? 'bg-violet-500/25 text-violet-300 border border-violet-400/40'
                        : 'bg-white/10 text-slate-400'
                    }`}
                    title={aiAnalysis.tip}
                  >
                    <BrainCircuit className="w-3 h-3" />
                    ИИ {aiAnalysis.realismScore}%
                  </button>
                </div>
                {aiAnalysis.alertKind === 'caution' && (
                  <div className="text-[10px] text-amber-300 truncate mt-0.5">{aiAnalysis.tip}</div>
                )}
              </div>

              <button
                type="button"
                onClick={handleToggleSpoofing}
                className={`rounded-2xl px-5 py-3 font-bold text-sm flex items-center gap-2 shrink-0 cursor-pointer transition-transform active:scale-95 ${
                  isSpoofing
                    ? 'bg-rose-500 hover:bg-rose-400 text-white'
                    : 'bg-emerald-400 hover:bg-emerald-300 text-slate-950'
                }`}
              >
                {isSpoofing ? (
                  <>
                    <Square className="w-4 h-4 fill-white" /> Стоп
                  </>
                ) : (
                  <>
                    <Play className="w-4 h-4 fill-slate-950" /> Старт
                  </>
                )}
              </button>
            </div>
          )}

          {dockTab === 'route' && (
            <div className="space-y-2">
              <div className="flex items-center gap-1.5">
                <input
                  type="text"
                  value={aiPrompt}
                  onChange={(e) => setAiPrompt(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleAiComposeRoute();
                  }}
                  placeholder="ИИ-план: напр. 3 км, центр, север..."
                  className="flex-1 rounded-xl bg-white/5 border border-white/15 px-3 py-1.5 text-xs text-white placeholder-slate-400 outline-none focus:border-violet-400"
                />
                <button
                  type="button"
                  onClick={() => handleAiComposeRoute()}
                  className="px-3 py-1.5 rounded-xl bg-violet-500/25 border border-violet-400/45 text-violet-200 font-bold text-xs flex items-center gap-1 cursor-pointer shrink-0"
                >
                  <Sparkles className="w-3.5 h-3.5" /> ИИ-маршрут
                </button>
              </div>

              <div className="flex items-center justify-between gap-2 text-xs">
                <div className="font-semibold text-slate-300 truncate">
                  {routingBusy
                    ? 'ИИ строит маршрут...'
                    : waypoints.length < 2
                      ? 'Тапайте карту или спросите ИИ'
                      : `${(routeDistanceM / 1000).toFixed(2)} км · ${isRealRoad ? 'OSM-дороги' : 'ИИ-улицы'}`}
                </div>

                <div className="flex items-center gap-1 shrink-0">
                  {[5, 18, 50].map((s) => (
                    <button
                      key={s}
                      type="button"
                      onClick={() => setConfig((c) => ({ ...c, speedKmh: s }))}
                      className={`px-2 py-1 rounded-lg text-[10px] font-bold cursor-pointer ${
                        config.speedKmh === s
                          ? 'bg-sky-400 text-slate-950'
                          : 'bg-white/10 text-slate-300'
                      }`}
                    >
                      {s} км/ч
                    </button>
                  ))}
                </div>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => handleAiComposeRoute('')}
                  className="px-3 py-2.5 rounded-2xl bg-violet-500/20 border border-violet-400/40 text-violet-200 text-xs font-bold flex items-center gap-1.5 cursor-pointer"
                  title={aiRouteSummary ?? 'Сгенерировать новый маршрут вокруг точки'}
                >
                  <BrainCircuit className="w-3.5 h-3.5" /> ИИ-круг
                </button>

                {waypoints.length > 0 && (
                  <button
                    type="button"
                    onClick={() => {
                      nativeStopRoute();
                      setRoutePlaying(false);
                      setWaypoints([]);
                      setRoadPath([]);
                      setAiRouteSummary(null);
                    }}
                    className="p-2.5 rounded-2xl bg-white/10 text-slate-300 hover:text-rose-400 cursor-pointer"
                    title="Очистить"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                )}

                <button
                  type="button"
                  onClick={handleStartOrStopRoute}
                  className={`flex-1 py-2.5 rounded-2xl font-bold text-xs flex items-center justify-center gap-1.5 cursor-pointer ${
                    routePlaying
                      ? 'bg-rose-500 text-white'
                      : 'bg-sky-400 text-slate-950'
                  }`}
                >
                  {routePlaying ? <Square className="w-3.5 h-3.5 fill-white" /> : <Play className="w-3.5 h-3.5 fill-slate-950" />}
                  {routePlaying ? 'Стоп' : 'Поехать'}
                </button>
              </div>
            </div>
          )}

          {dockTab === 'places' && (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <input
                  type="text"
                  value={saveName}
                  onChange={(e) => setSaveName(e.target.value)}
                  placeholder="Название точки..."
                  className="flex-1 rounded-xl bg-white/5 border border-white/15 px-3 py-1.5 text-xs text-white outline-none focus:border-sky-400"
                />
                <button
                  type="button"
                  onClick={handleSaveFavorite}
                  className="px-3 py-1.5 rounded-xl bg-sky-400 text-slate-950 font-bold text-xs flex items-center gap-1 cursor-pointer"
                >
                  <Plus className="w-3.5 h-3.5" /> Сохранить
                </button>
              </div>

              <div className="flex gap-2 overflow-x-auto pb-0.5">
                {places.map((p) => (
                  <div
                    key={p.id}
                    className="shrink-0 rounded-xl bg-white/5 border border-white/10 px-3 py-1.5 flex items-center gap-2"
                  >
                    <button
                      type="button"
                      onClick={() => jumpToPoint(p.lat, p.lng)}
                      className="text-xs font-semibold text-white cursor-pointer"
                    >
                      {p.name}
                    </button>
                    <button
                      type="button"
                      onClick={() =>
                        setPlaces((prev) =>
                          prev.map((x) => (x.id === p.id ? { ...x, isFavorite: !x.isFavorite } : x)),
                        )
                      }
                      className="cursor-pointer"
                    >
                      <Star className={`w-3.5 h-3.5 ${p.isFavorite ? 'text-amber-400 fill-amber-400' : 'text-slate-500'}`} />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Minimal 3-Tab Switcher */}
          <nav className="grid grid-cols-3 gap-1.5 pt-1 border-t border-white/10">
            {(
              [
                { id: 'point', label: 'Точка', icon: <MapPin className="w-4 h-4" /> },
                { id: 'route', label: 'Маршрут', icon: <Route className="w-4 h-4" /> },
                { id: 'places', label: 'Места', icon: <Bookmark className="w-4 h-4" /> },
              ] as const
            ).map((t) => {
              const active = dockTab === t.id;
              return (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setDockTab(t.id)}
                  className={`py-2 rounded-2xl flex items-center justify-center gap-1.5 text-xs font-bold cursor-pointer transition-colors ${
                    active
                      ? 'bg-white/15 text-white'
                      : 'text-slate-400 hover:text-slate-200'
                  }`}
                >
                  {t.icon}
                  <span>{t.label}</span>
                </button>
              );
            })}
          </nav>
        </div>
      </div>

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
