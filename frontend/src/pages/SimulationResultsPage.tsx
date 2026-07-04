import { useEffect, useState } from "react";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getSimulationSnapshot } from "../services/simulationService";
import type { RiskLevel } from "../types/dashboard";
import type { SimulationSnapshot } from "../types/simulation";

type SimulationResultsPageProps = {
  refreshKey: number;
};

function riskTone(riskLevel: RiskLevel): "danger" | "info" | "success" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  if (riskLevel === "LOW") return "success";
  return "info";
}

function statusLabel(status: SimulationSnapshot["status"]) {
  if (status === "COMPLETED") return "Hoàn tất";
  if (status === "RUNNING") return "Đang chạy";
  return "Chưa chạy";
}

export function SimulationResultsPage({ refreshKey }: SimulationResultsPageProps) {
  useDemoRefresh();
  const [snapshot, setSnapshot] = useState<SimulationSnapshot | null>(null);

  useEffect(() => {
    void getSimulationSnapshot().then(setSnapshot);
  }, [refreshKey]);

  if (!snapshot) {
    return (
      <section className="page-grid simulation-results-page">
        <article className="card loading-card">Đang tải kết quả mô phỏng...</article>
      </section>
    );
  }

  const weatherRows = [
    { label: "Gió", note: `Beaufort cấp ${snapshot.beaufortNumber}`, value: `${snapshot.windSpeedMs.toFixed(1)} m/s` },
    { label: "Mưa 1 giờ", note: "Lượng mưa đưa vào mô phỏng", value: `${snapshot.rainfall1hMm.toFixed(1)} mm` },
    { label: "Tầm nhìn", note: "Khoảng cách quan sát", value: `${snapshot.visibilityKm.toFixed(1)} km` }
  ];

  return (
    <section className="page-grid simulation-results-page">
      <div className="section-heading">
        <div>
          <h2>Kết quả mô phỏng</h2>
          <p>Bảng tổng hợp lần chạy mô phỏng gần nhất, tách riêng khỏi dữ liệu vận hành thật</p>
        </div>
        <Badge tone={snapshot.status === "COMPLETED" ? "success" : snapshot.status === "RUNNING" ? "warning" : "info"}>
          {statusLabel(snapshot.status)}
        </Badge>
      </div>

      <div className="simulation-results-kpis">
        <article className="card card-pad simulation-results-kpi risk">
          <span>Rủi ro đỉnh</span>
          <strong>{snapshot.currentRiskLevel}</strong>
          <Badge tone={riskTone(snapshot.currentRiskLevel)}>{snapshot.currentRiskLevel}</Badge>
        </article>
        <article className="card card-pad simulation-results-kpi">
          <span>Chế độ cuối</span>
          <strong>{snapshot.currentMode}</strong>
          <small>{snapshot.modeChangeCount} lần đổi chế độ</small>
        </article>
        <article className="card card-pad simulation-results-kpi">
          <span>Cảnh báo phát</span>
          <strong>{snapshot.generatedAlertCount}</strong>
          <small>Tiến độ {snapshot.progressPercent}%</small>
        </article>
      </div>

      <div className="simulation-results-grid">
        <article className="card card-pad simulation-results-card">
          <div className="card-head">
            <div>
              <h3>Dữ liệu thời tiết mô phỏng</h3>
              <p>Giá trị đầu vào dùng để đánh giá rủi ro trong lần chạy gần nhất</p>
            </div>
          </div>
          <div className="simulation-results-table-wrap">
            <table aria-label="Bảng dữ liệu thời tiết mô phỏng" className="simulation-results-table compact">
              <thead>
                <tr>
                  <th>Yếu tố</th>
                  <th>Giá trị</th>
                  <th>Ghi chú</th>
                </tr>
              </thead>
              <tbody>
                {weatherRows.map((row) => (
                  <tr key={row.label}>
                    <td><strong>{row.label}</strong></td>
                    <td>{row.value}</td>
                    <td>{row.note}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>

        <article className="card card-pad simulation-results-card">
          <div className="card-head">
            <div>
              <h3>Sự kiện mô phỏng</h3>
              <p>Danh sách sự kiện, SOP và task sinh ra theo thứ tự thời gian</p>
            </div>
            <Badge tone={snapshot.feed.length ? "info" : "muted"}>{snapshot.feed.length} dòng</Badge>
          </div>
          <div className="simulation-results-table-wrap">
            <table aria-label="Bảng sự kiện mô phỏng" className="simulation-results-table">
              <thead>
                <tr>
                  <th>Thời gian</th>
                  <th>Sự kiện</th>
                  <th>Mức rủi ro</th>
                  <th>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {snapshot.feed.length === 0 ? (
                  <tr>
                    <td colSpan={4}>Chưa có sự kiện mô phỏng.</td>
                  </tr>
                ) : null}
                {snapshot.feed.map((item) => (
                  <tr key={`${item.title}-${item.happenedAt}`}>
                    <td>{item.happenedAt}</td>
                    <td><strong>{item.title}</strong></td>
                    <td><Badge tone={riskTone(item.riskLevel)}>{item.riskLevel}</Badge></td>
                    <td>{item.detail}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>
      </div>
    </section>
  );
}
