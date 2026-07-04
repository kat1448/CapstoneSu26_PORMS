import { useCallback, useEffect, useRef, useState } from "react";
import { AlertListCard } from "../components/dashboard/AlertListCard";
import { GisMapCard } from "../components/dashboard/GisMapCard";
import { ModeCard } from "../components/dashboard/ModeCard";
import { RiskHeroCard } from "../components/dashboard/RiskHeroCard";
import { WeatherDataTable } from "../components/dashboard/WeatherDataTable";
import { ZoneStatusCard } from "../components/dashboard/ZoneStatusCard";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getAlerts } from "../services/alertService";
import { getDashboardSummary, getWeatherSnapshot } from "../services/dashboardService";
import { getPorts, getPortZones } from "../services/portService";
import type { AlertItem } from "../types/alert";
import type { DashboardSummary, WeatherSnapshot } from "../types/dashboard";
import type { PortSummary, PortZone } from "../types/port";

export function DashboardPage({ refreshKey }: { refreshKey: number }) {
  useDemoRefresh();
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [weather, setWeather] = useState<WeatherSnapshot | null>(null);
  const [ports, setPorts] = useState<PortSummary[]>([]);
  const [selectedPortId, setSelectedPortId] = useState("");
  const selectedPortIdRef = useRef("");
  const showAllPortsRef = useRef(true);
  const [zones, setZones] = useState<PortZone[]>([]);

  const loadDashboard = useCallback(async () => {
    const [nextSummary, nextWeather, nextAlerts, nextPorts] = await Promise.all([
      getDashboardSummary(),
      getWeatherSnapshot(),
      getAlerts(),
      getPorts()
    ]);
    const availablePortIds = new Set(nextPorts.map((port) => port.portId));
    let nextSelectedPortId = selectedPortIdRef.current && availablePortIds.has(selectedPortIdRef.current)
      ? selectedPortIdRef.current
      : "";
    let nextZones: PortZone[] = [];

    if (showAllPortsRef.current) {
      nextSelectedPortId = "";
      nextZones = [];
    } else if (nextSelectedPortId) {
      nextZones = await getPortZones(nextSelectedPortId);
    }

    setSummary(nextSummary);
    setWeather(nextWeather);
    setAlerts(nextAlerts);
    setPorts(nextPorts);
    selectedPortIdRef.current = nextSelectedPortId;
    setSelectedPortId(nextSelectedPortId);
    setZones(nextZones);
  }, []);

  const handleSelectPort = useCallback(async (portId: string) => {
    showAllPortsRef.current = false;
    selectedPortIdRef.current = portId;
    setSelectedPortId(portId);
    setZones(await getPortZones(portId));
  }, []);

  const handleResetSelection = useCallback(() => {
    showAllPortsRef.current = true;
    selectedPortIdRef.current = "";
    setSelectedPortId("");
    setZones([]);
  }, []);

  useEffect(() => { void loadDashboard(); }, [loadDashboard, refreshKey]);

  useEffect(() => {
    const timer = window.setInterval(() => void loadDashboard(), 600_000);
    return () => window.clearInterval(timer);
  }, [loadDashboard]);

  if (!summary || !weather) {
    return <section className="page-grid"><article className="card loading-card">Đang tải dashboard...</article></section>;
  }

  const selectedPort = ports.find((port) => port.portId === selectedPortId);
  const mapPortName = selectedPort?.portName ?? (selectedPortId ? summary.portName : "Tất cả cảng");
  const zoneStatusPortId = selectedPortId || summary.portId;

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Trung tâm điều hành</h2>
          <p>Theo dõi trạng thái vận hành theo thời gian thực</p>
        </div>
      </div>
      <div className="dashboard-grid">
        <div className="dashboard-main" data-testid="dashboard-left">
          <div className="hero-grid">
            <RiskHeroCard summary={summary} weather={weather} />
            <ModeCard operationMode={summary.currentOperationMode} />
          </div>
          <GisMapCard
            onSelectPort={(portId) => { void handleSelectPort(portId); }}
            onResetSelection={handleResetSelection}
            portName={mapPortName}
            ports={ports}
            selectedPortId={selectedPortId}
            zones={zones}
          />
          <WeatherDataTable weather={weather} />
          <ZoneStatusCard portId={zoneStatusPortId} zones={zones} />
        </div>
        <div className="dashboard-side" data-testid="dashboard-right">
          <AlertListCard alerts={alerts} />
        </div>
      </div>
    </section>
  );
}
