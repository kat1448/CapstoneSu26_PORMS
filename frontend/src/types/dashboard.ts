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
  dataPoints?: WeatherDataPoint[];
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

export type WeatherDataPoint = {
  beaufortNumber: number;
  dataSource?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  observedAt?: string | null;
  portCode: string;
  portName: string;
  rainfall1hMm: number;
  recordedAt?: string | null;
  temperatureC: number;
  humidityPct?: number | null;
  visibilityKm: number;
  weatherDescription?: string | null;
  windSpeedMs: number;
  zoneName?: string | null;
};

export type RiskTrendPoint = {
  hourLabel: string;
  riskScore: 1 | 2 | 3 | 4;
};
