import { requestJson } from "./api";
import type { OpenWeatherForecast } from "../types/weather";

export function getOpenWeatherForecast(portCode: string, days = 5): Promise<OpenWeatherForecast> {
  const query = new URLSearchParams({
    days: String(days),
    portCode
  });

  return requestJson<OpenWeatherForecast>(`/api/weather/forecast?${query.toString()}`);
}
