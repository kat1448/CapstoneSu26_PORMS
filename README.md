# PORMS — Port Operation Risk Management System

> **Hệ thống Hỗ trợ Quyết định và Cảnh báo Rủi ro Vận hành Cảng biển dựa trên Dữ liệu Thời tiết Thời gian Thực**

[![Backend CI](https://github.com/<org>/porms/actions/workflows/ci-backend.yml/badge.svg)](https://github.com/<org>/porms/actions)
[![Frontend CI](https://github.com/<org>/porms/actions/workflows/ci-frontend.yml/badge.svg)](https://github.com/<org>/porms/actions)

---

## Mục lục

- [Giới thiệu](#giới-thiệu)
- [Tính năng chính](#tính-năng-chính)
- [Kiến trúc hệ thống](#kiến-trúc-hệ-thống)
- [Tech Stack](#tech-stack)
- [Cài đặt & Chạy](#cài-đặt--chạy)
- [Thành viên nhóm](#thành-viên-nhóm)
- [Tài liệu liên quan](#tài-liệu-liên-quan)

---

## Giới thiệu

Hoạt động cảng biển tại Việt Nam phụ thuộc lớn vào điều kiện thời tiết, nhưng nhiều cảng vẫn ra quyết định dựa trên **kinh nghiệm cá nhân** và **nguồn dữ liệu rời rạc** — dẫn đến phản ứng chậm, thiếu nhất quán và không có audit trail.

**PORMS** giải quyết bài toán đó bằng một pipeline tự động hóa hoàn chỉnh:

```
Thời tiết (OpenWeather)
    → Risk Engine (Beaufort Scale)
        → SOP Engine (Rule-based)
            → Operation Mode (State Machine)
                → Tasks + Alerts + BI Dashboard
```

Hệ thống thu thập dữ liệu thời tiết mỗi 15 phút, tự động đánh giá mức rủi ro, kích hoạt quy trình vận hành (SOP) tương ứng và cảnh báo người vận hành theo thời gian thực.

---

## Tính năng chính

| # | Tính năng | Mô tả |
|---|-----------|-------|
| F1 | **User Management & Auth** | JWT authentication, RBAC 3 vai trò: Admin / Company Admin / Operator |
| F2 | **Port & Zone Management** | Quản lý cảng và vùng (Dock/Yard/Gate), bản đồ tọa độ Leaflet |
| F3 | **Weather Data Processing** | Thu thập OpenWeather API mỗi 15 phút, chuẩn hóa m/s → Beaufort |
| F4 | **Risk Engine** | Đánh giá rủi ro đa yếu tố (gió + mưa), ngưỡng configurable |
| F5 | **SOP Engine** | Tự động kích hoạt quy trình vận hành theo risk level × zone type |
| F6 | **Task Auto-generation** | Tự tạo task log khi SOP trigger, ghi nhận action type và zone |
| F7 | **Operation Mode** | State machine NORMAL → LIMITED → STOP, admin có thể override |
| F8 | **Zone Control** | Restriction flags theo risk level, banner cảnh báo per-zone |
| F9 | **Alert & Notification** | Cảnh báo real-time qua polling, mark-read, lọc theo mức độ |
| F10 | **Operation Logging** | Audit trail toàn bộ events, filter theo ngày/loại/zone |
| F11 | **Realtime Dashboard** | Weather card, Risk badge (đổi màu), Mode indicator, Chart.js |
| F12 | **Weather Orchestration** | Prefect thu thập thời tiết định kỳ và gọi Risk Engine |
| F13 | **Simulation Mode** | Replay dữ liệu lịch sử với speed multiplier — dùng cho demo |

---

## Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────────┐
│                    TẦNG NGUỒN DỮ LIỆU                       │
│  OpenWeather API (mỗi 15 phút)    Admin Portal (config)     │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│                    ETL PIPELINE (Prefect)                    │
│  weather_collector → operational.weather_readings           │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│                    CORE ENGINE (ASP.NET Core)                │
│                                                             │
│  Risk Engine ──RiskChangedEvent──► SOP Engine               │
│      │                                  │                   │
│      ▼                                  ▼                   │
│  RiskAssessment                  OperationMode (State)      │
│                                  TaskGenerator              │
│                                  AlertService               │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│                    PERSISTENCE                               │
│  PostgreSQL: schema operational                            │
└────────────────────┬────────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────────┐
│   React Dashboard — theo dõi và xử lý nghiệp vụ vận hành   │
└─────────────────────────────────────────────────────────────┘
```

Hệ thống dùng một luồng dữ liệu vận hành thống nhất: Prefect thu thập thời tiết,
ASP.NET Core đánh giá rủi ro và PostgreSQL lưu dữ liệu trong schema `operational`.

---

## Tech Stack

| Layer | Công nghệ | Phiên bản |
|-------|-----------|-----------|
| Frontend | React + TypeScript | 18 / 5.x |
| Frontend | Vite, TailwindCSS, Chart.js, Leaflet.js | latest |
| Backend | ASP.NET Core | 8.0 |
| Backend | Entity Framework Core, PostgreSQL | 8.0 / 16 |
| ETL | Python + Prefect + Pandas | 3.11 / 2.x |
| Database | PostgreSQL | 16 |
| DevOps | Docker + Compose, GitHub Actions | — |
| Hosting | Vercel (FE) + AWS EC2 t3.small (BE/DB) | — |

---

## Cài đặt & Chạy

### Yêu cầu

- Docker Desktop (≥ 4.x)
- .NET SDK 8.0
- Node.js 20+
- Python 3.11+
- Git

### Bước 1 — Clone và cấu hình môi trường

```bash
git clone https://github.com/<org>/porms.git
cd porms

# Tạo file .env từ template và điền các giá trị
cp .env.example .env
```

Các biến cần điền trong `.env`:

```env
# OpenWeather
OPENWEATHER_API_KEY=your_api_key_here

# Database
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=porms
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_password_here

# JWT
JWT_SECRET=your_secret_key_min_32_chars
JWT_EXPIRY_MINUTES=60

```

### Bước 2 — Khởi động services nền

```bash
# Khởi động PostgreSQL và Prefect
docker compose up -d

# Kiểm tra tất cả services đang chạy
docker compose ps
```

### Bước 3 — Setup Database

```bash
cd backend

# Chạy EF Core migration (tạo schema operational)
dotnet ef database update \
  --project PORMS.Infrastructure \
  --startup-project PORMS.API

# Seed dữ liệu mẫu
psql -h localhost -U postgres -d porms -f ../scripts/seed_data.sql
psql -h localhost -U postgres -d porms -f ../scripts/seed_sop_rules.sql
```

### Bước 4 — Chạy Backend

```bash
cd backend/PORMS.API
dotnet run
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

### Bước 5 — Chạy Frontend

```bash
cd frontend
npm install
npm run dev
# App: http://localhost:5173
```

### Bước 6 — Chạy ETL (tùy chọn khi phát triển)

```bash
cd etl
pip install -r requirements.txt

# Chạy một lần để test
python flows/weather_collector.py

# Hoặc để Prefect tự schedule (sau khi Prefect server đã chạy qua Docker)
prefect deploy --all
```

### Tài khoản mặc định (sau seed)

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@porms.vn | Admin@123 |
| Company Admin | cadmin@danangport.vn | Admin@123 |
| Operator | operator@danangport.vn | Admin@123 |

---

## Kịch bản Demo (10 phút)

Demo sử dụng **Simulation Mode** để replay dữ liệu bão Đà Nẵng tháng 10/2023 (không phụ thuộc thời tiết thực tế ngày bảo vệ).

```
0–1 phút   Mở Dashboard — giải thích layout: weather card, risk badge, mode indicator, alert bell
1–3 phút   Kích hoạt Simulation Mode với dataset bão cấp 9 (gió 22 m/s, mưa 35 mm/h)
3–5 phút   Theo dõi Risk badge: LOW → MEDIUM → HIGH → CRITICAL (real-time)
5–6 phút   Operation Mode tự động: NORMAL → LIMITED → STOP, alert bell đổ thông báo
6–7 phút   Mở SOP/Task log: xem actions được tạo tự động (dừng bốc xếp Dock A...)
7–9 phút   Xem lịch sử vận hành, dự báo và báo cáo trên cổng PORMS
9–10 phút  Admin UI: chỉnh ngưỡng Beaufort từ 8→7, replay lại sim, thấy hệ thống phản ứng khác
```

---

## Thành viên nhóm

| Vai trò | Họ tên | MSSV | Module phụ trách |
|---------|--------|------|-----------------|
| PM · DE | Nguyễn Phan Anh Minh | HE153552 | Project Management, ETL Pipeline |
| FE — A | Nguyễn Anh Kiệt | DE170152 | Toàn bộ Frontend: Dashboard, Auth, Port/Zone, Alert, Chart.js |
| BE — B | Trần Quang Dũng | DE170780 | Auth (JWT/RBAC), Port & Zone CRUD, Zone Control, User Management |
| BE — C | Đinh Hải Quân | DE180741 | Weather Integration, Risk Engine, BackgroundService, Simulation Mode |
| BE — D | Võ Văn Kiên | DE170297 | SOP Engine, Operation Mode, Task Generator, Alert API, Operation Log |

**Giảng viên hướng dẫn:** ThS. Nguyễn Thị Hạnh — hanhnt54@fe.edu.vn  
**Lớp:** CP_SEP490 — Trường Đại học FPT Đà Nẵng  
**Thời gian:** 04/05/2026 – 31/08/2026

---

## Tài liệu liên quan

| Tài liệu | Mô tả |
|----------|-------|
| [`CONTRIBUTING.md`](./CONTRIBUTING.md) | Cấu trúc folder chi tiết + Git Workflow |
| [`docs/PORMS_Project_Overview.docx`](./docs/PORMS_Project_Overview.docx) | Kiến trúc, feature list, risk management |
| [`docs/api-contracts/`](./docs/api-contracts/) | Swagger YAML cho từng module |
| [`docs/database/schema.sql`](./docs/database/schema.sql) | DDL cho schema nghiệp vụ `operational` |

---

## Milestone

| Tuần | Milestone | Deliverable chính |
|------|-----------|------------------|
| T1 | Kickoff Complete | API contracts chốt, DB schema merged, docker-compose chạy |
| T3 | Auth & ETL Live | JWT login hoạt động, Prefect fetch weather data |
| T5 | Core Chain Working | Weather → Risk → SOP → Mode trigger end-to-end |
| T7 | Dashboard Live | React dashboard kết nối dữ liệu vận hành |
| T9 | Feature Complete | F1–F14 hoàn thành, FE kết nối API thật toàn bộ |
| T10 | Release Ready | Deploy production, demo rehearse ≥ 3 lần thành công |

---

*PORMS · CP_SEP490 · FPT University Da Nang · 2026*
