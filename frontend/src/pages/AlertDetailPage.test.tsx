import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AlertDetailPage } from "./AlertDetailPage";
import { getAlert, getAlertTasks } from "../services/alertService";
import { acknowledgeTask, assignTask, completeTask, getTaskAssignees, startTask } from "../services/taskService";

vi.mock("../services/alertService", () => ({
  getAlert: vi.fn(),
  getAlertTasks: vi.fn()
}));

vi.mock("../services/taskService", () => ({
  acknowledgeTask: vi.fn(),
  assignTask: vi.fn(),
  completeTask: vi.fn(),
  getTaskAssignees: vi.fn(),
  startTask: vi.fn()
}));

const task = {
  alertId: "alert-1",
  assignedTeam: null,
  assignedUserId: null,
  assignedUserName: null,
  createdAt: "2026-07-04T15:27:03Z",
  description: "Cấm bốc dỡ khu vực chịu ảnh hưởng.",
  dueAt: null,
  isSimulation: false,
  portCode: "DNTSA",
  portId: "port-1",
  portName: "Cảng Tiên Sa",
  priority: "HIGH",
  simulationSessionId: null,
  status: "NEW",
  taskCode: "TASK-001",
  taskId: "task-1",
  title: "Tạm dừng khai thác bến số 1",
  updatedAt: "2026-07-04T15:27:03Z",
  zoneId: "zone-1",
  zoneName: "Bến số 1"
} as const;

function renderDetail() {
  return render(
    <MemoryRouter initialEntries={["/alerts/alert-1"]}>
      <Routes>
        <Route path="/alerts/:alertId" element={<AlertDetailPage currentUser={{ email: "admin@porms.vn", initials: "AD", name: "Admin", portName: "Toàn hệ thống", role: "ADMIN" }} />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertDetailPage", () => {
  beforeEach(() => {
    vi.mocked(getAlert).mockResolvedValue({
      alertId: "alert-1",
      alertType: "WEATHER",
      createdAt: "27/06/2026 20:45:12",
      message: "Gió mạnh tại bến số 1.",
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cảng Tiên Sa",
      read: false,
      severity: "HIGH",
      title: "Cảnh báo khu vực bến số 1",
      zoneName: "Bến số 1"
    });
    vi.mocked(getAlertTasks).mockResolvedValue([task]);
    vi.mocked(getTaskAssignees).mockResolvedValue([{
      email: "operator@porms.vn",
      fullName: "Nguyễn Văn A",
      role: "OPERATOR",
      userId: "user-1"
    }]);
    vi.mocked(assignTask).mockResolvedValue({
      ...task,
      assignedUserId: "user-1",
      assignedUserName: "Nguyễn Văn A"
    });
    vi.mocked(acknowledgeTask).mockResolvedValue({
      ...task,
      assignedUserId: "user-1",
      assignedUserName: "Nguyễn Văn A",
      status: "ACKNOWLEDGED"
    });
    vi.mocked(startTask).mockResolvedValue({
      ...task,
      assignedUserId: "user-1",
      assignedUserName: "Nguyễn Văn A",
      status: "IN_PROGRESS"
    });
    vi.mocked(completeTask).mockImplementation(async (taskId, request) => ({
      ...task,
      assignedUserId: "user-1",
      assignedUserName: "Nguyễn Văn A",
      completionNote: request.completionNote,
      status: "COMPLETED",
      taskId
    }));
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("loads alert detail and assigns tasks to a selected user only", async () => {
    const user = userEvent.setup();

    renderDetail();

    expect(await screen.findByText("Cảnh báo khu vực bến số 1")).toBeInTheDocument();
    expect(getAlert).toHaveBeenCalledWith("alert-1");
    expect(getAlertTasks).toHaveBeenCalledWith("alert-1");
    expect(screen.getByText("Nhiệm vụ cần thực hiện")).toBeInTheDocument();
    expect(screen.getByText("Tạm dừng khai thác bến số 1")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Quay lại danh sách cảnh báo" })).toHaveAttribute("href", "/alerts");

    await user.click(screen.getByRole("button", { name: "Phân công" }));

    expect(screen.queryByLabelText("Đội phụ trách")).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText("Người phụ trách"), "user-1");
    await user.click(screen.getByRole("button", { name: "Lưu phân công" }));

    expect(assignTask).toHaveBeenCalledWith("task-1", {
      assignedUserId: "user-1",
      dueAt: null
    });
    expect(await screen.findByText("Nguyễn Văn A")).toBeInTheDocument();

    expect(screen.queryByRole("button", { name: "Xác nhận" })).not.toBeInTheDocument();
  });
});
