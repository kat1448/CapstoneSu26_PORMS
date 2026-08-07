# PORMS Prefect Deployment Guide

Prefect được giữ lại để điều phối các tác vụ phục vụ trực tiếp cho PORMS. Nó không còn chạy luồng Data Warehouse hoặc Metabase.

## Các deployment

| Deployment | Lịch chạy | Mục đích |
|---|---|---|
| `weather-collector/prod` | Mỗi 10 phút | Lấy OpenWeather, lưu `operational.weather_readings`, gọi Risk Engine |
| `forecast-plan-refresh/prod` | 00:15 hằng ngày | Làm mới kế hoạch dự báo vận hành |
| `historical-data-backfill/prod` | Thủ công | Nạp dữ liệu lịch sử khi cần |

## Khởi động

```powershell
cd infra
docker compose --env-file ../.env up -d postgres prefect-server prefect-worker
```

Prefect UI: http://localhost:4200

## Kiểm tra

```powershell
docker exec porms-prefect-worker prefect deployment ls
docker logs --tail 100 porms-prefect-worker
```

Kết quả đúng không được xuất hiện deployment `dw-loader`; database đích của các flow nghiệp vụ là schema `operational`.
