import { getPortZones as getPortZonesData, getPorts as getPortsData, updatePort as updatePortData } from "../mock/demoData";
import { requestJson, requestVoid, withMockFallback } from "./api";
import type { PortSummary, PortZone } from "../types/port";

export type CreateZoneInput = {
  capacityUnit?: string | null;
  capacityValue?: number | null;
  displayOrder: number;
  latitude?: number | null;
  longitude?: number | null;
  name: string;
  zoneType: PortZone["zoneType"];
};

export type CreatePortInput = {
  address?: string | null;
  code: string;
  isActive: boolean;
  latitude: number;
  longitude: number;
  name: string;
  timezone: string;
  weatherSource: string;
  weatherStationId?: string | null;
  zones: CreateZoneInput[];
};

export type UpdateZoneInput = CreateZoneInput & {
  isActive: boolean;
};

export type UpdatePortInput = {
  address?: string | null;
  code: string;
  isActive: boolean;
  latitude: number;
  longitude: number;
  name: string;
  timezone: string;
  weatherSource: string;
  weatherStationId?: string | null;
};

export async function getPorts(): Promise<PortSummary[]> {
  return withMockFallback(
    () => requestJson<PortSummary[]>("/api/ports"),
    () => getPortsData()
  );
}

export async function getPortZones(portId: string): Promise<PortZone[]> {
  return withMockFallback(
    () => requestJson<PortZone[]>(`/api/ports/${portId}/zones`),
    () => getPortZonesData(portId)
  );
}

export async function createPort(input: CreatePortInput): Promise<PortSummary> {
  return requestJson<PortSummary>("/api/ports", {
    body: JSON.stringify(input),
    method: "POST"
  });
}

export async function updatePort(portId: string, input: UpdatePortInput): Promise<PortSummary> {
  return withMockFallback(
    () => requestJson<PortSummary>(`/api/ports/${portId}`, {
      body: JSON.stringify(input),
      method: "PUT"
    }),
    () => updatePortData(portId, input)
  );
}

export async function updatePortZone(portId: string, zoneId: string, input: UpdateZoneInput): Promise<PortZone> {
  return requestJson<PortZone>(`/api/ports/${portId}/zones/${zoneId}`, {
    body: JSON.stringify(input),
    method: "PUT"
  });
}

export async function deletePortZone(portId: string, zoneId: string): Promise<void> {
  return requestVoid(`/api/ports/${portId}/zones/${zoneId}`, { method: "DELETE" });
}
