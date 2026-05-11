# Hướng dẫn Deploy ETL lên Prefect — PORMS

**Prefect version:** 2.20.7  
**Python:** 3.11  
**Môi trường:** Docker (container `porms-prefect-worker`)

---

## Mục lục

1. [Kiến trúc Prefect trong dự án](#1-kiến-trúc-prefect-trong-dự-án)
2. [Khởi động hệ thống](#2-khởi-động-hệ-thống)
3. [Deploy flows lên Prefect](#3-deploy-flows-lên-prefect)
4. [Chạy flows thủ công](#4-chạy-flows-thủ-công)
5. [Kiểm tra trạng thái](#5-kiểm-tra-trạng-thái)
6. [Quy trình chuẩn bị trước demo](#6-quy-trình-chuẩn-bị-trước-demo)
7. [Xử lý lỗi thường gặp](#7-xử-lý-lỗi-thường-gặp)

---

## 1. Kiến trúc Prefect trong dự án

```
┌─────────────────────────────────────────────────────────┐
│  Docker Network: porms-network                          │
│                                                         │
│  porms-prefect-server          porms-prefect-worker     │
│  ┌─────────────────────┐      ┌──────────────────────┐  │
│  │ Prefect Server      │◄────►│ Prefect Worker       │  │
│  │ - Lưu metadata      │      │ - Chạy flow thật     │  │
│  │ - Quản lý schedule  │      │ - Kết nối OpenWeather│  │
│  │ - UI tại :4200      │      │ - Kết nối PostgreSQL │  │
│  └─────────────────────┘      └──────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

**3 flows đã triển khai:**

| Flow | Schedule | Mô tả |
|------|----------|-------|
| `weather-collector/prod` | Mỗi 15 phút | Fetch thời tiết từ OpenWeatherMap |
| `dw-loader/prod` | Mỗi giờ (`:00`) | Sync data vào Data Warehouse |
| `historical-data-backfill/prod` | Thủ công | Nạp data lịch sử 30 ngày (chạy 1 lần) |

---

## 2. Khởi động hệ thống

### Bước 1 — Khởi động toàn bộ Docker stack

```bash
# Từ thư mục gốc dự án (f:\SU2026)
cd infra
docker compose --env-file ../.env up -d
```

Kiểm tra tất cả container đang chạy:

```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

Kết quả mong đợi:
```
NAMES                   STATUS          PORTS
porms-prefect-worker    Up X minutes
porms-prefect-server    Up X minutes    0.0.0.0:4200->4200/tcp
porms-postgres          Up X minutes    0.0.0.0:5432->5432/tcp
porms-metabase          Up X minutes    0.0.0.0:3000->3000/tcp
```

### Bước 2 — Mở Prefect UI

Truy cập: **http://localhost:4200**

- Tab **Flows**: xem danh sách flows đã đăng ký
- Tab **Deployments**: xem lịch chạy và trigger thủ công
- Tab **Flow Runs**: xem lịch sử các lần chạy
- Tab **Work Pools**: xem trạng thái worker

---

## 3. Deploy flows lên Prefect

> Cần làm mỗi khi **thay đổi code** hoặc **lần đầu setup**.

### Cách 1 — Dùng script có sẵn (khuyến nghị)

```bash
docker exec porms-prefect-worker bash -c "cd /app && PYTHONPATH=/app python deployments/deploy_all.py"
```

Kết quả mong đợi:
```
✅ weather-collector deployed (every 15 min)
✅ dw-loader deployed (every hour at :00)
✅ historical-backfill deployed (manual trigger only)

🎉 All deployments registered!
```

### Cách 2 — Deploy từng flow riêng lẻ (qua Prefect CLI)

```bash
# Vào bên trong container worker
docker exec -it porms-prefect-worker bash

# Trong container:
cd /app
export PYTHONPATH=/app

# Deploy weather-collector với schedule 15 phút
prefect deployment build flows/weather_collector.py:weather_collector_flow \
    --name prod \
    --pool porms-pool \
    --interval 900 \
    --apply

# Deploy dw-loader với cron hàng giờ
prefect deployment build flows/dw_loader.py:dw_loader_flow \
    --name prod \
    --pool porms-pool \
    --cron "0 * * * *" \
    --apply

# Deploy historical-backfill không có schedule (thủ công)
prefect deployment build flows/historical_backfill.py:historical_backfill_flow \
    --name prod \
    --pool porms-pool \
    --apply
```

### Cập nhật code sau khi sửa file

Khi sửa code Python trong `etl/`, cần copy file vào container và redeploy:

```bash
# Copy file đã sửa vào container (ví dụ: weather_collector.py)
docker cp etl/flows/weather_collector.py porms-prefect-worker:/app/flows/weather_collector.py

# Redeploy
docker exec porms-prefect-worker bash -c "cd /app && PYTHONPATH=/app python deployments/deploy_all.py"
```

> **Lưu ý:** Cách nhanh hơn là rebuild Docker image sau mỗi thay đổi lớn:
> ```bash
> docker compose --env-file ../.env up -d --build etl
> ```

---

## 4. Chạy flows thủ công

### Cách 1 — Trực tiếp trong container (nhanh nhất, dùng để test)

```bash
# Test weather_collector (1 lần)
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python -c \
  'from flows.weather_collector import weather_collector_flow; weather_collector_flow()'"

# Test dw_loader (1 lần)
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python -c \
  'from flows.dw_loader import dw_loader_flow; dw_loader_flow()'"

# Chạy historical backfill 30 ngày
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python -c \
  'from flows.historical_backfill import historical_backfill_flow; historical_backfill_flow(days_back=30)'"
```

### Cách 2 — Qua Prefect CLI (chạy qua scheduler)

```bash
# Trigger deployment từ CLI (flow sẽ được worker nhận và chạy)
docker exec porms-prefect-server bash -c \
  "prefect deployment run 'weather-collector/prod'"

# Trigger với tham số (ví dụ backfill 7 ngày)
docker exec porms-prefect-server bash -c \
  "prefect deployment run 'historical-data-backfill/prod' --param days_back=7"
```

### Cách 3 — Qua Prefect UI

1. Vào **http://localhost:4200**
2. Tab **Deployments**
3. Tìm deployment cần chạy → nhấn nút **Quick Run**
4. Với `historical-data-backfill`: nhấn **Custom Run** để nhập `days_back`

---

## 5. Kiểm tra trạng thái

### Kiểm tra deployments đã đăng ký

```bash
docker exec porms-prefect-server bash -c "prefect deployment ls"
```

Kết quả mong đợi:
```
┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┳━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
┃ Name                          ┃ ID                                   ┃
┡━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━╇━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┩
│ dw-loader/prod                │ ...                                  │
│ historical-data-backfill/prod │ ...                                  │
│ weather-collector/prod        │ ...                                  │
└───────────────────────────────┴──────────────────────────────────────┘
```

### Kiểm tra worker đang hoạt động

```bash
docker exec porms-prefect-server bash -c "prefect work-pool ls"
```

### Kiểm tra data đã được lưu vào DB

```bash
# Xem số lượng và khoảng thời gian data
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT
    COUNT(*) AS tong_readings,
    COUNT(*) FILTER (WHERE data_source = 'OPENWEATHER_API') AS api_that,
    COUNT(*) FILTER (WHERE data_source = 'MOCK_HISTORICAL') AS mock,
    MIN(observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS tu_ngay,
    MAX(observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS den_ngay
FROM operational.weather_readings;
"

# Xem analytics đã được sync chưa
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT flow_name, last_loaded_at AT TIME ZONE 'Asia/Ho_Chi_Minh' AS last_run
FROM analytics.etl_watermarks ORDER BY flow_name;
"

# Xem 5 reading gần nhất
docker exec porms-postgres psql -U postgres -d porms_db -c "
SELECT p.code, wr.observed_at AT TIME ZONE 'Asia/Ho_Chi_Minh' AS thoi_gian,
       wr.wind_speed_ms, wr.beaufort_number, wr.rainfall_1h_mm, wr.data_source
FROM operational.weather_readings wr
JOIN operational.ports p ON p.id = wr.port_id
ORDER BY wr.observed_at DESC LIMIT 5;
"
```

### Xem logs của worker

```bash
# Logs realtime
docker logs porms-prefect-worker -f

# 50 dòng gần nhất
docker logs porms-prefect-worker --tail 50
```

---

## 6. Quy trình chuẩn bị trước demo

Thực hiện theo đúng thứ tự sau:

```bash
# Bước 1: Khởi động Docker stack
cd infra
docker compose --env-file ../.env up -d

# Bước 2: Chờ ~30 giây cho tất cả container healthy, kiểm tra
docker ps

# Bước 3: Deploy tất cả flows
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python deployments/deploy_all.py"

# Bước 4: Nạp data lịch sử 30 ngày
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python -c \
  'from flows.historical_backfill import historical_backfill_flow; historical_backfill_flow(days_back=30)'"

# Bước 5: Sync data vào analytics (Metabase)
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python -c \
  'from flows.dw_loader import dw_loader_flow; dw_loader_flow()'"

# Bước 6: Verify data
docker exec porms-postgres psql -U postgres -d porms_db -c \
  "SELECT COUNT(*) FROM operational.weather_readings; SELECT COUNT(*) FROM analytics.fact_weather_readings;"
```

Từ thời điểm này, `weather-collector` sẽ **tự động chạy mỗi 15 phút** mà không cần can thiệp thủ công.

---

## 7. Xử lý lỗi thường gặp

### Lỗi: `No module named 'flows'`

```bash
# Thiếu PYTHONPATH — luôn thêm khi chạy trong container
docker exec porms-prefect-worker bash -c "cd /app && PYTHONPATH=/app python ..."
```

### Lỗi: `Connection refused` khi kết nối Prefect Server

```bash
# Kiểm tra server đang chạy chưa
docker logs porms-prefect-server --tail 20

# Khởi động lại nếu cần
docker restart porms-prefect-server
# Chờ 10 giây rồi restart worker
docker restart porms-prefect-worker
```

### Lỗi: `OPENWEATHER_API_KEY` trống — flow fetch thất bại

```bash
# Kiểm tra env var trong container
docker exec porms-prefect-worker bash -c "echo \$OPENWEATHER_API_KEY"

# Nếu trống, kiểm tra file .env ở thư mục gốc
# Đảm bảo có dòng: OPENWEATHER_API_KEY=your_key_here
# Sau đó restart container
docker compose --env-file ../.env up -d --force-recreate prefect-worker
```

### Lỗi: Flow chạy nhưng không insert được DB

```bash
# Kiểm tra postgres đang chạy
docker logs porms-postgres --tail 20

# Test kết nối DB từ worker
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python -c \
  'from db.connection import get_operational_session; print(\"DB OK\")'"
```

### Redeploy sau khi sửa code

```bash
# 1. Copy file đã sửa vào container
docker cp etl/flows/<ten_file>.py porms-prefect-worker:/app/flows/<ten_file>.py

# 2. Redeploy
docker exec porms-prefect-worker bash -c \
  "cd /app && PYTHONPATH=/app python deployments/deploy_all.py"
```

---

## Tham khảo nhanh

| Việc cần làm | Lệnh |
|---|---|
| Xem tất cả deployments | `docker exec porms-prefect-server bash -c "prefect deployment ls"` |
| Xem worker pool | `docker exec porms-prefect-server bash -c "prefect work-pool ls"` |
| Trigger weather-collector | `docker exec porms-prefect-server bash -c "prefect deployment run 'weather-collector/prod'"` |
| Trigger dw-loader | `docker exec porms-prefect-server bash -c "prefect deployment run 'dw-loader/prod'"` |
| Xem logs worker | `docker logs porms-prefect-worker -f` |
| Mở Prefect UI | http://localhost:4200 |
