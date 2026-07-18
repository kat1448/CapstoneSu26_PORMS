import { Navigate, Outlet, createBrowserRouter } from "react-router-dom";
import type { ReactNode } from "react";
import type { DemoUser } from "../App";
import { AppShell } from "../components/layout/AppShell";
import { AlertDetailPage } from "../pages/AlertDetailPage";
import { AlertPage } from "../pages/AlertPage";
import { ChangePasswordPage } from "../pages/ChangePasswordPage";
import { DashboardPage } from "../pages/DashboardPage";
import { ForecastPlanningPage } from "../pages/ForecastPlanningPage";
import { AiLongRangeForecastPage } from "../pages/AiLongRangeForecastPage";
import { ForecastEvaluationPage } from "../pages/ForecastEvaluationPage";
import { LogPage } from "../pages/LogPage";
import { LoginPage } from "../pages/LoginPage";
import { PortCreatePage } from "../pages/PortCreatePage";
import { PortManagementPage } from "../pages/PortManagementPage";
import { ProfilePage } from "../pages/ProfilePage";
import { RiskConfigPage } from "../pages/RiskConfigPage";
import { SimulationPage } from "../pages/SimulationPage";
import { SimulationResultsPage } from "../pages/SimulationResultsPage";
import { SopRulesPage } from "../pages/SopRulesPage";
import { UserFormPage } from "../pages/UserFormPage";
import { UsersPage } from "../pages/UsersPage";
import { TasksPage } from "../pages/TasksPage";
import { isRouteAllowed } from "../navigation/navigation";

type RouterContext = {
  currentUser: DemoUser | null;
  demoUsers: DemoUser[];
  onLogin: (email: string, password: string) => Promise<void>;
  onLogout: () => void;
  onRefresh: () => void;
  refreshKey: number;
};

function ProtectedLayout(props: RouterContext) {
  if (!props.currentUser) {
    return <Navigate to="/login" replace />;
  }

  return (
    <AppShell
      currentUser={props.currentUser}
      onLogout={props.onLogout}
      onRefresh={props.onRefresh}
      refreshKey={props.refreshKey}
    >
      <Outlet />
    </AppShell>
  );
}

export function buildRouter(context: RouterContext) {
  const guarded = (path: string, element: ReactNode) =>
    context.currentUser && isRouteAllowed(context.currentUser.role, path)
      ? element
      : <Navigate to="/dashboard" replace />;

  return createBrowserRouter([
    {
      path: "/login",
      element: context.currentUser ? (
        <Navigate to="/dashboard" replace />
      ) : (
        <LoginPage demoUsers={context.demoUsers} onLogin={context.onLogin} />
      )
    },
    {
      path: "/",
      element: <ProtectedLayout {...context} />,
      children: [
        { index: true, element: <Navigate to="/dashboard" replace /> },
        { path: "dashboard", element: <DashboardPage refreshKey={context.refreshKey} /> },
        { path: "alerts", element: <AlertPage refreshKey={context.refreshKey} /> },
        { path: "alerts/:alertId", element: <AlertDetailPage /> },
        { path: "tasks", element: guarded("/tasks", <TasksPage />) },
        { path: "operation-log", element: guarded("/operation-log", <LogPage refreshKey={context.refreshKey} />) },
        { path: "ports", element: <PortManagementPage refreshKey={context.refreshKey} /> },
        { path: "ports/new", element: <PortCreatePage /> },
        { path: "ports/:portId", element: <PortManagementPage refreshKey={context.refreshKey} detailMode /> },
        { path: "users/new", element: guarded("/users/new", <UserFormPage mode="create" />) },
        { path: "users/:userId/edit", element: guarded("/users/edit", <UserFormPage mode="edit" />) },
        { path: "users", element: guarded("/users", <UsersPage refreshKey={context.refreshKey} />) },
        { path: "risk-config", element: guarded("/risk-config", <RiskConfigPage />) },
        { path: "sop-rules", element: guarded("/sop-rules", <SopRulesPage />) },
        { path: "simulation", element: guarded("/simulation", <SimulationPage refreshKey={context.refreshKey} />) },
        { path: "forecast-planning", element: guarded("/forecast-planning", <ForecastPlanningPage />) },
        { path: "ai-long-range-forecast", element: guarded("/ai-long-range-forecast", <AiLongRangeForecastPage />) },
        { path: "forecast-evaluation", element: guarded("/forecast-evaluation", <ForecastEvaluationPage />) },
        { path: "simulation-results", element: guarded("/simulation-results", <SimulationResultsPage refreshKey={context.refreshKey} />) },
        { path: "profile", element: <ProfilePage currentUser={context.currentUser!} /> },
        { path: "change-password", element: <ChangePasswordPage onChanged={context.onLogout} /> }
      ]
    },
    {
      path: "*",
      element: <Navigate to={context.currentUser ? "/dashboard" : "/login"} replace />
    }
  ]);
}
