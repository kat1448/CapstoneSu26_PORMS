import { Link } from "react-router-dom";
import type { PortZone } from "../../types/port";
import { Badge } from "../common/Badge";
import { Icon } from "../common/Icon";

type ZoneStatusCardProps = {
  portId: string;
  zones: PortZone[];
};

export function ZoneStatusCard({ portId, zones }: ZoneStatusCardProps) {
  return (
    <article className="card card-pad zone-status-card">
      <div className="card-head">
        <div><h3>Trạng thái khu vực</h3><p>{zones.length} khu vực đang được giám sát</p></div>
        <Link className="text-link" to={`/ports/${portId}`}>Xem chi tiết →</Link>
      </div>
      <div className="zone-grid">
        {zones.map((zone) => (
          <div className="zone-card" key={zone.zoneId}>
            <span className="zone-icon"><Icon name={zone.zoneType === "DOCK" ? "port" : "dashboard"} /></span>
            <span className="zone-info"><strong>{zone.zoneName}</strong><small>{zone.zoneType} · {zone.statusLabel}</small></span>
            <Badge tone={zone.currentRiskLevel === "CRITICAL" ? "danger" : zone.currentRiskLevel === "HIGH" ? "warning" : zone.currentRiskLevel === "LOW" ? "success" : "info"}>{zone.currentRiskLevel}</Badge>
          </div>
        ))}
      </div>
    </article>
  );
}
