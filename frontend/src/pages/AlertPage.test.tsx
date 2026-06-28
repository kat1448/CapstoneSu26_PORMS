import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AlertPage } from "./AlertPage";
import { getAlerts } from "../services/alertService";

vi.mock("../hooks/useDemoRefresh", () => ({
  useDemoRefresh: vi.fn()
}));

vi.mock("../services/alertService", () => ({
  getAlerts: vi.fn()
}));

describe("AlertPage", () => {
  it("renders alert time and operational information in Vietnamese", async () => {
    vi.mocked(getAlerts).mockResolvedValue([
      {
        alertId: "alert-1",
        alertType: "SIMULATION",
        createdAt: "27/06/2026 20:45:12",
        message: "AB1 (AB) đạt mức HIGH: Gió Beaufort 8, mưa 28 mm/h.",
        portCode: "AB",
        portId: "port-1",
        portName: "Cảng A",
        read: false,
        severity: "HIGH",
        title: "Cảnh báo mô phỏng HIGH tại AB1",
        zoneName: "AB1"
      }
    ]);

    render(<AlertPage refreshKey={0} />);

    expect(await screen.findByText("Cảnh báo")).toBeInTheDocument();
    expect(screen.getByText("Theo dõi và xác nhận các cảnh báo vận hành")).toBeInTheDocument();
    expect(screen.getByText("Cảng")).toBeInTheDocument();
    expect(screen.getByText("AB - Cảng A")).toBeInTheDocument();
    expect(screen.getByText("Cảnh báo mô phỏng HIGH tại AB1")).toBeInTheDocument();
    expect(screen.getByText("AB1 (AB) đạt mức HIGH: Gió Beaufort 8, mưa 28 mm/h.")).toBeInTheDocument();
    expect(screen.getByText("27/06/2026 20:45:12")).toBeInTheDocument();
  });
});
