import { Badge } from "../common/Badge";
import type { RiskLevel } from "../../types/dashboard";

type RiskBadgeProps = {
  riskLevel: RiskLevel;
};

export function RiskBadge({ riskLevel }: RiskBadgeProps) {
  const tone =
    riskLevel === "CRITICAL"
      ? "danger"
      : riskLevel === "HIGH"
        ? "warning"
        : riskLevel === "MEDIUM"
          ? "info"
          : "success";

  return <Badge tone={tone}>{riskLevel}</Badge>;
}
