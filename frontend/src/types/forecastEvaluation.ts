export type ForecastEvaluationSummary = {
  avgRainMae: number | null;
  avgRiskScoreError: number | null;
  avgVisibilityMae: number | null;
  avgWindMae: number | null;
  confidenceLevel: "HIGH" | "INSUFFICIENT" | "LOW" | "MEDIUM";
  confidencePct: number | null;
  consecutiveMismatchCount: number;
  dangerousUnderestimateCount: number;
  eligiblePastPoints: number;
  interventionMessage: string;
  interventionRequired: boolean;
  matchRatePct: number;
  matchedActualPoints: number;
  recommendedActions: string[];
  riskMatchRatePct: number;
  totalForecastPoints: number;
  horizonConfidence?: ForecastHorizonConfidence[];
};

export type ForecastHorizonConfidence = {
  avgRainMae: number | null;
  avgVisibilityMae: number | null;
  avgWindMae: number | null;
  confidenceLevel: "HIGH" | "INSUFFICIENT" | "LOW" | "MEDIUM";
  confidencePct: number | null;
  horizonDay: number;
  sampleCount: number;
};

export type ForecastEvaluationRow = {
  actualObservedAt: string | null;
  actualDataSource?: string | null;
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
  status: "FUTURE" | "MATCHED" | "MATCHED_DEMO" | "WAITING_ACTUAL";
  visibilityAbsError: number | null;
  windAbsError: number | null;
};

export type ForecastEvaluationResponse = {
  dataNotice?: string | null;
  isDemonstration?: boolean;
  rows: ForecastEvaluationRow[];
  summary: ForecastEvaluationSummary;
};
