import httpx
import structlog
from prefect import flow, task
from sqlalchemy import text

from config import settings
from db.connection import get_operational_session

logger = structlog.get_logger()


@task(name="load-forecast-ready-ports", retries=2, retry_delay_seconds=5)
def load_forecast_ready_ports() -> list[str]:
    """Only create plans after a real OpenWeather reading exists for the port."""
    with get_operational_session() as session:
        rows = session.execute(text("""
            SELECT DISTINCT p.code
            FROM operational.ports p
            JOIN operational.weather_readings w ON w.port_id = p.id
            WHERE p.is_active = TRUE
              AND p.deleted_at IS NULL
              AND w.is_simulation = FALSE
              AND w.data_source = 'OPENWEATHER_API'
            ORDER BY p.code
        """)).fetchall()
    return [row.code for row in rows]


@task(name="refresh-port-forecast-plan", retries=6, retry_delay_seconds=10)
def refresh_port_forecast_plan(port_code: str) -> bool:
    backend_base_url = settings.BACKEND_TRIGGER_URL.split("/api/", 1)[0]
    with httpx.Client(timeout=20.0) as client:
        response = client.post(
            f"{backend_base_url}/api/simulation/forecast-plan",
            json={"portCode": port_code, "horizonDays": 5},
        )
        response.raise_for_status()
    logger.info("forecast_plan_refreshed", port_code=port_code)
    return True


@flow(
    name="forecast-plan-refresh",
    description="Tạo kế hoạch dự báo 5 ngày mới cho các cảng có dữ liệu OpenWeather",
    version="1.0.0",
    log_prints=True,
)
def forecast_plan_refresh_flow():
    ports = load_forecast_ready_ports()
    refreshed = 0
    for port_code in ports:
        if refresh_port_forecast_plan(port_code):
            refreshed += 1
    logger.info("forecast_plan_refresh_completed", ports=ports, refreshed=refreshed)
    return refreshed
