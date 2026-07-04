import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SopRulesPage } from "./SopRulesPage";
import { createSopRule, deleteSopRule, getSopRules } from "../services/sopRulesService";
import type { SopRulesResponse } from "../services/sopRulesService";

vi.mock("../services/sopRulesService", () => ({
  createSopRule: vi.fn(),
  deleteSopRule: vi.fn(),
  getSopRules: vi.fn(),
  updateSopRule: vi.fn()
}));

const response: SopRulesResponse = {
  executions: [],
  rules: [{
    actionConfig: { task: "inspect" },
    actionConfigText: "{\"task\":\"inspect\"}",
    actionType: "CREATE_TASK",
    appliesToZoneType: "YARD",
    description: "Kiem tra bai",
    executionCount: 3,
    executionOrder: 2,
    id: "rule-1",
    isActive: true,
    previousRiskLevel: null,
    ruleCode: "SOP-HIGH-YARD-01",
    ruleName: "Kiem tra bai khi rui ro cao",
    triggerRiskLevel: "HIGH",
    updatedAt: "2026-06-27T00:00:00Z",
    version: 1
  }],
  summary: { activeRules: 1, automatedTasks: 1, recentExecutions: 0, totalRules: 1 }
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("SopRulesPage", () => {
  it("loads SOP rules from API and opens the create form", async () => {
    const user = userEvent.setup();
    vi.mocked(getSopRules).mockResolvedValue(response);

    render(<SopRulesPage />);

    await screen.findByText("SOP-HIGH-YARD-01");
    expect(screen.getByText("Tổng quy tắc")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Thêm quy tắc" }));
    expect(screen.getByRole("heading", { name: "Thêm quy tắc" })).toBeInTheDocument();
  });

  it("creates and deletes SOP rules through the API", async () => {
    const user = userEvent.setup();
    vi.mocked(getSopRules).mockResolvedValue(response);
    vi.mocked(createSopRule).mockResolvedValue(response.rules[0]);
    vi.mocked(deleteSopRule).mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValueOnce(true);

    render(<SopRulesPage />);

    await screen.findByText("SOP-HIGH-YARD-01");
    await user.click(screen.getByRole("button", { name: "Thêm quy tắc" }));
    await user.type(screen.getByLabelText("Mã quy tắc"), "SOP-NEW-01");
    await user.type(screen.getByLabelText("Tên quy tắc"), "Rule moi");
    await user.click(screen.getByRole("button", { name: "Tạo quy tắc" }));

    await waitFor(() => expect(createSopRule).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: "Xóa" }));
    expect(deleteSopRule).toHaveBeenCalledWith("rule-1");
  });
});
