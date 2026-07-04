import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { getTasks } from "../services/taskService";
import { TasksPage } from "./TasksPage";

vi.mock("../services/taskService", () => ({
  getTasks: vi.fn()
}));

describe("TasksPage", () => {
  it("loads task log rows from the API service", async () => {
    vi.mocked(getTasks).mockResolvedValue([{
      assignedTeam: "Đội vận hành",
      assignedUserId: null,
      assignedUserName: null,
      createdAt: "2026-07-04T15:27:03Z",
      description: "Created from SOP",
      dueAt: null,
      isSimulation: false,
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cảng Tiên Sa",
      priority: "HIGH",
      simulationSessionId: null,
      status: "NEW",
      taskCode: "TASK-REAL-001",
      taskId: "task-1",
      title: "Kiểm tra khu vực rủi ro",
      updatedAt: "2026-07-04T15:27:03Z",
      zoneId: "zone-1",
      zoneName: "Bến số 1"
    }]);

    render(<TasksPage />);

    expect(screen.getByRole("heading", { name: "Nhật ký nhiệm vụ" })).toBeInTheDocument();
    expect(await screen.findByText("TASK-REAL-001")).toBeInTheDocument();
    expect(screen.getByText("Kiểm tra khu vực rủi ro")).toBeInTheDocument();
    expect(screen.getByText("Bến số 1")).toBeInTheDocument();
    expect(screen.getByText("Đội vận hành")).toBeInTheDocument();
    expect(screen.queryByText("TASK-2026-041")).not.toBeInTheDocument();
  });
});
