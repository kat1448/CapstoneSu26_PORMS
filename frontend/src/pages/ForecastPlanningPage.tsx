import { useCallback, useEffect, useState } from "react";
import { LineChart } from "@mui/x-charts";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { getPorts } from "../services/portService";
import { createForecastPlan } from "../services/simulationService";
import { getOpenWeatherForecast } from "../services/weatherService";
import { analyzeForecastRisk } from "../services/mlService";
import { getForecastEvaluation } from "../services/forecastEvaluationService";
import type { PortSummary } from "../types/port";
import type { ForecastHorizonDays, ForecastPlan } from "../types/simulation";
import type { OpenWeatherForecast } from "../types/weather";
import type { ForecastRiskAnalysis } from "../types/ml";
import type { ForecastEvaluationSummary } from "../types/forecastEvaluation";
import { clusterLabel, forecastTextLabel, operationModeLabel, riskLabel, weatherDescriptionLabel } from "../utils/displayLabels";

const FORECAST_AUTO_REFRESH_MS = 24 * 60 * 60 * 1000;
const ANALYSIS_STEP_DELAY_MS = 180;

type ForecastProcessingStep = "forecast" | "gemini" | "kmeans" | "pca" | null;

const forecastProcessingSteps: Array<{
  description: string;
  key: Exclude<ForecastProcessingStep, null>;
  label: string;
  progress: number;
}> = [
  {
    description: "Lấy dự báo thời tiết và tạo kế hoạch 5 ngày",
    key: "forecast",
    label: "Dữ liệu dự báo",
    progress: 25
  },
  {
    description: "Chuẩn hóa chỉ số thời tiết thành vector phân tích",
    key: "pca",
    label: "Chuẩn hóa dữ liệu",
    progress: 52
  },
  {
    description: "Phân cụm trạng thái thời tiết và rủi ro vận hành",
    key: "kmeans",
    label: "Nhóm tình huống",
    progress: 74
  },
  {
    description: "Tạo khuyến nghị vận hành dễ hiểu",
    key: "gemini",
    label: "Đề xuất phương án",
    progress: 94
  }
];

function riskTone(riskLevel: string): "danger" | "info" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  return "info";
}

function operationModeTone(mode: string): "danger" | "success" | "warning" {
  if (mode === "STOP") return "danger";
  if (mode === "LIMITED") return "warning";
  return "success";
}

function formatDateTime(value: Date) {
  return value.toLocaleString("vi-VN");
}

const riskScores = {
  CRITICAL: 4,
  HIGH: 3,
  LOW: 1,
  MEDIUM: 2
};

const riskLabels = ["", "Thấp", "Cần lưu ý", "Cao", "Rất cao"];

function operationRecommendation(riskLevel: string) {
  if (riskLevel === "CRITICAL") return "STOP";
  if (riskLevel === "HIGH") return "LIMITED";
  return "NORMAL";
}

function sameDate(left: string, right: string) {
  return new Date(left).toISOString().slice(0, 10) === new Date(right).toISOString().slice(0, 10);
}

function aiRiskLevelFromScore(score: number) {
  if (score >= 75) return "CRITICAL";
  if (score >= 50) return "HIGH";
  if (score >= 25) return "MEDIUM";
  return "LOW";
}

function delay(ms: number) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

function processingStepIndex(step: ForecastProcessingStep) {
  return Math.max(0, forecastProcessingSteps.findIndex((item) => item.key === step));
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
  const [mlAnalysis, setMlAnalysis] = useState<ForecastRiskAnalysis | null>(null);
  const [mlError, setMlError] = useState("");
  const [lastForecastUpdatedAt, setLastForecastUpdatedAt] = useState<Date | null>(null);
  const [message, setMessage] = useState("");
  const [nextForecastRefreshAt, setNextForecastRefreshAt] = useState<Date | null>(null);
  const [processingStep, setProcessingStep] = useState<ForecastProcessingStep>(null);
  const [evaluationSummary, setEvaluationSummary] = useState<ForecastEvaluationSummary | null>(null);

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

  const reloadForecast = useCallback(async () => {
    setForecastLoading(true);
    setForecastError("");
    try {
      const nextForecast = await getOpenWeatherForecast(portCode, 5);
      const now = new Date();
      setForecast(nextForecast);
      setLastForecastUpdatedAt(now);
      setNextForecastRefreshAt(new Date(now.getTime() + FORECAST_AUTO_REFRESH_MS));
    } catch (requestError) {
      setForecast(null);
      setForecastError(requestError instanceof Error ? requestError.message : "Không tải được dự báo thời tiết");
    } finally {
      setForecastLoading(false);
    }
  }, [portCode]);

  useEffect(() => {
    void reloadForecast();
  }, [reloadForecast]);

  useEffect(() => {
    let active = true;
    const to = new Date();
    const from = new Date(to);
    from.setDate(from.getDate() - 30);

    getForecastEvaluation({
      from: from.toISOString(),
      portCode,
      to: to.toISOString()
    })
      .then((response) => {
        if (active) setEvaluationSummary(response.summary);
      })
      .catch(() => {
        if (active) setEvaluationSummary(null);
      });

    return () => {
      active = false;
    };
  }, [portCode]);

  useEffect(() => {
    const timer = window.setInterval(() => void reloadForecast(), FORECAST_AUTO_REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [reloadForecast]);

  async function handleCreateForecastPlan() {
    setLoading(true);
    setProcessingStep("forecast");
    setError("");
    setMlError("");
    setMessage("");
    try {
      const nextPlan = await createForecastPlan({
        horizonDays,
        portCode: portCode.trim() || "DNTSA"
      });
      setPlan(nextPlan);
      const analysisInput = {
        portCode: nextPlan.dataset.portCode,
        items: nextPlan.items.map((item) => {
          const forecastDay = forecast?.days.find((day) => sameDate(day.date, item.plannedAt));
          return {
            humidityPct: forecastDay?.humidityPct ?? null,
            plannedAt: item.plannedAt,
            pressureHpa: forecastDay?.pressureHpa ?? null,
            rainRiskLevel: item.rainRiskLevel,
            rainfallMm: forecastDay?.rainMm ?? null,
            ruleRiskLevel: item.riskLevel,
            temperatureC: forecastDay?.temperatureDayC ?? null,
            visibilityKm: forecastDay?.visibilityKm ?? null,
            visibilityRiskLevel: item.visibilityRiskLevel,
            windRiskLevel: item.windRiskLevel,
            windSpeedMs: forecastDay?.windSpeedMs ?? null
          };
        })
      };
      setProcessingStep("pca");
      await delay(ANALYSIS_STEP_DELAY_MS);
      setProcessingStep("kmeans");
      await delay(ANALYSIS_STEP_DELAY_MS);
      setProcessingStep("gemini");
      setMlAnalysis(await analyzeForecastRisk(analysisInput));
      setMessage("Đã cập nhật kế hoạch vận hành");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Không tạo được kế hoạch dự báo");
      setMlAnalysis(null);
      setMlError(requestError instanceof Error ? requestError.message : "Không phân tích được dữ liệu ML");
    } finally {
      setLoading(false);
      setProcessingStep(null);
    }
  }

  const planChartData = plan?.items.map((item) => ({
    date: new Date(item.plannedAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" }),
    riskScore: riskScores[item.riskLevel] ?? 1
  })) ?? [];
  const mlChartData = mlAnalysis?.items.map((item) => ({
    date: new Date(item.plannedAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" }),
    score: item.pcaRiskScore
  })) ?? [];
  const currentProcessingIndex = processingStepIndex(processingStep);
  const currentProcessingStep = forecastProcessingSteps[currentProcessingIndex] ?? forecastProcessingSteps[0];
  const processingProgress = processingStep ? currentProcessingStep.progress : 0;

  return (
    <section className="page-grid simulation-page">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Dự báo 5 ngày</span>
          <h1>Dự báo vận hành</h1>
          <p>Thông tin thời tiết sắp tới giúp bạn chuẩn bị nhân sự, thiết bị và phương án vận hành tại cảng.</p>
        </div>
        <Link className="button button-secondary" to="/simulation">
          Mô phỏng tình huống
        </Link>
      </div>

      <article className="card card-pad simulation-forecast-panel">
        <div className="card-head">
          <div>
            <h3>Kế hoạch vận hành 5 ngày tới</h3>
            <p>Dữ liệu được cập nhật từ nguồn thời tiết trực tuyến và có thể làm mới khi điều kiện thời tiết thay đổi.</p>
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
            {loading ? "Đang phân tích..." : "Cập nhật kế hoạch"}
          </button>
        </div>

        {evaluationSummary ? (
          <div
            aria-label="Độ tin cậy lịch sử của dự báo"
            className={`forecast-plan-confidence-strip ${evaluationSummary.interventionRequired ? "requires-intervention" : ""}`}
          >
            <div>
              <span>Độ tin cậy 30 ngày</span>
              <strong>{evaluationSummary.confidencePct === null ? "Chưa đủ dữ liệu" : `${evaluationSummary.confidencePct.toFixed(1)}%`}</strong>
              <small>Dựa trên {evaluationSummary.matchedActualPoints} mốc dự báo đã đối chiếu dữ liệu thật.</small>
            </div>
            <p>{evaluationSummary.interventionMessage}</p>
            <Link className="button button-secondary button-small" to="/forecast-evaluation">Xem kiểm chứng</Link>
          </div>
        ) : null}

        {message ? <p className="form-success">{message}</p> : null}
        {error ? <p className="form-error">{error}</p> : null}
        {loading && processingStep ? (
          <div aria-label="Tiến trình phân tích dự báo" className="forecast-analysis-loader">
            <div className="forecast-analysis-loader-head">
              <div>
                <strong>{currentProcessingStep.label}</strong>
                <span>{currentProcessingStep.description}</span>
              </div>
              <b>{processingProgress}%</b>
            </div>
            <div className="forecast-analysis-track">
              <span style={{ width: `${processingProgress}%` }} />
            </div>
            <div className="forecast-analysis-steps">
              {forecastProcessingSteps.map((step, index) => (
                <div
                  className={[
                    "forecast-analysis-step",
                    index < currentProcessingIndex ? "is-done" : "",
                    index === currentProcessingIndex ? "is-active" : ""
                  ].filter(Boolean).join(" ")}
                  key={step.key}
                >
                  <i />
                  <span>{step.label}</span>
                </div>
              ))}
            </div>
          </div>
        ) : null}

        {plan ? (
          <>
            <div className="sim-dataset-card simulation-forecast-dataset">
              <strong>{plan.dataset.name}</strong>
              <span>{plan.dataset.description}</span>
            <small>{plan.dataset.snapshotCount} thời điểm dự báo · {plan.dataset.portCode}</small>
            </div>
            <div className="forecast-plan-visual-grid">
              <div aria-label="Timeline dự báo vận hành 5 ngày" className="forecast-plan-timeline">
                {plan.items.map((item) => (
                  <div className={`forecast-plan-timeline-item risk-${item.riskLevel.toLowerCase()}`} key={item.plannedAt}>
                    <div className="forecast-plan-time-marker">
                      <span />
                    </div>
                    <div className="forecast-plan-timeline-content">
                      <div className="forecast-plan-timeline-head">
                        <strong>{new Date(item.plannedAt).toLocaleDateString("vi-VN")}</strong>
                        <Badge tone={riskTone(item.riskLevel)}>{riskLabel(item.riskLevel)}</Badge>
                      </div>
                      <p>{item.summary}</p>
                      <div className="forecast-plan-metrics">
                        <small>Gió: {riskLabel(item.windRiskLevel)}</small>
                        <small>Mưa: {riskLabel(item.rainRiskLevel)}</small>
                        <small>Tầm nhìn: {riskLabel(item.visibilityRiskLevel)}</small>
                      </div>
                      <div className="forecast-plan-operation">
                        <span>Khuyến nghị vận hành</span>
                        <strong>{operationModeLabel(operationRecommendation(item.riskLevel))}</strong>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
              <div aria-label="Biểu đồ rủi ro dự báo 5 ngày" className="forecast-plan-chart">
                <div className="card-head">
                  <div>
                    <h3>Biểu đồ rủi ro 5 ngày</h3>
                    <p>Đường xu hướng đánh giá rủi ro vận hành từ dữ liệu dự báo.</p>
                  </div>
                </div>
                <LineChart
                  height={280}
                  margin={{ bottom: 36, left: 48, right: 20, top: 20 }}
                  series={[{
                    area: true,
                    color: "#0f766e",
                    curve: "linear",
                    data: planChartData.map((item) => item.riskScore),
                    label: "Mức rủi ro"
                  }]}
                  xAxis={[{
                    data: planChartData.map((item) => item.date),
                    scaleType: "point"
                  }]}
                  yAxis={[{
                    max: 4,
                    min: 1,
                    tickMinStep: 1,
                    valueFormatter: (value: number) => riskLabels[Number(value)] ?? ""
                  }]}
                />
              </div>
            </div>
            {mlError ? <p className="form-error">{mlError}</p> : null}
            {mlAnalysis ? (
              <article aria-label="Phân tích mức rủi ro dự báo 5 ngày" className="forecast-ml-card">
                <div className="card-head">
                  <div>
                    <h3>Phân tích mức rủi ro</h3>
                    <p>Đối chiếu điều kiện thời tiết với điểm rủi ro để đưa ra khuyến nghị vận hành.</p>
                  </div>
                  <Badge tone="info">Phân tích tự động</Badge>
                </div>
                <div aria-label="Biểu đồ điểm rủi ro 5 ngày" className="forecast-ml-chart">
                  <div className="forecast-ml-chart-body">
                    <div className="forecast-ml-line">
                      <LineChart
                        height={240}
                        margin={{ bottom: 36, left: 48, right: 20, top: 18 }}
                        series={[{
                          color: "#2563eb",
                          curve: "linear",
                          data: mlChartData.map((item) => item.score),
                          showMark: true,
                          label: "Điểm rủi ro"
                        }]}
                        xAxis={[{
                          data: mlChartData.map((item) => item.date),
                          scaleType: "point"
                        }]}
                        yAxis={[{
                          max: 100,
                          min: 0,
                          tickMinStep: 20
                        }]}
                      />
                    </div>
                    <div className="forecast-ml-legend" aria-label="Chú thích mức điểm rủi ro">
                      <span><i className="score-low" />0-24 · Thấp</span>
                      <span><i className="score-medium" />25-49 · Cần lưu ý</span>
                      <span><i className="score-high" />50-74 · Cao</span>
                      <span><i className="score-critical" />75-100 · Rất cao</span>
                    </div>
                  </div>
                </div>
                <div className="forecast-ml-grid">
                  {mlAnalysis.items.map((item) => {
                    const aiRiskLevel = aiRiskLevelFromScore(item.pcaRiskScore);
                    return (
                      <div className={`forecast-ml-item risk-${aiRiskLevel.toLowerCase()}`} key={item.plannedAt}>
                        <div>
                          <strong>{new Date(item.plannedAt).toLocaleDateString("vi-VN")}</strong>
                          <span>{clusterLabel(item.clusterLabel)}</span>
                        </div>
                        <Badge tone={riskTone(aiRiskLevel)}>Mức {riskLabel(aiRiskLevel)}</Badge>
                        <div className="forecast-ml-score">
                          <span>Điểm phân tích {item.pcaRiskScore}</span>
                          <strong>{operationModeLabel(item.mlRecommendation)}</strong>
                        </div>
                        <small>Theo quy tắc: {riskLabel(item.ruleRiskLevel)}</small>
                        <small>{item.dominantFactors.join(" / ")}</small>
                      </div>
                    );
                  })}
                </div>
                {mlAnalysis.llmPlanAnalysis ? (
                  <section aria-label="Phân tích kế hoạch vận hành bằng LLM" className="forecast-llm-plan">
                    <div className="forecast-llm-head">
                      <div>
                        <h3>Giải thích và phương án đề xuất</h3>
                        <p>{forecastTextLabel(mlAnalysis.llmPlanAnalysis.summary)}</p>
                      </div>
                      <Badge tone={mlAnalysis.llmPlanAnalysis.isConfigured ? "success" : "muted"}>
                        Phân tích tự động
                      </Badge>
                    </div>
                    <div className="forecast-llm-grid">
                      {mlAnalysis.llmPlanAnalysis.items.map((item) => (
                        <div className={`forecast-llm-item mode-${item.operationMode.toLowerCase()}`} key={`${item.plannedAt}-${item.operationMode}`}>
                          <div className="forecast-llm-item-head">
                            <strong>{new Date(item.plannedAt).toLocaleDateString("vi-VN")}</strong>
                            <Badge tone={operationModeTone(item.operationMode)}>{operationModeLabel(item.operationMode)}</Badge>
                          </div>
                          <p>{forecastTextLabel(item.planChange)}</p>
                          <small>{forecastTextLabel(item.reason)}</small>
                          <div className="forecast-llm-actions">
                            <span>Việc nên làm</span>
                            {item.recommendedActions.map((action) => (
                              <strong key={action}>{forecastTextLabel(action)}</strong>
                            ))}
                          </div>
                          <div className="forecast-llm-affected">
                            {item.affectedOperations.map((operation) => (
                              <span key={operation}>{forecastTextLabel(operation)}</span>
                            ))}
                          </div>
                        </div>
                      ))}
                    </div>
                  </section>
                ) : null}
              </article>
            ) : null}
          </>
        ) : (
          <div className="simulation-forecast-empty">
            Chưa có kế hoạch dự báo. Hãy chọn cảng và bấm “Cập nhật kế hoạch”.
          </div>
        )}
      </article>

      <article className="card card-pad simulation-forecast-panel">
        <div className="card-head">
          <div>
            <h3>Dự báo thời tiết 5 ngày tới</h3>
            <p>Dữ liệu thời tiết trực tuyến dùng để đối chiếu trước khi tạo kế hoạch vận hành.</p>
            <p>
              {lastForecastUpdatedAt
                ? `Cập nhật lúc ${formatDateTime(lastForecastUpdatedAt)}. Tự động cập nhật hằng ngày${nextForecastRefreshAt ? `, lần kế tiếp ${formatDateTime(nextForecastRefreshAt)}` : ""}.`
                : "Bảng sẽ tải dự báo khi mở trang và tự động cập nhật hằng ngày."}
            </p>
          </div>
          <div className="card-head-actions">
            <button className="button button-secondary button-small" disabled={forecastLoading} onClick={() => void reloadForecast()} type="button">
              {forecastLoading ? "Đang tải..." : "Làm mới dự báo"}
            </button>
            <Badge tone="info">{forecastLoading ? "Đang tải" : "5 ngày"}</Badge>
          </div>
        </div>
        {forecastError ? <p className="form-error">{forecastError}</p> : null}
        {forecast ? (
          <div className="forecast-table-shell">
            <table aria-label="Dự báo thời tiết 5 ngày" className="forecast-table">
              <thead>
                <tr>
                  <th>Ngày</th>
                  <th>Thời tiết</th>
                  <th>Nhiệt độ</th>
                  <th>Gió</th>
                  <th>Mưa</th>
                  <th>Tầm nhìn</th>
                  <th>Độ ẩm</th>
                  <th>Áp suất</th>
                </tr>
              </thead>
              <tbody>
                {forecast.days.map((day) => (
                  <tr key={day.date}>
                    <td>{new Date(day.date).toLocaleDateString("vi-VN")}</td>
                    <td>
                      <strong>{weatherDescriptionLabel(day.summary || day.weatherDescription)}</strong>
                      {day.weatherDescription ? <small>{weatherDescriptionLabel(day.weatherDescription)}</small> : null}
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
                    <td>{day.visibilityKm !== null ? `${day.visibilityKm.toFixed(1)} km` : "Chưa có"}</td>
                    <td>{day.humidityPct}%</td>
                    <td>{day.pressureHpa !== null ? `${day.pressureHpa.toFixed(0)} hPa` : "Chưa có"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="simulation-forecast-empty">
            {forecastLoading ? "Đang tải dự báo thời tiết..." : "Chưa có dữ liệu dự báo thời tiết."}
          </div>
        )}
      </article>
    </section>
  );
}
