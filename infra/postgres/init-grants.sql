-- ============================================================
-- PORMS — Schema-level grants (chạy sau khi schema.sql tạo xong)
-- Hệ thống hiện chỉ sử dụng schema operational.
-- ============================================================

-- ETL user: thu thập và cập nhật dữ liệu thời tiết vận hành.
GRANT USAGE ON SCHEMA operational TO porms_etl;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA operational TO porms_etl;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operational TO porms_etl;

ALTER DEFAULT PRIVILEGES IN SCHEMA operational
  GRANT SELECT, INSERT, UPDATE ON TABLES TO porms_etl;

-- API user: xử lý toàn bộ nghiệp vụ của PORMS.
GRANT USAGE ON SCHEMA operational TO porms_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA operational TO porms_api;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA operational TO porms_api;

ALTER DEFAULT PRIVILEGES IN SCHEMA operational
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO porms_api;
