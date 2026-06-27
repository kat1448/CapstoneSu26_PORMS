import { useEffect, useMemo, useRef } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import type { SimulationMapPoint } from "../../types/simulation";

const OSM_TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const DEFAULT_CENTER: [number, number] = [16.1228, 108.2144];

type SimulationMapProps = {
  points: SimulationMapPoint[];
  running: boolean;
};

const riskColors: Record<string, string> = {
  CRITICAL: "#d94848",
  HIGH: "#ee7623",
  LOW: "#19a66a",
  MEDIUM: "#e9a11b"
};

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function createIcon(point: SimulationMapPoint, running: boolean) {
  const color = riskColors[point.riskLevel] ?? riskColors.LOW;
  const label = escapeHtml(point.zoneName || "Điểm mô phỏng");
  return L.divIcon({
    className: "",
    html: `<div class="simulation-map-marker-shell ${running ? "is-running" : ""}" style="--risk-color:${color}">
      <div class="simulation-map-marker"><span></span></div>
      <div class="simulation-map-marker-label">${label}</div>
    </div>`,
    iconAnchor: [60, 18],
    iconSize: [120, 56]
  });
}

function popupContent(point: SimulationMapPoint) {
  return [
    `<div class="gis-popup-title">${point.zoneName}</div>`,
    `<div class="gis-popup-meta">Rủi ro: <strong>${point.riskLevel}</strong></div>`,
    `<div class="gis-popup-meta">${point.latitude.toFixed(6)}, ${point.longitude.toFixed(6)}</div>`
  ].join("");
}

export function SimulationMap({ points, running }: SimulationMapProps) {
  const mapElementRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const markerLayerRef = useRef<L.LayerGroup | null>(null);
  const safePoints = useMemo(
    () => points.filter((point) => Number.isFinite(point.latitude) && Number.isFinite(point.longitude)),
    [points]
  );

  useEffect(() => {
    const element = mapElementRef.current;
    if (!element) return undefined;

    const map = L.map(element).setView(DEFAULT_CENTER, 14);
    mapRef.current = map;
    markerLayerRef.current = L.layerGroup().addTo(map);

    L.tileLayer(OSM_TILE_URL, {
      attribution: "",
      maxZoom: 18,
      minZoom: 11
    }).addTo(map);

    window.setTimeout(() => map.invalidateSize(), 0);

    return () => {
      map.remove();
      mapRef.current = null;
      markerLayerRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    const markerLayer = markerLayerRef.current;
    if (!map || !markerLayer) return;

    markerLayer.clearLayers();
    const bounds = safePoints.map((point) => [point.latitude, point.longitude] as [number, number]);

    safePoints.forEach((point) => {
      L.marker([point.latitude, point.longitude], {
        icon: createIcon(point, running)
      }).addTo(markerLayer).bindPopup(popupContent(point));
    });

    if (bounds.length > 1) {
      map.fitBounds(L.latLngBounds(bounds), { padding: [24, 24] });
    } else if (bounds.length === 1) {
      map.setView(bounds[0], 14);
    }
  }, [running, safePoints]);

  return (
    <div className="simulation-map-shell" data-testid="simulation-map">
      <div aria-label="Bản đồ mô phỏng" className="simulation-map-canvas" ref={mapElementRef} role="application" />
      {safePoints.length === 0 ? (
        <div className="gis-empty" role="status">Chưa có điểm mô phỏng trên bản đồ.</div>
      ) : null}
    </div>
  );
}
