import { getAlerts as getAlertsData } from "../mock/demoData";
import { formatTimeLabel, getApiUrl, requestJson, requestVoid, withMockFallback } from "./api";
import { getStoredSession } from "./authService";
import type { TaskLogRecord } from "./taskService";
import type { AlertItem } from "../types/alert";

type AlertApiResponse = Omit<AlertItem, "createdAt" | "zoneName"> & {
  createdAt: string;
  zoneName: string | null;
};

function normalize(alerts: AlertApiResponse[]): AlertItem[] {
  return alerts.map((alert) => ({
    ...alert,
    createdAt: formatTimeLabel(alert.createdAt),
    zoneName: alert.zoneName ?? "Toàn cảng"
  }));
}

function normalizeOne(alert: AlertApiResponse): AlertItem {
  return normalize([alert])[0];
}

export async function getAlerts(): Promise<AlertItem[]> {
  return withMockFallback(
    async () => normalize(await requestJson<AlertApiResponse[]>("/api/alerts")),
    () => normalize(getAlertsData())
  );
}

export async function getAlert(alertId: string): Promise<AlertItem> {
  return requestJson<AlertApiResponse>(`/api/alerts/${alertId}`).then(normalizeOne);
}

export async function getAlertTasks(alertId: string): Promise<TaskLogRecord[]> {
  return requestJson<TaskLogRecord[]>(`/api/alerts/${alertId}/tasks`);
}

export async function acknowledgeAlert(alertId: string): Promise<void> {
  await requestVoid(`/api/alerts/${alertId}/acknowledge`, { method: "PATCH" });
}

export function getAlertSpeechUrl(alertId: string): string {
  return getApiUrl(`/api/alerts/${encodeURIComponent(alertId)}/speech`);
}

export async function getAlertSpeechAudio(alertId: string): Promise<Blob> {
  const session = getStoredSession();
  const response = await fetch(getAlertSpeechUrl(alertId), {
    headers: session ? { Authorization: `Bearer ${session.accessToken}` } : {}
  });

  if (!response.ok) {
    throw new Error(`Speech API returned ${response.status}`);
  }

  return response.blob();
}
