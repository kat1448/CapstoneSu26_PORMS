import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { AlertPage } from "./AlertPage";
import { getAlerts } from "../services/alertService";
import type { AlertItem } from "../types/alert";

vi.mock("../hooks/useDemoRefresh", () => ({
  useDemoRefresh: vi.fn()
}));

vi.mock("../services/alertService", () => ({
  getAlerts: vi.fn()
}));

function makeAlert(index: number): AlertItem {
  return {
    alertId: `alert-${index}`,
    alertType: "WEATHER",
    createdAt: `27/06/2026 20:${String(index).padStart(2, "0")}:00`,
    message: `Noi dung canh bao ${index}`,
    portCode: "AB",
    portId: "port-1",
    portName: "Cang A",
    read: false,
    severity: "HIGH",
    title: `Canh bao ${index}`,
    zoneName: `Zone ${index}`
  };
}

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
    expect(screen.getAllByText("Cảng").length).toBeGreaterThan(0);
    expect(screen.getAllByText("AB - Cảng A").length).toBeGreaterThan(0);
    expect(screen.getByText("Cảnh báo mô phỏng HIGH tại AB1")).toBeInTheDocument();
    expect(screen.getByText("AB1 (AB) đạt mức HIGH: Gió Beaufort 8, mưa 28 mm/h.")).toBeInTheDocument();
    expect(screen.getByText("27/06/2026 20:45:12")).toBeInTheDocument();
  });

  it("paginates alerts fifteen rows at a time", async () => {
    const user = userEvent.setup();
    vi.mocked(getAlerts).mockResolvedValue(Array.from({ length: 16 }, (_, index) => makeAlert(index + 1)));

    render(<AlertPage refreshKey={0} />);

    expect(await screen.findByText("Trang 1/2")).toBeInTheDocument();
    expect(screen.getByText("Canh bao 1")).toBeInTheDocument();
    expect(screen.getByText("Canh bao 15")).toBeInTheDocument();
    expect(screen.queryByText("Canh bao 16")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Sau" }));

    expect(screen.getByText("Trang 2/2")).toBeInTheDocument();
    expect(screen.queryByText("Canh bao 1")).not.toBeInTheDocument();
    expect(screen.getByText("Canh bao 16")).toBeInTheDocument();
  });

  it("filters alerts by port, zone, date range and risk level before paginating", async () => {
    const user = userEvent.setup();
    vi.mocked(getAlerts).mockResolvedValue([
      {
        ...makeAlert(1),
        alertId: "alert-target",
        createdAt: "27/06/2026 20:45:12",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        severity: "CRITICAL",
        title: "Canh bao dung bo loc",
        zoneName: "Ben so 1"
      },
      {
        ...makeAlert(2),
        alertId: "alert-other-port",
        createdAt: "27/06/2026 20:50:12",
        portCode: "AB",
        portId: "port-other",
        portName: "Cang A",
        severity: "CRITICAL",
        title: "Canh bao sai cang",
        zoneName: "Ben so 1"
      },
      {
        ...makeAlert(3),
        alertId: "alert-other-zone",
        createdAt: "27/06/2026 21:00:12",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        severity: "CRITICAL",
        title: "Canh bao sai khu vuc",
        zoneName: "Ben so 2"
      },
      {
        ...makeAlert(4),
        alertId: "alert-other-risk",
        createdAt: "27/06/2026 21:10:12",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        severity: "HIGH",
        title: "Canh bao sai muc do",
        zoneName: "Ben so 1"
      },
      {
        ...makeAlert(5),
        alertId: "alert-other-date",
        createdAt: "28/06/2026 09:00:00",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        severity: "CRITICAL",
        title: "Canh bao sai ngay",
        zoneName: "Ben so 1"
      }
    ]);

    const { container } = render(<AlertPage refreshKey={0} />);
    const view = within(container);

    expect(await view.findByText("Canh bao dung bo loc")).toBeInTheDocument();

    await user.selectOptions(view.getByLabelText("Cảng"), "port-target");
    await user.selectOptions(view.getByLabelText("Khu vực"), "Ben so 1");
    await user.selectOptions(view.getByLabelText("Cấp độ rủi ro"), "CRITICAL");
    await user.type(view.getByLabelText("Từ ngày"), "2026-06-27");
    await user.type(view.getByLabelText("Đến ngày"), "2026-06-27");

    expect(view.getByText("Canh bao dung bo loc")).toBeInTheDocument();
    expect(view.queryByText("Canh bao sai cang")).not.toBeInTheDocument();
    expect(view.queryByText("Canh bao sai khu vuc")).not.toBeInTheDocument();
    expect(view.queryByText("Canh bao sai muc do")).not.toBeInTheDocument();
    expect(view.queryByText("Canh bao sai ngay")).not.toBeInTheDocument();
    expect(view.queryByText("Trang 1/")).not.toBeInTheDocument();
  });
});
