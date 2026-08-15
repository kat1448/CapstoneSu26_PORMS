import { useEffect, useMemo, useRef, useState } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { Link } from "react-router-dom";
import type { OperationMode, RiskLevel } from "../../types/dashboard";
import type { PortSummary, PortZone } from "../../types/port";
import { Badge } from "../common/Badge";

const OSM_TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const DEFAULT_CENTER: [number, number] = [16.1228, 108.2144];

type GisMapCardProps = {
  canManage?: boolean;
  onSelectPort: (portId: string) => void;
  onResetSelection: () => void;
  portName: string;
  ports: PortSummary[];
  selectedPortId: string;
  zones: PortZone[];
};

type MappablePort = PortSummary & {
  latitude: number;
  longitude: number;
};

type MappableZone = PortZone & {
  latitude: number;
  longitude: number;
};

type ZoneMapPoint = PortZone & {
  displayLatitude: number;
  displayLongitude: number;
  usesPortCoordinates: boolean;
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

const modeLabels: Record<OperationMode, string> = {
  LIMITED: "Hạn chế",
  NORMAL: "Bình thường",
  STOP: "Tạm dừng"
};

function hasPortCoordinates(port: PortSummary): port is MappablePort {
  return typeof port.latitude === "number" && typeof port.longitude === "number";
}

function hasZoneCoordinates(zone: PortZone): zone is MappableZone {
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

function createPortMarkerIcon(port: MappablePort, selectedPortId: string) {
  const selectedClass = port.portId === selectedPortId ? " is-selected" : "";
  const portName = escapeHtml(port.portName);
  const portCode = escapeHtml(port.portCode);
  return L.divIcon({
    className: "",
    html: `<div class="gis-marker gis-marker-port${selectedClass}" title="${portName}" style="background:${riskColors[port.currentRiskLevel]}"><span>${portCode}</span></div>`,
    iconAnchor: [80, 18],
    iconSize: [160, 36]
  });
}

function createZoneMarkerIcon(zone: MappableZone) {
  return L.divIcon({
    className: "",
    html: `<div class="gis-marker gis-marker-zone" style="background:${riskColors[zone.currentRiskLevel]}"></div>`,
    iconAnchor: [9, 9],
    iconSize: [18, 18]
  });
}

function portPopupContent(port: MappablePort) {
  const color = riskColors[port.currentRiskLevel];
  return [
    `<div class="gis-popup-title">${escapeHtml(port.portName)}</div>`,
    `<div class="gis-popup-meta">${escapeHtml(port.portCode)} · ${escapeHtml(modeLabels[port.currentOperationMode])}</div>`,
    `<div class="gis-popup-meta">Rủi ro: <strong style="color:${color}">${escapeHtml(port.currentRiskLevel)}</strong></div>`,
    `<div class="gis-popup-meta">Cảnh báo: ${port.activeAlertCount}</div>`,
    `<div class="gis-popup-meta">${port.latitude.toFixed(6)}, ${port.longitude.toFixed(6)}</div>`
  ].join("");
}

function createZoneMapPoints(zones: PortZone[], selectedPort?: MappablePort): ZoneMapPoint[] {
  if (!selectedPort) {
    return [];
  }

  const radius = 0.0012;
  return zones.flatMap<ZoneMapPoint>((zone, index) => {
    if (hasZoneCoordinates(zone)) {
      return [{
        ...zone,
        displayLatitude: zone.latitude,
        displayLongitude: zone.longitude,
        usesPortCoordinates: false
      }];
    }

    const angle = (Math.PI * 2 * index) / Math.max(zones.length, 1);
    return [{
      ...zone,
      displayLatitude: selectedPort.latitude + Math.sin(angle) * radius,
      displayLongitude: selectedPort.longitude + Math.cos(angle) * radius,
      usesPortCoordinates: true
    }];
  });
}

function zonePointPopupContent(zone: ZoneMapPoint) {
  const color = riskColors[zone.currentRiskLevel];
  const sourceLabel = zone.usesPortCoordinates ? "Theo toa do cang" : "Toa do khu vuc";
  return [
    `<div class="gis-popup-title">${escapeHtml(zone.zoneName)}</div>`,
    `<div class="gis-popup-meta">${escapeHtml(zone.zoneType)} · ${escapeHtml(zone.statusLabel)}</div>`,
    `<div class="gis-popup-meta">Rủi ro: <strong style="color:${color}">${escapeHtml(zone.currentRiskLevel)}</strong></div>`,
    `<div class="gis-popup-meta">${escapeHtml(sourceLabel)}</div>`,
    `<div class="gis-popup-meta">${zone.displayLatitude.toFixed(6)}, ${zone.displayLongitude.toFixed(6)}</div>`
  ].join("");
}

function coordinateLabel(zone: PortZone) {
  if (typeof zone.latitude !== "number" || typeof zone.longitude !== "number") {
    return "Chưa có tọa độ";
  }

  return `${zone.latitude.toFixed(6)}, ${zone.longitude.toFixed(6)}`;
}

function zoneDetailPath(zone: PortZone) {
  return `/ports/${zone.portId}?zoneId=${zone.zoneId}`;
}

export function GisMapCard({ canManage = false, onSelectPort, onResetSelection, portName, ports, selectedPortId, zones }: GisMapCardProps) {
  const mapElementRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const expandedMapElementRef = useRef<HTMLDivElement>(null);
  const expandedMapRef = useRef<L.Map | null>(null);
  const [isExpanded, setIsExpanded] = useState(false);
  const mappablePorts = useMemo(() => ports.filter(hasPortCoordinates), [ports]);
  const selectedMappablePort = useMemo(
    () => mappablePorts.find((port) => port.portId === selectedPortId),
    [mappablePorts, selectedPortId]
  );
  const visiblePorts = useMemo(
    () => selectedPortId ? mappablePorts.filter((port) => port.portId === selectedPortId) : mappablePorts,
    [mappablePorts, selectedPortId]
  );
  const zoneMapPoints = useMemo(
    () => createZoneMapPoints(zones, selectedMappablePort),
    [selectedMappablePort, zones]
  );

  function mountMap(element: HTMLDivElement) {
    const center = selectedMappablePort
      ? [selectedMappablePort.latitude, selectedMappablePort.longitude] as [number, number]
      : mappablePorts.length === 1
        ? [mappablePorts[0].latitude, mappablePorts[0].longitude] as [number, number]
        : DEFAULT_CENTER;
    const map = L.map(element).setView(center, 13);

    L.tileLayer(OSM_TILE_URL, {
      attribution: "",
      maxZoom: 18,
      minZoom: 8
    }).addTo(map);

    const points = [
      ...visiblePorts.map((port) => [port.latitude, port.longitude] as [number, number]),
      ...zoneMapPoints.map((zone) => [zone.displayLatitude, zone.displayLongitude] as [number, number])
    ];
    visiblePorts.forEach((port) => {
      const marker = L.marker([port.latitude, port.longitude], {
        icon: createPortMarkerIcon(port, selectedPortId)
      });
      marker
        .addTo(map)
        .bindPopup(portPopupContent(port));
      marker.on("click", () => onSelectPort(port.portId));
    });
    zoneMapPoints.forEach((zone) => {
      L.marker([zone.displayLatitude, zone.displayLongitude], {
        icon: createZoneMarkerIcon({
          ...zone,
          latitude: zone.displayLatitude,
          longitude: zone.displayLongitude
        })
      })
        .addTo(map)
        .bindPopup(zonePointPopupContent(zone));
    });

    if (points.length > 1) {
      map.fitBounds(L.latLngBounds(points), { padding: [24, 24] });
    }

    window.setTimeout(() => map.invalidateSize(), 0);

    return map;
  }

  useEffect(() => {
    const element = mapElementRef.current;
    if (!element) return undefined;

    if (mapRef.current) {
      mapRef.current.remove();
      mapRef.current = null;
    }

    const map = mountMap(element);
    mapRef.current = map;

    return () => {
      map.remove();
      if (mapRef.current === map) {
        mapRef.current = null;
      }
    };
  }, [mappablePorts, onSelectPort, selectedMappablePort, selectedPortId, zoneMapPoints]);

  useEffect(() => {
    const element = expandedMapElementRef.current;
    if (!isExpanded || !element) return undefined;

    if (expandedMapRef.current) {
      expandedMapRef.current.remove();
      expandedMapRef.current = null;
    }

    const map = mountMap(element);
    expandedMapRef.current = map;

    return () => {
      map.remove();
      if (expandedMapRef.current === map) {
        expandedMapRef.current = null;
      }
    };
  }, [isExpanded, mappablePorts, onSelectPort, selectedMappablePort, selectedPortId, zoneMapPoints]);

  return (
    <>
    <article className="card card-pad gis-card">
      <div className="card-head">
        <div>
          <h3>Bản đồ GIS {portName}</h3>
          <p>Tọa độ cảng và khu vực theo cảng đang chọn</p>
        </div>
        <div className="card-head-actions">
          <button
            type="button"
            className="button button-secondary button-small"
            onClick={onResetSelection}
            disabled={!selectedPortId}
          >
            Hiển thị tất cả cảng
          </button>
          <Badge tone={mappablePorts.length > 0 ? "info" : "muted"}>{mappablePorts.length} cảng</Badge>
        </div>
      </div>

      <div className="gis-map-shell">
        <div aria-label={`Bản đồ GIS ${portName}`} className="gis-map" ref={mapElementRef} role="application" />
        <div aria-label="Dieu khien ban do" className="map-expand-controls">
          <button aria-label="Mo rong ban do" className="map-expand-button" onClick={() => setIsExpanded(true)} type="button">⛶</button>
        </div>
        {mappablePorts.length === 0 ? (
          <div className="gis-empty" role="status">Chưa có tọa độ GIS cho các cảng.</div>
        ) : null}
      </div>

      <div className={`gis-data-table${canManage ? " has-actions" : " is-readonly"}`} aria-label="Bảng dữ liệu khu vực theo cảng">
        <div className="gis-data-row gis-data-head">
          <span>Khu vực</span>
          <span>Loại</span>
          <span>Rủi ro</span>
          <span>Trạng thái</span>
          <span>Tọa độ</span>
          {canManage ? <span>Thao tác</span> : null}
        </div>
        {zones.length === 0 ? (
          <div className="gis-data-row">
            <strong>Chưa có khu vực</strong>
            {canManage ? <span>-</span> : null}
            <span>-</span>
            <span>-</span>
            <span>-</span>
            <span>-</span>
          </div>
        ) : null}
        {zones.map((zone) => (
          <div className="gis-data-row" key={zone.zoneId}>
            <strong>{zone.zoneName}</strong>
            <span>{zone.zoneType}</span>
            <span><Badge tone={riskTones[zone.currentRiskLevel]}>{zone.currentRiskLevel}</Badge></span>
            <span>{zone.statusLabel}</span>
            <span>{coordinateLabel(zone)}{typeof zone.latitude !== "number" || typeof zone.longitude !== "number" ? " · Theo toa do cang" : ""}</span>
            {canManage ? (
              <span className="gis-row-actions">
                <Link className="button button-secondary button-small" to={`/ports/${zone.portId}`}>Chi tiết cảng</Link>
                <Link className="button button-secondary button-small" to={zoneDetailPath(zone)}>Chi tiết khu vực</Link>
              </span>
            ) : null}
          </div>
        ))}
      </div>
    </article>
    {isExpanded ? (
      <div className="map-modal-backdrop">
        <section aria-label="Ban do GIS mo rong" className="map-modal" role="dialog">
          <div className="map-modal-head">
            <div>
              <h3>Bản đồ GIS {portName}</h3>
              <p>Không gian bản đồ mở rộng</p>
            </div>
            <button aria-label="Dong ban do mo rong" className="map-modal-close" onClick={() => setIsExpanded(false)} type="button">×</button>
          </div>
          <div className="map-modal-body">
            <div aria-label={`Ban do GIS mo rong ${portName}`} className="map-modal-canvas" ref={expandedMapElementRef} role="application" />
            {mappablePorts.length === 0 ? (
              <div className="gis-empty" role="status">Chưa có tọa độ GIS cho các cảng.</div>
            ) : null}
          </div>
        </section>
      </div>
    ) : null}
    </>
  );
}
