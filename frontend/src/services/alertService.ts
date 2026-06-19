import { getAlerts as getAlertsData } from "../mock/demoData";
import { formatTimeLabel, requestJson, withMockFallback } from "./api";
import type { AlertItem } from "../types/alert";

type AlertApiResponse = Omit<AlertItem, "createdAt" | "zoneName"> & {
  createdAt: string;
  zoneName: string | null;
};

export async function getAlerts(): Promise<AlertItem[]> {
  const normalize = (alerts: AlertApiResponse[]) => alerts.map((alert) => ({
    ...alert,
    createdAt: formatTimeLabel(alert.createdAt),
    zoneName: alert.zoneName ?? "Toàn cảng"
  }));

  return withMockFallback(
    async () => {
      const alerts = await requestJson<AlertApiResponse[]>("/api/alerts");
      return normalize(alerts);
    },
    () => normalize(getAlertsData())
  );
}
