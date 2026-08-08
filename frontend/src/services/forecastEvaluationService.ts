import { requestJson } from "./api";
import { getStoredSession } from "./authService";
import type { ForecastEvaluationResponse } from "../types/forecastEvaluation";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000").replace(/\/$/, "");

export type ForecastEvaluationFilters = {
  from?: string;
  portCode?: string;
  to?: string;
};

function buildQuery(filters: ForecastEvaluationFilters) {
  const params = new URLSearchParams();
  if (filters.portCode && filters.portCode !== "ALL") params.set("portCode", filters.portCode);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  const query = params.toString();
  return query ? `?${query}` : "";
}

export function getForecastEvaluation(filters: ForecastEvaluationFilters): Promise<ForecastEvaluationResponse> {
  return requestJson<ForecastEvaluationResponse>(`/api/forecast-evaluation${buildQuery(filters)}`);
}

export function getForecastInterventionDemo(): Promise<ForecastEvaluationResponse> {
  return requestJson<ForecastEvaluationResponse>("/api/forecast-evaluation/demo");
}

export async function exportForecastEvaluation(filters: ForecastEvaluationFilters): Promise<void> {
  const session = getStoredSession();
  const response = await fetch(`${API_BASE_URL}/api/forecast-evaluation/export${buildQuery(filters)}`, {
    headers: {
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {})
    }
  });
  if (!response.ok) {
    throw new Error(`API /api/forecast-evaluation/export failed with ${response.status}`);
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = getFileName(response.headers.get("content-disposition"));
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function getFileName(contentDisposition: string | null) {
  const match = contentDisposition?.match(/filename="?([^"]+)"?/i);
  return match?.[1] ?? "forecast-evaluation.csv";
}
