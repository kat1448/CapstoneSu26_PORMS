import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { Sidebar } from "./Sidebar";

describe("Sidebar", () => {
  it("renders role-filtered navigation, alert count and user footer", () => {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Sidebar
          currentUser={{
            email: "standard@porms.vn",
            initials: "MD",
            name: "Pham Minh Duc",
            portName: "Cảng Tiên Sa",
            role: "STANDARD_USER"
          }}
          isOpen={false}
          onClose={() => undefined}
          unreadAlertCount={3}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Dashboard")).toBeInTheDocument();
    expect(screen.getByText("Mô phỏng")).toBeInTheDocument();
    expect(screen.queryByText("Kết quả mô phỏng")).not.toBeInTheDocument();
    expect(screen.queryByText("Nhật ký nhiệm vụ")).not.toBeInTheDocument();
    expect(screen.queryByText("Người dùng")).not.toBeInTheDocument();
    expect(screen.queryByText("Ngưỡng rủi ro")).not.toBeInTheDocument();
    expect(screen.getByText("Pham Minh Duc")).toBeInTheDocument();
    expect(screen.getByLabelText("3 cảnh báo chưa đọc")).toBeInTheDocument();
  });
});
