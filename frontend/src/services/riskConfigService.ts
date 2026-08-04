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
  const session = getStoredSession();

  if (!session) {
    throw new Error("Phiên đăng nhập không tồn tại.");
  }

  const response = await fetch(
    getApiUrl("/api/risk/thresholds/import-template"),
    {
      headers: {
        Authorization: `Bearer ${session.accessToken}`
      }
    }
  );

  if (response.status === 401) {
    expireSession();
  }

  if (!response.ok) {
    throw new Error("Không thể tải template ngưỡng rủi ro.");
  }

  return response.blob();
}