import type { RiskLevel } from "./dashboard";

export type ForecastRiskAnalysisInputItem = {
  humidityPct?: number | null;
  plannedAt: string;
  pressureHpa?: number | null;
  rainRiskLevel: RiskLevel;
  rainfallMm?: number | null;
  ruleRiskLevel: RiskLevel;
  temperatureC?: number | null;
  visibilityKm?: number | null;
  visibilityRiskLevel: RiskLevel;
  windRiskLevel: RiskLevel;
  windSpeedMs?: number | null;
};

export type ForecastRiskAnalysisInput = {
  items: ForecastRiskAnalysisInputItem[];
  portCode: string;
};

export type ForecastRiskAnalysisItem = {
  clusterId: number;
  clusterLabel: string;
  dominantFactors: string[];
  mlRecommendation: "NORMAL" | "LIMITED" | "STOP";
  pcaRiskScore: number;
  plannedAt: string;
  ruleRiskLevel: RiskLevel;
};

export type OperationPlanAnalysisItem = {
  affectedOperations: string[];
  operationMode: "NORMAL" | "LIMITED" | "STOP";
  planChange: string;
  plannedAt: string;
  reason: string;
  recommendedActions: string[];
};

export type OperationPlanAnalysis = {
  isConfigured: boolean;
  items: OperationPlanAnalysisItem[];
  model: string;
  portCode: string;
  provider: string;
  summary: string;
};

export type ForecastRiskAnalysis = {
  items: ForecastRiskAnalysisItem[];
  llmPlanAnalysis?: OperationPlanAnalysis | null;
  modelVersion: string;
  portCode: string;
};
