import { useEffect, useState } from "react";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getAlerts } from "../services/alertService";
import type { AlertItem } from "../types/alert";

type AlertPageProps = {
  refreshKey: number;
};

export function AlertPage({ refreshKey }: AlertPageProps) {
  useDemoRefresh();
  const [alerts, setAlerts] = useState<AlertItem[]>([]);

  useEffect(() => {
    void getAlerts().then(setAlerts);
  }, [refreshKey]);

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Cảnh báo</h2>
          <p>Theo dõi và xác nhận các cảnh báo vận hành</p>
        </div>
      </div>
      <div className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Mức độ</th>
              <th>Nội dung</th>
              <th>Loại</th>
              <th>Khu vực</th>
              <th>Thời gian</th>
            </tr>
          </thead>
          <tbody>
            {alerts.map((alert) => (
              <tr key={alert.alertId}>
                <td>
                  <Badge tone={alert.severity === "CRITICAL" ? "danger" : alert.severity === "HIGH" ? "warning" : "info"}>
                    {alert.severity}
                  </Badge>
                </td>
                <td>
                  <strong>{alert.title}</strong>
                  <p>{alert.message}</p>
                </td>
                <td>{alert.alertType}</td>
                <td>{alert.zoneName}</td>
                <td>{alert.createdAt}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
