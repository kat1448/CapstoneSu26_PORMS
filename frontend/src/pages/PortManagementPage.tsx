import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { PortTable } from "../components/port/PortTable";
import { ZoneList } from "../components/port/ZoneList";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { deletePortZone, getPortZones, getPorts, updatePort, updatePortZone, type UpdatePortInput, type UpdateZoneInput } from "../services/portService";
import type { PortSummary, PortZone } from "../types/port";

type PortManagementPageProps = {
  detailMode?: boolean;
  refreshKey: number;
};

export function PortManagementPage({ detailMode = false, refreshKey }: PortManagementPageProps) {
  useDemoRefresh();
  const { portId } = useParams();
  const [ports, setPorts] = useState<PortSummary[]>([]);
  const [zones, setZones] = useState<PortZone[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [portForm, setPortForm] = useState<UpdatePortInput | null>(null);
  const [submittingPort, setSubmittingPort] = useState(false);

  async function loadZones(selectedPortId: string) {
    setZones(await getPortZones(selectedPortId));
  }

  useEffect(() => {
    void getPorts().then((items) => {
      setPorts(items);
      if (!portId) {
        setPortForm(null);
        return;
      }

      const selected = items.find((item) => item.portId === portId) ?? null;
      setPortForm(selected ? {
        address: null,
        code: selected.portCode,
        isActive: selected.isActive,
        latitude: Number(selected.latitude ?? 0),
        longitude: Number(selected.longitude ?? 0),
        name: selected.portName,
        timezone: "Asia/Ho_Chi_Minh",
        weatherSource: "OPENWEATHER",
        weatherStationId: null
      } : null);
    });
  }, [portId, refreshKey]);

  useEffect(() => {
    if (!portId) {
      setZones([]);
      return;
    }

    void loadZones(portId);
  }, [portId, refreshKey]);

  async function handleUpdateZone(zone: PortZone, input: UpdateZoneInput) {
    if (!portId) return;

    setError(null);
    setMessage(null);
    try {
      await updatePortZone(portId, zone.zoneId, input);
      setMessage("Đã cập nhật khu vực.");
      await loadZones(portId);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể cập nhật khu vực.");
      throw caught;
    }
  }

  async function handleDeleteZone(zone: PortZone) {
    if (!portId || !window.confirm(`Xóa khu vực ${zone.zoneName}?`)) {
      return;
    }

    setError(null);
    setMessage(null);
    try {
      await deletePortZone(portId, zone.zoneId);
      setMessage("Đã xoá khu vực.");
      await loadZones(portId);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể xoá khu vực.");
    }
  }

  async function handleSavePort(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!portId || !portForm) {
      return;
    }

    setError(null);
    setMessage(null);
    setSubmittingPort(true);
    try {
      const updated = await updatePort(portId, portForm);
      setMessage("Đã cập nhật thông tin cảng.");
      setPorts((items) => items.map((item) => item.portId === portId ? updated : item));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể cập nhật cảng.");
    } finally {
      setSubmittingPort(false);
    }
  }

  if (!detailMode) {
    return (
      <section className="page-grid">
        <div className="section-heading">
          <div>
            <h2>Cảng & khu vực</h2>
            <p>Quản lý thông tin và trạng thái vận hành các cảng</p>
          </div>
          <Link className="button button-primary" to="/ports/new">
            Thêm cảng
          </Link>
        </div>
        <PortTable ports={ports} />
      </section>
    );
  }

  const selectedPort = ports.find((item) => item.portId === portId);
  if (!selectedPort) {
    return (
      <section className="page-grid">
        <article className="card loading-card">Không tìm thấy cảng.</article>
      </section>
    );
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>{selectedPort.portName}</h2>
          <p>{selectedPort.portCode} · chi tiết khu vực</p>
        </div>
        <Link className="button button-secondary" to="/ports">
          Quay lại
        </Link>
      </div>
      {error ? <div className="form-error" role="alert">{error}</div> : null}
      {message ? <div className="form-success" role="status">{message}</div> : null}
      <article className="card card-pad">
        <form className="port-create-form" onSubmit={handleSavePort}>
          <div className="port-form-grid">
            <label>
              <span>Mã cảng</span>
              <input aria-label="Mã cảng" onChange={(event) => setPortForm((value) => value ? { ...value, code: event.target.value } : value)} required value={portForm?.code ?? ""} />
            </label>
            <label>
              <span>Tên cảng</span>
              <input aria-label="Tên cảng" onChange={(event) => setPortForm((value) => value ? { ...value, name: event.target.value } : value)} required value={portForm?.name ?? ""} />
            </label>
            <label>
              <span>Địa chỉ</span>
              <input aria-label="Địa chỉ" onChange={(event) => setPortForm((value) => value ? { ...value, address: event.target.value } : value)} value={portForm?.address ?? ""} />
            </label>
            <label>
              <span>Latitude cảng</span>
              <input aria-label="Latitude cảng" onChange={(event) => setPortForm((value) => value ? { ...value, latitude: Number(event.target.value) } : value)} required step="0.000001" type="number" value={portForm?.latitude ?? ""} />
            </label>
            <label>
              <span>Longitude cảng</span>
              <input aria-label="Longitude cảng" onChange={(event) => setPortForm((value) => value ? { ...value, longitude: Number(event.target.value) } : value)} required step="0.000001" type="number" value={portForm?.longitude ?? ""} />
            </label>
            <label>
              <span>Timezone</span>
              <input aria-label="Timezone" onChange={(event) => setPortForm((value) => value ? { ...value, timezone: event.target.value } : value)} required value={portForm?.timezone ?? ""} />
            </label>
            <label>
              <span>Nguồn thời tiết</span>
              <input aria-label="Nguồn thời tiết" onChange={(event) => setPortForm((value) => value ? { ...value, weatherSource: event.target.value } : value)} required value={portForm?.weatherSource ?? ""} />
            </label>
            <label>
              <span>Mã trạm thời tiết</span>
              <input aria-label="Mã trạm thời tiết" onChange={(event) => setPortForm((value) => value ? { ...value, weatherStationId: event.target.value } : value)} value={portForm?.weatherStationId ?? ""} />
            </label>
            <label className="checkbox-field">
              <input checked={portForm?.isActive ?? false} onChange={(event) => setPortForm((value) => value ? { ...value, isActive: event.target.checked } : value)} type="checkbox" />
              <span>Đang hoạt động</span>
            </label>
          </div>

          <div className="form-actions">
            <button className="button button-primary" disabled={submittingPort} type="submit">
              {submittingPort ? "Đang lưu..." : "Lưu thông tin cảng"}
            </button>
          </div>
        </form>
      </article>
      <ZoneList onDeleteZone={handleDeleteZone} onUpdateZone={handleUpdateZone} zones={zones} />
    </section>
  );
}
