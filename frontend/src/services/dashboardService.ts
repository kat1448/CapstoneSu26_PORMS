import {
  getDashboardSummary as getDashboardSummaryData,
  getWeatherSnapshot as getWeatherSnapshotData
} from "../mock/demoData";
import { requestJson, requestVoid, withMockFallback } from "./api";
import type { DashboardSummary, RiskTrendPoint, WeatherSnapshot } from "../types/dashboard";

export async function getDashboardSummary(): Promise<DashboardSummary> {
  return withMockFallback(
    () => requestJson<DashboardSummary>("/api/dashboard/summary"),
    () => getDashboardSummaryData()
  );
}

export async function getWeatherSnapshot(): Promise<WeatherSnapshot> {
  return withMockFallback(
    async () => {
      try {
        await requestVoid("/api/weather/refresh", { method: "POST" });
      } catch {
        // Keep dashboard usable with the latest DB snapshot if OpenWeather is temporarily unavailable.
      }

      return requestJson<WeatherSnapshot>("/api/weather/current");
    },
    () => getWeatherSnapshotData()
  );
}

export async function getRiskTrend(): Promise<RiskTrendPoint[]> {
  return requestJson<RiskTrendPoint[]>("/api/risk/trend");
}
