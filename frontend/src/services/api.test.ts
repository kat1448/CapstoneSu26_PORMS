import { afterEach, describe, expect, it, vi } from "vitest";
import { formatTimeLabel, requestJson } from "./api";

describe("formatTimeLabel", () => {
  it("includes day month year and time", () => {
    expect(formatTimeLabel("2026-06-19T14:30:25")).toBe("19/06/2026 14:30:25");
  });
});

describe("requestJson", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("surfaces API error response messages", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ error: "No OpenWeather data exists for port DNTSA." }),
      { headers: { "Content-Type": "application/json" }, status: 404 }
    )));

    await expect(requestJson("/api/simulation/forecast-plan", { method: "POST" }))
      .rejects.toThrow("No OpenWeather data exists for port DNTSA.");
  });
});
