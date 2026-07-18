import { useCallback, useEffect, useMemo, useState } from "react";
import { LineChart } from "@mui/x-charts";
import { Badge } from "../components/common/Badge";
import { analyzeForecastRisk } from "../services/mlService";
import { getPorts } from "../services/portService";
import { getOpenWeatherForecast } from "../services/weatherService";
import type { RiskLevel } from "../types/dashboard";
import type { ForecastRiskAnalysis, ForecastRiskAnalysisInputItem } from "../types/ml";
import type { PortSummary } from "../types/port";
import type { OpenWeatherForecast, OpenWeatherForecastDay } from "../types/weather";

type HorizonOption = {
  confidence: string;
  days: number;
  label: string;
  note: string;
};

const horizonOptions: HorizonOption[] = [
  { confidence: "Cao", days: 7, label: "7 ngày", note: "Gần dữ liệu OpenWeather, phù hợp điều chỉnh kế hoạch tuần." },
  { confidence: "Khá", days: 14, label: "14 ngày", note: "Dùng để chuẩn bị nhân sự và thiết bị sớm." },
  { confidence: "Trung bình", days: 30, label: "30 ngày", note: "Dùng để xem xu hướng rủi ro theo tháng." },
  { confidence: "Tham khảo", days: 60, label: "2 tháng", note: "Dùng để lập kế hoạch năng lực, không thay thế dự báo thời tiết." },
  { confidence: "Tham khảo", days: 90, label: "3 tháng", note: "Dùng để nhìn xu hướng dài hạn và rủi ro mùa vụ." }
];

function riskTone(riskLevel: RiskLevel): "danger" | "info" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  return "info";
}

function operationTone(mode: string): "danger" | "success" | "warning" {
  if (mode === "STOP") return "danger";
  if (mode === "LIMITED") return "warning";
  return "success";
}

function riskFromWeather(day: OpenWeatherForecastDay): RiskLevel {
  const visibility = day.visibilityKm ?? 10;
  if (day.windSpeedMs >= 24.5 || day.rainMm >= 50 || visibility < 1) return "CRITICAL";
  if (day.windSpeedMs >= 17.2 || day.rainMm >= 25 || visibility < 3) return "HIGH";
  if (day.windSpeedMs >= 10.8 || day.rainMm >= 10 || visibility < 6) return "MEDIUM";
  return "LOW";
}

function addDays(base: Date, days: number) {
  const next = new Date(base);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function buildLongRangeItems(forecast: OpenWeatherForecast, horizonDays: number): ForecastRiskAnalysisInputItem[] {
  const seedDays = forecast.days.length > 0 ? forecast.days : [];
  if (seedDays.length === 0) return [];

  const startDate = new Date(seedDays[0].date);
  return Array.from({ length: horizonDays }, (_, index) => {
    const seed = seedDays[index % seedDays.length];
    const cycle = Math.floor(index / seedDays.length);
    const seasonalWave = Math.sin((index / Math.max(horizonDays, 1)) * Math.PI * 2);
    const windSpeedMs = clamp(seed.windSpeedMs + cycle * 0.35 + seasonalWave * 1.2, 0, 35);
    const rainfallMm = clamp(seed.rainMm + cycle * 0.45 + Math.max(0, seasonalWave) * 3, 0, 80);
    const visibilityKm = seed.visibilityKm === null
      ? null
      : clamp(seed.visibilityKm - cycle * 0.08 - Math.max(0, seasonalWave) * 0.5, 0.2, 20);
    const projectedDay: OpenWeatherForecastDay = {
      ...seed,
      rainMm: rainfallMm,
      visibilityKm,
      windSpeedMs
    };
    const ruleRiskLevel = riskFromWeather(projectedDay);

    return {
      humidityPct: seed.humidityPct,
      plannedAt: addDays(startDate, index).toISOString(),
      pressureHpa: seed.pressureHpa,
      rainRiskLevel: rainfallMm >= 25 ? "HIGH" : rainfallMm >= 10 ? "MEDIUM" : "LOW",
      rainfallMm,
      ruleRiskLevel,
      temperatureC: seed.temperatureDayC,
      visibilityKm,
      visibilityRiskLevel: visibilityKm !== null && visibilityKm < 3 ? "HIGH" : visibilityKm !== null && visibilityKm < 6 ? "MEDIUM" : "LOW",
      windRiskLevel: windSpeedMs >= 17.2 ? "HIGH" : windSpeedMs >= 10.8 ? "MEDIUM" : "LOW",
      windSpeedMs
    };
  });
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
}

export function AiLongRangeForecastPage() {
  const [ports, setPorts] = useState<PortSummary[]>([]);
  const [portCode, setPortCode] = useState("DNTSA");
  const [horizon, setHorizon] = useState<HorizonOption>(horizonOptions[0]);
  const [forecast, setForecast] = useState<OpenWeatherForecast | null>(null);
  const [analysis, setAnalysis] = useState<ForecastRiskAnalysis | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

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

  const runPrediction = useCallback(async (nextHorizon = horizon) => {
    setLoading(true);
    setError("");
    try {
      const openWeatherForecast = await getOpenWeatherForecast(portCode, 5);
      const longRangeItems = buildLongRangeItems(openWeatherForecast, nextHorizon.days);
      const nextAnalysis = await analyzeForecastRisk({
        items: longRangeItems,
        portCode: openWeatherForecast.portCode
      });
      setForecast(openWeatherForecast);
      setAnalysis(nextAnalysis);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Không thể tạo dữ liệu AI dự đoán dài hạn.");
      setAnalysis(null);
    } finally {
      setLoading(false);
    }
  }, [horizon, portCode]);

  useEffect(() => {
    void runPrediction();
  }, [runPrediction]);

  const chartRows = useMemo(() => analysis?.items.map((item, index) => ({
    confidence: Math.max(35, 92 - index * (horizon.days > 30 ? 0.55 : 0.85)),
    date: formatDate(item.plannedAt),
    operationMode: item.mlRecommendation,
    riskLevel: item.ruleRiskLevel,
    score: item.pcaRiskScore
  })) ?? [], [analysis, horizon.days]);

  const summary = useMemo(() => {
    const items = analysis?.items ?? [];
    const highOrCritical = items.filter((item) => item.ruleRiskLevel === "HIGH" || item.ruleRiskLevel === "CRITICAL").length;
    const stopDays = items.filter((item) => item.mlRecommendation === "STOP").length;
    const averageScore = items.length
      ? Math.round(items.reduce((total, item) => total + item.pcaRiskScore, 0) / items.length)
      : 0;
    return { averageScore, highOrCritical, stopDays };
  }, [analysis]);

  async function selectHorizon(option: HorizonOption) {
    setHorizon(option);
    await runPrediction(option);
  }

  return (
    <section className="page-grid ai-forecast-page">
      <div className="section-heading">
        <div>
          <span className="eyebrow">AI planning</span>
          <h1>AI dự đoán dài hạn</h1>
          <p>Tách riêng dữ liệu OpenWeather 5 ngày và dữ liệu AI tự dự đoán cho các mốc xa hơn: 7 ngày, 14 ngày, 30 ngày, 2 tháng và 3 tháng.</p>
        </div>
      </div>

      <article className="card card-pad ai-forecast-source-card">
        <div className="card-head">
          <div>
            <h3>Dữ liệu OpenWeather 5 ngày</h3>
            <p>Nguồn dữ liệu thật từ OpenWeather API, dùng làm đầu vào tham chiếu cho mô hình AI dài hạn.</p>
          </div>
          <Badge tone="success">Nguồn thật</Badge>
        </div>
        <div className="simulation-forecast-controls">
          <label>
            <span>Cảng</span>
            <select className="input select-input" onChange={(event) => setPortCode(event.target.value)} value={portCode}>
              {ports.length ? ports.map((port) => (
                <option key={port.portId} value={port.portCode}>{port.portCode} - {port.portName}</option>
              )) : <option value="DNTSA">DNTSA</option>}
            </select>
          </label>
          <button className="button button-primary" disabled={loading} onClick={() => void runPrediction()} type="button">
            {loading ? "Đang chạy AI..." : "Chạy lại dự đoán"}
          </button>
        </div>
        <div className="ai-forecast-seed-grid">
          {forecast?.days.map((day) => (
            <div className="ai-forecast-seed-day" key={day.date}>
              <strong>{formatDate(day.date)}</strong>
              <span>{day.weatherDescription ?? day.summary ?? "OpenWeather"}</span>
              <small>Gió {day.windSpeedMs.toFixed(1)} m/s · Mưa {day.rainMm.toFixed(1)} mm</small>
              <small>Tầm nhìn {day.visibilityKm !== null ? `${day.visibilityKm.toFixed(1)} km` : "chưa có"}</small>
            </div>
          )) ?? <div className="simulation-forecast-empty">Đang tải dữ liệu OpenWeather 5 ngày...</div>}
        </div>
      </article>

      <article className="card card-pad ai-forecast-main-card">
        <div className="card-head">
          <div>
            <h3>AI dự đoán dài hạn</h3>
            <p>Đây là dữ liệu AI nội bộ dùng để tham khảo lập kế hoạch, không thay thế dữ liệu dự báo thời tiết thật.</p>
          </div>
          <Badge tone="info">Độ tin cậy: {horizon.confidence}</Badge>
        </div>

        <div className="ai-horizon-tabs" aria-label="Mốc dự đoán dài hạn">
          {horizonOptions.map((option) => (
            <button
              className={option.days === horizon.days ? "is-active" : ""}
              disabled={loading}
              key={option.days}
              onClick={() => void selectHorizon(option)}
              type="button"
            >
              {option.label}
            </button>
          ))}
        </div>

        <div className="ai-forecast-summary-grid">
          <div>
            <span>Mốc đang xem</span>
            <strong>{horizon.label}</strong>
            <small>{horizon.note}</small>
          </div>
          <div>
            <span>Risk score trung bình</span>
            <strong>{summary.averageScore}</strong>
            <small>Thang AI 0-100</small>
          </div>
          <div>
            <span>Ngày HIGH/CRITICAL</span>
            <strong>{summary.highOrCritical}</strong>
            <small>Cần theo dõi kế hoạch vận hành</small>
          </div>
          <div>
            <span>Ngày STOP</span>
            <strong>{summary.stopDays}</strong>
            <small>Theo khuyến nghị AI</small>
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}
        {loading ? <div className="simulation-forecast-empty">Đang chạy PCA, K-Means và Gemini cho mốc {horizon.label}...</div> : null}

        {analysis ? (
          <>
            <div className="ai-forecast-chart-card">
              <div>
                <h3>Biểu đồ đường AI risk score</h3>
                <p>Score càng cao thì rủi ro vận hành càng lớn; các mốc dài hạn có độ tin cậy giảm dần.</p>
              </div>
              <div aria-label="Biểu đồ đường AI dự đoán dài hạn" className="ai-forecast-line-chart">
                <LineChart
                  height={300}
                  margin={{ bottom: 38, left: 48, right: 22, top: 22 }}
                  series={[{
                    color: "#2563eb",
                    curve: "linear",
                    data: chartRows.map((row) => row.score),
                    label: "AI risk score",
                    showMark: true
                  }]}
                  xAxis={[{
                    data: chartRows.map((row) => row.date),
                    scaleType: "point"
                  }]}
                  yAxis={[{
                    max: 100,
                    min: 0,
                    tickMinStep: 20
                  }]}
                />
              </div>
            </div>

            <div className="forecast-ml-legend ai-forecast-score-legend" aria-label="Chú thích mức độ AI score">
              <span><i className="score-low" />0-24 LOW</span>
              <span><i className="score-medium" />25-49 MEDIUM</span>
              <span><i className="score-high" />50-74 HIGH</span>
              <span><i className="score-critical" />75-100 CRITICAL</span>
            </div>

            <div className="forecast-table-shell">
              <table className="forecast-table ai-forecast-table">
                <thead>
                  <tr>
                    <th>Ngày</th>
                    <th>AI score</th>
                    <th>Rule risk</th>
                    <th>K-Means cluster</th>
                    <th>Khuyến nghị</th>
                    <th>Độ tin cậy</th>
                  </tr>
                </thead>
                <tbody>
                  {analysis.items.map((item, index) => (
                    <tr key={item.plannedAt}>
                      <td>{formatDate(item.plannedAt)}</td>
                      <td><strong>{item.pcaRiskScore}</strong></td>
                      <td><Badge tone={riskTone(item.ruleRiskLevel)}>{item.ruleRiskLevel}</Badge></td>
                      <td>{item.clusterLabel}</td>
                      <td><Badge tone={operationTone(item.mlRecommendation)}>{item.mlRecommendation}</Badge></td>
                      <td>{Math.round(chartRows[index]?.confidence ?? 40)}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <section className="forecast-llm-plan ai-forecast-explain">
              <div className="forecast-llm-head">
                <div>
                  <h3>Giải thích AI</h3>
                  <p>{analysis.llmPlanAnalysis?.summary ?? "Mô hình PCA/K-Means đã phân tích chuỗi dữ liệu dài hạn để hỗ trợ lập kế hoạch vận hành."}</p>
                </div>
                <Badge tone="muted">{analysis.modelVersion}</Badge>
              </div>
            </section>
          </>
        ) : null}
      </article>
    </section>
  );
}
