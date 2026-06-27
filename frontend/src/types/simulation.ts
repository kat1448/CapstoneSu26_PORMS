import type { OperationMode, RiskLevel } from "./dashboard";

export type SimulationFeedItem = {
  detail: string;
  happenedAt: string;
  riskLevel: RiskLevel;
  title: string;
};

export type SimulationSnapshot = {
  beaufortNumber: number;
  currentMode: OperationMode;
  currentRiskLevel: RiskLevel;
  feed: SimulationFeedItem[];
  generatedAlertCount: number;
  rainfall1hMm: number;
  modeChangeCount: number;
  progressPercent: number;
  status: "IDLE" | "RUNNING" | "COMPLETED";
  visibilityKm: number;
  windSpeedMs: number;
};

export type SimulationDatasetSummary = {
  datasetId: string;
  description: string | null;
  name: string;
  portCode: string;
  snapshotCount: number;
};

export type CreateSimulationSnapshotInput = {
  beaufortNumber: number;
  rainfall1hMm: number;
  snapshotNumber: number;
  visibilityKm: number;
  windSpeedMs: number;
  zoneId?: string | null;
};

export type CreateSimulationDatasetInput = {
  description?: string | null;
  name: string;
  portCode: string;
  snapshots: CreateSimulationSnapshotInput[];
};

export type SimulationRunResult = {
  finalRiskLevel: RiskLevel;
  generatedTaskCount: number;
  sessionId: string;
};

export type SimulationMapPoint = {
  latitude: number;
  longitude: number;
  riskLevel: RiskLevel;
  zoneId: string;
  zoneName: string;
};

export type SimulationDangerousZone = {
  reason?: string | null;
  riskLevel: RiskLevel;
  zoneId: string;
  zoneName: string;
};

export type SimulationGeneratedTask = {
  priority: string;
  taskCode: string;
  title: string;
  zoneName?: string | null;
};

export type SimulationResult = {
  dangerousZones: SimulationDangerousZone[];
  mapPoints: SimulationMapPoint[];
  sessionId: string;
  tasks: SimulationGeneratedTask[];
};
