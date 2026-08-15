BEGIN;

-- Chuẩn hóa dữ liệu cũ: cảnh báo thiếu hoặc có thời hạn không hợp lệ sẽ hết hạn sau 2 giờ.
UPDATE operational.alerts
SET expires_at = created_at + INTERVAL '2 hours'
WHERE expires_at IS NULL
   OR expires_at <= created_at;

CREATE OR REPLACE FUNCTION operational.set_default_alert_expiration()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    -- Bảo vệ mọi nguồn tạo cảnh báo, kể cả khi nguồn đó không truyền expires_at.
    IF NEW.expires_at IS NULL THEN
        NEW.expires_at := COALESCE(NEW.created_at, NOW()) + INTERVAL '2 hours';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_set_default_alert_expiration ON operational.alerts;
CREATE TRIGGER trg_set_default_alert_expiration
    BEFORE INSERT ON operational.alerts
    FOR EACH ROW EXECUTE FUNCTION operational.set_default_alert_expiration();

ALTER TABLE operational.alerts
    ALTER COLUMN expires_at SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'operational.alerts'::regclass
          AND conname = 'alerts_expiry_after_creation'
    ) THEN
        ALTER TABLE operational.alerts
            ADD CONSTRAINT alerts_expiry_after_creation
            CHECK (expires_at > created_at);
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS idx_alerts_port_expiration
    ON operational.alerts (port_id, expires_at);

CREATE OR REPLACE VIEW operational.v_port_current_state AS
SELECT
    p.id AS port_id,
    p.code AS port_code,
    p.name AS port_name,
    p.current_risk_level,
    p.current_operation_mode,
    p.is_active,
    p.last_weather_fetch_at,
    wr.wind_speed_ms,
    wr.beaufort_number,
    wr.rainfall_1h_mm,
    wr.temperature_c,
    wr.humidity_pct,
    wr.visibility_km,
    wr.observed_at AS weather_observed_at,
    ra.assessment_summary,
    ra.dominant_factor,
    ra.evaluated_at AS last_assessed_at,
    (
        SELECT COUNT(*)
        FROM operational.alerts a
        WHERE a.port_id = p.id
          AND a.expires_at > NOW()
    ) AS active_alert_count
FROM operational.ports p
LEFT JOIN LATERAL (
    SELECT w.*
    FROM operational.weather_readings w
    WHERE w.port_id = p.id
      AND w.is_simulation = FALSE
    ORDER BY w.observed_at DESC
    LIMIT 1
) wr ON TRUE
LEFT JOIN LATERAL (
    SELECT r.*
    FROM operational.risk_assessments r
    WHERE r.port_id = p.id
      AND r.zone_id IS NULL
      AND r.is_simulation = FALSE
    ORDER BY r.evaluated_at DESC
    LIMIT 1
) ra ON TRUE
WHERE p.deleted_at IS NULL;

INSERT INTO public.schema_migrations (version, description)
VALUES (
    '2.0.1',
    'Backfill and enforce two-hour alert expiration'
)
ON CONFLICT (version) DO NOTHING;

COMMIT;
