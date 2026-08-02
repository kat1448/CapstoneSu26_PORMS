import type { AuthSession, ChangePasswordInput } from "../types/auth";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000").replace(/\/$/, "");
const SESSION_KEY = "porms.auth.session";
const VALID_ROLES = new Set(["ADMIN", "PORT_MANAGER", "OPERATOR"]);
export const AUTH_SESSION_EXPIRED_EVENT = "porms.auth.session-expired";

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
  clearSession();

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
  if (!response.ok) {
    clearSession();
    throw new Error(await readError(response));
  }

  const session = await response.json() as unknown;
  if (!isValidSession(session)) {
    clearSession();
    throw new Error("Phiên đăng nhập không hợp lệ. Hãy kiểm tra phiên bản database và role người dùng.");
  }

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
    const session = JSON.parse(raw) as unknown;
    if (!isValidSession(session)) {
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

export function expireSession() {
  clearSession();
  window.dispatchEvent(new Event(AUTH_SESSION_EXPIRED_EVENT));
}

function isValidSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== "object") return false;

  const session = value as Partial<AuthSession>;
  const expiresAt = Date.parse(session.expiresAt ?? "");
  const user = session.user;

  return isNonEmptyString(session.accessToken)
    && isNonEmptyString(session.refreshToken)
    && Number.isFinite(expiresAt)
    && expiresAt > Date.now()
    && Boolean(user)
    && isNonEmptyString(user?.email)
    && isNonEmptyString(user?.name)
    && isNonEmptyString(user?.initials)
    && typeof user?.portName === "string"
    && typeof user?.role === "string"
    && VALID_ROLES.has(user.role);
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}
