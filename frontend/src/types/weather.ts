export type OpenWeatherForecastDay = {
  date: string;
  humidityPct: number;
  popPct: number;
  pressureHpa: number | null;
  rainMm: number;
  summary: string | null;
  temperatureDayC: number;
  temperatureMaxC: number;
  temperatureMinC: number;
  weatherCode: number | null;
  weatherDescription: string | null;
  windDirectionDeg: number | null;
  windGustMs: number | null;
  windSpeedMs: number;
};

export type OpenWeatherForecast = {
  days: OpenWeatherForecastDay[];
  fetchedAt: string;
  portCode: string;
  portName: string;
};
