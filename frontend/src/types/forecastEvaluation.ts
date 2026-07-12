export type ForecastEvaluationSummary = {
  avgRainMae: number | null;
  avgRiskScoreError: number | null;
  avgVisibilityMae: number | null;
  avgWindMae: number | null;
  matchRatePct: number;
  matchedActualPoints: number;
  totalForecastPoints: number;
};

export type ForecastEvaluationRow = {
  actualObservedAt: string | null;
  actualRainfallMm: number | null;
  actualRiskLevel: string | null;
  actualVisibilityKm: number | null;
  actualWindSpeedMs: number | null;
  datasetName: string;
  forecastRainfallMm: number;
  forecastRiskLevel: string;
  forecastVisibilityKm: number | null;
  forecastWindSpeedMs: number;
  plannedAt: string;
  portCode: string;
  portName: string;
  rainAbsError: number | null;
  riskScoreError: number | null;
  snapshotNumber: number;
  status: "MATCHED" | "WAITING_ACTUAL";
  visibilityAbsError: number | null;
  windAbsError: number | null;
};

export type ForecastEvaluationResponse = {
  rows: ForecastEvaluationRow[];
  summary: ForecastEvaluationSummary;
};
