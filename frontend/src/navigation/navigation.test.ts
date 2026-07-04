import { describe, expect, it } from "vitest";
import { getNavigationForRole, isRouteAllowed } from "./navigation";

describe("role navigation", () => {
  it("shows every group to SUPER_ADMIN", () => {
    const groups = getNavigationForRole("SUPER_ADMIN");

    expect(groups.map((group) => group.label)).toEqual([
      "Vận hành",
      "Quản lý",
      "Cấu hình",
      "Công cụ & báo cáo"
    ]);
    expect(groups.flatMap((group) => group.items.map((item) => item.path))).toContain("/users");
    expect(groups.flatMap((group) => group.items.map((item) => item.path))).toContain("/forecast-planning");
  });

  it("allows ADMIN to configure real SOP data and tasks without user administration", () => {
    const paths = getNavigationForRole("ADMIN")
      .flatMap((group) => group.items.map((item) => item.path));

    expect(paths).toContain("/tasks");
    expect(paths).toContain("/risk-config");
    expect(paths).toContain("/sop-rules");
    expect(paths).not.toContain("/users");
  });

  it("limits STANDARD_USER to read-only views and simulation", () => {
    const paths = getNavigationForRole("STANDARD_USER")
      .flatMap((group) => group.items.map((item) => item.path));

    expect(paths).toEqual(["/dashboard", "/alerts", "/ports", "/simulation", "/simulation-results"]);
  });

  it("rejects direct access to disallowed routes", () => {
    expect(isRouteAllowed("STANDARD_USER", "/operation-log")).toBe(false);
    expect(isRouteAllowed("STANDARD_USER", "/tasks")).toBe(false);
    expect(isRouteAllowed("STANDARD_USER", "/ports/new")).toBe(false);
    expect(isRouteAllowed("STANDARD_USER", "/simulation")).toBe(true);
    expect(isRouteAllowed("STANDARD_USER", "/simulation-results")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users")).toBe(false);
    expect(isRouteAllowed("SUPER_ADMIN", "/users")).toBe(true);
    expect(isRouteAllowed("SUPER_ADMIN", "/ports/new")).toBe(true);
    expect(isRouteAllowed("SUPER_ADMIN", "/users/new")).toBe(true);
    expect(isRouteAllowed("SUPER_ADMIN", "/users/user-1/edit")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/forecast-planning")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users/new")).toBe(false);
  });
});
