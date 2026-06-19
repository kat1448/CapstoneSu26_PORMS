export type RiskLevel = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type OperationMode = "NORMAL" | "LIMITED" | "STOP";

export type DashboardSummary = {
  activeAlertCount: number;
  beaufortNumber: number | null;
  currentOperationMode: OperationMode;
  currentRiskLevel: RiskLevel;
  portCode: string;
  portId: string;
  portName: string;
  rainfall1hMm: number | null;
  visibilityKm: number | null;
  windSpeedMs: number | null;
};

export type WeatherSnapshot = {
  humidityPct: number;
  rainfall1hMm: number;
  temperatureC: number;
  visibilityKm: number;
  windSpeedMs: number;
};

export type RiskTrendPoint = {
  hourLabel: string;
  riskScore: 1 | 2 | 3 | 4;
};
