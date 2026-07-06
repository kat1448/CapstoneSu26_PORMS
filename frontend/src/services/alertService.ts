import { getAlerts as getAlertsData } from "../mock/demoData";
import { formatTimeLabel, requestJson, withMockFallback } from "./api";
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
