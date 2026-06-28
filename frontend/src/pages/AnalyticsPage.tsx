import { useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import { getAlerts } from "../services/alertService";
import { getDashboardSummary, getRiskTrend, getWeatherSnapshot } from "../services/dashboardService";
import { getPorts, getPortZones } from "../services/portService";
import { getSimulationSnapshot } from "../services/simulationService";
import type { AlertItem } from "../types/alert";
import type { DashboardSummary, RiskLevel, RiskTrendPoint, WeatherSnapshot } from "../types/dashboard";
import type { PortSummary, PortZone } from "../types/port";
import type { SimulationSnapshot } from "../types/simulation";

type AnalyticsState = {
  alerts: AlertItem[];
  ports: PortSummary[];
  summary: DashboardSummary;
  trend: RiskTrendPoint[];
  weather: WeatherSnapshot;
  simulation: SimulationSnapshot | null;
  zones: PortZone[];
};

const riskScores: Record<RiskLevel, number> = {
  CRITICAL: 4,
  HIGH: 3,
  LOW: 1,
  MEDIUM: 2
};

const riskColors: Record<RiskLevel, string> = {
  CRITICAL: "#d94848",
  HIGH: "#ee7623",
  LOW: "#19a66a",
  MEDIUM: "#e9a11b"
};

const riskLabels: Record<number, RiskLevel> = {
  1: "LOW",
  2: "MEDIUM",
  3: "HIGH",
  4: "CRITICAL"
};

function riskTone(riskLevel: RiskLevel): "danger" | "info" | "success" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  if (riskLevel === "LOW") return "success";
  return "info";
}

function scoreToRisk(score: number): RiskLevel {
  if (score >= 3.5) return "CRITICAL";
  if (score >= 2.5) return "HIGH";
  if (score >= 1.5) return "MEDIUM";
  return "LOW";
}

function percent(value: number, total: number) {
  if (total === 0) return 0;
  return Math.round((value / total) * 100);
}

export function AnalyticsPage() {
  const [data, setData] = useState<AnalyticsState | null>(null);
  const [selectedPort, setSelectedPort] = useState("ALL");
  const [selectedRisk, setSelectedRisk] = useState("ALL");
  const [range, setRange] = useState("24H");

  useEffect(() => {
    let cancelled = false;

    async function loadAnalytics() {
      const [summary, weather, trend, alerts, ports, simulation] = await Promise.all([
        getDashboardSummary(),
        getWeatherSnapshot(),
        getRiskTrend(),
        getAlerts(),
        getPorts(),
        getSimulationSnapshot().catch(() => null)
      ]);
      const zoneGroups = await Promise.all(ports.map((port) => getPortZones(port.portId)));

      if (!cancelled) {
        setData({
          alerts,
          ports,
          simulation,
          summary,
          trend,
          weather,
          zones: zoneGroups.flat()
        });
      }
    }

    void loadAnalytics();

    return () => {
      cancelled = true;
    };
  }, []);

  const analytics = useMemo(() => {
    if (!data) return null;

    const scopedPorts = selectedPort === "ALL"
      ? data.ports
      : data.ports.filter((port) => port.portId === selectedPort);
    const scopedPortIds = new Set(scopedPorts.map((port) => port.portId));
    const scopedZones = data.zones.filter((zone) => scopedPortIds.has(zone.portId));
    const riskFilteredZones = selectedRisk === "ALL"
      ? scopedZones
      : scopedZones.filter((zone) => zone.currentRiskLevel === selectedRisk);
    const highRiskZones = scopedZones.filter((zone) => ["HIGH", "CRITICAL"].includes(zone.currentRiskLevel));
    const openAlerts = data.alerts.filter((alert) => !alert.read && (selectedPort === "ALL" || alert.portId === selectedPort));
    const avgScore = data.trend.length > 0
      ? data.trend.reduce((sum, point) => sum + point.riskScore, 0) / data.trend.length
      : riskScores[data.summary.currentRiskLevel];
    const portRiskRows = scopedPorts.map((port) => ({
      ...port,
      score: riskScores[port.currentRiskLevel]
    })).sort((a, b) => b.score - a.score || b.activeAlertCount - a.activeAlertCount);
    const modeCounts = scopedPorts.reduce<Record<string, number>>((acc, port) => {
      acc[port.currentOperationMode] = (acc[port.currentOperationMode] ?? 0) + 1;
      return acc;
    }, {});
    const mostRiskyPort = portRiskRows[0];
    const mostRiskyZone = [...scopedZones].sort((a, b) => riskScores[b.currentRiskLevel] - riskScores[a.currentRiskLevel])[0];

    return {
      avgRisk: scoreToRisk(avgScore),
      avgScore,
      highRiskZones,
      modeCounts,
      mostRiskyPort,
      mostRiskyZone,
      openAlerts,
      portRiskRows,
      riskFilteredZones,
      scopedPorts,
      scopedZones
    };
  }, [data, selectedPort, selectedRisk]);

  if (!data || !analytics) {
    return (
      <section className="page-grid">
        <article className="card loading-card">Đang tải dữ liệu BI...</article>
      </section>
    );
  }

  const chartWidth = 520;
  const chartHeight = 190;
  const chartPoints = data.trend.map((point, index) => {
    const x = data.trend.length === 1 ? chartWidth / 2 : 34 + index * ((chartWidth - 68) / (data.trend.length - 1));
    const y = chartHeight - 26 - ((point.riskScore - 1) / 3) * 132;
    return { ...point, x, y };
  });
  const linePath = chartPoints.map((point, index) => `${index === 0 ? "M" : "L"} ${point.x} ${point.y}`).join(" ");
  const areaPath = chartPoints.length > 0
    ? `${linePath} L ${chartPoints[chartPoints.length - 1].x} ${chartHeight - 26} L ${chartPoints[0].x} ${chartHeight - 26} Z`
    : "";
  const maxPortScore = Math.max(...analytics.portRiskRows.map((port) => port.score), 1);

  return (
    <section className="page-grid bi-page">
      <div className="section-heading bi-heading">
        <div>
          <h2>Phân tích BI</h2>
          <p>Phân tích rủi ro, hiệu suất vận hành, cảnh báo và SOP theo dữ liệu hệ thống</p>
        </div>
      </div>

      <article aria-label="Bộ lọc BI" className="card bi-filter-bar bi-card-pad">
        <label>
          <span>Khoảng thời gian</span>
          <select value={range} onChange={(event) => setRange(event.target.value)}>
            <option value="24H">24 giờ gần nhất</option>
            <option value="7D">7 ngày</option>
            <option value="30D">30 ngày</option>
          </select>
        </label>
        <label>
          <span>Cảng</span>
          <select value={selectedPort} onChange={(event) => setSelectedPort(event.target.value)}>
            <option value="ALL">Tất cả cảng</option>
            {data.ports.map((port) => (
              <option key={port.portId} value={port.portId}>{port.portName}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Mức rủi ro</span>
          <select value={selectedRisk} onChange={(event) => setSelectedRisk(event.target.value)}>
            <option value="ALL">Tất cả mức</option>
            <option value="LOW">LOW</option>
            <option value="MEDIUM">MEDIUM</option>
            <option value="HIGH">HIGH</option>
            <option value="CRITICAL">CRITICAL</option>
          </select>
        </label>
        <div className="bi-filter-status">
          <span>Cập nhật từ API hiện có</span>
          <strong>{range}</strong>
        </div>
      </article>

      <div className="bi-kpi-grid">
        <article className="card bi-kpi-card bi-card-pad">
          <span>Tổng số cảng</span>
          <strong>{analytics.scopedPorts.length}</strong>
          <small>{data.ports.filter((port) => port.isActive).length} đang hoạt động</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad risk">
          <span>Khu vuc HIGH/CRITICAL</span>
          <strong>{analytics.highRiskZones.length}</strong>
          <small>{percent(analytics.highRiskZones.length, analytics.scopedZones.length)}% tổng khu vực</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad">
          <span>Cảnh báo đang mở</span>
          <strong>{analytics.openAlerts.length}</strong>
          <small>{data.summary.activeAlertCount} theo dashboard</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad">
          <span>SOP / Task sinh ra</span>
          <strong>{data.simulation?.generatedAlertCount ?? 0}</strong>
          <small>{data.simulation?.modeChangeCount ?? 0} lần đổi chế độ</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad">
          <span>Rủi ro trung bình</span>
          <strong>{analytics.avgRisk}</strong>
          <small>Score {analytics.avgScore.toFixed(1)}</small>
        </article>
      </div>

      <div className="bi-main-grid">
        <article className="card card-pad bi-card-pad bi-chart-card">
          <div className="card-head">
            <div>
              <h3>Xu hướng rủi ro theo thời gian</h3>
              <p>Dữ liệu từ /api/risk/trend</p>
            </div>
            <Badge tone={riskTone(data.summary.currentRiskLevel)}>{data.summary.currentRiskLevel}</Badge>
          </div>
          <svg className="bi-risk-trend" data-testid="bi-risk-trend-chart" viewBox={`0 0 ${chartWidth} ${chartHeight}`} preserveAspectRatio="none">
            <defs>
              <linearGradient id="biRiskArea" x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stopColor="#2f6fab" stopOpacity="0.22" />
                <stop offset="100%" stopColor="#2f6fab" stopOpacity="0" />
              </linearGradient>
            </defs>
            {[1, 2, 3, 4].map((score) => {
              const y = chartHeight - 26 - ((score - 1) / 3) * 132;
              return (
                <g className="bi-chart-grid" key={score}>
                  <line x1="34" x2={chartWidth - 24} y1={y} y2={y} />
                  <text x="6" y={y - 4}>{riskLabels[score]}</text>
                </g>
              );
            })}
            {areaPath ? <path className="bi-trend-area" d={areaPath} /> : null}
            {linePath ? <path className="bi-trend-line" d={linePath} /> : null}
            {chartPoints.map((point) => (
              <g className="bi-trend-point" key={`${point.hourLabel}-${point.riskScore}`}>
                <circle cx={point.x} cy={point.y} r="5" style={{ fill: riskColors[riskLabels[point.riskScore]] }} />
                <text x={point.x} y="180">{point.hourLabel}</text>
              </g>
            ))}
          </svg>
        </article>

        <article className="card card-pad bi-card-pad bi-distribution-card">
          <div className="card-head">
            <div>
              <h3>Phân bố rủi ro theo cảng</h3>
              <p>Xếp hạng theo mức rủi ro và cảnh báo đang mở</p>
            </div>
          </div>
          <div className="bi-port-bars">
            {analytics.portRiskRows.map((port) => (
              <div className="bi-port-bar" key={port.portId}>
                <div>
                  <strong>{port.portName}</strong>
                  <span>{port.portCode} · {port.activeAlertCount} cảnh báo</span>
                </div>
                <div className="bi-bar-track">
                  <span style={{ background: riskColors[port.currentRiskLevel], width: `${Math.max((port.score / maxPortScore) * 100, 12)}%` }} />
                </div>
                <Badge tone={riskTone(port.currentRiskLevel)}>{port.currentRiskLevel}</Badge>
              </div>
            ))}
          </div>
        </article>
      </div>

      <div className="bi-ops-grid">
        <article className="card card-pad bi-card-pad">
          <div className="card-head">
            <div>
              <h3>Hiệu suất vận hành</h3>
              <p>Tỷ lệ chế độ và điều kiện thời tiết đang chi phối</p>
            </div>
          </div>
          <div className="bi-mode-grid">
            {["NORMAL", "LIMITED", "STOP"].map((mode) => (
              <div className="bi-mode-cell" key={mode}>
                <span>{mode}</span>
                <strong>{analytics.modeCounts[mode] ?? 0}</strong>
              </div>
            ))}
          </div>
          <div className="bi-weather-strip">
            <span>Gió {data.weather.windSpeedMs.toFixed(1)} m/s</span>
            <span>Mưa {data.weather.rainfall1hMm.toFixed(1)} mm/h</span>
            <span>Tầm nhìn {data.weather.visibilityKm.toFixed(1)} km</span>
          </div>
        </article>

        <article className="card card-pad bi-card-pad bi-insight-card">
          <div className="card-head">
            <div>
              <h3>Insight nghiệp vụ</h3>
              <p>Gợi ý điều hành từ dữ liệu cảng, khu vực và mô phỏng</p>
            </div>
          </div>
          <ul className="bi-insight-list">
            <li>
              <strong>{analytics.mostRiskyPort?.portName ?? "Chưa có cảng"}</strong>
              <span>đang có mức rủi ro cao nhất trong phạm vi lọc.</span>
            </li>
            <li>
              <strong>{analytics.mostRiskyZone?.zoneName ?? "Chưa có khu vực"}</strong>
              <span>là khu vực cần theo dõi ưu tiên.</span>
            </li>
            <li>
              <strong>{data.simulation?.currentRiskLevel ?? data.summary.currentRiskLevel}</strong>
              <span>là kết quả rủi ro mới nhất từ mô phỏng/vận hành.</span>
            </li>
          </ul>
        </article>
      </div>

      <article className="card bi-card-pad bi-table-card">
        <div className="card-head bi-table-head">
          <div>
            <h3>Khu vực cần chú ý</h3>
            <p>Bảng drill-down theo cảng và khu vực</p>
          </div>
          <Badge tone="info">{analytics.riskFilteredZones.length} dòng</Badge>
        </div>
        <div className="bi-table-wrap">
          <table className="bi-table">
            <thead>
              <tr>
                <th>Khu vực</th>
                <th>Cảng</th>
                <th>Loại</th>
                <th>Rủi ro</th>
                <th>Trạng thái</th>
                <th>Hạn chế</th>
              </tr>
            </thead>
            <tbody>
              {analytics.riskFilteredZones.map((zone) => {
                const port = data.ports.find((item) => item.portId === zone.portId);
                return (
                  <tr key={zone.zoneId}>
                    <td><strong>{zone.zoneName}</strong></td>
                    <td>{port?.portName ?? zone.portId}</td>
                    <td>{zone.zoneType}</td>
                    <td><Badge tone={riskTone(zone.currentRiskLevel)}>{zone.currentRiskLevel}</Badge></td>
                    <td>{zone.statusLabel}</td>
                    <td>{zone.isRestricted ? "Có" : "Không"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </article>
    </section>
  );
}
