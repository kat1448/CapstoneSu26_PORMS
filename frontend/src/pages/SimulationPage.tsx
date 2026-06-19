import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getSimulationSnapshot, runDemoSimulation } from "../services/simulationService";
import type { SimulationSnapshot } from "../types/simulation";

type SimulationPageProps = {
  refreshKey: number;
};

export function SimulationPage({ refreshKey }: SimulationPageProps) {
  const demoVersion = useDemoRefresh();
  const [snapshot, setSnapshot] = useState<SimulationSnapshot | null>(null);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    void getSimulationSnapshot().then(setSnapshot);
  }, [demoVersion, refreshKey]);

  if (!snapshot) {
    return <section className="page-grid"><article className="card loading-card">Đang tải mô phỏng...</article></section>;
  }

  const isRunning = running || snapshot.status === "RUNNING";
  const statusLabel = isRunning
    ? "ĐANG PHÁT DỮ LIỆU THỜI GIAN THỰC"
    : snapshot.status === "COMPLETED"
      ? "MÔ PHỎNG HOÀN TẤT"
      : "SẴN SÀNG MÔ PHỎNG";

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Chế độ mô phỏng</h2>
          <p>Replay dữ liệu bão lịch sử để huấn luyện và trình diễn</p>
        </div>
        {snapshot.status === "COMPLETED" ? (
          <Link className="button button-secondary" to="/simulation-results">
            Xem kết quả gần nhất
          </Link>
        ) : null}
      </div>
      <div className="simulation-layout">
        <article className="card simulation-hero">
          <div className="sim-status">
            <span className={`sim-dot${isRunning ? " pulse-dot" : ""}`} />
            {statusLabel}
          </div>
          <h3>Kịch bản bão Đà Nẵng 10/2023</h3>
          <p className="sim-copy">
            Dữ liệu mô phỏng quá trình gió tăng từ cấp 4 lên cấp 10, mưa lớn và tầm nhìn
            giảm. Hệ thống sẽ tự động đánh giá rủi ro, kích hoạt SOP và thay đổi chế độ vận hành.
          </p>
          <div className="progress-bar sim-progress">
            <span style={{ width: `${snapshot.progressPercent}%` }} />
          </div>
          <div className="sim-progress-label">
            <span>Bắt đầu</span>
            <span>
              {snapshot.progressPercent}% ·{" "}
              {isRunning ? "Đang chạy" : snapshot.status === "COMPLETED" ? "Hoàn tất" : "Chưa chạy"}
            </span>
            <span>Kết thúc</span>
          </div>
          <div className="sim-values">
            <div>
              <small>Gió</small>
              <strong>{snapshot.windSpeedMs.toFixed(1)} m/s</strong>
            </div>
            <div>
              <small>Beaufort</small>
              <strong>Cấp {snapshot.beaufortNumber}</strong>
            </div>
            <div>
              <small>Rủi ro</small>
              <strong>{snapshot.currentRiskLevel}</strong>
            </div>
            <div>
              <small>Chế độ</small>
              <strong>{snapshot.currentMode}</strong>
            </div>
          </div>
        </article>
        <aside className="card simulation-settings">
          <div>
            <h3>Thiết lập mô phỏng</h3>
            <p>Bộ dữ liệu replay phục vụ demo vận hành</p>
          </div>
          <label className="field-label" htmlFor="simulation-dataset">
            Bộ dữ liệu
          </label>
          <select className="input select-input" disabled={isRunning} id="simulation-dataset">
            <option>Bão Đà Nẵng tháng 10/2023</option>
            <option>Mưa lớn tháng 09/2024</option>
          </select>
          <label className="field-label" htmlFor="simulation-speed">
            Tốc độ phát lại
          </label>
          <select className="input select-input" disabled={isRunning} id="simulation-speed">
            <option>1x - Thực tế</option>
            <option>2x - Nhanh</option>
            <option>5x - Demo</option>
          </select>
          <div className="sim-dataset-card">
            <strong>120 mẫu thời tiết</strong>
            <span>Thời lượng gốc: 30 giờ</span>
            <span>Độ phân giải: 15 phút</span>
            <span>Rủi ro đỉnh: CRITICAL</span>
          </div>
          <button
            className={isRunning ? "button button-danger simulation-action" : "button button-primary simulation-action"}
            disabled={isRunning}
            onClick={async () => {
              setRunning(true);
              await runDemoSimulation();
              setSnapshot(await getSimulationSnapshot());
              setRunning(false);
            }}
            type="button"
          >
            {isRunning ? "Đang phát dữ liệu..." : snapshot.status === "COMPLETED" ? "Chạy lại" : "Bắt đầu mô phỏng"}
          </button>
        </aside>
      </div>
      <article className="card side-card">
        <div className="section-heading compact">
          <div>
            <h3>Luồng sự kiện mô phỏng</h3>
            <p>Các phản ứng tự động sẽ xuất hiện tại đây</p>
          </div>
        </div>
        <div className="timeline">
          {snapshot.feed.length === 0 ? (
            <div className="empty-state">
              <strong>Chưa có dữ liệu mô phỏng</strong>
              <span>Bấm “Bắt đầu mô phỏng” để phát dữ liệu.</span>
            </div>
          ) : (
            snapshot.feed.map((item) => (
              <div className={`timeline-item sim-feed-item risk-${item.riskLevel.toLowerCase()}`} key={`${item.title}-${item.happenedAt}`}>
                <div className="timeline-header">
                  <strong>{item.title}</strong>
                  <small>{item.happenedAt}</small>
                </div>
                <p>{item.detail}</p>
                <Badge tone={item.riskLevel === "CRITICAL" ? "danger" : item.riskLevel === "HIGH" ? "warning" : "info"}>
                  {item.riskLevel}
                </Badge>
              </div>
            ))
          )}
        </div>
      </article>
    </section>
  );
}
