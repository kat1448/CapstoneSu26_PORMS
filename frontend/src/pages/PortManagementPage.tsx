import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { PortTable } from "../components/port/PortTable";
import { ZoneList } from "../components/port/ZoneList";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getPorts, getPortZones } from "../services/portService";
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

  useEffect(() => {
    void getPorts().then(setPorts);
  }, [refreshKey]);

  useEffect(() => {
    if (!portId) {
      setZones([]);
      return;
    }

    void getPortZones(portId).then(setZones);
  }, [portId, refreshKey]);

  if (!detailMode) {
    return (
      <section className="page-grid">
        <div className="section-heading">
          <div>
            <h2>Cảng & khu vực</h2>
            <p>Quản lý thông tin và trạng thái vận hành các cảng</p>
          </div>
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
      <ZoneList zones={zones} />
    </section>
  );
}
