import { afterEach, describe, expect, it, vi } from "vitest";
import { formatTimeLabel, requestJson } from "./api";
import { AUTH_SESSION_EXPIRED_EVENT } from "./authService";

describe("formatTimeLabel", () => {
  it("includes day month year and time", () => {
    expect(formatTimeLabel("2026-06-19T14:30:25")).toBe("19/06/2026 14:30:25");
  });
});

describe("requestJson", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("surfaces API error response messages", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ error: "No OpenWeather data exists for port DNTSA." }),
      { headers: { "Content-Type": "application/json" }, status: 404 }
    )));

    await expect(requestJson("/api/simulation/forecast-plan", { method: "POST" }))
      .rejects.toThrow("No OpenWeather data exists for port DNTSA.");
  });

  it("clears and announces an expired session when the API returns 401", async () => {
    const sessionExpired = vi.fn();
    window.addEventListener(AUTH_SESSION_EXPIRED_EVENT, sessionExpired, { once: true });
    localStorage.setItem("porms.auth.session", "stored-session");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ message: "Unauthorized" }),
      { headers: { "Content-Type": "application/json" }, status: 401 }
    )));

    await expect(requestJson("/api/dashboard/summary")).rejects.toThrow("Unauthorized");

    expect(localStorage.getItem("porms.auth.session")).toBeNull();
    expect(sessionExpired).toHaveBeenCalledOnce();
  });
});
