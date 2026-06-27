-- =============================================================================
-- PORMS - Port Operation Risk Management System
-- PostgreSQL 16 fresh-install schema, version 2.0.0
-- =============================================================================
-- Usage:
--   psql -v ON_ERROR_STOP=1 -U postgres -d porms_db -f porms_schema.sql
--
-- Scope:
--   - Run against an empty database.
--   - Creates no PostgreSQL login roles and stores no deployment secrets.
--   - Seeds configuration and demonstration port data, but no user accounts.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS operational;
CREATE SCHEMA IF NOT EXISTS analytics;

-- =============================================================================
-- ENUMS
-- =============================================================================

DO $$ BEGIN
    CREATE TYPE operational.user_role_enum AS ENUM
        ('ADMIN', 'PORT_MANAGER', 'OPERATOR');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.user_status_enum AS ENUM
        ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'LOCKED');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.risk_level_enum AS ENUM
        ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.operation_mode_enum AS ENUM
        ('NORMAL', 'LIMITED', 'STOP');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.zone_type_enum AS ENUM
        ('DOCK', 'YARD', 'GATE', 'WAREHOUSE');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.weather_factor_enum AS ENUM
        ('WIND', 'RAIN', 'VISIBILITY');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.threshold_operator_enum AS ENUM
        ('GTE', 'LTE');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.sop_action_type_enum AS ENUM (
        'CREATE_TASK',
        'SEND_ALERT',
        'RESTRICT_ZONE',
        'UNRESTRICT_ZONE',
        'SET_NORMAL_MODE',
        'SET_LIMITED_MODE',
        'STOP_OPERATIONS'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.task_status_enum AS ENUM
        ('NEW', 'ACKNOWLEDGED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.alert_type_enum AS ENUM (
        'RISK_CHANGED',
        'MODE_CHANGED',
        'WEATHER',
        'SOP',
        'FETCH_FAILED',
        'SYSTEM',
        'SIMULATION'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.alert_severity_enum AS ENUM
        ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.simulation_status_enum AS ENUM
        ('PENDING', 'RUNNING', 'COMPLETED', 'STOPPED', 'FAILED');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE operational.job_status_enum AS ENUM
        ('PENDING', 'RUNNING', 'SUCCESS', 'FAILED');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- =============================================================================
-- OPERATIONAL: PORTS, USERS, AND AUTHENTICATION
-- =============================================================================

CREATE TABLE IF NOT EXISTS operational.ports (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code                     VARCHAR(20) NOT NULL,
    name                     VARCHAR(255) NOT NULL,
    address                  TEXT,
    latitude                 NUMERIC(9,6) NOT NULL
                                 CHECK (latitude BETWEEN -90 AND 90),
    longitude                NUMERIC(9,6) NOT NULL
                                 CHECK (longitude BETWEEN -180 AND 180),
    timezone                 VARCHAR(64) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    weather_source           VARCHAR(64) NOT NULL DEFAULT 'OPENWEATHER',
    weather_station_id       VARCHAR(100),
    is_active                BOOLEAN NOT NULL DEFAULT TRUE,
    current_risk_level       operational.risk_level_enum NOT NULL DEFAULT 'LOW',
    current_operation_mode   operational.operation_mode_enum NOT NULL DEFAULT 'NORMAL',
    last_weather_fetch_at    TIMESTAMPTZ,
    last_weather_fetch_ok    BOOLEAN,
    created_by_user_id       UUID,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at               TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_ports_code_active
    ON operational.ports (UPPER(code))
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS operational.users (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email                    VARCHAR(255) NOT NULL,
    full_name                VARCHAR(255) NOT NULL,
    phone_number             VARCHAR(20),
    password_hash            VARCHAR(255) NOT NULL,
    role                     operational.user_role_enum NOT NULL DEFAULT 'OPERATOR',
    status                   operational.user_status_enum NOT NULL DEFAULT 'ACTIVE',
    assigned_port_id         UUID REFERENCES operational.ports(id) ON DELETE RESTRICT,
    failed_login_count       SMALLINT NOT NULL DEFAULT 0
                                 CHECK (failed_login_count >= 0),
    locked_until             TIMESTAMPTZ,
    last_login_at            TIMESTAMPTZ,
    password_changed_at      TIMESTAMPTZ,
    created_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at               TIMESTAMPTZ,
    CONSTRAINT users_role_port_assignment CHECK (
        (role = 'ADMIN' AND assigned_port_id IS NULL)
        OR
        (role IN ('PORT_MANAGER', 'OPERATOR') AND assigned_port_id IS NOT NULL)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_email_active
    ON operational.users (LOWER(email))
    WHERE deleted_at IS NULL;

ALTER TABLE operational.ports
    DROP CONSTRAINT IF EXISTS ports_created_by_user_id_fkey;
ALTER TABLE operational.ports
    ADD CONSTRAINT ports_created_by_user_id_fkey
    FOREIGN KEY (created_by_user_id)
    REFERENCES operational.users(id)
    ON DELETE SET NULL;

CREATE TABLE IF NOT EXISTS operational.refresh_tokens (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                  UUID NOT NULL
                                 REFERENCES operational.users(id) ON DELETE CASCADE,
    token_hash               VARCHAR(255) NOT NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at               TIMESTAMPTZ NOT NULL,
    revoked_at               TIMESTAMPTZ,
    replaced_by_token_id     UUID REFERENCES operational.refresh_tokens(id) ON DELETE SET NULL,
    created_by_ip            INET,
    revoked_by_ip            INET,
    user_agent               TEXT,
    CONSTRAINT refresh_tokens_expiry_after_creation CHECK (expires_at > created_at)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_refresh_tokens_hash
    ON operational.refresh_tokens (token_hash);

CREATE TABLE IF NOT EXISTS operational.password_reset_tokens (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                  UUID NOT NULL
                                 REFERENCES operational.users(id) ON DELETE CASCADE,
    token_hash               VARCHAR(255) NOT NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at               TIMESTAMPTZ NOT NULL,
    used_at                  TIMESTAMPTZ,
    requested_by_ip          INET,
    CONSTRAINT reset_tokens_expiry_after_creation CHECK (expires_at > created_at)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_password_reset_tokens_hash
    ON operational.password_reset_tokens (token_hash);

-- =============================================================================
-- OPERATIONAL: PORT ZONES AND THRESHOLD OVERRIDES
-- =============================================================================

CREATE TABLE IF NOT EXISTS operational.zones (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE CASCADE,
    name                     VARCHAR(255) NOT NULL,
    zone_type                operational.zone_type_enum NOT NULL,
    description              TEXT,
    capacity_value           NUMERIC(12,2)
                                 CHECK (capacity_value IS NULL OR capacity_value >= 0),
    capacity_unit            VARCHAR(30),
    latitude                 NUMERIC(9,6)
                                 CHECK (latitude IS NULL OR latitude BETWEEN -90 AND 90),
    longitude                NUMERIC(9,6)
                                 CHECK (longitude IS NULL OR longitude BETWEEN -180 AND 180),
    display_order            SMALLINT NOT NULL DEFAULT 0
                                 CHECK (display_order >= 0),
    is_active                BOOLEAN NOT NULL DEFAULT TRUE,
    is_restricted            BOOLEAN NOT NULL DEFAULT FALSE,
    restriction_reason       TEXT,
    current_risk_level       operational.risk_level_enum NOT NULL DEFAULT 'LOW',
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at               TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_zones_port_name_active
    ON operational.zones (port_id, LOWER(name))
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS operational.zone_threshold_overrides (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    zone_id                  UUID NOT NULL
                                 REFERENCES operational.zones(id) ON DELETE CASCADE,
    factor                   operational.weather_factor_enum NOT NULL,
    risk_level               operational.risk_level_enum NOT NULL,
    comparison_operator      operational.threshold_operator_enum NOT NULL,
    threshold_value          NUMERIC(12,3) NOT NULL,
    unit                     VARCHAR(20) NOT NULL,
    is_enabled               BOOLEAN NOT NULL DEFAULT TRUE,
    change_reason            TEXT,
    created_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    updated_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_zone_threshold_factor_level UNIQUE (zone_id, factor, risk_level)
);

-- =============================================================================
-- OPERATIONAL: SIMULATION DEFINITIONS
-- =============================================================================

CREATE TABLE IF NOT EXISTS operational.simulation_datasets (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                     VARCHAR(255) NOT NULL,
    description              TEXT,
    source_filename          VARCHAR(255),
    checksum_sha256          VARCHAR(64),
    snapshot_count           INTEGER NOT NULL DEFAULT 0
                                 CHECK (snapshot_count >= 0),
    starts_at                TIMESTAMPTZ,
    ends_at                  TIMESTAMPTZ,
    metadata                 JSONB NOT NULL DEFAULT '{}'::JSONB,
    is_active                BOOLEAN NOT NULL DEFAULT TRUE,
    created_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT simulation_dataset_time_range CHECK (
        ends_at IS NULL OR starts_at IS NULL OR ends_at >= starts_at
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_simulation_dataset_checksum
    ON operational.simulation_datasets (checksum_sha256)
    WHERE checksum_sha256 IS NOT NULL;

CREATE TABLE IF NOT EXISTS operational.simulation_sessions (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dataset_id               UUID NOT NULL
                                 REFERENCES operational.simulation_datasets(id) ON DELETE RESTRICT,
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    started_by_user_id       UUID NOT NULL
                                 REFERENCES operational.users(id) ON DELETE RESTRICT,
    stopped_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    status                   operational.simulation_status_enum NOT NULL DEFAULT 'PENDING',
    speed_multiplier         NUMERIC(8,2) NOT NULL DEFAULT 1
                                 CHECK (speed_multiplier > 0 AND speed_multiplier <= 1000),
    progress_percent         NUMERIC(5,2) NOT NULL DEFAULT 0
                                 CHECK (progress_percent BETWEEN 0 AND 100),
    current_snapshot_number  INTEGER NOT NULL DEFAULT 0
                                 CHECK (current_snapshot_number >= 0),
    peak_risk_level          operational.risk_level_enum,
    generated_alert_count    INTEGER NOT NULL DEFAULT 0 CHECK (generated_alert_count >= 0),
    generated_task_count     INTEGER NOT NULL DEFAULT 0 CHECK (generated_task_count >= 0),
    sop_execution_count      INTEGER NOT NULL DEFAULT 0 CHECK (sop_execution_count >= 0),
    mode_change_count        INTEGER NOT NULL DEFAULT 0 CHECK (mode_change_count >= 0),
    started_at               TIMESTAMPTZ,
    ended_at                 TIMESTAMPTZ,
    stop_reason              TEXT,
    error_detail             TEXT,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS operational.simulation_snapshots (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dataset_id               UUID NOT NULL
                                 REFERENCES operational.simulation_datasets(id) ON DELETE CASCADE,
    snapshot_number          INTEGER NOT NULL CHECK (snapshot_number > 0),
    source_observed_at       TIMESTAMPTZ NOT NULL,
    wind_speed_ms            NUMERIC(7,2) NOT NULL CHECK (wind_speed_ms >= 0),
    beaufort_number          SMALLINT NOT NULL CHECK (beaufort_number BETWEEN 0 AND 12),
    wind_direction_deg       SMALLINT CHECK (wind_direction_deg BETWEEN 0 AND 360),
    wind_gust_ms             NUMERIC(7,2) CHECK (wind_gust_ms IS NULL OR wind_gust_ms >= 0),
    rainfall_1h_mm           NUMERIC(8,2) NOT NULL DEFAULT 0 CHECK (rainfall_1h_mm >= 0),
    temperature_c            NUMERIC(5,2),
    humidity_pct             SMALLINT CHECK (humidity_pct BETWEEN 0 AND 100),
    visibility_km            NUMERIC(7,2) CHECK (visibility_km IS NULL OR visibility_km >= 0),
    pressure_hpa             NUMERIC(7,2),
    raw_payload              JSONB NOT NULL DEFAULT '{}'::JSONB,
    CONSTRAINT uq_simulation_snapshot_order UNIQUE (dataset_id, snapshot_number)
);

-- =============================================================================
-- OPERATIONAL: WEATHER AND RISK
-- =============================================================================

CREATE TABLE IF NOT EXISTS operational.weather_readings (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    zone_id                  UUID REFERENCES operational.zones(id) ON DELETE SET NULL,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE CASCADE,
    wind_speed_ms            NUMERIC(7,2) NOT NULL CHECK (wind_speed_ms >= 0),
    beaufort_number          SMALLINT NOT NULL CHECK (beaufort_number BETWEEN 0 AND 12),
    wind_direction_deg       SMALLINT CHECK (wind_direction_deg BETWEEN 0 AND 360),
    wind_gust_ms             NUMERIC(7,2) CHECK (wind_gust_ms IS NULL OR wind_gust_ms >= 0),
    rainfall_1h_mm           NUMERIC(8,2) NOT NULL DEFAULT 0 CHECK (rainfall_1h_mm >= 0),
    rainfall_3h_mm           NUMERIC(8,2) CHECK (rainfall_3h_mm IS NULL OR rainfall_3h_mm >= 0),
    temperature_c            NUMERIC(5,2),
    humidity_pct             SMALLINT CHECK (humidity_pct BETWEEN 0 AND 100),
    visibility_km            NUMERIC(7,2) CHECK (visibility_km IS NULL OR visibility_km >= 0),
    pressure_hpa             NUMERIC(7,2),
    weather_code             INTEGER,
    weather_description      VARCHAR(255),
    observed_at              TIMESTAMPTZ NOT NULL,
    recorded_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    data_source              VARCHAR(50) NOT NULL DEFAULT 'OPENWEATHER_API',
    source_record_key        VARCHAR(255),
    raw_payload              JSONB NOT NULL DEFAULT '{}'::JSONB,
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT weather_simulation_consistency CHECK (
        (is_simulation = TRUE AND simulation_session_id IS NOT NULL)
        OR
        (is_simulation = FALSE AND simulation_session_id IS NULL)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_weather_source_observation
    ON operational.weather_readings (
        port_id,
        COALESCE(zone_id, '00000000-0000-0000-0000-000000000000'::UUID),
        data_source,
        observed_at,
        COALESCE(simulation_session_id, '00000000-0000-0000-0000-000000000000'::UUID)
    );

CREATE TABLE IF NOT EXISTS operational.risk_thresholds (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    factor                   operational.weather_factor_enum NOT NULL,
    risk_level               operational.risk_level_enum NOT NULL,
    comparison_operator      operational.threshold_operator_enum NOT NULL,
    threshold_value          NUMERIC(12,3) NOT NULL,
    unit                     VARCHAR(20) NOT NULL,
    description              TEXT,
    version                  INTEGER NOT NULL DEFAULT 1 CHECK (version > 0),
    is_enabled               BOOLEAN NOT NULL DEFAULT TRUE,
    change_reason            TEXT,
    created_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    updated_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_risk_threshold_version UNIQUE (factor, risk_level, version)
);

CREATE TABLE IF NOT EXISTS operational.risk_assessments (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    weather_reading_id       UUID NOT NULL
                                 REFERENCES operational.weather_readings(id) ON DELETE RESTRICT,
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    zone_id                  UUID REFERENCES operational.zones(id) ON DELETE SET NULL,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE CASCADE,
    wind_risk_level          operational.risk_level_enum NOT NULL,
    rain_risk_level          operational.risk_level_enum NOT NULL,
    visibility_risk_level    operational.risk_level_enum NOT NULL,
    final_risk_level         operational.risk_level_enum NOT NULL,
    previous_risk_level      operational.risk_level_enum,
    level_changed            BOOLEAN NOT NULL DEFAULT FALSE,
    dominant_factor          operational.weather_factor_enum NOT NULL,
    assessment_summary       TEXT,
    threshold_version        INTEGER NOT NULL DEFAULT 1 CHECK (threshold_version > 0),
    evaluated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT risk_simulation_consistency CHECK (
        (is_simulation = TRUE AND simulation_session_id IS NOT NULL)
        OR
        (is_simulation = FALSE AND simulation_session_id IS NULL)
    )
);

-- =============================================================================
-- OPERATIONAL: SOP AND EXECUTION
-- =============================================================================

CREATE TABLE IF NOT EXISTS operational.sop_rules (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rule_code                VARCHAR(80) NOT NULL,
    rule_name                VARCHAR(255) NOT NULL,
    description              TEXT,
    trigger_risk_level       operational.risk_level_enum NOT NULL,
    previous_risk_level      operational.risk_level_enum,
    applies_to_zone_type     operational.zone_type_enum,
    action_type              operational.sop_action_type_enum NOT NULL,
    action_config            JSONB NOT NULL DEFAULT '{}'::JSONB,
    execution_order          SMALLINT NOT NULL DEFAULT 100 CHECK (execution_order >= 0),
    is_active                BOOLEAN NOT NULL DEFAULT TRUE,
    version                  INTEGER NOT NULL DEFAULT 1 CHECK (version > 0),
    change_reason            TEXT,
    created_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    updated_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at               TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_sop_rule_code_active
    ON operational.sop_rules (UPPER(rule_code))
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS operational.sop_executions (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sop_rule_id              UUID NOT NULL
                                 REFERENCES operational.sop_rules(id) ON DELETE RESTRICT,
    risk_assessment_id       UUID NOT NULL
                                 REFERENCES operational.risk_assessments(id) ON DELETE RESTRICT,
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    zone_id                  UUID REFERENCES operational.zones(id) ON DELETE SET NULL,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE CASCADE,
    status                   operational.job_status_enum NOT NULL DEFAULT 'PENDING',
    execution_result         JSONB NOT NULL DEFAULT '{}'::JSONB,
    error_detail             TEXT,
    started_at               TIMESTAMPTZ,
    completed_at             TIMESTAMPTZ,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT sop_execution_time_order CHECK (
        completed_at IS NULL OR started_at IS NULL OR completed_at >= started_at
    )
);

-- =============================================================================
-- OPERATIONAL: MODES, TASKS, ALERTS, AUDIT, AND INGESTION
-- =============================================================================

CREATE TABLE IF NOT EXISTS operational.operation_mode_logs (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    previous_mode            operational.operation_mode_enum,
    new_mode                 operational.operation_mode_enum NOT NULL,
    change_source            VARCHAR(20) NOT NULL
                                 CHECK (change_source IN ('AUTO', 'MANUAL', 'SIMULATION')),
    risk_assessment_id       UUID REFERENCES operational.risk_assessments(id) ON DELETE SET NULL,
    sop_execution_id         UUID REFERENCES operational.sop_executions(id) ON DELETE SET NULL,
    changed_by_user_id       UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    override_reason          TEXT,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE CASCADE,
    changed_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT operation_mode_manual_reason CHECK (
        change_source <> 'MANUAL'
        OR
        (changed_by_user_id IS NOT NULL AND LENGTH(BTRIM(override_reason)) >= 20)
    )
);

CREATE TABLE IF NOT EXISTS operational.tasks (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_code                VARCHAR(40) NOT NULL,
    sop_execution_id         UUID REFERENCES operational.sop_executions(id) ON DELETE SET NULL,
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    zone_id                  UUID REFERENCES operational.zones(id) ON DELETE SET NULL,
    title                    VARCHAR(255) NOT NULL,
    description              TEXT,
    priority                 operational.alert_severity_enum NOT NULL DEFAULT 'MEDIUM',
    status                   operational.task_status_enum NOT NULL DEFAULT 'NEW',
    assigned_user_id         UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    assigned_team            VARCHAR(120),
    acknowledged_by_user_id  UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    acknowledged_at          TIMESTAMPTZ,
    completed_by_user_id     UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    completed_at             TIMESTAMPTZ,
    due_at                   TIMESTAMPTZ,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE CASCADE,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_tasks_code
    ON operational.tasks (task_code);

CREATE TABLE IF NOT EXISTS operational.alerts (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE RESTRICT,
    zone_id                  UUID REFERENCES operational.zones(id) ON DELETE SET NULL,
    risk_assessment_id       UUID REFERENCES operational.risk_assessments(id) ON DELETE SET NULL,
    sop_execution_id         UUID REFERENCES operational.sop_executions(id) ON DELETE SET NULL,
    alert_type               operational.alert_type_enum NOT NULL,
    severity                 operational.alert_severity_enum NOT NULL,
    title                    VARCHAR(255) NOT NULL,
    message                  TEXT NOT NULL,
    context                  JSONB NOT NULL DEFAULT '{}'::JSONB,
    expires_at               TIMESTAMPTZ,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE CASCADE,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS operational.alert_receipts (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_id                 UUID NOT NULL
                                 REFERENCES operational.alerts(id) ON DELETE CASCADE,
    user_id                  UUID NOT NULL
                                 REFERENCES operational.users(id) ON DELETE CASCADE,
    delivered_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    read_at                  TIMESTAMPTZ,
    acknowledged_at          TIMESTAMPTZ,
    CONSTRAINT uq_alert_receipt_user UNIQUE (alert_id, user_id),
    CONSTRAINT alert_receipt_ack_after_read CHECK (
        acknowledged_at IS NULL OR read_at IS NOT NULL
    )
);

CREATE TABLE IF NOT EXISTS operational.operation_events (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type               VARCHAR(80) NOT NULL,
    port_id                  UUID REFERENCES operational.ports(id) ON DELETE SET NULL,
    zone_id                  UUID REFERENCES operational.zones(id) ON DELETE SET NULL,
    actor_user_id            UUID REFERENCES operational.users(id) ON DELETE SET NULL,
    entity_type              VARCHAR(80),
    entity_id                UUID,
    summary                  TEXT NOT NULL,
    payload                  JSONB NOT NULL DEFAULT '{}'::JSONB,
    simulation_session_id    UUID REFERENCES operational.simulation_sessions(id) ON DELETE SET NULL,
    correlation_id           UUID,
    client_ip                INET,
    user_agent               TEXT,
    occurred_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS operational.weather_fetch_jobs (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    port_id                  UUID NOT NULL
                                 REFERENCES operational.ports(id) ON DELETE CASCADE,
    scheduled_at             TIMESTAMPTZ NOT NULL,
    started_at               TIMESTAMPTZ,
    completed_at             TIMESTAMPTZ,
    status                   operational.job_status_enum NOT NULL DEFAULT 'PENDING',
    attempt_count            SMALLINT NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    http_status_code         INTEGER CHECK (http_status_code BETWEEN 100 AND 599),
    weather_reading_id       UUID REFERENCES operational.weather_readings(id) ON DELETE SET NULL,
    error_category           VARCHAR(80),
    error_message            TEXT,
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT weather_fetch_job_time_order CHECK (
        completed_at IS NULL OR started_at IS NULL OR completed_at >= started_at
    )
);

-- =============================================================================
-- ANALYTICS DIMENSIONS
-- =============================================================================

CREATE TABLE IF NOT EXISTS analytics.dim_time (
    time_key                 INTEGER PRIMARY KEY,
    full_datetime            TIMESTAMPTZ NOT NULL,
    date_value               DATE NOT NULL,
    year                     SMALLINT NOT NULL,
    quarter                  SMALLINT NOT NULL CHECK (quarter BETWEEN 1 AND 4),
    month                    SMALLINT NOT NULL CHECK (month BETWEEN 1 AND 12),
    month_name               VARCHAR(20) NOT NULL,
    week_of_year             SMALLINT NOT NULL,
    day_of_month             SMALLINT NOT NULL CHECK (day_of_month BETWEEN 1 AND 31),
    day_of_week              SMALLINT NOT NULL CHECK (day_of_week BETWEEN 1 AND 7),
    day_name                 VARCHAR(20) NOT NULL,
    hour                     SMALLINT NOT NULL CHECK (hour BETWEEN 0 AND 23),
    is_weekend               BOOLEAN NOT NULL,
    is_business_hour         BOOLEAN NOT NULL
);

CREATE TABLE IF NOT EXISTS analytics.dim_port (
    port_key                 BIGSERIAL PRIMARY KEY,
    source_port_id           UUID NOT NULL UNIQUE,
    port_code                VARCHAR(20) NOT NULL,
    port_name                VARCHAR(255) NOT NULL,
    address                  TEXT,
    latitude                 NUMERIC(9,6),
    longitude                NUMERIC(9,6),
    timezone                 VARCHAR(64),
    is_active                BOOLEAN NOT NULL,
    last_synced_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.dim_zone (
    zone_key                 BIGSERIAL PRIMARY KEY,
    source_zone_id           UUID NOT NULL UNIQUE,
    source_port_id           UUID NOT NULL,
    port_code                VARCHAR(20) NOT NULL,
    port_name                VARCHAR(255) NOT NULL,
    zone_name                VARCHAR(255) NOT NULL,
    zone_type                VARCHAR(30) NOT NULL,
    is_active                BOOLEAN NOT NULL,
    last_synced_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.dim_risk_level (
    risk_level_key           SMALLINT PRIMARY KEY,
    risk_level               VARCHAR(20) NOT NULL UNIQUE,
    display_label            VARCHAR(50) NOT NULL,
    color_hex                VARCHAR(7) NOT NULL,
    sort_order               SMALLINT NOT NULL UNIQUE,
    beaufort_min             SMALLINT NOT NULL,
    beaufort_max             SMALLINT NOT NULL,
    description              TEXT
);

CREATE TABLE IF NOT EXISTS analytics.dim_sop_action (
    sop_action_key           SMALLINT PRIMARY KEY,
    action_type              VARCHAR(50) NOT NULL UNIQUE,
    display_label            VARCHAR(100) NOT NULL,
    description              TEXT
);

-- =============================================================================
-- ANALYTICS FACTS AND ETL STATE
-- =============================================================================

CREATE TABLE IF NOT EXISTS analytics.fact_weather_hourly (
    id                       BIGSERIAL PRIMARY KEY,
    source_group_key         VARCHAR(180) NOT NULL UNIQUE,
    time_key                 INTEGER NOT NULL REFERENCES analytics.dim_time(time_key),
    port_key                 BIGINT NOT NULL REFERENCES analytics.dim_port(port_key),
    zone_key                 BIGINT REFERENCES analytics.dim_zone(zone_key),
    source_port_id           UUID NOT NULL,
    source_zone_id           UUID,
    reading_count            INTEGER NOT NULL DEFAULT 0,
    avg_wind_speed_ms        NUMERIC(8,2),
    max_wind_speed_ms        NUMERIC(8,2),
    max_beaufort             SMALLINT,
    total_rainfall_mm        NUMERIC(10,2),
    avg_temperature_c        NUMERIC(6,2),
    avg_visibility_km        NUMERIC(8,2),
    min_visibility_km        NUMERIC(8,2),
    minutes_at_low           SMALLINT NOT NULL DEFAULT 0,
    minutes_at_medium        SMALLINT NOT NULL DEFAULT 0,
    minutes_at_high          SMALLINT NOT NULL DEFAULT 0,
    minutes_at_critical      SMALLINT NOT NULL DEFAULT 0,
    final_risk_level_key     SMALLINT REFERENCES analytics.dim_risk_level(risk_level_key),
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    etl_batch_id             VARCHAR(120),
    etl_loaded_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.fact_risk_assessment (
    id                       BIGSERIAL PRIMARY KEY,
    source_assessment_id     UUID NOT NULL UNIQUE,
    time_key                 INTEGER NOT NULL REFERENCES analytics.dim_time(time_key),
    port_key                 BIGINT NOT NULL REFERENCES analytics.dim_port(port_key),
    zone_key                 BIGINT REFERENCES analytics.dim_zone(zone_key),
    risk_level_key           SMALLINT NOT NULL REFERENCES analytics.dim_risk_level(risk_level_key),
    previous_risk_level_key  SMALLINT REFERENCES analytics.dim_risk_level(risk_level_key),
    dominant_factor          VARCHAR(20) NOT NULL,
    level_changed            BOOLEAN NOT NULL,
    evaluated_at             TIMESTAMPTZ NOT NULL,
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    etl_batch_id             VARCHAR(120),
    etl_loaded_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.fact_sop_execution (
    id                       BIGSERIAL PRIMARY KEY,
    source_execution_id      UUID NOT NULL UNIQUE,
    time_key                 INTEGER NOT NULL REFERENCES analytics.dim_time(time_key),
    port_key                 BIGINT NOT NULL REFERENCES analytics.dim_port(port_key),
    zone_key                 BIGINT REFERENCES analytics.dim_zone(zone_key),
    sop_action_key           SMALLINT NOT NULL REFERENCES analytics.dim_sop_action(sop_action_key),
    risk_level_key           SMALLINT NOT NULL REFERENCES analytics.dim_risk_level(risk_level_key),
    status                   VARCHAR(20) NOT NULL,
    duration_ms              BIGINT CHECK (duration_ms IS NULL OR duration_ms >= 0),
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    executed_at              TIMESTAMPTZ NOT NULL,
    etl_batch_id             VARCHAR(120),
    etl_loaded_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.fact_alert (
    id                       BIGSERIAL PRIMARY KEY,
    source_alert_id          UUID NOT NULL UNIQUE,
    time_key                 INTEGER NOT NULL REFERENCES analytics.dim_time(time_key),
    port_key                 BIGINT NOT NULL REFERENCES analytics.dim_port(port_key),
    zone_key                 BIGINT REFERENCES analytics.dim_zone(zone_key),
    severity_key             SMALLINT NOT NULL REFERENCES analytics.dim_risk_level(risk_level_key),
    alert_type               VARCHAR(40) NOT NULL,
    recipient_count          INTEGER NOT NULL DEFAULT 0,
    read_count               INTEGER NOT NULL DEFAULT 0,
    acknowledged_count       INTEGER NOT NULL DEFAULT 0,
    avg_read_seconds         NUMERIC(12,2),
    created_at               TIMESTAMPTZ NOT NULL,
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    etl_batch_id             VARCHAR(120),
    etl_loaded_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.fact_operation_event (
    id                       BIGSERIAL PRIMARY KEY,
    source_event_id          UUID NOT NULL UNIQUE,
    time_key                 INTEGER NOT NULL REFERENCES analytics.dim_time(time_key),
    port_key                 BIGINT REFERENCES analytics.dim_port(port_key),
    zone_key                 BIGINT REFERENCES analytics.dim_zone(zone_key),
    event_type               VARCHAR(80) NOT NULL,
    actor_role               VARCHAR(30),
    risk_level_before        VARCHAR(20),
    risk_level_after         VARCHAR(20),
    mode_before              VARCHAR(20),
    mode_after               VARCHAR(20),
    occurred_at              TIMESTAMPTZ NOT NULL,
    is_simulation            BOOLEAN NOT NULL DEFAULT FALSE,
    etl_batch_id             VARCHAR(120),
    etl_loaded_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics.etl_watermarks (
    flow_name                VARCHAR(120) PRIMARY KEY,
    last_loaded_at           TIMESTAMPTZ,
    last_batch_id            VARCHAR(120),
    last_status              operational.job_status_enum NOT NULL DEFAULT 'PENDING',
    last_row_count           BIGINT NOT NULL DEFAULT 0 CHECK (last_row_count >= 0),
    error_detail             TEXT,
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version                  VARCHAR(50) PRIMARY KEY,
    description              TEXT NOT NULL,
    applied_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    applied_by               VARCHAR(100) NOT NULL DEFAULT CURRENT_USER
);

-- =============================================================================
-- INDEXES
-- =============================================================================

CREATE INDEX IF NOT EXISTS idx_users_port_role
    ON operational.users (assigned_port_id, role)
    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_users_status
    ON operational.users (status, locked_until)
    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_active
    ON operational.refresh_tokens (user_id, expires_at DESC)
    WHERE revoked_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_ports_active
    ON operational.ports (is_active)
    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_zones_port_type
    ON operational.zones (port_id, zone_type, display_order)
    WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_weather_port_observed
    ON operational.weather_readings (port_id, observed_at DESC);
CREATE INDEX IF NOT EXISTS idx_weather_zone_observed
    ON operational.weather_readings (zone_id, observed_at DESC)
    WHERE zone_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_risk_port_evaluated
    ON operational.risk_assessments (port_id, evaluated_at DESC);
CREATE INDEX IF NOT EXISTS idx_risk_level_changes
    ON operational.risk_assessments (port_id, evaluated_at DESC)
    WHERE level_changed = TRUE;
CREATE INDEX IF NOT EXISTS idx_sop_rules_lookup
    ON operational.sop_rules (trigger_risk_level, applies_to_zone_type, execution_order)
    WHERE is_active = TRUE AND deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_sop_execution_history
    ON operational.sop_executions (port_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_mode_logs_port_time
    ON operational.operation_mode_logs (port_id, changed_at DESC);
CREATE INDEX IF NOT EXISTS idx_tasks_open
    ON operational.tasks (port_id, priority, created_at DESC)
    WHERE status IN ('NEW', 'ACKNOWLEDGED', 'IN_PROGRESS');
CREATE INDEX IF NOT EXISTS idx_tasks_zone_history
    ON operational.tasks (zone_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_alerts_port_history
    ON operational.alerts (port_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_alert_receipts_unread
    ON operational.alert_receipts (user_id, delivered_at DESC)
    WHERE read_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_events_port_time
    ON operational.operation_events (port_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_events_type_time
    ON operational.operation_events (event_type, occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_events_payload_gin
    ON operational.operation_events USING GIN (payload);
CREATE INDEX IF NOT EXISTS idx_simulation_sessions_port_status
    ON operational.simulation_sessions (port_id, status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_fetch_jobs_failures
    ON operational.weather_fetch_jobs (port_id, scheduled_at DESC)
    WHERE status = 'FAILED';
CREATE INDEX IF NOT EXISTS idx_fact_weather_time_port
    ON analytics.fact_weather_hourly (time_key DESC, port_key, zone_key);
CREATE INDEX IF NOT EXISTS idx_fact_risk_time_port
    ON analytics.fact_risk_assessment (time_key DESC, port_key, risk_level_key);
CREATE INDEX IF NOT EXISTS idx_fact_sop_time_port
    ON analytics.fact_sop_execution (time_key DESC, port_key, sop_action_key);
CREATE INDEX IF NOT EXISTS idx_fact_alert_time_port
    ON analytics.fact_alert (time_key DESC, port_key, severity_key);
CREATE INDEX IF NOT EXISTS idx_fact_event_time_type
    ON analytics.fact_operation_event (time_key DESC, event_type);

-- =============================================================================
-- FUNCTIONS AND TRIGGERS
-- =============================================================================

CREATE OR REPLACE FUNCTION operational.set_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$$;

DO $$
DECLARE
    table_name TEXT;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'ports',
        'users',
        'zones',
        'zone_threshold_overrides',
        'simulation_datasets',
        'simulation_sessions',
        'risk_thresholds',
        'sop_rules',
        'tasks',
        'alerts',
        'weather_fetch_jobs'
    ]
    LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_%1$s_updated_at ON operational.%1$I',
            table_name
        );
        EXECUTE format(
            'CREATE TRIGGER trg_%1$s_updated_at
             BEFORE UPDATE ON operational.%1$I
             FOR EACH ROW EXECUTE FUNCTION operational.set_updated_at()',
            table_name
        );
    END LOOP;
END;
$$;

DROP TRIGGER IF EXISTS trg_etl_watermarks_updated_at ON analytics.etl_watermarks;
CREATE TRIGGER trg_etl_watermarks_updated_at
    BEFORE UPDATE ON analytics.etl_watermarks
    FOR EACH ROW EXECUTE FUNCTION operational.set_updated_at();

CREATE OR REPLACE FUNCTION operational.protect_port_code()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.code IS DISTINCT FROM OLD.code THEN
        RAISE EXCEPTION 'Port code is immutable';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_protect_port_code ON operational.ports;
CREATE TRIGGER trg_protect_port_code
    BEFORE UPDATE OF code ON operational.ports
    FOR EACH ROW EXECUTE FUNCTION operational.protect_port_code();

CREATE OR REPLACE FUNCTION operational.sync_port_current_risk()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.zone_id IS NULL THEN
        UPDATE operational.ports
        SET current_risk_level = NEW.final_risk_level
        WHERE id = NEW.port_id;
    ELSE
        UPDATE operational.zones
        SET current_risk_level = NEW.final_risk_level
        WHERE id = NEW.zone_id;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sync_port_current_risk ON operational.risk_assessments;
CREATE TRIGGER trg_sync_port_current_risk
    AFTER INSERT ON operational.risk_assessments
    FOR EACH ROW EXECUTE FUNCTION operational.sync_port_current_risk();

CREATE OR REPLACE FUNCTION operational.sync_port_operation_mode()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE operational.ports
    SET current_operation_mode = NEW.new_mode
    WHERE id = NEW.port_id;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sync_port_operation_mode ON operational.operation_mode_logs;
CREATE TRIGGER trg_sync_port_operation_mode
    AFTER INSERT ON operational.operation_mode_logs
    FOR EACH ROW EXECUTE FUNCTION operational.sync_port_operation_mode();

CREATE OR REPLACE FUNCTION operational.prevent_operation_event_mutation()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'operation_events is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_prevent_operation_event_update ON operational.operation_events;
CREATE TRIGGER trg_prevent_operation_event_update
    BEFORE UPDATE OR DELETE ON operational.operation_events
    FOR EACH ROW EXECUTE FUNCTION operational.prevent_operation_event_mutation();

-- =============================================================================
-- VIEWS
-- =============================================================================

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
          AND (a.expires_at IS NULL OR a.expires_at > NOW())
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

CREATE OR REPLACE VIEW operational.v_alert_feed AS
SELECT
    a.id,
    a.port_id,
    p.code AS port_code,
    p.name AS port_name,
    a.zone_id,
    z.name AS zone_name,
    a.alert_type,
    a.severity,
    a.title,
    a.message,
    a.context,
    a.created_at,
    a.expires_at,
    COUNT(ar.id) AS recipient_count,
    COUNT(ar.read_at) AS read_count,
    COUNT(ar.acknowledged_at) AS acknowledged_count
FROM operational.alerts a
JOIN operational.ports p ON p.id = a.port_id
LEFT JOIN operational.zones z ON z.id = a.zone_id
LEFT JOIN operational.alert_receipts ar ON ar.alert_id = a.id
GROUP BY a.id, p.code, p.name, z.name;

CREATE OR REPLACE VIEW operational.v_open_tasks AS
SELECT
    t.*,
    p.code AS port_code,
    p.name AS port_name,
    z.name AS zone_name,
    u.full_name AS assigned_user_name
FROM operational.tasks t
JOIN operational.ports p ON p.id = t.port_id
LEFT JOIN operational.zones z ON z.id = t.zone_id
LEFT JOIN operational.users u ON u.id = t.assigned_user_id
WHERE t.status IN ('NEW', 'ACKNOWLEDGED', 'IN_PROGRESS');

CREATE OR REPLACE VIEW operational.v_simulation_summary AS
SELECT
    s.id,
    d.name AS dataset_name,
    p.code AS port_code,
    p.name AS port_name,
    s.status,
    s.speed_multiplier,
    s.progress_percent,
    s.peak_risk_level,
    s.generated_alert_count,
    s.generated_task_count,
    s.sop_execution_count,
    s.mode_change_count,
    s.started_at,
    s.ended_at,
    u.full_name AS started_by
FROM operational.simulation_sessions s
JOIN operational.simulation_datasets d ON d.id = s.dataset_id
JOIN operational.ports p ON p.id = s.port_id
JOIN operational.users u ON u.id = s.started_by_user_id;

-- =============================================================================
-- SEED: DIMENSIONS AND CONFIGURATION
-- =============================================================================

INSERT INTO analytics.dim_risk_level (
    risk_level_key, risk_level, display_label, color_hex,
    sort_order, beaufort_min, beaufort_max, description
)
VALUES
    (1, 'LOW', 'Low Risk', '#19A66A', 1, 0, 5, 'Normal operations'),
    (2, 'MEDIUM', 'Medium Risk', '#E9A11B', 2, 6, 7, 'Enhanced monitoring'),
    (3, 'HIGH', 'High Risk', '#EE7623', 3, 8, 9, 'Restricted operations'),
    (4, 'CRITICAL', 'Critical Risk', '#D94848', 4, 10, 12, 'Stop operations')
ON CONFLICT (risk_level_key) DO UPDATE SET
    display_label = EXCLUDED.display_label,
    color_hex = EXCLUDED.color_hex,
    sort_order = EXCLUDED.sort_order,
    beaufort_min = EXCLUDED.beaufort_min,
    beaufort_max = EXCLUDED.beaufort_max,
    description = EXCLUDED.description;

INSERT INTO analytics.dim_sop_action (
    sop_action_key, action_type, display_label, description
)
VALUES
    (1, 'CREATE_TASK', 'Create Task', 'Generate an operational task'),
    (2, 'SEND_ALERT', 'Send Alert', 'Notify affected users'),
    (3, 'RESTRICT_ZONE', 'Restrict Zone', 'Restrict a port zone'),
    (4, 'UNRESTRICT_ZONE', 'Unrestrict Zone', 'Remove a zone restriction'),
    (5, 'SET_NORMAL_MODE', 'Set Normal Mode', 'Set port operation mode to NORMAL'),
    (6, 'SET_LIMITED_MODE', 'Set Limited Mode', 'Set port operation mode to LIMITED'),
    (7, 'STOP_OPERATIONS', 'Stop Operations', 'Set port operation mode to STOP')
ON CONFLICT (sop_action_key) DO UPDATE SET
    display_label = EXCLUDED.display_label,
    description = EXCLUDED.description;

INSERT INTO operational.risk_thresholds (
    factor, risk_level, comparison_operator, threshold_value,
    unit, description, version
)
VALUES
    ('WIND', 'LOW', 'GTE', 0, 'Beaufort', 'Beaufort 0-5', 1),
    ('WIND', 'MEDIUM', 'GTE', 6, 'Beaufort', 'Beaufort 6-7', 1),
    ('WIND', 'HIGH', 'GTE', 8, 'Beaufort', 'Beaufort 8-9', 1),
    ('WIND', 'CRITICAL', 'GTE', 10, 'Beaufort', 'Beaufort 10-12', 1),
    ('RAIN', 'LOW', 'GTE', 0, 'mm/h', 'Rain below 10 mm/h', 1),
    ('RAIN', 'MEDIUM', 'GTE', 10, 'mm/h', 'Rain from 10 mm/h', 1),
    ('RAIN', 'HIGH', 'GTE', 25, 'mm/h', 'Rain from 25 mm/h', 1),
    ('RAIN', 'CRITICAL', 'GTE', 50, 'mm/h', 'Rain from 50 mm/h', 1),
    ('VISIBILITY', 'LOW', 'GTE', 10, 'km', 'Visibility at least 10 km', 1),
    ('VISIBILITY', 'MEDIUM', 'LTE', 10, 'km', 'Visibility at most 10 km', 1),
    ('VISIBILITY', 'HIGH', 'LTE', 5, 'km', 'Visibility at most 5 km', 1),
    ('VISIBILITY', 'CRITICAL', 'LTE', 1, 'km', 'Visibility at most 1 km', 1)
ON CONFLICT (factor, risk_level, version) DO UPDATE SET
    comparison_operator = EXCLUDED.comparison_operator,
    threshold_value = EXCLUDED.threshold_value,
    unit = EXCLUDED.unit,
    description = EXCLUDED.description,
    is_enabled = TRUE;

INSERT INTO operational.ports (
    code, name, address, latitude, longitude, timezone, weather_source
)
VALUES (
    'DNTSA',
    'Cảng Tiên Sa',
    '01 Yết Kiêu, Sơn Trà, Đà Nẵng',
    16.122800,
    108.214400,
    'Asia/Ho_Chi_Minh',
    'OPENWEATHER'
)
ON CONFLICT DO NOTHING;

INSERT INTO operational.zones (
    port_id, name, zone_type, description,
    capacity_value, capacity_unit, display_order, latitude, longitude
)
SELECT
    p.id,
    z.name,
    z.zone_type::operational.zone_type_enum,
    z.description,
    z.capacity_value,
    z.capacity_unit,
    z.display_order,
    z.latitude,
    z.longitude
FROM operational.ports p
CROSS JOIN (
    VALUES
        ('Bến số 1', 'DOCK', 'Cầu tàu container chính', 2, 'tàu', 1, 16.124000, 108.214000),
        ('Bến số 2', 'DOCK', 'Cầu tàu hàng tổng hợp', 2, 'tàu', 2, 16.124500, 108.214500),
        ('Bãi container A', 'YARD', 'Bãi container nhập', 1200, 'TEU', 3, 16.123000, 108.216000),
        ('Bãi container B', 'YARD', 'Bãi container xuất', 980, 'TEU', 4, 16.122000, 108.217000),
        ('Cổng chính', 'GATE', 'Cổng kiểm soát phương tiện', 8, 'làn', 5, 16.125000, 108.213000),
        ('Kho tổng hợp', 'WAREHOUSE', 'Kho hàng tổng hợp', 5000, 'm2', 6, 16.121000, 108.215000)
) AS z(name, zone_type, description, capacity_value, capacity_unit, display_order, latitude, longitude)
WHERE p.code = 'DNTSA'
ON CONFLICT DO NOTHING;

INSERT INTO operational.sop_rules (
    rule_code, rule_name, description, trigger_risk_level,
    applies_to_zone_type, action_type, action_config, execution_order, version
)
VALUES
    (
        'SOP-MED-GATE-01',
        'Giảm tốc độ phương tiện',
        'Giảm tốc độ xe trong khu vực cổng khi rủi ro MEDIUM.',
        'MEDIUM', 'GATE', 'CREATE_TASK',
        '{"title":"Giảm tốc độ phương tiện xuống 10 km/h","priority":"MEDIUM"}',
        10, 1
    ),
    (
        'SOP-HIGH-DOCK-01',
        'Hạn chế bốc xếp khi gió mạnh',
        'Tạm dừng thiết bị nâng cao và hạn chế bốc xếp tại cầu cảng.',
        'HIGH', 'DOCK', 'SET_LIMITED_MODE',
        '{"mode":"LIMITED","createTask":true,"sendAlert":true}',
        10, 1
    ),
    (
        'SOP-HIGH-YARD-01',
        'Neo giữ container và thiết bị',
        'Tạo nhiệm vụ neo giữ thiết bị và container tại bãi.',
        'HIGH', 'YARD', 'CREATE_TASK',
        '{"title":"Kiểm tra và neo giữ container, thiết bị","priority":"HIGH"}',
        20, 1
    ),
    (
        'SOP-CRIT-ALL-01',
        'Dừng toàn bộ hoạt động',
        'Dừng toàn bộ hoạt động cảng khi rủi ro CRITICAL.',
        'CRITICAL', NULL, 'STOP_OPERATIONS',
        '{"mode":"STOP","createTask":true,"sendAlert":true}',
        5, 1
    ),
    (
        'SOP-LOW-ALL-01',
        'Khôi phục vận hành bình thường',
        'Khôi phục NORMAL sau khi kiểm tra an toàn.',
        'LOW', NULL, 'SET_NORMAL_MODE',
        '{"mode":"NORMAL","requiresInspection":true}',
        100, 1
    )
ON CONFLICT DO NOTHING;

INSERT INTO analytics.etl_watermarks (
    flow_name, last_status, last_row_count
)
VALUES
    ('dimensional-sync', 'PENDING', 0),
    ('fact-weather-hourly', 'PENDING', 0),
    ('fact-operational-events', 'PENDING', 0)
ON CONFLICT (flow_name) DO NOTHING;

INSERT INTO public.schema_migrations (version, description)
VALUES (
    '2.0.0',
    'Fresh PORMS operational and analytics schema aligned with SRS and design demo'
)
ON CONFLICT (version) DO NOTHING;

-- Populate hourly time dimension for 2025-2028.
INSERT INTO analytics.dim_time (
    time_key, full_datetime, date_value, year, quarter, month,
    month_name, week_of_year, day_of_month, day_of_week,
    day_name, hour, is_weekend, is_business_hour
)
SELECT
    TO_CHAR(ts AT TIME ZONE 'Asia/Ho_Chi_Minh', 'YYYYMMDDHH24')::INTEGER,
    ts,
    (ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::DATE,
    EXTRACT(YEAR FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::SMALLINT,
    EXTRACT(QUARTER FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::SMALLINT,
    EXTRACT(MONTH FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::SMALLINT,
    TO_CHAR(ts AT TIME ZONE 'Asia/Ho_Chi_Minh', 'FMMonth'),
    EXTRACT(WEEK FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::SMALLINT,
    EXTRACT(DAY FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::SMALLINT,
    (EXTRACT(ISODOW FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh'))::SMALLINT,
    TO_CHAR(ts AT TIME ZONE 'Asia/Ho_Chi_Minh', 'FMDay'),
    EXTRACT(HOUR FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::SMALLINT,
    EXTRACT(ISODOW FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh') IN (6, 7),
    (
        EXTRACT(ISODOW FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh') BETWEEN 1 AND 5
        AND EXTRACT(HOUR FROM ts AT TIME ZONE 'Asia/Ho_Chi_Minh') BETWEEN 8 AND 17
    )
FROM generate_series(
    '2025-01-01 00:00:00+07'::TIMESTAMPTZ,
    '2028-12-31 23:00:00+07'::TIMESTAMPTZ,
    INTERVAL '1 hour'
) AS ts
ON CONFLICT (time_key) DO NOTHING;

-- =============================================================================
-- END OF PORMS SCHEMA 2.0.0
-- =============================================================================
