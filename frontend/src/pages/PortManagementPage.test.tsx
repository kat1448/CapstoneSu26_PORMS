import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PortManagementPage } from "./PortManagementPage";
import { deletePortZone, getPortZones, getPorts, updatePort, updatePortZone } from "../services/portService";
import type { PortSummary, PortZone } from "../types/port";

vi.mock("../services/portService", () => ({
  createPort: vi.fn(),
  deletePortZone: vi.fn(),
  getPortZones: vi.fn(),
  getPorts: vi.fn(),
  updatePort: vi.fn(),
  updatePortZone: vi.fn()
}));

const ports: PortSummary[] = [{
  activeAlertCount: 0,
  currentOperationMode: "NORMAL",
  currentRiskLevel: "LOW",
  isActive: true,
  latitude: 16.1228,
  longitude: 108.2144,
  portCode: "DNTSA",
  portId: "port-dntsa",
  portName: "Cang Tien Sa",
  updatedAtLabel: "Vua cap nhat"
}];

const zones: PortZone[] = [{
  capacityLabel: "1000 TEU",
  currentRiskLevel: "LOW",
  displayOrder: 1,
  isActive: true,
  isRestricted: false,
  latitude: 16.12,
  longitude: 108.22,
  overrideEnabled: false,
  portId: "port-dntsa",
  restrictionReason: null,
  statusLabel: "Binh thuong",
  zoneId: "zone-1",
  zoneName: "Khu A",
  zoneType: "YARD"
}];

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function renderDetailPage() {
  render(
    <MemoryRouter initialEntries={["/ports/port-dntsa"]}>
      <Routes>
        <Route path="/ports/:portId" element={<PortManagementPage detailMode refreshKey={0} />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("PortManagementPage", () => {
  it("keeps the ports list separate and links to the create page", async () => {
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getPortZones).mockResolvedValue([]);

    render(
      <MemoryRouter>
        <PortManagementPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByText("Cang Tien Sa");
    expect(screen.getByRole("link", { name: "Thêm cảng" })).toHaveAttribute("href", "/ports/new");
    expect(screen.queryByRole("heading", { name: "Tạo cảng mới" })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Mã cảng")).not.toBeInTheDocument();
  });

  it("shows edit and delete actions for zones on the detail page", async () => {
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getPortZones).mockResolvedValue(zones);

    renderDetailPage();

    await screen.findByText("Khu A");
    expect(screen.getByRole("button", { name: "Chỉnh sửa Khu A" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Xóa Khu A" })).toBeInTheDocument();
  });

  it("updates a zone and reloads the zone table", async () => {
    const user = userEvent.setup();
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getPortZones).mockResolvedValue(zones);
    vi.mocked(updatePortZone).mockResolvedValue({ ...zones[0], zoneName: "Khu B" });

    renderDetailPage();

    await screen.findByText("Khu A");
    await user.click(screen.getByRole("button", { name: "Chỉnh sửa Khu A" }));
    await user.clear(screen.getByLabelText("Tên khu vực"));
    await user.type(screen.getByLabelText("Tên khu vực"), "Khu B");
    await user.click(screen.getByRole("button", { name: "Lưu khu vực" }));

    expect(updatePortZone).toHaveBeenCalledWith("port-dntsa", "zone-1", {
      capacityUnit: "TEU",
      capacityValue: 1000,
      displayOrder: 1,
      isActive: true,
      latitude: 16.12,
      longitude: 108.22,
      name: "Khu B",
      zoneType: "YARD"
    });
    expect(getPortZones).toHaveBeenCalledTimes(2);
  });

  it("updates the selected port details and reloads the port list", async () => {
    const user = userEvent.setup();
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getPortZones).mockResolvedValue(zones);
    vi.mocked(updatePort).mockResolvedValue({ ...ports[0], portName: "Cảng Tiên Sa mới" });

    renderDetailPage();

    await screen.findByText("Cang Tien Sa");
    await user.clear(screen.getByLabelText("Tên cảng"));
    await user.type(screen.getByLabelText("Tên cảng"), "Cảng Tiên Sa mới");
    await user.click(screen.getByRole("button", { name: "Lưu thông tin cảng" }));

    expect(updatePort).toHaveBeenCalledWith("port-dntsa", {
      address: null,
      code: "DNTSA",
      isActive: true,
      latitude: 16.1228,
      longitude: 108.2144,
      name: "Cảng Tiên Sa mới",
      timezone: "Asia/Ho_Chi_Minh",
      weatherSource: "OPENWEATHER",
      weatherStationId: null
    });
    expect(getPorts).toHaveBeenCalledTimes(1);
  });

  it("deletes a zone after confirmation and reloads the zone table", async () => {
    const user = userEvent.setup();
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getPortZones).mockResolvedValue(zones);
    vi.mocked(deletePortZone).mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValueOnce(true);

    renderDetailPage();

    await screen.findByText("Khu A");
    await user.click(screen.getByRole("button", { name: "Xóa Khu A" }));

    expect(deletePortZone).toHaveBeenCalledWith("port-dntsa", "zone-1");
    expect(getPortZones).toHaveBeenCalledTimes(2);
  });
});
