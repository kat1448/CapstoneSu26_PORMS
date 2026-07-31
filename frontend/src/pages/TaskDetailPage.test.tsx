import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { DemoUser } from "../App";
import {
  acknowledgeTask,
  completeTask,
  getTask,
  getTaskAssignees,
  startTask,
  type TaskLogRecord
} from "../services/taskService";
import { TaskDetailPage } from "./TaskDetailPage";

vi.mock("../services/taskService", () => ({
  acknowledgeTask: vi.fn(),
  assignTask: vi.fn(),
  completeTask: vi.fn(),
  getTask: vi.fn(),
  getTaskAssignees: vi.fn(),
  startTask: vi.fn()
}));

const operator: DemoUser = {
  id: "operator-1",
  email: "operator@porms.vn",
  initials: "OP",
  name: "Nhân viên vận hành",
  portId: "port-1",
  portName: "Cảng Tiên Sa",
  role: "OPERATOR"
};

const task: TaskLogRecord = {
  alertId: "alert-1",
  assignedUserId: operator.id,
  assignedUserName: operator.name,
  createdAt: "2026-07-29T10:00:00Z",
  description: "Kiểm tra và bảo đảm an toàn khu cầu bến.",
  dueAt: "2026-07-29T14:00:00Z",
  isSimulation: true,
  portCode: "DNTSA",
  portId: "port-1",
  portName: "Cảng Tiên Sa",
  priority: "CRITICAL",
  simulationSessionId: "session-1",
  status: "NEW",
  taskCode: "TASK-001",
  taskId: "task-1",
  title: "Dừng hoạt động và kiểm tra an toàn",
  updatedAt: "2026-07-29T10:00:00Z",
  zoneId: "zone-1",
  zoneName: "Khu cầu bến"
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/tasks/task-1"]}>
      <Routes>
        <Route path="/tasks/:taskId" element={<TaskDetailPage currentUser={operator} />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("TaskDetailPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getTask).mockResolvedValue(task);
    vi.mocked(getTaskAssignees).mockResolvedValue([]);
  });

  it("forces the assigned operator through receive, start and completion with a result", async () => {
    const user = userEvent.setup();
    vi.mocked(acknowledgeTask).mockResolvedValue({
      ...task,
      acknowledgedAt: "2026-07-29T10:05:00Z",
      status: "ACKNOWLEDGED"
    });
    vi.mocked(startTask).mockResolvedValue({
      ...task,
      acknowledgedAt: "2026-07-29T10:05:00Z",
      startedAt: "2026-07-29T10:10:00Z",
      status: "IN_PROGRESS"
    });
    vi.mocked(completeTask).mockImplementation(async (_taskId, input) => ({
      ...task,
      acknowledgedAt: "2026-07-29T10:05:00Z",
      completedAt: "2026-07-29T10:30:00Z",
      completionNote: input.completionNote,
      startedAt: "2026-07-29T10:10:00Z",
      status: "COMPLETED"
    }));

    renderPage();

    await user.click(await screen.findByRole("button", { name: "Tiếp nhận nhiệm vụ" }));
    expect(acknowledgeTask).toHaveBeenCalledWith("task-1");
    expect(screen.queryByRole("button", { name: "Hoàn tất và ghi kết quả" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Bắt đầu thực hiện" }));
    expect(startTask).toHaveBeenCalledWith("task-1");

    await user.click(screen.getByRole("button", { name: "Hoàn tất và ghi kết quả" }));
    const result = "Đã kiểm tra thiết bị và xác nhận khu vực an toàn.";
    await user.type(screen.getByLabelText(/Kết quả xử lý/), result);
    await user.click(screen.getByRole("button", { name: "Xác nhận hoàn tất" }));

    expect(completeTask).toHaveBeenCalledWith("task-1", { completionNote: result });
    expect(await screen.findByText(result)).toBeInTheDocument();
    expect(screen.getByText("Nhiệm vụ đã hoàn tất")).toBeInTheDocument();
  });
});
