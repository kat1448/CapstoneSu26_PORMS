import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getPorts } from "../services/portService";
import { getForecastEvaluation, getForecastInterventionDemo } from "../services/forecastEvaluationService";
import { ForecastEvaluationPage } from "./ForecastEvaluationPage";

vi.mock("@mui/x-charts", () => ({
  LineChart: (props: { "aria-label"?: string; series?: Array<{ label?: string }> }) => (
    <div aria-label={props["aria-label"] ?? "line-chart"}>
      {props.series?.map((series) => <span key={series.label}>{series.label}</span>)}
    </div>
  )
}));

vi.mock("../services/portService", () => ({
  getPorts: vi.fn()
}));

vi.mock("../services/forecastEvaluationService", () => ({
  exportForecastEvaluation: vi.fn(),
  getForecastEvaluation: vi.fn(),
  getForecastInterventionDemo: vi.fn()
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
  vi.mocked(getForecastEvaluation).mockResolvedValue({
    rows: [
      {
        actualObservedAt: "2026-07-01T00:10:00Z",
        actualRainfallMm: 2,
        actualRiskLevel: "LOW",
        actualVisibilityKm: 9,
        actualWindSpeedMs: 8,
        datasetName: "Dự báo 5 ngày",
        forecastRainfallMm: 3,
        forecastRiskLevel: "LOW",
        forecastVisibilityKm: 8,
        forecastWindSpeedMs: 9,
        plannedAt: "2026-07-01T00:00:00Z",
        portCode: "DNTSA",
        portName: "Cảng Tiên Sa",
        rainAbsError: 1,
        riskScoreError: 0,
        snapshotNumber: 1,
        status: "MATCHED",
        visibilityAbsError: 1,
        windAbsError: 1
      },
      {
        actualObservedAt: "2026-07-02T00:10:00Z",
        actualRainfallMm: 5,
        actualRiskLevel: "MEDIUM",
        actualVisibilityKm: 7,
        actualWindSpeedMs: 12,
        datasetName: "Dự báo 5 ngày",
        forecastRainfallMm: 8,
        forecastRiskLevel: "MEDIUM",
        forecastVisibilityKm: 6,
        forecastWindSpeedMs: 15,
        plannedAt: "2026-07-02T00:00:00Z",
        portCode: "DNTSA",
        portName: "Cảng Tiên Sa",
        rainAbsError: 3,
        riskScoreError: 0,
        snapshotNumber: 2,
        status: "MATCHED",
        visibilityAbsError: 1,
        windAbsError: 3
      }
    ],
    summary: {
      avgRainMae: 2,
      avgRiskScoreError: 0,
      avgVisibilityMae: 1,
      avgWindMae: 2,
      confidenceLevel: "INSUFFICIENT",
      confidencePct: 100,
      consecutiveMismatchCount: 0,
      dangerousUnderestimateCount: 0,
      eligiblePastPoints: 2,
      interventionMessage: "Mới có 2 mốc dữ liệu thật; cần ít nhất 3 mốc để kết luận độ tin cậy.",
      interventionRequired: false,
      matchRatePct: 100,
      matchedActualPoints: 2,
      recommendedActions: ["Tiếp tục thu thập dữ liệu thật."],
      riskMatchRatePct: 100,
      totalForecastPoints: 2
    }
  });
  vi.mocked(getForecastInterventionDemo).mockResolvedValue({
    dataNotice: "DỮ LIỆU MÔ PHỎNG - không tính vào độ tin cậy thực tế.",
    isDemonstration: true,
    rows: [],
    summary: {
      avgRainMae: 27,
      avgRiskScoreError: 1,
      avgVisibilityMae: 3,
      avgWindMae: 8,
      confidenceLevel: "LOW",
      confidencePct: 40,
      consecutiveMismatchCount: 3,
      dangerousUnderestimateCount: 3,
      eligiblePastPoints: 5,
      interventionMessage: "Cần rà soát vì dự báo sai liên tiếp.",
      interventionRequired: true,
      matchRatePct: 100,
      matchedActualPoints: 5,
      recommendedActions: ["Kiểm tra dữ liệu và ngưỡng rủi ro."],
      riskMatchRatePct: 40,
      totalForecastPoints: 5
    }
  });
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ForecastEvaluationPage", () => {
  it("shows a line chart comparing actual data and absolute error", async () => {
    render(<ForecastEvaluationPage />);

    expect(await screen.findByLabelText("Biểu đồ tương quan dữ liệu thật và sai số")).toBeInTheDocument();
    expect(screen.getByText("Gió thực tế (m/s)")).toBeInTheDocument();
    expect(screen.getByText("Sai số gió (m/s)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "1 tuần" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "1 tháng" })).toBeInTheDocument();
    expect(screen.getByLabelText("Độ tin cậy và khuyến nghị can thiệp")).toBeInTheDocument();
    expect(screen.getAllByText("100.0%").length).toBeGreaterThan(0);
  });

  it("reloads evaluation data when the user selects the one week range", async () => {
    const user = userEvent.setup();
    render(<ForecastEvaluationPage />);

    await screen.findByLabelText("Biểu đồ tương quan dữ liệu thật và sai số");
    await user.click(screen.getByRole("button", { name: "1 tuần" }));

    await waitFor(() => {
      expect(getForecastEvaluation).toHaveBeenCalledTimes(2);
    });
    expect(vi.mocked(getForecastEvaluation).mock.lastCall?.[0]).toEqual(expect.objectContaining({
      from: expect.stringMatching(/T00:00:00Z$/),
      to: expect.stringMatching(/T23:59:59Z$/)
    }));
  });

  it("runs the intervention demonstration without mixing it with official data", async () => {
    const user = userEvent.setup();
    render(<ForecastEvaluationPage />);

    await user.click(await screen.findByRole("button", { name: "Demo dự báo sai liên tiếp" }));
    expect(await screen.findByLabelText("Kịch bản mô phỏng can thiệp dự báo")).toBeInTheDocument();
    expect(screen.getByText("Dữ liệu mô phỏng")).toBeInTheDocument();
    expect(screen.getAllByText("40.0%").length).toBeGreaterThan(0);

    await user.click(screen.getByRole("button", { name: "Xác nhận rà soát" }));
    expect(screen.getByText("Đã xác nhận rà soát")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Áp dụng chế độ an toàn tạm thời" }));
    expect(screen.getByText("Đã áp dụng chế độ an toàn")).toBeInTheDocument();
    expect(screen.getByText(/Chế độ vận hành mô phỏng: Hạn chế vận hành/)).toBeInTheDocument();
  });
});
