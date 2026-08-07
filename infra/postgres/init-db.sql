-- ============================================================
-- PORMS — PostgreSQL Initialization Script
-- Chạy tự động khi container postgres khởi động lần đầu
-- NOTE: Passwords được set qua ALTER USER trong init-users.sh
--       (bash script chạy trước file này không hỗ trợ env vars trực tiếp)
-- ============================================================

-- ─── 1. Tạo database cho Prefect ────────────────────────────
SELECT 'CREATE DATABASE prefect_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'prefect_db') \gexec

-- ─── 2. Tạo users với placeholder password (sẽ được set đúng bởi init-users.sh) ─
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'porms_etl') THEN
    CREATE USER porms_etl WITH PASSWORD 'placeholder';
  END IF;
END
$$;

DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'porms_api') THEN
    CREATE USER porms_api WITH PASSWORD 'placeholder';
  END IF;
END
$$;

-- ─── 3. Extension hỗ trợ kiểu dữ liệu không phân biệt hoa thường ─
CREATE EXTENSION IF NOT EXISTS citext WITH SCHEMA public;

-- ─── 4. Cấp quyền kết nối và schema ────────────────────────
GRANT CONNECT ON DATABASE porms_db TO porms_etl;
GRANT CONNECT ON DATABASE porms_db TO porms_api;
