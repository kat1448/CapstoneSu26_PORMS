import { Badge } from "../common/Badge";
import type { OperationMode } from "../../types/dashboard";

type ModeIndicatorProps = {
  mode: OperationMode;
};

export function ModeIndicator({ mode }: ModeIndicatorProps) {
  const tone = mode === "STOP" ? "danger" : mode === "LIMITED" ? "warning" : "success";

  return <Badge tone={tone}>{mode}</Badge>;
}
