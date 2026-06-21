import type { AuthSession, ChangePasswordInput } from "../types/auth";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000").replace(/\/$/, "");
const SESSION_KEY = "porms.auth.session";

async function readError(response: Response) {
  const fallback = `Máy chủ trả về lỗi ${response.status}.`;
  try {
    const payload = await response.json() as { error?: string; message?: string };
    return payload.message ?? payload.error ?? fallback;
  } catch {
    return fallback;
  }
}

export async function login(email: string, password: string): Promise<AuthSession> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password })
    });
  } catch {
    throw new Error("Không thể kết nối tới máy chủ.");
  }
  if (!response.ok) throw new Error(await readError(response));

  const session = await response.json() as AuthSession;
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  return session;
}

export async function changePassword(input: ChangePasswordInput): Promise<void> {
  const session = getStoredSession();
  const response = await fetch(`${API_BASE_URL}/api/auth/change-password`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {})
    },
    body: JSON.stringify(input)
  });
  if (!response.ok) throw new Error(await readError(response));
}

export function getStoredSession(): AuthSession | null {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) return null;

  try {
    const session = JSON.parse(raw) as AuthSession;
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      clearSession();
      return null;
    }
    return session;
  } catch {
    clearSession();
    return null;
  }
}

export function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}
