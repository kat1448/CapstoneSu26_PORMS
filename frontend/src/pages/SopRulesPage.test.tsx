import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SopRulesPage } from "./SopRulesPage";
import { createSopRule, deleteSopRule, getSopRules } from "../services/sopRulesService";
import type { SopRule, SopRulesResponse } from "../services/sopRulesService";

vi.mock("../services/sopRulesService", () => ({
  createSopRule: vi.fn(),
  deleteSopRule: vi.fn(),
  getSopRules: vi.fn(),
  updateSopRule: vi.fn()
}));

function rule(overrides: Partial<SopRule>): SopRule {
  return {
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
    version: 1,
    ...overrides
  };
}

const response: SopRulesResponse = {
  executions: [],
  rules: [
    rule({ executionCount: 1, id: "rule-low", ruleCode: "SOP-LOW-01", ruleName: "Theo doi muc thap", triggerRiskLevel: "LOW" }),
    rule({ executionCount: 2, id: "rule-medium", ruleCode: "SOP-MEDIUM-01", ruleName: "Tang giam sat", triggerRiskLevel: "MEDIUM" }),
    rule({ executionCount: 3, id: "rule-high", ruleCode: "SOP-HIGH-YARD-01", ruleName: "Kiem tra bai khi rui ro cao", triggerRiskLevel: "HIGH" }),
    rule({ actionType: "STOP_OPERATIONS", executionCount: 4, id: "rule-critical", ruleCode: "SOP-CRITICAL-01", ruleName: "Dung khai thac", triggerRiskLevel: "CRITICAL" })
  ],
  summary: { activeRules: 4, automatedTasks: 3, recentExecutions: 10, totalRules: 4 }
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("SopRulesPage", () => {
  it("groups SOP rules into four risk sections and opens a selected detail list", async () => {
    const user = userEvent.setup();
    vi.mocked(getSopRules).mockResolvedValue(response);

    render(<SopRulesPage />);

    const riskGrid = await screen.findByLabelText("Nhóm quy tắc SOP theo mức rủi ro");
    for (const risk of ["LOW", "MEDIUM", "HIGH", "CRITICAL"]) {
      expect(within(riskGrid).getByRole("heading", { name: risk })).toBeInTheDocument();
    }
    expect(screen.queryByText("Kích hoạt gần đây")).not.toBeInTheDocument();
    expect(screen.queryByText(/L.+n k.+ch ho.+t/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/l.+n ch.+y/i)).not.toBeInTheDocument();
    expect(screen.queryByText("SOP-HIGH-YARD-01")).not.toBeInTheDocument();

    const highCard = within(riskGrid).getByRole("heading", { name: "HIGH" }).closest("article");
    expect(highCard).not.toBeNull();
    await user.click(within(highCard as HTMLElement).getByRole("button", { name: "Chi tiết" }));

    expect(screen.getByRole("heading", { name: "Danh sách quy tắc HIGH" })).toBeInTheDocument();
    expect(screen.getByText("SOP-HIGH-YARD-01")).toBeInTheDocument();
    expect(screen.queryByText(/l.+n ch.+y/i)).not.toBeInTheDocument();
    expect(screen.queryByText("SOP-CRITICAL-01")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Quay lại tổng quan" }));
    expect(screen.getByLabelText("Nhóm quy tắc SOP theo mức rủi ro")).toBeInTheDocument();
  });

  it("loads SOP rules from API and opens the create form", async () => {
    const user = userEvent.setup();
    vi.mocked(getSopRules).mockResolvedValue(response);

    render(<SopRulesPage />);

    await screen.findByLabelText("Nhóm quy tắc SOP theo mức rủi ro");
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

    const riskGrid = await screen.findByLabelText("Nhóm quy tắc SOP theo mức rủi ro");
    await user.click(screen.getByRole("button", { name: "Thêm quy tắc" }));
    await user.type(screen.getByLabelText("Mã quy tắc"), "SOP-NEW-01");
    await user.type(screen.getByLabelText("Tên quy tắc"), "Rule moi");
    await user.click(screen.getByRole("button", { name: "Tạo quy tắc" }));

    await waitFor(() => expect(createSopRule).toHaveBeenCalled());

    const highCard = within(riskGrid).getByRole("heading", { name: "HIGH" }).closest("article");
    await user.click(within(highCard as HTMLElement).getByRole("button", { name: "Chi tiết" }));
    await user.click(screen.getByRole("button", { name: "Xóa" }));

    expect(deleteSopRule).toHaveBeenCalledWith("rule-high");
  });
});
