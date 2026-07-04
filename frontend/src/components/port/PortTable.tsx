import { Link } from "react-router-dom";
import type { PortSummary } from "../../types/port";
import { Badge } from "../common/Badge";

function riskTone(risk: PortSummary["currentRiskLevel"]) {
  if (risk === "CRITICAL") return "danger";
  if (risk === "HIGH") return "warning";
  if (risk === "MEDIUM") return "info";
  return "success";
}

export function PortTable({ ports }: { ports: PortSummary[] }) {
  return (
    <div className="card table-card">
      <table className="data-table port-table">
        <thead>
          <tr>
            <th>Mã cảng</th>
            <th>Tên cảng</th>
            <th>Trạng thái</th>
            <th>Mức rủi ro</th>
            <th>Chế độ vận hành</th>
            <th>Cảnh báo</th>
            <th>Cập nhật lúc</th>
            <th>Thao tác</th>
          </tr>
        </thead>
        <tbody>
          {ports.map((port) => (
            <tr key={port.portId}>
              <td><strong>{port.portCode}</strong></td>
              <td>{port.portName}</td>
              <td><Badge tone={port.isActive ? "success" : "muted"}>{port.isActive ? "Hoạt động" : "Tạm ngưng"}</Badge></td>
              <td><Badge tone={riskTone(port.currentRiskLevel)}>{port.currentRiskLevel}</Badge></td>
              <td>{port.currentOperationMode}</td>
              <td>{port.activeAlertCount}</td>
              <td>{port.updatedAtLabel}</td>
              <td>
                <Link aria-label={`Chi tiết ${port.portName}`} className="button button-secondary button-small" to={`/ports/${port.portId}`}>
                  Chi tiết
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
