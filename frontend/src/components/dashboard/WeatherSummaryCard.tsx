import type { WeatherSnapshot } from "../../types/dashboard";

export function WeatherSummaryCard({ beaufortNumber, summary }: { beaufortNumber: number | null; summary: WeatherSnapshot }) {
  const metrics = [
    ["Tốc độ gió", `${summary.windSpeedMs.toFixed(1)} m/s`],
    ["Beaufort", `Cấp ${beaufortNumber ?? "-"}`],
    ["Lượng mưa", `${summary.rainfall1hMm.toFixed(1)} mm/h`],
    ["Tầm nhìn", `${summary.visibilityKm.toFixed(1)} km`],
    ["Nhiệt độ", `${summary.temperatureC}°C`],
    ["Độ ẩm", `${summary.humidityPct}%`]
  ];

  return (
    <article className="card card-pad weather-card">
      <div className="card-head">
        <div>
          <h3>Thời tiết hiện tại</h3>
          <p>Cập nhật gần nhất từ OpenWeather</p>
        </div>
      </div>
      <div className="weather-grid">
        {metrics.map(([label, value]) => (
          <div className="weather-metric" key={label}>
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </div>
    </article>
  );
}
