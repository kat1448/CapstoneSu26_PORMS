import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AnalyticsPage } from "./AnalyticsPage";

vi.mock("../services/dashboardService", () => ({
  getDashboardSummary: vi.fn(async () => ({
    activeAlertCount: 3,
    beaufortNumber: 7,
    currentOperationMode: "LIMITED",
    currentRiskLevel: "HIGH",
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cang Tien Sa",
    rainfall1hMm: 18.5,
    visibilityKm: 6.2,
    windSpeedMs: 15.8
  })),
  getRiskTrend: vi.fn(async () => [
    { hourLabel: "08:00", riskScore: 2 },
    { hourLabel: "09:00", riskScore: 3 },
    { hourLabel: "10:00", riskScore: 4 }
  ]),
  getWeatherSnapshot: vi.fn(async () => ({
    beaufortNumber: 7,
    humidityPct: 82,
    rainfall1hMm: 18.5,
    temperatureC: 29.2,
    visibilityKm: 6.2,
    windSpeedMs: 15.8
  }))
}));

vi.mock("../services/portService", () => ({
  getPortZones: vi.fn(async (portId: string) => portId === "port-1"
    ? [
      {
        capacityLabel: "2 tau",
        currentRiskLevel: "HIGH",
        displayOrder: 1,
        isActive: true,
        isRestricted: true,
        overrideEnabled: false,
        portId: "port-1",
        restrictionReason: "Gio manh",
        statusLabel: "Han che",
        zoneId: "zone-1",
        zoneName: "Ben so 1",
        zoneType: "DOCK"
      },
      {
        capacityLabel: "1200 TEU",
        currentRiskLevel: "MEDIUM",
        displayOrder: 2,
        isActive: true,
        isRestricted: false,
        overrideEnabled: false,
        portId: "port-1",
        restrictionReason: null,
        statusLabel: "Tang giam sat",
        zoneId: "zone-2",
        zoneName: "Bai container A",
        zoneType: "YARD"
      }
    ]
    : [
      {
        capacityLabel: "1 cong",
        currentRiskLevel: "CRITICAL",
        displayOrder: 1,
        isActive: true,
        isRestricted: true,
        overrideEnabled: true,
        portId: "port-2",
        restrictionReason: "Tam dung",
        statusLabel: "Tam dung",
        zoneId: "zone-3",
        zoneName: "Cong chinh",
        zoneType: "GATE"
      }
    ]),
  getPorts: vi.fn(async () => [
    {
      activeAlertCount: 2,
      currentOperationMode: "LIMITED",
      currentRiskLevel: "HIGH",
      isActive: true,
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cang Tien Sa",
      updatedAtLabel: "Vua cap nhat"
    },
    {
      activeAlertCount: 1,
      currentOperationMode: "STOP",
      currentRiskLevel: "CRITICAL",
      isActive: true,
      portCode: "DNLH",
      portId: "port-2",
      portName: "Cang Lien Chieu",
      updatedAtLabel: "5 phut truoc"
    }
  ])
}));

vi.mock("../services/alertService", () => ({
  getAlerts: vi.fn(async () => [
    {
      alertId: "alert-1",
      alertType: "WEATHER",
      createdAt: "Vua xong",
      message: "Gio tang",
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cang Tien Sa",
      read: false,
      severity: "HIGH",
      title: "Gio manh",
      zoneName: "Ben so 1"
    },
    {
      alertId: "alert-2",
      alertType: "RISK",
      createdAt: "5 phut truoc",
      message: "Tam dung",
      portCode: "DNLH",
      portId: "port-2",
      portName: "Cang Lien Chieu",
      read: false,
      severity: "CRITICAL",
      title: "Rui ro rat cao",
      zoneName: "Cong chinh"
    }
  ])
}));

vi.mock("../services/simulationService", () => ({
  getSimulationSnapshot: vi.fn(async () => ({
    beaufortNumber: 8,
    currentMode: "LIMITED",
    currentRiskLevel: "HIGH",
    feed: [],
    generatedAlertCount: 2,
    modeChangeCount: 1,
    progressPercent: 100,
    rainfall1hMm: 22,
    status: "COMPLETED",
    visibilityKm: 5,
    windSpeedMs: 18
  }))
}));

describe("AnalyticsPage", () => {
  it("renders BI layout from operational data sources", async () => {
    render(<AnalyticsPage />);

    expect(screen.getByText("Đang tải dữ liệu BI...")).toBeInTheDocument();

    await waitFor(() => expect(screen.getByRole("heading", { name: "Phân tích BI" })).toBeInTheDocument());

    expect(screen.getByLabelText("Bộ lọc BI")).toBeInTheDocument();
    expect(screen.getByText("Tổng số cảng")).toBeInTheDocument();
    expect(screen.getByText("Khu vuc HIGH/CRITICAL")).toBeInTheDocument();
    expect(screen.getByText("Cảnh báo đang mở")).toBeInTheDocument();
    expect(screen.getByText("Xu hướng rủi ro theo thời gian")).toBeInTheDocument();
    expect(screen.getByTestId("bi-risk-trend-chart")).toBeInTheDocument();
    expect(screen.getByText("Phân bố rủi ro theo cảng")).toBeInTheDocument();
    expect(screen.getAllByText("Cang Lien Chieu").length).toBeGreaterThan(0);
    expect(screen.getByText("Khu vực cần chú ý")).toBeInTheDocument();
    expect(screen.getAllByText("Cong chinh").length).toBeGreaterThan(0);
    expect(screen.getByText("Insight nghiệp vụ")).toBeInTheDocument();
    expect(document.querySelectorAll(".bi-card-pad").length).toBeGreaterThan(0);
  });
});
