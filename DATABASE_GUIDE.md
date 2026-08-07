# PORMS Database Guide

## Cấu trúc hiện tại

- `porms_db` là database của ứng dụng PORMS.
- `operational` chứa toàn bộ bảng nghiệp vụ: người dùng, cảng, khu vực, thời tiết, đánh giá rủi ro, SOP, cảnh báo, nhiệm vụ, mô phỏng và lịch sử vận hành.
- `public` là schema mặc định của PostgreSQL, hiện chỉ dùng cho extension và bảng theo dõi phiên bản schema.
- `prefect_db` là database riêng do Prefect tự quản lý; không phải dữ liệu nghiệp vụ của PORMS.

Hệ thống không còn schema `analytics` và không còn database metadata của Metabase.

## Kết nối pgAdmin

Tạo server với các thông tin:

- Host: `localhost`
- Port: `55432`
- Maintenance database: `porms_db`
- Username: `postgres`
- Password: giá trị `POSTGRES_PASSWORD` trong `.env`

Sau khi kết nối, mở `Databases → porms_db → Schemas → operational → Tables`.

## Tạo ERD toàn bộ dữ liệu nghiệp vụ

Nhấp phải schema `operational`, chọn `ERD For Schema`. Đây là ERD tổng thể của PORMS vì toàn bộ bảng ứng dụng hiện nằm trong schema này.

## Lệnh kiểm tra nhanh

```sql
SELECT schema_name
FROM information_schema.schemata
WHERE schema_name IN ('operational', 'analytics');

SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'operational'
ORDER BY table_name;
```
