import { describe, expect, it } from "vitest";
import { getNavigationForRole, isRouteAllowed } from "./navigation";

describe("role navigation", () => {
  it("shows every prototype group to ADMIN", () => {
    const groups = getNavigationForRole("ADMIN");

    expect(groups.map((group) => group.label)).toEqual([
      "Vận hành",
      "Quản lý",
      "Cấu hình",
      "Công cụ & báo cáo"
    ]);
    expect(groups.flatMap((group) => group.items.map((item) => item.path))).toContain("/users");
  });

  it("limits OPERATOR to operational routes", () => {
    const paths = getNavigationForRole("OPERATOR")
      .flatMap((group) => group.items.map((item) => item.path));

    expect(paths).toEqual(["/dashboard", "/alerts", "/tasks", "/ports"]);
  });

  it("rejects direct access to disallowed routes", () => {
    expect(isRouteAllowed("OPERATOR", "/operation-log")).toBe(false);
    expect(isRouteAllowed("PORT_MANAGER", "/users")).toBe(false);
    expect(isRouteAllowed("ADMIN", "/users")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users/new")).toBe(true);
    expect(isRouteAllowed("ADMIN", "/users/user-1/edit")).toBe(true);
    expect(isRouteAllowed("PORT_MANAGER", "/users/new")).toBe(false);
  });
});
