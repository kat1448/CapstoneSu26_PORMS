import {
  confirmSopRuleImport,
  createSopRule,
  deleteSopRule,
  getSopRuleImportTemplate,
  getSopRules,
  previewSopRuleImport,
  updateSopRule
} from "./sopRulesService";

import type {
  SopRuleImportPreview,
  SopRuleInput
} from "./sopRulesService";

import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi
} from "vitest";

const validPreview: SopRuleImportPreview = {
  canImport: true,
  createCount: 1,
  errors: [],
  fileName: "PORMS_SopRules.xlsx",
  invalidRows: 0,
  rows: [],
  totalRows: 2,
  unchangedCount: 0,
  updateCount: 1,
  validRows: 2
};

/**
 * Lưu phiên ADMIN hợp lệ để kiểm tra request có JWT.
 */
function storeAdminSession() {
  localStorage.setItem(
    "porms.auth.session",
    JSON.stringify({
      accessToken: "admin-access-token",
      refreshToken: "admin-refresh-token",
      expiresAt:
        new Date(Date.now() + 60_000).toISOString(),
      user: {
        id: "11111111-1111-1111-1111-111111111111",
        email: "admin@porms.vn",
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
    "PORMS_SopRules.xlsx",
    {
      type:
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    }
  );
}

describe("sopRulesService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("loads SOP rules from the real API", async () => {
    const response = { executions: [], rules: [], summary: { activeRules: 0, automatedTasks: 0, recentExecutions: 0, totalRules: 0 } };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(getSopRules()).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/sop-rules",
      expect.objectContaining({
        headers: expect.objectContaining({ "Content-Type": "application/json" })
      })
    );
  });

  it("creates, updates, and deletes SOP rules without mock fallback", async () => {
    const payload: SopRuleInput = {
      actionConfigText: "{\"task\":\"inspect\"}",
      actionType: "CREATE_TASK",
      appliesToZoneType: "YARD",
      changeReason: "Them SOP san bai",
      description: "Kiem tra bai container",
      executionOrder: 2,
      isActive: true,
      previousRiskLevel: null,
      ruleCode: "SOP-HIGH-YARD-01",
      ruleName: "Kiem tra bai khi rui ro cao",
      triggerRiskLevel: "HIGH"
    };
    const response = { ...payload, actionConfig: { task: "inspect" }, executionCount: 0, id: "rule-1", updatedAt: "2026-06-27T00:00:00Z", version: 1 };
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await createSopRule(payload);
    await updateSopRule("rule-1", payload);
    await deleteSopRule("rule-1");

    expect(fetch).toHaveBeenNthCalledWith(
      1,
      "http://localhost:5000/api/sop-rules",
      expect.objectContaining({ body: JSON.stringify(payload), method: "POST" })
    );
    expect(fetch).toHaveBeenNthCalledWith(
      2,
      "http://localhost:5000/api/sop-rules/rule-1",
      expect.objectContaining({ body: JSON.stringify(payload), method: "PUT" })
    );
    expect(fetch).toHaveBeenNthCalledWith(
      3,
      "http://localhost:5000/api/sop-rules/rule-1",
      expect.objectContaining({ method: "DELETE" })
    );
  });

  it("downloads the SOP Excel template with ADMIN authorization", async () => {
    storeAdminSession();

    const expectedBlob = new Blob(
      ["excel-template"],
      {
        type:
          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      }
    );

    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(expectedBlob, { status: 200 })
    );

    const result = await getSopRuleImportTemplate();

    expect(result.size).toBeGreaterThan(0);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/sop-rules/import-template",
      {
        headers: {
          Authorization: "Bearer admin-access-token"
        },
        method: "GET"
      }
    );
  });

  it("previews an SOP Excel file as multipart data", async () => {
    storeAdminSession();

    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(
        JSON.stringify(validPreview),
        { status: 200 }
      )
    );

    const file = createExcelFile();
    const result = await previewSopRuleImport(file);

    expect(result).toEqual(validPreview);

    const [, request] =
      vi.mocked(fetch).mock.calls[0];

    expect(request?.method).toBe("POST");
    expect(request?.headers).toEqual({
      Authorization: "Bearer admin-access-token"
    });

    // Không đặt Content-Type để trình duyệt tự tạo multipart boundary
    expect(request?.body).toBeInstanceOf(FormData);

    const formData = request?.body as FormData;

    expect(formData.get("File")).toBe(file);
  });

  it("confirms a valid SOP import and returns the new configuration", async () => {
    storeAdminSession();

    const importResponse = {
      configuration: {
        executions: [],
        rules: [],
        summary: {
          activeRules: 6,
          automatedTasks: 2,
          recentExecutions: 0,
          totalRules: 6
        }
      },
      createdCount: 1,
      fileName: "PORMS_SopRules.xlsx",
      importBatchId:
        "22222222-2222-2222-2222-222222222222",
      unchangedCount: 0,
      updatedCount: 1
    };

    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(
        JSON.stringify(importResponse),
        { status: 200 }
      )
    );

    const file = createExcelFile();

    const result = await confirmSopRuleImport(
      file,
      "Cập nhật SOP từ file Excel."
    );

    expect(result.succeeded).toBe(true);

    if (!result.succeeded) {
      throw new Error("Import phải thành công.");
    }

    expect(result.response).toEqual(importResponse);

    const [, request] =
      vi.mocked(fetch).mock.calls[0];

    const formData = request?.body as FormData;

    expect(request?.method).toBe("POST");
    expect(formData.get("File")).toBe(file);
    expect(formData.get("ChangeReason"))
      .toBe("Cập nhật SOP từ file Excel.");
  });

  it("returns backend validation details when confirmation is rejected", async () => {
    storeAdminSession();

    const invalidPreview: SopRuleImportPreview = {
      ...validPreview,
      canImport: false,
      createCount: 0,
      errors: [
        {
          column: "ChangeReason",
          message:
            "Lý do thay đổi phải có ít nhất 5 ký tự.",
          rowNumber: 0
        }
      ],
      invalidRows: 1,
      totalRows: 1,
      updateCount: 0,
      validRows: 0
    };

    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(
        JSON.stringify(invalidPreview),
        { status: 400 }
      )
    );

    const result = await confirmSopRuleImport(
      createExcelFile(),
      "abc"
    );

    expect(result.succeeded).toBe(false);

    if (result.succeeded) {
      throw new Error("Import phải bị từ chối.");
    }

    expect(result.preview).toEqual(invalidPreview);
    expect(result.preview.errors[0]?.column)
      .toBe("ChangeReason");
  });
});
