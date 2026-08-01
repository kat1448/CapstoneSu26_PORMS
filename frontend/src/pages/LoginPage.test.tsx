import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { DemoUser } from "../App";
import { LoginPage } from "./LoginPage";

const demoUsers: DemoUser[] = [
  {
    email: "admin@porms.vn",
    initials: "AD",
    name: "System Admin",
    portName: "Tat ca",
    role: "ADMIN"
  },
  {
    email: "operator@porms.vn",
    initials: "MD",
    name: "Pham Minh Duc",
    portName: "Cang Tien Sa",
    role: "OPERATOR"
  }
];

afterEach(cleanup);

describe("LoginPage", () => {
  it("submits the current email and password", async () => {
    const user = userEvent.setup();
    const onLogin = vi.fn().mockResolvedValue(undefined);
    const { container } = render(<LoginPage demoUsers={demoUsers} onLogin={onLogin} />);
    const emailInput = container.querySelector<HTMLInputElement>("#login-email");
    const passwordInput = container.querySelector<HTMLInputElement>("#login-password");

    expect(emailInput).not.toBeNull();
    expect(passwordInput).not.toBeNull();

    await user.clear(emailInput!);
    await user.type(emailInput!, "operator@porms.vn");
    await user.clear(passwordInput!);
    await user.type(passwordInput!, "Standard@2026!");
    await user.click(screen.getByRole("button", { name: /dang nhap|đăng nhập/i }));

    await waitFor(() => {
      expect(onLogin).toHaveBeenCalledWith("operator@porms.vn", "Standard@2026!");
    });
  });

  it("shows login errors returned by the login callback", async () => {
    const user = userEvent.setup();
    const onLogin = vi.fn().mockRejectedValue(new Error("Email hoac mat khau khong dung."));
    render(<LoginPage demoUsers={demoUsers} onLogin={onLogin} />);

    await user.click(screen.getByRole("button", { name: /dang nhap|đăng nhập/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Email hoac mat khau khong dung.");
  });

  it("fills credentials when a demo account is selected", async () => {
    const user = userEvent.setup();
    const onLogin = vi.fn().mockResolvedValue(undefined);
    const { container } = render(<LoginPage demoUsers={demoUsers} onLogin={onLogin} />);
    const emailInput = container.querySelector<HTMLInputElement>("#login-email");
    const passwordInput = container.querySelector<HTMLInputElement>("#login-password");

    await user.click(screen.getByRole("button", { name: /Pham Minh Duc/i }));

    expect(emailInput).toHaveValue("operator@porms.vn");
    expect(passwordInput).toHaveValue("Admin@2026!");
  });
});
