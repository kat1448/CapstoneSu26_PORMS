import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { WeatherDataTable } from "./WeatherDataTable";
import type { WeatherSnapshot } from "../../types/dashboard";

const weather: WeatherSnapshot = {
  beaufortNumber: 8,
  dataSource: "OPENWEATHER_API",
  humidityPct: 82,
  observedAt: "2026-06-26T03:10:00Z",
  pressureHpa: 1008,
  rainfall1hMm: 28.5,
  recordedAt: "2026-06-26T03:11:30Z",
  temperatureC: 29,
  visibilityKm: 4.2,
  weatherCode: 500,
  weatherDescription: "moderate rain",
  windDirectionDeg: 110,
  windGustMs: 22.4,
  windSpeedMs: 18.4,
  dataPoints: [
    {
      beaufortNumber: 8,
      dataSource: "OPENWEATHER_API",
      latitude: 16.116235,
      longitude: 108.230378,
      observedAt: "2026-06-26T03:10:00Z",
      portCode: "DNTSA",
      portName: "Cảng Tiên Sa",
      rainfall1hMm: 28.5,
      recordedAt: "2026-06-26T03:11:30Z",
      temperatureC: 29,
      humidityPct: 82,
      visibilityKm: 4.2,
      weatherDescription: "moderate rain",
      windSpeedMs: 18.4,
      zoneName: "Bến số 1"
    }
  ]
};

describe("WeatherDataTable", () => {
  it("renders OpenWeather detail rows", () => {
    render(<WeatherDataTable weather={weather} />);

    expect(screen.getByRole("heading", { name: "Dữ liệu thời tiết theo cảng và khu vực" })).toBeInTheDocument();
    expect(screen.getByRole("table", { name: "Dữ liệu thời tiết theo cảng và khu vực" })).toBeInTheDocument();
    expect(screen.getByText("OPENWEATHER_API")).toBeInTheDocument();
    expect(screen.getByText("moderate rain")).toBeInTheDocument();
    expect(screen.getByText("18.4 m/s")).toBeInTheDocument();
    expect(screen.getByText("Beaufort 8")).toBeInTheDocument();
    expect(screen.getByText("82%")).toBeInTheDocument();
    expect(screen.getByText("26/06/2026 12:10:00")).toBeInTheDocument();
    expect(screen.getByText("26/06/2026 12:11:30")).toBeInTheDocument();
    expect(screen.getByText("Cảng Tiên Sa")).toBeInTheDocument();
    expect(screen.getByText("Bến số 1")).toBeInTheDocument();
    expect(screen.getByText("16.116235, 108.230378")).toBeInTheDocument();
  });

  it("shows placeholders for missing optional OpenWeather values", () => {
    render(
      <WeatherDataTable
        weather={{ ...weather, dataPoints: [], pressureHpa: null, weatherCode: null, weatherDescription: null, windGustMs: null }}
      />
    );

    expect(screen.getAllByText("Chưa có dữ liệu").length).toBeGreaterThanOrEqual(4);
    expect(screen.getByText("Toàn cảng")).toBeInTheDocument();
  });
});
