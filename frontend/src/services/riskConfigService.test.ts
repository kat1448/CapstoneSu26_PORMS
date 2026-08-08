import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  confirmRiskThresholdImport,
  deleteZoneThresholdOverride,
  getRiskConfig,
  getRiskThresholdTemplate,
  previewRiskThresholdImport,
  saveRiskThresholds,
  saveZoneThresholdOverrides
} from "./riskConfigService";
import type {
  RiskThresholdImportPreview,
  SaveRiskThresholdsInput,
  SaveZoneThresholdOverridesInput
} from "./riskConfigService";

const validPreview: RiskThresholdImportPreview = {
  canImport: true,
  createCount: 0,
  errors: [],
  fileName: "PORMS_RiskThresholds.xlsx",
  invalidRows: 0,
  rows: [],
  totalRows: 12,
  unchangedCount: 10,
  updateCount: 2,
  validRows: 12
};

/** Lưu phiên ADMIN hợp lệ để kiểm tra request Excel có JWT. */
function storeAdminSession() {
  localStorage.setItem(
    "porms.auth.session",
    JSON.stringify({
      accessToken: "admin-access-token",
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      refreshToken: "admin-refresh-token",
      user: {
        email: "admin@porms.vn",
        id: "11111111-1111-1111-1111-111111111111",
        initials: "AD",
        name: "System Admin",
        portName: "Toàn hệ thống",
        role: "ADMIN"
      }
    })
  );
}

function createExcelFile() {
  return new File(
    ["fake-xlsx-content"],
    "PORMS_RiskThresholds.xlsx",
    {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    }
  );
}

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

  it("downloads the risk-threshold template with ADMIN authorization", async () => {
    storeAdminSession();
    const expectedBlob = new Blob(["excel-template"], {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    });
    vi.mocked(fetch).mockResolvedValueOnce(new Response(expectedBlob, { status: 200 }));

    await expect(getRiskThresholdTemplate()).resolves.toHaveProperty("size");

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/risk/thresholds/import-template",
      {
        headers: { Authorization: "Bearer admin-access-token" },
        method: "GET"
      }
    );
  });

  it("previews a risk-threshold Excel file as multipart data", async () => {
    storeAdminSession();
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(validPreview), { status: 200 })
    );

    const file = createExcelFile();
    await expect(previewRiskThresholdImport(file)).resolves.toEqual(validPreview);

    const [, request] = vi.mocked(fetch).mock.calls[0];
    expect(request?.method).toBe("POST");
    expect(request?.headers).toEqual({ Authorization: "Bearer admin-access-token" });
    expect(request?.body).toBeInstanceOf(FormData);

    // Không đặt Content-Type để trình duyệt tự tạo multipart boundary.
    const formData = request?.body as FormData;
    expect(formData.get("File")).toBe(file);
  });

  it("confirms a valid import and returns the latest risk configuration", async () => {
    storeAdminSession();
    const importResponse = {
      configuration: { thresholds: [], zoneOverrides: [], zones: [] },
      createdCount: 0,
      fileName: "PORMS_RiskThresholds.xlsx",
      unchangedCount: 10,
      updatedCount: 2
    };
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(importResponse), { status: 200 })
    );

    const file = createExcelFile();
    const result = await confirmRiskThresholdImport(
      file,
      "Cập nhật ngưỡng từ tài liệu đã duyệt"
    );

    expect(result.succeeded).toBe(true);
    if (!result.succeeded) throw new Error("Import phải thành công.");
    expect(result.response).toEqual(importResponse);

    const [, request] = vi.mocked(fetch).mock.calls[0];
    const formData = request?.body as FormData;
    expect(formData.get("File")).toBe(file);
    expect(formData.get("ChangeReason")).toBe("Cập nhật ngưỡng từ tài liệu đã duyệt");
  });

  it("returns validation details when risk-threshold confirmation is rejected", async () => {
    storeAdminSession();
    const invalidPreview: RiskThresholdImportPreview = {
      ...validPreview,
      canImport: false,
      errors: [{ column: "ChangeReason", message: "Lý do quá ngắn.", rowNumber: 0 }],
      invalidRows: 1,
      validRows: 11
    };
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify(invalidPreview), { status: 400 })
    );

    const result = await confirmRiskThresholdImport(createExcelFile(), "abc");

    expect(result.succeeded).toBe(false);
    if (result.succeeded) throw new Error("Import phải bị từ chối.");
    expect(result.preview).toEqual(invalidPreview);
  });
});
