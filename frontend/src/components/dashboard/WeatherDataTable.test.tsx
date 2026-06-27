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
  windSpeedMs: 18.4
};

describe("WeatherDataTable", () => {
  it("renders OpenWeather detail rows", () => {
    render(<WeatherDataTable weather={weather} />);

    expect(screen.getByRole("heading", { name: "Bảng dữ liệu OpenWeather" })).toBeInTheDocument();
    expect(screen.getByText("OPENWEATHER_API")).toBeInTheDocument();
    expect(screen.getByText("moderate rain · 500")).toBeInTheDocument();
    expect(screen.getByText("18.4 m/s")).toBeInTheDocument();
    expect(screen.getByText("22.4 m/s")).toBeInTheDocument();
    expect(screen.getByText("110°")).toBeInTheDocument();
    expect(screen.getByText("1008 hPa")).toBeInTheDocument();
    expect(screen.getByText("26/06/2026 10:10:00")).toBeInTheDocument();
    expect(screen.getByText("26/06/2026 10:11:30")).toBeInTheDocument();
  });

  it("shows placeholders for missing optional OpenWeather values", () => {
    render(
      <WeatherDataTable
        weather={{ ...weather, pressureHpa: null, weatherCode: null, weatherDescription: null, windGustMs: null }}
      />
    );

    expect(screen.getAllByText("Chưa có dữ liệu").length).toBeGreaterThanOrEqual(3);
  });
});
