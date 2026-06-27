import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { GisMapCard } from "./GisMapCard";
import type { PortSummary, PortZone } from "../../types/port";

const leafletState = vi.hoisted(() => ({
  markers: [] as Array<{ click: (() => void) | null; coordinates: [number, number]; options: unknown; popup: string | null }>,
  tileLayers: [] as string[]
}));

vi.mock("leaflet", () => ({
  default: {
    divIcon: (options: unknown) => options,
    latLngBounds: (coordinates: Array<[number, number]>) => coordinates,
    map: () => ({
      fitBounds: vi.fn(),
      invalidateSize: vi.fn(),
      remove: vi.fn(),
      setView: vi.fn().mockReturnThis()
    }),
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
    activeAlertCount: 2,
    currentOperationMode: "LIMITED",
    currentRiskLevel: "HIGH",
    isActive: true,
    latitude: 16.124,
    longitude: 108.214,
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
    currentRiskLevel: "HIGH",
    displayOrder: 1,
    isActive: true,
    isRestricted: true,
    latitude: 16.124,
    longitude: 108.214,
    overrideEnabled: false,
    portId: "port-1",
    restrictionReason: "Gio manh",
    statusLabel: "Han che",
    zoneId: "zone-1",
    zoneName: "Ben so 1",
    zoneType: "DOCK"
  },
  {
    capacityLabel: "1200 TEU",
    currentRiskLevel: "MEDIUM",
    displayOrder: 2,
    isActive: true,
    isRestricted: false,
    latitude: 16.123,
    longitude: 108.216,
    overrideEnabled: false,
    portId: "port-1",
    restrictionReason: null,
    statusLabel: "Tang giam sat",
    zoneId: "zone-2",
    zoneName: "Bai container A",
    zoneType: "YARD"
  }
];

const zonesWithoutCoordinates = zones.map((zone) => ({ ...zone, latitude: null, longitude: null }));

afterEach(() => {
  cleanup();
  leafletState.markers = [];
  leafletState.tileLayers = [];
});

describe("GisMapCard", () => {
  it("renders OpenStreetMap markers for ports and the selected port zones", () => {
    render(<GisMapCard onSelectPort={vi.fn()} portName="Cang Tien Sa" ports={ports} selectedPortId="port-1" zones={zones} />);

    expect(screen.getByRole("heading", { name: /GIS Cang Tien Sa/ })).toBeInTheDocument();
    expect(screen.getByText("Ben so 1")).toBeInTheDocument();
    expect(screen.getByText("16.124000, 108.214000")).toBeInTheDocument();
    expect(leafletState.tileLayers).toContain("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png");
    expect(leafletState.markers).toHaveLength(4);
    expect(leafletState.markers[0].coordinates).toEqual([16.124, 108.214]);
    expect(leafletState.markers[0].popup).toContain("Cang Tien Sa");
    expect(leafletState.markers[2].coordinates).toEqual([16.124, 108.214]);
    expect(leafletState.markers[2].popup).toContain("Ben so 1");
  });

  it("selects a port when its map marker is clicked", () => {
    const onSelectPort = vi.fn();

    render(<GisMapCard onSelectPort={onSelectPort} portName="Cang Tien Sa" ports={ports} selectedPortId="port-1" zones={zones} />);

    leafletState.markers[1].click?.();

    expect(onSelectPort).toHaveBeenCalledWith("port-2");
  });

  it("places zones around the selected port when zone coordinates are missing", () => {
    render(<GisMapCard onSelectPort={vi.fn()} portName="Cang Tien Sa" ports={ports} selectedPortId="port-1" zones={zonesWithoutCoordinates} />);

    expect(leafletState.markers).toHaveLength(4);
    expect(leafletState.markers[2].coordinates[1]).not.toBe(108.214);
    expect(leafletState.markers[2].popup).toContain("Ben so 1");
    expect(leafletState.markers[2].popup).toContain("toa do cang");
    expect(screen.getAllByText(/Theo toa do cang/)).toHaveLength(2);
  });

  it("still renders the OpenStreetMap canvas and zone markers when no port has coordinates", () => {
    render(<GisMapCard onSelectPort={vi.fn()} portName="Cang Tien Sa" ports={[]} selectedPortId="port-1" zones={zones} />);

    expect(screen.getByRole("status")).toHaveTextContent("GIS");
    expect(screen.getByRole("application", { name: /GIS Cang Tien Sa/ })).toBeInTheDocument();
    expect(leafletState.tileLayers).toContain("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png");
    expect(leafletState.markers).toHaveLength(2);
    expect(leafletState.markers[0].popup).toContain("Ben so 1");
  });
});
