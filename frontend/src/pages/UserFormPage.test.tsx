import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { getPorts } from "../services/portService";
import { createUser, getUsers, updateUser } from "../services/userService";
import type { UserRecord } from "../services/userService";
import { UserFormPage } from "./UserFormPage";

const adminUser = {
  id: "admin-1",
  email: "admin@porms.vn",
  initials: "AD",
  name: "System Administrator",
  portName: "Tất cả",
  role: "ADMIN" as const
};

vi.mock("../services/portService", () => ({
  getPorts: vi.fn()
}));

vi.mock("../services/userService", () => ({
  createUser: vi.fn(),
  getUsers: vi.fn(),
  updateUser: vi.fn()
}));

const portId = "11111111-1111-1111-1111-111111111111";
const ports = [
  {
    activeAlertCount: 0,
    currentOperationMode: "NORMAL" as const,
    currentRiskLevel: "LOW" as const,
    isActive: true,
    latitude: 16.1,
    longitude: 108.2,
    portCode: "DNTSA",
    portId,
    portName: "Cang Tien Sa",
    updatedAtLabel: "Vua xong"
  }
];

const userRecords: UserRecord[] = [
  {
    email: "hung@example.com",
    fullName: "Nguyen Van Hung",
    lastLoginLabel: "Vua xong",
    portId,
    portName: "Cang Tien Sa",
    role: "PORT_MANAGER",
    status: "ACTIVE",
    userId: "user-1"
  }
];

const adminRecord: UserRecord = {
  email: "admin@porms.vn",
  fullName: "System Administrator",
  lastLoginLabel: "Vua xong",
  portId: null,
  portName: "Tất cả",
  role: "ADMIN",
  status: "ACTIVE",
  userId: "admin-1"
};

function renderRoutes(initialPath: string, mode: "create" | "edit") {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/users/new" element={<UserFormPage currentUser={adminUser} mode={mode} />} />
        <Route path="/users/:userId/edit" element={<UserFormPage currentUser={adminUser} mode={mode} />} />
        <Route path="/users" element={<div>Users list page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("UserFormPage", () => {
  it("locks the role when an Admin edits their own account", async () => {
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getUsers).mockResolvedValue([adminRecord]);

    renderRoutes("/users/admin-1/edit", "edit");

    expect(await screen.findByLabelText("Vai trò hiện tại")).toHaveTextContent("ADMIN - System Administrator");
    expect(screen.queryByLabelText("Vai trò")).not.toBeInTheDocument();
    expect(screen.getByText(/tự mất quyền quản trị/i)).toBeInTheDocument();
  });

  it("creates a user and returns to the user list", async () => {
    const user = userEvent.setup();
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(createUser).mockResolvedValue({ ...userRecords[0], userId: "user-2" });

    renderRoutes("/users/new", "create");

    await user.type(screen.getByLabelText("Họ tên"), "Le Thi Mai");
    await user.type(screen.getByLabelText("Email"), "mai@example.com");
    await user.selectOptions(screen.getByLabelText("Vai trò"), "PORT_MANAGER");
    await user.type(screen.getByLabelText("Mật khẩu"), "Strong@2027!");
    await user.click(screen.getByRole("button", { name: "Tạo người dùng" }));

    expect(createUser).toHaveBeenCalledWith({
      email: "mai@example.com",
      fullName: "Le Thi Mai",
      password: "Strong@2027!",
      portId,
      role: "PORT_MANAGER",
      status: "ACTIVE"
    });
    await screen.findByText("Users list page");
  });

  it("clears assigned port for system admin", async () => {
    const user = userEvent.setup();
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(createUser).mockResolvedValue({
      ...userRecords[0],
      portId: null,
      portName: "Tat ca",
      role: "ADMIN",
      userId: "user-2"
    });

    renderRoutes("/users/new", "create");

    await user.type(screen.getByLabelText("Họ tên"), "Le Thi Mai");
    await user.type(screen.getByLabelText("Email"), "mai@example.com");
    await user.selectOptions(screen.getByLabelText("Vai trò"), "ADMIN");
    await user.type(screen.getByLabelText("Mật khẩu"), "Strong@2027!");
    await user.click(screen.getByRole("button", { name: "Tạo người dùng" }));

    expect(createUser).toHaveBeenCalledWith(expect.objectContaining({
      portId: null,
      role: "ADMIN"
    }));
  });

  it("loads the selected user, updates it, and returns to the user list", async () => {
    const user = userEvent.setup();
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getUsers).mockResolvedValue(userRecords);
    vi.mocked(updateUser).mockResolvedValue({ ...userRecords[0], fullName: "Nguyen Van Hung Updated" });

    renderRoutes("/users/user-1/edit", "edit");

    const fullNameInput = await screen.findByLabelText("Họ tên");
    await waitFor(() => expect(fullNameInput).toHaveValue("Nguyen Van Hung"));
    await user.clear(fullNameInput);
    await user.type(fullNameInput, "Nguyen Van Hung Updated");
    await user.click(screen.getByRole("button", { name: "Lưu thay đổi" }));

    expect(updateUser).toHaveBeenCalledWith("user-1", {
      email: "hung@example.com",
      fullName: "Nguyen Van Hung Updated",
      portId,
      role: "PORT_MANAGER",
      status: "ACTIVE"
    });
    await screen.findByText("Users list page");
  });
});
