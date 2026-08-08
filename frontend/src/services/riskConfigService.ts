import { requestJson, requestVoid } from "./api";
import { expireSession, getStoredSession } from "./authService";
import { getApiUrl } from "./api";

export type RiskLevel = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type WeatherFactor = "WIND" | "RAIN" | "WAVE" | "VISIBILITY";
export type ThresholdOperator = ">=" | ">" | "<=" | "<";

export type RiskThreshold = {
  comparisonOperator: ThresholdOperator;
  description: string | null;
  factor: WeatherFactor;
  id?: string;
  isEnabled: boolean;
  riskLevel: RiskLevel;
  thresholdValue: number;
  unit: string;
  updatedAt?: string;
  version: number;
};

export type ZoneThresholdOverride = {
  comparisonOperator: ThresholdOperator;
  factor: WeatherFactor;
  id: string;
  isEnabled: boolean;
  riskLevel: RiskLevel;
  thresholdValue: number;
  unit: string;
  updatedAt?: string;
  zoneId: string;
  zoneName: string;
  zoneType: string;
};

export type RiskConfigZone = {
  portName: string;
  zoneId: string;
  zoneName: string;
  zoneType: string;
};

export type RiskConfigResponse = {
  thresholds: RiskThreshold[];
  zoneOverrides: ZoneThresholdOverride[];
  zones: RiskConfigZone[];
};

export type SaveRiskThresholdsInput = {
  changeReason?: string | null;
  thresholds: RiskThreshold[];
};

export type SaveZoneThresholdOverridesInput = {
  changeReason?: string | null;
  overrides: Array<Omit<ZoneThresholdOverride, "id" | "updatedAt" | "zoneId" | "zoneName" | "zoneType">>;
};

export type RiskThresholdImportAction =
  | "CREATE"
  | "UPDATE"
  | "UNCHANGED"
  | "INVALID";

export type RiskThresholdImportError = {
  column: string;
  message: string;
  rowNumber: number;
};

export type RiskThresholdImportExistingValue = {
  comparisonOperator: string;
  description: string | null;
  isEnabled: boolean;
  thresholdValue: number;
  unit: string;
};

export type RiskThresholdImportRow = {
  action: RiskThresholdImportAction;
  comparisonOperator: string | null;
  description: string | null;
  errors: RiskThresholdImportError[];
  existingValue: RiskThresholdImportExistingValue | null;
  factor: string | null;
  isEnabled: boolean | null;
  riskLevel: string | null;
  rowNumber: number;
  thresholdValue: number | null;
  unit: string | null;
};

export type RiskThresholdImportPreview = {
  canImport: boolean;
  createCount: number;
  errors: RiskThresholdImportError[];
  fileName: string;
  invalidRows: number;
  rows: RiskThresholdImportRow[];
  totalRows: number;
  unchangedCount: number;
  updateCount: number;
  validRows: number;
};

export type RiskThresholdImportResponse = {
  configuration: RiskConfigResponse;
  createdCount: number;
  fileName: string;
  unchangedCount: number;
  updatedCount: number;
};

export type ConfirmRiskThresholdImportResult =
  | {
      succeeded: true;
      response: RiskThresholdImportResponse;
    }
  | {
      succeeded: false;
      preview: RiskThresholdImportPreview;
    };

export function getRiskConfig(): Promise<RiskConfigResponse> {
  return requestJson<RiskConfigResponse>("/api/risk/thresholds");
}

export function saveRiskThresholds(input: SaveRiskThresholdsInput): Promise<RiskConfigResponse> {
  return requestJson<RiskConfigResponse>("/api/risk/thresholds", {
    body: JSON.stringify(input),
    method: "PUT"
  });
}

export function saveZoneThresholdOverrides(zoneId: string, input: SaveZoneThresholdOverridesInput): Promise<RiskConfigResponse> {
  return requestJson<RiskConfigResponse>(`/api/risk/zones/${zoneId}/threshold-overrides`, {
    body: JSON.stringify(input),
    method: "PUT"
  });
}

export function deleteZoneThresholdOverride(zoneId: string, overrideId: string): Promise<void> {
  return requestVoid(`/api/risk/zones/${zoneId}/threshold-overrides/${overrideId}`, {
    method: "DELETE"
  });
}

export async function getRiskThresholdTemplate(): Promise<Blob> {
  const response = await sendRiskThresholdImportRequest(
    "/api/risk/thresholds/import-template",
    { method: "GET" }
  );

  if (!response.ok) {
    throw new Error(
      await readRiskThresholdImportError(
        response,
        "Không thể tải template ngưỡng rủi ro."
      )
    );
  }

  return response.blob();
}

/**
 * Kiểm tra toàn bộ file Excel nhưng không ghi dữ liệu vào database.
 */
export async function previewRiskThresholdImport(
  file: File
): Promise<RiskThresholdImportPreview> {
  const formData = new FormData();
  formData.append("File", file);

  const response = await sendRiskThresholdImportRequest(
    "/api/risk/thresholds/import/preview",
    {
      body: formData,
      method: "POST"
    }
  );

  if (!response.ok) {
    throw new Error(
      await readRiskThresholdImportError(
        response,
        "Không thể kiểm tra file ngưỡng rủi ro."
      )
    );
  }

  return (await response.json()) as RiskThresholdImportPreview;
}

/**
 * Backend kiểm tra lại file và database trước khi ghi toàn bộ transaction.
 * HTTP 400 được trả về dưới dạng preview để giao diện hiển thị lỗi chi tiết.
 */
export async function confirmRiskThresholdImport(
  file: File,
  changeReason: string
): Promise<ConfirmRiskThresholdImportResult> {
  const formData = new FormData();
  formData.append("File", file);
  formData.append("ChangeReason", changeReason);

  const response = await sendRiskThresholdImportRequest(
    "/api/risk/thresholds/import",
    {
      body: formData,
      method: "POST"
    }
  );

  if (response.status === 400) {
    return {
      succeeded: false,
      preview: (await response.json()) as RiskThresholdImportPreview
    };
  }

  if (!response.ok) {
    throw new Error(
      await readRiskThresholdImportError(
        response,
        "Không thể nhập ngưỡng rủi ro."
      )
    );
  }

  return {
    succeeded: true,
    response: (await response.json()) as RiskThresholdImportResponse
  };
}

/**
 * Gửi request Excel kèm JWT nhưng để trình duyệt tự tạo multipart boundary.
 */
async function sendRiskThresholdImportRequest(
  path: string,
  init: RequestInit
): Promise<Response> {
  const session = getStoredSession();

  if (!session) {
    throw new Error("Phiên đăng nhập không tồn tại hoặc đã hết hạn.");
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
 * Ưu tiên thông báo lỗi do API trả về trước khi dùng nội dung dự phòng.
 */
async function readRiskThresholdImportError(
  response: Response,
  fallback: string
): Promise<string> {
  try {
    const payload = (await response.json()) as {
      error?: unknown;
      message?: unknown;
    };

    if (typeof payload.message === "string" && payload.message.trim()) {
      return payload.message;
    }

    if (typeof payload.error === "string" && payload.error.trim()) {
      return payload.error;
    }
  } catch {
    // Response không phải JSON, sử dụng thông báo dự phòng.
  }

  return fallback;
}
