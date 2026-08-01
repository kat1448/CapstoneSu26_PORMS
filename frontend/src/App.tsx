import { useMemo, useState } from "react";
import { RouterProvider } from "react-router-dom";
import { buildRouter } from "./router";
import { clearSession, getStoredSession, login } from "./services/authService";

export type DemoUserRole = "ADMIN" | "PORT_MANAGER" | "OPERATOR";

export type DemoUser = {
  id?: string;
  email: string;
  initials: string;
  name: string;
  portName: string;
  role: DemoUserRole;
};

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

export default function App() {
  const [currentUser, setCurrentUser] = useState<DemoUser | null>(() => getStoredSession()?.user ?? null);
  const [refreshKey, setRefreshKey] = useState(0);

  const router = useMemo(
    () =>
      buildRouter({
        currentUser,
        demoUsers: DEMO_USERS,
        onLogin: async (email, password) => {
          const session = await login(email, password);
          setCurrentUser(session.user);
        },
        onLogout: () => {
          clearSession();
          setCurrentUser(null);
        },
        onRefresh: () => setRefreshKey((value) => value + 1),
        refreshKey
      }),
    [currentUser, refreshKey],
  );

  return <RouterProvider router={router} />;
}
