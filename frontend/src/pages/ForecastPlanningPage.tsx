import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { getPorts } from "../services/portService";
import { createForecastPlan } from "../services/simulationService";
import { getOpenWeatherForecast } from "../services/weatherService";
import type { PortSummary } from "../types/port";
import type { ForecastHorizonDays, ForecastPlan } from "../types/simulation";
import type { OpenWeatherForecast } from "../types/weather";

function riskTone(riskLevel: string): "danger" | "info" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  return "info";
}

export function ForecastPlanningPage() {
  const [ports, setPorts] = useState<PortSummary[]>([]);
  const [portCode, setPortCode] = useState("DNTSA");
  const [horizonDays, setHorizonDays] = useState<ForecastHorizonDays>(5);
  const [plan, setPlan] = useState<ForecastPlan | null>(null);
  const [forecast, setForecast] = useState<OpenWeatherForecast | null>(null);
  const [loading, setLoading] = useState(false);
  const [forecastLoading, setForecastLoading] = useState(false);
  const [error, setError] = useState("");
  const [forecastError, setForecastError] = useState("");
  const [message, setMessage] = useState("");

  useEffect(() => {
    let active = true;
    getPorts()
      .then((items) => {
        if (!active) return;
        setPorts(items);
        setPortCode((current) => current || items[0]?.portCode || "DNTSA");
      })
      .catch(() => {
        if (active) setPorts([]);
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    setForecastLoading(true);
    setForecastError("");
    getOpenWeatherForecast(portCode, 5)
      .then((nextForecast) => {
        if (active) setForecast(nextForecast);
      })
      .catch((requestError) => {
        if (active) {
          setForecast(null);
          setForecastError(requestError instanceof Error ? requestError.message : "Không tải được dự báo OpenWeather");
        }
      })
      .finally(() => {
        if (active) setForecastLoading(false);
      });

    return () => {
      active = false;
    };
  }, [portCode]);

  async function handleCreateForecastPlan() {
    setLoading(true);
    setError("");
    setMessage("");
    try {
      const nextPlan = await createForecastPlan({
        horizonDays,
        portCode: portCode.trim() || "DNTSA"
      });
      setPlan(nextPlan);
      setMessage("Đã cập nhật kế hoạch dự báo từ OpenWeather");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Không tạo được kế hoạch dự báo");
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="page-grid simulation-page">
      <div className="section-heading">
        <div>
          <span className="eyebrow">OpenWeather API</span>
          <h1>Dự báo vận hành</h1>
          <p>Dữ liệu dự đoán tương lai từ OpenWeather API phục vụ lập kế hoạch vận hành trong 5 ngày tới.</p>
        </div>
        <Link className="button button-secondary" to="/simulation">
          Dữ liệu mô phỏng nhập tay
        </Link>
      </div>

      <article className="card card-pad simulation-forecast-panel">
        <div className="card-head">
          <div>
            <h3>Kế hoạch dự báo tương lai</h3>
            <p>Nguồn dữ liệu này được sinh từ OpenWeather mới nhất trong database và có thể cập nhật lại khi thời tiết thay đổi.</p>
          </div>
          <Badge tone={plan ? riskTone(plan.items[0]?.riskLevel ?? "LOW") : "info"}>
            {plan ? `${plan.horizonDays} ngày` : "Dự báo"}
          </Badge>
        </div>

        <div className="simulation-forecast-controls">
          <label>
            <span>Cảng</span>
            <select
              aria-label="Cảng dự báo"
              className="input select-input"
              onChange={(event) => setPortCode(event.target.value)}
              value={portCode}
            >
              {ports.length ? ports.map((port) => (
                <option key={port.portId} value={port.portCode}>{port.portCode} - {port.portName}</option>
              )) : <option value="DNTSA">DNTSA</option>}
            </select>
          </label>
          <label>
            <span>Khoảng dự báo</span>
            <select
              aria-label="Khoảng dự báo"
              className="input select-input"
              onChange={(event) => setHorizonDays(Number(event.target.value) as ForecastHorizonDays)}
              value={horizonDays}
            >
              <option value={5}>5 ngày tới</option>
            </select>
          </label>
          <button className="button button-primary" disabled={loading} onClick={handleCreateForecastPlan} type="button">
            {loading ? "Đang cập nhật..." : "Cập nhật kế hoạch từ OpenWeather"}
          </button>
        </div>

        {message ? <p className="form-success">{message}</p> : null}
        {error ? <p className="form-error">{error}</p> : null}

        {plan ? (
          <>
            <div className="sim-dataset-card simulation-forecast-dataset">
              <strong>{plan.dataset.name}</strong>
              <span>{plan.dataset.description}</span>
              <small>{plan.dataset.snapshotCount} điểm dự báo · {plan.dataset.portCode}</small>
            </div>
            <div className="simulation-forecast-list">
              {plan.items.map((item) => (
                <div className="simulation-forecast-row" key={item.plannedAt}>
                  <div>
                    <strong>{new Date(item.plannedAt).toLocaleDateString("vi-VN")}</strong>
                    <small>{item.summary}</small>
                  </div>
                  <Badge tone={riskTone(item.riskLevel)}>{item.riskLevel}</Badge>
                  <span>{item.operationPlan}</span>
                </div>
              ))}
            </div>
          </>
        ) : (
          <div className="simulation-forecast-empty">
            Chưa có kế hoạch dự báo. Hãy chọn cảng và cập nhật kế hoạch 5 ngày từ dữ liệu OpenWeather mới nhất.
          </div>
        )}
      </article>

      <article className="card card-pad simulation-forecast-panel">
        <div className="card-head">
          <div>
            <h3>Bảng dự báo OpenWeather 5 ngày</h3>
            <p>Dữ liệu dự báo trực tiếp từ OpenWeather API, dùng để đối chiếu trước khi tạo kế hoạch vận hành.</p>
          </div>
          <Badge tone="info">{forecastLoading ? "Đang tải" : "5 ngày"}</Badge>
        </div>
        {forecastError ? <p className="form-error">{forecastError}</p> : null}
        {forecast ? (
          <div className="forecast-table-shell">
            <table aria-label="Dự báo OpenWeather 5 ngày" className="forecast-table">
              <thead>
                <tr>
                  <th>Ngày</th>
                  <th>Thời tiết</th>
                  <th>Nhiệt độ</th>
                  <th>Gió</th>
                  <th>Mưa</th>
                  <th>Độ ẩm</th>
                  <th>Áp suất</th>
                </tr>
              </thead>
              <tbody>
                {forecast.days.map((day) => (
                  <tr key={day.date}>
                    <td>{new Date(day.date).toLocaleDateString("vi-VN")}</td>
                    <td>
                      <strong>{day.summary || day.weatherDescription || "Chưa có mô tả"}</strong>
                      {day.weatherDescription ? <small>{day.weatherDescription}</small> : null}
                    </td>
                    <td>{day.temperatureMinC.toFixed(0)}-{day.temperatureMaxC.toFixed(0)} °C</td>
                    <td>
                      <strong>{day.windSpeedMs.toFixed(1)} m/s</strong>
                      {day.windGustMs !== null ? <small>Giật {day.windGustMs.toFixed(1)} m/s</small> : null}
                    </td>
                    <td>
                      <strong>{day.rainMm.toFixed(1)} mm</strong>
                      <small>{day.popPct}% khả năng</small>
                    </td>
                    <td>{day.humidityPct}%</td>
                    <td>{day.pressureHpa !== null ? `${day.pressureHpa.toFixed(0)} hPa` : "Chưa có"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="simulation-forecast-empty">
            {forecastLoading ? "Đang tải dự báo OpenWeather..." : "Chưa có dữ liệu dự báo OpenWeather."}
          </div>
        )}
      </article>
    </section>
  );
}
