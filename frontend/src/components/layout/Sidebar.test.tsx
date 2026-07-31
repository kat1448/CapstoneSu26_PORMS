import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { Sidebar } from "./Sidebar";

describe("Sidebar", () => {
  it("renders operator navigation, alert count and user footer", () => {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Sidebar
          currentUser={{
            email: "operator@porms.vn",
            initials: "MD",
            name: "Pham Minh Duc",
            portName: "Cảng Tiên Sa",
            role: "OPERATOR"
          }}
          isOpen={false}
          onClose={() => undefined}
          unreadAlertCount={3}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Tổng quan")).toBeInTheDocument();
    expect(screen.getByText("Nhiệm vụ")).toBeInTheDocument();
    expect(screen.getByText("Lịch sử vận hành")).toBeInTheDocument();
    expect(screen.queryByText("Mô phỏng")).not.toBeInTheDocument();
    expect(screen.queryByText("Người dùng")).not.toBeInTheDocument();
    expect(screen.queryByText("Mức cảnh báo")).not.toBeInTheDocument();
    expect(screen.getByText("Pham Minh Duc")).toBeInTheDocument();
    expect(screen.getByLabelText("3 cảnh báo chưa đọc")).toBeInTheDocument();
  });
});
