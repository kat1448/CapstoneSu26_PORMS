import { getApiUrl, requestJson, requestVoid } from "./api";
import { expireSession, getStoredSession } from "./authService";
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
  actionConfigText?: string;
  actionType: SopActionType;
  appliesToZoneType: string | null;
  changeReason?: string | null;
  description: string | null;
  executionOrder?: number;
  isActive: boolean;
  previousRiskLevel: RiskLevel | null;
  ruleCode: string;
  ruleName: string;
  triggerRiskLevel: RiskLevel;
};

export type SopRuleImportAction =
  | "CREATE"
  | "UPDATE"
  | "UNCHANGED"
  | "INVALID";

export type SopRuleImportError = {
  column: string;
  message: string;
  rowNumber: number;
};

export type SopRuleImportExistingValue = {
  actionConfigJson: string;
  actionType: SopActionType;
  appliesToZoneType: string | null;
  description: string | null;
  executionOrder: number;
  id: string;
  isActive: boolean;
  previousRiskLevel: RiskLevel | null;
  ruleCode: string;
  ruleName: string;
  triggerRiskLevel: RiskLevel;
  version: number;
};

export type SopRuleImportRow = {
  action: SopRuleImportAction;
  actionConfigJson: string | null;
  actionType: string | null;
  appliesToZoneType: string | null;
  description: string | null;
  errors: SopRuleImportError[];
  executionOrder: number | null;
  existingValue: SopRuleImportExistingValue | null;
  isActive: boolean | null;
  previousRiskLevel: string | null;
  rowNumber: number;
  ruleCode: string | null;
  ruleName: string | null;
  triggerRiskLevel: string | null;
};

export type SopRuleImportPreview = {
  canImport: boolean;
  createCount: number;
  errors: SopRuleImportError[];
  fileName: string;
  invalidRows: number;
  rows: SopRuleImportRow[];
  totalRows: number;
  unchangedCount: number;
  updateCount: number;
  validRows: number;
};

export type SopRuleImportResponse = {
  configuration: SopRulesResponse;
  createdCount: number;
  fileName: string;
  importBatchId: string;
  unchangedCount: number;
  updatedCount: number;
};

export type ConfirmSopRuleImportResult =
  | {
      succeeded: true;
      response: SopRuleImportResponse;
    }
  | {
      succeeded: false;
      preview: SopRuleImportPreview;
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

/**
 * Tải template Excel chứa các SOP hiện tại.
 * Chỉ ADMIN được backend cho phép sử dụng endpoint này.
 */
export async function getSopRuleImportTemplate(): Promise<Blob> {
  const response = await sendSopImportRequest(
    "/api/sop-rules/import-template",
    { method: "GET" }
  );

  if (!response.ok) {
    throw new Error(
      await readSopImportError(
        response,
        "Không thể tải template SOP."
      )
    );
  }

  return response.blob();
}

/**
 * Kiểm tra file Excel nhưng không ghi dữ liệu vào database.
 */
export async function previewSopRuleImport(
  file: File
): Promise<SopRuleImportPreview> {
  const formData = new FormData();
  formData.append("File", file);

  const response = await sendSopImportRequest(
    "/api/sop-rules/import/preview",
    {
      body: formData,
      method: "POST"
    }
  );

  if (!response.ok) {
    throw new Error(
      await readSopImportError(
        response,
        "Không thể kiểm tra file SOP."
      )
    );
  }

  return (await response.json()) as SopRuleImportPreview;
}

/**
 * Backend sẽ đọc và kiểm tra lại file trước khi ghi transaction.
 * HTTP 400 được trả dưới dạng preview để giao diện hiển thị lỗi.
 */
export async function confirmSopRuleImport(
  file: File,
  changeReason: string
): Promise<ConfirmSopRuleImportResult> {
  const formData = new FormData();

  formData.append("File", file);
  formData.append("ChangeReason", changeReason);

  const response = await sendSopImportRequest(
    "/api/sop-rules/import",
    {
      body: formData,
      method: "POST"
    }
  );

  if (response.status === 400) {
    return {
      succeeded: false,
      preview:
        (await response.json()) as SopRuleImportPreview
    };
  }

  if (!response.ok) {
    throw new Error(
      await readSopImportError(
        response,
        "Không thể nhập quy tắc SOP."
      )
    );
  }

  return {
    succeeded: true,
    response:
      (await response.json()) as SopRuleImportResponse
  };
}

/**
 * Gửi request Excel có JWT nhưng không đặt Content-Type thủ công.
 * Trình duyệt phải tự tạo multipart boundary cho FormData.
 */
async function sendSopImportRequest(
  path: string,
  init: RequestInit
): Promise<Response> {
  const session = getStoredSession();

  if (!session) {
    throw new Error(
      "Phiên đăng nhập không tồn tại hoặc đã hết hạn."
    );
  }

  const response = await fetch(getApiUrl(path), {
    ...init,
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      ...(init.headers ?? {})
    }
  });

  if (response.status === 401) {
    expireSession();
  }

  return response;
}

/**
 * Đọc thông báo lỗi chuẩn của API trước khi dùng nội dung dự phòng.
 */
async function readSopImportError(
  response: Response,
  fallback: string
): Promise<string> {
  try {
    const payload = (await response.json()) as {
      error?: unknown;
      message?: unknown;
    };

    if (
      typeof payload.message === "string" &&
      payload.message.trim()
    ) {
      return payload.message;
    }

    if (
      typeof payload.error === "string" &&
      payload.error.trim()
    ) {
      return payload.error;
    }
  } catch {
    // Response không phải JSON, sử dụng thông báo dự phòng.
  }

  return fallback;
}
