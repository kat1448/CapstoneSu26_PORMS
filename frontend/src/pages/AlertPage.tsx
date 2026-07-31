import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getAlerts } from "../services/alertService";
import type { AlertItem } from "../types/alert";
import { riskLabel } from "../utils/displayLabels";

type AlertPageProps = { refreshKey: number };

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

function alertTypeLabel(type: string) {
  const labels: Record<string, string> = {
    SIMULATION: "Mô phỏng vận hành",
    WEATHER: "Thời tiết",
    THRESHOLD_EXCEEDED: "Vượt ngưỡng an toàn",
    RISK_LEVEL_CHANGED: "Thay đổi mức rủi ro",
    MODE_CHANGED: "Thay đổi chế độ vận hành",
    STORM_WARNING: "Cảnh báo thời tiết nguy hiểm",
    SOP_TRIGGERED: "Kích hoạt quy trình ứng phó"
  };
  return labels[type.toUpperCase()] ?? "Cảnh báo vận hành";
}

function alertStatusLabel(alert: AlertItem) {
  if (alert.acknowledged || alert.status === "ACKNOWLEDGED") return "Đã xác nhận";
  if (alert.read || alert.status === "READ") return "Đã xem";
  return "Mới";
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

  useEffect(() => { void getAlerts().then(setAlerts); }, [refreshKey]);

  const portOptions = useMemo(
    () => uniqueBy(alerts, (alert) => alert.portId).map((alert) => ({
      portId: alert.portId,
      label: `${alert.portCode} - ${alert.portName}`
    })),
    [alerts]
  );

  const zoneOptions = useMemo(() => {
    const scoped = selectedPortId ? alerts.filter((alert) => alert.portId === selectedPortId) : alerts;
    return [...new Set(scoped.map((alert) => alert.zoneName).filter(Boolean))].sort();
  }, [alerts, selectedPortId]);

  useEffect(() => {
    if (selectedZoneName && !zoneOptions.includes(selectedZoneName)) setSelectedZoneName("");
  }, [selectedZoneName, zoneOptions]);

  const filteredAlerts = useMemo(() => alerts.filter((alert) => {
    const alertDate = parseAlertDate(alert.createdAt);
    return (!selectedPortId || alert.portId === selectedPortId)
      && (!selectedZoneName || alert.zoneName === selectedZoneName)
      && (!selectedSeverity || alert.severity === selectedSeverity)
      && (!fromDate || alertDate >= fromDate)
      && (!toDate || alertDate <= toDate);
  }), [alerts, fromDate, selectedPortId, selectedSeverity, selectedZoneName, toDate]);

  const summary = useMemo(() => ({
    active: alerts.filter((alert) => !alert.acknowledged).length,
    critical: alerts.filter((alert) => alert.severity === "CRITICAL" && !alert.acknowledged).length,
    high: alerts.filter((alert) => alert.severity === "HIGH" && !alert.acknowledged).length,
    acknowledged: alerts.filter((alert) => alert.acknowledged || alert.status === "ACKNOWLEDGED").length
  }), [alerts]);

  const totalPages = Math.max(1, Math.ceil(filteredAlerts.length / PAGE_SIZE));
  const visibleAlerts = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return filteredAlerts.slice(start, start + PAGE_SIZE);
  }, [filteredAlerts, currentPage]);

  useEffect(() => setCurrentPage((page) => Math.min(page, totalPages)), [totalPages]);
  useEffect(() => setCurrentPage(1), [fromDate, selectedPortId, selectedSeverity, selectedZoneName, toDate]);

  function resetFilters() {
    setSelectedPortId("");
    setSelectedZoneName("");
    setSelectedSeverity("");
    setFromDate("");
    setToDate("");
  }

  return (
    <section className="page-grid alerts-page alerts-page-v2">
      <section className="alerts-overview-shell">
        <header className="alerts-command-hero">
          <div className="alerts-command-copy">
            <div className="alerts-command-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24"><path d="M12 3 3.8 6.5v5.3c0 4.7 3.2 8.2 8.2 9.2 5-1 8.2-4.5 8.2-9.2V6.5L12 3Z"/><path d="M12 8v5"/><path d="M12 16.5h.01"/></svg>
            </div>
            <div>
              <span className="page-eyebrow">ĐIỀU HÀNH AN TOÀN</span>
              <h1>Trung tâm cảnh báo</h1>
              <p>Nắm bắt tình hình, ưu tiên xử lý và phối hợp ứng phó tại các khu vực cảng.</p>
            </div>
          </div>
          <div className="alerts-system-state">
            <div className="alerts-live-indicator"><span /> Hệ thống đang giám sát</div>
            <small>Cập nhật cảnh báo vận hành theo thời gian thực</small>
          </div>
        </header>

        <div className="alert-summary-grid" aria-label="Tổng hợp cảnh báo">
          <article className="alert-summary-card summary-active">
            <span className="summary-card-icon" aria-hidden="true">!</span>
            <div><span>Chờ tiếp nhận</span><strong>{summary.active}</strong><small>Cảnh báo cần được xác nhận</small></div>
          </article>
          <article className="alert-summary-card summary-critical">
            <span className="summary-card-icon" aria-hidden="true">!!</span>
            <div><span>Mức rất cao</span><strong>{summary.critical}</strong><small>Yêu cầu hành động ngay</small></div>
          </article>
          <article className="alert-summary-card summary-high">
            <span className="summary-card-icon" aria-hidden="true">↑</span>
            <div><span>Mức cao</span><strong>{summary.high}</strong><small>Cần chủ động ứng phó</small></div>
          </article>
          <article className="alert-summary-card summary-done">
            <span className="summary-card-icon" aria-hidden="true">✓</span>
            <div><span>Đã tiếp nhận</span><strong>{summary.acknowledged}</strong><small>Đã có người phụ trách</small></div>
          </article>
        </div>
      </section>

      <section className="alert-workspace">
        <div className="alert-workspace-toolbar">
          <div>
            <span className="workspace-kicker">NHẬT KÝ CẢNH BÁO</span>
            <h2>Tình hình tại các cảng</h2>
            <p>Hiển thị {filteredAlerts.length} trên tổng số {alerts.length} cảnh báo</p>
          </div>
          <button className="alert-reset-button" onClick={resetFilters} type="button">
            <span aria-hidden="true">↻</span> Đặt lại bộ lọc
          </button>
        </div>

        <div className="alert-filter-panel">
          <label><span>Cảng theo dõi</span><select aria-label="Cảng" className="select-input" onChange={(event) => setSelectedPortId(event.target.value)} value={selectedPortId}><option value="">Tất cả cảng</option>{portOptions.map((port) => <option key={port.portId} value={port.portId}>{port.label}</option>)}</select></label>
          <label><span>Khu vực</span><select className="select-input" onChange={(event) => setSelectedZoneName(event.target.value)} value={selectedZoneName}><option value="">Tất cả khu vực</option>{zoneOptions.map((zone) => <option key={zone} value={zone}>{zone}</option>)}</select></label>
          <label><span>Mức cảnh báo</span><select aria-label="Cấp độ rủi ro" className="select-input" onChange={(event) => setSelectedSeverity(event.target.value)} value={selectedSeverity}><option value="">Tất cả cấp độ</option>{riskOptions.map((risk) => <option key={risk} value={risk}>{riskLabel(risk)}</option>)}</select></label>
          <label><span>Từ ngày</span><input className="input" onChange={(event) => setFromDate(event.target.value)} type="date" value={fromDate} /></label>
          <label><span>Đến ngày</span><input className="input" onChange={(event) => setToDate(event.target.value)} type="date" value={toDate} /></label>
        </div>

        <div className="alert-feed" aria-label="Danh sách cảnh báo">
          {visibleAlerts.length === 0 ? (
            <div className="alert-empty-state">
              <span aria-hidden="true">✓</span>
              <strong>{alerts.length ? "Không tìm thấy cảnh báo phù hợp" : "Các cảng đang vận hành ổn định"}</strong>
              <p>{alerts.length ? "Bạn hãy thay đổi phạm vi hoặc đặt lại bộ lọc để xem cảnh báo khác." : "Hệ thống sẽ thông báo ngay khi phát hiện tình huống cần chú ý."}</p>
            </div>
          ) : visibleAlerts.map((alert) => {
            const status = alertStatusLabel(alert);
            return (
              <article className={`alert-feed-item severity-${alert.severity.toLowerCase()}`} key={alert.alertId}>
                <div className="alert-feed-severity" aria-hidden="true"><span>!</span></div>
                <div className="alert-feed-main">
                  <div className="alert-feed-topline">
                    <div className="alert-feed-tags">
                      <Badge tone={badgeTone(alert.severity)}>{riskLabel(alert.severity)}</Badge>
                      <span className="alert-kind-tag">{alertTypeLabel(alert.alertType)}</span>
                      <span className={`alert-status status-${status === "Mới" ? "new" : "done"}`}><i />{status}</span>
                    </div>
                    <time>{alert.createdAt}</time>
                  </div>
                  <h3>{alert.title}</h3>
                  <p>{alert.message}</p>
                  <div className="alert-feed-location">
                    <span className="location-pin" aria-hidden="true">⌖</span>
                    <strong>{alert.portName}</strong>
                    <span>{alert.portCode}</span>
                    <i />
                    <span>{alert.zoneName}</span>
                  </div>
                </div>
                <Link aria-label="Xem chi tiết" className="alert-feed-action" to={`/alerts/${alert.alertId}`}>
                  <span>Xem chi tiết</span><b aria-hidden="true">→</b>
                </Link>
              </article>
            );
          })}
        </div>

        {totalPages > 1 ? <div className="alert-pagination" aria-label="Phân trang cảnh báo"><button aria-label="Trước" disabled={currentPage === 1} onClick={() => setCurrentPage((page) => Math.max(1, page - 1))} type="button">← Trang trước</button><span>Trang {currentPage}/{totalPages}</span><button aria-label="Sau" disabled={currentPage === totalPages} onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))} type="button">Trang sau →</button></div> : null}
      </section>
    </section>
  );
}
