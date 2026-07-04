import { useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getOperationEvents } from "../services/logService";
import type { OperationEvent } from "../types/log";

type LogPageProps = {
  refreshKey: number;
};

type LogScope = "live" | "simulation";

type OperationLogRun = {
  events: OperationEvent[];
  firstAt: string;
  groupKey: string;
  lastAt: string;
  portCode: string;
  portName: string;
  riskTone: OperationEvent["tone"];
  runLabel: string;
};

const tonePriority: Record<OperationEvent["tone"], number> = {
  danger: 4,
  warning: 3,
  success: 2,
  info: 1
};

function badgeTone(tone: OperationEvent["tone"]): "danger" | "info" | "success" | "warning" {
  if (tone === "danger") return "danger";
  if (tone === "warning") return "warning";
  if (tone === "success") return "success";
  return "info";
}

function getRunBucket(event: OperationEvent) {
  if (event.simulationSessionId) {
    return event.simulationSessionId;
  }

  const date = new Date(event.occurredAtRaw);
  if (Number.isNaN(date.getTime())) {
    return event.occurredAt;
  }

  const bucket = new Date(date);
  bucket.setMinutes(0, 0, 0);
  return bucket.toISOString();
}

function groupOperationRuns(events: OperationEvent[]): OperationLogRun[] {
  const groups = new Map<string, OperationEvent[]>();

  events.forEach((event) => {
    const key = `${event.portId ?? event.portCode}|${getRunBucket(event)}`;
    groups.set(key, [...(groups.get(key) ?? []), event]);
  });

  return Array.from(groups.entries()).map(([groupKey, groupEvents]) => {
    const sortedEvents = [...groupEvents].sort((a, b) => new Date(a.occurredAtRaw).getTime() - new Date(b.occurredAtRaw).getTime());
    const newest = sortedEvents[sortedEvents.length - 1];
    const oldest = sortedEvents[0];
    const strongestTone = sortedEvents.reduce<OperationEvent["tone"]>((current, event) => (
      tonePriority[event.tone] > tonePriority[current] ? event.tone : current
    ), "info");

    return {
      events: sortedEvents,
      firstAt: oldest?.occurredAt ?? "-",
      groupKey,
      lastAt: newest?.occurredAt ?? "-",
      portCode: newest?.portCode ?? "N/A",
      portName: newest?.portName ?? newest?.portCode ?? "Không xác định",
      riskTone: strongestTone,
      runLabel: newest?.simulationSessionId ? `Phiên mô phỏng ${newest.simulationSessionId.slice(0, 8)}` : `Lần chạy ${newest?.portCode ?? "N/A"}`
    };
  }).sort((a, b) => new Date(b.events[b.events.length - 1]?.occurredAtRaw ?? 0).getTime() - new Date(a.events[a.events.length - 1]?.occurredAtRaw ?? 0).getTime());
}

export function LogPage({ refreshKey }: LogPageProps) {
  useDemoRefresh();
  const [events, setEvents] = useState<OperationEvent[]>([]);
  const [scope, setScope] = useState<LogScope>("live");
  const [selectedRunKey, setSelectedRunKey] = useState<string | null>(null);

  useEffect(() => {
    setSelectedRunKey(null);
    void getOperationEvents(scope).then(setEvents);
  }, [refreshKey, scope]);

  const runs = useMemo(() => groupOperationRuns(events), [events]);
  const selectedRun = runs.find((run) => run.groupKey === selectedRunKey) ?? null;

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký vận hành</h2>
          <p>Audit trail bất biến, gom các nhật ký trong cùng một lần chạy của từng cảng.</p>
        </div>
        <div aria-label="Loại nhật ký" className="segmented-control" role="tablist">
          <button
            aria-selected={scope === "live"}
            className={scope === "live" ? "active" : ""}
            onClick={() => setScope("live")}
            role="tab"
            type="button"
          >
            Nhật ký thật
          </button>
          <button
            aria-selected={scope === "simulation"}
            className={scope === "simulation" ? "active" : ""}
            onClick={() => setScope("simulation")}
            role="tab"
            type="button"
          >
            Nhật ký mô phỏng
          </button>
        </div>
      </div>

      {!selectedRun ? (
        <article className="card timeline-card">
          {runs.length === 0 ? (
            <div className="empty-state">
              <strong>Chưa có nhật ký</strong>
              <span>{scope === "simulation" ? "Chạy mô phỏng để xem sự kiện riêng." : "Chưa có sự kiện vận hành thật."}</span>
            </div>
          ) : null}
          {runs.map((run) => (
            <div className={`timeline-item tone-${run.riskTone}`} key={run.groupKey}>
              <div className="timeline-header">
                <strong>{run.portCode}</strong>
                <small>{run.firstAt} - {run.lastAt}</small>
              </div>
              <div className="timeline-meta">
                <Badge tone={badgeTone(run.riskTone)}>{run.events.length} nhật ký</Badge>
                <span>{run.portName}</span>
                <span>{run.runLabel}</span>
                <button className="button button-secondary button-small" onClick={() => setSelectedRunKey(run.groupKey)} type="button">
                  Chi tiết
                </button>
              </div>
            </div>
          ))}
        </article>
      ) : (
        <section className="simulation-results-page">
          <div className="section-heading">
            <div>
              <h2>Chi tiết nhật ký vận hành</h2>
              <p>{selectedRun.portName} · {selectedRun.firstAt} - {selectedRun.lastAt}</p>
            </div>
            <button className="button button-secondary" onClick={() => setSelectedRunKey(null)} type="button">Quay lại</button>
          </div>

          <div className="simulation-results-kpis">
            <article className="card card-pad simulation-results-kpi">
              <span>Cảng</span>
              <strong>{selectedRun.portCode}</strong>
              <small>{selectedRun.portName}</small>
            </article>
            <article className="card card-pad simulation-results-kpi">
              <span>Số nhật ký</span>
              <strong>{selectedRun.events.length}</strong>
              <small>{selectedRun.runLabel}</small>
            </article>
            <article className="card card-pad simulation-results-kpi risk">
              <span>Mức nổi bật</span>
              <strong>{selectedRun.riskTone.toUpperCase()}</strong>
              <Badge tone={badgeTone(selectedRun.riskTone)}>{selectedRun.riskTone.toUpperCase()}</Badge>
            </article>
          </div>

          <article className="card card-pad simulation-results-card">
            <div className="card-head">
              <div>
                <h3>Sự kiện trong lần chạy</h3>
                <p>Danh sách nhật ký của cùng một cảng, cùng một phiên hoặc cùng khung thời gian vận hành.</p>
              </div>
              <Badge tone="info">{selectedRun.events.length} dòng</Badge>
            </div>
            <div className="simulation-results-table-wrap">
              <table aria-label="Bảng sự kiện vận hành trong lần chạy" className="simulation-results-table">
                <thead>
                  <tr>
                    <th>Thời gian</th>
                    <th>Sự kiện</th>
                    <th>Khu vực</th>
                    <th>Loại</th>
                    <th>Người/nguồn</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedRun.events.map((event) => (
                    <tr key={event.operationEventId}>
                      <td>{event.occurredAt}</td>
                      <td><strong>{event.summary}</strong></td>
                      <td>{event.zoneName ?? "Toàn cảng"}</td>
                      <td><Badge tone={badgeTone(event.tone)}>{event.eventType}</Badge></td>
                      <td>{event.actorName}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </article>
        </section>
      )}
    </section>
  );
}
