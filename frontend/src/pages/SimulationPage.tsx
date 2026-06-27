import { FormEvent, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { SimulationMap } from "../components/simulation/SimulationMap";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import {
  createSimulationDataset,
  getSimulationDatasets,
  getSimulationMapPoints,
  getSimulationResult,
  getSimulationSnapshot,
  runDemoSimulation,
  runSimulationDataset
} from "../services/simulationService";
import { getPorts, getPortZones } from "../services/portService";
import type {
  CreateSimulationDatasetInput,
  SimulationDatasetSummary,
  SimulationMapPoint,
  SimulationResult,
  SimulationSnapshot
} from "../types/simulation";
import type { PortZone } from "../types/port";

type SimulationPageProps = {
  refreshKey: number;
};

function riskTone(riskLevel: string): "danger" | "info" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  return "info";
}

export function SimulationPage({ refreshKey }: SimulationPageProps) {
  const demoVersion = useDemoRefresh();
  const [snapshot, setSnapshot] = useState<SimulationSnapshot | null>(null);
  const [datasets, setDatasets] = useState<SimulationDatasetSummary[]>([]);
  const [baseMapPoints, setBaseMapPoints] = useState<SimulationMapPoint[]>([]);
  const [selectedDatasetId, setSelectedDatasetId] = useState("");
  const [result, setResult] = useState<SimulationResult | null>(null);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [saveMessage, setSaveMessage] = useState("");
  const [saveError, setSaveError] = useState("");
  const [formZones, setFormZones] = useState<PortZone[]>([]);
  const [loadingFormZones, setLoadingFormZones] = useState(false);
  const [datasetForm, setDatasetForm] = useState<CreateSimulationDatasetInput>({
    description: "",
    name: "",
    portCode: "DNTSA",
    snapshots: [{
      beaufortNumber: 8,
      rainfall1hMm: 28,
      snapshotNumber: 1,
      visibilityKm: 4,
      windSpeedMs: 18,
      zoneId: ""
    }]
  });
  const [running, setRunning] = useState(false);
  const [savingDataset, setSavingDataset] = useState(false);

  useEffect(() => {
    void getSimulationSnapshot().then(setSnapshot);
  }, [demoVersion, refreshKey]);

  useEffect(() => {
    let active = true;
    getSimulationDatasets()
      .then((items) => {
        if (!active) return;
        setDatasets(items);
        setSelectedDatasetId((current) => current || items[0]?.datasetId || "");
      })
      .catch(() => {
        if (active) setDatasets([]);
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    getSimulationMapPoints()
      .then((points) => {
        if (active) setBaseMapPoints(points);
      })
      .catch(() => {
        if (active) setBaseMapPoints([]);
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!createModalOpen || !datasetForm.portCode.trim()) return undefined;

    let active = true;
    setLoadingFormZones(true);
    getPorts()
      .then(async (ports) => {
        const selectedPort = ports.find((port) => port.portCode.toUpperCase() === datasetForm.portCode.trim().toUpperCase());
        if (!selectedPort) return [];
        return getPortZones(selectedPort.portId);
      })
      .then((zones) => {
        if (active) setFormZones(zones);
      })
      .catch(() => {
        if (active) setFormZones([]);
      })
      .finally(() => {
        if (active) setLoadingFormZones(false);
      });

    return () => {
      active = false;
    };
  }, [createModalOpen, datasetForm.portCode]);

  if (!snapshot) {
    return <section className="page-grid"><article className="card loading-card">Đang tải mô phỏng...</article></section>;
  }

  const isRunning = running || snapshot.status === "RUNNING";
  const statusLabel = isRunning
    ? "Đang phát dữ liệu thời gian thực"
    : snapshot.status === "COMPLETED"
      ? "Mô phỏng hoàn tất"
      : "Sẵn sàng mô phỏng";

  async function handleRunSimulation() {
    setRunning(true);
    await runDemoSimulation();
    setSnapshot(await getSimulationSnapshot());
    setRunning(false);
  }

  async function handleSaveDataset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSavingDataset(true);
    setSaveError("");
    try {
      const created = await createSimulationDataset(datasetForm);
      setDatasets((current) => [created, ...current.filter((item) => item.datasetId !== created.datasetId)]);
      setSelectedDatasetId(created.datasetId);
      setSaveMessage("Đã lưu dữ liệu mô phỏng");
      setCreateModalOpen(false);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Không lưu được dữ liệu mô phỏng");
    } finally {
      setSavingDataset(false);
    }
  }

  async function handleRunDataset() {
    if (!selectedDatasetId) return;
    setRunning(true);
    try {
      const run = await runSimulationDataset(selectedDatasetId);
      setSnapshot(await getSimulationSnapshot());
      setResult(await getSimulationResult(run.sessionId));
    } finally {
      setRunning(false);
    }
  }

  const firstSnapshot = datasetForm.snapshots[0];
  const mapPoints = result?.mapPoints.length ? result.mapPoints : baseMapPoints;

  return (
    <section className="page-grid simulation-page">
      <div className="section-heading">
        <div>
          <h2>Chế độ mô phỏng</h2>
          <p>Replay dữ liệu bão lịch sử để huấn luyện và trình diễn</p>
        </div>
        {snapshot.status === "COMPLETED" ? (
          <Link className="button button-secondary" to="/simulation-results">
            Xem kết quả gần nhất
          </Link>
        ) : null}
      </div>

      <article className="card card-pad simulation-status-panel" data-testid="simulation-status-panel">
        <div className="simulation-status-head">
          <div>
            <div className="sim-status">
              <span className={`sim-dot${isRunning ? " pulse-dot" : ""}`} />
              {statusLabel}
            </div>
            <h3>Kịch bản bão Đà Nẵng 10/2023</h3>
            <p>
              Dữ liệu mô phỏng quá trình gió tăng từ cấp 4 lên cấp 10, mưa lớn và tầm nhìn giảm.
              Hệ thống đánh giá rủi ro, kích hoạt SOP và cập nhật chế độ vận hành.
            </p>
          </div>
          <Badge tone={riskTone(snapshot.currentRiskLevel)}>{snapshot.currentRiskLevel}</Badge>
        </div>
        <div className="progress-bar sim-progress" aria-label="Tiến độ mô phỏng">
          <span style={{ width: `${snapshot.progressPercent}%` }} />
        </div>
        <div className="sim-progress-label">
          <span>Bắt đầu</span>
          <span>{snapshot.progressPercent}% · {isRunning ? "Đang chạy" : snapshot.status === "COMPLETED" ? "Hoàn tất" : "Chưa chạy"}</span>
          <span>Kết thúc</span>
        </div>
      </article>

      <div className="simulation-kpi-grid" data-testid="simulation-kpi-grid">
        <article className="card card-pad sim-kpi-card">
          <span>Gió</span>
          <strong>{snapshot.windSpeedMs.toFixed(1)} m/s</strong>
          <small>Beaufort cấp {snapshot.beaufortNumber}</small>
        </article>
        <article className="card card-pad sim-kpi-card">
          <span>Mưa 1 giờ</span>
          <strong>{snapshot.rainfall1hMm.toFixed(1)} mm</strong>
          <small>Tầm nhìn {snapshot.visibilityKm.toFixed(1)} km</small>
        </article>
        <article className="card card-pad sim-kpi-card">
          <span>Chế độ</span>
          <strong>{snapshot.currentMode}</strong>
          <small>{snapshot.modeChangeCount} lần đổi chế độ</small>
        </article>
        <article className="card card-pad sim-kpi-card">
          <span>Cảnh báo</span>
          <strong>{snapshot.generatedAlertCount}</strong>
          <small>Sinh từ luồng mô phỏng</small>
        </article>
      </div>

      <div className="simulation-workbench-grid">
        {false ? (
        <article className="card card-pad simulation-data-panel">
          <div className="card-head">
            <div>
              <h3>Dữ liệu mô phỏng</h3>
              <p>Nhập dữ liệu thời tiết để lưu thành dataset thật trong database</p>
            </div>
          </div>
          <form className="simulation-data-form" onSubmit={handleSaveDataset}>
            <label>
              <span>Tên kịch bản</span>
              <input
                aria-label="Tên kịch bản"
                onChange={(event) => setDatasetForm((value) => ({ ...value, name: event.target.value }))}
                required
                value={datasetForm.name}
              />
            </label>
            <label>
              <span>Mã cảng</span>
              <input
                aria-label="Mã cảng"
                onChange={(event) => setDatasetForm((value) => ({ ...value, portCode: event.target.value }))}
                required
                value={datasetForm.portCode}
              />
            </label>
            <label className="wide-field">
              <span>Mô tả</span>
              <input
                aria-label="Mô tả kịch bản"
                onChange={(event) => setDatasetForm((value) => ({ ...value, description: event.target.value }))}
                value={datasetForm.description ?? ""}
              />
            </label>
            <label>
              <span>Gió m/s</span>
              <input
                aria-label="Gió m/s"
                onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, windSpeedMs: Number(event.target.value) }] }))}
                type="number"
                value={firstSnapshot.windSpeedMs}
              />
            </label>
            <label>
              <span>Beaufort</span>
              <input
                aria-label="Beaufort"
                onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, beaufortNumber: Number(event.target.value) }] }))}
                type="number"
                value={firstSnapshot.beaufortNumber}
              />
            </label>
            <label>
              <span>Mưa mm/h</span>
              <input
                aria-label="Mưa mm/h"
                onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, rainfall1hMm: Number(event.target.value) }] }))}
                type="number"
                value={firstSnapshot.rainfall1hMm}
              />
            </label>
            <label>
              <span>Tầm nhìn km</span>
              <input
                aria-label="Tầm nhìn km"
                onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, visibilityKm: Number(event.target.value) }] }))}
                type="number"
                value={firstSnapshot.visibilityKm}
              />
            </label>
            <label>
              <span>Zone ID</span>
              <input
                aria-label="Zone ID"
                onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, zoneId: event.target.value || null }] }))}
                placeholder="Để trống dùng khu vực đầu tiên"
                value={firstSnapshot.zoneId ?? ""}
              />
            </label>
            <div className="form-actions wide-field">
              <button className="button button-secondary" disabled={savingDataset} type="submit">Lưu dữ liệu</button>
            </div>
          </form>
        </article>
        ) : null}

        <article className="card card-pad simulation-map-panel simulation-map-panel-wide">
          <div className="card-head">
            <div>
              <h3>Bản đồ mô phỏng</h3>
              <p>Điểm thay đổi sẽ phát sáng và tỏa vòng màu theo mức rủi ro</p>
            </div>
            <Badge tone={mapPoints.length ? "info" : "muted"}>{mapPoints.length} điểm</Badge>
          </div>
          <SimulationMap points={mapPoints} running={running || snapshot.status === "RUNNING"} />
          <div className="simulation-map-actions">
            <button
              className="button button-secondary"
              onClick={() => {
                setSaveMessage("");
                setSaveError("");
                setCreateModalOpen(true);
              }}
              type="button"
            >
              Tạo dữ liệu mô phỏng
            </button>
            {saveMessage ? <span className="form-success">{saveMessage}</span> : null}
          </div>
        </article>
      </div>

      {createModalOpen ? (
        <div className="simulation-result-modal" role="dialog" aria-label="Tạo dữ liệu mô phỏng">
          <article className="card card-pad simulation-result-card">
            <div className="card-head">
              <div>
                <h3>Tạo dữ liệu mô phỏng</h3>
                <p>Nhập dữ liệu thời tiết để lưu thành dataset thật trong database</p>
              </div>
              <button className="button button-secondary button-small" onClick={() => setCreateModalOpen(false)} type="button">Đóng</button>
            </div>
            <form className="simulation-data-form" onSubmit={handleSaveDataset}>
              <label>
                <span>Tên kịch bản</span>
                <input
                  aria-label="Tên kịch bản"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, name: event.target.value }))}
                  required
                  value={datasetForm.name}
                />
              </label>
              <label>
                <span>Mã cảng</span>
                <input
                  aria-label="Mã cảng"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, portCode: event.target.value, snapshots: [{ ...firstSnapshot, zoneId: null }] }))}
                  required
                  value={datasetForm.portCode}
                />
              </label>
              <label className="wide-field">
                <span>Mô tả</span>
                <input
                  aria-label="Mô tả kịch bản"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, description: event.target.value }))}
                  value={datasetForm.description ?? ""}
                />
              </label>
              <label>
                <span>Gió m/s</span>
                <input
                  aria-label="Gió m/s"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, windSpeedMs: Number(event.target.value) }] }))}
                  type="number"
                  value={firstSnapshot.windSpeedMs}
                />
              </label>
              <label>
                <span>Beaufort</span>
                <input
                  aria-label="Beaufort"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, beaufortNumber: Number(event.target.value) }] }))}
                  type="number"
                  value={firstSnapshot.beaufortNumber}
                />
              </label>
              <label>
                <span>Mưa mm/h</span>
                <input
                  aria-label="Mưa mm/h"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, rainfall1hMm: Number(event.target.value) }] }))}
                  type="number"
                  value={firstSnapshot.rainfall1hMm}
                />
              </label>
              <label>
                <span>Tầm nhìn km</span>
                <input
                  aria-label="Tầm nhìn km"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, visibilityKm: Number(event.target.value) }] }))}
                  type="number"
                  value={firstSnapshot.visibilityKm}
                />
              </label>
              <label>
                <span>Zone ID</span>
                <select
                  aria-label="Zone ID"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, snapshots: [{ ...firstSnapshot, zoneId: event.target.value || null }] }))}
                  value={firstSnapshot.zoneId ?? ""}
                >
                  <option value="">{loadingFormZones ? "Đang tải khu vực..." : "Tự chọn khu vực đầu tiên"}</option>
                  {formZones.map((zone) => (
                    <option key={zone.zoneId} value={zone.zoneId}>{zone.zoneName}</option>
                  ))}
                </select>
              </label>
              <div className="form-actions wide-field">
                <button className="button button-secondary" disabled={savingDataset} type="button" onClick={() => setCreateModalOpen(false)}>Hủy</button>
                <button className="button button-primary" disabled={savingDataset} type="submit">
                  {savingDataset ? "Đang lưu..." : "Lưu dữ liệu"}
                </button>
              </div>
              {saveError ? <p className="form-error wide-field">{saveError}</p> : null}
            </form>
          </article>
        </div>
      ) : null}

      <div className="simulation-main-grid" data-testid="simulation-main-grid">
        <aside className="card card-pad simulation-settings">
          <div>
            <h3>Thiết lập mô phỏng</h3>
            <p>Bộ dữ liệu replay phục vụ demo vận hành</p>
          </div>
          <label className="field-label" htmlFor="simulation-dataset">Bộ dữ liệu</label>
          <select
            className="input select-input"
            disabled={isRunning}
            id="simulation-dataset"
            onChange={(event) => setSelectedDatasetId(event.target.value)}
            value={selectedDatasetId}
          >
            {datasets.length === 0 ? <option value="">Chưa có dữ liệu</option> : null}
            {datasets.map((dataset) => (
              <option key={dataset.datasetId} value={dataset.datasetId}>{dataset.name}</option>
            ))}
          </select>
          <label className="field-label" htmlFor="simulation-speed">Tốc độ phát lại</label>
          <select className="input select-input" disabled={isRunning} id="simulation-speed">
            <option>1x - Thực tế</option>
            <option>2x - Nhanh</option>
            <option>5x - Demo</option>
          </select>
          <div className="sim-dataset-card">
            <strong>120 mẫu thời tiết</strong>
            <span>Thời lượng gốc: 30 giờ</span>
            <span>Độ phân giải: 15 phút</span>
            <span>Rủi ro đỉnh: CRITICAL</span>
          </div>
          <button
            className={isRunning ? "button button-danger simulation-action" : "button button-primary simulation-action"}
            disabled={isRunning}
            onClick={handleRunSimulation}
            type="button"
          >
            {isRunning ? "Đang phát dữ liệu..." : snapshot.status === "COMPLETED" ? "Chạy lại" : "Bắt đầu mô phỏng"}
          </button>
          <button
            className="button button-secondary simulation-action"
            disabled={running || !selectedDatasetId}
            onClick={handleRunDataset}
            type="button"
          >
            Chạy dữ liệu đã chọn
          </button>
        </aside>

        <article className="card card-pad simulation-feed-panel">
          <div className="card-head">
            <div>
              <h3>Luồng sự kiện mô phỏng</h3>
              <p>Các phản ứng tự động sẽ xuất hiện tại đây</p>
            </div>
          </div>
          <div className="timeline">
            {snapshot.feed.length === 0 ? (
              <div className="empty-state">
                <strong>Chưa có dữ liệu mô phỏng</strong>
                <span>Bấm “Bắt đầu mô phỏng” để phát dữ liệu.</span>
              </div>
            ) : (
              snapshot.feed.map((item) => (
                <div className={`timeline-item sim-feed-item risk-${item.riskLevel.toLowerCase()}`} key={`${item.title}-${item.happenedAt}`}>
                  <div className="timeline-header">
                    <strong>{item.title}</strong>
                    <small>{item.happenedAt}</small>
                  </div>
                  <p>{item.detail}</p>
                  <Badge tone={riskTone(item.riskLevel)}>{item.riskLevel}</Badge>
                </div>
              ))
            )}
          </div>
        </article>
      </div>

      {result ? (
        <div className="simulation-result-modal" role="dialog" aria-label="Kết quả mô phỏng">
          <article className="card card-pad simulation-result-card">
            <div className="card-head">
              <div>
                <h3>Kết quả mô phỏng</h3>
                <p>Khu vực nguy hiểm và task cần thực hiện</p>
              </div>
              <button className="button button-secondary button-small" onClick={() => setResult(null)} type="button">Đóng</button>
            </div>
            <div className="simulation-result-grid">
              <div>
                <h4>Khu vực nguy hiểm</h4>
                {result.dangerousZones.map((zone) => (
                  <div className="result-row" key={zone.zoneId}>
                    <strong>{zone.zoneName}</strong>
                    <Badge tone={riskTone(zone.riskLevel)}>{zone.riskLevel}</Badge>
                    <small>{zone.reason ?? "Vượt ngưỡng mô phỏng"}</small>
                  </div>
                ))}
              </div>
              <div>
                <h4>Task sẽ thực hiện</h4>
                {result.tasks.map((task) => (
                  <div className="result-row" key={task.taskCode}>
                    <strong>{task.title}</strong>
                    <small>{task.taskCode} · {task.zoneName ?? "Toàn cảng"} · {task.priority}</small>
                  </div>
                ))}
              </div>
            </div>
          </article>
        </div>
      ) : null}
    </section>
  );
}
