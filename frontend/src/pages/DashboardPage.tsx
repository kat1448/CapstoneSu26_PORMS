import { useCallback, useEffect, useRef, useState } from "react";
import { AlertListCard } from "../components/dashboard/AlertListCard";
import { GisMapCard } from "../components/dashboard/GisMapCard";
import { WeatherDataTable } from "../components/dashboard/WeatherDataTable";
import { ZoneRiskOverviewCard } from "../components/dashboard/ZoneRiskOverviewCard";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getAlerts } from "../services/alertService";
import { getDashboardSummary, getWeatherSnapshot } from "../services/dashboardService";
import { getPorts, getPortZones } from "../services/portService";
import type { AlertItem } from "../types/alert";
import type { DashboardSummary, WeatherSnapshot } from "../types/dashboard";
import type { PortSummary, PortZone } from "../types/port";
import type { DemoUser } from "../App";

export function DashboardPage({ currentUser, refreshKey }: { currentUser: DemoUser; refreshKey: number }) {
  useDemoRefresh();
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [weather, setWeather] = useState<WeatherSnapshot | null>(null);
  const [ports, setPorts] = useState<PortSummary[]>([]);
  const [selectedPortId, setSelectedPortId] = useState("");
  const selectedPortIdRef = useRef("");
  const showAllPortsRef = useRef(true);
  const [zones, setZones] = useState<PortZone[]>([]);
  const [riskOverviewZones, setRiskOverviewZones] = useState<PortZone[]>([]);

  const loadDashboard = useCallback(async () => {
    const [nextSummary, nextWeather, nextAlerts, nextPorts] = await Promise.all([
      getDashboardSummary(),
      getWeatherSnapshot(),
      getAlerts(),
      getPorts()
    ]);
    const availablePortIds = new Set(nextPorts.map((port) => port.portId));
    const zoneGroups = await Promise.all(nextPorts.map((port) => getPortZones(port.portId)));
    let nextSelectedPortId = selectedPortIdRef.current && availablePortIds.has(selectedPortIdRef.current)
      ? selectedPortIdRef.current
      : "";
    let nextZones: PortZone[] = [];

    if (showAllPortsRef.current) {
      nextSelectedPortId = "";
      nextZones = [];
    } else if (nextSelectedPortId) {
      const selectedPortIndex = nextPorts.findIndex((port) => port.portId === nextSelectedPortId);
      nextZones = selectedPortIndex >= 0 ? zoneGroups[selectedPortIndex] : [];
    }

    setSummary(nextSummary);
    setWeather(nextWeather);
    setAlerts(nextAlerts);
    setPorts(nextPorts);
    setRiskOverviewZones(zoneGroups.flat());
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

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Trung tâm điều hành</h2>
          <p>Nắm bắt nhanh tình hình an toàn và hoạt động tại các cảng.</p>
        </div>
      </div>
      <div className="dashboard-grid">
        <div className="dashboard-main dashboard-main-full" data-testid="dashboard-left">
          <div className="hero-grid">
            <ZoneRiskOverviewCard zones={riskOverviewZones} />
            <AlertListCard alerts={alerts} />
          </div>
          <GisMapCard
            canManage={currentUser.role === "ADMIN"}
            onSelectPort={(portId) => { void handleSelectPort(portId); }}
            onResetSelection={handleResetSelection}
            portName={mapPortName}
            ports={ports}
            selectedPortId={selectedPortId}
            zones={zones}
          />
          <WeatherDataTable weather={weather} />
        </div>
      </div>
    </section>
  );
}
