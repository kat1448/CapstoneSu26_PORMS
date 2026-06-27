import type { RiskLevel, RiskTrendPoint } from "../../types/dashboard";
import { Badge } from "../common/Badge";

type RiskTrendChartProps = {
  currentRiskLevel: RiskLevel;
  points: RiskTrendPoint[];
};

const riskLabels: Record<number, RiskLevel> = {
  1: "LOW",
  2: "MEDIUM",
  3: "HIGH",
  4: "CRITICAL"
};

const riskColors: Record<RiskLevel, string> = {
  CRITICAL: "#d94848",
  HIGH: "#ee7623",
  LOW: "#19a66a",
  MEDIUM: "#e9a11b"
};

function badgeTone(riskLevel: RiskLevel): "danger" | "info" | "success" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  if (riskLevel === "LOW") return "success";
  return "info";
}

function yForScore(score: number) {
  return 154 - score * 32;
}

export function RiskTrendChart({ currentRiskLevel, points }: RiskTrendChartProps) {
  const width = 520;
  const height = 190;
  const plotLeft = 44;
  const plotRight = 18;
  const plotBottom = 166;
  const tooltipWidth = 104;
  const chartPoints = points.slice(-24);
  const plotWidth = width - plotLeft - plotRight;
  const stepX = plotWidth / Math.max(chartPoints.length - 1, 1);
  const coordinates = chartPoints.map((point, index) => ({
    ...point,
    color: riskColors[riskLabels[point.riskScore]],
    riskLevel: riskLabels[point.riskScore],
    x: plotLeft + index * stepX,
    y: yForScore(point.riskScore)
  }));
  const linePath = coordinates
    .map((point, index) => `${index === 0 ? "M" : "L"} ${point.x} ${point.y}`)
    .join(" ");
  const areaPath = coordinates.length
    ? `${linePath} L ${coordinates[coordinates.length - 1].x} ${plotBottom} L ${coordinates[0].x} ${plotBottom} Z`
    : "";

  return (
    <article className="card chart-card risk-trend-card">
      <div className="card-head">
        <div>
          <h3>Xu hướng rủi ro 24 giờ</h3>
          <p>Dữ liệu thật từ đánh giá rủi ro theo giờ</p>
        </div>
        <Badge tone={badgeTone(currentRiskLevel)}>{currentRiskLevel}</Badge>
      </div>

      {coordinates.length === 0 ? (
        <div className="risk-trend-empty">Chưa có dữ liệu rủi ro 24 giờ</div>
      ) : (
        <svg className="chart-svg risk-trend-svg" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none">
          <defs>
            <linearGradient id="riskTrendArea" x1="0" x2="0" y1="0" y2="1">
              <stop offset="0%" stopColor="#ee7623" stopOpacity="0.24" />
              <stop offset="100%" stopColor="#19a66a" stopOpacity="0.04" />
            </linearGradient>
          </defs>
          {[1, 2, 3, 4].map((score) => (
            <g className="risk-trend-grid" key={score}>
              <line x1={plotLeft} x2={width - plotRight} y1={yForScore(score)} y2={yForScore(score)} />
              <text x="6" y={yForScore(score) - 4}>{riskLabels[score]}</text>
            </g>
          ))}
          <path className="risk-trend-area" d={areaPath} />
          <path className="risk-trend-line" d={linePath} />
          <g data-testid="risk-trend-tooltip-layer">
            {coordinates.map((point) => (
              <g
                aria-label={`${point.hourLabel} ${point.riskLevel} score ${point.riskScore}`}
                className="risk-trend-point"
                key={`${point.hourLabel}-${point.riskScore}`}
                tabIndex={0}
              >
                <line className="risk-trend-hover-line" x1={point.x} x2={point.x} y1="18" y2={plotBottom} />
                <circle className="risk-trend-dot-halo" cx={point.x} cy={point.y} r="9" />
                <circle className="risk-trend-dot" cx={point.x} cy={point.y} r="4.5" style={{ fill: point.color }} />
                <foreignObject className="risk-trend-tooltip" x={Math.min(Math.max(point.x - tooltipWidth / 2, 2), width - tooltipWidth - 2)} y={Math.max(point.y - 58, 4)} width={tooltipWidth} height="46">
                  <div>
                    <strong>{point.riskLevel}</strong>
                    <span>{point.hourLabel} · score {point.riskScore}</span>
                  </div>
                </foreignObject>
                <text className="risk-trend-hour" x={point.x} y="184">{point.hourLabel}</text>
              </g>
            ))}
          </g>
        </svg>
      )}
    </article>
  );
}
