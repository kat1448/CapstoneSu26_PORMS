import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { TasksPage } from "./TasksPage";

describe("TasksPage", () => {
  it("renders deterministic task log rows", () => {
    render(<TasksPage />);

    expect(screen.getByRole("heading", { name: "Nhật ký nhiệm vụ" })).toBeInTheDocument();
    expect(screen.getByText("TASK-2026-041")).toBeInTheDocument();
    expect(screen.getByText("Hạn chế bốc xếp tại Bến số 1")).toBeInTheDocument();
  });
});
