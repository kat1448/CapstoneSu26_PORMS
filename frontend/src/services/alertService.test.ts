import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { acknowledgeAlert, getAlert, getAlertTasks } from "./alertService";

describe("alertService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("loads one alert through the API", async () => {
    const response = {
      alertId: "alert-1",
      alertType: "WEATHER",
      createdAt: "2026-07-04T15:27:03Z",
      message: "Gió mạnh tại bến số 1.",
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cảng Tiên Sa",
      read: false,
      severity: "HIGH",
      title: "Cảnh báo khu vực bến số 1",
      zoneName: null
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(getAlert("alert-1")).resolves.toEqual({
      ...response,
      createdAt: expect.stringMatching(/\d{2}\/\d{2}\/2026 \d{2}:\d{2}:\d{2}/),
      createdAtIso: response.createdAt,
      zoneName: "Toàn cảng"
    });

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/alerts/alert-1", expect.objectContaining({
      headers: expect.objectContaining({ "Content-Type": "application/json" })
    }));
  });

  it("loads tasks for a selected alert through the API", async () => {
    const response = [{
      alertId: "alert-1",
      createdAt: "2026-07-04T15:27:03Z",
      isSimulation: false,
      portCode: "DNTSA",
      portId: "port-1",
      portName: "Cảng Tiên Sa",
      priority: "HIGH",
      status: "NEW",
      taskCode: "TASK-001",
      taskId: "task-1",
      title: "Tạm dừng khai thác bến số 1",
      updatedAt: "2026-07-04T15:27:03Z"
    }];
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(getAlertTasks("alert-1")).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/alerts/alert-1/tasks", expect.objectContaining({
      headers: expect.objectContaining({ "Content-Type": "application/json" })
    }));
  });

  it("acknowledges one alert through the API", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(acknowledgeAlert("alert-1")).resolves.toBeUndefined();

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/alerts/alert-1/acknowledge", expect.objectContaining({
      headers: expect.objectContaining({ "Content-Type": "application/json" }),
      method: "PATCH"
    }));
  });
});
