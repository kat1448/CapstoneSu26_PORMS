import { describe, expect, it } from "vitest";
import { getNavigationForRole, isRouteAllowed } from "./navigation";

describe("role navigation", () => {
  it("shows every group to ADMIN", () => {
    const groups = getNavigationForRole("ADMIN");

    expect(groups.map((group) => group.label)).toEqual([
      "Vận hành",
      "Quản lý",
      "Cấu hình",
      "Công cụ & báo cáo"
    ]);
    const paths = groups.flatMap((group) => group.items.map((item) => item.path));
    expect(paths).toContain("/users");
    expect(paths).toContain("/ports");
    expect(paths).toContain("/simulation");
    expect(paths).toContain("/forecast-planning");
    expect(paths).toContain("/ai-long-range-forecast");
    expect(paths).toContain("/forecast-evaluation");
  });

  it("allows PORT_MANAGER to configure operational rules without user or port administration", () => {
    const paths = getNavigationForRole("PORT_MANAGER")
      .flatMap((group) => group.items.map((item) => item.path));

    expect(paths).toContain("/dashboard");
    expect(paths).toContain("/alerts");
    expect(paths).toContain("/tasks");
    expect(paths).toContain("/operation-log");
    expect(paths).toContain("/risk-config");
    expect(paths).toContain("/sop-rules");
    expect(paths).toContain("/forecast-planning");
    expect(paths).toContain("/ai-long-range-forecast");
    expect(paths).toContain("/forecast-evaluation");
    expect(paths).not.toContain("/users");
    expect(paths).not.toContain("/ports");
    expect(paths).not.toContain("/simulation");
  });

  it("limits OPERATOR to operational read-only views", () => {
    const paths = getNavigationForRole("OPERATOR")
      .flatMap((group) => group.items.map((item) => item.path));

    expect(paths).toEqual(["/dashboard", "/alerts", "/tasks", "/operation-log"]);
  });

  it("rejects direct access to disallowed routes", () => {
    expect(isRouteAllowed("OPERATOR", "/operation-log")).toBe(true);
    expect(isRouteAllowed("OPERATOR", "/tasks")).toBe(true);
    expect(isRouteAllowed("OPERATOR", "/ports/new")).toBe(false);
    expect(isRouteAllowed("OPERATOR", "/risk-config")).toBe(false);
    expect(isRouteAllowed("OPERATOR", "/sop-rules")).toBe(false);
    expect(isRouteAllowed("OPERATOR", "/simulation")).toBe(false);
    expect(isRouteAllowed("OPERATOR", "/simulation-results")).toBe(false);
    expect(isRouteAllowed("PORT_MANAGER", "/users")).toBe(false);
    expect(isRouteAllowed("PORT_MANAGER", "/ports")).toBe(false);
    expect(isRouteAllowed("PORT_MANAGER", "/forecast-planning")).toBe(true);
    expect(isRouteAllowed("PORT_MANAGER", "/ai-long-range-forecast")).toBe(true);
    expect(isRouteAllowed("PORT_MANAGER", "/forecast-evaluation")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/ports/new")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users/new")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users/user-1/edit")).toBe(true);
  });
});
