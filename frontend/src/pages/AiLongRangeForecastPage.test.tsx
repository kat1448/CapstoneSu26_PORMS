import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getPorts } from "../services/portService";
import { getOpenWeatherForecast } from "../services/weatherService";
import { analyzeForecastRisk } from "../services/mlService";
import { AiLongRangeForecastPage } from "./AiLongRangeForecastPage";

vi.mock("@mui/x-charts", () => ({
  LineChart: (props: { "aria-label"?: string }) => (
    <div aria-label={props["aria-label"] ?? "line-chart"} />
  )
}));

vi.mock("../services/portService", () => ({
  getPorts: vi.fn()
}));

vi.mock("../services/weatherService", () => ({
  getOpenWeatherForecast: vi.fn()
}));

vi.mock("../services/mlService", () => ({
  analyzeForecastRisk: vi.fn()
}));

beforeEach(() => {
  vi.mocked(getPorts).mockResolvedValue([{
    activeAlertCount: 0,
    currentOperationMode: "NORMAL",
    currentRiskLevel: "LOW",
    isActive: true,
    latitude: 16.1,
    longitude: 108.2,
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cảng Tiên Sa",
    updatedAtLabel: "Vừa xong"
  }]);
  vi.mocked(getOpenWeatherForecast).mockResolvedValue({
    fetchedAt: "2026-07-17T00:00:00Z",
    portCode: "DNTSA",
    portName: "Cảng Tiên Sa",
    days: Array.from({ length: 5 }, (_, index) => ({
      date: `2026-07-${18 + index}T00:00:00Z`,
      humidityPct: 80,
      popPct: 35,
      pressureHpa: 1006,
      rainMm: 4 + index,
      summary: "Dự báo OpenWeather",
      temperatureDayC: 30,
      temperatureMaxC: 32,
      temperatureMinC: 27,
      visibilityKm: 9 - index * 0.5,
      weatherCode: 500,
      weatherDescription: "rain",
      windDirectionDeg: 100,
      windGustMs: 12 + index,
      windSpeedMs: 8 + index
    }))
  });
  vi.mocked(analyzeForecastRisk).mockImplementation(async (input) => ({
    items: input.items.map((item, index) => ({
      clusterId: index % 3,
      clusterLabel: index > 20 ? "LONG_RANGE_TREND" : "WEATHER_PATTERN",
      dominantFactors: ["WIND", "RAIN"],
      mlRecommendation: index > 45 ? "LIMITED" : "NORMAL",
      pcaRiskScore: Math.min(95, 20 + index),
      plannedAt: item.plannedAt,
      ruleRiskLevel: item.ruleRiskLevel
    })),
    llmPlanAnalysis: {
      isConfigured: false,
      items: [],
      model: "fallback",
      portCode: input.portCode,
      provider: "PORMS",
      summary: "AI dự đoán dài hạn để tham khảo lập kế hoạch."
    },
    modelVersion: "test-long-range",
    portCode: input.portCode
  }));
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("AiLongRangeForecastPage", () => {
  it("shows separated OpenWeather seed and AI long range prediction horizons", async () => {
    render(<AiLongRangeForecastPage />);

    expect(await screen.findByRole("heading", { level: 1, name: "AI dự đoán dài hạn" })).toBeInTheDocument();
    expect(screen.getByText("Dữ liệu OpenWeather 5 ngày")).toBeInTheDocument();
    expect(screen.getAllByText("AI dự đoán dài hạn")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "7 ngày" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "14 ngày" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "30 ngày" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "2 tháng" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "3 tháng" })).toBeInTheDocument();
    expect(screen.getByLabelText("Biểu đồ đường AI dự đoán dài hạn")).toBeInTheDocument();
  });

  it("rebuilds AI prediction for the selected 3 month horizon", async () => {
    const user = userEvent.setup();
    render(<AiLongRangeForecastPage />);

    await screen.findByRole("heading", { level: 1, name: "AI dự đoán dài hạn" });
    await user.click(screen.getByRole("button", { name: "3 tháng" }));

    await waitFor(() => {
      expect(analyzeForecastRisk).toHaveBeenLastCalledWith(expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ plannedAt: expect.any(String) })
        ]),
        portCode: "DNTSA"
      }));
    });
    expect(vi.mocked(analyzeForecastRisk).mock.lastCall?.[0].items).toHaveLength(90);
    expect(await screen.findByText("28-09")).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(91);
  });
});
