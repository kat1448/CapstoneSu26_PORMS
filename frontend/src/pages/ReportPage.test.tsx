import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ReportPage } from "./ReportPage";
import { downloadReport, getReportPreview } from "../services/reportService";
import { getPorts } from "../services/portService";

vi.mock("../services/reportService", () => ({ downloadReport: vi.fn(), getReportPreview: vi.fn() }));
vi.mock("../services/portService", () => ({ getPorts: vi.fn() }));

describe("ReportPage", () => {
  beforeEach(() => {
    vi.mocked(getPorts).mockResolvedValue([
      { portId: "port-1", portCode: "DNTSA", portName: "Cảng Tiên Sa" },
      { portId: "port-2", portCode: "VNCLI", portName: "Cảng Cát Lái" }
    ] as never);
    vi.mocked(getReportPreview).mockResolvedValue({ reportType: "ALERTS", totalRows: 1, rows: [{ occurredAt: "2026-07-29T10:00:00Z", portCode: "DNTSA", portName: "Cảng Tiên Sa", zoneName: "Khu cầu bến", riskLevel: "HIGH", subject: "Cảnh báo gió mạnh", description: "Cần chủ động ứng phó.", owner: "Vận hành thực tế", status: "Đã xác nhận" }] });
  });

  it("keeps a Port Manager inside the assigned port and exports the preview", async () => {
    const user = userEvent.setup();
    render(<ReportPage currentUser={{ email: "pm@porms.local", initials: "PM", name: "Port Manager", portId: "port-1", portName: "Cảng Tiên Sa", role: "PORT_MANAGER" }} />);
    await waitFor(() => expect(screen.getByRole("option", { name: /DNTSA/ })).toBeInTheDocument());
    expect(screen.queryByRole("option", { name: /VNCLI/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Xem trước báo cáo" }));
    expect(await screen.findByText("Cảnh báo gió mạnh")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Xuất PDF" }));
    expect(downloadReport).toHaveBeenCalledWith(expect.objectContaining({ portCode: "DNTSA", type: "ALERTS" }), "pdf");
  });
});
