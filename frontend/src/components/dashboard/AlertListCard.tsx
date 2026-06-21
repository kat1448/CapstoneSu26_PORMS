import { Link } from "react-router-dom";
import type { AlertItem } from "../../types/alert";
import { Icon } from "../common/Icon";

export function AlertListCard({ alerts }: { alerts: AlertItem[] }) {
  const active = [...alerts].sort((a, b) => Number(a.read) - Number(b.read)).slice(0, 4);
  return (
    <article className="card card-pad side-card">
      <div className="card-head">
        <div><h3>Cảnh báo đang hoạt động</h3><p>{alerts.filter((item) => !item.read).length} cảnh báo chưa đọc</p></div>
        <Link className="text-link" to="/alerts">Xem tất cả</Link>
      </div>
      <div className="list">
        {active.map((alert) => (
          <div className="list-row" key={alert.alertId}>
            <span className={`list-icon severity-${alert.severity.toLowerCase()}`}><Icon name="alert" /></span>
            <div className="list-main"><strong>{alert.title}</strong><p>{alert.zoneName} · {alert.message.slice(0, 72)}{alert.message.length > 72 ? "…" : ""}</p></div>
            <span className="list-time">{alert.createdAt}</span>
          </div>
        ))}
      </div>
    </article>
  );
}
