import type { RiskTrendPoint } from "../../types/dashboard";
import type { RiskLevel } from "../../types/dashboard";
import { Badge } from "../common/Badge";

type RiskTrendChartProps = {
  currentRiskLevel: RiskLevel;
  points: RiskTrendPoint[];
};

export function RiskTrendChart({ currentRiskLevel, points }: RiskTrendChartProps) {
  const maxHeight = 150;
  const width = 440;
  const stepX = width / Math.max(points.length - 1, 1);
  const path = points
    .map((point, index) => `${index === 0 ? "M" : "L"} ${index * stepX} ${maxHeight - point.riskScore * 30}`)
    .join(" ");

  return (
    <article className="card chart-card">
      <div className="card-head">
        <div>
          <h3>Xu hướng rủi ro 24 giờ</h3><p>Dữ liệu tổng hợp theo giờ · LOW 1 — CRITICAL 4</p>
        </div>
        <Badge tone={currentRiskLevel === "CRITICAL" ? "danger" : currentRiskLevel === "HIGH" ? "warning" : currentRiskLevel === "LOW" ? "success" : "info"}>{currentRiskLevel}</Badge>
      </div>
      <svg className="chart-svg" viewBox={`0 0 ${width} 180`} preserveAspectRatio="none">
        <path d={path} />
        {points.map((point, index) => (
          <g key={`${point.hourLabel}-${point.riskScore}`}>
            <circle cx={index * stepX} cy={maxHeight - point.riskScore * 30} r="4" />
            <text x={index * stepX} y="176">
              {point.hourLabel}
            </text>
          </g>
        ))}
      </svg>
    </article>
  );
}
