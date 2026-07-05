import type { RiskLevel } from "../../types/dashboard";
import type { PortZone } from "../../types/port";

const riskLevels: RiskLevel[] = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];
const riskLabels: Record<RiskLevel, string> = {
  CRITICAL: "CRITICAL",
  HIGH: "HIGH",
  LOW: "LOW",
  MEDIUM: "MEDIUM"
};

export function ZoneRiskOverviewCard({ zones }: { zones: PortZone[] }) {
  const counts = riskLevels.reduce<Record<RiskLevel, number>>((result, level) => ({
    ...result,
    [level]: zones.filter((zone) => zone.currentRiskLevel === level).length
  }), { CRITICAL: 0, HIGH: 0, LOW: 0, MEDIUM: 0 });
  const maxCount = Math.max(...riskLevels.map((level) => counts[level]), 1);
  const totalZones = zones.length;
  const percentages = riskLevels.reduce<Record<RiskLevel, number>>((result, level) => ({
    ...result,
    [level]: totalZones > 0 ? Math.round((counts[level] / totalZones) * 100) : 0
  }), { CRITICAL: 0, HIGH: 0, LOW: 0, MEDIUM: 0 });

  return (
    <article className="card zone-risk-overview-card">
      <div className="card-head">
        <div>
          <h3>Tổng quan rủi ro khu vực</h3>
          <p>{totalZones} khu vực đang được theo dõi</p>
        </div>
      </div>
      <div className="zone-risk-summary-grid">
        {riskLevels.map((level) => (
          <div aria-label={`${level} zones`} className={`zone-risk-summary risk-${level.toLowerCase()}`} key={level}>
            <span>{riskLabels[level]}</span>
            <strong>{counts[level]}</strong>
          </div>
        ))}
      </div>
      <div aria-label="Biểu đồ cột rủi ro khu vực" className="zone-risk-chart">
        {riskLevels.map((level) => {
          const height = `${Math.max((counts[level] / maxCount) * 100, counts[level] > 0 ? 16 : 3)}%`;
          return (
            <div className="zone-risk-bar-column" key={level}>
              <div className="zone-risk-bar-track">
                <span className={`zone-risk-bar-fill risk-${level.toLowerCase()}`} style={{ height }} />
              </div>
              <small>{level}</small>
              <em>{percentages[level]}%</em>
            </div>
          );
        })}
      </div>
    </article>
  );
}
