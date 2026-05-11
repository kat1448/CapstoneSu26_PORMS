#!/bin/bash
# Set correct passwords for dedicated users from environment variables.
# Runs as 01_init_passwords.sh — after 00_init_db.sql (users already exist).
# Order is controlled by numeric prefix in docker-compose volume mounts, not filename sort.

set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
  ALTER USER porms_etl      WITH PASSWORD '${POSTGRES_ETL_PASSWORD}';
  ALTER USER porms_metabase WITH PASSWORD '${POSTGRES_METABASE_PASSWORD}';
  ALTER USER porms_api      WITH PASSWORD '${POSTGRES_API_PASSWORD}';
EOSQL

echo "[init-passwords] Passwords set for porms_etl, porms_metabase, porms_api"
