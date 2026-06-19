import type { OperationEvent } from "../../types/log";
import { Badge } from "../common/Badge";

type OperationLogCardProps = {
  events: OperationEvent[];
};

export function OperationLogCard({ events }: OperationLogCardProps) {
  return (
    <article className="card side-card">
      <div className="section-heading compact">
        <div>
          <h3>Nhật ký gần đây</h3>
          <p>Audit trail của hệ thống</p>
        </div>
      </div>
      <div className="timeline">
        {events.slice(0, 4).map((event) => (
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
      </div>
    </article>
  );
}
