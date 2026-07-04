import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getOperationEvents } from "./logService";

describe("logService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("loads live operation events by default", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));

    await getOperationEvents();

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/operation-events",
      expect.any(Object)
    );
  });

  it("loads simulation operation events with a separate scope", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));

    await getOperationEvents("simulation");

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/operation-events?scope=simulation",
      expect.any(Object)
    );
  });
});
