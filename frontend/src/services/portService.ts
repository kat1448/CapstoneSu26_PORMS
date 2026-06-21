import { getPortZones as getPortZonesData, getPorts as getPortsData } from "../mock/demoData";
import { requestJson, withMockFallback } from "./api";
import type { PortSummary, PortZone } from "../types/port";

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
