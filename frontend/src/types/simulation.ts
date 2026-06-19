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
