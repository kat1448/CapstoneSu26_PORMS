import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RiskConfigPage } from "./RiskConfigPage";
import { getRiskConfig, saveRiskThresholds, saveZoneThresholdOverrides } from "../services/riskConfigService";
import type { RiskConfigResponse } from "../services/riskConfigService";

vi.mock("../services/riskConfigService", () => ({
  deleteZoneThresholdOverride: vi.fn(),
  getRiskConfig: vi.fn(),
  saveRiskThresholds: vi.fn(),
  saveZoneThresholdOverrides: vi.fn()
}));

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

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("RiskConfigPage", () => {
  it("loads thresholds from API and saves global configuration", async () => {
    const user = userEvent.setup();
    vi.mocked(getRiskConfig).mockResolvedValue(config);
    vi.mocked(saveRiskThresholds).mockResolvedValue(config);

    render(<RiskConfigPage />);

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

    render(<RiskConfigPage />);

    await screen.findByText("Ben so 1");
    await user.click(screen.getByRole("button", { name: "Lưu override" }));

    await waitFor(() => expect(saveZoneThresholdOverrides).toHaveBeenCalledWith(
      "zone-1",
      expect.objectContaining({
        overrides: expect.arrayContaining([expect.objectContaining({ factor: "WIND", riskLevel: "HIGH" })])
      })
    ));
  });
});
