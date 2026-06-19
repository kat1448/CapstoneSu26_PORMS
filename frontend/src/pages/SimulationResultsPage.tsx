import { useEffect, useState } from "react";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getSimulationSnapshot } from "../services/simulationService";
import type { SimulationSnapshot } from "../types/simulation";

type SimulationResultsPageProps = {
  refreshKey: number;
};

export function SimulationResultsPage({ refreshKey }: SimulationResultsPageProps) {
  useDemoRefresh();
  const [snapshot, setSnapshot] = useState<SimulationSnapshot | null>(null);

  useEffect(() => {
    void getSimulationSnapshot().then(setSnapshot);
  }, [refreshKey]);

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Kết quả mô phỏng</h2>
          <p>Tóm tắt lần chạy demo gần nhất</p>
        </div>
      </div>
      <div className="stats-grid">
        <article className="card stat-card">
          <span>Rủi ro đỉnh</span>
          <strong>{snapshot?.currentRiskLevel ?? "HIGH"}</strong>
        </article>
        <article className="card stat-card">
          <span>Lần đổi chế độ</span>
          <strong>{snapshot?.modeChangeCount ?? 0}</strong>
        </article>
        <article className="card stat-card">
          <span>Cảnh báo phát</span>
          <strong>{snapshot?.generatedAlertCount ?? 0}</strong>
        </article>
      </div>
    </section>
  );
}
