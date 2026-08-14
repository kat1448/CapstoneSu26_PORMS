import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { getPorts } from "../services/portService";
import { createUser, getUsers, updateUser } from "../services/userService";
import type { UserRecord } from "../services/userService";
import type { DemoUser } from "../App";
import { UserFormPage } from "./UserFormPage";

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

function renderRoutes(initialPath: string, mode: "create" | "edit", currentUser?: DemoUser) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/users/new" element={<UserFormPage currentUser={currentUser} mode={mode} />} />
        <Route path="/users/:userId/edit" element={<UserFormPage currentUser={currentUser} mode={mode} />} />
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

  it("prevents the signed-in admin from changing their own role", async () => {
    const user = userEvent.setup();
    const adminRecord: UserRecord = {
      ...userRecords[0],
      email: "admin@porms.vn",
      fullName: "System Admin",
      portId: null,
      portName: "Tat ca",
      role: "ADMIN",
      userId: "demo-admin"
    };
    vi.mocked(getPorts).mockResolvedValue(ports);
    vi.mocked(getUsers).mockResolvedValue([adminRecord]);
    vi.mocked(updateUser).mockResolvedValue(adminRecord);

    renderRoutes("/users/demo-admin/edit", "edit", {
      email: "admin@porms.vn",
      initials: "SA",
      name: "System Admin",
      portName: "Tat ca",
      role: "ADMIN"
    });

    const roleSelect = await screen.findByRole("combobox", { name: "Vai trò" });
    expect(roleSelect).toBeDisabled();
    expect(screen.getByText("Admin không thể tự thay đổi vai trò của chính mình.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Lưu thay đổi" }));

    expect(updateUser).toHaveBeenCalledWith("demo-admin", expect.objectContaining({
      portId: null,
      role: "ADMIN"
    }));
  });
});
