-- =============================================================================
--  PORMS — Port Operation Risk Management System
--  Database Migration Script — v1.0.0
-- =============================================================================
--  Môi trường   : PostgreSQL 16+
--  Encoding     : UTF-8
--  Tác giả      : Nguyễn Phan Anh Minh (PM/DE) — HE153552
--  Ngày tạo     : 04/05/2026
--  Mô tả        : Full schema migration cho hai schema:
--                   • operational  — dữ liệu real-time (API server đọc/ghi)
--                   • analytics    — Data Warehouse (Prefect ETL ghi, Metabase đọc)
--
--  Thứ tự chạy  : psql -U postgres -d porms_db -f porms_migration.sql
--  Reset toàn bộ: psql -U postgres -d porms_db -f porms_migration.sql (idempotent)
--
--  Lưu ý quan trọng:
--    - Script này idempotent: chạy nhiều lần không bị lỗi (dùng IF NOT EXISTS)
--    - Tất cả timestamp dùng TIMESTAMPTZ (UTC) — convert sang UTC+7 ở application layer
--    - Soft delete dùng is_active / deleted_at, KHÔNG dùng hard DELETE
--    - JSONB cho payload linh hoạt — tránh alter table khi thêm field
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 0. KHỞI TẠO DATABASE & EXTENSION
-- ---------------------------------------------------------------------------

-- Tạo database (chạy với user postgres, bên ngoài script nếu cần)
-- CREATE DATABASE porms_db ENCODING 'UTF8' LC_COLLATE 'en_US.UTF-8' LC_CTYPE 'en_US.UTF-8';

-- Extension cần thiết
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";   -- uuid_generate_v4()
CREATE EXTENSION IF NOT EXISTS "pgcrypto";    -- gen_random_uuid(), crypt() cho password
CREATE EXTENSION IF NOT EXISTS "pg_trgm";     -- trigram index cho full-text search (logs)

-- ---------------------------------------------------------------------------
-- 1. TẠO SCHEMA
-- ---------------------------------------------------------------------------

-- Schema operational: toàn bộ dữ liệu real-time của ứng dụng
CREATE SCHEMA IF NOT EXISTS operational;

-- Schema analytics: Data Warehouse, chỉ Prefect ETL được ghi vào
-- Metabase connect trực tiếp schema này — KHÔNG share với operational
CREATE SCHEMA IF NOT EXISTS analytics;

-- Đặt search_path mặc định cho session
SET search_path TO operational, public;

-- ---------------------------------------------------------------------------
-- 2. ENUM TYPES
-- ---------------------------------------------------------------------------

-- Mức độ rủi ro — theo thang Beaufort quốc tế (WMO)
-- LOW      : Beaufort 0–5   (0–10.7 m/s)  — Vận hành bình thường
-- MEDIUM   : Beaufort 6–7   (10.8–17.1)   — Tăng cường giám sát
-- HIGH     : Beaufort 8–9   (17.2–24.4)   — Hạn chế hoạt động
-- CRITICAL : Beaufort 10–12 (>24.5 m/s)  — Dừng toàn bộ, sơ tán
DO $$ BEGIN
    CREATE TYPE operational.risk_level_enum AS ENUM ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Trạng thái vận hành cảng — state machine (chỉ cho phép NORMAL→LIMITED→STOP)
-- NORMAL   : Vận hành đầy đủ, không hạn chế
-- LIMITED  : Giới hạn một số hoạt động (bốc xếp hàng cao, tàu lớn)
-- STOP     : Dừng toàn bộ hoạt động, kích hoạt SOP khẩn cấp
DO $$ BEGIN
    CREATE TYPE operational.operation_mode_enum AS ENUM ('NORMAL', 'LIMITED', 'STOP');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Loại vùng trong cảng
-- DOCK  : Khu vực cầu tàu — tiếp nhận/xuất tàu, bốc xếp container
-- YARD  : Khu vực bãi — lưu trữ container, xe tải
-- GATE  : Khu vực cổng — kiểm soát vào/ra
-- WAREHOUSE : Kho hàng nội địa
DO $$ BEGIN
    CREATE TYPE operational.zone_type_enum AS ENUM ('DOCK', 'YARD', 'GATE', 'WAREHOUSE');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Vai trò người dùng — RBAC
-- ADMIN         : Toàn quyền hệ thống (thêm port, cấu hình threshold, quản lý user)
-- COMPANY_ADMIN : Quản lý cảng được phân công (xem tất cả, sửa SOP, override mode)
-- OPERATOR      : Nhân viên vận hành (chỉ xem dashboard, đọc alert, mark alert as read)
DO $$ BEGIN
    CREATE TYPE operational.user_role_enum AS ENUM ('ADMIN', 'COMPANY_ADMIN', 'OPERATOR');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Trạng thái tài khoản người dùng
DO $$ BEGIN
    CREATE TYPE operational.user_status_enum AS ENUM ('ACTIVE', 'INACTIVE', 'SUSPENDED');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Loại yếu tố thời tiết dùng để đánh giá rủi ro
DO $$ BEGIN
    CREATE TYPE operational.weather_factor_enum AS ENUM ('WIND', 'RAIN', 'WAVE', 'VISIBILITY');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Loại hành động SOP
-- STOP_LOADING      : Dừng bốc dỡ hàng
-- LIMIT_VESSEL_ENTRY: Hạn chế tàu vào cảng
-- EVACUATE_EQUIPMENT: Di chuyển thiết bị vào nơi an toàn
-- CLOSE_GATE        : Đóng cổng khu vực
-- EMERGENCY_SHUTDOWN: Tắt khẩn cấp toàn bộ
-- NOTIFY_AUTHORITY  : Thông báo cơ quan quản lý
-- CUSTOM            : Hành động tùy chỉnh (lưu trong description)
DO $$ BEGIN
    CREATE TYPE operational.sop_action_type_enum AS ENUM (
        'STOP_LOADING',
        'LIMIT_VESSEL_ENTRY',
        'EVACUATE_EQUIPMENT',
        'CLOSE_GATE',
        'EMERGENCY_SHUTDOWN',
        'NOTIFY_AUTHORITY',
        'CUSTOM'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Mức độ nghiêm trọng của alert
DO $$ BEGIN
    CREATE TYPE operational.alert_severity_enum AS ENUM ('INFO', 'WARNING', 'CRITICAL');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- Loại event trong operation log (audit trail)
DO $$ BEGIN
    CREATE TYPE operational.event_type_enum AS ENUM (
        'WEATHER_FETCHED',       -- Dữ liệu thời tiết được fetch từ API
        'RISK_ASSESSED',         -- Risk Engine đánh giá xong
        'RISK_LEVEL_CHANGED',    -- Risk level thay đổi (LOW→HIGH, v.v.)
        'SOP_TRIGGERED',         -- SOP Engine kích hoạt một rule
        'MODE_CHANGED',          -- Operation mode thay đổi
        'TASK_CREATED',          -- Task được tạo tự động
        'ALERT_CREATED',         -- Alert được tạo
        'ALERT_READ',            -- Alert được đọc bởi operator
        'THRESHOLD_UPDATED',     -- Admin thay đổi ngưỡng risk
        'SOP_RULE_UPDATED',      -- Admin thay đổi SOP rule
        'SIMULATION_STARTED',    -- Simulation mode bắt đầu
        'SIMULATION_ENDED',      -- Simulation mode kết thúc
        'MODE_OVERRIDDEN'        -- Admin override mode thủ công
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ---------------------------------------------------------------------------
-- 3. SCHEMA OPERATIONAL — BẢNG CHÍNH
-- ---------------------------------------------------------------------------

-- ============================================================
-- 3.1 BẢNG: operational.ports
-- Thông tin cảng biển — đơn vị tổ chức cấp cao nhất trong hệ thống
-- Mỗi cảng có tọa độ địa lý để gọi OpenWeather API (lat/lon)
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.ports (
    -- PK: UUID để tránh enumeration attack qua API
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- Tên cảng — hiển thị trên UI và báo cáo
    name                VARCHAR(255)    NOT NULL,

    -- Tên viết tắt để hiển thị compact trên dashboard (ví dụ: "ĐNPT")
    code                VARCHAR(20)     NOT NULL UNIQUE,

    -- Địa chỉ đầy đủ của cảng
    address             TEXT,

    -- Tọa độ địa lý — dùng để gọi OpenWeather API
    -- Precision 6 decimal places ≈ độ chính xác ~0.1m (đủ cho cảng biển)
    latitude            DECIMAL(9, 6)   NOT NULL
                            CONSTRAINT ports_latitude_range
                            CHECK (latitude BETWEEN -90 AND 90),

    longitude           DECIMAL(9, 6)   NOT NULL
                            CONSTRAINT ports_longitude_range
                            CHECK (longitude BETWEEN -180 AND 180),

    -- Múi giờ của cảng — dùng để hiển thị thời gian local trên UI
    -- Ví dụ: 'Asia/Ho_Chi_Minh' cho UTC+7
    timezone            VARCHAR(50)     NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',

    -- Trạng thái cảng: TRUE = đang hoạt động, FALSE = tạm ngưng (soft delete)
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,

    -- Chế độ vận hành hiện tại — cache ở đây để tránh join với operation_mode_log
    -- Được cập nhật mỗi khi mode thay đổi (denormalized for read performance)
    current_mode        operational.operation_mode_enum NOT NULL DEFAULT 'NORMAL',

    -- Risk level hiện tại — cache tương tự current_mode
    current_risk_level  operational.risk_level_enum NOT NULL DEFAULT 'LOW',

    -- OpenWeather API: station ID nếu có (chính xác hơn lat/lon lookup)
    -- NULL nếu dùng lat/lon
    ow_station_id       VARCHAR(100),

    -- Metadata
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Người tạo (FK → users, nullable vì có thể là seed data)
    created_by_user_id  UUID
);

COMMENT ON TABLE  operational.ports                   IS 'Danh sách cảng biển được quản lý trong hệ thống';
COMMENT ON COLUMN operational.ports.id                IS 'UUID primary key — tránh sequential ID enumeration';
COMMENT ON COLUMN operational.ports.code              IS 'Mã cảng viết tắt, unique. VD: DNPT, SGPT, HPT';
COMMENT ON COLUMN operational.ports.latitude          IS 'Vĩ độ — dùng cho OpenWeather API current weather endpoint';
COMMENT ON COLUMN operational.ports.longitude         IS 'Kinh độ — dùng cho OpenWeather API current weather endpoint';
COMMENT ON COLUMN operational.ports.current_mode      IS 'Cache mode hiện tại — denormalized từ operation_mode_log để đọc nhanh';
COMMENT ON COLUMN operational.ports.current_risk_level IS 'Cache risk level hiện tại — denormalized từ risk_assessments';
COMMENT ON COLUMN operational.ports.ow_station_id     IS 'OpenWeather station ID (tùy chọn) — chính xác hơn lat/lon nếu có';


-- ============================================================
-- 3.2 BẢNG: operational.zones
-- Vùng/khu vực trong cảng — đơn vị địa lý cấp 2
-- Mỗi zone có loại riêng ảnh hưởng đến SOP rules áp dụng
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.zones (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng chứa zone này
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- Tên zone — hiển thị trên map và dashboard
    name                VARCHAR(255)    NOT NULL,

    -- Loại zone — quyết định SOP rules nào được áp dụng
    zone_type           operational.zone_type_enum NOT NULL,

    -- Mô tả thêm về zone (vị trí, đặc điểm, capacity)
    description         TEXT,

    -- Capacity tối đa (TEU cho DOCK, số xe cho YARD) — dùng cho báo cáo
    capacity            INTEGER
                            CONSTRAINT zones_capacity_positive
                            CHECK (capacity IS NULL OR capacity > 0),

    -- Tọa độ trung tâm zone — hiển thị trên Leaflet map
    latitude            DECIMAL(9, 6),
    longitude           DECIMAL(9, 6),

    -- Trạng thái hoạt động
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,

    -- Risk level hiện tại của zone (có thể khác port nếu có override)
    current_risk_level  operational.risk_level_enum NOT NULL DEFAULT 'LOW',

    -- Thứ tự hiển thị trên UI
    display_order       SMALLINT        NOT NULL DEFAULT 0,

    -- Metadata
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE  operational.zones              IS 'Vùng/khu vực trong cảng (DOCK/YARD/GATE/WAREHOUSE)';
COMMENT ON COLUMN operational.zones.zone_type    IS 'Loại zone — SOP engine dùng để match rules';
COMMENT ON COLUMN operational.zones.capacity     IS 'TEU (DOCK), số xe (YARD), slots (GATE) — cho báo cáo capacity';
COMMENT ON COLUMN operational.zones.display_order IS 'Thứ tự hiển thị trên dashboard map';


-- ============================================================
-- 3.3 BẢNG: operational.users
-- Tài khoản người dùng hệ thống
-- Password hash bằng bcrypt (cost factor 12) — KHÔNG lưu plain text
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.users (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- Email là username — unique across system
    email               VARCHAR(255)    NOT NULL UNIQUE,

    -- Họ tên đầy đủ — hiển thị trên UI và audit log
    full_name           VARCHAR(255)    NOT NULL,

    -- Password hash: bcrypt(password, cost=12)
    -- Không bao giờ log hoặc return field này qua API
    password_hash       VARCHAR(255)    NOT NULL,

    -- Vai trò — quyết định access control
    role                operational.user_role_enum NOT NULL DEFAULT 'OPERATOR',

    -- Trạng thái tài khoản
    status              operational.user_status_enum NOT NULL DEFAULT 'ACTIVE',

    -- Port mà user này phụ trách (NULL cho ADMIN — quản lý tất cả)
    -- COMPANY_ADMIN và OPERATOR chỉ xem được data của port_id này
    assigned_port_id    UUID
                            REFERENCES operational.ports(id)
                            ON DELETE SET NULL,

    -- Số điện thoại — dùng cho liên lạc khẩn cấp khi alert CRITICAL
    phone_number        VARCHAR(20),

    -- Refresh token hash — lưu để invalidate khi logout/revoke
    -- NULL khi user chưa login hoặc đã logout
    refresh_token_hash  VARCHAR(255),

    -- Thời gian hết hạn của refresh token
    refresh_token_expires_at TIMESTAMPTZ,

    -- Thời gian đăng nhập lần cuối — audit & security monitoring
    last_login_at       TIMESTAMPTZ,

    -- Số lần đăng nhập thất bại liên tiếp — lockout sau 5 lần
    failed_login_count  SMALLINT        NOT NULL DEFAULT 0,

    -- Thời gian khóa tài khoản (NULL = không bị khóa)
    locked_until        TIMESTAMPTZ,

    -- Metadata
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Soft delete
    deleted_at          TIMESTAMPTZ,

    -- Người tạo tài khoản này (NULL nếu tự đăng ký hoặc seed)
    created_by_user_id  UUID
                            REFERENCES operational.users(id)
                            ON DELETE SET NULL
);

COMMENT ON TABLE  operational.users                      IS 'Tài khoản người dùng — RBAC: ADMIN/COMPANY_ADMIN/OPERATOR';
COMMENT ON COLUMN operational.users.password_hash        IS 'bcrypt hash với cost=12. KHÔNG return qua API, KHÔNG log';
COMMENT ON COLUMN operational.users.assigned_port_id     IS 'Port phụ trách. NULL = ADMIN (xem tất cả port)';
COMMENT ON COLUMN operational.users.refresh_token_hash   IS 'Hash của refresh token để validate. NULL = logged out';
COMMENT ON COLUMN operational.users.failed_login_count   IS 'Reset về 0 sau khi login thành công. Lockout sau 5 lần';
COMMENT ON COLUMN operational.users.deleted_at           IS 'Soft delete timestamp. NULL = chưa xóa. Query luôn filter deleted_at IS NULL';


-- ============================================================
-- 3.4 BẢNG: operational.weather_readings
-- Dữ liệu thời tiết raw từ OpenWeather API
-- Được insert bởi BackgroundService (BE-C) mỗi 15 phút
-- Prefect ETL đọc bảng này để load vào Data Warehouse
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.weather_readings (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng được đo
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- ── DỮ LIỆU GIÓ ──────────────────────────────────────────
    -- Tốc độ gió raw từ API (m/s) — lưu raw để tính lại nếu thuật toán đổi
    wind_speed_ms       DECIMAL(6, 2)   NOT NULL
                            CONSTRAINT weather_wind_speed_positive
                            CHECK (wind_speed_ms >= 0),

    -- Cấp Beaufort tương ứng (0–12) — tính từ wind_speed_ms theo WMO table
    beaufort_number     SMALLINT        NOT NULL
                            CONSTRAINT weather_beaufort_range
                            CHECK (beaufort_number BETWEEN 0 AND 12),

    -- Hướng gió (độ, 0–360) — 0/360=Bắc, 90=Đông, 180=Nam, 270=Tây
    wind_direction_deg  SMALLINT
                            CONSTRAINT weather_wind_dir_range
                            CHECK (wind_direction_deg BETWEEN 0 AND 360),

    -- Tốc độ gió giật (m/s) — thường cao hơn wind_speed_ms
    wind_gust_ms        DECIMAL(6, 2)
                            CONSTRAINT weather_wind_gust_positive
                            CHECK (wind_gust_ms IS NULL OR wind_gust_ms >= 0),

    -- ── DỮ LIỆU MƯA ──────────────────────────────────────────
    -- Lượng mưa 1 giờ qua (mm/h) — NULL nếu không có mưa
    rainfall_1h_mm      DECIMAL(7, 2)
                            CONSTRAINT weather_rainfall_positive
                            CHECK (rainfall_1h_mm IS NULL OR rainfall_1h_mm >= 0),

    -- Lượng mưa 3 giờ qua (mm/3h) — từ OpenWeather forecast
    rainfall_3h_mm      DECIMAL(7, 2),

    -- ── DỮ LIỆU NHIỆT ĐỘ & ĐỘ ẨM ───────────────────────────
    -- Nhiệt độ (°C) — dùng cho báo cáo và đánh giá cảnh báo nhiệt
    temperature_c       DECIMAL(5, 2),

    -- Độ ẩm (%) — 0–100
    humidity_pct        SMALLINT
                            CONSTRAINT weather_humidity_range
                            CHECK (humidity_pct IS NULL OR humidity_pct BETWEEN 0 AND 100),

    -- ── DỮ LIỆU TẦẦM NHÌN & ÁP SUẤT ────────────────────────
    -- Tầm nhìn (km) — quan trọng cho điều hướng tàu
    visibility_km       DECIMAL(6, 2)
                            CONSTRAINT weather_visibility_positive
                            CHECK (visibility_km IS NULL OR visibility_km >= 0),

    -- Áp suất khí quyển (hPa) — dấu hiệu bão khi giảm nhanh
    pressure_hpa        DECIMAL(7, 2),

    -- ── MÃ THỜI TIẾT ─────────────────────────────────────────
    -- OpenWeather weather condition code (ví dụ: 800=clear, 200=thunderstorm)
    -- Tham khảo: https://openweathermap.org/weather-conditions
    ow_weather_code     SMALLINT,

    -- Mô tả thời tiết từ API (ví dụ: "moderate rain", "strong breeze")
    ow_weather_desc     VARCHAR(100),
    ow_weather_icon     VARCHAR(20),

    -- ── METADATA FETCH ────────────────────────────────────────
    -- Thời điểm quan trắc theo OpenWeather (có thể khác recorded_at)
    observed_at         TIMESTAMPTZ     NOT NULL,

    -- Thời điểm hệ thống insert record này vào DB
    recorded_at         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Nguồn dữ liệu: 'OPENWEATHER_API', 'MANUAL', 'SIMULATION'
    data_source         VARCHAR(50)     NOT NULL DEFAULT 'OPENWEATHER_API',

    -- Raw JSON response từ API — lưu để debug hoặc re-process
    raw_payload         JSONB,

    -- Đánh dấu record từ simulation mode (không dùng cho analytics thật)
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE
);

ALTER TABLE operational.weather_readings
    ADD COLUMN IF NOT EXISTS ow_weather_icon VARCHAR(20);

COMMENT ON TABLE  operational.weather_readings               IS 'Dữ liệu thời tiết raw từ OpenWeather API — insert mỗi 15 phút/port';
COMMENT ON COLUMN operational.weather_readings.beaufort_number IS 'Tính từ wind_speed_ms theo WMO: 0–5=LOW, 6–7=MEDIUM, 8–9=HIGH, 10–12=CRITICAL';
COMMENT ON COLUMN operational.weather_readings.observed_at   IS 'Timestamp theo OpenWeather (dt field) — UTC';
COMMENT ON COLUMN operational.weather_readings.recorded_at   IS 'Timestamp khi hệ thống insert — dùng để detect ETL lag';
COMMENT ON COLUMN operational.weather_readings.raw_payload   IS 'JSON raw response để debug/re-process khi thuật toán thay đổi';
COMMENT ON COLUMN operational.weather_readings.is_simulation  IS 'TRUE nếu từ simulation mode — loại khỏi analytics thật';


-- ============================================================
-- 3.5 TABLE: operational.weather_fetch_jobs
-- Sprint 2 fetch monitoring for BackgroundService and Prefect runs
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.weather_fetch_jobs (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- Nullable until operational.weather_sources is merged
    source_id           UUID,

    status              VARCHAR(20)     NOT NULL DEFAULT 'PENDING'
                            CONSTRAINT weather_fetch_job_status_valid
                            CHECK (status IN ('PENDING', 'RUNNING', 'SUCCESS', 'FAILED', 'SKIPPED')),

    started_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    completed_at        TIMESTAMPTZ,
    response_time_ms    INTEGER
                            CONSTRAINT weather_fetch_response_time_positive
                            CHECK (response_time_ms IS NULL OR response_time_ms >= 0),
    http_status_code    INTEGER,
    error_message       TEXT,

    created_reading_id  UUID
                            REFERENCES operational.weather_readings(id)
                            ON DELETE SET NULL,

    prefect_flow_run_id VARCHAR(100)
);

COMMENT ON TABLE operational.weather_fetch_jobs IS
    'Fetch monitoring for OpenWeather BackgroundService and Prefect runs';
COMMENT ON COLUMN operational.weather_fetch_jobs.source_id IS
    'Weather source UUID; nullable while weather_sources is absent from the current schema';
COMMENT ON COLUMN operational.weather_fetch_jobs.created_reading_id IS
    'Weather reading created by a successful fetch';


-- ============================================================
-- 3.5 BẢNG: operational.risk_thresholds
-- Cấu hình ngưỡng đánh giá rủi ro — Admin có thể chỉnh qua UI
-- Risk Engine đọc bảng này mỗi lần đánh giá (cached in-memory 5 phút)
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.risk_thresholds (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- Yếu tố thời tiết được cấu hình ngưỡng
    factor              operational.weather_factor_enum NOT NULL,

    -- Mức rủi ro tương ứng với khoảng giá trị này
    risk_level          operational.risk_level_enum NOT NULL,

    -- Giá trị tối thiểu của khoảng (inclusive)
    min_value           DECIMAL(10, 3)  NOT NULL,

    -- Giá trị tối đa của khoảng (exclusive — [min, max))
    -- NULL = không có giới hạn trên (ví dụ: CRITICAL ≥ 24.5 m/s)
    max_value           DECIMAL(10, 3),

    -- Đơn vị đo (m/s, mm/h, km, hPa)
    unit                VARCHAR(20)     NOT NULL,

    -- Mô tả để Admin hiểu khi chỉnh sửa trên UI
    description         TEXT,

    -- Trạng thái: FALSE = tạm vô hiệu hóa rule này
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,

    -- Metadata
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Người cập nhật gần nhất
    updated_by_user_id  UUID
                            REFERENCES operational.users(id)
                            ON DELETE SET NULL,

    -- Đảm bảo không trùng factor + risk_level
    CONSTRAINT risk_thresholds_unique_factor_level
        UNIQUE (factor, risk_level)
);

COMMENT ON TABLE  operational.risk_thresholds              IS 'Ngưỡng đánh giá rủi ro — configurable qua Admin UI, không cần redeploy';
COMMENT ON COLUMN operational.risk_thresholds.factor       IS 'Yếu tố: WIND (Beaufort), RAIN (mm/h), WAVE (m), VISIBILITY (km)';
COMMENT ON COLUMN operational.risk_thresholds.min_value    IS 'Ngưỡng dưới inclusive: risk_level áp dụng khi value >= min_value';
COMMENT ON COLUMN operational.risk_thresholds.max_value    IS 'Ngưỡng trên exclusive. NULL = không giới hạn trên';


-- ============================================================
-- 3.6 BẢNG: operational.risk_assessments
-- Kết quả đánh giá rủi ro — output của Risk Engine
-- Mỗi record = một lần Risk Engine chạy cho một port
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.risk_assessments (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng được đánh giá
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- FK: weather_reading dùng làm input
    weather_reading_id  UUID            NOT NULL
                            REFERENCES operational.weather_readings(id)
                            ON DELETE RESTRICT,

    -- ── KẾT QUẢ ĐÁNH GIÁ ─────────────────────────────────────
    -- Risk level cuối cùng (worst-case của tất cả factors)
    final_risk_level    operational.risk_level_enum NOT NULL,

    -- Risk level từng yếu tố riêng lẻ (để giải thích lý do)
    wind_risk_level     operational.risk_level_enum NOT NULL,
    rain_risk_level     operational.risk_level_enum NOT NULL,
    visibility_risk_level operational.risk_level_enum,

    -- Giá trị input đã dùng (snapshot — tránh join lại weather_readings)
    beaufort_number     SMALLINT        NOT NULL,
    rainfall_1h_mm      DECIMAL(7, 2),
    visibility_km       DECIMAL(6, 2),

    -- Risk level trước đó — để detect khi level thay đổi (trigger event)
    previous_risk_level operational.risk_level_enum,

    -- Có sự thay đổi risk level so với lần đánh giá trước không?
    level_changed       BOOLEAN         NOT NULL DEFAULT FALSE,

    -- Lý do đánh giá (tóm tắt để hiển thị trên UI)
    -- VD: "Wind Beaufort 8 (HIGH) exceeded threshold. Rain 30mm/h (HIGH)."
    assessment_summary  TEXT,

    -- Thời điểm đánh giá
    evaluated_at        TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Đánh dấu simulation
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE
);

COMMENT ON TABLE  operational.risk_assessments               IS 'Kết quả Risk Engine — mỗi lần fetch weather → 1 assessment record/port';
COMMENT ON COLUMN operational.risk_assessments.final_risk_level IS 'Worst-case: MAX(wind_risk, rain_risk, visibility_risk)';
COMMENT ON COLUMN operational.risk_assessments.level_changed  IS 'TRUE khi final_risk_level ≠ previous_risk_level → trigger SOP Engine';
COMMENT ON COLUMN operational.risk_assessments.assessment_summary IS 'Giải thích bằng text để hiển thị trên UI dashboard';


-- ============================================================
-- 3.6.1 BẢNG: operational.risk_assessment_details
-- Chi tiết đánh giá từng factor của mỗi RiskAssessment
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.risk_assessment_details (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    assessment_id       UUID            NOT NULL
                            REFERENCES operational.risk_assessments(id)
                            ON DELETE CASCADE,

    factor              operational.weather_factor_enum NOT NULL,
    raw_value           DECIMAL(10, 3)  NOT NULL,
    beaufort_number     SMALLINT,
    risk_level          operational.risk_level_enum NOT NULL,
    unit                VARCHAR(20)     NOT NULL,
    threshold_applied   TEXT            NOT NULL,

    CONSTRAINT risk_assessment_details_unique_factor
        UNIQUE (assessment_id, factor)
);

COMMENT ON TABLE operational.risk_assessment_details IS
    'Chi tiết Risk Engine theo từng factor WIND/RAIN/VISIBILITY cho một assessment';


-- ============================================================
-- 3.7 BẢNG: operational.sop_rules
-- Bộ quy tắc SOP — map (risk_level, zone_type) → action
-- SOP Engine đọc bảng này khi RiskChangedEvent xảy ra
-- Admin có thể thêm/sửa/vô hiệu hóa rules qua UI
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.sop_rules (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- Tên rule để nhận biết (hiển thị trong log và admin UI)
    rule_name           VARCHAR(255)    NOT NULL,

    -- ── ĐIỀU KIỆN KÍCH HOẠT ──────────────────────────────────
    -- Risk level kích hoạt rule này
    -- NULL = áp dụng cho mọi risk level (hiếm dùng)
    trigger_risk_level  operational.risk_level_enum NOT NULL,

    -- Loại zone áp dụng rule này
    -- NULL = áp dụng cho tất cả zone types trong port
    applies_to_zone_type operational.zone_type_enum,

    -- ── HÀNH ĐỘNG THỰC HIỆN ──────────────────────────────────
    -- Loại action được thực hiện
    action_type         operational.sop_action_type_enum NOT NULL,

    -- Mô tả chi tiết action (dùng khi action_type = CUSTOM hoặc bổ sung)
    action_description  TEXT            NOT NULL,

    -- ── CHẾ ĐỘ VẬN HÀNH MỚI ─────────────────────────────────
    -- Mode mới sẽ áp dụng khi rule này trigger
    -- NULL = rule này không thay đổi operation mode
    target_operation_mode operational.operation_mode_enum,

    -- ── ƯU TIÊN ──────────────────────────────────────────────
    -- Thứ tự thực hiện khi nhiều rules cùng trigger (thấp hơn = trước)
    execution_order     SMALLINT        NOT NULL DEFAULT 100,

    -- ── THÔNG BÁO ─────────────────────────────────────────────
    -- Nội dung alert message khi rule trigger
    alert_message       TEXT,

    -- Mức nghiêm trọng của alert tương ứng
    alert_severity      operational.alert_severity_enum NOT NULL DEFAULT 'WARNING',

    -- ── TRẠNG THÁI ────────────────────────────────────────────
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,

    -- Metadata
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_by_user_id  UUID
                            REFERENCES operational.users(id)
                            ON DELETE SET NULL
);

COMMENT ON TABLE  operational.sop_rules                        IS 'Bộ quy tắc SOP — configurable, không cần redeploy khi thêm/sửa rule';
COMMENT ON COLUMN operational.sop_rules.trigger_risk_level     IS 'Rule chỉ trigger khi risk level của port = trigger_risk_level';
COMMENT ON COLUMN operational.sop_rules.applies_to_zone_type   IS 'NULL = áp dụng tất cả zone. Nếu có value = chỉ áp dụng zone type đó';
COMMENT ON COLUMN operational.sop_rules.target_operation_mode  IS 'NULL = không đổi mode. Có value = set mode mới sau khi rule trigger';
COMMENT ON COLUMN operational.sop_rules.execution_order        IS 'Ascending: 1 chạy trước 100. Rules cùng order chạy song song';


-- ============================================================
-- 3.8 BẢNG: operational.operation_mode_log
-- Lịch sử thay đổi operation mode của cảng
-- State machine: NORMAL → LIMITED → STOP (không skip, không reverse tự động)
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.operation_mode_log (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng thay đổi mode
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- Mode trước khi thay đổi (NULL cho record đầu tiên khi khởi tạo)
    previous_mode       operational.operation_mode_enum,

    -- Mode mới sau khi thay đổi
    new_mode            operational.operation_mode_enum NOT NULL,

    -- ── LÝ DO THAY ĐỔI ───────────────────────────────────────
    -- Risk level đã trigger sự thay đổi này
    triggered_by_risk_level operational.risk_level_enum,

    -- FK: SOP rule đã trigger (NULL nếu thay đổi thủ công bởi admin)
    triggered_by_sop_rule_id UUID
                            REFERENCES operational.sop_rules(id)
                            ON DELETE SET NULL,

    -- Nếu thay đổi thủ công: FK user đã override
    overridden_by_user_id UUID
                            REFERENCES operational.users(id)
                            ON DELETE SET NULL,

    -- Ghi chú của admin khi override thủ công
    override_reason     TEXT,

    -- ── LOẠI THAY ĐỔI ────────────────────────────────────────
    -- AUTOMATIC: do SOP Engine trigger
    -- MANUAL   : do Admin/CompanyAdmin override
    change_type         VARCHAR(20)     NOT NULL DEFAULT 'AUTOMATIC'
                            CONSTRAINT operation_mode_change_type
                            CHECK (change_type IN ('AUTOMATIC', 'MANUAL')),

    -- Thời điểm thay đổi
    changed_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Đánh dấu simulation
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE
);

COMMENT ON TABLE  operational.operation_mode_log                      IS 'Lịch sử thay đổi operation mode — state machine NORMAL→LIMITED→STOP';
COMMENT ON COLUMN operational.operation_mode_log.triggered_by_sop_rule_id IS 'NULL = thay đổi thủ công bởi admin. Có value = automatic';
COMMENT ON COLUMN operational.operation_mode_log.override_reason      IS 'Ghi chú bắt buộc khi admin override thủ công — audit trail';


-- ============================================================
-- 3.9 BẢNG: operational.task_logs
-- Task được tạo tự động khi SOP rule trigger
-- Không có assignment (không phải task management system)
-- Mục đích: ghi lại "hệ thống đã khuyến nghị làm gì, lúc nào"
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.task_logs (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng liên quan
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- FK: zone cụ thể (nếu rule áp dụng cho zone type cụ thể)
    zone_id             UUID
                            REFERENCES operational.zones(id)
                            ON DELETE SET NULL,

    -- FK: SOP rule đã trigger task này
    triggered_by_rule_id UUID           NOT NULL
                            REFERENCES operational.sop_rules(id)
                            ON DELETE RESTRICT,

    -- FK: risk assessment đã trigger rule
    triggered_by_assessment_id UUID
                            REFERENCES operational.risk_assessments(id)
                            ON DELETE SET NULL,

    -- Loại action (copy từ sop_rules để tránh join)
    action_type         operational.sop_action_type_enum NOT NULL,

    -- Mô tả hành động cần thực hiện (copy từ sop_rules)
    action_description  TEXT            NOT NULL,

    -- Risk level tại thời điểm task được tạo
    risk_level_at_creation operational.risk_level_enum NOT NULL,

    -- Thời điểm task được tạo (auto)
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Đánh dấu simulation
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE
);

COMMENT ON TABLE  operational.task_logs                           IS 'Task tự động do SOP Engine tạo — log "hệ thống khuyến nghị làm gì"';
COMMENT ON COLUMN operational.task_logs.zone_id                   IS 'NULL nếu rule áp dụng toàn cảng. Có value nếu rule theo zone type';
COMMENT ON COLUMN operational.task_logs.triggered_by_rule_id      IS 'NOT NULL — mọi task đều phải có nguồn gốc từ SOP rule';


-- ============================================================
-- 3.10 BẢNG: operational.alerts
-- Thông báo cảnh báo gửi tới người dùng
-- Frontend polling GET /alerts mỗi 30 giây
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.alerts (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng liên quan
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- Loại alert để frontend hiển thị icon/màu phù hợp
    alert_type          VARCHAR(50)     NOT NULL,
    -- Các giá trị thông dụng:
    --   'RISK_LEVEL_CHANGED', 'MODE_CHANGED', 'STORM_WARNING',
    --   'THRESHOLD_EXCEEDED', 'SYSTEM_ERROR', 'SOP_TRIGGERED'

    -- Mức nghiêm trọng — quyết định màu sắc và âm thanh trên UI
    severity            operational.alert_severity_enum NOT NULL,

    -- Tiêu đề ngắn — hiển thị trong notification bell
    title               VARCHAR(255)    NOT NULL,

    -- Nội dung chi tiết — hiển thị khi mở alert
    message             TEXT            NOT NULL,

    -- Dữ liệu bổ sung (risk level mới, zone bị ảnh hưởng, v.v.)
    metadata            JSONB,

    -- FK: SOP rule liên quan (nếu alert từ SOP trigger)
    related_sop_rule_id UUID
                            REFERENCES operational.sop_rules(id)
                            ON DELETE SET NULL,

    -- FK: Risk assessment liên quan
    related_assessment_id UUID
                            REFERENCES operational.risk_assessments(id)
                            ON DELETE SET NULL,

    -- Thời điểm tạo alert
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Thời điểm đọc (NULL = chưa đọc)
    read_at             TIMESTAMPTZ,

    -- FK: Người đã đọc alert
    read_by_user_id     UUID
                            REFERENCES operational.users(id)
                            ON DELETE SET NULL,

    -- Đánh dấu simulation
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE
);

COMMENT ON TABLE  operational.alerts               IS 'Thông báo cảnh báo — frontend polling mỗi 30 giây';
COMMENT ON COLUMN operational.alerts.alert_type    IS 'String enum mở — không dùng PG ENUM để dễ thêm type mới';
COMMENT ON COLUMN operational.alerts.metadata      IS 'JSONB payload: {new_risk_level, zone_id, beaufort, ...}';
COMMENT ON COLUMN operational.alerts.read_at       IS 'NULL = chưa đọc. PATCH /alerts/{id}/read set field này';


-- ============================================================
-- 3.11 BẢNG: operational.operation_events
-- Audit log tổng hợp TOÀN BỘ events trong hệ thống
-- Append-only — KHÔNG UPDATE/DELETE records
-- Dùng để: debug, compliance, phân tích sau sự kiện
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.operation_events (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng liên quan (NULL nếu event ở cấp system)
    port_id             UUID
                            REFERENCES operational.ports(id)
                            ON DELETE SET NULL,

    -- Loại event
    event_type          operational.event_type_enum NOT NULL,

    -- Actor: user thực hiện (NULL nếu system/automated)
    actor_user_id       UUID
                            REFERENCES operational.users(id)
                            ON DELETE SET NULL,

    -- Payload chứa toàn bộ context của event
    -- Ví dụ RISK_LEVEL_CHANGED: {from: "LOW", to: "HIGH", beaufort: 8, ...}
    payload             JSONB           NOT NULL DEFAULT '{}',

    -- Mô tả ngắn dạng text — hiển thị trong Operation Log trên UI
    summary             TEXT,

    -- Thời điểm event xảy ra
    occurred_at         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    -- Đánh dấu simulation
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE
);

COMMENT ON TABLE  operational.operation_events              IS 'Audit log append-only — mọi event đều được ghi. KHÔNG UPDATE/DELETE';
COMMENT ON COLUMN operational.operation_events.actor_user_id IS 'NULL = system event (automated). Có value = human action';
COMMENT ON COLUMN operational.operation_events.payload      IS 'JSONB — flexible, chứa đủ context để re-construct event';
COMMENT ON COLUMN operational.operation_events.summary      IS 'Text ngắn để hiển thị trong log timeline trên UI';


-- ============================================================
-- 3.12 BẢNG: operational.simulation_sessions
-- Metadata của mỗi phiên simulation mode
-- BE-C tạo record khi POST /simulation/start
-- ============================================================
CREATE TABLE IF NOT EXISTS operational.simulation_sessions (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK: cảng đang simulate
    port_id             UUID            NOT NULL
                            REFERENCES operational.ports(id)
                            ON DELETE CASCADE,

    -- User khởi động simulation
    started_by_user_id  UUID            NOT NULL
                            REFERENCES operational.users(id)
                            ON DELETE RESTRICT,

    -- Mô tả kịch bản (ví dụ: "Bão Linda 10/2023 - Đà Nẵng")
    scenario_name       VARCHAR(255)    NOT NULL,

    -- Tốc độ replay (1 = real-time, 10 = nhanh gấp 10)
    speed_multiplier    SMALLINT        NOT NULL DEFAULT 10
                            CONSTRAINT sim_speed_range
                            CHECK (speed_multiplier BETWEEN 1 AND 100),

    -- Số lượng weather snapshots trong dataset
    total_snapshots     INTEGER         NOT NULL,

    -- Trạng thái: RUNNING, COMPLETED, CANCELLED
    status              VARCHAR(20)     NOT NULL DEFAULT 'RUNNING'
                            CONSTRAINT sim_status_check
                            CHECK (status IN ('RUNNING', 'COMPLETED', 'CANCELLED')),

    -- Thời điểm bắt đầu và kết thúc
    started_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    ended_at            TIMESTAMPTZ
);

COMMENT ON TABLE  operational.simulation_sessions                IS 'Metadata phiên simulation — 1 phiên = 1 kịch bản bão';
COMMENT ON COLUMN operational.simulation_sessions.speed_multiplier IS '10 = replay 15 phút data trong 90 giây (phù hợp cho demo 10 phút)';


-- ---------------------------------------------------------------------------
-- 4. SCHEMA ANALYTICS — DATA WAREHOUSE
-- ---------------------------------------------------------------------------
-- Star schema đơn giản:
--   fact_weather_readings  ← dim_time, dim_zones, dim_risk_levels
--   fact_operation_events  ← dim_time, dim_zones
-- Prefect ETL nạp data vào đây mỗi giờ
-- Metabase connect trực tiếp schema này

-- ============================================================
-- 4.1 DIMENSION: analytics.dim_time
-- Time dimension — granularity: giờ
-- Pre-populated từ 01/01/2025 đến 31/12/2027
-- ============================================================
CREATE TABLE IF NOT EXISTS analytics.dim_time (
    time_key            INTEGER         PRIMARY KEY,
    -- time_key = YYYYMMDDHH (ví dụ: 2026050714 = 07/05/2026 lúc 14h)

    -- Các thuộc tính thời gian để filter nhanh trên Metabase
    full_datetime       TIMESTAMPTZ     NOT NULL,
    date_only           DATE            NOT NULL,
    year                SMALLINT        NOT NULL,
    quarter             SMALLINT        NOT NULL CHECK (quarter BETWEEN 1 AND 4),
    month               SMALLINT        NOT NULL CHECK (month BETWEEN 1 AND 12),
    month_name          VARCHAR(20)     NOT NULL,  -- 'January', 'February', ...
    week_of_year        SMALLINT        NOT NULL,
    day_of_month        SMALLINT        NOT NULL CHECK (day_of_month BETWEEN 1 AND 31),
    day_of_week         SMALLINT        NOT NULL CHECK (day_of_week BETWEEN 1 AND 7),
    day_name            VARCHAR(20)     NOT NULL,  -- 'Monday', 'Tuesday', ...
    hour                SMALLINT        NOT NULL CHECK (hour BETWEEN 0 AND 23),
    is_weekend          BOOLEAN         NOT NULL,
    is_business_hour    BOOLEAN         NOT NULL   -- 8h–17h weekday
);

COMMENT ON TABLE  analytics.dim_time          IS 'Time dimension — pre-populated, granularity: giờ';
COMMENT ON COLUMN analytics.dim_time.time_key IS 'Surrogate key: YYYYMMDDHH. VD: 2026050714';


-- ============================================================
-- 4.2 DIMENSION: analytics.dim_zones
-- Zone dimension — slowly changing (Type 1: overwrite)
-- ============================================================
CREATE TABLE IF NOT EXISTS analytics.dim_zones (
    zone_key            UUID            PRIMARY KEY,
    -- zone_key = operational.zones.id (dùng UUID trực tiếp cho đơn giản)

    port_id             UUID            NOT NULL,
    port_name           VARCHAR(255)    NOT NULL,
    port_code           VARCHAR(20)     NOT NULL,
    zone_name           VARCHAR(255)    NOT NULL,
    zone_type           VARCHAR(50)     NOT NULL,
    is_active           BOOLEAN         NOT NULL,

    -- Sync metadata
    last_synced_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE analytics.dim_zones IS 'Zone dimension — synced từ operational.zones + ports mỗi giờ';


-- ============================================================
-- 4.3 DIMENSION: analytics.dim_risk_levels
-- Risk level dimension — static, seed một lần
-- ============================================================
CREATE TABLE IF NOT EXISTS analytics.dim_risk_levels (
    risk_level_key      SMALLINT        PRIMARY KEY,
    -- 1=LOW, 2=MEDIUM, 3=HIGH, 4=CRITICAL (để ORDER BY cho đúng)

    risk_level          VARCHAR(20)     NOT NULL UNIQUE,
    display_label       VARCHAR(50)     NOT NULL,  -- 'Low Risk', 'Medium Risk', ...
    color_hex           VARCHAR(7)      NOT NULL,  -- '#27AE60', '#F39C12', ...
    beaufort_min        SMALLINT        NOT NULL,
    beaufort_max        SMALLINT        NOT NULL,
    description         TEXT
);

COMMENT ON TABLE  analytics.dim_risk_levels           IS 'Risk level dimension — static seed data, dùng để join và filter trên Metabase';
COMMENT ON COLUMN analytics.dim_risk_levels.risk_level_key IS '1=LOW,2=MEDIUM,3=HIGH,4=CRITICAL — đảm bảo ORDER BY đúng chiều';


-- ============================================================
-- 4.4 FACT: analytics.fact_weather_readings
-- Fact table chứa dữ liệu thời tiết đã aggregate theo giờ
-- Prefect ETL load mỗi giờ từ operational.weather_readings
-- ============================================================
CREATE TABLE IF NOT EXISTS analytics.fact_weather_readings (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK dimensions
    time_key            INTEGER         NOT NULL
                            REFERENCES analytics.dim_time(time_key),
    zone_key            UUID            NOT NULL
                            REFERENCES analytics.dim_zones(zone_key),
    risk_level_key      SMALLINT        NOT NULL
                            REFERENCES analytics.dim_risk_levels(risk_level_key),

    -- FK operational (để trace ngược nếu cần)
    port_id             UUID            NOT NULL,

    -- ── MEASURES (aggregate theo giờ) ────────────────────────
    -- Tốc độ gió trung bình trong giờ (m/s)
    avg_wind_speed_ms   DECIMAL(6, 2),

    -- Beaufort cao nhất trong giờ (worst case cho risk)
    max_beaufort        SMALLINT,

    -- Tổng mưa trong giờ (mm)
    total_rainfall_mm   DECIMAL(7, 2),

    -- Số lần đánh giá risk trong giờ
    assessment_count    SMALLINT        NOT NULL DEFAULT 0,

    -- Số phút ở mỗi risk level trong giờ (tổng = 60 phút lý tưởng)
    minutes_at_low      SMALLINT        NOT NULL DEFAULT 0,
    minutes_at_medium   SMALLINT        NOT NULL DEFAULT 0,
    minutes_at_high     SMALLINT        NOT NULL DEFAULT 0,
    minutes_at_critical SMALLINT        NOT NULL DEFAULT 0,

    -- Risk level cuối giờ (để trending chart)
    final_risk_level    VARCHAR(20),

    -- Đánh dấu record từ simulation (loại khỏi báo cáo thật)
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE,

    -- ETL metadata
    etl_loaded_at       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    etl_batch_id        VARCHAR(100)    -- Prefect flow run ID để trace

);

COMMENT ON TABLE  analytics.fact_weather_readings              IS 'Fact bảng thời tiết — hourly aggregate từ operational.weather_readings';
COMMENT ON COLUMN analytics.fact_weather_readings.max_beaufort IS 'Beaufort cao nhất trong giờ — dùng cho worst-case risk analysis';
COMMENT ON COLUMN analytics.fact_weather_readings.minutes_at_critical IS 'Phút ở CRITICAL trong giờ — KPI quan trọng cho báo cáo an toàn';


-- ============================================================
-- 4.5 FACT: analytics.fact_operation_events
-- Fact table chứa events vận hành đã flatten từ JSONB payload
-- Dễ query hơn operational.operation_events cho Metabase
-- ============================================================
CREATE TABLE IF NOT EXISTS analytics.fact_operation_events (
    id                  UUID            PRIMARY KEY DEFAULT uuid_generate_v4(),

    -- FK dimensions
    time_key            INTEGER         NOT NULL
                            REFERENCES analytics.dim_time(time_key),
    zone_key            UUID
                            REFERENCES analytics.dim_zones(zone_key),

    -- FK operational
    port_id             UUID            NOT NULL,
    source_event_id     UUID            NOT NULL,  -- operational.operation_events.id

    -- ── EVENT DETAILS ─────────────────────────────────────────
    event_type          VARCHAR(50)     NOT NULL,

    -- Flattened fields từ JSONB payload (tránh Metabase phải parse JSON)
    risk_level_before   VARCHAR(20),
    risk_level_after    VARCHAR(20),
    mode_before         VARCHAR(20),
    mode_after          VARCHAR(20),
    sop_rule_name       VARCHAR(255),
    action_type         VARCHAR(50),
    actor_role          VARCHAR(50),   -- role của user thực hiện

    -- Thời điểm event
    occurred_at         TIMESTAMPTZ     NOT NULL,

    -- Đánh dấu simulation
    is_simulation       BOOLEAN         NOT NULL DEFAULT FALSE,

    -- ETL metadata
    etl_loaded_at       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    etl_batch_id        VARCHAR(100)
);

COMMENT ON TABLE  analytics.fact_operation_events            IS 'Fact bảng events — flattened từ operational.operation_events để Metabase dễ query';
COMMENT ON COLUMN analytics.fact_operation_events.risk_level_before IS 'Extracted từ payload JSONB — tránh Metabase phải tự parse JSON';


-- ---------------------------------------------------------------------------
-- 5. INDEXES
-- ---------------------------------------------------------------------------

-- ── operational.ports ──────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_ports_is_active
    ON operational.ports (is_active)
    WHERE is_active = TRUE;
-- Hầu hết query đều filter is_active = TRUE → partial index nhỏ hơn

-- ── operational.zones ──────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_zones_port_id
    ON operational.zones (port_id);
-- Hay join từ zones về ports

CREATE INDEX IF NOT EXISTS idx_zones_port_type
    ON operational.zones (port_id, zone_type);
-- SOP engine lookup: "zones của port X có type Y"

-- ── operational.users ──────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_users_email
    ON operational.users (email)
    WHERE deleted_at IS NULL;
-- Login lookup theo email — partial index bỏ qua deleted users

CREATE INDEX IF NOT EXISTS idx_users_port_role
    ON operational.users (assigned_port_id, role)
    WHERE deleted_at IS NULL;
-- RBAC check: user thuộc port nào, role gì

-- ── operational.weather_readings ───────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_weather_port_observed
    ON operational.weather_readings (port_id, observed_at DESC);
-- Query phổ biến nhất: "weather mới nhất của port X"

CREATE INDEX IF NOT EXISTS idx_weather_port_time_range
    ON operational.weather_readings (port_id, observed_at)
    WHERE is_simulation = FALSE;
-- Chart lịch sử 24h/7 ngày — loại simulation

-- ── operational.weather_fetch_jobs ─────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_weather_fetch_jobs_port_started
    ON operational.weather_fetch_jobs (port_id, started_at DESC);

CREATE INDEX IF NOT EXISTS idx_weather_fetch_jobs_status_started
    ON operational.weather_fetch_jobs (status, started_at DESC);

-- ── operational.risk_assessments ───────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_risk_port_evaluated
    ON operational.risk_assessments (port_id, evaluated_at DESC);
-- Risk history chart, current risk lookup

CREATE INDEX IF NOT EXISTS idx_risk_level_changed
    ON operational.risk_assessments (port_id, level_changed, evaluated_at DESC)
    WHERE level_changed = TRUE;
-- Alert generation: chỉ cần records khi level thay đổi

CREATE INDEX IF NOT EXISTS idx_risk_assessment_details_assessment
    ON operational.risk_assessment_details (assessment_id);
-- Risk details endpoint: lấy 3 factor details theo assessment

-- ── operational.sop_rules ──────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_sop_rules_lookup
    ON operational.sop_rules (trigger_risk_level, applies_to_zone_type, is_active);
-- SOP Engine lookup: "rules nào active, match risk level X và zone type Y"

-- ── operational.operation_mode_log ─────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_mode_log_port_time
    ON operational.operation_mode_log (port_id, changed_at DESC);
-- Current mode lookup, mode history

-- ── operational.task_logs ──────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_task_logs_port_time
    ON operational.task_logs (port_id, created_at DESC);
-- Task log timeline trên UI

CREATE INDEX IF NOT EXISTS idx_task_logs_zone
    ON operational.task_logs (zone_id, created_at DESC);
-- Filter task log theo zone

-- ── operational.alerts ─────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_alerts_port_unread
    ON operational.alerts (port_id, created_at DESC)
    WHERE read_at IS NULL;
-- Polling API GET /alerts: unread alerts mới nhất — partial index rất nhỏ

CREATE INDEX IF NOT EXISTS idx_alerts_port_history
    ON operational.alerts (port_id, created_at DESC);
-- Alert history page

-- ── operational.operation_events ───────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_events_port_time
    ON operational.operation_events (port_id, occurred_at DESC);
-- Operation log timeline

CREATE INDEX IF NOT EXISTS idx_events_type_port
    ON operational.operation_events (event_type, port_id, occurred_at DESC);
-- Filter log theo event type

CREATE INDEX IF NOT EXISTS idx_events_payload_gin
    ON operational.operation_events USING GIN (payload);
-- Full-text search trong JSONB payload (debug)

-- ── analytics fact tables ──────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_fact_weather_port_time
    ON analytics.fact_weather_readings (port_id, time_key DESC)
    WHERE is_simulation = FALSE;

CREATE INDEX IF NOT EXISTS idx_fact_events_port_type
    ON analytics.fact_operation_events (port_id, event_type, time_key DESC)
    WHERE is_simulation = FALSE;


-- ---------------------------------------------------------------------------
-- 6. TRIGGERS
-- ---------------------------------------------------------------------------

-- Hàm tự động cập nhật updated_at khi UPDATE
CREATE OR REPLACE FUNCTION operational.set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Áp dụng trigger cho tất cả bảng có updated_at
DO $$
DECLARE
    tbl TEXT;
BEGIN
    FOREACH tbl IN ARRAY ARRAY[
        'ports', 'zones', 'users', 'risk_thresholds', 'sop_rules'
    ] LOOP
        EXECUTE format('
            DROP TRIGGER IF EXISTS trg_%s_updated_at ON operational.%s;
            CREATE TRIGGER trg_%s_updated_at
                BEFORE UPDATE ON operational.%s
                FOR EACH ROW EXECUTE FUNCTION operational.set_updated_at();
        ', tbl, tbl, tbl, tbl);
    END LOOP;
END;
$$;

-- Trigger: Khi risk_assessments có level_changed=TRUE → cập nhật cache ports.current_risk_level
CREATE OR REPLACE FUNCTION operational.sync_port_risk_level()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.level_changed = TRUE THEN
        UPDATE operational.ports
        SET    current_risk_level = NEW.final_risk_level,
               updated_at         = NOW()
        WHERE  id = NEW.port_id;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_port_risk_level ON operational.risk_assessments;
CREATE TRIGGER trg_sync_port_risk_level
    AFTER INSERT ON operational.risk_assessments
    FOR EACH ROW EXECUTE FUNCTION operational.sync_port_risk_level();

COMMENT ON FUNCTION operational.sync_port_risk_level() IS
    'Tự động cập nhật ports.current_risk_level khi risk assessment mới có level_changed=TRUE';

-- Trigger: Khi operation_mode_log INSERT → cập nhật cache ports.current_mode
CREATE OR REPLACE FUNCTION operational.sync_port_mode()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE operational.ports
    SET    current_mode = NEW.new_mode,
           updated_at   = NOW()
    WHERE  id = NEW.port_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_port_mode ON operational.operation_mode_log;
CREATE TRIGGER trg_sync_port_mode
    AFTER INSERT ON operational.operation_mode_log
    FOR EACH ROW EXECUTE FUNCTION operational.sync_port_mode();

COMMENT ON FUNCTION operational.sync_port_mode() IS
    'Tự động cập nhật ports.current_mode khi có mode log mới — giữ cache đồng bộ';


-- ---------------------------------------------------------------------------
-- 7. SEED DATA — DIM TABLES & INITIAL CONFIG
-- ---------------------------------------------------------------------------

-- ── 7.1 Risk level dimension (static) ─────────────────────────────────────
INSERT INTO analytics.dim_risk_levels
    (risk_level_key, risk_level, display_label, color_hex, beaufort_min, beaufort_max, description)
VALUES
    (1, 'LOW',      'Low Risk',      '#27AE60', 0, 5,  'Beaufort 0–5 (0–10.7 m/s): Hoạt động bình thường, không hạn chế'),
    (2, 'MEDIUM',   'Medium Risk',   '#F39C12', 6, 7,  'Beaufort 6–7 (10.8–17.1 m/s): Tăng cường giám sát, cảnh báo sớm'),
    (3, 'HIGH',     'High Risk',     '#E67E22', 8, 9,  'Beaufort 8–9 (17.2–24.4 m/s): Hạn chế bốc xếp, tàu lớn, thiết bị cao'),
    (4, 'CRITICAL', 'Critical Risk', '#C0392B', 10, 12,'Beaufort 10–12 (>24.5 m/s): Dừng toàn bộ, sơ tán, kích hoạt khẩn cấp')
ON CONFLICT (risk_level_key) DO UPDATE
    SET display_label = EXCLUDED.display_label,
        color_hex     = EXCLUDED.color_hex,
        description   = EXCLUDED.description;

-- ── 7.2 Risk thresholds (configurable defaults) ────────────────────────────
-- WIND — theo thang Beaufort (WMO)
INSERT INTO operational.risk_thresholds
    (factor, risk_level, min_value, max_value, unit, description)
VALUES
    ('WIND', 'LOW',      0,    10.8,  'm/s', 'Beaufort 0–5: Gió yếu đến gió nhẹ — vận hành bình thường'),
    ('WIND', 'MEDIUM',   10.8, 17.2,  'm/s', 'Beaufort 6–7: Gió mạnh đến gió rất mạnh — tăng giám sát'),
    ('WIND', 'HIGH',     17.2, 24.5,  'm/s', 'Beaufort 8–9: Gió to đến gió rất to — hạn chế hoạt động'),
    ('WIND', 'CRITICAL', 24.5, NULL,  'm/s', 'Beaufort 10–12: Bão — dừng toàn bộ hoạt động')
ON CONFLICT (factor, risk_level) DO UPDATE
    SET min_value   = EXCLUDED.min_value,
        max_value   = EXCLUDED.max_value,
        description = EXCLUDED.description;

-- RAIN — theo cường độ mưa QCVN/WMO
INSERT INTO operational.risk_thresholds
    (factor, risk_level, min_value, max_value, unit, description)
VALUES
    ('RAIN', 'LOW',      0,    10,    'mm/h', 'Mưa nhỏ: < 10mm/h — không ảnh hưởng đáng kể'),
    ('RAIN', 'MEDIUM',   10,   25,    'mm/h', 'Mưa vừa: 10–25mm/h — giảm tầm nhìn, trơn trượt'),
    ('RAIN', 'HIGH',     25,   50,    'mm/h', 'Mưa to: 25–50mm/h — nguy hiểm cho bốc xếp, crane'),
    ('RAIN', 'CRITICAL', 50,   NULL,  'mm/h', 'Mưa rất to: > 50mm/h — nguy cơ lũ, dừng hoạt động')
ON CONFLICT (factor, risk_level) DO UPDATE
    SET min_value   = EXCLUDED.min_value,
        max_value   = EXCLUDED.max_value,
        description = EXCLUDED.description;

-- VISIBILITY
INSERT INTO operational.risk_thresholds
    (factor, risk_level, min_value, max_value, unit, description)
VALUES
    ('VISIBILITY', 'LOW',      10,   NULL,  'km', 'Tầm nhìn tốt: > 10km — vận hành bình thường'),
    ('VISIBILITY', 'MEDIUM',   5,    10,    'km', 'Tầm nhìn trung bình: 5–10km — cẩn thận điều hướng'),
    ('VISIBILITY', 'HIGH',     1,    5,     'km', 'Tầm nhìn kém: 1–5km — hạn chế tàu vào/ra'),
    ('VISIBILITY', 'CRITICAL', 0,    1,     'km', 'Tầm nhìn rất kém: < 1km — dừng điều hướng tàu')
ON CONFLICT (factor, risk_level) DO UPDATE
    SET min_value   = EXCLUDED.min_value,
        max_value   = EXCLUDED.max_value,
        description = EXCLUDED.description;

-- ── 7.3 SOP Rules mẫu ─────────────────────────────────────────────────────
-- Đây là 12 rules cơ bản cho cảng container quy mô vừa
-- Admin có thể chỉnh sửa/thêm qua UI sau khi deploy
INSERT INTO operational.sop_rules
    (rule_name, trigger_risk_level, applies_to_zone_type,
     action_type, action_description, target_operation_mode,
     execution_order, alert_message, alert_severity)
VALUES
    -- ─── MEDIUM RISK ───────────────────────────────────────────────────────
    (
        'MEDIUM-001: Tăng cường giám sát toàn cảng',
        'MEDIUM', NULL,
        'CUSTOM',
        'Thông báo tất cả giám sát viên trực ca về điều kiện gió tăng. Kiểm tra tình trạng neo buộc các thiết bị di động trên sân bãi.',
        'LIMITED',
        10,
        'Gió tăng lên cấp 6–7 (MEDIUM). Chuyển sang chế độ vận hành hạn chế. Tăng cường kiểm tra thiết bị.',
        'WARNING'
    ),
    (
        'MEDIUM-002: Hạn chế thiết bị nâng cao tại DOCK',
        'MEDIUM', 'DOCK',
        'STOP_LOADING',
        'Tạm dừng hoạt động của crane và thiết bị nâng cao hơn 15m tại khu vực cầu tàu. Chuyển sang bốc xếp mặt đất.',
        NULL,
        20,
        'Hạn chế thiết bị nâng cao tại Dock do gió cấp 6–7.',
        'WARNING'
    ),
    (
        'MEDIUM-003: Cảnh báo tàu neo đậu',
        'MEDIUM', 'DOCK',
        'NOTIFY_AUTHORITY',
        'Thông báo tất cả tàu đang neo đậu tại cầu tàu kiểm tra và siết chặt hệ thống dây neo. Chuẩn bị phương án rời cảng nếu gió tăng tiếp.',
        NULL,
        30,
        'Yêu cầu kiểm tra neo buộc tàu tại Dock.',
        'INFO'
    ),

    -- ─── HIGH RISK ─────────────────────────────────────────────────────────
    (
        'HIGH-001: Dừng bốc xếp container tại DOCK',
        'HIGH', 'DOCK',
        'STOP_LOADING',
        'Dừng ngay toàn bộ hoạt động bốc xếp container tại khu vực cầu tàu. Hạ boom crane xuống vị trí an toàn. Rút nhân viên khỏi khu vực mặt cầu tàu.',
        NULL,
        10,
        'Dừng bốc xếp tại Dock — Gió cấp 8–9 (HIGH). Hạ boom crane.',
        'CRITICAL'
    ),
    (
        'HIGH-002: Hạn chế tàu vào cảng',
        'HIGH', 'DOCK',
        'LIMIT_VESSEL_ENTRY',
        'Chỉ cho phép tàu trọng tải dưới 5000 DWT vào cảng. Tàu lớn hơn phải neo ngoài vùng đợi cho đến khi điều kiện thời tiết cải thiện.',
        NULL,
        20,
        'Hạn chế tàu > 5000 DWT vào cảng. Gió HIGH.',
        'CRITICAL'
    ),
    (
        'HIGH-003: Di chuyển thiết bị tại YARD',
        'HIGH', 'YARD',
        'EVACUATE_EQUIPMENT',
        'Di chuyển xe nâng, xe straddle carrier và thiết bị di động về nhà để và vị trí trú ẩn. Chằng buộc container hàng trên bãi. Không để xe không người lái trên bãi hở.',
        NULL,
        15,
        'Di chuyển thiết bị YARD về nơi trú ẩn. Gió HIGH.',
        'CRITICAL'
    ),
    (
        'HIGH-004: Chuyển sang chế độ STOP',
        'HIGH', NULL,
        'CUSTOM',
        'Chuyển toàn bộ cảng sang chế độ dừng vận hành. Thông báo tất cả nhân viên hoàn thành công việc hiện tại và về vị trí an toàn trong vòng 30 phút.',
        'STOP',
        50,
        'Cảng chuyển sang chế độ STOP — Gió HIGH. Nhân viên về vị trí an toàn.',
        'CRITICAL'
    ),

    -- ─── CRITICAL RISK ─────────────────────────────────────────────────────
    (
        'CRITICAL-001: Đóng cổng cảng',
        'CRITICAL', 'GATE',
        'CLOSE_GATE',
        'Đóng ngay toàn bộ cổng ra vào cảng. Không tiếp nhận xe tải, phương tiện mới vào cảng. Cho phép phương tiện đang trong cảng ra khẩn cấp nếu an toàn.',
        NULL,
        5,
        'KHẨN CẤP: Đóng toàn bộ cổng cảng — Bão cấp 10+.',
        'CRITICAL'
    ),
    (
        'CRITICAL-002: Sơ tán thiết bị khẩn cấp',
        'CRITICAL', NULL,
        'EVACUATE_EQUIPMENT',
        'Kích hoạt quy trình sơ tán thiết bị khẩn cấp. Di chuyển tất cả crane, thiết bị di động vào nhà để. Hạ và cố định tất cả boom crane. Ngắt nguồn điện các thiết bị ngoài trời.',
        NULL,
        10,
        'KHẨN CẤP: Sơ tán thiết bị toàn cảng — Bão CRITICAL.',
        'CRITICAL'
    ),
    (
        'CRITICAL-003: Thông báo cơ quan quản lý',
        'CRITICAL', NULL,
        'NOTIFY_AUTHORITY',
        'Thông báo ngay cho Cảng vụ địa phương, Đội tìm kiếm cứu nạn, và Ban quản lý cảng về tình trạng thời tiết khẩn cấp. Kích hoạt đường dây khẩn cấp 24/7.',
        NULL,
        15,
        'Đã thông báo cơ quan chức năng về tình trạng bão CRITICAL.',
        'CRITICAL'
    ),
    (
        'CRITICAL-004: Tắt khẩn cấp và sơ tán nhân viên',
        'CRITICAL', NULL,
        'EMERGENCY_SHUTDOWN',
        'Tắt toàn bộ hệ thống điện không thiết yếu. Sơ tán tất cả nhân viên về nhà tránh bão hoặc rời khỏi khu vực cảng theo phương án đã chuẩn bị. Chỉ giữ lại đội ứng phó khẩn cấp tối thiểu.',
        'STOP',
        100,
        'KHẨN CẤP: Tắt hệ thống và sơ tán nhân viên — Bão cấp CRITICAL.',
        'CRITICAL'
    ),

    -- ─── KHÔI PHỤC ─────────────────────────────────────────────────────────
    (
        'RECOVERY-001: Thông báo khôi phục về LOW',
        'LOW', NULL,
        'CUSTOM',
        'Điều kiện thời tiết đã trở về bình thường. Thông báo các đội vận hành có thể khôi phục hoạt động theo quy trình tiêu chuẩn. Kiểm tra thiết bị và cơ sở hạ tầng trước khi vận hành lại.',
        'NORMAL',
        200,
        'Thời tiết trở về bình thường (LOW). Có thể khôi phục vận hành sau khi kiểm tra.',
        'INFO'
    )

ON CONFLICT DO NOTHING;

-- ── 7.4 Port mẫu (Cảng Đà Nẵng) ──────────────────────────────────────────
INSERT INTO operational.ports
    (name, code, address, latitude, longitude, timezone)
VALUES
    (
        'Cảng Tiên Sa - Đà Nẵng',
        'DNTSA',
        'Bán đảo Sơn Trà, phường Thọ Quang, quận Sơn Trà, Đà Nẵng',
        16.1051,
        108.2338,
        'Asia/Ho_Chi_Minh'
    )
ON CONFLICT (code) DO NOTHING;

-- ── 7.5 Zones mẫu cho cảng Tiên Sa ───────────────────────────────────────
INSERT INTO operational.zones
    (port_id, name, zone_type, description, capacity, display_order)
SELECT
    p.id,
    z.name,
    z.zone_type::operational.zone_type_enum,
    z.description,
    z.capacity,
    z.display_order
FROM operational.ports p
CROSS JOIN (VALUES
    ('Cầu tàu số 1 (Dock A)',   'DOCK',      'Cầu tàu chính — tiếp nhận tàu container tải trọng đến 30,000 DWT', 450, 1),
    ('Cầu tàu số 2 (Dock B)',   'DOCK',      'Cầu tàu phụ — hàng rời, hàng tổng hợp', 200, 2),
    ('Bãi Container (Yard A)',  'YARD',      'Khu bãi container nhập — sức chứa 1,200 TEU', 1200, 3),
    ('Bãi Hàng Rời (Yard B)',   'YARD',      'Khu bãi hàng rời xuất — diện tích 5,000 m²', NULL, 4),
    ('Cổng Chính (Gate 1)',     'GATE',      'Cổng kiểm soát xe tải vào/ra chính', 20, 5),
    ('Kho CFS',                 'WAREHOUSE', 'Container Freight Station — đóng gói LCL', NULL, 6)
) AS z(name, zone_type, description, capacity, display_order)
WHERE p.code = 'DNTSA'
ON CONFLICT DO NOTHING;

-- ── 7.6 Admin user mặc định ────────────────────────────────────────────────
-- Password mặc định: Admin@2026! — BẮT BUỘC đổi trước khi deploy production
-- Hash: bcrypt('Admin@2026!', 12) — tạo bằng lệnh:
--   node -e "const b=require('bcrypt');b.hash('Admin@2026!',12).then(console.log)"
INSERT INTO operational.users
    (email, full_name, password_hash, role, status)
VALUES
    (
        'admin@porms.vn',
        'System Administrator',
        -- bcrypt hash placeholder — PHẢI chạy lại với hash thật trước khi deploy
        '$2b$12$PLACEHOLDER_REPLACE_WITH_REAL_BCRYPT_HASH_BEFORE_DEPLOY',
        'ADMIN',
        'ACTIVE'
    )
ON CONFLICT (email) DO NOTHING;

-- ── 7.7 Time dimension populate (2025–2027) ────────────────────────────────
-- Populate dim_time từ 01/01/2025 đến 31/12/2027 — granularity: giờ
-- Chạy một lần duy nhất, ~26,280 rows (3 năm × 365 × 24)
INSERT INTO analytics.dim_time (
    time_key, full_datetime, date_only, year, quarter, month, month_name,
    week_of_year, day_of_month, day_of_week, day_name, hour,
    is_weekend, is_business_hour
)
SELECT
    TO_CHAR(dt, 'YYYYMMDDHH24')::INTEGER  AS time_key,
    dt                                     AS full_datetime,
    dt::DATE                               AS date_only,
    EXTRACT(YEAR  FROM dt)::SMALLINT       AS year,
    EXTRACT(QUARTER FROM dt)::SMALLINT     AS quarter,
    EXTRACT(MONTH FROM dt)::SMALLINT       AS month,
    TO_CHAR(dt, 'Month')                   AS month_name,
    EXTRACT(WEEK  FROM dt)::SMALLINT       AS week_of_year,
    EXTRACT(DAY   FROM dt)::SMALLINT       AS day_of_month,
    EXTRACT(DOW   FROM dt)::SMALLINT + 1   AS day_of_week,  -- 1=Sunday...7=Saturday
    TO_CHAR(dt, 'Day')                     AS day_name,
    EXTRACT(HOUR  FROM dt)::SMALLINT       AS hour,
    EXTRACT(DOW   FROM dt) IN (0, 6)       AS is_weekend,
    (EXTRACT(DOW FROM dt) NOT IN (0, 6)
     AND EXTRACT(HOUR FROM dt) BETWEEN 8 AND 17) AS is_business_hour
FROM generate_series(
    '2025-01-01 00:00:00+07'::TIMESTAMPTZ,
    '2027-12-31 23:00:00+07'::TIMESTAMPTZ,
    '1 hour'::INTERVAL
) AS dt
ON CONFLICT (time_key) DO NOTHING;


-- ---------------------------------------------------------------------------
-- 8. VIEWS — HỖ TRỢ API QUERY
-- ---------------------------------------------------------------------------

-- View: current state của tất cả ports (dùng cho dashboard overview)
CREATE OR REPLACE VIEW operational.v_port_current_state AS
SELECT
    p.id                AS port_id,
    p.name              AS port_name,
    p.code              AS port_code,
    p.latitude,
    p.longitude,
    p.current_mode,
    p.current_risk_level,
    p.is_active,

    -- Latest weather (denormalized)
    wr.wind_speed_ms,
    wr.beaufort_number,
    wr.rainfall_1h_mm,
    wr.temperature_c,
    wr.ow_weather_desc,
    wr.observed_at      AS weather_observed_at,

    -- Unread alert count
    (SELECT COUNT(*) FROM operational.alerts a
     WHERE a.port_id = p.id AND a.read_at IS NULL AND a.is_simulation = FALSE
    )                   AS unread_alert_count,

    -- Latest assessment summary
    ra.assessment_summary,
    ra.evaluated_at     AS last_assessed_at

FROM operational.ports p
LEFT JOIN LATERAL (
    -- Lấy weather reading mới nhất (LATERAL JOIN thay vì subquery)
    SELECT * FROM operational.weather_readings wr2
    WHERE wr2.port_id = p.id AND wr2.is_simulation = FALSE
    ORDER BY wr2.observed_at DESC
    LIMIT 1
) wr ON TRUE
LEFT JOIN LATERAL (
    SELECT * FROM operational.risk_assessments ra2
    WHERE ra2.port_id = p.id AND ra2.is_simulation = FALSE
    ORDER BY ra2.evaluated_at DESC
    LIMIT 1
) ra ON TRUE
WHERE p.is_active = TRUE;

COMMENT ON VIEW operational.v_port_current_state IS
    'Current state snapshot của mỗi port — dùng cho dashboard GET /ports/status';

-- ---------------------------------------------------------------------------

-- View: Alert feed (unread first, với port info)
CREATE OR REPLACE VIEW operational.v_alert_feed AS
SELECT
    a.id,
    a.port_id,
    p.name          AS port_name,
    p.code          AS port_code,
    a.alert_type,
    a.severity,
    a.title,
    a.message,
    a.metadata,
    a.created_at,
    a.read_at,
    (a.read_at IS NULL) AS is_unread
FROM operational.alerts a
JOIN operational.ports p ON p.id = a.port_id
WHERE a.is_simulation = FALSE
ORDER BY
    (a.read_at IS NULL) DESC,  -- Unread lên đầu
    a.severity = 'CRITICAL' DESC,
    a.created_at DESC;

COMMENT ON VIEW operational.v_alert_feed IS
    'Alert feed đã sắp xếp: unread + critical lên đầu. Dùng cho GET /alerts';


-- ---------------------------------------------------------------------------
-- 9. PERMISSIONS
-- ---------------------------------------------------------------------------

-- User cho ASP.NET Core API (read/write operational schema)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'porms_api') THEN
        CREATE ROLE porms_api WITH LOGIN PASSWORD 'change_this_password_in_production';
    END IF;
END $$;

GRANT USAGE ON SCHEMA operational TO porms_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA operational TO porms_api;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA operational TO porms_api;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA operational TO porms_api;

-- User cho Prefect ETL (write analytics, read operational)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'porms_etl') THEN
        CREATE ROLE porms_etl WITH LOGIN PASSWORD 'change_this_password_in_production';
    END IF;
END $$;

GRANT USAGE ON SCHEMA operational TO porms_etl;
GRANT SELECT ON ALL TABLES IN SCHEMA operational TO porms_etl;
GRANT USAGE ON SCHEMA analytics TO porms_etl;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA analytics TO porms_etl;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA analytics TO porms_etl;

-- User cho Metabase (read-only analytics schema)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'porms_metabase') THEN
        CREATE ROLE porms_metabase WITH LOGIN PASSWORD 'change_this_password_in_production';
    END IF;
END $$;

GRANT USAGE ON SCHEMA analytics TO porms_metabase;
GRANT SELECT ON ALL TABLES IN SCHEMA analytics TO porms_metabase;
-- Metabase KHÔNG có quyền truy cập schema operational
-- (tách biệt hoàn toàn để tránh leak data nhạy cảm như users.password_hash)


-- ---------------------------------------------------------------------------
-- 10. MIGRATION VERSION TRACKING
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version         VARCHAR(50)     PRIMARY KEY,
    description     TEXT            NOT NULL,
    applied_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    applied_by      VARCHAR(100)    NOT NULL DEFAULT CURRENT_USER,
    checksum        VARCHAR(64)     -- MD5 của file migration để detect tampering
);

INSERT INTO public.schema_migrations (version, description)
VALUES ('v1.0.0', 'Initial schema: operational + analytics, 12 tables, seed data, indexes, triggers')
ON CONFLICT (version) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 11. VERIFY
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    operational_count INTEGER;
    analytics_count   INTEGER;
    seed_ports        INTEGER;
    seed_sop_rules    INTEGER;
    seed_thresholds   INTEGER;
    dim_time_count    INTEGER;
BEGIN
    SELECT COUNT(*) INTO operational_count
    FROM information_schema.tables
    WHERE table_schema = 'operational' AND table_type = 'BASE TABLE';

    SELECT COUNT(*) INTO analytics_count
    FROM information_schema.tables
    WHERE table_schema = 'analytics' AND table_type = 'BASE TABLE';

    SELECT COUNT(*) INTO seed_ports       FROM operational.ports;
    SELECT COUNT(*) INTO seed_sop_rules   FROM operational.sop_rules;
    SELECT COUNT(*) INTO seed_thresholds  FROM operational.risk_thresholds;
    SELECT COUNT(*) INTO dim_time_count   FROM analytics.dim_time;

    RAISE NOTICE '=== PORMS Migration v1.0.0 — Verify ===';
    RAISE NOTICE 'Schema operational : % tables', operational_count;
    RAISE NOTICE 'Schema analytics   : % tables', analytics_count;
    RAISE NOTICE 'Seed ports         : %', seed_ports;
    RAISE NOTICE 'Seed SOP rules     : %', seed_sop_rules;
    RAISE NOTICE 'Seed risk threshold: %', seed_thresholds;
    RAISE NOTICE 'dim_time rows      : %', dim_time_count;
    RAISE NOTICE '=========================================';
    RAISE NOTICE 'Migration completed successfully!';
    RAISE NOTICE 'IMPORTANT: Đổi password của porms_api, porms_etl, porms_metabase trước khi deploy!';
    RAISE NOTICE 'IMPORTANT: Tạo lại bcrypt hash cho admin@porms.vn trước khi deploy!';
END $$;

-- =============================================================================
-- END OF MIGRATION v1.0.0
-- =============================================================================
