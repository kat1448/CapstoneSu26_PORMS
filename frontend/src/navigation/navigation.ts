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

const allRoles: DemoUserRole[] = ["SUPER_ADMIN", "ADMIN", "STANDARD_USER"];
const superAdminOnly: DemoUserRole[] = ["SUPER_ADMIN"];
const adminRoles: DemoUserRole[] = ["SUPER_ADMIN", "ADMIN"];

export const navigationGroups: NavigationGroup[] = [
  {
    label: "Vận hành",
    items: [
      { icon: "dashboard", label: "Dashboard", path: "/dashboard", roles: allRoles },
      { icon: "alert", label: "Cảnh báo", path: "/alerts", roles: allRoles },
      { icon: "tasks", label: "Nhật ký nhiệm vụ", path: "/tasks", roles: adminRoles },
      { icon: "log", label: "Nhật ký vận hành", path: "/operation-log", roles: adminRoles }
    ]
  },
  {
    label: "Quản lý",
    items: [
      { icon: "port", label: "Cảng & khu vực", path: "/ports", roles: allRoles },
      { icon: "users", label: "Người dùng", path: "/users", roles: superAdminOnly }
    ]
  },
  {
    label: "Cấu hình",
    items: [
      { icon: "settings", label: "Ngưỡng rủi ro", path: "/risk-config", roles: adminRoles },
      { icon: "rules", label: "Quy tắc SOP", path: "/sop-rules", roles: adminRoles }
    ]
  },
  {
    label: "Công cụ & báo cáo",
    items: [
      { icon: "play", label: "Mô phỏng", path: "/simulation", roles: allRoles },
      { icon: "chart", label: "Kết quả mô phỏng", path: "/simulation-results", roles: allRoles },
      { icon: "chart", label: "Dự báo vận hành", path: "/forecast-planning", roles: adminRoles },
      { icon: "chart", label: "Phân tích BI", path: "/analytics", roles: adminRoles }
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

  if (pathname === "/ports/new") {
    return superAdminOnly.includes(role);
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
