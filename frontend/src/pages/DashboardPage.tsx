import { useCallback, useEffect, useState } from "react";
import { AlertListCard } from "../components/dashboard/AlertListCard";
import { GisMapCard } from "../components/dashboard/GisMapCard";
import { ModeCard } from "../components/dashboard/ModeCard";
import { RiskHeroCard } from "../components/dashboard/RiskHeroCard";
import { RiskTrendChart } from "../components/dashboard/RiskTrendChart";
import { WeatherDataTable } from "../components/dashboard/WeatherDataTable";
import { WeatherSummaryCard } from "../components/dashboard/WeatherSummaryCard";
import { ZoneStatusCard } from "../components/dashboard/ZoneStatusCard";
import { useDemoRefresh } from "../hooks/useDemoRefresh";
import { getAlerts } from "../services/alertService";
import { getDashboardSummary, getRiskTrend, getWeatherSnapshot } from "../services/dashboardService";
import { getPortZones } from "../services/portService";
import type { AlertItem } from "../types/alert";
import type { DashboardSummary, RiskTrendPoint, WeatherSnapshot } from "../types/dashboard";
import type { PortZone } from "../types/port";

export function DashboardPage({ refreshKey }: { refreshKey: number }) {
  useDemoRefresh();
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [trend, setTrend] = useState<RiskTrendPoint[]>([]);
  const [weather, setWeather] = useState<WeatherSnapshot | null>(null);
  const [zones, setZones] = useState<PortZone[]>([]);

  const loadDashboard = useCallback(async () => {
    const [nextSummary, nextWeather, nextTrend, nextAlerts] = await Promise.all([
      getDashboardSummary(),
      getWeatherSnapshot(),
      getRiskTrend(),
      getAlerts()
    ]);
    const nextZones = await getPortZones(nextSummary.portId);
    setSummary(nextSummary);
    setWeather(nextWeather);
    setTrend(nextTrend);
    setAlerts(nextAlerts);
    setZones(nextZones);
  }, []);

  useEffect(() => { void loadDashboard(); }, [loadDashboard, refreshKey]);

  useEffect(() => {
    const timer = window.setInterval(() => void loadDashboard(), 600_000);
    return () => window.clearInterval(timer);
  }, [loadDashboard]);

  if (!summary || !weather) {
    return <section className="page-grid"><article className="card loading-card">Đang tải dashboard...</article></section>;
  }

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
          <GisMapCard portName={summary.portName} zones={zones} />
          <RiskTrendChart currentRiskLevel={summary.currentRiskLevel} points={trend} />
          <ZoneStatusCard portId={summary.portId} zones={zones} />
        </div>
        <div className="dashboard-side" data-testid="dashboard-right">
          <WeatherSummaryCard beaufortNumber={summary.beaufortNumber} summary={weather} />
          <WeatherDataTable weather={weather} />
          <AlertListCard alerts={alerts} />
        </div>
      </div>
    </section>
  );
}
