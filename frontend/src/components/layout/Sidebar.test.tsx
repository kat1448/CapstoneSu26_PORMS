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
            email: "operator@porms.vn",
            initials: "MD",
            name: "Phạm Minh Đức",
            portName: "Cảng Tiên Sa",
            role: "OPERATOR"
          }}
          isOpen={false}
          onClose={() => undefined}
          unreadAlertCount={3}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Nhật ký nhiệm vụ")).toBeInTheDocument();
    expect(screen.queryByText("Người dùng")).not.toBeInTheDocument();
    expect(screen.queryByText("Ngưỡng rủi ro")).not.toBeInTheDocument();
    expect(screen.getByText("Phạm Minh Đức")).toBeInTheDocument();
    expect(screen.getByLabelText("3 cảnh báo chưa đọc")).toBeInTheDocument();
  });
});
