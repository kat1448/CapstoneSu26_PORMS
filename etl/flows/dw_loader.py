from datetime import datetime, timezone
from prefect import flow, task
import pandas as pd
import structlog
from sqlalchemy import text

from db.connection import get_operational_session, get_analytics_session
from tasks.transformer import transform_weather_readings

logger = structlog.get_logger()


@task(name="sync-dim-tables", retries=2, retry_delay_seconds=10)
def task_sync_dim_tables():
    """Sync Dimension tables từ operational sang analytics (SCD Type 1)."""
    with get_operational_session() as op_sess, \
         get_analytics_session() as an_sess:

        ports = op_sess.execute(text("""
            SELECT id::text AS port_key, name AS port_name, code AS port_code,
                   address, latitude, longitude, timezone, is_active
            FROM operational.ports
            WHERE is_active = TRUE
        """)).fetchall()

        for port in ports:
            an_sess.execute(text("""
                INSERT INTO analytics.dim_port
                    (port_key, port_name, port_code, address,
                     latitude, longitude, timezone, is_active, last_synced_at)
                VALUES
                    (CAST(:port_key AS uuid), :port_name, :port_code, :address,
                     :latitude, :longitude, :timezone, :is_active, NOW())
                ON CONFLICT (port_key) DO UPDATE SET
                    port_name = EXCLUDED.port_name,
                    is_active = EXCLUDED.is_active,
                    last_synced_at = NOW()
            """), dict(port._mapping))

        zones = op_sess.execute(text("""
            SELECT z.id::text AS zone_key, z.port_id::text AS port_key,
                   p.name AS port_name, p.code AS port_code,
                   z.name AS zone_name, z.zone_type::text AS zone_type,
                   z.is_active
            FROM operational.zones z
            JOIN operational.ports p ON p.id = z.port_id
            WHERE z.is_active = TRUE AND p.is_active = TRUE
        """)).fetchall()

        for zone in zones:
            an_sess.execute(text("""
                INSERT INTO analytics.dim_zone
                    (zone_key, port_key, port_name, port_code,
                     zone_name, zone_type, is_active, last_synced_at)
                VALUES
                    (CAST(:zone_key AS uuid), CAST(:port_key AS uuid), :port_name, :port_code,
                     :zone_name, :zone_type, :is_active, NOW())
                ON CONFLICT (zone_key) DO UPDATE SET
                    zone_name = EXCLUDED.zone_name,
                    zone_type = EXCLUDED.zone_type,
                    is_active = EXCLUDED.is_active,
                    last_synced_at = NOW()
            """), dict(zone._mapping))

        an_sess.commit()
        logger.info("dim_tables_synced", ports=len(ports), zones=len(zones))


@task(name="get-watermark", retries=3, retry_delay_seconds=5)
def task_get_watermark(flow_name: str) -> datetime:
    with get_analytics_session() as session:
        result = session.execute(text("""
            SELECT last_loaded_at FROM analytics.etl_watermarks
            WHERE flow_name = :flow_name
        """), {"flow_name": flow_name}).scalar()
        return result or datetime(2025, 1, 1, tzinfo=timezone.utc)


@task(name="update-watermark", retries=3, retry_delay_seconds=5)
def task_update_watermark(flow_name: str, loaded_until: datetime, batch_id: str):
    with get_analytics_session() as session:
        session.execute(text("""
            UPDATE analytics.etl_watermarks
            SET last_loaded_at = :ts, last_batch_id = :batch_id, updated_at = NOW()
            WHERE flow_name = :flow_name
        """), {"ts": loaded_until, "batch_id": batch_id, "flow_name": flow_name})
        session.commit()


@task(name="load-fact-weather-readings", retries=2, retry_delay_seconds=15)
def task_load_fact_weather_readings(batch_id: str) -> int:
    watermark = task_get_watermark("fact_weather_readings")
    now = datetime.now(timezone.utc)

    with get_operational_session() as op_sess:
        df = pd.read_sql(
            text("""
                SELECT
                    port_id::text, wind_speed_ms, beaufort_number,
                    rainfall_1h_mm, visibility_km, temperature_c, observed_at
                FROM operational.weather_readings
                WHERE recorded_at > :watermark
                  AND recorded_at <= :now
                  AND is_simulation = FALSE
                ORDER BY observed_at
            """).bindparams(watermark=watermark, now=now),
            op_sess.get_bind(),
        )

    if df.empty:
        logger.info("no_new_weather_readings", watermark=str(watermark))
        return 0

    agg_df = transform_weather_readings(df)

    with get_analytics_session() as an_sess:
        for _, row in agg_df.iterrows():
            an_sess.execute(text("""
                INSERT INTO analytics.fact_weather_readings (
                    time_key, port_id, risk_level_key,
                    assessment_count, avg_wind_speed_ms, max_beaufort,
                    total_rainfall_mm,
                    minutes_at_low, minutes_at_medium,
                    minutes_at_high, minutes_at_critical,
                    final_risk_level, is_simulation,
                    etl_loaded_at, etl_batch_id
                ) VALUES (
                    :time_key, CAST(:port_id AS uuid),
                    (SELECT risk_level_key FROM analytics.dim_risk_levels
                     WHERE risk_level = :final_risk_level),
                    :reading_count, :avg_wind_speed_ms, :max_beaufort,
                    :total_rainfall_mm,
                    :minutes_at_low, :minutes_at_medium,
                    :minutes_at_high, :minutes_at_critical,
                    :final_risk_level, FALSE, NOW(), :batch_id
                )
                ON CONFLICT DO NOTHING
            """), {**row.to_dict(), "batch_id": batch_id})
        an_sess.commit()

    task_update_watermark("fact_weather_readings", now, batch_id)
    logger.info("fact_weather_loaded", rows=len(agg_df), batch_id=batch_id)
    return len(agg_df)


@task(name="load-fact-risk-assessments", retries=2, retry_delay_seconds=15)
def task_load_fact_risk_assessments(batch_id: str) -> int:
    watermark = task_get_watermark("fact_risk_assessments")
    now = datetime.now(timezone.utc)

    with get_operational_session() as op_sess:
        rows = op_sess.execute(text("""
            SELECT
                ra.id::text AS source_assessment_id,
                ra.port_id::text,
                TO_CHAR(ra.evaluated_at AT TIME ZONE 'Asia/Ho_Chi_Minh',
                        'YYYYMMDDHH24')::integer AS time_key,
                ra.final_risk_level::text,
                ra.previous_risk_level::text,
                ra.level_changed,
                ra.beaufort_number,
                ra.rainfall_1h_mm,
                ra.visibility_km,
                ra.evaluated_at
            FROM operational.risk_assessments ra
            WHERE ra.evaluated_at > :watermark
              AND ra.evaluated_at <= :now
              AND ra.is_simulation = FALSE
            ORDER BY ra.evaluated_at
        """), {"watermark": watermark, "now": now}).fetchall()

    if not rows:
        return 0

    with get_analytics_session() as an_sess:
        for row in rows:
            r = dict(row._mapping)
            an_sess.execute(text("""
                INSERT INTO analytics.fact_risk_assessments (
                    time_key, port_key, risk_level_key,
                    source_assessment_id, beaufort_number,
                    rainfall_1h_mm, visibility_km,
                    level_changed, previous_risk_level,
                    evaluated_at, is_simulation, etl_loaded_at, etl_batch_id
                ) VALUES (
                    :time_key, CAST(:port_id AS uuid),
                    (SELECT risk_level_key FROM analytics.dim_risk_levels
                     WHERE risk_level = :final_risk_level),
                    CAST(:source_assessment_id AS uuid), :beaufort_number,
                    :rainfall_1h_mm, :visibility_km,
                    :level_changed, :previous_risk_level,
                    :evaluated_at, FALSE, NOW(), :batch_id
                )
                ON CONFLICT DO NOTHING
            """), {**r, "batch_id": batch_id})
        an_sess.commit()

    task_update_watermark("fact_risk_assessments", now, batch_id)
    return len(rows)


@task(name="load-fact-sop-executions", retries=2, retry_delay_seconds=15)
def task_load_fact_sop_executions(batch_id: str) -> int:
    watermark = task_get_watermark("fact_sop_executions")
    now = datetime.now(timezone.utc)

    with get_operational_session() as op_sess:
        rows = op_sess.execute(text("""
            SELECT
                se.id::text AS source_execution_id,
                se.port_id::text,
                se.zone_id::text,
                TO_CHAR(se.triggered_at AT TIME ZONE 'Asia/Ho_Chi_Minh',
                        'YYYYMMDDHH24')::integer AS time_key,
                ra.final_risk_level::text AS risk_level,
                sr.action_type::text,
                sr.rule_name,
                se.status::text AS execution_status,
                ml.new_mode::text AS mode_changed_to,
                se.triggered_at,
                se.completed_at,
                EXTRACT(EPOCH FROM (se.completed_at - se.triggered_at)) * 1000 AS duration_ms
            FROM operational.sop_executions se
            JOIN operational.sop_rules sr ON sr.id = se.sop_rule_id
            JOIN operational.risk_assessments ra ON ra.id = se.assessment_id
            LEFT JOIN operational.operation_mode_log ml
                ON ml.triggered_by_sop_rule_id = se.sop_rule_id
               AND ml.port_id = se.port_id
            WHERE se.triggered_at > :watermark
              AND se.triggered_at <= :now
              AND se.is_simulation = FALSE
            ORDER BY se.triggered_at
        """), {"watermark": watermark, "now": now}).fetchall()

    if not rows:
        return 0

    with get_analytics_session() as an_sess:
        for row in rows:
            r = dict(row._mapping)
            an_sess.execute(text("""
                INSERT INTO analytics.fact_sop_executions (
                    time_key, port_key, zone_key, risk_level_key, action_key,
                    source_execution_id, rule_name, execution_status,
                    mode_changed_to, triggered_at, completed_at, duration_ms,
                    is_simulation, etl_loaded_at, etl_batch_id
                ) VALUES (
                    :time_key, CAST(:port_id AS uuid), CAST(:zone_id AS uuid),
                    (SELECT risk_level_key FROM analytics.dim_risk_levels
                     WHERE risk_level = :risk_level),
                    (SELECT action_key FROM analytics.dim_sop_action
                     WHERE action_type = :action_type),
                    CAST(:source_execution_id AS uuid), :rule_name, :execution_status,
                    :mode_changed_to, :triggered_at, :completed_at, :duration_ms,
                    FALSE, NOW(), :batch_id
                )
                ON CONFLICT DO NOTHING
            """), {**r, "batch_id": batch_id})
        an_sess.commit()

    task_update_watermark("fact_sop_executions", now, batch_id)
    return len(rows)


@task(name="load-fact-alerts", retries=2, retry_delay_seconds=15)
def task_load_fact_alerts(batch_id: str) -> int:
    watermark = task_get_watermark("fact_alerts")
    now = datetime.now(timezone.utc)

    with get_operational_session() as op_sess:
        rows = op_sess.execute(text("""
            SELECT
                a.id::text AS source_alert_id,
                a.port_id::text,
                TO_CHAR(a.created_at AT TIME ZONE 'Asia/Ho_Chi_Minh',
                        'YYYYMMDDHH24')::integer AS time_key,
                ra.final_risk_level::text AS risk_level,
                a.alert_type,
                a.severity::text,
                EXTRACT(EPOCH FROM (a.read_at - a.created_at)) / 60 AS time_to_read_min,
                a.created_at,
                a.read_at
            FROM operational.alerts a
            LEFT JOIN operational.risk_assessments ra
                ON ra.id = a.related_assessment_id
            WHERE a.created_at > :watermark
              AND a.created_at <= :now
              AND a.is_simulation = FALSE
            ORDER BY a.created_at
        """), {"watermark": watermark, "now": now}).fetchall()

    if not rows:
        return 0

    with get_analytics_session() as an_sess:
        for row in rows:
            r = dict(row._mapping)
            an_sess.execute(text("""
                INSERT INTO analytics.fact_alerts (
                    time_key, port_key, risk_level_key,
                    source_alert_id, alert_type, severity,
                    time_to_read_min, created_at, read_at,
                    is_simulation, etl_loaded_at, etl_batch_id
                ) VALUES (
                    :time_key, CAST(:port_id AS uuid),
                    (SELECT risk_level_key FROM analytics.dim_risk_levels
                     WHERE risk_level = :risk_level),
                    CAST(:source_alert_id AS uuid), :alert_type, :severity,
                    :time_to_read_min, :created_at, :read_at,
                    FALSE, NOW(), :batch_id
                )
                ON CONFLICT DO NOTHING
            """), {**r, "batch_id": batch_id})
        an_sess.commit()

    task_update_watermark("fact_alerts", now, batch_id)
    return len(rows)


@flow(
    name="dw-loader",
    description="Load dữ liệu từ operational DB vào Data Warehouse (analytics schema)",
    version="1.0.0",
)
def dw_loader_flow():
    """Flow chính DW Loader — chạy mỗi giờ."""
    from prefect.context import get_run_context
    batch_id = str(get_run_context().flow_run.id)

    logger.info("dw_loader_started", batch_id=batch_id)

    task_sync_dim_tables()

    counts = {}
    counts["weather"] = task_load_fact_weather_readings(batch_id)
    counts["risk"] = task_load_fact_risk_assessments(batch_id)
    counts["sop"] = task_load_fact_sop_executions(batch_id)
    counts["alerts"] = task_load_fact_alerts(batch_id)

    logger.info("dw_loader_completed", batch_id=batch_id, row_counts=counts)
