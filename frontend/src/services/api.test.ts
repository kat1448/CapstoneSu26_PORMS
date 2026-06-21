import { describe, expect, it } from "vitest";
import { formatTimeLabel } from "./api";

describe("formatTimeLabel", () => {
  it("includes day month year and time", () => {
    expect(formatTimeLabel("2026-06-19T14:30:25")).toBe("19/06/2026 14:30:25");
  });
});
