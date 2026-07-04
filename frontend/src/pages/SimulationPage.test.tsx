import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SimulationPage } from "./SimulationPage";
import {
  createSimulationDataset,
  deleteSimulationDataset,
  getSimulationDataset,
  getSimulationDatasets,
  getSimulationMapPoints,
  getSimulationResult,
  getSimulationSnapshot,
  runDemoSimulation,
  runSimulationDataset,
  updateSimulationDataset
} from "../services/simulationService";
import { getPorts, getPortZones } from "../services/portService";
import type { SimulationSnapshot } from "../types/simulation";

vi.mock("../hooks/useDemoRefresh", () => ({
  useDemoRefresh: () => 0
}));

vi.mock("../services/simulationService", () => ({
  createSimulationDataset: vi.fn(),
  deleteSimulationDataset: vi.fn(),
  getSimulationDataset: vi.fn(),
  getSimulationDatasets: vi.fn(),
  getSimulationMapPoints: vi.fn(),
  getSimulationResult: vi.fn(),
  getSimulationSnapshot: vi.fn(),
  runDemoSimulation: vi.fn(),
  runSimulationDataset: vi.fn(),
  updateSimulationDataset: vi.fn()
}));

vi.mock("../services/portService", () => ({
  getPorts: vi.fn(),
  getPortZones: vi.fn()
}));

const snapshot: SimulationSnapshot = {
  beaufortNumber: 8,
  currentMode: "LIMITED",
  currentRiskLevel: "HIGH",
  feed: [{
    detail: "Gio manh tai ben so 1",
    happenedAt: "08:42",
    riskLevel: "HIGH",
    title: "Kich hoat SOP gio manh"
  }],
  generatedAlertCount: 2,
  modeChangeCount: 1,
  progressPercent: 65,
  rainfall1hMm: 28,
  status: "RUNNING",
  visibilityKm: 4.2,
  windSpeedMs: 18.4
};

vi.mock("leaflet", () => ({
  default: {
    divIcon: (options: unknown) => options,
    latLngBounds: (coordinates: Array<[number, number]>) => coordinates,
    map: () => ({
      fitBounds: vi.fn(),
      invalidateSize: vi.fn(),
      remove: vi.fn(),
      setView: vi.fn().mockReturnThis()
    }),
    marker: () => ({
      addTo: vi.fn().mockReturnThis(),
      bindPopup: vi.fn().mockReturnThis()
    }),
    tileLayer: () => ({ addTo: vi.fn() })
  }
}));

vi.mock("../components/simulation/SimulationMap", () => ({
  SimulationMap: ({ points }: { points: Array<{ zoneName: string }> }) => (
    <div data-point-count={points.length} data-testid="simulation-map">
      {points.map((point) => <span key={point.zoneName}>{point.zoneName}</span>)}
    </div>
  )
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

beforeEach(() => {
  vi.mocked(getSimulationDatasets).mockResolvedValue([
    { datasetId: "dataset-1", description: "Bao lon", name: "Bao Da Nang", portCode: "DNTSA", snapshotCount: 2 },
    { datasetId: "dataset-2", description: "Mua lon", name: "Mua cuc bo", portCode: "DNTSA", snapshotCount: 1 }
  ]);
  vi.mocked(getSimulationMapPoints).mockResolvedValue([
    { latitude: 16.116235, longitude: 108.230378, riskLevel: "LOW", zoneId: "zone-1", zoneName: "acc1" },
    { latitude: 16.216235, longitude: 108.130378, riskLevel: "LOW", zoneId: "zone-2", zoneName: "acc2" }
  ]);
  vi.mocked(getSimulationResult).mockResolvedValue({
    dangerousZones: [{ riskLevel: "CRITICAL", reason: "Gio cap 10", zoneId: "zone-1", zoneName: "Ben so 1" }],
    mapPoints: [{ latitude: 16.1, longitude: 108.2, riskLevel: "CRITICAL", zoneId: "zone-1", zoneName: "Ben so 1" }],
    sessionId: "session-1",
    tasks: [{ priority: "HIGH", taskCode: "TASK-1", title: "Dung boc xep", zoneName: "Ben so 1" }]
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
      portName: "Cang Tien Sa",
      updatedAtLabel: "Hom nay"
    }
  ]);
  vi.mocked(getPortZones).mockResolvedValue([
    {
      capacityLabel: "2 tau",
      currentRiskLevel: "LOW",
      displayOrder: 1,
      isActive: true,
      isRestricted: false,
      latitude: 16.12,
      longitude: 108.22,
      overrideEnabled: false,
      portId: "port-1",
      restrictionReason: null,
      statusLabel: "Binh thuong",
      zoneId: "zone-dock-1",
      zoneName: "Ben so 1",
      zoneType: "DOCK"
    }
  ]);
});

describe("SimulationPage", () => {
  it("renders the rebuilt responsive simulation layout", async () => {
    vi.mocked(getSimulationSnapshot).mockResolvedValue(snapshot);

    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByRole("heading", { name: "Chế độ mô phỏng" });

    expect(screen.getByTestId("simulation-status-panel")).toBeInTheDocument();
    expect(screen.getByTestId("simulation-kpi-grid")).toBeInTheDocument();
    expect(screen.getByTestId("simulation-main-grid")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Thiết lập mô phỏng" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Luồng sự kiện mô phỏng" })).toBeInTheDocument();
    expect(screen.getAllByText("HIGH").length).toBeGreaterThan(0);
  });

  it("falls back to ports when simulation points are empty", async () => {
    vi.mocked(getSimulationSnapshot).mockResolvedValue(snapshot);
    vi.mocked(getSimulationMapPoints).mockResolvedValue([]);
    vi.mocked(getSimulationResult).mockResolvedValue({
      dangerousZones: [],
      mapPoints: [],
      sessionId: "session-1",
      tasks: []
    });

    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    expect(await screen.findByText("Cang Tien Sa")).toBeInTheDocument();
  });

  it("runs the demo from the settings panel", async () => {
    vi.mocked(getSimulationSnapshot).mockResolvedValue(snapshot);
    vi.mocked(runDemoSimulation).mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByRole("button", { name: "Đang phát dữ liệu..." });

    expect(runDemoSimulation).not.toHaveBeenCalled();
  });

  it("opens the create data popup, saves a dataset, and selects it for simulation", async () => {
    const user = userEvent.setup();
    vi.mocked(getSimulationSnapshot).mockResolvedValue(snapshot);
    vi.mocked(createSimulationDataset).mockResolvedValue({
      datasetId: "dataset-new",
      description: "Nhap tay",
      name: "Kich ban moi",
      portCode: "DNTSA",
      snapshotCount: 1
    });
    vi.mocked(runSimulationDataset).mockResolvedValue({
      finalRiskLevel: "CRITICAL",
      generatedTaskCount: 1,
      sessionId: "session-1"
    });

    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    expect(await screen.findByTestId("simulation-map")).toBeInTheDocument();
    expect(await screen.findByText("acc1")).toBeInTheDocument();
    expect(screen.getByText("acc2")).toBeInTheDocument();
    expect(screen.queryByLabelText("Tên kịch bản")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Tạo dữ liệu mô phỏng" }));

    expect(screen.getByRole("dialog", { name: "Tạo dữ liệu mô phỏng" })).toBeInTheDocument();
    expect(await screen.findByRole("combobox", { name: "Zone ID" })).toHaveTextContent("Ben so 1");
    await user.selectOptions(screen.getByRole("combobox", { name: "Zone ID" }), "zone-dock-1");
    await user.type(screen.getByLabelText("Tên kịch bản"), "Kich ban moi");
    await user.click(screen.getByRole("button", { name: "Lưu dữ liệu" }));

    expect(createSimulationDataset).toHaveBeenCalled();
    expect(await screen.findByText("Đã lưu dữ liệu mô phỏng")).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: /Kich ban moi/ })).toBeChecked();
    expect(screen.getByRole("button", { name: "Chạy dữ liệu đã chọn" })).toBeInTheDocument();
  });

  it("runs every selected simulation dataset in sequence", async () => {
    const user = userEvent.setup();
    vi.mocked(getSimulationSnapshot).mockResolvedValue({ ...snapshot, status: "IDLE", progressPercent: 0 });
    vi.mocked(runSimulationDataset)
      .mockResolvedValueOnce({
        finalRiskLevel: "HIGH",
        generatedTaskCount: 1,
        sessionId: "session-1"
      })
      .mockResolvedValueOnce({
        finalRiskLevel: "CRITICAL",
        generatedTaskCount: 2,
        sessionId: "session-2"
      });

    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    const firstDataset = await screen.findByRole("checkbox", { name: /Bao Da Nang/ });
    const secondDataset = screen.getByRole("checkbox", { name: /Mua cuc bo/ });
    expect(firstDataset).toBeChecked();

    await user.click(secondDataset);
    await user.click(screen.getByRole("button", { name: "Chạy dữ liệu đã chọn" }));

    expect(runSimulationDataset).toHaveBeenNthCalledWith(1, "dataset-1");
    expect(runSimulationDataset).toHaveBeenNthCalledWith(2, "dataset-2");
    expect(getSimulationResult).toHaveBeenCalledWith("session-2");
  });

  it("edits and deletes simulation datasets from the settings list", async () => {
    const user = userEvent.setup();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(getSimulationSnapshot).mockResolvedValue({ ...snapshot, status: "IDLE", progressPercent: 0 });
    vi.mocked(getSimulationDataset).mockResolvedValue({
      datasetId: "dataset-1",
      description: "Bao lon",
      name: "Bao Da Nang",
      portCode: "DNTSA",
      snapshotCount: 1,
      snapshots: [{
        beaufortNumber: 8,
        rainfall1hMm: 28,
        snapshotNumber: 1,
        visibilityKm: 4,
        windSpeedMs: 18,
        zoneId: "zone-dock-1"
      }]
    });
    vi.mocked(updateSimulationDataset).mockResolvedValue({
      datasetId: "dataset-1",
      description: "Bao lon",
      name: "Bao Da Nang Updated",
      portCode: "DNTSA",
      snapshotCount: 1
    });
    vi.mocked(deleteSimulationDataset).mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByRole("checkbox", { name: /Bao Da Nang/ });
    await user.click(screen.getAllByRole("button", { name: "Chỉnh sửa" })[0]);

    expect(getSimulationDataset).toHaveBeenCalledWith("dataset-1");
    expect(await screen.findByRole("dialog", { name: "Chỉnh sửa dữ liệu mô phỏng" })).toBeInTheDocument();
    const nameInput = screen.getByLabelText("Tên kịch bản");
    await user.clear(nameInput);
    await user.type(nameInput, "Bao Da Nang Updated");
    await user.click(screen.getByRole("button", { name: "Lưu dữ liệu" }));

    expect(updateSimulationDataset).toHaveBeenCalledWith("dataset-1", expect.objectContaining({
      name: "Bao Da Nang Updated"
    }));
    expect(await screen.findByText("Đã cập nhật dữ liệu mô phỏng")).toBeInTheDocument();

    await user.click(screen.getAllByRole("button", { name: "Xóa" })[0]);

    expect(deleteSimulationDataset).toHaveBeenCalledWith("dataset-1");
    expect(await screen.findByText("Đã xóa dữ liệu mô phỏng")).toBeInTheDocument();
    expect(screen.queryByRole("checkbox", { name: /Bao Da Nang Updated/ })).not.toBeInTheDocument();
  });

  it("keeps OpenWeather forecast planning out of the manual simulation page", async () => {
    vi.mocked(getSimulationSnapshot).mockResolvedValue({ ...snapshot, status: "IDLE", progressPercent: 0 });
    render(
      <MemoryRouter>
        <SimulationPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByRole("heading", { name: "Thiết lập mô phỏng" });

    expect(screen.queryByRole("heading", { name: "Kế hoạch dự báo tương lai" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cập nhật kế hoạch từ thời tiết mới" })).not.toBeInTheDocument();
    expect(screen.queryByText("Lap lich linh hoat")).not.toBeInTheDocument();
    expect(screen.queryByRole("checkbox", { name: /Ke hoach du bao DNTSA/ })).not.toBeInTheDocument();
  });
});
