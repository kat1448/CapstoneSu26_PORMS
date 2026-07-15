import { useEffect, useMemo, useState } from "react";
import { Badge } from "../components/common/Badge";
import { exportForecastEvaluation, getForecastEvaluation } from "../services/forecastEvaluationService";
import { getPorts } from "../services/portService";
import type { ForecastEvaluationResponse } from "../types/forecastEvaluation";
import type { PortSummary } from "../types/port";

const PAGE_SIZE = 50;

function today(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

function toIsoDate(value: string, endOfDay = false) {
  if (!value) return undefined;
  return `${value}T${endOfDay ? "23:59:59" : "00:00:00"}Z`;
}

function formatNumber(value: number | null | undefined, suffix = "") {
  return value === null || value === undefined ? "Chưa có" : `${value.toFixed(2)}${suffix}`;
}

function formatDateTime(value: string | null) {
  if (!value) return "Chưa có dữ liệu thật";
  return new Date(value).toLocaleString("vi-VN");
}

function riskTone(riskLevel: string | null): "danger" | "info" | "muted" | "success" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  if (riskLevel === "LOW") return "success";
  if (riskLevel === "MEDIUM") return "info";
  return "muted";
}

export function ForecastEvaluationPage() {
  const [ports, setPorts] = useState<PortSummary[]>([]);
  const [data, setData] = useState<ForecastEvaluationResponse | null>(null);
  const [portCode, setPortCode] = useState("ALL");
  const [fromDate, setFromDate] = useState(today(-7));
  const [toDate, setToDate] = useState(today(7));
  const [currentPage, setCurrentPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const filters = useMemo(() => ({
    from: toIsoDate(fromDate),
    portCode,
    to: toIsoDate(toDate, true)
  }), [fromDate, portCode, toDate]);

  const rows = data?.rows ?? [];
  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const visibleRows = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return rows.slice(start, start + PAGE_SIZE);
  }, [currentPage, rows]);

  useEffect(() => {
    let active = true;
    getPorts()
      .then((items) => {
        if (active) setPorts(items);
      })
      .catch(() => {
        if (active) setPorts([]);
      });

    return () => {
      active = false;
    };
  }, []);

  async function loadEvaluation() {
    setLoading(true);
    setError("");
    setMessage("");
    try {
      setData(await getForecastEvaluation(filters));
    } catch (requestError) {
      setData(null);
      setError(requestError instanceof Error ? requestError.message : "Không tải được dữ liệu thống kê và đánh giá");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadEvaluation();
  }, [filters]);

  useEffect(() => {
    setCurrentPage(1);
  }, [filters]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  async function handleExport() {
    setExporting(true);
    setError("");
    setMessage("");
    try {
      await exportForecastEvaluation(filters);
      setMessage("Đã xuất báo cáo thống kê và đánh giá");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Không xuất được báo cáo");
    } finally {
      setExporting(false);
    }
  }

  return (
    <section className="page-grid forecast-evaluation-page">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Forecast evaluation</span>
          <h1>Thống kê và đánh giá</h1>
          <p>So sánh dữ liệu dự đoán 5 ngày với dữ liệu thời tiết thật để đo sai số và điều chỉnh mô hình.</p>
        </div>
        <button className="button button-primary" disabled={exporting || !rows.length} onClick={handleExport} type="button">
          {exporting ? "Đang xuất..." : "Xuất báo cáo CSV"}
        </button>
      </div>

      <article aria-label="Bộ lọc thống kê và đánh giá" className="card bi-filter-bar bi-card-pad">
        <label>
          <span>Cảng</span>
          <select value={portCode} onChange={(event) => setPortCode(event.target.value)}>
            <option value="ALL">Tất cả cảng</option>
            {ports.map((port) => (
              <option key={port.portId} value={port.portCode}>{port.portCode} - {port.portName}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Từ ngày</span>
          <input className="input" type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        </label>
        <label>
          <span>Đến ngày</span>
          <input className="input" type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        </label>
        <button className="button button-secondary" disabled={loading} onClick={() => void loadEvaluation()} type="button">
          {loading ? "Đang tải..." : "Lọc dữ liệu"}
        </button>
      </article>

      {message ? <p className="form-success">{message}</p> : null}
      {error ? <p className="form-error">{error}</p> : null}

      <div className="bi-kpi-grid">
        <article className="card bi-kpi-card bi-card-pad">
          <span>Điểm dự báo</span>
          <strong>{data?.summary.totalForecastPoints ?? 0}</strong>
          <small>{data?.summary.matchedActualPoints ?? 0} điểm đã có dữ liệu thật</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad">
          <span>Tỷ lệ đối chiếu</span>
          <strong>{data?.summary.matchRatePct.toFixed(1) ?? "0.0"}%</strong>
          <small>Dự báo đã khớp với dữ liệu thời tiết thật</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad risk">
          <span>MAE gió</span>
          <strong>{formatNumber(data?.summary.avgWindMae, " m/s")}</strong>
          <small>Sai số tuyệt đối trung bình</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad">
          <span>MAE mưa</span>
          <strong>{formatNumber(data?.summary.avgRainMae, " mm")}</strong>
          <small>Sai số lượng mưa 1h</small>
        </article>
        <article className="card bi-kpi-card bi-card-pad">
          <span>MAE tầm nhìn</span>
          <strong>{formatNumber(data?.summary.avgVisibilityMae, " km")}</strong>
          <small>Sai số tầm nhìn</small>
        </article>
      </div>

      <article className="card bi-card-pad bi-table-card">
        <div className="card-head bi-table-head">
          <div>
            <h3>Bảng đối chiếu dự báo và dữ liệu thật</h3>
            <p>Mỗi dòng là một mốc dự báo, ghép với dữ liệu OpenWeather thật gần thời điểm đó nhất.</p>
          </div>
          <Badge tone="info">{rows.length} dòng</Badge>
        </div>
        <div className="bi-table-wrap">
          <table className="bi-table forecast-evaluation-table">
            <thead>
              <tr>
                <th>Thời gian dự báo</th>
                <th>Cảng</th>
                <th>Gió dự báo / thật</th>
                <th>Mưa dự báo / thật</th>
                <th>Tầm nhìn dự báo / thật</th>
                <th>Rủi ro</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((row, index) => (
                <tr key={`${row.datasetName}-${row.portCode}-${row.snapshotNumber}-${row.plannedAt}-${currentPage}-${index}`}>
                  <td>
                    <strong>{new Date(row.plannedAt).toLocaleString("vi-VN")}</strong>
                    <small>{row.datasetName}</small>
                    <small>Thực tế: {formatDateTime(row.actualObservedAt)}</small>
                  </td>
                  <td>
                    <strong>{row.portCode}</strong>
                    <small>{row.portName}</small>
                  </td>
                  <td>
                    <strong>{row.forecastWindSpeedMs.toFixed(1)} / {formatNumber(row.actualWindSpeedMs, " m/s")}</strong>
                    <small>Sai số {formatNumber(row.windAbsError, " m/s")}</small>
                  </td>
                  <td>
                    <strong>{row.forecastRainfallMm.toFixed(1)} / {formatNumber(row.actualRainfallMm, " mm")}</strong>
                    <small>Sai số {formatNumber(row.rainAbsError, " mm")}</small>
                  </td>
                  <td>
                    <strong>{formatNumber(row.forecastVisibilityKm, " km")} / {formatNumber(row.actualVisibilityKm, " km")}</strong>
                    <small>Sai số {formatNumber(row.visibilityAbsError, " km")}</small>
                  </td>
                  <td>
                    <Badge tone={riskTone(row.forecastRiskLevel)}>{row.forecastRiskLevel}</Badge>
                    <small>Thực tế: {row.actualRiskLevel ?? "Chưa có"}</small>
                    <small>Lệch mức: {row.riskScoreError ?? "N/A"}</small>
                  </td>
                  <td>{row.status === "MATCHED" ? "Đã đối chiếu" : "Chờ dữ liệu thật"}</td>
                </tr>
              ))}
              {!loading && rows.length === 0 ? (
                <tr>
                  <td colSpan={7}>Chưa có dữ liệu dự báo phù hợp với bộ lọc.</td>
                </tr>
              ) : null}
              {loading ? (
                <tr>
                  <td colSpan={7}>Đang tải dữ liệu thống kê...</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
        {!loading && totalPages > 1 ? (
          <div className="table-pagination" aria-label="Phân trang thống kê và đánh giá">
            <button
              className="button button-secondary button-small"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
              type="button"
            >
              Trước
            </button>
            <span>Trang {currentPage}/{totalPages} - 50 dòng/trang</span>
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
      </article>
    </section>
  );
}
