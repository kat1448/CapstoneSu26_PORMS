import { requestJson } from "./api";
import type { ForecastRiskAnalysis, ForecastRiskAnalysisInput } from "../types/ml";

export function analyzeForecastRisk(input: ForecastRiskAnalysisInput): Promise<ForecastRiskAnalysis> {
  return requestJson<ForecastRiskAnalysis>("/api/ml/forecast-risk-analysis", {
    body: JSON.stringify(input),
    method: "POST"
  });
}
