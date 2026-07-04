import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getOperationEvents } from "../services/logService";
import type { OperationEvent } from "../types/log";
import { LogPage } from "./LogPage";

vi.mock("../hooks/useDemoRefresh", () => ({
  useDemoRefresh: () => 0
}));

vi.mock("../services/logService", () => ({
  getOperationEvents: vi.fn()
}));

const events: OperationEvent[] = [
  {
    actorName: "SYSTEM",
    entityType: "simulation",
    eventType: "SIMULATION_COMPLETED",
    isSimulation: true,
    occurredAt: "04/07/2026 15:35:00",
    occurredAtRaw: "2026-07-04T15:35:00Z",
    operationEventId: "event-3",
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cảng Tiên Sa",
    simulationSessionId: "session-1",
    summary: "Simulation completed",
    tone: "success",
    zoneId: null,
    zoneName: null
  },
  {
    actorName: "SYSTEM",
    entityType: "simulation",
    eventType: "SIMULATION_STEP",
    isSimulation: true,
    occurredAt: "04/07/2026 15:30:00",
    occurredAtRaw: "2026-07-04T15:30:00Z",
    operationEventId: "event-2",
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cảng Tiên Sa",
    simulationSessionId: "session-1",
    summary: "Bến số 1 moved to CRITICAL",
    tone: "warning",
    zoneId: "zone-1",
    zoneName: "Bến số 1"
  },
  {
    actorName: "SYSTEM",
    entityType: "simulation",
    eventType: "SIMULATION_STARTED",
    isSimulation: true,
    occurredAt: "04/07/2026 15:27:00",
    occurredAtRaw: "2026-07-04T15:27:00Z",
    operationEventId: "event-1",
    portCode: "DNTSA",
    portId: "port-1",
    portName: "Cảng Tiên Sa",
    simulationSessionId: "session-1",
    summary: "Simulation started",
    tone: "info",
    zoneId: null,
    zoneName: null
  }
];

describe("LogPage", () => {
  beforeEach(() => {
    vi.mocked(getOperationEvents).mockResolvedValue(events);
  });

  it("groups one port run into a single row and opens a simulation-result-style detail view", async () => {
    const user = userEvent.setup();

    render(<LogPage refreshKey={0} />);

    expect(await screen.findByText("DNTSA")).toBeInTheDocument();
    expect(screen.getByText("3 nhật ký")).toBeInTheDocument();
    expect(screen.queryByText("Simulation started")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Chi tiết" }));

    expect(screen.getByRole("heading", { name: "Chi tiết nhật ký vận hành" })).toBeInTheDocument();
    expect(screen.getByText("Cảng Tiên Sa")).toBeInTheDocument();
    const table = screen.getByRole("table", { name: "Bảng sự kiện vận hành trong lần chạy" });
    expect(within(table).getAllByRole("row")).toHaveLength(4);
    expect(within(table).getByText("Simulation started")).toBeInTheDocument();
    expect(within(table).getByText("Bến số 1")).toBeInTheDocument();
  });
});
