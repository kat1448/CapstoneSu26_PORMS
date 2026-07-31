import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { PortSummary, PortZone } from "../../types/port";
import type { SimulationMapPoint } from "../../types/simulation";
import { SimulationMap } from "./SimulationMap";

const leafletState = vi.hoisted(() => ({
  map: null as null | Record<string, ReturnType<typeof vi.fn>>,
  markers: [] as Array<{ click: (() => void) | null; coordinates: [number, number]; options: unknown; popup: string | null }>,
  tileLayers: [] as string[]
}));

vi.mock("leaflet", () => ({
  default: {
    divIcon: (options: unknown) => options,
    latLngBounds: (coordinates: Array<[number, number]>) => coordinates,
    layerGroup: () => ({
      addTo: vi.fn().mockReturnThis(),
      clearLayers: vi.fn()
    }),
    map: () => {
      const mapApi = {
        fitBounds: vi.fn(),
        invalidateSize: vi.fn(),
        remove: vi.fn(),
        setView: vi.fn().mockReturnThis()
      };
      leafletState.map = mapApi;
      return mapApi;
    },
    marker: (coordinates: [number, number], options: unknown) => {
      const entry = { click: null as (() => void) | null, coordinates, options, popup: null as string | null };
      leafletState.markers.push(entry);
      const markerApi = {
        addTo: vi.fn().mockReturnThis(),
        bindPopup: vi.fn((popup: string) => {
          entry.popup = popup;
          return markerApi;
        }),
        on: vi.fn((eventName: string, handler: () => void) => {
          if (eventName === "click") {
            entry.click = handler;
          }
          return markerApi;
        })
      };
      return markerApi;
    },
    tileLayer: (url: string) => {
      leafletState.tileLayers.push(url);
      return { addTo: vi.fn() };
    }
  }
}));

const ports: PortSummary[] = [
  {
    activeAlertCount: 1,
    currentOperationMode: "LIMITED",
    currentRiskLevel: "HIGH",
    isActive: true,
    latitude: 16.1228,
    longitude: 108.2144,
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cang Tien Sa",
    updatedAtLabel: "Vua cap nhat"
  },
  {
    activeAlertCount: 0,
    currentOperationMode: "NORMAL",
    currentRiskLevel: "LOW",
    isActive: true,
    latitude: 16.165,
    longitude: 108.1915,
    portCode: "DNLH",
    portId: "port-2",
    portName: "Cang Lien Chieu",
    updatedAtLabel: "5 phut truoc"
  }
];

const zones: PortZone[] = [
  {
    capacityLabel: "2 tau",
    currentRiskLevel: "LOW",
    displayOrder: 1,
    isActive: true,
    isRestricted: false,
    latitude: null,
    longitude: null,
    overrideEnabled: false,
    portId: "port-1",
    restrictionReason: null,
    statusLabel: "Binh thuong",
    zoneId: "zone-1",
    zoneName: "Ben so 1",
    zoneType: "DOCK"
  }
];

const points: SimulationMapPoint[] = [
  { latitude: 16.1, longitude: 108.2, portCode: "DNTSA", portId: "port-1", riskLevel: "CRITICAL", zoneId: "zone-1", zoneName: "Ben so 1" }
];

afterEach(() => {
  cleanup();
  leafletState.map = null;
  leafletState.markers = [];
  leafletState.tileLayers = [];
});

describe("SimulationMap", () => {
  it("renders port markers and selects a port when its marker is clicked", () => {
    const onSelectPort = vi.fn();

    render(
      <SimulationMap
        onResetSelection={vi.fn()}
        onSelectPort={onSelectPort}
        points={points}
        ports={ports}
        running={false}
        selectedPortId=""
        zones={[]}
      />
    );

    expect(leafletState.markers).toHaveLength(2);
    expect(leafletState.markers[0].popup).toContain("Cang Tien Sa");

    leafletState.markers[1].click?.();

    expect(onSelectPort).toHaveBeenCalledWith("port-2");
  });

  it("colors the selected port marker by simulated risk instead of live dashboard risk", () => {
    render(
      <SimulationMap
        onResetSelection={vi.fn()}
        onSelectPort={vi.fn()}
        points={points}
        ports={[{ ...ports[0], currentRiskLevel: "LOW" }]}
        running={false}
        selectedPortId=""
        zones={[]}
      />
    );

    expect(JSON.stringify(leafletState.markers[0].options)).toContain("#d94848");
    expect(leafletState.markers[0].popup).toContain("Rất cao");
  });

  it("renders selected port zones using simulation risk and port-coordinate fallback", () => {
    render(
      <SimulationMap
        onResetSelection={vi.fn()}
        onSelectPort={vi.fn()}
        points={points}
        ports={ports}
        running
        selectedPortId="port-1"
        zones={zones}
      />
    );

    expect(leafletState.markers).toHaveLength(2);
    expect(leafletState.markers[0].popup).toContain("Cang Tien Sa");
    expect(leafletState.markers.some((marker) => marker.popup?.includes("Cang Lien Chieu"))).toBe(false);
    expect(leafletState.markers[1].coordinates[1]).not.toBe(108.2144);
    expect(leafletState.markers[1].popup).toContain("Ben so 1");
    expect(leafletState.markers[1].popup).toContain("Rất cao");
    expect(leafletState.markers[1].popup).toContain("toa do cang");
  });

  it("opens the simulation map in a floating window from the bottom-left control", () => {
    render(
      <SimulationMap
        onResetSelection={vi.fn()}
        onSelectPort={vi.fn()}
        points={points}
        ports={ports}
        running={false}
        selectedPortId=""
        zones={[]}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Mo rong ban do" }));

    expect(screen.getByRole("dialog", { name: "Ban do mo phong mo rong" })).toBeInTheDocument();
    expect(screen.getByRole("application", { name: "Ban do mo phong mo rong" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Dong ban do mo rong" }));

    expect(screen.queryByRole("dialog", { name: "Ban do mo phong mo rong" })).not.toBeInTheDocument();
  });
});
