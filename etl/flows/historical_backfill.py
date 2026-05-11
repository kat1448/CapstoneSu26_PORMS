import random
from datetime import datetime, timedelta, timezone
from prefect import flow, task
import structlog
from sqlalchemy import text

from utils.beaufort import ms_to_beaufort
from db.connection import get_operational_session

logger = structlog.get_logger()


@task(name="generate-mock-historical-data")
def task_generate_mock_weather(
    port_id: str,
    port_code: str,
    latitude: float,
    longitude: float,
    days_back: int = 30,
) -> list[dict]:
    """
    Tạo mock weather data realistic cho N ngày qua.
    Pattern thực tế Đà Nẵng tháng 5: gió TB 3–5 m/s, xen kẽ vài ngày gió mạnh.
    Thay bằng OpenWeather History API nếu có key trả phí.
    """
    readings = []
    now = datetime.now(timezone.utc)

    for day_offset in range(days_back, 0, -1):
        base_date = now - timedelta(days=day_offset)

        for hour_offset in [0, 6, 12, 18]:
            observed_at = base_date.replace(
                hour=hour_offset, minute=0, second=0, microsecond=0
            )

            is_day = 6 <= hour_offset <= 18
            base_temp = 30 if is_day else 25

            is_storm_day = (day_offset % 7 == 3)
            if is_storm_day and hour_offset in [6, 12]:
                wind_speed = random.uniform(15, 22)
            else:
                wind_speed = random.uniform(2, 9)

            is_rainy = random.random() < 0.2
            rainfall = random.uniform(5, 30) if is_rainy else 0

            beaufort = ms_to_beaufort(wind_speed)

            readings.append({
                "port_id": port_id,
                "wind_speed_ms": round(wind_speed, 2),
                "beaufort_number": beaufort,
                "wind_direction_deg": random.randint(0, 360),
                "rainfall_1h_mm": round(rainfall, 2),
                "temperature_c": round(base_temp + random.uniform(-2, 2), 1),
                "humidity_pct": random.randint(65, 90),
                "visibility_km": round(random.uniform(8, 15), 1),
                "pressure_hpa": round(random.uniform(1008, 1015), 1),
                "ow_weather_code": 800 if rainfall == 0 else 500,
                "ow_weather_desc": "clear sky" if rainfall == 0 else "light rain",
                "observed_at": observed_at,
                "data_source": "MOCK_HISTORICAL",
                "is_simulation": False,
            })

    logger.info("mock_historical_generated", port_code=port_code,
                readings_count=len(readings), days_back=days_back)
    return readings


@task(name="bulk-insert-historical-readings", retries=3, retry_delay_seconds=10)
def task_bulk_insert_readings(readings: list[dict], port_code: str) -> int:
    """Bulk insert historical readings. IDEMPOTENT: ON CONFLICT DO NOTHING."""
    if not readings:
        return 0

    inserted = 0
    batch_size = 100

    with get_operational_session() as session:
        for i in range(0, len(readings), batch_size):
            batch = readings[i:i + batch_size]
            for r in batch:
                result = session.execute(text("""
                    INSERT INTO operational.weather_readings (
                        port_id, wind_speed_ms, beaufort_number, wind_direction_deg,
                        rainfall_1h_mm, temperature_c, humidity_pct, visibility_km,
                        pressure_hpa, ow_weather_code, ow_weather_desc,
                        observed_at, recorded_at, data_source, is_simulation
                    ) VALUES (
                        CAST(:port_id AS uuid), :wind_speed_ms, :beaufort_number,
                        :wind_direction_deg, :rainfall_1h_mm, :temperature_c,
                        :humidity_pct, :visibility_km, :pressure_hpa,
                        :ow_weather_code, :ow_weather_desc,
                        :observed_at, NOW(), :data_source, :is_simulation
                    )
                    ON CONFLICT DO NOTHING
                    RETURNING id
                """), r)
                if result.scalar():
                    inserted += 1

            session.commit()
            logger.info("batch_inserted", port_code=port_code,
                        batch=i // batch_size + 1, inserted=inserted)

    return inserted


@flow(
    name="historical-data-backfill",
    description="[ONE-TIME] Nạp dữ liệu lịch sử 30 ngày vào DB trước ngày demo",
    version="1.0.0",
)
def historical_backfill_flow(days_back: int = 30):
    """
    Chạy thủ công một lần trước demo:
        prefect deployment run historical-data-backfill/prod --param days_back=30
    Sau khi xong: trigger dw_loader để sync vào analytics.
    """
    logger.info("historical_backfill_started", days_back=days_back)

    with get_operational_session() as session:
        ports = session.execute(text("""
            SELECT id::text, code, latitude, longitude
            FROM operational.ports WHERE is_active = TRUE
        """)).fetchall()

    total_inserted = 0
    for port in ports:
        p = dict(port._mapping)
        readings = task_generate_mock_weather(
            port_id=p["id"],
            port_code=p["code"],
            latitude=p["latitude"],
            longitude=p["longitude"],
            days_back=days_back,
        )
        count = task_bulk_insert_readings(readings, p["code"])
        total_inserted += count
        logger.info("port_backfill_done", port_code=p["code"], inserted=count)

    logger.info("historical_backfill_completed", total_inserted=total_inserted)
