# PORMS Docker Setup

PORMS hiện chạy PostgreSQL, Prefect Server, Prefect Worker, ASP.NET Core API và React frontend. Hệ thống không còn sử dụng Metabase hoặc kho dữ liệu BI riêng.

## Chuẩn bị

Sao chép `.env.example` thành `.env`, sau đó cấu hình tối thiểu:

```env
POSTGRES_PASSWORD=...
POSTGRES_API_PASSWORD=...
POSTGRES_ETL_PASSWORD=...
OPENWEATHER_API_KEY=...
JWT_SECRET=...
INTERNAL_API_KEY=...
```

## Chạy toàn bộ hệ thống

```powershell
cd infra
docker compose --env-file ../.env --profile app up -d --build
docker compose --env-file ../.env ps
```

Các địa chỉ chính:

- Web PORMS: http://localhost:5173
- Backend API: http://localhost:5000
- Prefect UI: http://localhost:4200
- PostgreSQL: `localhost:55432`, database `porms_db`

## Kiểm tra và xem log

```powershell
docker compose --env-file ../.env logs -f backend
docker compose --env-file ../.env logs -f frontend
docker compose --env-file ../.env logs -f prefect-worker
```

Database nghiệp vụ nằm trong schema `operational`. Database `prefect_db` chỉ lưu trạng thái điều phối nội bộ của Prefect.
