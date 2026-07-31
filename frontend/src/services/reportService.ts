import { getApiUrl, requestJson } from "./api";
import { getStoredSession } from "./authService";

export type ReportType = "ALERTS" | "TASKS" | "EVENTS";
export type ReportFilters = { type: ReportType; portCode?: string; zoneName?: string; from?: string; to?: string; riskLevel?: string };
export type ReportRow = { occurredAt: string; portCode: string; portName: string; zoneName: string; riskLevel?: string | null; subject: string; description: string; owner: string; status: string };
export type ReportPreview = { reportType: ReportType; totalRows: number; rows: ReportRow[] };

function query(filters: ReportFilters) {
  const params = new URLSearchParams({ type: filters.type });
  for (const [key, value] of Object.entries(filters)) if (key !== "type" && value && value !== "ALL") params.set(key, value);
  return params.toString();
}

export function getReportPreview(filters: ReportFilters) { return requestJson<ReportPreview>(`/api/reports/preview?${query(filters)}`); }

export async function downloadReport(filters: ReportFilters, format: "xlsx" | "pdf") {
  const session = getStoredSession();
  const response = await fetch(getApiUrl(`/api/reports/export/${format}?${query(filters)}`), { headers: session ? { Authorization: `Bearer ${session.accessToken}` } : {} });
  if (!response.ok) throw new Error(`Không thể tạo báo cáo (${response.status}).`);
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a"); link.href = url; link.download = response.headers.get("content-disposition")?.match(/filename="?([^";]+)"?/)?.[1] ?? `porms-report.${format}`; link.click(); URL.revokeObjectURL(url);
}
