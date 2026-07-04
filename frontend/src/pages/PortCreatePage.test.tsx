import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PortCreatePage } from "./PortCreatePage";
import { createPort } from "../services/portService";

vi.mock("../services/portService", () => ({
  createPort: vi.fn()
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("PortCreatePage", () => {
  it("creates a port with zone rows then returns to the ports list", async () => {
    const user = userEvent.setup();
    vi.mocked(createPort).mockResolvedValue({
      activeAlertCount: 0,
      currentOperationMode: "NORMAL",
      currentRiskLevel: "LOW",
      isActive: true,
      portCode: "DNNEW",
      portId: "port-new",
      portName: "Cang Moi",
      updatedAtLabel: "Chưa có dữ liệu"
    });

    render(
      <MemoryRouter initialEntries={["/ports/new"]}>
        <Routes>
          <Route path="/ports/new" element={<PortCreatePage />} />
          <Route path="/ports" element={<div>Ports list page</div>} />
        </Routes>
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText("Mã cảng"), "DNNEW");
    await user.type(screen.getByLabelText("Tên cảng"), "Cang Moi");
    await user.type(screen.getByLabelText("Địa chỉ"), "Da Nang");
    await user.clear(screen.getByLabelText("Latitude cảng"));
    await user.type(screen.getByLabelText("Latitude cảng"), "16.12");
    await user.clear(screen.getByLabelText("Longitude cảng"));
    await user.type(screen.getByLabelText("Longitude cảng"), "108.22");

    await user.clear(screen.getByLabelText("Tên khu vực 1"));
    await user.type(screen.getByLabelText("Tên khu vực 1"), "Bai A");
    await user.selectOptions(screen.getByLabelText("Loại khu vực 1"), "YARD");
    await user.clear(screen.getByLabelText("Sức chứa 1"));
    await user.type(screen.getByLabelText("Sức chứa 1"), "1000");
    await user.type(screen.getByLabelText("Đơn vị 1"), "TEU");
    await user.click(screen.getByRole("button", { name: "Tạo cảng" }));

    expect(createPort).toHaveBeenCalledWith({
      address: "Da Nang",
      code: "DNNEW",
      isActive: true,
      latitude: 16.12,
      longitude: 108.22,
      name: "Cang Moi",
      timezone: "Asia/Ho_Chi_Minh",
      weatherSource: "OPENWEATHER",
      weatherStationId: null,
      zones: [{
        capacityUnit: "TEU",
        capacityValue: 1000,
        displayOrder: 1,
        latitude: null,
        longitude: null,
        name: "Bai A",
        zoneType: "YARD"
      }]
    });
    await waitFor(() => expect(screen.getByText("Ports list page")).toBeInTheDocument());
  });
});
