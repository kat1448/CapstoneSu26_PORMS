import { useEffect, useMemo, useState } from "react";
import type { WeatherSnapshot } from "../../types/dashboard";
import { dataSourceLabel, weatherDescriptionLabel } from "../../utils/displayLabels";

type WeatherDataTableProps = {
  weather: WeatherSnapshot;
};

const PAGE_SIZE = 5;

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

  return new Intl.DateTimeFormat("vi-VN", {
    day: "2-digit",
    hour: "2-digit",
    hour12: false,
    minute: "2-digit",
    month: "2-digit",
    second: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric"
  }).format(date);
}

function formatWeatherDescription(weather: WeatherSnapshot) {
  const description = weather.weatherDescription?.trim();
  const code = weather.weatherCode;

  if (description && code !== null && code !== undefined) return `${weatherDescriptionLabel(description)} · ${code}`;
  if (description) return weatherDescriptionLabel(description);
  if (code !== null && code !== undefined) return String(code);
  return "Chưa có dữ liệu";
}

function formatPointWeatherDescription(description: string | null | undefined) {
  return weatherDescriptionLabel(description);
}

function formatCoordinate(latitude: number | null | undefined, longitude: number | null | undefined) {
  if (latitude === null || latitude === undefined || longitude === null || longitude === undefined) {
    return "Chưa có dữ liệu";
  }

  return `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
}

export function WeatherDataTable({ weather }: WeatherDataTableProps) {
  const [currentPage, setCurrentPage] = useState(1);
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
  const totalPages = Math.max(1, Math.ceil(points.length / PAGE_SIZE));
  const visiblePoints = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return points.slice(start, start + PAGE_SIZE);
  }, [currentPage, points]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  return (
    <article className="card card-pad weather-data-card">
      <div className="card-head">
        <div>
          <h3>Chi tiết thời tiết tại các khu vực</h3>
          <p>So sánh nhanh điều kiện gió, mưa, tầm nhìn và thời điểm cập nhật tại từng vị trí trong cảng.</p>
        </div>
      </div>

      <div className="weather-point-table-shell">
        <table aria-label="Chi tiết thời tiết tại các khu vực" className="weather-point-table">
          <thead>
            <tr>
              <th>Cảng</th>
              <th>Khu vực</th>
              <th>Tọa độ</th>
              <th>Ghi nhận lúc</th>
              <th>Hệ thống nhận lúc</th>
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
            {visiblePoints.map((point) => (
              <tr key={`${point.portCode}-${point.zoneName ?? "port"}-${point.observedAt ?? ""}`}>
                <td>
                  <strong>{point.portName}</strong>
                  <small>{point.portCode}</small>
                </td>
                <td>{point.zoneName || "Toàn cảng"}</td>
                <td>{formatCoordinate(point.latitude, point.longitude)}</td>
                <td>{formatTimestamp(point.observedAt)}</td>
                <td>{formatTimestamp(point.recordedAt)}</td>
                <td>{dataSourceLabel(point.dataSource)}</td>
                <td>{formatPointWeatherDescription(point.weatherDescription)}</td>
                <td>
                  <strong>{formatNumber(point.windSpeedMs, "m/s")}</strong>
                  <small>Cấp gió {point.beaufortNumber}</small>
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
      {totalPages > 1 ? (
        <div className="table-pagination" aria-label="Phân trang dữ liệu thời tiết">
          <button
            className="button button-secondary button-small"
            disabled={currentPage === 1}
            onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
            type="button"
          >
            Trước
          </button>
          <span>Trang {currentPage}/{totalPages}</span>
          <button
            className="button button-secondary button-small"
            disabled={currentPage === totalPages}
            onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
            type="button"
          >
            Sau
          </button>
        </div>
      ) : null}
    </article>
  );
}
