import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getAlerts } from "../services/alertService";
import type { AlertItem } from "../types/alert";

type AlertPageProps = {
  refreshKey: number;
};

const PAGE_SIZE = 15;
const riskOptions = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

function uniqueBy<T>(items: T[], keyOf: (item: T) => string) {
  const seen = new Set<string>();
  return items.filter((item) => {
    const key = keyOf(item);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function parseAlertDate(value: string) {
  const [datePart] = value.split(" ");
  const parts = datePart.split("/");
  if (parts.length === 3) {
    const [day, month, year] = parts;
    return `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`;
  }

  return value.slice(0, 10);
}

function badgeTone(level: string) {
  if (level === "CRITICAL") return "danger";
  if (level === "HIGH") return "warning";
  return "info";
}

export function AlertPage({ refreshKey }: AlertPageProps) {
  useDemoRefresh();
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedPortId, setSelectedPortId] = useState("");
  const [selectedZoneName, setSelectedZoneName] = useState("");
  const [selectedSeverity, setSelectedSeverity] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  useEffect(() => {
    void getAlerts().then(setAlerts);
  }, [refreshKey]);

  const portOptions = useMemo(
    () => uniqueBy(alerts, (alert) => alert.portId).map((alert) => ({
      portId: alert.portId,
      label: `${alert.portCode} - ${alert.portName}`
    })),
    [alerts]
  );
  const zoneOptions = useMemo(() => {
    const scopedAlerts = selectedPortId ? alerts.filter((alert) => alert.portId === selectedPortId) : alerts;
    return [...new Set(scopedAlerts.map((alert) => alert.zoneName).filter(Boolean))].sort();
  }, [alerts, selectedPortId]);

  useEffect(() => {
    if (selectedZoneName && !zoneOptions.includes(selectedZoneName)) {
      setSelectedZoneName("");
    }
  }, [selectedZoneName, zoneOptions]);

  const filteredAlerts = useMemo(() => alerts.filter((alert) => {
    const alertDate = parseAlertDate(alert.createdAt);

    return (!selectedPortId || alert.portId === selectedPortId)
      && (!selectedZoneName || alert.zoneName === selectedZoneName)
      && (!selectedSeverity || alert.severity === selectedSeverity)
      && (!fromDate || alertDate >= fromDate)
      && (!toDate || alertDate <= toDate);
  }), [alerts, fromDate, selectedPortId, selectedSeverity, selectedZoneName, toDate]);

  const totalPages = Math.max(1, Math.ceil(filteredAlerts.length / PAGE_SIZE));
  const visibleAlerts = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return filteredAlerts.slice(start, start + PAGE_SIZE);
  }, [filteredAlerts, currentPage]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  useEffect(() => {
    setCurrentPage(1);
  }, [fromDate, selectedPortId, selectedSeverity, selectedZoneName, toDate]);

  function resetFilters() {
    setSelectedPortId("");
    setSelectedZoneName("");
    setSelectedSeverity("");
    setFromDate("");
    setToDate("");
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Cảnh báo</h2>
          <p>Theo dõi và xác nhận các cảnh báo vận hành</p>
        </div>
      </div>
      <div className="card toolbar sop-toolbar filter-toolbar">
        <label>
          <span>Cảng</span>
          <select className="select-input" onChange={(event) => setSelectedPortId(event.target.value)} value={selectedPortId}>
            <option value="">Tất cả cảng</option>
            {portOptions.map((port) => (
              <option key={port.portId} value={port.portId}>{port.label}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Khu vực</span>
          <select className="select-input" onChange={(event) => setSelectedZoneName(event.target.value)} value={selectedZoneName}>
            <option value="">Tất cả khu vực</option>
            {zoneOptions.map((zone) => (
              <option key={zone} value={zone}>{zone}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Từ ngày</span>
          <input className="input" onChange={(event) => setFromDate(event.target.value)} type="date" value={fromDate} />
        </label>
        <label>
          <span>Đến ngày</span>
          <input className="input" onChange={(event) => setToDate(event.target.value)} type="date" value={toDate} />
        </label>
        <label>
          <span>Cấp độ rủi ro</span>
          <select className="select-input" onChange={(event) => setSelectedSeverity(event.target.value)} value={selectedSeverity}>
            <option value="">Tất cả cấp độ</option>
            {riskOptions.map((risk) => (
              <option key={risk} value={risk}>{risk}</option>
            ))}
          </select>
        </label>
        <button className="button button-secondary button-small" onClick={resetFilters} type="button">Xóa lọc</button>
      </div>
      <div className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Cảng</th>
              <th>Khu vực</th>
              <th>Mức độ</th>
              <th>Loại</th>
              <th>Nội dung</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {visibleAlerts.length === 0 ? (
              <tr>
                <td colSpan={7}>Không có cảnh báo phù hợp.</td>
              </tr>
            ) : null}
            {visibleAlerts.map((alert) => (
              <tr key={alert.alertId}>
                <td>{alert.createdAt}</td>
                <td>{alert.portCode} - {alert.portName}</td>
                <td>{alert.zoneName}</td>
                <td>
                  <Badge tone={badgeTone(alert.severity)}>
                    {alert.severity}
                  </Badge>
                </td>
                <td>{alert.alertType}</td>
                <td>
                  <strong>{alert.title}</strong>
                  <p>{alert.message}</p>
                </td>
                <td>
                  <Link className="button button-secondary button-small" to={`/alerts/${alert.alertId}`}>
                    Chi tiết
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {totalPages > 1 ? (
          <div className="table-pagination" aria-label="Phân trang cảnh báo">
            <button
              className="button button-secondary button-small"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
              type="button"
            >
              Trước
            </button>
            <span>Trang {currentPage}/{totalPages}</span>
            <button
              className="button button-secondary button-small"
              disabled={currentPage === totalPages}
              onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
              type="button"
            >
              Sau
            </button>
          </div>
        ) : null}
      </div>
    </section>
  );
}
