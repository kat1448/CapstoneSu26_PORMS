import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ForecastPlanningPage } from "./ForecastPlanningPage";
import { createForecastPlan } from "../services/simulationService";
import { getPorts } from "../services/portService";
import { getOpenWeatherForecast } from "../services/weatherService";
import type { OpenWeatherForecast } from "../types/weather";

vi.mock("../services/simulationService", () => ({
  createForecastPlan: vi.fn()
}));

vi.mock("../services/weatherService", () => ({
  getOpenWeatherForecast: vi.fn()
}));

vi.mock("../services/portService", () => ({
  getPorts: vi.fn()
}));

const forecast: OpenWeatherForecast = {
  days: [
    {
      date: "2026-07-03T00:00:00Z",
      humidityPct: 78,
      popPct: 65,
      pressureHpa: 1007,
      rainMm: 12.5,
      summary: "Mưa rào rải rác",
      temperatureDayC: 29,
      temperatureMaxC: 31,
      temperatureMinC: 25,
      weatherCode: 500,
      weatherDescription: "mưa vừa",
      visibilityKm: 6.2,
      windDirectionDeg: 110,
      windGustMs: 14.2,
      windSpeedMs: 9.4
    }
  ],
  fetchedAt: "2026-07-02T00:00:00Z",
  portCode: "DNTSA",
  portName: "Cảng Tiên Sa"
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  vi.useRealTimers();
});

beforeEach(() => {
  vi.mocked(getPorts).mockResolvedValue([
    {
      activeAlertCount: 1,
      currentOperationMode: "LIMITED",
      currentRiskLevel: "HIGH",
      isActive: true,
      latitude: 16.12,
      longitude: 108.22,
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cảng Tiên Sa",
      updatedAtLabel: "Hôm nay"
    }
  ]);
  vi.mocked(getOpenWeatherForecast).mockResolvedValue(forecast);
});

function renderPage() {
  render(
    <MemoryRouter>
      <ForecastPlanningPage />
    </MemoryRouter>
  );
}

describe("ForecastPlanningPage", () => {
  it("creates future planning data from OpenWeather in its own page", async () => {
    const user = userEvent.setup();
    vi.mocked(createForecastPlan).mockResolvedValue({
      dataset: {
        datasetId: "forecast-1",
        description: "Dự báo 5 ngày",
        name: "Kế hoạch dự báo DNTSA",
        portCode: "DNTSA",
        snapshotCount: 5
      },
      generatedAt: "2026-07-02T00:00:00Z",
      horizonDays: 5,
      items: [{
        operationPlan: "Lập lịch linh hoạt",
        plannedAt: "2026-07-03T00:00:00Z",
        rainRiskLevel: "LOW",
        riskLevel: "MEDIUM",
        summary: "Dự báo ngày mai",
        visibilityRiskLevel: "LOW",
        windRiskLevel: "MEDIUM"
      }],
      sourceObservedAt: "2026-07-02T00:00:00Z"
    });

    renderPage();

    await screen.findByRole("heading", { name: "Dự báo vận hành" });
    expect(screen.getByText(/Dữ liệu dự đoán tương lai từ OpenWeather API/)).toBeInTheDocument();
    expect(await screen.findByRole("table", { name: "Dự báo OpenWeather 5 ngày" })).toBeInTheDocument();
    expect(getOpenWeatherForecast).toHaveBeenCalledWith("DNTSA", 5);
    expect(screen.getByText("Mưa rào rải rác")).toBeInTheDocument();
    expect(screen.getByText("9.4 m/s")).toBeInTheDocument();
    expect(screen.getByText("6.2 km")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Cập nhật kế hoạch từ OpenWeather" }));

    expect(createForecastPlan).toHaveBeenCalledWith({ horizonDays: 5, portCode: "DNTSA" });
    expect(await screen.findByText("Kế hoạch dự báo DNTSA")).toBeInTheDocument();
    expect(screen.getByText("Lập lịch linh hoạt")).toBeInTheDocument();
  });

  it("reloads the five-day forecast manually", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(getOpenWeatherForecast).toHaveBeenCalledTimes(1));
    expect(screen.getByText(/Tự động cập nhật hằng ngày/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Reload dự báo" }));

    await waitFor(() => expect(getOpenWeatherForecast).toHaveBeenCalledTimes(2));
  });

  it("automatically refreshes the five-day forecast once per day", async () => {
    vi.useFakeTimers();
    renderPage();

    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(getOpenWeatherForecast).toHaveBeenCalledTimes(1);

    await act(async () => {
      vi.advanceTimersByTime(24 * 60 * 60 * 1000);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(getOpenWeatherForecast).toHaveBeenCalledTimes(2);
  });
});
