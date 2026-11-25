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
  waypoints: LatLng[];
  roadPath: LatLng[];
  onPickPoint: (point: LatLng) => void;
  followTarget?: boolean;
}

const TILE_LAYERS: Record<MapStyle, string> = {
  dark: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
  osm: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
  satellite: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
};

function createPinIcon(isSpoofing: boolean): L.DivIcon {
  const color = isSpoofing ? '#10b981' : '#38bdf8';
  const ring = isSpoofing
    ? `<span class="gps-pulse-ring" style="position:absolute;inset:-9px;border-radius:9999px;border:2.5px solid ${color};"></span>`
    : '';
  return L.divIcon({
    className: 'fakegps-pin',
    html: `
      <div style="position:relative;width:26px;height:26px;display:flex;align-items:center;justify-content:center;">
        ${ring}
        <span style="width:18px;height:18px;border-radius:9999px;background:${color};border:3px solid #ffffff;box-shadow:0 4px 14px rgba(0,0,0,0.45);"></span>
      </div>
    `,
    iconSize: [26, 26],
    iconAnchor: [13, 13],
  });
}

export const MapCanvas: React.FC<MapCanvasProps> = ({
  target,
  isSpoofing,
  accuracyMeters,
  mapStyle,
  onChangeMapStyle,
  waypoints,
  roadPath,
  onPickPoint,
  followTarget = false,
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<L.Map | null>(null);
  const tileRef = useRef<L.TileLayer | null>(null);
  const markerRef = useRef<L.Marker | null>(null);
  const circleRef = useRef<L.Circle | null>(null);
  const roadLineRef = useRef<L.Polyline | null>(null);
  const waypointMarkersRef = useRef<L.CircleMarker[]>([]);

  const onPickRef = useRef(onPickPoint);
  onPickRef.current = onPickPoint;

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    const map = L.map(containerRef.current, {
      center: [target.lat, target.lng],
      zoom: 15,
      zoomControl: false,
      attributionControl: false,
    });
    mapRef.current = map;

    tileRef.current = L.tileLayer(TILE_LAYERS[mapStyle], { maxZoom: 19 }).addTo(map);

    circleRef.current = L.circle([target.lat, target.lng], {
      radius: Math.max(8, accuracyMeters),
      color: isSpoofing ? '#10b981' : '#38bdf8',
      fillColor: isSpoofing ? '#10b981' : '#38bdf8',
      fillOpacity: 0.14,
      weight: 1.5,
    }).addTo(map);

    const marker = L.marker([target.lat, target.lng], {
      icon: createPinIcon(isSpoofing),
      draggable: true,
    }).addTo(map);
    markerRef.current = marker;

    marker.on('dragend', () => {
      const pos = marker.getLatLng();
      onPickRef.current({
        lat: Number(pos.lat.toFixed(6)),
        lng: Number(pos.lng.toFixed(6)),
      });
    });

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

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    if (tileRef.current) map.removeLayer(tileRef.current);
    tileRef.current = L.tileLayer(TILE_LAYERS[mapStyle], { maxZoom: 19 }).addTo(map);
  }, [mapStyle]);

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

  // Draw real street-snapped road path + waypoint dots
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    if (roadLineRef.current) {
      map.removeLayer(roadLineRef.current);
      roadLineRef.current = null;
    }
    waypointMarkersRef.current.forEach((m) => map.removeLayer(m));
    waypointMarkersRef.current = [];

    const lineCoords = roadPath.length >= 2 ? roadPath : waypoints;
    if (lineCoords.length >= 2) {
      roadLineRef.current = L.polyline(
        lineCoords.map((p) => [p.lat, p.lng] as [number, number]),
        {
          color: '#38bdf8',
          weight: 5,
          opacity: 0.9,
          lineJoin: 'round',
          lineCap: 'round',
        },
      ).addTo(map);
    }

    waypoints.forEach((p, idx) => {
      const dot = L.circleMarker([p.lat, p.lng], {
        radius: 6,
        color: '#ffffff',
        weight: 2,
        fillColor: idx === 0 ? '#10b981' : idx === waypoints.length - 1 ? '#f43f5e' : '#f59e0b',
        fillOpacity: 1,
      }).addTo(map);
      waypointMarkersRef.current.push(dot);
    });
  }, [waypoints, roadPath]);

  const cycleStyle = () => {
    const order: MapStyle[] = ['dark', 'osm', 'satellite'];
    onChangeMapStyle(order[(order.indexOf(mapStyle) + 1) % order.length]);
  };

  return (
    <div className="relative w-full h-full">
      <div ref={containerRef} className="w-full h-full z-0" />

      {/* Minimalist floating right controls */}
      <div className="absolute right-3 top-20 z-20 flex flex-col gap-2">
        <button
          type="button"
          onClick={cycleStyle}
          className="w-10 h-10 rounded-2xl bg-slate-950/80 border border-white/15 text-slate-100 hover:bg-slate-900 shadow-xl flex items-center justify-center cursor-pointer backdrop-blur-xl"
          title="Слой карты"
        >
          <Layers className="w-4 h-4 text-sky-400" />
        </button>

        <button
          type="button"
          onClick={() => {
            const c = mapRef.current?.getCenter();
            if (c) onPickPoint({ lat: Number(c.lat.toFixed(6)), lng: Number(c.lng.toFixed(6)) });
          }}
          className="w-10 h-10 rounded-2xl bg-slate-950/80 border border-white/15 text-slate-100 hover:bg-slate-900 shadow-xl flex items-center justify-center cursor-pointer backdrop-blur-xl"
          title="Точка в центр"
        >
          <Crosshair className="w-4 h-4 text-emerald-400" />
        </button>

        <button
          type="button"
          onClick={() =>
            mapRef.current?.setView([target.lat, target.lng], Math.max(mapRef.current.getZoom(), 15), {
              animate: true,
            })
          }
          className="w-10 h-10 rounded-2xl bg-slate-950/80 border border-white/15 text-slate-100 hover:bg-slate-900 shadow-xl flex items-center justify-center cursor-pointer backdrop-blur-xl"
          title="К маркеру"
        >
          <LocateFixed className="w-4 h-4 text-sky-400" />
        </button>

        <div className="flex flex-col rounded-2xl bg-slate-950/80 border border-white/15 overflow-hidden shadow-xl backdrop-blur-xl">
          <button
            type="button"
            onClick={() => mapRef.current?.zoomIn()}
            className="w-10 h-9 flex items-center justify-center text-slate-100 hover:bg-slate-900 border-b border-white/10 cursor-pointer"
          >
            <Plus className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={() => mapRef.current?.zoomOut()}
            className="w-10 h-9 flex items-center justify-center text-slate-100 hover:bg-slate-900 cursor-pointer"
          >
            <Minus className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
};
