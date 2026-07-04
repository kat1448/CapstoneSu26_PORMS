import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getTasks } from "./taskService";

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
});
