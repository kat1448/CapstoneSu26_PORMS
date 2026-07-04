import { afterEach, describe, expect, it, vi } from "vitest";
import { getOpenWeatherForecast } from "./weatherService";

describe("weatherService", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("loads five-day OpenWeather forecast for a port", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      days: [],
      fetchedAt: "2026-07-02T00:00:00Z",
      portCode: "DNTSA",
      portName: "Cảng Tiên Sa"
    }), { status: 200 })));

    await getOpenWeatherForecast("DNTSA", 5);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/weather/forecast?days=5&portCode=DNTSA",
      expect.any(Object)
    );
  });
});
