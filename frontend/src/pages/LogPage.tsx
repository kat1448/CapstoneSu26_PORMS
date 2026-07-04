import { useEffect, useState } from "react";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getOperationEvents } from "../services/logService";
import type { OperationEvent } from "../types/log";

type LogPageProps = {
  refreshKey: number;
};

type LogScope = "live" | "simulation";

export function LogPage({ refreshKey }: LogPageProps) {
  useDemoRefresh();
  const [events, setEvents] = useState<OperationEvent[]>([]);
  const [scope, setScope] = useState<LogScope>("live");

  useEffect(() => {
    void getOperationEvents(scope).then(setEvents);
  }, [refreshKey, scope]);

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký vận hành</h2>
          <p>Audit trail bất biến, tách riêng dữ liệu thật và dữ liệu mô phỏng</p>
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
      <article className="card timeline-card">
        {events.length === 0 ? (
          <div className="empty-state">
            <strong>Chưa có nhật ký</strong>
            <span>{scope === "simulation" ? "Chạy mô phỏng để xem sự kiện riêng." : "Chưa có sự kiện vận hành thật."}</span>
          </div>
        ) : null}
        {events.map((event) => (
          <div className={`timeline-item tone-${event.tone}`} key={event.operationEventId}>
            <div className="timeline-header">
              <strong>{event.summary}</strong>
              <small>{event.occurredAt}</small>
            </div>
            <div className="timeline-meta">
              <Badge tone={event.tone === "danger" ? "danger" : event.tone === "warning" ? "warning" : "info"}>
                {event.eventType}
              </Badge>
              <span>{event.actorName}</span>
            </div>
          </div>
        ))}
      </article>
    </section>
  );
}
