import type { PortZone } from "../../types/port";
import { Badge } from "../common/Badge";

type ZoneListProps = {
  zones: PortZone[];
};

export function ZoneList({ zones }: ZoneListProps) {
  return (
    <div className="card table-card">
      <table className="data-table">
        <thead>
          <tr>
            <th>Khu vực</th>
            <th>Loại</th>
            <th>Sức chứa</th>
            <th>Rủi ro</th>
            <th>Trạng thái</th>
          </tr>
        </thead>
        <tbody>
          {zones.map((zone) => (
            <tr key={zone.zoneId}>
              <td>{zone.zoneName}</td>
              <td>{zone.zoneType}</td>
              <td>{zone.capacityLabel}</td>
              <td>
                <Badge
                  tone={
                    zone.currentRiskLevel === "CRITICAL"
                      ? "danger"
                      : zone.currentRiskLevel === "HIGH"
                        ? "warning"
                        : zone.currentRiskLevel === "MEDIUM"
                          ? "info"
                          : "success"
                  }
                >
                  {zone.currentRiskLevel}
                </Badge>
              </td>
              <td>{zone.statusLabel}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
