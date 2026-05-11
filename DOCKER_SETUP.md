# Hướng dẫn chạy Docker — PORMS

## Bước 1 — Chuẩn bị file .env

```powershell
cd f:\SU2026
copy .env.example .env
```

Mở `.env` và điền các giá trị bắt buộc:

```env
POSTGRES_PASSWORD=StrongPass123!
POSTGRES_API_PASSWORD=ApiPass123!
POSTGRES_ETL_PASSWORD=EtlPass123!
POSTGRES_METABASE_PASSWORD=MbPass123!

OPENWEATHER_API_KEY=<key lấy từ openweathermap.org>

JWT_SECRET=thay-bang-chuoi-ngau-nhien-32-ky-tu-tro-len
INTERNAL_API_KEY=thay-bang-chuoi-ngau-nhien-bat-ky
METABASE_SECRET_KEY=thay-bang-chuoi-ngau-nhien-bat-ky
```

---

## Bước 2 — Khởi động services (dev mode)

> **Lưu ý:** Phải dùng `--env-file ../.env` vì file `.env` nằm ở root repo (`f:\SU2026\`),
> không phải trong thư mục `infra/`.

Chạy 4 services hạ tầng: PostgreSQL, Prefect Server, Prefect Worker, Metabase
(Backend và Frontend chạy native riêng — không qua Docker)

```powershell
cd f:\SU2026\infra
docker compose --env-file ../.env up -d
```

Full stack (bao gồm BE và FE đã build):

```powershell
docker compose --env-file ../.env --profile app up -d
```

---

## Bước 3 — Kiểm tra tất cả containers đang chạy

```powershell
docker compose --env-file ../.env ps
```

Kết quả mong đợi:

```
NAME                   STATUS              PORTS
porms-postgres         running (healthy)   0.0.0.0:5432->5432/tcp
porms-prefect-server   running             0.0.0.0:4200->4200/tcp
porms-prefect-worker   running
porms-metabase         running (healthy)   0.0.0.0:3000->3000/tcp
```

Lần đầu khởi động Metabase mất khoảng 60–90 giây để init.

---

## Bước 4 — Verify PostgreSQL và seed data

```powershell
# Kiểm tra 2 schema đã tạo chưa
docker exec -it porms-postgres psql -U postgres -d porms_db -c "\dn"
```

Kết quả mong đợi:

```
   List of schemas
    Name     |  Owner
-------------+----------
 analytics   | postgres
 operational | postgres
```

```powershell
# Kiểm tra seed data
docker exec -it porms-postgres psql -U postgres -d porms_db -c "SELECT code, name FROM operational.ports;"
```

---

## Bước 5 — Mở các UI

| Service | URL | Dùng để |
|---|---|---|
| Prefect UI | http://localhost:4200 | Xem ETL flows, logs, schedules |
| Metabase | http://localhost:3000 | BI dashboard (setup lần đầu) |
| Swagger (khi BE chạy) | http://localhost:5000/swagger | Test API |

---

## Các lệnh thường dùng

```powershell
# Xem logs realtime của 1 service
docker compose --env-file ../.env logs -f prefect-worker
docker compose --env-file ../.env logs -f metabase

# Dừng tất cả (giữ data)
docker compose --env-file ../.env down

# Dừng và XÓA toàn bộ data (reset sạch, init DB lại từ đầu)
docker compose --env-file ../.env down -v

# Restart 1 service
docker compose --env-file ../.env restart metabase

# Vào shell bên trong container
docker exec -it porms-postgres psql -U postgres -d porms_db
docker exec -it porms-prefect-worker bash
```

---

## Xử lý lỗi thường gặp

**postgres không healthy:**
```powershell
docker compose --env-file ../.env logs postgres
# Thường do POSTGRES_PASSWORD chưa điền trong .env
```

**prefect-worker lỗi kết nối:**
```powershell
docker compose --env-file ../.env logs prefect-worker
# Đợi thêm 15–20s, worker cần prefect-server sẵn sàng trước
```

**Metabase không mở được:**
```powershell
# Đợi 90s rồi thử lại — Metabase khởi động chậm (JVM)
docker compose --env-file ../.env logs metabase
```

**Reset toàn bộ và chạy lại từ đầu:**
```powershell
docker compose --env-file ../.env down -v   # Xóa cả data volumes
docker compose --env-file ../.env up -d     # Khởi động lại, init DB từ đầu
```
