import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { deleteZoneThresholdOverride, getRiskConfig, saveRiskThresholds, saveZoneThresholdOverrides } from "./riskConfigService";
import type { SaveRiskThresholdsInput, SaveZoneThresholdOverridesInput } from "./riskConfigService";

describe("riskConfigService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("loads risk thresholds and zone overrides from the real API", async () => {
    const response = {
      thresholds: [],
      zoneOverrides: [],
      zones: []
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(getRiskConfig()).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/risk/thresholds",
      expect.objectContaining({
        headers: expect.objectContaining({ "Content-Type": "application/json" })
      })
    );
  });

  it("saves global thresholds without mock fallback", async () => {
    const payload: SaveRiskThresholdsInput = {
      changeReason: "Cap nhat nguong mua",
      thresholds: [{
        comparisonOperator: ">=",
        description: "Mua lon",
        factor: "RAIN",
        isEnabled: true,
        riskLevel: "HIGH",
        thresholdValue: 25,
        unit: "mm/h",
        version: 1
      }]
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ thresholds: payload.thresholds, zoneOverrides: [], zones: [] }), { status: 200 }));

    await saveRiskThresholds(payload);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/risk/thresholds",
      expect.objectContaining({
        body: JSON.stringify(payload),
        method: "PUT"
      })
    );
  });

  it("saves and deletes zone threshold overrides without mock fallback", async () => {
    const zonePayload: SaveZoneThresholdOverridesInput = {
      changeReason: "Bai container nhay cam hon",
      overrides: [{
        comparisonOperator: ">=",
        factor: "WIND",
        isEnabled: true,
        riskLevel: "HIGH",
        thresholdValue: 7,
        unit: "cap"
      }]
    };
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({ thresholds: [], zoneOverrides: [], zones: [] }), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await saveZoneThresholdOverrides("zone-1", zonePayload);
    await deleteZoneThresholdOverride("zone-1", "override-1");

    expect(fetch).toHaveBeenNthCalledWith(
      1,
      "http://localhost:5000/api/risk/zones/zone-1/threshold-overrides",
      expect.objectContaining({
        body: JSON.stringify(zonePayload),
        method: "PUT"
      })
    );
    expect(fetch).toHaveBeenNthCalledWith(
      2,
      "http://localhost:5000/api/risk/zones/zone-1/threshold-overrides/override-1",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
