import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ForecastPlanningPage } from "./ForecastPlanningPage";
import { createForecastPlan } from "../services/simulationService";
import { getPorts } from "../services/portService";
import { getOpenWeatherForecast } from "../services/weatherService";
import { analyzeForecastRisk } from "../services/mlService";
import { getForecastEvaluation } from "../services/forecastEvaluationService";
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

vi.mock("../services/mlService", () => ({
  analyzeForecastRisk: vi.fn()
}));

vi.mock("../services/forecastEvaluationService", () => ({
  getForecastEvaluation: vi.fn()
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
  vi.mocked(getForecastEvaluation).mockResolvedValue({
    rows: [],
    summary: {
      avgRainMae: 1.2,
      avgRiskScoreError: 0.2,
      avgVisibilityMae: 0.8,
      avgWindMae: 1.4,
      confidenceLevel: "HIGH",
      confidencePct: 90,
      consecutiveMismatchCount: 0,
      dangerousUnderestimateCount: 0,
      eligiblePastPoints: 10,
      interventionMessage: "Dự báo đang khớp tốt với mức rủi ro thực tế; tiếp tục theo dõi định kỳ.",
      interventionRequired: false,
      matchRatePct: 100,
      matchedActualPoints: 10,
      recommendedActions: ["Tiếp tục đối chiếu hằng ngày."],
      riskMatchRatePct: 90,
      totalForecastPoints: 10
    }
  });
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
  vi.mocked(analyzeForecastRisk).mockResolvedValue({
    items: [{
      clusterId: 2,
      clusterLabel: "WIND_RISK",
      dominantFactors: ["WIND", "VISIBILITY"],
      mlRecommendation: "LIMITED",
      pcaRiskScore: 74,
      plannedAt: "2026-07-03T00:00:00Z",
      ruleRiskLevel: "MEDIUM"
    }],
    llmPlanAnalysis: {
      isConfigured: false,
      items: [{
        affectedOperations: ["Giám sát cầu bến"],
        operationMode: "NORMAL",
        planChange: "Ngày 1 vận hành bình thường, tăng giám sát gió.",
        plannedAt: "2026-07-03T00:00:00Z",
        reason: "Điểm AI ở mức HIGH nhưng chưa tới ngưỡng dừng.",
        recommendedActions: ["Theo dõi gió mỗi ca", "Chuẩn bị phương án hạn chế bốc xếp"]
      }],
      model: "local-operation-planner",
      portCode: "DNTSA",
      provider: "LOCAL_RULE_FALLBACK",
      summary: "Kế hoạch vận hành thay đổi theo xu hướng thời tiết."
    },
    modelVersion: "pca-kmeans-v1",
    portCode: "DNTSA"
  });
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
    expect(screen.getByText(/Thông tin thời tiết sắp tới giúp bạn chuẩn bị nhân sự/)).toBeInTheDocument();
    expect(await screen.findByRole("table", { name: "Dự báo thời tiết 5 ngày" })).toBeInTheDocument();
    expect(getOpenWeatherForecast).toHaveBeenCalledWith("DNTSA", 5);
    expect(screen.getByText("Mưa rào rải rác")).toBeInTheDocument();
    expect(screen.getByText("9.4 m/s")).toBeInTheDocument();
    expect(screen.getByText("6.2 km")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Cập nhật kế hoạch" }));

    expect(createForecastPlan).toHaveBeenCalledWith({ horizonDays: 5, portCode: "DNTSA" });
    expect(await screen.findByText("Kế hoạch dự báo DNTSA")).toBeInTheDocument();
    expect(screen.getByLabelText("Tiến trình phân tích dự báo")).toBeInTheDocument();
    await waitFor(() => expect(analyzeForecastRisk).toHaveBeenCalled(), { timeout: 3000 });
    expect(await screen.findByText("Điểm phân tích 74", {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getAllByText("Vận hành bình thường").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Hạn chế vận hành").length).toBeGreaterThan(0);
  });

  it("shows a five-day operation timeline and risk chart from forecast plan items", async () => {
    const user = userEvent.setup();
    vi.mocked(createForecastPlan).mockResolvedValue({
      dataset: {
        datasetId: "forecast-1",
        description: "Du bao 5 ngay",
        name: "Ke hoach du bao DNTSA",
        portCode: "DNTSA",
        snapshotCount: 5
      },
      generatedAt: "2026-07-02T00:00:00Z",
      horizonDays: 5,
      items: [
        {
          operationPlan: "NORMAL",
          plannedAt: "2026-07-03T00:00:00Z",
          rainRiskLevel: "LOW",
          riskLevel: "LOW",
          summary: "Troi quang, gio nhe",
          visibilityRiskLevel: "LOW",
          windRiskLevel: "LOW"
        },
        {
          operationPlan: "LIMITED",
          plannedAt: "2026-07-04T00:00:00Z",
          rainRiskLevel: "MEDIUM",
          riskLevel: "HIGH",
          summary: "Gio manh va mua",
          visibilityRiskLevel: "LOW",
          windRiskLevel: "HIGH"
        },
        {
          operationPlan: "STOP",
          plannedAt: "2026-07-05T00:00:00Z",
          rainRiskLevel: "CRITICAL",
          riskLevel: "CRITICAL",
          summary: "Mua lon, tam nhin giam",
          visibilityRiskLevel: "CRITICAL",
          windRiskLevel: "HIGH"
        }
      ],
      sourceObservedAt: "2026-07-02T00:00:00Z"
    });

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Cập nhật kế hoạch" }));

    expect(await screen.findByLabelText("Timeline dự báo vận hành 5 ngày")).toBeInTheDocument();
    expect(screen.getByLabelText("Biểu đồ rủi ro dự báo 5 ngày")).toBeInTheDocument();
    expect(await screen.findByLabelText("Phân tích mức rủi ro dự báo 5 ngày")).toBeInTheDocument();
    expect(screen.getByLabelText("Biểu đồ điểm rủi ro 5 ngày")).toBeInTheDocument();
    expect(screen.getByText("0-24 · Thấp")).toBeInTheDocument();
    expect(screen.getByText("25-49 · Cần lưu ý")).toBeInTheDocument();
    expect(screen.getByText("50-74 · Cao")).toBeInTheDocument();
    expect(screen.getByText("75-100 · Rất cao")).toBeInTheDocument();
    expect(screen.getByText("Gió mạnh cần lưu ý")).toBeInTheDocument();
    expect(screen.getByText("Điểm phân tích 74")).toBeInTheDocument();
    expect(screen.getByText("Mức Cao")).toBeInTheDocument();
    expect(screen.getByText("Theo quy tắc: Cần lưu ý")).toBeInTheDocument();
    expect(screen.getByText("Giải thích và phương án đề xuất")).toBeInTheDocument();
    expect(screen.getByText("Ngày 1 vận hành bình thường, tăng giám sát gió.")).toBeInTheDocument();
    expect(screen.getByText("Chuẩn bị phương án hạn chế bốc xếp")).toBeInTheDocument();
    expect(analyzeForecastRisk).toHaveBeenCalledWith(expect.objectContaining({
      portCode: "DNTSA"
    }));
    expect(screen.getByText("Troi quang, gio nhe")).toBeInTheDocument();
    expect(screen.getByText("Gio manh va mua")).toBeInTheDocument();
    expect(screen.getByText("Mua lon, tam nhin giam")).toBeInTheDocument();
    expect(screen.getAllByText("Vận hành bình thường").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Hạn chế vận hành").length).toBeGreaterThan(0);
    expect(screen.getByText("Tạm dừng vận hành")).toBeInTheDocument();
  });

  it("reloads the five-day forecast manually", async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(getOpenWeatherForecast).toHaveBeenCalledTimes(1));
    expect(screen.getByText(/Tự động cập nhật hằng ngày/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Làm mới dự báo" }));

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
