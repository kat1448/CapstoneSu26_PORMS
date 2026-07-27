import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { SimulationMap } from "../components/simulation/SimulationMap";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import {
  createSimulationDataset,
  deleteSimulationDataset,
  getSimulationDataset,
  getSimulationDatasets,
  getSimulationMapPoints,
  getSimulationResult,
  getSimulationSnapshot,
  runDemoSimulation,
  runSimulationDataset,
  updateSimulationDataset
} from "../services/simulationService";
import { getPorts, getPortZones } from "../services/portService";
import type {
  CreateSimulationDatasetInput,
  SimulationDatasetSummary,
  SimulationMapPoint,
  SimulationResult,
  SimulationSnapshot
} from "../types/simulation";
import type { PortSummary, PortZone } from "../types/port";
import { operationModeLabel, riskLabel, simulationDetailLabel, simulationEventLabels } from "../utils/displayLabels";

type SimulationPageProps = {
  refreshKey: number;
};

function riskTone(riskLevel: string): "danger" | "info" | "warning" {
  if (riskLevel === "CRITICAL") return "danger";
  if (riskLevel === "HIGH") return "warning";
  return "info";
}

function simulationEventKind(title: string) {
  if (title === "SIMULATION_COMPLETED") return "completed";
  if (title === "SIMULATION_STARTED") return "started";
  return "step";
}

function simulationEventIcon(title: string) {
  if (title === "SIMULATION_COMPLETED") return "✓";
  if (title === "SIMULATION_STARTED") return "▶";
  return "↗";
}

export function SimulationPage({ refreshKey }: SimulationPageProps) {
  const demoVersion = useDemoRefresh();
  const [snapshot, setSnapshot] = useState<SimulationSnapshot | null>(null);
  const [datasets, setDatasets] = useState<SimulationDatasetSummary[]>([]);
  const [baseMapPoints, setBaseMapPoints] = useState<SimulationMapPoint[]>([]);
  const [portsForMap, setPortsForMap] = useState<PortSummary[]>([]);
  const [selectedPortIdForMap, setSelectedPortIdForMap] = useState("");
  const selectedPortIdRef = useRef("");
  const showAllPortsRef = useRef(true);
  const [mapZones, setMapZones] = useState<PortZone[]>([]);
  const [selectedDatasetIds, setSelectedDatasetIds] = useState<string[]>([]);
  const [result, setResult] = useState<SimulationResult | null>(null);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [editingDatasetId, setEditingDatasetId] = useState<string | null>(null);
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
  const [runningDatasetIndex, setRunningDatasetIndex] = useState(0);
  const [savingDataset, setSavingDataset] = useState(false);
  const [datasetActionId, setDatasetActionId] = useState<string | null>(null);

  useEffect(() => {
    void getSimulationSnapshot().then(setSnapshot);
  }, [demoVersion, refreshKey]);

  useEffect(() => {
    let active = true;
    getPorts()
      .then((ports) => {
        if (active) setPortsForMap(ports);
      })
      .catch(() => {
        if (active) setPortsForMap([]);
      });

    return () => {
      active = false;
    };
  }, []);

  const handleSelectPort = useCallback(async (portId: string) => {
    showAllPortsRef.current = false;
    selectedPortIdRef.current = portId;
    setSelectedPortIdForMap(portId);
    setMapZones(await getPortZones(portId));
  }, []);

  const handleResetSelection = useCallback(() => {
    showAllPortsRef.current = true;
    selectedPortIdRef.current = "";
    setSelectedPortIdForMap("");
    setMapZones([]);
  }, []);

  useEffect(() => {
    let active = true;
    getSimulationDatasets()
      .then((items) => {
        if (!active) return;
        setDatasets(items);
        setSelectedDatasetIds((current) => {
          const available = new Set(items.map((item) => item.datasetId));
          const kept = current.filter((id) => available.has(id));
          return kept.length ? kept : items[0]?.datasetId ? [items[0].datasetId] : [];
        });
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
        if (!active) return;
        setFormZones(zones);
        setDatasetForm((value) => {
          const currentSnapshot = value.snapshots[0];
          if (!zones[0] || currentSnapshot.zoneId) {
            return value;
          }

          return {
            ...value,
            snapshots: [{ ...currentSnapshot, zoneId: zones[0].zoneId }]
          };
        });
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

  async function handleSaveDataset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSavingDataset(true);
    setSaveError("");
    try {
      const saved = editingDatasetId
        ? await updateSimulationDataset(editingDatasetId, datasetForm)
        : await createSimulationDataset(datasetForm);
      setDatasets((current) => [saved, ...current.filter((item) => item.datasetId !== saved.datasetId)]);
      setSelectedDatasetIds([saved.datasetId]);
      setSaveMessage(editingDatasetId ? "Đã cập nhật dữ liệu mô phỏng" : "Đã lưu dữ liệu mô phỏng");
      setCreateModalOpen(false);
      setEditingDatasetId(null);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Không lưu được dữ liệu mô phỏng");
    } finally {
      setSavingDataset(false);
    }
  }

  async function handleEditDataset(datasetId: string) {
    setDatasetActionId(datasetId);
    setSaveMessage("");
    setSaveError("");
    try {
      const dataset = await getSimulationDataset(datasetId);
      setDatasetForm({
        description: dataset.description,
        name: dataset.name,
        portCode: dataset.portCode,
        snapshots: dataset.snapshots.length ? dataset.snapshots : datasetForm.snapshots
      });
      setEditingDatasetId(datasetId);
      setCreateModalOpen(true);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Không tải được dữ liệu mô phỏng");
    } finally {
      setDatasetActionId(null);
    }
  }

  async function handleDeleteDataset(dataset: SimulationDatasetSummary) {
    if (!window.confirm(`Xóa bộ dữ liệu "${dataset.name}"?`)) return;

    setDatasetActionId(dataset.datasetId);
    setSaveMessage("");
    setSaveError("");
    try {
      await deleteSimulationDataset(dataset.datasetId);
      setDatasets((current) => current.filter((item) => item.datasetId !== dataset.datasetId));
      setSelectedDatasetIds((current) => current.filter((id) => id !== dataset.datasetId));
      setSaveMessage("Đã xóa dữ liệu mô phỏng");
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Không xóa được dữ liệu mô phỏng");
    } finally {
      setDatasetActionId(null);
    }
  }

  async function handleRunDataset() {
    if (selectedDatasetIds.length === 0) return;
    setRunning(true);
    setRunningDatasetIndex(0);
    try {
      let latestRun = null as Awaited<ReturnType<typeof runSimulationDataset>> | null;
      for (const [index, datasetId] of selectedDatasetIds.entries()) {
        setRunningDatasetIndex(index + 1);
        latestRun = await runSimulationDataset(datasetId);
        setSnapshot(await getSimulationSnapshot());
      }
      if (latestRun) {
        setResult(await getSimulationResult(latestRun.sessionId));
      }
    } finally {
      setRunning(false);
      setRunningDatasetIndex(0);
    }
  }

  async function handleRunStormDemo() {
    setRunning(true);
    setSaveError("");
    setResult(null);
    try {
      const run = await runDemoSimulation();
      setSnapshot(await getSimulationSnapshot());
      if (run?.sessionId) setResult(await getSimulationResult(run.sessionId));
      setSaveMessage("Đã tạo tình huống rủi ro cao cùng cảnh báo và nhiệm vụ ứng phó.");
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Chưa thể chạy kịch bản mẫu. Vui lòng thử lại.");
    } finally {
      setRunning(false);
    }
  }

  function toggleDatasetSelection(datasetId: string) {
    setSelectedDatasetIds((current) => (
      current.includes(datasetId)
        ? current.filter((id) => id !== datasetId)
        : [...current, datasetId]
    ));
  }

  const firstSnapshot = datasetForm.snapshots[0];
  const fallbackPoints = portsForMap
    .filter((port) => Number.isFinite(port.latitude) && Number.isFinite(port.longitude))
    .map((port) => ({
      latitude: port.latitude ?? 0,
      longitude: port.longitude ?? 0,
      riskLevel: "LOW" as const,
      zoneId: port.portId,
      zoneName: port.portName
    }));
  const mapPoints = result?.mapPoints.length ? result.mapPoints : baseMapPoints.length ? baseMapPoints : fallbackPoints;

  return (
    <section className="page-grid simulation-page">
      <div className="section-heading">
        <div>
          <h2>Mô phỏng tình huống vận hành</h2>
          <p>Thử các tình huống thời tiết để xem hệ thống đánh giá, cảnh báo và giao nhiệm vụ ứng phó.</p>
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
            <h3>Diễn biến thời tiết tại Cảng Tiên Sa</h3>
            <p>
              Hệ thống lần lượt tăng mức gió, lượng mưa và giảm tầm nhìn để kiểm tra khả năng đánh giá rủi ro,
              phát cảnh báo và đề xuất nhiệm vụ ứng phó.
            </p>
          </div>
          <Badge tone={riskTone(snapshot.currentRiskLevel)}>{riskLabel(snapshot.currentRiskLevel)}</Badge>
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
          <small>Cấp gió {snapshot.beaufortNumber}</small>
        </article>
        <article className="card card-pad sim-kpi-card">
          <span>Mưa 1 giờ</span>
          <strong>{snapshot.rainfall1hMm.toFixed(1)} mm</strong>
          <small>Tầm nhìn {snapshot.visibilityKm.toFixed(1)} km</small>
        </article>
        <article className="card card-pad sim-kpi-card">
          <span>Chế độ</span>
          <strong>{operationModeLabel(snapshot.currentMode)}</strong>
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
              <p>Tạo một kịch bản thời tiết riêng để kiểm tra cách hệ thống phản ứng.</p>
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
              <span>Mã khu vực</span>
              <input
                aria-label="Mã khu vực"
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
            <div className="card-head-actions">
              <button
                type="button"
                className="button button-secondary button-small"
                onClick={handleResetSelection}
                disabled={!selectedPortIdForMap}
              >
                Hiển thị tất cả cảng
              </button>
              <Badge tone={mapPoints.length ? "info" : "muted"}>{mapPoints.length} điểm</Badge>
            </div>
          </div>
          <SimulationMap
            onResetSelection={handleResetSelection}
            onSelectPort={(portId) => { void handleSelectPort(portId); }}
            ports={portsForMap}
            points={mapPoints}
            running={running || snapshot.status === "RUNNING"}
            selectedPortId={selectedPortIdForMap}
            zones={mapZones}
          />
          <div className="simulation-map-actions">
            <button
              className="button button-secondary"
              onClick={() => {
                setSaveMessage("");
                setSaveError("");
                setEditingDatasetId(null);
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
        <div className="simulation-result-modal" role="dialog" aria-label={editingDatasetId ? "Chỉnh sửa dữ liệu mô phỏng" : "Tạo dữ liệu mô phỏng"}>
          <article className="card card-pad simulation-result-card">
            <div className="card-head">
              <div>
                <h3>{editingDatasetId ? "Chỉnh sửa dữ liệu mô phỏng" : "Tạo dữ liệu mô phỏng"}</h3>
                <p>Nhập các chỉ số thời tiết để tạo một tình huống kiểm tra mới.</p>
              </div>
              <button className="button button-secondary button-small" onClick={() => { setCreateModalOpen(false); setEditingDatasetId(null); }} type="button">Đóng</button>
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
                <select
                  aria-label="Mã cảng"
                  onChange={(event) => setDatasetForm((value) => ({ ...value, portCode: event.target.value, snapshots: [{ ...firstSnapshot, zoneId: null }] }))}
                  required
                  value={datasetForm.portCode}
                >
                  {portsForMap.map((port) => (
                    <option key={port.portId} value={port.portCode}>{port.portCode} - {port.portName}</option>
                  ))}
                </select>
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
                <span>Mã khu vực</span>
                <select
                  aria-label="Mã khu vực"
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
                <button className="button button-secondary" disabled={savingDataset} type="button" onClick={() => { setCreateModalOpen(false); setEditingDatasetId(null); }}>Hủy</button>
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
            <p>Chọn tình huống đã lưu hoặc chạy nhanh kịch bản bão mẫu.</p>
          </div>
          <div className="field-label">Tình huống đã lưu</div>
          <div className="simulation-dataset-checklist" aria-label="Bộ dữ liệu">
            {datasets.length === 0 ? (
              <div className="simulation-dataset-empty">Chưa có dữ liệu</div>
            ) : null}
            {datasets.map((dataset) => (
              <div className="simulation-dataset-option" key={dataset.datasetId}>
                <label className="simulation-dataset-check">
                  <input
                    checked={selectedDatasetIds.includes(dataset.datasetId)}
                    disabled={isRunning}
                    onChange={() => toggleDatasetSelection(dataset.datasetId)}
                    type="checkbox"
                  />
                  <span>
                    <strong>{dataset.name}</strong>
                    <small>{dataset.portCode} · {dataset.snapshotCount} thời điểm</small>
                  </span>
                </label>
                <div className="simulation-dataset-actions">
                  <button
                    className="button button-secondary button-small"
                    disabled={isRunning || datasetActionId === dataset.datasetId}
                    onClick={() => { void handleEditDataset(dataset.datasetId); }}
                    type="button"
                  >
                    Chỉnh sửa
                  </button>
                  <button
                    className="button button-danger button-small"
                    disabled={isRunning || datasetActionId === dataset.datasetId}
                    onClick={() => { void handleDeleteDataset(dataset); }}
                    type="button"
                  >
                    Xóa
                  </button>
                </div>
              </div>
            ))}
          </div>
          <p className="simulation-selection-hint">
            {selectedDatasetIds.length > 0
              ? `Đã chọn ${selectedDatasetIds.length} bộ dữ liệu${runningDatasetIndex ? ` · Đang chạy ${runningDatasetIndex}/${selectedDatasetIds.length}` : ""}`
              : "Chưa chọn bộ dữ liệu mô phỏng"}
          </p>
          <label className="field-label" htmlFor="simulation-speed">Tốc độ mô phỏng</label>
          <select className="input select-input" disabled={isRunning} id="simulation-speed">
            <option>1x - Thực tế</option>
            <option>2x - Nhanh</option>
            <option>5x - Demo</option>
          </select>
          <div className="sim-dataset-card sim-demo-callout">
            <strong>Cần xem đầy đủ cảnh báo và nhiệm vụ?</strong>
            <span>Chạy kịch bản mẫu để hệ thống tạo tình huống từ Thấp đến Rất cao.</span>
            <button
              className="button button-primary button-small"
              disabled={isRunning}
              onClick={() => { void handleRunStormDemo(); }}
              type="button"
            >
              Chạy kịch bản bão mẫu
            </button>
          </div>
          <button
            className="button button-secondary simulation-action"
            disabled={isRunning || selectedDatasetIds.length === 0}
            onClick={handleRunDataset}
            type="button"
          >
            Chạy tình huống đã chọn
          </button>
        </aside>

        <article className="card card-pad simulation-feed-panel">
          <div className="card-head">
            <div>
              <h3>Diễn biến mô phỏng</h3>
              <p>Theo dõi từng bước thay đổi và phản ứng của hệ thống.</p>
            </div>
            <Badge tone="muted">{snapshot.feed.length} sự kiện</Badge>
          </div>
          <div className="timeline simulation-event-timeline">
            {snapshot.feed.length === 0 ? (
              <div className="empty-state">
                <strong>Chưa có dữ liệu mô phỏng</strong>
                <span>Chọn bộ dữ liệu và bấm “Chạy dữ liệu đã chọn” để phát dữ liệu.</span>
              </div>
            ) : (
              snapshot.feed.map((item) => (
                <div className={`sim-event sim-event-${simulationEventKind(item.title)} risk-${item.riskLevel.toLowerCase()}`} key={`${item.title}-${item.happenedAt}`}>
                  <div aria-hidden="true" className="sim-event-rail">
                    <span>{simulationEventIcon(item.title)}</span>
                  </div>
                  <div className="sim-event-card">
                    <div className="sim-event-head">
                      <strong>{simulationEventLabels[item.title] ?? item.title}</strong>
                      <time>{item.happenedAt}</time>
                    </div>
                    <p>{simulationDetailLabel(item.detail)}</p>
                    <div className="sim-event-footer">
                      <span>Mức độ tại thời điểm này</span>
                      <Badge tone={riskTone(item.riskLevel)}>{riskLabel(item.riskLevel)}</Badge>
                    </div>
                  </div>
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
                <p>Tổng hợp khu vực cần chú ý và nhiệm vụ ứng phó được tạo tự động.</p>
              </div>
              <button className="button button-secondary button-small" onClick={() => setResult(null)} type="button">Đóng</button>
            </div>
            <div className="simulation-result-grid">
              <div>
                <h4>Khu vực cần ưu tiên</h4>
                {result.dangerousZones.map((zone) => (
                  <div className="result-row" key={zone.zoneId}>
                    <strong>{zone.zoneName}</strong>
                    <Badge tone={riskTone(zone.riskLevel)}>{riskLabel(zone.riskLevel)}</Badge>
                    <small>{zone.reason ?? "Vượt ngưỡng mô phỏng"}</small>
                  </div>
                ))}
              </div>
              <div>
                <h4>Nhiệm vụ cần thực hiện</h4>
                {result.tasks.map((task) => (
                  <div className="result-row" key={task.taskCode}>
                    <strong>{task.title}</strong>
                    <small>{task.taskCode} · {task.zoneName ?? "Toàn cảng"} · Mức {riskLabel(task.priority)}</small>
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
