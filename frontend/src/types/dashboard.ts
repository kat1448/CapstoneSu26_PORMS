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
  beaufortNumber?: number | null;
  dataSource?: string | null;
  humidityPct: number;
  observedAt?: string | null;
  pressureHpa?: number | null;
  rainfall1hMm: number;
  recordedAt?: string | null;
  temperatureC: number;
  visibilityKm: number;
  weatherCode?: number | null;
  weatherDescription?: string | null;
  windDirectionDeg?: number | null;
  windGustMs?: number | null;
  windSpeedMs: number;
};

export type RiskTrendPoint = {
  hourLabel: string;
  riskScore: 1 | 2 | 3 | 4;
};
