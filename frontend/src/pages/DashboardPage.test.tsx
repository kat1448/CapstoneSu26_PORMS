import { act, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { DashboardPage } from "./DashboardPage";

const adminUser = {
  email: "admin@porms.vn",
  initials: "AD",
  name: "Admin",
  portName: "Toàn hệ thống",
  role: "ADMIN" as const
};

const serviceMocks = vi.hoisted(() => ({
  getAlerts: vi.fn(),
  getDashboardSummary: vi.fn(),
  getPorts: vi.fn(),
  getPortZones: vi.fn(),
  getRiskTrend: vi.fn(),
  getWeatherSnapshot: vi.fn()
}));

vi.mock("../hooks/useDemoRefresh", () => ({ useDemoRefresh: () => 0 }));
vi.mock("../services/dashboardService", () => ({
  getDashboardSummary: serviceMocks.getDashboardSummary,
  getRiskTrend: serviceMocks.getRiskTrend,
  getWeatherSnapshot: serviceMocks.getWeatherSnapshot
}));
vi.mock("../services/alertService", () => ({
  getAlerts: serviceMocks.getAlerts
}));
vi.mock("../services/portService", () => ({
  getPorts: serviceMocks.getPorts,
  getPortZones: serviceMocks.getPortZones
}));
vi.mock("../components/dashboard/GisMapCard", () => ({
  GisMapCard: ({
    onSelectPort,
    ports,
    portName,
    selectedPortId,
    zones
  }: {
    onSelectPort: (portId: string) => void;
    ports: Array<{ latitude?: number | null; longitude?: number | null; portId: string; portName: string }>;
    portName: string;
    selectedPortId: string;
    zones: Array<{ latitude?: number | null; longitude?: number | null; zoneName: string }>;
  }) => (
    <article>
      <h3>Ban do GIS {portName}</h3>
      <div>Selected {selectedPortId}</div>
      {ports.map((port) => (
        <button key={port.portId} onClick={() => onSelectPort(port.portId)} type="button">
          {port.portName} {port.latitude}, {port.longitude}
        </button>
      ))}
      {zones.map((zone) => (
        <div key={zone.zoneName}>
          {zone.zoneName} {zone.latitude}, {zone.longitude}
        </div>
      ))}
    </article>
  )
}));

function mockDashboardServices() {
  serviceMocks.getDashboardSummary.mockResolvedValue({
    activeAlertCount: 1,
    beaufortNumber: 8,
    currentOperationMode: "LIMITED",
    currentRiskLevel: "HIGH",
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cang Tien Sa",
    rainfall1hMm: 28.5,
    visibilityKm: 4.2,
    windSpeedMs: 18.4
  });
  serviceMocks.getPorts.mockResolvedValue([
    {
      activeAlertCount: 1,
      currentOperationMode: "LIMITED",
      currentRiskLevel: "HIGH",
      isActive: true,
      latitude: 16.124,
      longitude: 108.214,
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cang Tien Sa",
      updatedAtLabel: "Vua cap nhat"
    },
    {
      activeAlertCount: 0,
      currentOperationMode: "NORMAL",
      currentRiskLevel: "LOW",
      isActive: true,
      latitude: 16.165,
      longitude: 108.1915,
      portCode: "DNLH",
      portId: "port-2",
      portName: "Cang Lien Chieu",
      updatedAtLabel: "5 phut truoc"
    }
  ]);
  serviceMocks.getRiskTrend.mockResolvedValue([
    { hourLabel: "00:00", riskScore: 1 },
    { hourLabel: "06:00", riskScore: 2 },
    { hourLabel: "12:00", riskScore: 3 },
    { hourLabel: "18:00", riskScore: 4 }
  ]);
  serviceMocks.getWeatherSnapshot.mockResolvedValue({
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
  });
  serviceMocks.getAlerts.mockResolvedValue([
    {
      alertId: "a1",
      alertType: "WEATHER",
      createdAt: "2 phut truoc",
      message: "Gio manh",
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cang Tien Sa",
      read: false,
      severity: "HIGH",
      title: "Canh bao gio manh",
      zoneName: "Ben so 1"
    }
  ]);
  serviceMocks.getPortZones.mockImplementation((portId: string) => Promise.resolve(portId === "port-1"
    ? [
        {
          capacityLabel: "2 tau",
          currentRiskLevel: "HIGH",
          displayOrder: 1,
          isActive: true,
          isRestricted: true,
          latitude: 16.124,
          longitude: 108.214,
          overrideEnabled: false,
          portId: "port-1",
          restrictionReason: "Gio manh",
          statusLabel: "Han che",
          zoneId: "z1",
          zoneName: "Ben so 1",
          zoneType: "DOCK"
        },
        {
          capacityLabel: "1 cong",
          currentRiskLevel: "LOW",
          displayOrder: 2,
          isActive: true,
          isRestricted: false,
          latitude: 16.126,
          longitude: 108.216,
          overrideEnabled: false,
          portId: "port-1",
          restrictionReason: null,
          statusLabel: "Binh thuong",
          zoneId: "z2",
          zoneName: "Cong A",
          zoneType: "GATE"
        }
      ]
    : [
        {
          capacityLabel: "3 tau",
          currentRiskLevel: "MEDIUM",
          displayOrder: 1,
          isActive: true,
          isRestricted: false,
          latitude: 16.165,
          longitude: 108.1915,
          overrideEnabled: false,
          portId: "port-2",
          restrictionReason: null,
          statusLabel: "Giam sat",
          zoneId: "z3",
          zoneName: "Ben so 2",
          zoneType: "DOCK"
        },
        {
          capacityLabel: "1 bai",
          currentRiskLevel: "CRITICAL",
          displayOrder: 2,
          isActive: true,
          isRestricted: true,
          latitude: 16.167,
          longitude: 108.193,
          overrideEnabled: false,
          portId: "port-2",
          restrictionReason: "Mua lon",
          statusLabel: "Dung",
          zoneId: "z4",
          zoneName: "Bai C",
          zoneType: "YARD"
        }
      ]));
}

async function flushDashboardLoad() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe("DashboardPage", () => {
  beforeEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
    mockDashboardServices();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("matches the prototype section composition", async () => {
    render(
      <MemoryRouter>
        <DashboardPage currentUser={adminUser} refreshKey={0} />
      </MemoryRouter>
    );

    expect(await screen.findByRole("heading", { name: "Trung tâm điều hành" })).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-left")).not.toHaveTextContent("Mức rủi ro hiện tại");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Tổng quan rủi ro khu vực");
    expect(screen.getByLabelText("LOW zones")).toHaveTextContent("1");
    expect(screen.getByLabelText("MEDIUM zones")).toHaveTextContent("1");
    expect(screen.getByLabelText("HIGH zones")).toHaveTextContent("1");
    expect(screen.getByLabelText("CRITICAL zones")).toHaveTextContent("1");
    expect(screen.getByLabelText("LOW zones")).not.toHaveTextContent("25%");
    expect(screen.getByLabelText("MEDIUM zones")).not.toHaveTextContent("25%");
    expect(screen.getByLabelText("HIGH zones")).not.toHaveTextContent("25%");
    expect(screen.getByLabelText("CRITICAL zones")).not.toHaveTextContent("25%");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Cảnh báo đang hoạt động");
    expect(screen.getByTestId("dashboard-left")).not.toHaveTextContent("Chế độ vận hành");
    expect(screen.getByTestId("dashboard-left")).not.toHaveTextContent("Trạng thái khu vực");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Ban do GIS Tất cả cảng");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Cang Lien Chieu 16.165, 108.1915");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Chi tiết thời tiết tại các khu vực");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Dữ liệu thời tiết trực tuyến");
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Mưa vừa");
    expect(screen.getByTestId("dashboard-left")).not.toHaveTextContent("Xu hướng rủi ro 24 giờ");
    expect(screen.queryByTestId("dashboard-right")).not.toBeInTheDocument();
    expect(screen.getByTestId("dashboard-left")).not.toHaveTextContent("Thời tiết hiện tại");
    expect(screen.queryByRole("button", { name: /Chạy mô phỏng demo|Đang chạy mô phỏng/ })).not.toBeInTheDocument();
    expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Cảnh báo đang hoạt động");
    expect(screen.queryByText("Nháº­t kÃ½ gáº§n Ä‘Ã¢y")).not.toBeInTheDocument();
    expect(serviceMocks.getRiskTrend).not.toHaveBeenCalled();
  });

  it("refreshes dashboard data every 10 minutes", async () => {
    vi.useFakeTimers();
    render(
      <MemoryRouter>
        <DashboardPage currentUser={adminUser} refreshKey={0} />
      </MemoryRouter>
    );

    await flushDashboardLoad();
    expect(serviceMocks.getDashboardSummary).toHaveBeenCalledTimes(1);
    expect(serviceMocks.getWeatherSnapshot).toHaveBeenCalledTimes(1);
    expect(serviceMocks.getRiskTrend).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(600_000);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(serviceMocks.getDashboardSummary).toHaveBeenCalledTimes(2);
    expect(serviceMocks.getWeatherSnapshot).toHaveBeenCalledTimes(2);
    expect(serviceMocks.getRiskTrend).not.toHaveBeenCalled();
    expect(serviceMocks.getPorts).toHaveBeenCalledTimes(2);
    expect(serviceMocks.getPortZones).toHaveBeenCalledTimes(4);
  });
});
