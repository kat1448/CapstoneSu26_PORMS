import {
  getDashboardSummary as getDashboardSummaryData,
  getWeatherSnapshot as getWeatherSnapshotData
} from "../mock/demoData";
import { requestJson, withMockFallback } from "./api";
import type { DashboardSummary, RiskTrendPoint, WeatherSnapshot } from "../types/dashboard";

export async function getDashboardSummary(): Promise<DashboardSummary> {
  return withMockFallback(
    () => requestJson<DashboardSummary>("/api/dashboard/summary"),
    () => getDashboardSummaryData()
  );
}

export async function getWeatherSnapshot(): Promise<WeatherSnapshot> {
  return withMockFallback(
    () => requestJson<WeatherSnapshot>("/api/weather/current"),
    () => getWeatherSnapshotData()
  );
}

export async function getRiskTrend(): Promise<RiskTrendPoint[]> {
  return requestJson<RiskTrendPoint[]>("/api/risk/trend");
}
