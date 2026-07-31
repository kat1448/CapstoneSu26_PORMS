import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { getTasks, type TaskLogRecord } from "../services/taskService";
import { TasksPage } from "./TasksPage";

vi.mock("../services/taskService", () => ({
  getTasks: vi.fn()
}));

function makeTask(index: number): TaskLogRecord {
  return {
    assignedTeam: "Team A",
    assignedUserId: null,
    assignedUserName: null,
    createdAt: `2026-07-04T15:${String(index).padStart(2, "0")}:03Z`,
    description: "Created from SOP",
    dueAt: null,
    isSimulation: false,
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cang Tien Sa",
    priority: "HIGH",
    simulationSessionId: null,
    status: "NEW",
    taskCode: `TASK-PAGE-${String(index).padStart(3, "0")}`,
    taskId: `task-${index}`,
    title: `Task row ${index}`,
    updatedAt: `2026-07-04T15:${String(index).padStart(2, "0")}:03Z`,
    zoneId: `zone-${index}`,
    zoneName: `Zone ${index}`
  };
}

function renderTasks() {
  return render(<MemoryRouter><TasksPage /></MemoryRouter>);
}

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

    renderTasks();

    expect(screen.getByRole("heading", { name: "Quản lý nhiệm vụ" })).toBeInTheDocument();
    expect(await screen.findByText("Kiểm tra khu vực rủi ro")).toBeInTheDocument();
    expect(screen.getAllByText("Bến số 1").length).toBeGreaterThan(0);
    expect(screen.getByText("Đội vận hành")).toBeInTheDocument();
    expect(screen.queryByText("TASK-2026-041")).not.toBeInTheDocument();
  });

  it("paginates task log rows fifteen at a time", async () => {
    const user = userEvent.setup();
    vi.mocked(getTasks).mockResolvedValue(Array.from({ length: 16 }, (_, index) => makeTask(index + 1)));

    renderTasks();

    expect(await screen.findByText("Trang 1/2")).toBeInTheDocument();
    expect(screen.getByText("Task row 1")).toBeInTheDocument();
    expect(screen.getByText("Task row 15")).toBeInTheDocument();
    expect(screen.queryByText("Task row 16")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Sau" }));

    expect(screen.getByText("Trang 2/2")).toBeInTheDocument();
    expect(screen.queryByText("Task row 1")).not.toBeInTheDocument();
    expect(screen.getByText("Task row 16")).toBeInTheDocument();
  });

  it("filters task log by port, zone, date range and risk level before paginating", async () => {
    const user = userEvent.setup();
    vi.mocked(getTasks).mockResolvedValue([
      {
        ...makeTask(1),
        createdAt: "2026-07-04T15:27:03Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        priority: "CRITICAL",
        taskCode: "TASK-FILTER-001",
        taskId: "task-target",
        title: "Task dung bo loc",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      },
      {
        ...makeTask(2),
        createdAt: "2026-07-04T15:30:03Z",
        portCode: "AB",
        portId: "port-other",
        portName: "Cang A",
        priority: "CRITICAL",
        taskCode: "TASK-FILTER-002",
        taskId: "task-other-port",
        title: "Task sai cang",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      },
      {
        ...makeTask(3),
        createdAt: "2026-07-04T15:35:03Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        priority: "CRITICAL",
        taskCode: "TASK-FILTER-003",
        taskId: "task-other-zone",
        title: "Task sai khu vuc",
        zoneId: "zone-other",
        zoneName: "Ben so 2"
      },
      {
        ...makeTask(4),
        createdAt: "2026-07-04T15:40:03Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        priority: "HIGH",
        taskCode: "TASK-FILTER-004",
        taskId: "task-other-risk",
        title: "Task sai muc do",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      },
      {
        ...makeTask(5),
        createdAt: "2026-07-05T09:00:00Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        priority: "CRITICAL",
        taskCode: "TASK-FILTER-005",
        taskId: "task-other-date",
        title: "Task sai ngay",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      }
    ]);

    const { container } = renderTasks();
    const view = within(container);

    expect(await view.findByText("Task dung bo loc")).toBeInTheDocument();

    await user.selectOptions(view.getByLabelText("Cảng"), "port-target");
    await user.selectOptions(view.getByLabelText("Khu vực"), "Ben so 1");
    await user.selectOptions(view.getByLabelText("Mức ưu tiên"), "CRITICAL");
    await user.type(view.getByLabelText("Từ ngày"), "2026-07-04");
    await user.type(view.getByLabelText("Đến ngày"), "2026-07-04");

    expect(view.getByText("Task dung bo loc")).toBeInTheDocument();
    expect(view.queryByText("Task sai cang")).not.toBeInTheDocument();
    expect(view.queryByText("Task sai khu vuc")).not.toBeInTheDocument();
    expect(view.queryByText("Task sai muc do")).not.toBeInTheDocument();
    expect(view.queryByText("Task sai ngay")).not.toBeInTheDocument();
    expect(view.queryByText("Trang 1/")).not.toBeInTheDocument();
  });
});
