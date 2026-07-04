import { render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getSimulationSnapshot } from "../services/simulationService";
import type { SimulationSnapshot } from "../types/simulation";
import { SimulationResultsPage } from "./SimulationResultsPage";

vi.mock("../hooks/useDemoRefresh", () => ({
  useDemoRefresh: () => 0
}));

vi.mock("../services/simulationService", () => ({
  getSimulationSnapshot: vi.fn()
}));

const snapshot: SimulationSnapshot = {
  beaufortNumber: 10,
  currentMode: "STOP",
  currentRiskLevel: "CRITICAL",
  feed: [
    {
      detail: "Bến số 1 đạt CRITICAL, dừng bốc xếp",
      happenedAt: "14:30",
      riskLevel: "CRITICAL",
      title: "Kích hoạt SOP"
    },
    {
      detail: "Tạo task kiểm tra an toàn",
      happenedAt: "14:35",
      riskLevel: "HIGH",
      title: "Sinh task vận hành"
    }
  ],
  generatedAlertCount: 3,
  modeChangeCount: 2,
  progressPercent: 100,
  rainfall1hMm: 60,
  status: "COMPLETED",
  visibilityKm: 0.8,
  windSpeedMs: 27.4
};

describe("SimulationResultsPage", () => {
  beforeEach(() => {
    vi.mocked(getSimulationSnapshot).mockResolvedValue(snapshot);
  });

  it("renders simulation results as readable tables", async () => {
    render(<SimulationResultsPage refreshKey={0} />);

    expect(await screen.findByRole("heading", { name: "Kết quả mô phỏng" })).toBeInTheDocument();
    expect(screen.getAllByText("CRITICAL").length).toBeGreaterThan(0);
    expect(screen.getByText("STOP")).toBeInTheDocument();

    const eventTable = screen.getByRole("table", { name: "Bảng sự kiện mô phỏng" });
    const rows = within(eventTable).getAllByRole("row");
    expect(rows).toHaveLength(3);
    expect(within(eventTable).getByText("Kích hoạt SOP")).toBeInTheDocument();
    expect(within(eventTable).getByText("Bến số 1 đạt CRITICAL, dừng bốc xếp")).toBeInTheDocument();

    const weatherTable = screen.getByRole("table", { name: "Bảng dữ liệu thời tiết mô phỏng" });
    expect(within(weatherTable).getByText("27.4 m/s")).toBeInTheDocument();
    expect(within(weatherTable).getByText("0.8 km")).toBeInTheDocument();
  });
});
