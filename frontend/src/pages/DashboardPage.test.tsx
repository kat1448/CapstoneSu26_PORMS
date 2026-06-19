import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { DashboardPage } from "./DashboardPage";

vi.mock("../hooks/useDemoRefresh", () => ({ useDemoRefresh: () => 0 }));
vi.mock("../services/dashboardService", () => ({
  getDashboardSummary: async () => ({
    activeAlertCount: 1, beaufortNumber: 8, currentOperationMode: "LIMITED",
    currentRiskLevel: "HIGH", portCode: "DNTSA", portId: "port-1",
    portName: "Cảng Tiên Sa", rainfall1hMm: 28.5, visibilityKm: 4.2, windSpeedMs: 18.4
  }),
  getRiskTrend: async () => [
    { hourLabel: "00:00", riskScore: 1 }, { hourLabel: "06:00", riskScore: 2 },
    { hourLabel: "12:00", riskScore: 3 }, { hourLabel: "18:00", riskScore: 4 }
  ],
  getWeatherSnapshot: async () => ({
    humidityPct: 82, rainfall1hMm: 28.5, temperatureC: 29, visibilityKm: 4.2, windSpeedMs: 18.4
  })
}));
vi.mock("../services/alertService", () => ({
  getAlerts: async () => [{
    alertId: "a1", alertType: "WEATHER", createdAt: "2 phút trước", message: "Gió mạnh",
    portCode: "DNTSA", portId: "port-1", portName: "Cảng Tiên Sa", read: false,
    severity: "HIGH", title: "Cảnh báo gió mạnh", zoneName: "Bến số 1"
  }]
}));
vi.mock("../services/portService", () => ({
  getPortZones: async () => [{
    capacityLabel: "2 tàu", currentRiskLevel: "HIGH", displayOrder: 1, isActive: true,
    isRestricted: true, overrideEnabled: false, portId: "port-1", restrictionReason: "Gió mạnh",
    statusLabel: "Hạn chế", zoneId: "z1", zoneName: "Bến số 1", zoneType: "DOCK"
  }]
}));
vi.mock("../services/simulationService", () => ({ runDemoSimulation: async () => undefined }));

describe("DashboardPage", () => {
  it("matches the prototype section composition", async () => {
    render(<MemoryRouter><DashboardPage refreshKey={0} /></MemoryRouter>);

    expect(await screen.findByRole("heading", { name: "Trung tâm điều hành" })).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Mức rủi ro hiện tại");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Trạng thái khu vực");
    expect(screen.getByTestId("dashboard-right")).toHaveTextContent("Thời tiết hiện tại");
    expect(screen.getByTestId("dashboard-right")).toHaveTextContent("Cảnh báo đang hoạt động");
    expect(screen.queryByText("Nhật ký gần đây")).not.toBeInTheDocument();
  });
});
