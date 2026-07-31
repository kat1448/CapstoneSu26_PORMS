import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

    expect(screen.getByRole("heading", { name: "Chi tiết thời tiết tại các khu vực" })).toBeInTheDocument();
    expect(screen.getByRole("table", { name: "Chi tiết thời tiết tại các khu vực" })).toBeInTheDocument();
    expect(screen.getByText("Dữ liệu thời tiết trực tuyến")).toBeInTheDocument();
    expect(screen.getByText("Mưa vừa")).toBeInTheDocument();
    expect(screen.getByText("18.4 m/s")).toBeInTheDocument();
    expect(screen.getByText("Cấp gió 8")).toBeInTheDocument();
    expect(screen.getByText("82%")).toBeInTheDocument();
    expect(screen.getByText("10:10:00 26/06/2026")).toBeInTheDocument();
    expect(screen.getByText("10:11:30 26/06/2026")).toBeInTheDocument();
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

  it("paginates weather points five rows at a time", async () => {
    const user = userEvent.setup();
    const points = Array.from({ length: 6 }, (_, index) => ({
      ...weather.dataPoints![0],
      observedAt: `2026-06-26T03:${String(index).padStart(2, "0")}:00Z`,
      portCode: `PORT${index + 1}`,
      portName: `Cang ${index + 1}`,
      zoneName: `Khu vuc ${index + 1}`
    }));

    render(<WeatherDataTable weather={{ ...weather, dataPoints: points }} />);

    expect(screen.getByText("Trang 1/2")).toBeInTheDocument();
    expect(screen.getByText("Khu vuc 1")).toBeInTheDocument();
    expect(screen.getByText("Khu vuc 5")).toBeInTheDocument();
    expect(screen.queryByText("Khu vuc 6")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Sau" }));

    expect(screen.getByText("Trang 2/2")).toBeInTheDocument();
    expect(screen.queryByText("Khu vuc 1")).not.toBeInTheDocument();
    expect(screen.getByText("Khu vuc 6")).toBeInTheDocument();
  });
});
