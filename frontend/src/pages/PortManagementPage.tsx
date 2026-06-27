import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { PortTable } from "../components/port/PortTable";
import { ZoneList } from "../components/port/ZoneList";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { deletePortZone, getPortZones, getPorts, updatePortZone, type UpdateZoneInput } from "../services/portService";
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

  async function loadZones(selectedPortId: string) {
    setZones(await getPortZones(selectedPortId));
  }

  useEffect(() => {
    void getPorts().then(setPorts);
  }, [refreshKey]);

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
      <ZoneList onDeleteZone={handleDeleteZone} onUpdateZone={handleUpdateZone} zones={zones} />
    </section>
  );
}
