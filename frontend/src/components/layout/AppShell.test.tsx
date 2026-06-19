import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { AppShell } from "./AppShell";

vi.mock("../../services/alertService", () => ({
  getAlerts: async () => []
}));

describe("AppShell", () => {
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
});
