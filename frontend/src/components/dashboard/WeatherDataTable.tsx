import type { WeatherSnapshot } from "../../types/dashboard";

type WeatherDataTableProps = {
  weather: WeatherSnapshot;
};

function formatNumber(value: number | null | undefined, suffix: string, digits = 1) {
  if (value === null || value === undefined) return "Chưa có dữ liệu";
  return `${value.toFixed(digits)} ${suffix}`;
}

function formatInteger(value: number | null | undefined, suffix = "") {
  if (value === null || value === undefined) return "Chưa có dữ liệu";
  return `${value}${suffix}`;
}

function formatTimestamp(value: string | null | undefined) {
  if (!value) return "Chưa có dữ liệu";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  const pad = (part: number) => String(part).padStart(2, "0");
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function formatWeatherDescription(weather: WeatherSnapshot) {
  const description = weather.weatherDescription?.trim();
  const code = weather.weatherCode;

  if (description && code !== null && code !== undefined) return `${description} · ${code}`;
  if (description) return description;
  if (code !== null && code !== undefined) return String(code);
  return "Chưa có dữ liệu";
}

export function WeatherDataTable({ weather }: WeatherDataTableProps) {
  const rows = [
    ["Nguồn dữ liệu", weather.dataSource ?? "Chưa có dữ liệu"],
    ["Thời điểm quan trắc", formatTimestamp(weather.observedAt)],
    ["Thời điểm ghi nhận", formatTimestamp(weather.recordedAt)],
    ["Mô tả thời tiết", formatWeatherDescription(weather)],
    ["Tốc độ gió", formatNumber(weather.windSpeedMs, "m/s")],
    ["Gió giật", formatNumber(weather.windGustMs, "m/s")],
    ["Hướng gió", formatInteger(weather.windDirectionDeg, "°")],
    ["Beaufort", formatInteger(weather.beaufortNumber, "")],
    ["Lượng mưa 1h", formatNumber(weather.rainfall1hMm, "mm/h")],
    ["Tầm nhìn", formatNumber(weather.visibilityKm, "km")],
    ["Nhiệt độ", formatNumber(weather.temperatureC, "°C", 0)],
    ["Độ ẩm", formatInteger(weather.humidityPct, "%")],
    ["Áp suất", formatNumber(weather.pressureHpa, "hPa", 0)]
  ];

  return (
    <article className="card card-pad weather-data-card">
      <div className="card-head">
        <div>
          <h3>Bảng dữ liệu OpenWeather</h3>
          <p>Cập nhật tự động theo dữ liệu OpenWeather mới nhất</p>
        </div>
      </div>
      <div className="weather-data-table" aria-label="Bảng dữ liệu OpenWeather">
        {rows.map(([label, value]) => (
          <div className="weather-data-row" key={label}>
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </div>
    </article>
  );
}
