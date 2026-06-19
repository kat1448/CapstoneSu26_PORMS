import type { OperationMode } from "../../types/dashboard";
import { Badge } from "../common/Badge";

const descriptions: Record<OperationMode, string> = {
  LIMITED: "Hạn chế một phần hoạt động",
  NORMAL: "Hoạt động bình thường",
  STOP: "Dừng toàn bộ hoạt động"
};

export function ModeCard({ operationMode }: { operationMode: OperationMode }) {
  const tone = operationMode === "STOP" ? "danger" : operationMode === "LIMITED" ? "warning" : "success";
  return (
    <article className="card mode-panel">
      <div><div className="card-sub">Chế độ vận hành</div><div className={`mode-orb mode-${operationMode.toLowerCase()}`}>{operationMode}</div><div className="card-title">{descriptions[operationMode]}</div><div className="card-sub">Tự động cập nhật theo Risk Engine</div></div>
      <div className="mode-row"><Badge tone={tone}>{operationMode}</Badge></div>
    </article>
  );
}
