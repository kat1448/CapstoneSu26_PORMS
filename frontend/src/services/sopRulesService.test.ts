import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createSopRule, deleteSopRule, getSopRules, updateSopRule } from "./sopRulesService";
import type { SopRuleInput } from "./sopRulesService";

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
});
