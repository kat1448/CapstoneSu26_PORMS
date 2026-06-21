import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Topbar } from "./Topbar";

afterEach(cleanup);

describe("Topbar", () => {
  function renderTopbar(onLogout = vi.fn()) {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Topbar
          currentUser={{
            email: "admin@porms.vn",
            initials: "NH",
            name: "Nguyễn Văn Hùng",
            portName: "Cảng Tiên Sa",
            role: "ADMIN"
          }}
          onLogout={onLogout}
          onMenuToggle={() => undefined}
          onRefresh={() => undefined}
          unreadAlertCount={3}
        />
      </MemoryRouter>
    );

    return onLogout;
  }

  it("matches the HTML design action controls", () => {
    renderTopbar();

    expect(screen.getByText(/^\d{2}:\d{2}:\d{2} · \d{2}\/\d{2}\/\d{4}$/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "3 cảnh báo chưa đọc" })).toHaveClass("topbar-icon-button");
    expect(screen.getByRole("button", { name: "Làm mới" })).toHaveClass("topbar-refresh-button");
    expect(screen.getByRole("button", { name: "Tài khoản Nguyễn Văn Hùng" })).toHaveAttribute("aria-expanded", "false");
  });

  it("opens and dismisses the account drop-card", async () => {
    const user = userEvent.setup();
    renderTopbar();
    const accountButton = screen.getByRole("button", { name: "Tài khoản Nguyễn Văn Hùng" });

    await user.click(accountButton);
    expect(accountButton).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("menu", { name: "Tài khoản" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Thông tin cá nhân" })).toHaveAttribute("href", "/profile");
    expect(screen.getByRole("menuitem", { name: "Đổi mật khẩu" })).toHaveAttribute("href", "/change-password");

    await user.click(accountButton);
    expect(screen.queryByRole("menu", { name: "Tài khoản" })).not.toBeInTheDocument();

    await user.click(accountButton);
    await user.click(document.body);
    expect(screen.queryByRole("menu", { name: "Tài khoản" })).not.toBeInTheDocument();
  });

  it("logs out from the account drop-card", async () => {
    const user = userEvent.setup();
    const onLogout = renderTopbar();

    await user.click(screen.getByRole("button", { name: "Tài khoản Nguyễn Văn Hùng" }));
    await user.click(screen.getByRole("menuitem", { name: "Đăng xuất" }));

    expect(onLogout).toHaveBeenCalledOnce();
    expect(screen.queryByRole("menu", { name: "Tài khoản" })).not.toBeInTheDocument();
  });
});
