import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clearSession, getStoredSession, login } from "./authService";
import type { AuthSession } from "../types/auth";

const SESSION_KEY = "porms.auth.session";

const validSession: AuthSession = {
  accessToken: "access-token",
  refreshToken: "refresh-token",
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: {
    id: "11111111-1111-1111-1111-111111111111",
    email: "admin@porms.vn",
    initials: "NH",
    name: "Nguyen Van Hung",
    portName: "Cang Tien Sa",
    role: "ADMIN"
  }
};

describe("authService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("stores the returned session after successful login", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(validSession), { status: 200 }));

    await expect(login("admin@porms.vn", "Admin@2026!")).resolves.toEqual(validSession);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "admin@porms.vn", password: "Admin@2026!" })
    });
    expect(JSON.parse(localStorage.getItem(SESSION_KEY) ?? "{}")).toEqual(validSession);
  });

  it("surfaces the backend login error message", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      new Response(JSON.stringify({ message: "Email hoac mat khau khong dung." }), { status: 401 })
    );

    await expect(login("admin@porms.vn", "bad-password")).rejects.toThrow("Email hoac mat khau khong dung.");
    expect(localStorage.getItem(SESSION_KEY)).toBeNull();
  });

  it("reports connection failures without storing a session", async () => {
    vi.mocked(fetch).mockRejectedValueOnce(new Error("network down"));

    await expect(login("admin@porms.vn", "Admin@2026!")).rejects.toThrow("Không thể kết nối tới máy chủ.");
    expect(localStorage.getItem(SESSION_KEY)).toBeNull();
  });

  it("returns a valid stored session", () => {
    localStorage.setItem(SESSION_KEY, JSON.stringify(validSession));

    expect(getStoredSession()).toEqual(validSession);
  });

  it("clears expired stored sessions", () => {
    localStorage.setItem(
      SESSION_KEY,
      JSON.stringify({ ...validSession, expiresAt: new Date(Date.now() - 1_000).toISOString() })
    );

    expect(getStoredSession()).toBeNull();
    expect(localStorage.getItem(SESSION_KEY)).toBeNull();
  });

  it("clears malformed stored sessions", () => {
    localStorage.setItem(SESSION_KEY, "{");

    expect(getStoredSession()).toBeNull();
    expect(localStorage.getItem(SESSION_KEY)).toBeNull();
  });

  it("clears the session on logout", () => {
    localStorage.setItem(SESSION_KEY, JSON.stringify(validSession));

    clearSession();

    expect(localStorage.getItem(SESSION_KEY)).toBeNull();
  });
});
