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

function formatPointWeatherDescription(description: string | null | undefined) {
  return description?.trim() || "Chưa có dữ liệu";
}

function formatCoordinate(latitude: number | null | undefined, longitude: number | null | undefined) {
  if (latitude === null || latitude === undefined || longitude === null || longitude === undefined) {
    return "Chưa có dữ liệu";
  }

  return `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
}

export function WeatherDataTable({ weather }: WeatherDataTableProps) {
  const points = weather.dataPoints?.length ? weather.dataPoints : [{
    beaufortNumber: weather.beaufortNumber ?? 0,
    dataSource: weather.dataSource,
    latitude: null,
    longitude: null,
    observedAt: weather.observedAt,
    portCode: "Chưa có dữ liệu",
    portName: "Chưa có dữ liệu",
    rainfall1hMm: weather.rainfall1hMm,
    recordedAt: weather.recordedAt,
    temperatureC: weather.temperatureC,
    humidityPct: weather.humidityPct,
    visibilityKm: weather.visibilityKm,
    weatherDescription: formatWeatherDescription(weather),
    windSpeedMs: weather.windSpeedMs,
    zoneName: "Toàn cảng"
  }];

  return (
    <article className="card card-pad weather-data-card">
      <div className="card-head">
        <div>
          <h3>Dữ liệu thời tiết theo cảng và khu vực</h3>
          <p>Mỗi dòng thể hiện dữ liệu OpenWeather gắn với cảng, khu vực, vị trí và thời gian cập nhật.</p>
        </div>
      </div>

      <div className="weather-point-table-shell">
        <table aria-label="Dữ liệu thời tiết theo cảng và khu vực" className="weather-point-table">
          <thead>
            <tr>
              <th>Cảng</th>
              <th>Khu vực</th>
              <th>Vị trí</th>
              <th>Quan trắc lúc</th>
              <th>Cập nhật lúc</th>
              <th>Nguồn</th>
              <th>Thời tiết</th>
              <th>Gió</th>
              <th>Mưa</th>
              <th>Tầm nhìn</th>
              <th>Nhiệt độ</th>
              <th>Độ ẩm</th>
            </tr>
          </thead>
          <tbody>
            {points.map((point) => (
              <tr key={`${point.portCode}-${point.zoneName ?? "port"}-${point.observedAt ?? ""}`}>
                <td>
                  <strong>{point.portName}</strong>
                  <small>{point.portCode}</small>
                </td>
                <td>{point.zoneName || "Toàn cảng"}</td>
                <td>{formatCoordinate(point.latitude, point.longitude)}</td>
                <td>{formatTimestamp(point.observedAt)}</td>
                <td>{formatTimestamp(point.recordedAt)}</td>
                <td>{point.dataSource || "Chưa có dữ liệu"}</td>
                <td>{formatPointWeatherDescription(point.weatherDescription)}</td>
                <td>
                  <strong>{formatNumber(point.windSpeedMs, "m/s")}</strong>
                  <small>Beaufort {point.beaufortNumber}</small>
                </td>
                <td>{formatNumber(point.rainfall1hMm, "mm/h")}</td>
                <td>{formatNumber(point.visibilityKm, "km")}</td>
                <td>{formatNumber(point.temperatureC, "°C", 0)}</td>
                <td>{formatInteger(point.humidityPct, "%")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </article>
  );
}
