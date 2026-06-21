import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ChangePasswordPage } from "./ChangePasswordPage";

afterEach(cleanup);

describe("ChangePasswordPage", () => {
  it("validates confirmation before submitting", async () => {
    const user = userEvent.setup();
    const onChanged = vi.fn();
    render(
      <MemoryRouter>
        <ChangePasswordPage onChanged={onChanged} />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText("Mật khẩu hiện tại"), "Admin@2026!");
    await user.type(screen.getByLabelText("Mật khẩu mới"), "Strong@2027!");
    await user.type(screen.getByLabelText("Xác nhận mật khẩu mới"), "Different@2027!");
    await user.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Mật khẩu xác nhận không khớp");
    expect(onChanged).not.toHaveBeenCalled();
  });

  it("submits a valid password change and logs out", async () => {
    const user = userEvent.setup();
    const onChanged = vi.fn();
    const changePassword = vi.fn().mockResolvedValue(undefined);
    render(
      <MemoryRouter>
        <ChangePasswordPage changePassword={changePassword} onChanged={onChanged} />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText("Mật khẩu hiện tại"), "Admin@2026!");
    await user.type(screen.getByLabelText("Mật khẩu mới"), "Strong@2027!");
    await user.type(screen.getByLabelText("Xác nhận mật khẩu mới"), "Strong@2027!");
    await user.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));

    expect(changePassword).toHaveBeenCalledWith({
      currentPassword: "Admin@2026!",
      newPassword: "Strong@2027!",
      confirmPassword: "Strong@2027!"
    });
    expect(onChanged).toHaveBeenCalledOnce();
  });
});
