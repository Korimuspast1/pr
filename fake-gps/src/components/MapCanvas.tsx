import React, { useEffect, useRef } from 'react';
import L from 'leaflet';
import { Crosshair, Layers, LocateFixed, Minus, Plus } from 'lucide-react';
import { LatLng, MapStyle } from '../types/gps';

interface MapCanvasProps {
  target: LatLng;
  isSpoofing: boolean;
  accuracyMeters: number;
  mapStyle: MapStyle;
  onChangeMapStyle: (style: MapStyle) => void;
  routeMode: boolean;
  routePoints: LatLng[];
  onPickPoint: (point: LatLng) => void;
  followTarget?: boolean;
}

const TILE_LAYERS: Record<MapStyle, { url: string; attr: string; label: string }> = {
  osm: {
    url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    attr: '© OpenStreetMap',
    label: 'Карта',
  },
  dark: {
    url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
    attr: '© CartoDB',
    label: 'Тёмная',
  },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attr: '© Esri',
    label: 'Спутник',
  },
};

function createPinIcon(isSpoofing: boolean): L.DivIcon {
  const color = isSpoofing ? '#10b981' : '#38bdf8';
  const ring = isSpoofing
    ? `<span class="gps-pulse-ring" style="position:absolute;inset:-8px;border-radius:9999px;border:3px solid ${color};"></span>`
    : '';
  return L.divIcon({
    className: 'fakegps-custom-pin',
    html: `
      <div style="position:relative;width:28px;height:28px;display:flex;align-items:center;justify-content:center;">
        ${ring}
        <span style="width:20px;height:20px;border-radius:9999px;background:${color};border:3px solid #ffffff;box-shadow:0 4px 12px rgba(0,0,0,0.5);"></span>
      </div>
    `,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
  });
}

export const MapCanvas: React.FC<MapCanvasProps> = ({
  target,
  isSpoofing,
  accuracyMeters,
  mapStyle,
  onChangeMapStyle,
  routeMode,
  routePoints,
  onPickPoint,
  followTarget = false,
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<L.Map | null>(null);
  const tileRef = useRef<L.TileLayer | null>(null);
  const markerRef = useRef<L.Marker | null>(null);
  const circleRef = useRef<L.Circle | null>(null);
  const routeLineRef = useRef<L.Polyline | null>(null);
  const routeMarkersRef = useRef<L.CircleMarker[]>([]);

  const onPickRef = useRef(onPickPoint);
  onPickRef.current = onPickPoint;

  // Initialize Leaflet Map once
  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    const map = L.map(containerRef.current, {
      center: [target.lat, target.lng],
      zoom: 15,
      zoomControl: false,
      attributionControl: false,
    });
    mapRef.current = map;

    const layerConf = TILE_LAYERS[mapStyle];
    tileRef.current = L.tileLayer(layerConf.url, { maxZoom: 19 }).addTo(map);

    const circle = L.circle([target.lat, target.lng], {
      radius: Math.max(8, accuracyMeters),
      color: isSpoofing ? '#10b981' : '#38bdf8',
      fillColor: isSpoofing ? '#10b981' : '#38bdf8',
      fillOpacity: 0.16,
      weight: 1.5,
    }).addTo(map);
    circleRef.current = circle;

    const marker = L.marker([target.lat, target.lng], {
      icon: createPinIcon(isSpoofing),
      draggable: true,
    }).addTo(map);
    markerRef.current = marker;

    // Dragging the pin updates location directly
    marker.on('dragend', () => {
      const pos = marker.getLatLng();
      onPickRef.current({
        lat: Number(pos.lat.toFixed(6)),
        lng: Number(pos.lng.toFixed(6)),
      });
    });

    // Tapping/clicking anywhere on the map drops the pin there!
    map.on('click', (ev: L.LeafletMouseEvent) => {
      onPickRef.current({
        lat: Number(ev.latlng.lat.toFixed(6)),
        lng: Number(ev.latlng.lng.toFixed(6)),
      });
    });

    return () => {
      map.remove();
      mapRef.current = null;
    };
  }, []);

  // Update tile style
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    if (tileRef.current) {
      map.removeLayer(tileRef.current);
    }
    tileRef.current = L.tileLayer(TILE_LAYERS[mapStyle].url, { maxZoom: 19 }).addTo(map);
  }, [mapStyle]);

  // Sync pin position & accuracy circle
  useEffect(() => {
    const map = mapRef.current;
    const marker = markerRef.current;
    const circle = circleRef.current;
    if (!map || !marker || !circle) return;

    marker.setLatLng([target.lat, target.lng]);
    marker.setIcon(createPinIcon(isSpoofing));

    circle.setLatLng([target.lat, target.lng]);
    circle.setRadius(Math.max(8, accuracyMeters));
    circle.setStyle({
      color: isSpoofing ? '#10b981' : '#38bdf8',
      fillColor: isSpoofing ? '#10b981' : '#38bdf8',
    });

    if (followTarget) {
      map.panTo([target.lat, target.lng], { animate: true, duration: 0.3 });
    }
  }, [target.lat, target.lng, isSpoofing, accuracyMeters, followTarget]);

  // Sync route polyline & waypoint dots
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    if (routeLineRef.current) {
      map.removeLayer(routeLineRef.current);
      routeLineRef.current = null;
    }
    routeMarkersRef.current.forEach((m) => map.removeLayer(m));
    routeMarkersRef.current = [];

    if (routePoints.length > 0) {
      const latlngs = routePoints.map((p) => [p.lat, p.lng] as [number, number]);
      routeLineRef.current = L.polyline(latlngs, {
        color: '#f59e0b',
        weight: 4,
        dashArray: '8 6',
      }).addTo(map);

      routePoints.forEach((p, idx) => {
        const dot = L.circleMarker([p.lat, p.lng], {
          radius: 7,
          color: '#ffffff',
          weight: 2,
          fillColor: idx === 0 ? '#10b981' : '#f59e0b',
          fillOpacity: 1,
        }).addTo(map);
        routeMarkersRef.current.push(dot);
      });
    }
  }, [routePoints]);

  const handleCenterOnTarget = () => {
    mapRef.current?.setView([target.lat, target.lng], Math.max(mapRef.current.getZoom(), 15), {
      animate: true,
    });
  };

  const handleDropAtCenter = () => {
    const map = mapRef.current;
    if (!map) return;
    const c = map.getCenter();
    onPickPoint({
      lat: Number(c.lat.toFixed(6)),
      lng: Number(c.lng.toFixed(6)),
    });
  };

  const cycleMapStyle = () => {
    const order: MapStyle[] = ['osm', 'dark', 'satellite'];
    const next = order[(order.indexOf(mapStyle) + 1) % order.length];
    onChangeMapStyle(next);
  };

  return (
    <div className="relative w-full h-full">
      <div ref={containerRef} className="w-full h-full z-0" />

      {/* Subtle Center Crosshair */}
      <div className="pointer-events-none absolute inset-0 flex items-center justify-center z-10 opacity-45">
        <Crosshair className="w-7 h-7 text-sky-400 drop-shadow" />
      </div>

      {/* Tap Hint Pill */}
      <div className="pointer-events-none absolute top-3 left-1/2 -translate-x-1/2 z-10 px-3 py-1 rounded-full bg-slate-950/75 border border-slate-700/80 text-[11px] font-semibold text-slate-200 whitespace-nowrap backdrop-blur">
        {routeMode
          ? 'Нажимайте на карту, чтобы добавить точки маршрута'
          : 'Нажмите на карту или перетащите маркер, чтобы выбрать точку'}
      </div>

      {/* Right Map Controls */}
      <div className="absolute right-3 top-14 z-10 flex flex-col gap-2">
        <button
          type="button"
          onClick={cycleMapStyle}
          className="px-2.5 py-2 rounded-xl bg-slate-900/90 border border-slate-700 text-slate-100 hover:bg-slate-800 shadow-lg flex items-center gap-1.5 text-xs font-bold cursor-pointer backdrop-blur"
          title="Сменить слой карты"
        >
          <Layers className="w-4 h-4 text-sky-400" />
          <span>{TILE_LAYERS[mapStyle].label}</span>
        </button>

        <button
          type="button"
          onClick={handleDropAtCenter}
          className="px-2.5 py-2 rounded-xl bg-slate-900/90 border border-slate-700 text-slate-100 hover:bg-slate-800 shadow-lg flex items-center gap-1.5 text-xs font-bold cursor-pointer backdrop-blur"
          title="Поставить точку в центр прицела"
        >
          <Crosshair className="w-4 h-4 text-emerald-400" />
          <span>В центр</span>
        </button>

        <button
          type="button"
          onClick={handleCenterOnTarget}
          className="w-10 h-10 self-end rounded-xl bg-slate-900/90 border border-slate-700 text-slate-100 hover:bg-slate-800 shadow-lg flex items-center justify-center cursor-pointer backdrop-blur"
          title="Показать маркер"
        >
          <LocateFixed className="w-4 h-4 text-sky-400" />
        </button>

        <div className="self-end flex flex-col rounded-xl bg-slate-900/90 border border-slate-700 overflow-hidden shadow-lg backdrop-blur">
          <button
            type="button"
            onClick={() => mapRef.current?.zoomIn()}
            className="w-10 h-9 flex items-center justify-center text-slate-100 hover:bg-slate-800 border-b border-slate-700 cursor-pointer"
            title="Приблизить"
          >
            <Plus className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={() => mapRef.current?.zoomOut()}
            className="w-10 h-9 flex items-center justify-center text-slate-100 hover:bg-slate-800 cursor-pointer"
            title="Отдалить"
          >
            <Minus className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
};
