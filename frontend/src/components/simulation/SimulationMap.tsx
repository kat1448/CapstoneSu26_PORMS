import { useEffect, useMemo, useRef, useState } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import type { OperationMode, RiskLevel } from "../../types/dashboard";
import type { PortSummary, PortZone } from "../../types/port";
import type { SimulationMapPoint } from "../../types/simulation";

const OSM_TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const DEFAULT_CENTER: [number, number] = [16.1228, 108.2144];

type SimulationMapProps = {
  onResetSelection: () => void;
  onSelectPort: (portId: string) => void;
  points: SimulationMapPoint[];
  ports: PortSummary[];
  running: boolean;
  selectedPortId: string;
  zones: PortZone[];
};

type MappablePort = PortSummary & {
  latitude: number;
  longitude: number;
};

type DisplayPort = MappablePort & {
  displayRiskLevel: RiskLevel;
  riskSource: "live" | "simulation";
};

type ZoneMapPoint = PortZone & {
  displayLatitude: number;
  displayLongitude: number;
  displayRiskLevel: RiskLevel;
  usesPortCoordinates: boolean;
};

const riskColors: Record<RiskLevel, string> = {
  CRITICAL: "#d94848",
  HIGH: "#ee7623",
  LOW: "#19a66a",
  MEDIUM: "#e9a11b"
};

const riskScore: Record<RiskLevel, number> = {
  CRITICAL: 4,
  HIGH: 3,
  MEDIUM: 2,
  LOW: 1
};

const modeLabels: Record<OperationMode, string> = {
  LIMITED: "Han che",
  NORMAL: "Binh thuong",
  STOP: "Tam dung"
};

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function hasPortCoordinates(port: PortSummary): port is MappablePort {
  return typeof port.latitude === "number" && typeof port.longitude === "number";
}

function hasZoneCoordinates(zone: PortZone) {
  return typeof zone.latitude === "number" && typeof zone.longitude === "number";
}

function createPortIcon(port: DisplayPort, selectedPortId: string) {
  const selectedClass = port.portId === selectedPortId ? " is-selected" : "";
  const portName = escapeHtml(port.portName);
  const portCode = escapeHtml(port.portCode);
  return L.divIcon({
    className: "",
    html: `<div class="gis-marker gis-marker-port${selectedClass}" title="${portName}" style="background:${riskColors[port.displayRiskLevel]}"><span>${portCode}</span></div>`,
    iconAnchor: [80, 18],
    iconSize: [160, 36]
  });
}

function createZoneIcon(point: ZoneMapPoint, running: boolean) {
  const color = riskColors[point.displayRiskLevel] ?? riskColors.LOW;
  const label = escapeHtml(point.zoneName || "Diem mo phong");
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

function portPopupContent(port: DisplayPort) {
  const color = riskColors[port.displayRiskLevel];
  const riskLabel = port.riskSource === "simulation" ? "Rui ro mo phong" : "Rui ro";
  return [
    `<div class="gis-popup-title">${escapeHtml(port.portName)}</div>`,
    `<div class="gis-popup-meta">${escapeHtml(port.portCode)} · ${escapeHtml(modeLabels[port.currentOperationMode])}</div>`,
    `<div class="gis-popup-meta">${riskLabel}: <strong style="color:${color}">${escapeHtml(port.displayRiskLevel)}</strong></div>`,
    `<div class="gis-popup-meta">${port.latitude.toFixed(6)}, ${port.longitude.toFixed(6)}</div>`
  ].join("");
}

function zonePopupContent(point: ZoneMapPoint) {
  const sourceLabel = point.usesPortCoordinates ? "Theo toa do cang" : "Toa do khu vuc";
  return [
    `<div class="gis-popup-title">${escapeHtml(point.zoneName)}</div>`,
    `<div class="gis-popup-meta">${escapeHtml(point.zoneType)} · ${escapeHtml(point.statusLabel)}</div>`,
    `<div class="gis-popup-meta">Rui ro mo phong: <strong>${escapeHtml(point.displayRiskLevel)}</strong></div>`,
    `<div class="gis-popup-meta">${escapeHtml(sourceLabel)}</div>`,
    `<div class="gis-popup-meta">${point.displayLatitude.toFixed(6)}, ${point.displayLongitude.toFixed(6)}</div>`
  ].join("");
}

function higherRisk(left: RiskLevel, right: RiskLevel) {
  return riskScore[right] > riskScore[left] ? right : left;
}

function applySimulationRiskToPorts(ports: MappablePort[], points: SimulationMapPoint[]): DisplayPort[] {
  const riskByPortId = new Map<string, RiskLevel>();
  const riskByPortCode = new Map<string, RiskLevel>();

  points.forEach((point) => {
    if (point.portId) {
      const current = riskByPortId.get(point.portId);
      riskByPortId.set(point.portId, current ? higherRisk(current, point.riskLevel) : point.riskLevel);
    }

    if (point.portCode) {
      const key = point.portCode.toUpperCase();
      const current = riskByPortCode.get(key);
      riskByPortCode.set(key, current ? higherRisk(current, point.riskLevel) : point.riskLevel);
    }
  });

  return ports.map((port) => {
    const simulationRisk = riskByPortId.get(port.portId) ?? riskByPortCode.get(port.portCode.toUpperCase());
    return {
      ...port,
      displayRiskLevel: simulationRisk ?? port.currentRiskLevel,
      riskSource: simulationRisk ? "simulation" : "live"
    };
  });
}

function createZoneMapPoints(
  zones: PortZone[],
  selectedPort: MappablePort | undefined,
  simulationPoints: SimulationMapPoint[]
): ZoneMapPoint[] {
  const simulationPointByZoneId = new Map(simulationPoints.map((point) => [point.zoneId, point]));
  const radius = 0.0012;

  return zones.flatMap<ZoneMapPoint>((zone, index) => {
    const simulationPoint = simulationPointByZoneId.get(zone.zoneId);
    const displayRiskLevel = simulationPoint?.riskLevel ?? zone.currentRiskLevel;

    if (hasZoneCoordinates(zone)) {
      return [{
        ...zone,
        displayLatitude: zone.latitude as number,
        displayLongitude: zone.longitude as number,
        displayRiskLevel,
        usesPortCoordinates: false
      }];
    }

    if (!selectedPort) {
      return [];
    }

    const angle = (Math.PI * 2 * index) / Math.max(zones.length, 1);
    return [{
      ...zone,
      displayLatitude: selectedPort.latitude + Math.sin(angle) * radius,
      displayLongitude: selectedPort.longitude + Math.cos(angle) * radius,
      displayRiskLevel,
      usesPortCoordinates: true
    }];
  });
}

export function SimulationMap({
  onSelectPort,
  points,
  ports,
  running,
  selectedPortId,
  zones
}: SimulationMapProps) {
  const mapElementRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const markerLayerRef = useRef<L.LayerGroup | null>(null);
  const expandedMapElementRef = useRef<HTMLDivElement>(null);
  const expandedMapRef = useRef<L.Map | null>(null);
  const expandedMarkerLayerRef = useRef<L.LayerGroup | null>(null);
  const [isExpanded, setIsExpanded] = useState(false);
  const mappablePorts = useMemo(() => applySimulationRiskToPorts(ports.filter(hasPortCoordinates), points), [points, ports]);
  const selectedPort = useMemo(
    () => mappablePorts.find((port) => port.portId === selectedPortId),
    [mappablePorts, selectedPortId]
  );
  const visiblePorts = useMemo(
    () => selectedPortId ? mappablePorts.filter((port) => port.portId === selectedPortId) : mappablePorts,
    [mappablePorts, selectedPortId]
  );
  const zoneMapPoints = useMemo(
    () => selectedPortId ? createZoneMapPoints(zones, selectedPort, points) : [],
    [points, selectedPort, selectedPortId, zones]
  );

  function mountBaseMap(element: HTMLDivElement) {
    const map = L.map(element).setView(DEFAULT_CENTER, 11);
    const markerLayer = L.layerGroup().addTo(map);

    L.tileLayer(OSM_TILE_URL, {
      attribution: "",
      maxZoom: 18,
      minZoom: 8
    }).addTo(map);

    window.setTimeout(() => map.invalidateSize(), 0);

    return { map, markerLayer };
  }

  function syncMarkers(map: L.Map, markerLayer: L.LayerGroup) {
    markerLayer.clearLayers();

    const bounds = [
      ...visiblePorts.map((port) => [port.latitude, port.longitude] as [number, number]),
      ...zoneMapPoints.map((point) => [point.displayLatitude, point.displayLongitude] as [number, number])
    ];

    visiblePorts.forEach((port) => {
      const marker = L.marker([port.latitude, port.longitude], {
        icon: createPortIcon(port, selectedPortId)
      });
      marker
        .addTo(markerLayer)
        .bindPopup(portPopupContent(port));
      marker.on("click", () => onSelectPort(port.portId));
    });

    zoneMapPoints.forEach((point) => {
      L.marker([point.displayLatitude, point.displayLongitude], {
        icon: createZoneIcon(point, running)
      })
        .addTo(markerLayer)
        .bindPopup(zonePopupContent(point));
    });

    if (bounds.length > 1) {
      map.fitBounds(L.latLngBounds(bounds), { padding: [24, 24] });
    } else if (bounds.length === 1) {
      map.setView(bounds[0], 12);
    }
  }

  useEffect(() => {
    const element = mapElementRef.current;
    if (!element) return undefined;

    const mounted = mountBaseMap(element);
    mapRef.current = mounted.map;
    markerLayerRef.current = mounted.markerLayer;

    return () => {
      mounted.map.remove();
      mapRef.current = null;
      markerLayerRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    const markerLayer = markerLayerRef.current;
    if (map && markerLayer) {
      syncMarkers(map, markerLayer);
    }

    const expandedMap = expandedMapRef.current;
    const expandedMarkerLayer = expandedMarkerLayerRef.current;
    if (expandedMap && expandedMarkerLayer) {
      syncMarkers(expandedMap, expandedMarkerLayer);
    }
  }, [onSelectPort, running, selectedPortId, visiblePorts, zoneMapPoints]);

  useEffect(() => {
    const element = expandedMapElementRef.current;
    if (!isExpanded || !element) return undefined;

    const mounted = mountBaseMap(element);
    expandedMapRef.current = mounted.map;
    expandedMarkerLayerRef.current = mounted.markerLayer;
    syncMarkers(mounted.map, mounted.markerLayer);

    return () => {
      mounted.map.remove();
      expandedMapRef.current = null;
      expandedMarkerLayerRef.current = null;
    };
  }, [isExpanded, onSelectPort, running, selectedPortId, visiblePorts, zoneMapPoints]);

  return (
    <>
    <div className="simulation-map-shell" data-testid="simulation-map">
      <div aria-label="Bản đồ mô phỏng" className="simulation-map-canvas" ref={mapElementRef} role="application" />
      <div aria-label="Dieu khien ban do" className="map-expand-controls">
        <button aria-label="Mo rong ban do" className="map-expand-button" onClick={() => setIsExpanded(true)} type="button">⛶</button>
      </div>
      {mappablePorts.length === 0 ? (
        <div className="gis-empty" role="status">Chưa có tọa độ GIS cho các cảng.</div>
      ) : null}
    </div>
    {isExpanded ? (
      <div className="map-modal-backdrop">
        <section aria-label="Ban do mo phong mo rong" className="map-modal" role="dialog">
          <div className="map-modal-head">
            <div>
              <h3>Bản đồ mô phỏng</h3>
              <p>Không gian bản đồ mô phỏng mở rộng</p>
            </div>
            <button aria-label="Dong ban do mo rong" className="map-modal-close" onClick={() => setIsExpanded(false)} type="button">×</button>
          </div>
          <div className="map-modal-body">
            <div aria-label="Ban do mo phong mo rong" className="map-modal-canvas" ref={expandedMapElementRef} role="application" />
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
