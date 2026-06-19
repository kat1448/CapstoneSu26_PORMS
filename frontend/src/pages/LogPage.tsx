import { useEffect, useState } from "react";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getOperationEvents } from "../services/logService";
import type { OperationEvent } from "../types/log";

type LogPageProps = {
  refreshKey: number;
};

export function LogPage({ refreshKey }: LogPageProps) {
  useDemoRefresh();
  const [events, setEvents] = useState<OperationEvent[]>([]);

  useEffect(() => {
    void getOperationEvents().then(setEvents);
  }, [refreshKey]);

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Nhật ký vận hành</h2>
          <p>Audit trail bất biến của toàn bộ sự kiện hệ thống</p>
        </div>
      </div>
      <article className="card timeline-card">
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
