import { Link } from "react-router-dom";
import type { PortSummary } from "../../types/port";
import { Badge } from "../common/Badge";

type PortListCardProps = {
  port: PortSummary;
};

export function PortListCard({ port }: PortListCardProps) {
  const tone =
    port.currentRiskLevel === "CRITICAL"
      ? "danger"
      : port.currentRiskLevel === "HIGH"
        ? "warning"
        : port.currentRiskLevel === "MEDIUM"
          ? "info"
          : "success";

  return (
    <article className="card port-card">
      <div className="port-card-head">
        <div>
          <h3>{port.portName}</h3>
          <p>
            {port.portCode} · {port.updatedAtLabel}
          </p>
        </div>
        <Badge tone={tone}>{port.currentRiskLevel}</Badge>
      </div>
      <div className="port-stats">
        <div>
          <span>Mode</span>
          <strong>{port.currentOperationMode}</strong>
        </div>
        <div>
          <span>Cảnh báo</span>
          <strong>{port.activeAlertCount}</strong>
        </div>
      </div>
      <Link className="button button-secondary" to={`/ports/${port.portId}`}>
        Chi tiết
      </Link>
    </article>
  );
}
