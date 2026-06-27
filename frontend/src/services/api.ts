import { getStoredSession } from "./authService";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000").replace(/\/$/, "");
const USE_MOCK_FALLBACK = import.meta.env.VITE_USE_MOCK_FALLBACK !== "false";

export async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const session = getStoredSession();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    throw new Error(`API ${path} failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function requestVoid(path: string, init?: RequestInit): Promise<void> {
  const session = getStoredSession();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    throw new Error(`API ${path} failed with ${response.status}`);
  }
}

export async function withMockFallback<T>(request: () => Promise<T>, fallback: () => T | Promise<T>): Promise<T> {
  try {
    return await request();
  } catch (error) {
    if (!USE_MOCK_FALLBACK) {
      throw error;
    }

    return fallback();
  }
}

export function formatTimeLabel(value: string | null | undefined): string {
  if (!value) {
    return "Chưa có dữ liệu";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const pad = (part: number) => String(part).padStart(2, "0");
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}
