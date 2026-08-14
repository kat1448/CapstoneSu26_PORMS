import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RiskConfigPage } from "./RiskConfigPage";
import {
  confirmRiskThresholdImport,
  getRiskConfig,
  previewRiskThresholdImport,
  saveRiskThresholds,
  saveZoneThresholdOverrides
} from "../services/riskConfigService";
import type {
  RiskConfigResponse,
  RiskThresholdImportPreview
} from "../services/riskConfigService";
import type { DemoUser } from "../App";

vi.mock("../services/riskConfigService", () => ({
  confirmRiskThresholdImport: vi.fn(),
  deleteZoneThresholdOverride: vi.fn(),
  getRiskConfig: vi.fn(),
  getRiskThresholdTemplate: vi.fn(),
  previewRiskThresholdImport: vi.fn(),
  saveRiskThresholds: vi.fn(),
  saveZoneThresholdOverrides: vi.fn()
}));

const adminUser: DemoUser = {
  email: "admin@porms.vn",
  initials: "AD",
  name: "Admin",
  portName: "Toàn hệ thống",
  role: "ADMIN"
};

const portManagerUser: DemoUser = {
  email: "manager@porms.vn",
  initials: "PM",
  name: "Port Manager",
  portName: "Cảng Tiên Sa",
  role: "PORT_MANAGER"
};

const config: RiskConfigResponse = {
  thresholds: [
    { comparisonOperator: ">=", description: "Wind medium", factor: "WIND", id: "t-1", isEnabled: true, riskLevel: "MEDIUM", thresholdValue: 5, unit: "cap", updatedAt: "2026-06-27T00:00:00Z", version: 1 },
    { comparisonOperator: ">=", description: "Wind high", factor: "WIND", id: "t-2", isEnabled: true, riskLevel: "HIGH", thresholdValue: 7, unit: "cap", updatedAt: "2026-06-27T00:00:00Z", version: 1 },
    { comparisonOperator: ">=", description: "Wind critical", factor: "WIND", id: "t-3", isEnabled: true, riskLevel: "CRITICAL", thresholdValue: 9, unit: "cap", updatedAt: "2026-06-27T00:00:00Z", version: 1 },
    { comparisonOperator: ">=", description: "Rain medium", factor: "RAIN", id: "t-4", isEnabled: true, riskLevel: "MEDIUM", thresholdValue: 10, unit: "mm/h", updatedAt: "2026-06-27T00:00:00Z", version: 1 },
    { comparisonOperator: ">=", description: "Rain high", factor: "RAIN", id: "t-5", isEnabled: true, riskLevel: "HIGH", thresholdValue: 25, unit: "mm/h", updatedAt: "2026-06-27T00:00:00Z", version: 1 },
    { comparisonOperator: "<=", description: "Visibility critical", factor: "VISIBILITY", id: "t-6", isEnabled: true, riskLevel: "CRITICAL", thresholdValue: 1.5, unit: "km", updatedAt: "2026-06-27T00:00:00Z", version: 1 }
  ],
  zoneOverrides: [{
    comparisonOperator: ">=",
    factor: "WIND",
    id: "override-1",
    isEnabled: true,
    riskLevel: "HIGH",
    thresholdValue: 7,
    unit: "cap",
    updatedAt: "2026-06-27T00:00:00Z",
    zoneId: "zone-1",
    zoneName: "Ben so 1",
    zoneType: "DOCK"
  }],
  zones: [{ portName: "Cang Tien Sa", zoneId: "zone-1", zoneName: "Ben so 1", zoneType: "DOCK" }]
};

const validImportPreview: RiskThresholdImportPreview = {
  canImport: true,
  createCount: 0,
  errors: [],
  fileName: "risk-thresholds.xlsx",
  invalidRows: 0,
  rows: [{
    action: "UPDATE",
    comparisonOperator: ">=",
    description: "Wind high updated",
    errors: [],
    existingValue: {
      comparisonOperator: ">=",
      description: "Wind high",
      isEnabled: true,
      thresholdValue: 7,
      unit: "cap"
    },
    factor: "WIND",
    isEnabled: true,
    riskLevel: "HIGH",
    rowNumber: 2,
    thresholdValue: 8,
    unit: "cap"
  }],
  totalRows: 1,
  unchangedCount: 0,
  updateCount: 1,
  validRows: 1
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("RiskConfigPage", () => {
  it("loads thresholds from API and saves global configuration", async () => {
    const user = userEvent.setup();
    vi.mocked(getRiskConfig).mockResolvedValue(config);
    vi.mocked(saveRiskThresholds).mockResolvedValue(config);

    render(<RiskConfigPage currentUser={adminUser} />);

    await screen.findByRole("heading", { name: "Cấu hình ngưỡng rủi ro" });
    expect(screen.getByText("Database")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Lưu cấu hình" }));

    expect(saveRiskThresholds).toHaveBeenCalledWith(expect.objectContaining({
      thresholds: expect.arrayContaining([expect.objectContaining({ factor: "WIND", riskLevel: "HIGH" })])
    }));
  });

  it("saves a zone override through the API", async () => {
    const user = userEvent.setup();
    vi.mocked(getRiskConfig).mockResolvedValue(config);
    vi.mocked(saveZoneThresholdOverrides).mockResolvedValue(config);

    render(<RiskConfigPage currentUser={adminUser} />);

    await screen.findByText("Ben so 1");
    await user.click(screen.getByRole("button", { name: "Lưu override" }));

    await waitFor(() => expect(saveZoneThresholdOverrides).toHaveBeenCalledWith(
      "zone-1",
      expect.objectContaining({
        overrides: expect.arrayContaining([expect.objectContaining({ factor: "WIND", riskLevel: "HIGH" })])
      })
    ));
  });

  it("previews and confirms a valid risk-threshold Excel import", async () => {
    const user = userEvent.setup();
    const importedConfiguration: RiskConfigResponse = {
      ...config,
      thresholds: config.thresholds.map((threshold) => (
        threshold.factor === "WIND" && threshold.riskLevel === "HIGH"
          ? { ...threshold, thresholdValue: 8 }
          : threshold
      ))
    };

    vi.mocked(getRiskConfig).mockResolvedValue(config);
    vi.mocked(previewRiskThresholdImport).mockResolvedValue(validImportPreview);
    vi.mocked(confirmRiskThresholdImport).mockResolvedValue({
      succeeded: true,
      response: {
        configuration: importedConfiguration,
        createdCount: 0,
        fileName: "risk-thresholds.xlsx",
        unchangedCount: 0,
        updatedCount: 1
      }
    });

    render(<RiskConfigPage currentUser={adminUser} />);

    await screen.findByRole("heading", { name: "Cấu hình ngưỡng rủi ro" });
    await user.click(screen.getByRole("button", { name: "Nhập Excel" }));

    const file = new File(["excel-content"], "risk-thresholds.xlsx", {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    });

    await user.upload(screen.getByLabelText("File Excel ngưỡng rủi ro"), file);
    await user.click(screen.getByRole("button", { name: "Kiểm tra file" }));

    expect(await screen.findByText("File hợp lệ và có thể nhập.")).toBeInTheDocument();
    expect(screen.getByText("WIND")).toBeInTheDocument();

    await user.type(
      screen.getByLabelText("Lý do thay đổi khi nhập ngưỡng"),
      "Cập nhật ngưỡng theo tài liệu đã duyệt"
    );
    await user.click(screen.getByRole("button", { name: "Xác nhận nhập dữ liệu" }));

    await waitFor(() => {
      expect(confirmRiskThresholdImport).toHaveBeenCalledWith(
        file,
        "Cập nhật ngưỡng theo tài liệu đã duyệt"
      );
    });

    expect(await screen.findByRole("status")).toHaveTextContent("Nhập ngưỡng thành công");

    // Cấu hình mới có trong response import, không gọi GET lần hai.
    expect(getRiskConfig).toHaveBeenCalledTimes(1);
  });

  it("hides ADMIN-only Excel controls from Port Managers", async () => {
    vi.mocked(getRiskConfig).mockResolvedValue(config);

    render(<RiskConfigPage currentUser={portManagerUser} />);

    await screen.findByRole("heading", { name: "Cấu hình ngưỡng rủi ro" });

    expect(screen.queryByRole("button", { name: "Tải template Excel" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Nhập Excel" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Khôi phục" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Lưu cấu hình" })).not.toBeInTheDocument();
  });
});
