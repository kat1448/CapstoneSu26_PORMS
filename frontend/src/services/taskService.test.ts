import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { acknowledgeTask, assignTask, completeTask, getTaskAssignees, getTasks, startTask } from "./taskService";

describe("taskService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("loads task logs through the API", async () => {
    const response = [{
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
    }];
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(getTasks()).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/tasks", expect.objectContaining({
      headers: expect.objectContaining({ "Content-Type": "application/json" })
    }));
  });

  it("loads task assignees through the API", async () => {
    const response = [{
      email: "operator@porms.vn",
      fullName: "Operator",
      role: "STANDARD_USER",
      userId: "user-1"
    }];
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(getTaskAssignees()).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/tasks/assignees", expect.objectContaining({
      headers: expect.objectContaining({ "Content-Type": "application/json" })
    }));
  });

  it("updates task workflow through patch endpoints", async () => {
    const response = {
      createdAt: "2026-07-04T15:27:03Z",
      isSimulation: false,
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cảng Tiên Sa",
      priority: "HIGH",
      status: "ACKNOWLEDGED",
      taskCode: "TASK-REAL-001",
      taskId: "task-1",
      title: "Kiểm tra khu vực",
      updatedAt: "2026-07-04T15:27:03Z"
    };
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...response, status: "ACKNOWLEDGED" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...response, status: "IN_PROGRESS" }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...response, status: "COMPLETED" }), { status: 200 }));

    await assignTask("task-1", { assignedUserId: "user-1", dueAt: null });
    await acknowledgeTask("task-1");
    await startTask("task-1");
    await completeTask("task-1", { completionNote: "Đã hoàn tất" });

    expect(fetch).toHaveBeenNthCalledWith(1, "http://localhost:5000/api/tasks/task-1/assignment", expect.objectContaining({
      body: JSON.stringify({ assignedUserId: "user-1", dueAt: null }),
      method: "PATCH"
    }));
    expect(fetch).toHaveBeenNthCalledWith(2, "http://localhost:5000/api/tasks/task-1/acknowledge", expect.objectContaining({ method: "PATCH" }));
    expect(fetch).toHaveBeenNthCalledWith(3, "http://localhost:5000/api/tasks/task-1/start", expect.objectContaining({ method: "PATCH" }));
    expect(fetch).toHaveBeenNthCalledWith(4, "http://localhost:5000/api/tasks/task-1/complete", expect.objectContaining({
      body: JSON.stringify({ completionNote: "Đã hoàn tất" }),
      method: "PATCH"
    }));
  });
});
