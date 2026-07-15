import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { deleteUser, getUsers } from "../services/userService";
import type { UserRecord } from "../services/userService";
import { UsersPage } from "./UsersPage";

vi.mock("../services/userService", () => ({
  deleteUser: vi.fn(),
  getUsers: vi.fn()
}));

const userRecords: UserRecord[] = [
  {
    email: "hung@example.com",
    fullName: "Nguyen Van Hung",
    lastLoginLabel: "Vua xong",
    portId: "port-dntsa",
    portName: "Cang Tien Sa",
    role: "PORT_MANAGER",
    status: "ACTIVE",
    userId: "user-1"
  },
  {
    email: "lan@example.com",
    fullName: "Tran Thi Lan",
    lastLoginLabel: "Chua dang nhap",
    portId: "port-dntsa",
    portName: "Cang Tien Sa",
    role: "OPERATOR",
    status: "LOCKED",
    userId: "user-2"
  }
];

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("UsersPage", () => {
  it("shows the list with links to separate create and edit pages", async () => {
    vi.mocked(getUsers).mockResolvedValue(userRecords);

    render(
      <MemoryRouter>
        <UsersPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByText("Nguyen Van Hung");

    expect(screen.getByRole("link", { name: "Thêm người dùng" })).toHaveAttribute("href", "/users/new");
    expect(screen.getByRole("link", { name: "Sửa Nguyen Van Hung" })).toHaveAttribute("href", "/users/user-1/edit");
    expect(screen.queryByLabelText("Họ tên")).not.toBeInTheDocument();
  });

  it("deletes a user after confirmation", async () => {
    const user = userEvent.setup();
    vi.mocked(getUsers).mockResolvedValue(userRecords);
    vi.mocked(deleteUser).mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValueOnce(true);

    render(
      <MemoryRouter>
        <UsersPage refreshKey={0} />
      </MemoryRouter>
    );

    await screen.findByText("Tran Thi Lan");
    await user.click(screen.getByRole("button", { name: "Xóa Tran Thi Lan" }));

    expect(deleteUser).toHaveBeenCalledWith("user-2");
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });
});
