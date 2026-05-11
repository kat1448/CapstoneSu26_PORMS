import structlog
from prefect import task
from db.connection import get_operational_session
from config import settings

logger = structlog.get_logger()


@task(name="check-consecutive-failures")
def task_check_consecutive_failures():
    """
    Kiểm tra sau mỗi collector flow: nếu port nào có 3 FAILED liên tiếp
    → gọi ASP.NET để tạo alert FETCH_FAILED.
    """
    import httpx

    with get_operational_session() as session:
        failed_ports = session.execute("""
            WITH ranked_jobs AS (
                SELECT
                    port_id,
                    status,
                    ROW_NUMBER() OVER (PARTITION BY port_id ORDER BY started_at DESC) AS rn
                FROM operational.weather_fetch_jobs
                WHERE started_at > NOW() - INTERVAL '2 hours'
            )
            SELECT DISTINCT port_id::text
            FROM ranked_jobs
            WHERE rn <= 3
            GROUP BY port_id
            HAVING SUM(CASE WHEN status = 'FAILED' THEN 1 ELSE 0 END) >= 3
        """).fetchall()

        for row in failed_ports:
            port_id = row["port_id"]
            try:
                with httpx.Client(timeout=5.0) as client:
                    client.post(
                        f"{settings.BACKEND_TRIGGER_URL.replace('trigger-risk-engine', 'fetch-failed-alert')}",
                        json={"portId": port_id, "consecutiveFailures": 3},
                        headers={"X-Internal-Key": settings.INTERNAL_API_KEY},
                    )
                logger.warning("consecutive_failures_alert_sent", port_id=port_id)
            except Exception as e:
                logger.error("consecutive_failures_alert_failed",
                             port_id=port_id, error=str(e))
