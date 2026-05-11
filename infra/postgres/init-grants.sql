-- ============================================================
-- PORMS — Schema-level grants (chạy sau khi schema.sql tạo xong)
-- File này được mount vào initdb.d AFTER schema.sql
-- ============================================================

-- ─── ETL user (porms_etl): đọc/ghi cả 2 schema ──────────────
GRANT USAGE ON SCHEMA operational TO porms_etl;
GRANT USAGE ON SCHEMA analytics   TO porms_etl;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA operational TO porms_etl;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA analytics   TO porms_etl;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operational TO porms_etl;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA analytics   TO porms_etl;

ALTER DEFAULT PRIVILEGES IN SCHEMA operational
  GRANT SELECT, INSERT, UPDATE ON TABLES TO porms_etl;
ALTER DEFAULT PRIVILEGES IN SCHEMA analytics
  GRANT SELECT, INSERT, UPDATE ON TABLES TO porms_etl;

-- ─── API user (porms_api): full operational, read analytics ──
GRANT USAGE ON SCHEMA operational TO porms_api;
GRANT USAGE ON SCHEMA analytics   TO porms_api;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA operational TO porms_api;
GRANT SELECT                          ON ALL TABLES IN SCHEMA analytics   TO porms_api;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operational TO porms_api;

ALTER DEFAULT PRIVILEGES IN SCHEMA operational
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO porms_api;
ALTER DEFAULT PRIVILEGES IN SCHEMA analytics
  GRANT SELECT ON TABLES TO porms_api;

-- ─── Metabase user (porms_metabase): read-only analytics ─────
GRANT USAGE  ON SCHEMA analytics   TO porms_metabase;
GRANT SELECT ON ALL TABLES IN SCHEMA analytics TO porms_metabase;

ALTER DEFAULT PRIVILEGES IN SCHEMA analytics
  GRANT SELECT ON TABLES TO porms_metabase;
