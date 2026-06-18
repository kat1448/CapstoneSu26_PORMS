# PORMS Dashboard and Shell Fidelity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the React sidebar, topbar, Dashboard, role-based navigation, and new Tasks route match `001.design/design.html` while preserving current API-first data behavior.

**Architecture:** Extract navigation permissions into a typed frontend model shared by Sidebar and router guards. Keep the existing services and API contracts, but reshape Dashboard composition into prototype-aligned presentation components. Add Vitest and Testing Library coverage for role filtering, route protection, shell interactions, and Dashboard section ordering before changing implementation.

**Tech Stack:** React 18, TypeScript, React Router 6, Vite 5, Vitest, Testing Library, CSS

---

## File Structure

### Test Foundation

- Modify: `frontend/package.json`
- Modify: `frontend/vite.config.ts`
- Create: `frontend/src/test/setup.ts`

### Navigation and Routing

- Create: `frontend/src/navigation/navigation.ts`
- Create: `frontend/src/navigation/navigation.test.ts`
- Modify: `frontend/src/router/index.tsx`
- Create: `frontend/src/router/index.test.tsx`
- Create: `frontend/src/pages/TasksPage.tsx`
- Create: `frontend/src/pages/TasksPage.test.tsx`

### Shared Shell

- Create: `frontend/src/components/common/Icon.tsx`
- Modify: `frontend/src/components/layout/AppShell.tsx`
- Modify: `frontend/src/components/layout/Sidebar.tsx`
- Modify: `frontend/src/components/layout/Topbar.tsx`
- Create: `frontend/src/components/layout/AppShell.test.tsx`
- Create: `frontend/src/components/layout/Sidebar.test.tsx`
- Create: `frontend/src/components/layout/Topbar.test.tsx`

### Dashboard

- Modify: `frontend/src/pages/DashboardPage.tsx`
- Create: `frontend/src/pages/DashboardPage.test.tsx`
- Modify: `frontend/src/components/dashboard/RiskHeroCard.tsx`
- Modify: `frontend/src/components/dashboard/ModeCard.tsx`
- Modify: `frontend/src/components/dashboard/RiskTrendChart.tsx`
- Modify: `frontend/src/components/dashboard/WeatherSummaryCard.tsx`
- Modify: `frontend/src/components/dashboard/AlertListCard.tsx`
- Create: `frontend/src/components/dashboard/ZoneStatusCard.tsx`

### Styling and Text

- Modify: `frontend/src/styles/app.css`
- Modify: `frontend/src/styles/layout.css`
- Modify: `frontend/src/styles/pages.css`
- Modify: `frontend/src/styles/components.css`
- Modify touched shell and Dashboard files to contain valid UTF-8 Vietnamese.

---

### Task 1: Add Frontend Component Test Infrastructure

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/vite.config.ts`
- Create: `frontend/src/test/setup.ts`

- [ ] **Step 1: Add the test command and test dependencies**

Update `frontend/package.json`:

```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc --noEmit -p tsconfig.app.json && vite build",
    "test": "vitest run",
    "test:watch": "vitest",
    "preview": "vite preview"
  },
  "devDependencies": {
    "@testing-library/jest-dom": "^6.6.3",
    "@testing-library/react": "^16.1.0",
    "@testing-library/user-event": "^14.5.2",
    "@types/react": "^18.3.3",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.1",
    "autoprefixer": "^10.4.20",
    "jsdom": "^25.0.1",
    "postcss": "^8.4.49",
    "tailwindcss": "^3.4.17",
    "typescript": "^5.5.4",
    "vite": "^5.3.4",
    "vitest": "^2.1.8"
  }
}
```

- [ ] **Step 2: Configure Vitest**

Update `frontend/vite.config.ts` to export:

```ts
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts"
  }
});
```

- [ ] **Step 3: Add DOM matchers**

Create `frontend/src/test/setup.ts`:

```ts
import "@testing-library/jest-dom/vitest";
```

- [ ] **Step 4: Install and verify the test runner**

Run:

```powershell
cd frontend
npm install
npm test -- --passWithNoTests
```

Expected: exit code `0`.

- [ ] **Step 5: Commit**

```powershell
git add frontend/package.json frontend/package-lock.json frontend/vite.config.ts frontend/src/test/setup.ts
git commit -m "test: add frontend component test setup"
```

---

### Task 2: Define Typed Navigation and Role Permissions

**Files:**
- Create: `frontend/src/navigation/navigation.ts`
- Create: `frontend/src/navigation/navigation.test.ts`
- Modify: `frontend/src/App.tsx`

- [ ] **Step 1: Write failing role-filter tests**

Create `frontend/src/navigation/navigation.test.ts`:

```ts
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

  it("hides admin-only and manager-only routes from OPERATOR", () => {
    const paths = getNavigationForRole("OPERATOR")
      .flatMap((group) => group.items.map((item) => item.path));

    expect(paths).toEqual([
      "/dashboard",
      "/alerts",
      "/tasks",
      "/ports"
    ]);
  });

  it("rejects direct access to disallowed routes", () => {
    expect(isRouteAllowed("OPERATOR", "/operation-log")).toBe(false);
    expect(isRouteAllowed("PORT_MANAGER", "/users")).toBe(false);
    expect(isRouteAllowed("ADMIN", "/users")).toBe(true);
  });
});
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
cd frontend
npm test -- src/navigation/navigation.test.ts
```

Expected: FAIL because `navigation.ts` does not exist.

- [ ] **Step 3: Implement the navigation model**

Create `frontend/src/navigation/navigation.ts`:

```ts
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
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => item.roles.includes(role))
    }))
    .filter((group) => group.items.length > 0);
}

export function isRouteAllowed(role: DemoUserRole, pathname: string): boolean {
  if (pathname === "/profile" || pathname === "/change-password" || pathname === "/simulation-results") {
    return true;
  }

  const normalizedPath = pathname.startsWith("/ports/") ? "/ports" : pathname;
  return navigationGroups
    .flatMap((group) => group.items)
    .some((item) => item.path === normalizedPath && item.roles.includes(role));
}
```

- [ ] **Step 4: Correct demo-user UTF-8 strings**

In `frontend/src/App.tsx`, use:

```ts
const DEMO_USERS: DemoUser[] = [
  {
    email: "admin@porms.vn",
    initials: "NV",
    name: "Nguyễn Văn Hùng",
    portName: "Cảng Tiên Sa",
    role: "ADMIN"
  },
  {
    email: "manager@porms.vn",
    initials: "TL",
    name: "Trần Thị Lan",
    portName: "Cảng Tiên Sa",
    role: "PORT_MANAGER"
  },
  {
    email: "operator@porms.vn",
    initials: "MD",
    name: "Phạm Minh Đức",
    portName: "Cảng Tiên Sa",
    role: "OPERATOR"
  }
];
```

- [ ] **Step 5: Run tests and verify GREEN**

Run:

```powershell
cd frontend
npm test -- src/navigation/navigation.test.ts
```

Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```powershell
git add frontend/src/navigation frontend/src/App.tsx
git commit -m "feat: define role-aware navigation"
```

---

### Task 3: Add Route Guards and the Tasks Page

**Files:**
- Modify: `frontend/src/router/index.tsx`
- Create: `frontend/src/router/index.test.tsx`
- Create: `frontend/src/pages/TasksPage.tsx`
- Create: `frontend/src/pages/TasksPage.test.tsx`

- [ ] **Step 1: Write failing route tests**

Create `frontend/src/router/index.test.tsx` with tests using `RouterProvider` and
the existing `buildRouter()`:

```tsx
import { render, screen } from "@testing-library/react";
import { RouterProvider } from "react-router-dom";
import { describe, expect, it } from "vitest";
import type { DemoUser } from "../App";
import { buildRouter } from "./index";

const operator: DemoUser = {
  email: "operator@porms.vn",
  initials: "MD",
  name: "Phạm Minh Đức",
  portName: "Cảng Tiên Sa",
  role: "OPERATOR"
};

function renderAt(path: string) {
  window.history.pushState({}, "", path);
  const router = buildRouter({
    currentUser: operator,
    demoUsers: [operator],
    onLogin: () => undefined,
    onLogout: () => undefined,
    onRefresh: () => undefined,
    refreshKey: 0
  });
  render(<RouterProvider router={router} />);
}

describe("protected routes", () => {
  it("renders the Tasks route for OPERATOR", async () => {
    renderAt("/tasks");
    expect(await screen.findByRole("heading", { name: "Nhật ký nhiệm vụ" })).toBeInTheDocument();
  });

  it("redirects OPERATOR away from Operation Log", async () => {
    renderAt("/operation-log");
    expect(await screen.findByRole("heading", { name: "Trung tâm điều hành" })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Write the failing Tasks page test**

Create `frontend/src/pages/TasksPage.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { TasksPage } from "./TasksPage";

describe("TasksPage", () => {
  it("renders deterministic task log rows", () => {
    render(<TasksPage />);

    expect(screen.getByRole("heading", { name: "Nhật ký nhiệm vụ" })).toBeInTheDocument();
    expect(screen.getByText("TASK-2026-041")).toBeInTheDocument();
    expect(screen.getByText("Hạn chế bốc xếp tại Bến số 1")).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run tests and verify RED**

Run:

```powershell
cd frontend
npm test -- src/router/index.test.tsx src/pages/TasksPage.test.tsx
```

Expected: FAIL because `TasksPage` and route guards do not exist.

- [ ] **Step 4: Implement the Tasks page**

Create `frontend/src/pages/TasksPage.tsx` with a fixed typed array:

```tsx
import { Badge } from "../components/common/Badge";

const tasks = [
  {
    code: "TASK-2026-041",
    title: "Hạn chế bốc xếp tại Bến số 1",
    zone: "Bến số 1",
    priority: "HIGH",
    status: "Đang thực hiện",
    assignee: "Phạm Minh Đức",
    createdAt: "21:54"
  },
  {
    code: "TASK-2026-040",
    title: "Neo giữ container tại Bãi container A",
    zone: "Bãi container A",
    priority: "MEDIUM",
    status: "Chờ xác nhận",
    assignee: "Đội vận hành",
    createdAt: "21:53"
  }
] as const;

export function TasksPage() {
  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký nhiệm vụ</h2>
          <p>Theo dõi nhiệm vụ được tạo tự động từ các quy tắc SOP</p>
        </div>
      </div>
      <article className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Mã</th>
              <th>Nhiệm vụ</th>
              <th>Khu vực</th>
              <th>Ưu tiên</th>
              <th>Phụ trách</th>
              <th>Trạng thái</th>
              <th>Thời gian</th>
            </tr>
          </thead>
          <tbody>
            {tasks.map((task) => (
              <tr key={task.code}>
                <td><span className="code-chip">{task.code}</span></td>
                <td><strong>{task.title}</strong></td>
                <td>{task.zone}</td>
                <td><Badge tone={task.priority === "HIGH" ? "warning" : "info"}>{task.priority}</Badge></td>
                <td>{task.assignee}</td>
                <td>{task.status}</td>
                <td>{task.createdAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </article>
    </section>
  );
}
```

- [ ] **Step 5: Add guarded routing**

In `frontend/src/router/index.tsx`:

- import `isRouteAllowed`;
- import `TasksPage`;
- add a `RoleGuard` that checks `useLocation().pathname`;
- wrap `<Outlet />` with the guard;
- add `{ path: "tasks", element: <TasksPage /> }`.

Use:

```tsx
function RoleGuard({ currentUser }: { currentUser: DemoUser }) {
  const location = useLocation();

  if (!isRouteAllowed(currentUser.role, location.pathname)) {
    return <Navigate to="/dashboard" replace />;
  }

  return <Outlet />;
}
```

Nest the protected page routes under:

```tsx
{
  element: <RoleGuard currentUser={context.currentUser!} />,
  children: [
    { index: true, element: <Navigate to="/dashboard" replace /> },
    { path: "dashboard", element: <DashboardPage refreshKey={context.refreshKey} /> },
    { path: "alerts", element: <AlertPage refreshKey={context.refreshKey} /> },
    { path: "tasks", element: <TasksPage /> }
    // retain the remaining existing routes here
  ]
}
```

- [ ] **Step 6: Run tests and verify GREEN**

Run:

```powershell
cd frontend
npm test -- src/router/index.test.tsx src/pages/TasksPage.test.tsx
```

Expected: all route and Tasks tests pass.

- [ ] **Step 7: Commit**

```powershell
git add frontend/src/router frontend/src/pages/TasksPage.tsx frontend/src/pages/TasksPage.test.tsx
git commit -m "feat: add guarded tasks route"
```

---

### Task 4: Rebuild the Sidebar and Mobile Shell

**Files:**
- Create: `frontend/src/components/common/Icon.tsx`
- Modify: `frontend/src/components/layout/Sidebar.tsx`
- Modify: `frontend/src/components/layout/AppShell.tsx`
- Create: `frontend/src/components/layout/Sidebar.test.tsx`
- Create: `frontend/src/components/layout/AppShell.test.tsx`

- [ ] **Step 1: Write failing Sidebar tests**

Create `frontend/src/components/layout/Sidebar.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { Sidebar } from "./Sidebar";

const operator = {
  email: "operator@porms.vn",
  initials: "MD",
  name: "Phạm Minh Đức",
  portName: "Cảng Tiên Sa",
  role: "OPERATOR" as const
};

describe("Sidebar", () => {
  it("renders only OPERATOR navigation and the user footer", () => {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Sidebar currentUser={operator} isOpen={false} onClose={() => undefined} unreadAlertCount={3} />
      </MemoryRouter>
    );

    expect(screen.getByText("Nhật ký nhiệm vụ")).toBeInTheDocument();
    expect(screen.queryByText("Người dùng")).not.toBeInTheDocument();
    expect(screen.queryByText("Ngưỡng rủi ro")).not.toBeInTheDocument();
    expect(screen.getByText("Phạm Minh Đức")).toBeInTheDocument();
    expect(screen.getByLabelText("3 cảnh báo chưa đọc")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Write failing mobile-shell test**

Create `frontend/src/components/layout/AppShell.test.tsx`:

```tsx
import userEvent from "@testing-library/user-event";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AppShell } from "./AppShell";

it("opens and dismisses the mobile sidebar", async () => {
  const user = userEvent.setup();
  render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <AppShell
        currentUser={{
          email: "admin@porms.vn",
          initials: "NV",
          name: "Nguyễn Văn Hùng",
          portName: "Cảng Tiên Sa",
          role: "ADMIN"
        }}
        onLogout={() => undefined}
        onRefresh={() => undefined}
        refreshKey={0}
      >
        <div>Content</div>
      </AppShell>
    </MemoryRouter>
  );

  await user.click(screen.getByRole("button", { name: "Mở menu" }));
  expect(screen.getByRole("navigation", { name: "Điều hướng chính" })).toHaveClass("open");

  await user.click(screen.getByTestId("sidebar-backdrop"));
  expect(screen.getByRole("navigation", { name: "Điều hướng chính" })).not.toHaveClass("open");
});
```

- [ ] **Step 3: Run tests and verify RED**

Run:

```powershell
cd frontend
npm test -- src/components/layout/Sidebar.test.tsx src/components/layout/AppShell.test.tsx
```

Expected: FAIL because the new Sidebar props and mobile behavior do not exist.

- [ ] **Step 4: Add the shared line-icon component**

Create `frontend/src/components/common/Icon.tsx`. It should accept:

```ts
type IconProps = {
  name: IconName | "bell" | "chevron" | "menu" | "refresh" | "user";
  size?: number;
};
```

Render a single `<svg className="icon">` and select prototype-compatible
`<path>`, `<rect>`, and `<circle>` children by `name`. Keep icons local; do not
add an icon dependency.

- [ ] **Step 5: Rebuild Sidebar**

Change `Sidebar` props to:

```ts
type SidebarProps = {
  currentUser: DemoUser;
  isOpen: boolean;
  onClose: () => void;
  unreadAlertCount: number;
};
```

Implementation requirements:

- call `getNavigationForRole(currentUser.role)`;
- render `<nav aria-label="Điều hướng chính" className={`sidebar${isOpen ? " open" : ""}`}>`;
- include prototype brand;
- render each item with `<Icon name={item.icon} />`;
- show `<span className="count" aria-label={`${unreadAlertCount} cảnh báo chưa đọc`}>`;
- call `onClose` after navigation;
- render sidebar footer with user avatar, name, and:

```ts
const roleLabels = {
  ADMIN: "Quản trị hệ thống",
  PORT_MANAGER: "Quản lý cảng",
  OPERATOR: "Nhân viên vận hành"
} as const;
```

- [ ] **Step 6: Add shell state and alert count**

In `AppShell.tsx`:

- manage `isSidebarOpen`;
- load alerts with `getAlerts()` whenever `refreshKey` changes;
- calculate unread count;
- pass count and current user to Sidebar and Topbar;
- render:

```tsx
{isSidebarOpen ? (
  <button
    aria-label="Đóng menu"
    className="sidebar-backdrop"
    data-testid="sidebar-backdrop"
    onClick={() => setSidebarOpen(false)}
    type="button"
  />
) : null}
```

- [ ] **Step 7: Run tests and verify GREEN**

Run:

```powershell
cd frontend
npm test -- src/components/layout/Sidebar.test.tsx src/components/layout/AppShell.test.tsx
```

Expected: both tests pass.

- [ ] **Step 8: Commit**

```powershell
git add frontend/src/components/common/Icon.tsx frontend/src/components/layout
git commit -m "feat: rebuild role-aware application sidebar"
```

---

### Task 5: Match the Prototype Topbar

**Files:**
- Modify: `frontend/src/components/layout/Topbar.tsx`
- Create: `frontend/src/components/layout/Topbar.test.tsx`

- [ ] **Step 1: Write the failing Topbar test**

Create `frontend/src/components/layout/Topbar.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { Topbar } from "./Topbar";

it("renders prototype topbar controls and Vietnamese title", () => {
  vi.setSystemTime(new Date("2026-06-18T14:30:00+07:00"));
  render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <Topbar
        currentUser={{
          email: "admin@porms.vn",
          initials: "NV",
          name: "Nguyễn Văn Hùng",
          portName: "Cảng Tiên Sa",
          role: "ADMIN"
        }}
        onLogout={() => undefined}
        onMenuToggle={() => undefined}
        onRefresh={() => undefined}
        unreadAlertCount={3}
      />
    </MemoryRouter>
  );

  expect(screen.getByText("Dashboard")).toBeInTheDocument();
  expect(screen.getByText("Cảng Tiên Sa — Đà Nẵng")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Mở menu" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "3 cảnh báo chưa đọc" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Làm mới" })).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
cd frontend
npm test -- src/components/layout/Topbar.test.tsx
```

Expected: FAIL because the required controls and props are absent.

- [ ] **Step 3: Implement the prototype Topbar**

Use props:

```ts
type TopbarProps = {
  currentUser: DemoUser;
  onLogout: () => void;
  onMenuToggle: () => void;
  onRefresh: () => void;
  unreadAlertCount: number;
};
```

Add:

- mobile menu icon button;
- page title mapping with Vietnamese names, including `/tasks`;
- breadcrumb;
- clock updated every second with `toLocaleTimeString("vi-VN")`;
- alert link to `/alerts` with unread count;
- refresh button;
- profile/account icon linking to `/profile`.

Do not add a new account dropdown in this task; retain logout through the
existing accessible account area or profile flow.

- [ ] **Step 4: Run the test and verify GREEN**

Run:

```powershell
cd frontend
npm test -- src/components/layout/Topbar.test.tsx
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add frontend/src/components/layout/Topbar.tsx frontend/src/components/layout/Topbar.test.tsx
git commit -m "feat: align topbar with PORMS prototype"
```

---

### Task 6: Recompose Dashboard to Match the Prototype

**Files:**
- Modify: `frontend/src/pages/DashboardPage.tsx`
- Create: `frontend/src/pages/DashboardPage.test.tsx`
- Create: `frontend/src/components/dashboard/ZoneStatusCard.tsx`
- Modify Dashboard card files listed in File Structure.

- [ ] **Step 1: Write the failing Dashboard structure test**

Create `frontend/src/pages/DashboardPage.test.tsx`. Mock the service modules with
stable resolved values, then assert:

```tsx
expect(await screen.findByRole("heading", { name: "Trung tâm điều hành" })).toBeInTheDocument();
expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Mức rủi ro hiện tại");
expect(screen.getByTestId("dashboard-left")).toHaveTextContent("Trạng thái khu vực");
expect(screen.getByTestId("dashboard-right")).toHaveTextContent("Thời tiết hiện tại");
expect(screen.getByTestId("dashboard-right")).toHaveTextContent("Cảnh báo đang hoạt động");
expect(screen.queryByText("Nhật ký gần đây")).not.toBeInTheDocument();
```

The service mocks must provide one port, two zones, one alert, weather, summary,
and four trend points.

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
cd frontend
npm test -- src/pages/DashboardPage.test.tsx
```

Expected: FAIL because Dashboard lacks zones and still renders the operation-log card.

- [ ] **Step 3: Add the Zone Status card**

Create `ZoneStatusCard.tsx` with props:

```ts
type ZoneStatusCardProps = {
  portId: string;
  zones: PortZone[];
};
```

Render:

- title `Trạng thái khu vực`;
- subtitle `${zones.length} khu vực đang được giám sát`;
- link `/ports/${portId}` labeled `Xem chi tiết →`;
- two-column `.zone-grid`;
- icon selected from zone type;
- zone name, `${zone.zoneType} · ${zone.statusLabel}`, and a risk badge.

- [ ] **Step 4: Recompose Dashboard data loading**

In `DashboardPage.tsx`:

- remove operation-event imports and state;
- add `getPortZones`;
- fetch zones after summary resolves, using `summary.portId`;
- keep API-first service behavior;
- after simulation, refetch summary, weather, trend, alerts, and zones;
- render this order:

```tsx
<div className="dashboard-grid">
  <div className="dashboard-main" data-testid="dashboard-left">
    <div className="hero-grid">
      <RiskHeroCard summary={summary} weather={weather} />
      <ModeCard operationMode={summary.currentOperationMode} />
    </div>
    <RiskTrendChart points={trend} currentRiskLevel={summary.currentRiskLevel} />
    <ZoneStatusCard portId={summary.portId} zones={zones} />
  </div>
  <div className="dashboard-side" data-testid="dashboard-right">
    <WeatherSummaryCard summary={weather} beaufortNumber={summary.beaufortNumber} />
    <AlertListCard alerts={alerts} />
  </div>
</div>
```

- [ ] **Step 5: Align card markup**

Implement these exact presentation changes:

- `RiskHeroCard`: prototype kicker, `risk-main`, score, progress percentages
  `18/43/73/96`, decorative card class, weather-based reason.
- `ModeCard`: prototype `mode-panel`, description, automatic-change caption,
  and bottom status row.
- `RiskTrendChart`: compact header, current-risk badge, chart grid and area
  gradient.
- `WeatherSummaryCard`: title `Thời tiết hiện tại`, show wind, Beaufort, rain,
  visibility, temperature, and humidity without inventing missing values.
- `AlertListCard`: unread alerts first, at most four rows, icon + text + time,
  and `Xem tất cả` link.

- [ ] **Step 6: Correct UTF-8 text in touched Dashboard files**

Replace every mojibake string in:

```text
frontend/src/pages/DashboardPage.tsx
frontend/src/components/dashboard/RiskHeroCard.tsx
frontend/src/components/dashboard/ModeCard.tsx
frontend/src/components/dashboard/RiskTrendChart.tsx
frontend/src/components/dashboard/WeatherSummaryCard.tsx
frontend/src/components/dashboard/AlertListCard.tsx
frontend/src/services/alertService.ts
```

- [ ] **Step 7: Run Dashboard tests and verify GREEN**

Run:

```powershell
cd frontend
npm test -- src/pages/DashboardPage.test.tsx
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add frontend/src/pages/DashboardPage.tsx frontend/src/pages/DashboardPage.test.tsx frontend/src/components/dashboard frontend/src/services/alertService.ts
git commit -m "feat: align dashboard composition with prototype"
```

---

### Task 7: Apply Prototype CSS and Responsive Behavior

**Files:**
- Modify: `frontend/src/styles/app.css`
- Modify: `frontend/src/styles/layout.css`
- Modify: `frontend/src/styles/pages.css`
- Modify: `frontend/src/styles/components.css`

- [ ] **Step 1: Add a CSS contract test**

Create `frontend/src/styles/style-contract.test.ts` that reads the CSS files and
asserts required prototype selectors and tokens exist:

```ts
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const css = ["app.css", "layout.css", "pages.css", "components.css"]
  .map((file) => readFileSync(resolve(process.cwd(), "src/styles", file), "utf8"))
  .join("\n");

describe("prototype style contract", () => {
  it("contains the shell and Dashboard layout rules", () => {
    expect(css).toContain("--sidebar: 224px");
    expect(css).toContain("--topbar: 58px");
    expect(css).toContain(".sidebar.open");
    expect(css).toContain(".sidebar-backdrop");
    expect(css).toContain("grid-template-columns: 1.25fr 0.75fr");
    expect(css).toContain(".zone-grid");
    expect(css).toContain("@media (max-width: 780px)");
  });
});
```

- [ ] **Step 2: Run the style test and verify RED**

Run:

```powershell
cd frontend
npm test -- src/styles/style-contract.test.ts
```

Expected: FAIL because current layout tokens and selectors differ.

- [ ] **Step 3: Align global tokens**

In `app.css`, use the prototype tokens:

```css
:root {
  --navy: #10283d;
  --navy-2: #17364f;
  --navy-3: #22455f;
  --blue: #2f6fab;
  --blue-2: #3c82c3;
  --bg: #f1f5f9;
  --card: #ffffff;
  --line: #dbe3ec;
  --text: #17283a;
  --muted: #6d7f91;
  --green: #19a66a;
  --amber: #e9a11b;
  --orange: #ee7623;
  --red: #d94848;
  --radius: 10px;
  --sidebar: 224px;
  --topbar: 58px;
}
```

- [ ] **Step 4: Align shell CSS**

In `layout.css`, implement:

- fixed sidebar and `.main`/`.shell-main` margin;
- 72px brand;
- scrollable grouped navigation;
- 38px navigation items with icon, active state, and count;
- sidebar user footer;
- sticky 58px topbar;
- mobile toggle and backdrop;
- off-canvas sidebar at 780px.

- [ ] **Step 5: Align Dashboard CSS**

In `pages.css` and `components.css`, implement prototype rules:

```css
.dashboard-grid {
  display: grid;
  gap: 14px;
  grid-template-columns: 1.25fr 0.75fr;
}

.dashboard-main,
.dashboard-side {
  display: grid;
  gap: 14px;
  align-content: start;
}

.hero-grid {
  display: grid;
  gap: 14px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}
```

Also copy the prototype dimensions and visual treatment for:

- `.risk-hero-card`;
- `.mode-panel`;
- `.chart-card` and chart SVG;
- `.zone-grid`, `.zone-card`, `.zone-icon`;
- `.list-row`, `.list-icon`, `.list-time`;
- `.weather-grid` and weather metrics;
- cards, buttons, and badges.

- [ ] **Step 6: Add responsive rules**

Use:

```css
@media (max-width: 1100px) {
  .dashboard-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 780px) {
  :root {
    --sidebar: 238px;
  }

  .hero-grid,
  .zone-grid {
    grid-template-columns: 1fr;
  }
}
```

Ensure no horizontal overflow at 375px width.

- [ ] **Step 7: Run the style test and full frontend tests**

Run:

```powershell
cd frontend
npm test
```

Expected: all frontend tests pass.

- [ ] **Step 8: Commit**

```powershell
git add frontend/src/styles
git commit -m "style: match PORMS shell and dashboard prototype"
```

---

### Task 8: Production Verification and Visual Review

**Files:**
- Verify only unless defects are found.

- [ ] **Step 1: Scan touched source for mojibake**

Run:

```powershell
rg -n "Cáº|Ä|Ã|Â" frontend/src/App.tsx frontend/src/navigation frontend/src/router frontend/src/components/layout frontend/src/components/dashboard frontend/src/pages/DashboardPage.tsx frontend/src/pages/TasksPage.tsx frontend/src/services/alertService.ts
```

Expected: no matches.

- [ ] **Step 2: Run all frontend tests**

Run:

```powershell
cd frontend
npm test
```

Expected: all tests pass with zero failures.

- [ ] **Step 3: Build in real API mode**

Run:

```powershell
cd frontend
$env:VITE_API_BASE_URL='http://localhost:5000'
$env:VITE_USE_MOCK_FALLBACK='false'
npm run build
```

Expected: TypeScript and Vite build exit `0`.

- [ ] **Step 4: Start the production-like stack**

Run:

```powershell
docker compose -f infra/docker-compose.yml --env-file .env up -d postgres backend frontend
docker compose -f infra/docker-compose.yml --env-file .env ps
```

Expected: PostgreSQL healthy; backend and frontend running.

- [ ] **Step 5: Verify API prerequisites**

Run:

```powershell
Invoke-WebRequest http://localhost:5000/health -UseBasicParsing
Invoke-WebRequest http://localhost:5000/api/dashboard/summary -UseBasicParsing
Invoke-WebRequest http://localhost:5000/api/ports -UseBasicParsing
Invoke-WebRequest http://localhost:5000/api/alerts -UseBasicParsing
```

Expected: HTTP 200 for every request.

- [ ] **Step 6: Perform visual review against `design.html`**

Open:

```text
Prototype: D:\14.Business\007.fpt_support\001.design\design.html
Frontend:  http://localhost:5173
```

Verify at desktop width:

- sidebar width, groups, icons, active state, unread count, user footer;
- topbar title, breadcrumb, clock, alert, refresh, account;
- Dashboard order and 1.25/0.75 ratio;
- compact risk, mode, trend, zones, weather, and alert cards.

Verify at 375px width:

- menu button opens sidebar;
- backdrop closes sidebar;
- no horizontal overflow;
- Dashboard cards stack in a readable order.

- [ ] **Step 7: Verify all role combinations**

Use each demo account:

```text
ADMIN:        all sidebar items
PORT_MANAGER: all except Users
OPERATOR:     Dashboard, Alerts, Tasks, Ports & Zones
```

Manually enter a forbidden OPERATOR URL such as `/users`; expected redirect is
`/dashboard`.

- [ ] **Step 8: Verify simulation refresh**

From Dashboard, run the existing demo simulation.

Expected:

- risk and mode refresh;
- weather values refresh;
- alert list and unread badge refresh;
- zone status refresh;
- routing remains intact.

- [ ] **Step 9: Commit any verification-only fixes**

If verification required corrections:

```powershell
git add frontend
git commit -m "fix: address dashboard fidelity verification"
```

If no corrections were needed, do not create an empty commit.

---

## Self-Review

- Spec coverage: Sidebar structure, role matrix, Tasks route, mobile behavior,
  topbar, Dashboard ordering, zone restoration, alerts, weather, UTF-8, and
  verification are each assigned to explicit tasks.
- Placeholder scan: Every implementation and verification step is concrete.
- Type consistency: `DemoUserRole`, `NavigationGroup`, `NavigationItem`,
  `IconName`, `SidebarProps`, and `TopbarProps` are defined before use and kept
  consistent across tasks.
