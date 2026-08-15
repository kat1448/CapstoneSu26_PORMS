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
const adminOnly: DemoUserRole[] = ["ADMIN"];
const adminAndPortManager: DemoUserRole[] = ["ADMIN", "PORT_MANAGER"];

export const navigationGroups: NavigationGroup[] = [
  {
    label: "Vận hành",
    items: [
      { icon: "dashboard", label: "Tổng quan", path: "/dashboard", roles: allRoles },
      { icon: "alert", label: "Cảnh báo", path: "/alerts", roles: allRoles },
      { icon: "tasks", label: "Nhiệm vụ", path: "/tasks", roles: allRoles },
      { icon: "log", label: "Lịch sử vận hành", path: "/operation-log", roles: allRoles }
    ]
  },
  {
    label: "Quản lý",
    items: [
      { icon: "port", label: "Cảng & khu vực", path: "/ports", roles: adminOnly },
      { icon: "users", label: "Nhân sự cảng", path: "/users", roles: adminAndPortManager }
    ]
  },
  {
    label: "Cấu hình",
    items: [
      { icon: "settings", label: "Mức cảnh báo", path: "/risk-config", roles: adminAndPortManager },
      { icon: "rules", label: "Quy trình ứng phó", path: "/sop-rules", roles: adminAndPortManager }
    ]
  },
  {
    label: "Phân tích & kế hoạch",
    items: [
      { icon: "play", label: "Mô phỏng", path: "/simulation", roles: adminOnly },
      { icon: "chart", label: "Dự báo vận hành", path: "/forecast-planning", roles: adminAndPortManager },
      { icon: "chart", label: "Dự báo dài hạn", path: "/ai-long-range-forecast", roles: adminAndPortManager },
      { icon: "chart", label: "Đánh giá dự báo", path: "/forecast-evaluation", roles: adminAndPortManager },
      { icon: "chart", label: "Báo cáo vận hành", path: "/reports", roles: adminAndPortManager }
    ]
  }
];

export function getNavigationForRole(role: DemoUserRole): NavigationGroup[] {
  return navigationGroups
    .map((group) => ({ ...group, items: group.items.filter((item) => item.roles.includes(role)) }))
    .filter((group) => group.items.length > 0);
}

export function isRouteAllowed(role: DemoUserRole, pathname: string): boolean {
  if (pathname === "/profile" || pathname === "/change-password" || pathname === "/notification-settings") {
    return true;
  }

  if (pathname === "/simulation-results") {
    return adminOnly.includes(role);
  }

  if (pathname === "/ports/new") {
    return adminOnly.includes(role);
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

