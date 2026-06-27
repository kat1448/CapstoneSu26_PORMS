import { useEffect, useMemo, useRef } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import type { RiskLevel } from "../../types/dashboard";
import type { PortZone } from "../../types/port";
import { Badge } from "../common/Badge";

const OSM_TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const TIEN_SA_CENTER: [number, number] = [16.1228, 108.2144];

type GisMapCardProps = {
  portName: string;
  zones: PortZone[];
};

type MappableZone = PortZone & {
  latitude: number;
  longitude: number;
};

const riskColors: Record<RiskLevel, string> = {
  CRITICAL: "#d94848",
  HIGH: "#ee7623",
  LOW: "#19a66a",
  MEDIUM: "#e9a11b"
};

const riskTones: Record<RiskLevel, "danger" | "info" | "success" | "warning"> = {
  CRITICAL: "danger",
  HIGH: "warning",
  LOW: "success",
  MEDIUM: "info"
};

function hasCoordinates(zone: PortZone): zone is MappableZone {
  return typeof zone.latitude === "number" && typeof zone.longitude === "number";
}

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function createMarkerIcon(riskLevel: RiskLevel, index: number) {
  return L.divIcon({
    className: "",
    html: `<div class="gis-marker" style="background:${riskColors[riskLevel]}">${index + 1}</div>`,
    iconAnchor: [18, 18],
    iconSize: [36, 36]
  });
}

function popupContent(zone: MappableZone) {
  const color = riskColors[zone.currentRiskLevel];
  return [
    `<div class="gis-popup-title">${escapeHtml(zone.zoneName)}</div>`,
    `<div class="gis-popup-meta">${escapeHtml(zone.zoneType)} · Trạng thái: ${escapeHtml(zone.statusLabel)}</div>`,
    `<div class="gis-popup-meta">Rủi ro: <strong style="color:${color}">${escapeHtml(zone.currentRiskLevel)}</strong></div>`,
    `<div class="gis-popup-meta">${zone.latitude.toFixed(6)}, ${zone.longitude.toFixed(6)}</div>`
  ].join("");
}

export function GisMapCard({ portName, zones }: GisMapCardProps) {
  const mapElementRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const mappableZones = useMemo(() => zones.filter(hasCoordinates), [zones]);

  useEffect(() => {
    const element = mapElementRef.current;
    if (!element) return undefined;

    if (mapRef.current) {
      mapRef.current.remove();
      mapRef.current = null;
    }

    const center = mappableZones.length === 1
      ? [mappableZones[0].latitude, mappableZones[0].longitude] as [number, number]
      : TIEN_SA_CENTER;
    const map = L.map(element).setView(center, 14);
    mapRef.current = map;

    L.tileLayer(OSM_TILE_URL, {
      attribution: "© OpenStreetMap",
      maxZoom: 18,
      minZoom: 11
    }).addTo(map);

    const points = mappableZones.map((zone) => [zone.latitude, zone.longitude] as [number, number]);
    mappableZones.forEach((zone, index) => {
      L.marker([zone.latitude, zone.longitude], {
        icon: createMarkerIcon(zone.currentRiskLevel, index)
      })
        .addTo(map)
        .bindPopup(popupContent(zone));
    });

    if (points.length > 1) {
      map.fitBounds(L.latLngBounds(points), { padding: [24, 24] });
    }

    window.setTimeout(() => map.invalidateSize(), 0);

    return () => {
      map.remove();
      if (mapRef.current === map) {
        mapRef.current = null;
      }
    };
  }, [mappableZones]);

  return (
    <article className="card card-pad gis-card">
      <div className="card-head">
        <div>
          <h3>Bản đồ GIS {portName}</h3>
          <p>Vị trí khu vực và mức rủi ro hiện tại · © OpenStreetMap</p>
        </div>
        <Badge tone={mappableZones.length > 0 ? "info" : "muted"}>{mappableZones.length} điểm</Badge>
      </div>

      <div className="gis-map-shell">
        <div aria-label={`Bản đồ GIS ${portName}`} className="gis-map" ref={mapElementRef} role="application" />
        {mappableZones.length === 0 ? (
          <div className="gis-empty" role="status">Chưa có tọa độ GIS cho các khu vực.</div>
        ) : null}
      </div>

      <div className="gis-data-table" aria-label="Bảng dữ liệu điểm GIS">
        <div className="gis-data-row gis-data-head">
          <span>Khu vực</span>
          <span>Loại</span>
          <span>Rủi ro</span>
          <span>Trạng thái</span>
          <span>Tọa độ</span>
        </div>
        {mappableZones.map((zone) => (
          <div className="gis-data-row" key={zone.zoneId}>
            <strong>{zone.zoneName}</strong>
            <span>{zone.zoneType}</span>
            <span><Badge tone={riskTones[zone.currentRiskLevel]}>{zone.currentRiskLevel}</Badge></span>
            <span>{zone.statusLabel}</span>
            <span>{zone.latitude.toFixed(6)}, {zone.longitude.toFixed(6)}</span>
          </div>
        ))}
      </div>
    </article>
  );
}
