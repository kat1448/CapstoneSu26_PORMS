import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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

function makeRunEvent(index: number): OperationEvent {
  return {
    actorName: "SYSTEM",
    entityType: "operation",
    eventType: "MODE_CHANGED",
    isSimulation: false,
    occurredAt: `04/07/2026 15:${String(index).padStart(2, "0")}:00`,
    occurredAtRaw: `2026-07-04T15:${String(index).padStart(2, "0")}:00Z`,
    operationEventId: `run-event-${index}`,
    portCode: `P${String(index).padStart(2, "0")}`,
    portId: `port-${index}`,
    portName: `Port ${index}`,
    simulationSessionId: null,
    summary: `Run event ${index}`,
    tone: "info",
    zoneId: null,
    zoneName: null
  };
}

describe("LogPage", () => {
  beforeEach(() => {
    vi.mocked(getOperationEvents).mockResolvedValue(events);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("groups one port run into a single row and opens a simulation-result-style detail view", async () => {
    const user = userEvent.setup();

    render(<LogPage refreshKey={0} />);

    expect(await screen.findByText("DNTSA")).toBeInTheDocument();
    expect(screen.getByText(/3\s*nhật ký/i)).toBeInTheDocument();
    expect(screen.queryByText("Simulation started")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /Chi tiết/i }));

    expect(screen.getByRole("heading", { name: /Chi tiết nhật ký vận hành/i })).toBeInTheDocument();
    expect(screen.getAllByText(/Cảng Tiên Sa/i).length).toBeGreaterThan(0);
    const eventList = screen.getByRole("list", { name: /Danh sách diễn biến vận hành/i });
    expect(within(eventList).getAllByRole("listitem")).toHaveLength(3);
    expect(within(eventList).getByText("Simulation started")).toBeInTheDocument();
    expect(within(eventList).getAllByText(/Bến số 1/i).length).toBeGreaterThan(0);
  });

  it("paginates grouped operation runs fifteen at a time", async () => {
    const user = userEvent.setup();
    vi.mocked(getOperationEvents).mockResolvedValue(Array.from({ length: 16 }, (_, index) => makeRunEvent(index + 1)));

    render(<LogPage refreshKey={0} />);

    expect(await screen.findByText("Trang 1/2")).toBeInTheDocument();
    expect(screen.getByText("P16")).toBeInTheDocument();
    expect(screen.getByText("P02")).toBeInTheDocument();
    expect(screen.queryByText("P01")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Sau" }));

    expect(screen.getByText("Trang 2/2")).toBeInTheDocument();
    expect(screen.queryByText("P16")).not.toBeInTheDocument();
    expect(screen.getByText("P01")).toBeInTheDocument();
  });

  it("filters grouped operation logs by port, zone, date range and level before paginating", async () => {
    const user = userEvent.setup();
    vi.mocked(getOperationEvents).mockResolvedValue([
      {
        ...makeRunEvent(1),
        occurredAt: "04/07/2026 15:27:00",
        occurredAtRaw: "2026-07-04T15:27:00Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        summary: "Log dung bo loc",
        tone: "warning",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      },
      {
        ...makeRunEvent(2),
        occurredAt: "04/07/2026 15:30:00",
        occurredAtRaw: "2026-07-04T15:30:00Z",
        portCode: "AB",
        portId: "port-other",
        portName: "Cang A",
        summary: "Log sai cang",
        tone: "warning",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      },
      {
        ...makeRunEvent(3),
        occurredAt: "04/07/2026 16:35:00",
        occurredAtRaw: "2026-07-04T16:35:00Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        summary: "Log sai khu vuc",
        tone: "warning",
        zoneId: "zone-other",
        zoneName: "Ben so 2"
      },
      {
        ...makeRunEvent(4),
        occurredAt: "04/07/2026 17:40:00",
        occurredAtRaw: "2026-07-04T17:40:00Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        summary: "Log sai muc",
        tone: "info",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      },
      {
        ...makeRunEvent(5),
        occurredAt: "05/07/2026 09:00:00",
        occurredAtRaw: "2026-07-05T09:00:00Z",
        portCode: "DNTSA",
        portId: "port-target",
        portName: "Cang Tien Sa",
        summary: "Log sai ngay",
        tone: "warning",
        zoneId: "zone-target",
        zoneName: "Ben so 1"
      }
    ]);

    const { container } = render(<LogPage refreshKey={0} />);
    const view = within(container);

    expect(await view.findByText("Cang A")).toBeInTheDocument();

    await user.selectOptions(view.getByLabelText(/Cảng/i), "port-target");
    await user.selectOptions(view.getByLabelText(/Khu vực/i), "Ben so 1");
    await user.selectOptions(view.getByLabelText(/Cấp độ/i), "warning");
    await user.type(view.getByLabelText(/Từ ngày/i), "2026-07-04");
    await user.type(view.getByLabelText(/Đến ngày/i), "2026-07-04");

    const detailButtons = view.getAllByRole("button", { name: /Chi tiết/i });
    expect(detailButtons).toHaveLength(1);
    await user.click(detailButtons[0]);

    expect(view.getByText("Log dung bo loc")).toBeInTheDocument();
    expect(view.queryByText("Log sai cang")).not.toBeInTheDocument();
    expect(view.queryByText("Log sai khu vuc")).not.toBeInTheDocument();
    expect(view.queryByText("Log sai muc")).not.toBeInTheDocument();
    expect(view.queryByText("Log sai ngay")).not.toBeInTheDocument();
    expect(view.queryByText("Trang 1/")).not.toBeInTheDocument();
  });

  it("searches simulation operation logs by session id and scenario name", async () => {
    const user = userEvent.setup();
    vi.mocked(getOperationEvents).mockResolvedValue([
      {
        ...makeRunEvent(1),
        isSimulation: true,
        portCode: "DNTSA",
        portId: "port-1",
        portName: "Cang Tien Sa",
        simulationDatasetName: "Kich ban bao Tien Sa",
        simulationSessionId: "storm-session-1",
        summary: "Storm scenario started"
      },
      {
        ...makeRunEvent(2),
        isSimulation: true,
        portCode: "DNTSA",
        portId: "port-1",
        portName: "Cang Tien Sa",
        simulationDatasetName: "Kich ban mua lon",
        simulationSessionId: "rain-session-2",
        summary: "Rain scenario started"
      }
    ]);

    const { container } = render(<LogPage refreshKey={0} />);
    const view = within(container);

    await user.click(await screen.findByRole("tab", { name: /mô phỏng/i }));
    await user.type(view.getByLabelText(/Phiên\/kịch bản/i), "bao tien");

    expect(view.getByText("Kich ban bao Tien Sa")).toBeInTheDocument();
    expect(view.queryByText("Kich ban mua lon")).not.toBeInTheDocument();

    await user.clear(view.getByLabelText(/Phiên\/kịch bản/i));
    await user.type(view.getByLabelText(/Phiên\/kịch bản/i), "rain-session");

    expect(view.queryByText("Kich ban bao Tien Sa")).not.toBeInTheDocument();
    expect(view.getByText("Kich ban mua lon")).toBeInTheDocument();
  });
});

