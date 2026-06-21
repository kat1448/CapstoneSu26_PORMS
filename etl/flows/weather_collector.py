import time
from datetime import datetime, timezone, timedelta
from typing import Optional

import httpx
import structlog
from sqlalchemy import text
from prefect import flow, task, get_run_logger
from prefect.tasks import task_input_hash

from config import settings
from db.connection import get_operational_session
from models.weather import WeatherReadingModel
from utils.beaufort import ms_to_beaufort
from utils.db_ops import log_operation_event

logger = structlog.get_logger()


@task(
    name="load-active-ports",
    retries=3,
    retry_delay_seconds=5,
    cache_key_fn=task_input_hash,
    cache_expiration=timedelta(minutes=5),
)
def task_load_active_ports() -> list[dict]:
    with get_operational_session() as session:
        result = session.execute(text("""
            SELECT id::text, code, name, latitude, longitude
            FROM operational.ports
            WHERE is_active = TRUE
            ORDER BY code
        """))
        ports = [dict(row._mapping) for row in result.fetchall()]

    logger.info("loaded_active_ports", count=len(ports),
                codes=[p["code"] for p in ports])
    return ports


@task(
    name="fetch-openweather",
    retries=3,
    retry_delay_seconds=[1, 2, 4],
    timeout_seconds=15,
)
def task_fetch_from_openweather(port: dict) -> Optional[dict]:
    """Gọi OpenWeather Current Weather API với retry/backoff."""
    url = f"{settings.OW_BASE_URL}/weather"
    params = {
        "lat": port["latitude"],
        "lon": port["longitude"],
        "appid": settings.OPENWEATHER_API_KEY,
        "units": "metric",
    }

    try:
        with httpx.Client(timeout=10.0) as client:
            response = client.get(url, params=params)
            response.raise_for_status()
            raw_data = response.json()

            logger.info("openweather_fetch_success", port_code=port["code"])
            return raw_data

    except httpx.HTTPStatusError as e:
        logger.error("openweather_http_error", port_code=port["code"],
                     status_code=e.response.status_code, error=str(e))
        raise

    except httpx.TimeoutException:
        logger.error("openweather_timeout", port_code=port["code"])
        raise


@task(name="normalize-weather")
def task_normalize_weather(raw_json: dict, port_id: str) -> WeatherReadingModel:
    """Chuyển đổi raw JSON từ OpenWeather sang WeatherReadingModel chuẩn."""
    wind_speed_ms = float(raw_json.get("wind", {}).get("speed", 0))
    beaufort = ms_to_beaufort(wind_speed_ms)

    visibility_m = raw_json.get("visibility")
    visibility_km = round(visibility_m / 1000, 2) if visibility_m is not None else None

    rain = raw_json.get("rain", {})
    rainfall_1h = float(rain.get("1h", 0))
    rainfall_3h = float(rain.get("3h", 0)) if "3h" in rain else None

    observed_at = datetime.fromtimestamp(raw_json["dt"], tz=timezone.utc)
    weather_info = raw_json.get("weather", [{}])[0]

    model = WeatherReadingModel(
        port_id=port_id,
        wind_speed_ms=wind_speed_ms,
        beaufort_number=beaufort,
        wind_direction_deg=raw_json.get("wind", {}).get("deg"),
        wind_gust_ms=raw_json.get("wind", {}).get("gust"),
        rainfall_1h_mm=rainfall_1h,
        rainfall_3h_mm=rainfall_3h,
        temperature_c=raw_json.get("main", {}).get("temp"),
        humidity_pct=raw_json.get("main", {}).get("humidity"),
        visibility_km=visibility_km,
        pressure_hpa=raw_json.get("main", {}).get("pressure"),
        weather_code=weather_info.get("id"),
        weather_description=weather_info.get("description"),
        observed_at=observed_at,
        raw_payload=raw_json,
        is_simulation=False,
    )

    logger.info("weather_normalized", port_id=port_id,
                wind_speed_ms=wind_speed_ms, beaufort=beaufort,
                rainfall_1h=rainfall_1h, visibility_km=visibility_km)
    return model


@task(name="save-weather-reading", retries=3, retry_delay_seconds=2)
def task_save_weather_reading(model: WeatherReadingModel) -> Optional[str]:
    """INSERT vào operational.weather_readings. IDEMPOTENT: ON CONFLICT DO NOTHING."""
    with get_operational_session() as session:
        result = session.execute(text("""
            INSERT INTO operational.weather_readings (
                port_id, wind_speed_ms, beaufort_number, wind_direction_deg, wind_gust_ms,
                rainfall_1h_mm, rainfall_3h_mm,
                temperature_c, humidity_pct, visibility_km, pressure_hpa,
                weather_code, weather_description,
                observed_at, recorded_at, data_source, raw_payload, is_simulation
            ) VALUES (
                CAST(:port_id AS uuid), :wind_speed_ms, :beaufort_number,
                :wind_direction_deg, :wind_gust_ms,
                :rainfall_1h_mm, :rainfall_3h_mm,
                :temperature_c, :humidity_pct, :visibility_km, :pressure_hpa,
                :weather_code, :weather_description,
                :observed_at, NOW(), 'OPENWEATHER_API', CAST(:raw_payload AS jsonb), FALSE
            )
            ON CONFLICT DO NOTHING
            RETURNING id::text
        """), model.to_db_dict())

        reading_id = result.scalar()
        session.commit()
        return reading_id


@task(name="trigger-risk-engine", retries=2, retry_delay_seconds=3)
def task_trigger_risk_engine(port_id: str, reading_id: str) -> bool:
    """
    Trigger Risk Engine trên ASP.NET.
    Không raise exception nếu fail — Risk Engine có BackgroundService backup.
    """
    if not reading_id:
        return False

    try:
        with httpx.Client(timeout=5.0) as client:
            response = client.post(
                settings.BACKEND_TRIGGER_URL,
                json={"portId": port_id, "weatherReadingId": reading_id},
                headers={"X-Internal-Key": settings.INTERNAL_API_KEY},
            )
            response.raise_for_status()
            logger.info("risk_engine_triggered", port_id=port_id, reading_id=reading_id)
            return True
    except Exception as e:
        logger.warning("risk_engine_trigger_failed", port_id=port_id, error=str(e))
        return False


@flow(
    name="weather-collector",
    description="Thu thập dữ liệu thời tiết từ OpenWeather API cho tất cả cảng active",
    version="1.0.0",
    log_prints=True,
)
def weather_collector_flow():
    """Flow chính: chạy mỗi 15 phút."""
    try:
        flow_run_id = str(get_run_logger().extra.get("flow_run_id", "unknown"))
    except Exception:
        flow_run_id = "local"

    logger.info("weather_collector_started", flow_run_id=flow_run_id)

    ports = task_load_active_ports()
    if not ports:
        logger.warning("no_active_ports_found")
        return

    results = {"success": 0, "failed": 0, "skipped": 0}

    for port in ports:
        try:
            port_id = port["id"]

            raw_json = task_fetch_from_openweather(port)
            model = task_normalize_weather(raw_json, port_id)
            reading_id = task_save_weather_reading(model)

            if reading_id:
                results["success"] += 1
                task_trigger_risk_engine(port_id, reading_id)
            else:
                results["skipped"] += 1

        except Exception as e:
            results["failed"] += 1
            logger.error("port_fetch_failed", port_code=port.get("code"), error=str(e))

    log_operation_event(
        event_type="WEATHER_FETCHED",
        port_id=None,
        payload={
            "flow_run_id": flow_run_id,
            "ports_processed": len(ports),
            **results,
        },
        summary=(
            f"Weather collected: {results['success']} success, "
            f"{results['failed']} failed, {results['skipped']} skipped"
        ),
    )

    logger.info("weather_collector_completed", **results)
