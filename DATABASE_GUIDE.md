# Hướng dẫn kết nối & kiểm tra Database — PORMS

**Database:** PostgreSQL 16  
**Container:** `porms-postgres`  
**Port:** 5432 (expose ra host)

---

## Mục lục

1. [Thông tin kết nối](#1-thông-tin-kết-nối)
2. [Kết nối bằng các công cụ phổ biến](#2-kết-nối-bằng-các-công-cụ-phổ-biến)
3. [Kết nối qua Docker (không cần cài PostgreSQL)](#3-kết-nối-qua-docker-không-cần-cài-postgresql)
4. [Cấu trúc Database](#4-cấu-trúc-database)
5. [Câu lệnh kiểm tra dữ liệu](#5-câu-lệnh-kiểm-tra-dữ-liệu)
6. [Câu lệnh kiểm tra hệ thống](#6-câu-lệnh-kiểm-tra-hệ-thống)
7. [Tham khảo nhanh psql](#7-tham-khảo-nhanh-psql)

---

## 1. Thông tin kết nối

### Tài khoản

| User | Mật khẩu (trong `.env`) | Quyền |
|------|--------------------------|-------|
| `postgres` | `POSTGRES_PASSWORD` | Superuser — dùng để quản trị |
| `porms_etl` | `POSTGRES_ETL_PASSWORD` | Đọc/ghi `operational.*` và `analytics.*` — dùng cho ETL Python |
| `porms_api` | `POSTGRES_API_PASSWORD` | Đọc/ghi `operational.*` — dùng cho .NET Backend |
| `porms_metabase` | `POSTGRES_METABASE_PASSWORD` | Chỉ đọc `analytics.*` — dùng cho Metabase |

### Connection string

```
# Format chung
postgresql://<user>:<password>@<host>:<port>/<database>

# Kết nối từ máy host (bên ngoài Docker)
postgresql://postgres:<POSTGRES_PASSWORD>@localhost:5432/porms_db

# Kết nối từ bên trong Docker network (các service khác)
postgresql://porms_etl:<POSTGRES_ETL_PASSWORD>@porms-postgres:5432/porms_db
```

> Xem mật khẩu thực tế trong file `.env` ở thư mục gốc dự án.

---

## 2. Kết nối bằng các công cụ phổ biến

### DBeaver (khuyến nghị cho team)

1. Tạo kết nối mới → chọn **PostgreSQL**
2. Điền thông tin:
   - **Host:** `localhost`
   - **Port:** `5432`
   - **Database:** `porms_db`
   - **Username:** `postgres`
   - **Password:** lấy từ `.env` → `POSTGRES_PASSWORD`
3. Nhấn **Test Connection** → **Finish**
4. Trong Database Navigator, mở: `porms_db` → `Schemas` → `operational` hoặc `analytics`

### TablePlus / DataGrip

Điền tương tự DBeaver, driver chọn **PostgreSQL**.

### pgAdmin

1. Servers → Register → Server
2. Tab **General**: đặt tên (ví dụ: `PORMS Local`)
3. Tab **Connection**:
   - Host: `localhost`
   - Port: `5432`
   - Database: `porms_db`
   - Username: `postgres`
   - Password: lấy từ `.env`

---

## 3. Kết nối qua Docker (không cần cài PostgreSQL)

Cách đơn giản nhất — chạy `psql` trực tiếp trong container:

```bash
# Mở psql với superuser postgres
docker exec -it porms-postgres psql -U postgres -d porms_db

# Chạy 1 câu lệnh rồi thoát (không cần vào interactive mode)
docker exec porms-postgres psql -U postgres -d porms_db -c "SELECT NOW();"
```

Sau khi vào `psql`, dấu nhắc sẽ hiện:
```
porms_db=#
```

Thoát khỏi psql: gõ `\q` rồi Enter.

---

## 4. Cấu trúc Database

Database `porms_db` có 2 schema chính:

```
porms_db
├── operational/          ← Dữ liệu vận hành thời gian thực
│   ├── ports             ← Danh sách cảng
│   ├── zones             ← Các khu vực trong cảng
│   ├── users             ← Tài khoản operator
│   ├── weather_readings  ← Dữ liệu thời tiết thu thập được
│   ├── risk_assessments  ← Kết quả đánh giá rủi ro
│   ├── risk_thresholds   ← Ngưỡng rủi ro (cấu hình)
│   ├── sop_rules         ← Quy tắc SOP
│   ├── sop_executions    ← Lịch sử thực thi SOP
│   ├── alerts            ← Cảnh báo gửi cho operator
│   ├── operation_events  ← Log sự kiện hệ thống
│   ├── operation_mode_log← Lịch sử thay đổi chế độ vận hành
│   ├── simulation_sessions← Phiên mô phỏng
│   └── task_logs         ← Log task
│
└── analytics/            ← Data Warehouse cho báo cáo (Metabase)
    ├── dim_port          ← Dimension: cảng
    ├── dim_zone          ← Dimension: khu vực
    ├── dim_risk_levels   ← Dimension: mức rủi ro
    ├── dim_sop_action    ← Dimension: loại hành động SOP
    ├── dim_time          ← Dimension: thời gian
    ├── fact_weather_readings ← Fact: thời tiết (aggregate theo giờ)
    ├── fact_risk_assessments ← Fact: đánh giá rủi ro
    ├── fact_sop_executions   ← Fact: thực thi SOP
    ├── fact_alerts           ← Fact: cảnh báo
    ├── fact_operation_events ← Fact: sự kiện vận hành
    └── etl_watermarks        ← Trạng thái ETL (watermark)
```

---

## 5. Câu lệnh kiểm tra dữ liệu

### Dữ liệu thời tiết

```sql
-- Xem 10 reading mới nhất
SELECT p.code AS cang,
       wr.observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh' AS thoi_gian,
       wr.wind_speed_ms AS gio_ms,
       wr.beaufort_number AS beaufort,
       wr.rainfall_1h_mm AS mua_mm,
       wr.visibility_km AS tam_nhin_km,
       wr.temperature_c AS nhiet_do,
       wr.data_source AS nguon
FROM operational.weather_readings wr
JOIN operational.ports p ON p.id = wr.port_id
ORDER BY wr.observed_at DESC
LIMIT 10;

-- Tổng hợp số lượng data
SELECT
    COUNT(*) AS tong_readings,
    COUNT(*) FILTER (WHERE data_source = 'OPENWEATHER_API') AS tu_api_that,
    COUNT(*) FILTER (WHERE data_source = 'MOCK_HISTORICAL') AS mock_lich_su,
    MIN(observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS tu_ngay,
    MAX(observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS den_ngay
FROM operational.weather_readings;

-- Thống kê theo ngày
SELECT
    (observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS ngay,
    COUNT(*) AS so_readings,
    ROUND(AVG(wind_speed_ms)::numeric, 2) AS gio_tb,
    MAX(beaufort_number) AS beaufort_max,
    ROUND(SUM(rainfall_1h_mm)::numeric, 1) AS tong_mua_mm
FROM operational.weather_readings
GROUP BY 1
ORDER BY 1 DESC
LIMIT 14;
```

### Cảng và khu vực

```sql
-- Danh sách cảng
SELECT id, name, code, latitude, longitude, current_risk_level, is_active
FROM operational.ports;

-- Danh sách khu vực theo cảng
SELECT z.name AS khu_vuc, z.zone_type, p.code AS cang, z.is_active
FROM operational.zones z
JOIN operational.ports p ON p.id = z.port_id
ORDER BY p.code, z.zone_type;
```

### Đánh giá rủi ro

```sql
-- 10 đánh giá rủi ro gần nhất
SELECT p.code AS cang,
       ra.evaluated_at AT TIME ZONE 'Asia/Ho_Chi_Minh' AS thoi_gian,
       ra.final_risk_level AS muc_rui_ro,
       ra.beaufort_number,
       ra.rainfall_1h_mm,
       ra.visibility_km,
       ra.level_changed AS vua_thay_doi
FROM operational.risk_assessments ra
JOIN operational.ports p ON p.id = ra.port_id
ORDER BY ra.evaluated_at DESC
LIMIT 10;

-- Thống kê số lần mỗi mức rủi ro xuất hiện
SELECT final_risk_level, COUNT(*) AS so_lan
FROM operational.risk_assessments
WHERE is_simulation = FALSE
GROUP BY final_risk_level
ORDER BY so_lan DESC;
```

### Cảnh báo

```sql
-- Cảnh báo chưa đọc
SELECT p.code AS cang,
       a.alert_type, a.severity, a.title,
       a.created_at AT TIME ZONE 'Asia/Ho_Chi_Minh' AS tao_luc
FROM operational.alerts a
JOIN operational.ports p ON p.id = a.port_id
WHERE a.read_at IS NULL
ORDER BY a.created_at DESC;

-- Thống kê cảnh báo 7 ngày qua
SELECT
    (created_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS ngay,
    severity,
    COUNT(*) AS so_canh_bao
FROM operational.alerts
WHERE created_at >= NOW() - INTERVAL '7 days'
GROUP BY 1, 2
ORDER BY 1 DESC, 2;
```

### SOP Rules

```sql
-- Xem toàn bộ SOP rules đang active
SELECT rule_name, trigger_risk_level, applies_to_zone_type,
       action_type, execution_order, is_active
FROM operational.sop_rules
WHERE is_active = TRUE
ORDER BY trigger_risk_level, execution_order;
```

### Analytics (Data Warehouse)

```sql
-- Xem fact_weather_readings đã được ETL load
SELECT time_key,
       p.code AS cang,
       avg_wind_speed_ms,
       max_beaufort,
       total_rainfall_mm,
       minutes_at_low, minutes_at_medium, minutes_at_high, minutes_at_critical,
       final_risk_level
FROM analytics.fact_weather_readings fwr
JOIN analytics.dim_port p ON p.port_key = fwr.port_id
ORDER BY time_key DESC
LIMIT 10;

-- Kiểm tra ETL watermark (lần cuối ETL chạy)
SELECT flow_name,
       last_loaded_at AT TIME ZONE 'Asia/Ho_Chi_Minh' AS lan_cuoi_chay,
       last_batch_id
FROM analytics.etl_watermarks
ORDER BY flow_name;

-- Thống kê rủi ro theo giờ trong tuần qua
SELECT
    time_key,
    final_risk_level,
    avg_wind_speed_ms,
    total_rainfall_mm
FROM analytics.fact_weather_readings
WHERE time_key >= TO_CHAR(NOW() - INTERVAL '7 days', 'YYYYMMDDHH24')::integer
ORDER BY time_key DESC;
```

---

## 6. Câu lệnh kiểm tra hệ thống

### Kiểm tra kết nối & container

```bash
# Container postgres đang chạy không?
docker ps --filter name=porms-postgres

# Logs của postgres (xem lỗi nếu có)
docker logs porms-postgres --tail 30

# Test kết nối nhanh
docker exec porms-postgres psql -U postgres -d porms_db -c "SELECT 'OK' AS ket_noi, NOW() AS thoi_gian;"
```

### Kiểm tra cấu trúc bảng

```bash
# Xem tất cả bảng trong schema operational
docker exec porms-postgres psql -U postgres -d porms_db -c "\dt operational.*"

# Xem tất cả bảng trong schema analytics
docker exec porms-postgres psql -U postgres -d porms_db -c "\dt analytics.*"

# Xem cột của một bảng cụ thể
docker exec porms-postgres psql -U postgres -d porms_db -c "\d operational.weather_readings"

# Xem tất cả schema
docker exec porms-postgres psql -U postgres -d porms_db -c "\dn"
```

### Kiểm tra users & quyền

```bash
# Danh sách users
docker exec porms-postgres psql -U postgres -d porms_db -c "\du"

# Kiểm tra quyền trên schema
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT grantee, privilege_type, table_schema, table_name
FROM information_schema.role_table_grants
WHERE grantee IN ('porms_etl', 'porms_api', 'porms_metabase')
ORDER BY grantee, table_schema, table_name
LIMIT 20;
"
```

### Kiểm tra dung lượng

```bash
# Dung lượng từng bảng (top 10 lớn nhất)
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT schemaname || '.' || tablename AS bang,
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS kich_thuoc,
       n_live_tup AS so_hang
FROM pg_stat_user_tables
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC
LIMIT 10;
"

# Tổng dung lượng database
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT pg_size_pretty(pg_database_size('porms_db')) AS tong_dung_luong;
"
```

### Kiểm tra connections đang mở

```bash
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT usename AS user, application_name AS app, state, COUNT(*) AS so_ket_noi
FROM pg_stat_activity
WHERE datname = 'porms_db'
GROUP BY 1, 2, 3
ORDER BY so_ket_noi DESC;
"
```

---

## 7. Tham khảo nhanh psql

Các lệnh dùng **bên trong** psql interactive (`porms_db=#`):

| Lệnh | Tác dụng |
|------|----------|
| `\dt operational.*` | Liệt kê bảng trong schema operational |
| `\dt analytics.*` | Liệt kê bảng trong schema analytics |
| `\d <ten_bang>` | Xem cột và kiểu dữ liệu của bảng |
| `\dn` | Liệt kê tất cả schema |
| `\du` | Liệt kê tất cả users/roles |
| `\l` | Liệt kê tất cả databases |
| `\timing` | Bật/tắt hiển thị thời gian thực thi |
| `\x` | Bật/tắt expanded mode (dễ đọc hơn khi có nhiều cột) |
| `\q` | Thoát khỏi psql |
| `\?` | Xem tất cả lệnh psql |

**Tip:** Dùng `\x` trước khi xem bảng có nhiều cột sẽ dễ đọc hơn:
```sql
porms_db=# \x
Expanded display is on.
porms_db=# SELECT * FROM operational.ports;
```
