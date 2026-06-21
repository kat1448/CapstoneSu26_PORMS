import type { DashboardSummary, WeatherSnapshot } from "../../types/dashboard";

export function RiskHeroCard({ summary, weather }: { summary: DashboardSummary; weather: WeatherSnapshot }) {
  const score = { LOW: "1 / 4", MEDIUM: "2 / 4", HIGH: "3 / 4", CRITICAL: "4 / 4" }[summary.currentRiskLevel];
  const width = { LOW: "18%", MEDIUM: "43%", HIGH: "73%", CRITICAL: "96%" }[summary.currentRiskLevel];

  return (
    <article className="card risk-hero-card">
      <div className="risk-kicker">Mức rủi ro hiện tại</div>
      <div className="risk-main"><span className="risk-word">{summary.currentRiskLevel}</span><span className="risk-score">{score}</span></div>
      <div className="risk-bar"><span style={{ width }} /></div>
      <p className="risk-reason">Yếu tố chi phối: <strong>Gió cấp {summary.beaufortNumber ?? "—"}</strong>. Kết hợp lượng mưa {weather.rainfall1hMm.toFixed(1)} mm/h làm tăng rủi ro tại khu vực cầu cảng.</p>
    </article>
  );
}
