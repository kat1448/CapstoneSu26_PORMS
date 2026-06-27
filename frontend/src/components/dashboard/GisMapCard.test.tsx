import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { GisMapCard } from "./GisMapCard";
import type { PortZone } from "../../types/port";

const leafletState = vi.hoisted(() => ({
  markers: [] as Array<{ coordinates: [number, number]; options: unknown; popup: string | null }>,
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
      const entry = { coordinates, options, popup: null as string | null };
      leafletState.markers.push(entry);
      return {
        addTo: vi.fn().mockReturnThis(),
        bindPopup: vi.fn((popup: string) => {
          entry.popup = popup;
          return entry;
        })
      };
    },
    tileLayer: (url: string) => {
      leafletState.tileLayers.push(url);
      return { addTo: vi.fn() };
    }
  }
}));

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

afterEach(() => {
  cleanup();
  leafletState.markers = [];
  leafletState.tileLayers = [];
});

describe("GisMapCard", () => {
  it("renders OpenStreetMap markers and a data table for coordinate-bearing zones", () => {
    render(<GisMapCard portName="Cang Tien Sa" zones={zones} />);

    expect(screen.getByRole("heading", { name: "Bản đồ GIS Cang Tien Sa" })).toBeInTheDocument();
    expect(screen.getByText("Ben so 1")).toBeInTheDocument();
    expect(screen.getByText("16.124000, 108.214000")).toBeInTheDocument();
    expect(leafletState.tileLayers).toContain("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png");
    expect(leafletState.markers).toHaveLength(2);
    expect(leafletState.markers[0].coordinates).toEqual([16.124, 108.214]);
    expect(leafletState.markers[0].popup).toContain("Ben so 1");
  });

  it("still renders the OpenStreetMap canvas when no zone has coordinates", () => {
    const zonesWithoutCoordinates = zones.map((zone) => ({ ...zone, latitude: null, longitude: null }));

    render(<GisMapCard portName="Cang Tien Sa" zones={zonesWithoutCoordinates} />);

    expect(screen.getByText("Chưa có tọa độ GIS cho các khu vực.")).toBeInTheDocument();
    expect(screen.getByRole("application", { name: "Bản đồ GIS Cang Tien Sa" })).toBeInTheDocument();
    expect(leafletState.tileLayers).toContain("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png");
    expect(leafletState.markers).toHaveLength(0);
  });
});
