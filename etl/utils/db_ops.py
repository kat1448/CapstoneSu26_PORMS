"""
Common DB operations dùng chung giữa các flows (plain functions, không phải Prefect tasks).
"""
import json
import structlog
from sqlalchemy import text
from db.connection import get_operational_session

logger = structlog.get_logger()


def log_operation_event(
    event_type: str,
    port_id: str | None,
    payload: dict,
    summary: str,
):
    """Ghi một event vào operational.operation_events."""
    try:
        with get_operational_session() as session:
            session.execute(
                text("""
                INSERT INTO operational.operation_events
                    (event_type, port_id, payload, summary)
                VALUES
                    (:event_type,
                     CAST(:port_id AS uuid),
                     CAST(:payload AS jsonb),
                     :summary)
                """),
                {
                    "event_type": event_type,
                    "port_id": port_id,
                    "payload": json.dumps(payload),
                    "summary": summary[:500],
                },
            )
            session.commit()
    except Exception as e:
        # Log nhưng không crash flow — event logging là non-critical
        logger.warning("operation_event_log_failed", event_type=event_type, error=str(e))
