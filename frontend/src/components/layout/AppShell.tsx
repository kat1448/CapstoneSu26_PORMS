import { useEffect, useState, type PropsWithChildren } from "react";
import type { DemoUser } from "../../App";
import { getAlerts } from "../../services/alertService";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

type AppShellProps = PropsWithChildren<{
  currentUser: DemoUser;
  onLogout: () => void;
  onRefresh: () => void;
  refreshKey: number;
}>;

export function AppShell({ children, currentUser, onLogout, onRefresh, refreshKey }: AppShellProps) {
  const [isSidebarOpen, setSidebarOpen] = useState(false);
  const [unreadAlertCount, setUnreadAlertCount] = useState(0);

  useEffect(() => {
    void getAlerts().then((alerts) => setUnreadAlertCount(alerts.filter((alert) => !alert.read).length));
  }, [refreshKey]);

  return (
    <div className="app-shell">
      <Sidebar currentUser={currentUser} isOpen={isSidebarOpen} onClose={() => setSidebarOpen(false)} unreadAlertCount={unreadAlertCount} />
      {isSidebarOpen ? <button aria-label="Đóng menu" className="sidebar-backdrop" data-testid="sidebar-backdrop" onClick={() => setSidebarOpen(false)} type="button" /> : null}
      <div className="shell-main">
        <Topbar currentUser={currentUser} onLogout={onLogout} onMenuToggle={() => setSidebarOpen((value) => !value)} onRefresh={onRefresh} unreadAlertCount={unreadAlertCount} />
        <main className="content">{children}</main>
      </div>
    </div>
  );
}
