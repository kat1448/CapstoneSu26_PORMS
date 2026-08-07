# Contributing to PORMS

## Cấu trúc chính

```text
backend/                 ASP.NET Core API, Risk Engine và nghiệp vụ
frontend/                React + TypeScript web portal
etl/                     Prefect weather/forecast flows
docs/database/schema.sql PostgreSQL operational schema
infra/                   Docker Compose, PostgreSQL init và Nginx
scripts/                 Seed data và script hỗ trợ phát triển
```

PORMS sử dụng một schema ứng dụng duy nhất là `operational`. Prefect ghi dữ liệu thời tiết trực tiếp vào schema này và gọi API nội bộ để kích hoạt đánh giá rủi ro.

## Chạy môi trường phát triển

```powershell
Copy-Item .env.example .env
cd infra
docker compose --env-file ../.env --profile app up -d --build
```

Các cổng mặc định:

- Frontend: `5173`
- Backend: `5000`
- Prefect: `4200`
- PostgreSQL: `55432`

## Kiểm tra trước khi commit

```powershell
cd frontend
npm.cmd run build

cd ../backend
dotnet test PORMS.sln

cd ..
python -m compileall etl
```

## Quy ước Git

- Tạo nhánh riêng theo tính năng hoặc lỗi cần sửa.
- Không commit `.env`, mật khẩu, API key hoặc dữ liệu cá nhân.
- Chỉ sửa các tệp thuộc phạm vi công việc và không ghi đè thay đổi chưa liên quan.
- Commit message nên mô tả rõ mục đích, ví dụ `fix(tasks): repair assignment dialog layout`.
- Trước khi mở pull request, kiểm tra frontend build được và các service chính khởi động healthy.

## Database

Không tạo schema hoặc bảng trực tiếp trong pgAdmin để thay đổi cấu trúc chính thức. Mọi thay đổi DDL phải được cập nhật trong `docs/database/schema.sql` hoặc migration tương ứng, sau đó kiểm tra trên database tạm trước khi áp dụng.
