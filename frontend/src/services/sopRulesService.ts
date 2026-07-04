import { requestJson, requestVoid } from "./api";
import type { RiskLevel } from "./riskConfigService";

export type SopActionType =
  | "CREATE_TASK"
  | "RESTRICT_ZONE"
  | "SEND_ALERT"
  | "SET_LIMITED_MODE"
  | "SET_NORMAL_MODE"
  | "STOP_OPERATIONS"
  | "UNRESTRICT_ZONE";

export type SopRule = {
  actionConfig: Record<string, unknown>;
  actionConfigText: string;
  actionType: SopActionType;
  appliesToZoneType: string | null;
  description: string | null;
  executionCount: number;
  executionOrder: number;
  id: string;
  isActive: boolean;
  previousRiskLevel: RiskLevel | null;
  ruleCode: string;
  ruleName: string;
  triggerRiskLevel: RiskLevel;
  updatedAt: string;
  version: number;
};

export type SopExecution = {
  actionType: SopActionType;
  completedAt: string | null;
  id: string;
  riskLevel: RiskLevel;
  ruleCode: string;
  ruleName: string;
  status: string;
  zoneName: string | null;
};

export type SopRulesSummary = {
  activeRules: number;
  automatedTasks: number;
  recentExecutions: number;
  totalRules: number;
};

export type SopRulesResponse = {
  executions: SopExecution[];
  rules: SopRule[];
  summary: SopRulesSummary;
};

export type SopRuleInput = {
  actionConfigText: string;
  actionType: SopActionType;
  appliesToZoneType: string | null;
  changeReason?: string | null;
  description: string | null;
  executionOrder: number;
  isActive: boolean;
  previousRiskLevel: RiskLevel | null;
  ruleCode: string;
  ruleName: string;
  triggerRiskLevel: RiskLevel;
};

export function getSopRules(): Promise<SopRulesResponse> {
  return requestJson<SopRulesResponse>("/api/sop-rules");
}

export function createSopRule(input: SopRuleInput): Promise<SopRule> {
  return requestJson<SopRule>("/api/sop-rules", {
    body: JSON.stringify(input),
    method: "POST"
  });
}

export function updateSopRule(ruleId: string, input: SopRuleInput): Promise<SopRule> {
  return requestJson<SopRule>(`/api/sop-rules/${ruleId}`, {
    body: JSON.stringify(input),
    method: "PUT"
  });
}

export function deleteSopRule(ruleId: string): Promise<void> {
  return requestVoid(`/api/sop-rules/${ruleId}`, {
    method: "DELETE"
  });
}
