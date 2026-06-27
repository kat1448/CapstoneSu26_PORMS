import type { DemoUserRole } from "../App";

export type IconName =
  | "alert"
  | "chart"
  | "dashboard"
  | "log"
  | "play"
  | "port"
  | "rules"
  | "settings"
  | "tasks"
  | "users";

export type NavigationItem = {
  icon: IconName;
  label: string;
  path: string;
  roles: DemoUserRole[];
};

export type NavigationGroup = {
  items: NavigationItem[];
  label: string;
};

const allRoles: DemoUserRole[] = ["ADMIN", "PORT_MANAGER", "OPERATOR"];
const adminAndManager: DemoUserRole[] = ["ADMIN", "PORT_MANAGER"];

export const navigationGroups: NavigationGroup[] = [
  {
    label: "Vận hành",
    items: [
      { icon: "dashboard", label: "Dashboard", path: "/dashboard", roles: allRoles },
      { icon: "alert", label: "Cảnh báo", path: "/alerts", roles: allRoles },
      { icon: "tasks", label: "Nhật ký nhiệm vụ", path: "/tasks", roles: allRoles },
      { icon: "log", label: "Nhật ký vận hành", path: "/operation-log", roles: adminAndManager }
    ]
  },
  {
    label: "Quản lý",
    items: [
      { icon: "port", label: "Cảng & khu vực", path: "/ports", roles: allRoles },
      { icon: "users", label: "Người dùng", path: "/users", roles: ["ADMIN"] }
    ]
  },
  {
    label: "Cấu hình",
    items: [
      { icon: "settings", label: "Ngưỡng rủi ro", path: "/risk-config", roles: adminAndManager },
      { icon: "rules", label: "Quy tắc SOP", path: "/sop-rules", roles: adminAndManager }
    ]
  },
  {
    label: "Công cụ & báo cáo",
    items: [
      { icon: "play", label: "Mô phỏng", path: "/simulation", roles: adminAndManager },
      { icon: "chart", label: "Phân tích BI", path: "/analytics", roles: adminAndManager }
    ]
  }
];

export function getNavigationForRole(role: DemoUserRole): NavigationGroup[] {
  return navigationGroups
    .map((group) => ({ ...group, items: group.items.filter((item) => item.roles.includes(role)) }))
    .filter((group) => group.items.length > 0);
}

export function isRouteAllowed(role: DemoUserRole, pathname: string): boolean {
  if (pathname === "/profile" || pathname === "/change-password") {
    return true;
  }
  if (pathname === "/simulation-results") {
    return role !== "OPERATOR";
  }

  const normalizedPath = pathname.startsWith("/ports/")
    ? "/ports"
    : pathname.startsWith("/users/")
      ? "/users"
      : pathname;
  return navigationGroups
    .flatMap((group) => group.items)
    .some((item) => item.path === normalizedPath && item.roles.includes(role));
}
